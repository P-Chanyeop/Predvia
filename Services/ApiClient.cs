using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Gumaedaehang.Services
{
    public class ApiClient
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;

        public ApiClient(string baseUrl = "https://api.predvia.com")
        {
            _baseUrl = baseUrl;
            _httpClient = new HttpClient();
            _httpClient.BaseAddress = new Uri(_baseUrl);
            _httpClient.DefaultRequestHeaders.Accept.Clear();
            _httpClient.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
        }

        // 로그인 메서드
        public async Task<AuthResponse> LoginAsync(string username, string password)
        {
            try
            {
                var loginData = new
                {
                    username = username,
                    password = password
                };

                var content = new StringContent(JsonSerializer.Serialize(loginData), Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync("/api/auth/login", content);

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    var authResponse = JsonSerializer.Deserialize<AuthResponse>(responseContent);
                    return authResponse;
                }
                else
                {
                    // API 오류 처리
                    var errorContent = await response.Content.ReadAsStringAsync();
                    throw new ApiException($"로그인 실패: {response.StatusCode}", errorContent);
                }
            }
            catch (Exception ex) when (!(ex is ApiException))
            {
                // 네트워크 오류 등 처리
                throw new ApiException("API 연결 오류", ex.Message);
            }
        }

        // 회원가입 메서드
        public async Task<AuthResponse> RegisterAsync(string username, string password, string confirmPassword)
        {
            try
            {
                var registerData = new
                {
                    username = username,
                    password = password,
                    confirmPassword = confirmPassword
                };

                var content = new StringContent(JsonSerializer.Serialize(registerData), Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync("/api/auth/register", content);

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    var authResponse = JsonSerializer.Deserialize<AuthResponse>(responseContent);
                    return authResponse;
                }
                else
                {
                    // API 오류 처리
                    var errorContent = await response.Content.ReadAsStringAsync();
                    throw new ApiException($"회원가입 실패: {response.StatusCode}", errorContent);
                }
            }
            catch (Exception ex) when (!(ex is ApiException))
            {
                // 네트워크 오류 등 처리
                throw new ApiException("API 연결 오류", ex.Message);
            }
        }

        // 테스트 모드용 메서드 (실제 API가 없을 때 사용)
        public async Task<AuthResponse> LoginTestModeAsync(string username, string password)
        {
            await Task.Delay(500); // API 호출 시뮬레이션을 위한 지연

            // 테스트 계정 확인
            if ((username == "admin" && password == "admin") || 
                (username == "test" && password == "test"))
            {
                return new AuthResponse
                {
                    Success = true,
                    Token = "test_token_" + Guid.NewGuid().ToString(),
                    Username = username,
                    Message = "로그인 성공"
                };
            }
            else
            {
                throw new ApiException("로그인 실패", "아이디 또는 비밀번호가 올바르지 않습니다.");
            }
        }

        // 테스트 모드용 회원가입 메서드
        public async Task<AuthResponse> RegisterTestModeAsync(string username, string password, string confirmPassword)
        {
            await Task.Delay(500); // API 호출 시뮬레이션을 위한 지연

            // 비밀번호 확인 검증
            if (password != confirmPassword)
            {
                throw new ApiException("회원가입 실패", "비밀번호와 비밀번호 확인이 일치하지 않습니다.");
            }

            // 사용자 이름 중복 검사 (테스트 계정과 중복되면 안됨)
            if (username == "admin" || username == "test")
            {
                throw new ApiException("회원가입 실패", "이미 사용 중인 아이디입니다.");
            }

            return new AuthResponse
            {
                Success = true,
                Token = "test_token_" + Guid.NewGuid().ToString(),
                Username = username,
                Message = "회원가입 성공"
            };
        }
    }

    // API 응답 클래스
    public class AuthResponse
    {
        public bool Success { get; set; }
        public string? Token { get; set; }
        public string? Username { get; set; }
        public string? Message { get; set; }
    }

    // API 예외 클래스
    public class ApiException : Exception
    {
        public string ErrorDetails { get; }

        public ApiException(string message, string errorDetails) : base(message)
        {
            ErrorDetails = errorDetails;
        }
    }
}
