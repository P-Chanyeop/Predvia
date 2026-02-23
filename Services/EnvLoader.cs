using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Gumaedaehang.Services
{
    public static class EnvLoader
    {
        // 앱 고유 키 (난독화용, 완벽한 보안은 아니지만 평문 노출 방지)
        private static readonly byte[] Key = Encoding.UTF8.GetBytes("Predvia2026!Sftc"); // 16 bytes = AES-128
        private static readonly byte[] IV  = Encoding.UTF8.GetBytes("SoftcatPredvia!!");  // 16 bytes

        public static void Load()
        {
            try
            {
                // 1순위: .env.enc (암호화 파일)
                var encPath = FindFile(".env.enc");
                if (encPath != null)
                {
                    var decrypted = Decrypt(File.ReadAllBytes(encPath));
                    ParseAndSet(decrypted);
                    return;
                }

                // 2순위: .env (개발용 평문)
                var envPath = FindFile(".env");
                if (envPath != null)
                {
                    ParseAndSet(File.ReadAllText(envPath));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"EnvLoader error: {ex.Message}");
            }
        }

        // .env.enc 파일 생성 (패키징 시 호출)
        public static void EncryptEnvFile(string envPath, string outputPath)
        {
            var plainText = File.ReadAllText(envPath);
            var encrypted = Encrypt(plainText);
            File.WriteAllBytes(outputPath, encrypted);
        }

        private static void ParseAndSet(string content)
        {
            foreach (var line in content.Split('\n', '\r'))
            {
                var trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#")) continue;
                var idx = trimmed.IndexOf('=');
                if (idx <= 0) continue;
                var key = trimmed[..idx].Trim();
                var val = trimmed[(idx + 1)..].Trim();
                Environment.SetEnvironmentVariable(key, val);
            }
        }

        private static string? FindFile(string fileName)
        {
            // exe와 같은 폴더에서 찾기
            var exeDir = AppDomain.CurrentDomain.BaseDirectory;
            var path = Path.Combine(exeDir, fileName);
            if (File.Exists(path)) return path;

            // 현재 작업 디렉토리
            path = Path.Combine(Directory.GetCurrentDirectory(), fileName);
            if (File.Exists(path)) return path;

            return null;
        }

        private static byte[] Encrypt(string plainText)
        {
            using var aes = Aes.Create();
            aes.Key = Key; aes.IV = IV;
            using var ms = new MemoryStream();
            using (var cs = new CryptoStream(ms, aes.CreateEncryptor(), CryptoStreamMode.Write))
            {
                var bytes = Encoding.UTF8.GetBytes(plainText);
                cs.Write(bytes, 0, bytes.Length);
            }
            return ms.ToArray();
        }

        private static string Decrypt(byte[] cipherBytes)
        {
            using var aes = Aes.Create();
            aes.Key = Key; aes.IV = IV;
            using var ms = new MemoryStream(cipherBytes);
            using var cs = new CryptoStream(ms, aes.CreateDecryptor(), CryptoStreamMode.Read);
            using var reader = new StreamReader(cs, Encoding.UTF8);
            return reader.ReadToEnd();
        }
    }
}
