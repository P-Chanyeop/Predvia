// 공구탭에서 실행되는 스크립트 - 공구 개수 확인
console.log('🔍 공구 개수 확인 스크립트 실행');

// 페이지 로딩 완료 후 실행
setTimeout(() => {
  checkGongguCount();
}, 2000);

// 추가로 5초 후에도 한번 더 시도
setTimeout(() => {
  checkGongguCount();
}, 5000);

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
    }
    
    // 결과를 서버로 전송
    sendGongguResult(gongguCount);
    
  } catch (error) {
    console.error('공구 개수 확인 오류:', error);
    sendGongguResult(0);
  }
}

// 서버로 공구 개수 결과 전송
async function sendGongguResult(gongguCount) {
  try {
    // URL에서 스토어 ID 추출
    const storeId = extractStoreIdFromUrl(window.location.href);
    
    const data = {
      storeId: storeId,
      gongguCount: gongguCount,
      isValid: gongguCount >= 1000,
      timestamp: new Date().toISOString(),
      pageUrl: window.location.href
    };
    
    console.log('📡 서버로 공구 개수 결과 전송:', data);
    
    const response = await fetch('http://localhost:8080/api/smartstore/gonggu-check', {
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
        
        // 전체상품 판매많은순 URL 생성
        const allProductsUrl = `https://smartstore.naver.com/${storeId}/category/ALL?st=TOTALSALE`;
        console.log(`🔗 전체상품 URL: ${allProductsUrl}`);
        
        // 즉시 페이지 이동 (setTimeout 제거)
        console.log('🚀 전체상품 페이지로 이동 중...');
        window.location.replace(allProductsUrl);
        
      } else {
        console.log(`❌ ${storeId}: 공구 ${gongguCount}개 < 1000개 - 페이지 유지 (곧 닫힐 예정)`);
      }
      
    } else {
      console.error('❌ 서버 응답 오류:', response.status);
    }
    
  } catch (error) {
    console.error('❌ 공구 개수 결과 전송 실패:', error);
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
