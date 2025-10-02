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
        
        // ⭐ 상품 카운터 및 랜덤 선택 관련 변수
        private int _totalProductCount = 0;
        private const int TARGET_PRODUCT_COUNT = 100;
        private const int MAX_STORES_TO_VISIT = 10;
        private List<SmartStoreLink> _selectedStores = new();
        private bool _shouldStop = false;
        private readonly object _counterLock = new object();

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
                _app.MapGet("/api/smartstore/status", HandleGetStatus);

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
                        LogWindow.AddLogStatic($"웹서버 실행 오류: {ex.Message}");
                    }
                });

                // 서버 시작 대기
                await Task.Delay(1000);
            }
            catch (Exception ex)
            {
                LogWindow.AddLogStatic($"웹서버 시작 오류: {ex.Message}");
                Debug.WriteLine($"웹서버 시작 오류: {ex.Message}");
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

                // ⭐ 랜덤으로 10개 선택
                var random = new Random();
                _selectedStores = requestData.SmartStoreLinks
                    .OrderBy(x => random.Next())
                    .Take(MAX_STORES_TO_VISIT)
                    .ToList();
                
                // 상품 카운터 초기화
                lock (_counterLock)
                {
                    _totalProductCount = 0;
                    _shouldStop = false;
                    LogWindow.AddLogStatic($"🔄 상품 카운터 초기화: 0/100개");
                }

                LogWindow.AddLogStatic($"랜덤으로 선택된 {_selectedStores.Count}개 스토어:");
                foreach (var store in _selectedStores)
                {
                    LogWindow.AddLogStatic($"  - {store.Title}: {store.Url}");
                }

                LogWindow.AddLogStatic($"목표: {TARGET_PRODUCT_COUNT}개 상품 수집");

                var response = new { 
                    success = true,
                    totalLinks = requestData.SmartStoreLinks.Count,
                    selectedLinks = _selectedStores.Count,
                    targetProducts = TARGET_PRODUCT_COUNT,
                    selectedStores = _selectedStores.Select(s => new {
                        title = s.Title,
                        url = s.Url,
                        storeId = s.Url.Split('/').LastOrDefault()?.Split('?').FirstOrDefault()?.Replace("inflow/outlink/url?url=https%3A%2F%2Fsmartstore.naver.com%2F", "") ?? ""
                    }).ToList(),
                    message = $"{requestData.SmartStoreLinks.Count}개 중 {_selectedStores.Count}개 스토어 선택 완료"
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

                if (visitData == null)
                {
                    return Results.BadRequest(new { error = "Invalid visit data" });
                }

                // ⭐ 선택된 스토어인지 확인
                var storeIdFromUrl = visitData.Url.Split('/').LastOrDefault()?.Split('?').FirstOrDefault() ?? "";
                var isSelectedStore = _selectedStores.Any(s => 
                    s.Url.Contains(storeIdFromUrl) || 
                    visitData.StoreId.Equals(s.Url.Split('/').LastOrDefault()?.Split('?').FirstOrDefault(), StringComparison.OrdinalIgnoreCase)
                );
                
                LogWindow.AddLogStatic($"스토어 선택 확인: {visitData.StoreId} -> {(isSelectedStore ? "선택됨" : "선택안됨")}");
                
                if (!isSelectedStore)
                {
                    LogWindow.AddLogStatic($"선택되지 않은 스토어 건너뛰기: {visitData.StoreId}");
                    return Results.Ok(new { 
                        success = true, 
                        skip = true,
                        message = "Store not selected for crawling" 
                    });
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

                return Results.Ok(new { 
                    success = true,
                    currentProducts = _totalProductCount,
                    targetProducts = TARGET_PRODUCT_COUNT,
                    message = "Visit logged successfully" 
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
                        LogWindow.AddLogStatic($"{gongguData.StoreId}: 공구 {gongguData.GongguCount}개 (≥1000개) - 진행");
                    }
                    else
                    {
                        LogWindow.AddLogStatic($"{gongguData.StoreId}: 공구 {gongguData.GongguCount}개 (<1000개) - 스킵");
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
                    // ⭐ 선택된 스토어인지 엄격하게 확인
                    var selectedStoreIds = _selectedStores.Select(s => {
                        var url = s.Url;
                        if (url.Contains("inflow/outlink/url?url="))
                        {
                            var decoded = Uri.UnescapeDataString(url);
                            var match = System.Text.RegularExpressions.Regex.Match(decoded, @"smartstore\.naver\.com/([^/&?]+)");
                            return match.Success ? match.Groups[1].Value : "";
                        }
                        return "";
                    }).Where(id => !string.IsNullOrEmpty(id)).ToList();
                    
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
                            LogWindow.AddLogStatic($"🎉 목표 달성! 정확히 100개 상품 수집 완료 - 크롤링 중단");
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
                    LogWindow.AddLogStatic($"상태 조회 시도: {key}");
                    LogWindow.AddLogStatic($"저장된 키들: {string.Join(", ", _storeStates.Keys)}");
                    
                    if (!_storeStates.TryGetValue(key, out storeState))
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
        private IResult HandleGetStatus()
        {
            try
            {
                lock (_counterLock)
                {
                    return Results.Ok(new
                    {
                        totalProducts = _totalProductCount,
                        targetProducts = TARGET_PRODUCT_COUNT,
                        shouldStop = _shouldStop,
                        selectedStores = _selectedStores.Count,
                        progress = _totalProductCount * 100.0 / TARGET_PRODUCT_COUNT
                    });
                }
            }
            catch (Exception ex)
            {
                LogWindow.AddLogStatic($"상태 확인 오류: {ex.Message}");
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
}
