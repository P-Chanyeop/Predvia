using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Diagnostics;

namespace Gumaedaehang.Services
{
    public class ThumbnailApiService
    {
        private HttpListener? _listener;
        private readonly ThumbnailService _thumbnailService;
        private bool _isRunning = false;

        public ThumbnailApiService()
        {
            _thumbnailService = new ThumbnailService();
        }

        // API 서버 시작
        public async Task StartAsync()
        {
            if (_isRunning) return;

            try
            {
                _listener = new HttpListener();
                _listener.Prefixes.Add("http://127.0.0.1:8888/");
                _listener.Start();
                _isRunning = true;

                Debug.WriteLine("🚀 썸네일 API 서버 시작됨: http://localhost:8080");
                LogWindow.AddLogStatic("🚀 썸네일 API 서버 시작됨: http://localhost:8080");
                LogWindow.AddLogStatic("⏳ 요청 대기 중...");

                // 요청 처리 루프
                _ = Task.Run(async () =>
                {
                    while (_isRunning && _listener.IsListening)
                    {
                        try
                        {
                            LogWindow.AddLogStatic("🔄 GetContextAsync 대기 중...");
                            var context = await _listener.GetContextAsync();
                            LogWindow.AddLogStatic($"📨 요청 수신됨: {context.Request.HttpMethod} {context.Request.Url}");
                            _ = Task.Run(() => HandleRequestAsync(context));
                        }
                        catch (Exception ex)
                        {
                            if (_isRunning)
                            {
                                Debug.WriteLine($"API 서버 오류: {ex.Message}");
                                LogWindow.AddLogStatic($"❌ API 서버 오류: {ex.Message}");
                            }
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"API 서버 시작 오류: {ex.Message}");
            }
        }

        // 요청 처리
        private async Task HandleRequestAsync(HttpListenerContext context)
        {
            var request = context.Request;
            var response = context.Response;

            try
            {
                // CORS 헤더 추가
                response.Headers.Add("Access-Control-Allow-Origin", "*");
                response.Headers.Add("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
                response.Headers.Add("Access-Control-Allow-Headers", "Content-Type, Origin");

                if (request.HttpMethod == "OPTIONS")
                {
                    response.StatusCode = 200;
                    response.Close();
                    return;
                }

                var url = request.Url.AbsolutePath;
                Debug.WriteLine($"📡 API 요청: {request.HttpMethod} {url}");
                
                // 로그에도 요청 기록
                LogWindow.AddLogStatic($"📡 API 요청 수신: {request.HttpMethod} {url}");

                if (url == "/api/thumbnails/save" && request.HttpMethod == "POST")
                {
                    await HandleSaveThumbnailsAsync(request, response);
                }
                else if (url == "/api/thumbnails/list" && request.HttpMethod == "GET")
                {
                    await HandleGetThumbnailsAsync(response);
                }
                else
                {
                    response.StatusCode = 404;
                    LogWindow.AddLogStatic($"❌ 알 수 없는 API 경로: {url}");
                    await WriteResponseAsync(response, "Not Found");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"요청 처리 오류: {ex.Message}");
                LogWindow.AddLogStatic($"❌ API 요청 처리 오류: {ex.Message}");
                response.StatusCode = 500;
                await WriteResponseAsync(response, "Internal Server Error");
            }
        }

        // 썸네일 저장 처리
        private async Task HandleSaveThumbnailsAsync(HttpListenerRequest request, HttpListenerResponse response)
        {
            try
            {
                using var reader = new StreamReader(request.InputStream, request.ContentEncoding);
                var json = await reader.ReadToEndAsync();
                
                LogWindow.AddLogStatic($"수신된 JSON 데이터: {json.Substring(0, Math.Min(200, json.Length))}...");
                
                var requestData = JsonSerializer.Deserialize<ThumbnailSaveRequest>(json);
                if (requestData?.Products == null)
                {
                    LogWindow.AddLogStatic("잘못된 요청 데이터");
                    response.StatusCode = 400;
                    await WriteResponseAsync(response, "Invalid request data");
                    return;
                }

                LogWindow.AddLogStatic($"{requestData.Products.Count}개 상품 썸네일 저장 요청");

                // 썸네일 다운로드 및 저장
                var savedCount = await _thumbnailService.DownloadThumbnailsAsync(requestData.Products);

                // 로그 창에 결과 표시
                try
                {
                    // LogWindow에 정적 메서드로 로그 추가
                    await Task.Run(() =>
                    {
                        LogWindow.AddLogStatic($"Chrome 확장프로그램에서 {requestData.Products.Count}개 상품 데이터 수신");
                        LogWindow.AddLogStatic($"{savedCount}개 썸네일 이미지 다운로드 및 저장 완료");
                        LogWindow.AddLogStatic($"저장 위치: %APPDATA%\\Predvia\\Thumbnails\\");
                        
                        // 처음 3개 상품만 표시
                        for (int i = 0; i < Math.Min(3, requestData.Products.Count); i++)
                        {
                            var product = requestData.Products[i];
                            var title = product.Title.Length > 30 ? product.Title.Substring(0, 30) + "..." : product.Title;
                            LogWindow.AddLogStatic($"   {title}");
                        }
                        
                        if (requestData.Products.Count > 3)
                        {
                            LogWindow.AddLogStatic($"   ... 외 {requestData.Products.Count - 3}개 더");
                        }
                    });
                }
                catch (Exception logEx)
                {
                    Debug.WriteLine($"로그 추가 오류: {logEx.Message}");
                }

                var result = new
                {
                    success = true,
                    savedCount = savedCount,
                    message = $"{savedCount}개 썸네일 저장 완료"
                };

                response.StatusCode = 200;
                await WriteResponseAsync(response, JsonSerializer.Serialize(result));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"썸네일 저장 오류: {ex.Message}");
                
                // 오류도 로그에 표시
                try
                {
                    await Task.Run(() =>
                    {
                        LogWindow.AddLogStatic($"❌ 썸네일 저장 오류: {ex.Message}");
                    });
                }
                catch { }
                
                response.StatusCode = 500;
                await WriteResponseAsync(response, $"Error: {ex.Message}");
            }
        }

        // 썸네일 목록 조회
        private async Task HandleGetThumbnailsAsync(HttpListenerResponse response)
        {
            try
            {
                var thumbnails = await _thumbnailService.LoadThumbnailInfoAsync();
                
                var result = new
                {
                    success = true,
                    count = thumbnails.Count,
                    thumbnails = thumbnails
                };

                response.StatusCode = 200;
                await WriteResponseAsync(response, JsonSerializer.Serialize(result));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"썸네일 조회 오류: {ex.Message}");
                response.StatusCode = 500;
                await WriteResponseAsync(response, $"Error: {ex.Message}");
            }
        }

        // 응답 작성
        private async Task WriteResponseAsync(HttpListenerResponse response, string content)
        {
            response.ContentType = "application/json; charset=utf-8";
            var buffer = Encoding.UTF8.GetBytes(content);
            response.ContentLength64 = buffer.Length;
            await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
            response.Close();
        }

        // API 서버 중지
        public void Stop()
        {
            if (!_isRunning) return;

            try
            {
                _isRunning = false;
                _listener?.Stop();
                _listener?.Close();
                Debug.WriteLine("🛑 썸네일 API 서버 중지됨");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"API 서버 중지 오류: {ex.Message}");
            }
        }
    }

    // 썸네일 저장 요청 클래스
    public class ThumbnailSaveRequest
    {
        [JsonPropertyName("products")]
        public List<ProductData> Products { get; set; } = new();
        
        [JsonPropertyName("source")]
        public string Source { get; set; } = string.Empty;
        
        [JsonPropertyName("timestamp")]
        public string Timestamp { get; set; } = string.Empty;
    }
}
