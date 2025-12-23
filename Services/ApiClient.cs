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
                    return authResponse ?? new AuthResponse { IsSuccess = false, Message = "로그인 응답이 null입니다." };
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
                    return authResponse ?? new AuthResponse { IsSuccess = false, Message = "회원가입 응답이 null입니다." };
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
                    IsSuccess = true,
                    Message = "로그인 성공",
                    Token = "test-token-12345",
                    User = new UserInfo
                    {
                        Id = 1,
                        Username = username,
                        Email = $"{username}@test.com"
                    }
                };
            }
            else
            {
                return new AuthResponse
                {
                    IsSuccess = false,
                    Message = "잘못된 사용자명 또는 비밀번호입니다."
                };
            }
        }

        // 테스트 모드용 회원가입 메서드
        public async Task<AuthResponse> RegisterTestModeAsync(string username, string password, string confirmPassword)
        {
            await Task.Delay(500); // API 호출 시뮬레이션을 위한 지연

            // 비밀번호 확인
            if (password != confirmPassword)
            {
                return new AuthResponse
                {
                    IsSuccess = false,
                    Message = "비밀번호가 일치하지 않습니다."
                };
            }

            // 사용자명 길이 확인
            if (username.Length < 3)
            {
                return new AuthResponse
                {
                    IsSuccess = false,
                    Message = "사용자명은 3자 이상이어야 합니다."
                };
            }

            return new AuthResponse
            {
                IsSuccess = true,
                Message = "회원가입 성공",
                Token = "test-token-67890",
                User = new UserInfo
                {
                    Id = 2,
                    Username = username,
                    Email = $"{username}@test.com"
                }
            };
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }

    // API 응답 모델
    public class AuthResponse
    {
        public bool IsSuccess { get; set; }
        public string Message { get; set; } = string.Empty;
        public string Token { get; set; } = string.Empty;
        public UserInfo? User { get; set; }
    }

    public class UserInfo
    {
        public int Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
    }

    // API 예외 클래스
    public class ApiException : Exception
    {
        public string Details { get; }

        public ApiException(string message, string details) : base(message)
        {
            Details = details;
        }
    }
}
