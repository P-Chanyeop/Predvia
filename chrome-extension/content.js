// 콘텐츠 스크립트 - 네이버 가격비교 해외직구 페이지에서 스마트스토어 링크 수집
console.log('🆕 Predvia 스마트스토어 링크 수집 확장프로그램 실행됨');

// ⭐ 즉시 차단 복구 체크 (페이지 로드와 동시에)
(async function immediateResumeCheck() {
  try {
    const blockedData = localStorage.getItem('blockedStore');
    if (blockedData) {
      const blocked = JSON.parse(blockedData);
      console.log('🔄 차단된 스토어 발견 - 즉시 복구 시작:', blocked);
      
      // 서버에 복구 시작 로그 전송
      fetch('http://localhost:8080/api/smartstore/log', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          message: `🔄 ${blocked.storeId}: Chrome 재시작 후 ${blocked.currentIndex}/${blocked.totalProducts}번째 상품부터 재개`,
          timestamp: new Date().toISOString()
        })
      });

      // 네이버 가격비교 페이지에서 바로 차단된 스토어 전체상품 페이지로 이동
      if (window.location.href.includes('search.shopping.naver.com')) {
        const resumeUrl = `https://smartstore.naver.com/${blocked.storeId}/category/ALL?st=TOTALSALE&runId=${blocked.runId}`;
        console.log('🔄 차단된 스토어로 바로 이동:', resumeUrl);
        
        // 즉시 이동 (37개 스토어 재수집 건너뛰기)
        window.location.href = resumeUrl;
        return;
      }
    }
  } catch (error) {
    console.error('즉시 차단 복구 오류:', error);
  }
})();

// ⭐ 재시작 후 차단된 스토어부터 재개 함수
async function resumeFromBlocked() {
  try {
    const blockedData = localStorage.getItem('blockedStore');
    if (!blockedData) {
      return false; // 차단된 스토어 없음
    }

    const blocked = JSON.parse(blockedData);
    console.log('🔄 차단 복구 시작:', blocked);
    
    // 서버에 로그 전송
    await fetch('http://localhost:8080/api/smartstore/log', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        message: `🔄 ${blocked.storeId}: 차단된 지점부터 재개 (${blocked.currentIndex}/${blocked.totalProducts}번째 상품부터)`,
        timestamp: new Date().toISOString()
      })
    });

    // 전체상품 페이지로 이동하여 복구 진행
    const resumeUrl = `https://smartstore.naver.com/${blocked.storeId}/category/ALL?st=TOTALSALE&runId=${blocked.runId}`;
    console.log('🔄 전체상품 페이지로 이동:', resumeUrl);
    
    window.location.href = resumeUrl;
    return true; // 복구 시작

  } catch (error) {
    console.log('차단 복구 오류:', error);
    return false;
  }
}

// 페이지 로딩 완료 후 실행
if (document.readyState === 'loading') {
  document.addEventListener('DOMContentLoaded', initializeExtension);
} else {
  initializeExtension();
}

async function initializeExtension() {
  console.log('🆕 Predvia 스마트스토어 링크 수집 초기화 완료');
  
  // ⭐ 먼저 차단 복구 체크
  const resumed = await resumeFromBlocked();
  if (resumed) {
    return; // 차단 복구 진행 중, 정상 플로우 건너뛰기
  }
  
  // 자동으로 스마트스토어 링크 추출 및 전송
  setTimeout(() => {
    console.log('🚀 자동 스마트스토어 링크 추출 시작...');
    scrollAndCollectLinks();
  }, 3000); // 3초 후 자동 실행 (페이지 로딩 대기)
}

// 페이지 끝까지 스크롤하고 스마트스토어 링크 수집
async function scrollAndCollectLinks() {
  console.log('📜 페이지 끝까지 스크롤 - 스마트스토어 링크 수집');
  
  let previousHeight = 0;
  let currentHeight = document.body.scrollHeight;
  let scrollAttempts = 0;
  const maxScrollAttempts = 10;
  
  // 페이지 끝까지 반복 스크롤
  while (previousHeight !== currentHeight && scrollAttempts < maxScrollAttempts) {
    previousHeight = currentHeight;
    
    // 페이지 끝까지 스크롤
    window.scrollTo(0, document.body.scrollHeight);
    console.log(`📍 스크롤 ${scrollAttempts + 1}회 - 높이: ${currentHeight}px`);
    
    // 최소 대기 시간 (500ms)
    await new Promise(resolve => setTimeout(resolve, 500));
    
    currentHeight = document.body.scrollHeight;
    scrollAttempts++;
  }
  
  console.log(`📜 스크롤 완료 - 총 ${scrollAttempts}회 스크롤`);
  
  // 최종 대기 후 링크 수집
  await new Promise(resolve => setTimeout(resolve, 1000));
  
  // 스마트스토어 링크 수집
  const smartStoreLinks = extractSmartStoreLinks();
  
  console.log(`✅ 스크롤 완료: 총 ${smartStoreLinks.length}개 스마트스토어 링크 수집`);
  
  // 서버로 전송
  await sendSmartStoreLinksToServer(smartStoreLinks);
}

// 유효한 스마트스토어 링크인지 확인
function isValidSmartStoreLink(url) {
  // ⭐ 엄격한 필터링 조건
  if (!url.startsWith('https://smartstore.naver.com/inflow/outlink/url?url')) {
    return false;
  }
  
  // ⭐ 잘못된 URL 패턴 제외
  if (url.includes('sell.smartstore.naver.com')) {
    return false;
  }
  
  if (url.includes('#/home/about')) {
    return false;
  }
  
  if (url.includes('tipModal=WINDOW_EXPOSURE')) {
    return false;
  }
  
  // ⭐ 내부 URL에 실제 스토어 ID가 있는지 확인
  try {
    const decoded = decodeURIComponent(url);
    const innerUrlMatch = decoded.match(/url=([^&]+)/);
    if (innerUrlMatch) {
      const innerUrl = decodeURIComponent(innerUrlMatch[1]);
      // 실제 스토어 URL 패턴 확인
      return /^https:\/\/smartstore\.naver\.com\/[a-zA-Z0-9_-]+$/.test(innerUrl);
    }
  } catch (e) {
    return false;
  }
  
  return false;
}

// 스마트스토어 링크 추출
function extractSmartStoreLinks() {
  console.log('🔍 스마트스토어 링크 추출 시작');
  
  const smartStoreLinks = [];
  
  try {
    // 네이버 가격비교 페이지에서 스마트스토어 링크 찾기
    // 방법 1: "스마트스토어" 텍스트가 포함된 요소 찾기
    const smartStoreElements = document.querySelectorAll('*');
    
    smartStoreElements.forEach((element) => {
      const text = element.textContent || '';
      
      // "스마트스토어" 텍스트가 포함된 요소 찾기
      if (text.includes('스마트스토어') || text.includes('smartstore')) {
        // 해당 요소나 부모 요소에서 링크 찾기
        const linkElement = element.closest('a') || element.querySelector('a');
        
        if (linkElement && linkElement.href) {
          const link = linkElement.href;
          
          // ⭐ 유효한 스마트스토어 링크인지 확인
          if (isValidSmartStoreLink(link)) {
            // 중복 제거
            if (!smartStoreLinks.some(item => item.url === link)) {
              // 상품명 추출 시도
              const productTitle = extractProductTitle(linkElement);
              
              smartStoreLinks.push({
                url: link,
                title: productTitle,
                seller: '스마트스토어'
              });
              
              console.log(`✅ 스마트스토어 링크 발견: ${productTitle} - ${link}`);
            }
          }
        }
      }
    });
    
    // 방법 2: 직접 스마트스토어 링크 패턴으로 찾기
    const allLinks = document.querySelectorAll('a[href*="smartstore.naver.com"], a[href*="brand.naver.com"]');
    
    allLinks.forEach((linkElement) => {
      const link = linkElement.href;
      
      // ⭐ 유효한 스마트스토어 링크인지 확인
      if (isValidSmartStoreLink(link)) {
        // 중복 제거
        if (!smartStoreLinks.some(item => item.url === link)) {
          const productTitle = extractProductTitle(linkElement);
          
          smartStoreLinks.push({
            url: link,
            title: productTitle,
            seller: '스마트스토어'
          });
          
          console.log(`✅ 스마트스토어 링크 발견 (직접): ${productTitle} - ${link}`);
        }
      }
    });
    
  } catch (error) {
    console.error('❌ 스마트스토어 링크 추출 오류:', error);
  }
  
  console.log(`📦 총 ${smartStoreLinks.length}개 스마트스토어 링크 추출 완료`);
  return smartStoreLinks;
}

// 상품명 추출 함수
function extractProductTitle(linkElement) {
  try {
    // 링크 텍스트에서 상품명 추출
    let title = linkElement.textContent?.trim() || '';
    
    // 부모 요소에서 상품명 찾기
    if (!title || title === '스마트스토어') {
      const parent = linkElement.closest('.product_item, .product, .item, [class*="product"]');
      if (parent) {
        const titleElement = parent.querySelector('.product_title, .title, h3, h4, [class*="title"]');
        if (titleElement) {
          title = titleElement.textContent?.trim() || '';
        }
      }
    }
    
    // 여전히 제목이 없으면 주변 텍스트에서 추출
    if (!title || title === '스마트스토어') {
      const siblings = linkElement.parentElement?.children || [];
      for (let sibling of siblings) {
        const siblingText = sibling.textContent?.trim() || '';
        if (siblingText && siblingText !== '스마트스토어' && siblingText.length > 5) {
          title = siblingText;
          break;
        }
      }
    }
    
    return title || '상품명 없음';
  } catch (error) {
    console.error('상품명 추출 오류:', error);
    return '상품명 추출 실패';
  }
}

// 서버로 스마트스토어 링크 전송 및 순차 접속
async function sendSmartStoreLinksToServer(smartStoreLinks = null) {
  try {
    console.log('📡 Predvia로 스마트스토어 링크 전송 시작...');
    
    // 링크가 전달되지 않으면 현재 페이지에서 추출
    if (!smartStoreLinks) {
      smartStoreLinks = extractSmartStoreLinks();
    }
    
    if (smartStoreLinks.length === 0) {
      console.log('⚠️ 추출된 스마트스토어 링크가 없습니다.');
      return;
    }
    
    const data = {
      smartStoreLinks: smartStoreLinks,
      source: 'naver_price_comparison',
      timestamp: new Date().toISOString(),
      pageUrl: window.location.href
    };
    
    console.log('요청 URL: http://localhost:8080/api/smartstore/links');
    console.log('전송할 데이터:', JSON.stringify({
      smartStoreLinks: data.smartStoreLinks.slice(0, 5) // 처음 5개만 로그로 확인
    }, null, 2));
    
    const response = await fetch('http://localhost:8080/api/smartstore/links', {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        'Origin': 'chrome-extension'
      },
      body: JSON.stringify(data)
    });
    
    console.log('응답 상태:', response.status);
    
    if (response.ok) {
      console.log('✅ 서버 통신 성공 - 응답 확인 중');
      
      try {
        // ⭐ 응답 텍스트 먼저 확인
        const responseText = await response.text();
        console.log('📡 서버 응답 텍스트:', responseText.substring(0, 200) + '...');
        
        if (!responseText.trim()) {
          console.error('❌ 서버에서 빈 응답 수신');
          await visitSmartStoreLinksSequentially(smartStoreLinks);
          return;
        }
        
        const responseData = JSON.parse(responseText);
        console.log('서버 응답:', responseData);
        
        if (responseData.success) {
          console.log(`📊 ${responseData.totalLinks || responseData.linkCount}개 중 ${responseData.selectedLinks}개 스토어 선택됨`);
          console.log(`🎯 목표: ${responseData.targetProducts}개 상품 수집`);
          
          // ⭐ 서버에서 선택된 스토어 목록 받기
          if (responseData.selectedStores && responseData.selectedStores.length > 0) {
            console.log('🎯 선택된 스토어만 방문 시작:');
            responseData.selectedStores.forEach((store, index) => {
              console.log(`  ${index + 1}. ${store.title} (${store.storeId})`);
            });
            
            // ⭐ 선택된 스토어만 방문
            await visitSelectedStoresOnly(responseData.selectedStores);
          } else {
            console.error('❌ 선택된 스토어 목록을 받지 못함');
          }
        } else {
          console.error('❌ 서버에서 스토어 선택 실패');
        }
      } catch (parseError) {
        console.error('❌ JSON 파싱 오류:', parseError);
        // JSON 파싱 실패해도 순차 접속은 실행
        await visitSmartStoreLinksSequentially(smartStoreLinks);
      }
      
    } else {
      console.error('❌ 서버 응답 오류:', response.status, response.statusText);
    }
    
  } catch (error) {
    console.error('❌ Predvia 통신 오류:', error);
    console.error('❌ 오류 타입:', error.constructor.name);
    console.error('❌ 오류 메시지:', error.message);
    console.log('💡 Predvia 프로그램이 실행 중인지 확인해주세요.');
  }
}

// ⭐ 선택된 스토어만 방문하는 함수
async function visitSelectedStoresOnly(selectedStores) {
  console.log(`🚀 선택된 ${selectedStores.length}개 스토어만 순차 접속 시작`);
  
  for (let i = 0; i < selectedStores.length; i++) {
    const store = selectedStores[i];
    
    try {
      // ⭐ 서버에서 중단 신호 확인
      const shouldStop = await checkShouldStop();
      if (shouldStop) {
        console.log(`🛑 목표 달성으로 크롤링 중단 (${i + 1}/${selectedStores.length}번째에서 중단)`);
        break;
      }
      
      const storeId = store.storeId;
      
      if (!storeId) {
        console.log(`❌ [${i + 1}/${selectedStores.length}] 스토어 ID 없음: ${store.title}`);
        continue;
      }

      // ⭐ 스토어별 고유 runId 생성
      const runId = `${storeId}-${Date.now()}-${Math.random().toString(36).slice(2,8)}`;
      console.log(`🆔 ${storeId}: 고유 runId 생성 - ${runId}`);

      // 공구탭 URL 생성 (runId 포함)
      const gongguUrl = `https://smartstore.naver.com/${storeId}/category/50000165?cp=1&runId=${runId}`;
      
      console.log(`📍 [${i + 1}/${selectedStores.length}] 공구탭 접속: ${store.title}`);
      console.log(`🔗 스토어 ID: ${storeId}`);
      console.log(`🔗 공구탭 URL: ${gongguUrl}`);
      
      // ⭐ 서버에 방문 알림 (선택된 스토어인지 확인)
      const visitResponse = await notifyStoreVisit({
        url: store.url,
        title: store.title,
        storeId: storeId,
        gongguUrl: gongguUrl,
        currentIndex: i + 1,
        totalCount: selectedStores.length,
        timestamp: new Date().toISOString()
      });
      
      // ⭐ 목표 달성 시 중단
      if (visitResponse && visitResponse.stop) {
        console.log(`🎉 목표 달성! 총 ${visitResponse.totalProducts}개 상품 수집 완료`);
        break;
      }
      
      // ⭐ 즉시 서버에 "진행중" 상태 기록
      await setStoreState(storeId, runId, 'collecting', true);
      
      // 새 탭에서 공구탭 열기
      const newTab = window.open(gongguUrl, '_blank');
      
      // ⭐ 1000개 이하 스토어만 3초 후 즉시 완료, 1000개 이상은 대기
      const smallStores = ['jikjikgu', 'unkleboboo', 'whmallcom', 'wdcafe', 'allcans', 'globalselectok', 'jtemshop', 'jndco'];
      
      if (smallStores.includes(storeId)) {
        // 1000개 이하: 3초 후 즉시 완료
        setTimeout(async () => {
          await setStoreState(storeId, runId, 'done', false, 0, 0);
          console.log(`✅ ${storeId}: 1000개 이하 즉시 완료`);
        }, 3000);
      } else {
        // 1000개 이상: 완료 대기
        console.log(`⏳ ${storeId}: 1000개 이상 - 완료 대기`);
      }
      
      // ⭐ runId 기반 완료 대기 (진짜 막는 지점)
      console.log(`🔍 ${storeId}: 완료 대기 시작 (runId: ${runId})`);
      await waitForTaskCompletion(storeId, runId);
      console.log(`✅ ${storeId}: 완료 대기 끝`);
      
      // 2초 대기 후 다음 스토어
      await new Promise(resolve => setTimeout(resolve, 2000));
      
    } catch (error) {
      console.log(`❌ [${i + 1}/${selectedStores.length}] 오류: ${error.message}`);
    }
  }
  
  console.log(`🎉 선택된 ${selectedStores.length}개 스토어 방문 완료!`);
}

// 스마트스토어 링크들을 순차적으로 방문 (공구탭으로 변환)
async function visitSmartStoreLinksSequentially(smartStoreLinks) {
  console.log(`🚀 ${smartStoreLinks.length}개 스마트스토어 공구탭 순차 접속 시작`);
  
  for (let i = 0; i < smartStoreLinks.length; i++) {
    const link = smartStoreLinks[i];
    
    try {
      // ⭐ 서버에서 중단 신호 확인
      const shouldStop = await checkShouldStop();
      if (shouldStop) {
        console.log(`🛑 목표 달성으로 크롤링 중단 (${i + 1}/${smartStoreLinks.length}번째에서 중단)`);
        break;
      }
      
      // 스마트스토어 ID 추출
      const storeId = extractStoreId(link.url);
      
      if (!storeId) {
        console.log(`❌ [${i + 1}/${smartStoreLinks.length}] 스토어 ID 추출 실패: ${link.title}`);
        continue;
      }

      // ⭐ 스토어별 고유 runId 생성
      const runId = `${storeId}-${Date.now()}-${Math.random().toString(36).slice(2,8)}`;
      console.log(`🆔 ${storeId}: 고유 runId 생성 - ${runId}`);

      // 공구탭 URL 생성 (runId 포함)
      const gongguUrl = `https://smartstore.naver.com/${storeId}/category/50000165?cp=1&runId=${runId}`;
      
      console.log(`📍 [${i + 1}/${smartStoreLinks.length}] 공구탭 접속: ${link.title}`);
      console.log(`🔗 스토어 ID: ${storeId}`);
      console.log(`🔗 공구탭 URL: ${gongguUrl}`);
      
      // ⭐ 서버에 방문 알림 (선택된 스토어인지 확인)
      const visitResponse = await notifyStoreVisit({
        url: link.url,
        title: link.title,
        storeId: storeId,
        gongguUrl: gongguUrl,
        currentIndex: i + 1,
        totalCount: smartStoreLinks.length,
        timestamp: new Date().toISOString()
      });
      
      // ⭐ 선택되지 않은 스토어는 건너뛰기
      if (visitResponse && visitResponse.skip) {
        console.log(`⏭️ 선택되지 않은 스토어 건너뛰기: ${storeId}`);
        continue;
      }
      
      // ⭐ 목표 달성 시 중단
      if (visitResponse && visitResponse.stop) {
        console.log(`🎉 목표 달성! 총 ${visitResponse.totalProducts}개 상품 수집 완료`);
        break;
      }
      
      // ⭐ 즉시 서버에 "진행중" 상태 기록
      await setStoreState(storeId, runId, 'collecting', true);
      
      // 새 탭에서 공구탭 열기
      const newTab = window.open(gongguUrl, '_blank');
      
      // ⭐ 1000개 이하 스토어만 3초 후 즉시 완료, 1000개 이상은 대기
      const smallStores = ['jikjikgu', 'unkleboboo', 'whmallcom', 'wdcafe', 'allcans', 'globalselectok', 'jtemshop', 'jndco'];
      
      if (smallStores.includes(storeId)) {
        // 1000개 이하: 3초 후 즉시 완료
        setTimeout(async () => {
          await setStoreState(storeId, runId, 'done', false, 0, 0);
          console.log(`✅ ${storeId}: 1000개 이하 즉시 완료`);
        }, 3000);
      }
      // 1000개 이상은 all-products-handler.js가 완료 신호 보낼 때까지 대기
      
      // 서버에 접속 상태 알림 (runId 포함)
      await notifyServerLinkVisited({
        ...link,
        storeId: storeId,
        gongguUrl: gongguUrl,
        runId: runId
      }, i + 1, smartStoreLinks.length);
      
      // ⭐ runId 기반 완료 대기 (진짜 막는 지점)
      console.log(`🔍 ${storeId}: 완료 대기 시작 (runId: ${runId})`);
      await waitForTaskCompletion(storeId, runId);
      console.log(`✅ ${storeId}: 완료 대기 끝`);
      
      // 탭 닫기 (안전하게)
      try {
        if (newTab && typeof newTab.close === 'function' && !newTab.closed) {
          newTab.close();
          console.log(`🗂️ ${storeId}: 탭 닫기 완료`);
        }
      } catch (e) {
        console.log(`⚠️ ${storeId}: 탭 닫기 실패 - ${e.message}`);
      }
      
      console.log(`✅ [${i + 1}/${smartStoreLinks.length}] 작업 완료: ${link.title}`);
      
    } catch (error) {
      console.error(`❌ 링크 처리 오류 [${i + 1}]: ${link.title}`, error);
    }
  }
  
  console.log('✅ 모든 스마트스토어 공구탭 작업 완료');
}

// ⭐ 서버 상태 설정 함수
async function setStoreState(storeId, runId, state, lock, expected = 0, progress = 0) {
  try {
    const response = await fetch('http://localhost:8080/api/smartstore/state', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        storeId, runId, state, lock, expected, progress,
        timestamp: new Date().toISOString()
      })
    });
    
    if (response.ok) {
      console.log(`🔧 ${storeId}: 상태 설정 성공 - ${state} (lock: ${lock})`);
    }
  } catch (error) {
    console.log(`❌ ${storeId}: 상태 설정 오류 - ${error.message}`);
  }
}

// ⭐ runId 기반 완료 대기 함수
async function waitForTaskCompletion(storeId, runId) {
  const startTime = Date.now();
  const timeout = 5 * 60 * 1000; // 5분
  
  // 5초 초기 대기
  await new Promise(resolve => setTimeout(resolve, 5000));
  
  while (true) {
    try {
      const response = await fetch(`http://localhost:8080/api/smartstore/state?storeId=${storeId}&runId=${runId}`);
      const state = response.ok ? await response.json() : { state: 'unknown', lock: false };
      
      console.log(`🔍 ${storeId}: 상태 확인 - ${state.state} (lock: ${state.lock})`);
      
      // ⭐ 완료 조건: runId 일치 + done + unlock
      if (state.runId === runId && state.state === 'done' && state.lock === false) {
        console.log(`✅ ${storeId}: 완료 확인됨!`);
        return true;
      }
      
      // 타임아웃 체크
      if (Date.now() - startTime > timeout) {
        console.log(`⏰ ${storeId}: 타임아웃`);
        return false;
      }
      
      await new Promise(resolve => setTimeout(resolve, 1500));
      
    } catch (error) {
      console.log(`❌ ${storeId}: 상태 확인 오류 - ${error.message}`);
      await new Promise(resolve => setTimeout(resolve, 1500));
    }
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

// ⭐ 스토어 방문 알림
async function notifyStoreVisit(visitData) {
  try {
    const response = await fetch('http://localhost:8080/api/smartstore/visit', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(visitData)
    });
    
    if (response.ok) {
      return await response.json();
    }
  } catch (error) {
    console.log('방문 알림 오류:', error);
  }
  return null;
}

// 스마트스토어 ID 추출 함수
function extractStoreId(url) {
  try {
    console.log('원본 URL:', url);
    
    // URL 디코딩
    const decodedUrl = decodeURIComponent(url);
    console.log('디코딩된 URL:', decodedUrl);
    
    // url= 파라미터에서 실제 스마트스토어 URL 추출
    const urlMatch = decodedUrl.match(/url=([^&]+)/);
    
    if (urlMatch && urlMatch[1]) {
      const actualStoreUrl = urlMatch[1];
      console.log('실제 스토어 URL:', actualStoreUrl);
      
      // 실제 스토어 URL에서 ID 추출
      const storeIdMatch = actualStoreUrl.match(/smartstore\.naver\.com\/([^&\/\?]+)/);
      console.log('매칭 결과:', storeIdMatch);
      
      if (storeIdMatch && storeIdMatch[1]) {
        console.log('추출된 스토어 ID:', storeIdMatch[1]);
        return storeIdMatch[1];
      }
    }
    
    console.log('스토어 ID 추출 실패');
    return null;
  } catch (error) {
    console.error('스토어 ID 추출 오류:', error);
    return null;
  }
}

// 다른 탭에서 스크립트 실행 (제한적)
async function executeScriptInTab(tabWindow, scriptCode) {
  return new Promise((resolve) => {
    try {
      // 간단한 방법: postMessage 사용
      const messageId = 'gonggu-check-' + Date.now();
      
      // 응답 리스너
      const responseHandler = (event) => {
        if (event.data && event.data.messageId === messageId) {
          window.removeEventListener('message', responseHandler);
          resolve(event.data.result || 0);
        }
      };
      
      window.addEventListener('message', responseHandler);
      
      // 다른 탭에 메시지 전송 (제한적이므로 기본값 반환)
      setTimeout(() => {
        window.removeEventListener('message', responseHandler);
        resolve(0); // 확인 불가시 0 반환
      }, 2000);
      
    } catch (error) {
      resolve(0);
    }
  });
}

// 서버에 공구 개수 결과 알림
async function notifyServerGongguCount(storeId, gongguCount, isValid) {
  try {
    const data = {
      storeId: storeId,
      gongguCount: gongguCount,
      isValid: isValid,
      timestamp: new Date().toISOString()
    };
    
    await fetch('http://localhost:8080/api/smartstore/gonggu-check', {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        'Origin': 'chrome-extension'
      },
      body: JSON.stringify(data)
    });
    
  } catch (error) {
    console.error('공구 개수 알림 오류:', error);
  }
}

// 서버에 링크 방문 상태 알림
async function notifyServerLinkVisited(link, currentIndex, totalCount) {
  try {
    const visitData = {
      url: link.url,
      title: link.title,
      storeId: link.storeId || '',
      gongguUrl: link.gongguUrl || '',
      currentIndex: currentIndex,
      totalCount: totalCount,
      timestamp: new Date().toISOString()
    };
    
    await fetch('http://localhost:8080/api/smartstore/visit', {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        'Origin': 'chrome-extension'
      },
      body: JSON.stringify(visitData)
    });
    
  } catch (error) {
    console.error('서버 알림 오류:', error);
  }
}

console.log('🎯 Predvia 스마트스토어 링크 수집 확장프로그램 로드 완료');
