// 콘텐츠 스크립트 - 네이버 가격비교 해외직구 페이지에서 스마트스토어 링크 수집
console.log('🆕 Predvia 스마트스토어 링크 수집 확장프로그램 실행됨');

// 페이지 로딩 완료 후 실행
if (document.readyState === 'loading') {
  document.addEventListener('DOMContentLoaded', initializeExtension);
} else {
  initializeExtension();
}

function initializeExtension() {
  console.log('🆕 Predvia 스마트스토어 링크 수집 초기화 완료');
  
  // 자동으로 스마트스토어 링크 추출 및 전송
  setTimeout(() => {
    console.log('🚀 자동 스마트스토어 링크 추출 시작...');
    scrollAndCollectLinks();
  }, 3000); // 3초 후 자동 실행 (페이지 로딩 대기)
}

// 페이지 끝까지 스크롤하고 스마트스토어 링크 수집
async function scrollAndCollectLinks() {
  console.log('📜 페이지 끝까지 스크롤 - 스마트스토어 링크 수집');
  
  // 한번에 페이지 끝까지 스크롤
  window.scrollTo(0, document.body.scrollHeight);
  
  // 페이지 로딩 완료 대기
  await new Promise(resolve => setTimeout(resolve, 3000));
  
  // 스마트스토어 링크 수집
  const smartStoreLinks = extractSmartStoreLinks();
  
  console.log(`✅ 스크롤 완료: 총 ${smartStoreLinks.length}개 스마트스토어 링크 수집`);
  
  // 서버로 전송
  await sendSmartStoreLinksToServer(smartStoreLinks);
}

// 유효한 스마트스토어 링크인지 확인
function isValidSmartStoreLink(url) {
  // 특정 패턴으로 시작하는 링크만 허용
  return url.startsWith('https://smartstore.naver.com/inflow/outlink/url?url');
}

// 스마트스토어 링크 추출
function extractSmartStoreLinks() {
  console.log('🔍 스마트스토어 링크 추출 시작');
  
  const smartStoreLinks = [];
  
  try {
    // 네이버 가격비교 페이지에서 스마트스토어 링크 찾기
    // 방법 1: "스마트스토어" 텍스트가 포함된 요소 찾기
    const smartStoreElements = document.querySelectorAll('*');
    
    smartStoreElements.forEach((element) => {
      const text = element.textContent || '';
      
      // "스마트스토어" 텍스트가 포함된 요소 찾기
      if (text.includes('스마트스토어') || text.includes('smartstore')) {
        // 해당 요소나 부모 요소에서 링크 찾기
        const linkElement = element.closest('a') || element.querySelector('a');
        
        if (linkElement && linkElement.href) {
          const link = linkElement.href;
          
          // 스마트스토어 링크인지 확인
          if (link.includes('smartstore.naver.com') || link.includes('brand.naver.com')) {
            // 중복 제거
            if (!smartStoreLinks.some(item => item.url === link)) {
              // 상품명 추출 시도
              const productTitle = extractProductTitle(linkElement);
              
              smartStoreLinks.push({
                url: link,
                title: productTitle,
                seller: '스마트스토어'
              });
              
              console.log(`✅ 스마트스토어 링크 발견: ${productTitle} - ${link}`);
            }
          }
        }
      }
    });
    
    // 방법 2: 직접 스마트스토어 링크 패턴으로 찾기
    const allLinks = document.querySelectorAll('a[href*="smartstore.naver.com"], a[href*="brand.naver.com"]');
    
    allLinks.forEach((linkElement) => {
      const link = linkElement.href;
      
      // 중복 제거
      if (!smartStoreLinks.some(item => item.url === link)) {
        const productTitle = extractProductTitle(linkElement);
        
        smartStoreLinks.push({
          url: link,
          title: productTitle,
          seller: '스마트스토어'
        });
        
        console.log(`✅ 스마트스토어 링크 발견 (직접): ${productTitle} - ${link}`);
      }
    });
    
  } catch (error) {
    console.error('❌ 스마트스토어 링크 추출 오류:', error);
  }
  
  console.log(`📦 총 ${smartStoreLinks.length}개 스마트스토어 링크 추출 완료`);
  return smartStoreLinks;
}

// 상품명 추출 함수
function extractProductTitle(linkElement) {
  try {
    // 링크 텍스트에서 상품명 추출
    let title = linkElement.textContent?.trim() || '';
    
    // 부모 요소에서 상품명 찾기
    if (!title || title === '스마트스토어') {
      const parent = linkElement.closest('.product_item, .product, .item, [class*="product"]');
      if (parent) {
        const titleElement = parent.querySelector('.product_title, .title, h3, h4, [class*="title"]');
        if (titleElement) {
          title = titleElement.textContent?.trim() || '';
        }
      }
    }
    
    // 여전히 제목이 없으면 주변 텍스트에서 추출
    if (!title || title === '스마트스토어') {
      const siblings = linkElement.parentElement?.children || [];
      for (let sibling of siblings) {
        const siblingText = sibling.textContent?.trim() || '';
        if (siblingText && siblingText !== '스마트스토어' && siblingText.length > 5) {
          title = siblingText;
          break;
        }
      }
    }
    
    return title || '상품명 없음';
  } catch (error) {
    console.error('상품명 추출 오류:', error);
    return '상품명 추출 실패';
  }
}

// 서버로 스마트스토어 링크 전송 및 순차 접속
async function sendSmartStoreLinksToServer(smartStoreLinks = null) {
  try {
    console.log('📡 Predvia로 스마트스토어 링크 전송 시작...');
    
    // 링크가 전달되지 않으면 현재 페이지에서 추출
    if (!smartStoreLinks) {
      smartStoreLinks = extractSmartStoreLinks();
    }
    
    if (smartStoreLinks.length === 0) {
      console.log('⚠️ 추출된 스마트스토어 링크가 없습니다.');
      return;
    }
    
    const data = {
      smartStoreLinks: smartStoreLinks,
      source: 'naver_price_comparison',
      timestamp: new Date().toISOString(),
      pageUrl: window.location.href
    };
    
    console.log('요청 URL: http://localhost:8080/api/smartstore/links');
    console.log('전송할 데이터:', JSON.stringify({
      smartStoreLinks: data.smartStoreLinks.slice(0, 5) // 처음 5개만 로그로 확인
    }, null, 2));
    
    const response = await fetch('http://localhost:8080/api/smartstore/links', {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        'Origin': 'chrome-extension'
      },
      body: JSON.stringify(data)
    });
    
    console.log('응답 상태:', response.status);
    
    if (response.ok) {
      console.log('✅ 서버 통신 성공 - 순차 접속 시작');
      
      // 응답 상태가 200이면 순차 접속 실행 (응답 내용과 관계없이)
      await visitSmartStoreLinksSequentially(smartStoreLinks);
      
    } else {
      console.error('❌ 서버 응답 오류:', response.status, response.statusText);
    }
    
  } catch (error) {
    console.error('❌ Predvia 통신 오류:', error);
    console.error('❌ 오류 타입:', error.constructor.name);
    console.error('❌ 오류 메시지:', error.message);
    console.log('💡 Predvia 프로그램이 실행 중인지 확인해주세요.');
  }
}

// 스마트스토어 링크들을 순차적으로 방문
async function visitSmartStoreLinksSequentially(smartStoreLinks) {
  console.log(`🚀 ${smartStoreLinks.length}개 스마트스토어 링크 순차 접속 시작`);
  
  for (let i = 0; i < smartStoreLinks.length; i++) {
    const link = smartStoreLinks[i];
    
    try {
      console.log(`📍 [${i + 1}/${smartStoreLinks.length}] 접속 중: ${link.title}`);
      console.log(`🔗 URL: ${link.url}`);
      
      // 새 탭에서 링크 열기
      window.open(link.url, '_blank');
      
      // 서버에 접속 상태 알림
      await notifyServerLinkVisited(link, i + 1, smartStoreLinks.length);
      
      // 다음 링크 접속 전 대기 (2초)
      if (i < smartStoreLinks.length - 1) {
        console.log('⏳ 2초 대기 중...');
        await new Promise(resolve => setTimeout(resolve, 2000));
      }
      
    } catch (error) {
      console.error(`❌ 링크 접속 오류 [${i + 1}]: ${link.title}`, error);
    }
  }
  
  console.log('✅ 모든 스마트스토어 링크 접속 완료');
}

// 서버에 링크 방문 상태 알림
async function notifyServerLinkVisited(link, currentIndex, totalCount) {
  try {
    const visitData = {
      url: link.url,
      title: link.title,
      currentIndex: currentIndex,
      totalCount: totalCount,
      timestamp: new Date().toISOString()
    };
    
    await fetch('http://localhost:8080/api/smartstore/visit', {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        'Origin': 'chrome-extension'
      },
      body: JSON.stringify(visitData)
    });
    
  } catch (error) {
    console.error('서버 알림 오류:', error);
  }
}

console.log('🎯 Predvia 스마트스토어 링크 수집 확장프로그램 로드 완료');
