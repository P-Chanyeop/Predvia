console.log('🔥 all-products-handler.js 파일 로드됨!');
console.log('🔥 현재 URL:', window.location.href);

// ⭐ 즉시 실행 (페이지 로드 완료 후)
window.addEventListener('load', function() {
  console.log('🔥 페이지 로드 완료 - 핸들러 시작!');
  
  setTimeout(() => {
    console.log('🔥 handleAllProductsPage 호출!');
    
    const storeId = extractStoreIdFromUrl(window.location.href);
    const urlParams = new URLSearchParams(window.location.search);
    const runId = urlParams.get('runId') || 'unknown';
    
    console.log(`🚀 ${storeId}: 핸들러 시작 (runId: ${runId})`);
    
    // 2초 후 리뷰 검색 시작
    setTimeout(async () => {
      await sendLogToServer(`🔍 ${storeId}: 리뷰 검색 시작`);
      
      const productData = await collectProductData(storeId, runId);
      await sendProductDataToServer(storeId, productData, 1);
      
    }, 2000);
    
  }, 2000);
});

// ⭐ 중복 실행 방지 가드 (즉시 실행)
if (window.__ALL_PRODUCTS_HANDLER_RUNNING__) {
  console.log('🚫 all-products-handler 이미 실행 중 - 중복 실행 방지');
} else {
  window.__ALL_PRODUCTS_HANDLER_RUNNING__ = true;
  console.log('✅ all-products-handler 실행 시작 - 가드 설정 완료');
}

// 전체상품 판매많은순 페이지에서 실행되는 스크립트
console.log('🛍️ 전체상품 페이지 핸들러 실행 시작');

// 즉시 서버에 실행 알림
(async function() {
  try {
    await fetch('http://localhost:8080/api/smartstore/log', {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json'
      },
      body: JSON.stringify({
        message: `🚀 전체상품 핸들러 실행: ${window.location.href}`,
        timestamp: new Date().toISOString()
      })
    });
  } catch (e) {
    console.log('초기 로그 전송 실패:', e);
  }
})();

// 페이지 로딩 완료 후 실행
setTimeout(() => {
  handleAllProductsPage();
}, 3000); // 3초로 단축

async function handleAllProductsPage() {
  // ⭐ 중복 실행 체크
  if (window.__ALL_PRODUCTS_HANDLER_RUNNING__) {
    console.log('🚫 handleAllProductsPage 이미 실행 중 - 중복 실행 방지');
    return;
  }
  
  try {
    const storeId = extractStoreIdFromUrl(window.location.href);
    
    // ⭐ URL에서 runId 추출
    const urlParams = new URLSearchParams(window.location.search);
    const runId = urlParams.get('runId') || 'unknown';
    
    console.log(`🚀 ${storeId}: 핸들러 시작 (runId: ${runId})`);
    console.log(`🔗 현재 URL: ${window.location.href}`);
    
    // 즉시 로그 전송
    fetch('http://localhost:8080/api/smartstore/log', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        message: `🚀 ${storeId}: 핸들러 시작 (runId: ${runId})`,
        timestamp: new Date().toISOString()
      })
    }).catch(e => console.log('로그 전송 실패:', e));
    
    await sendLogToServer(`🚀 ${storeId}: 핸들러 시작 (runId: ${runId})`);
    
    // 서버에 전체상품 페이지 접속 알림
    notifyAllProductsPageLoaded(storeId);
    
    // 바로 리뷰 검색 실행
    setTimeout(async () => {
      await sendLogToServer(`🔍 ${storeId}: 리뷰 검색 시작`);
      
      const productData = await collectProductData(storeId, runId);
      sendProductDataToServer(storeId, productData, 1);
      
    }, 2000); // 2초만 대기
    
  } catch (error) {
    const errorMsg = `❌ ${storeId}: 핸들러 오류 - ${error.message}`;
    sendLogToServer(errorMsg);
  }
}

// 로그를 서버로 전송하는 함수 (동기식으로 변경)
async function sendLogToServer(message) {
  try {
    const response = await fetch('http://localhost:8080/api/smartstore/log', {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json'
      },
      body: JSON.stringify({
        message: message,
        timestamp: new Date().toISOString()
      })
    });
    
    console.log('로그 전송:', message);
    
  } catch (error) {
    console.log('로그 전송 실패:', error);
  }
}

// ⭐ 상태 설정 함수
async function setStoreStateFromHandler(storeId, runId, state, lock, expected = 0, progress = 0) {
  try {
    const response = await fetch('http://localhost:8080/api/smartstore/state', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        storeId,
        runId,
        state,
        lock,
        expected,
        progress,
        timestamp: new Date().toISOString()
      })
    });
    
    if (response.ok) {
      console.log(`🔧 ${storeId}: 상태 설정 - ${state} (lock: ${lock}, ${progress}/${expected})`);
    }
  } catch (error) {
    console.log(`❌ ${storeId}: 상태 설정 오류 - ${error.message}`);
  }
}

// ⭐ 진행률 업데이트 함수
async function updateProgress(storeId, runId, inc = 1) {
  try {
    await fetch('http://localhost:8080/api/smartstore/progress', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ storeId, runId, inc })
    });
  } catch (error) {
    console.log(`❌ ${storeId}: 진행률 업데이트 오류 - ${error.message}`);
  }
}

// 상품 데이터 수집 (40개 상품 중 마지막 리뷰 상품 찾기)
async function collectProductData(storeId, runId) {
  try {
    const debugMsg = `🔍 ${storeId}: 리뷰 span 검색 시작`;
    sendLogToServer(debugMsg);
    
    // 정확히 "리뷰" 텍스트를 가진 span 찾기
    const reviewSpans = document.evaluate("//span[normalize-space(text())='리뷰']", document, null, XPathResult.ORDERED_NODE_SNAPSHOT_TYPE, null);
    
    const spanMsg = `📝 ${storeId}: ${reviewSpans.snapshotLength}개 "리뷰" span 발견`;
    sendLogToServer(spanMsg);
    
    if (reviewSpans.snapshotLength === 0) {
      const noSpanMsg = `❌ ${storeId}: "리뷰" span 없음 - 즉시 완료 처리`;
      await sendLogToServer(noSpanMsg);
      
      // ⭐ 즉시 완료 상태로 설정
      await setStoreStateFromHandler(storeId, runId, 'done', false, 0, 0);
      await sendLogToServer(`✅ ${storeId}: 리뷰 없음으로 완료 처리됨`);
      
      return [];
    }
    
    // 1단계: 모든 상품 링크 가져오기
    const allProducts = document.querySelectorAll('a[data-shp-contents-rank]');
    
    // 2단계: 처음 40개 상품에서 리뷰가 있는지 확인하여 마지막 리뷰 rank 찾기
    let lastReviewRank = -1;
    
    for (let i = 0; i < allProducts.length; i++) {
      const productLink = allProducts[i];
      const rank = parseInt(productLink.getAttribute('data-shp-contents-rank'));
      
      // 40개까지만 확인
      if (rank > 40) continue;
      
      // 상품 주변에서 리뷰 span 찾기
      const parent = productLink.parentElement;
      if (parent && parent.textContent.includes('리뷰')) {
        lastReviewRank = Math.max(lastReviewRank, rank);
        const reviewMsg = `🔢 ${storeId}: ${rank}번 상품에 리뷰 발견`;
        sendLogToServer(reviewMsg);
      }
    }
    
    if (lastReviewRank === -1) {
      const noRankMsg = `❌ ${storeId}: 리뷰 상품 없음`;
      sendLogToServer(noRankMsg);
      return [];
    }
    
    const rangeMsg = `✅ ${storeId}: 1번부터 ${lastReviewRank}번째 상품까지 수집 (총 ${lastReviewRank}개)`;
    sendLogToServer(rangeMsg);
    
    // 3단계: 1번부터 lastReviewRank까지 모든 상품 수집 (중복 제거)
    const allProductUrls = [];
    const seenIds = new Set();
    
    for (let i = 0; i < allProducts.length; i++) {
      const productLink = allProducts[i];
      const rank = parseInt(productLink.getAttribute('data-shp-contents-rank'));
      
      if (rank <= lastReviewRank) {
        const productId = productLink.getAttribute('data-shp-contents-id');
        
        if (productId && /^\d{8,}$/.test(productId) && !seenIds.has(productId)) {
          seenIds.add(productId);
          const productUrl = `https://smartstore.naver.com/${storeId}/products/${productId}`;
          allProductUrls.push({ url: productUrl, storeId: storeId, index: rank });
          
          const idMsg = `🆔 ${storeId}: [${rank}번] 상품 ID ${productId} 발견`;
          sendLogToServer(idMsg);
        }
      }
    }
    
    // rank 순서로 정렬
    allProductUrls.sort((a, b) => a.index - b.index);
    
    // 4단계: 실제 상품 접속 시작
    if (allProductUrls.length > 0) {
      const waitMsg = `⏳ ${storeId}: ${allProductUrls.length}개 상품 순차 접속 시작`;
      await sendLogToServer(waitMsg);
      
      // ⭐ visiting 상태로 변경
      await setStoreStateFromHandler(storeId, runId, 'visiting', true, allProductUrls.length, 0);
      
      await visitProductsSequentially(storeId, runId, allProductUrls);
    } else {
      // ⭐ 리뷰 없으면 즉시 완료 처리
      await sendLogToServer(`❌ ${storeId}: 리뷰 없음 - 즉시 완료 처리`);
      await sendProductDataToServer(storeId, [], 0);
      
      // ⭐ 완료 상태로 설정
      await setStoreStateFromHandler(storeId, runId, 'done', false, 0, 0);
    }
    
    return allProductUrls;
    
  } catch (error) {
    const errorMsg = `❌ ${storeId}: 오류 - ${error.message}`;
    sendLogToServer(errorMsg);
    return [];
  }
}

// 리뷰 span에서 상품 ID 찾아서 URL 생성
function findProductIdFromSpan(reviewSpan, storeId) {
  try {
    let container = reviewSpan;
    
    // 부모 요소들을 올라가면서 data-shp-contents-id 찾기
    for (let level = 0; level < 10 && container; level++) {
      
      // 1순위: data-shp-contents-id 속성 찾기
      if (container.getAttribute && container.getAttribute('data-shp-contents-id')) {
        const productId = container.getAttribute('data-shp-contents-id');
        if (productId && /^\d{8,}$/.test(productId)) {
          const url = `https://smartstore.naver.com/${storeId}/products/${productId}`;
          
          const idMsg = `🆔 ${storeId}: data-shp-contents-id에서 상품 ID ${productId} 발견`;
          sendLogToServer(idMsg);
          
          const urlMsg = `🔗 ${storeId}: URL 생성 - ${url}`;
          sendLogToServer(urlMsg);
          
          return url;
        }
      }
      
      // 2순위: 자식 요소들에서 data-shp-contents-id 찾기
      if (container.querySelectorAll) {
        const elementsWithId = container.querySelectorAll('[data-shp-contents-id]');
        
        for (let element of elementsWithId) {
          const productId = element.getAttribute('data-shp-contents-id');
          if (productId && /^\d{8,}$/.test(productId)) {
            const url = `https://smartstore.naver.com/${storeId}/products/${productId}`;
            
            const childMsg = `🆔 ${storeId}: 자식 data-shp-contents-id에서 상품 ID ${productId} 발견`;
            sendLogToServer(childMsg);
            
            const urlMsg = `🔗 ${storeId}: URL 생성 - ${url}`;
            sendLogToServer(urlMsg);
            
            return url;
          }
        }
      }
      
      container = container.parentElement;
    }
    
    // 3순위: href에서 products ID 추출
    const productLinks = document.querySelectorAll('a[href*="/products/"]');
    
    for (let link of productLinks) {
      // 리뷰 span과 연관된 링크인지 확인
      if (link.contains(reviewSpan) || reviewSpan.contains(link) || 
          (link.parentElement && link.parentElement.contains(reviewSpan))) {
        
        const productIdMatch = link.href.match(/\/products\/(\d+)/);
        if (productIdMatch) {
          const productId = productIdMatch[1];
          const url = `https://smartstore.naver.com/${storeId}/products/${productId}`;
          
          const linkMsg = `🔗 ${storeId}: href에서 상품 ID ${productId} 발견`;
          sendLogToServer(linkMsg);
          
          const urlMsg = `🔗 ${storeId}: URL 생성 - ${url}`;
          sendLogToServer(urlMsg);
          
          return url;
        }
      }
    }
    
    return null;
    
  } catch (error) {
    console.log('상품 ID 찾기 오류:', error);
    return null;
  }
}

// 상품 요소에서 리뷰 정보 추출
function extractReviewInfo(productElement) {
  try {
    // 리뷰 관련 텍스트 패턴들
    const reviewPatterns = [
      /(\d+)개?\s*리뷰/i,
      /(\d+)개?\s*후기/i,
      /리뷰\s*(\d+)/i,
      /후기\s*(\d+)/i,
      /(\d+)\s*리뷰/i,
      /(\d+)\s*후기/i,
      /평점.*?(\d+)/i
    ];
    
    const textContent = productElement.textContent || '';
    
    for (let pattern of reviewPatterns) {
      const match = textContent.match(pattern);
      if (match) {
        const count = parseInt(match[1]);
        if (count > 0) {
          return {
            count: count,
            text: match[0]
          };
        }
      }
    }
    
    return null;
    
  } catch (error) {
    return null;
  }
}

// 상품 ID 추출 및 URL 생성
function findProductIdAndGenerateUrl(element, storeId) {
  try {
    let container = element;
    
    // 최대 10단계까지 부모 요소 탐색
    for (let level = 0; level < 10 && container; level++) {
      
      // 1순위: data-shp-contents-id 속성들에서 상품 ID 찾기
      if (container.querySelectorAll) {
        const allElements = container.querySelectorAll('*[data-shp-contents-id]');
        
        for (let element of allElements) {
          const allAttributes = element.attributes;
          for (let attr of allAttributes) {
            // 숫자로만 이루어진 긴 값 찾기 (상품 ID 패턴)
            if (attr.value && /^\d{8,}$/.test(attr.value)) {
              const productId = attr.value;
              const generatedUrl = `https://smartstore.naver.com/${storeId}/products/${productId}`;
              
              const idMsg = `🆔 ${storeId}: 상품 ID ${productId} 발견 (${attr.name})`;
              sendLogToServer(idMsg);
              
              const urlMsg = `🔗 ${storeId}: 생성된 URL - ${generatedUrl}`;
              sendLogToServer(urlMsg);
              
              return generatedUrl;
            }
          }
        }
      }
      
      // 2순위: 기존 링크에서 상품 ID 추출
      const links = container.querySelectorAll ? container.querySelectorAll('a[href]') : [];
      
      for (let link of links) {
        const href = link.href;
        
        // 로그인 링크 제외
        if (href.includes('login') || href.includes('auth')) {
          continue;
        }
        
        // URL에서 상품 ID 추출
        const productIdMatch = href.match(/\/products\/(\d+)|\/product\/(\d+)|\/item\/(\d+)|productNo=(\d+)/);
        if (productIdMatch) {
          const productId = productIdMatch[1] || productIdMatch[2] || productIdMatch[3] || productIdMatch[4];
          const generatedUrl = `https://smartstore.naver.com/${storeId}/products/${productId}`;
          
          const idMsg = `🆔 ${storeId}: URL에서 상품 ID ${productId} 추출`;
          sendLogToServer(idMsg);
          
          const urlMsg = `🔗 ${storeId}: 생성된 URL - ${generatedUrl}`;
          sendLogToServer(urlMsg);
          
          return generatedUrl;
        }
      }
      
      // 부모로 이동
      container = container.parentElement;
    }
    
    return null;
    
  } catch (error) {
    console.log('상품 ID 찾기 오류:', error);
    return null;
  }
}

// 전체 페이지에서 리뷰 찾기 (폴백 방법)
function findReviewsInWholePage(storeId) {
  try {
    const fallbackMsg = `🔄 ${storeId}: 전체 페이지 리뷰 검색`;
    sendLogToServer(fallbackMsg);
    
    // 1단계: 정확한 "리뷰" span 찾기
    const exactReviewSpans = document.evaluate("//span[normalize-space(text())='리뷰']", document, null, XPathResult.ORDERED_NODE_SNAPSHOT_TYPE, null);
    
    const exactMsg = `📝 ${storeId}: 정확한 "리뷰" span ${exactReviewSpans.snapshotLength}개 발견`;
    sendLogToServer(exactMsg);
    
    // 2단계: 모든 리뷰 관련 텍스트 찾기
    const allReviewTexts = document.evaluate("//text()[contains(., '리뷰')]", document, null, XPathResult.ORDERED_NODE_SNAPSHOT_TYPE, null);
    
    const allMsg = `📝 ${storeId}: 모든 리뷰 텍스트 ${allReviewTexts.snapshotLength}개 발견`;
    sendLogToServer(allMsg);
    
    // 3단계: 페이지의 모든 텍스트 확인
    const pageText = document.body.textContent || '';
    const reviewMatches = pageText.match(/\d+\s*리뷰|\d+개\s*리뷰|리뷰\s*\d+/g);
    
    if (reviewMatches) {
      const textMsg = `📝 ${storeId}: 텍스트에서 ${reviewMatches.length}개 리뷰 패턴: ${reviewMatches.slice(0, 5).join(', ')}`;
      sendLogToServer(textMsg);
    }
    
    // 4단계: DOM 요소들 직접 검색
    const allSpans = document.querySelectorAll('span');
    let reviewSpans = [];
    
    for (let span of allSpans) {
      const text = span.textContent.trim();
      if (text === '리뷰' || /^\d+\s*리뷰$/.test(text) || /^리뷰\s*\d+$/.test(text)) {
        reviewSpans.push(span);
        const spanMsg = `✅ ${storeId}: span 리뷰 발견 - "${text}"`;
        sendLogToServer(spanMsg);
      }
    }
    
    const spanMsg = `🔍 ${storeId}: ${reviewSpans.length}개 리뷰 span 발견`;
    sendLogToServer(spanMsg);
    
    // 5단계: 첫 번째 상품 링크라도 찾기 (임시)
    const firstProductLink = document.querySelector('a[href*="/products/"], a[href*="/product/"]');
    if (firstProductLink && !firstProductLink.href.includes('login')) {
      const tempMsg = `🔗 ${storeId}: 임시 첫 번째 상품 링크 - ${firstProductLink.href}`;
      sendLogToServer(tempMsg);
      return [{ url: firstProductLink.href, storeId: storeId }];
    }
    
    const noLinkMsg = `❌ ${storeId}: 상품 링크 없음`;
    sendLogToServer(noLinkMsg);
    return [];
    
  } catch (error) {
    const errorMsg = `❌ ${storeId}: 리뷰 검색 오류 - ${error.message}`;
    sendLogToServer(errorMsg);
    return [];
  }
}

// 서버로 상품 데이터 전송
async function sendProductDataToServer(storeId, productData, reviewCount) {
  try {
    const data = {
      storeId: storeId,
      productCount: productData.length,
      reviewProductCount: reviewCount,
      products: productData,
      pageUrl: window.location.href,
      timestamp: new Date().toISOString()
    };
    
    // 디버깅: 전송할 데이터 확인
    console.log('📡 전송 데이터:', {
      storeId: data.storeId,
      productCount: data.productCount,
      reviewProductCount: data.reviewProductCount,
      products: data.products
    });
    
    const response = await fetch('http://localhost:8080/api/smartstore/product-data', {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json'
      },
      body: JSON.stringify(data)
    });
    
    if (!response.ok) {
      console.error('❌ 서버 응답 오류:', response.status);
    }
    
  } catch (error) {
    console.error('❌ 상품 데이터 전송 실패:', error);
  }
}

// 서버에 전체상품 페이지 접속 알림
async function notifyAllProductsPageLoaded(storeId) {
  try {
    const data = {
      storeId: storeId,
      pageType: 'all-products',
      pageUrl: window.location.href,
      timestamp: new Date().toISOString()
    };
    
    const response = await fetch('http://localhost:8080/api/smartstore/all-products', {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json'
      },
      body: JSON.stringify(data)
    });
    
    if (!response.ok) {
      console.error('❌ 서버 응답 오류:', response.status);
    }
    
  } catch (error) {
    console.error('❌ 전체상품 페이지 알림 실패:', error);
  }
}

// URL에서 스토어 ID 추출
function extractStoreIdFromUrl(url) {
  try {
    const match = url.match(/smartstore\.naver\.com\/([^\/\?]+)/);
    return match ? match[1] : 'unknown';
  } catch (error) {
    return 'unknown';
  }
}

// 상품들에 순차적으로 접속
async function visitProductsSequentially(storeId, runId, productUrls) {
  try {
    const startMsg = `🚀 ${storeId}: ${productUrls.length}개 상품에 순차 접속 시작`;
    sendLogToServer(startMsg);
    
    for (let i = 0; i < productUrls.length; i++) {
      const product = productUrls[i];
      
      try {
        const visitMsg = `🔗 ${storeId}: [${i + 1}/${productUrls.length}] ${product.url} 접속`;
        sendLogToServer(visitMsg);
        
        // ⭐ 5초 타임아웃으로 상품 페이지 접속
        await Promise.race([
          new Promise(async (resolve) => {
            const productTab = window.open(product.url, '_blank');
            await new Promise(r => setTimeout(r, 2000));
            if (productTab && !productTab.closed) {
              productTab.close();
            }
            resolve();
          }),
          new Promise(resolve => setTimeout(resolve, 5000)) // 5초 타임아웃
        ]);
        
        const completeMsg = `✅ ${storeId}: [${i + 1}/${productUrls.length}] 접속 완료`;
        sendLogToServer(completeMsg);
        
      } catch (error) {
        const errorMsg = `❌ ${storeId}: [${i + 1}/${productUrls.length}] 접속 오류 - ${error.message}`;
        sendLogToServer(errorMsg);
        // 오류가 발생해도 계속 진행
      }
    }
    
    // 모든 상품 접속 완료 후 서버에 완료 신호
    const beforeSendMsg = `📡 ${storeId}: 완료 신호 전송 시작`;
    await sendLogToServer(beforeSendMsg);
    
    await sendProductDataToServer(storeId, productUrls, productUrls.length);
    
    const afterSendMsg = `📡 ${storeId}: 완료 신호 전송 완료`;
    await sendLogToServer(afterSendMsg);
    
    // ⭐ 강제로 완료 상태 설정 (무한 대기 방지)
    await setStoreStateFromHandler(storeId, runId, 'done', false, productUrls.length, productUrls.length);
    
    const finalMsg = `🎉 ${storeId}: 모든 상품 접속 완료 (${productUrls.length}개)`;
    await sendLogToServer(finalMsg);
    
  } catch (error) {
    const errorMsg = `❌ ${storeId}: 순차 접속 오류 - ${error.message}`;
    await sendLogToServer(errorMsg);
    
    // ⭐ 오류 발생 시에도 완료 처리 (무한 대기 방지)
    await setStoreStateFromHandler(storeId, runId, 'done', false, 0, 0);
  }
}
    
    // ⭐ 서버에 완료 상태 설정
    try {
      const response = await fetch('http://localhost:8080/api/smartstore/complete', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json'
        },
        body: JSON.stringify({
          storeId: storeId,
          completed: true,
          timestamp: new Date().toISOString()
        })
      });
      
      if (response.ok) {
        await sendLogToServer(`✅ ${storeId}: 서버 완료 상태 설정 성공`);
      } else {
        await sendLogToServer(`❌ ${storeId}: 서버 완료 상태 설정 실패 - ${response.status}`);
      }
    } catch (error) {
      await sendLogToServer(`❌ ${storeId}: 서버 완료 상태 설정 오류 - ${error.message}`);
    }
    
    // ⭐ 완료 상태로 설정 (done + unlock)
    await setStoreStateFromHandler(storeId, runId, 'done', false, productUrls.length, productUrls.length);
    
    const finalMsg = `🎉 ${storeId}: 모든 상품 접속 완료 (${productUrls.length}개)`;
    await sendLogToServer(finalMsg);
    
  } catch (error) {
    const errorMsg = `❌ ${storeId}: 순차 접속 오류 - ${error.message}`;
    await sendLogToServer(errorMsg);
    
    // 오류 발생 시에도 완료 신호 전송
    await sendProductDataToServer(storeId, productUrls, productUrls.length);
    
    // ⭐ 오류 시에도 서버 완료 상태 설정
    try {
      await fetch('http://localhost:8080/api/smartstore/complete', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          storeId: storeId,
          completed: true,
          error: error.message,
          timestamp: new Date().toISOString()
        })
      });
      await sendLogToServer(`✅ ${storeId}: 오류 완료 상태 설정 성공`);
    } catch (e) {
      await sendLogToServer(`❌ ${storeId}: 오류 완료 상태 설정 실패`);
    }
  }
}
