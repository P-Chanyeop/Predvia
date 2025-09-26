// 백그라운드 서비스 워커

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
      console.log('🎯 공구탭 페이지 감지 - 스크립트 강제 주입');
      
      // 강제 스크립트 주입
      chrome.scripting.executeScript({
        target: { tabId: tabId },
        files: ['gonggu-checker.js']
      }).then(() => {
        console.log('✅ gonggu-checker.js 강제 주입 완료');
      }).catch((error) => {
        console.log('❌ 스크립트 주입 실패:', error);
      });
    }
  }
});

chrome.runtime.onMessage.addListener((request, sender, sendResponse) => {
  if (request.action === 'searchNaver') {
    searchNaverShopping(request.keyword)
      .then(result => sendResponse({success: true, data: result}))
      .catch(error => sendResponse({success: false, error: error.message}));
    return true; // 비동기 응답을 위해 true 반환
  }
});

// 네이버 쇼핑 검색 함수
async function searchNaverShopping(keyword) {
  try {
    // 새 탭 생성 (작은 창으로
    /*
    const tab = await chrome.tabs.create({
      url: `https://search.shopping.naver.com/search/all?adQuery=${encodeURIComponent(keyword)}&origQuery=${encodeURIComponent(keyword)}&pagingIndex=1&pagingSize=40&productSet=overseas&query=${encodeURIComponent(keyword)}&sort=rel&timestamp=&viewType=list`,
      active: false, // 백그라운드에서 실행
      pinned: false
    });
     */
    var naverUrl = `https://search.shopping.naver.com/search/all?adQuery=${encodeURIComponent(keyword)}&origQuery=${encodeURIComponent(keyword)}&pagingIndex=1&pagingSize=40&productSet=overseas&query=${encodeURIComponent(keyword)}&sort=rel&timestamp=&viewType=list`
    const tab = await chrome.windows.create({
          url: naverUrl,
          type: 'normal',
          width: 1200,
          height: 800,
          left: 100,
          top: 100,
          focused: true
        });



    // 작은 창으로 만들기 → 큰 창으로 변경
    /*
    await chrome.windows.update(tab.windowId, {
      width: 1200,
      height: 800,
      left: 100,
      top: 100
    });
    */
     

    // 페이지 로딩 완료 대기
    await new Promise(resolve => {
      const listener = (tabId, changeInfo) => {
        if (tabId === tab.id && changeInfo.status === 'complete') {
          chrome.tabs.onUpdated.removeListener(listener);
          resolve();
        }
      };
      chrome.tabs.onUpdated.addListener(listener);
    });

    // 콘텐츠 스크립트 실행하여 데이터 추출
    const results = await chrome.scripting.executeScript({
      target: { tabId: tab.id },
      func: extractProductData
    });

    // 접속 확인을 위해 10초 후 탭 닫기 (기존 1초에서 변경)
    setTimeout(() => {
      chrome.tabs.remove(tab.id);
    }, 10000);

    return results[0].result;
  } catch (error) {
    console.error('네이버 쇼핑 검색 실패:', error);
    throw error;
  }
}

// 상품 데이터 추출 함수 (페이지에서 실행됨)
function extractProductData() {
  const products = [];
  
  // 상품 요소들 선택
  const productElements = document.querySelectorAll('.product_item, .basicList_item__2XT81');
  
  productElements.forEach((element, index) => {
    if (index >= 10) return; // 상위 10개만
    
    try {
      const title = element.querySelector('.product_title, .basicList_title__3P9Q7')?.textContent?.trim();
      const price = element.querySelector('.price_num, .price_price__1WUXk')?.textContent?.trim();
      const image = element.querySelector('.product_img img, .basicList_thumb__3yvXP img')?.src;
      const link = element.querySelector('a')?.href;
      
      if (title && price) {
        products.push({
          title,
          price,
          image,
          link
        });
      }
    } catch (e) {
      console.error('상품 데이터 추출 오류:', e);
    }
  });
  
  return {
    products,
    timestamp: new Date().toISOString(),
    keyword: new URLSearchParams(window.location.search).get('query')
  };
}
