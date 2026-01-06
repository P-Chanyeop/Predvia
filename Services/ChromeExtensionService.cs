using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace Gumaedaehang.Services
{
    public class ChromeExtensionService
    {
        private readonly string _extensionPath;
        
        public ChromeExtensionService()
        {
            _extensionPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "chrome-extension");
        }
        
        public Task<bool> SearchWithExtension(string keyword)
        {
            try
            {
                // 네이버 쇼핑 URL 직접 생성
                var encodedKeyword = Uri.EscapeDataString(keyword);
                var naverUrl = $"https://search.shopping.naver.com/search/all?adQuery={encodedKeyword}&origQuery={encodedKeyword}&pagingIndex=1&pagingSize=40&productSet=overseas&query={encodedKeyword}&sort=rel&timestamp=&viewType=list";
                
                // Chrome을 확장프로그램과 함께 실행하면서 바로 네이버 페이지로 이동 (앱 모드 일반 크기, JavaScript로 우하단 이동)
                var chromeArgs = $"--load-extension=\"{_extensionPath}\" --app=\"{naverUrl}\" --window-size=800,600 --window-position=100,100 --no-first-run --no-default-browser-check --disable-web-security";
                
                var processInfo = new ProcessStartInfo
                {
                    FileName = GetChromePath(),
                    Arguments = chromeArgs,
                    UseShellExecute = false,
                    CreateNoWindow = false
                };
                
                var process = Process.Start(processInfo);
                
                if (process != null)
                {
                    Debug.WriteLine($"확장프로그램으로 네이버 쇼핑 검색 실행: {keyword}");
                    
                    // 10초 후 강제 종료
                    _ = Task.Run(async () =>
                    {
                        await Task.Delay(10000);
                        try
                        {
                            if (!process.HasExited)
                            {
                                process.CloseMainWindow(); // 먼저 정상 종료 시도
                                await Task.Delay(1000);
                                
                                if (!process.HasExited)
                                {
                                    process.Kill(); // 강제 종료
                                    process.WaitForExit(2000);
                                }
                                Debug.WriteLine("10초 후 Chrome 프로세스 종료 완료");
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"프로세스 종료 중 오류: {ex.Message}");
                        }
                        finally
                        {
                            process?.Dispose();
                        }
                    });
                    
                    return Task.FromResult(true);
                }
                
                return Task.FromResult(false);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"확장프로그램 실행 실패: {ex.Message}");
                return Task.FromResult(false);
            }
        }
        
        public Task<bool> OpenNaverPriceComparison(string searchUrl)
        {
            try
            {
                // Chrome을 확장프로그램과 함께 실행하면서 네이버 가격비교 페이지로 이동 (앱 모드 일반 크기, JavaScript로 우하단 이동)
                var chromeArgs = $"--load-extension=\"{_extensionPath}\" --app=\"{searchUrl}\" --window-size=800,600 --window-position=100,100 --no-first-run --no-default-browser-check --disable-web-security --user-agent=\"Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36\"";

                var processInfo = new ProcessStartInfo
                {
                    FileName = GetChromePath(),
                    Arguments = chromeArgs,
                    UseShellExecute = false,
                    CreateNoWindow = false
                };

                var process = Process.Start(processInfo);

                if (process != null)
                {
                    Debug.WriteLine($"네이버 가격비교 페이지 열기: {searchUrl}");

                    // ⭐ 15초 후 자동 종료 (링크 수집 시간 충분히 확보)
                    _ = Task.Run(async () =>
                    {
                        await Task.Delay(15000); // 15초 대기
                        try
                        {
                            if (!process.HasExited)
                            {
                                Debug.WriteLine("15초 경과 - 네이버 가격비교 Chrome 강제 종료 시작");

                                // ⭐ 모든 하위 Chrome 프로세스 강제 종료
                                try
                                {
                                    process.Kill(entireProcessTree: true);
                                    process.WaitForExit(3000);
                                    Debug.WriteLine("✅ 네이버 가격비교 Chrome 프로세스 트리 전체 종료 완료");
                                }
                                catch
                                {
                                    // Kill이 실패하면 개별적으로 시도
                                    process.CloseMainWindow();
                                    await Task.Delay(1000);

                                    if (!process.HasExited)
                                    {
                                        process.Kill();
                                        process.WaitForExit(2000);
                                    }
                                    Debug.WriteLine("✅ 네이버 가격비교 Chrome 프로세스 종료 완료");
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"❌ 가격비교 브라우저 종료 중 오류: {ex.Message}");
                        }
                        finally
                        {
                            process?.Dispose();
                        }
                    });

                    return Task.FromResult(true);
                }

                return Task.FromResult(false);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"네이버 가격비교 페이지 열기 실패: {ex.Message}");
                return Task.FromResult(false);
            }
        }
        
        public Task<bool> OpenUrlInNewTab(string url)
        {
            try
            {
                // Chrome 새 창에서 URL 열기 (확장프로그램 로드) - 앱 모드 우하단 최소 창, 처음부터 우하단 배치
                // 1920x1080 기준 우하단 위치: 1920-200-20=1700, 1080-300-50=730
                var chromeArgs = $"--load-extension=\"{_extensionPath}\" --app=\"{url}\" --window-size=200,300 --window-position=1700,730 --no-first-run --no-default-browser-check --disable-web-security";
                
                var processInfo = new ProcessStartInfo
                {
                    FileName = GetChromePath(),
                    Arguments = chromeArgs,
                    UseShellExecute = false,
                    CreateNoWindow = false
                };
                
                var process = Process.Start(processInfo);
                
                if (process != null)
                {
                    Debug.WriteLine($"새 창에서 URL 열기 (확장프로그램 포함): {url}");
                    return Task.FromResult(true);
                }
                
                return Task.FromResult(false);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"URL 열기 실패: {ex.Message}");
                return Task.FromResult(false);
            }
        }
        
        private string GetChromePath()
        {
            // Chrome 설치 경로 찾기
            var chromePaths = new[]
            {
                @"C:\Program Files\Google\Chrome\Application\chrome.exe",
                @"C:\Program Files (x86)\Google\Chrome\Application\chrome.exe",
                @"C:\Users\" + Environment.UserName + @"\AppData\Local\Google\Chrome\Application\chrome.exe"
            };
            
            foreach (var path in chromePaths)
            {
                if (File.Exists(path))
                {
                    return path;
                }
            }
            
            return "chrome"; // PATH에서 찾기
        }
    }
}
