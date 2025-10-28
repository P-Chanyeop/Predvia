// 크롤링 루프에 중단 체크 추가
async function 크롤링루프() {
  for (let i = 0; i < 상품목록.length; i++) {
    // 각 상품 처리 전 중단 신호 체크
    if (await checkShouldStop()) {
      console.log('🛑 서버 중단 신호 감지 - 크롤링 중단');
      break; // 이 break문이 누락되었을 가능성
    }
    
    // 상품 처리...
    await 상품처리(상품목록[i]);
  }
}

async function checkShouldStop() {
  try {
    const response = await fetch('http://localhost:8080/api/smartstore/status', {
      method: 'GET',
      headers: { 'Content-Type': 'application/json' }
    });
    
    if (response.ok) {
      const data = await response.json();
      return data.shouldStop || false;
    }
  } catch (error) {
    console.log('중단 체크 오류:', error);
  }
  return false; // 기본 반환값 추가
}
