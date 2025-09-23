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
    
    // 즉시 처리 (추가 로딩 방지)
    setTimeout(() => {
      findReviewProductsAndCollectData(storeId);
    }, 1000); // 1초로 단축
    
  } catch (error) {
    console.error('전체상품 페이지 처리 오류:', error);
  }
}

// 로그를 서버로 전송하는 함수 (타임아웃 추가)
async function sendLogToServer(message) {
  try {
    const controller = new AbortController();
    const timeoutId = setTimeout(() => controller.abort(), 1000); // 1초 타임아웃
    
    await fetch('http://localhost:8080/api/smartstore/log', {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        'Origin': 'chrome-extension'
      },
      body: JSON.stringify({
        message: message,
        timestamp: new Date().toISOString()
      }),
      signal: controller.signal
    });
    
    clearTimeout(timeoutId);
  } catch (error) {
    // 서버 전송 실패해도 콘솔 로그는 유지, 에러는 무시
  }
}

// 리뷰 상품 찾기 및 데이터 수집
function findReviewProductsAndCollectData(storeId) {
  try {
    const logMsg = `🔍 ${storeId}: 40개 상품 내에서 마지막 리뷰 찾기 시작`;
    console.log(logMsg);
    sendLogToServer(logMsg); // await 제거
    
    // 1페이지 상품만 처리하기 위해 스크롤 방지
    window.scrollTo(0, 0);
    
    // 상품 정보 수집 (40개 제한 후 리뷰 찾기)
    const productData = collectProductData(storeId);
    
    const completeMsg = `✅ ${storeId}: 상품 데이터 수집 완료 - ${productData.length}개`;
    console.log(completeMsg);
    sendLogToServer(completeMsg); // await 제거
    
    // 서버로 상품 데이터 전송
    sendProductDataToServer(storeId, productData, 1);
    
  } catch (error) {
    const errorMsg = `❌ ${storeId}: 리뷰 상품 탐지 오류: ${error.message}`;
    console.error(errorMsg);
    sendLogToServer(errorMsg); // await 제거
  }
}

// 상품 데이터 수집 (40개 상품 내에서 마지막 리뷰 찾기)
function collectProductData(storeId) {
  try {
    const startMsg = `📊 ${storeId}: 마지막 리뷰 상품 찾기 시작`;
    console.log(startMsg);
    sendLogToServer(startMsg); // await 제거
    
    // 스크롤 차단
    document.body.style.overflow = 'hidden';
    window.scrollTo(0, 0);
    
    // XPath로 리뷰 텍스트 찾기
    const xpath = "//text()[contains(., '리뷰')]";
    const result = document.evaluate(xpath, document, null, XPathResult.ORDERED_NODE_SNAPSHOT_TYPE, null);
    
    const xpathMsg = `🔍 ${storeId}: XPath로 ${result.snapshotLength}개 리뷰 텍스트 발견`;
    console.log(xpathMsg);
    sendLogToServer(xpathMsg); // await 제거
    
    if (result.snapshotLength === 0) {
      const noReviewMsg = `❌ ${storeId}: 리뷰 텍스트를 찾을 수 없습니다`;
      console.log(noReviewMsg);
      sendLogToServer(noReviewMsg); // await 제거
      return [];
    }
    
    // 마지막 리뷰 텍스트 노드 가져오기
    const lastReviewNode = result.snapshotItem(result.snapshotLength - 1);
    const reviewText = lastReviewNode.textContent.trim();
    
    const foundMsg = `🎯 ${storeId}: 마지막 리뷰 발견 - "${reviewText}"`;
    console.log(foundMsg);
    sendLogToServer(foundMsg); // await 제거
    
    // 리뷰 텍스트 노드의 부모 상품 요소 찾기
    let productElement = lastReviewNode.parentElement;
    while (productElement && !isProductElement(productElement)) {
      productElement = productElement.parentElement;
    }
    
    if (!productElement) {
      const noProductMsg = `❌ ${storeId}: 리뷰의 상품 요소를 찾을 수 없습니다`;
      console.log(noProductMsg);
      sendLogToServer(noProductMsg); // await 제거
      return [];
    }
    
    // 상품 링크 찾기
    const productLink = productElement.querySelector('a[href*="/product/"]');
    if (!productLink) {
      const noLinkMsg = `❌ ${storeId}: 상품 링크를 찾을 수 없습니다`;
      console.log(noLinkMsg);
      sendLogToServer(noLinkMsg); // await 제거
      return [];
    }
    
    const productUrl = productLink.href;
    const linkMsg = `🔗 ${storeId}: 마지막 리뷰 상품 URL - ${productUrl}`;
    console.log(linkMsg);
    sendLogToServer(linkMsg); // await 제거
    
    // 상품 URL을 서버로 전송 (페이지 이동하지 않음)
    const completeMsg = `✅ ${storeId}: 마지막 리뷰 상품 URL 찾기 완료`;
    console.log(completeMsg);
    sendLogToServer(completeMsg); // await 제거
    
    // 스크롤 복원
    document.body.style.overflow = '';
    
    return [{ url: productUrl, storeId: storeId }];
    
  } catch (error) {
    const errorMsg = `${storeId} 마지막 리뷰 찾기 오류: ${error.message}`;
    console.error(errorMsg);
    sendLogToServer(errorMsg); // await 제거
    document.body.style.overflow = '';
    return [];
  }
}

// 상품 요소인지 확인하는 함수
function isProductElement(element) {
  const tagName = element.tagName.toLowerCase();
  const className = element.className || '';
  
  return (tagName === 'li' || tagName === 'div') && 
         (className.includes('product') || 
          className.includes('item') || 
          className.includes('card') ||
          element.querySelector('a[href*="/product/"]'));
}

// 개별 상품 정보 추출
function extractProductInfo(element, index) {
  try {
    // 상품명 추출
    const nameSelectors = ['h3', 'h4', 'h5', '[class*="title"]', '[class*="name"]', 'strong', 'span'];
    let name = '';
    
    for (let selector of nameSelectors) {
      const nameElement = element.querySelector(selector);
      if (nameElement && nameElement.textContent.trim().length > 5) {
        name = nameElement.textContent.trim();
        break;
      }
    }
    
    // 가격 추출
    const priceSelectors = ['[class*="price"]', '[class*="cost"]', 'strong', 'span'];
    let price = '';
    
    for (let selector of priceSelectors) {
      const priceElements = element.querySelectorAll(selector);
      for (let priceElement of priceElements) {
        const text = priceElement.textContent.trim();
        if (text.includes('원') || text.includes(',')) {
          price = text;
          break;
        }
      }
      if (price) break;
    }
    
    // 이미지 URL 추출
    const imgElement = element.querySelector('img');
    const imageUrl = imgElement ? imgElement.src : '';
    
    // 리뷰 정보 추출
    const reviewSpans = element.querySelectorAll('span');
    let reviewCount = '';
    
    for (let span of reviewSpans) {
      const text = span.textContent.trim();
      if (text.includes('리뷰')) {
        reviewCount = text;
        break;
      }
    }
    
    return {
      index: index,
      name: name || `상품 ${index}`,
      price: price || '가격 정보 없음',
      imageUrl: imageUrl,
      reviewCount: reviewCount || '리뷰 없음',
      element: element.outerHTML.substring(0, 200) + '...' // 디버깅용
    };
    
  } catch (error) {
    console.error(`상품 ${index} 정보 추출 오류:`, error);
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
    
    console.log('📡 서버로 상품 데이터 전송:', {
      storeId: storeId,
      productCount: productData.length,
      reviewProductCount: reviewCount
    });
    
    const response = await fetch('http://localhost:8080/api/smartstore/product-data', {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        'Origin': 'chrome-extension'
      },
      body: JSON.stringify(data)
    });
    
    if (response.ok) {
      console.log('✅ 상품 데이터 전송 완료');
    } else {
      console.error('❌ 서버 응답 오류:', response.status);
    }
    
  } catch (error) {
    console.error('❌ 상품 데이터 전송 실패:', error);
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
