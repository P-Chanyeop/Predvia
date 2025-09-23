// 전체상품 판매많은순 페이지에서 실행되는 스크립트
console.log('🛍️ 전체상품 페이지 핸들러 실행');

// 페이지 로딩 완료 후 실행
setTimeout(() => {
  handleAllProductsPage();
}, 3000);

function handleAllProductsPage() {
  try {
    const storeId = extractStoreIdFromUrl(window.location.href);
    console.log(`🛍️ ${storeId} 전체상품 페이지 로딩 완료`);
    
    // 서버에 전체상품 페이지 접속 알림
    notifyAllProductsPageLoaded(storeId);
    
    // 여기서 추가 작업 수행 예정
    console.log(`✅ ${storeId} 전체상품 페이지 처리 준비 완료`);
    
  } catch (error) {
    console.error('전체상품 페이지 처리 오류:', error);
  }
}

// 서버에 전체상품 페이지 접속 알림
async function notifyAllProductsPageLoaded(storeId) {
  try {
    const data = {
      storeId: storeId,
      pageType: 'all-products',
      pageUrl: window.location.href,
      timestamp: new Date().toISOString()
    };
    
    console.log('📡 서버에 전체상품 페이지 접속 알림:', data);
    
    const response = await fetch('http://localhost:8080/api/smartstore/all-products', {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        'Origin': 'chrome-extension'
      },
      body: JSON.stringify(data)
    });
    
    if (response.ok) {
      console.log('✅ 전체상품 페이지 접속 알림 완료');
    } else {
      console.error('❌ 서버 응답 오류:', response.status);
    }
    
  } catch (error) {
    console.error('❌ 전체상품 페이지 알림 실패:', error);
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
