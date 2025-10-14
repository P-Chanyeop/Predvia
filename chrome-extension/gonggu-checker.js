// 공구탭에서 실행되는 스크립트 - 공구 개수 확인
console.log('🔥 gonggu-checker.js 파일 로드됨!');
console.log('🔥 현재 URL:', window.location.href);
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
        
        // 전체상품 판매많은순 URL 생성 (runId 포함)
        const urlParams = new URLSearchParams(window.location.search);
        const runId = urlParams.get('runId') || 'unknown';
        const allProductsUrl = `https://smartstore.naver.com/${storeId}/category/ALL?st=TOTALSALE&runId=${runId}`;
        console.log(`🔗 전체상품 URL: ${allProductsUrl}`);
        
        // 서버에 전체상품 페이지 이동 알림
        try {
          await fetch('http://localhost:8080/api/smartstore/log', {
            method: 'POST',
            headers: {
              'Content-Type': 'application/json',
              'Origin': 'chrome-extension'
            },
            body: JSON.stringify({
              message: `🛍️ ${storeId}: 전체상품 페이지로 이동 - ${allProductsUrl}`,
              timestamp: new Date().toISOString()
            })
          });
        } catch (e) {
          console.log('로그 전송 실패:', e);
        }
        
        // 페이지 이동 후 리뷰 찾기 로직 실행
        setTimeout(() => {
          console.log('🚀 전체상품 페이지로 이동 중...');
          window.location.href = allProductsUrl;
          
          // 페이지 이동 후 리뷰 찾기 실행
          setTimeout(() => {
            findLastReviewProduct(storeId);
          }, 5000);
        }, 1000);
        
      } else {
        console.log(`❌ ${storeId}: 공구 ${gongguCount}개 < 1000개 - 페이지 유지 (곧 닫힐 예정)`);
        
        // ⭐ 1000개 이하면 즉시 완료 상태로 설정
        try {
          const urlParams = new URLSearchParams(window.location.search);
          const runId = urlParams.get('runId') || 'unknown';
          
          console.log(`🔧 ${storeId}: 완료 상태 설정 시도 (runId: ${runId})`);
          
          // ⭐ 즉시 done + unlock 상태로 설정
          const response = await fetch('http://localhost:8080/api/smartstore/state', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
              storeId: storeId,
              runId: runId,
              state: 'done',
              lock: false,
              expected: 0,
              progress: 0,
              reason: 'below-threshold',
              timestamp: new Date().toISOString()
            })
          });
          
          if (response.ok) {
            console.log(`✅ ${storeId}: 완료 상태 설정 성공 (공구 ${gongguCount}개 < 1000개)`);
            
            // 서버에 로그 전송
            await fetch('http://localhost:8080/api/smartstore/log', {
              method: 'POST',
              headers: { 'Content-Type': 'application/json' },
              body: JSON.stringify({
                message: `🔧 ${storeId}: 완료 상태 설정 성공 (공구 ${gongguCount}개 < 1000개)`,
                timestamp: new Date().toISOString()
              })
            });
            
            // ⭐ 1000개 미만 스토어 탭 닫기
            setTimeout(() => {
              window.close();
            }, 2000);
            
          } else {
            console.log(`❌ ${storeId}: 완료 상태 설정 실패 - ${response.status}`);
            // ⭐ 실패 시에도 탭 닫기
            setTimeout(() => {
              window.close();
            }, 2000);
          }
        } catch (e) {
          console.log(`❌ ${storeId}: 완료 상태 설정 오류 - ${e.message}`);
          // ⭐ 오류 시에도 탭 닫기
          setTimeout(() => {
            window.close();
          }, 2000);
        }
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
// 마지막 리뷰 상품 찾기 함수
async function findLastReviewProduct(storeId) {
  try {
    const logMsg = `🔍 ${storeId}: 마지막 리뷰 상품 찾기 시작`;
    await sendLogToServer(logMsg);
    
    // 상품 리뷰만 찾는 정확한 패턴
    const allTextNodes = document.evaluate("//text()[contains(., '리뷰')]", document, null, XPathResult.ORDERED_NODE_SNAPSHOT_TYPE, null);
    
    let reviewElements = [];
    
    for (let i = 0; i < allTextNodes.snapshotLength; i++) {
      const textNode = allTextNodes.snapshotItem(i);
      const text = textNode.textContent.trim();
      
      // 상품 리뷰 패턴만 허용
      if (/^\d+\s*리뷰$/.test(text) || 
          /^리뷰\s*\d+$/.test(text) || 
          /^\d+개\s*리뷰$/.test(text)) {
        reviewElements.push(textNode);
        
        const validMsg = `✅ ${storeId}: 유효한 리뷰 - "${text}"`;
        await sendLogToServer(validMsg);
      }
    }
    
    const reviewMsg = `🔍 ${storeId}: ${reviewElements.length}개 상품 리뷰 발견`;
    await sendLogToServer(reviewMsg);
    
    if (reviewElements.length === 0) {
      const noReviewMsg = `❌ ${storeId}: 상품 리뷰 없음`;
      await sendLogToServer(noReviewMsg);
      return;
    }
    
    // 마지막 리뷰부터 역순으로 상품 ID 찾기
    for (let i = reviewElements.length - 1; i >= 0; i--) {
      const reviewElement = reviewElements[i];
      const reviewText = reviewElement.textContent.trim();
      
      const tryMsg = `🔍 ${storeId}: "${reviewText}"에서 상품 ID 찾기`;
      await sendLogToServer(tryMsg);
      
      const productUrl = findProductIdAndGenerateUrl(reviewElement, storeId);
      if (productUrl) {
        // 서버로 상품 데이터 전송
        await sendProductDataToServer(storeId, [{ url: productUrl, storeId: storeId }], 1);
        return;
      }
    }
    
    const noLinkMsg = `❌ ${storeId}: 상품 ID 없음`;
    await sendLogToServer(noLinkMsg);
    
  } catch (error) {
    const errorMsg = `❌ ${storeId}: 리뷰 검색 오류 - ${error.message}`;
    await sendLogToServer(errorMsg);
  }
}

// 상품 ID 추출 및 URL 생성
function findProductIdAndGenerateUrl(element, storeId) {
  try {
    let container = element;
    
    // 최대 10단계까지 부모 요소 탐색
    for (let level = 0; level < 10 && container; level++) {
      
      // DOM 속성들에서 상품 ID 찾기
      if (container.querySelectorAll) {
        const allElements = container.querySelectorAll('*');
        
        for (let element of allElements) {
          const allAttributes = element.attributes;
          for (let attr of allAttributes) {
            // 숫자로만 이루어진 긴 값 찾기 (상품 ID 패턴)
            if (attr.value && /^\d{8,}$/.test(attr.value)) {
              const productId = attr.value;
              const generatedUrl = `https://smartstore.naver.com/${storeId}/products/${productId}`;
              
              sendLogToServer(`🆔 ${storeId}: 상품 ID ${productId} 발견`);
              sendLogToServer(`🔗 ${storeId}: 생성된 URL - ${generatedUrl}`);
              
              return generatedUrl;
            }
          }
        }
      }
      
      container = container.parentElement;
    }
    
    return null;
    
  } catch (error) {
    console.log('상품 ID 찾기 오류:', error);
    return null;
  }
}

// 서버로 상품 데이터 전송
async function sendProductDataToServer(storeId, productData, reviewCount) {
  try {
    const data = {
      storeId: storeId,
      productCount: productData.length,
      reviewProductCount: reviewCount,
      products: productData,
      pageUrl: window.location.href,
      timestamp: new Date().toISOString()
    };
    
    const response = await fetch('http://localhost:8080/api/smartstore/product-data', {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        'Origin': 'chrome-extension'
      },
      body: JSON.stringify(data)
    });
    
    if (!response.ok) {
      console.error('❌ 서버 응답 오류:', response.status);
    }
    
  } catch (error) {
    console.error('❌ 상품 데이터 전송 실패:', error);
  }
}
