console.log('🔥 all-products-handler.js 파일 로드됨!');
console.log('🔥 현재 URL:', window.location.href);

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
    
    console.log(`🔧 스마트스토어 창 크기 조절: ${windowWidth}x${windowHeight} at (${x}, ${y})`);
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

// ⭐ 중복 실행 방지 가드
if (window.__ALL_PRODUCTS_HANDLER_RUNNING__) {
  console.log('🚫 all-products-handler 이미 실행 중 - 중복 실행 방지');
} else {
  window.__ALL_PRODUCTS_HANDLER_RUNNING__ = true;
  console.log('✅ all-products-handler 실행 시작 - 가드 설정 완료');
  
  // ⭐ 순차 처리 권한 요청
  chrome.runtime.sendMessage({
    action: 'requestProcessing',
    storeId: getStoreIdFromUrl(),
    storeTitle: document.title
  }, (response) => {
    if (response.granted) {
      console.log('✅ 순차 처리 권한 획득');
      // ⭐ 페이지 로드 완료 후 실행
      if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', initHandler);
      } else {
        initHandler();
      }
    } else {
      console.log(`🔒 대기열 ${response.position}번째 - 권한 대기 중`);
    }
  });
}

function getStoreIdFromUrl() {
  const url = window.location.href;
  const match = url.match(/smartstore\.naver\.com\/([^\/]+)/);
  return match ? match[1] : 'unknown';
}

function initHandler() {
  console.log('🔥 페이지 로드 완료 - 핸들러 초기화');
  
  setTimeout(() => {
    handleAllProductsPage();
  }, 1000); // 3초→1초로 단축
}

async function handleAllProductsPage() {
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
    
    // 카테고리 정보 추출 및 전송
    await extractAndSendCategories(storeId);
    
    // 바로 리뷰 검색 실행
    setTimeout(async () => {
      await sendLogToServer(`🔍 ${storeId}: 리뷰 검색 시작`);
      
      const productData = await collectProductData(storeId, runId);
      // ⭐ 중복 호출 제거 - visitProductsSequentially 완료 후에만 호출
      
    }, 2000); // 2초만 대기
    
  } catch (error) {
    const errorMsg = `❌ ${storeId}: 핸들러 오류 - ${error.message}`;
    sendLogToServer(errorMsg);
  }
}

// 카테고리 정보 추출 및 전송
async function extractAndSendCategories(storeId) {
    try {
        console.log('📂 카테고리 정보 추출 시작...');
        await sendLogToServer(`📂 ${storeId}: 카테고리 정보 추출 시작`);
        
        // ⭐ 안정적인 CSS 선택자 사용: ul.ySOklWNBjf .sAla67hq4a
        const categorySpans = document.querySelectorAll('ul.ySOklWNBjf .sAla67hq4a');
        const categories = [];
        
        if (categorySpans.length > 0) {
            await sendLogToServer(`📂 ${storeId}: ${categorySpans.length}개 카테고리 발견`);
            
            categorySpans.forEach((span, index) => {
                const categoryName = span.textContent.trim();
                if (categoryName) {
                    const link = span.closest('a');
                    categories.push({
                        name: categoryName,
                        url: link ? link.getAttribute('href') : null,
                        categoryId: null,
                        order: index
                    });
                }
            });
        } else {
            // 기본 홈 카테고리 추가
            categories.push({
                name: "홈",
                url: `/${storeId}`,
                categoryId: null,
                order: 0
            });
        }
        
        if (categories.length > 0) {
            console.log(`✅ ${categories.length}개 카테고리 발견:`, categories);
            
            const categoryData = {
                storeId: storeId,
                categories: categories,
                pageUrl: window.location.href,
                extractedAt: new Date().toISOString()
            };
            
            // 서버로 카테고리 데이터 전송
            await sendToServer('/api/smartstore/categories', categoryData);
            await sendLogToServer(`✅ ${storeId}: ${categories.length}개 카테고리 수집 완료`);
        }
        
    } catch (error) {
        console.error('❌ 카테고리 추출 중 오류:', error);
        await sendLogToServer(`❌ ${storeId}: 카테고리 추출 오류 - ${error.message}`);
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

// 서버로 데이터 전송하는 범용 함수
async function sendToServer(endpoint, data) {
  try {
    const controller = new AbortController();
    const timeoutId = setTimeout(() => controller.abort(), 5000); // 5초 타임아웃
    
    const response = await fetch(`http://localhost:8080${endpoint}`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json'
      },
      body: JSON.stringify(data),
      signal: controller.signal
    });
    
    clearTimeout(timeoutId);
    
    if (response.ok) {
      console.log(`✅ 서버 전송 성공: ${endpoint}`);
      return true;
    } else {
      // 카테고리 전송 실패는 로그에 표시하지 않음 (너무 빈번함)
      if (!endpoint.includes('/categories')) {
        console.error(`❌ 서버 전송 실패: ${endpoint} - ${response.status} ${response.statusText}`);
      }
      return false;
    }
  } catch (error) {
    // 카테고리 전송 실패는 로그에 표시하지 않음
    if (!endpoint.includes('/categories')) {
      console.error(`❌ 서버 전송 오류: ${endpoint} - ${error.message}`);
    }
    return false;
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
      
      // ⭐ 해당 스토어의 모든 앱 창 닫기
      chrome.runtime.sendMessage({
        action: 'closeAppWindows',
        storeId: storeId
      });
      
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
      
      // ⭐ 해당 스토어의 모든 앱 창 닫기
      chrome.runtime.sendMessage({
        action: 'closeAppWindows',
        storeId: storeId
      });
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
  console.log(`🔥🔥🔥 sendProductDataToServer 함수 진입: ${storeId}`);
  
  try {
    const data = {
      storeId: storeId,
      productCount: productData.length,
      reviewProductCount: reviewCount,
      products: productData,
      pageUrl: window.location.href,
      timestamp: new Date().toISOString()
    };
    
    console.log(`🔥🔥🔥 전송할 데이터 준비 완료: ${storeId}, 상품수: ${data.productCount}`);
    
    const response = await fetch('http://localhost:8080/api/smartstore/product-data', {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json'
      },
      body: JSON.stringify(data)
    });
    
    console.log(`🔥🔥🔥 서버 응답 받음: ${storeId}, 상태: ${response.status}`);
    
    if (response.ok) {
      console.log(`✅ ${storeId}: 상품 데이터 전송 성공`);
    } else {
      console.error(`❌ ${storeId}: 서버 응답 오류 ${response.status}`);
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
    
    if (response.ok) {
      let result;
      try {
        const responseText = await response.text();
        console.log('📡 서버 응답 텍스트:', responseText);
        
        if (!responseText || responseText.trim() === '') {
          console.log('❌ 빈 응답 수신 - 크롤링 중단');
          console.log('🚫 탭 닫기 예정 (디버깅용 비활성화)');
          // window.close();
          return;
        }
        
        result = JSON.parse(responseText);
        console.log('📡 서버 응답 파싱 완료:', result);
      } catch (jsonError) {
        console.log('❌ JSON 파싱 오류:', jsonError.message);
        console.log('🚫 크롤링 중단 - 탭 닫기 (디버깅용 비활성화)');
        // window.close();
        return;
      }
      
      // ⭐ 서버에서 차단된 경우 즉시 중단
      if (!result.success) {
        console.log(`❌ ${storeId}: 서버에서 차단됨 - ${result.message}`);
        console.log('🚫 크롤링 중단 - 탭 닫기 (디버깅용 비활성화)');
        // window.close();
        return;
      }
    } else {
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
        // ⭐ 서버에서 중단 신호 확인
        const shouldStop = await checkShouldStop();
        if (shouldStop) {
          const stopMsg = `🛑 ${storeId}: 목표 달성으로 상품 접속 중단 (${i + 1}/${productUrls.length}번째에서 중단)`;
          await sendLogToServer(stopMsg);
          
          // ⭐ 100% 확실한 중단을 위해 함수 즉시 종료
          setTimeout(() => {
            window.close();
            if (chrome && chrome.tabs) {
              chrome.tabs.getCurrent((tab) => {
                if (tab) chrome.tabs.remove(tab.id);
              });
            }
          }, 500);
          return; // 함수 즉시 종료
        }
        
        const visitMsg = `🔗 ${storeId}: [${i + 1}/${productUrls.length}] ${product.url} 접속`;
        sendLogToServer(visitMsg);
        
        // ⭐ 2-4초 랜덤 대기 (차단 방지, 속도 개선)
        const delay = 2000 + Math.random() * 2000;
        const timeoutPromise = new Promise(resolve => setTimeout(resolve, delay));
        const accessPromise = new Promise(async (resolve, reject) => {
          try {
            // ⭐ 앱 모드 작은 창으로 열기 (Chrome API 사용)
            chrome.runtime.sendMessage({
              action: 'openAppWindow',
              url: product.url,
              storeId: storeId  // 스토어 ID 전달
            }, (response) => {
              if (response && response.success) {
                console.log(`✅ 앱 모드 창으로 상품 접속: ${product.url}`);
              }
            });
            
            // ⭐ 차단 페이지 감지를 위한 체크
            setTimeout(async () => {
              try {
                if (true) {
                  // 차단 페이지 텍스트 감지
                  const pageContent = productTab.document.body.textContent || '';
                  if (pageContent.includes('현재 서비스 접속이 불가합니다') || 
                      pageContent.includes('동시에 접속하는 이용자 수가 많거나') ||
                      pageContent.includes('인터넷 네트워크 상태가 불안정하여')) {
                    
                    await sendLogToServer(`🚫 ${storeId}: 네이버 차단 페이지 감지 - 크롤링 즉시 중단`);
                    
                    // ⭐ 서버에 중단 신호 전송
                    try {
                      await fetch('http://localhost:8080/api/smartstore/stop', {
                        method: 'POST',
                        headers: { 'Content-Type': 'application/json' },
                        body: JSON.stringify({
                          reason: 'blocked',
                          storeId: storeId,
                          message: '네이버 차단 페이지 감지로 인한 크롤링 중단'
                        })
                      });
                    } catch (e) {
                      console.log('중단 신호 전송 오류:', e);
                    }
                    
                    
                    reject(new Error('BLOCKED_BY_NAVER'));
                    return;
                  }
                  
                  // ⭐ 개별 상품 페이지에서 카테고리 추출
                  try {
                    const categorySpans = productTab.document.querySelectorAll('ul.ySOklWNBjf .sAla67hq4a');
                    const productId = product.url.split('/products/')[1];
                    
                    if (categorySpans.length > 0) {
                      const categories = [];
                      categorySpans.forEach((span, index) => {
                        const categoryName = span.textContent.trim();
                        if (categoryName) {
                          const link = span.closest('a');
                          categories.push({
                            name: categoryName,
                            url: link ? link.getAttribute('href') : null,
                            categoryId: null,
                            order: index
                          });
                        }
                      });
                      
                      await sendLogToServer(`📂 ${storeId}: 상품 ${productId} 카테고리 ${categories.length}개 발견`);
                      
                      // 서버로 카테고리 데이터 전송
                      const categoryData = {
                        storeId: storeId,
                        productId: productId,
                        categories: categories,
                        pageUrl: product.url,
                        extractedAt: new Date().toISOString()
                      };
                      
                      try {
                        await sendLogToServer(`📂 ${storeId}: 상품 ${productId} 카테고리 전송 시작`);
                        
                        // ⭐ 기존 categories API 사용 (잘 작동하는 API)
                        const response = await fetch('http://localhost:8080/api/smartstore/categories', {
                          method: 'POST',
                          headers: { 'Content-Type': 'application/json' },
                          body: JSON.stringify({
                            storeId: storeId,
                            categories: categories,
                            pageUrl: product.url,
                            extractedAt: new Date().toISOString(),
                            productId: productId // 상품 ID 추가
                          })
                        });
                        
                        if (response.ok) {
                          await sendLogToServer(`✅ ${storeId}: 상품 ${productId} 카테고리 서버 전송 완료`);
                        } else {
                          const errorText = await response.text();
                          await sendLogToServer(`❌ ${storeId}: 상품 ${productId} 카테고리 서버 전송 실패 - ${response.status}: ${errorText}`);
                        }
                      } catch (fetchError) {
                        await sendLogToServer(`❌ ${storeId}: 카테고리 전송 오류 - ${fetchError.message}`);
                      }
                      
                    } else {
                      await sendLogToServer(`📂 ${storeId}: 상품 ${productId} 카테고리 없음`);
                    }
                  } catch (categoryError) {
                    await sendLogToServer(`❌ ${storeId}: 카테고리 추출 오류 - ${categoryError.message}`);
                  }

                  // ⭐ 상품 이미지 추출
                  try {
                    const mainImage = productTab.document.querySelector('.bd_2DO68') || 
                                     productTab.document.querySelector('img[alt="대표이미지"]');
                    
                    if (mainImage && mainImage.src) {
                      const imageUrl = mainImage.src;
                      const productId = product.url.split('/products/')[1];
                      
                      await sendLogToServer(`🖼️ ${storeId}: 상품 이미지 발견 - ${productId}`);
                      
                      // ⭐ 서버로 이미지 URL 전송
                      await fetch('http://localhost:8080/api/smartstore/image', {
                        method: 'POST',
                        headers: { 'Content-Type': 'application/json' },
                        body: JSON.stringify({
                          storeId: storeId,
                          productId: productId,
                          imageUrl: imageUrl,
                          productUrl: product.url
                        })
                      });
                      
                    } else {
                      await sendLogToServer(`❌ ${storeId}: 상품 이미지 없음 - ${product.url}`);
                    }
                  } catch (imageError) {
                    await sendLogToServer(`❌ ${storeId}: 이미지 추출 오류 - ${imageError.message}`);
                  }

                  // ⭐ 상품명 추출
                  try {
                    const productNameElement = productTab.document.querySelector('.DCVBehA8ZB') || 
                                              productTab.document.querySelector('h3._copyable');
                    
                    if (productNameElement && productNameElement.textContent) {
                      const productName = productNameElement.textContent.trim();
                      const productId = product.url.split('/products/')[1];
                      
                      await sendLogToServer(`📝 ${storeId}: 상품명 발견 - ${productName}`);
                      
                      // ⭐ 서버로 상품명 전송
                      await fetch('http://localhost:8080/api/smartstore/product-name', {
                        method: 'POST',
                        headers: { 'Content-Type': 'application/json' },
                        body: JSON.stringify({
                          storeId: storeId,
                          productId: productId,
                          productName: productName,
                          productUrl: product.url
                        })
                      });
                      
                    } else {
                      await sendLogToServer(`❌ ${storeId}: 상품명 없음 - ${product.url}`);
                    }
                  } catch (nameError) {
                    await sendLogToServer(`❌ ${storeId}: 상품명 추출 오류 - ${nameError.message}`);
                  }

                  // ⭐ 리뷰 데이터 수집
                  try {
                    await sendLogToServer(`📊 ${storeId}: 리뷰 수집 시작`);
                    
                    const reviews = [];
                    const productId = product.url.split('/products/')[1];
                    
                    // v1.25에서 사용한 정확한 선택자 사용
                    const ratingElements = productTab.document.querySelectorAll('em.n6zq2yy0KA');
                    const reviewContentElements = productTab.document.querySelectorAll('.vhlVUsCtw3 .K0kwJOXP06');
                    
                    await sendLogToServer(`📊 ${storeId}: 별점 ${ratingElements.length}개, 리뷰 내용 ${reviewContentElements.length}개 발견`);
                    
                    // 리뷰 데이터 수집
                    const maxReviews = Math.max(ratingElements.length, reviewContentElements.length);
                    
                    for (let j = 0; j < maxReviews; j++) {
                      let rating = 5.0;
                      let content = '';
                      
                      // 별점 추출
                      if (j < ratingElements.length) {
                        const ratingText = ratingElements[j].textContent.trim();
                        rating = parseFloat(ratingText) || 5.0;
                      }
                      
                      // 리뷰 내용 추출
                      if (j < reviewContentElements.length) {
                        content = reviewContentElements[j].textContent.trim();
                      }
                      
                      if (rating || content) {
                        reviews.push({
                          rating: rating,
                          content: content || `평점 ${rating}점`
                        });
                        
                        await sendLogToServer(`⭐ ${storeId}: 리뷰 ${j+1} - 평점 ${rating}점`);
                      }
                    }
                    
                    // 서버로 리뷰 데이터 전송
                    if (reviews.length > 0) {
                      const reviewData = {
                        storeId: storeId,
                        productId: productId,
                        productUrl: product.url,
                        reviews: reviews,
                        reviewCount: reviews.length,
                        timestamp: new Date().toISOString()
                      };
                      
                      const reviewResponse = await fetch('http://localhost:8080/api/smartstore/reviews', {
                        method: 'POST',
                        headers: { 'Content-Type': 'application/json' },
                        body: JSON.stringify(reviewData)
                      });
                      
                      if (reviewResponse.ok) {
                        await sendLogToServer(`✅ ${storeId}: 리뷰 ${reviews.length}개 서버 전송 완료`);
                      } else {
                        await sendLogToServer(`❌ ${storeId}: 리뷰 서버 전송 실패`);
                      }
                    } else {
                      await sendLogToServer(`❌ ${storeId}: 리뷰 데이터 없음`);
                    }
                    
                  } catch (reviewError) {
                    await sendLogToServer(`❌ ${storeId}: 리뷰 수집 오류 - ${reviewError.message}`);
                  }
                  
                  
                }
                resolve();
              } catch (crossOriginError) {
                // 크로스 오리진 오류는 정상 접속으로 간주
                if (true) {
                  
                }
                resolve();
              }
            }, 1000); // 1초 후 차단 페이지 체크
            
          } catch (e) {
            resolve(); // 모든 오류는 완료 처리
          }
        });
        
        await Promise.race([accessPromise, timeoutPromise]);
        
        const completeMsg = `✅ ${storeId}: [${i + 1}/${productUrls.length}] 접속 완료`;
        sendLogToServer(completeMsg);
        
        // ⭐ 진행률 업데이트
        await updateProgress(storeId, runId, 1);
        
      } catch (error) {
        const errorMsg = `❌ ${storeId}: [${i + 1}/${productUrls.length}] 접속 오류 - ${error.message}`;
        sendLogToServer(errorMsg);
        
        // ⭐ 네이버 차단 감지 시 전체 크롤링 중단
        if (error.message === 'BLOCKED_BY_NAVER') {
          await sendLogToServer(`🛑 ${storeId}: 네이버 차단으로 인한 전체 크롤링 중단`);
          throw error; // 상위로 예외 전파하여 전체 크롤링 중단
        }
        
        // 다른 오류는 계속 진행
      }
    }
    
    // 모든 상품 접속 완료 후 서버에 완료 신호
    const beforeSendMsg = `📡 ${storeId}: 완료 신호 전송 시작`;
    await sendLogToServer(beforeSendMsg);
    
    console.log(`🔥🔥🔥 sendProductDataToServer 호출 시작: ${storeId}, 상품수: ${productUrls.length}`);
    await sendProductDataToServer(storeId, productUrls, productUrls.length);
    console.log(`🔥🔥🔥 sendProductDataToServer 호출 완료: ${storeId}`);
    
    const afterSendMsg = `📡 ${storeId}: 완료 신호 전송 완료`;
    await sendLogToServer(afterSendMsg);
    
    // ⭐ 순차 처리 권한 해제 (정상 완료)
    chrome.runtime.sendMessage({
      action: 'releaseProcessing',
      storeId: storeId
    }, (response) => {
      console.log('🔓 순차 처리 권한 해제 완료 (정상)');
    });
    
    // ⭐ 강제로 완료 상태 설정 (무한 대기 방지)
    await setStoreStateFromHandler(storeId, runId, 'done', false, productUrls.length, productUrls.length);
    
    // ⭐ 해당 스토어의 모든 앱 창 닫기
    chrome.runtime.sendMessage({
      action: 'closeAppWindows',
      storeId: storeId
    });
    
    const finalMsg = `🎉 ${storeId}: 모든 상품 접속 완료 (${productUrls.length}개)`;
    await sendLogToServer(finalMsg);
    
    // ⭐ 메인 스토어 탭 닫기 (작업 완료 후)
    setTimeout(() => {
      console.log('🔥 전체상품 페이지 작업 완료 - 창 닫기');
      // 일반 닫기 시도
      window.close();
      
      // Chrome API로 강제 닫기
      if (chrome && chrome.tabs) {
        chrome.tabs.getCurrent((tab) => {
          if (tab) {
            chrome.tabs.remove(tab.id);
          }
        });
      }
    }, 500); // 즉시 닫기
    
  } catch (error) {
    const errorMsg = `❌ ${storeId}: 순차 접속 오류 - ${error.message}`;
    await sendLogToServer(errorMsg);
    
    // ⭐ 오류 발생 시에도 완료 처리 (무한 대기 방지)
    await setStoreStateFromHandler(storeId, runId, 'done', false, 0, 0);
    
    // ⭐ 해당 스토어의 모든 앱 창 닫기
    chrome.runtime.sendMessage({
      action: 'closeAppWindows',
      storeId: storeId
    });
    
    // ⭐ 오류 시에도 탭 닫기
    setTimeout(() => {
      // 일반 닫기 시도
      window.close();
      
      // Chrome API로 강제 닫기
      if (chrome && chrome.tabs) {
        chrome.tabs.getCurrent((tab) => {
          if (tab) {
            chrome.tabs.remove(tab.id);
          }
        });
      }
    }, 500); // 2초→0.5초로 단축
  }
}

// ⭐ 서버에서 중단 신호 확인
async function checkShouldStop() {
  try {
    const response = await fetch('http://localhost:8080/api/smartstore/status', {
      method: 'GET',
      headers: { 'Content-Type': 'application/json' }
    });
    
    if (response.ok) {
      const data = await response.json();
      return data.shouldStop || false;
    }
  } catch (error) {
    console.log('중단 체크 오류:', error);
  }
  return false;
}


