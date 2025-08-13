using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Gumaedaehang.Services;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Gumaedaehang
{
    public partial class MainWindow : Window
    {
        // 탭 버튼들
        private Button? _sourcingTab;
        private Button? _marketCheckTab;
        private Button? _mainProductTab;
        private Button? _settingsTab;
        
        // 콘텐츠 영역들
        private Grid? _homeContent;
        private ContentControl? _sourcingContent;
        private ContentControl? _marketCheckContent;
        private ContentControl? _marketRegistrationContent;
        private Grid? _mainProductContent;
        private Grid? _settingsContent;
        
        // 명언 관련 요소
        private TextBlock? _adviceText;
        private TextBlock? _adviceAuthor;
        private ScrollViewer? _adviceScrollViewer;
        private StackPanel? _adviceContainer;
        private readonly AdviceService _adviceService;
        private DispatcherTimer? _slideTimer;
        
        public MainWindow()
        {
            InitializeComponent();
            Debug.WriteLine("MainWindow initialized");
            
            // 서비스 초기화
            _adviceService = new AdviceService();
            
            // UI 요소 참조 가져오기
            var themeToggleButton = this.FindControl<Button>("themeToggleButton");
            var themeToggleText = this.FindControl<TextBlock>("themeToggleText");
            var userWelcomeText = this.FindControl<TextBlock>("userWelcomeText");
            _adviceText = this.FindControl<TextBlock>("adviceText");
            _adviceAuthor = this.FindControl<TextBlock>("adviceAuthor");
            _adviceScrollViewer = this.FindControl<ScrollViewer>("adviceScrollViewer");
            _adviceContainer = this.FindControl<StackPanel>("adviceContainer");
            
            Debug.WriteLine("Finding tab buttons...");
            // 탭 버튼 참조
            _sourcingTab = this.FindControl<Button>("SourcingTab");
            Debug.WriteLine($"SourcingTab found: {_sourcingTab != null}");
            
            _marketCheckTab = this.FindControl<Button>("MarketCheckTab");
            Debug.WriteLine($"MarketCheckTab found: {_marketCheckTab != null}");
            
            _mainProductTab = this.FindControl<Button>("MainProductTab");
            Debug.WriteLine($"MainProductTab found: {_mainProductTab != null}");
            
            _settingsTab = this.FindControl<Button>("SettingsTab");
            Debug.WriteLine($"SettingsTab found: {_settingsTab != null}");
            
            Debug.WriteLine("Finding content areas...");
            // 콘텐츠 영역 참조
            _homeContent = this.FindControl<Grid>("HomeContent");
            Debug.WriteLine($"HomeContent found: {_homeContent != null}");
            
            _sourcingContent = this.FindControl<ContentControl>("SourcingContent");
            Debug.WriteLine($"SourcingContent found: {_sourcingContent != null}");
            
            _marketCheckContent = this.FindControl<ContentControl>("MarketCheckContent");
            Debug.WriteLine($"MarketCheckContent found: {_marketCheckContent != null}");
            
            _marketRegistrationContent = this.FindControl<ContentControl>("MarketRegistrationContent");
            Debug.WriteLine($"MarketRegistrationContent found: {_marketRegistrationContent != null}");
            
            _mainProductContent = this.FindControl<Grid>("MainProductContent");
            Debug.WriteLine($"MainProductContent found: {_mainProductContent != null}");
            
            _settingsContent = this.FindControl<Grid>("SettingsContent");
            Debug.WriteLine($"SettingsContent found: {_settingsContent != null}");
            
            // 이벤트 핸들러 등록
            if (themeToggleButton != null)
                themeToggleButton.Click += ThemeToggleButton_Click;
                
            Debug.WriteLine("Registering tab button event handlers...");
            // 탭 버튼 이벤트 등록
            if (_sourcingTab != null)
            {
                _sourcingTab.Click += SourcingTab_Click;
                Debug.WriteLine("SourcingTab event handler registered");
            }
            
            if (_marketCheckTab != null)
            {
                _marketCheckTab.Click += MarketCheckTab_Click;
                Debug.WriteLine("MarketCheckTab event handler registered");
            }
            
            if (_mainProductTab != null)
            {
                _mainProductTab.Click += MainProductTab_Click;
                Debug.WriteLine("MainProductTab event handler registered");
            }
            
            if (_settingsTab != null)
            {
                _settingsTab.Click += SettingsTab_Click;
                Debug.WriteLine("SettingsTab event handler registered");
            }
            
            // 사용자 환영 메시지 업데이트
            if (userWelcomeText != null && AuthManager.Instance.IsAuthenticated)
                userWelcomeText.Text = $"{AuthManager.Instance.Username} 님 어서오세요.";
            
            // 현재 테마에 맞게 버튼 텍스트 업데이트
            if (themeToggleText != null)
                themeToggleText.Text = ThemeManager.Instance.IsDarkTheme ? "라이트모드" : "다크모드";
            
            // 명언 가져오기
            LoadRandomAdvice();
            
            // 테마 변경 이벤트 구독
            ThemeManager.Instance.ThemeChanged += (sender, theme) => {
                var toggleText = this.FindControl<TextBlock>("themeToggleText");
                if (toggleText != null)
                    toggleText.Text = ThemeManager.Instance.IsDarkTheme ? "라이트모드" : "다크모드";
                
                // 테마 변경 시 현재 활성화된 탭을 찾아 스타일 업데이트
                Button? activeTab = null;
                if (_sourcingTab != null && _sourcingContent != null && _sourcingContent.IsVisible)
                    activeTab = _sourcingTab;
                else if (_marketCheckTab != null && _marketCheckContent != null && _marketCheckContent.IsVisible)
                    activeTab = _marketCheckTab;
                else if (_mainProductTab != null && _mainProductContent != null && _mainProductContent.IsVisible)
                    activeTab = _mainProductTab;
                else if (_settingsTab != null && _settingsContent != null && _settingsContent.IsVisible)
                    activeTab = _settingsTab;
                
                UpdateTabStyles(activeTab);
            };
            
            // 인증 상태 변경 이벤트 구독
            AuthManager.Instance.AuthStateChanged += (sender, args) => {
                var welcomeText = this.FindControl<TextBlock>("userWelcomeText");
                if (welcomeText != null)
                {
                    if (args.IsAuthenticated)
                        welcomeText.Text = $"{args.Username} 님 어서오세요.";
                    else
                    {
                        // 로그아웃 시 API 키 인증 화면으로 이동
                        var apiKeyAuthWindow = new ApiKeyAuthWindow();
                        apiKeyAuthWindow.Show();
                        this.Close();
                    }
                }
            };
            
            // 초기 탭 스타일 설정 (소싱 탭이 기본 선택)
            UpdateTabStyles(_sourcingTab);
            
            // 창 닫기 이벤트 구독 (타이머 정리)
            this.Closing += (sender, e) => {
                StopAdviceSlideAnimation();
            };
            
            // 디버그 메시지 출력
            Debug.WriteLine("MainWindow initialization completed");
        }
        
        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
            Debug.WriteLine("InitializeComponent called");
        }
        
        // 명언 가져오기 메서드
        private async void LoadRandomAdvice()
        {
            try
            {
                Debug.WriteLine("LoadRandomAdvice started");
                
                if (_adviceText != null)
                {
                    // 기존 애니메이션 중지
                    StopAdviceSlideAnimation();
                    
                    _adviceText.Text = "명언을 불러오는 중...";
                    Debug.WriteLine("Loading advice...");
                    
                    // API에서 명언 가져오기
                    var advice = await _adviceService.GetRandomAdviceAsync();
                    Debug.WriteLine($"Advice loaded: {advice.Message} - {advice.Author}");
                    
                    // UI 스레드에서 업데이트
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        if (_adviceText != null && _adviceAuthor != null)
                        {
                            _adviceText.Text = advice.Message;
                            _adviceAuthor.Text = advice.Author;
                            _adviceAuthor.IsVisible = !string.IsNullOrEmpty(advice.Author);
                            
                            Debug.WriteLine($"UI updated with advice: {advice.Message}");
                            
                            // 명언 길이 확인 (10글자 이상이면 슬라이드)
                            var fullText = advice.Message + (string.IsNullOrEmpty(advice.Author) ? "" : $" - {advice.Author}");
                            Debug.WriteLine($"Full text length: {fullText.Length}");
                            
                            if (fullText.Length > 10)
                            {
                                // 레이아웃 업데이트 후 슬라이드 애니메이션 시작
                                Dispatcher.UIThread.Post(() =>
                                {
                                    StartAdviceSlideAnimation();
                                }, DispatcherPriority.Loaded);
                            }
                            else
                            {
                                // 짧은 텍스트는 스크롤 위치 초기화
                                if (_adviceScrollViewer != null)
                                {
                                    _adviceScrollViewer.Offset = new Vector(0, 0);
                                }
                            }
                        }
                    });
                }
                else
                {
                    Debug.WriteLine("_adviceText is null!");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"명언 로딩 오류: {ex.Message}");
                Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                
                if (_adviceText != null)
                {
                    _adviceText.Text = "명언을 불러오는 중 오류가 발생했습니다.";
                }
            }
        }
        
        private void ThemeToggleButton_Click(object? sender, RoutedEventArgs e)
        {
            // 테마 전환
            ThemeManager.Instance.ToggleTheme();
        }
        
        // 탭 전환 메서드들
        public void SourcingTab_Click(object? sender, RoutedEventArgs e)
        {
            Debug.WriteLine("SourcingTab_Click called");
            ShowContent(_sourcingContent);
            UpdateTabStyles(_sourcingTab);
        }
        
        public void MarketCheckTab_Click(object? sender, RoutedEventArgs e)
        {
            Debug.WriteLine("MarketCheckTab_Click called");
            ShowContent(_marketCheckContent);
            UpdateTabStyles(_marketCheckTab);
        }
        
        public void MainProductTab_Click(object? sender, RoutedEventArgs e)
        {
            Debug.WriteLine("MainProductTab_Click called");
            ShowContent(_mainProductContent);
            UpdateTabStyles(_mainProductTab);
        }
        
        public void SettingsTab_Click(object? sender, RoutedEventArgs e)
        {
            Debug.WriteLine("SettingsTab_Click called");
            ShowContent(_settingsContent);
            UpdateTabStyles(_settingsTab);
        }
        
        // 콘텐츠 표시 메서드
        private void ShowContent(Control? contentToShow)
        {
            Debug.WriteLine($"ShowContent called with {contentToShow?.Name}");
            
            // 모든 콘텐츠 숨기기
            if (_homeContent != null) _homeContent.IsVisible = contentToShow == _homeContent;
            if (_sourcingContent != null) _sourcingContent.IsVisible = contentToShow == _sourcingContent;
            if (_marketCheckContent != null) _marketCheckContent.IsVisible = contentToShow == _marketCheckContent;
            if (_marketRegistrationContent != null) _marketRegistrationContent.IsVisible = contentToShow == _marketRegistrationContent;
            if (_mainProductContent != null) _mainProductContent.IsVisible = contentToShow == _mainProductContent;
            if (_settingsContent != null) _settingsContent.IsVisible = contentToShow == _settingsContent;
            
            // 소싱 페이지가 표시될 때 데이터 상태 설정
            if (contentToShow == _sourcingContent && _sourcingContent != null)
            {
                // 소싱 페이지의 인스턴스 가져오기
                var sourcingPage = _sourcingContent.Content as SourcingPage;
                if (sourcingPage != null)
                {
                    // 초기에는 데이터가 없는 상태로 설정
                    sourcingPage.SetHasData(false);
                }
            }
        }
        
        // 마켓등록 페이지로 이동하는 공개 메서드
        public void NavigateToMarketRegistration()
        {
            Debug.WriteLine("NavigateToMarketRegistration called");
            ShowContent(_marketRegistrationContent);
            
            // 마켓점검 탭을 활성화 상태로 유지 (마켓등록은 마켓점검의 하위 페이지)
            UpdateTabStyles(_marketCheckTab);
        }
        
        // 탭 스타일 업데이트 메서드
        private void UpdateTabStyles(Button? activeTab)
        {
            Debug.WriteLine($"UpdateTabStyles called with {activeTab?.Name}");
            
            // 현재 테마에 맞는 색상 가져오기
            var textColor = ThemeManager.Instance.IsDarkTheme ? Colors.White : Colors.Black;
            
            // 모든 탭 스타일 초기화
            if (_sourcingTab != null)
            {
                _sourcingTab.FontWeight = FontWeight.Normal;
                _sourcingTab.Foreground = new SolidColorBrush(textColor);
            }
            
            if (_marketCheckTab != null)
            {
                _marketCheckTab.FontWeight = FontWeight.Normal;
                _marketCheckTab.Foreground = new SolidColorBrush(textColor);
            }
            
            if (_mainProductTab != null)
            {
                _mainProductTab.FontWeight = FontWeight.Normal;
                _mainProductTab.Foreground = new SolidColorBrush(textColor);
            }
            
            if (_settingsTab != null)
            {
                _settingsTab.FontWeight = FontWeight.Normal;
                _settingsTab.Foreground = new SolidColorBrush(textColor);
            }
            
            // 활성 탭 스타일 설정
            if (activeTab != null)
            {
                activeTab.FontWeight = FontWeight.Bold;
                activeTab.Foreground = new SolidColorBrush(Color.Parse("#F47B20"));
            }
        }
        
        // 명언 슬라이드 애니메이션 시작
        private void StartAdviceSlideAnimation()
        {
            if (_adviceScrollViewer == null || _adviceContainer == null) return;
            
            // 레이아웃 업데이트 대기
            Dispatcher.UIThread.Post(() =>
            {
                try
                {
                    // 컨테이너 크기 측정
                    _adviceContainer.Measure(Size.Infinity);
                    var containerWidth = _adviceContainer.DesiredSize.Width;
                    var viewerWidth = 800; // Border Width 값
                    
                    Debug.WriteLine($"Container width: {containerWidth}, Viewer width: {viewerWidth}");
                    
                    // 텍스트가 뷰어보다 길 경우에만 슬라이드
                    if (containerWidth > viewerWidth)
                    {
                        // 기존 타이머 정리
                        _slideTimer?.Stop();
                        
                        // 새 타이머 생성
                        _slideTimer = new DispatcherTimer
                        {
                            Interval = TimeSpan.FromMilliseconds(50) // 50ms마다 업데이트
                        };
                        
                        double currentOffset = 0;
                        double maxOffset = containerWidth - viewerWidth + 50; // 여유 공간 추가
                        double speed = 1.0; // 픽셀/프레임
                        int pauseCounter = 0;
                        const int pauseFrames = 40; // 2초 정지 (50ms * 40 = 2000ms)
                        bool isWaiting = false;
                        
                        _slideTimer.Tick += (sender, e) =>
                        {
                            // 대기 중이면 카운터 감소
                            if (isWaiting)
                            {
                                pauseCounter--;
                                if (pauseCounter <= 0)
                                {
                                    // 대기 완료, 처음부터 다시 시작
                                    isWaiting = false;
                                    currentOffset = 0;
                                    _adviceScrollViewer.Offset = new Vector(currentOffset, 0);
                                }
                                return;
                            }
                            
                            // 오른쪽으로 슬라이드
                            currentOffset += speed;
                            
                            // 끝에 도달했으면 2초 대기 후 처음부터 다시 시작
                            if (currentOffset >= maxOffset)
                            {
                                currentOffset = maxOffset;
                                isWaiting = true;
                                pauseCounter = pauseFrames;
                            }
                            
                            _adviceScrollViewer.Offset = new Vector(currentOffset, 0);
                        };
                        
                        _slideTimer.Start();
                        Debug.WriteLine("Slide animation started (one-way with restart)");
                    }
                    else
                    {
                        // 텍스트가 짧으면 스크롤 위치 초기화
                        _adviceScrollViewer.Offset = new Vector(0, 0);
                        Debug.WriteLine("Text is short, no slide needed");
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Slide animation error: {ex.Message}");
                }
            }, DispatcherPriority.Loaded);
        }
        
        // 명언 슬라이드 애니메이션 중지
        private void StopAdviceSlideAnimation()
        {
            _slideTimer?.Stop();
            _slideTimer = null;
            
            // 스크롤 위치 초기화
            if (_adviceScrollViewer != null)
            {
                _adviceScrollViewer.Offset = new Vector(0, 0);
            }
        }
        
        private void UpdateThemeResources()
        {
            try
            {
                if (Application.Current?.Resources != null)
                {
                    if (ThemeManager.Instance.IsDarkTheme)
                    {
                        // 다크모드 색상으로 업데이트
                        Application.Current.Resources["BackgroundColor"] = Color.Parse("#1E1E1E");
                        Application.Current.Resources["BackgroundSecondaryColor"] = Color.Parse("#3D3D3D");
                        Application.Current.Resources["ForegroundColor"] = Colors.White;
                        Application.Current.Resources["AccentColor"] = Color.Parse("#FF8A46");
                        Application.Current.Resources["BorderColor"] = Color.Parse("#FF8A46");
                        Application.Current.Resources["DarkModeIconColor"] = Color.Parse("#FF8A46");
                    }
                    else
                    {
                        // 라이트모드 색상으로 업데이트
                        Application.Current.Resources["BackgroundColor"] = Colors.White;
                        Application.Current.Resources["BackgroundSecondaryColor"] = Color.Parse("#FFF8F3");
                        Application.Current.Resources["ForegroundColor"] = Color.Parse("#333333");
                        Application.Current.Resources["AccentColor"] = Color.Parse("#F47B20");
                        Application.Current.Resources["BorderColor"] = Color.Parse("#DDDDDD");
                        Application.Current.Resources["DarkModeIconColor"] = Color.Parse("#FFE4D0");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"테마 리소스 업데이트 실패: {ex.Message}");
            }
        }
    }
}
