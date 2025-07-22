using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using System;
using System.Diagnostics;

namespace Gumaedaehang
{
    public partial class MainWindow : Window
    {
        // 사용자 정보
        private string _username = "admin"; // 기본값
        
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
        
        public MainWindow()
        {
            InitializeComponent();
            
            // UI 요소 참조 가져오기
            var themeToggleButton = this.FindControl<Button>("themeToggleButton");
            var themeToggleText = this.FindControl<TextBlock>("themeToggleText");
            var userWelcomeText = this.FindControl<TextBlock>("userWelcomeText");
            
            // 탭 버튼 참조
            _sourcingTab = this.FindControl<Button>("SourcingTab");
            _marketCheckTab = this.FindControl<Button>("MarketCheckTab");
            _mainProductTab = this.FindControl<Button>("MainProductTab");
            _settingsTab = this.FindControl<Button>("SettingsTab");
            
            // 콘텐츠 영역 참조
            _homeContent = this.FindControl<Grid>("HomeContent");
            _sourcingContent = this.FindControl<ContentControl>("SourcingContent");
            _marketCheckContent = this.FindControl<Grid>("MarketCheckContent");
            _mainProductContent = this.FindControl<Grid>("MainProductContent");
            _settingsContent = this.FindControl<Grid>("SettingsContent");
            
            // 이벤트 핸들러 등록
            if (themeToggleButton != null)
                themeToggleButton.Click += ThemeToggleButton_Click;
                
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
            if (userWelcomeText != null)
                userWelcomeText.Text = $"{_username} 님 어서오세요.";
            
            // 현재 테마에 맞게 버튼 텍스트 업데이트
            if (themeToggleText != null)
                themeToggleText.Text = ThemeManager.Instance.IsDarkTheme ? "라이트모드" : "다크모드";
            
            // 테마 변경 이벤트 구독
            ThemeManager.Instance.ThemeChanged += (sender, theme) => {
                var toggleText = this.FindControl<TextBlock>("themeToggleText");
                if (toggleText != null)
                    toggleText.Text = ThemeManager.Instance.IsDarkTheme ? "라이트모드" : "다크모드";
            };
            
            // 디버그 메시지 출력
            Debug.WriteLine("MainWindow initialized");
        }
        
        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
            Debug.WriteLine("InitializeComponent called");
        }
        
        // 로그인한 사용자 이름 설정 메서드
        public void SetUsername(string username)
        {
            _username = username;
            var userWelcomeText = this.FindControl<TextBlock>("userWelcomeText");
            if (userWelcomeText != null)
                userWelcomeText.Text = $"{_username} 님 어서오세요.";
        }
        
        private void ThemeToggleButton_Click(object? sender, RoutedEventArgs e)
        {
            // 테마 전환
            ThemeManager.Instance.ToggleTheme();
        }
        
        // 탭 전환 메서드들
        private void SourcingTab_Click(object? sender, RoutedEventArgs e)
        {
            Debug.WriteLine("SourcingTab_Click called");
            ShowContent(_sourcingContent);
            UpdateTabStyles(_sourcingTab);
        }
        
        private void MarketCheckTab_Click(object? sender, RoutedEventArgs e)
        {
            Debug.WriteLine("MarketCheckTab_Click called");
            ShowContent(_marketCheckContent);
            UpdateTabStyles(_marketCheckTab);
        }
        
        private void MainProductTab_Click(object? sender, RoutedEventArgs e)
        {
            Debug.WriteLine("MainProductTab_Click called");
            ShowContent(_mainProductContent);
            UpdateTabStyles(_mainProductTab);
        }
        
        private void SettingsTab_Click(object? sender, RoutedEventArgs e)
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
        }
        
        // 탭 스타일 업데이트 메서드
        private void UpdateTabStyles(Button? activeTab)
        {
            Debug.WriteLine($"UpdateTabStyles called with {activeTab?.Name}");
            
            // 모든 탭 스타일 초기화
            if (_sourcingTab != null)
            {
                _sourcingTab.FontWeight = FontWeight.Normal;
                _sourcingTab.Foreground = new SolidColorBrush(Colors.Black);
            }
            
            if (_marketCheckTab != null)
            {
                _marketCheckTab.FontWeight = FontWeight.Normal;
                _marketCheckTab.Foreground = new SolidColorBrush(Colors.Black);
            }
            
            if (_mainProductTab != null)
            {
                _mainProductTab.FontWeight = FontWeight.Normal;
                _mainProductTab.Foreground = new SolidColorBrush(Colors.Black);
            }
            
            if (_settingsTab != null)
            {
                _settingsTab.FontWeight = FontWeight.Normal;
                _settingsTab.Foreground = new SolidColorBrush(Colors.Black);
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
