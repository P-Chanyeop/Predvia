using System;
using System.IO;
using System.Threading.Tasks;
using Amazon;
using Amazon.S3;
using Amazon.S3.Transfer;

namespace Gumaedaehang.Services
{
    public class S3Service
    {
        private static S3Service? _instance;
        public static S3Service Instance => _instance ??= new S3Service();

        private readonly AmazonS3Client _client;
        private const string BucketName = "softcat-bucket";
        private const string Prefix = "predvia";

        private S3Service()
        {
            var accessKey = Environment.GetEnvironmentVariable("AWS_ACCESS_KEY") ?? "";
            var secretKey = Environment.GetEnvironmentVariable("AWS_SECRET_KEY") ?? "";
            _client = new AmazonS3Client(accessKey, secretKey, RegionEndpoint.APNortheast1);
        }

        // 이미지 바이트 → S3 업로드 → URL 반환
        public async Task<string?> UploadImageAsync(string apiKey, string storeId, string productId, byte[] imageBytes, string extension = ".jpg")
        {
            try
            {
                var key = $"{Prefix}/{apiKey}/{storeId}_{productId}{extension}";

                using var ms = new MemoryStream(imageBytes);
                var transfer = new TransferUtility(_client);
                await transfer.UploadAsync(ms, BucketName, key);

                var url = $"https://{BucketName}.s3.ap-northeast-1.amazonaws.com/{key}";
                LogWindow.AddLogStatic($"✅ S3 업로드 완료: {key}");
                return url;
            }
            catch (Exception ex)
            {
                LogWindow.AddLogStatic($"❌ S3 업로드 실패: {ex.Message}");
                return null;
            }
        }
    }
}
