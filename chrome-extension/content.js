// 콘텐츠 스크립트 - 네이버 쇼핑 페이지에서 실행
console.log('Predvia 확장프로그램이 네이버 쇼핑 페이지에서 실행됨');

// 페이지 로딩 완료 후 실행
if (document.readyState === 'loading') {
  document.addEventListener('DOMContentLoaded', initializeExtension);
} else {
  initializeExtension();
}

function initializeExtension() {
  // 확장프로그램이 활성화되었음을 표시
  console.log('Predvia 확장프로그램 초기화 완료');
  
  // 필요시 페이지 데이터 실시간 모니터링
  observePageChanges();
}

// 페이지 변화 감지
function observePageChanges() {
  const observer = new MutationObserver((mutations) => {
    mutations.forEach((mutation) => {
      if (mutation.type === 'childList') {
        // 새로운 상품이 로드되었을 때 처리
        const newProducts = mutation.addedNodes;
        // 필요시 추가 처리 로직
      }
    });
  });

  // 상품 목록 컨테이너 관찰
  const productContainer = document.querySelector('.list_basis, .basicList_list_basis__uNBZx');
  if (productContainer) {
    observer.observe(productContainer, {
      childList: true,
      subtree: true
    });
  }
}

// 백그라운드 스크립트와 통신
chrome.runtime.onMessage.addListener((request, sender, sendResponse) => {
  if (request.action === 'extractData') {
    const data = extractCurrentPageData();
    sendResponse({success: true, data});
  }
});

// 현재 페이지 데이터 추출
function extractCurrentPageData() {
  const products = [];
  const productElements = document.querySelectorAll('.product_item, .basicList_item__2XT81');
  
  productElements.forEach((element, index) => {
    if (index >= 20) return; // 상위 20개
    
    try {
      const title = element.querySelector('.product_title, .basicList_title__3P9Q7')?.textContent?.trim();
      const price = element.querySelector('.price_num, .price_price__1WUXk')?.textContent?.trim();
      const image = element.querySelector('.product_img img, .basicList_thumb__3yvXP img')?.src;
      const link = element.querySelector('a')?.href;
      const rating = element.querySelector('.product_grade, .basicList_star__3rVKs')?.textContent?.trim();
      
      if (title && price) {
        products.push({
          title,
          price,
          image,
          link,
          rating: rating || 'N/A'
        });
      }
    } catch (e) {
      console.error('상품 데이터 추출 오류:', e);
    }
  });
  
  return {
    products,
    totalCount: products.length,
    pageUrl: window.location.href,
    timestamp: new Date().toISOString()
  };
}
