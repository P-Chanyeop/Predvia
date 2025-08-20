using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.Input;
using Avalonia.LogicalTree;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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
                SetupEventHandlers();
            });
        }

        private void SetupEventHandlers()
        {
            try
            {
                // ComboBox 기본값 설정
                var thumbnailSizeComboBox = this.FindControl<ComboBox>("ThumbnailSizeComboBox");
                if (thumbnailSizeComboBox != null && thumbnailSizeComboBox.Items.Count > 0)
                {
                    thumbnailSizeComboBox.SelectedIndex = 0;
                }

                var deliveryDaysComboBox = this.FindControl<ComboBox>("DeliveryDaysComboBox");
                if (deliveryDaysComboBox != null && deliveryDaysComboBox.Items.Count > 0)
                {
                    deliveryDaysComboBox.SelectedIndex = 0;
                }

                // 마진 슬라이더 이벤트 연결
                var marginSlider = this.FindControl<Slider>("MarginSlider");
                var marginValueText = this.FindControl<TextBlock>("MarginValueText");
                
                if (marginSlider != null && marginValueText != null)
                {
                    marginSlider.PropertyChanged += (s, args) =>
                    {
                        if (args.Property.Name == "Value")
                        {
                            marginValueText.Text = $"{(int)marginSlider.Value}%";
                            UpdateTaobaoPreview();
                        }
                    };
                }

                // 다른 입력 필드들도 이벤트 연결
                var originalLinkTextBox = this.FindControl<TextBox>("OriginalLinkTextBox");
                if (originalLinkTextBox != null)
                {
                    originalLinkTextBox.PropertyChanged += (s, args) =>
                    {
                        if (args.Property.Name == "Text")
                        {
                            UpdateTaobaoPreview();
                        }
                    };
                }

                var shippingCostTextBox = this.FindControl<TextBox>("ShippingCostTextBox");
                if (shippingCostTextBox != null)
                {
                    shippingCostTextBox.PropertyChanged += (s, args) =>
                    {
                        if (args.Property.Name == "Text")
                        {
                            UpdateTaobaoPreview();
                        }
                    };
                }

                var processingFeeTextBox = this.FindControl<TextBox>("ProcessingFeeTextBox");
                if (processingFeeTextBox != null)
                {
                    processingFeeTextBox.PropertyChanged += (s, args) =>
                    {
                        if (args.Property.Name == "Text")
                        {
                            UpdateTaobaoPreview();
                        }
                    };
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"이벤트 핸들러 설정 오류: {ex.Message}");
            }
        }

        private void UpdateTaobaoPreview()
        {
            try
            {
                var originalLinkTextBox = this.FindControl<TextBox>("OriginalLinkTextBox");
                var marginSlider = this.FindControl<Slider>("MarginSlider");
                var shippingCostTextBox = this.FindControl<TextBox>("ShippingCostTextBox");
                var processingFeeTextBox = this.FindControl<TextBox>("ProcessingFeeTextBox");

                var previewOriginalPrice = this.FindControl<TextBlock>("PreviewOriginalPrice");
                var previewFinalPrice = this.FindControl<TextBlock>("PreviewFinalPrice");
                var previewProfit = this.FindControl<TextBlock>("PreviewProfit");
                var previewNewLink = this.FindControl<TextBlock>("PreviewNewLink");

                if (originalLinkTextBox?.Text?.Contains("taobao.com") == true)
                {
                    // 예시 계산 (실제로는 API 호출)
                    var originalPrice = 15000; // 예시 가격
                    var marginPercent = (int)(marginSlider?.Value ?? 50);
                    var shippingCost = int.TryParse(shippingCostTextBox?.Text, out var shipping) ? shipping : 3000;
                    var processingFee = int.TryParse(processingFeeTextBox?.Text, out var fee) ? fee : 1000;

                    var finalPrice = originalPrice + (originalPrice * marginPercent / 100) + shippingCost + processingFee;
                    var profit = finalPrice - originalPrice - shippingCost - processingFee;

                    if (previewOriginalPrice != null)
                        previewOriginalPrice.Text = $"원본 가격: {originalPrice:N0}원";
                    
                    if (previewFinalPrice != null)
                        previewFinalPrice.Text = $"최종 판매가격: {finalPrice:N0}원";
                    
                    if (previewProfit != null)
                        previewProfit.Text = $"예상 수익: {profit:N0}원";
                    
                    if (previewNewLink != null)
                        previewNewLink.Text = $"새 링크: https://yourstore.com/product/{DateTime.Now.Ticks}";
                }
                else
                {
                    if (previewOriginalPrice != null)
                        previewOriginalPrice.Text = "원본 가격: -";
                    
                    if (previewFinalPrice != null)
                        previewFinalPrice.Text = "최종 판매가격: -";
                    
                    if (previewProfit != null)
                        previewProfit.Text = "예상 수익: -";
                    
                    if (previewNewLink != null)
                        previewNewLink.Text = "새 링크: 링크를 입력하면 생성됩니다";
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"타오바오 미리보기 업데이트 오류: {ex.Message}");
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

                // 현재 표시된 오버레이들도 업데이트
                Dispatcher.UIThread.Post(() =>
                {
                    UpdateVisibleOverlays();
                });

                // UI 강제 업데이트
                InvalidateVisual();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"테마 업데이트 오류: {ex.Message}");
            }
        }

        private void UpdateVisibleOverlays()
        {
            try
            {
                var detailOverlay = this.FindControl<Grid>("DetailPageOverlay");
                if (detailOverlay?.IsVisible == true)
                {
                    UpdateOverlayTheme(detailOverlay);
                }

                var thumbnailOverlay = this.FindControl<Grid>("ThumbnailOverlay");
                if (thumbnailOverlay?.IsVisible == true)
                {
                    UpdateOverlayTheme(thumbnailOverlay);
                }

                var taobaoOverlay = this.FindControl<Grid>("TaobaoLinkOverlay");
                if (taobaoOverlay?.IsVisible == true)
                {
                    UpdateOverlayTheme(taobaoOverlay);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"오버레이 업데이트 오류: {ex.Message}");
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

        // 버튼 클릭 이벤트 핸들러들
        private void OnDetailPageButtonClick(object? sender, RoutedEventArgs e)
        {
            ShowDetailPageOverlay();
        }

        private void OnThumbnailButtonClick(object? sender, RoutedEventArgs e)
        {
            ShowThumbnailOverlay();
        }

        private void OnTaobaoLinkButtonClick(object? sender, RoutedEventArgs e)
        {
            ShowTaobaoLinkOverlay();
        }

        // 오버레이 표시/숨기기 메서드들
        private void ShowDetailPageOverlay()
        {
            var overlay = this.FindControl<Grid>("DetailPageOverlay");
            if (overlay != null)
            {
                overlay.IsVisible = true;
                UpdateOverlayTheme(overlay);
            }
        }

        private void ShowThumbnailOverlay()
        {
            var overlay = this.FindControl<Grid>("ThumbnailOverlay");
            if (overlay != null)
            {
                overlay.IsVisible = true;
                UpdateOverlayTheme(overlay);
            }
        }

        private void ShowTaobaoLinkOverlay()
        {
            var overlay = this.FindControl<Grid>("TaobaoLinkOverlay");
            if (overlay != null)
            {
                overlay.IsVisible = true;
                UpdateOverlayTheme(overlay);
            }
        }

        private void CloseDetailPageOverlay(object? sender, RoutedEventArgs e)
        {
            var overlay = this.FindControl<Grid>("DetailPageOverlay");
            if (overlay != null)
            {
                overlay.IsVisible = false;
            }
        }

        private void CloseThumbnailOverlay(object? sender, RoutedEventArgs e)
        {
            var overlay = this.FindControl<Grid>("ThumbnailOverlay");
            if (overlay != null)
            {
                overlay.IsVisible = false;
            }
        }

        private void CloseTaobaoLinkOverlay(object? sender, RoutedEventArgs e)
        {
            var overlay = this.FindControl<Grid>("TaobaoLinkOverlay");
            if (overlay != null)
            {
                overlay.IsVisible = false;
            }
        }

        // 오버레이 테마 업데이트
        private void UpdateOverlayTheme(Grid overlay)
        {
            try
            {
                var isDarkTheme = ThemeManager.Instance.IsDarkTheme;
                
                // 오버레이 배경색 설정
                if (isDarkTheme)
                {
                    overlay.Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#1E1E1E"));
                }
                else
                {
                    overlay.Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#F5F5F5"));
                }

                // 모든 섹션 Border들 업데이트
                var sectionBorders = overlay.GetLogicalDescendants().OfType<Border>().Where(b => b.Classes.Contains("overlay-section"));
                foreach (var border in sectionBorders)
                {
                    if (isDarkTheme)
                    {
                        border.Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#3A3A3A"));
                        border.BorderBrush = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#FFDAC4"));
                    }
                    else
                    {
                        border.Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#FFF5E6"));
                        border.BorderBrush = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#E67E22"));
                    }
                }

                // 미리보기 Border들 업데이트
                var previewBorders = overlay.GetLogicalDescendants().OfType<Border>().Where(b => b.Classes.Contains("overlay-preview"));
                foreach (var border in previewBorders)
                {
                    if (isDarkTheme)
                    {
                        border.Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#333333"));
                        border.BorderBrush = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#666666"));
                    }
                    else
                    {
                        border.Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#F8F8F8"));
                        border.BorderBrush = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#E0E0E0"));
                    }
                }

                // 모든 TextBlock 업데이트
                var textBlocks = overlay.GetLogicalDescendants().OfType<TextBlock>();
                foreach (var textBlock in textBlocks)
                {
                    if (isDarkTheme)
                    {
                        // 제목들 (큰 폰트)
                        if (textBlock.FontSize >= 28)
                        {
                            textBlock.Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#FFDAC4"));
                        }
                        // 일반 텍스트
                        else if (textBlock.Foreground?.ToString() == "#333333" || textBlock.Foreground?.ToString() == "#666666")
                        {
                            textBlock.Foreground = Avalonia.Media.Brushes.White;
                        }
                        // 강조 텍스트 (주황색)
                        else if (textBlock.Foreground?.ToString() == "#E67E22")
                        {
                            textBlock.Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#FFDAC4"));
                        }
                        // 회색 텍스트
                        else if (textBlock.Foreground?.ToString() == "#999999")
                        {
                            textBlock.Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#CCCCCC"));
                        }
                    }
                    else
                    {
                        // 라이트모드 색상 복원
                        if (textBlock.FontSize >= 28) // 제목들
                        {
                            textBlock.Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#E67E22"));
                        }
                        else if (textBlock.FontSize >= 20) // 라벨들
                        {
                            textBlock.Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#333333"));
                        }
                        else if (textBlock.FontSize >= 16) // 일반 텍스트
                        {
                            textBlock.Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#666666"));
                        }
                    }
                }

                // 모든 TextBox 업데이트
                var textBoxes = overlay.GetLogicalDescendants().OfType<TextBox>();
                foreach (var textBox in textBoxes)
                {
                    if (isDarkTheme)
                    {
                        textBox.Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#4A4A4A"));
                        textBox.Foreground = Avalonia.Media.Brushes.White;
                        textBox.BorderBrush = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#FFDAC4"));
                    }
                    else
                    {
                        textBox.Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#FFDAC4"));
                        textBox.Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#333333"));
                        textBox.BorderBrush = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#E67E22"));
                    }
                }

                // 모든 ComboBox 업데이트
                var comboBoxes = overlay.GetLogicalDescendants().OfType<ComboBox>();
                foreach (var comboBox in comboBoxes)
                {
                    if (isDarkTheme)
                    {
                        comboBox.Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#4A4A4A"));
                        comboBox.Foreground = Avalonia.Media.Brushes.White;
                        comboBox.BorderBrush = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#FFDAC4"));
                    }
                    else
                    {
                        comboBox.Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#FFDAC4"));
                        comboBox.Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#333333"));
                        comboBox.BorderBrush = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#E67E22"));
                    }
                }

                // 모든 RadioButton과 CheckBox 업데이트
                var radioButtons = overlay.GetLogicalDescendants().OfType<RadioButton>();
                foreach (var radioButton in radioButtons)
                {
                    if (isDarkTheme)
                    {
                        radioButton.Foreground = Avalonia.Media.Brushes.White;
                    }
                    else
                    {
                        radioButton.Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#333333"));
                    }
                }

                var checkBoxes = overlay.GetLogicalDescendants().OfType<CheckBox>();
                foreach (var checkBox in checkBoxes)
                {
                    if (isDarkTheme)
                    {
                        checkBox.Foreground = Avalonia.Media.Brushes.White;
                    }
                    else
                    {
                        checkBox.Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#333333"));
                    }
                }

                // 모든 Button 업데이트
                var buttons = overlay.GetLogicalDescendants().OfType<Button>();
                foreach (var button in buttons)
                {
                    if (button.Classes.Contains("secondary-button"))
                    {
                        // 취소 버튼
                        if (isDarkTheme)
                        {
                            button.Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#666666"));
                            button.Foreground = Avalonia.Media.Brushes.White;
                        }
                        else
                        {
                            button.Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#CCCCCC"));
                            button.Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#333333"));
                        }
                    }
                    else if (button.Classes.Contains("close-button"))
                    {
                        // 닫기 버튼
                        button.Background = Avalonia.Media.Brushes.Transparent;
                        if (isDarkTheme)
                        {
                            button.Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#FFDAC4"));
                        }
                        else
                        {
                            button.Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#E67E22"));
                        }
                    }
                    else if (button.Classes.Contains("primary-button"))
                    {
                        // 주요 버튼들
                        if (isDarkTheme)
                        {
                            button.Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#FFDAC4"));
                            button.Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#333333"));
                        }
                        else
                        {
                            button.Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#E67E22"));
                            button.Foreground = Avalonia.Media.Brushes.White;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"오버레이 테마 업데이트 오류: {ex.Message}");
            }
        }

        // 기능별 이벤트 핸들러들
        private void GeneratePreview(object? sender, RoutedEventArgs e)
        {
            try
            {
                var productNameTextBox = this.FindControl<TextBox>("ProductNameTextBox");
                var priceTextBox = this.FindControl<TextBox>("PriceTextBox");
                var descriptionTextBox = this.FindControl<TextBox>("DescriptionTextBox");

                if (string.IsNullOrWhiteSpace(productNameTextBox?.Text))
                {
                    ShowMessage("상품명을 입력해주세요.");
                    return;
                }

                // 미리보기 생성 로직 (실제로는 더 복잡한 HTML 생성)
                Console.WriteLine("상세페이지 미리보기 생성 중...");
                ShowMessage("미리보기가 생성되었습니다!");
            }
            catch (Exception ex)
            {
                ShowMessage($"미리보기 생성 중 오류가 발생했습니다: {ex.Message}");
            }
        }

        private void GenerateDetailPage(object? sender, RoutedEventArgs e)
        {
            try
            {
                var productNameTextBox = this.FindControl<TextBox>("ProductNameTextBox");
                var priceTextBox = this.FindControl<TextBox>("PriceTextBox");
                var categoryComboBox = this.FindControl<ComboBox>("CategoryComboBox");
                var keywordsTextBox = this.FindControl<TextBox>("KeywordsTextBox");
                var descriptionTextBox = this.FindControl<TextBox>("DescriptionTextBox");

                if (string.IsNullOrWhiteSpace(productNameTextBox?.Text))
                {
                    ShowMessage("상품명을 입력해주세요.");
                    return;
                }

                if (string.IsNullOrWhiteSpace(priceTextBox?.Text))
                {
                    ShowMessage("가격을 입력해주세요.");
                    return;
                }

                // 상세페이지 생성 로직
                Console.WriteLine("상세페이지 생성 중...");
                ShowMessage("상세페이지가 성공적으로 생성되었습니다!");
                
                // 2초 후 오버레이 닫기
                Dispatcher.UIThread.Post(async () =>
                {
                    await Task.Delay(2000);
                    CloseDetailPageOverlay(null, new RoutedEventArgs());
                });
            }
            catch (Exception ex)
            {
                ShowMessage($"상세페이지 생성 중 오류가 발생했습니다: {ex.Message}");
            }
        }

        private void SelectImageFile(object? sender, RoutedEventArgs e)
        {
            try
            {
                // 파일 선택 다이얼로그 (실제로는 OpenFileDialog 사용)
                Console.WriteLine("이미지 파일 선택 다이얼로그 열기");
                ShowMessage("이미지가 업로드되었습니다!");
            }
            catch (Exception ex)
            {
                ShowMessage($"이미지 업로드 중 오류가 발생했습니다: {ex.Message}");
            }
        }

        private void GenerateThumbnailPreview(object? sender, RoutedEventArgs e)
        {
            try
            {
                var thumbnailSizeComboBox = this.FindControl<ComboBox>("ThumbnailSizeComboBox");
                var qualitySlider = this.FindControl<Slider>("QualitySlider");

                Console.WriteLine($"썸네일 미리보기 생성 - 크기: {thumbnailSizeComboBox?.SelectedItem}, 품질: {qualitySlider?.Value}%");
                ShowMessage("썸네일 미리보기가 생성되었습니다!");
            }
            catch (Exception ex)
            {
                ShowMessage($"썸네일 미리보기 생성 중 오류가 발생했습니다: {ex.Message}");
            }
        }

        private void GenerateThumbnail(object? sender, RoutedEventArgs e)
        {
            try
            {
                var thumbnailSizeComboBox = this.FindControl<ComboBox>("ThumbnailSizeComboBox");
                var qualitySlider = this.FindControl<Slider>("QualitySlider");

                Console.WriteLine($"썸네일 생성 - 크기: {thumbnailSizeComboBox?.SelectedItem}, 품질: {qualitySlider?.Value}%");
                ShowMessage("썸네일이 성공적으로 생성되었습니다!");
                
                // 2초 후 오버레이 닫기
                Dispatcher.UIThread.Post(async () =>
                {
                    await Task.Delay(2000);
                    CloseThumbnailOverlay(null, new RoutedEventArgs());
                });
            }
            catch (Exception ex)
            {
                ShowMessage($"썸네일 생성 중 오류가 발생했습니다: {ex.Message}");
            }
        }

        private void AnalyzeTaobaoLink(object? sender, RoutedEventArgs e)
        {
            try
            {
                var originalLinkTextBox = this.FindControl<TextBox>("OriginalLinkTextBox");
                
                if (string.IsNullOrWhiteSpace(originalLinkTextBox?.Text))
                {
                    ShowMessage("타오바오 링크를 입력해주세요.");
                    return;
                }

                if (!originalLinkTextBox.Text.Contains("taobao.com"))
                {
                    ShowMessage("올바른 타오바오 링크를 입력해주세요.");
                    return;
                }

                Console.WriteLine("타오바오 링크 분석 중...");
                UpdateTaobaoPreview();
                ShowMessage("링크 분석이 완료되었습니다!");
            }
            catch (Exception ex)
            {
                ShowMessage($"링크 분석 중 오류가 발생했습니다: {ex.Message}");
            }
        }

        private void GenerateRebairedLink(object? sender, RoutedEventArgs e)
        {
            try
            {
                var originalLinkTextBox = this.FindControl<TextBox>("OriginalLinkTextBox");
                var marginSlider = this.FindControl<Slider>("MarginSlider");
                var shippingCostTextBox = this.FindControl<TextBox>("ShippingCostTextBox");
                var processingFeeTextBox = this.FindControl<TextBox>("ProcessingFeeTextBox");

                if (string.IsNullOrWhiteSpace(originalLinkTextBox?.Text))
                {
                    ShowMessage("타오바오 링크를 입력해주세요.");
                    return;
                }

                if (!originalLinkTextBox.Text.Contains("taobao.com"))
                {
                    ShowMessage("올바른 타오바오 링크를 입력해주세요.");
                    return;
                }

                Console.WriteLine("재배어링 링크 생성 중...");
                ShowMessage("재배어링 링크가 성공적으로 생성되었습니다!");
                
                // 2초 후 오버레이 닫기
                Dispatcher.UIThread.Post(async () =>
                {
                    await Task.Delay(2000);
                    CloseTaobaoLinkOverlay(null, new RoutedEventArgs());
                });
            }
            catch (Exception ex)
            {
                ShowMessage($"재배어링 링크 생성 중 오류가 발생했습니다: {ex.Message}");
            }
        }

        private void ShowMessage(string message)
        {
            // 간단한 메시지 표시 (실제로는 MessageBox나 Toast 사용)
            Console.WriteLine($"메시지: {message}");
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
