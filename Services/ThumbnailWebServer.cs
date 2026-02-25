using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Management;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using PuppeteerSharp;

namespace Gumaedaehang.Services
{
    public class ThumbnailWebServer
    {
        // ⭐ 싱글톤 인스턴스 * from 
        public static ThumbnailWebServer? Instance { get; private set; }

        private WebApplication? _app;
        private readonly ThumbnailService _thumbnailService;
        private bool _isRunning = false;

        // 정적 IsRunning 속성
        public static bool IsRunning { get; private set; } = false;

        // ⭐ MainWindow 참조 (자동 저장용)
        private static MainWindow? _mainWindowReference = null;
        public static void SetMainWindowReference(MainWindow mainWindow)
        {
            _mainWindowReference = mainWindow;
        }
        
        // ⭐ 가격 필터링 설정 (정적 변수)
        private static int _minPrice = 1000; // 최소 가격 (원) - 사용자 친화적 기본값
        private static int _maxPrice = 50000; // 최대 가격 (원) - 사용자 친화적 기본값  
        private static bool _priceFilterEnabled = true; // 가격 필터링 활성화 🔥
        
        // ⭐ 타오바오 쿠키 저장
        private static Dictionary<string, string> _taobaoCookies = new();
        private static string? _taobaoToken = null;
        
        // ⭐ 타오바오 토큰 가져오기 (외부에서 접근 가능)
        public static string? GetTaobaoToken() => _taobaoToken;
        
        // ⭐ Predvia 전용 Chrome 프로필 경로
        private static string GetPredviaChromeProfile()
        {
            var profilePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Predvia",
                "ChromeProfile"
            );
            Directory.CreateDirectory(profilePath);
            return profilePath;
        }
        
        // ⭐ 상태 관리 시스템
        private readonly Dictionary<string, StoreState> _storeStates = new();
        private readonly object _statesLock = new object();
        
        // ⭐ 상품 카운터 및 랜덤 선택 관련 변수
        private int _productCount = 0;
        private int _totalAttempted = 0; // 총 시도 수 (성공+실패)
        private int _sessionStartFileCount = 0; // ⭐ 세션 시작 시 파일 개수
        private bool _isCrawlingActive = false;
        private const int TARGET_PRODUCT_COUNT = 100;
        private const int MAX_STORES_TO_VISIT = 10;
        private List<SmartStoreLink> _selectedStores = new();
        private int _currentStoreIndex = 0; // 현재 처리 중인 스토어 인덱스
        private readonly object _storeProcessLock = new object(); // 스토어 처리 동기화
        private bool _shouldStop = false;
        private readonly object _counterLock = new object();
        private bool _completionPopupShown = false; // 완료 팝업 중복 방지
        private DateTime _lastCrawlingActivity = DateTime.Now; // 마지막 크롤링 활동 시간
        private System.Threading.Timer? _crawlingWatchdogTimer; // 크롤링 멈춤 감지 타이머
        
        // ⭐ 중복 처리 방지를 위한 처리된 스토어 추적
        private readonly HashSet<string> _processedStores = new HashSet<string>();
        
        // ⭐ 상품별 중복 카운팅 방지
        private readonly HashSet<string> _processedProducts = new HashSet<string>();
        
        // ⭐ 상품 처리 완료 신호
        private string? _lastCompletedProductId = null;
        private readonly object _productDoneLock = new object();
        
        // ⭐ 크롤링 허용 플래그
        private bool _crawlingAllowed = true;
        private readonly object _crawlingLock = new object();

        // ⭐ 서버 주도 크롤링 상태 머신
        private CrawlStateMachine? _crawlSM = null;
        private bool _v2Mode = false; // v2 모드 활성화 여부

        // ⭐ 상품별 키워드 저장 (productId → keywords)
        private Dictionary<int, List<string>> _productKeywords = new();
        private List<string> _latestKeywords = new();  // 가장 최근 키워드
        private DateTime _latestKeywordsTime = DateTime.MinValue;  // 최근 키워드 시간
        private int _currentProductId = 0;  // 현재 검색 중인 상품 ID
        private readonly object _keywordsLock = new object();
        
        // ⭐ 프록시 시스템 (모모아이피)
        private static List<string> _proxyList = new();
        private static readonly Random _proxyRandom = new();
        private static readonly object _proxyLock = new object();
        
        // ⭐ 랜덤 User-Agent 목록 (30개)
        private static readonly string[] _userAgents = new[]
        {
            // Chrome Windows
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/119.0.0.0 Safari/537.36",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/118.0.0.0 Safari/537.36",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/117.0.0.0 Safari/537.36",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/116.0.0.0 Safari/537.36",
            // Chrome Mac
            "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36",
            "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/119.0.0.0 Safari/537.36",
            "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/118.0.0.0 Safari/537.36",
            // Chrome Linux
            "Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36",
            "Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/119.0.0.0 Safari/537.36",
            // Firefox
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:121.0) Gecko/20100101 Firefox/121.0",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:120.0) Gecko/20100101 Firefox/120.0",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:119.0) Gecko/20100101 Firefox/119.0",
            "Mozilla/5.0 (Macintosh; Intel Mac OS X 10.15; rv:121.0) Gecko/20100101 Firefox/121.0",
            "Mozilla/5.0 (X11; Linux x86_64; rv:121.0) Gecko/20100101 Firefox/121.0",
            // Safari
            "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.2 Safari/605.1.15",
            "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.1 Safari/605.1.15",
            "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/16.6 Safari/605.1.15",
            // Edge
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36 Edg/120.0.0.0",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/119.0.0.0 Safari/537.36 Edg/119.0.0.0",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/118.0.0.0 Safari/537.36 Edg/118.0.0.0",
            // Mobile
            "Mozilla/5.0 (iPhone; CPU iPhone OS 17_2 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.2 Mobile/15E148 Safari/604.1",
            "Mozilla/5.0 (iPhone; CPU iPhone OS 17_1 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.1 Mobile/15E148 Safari/604.1",
            "Mozilla/5.0 (iPhone; CPU iPhone OS 16_6 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/16.6 Mobile/15E148 Safari/604.1",
            "Mozilla/5.0 (iPad; CPU OS 17_2 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.2 Mobile/15E148 Safari/604.1",
            "Mozilla/5.0 (Linux; Android 14; SM-S918B) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Mobile Safari/537.36",
            "Mozilla/5.0 (Linux; Android 14; Pixel 8) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Mobile Safari/537.36",
            // Opera
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36 OPR/106.0.0.0",
            "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36 OPR/106.0.0.0",
            // Brave
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36 Brave/120"
        };
        
        private static string GetRandomUserAgent() => _userAgents[_proxyRandom.Next(_userAgents.Length)];

        public ThumbnailWebServer()
        {
            _thumbnailService = new ThumbnailService();
            Instance = this; // 싱글톤 인스턴스 설정
        }

        // ⭐ CAPTCHA 감지 핸들러
        private bool _captchaDetected = false;

        private IResult HandleCaptchaDetected(HttpContext context)
        {
            try
            {
                using var reader = new StreamReader(context.Request.Body);
                var body = reader.ReadToEnd();

                LogWindow.AddLogStatic($"🚫 네이버 가격비교 캡챠 감지!");

                // 플래그 설정
                _captchaDetected = true;

                // ⭐ UI 스레드에서 메시지 박스 표시
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    try
                    {
                        LogWindow.AddLogStatic("⚠️ 캡챠 감지 - 사용자 안내 메시지 표시");
                        
                        // 간단한 메시지 박스 표시
                        var messageBox = new Window
                        {
                            Title = "캡챠 감지",
                            Width = 400,
                            Height = 150,
                            WindowStartupLocation = WindowStartupLocation.CenterScreen,
                            CanResize = false
                        };

                        var panel = new StackPanel
                        {
                            Margin = new Avalonia.Thickness(20),
                            Spacing = 15
                        };

                        panel.Children.Add(new TextBlock
                        {
                            Text = "네이버 캡챠가 감지되었습니다.\n\n기존 브라우저에서 가격비교 탭 접속 후\n캡챠를 1회 해결한 뒤 다시 시도해주세요.",
                            TextAlignment = Avalonia.Media.TextAlignment.Center,
                            FontSize = 14
                        });

                        var okButton = new Button
                        {
                            Content = "확인",
                            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                            Padding = new Avalonia.Thickness(30, 8)
                        };
                        okButton.Click += (s, e) => messageBox.Close();
                        panel.Children.Add(okButton);

                        messageBox.Content = panel;
                        messageBox.Show();
                    }
                    catch (Exception ex)
                    {
                        LogWindow.AddLogStatic($"⚠️ 메시지 박스 표시 실패: {ex.Message}");
                    }
                });

                return Results.Ok(new { success = true, message = "CAPTCHA detected" });
            }
            catch (Exception ex)
            {
                LogWindow.AddLogStatic($"❌ CAPTCHA 처리 오류: {ex.Message}");
                return Results.Json(new { success = false, error = ex.Message });
            }
        }

        // CAPTCHA 플래그 확인 및 리셋
        public bool CheckAndResetCaptcha()
        {
            var result = _captchaDetected;
            _captchaDetected = false;
            return result;
        }

        public async Task StartAsync()
        {
            if (_isRunning) 
            {
                LogWindow.AddLogStatic("⚠️ 웹서버가 이미 실행 중입니다");
                return;
            }

            try
            {
                LogWindow.AddLogStatic("🚀 웹서버 시작 중...");
                
                // ⭐ 크롤링 플래그 강제 초기화
                lock (_crawlingLock)
                {
                    _crawlingAllowed = false;
                }
                LogWindow.AddLogStatic("🔄 크롤링 플래그 초기화 완료 (false)");
                
                // ⭐ 기존 데이터 초기화 비활성화 - 엑셀 추출 시에만 삭제
                // ClearPreviousData();
                
                // ⭐ 타오바오 쿠키 자동 로드
                await LoadTaobaoCookiesFromFile();
                
                // ⭐ 프록시 목록 로드
                LoadProxyList();
                
                var builder = WebApplication.CreateBuilder();
                
                // CORS 서비스 추가
                builder.Services.AddCors();
                LogWindow.AddLogStatic("✅ CORS 서비스 추가 완료");
                
                _app = builder.Build();
                
                // CORS 정책 설정
                _app.UseCors(policy => policy
                    .AllowAnyOrigin()
                    .AllowAnyMethod()
                    .AllowAnyHeader());
                LogWindow.AddLogStatic("✅ CORS 정책 설정 완료");

                // API 엔드포인트 설정
                _app.MapPost("/api/thumbnails/save", HandleSaveThumbnails);
                _app.MapGet("/api/thumbnails/list", HandleGetThumbnails);
                _app.MapPost("/api/smartstore/links", HandleSmartStoreLinks);
                _app.MapPost("/api/smartstore/visit", HandleSmartStoreVisit);
                _app.MapPost("/api/smartstore/gonggu-check", HandleGongguCheck);
                _app.MapPost("/api/smartstore/all-products", HandleAllProductsPage);
                _app.MapPost("/api/smartstore/product-data", HandleProductData);
                _app.MapPost("/api/smartstore/log", HandleExtensionLog);
                _app.MapPost("/api/smartstore/stop", HandleStopCrawling); // ⭐ 크롤링 중단 API 추가
                _app.MapPost("/api/smartstore/skip-store", HandleSkipStore); // ⭐ 스토어 스킵 API 추가
                _app.MapPost("/api/smartstore/image", HandleProductImage); // ⭐ 상품 이미지 처리 API 추가
                _app.MapPost("/api/smartstore/product-name", HandleProductName); // ⭐ 상품명 처리 API 추가
                _app.MapPost("/api/smartstore/product-price", HandleProductPrice); // ⭐ 가격 처리 API 추가
                _app.MapPost("/api/smartstore/reviews", HandleProductReviews); // ⭐ 리뷰 처리 API 추가
                _app.MapPost("/api/smartstore/product-done", HandleProductDone); // ⭐ 상품 처리 완료 신호 API
                _app.MapGet("/api/smartstore/product-done", HandleGetProductDone); // ⭐ 상품 처리 완료 확인 API
                _app.MapPost("/api/captcha/detected", HandleCaptchaDetected); // ⭐ CAPTCHA 감지 API 추가
                _app.MapPost("/api/smartstore/categories", HandleCategories); // ⭐ 카테고리 처리 API 추가
                _app.MapPost("/api/smartstore/product-categories", HandleProductCategories); // ⭐ 개별 상품 카테고리 처리 API 추가
                
                // ⭐ 상태 관리 API 추가
                _app.MapPost("/api/smartstore/state", HandleStoreState);
                _app.MapGet("/api/smartstore/status", HandleGetStatus); // ⭐ 상태 조회 API 추가
                _app.MapGet("/api/smartstore/state", HandleGetStoreState);
                _app.MapPost("/api/smartstore/progress", HandleStoreProgress);
                
                // ⭐ 크롤링 플래그 API 추가
                _app.MapGet("/api/crawling/allowed", HandleGetCrawlingAllowed);
                _app.MapPost("/api/crawling/allow", HandleAllowCrawling);
                _app.MapDelete("/api/crawling/allow", HandleResetCrawling);
                
                // ⭐ 가격 필터링 설정 API 추가
                _app.MapGet("/api/price-filter/settings", HandleGetPriceFilterSettings);
                _app.MapPost("/api/price-filter/settings", HandleSetPriceFilterSettings);
                
                // ⭐ 상품명 처리 API 추가
                _app.MapPost("/api/smartstore/product-names", HandleProductNames);
                _app.MapPost("/api/smartstore/set-current-product", HandleSetCurrentProduct); // ⭐ 현재 상품 ID 설정 API
                _app.MapGet("/api/smartstore/latest-keywords", HandleGetLatestKeywords);
                _app.MapPost("/api/smartstore/trigger-keywords", HandleTriggerKeywords);
                _app.MapPost("/api/smartstore/all-stores-completed", HandleAllStoresCompleted); // ⭐ 모든 스토어 완료 API 추가
                _app.MapGet("/api/smartstore/check-all-completed", HandleCheckAllCompleted); // ⭐ 완료 상태 체크 API 추가
                _app.MapGet("/api/smartstore/crawling-status", HandleGetCrawlingStatus); // ⭐ 크롤링 상태 확인 API 추가
                
                // ⭐ 서버 주도 크롤링 API (v2)
                _app.MapGet("/api/crawl/next-task", HandleCrawlNextTask);
                _app.MapPost("/api/crawl/report", HandleCrawlReport);
                _app.MapPost("/api/crawl/start", HandleCrawlStart);
                
                _app.MapPost("/api/taobao/upload-image", HandleTaobaoImageUpload); // ⭐ 타오바오 이미지 업로드 API
                _app.MapPost("/api/taobao/login", HandleTaobaoLogin); // ⭐ 타오바오 로그인 API
                _app.MapPost("/api/taobao/cookies", HandleTaobaoCookies); // ⭐ 타오바오 쿠키 수신 API
                _app.MapGet("/api/taobao/cookies", HandleGetTaobaoCookies); // ⭐ 타오바오 쿠키 상태 확인 API
                _app.MapPost("/api/taobao/image-search", HandleTaobaoImageSearch); // ⭐ 타오바오 이미지 검색 결과 수신
                _app.MapPost("/api/taobao/search-request", HandleTaobaoSearchRequest); // ⭐ 이미지 검색 요청
                _app.MapGet("/api/taobao/search-result", HandleTaobaoSearchResult); // ⭐ 검색 결과 조회
                _app.MapGet("/api/taobao/pending-search", HandlePendingSearch); // ⭐ 대기 중인 검색 요청 (확장프로그램용)
                _app.MapPost("/api/taobao/proxy-search", HandleTaobaoProxySearch); // ⭐ 프록시 기반 이미지 검색 (서버 측)
                _app.MapGet("/api/taobao/get-search-image", HandleGetSearchImage); // ⭐ 검색 이미지 데이터 조회 (content script용)
                _app.MapPost("/api/taobao/image-search-result", HandleImageSearchResult); // ⭐ 이미지 검색 결과 수신 (content script용)
                _app.MapPost("/api/google-lens/search", HandleGoogleLensSearch); // ⭐ 구글렌즈 타오바오 검색
                _app.MapPost("/api/imgur/upload", HandleImgurUpload); // ⭐ 이미지 업로드
                _app.MapGet("/temp-image/{fileName}", (HttpContext ctx, string fileName) => HandleTempImage(ctx, fileName)); // ⭐ 임시 이미지 서빙
                
                LogWindow.AddLogStatic("✅ API 엔드포인트 등록 완료 (30개)");

                // ⭐ 서버 변수 초기화
                lock (_counterLock)
                {
                    _productCount = 0;
                                        _totalAttempted = 0;
                    _shouldStop = false;
                    _completionPopupShown = false; // 팝업 플래그 초기화
                    _saveCompleted = false; // 저장 플래그 초기화
                }
                
                lock (_statesLock)
                {
                    _storeStates.Clear();
                }
                
                _selectedStores.Clear();
                _processedStores.Clear(); // 처리된 스토어 목록도 초기화
                // ⭐ _isCrawlingActive는 HandleAllowCrawling()에서 설정되므로 여기서는 건드리지 않음
                _currentStoreIndex = 0; // 순차 처리 인덱스 초기화
                LogWindow.AddLogStatic("✅ 서버 변수 초기화 완료");

                _isRunning = true;
                IsRunning = true;
                
                LogWindow.AddLogStatic("🌐 웹서버를 localhost:8080에서 시작합니다...");

                // 백그라운드에서 서버 실행
                _ = Task.Run(async () =>
                {
                    try
                    {
                        LogWindow.AddLogStatic("🔥🔥🔥 실제 서버 시작 중...");
                        await _app.RunAsync("http://localhost:8080");
                        LogWindow.AddLogStatic("🔥🔥🔥 서버 실행 완료!");
                    }
                    catch (Exception ex)
                    {
                        LogWindow.AddLogStatic($"❌ 웹서버 실행 오류: {ex.Message}");
                        LogWindow.AddLogStatic($"🔥 서버 오류 스택: {ex.StackTrace}");
                        _isRunning = false;
                    }
                });

                // 서버 시작 대기
                await Task.Delay(3000); // 3초로 늘림
                
                if (_isRunning)
                {
                    LogWindow.AddLogStatic("✅ 웹서버가 성공적으로 시작되었습니다!");
                    LogWindow.AddLogStatic("🔗 서버 주소: http://localhost:8080");
                    LogWindow.AddLogStatic("📡 Chrome 확장프로그램 연결 대기 중...");
                    
                    // 서버 테스트 요청
                    _ = Task.Run(async () =>
                    {
                        await Task.Delay(1000);
                        try
                        {
                            using var client = new HttpClient();
                            var testResponse = await client.GetAsync("http://localhost:8080/api/smartstore/status");
                            LogWindow.AddLogStatic($"🔥 서버 자체 테스트: {testResponse.StatusCode}");
                        }
                        catch (Exception testEx)
                        {
                            LogWindow.AddLogStatic($"🔥 서버 자체 테스트 실패: {testEx.Message}");
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                LogWindow.AddLogStatic($"❌ 웹서버 시작 오류: {ex.Message}");
                LogWindow.AddLogStatic($"❌ 오류 상세: {ex.StackTrace}");
                Debug.WriteLine($"웹서버 시작 오류: {ex.Message}");
                _isRunning = false;
            }
        }

        // 썸네일 저장 API
        private async Task<IResult> HandleSaveThumbnails(HttpContext context)
        {
            try
            {
                LogWindow.AddLogStatic("API 요청 수신: POST /api/thumbnails/save");

                using var reader = new StreamReader(context.Request.Body);
                var json = await reader.ReadToEndAsync();
                
                LogWindow.AddLogStatic($"수신된 데이터 크기: {json.Length} bytes");
                LogWindow.AddLogStatic($"JSON 내용: {json.Substring(0, Math.Min(500, json.Length))}");

                ThumbnailSaveRequest? requestData = null;
                try
                {
                    requestData = JsonSerializer.Deserialize<ThumbnailSaveRequest>(json);
                }
                catch (Exception jsonEx)
                {
                    LogWindow.AddLogStatic($"JSON 역직렬화 오류: {jsonEx.Message}");
                    return Results.BadRequest($"JSON parsing error: {jsonEx.Message}");
                }
                
                if (requestData?.Products == null)
                {
                    LogWindow.AddLogStatic("잘못된 요청 데이터");
                    return Results.BadRequest("Invalid request data");
                }

                LogWindow.AddLogStatic($"{requestData.Products.Count}개 썸네일 저장 시작...");

                int savedCount = 0;
                foreach (var product in requestData.Products)
                {
                    try
                    {
                        await _thumbnailService.SaveThumbnailAsync(
                            product.Id,
                            product.Title,
                            product.ThumbnailUrl,
                            product.Price,
                            product.Link
                        );
                        savedCount++;
                    }
                    catch (Exception ex)
                    {
                        LogWindow.AddLogStatic($"썸네일 저장 실패: {product.Title} - {ex.Message}");
                    }
                }

                LogWindow.AddLogStatic($"{savedCount}개 썸네일 저장 완료");

                var response = new { 
                    success = true,
                    savedCount = savedCount, 
                    totalCount = requestData.Products.Count,
                    message = $"{savedCount}개 썸네일 저장 완료"
                };
                
                return Results.Json(response, new JsonSerializerOptions 
                { 
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase 
                });
            }
            catch (Exception ex)
            {
                LogWindow.AddLogStatic($"API 처리 오류: {ex.Message}");
                return Results.Json(new { 
                    success = false, 
                    error = ex.Message 
                }, statusCode: 500);
            }
        }

        // 썸네일 목록 조회 API
        private async Task<IResult> HandleGetThumbnails(HttpContext context)
        {
            try
            {
                LogWindow.AddLogStatic("API 요청 수신: GET /api/thumbnails/list");
                
                var thumbnails = await _thumbnailService.GetThumbnailsAsync();
                return Results.Ok(thumbnails);
            }
            catch (Exception ex)
            {
                LogWindow.AddLogStatic($"API 처리 오류: {ex.Message}");
                return Results.StatusCode(500);
            }
        }

        // 스마트스토어 링크 수집 API
        private async Task<IResult> HandleSmartStoreLinks(HttpContext context)
        {
            // ⭐ 프로그램에서 소싱 버튼 눌렀을 때만 크롤링 허용
            if (!_crawlingAllowed)
            {
                LogWindow.AddLogStatic("🚫 크롤링 미허용 상태 - 링크 수신 무시");
                return Results.Json(new { success = false, message = "크롤링이 허용되지 않았습니다." });
            }
            
            // ⭐ 새 크롤링 세션 시작 - 이전 세션 상태 리셋
            _shouldStop = false;
            _completionPopupShown = false;
            _saveCompleted = false;
            _productCount = 0;
            _totalAttempted = 0;
            _sessionStartFileCount = GetRawFileCount();
            
            LogWindow.AddLogStatic("🔥🔥🔥 HandleSmartStoreLinks 메서드 진입!");
            LogWindow.AddLogStatic($"🔥 요청 메서드: {context.Request.Method}");
            LogWindow.AddLogStatic($"🔥 요청 경로: {context.Request.Path}");
            
            try
            {
                LogWindow.AddLogStatic("🔄 API 요청 수신: POST /api/smartstore/links");

                using var reader = new StreamReader(context.Request.Body);
                var json = await reader.ReadToEndAsync();
                
                LogWindow.AddLogStatic($"📊 수신된 데이터 크기: {json.Length} bytes");
                
                if (string.IsNullOrWhiteSpace(json))
                {
                    LogWindow.AddLogStatic("❌ 빈 JSON 데이터 수신");
                    var errorResponse = Results.Json(new { 
                        success = false, 
                        error = "Empty JSON data received" 
                    }, statusCode: 400);
                    LogWindow.AddLogStatic("🔥 빈 JSON 오류 응답 반환");
                    return errorResponse;
                }

                LogWindow.AddLogStatic($"📝 JSON 내용 미리보기: {json.Substring(0, Math.Min(300, json.Length))}...");

                SmartStoreLinkRequest? requestData = null;
                try
                {
                    requestData = JsonSerializer.Deserialize<SmartStoreLinkRequest>(json);
                    LogWindow.AddLogStatic("✅ JSON 역직렬화 성공");
                }
                catch (Exception jsonEx)
                {
                    LogWindow.AddLogStatic($"❌ JSON 역직렬화 오류: {jsonEx.Message}");
                    return Results.Json(new { 
                        success = false, 
                        error = $"JSON parsing error: {jsonEx.Message}" 
                    }, statusCode: 400);
                }
                
                if (requestData?.SmartStoreLinks == null || requestData.SmartStoreLinks.Count == 0)
                {
                    LogWindow.AddLogStatic("❌ 잘못된 요청 데이터 또는 빈 스토어 목록");
                    return Results.Json(new { 
                        success = false, 
                        error = "Invalid request data or empty store list" 
                    }, statusCode: 400);
                }

                LogWindow.AddLogStatic($"📦 {requestData.SmartStoreLinks.Count}개 스마트스토어 링크 수신");

                // ⭐ 진짜 랜덤 선택 (Guid 기반)
                _selectedStores = requestData.SmartStoreLinks
                    .OrderBy(x => Guid.NewGuid())
                    .Take(MAX_STORES_TO_VISIT)
                    .ToList();
                
                LogWindow.AddLogStatic($"🎲 랜덤 선택 완료: {DateTime.Now:HH:mm:ss.fff}");
                
                // ⭐ 선택된 스토어 검증
                if (_selectedStores == null || _selectedStores.Count == 0)
                {
                    LogWindow.AddLogStatic("❌ 스토어 선택 실패 - 빈 목록");
                    return Results.Json(new { 
                        success = false, 
                        error = "No stores selected" 
                    }, statusCode: 400);
                }
                
                // 상품 카운터 초기화
                lock (_counterLock)
                {
                    _productCount = 0;
                                        _totalAttempted = 0;
                    _shouldStop = false;
                    _processedStores.Clear(); // ⭐ 처리된 스토어 목록도 초기화
                    _processedProducts.Clear(); // ⭐ 처리된 상품 목록도 초기화
                    LogWindow.AddLogStatic($"🔄 상품 카운터 초기화: 0/{TARGET_PRODUCT_COUNT}개");
                }

                LogWindow.AddLogStatic($"🎯 랜덤으로 선택된 {_selectedStores.Count}개 스토어:");
                foreach (var store in _selectedStores)
                {
                    LogWindow.AddLogStatic($"  - {store.Title}: {store.Url}");
                }

                LogWindow.AddLogStatic($"🎯 목표: {TARGET_PRODUCT_COUNT}개 상품 수집");

                // ⭐ 응답 데이터 생성 (확실한 구조)
                var selectedStoresList = new List<object>();
                
                foreach (var store in _selectedStores)
                {
                    // ⭐ URL에서 정확한 스토어 ID 추출
                    var url = store.Url ?? "";
                    var storeId = "";
                    
                    if (!string.IsNullOrEmpty(url) && url.Contains("smartstore.naver.com/"))
                    {
                        var decoded = Uri.UnescapeDataString(url);
                        // ⭐ inflow URL에서 실제 스토어 ID 추출
                        if (decoded.Contains("inflow/outlink/url?url="))
                        {
                            var innerUrlMatch = System.Text.RegularExpressions.Regex.Match(decoded, @"url=([^&]+)");
                            if (innerUrlMatch.Success)
                            {
                                var innerUrl = Uri.UnescapeDataString(innerUrlMatch.Groups[1].Value);
                                var storeMatch = System.Text.RegularExpressions.Regex.Match(innerUrl, @"smartstore\.naver\.com/([^/&?]+)");
                                if (storeMatch.Success)
                                {
                                    storeId = storeMatch.Groups[1].Value;
                                }
                            }
                        }
                        else
                        {
                            // 일반 smartstore URL
                            var match = System.Text.RegularExpressions.Regex.Match(decoded, @"smartstore\.naver\.com/([^/&?]+)");
                            if (match.Success)
                            {
                                storeId = match.Groups[1].Value;
                            }
                        }
                    }
                    
                    LogWindow.AddLogStatic($"🔍 URL 파싱: {url} -> {storeId}");
                    
                    selectedStoresList.Add(new {
                        title = store.Title ?? "제목없음",
                        url = store.Url ?? "",
                        storeId = storeId ?? "unknown"
                    });
                }

                // ⭐ 응답 데이터 검증
                if (selectedStoresList.Count == 0)
                {
                    LogWindow.AddLogStatic("❌ 선택된 스토어 목록이 비어있음");
                    return Results.Json(new { 
                        success = false, 
                        error = "Selected stores list is empty" 
                    }, statusCode: 400);
                }

                var response = new { 
                    success = true,
                    totalLinks = requestData.SmartStoreLinks.Count,
                    selectedLinks = _selectedStores.Count,
                    targetProducts = TARGET_PRODUCT_COUNT,
                    selectedStores = selectedStoresList,
                    message = $"{requestData.SmartStoreLinks.Count}개 중 {_selectedStores.Count}개 스토어 선택 완료",
                    timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                };
                
                LogWindow.AddLogStatic($"📡 응답 데이터 생성 완료: {selectedStoresList.Count}개 스토어");
                
                // ⭐ 직접 응답 작성 (Results.Json 대신)
                var jsonString = System.Text.Json.JsonSerializer.Serialize(response, new JsonSerializerOptions 
                { 
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    WriteIndented = false
                });
                
                context.Response.ContentType = "application/json; charset=utf-8";
                context.Response.StatusCode = 200;
                await context.Response.WriteAsync(jsonString);
                
                LogWindow.AddLogStatic("✅ JSON 응답 직접 작성 완료");
                LogWindow.AddLogStatic($"🔥🔥🔥 실제 응답 반환: {jsonString}");
                
                return Results.Ok();
            }
            catch (Exception ex)
            {
                LogWindow.AddLogStatic($"❌ API 처리 오류: {ex.Message}");
                LogWindow.AddLogStatic($"🔥 오류 스택: {ex.StackTrace}");
                
                var errorJson = System.Text.Json.JsonSerializer.Serialize(new { 
                    success = false, 
                    error = ex.Message ?? "Unknown error" 
                });
                
                context.Response.ContentType = "application/json; charset=utf-8";
                context.Response.StatusCode = 500;
                await context.Response.WriteAsync(errorJson);
                
                LogWindow.AddLogStatic("🔥 오류 응답 직접 작성 완료");
                return Results.Ok();
            }
        }

        // 스마트스토어 링크 방문 알림 API
        private async Task<IResult> HandleSmartStoreVisit(HttpContext context)
        {
            try
            {
                // ⭐ 크롤링 중단 체크 추가
                if (_shouldStop || !_isCrawlingActive)
                {
                    LogWindow.AddLogStatic($"🛑 크롤링 중단됨 - 방문 요청 무시");
                    return Results.Json(new { success = false, message = "Crawling stopped" });
                }

                using var reader = new StreamReader(context.Request.Body);
                var json = await reader.ReadToEndAsync();
                
                SmartStoreVisitRequest? visitData = null;
                try
                {
                    visitData = JsonSerializer.Deserialize<SmartStoreVisitRequest>(json);
                }
                catch (Exception jsonEx)
                {
                    LogWindow.AddLogStatic($"❌ 방문 데이터 JSON 파싱 오류: {jsonEx.Message}");
                    return Results.BadRequest(new { error = "Invalid JSON format" });
                }

                if (visitData == null)
                {
                    return Results.BadRequest(new { error = "Invalid visit data" });
                }

                // ⭐ 먼저 visiting 상태 체크 - 다른 스토어가 상품 처리 중이면 모든 요청 차단
                lock (_statesLock)
                {
                    LogWindow.AddLogStatic($"🔍 visiting 상태 체크 시작 - 총 {_storeStates.Count}개 상태");
                    foreach (var kvp in _storeStates)
                    {
                        var key = kvp.Key;
                        var state = kvp.Value;
                        LogWindow.AddLogStatic($"🔍 상태 체크: {key} -> {state.State} (Lock: {state.Lock})");
                        
                        // ⭐ visiting 상태이고 Lock이 true인 스토어가 있으면 차단
                        if (state.State == "visiting" && state.Lock)
                        {
                            // 키에서 스토어 ID 추출 (storeId:runId 형태)
                            var keyStoreId = key.Split(':')[0];
                            LogWindow.AddLogStatic($"🚫 {keyStoreId} 스토어가 상품 처리 중 - {visitData.StoreId} 요청 차단");
                            return Results.Ok(new { success = false, message = "다른 스토어 처리 중" });
                        }
                    }
                    LogWindow.AddLogStatic($"🔍 visiting 상태 체크 완료 - 차단 없음");
                }
                
                // ⭐ 순차 처리 - 현재 처리할 스토어인지 확인
                lock (_storeProcessLock)
                {
                    // ⭐ 100% 확실한 중단 체크 - 차단 감지 시 더 이상 진행하지 않음
                    if (_shouldStop)
                    {
                        LogWindow.AddLogStatic($"🛑 크롤링 중단됨 - {visitData.StoreId} 방문 요청 무시");
                        return Results.Ok(new { success = false, message = "크롤링 중단됨" });
                    }
                    
                    LogWindow.AddLogStatic($"🔥🔥🔥 방문 API 디버깅 시작 - 요청 스토어: {visitData.StoreId}");
                    LogWindow.AddLogStatic($"🔥 현재 인덱스: {_currentStoreIndex}, 전체 스토어 수: {_selectedStores.Count}");
                    
                    if (_currentStoreIndex >= _selectedStores.Count)
                    {
                        LogWindow.AddLogStatic($"모든 스토어 처리 완료 - 요청 무시: {visitData.StoreId}");
                        
                        // ⭐ 플래그 리셋 후 크롤링 완료 시 팝업창 표시
                        _completionPopupShown = false; // 플래그 리셋
                        var finalCount = GetCurrentProductCount();
                        ShowCrawlingResultPopup(finalCount, "모든 스토어 처리 완료");
                        
                        // ⭐ 크롬 탭 자동 닫기 제거 (테스트용)
                        // _ = Task.Run(() => CloseAllChromeTabs());
                        
                        return Results.Ok(new { success = false, message = "모든 스토어 처리 완료" });
                    }
                    
                    var currentStore = _selectedStores[_currentStoreIndex];
                    LogWindow.AddLogStatic($"🔥 현재 스토어 URL: {currentStore.Url}");
                    LogWindow.AddLogStatic($"🔥 현재 스토어 제목: {currentStore.Title}");
                    
                    var currentStoreId = UrlExtensions.ExtractStoreIdFromUrl(currentStore.Url);
                    LogWindow.AddLogStatic($"🔥🔥🔥 추출된 현재 스토어 ID: '{currentStoreId}'");
                    LogWindow.AddLogStatic($"🔥🔥🔥 요청된 스토어 ID: '{visitData.StoreId}'");
                    
                    if (!visitData.StoreId.Equals(currentStoreId, StringComparison.OrdinalIgnoreCase))
                    {
                        LogWindow.AddLogStatic($"순차 처리 위반 - 현재 처리할 스토어: {currentStoreId}, 요청 스토어: {visitData.StoreId}");
                        
                        // ⭐ 현재 스토어 인덱스 강제 업데이트
                        for (int i = 0; i < _selectedStores.Count; i++)
                        {
                            if (_selectedStores[i].StoreId.Equals(visitData.StoreId, StringComparison.OrdinalIgnoreCase))
                            {
                                _currentStoreIndex = i;
                                LogWindow.AddLogStatic($"🔄 스토어 인덱스 강제 업데이트: {_currentStoreIndex}/{_selectedStores.Count}");
                                break;
                            }
                        }
                        
                        // ⭐ 이전 스토어들 모두 완료 처리
                        for (int i = 0; i < _currentStoreIndex; i++)
                        {
                            var prevStoreId = _selectedStores[i].StoreId;
                            if (_storeStates.ContainsKey(prevStoreId) && _storeStates[prevStoreId].Status != "done")
                            {
                                _storeStates[prevStoreId] = new StoreState 
                                { 
                                    Status = "done", 
                                    IsLocked = false, 
                                    ProductCount = _storeStates[prevStoreId].ProductCount,
                                    UpdatedAt = DateTime.Now
                                };
                                LogWindow.AddLogStatic($"✅ {prevStoreId}: 이전 스토어 자동 완료 처리");
                            }
                        }
                    }
                    
                    LogWindow.AddLogStatic($"✅ 순차 처리 승인: {visitData.StoreId} ({_currentStoreIndex + 1}/{_selectedStores.Count})");
                }

                // ⭐ 목표 달성 시 완전 중단 - 새로운 방문 차단
                lock (_counterLock)
                {
                    if (_productCount >= TARGET_PRODUCT_COUNT)
                    {
                        LogWindow.AddLogStatic($"목표 달성으로 크롤링 중단: {_productCount}/{TARGET_PRODUCT_COUNT}");
                        
                        // 모든 스토어를 강제로 완료 상태로 변경
                        foreach (var store in _storeStates.Keys.ToList())
                        {
                            if (_storeStates[store].State != "done")
                            {
                                _storeStates[store].State = "done";
                                _storeStates[store].Lock = false;
                                LogWindow.AddLogStatic($"🛑 {store}: 강제 완료 처리 (목표 달성)");
                            }
                        }
                        
                        _shouldStop = true;
                        _isCrawlingActive = false;
                        
                        // ⭐ 파일 기반 JSON 저장
                        SaveProductCardsFromFiles();
                        
                        return Results.Ok(new { 
                            success = true, 
                            stop = true,
                            totalProducts = _productCount,
                            message = "Target reached, stopping crawl" 
                        });
                    }
                }

                LogWindow.AddLogStatic($"[{visitData.CurrentIndex}/{visitData.TotalCount}] 스마트스토어 공구탭 접속: {visitData.Title}");
                LogWindow.AddLogStatic($"현재 상품 수: {_productCount}/{TARGET_PRODUCT_COUNT}");
                _lastCrawlingActivity = DateTime.Now;

                var response = new { 
                    success = true,
                    currentProducts = _productCount,
                    targetProducts = TARGET_PRODUCT_COUNT,
                    message = "Visit logged successfully" 
                };
                
                var responseJson = JsonSerializer.Serialize(response);
                LogWindow.AddLogStatic($"🔥 HandleSmartStoreVisit 응답: {responseJson}");
                
                return Results.Json(response);
            }
            catch (Exception ex)
            {
                LogWindow.AddLogStatic($"방문 상태 처리 오류: {ex.Message}");
                return Results.Json(new { 
                    success = false, 
                    error = ex.Message 
                }, statusCode: 500);
            }
        }

        // 공구 개수 확인 결과 API
        private async Task<IResult> HandleGongguCheck(HttpContext context)
        {
            try
            {
                // ⭐ 크롤링 중단 체크 추가
                if (_shouldStop || !_isCrawlingActive)
                {
                    LogWindow.AddLogStatic($"🛑 크롤링 중단됨 - 공구체크 요청 무시");
                    return Results.Json(new { success = false, message = "Crawling stopped" });
                }

                // ⭐ 먼저 visiting 상태 체크 - 다른 스토어가 상품 처리 중이면 모든 요청 차단
                lock (_statesLock)
                {
                    LogWindow.AddLogStatic($"🔍 [공구체크] visiting 상태 체크 시작 - 총 {_storeStates.Count}개 상태");
                    foreach (var kvp in _storeStates)
                    {
                        var key = kvp.Key;
                        var state = kvp.Value;
                        LogWindow.AddLogStatic($"🔍 [공구체크] 상태 체크: {key} -> {state.State} (Lock: {state.Lock})");
                        
                        // ⭐ visiting 상태이고 Lock이 true인 스토어가 있으면 차단
                        if (state.State == "visiting" && state.Lock)
                        {
                            // 키에서 스토어 ID 추출 (storeId:runId 형태)
                            var keyStoreId = key.Split(':')[0];
                            LogWindow.AddLogStatic($"🚫 [공구체크] {keyStoreId} 스토어가 상품 처리 중 - 요청 차단");
                            return Results.Json(new { success = false, message = "다른 스토어 처리 중" });
                        }
                    }
                    LogWindow.AddLogStatic($"🔍 [공구체크] visiting 상태 체크 완료 - 차단 없음");
                }
                
                using var reader = new StreamReader(context.Request.Body);
                var json = await reader.ReadToEndAsync();
                
                GongguCheckRequest? gongguData = null;
                try
                {
                    gongguData = JsonSerializer.Deserialize<GongguCheckRequest>(json);
                }
                catch (Exception jsonEx)
                {
                    LogWindow.AddLogStatic($"❌ 공구 데이터 JSON 파싱 오류: {jsonEx.Message}");
                    return Results.Json(new { 
                        success = false, 
                        error = "Invalid JSON format" 
                    }, statusCode: 400);
                }
                
                if (gongguData != null)
                {
                    // ⭐ 순차 처리 체크 - 현재 차례가 아니면 즉시 차단
                    lock (_storeProcessLock)
                    {
                        // ⭐ 100% 확실한 중단 체크 - 차단 감지 시 더 이상 진행하지 않음
                        if (_shouldStop)
                        {
                            LogWindow.AddLogStatic($"🛑 크롤링 중단됨 - {gongguData.StoreId} 공구체크 요청 무시");
                            return Results.Json(new { 
                                success = false, 
                                message = "크롤링 중단됨" 
                            });
                        }
                        
                        if (_currentStoreIndex >= _selectedStores.Count)
                        {
                            LogWindow.AddLogStatic($"❌ 모든 스토어 처리 완료 - {gongguData.StoreId} 차단");
                            return Results.Json(new { 
                                success = false, 
                                message = "크롤링 완료됨" 
                            });
                        }
                        
                        var currentStore = _selectedStores[_currentStoreIndex];
                        LogWindow.AddLogStatic($"🔍 디버그 - 현재 인덱스: {_currentStoreIndex}, 스토어 URL: {currentStore.Url}");
                        
                        var currentStoreId = UrlExtensions.ExtractStoreIdFromUrl(currentStore.Url);
                        LogWindow.AddLogStatic($"🔍 디버그 - 추출된 스토어 ID: '{currentStoreId}'");
                        
                        if (!gongguData.StoreId.Equals(currentStoreId, StringComparison.OrdinalIgnoreCase))
                        {
                            LogWindow.AddLogStatic($"❌ 순차 처리 위반 - 현재: {currentStoreId}, 요청: {gongguData.StoreId} - 인덱스 강제 업데이트");

                            // ⭐ 현재 스토어 인덱스 강제 업데이트 (방문 API와 동일)
                            for (int i = 0; i < _selectedStores.Count; i++)
                            {
                                if (_selectedStores[i].StoreId.Equals(gongguData.StoreId, StringComparison.OrdinalIgnoreCase))
                                {
                                    _currentStoreIndex = i;
                                    LogWindow.AddLogStatic($"🔄 [공구체크] 스토어 인덱스 강제 업데이트: {_currentStoreIndex}/{_selectedStores.Count}");
                                    break;
                                }
                            }

                            // ⭐ 이전 스토어들 모두 완료 처리
                            for (int i = 0; i < _currentStoreIndex; i++)
                            {
                                var prevStoreId = UrlExtensions.ExtractStoreIdFromUrl(_selectedStores[i].Url);
                                lock (_statesLock)
                                {
                                    var keys = _storeStates.Keys.Where(k => k.StartsWith(prevStoreId + ":")).ToList();
                                    foreach (var key in keys)
                                    {
                                        if (_storeStates[key].State != "done")
                                        {
                                            _storeStates[key].State = "done";
                                            _storeStates[key].Lock = false;
                                            LogWindow.AddLogStatic($"🔄 [공구체크] {prevStoreId} 강제 완료 처리");
                                        }
                                    }
                                }
                            }
                        }
                    }
                    
                    if (gongguData.IsValid)
                    {
                        LogWindow.AddLogStatic($"{gongguData.StoreId}: 공구 {gongguData.GongguCount}개 (≥1000개) - 진행");
                    }
                    else
                    {
                        LogWindow.AddLogStatic($"{gongguData.StoreId}: 공구 {gongguData.GongguCount}개 (<1000개) - 스킵");
                        
                        // ⭐ 스킵 시 즉시 done 상태로 변경
                        lock (_statesLock)
                        {
                            var key = $"{gongguData.StoreId}:unknown";
                            if (_storeStates.ContainsKey(key))
                            {
                                _storeStates[key].State = "done";
                                _storeStates[key].Lock = false;
                                _storeStates[key].UpdatedAt = DateTime.Now;
                                LogWindow.AddLogStatic($"🔄 {gongguData.StoreId}: 스킵으로 인한 강제 done 상태 설정");
                            }
                        }
                        
                        // ⭐ 다음 스토어로 이동
                        lock (_storeProcessLock)
                        {
                            // 먼저 인덱스 증가
                            _currentStoreIndex++;
                            var totalStores = _selectedStores?.Count ?? 10;
                            LogWindow.AddLogStatic($"📈 다음 스토어로 이동: {_currentStoreIndex}/{totalStores}");

                            // 🛑 모든 스토어 완료 체크 (실제 스토어 개수와 비교)
                            if (_currentStoreIndex >= totalStores)
                            {
                                LogWindow.AddLogStatic($"🎉 {totalStores}개 스토어 모두 완료 - 크롤링 중단");
                                _shouldStop = true;
                                _isCrawlingActive = false;

                                // ⭐ 크롤링 완료 처리
                                if (!_completionPopupShown)
                                {
                                    var finalCount = GetCurrentProductCount();
                                    ShowCrawlingResultPopup(finalCount, $"{totalStores}개 스토어 모두 완료");
                                }
                                return Results.Json(new { success = true, completed = true });
                            }
                        }
                    }
                }

                _lastCrawlingActivity = DateTime.Now;
                return Results.Json(new { 
                    success = true,
                    message = "공구 개수 확인 완료"
                });
            }
            catch (Exception ex)
            {
                LogWindow.AddLogStatic($"공구 개수 확인 오류: {ex.Message}");
                
                // 안전한 오류 응답
                context.Response.ContentType = "application/json; charset=utf-8";
                context.Response.StatusCode = 500;
                await context.Response.WriteAsync(JsonSerializer.Serialize(new { 
                    success = false, 
                    error = ex.Message 
                }));
                
                return Results.Ok();
            }
        }

        // 전체상품 페이지 접속 알림 API
        private async Task<IResult> HandleAllProductsPage(HttpContext context)
        {
            try
            {
                // ⭐ 크롤링 중단 체크 추가
                if (_shouldStop || !_isCrawlingActive)
                {
                    LogWindow.AddLogStatic($"🛑 크롤링 중단됨 - 전체상품 요청 무시");
                    return Results.Json(new { success = false, message = "Crawling stopped" });
                }

                // ⭐ 먼저 visiting 상태 체크 - 다른 스토어가 상품 처리 중이면 모든 요청 차단
                lock (_statesLock)
                {
                    LogWindow.AddLogStatic($"🔍 [전체상품] visiting 상태 체크 시작 - 총 {_storeStates.Count}개 상태");
                    foreach (var kvp in _storeStates)
                    {
                        var key = kvp.Key;
                        var state = kvp.Value;
                        LogWindow.AddLogStatic($"🔍 [전체상품] 상태 체크: {key} -> {state.State} (Lock: {state.Lock})");
                        
                        // ⭐ visiting 상태이고 Lock이 true인 스토어가 있으면 차단
                        if (state.State == "visiting" && state.Lock)
                        {
                            // 키에서 스토어 ID 추출 (storeId:runId 형태)
                            var keyStoreId = key.Split(':')[0];
                            LogWindow.AddLogStatic($"🚫 [전체상품] {keyStoreId} 스토어가 상품 처리 중 - 요청 차단");
                            return Results.Json(new { success = false, message = "다른 스토어 처리 중" });
                        }
                    }
                    LogWindow.AddLogStatic($"🔍 [전체상품] visiting 상태 체크 완료 - 차단 없음");
                }
                
                using var reader = new StreamReader(context.Request.Body);
                var json = await reader.ReadToEndAsync();
                
                AllProductsPageRequest? pageData = null;
                try
                {
                    pageData = JsonSerializer.Deserialize<AllProductsPageRequest>(json);
                }
                catch (Exception jsonEx)
                {
                    LogWindow.AddLogStatic($"❌ 페이지 데이터 JSON 파싱 오류: {jsonEx.Message}");
                    return Results.Json(new { 
                        success = false, 
                        error = "Invalid JSON format" 
                    }, statusCode: 400);
                }
                
                if (pageData != null)
                {
                    // ⭐ 순차 처리 체크 - 현재 차례가 아니면 즉시 차단
                    lock (_storeProcessLock)
                    {
                        // ⭐ 100% 확실한 중단 체크 - 차단 감지 시 더 이상 진행하지 않음
                        if (_shouldStop)
                        {
                            LogWindow.AddLogStatic($"🛑 크롤링 중단됨 - {pageData.StoreId} 전체상품 요청 무시");
                            return Results.Json(new { 
                                success = false, 
                                message = "크롤링 중단됨" 
                            });
                        }
                        
                        if (_currentStoreIndex >= _selectedStores.Count)
                        {
                            LogWindow.AddLogStatic($"❌ 모든 스토어 처리 완료 - {pageData.StoreId} 차단");
                            return Results.Json(new { 
                                success = false, 
                                message = "크롤링 완료됨" 
                            });
                        }
                        
                        var currentStore = _selectedStores[_currentStoreIndex];
                        var currentStoreId = UrlExtensions.ExtractStoreIdFromUrl(currentStore.Url);
                        
                        if (!pageData.StoreId.Equals(currentStoreId, StringComparison.OrdinalIgnoreCase))
                        {
                            LogWindow.AddLogStatic($"❌ 순차 처리 위반 - 현재: {currentStoreId}, 요청: {pageData.StoreId} - 인덱스 강제 업데이트");

                            // ⭐ 현재 스토어 인덱스 강제 업데이트 (방문 API와 동일)
                            for (int i = 0; i < _selectedStores.Count; i++)
                            {
                                if (_selectedStores[i].StoreId.Equals(pageData.StoreId, StringComparison.OrdinalIgnoreCase))
                                {
                                    _currentStoreIndex = i;
                                    LogWindow.AddLogStatic($"🔄 [전체상품] 스토어 인덱스 강제 업데이트: {_currentStoreIndex}/{_selectedStores.Count}");
                                    break;
                                }
                            }

                            // ⭐ 이전 스토어들 모두 완료 처리
                            for (int i = 0; i < _currentStoreIndex; i++)
                            {
                                var prevStoreId = UrlExtensions.ExtractStoreIdFromUrl(_selectedStores[i].Url);
                                lock (_statesLock)
                                {
                                    var keys = _storeStates.Keys.Where(k => k.StartsWith(prevStoreId + ":")).ToList();
                                    foreach (var key in keys)
                                    {
                                        if (_storeStates[key].State != "done")
                                        {
                                            _storeStates[key].State = "done";
                                            _storeStates[key].Lock = false;
                                            LogWindow.AddLogStatic($"🔄 [전체상품] {prevStoreId} 강제 완료 처리");
                                        }
                                    }
                                }
                            }
                        }
                    }
                    
                    LogWindow.AddLogStatic($"{pageData.StoreId}: 전체상품 페이지 접속 완료");
                    LogWindow.AddLogStatic($"  URL: {pageData.PageUrl}");
                }

                return Results.Json(new { 
                    success = true,
                    message = "전체상품 페이지 접속 확인"
                });
            }
            catch (Exception ex)
            {
                LogWindow.AddLogStatic($"전체상품 페이지 처리 오류: {ex.Message}");
                
                // 안전한 오류 응답
                context.Response.ContentType = "application/json; charset=utf-8";
                context.Response.StatusCode = 500;
                await context.Response.WriteAsync(JsonSerializer.Serialize(new { 
                    success = false, 
                    error = ex.Message 
                }));
                
                return Results.Ok();
            }
        }

        // 상품 데이터 수집 결과 API
        private async Task<IResult> HandleProductData(HttpContext context)
        {
            try
            {
                _lastCrawlingActivity = DateTime.Now;
                LogWindow.AddLogStatic("🔥 HandleProductData 메서드 진입!");
                
                using var reader = new StreamReader(context.Request.Body);
                var json = await reader.ReadToEndAsync();
                
                LogWindow.AddLogStatic($"🔥 수신된 JSON 길이: {json.Length}");
                
                ProductDataRequest? productData = null;
                try
                {
                    productData = JsonSerializer.Deserialize<ProductDataRequest>(json);
                    LogWindow.AddLogStatic("🔥 JSON 파싱 성공");
                }
                catch (Exception jsonEx)
                {
                    LogWindow.AddLogStatic($"❌ 상품 데이터 JSON 파싱 오류: {jsonEx.Message}");
                    return Results.Json(new { 
                        success = false, 
                        error = "Invalid JSON format" 
                    }, statusCode: 400);
                }
                
                if (productData != null)
                {
                    LogWindow.AddLogStatic($"📊 {productData.StoreId}: {productData.ProductCount}개 상품 데이터 수신");
                    
                    // ⭐ 100개 달성 체크 (HandleProductName에서 카운터 증가)
                    if (_productCount >= 100)
                    {
                        LogWindow.AddLogStatic("🎉 목표 달성! 100개 상품 수집 완료 - 크롤링 중단");

                        // ⭐ 크롤링 완전 중단 신호 설정
                        _shouldStop = true;
                        _isCrawlingActive = false;

                        LogWindow.AddLogStatic($"🛑 크롤링 중단 플래그 설정: _shouldStop = {_shouldStop}");

                        // ⭐ 1차 자동 저장 (목표 달성 직후) - 파일 기반으로 직접 저장!
                        _ = Task.Run(() =>
                        {
                            try
                            {
                                LogWindow.AddLogStatic("💾 [목표 달성] 1차 자동 저장 시작...");
                                SaveProductCardsFromFiles();
                                LogWindow.AddLogStatic("✅ [목표 달성] 1차 자동 저장 완료!");
                            }
                            catch (Exception ex)
                            {
                                LogWindow.AddLogStatic($"❌ 1차 자동 저장 실패: {ex.Message}");
                            }
                        });
                        
                        // ⭐ 모든 스토어를 done 상태로 변경하여 Chrome 중단
                        lock (_statesLock)
                        {
                            foreach (var storeId in _storeStates.Keys.ToList())
                            {
                                var state = _storeStates[storeId];
                                if (state.State != "done")
                                {
                                    state.State = "done";
                                    state.Lock = false;
                                    LogWindow.AddLogStatic($"🛑 {storeId}: 강제 완료 처리 (목표 달성)");
                                }
                            }
                        }
                        
                        // ⭐ 이미 팝업이 표시되었으면 중복 실행 방지
                        if (!_completionPopupShown)
                        {
                            // 🔄 로딩창 숨김
                            LoadingHelper.HideLoadingFromSourcingPage();
                            
                            // ⭐ Chrome 앱 창들 닫기
                            _ = Task.Run(async () => await CloseAllChromeApps());
                            
                            // ⭐ 팝업창으로 최종 결과 표시
                            ShowCrawlingResultPopup(100, "목표 달성");
                        }
                        
                        return Results.Json(new { 
                            success = true,
                            totalProducts = 100,
                            targetProducts = TARGET_PRODUCT_COUNT,
                            shouldStop = true,
                            message = "목표 달성으로 크롤링 완료"
                        });
                    }
                    
                    // ⭐ 상품 카운터 업데이트 (실제 수집된 상품 수 반영)
                    // 주의: HandleProductName에서도 카운터가 증가하므로 여기서는 증가하지 않음
                    LogWindow.AddLogStatic($"📊 {productData.StoreId}: {productData.ProductCount}개 상품 데이터 수신");
                    
                    // ⭐ 정상 완료 시 다음 스토어로 이동
                    lock (_storeProcessLock)
                    {
                        // 먼저 인덱스 증가
                        _currentStoreIndex++;
                        var totalStores = _selectedStores?.Count ?? 10;
                        LogWindow.AddLogStatic($"📈 다음 스토어로 이동: {_currentStoreIndex}/{totalStores}");

                        // 🛑 모든 스토어 완료 체크
                        if (_currentStoreIndex >= totalStores)
                        {
                            LogWindow.AddLogStatic($"🎉 {totalStores}개 스토어 모두 완료 - 크롤링 중단");
                            _shouldStop = true;
                            _isCrawlingActive = false;

                            SaveProductCardsFromFiles();

                            if (!_completionPopupShown)
                            {
                                var finalCount = GetCurrentProductCount();
                                ShowCrawlingResultPopup(finalCount, $"{totalStores}개 스토어 모두 완료");
                            }

                            var currentCount = GetCurrentProductCount();
                            return Results.Json(new {
                                success = true,
                                currentProducts = currentCount,
                                totalProducts = currentCount,
                                targetProducts = TARGET_PRODUCT_COUNT,
                                shouldStop = true,
                                message = $"{totalStores}개 스토어 모두 완료"
                            });
                        }

                        // 🚀 다음 스토어 자동 방문 시작
                        if (_currentStoreIndex < totalStores && !_shouldStop)
                        {
                            var nextStore = _selectedStores[_currentStoreIndex];
                            var nextStoreId = UrlExtensions.ExtractStoreIdFromUrl(nextStore.Url);
                            LogWindow.AddLogStatic($"🚀 다음 스토어 자동 방문 시작: {nextStoreId} ({_currentStoreIndex + 1}/{_selectedStores.Count})");

                            // Chrome 확장프로그램에 다음 스토어 방문 요청
                            _ = Task.Run(async () =>
                            {
                                await Task.Delay(2000); // 2초 대기 후 다음 스토어 방문
                                try
                                {
                                    using var client = new HttpClient();
                                    var visitRequest = new { storeId = nextStoreId, url = nextStore.Url };
                                    var json = JsonSerializer.Serialize(visitRequest);
                                    var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
                                    await client.PostAsync("http://localhost:8080/api/smartstore/visit", content);
                                }
                                catch (Exception ex)
                                {
                                    LogWindow.AddLogStatic($"❌ 다음 스토어 자동 방문 실패: {ex.Message}");
                                }
                            });
                        }
                    }
                }

                return Results.Json(new { 
                    success = true,
                    totalProducts = _productCount,
                    targetProducts = TARGET_PRODUCT_COUNT,
                    shouldStop = _shouldStop,
                    message = "상품 데이터 수집 완료"
                });
            }
            catch (Exception ex)
            {
                LogWindow.AddLogStatic($"상품 데이터 처리 오류: {ex.Message}");
                
                // ⭐ 오류 발생 시에도 다음 스토어로 이동
                lock (_storeProcessLock)
                {
                    _currentStoreIndex++;
                    LogWindow.AddLogStatic($"📈 오류 후 다음 스토어로 이동: {_currentStoreIndex}/{_selectedStores.Count}");

                    // 🛑 10개 스토어 완료 체크
                    if (_currentStoreIndex >= 10)
                    {
                        LogWindow.AddLogStatic("🎉 10개 스토어 모두 완료 (오류 발생 후) - 크롤링 중단");
                        _shouldStop = true;
                        _isCrawlingActive = false;

                        if (!_completionPopupShown)
                        {
                            var finalCount = GetCurrentProductCount();
                            ShowCrawlingResultPopup(finalCount, "10개 스토어 모두 완료");
                        }
                    }
                }
                
                return Results.Json(new { 
                    success = false, 
                    error = ex.Message 
                }, statusCode: 500);
            }
        }

        // Chrome 확장프로그램 로그 API
        private async Task<IResult> HandleExtensionLog(HttpContext context)
        {
            try
            {
                using var reader = new StreamReader(context.Request.Body);
                var json = await reader.ReadToEndAsync();
                
                ExtensionLogRequest? logData = null;
                try
                {
                    logData = JsonSerializer.Deserialize<ExtensionLogRequest>(json);
                }
                catch (Exception jsonEx)
                {
                    LogWindow.AddLogStatic($"❌ 로그 데이터 JSON 파싱 오류: {jsonEx.Message}");
                    return Results.Json(new { 
                        success = false, 
                        error = "Invalid JSON format" 
                    }, statusCode: 400);
                }
                
                if (logData != null && !string.IsNullOrEmpty(logData.Message))
                {
                    LogWindow.AddLogStatic(logData.Message);
                }

                return Results.Json(new { 
                    success = true,
                    message = "로그 수신 완료"
                });
            }
            catch (Exception ex)
            {
                return Results.Json(new { 
                    success = false, 
                    error = ex.Message 
                }, statusCode: 500);
            }
        }

        // ⭐ 스토어 상태 설정
        private async Task<IResult> HandleStoreState(HttpRequest request)
        {
            try
            {
                using var reader = new StreamReader(request.Body);
                var json = await reader.ReadToEndAsync();
                
                JsonElement data;
                try
                {
                    data = JsonSerializer.Deserialize<JsonElement>(json);
                }
                catch (Exception jsonEx)
                {
                    LogWindow.AddLogStatic($"❌ 상태 데이터 JSON 파싱 오류: {jsonEx.Message}");
                    return Results.BadRequest(new { error = "Invalid JSON format" });
                }
                
                var storeId = data.GetProperty("storeId").GetString() ?? "";
                var runId = data.GetProperty("runId").GetString() ?? "";
                var state = data.GetProperty("state").GetString() ?? "";
                var lockValue = data.GetProperty("lock").GetBoolean();
                var expected = data.TryGetProperty("expected", out var exp) ? exp.GetInt32() : 0;
                var progress = data.TryGetProperty("progress", out var prog) ? prog.GetInt32() : 0;
                
                var storeState = new StoreState
                {
                    StoreId = storeId,
                    RunId = runId,
                    State = state,
                    Lock = lockValue,
                    Expected = expected,
                    Progress = progress,
                    UpdatedAt = DateTime.Now
                };
                
                lock (_statesLock)
                {
                    var key = $"{storeId}:{runId}";
                    _storeStates[key] = storeState;
                }
                
                // ⭐ 스토어가 완료(done) 상태가 되면 모든 스토어 완료 체크
                if (state == "done")
                {
                    LogWindow.AddLogStatic($"✅ {storeId}: 완료 상태로 변경됨 - 전체 완료 체크 시작");
                    CheckAllStoresCompletedFromServer();
                }
                
                LogWindow.AddLogStatic($"{storeId}: 상태 설정 - {state} (lock: {lockValue}, {progress}/{expected})");
                
                return Results.Ok(new { success = true, storeId, runId, state });
            }
            catch (Exception ex)
            {
                LogWindow.AddLogStatic($"상태 설정 오류: {ex.Message}");
                return Results.BadRequest(new { error = ex.Message });
            }
        }

        // ⭐ 스토어 상태 확인
        private IResult HandleGetStoreState(HttpContext context)
        {
            try
            {
                var storeId = context.Request.Query["storeId"].ToString();
                var runId = context.Request.Query["runId"].ToString();
                
                if (string.IsNullOrEmpty(storeId) || string.IsNullOrEmpty(runId))
                {
                    return Results.BadRequest(new { error = "storeId and runId required" });
                }
                
                StoreState storeState;
                lock (_statesLock)
                {
                    var key = $"{storeId}:{runId}";
                    // 상태 조회 로그 제거 (너무 빈번함)
                    
                    if (!_storeStates.TryGetValue(key, out storeState!))
                    {
                        // ⭐ 상태가 없으면 기본 상태 생성
                        storeState = new StoreState
                        {
                            StoreId = storeId,
                            RunId = runId,
                            State = "waiting",
                            Lock = false,
                            Expected = 0,
                            Progress = 0,
                            UpdatedAt = DateTime.Now
                        };
                        _storeStates[key] = storeState;
                        LogWindow.AddLogStatic($"{storeId}: 기본 상태 생성 - waiting");
                    }
                }
                
                // ⭐ 진행률 정체 감지 (같은 진행률이 5번 반복되면 강제 진행)
                if (storeState.State == "visiting")
                {
                    if (storeState.LastProgress == storeState.Progress)
                    {
                        storeState.StuckCount++;
                        if (storeState.StuckCount >= 5)
                        {
                            LogWindow.AddLogStatic($"{storeId}: 진행률 정체 감지 ({storeState.Progress}/{storeState.Expected}) - 강제 진행");
                            
                            lock (_statesLock)
                            {
                                var key = $"{storeId}:{runId}";
                                if (_storeStates.ContainsKey(key))
                                {
                                    _storeStates[key].Progress++;
                                    _storeStates[key].StuckCount = 0;
                                    _storeStates[key].UpdatedAt = DateTime.Now;
                                    storeState = _storeStates[key];
                                }
                            }
                        }
                    }
                    else
                    {
                        storeState.LastProgress = storeState.Progress;
                        storeState.StuckCount = 0;
                    }
                }
                
                // ⭐ collecting 상태 세분화된 타임아웃 처리
                if (storeState.State.StartsWith("collecting"))
                {
                    // 연속 카운터 증가
                    storeState.StuckCount++;
                    
                    // 상태별 다른 타임아웃 적용
                    int maxStuckCount = storeState.State switch
                    {
                        "collecting_gonggu" => 3,      // 공구 체크: 3번 (9초)
                        "collecting_category" => 2,    // 카테고리: 2번 (6초)  
                        "collecting_products" => 5,    // 상품 검색: 5번 (15초)
                        _ => 5                          // 기본값 (collecting)
                    };
                    
                    if (storeState.StuckCount >= maxStuckCount)
                    {
                        LogWindow.AddLogStatic($"{storeId}: {storeState.State} 상태 {maxStuckCount}번 연속 - 강제 완료 처리");

                        lock (_statesLock)
                        {
                            var key = $"{storeId}:{runId}";
                            if (_storeStates.ContainsKey(key))
                            {
                                _storeStates[key].State = "done";
                                _storeStates[key].Lock = false;
                                _storeStates[key].StuckCount = 0;
                                _storeStates[key].UpdatedAt = DateTime.Now;
                                storeState = _storeStates[key];

                                LogWindow.AddLogStatic($"⏭️ {storeId} 강제 완료 - 다음 스토어로 강제 이동");

                                // 🔥 크롤링 완료 시 소싱 페이지 새로고침
                                RefreshSourcingPage();
                            }
                        }
                        
                        // ⭐ 강제로 다음 스토어 열기
                        _ = Task.Run(async () => {
                            await Task.Delay(1000);
                            await ForceOpenNextStore();
                        });
                    }
                }
                else
                {
                    // collecting 상태가 아니면 카운터 리셋
                    storeState.StuckCount = 0;
                }
                
                // ⭐ 타임아웃 체크 (30초 이상 collecting 상태면 강제 완료)
                if (storeState.State.StartsWith("collecting") &&
                    DateTime.Now - storeState.UpdatedAt > TimeSpan.FromSeconds(30))
                {
                    LogWindow.AddLogStatic($"{storeId}: 30초 {storeState.State} 타임아웃 - 강제 완료 처리");

                    lock (_statesLock)
                    {
                        var key = $"{storeId}:{runId}";
                        if (_storeStates.ContainsKey(key))
                        {
                            _storeStates[key].State = "done";
                            _storeStates[key].Lock = false;
                            _storeStates[key].UpdatedAt = DateTime.Now;
                            storeState = _storeStates[key];

                            // ⭐ 인덱스 증가는 제거 - Chrome 확장에서 다음 스토어 요청 시 자동으로 증가됨
                            LogWindow.AddLogStatic($"⏭️ {storeId} 30초 타임아웃 완료 - Chrome 확장이 다음 스토어로 이동할 때까지 대기");

                            // 🔥 크롤링 완료 시 소싱 페이지 새로고침
                            RefreshSourcingPage();
                        }
                    }
                }

                // ⭐ 타임아웃 체크 (2분 이상 visiting 상태면 강제 완료)
                if (storeState.State == "visiting" &&
                    DateTime.Now - storeState.UpdatedAt > TimeSpan.FromMinutes(2))
                {
                    LogWindow.AddLogStatic($"{storeId}: 2분 타임아웃 - 강제 완료 처리");

                    lock (_statesLock)
                    {
                        var key = $"{storeId}:{runId}";
                        if (_storeStates.ContainsKey(key))
                        {
                            _storeStates[key].State = "done";
                            _storeStates[key].Lock = false;
                            _storeStates[key].UpdatedAt = DateTime.Now;
                            storeState = _storeStates[key];

                            // ⭐ 인덱스 증가는 제거 - Chrome 확장에서 다음 스토어 요청 시 자동으로 증가됨
                            LogWindow.AddLogStatic($"⏭️ {storeId} 2분 타임아웃 완료 - Chrome 확장이 다음 스토어로 이동할 때까지 대기");

                            // 🔥 크롤링 완료 시 소싱 페이지 새로고침
                            RefreshSourcingPage();
                        }
                    }
                }
                
                // ⭐ Chrome 순차 처리 시스템 사용 - 서버 타임아웃 제거
                // collecting 상태 타임아웃 체크 제거됨 (Chrome에서 처리)
                
                LogWindow.AddLogStatic($"{storeId}: 상태 확인 - {storeState.State} (lock: {storeState.Lock}, {storeState.Progress}/{storeState.Expected})");
                
                return Results.Ok(storeState);
            }
            catch (Exception ex)
            {
                LogWindow.AddLogStatic($"상태 확인 오류: {ex.Message}");
                return Results.BadRequest(new { error = ex.Message });
            }
        }

        // ⭐ 진행률 업데이트
        private async Task<IResult> HandleStoreProgress(HttpRequest request)
        {
            try
            {
                using var reader = new StreamReader(request.Body);
                var json = await reader.ReadToEndAsync();
                
                JsonElement data;
                try
                {
                    data = JsonSerializer.Deserialize<JsonElement>(json);
                }
                catch (Exception jsonEx)
                {
                    LogWindow.AddLogStatic($"❌ 진행률 데이터 JSON 파싱 오류: {jsonEx.Message}");
                    return Results.BadRequest(new { error = "Invalid JSON format" });
                }
                
                var storeId = data.GetProperty("storeId").GetString() ?? "";
                var runId = data.GetProperty("runId").GetString() ?? "";
                var inc = data.TryGetProperty("inc", out var incValue) ? incValue.GetInt32() : 1;
                
                lock (_statesLock)
                {
                    var key = $"{storeId}:{runId}";
                    if (_storeStates.TryGetValue(key, out var state))
                    {
                        state.Progress += inc;
                        state.UpdatedAt = DateTime.Now;
                        LogWindow.AddLogStatic($"{storeId}: 진행률 업데이트 - {state.Progress}/{state.Expected}");
                    }
                }
                
                return Results.Ok(new { success = true });
            }
            catch (Exception ex)
            {
                LogWindow.AddLogStatic($"진행률 업데이트 오류: {ex.Message}");
                return Results.BadRequest(new { error = ex.Message });
            }
        }

        // ⭐ 전체 상태 확인 API
        private async Task<IResult> HandleGetStatus(HttpContext context)
        {
            try
            {
                var status = new
                {
                    success = true,
                    productCount = _productCount,
                    targetCount = TARGET_PRODUCT_COUNT,
                    isRunning = !_shouldStop,
                    isCrawlingActive = _isCrawlingActive,
                    shouldStop = _shouldStop,
                    selectedStores = _selectedStores.Count,
                    progress = _productCount * 100.0 / TARGET_PRODUCT_COUNT,
                    timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                    v2Mode = _v2Mode
                };
                
                // ⭐ 중단 신호 요청 시 로그 출력
                if (_shouldStop)
                {
                    LogWindow.AddLogStatic($"🛑 Chrome에서 중단 신호 조회: shouldStop = {_shouldStop}, productCount = {_productCount}");
                }
                
                context.Response.ContentType = "application/json; charset=utf-8";
                context.Response.StatusCode = 200;
                await context.Response.WriteAsync(JsonSerializer.Serialize(status));
                
                return Results.Ok();
            }
            catch (Exception)
            {
                // 상태 조회 API 오류 로그 간소화
                
                context.Response.ContentType = "application/json; charset=utf-8";
                context.Response.StatusCode = 500;
                await context.Response.WriteAsync("{\"success\":false,\"error\":\"Status API error\"}");
                
                return Results.StatusCode(500);
            }
        }

        // 크롤링 중단 API (차단 감지 시)
        private async Task<IResult> HandleStopCrawling(HttpContext context)
        {
            try
            {
                using var reader = new StreamReader(context.Request.Body);
                var json = await reader.ReadToEndAsync();

                var stopData = JsonSerializer.Deserialize<JsonElement>(json);

                // ⭐ 선택적 파라미터 처리
                string? reason = stopData.TryGetProperty("reason", out var reasonProp) ? reasonProp.GetString() : "알 수 없음";
                string? storeId = stopData.TryGetProperty("storeId", out var storeIdProp) ? storeIdProp.GetString() : null;
                string? message = stopData.TryGetProperty("message", out var messageProp) ? messageProp.GetString() : null;

                LogWindow.AddLogStatic($"🚫 크롤링 중단 요청 수신: {reason}");
                if (storeId != null) LogWindow.AddLogStatic($"🚫 스토어: {storeId}");
                if (message != null) LogWindow.AddLogStatic($"🚫 사유: {message}");

                // ⭐ 즉시 크롤링 중단
                lock (_counterLock)
                {
                    // ⭐ 크롤링 중단
                    _shouldStop = true;
                    _isCrawlingActive = false; // ⭐ 추가: 모든 데이터 처리 중단

                    // ⭐ 크롬 탭 자동 닫기 제거 (테스트용)
                    // _ = Task.Run(() => CloseAllChromeTabs());

                    // ⭐ 실제 파일 개수로 정확한 계산
                    var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                    var imagesPath = Path.Combine(appDataPath, "Predvia", "Images");
                    var actualCount = 0;

                    try
                    {
                        if (Directory.Exists(imagesPath))
                        {
                            actualCount = Directory.GetFiles(imagesPath, "*_main.jpg").Length;
                        }
                    }
                    catch { }

                    LogWindow.AddLogStatic($"🛑 크롤링 중단: {reason}");
                    LogWindow.AddLogStatic($"📊 최종 수집 완료: {actualCount}/100개 ({(actualCount * 100.0 / 100):F1}%)");

                    // ⭐ 팝업창으로 최종 결과 표시 (포커싱 실패는 제외)
                    if (reason != "포커싱 실패")
                    {
                        ShowCrawlingResultPopup(actualCount, reason ?? "중단");
                    }

                    // ⭐ 80개 미만이면 Chrome 재시작
                    if (_productCount < 80)
                    {
                        LogWindow.AddLogStatic($"🔄 80개 미만 수집 - 크롤링 완료");
                    }
                }

                // ⭐ 로딩창 숨기기
                LoadingHelper.HideLoadingOverlay();
                LogWindow.AddLogStatic($"✅ 로딩창 숨김 완료 (크롤링 중단)");

                // ⭐ 브라우저 종료 (스마트스토어 창 + 네이버 가격비교 창) - 직접 실행
                try
                {
                    await Task.Delay(500);
                    LogWindow.AddLogStatic($"🔥 브라우저 종료 시작 (크롤링 중단)");

                    // 스마트스토어 크롤링 창들 종료
                    await ChromeExtensionService.CloseSmartStoreCrawlingWindows();
                    LogWindow.AddLogStatic($"✅ 크롤링 스마트스토어 창 종료 완료");

                    // 네이버 가격비교 창 종료
                    await ChromeExtensionService.CloseNaverPriceComparisonWindowByTitle();
                    LogWindow.AddLogStatic($"✅ 네이버 가격비교 창 종료 완료");
                }
                catch (Exception browserEx)
                {
                    LogWindow.AddLogStatic($"❌ 브라우저 종료 오류: {browserEx.Message}");
                }

                // 🔥 차단으로 중단되어도 카드 생성 (포커싱 실패는 제외)
                if (reason != "포커싱 실패")
                {
                    RefreshSourcingPage();
                }

                context.Response.ContentType = "application/json; charset=utf-8";
                context.Response.StatusCode = 200;
                await context.Response.WriteAsync("{\"success\":true,\"message\":\"Crawling stopped\"}");

                return Results.Ok();
            }
            catch (Exception ex)
            {
                LogWindow.AddLogStatic($"❌ 크롤링 중단 API 오류: {ex.Message}");
                LogWindow.AddLogStatic($"❌ 오류 상세: {ex.StackTrace}");

                context.Response.ContentType = "application/json; charset=utf-8";
                context.Response.StatusCode = 500;
                await context.Response.WriteAsync("{\"success\":false,\"error\":\"Stop API error\"}");

                return Results.Ok();
            }
        }

        // ⭐ 스토어 스킵 API (1000개 미만 스토어)
        private async Task<IResult> HandleSkipStore(HttpContext context)
        {
            try
            {
                using var reader = new StreamReader(context.Request.Body);
                var json = await reader.ReadToEndAsync();
                var data = JsonSerializer.Deserialize<JsonElement>(json);
                
                var storeId = data.TryGetProperty("storeId", out var sid) ? sid.GetString() : "unknown";
                var reason = data.TryGetProperty("reason", out var r) ? r.GetString() : "스킵";
                
                LogWindow.AddLogStatic($"⏭️ {storeId}: 스킵 - {reason}");
                
                // 스토어 상태를 done으로 설정
                lock (_counterLock)
                {
                    if (_storeStates.ContainsKey(storeId))
                    {
                        _storeStates[storeId].Status = "done";
                        _storeStates[storeId].IsLocked = false;
                    }
                }
                
                context.Response.ContentType = "application/json; charset=utf-8";
                await context.Response.WriteAsync("{\"success\":true}");
                return Results.Ok();
            }
            catch (Exception ex)
            {
                LogWindow.AddLogStatic($"❌ 스킵 API 오류: {ex.Message}");
                return Results.Ok();
            }
        }

        // ⭐ 크롬 탭 닫기 메서드
        private void CloseAllChromeTabs()
        {
            try
            {
                LogWindow.AddLogStatic("🔥 Chrome 프로세스 종료 시작");
                
                var chromeProcesses = System.Diagnostics.Process.GetProcessesByName("chrome");
                LogWindow.AddLogStatic($"🔍 발견된 Chrome 프로세스: {chromeProcesses.Length}개");
                
                foreach (var process in chromeProcesses)
                {
                    try
                    {
                        if (!process.HasExited)
                        {
                            process.Kill();
                            LogWindow.AddLogStatic($"🔥 Chrome 프로세스 종료: PID {process.Id}");
                        }
                    }
                    catch (Exception ex)
                    {
                        LogWindow.AddLogStatic($"❌ Chrome 프로세스 종료 실패: PID {process.Id} - {ex.Message}");
                    }
                    finally
                    {
                        process.Dispose();
                    }
                }
                
                LogWindow.AddLogStatic("✅ 모든 Chrome 프로세스 종료 완료");
            }
            catch (Exception ex)
            {
                LogWindow.AddLogStatic($"❌ Chrome 탭 닫기 실행 오류: {ex.Message}");
            }
        }
        
        // ⭐ 모든 Chrome 앱 창 닫기 (네이버 + 스마트스토어 + 상품페이지)
        [System.Runtime.Versioning.SupportedOSPlatform("windows")]
        private static bool _saveCompleted = false;
        
        private async Task CloseAllChromeApps()
        {
            try
            {
                // ⭐ 중복 저장 방지
                if (!_saveCompleted)
                {
                    _saveCompleted = true;
                    LogWindow.AddLogStatic("💾 크롤링 완료 - 상품 데이터 저장 중...");
                    SaveProductCardsFromFiles();
                }
                
                LogWindow.AddLogStatic("🔥 Chrome 앱 창들 닫기 시작 - 가격비교 창 포함");
                
                // ⭐ 먼저 가격비교 창 닫기
                var chromeExtensionService = new ChromeExtensionService();
                chromeExtensionService.CloseNaverPriceComparisonOnly();
                
                var chromeProcesses = System.Diagnostics.Process.GetProcessesByName("chrome");
                LogWindow.AddLogStatic($"📊 총 Chrome 프로세스 개수: {chromeProcesses.Length}개");
                
                int closedCount = 0;
                int checkedCount = 0;
                
                foreach (var process in chromeProcesses)
                {
                    try
                    {
                        if (!process.HasExited)
                        {
                            checkedCount++;
                            LogWindow.AddLogStatic($"🔍 Chrome 프로세스 분석 중: PID {process.Id}");
                            
                            // ⭐ CommandLine으로 --app 옵션 확인
                            bool isAppMode = false;
                            string commandLineInfo = "";
                            
                            try
                            {
                                using (var searcher = new ManagementObjectSearcher(
                                    $"SELECT CommandLine FROM Win32_Process WHERE ProcessId = {process.Id}"))
                                {
                                    foreach (ManagementObject obj in searcher.Get())
                                    {
                                        var commandLine = obj["CommandLine"]?.ToString() ?? "";
                                        commandLineInfo = commandLine.Length > 200 ? commandLine.Substring(0, 200) + "..." : commandLine;
                                        
                                        if (commandLine.Contains("--app="))
                                        {
                                            isAppMode = true;
                                            LogWindow.AddLogStatic($"🎯 앱 모드 감지! PID {process.Id}");
                                            LogWindow.AddLogStatic($"📝 CommandLine: {commandLineInfo}");
                                            break;
                                        }
                                    }
                                }
                                
                                if (!isAppMode && !string.IsNullOrEmpty(commandLineInfo))
                                {
                                    LogWindow.AddLogStatic($"❌ 일반 Chrome: PID {process.Id} - {commandLineInfo}");
                                }
                            }
                            catch (Exception cmdEx)
                            {
                                LogWindow.AddLogStatic($"⚠️ CommandLine 조회 실패 PID {process.Id}: {cmdEx.Message}");
                                
                                // CommandLine 조회 실패 시 창 크기로 대체 판별
                                if (process.MainWindowHandle != IntPtr.Zero)
                                {
                                    var windowRect = new System.Drawing.Rectangle();
                                    if (GetWindowRect(process.MainWindowHandle, out windowRect))
                                    {
                                        int width = windowRect.Width;
                                        int height = windowRect.Height;
                                        LogWindow.AddLogStatic($"📏 창 크기: PID {process.Id} - {width}x{height}");
                                        
                                        // 작은 창이면 앱 모드로 추정 (더 넓은 범위)
                                        if (width <= 800 && height <= 800)
                                        {
                                            isAppMode = true;
                                            LogWindow.AddLogStatic($"🔍 크기 기반 앱 모드 추정: PID {process.Id} ({width}x{height})");
                                        }
                                    }
                                }
                            }
                            
                            // ⭐ 앱 모드로 판별된 경우에만 종료
                            if (isAppMode)
                            {
                                LogWindow.AddLogStatic($"🔥 Chrome 앱 창 종료 시도: PID {process.Id}");
                                
                                // 1단계: 정상 종료 시도
                                bool closed = process.CloseMainWindow();
                                LogWindow.AddLogStatic($"📤 CloseMainWindow 결과: {closed}");
                                
                                await Task.Delay(500);
                                
                                // 2단계: 아직 살아있으면 강제 종료
                                if (!process.HasExited)
                                {
                                    LogWindow.AddLogStatic($"💀 강제 종료 시도: PID {process.Id}");
                                    process.Kill();
                                    process.WaitForExit(2000);
                                }
                                
                                if (process.HasExited)
                                {
                                    closedCount++;
                                    LogWindow.AddLogStatic($"✅ Chrome 앱 창 종료 완료: PID {process.Id}");
                                }
                                else
                                {
                                    LogWindow.AddLogStatic($"❌ Chrome 앱 창 종료 실패: PID {process.Id}");
                                }
                            }
                        }
                    }
                    catch (Exception processEx)
                    {
                        LogWindow.AddLogStatic($"❌ 프로세스 처리 오류 PID {process.Id}: {processEx.Message}");
                    }
                    finally
                    {
                        process?.Dispose();
                    }
                }
                
                LogWindow.AddLogStatic($"🎯 Chrome 앱 창 닫기 완료: {closedCount}/{checkedCount}개 종료");
                
                // ⭐ 추가 확인: 남은 Chrome 프로세스 개수
                await Task.Delay(1000);
                var remainingProcesses = System.Diagnostics.Process.GetProcessesByName("chrome");
                LogWindow.AddLogStatic($"📊 남은 Chrome 프로세스: {remainingProcesses.Length}개");
            }
            catch (Exception ex)
            {
                LogWindow.AddLogStatic($"❌ Chrome 앱 창 닫기 전체 오류: {ex.Message}");
            }
        }
        
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out System.Drawing.Rectangle lpRect);
        
        // ⭐ 서버에서 모든 스토어 완료 체크
        private void CheckAllStoresCompletedFromServer()
        {
            try
            {
                // ⭐ 이미 팝업이 표시되었으면 중복 실행 방지
                if (_completionPopupShown)
                {
                    return;
                }
                
                // ⭐ 100개 달성 체크 - 정확한 파일 개수로 확인
                var actualCount = GetCurrentProductCount();
                if (actualCount >= TARGET_PRODUCT_COUNT)
                {
                    LogWindow.AddLogStatic("🎉 목표 달성! 100개 상품 수집 완료 - 크롤링 중단");
                    
                    // ⭐ 크롤링 완전 중단 신호 설정
                    _shouldStop = true;
                    _isCrawlingActive = false;
                    
                    // ⭐ 모든 스토어를 done 상태로 변경하여 Chrome 중단
                    lock (_statesLock)
                    {
                        foreach (var storeId in _selectedStores.Select(s => s.StoreId))
                        {
                            if (_storeStates.ContainsKey(storeId))
                            {
                                var state = _storeStates[storeId];
                                if (state.State != "done")
                                {
                                    state.State = "done";
                                    state.Lock = false;
                                    LogWindow.AddLogStatic($"🛑 {storeId}: 강제 완료 처리 (목표 달성)");
                                }
                            }
                        }
                    }
                    
                    // ⭐ 팝업창으로 최종 결과 표시
                    ShowCrawlingResultPopup(actualCount, "목표 달성");
                    
                    return;
                }
                
                // 나머지 로직: 모든 스토어 완료 체크
                int totalSelectedStores = _selectedStores?.Count ?? 0;
                int completedStores = _storeStates.Values.Count(s => s.State == "done");
                bool allStoresCompleted = completedStores >= 10; // 10개 이상 완료되면 종료
                
                LogWindow.AddLogStatic($"🔍 모든 스토어 완료 여부: {allStoresCompleted} ({completedStores}/10)");
                
                if (allStoresCompleted)
                {
                    LogWindow.AddLogStatic("🎉 10개 스토어 모두 완료 - 크롤링 종료");
                    
                    // ⭐ 플래그 리셋 후 Chrome 앱 창들 닫기
                    _completionPopupShown = false; // 플래그 리셋
                    _ = Task.Run(async () => await CloseAllChromeApps());
                    
                    // ⭐ 팝업창으로 최종 결과 표시
                    ShowCrawlingResultPopup(actualCount, "10개 스토어 모두 완료");
                    
                    return;
                }
                
                LogWindow.AddLogStatic($"📊 진행 상황: {completedStores}/10 스토어 완료, {actualCount}/100 상품 수집 - 크롤링 계속 진행");
                
            }
            catch (Exception ex)
            {
                LogWindow.AddLogStatic($"❌ 서버 측 모든 스토어 완료 체크 오류: {ex.Message}");
            }
        }

        // ⭐ 크롤링 상태 확인 API
        private async Task<IResult> HandleGetCrawlingStatus(HttpContext context)
        {
            try
            {
                var currentCount = GetCurrentProductCount();
                int processedStores = 0;
                int totalStores = _selectedStores?.Count ?? 0;

                // v2 상태머신 우선, 없으면 v1 폴백
                if (_crawlSM != null && _crawlSM.Stores.Count > 0)
                {
                    processedStores = _crawlSM.CurrentStoreIdx;
                    totalStores = _crawlSM.Stores.Count;
                }
                else
                {
                    processedStores = _currentStoreIndex;
                }

                var json = JsonSerializer.Serialize(new {
                    currentCount = currentCount,
                    totalAttempted = _crawlSM?.TotalAttempted ?? _totalAttempted,
                    processedStores = processedStores,
                    totalStores = totalStores,
                    isCompleted = _shouldStop || (totalStores > 0 && (processedStores >= totalStores))
                });
                context.Response.ContentType = "application/json; charset=utf-8";
                await context.Response.WriteAsync(json);
                return Results.Ok();
            }
            catch (Exception ex)
            {
                context.Response.ContentType = "application/json; charset=utf-8";
                await context.Response.WriteAsync(JsonSerializer.Serialize(new { error = ex.Message }));
                return Results.Ok();
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 서버 주도 크롤링 API (v2)
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private async Task<IResult> HandleCrawlStart(HttpContext context)
        {
            try
            {
                var body = await new StreamReader(context.Request.Body).ReadToEndAsync();
                var data = JsonSerializer.Deserialize<JsonElement>(body);
                
                var stores = new List<CrawlStore>();
                if (data.TryGetProperty("stores", out var storesArr))
                {
                    foreach (var s in storesArr.EnumerateArray())
                    {
                        stores.Add(new CrawlStore
                        {
                            StoreId = s.GetProperty("storeId").GetString() ?? "",
                            Url = s.TryGetProperty("url", out var u) ? u.GetString() ?? "" : "",
                            Title = s.TryGetProperty("title", out var t) ? t.GetString() ?? "" : ""
                        });
                    }
                }

                _crawlSM = new CrawlStateMachine(TARGET_PRODUCT_COUNT, _minPrice, _maxPrice, _priceFilterEnabled);
                _crawlSM.SetStores(stores);
                
                // 기존 v1 상태 초기화
                _shouldStop = false;
                _productCount = 0;
                _totalAttempted = 0;
                _currentStoreIndex = 0;
                _v2Mode = true;
                _selectedStores = stores.Select(s => new SmartStoreLink { StoreId = s.StoreId, Url = s.Url, Title = s.Title }).ToList();
                
                LogWindow.AddLogStatic($"🚀 [v2] 크롤링 시작: {stores.Count}개 스토어");
                
                context.Response.ContentType = "application/json; charset=utf-8";
                await context.Response.WriteAsync(JsonSerializer.Serialize(new { success = true, storeCount = stores.Count }));
                return Results.Ok();
            }
            catch (Exception ex)
            {
                context.Response.ContentType = "application/json; charset=utf-8";
                await context.Response.WriteAsync(JsonSerializer.Serialize(new { success = false, error = ex.Message }));
                return Results.Ok();
            }
        }

        private async Task<IResult> HandleCrawlNextTask(HttpContext context)
        {
            try
            {
                context.Response.ContentType = "application/json; charset=utf-8";
                if (_crawlSM == null)
                {
                    await context.Response.WriteAsync(JsonSerializer.Serialize(new { action = "wait", reason = "크롤링 미시작" }));
                    return Results.Ok();
                }
                
                var task = _crawlSM.GetNextTask();
                
                // v2 완료 시 기존 시스템과 연동
                if (_crawlSM.IsCompleted && !_shouldStop)
                {
                    _shouldStop = true;
                    _productCount = _crawlSM.SuccessCount;
                    _totalAttempted = _crawlSM.TotalAttempted;
                    LogWindow.AddLogStatic($"🏁 [v2] 크롤링 완료: 성공 {_crawlSM.SuccessCount}, 시도 {_crawlSM.TotalAttempted}");
                    
                    // 로딩창 숨김 + Chrome 닫기 + 팝업
                    var finalCount = _crawlSM.SuccessCount;
                    var server = this;
                    _ = Task.Run(async () =>
                    {
                        await Task.Delay(1000);
                        LoadingHelper.HideLoadingFromSourcingPage();
                        await server.CloseAllChromeApps();
                        ShowCrawlingResultPopup(finalCount, "크롤링 완료");
                    });
                }
                
                await context.Response.WriteAsync(JsonSerializer.Serialize(task));
                return Results.Ok();
            }
            catch (Exception ex)
            {
                context.Response.ContentType = "application/json; charset=utf-8";
                await context.Response.WriteAsync(JsonSerializer.Serialize(new { action = "wait", reason = ex.Message }));
                return Results.Ok();
            }
        }

        private async Task<IResult> HandleCrawlReport(HttpContext context)
        {
            try
            {
                var body = await new StreamReader(context.Request.Body).ReadToEndAsync();
                var data = JsonSerializer.Deserialize<JsonElement>(body);
                var type = data.GetProperty("type").GetString() ?? "";
                var storeId = data.TryGetProperty("storeId", out var sid) ? sid.GetString() ?? "" : "";
                LogWindow.AddLogStatic($"📨 [v2] report 수신: type={type}, storeId={storeId}");

                if (_crawlSM == null)
                {
                    context.Response.ContentType = "application/json; charset=utf-8";
                    await context.Response.WriteAsync(JsonSerializer.Serialize(new { success = false, error = "크롤링 미시작" }));
                    return Results.Ok();
                }

                switch (type)
                {
                    case "gonggu_result":
                        var count = data.GetProperty("count").GetInt32();
                        _crawlSM.ReportGongguResult(storeId, count);
                        break;

                    case "no_gonggu":
                        _crawlSM.ReportNoGonggu(storeId);
                        break;

                    case "product_list":
                        var products = new List<string>();
                        foreach (var p in data.GetProperty("products").EnumerateArray())
                            products.Add(p.GetString() ?? "");
                        _crawlSM.ReportProductList(storeId, products);
                        break;

                    case "product_data":
                        var productId = data.GetProperty("productId").GetString() ?? "";
                        var priceValue = data.TryGetProperty("priceValue", out var pv) ? pv.GetInt32() : 0;
                        var hasImage = data.TryGetProperty("hasImage", out var hi) && hi.GetBoolean();
                        var hasName = data.TryGetProperty("hasName", out var hn) && hn.GetBoolean();
                        _crawlSM.ReportProductData(storeId, productId, priceValue, hasImage, hasName);
                        
                        // 기존 데이터 저장 로직 호출 (이미지/상품명/리뷰는 기존 API로 처리)
                        break;

                    case "page_timeout":
                        _crawlSM.ReportPageLoadTimeout(storeId);
                        break;

                    default:
                        LogWindow.AddLogStatic($"⚠️ [v2] 알 수 없는 report type: {type}");
                        break;
                }

                context.Response.ContentType = "application/json; charset=utf-8";
                await context.Response.WriteAsync(JsonSerializer.Serialize(new { 
                    success = true, 
                    phase = _crawlSM.CurrentPhase.ToString(),
                    successCount = _crawlSM.SuccessCount,
                    attempted = _crawlSM.TotalAttempted
                }));
                return Results.Ok();
            }
            catch (Exception ex)
            {
                context.Response.ContentType = "application/json; charset=utf-8";
                await context.Response.WriteAsync(JsonSerializer.Serialize(new { success = false, error = ex.Message }));
                return Results.Ok();
            }
        }

        // ⭐ 구글렌즈 타오바오 검색 핸들러
        private async Task<IResult> HandleGoogleLensSearch(HttpContext context)
        {
            try
            {
                using var reader = new StreamReader(context.Request.Body);
                var body = await reader.ReadToEndAsync();
                var data = JsonSerializer.Deserialize<GoogleLensSearchRequest>(body);
                
                if (data == null || string.IsNullOrEmpty(data.ImageBase64))
                {
                    return Results.BadRequest(new { error = "이미지 데이터 필요" });
                }
                
                LogWindow.AddLogStatic($"🔍 [1688 검색] 상품 {data.ProductId} 검색 시작");
                
                var imageBytes = Convert.FromBase64String(data.ImageBase64);
                
                // 1688 이미지 검색 (비로그인)
                var products = await Search1688ByImage(imageBytes);
                
                LogWindow.AddLogStatic($"✅ 1688 검색 완료: {products.Count}개 상품 발견");
                
                var responseJson = JsonSerializer.Serialize(new { success = true, products = products });
                context.Response.ContentType = "application/json; charset=utf-8";
                await context.Response.WriteAsync(responseJson);
                return Results.Ok();
            }
            catch (Exception ex)
            {
                LogWindow.AddLogStatic($"❌ 1688 검색 오류: {ex.Message}");
                var errorJson = JsonSerializer.Serialize(new { success = false, error = ex.Message });
                context.Response.ContentType = "application/json; charset=utf-8";
                await context.Response.WriteAsync(errorJson);
                return Results.Ok();
            }
        }
        
        // ⭐ 1688 이미지 검색 (비로그인) -> 알리바바 API로 변경
        private async Task<List<TaobaoProduct>> Search1688ByImage(byte[] imageBytes)
        {
            var products = new List<TaobaoProduct>();
            
            try
            {
                // 프록시 사용 (비활성화)
                // var proxy = GetRandomProxy();
                using var handler = new HttpClientHandler();
                // if (proxy != null)
                // {
                //     handler.Proxy = new System.Net.WebProxy(proxy);
                //     handler.UseProxy = true;
                //     LogWindow.AddLogStatic($"🔄 알리바바 프록시: {proxy}");
                // }
                
                using var client = new HttpClient(handler);
                client.Timeout = TimeSpan.FromSeconds(30);
                client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
                client.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8");
                client.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.5");
                
                // 1. Sign 가져오기
                var signUrl = "https://open-s.alibaba.com/openservice/ossUploadSecretKeyDataService?appKey=a5m1ismomeptugvfmkkjnwwqnwyrhpb1&appName=magellan";
                var signResponse = await client.GetAsync(signUrl);
                var signJson = await signResponse.Content.ReadAsStringAsync();
                
                LogWindow.AddLogStatic($"📝 Sign 응답: {signJson.Substring(0, Math.Min(200, signJson.Length))}...");
                
                var signData = JsonSerializer.Deserialize<JsonElement>(signJson);
                if (!signData.TryGetProperty("data", out var data))
                {
                    LogWindow.AddLogStatic("❌ Sign 데이터 없음");
                    return products;
                }
                
                var host = data.GetProperty("host").GetString() ?? "";
                var signature = data.GetProperty("signature").GetString() ?? "";
                var policy = data.GetProperty("policy").GetString() ?? "";
                var accessId = data.GetProperty("accessid").GetString() ?? "";
                var imagePath = data.GetProperty("imagePath").GetString() ?? "";
                
                // 2. 이미지 키 생성
                var random = new Random();
                var randomStr = new string(Enumerable.Range(0, 10).Select(_ => "abcdefghijklmnopqrstuvwxyz"[random.Next(26)]).ToArray());
                var imageKey = $"{imagePath}/{randomStr}{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
                
                LogWindow.AddLogStatic($"🔑 이미지 키: {imageKey}");
                
                // 3. OSS 업로드 (프록시 없이 - OSS는 직접 연결)
                using var uploadClient = new HttpClient();
                var boundary = $"----WebKitFormBoundary{Guid.NewGuid():N}".Substring(0, 40);
                
                var sb = new System.Text.StringBuilder();
                var fileName = $"{randomStr}.jpg";
                
                var fields = new Dictionary<string, string>
                {
                    { "name", fileName },
                    { "key", imageKey },
                    { "policy", policy },
                    { "OSSAccessKeyId", accessId },
                    { "success_action_status", "200" },
                    { "callback", "" },
                    { "signature", signature }
                };
                
                foreach (var field in fields)
                {
                    sb.Append($"--{boundary}\r\n");
                    sb.Append($"Content-Disposition: form-data; name=\"{field.Key}\"\r\n\r\n");
                    sb.Append($"{field.Value}\r\n");
                }
                
                sb.Append($"--{boundary}\r\n");
                sb.Append($"Content-Disposition: form-data; name=\"file\"; filename=\"{fileName}\"\r\n");
                sb.Append("Content-Type: application/octet-stream\r\n\r\n");
                
                var headerBytes = System.Text.Encoding.UTF8.GetBytes(sb.ToString());
                var footerBytes = System.Text.Encoding.UTF8.GetBytes($"\r\n--{boundary}--\r\n");
                
                var bodyBytes = new byte[headerBytes.Length + imageBytes.Length + footerBytes.Length];
                Buffer.BlockCopy(headerBytes, 0, bodyBytes, 0, headerBytes.Length);
                Buffer.BlockCopy(imageBytes, 0, bodyBytes, headerBytes.Length, imageBytes.Length);
                Buffer.BlockCopy(footerBytes, 0, bodyBytes, headerBytes.Length + imageBytes.Length, footerBytes.Length);
                
                var uploadContent = new ByteArrayContent(bodyBytes);
                uploadContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("multipart/form-data");
                uploadContent.Headers.ContentType.Parameters.Add(new System.Net.Http.Headers.NameValueHeaderValue("boundary", boundary));
                
                var uploadResponse = await uploadClient.PostAsync(host, uploadContent);
                LogWindow.AddLogStatic($"📤 업로드: {uploadResponse.StatusCode}");
                
                if (uploadResponse.StatusCode != System.Net.HttpStatusCode.OK)
                {
                    LogWindow.AddLogStatic("❌ 이미지 업로드 실패");
                    return products;
                }
                
                // 4. 이미지 검색 (CNY 통화 설정)
                var searchUrl = $"https://www.alibaba.com/picture/search.htm?imageType=oss&escapeQp=true&imageAddress=/{imageKey}&sourceFrom=imageupload&currency=CNY";
                LogWindow.AddLogStatic($"🔍 검색 URL: {searchUrl}");
                
                // CNY 통화 설정 쿠키 추가
                client.DefaultRequestHeaders.Add("Cookie", "ali_apache_id=11.1.1.1; intl_locale=en_US; CURRENCY=CNY");
                client.DefaultRequestHeaders.Add("Referer", "https://www.alibaba.com/");
                var searchResponse = await client.GetAsync(searchUrl);
                var searchHtml = await searchResponse.Content.ReadAsStringAsync();
                
                LogWindow.AddLogStatic($"📄 검색 HTML 길이: {searchHtml.Length}");
                
                // 디버깅: 실제 HTML에 가격/리뷰 관련 키워드 확인
                LogWindow.AddLogStatic($"🔎 price-main:{searchHtml.Contains("price-main")}, CN¥:{searchHtml.Contains("CN¥")}, US$:{searchHtml.Contains("US$")}, review-score:{searchHtml.Contains("review-score")}, e-review:{searchHtml.Contains("e-review")}");
                foreach (var kw in new[] { "CN¥", "US$", "price-main", "price-area", "review" })
                {
                    var ki = searchHtml.IndexOf(kw);
                    if (ki >= 0)
                    {
                        var s = Math.Max(0, ki - 80);
                        LogWindow.AddLogStatic($"💰 [{kw}] pos={ki}: {searchHtml.Substring(s, Math.Min(300, searchHtml.Length - s))}");
                    }
                }
                
                // HTML 파서로 가격/리뷰 추출
                var doc = new HtmlAgilityPack.HtmlDocument();
                doc.LoadHtml(searchHtml);
                
                var uniqueUrls = new HashSet<string>();
                
                // 1. 상품 이미지 추출 (기존 정규식)
                var imgPattern = new System.Text.RegularExpressions.Regex(
                    @"<img[^>]*src=""(//s\.alicdn\.com/@sc\d+/kf/[^""]+)""",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                var imageUrls = imgPattern.Matches(searchHtml)
                    .Cast<System.Text.RegularExpressions.Match>()
                    .Select(m => "https:" + m.Groups[1].Value)
                    .Distinct().ToList();
                
                // 2. 가격 추출 (파서)
                var priceNodes = doc.DocumentNode.SelectNodes("//*[contains(@class, 'price-main')]");
                var priceList = priceNodes?.Select(n => n.InnerText.Trim()).ToList() ?? new List<string>();
                
                // 3. 리뷰 개수 추출 (파서)
                var reviewNodes = doc.DocumentNode.SelectNodes("//*[contains(@class, 'e-review')]");
                var reviewList = new List<string>();
                if (reviewNodes != null)
                {
                    foreach (var rn in reviewNodes)
                    {
                        var m = System.Text.RegularExpressions.Regex.Match(rn.InnerText, @"\((\d+)\)");
                        reviewList.Add(m.Success ? m.Groups[1].Value : "");
                    }
                }
                
                LogWindow.AddLogStatic($"🖼️ 이미지 {imageUrls.Count}개, 💰 가격 {priceList.Count}개, ⭐ 리뷰 {reviewList.Count}개");
                
                // 4. 상품 링크 추출 (기존 정규식)
                var linkPattern = new System.Text.RegularExpressions.Regex(
                    @"//www\.alibaba\.com/product-detail/[^""'\s<>]+",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                var linkMatches = linkPattern.Matches(searchHtml);
                
                int imgIdx = 0, priceIdx = 0, revIdx = 0;
                foreach (System.Text.RegularExpressions.Match match in linkMatches)
                {
                    var productUrl = "https:" + match.Value.Split('"')[0].Split('\'')[0];
                    if (uniqueUrls.Add(productUrl))
                    {
                        var idMatch = System.Text.RegularExpressions.Regex.Match(productUrl, @"(\d{10,})");
                        var imageUrl = imgIdx < imageUrls.Count ? imageUrls[imgIdx++] : "";
                        var price = priceIdx < priceList.Count ? priceList[priceIdx++] : "";
                        var review = "";
                        if (revIdx < reviewList.Count)
                        {
                            if (!string.IsNullOrEmpty(reviewList[revIdx]))
                                review = $"{reviewList[revIdx]}+";
                            revIdx++;
                        }
                        
                        products.Add(new TaobaoProduct
                        {
                            ProductId = idMatch.Success ? idMatch.Groups[1].Value : Guid.NewGuid().ToString("N").Substring(0, 8),
                            Title = "알리바바 상품",
                            ProductUrl = productUrl,
                            ImageUrl = imageUrl,
                            Price = price,
                            Sales = review
                        });
                        LogWindow.AddLogStatic($"🔗 상품: {price} | 리뷰 {review}");
                        if (products.Count >= 5) break;
                    }
                }
                
                // 상품 못 찾으면 검색 URL 반환
                if (products.Count == 0)
                {
                    products.Add(new TaobaoProduct
                    {
                        ProductId = imageKey,
                        Title = "알리바바 검색 결과 보기 (클릭)",
                        ProductUrl = searchUrl,
                        ImageUrl = "",
                        Price = "",
                        Sales = ""
                    });
                }
            }
            catch (Exception ex)
            {
                LogWindow.AddLogStatic($"❌ 알리바바 검색 오류: {ex.Message}");
            }
            
            return products;
        }
        
        // ⭐ 타오바오 로그인 핸들러
        private async Task<IResult> HandleTaobaoLogin(HttpContext context)
        {
            try
            {
                LogWindow.AddLogStatic("🔐 타오바오 로그인 시작...");
                
                await OpenTaobaoLoginPage();
                
                LogWindow.AddLogStatic("✅ 타오바오 로그인 페이지 열림 - 사용자가 로그인하세요");
                return Results.Ok(new { success = true, message = "타오바오 로그인 페이지 열림" });
            }
            catch (Exception ex)
            {
                LogWindow.AddLogStatic($"❌ 타오바오 로그인 오류: {ex.Message}");
                return Results.BadRequest(new { error = ex.Message });
            }
        }
        
        // ⭐ 타오바오 쿠키 수신 핸들러
        private async Task<IResult> HandleTaobaoCookies(HttpContext context)
        {
            try
            {
                var body = await new StreamReader(context.Request.Body).ReadToEndAsync();
                
                // Chrome 확장프로그램에서 보내는 JSON 구조에 맞게 수정
                var requestData = JsonSerializer.Deserialize<JsonElement>(body);
                
                Dictionary<string, string>? cookies = null;
                
                // cookies 필드가 있는지 확인
                if (requestData.TryGetProperty("cookies", out var cookiesElement))
                {
                    cookies = JsonSerializer.Deserialize<Dictionary<string, string>>(cookiesElement.GetRawText());
                }
                else
                {
                    // 직접 쿠키 딕셔너리인 경우 (이전 방식 호환)
                    cookies = JsonSerializer.Deserialize<Dictionary<string, string>>(body);
                }
                
                if (cookies != null && cookies.Count > 0)
                {
                    _taobaoCookies.Clear();
                    
                    foreach (var cookie in cookies)
                    {
                        _taobaoCookies[cookie.Key] = cookie.Value;
                        
                        // _m_h5_tk 토큰 추출 (전체 토큰 저장 - 타임스탬프 포함)
                        if (cookie.Key == "_m_h5_tk" && !string.IsNullOrEmpty(cookie.Value))
                        {
                            _taobaoToken = cookie.Value; // 전체 토큰 저장 (예: token_timestamp)
                            var displayToken = cookie.Value.Split('_')[0]; // 표시용
                            LogWindow.AddLogStatic($"🔑 타오바오 토큰 수신: {displayToken.Substring(0, Math.Min(10, displayToken.Length))}...");
                        }
                    }
                    
                    // 쿠키를 파일로도 저장 (안전한 방식)
                    var cookiesPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Predvia", "taobao_cookies.json");
                    Directory.CreateDirectory(Path.GetDirectoryName(cookiesPath)!);
                    
                    try
                    {
                        // 파일 잠금 방지를 위한 안전한 쓰기
                        var tempPath = cookiesPath + ".tmp";
                        
                        // Python이 읽을 수 있는 형식으로 저장 (단순 딕셔너리)
                        await File.WriteAllTextAsync(tempPath, JsonSerializer.Serialize(_taobaoCookies, new JsonSerializerOptions { WriteIndented = true }));
                        
                        // 기존 파일이 있으면 삭제 후 이동
                        if (File.Exists(cookiesPath))
                            File.Delete(cookiesPath);
                        File.Move(tempPath, cookiesPath);
                    }
                    catch (Exception fileEx)
                    {
                        LogWindow.AddLogStatic($"⚠️ 쿠키 파일 저장 실패: {fileEx.Message}");
                    }
                    
                    LogWindow.AddLogStatic($"✅ 타오바오 쿠키 {_taobaoCookies.Count}개 수신 및 저장 완료");
                    return Results.Ok(new { success = true, cookieCount = _taobaoCookies.Count, hasToken = !string.IsNullOrEmpty(_taobaoToken) });
                }
                
                return Results.BadRequest(new { error = "쿠키 데이터가 없습니다" });
            }
            catch (Exception ex)
            {
                LogWindow.AddLogStatic($"❌ 쿠키 수신 오류: {ex.Message}");
                return Results.BadRequest(new { error = ex.Message });
            }
        }
        
        // ⭐ 타오바오 쿠키 상태 확인 핸들러
        private async Task<IResult> HandleGetTaobaoCookies(HttpContext context)
        {
            try
            {
                // 파일에서도 쿠키 확인
                var cookiesPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Predvia", "taobao_cookies.json");
                var fileExists = File.Exists(cookiesPath);
                var fileCookieCount = 0;
                string? fileToken = null;
                bool hasLoggedIn = false; // ⭐ 로그인 쿠키 여부
                
                if (fileExists)
                {
                    var fileContent = await File.ReadAllTextAsync(cookiesPath);
                    var fileCookies = JsonSerializer.Deserialize<Dictionary<string, string>>(fileContent);
                    fileCookieCount = fileCookies?.Count ?? 0;
                    
                    // 파일에서 토큰 확인
                    if (fileCookies != null && fileCookies.TryGetValue("_m_h5_tk", out var h5tk))
                    {
                        fileToken = h5tk.Split('_')[0];
                    }
                    
                    // ⭐ 로그인 쿠키로도 확인 (lgc, unb, lid 중 하나라도 있으면 로그인됨)
                    if (fileCookies != null && !hasLoggedIn)
                    {
                        hasLoggedIn = fileCookies.ContainsKey("lgc") || 
                                      fileCookies.ContainsKey("unb") || 
                                      fileCookies.ContainsKey("lid");
                    }
                }
                
                // 메모리 토큰이 없으면 파일 토큰 사용, 또는 로그인 쿠키 확인
                var hasToken = !string.IsNullOrEmpty(_taobaoToken) || !string.IsNullOrEmpty(fileToken) || hasLoggedIn;
                var tokenPreview = !string.IsNullOrEmpty(_taobaoToken) ? _taobaoToken : fileToken;
                
                var result = new
                {
                    success = true,
                    memoryCookieCount = _taobaoCookies.Count,
                    fileCookieCount = fileCookieCount,
                    hasToken = hasToken,
                    tokenPreview = !string.IsNullOrEmpty(tokenPreview) ? 
                        tokenPreview.Substring(0, Math.Min(10, tokenPreview.Length)) + "..." : "",
                    message = $"메모리 쿠키 {_taobaoCookies.Count}개, 파일 쿠키 {fileCookieCount}개, 토큰 {(hasToken ? "있음" : "없음")}"
                };
                
                // Results.Ok()가 빈 응답을 보내는 버그 우회
                context.Response.ContentType = "application/json; charset=utf-8";
                await context.Response.WriteAsync(JsonSerializer.Serialize(result));
                return Results.Ok();
            }
            catch (Exception ex)
            {
                context.Response.ContentType = "application/json; charset=utf-8";
                await context.Response.WriteAsync(JsonSerializer.Serialize(new { error = ex.Message }));
                return Results.Ok();
            }
        }
        
        // ⭐ 타오바오 이미지 검색 결과 저장용
        private static List<TaobaoProduct>? _lastImageSearchResults = null;
        private static readonly object _imageSearchLock = new object();
        
        // ⭐ 타오바오 이미지 검색 핸들러 (확장프로그램에서 결과 수신)
        private async Task<IResult> HandleTaobaoImageSearch(HttpContext context)
        {
            try
            {
                using var reader = new StreamReader(context.Request.Body);
                var body = await reader.ReadToEndAsync();
                var data = JsonSerializer.Deserialize<JsonElement>(body);
                
                var productId = data.TryGetProperty("productId", out var pid) ? pid.GetInt32() : 0;
                
                if (data.TryGetProperty("success", out var successProp) && successProp.GetBoolean())
                {
                    if (data.TryGetProperty("products", out var productsProp))
                    {
                        var products = new List<TaobaoProduct>();
                        foreach (var item in productsProp.EnumerateArray())
                        {
                            products.Add(new TaobaoProduct
                            {
                                ProductId = item.TryGetProperty("nid", out var nid) ? nid.GetString() ?? "" : "",
                                Title = item.TryGetProperty("title", out var title) ? title.GetString() ?? "" : "",
                                Price = item.TryGetProperty("price", out var price) ? price.GetString() ?? "" : "",
                                ImageUrl = item.TryGetProperty("imageUrl", out var img) ? img.GetString() ?? "" : "",
                                Sales = item.TryGetProperty("sales", out var sales) ? sales.GetString() ?? "" : ""
                            });
                        }
                        
                        // productId별로 결과 저장
                        lock (_searchLock)
                        {
                            _searchResults[productId] = products;
                        }
                        
                        LogWindow.AddLogStatic($"✅ 타오바오 검색 결과: 상품 {productId} → {products.Count}개");
                        return Results.Ok(new { success = true, count = products.Count });
                    }
                }
                else if (data.TryGetProperty("error", out var errorProp))
                {
                    var error = errorProp.GetString();
                    LogWindow.AddLogStatic($"❌ 타오바오 검색 실패: {error}");
                    return Results.Ok(new { success = false, error = error });
                }
                
                return Results.Ok(new { success = false, error = "알 수 없는 응답" });
            }
            catch (Exception ex)
            {
                LogWindow.AddLogStatic($"❌ 이미지 검색 오류: {ex.Message}");
                return Results.BadRequest(new { error = ex.Message });
            }
        }
        
        // ⭐ 프록시 기반 타오바오 이미지 검색 (Chrome 확장프로그램에 위임)
        private async Task<IResult> HandleTaobaoProxySearch(HttpContext context)
        {
            try
            {
                using var reader = new StreamReader(context.Request.Body);
                var body = await reader.ReadToEndAsync();
                var request = JsonSerializer.Deserialize<TaobaoProxySearchRequest>(body);
                
                if (request == null)
                {
                    return Results.BadRequest(new { error = "요청 데이터가 필요합니다" });
                }
                
                LogWindow.AddLogStatic($"🔍 타오바오 이미지 검색 시작: 상품 {request.ProductId}");
                
                // 이미지 경로 확인
                string imagePath = request.ImagePath ?? "";
                byte[] imageBytes;
                
                if (!string.IsNullOrEmpty(imagePath) && File.Exists(imagePath))
                {
                    imageBytes = await File.ReadAllBytesAsync(imagePath);
                }
                else if (!string.IsNullOrEmpty(request.ImageBase64))
                {
                    imageBytes = Convert.FromBase64String(request.ImageBase64);
                }
                else
                {
                    return Results.BadRequest(new { error = "이미지 데이터가 필요합니다" });
                }
                
                // ⭐ C# 서버에서 직접 프록시로 타오바오 API 호출
                LogWindow.AddLogStatic($"📤 C# 서버에서 프록시 기반 타오바오 검색 시작");
                var foundProducts = await SearchTaobaoWithProxy(imageBytes);
                
                if (foundProducts != null && foundProducts.Count > 0)
                {
                    LogWindow.AddLogStatic($"✅ 검색 완료: {foundProducts.Count}개 상품 발견");
                    var responseJson = JsonSerializer.Serialize(new { success = true, products = foundProducts, count = foundProducts.Count });
                    context.Response.ContentType = "application/json; charset=utf-8";
                    await context.Response.WriteAsync(responseJson);
                    return Results.Ok();
                }
                else
                {
                    LogWindow.AddLogStatic($"⚠️ 검색 결과 없음");
                    context.Response.ContentType = "application/json; charset=utf-8";
                    await context.Response.WriteAsync(JsonSerializer.Serialize(new { success = false, products = new List<TaobaoProduct>(), count = 0, error = "결과 없음" }));
                    return Results.Ok();
                }
            }
            catch (Exception ex)
            {
                LogWindow.AddLogStatic($"❌ 검색 오류: {ex.Message}");
                context.Response.ContentType = "application/json; charset=utf-8";
                context.Response.StatusCode = 400;
                await context.Response.WriteAsync(JsonSerializer.Serialize(new { error = ex.Message }));
                return Results.Ok();
            }
        }
        
        // ⭐ 프록시 기반 타오바오 이미지 검색 (Chrome 확장 방식과 동일)
        private static async Task<List<TaobaoProduct>> SearchTaobaoWithProxy(byte[] imageBytes)
        {
            var products = new List<TaobaoProduct>();
            
            try
            {
                // 1. 타오바오 쿠키 로드
                var cookiePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Predvia", "taobao_cookies.json");
                string cookieString = "";
                string? token = null;
                
                if (File.Exists(cookiePath))
                {
                    var cookieJson = await File.ReadAllTextAsync(cookiePath);
                    var cookies = JsonSerializer.Deserialize<Dictionary<string, string>>(cookieJson);
                    if (cookies != null)
                    {
                        cookieString = string.Join("; ", cookies.Select(c => $"{c.Key}={c.Value}"));
                        if (cookies.TryGetValue("_m_h5_tk", out var h5tk))
                        {
                            token = h5tk.Split('_')[0];
                            LogWindow.AddLogStatic($"🔑 토큰: {token?.Substring(0, Math.Min(8, token?.Length ?? 0))}...");
                        }
                    }
                }
                
                if (string.IsNullOrEmpty(token))
                {
                    LogWindow.AddLogStatic("⚠️ 타오바오 로그인 필요");
                    return products;
                }
                
                // 2. Base64 이미지 준비 (Chrome 확장과 동일한 방식)
                var strimg = Convert.ToBase64String(imageBytes).TrimEnd('=');
                LogWindow.AddLogStatic($"🖼️ strimg 길이: {strimg.Length}");
                
                // 3. mtop API 직접 호출 (프록시 사용)
                for (int attempt = 0; attempt < 5 && products.Count == 0; attempt++)
                {
                    var proxy = GetRandomProxy();
                    LogWindow.AddLogStatic($"🔄 시도 {attempt + 1}/5 (프록시: {proxy ?? "없음"})");
                    
                    try
                    {
                        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                        var appKey = "12574478";
                        
                        // Chrome 확장과 동일한 params 구조
                        var paramsObj = new {
                            strimg = strimg,
                            pcGraphSearch = true,
                            sortOrder = 0,
                            tab = "all",
                            vm = "nv"
                        };
                        var paramsJson = JsonSerializer.Serialize(paramsObj);
                        var dataObj = new { @params = paramsJson, appId = "34850" };
                        var dataJson = JsonSerializer.Serialize(dataObj);
                        
                        var sign = GenerateMd5Sign($"{token}&{timestamp}&{appKey}&{dataJson}");
                        
                        var apiUrl = $"https://h5api.m.taobao.com/h5/mtop.relationrecommend.wirelessrecommend.recommend/2.0/?" +
                            $"jsv=2.7.2&appKey={appKey}&t={timestamp}&sign={sign}" +
                            $"&api=mtop.relationrecommend.wirelessrecommend.recommend&v=2.0" +
                            $"&type=json&dataType=json";
                        
                        var handler = new HttpClientHandler { UseCookies = false };
                        // 프록시 비활성화
                        // if (!string.IsNullOrEmpty(proxy))
                        // {
                        //     handler.Proxy = new System.Net.WebProxy($"http://{proxy}");
                        //     handler.UseProxy = true;
                        // }
                        
                        using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(15) };
                        client.DefaultRequestHeaders.Add("Referer", "https://www.taobao.com/");
                        client.DefaultRequestHeaders.Add("Origin", "https://www.taobao.com");
                        client.DefaultRequestHeaders.Add("User-Agent", GetRandomUserAgent());
                        client.DefaultRequestHeaders.Add("Cookie", cookieString);
                        
                        // POST 요청 (Chrome 확장과 동일)
                        var postContent = new StringContent($"data={Uri.EscapeDataString(dataJson)}", 
                            System.Text.Encoding.UTF8, "application/x-www-form-urlencoded");
                        
                        var response = await client.PostAsync(apiUrl, postContent);
                        var responseText = await response.Content.ReadAsStringAsync();
                        
                        LogWindow.AddLogStatic($"📥 응답: {responseText.Substring(0, Math.Min(200, responseText.Length))}");
                        
                        // JSON 파싱
                        var json = JsonSerializer.Deserialize<JsonElement>(responseText);
                        if (json.TryGetProperty("data", out var data) && data.TryGetProperty("itemsArray", out var itemsArray))
                        {
                            foreach (var item in itemsArray.EnumerateArray().Take(10))
                            {
                                var product = new TaobaoProduct
                                {
                                    ProductId = item.TryGetProperty("nid", out var nid) ? nid.GetString() ?? "" : "",
                                    Title = item.TryGetProperty("title", out var title) ? title.GetString() ?? "" : "",
                                    Price = "",
                                    ImageUrl = "",
                                    Sales = "",
                                    ShopName = "",
                                    ProductUrl = ""
                                };
                                
                                // 가격
                                if (item.TryGetProperty("priceInfo", out var priceInfo))
                                {
                                    if (priceInfo.TryGetProperty("wapFinalPrice", out var wfp))
                                        product.Price = $"¥{wfp}";
                                    else if (priceInfo.TryGetProperty("pcFinalPrice", out var pfp))
                                        product.Price = $"¥{pfp}";
                                }
                                
                                // 이미지
                                if (item.TryGetProperty("pics", out var pics) && pics.TryGetProperty("mainPic", out var mainPic))
                                {
                                    var imgUrl = mainPic.GetString() ?? "";
                                    if (!string.IsNullOrEmpty(imgUrl) && !imgUrl.StartsWith("http"))
                                        imgUrl = "https:" + imgUrl;
                                    product.ImageUrl = imgUrl;
                                }
                                
                                // 판매량
                                if (item.TryGetProperty("salesInfo", out var salesInfo) && salesInfo.TryGetProperty("totalSale", out var totalSale))
                                    product.Sales = totalSale.GetString() ?? "";
                                
                                // 상점명
                                if (item.TryGetProperty("sellerInfo", out var sellerInfo) && sellerInfo.TryGetProperty("shopTitle", out var shopTitle))
                                    product.ShopName = shopTitle.GetString() ?? "";
                                
                                // URL
                                if (item.TryGetProperty("auctionUrl", out var auctionUrl))
                                    product.ProductUrl = auctionUrl.GetString() ?? "";
                                else
                                    product.ProductUrl = $"https://item.taobao.com/item.htm?id={product.ProductId}";
                                
                                products.Add(product);
                            }
                            
                            LogWindow.AddLogStatic($"📦 상품 {products.Count}개 파싱 완료");
                        }
                        else if (responseText.Contains("SCENE_FLOW_CONTROL"))
                        {
                            LogWindow.AddLogStatic($"⚠️ QPS 제한 - 다른 프록시로 재시도");
                            await Task.Delay(1000);
                        }
                    }
                    catch (Exception ex)
                    {
                        LogWindow.AddLogStatic($"⚠️ 시도 {attempt + 1} 실패: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                LogWindow.AddLogStatic($"❌ 프록시 검색 오류: {ex.Message}");
            }
            
            return products;
        }
        
        // ⭐ Chrome 확장프로그램과 동일한 방식의 타오바오 이미지 검색
        private static async Task<List<TaobaoProduct>> SearchTaobaoWithCookieApi(byte[] imageBytes)
        {
            var products = new List<TaobaoProduct>();
            
            try
            {
                // 1. 타오바오 쿠키 로드
                var cookiePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Predvia", "taobao_cookies.json");
                string cookieString = "";
                string? token = null;
                
                if (File.Exists(cookiePath))
                {
                    var cookieJson = await File.ReadAllTextAsync(cookiePath);
                    var cookies = JsonSerializer.Deserialize<Dictionary<string, string>>(cookieJson);
                    if (cookies != null)
                    {
                        cookieString = string.Join("; ", cookies.Select(c => $"{c.Key}={c.Value}"));
                        if (cookies.TryGetValue("_m_h5_tk", out var h5tk))
                        {
                            token = h5tk.Split('_')[0];
                            LogWindow.AddLogStatic($"🔑 토큰: {token?.Substring(0, Math.Min(8, token?.Length ?? 0))}...");
                        }
                    }
                }
                
                if (string.IsNullOrEmpty(token))
                {
                    LogWindow.AddLogStatic("⚠️ 타오바오 로그인 필요");
                    return products;
                }
                
                // 2. 이미지 업로드 (첫 시도는 프록시 없이, 실패하면 프록시로 재시도)
                string? imageUrl = null;
                for (int i = 0; i < 3 && string.IsNullOrEmpty(imageUrl); i++)
                {
                    var proxy = i == 0 ? null : GetRandomProxy(); // 첫 시도는 프록시 없이
                    if (i > 0) LogWindow.AddLogStatic($"🔄 이미지 업로드 재시도 {i+1}/3 (프록시: {proxy ?? "없음"})");
                    imageUrl = await UploadImageToTaobaoServer(imageBytes, cookieString, proxy);
                }
                
                if (string.IsNullOrEmpty(imageUrl))
                {
                    LogWindow.AddLogStatic("⚠️ 이미지 업로드 실패");
                    return products;
                }
                
                LogWindow.AddLogStatic($"📤 이미지 업로드 성공");
                
                // 3. mtop API 호출 (첫 시도는 프록시 없이, 실패하면 프록시로 재시도)
                for (int i = 0; i < 3 && products.Count == 0; i++)
                {
                    var proxy = i == 0 ? null : GetRandomProxy(); // 첫 시도는 프록시 없이
                    if (i > 0) LogWindow.AddLogStatic($"🔄 API 호출 재시도 {i+1}/3 (프록시: {proxy ?? "없음"})");
                    
                    try
                    {
                        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                        var appKey = "12574478";
                        var data = JsonSerializer.Serialize(new { imageUrl, extendInfo = "{}", p = "mm_26632258_3504122_32538762" });
                        var sign = GenerateMd5Sign($"{token}&{timestamp}&{appKey}&{data}");
                        
                        var apiUrl = $"https://h5api.m.taobao.com/h5/mtop.relationrecommend.wirelessrecommend.recommend/2.0/?" +
                            $"jsv=2.6.1&appKey={appKey}&t={timestamp}&sign={sign}" +
                            $"&api=mtop.relationrecommend.wirelessrecommend.recommend&v=2.0" +
                            $"&type=jsonp&dataType=jsonp&callback=mtopjsonp1&data={Uri.EscapeDataString(data)}";
                        
                        var handler = new HttpClientHandler { UseCookies = false };
                        // 프록시 비활성화
                        // if (!string.IsNullOrEmpty(proxy))
                        // {
                        //     handler.Proxy = new System.Net.WebProxy($"http://{proxy}");
                        //     handler.UseProxy = true;
                        // }
                        
                        using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(30) };
                        client.DefaultRequestHeaders.Add("Referer", "https://s.taobao.com/");
                        client.DefaultRequestHeaders.Add("User-Agent", GetRandomUserAgent());
                        client.DefaultRequestHeaders.Add("Cookie", cookieString);
                        
                        var response = await client.GetAsync(apiUrl);
                        var responseText = await response.Content.ReadAsStringAsync();
                        
                        var jsonStart = responseText.IndexOf('(');
                        var jsonEnd = responseText.LastIndexOf(')');
                        if (jsonStart >= 0 && jsonEnd > jsonStart)
                        {
                            var jsonStr = responseText.Substring(jsonStart + 1, jsonEnd - jsonStart - 1);
                            products = ParseTaobaoApiResponse(jsonStr);
                        }
                    }
                    catch (Exception ex)
                    {
                        LogWindow.AddLogStatic($"⚠️ 시도 {i+1} 실패: {ex.Message}");
                    }
                }
                
                if (products.Count == 0)
                {
                    LogWindow.AddLogStatic("🔄 결과 없음, 1688 API 시도...");
                    products = await Search1688Api(imageBytes);
                }
            }
            catch (Exception ex)
            {
                LogWindow.AddLogStatic($"❌ API 호출 오류: {ex.Message}");
            }
            
            return products;
        }
        
        // 타오바오 서버에 이미지 업로드
        private static async Task<string?> UploadImageToTaobaoServer(byte[] imageBytes, string cookieString, string? proxy = null)
        {
            try
            {
                var handler = new HttpClientHandler 
                { 
                    UseCookies = false,
                    AllowAutoRedirect = true,  // 리다이렉트 자동 따라가기
                    MaxAutomaticRedirections = 5
                };
                // 프록시 비활성화
                // if (!string.IsNullOrEmpty(proxy))
                // {
                //     handler.Proxy = new System.Net.WebProxy($"http://{proxy}");
                //     handler.UseProxy = true;
                // }
                
                using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(30) };
                
                var boundary = "----WebKitFormBoundary" + Guid.NewGuid().ToString("N").Substring(0, 16);
                var content = new MultipartFormDataContent(boundary);
                var imageContent = new ByteArrayContent(imageBytes);
                imageContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg");
                content.Add(imageContent, "imgfile", "search.jpg");
                
                var uploadUrl = $"https://s.taobao.com/image?_ksTS={DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}_70&callback=jsonp";
                
                client.DefaultRequestHeaders.Add("Referer", "https://s.taobao.com/search?imgfile=&js=1&stats_click=search_radio_all%3A1&initiative_id=staobaoz_20200101&ie=utf8&tfsid=&app=imgsearch");
                client.DefaultRequestHeaders.Add("Origin", "https://s.taobao.com");
                client.DefaultRequestHeaders.Add("User-Agent", GetRandomUserAgent());
                client.DefaultRequestHeaders.Add("Accept", "*/*");
                client.DefaultRequestHeaders.Add("Accept-Language", "zh-CN,zh;q=0.9,en;q=0.8");
                if (!string.IsNullOrEmpty(cookieString))
                    client.DefaultRequestHeaders.Add("Cookie", cookieString);
                
                LogWindow.AddLogStatic($"📤 업로드 시도: {imageBytes.Length} bytes, 프록시: {proxy ?? "없음"}");
                
                var response = await client.PostAsync(uploadUrl, content);
                var responseText = await response.Content.ReadAsStringAsync();
                
                LogWindow.AddLogStatic($"📥 업로드 응답: {response.StatusCode}, {responseText.Substring(0, Math.Min(200, responseText.Length))}");
                
                // JSONP 파싱
                var jsonStart = responseText.IndexOf('(');
                var jsonEnd = responseText.LastIndexOf(')');
                if (jsonStart >= 0 && jsonEnd > jsonStart)
                {
                    var jsonStr = responseText.Substring(jsonStart + 1, jsonEnd - jsonStart - 1);
                    var json = JsonSerializer.Deserialize<JsonElement>(jsonStr);
                    
                    if (json.TryGetProperty("result", out var result))
                    {
                        if (result.TryGetProperty("picUrl", out var picUrl))
                            return picUrl.GetString();
                        if (result.TryGetProperty("tfsId", out var tfsId))
                            return $"https://img.alicdn.com/tfscom/{tfsId.GetString()}";
                    }
                    else
                    {
                        LogWindow.AddLogStatic($"⚠️ result 없음: {jsonStr.Substring(0, Math.Min(100, jsonStr.Length))}");
                    }
                }
                else
                {
                    LogWindow.AddLogStatic($"⚠️ JSONP 파싱 실패");
                }
            }
            catch (Exception ex)
            {
                LogWindow.AddLogStatic($"⚠️ 이미지 업로드 오류: {ex.Message}");
            }
            return null;
        }
        
        // MD5 서명 생성 (Chrome 확장프로그램과 동일)
        private static string GenerateMd5Sign(string input)
        {
            using var md5 = System.Security.Cryptography.MD5.Create();
            var inputBytes = System.Text.Encoding.UTF8.GetBytes(input);
            var hashBytes = md5.ComputeHash(inputBytes);
            return BitConverter.ToString(hashBytes).Replace("-", "").ToLower();
        }
        
        // 타오바오 API 응답 파싱
        private static List<TaobaoProduct> ParseTaobaoApiResponse(string json)
        {
            var products = new List<TaobaoProduct>();
            try
            {
                var root = JsonSerializer.Deserialize<JsonElement>(json);
                
                // data.resultList 에서 상품 추출
                if (root.TryGetProperty("data", out var data) && 
                    data.TryGetProperty("resultList", out var resultList))
                {
                    int count = 0;
                    foreach (var item in resultList.EnumerateArray())
                    {
                        if (count >= 5) break;
                        
                        var nid = item.TryGetProperty("nid", out var n) ? n.ToString() : "";
                        var title = item.TryGetProperty("title", out var t) ? t.GetString() ?? "" : "";
                        var price = item.TryGetProperty("price", out var p) ? p.ToString() : "0";
                        var picUrl = item.TryGetProperty("pic_url", out var pic) ? pic.GetString() ?? "" : "";
                        
                        if (!picUrl.StartsWith("http")) picUrl = "https:" + picUrl;
                        
                        products.Add(new TaobaoProduct
                        {
                            ProductId = nid,
                            Title = title,
                            Price = $"¥{price}",
                            ImageUrl = picUrl,
                            ProductUrl = $"https://item.taobao.com/item.htm?id={nid}"
                        });
                        count++;
                    }
                }
                
                LogWindow.AddLogStatic($"📦 API 파싱 결과: {products.Count}개");
            }
            catch (Exception ex)
            {
                LogWindow.AddLogStatic($"⚠️ API 응답 파싱 오류: {ex.Message}");
            }
            return products;
        }
        
        // ⭐ 타오바오 이미지 검색 (쿠키 사용)
        // 1688 이미지 검색 API
        private static async Task<List<TaobaoProduct>> Search1688Api(byte[] imageBytes)
        {
            var products = new List<TaobaoProduct>();
            try
            {
                using var client = new HttpClient();
                client.Timeout = TimeSpan.FromSeconds(30);
                client.DefaultRequestHeaders.Add("User-Agent", GetRandomUserAgent());
                client.DefaultRequestHeaders.Add("Referer", "https://s.1688.com/");
                
                // 1688 이미지 업로드
                var content = new MultipartFormDataContent();
                var imageContent = new ByteArrayContent(imageBytes);
                imageContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg");
                content.Add(imageContent, "file", "search.jpg");
                
                var uploadUrl = "https://s.1688.com/youyuan/index.htm?tab=imageSearch";
                var response = await client.PostAsync(uploadUrl, content);
                var html = await response.Content.ReadAsStringAsync();
                
                // __INITIAL_STATE__ 또는 상품 데이터 추출
                var regex = new System.Text.RegularExpressions.Regex(@"""offerId""\s*:\s*""?(\d+)""?.*?""subject""\s*:\s*""([^""]+)"".*?""priceInfo"".*?""price""\s*:\s*""([^""]+)"".*?""image""\s*:\s*""([^""]+)""", System.Text.RegularExpressions.RegexOptions.Singleline);
                var matches = regex.Matches(html);
                
                int count = 0;
                foreach (System.Text.RegularExpressions.Match match in matches)
                {
                    if (count >= 5) break;
                    var offerId = match.Groups[1].Value;
                    products.Add(new TaobaoProduct
                    {
                        ProductId = offerId,
                        Title = match.Groups[2].Value,
                        Price = $"¥{match.Groups[3].Value}",
                        ImageUrl = match.Groups[4].Value,
                        ProductUrl = $"https://detail.1688.com/offer/{offerId}.html"
                    });
                    count++;
                }
                
                LogWindow.AddLogStatic($"📦 1688 검색 결과: {products.Count}개");
            }
            catch (Exception ex)
            {
                LogWindow.AddLogStatic($"⚠️ 1688 검색 오류: {ex.Message}");
            }
            return products;
        }
        
        // 타오바오 검색 HTML 파싱
        private static List<TaobaoProduct> ParseTaobaoSearchHtml(string html)
        {
            var products = new List<TaobaoProduct>();
            try
            {
                // g_page_config 또는 __INITIAL_STATE__ 에서 상품 데이터 추출
                var regex = new System.Text.RegularExpressions.Regex(@"""nid""\s*:\s*""?(\d+)""?.*?""title""\s*:\s*""([^""]+)"".*?""view_price""\s*:\s*""([^""]+)"".*?""pic_url""\s*:\s*""([^""]+)""", System.Text.RegularExpressions.RegexOptions.Singleline);
                var matches = regex.Matches(html);
                
                int count = 0;
                foreach (System.Text.RegularExpressions.Match match in matches)
                {
                    if (count >= 5) break;
                    var nid = match.Groups[1].Value;
                    var imgUrl = match.Groups[4].Value;
                    if (!imgUrl.StartsWith("http")) imgUrl = "https:" + imgUrl;
                    
                    products.Add(new TaobaoProduct
                    {
                        ProductId = nid,
                        Title = match.Groups[2].Value,
                        Price = $"¥{match.Groups[3].Value}",
                        ImageUrl = imgUrl,
                        ProductUrl = $"https://item.taobao.com/item.htm?id={nid}"
                    });
                    count++;
                }
                
                LogWindow.AddLogStatic($"📦 타오바오 HTML 파싱: {products.Count}개");
            }
            catch (Exception ex)
            {
                LogWindow.AddLogStatic($"⚠️ HTML 파싱 오류: {ex.Message}");
            }
            return products;
        }
        
        // zhaojiafang API 응답 파싱
        private static List<TaobaoProduct> ParseZhaojiafangResponse(string json)
        {
            var products = new List<TaobaoProduct>();
            try
            {
                var root = JsonSerializer.Deserialize<JsonElement>(json);
                if (root.TryGetProperty("datas", out var datas) && datas.TryGetProperty("goods_list", out var goodsList))
                {
                    foreach (var item in goodsList.EnumerateArray())
                    {
                        // zhaojiafang API 필드명: goods_id, goods_name, goods_price, goods_image_url
                        var goodsId = item.TryGetProperty("goods_id", out var id) ? id.ToString() : "";
                        var product = new TaobaoProduct
                        {
                            ProductId = goodsId,
                            Title = item.TryGetProperty("goods_name", out var name) ? name.GetString() ?? "" : "",
                            Price = item.TryGetProperty("goods_price", out var price) ? price.GetString() ?? "0" : "0",
                            ImageUrl = item.TryGetProperty("goods_image_url", out var img) ? img.GetString() ?? "" : "",
                            ProductUrl = $"https://www.zhaojiafang.com/goods/{goodsId}.html", // zhaojiafang 링크
                            Sales = item.TryGetProperty("sales", out var sales) ? sales.ToString() : "0"
                        };
                        
                        if (!string.IsNullOrEmpty(product.Title))
                        {
                            LogWindow.AddLogStatic($"📦 {product.Title.Substring(0, Math.Min(15, product.Title.Length))}... img={!string.IsNullOrEmpty(product.ImageUrl)}");
                            products.Add(product);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogWindow.AddLogStatic($"⚠️ zhaojiafang 파싱 오류: {ex.Message}");
            }
            return products;
        }
        
        // ⭐ 검색 요청 저장용
        private static Dictionary<int, string> _pendingSearchRequests = new();
        private static Dictionary<int, List<TaobaoProduct>> _searchResults = new();
        private static readonly object _searchLock = new();
        
        // ⭐ 이미지 검색 요청 핸들러 (C# → 확장프로그램)
        private async Task<IResult> HandleTaobaoSearchRequest(HttpContext context)
        {
            try
            {
                using var reader = new StreamReader(context.Request.Body);
                var body = await reader.ReadToEndAsync();
                var data = JsonSerializer.Deserialize<JsonElement>(body);
                
                var productId = data.TryGetProperty("productId", out var pid) ? pid.GetInt32() : 0;
                var imageBase64 = data.TryGetProperty("imageBase64", out var img) ? img.GetString() ?? "" : "";
                var searchId = data.TryGetProperty("searchId", out var sid) ? sid.GetString() ?? "" : "";
                
                if (string.IsNullOrEmpty(imageBase64))
                {
                    return Results.BadRequest(new { error = "imageBase64 필요" });
                }
                
                // searchId가 있으면 content script용으로 저장
                if (!string.IsNullOrEmpty(searchId))
                {
                    lock (_contentScriptLock)
                    {
                        _contentScriptSearchImages[searchId] = imageBase64;
                    }
                    LogWindow.AddLogStatic($"📥 Content Script용 이미지 저장: {searchId}");
                    return Results.Ok(new { success = true, searchId = searchId });
                }
                
                // 기존 방식 (productId 기반)
                if (productId == 0)
                {
                    return Results.BadRequest(new { error = "productId 또는 searchId 필요" });
                }
                
                // 요청 저장 (확장프로그램이 폴링해서 가져감)
                lock (_searchLock)
                {
                    _pendingSearchRequests[productId] = imageBase64;
                }
                
                LogWindow.AddLogStatic($"📥 이미지 검색 요청 저장: 상품 {productId}");
                return Results.Ok(new { success = true, message = "검색 요청 등록됨" });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        }
        
        // ⭐ 검색 결과 조회 핸들러
        private async Task<IResult> HandleTaobaoSearchResult(HttpContext context)
        {
            try
            {
                var productIdStr = context.Request.Query["productId"].ToString();
                if (!int.TryParse(productIdStr, out var productId))
                {
                    return Results.BadRequest(new { error = "productId 필요" });
                }
                
                List<TaobaoProduct>? products = null;
                lock (_searchLock)
                {
                    if (_searchResults.TryGetValue(productId, out var result))
                    {
                        products = result;
                        _searchResults.Remove(productId); // 한 번 조회하면 삭제
                    }
                }
                
                if (products != null && products.Count > 0)
                {
                    return Results.Ok(new { success = true, products = products });
                }
                
                return Results.Ok(new { success = false, message = "결과 없음" });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        }
        
        // ⭐ 대기 중인 검색 요청 조회 (확장프로그램 폴링용)
        private async Task<IResult> HandlePendingSearch(HttpContext context)
        {
            try
            {
                int? productId = null;
                string? imageBase64 = null;
                
                lock (_searchLock)
                {
                    if (_pendingSearchRequests.Count > 0)
                    {
                        var first = _pendingSearchRequests.First();
                        productId = first.Key;
                        imageBase64 = first.Value;
                        _pendingSearchRequests.Remove(first.Key);
                    }
                }
                
                context.Response.ContentType = "application/json";
                
                if (productId.HasValue && imageBase64 != null)
                {
                    LogWindow.AddLogStatic($"📤 검색 요청 전달: 상품 {productId}");
                    var json = JsonSerializer.Serialize(new { hasPending = true, productId = productId.Value, imageBase64 = imageBase64 });
                    await context.Response.WriteAsync(json);
                }
                else
                {
                    await context.Response.WriteAsync("{\"hasPending\":false}");
                }
                
                return Results.Ok();
            }
            catch (Exception ex)
            {
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync($"{{\"hasPending\":false,\"error\":\"{ex.Message}\"}}");
                return Results.Ok();
            }
        }
        
        // ⭐ Content Script용 검색 이미지 데이터 조회
        private static Dictionary<string, string> _contentScriptSearchImages = new();
        private static Dictionary<string, List<TaobaoProduct>> _contentScriptSearchResults = new();
        private static readonly object _contentScriptLock = new();
        
        private async Task<IResult> HandleGetSearchImage(HttpContext context)
        {
            try
            {
                var searchId = context.Request.Query["id"].ToString();
                
                if (string.IsNullOrEmpty(searchId))
                {
                    return Results.BadRequest(new { error = "검색 ID 필요" });
                }
                
                string? imageBase64 = null;
                lock (_contentScriptLock)
                {
                    if (_contentScriptSearchImages.TryGetValue(searchId, out var img))
                    {
                        imageBase64 = img;
                    }
                }
                
                if (imageBase64 != null)
                {
                    LogWindow.AddLogStatic($"📤 검색 이미지 전달: {searchId}");
                    return Results.Ok(new { imageBase64 = imageBase64 });
                }
                
                return Results.NotFound(new { error = "이미지 없음" });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        }
        
        // ⭐ Content Script에서 검색 결과 수신
        private async Task<IResult> HandleImageSearchResult(HttpContext context)
        {
            try
            {
                using var reader = new StreamReader(context.Request.Body);
                var body = await reader.ReadToEndAsync();
                var data = JsonSerializer.Deserialize<JsonElement>(body);
                
                var searchId = data.GetProperty("searchId").GetString() ?? "";
                var success = data.TryGetProperty("success", out var successProp) && successProp.GetBoolean();
                
                if (success && data.TryGetProperty("products", out var productsProp))
                {
                    var products = new List<TaobaoProduct>();
                    foreach (var p in productsProp.EnumerateArray())
                    {
                        products.Add(new TaobaoProduct
                        {
                            ProductId = p.TryGetProperty("nid", out var nid) ? nid.GetString() ?? "" : "",
                            Title = p.TryGetProperty("title", out var title) ? title.GetString() ?? "" : "",
                            Price = p.TryGetProperty("price", out var price) ? price.GetString() ?? "" : "",
                            ImageUrl = p.TryGetProperty("imageUrl", out var img) ? img.GetString() ?? "" : "",
                            Sales = p.TryGetProperty("sales", out var sales) ? sales.GetString() ?? "" : "",
                            ShopName = p.TryGetProperty("shopName", out var shop) ? shop.GetString() ?? "" : ""
                        });
                    }
                    
                    lock (_contentScriptLock)
                    {
                        _contentScriptSearchResults[searchId] = products;
                        _contentScriptSearchImages.Remove(searchId);
                    }
                    
                    LogWindow.AddLogStatic($"✅ 검색 결과 수신: {searchId}, 상품 {products.Count}개");
                }
                else
                {
                    var error = data.TryGetProperty("error", out var errProp) ? errProp.GetString() : "알 수 없는 오류";
                    var needLogin = data.TryGetProperty("needLogin", out var loginProp) && loginProp.GetBoolean();
                    
                    lock (_contentScriptLock)
                    {
                        _contentScriptSearchResults[searchId] = new List<TaobaoProduct>();
                        _contentScriptSearchImages.Remove(searchId);
                    }
                    
                    if (needLogin)
                    {
                        LogWindow.AddLogStatic($"⚠️ 타오바오 로그인 필요: {searchId}");
                    }
                    else
                    {
                        LogWindow.AddLogStatic($"❌ 검색 실패: {searchId} - {error}");
                    }
                }
                
                return Results.Ok(new { success = true });
            }
            catch (Exception ex)
            {
                LogWindow.AddLogStatic($"❌ 검색 결과 처리 오류: {ex.Message}");
                return Results.BadRequest(new { error = ex.Message });
            }
        }
        
        // ⭐ imgur 이미지 업로드 (프록시 사용)
        private async Task<IResult> HandleImgurUpload(HttpContext context)
        {
            context.Response.ContentType = "application/json; charset=utf-8";
            
            try
            {
                using var reader = new StreamReader(context.Request.Body);
                var body = await reader.ReadToEndAsync();
                var data = JsonSerializer.Deserialize<JsonElement>(body);
                
                var base64 = data.GetProperty("image").GetString() ?? "";
                if (base64.Contains(",")) base64 = base64.Split(',')[1];
                
                LogWindow.AddLogStatic($"📤 이미지 ({base64.Length / 1024}KB)");
                
                var imageBytes = Convert.FromBase64String(base64);
                
                // freeimage.host에 업로드 시도
                var url = await UploadToDocsQQ(imageBytes);
                
                if (!string.IsNullOrEmpty(url))
                {
                    LogWindow.AddLogStatic($"✅ freeimage: {url}");
                    await context.Response.WriteAsync($"{{\"success\":true,\"url\":\"{url}\"}}");
                }
                else
                {
                    // 실패 시 로컬 저장
                    var fileName = $"temp_{Guid.NewGuid():N}.jpg";
                    var tempDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Predvia", "TempImages");
                    Directory.CreateDirectory(tempDir);
                    var filePath = Path.Combine(tempDir, fileName);
                    await File.WriteAllBytesAsync(filePath, imageBytes);
                    url = $"http://localhost:8080/temp-image/{fileName}";
                    LogWindow.AddLogStatic($"⚠️ 로컬 폴백: {url}");
                    await context.Response.WriteAsync($"{{\"success\":true,\"url\":\"{url}\"}}");
                }
            }
            catch (Exception ex)
            {
                LogWindow.AddLogStatic($"❌ 오류: {ex.Message}");
                await context.Response.WriteAsync($"{{\"success\":false,\"error\":\"{ex.Message.Replace("\"", "'")}\"}}");
            }
            
            return Results.Ok();
        }
        
        // ⭐ freeimage.host 이미지 업로드 (무료 CDN)
        private static async Task<string?> UploadToDocsQQ(byte[] imageBytes)
        {
            try
            {
                using var client = new HttpClient();
                client.Timeout = TimeSpan.FromSeconds(30);
                
                using var content = new MultipartFormDataContent();
                content.Add(new StringContent("6d207e02198a847aa98d0a2a901485a5"), "key");
                content.Add(new StringContent("json"), "format");
                
                var imageContent = new ByteArrayContent(imageBytes);
                imageContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg");
                content.Add(imageContent, "source", $"img_{DateTime.Now.Ticks}.jpg");
                
                var response = await client.PostAsync("https://freeimage.host/api/1/upload", content);
                var result = await response.Content.ReadAsStringAsync();
                
                LogWindow.AddLogStatic($"📥 freeimage 응답: {result.Substring(0, Math.Min(200, result.Length))}");
                
                var json = JsonSerializer.Deserialize<JsonElement>(result);
                if (json.TryGetProperty("image", out var img) && img.TryGetProperty("url", out var urlProp))
                {
                    return urlProp.GetString();
                }
            }
            catch (Exception ex)
            {
                LogWindow.AddLogStatic($"❌ freeimage 오류: {ex.Message}");
            }
            return null;
        }
        
        private async Task<IResult> HandleTempImage(HttpContext context, string fileName)
        {
            var tempDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Predvia", "TempImages");
            var filePath = Path.Combine(tempDir, fileName);
            
            if (File.Exists(filePath))
            {
                context.Response.ContentType = "image/jpeg";
                await context.Response.Body.WriteAsync(await File.ReadAllBytesAsync(filePath));
            }
            else
            {
                context.Response.StatusCode = 404;
            }
            
            return Results.Ok();
        }
        
        // ⭐ 파일에서 타오바오 쿠키 로드
        // ⭐ 프록시 목록 로드 (모모아이피)
        private static void LoadProxyList()
        {
            try
            {
                var proxyPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "image_search_products-master", "프록시유동_모모아이피.txt");
                
                // 대안 경로들
                var altPaths = new[]
                {
                    proxyPath,
                    Path.Combine(Directory.GetCurrentDirectory(), "image_search_products-master", "프록시유동_모모아이피.txt"),
                    @"C:\GITHUB\Gumaedaehang\image_search_products-master\프록시유동_모모아이피.txt"
                };
                
                foreach (var path in altPaths)
                {
                    if (File.Exists(path))
                    {
                        var lines = File.ReadAllLines(path);
                        lock (_proxyLock)
                        {
                            _proxyList = lines.Where(l => !string.IsNullOrWhiteSpace(l)).ToList();
                        }
                        LogWindow.AddLogStatic($"✅ 프록시 {_proxyList.Count}개 로드 완료: {path}");
                        return;
                    }
                }
                
                LogWindow.AddLogStatic("⚠️ 프록시 파일을 찾을 수 없습니다");
            }
            catch (Exception ex)
            {
                LogWindow.AddLogStatic($"❌ 프록시 로드 오류: {ex.Message}");
            }
        }
        
        // ⭐ 랜덤 프록시 선택
        private static string? GetRandomProxy()
        {
            lock (_proxyLock)
            {
                if (_proxyList.Count == 0) return null;
                return _proxyList[_proxyRandom.Next(_proxyList.Count)];
            }
        }
        
        // ⭐ 프록시를 사용하는 HttpClient 생성
        private static HttpClient CreateHttpClientWithProxy(string? proxyAddress = null)
        {
            if (string.IsNullOrEmpty(proxyAddress))
            {
                proxyAddress = GetRandomProxy();
            }
            
            HttpClientHandler handler;
            
            if (!string.IsNullOrEmpty(proxyAddress))
            {
                var proxy = new WebProxy($"http://{proxyAddress}");
                handler = new HttpClientHandler
                {
                    Proxy = proxy,
                    UseProxy = true
                };
                LogWindow.AddLogStatic($"🔄 프록시 사용: {proxyAddress}");
            }
            else
            {
                handler = new HttpClientHandler();
                LogWindow.AddLogStatic("⚠️ 프록시 없이 직접 연결");
            }
            
            var client = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromSeconds(30)
            };
            
            client.DefaultRequestHeaders.Add("User-Agent", GetRandomUserAgent());
            
            return client;
        }
        
        private static async Task LoadTaobaoCookiesFromFile()
        {
            try
            {
                var cookiesPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Predvia", "taobao_cookies.json");
                
                LogWindow.AddLogStatic($"🔍 쿠키 파일 경로: {cookiesPath}");
                
                if (File.Exists(cookiesPath))
                {
                    LogWindow.AddLogStatic("✅ 쿠키 파일 존재 확인");
                    var fileContent = await File.ReadAllTextAsync(cookiesPath);
                    LogWindow.AddLogStatic($"📄 파일 내용 길이: {fileContent.Length}자");
                    
                    var fileCookies = JsonSerializer.Deserialize<Dictionary<string, string>>(fileContent);
                    
                    if (fileCookies != null && fileCookies.Count > 0)
                    {
                        LogWindow.AddLogStatic($"🍪 파일에서 {fileCookies.Count}개 쿠키 발견");
                        _taobaoCookies.Clear();
                        
                        foreach (var cookie in fileCookies)
                        {
                            _taobaoCookies[cookie.Key] = cookie.Value;
                            
                            // _m_h5_tk 토큰 추출 (전체 토큰 저장 - 타임스탬프 포함)
                            if (cookie.Key == "_m_h5_tk" && !string.IsNullOrEmpty(cookie.Value))
                            {
                                _taobaoToken = cookie.Value; // 전체 토큰 저장 (예: token_timestamp)
                            }
                        }
                        
                        LogWindow.AddLogStatic($"✅ 파일에서 타오바오 쿠키 {_taobaoCookies.Count}개 로드 완료");
                    }
                }
            }
            catch (Exception ex)
            {
                LogWindow.AddLogStatic($"⚠️ 쿠키 파일 로드 실패: {ex.Message}");
            }
        }
        
        // ⭐ 타오바오 로그인 페이지 열기
        private async Task OpenTaobaoLoginPage()
        {
            IBrowser? browser = null;
            IPage? page = null;
            
            try
            {
                LogWindow.AddLogStatic("🌐 Chrome 다운로드 중...");
                
                var browserFetcher = new BrowserFetcher();
                var revisionInfo = await browserFetcher.DownloadAsync();
                
                LogWindow.AddLogStatic("✅ Chrome 다운로드 완료");
                
                // ⭐ Predvia 전용 프로필 사용
                var profilePath = GetPredviaChromeProfile();
                LogWindow.AddLogStatic($"📁 Chrome 프로필: {profilePath}");
                
                browser = await Puppeteer.LaunchAsync(new LaunchOptions
                {
                    Headless = false,
                    ExecutablePath = revisionInfo.GetExecutablePath(),
                    UserDataDir = profilePath,  // ⭐ 핵심: 프로필 지정
                    Args = new[] { 
                        "--start-maximized",
                        "--disable-blink-features=AutomationControlled"
                    },
                    DefaultViewport = null
                });
                
                LogWindow.AddLogStatic("✅ Chrome 실행 성공");
                
                page = await browser.NewPageAsync();
                
                // 타오바오 로그인 페이지로 이동
                await page.GoToAsync("https://login.taobao.com/", new NavigationOptions
                {
                    WaitUntil = new[] { WaitUntilNavigation.Networkidle0 },
                    Timeout = 30000
                });
                
                LogWindow.AddLogStatic("🌐 타오바오 로그인 페이지 로드 완료");
                LogWindow.AddLogStatic("👤 사용자가 로그인을 완료하면 창을 닫으세요");
                LogWindow.AddLogStatic("💾 로그인 정보는 자동으로 저장됩니다");
                
                // 사용자가 로그인할 때까지 대기 (창을 열어둠)
                // 사용자가 수동으로 창을 닫으면 종료
            }
            catch (Exception ex)
            {
                LogWindow.AddLogStatic($"❌ 타오바오 로그인 페이지 오류: {ex.Message}");
                
                if (page != null)
                {
                    try { await page.CloseAsync(); } catch { }
                }
                
                throw;
            }
        }
        
        // ⭐ 타오바오 이미지 업로드 핸들러
        private async Task<IResult> HandleTaobaoImageUpload(HttpContext context)
        {
            try
            {
                var requestData = await context.Request.ReadFromJsonAsync<TaobaoImageUploadRequest>();
                if (requestData == null || string.IsNullOrEmpty(requestData.ImagePath))
                {
                    return Results.BadRequest(new { error = "이미지 경로가 필요합니다" });
                }
                
                LogWindow.AddLogStatic($"🔍 타오바오 이미지 업로드 요청: {requestData.ProductId}");
                
                // 파이썬에서 이미 처리된 상품 데이터 사용
                var products = requestData.Products ?? new List<TaobaoProduct>();
                
                if (products.Count > 0)
                {
                    LogWindow.AddLogStatic($"✅ 타오바오 이미지 업로드 완료: {requestData.ProductId}");
                    LogWindow.AddLogStatic($"📦 타오바오 상품 {products.Count}개 수집 완료");
                }
                else
                {
                    LogWindow.AddLogStatic("❌ 파이썬에서 상품 데이터를 받지 못했습니다.");
                    return Results.BadRequest(new { error = "상품 데이터가 없습니다." });
                }
                
                LogWindow.AddLogStatic($"✅ 타오바오 이미지 업로드 완료: {requestData.ProductId}");
                LogWindow.AddLogStatic($"📦 타오바오 상품 {products.Count}개 수집 완료");
                
                // 명시적 JSON 응답 작성
                var responseJson = JsonSerializer.Serialize(new 
                { 
                    success = true, 
                    message = "이미지 업로드 완료", 
                    products = products 
                });
                
                context.Response.ContentType = "application/json; charset=utf-8";
                await context.Response.WriteAsync(responseJson);
                return Results.Ok();
            }
            catch (Exception ex)
            {
                LogWindow.AddLogStatic($"❌ 타오바오 이미지 업로드 오류: {ex.Message}");
                
                var errorJson = JsonSerializer.Serialize(new { error = ex.Message });
                context.Response.ContentType = "application/json; charset=utf-8";
                context.Response.StatusCode = 400;
                await context.Response.WriteAsync(errorJson);
                return Results.Ok();
            }
        }
        
        // ⭐ 타오바오 검색 결과 파싱
        private static async Task<List<TaobaoProduct>> ParseTaobaoSearchResults(string searchUrl, HttpClient httpClient)
        {
            var products = new List<TaobaoProduct>();
            
            try
            {
                LogWindow.AddLogStatic("📄 타오바오 검색 페이지 요청 중...");
                
                // 검색 페이지 HTML 가져오기
                var response = await httpClient.GetAsync(searchUrl);
                var html = await response.Content.ReadAsStringAsync();
                
                LogWindow.AddLogStatic($"📄 HTML 응답 크기: {html.Length} bytes");
                
                // JSON 데이터 추출 (타오바오는 페이지 내에 JSON 데이터를 포함)
                var jsonPatterns = new[] { "g_page_config = ", "window.g_config = ", "__INITIAL_STATE__ = " };
                var jsonStart = -1;
                var usedPattern = "";
                
                foreach (var pattern in jsonPatterns)
                {
                    jsonStart = html.IndexOf(pattern);
                    if (jsonStart != -1)
                    {
                        usedPattern = pattern;
                        break;
                    }
                }
                
                if (jsonStart == -1)
                {
                    LogWindow.AddLogStatic("❌ 페이지에서 상품 데이터를 찾을 수 없습니다 (모든 패턴 시도)");
                    return products;
                }
                
                jsonStart += usedPattern.Length;
                LogWindow.AddLogStatic($"🔍 JSON 패턴 발견: {usedPattern}");
                var jsonEnd = html.IndexOf(";</script>", jsonStart);
                if (jsonEnd == -1)
                {
                    LogWindow.AddLogStatic("❌ JSON 데이터 끝을 찾을 수 없습니다");
                    return products;
                }
                
                var jsonData = html.Substring(jsonStart, jsonEnd - jsonStart);
                LogWindow.AddLogStatic("🔍 상품 데이터 JSON 추출 완료");
                
                // JSON 파싱
                var pageConfig = JsonSerializer.Deserialize<JsonElement>(jsonData);
                
                if (pageConfig.TryGetProperty("mods", out var mods) &&
                    mods.TryGetProperty("itemlist", out var itemlist) &&
                    itemlist.TryGetProperty("data", out var data) &&
                    data.TryGetProperty("auctions", out var auctions))
                {
                    var count = 0;
                    foreach (var auction in auctions.EnumerateArray())
                    {
                        if (count >= 5) break; // 최대 5개만
                        
                        var product = new TaobaoProduct();
                        
                        // 상품명
                        if (auction.TryGetProperty("raw_title", out var title))
                        {
                            product.Title = title.GetString() ?? "제목 없음";
                        }
                        
                        // 가격
                        if (auction.TryGetProperty("view_price", out var price))
                        {
                            product.Price = $"¥ {price.GetString()}";
                        }
                        
                        // 판매량
                        if (auction.TryGetProperty("view_sales", out var sales))
                        {
                            product.Sales = sales.GetString() ?? "0";
                        }
                        
                        // 이미지 URL
                        if (auction.TryGetProperty("pic_url", out var picUrl))
                        {
                            var imageUrl = picUrl.GetString();
                            if (!string.IsNullOrEmpty(imageUrl) && !imageUrl.StartsWith("http"))
                            {
                                imageUrl = "https:" + imageUrl;
                            }
                            product.ImageUrl = imageUrl ?? "";
                        }
                        
                        // 상품 URL
                        if (auction.TryGetProperty("detail_url", out var detailUrl))
                        {
                            var productUrl = detailUrl.GetString();
                            if (!string.IsNullOrEmpty(productUrl) && !productUrl.StartsWith("http"))
                            {
                                productUrl = "https:" + productUrl;
                            }
                            product.ProductUrl = productUrl ?? "";
                        }
                        
                        products.Add(product);
                        count++;
                        
                        LogWindow.AddLogStatic($"📦 상품 {count}: {product.Title} - {product.Price} - 판매량: {product.Sales}");
                    }
                    
                    LogWindow.AddLogStatic($"✅ 총 {products.Count}개 상품 파싱 완료");
                }
                else
                {
                    LogWindow.AddLogStatic("❌ 상품 목록 데이터 구조를 찾을 수 없습니다");
                }
            }
            catch (Exception ex)
            {
                LogWindow.AddLogStatic($"❌ 검색 결과 파싱 오류: {ex.Message}");
            }
            
            return products;
        }
        
        // ⭐ 쿠키 기반 타오바오 이미지 업로드
        private static async Task<List<TaobaoProduct>> UploadImageToTaobaoWithCookies(string imagePath)
        {
            var products = new List<TaobaoProduct>();
            
            try
            {
                // 메모리에 쿠키가 없으면 파일에서 로드 시도
                LogWindow.AddLogStatic($"🔍 현재 상태 - 토큰: {(_taobaoToken ?? "null")}, 쿠키 개수: {_taobaoCookies.Count}");
                
                if (string.IsNullOrEmpty(_taobaoToken) || _taobaoCookies.Count == 0)
                {
                    LogWindow.AddLogStatic("🔄 메모리에 쿠키 없음 - 파일에서 로드 시도");
                    await LoadTaobaoCookiesFromFile();
                    LogWindow.AddLogStatic($"🔍 쿠키 로드 결과: {_taobaoCookies.Count}개, 토큰: {(!string.IsNullOrEmpty(_taobaoToken) ? "있음" : "없음")}");
                }
                else
                {
                    LogWindow.AddLogStatic("✅ 메모리에 쿠키 이미 존재");
                }
                
                // 쿠키와 토큰 확인
                if (string.IsNullOrEmpty(_taobaoToken) || _taobaoCookies.Count == 0)
                {
                    LogWindow.AddLogStatic("❌ 타오바오 쿠키가 없습니다. 먼저 타오바오에 로그인하세요.");
                    return products;
                }
                
                LogWindow.AddLogStatic("🔍 쿠키 기반 타오바오 이미지 검색 시작...");
                
                // 이미지를 Base64로 변환
                var imageBytes = await File.ReadAllBytesAsync(imagePath);
                var base64Image = Convert.ToBase64String(imageBytes).Replace("==", "");
                
                // 타오바오 API 요청 데이터 생성
                var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                var paramsData = JsonSerializer.Serialize(new
                {
                    strimg = base64Image,
                    pcGraphSearch = true,
                    sortOrder = 0,
                    tab = "all",
                    vm = "nv"
                });
                
                var requestData = JsonSerializer.Serialize(new
                {
                    @params = paramsData,
                    appId = "34850"
                });
                
                // 서명 생성
                var sign = GenerateTaobaoSign(requestData, timestamp);
                
                // API 요청
                using var httpClient = new HttpClient();
                
                // 쿠키 헤더 설정
                var cookieHeader = string.Join("; ", _taobaoCookies.Select(kvp => $"{kvp.Key}={kvp.Value}"));
                httpClient.DefaultRequestHeaders.Add("Cookie", cookieHeader);
                httpClient.DefaultRequestHeaders.Add("User-Agent", GetRandomUserAgent());
                httpClient.DefaultRequestHeaders.Add("Referer", "https://www.taobao.com/");
                
                var url = "https://h5api.m.taobao.com/h5/mtop.taobao.wireless.search.imagesearch.upload/1.0/";
                var queryParams = new Dictionary<string, string>
                {
                    ["jsv"] = "2.4.11",
                    ["appKey"] = "12574478",
                    ["t"] = timestamp.ToString(),
                    ["api"] = "mtop.taobao.wireless.search.imagesearch.upload",
                    ["v"] = "1.0",
                    ["type"] = "originaljson",
                    ["dataType"] = "json",
                    ["sign"] = sign
                };
                
                var queryString = string.Join("&", queryParams.Select(kvp => $"{kvp.Key}={kvp.Value}"));
                var fullUrl = $"{url}?{queryString}";
                
                var content = new FormUrlEncodedContent(new[] { new KeyValuePair<string, string>("data", requestData) });
                
                LogWindow.AddLogStatic("📤 타오바오 API 요청 전송 중...");
                
                // 캡차 방지를 위한 대기
                await Task.Delay(3000); // 3초 대기
                
                var response = await httpClient.PostAsync(fullUrl, content);
                var responseText = await response.Content.ReadAsStringAsync();
                
                LogWindow.AddLogStatic($"📥 API 응답 수신: {response.StatusCode}");
                LogWindow.AddLogStatic($"📄 응답 내용: {responseText}");
                
                if (response.IsSuccessStatusCode)
                {
                    // JSON 파싱 시도
                    try
                    {
                        var jsonResponse = JsonSerializer.Deserialize<JsonElement>(responseText);
                        
                        // QPS 제한 감지
                        if (jsonResponse.TryGetProperty("ret", out var retElement) && 
                            retElement.ValueKind == JsonValueKind.Array)
                        {
                            var retArray = retElement.EnumerateArray().ToArray();
                            if (retArray.Length > 0)
                            {
                                var errorMessage = retArray[0].GetString() ?? "";
                                
                                // 캡차 요구 감지 - 재시도 허용
                                if (errorMessage.Contains("FAIL_SYS_USER_VALIDATE") || errorMessage.Contains("captcha"))
                                {
                                    LogWindow.AddLogStatic("🤖 타오바오 캡차 감지 - User-Agent 변경 후 재시도...");
                                    
                                    // 새로운 User-Agent로 재시도
                                    httpClient.DefaultRequestHeaders.Remove("User-Agent");
                                    httpClient.DefaultRequestHeaders.Add("User-Agent", GetRandomUserAgent());
                                    
                                    await Task.Delay(5000); // 5초 대기 (캡차 대응)
                                    
                                    LogWindow.AddLogStatic("🔄 캡차 우회 재시도 중...");
                                    var retryResponse = await httpClient.PostAsync(fullUrl, content);
                                    var retryResponseText = await retryResponse.Content.ReadAsStringAsync();
                                    
                                    LogWindow.AddLogStatic($"📥 재시도 응답: {retryResponse.StatusCode}");
                                    LogWindow.AddLogStatic($"📄 재시도 응답 내용: {retryResponseText}");
                                    
                                    if (retryResponse.IsSuccessStatusCode)
                                    {
                                        responseText = retryResponseText;
                                        response = retryResponse;
                                        // 재파싱을 위해 continue 대신 다시 파싱
                                        jsonResponse = JsonSerializer.Deserialize<JsonElement>(responseText);
                                    }
                                    else
                                    {
                                        LogWindow.AddLogStatic("❌ 캡차 우회 재시도 실패");
                                        LogWindow.AddLogStatic("💡 수동으로 https://www.taobao.com 접속하여 캡차 해결 필요");
                                        return products;
                                    }
                                }
                                else if (errorMessage.Contains("SCENE_FLOW_CONTROL") || errorMessage.Contains("QpsFlowCtrlHandler"))
                                {
                                    LogWindow.AddLogStatic("🚫 QPS 제한 감지 - User-Agent 변경 후 재시도...");
                                    
                                    // 새로운 User-Agent로 재시도
                                    httpClient.DefaultRequestHeaders.Remove("User-Agent");
                                    httpClient.DefaultRequestHeaders.Add("User-Agent", GetRandomUserAgent());
                                    
                                    await Task.Delay(3000); // 3초 대기
                                    
                                    LogWindow.AddLogStatic("🔄 새로운 User-Agent로 재시도 중...");
                                    var retryResponse = await httpClient.PostAsync(fullUrl, content);
                                    var retryResponseText = await retryResponse.Content.ReadAsStringAsync();
                                    
                                    LogWindow.AddLogStatic($"📥 재시도 응답: {retryResponse.StatusCode}");
                                    LogWindow.AddLogStatic($"📄 재시도 응답 내용: {retryResponseText}");
                                    
                                    if (retryResponse.IsSuccessStatusCode)
                                    {
                                        responseText = retryResponseText;
                                        response = retryResponse;
                                        // 재파싱을 위해 continue 대신 다시 파싱
                                        jsonResponse = JsonSerializer.Deserialize<JsonElement>(responseText);
                                    }
                                }
                            }
                        }
                        
                        // 첫 번째 응답에서 직접 상품 데이터 추출 (파이썬 extract_products와 동일)
                        if (jsonResponse.TryGetProperty("data", out var dataElement) &&
                            dataElement.TryGetProperty("itemsArray", out var itemsArrayElement))
                        {
                            LogWindow.AddLogStatic("✅ 첫 번째 응답에서 상품 데이터 직접 추출");
                            
                            var seen = new HashSet<string>();
                            var count = 0;
                            
                            foreach (var item in itemsArrayElement.EnumerateArray())
                            {
                                if (count >= 5) break; // 최대 5개
                                
                                // nid 중복 체크
                                if (!item.TryGetProperty("nid", out var nidElement)) continue;
                                var nid = nidElement.GetString();
                                if (string.IsNullOrEmpty(nid) || seen.Contains(nid)) continue;
                                seen.Add(nid);
                                
                                var product = new TaobaoProduct();
                                
                                // nid
                                product.ProductId = nid;
                                
                                // title
                                product.Title = item.TryGetProperty("title", out var titleElement) ? 
                                               titleElement.GetString() ?? "" : "";
                                
                                // price (priceInfo에서 추출)
                                var price = "";
                                var currency = "";
                                if (item.TryGetProperty("priceInfo", out var priceInfoElement))
                                {
                                    if (priceInfoElement.TryGetProperty("monetaryUnit", out var currencyElement))
                                        currency = currencyElement.GetString() ?? "";
                                    
                                    string priceValue = null;
                                    if (priceInfoElement.TryGetProperty("pcFinalPrice", out var pcPriceElement))
                                        priceValue = pcPriceElement.GetString();
                                    else if (priceInfoElement.TryGetProperty("wapFinalPrice", out var wapPriceElement))
                                        priceValue = wapPriceElement.GetString();
                                    else if (priceInfoElement.TryGetProperty("reservePrice", out var reservePriceElement))
                                        priceValue = reservePriceElement.GetString();
                                    
                                    if (!string.IsNullOrEmpty(priceValue))
                                        price = $"{currency}{priceValue}";
                                }
                                product.Price = price;
                                
                                // url (auctionUrl)
                                product.ProductUrl = item.TryGetProperty("auctionUrl", out var urlElement) ? 
                                                    urlElement.GetString() ?? "" : "";
                                
                                // review_count (comments.nums)
                                var reviewCount = 0;
                                if (item.TryGetProperty("comments", out var commentsElement) &&
                                    commentsElement.TryGetProperty("nums", out var numsElement))
                                {
                                    reviewCount = numsElement.GetInt32();
                                }
                                product.ReviewCount = reviewCount;
                                
                                // shop (sellerInfo.shopTitle)
                                product.ShopName = "";
                                if (item.TryGetProperty("sellerInfo", out var sellerInfoElement) &&
                                    sellerInfoElement.TryGetProperty("shopTitle", out var shopTitleElement))
                                {
                                    product.ShopName = shopTitleElement.GetString() ?? "";
                                }
                                
                                // img (pics.mainPic)
                                if (item.TryGetProperty("pics", out var picsElement) &&
                                    picsElement.TryGetProperty("mainPic", out var imgElement))
                                {
                                    var imgUrl = imgElement.GetString() ?? "";
                                    if (!string.IsNullOrEmpty(imgUrl) && !imgUrl.StartsWith("http"))
                                        imgUrl = "https:" + imgUrl;
                                    product.ImageUrl = imgUrl;
                                }
                                
                                // Sales 필드에 리뷰 수 표시
                                product.Sales = $"리뷰 {reviewCount}개";
                                
                                products.Add(product);
                                count++;
                            }
                            
                            LogWindow.AddLogStatic($"✅ {products.Count}개 상품 정보 추출 완료");
                            
                            // 성공하면 즉시 반환 (추가 처리 방지)
                            return products;
                        }
                        else
                        {
                            LogWindow.AddLogStatic("❌ 응답에서 이미지 ID를 찾을 수 없습니다");
                        }
                    }
                    catch (JsonException ex)
                    {
                        LogWindow.AddLogStatic($"❌ JSON 파싱 오류: {ex.Message}");
                        LogWindow.AddLogStatic("❌ 타오바오 쿠키가 만료되었거나 잘못되었습니다.");
                    }
                }
                else
                {
                    LogWindow.AddLogStatic($"❌ API 요청 실패: {response.StatusCode}");
                    LogWindow.AddLogStatic($"응답 내용: {responseText}");
                }
                
                return products;
            }
            catch (Exception ex)
            {
                LogWindow.AddLogStatic($"❌ 쿠키 기반 업로드 오류: {ex.Message}");
                return products;
            }
        }
        
        // 타오바오 서명 생성
        private static string GenerateTaobaoSign(string data, long timestamp)
        {
            var text = $"{_taobaoToken}&{timestamp}&12574478&{data}";
            using var md5 = System.Security.Cryptography.MD5.Create();
            var hash = md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes(text));
            return Convert.ToHexString(hash).ToLower();
        }
        private async Task<List<TaobaoProduct>> UploadImageToTaobao(string imagePath)
        {
            IBrowser? browser = null;
            IPage? page = null;
            var products = new List<TaobaoProduct>();
            
            try
            {
                var absolutePath = Path.GetFullPath(imagePath);
                if (!File.Exists(absolutePath))
                {
                    throw new FileNotFoundException($"이미지 파일을 찾을 수 없습니다: {absolutePath}");
                }
                
                LogWindow.AddLogStatic("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
                LogWindow.AddLogStatic("🔍 타오바오 이미지 검색 시작 (네이버 크롤링 아님)");
                LogWindow.AddLogStatic("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
                LogWindow.AddLogStatic("🌐 Chrome 다운로드 중...");
                
                var browserFetcher = new BrowserFetcher();
                var revisionInfo = await browserFetcher.DownloadAsync();
                
                LogWindow.AddLogStatic($"✅ Chrome 다운로드 완료: {revisionInfo.GetExecutablePath()}");
                LogWindow.AddLogStatic("🌐 Chrome 실행 중...");
                
                // ⭐ Predvia 전용 프로필 사용 (로그인 정보 자동 로드)
                var profilePath = GetPredviaChromeProfile();
                LogWindow.AddLogStatic($"📁 Chrome 프로필: {profilePath}");
                LogWindow.AddLogStatic("🔐 저장된 타오바오 로그인 정보 로드 중...");
                
                browser = await Puppeteer.LaunchAsync(new LaunchOptions
                {
                    Headless = false,
                    ExecutablePath = revisionInfo.GetExecutablePath(),
                    UserDataDir = profilePath,  // ⭐ 핵심: 동일한 프로필 사용
                    Args = new[] { 
                        "--window-size=200,300",
                        "--window-position=1700,680",
                        "--disable-blink-features=AutomationControlled",
                        "--disable-infobars",
                        "--no-sandbox"
                    },
                    DefaultViewport = null
                });
                
                LogWindow.AddLogStatic("✅ Chrome 실행 성공");
                
                // 새 탭 생성
                page = await browser.NewPageAsync();
                LogWindow.AddLogStatic("📄 새 탭 생성 완료");
                
                // Anti-bot: navigator.webdriver 제거
                await page.EvaluateFunctionOnNewDocumentAsync(@"() => {
                    Object.defineProperty(navigator, 'webdriver', {
                        get: () => undefined
                    });
                    
                    Object.defineProperty(navigator, 'plugins', {
                        get: () => [1, 2, 3, 4, 5]
                    });
                    
                    Object.defineProperty(navigator, 'languages', {
                        get: () => ['ko-KR', 'ko', 'en-US', 'en']
                    });
                    
                    window.chrome = { runtime: {} };
                }");
                
                LogWindow.AddLogStatic("🛡️ 봇 감지 우회 설정 완료");
                
                // 타오바오 페이지로 이동
                LogWindow.AddLogStatic("🌐 타오바오 페이지로 이동 중...");
                try
                {
                    await page.GoToAsync("https://www.taobao.com/", new NavigationOptions
                    {
                        WaitUntil = new[] { WaitUntilNavigation.Networkidle0 },
                        Timeout = 30000
                    });
                    LogWindow.AddLogStatic("✅ 타오바오 페이지 로드 완료");
                }
                catch (Exception navEx)
                {
                    LogWindow.AddLogStatic($"⚠️ 타오바오 페이지 로드 오류: {navEx.Message}");
                    LogWindow.AddLogStatic("🔄 재시도 중...");
                    
                    // 재시도
                    await page.GoToAsync("https://www.taobao.com/", new NavigationOptions
                    {
                        WaitUntil = new[] { WaitUntilNavigation.DOMContentLoaded },
                        Timeout = 30000
                    });
                    LogWindow.AddLogStatic("✅ 타오바오 페이지 로드 완료 (재시도 성공)");
                }
                
                // 1단계: 파일 input 찾기 및 이미지 업로드
                LogWindow.AddLogStatic("📁 파일 업로드 input 찾는 중...");
                var fileInput = await page.QuerySelectorAsync("input[type='file']");
                if (fileInput != null)
                {
                    LogWindow.AddLogStatic("✅ 파일 업로드 input 발견");
                    await fileInput.UploadFileAsync(absolutePath);
                    LogWindow.AddLogStatic($"✅ 이미지 파일 업로드 완료: {Path.GetFileName(absolutePath)}");
                    
                    // 이미지 업로드 후 UI 업데이트 대기
                    await Task.Delay(1500);
                }
                else
                {
                    LogWindow.AddLogStatic("❌ 파일 업로드 input을 찾을 수 없습니다");
                    throw new Exception("파일 업로드 input을 찾을 수 없습니다");
                }
                
                // 2단계: 이미지 업로드 후 검색 버튼 클릭
                LogWindow.AddLogStatic("🔍 이미지 검색 버튼 찾는 중...");
                try
                {
                    // 타오바오 이미지 검색 버튼: #image-search-upload-button
                    await page.WaitForSelectorAsync("#image-search-upload-button", new WaitForSelectorOptions
                    {
                        Timeout = 10000
                    });
                    LogWindow.AddLogStatic("✅ 이미지 검색 버튼 발견");
                    
                    // 현재 페이지 수 확인
                    var pagesBefore = (await browser.PagesAsync()).Length;
                    LogWindow.AddLogStatic($"📄 클릭 전 페이지 수: {pagesBefore}");
                    
                    // 버튼 클릭
                    await page.ClickAsync("#image-search-upload-button");
                    LogWindow.AddLogStatic("✅ 이미지 검색 버튼 클릭 완료");
                    
                    // 새 탭이 열릴 때까지 대기
                    await Task.Delay(3000);
                    
                    // 모든 페이지 확인
                    var pagesAfter = await browser.PagesAsync();
                    LogWindow.AddLogStatic($"📄 클릭 후 페이지 수: {pagesAfter.Length}");
                    
                    // 검색 결과 페이지 찾기 (s.taobao.com 포함된 페이지)
                    IPage? searchResultPage = null;
                    for (int i = 0; i < 30; i++) // 최대 15초 대기
                    {
                        await Task.Delay(500);
                        
                        foreach (var p in await browser.PagesAsync())
                        {
                            if (p.Url.Contains("s.taobao.com"))
                            {
                                searchResultPage = p;
                                break;
                            }
                        }
                        
                        if (searchResultPage != null)
                        {
                            LogWindow.AddLogStatic($"✅ 검색 결과 페이지 발견: {searchResultPage.Url}");
                            break;
                        }
                    }
                    
                    if (searchResultPage != null)
                    {
                        page = searchResultPage;
                        LogWindow.AddLogStatic($"✅ 검색 결과 페이지로 전환 완료");
                        
                        // 추가 로딩 대기
                        await Task.Delay(2000);
                    }
                    else
                    {
                        LogWindow.AddLogStatic("⚠️ 검색 결과 페이지를 찾을 수 없습니다");
                    }
                    
                    LogWindow.AddLogStatic($"🌐 최종 페이지: {page.Url}");
                    
                    // 3단계: 검색 결과에서 상위 5개 상품 정보 크롤링
                    LogWindow.AddLogStatic("📦 타오바오 상품 정보 수집 중...");
                    products = await ExtractTaobaoProducts(page);
                    LogWindow.AddLogStatic($"✅ 타오바오 상품 {products.Count}개 수집 완료");
                }
                catch (Exception btnEx)
                {
                    LogWindow.AddLogStatic($"⚠️ 이미지 검색 오류: {btnEx.Message}");
                    LogWindow.AddLogStatic($"📍 현재 URL: {page.Url}");
                }
                
                // 탭은 사용자가 결과를 볼 수 있도록 열어둠 (닫지 않음)
                LogWindow.AddLogStatic("✅ 타오바오 이미지 검색 완료 - 탭 유지");
                
                return products;
            }
            catch (Exception ex)
            {
                LogWindow.AddLogStatic($"❌ 타오바오 업로드 오류: {ex.Message}");
                
                // 오류 발생 시 탭 닫기
                if (page != null)
                {
                    try { await page.CloseAsync(); } catch { }
                }
                
                throw;
            }
            // browser는 ConnectAsync이므로 Disconnect 불필요 (자동 해제)
        }
        
        // ⭐ 타오바오 검색 결과에서 상위 5개 상품 정보 추출
        private async Task<List<TaobaoProduct>> ExtractTaobaoProducts(IPage page)
        {
            var products = new List<TaobaoProduct>();
            
            try
            {
                LogWindow.AddLogStatic($"🔍 현재 페이지 URL: {page.Url}");
                
                // 상품 카드 대기 (타임아웃 증가)
                await page.WaitForSelectorAsync(".doubleCard--gO3Bz6bu", new WaitForSelectorOptions
                {
                    Timeout = 20000
                });
                
                LogWindow.AddLogStatic("✅ 상품 카드 발견 - 정보 추출 시작");
                
                // JavaScript로 상위 5개 상품 정보 추출
                var productsData = await page.EvaluateFunctionAsync<List<Dictionary<string, string>>>(@"() => {
                    const cards = document.querySelectorAll('.doubleCard--gO3Bz6bu');
                    const results = [];
                    
                    console.log('🔍 발견된 상품 카드 개수:', cards.length);
                    
                    for (let i = 0; i < Math.min(5, cards.length); i++) {
                        const card = cards[i];
                        
                        try {
                            // 이미지 - img 태그 직접 찾기
                            const img = card.querySelector('img[class*=""mainPic""]');
                            const imageUrl = img ? img.src : '';
                            
                            // 가격 - priceInt로 시작하는 클래스
                            const priceInt = card.querySelector('[class*=""priceInt""]');
                            const price = priceInt ? priceInt.textContent.trim() : '';
                            
                            // 판매량 - realSales로 시작하는 클래스
                            const sales = card.querySelector('[class*=""realSales""]');
                            const salesText = sales ? sales.textContent.trim() : '';
                            
                            // 상품명 - title로 시작하는 클래스 안의 span
                            const title = card.querySelector('[class*=""title""] span');
                            const titleText = title ? title.textContent.trim() : '';
                            
                            // 상품 링크 - 카드를 감싸는 부모 a 태그
                            const parentLink = card.closest('a');
                            const productUrl = parentLink ? parentLink.href : '';
                            
                            console.log(`상품 ${i+1}:`, { imageUrl, price, salesText, titleText, productUrl });
                            
                            if (imageUrl && price) {
                                results.push({
                                    imageUrl: imageUrl,
                                    price: price,
                                    sales: salesText,
                                    title: titleText,
                                    productUrl: productUrl
                                });
                            }
                        } catch (e) {
                            console.error('상품 정보 추출 오류:', e);
                        }
                    }
                    
                    return results;
                }");
                
                // Dictionary를 TaobaoProduct로 변환
                foreach (var data in productsData)
                {
                    products.Add(new TaobaoProduct
                    {
                        ImageUrl = data.ContainsKey("imageUrl") ? data["imageUrl"] : "",
                        Price = data.ContainsKey("price") ? data["price"] : "",
                        Sales = data.ContainsKey("sales") ? data["sales"] : "",
                        Title = data.ContainsKey("title") ? data["title"] : "",
                        ProductUrl = data.ContainsKey("productUrl") ? data["productUrl"] : ""
                    });
                }
                
                LogWindow.AddLogStatic($"📦 상품 정보 추출 완료: {products.Count}개");
                
                // 각 상품 정보 로그
                for (int i = 0; i < products.Count; i++)
                {
                    LogWindow.AddLogStatic($"  [{i+1}] ¥{products[i].Price} | {products[i].Sales}");
                }
            }
            catch (Exception ex)
            {
                LogWindow.AddLogStatic($"⚠️ 상품 정보 추출 오류: {ex.Message}");
                LogWindow.AddLogStatic($"📍 현재 URL: {page.Url}");
            }
            
            return products;
        }
        
        // ⭐ 모든 스토어 완료 처리
        private async Task<IResult> HandleAllStoresCompleted(HttpContext context)
        {
            try
            {
                // ⭐ 이미 팝업이 표시되었으면 중복 실행 방지
                if (_completionPopupShown)
                {
                    LogWindow.AddLogStatic("⚠️ 완료 팝업 이미 표시됨 - 중복 요청 무시");
                    return Results.Ok(new { success = false, message = "Already completed" });
                }
                
                LogWindow.AddLogStatic("🎉 Chrome에서 모든 스토어 완료 신호 수신");

                // Chrome의 판단을 신뢰하고 무조건 완료 처리
                var currentCount = GetCurrentProductCount();
                LogWindow.AddLogStatic($"🎉 모든 스토어 방문 완료! 최종 수집: {currentCount}/100개");

                // 로딩창 숨김
                LoadingHelper.HideLoadingFromSourcingPage();

                // ⭐ 크롤링 브라우저들 종료 (스마트스토어 창 + 네이버 가격비교 창) - 직접 실행
                try
                {
                    await Task.Delay(500);
                    LogWindow.AddLogStatic($"🔥 브라우저 종료 시작 (모든 스토어 완료)");

                    // 스마트스토어 크롤링 창들 종료
                    await ChromeExtensionService.CloseSmartStoreCrawlingWindows();
                    LogWindow.AddLogStatic($"✅ 크롤링 스마트스토어 창 종료 완료");

                    // 네이버 가격비교 창 종료
                    await ChromeExtensionService.CloseNaverPriceComparisonWindowByTitle();
                    LogWindow.AddLogStatic($"✅ 네이버 가격비교 창 종료 완료");
                }
                catch (Exception browserEx)
                {
                    LogWindow.AddLogStatic($"❌ 브라우저 종료 오류: {browserEx.Message}");
                }

                // 팝업창 표시
                ShowCrawlingResultPopup(currentCount, "모든 스토어 방문 완료");

                return Results.Ok(new { success = true, message = "All stores completed popup shown" });
            }
            catch (Exception ex)
            {
                LogWindow.AddLogStatic($"❌ 모든 스토어 완료 처리 오류: {ex.Message}");
                return Results.BadRequest(new { error = ex.Message });
            }
        }
        
        // ⭐ 모든 스토어 완료 상태 체크
        private IResult HandleCheckAllCompleted()
        {
            try
            {
                var allCompleted = _storeStates.Values.All(s => s.State == "done");
                var completedCount = _storeStates.Count(s => s.Value.State == "done");
                var totalCount = _storeStates.Count;
                var currentProducts = GetCurrentProductCount();
                
                return Results.Json(new { 
                    allCompleted, 
                    completedCount, 
                    totalCount,
                    currentProducts
                });
            }
            catch (Exception ex)
            {
                LogWindow.AddLogStatic($"❌ 완료 상태 체크 오류: {ex.Message}");
                return Results.BadRequest(new { error = ex.Message });
            }
        }
        
        // ⭐ 테스트용: 10초 후 자동으로 모든 스토어 완료 체크 (사용 안 함)
        private void StartAutoCompleteTimer()
        {
            // 더 이상 사용하지 않음 - Chrome이 직접 완료 신호 전송
        }
        
        // ⭐ 크롤링 멈춤 감지 워치독 타이머
        private void StartCrawlingWatchdog()
        {
            _crawlingWatchdogTimer?.Dispose();
            _crawlingWatchdogTimer = new System.Threading.Timer(_ =>
            {
                if (!_isCrawlingActive || _shouldStop) 
                {
                    _crawlingWatchdogTimer?.Dispose();
                    return;
                }
                
                var elapsed = (DateTime.Now - _lastCrawlingActivity).TotalSeconds;
                if (elapsed >= 10)
                {
                    LogWindow.AddLogStatic($"⏰ 크롤링 10초 이상 멈춤 감지! ({elapsed:F0}초) - 다음 스토어로 강제 이동");
                    _lastCrawlingActivity = DateTime.Now;
                    ForceSkipToNextStore();
                }
            }, null, 5000, 3000); // 5초 후 시작, 3초마다 체크
        }
        
        // ⭐ 현재 스토어 강제 스킵 → 다음 스토어로 이동
        private void ForceSkipToNextStore()
        {
            lock (_storeProcessLock)
            {
                if (_currentStoreIndex >= _selectedStores.Count)
                {
                    LogWindow.AddLogStatic("⏰ 모든 스토어 처리 완료 - 워치독 종료");
                    _crawlingWatchdogTimer?.Dispose();
                    return;
                }
                
                var skippedStore = _selectedStores[_currentStoreIndex];
                var skippedStoreId = UrlExtensions.ExtractStoreIdFromUrl(skippedStore.Url);
                
                // 현재 스토어 강제 완료 처리
                foreach (var key in _storeStates.Keys.ToList())
                {
                    if (key.StartsWith(skippedStoreId, StringComparison.OrdinalIgnoreCase))
                    {
                        _storeStates[key].State = "done";
                        _storeStates[key].Lock = false;
                    }
                }
                
                _currentStoreIndex++;
                LogWindow.AddLogStatic($"⏰ {skippedStoreId} 강제 스킵 → 다음 스토어 ({_currentStoreIndex}/{_selectedStores.Count})");
                
                if (_currentStoreIndex >= _selectedStores.Count)
                {
                    LogWindow.AddLogStatic("⏰ 모든 스토어 처리 완료");
                    var finalCount = GetCurrentProductCount();
                    ShowCrawlingResultPopup(finalCount, "모든 스토어 처리 완료");
                    _crawlingWatchdogTimer?.Dispose();
                    return;
                }
                
                // 다음 스토어로 Chrome 확장프로그램에 접속 요청
                var nextStore = _selectedStores[_currentStoreIndex];
                var nextStoreId = UrlExtensions.ExtractStoreIdFromUrl(nextStore.Url);
                LogWindow.AddLogStatic($"⏰ 다음 스토어 접속 시도: {nextStoreId}");
                
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await ForceOpenNextStore();
                    }
                    catch (Exception ex)
                    {
                        LogWindow.AddLogStatic($"⏰ 다음 스토어 열기 실패: {ex.Message}");
                    }
                });
            }
        }
        
        
        // ⭐ 크롤링 결과 팝업창 표시
        private void ShowCrawlingResultPopup(int count, string reason)
        {
            try
            {
                _completionPopupShown = true;
                LoadingHelper.HideLoadingFromSourcingPage();
                
                // 상품데이터 페이지 캐시 무효화 → 다음 탭 진입 시 DB 재로드
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    if (Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
                    {
                        var mw = desktop.MainWindow;
                        var pdContent = mw?.FindControl<Avalonia.Controls.ContentControl>("ProductDataContent");
                        if (pdContent?.Content is ProductDataPage pdPage)
                            pdPage.ForceReloadProductCards();
                    }
                });

                _ = Task.Run(async () =>
                {
                    await Task.Delay(1000);
                    try
                    {
                        await ChromeExtensionService.CloseSmartStoreCrawlingWindows();
                        await ChromeExtensionService.CloseNaverPriceComparisonWindowByTitle();
                    }
                    catch { }
                });

                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    var mainWindow = Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
                        ? desktop.MainWindow
                        : null;

                    if (mainWindow == null) return;

                    var messageBox = new Avalonia.Controls.Window
                    {
                        Title = "크롤링 완료",
                        Width = 420,
                        Height = 200,
                        WindowStartupLocation = Avalonia.Controls.WindowStartupLocation.CenterOwner,
                        CanResize = false,
                        Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#F8F9FA")),
                        Content = new Avalonia.Controls.StackPanel
                        {
                            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                            Spacing = 20,
                            Children =
                            {
                                new Avalonia.Controls.TextBlock
                                {
                                    Text = $"상품 {count}개 크롤링 완료!\n상품데이터탭에서 확인하세요.",
                                    FontSize = 18,
                                    FontWeight = Avalonia.Media.FontWeight.Bold,
                                    Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#2C3E50")),
                                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                                    TextAlignment = Avalonia.Media.TextAlignment.Center
                                },
                                new Avalonia.Controls.Button
                                {
                                    Content = "확인",
                                    FontSize = 16,
                                    Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#E67E22")),
                                    Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Colors.White),
                                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                                    Padding = new Avalonia.Thickness(40, 10),
                                    CornerRadius = new Avalonia.CornerRadius(6),
                                    BorderThickness = new Avalonia.Thickness(0)
                                }
                            }
                        }
                    };

                    var panel = messageBox.Content as Avalonia.Controls.StackPanel;
                    var confirmButton = panel?.Children[1] as Avalonia.Controls.Button;
                    if (confirmButton != null)
                        confirmButton.Click += (s, e) => messageBox.Close();

                    if (mainWindow == null || !mainWindow.IsVisible)
                    {
                        messageBox.Show();
                    }
                    else
                    {
                        messageBox.Show(mainWindow);
                    }
                });
            }
            catch (Exception ex)
            {
                LogWindow.AddLogStatic($"❌ 팝업창 표시 오류: {ex.Message}");
            }
        }


        // ⭐ 현재 상품 개수 가져오기
        private int GetCurrentProductCount()
        {
            // ⭐ 이번 세션에서 추가된 개수만 반환 (기존 파일 제외)
            var totalFiles = GetRawFileCount();
            var sessionCount = totalFiles - _sessionStartFileCount;
            return Math.Max(0, sessionCount);
        }
        
        // ⭐ 실제 파일 개수 (세션 무관)
        private int GetRawFileCount()
        {
            try
            {
                var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                var imagesPath = Path.Combine(appDataPath, "Predvia", "Images");
                
                if (Directory.Exists(imagesPath))
                {
                    return Directory.GetFiles(imagesPath, "*_main.jpg").Length;
                }
                return 0;
            }
            catch
            {
                return 0;
            }
        }

        // ⭐ 파일 기반으로 상품 데이터 저장 (UI 카드 없이도 저장 가능)
        private void SaveProductCardsFromFiles()
        {
            try
            {
                var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                var predviaPath = Path.Combine(appDataPath, "Predvia");
                var imagesPath = Path.Combine(predviaPath, "Images");
                
                LogWindow.AddLogStatic($"🔍 JSON 저장 시도 - Images 경로: {imagesPath}");
                
                // 폴더 없으면 생성
                if (!Directory.Exists(predviaPath))
                {
                    Directory.CreateDirectory(predviaPath);
                }
                
                if (!Directory.Exists(imagesPath))
                {
                    LogWindow.AddLogStatic("❌ Images 폴더 없음 - JSON 저장 스킵");
                    return; // 이미지 폴더 없으면 저장할 것도 없음
                }

                var productCards = new List<object>();
                // ⭐ 파일 생성 시간순 정렬 (크롤링 순서대로)
                var imageFiles = Directory.GetFiles(imagesPath, "*_main.jpg")
                    .OrderBy(f => new FileInfo(f).CreationTime)
                    .ToArray();
                
                LogWindow.AddLogStatic($"🔍 이미지 파일 개수: {imageFiles.Length}개");
                
                // ⭐ 이미지 파일 또는 상품명 파일 기반으로 상품 목록 생성
                var productDataPath = Path.Combine(predviaPath, "ProductData");
                
                // 이미지 파일이 없으면 상품명 파일로 대체
                if (imageFiles.Length == 0 && Directory.Exists(productDataPath))
                {
                    var nameFiles = Directory.GetFiles(productDataPath, "*_name.txt")
                        .OrderBy(f => new FileInfo(f).CreationTime)
                        .ToArray();
                    
                    LogWindow.AddLogStatic($"🔍 이미지 없음, 상품명 파일로 대체: {nameFiles.Length}개");
                    
                    foreach (var nameFile in nameFiles)
                    {
                        try
                        {
                            var fileName = Path.GetFileNameWithoutExtension(nameFile);
                            var parts = fileName.Replace("_name", "").Split('_');
                            if (parts.Length < 2) continue;
                            
                            var productId = parts[parts.Length - 1];
                            var storeId = string.Join("_", parts.Take(parts.Length - 1));
                            var productName = File.ReadAllText(nameFile, System.Text.Encoding.UTF8).Trim();
                            
                            productCards.Add(new
                            {
                                storeId = storeId,
                                realProductId = productId,
                                imageUrl = "",
                                productName = productName
                            });
                        }
                        catch { }
                    }
                }
                else
                {
                    foreach (var imageFile in imageFiles)
                    {
                        try
                        {
                            var fileName = Path.GetFileNameWithoutExtension(imageFile);
                            var parts = fileName.Replace("_main", "").Split('_');
                            if (parts.Length < 2) continue;
                            
                            var productId = parts[parts.Length - 1];
                            var storeId = string.Join("_", parts.Take(parts.Length - 1));
                            
                            // 상품명 파일에서 읽기
                            var productName = "";
                            var nameFilePath = Path.Combine(productDataPath, $"{storeId}_{productId}_name.txt");
                            if (File.Exists(nameFilePath))
                            {
                                productName = File.ReadAllText(nameFilePath, System.Text.Encoding.UTF8).Trim();
                            }
                            
                            productCards.Add(new
                            {
                                storeId = storeId,
                                realProductId = productId,
                                imageUrl = imageFile,
                                productName = productName
                            });
                        }
                        catch { }
                    }
                }

                var jsonFilePath = Path.Combine(predviaPath, "product_cards.json");
                var options = new System.Text.Json.JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                };
                var json = System.Text.Json.JsonSerializer.Serialize(productCards, options);
                File.WriteAllText(jsonFilePath, json);

                LogWindow.AddLogStatic($"💾 상품 데이터 저장 완료: {productCards.Count}개 상품");
            }
            catch (Exception ex)
            {
                LogWindow.AddLogStatic($"❌ 상품 데이터 저장 실패: {ex.Message}");
            }
        }

        public async Task StopAsync()
        {
            if (_app != null && _isRunning)
            {
                await _app.StopAsync();
                _isRunning = false;
                LogWindow.AddLogStatic("웹서버 중지됨");
            }
        }

        
        // ⭐ 강제로 다음 스토어 열기 (Chrome 확장 먹통 시)
        private async Task ForceOpenNextStore()
        {
            try
            {
                lock (_counterLock)
                {
                    if (_currentStoreIndex >= _selectedStores.Count)
                    {
                        LogWindow.AddLogStatic("✅ 모든 스토어 처리 완료");
                        return;
                    }
                    
                    if (_productCount >= 100)
                    {
                        LogWindow.AddLogStatic("✅ 100개 달성 - 추가 스토어 열기 중단");
                        return;
                    }
                }
                
                // 다음 스토어 URL 가져오기
                string nextStoreUrl;
                string nextStoreTitle;
                lock (_counterLock)
                {
                    if (_currentStoreIndex >= _selectedStores.Count) return;
                    
                    var nextStore = _selectedStores[_currentStoreIndex];
                    nextStoreUrl = nextStore.Url ?? "";
                    nextStoreTitle = nextStore.Title ?? "알 수 없음";
                    
                    // URL에서 실제 스토어 URL 추출
                    if (nextStoreUrl.Contains("url="))
                    {
                        var urlParam = System.Web.HttpUtility.ParseQueryString(new Uri(nextStoreUrl).Query)["url"];
                        if (!string.IsNullOrEmpty(urlParam))
                        {
                            nextStoreUrl = urlParam + "/category/50000165";
                        }
                    }
                }
                
                if (string.IsNullOrEmpty(nextStoreUrl))
                {
                    LogWindow.AddLogStatic("❌ 다음 스토어 URL 없음");
                    return;
                }
                
                LogWindow.AddLogStatic($"🔥 강제 스토어 열기: {nextStoreTitle} - {nextStoreUrl}");
                
                // Chrome으로 공구탭 열기
                await ChromeExtensionService.OpenSmartStoreGongguTab(nextStoreUrl);
                
            }
            catch (Exception ex)
            {
                LogWindow.AddLogStatic($"❌ 강제 스토어 열기 오류: {ex.Message}");
            }
        }
        
        // 🔥 소싱 페이지 새로고침 (크롤링 완료 후 카드 표시)
        public void RefreshSourcingPage()
        {
            try
            {
                var mainWindow = Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
                    ? desktop.MainWindow as MainWindow
                    : null;

                if (mainWindow != null)
                {
                    Dispatcher.UIThread.Post(() =>
                    {
                        // 여러 방법으로 SourcingPage 찾기 시도
                        SourcingPage? sourcingPage = null;
                        
                        // 방법 1: SourcingPageInstance 속성 사용
                        sourcingPage = mainWindow.SourcingPageInstance;
                        
                        // 방법 3: FindControl로 직접 찾기
                        if (sourcingPage == null)
                        {
                            sourcingPage = mainWindow.FindControl<SourcingPage>("SourcingPageContent");
                        }
                        
                        if (sourcingPage != null)
                        {
                            // 🔄 카테고리 캐시 새로고침 먼저 실행
                            sourcingPage.RefreshCategoryCache();
                            
                            // LoadCrawledData 직접 호출
                            sourcingPage.LoadCrawledData();
                            LogWindow.AddLogStatic("✅ 소싱 페이지 새로고침 완료 (카테고리 캐시 포함)");
                        }
                        else
                        {
                            LogWindow.AddLogStatic("❌ SourcingPage를 찾을 수 없음 - 모든 방법 실패");
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                LogWindow.AddLogStatic($"❌ 소싱 페이지 새로고침 오류: {ex.Message}");
            }
        }

        // ⭐ 상품 처리 완료 신호 수신 API
        private async Task<IResult> HandleProductDone(HttpContext context)
        {
            try
            {
                using var reader = new StreamReader(context.Request.Body);
                var body = await reader.ReadToEndAsync();
                var json = JsonDocument.Parse(body);
                
                var storeId = json.RootElement.GetProperty("storeId").GetString() ?? "";
                var productId = json.RootElement.GetProperty("productId").GetString() ?? "";
                
                lock (_productDoneLock)
                {
                    _lastCompletedProductId = $"{storeId}_{productId}";
                }
                
                LogWindow.AddLogStatic($"✅ 상품 처리 완료 신호: {storeId}/{productId}");
                return Results.Ok(new { success = true });
            }
            catch (Exception ex)
            {
                return Results.Ok(new { success = false, error = ex.Message });
            }
        }
        
        // ⭐ 상품 처리 완료 확인 API (폴링용)
        private Task<IResult> HandleGetProductDone(HttpContext context)
        {
            var productKey = context.Request.Query["productKey"].ToString();
            
            lock (_productDoneLock)
            {
                var isDone = _lastCompletedProductId == productKey;
                if (isDone)
                {
                    _lastCompletedProductId = null; // 확인 후 초기화
                }
                return Task.FromResult(Results.Ok(new { done = isDone }));
            }
        }

        // ⭐ 상품 이미지 처리 API
        private async Task<IResult> HandleProductImage(HttpContext context)
        {
            try
            {
                // 100개 달성 시 즉시 차단
                if (_productCount >= 100)
                {
                    LogWindow.AddLogStatic("🛑 100개 달성으로 이미지 처리 차단");
                    await context.Response.WriteAsync(JsonSerializer.Serialize(new { success = false, message = "목표 달성으로 차단" }));
                    return Results.Ok();
                }

                var body = await new StreamReader(context.Request.Body).ReadToEndAsync();
                LogWindow.AddLogStatic($"🖼️ 이미지 처리 요청: {body}");

                var imageData = JsonSerializer.Deserialize<ProductImageData>(body);
                if (imageData == null)
                {
                    LogWindow.AddLogStatic("❌ 이미지 데이터 파싱 실패");
                    return Results.BadRequest("Invalid image data");
                }

                // 이미지 다운로드 및 저장
                await DownloadAndSaveImage(imageData);

                await context.Response.WriteAsync(JsonSerializer.Serialize(new { success = true }));
                return Results.Ok();
            }
            catch (Exception ex)
            {
                LogWindow.AddLogStatic($"❌ 이미지 처리 오류: {ex.Message}");
                await context.Response.WriteAsync(JsonSerializer.Serialize(new { success = false, error = ex.Message }));
                return Results.Ok();
            }
        }

        // ⭐ 이미지 다운로드 및 저장
        private async Task DownloadAndSaveImage(ProductImageData imageData)
        {
            try
            {
                using var httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.Add("User-Agent", 
                    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");

                LogWindow.AddLogStatic($"🔽 이미지 다운로드 시작: {imageData.ImageUrl}");
                
                var imageBytes = await httpClient.GetByteArrayAsync(imageData.ImageUrl);
                
                // 저장 디렉토리 생성
                var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                var imagesDir = System.IO.Path.Combine(appDataPath, "Predvia", "Images");
                Directory.CreateDirectory(imagesDir);

                // 파일명 생성: {storeId}_{productId}_main.jpg
                var fileName = $"{imageData.StoreId}_{imageData.ProductId}_main.jpg";
                var filePath = System.IO.Path.Combine(imagesDir, fileName);

                await File.WriteAllBytesAsync(filePath, imageBytes);
                
                LogWindow.AddLogStatic($"✅ 이미지 저장 완료: {fileName} ({imageBytes.Length} bytes)");

                // 🔥 S3 업로드 + DB에 S3 URL 저장
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var s3Url = await S3Service.Instance.UploadImageAsync(
                            DatabaseService.CurrentApiKey, imageData.StoreId, imageData.ProductId, imageBytes);
                        await DatabaseService.Instance.SaveProductAsync(
                            imageData.StoreId, imageData.ProductId,
                            null, null, 0, s3Url ?? imageData.ImageUrl, null, null);
                    }
                    catch (Exception dbEx)
                    {
                        LogWindow.AddLogStatic($"⚠️ 이미지 S3/DB 저장 실패: {dbEx.Message}");
                    }
                });

                // ⭐ 이미지 저장할 때마다 JSON 파일도 업데이트
                SaveProductCardsFromFiles();

                // ⭐ 실시간 카드 업데이트
                await UpdateSourcingPageCard(imageData.StoreId, imageData.ProductId, filePath);
            }
            catch (Exception ex)
            {
                LogWindow.AddLogStatic($"❌ 이미지 저장 실패: {ex.Message}");
            }
        }

        // ⭐ 소싱 페이지 실시간 카드 업데이트
        private async Task UpdateSourcingPageCard(string storeId, string productId, string imagePath)
        {
            try
            {
                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                {
                    // MainWindow에서 SourcingPage 찾기
                    var mainWindow = Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
                        ? desktop.MainWindow as MainWindow
                        : null;

                    if (mainWindow != null)
                    {
                        // SourcingPage 찾기 (private 필드이므로 리플렉션 사용)
                        var sourcingPageField = typeof(MainWindow).GetField("_sourcingPage", 
                            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        
                        if (sourcingPageField?.GetValue(mainWindow) is SourcingPage sourcingPage)
                        {
                            // 로컬 파일 경로를 file:// URI로 변환
                            var fileUri = new Uri(imagePath).ToString();
                            sourcingPage.AddProductImageCard(storeId, productId, fileUri);
                            LogWindow.AddLogStatic($"🎯 실시간 카드 업데이트: {storeId}_{productId}");
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                LogWindow.AddLogStatic($"❌ 실시간 카드 업데이트 실패: {ex.Message}");
            }
        }

        // ⭐ 상품명 처리 API
        private async Task<IResult> HandleProductName(HttpContext context)
        {
            try
            {
                // ⭐ 100개 달성 시 즉시 차단
                bool shouldStop = false;
                lock (_counterLock)
                {
                    shouldStop = _productCount >= 100;
                }
                
                if (shouldStop)
                {
                    LogWindow.AddLogStatic("🛑 100개 달성으로 상품명 처리 차단");
                    context.Response.ContentType = "application/json; charset=utf-8";
                    context.Response.StatusCode = 200;
                    await context.Response.WriteAsync(JsonSerializer.Serialize(new { 
                        success = true,
                        stop = true,
                        message = "Target reached - no more processing"
                    }));
                    return Results.Ok();
                }
                
                // 목표 달성과 관계없이 이미 접속한 상품의 상품명은 반드시 처리
                var body = await new StreamReader(context.Request.Body).ReadToEndAsync();
                LogWindow.AddLogStatic($"📝 상품명 처리 요청: {body}");

                var nameData = JsonSerializer.Deserialize<ProductNameData>(body);
                if (nameData == null)
                {
                    LogWindow.AddLogStatic("❌ 상품명 데이터 파싱 실패");
                    return Results.BadRequest("Invalid product name data");
                }

                // 상품명 저장
                await SaveProductName(nameData);

                // ⭐ 100개 달성 시 중단 신호 응답
                bool shouldStopAfterSave = false;
                lock (_counterLock)
                {
                    shouldStopAfterSave = _productCount >= 100;
                }
                
                if (shouldStopAfterSave)
                {
                    context.Response.ContentType = "application/json; charset=utf-8";
                    context.Response.StatusCode = 200;
                    await context.Response.WriteAsync(JsonSerializer.Serialize(new { 
                        success = true,
                        stop = true,
                        message = "Target reached after save"
                    }));
                    return Results.Ok();
                }

                context.Response.ContentType = "application/json; charset=utf-8";
                context.Response.StatusCode = 200;
                await context.Response.WriteAsync(JsonSerializer.Serialize(new { success = true }));
                return Results.Ok();
            }
            catch (Exception ex)
            {
                LogWindow.AddLogStatic($"❌ 상품명 처리 오류: {ex.Message}");
                context.Response.ContentType = "application/json; charset=utf-8";
                context.Response.StatusCode = 500;
                await context.Response.WriteAsync(JsonSerializer.Serialize(new { success = false, error = ex.Message }));
                return Results.Ok();
            }
        }

        // ⭐ 상품명 저장
        private async Task SaveProductName(ProductNameData nameData)
        {
            try
            {
                // 저장 디렉토리 생성
                var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                var dataDir = System.IO.Path.Combine(appDataPath, "Predvia", "ProductData");
                Directory.CreateDirectory(dataDir);

                // 파일명 생성: {storeId}_{productId}_name.txt
                var fileName = $"{nameData.StoreId}_{nameData.ProductId}_name.txt";
                var filePath = System.IO.Path.Combine(dataDir, fileName);

                await File.WriteAllTextAsync(filePath, nameData.ProductName, System.Text.Encoding.UTF8);
                
                // 🔥 상품별 중복 카운팅 방지
                var productKey = $"{nameData.StoreId}_{nameData.ProductId}";
                bool isNewProduct = false;
                
                lock (_counterLock)
                {
                    if (!_processedProducts.Contains(productKey))
                    {
                        _processedProducts.Add(productKey);
                        _productCount++;
                        isNewProduct = true;
                    }
                }
                
                if (isNewProduct)
                {
                    var percentage = (_productCount * 100.0) / 100;
                    LogWindow.AddLogStatic($"📊 실시간 진행률: {_productCount}/100개 ({percentage:F1}%)");
                }
                
                LogWindow.AddLogStatic($"✅ 상품명 저장 완료: {fileName} - {nameData.ProductName}");
                
                // 🔥 DB 저장 (비동기, 실패해도 파일 저장은 이미 완료)
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await DatabaseService.Instance.SaveProductAsync(
                            nameData.StoreId, nameData.ProductId,
                            null, nameData.ProductName,
                            0, null,
                            $"https://smartstore.naver.com/{nameData.StoreId}/products/{nameData.ProductId}",
                            null);
                    }
                    catch (Exception dbEx)
                    {
                        LogWindow.AddLogStatic($"⚠️ 상품명 DB 저장 실패: {dbEx.Message}");
                    }
                });
                
                // 🔥 소싱 페이지에 실시간 카드 추가
                try
                {
                    await AddProductCardToSourcingPage(nameData.StoreId, nameData.ProductId, nameData.ProductName);
                }
                catch (Exception cardEx)
                {
                    LogWindow.AddLogStatic($"⚠️ 카드 추가 오류: {cardEx.Message}");
                }
                
                // 🚨 100개 달성 시 크롤링 완전 중단
                if (_productCount >= 100)
                {
                    LogWindow.AddLogStatic("🎉 목표 달성! 100개 상품 수집 완료 - 크롤링 중단");
                    
                    // ⭐ 크롤링 완전 중단 신호 설정
                    _shouldStop = true;
                    _isCrawlingActive = false;
                    
                    LogWindow.AddLogStatic($"🛑 SaveProductName에서 크롤링 중단 플래그 설정: _shouldStop = {_shouldStop}");
                    
                    // ⭐ 모든 스토어를 done 상태로 변경하여 Chrome 중단
                    lock (_statesLock)
                    {
                        foreach (var storeId in _storeStates.Keys.ToList())
                        {
                            var state = _storeStates[storeId];
                            if (state.State != "done")
                            {
                                state.State = "done";
                                state.Lock = false;
                                LogWindow.AddLogStatic($"🛑 {storeId}: 강제 완료 처리 (목표 달성)");
                            }
                        }
                    }
                    
                    // 🔄 로딩창 숨김 - 소싱 페이지에서 직접 처리
                    LoadingHelper.HideLoadingFromSourcingPage();
                    
                    // ⭐ Chrome 앱 창들 닫기
                    _ = Task.Run(async () => await CloseAllChromeApps());
                    
                    // ⭐ 팝업창으로 최종 결과 표시
                    ShowCrawlingResultPopup(100, "목표 달성");
                    
                    // ⭐ 즉시 반환 (비동기 메서드에서는 return만)
                    return;
                }
            }
            catch (Exception ex)
            {
                LogWindow.AddLogStatic($"❌ 상품명 저장 실패: {ex.Message}");
            }
        }

        // ⭐ 가격 처리 API
        private async Task<IResult> HandleProductPrice(HttpContext context)
        {
            try
            {
                // 100개 달성 시 즉시 차단
                if (_productCount >= 100)
                {
                    LogWindow.AddLogStatic("🛑 100개 달성으로 가격 처리 차단");
                    await context.Response.WriteAsync(JsonSerializer.Serialize(new { success = false, message = "목표 달성으로 차단" }));
                    return Results.Ok();
                }

                var body = await new StreamReader(context.Request.Body).ReadToEndAsync();
                LogWindow.AddLogStatic($"💰 가격 처리 요청: {body}");

                _totalAttempted++;

                var priceData = JsonSerializer.Deserialize<ProductPriceData>(body);
                if (priceData == null)
                {
                    await context.Response.WriteAsync(JsonSerializer.Serialize(new { success = false, error = "Invalid price data" }));
                    return Results.Ok();
                }

                // ⭐ 가격 필터링 체크
                if (_priceFilterEnabled)
                {
                    var priceValue = ExtractPriceValue(priceData.Price);
                    if (priceValue < _minPrice || priceValue > _maxPrice)
                    {
                        LogWindow.AddLogStatic($"🚫 가격 필터링: {priceData.Price} ({priceValue}원) - 범위 밖 ({_minPrice}~{_maxPrice}원)");
                        await context.Response.WriteAsync(JsonSerializer.Serialize(new { 
                            success = false, 
                            filtered = true,
                            message = "가격 필터링으로 제외됨" 
                        }));
                        return Results.Ok();
                    }
                    LogWindow.AddLogStatic($"✅ 가격 필터링 통과: {priceData.Price} ({priceValue}원)");
                }

                await SaveProductPrice(priceData);
                
                await context.Response.WriteAsync(JsonSerializer.Serialize(new { success = true }));
                return Results.Ok();
            }
            catch (Exception ex)
            {
                LogWindow.AddLogStatic($"❌ 가격 처리 오류: {ex.Message}");
                await context.Response.WriteAsync(JsonSerializer.Serialize(new { success = false, error = ex.Message }));
                return Results.Ok();
            }
        }

        // ⭐ 가격 문자열에서 숫자 추출
        private int ExtractPriceValue(string priceString)
        {
            try
            {
                if (string.IsNullOrEmpty(priceString))
                    return 0;
                    
                // "7,572원", "1,354원" 등에서 숫자만 추출
                var numbers = System.Text.RegularExpressions.Regex.Replace(priceString, @"[^\d]", "");
                return int.TryParse(numbers, out int price) ? price : 0;
            }
            catch
            {
                return 0;
            }
        }

        // ⭐ 가격 저장 메서드
        private async Task SaveProductPrice(ProductPriceData priceData)
        {
            try
            {
                var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                var dataDir = System.IO.Path.Combine(appDataPath, "Predvia", "ProductData");
                Directory.CreateDirectory(dataDir);

                // 파일명 생성: {storeId}_{productId}_price.txt
                var fileName = $"{priceData.StoreId}_{priceData.ProductId}_price.txt";
                var filePath = System.IO.Path.Combine(dataDir, fileName);

                await File.WriteAllTextAsync(filePath, priceData.Price.ToString(), System.Text.Encoding.UTF8);
                
                LogWindow.AddLogStatic($"✅ 가격 저장 완료: {fileName} - {priceData.PriceText}");
                
                // 🔥 DB 가격 업데이트
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var priceValue = ExtractPriceValue(priceData.Price);
                        await DatabaseService.Instance.SaveProductAsync(
                            priceData.StoreId, priceData.ProductId,
                            null, null, priceValue, null, null, null);
                    }
                    catch (Exception dbEx)
                    {
                        LogWindow.AddLogStatic($"⚠️ 가격 DB 저장 실패: {dbEx.Message}");
                    }
                });
            }
            catch (Exception ex)
            {
                LogWindow.AddLogStatic($"❌ 가격 저장 실패: {ex.Message}");
            }
        }
        
        // 🔥 소싱 페이지에 실시간 카드 추가
        private async Task AddProductCardToSourcingPage(string storeId, string productId, string productName)
        {
            try
            {
                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                {
                    var mainWindow = Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
                        ? desktop.MainWindow as MainWindow
                        : null;
                        
                    if (mainWindow?.SourcingPageInstance != null)
                    {
                        // 이미지 파일 경로 생성
                        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                        var imageDir = System.IO.Path.Combine(appDataPath, "Predvia", "Images");
                        var imageFileName = $"{storeId}_{productId}_main.jpg";
                        var imagePath = System.IO.Path.Combine(imageDir, imageFileName);
                        
                        // 이미지 파일이 있으면 파일 경로, 없으면 상품명 사용
                        var imageUrl = File.Exists(imagePath) ? imagePath : productName;
                        
                        mainWindow.SourcingPageInstance.AddProductImageCard(storeId, productId, imageUrl, productName);
                        LogWindow.AddLogStatic($"🆔 새 카드 ID 생성: {_productCount}");
                    }
                });
            }
            catch (Exception ex)
            {
                LogWindow.AddLogStatic($"❌ 카드 추가 실패: {ex.Message}");
            }
        }

        // ⭐ 카테고리 처리 API
        private async Task<IResult> HandleCategories(HttpContext context)
        {
            try
            {
                var requestBody = await new StreamReader(context.Request.Body).ReadToEndAsync();
                var categoryData = JsonSerializer.Deserialize<CategoryData>(requestBody);

                if (categoryData?.Categories != null && categoryData.Categories.Count > 0)
                {
                    LogWindow.AddLogStatic($"🔍 카테고리 데이터 수신: {categoryData.StoreId} - {categoryData.Categories.Count}개");
                    
                    // ⭐ 개별 상품 카테고리인지 확인 (productId 필드 존재)
                    var jsonDoc = JsonDocument.Parse(requestBody);
                    string? productId = null;
                    if (jsonDoc.RootElement.TryGetProperty("productId", out var productIdElement))
                    {
                        productId = productIdElement.GetString();
                        LogWindow.AddLogStatic($"🔍 개별 상품 카테고리 감지: productId = {productId}");
                        
                        var categoryNames = string.Join(", ", categoryData.Categories.Select(c => c.Name));
                        LogWindow.AddLogStatic($"📂 {categoryData.StoreId}: 상품 {productId} 카테고리 수집 성공 - {categoryNames}");
                    }
                    else
                    {
                        LogWindow.AddLogStatic($"🔍 전체 카테고리 감지: productId 없음");
                    }
                    
                    // ⭐ productId 전달하여 저장
                    await SaveCategories(categoryData, productId);
                    LogWindow.AddLogStatic($"✅ {categoryData.StoreId}: {categoryData.Categories.Count}개 카테고리 저장 완료");
                    
                    // 🔥 DB 카테고리 업데이트
                    if (!string.IsNullOrEmpty(productId))
                    {
                        var categoryNames = string.Join(" > ", categoryData.Categories
                            .Where(c => !string.IsNullOrEmpty(c.Name) && c.Name != "전체상품" && c.Name != "홈" && c.Name != "Home")
                            .Select(c => c.Name));
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                await DatabaseService.Instance.SaveProductAsync(
                                    categoryData.StoreId, productId,
                                    null, null, 0, null, null, categoryNames);
                            }
                            catch (Exception dbEx)
                            {
                                LogWindow.AddLogStatic($"⚠️ 카테고리 DB 저장 실패: {dbEx.Message}");
                            }
                        });
                    }
                    
                    // 소싱 페이지에 카테고리 데이터 실시간 표시
                    await UpdateSourcingPageCategories(categoryData);
                }

                await context.Response.WriteAsync("{\"status\":\"success\"}");
                return Results.Ok();
            }
            catch (Exception ex)
            {
                LogWindow.AddLogStatic($"❌ 카테고리 처리 오류: {ex.Message}");
                return Results.BadRequest($"카테고리 처리 실패: {ex.Message}");
            }
        }

        // ⭐ 개별 상품 카테고리 처리 API
        private async Task<IResult> HandleProductCategories(HttpContext context)
        {
            try
            {
                var requestBody = await new StreamReader(context.Request.Body).ReadToEndAsync();
                var productCategoryData = JsonSerializer.Deserialize<ProductCategoryData>(requestBody);

                if (productCategoryData?.Categories != null && productCategoryData.Categories.Count > 0)
                {
                    var categoryNames = string.Join(", ", productCategoryData.Categories.Select(c => c.Name));
                    LogWindow.AddLogStatic($"📂 {productCategoryData.StoreId}: 상품 {productCategoryData.ProductId} 카테고리 수집 성공 - {categoryNames}");
                    
                    // 🔥 DB 카테고리 업데이트
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await DatabaseService.Instance.SaveProductAsync(
                                productCategoryData.StoreId, productCategoryData.ProductId,
                                null, null, 0, null, null, categoryNames);
                        }
                        catch (Exception dbEx)
                        {
                            LogWindow.AddLogStatic($"⚠️ 카테고리 DB 저장 실패: {dbEx.Message}");
                        }
                    });
                }
                else
                {
                    LogWindow.AddLogStatic($"📂 {productCategoryData?.StoreId}: 상품 {productCategoryData?.ProductId} 카테고리 수집 실패");
                }

                await context.Response.WriteAsync("{\"status\":\"success\"}");
                return Results.Ok();
            }
            catch (Exception ex)
            {
                LogWindow.AddLogStatic($"❌ 개별 상품 카테고리 처리 오류: {ex.Message}");
                return Results.BadRequest($"개별 상품 카테고리 처리 실패: {ex.Message}");
            }
        }

        // 카테고리 데이터 저장
        private async Task SaveCategories(CategoryData categoryData, string? productId = null)
        {
            try
            {
                var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                var predviaPath = Path.Combine(appDataPath, "Predvia");
                var categoriesPath = Path.Combine(predviaPath, "Categories");

                Directory.CreateDirectory(categoriesPath);

                // ⭐ productId가 있으면 개별 상품 카테고리 파일로 저장
                string fileName;
                if (!string.IsNullOrEmpty(productId))
                {
                    fileName = $"{categoryData.StoreId}_{productId}_categories.json";
                    LogWindow.AddLogStatic($"💾 개별 상품 카테고리 파일명: {fileName}");
                }
                else if (categoryData.PageUrl?.Contains("/products/") == true)
                {
                    fileName = $"{categoryData.StoreId}_{ExtractProductIdFromUrl(categoryData.PageUrl)}_categories.json";
                }
                else
                {
                    fileName = $"{categoryData.StoreId}_categories.json";
                }
                
                var filePath = Path.Combine(categoriesPath, fileName);

                var json = JsonSerializer.Serialize(categoryData, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                });

                await File.WriteAllTextAsync(filePath, json, System.Text.Encoding.UTF8);
                LogWindow.AddLogStatic($"💾 카테고리 파일 저장: {filePath}");
            }
            catch (Exception ex)
            {
                LogWindow.AddLogStatic($"❌ 카테고리 저장 오류: {ex.Message}");
            }
        }

        // URL에서 상품 ID 추출 헬퍼 메서드
        private string ExtractProductIdFromUrl(string url)
        {
            try
            {
                if (string.IsNullOrEmpty(url)) return "unknown";
                
                var match = System.Text.RegularExpressions.Regex.Match(url, @"/products/(\d+)");
                return match.Success ? match.Groups[1].Value : "unknown";
            }
            catch
            {
                return "unknown";
            }
        }

        // 소싱 페이지에 카테고리 데이터 실시간 업데이트
        private async Task UpdateSourcingPageCategories(CategoryData categoryData)
        {
            try
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    try
                    {
                        var mainWindow = Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
                            ? desktop.MainWindow as MainWindow
                            : null;

                        if (mainWindow?.SourcingPageInstance != null)
                        {
                            mainWindow.SourcingPageInstance.AddCategoryData(new Gumaedaehang.CategoryData 
                            {
                                StoreId = categoryData.StoreId,
                                Categories = categoryData.Categories?.Select(c => new Gumaedaehang.CategoryInfo
                                {
                                    Name = c.Name,
                                    Url = c.Url,
                                    CategoryId = c.CategoryId,
                                    Order = c.Order
                                }).ToList() ?? new List<Gumaedaehang.CategoryInfo>()
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        LogWindow.AddLogStatic($"❌ 소싱 페이지 카테고리 업데이트 오류: {ex.Message}");
                    }
                });
            }
            catch (Exception ex)
            {
                LogWindow.AddLogStatic($"❌ UI 스레드 카테고리 업데이트 오류: {ex.Message}");
            }
        }

        // ⭐ 리뷰 처리 API
        private async Task<IResult> HandleProductReviews(HttpContext context)
        {
            try
            {
                var body = await new StreamReader(context.Request.Body).ReadToEndAsync();
                LogWindow.AddLogStatic($"⭐ 리뷰 처리 요청: {body}");

                var reviewData = JsonSerializer.Deserialize<ProductReviewsData>(body);
                if (reviewData == null)
                {
                    LogWindow.AddLogStatic("❌ 리뷰 데이터 파싱 실패");
                    return Results.BadRequest("Invalid review data");
                }

                // 리뷰 저장
                await SaveProductReviews(reviewData);
                
                // ⭐ UI 업데이트
                await UpdateSourcingPageReviews(reviewData);

                await context.Response.WriteAsync(JsonSerializer.Serialize(new { success = true }));
                return Results.Ok();
            }
            catch (Exception ex)
            {
                LogWindow.AddLogStatic($"❌ 리뷰 처리 오류: {ex.Message}");
                await context.Response.WriteAsync(JsonSerializer.Serialize(new { success = false, error = ex.Message }));
                return Results.Ok();
            }
        }

        // ⭐ 리뷰 저장
        private async Task SaveProductReviews(ProductReviewsData reviewData)
        {
            try
            {
                // 저장 디렉토리 생성
                var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                var reviewsDir = System.IO.Path.Combine(appDataPath, "Predvia", "Reviews");
                Directory.CreateDirectory(reviewsDir);

                // 파일명 생성: {storeId}_{productId}_reviews.json
                var fileName = $"{reviewData.StoreId}_{reviewData.ProductId}_reviews.json";
                var filePath = System.IO.Path.Combine(reviewsDir, fileName);

                var jsonString = JsonSerializer.Serialize(reviewData, new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
                await File.WriteAllTextAsync(filePath, jsonString, System.Text.Encoding.UTF8);
                
                LogWindow.AddLogStatic($"✅ 리뷰 저장 완료: {fileName} - {reviewData.Reviews.Count}개 리뷰");
                
                // 🔥 DB 리뷰 저장
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var reviewList = reviewData.Reviews
                            .Select(r => (int.TryParse(r.Rating, out var rt) ? rt : 0, (string?)r.Content))
                            .ToList();
                        await DatabaseService.Instance.SaveReviewsAsync(
                            reviewData.StoreId, reviewData.ProductId, reviewList);
                    }
                    catch (Exception dbEx)
                    {
                        LogWindow.AddLogStatic($"⚠️ 리뷰 DB 저장 실패: {dbEx.Message}");
                    }
                });
            }
            catch (Exception ex)
            {
                LogWindow.AddLogStatic($"❌ 리뷰 저장 실패: {ex.Message}");
            }
        }
        
        // ⭐ 리뷰 UI 업데이트
        private async Task UpdateSourcingPageReviews(ProductReviewsData reviewData)
        {
            try
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    var mainWindow = Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
                        ? desktop.MainWindow as MainWindow
                        : null;
                    
                    mainWindow?.SourcingPageInstance?.UpdateProductReviews(reviewData.StoreId, reviewData.ProductId, reviewData.Reviews);
                });
            }
            catch (Exception ex)
            {
                LogWindow.AddLogStatic($"❌ 리뷰 UI 업데이트 오류: {ex.Message}");
            }
        }

        // ⭐ 기존 데이터 초기화
        public void ClearPreviousData()
        {
            try
            {
                var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                var predviaPath = System.IO.Path.Combine(appDataPath, "Predvia");
                
                // 초기화할 폴더들
                var foldersToClean = new[]
                {
                    System.IO.Path.Combine(predviaPath, "Images"),
                    System.IO.Path.Combine(predviaPath, "ProductData"),
                    System.IO.Path.Combine(predviaPath, "Reviews")
                };
                
                foreach (var folder in foldersToClean)
                {
                    if (Directory.Exists(folder))
                    {
                        var files = Directory.GetFiles(folder);
                        foreach (var file in files)
                        {
                            File.Delete(file);
                        }
                        LogWindow.AddLogStatic($"🧹 {System.IO.Path.GetFileName(folder)} 폴더 초기화 완료 ({files.Length}개 파일 삭제)");
                    }
                }
                
                // 상품 카운터 초기화
                _productCount = 0;
                                    _totalAttempted = 0;
                _isCrawlingActive = true;
                _processedStores.Clear();
                _processedProducts.Clear(); // ⭐ 상품 목록도 초기화
                _lastCrawlingActivity = DateTime.Now;
                StartCrawlingWatchdog();
                
                LogWindow.AddLogStatic("✅ 기존 데이터 초기화 완료 - 새로운 크롤링 준비됨");
            }
            catch (Exception ex)
            {
                LogWindow.AddLogStatic($"❌ 데이터 초기화 오류: {ex.Message}");
            }
        }
        
        // ⭐ 크롤링 허용 상태 조회 API
        private async Task<IResult> HandleGetCrawlingAllowed()
        {
            await Task.CompletedTask;
            lock (_crawlingLock)
            {
                return Results.Json(new { allowed = _crawlingAllowed });
            }
        }
        
        // ⭐ 크롤링 허용 설정 API
        private async Task<IResult> HandleAllowCrawling()
        {
            await Task.CompletedTask;
            lock (_crawlingLock)
            {
                _crawlingAllowed = true;
                _isCrawlingActive = true;
                _shouldStop = false;
                _currentStoreIndex = 0;
                _completionPopupShown = false;
                _saveCompleted = false;
                _v2Mode = false;
                _crawlSM = null;
            }
            lock (_counterLock)
            {
                _productCount = 0;
                                    _totalAttempted = 0;
                // ⭐ 세션 시작 시 기존 파일 개수 저장 (이번 세션에서 추가된 개수만 카운트)
                _sessionStartFileCount = GetRawFileCount();
            }
            lock (_statesLock)
            {
                _storeStates.Clear();
            }
            _selectedStores.Clear();
            _processedStores.Clear();
            LogWindow.AddLogStatic($"✅ 새로운 크롤링 세션 시작 - 기존 파일: {_sessionStartFileCount}개, 목표: +100개");
            return Results.Json(new { success = true });
        }

        // ⭐ 상품명 처리 API
        private async Task<IResult> HandleProductNames(HttpContext context)
        {
            try
            {
                var body = await new StreamReader(context.Request.Body).ReadToEndAsync();
                var request = JsonSerializer.Deserialize<ProductNamesRequest>(body);
                
                if (request?.ProductNames == null || request.ProductNames.Count == 0)
                {
                    return Results.Json(new { success = false, message = "상품명이 없습니다." });
                }
                
                var productId = request.ProductId > 0 ? request.ProductId : _currentProductId;
                LogWindow.AddLogStatic($"📝 상품명 {request.ProductNames.Count}개 수신 (상품 ID: {productId}, 현재 설정: {_currentProductId})");
                
                // 한글만 추출 및 중복 제거
                var koreanKeywords = ExtractKoreanKeywords(request.ProductNames);
                
                // ⭐ 키워드 누적 저장 (기존 키워드에 추가)
                lock (_keywordsLock)
                {
                    // 기존 키워드가 있으면 병합, 없으면 새로 생성
                    if (_productKeywords.ContainsKey(productId))
                    {
                        var existingKeywords = _productKeywords[productId];
                        var mergedKeywords = new HashSet<string>(existingKeywords);
                        mergedKeywords.UnionWith(koreanKeywords);
                        _productKeywords[productId] = mergedKeywords.ToList();
                        LogWindow.AddLogStatic($"✅ 키워드 병합: 기존 {existingKeywords.Count}개 + 새로운 {koreanKeywords.Count}개 = 총 {_productKeywords[productId].Count}개 (상품 ID: {productId})");
                    }
                    else
                    {
                        _productKeywords[productId] = koreanKeywords;
                        LogWindow.AddLogStatic($"✅ 한글 키워드 {koreanKeywords.Count}개 추출 완료 (상품 ID: {productId})");
                    }
                    
                    _latestKeywords = _productKeywords[productId];
                    _latestKeywordsTime = DateTime.Now;
                }
                
                return Results.Json(new { 
                    success = true, 
                    productId = productId,
                    originalCount = request.ProductNames.Count,
                    filteredCount = koreanKeywords.Count,
                    keywords = koreanKeywords 
                });
            }
            catch (Exception ex)
            {
                LogWindow.AddLogStatic($"❌ 상품명 처리 오류: {ex.Message}");
                return Results.Json(new { success = false, message = ex.Message });
            }
        }

        // ⭐ 한글 키워드 추출 및 중복 제거
        private List<string> ExtractKoreanKeywords(List<string> productNames)
        {
            var keywords = new HashSet<string>();
            
            foreach (var productName in productNames)
            {
                if (string.IsNullOrWhiteSpace(productName)) continue;
                
                // ⭐ 한글이 포함된 상품명만 처리
                if (!System.Text.RegularExpressions.Regex.IsMatch(productName, @"[가-힣]"))
                {
                    continue; // 한글이 없으면 스킵
                }
                
                // ⭐ 공백으로 단어 분리 후 각 단어에서 한글만 추출
                var words = productName.Split(new char[] { ' ', '\t', '\n', '-', '/', '(', ')', '[', ']', ',', '.' }, 
                    StringSplitOptions.RemoveEmptyEntries);
                
                foreach (var word in words)
                {
                    // 각 단어에서 한글만 추출 (2글자 이상)
                    var cleanWord = System.Text.RegularExpressions.Regex.Replace(word, @"[^가-힣]", "");
                    if (cleanWord.Length >= 2)
                    {
                        keywords.Add(cleanWord);
                    }
                }
            }
            
            LogWindow.AddLogStatic($"🏷️ 한글 키워드 추출: {string.Join(", ", keywords.Take(10))}...");
            return keywords.ToList();
        }

        // ⭐ 키워드 태그 표시 트리거 API
        private Task<IResult> HandleTriggerKeywords(HttpContext context)
        {
            try
            {
                LogWindow.AddLogStatic("🏷️ 키워드 태그 표시 트리거 수신");
                
                // ⭐ 즉시 SourcingPage에 키워드 태그 생성 요청
                _ = Task.Run(async () =>
                {
                    await Task.Delay(500); // 0.5초 대기
                    await TriggerKeywordTagsDisplay();
                });
                
                return Task.FromResult(Results.Json(new { success = true, message = "키워드 태그 생성 요청 완료" }));
            }
            catch (Exception ex)
            {
                LogWindow.AddLogStatic($"❌ 키워드 태그 트리거 오류: {ex.Message}");
                return Task.FromResult(Results.Json(new { success = false, message = ex.Message }));
            }
        }

        // ⭐ 소싱 페이지에 키워드 태그 표시 요청
        private async Task TriggerKeywordTagsDisplay()
        {
            try
            {
                LogWindow.AddLogStatic("🏷️ 키워드 태그 표시 트리거 시작");
                
                // MainWindow를 통해 SourcingPage에 키워드 태그 표시 요청
                await Dispatcher.UIThread.InvokeAsync(async () =>
                {
                    try
                    {
                        // Application.Current를 통해 MainWindow 찾기
                        var app = Application.Current;
                        if (app?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                        {
                            var mainWindow = desktop.MainWindow;
                            LogWindow.AddLogStatic($"🔍 ApplicationLifetime 타입: {desktop.GetType().Name}");
                            LogWindow.AddLogStatic($"🔍 MainWindow 타입: {mainWindow?.GetType().Name}");
                            
                            if (mainWindow is MainWindow predviaMainWindow)
                            {
                                LogWindow.AddLogStatic("🏷️ MainWindow 찾음 - 키워드 태그 표시 요청");
                                await predviaMainWindow.TriggerKeywordTagsDisplay();
                                LogWindow.AddLogStatic("✅ 소싱 페이지 키워드 태그 표시 완료");
                            }
                            else
                            {
                                LogWindow.AddLogStatic($"❌ MainWindow 타입 불일치: {mainWindow?.GetType().Name}");
                            }
                        }
                        else
                        {
                            LogWindow.AddLogStatic($"❌ ApplicationLifetime 타입 불일치: {app?.ApplicationLifetime?.GetType().Name}");
                        }
                    }
                    catch (Exception innerEx)
                    {
                        LogWindow.AddLogStatic($"❌ UI 스레드 내부 오류: {innerEx.Message}");
                    }
                });
            }
            catch (Exception ex)
            {
                LogWindow.AddLogStatic($"❌ 키워드 태그 표시 오류: {ex.Message}");
            }
        }
        private async Task<IResult> HandleGetLatestKeywords(HttpContext context)
        {
            // ⭐ 쿼리 파라미터에서 productId 가져오기
            var productIdStr = context.Request.Query["productId"].ToString();
            var productId = int.TryParse(productIdStr, out var id) ? id : 0;
            
            LogWindow.AddLogStatic($"🔍 키워드 조회 요청: productId={productId}");
            
            object responseData;
            
            lock (_keywordsLock)
            {
                LogWindow.AddLogStatic($"🔍 저장된 키워드 개수: {_productKeywords.Count}개, 최신 키워드: {_latestKeywords.Count}개 (시간: {_latestKeywordsTime:HH:mm:ss.fff})");
                
                if (_productKeywords.TryGetValue(productId, out var keywords))
                {
                    LogWindow.AddLogStatic($"✅ productId={productId} 키워드 {keywords.Count}개 반환");
                    responseData = new { 
                        success = true,
                        productId = productId,
                        keywords = keywords,
                        filteredCount = keywords.Count
                    };
                }
                else
                {
                    // ⭐ 해당 상품의 키워드가 없으면 빈 배열 반환 (다른 상품 키워드 복사 금지)
                    LogWindow.AddLogStatic($"❌ productId={productId} 키워드 없음 - 빈 배열 반환");
                    responseData = new { 
                        success = true,
                        productId = productId,
                        keywords = new List<string>(),
                        filteredCount = 0
                    };
                }
            }
            
            // ⭐ 직접 JSON 응답 작성
            context.Response.ContentType = "application/json; charset=utf-8";
            var json = JsonSerializer.Serialize(responseData);
            await context.Response.WriteAsync(json);
            return Results.Ok();
        }

        // ⭐ 크롤링 플래그 리셋 API (항상 true 유지)
        private async Task<IResult> HandleResetCrawling()
        {
            await Task.CompletedTask;
            // _crawlingAllowed는 항상 true 유지
            return Results.Json(new { success = true });
        }

        // ⭐ 현재 상품 ID 설정 API
        private async Task<IResult> HandleSetCurrentProduct(HttpContext context)
        {
            try
            {
                var body = await new StreamReader(context.Request.Body).ReadToEndAsync();
                LogWindow.AddLogStatic($"📥 현재 상품 ID 설정 요청 수신: {body}");
                
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var request = JsonSerializer.Deserialize<SetCurrentProductRequest>(body, options);
                
                if (request == null)
                {
                    LogWindow.AddLogStatic("❌ 요청 데이터 역직렬화 실패");
                    return Results.Json(new { success = false, message = "요청 데이터가 없습니다." });
                }
                
                lock (_keywordsLock)
                {
                    _currentProductId = request.ProductId;
                    LogWindow.AddLogStatic($"✅ 현재 상품 ID 설정: {_currentProductId}");
                }
                
                return Results.Json(new { success = true, productId = _currentProductId });
            }
            catch (Exception ex)
            {
                LogWindow.AddLogStatic($"❌ 현재 상품 ID 설정 오류: {ex.Message}");
                return Results.Json(new { success = false, message = ex.Message });
            }
        }

        // ⭐ 가격 필터링 설정 조회 API
        private static async Task<IResult> HandleGetPriceFilterSettings(HttpContext context)
        {
            try
            {
                var settings = new
                {
                    enabled = _priceFilterEnabled,
                    minPrice = _minPrice,
                    maxPrice = _maxPrice
                };
                
                context.Response.ContentType = "application/json; charset=utf-8";
                await context.Response.WriteAsync(JsonSerializer.Serialize(settings));
                return Results.Ok();
            }
            catch (Exception ex)
            {
                LogWindow.AddLogStatic($"❌ 가격 필터링 설정 조회 오류: {ex.Message}");
                context.Response.ContentType = "application/json; charset=utf-8";
                await context.Response.WriteAsync(JsonSerializer.Serialize(new { success = false, error = ex.Message }));
                return Results.Ok();
            }
        }

        // ⭐ 가격 필터링 설정 변경 API
        private static async Task<IResult> HandleSetPriceFilterSettings(HttpContext context)
        {
            try
            {
                var body = await new StreamReader(context.Request.Body).ReadToEndAsync();
                var settings = JsonSerializer.Deserialize<PriceFilterSettings>(body);
                
                if (settings != null)
                {
                    _priceFilterEnabled = settings.Enabled;
                    _minPrice = settings.MinPrice;
                    _maxPrice = settings.MaxPrice;
                    
                    LogWindow.AddLogStatic($"✅ 가격 필터링 설정 변경: {(_priceFilterEnabled ? "활성화" : "비활성화")} ({_minPrice}~{_maxPrice}원)");
                }
                
                context.Response.ContentType = "application/json; charset=utf-8";
                await context.Response.WriteAsync(JsonSerializer.Serialize(new { success = true }));
                return Results.Ok();
            }
            catch (Exception ex)
            {
                LogWindow.AddLogStatic($"❌ 가격 필터링 설정 변경 오류: {ex.Message}");
                context.Response.ContentType = "application/json; charset=utf-8";
                await context.Response.WriteAsync(JsonSerializer.Serialize(new { success = false, error = ex.Message }));
                return Results.Ok();
            }
        }

        public class PriceFilterSettings
        {
            [JsonPropertyName("enabled")]
            public bool Enabled { get; set; }
            
            [JsonPropertyName("minPrice")]
            public int MinPrice { get; set; }
            
            [JsonPropertyName("maxPrice")]
            public int MaxPrice { get; set; }
        }
    }

    // ⭐ 현재 상품 ID 설정 요청 모델
    public class SetCurrentProductRequest
    {
        public int ProductId { get; set; }
    }

    // 스마트스토어 링크 요청 데이터 모델
    public class SmartStoreLinkRequest
    {
        [JsonPropertyName("smartStoreLinks")]
        public List<SmartStoreLink> SmartStoreLinks { get; set; } = new();
        
        [JsonPropertyName("source")]
        public string Source { get; set; } = string.Empty;
        
        [JsonPropertyName("timestamp")]
        public string Timestamp { get; set; } = string.Empty;
        
        [JsonPropertyName("pageUrl")]
        public string PageUrl { get; set; } = string.Empty;
    }

    // 스마트스토어 링크 데이터 모델
    public class SmartStoreLink
    {
        [JsonPropertyName("url")]
        public string Url { get; set; } = string.Empty;
        
        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;
        
        [JsonPropertyName("seller")]
        public string Seller { get; set; } = string.Empty;
        
        [JsonPropertyName("storeId")]
        public string StoreId { get; set; } = string.Empty;
    }

    // 스마트스토어 방문 요청 데이터 모델
    public class SmartStoreVisitRequest
    {
        [JsonPropertyName("url")]
        public string Url { get; set; } = string.Empty;
        
        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;
        
        [JsonPropertyName("storeId")]
        public string StoreId { get; set; } = string.Empty;
        
        [JsonPropertyName("gongguUrl")]
        public string GongguUrl { get; set; } = string.Empty;
        
        [JsonPropertyName("currentIndex")]
        public int CurrentIndex { get; set; }
        
        [JsonPropertyName("totalCount")]
        public int TotalCount { get; set; }
        
        [JsonPropertyName("timestamp")]
        public string Timestamp { get; set; } = string.Empty;
    }

    // 공구 개수 확인 요청 데이터 모델
    public class GongguCheckRequest
    {
        [JsonPropertyName("storeId")]
        public string StoreId { get; set; } = string.Empty;
        
        [JsonPropertyName("gongguCount")]
        public int GongguCount { get; set; }
        
        [JsonPropertyName("isValid")]
        public bool IsValid { get; set; }
        
        [JsonPropertyName("timestamp")]
        public string Timestamp { get; set; } = string.Empty;
    }

    // 전체상품 페이지 요청 데이터 모델
    public class AllProductsPageRequest
    {
        [JsonPropertyName("storeId")]
        public string StoreId { get; set; } = string.Empty;
        
        [JsonPropertyName("pageType")]
        public string PageType { get; set; } = string.Empty;
        
        [JsonPropertyName("pageUrl")]
        public string PageUrl { get; set; } = string.Empty;
        
        [JsonPropertyName("timestamp")]
        public string Timestamp { get; set; } = string.Empty;
    }

    // 상품 데이터 요청 모델
    public class ProductDataRequest
    {
        [JsonPropertyName("storeId")]
        public string StoreId { get; set; } = string.Empty;
        
        [JsonPropertyName("productCount")]
        public int ProductCount { get; set; }
        
        [JsonPropertyName("reviewProductCount")]
        public int ReviewProductCount { get; set; }
        
        [JsonPropertyName("products")]
        public List<ProductInfo> Products { get; set; } = new();
        
        [JsonPropertyName("pageUrl")]
        public string PageUrl { get; set; } = string.Empty;
        
        [JsonPropertyName("timestamp")]
        public string Timestamp { get; set; } = string.Empty;
    }

    // Chrome 확장프로그램 로그 요청 데이터 모델
    public class ExtensionLogRequest
    {
        [JsonPropertyName("message")]
        public string Message { get; set; } = string.Empty;
        
        [JsonPropertyName("timestamp")]
        public string Timestamp { get; set; } = string.Empty;
    }

    // 상품 정보 모델
    public class ProductInfo
    {
        [JsonPropertyName("index")]
        public int Index { get; set; }
        
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;
        
        [JsonPropertyName("price")]
        public string Price { get; set; } = string.Empty;
        
        [JsonPropertyName("imageUrl")]
        public string ImageUrl { get; set; } = string.Empty;
        
        [JsonPropertyName("reviewCount")]
        public string ReviewCount { get; set; } = string.Empty;
        
        [JsonPropertyName("element")]
        public string Element { get; set; } = string.Empty;
    }

    // ⭐ 스토어 상태 모델
    public class StoreState
    {
        [JsonPropertyName("storeId")]
        public string StoreId { get; set; } = string.Empty;
        
        [JsonPropertyName("runId")]
        public string RunId { get; set; } = string.Empty;
        
        [JsonPropertyName("state")]
        public string State { get; set; } = string.Empty; // collecting_gonggu, collecting_category, collecting_products, visiting, done
        
        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty; // collecting, visiting, done
        
        [JsonPropertyName("lock")]
        public bool Lock { get; set; } = false;
        
        [JsonPropertyName("isLocked")]
        public bool IsLocked { get; set; } = false;
        
        [JsonPropertyName("expected")]
        public int Expected { get; set; } = 0;
        
        [JsonPropertyName("progress")]
        public int Progress { get; set; } = 0;
        
        [JsonPropertyName("productCount")]
        public int ProductCount { get; set; } = 0;
        
        [JsonPropertyName("updatedAt")]
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
        
        // ⭐ 진행률 정체 감지용
        [JsonPropertyName("lastProgress")]
        public int LastProgress { get; set; } = -1;
        
        [JsonPropertyName("stuckCount")]
        public int StuckCount { get; set; } = 0;
    }

    // ⭐ 차단 정보 모델
    public class BlockedStoreInfo
    {
    [JsonPropertyName("storeId")]
    public string StoreId { get; set; } = string.Empty;
    
    [JsonPropertyName("runId")]
    public string RunId { get; set; } = string.Empty;
    
    [JsonPropertyName("currentIndex")]
    public int CurrentIndex { get; set; }
    
    [JsonPropertyName("totalProducts")]
    public int TotalProducts { get; set; }
    
    [JsonPropertyName("productUrls")]
    public List<string> ProductUrls { get; set; } = new();
    
    [JsonPropertyName("timestamp")]
    public long Timestamp { get; set; }
}

// ⭐ 카테고리 데이터 모델
public class CategoryData
{
    [JsonPropertyName("storeId")]
    public string StoreId { get; set; } = "";

    [JsonPropertyName("categories")]
    public List<CategoryInfo> Categories { get; set; } = new();

    [JsonPropertyName("pageUrl")]
    public string PageUrl { get; set; } = "";

    [JsonPropertyName("extractedAt")]
    public string ExtractedAt { get; set; } = "";
}

public class CategoryInfo
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("url")]
    public string Url { get; set; } = "";

    [JsonPropertyName("categoryId")]
    public string CategoryId { get; set; } = "";

    [JsonPropertyName("order")]
    public int Order { get; set; }
}

// ⭐ 개별 상품 카테고리 데이터 모델
public class ProductCategoryData
{
    [JsonPropertyName("storeId")]
    public string StoreId { get; set; } = "";

    [JsonPropertyName("productId")]
    public string ProductId { get; set; } = "";

    [JsonPropertyName("categories")]
    public List<CategoryInfo> Categories { get; set; } = new();

    [JsonPropertyName("pageUrl")]
    public string PageUrl { get; set; } = "";

    [JsonPropertyName("extractedAt")]
    public string ExtractedAt { get; set; } = "";
}

// ⭐ 상품 이미지 데이터 모델
    public class ProductImageData
    {
        [JsonPropertyName("storeId")]
        public string StoreId { get; set; } = string.Empty;
        
        [JsonPropertyName("productId")]
        public string ProductId { get; set; } = string.Empty;
        
        [JsonPropertyName("imageUrl")]
        public string ImageUrl { get; set; } = string.Empty;
        
        [JsonPropertyName("productUrl")]
        public string ProductUrl { get; set; } = string.Empty;
    }

    // ⭐ 상품명 데이터 모델
    public class ProductNameData
    {
        [JsonPropertyName("storeId")]
        public string StoreId { get; set; } = string.Empty;
        
        [JsonPropertyName("productId")]
        public string ProductId { get; set; } = string.Empty;
        
        [JsonPropertyName("productName")]
        public string ProductName { get; set; } = string.Empty;
        
        [JsonPropertyName("productUrl")]
        public string ProductUrl { get; set; } = string.Empty;
    }

    // ⭐ 가격 데이터 모델
    public class ProductPriceData
    {
        [JsonPropertyName("storeId")]
        public string StoreId { get; set; } = string.Empty;
        
        [JsonPropertyName("productId")]
        public string ProductId { get; set; } = string.Empty;
        
        [JsonPropertyName("price")]
        public string Price { get; set; } = string.Empty;
        
        [JsonPropertyName("priceText")]
        public string PriceText { get; set; } = string.Empty;
        
        [JsonPropertyName("productUrl")]
        public string ProductUrl { get; set; } = string.Empty;
    }

    // ⭐ 리뷰 데이터 모델
    public class ProductReviewsData
    {
        [JsonPropertyName("storeId")]
        public string StoreId { get; set; } = string.Empty;
        
        [JsonPropertyName("productId")]
        public string ProductId { get; set; } = string.Empty;
        
        [JsonPropertyName("productUrl")]
        public string ProductUrl { get; set; } = string.Empty;
        
        [JsonPropertyName("reviews")]
        public List<ReviewData> Reviews { get; set; } = new List<ReviewData>();
        
        [JsonPropertyName("reviewCount")]
        public int ReviewCount { get; set; }
        
        [JsonPropertyName("timestamp")]
        public DateTime Timestamp { get; set; } = DateTime.Now;
    }

    public class ReviewData
    {
        [JsonPropertyName("rating")]
        public string Rating { get; set; } = "0";
        
        [JsonPropertyName("content")]
        public string Content { get; set; } = string.Empty;
        
        [JsonPropertyName("ratingText")]
        public string RatingText { get; set; } = string.Empty;
        
        [JsonPropertyName("recentRating")]
        public string RecentRating { get; set; } = string.Empty;
    }

    // URL에서 스토어 ID 추출 확장 메서드
    public static class UrlExtensions
    {
        public static string ExtractStoreIdFromUrl(string url)
        {
            try
            {
                var storeId = "";
                
                if (!string.IsNullOrEmpty(url) && url.Contains("smartstore.naver.com/"))
                {
                    var decoded = Uri.UnescapeDataString(url);
                    // ⭐ inflow URL에서 실제 스토어 ID 추출
                    if (decoded.Contains("inflow/outlink/url?url="))
                    {
                        var innerUrlMatch = System.Text.RegularExpressions.Regex.Match(decoded, @"url=([^&]+)");
                        if (innerUrlMatch.Success)
                        {
                            var innerUrl = Uri.UnescapeDataString(innerUrlMatch.Groups[1].Value);
                            var storeMatch = System.Text.RegularExpressions.Regex.Match(innerUrl, @"smartstore\.naver\.com/([^/&?]+)");
                            if (storeMatch.Success)
                            {
                                storeId = storeMatch.Groups[1].Value;
                            }
                        }
                    }
                    else
                    {
                        // 일반 smartstore URL
                        var match = System.Text.RegularExpressions.Regex.Match(decoded, @"smartstore\.naver\.com/([^/&?]+)");
                        if (match.Success)
                        {
                            storeId = match.Groups[1].Value;
                        }
                    }
                }
                
                return storeId ?? "unknown";
            }
            catch (Exception)
            {
                return "unknown";
            }
        }
    }

    // ⭐ 상품명 요청 데이터 모델
    public class ProductNamesRequest
    {
        [JsonPropertyName("productNames")]
        public List<string> ProductNames { get; set; } = new();
        
        [JsonPropertyName("productId")]
        public int ProductId { get; set; } = 0;
        
        [JsonPropertyName("pageUrl")]
        public string PageUrl { get; set; } = string.Empty;
        
        [JsonPropertyName("timestamp")]
        public string Timestamp { get; set; } = string.Empty;
    }
    
    // 타오바오 이미지 업로드 요청 데이터
    public class TaobaoImageUploadRequest
    {
        [JsonPropertyName("imagePath")]
        public string ImagePath { get; set; } = string.Empty;
        
        [JsonPropertyName("productId")]
        public string ProductId { get; set; } = string.Empty;
        
        [JsonPropertyName("products")]
        public List<TaobaoProduct>? Products { get; set; }
    }
    
    // 타오바오 상품 정보 (파이썬 extract_products와 일치)
    public class TaobaoProduct
    {
        [JsonPropertyName("nid")]
        public string ProductId { get; set; } = string.Empty;
        
        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;
        
        [JsonPropertyName("price")]
        public string Price { get; set; } = string.Empty;
        
        [JsonPropertyName("url")]
        public string ProductUrl { get; set; } = string.Empty;
        
        [JsonPropertyName("review_count")]
        public int ReviewCount { get; set; } = 0;
        
        [JsonPropertyName("shop")]
        public string ShopName { get; set; } = string.Empty;
        
        [JsonPropertyName("img")]
        public string ImageUrl { get; set; } = string.Empty;
        
        // UI 표시용 추가 필드
        [JsonPropertyName("sales")]
        public string Sales { get; set; } = string.Empty;
    }
    
    // ⭐ 프록시 기반 타오바오 이미지 검색 요청 모델
    public class TaobaoProxySearchRequest
    {
        [JsonPropertyName("imagePath")]
        public string ImagePath { get; set; } = string.Empty;
        
        [JsonPropertyName("imageBase64")]
        public string? ImageBase64 { get; set; }
        
        [JsonPropertyName("productId")]
        public int ProductId { get; set; }
    }
    
    // ⭐ 구글렌즈 검색 요청 모델
    public class GoogleLensSearchRequest
    {
        [JsonPropertyName("productId")]
        public int ProductId { get; set; }
        
        [JsonPropertyName("imageBase64")]
        public string ImageBase64 { get; set; } = string.Empty;
    }
    
    // 🔄 소싱 페이지에서 직접 로딩창 숨김
    public static class LoadingHelper
    {
        public static void HideLoadingFromSourcingPage()
        {
            try
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    // 모든 윈도우에서 SourcingPage 찾기
                    foreach (var window in Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop 
                        ? desktop.Windows : new List<Avalonia.Controls.Window>())
                    {
                        if (window is MainWindow mainWindow)
                        {
                            mainWindow.HideLoading();
                            LogWindow.AddLogStatic("✅ 로딩창 숨김 완료 (소싱페이지 경로)");
                            return;
                        }
                    }
                    LogWindow.AddLogStatic("❌ MainWindow를 찾을 수 없음 (소싱페이지 경로)");
                });
            }
            catch (Exception ex)
            {
                LogWindow.AddLogStatic($"❌ 로딩창 숨김 오류: {ex.Message}");
            }
        }

        public static void HideLoadingOverlay()
        {
            HideLoadingFromSourcingPage();
        }
    }

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    // 서버 주도 크롤링 상태 머신
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    public class CrawlStateMachine
    {
        public enum Phase { Idle, WaitingLinks, GongguCheck, WaitingGonggu, AllProducts, WaitingProducts, VisitingProduct, WaitingProductData, Done }

        public Phase CurrentPhase { get; private set; } = Phase.Idle;
        public List<CrawlStore> Stores { get; private set; } = new();
        public int CurrentStoreIdx { get; private set; } = 0;
        public int CurrentProductIdx { get; private set; } = 0;
        public List<string> CurrentProductList { get; private set; } = new();
        public int SuccessCount { get; private set; } = 0;
        public int TotalAttempted { get; private set; } = 0;
        public bool IsCompleted => CurrentPhase == Phase.Done;
        public string? CurrentTabUrl { get; private set; }

        private readonly int _targetCount;
        private readonly int _minPrice;
        private readonly int _maxPrice;
        private readonly bool _priceFilterEnabled;

        public CrawlStateMachine(int targetCount = 100, int minPrice = 0, int maxPrice = int.MaxValue, bool priceFilterEnabled = true)
        {
            _targetCount = targetCount;
            _minPrice = minPrice;
            _maxPrice = maxPrice;
            _priceFilterEnabled = priceFilterEnabled;
        }

        public void SetStores(List<CrawlStore> stores)
        {
            Stores = stores;
            CurrentStoreIdx = 0;
            CurrentPhase = stores.Count > 0 ? Phase.GongguCheck : Phase.Done;
        }

        public object GetNextTask()
        {
            if (SuccessCount >= _targetCount)
            {
                CurrentPhase = Phase.Done;
                return new { action = "done", reason = "목표 달성", success = SuccessCount, attempted = TotalAttempted };
            }

            if (CurrentStoreIdx >= Stores.Count)
            {
                CurrentPhase = Phase.Done;
                return new { action = "done", reason = "모든 스토어 완료", success = SuccessCount, attempted = TotalAttempted };
            }

            var store = Stores[CurrentStoreIdx];

            switch (CurrentPhase)
            {
                case Phase.Idle:
                case Phase.WaitingLinks:
                    return new { action = "wait", reason = "링크 수집 대기" };

                case Phase.GongguCheck:
                    CurrentPhase = Phase.WaitingGonggu;
                    var gongguUrl = $"https://smartstore.naver.com/{store.StoreId}/category/50000165?cp=1";
                    CurrentTabUrl = gongguUrl;
                    return new { action = "open_gonggu", url = gongguUrl, storeId = store.StoreId, storeIdx = CurrentStoreIdx, totalStores = Stores.Count };

                case Phase.WaitingGonggu:
                    return new { action = "wait", reason = "공구 개수 확인 중", storeId = store.StoreId };

                case Phase.AllProducts:
                    CurrentPhase = Phase.WaitingProducts;
                    var allUrl = $"https://smartstore.naver.com/{store.StoreId}/category/ALL?st=TOTALSALE";
                    CurrentTabUrl = allUrl;
                    return new { action = "open_all_products", url = allUrl, storeId = store.StoreId };

                case Phase.WaitingProducts:
                    return new { action = "wait", reason = "상품 목록 수집 중", storeId = store.StoreId };

                case Phase.VisitingProduct:
                    if (CurrentProductIdx >= CurrentProductList.Count)
                    {
                        MoveToNextStore();
                        return GetNextTask();
                    }
                    CurrentPhase = Phase.WaitingProductData;
                    var productId = CurrentProductList[CurrentProductIdx];
                    var productUrl = $"https://smartstore.naver.com/{store.StoreId}/products/{productId}";
                    CurrentTabUrl = productUrl;
                    return new { action = "open_product", url = productUrl, storeId = store.StoreId, productId = productId, productIdx = CurrentProductIdx, totalProducts = CurrentProductList.Count };

                case Phase.WaitingProductData:
                    return new { action = "wait", reason = "상품 데이터 수집 중", storeId = store.StoreId };

                case Phase.Done:
                    return new { action = "done", reason = "크롤링 완료", success = SuccessCount, attempted = TotalAttempted };

                default:
                    return new { action = "wait", reason = "알 수 없는 상태" };
            }
        }

        public void ReportGongguResult(string storeId, int count)
        {
            if (CurrentPhase != Phase.WaitingGonggu) return;
            var store = Stores[CurrentStoreIdx];
            if (store.StoreId != storeId) return;

            store.GongguCount = count;
            if (count >= 1000)
            {
                store.Status = "products";
                CurrentPhase = Phase.AllProducts;
                LogWindow.AddLogStatic($"✅ {storeId}: 공구 {count}개 → 전체상품 진행");
            }
            else
            {
                store.Status = "skip";
                LogWindow.AddLogStatic($"⏭️ {storeId}: 공구 {count}개 → 스킵");
                MoveToNextStore();
            }
        }

        public void ReportNoGonggu(string storeId)
        {
            if (CurrentPhase != Phase.WaitingGonggu) return;
            var store = Stores[CurrentStoreIdx];
            if (store.StoreId != storeId) return;

            store.Status = "skip";
            LogWindow.AddLogStatic($"⏭️ {storeId}: 공구탭 없음 → 스킵");
            MoveToNextStore();
        }

        public void ReportProductList(string storeId, List<string> productIds)
        {
            if (CurrentPhase != Phase.WaitingProducts) return;
            var store = Stores[CurrentStoreIdx];
            if (store.StoreId != storeId) return;

            CurrentProductList = productIds;
            CurrentProductIdx = 0;
            CurrentPhase = Phase.VisitingProduct;
            LogWindow.AddLogStatic($"📋 {storeId}: {productIds.Count}개 상품 목록 수신");
        }

        public void ReportProductData(string storeId, string productId, int priceValue, bool hasImage, bool hasName)
        {
            if (CurrentPhase != Phase.WaitingProductData) return;

            TotalAttempted++;

            // 가격 필터링
            bool priceOk = !_priceFilterEnabled || (priceValue >= _minPrice && priceValue <= _maxPrice);
            if (priceOk && hasImage && hasName)
            {
                SuccessCount++;
                LogWindow.AddLogStatic($"✅ {storeId}/{productId}: 성공 ({SuccessCount}/{_targetCount})");
            }
            else
            {
                string reason = !priceOk ? $"가격 {priceValue}원 범위 밖" : "데이터 부족";
                LogWindow.AddLogStatic($"⏭️ {storeId}/{productId}: {reason}");
            }

            CurrentProductIdx++;
            CurrentPhase = Phase.VisitingProduct;

            if (SuccessCount >= _targetCount)
                CurrentPhase = Phase.Done;
        }

        public void ReportPageLoadTimeout(string storeId)
        {
            TotalAttempted++;
            CurrentProductIdx++;
            CurrentPhase = Phase.VisitingProduct;
            LogWindow.AddLogStatic($"⏱️ {storeId}: 페이지 로드 타임아웃");
        }

        private void MoveToNextStore()
        {
            CurrentStoreIdx++;
            CurrentProductList.Clear();
            CurrentProductIdx = 0;
            CurrentPhase = CurrentStoreIdx < Stores.Count ? Phase.GongguCheck : Phase.Done;
        }

        public void Reset()
        {
            CurrentPhase = Phase.Idle;
            Stores.Clear();
            CurrentStoreIdx = 0;
            CurrentProductIdx = 0;
            CurrentProductList.Clear();
            SuccessCount = 0;
            TotalAttempted = 0;
            CurrentTabUrl = null;
        }
    }

    public class CrawlStore
    {
        public string StoreId { get; set; } = "";
        public string Url { get; set; } = "";
        public string Title { get; set; } = "";
        public string Status { get; set; } = "pending"; // pending, gonggu, products, visiting, done, skip
        public int GongguCount { get; set; } = 0;
    }
}
