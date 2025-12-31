// 타오바오 페이지에서 자동으로 쿠키 수집하는 스크립트

console.log('🍪 타오바오 자동 쿠키 수집 스크립트 시작');

let lastCollectionTime = 0;
const COLLECTION_INTERVAL = 5000; // 5초 간격으로 제한

// 페이지 로드 완료 후 쿠키 수집
function collectAndSendCookies() {
    const now = Date.now();
    if (now - lastCollectionTime < COLLECTION_INTERVAL) {
        console.log('⏳ 쿠키 수집 간격 제한 (5초 대기)');
        return;
    }
    
    lastCollectionTime = now;
    console.log('🔍 타오바오 쿠키 수집 중...');
    
    // Chrome 확장프로그램에서 쿠키 수집 요청
    chrome.runtime.sendMessage({
        action: 'collectTaobaoCookies',
        url: window.location.href
    }, function(response) {
        if (chrome.runtime.lastError) {
            console.error('쿠키 수집 요청 오류:', chrome.runtime.lastError);
        } else {
            console.log('✅ 쿠키 수집 요청 완료:', response);
        }
    });
}

// 페이지 로드 완료 후 2초 뒤 쿠키 수집
setTimeout(collectAndSendCookies, 2000);

// 로그인 상태 변경 감지 (로그인 버튼이 사라지면 쿠키 재수집)
const observer = new MutationObserver(function(mutations) {
    mutations.forEach(function(mutation) {
        if (mutation.type === 'childList') {
            // 로그인 관련 요소 변경 감지
            const loginElements = document.querySelectorAll('[data-spm*="login"], .login, #login');
            if (loginElements.length === 0) {
                // 로그인 요소가 사라졌으면 로그인 완료로 간주
                console.log('🔐 로그인 상태 변경 감지, 쿠키 재수집');
                setTimeout(collectAndSendCookies, 1000);
            }
        }
    });
});

// DOM 변경 감지 시작
observer.observe(document.body, {
    childList: true,
    subtree: true
});

console.log('🍪 타오바오 자동 쿠키 수집 스크립트 준비 완료');
