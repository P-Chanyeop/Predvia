// ⭐ localhost 프록시 함수 (CORS 우회)
async function localFetch(url, options = {}) {
    return new Promise((resolve, reject) => {
        chrome.runtime.sendMessage(
            { action: 'proxyFetch', url, method: options.method || 'GET', body: options.body ? (typeof options.body === 'string' ? options.body : JSON.stringify(options.body)) : null },
            (resp) => {
                if (chrome.runtime.lastError) { reject(new Error(chrome.runtime.lastError.message)); return; }
                if (!resp || !resp.success) { reject(new Error(resp?.error || 'proxyFetch failed')); return; }
                resolve({ ok: resp.status >= 200 && resp.status < 300, status: resp.status, json: () => Promise.resolve(resp.data), text: () => Promise.resolve(typeof resp.data === 'string' ? resp.data : JSON.stringify(resp.data)) });
            }
        );
    });
}

// 개별 상품 페이지 전용 핸들러
console.log('🔥🔥🔥 product-handler.js 로드됨 - ', window.location.href);

// ⭐ 서버로 로그 전송 함수 추가
function sendLogToServer(message) {
  try {
    localFetch('http://localhost:8080/api/smartstore/log', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ message: message, timestamp: new Date().toISOString() })
    }).catch(() => {}); // 조용한 처리
  } catch (error) {
    // 조용한 처리 - 오류 시 콘솔 스팸 방지
  }
}

// ⭐ 페이지 로드 후 창 크기 및 위치 강제 조절 (우하단 최소 크기)
function forceWindowResize() {
  try {
    window.resizeTo(200, 300);
    const screenWidth = window.screen.availWidth;
    const screenHeight = window.screen.availHeight;
    const windowWidth = 200;
    const windowHeight = 300;
    
    // 우하단 위치 계산 (여백 20px)
    const x = screenWidth - windowWidth - 20;
    const y = screenHeight - windowHeight - 20;
    
    window.moveTo(x, y);
    
    // 포커싱 방지: 창을 백그라운드로 보내기
    window.blur();
    
    console.log(`🔧 상품페이지 창 크기 조절: ${windowWidth}x${windowHeight} at (${x}, ${y})`);
  } catch (error) {
    console.log('⚠️ 창 크기 조절 실패:', error.message);
  }
}

// ⭐ 즉시 실행하지 않고, 크롤링 활성 시에만 창 크기 조절
async function initWindowResize() {
  try {
    const statusResp = await localFetch('http://localhost:8080/api/smartstore/status');
    const statusData = await statusResp.json();
    if (!statusData.isCrawlingActive) return; // 크롤링 비활성이면 스킵
  } catch (e) { return; }

  forceWindowResize();
  setTimeout(forceWindowResize, 100);
  setTimeout(forceWindowResize, 500);
  setTimeout(forceWindowResize, 1000);
  setTimeout(forceWindowResize, 2000);

  document.addEventListener('DOMContentLoaded', forceWindowResize);
  window.addEventListener('load', forceWindowResize);

  setInterval(() => {
    const currentX = window.screenX;
    const currentY = window.screenY;
    const targetX = window.screen.availWidth - 220;
    const targetY = window.screen.availHeight - 320;
    if (Math.abs(currentX - targetX) > 50 || Math.abs(currentY - targetY) > 50) {
      forceWindowResize();
    }
  }, 1000);
}
initWindowResize();

// 페이지 로드 완료 대기
if (document.readyState === 'loading') {
  document.addEventListener('DOMContentLoaded', initProductHandler);
} else {
  initProductHandler();
}

async function initProductHandler() {
  if (window.__PRODUCT_HANDLER_RUNNING__) return;
  window.__PRODUCT_HANDLER_RUNNING__ = true;
  try {
    // ⭐ 서버 상태 확인
    let v2Mode = false;
    try {
      const statusResp = await localFetch('http://localhost:8080/api/smartstore/status');
      const statusData = await statusResp.json();
      v2Mode = statusData.v2Mode || false;
      if (!statusData.isCrawlingActive && !v2Mode) {
        console.log('ℹ️ 크롤링 비활성 - 핸들러 스킵');
        return;
      }
    } catch (e) {
      console.log('ℹ️ 서버 연결 불가 - 핸들러 스킵');
      return;
    }

    const url = window.location.href;
    const storeMatch = url.match(/smartstore\.naver\.com\/([^\/]+)/);
    const productMatch = url.match(/products\/(\d+)/);
    
    if (!storeMatch || !productMatch) {
      console.log('❌ 스토어ID 또는 상품ID 추출 실패');
      return;
    }
    
    const storeId = storeMatch[1];
    const productId = productMatch[1];
    
    console.log(`🎯 상품 데이터 수집 시작: ${storeId}/${productId}` + (v2Mode ? ' [v2]' : ''));
    
    // 0.5초 대기 후 데이터 수집
    setTimeout(async () => {
      await collectProductPageData(storeId, productId);
    }, 500);
    
  } catch (error) {
    console.error('❌ 상품 핸들러 오류:', error);
    sendLogToServer(`❌ 상품 핸들러 오류: ${error.message}`);
  }
}

// ⭐ 페이지 완전 로딩 대기
async function waitForPageLoad() {
  return new Promise((resolve) => {
    if (document.readyState === 'complete') {
      resolve();
    } else {
      window.addEventListener('load', resolve);
    }
  });
}

// ⭐ 특정 요소가 나타날 때까지 대기
async function waitForElement(selector, timeout = 5000) {
  const start = Date.now();
  while (Date.now() - start < timeout) {
    const element = document.querySelector(selector);
    if (element) return element;
    await new Promise(r => setTimeout(r, 100));
  }
  return null;
}

// 상품 페이지에서 데이터 수집
async function collectProductPageData(storeId, productId) {
  try {
    console.log(`🔍 ${storeId}/${productId}: 데이터 수집 시작`);
    
    // ⭐ 페이지 완전 로딩 대기
    await waitForPageLoad();
    sendLogToServer(`📄 ${storeId}/${productId}: 페이지 로딩 완료`);
    
    // ⭐ 추가 대기 (동적 콘텐츠 로딩)
    // await new Promise(r => setTimeout(r, 1000));
    
    // ⭐ 카테고리 요소 대기 (최대 5초)
    await waitForElement('ul.ySOklWNBjf', 1000);
    
    // 1. 가격 정보 먼저 추출 (필터링용)
    const priceResult = await extractProductPrice(storeId, productId);
    
    // 가격 필터링으로 제외된 경우만 중단 (가격 추출 실패는 계속 진행)
    if (priceResult && priceResult.filtered) {
      console.log(`🚫 ${storeId}/${productId}: 가격 필터링으로 제외됨`);
      // [v2] 필터링된 상품도 보고 (hasImage/hasName = false로)
      const filteredPrice = parseInt(String(priceResult.price).replace(/[^0-9]/g, '')) || 0;
      v2ReportProductData(storeId, productId, filteredPrice, false, false);
      setTimeout(() => {
        window.close();
      }, 500);
      return;
    }
    
    // ⭐ 재시도 포함 데이터 추출
    let imageData = await extractProductImage(storeId, productId);
    let nameData = await extractProductName(storeId, productId);
    let reviewData = await extractProductReviews(storeId, productId);
    let categoryData = await extractProductCategories(storeId, productId);
    
    // ⭐ 실패한 항목 1회 재시도
    if (!imageData || !nameData || !categoryData) {
      sendLogToServer(`🔄 ${storeId}/${productId}: 일부 실패 - 0.5초 후 재시도`);
      await new Promise(r => setTimeout(r, 500));
      
      if (!imageData) imageData = await extractProductImage(storeId, productId);
      if (!nameData) nameData = await extractProductName(storeId, productId);
      if (!categoryData) categoryData = await extractProductCategories(storeId, productId);
    }
    
    // ⭐ 모든 추출 완료 확인 로그
    sendLogToServer(`✅ ${storeId}/${productId}: 추출 완료 (이미지:${!!imageData}, 상품명:${!!nameData}, 리뷰:${!!reviewData}, 카테고리:${!!categoryData})`);
    
    // [v2] 서버 주도 크롤링에 상품 데이터 보고
    const priceNum = priceResult && priceResult.price ? parseInt(String(priceResult.price).replace(/[^0-9]/g, '')) || 0 : 0;
    v2ReportProductData(storeId, productId, priceNum, !!imageData, !!nameData);
    
    console.log(`✅ ${storeId}/${productId}: 데이터 수집 완료`);
    
    // ⭐ 서버에 상품 처리 완료 신호 전송
    await localFetch('http://localhost:8080/api/smartstore/product-done', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ storeId, productId })
    }).catch(e => {});
    
    // ⭐ 탭 닫기
    setTimeout(() => {
      window.close();
    }, 500);
    
  } catch (error) {
    console.error(`❌ ${storeId}/${productId}: 데이터 수집 실패:`, error);
    sendLogToServer(`❌ ${storeId}/${productId}: 데이터 수집 실패 - ${error.message}`);
    
    // [v2] 실패도 보고
    v2ReportProductData(storeId, productId, 0, false, false);
    
    // ⭐ 실패해도 완료 신호 전송 (다음 상품 진행)
    await localFetch('http://localhost:8080/api/smartstore/product-done', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ storeId, productId })
    }).catch(e => {});
    
    setTimeout(() => {
      window.close();
    }, 500);
  }
}

// 상품 이미지 추출
async function extractProductImage(storeId, productId) {
  try {
    sendLogToServer(`🔍 ${storeId}/${productId}: 이미지 추출 시작`);
    
    // 대표 이미지 선택자들
    const selectors = [
      '.bd_2DO68 img[alt="대표이미지"]',
      '.bd_2DO68 img',
      'img[alt="대표이미지"]',
      '.product_thumb img',
      '.thumb_area img',
      '.product_image img'
    ];
    
    let imageElement = null;
    for (const selector of selectors) {
      imageElement = document.querySelector(selector);
      if (imageElement && imageElement.src) {
        sendLogToServer(`🔍 ${storeId}/${productId}: 선택자 ${selector}로 이미지 발견`);
        break;
      }
    }
    
    if (!imageElement || !imageElement.src) {
      sendLogToServer(`❌ ${storeId}/${productId}: 상품 이미지 없음`);
      return null;
    }
    
    const imageUrl = imageElement.src;
    console.log(`🖼️ ${storeId}/${productId}: 이미지 발견 - ${imageUrl}`);
    
    // 서버로 이미지 데이터 전송
    const imageData = {
      storeId: storeId,
      productId: productId,
      imageUrl: imageUrl,
      timestamp: new Date().toISOString()
    };
    
    await localFetch('http://localhost:8080/api/smartstore/image', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(imageData)
    }).catch(e => console.log('이미지 전송 오류:', e.message));
    
    sendLogToServer(`✅ ${storeId}/${productId}: 이미지 전송 완료`);
    return imageData;
    
  } catch (error) {
    console.error(`❌ ${storeId}/${productId}: 이미지 추출 실패:`, error);
    return null;
  }
}

// 상품명 추출
async function extractProductName(storeId, productId) {
  try {
    // 상품명 선택자들
    const selectors = [
      '.DCVBehA8ZB',
      'h3._copyable',
      '.product_title',
      '.prod_name',
      'h1'
    ];
    
    let nameElement = null;
    for (const selector of selectors) {
      nameElement = document.querySelector(selector);
      if (nameElement && nameElement.textContent.trim()) break;
    }
    
    if (!nameElement) {
      console.log(`❌ ${storeId}/${productId}: 상품명 없음`);
      return null;
    }
    
    const productName = nameElement.textContent.trim();
    console.log(`📝 ${storeId}/${productId}: 상품명 발견 - ${productName}`);
    
    // 서버로 상품명 데이터 전송
    const nameData = {
      storeId: storeId,
      productId: productId,
      productName: productName,
      timestamp: new Date().toISOString()
    };
    
    await localFetch('http://localhost:8080/api/smartstore/product-name', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(nameData)
    }).catch(e => console.log('상품명 전송 오류:', e.message));
    
    sendLogToServer(`✅ ${storeId}/${productId}: 상품명 전송 완료`);
    return nameData;
    
  } catch (error) {
    console.error(`❌ ${storeId}/${productId}: 상품명 추출 실패:`, error);
    return null;
  }
}

// 리뷰 데이터 추출
async function extractProductReviews(storeId, productId) {
  try {
    sendLogToServer(`⭐ ${storeId}/${productId}: 리뷰 추출 시작`);
    
    // 리뷰 영역 대기 (0.5초로 단축)
    await new Promise(resolve => setTimeout(resolve, 500));
    
    // 별점 선택자들
    const ratingSelectors = [
      'em.n6zq2yy0KA',
      '.rating_star em',
      '.review_rating em'
    ];
    
    // 리뷰 내용 선택자들  
    const contentSelectors = [
      '.vhlVUsCtw3 .K0kwJOXP06',
      '.review_content',
      '.review_text'
    ];
    
    const reviews = [];
    
    // 별점 추출
    let ratingElements = [];
    for (const selector of ratingSelectors) {
      ratingElements = document.querySelectorAll(selector);
      if (ratingElements.length > 0) break;
    }
    
    // 리뷰 내용 추출
    let contentElements = [];
    for (const selector of contentSelectors) {
      contentElements = document.querySelectorAll(selector);
      if (contentElements.length > 0) break;
    }
    
    console.log(`🔍 ${storeId}/${productId}: 별점 ${ratingElements.length}개, 내용 ${contentElements.length}개 발견`);
    
    // 리뷰 데이터 조합
    const maxReviews = Math.min(ratingElements.length, contentElements.length, 10);
    for (let i = 0; i < maxReviews; i++) {
      const rating = ratingElements[i]?.textContent?.trim() || '5';
      const content = contentElements[i]?.textContent?.trim() || '';
      
      if (content) {
        reviews.push({
          rating: rating,
          content: content
        });
      }
    }
    
    console.log(`📊 ${storeId}/${productId}: ${reviews.length}개 리뷰 수집`);
    
    // 서버로 리뷰 데이터 전송
    const reviewData = {
      storeId: storeId,
      productId: productId,
      reviews: reviews,
      reviewCount: reviews.length,
      timestamp: new Date().toISOString(),
      productUrl: window.location.href
    };
    
    await localFetch('http://localhost:8080/api/smartstore/reviews', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(reviewData)
    }).catch(e => console.log('리뷰 전송 오류:', e.message));
    
    sendLogToServer(`✅ ${storeId}/${productId}: 리뷰 ${reviews.length}개 전송 완료`);
    return reviewData;
    
  } catch (error) {
    sendLogToServer(`❌ ${storeId}/${productId}: 리뷰 추출 오류 - ${error.message}`);
    return null;
  }
}

// 가격 정보 추출
async function extractProductPrice(storeId, productId) {
  try {
    // 네이버 스마트스토어 정확한 상품 가격 선택자만
    const selectors = [
      'strong.Xu9MEKUuIo span.e1DMQNBPJ_', // 최우선: 정확한 상품 가격
      '.Xu9MEKUuIo .e1DMQNBPJ_',        // 상품 가격 컨테이너
      'span.e1DMQNBPJ_',                // 가격 숫자 span
      '.bd_15LKy'                       // 대안 가격 선택자
    ];
    
    // "상품 가격" 텍스트가 있는 정확한 가격 요소 찾기
    let foundPrice = null;
    
    // 1. "상품 가격" span을 포함한 strong 요소 찾기
    const priceElements = document.querySelectorAll('strong');
    for (const strong of priceElements) {
      const blindSpan = strong.querySelector('span.blind');
      if (blindSpan && blindSpan.textContent?.includes('상품 가격')) {
        // 가격 숫자가 있는 span 찾기
        const priceSpan = strong.querySelector('span.e1DMQNBPJ_');
        const wonSpan = strong.querySelector('span.won');
        
        if (priceSpan && wonSpan) {
          const priceNumber = priceSpan.textContent?.trim();
          if (priceNumber && /^\d{1,3}(?:,\d{3})*$/.test(priceNumber)) {
            foundPrice = priceNumber + '원';
            console.log(`✅ "상품 가격" 요소에서 발견: ${foundPrice}`);
            break;
          }
        }
      }
    }
    
    // 1-1. 빨간색 가격 클래스로 찾기 (.Xu9MEKUuIo.s6EKUu28OE - #d40022 색상)
    if (!foundPrice) {
      const redPriceElements = document.querySelectorAll('.Xu9MEKUuIo.s6EKUu28OE');
      for (const element of redPriceElements) {
        const blindSpan = element.querySelector('span.blind');
        if (blindSpan && blindSpan.textContent?.includes('상품 가격')) {
          const priceSpan = element.querySelector('span.e1DMQNBPJ_');
          const wonSpan = element.querySelector('span.won');
          
          if (priceSpan && wonSpan) {
            const priceNumber = priceSpan.textContent?.trim();
            if (priceNumber && /^\d{1,3}(?:,\d{3})*$/.test(priceNumber)) {
              foundPrice = priceNumber + '원';
              console.log(`✅ 빨간색 가격 클래스에서 발견: ${foundPrice}`);
              break;
            }
          }
        }
      }
    }
    
    // 2. 대안: 기존 선택자들
    if (!foundPrice) {
      const selectors = [
        'strong.Xu9MEKUuIo span.e1DMQNBPJ_',
        '.Xu9MEKUuIo .e1DMQNBPJ_',
        'span.e1DMQNBPJ_',
        '.bd_15LKy'
      ];
      
      for (const selector of selectors) {
        const elements = document.querySelectorAll(selector);
        
        for (const element of elements) {
          const text = element.textContent?.trim();
          if (text && text.includes('원') && /\d{1,3}(?:,\d{3})*\s*원/.test(text)) {
            const match = text.match(/(\d{1,3}(?:,\d{3})*)\s*원/);
            if (match) {
              foundPrice = match[0];
              console.log(`✅ 대안 선택자에서 발견: ${foundPrice} (${selector})`);
              break;
            }
          }
        }
        
        if (foundPrice) break;
      }
    }
    
    if (foundPrice) {
      const priceData = {
        storeId: storeId,
        productId: productId,
        price: foundPrice,
        timestamp: new Date().toISOString(),
        productUrl: window.location.href
      };
      
      const response = await localFetch('http://localhost:8080/api/smartstore/product-price', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(priceData)
      });
      
      const result = await response.json();
      
      // 필터링 결과 반환
      if (result.filtered) {
        return { filtered: true, price: foundPrice };
      }
      
      // 성공 시 priceData 반환, 실패 시 null
      return (result.success !== false) ? priceData : null;
    }
    
    const message = `❌ ${storeId}/${productId}: 가격 추출 실패 - 모든 선택자에서 가격 정보를 찾을 수 없음`;
    console.log(message);
    sendLogToServer(message);
    return null;
  } catch (error) {
    const message = `❌ ${storeId}/${productId}: 가격 추출 오류 - ${error.message}`;
    console.log(message);
    sendLogToServer(message);
    return null;
  }
}


// ⭐ 카테고리 추출
async function extractProductCategories(storeId, productId) {
  try {
    sendLogToServer(`⭐ ${storeId}/${productId}: 카테고리 추출 시작`);
    
    // ⭐ 상품 브레드크럼(경로)에서만 카테고리 추출
    const breadcrumb = document.querySelector('ul.ySOklWNBjf');
    const categories = [];
    
    if (breadcrumb) {
      const items = breadcrumb.querySelectorAll('li');
      items.forEach(li => {
        // 텍스트만 추출 (하위 메뉴 있음, 총 X개 등 제거)
        let text = '';
        const span = li.querySelector('span.sAla67hq4a, span._copyable');
        if (span) {
          text = span.textContent.trim();
        } else {
          // span이 없으면 li 직접 텍스트
          text = li.textContent
            .replace(/하위 메뉴 있음/g, '')
            .replace(/\(총\s*\d+개\)/g, '')
            .replace(/카테고리 더보기/g, '')
            .trim();
        }
        
        if (text && text !== '홈' && text !== 'Home' && text !== '전체상품' && text.length > 0 && !categories.includes(text)) {
          categories.push(text);
        }
      });
    }
    
    const categoryString = categories.join(' > ');
    sendLogToServer(`📂 ${storeId}/${productId}: 카테고리 - ${categoryString || '없음'}`);
    
    if (categories.length === 0) {
      return null;
    }
    
    // 서버로 카테고리 데이터 전송
    const categoryData = {
      storeId: storeId,
      productId: productId,
      categoryString: categoryString,
      categories: categories.map((name, index) => ({
        name: name,
        order: index
      })),
      pageUrl: window.location.href,
      extractedAt: new Date().toISOString()
    };
    
    await localFetch('http://localhost:8080/api/smartstore/categories', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(categoryData)
    }).catch(e => console.log('카테고리 전송 오류:', e.message));
    
    sendLogToServer(`✅ ${storeId}/${productId}: 카테고리 전송 완료 - ${categoryString}`);
    return categoryData;
    
  } catch (error) {
    sendLogToServer(`❌ ${storeId}/${productId}: 카테고리 추출 오류 - ${error.message}`);
    return null;
  }
}

// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
// [v2] 서버 주도 크롤링 - 상품 데이터 보고
// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
function v2ReportProductData(storeId, productId, priceValue, hasImage, hasName) {
  chrome.runtime.sendMessage({
    type: 'v2_report',
    data: { type: 'product_data', storeId, productId, priceValue, hasImage, hasName }
  }, (resp) => {
    console.log(`[v2] 상품 데이터 보고: ${storeId}/${productId}`);
  });
}
