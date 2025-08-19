using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.Input;
using Avalonia.LogicalTree;
using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;

namespace Gumaedaehang
{
    public partial class SettingsPage : UserControl
    {
        public SettingsPage()
        {
            InitializeComponent();
            
            // 테마 변경 이벤트 구독
            if (Application.Current != null)
            {
                Application.Current.ActualThemeVariantChanged += OnThemeChanged;
                UpdateTheme();
            }
            
            // ThemeManager 이벤트도 구독
            ThemeManager.Instance.ThemeChanged += OnThemeManagerChanged;
            
            // 초기 테마 적용
            Dispatcher.UIThread.Post(() =>
            {
                UpdateTheme();
                // 기본적으로 계정관리가 선택된 상태로 시작
                SetActiveMenu("AccountManagement");
            });
        }

        private void OnThemeChanged(object? sender, EventArgs e)
        {
            try
            {
                UpdateTheme();
            }
            catch
            {
                // 테마 변경 중 오류 발생시 무시
            }
        }

        private void OnThemeManagerChanged(object? sender, ThemeManager.ThemeType themeType)
        {
            try
            {
                Dispatcher.UIThread.Post(() =>
                {
                    UpdateTheme();
                });
            }
            catch
            {
                // 테마 변경 중 오류 발생시 무시
            }
        }

        public void UpdateTheme()
        {
            try
            {
                var isDarkTheme = ThemeManager.Instance.IsDarkTheme;
                System.Diagnostics.Debug.WriteLine($"SettingsPage UpdateTheme: isDarkTheme = {isDarkTheme}");
                
                if (isDarkTheme)
                {
                    this.Classes.Add("dark-theme");
                    this.Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#1E1E1E"));
                    
                    // 다크모드 직접 적용
                    Dispatcher.UIThread.Post(() =>
                    {
                        ApplyDarkModeDirectly();
                    });
                }
                else
                {
                    this.Classes.Remove("dark-theme");
                    this.Background = Avalonia.Media.Brushes.White;
                    
                    // 라이트모드 직접 적용
                    Dispatcher.UIThread.Post(() =>
                    {
                        ApplyLightModeDirectly();
                    });
                }

                // UI 강제 업데이트
                InvalidateVisual();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"테마 업데이트 오류: {ex.Message}");
            }
        }

        private void ApplyDarkModeDirectly()
        {
            try
            {
                // 모든 설정 박스 배경색 변경 (계정관리, 마켓주소관리, 프로그램 설정)
                var allSettingsBoxes = this.FindAll<Border>().Where(b => b.Classes.Contains("settings-box"));
                foreach (var settingsBox in allSettingsBoxes)
                {
                    settingsBox.Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#2D2D2D"));
                    settingsBox.BorderBrush = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#FFDAC4"));
                    System.Diagnostics.Debug.WriteLine("다크모드: 설정 박스 배경색 적용됨");
                }

                // 메인 제목 색상 변경 (계정관리)
                var mainTitles = this.FindAll<TextBlock>().Where(t => t.Classes.Contains("main-title"));
                foreach (var title in mainTitles)
                {
                    title.Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#FFDAC4"));
                    System.Diagnostics.Debug.WriteLine("다크모드: 메인 제목 색상 적용됨");
                }

                // 서브 제목 색상 변경 (마켓주소관리, 프로그램 설정)
                var subTitles = this.FindAll<TextBlock>().Where(t => t.Classes.Contains("sub-title"));
                foreach (var title in subTitles)
                {
                    title.Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#AAAAAA"));
                    System.Diagnostics.Debug.WriteLine("다크모드: 서브 제목 색상 적용됨");
                }

                // 메뉴 제목 버튼 색상 변경
                var menuTitleButtons = this.FindAll<Button>().Where(b => b.Classes.Contains("menu-title-button"));
                foreach (var button in menuTitleButtons)
                {
                    if (button.Classes.Contains("main-title"))
                    {
                        button.Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#FFDAC4"));
                    }
                    else if (button.Classes.Contains("sub-title"))
                    {
                        button.Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#AAAAAA"));
                    }
                    System.Diagnostics.Debug.WriteLine("다크모드: 메뉴 제목 버튼 색상 적용됨");
                }

                // 메뉴 항목 버튼 색상 변경
                var menuItems = this.FindAll<Button>().Where(b => b.Classes.Contains("menu-item"));
                foreach (var button in menuItems)
                {
                    button.Foreground = Avalonia.Media.Brushes.White;
                    System.Diagnostics.Debug.WriteLine("다크모드: 메뉴 항목 색상 적용됨");
                }

                // 구분선 색상 변경 (모든 설정 박스)
                var allSeparators = new[] { "AccountSeparator1", "AccountSeparator2", "ProgramSeparator1", "ProgramSeparator2" };
                foreach (var separatorName in allSeparators)
                {
                    var separator = this.FindControl<Border>(separatorName);
                    if (separator != null)
                    {
                        separator.Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#555555"));
                    }
                }

                // 워터마크 색상 변경
                var watermarks = this.FindAll<TextBlock>().Where(t => t.Classes.Contains("watermark"));
                foreach (var watermark in watermarks)
                {
                    watermark.Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#404040"));
                    watermark.Opacity = 0.4;
                    System.Diagnostics.Debug.WriteLine("다크모드: 워터마크 색상 적용됨");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"다크모드 직접 적용 오류: {ex.Message}");
            }
        }

        private void ApplyLightModeDirectly()
        {
            try
            {
                // 모든 설정 박스 배경색 변경 (계정관리, 마켓주소관리, 프로그램 설정)
                var allSettingsBoxes = this.FindAll<Border>().Where(b => b.Classes.Contains("settings-box"));
                foreach (var settingsBox in allSettingsBoxes)
                {
                    settingsBox.Background = Avalonia.Media.Brushes.White;
                    settingsBox.BorderBrush = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#E67E22"));
                    System.Diagnostics.Debug.WriteLine("라이트모드: 설정 박스 배경색 적용됨");
                }

                // 메인 제목 색상 변경 (계정관리)
                var mainTitles = this.FindAll<TextBlock>().Where(t => t.Classes.Contains("main-title"));
                foreach (var title in mainTitles)
                {
                    title.Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#E67E22"));
                    System.Diagnostics.Debug.WriteLine("라이트모드: 메인 제목 색상 적용됨");
                }

                // 서브 제목 색상 변경 (마켓주소관리, 프로그램 설정)
                var subTitles = this.FindAll<TextBlock>().Where(t => t.Classes.Contains("sub-title"));
                foreach (var title in subTitles)
                {
                    title.Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#888888"));
                    System.Diagnostics.Debug.WriteLine("라이트모드: 서브 제목 색상 적용됨");
                }

                // 메뉴 제목 버튼 색상 변경
                var menuTitleButtons = this.FindAll<Button>().Where(b => b.Classes.Contains("menu-title-button"));
                foreach (var button in menuTitleButtons)
                {
                    if (button.Classes.Contains("main-title"))
                    {
                        button.Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#E67E22"));
                    }
                    else if (button.Classes.Contains("sub-title"))
                    {
                        button.Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#888888"));
                    }
                    System.Diagnostics.Debug.WriteLine("라이트모드: 메뉴 제목 버튼 색상 적용됨");
                }

                // 메뉴 항목 버튼 색상 변경
                var menuItems = this.FindAll<Button>().Where(b => b.Classes.Contains("menu-item"));
                foreach (var button in menuItems)
                {
                    button.Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#333333"));
                    System.Diagnostics.Debug.WriteLine("라이트모드: 메뉴 항목 색상 적용됨");
                }

                // 구분선 색상 변경 (모든 설정 박스)
                var allSeparators = new[] { "AccountSeparator1", "AccountSeparator2", "ProgramSeparator1", "ProgramSeparator2" };
                foreach (var separatorName in allSeparators)
                {
                    var separator = this.FindControl<Border>(separatorName);
                    if (separator != null)
                    {
                        separator.Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#E0E0E0"));
                    }
                }

                // 워터마크 색상 변경
                var watermarks = this.FindAll<TextBlock>().Where(t => t.Classes.Contains("watermark"));
                foreach (var watermark in watermarks)
                {
                    watermark.Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#F0F0F0"));
                    watermark.Opacity = 0.3;
                    System.Diagnostics.Debug.WriteLine("라이트모드: 워터마크 색상 적용됨");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"라이트모드 직접 적용 오류: {ex.Message}");
            }
        }

        // 메뉴 제목 클릭 이벤트 핸들러들
        private void OnAccountManagementClick(object? sender, RoutedEventArgs e)
        {
            Console.WriteLine("계정관리 클릭됨");
            SetActiveMenu("AccountManagement");
            // TODO: 계정관리 관련 설정 박스 내용 변경
        }

        private void OnMarketAddressClick(object? sender, RoutedEventArgs e)
        {
            Console.WriteLine("마켓주소관리 클릭됨");
            SetActiveMenu("MarketAddress");
            // TODO: 마켓주소관리 관련 설정 박스 내용 변경
        }

        private void OnProgramSettingsClick(object? sender, RoutedEventArgs e)
        {
            Console.WriteLine("프로그램 설정 클릭됨");
            SetActiveMenu("ProgramSettings");
            // TODO: 프로그램 설정 관련 설정 박스 내용 변경
        }

        // 활성 메뉴 설정 및 간격 조정 + 각 메뉴별 설정 박스 표시
        private void SetActiveMenu(string activeMenu)
        {
            try
            {
                var spacer1 = this.FindControl<Border>("Spacer1");
                var spacer2 = this.FindControl<Border>("Spacer2");
                var accountButton = this.FindControl<Button>("AccountManagementButton");
                var marketButton = this.FindControl<Button>("MarketAddressButton");
                var programButton = this.FindControl<Button>("ProgramSettingsButton");
                
                // 각 메뉴별 설정 박스들
                var accountSettingsBox = this.FindControl<Border>("AccountSettingsBox");
                var marketSettingsBox = this.FindControl<Border>("MarketSettingsBox");
                var programSettingsBox = this.FindControl<Border>("ProgramSettingsBox");

                // 모든 버튼을 서브 제목 스타일로 초기화
                if (accountButton != null)
                {
                    accountButton.Classes.Remove("main-title");
                    accountButton.Classes.Add("sub-title");
                }
                if (marketButton != null)
                {
                    marketButton.Classes.Remove("main-title");
                    marketButton.Classes.Add("sub-title");
                }
                if (programButton != null)
                {
                    programButton.Classes.Remove("main-title");
                    programButton.Classes.Add("sub-title");
                }

                // 간격 초기화
                if (spacer1 != null) spacer1.Height = 0;
                if (spacer2 != null) spacer2.Height = 0;

                // 모든 설정 박스 숨기기
                if (accountSettingsBox != null) accountSettingsBox.IsVisible = false;
                if (marketSettingsBox != null) marketSettingsBox.IsVisible = false;
                if (programSettingsBox != null) programSettingsBox.IsVisible = false;

                // 선택된 메뉴에 따라 스타일, 간격 및 해당 설정 박스 표시
                switch (activeMenu)
                {
                    case "AccountManagement":
                        if (accountButton != null)
                        {
                            accountButton.Classes.Remove("sub-title");
                            accountButton.Classes.Add("main-title");
                        }
                        if (spacer1 != null) spacer1.Height = 120; // 계정관리와 마켓주소관리 사이 큰 간격
                        
                        // 계정관리용 설정 박스 표시
                        if (accountSettingsBox != null)
                        {
                            accountSettingsBox.IsVisible = true;
                            Canvas.SetLeft(accountSettingsBox, 520);
                            Canvas.SetTop(accountSettingsBox, 150);
                            
                            // 즉시 테마 적용
                            ApplyThemeToSettingsBox(accountSettingsBox);
                        }
                        break;

                    case "MarketAddress":
                        if (marketButton != null)
                        {
                            marketButton.Classes.Remove("sub-title");
                            marketButton.Classes.Add("main-title");
                        }
                        if (spacer2 != null) spacer2.Height = 120; // 마켓주소관리와 프로그램 설정 사이 큰 간격
                        
                        // 마켓주소관리용 설정 박스 표시 (16.png와 100% 일치)
                        if (marketSettingsBox != null)
                        {
                            marketSettingsBox.IsVisible = true;
                            Canvas.SetLeft(marketSettingsBox, 520);
                            Canvas.SetTop(marketSettingsBox, 350);
                            
                            // 즉시 테마 적용
                            ApplyThemeToSettingsBox(marketSettingsBox);
                        }
                        break;

                    case "ProgramSettings":
                        if (programButton != null)
                        {
                            programButton.Classes.Remove("sub-title");
                            programButton.Classes.Add("main-title");
                        }
                        if (spacer2 != null) spacer2.Height = 120; // 마켓주소관리와 프로그램 설정 사이 큰 간격
                        
                        // 프로그램 설정용 설정 박스 표시
                        if (programSettingsBox != null)
                        {
                            programSettingsBox.IsVisible = true;
                            Canvas.SetLeft(programSettingsBox, 520);
                            Canvas.SetTop(programSettingsBox, 550);
                            
                            // 즉시 테마 적용
                            ApplyThemeToSettingsBox(programSettingsBox);
                        }
                        break;
                }

                System.Diagnostics.Debug.WriteLine($"활성 메뉴 변경: {activeMenu}, 해당 설정 박스 표시됨");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SetActiveMenu 오류: {ex.Message}");
            }
        }

        // 개별 설정 박스에 현재 테마 적용
        private void ApplyThemeToSettingsBox(Border settingsBox)
        {
            try
            {
                var isDarkTheme = ThemeManager.Instance.IsDarkTheme;
                
                if (isDarkTheme)
                {
                    settingsBox.Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#2D2D2D"));
                    settingsBox.BorderBrush = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#FFDAC4"));
                }
                else
                {
                    settingsBox.Background = Avalonia.Media.Brushes.White;
                    settingsBox.BorderBrush = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#E67E22"));
                }
                
                System.Diagnostics.Debug.WriteLine($"개별 설정 박스 테마 적용: {(isDarkTheme ? "다크" : "라이트")}모드");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"개별 설정 박스 테마 적용 오류: {ex.Message}");
            }
        }
        private void OnNicknameChangeClick(object? sender, RoutedEventArgs e)
        {
            // 닉네임 변경 기능 구현 예정
            Console.WriteLine("닉네임 변경 클릭됨");
            // TODO: 닉네임 변경 다이얼로그 표시
        }

        private void OnApiKeyFindClick(object? sender, RoutedEventArgs e)
        {
            // API키 찾기 기능 구현 예정
            Console.WriteLine("API키 찾기 클릭됨");
            // TODO: API키 찾기 다이얼로그 표시
        }

        private void OnPasswordResetClick(object? sender, RoutedEventArgs e)
        {
            // 비밀번호 찾기 기능 구현 예정
            Console.WriteLine("비밀번호찾기 클릭됨");
            // TODO: 비밀번호 재설정 다이얼로그 표시
        }

        // 마켓주소관리 전용 이벤트 핸들러 (16.png)
        private void OnMarketSetupClick(object? sender, RoutedEventArgs e)
        {
            // 내마켓 설정하기 기능 구현 예정
            Console.WriteLine("내마켓 설정하기 클릭됨");
            // TODO: 마켓 설정 다이얼로그 표시
        }

        // 프로그램 설정 전용 이벤트 핸들러들
        private void OnThemeSettingsClick(object? sender, RoutedEventArgs e)
        {
            // 테마 설정 기능 구현 예정
            Console.WriteLine("테마 설정 클릭됨");
            // TODO: 테마 설정 다이얼로그 표시
        }

        private void OnLanguageSettingsClick(object? sender, RoutedEventArgs e)
        {
            // 언어 설정 기능 구현 예정
            Console.WriteLine("언어 설정 클릭됨");
            // TODO: 언어 설정 다이얼로그 표시
        }

        private void OnNotificationSettingsClick(object? sender, RoutedEventArgs e)
        {
            // 알림 설정 기능 구현 예정
            Console.WriteLine("알림 설정 클릭됨");
            // TODO: 알림 설정 다이얼로그 표시
        }
    }
}
