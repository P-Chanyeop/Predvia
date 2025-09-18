// 네이버 쇼핑 상품 썸네일 이미지 추출 스크립트
// 개발자 도구 콘솔에서 실행

function extractThumbnails() {
  console.log('🖼️ 썸네일 이미지 추출 시작...');
  
  const thumbnails = [];
  
  // 다양한 네이버 쇼핑 썸네일 선택자들
  const selectors = [
    '.product_img img',
    '.basicList_thumb__3yvXP img', 
    '.product_mall_img img',
    '.list_img img',
    '.adProduct_img img',
    '.thumbnail_thumb img',
    'img[data-shp-contents-id]',
    'img[src*="shopping-phinf"]'
  ];
  
  // 각 선택자로 이미지 찾기
  selectors.forEach(selector => {
    const images = document.querySelectorAll(selector);
    console.log(`${selector}: ${images.length}개 발견`);
    
    images.forEach((img, index) => {
      if (img.src && img.src.includes('http')) {
        // 상품 정보 추출
        const productElement = img.closest('[data-shp-contents-id]') || 
                              img.closest('.product_item') ||
                              img.closest('.basicList_item__2XT81') ||
                              img.closest('.adProduct_item');
        
        let productTitle = 'Unknown';
        let productPrice = 'Unknown';
        
        if (productElement) {
          // 제목 추출
          const titleElement = productElement.querySelector('.product_title') ||
                              productElement.querySelector('.basicList_title__3P9Q7') ||
                              productElement.querySelector('.adProduct_title');
          if (titleElement) {
            productTitle = titleElement.textContent.trim();
          }
          
          // 가격 추출  
          const priceElement = productElement.querySelector('.price_num') ||
                              productElement.querySelector('.price_price__1WUXk') ||
                              productElement.querySelector('.adProduct_price');
          if (priceElement) {
            productPrice = priceElement.textContent.trim();
          }
        }
        
        thumbnails.push({
          index: thumbnails.length + 1,
          src: img.src,
          alt: img.alt || '',
          title: productTitle,
          price: productPrice,
          selector: selector,
          width: img.naturalWidth || img.width,
          height: img.naturalHeight || img.height
        });
      }
    });
  });
  
  // 중복 제거 (같은 src)
  const uniqueThumbnails = thumbnails.filter((item, index, self) => 
    index === self.findIndex(t => t.src === item.src)
  );
  
  console.log(`📊 총 ${uniqueThumbnails.length}개의 고유 썸네일 발견`);
  
  // 결과 출력
  uniqueThumbnails.forEach(thumb => {
    console.log(`${thumb.index}. ${thumb.title}`);
    console.log(`   💰 ${thumb.price}`);
    console.log(`   🖼️ ${thumb.src}`);
    console.log(`   📏 ${thumb.width}x${thumb.height}`);
    console.log('---');
  });
  
  // 이미지 다운로드 함수
  window.downloadThumbnails = function() {
    uniqueThumbnails.forEach((thumb, index) => {
      const link = document.createElement('a');
      link.href = thumb.src;
      link.download = `thumbnail_${index + 1}_${thumb.title.substring(0, 20).replace(/[^a-zA-Z0-9가-힣]/g, '_')}.jpg`;
      document.body.appendChild(link);
      link.click();
      document.body.removeChild(link);
    });
    console.log('📥 모든 썸네일 다운로드 시작됨');
  };
  
  console.log('✅ 추출 완료! downloadThumbnails() 함수로 다운로드 가능');
  return uniqueThumbnails;
}

// 실행
const results = extractThumbnails();
