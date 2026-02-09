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

// Add this to your main crawling loop
async function crawlWithStopCheck() {
  for (let i = 0; i < products.length; i++) {
    // Check stop signal before processing each product
    if (await checkShouldStop()) {
      console.log('🛑 서버 중단 신호 감지 - 크롤링 중단');
      break; // This break statement was likely missing
    }
    
    // Process product...
    await processProduct(products[i]);
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
  return false; // Add default return value
}
