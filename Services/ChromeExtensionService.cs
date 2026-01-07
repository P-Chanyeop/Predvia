using System;
using System.Diagnostics;
using System.IO;
using System.Management;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace Gumaedaehang.Services
{
    public class ChromeExtensionService
    {
        private readonly string _extensionPath;
        private Process? _naverPriceComparisonProcess; // 가격비교 창 전용 프로세스

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
                    
                    // 가격비교 창 전용 프로세스 저장 (크롤링 완료까지 유지)
                    _naverPriceComparisonProcess = process;
                    
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
                    // ⭐ 가격비교 창 전용 프로세스 저장 (크롤링 완료 시까지 유지)
                    _naverPriceComparisonProcess = process;
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
        
        // 가격비교 창만 선별적으로 닫기
        public void CloseNaverPriceComparisonOnly()
        {
            try
            {
                if (_naverPriceComparisonProcess != null && !_naverPriceComparisonProcess.HasExited)
                {
                    Debug.WriteLine("🔥 네이버 가격비교 창만 선별적으로 닫기 시작");
                    _naverPriceComparisonProcess.CloseMainWindow();

                    Task.Delay(1000).ContinueWith(_ =>
                    {
                        if (!_naverPriceComparisonProcess.HasExited)
                        {
                            _naverPriceComparisonProcess.Kill();
                        }
                        _naverPriceComparisonProcess?.Dispose();
                        _naverPriceComparisonProcess = null;
                        Debug.WriteLine("✅ 네이버 가격비교 창 닫기 완료");
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ 가격비교 창 닫기 오류: {ex.Message}");
            }
        }

        // ⭐ 모든 Chrome 앱 모드 프로세스 종료 (static 메서드)
        public static async Task CloseAllChromeAppProcesses()
        {
            try
            {
                LogWindow.AddLogStatic("🔥 모든 Chrome 앱 프로세스 종료 시작");

                var chromeProcesses = Process.GetProcessesByName("chrome");
                var appProcesses = new System.Collections.Generic.List<int>(); // --app 모드 프로세스 ID 목록
                var checkedCount = 0;

                // 1단계: --app 모드 프로세스 찾기
                foreach (var process in chromeProcesses)
                {
                    try
                    {
                        if (process.HasExited) continue;
                        checkedCount++;

                        bool shouldClose = false;
                        string reason = "";

                        // 방법 1: CommandLine으로 --app 옵션 확인
                        try
                        {
                            using (var searcher = new ManagementObjectSearcher(
                                $"SELECT CommandLine FROM Win32_Process WHERE ProcessId = {process.Id}"))
                            {
                                foreach (ManagementObject obj in searcher.Get())
                                {
                                    var commandLine = obj["CommandLine"]?.ToString() ?? "";

                                    if (commandLine.Length > 0)
                                    {
                                        LogWindow.AddLogStatic($"🔍 PID {process.Id}: {(commandLine.Length > 100 ? commandLine.Substring(0, 100) + "..." : commandLine)}");

                                        // --app 모드인 Chrome 프로세스 확인
                                        if (commandLine.Contains("--app="))
                                        {
                                            // --load-extension도 포함되어 있으면 확실히 크롤링/가격비교 창
                                            if (commandLine.Contains("--load-extension") ||
                                                commandLine.Contains("shopping.naver.com") ||
                                                commandLine.Contains("smartstore.naver.com"))
                                            {
                                                shouldClose = true;
                                                reason = "CommandLine 확인";
                                            }
                                        }
                                    }
                                    break;
                                }
                            }
                        }
                        catch (Exception cmdEx)
                        {
                            LogWindow.AddLogStatic($"⚠️ CommandLine 체크 실패 PID {process.Id}: {cmdEx.Message}");
                        }

                        // 방법 2: 창 제목으로 "네이버 가격비교" 확인 (CommandLine이 실패하거나 매칭 안될 때)
                        if (!shouldClose)
                        {
                            try
                            {
                                var handle = FindChromeWindowByProcessId(process.Id);
                                if (handle != IntPtr.Zero)
                                {
                                    var windowTitle = new System.Text.StringBuilder(256);
                                    GetWindowText(handle, windowTitle, windowTitle.Capacity);
                                    string title = windowTitle.ToString();

                                    LogWindow.AddLogStatic($"🔍 PID {process.Id} 창 제목: {title}");

                                    // 네이버 가격비교, 스마트스토어 관련 제목 확인
                                    if (title.Contains("네이버 가격비교") ||
                                        title.Contains("가격비교") ||
                                        title.Contains("스마트스토어") ||
                                        title.Contains("smartstore"))
                                    {
                                        shouldClose = true;
                                        reason = "창 제목 확인";
                                        LogWindow.AddLogStatic($"✅ 가격비교/스마트스토어 창 발견: '{title}'");
                                    }
                                }
                            }
                            catch (Exception winEx)
                            {
                                LogWindow.AddLogStatic($"⚠️ 창 제목 체크 실패 PID {process.Id}: {winEx.Message}");
                            }
                        }

                        if (shouldClose)
                        {
                            appProcesses.Add(process.Id);
                            LogWindow.AddLogStatic($"✅ 종료 대상 발견 ({reason}): PID {process.Id}");
                        }
                    }
                    catch (Exception ex)
                    {
                        LogWindow.AddLogStatic($"⚠️ 프로세스 체크 실패 PID {process.Id}: {ex.Message}");
                    }
                }

                LogWindow.AddLogStatic($"📊 총 {checkedCount}개 Chrome 프로세스 확인, {appProcesses.Count}개 종료 대상 발견");

                // 2단계: 종료 대상 프로세스 종료
                int closedCount = 0;
                foreach (var pid in appProcesses)
                {
                    try
                    {
                        var process = Process.GetProcessById(pid);
                        if (!process.HasExited)
                        {
                            LogWindow.AddLogStatic($"🎯 앱 모드 Chrome 종료 중: PID {pid}");
                            process.Kill(entireProcessTree: true);
                            process.WaitForExit(2000);
                            closedCount++;
                            LogWindow.AddLogStatic($"✅ PID {pid} 종료 완료");
                        }
                        process.Dispose();
                    }
                    catch (Exception ex)
                    {
                        LogWindow.AddLogStatic($"⚠️ 프로세스 종료 실패 PID {pid}: {ex.Message}");
                    }
                }

                LogWindow.AddLogStatic($"✅ Chrome 앱 프로세스 종료 완료: {closedCount}개 종료");
                await Task.Delay(1000); // 프로세스 정리 대기
            }
            catch (Exception ex)
            {
                LogWindow.AddLogStatic($"❌ Chrome 앱 프로세스 종료 오류: {ex.Message}");
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
