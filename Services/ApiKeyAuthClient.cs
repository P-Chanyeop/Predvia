using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Gumaedaehang.Services
{
    public class ApiKeyAuthClient
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;

        public ApiKeyAuthClient(string baseUrl = "https://api.predvia.com")
        {
            _baseUrl = baseUrl;
            _httpClient = new HttpClient();
            _httpClient.BaseAddress = new Uri(_baseUrl);
            _httpClient.DefaultRequestHeaders.Accept.Clear();
            _httpClient.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
        }

        // API 키 인증 메서드 (테스트 모드 우선)
        public async Task<ApiKeyAuthResponse> AuthenticateWithApiKeyAsync(string apiKey)
        {
            try
            {
                // 먼저 테스트 모드로 시도
                return await AuthenticateWithApiKeyTestModeAsync(apiKey);
            }
            catch (ApiException)
            {
                // 테스트 모드 실패 시 실제 API 호출
                try
                {
                    var authData = new
                    {
                        apiKey = apiKey
                    };

                    var content = new StringContent(JsonSerializer.Serialize(authData), Encoding.UTF8, "application/json");
                    var response = await _httpClient.PostAsync("/api/auth/verify-key", content);

                    if (response.IsSuccessStatusCode)
                    {
                        var responseContent = await response.Content.ReadAsStringAsync();
                        var authResponse = JsonSerializer.Deserialize<ApiKeyAuthResponse>(responseContent);
                        return authResponse;
                    }
                    else
                    {
                        // API 오류 처리
                        var errorContent = await response.Content.ReadAsStringAsync();
                        throw new ApiException($"API 키 인증 실패: {response.StatusCode}", errorContent);
                    }
                }
                catch (Exception ex) when (!(ex is ApiException))
                {
                    // 네트워크 오류 등 처리
                    throw new ApiException("API 연결 오류", ex.Message);
                }
            }
        }

        // 테스트 모드용 메서드 (실제 API가 없을 때 사용)
        public async Task<ApiKeyAuthResponse> AuthenticateWithApiKeyTestModeAsync(string apiKey)
        {
            await Task.Delay(500); // API 호출 시뮬레이션을 위한 지연

            // 테스트 API 키 확인
            if (apiKey == "PREDVIA-API-KEY-12345" || apiKey == "TEST-API-KEY-67890")
            {
                return new ApiKeyAuthResponse
                {
                    Success = true,
                    LicenseType = apiKey.StartsWith("PREDVIA") ? "PREMIUM" : "STANDARD",
                    ExpiryDate = DateTime.Now.AddYears(1),
                    CompanyName = apiKey.StartsWith("PREDVIA") ? "PREDVIA Inc." : "Test Company",
                    Message = "API 키 인증 성공"
                };
            }
            else
            {
                throw new ApiException("API 키 인증 실패", "유효하지 않은 API 키입니다.");
            }
        }
    }

    // API 키 인증 응답 클래스
    public class ApiKeyAuthResponse
    {
        public bool Success { get; set; }
        public string LicenseType { get; set; }
        public DateTime ExpiryDate { get; set; }
        public string CompanyName { get; set; }
        public string Message { get; set; }
    }
}
