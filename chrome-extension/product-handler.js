// 개별 상품 페이지 전용 핸들러
console.log('🔥🔥🔥 product-handler.js 로드됨 - ', window.location.href);

// ⭐ 페이지 로드 후 창 크기 및 위치 강제 조절 (우하단 최소 크기)
function forceWindowResize() {
  try {
    window.resizeTo(200, 300);
    const screenWidth = window.screen.availWidth;
    const screenHeight = window.screen.availHeight;
    const windowWidth = 200;
    const windowHeight = 300;
    
    // 우하단 위치 계산 (여백 20px)
    const x = screenWidth - windowWidth - 20;
    const y = screenHeight - windowHeight - 20;
    
    window.moveTo(x, y);
    console.log(`🔧 상품페이지 창 크기 조절: ${windowWidth}x${windowHeight} at (${x}, ${y})`);
  } catch (error) {
    console.log('⚠️ 창 크기 조절 실패:', error.message);
  }
}

// 즉시 실행 및 1초 후 재실행
setTimeout(forceWindowResize, 100);
setTimeout(forceWindowResize, 1000);

// 페이지 로드 완료 대기
if (document.readyState === 'loading') {
  document.addEventListener('DOMContentLoaded', initProductHandler);
} else {
  initProductHandler();
}

async function initProductHandler() {
  try {
    const url = window.location.href;
    console.log('🔥 상품 페이지 핸들러 시작:', url);
    
    // URL에서 스토어ID와 상품ID 추출
    const storeMatch = url.match(/smartstore\.naver\.com\/([^\/]+)/);
    const productMatch = url.match(/products\/(\d+)/);
    
    if (!storeMatch || !productMatch) {
      console.log('❌ 스토어ID 또는 상품ID 추출 실패');
      return;
    }
    
    const storeId = storeMatch[1];
    const productId = productMatch[1];
    
    console.log(`🎯 상품 데이터 수집 시작: ${storeId}/${productId}`);
    
    // 2초 대기 후 데이터 수집
    setTimeout(async () => {
      await collectProductPageData(storeId, productId);
    }, 2000);
    
  } catch (error) {
    console.error('❌ 상품 핸들러 오류:', error);
  }
}

// 상품 페이지에서 데이터 수집
async function collectProductPageData(storeId, productId) {
  try {
    console.log(`🔍 ${storeId}/${productId}: 데이터 수집 시작`);
    
    // 1. 상품 이미지 추출
    const imageData = await extractProductImage(storeId, productId);
    
    // 2. 상품명 추출  
    const nameData = await extractProductName(storeId, productId);
    
    // 3. 리뷰 데이터 추출
    const reviewData = await extractProductReviews(storeId, productId);
    
    console.log(`✅ ${storeId}/${productId}: 데이터 수집 완료`);
    
    // 2초 후 탭 닫기
    setTimeout(() => {
      window.close();
    }, 2000);
    
  } catch (error) {
    console.error(`❌ ${storeId}/${productId}: 데이터 수집 실패:`, error);
    // 오류 시에도 탭 닫기
    setTimeout(() => {
      window.close();
    }, 1000);
  }
}

// 상품 이미지 추출
async function extractProductImage(storeId, productId) {
  try {
    // 대표 이미지 선택자들
    const selectors = [
      '.bd_2DO68 img[alt="대표이미지"]',
      '.bd_2DO68 img',
      '.product_thumb img',
      '.thumb_area img',
      '.product_image img'
    ];
    
    let imageElement = null;
    for (const selector of selectors) {
      imageElement = document.querySelector(selector);
      if (imageElement && imageElement.src) break;
    }
    
    if (!imageElement || !imageElement.src) {
      console.log(`❌ ${storeId}/${productId}: 상품 이미지 없음`);
      return null;
    }
    
    const imageUrl = imageElement.src;
    console.log(`🖼️ ${storeId}/${productId}: 이미지 발견 - ${imageUrl}`);
    
    // 서버로 이미지 데이터 전송
    const imageData = {
      storeId: storeId,
      productId: productId,
      imageUrl: imageUrl,
      timestamp: new Date().toISOString()
    };
    
    await fetch('http://localhost:8080/api/smartstore/image', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(imageData)
    });
    
    console.log(`✅ ${storeId}/${productId}: 이미지 전송 완료`);
    return imageData;
    
  } catch (error) {
    console.error(`❌ ${storeId}/${productId}: 이미지 추출 실패:`, error);
    return null;
  }
}

// 상품명 추출
async function extractProductName(storeId, productId) {
  try {
    // 상품명 선택자들
    const selectors = [
      '.DCVBehA8ZB',
      'h3._copyable',
      '.product_title',
      '.prod_name',
      'h1'
    ];
    
    let nameElement = null;
    for (const selector of selectors) {
      nameElement = document.querySelector(selector);
      if (nameElement && nameElement.textContent.trim()) break;
    }
    
    if (!nameElement) {
      console.log(`❌ ${storeId}/${productId}: 상품명 없음`);
      return null;
    }
    
    const productName = nameElement.textContent.trim();
    console.log(`📝 ${storeId}/${productId}: 상품명 발견 - ${productName}`);
    
    // 서버로 상품명 데이터 전송
    const nameData = {
      storeId: storeId,
      productId: productId,
      productName: productName,
      timestamp: new Date().toISOString()
    };
    
    await fetch('http://localhost:8080/api/smartstore/product-name', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(nameData)
    });
    
    console.log(`✅ ${storeId}/${productId}: 상품명 전송 완료`);
    return nameData;
    
  } catch (error) {
    console.error(`❌ ${storeId}/${productId}: 상품명 추출 실패:`, error);
    return null;
  }
}

// 리뷰 데이터 추출
async function extractProductReviews(storeId, productId) {
  try {
    // 리뷰 영역 대기
    await new Promise(resolve => setTimeout(resolve, 3000));
    
    // 별점 선택자들
    const ratingSelectors = [
      'em.n6zq2yy0KA',
      '.rating_star em',
      '.review_rating em'
    ];
    
    // 리뷰 내용 선택자들  
    const contentSelectors = [
      '.vhlVUsCtw3 .K0kwJOXP06',
      '.review_content',
      '.review_text'
    ];
    
    const reviews = [];
    
    // 별점 추출
    let ratingElements = [];
    for (const selector of ratingSelectors) {
      ratingElements = document.querySelectorAll(selector);
      if (ratingElements.length > 0) break;
    }
    
    // 리뷰 내용 추출
    let contentElements = [];
    for (const selector of contentSelectors) {
      contentElements = document.querySelectorAll(selector);
      if (contentElements.length > 0) break;
    }
    
    console.log(`🔍 ${storeId}/${productId}: 별점 ${ratingElements.length}개, 내용 ${contentElements.length}개 발견`);
    
    // 리뷰 데이터 조합
    const maxReviews = Math.min(ratingElements.length, contentElements.length, 10);
    for (let i = 0; i < maxReviews; i++) {
      const rating = ratingElements[i]?.textContent?.trim() || '5';
      const content = contentElements[i]?.textContent?.trim() || '';
      
      if (content) {
        reviews.push({
          rating: rating,
          content: content
        });
      }
    }
    
    console.log(`📊 ${storeId}/${productId}: ${reviews.length}개 리뷰 수집`);
    
    // 서버로 리뷰 데이터 전송
    const reviewData = {
      storeId: storeId,
      productId: productId,
      reviews: reviews,
      reviewCount: reviews.length,
      timestamp: new Date().toISOString(),
      productUrl: window.location.href
    };
    
    await fetch('http://localhost:8080/api/smartstore/reviews', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(reviewData)
    });
    
    console.log(`✅ ${storeId}/${productId}: 리뷰 전송 완료`);
    return reviewData;
    
  } catch (error) {
    console.error(`❌ ${storeId}/${productId}: 리뷰 추출 실패:`, error);
    return null;
  }
}
