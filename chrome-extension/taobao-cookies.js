// 타오바오 쿠키 수집 및 전송 (Background Script 방식)
let isCollecting = false; // 중복 수집 방지 플래그

async function collectTaobaoCookies() {
    if (isCollecting) {
        console.log('⏳ 이미 쿠키 수집 중입니다...');
        return false;
    }
    
    isCollecting = true;
    
    try {
        console.log('🍪 타오바오 쿠키 수집 시작...');
        
        // Background Script에 쿠키 수집 요청
        const response = await chrome.runtime.sendMessage({
            action: 'collectTaobaoCookies'
        });
        
        if (response && response.success) {
            console.log('✅ 타오바오 쿠키 수집 완료');
            return true;
        } else {
            console.log('❌ 쿠키 수집 실패:', response?.error || 'Unknown error');
            return false;
        }
        
    } catch (error) {
        console.error('❌ 쿠키 수집 오류:', error);
        return false;
    } finally {
        isCollecting = false; // 플래그 해제
    }
}

// 타오바오 페이지에서 자동으로 쿠키 수집
if (window.location.hostname.includes('taobao.com')) {
    // 페이지 로드 완료 후 쿠키 수집
    setTimeout(() => {
        collectTaobaoCookies();
    }, 2000);
}

// 메시지 리스너 (다른 스크립트에서 쿠키 수집 요청 시)
chrome.runtime.onMessage.addListener((request, sender, sendResponse) => {
    if (request.action === 'collectTaobaoCookies') {
        collectTaobaoCookies().then(success => {
            sendResponse({ success });
        });
        return true; // 비동기 응답
    }
});
