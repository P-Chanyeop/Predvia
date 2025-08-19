using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.Input;
using Avalonia.LogicalTree;
using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Gumaedaehang.Services;

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
                    ApplyDarkModeDirectly();
                }
                else
                {
                    this.Classes.Remove("dark-theme");
                    ApplyLightModeDirectly();
                }
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
                // 모든 설정 박스 배경색 변경 (계정관리, 마켓주소관리)
                var allSettingsBoxes = this.GetLogicalDescendants().OfType<Border>().Where(b => b.Classes.Contains("settings-box"));
                foreach (var settingsBox in allSettingsBoxes)
                {
                    settingsBox.Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#2D2D2D"));
                    settingsBox.BorderBrush = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#FFDAC4"));
                    System.Diagnostics.Debug.WriteLine("다크모드: 설정 박스 배경색 적용됨");
                }

                // 전체 화면 오버레이 페이지들 다크모드 적용
                ApplyDarkModeToOverlayPages();

                // 메인 제목 색상 변경 (계정관리)
                var mainTitles = this.GetLogicalDescendants().OfType<TextBlock>().Where(t => t.Classes.Contains("main-title"));
                foreach (var title in mainTitles)
                {
                    title.Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#FFDAC4"));
                    System.Diagnostics.Debug.WriteLine("다크모드: 메인 제목 색상 적용됨");
                }

                // 서브 제목 색상 변경 (마켓주소관리)
                var subTitles = this.GetLogicalDescendants().OfType<TextBlock>().Where(t => t.Classes.Contains("sub-title"));
                foreach (var title in subTitles)
                {
                    title.Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#AAAAAA"));
                    System.Diagnostics.Debug.WriteLine("다크모드: 서브 제목 색상 적용됨");
                }

                // 메뉴 제목 버튼 색상 변경
                var menuTitleButtons = this.GetLogicalDescendants().OfType<Button>().Where(b => b.Classes.Contains("menu-title-button"));
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

                // 메뉴 항목 버튼 색상 변경 (모든 설정 박스 내부)
                var menuItems = this.GetLogicalDescendants().OfType<Button>().Where(b => b.Classes.Contains("menu-item"));
                foreach (var button in menuItems)
                {
                    button.Foreground = Avalonia.Media.Brushes.White;
                    System.Diagnostics.Debug.WriteLine("다크모드: 메뉴 항목 색상 적용됨");
                }

                // 워터마크 색상 변경
                var watermarks = this.GetLogicalDescendants().OfType<TextBlock>().Where(t => t.Classes.Contains("watermark"));
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
                // 모든 설정 박스 배경색 변경 (계정관리, 마켓주소관리)
                var allSettingsBoxes = this.GetLogicalDescendants().OfType<Border>().Where(b => b.Classes.Contains("settings-box"));
                foreach (var settingsBox in allSettingsBoxes)
                {
                    settingsBox.Background = Avalonia.Media.Brushes.White;
                    settingsBox.BorderBrush = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#E67E22"));
                    System.Diagnostics.Debug.WriteLine("라이트모드: 설정 박스 배경색 적용됨");
                }

                // 전체 화면 오버레이 페이지들 라이트모드 적용
                ApplyLightModeToOverlayPages();

                // 메인 제목 색상 변경 (계정관리)
                var mainTitles = this.GetLogicalDescendants().OfType<TextBlock>().Where(t => t.Classes.Contains("main-title"));
                foreach (var title in mainTitles)
                {
                    title.Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#E67E22"));
                    System.Diagnostics.Debug.WriteLine("라이트모드: 메인 제목 색상 적용됨");
                }

                // 서브 제목 색상 변경 (마켓주소관리)
                var subTitles = this.GetLogicalDescendants().OfType<TextBlock>().Where(t => t.Classes.Contains("sub-title"));
                foreach (var title in subTitles)
                {
                    title.Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#666666"));
                    System.Diagnostics.Debug.WriteLine("라이트모드: 서브 제목 색상 적용됨");
                }

                // 메뉴 제목 버튼 색상 변경
                var menuTitleButtons = this.GetLogicalDescendants().OfType<Button>().Where(b => b.Classes.Contains("menu-title-button"));
                foreach (var button in menuTitleButtons)
                {
                    if (button.Classes.Contains("main-title"))
                    {
                        button.Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#E67E22"));
                    }
                    else if (button.Classes.Contains("sub-title"))
                    {
                        button.Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#666666"));
                    }
                    System.Diagnostics.Debug.WriteLine("라이트모드: 메뉴 제목 버튼 색상 적용됨");
                }

                // 메뉴 항목 버튼 색상 변경
                var menuItems = this.GetLogicalDescendants().OfType<Button>().Where(b => b.Classes.Contains("menu-item"));
                foreach (var button in menuItems)
                {
                    button.Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#333333"));
                    System.Diagnostics.Debug.WriteLine("라이트모드: 메뉴 항목 색상 적용됨");
                }

                // 워터마크 색상 변경
                var watermarks = this.GetLogicalDescendants().OfType<TextBlock>().Where(t => t.Classes.Contains("watermark"));
                foreach (var watermark in watermarks)
                {
                    watermark.Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#E0E0E0"));
                    watermark.Opacity = 0.3;
                    System.Diagnostics.Debug.WriteLine("라이트모드: 워터마크 색상 적용됨");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"라이트모드 직접 적용 오류: {ex.Message}");
            }
        }

        // 전체 화면 오버레이 페이지들에 다크모드 적용
        private void ApplyDarkModeToOverlayPages()
        {
            try
            {
                // 닉네임 변경 페이지 다크모드 적용
                var nicknamePage = this.FindControl<Border>("NicknameChangePage");
                if (nicknamePage != null)
                {
                    nicknamePage.Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#2D2D2D"));
                    nicknamePage.BorderBrush = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#FFDAC4"));
                    
                    // 닉네임 페이지 내부 요소들 다크모드 적용
                    ApplyDarkModeToPageElements(nicknamePage);
                    System.Diagnostics.Debug.WriteLine("다크모드: 닉네임 변경 페이지 적용됨");
                }

                // 마켓 설정 페이지 다크모드 적용
                var marketPage = this.FindControl<Border>("MarketSetupPage");
                if (marketPage != null)
                {
                    marketPage.Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#2D2D2D"));
                    marketPage.BorderBrush = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#FFDAC4"));
                    
                    // 마켓 설정 페이지 내부 요소들 다크모드 적용
                    ApplyDarkModeToPageElements(marketPage);
                    System.Diagnostics.Debug.WriteLine("다크모드: 마켓 설정 페이지 적용됨");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"오버레이 페이지 다크모드 적용 오류: {ex.Message}");
            }
        }

        // 전체 화면 오버레이 페이지들에 라이트모드 적용
        private void ApplyLightModeToOverlayPages()
        {
            try
            {
                // 닉네임 변경 페이지 라이트모드 적용
                var nicknamePage = this.FindControl<Border>("NicknameChangePage");
                if (nicknamePage != null)
                {
                    nicknamePage.Background = Avalonia.Media.Brushes.White;
                    nicknamePage.BorderBrush = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#E67E22"));
                    
                    // 닉네임 페이지 내부 요소들 라이트모드 적용
                    ApplyLightModeToPageElements(nicknamePage);
                    System.Diagnostics.Debug.WriteLine("라이트모드: 닉네임 변경 페이지 적용됨");
                }

                // 마켓 설정 페이지 라이트모드 적용
                var marketPage = this.FindControl<Border>("MarketSetupPage");
                if (marketPage != null)
                {
                    marketPage.Background = Avalonia.Media.Brushes.White;
                    marketPage.BorderBrush = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#E67E22"));
                    
                    // 마켓 설정 페이지 내부 요소들 라이트모드 적용
                    ApplyLightModeToPageElements(marketPage);
                    System.Diagnostics.Debug.WriteLine("라이트모드: 마켓 설정 페이지 적용됨");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"오버레이 페이지 라이트모드 적용 오류: {ex.Message}");
            }
        }

        // 페이지 내부 요소들에 다크모드 적용
        private void ApplyDarkModeToPageElements(Border page)
        {
            try
            {
                // 모든 TextBlock 찾기 (제목 제외)
                var textBlocks = page.GetLogicalDescendants().OfType<TextBlock>();
                foreach (var textBlock in textBlocks)
                {
                    // 제목은 주황색 유지, 나머지는 흰색
                    if (textBlock.FontSize >= 40) // 제목 크기
                    {
                        textBlock.Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#FFDAC4"));
                    }
                    else
                    {
                        textBlock.Foreground = Avalonia.Media.Brushes.White;
                    }
                }

                // 모든 TextBox 찾기
                var textBoxes = page.GetLogicalDescendants().OfType<TextBox>();
                foreach (var textBox in textBoxes)
                {
                    textBox.Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#4A4A4A"));
                    textBox.Foreground = Avalonia.Media.Brushes.White;
                    textBox.BorderBrush = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#FFDAC4"));
                }

                // 모든 ComboBox 찾기
                var comboBoxes = page.GetLogicalDescendants().OfType<ComboBox>();
                foreach (var comboBox in comboBoxes)
                {
                    comboBox.Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#4A4A4A"));
                    comboBox.Foreground = Avalonia.Media.Brushes.White;
                    comboBox.BorderBrush = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#FFDAC4"));
                }

                // 모든 Button 찾기
                var buttons = page.GetLogicalDescendants().OfType<Button>();
                foreach (var button in buttons)
                {
                    // 취소 버튼은 회색, 나머지는 연한 주황색
                    if (button.Content?.ToString() == "취소")
                    {
                        button.Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#666666"));
                        button.Foreground = Avalonia.Media.Brushes.White;
                    }
                    else
                    {
                        button.Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#FFDAC4"));
                        button.Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#333333"));
                    }
                }

                System.Diagnostics.Debug.WriteLine("페이지 내부 요소들 다크모드 적용 완료");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"페이지 내부 요소 다크모드 적용 오류: {ex.Message}");
            }
        }

        // 페이지 내부 요소들에 라이트모드 적용
        private void ApplyLightModeToPageElements(Border page)
        {
            try
            {
                // 모든 TextBlock 찾기
                var textBlocks = page.GetLogicalDescendants().OfType<TextBlock>();
                foreach (var textBlock in textBlocks)
                {
                    // 제목은 주황색, 나머지는 검은색
                    if (textBlock.FontSize >= 40) // 제목 크기
                    {
                        textBlock.Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#E67E22"));
                    }
                    else
                    {
                        textBlock.Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#333333"));
                    }
                }

                // 모든 TextBox 찾기
                var textBoxes = page.GetLogicalDescendants().OfType<TextBox>();
                foreach (var textBox in textBoxes)
                {
                    // 읽기 전용은 회색 배경, 편집 가능한 것은 연한 주황색 배경
                    if (textBox.IsReadOnly)
                    {
                        textBox.Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#F5F5F5"));
                        textBox.BorderBrush = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#E0E0E0"));
                    }
                    else
                    {
                        textBox.Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#FFDAC4"));
                        textBox.BorderBrush = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#E67E22"));
                    }
                    textBox.Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#333333"));
                }

                // 모든 ComboBox 찾기
                var comboBoxes = page.GetLogicalDescendants().OfType<ComboBox>();
                foreach (var comboBox in comboBoxes)
                {
                    comboBox.Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#FFDAC4"));
                    comboBox.Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#333333"));
                    comboBox.BorderBrush = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#E67E22"));
                }

                // 모든 Button 찾기
                var buttons = page.GetLogicalDescendants().OfType<Button>();
                foreach (var button in buttons)
                {
                    // 취소 버튼은 회색, 나머지는 주황색
                    if (button.Content?.ToString() == "취소")
                    {
                        button.Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#CCCCCC"));
                        button.Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#333333"));
                    }
                    else
                    {
                        button.Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#E67E22"));
                        button.Foreground = Avalonia.Media.Brushes.White;
                    }
                }

                System.Diagnostics.Debug.WriteLine("페이지 내부 요소들 라이트모드 적용 완료");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"페이지 내부 요소 라이트모드 적용 오류: {ex.Message}");
            }
        }

        // SetActiveMenu 메서드
        private void SetActiveMenu(string activeMenu)
        {
            try
            {
                var spacer1 = this.FindControl<Border>("Spacer1");
                var spacer2 = this.FindControl<Border>("Spacer2");
                var accountButton = this.FindControl<Button>("AccountManagementButton");
                var marketButton = this.FindControl<Button>("MarketAddressButton");
                
                // 각 메뉴별 설정 박스들
                var accountSettingsBox = this.FindControl<Border>("AccountSettingsBox");
                var marketSettingsBox = this.FindControl<Border>("MarketSettingsBox");

                // 모든 간격 초기화
                if (spacer1 != null) spacer1.Height = 0;
                if (spacer2 != null) spacer2.Height = 0;

                // 모든 버튼을 서브 제목으로 초기화
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

                // 모든 설정 박스 숨기기
                if (accountSettingsBox != null) accountSettingsBox.IsVisible = false;
                if (marketSettingsBox != null) marketSettingsBox.IsVisible = false;

                // 선택된 메뉴에 따라 처리
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

        // 메뉴 제목 클릭 이벤트 핸들러들
        private void OnAccountManagementClick(object? sender, RoutedEventArgs e)
        {
            Console.WriteLine("계정관리 클릭됨");
            SetActiveMenu("AccountManagement");
        }

        private void OnMarketAddressClick(object? sender, RoutedEventArgs e)
        {
            Console.WriteLine("마켓주소관리 클릭됨");
            SetActiveMenu("MarketAddress");
        }

        // 닉네임 변경 페이지 전환
        private void OnNicknameChangeClick(object? sender, RoutedEventArgs e)
        {
            Console.WriteLine("닉네임 변경 클릭됨");
            System.Diagnostics.Debug.WriteLine("OnNicknameChangeClick 호출됨");
            
            try
            {
                // 모든 설정 박스 숨기기
                HideAllSettingsBoxes();
                
                // 워터마크 숨기기
                var watermark = this.FindControl<TextBlock>("PredviaWatermark");
                if (watermark != null) watermark.IsVisible = false;
                
                // 다른 페이지들 숨기기
                var marketPage = this.FindControl<Border>("MarketSetupPage");
                if (marketPage != null) marketPage.IsVisible = false;
                
                // 닉네임 변경 페이지 표시
                var nicknamePage = this.FindControl<Border>("NicknameChangePage");
                var currentNicknameDisplay = this.FindControl<TextBox>("CurrentNicknameDisplay");
                var newNicknameInput = this.FindControl<TextBox>("NewNicknameInput");
                var errorMessage = this.FindControl<TextBlock>("NicknameErrorMessage");
                
                if (nicknamePage != null)
                {
                    nicknamePage.IsVisible = true;
                    
                    // 현재 테마 즉시 적용
                    var isDarkTheme = ThemeManager.Instance.IsDarkTheme;
                    if (isDarkTheme)
                    {
                        ApplyDarkModeToPageElements(nicknamePage);
                    }
                    else
                    {
                        ApplyLightModeToPageElements(nicknamePage);
                    }
                    
                    // 현재 닉네임 설정
                    if (currentNicknameDisplay != null && AuthManager.Instance.IsLoggedIn)
                    {
                        currentNicknameDisplay.Text = AuthManager.Instance.Username ?? "사용자";
                    }
                    
                    // 입력 필드 초기화
                    if (newNicknameInput != null)
                    {
                        newNicknameInput.Text = "";
                        newNicknameInput.Focus();
                    }
                    
                    // 오류 메시지 숨기기
                    if (errorMessage != null)
                    {
                        errorMessage.IsVisible = false;
                    }
                    
                    System.Diagnostics.Debug.WriteLine("닉네임 변경 페이지 표시됨");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"닉네임 변경 페이지 표시 오류: {ex.Message}");
                Console.WriteLine($"닉네임 변경 오류: {ex.Message}");
            }
        }

        // 마켓 설정 페이지 전환
        private void OnMarketSetupClick(object? sender, RoutedEventArgs e)
        {
            Console.WriteLine("내마켓 설정하기 클릭됨");
            System.Diagnostics.Debug.WriteLine("OnMarketSetupClick 호출됨");
            
            try
            {
                // 모든 설정 박스 숨기기
                HideAllSettingsBoxes();
                
                // 워터마크 숨기기
                var watermark = this.FindControl<TextBlock>("PredviaWatermark");
                if (watermark != null) watermark.IsVisible = false;
                
                // 다른 페이지들 숨기기
                var nicknamePage = this.FindControl<Border>("NicknameChangePage");
                if (nicknamePage != null) nicknamePage.IsVisible = false;
                
                // 마켓 설정 페이지 표시
                var marketPage = this.FindControl<Border>("MarketSetupPage");
                var marketNameInput = this.FindControl<TextBox>("MarketNameInput");
                var successMessage = this.FindControl<TextBlock>("MarketSuccessMessage");
                var errorMessage = this.FindControl<TextBlock>("MarketErrorMessage");
                
                if (marketPage != null)
                {
                    marketPage.IsVisible = true;
                    
                    // 현재 테마 즉시 적용
                    var isDarkTheme = ThemeManager.Instance.IsDarkTheme;
                    if (isDarkTheme)
                    {
                        ApplyDarkModeToPageElements(marketPage);
                    }
                    else
                    {
                        ApplyLightModeToPageElements(marketPage);
                    }
                    
                    // 입력 필드 포커스
                    if (marketNameInput != null)
                    {
                        marketNameInput.Focus();
                    }
                    
                    // 메시지 숨기기
                    if (successMessage != null) successMessage.IsVisible = false;
                    if (errorMessage != null) errorMessage.IsVisible = false;
                    
                    System.Diagnostics.Debug.WriteLine("마켓 설정 페이지 표시됨");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"마켓 설정 페이지 표시 오류: {ex.Message}");
                Console.WriteLine($"마켓 설정 오류: {ex.Message}");
            }
        }

        private void HideAllSettingsBoxes()
        {
            try
            {
                var accountBox = this.FindControl<Border>("AccountSettingsBox");
                var marketBox = this.FindControl<Border>("MarketSettingsBox");
                
                if (accountBox != null) accountBox.IsVisible = false;
                if (marketBox != null) marketBox.IsVisible = false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"설정 박스 숨기기 오류: {ex.Message}");
            }
        }

        // 닉네임 저장
        private void OnNicknameSaveClick(object? sender, RoutedEventArgs e)
        {
            try
            {
                var newNicknameInput = this.FindControl<TextBox>("NewNicknameInput");
                var currentNicknameDisplay = this.FindControl<TextBox>("CurrentNicknameDisplay");
                var errorMessage = this.FindControl<TextBlock>("NicknameErrorMessage");
                var nicknamePage = this.FindControl<Border>("NicknameChangePage");
                
                if (newNicknameInput == null || currentNicknameDisplay == null || errorMessage == null)
                    return;

                var newNickname = newNicknameInput.Text?.Trim();

                // 유효성 검사
                if (string.IsNullOrWhiteSpace(newNickname))
                {
                    ShowNicknameError("새 닉네임을 입력해주세요.");
                    return;
                }

                if (newNickname.Length < 2)
                {
                    ShowNicknameError("닉네임은 2글자 이상이어야 합니다.");
                    return;
                }

                if (newNickname.Length > 20)
                {
                    ShowNicknameError("닉네임은 20글자 이하여야 합니다.");
                    return;
                }

                if (newNickname == currentNicknameDisplay.Text)
                {
                    ShowNicknameError("현재 닉네임과 동일합니다.");
                    return;
                }

                // 특수문자 검사
                if (!System.Text.RegularExpressions.Regex.IsMatch(newNickname, @"^[가-힣a-zA-Z0-9]+$"))
                {
                    ShowNicknameError("닉네임은 한글, 영문, 숫자만 사용할 수 있습니다.");
                    return;
                }

                // 성공 - 닉네임 업데이트
                currentNicknameDisplay.Text = newNickname;
                newNicknameInput.Text = "";
                
                // TODO: 실제 서버 API 호출
                System.Diagnostics.Debug.WriteLine($"닉네임 변경 성공: {newNickname}");
                
                // 페이지 닫기
                if (nicknamePage != null) nicknamePage.IsVisible = false;
                
                // 워터마크 다시 표시
                var watermark = this.FindControl<TextBlock>("PredviaWatermark");
                if (watermark != null) watermark.IsVisible = true;
                
                Console.WriteLine($"닉네임이 '{newNickname}'으로 변경되었습니다.");
            }
            catch (Exception ex)
            {
                ShowNicknameError($"닉네임 변경 중 오류가 발생했습니다: {ex.Message}");
            }
        }

        // 닉네임 취소
        private void OnNicknameCancelClick(object? sender, RoutedEventArgs e)
        {
            try
            {
                // 닉네임 변경 페이지 숨기기
                var nicknamePage = this.FindControl<Border>("NicknameChangePage");
                if (nicknamePage != null) nicknamePage.IsVisible = false;
                
                // 워터마크 다시 표시
                var watermark = this.FindControl<TextBlock>("PredviaWatermark");
                if (watermark != null) watermark.IsVisible = true;
                
                System.Diagnostics.Debug.WriteLine("닉네임 변경 페이지 취소됨");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"닉네임 취소 오류: {ex.Message}");
            }
        }

        // 마켓 설정 저장
        private void OnMarketSaveClick(object? sender, RoutedEventArgs e)
        {
            try
            {
                var marketNameInput = this.FindControl<TextBox>("MarketNameInput");
                var categorySelect = this.FindControl<ComboBox>("MarketCategorySelect");
                var naverUrlInput = this.FindControl<TextBox>("NaverStoreUrlInput");
                var shippingFeeInput = this.FindControl<TextBox>("ShippingFeeInput");
                var freeShippingMinInput = this.FindControl<TextBox>("FreeShippingMinInput");
                var successMessage = this.FindControl<TextBlock>("MarketSuccessMessage");
                var errorMessage = this.FindControl<TextBlock>("MarketErrorMessage");
                var marketPage = this.FindControl<Border>("MarketSetupPage");

                // 유효성 검사
                if (string.IsNullOrWhiteSpace(marketNameInput?.Text))
                {
                    ShowMarketError("마켓 이름을 입력해주세요.");
                    marketNameInput?.Focus();
                    return;
                }

                if (categorySelect?.SelectedItem == null)
                {
                    ShowMarketError("마켓 카테고리를 선택해주세요.");
                    return;
                }

                // 배송비 검증
                if (!int.TryParse(shippingFeeInput?.Text, out var shippingFee) || shippingFee < 0)
                {
                    ShowMarketError("올바른 배송비를 입력해주세요. (0 이상의 숫자)");
                    shippingFeeInput?.Focus();
                    return;
                }

                // 무료배송 최소금액 검증
                if (!int.TryParse(freeShippingMinInput?.Text, out var freeShippingMin) || freeShippingMin < 0)
                {
                    ShowMarketError("올바른 무료배송 최소 주문금액을 입력해주세요. (0 이상의 숫자)");
                    freeShippingMinInput?.Focus();
                    return;
                }

                // 네이버 URL 검증 (선택사항)
                var naverUrl = naverUrlInput?.Text?.Trim();
                if (!string.IsNullOrEmpty(naverUrl))
                {
                    if (!IsValidUrl(naverUrl) || !naverUrl.Contains("smartstore.naver.com"))
                    {
                        ShowMarketError("올바른 네이버 스마트스토어 URL을 입력해주세요.");
                        naverUrlInput?.Focus();
                        return;
                    }
                }

                // 성공 - 설정 저장
                var marketSettings = new
                {
                    MarketName = marketNameInput?.Text?.Trim(),
                    Category = (categorySelect?.SelectedItem as ComboBoxItem)?.Content?.ToString(),
                    NaverStoreUrl = naverUrl,
                    ShippingFee = shippingFee,
                    FreeShippingMin = freeShippingMin
                };

                // TODO: 실제 저장 로직 구현
                System.Diagnostics.Debug.WriteLine($"마켓 설정 저장: {System.Text.Json.JsonSerializer.Serialize(marketSettings)}");

                // 성공 메시지 표시
                ShowMarketSuccess("마켓 설정이 성공적으로 저장되었습니다!");
                
                // 2초 후 페이지 닫기
                Dispatcher.UIThread.Post(async () =>
                {
                    await System.Threading.Tasks.Task.Delay(2000);
                    if (marketPage != null) marketPage.IsVisible = false;
                    
                    // 워터마크 다시 표시
                    var watermark = this.FindControl<TextBlock>("PredviaWatermark");
                    if (watermark != null) watermark.IsVisible = true;
                });
            }
            catch (Exception ex)
            {
                ShowMarketError($"설정 저장 중 오류가 발생했습니다: {ex.Message}");
            }
        }

        // 마켓 설정 취소
        private void OnMarketCancelClick(object? sender, RoutedEventArgs e)
        {
            try
            {
                // 마켓 설정 페이지 숨기기
                var marketPage = this.FindControl<Border>("MarketSetupPage");
                if (marketPage != null) marketPage.IsVisible = false;
                
                // 워터마크 다시 표시
                var watermark = this.FindControl<TextBlock>("PredviaWatermark");
                if (watermark != null) watermark.IsVisible = true;
                
                System.Diagnostics.Debug.WriteLine("마켓 설정 페이지 취소됨");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"마켓 설정 취소 오류: {ex.Message}");
            }
        }

        // 헬퍼 메서드들
        private void ShowNicknameError(string message)
        {
            var errorMessage = this.FindControl<TextBlock>("NicknameErrorMessage");
            if (errorMessage != null)
            {
                errorMessage.Text = message;
                errorMessage.IsVisible = true;
                
                // 3초 후 오류 메시지 숨기기
                Dispatcher.UIThread.Post(async () =>
                {
                    await System.Threading.Tasks.Task.Delay(3000);
                    if (errorMessage != null)
                    {
                        errorMessage.IsVisible = false;
                    }
                });
            }
        }

        private void ShowMarketSuccess(string message)
        {
            var successMessage = this.FindControl<TextBlock>("MarketSuccessMessage");
            var errorMessage = this.FindControl<TextBlock>("MarketErrorMessage");
            
            if (successMessage != null && errorMessage != null)
            {
                errorMessage.IsVisible = false;
                successMessage.Text = message;
                successMessage.IsVisible = true;
            }
        }

        private void ShowMarketError(string message)
        {
            var errorMessage = this.FindControl<TextBlock>("MarketErrorMessage");
            var successMessage = this.FindControl<TextBlock>("MarketSuccessMessage");
            
            if (errorMessage != null && successMessage != null)
            {
                successMessage.IsVisible = false;
                errorMessage.Text = message;
                errorMessage.IsVisible = true;
                
                // 5초 후 오류 메시지 숨기기
                Dispatcher.UIThread.Post(async () =>
                {
                    await System.Threading.Tasks.Task.Delay(5000);
                    if (errorMessage != null)
                    {
                        errorMessage.IsVisible = false;
                    }
                });
            }
        }

        private bool IsValidUrl(string url)
        {
            try
            {
                var uri = new Uri(url);
                return uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps;
            }
            catch
            {
                return false;
            }
        }
    }
}
