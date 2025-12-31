using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        private readonly HttpClient _httpClient;
        private readonly string _appKey = "12574478";
        private string? _token;
        private Dictionary<string, string> _cookies = new();

        public TaobaoImageSearchService()
        {
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("User-Agent", 
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
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
                
                // 쿠키 추가
                var cookieHeader = string.Join("; ", _cookies.Select(kvp => $"{kvp.Key}={kvp.Value}"));
                _httpClient.DefaultRequestHeaders.Add("Cookie", cookieHeader);

                var response = await _httpClient.PostAsync(fullUrl, content);
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
            _httpClient?.Dispose();
        }
    }
}
