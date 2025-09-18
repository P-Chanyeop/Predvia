// 콘텐츠 스크립트 - 네이버 쇼핑 페이지에서 실행
console.log('🆕 Predvia 새 확장프로그램이 네이버 쇼핑 페이지에서 실행됨');

// 기존 로그 함수들 무력화 (페이지는 건드리지 않음)
const originalLog = console.log;
console.log = function(...args) {
  const message = args.join(' ');
  if (message.includes('=== 네이버 자동 수집') || 
      message.includes('다중 체크') || 
      message.includes('로그인 페이지 체크')) {
    return; // 기존 로그 무시
  }
  originalLog.apply(console, args);
};

// 페이지 로딩 완료 후 실행
if (document.readyState === 'loading') {
  document.addEventListener('DOMContentLoaded', initializeExtension);
} else {
  initializeExtension();
}

function initializeExtension() {
  console.log('🆕 Predvia 새 확장프로그램 초기화 완료');
  
  // 자동으로 썸네일 추출 및 전송
  setTimeout(() => {
    console.log('🚀 자동 썸네일 추출 시작...');
    sendThumbnailsToPredvia();
  }, 2000); // 2초 후 자동 실행
}

// 현재 페이지 데이터 추출
function extractCurrentPageData() {
  console.log('🔍 네이버 쇼핑 데이터 추출 시작');
  
  const products = [];
  const thumbnails = [];
  
  // 상품 요소들 찾기 (더 정확한 선택자)
  const productElements = document.querySelectorAll([
    '.basicList_item__2XT81',
    '.product_item', 
    '.adProduct_item',
    '[data-shp-contents-id]',
    '.list_item'
  ].join(','));
  
  console.log(`📦 총 ${productElements.length}개 상품 요소 발견`);
  
  productElements.forEach((element, index) => {
    try {
      // 썸네일 이미지 추출
      const imgElement = element.querySelector('img');
      let thumbnailUrl = '';
      if (imgElement && imgElement.src && imgElement.src.startsWith('http')) {
        thumbnailUrl = imgElement.src;
        console.log(`🖼️ ${index + 1}번째 썸네일: ${thumbnailUrl.substring(0, 50)}...`);
        
        thumbnails.push({
          index: index + 1,
          src: thumbnailUrl,
          alt: imgElement.alt || '',
          width: imgElement.naturalWidth || imgElement.width,
          height: imgElement.naturalHeight || imgElement.height
        });
      }
      
      // 상품 제목 추출
      let title = '';
      const titleSelectors = [
        '.basicList_title__3P9Q7 a',
        '.product_title a',
        '.adProduct_title a',
        'a[data-shp-contents-id]',
        '.list_title a'
      ];
      
      for (const selector of titleSelectors) {
        const titleElement = element.querySelector(selector);
        if (titleElement) {
          title = titleElement.textContent.trim();
          break;
        }
      }
      
      // 가격 추출
      let price = '';
      const priceSelectors = [
        '.price_price__1WUXk .price_num',
        '.price_num',
        '.adProduct_price',
        '.list_price'
      ];
      
      for (const selector of priceSelectors) {
        const priceElement = element.querySelector(selector);
        if (priceElement) {
          price = priceElement.textContent.trim();
          break;
        }
      }
      
      // 링크 추출
      const linkElement = element.querySelector('a');
      const link = linkElement ? linkElement.href : '';
      
      if (title && thumbnailUrl) {
        products.push({
          index: index + 1,
          title,
          price: price || 'N/A',
          thumbnail: thumbnailUrl,
          link,
          extractedAt: new Date().toISOString()
        });
        
        console.log(`✅ ${index + 1}번째 상품: ${title.substring(0, 30)}...`);
      }
    } catch (error) {
      console.error(`❌ 상품 ${index + 1} 추출 오류:`, error);
    }
  });
  
  console.log(`🎯 최종 결과: ${products.length}개 상품, ${thumbnails.length}개 썸네일 추출 완료`);
  
  return {
    products,
    thumbnails,
    totalCount: products.length,
    pageUrl: window.location.href,
    keyword: new URLSearchParams(window.location.search).get('query'),
    timestamp: new Date().toISOString()
  };
}

// Predvia 프로그램으로 썸네일 데이터 전송
window.sendThumbnailsToPredvia = async function() {
  console.log('🚀 sendThumbnailsToPredvia 함수 시작');
  
  const data = extractCurrentPageData();
  console.log('📊 추출된 데이터:', data);
  
  if (data.products.length === 0) {
    console.log('❌ 추출된 상품이 없습니다.');
    console.log('🔍 현재 페이지:', window.location.href);
    return;
  }
  
  console.log(`✅ ${data.products.length}개 상품 추출 완료`);
  
  try {
    console.log('📡 Predvia로 데이터 전송 시작...');
    
    const response = await fetch('http://localhost:8080/api/thumbnails/save', {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        'Origin': 'chrome-extension'
      },
      body: JSON.stringify({
        products: data.products.map((product, index) => ({
          id: `naver_${Date.now()}_${index}`,
          title: product.title,
          thumbnailUrl: product.thumbnail,
          price: product.price,
          link: product.link
        })),
        source: 'naver-shopping',
        timestamp: new Date().toISOString()
      })
    });
    
    console.log('📡 응답 상태:', response.status);
    
    if (response.ok) {
      const result = await response.json();
      console.log('✅ Predvia로 썸네일 데이터 전송 완료');
      console.log(`📊 저장된 썸네일: ${result.savedCount}개`);
    } else {
      console.error('❌ Predvia 전송 실패:', response.status);
      const errorText = await response.text();
      console.error('❌ 오류 내용:', errorText);
    }
  } catch (error) {
    console.error('❌ Predvia 통신 오류:', error);
    console.error('❌ 상세 오류:', error.message);
    console.log('💡 Predvia 프로그램이 실행 중인지 확인해주세요.');
  }
};

// 전역 함수로 노출
window.extractThumbnails = function() {
  const data = extractCurrentPageData();
  console.log('🖼️ 추출된 썸네일:', data.thumbnails);
  return data.thumbnails;
};
