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
                    LogWindow.AddLogStatic($"[{visitData.CurrentIndex}/{visitData.TotalCount}] 스마트스토어 접속: {visitData.Title}");
                    LogWindow.AddLogStatic($"  URL: {visitData.Url}");
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
        
        [JsonPropertyName("currentIndex")]
        public int CurrentIndex { get; set; }
        
        [JsonPropertyName("totalCount")]
        public int TotalCount { get; set; }
        
        [JsonPropertyName("timestamp")]
        public string Timestamp { get; set; } = string.Empty;
    }
}
