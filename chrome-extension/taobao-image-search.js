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

// 타오바오 이미지 검색 전용 Content Script
console.log('🔍 [taobao-image-search.js] 스크립트 로드됨, URL:', window.location.href);

(async function() {
    const urlParams = new URLSearchParams(window.location.search);
    const searchRequestId = urlParams.get('predvia_search_id');
    
    console.log('🔍 [taobao-image-search.js] searchRequestId:', searchRequestId);
    
    if (!searchRequestId) {
        console.log('🔍 [taobao-image-search.js] 검색 요청 ID 없음, 종료');
        return;
    }
    
    console.log('🎯 검색 요청 ID:', searchRequestId);
    
    // 페이지 로드 대기
    await new Promise(r => setTimeout(r, 2000));
    
    try {
        // 서버에서 이미지 데이터 가져오기
        const response = await localFetch(`http://localhost:8080/api/taobao/get-search-image?id=${searchRequestId}`);
        if (!response.ok) {
            console.log('❌ 이미지 데이터 가져오기 실패:', response.status);
            await sendResult(searchRequestId, { success: false, error: '이미지 데이터 없음' });
            return;
        }
        
        const data = await response.json();
        if (!data.imageBase64) {
            console.log('❌ 이미지 데이터 없음');
            await sendResult(searchRequestId, { success: false, error: '이미지 데이터 없음' });
            return;
        }
        
        console.log('✅ 이미지 데이터 수신, 길이:', data.imageBase64.length);
        
        // Base64를 Blob으로 변환
        const byteString = atob(data.imageBase64);
        const ab = new ArrayBuffer(byteString.length);
        const ia = new Uint8Array(ab);
        for (let i = 0; i < byteString.length; i++) {
            ia[i] = byteString.charCodeAt(i);
        }
        const blob = new Blob([ab], { type: 'image/jpeg' });
        
        // FormData 생성
        const formData = new FormData();
        formData.append('imgfile', blob, 'search.jpg');
        formData.append('_input_charset', 'utf-8');
        
        console.log('📤 이미지 업로드 시작...');
        
        // 이미지 업로드
        const uploadResp = await fetch('https://s.taobao.com/api?_ksTS=' + Date.now() + '&callback=jsonp&m=upload', {
            method: 'POST',
            body: formData,
            credentials: 'include'
        });
        
        const uploadText = await uploadResp.text();
        console.log('📤 업로드 응답:', uploadText.substring(0, 500));
        
        // JSONP 파싱
        let uploadResult;
        try {
            const jsonStr = uploadText.replace(/^jsonp\d*\(/, '').replace(/\);?$/, '');
            uploadResult = JSON.parse(jsonStr);
        } catch (e) {
            console.log('❌ 업로드 응답 파싱 실패:', e.message);
            await sendResult(searchRequestId, { success: false, error: '업로드 응답 파싱 실패: ' + uploadText.substring(0, 100) });
            return;
        }
        
        if (uploadResult.rgv587_flag === 'sm') {
            console.log('⚠️ CAPTCHA 필요');
            await sendResult(searchRequestId, { success: false, error: 'CAPTCHA_REQUIRED', needLogin: true });
            return;
        }
        
        if (!uploadResult.url) {
            console.log('❌ 이미지 URL 없음:', JSON.stringify(uploadResult));
            await sendResult(searchRequestId, { success: false, error: '이미지 URL 없음' });
            return;
        }
        
        console.log('✅ 이미지 업로드 성공, URL:', uploadResult.url);
        
        // 쿠키에서 토큰 추출
        const tokenMatch = document.cookie.match(/_m_h5_tk=([^;]+)/);
        const fullToken = tokenMatch ? tokenMatch[1] : '';
        const token = fullToken.split('_')[0];
        
        if (!token) {
            console.log('⚠️ 토큰 없음');
            await sendResult(searchRequestId, { success: false, error: 'NO_TOKEN', needLogin: true });
            return;
        }
        
        console.log('🔑 토큰:', token.substring(0, 10) + '...');
        
        // 검색 API 호출
        const appKey = '12574478';
        const timestamp = Date.now();
        const apiData = JSON.stringify({ imageUrl: uploadResult.url, extendInfo: '{}' });
        
        // MD5 서명 생성
        const signStr = `${token}&${timestamp}&${appKey}&${apiData}`;
        const sign = md5(signStr);
        
        console.log('🔍 검색 API 호출...');
        
        const searchUrl = `https://h5api.m.taobao.com/h5/mtop.relationrecommend.wirelessrecommend.recommend/2.0/?jsv=2.6.1&appKey=${appKey}&t=${timestamp}&sign=${sign}&api=mtop.relationrecommend.wirelessrecommend.recommend&v=2.0&type=jsonp&dataType=jsonp&callback=mtopjsonp1&data=${encodeURIComponent(apiData)}`;
        
        const searchResp = await fetch(searchUrl, { credentials: 'include' });
        const searchText = await searchResp.text();
        
        console.log('🔎 검색 응답:', searchText.substring(0, 500));
        
        // JSONP 파싱
        let searchResult;
        try {
            const searchJson = searchText.replace(/^mtopjsonp\d*\(/, '').replace(/\);?$/, '');
            searchResult = JSON.parse(searchJson);
        } catch (e) {
            console.log('❌ 검색 응답 파싱 실패:', e.message);
            await sendResult(searchRequestId, { success: false, error: '검색 응답 파싱 실패' });
            return;
        }
        
        if (searchResult.data && searchResult.data.resultList) {
            const products = searchResult.data.resultList.slice(0, 10).map(item => ({
                nid: item.nid || item.itemId || '',
                title: item.title || '',
                price: item.price || '',
                imageUrl: item.pic || item.picUrl || '',
                sales: item.sales || '',
                shopName: item.nick || ''
            }));
            
            console.log('✅ 상품 발견:', products.length);
            await sendResult(searchRequestId, { success: true, products: products });
        } else {
            console.log('❌ 검색 결과 없음:', JSON.stringify(searchResult).substring(0, 200));
            await sendResult(searchRequestId, { success: false, error: '검색 결과 없음' });
        }
        
    } catch (e) {
        console.error('❌ 오류:', e);
        await sendResult(searchRequestId, { success: false, error: e.message });
    }
    
    // 결과 전송 후 탭 닫기
    setTimeout(() => window.close(), 2000);
})();

// 결과 전송 함수
async function sendResult(searchId, result) {
    try {
        await localFetch('http://localhost:8080/api/taobao/image-search-result', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ searchId, ...result })
        });
        console.log('📤 결과 전송 완료');
    } catch (e) {
        console.error('❌ 결과 전송 실패:', e);
    }
}

// MD5 해시 함수 (동기)
function md5(string) {
    function rotateLeft(v, s) { return (v << s) | (v >>> (32 - s)); }
    function addUnsigned(x, y) {
        const x8 = x & 0x80000000, y8 = y & 0x80000000;
        const x4 = x & 0x40000000, y4 = y & 0x40000000;
        const result = (x & 0x3FFFFFFF) + (y & 0x3FFFFFFF);
        if (x4 & y4) return result ^ 0x80000000 ^ x8 ^ y8;
        if (x4 | y4) return result & 0x40000000 ? result ^ 0xC0000000 ^ x8 ^ y8 : result ^ 0x40000000 ^ x8 ^ y8;
        return result ^ x8 ^ y8;
    }
    function F(x, y, z) { return (x & y) | (~x & z); }
    function G(x, y, z) { return (x & z) | (y & ~z); }
    function H(x, y, z) { return x ^ y ^ z; }
    function I(x, y, z) { return y ^ (x | ~z); }
    function FF(a, b, c, d, x, s, ac) { a = addUnsigned(a, addUnsigned(addUnsigned(F(b, c, d), x), ac)); return addUnsigned(rotateLeft(a, s), b); }
    function GG(a, b, c, d, x, s, ac) { a = addUnsigned(a, addUnsigned(addUnsigned(G(b, c, d), x), ac)); return addUnsigned(rotateLeft(a, s), b); }
    function HH(a, b, c, d, x, s, ac) { a = addUnsigned(a, addUnsigned(addUnsigned(H(b, c, d), x), ac)); return addUnsigned(rotateLeft(a, s), b); }
    function II(a, b, c, d, x, s, ac) { a = addUnsigned(a, addUnsigned(addUnsigned(I(b, c, d), x), ac)); return addUnsigned(rotateLeft(a, s), b); }
    function convertToWordArray(str) {
        let lWordCount, lMessageLength = str.length;
        let lNumberOfWords_temp1 = lMessageLength + 8;
        let lNumberOfWords_temp2 = (lNumberOfWords_temp1 - (lNumberOfWords_temp1 % 64)) / 64;
        let lNumberOfWords = (lNumberOfWords_temp2 + 1) * 16;
        let lWordArray = Array(lNumberOfWords - 1);
        let lBytePosition = 0, lByteCount = 0;
        while (lByteCount < lMessageLength) {
            lWordCount = (lByteCount - (lByteCount % 4)) / 4;
            lBytePosition = (lByteCount % 4) * 8;
            lWordArray[lWordCount] = (lWordArray[lWordCount] | (str.charCodeAt(lByteCount) << lBytePosition));
            lByteCount++;
        }
        lWordCount = (lByteCount - (lByteCount % 4)) / 4;
        lBytePosition = (lByteCount % 4) * 8;
        lWordArray[lWordCount] = lWordArray[lWordCount] | (0x80 << lBytePosition);
        lWordArray[lNumberOfWords - 2] = lMessageLength << 3;
        lWordArray[lNumberOfWords - 1] = lMessageLength >>> 29;
        return lWordArray;
    }
    function wordToHex(lValue) {
        let WordToHexValue = "", WordToHexValue_temp = "", lByte, lCount;
        for (lCount = 0; lCount <= 3; lCount++) {
            lByte = (lValue >>> (lCount * 8)) & 255;
            WordToHexValue_temp = "0" + lByte.toString(16);
            WordToHexValue = WordToHexValue + WordToHexValue_temp.substr(WordToHexValue_temp.length - 2, 2);
        }
        return WordToHexValue;
    }
    let x = convertToWordArray(string);
    let a = 0x67452301, b = 0xEFCDAB89, c = 0x98BADCFE, d = 0x10325476;
    for (let k = 0; k < x.length; k += 16) {
        let AA = a, BB = b, CC = c, DD = d;
        a = FF(a, b, c, d, x[k], 7, 0xD76AA478); d = FF(d, a, b, c, x[k + 1], 12, 0xE8C7B756);
        c = FF(c, d, a, b, x[k + 2], 17, 0x242070DB); b = FF(b, c, d, a, x[k + 3], 22, 0xC1BDCEEE);
        a = FF(a, b, c, d, x[k + 4], 7, 0xF57C0FAF); d = FF(d, a, b, c, x[k + 5], 12, 0x4787C62A);
        c = FF(c, d, a, b, x[k + 6], 17, 0xA8304613); b = FF(b, c, d, a, x[k + 7], 22, 0xFD469501);
        a = FF(a, b, c, d, x[k + 8], 7, 0x698098D8); d = FF(d, a, b, c, x[k + 9], 12, 0x8B44F7AF);
        c = FF(c, d, a, b, x[k + 10], 17, 0xFFFF5BB1); b = FF(b, c, d, a, x[k + 11], 22, 0x895CD7BE);
        a = FF(a, b, c, d, x[k + 12], 7, 0x6B901122); d = FF(d, a, b, c, x[k + 13], 12, 0xFD987193);
        c = FF(c, d, a, b, x[k + 14], 17, 0xA679438E); b = FF(b, c, d, a, x[k + 15], 22, 0x49B40821);
        a = GG(a, b, c, d, x[k + 1], 5, 0xF61E2562); d = GG(d, a, b, c, x[k + 6], 9, 0xC040B340);
        c = GG(c, d, a, b, x[k + 11], 14, 0x265E5A51); b = GG(b, c, d, a, x[k], 20, 0xE9B6C7AA);
        a = GG(a, b, c, d, x[k + 5], 5, 0xD62F105D); d = GG(d, a, b, c, x[k + 10], 9, 0x2441453);
        c = GG(c, d, a, b, x[k + 15], 14, 0xD8A1E681); b = GG(b, c, d, a, x[k + 4], 20, 0xE7D3FBC8);
        a = GG(a, b, c, d, x[k + 9], 5, 0x21E1CDE6); d = GG(d, a, b, c, x[k + 14], 9, 0xC33707D6);
        c = GG(c, d, a, b, x[k + 3], 14, 0xF4D50D87); b = GG(b, c, d, a, x[k + 8], 20, 0x455A14ED);
        a = GG(a, b, c, d, x[k + 13], 5, 0xA9E3E905); d = GG(d, a, b, c, x[k + 2], 9, 0xFCEFA3F8);
        c = GG(c, d, a, b, x[k + 7], 14, 0x676F02D9); b = GG(b, c, d, a, x[k + 12], 20, 0x8D2A4C8A);
        a = HH(a, b, c, d, x[k + 5], 4, 0xFFFA3942); d = HH(d, a, b, c, x[k + 8], 11, 0x8771F681);
        c = HH(c, d, a, b, x[k + 11], 16, 0x6D9D6122); b = HH(b, c, d, a, x[k + 14], 23, 0xFDE5380C);
        a = HH(a, b, c, d, x[k + 1], 4, 0xA4BEEA44); d = HH(d, a, b, c, x[k + 4], 11, 0x4BDECFA9);
        c = HH(c, d, a, b, x[k + 7], 16, 0xF6BB4B60); b = HH(b, c, d, a, x[k + 10], 23, 0xBEBFBC70);
        a = HH(a, b, c, d, x[k + 13], 4, 0x289B7EC6); d = HH(d, a, b, c, x[k], 11, 0xEAA127FA);
        c = HH(c, d, a, b, x[k + 3], 16, 0xD4EF3085); b = HH(b, c, d, a, x[k + 6], 23, 0x4881D05);
        a = HH(a, b, c, d, x[k + 9], 4, 0xD9D4D039); d = HH(d, a, b, c, x[k + 12], 11, 0xE6DB99E5);
        c = HH(c, d, a, b, x[k + 15], 16, 0x1FA27CF8); b = HH(b, c, d, a, x[k + 2], 23, 0xC4AC5665);
        a = II(a, b, c, d, x[k], 6, 0xF4292244); d = II(d, a, b, c, x[k + 7], 10, 0x432AFF97);
        c = II(c, d, a, b, x[k + 14], 15, 0xAB9423A7); b = II(b, c, d, a, x[k + 5], 21, 0xFC93A039);
        a = II(a, b, c, d, x[k + 12], 6, 0x655B59C3); d = II(d, a, b, c, x[k + 3], 10, 0x8F0CCC92);
        c = II(c, d, a, b, x[k + 10], 15, 0xFFEFF47D); b = II(b, c, d, a, x[k + 1], 21, 0x85845DD1);
        a = II(a, b, c, d, x[k + 8], 6, 0x6FA87E4F); d = II(d, a, b, c, x[k + 15], 10, 0xFE2CE6E0);
        c = II(c, d, a, b, x[k + 6], 15, 0xA3014314); b = II(b, c, d, a, x[k + 13], 21, 0x4E0811A1);
        a = II(a, b, c, d, x[k + 4], 6, 0xF7537E82); d = II(d, a, b, c, x[k + 11], 10, 0xBD3AF235);
        c = II(c, d, a, b, x[k + 2], 15, 0x2AD7D2BB); b = II(b, c, d, a, x[k + 9], 21, 0xEB86D391);
        a = addUnsigned(a, AA); b = addUnsigned(b, BB); c = addUnsigned(c, CC); d = addUnsigned(d, DD);
    }
    return (wordToHex(a) + wordToHex(b) + wordToHex(c) + wordToHex(d)).toLowerCase();
}
