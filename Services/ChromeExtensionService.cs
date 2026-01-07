using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace Gumaedaehang.Services
{
    public class ChromeExtensionService
    {
        private readonly string _extensionPath;

        // ⭐ Windows API - 창 활성화
        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern bool BringWindowToTop(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        // ⭐ EnumWindows로 모든 창 찾기 (Chrome --app 모드용)
        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int GetClassName(IntPtr hWnd, System.Text.StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        private const int SW_RESTORE = 9;
        private const int SW_SHOW = 5;
        private const int SW_SHOWNOACTIVATE = 4;
        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        private static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_SHOWWINDOW = 0x0040;

        // ⭐ 프로세스 ID로 Chrome 창 핸들 찾기 (제목으로 네이버 쇼핑 확인)
        private static IntPtr FindChromeWindowByProcessId(int processId)
        {
            IntPtr foundHandle = IntPtr.Zero;

            EnumWindows((hWnd, lParam) =>
            {
                GetWindowThreadProcessId(hWnd, out uint windowProcessId);

                if (windowProcessId == processId)
                {
                    // 보이는 창인지 확인
                    if (!IsWindowVisible(hWnd))
                        return true;

                    // Chrome 창 클래스 이름 확인
                    var className = new System.Text.StringBuilder(256);
                    GetClassName(hWnd, className, className.Capacity);

                    if (className.ToString().Contains("Chrome_WidgetWin"))
                    {
                        // 창 제목 확인 (네이버 쇼핑 페이지인지)
                        var windowTitle = new System.Text.StringBuilder(256);
                        GetWindowText(hWnd, windowTitle, windowTitle.Capacity);
                        string title = windowTitle.ToString();

                        // 네이버 가격비교 페이지인지 확인 ([키워드] : 네이버 가격비교)
                        if (title.Contains("네이버 가격비교") || title.Contains("가격비교"))
                        {
                            foundHandle = hWnd;
                            return false; // 찾았으니 중단
                        }
                    }
                }

                return true; // 계속 검색
            }, IntPtr.Zero);

            return foundHandle;
        }
        
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
                // Chrome을 확장프로그램과 함께 실행하면서 네이버 가격비교 페이지로 이동
                // ⭐ --app 모드로 우하단 작은 창 실행, EnumWindows로 핸들 찾아서 포커싱
                // ⭐ 기본 프로필 사용 (네이버 로그인 유지)
                var chromeArgs = $"--load-extension=\"{_extensionPath}\" --app=\"{searchUrl}\" --window-size=300,300 --window-position=1600,750 --no-first-run --no-default-browser-check --disable-web-security --user-agent=\"Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36\"";

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
                    Debug.WriteLine($"네이버 가격비교 페이지 열기 (포커싱 모드): {searchUrl}");

                    // ⭐ 계속 포커싱 유지 (사용자가 밀어도 다시 활성화)
                    _ = Task.Run(async () =>
                    {
                        LogWindow.AddLogStatic($"🚀 포커싱 작업 시작");

                        // ⭐ 최대 18초 동안 계속 시도
                        int attemptCount = 0;
                        int successCount = 0;
                        DateTime startTime = DateTime.Now;
                        TimeSpan timeout = TimeSpan.FromSeconds(18);
                        IntPtr targetHandle = IntPtr.Zero;

                        while ((DateTime.Now - startTime) < timeout)
                        {
                            attemptCount++;

                            try
                            {
                                // ⭐ 모든 Chrome 프로세스에서 가격비교 창 찾기
                                if (targetHandle == IntPtr.Zero)
                                {
                                    // 아직 창을 못 찾았으면 모든 Chrome 프로세스 검색
                                    var chromeProcesses = Process.GetProcessesByName("chrome");
                                    LogWindow.AddLogStatic($"🔍 Chrome 프로세스 {chromeProcesses.Length}개 검색 중...");

                                    foreach (var chromeProc in chromeProcesses)
                                    {
                                        IntPtr handle = FindChromeWindowByProcessId(chromeProc.Id);
                                        if (handle != IntPtr.Zero)
                                        {
                                            // 창 제목 로그로 확인
                                            var windowTitle = new System.Text.StringBuilder(256);
                                            GetWindowText(handle, windowTitle, windowTitle.Capacity);
                                            targetHandle = handle;
                                            LogWindow.AddLogStatic($"🔍 Chrome 창 발견! Handle: {handle}, PID: {chromeProc.Id}, Title: {windowTitle}");
                                            break;
                                        }
                                    }
                                }

                                if (targetHandle != IntPtr.Zero)
                                {
                                    // ⭐ 여러 방법으로 포커싱 시도
                                    bool result2 = ShowWindow(targetHandle, SW_SHOW);
                                    bool result3 = BringWindowToTop(targetHandle);
                                    bool result4 = SetForegroundWindow(targetHandle);

                                    // ⭐ 최상위로 올리기
                                    SetWindowPos(targetHandle, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW);
                                    await Task.Delay(50);
                                    SetWindowPos(targetHandle, HWND_NOTOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW);

                                    successCount++;
                                    if (successCount == 1 || successCount % 5 == 0)
                                    {
                                        LogWindow.AddLogStatic($"✅ 가격비교 창 활성화 {successCount}회 - Show:{result2}, Bring:{result3}, Focus:{result4}");
                                    }
                                }
                                else
                                {
                                    // 창을 못 찾은 경우
                                    if (attemptCount <= 3 || attemptCount % 5 == 0)
                                    {
                                        LogWindow.AddLogStatic($"⚠️ Chrome 창 찾는 중... (시도 {attemptCount}회)");
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                LogWindow.AddLogStatic($"❌ 창 활성화 실패 {attemptCount}회: {ex.Message}");
                            }

                            await Task.Delay(1500); // 1.5초마다 반복
                        }

                        LogWindow.AddLogStatic($"🔚 포커싱 완료 - 총 {attemptCount}회 시도, {successCount}회 성공");
                    });

                    // ⭐ 120초 후 자동 종료 (스토어 크롤링 시간 충분히 확보)
                    _ = Task.Run(async () =>
                    {
                        await Task.Delay(120000); // 120초(2분) 대기
                        try
                        {
                            if (!process.HasExited)
                            {
                                Debug.WriteLine("120초 경과 - 네이버 가격비교 Chrome 강제 종료 시작");

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
