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

// 크롤링 루프에 중단 체크 추가
async function 크롤링루프() {
  for (let i = 0; i < 상품목록.length; i++) {
    // 각 상품 처리 전 중단 신호 체크
    if (await checkShouldStop()) {
      console.log('🛑 서버 중단 신호 감지 - 크롤링 중단');
      break; // 이 break문이 누락되었을 가능성
    }
    
    // 상품 처리...
    await 상품처리(상품목록[i]);
  }
}

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
  return false; // 기본 반환값 추가
}
