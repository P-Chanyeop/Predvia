// 콘텐츠 스크립트 - 네이버 가격비교 해외직구 페이지에서 스마트스토어 링크 수집
console.log('🆕 Predvia 스마트스토어 링크 수집 확장프로그램 실행됨');
console.log('🌐 현재 URL:', window.location.href);
console.log('⏰ 현재 시간:', new Date().toLocaleString());

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

// ⭐ 네이버 가격비교 캡챠 감지 및 서버 알림 + 창 닫기
async function checkForNaverCaptcha() {
  // 네이버 가격비교 페이지에서만 실행
  if (!window.location.href.includes('search.shopping.naver.com')) {
    return false;
  }

  try {
    // 캡챠 관련 요소들 확인
    const captchaSelectors = [
      'div.captcha_img_cover',
      'img[src*="captcha"]',
      'div[class*="captcha"]',
      'iframe[src*="captcha"]',
      '#captcha',
      '.captcha'
    ];
    
    let captchaFound = false;
    for (const selector of captchaSelectors) {
      if (document.querySelector(selector)) {
        captchaFound = true;
        console.log(`🔍 캡챠 감지됨: ${selector}`);
        break;
      }
    }

    // 페이지 텍스트에서 캡챠 관련 문구 확인
    const bodyText = document.body?.innerText || '';
    const captchaKeywords = ['자동입력', '보안문자', '로봇이 아닙니다', '캡챠', 'captcha', '본인확인'];
    for (const keyword of captchaKeywords) {
      if (bodyText.includes(keyword)) {
        captchaFound = true;
        console.log(`🔍 캡챠 키워드 감지됨: ${keyword}`);
        break;
      }
    }

    if (captchaFound) {
      console.log('🚫 네이버 가격비교 캡챠 감지! 서버에 알림 후 창 닫기');

      // 서버에 캡챠 감지 알림
      try {
        await localFetch('http://localhost:8080/api/captcha/detected', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({
            url: window.location.href,
            type: 'naver_price_comparison',
            timestamp: new Date().toISOString()
          })
        });
        console.log('✅ 서버에 캡챠 감지 알림 전송 완료');
      } catch (e) {
        console.log('⚠️ 서버 알림 실패:', e.message);
      }

      // 2초 후 창 닫기
      setTimeout(() => {
        console.log('🔥 캡챠로 인해 창 닫기');
        window.close();
      }, 2000);

      return true;
    }
    return false;
  } catch (error) {
    console.log('⚠️ 캡챠 체크 오류:', error.message);
    return false;
  }
}

// 페이지 로드 후 캡챠 체크 (네이버 가격비교 페이지에서만)
if (window.location.href.includes('search.shopping.naver.com')) {
  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', () => {
      setTimeout(checkForNaverCaptcha, 1500);
    });
  } else {
    setTimeout(checkForNaverCaptcha, 1500);
  }
}

// ⭐ 페이지 로드 후 창 크기 및 위치 강제 조절 (우하단 최소 크기)
function forceWindowResize() {
  try {
    // 창 크기를 200x300으로 강제 조절
    window.resizeTo(200, 300);
    
    // 창을 우하단으로 이동 (화면 크기 고려)
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
    
    console.log(`🔧 창 크기 및 위치 강제 조절: ${windowWidth}x${windowHeight} at (${x}, ${y})`);
  } catch (error) {
    console.log('⚠️ 창 크기 조절 실패:', error.message);
  }
}

// 페이지 로드 완료 후 창 크기 조절 실행
if (document.readyState === 'loading') {
  document.addEventListener('DOMContentLoaded', () => {
    setTimeout(forceWindowResize, 500);
  });
} else {
  setTimeout(forceWindowResize, 500);
}

// 추가 안전장치: 1초 후 한 번 더 실행
setTimeout(forceWindowResize, 1000);

// ⭐ 크롤링 완료 시 네이버 창 자동 닫기 체크
setInterval(async () => {
  try {
    const response = await localFetch('http://localhost:8080/api/smartstore/crawling-status');
    if (response.ok) {
      const data = await response.json();
      // 크롤링이 완료되었고, 실제로 스토어가 있었을 때만 창 닫기
      if (data.isCompleted && data.totalStores > 0) {
        console.log('🔥 크롤링 완료 감지 - 네이버 창 닫기');
        setTimeout(() => {
          window.close();
        }, 2000);
      }
    }
  } catch (error) {
    // 서버 미실행 또는 에러 시 무시 (창 닫지 않음)
  }
}, 3000); // 3초마다 체크

// ⭐ Background Script 기반 중앙 집중식 순차 처리 잠금
async function requestProcessingPermission(storeId, storeTitle) {
  return new Promise((resolve) => {
    chrome.runtime.sendMessage({
      action: 'requestProcessing',
      storeId: storeId,
      storeTitle: storeTitle
    }, (response) => {
      if (response.granted) {
        console.log(`🔐 ${storeId}: 처리 권한 획득`);
        resolve(true);
      } else {
        console.log(`🔒 ${storeId}: 대기열 ${response.position}번째 - 대기 중...`);
        // 대기열에서 권한을 받을 때까지 대기
        waitForProcessingPermission(storeId, resolve);
      }
    });
  });
}

async function waitForProcessingPermission(storeId, resolve) {
  // 2초마다 상태 체크
  const checkInterval = setInterval(() => {
    chrome.runtime.sendMessage({
      action: 'checkProcessingStatus'
    }, (response) => {
      if (!response.isProcessing || response.currentStore === storeId) {
        clearInterval(checkInterval);
        resolve(true);
      } else {
        console.log(`🔒 ${storeId}: 현재 ${response.currentStore} 처리 중 - 계속 대기...`);
      }
    });
  }, 2000);
}

async function releaseProcessingPermission(storeId, retryCount = 0) {
  return new Promise((resolve) => {
    if (!chrome?.runtime?.sendMessage) {
      if (retryCount < 3) {
        console.log(`⚠️ ${storeId}: chrome.runtime 사용 불가 - ${retryCount + 1}초 후 재시도`);
        setTimeout(() => {
          releaseProcessingPermission(storeId, retryCount + 1).then(resolve);
        }, 1000);
        return;
      }
      console.log(`❌ ${storeId}: chrome.runtime 3회 재시도 실패`);
      resolve(false);
      return;
    }
    chrome.runtime.sendMessage({
      action: 'releaseProcessing',
      storeId: storeId
    }, (response) => {
      if (chrome.runtime.lastError) {
        if (retryCount < 3) {
          console.log(`⚠️ ${storeId}: 권한 해제 오류 - ${retryCount + 1}초 후 재시도`);
          setTimeout(() => {
            releaseProcessingPermission(storeId, retryCount + 1).then(resolve);
          }, 1000);
          return;
        }
        console.log(`❌ ${storeId}: 권한 해제 3회 재시도 실패`);
        resolve(false);
        return;
      }
      if (response?.success) {
        console.log(`🔓 ${storeId}: 처리 권한 해제 완료`);
      } else {
        console.log(`⚠️ ${storeId}: 처리 권한 해제 실패`);
      }
      resolve(response?.success || false);
    });
  });
}

// 차단 복구 시스템 제거됨

// 차단 복구 함수 제거됨

// 페이지 로딩 완료 후 실행
if (document.readyState === 'loading') {
  document.addEventListener('DOMContentLoaded', initializeExtension);
} else {
  initializeExtension();
}

async function initializeExtension() {
  console.log('🆕 Predvia 확장프로그램 초기화 시작');
  
  // ⭐ 타오바오 페이지인 경우 - 이미지 검색만 실행
  if (window.location.href.includes('taobao.com')) {
    console.log('🔍 타오바오 페이지 감지 - 네이버 크롤링 로직 건너뛰기');
    return; // 타오바오에서는 여기서 종료
  }
  
  // ⭐ 네이버 가격비교 페이지가 아닌 경우 (스마트스토어 페이지) 플래그 확인 건너뛰기
  if (!window.location.href.includes('search.shopping.naver.com')) {
    console.log('🔥 스마트스토어 페이지 - 플래그 확인 건너뛰고 크롤링 진행');
  } else {
    // ⭐ 네이버 가격비교 페이지에서만 플래그 확인
    console.log('🔍 네이버 가격비교 페이지 감지 - 플래그 확인 시작');
    console.log('⏰ 플래그 확인 시간:', new Date().toLocaleTimeString());

    // ⭐ 플래그 설정 시간을 주기 위해 1초 대기
    await new Promise(resolve => setTimeout(resolve, 1000));

    try {
      console.log('📡 플래그 확인 요청 전송: http://localhost:8080/api/crawling/allowed');
      const response = await localFetch('http://localhost:8080/api/crawling/allowed');
      console.log('📡 플래그 확인 응답 상태:', response.status, response.ok);

      if (response.ok) {
        const data = await response.json();
        console.log(`🔍 서버 플래그 확인 결과: allowed = ${data.allowed}`);
        console.log('🔍 서버 응답 전체:', JSON.stringify(data));

        if (!data.allowed) {
          console.log('🔒 크롤링이 허용되지 않았습니다. 키워드 수집은 상품데이터 탭에서 처리합니다.');
          // // ⭐ "추가" 버튼 모드: 상품명만 추출 (상품데이터 탭에서 별도 처리하므로 비활성화)
          // await extractAndSendProductNames();
          return;
        }
        console.log('🔥🔥🔥 크롤링이 허용되었습니다! 스마트스토어 링크 수집을 시작합니다!');
      } else {
        console.log('❌ 크롤링 허용 상태 확인 실패');
        return;
      }
    } catch (error) {
      console.log('❌ 서버 연결 실패:', error.message);
      return;
    }
  }
  
  // 차단 복구 데이터 정리
  localStorage.removeItem('blockedStore');
  
  // ⭐ 서버 연결 테스트
  const serverConnected = await testServerConnection();
  if (!serverConnected) {
    console.error('❌ 서버 연결 실패 - 작업을 중단합니다');
    return;
  }
  
  // 자동으로 스마트스토어 링크 추출 및 전송
  setTimeout(async () => {
    console.log('🚀 자동 스마트스토어 링크 추출 시작...');
    await scrollAndCollectLinks();
  }, 3000); // 3초 후 자동 실행 (페이지 로딩 대기)
}

// ⭐ 서버 연결 테스트 함수
async function testServerConnection() {
  try {
    console.log('🔍 Predvia 서버 연결 테스트 중...');
    
    const response = await localFetch('http://localhost:8080/api/smartstore/status', {
      method: 'GET',
      headers: { 'Content-Type': 'application/json' }
    });
    
    if (response.ok) {
      console.log('✅ Predvia 서버 연결 성공');
      return true;
    } else {
      console.error('❌ 서버 응답 오류:', response.status);
      return false;
    }
  } catch (error) {
    console.error('❌ 서버 연결 실패:', error.message);
    console.log('💡 Predvia 프로그램이 실행 중인지 확인해주세요');
    return false;
  }
}

// 페이지 끝까지 스크롤하고 스마트스토어 링크 수집
async function scrollAndCollectLinks() {
  console.log('📜 페이지 끝까지 스크롤 - 스마트스토어 링크 수집');
  
  // localStorage에서 재시도 횟수 확인
  let retryCount = parseInt(localStorage.getItem('smartstore_retry_count') || '0');
  const maxRetries = 3;
  
  console.log(`🔄 현재 재시도 횟수: ${retryCount}/${maxRetries}`);
  
  // 최대 재시도 초과 시 바로 종료
  if (retryCount >= maxRetries) {
    console.log('❌ 최대 재시도 횟수 초과 - 수집된 링크로 진행');
    localStorage.removeItem('smartstore_retry_count');
    const smartStoreLinks = extractSmartStoreLinks();
    await sendSmartStoreLinksToServer(smartStoreLinks);
    return;
  }
  
  // 첫 시도가 아니면 잠시 대기 (새로고침 후)
  if (retryCount > 0) {
    console.log('🔄 새로고침 후 대기 중...');
    await new Promise(resolve => setTimeout(resolve, 3000));
  }

    let previousHeight = 0;
    let currentHeight = document.body.scrollHeight;
    let sameHeightCount = 0;
    let scrollAttempts = 0;
    const maxScrollAttempts = 15; // 더 많은 스크롤 시도

    // 작은 단위로 여러번 스크롤
    while (scrollAttempts < maxScrollAttempts && sameHeightCount < 6) {
      previousHeight = currentHeight;

      // 작은 단위로 스크롤 (300px씩)
      for (let i = 0; i < 5; i++) {
        window.scrollBy(0, 300);
        await new Promise(resolve => setTimeout(resolve, 100));
      }

      console.log(`📍 스크롤 ${scrollAttempts + 1}회 - 높이: ${currentHeight}px`);

      // 최소 대기 시간
      await new Promise(resolve => setTimeout(resolve, 200));

      currentHeight = document.body.scrollHeight;

      if (currentHeight === previousHeight) {
        sameHeightCount++;
        console.log(`⏸️ 동일 높이 ${sameHeightCount}번째`);
      } else {
        sameHeightCount = 0;
      }

      scrollAttempts++;
    }

    console.log(`📜 스크롤 완료 - 총 ${scrollAttempts}회 스크롤`);

    // 최종 대기 후 링크 수집
    await new Promise(resolve => setTimeout(resolve, 1000));
    
    // 스마트스토어 링크 수집
    const smartStoreLinks = extractSmartStoreLinks();
    
    console.log(`✅ 스크롤 완료: 총 ${smartStoreLinks.length}개 스마트스토어 링크 수집`);
    
    // 10개 이상 수집되면 성공
    if (smartStoreLinks.length >= 10) {
      console.log(`🎉 충분한 링크 수집 성공: ${smartStoreLinks.length}개`);
      localStorage.removeItem('smartstore_retry_count'); // 성공 시 카운터 리셋
      await sendSmartStoreLinksToServer(smartStoreLinks);
    } else {
      console.log(`⚠️ 링크 부족 (${smartStoreLinks.length}개) - 재시도 필요`);
      
      // 재시도 횟수 증가 및 저장
      retryCount++;
      localStorage.setItem('smartstore_retry_count', retryCount.toString());
      
      if (retryCount >= maxRetries) {
        console.log(`❌ 최대 재시도 횟수 초과 - ${smartStoreLinks.length}개로 진행`);
        localStorage.removeItem('smartstore_retry_count');
        await sendSmartStoreLinksToServer(smartStoreLinks);
      } else {
        // 새로고침으로 재시도
        console.log('🔄 페이지 새로고침으로 재시도...');
        const currentUrl = window.location.href;
        const separator = currentUrl.includes('?') ? '&' : '?';
        window.location.href = `${currentUrl}${separator}t=${Date.now()}`;
      }
    }

  // ⭐ 크롤링 완료 후 플래그 리셋 (항상 true 유지하므로 비활성화)
  // try {
  //   await localFetch('http://localhost:8080/api/crawling/allow', { method: 'DELETE' });
  //   console.log('🔄 크롤링 허용 플래그 리셋 완료');
  // } catch (error) {
  //   console.log('❌ 플래그 리셋 오류:', error.message);
  // }

  // ⭐ 링크 수집 완료 - 가격비교 창은 크롤링 완료까지 유지
  console.log('✅ 링크 수집 완료 - 가격비교 창은 크롤링 완료까지 유지');
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
  console.log('🔥🔥🔥 extractSmartStoreLinks 함수 시작');
  console.log('🔍 스마트스토어 링크 추출 시작');
  
  const smartStoreLinks = [];
  
  try {
    // 네이버 가격비교 페이지에서 스마트스토어 링크 찾기
    // 방법 1: "스마트스토어" 텍스트가 포함된 요소 찾기
    const smartStoreElements = document.querySelectorAll('*');
    console.log('🔥 전체 요소 개수:', smartStoreElements.length);
    
    let smartStoreTextCount = 0;
    
    smartStoreElements.forEach((element) => {
      const text = element.textContent || '';
      
      // "스마트스토어" 텍스트가 포함된 요소 찾기
      if (text.includes('스마트스토어') || text.includes('smartstore')) {
        smartStoreTextCount++;
        console.log('🔥 스마트스토어 텍스트 발견:', text.substring(0, 100));
        
        // 해당 요소나 부모 요소에서 링크 찾기
        const linkElement = element.closest('a') || element.querySelector('a');
        
        if (linkElement && linkElement.href) {
          const link = linkElement.href;
          console.log('🔥 링크 발견:', link);
          
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
    
    console.log('🔥 스마트스토어 텍스트 포함 요소:', smartStoreTextCount, '개');
    
    // 방법 2: 직접 스마트스토어 링크 패턴으로 찾기
    const allLinks = document.querySelectorAll('a[href*="smartstore.naver.com"], a[href*="brand.naver.com"]');
    console.log('🔥 smartstore 링크 패턴 요소:', allLinks.length, '개');
    
    allLinks.forEach((linkElement) => {
      const link = linkElement.href;
      console.log('🔥 패턴 링크 확인:', link);
      
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
  
  console.log(`🔥🔥🔥 총 ${smartStoreLinks.length}개 스마트스토어 링크 추출 완료`);
  return smartStoreLinks;
}

// ⭐ "추가" 버튼 전용: 상품명만 추출하고 전송
async function extractAndSendProductNames() {
  try {
    console.log('📝 "추가" 버튼 모드: 상품명만 추출 시작');
    
    // ⭐ 크롤링처럼 페이지 끝까지 스크롤 (1페이지 전체 상품명 수집)
    console.log('📜 페이지 끝까지 스크롤 - 상품명 수집');
    
    // 🔥 백그라운드 탭에서도 스크롤 작동하도록 강제 포커스
    window.focus();
    
    let scrollCount = 0;
    let lastHeight = 0;
    
    while (scrollCount < 10) { // 최대 10회 스크롤
      // 🔥 다중 스크롤 방식 (백그라운드에서도 작동)
      window.scrollTo(0, document.body.scrollHeight);
      document.documentElement.scrollTop = document.body.scrollHeight;
      document.body.scrollTop = document.body.scrollHeight;
      
      // 🔥 프로그래밍 방식 스크롤 이벤트 강제 발생
      window.dispatchEvent(new Event('scroll'));
      document.dispatchEvent(new Event('scroll'));
      
      await new Promise(resolve => setTimeout(resolve, 1500)); // 1초→1.5초 증가
      
      const currentHeight = document.body.scrollHeight;
      console.log(`📍 스크롤 ${scrollCount + 1}회 - 높이: ${currentHeight}px`);
      
      if (currentHeight === lastHeight) {
        // 🔥 높이 변화 없어도 2번 더 시도 (지연 로딩 대응)
        if (scrollCount >= 2) break;
      }
      
      lastHeight = currentHeight;
      scrollCount++;
    }
    
    console.log(`📜 스크롤 완료 - 총 ${scrollCount}회 스크롤`);
    
    // 🔥 스마트스토어 링크 강제 로딩 (백그라운드에서도 작동)
    await forceLoadSmartStoreLinks();
    
    // 최종 대기 후 상품명 수집
    await new Promise(resolve => setTimeout(resolve, 2000)); // 1초→2초 증가
    
    // ⭐ 페이지 구조 분석 (디버깅용)
    console.log('🔍 페이지 구조 분석 시작');
    const allLinks = document.querySelectorAll('a');
    const allDivs = document.querySelectorAll('div');
    const allSpans = document.querySelectorAll('span');
    console.log(`📊 전체 요소: a태그 ${allLinks.length}개, div태그 ${allDivs.length}개, span태그 ${allSpans.length}개`);
    
    // 상품명 추출
    const productNames = extractAllProductNames();
    
    if (productNames.length > 0) {
      console.log(`📝 ${productNames.length}개 상품명 추출 완료`);
      console.log('📝 추출된 상품명 샘플:', productNames.slice(0, 5));
      await sendProductNamesToServer(productNames);
    } else {
      console.log('❌ 추출된 상품명이 없습니다.');
      
      // ⭐ 대안: 모든 텍스트에서 상품명 추출 시도
      console.log('🔍 대안 방법: 모든 텍스트에서 상품명 추출 시도');
      const allText = document.body.innerText;
      const lines = allText.split('\n').filter(line => 
        line.trim().length > 5 && 
        !line.includes('네이버') && 
        !line.includes('쇼핑') &&
        !line.includes('광고') &&
        line.includes('원') // 가격이 포함된 라인 근처에 상품명이 있을 가능성
      );
      console.log('🔍 가능한 상품명 후보:', lines.slice(0, 10));
    }
    
  } catch (error) {
    console.error('❌ 상품명 추출 오류:', error);
  }
}

// ⭐ 네이버 가격비교 페이지에서 모든 상품명 추출
function extractAllProductNames() {
  console.log('🔍 상품명 추출 시작');
  
  const productNames = [];
  
  // title 속성에서 상품명 추출
  const elementsWithTitle = document.querySelectorAll('[title]');
  console.log(`🔍 title 속성을 가진 요소: ${elementsWithTitle.length}개 발견`);
  
  elementsWithTitle.forEach(element => {
    const title = element.getAttribute('title');
    if (title && 
        title.length > 10 && // 충분히 긴 제목만
        /[가-힣]/.test(title) && // 한글 포함
        !title.includes('광고') && 
        !title.includes('AD') &&
        !title.includes('스폰서') &&
        !title.includes('네이버') &&
        !title.includes('쇼핑') &&
        !title.includes('가격비교')) {
      productNames.push(title);
      console.log(`📝 상품명 발견: "${title}"`);
    }
  });
  
  // 추가로 일반적인 상품 링크에서도 title 확인
  const productLinks = document.querySelectorAll('a[href*="smartstore"], a[href*="product"], a[data-nclick]');
  console.log(`🔍 상품 링크: ${productLinks.length}개 발견`);
  
  productLinks.forEach(link => {
    const title = link.getAttribute('title');
    if (title && 
        title.length > 10 && 
        /[가-힣]/.test(title) && 
        !title.includes('광고') &&
        !productNames.includes(title)) {
      productNames.push(title);
      console.log(`📝 링크에서 상품명 발견: "${title}"`);
    }
  });
  
  console.log(`✅ 총 ${productNames.length}개 상품명 추출 완료`);
  if (productNames.length > 0) {
    console.log('📝 추출된 상품명 샘플:', productNames.slice(0, 3));
  }
  
  return productNames;
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
    console.log('🔥🔥🔥 sendSmartStoreLinksToServer 함수 시작');
    
    // 링크가 전달되지 않으면 현재 페이지에서 추출
    if (!smartStoreLinks) {
      console.log('🔥 smartStoreLinks가 null이므로 추출 시작');
      smartStoreLinks = extractSmartStoreLinks();
      console.log('🔥 추출 결과:', smartStoreLinks.length, '개');
    }
    
    if (smartStoreLinks.length === 0) {
      console.log('⚠️ 추출된 스마트스토어 링크가 없습니다.');
      console.log('🔥 페이지 URL:', window.location.href);
      console.log('🔥 페이지 제목:', document.title);
      console.log('🔥 페이지 내용 샘플:', document.body.textContent.substring(0, 500));
      return;
    }
    
    const data = {
      smartStoreLinks: smartStoreLinks,
      source: 'naver_price_comparison',
      timestamp: new Date().toISOString(),
      pageUrl: window.location.href
    };
    
    console.log('🔥🔥🔥 요청 URL: http://localhost:8080/api/smartstore/links');
    console.log('🔥🔥🔥 전송할 데이터 크기:', JSON.stringify(data).length, 'bytes');
    console.log('전송할 데이터:', JSON.stringify({
      smartStoreLinks: data.smartStoreLinks.slice(0, 5) // 처음 5개만 로그로 확인
    }, null, 2));
    
    console.log('🔥🔥🔥 fetch 요청 시작...');
    
    const response = await localFetch('http://localhost:8080/api/smartstore/links', {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        'Origin': 'chrome-extension'
      },
      body: JSON.stringify(data)
    });
    
    console.log('🔥🔥🔥 응답 상태:', response.status);
    console.log('🔥🔥🔥 응답 헤더:', [...response.headers.entries()]);
    
    if (response.ok) {
      console.log('✅ 서버 통신 성공 - 응답 확인 중');
      
      try {
        // ⭐ 응답 텍스트 먼저 확인
        const responseText = await response.text();
        console.log('📡 서버 응답 길이:', responseText.length);
        console.log('📡 서버 응답 내용:', responseText.substring(0, 200));
        
        if (!responseText || responseText.trim().length === 0) {
          console.error('❌ 서버에서 완전히 빈 응답 수신');
          console.log('🔄 폴백: 모든 스토어 방문으로 전환');
          await visitSmartStoreLinksSequentially(smartStoreLinks);
          return;
        }
        
        // ⭐ JSON 파싱 시도
        let responseData;
        try {
          responseData = JSON.parse(responseText);
          console.log('✅ JSON 파싱 성공');
        } catch (parseError) {
          console.error('❌ JSON 파싱 실패:', parseError.message);
          console.log('📄 원본 응답:', responseText);
          console.log('🔄 폴백: 모든 스토어 방문으로 전환');
          await visitSmartStoreLinksSequentially(smartStoreLinks);
          return;
        }
        
        console.log('📊 서버 응답 데이터:', responseData);
        
        // ⭐ 응답 유효성 검사
        if (!responseData || typeof responseData !== 'object') {
          console.error('❌ 잘못된 응답 형식');
          console.log('🔄 폴백: 모든 스토어 방문으로 전환');
          await visitSmartStoreLinksSequentially(smartStoreLinks);
          return;
        }
        
        if (responseData.success === true) {
          console.log(`📊 ${responseData.totalLinks || 0}개 중 ${responseData.selectedLinks || 0}개 스토어 선택됨`);
          console.log(`🎯 목표: ${responseData.targetProducts || 100}개 상품 수집`);
          
          // ⭐ 서버에서 선택된 스토어 목록 받기
          if (responseData.selectedStores && Array.isArray(responseData.selectedStores) && responseData.selectedStores.length > 0) {
            console.log('🎯 선택된 스토어만 방문 시작:');
            responseData.selectedStores.forEach((store, index) => {
              console.log(`  ${index + 1}. ${store.title || '제목없음'} (${store.storeId || 'ID없음'})`);
            });
            
            // ⭐ 선택된 스토어만 방문
            visitSelectedStoresOnly(responseData.selectedStores); // await 제거 - 백그라운드에서 실행
            
            // 🔥 네이버 가격비교 완료 - 즉시 창 닫기 (v1.78)
            console.log('🔥 네이버 가격비교 링크 수집 완료 - 창 유지 (스토어 접속을 위해)');
          } else {
            console.error('❌ 선택된 스토어 목록이 없거나 잘못됨');
            console.log('🔄 폴백: 모든 스토어 방문으로 전환');
            visitSmartStoreLinksSequentially(smartStoreLinks); // await 제거
            
            // 🔥 폴백 완료 - 즉시 창 닫기 (v1.78)
            console.log('🔥 폴백 시작 - 창 유지 (스토어 접속을 위해)');
          }
        } else {
          console.error('❌ 서버에서 실패 응답:', responseData.error || '알 수 없는 오류');
          console.log('🔄 폴백: 모든 스토어 방문으로 전환');
          visitSmartStoreLinksSequentially(smartStoreLinks); // await 제거
          
          // 🔥 폴백 완료 - 즉시 창 닫기 (v1.78)
          console.log('🔥 폴백 시작 - 창 유지 (스토어 접속을 위해)');
        }
        
      } catch (processError) {
        console.error('❌ 응답 처리 오류:', processError);
        console.log('🔄 폴백: 모든 스토어 방문으로 전환');
        await visitSmartStoreLinksSequentially(smartStoreLinks);
      }
      
    } else {
      console.error('❌ 서버 응답 오류:', response.status, response.statusText);
      console.log('🔄 폴백: 모든 스토어 방문으로 전환');
      await visitSmartStoreLinksSequentially(smartStoreLinks);
    }
    
  } catch (error) {
    console.error('❌ Predvia 통신 오류:', error);
    console.error('❌ 오류 타입:', error.constructor.name);
    console.error('❌ 오류 메시지:', error.message);
    console.error('❌ 오류 스택:', error.stack);
    
    // ⭐ 네트워크 오류 상세 분석
    if (error.name === 'TypeError' && error.message.includes('fetch')) {
      console.error('🌐 네트워크 연결 오류 - Predvia 서버가 실행 중인지 확인');
    } else if (error.name === 'SyntaxError') {
      console.error('📄 JSON 파싱 오류 - 서버 응답 형식 문제');
    } else {
      console.error('❓ 알 수 없는 오류 유형');
    }
    
    console.log('💡 Predvia 프로그램이 실행 중인지 확인해주세요.');
    console.log('💡 localhost:8080 포트가 열려있는지 확인해주세요.');
    
    // ⭐ 오류 발생 시에도 폴백으로 모든 스토어 방문
    console.log('🔄 오류 발생으로 폴백: 모든 스토어 방문으로 전환');
    try {
      await visitSmartStoreLinksSequentially(smartStoreLinks);
    } catch (fallbackError) {
      console.error('❌ 폴백 실행도 실패:', fallbackError);
    }
  }
}

// ⭐ 선택된 스토어만 방문하는 함수
async function visitSelectedStoresOnly(selectedStores) {
  console.log(`🚀 선택된 ${selectedStores.length}개 스토어만 순차 접속 시작`);
  
  // ⭐ 순차 처리를 위한 재귀 함수
  async function processStoreSequentially(index) {
    if (index >= selectedStores.length) {
      console.log(`🎉 선택된 ${selectedStores.length}개 스토어 방문 완료!`);
      return;
    }
    
    const store = selectedStores[index];
    
    try {
      // ⭐ Background Script에서 처리 권한 요청
      await requestProcessingPermission(store.storeId, store.title);
      
      // ⭐ 서버에서 중단 신호 확인
      const shouldStop = await checkShouldStop();
      if (shouldStop) {
        console.log(`🛑 목표 달성으로 크롤링 중단 (${index + 1}/${selectedStores.length}번째에서 중단)`);
        // ⭐ 처리 권한 해제
        await releaseProcessingPermission(store.storeId);
        return;
      }
      
      const storeId = store.storeId;
      
      if (!storeId) {
        console.log(`❌ [${index + 1}/${selectedStores.length}] 스토어 ID 없음: ${store.title}`);
        // ⭐ 처리 권한 해제
        await releaseProcessingPermission(store.storeId);
        // 다음 스토어 처리
        await processStoreSequentially(index + 1);
        return;
      }

      // ⭐ 스토어별 고유 runId 생성
      const runId = `${storeId}-${Date.now()}-${Math.random().toString(36).slice(2,8)}`;
      console.log(`🆔 ${storeId}: 고유 runId 생성 - ${runId}`);

      // 공구탭 URL 생성 (runId 포함)
      const gongguUrl = `https://smartstore.naver.com/${storeId}/category/50000165?cp=1&runId=${runId}`;
      
      console.log(`📍 [${index + 1}/${selectedStores.length}] 공구탭 접속: ${store.title}`);
      console.log(`🔗 스토어 ID: ${storeId}`);
      console.log(`🔗 공구탭 URL: ${gongguUrl}`);
      
      // ⭐ 서버에 방문 알림 (선택된 스토어인지 확인)
      const visitResponse = await notifyStoreVisit({
        url: store.url,
        title: store.title,
        storeId: storeId,
        gongguUrl: gongguUrl,
        currentIndex: index + 1,
        totalCount: selectedStores.length,
        timestamp: new Date().toISOString()
      });
      
      // ⭐ 목표 달성 시 중단
      if (visitResponse && visitResponse.stop) {
        console.log(`🎉 목표 달성! 총 ${visitResponse.totalProducts}개 상품 수집 완료`);
        
        // ⭐ 완료 신호 전송
        try {
          await localFetch('http://localhost:8080/api/smartstore/all-stores-completed', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' }
          });
          console.log('✅ 목표 달성 완료 신호 전송 완료');
        } catch (error) {
          console.error('❌ 완료 신호 전송 실패:', error);
        }
        
        // ⭐ 처리 권한 해제
        await releaseProcessingPermission(storeId);
        return;
      }
      
      // ⭐ 순차 처리 위반 시 스킵
      if (visitResponse && visitResponse.success === false) {
        console.log(`🚫 ${storeId}: 순차 처리 위반 - 스킵`);
        // ⭐ 처리 권한 해제
        await releaseProcessingPermission(storeId);
        // 다음 스토어 처리
        await processStoreSequentially(index + 1);
        return;
      }
      
      // ⭐ 즉시 서버에 "진행중" 상태 기록
      await setStoreState(storeId, runId, 'collecting', true);
      
      // 새 탭에서 공구탭 열기
      chrome.runtime.sendMessage({action: 'openAppWindow', url: gongguUrl, storeId: storeId});
      
      // ⭐ 탭 열기 후 3초 강제 대기 (탭이 완전히 로드될 때까지)
      console.log(`⏳ ${storeId}: 탭 로딩 대기 중...`);
      await new Promise(resolve => setTimeout(resolve, 3000));
      
      // ⭐ 1000개 이하 스토어만 3초 후 즉시 완료, 1000개 이상은 대기
      const smallStores = ['jtemshop', 'dongsmarkett', 'swstore1316', 'jardine01', 'kind9', 'bigwheel', 'carpedime', 'rootselect'];
      
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
      
      // ⭐ 처리 권한 해제
      await releaseProcessingPermission(storeId);
      console.log(`🔓 ${store.title}: 처리 권한 해제 (완료)`);
      
      // 2초 대기 후 다음 스토어
      await new Promise(resolve => setTimeout(resolve, 2000));
      
      // 다음 스토어 처리
      await processStoreSequentially(index + 1);
      
    } catch (error) {
      console.log(`❌ [${index + 1}/${selectedStores.length}] 오류: ${error.message}`);
      // ⭐ 오류 시에도 처리 권한 해제
      await releaseProcessingPermission(store.storeId);
      console.log(`🔓 ${store.title}: 처리 권한 해제 (오류)`);
      
      // 다음 스토어 처리
      await processStoreSequentially(index + 1);
    }
  }
  
  // 첫 번째 스토어부터 시작
  await processStoreSequentially(0);
  
  // ⭐ 모든 스토어 방문 완료 후 즉시 창 닫기
  console.log('🔥 네이버 가격비교 페이지 작업 완료 - 창 유지 (스토어 접속을 위해)');
}

// 스마트스토어 링크들을 순차적으로 방문 (공구탭으로 변환)
async function visitSmartStoreLinksSequentially(smartStoreLinks) {
  console.log(`🚀 ${smartStoreLinks.length}개 스마트스토어 공구탭 순차 접속 시작`);
  
  // ⭐ 재귀 함수로 순차 처리 보장
  async function processLinkSequentially(index) {
    if (index >= smartStoreLinks.length) {
      console.log('✅ 모든 스마트스토어 공구탭 작업 완료');
      return;
    }
    
    const link = smartStoreLinks[index];
    
    try {
      // 스마트스토어 ID 추출
      const storeId = extractStoreId(link.url);
      
      if (!storeId) {
        console.log(`❌ [${index + 1}/${smartStoreLinks.length}] 스토어 ID 추출 실패: ${link.title}`);
        // 다음 링크 처리
        await processLinkSequentially(index + 1);
        return;
      }

      // ⭐ Background Script에서 처리 권한 요청
      await requestProcessingPermission(storeId, link.title);
      
      // ⭐ 서버에서 중단 신호 확인
      const shouldStop = await checkShouldStop();
      if (shouldStop) {
        console.log(`🛑 목표 달성으로 크롤링 중단 (${index + 1}/${smartStoreLinks.length}번째에서 중단)`);
        // ⭐ 처리 권한 해제
        await releaseProcessingPermission(storeId);
        return;
      }

      // ⭐ 스토어별 고유 runId 생성
      const runId = `${storeId}-${Date.now()}-${Math.random().toString(36).slice(2,8)}`;
      console.log(`🆔 ${storeId}: 고유 runId 생성 - ${runId}`);

      // 공구탭 URL 생성 (runId 포함)
      const gongguUrl = `https://smartstore.naver.com/${storeId}/category/50000165?cp=1&runId=${runId}`;
      
      console.log(`📍 [${index + 1}/${smartStoreLinks.length}] 공구탭 접속: ${link.title}`);
      console.log(`🔗 스토어 ID: ${storeId}`);
      console.log(`🔗 공구탭 URL: ${gongguUrl}`);
      
      // ⭐ 서버에 방문 알림 (선택된 스토어인지 확인)
      const visitResponse = await notifyStoreVisit({
        url: link.url,
        title: link.title,
        storeId: storeId,
        gongguUrl: gongguUrl,
        currentIndex: index + 1,
        totalCount: smartStoreLinks.length,
        timestamp: new Date().toISOString()
      });
      
      // ⭐ 선택되지 않은 스토어는 건너뛰기
      if (visitResponse && visitResponse.skip) {
        console.log(`⏭️ 선택되지 않은 스토어 건너뛰기: ${storeId}`);
        // ⭐ 처리 권한 해제
        await releaseProcessingPermission(storeId);
        // 다음 링크 처리
        await processLinkSequentially(index + 1);
        return;
      }
      
      // ⭐ 목표 달성 시 중단
      if (visitResponse && visitResponse.stop) {
        console.log(`🎉 목표 달성! 총 ${visitResponse.totalProducts}개 상품 수집 완료`);
        
        // ⭐ 완료 신호 전송
        try {
          await localFetch('http://localhost:8080/api/smartstore/all-stores-completed', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' }
          });
          console.log('✅ 목표 달성 완료 신호 전송 완료');
        } catch (error) {
          console.error('❌ 완료 신호 전송 실패:', error);
        }
        
        // ⭐ 처리 권한 해제
        await releaseProcessingPermission(storeId);
        return;
      }
      
      // ⭐ 순차 처리 위반 시 스킵
      if (visitResponse && visitResponse.success === false) {
        console.log(`🚫 ${storeId}: 순차 처리 위반 - 스킵`);
        // ⭐ 처리 권한 해제
        await releaseProcessingPermission(storeId);
        // 다음 링크 처리
        await processLinkSequentially(index + 1);
        return;
      }
      
      // ⭐ 즉시 서버에 "진행중" 상태 기록
      await setStoreState(storeId, runId, 'collecting', true);
      
      // 새 탭에서 공구탭 열기
      chrome.runtime.sendMessage({action: 'openAppWindow', url: gongguUrl, storeId: storeId});
      
      // ⭐ 탭 열기 후 3초 강제 대기 (탭이 완전히 로드될 때까지)
      console.log(`⏳ ${storeId}: 탭 로딩 대기 중...`);
      await new Promise(resolve => setTimeout(resolve, 3000));
      
      // ⭐ 1000개 이하 스토어만 3초 후 즉시 완료, 1000개 이상은 대기
      const smallStores = ['jtemshop', 'dongsmarkett', 'swstore1316', 'jardine01', 'kind9', 'bigwheel', 'carpedime', 'rootselect'];
      
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
      }, index + 1, smartStoreLinks.length);
      
      // ⭐ runId 기반 완료 대기 (진짜 막는 지점)
      console.log(`🔍 ${storeId}: 완료 대기 시작 (runId: ${runId})`);
      await waitForTaskCompletion(storeId, runId);
      console.log(`✅ ${storeId}: 완료 대기 끝`);
      
      // ⭐ 처리 권한 해제
      await releaseProcessingPermission(storeId);
      console.log(`🔓 ${link.title}: 처리 권한 해제 (완료)`);
      
      // 탭 닫기 (안전하게)
      try {
        if (newTab && typeof newTab.close === 'function' && !newTab.closed) {
          newTab.close();
          console.log(`🗂️ ${storeId}: 탭 닫기 완료`);
        }
      } catch (e) {
        console.log(`⚠️ ${storeId}: 탭 닫기 실패 - ${e.message}`);
      }
      
      console.log(`✅ [${index + 1}/${smartStoreLinks.length}] 작업 완료: ${link.title}`);
      
      // 다음 링크 처리
      await processLinkSequentially(index + 1);
      
    } catch (error) {
      console.error(`❌ 링크 처리 오류 [${index + 1}]: ${link.title}`, error);
      
      // 스토어 ID가 있으면 처리 권한 해제
      const storeId = extractStoreId(link.url);
      if (storeId) {
        await releaseProcessingPermission(storeId);
        console.log(`🔓 ${link.title}: 처리 권한 해제 (오류)`);
      }
      
      // 다음 링크 처리
      await processLinkSequentially(index + 1);
    }
  }
  
  // 첫 번째 링크부터 시작
  await processLinkSequentially(0);
}

// ⭐ 서버 상태 설정 함수
async function setStoreState(storeId, runId, state, lock, expected = 0, progress = 0) {
  try {
    const response = await localFetch('http://localhost:8080/api/smartstore/state', {
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
      const response = await localFetch(`http://localhost:8080/api/smartstore/state?storeId=${storeId}&runId=${runId}`);
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
    const response = await localFetch('http://localhost:8080/api/smartstore/status', {
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
    const response = await localFetch('http://localhost:8080/api/smartstore/visit', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(visitData)
    });
    
    if (response.ok) {
      try {
        const responseText = await response.text();
        console.log(`📡 서버 응답 텍스트: ${responseText}`);
        
        if (responseText.trim()) {
          const jsonData = JSON.parse(responseText);
          console.log(`📊 파싱된 응답:`, jsonData);
          return jsonData;
        } else {
          console.log('⚠️ 서버 응답 없음 - 크롤링 계속 진행');
          return { success: true, message: "No server response - continue crawling" };
        }
      } catch (jsonError) {
        console.log('JSON 파싱 오류:', jsonError);
        // JSON 파싱 실패 시 순차 처리 위반으로 간주
        return { success: false, message: "JSON parsing failed - sequential violation" };
      }
    } else {
      console.log(`❌ HTTP 오류: ${response.status}`);
      return { success: false, message: `HTTP error: ${response.status}` };
    }
  } catch (error) {
    console.log('방문 알림 오류:', error);
    return { success: false, message: `Network error: ${error.message}` };
  }
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
    
    await localFetch('http://localhost:8080/api/smartstore/gonggu-check', {
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
    
    await localFetch('http://localhost:8080/api/smartstore/visit', {
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

// ⭐ 상품명을 서버로 전송하는 함수
async function sendProductNamesToServer(productNames) {
  try {
    console.log(`📝 상품명 ${productNames.length}개 서버 전송 시작`);
    
    const data = {
      productNames: productNames,
      pageUrl: window.location.href,
      timestamp: new Date().toISOString()
    };
    
    const response = await localFetch('http://localhost:8080/api/smartstore/product-names', {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        'Origin': 'chrome-extension'
      },
      body: JSON.stringify(data)
    });
    
    if (response.ok) {
      console.log(`✅ 상품명 ${productNames.length}개 서버 전송 완료`);
      
      // ⭐ 키워드 태그 실시간 표시 요청
      console.log('🏷️ 키워드 태그 실시간 표시 요청 전송');
      try {
        await localFetch('http://localhost:8080/api/smartstore/trigger-keywords', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ action: 'show_keywords' })
        });
        console.log('✅ 키워드 태그 표시 요청 완료');
        
        // ⭐ 잠시 후 SourcingPage에서 키워드를 가져가도록 추가 요청
        setTimeout(async () => {
          try {
            await localFetch('http://localhost:8080/api/smartstore/latest-keywords', {
              method: 'GET'
            });
            console.log('✅ 키워드 가져가기 신호 전송 완료');
          } catch (fetchError) {
            console.log('❌ 키워드 가져가기 신호 실패:', fetchError);
          }
        }, 1000);
        
      } catch (triggerError) {
        console.log('❌ 키워드 태그 표시 요청 실패:', triggerError);
      }
      
    } else {
      console.log(`❌ 상품명 서버 전송 실패: ${response.status}`);
    }
    
  } catch (error) {
    console.error('❌ 상품명 전송 오류:', error);
  }
}

// ⭐ 서버로 로그 전송 함수
async function sendLogToServer(message) {
  try {
    await localFetch('http://localhost:8080/api/smartstore/log', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ message, timestamp: new Date().toISOString() })
    });
  } catch (error) {
    console.log('로그 전송 실패:', error);
  }
}

// ⭐ 상품 페이지에서 리뷰 수집
async function collectProductReviews() {
  try {
    const url = window.location.href;
    const storeMatch = url.match(/smartstore\.naver\.com\/([^\/]+)/);
    const productMatch = url.match(/products\/(\d+)/);
    
    if (!storeMatch || !productMatch) {
      console.log('❌ 스토어ID 또는 상품ID를 찾을 수 없음');
      return;
    }
    
    const storeId = storeMatch[1];
    const productId = productMatch[1];
    
    console.log(`📊 리뷰 수집 시작: ${storeId}/${productId}`);
    await sendLogToServer(`📊 ${storeId}: 리뷰 수집 시작`);
    
    // 페이지 로딩 대기
    await new Promise(resolve => setTimeout(resolve, 2000));
    
    const reviews = [];
    
    // v1.25에서 사용한 정확한 선택자 사용
    const ratingElements = document.querySelectorAll('em.n6zq2yy0KA');
    const reviewContentElements = document.querySelectorAll('.vhlVUsCtw3 .K0kwJOXP06');
    
    console.log(`📊 발견된 별점: ${ratingElements.length}개, 리뷰 내용: ${reviewContentElements.length}개`);
    await sendLogToServer(`📊 ${storeId}: 별점 ${ratingElements.length}개, 리뷰 내용 ${reviewContentElements.length}개 발견`);
    
    // 리뷰 데이터 수집
    const maxReviews = Math.max(ratingElements.length, reviewContentElements.length);
    
    for (let i = 0; i < maxReviews; i++) {
      let rating = 5.0;
      let content = '';
      
      // 별점 추출
      if (i < ratingElements.length) {
        const ratingText = ratingElements[i].textContent.trim();
        rating = parseFloat(ratingText) || 5.0;
      }
      
      // 리뷰 내용 추출
      if (i < reviewContentElements.length) {
        content = reviewContentElements[i].textContent.trim();
      }
      
      if (rating || content) {
        reviews.push({
          rating: rating,
          content: content || `평점 ${rating}점`
        });
        
        console.log(`⭐ 리뷰 ${i+1}: 평점=${rating}, 내용="${content.substring(0, 50)}..."`);
        await sendLogToServer(`⭐ ${storeId}: 리뷰 ${i+1} - 평점 ${rating}점`);
      }
    }
    
    // 서버로 리뷰 데이터 전송
    if (reviews.length > 0) {
      const reviewData = {
        storeId: storeId,
        productId: productId,
        productUrl: url,
        reviews: reviews,
        reviewCount: reviews.length,
        timestamp: new Date().toISOString()
      };
      
      const response = await localFetch('http://localhost:8080/api/smartstore/reviews', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          'Origin': 'chrome-extension'
        },
        body: JSON.stringify(reviewData)
      });
      
      if (response.ok) {
        console.log(`✅ 리뷰 ${reviews.length}개 서버 전송 완료`);
        await sendLogToServer(`✅ ${storeId}: 리뷰 ${reviews.length}개 서버 전송 완료`);
      } else {
        console.log(`❌ 리뷰 서버 전송 실패: ${response.status}`);
        await sendLogToServer(`❌ ${storeId}: 리뷰 서버 전송 실패`);
      }
    } else {
      console.log(`❌ 리뷰 없음: ${storeId}/${productId}`);
      await sendLogToServer(`❌ ${storeId}: 리뷰 데이터 없음`);
    }
    
  } catch (error) {
    console.error('❌ 리뷰 수집 오류:', error);
    await sendLogToServer(`❌ 리뷰 수집 오류: ${error.message}`);
  }
}

// 상품 페이지에서 자동으로 리뷰 수집 실행
if (window.location.href.includes('smartstore.naver.com') && window.location.href.includes('/products/')) {
  console.log('🎯 상품 페이지 감지 - 리뷰 수집 준비');
  
  // 페이지 로드 완료 후 리뷰 수집
  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', () => {
      setTimeout(async () => {
        await collectProductReviews();
      }, 3000);
    });
  } else {
    setTimeout(async () => {
      await collectProductReviews();
    }, 3000);
  }
}

// ⭐ 네이버 가격비교 페이지에서 모든 스토어 완료 감지 시작
if (window.location.href.includes('search.shopping.naver.com')) {
  console.log('🔍 네이버 가격비교 페이지 - 모든 스토어 완료 감지 시작');
  startAllStoresCompletionCheck();
}

// ⭐ 타오바오 페이지에서 이미지 검색 버튼 자동 클릭
if (window.location.href.includes('taobao.com')) {
  console.log('🔍 타오바오 페이지 감지!');
  console.log('🌐 현재 URL:', window.location.href);
  console.log('⏰ 페이지 로드 시간:', new Date().toLocaleString());
  console.log('⏳ 2초 후 이미지 검색 버튼 클릭 시도...');
  
  setTimeout(() => {
    clickTaobaoImageSearchButton();
  }, 2000); // 2초 후 클릭
}

// ⭐ 타오바오 이미지 검색 버튼 클릭
function clickTaobaoImageSearchButton() {
  console.log('🔍 === 타오바오 이미지 검색 (DevTools Protocol 방식) ===');
  console.log('ℹ️ 서버에서 자동으로 이미지를 업로드합니다.');
  console.log('ℹ️ 별도의 버튼 클릭이나 붙여넣기가 필요하지 않습니다.');
}

// ⭐ 클립보드에서 이미지 붙여넣기 (Ctrl+V)
function findAndTriggerFileUpload() {
  console.log('📁 === 파일 업로드 input 찾기 시작 ===');
  
  try {
    // 타오바오 이미지 업로드 input 찾기
    const fileInputs = document.querySelectorAll('input[type="file"]');
    console.log(`🔍 발견된 file input 개수: ${fileInputs.length}`);
    
    if (fileInputs.length > 0) {
      const fileInput = fileInputs[0];
      console.log('✅ 파일 업로드 input 발견!');
      
      // 사용자에게 파일 선택 다이얼로그 표시
      fileInput.click();
      console.log('✅ 파일 선택 다이얼로그 열기 완료');
      sendLogToServer('✅ 타오바오 파일 선택 다이얼로그 열기 완료');
    } else {
      console.log('❌ 파일 업로드 input을 찾을 수 없음');
      sendLogToServer('❌ 타오바오 파일 업로드 input을 찾을 수 없음');
    }
  } catch (error) {
    console.error('❌ 파일 업로드 오류:', error);
    sendLogToServer(`❌ 파일 업로드 오류: ${error.message}`);
  }
  
  console.log('📁 === 파일 업로드 input 찾기 종료 ===');
}

function pasteImageFromClipboard() {
  console.log('📋 === 클립보드 이미지 붙여넣기 (사용 안 함) ===');
  // DevTools Protocol 방식으로 변경되어 이 함수는 더 이상 사용하지 않음
  console.log('ℹ️ 서버에서 DevTools Protocol로 직접 업로드합니다.');
}

// ⭐ 서버에 로그 전송
async function sendLogToServer(message) {
  try {
    await localFetch('http://localhost:8080/api/log', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ message: message })
    });
  } catch (error) {
    console.error('로그 전송 실패:', error);
  }
}

// ⭐ 모든 스토어 완료 감지 시스템
function startAllStoresCompletionCheck() {
  console.log('🔍 모든 스토어 완료 감지 시작...');
  
  // 30초마다 체크
  const checkInterval = setInterval(async () => {
    try {
      const response = await localFetch('http://localhost:8080/api/smartstore/crawling-status');
      const status = await response.json();
      
      console.log(`📊 크롤링 상태: ${status.processedStores}/${status.totalStores} 스토어 완료, ${status.currentCount}/100개 수집`);
      
      // 모든 스토어가 완료된 경우 (100개 달성 여부와 관계없이)
      if (status.processedStores >= status.totalStores && status.totalStores > 0) {
        console.log('🎉 모든 스토어 완료 감지! 서버에 알림 전송...');
        clearInterval(checkInterval);
        
        // 서버에 모든 스토어 완료 알림
        await localFetch('http://localhost:8080/api/smartstore/all-stores-completed', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ 
            message: '모든 스토어 방문 완료',
            finalCount: status.currentCount 
          })
        });
        
        console.log('✅ 모든 스토어 완료 알림 전송 완료');
        return;
      }
      
      // 100개 달성 시에도 체크 중단
      if (status.currentCount >= 100) {
        console.log('🎯 100개 달성으로 완료 체크 중단');
        clearInterval(checkInterval);
        return;
      }
      
    } catch (error) {
      console.error('❌ 완료 상태 체크 오류:', error);
    }
  }, 30000); // 30초마다 체크
}

// 🔥 백그라운드에서도 스마트스토어 링크 강제 로딩
async function forceLoadSmartStoreLinks() {
  console.log('🔥 스마트스토어 링크 강제 로딩 시작');
  
  // 1. 모든 이미지 강제 로드
  const images = document.querySelectorAll('img[data-src], img[loading="lazy"]');
  images.forEach(img => {
    if (img.dataset.src) {
      img.src = img.dataset.src;
    }
    img.loading = 'eager';
  });
  
  // 2. 지연 로딩 요소들 강제 트리거
  const lazyElements = document.querySelectorAll('[data-lazy], [data-src]');
  lazyElements.forEach(el => {
    // Intersection Observer 이벤트 강제 발생
    const event = new Event('intersect');
    el.dispatchEvent(event);
  });
  
  // 3. 페이지 전체 다시 렌더링 강제
  document.body.style.display = 'none';
  document.body.offsetHeight; // 강제 리플로우
  document.body.style.display = '';
  
  console.log('🔥 스마트스토어 링크 강제 로딩 완료');
}
