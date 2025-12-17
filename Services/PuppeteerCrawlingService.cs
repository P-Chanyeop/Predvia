using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using PuppeteerSharp;

namespace Gumaedaehang.Services
{
    public class PuppeteerCrawlingService
    {
        private readonly HttpClient _httpClient;
        private IBrowser? _browser;
        private IPage? _page; // 하나의 페이지만 사용
        private readonly SemaphoreSlim _processingLock = new(1, 1);
        private bool _shouldStop = false;
        private int _currentProductCount = 0;
        private readonly string _appDataPath;
        private readonly string _imagesPath;
        private readonly string _productDataPath;
        private readonly string _reviewsPath;
        private readonly string _categoriesPath;

        public PuppeteerCrawlingService()
        {
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("User-Agent", 
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");

            _appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Predvia");
            _imagesPath = Path.Combine(_appDataPath, "Images");
            _productDataPath = Path.Combine(_appDataPath, "ProductData");
            _reviewsPath = Path.Combine(_appDataPath, "Reviews");
            _categoriesPath = Path.Combine(_appDataPath, "Categories");

            // 디렉토리 생성
            Directory.CreateDirectory(_imagesPath);
            Directory.CreateDirectory(_productDataPath);
            Directory.CreateDirectory(_reviewsPath);
            Directory.CreateDirectory(_categoriesPath);
        }

        public async Task<bool> StartCrawlingAsync(string keyword = "")
        {
            try
            {
                await _processingLock.WaitAsync();
                
                // 브라우저가 없을 때만 새로 생성
                if (_browser == null)
                {
                    // Puppeteer 브라우저 시작 (네이버 로그인 정보 사용)
                    await new BrowserFetcher().DownloadAsync();
                    
                    // 실제 Chrome 프로필 사용 (사용자의 기본 Chrome 프로필)
                    var realChromeProfile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Google", "Chrome", "User Data");
                    string userDataDir;
                    
                    // 실제 Chrome 프로필이 존재하는지 확인
                    if (Directory.Exists(realChromeProfile))
                    {
                        await SendLogAsync($"🌐 실제 Chrome 프로필 사용: {realChromeProfile}");
                        userDataDir = realChromeProfile;
                    }
                    else
                    {
                        // 실제 프로필이 없으면 기존 NaverProfile 사용
                        await SendLogAsync("⚠️ 실제 Chrome 프로필을 찾을 수 없어 NaverProfile 사용");
                        userDataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Predvia", "NaverProfile");
                        Directory.CreateDirectory(userDataDir);
                    }

                    _browser = await Puppeteer.LaunchAsync(new LaunchOptions
                    {
                        Headless = false, // 테스트용으로 헤드리스 비활성화
                        Args = new[] { 
                            "--no-sandbox", 
                            "--disable-dev-shm-usage",
                            "--disable-blink-features=AutomationControlled",
                            "--disable-features=VizDisplayCompositor",
                            "--window-size=1920,1080"
                        },
                        UserDataDir = userDataDir // 네이버 로그인 쿠키 사용
                    });

                    // 하나의 페이지만 생성
                    _page = await _browser.NewPageAsync();
                    await _page.SetViewportAsync(new ViewPortOptions
                    {
                        Width = 1920,
                        Height = 1080
                    });
                    
                    // User-Agent 설정 (페이지 레벨에서 안전하게)
                    await _page.SetUserAgentAsync("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
                }

                _shouldStop = false;
                _currentProductCount = 0;

                await SendLogAsync("🚀 Puppeteer 크롤링 시작 (네이버 로그인 쿠키 사용)");

                // 1단계: 네이버 가격비교에서 스마트스토어 링크 수집
                var storeLinks = await CollectSmartStoreLinksAsync(keyword);
                if (storeLinks.Count == 0)
                {
                    await SendLogAsync("❌ 스마트스토어 링크를 찾을 수 없습니다");
                    return false;
                }

                await SendLogAsync($"📋 수집된 스마트스토어: {storeLinks.Count}개");

                // 2단계: 랜덤 10개 스토어 선택
                var selectedStores = SelectRandomStores(storeLinks, 10);
                await SendLogAsync($"🎯 선택된 스토어: {selectedStores.Count}개");

                // 3단계: 각 스토어 순차 처리
                foreach (var store in selectedStores)
                {
                    if (_shouldStop || _currentProductCount >= 100)
                        break;

                    await ProcessStoreAsync(store);
                }

                await SendLogAsync($"✅ 크롤링 완료: {_currentProductCount}/100개");
                return true;
            }
            catch (Exception ex)
            {
                await SendLogAsync($"❌ 크롤링 오류: {ex.Message}");
                return false;
            }
            finally
            {
                _processingLock.Release();
            }
        }

        private async Task<List<SmartStoreLink>> CollectSmartStoreLinksAsync(string keyword = "")
        {
            var links = new List<SmartStoreLink>();
            
            try
            {
                await SendLogAsync($"🔍 브라우저 상태 확인: {(_browser == null ? "null" : "존재함")}");
                
                if (_browser == null || _page == null)
                {
                    await SendLogAsync("❌ 브라우저 또는 페이지가 null입니다");
                    return links;
                }
                
                // 키워드가 있으면 검색, 없으면 기본 검색
                string url;
                if (!string.IsNullOrEmpty(keyword))
                {
                    var encodedKeyword = Uri.EscapeDataString(keyword);
                    url = $"https://search.shopping.naver.com/search/all?query={encodedKeyword}";
                    await SendLogAsync($"🔍 네이버 가격비교 검색: {keyword}");
                }
                else
                {
                    url = "https://search.shopping.naver.com/overseas?query=해외직구";
                    await SendLogAsync("🔍 네이버 가격비교 기본 검색");
                }
                
                await _page.GoToAsync(url);
                await _page.WaitForSelectorAsync("div", new WaitForSelectorOptions { Timeout = 10000 });

                await SendLogAsync("📄 네이버 가격비교 페이지 로드 완료");

                // 3초 대기 후 해외직구 탭으로 이동
                await Task.Delay(3000);
                
                var overseasUrl = !string.IsNullOrEmpty(keyword) 
                    ? $"https://search.shopping.naver.com/overseas?query={Uri.EscapeDataString(keyword)}"
                    : "https://search.shopping.naver.com/overseas?query=해외직구";
                    
                await SendLogAsync("🌐 해외직구 탭으로 이동");
                await _page.GoToAsync(overseasUrl);
                await _page.WaitForSelectorAsync("div", new WaitForSelectorOptions { Timeout = 10000 });
                await SendLogAsync("📄 해외직구 페이지 로드 완료");

                // 로그인 상태 확인
                var isLoggedIn = await _page.EvaluateExpressionAsync<bool>(@"
                    (() => {
                        // 로그인 상태 확인 방법들
                        const loginButton = document.querySelector('a[href*=""login""]');
                        const userInfo = document.querySelector('.gnb_my');
                        const profileArea = document.querySelector('.my_area');
                        
                        // 로그인 버튼이 없고 사용자 정보가 있으면 로그인됨
                        return !loginButton && (userInfo || profileArea);
                    })()
                ");

                await SendLogAsync($"🔐 로그인 상태: {(isLoggedIn ? "로그인됨" : "로그인 안됨")}");

                // 캡차 확인
                var hasCaptcha = await _page.EvaluateExpressionAsync<bool>(@"
                    document.body.innerText.includes('자동입력 방지') || 
                    document.body.innerText.includes('캡차') ||
                    document.querySelector('iframe[src*=""captcha""]') !== null
                ");

                if (hasCaptcha)
                {
                    await SendLogAsync("🚫 캡차 감지됨 - 봇으로 인식되었습니다 - 10초 후 창이 닫힙니다");
                    await Task.Delay(10000); // 10초 대기로 확인 가능
                    return new List<SmartStoreLink>();
                }

                if (!isLoggedIn)
                {
                    await SendLogAsync("❌ 로그인이 필요합니다 - 10초 후 창이 닫힙니다");
                    await Task.Delay(10000); // 10초 대기로 확인 가능
                    return new List<SmartStoreLink>();
                }

                // 3초 대기 (JavaScript 실행 완료 대기)
                await Task.Delay(3000);

                // 페이지 끝까지 스크롤 (최대 10회)
                await ScrollToBottomAsync(_page);

                // 최종 1초 대기
                await Task.Delay(1000);

                // 현재 페이지 URL 확인
                await SendLogAsync($"🔍 현재 페이지 URL: {_page.Url}");

                // 페이지 내용 확인
                var pageTitle = await _page.GetTitleAsync();
                await SendLogAsync($"📄 페이지 제목: {pageTitle}");

                // 모든 링크 개수 확인
                var allLinksCount = await _page.EvaluateExpressionAsync<int>(@"
                    document.querySelectorAll('a').length
                ");
                await SendLogAsync($"🔗 전체 링크 개수: {allLinksCount}개");

                // smartstore 포함 링크 개수 확인
                var smartstoreLinksCount = await _page.EvaluateExpressionAsync<int>(@"
                    document.querySelectorAll('a[href*=""smartstore.naver.com""]').length
                ");
                await SendLogAsync($"🏪 smartstore 포함 링크: {smartstoreLinksCount}개");

                // inflow 포함 링크 개수 확인
                var inflowLinksCount = await _page.EvaluateExpressionAsync<int>(@"
                    document.querySelectorAll('a[href*=""inflow/outlink/url""]').length
                ");
                await SendLogAsync($"🔄 inflow 포함 링크: {inflowLinksCount}개");

                // 스마트스토어 링크 추출 (Chrome 확장프로그램과 동일한 방법)
                var extractedLinks = await _page.EvaluateExpressionAsync<string[]>(@"
                    (() => {
                        const smartStoreLinks = [];
                        
                        // 방법 1: '스마트스토어' 텍스트가 포함된 요소 찾기
                        const allElements = document.querySelectorAll('*');
                        
                        allElements.forEach((element) => {
                            const text = element.textContent || '';
                            
                            if (text.includes('스마트스토어') || text.includes('smartstore')) {
                                const linkElement = element.closest('a') || element.querySelector('a');
                                
                                if (linkElement && linkElement.href) {
                                    const link = linkElement.href;
                                    
                                    if (link.startsWith('https://smartstore.naver.com/inflow/outlink/url?url')) {
                                        if (!smartStoreLinks.includes(link)) {
                                            smartStoreLinks.push(link);
                                        }
                                    }
                                }
                            }
                        });
                        
                        // 방법 2: 직접 스마트스토어 링크 패턴으로 찾기
                        const allLinks = document.querySelectorAll('a[href*=""smartstore.naver.com""], a[href*=""brand.naver.com""]');
                        
                        allLinks.forEach((linkElement) => {
                            const link = linkElement.href;
                            
                            if (link.startsWith('https://smartstore.naver.com/inflow/outlink/url?url')) {
                                if (!smartStoreLinks.includes(link)) {
                                    smartStoreLinks.push(link);
                                }
                            }
                        });
                        
                        return smartStoreLinks;
                    })()
                ");

                await SendLogAsync($"🎯 추출된 스마트스토어 링크: {extractedLinks.Length}개");

                // 각 링크 상세 분석
                for (int i = 0; i < Math.Min(extractedLinks.Length, 3); i++)
                {
                    await SendLogAsync($"🔗 링크 {i+1}: {extractedLinks[i]}");
                }

                foreach (var link in extractedLinks)
                {
                    await SendLogAsync($"🔍 처리 중인 링크: {link}");
                    var storeId = ExtractStoreIdFromUrl(link);
                    await SendLogAsync($"📝 추출된 스토어 ID: '{storeId}'");
                    
                    if (!string.IsNullOrEmpty(storeId))
                    {
                        links.Add(new SmartStoreLink
                        {
                            StoreId = storeId,
                            Url = link,
                            Title = $"스토어_{storeId}"
                        });
                        await SendLogAsync($"✅ 스토어 추가 성공: {storeId}");
                    }
                    else
                    {
                        await SendLogAsync($"❌ 스토어 ID 추출 실패: {link}");
                    }
                }

                await SendLogAsync($"🔗 스마트스토어 링크 수집 완료: {links.Count}개");
            }
            catch (Exception ex)
            {
                await SendLogAsync($"❌ 링크 수집 오류: {ex.Message}");
            }

            return links.DistinctBy(x => x.StoreId).ToList();
        }

        private async Task ScrollToBottomAsync(IPage page)
        {
            await page.EvaluateExpressionAsync(@"
                (async () => {
                    let previousHeight = 0;
                    let currentHeight = document.body.scrollHeight;
                    let scrollAttempts = 0;
                    const maxScrollAttempts = 10;
                    
                    while (previousHeight !== currentHeight && scrollAttempts < maxScrollAttempts) {
                        previousHeight = currentHeight;
                        
                        window.scrollTo(0, document.body.scrollHeight);
                        await new Promise(resolve => setTimeout(resolve, 500));
                        
                        currentHeight = document.body.scrollHeight;
                        scrollAttempts++;
                    }
                })()
            ");
        }

        private List<SmartStoreLink> SelectRandomStores(List<SmartStoreLink> allStores, int count)
        {
            var random = new Random();
            return allStores.OrderBy(x => random.Next()).Take(count).ToList();
        }

        private async Task ProcessStoreAsync(SmartStoreLink store)
        {
            try
            {
                await SendLogAsync($"🏪 {store.StoreId}: 스토어 처리 시작");

                // 1. 공구 개수 확인
                var gongguCount = await CheckGongguCountAsync(store.StoreId);
                if (gongguCount < 1000)
                {
                    await SendLogAsync($"⏭️ {store.StoreId}: 공구 {gongguCount}개 (1000개 미만) - 스킵");
                    return;
                }

                await SendLogAsync($"✅ {store.StoreId}: 공구 {gongguCount}개 - 처리 진행");

                // 2. 전체상품 페이지 이동
                await ProcessAllProductsPageAsync(store.StoreId);
            }
            catch (Exception ex)
            {
                await SendLogAsync($"❌ {store.StoreId}: 스토어 처리 오류 - {ex.Message}");
            }
        }

        private async Task<int> CheckGongguCountAsync(string storeId)
        {
            try
            {
                var page = await _browser!.NewPageAsync();
                var gongguUrl = $"https://smartstore.naver.com/{storeId}/category/50000165";
                
                await page.GoToAsync(gongguUrl);
                await Task.Delay(3000);

                var pageText = await page.EvaluateExpressionAsync<string>("document.body.textContent || ''");
                
                var patterns = new[]
                {
                    @"공구\s*\(\s*총\s*([0-9,]+)\s*개\s*\)",
                    @"공구\s*\(\s*([0-9,]+)\s*개\s*\)",
                    @"총\s*([0-9,]+)\s*개",
                    @"([0-9,]+)\s*개\s*상품"
                };

                foreach (var pattern in patterns)
                {
                    var match = Regex.Match(pageText, pattern);
                    if (match.Success)
                    {
                        var countStr = match.Groups[1].Value.Replace(",", "");
                        if (int.TryParse(countStr, out int count))
                        {
                            await page.CloseAsync();
                            return count;
                        }
                    }
                }

                await page.CloseAsync();
                return 0;
            }
            catch (Exception ex)
            {
                await SendLogAsync($"❌ {storeId}: 공구 개수 확인 오류 - {ex.Message}");
                return 0;
            }
        }

        private async Task ProcessAllProductsPageAsync(string storeId)
        {
            try
            {
                var page = await _browser!.NewPageAsync();
                var allProductsUrl = $"https://smartstore.naver.com/{storeId}/category/ALL?st=TOTALSALE";
                
                await page.GoToAsync(allProductsUrl);
                await Task.Delay(2000);

                await SendLogAsync($"📄 {storeId}: 전체상품 페이지 로드 완료");

                // 카테고리 정보 추출
                await ExtractAndSaveCategoriesAsync(page, storeId);

                // 상품 데이터 수집
                await CollectProductDataAsync(page, storeId);

                await page.CloseAsync();
            }
            catch (Exception ex)
            {
                await SendLogAsync($"❌ {storeId}: 전체상품 페이지 처리 오류 - {ex.Message}");
            }
        }

        private async Task ExtractAndSaveCategoriesAsync(IPage page, string storeId)
        {
            try
            {
                var categories = await page.EvaluateExpressionAsync<object[]>(@"
                    Array.from(document.querySelectorAll('ul.ySOklWNBjf .sAla67hq4a')).map((span, index) => ({
                        name: span.textContent.trim(),
                        url: span.closest('a')?.href || '',
                        id: index + 1,
                        order: index
                    }))
                ");

                if (categories.Length > 0)
                {
                    var categoryData = new
                    {
                        storeId = storeId,
                        categories = categories,
                        timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                    };

                    var json = JsonSerializer.Serialize(categoryData, new JsonSerializerOptions { WriteIndented = true });
                    var filePath = Path.Combine(_categoriesPath, $"{storeId}_categories.json");
                    await File.WriteAllTextAsync(filePath, json, Encoding.UTF8);

                    await SendLogAsync($"📂 {storeId}: 카테고리 수집 성공 - {categories.Length}개");
                }
            }
            catch (Exception ex)
            {
                await SendLogAsync($"❌ {storeId}: 카테고리 추출 오류 - {ex.Message}");
            }
        }

        private async Task CollectProductDataAsync(IPage page, string storeId)
        {
            try
            {
                // 40개 상품 중 마지막 리뷰 상품 찾기
                var lastReviewProductInfo = await FindLastReviewProductAsync(page, storeId);
                if (lastReviewProductInfo == null)
                {
                    await SendLogAsync($"⏭️ {storeId}: 리뷰 상품 없음 - 스킵");
                    return;
                }

                await SendLogAsync($"🎯 {storeId}: 마지막 리뷰 상품 {lastReviewProductInfo.Rank}번째 발견");

                // 1번부터 마지막 리뷰 상품까지 순차 접속
                await VisitProductsSequentiallyAsync(storeId, lastReviewProductInfo.Rank);
            }
            catch (Exception ex)
            {
                await SendLogAsync($"❌ {storeId}: 상품 데이터 수집 오류 - {ex.Message}");
            }
        }

        private async Task<ProductInfo?> FindLastReviewProductAsync(IPage page, string storeId)
        {
            try
            {
                var productInfo = await page.EvaluateExpressionAsync<object>(@"
                    (() => {
                        const reviewSpans = document.evaluate(
                            ""//span[normalize-space(text())='리뷰']"",
                            document,
                            null,
                            XPathResult.ORDERED_NODE_SNAPSHOT_TYPE,
                            null
                        );
                        
                        if (reviewSpans.snapshotLength === 0) return null;
                        
                        const lastIndex = Math.min(39, reviewSpans.snapshotLength - 1);
                        const lastReviewSpan = reviewSpans.snapshotItem(lastIndex);
                        
                        let productElement = lastReviewSpan;
                        while (productElement && !productElement.getAttribute('data-shp-contents-id')) {
                            productElement = productElement.parentElement;
                        }
                        
                        if (!productElement) return null;
                        
                        const productId = productElement.getAttribute('data-shp-contents-id');
                        const rank = productElement.getAttribute('data-shp-contents-rank') || (lastIndex + 1).toString();
                        
                        return {
                            productId: productId,
                            rank: parseInt(rank),
                            url: `https://smartstore.naver.com/${storeId}/products/${productId}`
                        };
                    })()
                ");

                if (productInfo != null)
                {
                    var jsonElement = (JsonElement)productInfo;
                    return new ProductInfo
                    {
                        ProductId = jsonElement.GetProperty("productId").GetString()!,
                        Rank = jsonElement.GetProperty("rank").GetInt32(),
                        Url = jsonElement.GetProperty("url").GetString()!
                    };
                }

                return null;
            }
            catch (Exception ex)
            {
                await SendLogAsync($"❌ {storeId}: 마지막 리뷰 상품 찾기 오류 - {ex.Message}");
                return null;
            }
        }

        private async Task VisitProductsSequentiallyAsync(string storeId, int lastRank)
        {
            for (int rank = 1; rank <= lastRank && !_shouldStop && _currentProductCount < 100; rank++)
            {
                try
                {
                    var productUrl = $"https://smartstore.naver.com/{storeId}/products/{rank}";
                    await VisitProductPageAsync(storeId, rank.ToString(), productUrl);
                    
                    await Task.Delay(2000); // 2초 대기
                }
                catch (Exception ex)
                {
                    await SendLogAsync($"❌ {storeId}: 상품 {rank} 처리 오류 - {ex.Message}");
                }
            }
        }

        private async Task VisitProductPageAsync(string storeId, string productId, string productUrl)
        {
            try
            {
                var page = await _browser!.NewPageAsync();
                await page.GoToAsync(productUrl);
                await Task.Delay(3000);

                // 차단 감지
                var pageText = await page.EvaluateExpressionAsync<string>("document.body.textContent || ''");
                if (pageText.Contains("현재 서비스 접속이 불가합니다"))
                {
                    await SendLogAsync($"🚫 {storeId}: 상품 {productId} 차단 감지");
                    _shouldStop = true;
                    await page.CloseAsync();
                    return;
                }

                // 이미지 추출 및 저장
                await ExtractAndSaveImageAsync(page, storeId, productId);

                // 상품명 추출 및 저장
                await ExtractAndSaveProductNameAsync(page, storeId, productId);

                // 리뷰 추출 및 저장
                await ExtractAndSaveReviewsAsync(page, storeId, productId, productUrl);

                await page.CloseAsync();

                _currentProductCount++;
                var progress = (_currentProductCount * 100.0 / 100).ToString("F1");
                await SendLogAsync($"📊 실시간 진행률: {_currentProductCount}/100개 ({progress}%)");

                if (_currentProductCount >= 100)
                {
                    _shouldStop = true;
                    await SendLogAsync("🎉 목표 100개 달성!");
                }
            }
            catch (Exception ex)
            {
                await SendLogAsync($"❌ {storeId}: 상품 {productId} 방문 오류 - {ex.Message}");
            }
        }

        private async Task ExtractAndSaveImageAsync(IPage page, string storeId, string productId)
        {
            try
            {
                var imageUrl = await page.EvaluateExpressionAsync<string>(@"
                    (() => {
                        const img = document.querySelector('.bd_2DO68 img[alt=""대표이미지""]');
                        return img ? img.src : null;
                    })()
                ");

                if (!string.IsNullOrEmpty(imageUrl))
                {
                    var imageBytes = await _httpClient.GetByteArrayAsync(imageUrl);
                    var fileName = $"{storeId}_{productId}_main.jpg";
                    var filePath = Path.Combine(_imagesPath, fileName);
                    await File.WriteAllBytesAsync(filePath, imageBytes);

                    await SendLogAsync($"🖼️ {storeId}: 상품 {productId} 이미지 저장 완료");
                }
            }
            catch (Exception ex)
            {
                await SendLogAsync($"❌ {storeId}: 상품 {productId} 이미지 추출 오류 - {ex.Message}");
            }
        }

        private async Task ExtractAndSaveProductNameAsync(IPage page, string storeId, string productId)
        {
            try
            {
                var productName = await page.EvaluateExpressionAsync<string>(@"
                    (() => {
                        const nameElement = document.querySelector('.DCVBehA8ZB') || document.querySelector('h3._copyable');
                        return nameElement ? nameElement.textContent.trim() : null;
                    })()
                ");

                if (!string.IsNullOrEmpty(productName))
                {
                    var fileName = $"{storeId}_{productId}_name.txt";
                    var filePath = Path.Combine(_productDataPath, fileName);
                    await File.WriteAllTextAsync(filePath, productName, Encoding.UTF8);

                    await SendLogAsync($"📝 {storeId}: 상품 {productId} 상품명 저장 완료");
                }
            }
            catch (Exception ex)
            {
                await SendLogAsync($"❌ {storeId}: 상품 {productId} 상품명 추출 오류 - {ex.Message}");
            }
        }

        private async Task ExtractAndSaveReviewsAsync(IPage page, string storeId, string productId, string productUrl)
        {
            try
            {
                var reviews = await page.EvaluateExpressionAsync<object[]>(@"
                    (() => {
                        const reviewElements = document.querySelectorAll('.vhlVUsCtw3');
                        const reviews = [];
                        
                        reviewElements.forEach(element => {
                            const ratingElement = element.querySelector('em.n6zq2yy0KA');
                            const contentElement = element.querySelector('.K0kwJOXP06');
                            
                            if (ratingElement && contentElement) {
                                reviews.push({
                                    rating: parseInt(ratingElement.textContent.trim()),
                                    content: contentElement.textContent.trim()
                                });
                            }
                        });
                        
                        return reviews;
                    })()
                ");

                var reviewData = new
                {
                    storeId = storeId,
                    productId = productId,
                    productUrl = productUrl,
                    reviews = reviews,
                    reviewCount = reviews.Length,
                    timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                };

                var json = JsonSerializer.Serialize(reviewData, new JsonSerializerOptions { WriteIndented = true });
                var fileName = $"{storeId}_{productId}_reviews.json";
                var filePath = Path.Combine(_reviewsPath, fileName);
                await File.WriteAllTextAsync(filePath, json, Encoding.UTF8);

                await SendLogAsync($"⭐ {storeId}: 상품 {productId} 리뷰 {reviews.Length}개 저장 완료");
            }
            catch (Exception ex)
            {
                await SendLogAsync($"❌ {storeId}: 상품 {productId} 리뷰 추출 오료 - {ex.Message}");
            }
        }

        private string ExtractStoreIdFromUrl(string url)
        {
            try
            {
                // URL 디코딩
                var decodedUrl = Uri.UnescapeDataString(url);
                
                // url= 파라미터에서 실제 스마트스토어 URL 추출
                var urlMatch = Regex.Match(decodedUrl, @"url=([^&]+)");
                
                if (urlMatch.Success && !string.IsNullOrEmpty(urlMatch.Groups[1].Value))
                {
                    var actualStoreUrl = urlMatch.Groups[1].Value;
                    
                    // 실제 스토어 URL에서 ID 추출
                    var storeIdMatch = Regex.Match(actualStoreUrl, @"smartstore\.naver\.com/([^&/\?]+)");
                    
                    if (storeIdMatch.Success && !string.IsNullOrEmpty(storeIdMatch.Groups[1].Value))
                    {
                        return storeIdMatch.Groups[1].Value;
                    }
                }
                
                return "";
            }
            catch
            {
                return "";
            }
        }

        private async Task SendLogAsync(string message)
        {
            try
            {
                var logData = new
                {
                    message = message,
                    timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                };

                var json = JsonSerializer.Serialize(logData);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                
                await _httpClient.PostAsync("http://localhost:8080/api/smartstore/log", content);
            }
            catch
            {
                // 로그 전송 실패는 무시
            }
        }

        public async Task<List<TaobaoProduct>> SearchTaobaoImageAsync(string imagePath)
        {
            var products = new List<TaobaoProduct>();
            
            try
            {
                if (_browser == null)
                {
                    // UserDataDir 폴더 생성 (네이버 로그인용)
                    var userDataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Predvia", "NaverProfile");
                    Directory.CreateDirectory(userDataDir);
                    
                    await new BrowserFetcher().DownloadAsync();
                    _browser = await Puppeteer.LaunchAsync(new LaunchOptions
                    {
                        Headless = false, // 타오바오 검색은 사용자가 볼 수 있도록
                        Args = new[] { 
                            "--no-sandbox", 
                            "--disable-dev-shm-usage",
                            "--disable-blink-features=AutomationControlled"
                        },
                        UserDataDir = userDataDir // 네이버 로그인 쿠키 사용
                    });
                }

                var page = await _browser.NewPageAsync();
                
                await SendLogAsync("🔍 타오바오 이미지 검색 시작 (네이버 로그인 쿠키 사용)");
                
                // 타오바오 이미지 검색 페이지로 이동
                await page.GoToAsync("https://www.taobao.com/");
                await Task.Delay(2000);

                // 이미지 검색 버튼 찾기 및 클릭
                await page.ClickAsync("input[type='file']");
                
                // 이미지 파일 업로드 (PuppeteerSharp에서는 다른 방식 사용)
                var fileInput = await page.QuerySelectorAsync("input[type='file']");
                await fileInput!.UploadFileAsync(imagePath);
                await Task.Delay(3000);

                // 검색 결과 대기
                await page.WaitForSelectorAsync(".item", new WaitForSelectorOptions { Timeout = 10000 });

                // 상품 정보 추출
                var productData = await page.EvaluateExpressionAsync<object[]>(@"
                    Array.from(document.querySelectorAll('.item')).slice(0, 5).map(item => {
                        const img = item.querySelector('img');
                        const title = item.querySelector('.title');
                        const price = item.querySelector('.price');
                        const sales = item.querySelector('.sales');
                        const link = item.querySelector('a');
                        
                        return {
                            image: img ? img.src : '',
                            title: title ? title.textContent.trim() : '',
                            price: price ? price.textContent.trim() : '',
                            sales: sales ? sales.textContent.trim() : '',
                            url: link ? link.href : ''
                        };
                    })
                ");

                foreach (var item in productData)
                {
                    var jsonElement = (JsonElement)item;
                    products.Add(new TaobaoProduct
                    {
                        Image = jsonElement.GetProperty("image").GetString() ?? "",
                        Title = jsonElement.GetProperty("title").GetString() ?? "",
                        Price = jsonElement.GetProperty("price").GetString() ?? "",
                        Sales = jsonElement.GetProperty("sales").GetString() ?? "",
                        Url = jsonElement.GetProperty("url").GetString() ?? ""
                    });
                }

                await SendLogAsync($"🔍 타오바오 이미지 검색 완료: {products.Count}개 상품 발견");
                
                // 페이지는 열어두고 사용자가 확인할 수 있도록 함
                // await page.CloseAsync();
            }
            catch (Exception ex)
            {
                await SendLogAsync($"❌ 타오바오 이미지 검색 오류: {ex.Message}");
            }

            return products;
        }

        public async Task<List<string>> ExtractKeywordsFromNaverAsync(string keyword)
        {
            var keywords = new List<string>();
            
            try
            {
                if (_browser == null)
                {
                    await new BrowserFetcher().DownloadAsync();
                    _browser = await Puppeteer.LaunchAsync(new LaunchOptions
                    {
                        Headless = true,
                        Args = new[] { "--no-sandbox", "--disable-dev-shm-usage" }
                    });
                }

                var page = await _browser.NewPageAsync();
                var encodedKeyword = Uri.EscapeDataString(keyword);
                var searchUrl = $"https://search.shopping.naver.com/search/all?query={encodedKeyword}&productSet=overseas";
                
                await page.GoToAsync(searchUrl);
                await Task.Delay(3000);

                // 페이지 끝까지 스크롤
                await ScrollToBottomAsync(page);

                // 상품명 추출
                var productNames = await page.EvaluateExpressionAsync<string[]>(@"
                    Array.from(document.querySelectorAll('.basicList_title__VfX3c, .product_title, h3'))
                        .map(el => el.textContent.trim())
                        .filter(text => text.length >= 10 && /[가-힣]/.test(text))
                        .filter(text => !/(광고|스폰서|네이버|쇼핑|가격비교)/i.test(text))
                ");

                // 키워드 분리 및 필터링
                var keywordSet = new HashSet<string>();
                foreach (var name in productNames)
                {
                    var words = Regex.Split(name, @"[\s\-_/\(\)\[\]]+")
                        .Where(w => w.Length >= 2 && Regex.IsMatch(w, @"[가-힣]"))
                        .ToArray();
                    
                    foreach (var word in words)
                    {
                        keywordSet.Add(word);
                    }
                }

                keywords = keywordSet.ToList();
                await page.CloseAsync();
                
                await SendLogAsync($"🏷️ 키워드 추출 완료: {keywords.Count}개");
            }
            catch (Exception ex)
            {
                await SendLogAsync($"❌ 키워드 추출 오류: {ex.Message}");
            }

            return keywords;
        }

        public void Dispose()
        {
            _browser?.CloseAsync();
            _httpClient?.Dispose();
            _processingLock?.Dispose();
        }
    }

    public class SmartStoreLink
    {
        public string StoreId { get; set; } = "";
        public string Url { get; set; } = "";
        public string Title { get; set; } = "";
    }

    public class ProductInfo
    {
        public string ProductId { get; set; } = "";
        public int Rank { get; set; }
        public string Url { get; set; } = "";
    }

    public class TaobaoProduct
    {
        public string Image { get; set; } = "";
        public string ImageUrl { get; set; } = "";
        public string Title { get; set; } = "";
        public string Price { get; set; } = "";
        public string Sales { get; set; } = "";
        public string Url { get; set; } = "";
        public string ProductUrl { get; set; } = "";
    }
}
