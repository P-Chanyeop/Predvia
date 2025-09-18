using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Text.Json;

namespace Gumaedaehang.Services
{
    public class ThumbnailService
    {
        private static readonly HttpClient _httpClient = new HttpClient();
        private readonly string _thumbnailDirectory;

        public ThumbnailService()
        {
            // 썸네일 저장 폴더 생성
            _thumbnailDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Predvia", "Thumbnails");
            Directory.CreateDirectory(_thumbnailDirectory);
        }

        // 썸네일 다운로드 및 로컬 저장
        public async Task<string> DownloadThumbnailAsync(string imageUrl, string productId)
        {
            try
            {
                if (string.IsNullOrEmpty(imageUrl) || !imageUrl.StartsWith("http"))
                    return null;

                // 파일명 생성 (productId + timestamp)
                var fileName = $"{productId}_{DateTime.Now:yyyyMMdd_HHmmss}.jpg";
                var filePath = Path.Combine(_thumbnailDirectory, fileName);

                // 이미 존재하면 기존 파일 반환
                if (File.Exists(filePath))
                    return filePath;

                // 이미지 다운로드
                var response = await _httpClient.GetAsync(imageUrl);
                if (response.IsSuccessStatusCode)
                {
                    var imageBytes = await response.Content.ReadAsByteArrayAsync();
                    await File.WriteAllBytesAsync(filePath, imageBytes);
                    
                    Debug.WriteLine($"썸네일 저장 완료: {fileName}");
                    return filePath;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"썸네일 다운로드 오류: {ex.Message}");
            }

            return null;
        }

        // 여러 썸네일 일괄 다운로드
        public async Task<List<ThumbnailInfo>> DownloadThumbnailsAsync(List<ProductData> products)
        {
            var thumbnails = new List<ThumbnailInfo>();

            foreach (var product in products)
            {
                var localPath = await DownloadThumbnailAsync(product.ThumbnailUrl, product.Id);
                if (!string.IsNullOrEmpty(localPath))
                {
                    thumbnails.Add(new ThumbnailInfo
                    {
                        ProductId = product.Id,
                        ProductTitle = product.Title,
                        OriginalUrl = product.ThumbnailUrl,
                        LocalPath = localPath,
                        DownloadedAt = DateTime.Now
                    });
                }
            }

            // 썸네일 정보를 JSON으로 저장
            await SaveThumbnailInfoAsync(thumbnails);
            return thumbnails;
        }

        // 썸네일 정보 저장
        private async Task SaveThumbnailInfoAsync(List<ThumbnailInfo> thumbnails)
        {
            try
            {
                var jsonPath = Path.Combine(_thumbnailDirectory, "thumbnails.json");
                var json = JsonSerializer.Serialize(thumbnails, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(jsonPath, json);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"썸네일 정보 저장 오류: {ex.Message}");
            }
        }

        // 저장된 썸네일 정보 로드
        public async Task<List<ThumbnailInfo>> LoadThumbnailInfoAsync()
        {
            try
            {
                var jsonPath = Path.Combine(_thumbnailDirectory, "thumbnails.json");
                if (File.Exists(jsonPath))
                {
                    var json = await File.ReadAllTextAsync(jsonPath);
                    return JsonSerializer.Deserialize<List<ThumbnailInfo>>(json) ?? new List<ThumbnailInfo>();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"썸네일 정보 로드 오류: {ex.Message}");
            }

            return new List<ThumbnailInfo>();
        }

        // 특정 상품의 썸네일 경로 가져오기
        public async Task<string> GetThumbnailPathAsync(string productId)
        {
            var thumbnails = await LoadThumbnailInfoAsync();
            var thumbnail = thumbnails.Find(t => t.ProductId == productId);
            
            if (thumbnail != null && File.Exists(thumbnail.LocalPath))
                return thumbnail.LocalPath;

            return null;
        }

        // 썸네일 폴더 열기
        public void OpenThumbnailFolder()
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = _thumbnailDirectory,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"폴더 열기 오류: {ex.Message}");
            }
        }
    }

    // 상품 데이터 클래스
    public class ProductData
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string ThumbnailUrl { get; set; } = string.Empty;
        public string Price { get; set; } = string.Empty;
        public string Link { get; set; } = string.Empty;
    }

    // 썸네일 정보 클래스
    public class ThumbnailInfo
    {
        public string ProductId { get; set; } = string.Empty;
        public string ProductTitle { get; set; } = string.Empty;
        public string OriginalUrl { get; set; } = string.Empty;
        public string LocalPath { get; set; } = string.Empty;
        public DateTime DownloadedAt { get; set; }
    }
}
