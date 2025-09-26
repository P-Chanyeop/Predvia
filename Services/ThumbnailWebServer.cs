using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using Gumaedaehang.Services;

namespace Gumaedaehang.Services
{
    public class ThumbnailWebServer
    {
        private WebApplication? _app;
        private readonly ThumbnailService _thumbnailService;
        private bool _isRunning = false;
        
        // ⭐ 상태 관리 시스템
        private readonly Dictionary<string, StoreState> _storeStates = new();
        private readonly object _statesLock = new object();

        public ThumbnailWebServer()
        {
            _thumbnailService = new ThumbnailService();
        }

        public async Task StartAsync()
        {
            if (_isRunning) return;

            try
            {
                var builder = WebApplication.CreateBuilder();
                
                // CORS 서비스 추가
                builder.Services.AddCors();
                
                _app = builder.Build();
                
                // CORS 정책 설정
                _app.UseCors(policy => policy
                    .AllowAnyOrigin()
                    .AllowAnyMethod()
                    .AllowAnyHeader());

                // API 엔드포인트 설정
                _app.MapPost("/api/thumbnails/save", HandleSaveThumbnails);
                _app.MapGet("/api/thumbnails/list", HandleGetThumbnails);
                _app.MapPost("/api/smartstore/links", HandleSmartStoreLinks);
                _app.MapPost("/api/smartstore/visit", HandleSmartStoreVisit);
                _app.MapPost("/api/smartstore/gonggu-check", HandleGongguCheck);
                _app.MapPost("/api/smartstore/all-products", HandleAllProductsPage);
                _app.MapPost("/api/smartstore/product-data", HandleProductData);
                _app.MapPost("/api/smartstore/log", HandleExtensionLog);
                
                // ⭐ 상태 관리 API 추가
                _app.MapPost("/api/smartstore/state", HandleStoreState);
                _app.MapGet("/api/smartstore/state", HandleGetStoreState);
                _app.MapPost("/api/smartstore/progress", HandleStoreProgress);

                _isRunning = true;
                
                // 로그는 MainWindow에서 처리

                // 백그라운드에서 서버 실행
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _app.RunAsync("http://localhost:8080");
                    }
                    catch (Exception ex)
                    {
                        LogWindow.AddLogStatic($"❌ 웹서버 실행 오류: {ex.Message}");
                    }
                });

                // 서버 시작 대기
                await Task.Delay(1000);
            }
            catch (Exception ex)
            {
                LogWindow.AddLogStatic($"❌ 웹서버 시작 오류: {ex.Message}");
                Debug.WriteLine($"웹서버 시작 오류: {ex.Message}");
            }
        }

        // 썸네일 저장 API
        private async Task<IResult> HandleSaveThumbnails(HttpContext context)
        {
            try
            {
                LogWindow.AddLogStatic("📡 API 요청 수신: POST /api/thumbnails/save");

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
                        LogWindow.AddLogStatic($"❌ 썸네일 저장 실패: {product.Title} - {ex.Message}");
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
                LogWindow.AddLogStatic("📡 API 요청 수신: GET /api/thumbnails/list");
                
                var thumbnails = await _thumbnailService.GetThumbnailsAsync();
                return Results.Ok(thumbnails);
            }
            catch (Exception ex)
            {
                LogWindow.AddLogStatic($"❌ API 처리 오류: {ex.Message}");
                return Results.StatusCode(500);
            }
        }

        // 스마트스토어 링크 수집 API
        private async Task<IResult> HandleSmartStoreLinks(HttpContext context)
        {
            try
            {
                LogWindow.AddLogStatic("API 요청 수신: POST /api/smartstore/links");

                using var reader = new StreamReader(context.Request.Body);
                var json = await reader.ReadToEndAsync();
                
                LogWindow.AddLogStatic($"수신된 데이터 크기: {json.Length} bytes");
                LogWindow.AddLogStatic($"JSON 내용: {json.Substring(0, Math.Min(300, json.Length))}");

                SmartStoreLinkRequest? requestData = null;
                try
                {
                    requestData = JsonSerializer.Deserialize<SmartStoreLinkRequest>(json);
                }
                catch (Exception jsonEx)
                {
                    LogWindow.AddLogStatic($"JSON 역직렬화 오류: {jsonEx.Message}");
                    return Results.Json(new { 
                        success = false, 
                        error = $"JSON parsing error: {jsonEx.Message}" 
                    }, statusCode: 400);
                }
                
                if (requestData?.SmartStoreLinks == null)
                {
                    LogWindow.AddLogStatic("잘못된 요청 데이터");
                    return Results.Json(new { 
                        success = false, 
                        error = "Invalid request data" 
                    }, statusCode: 400);
                }

                LogWindow.AddLogStatic($"{requestData.SmartStoreLinks.Count}개 스마트스토어 링크 수신");

                // 스마트스토어 링크들을 로그에 출력
                foreach (var link in requestData.SmartStoreLinks.Take(10)) // 처음 10개만 표시
                {
                    LogWindow.AddLogStatic($"  - {link.Title}: {link.Url}");
                }
                
                if (requestData.SmartStoreLinks.Count > 10)
                {
                    LogWindow.AddLogStatic($"  ... 외 {requestData.SmartStoreLinks.Count - 10}개 더");
                }

                LogWindow.AddLogStatic($"{requestData.SmartStoreLinks.Count}개 스마트스토어 링크 수집 완료");

                var response = new { 
                    success = true,
                    linkCount = requestData.SmartStoreLinks.Count,
                    message = $"{requestData.SmartStoreLinks.Count}개 스마트스토어 링크 수집 완료"
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

        // 스마트스토어 링크 방문 알림 API
        private async Task<IResult> HandleSmartStoreVisit(HttpContext context)
        {
            try
            {
                using var reader = new StreamReader(context.Request.Body);
                var json = await reader.ReadToEndAsync();
                
                var visitData = JsonSerializer.Deserialize<SmartStoreVisitRequest>(json);
                
                if (visitData != null)
                {
                    LogWindow.AddLogStatic($"[{visitData.CurrentIndex}/{visitData.TotalCount}] 스마트스토어 공구탭 접속: {visitData.Title}");
                    
                    if (!string.IsNullOrEmpty(visitData.StoreId))
                    {
                        LogWindow.AddLogStatic($"  스토어 ID: {visitData.StoreId}");
                    }
                    
                    if (!string.IsNullOrEmpty(visitData.GongguUrl))
                    {
                        LogWindow.AddLogStatic($"  공구탭 URL: {visitData.GongguUrl}");
                    }
                    else
                    {
                        LogWindow.AddLogStatic($"  원본 URL: {visitData.Url}");
                    }
                }

                return Results.Json(new { 
                    success = true,
                    message = "방문 상태 수신 완료"
                });
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
                using var reader = new StreamReader(context.Request.Body);
                var json = await reader.ReadToEndAsync();
                
                var gongguData = JsonSerializer.Deserialize<GongguCheckRequest>(json);
                
                if (gongguData != null)
                {
                    if (gongguData.IsValid)
                    {
                        LogWindow.AddLogStatic($"✅ {gongguData.StoreId}: 공구 {gongguData.GongguCount}개 (≥1000개) - 진행");
                    }
                    else
                    {
                        LogWindow.AddLogStatic($"❌ {gongguData.StoreId}: 공구 {gongguData.GongguCount}개 (<1000개) - 스킵");
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
                return Results.Json(new { 
                    success = false, 
                    error = ex.Message 
                }, statusCode: 500);
            }
        }

        // 전체상품 페이지 접속 알림 API
        private async Task<IResult> HandleAllProductsPage(HttpContext context)
        {
            try
            {
                using var reader = new StreamReader(context.Request.Body);
                var json = await reader.ReadToEndAsync();
                
                var pageData = JsonSerializer.Deserialize<AllProductsPageRequest>(json);
                
                if (pageData != null)
                {
                    LogWindow.AddLogStatic($"🛍️ {pageData.StoreId}: 전체상품 페이지 접속 완료");
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
                return Results.Json(new { 
                    success = false, 
                    error = ex.Message 
                }, statusCode: 500);
            }
        }

        // 상품 데이터 수집 결과 API
        private async Task<IResult> HandleProductData(HttpContext context)
        {
            try
            {
                using var reader = new StreamReader(context.Request.Body);
                var json = await reader.ReadToEndAsync();
                
                var productData = JsonSerializer.Deserialize<ProductDataRequest>(json);
                
                if (productData != null)
                {
                    // 리뷰가 있는 상품 개수 확인
                    var reviewProducts = productData.Products.Where(p => !string.IsNullOrEmpty(p.ReviewCount) && p.ReviewCount != "리뷰 없음").ToList();
                    
                    if (reviewProducts.Any())
                    {
                        var lastReviewProduct = reviewProducts.Last();
                        LogWindow.AddLogStatic($"🎯 {productData.StoreId}: 40개 상품 중 {lastReviewProduct.Index}번째에 마지막 리뷰 발견");
                        LogWindow.AddLogStatic($"✅ {productData.StoreId}: 1~{lastReviewProduct.Index}번째 상품 {productData.ProductCount}개 수집 완료");
                    }
                    else
                    {
                        LogWindow.AddLogStatic($"📦 {productData.StoreId}: {productData.ProductCount}개 상품 데이터 수집 완료");
                        LogWindow.AddLogStatic($"  리뷰 상품: {productData.ReviewProductCount}개");
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
                }

                return Results.Json(new { 
                    success = true,
                    message = "상품 데이터 수집 완료"
                });
            }
            catch (Exception ex)
            {
                LogWindow.AddLogStatic($"상품 데이터 처리 오류: {ex.Message}");
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
                
                var logData = JsonSerializer.Deserialize<ExtensionLogRequest>(json);
                
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
                var data = JsonSerializer.Deserialize<JsonElement>(json);
                
                var storeId = data.GetProperty("storeId").GetString();
                var runId = data.GetProperty("runId").GetString();
                var state = data.GetProperty("state").GetString();
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
                
                LogWindow.AddLogStatic($"🔧 {storeId}: 상태 설정 - {state} (lock: {lockValue}, {progress}/{expected})");
                
                return Results.Ok(new { success = true, storeId, runId, state });
            }
            catch (Exception ex)
            {
                LogWindow.AddLogStatic($"❌ 상태 설정 오류: {ex.Message}");
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
                    _storeStates.TryGetValue(key, out storeState);
                }
                
                if (storeState == null)
                {
                    LogWindow.AddLogStatic($"🔍 {storeId}: 상태 없음 (runId: {runId})");
                    return Results.NotFound(new { error = "State not found", storeId, runId });
                }
                
                // ⭐ 타임아웃 체크 (5분 이상 visiting 상태면 강제 완료)
                if (storeState.State == "visiting" && 
                    DateTime.Now - storeState.UpdatedAt > TimeSpan.FromMinutes(5))
                {
                    LogWindow.AddLogStatic($"⏰ {storeId}: 5분 타임아웃 - 강제 완료 처리");
                    
                    lock (_statesLock)
                    {
                        var key = $"{storeId}:{runId}";
                        if (_storeStates.ContainsKey(key))
                        {
                            _storeStates[key].State = "done";
                            _storeStates[key].Lock = false;
                            _storeStates[key].UpdatedAt = DateTime.Now;
                            storeState = _storeStates[key];
                        }
                    }
                }
                
                LogWindow.AddLogStatic($"🔍 {storeId}: 상태 확인 - {storeState.State} (lock: {storeState.Lock}, {storeState.Progress}/{storeState.Expected})");
                
                return Results.Ok(storeState);
            }
            catch (Exception ex)
            {
                LogWindow.AddLogStatic($"❌ 상태 확인 오류: {ex.Message}");
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
                var data = JsonSerializer.Deserialize<JsonElement>(json);
                
                var storeId = data.GetProperty("storeId").GetString();
                var runId = data.GetProperty("runId").GetString();
                var inc = data.TryGetProperty("inc", out var incValue) ? incValue.GetInt32() : 1;
                
                lock (_statesLock)
                {
                    var key = $"{storeId}:{runId}";
                    if (_storeStates.TryGetValue(key, out var state))
                    {
                        state.Progress += inc;
                        state.UpdatedAt = DateTime.Now;
                        LogWindow.AddLogStatic($"📊 {storeId}: 진행률 업데이트 - {state.Progress}/{state.Expected}");
                    }
                }
                
                return Results.Ok(new { success = true });
            }
            catch (Exception ex)
            {
                LogWindow.AddLogStatic($"❌ 진행률 업데이트 오류: {ex.Message}");
                return Results.BadRequest(new { error = ex.Message });
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
        
        [JsonPropertyName("lock")]
        public bool Lock { get; set; } = false;
        
        [JsonPropertyName("expected")]
        public int Expected { get; set; } = 0;
        
        [JsonPropertyName("progress")]
        public int Progress { get; set; } = 0;
        
        [JsonPropertyName("updatedAt")]
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
    }
}
