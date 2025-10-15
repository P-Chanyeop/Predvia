using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Linq;

namespace Gumaedaehang.Services
{
    public class ThumbnailService
    {
        private static readonly HttpClient _httpClient = new HttpClient();
        private readonly string _thumbnailDirectory;

        public ThumbnailService()
        {
            // 썸네일 저장 폴더 생성
            _thumbnailDirectory = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Predvia", "Thumbnails");
            Directory.CreateDirectory(_thumbnailDirectory);
        }

        // 썸네일 다운로드 및 로컬 저장
        public async Task<string?> DownloadThumbnailAsync(string imageUrl, string productId)
        {
            try
            {
                if (string.IsNullOrEmpty(imageUrl) || !imageUrl.StartsWith("http"))
                    return null;

                // 파일명 생성 (productId + timestamp)
                var fileName = $"{productId}_{DateTime.Now:yyyyMMdd_HHmmss}.jpg";
                var filePath = System.IO.Path.Combine(_thumbnailDirectory, fileName);

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

        // 썸네일 저장 (웹서버용)
        public async Task SaveThumbnailAsync(string id, string title, string thumbnailUrl, string price, string link)
        {
            try
            {
                var filePath = await DownloadThumbnailAsync(thumbnailUrl, id);
                if (!string.IsNullOrEmpty(filePath))
                {
                    // 메타데이터 저장
                    var metadata = new ProductData
                    {
                        Id = id,
                        Title = title,
                        ThumbnailUrl = thumbnailUrl,
                        Price = price,
                        Link = link,
                        LocalPath = filePath,
                        SavedAt = DateTime.Now
                    };
                    
                    await SaveMetadataAsync(metadata);
                    LogWindow.AddLogStatic($"✅ 썸네일 저장: {title}");
                }
            }
            catch (Exception ex)
            {
                LogWindow.AddLogStatic($"❌ 썸네일 저장 실패: {title} - {ex.Message}");
            }
        }

        // 썸네일 목록 조회 (웹서버용)
        public async Task<List<ProductData>> GetThumbnailsAsync()
        {
            try
            {
                return await LoadMetadataAsync();
            }
            catch (Exception ex)
            {
                LogWindow.AddLogStatic($"❌ 썸네일 목록 조회 실패: {ex.Message}");
                return new List<ProductData>();
            }
        }

        // 메타데이터 저장
        private async Task SaveMetadataAsync(ProductData product)
        {
            try
            {
                var metadataFile = System.IO.Path.Combine(_thumbnailDirectory, "thumbnails.json");
                var products = await LoadMetadataAsync();
                
                // 기존 데이터 업데이트 또는 새로 추가
                var existing = products.FirstOrDefault(p => p.Id == product.Id);
                if (existing != null)
                {
                    products.Remove(existing);
                }
                products.Add(product);
                
                var json = JsonSerializer.Serialize(products, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(metadataFile, json);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"메타데이터 저장 오류: {ex.Message}");
            }
        }

        // 메타데이터 로드
        private async Task<List<ProductData>> LoadMetadataAsync()
        {
            try
            {
                var metadataFile = System.IO.Path.Combine(_thumbnailDirectory, "thumbnails.json");
                if (!File.Exists(metadataFile))
                    return new List<ProductData>();
                
                var json = await File.ReadAllTextAsync(metadataFile);
                return JsonSerializer.Deserialize<List<ProductData>>(json) ?? new List<ProductData>();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"메타데이터 로드 오류: {ex.Message}");
                return new List<ProductData>();
            }
        }

        // 썸네일 정보 로드 (ThumbnailInfo 형태로)
        public async Task<List<ThumbnailInfo>> LoadThumbnailInfoAsync()
        {
            try
            {
                var products = await LoadMetadataAsync();
                return products.Select(p => new ThumbnailInfo
                {
                    ProductId = p.Id,
                    ProductTitle = p.Title,
                    OriginalUrl = p.ThumbnailUrl,
                    LocalPath = p.LocalPath,
                    DownloadedAt = p.SavedAt
                }).ToList();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"썸네일 정보 로드 오류: {ex.Message}");
                return new List<ThumbnailInfo>();
            }
        }

        // 여러 썸네일 다운로드
        public async Task<int> DownloadThumbnailsAsync(List<ProductData> products)
        {
            int successCount = 0;
            foreach (var product in products)
            {
                try
                {
                    await SaveThumbnailAsync(product.Id, product.Title, product.ThumbnailUrl, product.Price, product.Link);
                    successCount++;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"썸네일 다운로드 실패: {product.Title} - {ex.Message}");
                }
            }
            return successCount;
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
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;
        
        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;
        
        [JsonPropertyName("thumbnailUrl")]
        public string ThumbnailUrl { get; set; } = string.Empty;
        
        [JsonPropertyName("price")]
        public string Price { get; set; } = string.Empty;
        
        [JsonPropertyName("link")]
        public string Link { get; set; } = string.Empty;
        
        public string LocalPath { get; set; } = string.Empty;
        public DateTime SavedAt { get; set; }
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
