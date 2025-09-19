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
  
  // 상품 이미지 직접 선택 (가장 간단한 방법)
  const productImages = document.querySelectorAll('img[src*="shopping-phinf.pstatic.net"]');
  
  console.log(`📦 총 ${productImages.length}개 상품 이미지 발견`);
  
  productImages.forEach((imgElement, index) => {
    try {
      // 상품명은 img의 alt 속성에서
      const title = imgElement.alt || '';
      
      // 썸네일은 img의 src에서
      const thumbnailUrl = imgElement.src || '';
      
      if (title && thumbnailUrl) {
        console.log(`✅ ${index + 1}번째 상품: ${title.substring(0, 30)}...`);
        
        products.push({
          index: index + 1,
          title,
          price: 'N/A', // 가격은 나중에
          thumbnail: thumbnailUrl,
          link: '', // 링크는 나중에
          extractedAt: new Date().toISOString()
        });
        
        thumbnails.push({
          index: index + 1,
          src: thumbnailUrl,
          alt: title,
          width: imgElement.naturalWidth || imgElement.width,
          height: imgElement.naturalHeight || imgElement.height
        });
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
    console.log('요청 URL: http://localhost:8080/api/thumbnails/save');
    console.log('📦 전송할 데이터:', JSON.stringify({
      products: data.products.slice(0, 2) // 처음 2개만 로그로 확인
    }, null, 2));
    
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
    console.log('📡 응답 헤더:', response.headers);
    
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
    console.error('❌ 오류 타입:', error.name);
    console.error('❌ 오류 메시지:', error.message);
    console.error('❌ 스택 트레이스:', error.stack);
    console.log('💡 Predvia 프로그램이 실행 중인지 확인해주세요.');
  }
};

// 전역 함수로 노출
window.extractThumbnails = function() {
  const data = extractCurrentPageData();
  console.log('🖼️ 추출된 썸네일:', data.thumbnails);
  return data.thumbnails;
};
