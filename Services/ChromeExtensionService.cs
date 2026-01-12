using System;
using System.Diagnostics;
using System.IO;
using System.Management;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using System.Net.Http;
using System.Text;

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
                        
                        // ⭐ 포커싱 실패 시 로그인 안내 및 브라우저 종료
                        if (successCount == 0)
                        {
                            LogWindow.AddLogStatic("❌ 포커싱 실패 - CAPTCHA 또는 로그인 문제 가능성");
                            
                            // 브라우저 종료
                            try
                            {
                                if (!process.HasExited)
                                {
                                    process.CloseMainWindow();
                                    await Task.Delay(1000);
                                    if (!process.HasExited)
                                    {
                                        process.Kill();
                                    }
                                    LogWindow.AddLogStatic("🔥 포커싱 실패로 가격비교 브라우저 종료");
                                }
                                
                                // 가격비교 프로세스 정리
                                if (_naverPriceComparisonProcess != null && !_naverPriceComparisonProcess.HasExited)
                                {
                                    _naverPriceComparisonProcess.CloseMainWindow();
                                    await Task.Delay(1000);
                                    if (!_naverPriceComparisonProcess.HasExited)
                                    {
                                        _naverPriceComparisonProcess.Kill();
                                    }
                                    _naverPriceComparisonProcess?.Dispose();
                                    _naverPriceComparisonProcess = null;
                                    LogWindow.AddLogStatic("🔥 가격비교 프로세스 정리 완료");
                                }
                            }
                            catch (Exception ex)
                            {
                                LogWindow.AddLogStatic($"❌ 브라우저 종료 오류: {ex.Message}");
                            }
                            
                            // UI 스레드에서 메시지 박스 표시
                            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                            {
                                try
                                {
                                    var messageBox = new Window
                                    {
                                        Title = "로그인 필요",
                                        Width = 350,
                                        Height = 150,
                                        WindowStartupLocation = Avalonia.Controls.WindowStartupLocation.CenterScreen,
                                        CanResize = false
                                    };
                                    
                                    var grid = new Avalonia.Controls.Grid
                                    {
                                        Margin = new Avalonia.Thickness(20),
                                        RowDefinitions = new Avalonia.Controls.RowDefinitions("Auto,20,Auto"),
                                        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                                        VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
                                    };
                                    
                                    var messageText = new Avalonia.Controls.TextBlock
                                    {
                                        Text = "로그인 후 다시 시도하세요.",
                                        FontSize = 14,
                                        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                                        VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
                                    };
                                    Avalonia.Controls.Grid.SetRow(messageText, 0);
                                    
                                    var okButton = new Avalonia.Controls.Button
                                    {
                                        Content = "확인",
                                        Width = 100,
                                        Height = 35,
                                        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                                        VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                                        Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#E67E22")),
                                        Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Colors.White)
                                    };
                                    Avalonia.Controls.Grid.SetRow(okButton, 2);
                                    
                                    okButton.Click += async (s, e) => 
                                    {
                                        messageBox.Close();
                                        
                                        // 크롤링 중단 처리
                                        try
                                        {
                                            // 서버에 크롤링 중단 신호 전송
                                            var httpClient = new System.Net.Http.HttpClient();
                                            var content = new System.Net.Http.StringContent("{\"reason\":\"포커싱 실패\"}", System.Text.Encoding.UTF8, "application/json");
                                            await httpClient.PostAsync("http://localhost:8080/api/smartstore/stop", content);
                                            LogWindow.AddLogStatic("🛑 포커싱 실패로 크롤링 중단 요청 완료");
                                        }
                                        catch (Exception ex)
                                        {
                                            LogWindow.AddLogStatic($"❌ 크롤링 중단 요청 오류: {ex.Message}");
                                        }
                                    };
                                    
                                    grid.Children.Add(messageText);
                                    grid.Children.Add(okButton);
                                    messageBox.Content = grid;
                                    
                                    messageBox.Show();
                                }
                                catch (Exception ex)
                                {
                                    LogWindow.AddLogStatic($"❌ 메시지 박스 표시 오류: {ex.Message}");
                                }
                            });
                        }
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

        // ⭐ 네이버 가격비교 창만 종료 (static 메서드) - 창 제목으로 찾기
        public static async Task CloseNaverPriceComparisonWindowByTitle()
        {
            try
            {
                LogWindow.AddLogStatic("🔥 네이버 가격비교 창 종료 시작 (--app + 창제목 검색)");

                var chromeProcesses = Process.GetProcessesByName("chrome");
                var priceComparisonProcesses = new System.Collections.Generic.List<int>();
                var checkedCount = 0;

                // 1단계: --app 모드이면서 창 제목에 "네이버 가격비교"가 있는 프로세스 찾기
                foreach (var process in chromeProcesses)
                {
                    try
                    {
                        if (process.HasExited) continue;
                        checkedCount++;

                        bool isAppMode = false;
                        bool isPriceComparison = false;

                        // CommandLine으로 --app 옵션 확인
                        using (var searcher = new ManagementObjectSearcher(
                            $"SELECT CommandLine FROM Win32_Process WHERE ProcessId = {process.Id}"))
                        {
                            foreach (ManagementObject obj in searcher.Get())
                            {
                                var commandLine = obj["CommandLine"]?.ToString() ?? "";
                                if (commandLine.Contains("--app="))
                                {
                                    isAppMode = true;
                                    LogWindow.AddLogStatic($"🔍 PID {process.Id}: --app 모드 확인");
                                }
                                break;
                            }
                        }

                        // --app 모드인 경우에만 창 제목 확인
                        if (isAppMode)
                        {
                            var handle = FindChromeWindowByProcessId(process.Id);
                            if (handle != IntPtr.Zero)
                            {
                                var windowTitle = new System.Text.StringBuilder(256);
                                GetWindowText(handle, windowTitle, windowTitle.Capacity);
                                string title = windowTitle.ToString();

                                if (title.Contains("네이버 가격비교") || title.Contains("가격비교"))
                                {
                                    isPriceComparison = true;
                                    LogWindow.AddLogStatic($"🔍 PID {process.Id}: 창 제목 '{title}' - 가격비교 확인");
                                }
                            }
                        }

                        // ⭐ --app 모드 AND 가격비교 창 제목 → 종료 대상
                        if (isAppMode && isPriceComparison)
                        {
                            priceComparisonProcesses.Add(process.Id);
                            LogWindow.AddLogStatic($"✅ 가격비교 창 발견: PID {process.Id}");
                        }
                    }
                    catch (Exception ex)
                    {
                        LogWindow.AddLogStatic($"⚠️ 프로세스 체크 실패 PID {process.Id}: {ex.Message}");
                    }
                }

                LogWindow.AddLogStatic($"📊 총 {checkedCount}개 Chrome 프로세스 확인, {priceComparisonProcesses.Count}개 가격비교 창 발견");

                // 2단계: 가격비교 창 종료
                int closedCount = 0;
                foreach (var pid in priceComparisonProcesses)
                {
                    try
                    {
                        var process = Process.GetProcessById(pid);
                        if (!process.HasExited)
                        {
                            LogWindow.AddLogStatic($"🎯 가격비교 창 종료 중: PID {pid}");
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

                LogWindow.AddLogStatic($"✅ 가격비교 창 종료 완료: {closedCount}개 종료");
                await Task.Delay(500);
            }
            catch (Exception ex)
            {
                LogWindow.AddLogStatic($"❌ 가격비교 창 종료 오류: {ex.Message}");
            }
        }

        // ⭐ 크롤링 스마트스토어 앱 창들만 종료 (static 메서드) - CommandLine 확인
        public static async Task CloseSmartStoreCrawlingWindows()
        {
            try
            {
                LogWindow.AddLogStatic("🔥 크롤링 스마트스토어 창들 종료 시작");

                var chromeProcesses = Process.GetProcessesByName("chrome");
                var appProcesses = new System.Collections.Generic.List<int>();
                var checkedCount = 0;

                // 1단계: --app 모드이면서 스마트스토어인 프로세스 찾기
                foreach (var process in chromeProcesses)
                {
                    try
                    {
                        if (process.HasExited) continue;
                        checkedCount++;

                        // CommandLine으로 --app 옵션 확인
                        using (var searcher = new ManagementObjectSearcher(
                            $"SELECT CommandLine FROM Win32_Process WHERE ProcessId = {process.Id}"))
                        {
                            foreach (ManagementObject obj in searcher.Get())
                            {
                                var commandLine = obj["CommandLine"]?.ToString() ?? "";

                                // ⭐ --app 모드이면서 smartstore.naver.com 포함 (네이버 가격비교 제외)
                                // 네이버 가격비교는 search.shopping.naver.com이므로 제외됨
                                if (commandLine.Contains("--app=") &&
                                    commandLine.Contains("smartstore.naver.com") &&
                                    !commandLine.Contains("search.shopping.naver.com"))
                                {
                                    appProcesses.Add(process.Id);
                                    LogWindow.AddLogStatic($"✅ 크롤링 창 발견: PID {process.Id}");
                                    break;
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        LogWindow.AddLogStatic($"⚠️ 프로세스 체크 실패 PID {process.Id}: {ex.Message}");
                    }
                }

                LogWindow.AddLogStatic($"📊 총 {checkedCount}개 Chrome 프로세스 확인, {appProcesses.Count}개 크롤링 창 발견");

                // 2단계: 크롤링 창 종료
                int closedCount = 0;
                foreach (var pid in appProcesses)
                {
                    try
                    {
                        var process = Process.GetProcessById(pid);
                        if (!process.HasExited)
                        {
                            LogWindow.AddLogStatic($"🎯 크롤링 창 종료 중: PID {pid}");
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

                LogWindow.AddLogStatic($"✅ 크롤링 창 종료 완료: {closedCount}개 종료");
                await Task.Delay(500);
            }
            catch (Exception ex)
            {
                LogWindow.AddLogStatic($"❌ 크롤링 창 종료 오류: {ex.Message}");
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
