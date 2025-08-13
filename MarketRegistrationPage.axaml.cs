using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Interactivity;
using Avalonia.Input;
using Avalonia.Styling;
using Avalonia;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Gumaedaehang
{
    public partial class MarketRegistrationPage : UserControl
    {
        // 전체 상품 데이터
        private List<ProductInfo> allProducts = new List<ProductInfo>();
        
        public MarketRegistrationPage()
        {
            InitializeComponent();
            InitializeProducts();
            
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
            
            // 초기 상품 카드 표시 (테마 설정 후)
            UpdateProductCards(allProducts?.Take(4).ToList() ?? new List<ProductInfo>());
            
            // UI가 완전히 로드된 후 다시 한번 업데이트 (라이트모드 초기 렌더링 문제 해결)
            this.Loaded += (s, e) => 
            {
                // 약간의 지연 후 다시 렌더링
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    UpdateProductCards(allProducts?.Take(4).ToList() ?? new List<ProductInfo>());
                }, Avalonia.Threading.DispatcherPriority.Background);
            };
        }
        
        private void OnThemeChanged(object? sender, EventArgs e)
        {
            try
            {
                UpdateTheme();
                // 상품 카드 다시 생성하여 테마 적용
                var currentProducts = allProducts.Take(4).ToList();
                UpdateProductCards(currentProducts);
            }
            catch
            {
                // 테마 업데이트 실패시 무시
            }
        }
        
        private void OnThemeManagerChanged(object? sender, ThemeManager.ThemeType themeType)
        {
            try
            {
                UpdateTheme();
                // 상품 카드 다시 생성하여 테마 적용
                var currentProducts = allProducts.Take(4).ToList();
                UpdateProductCards(currentProducts);
            }
            catch
            {
                // 테마 업데이트 실패시 무시
            }
        }
        
        private void UpdateTheme()
        {
            try
            {
                if (ThemeManager.Instance.IsDarkTheme)
                {
                    this.Classes.Add("dark-theme");
                    
                    // 루트 그리드와 메인 그리드에도 다크모드 클래스 추가
                    var rootGrid = this.FindControl<Grid>("RootGrid");
                    var mainGrid = this.FindControl<Grid>("MainGrid");
                    
                    if (rootGrid != null)
                    {
                        rootGrid.Background = new SolidColorBrush(Color.Parse("#1E1E1E"));
                    }
                    
                    if (mainGrid != null)
                    {
                        mainGrid.Background = new SolidColorBrush(Color.Parse("#1E1E1E"));
                    }
                    
                    // 검색박스 다크모드 스타일 직접 적용
                    var searchTextBox = this.FindControl<TextBox>("SearchTextBox");
                    if (searchTextBox != null)
                    {
                        searchTextBox.Foreground = Brushes.White;
                        searchTextBox.Background = Brushes.Transparent;
                    }
                    
                    // 검색창 Border 다크모드 스타일 직접 적용
                    var searchBorder = this.FindControl<Border>("SearchBorder");
                    if (searchBorder != null)
                    {
                        searchBorder.Background = new SolidColorBrush(Color.Parse("#4A4A4A"));
                        searchBorder.BorderBrush = new SolidColorBrush(Color.Parse("#FFDAC4"));
                    }
                    
                    // 차트 배경 다크모드 스타일 직접 적용
                    var chartBackground = this.FindControl<Border>("ChartBackground");
                    if (chartBackground != null)
                    {
                        chartBackground.Background = new SolidColorBrush(Color.Parse("#453F3C"));
                    }
                    
                    System.Diagnostics.Debug.WriteLine("MarketRegistrationPage: 다크모드 적용됨");
                }
                else
                {
                    this.Classes.Remove("dark-theme");
                    
                    // 루트 그리드와 메인 그리드를 라이트모드로 설정
                    var rootGrid = this.FindControl<Grid>("RootGrid");
                    var mainGrid = this.FindControl<Grid>("MainGrid");
                    
                    if (rootGrid != null)
                    {
                        rootGrid.Background = new SolidColorBrush(Colors.White);
                    }
                    
                    if (mainGrid != null)
                    {
                        mainGrid.Background = new SolidColorBrush(Colors.White);
                    }
                    
                    // 검색박스 라이트모드 스타일 직접 적용
                    var searchTextBox = this.FindControl<TextBox>("SearchTextBox");
                    if (searchTextBox != null)
                    {
                        searchTextBox.Foreground = new SolidColorBrush(Color.Parse("#333333"));
                        searchTextBox.Background = Brushes.Transparent;
                    }
                    
                    // 검색창 Border 라이트모드 스타일 직접 적용
                    var searchBorder = this.FindControl<Border>("SearchBorder");
                    if (searchBorder != null)
                    {
                        searchBorder.Background = new SolidColorBrush(Colors.White);
                        searchBorder.BorderBrush = new SolidColorBrush(Color.Parse("#E0E0E0"));
                    }
                    
                    // 차트 배경 라이트모드 스타일 직접 적용
                    var chartBackground = this.FindControl<Border>("ChartBackground");
                    if (chartBackground != null)
                    {
                        chartBackground.Background = new SolidColorBrush(Color.Parse("#FFF8F0"));
                    }
                    
                    System.Diagnostics.Debug.WriteLine("MarketRegistrationPage: 라이트모드 적용됨");
                }
            }
            catch
            {
                // 테마 설정 실패시 기본값 유지
                this.Classes.Remove("dark-theme");
            }
        }
        
        private void InitializeProducts()
        {
            // 샘플 상품 데이터 초기화
            allProducts = new List<ProductInfo>
            {
                new ProductInfo 
                { 
                    Name = "초코 바나나 시럽 아마토 핸드폰", 
                    Price = "100,000", 
                    Feedback = "일반업체 중국어가 있습니다.",
                    ImagePath = "images/product1.png"
                },
                new ProductInfo 
                { 
                    Name = "바나나 우유 스마트폰 케이스", 
                    Price = "25,000", 
                    Feedback = "브랜드명이 포함되어있습니다.",
                    ImagePath = "images/product1.png"
                },
                new ProductInfo 
                { 
                    Name = "초코렛 무선 이어폰", 
                    Price = "80,000", 
                    Feedback = "일반업체 중국어가 있습니다.",
                    ImagePath = "images/product1.png"
                },
                new ProductInfo 
                { 
                    Name = "아마토 블루투스 스피커", 
                    Price = "150,000", 
                    Feedback = "브랜드명이 포함되어있습니다.",
                    ImagePath = "images/product1.png"
                },
                new ProductInfo 
                { 
                    Name = "시럽 향 캔들 세트", 
                    Price = "35,000", 
                    Feedback = "일반업체 중국어가 있습니다.",
                    ImagePath = "images/product1.png"
                },
                new ProductInfo 
                { 
                    Name = "핸드폰 무선충전기", 
                    Price = "45,000", 
                    Feedback = "브랜드명이 포함되어있습니다.",
                    ImagePath = "images/product1.png"
                }
            };
        }
        
        private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                string searchText = textBox.Text?.ToLower() ?? "";
                
                // 검색어가 포함된 상품들 필터링
                var filteredProducts = allProducts
                    .Where(p => p.Name.ToLower().Contains(searchText))
                    .Take(4) // 최대 4개만 표시
                    .ToList();
                
                // UI 업데이트
                UpdateProductCards(filteredProducts);
            }
        }
        
        private void UpdateProductCards(List<ProductInfo> products)
        {
            // 기존 상품 카드들 제거
            FirstRowGrid.Children.Clear();
            SecondRowGrid.Children.Clear();
            
            // 새로운 상품 카드들 생성
            for (int i = 0; i < products.Count && i < 4; i++)
            {
                var productCard = CreateProductCard(products[i]);
                
                if (i < 2)
                {
                    // 첫 번째 행에 배치
                    Grid.SetColumn(productCard, i * 2); // 0 또는 2
                    FirstRowGrid.Children.Add(productCard);
                }
                else
                {
                    // 두 번째 행에 배치
                    Grid.SetColumn(productCard, (i - 2) * 2); // 0 또는 2
                    SecondRowGrid.Children.Add(productCard);
                }
            }
            
            // 강제로 UI 업데이트 (라이트모드 초기 렌더링 문제 해결)
            try
            {
                FirstRowGrid.InvalidateVisual();
                SecondRowGrid.InvalidateVisual();
                this.InvalidateVisual();
            }
            catch
            {
                // 무시
            }
        }
        
        private Border CreateProductCard(ProductInfo product)
        {
            // 현재 테마 확인 (ThemeManager 사용)
            bool isDarkMode = false;
            try
            {
                isDarkMode = ThemeManager.Instance.IsDarkTheme;
            }
            catch
            {
                // 테마 확인 실패시 라이트 모드로 기본 설정
                isDarkMode = false;
            }
            
            // 상품 카드 Border 생성
            var cardBorder = new Border
            {
                Classes = { "product-card" },
                Padding = new Avalonia.Thickness(16),
                CornerRadius = new Avalonia.CornerRadius(8)
            };
            
            // 다크모드에 따른 스타일 적용
            if (isDarkMode)
            {
                cardBorder.Background = new SolidColorBrush(Color.Parse("#3D3D3D"));
                cardBorder.BorderBrush = new SolidColorBrush(Color.Parse("#FFDAC4"));
                cardBorder.BorderThickness = new Avalonia.Thickness(1);
            }
            else
            {
                // 라이트모드에서도 카드가 보이도록 연한 회색 배경과 테두리 추가
                cardBorder.Background = new SolidColorBrush(Color.Parse("#FAFAFA"));
                cardBorder.BorderBrush = new SolidColorBrush(Color.Parse("#E0E0E0"));
                cardBorder.BorderThickness = new Avalonia.Thickness(1);
            }
            
            // 메인 Grid
            var mainGrid = new Grid();
            mainGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            mainGrid.RowDefinitions.Add(new RowDefinition(new GridLength(15)));
            mainGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            
            // 상품 이미지와 정보 Grid
            var imageInfoGrid = new Grid();
            imageInfoGrid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(320)));
            imageInfoGrid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(15)));
            imageInfoGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            Grid.SetRow(imageInfoGrid, 0);
            
            // 상품 이미지
            var imageBorder = new Border
            {
                Background = new SolidColorBrush(Color.Parse("#F8F8F8")),
                Width = 320,
                Height = 117,
                CornerRadius = new Avalonia.CornerRadius(20)
            };
            
            try
            {
                // 프로젝트 내 images 폴더의 이미지 사용
                var image = new Image
                {
                    Source = new Avalonia.Media.Imaging.Bitmap("images/product1.png"),
                    Stretch = Stretch.UniformToFill
                };
                imageBorder.Child = image;
            }
            catch
            {
                // 이미지 로드 실패 시 placeholder 텍스트 표시
                var placeholderText = new TextBlock
                {
                    Text = "상품 이미지",
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                    FontSize = 12,
                    Foreground = isDarkMode ? new SolidColorBrush(Color.Parse("#CCCCCC")) : new SolidColorBrush(Color.Parse("#999"))
                };
                imageBorder.Child = placeholderText;
            }
            Grid.SetColumn(imageBorder, 0);
            imageInfoGrid.Children.Add(imageBorder);
            
            // 상품 정보 StackPanel
            var infoStackPanel = new StackPanel
            {
                Spacing = 12,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top
            };
            Grid.SetColumn(infoStackPanel, 2);
            
            // 상품명
            var nameTextBlock = new TextBlock
            {
                Text = $"상품명: {product.Name}",
                FontSize = 13,
                FontWeight = FontWeight.Medium,
                TextWrapping = TextWrapping.Wrap,
                Classes = { "product-text" },
                Foreground = isDarkMode ? Brushes.White : new SolidColorBrush(Color.Parse("#333333"))
            };
            infoStackPanel.Children.Add(nameTextBlock);
            
            // 옵션 창
            var optionBorder = new Border
            {
                BorderBrush = new SolidColorBrush(Color.Parse("#FFDAC4")),
                BorderThickness = new Avalonia.Thickness(1),
                CornerRadius = new Avalonia.CornerRadius(4),
                Height = 60,
                Background = isDarkMode ? new SolidColorBrush(Color.Parse("#1E1E1E")) : Brushes.Transparent
            };
            var scrollViewer = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            };
            var optionTextBox = new TextBox
            {
                Text = "옵션",
                Background = Brushes.Transparent,
                BorderThickness = new Avalonia.Thickness(0),
                FontSize = 12,
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                Padding = new Avalonia.Thickness(8),
                Foreground = isDarkMode ? Brushes.White : new SolidColorBrush(Color.Parse("#333333"))
            };
            scrollViewer.Content = optionTextBox;
            optionBorder.Child = scrollViewer;
            infoStackPanel.Children.Add(optionBorder);
            
            // 가격
            var priceTextBlock = new TextBlock
            {
                Text = $"가격 : {product.Price}",
                FontSize = 13,
                FontWeight = FontWeight.SemiBold,
                Classes = { "product-text" },
                Foreground = isDarkMode ? Brushes.White : new SolidColorBrush(Color.Parse("#333333"))
            };
            infoStackPanel.Children.Add(priceTextBlock);
            
            imageInfoGrid.Children.Add(infoStackPanel);
            mainGrid.Children.Add(imageInfoGrid);
            
            // 피드백 및 링크 Grid
            var feedbackGrid = new Grid();
            feedbackGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            feedbackGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            Grid.SetRow(feedbackGrid, 2);
            
            // 피드백 StackPanel
            var feedbackStackPanel = new StackPanel
            {
                Orientation = Avalonia.Layout.Orientation.Horizontal,
                Spacing = 6,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
            };
            Grid.SetColumn(feedbackStackPanel, 0);
            
            var feedbackLabel = new TextBlock
            {
                Text = "피드백:",
                FontSize = 12,
                Foreground = isDarkMode ? new SolidColorBrush(Color.Parse("#CCCCCC")) : new SolidColorBrush(Color.Parse("#666"))
            };
            var feedbackText = new TextBlock
            {
                Text = product.Feedback,
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.Parse("#FFDAC4")),
                TextWrapping = TextWrapping.Wrap
            };
            feedbackStackPanel.Children.Add(feedbackLabel);
            feedbackStackPanel.Children.Add(feedbackText);
            feedbackGrid.Children.Add(feedbackStackPanel);
            
            // 네이버쇼핑 링크
            var linkTextBlock = new TextBlock
            {
                Text = "네이버쇼핑 바로가기",
                FontSize = 12,
                Foreground = isDarkMode ? new SolidColorBrush(Color.Parse("#CCCCCC")) : new SolidColorBrush(Color.Parse("#373737")),
                TextDecorations = TextDecorations.Underline,
                Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand),
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
            };
            Grid.SetColumn(linkTextBlock, 1);
            feedbackGrid.Children.Add(linkTextBlock);
            
            mainGrid.Children.Add(feedbackGrid);
            cardBorder.Child = mainGrid;
            
            return cardBorder;
        }
        
        // 사이드바 관련 메서드들
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
        
        private void CloseSidebar(object sender, PointerPressedEventArgs e)
        {
            CloseSidebarInstant();
        }
        
        private void OpenSidebarInstant()
        {
            // 사이드바와 오버레이 표시
            SidebarContainer.IsVisible = true;
            SidebarOverlay.IsVisible = true;
        }
        
        private void CloseSidebarInstant()
        {
            // 사이드바와 오버레이 숨기기
            SidebarContainer.IsVisible = false;
            SidebarOverlay.IsVisible = false;
        }
        
        // 사이드바 메뉴 버튼 이벤트 핸들러들
        private void LoadHistory(object sender, RoutedEventArgs e)
        {
            // 내역 불러오기 기능 구현
            // 여기에 실제 내역 불러오기 로직을 추가하세요
            
            // 임시로 메시지 표시 (실제 구현시 제거)
            System.Diagnostics.Debug.WriteLine("내역 불러오기 기능이 실행되었습니다.");
            
            // 사이드바 닫기
            CloseSidebarInstant();
        }
        
        private void ExportToPdf(object sender, RoutedEventArgs e)
        {
            // PDF 추출 기능 구현
            // 여기에 실제 PDF 생성 로직을 추가하세요
            
            // 임시로 메시지 표시 (실제 구현시 제거)
            System.Diagnostics.Debug.WriteLine("PDF로 추출하기 기능이 실행되었습니다.");
            
            // 사이드바 닫기
            CloseSidebarInstant();
        }
    }
    
    // 상품 정보 클래스
    public class ProductInfo
    {
        public string Name { get; set; } = "";
        public string Price { get; set; } = "";
        public string Feedback { get; set; } = "";
        public string ImagePath { get; set; } = "";
    }
}
