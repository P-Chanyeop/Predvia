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

// 공구탭에서 실행되는 스크립트 - 공구 개수 확인
console.log('🔥 gonggu-checker.js 파일 로드됨!');
console.log('🔥 현재 URL:', window.location.href);
console.log('🔍 공구 개수 확인 스크립트 실행');

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
    
    console.log(`🔧 공구탭 창 크기 조절: ${windowWidth}x${windowHeight} at (${x}, ${y})`);
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

// ⭐ 순차 처리 권한 요청
chrome.runtime.sendMessage({
  action: 'requestProcessing',
  storeId: getStoreIdFromUrl(),
  storeTitle: document.title
}, (response) => {
  if (response.granted) {
    console.log('✅ 순차 처리 권한 획득');
    // 페이지 로딩 완료 후 실행
    setTimeout(() => {
      checkGongguCount();
    }, 2000);
    
    // 추가로 5초 후에도 한번 더 시도
    setTimeout(() => {
      checkGongguCount();
    }, 5000);
  } else {
    console.log(`🔒 대기열 ${response.position}번째 - 권한 대기 중`);
  }
});

function getStoreIdFromUrl() {
  const url = window.location.href;
  const match = url.match(/smartstore\.naver\.com\/([^\/]+)/);
  return match ? match[1] : 'unknown';
}

function checkGongguCount() {
  try {
    console.log('📊 공구 개수 찾는 중...');
    
    // 페이지의 모든 텍스트 확인
    const pageText = document.body.textContent || '';
    console.log('📄 페이지 텍스트 샘플:', pageText.substring(0, 1000));
    
    // 다양한 패턴으로 공구 개수 찾기
    const patterns = [
      /공구\s*\(\s*총\s*([0-9,]+)\s*개\s*\)/g,
      /공구\s*\(\s*([0-9,]+)\s*개\s*\)/g,
      /총\s*([0-9,]+)\s*개/g,
      /([0-9,]+)\s*개\s*상품/g
    ];
    
    let gongguCount = 0;
    let found = false;
    let matchedText = '';
    
    for (let pattern of patterns) {
      const matches = pageText.match(pattern);
      if (matches) {
        console.log(`🎯 패턴 매칭 성공:`, matches);
        
        for (let match of matches) {
          const numberMatch = match.match(/([0-9,]+)/);
          if (numberMatch) {
            const countStr = numberMatch[1].replace(/,/g, '');
            const count = parseInt(countStr);
            
            if (count > gongguCount) {
              gongguCount = count;
              matchedText = match;
              found = true;
            }
          }
        }
        
        if (found) break;
      }
    }
    
    if (found) {
      console.log(`✅ 공구 개수 발견: ${gongguCount}개`);
      console.log(`📝 매칭된 텍스트: "${matchedText}"`);
    } else {
      console.log('❌ 공구 개수 텍스트를 찾을 수 없습니다');
      
      // DOM 요소별로 상세 검색
      const elements = document.querySelectorAll('*');
      for (let element of elements) {
        const text = element.textContent || '';
        if (text.includes('공구') || text.includes('개')) {
          console.log('🔍 관련 텍스트 발견:', text.trim().substring(0, 100));
        }
      }
      
      // 공구 개수를 찾지 못한 경우 0으로 설정
      gongguCount = 0;
      console.log('🔄 공구 개수를 0으로 설정 (공구탭 없음으로 판단)');
    }
    
    // 결과를 서버로 전송 (반드시 실행)
    sendGongguResult(gongguCount);
    
    // [v2] 서버 주도 크롤링에도 보고
    v2ReportGonggu(getStoreIdFromUrl(), gongguCount);
    
  } catch (error) {
    console.error('공구 개수 확인 오류:', error);
    // 오류 발생 시에도 0으로 전송
    sendGongguResult(0);
  } finally {
    // ⭐ 항상 순차 처리 권한 해제
    chrome.runtime.sendMessage({
      action: 'releaseProcessing',
      storeId: getStoreIdFromUrl()
    }, (response) => {
      console.log('🔓 순차 처리 권한 해제 완료');
    });
  }
}

// 서버로 공구 개수 결과 전송
async function sendGongguResult(gongguCount) {
  try {
    // URL에서 스토어 ID 추출
    const storeId = extractStoreIdFromUrl(window.location.href);
    console.log('🔥🔥🔥 서버 연결 테스트 시작');
    
    // 먼저 서버 연결 테스트
    try {
      const testResponse = await localFetch('http://localhost:8080/api/smartstore/status');
      console.log('🔥🔥🔥 서버 연결 테스트 결과:', testResponse.status);
      
      if (!testResponse.ok) {
        console.error('❌ 서버 연결 실패:', testResponse.status);
        return;
      }
      
      console.log('✅ 서버 연결 성공 - 공구 개수 체크 시작');
    } catch (testError) {
      console.error('❌ 서버 연결 테스트 오류:', testError);
      return;
    }
    
    const data = {
      storeId: storeId,
      gongguCount: gongguCount,
      isValid: gongguCount >= 1000,
      timestamp: new Date().toISOString(),
      pageUrl: window.location.href
    };
    
    console.log('📡 서버로 공구 개수 결과 전송:', data);
    
    const response = await localFetch('http://localhost:8080/api/smartstore/gonggu-check', {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        'Origin': 'chrome-extension'
      },
      body: JSON.stringify(data)
    });
    
    if (response.ok) {
      console.log('✅ 공구 개수 결과 전송 완료');
      
      // 1000개 이상이면 전체상품 판매많은순 페이지로 이동
      if (gongguCount >= 1000) {
        console.log(`🎯 ${storeId}: 공구 ${gongguCount}개 ≥ 1000개 - 전체상품 페이지로 이동`);
        
        // 전체상품 판매많은순 URL 생성 (runId 포함)
        const urlParams = new URLSearchParams(window.location.search);
        const runId = urlParams.get('runId') || 'unknown';
        const allProductsUrl = `https://smartstore.naver.com/${storeId}/category/ALL?st=TOTALSALE&runId=${runId}`;
        console.log(`🔗 전체상품 URL: ${allProductsUrl}`);
        
        // 서버에 전체상품 페이지 이동 알림
        try {
          await localFetch('http://localhost:8080/api/smartstore/log', {
            method: 'POST',
            headers: {
              'Content-Type': 'application/json',
              'Origin': 'chrome-extension'
            },
            body: JSON.stringify({
              message: `🛍️ ${storeId}: 전체상품 페이지로 이동 - ${allProductsUrl}`,
              timestamp: new Date().toISOString()
            })
          });
        } catch (e) {
          console.log('로그 전송 실패:', e);
        }
        
        // 페이지 이동 후 리뷰 찾기 로직 실행
        setTimeout(() => {
          console.log('🚀 전체상품 페이지로 이동 중...');
          window.location.replace(allProductsUrl);
        }, 500);
        
      } else {
        // 공구 개수가 1000개 미만인 경우 (0개 포함) 모두 탭 닫기
        console.log(`❌ ${storeId}: 공구 ${gongguCount}개 < 1000개 - 즉시 탭 닫기`);
        
        // ⭐ 서버에 스킵 완료 신호 전송 (다음 스토어로 이동 트리거)
        try {
          await localFetch('http://localhost:8080/api/smartstore/skip-store', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
              storeId: storeId,
              reason: `공구 ${gongguCount}개 < 1000개`
            })
          });
          console.log(`✅ ${storeId}: 스킵 완료 신호 전송`);
        } catch (e) {
          console.log(`⚠️ ${storeId}: 스킵 신호 전송 실패`);
        }
        
        // 즉시 window.close() 시도
        window.close();
        
        // Chrome API로도 탭 닫기 시도 (백업)
        try {
          chrome.runtime.sendMessage({
            action: 'closeCurrentTab'
          }, () => {
            if (chrome.runtime.lastError) {
              // 조용히 무시
            }
          });
        } catch (e) {
          // 조용히 무시
        }
        
        // 강제 페이지 이동으로 탭 무력화
        setTimeout(() => {
          window.location.href = 'about:blank';
        }, 500);
      }
      
    } else {
      console.error('❌ 서버 응답 오류:', response.status);
    }
    
  } catch (error) {
    // 네트워크 오류는 조용히 처리 (콘솔 스팸 방지)
    if (!error.message.includes('Failed to fetch')) {
      console.error('❌ 공구 개수 결과 전송 실패:', error);
    }
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
// 마지막 리뷰 상품 찾기 함수
async function findLastReviewProduct(storeId) {
  try {
    const logMsg = `🔍 ${storeId}: 마지막 리뷰 상품 찾기 시작`;
    await sendLogToServer(logMsg);
    
    // 상품 리뷰만 찾는 정확한 패턴
    const allTextNodes = document.evaluate("//text()[contains(., '리뷰')]", document, null, XPathResult.ORDERED_NODE_SNAPSHOT_TYPE, null);
    
    let reviewElements = [];
    
    for (let i = 0; i < allTextNodes.snapshotLength; i++) {
      const textNode = allTextNodes.snapshotItem(i);
      const text = textNode.textContent.trim();
      
      // 상품 리뷰 패턴만 허용
      if (/^\d+\s*리뷰$/.test(text) || 
          /^리뷰\s*\d+$/.test(text) || 
          /^\d+개\s*리뷰$/.test(text)) {
        reviewElements.push(textNode);
        
        const validMsg = `✅ ${storeId}: 유효한 리뷰 - "${text}"`;
        await sendLogToServer(validMsg);
      }
    }
    
    const reviewMsg = `🔍 ${storeId}: ${reviewElements.length}개 상품 리뷰 발견`;
    await sendLogToServer(reviewMsg);
    
    if (reviewElements.length === 0) {
      const noReviewMsg = `❌ ${storeId}: 상품 리뷰 없음`;
      await sendLogToServer(noReviewMsg);
      return;
    }
    
    // 마지막 리뷰부터 역순으로 상품 ID 찾기
    for (let i = reviewElements.length - 1; i >= 0; i--) {
      const reviewElement = reviewElements[i];
      const reviewText = reviewElement.textContent.trim();
      
      const tryMsg = `🔍 ${storeId}: "${reviewText}"에서 상품 ID 찾기`;
      await sendLogToServer(tryMsg);
      
      const productUrl = findProductIdAndGenerateUrl(reviewElement, storeId);
      if (productUrl) {
        // 서버로 상품 데이터 전송
        await sendProductDataToServer(storeId, [{ url: productUrl, storeId: storeId }], 1);
        return;
      }
    }
    
    const noLinkMsg = `❌ ${storeId}: 상품 ID 없음`;
    await sendLogToServer(noLinkMsg);
    
  } catch (error) {
    const errorMsg = `❌ ${storeId}: 리뷰 검색 오류 - ${error.message}`;
    await sendLogToServer(errorMsg);
  }
}

// 상품 ID 추출 및 URL 생성
function findProductIdAndGenerateUrl(element, storeId) {
  try {
    let container = element;
    
    // 최대 10단계까지 부모 요소 탐색
    for (let level = 0; level < 10 && container; level++) {
      
      // DOM 속성들에서 상품 ID 찾기
      if (container.querySelectorAll) {
        const allElements = container.querySelectorAll('*');
        
        for (let element of allElements) {
          const allAttributes = element.attributes;
          for (let attr of allAttributes) {
            // 숫자로만 이루어진 긴 값 찾기 (상품 ID 패턴)
            if (attr.value && /^\d{8,}$/.test(attr.value)) {
              const productId = attr.value;
              const generatedUrl = `https://smartstore.naver.com/${storeId}/products/${productId}`;
              
              sendLogToServer(`🆔 ${storeId}: 상품 ID ${productId} 발견`);
              sendLogToServer(`🔗 ${storeId}: 생성된 URL - ${generatedUrl}`);
              
              return generatedUrl;
            }
          }
        }
      }
      
      container = container.parentElement;
    }
    
    return null;
    
  } catch (error) {
    console.log('상품 ID 찾기 오류:', error);
    return null;
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
    
    const response = await localFetch('http://localhost:8080/api/smartstore/product-data', {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        'Origin': 'chrome-extension'
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

// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
// [v2] 서버 주도 크롤링 - 공구 결과 보고
// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
function v2ReportGonggu(storeId, count) {
  const type = count >= 0 ? 'gonggu_result' : 'no_gonggu';
  chrome.runtime.sendMessage({
    type: 'v2_report',
    data: { type, storeId, count }
  }, (resp) => {
    console.log(`[v2] 공구 보고 완료: ${storeId} = ${count}개`);
  });
}
