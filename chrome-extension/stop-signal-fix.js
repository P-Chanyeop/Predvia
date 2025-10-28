// Add this to your main crawling loop
async function crawlWithStopCheck() {
  for (let i = 0; i < products.length; i++) {
    // Check stop signal before processing each product
    if (await checkShouldStop()) {
      console.log('🛑 서버 중단 신호 감지 - 크롤링 중단');
      break; // This break statement was likely missing
    }
    
    // Process product...
    await processProduct(products[i]);
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
  return false; // Add default return value
}
