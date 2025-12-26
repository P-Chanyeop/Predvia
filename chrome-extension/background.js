// ⭐ 중앙 집중식 순차 처리 시스템
console.log('🚀 Predvia 중앙 순차 처리 시스템 시작');

let globalProcessingState = {
  isProcessing: false,
  currentStore: null,
  currentTabId: null,
  lockTimestamp: null,
  queue: [],
  openWindows: new Map()  // 열린 앱 창들 추적
};

// ⭐ 순차 처리 요청 핸들러
chrome.runtime.onMessage.addListener((request, sender, sendResponse) => {
  console.log('🔥 Background 메시지 수신:', request.action, request.storeId);
  
  switch (request.action) {
    case 'openNewTab':
      // ⭐ 새 탭으로 스토어 열기
      chrome.tabs.create({
        url: request.url,
        active: false  // 백그라운드에서 열기
      }, (tab) => {
        console.log('✅ 새 탭 생성:', request.url);
        sendResponse({ success: true, tabId: tab.id });
      });
      return true; // 비동기 응답을 위해 true 반환
      
    case 'openAppWindow':
      // ⭐ 앱 모드 작은 창으로 열기
      chrome.windows.create({
        url: request.url,
        type: 'popup',
        width: 250,
        height: 400,
        left: 50,
        top: 400,
        focused: false  // 포커싱 방지
      }, (window) => {
        console.log('✅ 앱 모드 창 생성:', request.url);
        
        // ⭐ 창 ID를 저장해서 나중에 닫을 수 있도록
        if (!globalProcessingState.openWindows) {
          globalProcessingState.openWindows = new Map();
        }
        globalProcessingState.openWindows.set(window.id, {
          storeId: request.storeId || 'unknown',
          url: request.url,
          timestamp: Date.now()
        });
        
        sendResponse({ success: true, windowId: window.id });
        
        // ⭐ 상품 페이지인 경우 데이터 추출 스크립트 주입
        if (request.url.includes('/products/')) {
          const tabId = window.tabs && window.tabs[0] ? window.tabs[0].id : null;
          if (tabId) {
            setTimeout(() => {
              chrome.scripting.executeScript({
                target: { tabId: tabId },
                func: extractProductData
              }).catch(e => console.log('상품 데이터 추출 실패:', e));
            }, 3000);
          }
        }
      });
      return true;
      
    case 'closeAppWindows':
      // ⭐ 특정 스토어의 모든 앱 창 닫기
      if (globalProcessingState.openWindows) {
        for (const [windowId, windowInfo] of globalProcessingState.openWindows.entries()) {
          if (windowInfo.storeId === request.storeId) {
            chrome.windows.remove(windowId, () => {
              if (chrome.runtime.lastError) {
                // 조용한 처리 - 이미 닫힌 창
                return;
              }
              console.log(`🗂️ 앱 창 닫기: ${windowInfo.url}`);
              globalProcessingState.openWindows.delete(windowId);
            });
          }
        }
      }
      sendResponse({ success: true });
      return true;
      
    case 'requestProcessing':
      handleProcessingRequest(request, sender, sendResponse);
      return true; // 비동기 응답
      
    case 'releaseProcessing':
      handleProcessingRelease(request, sender, sendResponse);
      return true;
      
    case 'checkProcessingStatus':
      sendResponse({
        isProcessing: globalProcessingState.isProcessing,
        currentStore: globalProcessingState.currentStore,
        queueLength: globalProcessingState.queue.length
      });
      return true;
      
    case 'closeCurrentTab':
      // 기존 탭 닫기 기능 유지
      if (sender.tab && sender.tab.id) {
        chrome.tabs.remove(sender.tab.id, () => {
          if (chrome.runtime.lastError) {
            // 조용히 무시
          }
          sendResponse({success: true});
        });
      }
      return true;
  }
});

// ⭐ 처리 요청 핸들러
function handleProcessingRequest(request, sender, sendResponse) {
  const { storeId, storeTitle } = request;
  const tabId = sender.tab.id;
  
  console.log(`🔍 처리 요청: ${storeId} (탭: ${tabId})`);
  
  // 5분 타임아웃 체크
  if (globalProcessingState.isProcessing && globalProcessingState.lockTimestamp) {
    const elapsed = Date.now() - globalProcessingState.lockTimestamp;
    if (elapsed > 300000) { // 5분
      console.log('🔓 5분 타임아웃으로 잠금 자동 해제');
      resetProcessingState();
    }
  }
  
  // 현재 처리 중인 스토어가 없으면 즉시 승인
  if (!globalProcessingState.isProcessing) {
    grantProcessing(storeId, storeTitle, tabId);
    sendResponse({ granted: true, position: 0 });
    return;
  }
  
  // 이미 처리 중인 스토어와 같으면 승인 (재요청)
  if (globalProcessingState.currentStore === storeId) {
    console.log(`✅ 같은 스토어 ${storeId} 재요청 - 즉시 승인`);
    sendResponse({ granted: true, position: 0 });
    return;
  }
  
  // 대기열에 추가
  const queueItem = { storeId, storeTitle, tabId, timestamp: Date.now(), sendResponse };
  globalProcessingState.queue.push(queueItem);
  
  console.log(`🔒 대기열 추가: ${storeId} (위치: ${globalProcessingState.queue.length})`);
  sendResponse({ granted: false, position: globalProcessingState.queue.length });
}

// ⭐ 처리 해제 핸들러
function handleProcessingRelease(request, sender, sendResponse) {
  const { storeId } = request;
  const tabId = sender.tab.id;
  
  console.log(`🔓 처리 해제 요청: ${storeId} (탭: ${tabId})`);
  console.log(`🔍 현재 처리 중인 스토어: ${globalProcessingState.currentStore}`);
  
  // 현재 처리 중인 스토어가 맞는지 확인 (대소문자 무시)
  if (globalProcessingState.currentStore && 
      globalProcessingState.currentStore.toLowerCase() === storeId.toLowerCase()) {
    console.log(`✅ 권한 해제 승인: ${storeId}`);
    resetProcessingState();
    processQueue();
    sendResponse({ success: true });
  } else {
    console.log(`⚠️ 잘못된 해제 요청: 현재 ${globalProcessingState.currentStore}, 요청 ${storeId}`);
    // 강제로 해제 (데드락 방지)
    console.log(`🔧 강제 권한 해제: ${storeId}`);
    resetProcessingState();
    processQueue();
    sendResponse({ success: true });
  }
}

// ⭐ 처리 권한 부여
function grantProcessing(storeId, storeTitle, tabId) {
  globalProcessingState.isProcessing = true;
  globalProcessingState.currentStore = storeId;
  globalProcessingState.currentTabId = tabId;
  globalProcessingState.lockTimestamp = Date.now();
  
  console.log(`🔐 처리 권한 부여: ${storeId} (탭: ${tabId})`);
}

// ⭐ 처리 상태 초기화
function resetProcessingState() {
  globalProcessingState.isProcessing = false;
  globalProcessingState.currentStore = null;
  globalProcessingState.currentTabId = null;
  globalProcessingState.lockTimestamp = null;
  
  console.log('🔓 처리 상태 초기화 완료');
}

// ⭐ 대기열 처리
function processQueue() {
  if (globalProcessingState.queue.length === 0) {
    console.log('📭 대기열 비어있음');
    return;
  }
  
  // 가장 오래된 요청 처리
  const nextItem = globalProcessingState.queue.shift();
  const { storeId, storeTitle, tabId, sendResponse } = nextItem;
  
  // 탭이 아직 존재하는지 확인
  chrome.tabs.get(tabId, (tab) => {
    if (chrome.runtime.lastError || !tab) {
      console.log(`⚠️ 탭 ${tabId} 더 이상 존재하지 않음, 다음 대기열 처리`);
      processQueue();
      return;
    }
    
    grantProcessing(storeId, storeTitle, tabId);
    sendResponse({ granted: true, position: 0 });
    console.log(`✅ 대기열에서 처리 권한 부여: ${storeId}`);
  });
}

// ⭐ 탭 닫힘 감지 시 자동 해제
chrome.tabs.onRemoved.addListener((tabId) => {
  if (globalProcessingState.currentTabId === tabId) {
    console.log(`🗂️ 처리 중인 탭 ${tabId} 닫힘, 자동 해제`);
    resetProcessingState();
    processQueue();
  }
  
  // 대기열에서도 제거
  globalProcessingState.queue = globalProcessingState.queue.filter(item => item.tabId !== tabId);
});

// ⭐ 탭 업데이트 감지 (전체상품 페이지 강제 주입)
chrome.tabs.onUpdated.addListener((tabId, changeInfo, tab) => {
  if (changeInfo.status === 'complete' && tab.url) {
    console.log('🔍 탭 업데이트 감지:', tab.url);
    
    // 전체상품 페이지 감지
    if (tab.url.includes('smartstore.naver.com') && tab.url.includes('/category/ALL')) {
      console.log('🎯 전체상품 페이지 감지 - 스크립트 강제 주입');
      
      // 강제 스크립트 주입
      chrome.scripting.executeScript({
        target: { tabId: tabId },
        files: ['all-products-handler.js']
      }).then(() => {
        console.log('✅ all-products-handler.js 강제 주입 완료');
      }).catch((error) => {
        console.log('❌ 스크립트 주입 실패:', error);
      });
    }
    
    // 공구탭 페이지 감지
    if (tab.url.includes('smartstore.naver.com') && tab.url.includes('/category/50000165')) {
      console.log('🎯 공구탭 페이지 감지 - 즉시 스크립트 주입');
      
      // 즉시 스크립트 주입 (대기 없음)
      chrome.scripting.executeScript({
        target: { tabId: tabId },
        files: ['gonggu-checker.js']
      }).then(() => {
        console.log('✅ gonggu-checker.js 즉시 주입 완료');
      }).catch((error) => {
        console.log('❌ 스크립트 주입 실패:', error);
        
        // 재시도 (1초 후)
        setTimeout(() => {
          chrome.scripting.executeScript({
            target: { tabId: tabId },
            files: ['gonggu-checker.js']
          }).then(() => {
            console.log('✅ gonggu-checker.js 재시도 주입 완료');
          }).catch((retryError) => {
            console.log('❌ 재시도 주입도 실패:', retryError);
          });
        }, 1000);
      });
    }
  }
});

console.log('🚀 Background Script 중앙 순차 처리 시스템 초기화 완료');

// ⭐ 상품 데이터 추출 함수 (앱 창에서 실행됨)
async function extractProductData() {
  try {
    const url = window.location.href;
    const storeId = url.match(/smartstore\.naver\.com\/([^\/]+)/)?.[1];
    const productId = url.match(/\/products\/(\d+)/)?.[1];
    
    if (!storeId || !productId) {
      console.log('❌ 스토어ID 또는 상품ID 추출 실패');
      return;
    }
    
    console.log(`🛍️ 앱 창에서 상품 데이터 추출 시작: ${storeId}/${productId}`);
    
    // 페이지 로딩 대기
    await new Promise(resolve => setTimeout(resolve, 2000));
    
    // ⭐ 상품 이미지 추출
    try {
      const mainImage = document.querySelector('.bd_2DO68') || 
                       document.querySelector('img[alt="대표이미지"]');
      
      if (mainImage && mainImage.src) {
        const imageUrl = mainImage.src;
        console.log(`🖼️ 상품 이미지 발견: ${imageUrl}`);
        
        await fetch('http://localhost:8080/api/smartstore/image', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({
            storeId: storeId,
            productId: productId,
            imageUrl: imageUrl,
            productUrl: url
          })
        });
        console.log(`✅ 이미지 서버 전송 완료`);
      }
    } catch (error) {
      console.log(`❌ 이미지 추출 오류: ${error.message}`);
    }
    
    // ⭐ 상품명 추출
    try {
      const productNameElement = document.querySelector('.DCVBehA8ZB') || 
                                document.querySelector('h3._copyable');
      
      if (productNameElement && productNameElement.textContent) {
        const productName = productNameElement.textContent.trim();
        console.log(`📝 상품명 발견: ${productName}`);
        
        await fetch('http://localhost:8080/api/smartstore/product-name', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({
            storeId: storeId,
            productId: productId,
            productName: productName,
            productUrl: url
          })
        });
        console.log(`✅ 상품명 서버 전송 완료`);
      }
    } catch (error) {
      console.log(`❌ 상품명 추출 오류: ${error.message}`);
    }
    
  } catch (error) {
    console.log(`❌ 상품 데이터 추출 전체 오류: ${error.message}`);
  }
  
  // ⭐ 상품 데이터 추출 완료 후 즉시 창 닫기
  console.log('🔥 개별 상품 데이터 추출 완료 - 창 닫기');
  setTimeout(() => {
    window.close();
  }, 500);
}
