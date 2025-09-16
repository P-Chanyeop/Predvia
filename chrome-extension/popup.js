// 팝업 스크립트
document.addEventListener('DOMContentLoaded', function() {
  const searchBtn = document.getElementById('searchBtn');
  const searchKeyword = document.getElementById('searchKeyword');
  const status = document.getElementById('status');
  
  // 검색 버튼 클릭 이벤트
  searchBtn.addEventListener('click', function() {
    const keyword = searchKeyword.value.trim();
    if (!keyword) {
      showStatus('검색어를 입력해주세요.', 'error');
      return;
    }
    
    performSearch(keyword);
  });
  
  // 엔터키 이벤트
  searchKeyword.addEventListener('keypress', function(e) {
    if (e.key === 'Enter') {
      searchBtn.click();
    }
  });
  
  // 검색 실행
  function performSearch(keyword) {
    searchBtn.disabled = true;
    searchBtn.textContent = '검색 중...';
    showStatus('네이버 쇼핑에서 검색 중입니다...', 'loading');
    
    // 백그라운드 스크립트에 검색 요청
    chrome.runtime.sendMessage({
      action: 'searchNaver',
      keyword: keyword
    }, function(response) {
      searchBtn.disabled = false;
      searchBtn.textContent = '네이버 쇼핑 검색';
      
      if (response && response.success) {
        const productCount = response.data.products.length;
        showStatus(`검색 완료! ${productCount}개 상품을 찾았습니다.`, 'success');
        
        // 1.5초 후 팝업 닫기
        setTimeout(() => {
          window.close();
        }, 1500);
        
      } else {
        const errorMsg = response ? response.error : '알 수 없는 오류가 발생했습니다.';
        showStatus(`검색 실패: ${errorMsg}`, 'error');
      }
    });
  }
  
  // 상태 메시지 표시
  function showStatus(message, type) {
    status.textContent = message;
    status.className = `status ${type}`;
    status.style.display = 'block';
  }
});
