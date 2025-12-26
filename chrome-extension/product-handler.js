// 개별 상품 페이지 전용 핸들러
console.log('🔥🔥🔥 product-handler.js 로드됨 - ', window.location.href);

// ⭐ 서버로 로그 전송 함수 추가
function sendLogToServer(message) {
  try {
    fetch('http://localhost:8080/api/smartstore/log', {
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

// ⭐ 즉시 실행 (페이지 로드 전에도)
forceWindowResize();

// ⭐ 다중 안전장치: 여러 시점에서 반복 실행
setTimeout(forceWindowResize, 50);   // 0.05초 후
setTimeout(forceWindowResize, 100);  // 0.1초 후
setTimeout(forceWindowResize, 200);  // 0.2초 후
setTimeout(forceWindowResize, 500);  // 0.5초 후
setTimeout(forceWindowResize, 1000); // 1초 후
setTimeout(forceWindowResize, 2000); // 2초 후

// ⭐ 페이지 로드 이벤트에서도 실행
document.addEventListener('DOMContentLoaded', forceWindowResize);
window.addEventListener('load', forceWindowResize);

// ⭐ 지속적 감시: 창이 다른 위치로 이동하면 다시 우하단으로
setInterval(() => {
  const currentX = window.screenX;
  const currentY = window.screenY;
  const targetX = window.screen.availWidth - 220;
  const targetY = window.screen.availHeight - 320;
  
  // 위치가 우하단이 아니면 다시 이동
  if (Math.abs(currentX - targetX) > 50 || Math.abs(currentY - targetY) > 50) {
    forceWindowResize();
  }
}, 1000); // 1초마다 위치 체크

// 페이지 로드 완료 대기
if (document.readyState === 'loading') {
  document.addEventListener('DOMContentLoaded', initProductHandler);
} else {
  initProductHandler();
}

async function initProductHandler() {
  try {
    const url = window.location.href;
    console.log('🔥 상품 페이지 핸들러 시작:', url);
    sendLogToServer(`🔥 상품 페이지 핸들러 시작: ${url}`);
    
    // URL에서 스토어ID와 상품ID 추출
    const storeMatch = url.match(/smartstore\.naver\.com\/([^\/]+)/);
    const productMatch = url.match(/products\/(\d+)/);
    
    if (!storeMatch || !productMatch) {
      console.log('❌ 스토어ID 또는 상품ID 추출 실패');
      sendLogToServer(`❌ 스토어ID 또는 상품ID 추출 실패: ${url}`);
      return;
    }
    
    const storeId = storeMatch[1];
    const productId = productMatch[1];
    
    console.log(`🎯 상품 데이터 수집 시작: ${storeId}/${productId}`);
    sendLogToServer(`🎯 상품 데이터 수집 시작: ${storeId}/${productId}`);
    
    // 2초 대기 후 데이터 수집
    setTimeout(async () => {
      await collectProductPageData(storeId, productId);
    }, 2000);
    
  } catch (error) {
    console.error('❌ 상품 핸들러 오류:', error);
    sendLogToServer(`❌ 상품 핸들러 오류: ${error.message}`);
  }
}

// 상품 페이지에서 데이터 수집
async function collectProductPageData(storeId, productId) {
  try {
    console.log(`🔍 ${storeId}/${productId}: 데이터 수집 시작`);
    
    // 1. 가격 정보 먼저 추출 (필터링용)
    const priceResult = await extractProductPrice(storeId, productId);
    
    // 가격 필터링 실패 시 다른 데이터 수집 중단
    if (!priceResult || priceResult.filtered) {
      console.log(`🚫 ${storeId}/${productId}: 가격 필터링으로 제외됨`);
      setTimeout(() => {
        window.close();
      }, 500);
      return;
    }
    
    // 2. 상품 이미지 추출
    const imageData = await extractProductImage(storeId, productId);
    
    // 3. 상품명 추출  
    const nameData = await extractProductName(storeId, productId);
    
    // 4. 리뷰 데이터 추출
    const reviewData = await extractProductReviews(storeId, productId);
    
    console.log(`✅ ${storeId}/${productId}: 데이터 수집 완료`);
    
    // 2초 후 탭 닫기
    setTimeout(() => {
      window.close();
    }, 2000);
    
  } catch (error) {
    console.error(`❌ ${storeId}/${productId}: 데이터 수집 실패:`, error);
    // 오류 시에도 탭 닫기
    setTimeout(() => {
      window.close();
    }, 1000);
  }
}

// 상품 이미지 추출
async function extractProductImage(storeId, productId) {
  try {
    // 대표 이미지 선택자들
    const selectors = [
      '.bd_2DO68 img[alt="대표이미지"]',
      '.bd_2DO68 img',
      '.product_thumb img',
      '.thumb_area img',
      '.product_image img'
    ];
    
    let imageElement = null;
    for (const selector of selectors) {
      imageElement = document.querySelector(selector);
      if (imageElement && imageElement.src) break;
    }
    
    if (!imageElement || !imageElement.src) {
      console.log(`❌ ${storeId}/${productId}: 상품 이미지 없음`);
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
    
    await fetch('http://localhost:8080/api/smartstore/image', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(imageData)
    });
    
    console.log(`✅ ${storeId}/${productId}: 이미지 전송 완료`);
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
    
    await fetch('http://localhost:8080/api/smartstore/product-name', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(nameData)
    });
    
    console.log(`✅ ${storeId}/${productId}: 상품명 전송 완료`);
    return nameData;
    
  } catch (error) {
    console.error(`❌ ${storeId}/${productId}: 상품명 추출 실패:`, error);
    return null;
  }
}

// 리뷰 데이터 추출
async function extractProductReviews(storeId, productId) {
  try {
    // 리뷰 영역 대기
    await new Promise(resolve => setTimeout(resolve, 3000));
    
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
    
    await fetch('http://localhost:8080/api/smartstore/reviews', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(reviewData)
    });
    
    console.log(`✅ ${storeId}/${productId}: 리뷰 전송 완료`);
    return reviewData;
    
  } catch (error) {
    // 조용한 처리 - 리뷰 추출 실패
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
      
      const response = await fetch('http://localhost:8080/api/smartstore/product-price', {
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
    
    return null;
  } catch (error) {
    return null;
  }
}
