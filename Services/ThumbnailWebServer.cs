using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;

namespace Gumaedaehang.Services
{
    public class ThumbnailWebServer
    {
        private WebApplication? _app;
        private readonly ThumbnailService _thumbnailService;
        private bool _isRunning = false;
        
        // 정적 IsRunning 속성
        public static bool IsRunning { get; private set; } = false;
        
        // ⭐ 상태 관리 시스템
        private readonly Dictionary<string, StoreState> _storeStates = new();
        private readonly object _statesLock = new object();
        
        // ⭐ 상품 카운터 및 랜덤 선택 관련 변수
        private int _productCount = 0;
        private bool _isCrawlingActive = false;
        private int _totalProductCount = 0;
        private const int TARGET_PRODUCT_COUNT = 100;
        private const int MAX_STORES_TO_VISIT = 10;
        private List<SmartStoreLink> _selectedStores = new();
        private int _currentStoreIndex = 0; // 현재 처리 중인 스토어 인덱스
        private readonly object _storeProcessLock = new object(); // 스토어 처리 동기화
        private bool _shouldStop = false;
        private readonly object _counterLock = new object();
        
        // ⭐ 중복 처리 방지를 위한 처리된 스토어 추적
        private readonly HashSet<string> _processedStores = new HashSet<string>();
        
        // ⭐ 크롤링 허용 플래그
        private bool _crawlingAllowed = false;
        private readonly object _crawlingLock = new object();

        // ⭐ 최신 키워드 저장
        private List<string> _latestKeywords = new();
        private readonly object _keywordsLock = new object();

        public ThumbnailWebServer()
        {
            _thumbnailService = new ThumbnailService();
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
                
                // ⭐ 기존 데이터 초기화
                ClearPreviousData();
                
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
                _app.MapPost("/api/smartstore/image", HandleProductImage); // ⭐ 상품 이미지 처리 API 추가
                _app.MapPost("/api/smartstore/product-name", HandleProductName); // ⭐ 상품명 처리 API 추가
                _app.MapPost("/api/smartstore/reviews", HandleProductReviews); // ⭐ 리뷰 처리 API 추가
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
                
                // ⭐ 상품명 처리 API 추가
                _app.MapPost("/api/smartstore/product-names", HandleProductNames);
                _app.MapGet("/api/smartstore/latest-keywords", HandleGetLatestKeywords);
                _app.MapPost("/api/smartstore/trigger-keywords", HandleTriggerKeywords);
                
                LogWindow.AddLogStatic("✅ API 엔드포인트 등록 완료 (19개)");

                // ⭐ 서버 변수 초기화
                lock (_counterLock)
                {
                    _totalProductCount = 0;
                    _shouldStop = false;
                }
                
                lock (_statesLock)
                {
                    _storeStates.Clear();
                }
                
                _selectedStores.Clear();
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
                    _totalProductCount = 0;
                    _shouldStop = false;
                    _processedStores.Clear(); // ⭐ 처리된 스토어 목록도 초기화
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
                        
                        // ⭐ 크롤링 완료 시 팝업창 표시
                        var finalCount = GetCurrentProductCount();
                        ShowCrawlingResultPopup(finalCount, "모든 스토어 처리 완료");
                        
                        // ⭐ 크롬 탭 닫기
                        _ = Task.Run(() => CloseAllChromeTabs());
                        
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

                // ⭐ 목표 달성 시 중단
                lock (_counterLock)
                {
                    if (_shouldStop || _totalProductCount >= TARGET_PRODUCT_COUNT)
                    {
                        LogWindow.AddLogStatic($"목표 달성으로 크롤링 중단: {_totalProductCount}/{TARGET_PRODUCT_COUNT}");
                        return Results.Ok(new { 
                            success = true, 
                            stop = true,
                            totalProducts = _totalProductCount,
                            message = "Target reached, stopping crawl" 
                        });
                    }
                }

                LogWindow.AddLogStatic($"[{visitData.CurrentIndex}/{visitData.TotalCount}] 스마트스토어 공구탭 접속: {visitData.Title}");
                LogWindow.AddLogStatic($"현재 상품 수: {_totalProductCount}/{TARGET_PRODUCT_COUNT}");

                var response = new { 
                    success = true,
                    currentProducts = _totalProductCount,
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
                            LogWindow.AddLogStatic($"❌ 순차 처리 위반 - 현재: {currentStoreId}, 요청: {gongguData.StoreId} 차단");
                            
                            // ⭐ 이전 스토어 요청이면 즉시 완료 처리
                            var prevStoreIndex = _currentStoreIndex - 1;
                            if (prevStoreIndex >= 0 && prevStoreIndex < _selectedStores.Count)
                            {
                                var prevStoreId = UrlExtensions.ExtractStoreIdFromUrl(_selectedStores[prevStoreIndex].Url);
                                if (gongguData.StoreId.Equals(prevStoreId, StringComparison.OrdinalIgnoreCase))
                                {
                                    LogWindow.AddLogStatic($"🔄 이전 스토어 {gongguData.StoreId} 공구 체크 - 즉시 완료 처리");
                                    return Results.Json(new { 
                                        success = true, 
                                        message = "이전 스토어 완료 처리됨" 
                                    });
                                }
                            }
                            
                            return Results.Json(new { 
                                success = false, 
                                message = "순차 처리 대기 중" 
                            });
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
                            _currentStoreIndex++;
                            LogWindow.AddLogStatic($"📈 다음 스토어로 이동: {_currentStoreIndex}/{_selectedStores.Count}");
                        }
                    }
                }

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
                            LogWindow.AddLogStatic($"❌ 순차 처리 위반 - 현재: {currentStoreId}, 요청: {pageData.StoreId} 차단");
                            
                            // ⭐ 이전 스토어 요청이면 즉시 완료 처리
                            var prevStoreIndex = _currentStoreIndex - 1;
                            if (prevStoreIndex >= 0 && prevStoreIndex < _selectedStores.Count)
                            {
                                var prevStoreId = UrlExtensions.ExtractStoreIdFromUrl(_selectedStores[prevStoreIndex].Url);
                                if (pageData.StoreId.Equals(prevStoreId, StringComparison.OrdinalIgnoreCase))
                                {
                                    LogWindow.AddLogStatic($"🔄 이전 스토어 {pageData.StoreId} 전체상품 페이지 - 즉시 완료 처리");
                                    return Results.Json(new { 
                                        success = true, 
                                        message = "이전 스토어 완료 처리됨" 
                                    });
                                }
                            }
                            
                            return Results.Json(new { 
                                success = false, 
                                message = "순차 처리 대기 중" 
                            });
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
                // ⭐ v1.39 수정: 100개 목표 달성 시 즉시 중단
                lock (_counterLock)
                {
                    if (_shouldStop || _totalProductCount >= TARGET_PRODUCT_COUNT)
                    {
                        LogWindow.AddLogStatic($"🛑 100개 목표 달성으로 추가 상품 처리 중단 (현재: {_totalProductCount}/100)");
                        return Results.Json(new { 
                            success = true,
                            stop = true,
                            totalProducts = _totalProductCount,
                            message = "Target reached, stopping crawling" 
                        });
                    }
                }
                
                using var reader = new StreamReader(context.Request.Body);
                var json = await reader.ReadToEndAsync();
                
                ProductDataRequest? productData = null;
                try
                {
                    productData = JsonSerializer.Deserialize<ProductDataRequest>(json);
                }
                catch (Exception jsonEx)
                {
                    LogWindow.AddLogStatic($"❌ 상품 데이터 JSON 파싱 오류: {jsonEx.Message}");
                    return Results.Json(new { 
                        success = false, 
                        error = "Invalid JSON format" 
                    }, statusCode: 400);
                }
                
                // ⭐ 크롤링 중단 체크 - 차단 시 즉시 중단
                lock (_counterLock)
                {
                    if (_shouldStop)
                    {
                        LogWindow.AddLogStatic($"🛑 크롤링 중단됨 - {productData.StoreId ?? "Unknown"} 데이터 무시");
                        return Results.Json(new { 
                            success = true,
                            stop = true,
                            totalProducts = _totalProductCount,
                            message = "Crawling stopped" 
                        });
                    }
                }
                
                if (productData != null)
                {
                    // ⭐ 선택된 스토어인지 엄격하게 확인
                    var selectedStoreIds = new List<string>();
                    foreach (var store in _selectedStores)
                    {
                        var url = store.Url;
                        if (url.Contains("smartstore.naver.com/"))
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
                                        selectedStoreIds.Add(storeMatch.Groups[1].Value);
                                    }
                                }
                            }
                            else
                            {
                                // 일반 smartstore URL
                                var match = System.Text.RegularExpressions.Regex.Match(decoded, @"smartstore\.naver\.com/([^/&?]+)");
                                if (match.Success)
                                {
                                    selectedStoreIds.Add(match.Groups[1].Value);
                                }
                            }
                        }
                    }
                    
                    var isSelectedStore = selectedStoreIds.Contains(productData.StoreId, StringComparer.OrdinalIgnoreCase);
                    
                    LogWindow.AddLogStatic($"🔍 스토어 확인: {productData.StoreId} -> {(isSelectedStore ? "✅선택됨" : "❌선택안됨")}");
                    LogWindow.AddLogStatic($"🔍 선택된 스토어들: {string.Join(", ", selectedStoreIds)}");
                    
                    if (!isSelectedStore)
                    {
                        LogWindow.AddLogStatic($"❌ 선택되지 않은 스토어 상품 데이터 완전 무시: {productData.StoreId}");
                        return Results.Json(new { 
                            success = true,
                            skip = true,
                            message = "Store not selected, data completely ignored" 
                        });
                    }
                    
                    // ⭐ 순차 처리 체크 - 현재 처리할 스토어가 아니면 차단
                    lock (_storeProcessLock)
                    {
                        if (_currentStoreIndex < _selectedStores.Count)
                        {
                            var currentStore = _selectedStores[_currentStoreIndex];
                            var currentStoreId = UrlExtensions.ExtractStoreIdFromUrl(currentStore.Url);
                            
                            if (!productData.StoreId.Equals(currentStoreId, StringComparison.OrdinalIgnoreCase))
                            {
                                LogWindow.AddLogStatic($"🚫 순차 처리 위반 - 현재: {currentStoreId}, 상품 데이터: {productData.StoreId} 차단");
                                return Results.Json(new { 
                                    success = true,
                                    skip = true,
                                    message = "순차 처리 대기 중" 
                                });
                            }
                        }
                    }
                    
                    // ⭐ 100개 초과 방지 - 미리 체크
                    lock (_counterLock)
                    {
                        if (_shouldStop || _totalProductCount >= TARGET_PRODUCT_COUNT)
                        {
                            LogWindow.AddLogStatic($"🛑 이미 목표 달성으로 추가 상품 무시: {productData.StoreId} (현재: {_totalProductCount}/100)");
                            return Results.Json(new { 
                                success = true,
                                stop = true,
                                totalProducts = _totalProductCount,
                                message = "Target already reached, ignoring additional products" 
                            });
                        }
                    }
                    
                    // ⭐ 상품 카운터 업데이트 (정확히 100개까지만)
                    lock (_counterLock)
                    {
                        // ⭐ 중복 처리 방지 체크
                        if (_processedStores.Contains(productData.StoreId))
                        {
                            LogWindow.AddLogStatic($"🔄 이미 처리된 스토어 중복 요청 무시: {productData.StoreId}");
                            return Results.Json(new { 
                                success = true,
                                duplicate = true,
                                totalProducts = _totalProductCount,
                                message = "Store already processed, ignoring duplicate request" 
                            });
                        }
                        
                        // ⭐ 처리된 스토어로 등록
                        _processedStores.Add(productData.StoreId);
                        
                        var previousCount = _totalProductCount;
                        var productsToAdd = Math.Min(productData.ProductCount, TARGET_PRODUCT_COUNT - _totalProductCount);
                        
                        if (productsToAdd <= 0)
                        {
                            LogWindow.AddLogStatic($"🛑 더 이상 추가할 수 없음: {productData.StoreId} (현재: {_totalProductCount}/100)");
                            return Results.Json(new { 
                                success = true,
                                stop = true,
                                totalProducts = _totalProductCount,
                                message = "Cannot add more products, target reached" 
                            });
                        }
                        
                        // ⭐ 실시간 진행률 표시 (1/100 형태)
                        for (int i = 1; i <= productsToAdd; i++)
                        {
                            var currentCount = previousCount + i;
                            LogWindow.AddLogStatic($"📊 실시간 진행률: {currentCount}/100개 ({(currentCount * 100.0 / TARGET_PRODUCT_COUNT):F1}%)");
                        }
                        
                        _totalProductCount += productsToAdd;
                        
                        LogWindow.AddLogStatic($"✅ {productData.StoreId}: {productsToAdd}개 상품 추가 완료 (요청: {productData.ProductCount}개, 전체: {_totalProductCount}/100)");
                        
                        // ⭐ 정확히 100개 달성 시 중단
                        if (_totalProductCount >= TARGET_PRODUCT_COUNT)
                        {
                            _shouldStop = true;
                            _isCrawlingActive = false; // ⭐ 추가: 모든 데이터 처리 중단
                            LogWindow.AddLogStatic($"🎉 목표 달성! 정확히 100개 상품 수집 완료 - 크롤링 중단");
                            
                            // 🔄 로딩창 숨김 - 소싱 페이지에서 직접 처리
                            LoadingHelper.HideLoadingFromSourcingPage();
                            
                            // ⭐ 크롬 탭 닫기
                            _ = Task.Run(() => CloseAllChromeTabs());
                            

                            // ⭐ 팝업창으로 최종 결과 표시
                            ShowCrawlingResultPopup(_totalProductCount, "목표 달성");
                            
                            // 🔥 즉시 카드 생성
                            RefreshSourcingPage();
                        }
                    }
                    
                    // 상품 정보 로그 (처음 3개만)
                    for (int i = 0; i < Math.Min(3, productData.Products.Count); i++)
                    {
                        var product = productData.Products[i];
                        LogWindow.AddLogStatic($"  [{i + 1}] {product.Name} - {product.Price}");
                    }
                    
                    if (productData.Products.Count > 3)
                    {
                        LogWindow.AddLogStatic($"  ... 외 {productData.Products.Count - 3}개 상품");
                    }
                    
                    // ⭐ 스토어 완료 처리 - 다음 스토어로 이동
                    lock (_storeProcessLock)
                    {
                        _currentStoreIndex++;
                        LogWindow.AddLogStatic($"📈 다음 스토어로 이동: {_currentStoreIndex}/{_selectedStores.Count}");
                    }
                }

                // ⭐ 상품 데이터 처리 완료 - 무조건 다음 스토어로 이동
                lock (_storeProcessLock)
                {
                    _currentStoreIndex++;
                    LogWindow.AddLogStatic($"📈 다음 스토어로 이동: {_currentStoreIndex}/{_selectedStores.Count}");
                }

                return Results.Json(new { 
                    success = true,
                    totalProducts = _totalProductCount,
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
                            
                            // 🔥 순차 처리 - 다음 스토어로 이동
                            lock (_storeProcessLock)
                            {
                                _currentStoreIndex++;
                                LogWindow.AddLogStatic($"📈 다음 스토어로 이동: {_currentStoreIndex}/{_selectedStores.Count}");
                            }
                            
                            // 🔥 크롤링 완료 시 소싱 페이지 새로고침
                            RefreshSourcingPage();
                        }
                    }
                }
                
                // ⭐ collecting 상태 타임아웃 체크 (5초 이상 collecting 상태면 강제 완료)
                if (storeState.State == "collecting" && 
                    DateTime.Now - storeState.UpdatedAt > TimeSpan.FromSeconds(5))
                {
                    LogWindow.AddLogStatic($"{storeId}: collecting 상태 5초 타임아웃 - 강제 완료 처리");
                    
                    lock (_statesLock)
                    {
                        var key = $"{storeId}:{runId}";
                        if (_storeStates.ContainsKey(key))
                        {
                            _storeStates[key].State = "done";
                            _storeStates[key].Lock = false;
                            _storeStates[key].UpdatedAt = DateTime.Now;
                            storeState = _storeStates[key];
                            
                            // 🔥 순차 처리 - 다음 스토어로 이동
                            lock (_storeProcessLock)
                            {
                                _currentStoreIndex++;
                                LogWindow.AddLogStatic($"📈 다음 스토어로 이동: {_currentStoreIndex}/{_selectedStores.Count}");
                            }
                        }
                    }
                }
                
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
                    productCount = _totalProductCount,
                    targetCount = TARGET_PRODUCT_COUNT,
                    isRunning = !_shouldStop,
                    shouldStop = _shouldStop,  // ⭐ Chrome 확장프로그램이 기대하는 필드 추가
                    selectedStores = _selectedStores.Count,
                    progress = _totalProductCount * 100.0 / TARGET_PRODUCT_COUNT,
                    timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                };
                
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
                var reason = stopData.GetProperty("reason").GetString();
                var storeId = stopData.GetProperty("storeId").GetString();
                var message = stopData.GetProperty("message").GetString();
                
                LogWindow.AddLogStatic($"🚫 크롤링 중단 요청 수신: {reason}");
                LogWindow.AddLogStatic($"🚫 스토어: {storeId}");
                LogWindow.AddLogStatic($"🚫 사유: {message}");
                
                // ⭐ 즉시 크롤링 중단
                lock (_counterLock)
                {
                    // ⭐ 크롤링 중단
                    _shouldStop = true;
                    _isCrawlingActive = false; // ⭐ 추가: 모든 데이터 처리 중단
                    
                    // ⭐ 크롬 탭 닫기
                    _ = Task.Run(() => CloseAllChromeTabs());
                    
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
                    
                    LogWindow.AddLogStatic($"🛑 네이버 차단 감지로 인한 크롤링 강제 중단");
                    LogWindow.AddLogStatic($"📊 최종 수집 완료: {actualCount}/100개 ({(actualCount * 100.0 / 100):F1}%)");
                    
                    // ⭐ 팝업창으로 최종 결과 표시
                    ShowCrawlingResultPopup(actualCount, "차단 감지로 인한 중단");
                    
                    // ⭐ 80개 미만이면 Chrome 재시작
                    if (_totalProductCount < 80)
                    {
                        LogWindow.AddLogStatic($"🔄 80개 미만 수집 - 크롤링 완료");
                    }
                }
                
                // 🔥 차단으로 중단되어도 카드 생성
                RefreshSourcingPage();
                
                context.Response.ContentType = "application/json; charset=utf-8";
                context.Response.StatusCode = 200;
                await context.Response.WriteAsync("{\"success\":true,\"message\":\"Crawling stopped due to blocking\"}");
                
                return Results.Ok();
            }
            catch (Exception ex)
            {
                LogWindow.AddLogStatic($"❌ 크롤링 중단 API 오류: {ex.Message}");
                
                context.Response.ContentType = "application/json; charset=utf-8";
                context.Response.StatusCode = 500;
                await context.Response.WriteAsync("{\"success\":false,\"error\":\"Stop API error\"}");
                
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

        // ⭐ 크롤링 결과 팝업창 표시
        private void ShowCrawlingResultPopup(int count, string reason)
        {
            try
            {
                // 🔄 팝업창 표시 전에 로딩창 먼저 숨김 - 소싱 페이지에서 직접 처리
                LoadingHelper.HideLoadingFromSourcingPage();
                
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    var mainWindow = Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
                        ? desktop.MainWindow
                        : null;

                    if (mainWindow != null)
                    {
                        var percentage = (count * 100.0 / 100);
                        
                        var messageBox = new Avalonia.Controls.Window
                        {
                            Title = "크롤링 완료",
                            Width = 450,
                            Height = 280,
                            WindowStartupLocation = Avalonia.Controls.WindowStartupLocation.CenterOwner,
                            CanResize = false,
                            Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#F8F9FA")),
                            Content = new Avalonia.Controls.Border
                            {
                                Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Colors.White),
                                CornerRadius = new Avalonia.CornerRadius(12),
                                Margin = new Avalonia.Thickness(20),
                                Child = new Avalonia.Controls.StackPanel
                                {
                                    Margin = new Avalonia.Thickness(30),
                                    Children =
                                    {
                                        new Avalonia.Controls.TextBlock
                                        {
                                            Text = "크롤링이 완료되었습니다",
                                            FontSize = 24,
                                            FontWeight = Avalonia.Media.FontWeight.Bold,
                                            Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#2C3E50")),
                                            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                                            Margin = new Avalonia.Thickness(0, 0, 0, 20)
                                        },
                                        new Avalonia.Controls.Border
                                        {
                                            Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#E67E22")),
                                            CornerRadius = new Avalonia.CornerRadius(8),
                                            Padding = new Avalonia.Thickness(20, 15),
                                            Margin = new Avalonia.Thickness(0, 0, 0, 25),
                                            Child = new Avalonia.Controls.TextBlock
                                            {
                                                Text = $"수집 완료: {count}/100개 ({percentage:F1}%)",
                                                FontSize = 18,
                                                FontWeight = Avalonia.Media.FontWeight.SemiBold,
                                                Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Colors.White),
                                                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center
                                            }
                                        },
                                        new Avalonia.Controls.Button
                                        {
                                            Content = "확인",
                                            FontSize = 16,
                                            FontWeight = Avalonia.Media.FontWeight.Medium,
                                            Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#3498DB")),
                                            Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Colors.White),
                                            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                                            Padding = new Avalonia.Thickness(40, 12),
                                            CornerRadius = new Avalonia.CornerRadius(6),
                                            BorderThickness = new Avalonia.Thickness(0)
                                        }
                                    }
                                }
                            }
                        };

                        var button = ((Avalonia.Controls.Border)messageBox.Content).Child as Avalonia.Controls.StackPanel;
                        var confirmButton = button?.Children[2] as Avalonia.Controls.Button;
                        if (confirmButton != null)
                        {
                            confirmButton.Click += (s, e) => messageBox.Close();
                        }

                        messageBox.Show();
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
                return _productCount; // 폴백으로 메모리 카운터 사용
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

        // ⭐ 상품 이미지 처리 API
        private async Task<IResult> HandleProductImage(HttpContext context)
        {
            try
            {
                // 🚨 크롤링 중단 상태 체크
                if (!_isCrawlingActive || _shouldStop)
                {
                    LogWindow.AddLogStatic("⏹️ 크롤링 중단됨 - 이미지 처리 스킵");
                    return Results.Ok(new { success = false, message = "크롤링 중단됨" });
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
                // 🚨 크롤링 중단 상태 체크
                if (!_isCrawlingActive || _shouldStop)
                {
                    LogWindow.AddLogStatic("⏹️ 크롤링 중단됨 - 상품명 처리 스킵");
                    return Results.Ok(new { success = false, message = "크롤링 중단됨" });
                }
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

                await context.Response.WriteAsync(JsonSerializer.Serialize(new { success = true }));
                return Results.Ok();
            }
            catch (Exception ex)
            {
                LogWindow.AddLogStatic($"❌ 상품명 처리 오류: {ex.Message}");
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
                
                // 🔥 상품 카운터 증가 및 100개 달성 체크
                _productCount++;
                var percentage = (_productCount * 100.0) / 100;
                
                LogWindow.AddLogStatic($"✅ 상품명 저장 완료: {fileName} - {nameData.ProductName}");
                LogWindow.AddLogStatic($"📊 실시간 진행률: {_productCount}/100개 ({percentage:F1}%)");
                
                // 🚨 100개 달성 시 크롤링 완전 중단
                if (_productCount >= 100)
                {
                    LogWindow.AddLogStatic("🎉 목표 달성! 100개 상품 수집 완료 - 크롤링 중단");
                    _isCrawlingActive = false;
                    
                    // 🔄 로딩창 숨김 - 소싱 페이지에서 직접 처리
                    LoadingHelper.HideLoadingFromSourcingPage();
                    
                    // ⭐ 크롬 탭 닫기
                    _ = Task.Run(() => CloseAllChromeTabs());
                    
                    // ⭐ 팝업창으로 최종 결과 표시
                    ShowCrawlingResultPopup(_productCount, "목표 달성");
                    
                    return;
                }
            }
            catch (Exception ex)
            {
                LogWindow.AddLogStatic($"❌ 상품명 저장 실패: {ex.Message}");
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
                    if (jsonDoc.RootElement.TryGetProperty("productId", out var productIdElement))
                    {
                        LogWindow.AddLogStatic($"🔍 개별 상품 카테고리 감지: productId = {productIdElement.GetString()}");
                        
                        // 개별 상품 카테고리 처리 - 파일로 저장
                        var productId = productIdElement.GetString();
                        var categoryNames = string.Join(", ", categoryData.Categories.Select(c => c.Name));
                        LogWindow.AddLogStatic($"📂 {categoryData.StoreId}: 상품 {productId} 카테고리 수집 성공 - {categoryNames}");
                        
                        // ⭐ 개별 상품 카테고리도 파일로 저장
                        LogWindow.AddLogStatic($"💾 SaveCategories 호출 시작: {categoryData.StoreId}");
                        await SaveCategories(categoryData);
                        LogWindow.AddLogStatic($"✅ {categoryData.StoreId}: {categoryData.Categories.Count}개 카테고리 저장 완료");
                        
                        // 소싱 페이지에 카테고리 데이터 실시간 표시
                        await UpdateSourcingPageCategories(categoryData);
                    }
                    else
                    {
                        LogWindow.AddLogStatic($"🔍 전체 카테고리 감지: productId 없음");
                        
                        // 기존 전체 카테고리 처리
                        await SaveCategories(categoryData);
                        LogWindow.AddLogStatic($"✅ {categoryData.StoreId}: {categoryData.Categories.Count}개 카테고리 저장 완료");
                        
                        // 소싱 페이지에 카테고리 데이터 실시간 표시
                        await UpdateSourcingPageCategories(categoryData);
                    }
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
        private async Task SaveCategories(CategoryData categoryData)
        {
            try
            {
                var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                var predviaPath = Path.Combine(appDataPath, "Predvia");
                var categoriesPath = Path.Combine(predviaPath, "Categories");

                Directory.CreateDirectory(categoriesPath);

                // ⭐ 개별 상품 카테고리인지 확인하여 파일명 결정
                var fileName = categoryData.PageUrl?.Contains("/products/") == true 
                    ? $"{categoryData.StoreId}_{ExtractProductIdFromUrl(categoryData.PageUrl)}_categories.json"
                    : $"{categoryData.StoreId}_categories.json";
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
            }
            catch (Exception ex)
            {
                LogWindow.AddLogStatic($"❌ 리뷰 저장 실패: {ex.Message}");
            }
        }

        // ⭐ 기존 데이터 초기화
        private void ClearPreviousData()
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
                _isCrawlingActive = true;
                _processedStores.Clear();
                
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
                return Results.Json(new { success = true });
            }
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
                
                LogWindow.AddLogStatic($"📝 상품명 {request.ProductNames.Count}개 수신");
                
                // 한글만 추출 및 중복 제거
                var koreanKeywords = ExtractKoreanKeywords(request.ProductNames);
                
                // ⭐ 최신 키워드 저장
                lock (_keywordsLock)
                {
                    _latestKeywords = koreanKeywords;
                }
                
                LogWindow.AddLogStatic($"✅ 한글 키워드 {koreanKeywords.Count}개 추출 완료");
                
                return Results.Json(new { 
                    success = true, 
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
        private async Task<IResult> HandleTriggerKeywords(HttpContext context)
        {
            try
            {
                LogWindow.AddLogStatic("🏷️ 키워드 태그 표시 트리거 수신");
                
                // ⭐ 즉시 키워드 태그 생성 요청
                _ = Task.Run(async () =>
                {
                    await Task.Delay(500); // 0.5초 대기
                    LogWindow.AddLogStatic("🏷️ 키워드 태그 자동 생성 시작");
                    
                    // 키워드가 있는지 확인하고 로그에 알림
                    lock (_keywordsLock)
                    {
                        if (_latestKeywords != null && _latestKeywords.Count > 0)
                        {
                            LogWindow.AddLogStatic($"🏷️ 키워드 {_latestKeywords.Count}개 준비됨 - UI 생성 필요");
                            LogWindow.AddLogStatic("🔔 소싱 페이지에서 키워드를 가져가세요!");
                        }
                        else
                        {
                            LogWindow.AddLogStatic("❌ 준비된 키워드가 없습니다");
                        }
                    }
                });
                
                return Results.Json(new { success = true, message = "키워드 태그 생성 요청 완료" });
            }
            catch (Exception ex)
            {
                LogWindow.AddLogStatic($"❌ 키워드 태그 트리거 오류: {ex.Message}");
                return Results.Json(new { success = false, message = ex.Message });
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
        private async Task<IResult> HandleGetLatestKeywords()
        {
            await Task.CompletedTask;
            lock (_keywordsLock)
            {
                return Results.Json(new { 
                    success = true,
                    keywords = _latestKeywords,
                    filteredCount = _latestKeywords.Count
                });
            }
        }

        // ⭐ 크롤링 플래그 리셋 API
        private async Task<IResult> HandleResetCrawling()
        {
            await Task.CompletedTask;
            lock (_crawlingLock)
            {
                _crawlingAllowed = false;
                return Results.Json(new { success = true });
            }
        }
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
        public string State { get; set; } = string.Empty; // collecting, visiting, done
        
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
        public double Rating { get; set; }
        
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
        
        [JsonPropertyName("pageUrl")]
        public string PageUrl { get; set; } = string.Empty;
        
        [JsonPropertyName("timestamp")]
        public string Timestamp { get; set; } = string.Empty;
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
}
