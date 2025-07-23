using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace Gumaedaehang.Services
{
    public class Advice
    {
        public string Author { get; set; } = string.Empty;
        public string AuthorProfile { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }
    
    public class AdviceService
    {
        private readonly HttpClient _httpClient;
        private const string ApiUrl = "https://korean-advice-open-api.vercel.app/api/advice";
        
        public AdviceService()
        {
            _httpClient = new HttpClient();
        }
        
        public async Task<Advice> GetRandomAdviceAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync(ApiUrl);
                response.EnsureSuccessStatusCode();
                
                var content = await response.Content.ReadAsStringAsync();
                var advice = JsonSerializer.Deserialize<Advice>(content, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                
                return advice ?? new Advice { Message = "명언을 불러오는 중 오류가 발생했습니다." };
            }
            catch (Exception ex)
            {
                return new Advice
                {
                    Author = "시스템",
                    AuthorProfile = "오류",
                    Message = $"명언을 불러오는 중 오류가 발생했습니다: {ex.Message}"
                };
            }
        }
    }
}
