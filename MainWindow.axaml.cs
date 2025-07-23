using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Threading;
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
        private Grid? _marketCheckContent;
        private Grid? _mainProductContent;
        private Grid? _settingsContent;
        
        // 명언 관련 요소
        private TextBlock? _adviceText;
        private TextBlock? _adviceAuthor;
        private readonly AdviceService _adviceService;
        
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
            
            _marketCheckContent = this.FindControl<Grid>("MarketCheckContent");
            Debug.WriteLine($"MarketCheckContent found: {_marketCheckContent != null}");
            
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
                if (_adviceText != null)
                {
                    _adviceText.Text = "명언을 불러오는 중...";
                    
                    // API에서 명언 가져오기
                    var advice = await _adviceService.GetRandomAdviceAsync();
                    
                    // UI 스레드에서 업데이트
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        if (_adviceText != null && _adviceAuthor != null)
                        {
                            _adviceText.Text = advice.Message;
                            _adviceAuthor.Text = advice.Author;
                            _adviceAuthor.IsVisible = !string.IsNullOrEmpty(advice.Author);
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"명언 로딩 오류: {ex.Message}");
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
    }
}
