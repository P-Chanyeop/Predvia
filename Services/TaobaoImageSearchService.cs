using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using PuppeteerSharp;

namespace Gumaedaehang.Services
{
    public class TaobaoImageSearchService
    {
        private readonly string _appKey = "12574478";
        private string? _token;
        private Dictionary<string, string> _cookies = new();

        // ⭐ 프록시 IP 목록
        private static List<string> _proxyList = new();
        private static Random _random = new Random();
        private static readonly object _proxyLock = new object();

        public TaobaoImageSearchService()
        {
            // ⭐ 프록시 목록 로드 (최초 1회)
            LoadProxyList();
        }

        // ⭐ 프록시 목록 파일에서 로드
        private static void LoadProxyList()
        {
            lock (_proxyLock)
            {
                if (_proxyList.Count > 0) return; // 이미 로드됨

                try
                {
                    // AppContext.BaseDirectory 사용 (single-file app 호환)
                    var baseDir = AppContext.BaseDirectory;
                    var proxyFilePath = Path.Combine(
                        baseDir,
                        "..", "..", "..", "..", "image_search_products-master", "프록시유동_모모아이피.txt"
                    );

                    proxyFilePath = Path.GetFullPath(proxyFilePath);

                    if (File.Exists(proxyFilePath))
                    {
                        _proxyList = File.ReadAllLines(proxyFilePath)
                            .Where(line => !string.IsNullOrWhiteSpace(line))
                            .Select(line => line.Trim())
                            .ToList();

                        LogWindow.AddLogStatic($"✅ 프록시 {_proxyList.Count}개 로드 완료 (파일: {Path.GetFileName(proxyFilePath)})");
                    }
                    else
                    {
                        LogWindow.AddLogStatic($"⚠️ 프록시 파일 없음: {proxyFilePath}");
                    }
                }
                catch (Exception ex)
                {
                    LogWindow.AddLogStatic($"❌ 프록시 로드 실패: {ex.Message}");
                }
            }
        }

        // ⭐ 랜덤으로 프록시 선택
        private static string? GetRandomProxy()
        {
            lock (_proxyLock)
            {
                if (_proxyList.Count == 0) return null;

                var index = _random.Next(_proxyList.Count);
                return _proxyList[index];
            }
        }

        // ⭐ 프록시를 사용하는 HttpClient 생성
        private static HttpClient CreateHttpClientWithProxy()
        {
            var proxy = GetRandomProxy();

            if (proxy != null)
            {
                LogWindow.AddLogStatic($"🔄 프록시 사용: {proxy}");

                var handler = new HttpClientHandler
                {
                    Proxy = new WebProxy($"http://{proxy}"),
                    UseProxy = true
                };

                var client = new HttpClient(handler)
                {
                    Timeout = TimeSpan.FromSeconds(30)
                };

                client.DefaultRequestHeaders.Add("User-Agent",
                    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");

                return client;
            }
            else
            {
                LogWindow.AddLogStatic($"⚠️ 프록시 없음 - 직접 연결");

                var client = new HttpClient
                {
                    Timeout = TimeSpan.FromSeconds(30)
                };

                client.DefaultRequestHeaders.Add("User-Agent",
                    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");

                return client;
            }
        }

        // Chrome 확장프로그램에서 쿠키 가져오기
        public async Task<bool> LoadCookiesFromChrome()
        {
            try
            {
                var browserFetcher = new BrowserFetcher();
                await browserFetcher.DownloadAsync();
                
                var browser = await Puppeteer.LaunchAsync(new LaunchOptions
                {
                    Headless = true,
                    UserDataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), 
                        "Google", "Chrome", "User Data")
                });

                var page = await browser.NewPageAsync();
                await page.GoToAsync("https://www.taobao.com");
                
                var cookies = await page.GetCookiesAsync();
                foreach (var cookie in cookies)
                {
                    _cookies[cookie.Name] = cookie.Value;
                    if (cookie.Name == "_m_h5_tk" && !string.IsNullOrEmpty(cookie.Value))
                    {
                        _token = cookie.Value.Split('_')[0];
                    }
                }

                await browser.CloseAsync();
                return !string.IsNullOrEmpty(_token);
            }
            catch (Exception ex)
            {
                LogWindow.AddLogStatic($"❌ 쿠키 로드 실패: {ex.Message}");
                return false;
            }
        }

        // 이미지 업로드
        public async Task<string?> UploadImage(string imagePath)
        {
            if (string.IsNullOrEmpty(_token))
            {
                LogWindow.AddLogStatic("❌ 토큰이 없습니다. 먼저 쿠키를 로드하세요.");
                return null;
            }

            try
            {
                var imageBytes = await File.ReadAllBytesAsync(imagePath);
                var base64Image = Convert.ToBase64String(imageBytes);
                
                var t = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                var data = JsonSerializer.Serialize(new
                {
                    strimg = base64Image.Replace("==", ""),
                    pcGraphSearch = true,
                    sortOrder = 0,
                    tab = "all",
                    vm = "nv"
                });

                var requestData = JsonSerializer.Serialize(new
                {
                    @params = data,
                    appId = "34850"
                });

                var sign = GenerateSign(requestData, t);
                
                var url = "https://h5api.m.taobao.com/h5/mtop.relationrecommend.wirelessrecommend.recommend/2.0/";
                var queryParams = new Dictionary<string, string>
                {
                    ["jsv"] = "2.4.11",
                    ["appKey"] = _appKey,
                    ["t"] = t.ToString(),
                    ["api"] = "mtop.relationrecommend.wirelessrecommend.recommend",
                    ["v"] = "2.0",
                    ["type"] = "originaljson",
                    ["dataType"] = "jsonp",
                    ["sign"] = sign
                };

                var queryString = string.Join("&", queryParams.Select(kvp => $"{kvp.Key}={kvp.Value}"));
                var fullUrl = $"{url}?{queryString}";

                var content = new FormUrlEncodedContent(new[] { new KeyValuePair<string, string>("data", requestData) });

                // ⭐ 매 요청마다 랜덤 프록시로 새 HttpClient 생성
                using var httpClient = CreateHttpClientWithProxy();

                // 쿠키 추가
                var cookieHeader = string.Join("; ", _cookies.Select(kvp => $"{kvp.Key}={kvp.Value}"));
                httpClient.DefaultRequestHeaders.Add("Cookie", cookieHeader);

                var response = await httpClient.PostAsync(fullUrl, content);
                var responseText = await response.Content.ReadAsStringAsync();
                
                LogWindow.AddLogStatic($"✅ 타오바오 이미지 업로드 완료");
                
                // 응답에서 이미지 ID 추출
                var jsonResponse = JsonSerializer.Deserialize<JsonElement>(responseText);
                if (jsonResponse.TryGetProperty("data", out var dataElement) &&
                    dataElement.TryGetProperty("imageId", out var imageIdElement))
                {
                    return imageIdElement.GetString();
                }

                return null;
            }
            catch (Exception ex)
            {
                LogWindow.AddLogStatic($"❌ 이미지 업로드 실패: {ex.Message}");
                return null;
            }
        }

        // 이미지로 상품 검색
        public string GenerateSearchUrl(string imageId)
        {
            return $"https://s.taobao.com/search?imgfile=&commend=all&ssid=s5-e&search_type=item&sourceId=tb.index&spm=a21bo.jianhua.201856-taobao-item.1&ie=utf8&initiative_id=tbindexz_20170306&imageId={imageId}";
        }

        // 서명 생성
        private string GenerateSign(string data, long timestamp)
        {
            var text = $"{_token}&{timestamp}&{_appKey}&{data}";
            using var md5 = MD5.Create();
            var hash = md5.ComputeHash(Encoding.UTF8.GetBytes(text));
            return Convert.ToHexString(hash).ToLower();
        }

        public void Dispose()
        {
            // HttpClient는 각 요청마다 using으로 처리되므로 별도 Dispose 불필요
        }
    }
}
