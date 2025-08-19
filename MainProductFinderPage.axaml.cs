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
    public partial class MainProductFinderPage : UserControl
    {
        public MainProductFinderPage()
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
                System.Diagnostics.Debug.WriteLine($"MainProductFinderPage UpdateTheme: isDarkTheme = {isDarkTheme}");
                
                if (isDarkTheme)
                {
                    this.Classes.Add("dark-theme");
                    this.Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#1E1E1E"));
                    
                    // 모든 Border 요소를 직접 찾아서 다크모드 적용
                    Dispatcher.UIThread.Post(() =>
                    {
                        ApplyDarkModeDirectly();
                    });
                }
                else
                {
                    this.Classes.Remove("dark-theme");
                    this.Background = Avalonia.Media.Brushes.White;
                    
                    // 모든 Border 요소를 직접 찾아서 라이트모드 적용
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
                // 모든 상품 카드 Border 찾기 (테두리 제거, 배경색만 적용)
                var productCards = this.FindAll<Border>().Where(b => b.Classes.Contains("product-card"));
                foreach (var card in productCards)
                {
                    card.Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#1E1E1E"));
                    card.BorderBrush = Avalonia.Media.Brushes.Transparent;
                    card.BorderThickness = new Avalonia.Thickness(0);
                    System.Diagnostics.Debug.WriteLine("다크모드: 상품카드 배경색 적용됨 (테두리 제거)");
                }

                // 사이드바 Border 찾기 (toggle-sidebar 클래스)
                var sidebarBorders = this.FindAll<Border>().Where(b => b.Classes.Contains("toggle-sidebar"));
                foreach (var sidebar in sidebarBorders)
                {
                    sidebar.Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#2D2D2D"));
                    sidebar.BorderBrush = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#FFDAC4"));
                    sidebar.BorderThickness = new Avalonia.Thickness(2);
                    System.Diagnostics.Debug.WriteLine("다크모드: 사이드바 배경색 적용됨");
                }

                // 사이드바 텍스트 색상 변경
                var sidebarTitleTexts = this.FindAll<TextBlock>().Where(t => t.Classes.Contains("sidebar-title-text"));
                foreach (var text in sidebarTitleTexts)
                {
                    text.Foreground = Avalonia.Media.Brushes.White;
                    System.Diagnostics.Debug.WriteLine("다크모드: 사이드바 제목 텍스트 색상 적용됨");
                }

                var sidebarDescTexts = this.FindAll<TextBlock>().Where(t => t.Classes.Contains("sidebar-desc-text"));
                foreach (var text in sidebarDescTexts)
                {
                    text.Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#CCCCCC"));
                    System.Diagnostics.Debug.WriteLine("다크모드: 사이드바 설명 텍스트 색상 적용됨");
                }

                // 사이드바 버튼 색상 변경
                var sidebarButtons = this.FindAll<Button>().Where(b => b.Content?.ToString() == "찾기");
                foreach (var button in sidebarButtons)
                {
                    button.Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#FFDAC4"));
                    button.Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#333333"));
                    System.Diagnostics.Debug.WriteLine("다크모드: 사이드바 버튼 색상 적용됨");
                }

                // 사이드바 토글 버튼 색상 변경
                var toggleButton = this.FindControl<Button>("SidebarToggleButton");
                if (toggleButton != null)
                {
                    toggleButton.Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#FFDAC4"));
                    System.Diagnostics.Debug.WriteLine("다크모드: 토글 버튼 색상 적용됨");
                }

                // 사이드바 닫기 버튼 색상 변경
                var closeButton = this.FindControl<Button>("SidebarCloseButton");
                if (closeButton != null)
                {
                    closeButton.Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#FFDAC4"));
                    System.Diagnostics.Debug.WriteLine("다크모드: 닫기 버튼 색상 적용됨");
                }

                // 모든 정보 패널 Border 찾기
                var infoPanels = this.FindAll<Border>().Where(b => b.Classes.Contains("info-panel"));
                foreach (var panel in infoPanels)
                {
                    panel.Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#2D2D2D"));
                    panel.BorderBrush = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#444444"));
                    panel.BorderThickness = new Avalonia.Thickness(1);
                    panel.Margin = new Avalonia.Thickness(0, 0, 20, 0); // 버튼과 정렬
                }

                // 모든 이미지 플레이스홀더 찾기
                var placeholders = this.FindAll<Border>().Where(b => b.Classes.Contains("image-placeholder"));
                foreach (var placeholder in placeholders)
                {
                    placeholder.Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#4A4A4A"));
                }

                // 모든 텍스트 색상 변경
                var titleTexts = this.FindAll<TextBlock>().Where(t => t.Classes.Contains("product-title"));
                foreach (var text in titleTexts)
                {
                    text.Foreground = Avalonia.Media.Brushes.White;
                }

                var labelTexts = this.FindAll<TextBlock>().Where(t => t.Classes.Contains("info-label"));
                foreach (var text in labelTexts)
                {
                    text.Foreground = Avalonia.Media.Brushes.White;
                }

                var valueTexts = this.FindAll<TextBlock>().Where(t => t.Classes.Contains("info-value"));
                foreach (var text in valueTexts)
                {
                    text.Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#FFDAC4"));
                }

                var placeholderTexts = this.FindAll<TextBlock>().Where(t => t.Classes.Contains("placeholder-text"));
                foreach (var text in placeholderTexts)
                {
                    text.Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#CCCCCC"));
                }

                // 모든 액션 버튼 색상 변경 (다크모드)
                var actionButtons = this.FindAll<Button>().Where(b => b.Classes.Contains("action-button"));
                foreach (var button in actionButtons)
                {
                    button.Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#FFDAC4"));
                    button.Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#333333"));
                    System.Diagnostics.Debug.WriteLine("다크모드: 액션 버튼 색상 적용됨");
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
                // 모든 상품 카드 Border 찾기 (테두리 제거, 배경색만 적용)
                var productCards = this.FindAll<Border>().Where(b => b.Classes.Contains("product-card"));
                foreach (var card in productCards)
                {
                    card.Background = Avalonia.Media.Brushes.White;
                    card.BorderBrush = Avalonia.Media.Brushes.Transparent;
                    card.BorderThickness = new Avalonia.Thickness(0);
                }

                // 사이드바 Border 찾기 (toggle-sidebar 클래스)
                var sidebarBorders = this.FindAll<Border>().Where(b => b.Classes.Contains("toggle-sidebar"));
                foreach (var sidebar in sidebarBorders)
                {
                    sidebar.Background = Avalonia.Media.Brushes.White;
                    sidebar.BorderBrush = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#E67E22"));
                    sidebar.BorderThickness = new Avalonia.Thickness(2);
                    System.Diagnostics.Debug.WriteLine("라이트모드: 사이드바 배경색 적용됨");
                }

                // 사이드바 텍스트 색상 변경
                var sidebarTitleTexts = this.FindAll<TextBlock>().Where(t => t.Classes.Contains("sidebar-title-text"));
                foreach (var text in sidebarTitleTexts)
                {
                    text.Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#333333"));
                    System.Diagnostics.Debug.WriteLine("라이트모드: 사이드바 제목 텍스트 색상 적용됨");
                }

                var sidebarDescTexts = this.FindAll<TextBlock>().Where(t => t.Classes.Contains("sidebar-desc-text"));
                foreach (var text in sidebarDescTexts)
                {
                    text.Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#444444"));
                    System.Diagnostics.Debug.WriteLine("라이트모드: 사이드바 설명 텍스트 색상 적용됨");
                }

                // 사이드바 버튼 색상 변경
                var sidebarButtons = this.FindAll<Button>().Where(b => b.Content?.ToString() == "찾기");
                foreach (var button in sidebarButtons)
                {
                    button.Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#E67E22"));
                    button.Foreground = Avalonia.Media.Brushes.White;
                    System.Diagnostics.Debug.WriteLine("라이트모드: 사이드바 버튼 색상 적용됨");
                }

                // 사이드바 토글 버튼 색상 복원
                var toggleButton = this.FindControl<Button>("SidebarToggleButton");
                if (toggleButton != null)
                {
                    toggleButton.Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#DF6C29"));
                    System.Diagnostics.Debug.WriteLine("라이트모드: 토글 버튼 색상 복원됨");
                }

                // 사이드바 닫기 버튼 색상 복원
                var closeButton = this.FindControl<Button>("SidebarCloseButton");
                if (closeButton != null)
                {
                    closeButton.Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#DF6C29"));
                    System.Diagnostics.Debug.WriteLine("라이트모드: 닫기 버튼 색상 복원됨");
                }

                // 모든 정보 패널 Border 찾기 (라이트모드 주황색 테두리)
                var infoPanels = this.FindAll<Border>().Where(b => b.Classes.Contains("info-panel"));
                foreach (var panel in infoPanels)
                {
                    panel.Background = Avalonia.Media.Brushes.White;
                    panel.BorderBrush = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#E67E22"));
                    panel.BorderThickness = new Avalonia.Thickness(2);
                    panel.Margin = new Avalonia.Thickness(0, 0, 20, 0); // 버튼과 정렬
                }

                // 모든 이미지 플레이스홀더 찾기
                var placeholders = this.FindAll<Border>().Where(b => b.Classes.Contains("image-placeholder"));
                foreach (var placeholder in placeholders)
                {
                    placeholder.Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#F0F0F0"));
                }

                // 모든 텍스트 색상 변경
                var titleTexts = this.FindAll<TextBlock>().Where(t => t.Classes.Contains("product-title"));
                foreach (var text in titleTexts)
                {
                    text.Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#333333"));
                }

                var labelTexts = this.FindAll<TextBlock>().Where(t => t.Classes.Contains("info-label"));
                foreach (var text in labelTexts)
                {
                    text.Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#333333"));
                }

                var valueTexts = this.FindAll<TextBlock>().Where(t => t.Classes.Contains("info-value"));
                foreach (var text in valueTexts)
                {
                    text.Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#DF6C29"));
                }

                var placeholderTexts = this.FindAll<TextBlock>().Where(t => t.Classes.Contains("placeholder-text"));
                foreach (var text in placeholderTexts)
                {
                    text.Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#999999"));
                }

                // 모든 액션 버튼 색상 변경 (라이트모드)
                var actionButtons = this.FindAll<Button>().Where(b => b.Classes.Contains("action-button"));
                foreach (var button in actionButtons)
                {
                    button.Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#E67E22"));
                    button.Foreground = Avalonia.Media.Brushes.White;
                    System.Diagnostics.Debug.WriteLine("라이트모드: 액션 버튼 색상 적용됨");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"라이트모드 직접 적용 오류: {ex.Message}");
            }
        }

        private void ApplyThemeToControls(Control parent, bool isDark)
        {
            try
            {
                // Avalonia에서는 LogicalChildren을 사용
                if (parent is ILogical logical)
                {
                    foreach (var child in logical.LogicalChildren)
                    {
                        if (child is Border border)
                        {
                            if (border.Classes.Contains("product-card"))
                            {
                                if (isDark)
                                {
                                    border.Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#1E1E1E"));
                                    border.BorderBrush = Avalonia.Media.Brushes.Transparent;
                                    border.BorderThickness = new Avalonia.Thickness(0);
                                }
                                else
                                {
                                    border.Background = Avalonia.Media.Brushes.White;
                                    border.BorderBrush = Avalonia.Media.Brushes.Transparent;
                                    border.BorderThickness = new Avalonia.Thickness(0);
                                }
                            }
                            else if (border.Classes.Contains("info-panel"))
                            {
                                if (isDark)
                                {
                                    border.Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#2D2D2D"));
                                    border.BorderBrush = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#444444"));
                                    border.BorderThickness = new Avalonia.Thickness(1);
                                }
                                else
                                {
                                    border.Background = Avalonia.Media.Brushes.White;
                                    border.BorderBrush = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#E67E22"));
                                    border.BorderThickness = new Avalonia.Thickness(2);
                                }
                            }
                            else if (border.Classes.Contains("toggle-sidebar"))
                            {
                                if (isDark)
                                {
                                    border.Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#2D2D2D"));
                                    border.BorderBrush = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#FFDAC4"));
                                }
                                else
                                {
                                    border.Background = Avalonia.Media.Brushes.White;
                                    border.BorderBrush = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#E67E22"));
                                }
                            }
                            else if (border.Classes.Contains("image-placeholder"))
                            {
                                if (isDark)
                                {
                                    border.Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#4A4A4A"));
                                }
                                else
                                {
                                    border.Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#F0F0F0"));
                                }
                            }
                        }
                        else if (child is TextBlock textBlock)
                        {
                            if (textBlock.Classes.Contains("product-title") || 
                                textBlock.Classes.Contains("info-label") ||
                                textBlock.Classes.Contains("sidebar-title-text"))
                            {
                                if (isDark)
                                {
                                    textBlock.Foreground = Avalonia.Media.Brushes.White;
                                }
                                else
                                {
                                    textBlock.Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#333333"));
                                }
                            }
                            else if (textBlock.Classes.Contains("info-value"))
                            {
                                if (isDark)
                                {
                                    textBlock.Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#FFDAC4"));
                                }
                                else
                                {
                                    textBlock.Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#DF6C29"));
                                }
                            }
                            else if (textBlock.Classes.Contains("placeholder-text") ||
                                     textBlock.Classes.Contains("sidebar-desc-text"))
                            {
                                if (isDark)
                                {
                                    textBlock.Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#CCCCCC"));
                                }
                                else
                                {
                                    textBlock.Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#666666"));
                                }
                            }
                        }

                        // 재귀적으로 자식 요소들도 처리
                        if (child is Control childControl)
                        {
                            ApplyThemeToControls(childControl, isDark);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"테마 적용 오류: {ex.Message}");
            }
        }

        // 사이드바 관련 메서드들 (마켓점검 탭과 동일)
        private void ToggleSidebar(object sender, RoutedEventArgs e)
        {
            if (SidebarContainer.IsVisible)
            {
                CloseSidebarInstant();
            }
            else
            {
                OpenSidebarInstant();
            }
        }
        
        private void CloseSidebar(object sender, RoutedEventArgs e)
        {
            CloseSidebarInstant();
        }
        
        private void OpenSidebarInstant()
        {
            // 사이드바 표시
            SidebarContainer.IsVisible = true;
            // 토글 버튼 숨기기
            SidebarToggleButton.IsVisible = false;
        }
        
        private void CloseSidebarInstant()
        {
            // 사이드바 숨기기
            SidebarContainer.IsVisible = false;
            // 토글 버튼 다시 표시
            SidebarToggleButton.IsVisible = true;
        }

        // 버튼 클릭 이벤트 핸들러들 (향후 구현)
        private void OnDetailPageButtonClick(object? sender, RoutedEventArgs e)
        {
            // 상세페이지 만들기 기능 구현 예정
            Console.WriteLine("상세페이지 만들기 (BETA) 클릭됨");
        }

        private void OnThumbnailButtonClick(object? sender, RoutedEventArgs e)
        {
            // 썸네일 만들기 기능 구현 예정
            Console.WriteLine("썸네일 만들기 (BETA) 클릭됨");
        }

        private void OnTaobaoLinkButtonClick(object? sender, RoutedEventArgs e)
        {
            // 타오바오 링크 재배어링 기능 구현 예정
            Console.WriteLine("타오바오 링크 재배어링 하기 클릭됨");
        }
    }

    // 확장 메서드 클래스
    public static class ControlExtensions
    {
        public static IEnumerable<T> FindAll<T>(this Control control) where T : Control
        {
            var result = new List<T>();
            FindAllRecursive(control, result);
            return result;
        }

        private static void FindAllRecursive<T>(Control parent, List<T> result) where T : Control
        {
            if (parent is T target)
            {
                result.Add(target);
            }

            if (parent is ILogical logical)
            {
                foreach (var child in logical.LogicalChildren)
                {
                    if (child is Control childControl)
                    {
                        FindAllRecursive(childControl, result);
                    }
                }
            }
        }
    }
}
