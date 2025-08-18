using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia;
using Avalonia.Styling;
using Avalonia.Media;
using System;

namespace Gumaedaehang
{
    public partial class MarketCheckPage : UserControl
    {
        public MarketCheckPage()
        {
            InitializeComponent();
            
            // 테마 변경 감지
            try
            {
                if (Application.Current != null)
                {
                    Application.Current.ActualThemeVariantChanged += OnThemeChanged;
                    UpdateTheme();
                }
                
                // ThemeManager 이벤트도 구독
                ThemeManager.Instance.ThemeChanged += OnThemeManagerChanged;
            }
            catch
            {
                // 테마 감지 실패시 기본 라이트 모드로 설정
            }
        }
        
        private void OnThemeChanged(object? sender, EventArgs e)
        {
            try
            {
                UpdateTheme();
            }
            catch
            {
                // 테마 변경 실패시 무시
            }
        }
        
        private void OnThemeManagerChanged(object? sender, ThemeManager.ThemeType themeType)
        {
            try
            {
                UpdateTheme();
            }
            catch
            {
                // 테마 변경 실패시 무시
            }
        }
        
        public void UpdateTheme()
        {
            try
            {
                if (ThemeManager.Instance.IsDarkTheme)
                {
                    this.Classes.Add("dark-theme");
                    
                    // 배경색을 직접 설정 (강제 적용)
                    this.Background = new SolidColorBrush(Color.Parse("#1E1E1E"));
                    
                    // 메인 그리드와 중앙 그리드에도 직접 배경색 설정
                    var mainGrid = this.FindControl<Grid>("MainGrid");
                    var centerGrid = this.FindControl<Grid>("CenterGrid");
                    
                    if (mainGrid != null)
                    {
                        mainGrid.Background = new SolidColorBrush(Color.Parse("#1E1E1E"));
                    }
                    
                    if (centerGrid != null)
                    {
                        centerGrid.Background = new SolidColorBrush(Color.Parse("#1E1E1E"));
                    }
                    
                    System.Diagnostics.Debug.WriteLine("MarketCheckPage: 다크모드 적용됨");
                }
                else
                {
                    this.Classes.Remove("dark-theme");
                    
                    // 배경색을 직접 설정 (라이트모드)
                    this.Background = new SolidColorBrush(Colors.White);
                    
                    // 메인 그리드와 중앙 그리드에도 직접 배경색 설정
                    var mainGrid = this.FindControl<Grid>("MainGrid");
                    var centerGrid = this.FindControl<Grid>("CenterGrid");
                    
                    if (mainGrid != null)
                    {
                        mainGrid.Background = new SolidColorBrush(Colors.White);
                    }
                    
                    if (centerGrid != null)
                    {
                        centerGrid.Background = new SolidColorBrush(Colors.Transparent);
                    }
                    
                    System.Diagnostics.Debug.WriteLine("MarketCheckPage: 라이트모드 적용됨");
                }
            }
            catch
            {
                // 테마 설정 실패시 기본값 유지
                this.Classes.Remove("dark-theme");
                this.Background = new SolidColorBrush(Colors.White);
            }
        }

        private void OnMarketRegistrationClick(object? sender, RoutedEventArgs e)
        {
            // 마켓등록 페이지로 이동하는 로직
            // MainWindow의 인스턴스를 찾아서 마켓등록 페이지로 전환
            var mainWindow = TopLevel.GetTopLevel(this) as MainWindow;
            if (mainWindow != null)
            {
                // 마켓등록 페이지로 이동
                mainWindow.NavigateToMarketRegistration();
                System.Diagnostics.Debug.WriteLine("마켓등록 페이지로 이동했습니다.");
            }
        }
    }
}
