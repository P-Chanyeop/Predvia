// ⭐ 중앙 집중식 순차 처리 시스템
console.log('🚀 Predvia 중앙 순차 처리 시스템 시작');

// ⭐ MD5 해시 함수 (타오바오 서명용)
function md5(string) {
    function md5cycle(x, k) {
        var a = x[0], b = x[1], c = x[2], d = x[3];
        a = ff(a, b, c, d, k[0], 7, -680876936); d = ff(d, a, b, c, k[1], 12, -389564586);
        c = ff(c, d, a, b, k[2], 17, 606105819); b = ff(b, c, d, a, k[3], 22, -1044525330);
        a = ff(a, b, c, d, k[4], 7, -176418897); d = ff(d, a, b, c, k[5], 12, 1200080426);
        c = ff(c, d, a, b, k[6], 17, -1473231341); b = ff(b, c, d, a, k[7], 22, -45705983);
        a = ff(a, b, c, d, k[8], 7, 1770035416); d = ff(d, a, b, c, k[9], 12, -1958414417);
        c = ff(c, d, a, b, k[10], 17, -42063); b = ff(b, c, d, a, k[11], 22, -1990404162);
        a = ff(a, b, c, d, k[12], 7, 1804603682); d = ff(d, a, b, c, k[13], 12, -40341101);
        c = ff(c, d, a, b, k[14], 17, -1502002290); b = ff(b, c, d, a, k[15], 22, 1236535329);
        a = gg(a, b, c, d, k[1], 5, -165796510); d = gg(d, a, b, c, k[6], 9, -1069501632);
        c = gg(c, d, a, b, k[11], 14, 643717713); b = gg(b, c, d, a, k[0], 20, -373897302);
        a = gg(a, b, c, d, k[5], 5, -701558691); d = gg(d, a, b, c, k[10], 9, 38016083);
        c = gg(c, d, a, b, k[15], 14, -660478335); b = gg(b, c, d, a, k[4], 20, -405537848);
        a = gg(a, b, c, d, k[9], 5, 568446438); d = gg(d, a, b, c, k[14], 9, -1019803690);
        c = gg(c, d, a, b, k[3], 14, -187363961); b = gg(b, c, d, a, k[8], 20, 1163531501);
        a = gg(a, b, c, d, k[13], 5, -1444681467); d = gg(d, a, b, c, k[2], 9, -51403784);
        c = gg(c, d, a, b, k[7], 14, 1735328473); b = gg(b, c, d, a, k[12], 20, -1926607734);
        a = hh(a, b, c, d, k[5], 4, -378558); d = hh(d, a, b, c, k[8], 11, -2022574463);
        c = hh(c, d, a, b, k[11], 16, 1839030562); b = hh(b, c, d, a, k[14], 23, -35309556);
        a = hh(a, b, c, d, k[1], 4, -1530992060); d = hh(d, a, b, c, k[4], 11, 1272893353);
        c = hh(c, d, a, b, k[7], 16, -155497632); b = hh(b, c, d, a, k[10], 23, -1094730640);
        a = hh(a, b, c, d, k[13], 4, 681279174); d = hh(d, a, b, c, k[0], 11, -358537222);
        c = hh(c, d, a, b, k[3], 16, -722521979); b = hh(b, c, d, a, k[6], 23, 76029189);
        a = hh(a, b, c, d, k[9], 4, -640364487); d = hh(d, a, b, c, k[12], 11, -421815835);
        c = hh(c, d, a, b, k[15], 16, 530742520); b = hh(b, c, d, a, k[2], 23, -995338651);
        a = ii(a, b, c, d, k[0], 6, -198630844); d = ii(d, a, b, c, k[7], 10, 1126891415);
        c = ii(c, d, a, b, k[14], 15, -1416354905); b = ii(b, c, d, a, k[5], 21, -57434055);
        a = ii(a, b, c, d, k[12], 6, 1700485571); d = ii(d, a, b, c, k[3], 10, -1894986606);
        c = ii(c, d, a, b, k[10], 15, -1051523); b = ii(b, c, d, a, k[1], 21, -2054922799);
        a = ii(a, b, c, d, k[8], 6, 1873313359); d = ii(d, a, b, c, k[15], 10, -30611744);
        c = ii(c, d, a, b, k[6], 15, -1560198380); b = ii(b, c, d, a, k[13], 21, 1309151649);
        a = ii(a, b, c, d, k[4], 6, -145523070); d = ii(d, a, b, c, k[11], 10, -1120210379);
        c = ii(c, d, a, b, k[2], 15, 718787259); b = ii(b, c, d, a, k[9], 21, -343485551);
        x[0] = add32(a, x[0]); x[1] = add32(b, x[1]); x[2] = add32(c, x[2]); x[3] = add32(d, x[3]);
    }
    function cmn(q, a, b, x, s, t) { a = add32(add32(a, q), add32(x, t)); return add32((a << s) | (a >>> (32 - s)), b); }
    function ff(a, b, c, d, x, s, t) { return cmn((b & c) | ((~b) & d), a, b, x, s, t); }
    function gg(a, b, c, d, x, s, t) { return cmn((b & d) | (c & (~d)), a, b, x, s, t); }
    function hh(a, b, c, d, x, s, t) { return cmn(b ^ c ^ d, a, b, x, s, t); }
    function ii(a, b, c, d, x, s, t) { return cmn(c ^ (b | (~d)), a, b, x, s, t); }
    function md51(s) {
        var n = s.length, state = [1732584193, -271733879, -1732584194, 271733878], i;
        for (i = 64; i <= s.length; i += 64) md5cycle(state, md5blk(s.substring(i - 64, i)));
        s = s.substring(i - 64);
        var tail = [0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0];
        for (i = 0; i < s.length; i++) tail[i >> 2] |= s.charCodeAt(i) << ((i % 4) << 3);
        tail[i >> 2] |= 0x80 << ((i % 4) << 3);
        if (i > 55) { md5cycle(state, tail); tail = [0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0]; }
        tail[14] = n * 8; md5cycle(state, tail); return state;
    }
    function md5blk(s) {
        var md5blks = [], i;
        for (i = 0; i < 64; i += 4) md5blks[i >> 2] = s.charCodeAt(i) + (s.charCodeAt(i + 1) << 8) + (s.charCodeAt(i + 2) << 16) + (s.charCodeAt(i + 3) << 24);
        return md5blks;
    }
    var hex_chr = '0123456789abcdef'.split('');
    function rhex(n) { var s = '', j = 0; for (; j < 4; j++) s += hex_chr[(n >> (j * 8 + 4)) & 0x0F] + hex_chr[(n >> (j * 8)) & 0x0F]; return s; }
    function hex(x) { for (var i = 0; i < x.length; i++) x[i] = rhex(x[i]); return x.join(''); }
    function add32(a, b) { return (a + b) & 0xFFFFFFFF; }
    return hex(md51(string));
}

// ⭐ 타오바오 이미지 검색 함수
async function searchTaobaoByImage(imageUrl) {
    console.log('🔍 타오바오 이미지 검색 시작:', imageUrl?.substring(0, 50));
    
    // 1. 타오바오 쿠키에서 토큰 가져오기
    const cookies = await chrome.cookies.getAll({ domain: '.taobao.com' });
    let token = null;
    
    for (const cookie of cookies) {
        if (cookie.name === '_m_h5_tk' && cookie.value) {
            token = cookie.value.split('_')[0];
            console.log('🔑 토큰 발견:', token.substring(0, 10) + '...');
            break;
        }
    }
    
    if (!token) {
        console.log('❌ 타오바오 토큰 없음 - 로그인 필요');
        return { success: false, error: '타오바오 로그인이 필요합니다' };
    }
    
    // 2. API 파라미터 생성
    const timestamp = Date.now();
    const appKey = '12574478';
    const data = JSON.stringify({
        imageUrl: imageUrl,
        extendInfo: '{}',
        p: 'mm_26632258_3504122_32538762'
    });
    
    // 3. 서명 생성
    const signStr = `${token}&${timestamp}&${appKey}&${data}`;
    const sign = md5(signStr);
    
    // 4. API 호출
    const apiUrl = 'https://h5api.m.taobao.com/h5/mtop.relationrecommend.wirelessrecommend.recommend/2.0/';
    const params = new URLSearchParams({
        jsv: '2.6.1',
        appKey: appKey,
        t: timestamp,
        sign: sign,
        api: 'mtop.relationrecommend.wirelessrecommend.recommend',
        v: '2.0',
        type: 'jsonp',
        dataType: 'jsonp',
        callback: 'mtopjsonp1',
        data: data
    });
    
    try {
        const response = await fetch(`${apiUrl}?${params.toString()}`, {
            method: 'GET',
            credentials: 'include',
            headers: { 'Referer': 'https://s.taobao.com/' }
        });
        
        const text = await response.text();
        console.log('📥 API 응답:', text.substring(0, 200));
        
        // JSONP 파싱
        const jsonStr = text.replace(/^mtopjsonp\d+\(/, '').replace(/\)$/, '');
        const result = JSON.parse(jsonStr);
        
        if (result.ret && result.ret[0] === 'SUCCESS::调用成功') {
            console.log('✅ 이미지 검색 성공');
            return { success: true, data: result.data };
        } else {
            console.log('❌ API 오류:', result.ret);
            return { success: false, error: result.ret?.[0] || 'API 오류' };
        }
    } catch (e) {
        console.error('❌ 요청 실패:', e);
        return { success: false, error: e.message };
    }
}

let globalProcessingState = {
  isProcessing: false,
  currentStore: null,
  currentTabId: null,
  lockTimestamp: null,
  queue: [],
  openWindows: new Map()  // 열린 앱 창들 추적
};

// ⭐ 이미지 검색 요청 폴링 (3초마다)
setInterval(async () => {
    try {
        const response = await fetch('http://localhost:8080/api/taobao/pending-search');
        if (!response.ok) {
            console.log('❌ pending-search 응답 오류:', response.status);
            return;
        }
        
        const data = await response.json();
        console.log('📡 폴링 응답:', JSON.stringify(data));
        
        if (!data.hasPending) return;
        
        console.log('🔍 검색 요청 발견:', data.productId);
        
        // Base64 이미지를 타오바오에 업로드하고 검색
        const result = await uploadAndSearchTaobao(data.imageBase64);
        console.log('🔎 검색 결과:', JSON.stringify(result).substring(0, 200));
        
        // 결과를 서버로 전송
        await fetch('http://localhost:8080/api/taobao/image-search', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                productId: data.productId,
                success: result.success,
                products: result.products || [],
                error: result.error
            })
        });
        
        console.log('✅ 검색 결과 전송 완료');
    } catch (e) {
        console.log('❌ 폴링 오류:', e.message);
    }
}, 3000);

// ⭐ Base64 이미지를 타오바오에 업로드하고 검색
async function uploadAndSearchTaobao(base64Image) {
    try {
        // 1. 타오바오 토큰 가져오기
        const cookies = await chrome.cookies.getAll({ domain: '.taobao.com' });
        console.log('🍪 타오바오 쿠키 개수:', cookies.length);
        
        let token = null;
        let tokenEnc = null;
        for (const cookie of cookies) {
            if (cookie.name === '_m_h5_tk' && cookie.value) {
                token = cookie.value.split('_')[0];
                console.log('🔑 토큰 발견:', token.substring(0, 15) + '...');
            }
            if (cookie.name === '_m_h5_tk_enc') {
                tokenEnc = cookie.value;
            }
        }
        
        if (!token) {
            console.log('❌ _m_h5_tk 쿠키 없음');
            return { success: false, error: '타오바오 로그인 필요' };
        }
        
        // 2. 이미지를 imgbb에 임시 업로드 (무료 이미지 호스팅)
        console.log('📤 이미지 업로드 중...');
        const formData = new FormData();
        formData.append('image', base64Image);
        
        const uploadResp = await fetch('https://api.imgbb.com/1/upload?key=d36eb6591370ae79f9bb33b06007e46e', {
            method: 'POST',
            body: formData
        });
        
        const uploadText = await uploadResp.text();
        console.log('📤 업로드 응답:', uploadText.substring(0, 500));
        
        let uploadResult;
        try {
            uploadResult = JSON.parse(uploadText);
        } catch (e) {
            return { success: false, error: '업로드 응답 파싱 실패: ' + uploadText.substring(0, 100) };
        }
        
        if (!uploadResult.success) {
            console.log('❌ 업로드 실패:', uploadResult);
            return { success: false, error: '이미지 업로드 실패: ' + (uploadResult.error?.message || 'unknown') };
        }
        
        const imageUrl = uploadResult.data.url;
        console.log('✅ 이미지 업로드 완료:', imageUrl);
        
        // 3. 타오바오 이미지 검색 API 호출
        const result = await searchTaobaoByImage(imageUrl);
        return result;
        
    } catch (e) {
        console.error('❌ 업로드/검색 오류:', e);
        return { success: false, error: e.message };
    }
}

// ⭐ 타오바오 쿠키 자동 전송 함수
async function sendTaobaoCookies() {
  try {
    console.log('🍪 타오바오 쿠키 수집 시작...');
    
    // Chrome API 사용 가능 여부 확인
    if (!chrome || !chrome.cookies) {
      console.log('❌ Chrome cookies API를 사용할 수 없습니다');
      return false;
    }
    
    const cookies = await chrome.cookies.getAll({
      domain: '.taobao.com'
    });
    
    if (cookies.length === 0) {
      console.log('❌ 타오바오 쿠키가 없습니다');
      return false;
    }
    
    const cookieDict = {};
    let hasToken = false;
    
    cookies.forEach(cookie => {
      cookieDict[cookie.name] = cookie.value;
      if (cookie.name === '_m_h5_tk' && cookie.value) {
        hasToken = true;
        console.log(`🔑 _m_h5_tk 토큰 발견: ${cookie.value.substring(0, 20)}...`);
      }
    });
    
    console.log(`📊 수집된 쿠키 개수: ${cookies.length}`);
    console.log(`🔑 토큰 상태: ${hasToken ? '있음' : '없음'}`);
    
    // 서버로 쿠키 전송
    const response = await fetch('http://localhost:8080/api/taobao/cookies', {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        'Accept': 'application/json'
      },
      mode: 'cors',
      body: JSON.stringify({
        cookies: cookieDict,
        timestamp: Date.now()
      })
    });
    
    if (response.ok) {
      console.log('✅ 타오바오 쿠키 전송 완료');
      return true;
    } else {
      console.log('❌ 쿠키 전송 실패:', response.status);
      return false;
    }
  } catch (error) {
    console.error('❌ 쿠키 전송 오류:', error);
    return false;
  }
}

// ⭐ 서버 연결 확인 함수
async function checkServerConnection() {
  try {
    const response = await fetch('http://localhost:8080/api/test', {
      method: 'GET',
      mode: 'cors'
    });
    return response.ok;
  } catch (error) {
    return false;
  }
}

// ⭐ 서버 대기 후 쿠키 전송
async function waitForServerAndSendCookies() {
  console.log('🔍 서버 연결 대기 중...');
  
  for (let i = 0; i < 12; i++) { // 최대 60초 대기 (5초 × 12회)
    const isConnected = await checkServerConnection();
    if (isConnected) {
      console.log('✅ 서버 연결 확인 - 쿠키 전송 시작');
      await sendTaobaoCookies();
      return;
    }
    console.log(`⏳ 서버 대기 중... (${i + 1}/12)`);
    await new Promise(resolve => setTimeout(resolve, 5000));
  }
  
  console.log('❌ 서버 연결 타임아웃 - 쿠키 전송 생략');
}

// ⭐ 확장프로그램 시작 시 쿠키 전송
chrome.runtime.onStartup.addListener(() => {
  waitForServerAndSendCookies();
});

chrome.runtime.onInstalled.addListener(() => {
  waitForServerAndSendCookies();
});

// ⭐ 순차 처리 요청 핸들러
chrome.runtime.onMessage.addListener((request, sender, sendResponse) => {
    console.log('Background received message:', request);
    
    // ⭐ 타오바오 쿠키 수집 요청 처리
    if (request.action === 'collectTaobaoCookies') {
        console.log('🍪 Content Script에서 쿠키 수집 요청 받음');
        
        // Background Script에서 직접 쿠키 수집
        chrome.cookies.getAll({domain: '.taobao.com'}, async function(cookies) {
            if (chrome.runtime.lastError) {
                console.error('❌ 쿠키 수집 오류:', chrome.runtime.lastError);
                sendResponse({success: false, error: chrome.runtime.lastError.message});
                return;
            }
            
            console.log(`📊 수집된 쿠키 개수: ${cookies.length}`);
            
            if (cookies.length === 0) {
                console.log('❌ 타오바오 쿠키가 없습니다');
                sendResponse({success: false, error: '쿠키 없음'});
                return;
            }
            
            // 쿠키를 딕셔너리 형태로 변환
            const cookieDict = {};
            let hasToken = false;
            
            cookies.forEach(cookie => {
                cookieDict[cookie.name] = cookie.value;
                if (cookie.name === '_m_h5_tk' && cookie.value) {
                    hasToken = true;
                    console.log(`🔑 _m_h5_tk 토큰 발견: ${cookie.value.substring(0, 20)}...`);
                }
            });
            
            console.log(`🔑 토큰 상태: ${hasToken ? '있음' : '없음'}`);
            
            try {
                // 서버로 쿠키 전송
                const response = await fetch('http://localhost:8080/api/taobao/cookies', {
                    method: 'POST',
                    headers: {
                        'Content-Type': 'application/json'
                    },
                    body: JSON.stringify({
                        cookies: cookieDict,
                        hasToken: hasToken,
                        cookieCount: cookies.length,
                        timestamp: new Date().toISOString()
                    })
                });
                
                if (response.ok) {
                    console.log('✅ 타오바오 쿠키 전송 완료');
                    sendResponse({success: true, cookieCount: cookies.length, hasToken});
                } else {
                    console.log('❌ 쿠키 전송 실패:', response.status);
                    sendResponse({success: false, error: `HTTP ${response.status}`});
                }
            } catch (error) {
                console.error('❌ 서버 전송 오류:', error);
                sendResponse({success: false, error: error.message});
            }
        });
        
        return true; // 비동기 응답
    }
    
    // ⭐ 타오바오 이미지 검색 요청 처리
    if (request.action === 'searchTaobaoByImage') {
        console.log('🔍 타오바오 이미지 검색 요청:', request.imageUrl?.substring(0, 50));
        searchTaobaoByImage(request.imageUrl).then(result => {
            sendResponse(result);
        }).catch(err => {
            sendResponse({ error: err.message });
        });
        return true;
    }
    
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
      
    case 'sendTaobaoCookies':
      // ⭐ 타오바오 쿠키 전송 요청
      sendTaobaoCookies().then(success => {
        sendResponse({ success });
      });
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
    
    // ⭐ 공구탭 없어서 리다이렉트된 경우 감지 (스토어 메인으로 이동)
    if (tab.url.includes('smartstore.naver.com') && 
        !tab.url.includes('/category/') && 
        !tab.url.includes('/products/')) {
      const storeIdMatch = tab.url.match(/smartstore\.naver\.com\/([^\/\?]+)/);
      const storeId = storeIdMatch ? storeIdMatch[1] : 'unknown';
      
      console.log(`⚠️ ${storeId}: 공구탭 없음 - 리다이렉트 감지`);
      
      // 서버에 스킵 신호 전송
      fetch('http://localhost:8080/api/smartstore/skip-store', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ storeId: storeId, reason: '공구탭 없음' })
      }).then(() => {
        console.log(`✅ ${storeId}: 스킵 완료`);
        chrome.tabs.remove(tabId);
      }).catch(() => {});
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
