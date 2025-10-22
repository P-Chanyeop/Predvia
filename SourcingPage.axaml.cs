using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Input;
using Avalonia.Platform;
using Avalonia.Threading;
using Avalonia.Layout;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System;
using System.Text;
using System.IO;
using System.Threading.Tasks;
using Gumaedaehang.Services;

namespace Gumaedaehang
{
    // 리뷰 데이터 구조
    public class ReviewItem
    {
        public int rating { get; set; }
        public string content { get; set; } = "";
    }

    public class ReviewFileData
    {
        public List<ReviewItem> reviews { get; set; } = new List<ReviewItem>();
        public int reviewCount { get; set; }
    }

namespace Gumaedaehang
{
    public partial class SourcingPage : UserControl
    {
        private readonly ThumbnailService _thumbnailService = new();
        private Grid? _noDataView;
        private Grid? _dataAvailableView;
        private TextBlock? _addMoreLink;
        private Button? _testDataButton;
        private Button? _testDataButton2;
        private CheckBox? _selectAllCheckBox;
        private bool _hasData = false;
        
        // 한글 입력 처리를 위한 타이머
        private DispatcherTimer? _inputTimer;
        private int _currentProductId = 0;
        
        // 상품별 UI 요소들을 관리하는 딕셔너리
        private Dictionary<int, ProductUIElements> _productElements = new Dictionary<int, ProductUIElements>();
        
        // 네이버 스마트스토어 서비스
        private NaverSmartStoreService? _naverService;
        private ChromeExtensionService? _extensionService;
        
        // UI 요소 참조
        private TextBox? _manualSourcingTextBox;
        private Button? _manualSourcingButton;
        private TextBox? _autoSourcingTextBox;
        private Button? _autoSourcingButton;
        private TextBox? _mainProductTextBox;
        private Button? _mainProductButton;
        
        // 실제 데이터 컨테이너
        private StackPanel? RealDataContainer;
        
        public SourcingPage()
        {
            try
            {
                InitializeComponent();
                
                // 🧹 프로그램 시작 시 자동 초기화 (조용히)
                ClearPreviousCrawlingDataSilent();
                
                // 플레이스홀더 설정
                SetupPlaceholders();
                
                // 한글 입력 처리용 타이머 초기화
                _inputTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(300) // 300ms 지연
                };
                _inputTimer.Tick += InputTimer_Tick;
                
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
                
                // UI 요소 참조 가져오기
                _noDataView = this.FindControl<Grid>("NoDataView");
                _dataAvailableView = this.FindControl<Grid>("DataAvailableView");
                _addMoreLink = this.FindControl<TextBlock>("AddMoreLink");
                _testDataButton = this.FindControl<Button>("TestDataButton");
                _testDataButton2 = this.FindControl<Button>("TestDataButton2");
                _selectAllCheckBox = this.FindControl<CheckBox>("SelectAllCheckBox");
                
                // 페어링 버튼 UI 요소 참조
                _manualSourcingTextBox = this.FindControl<TextBox>("ManualSourcingTextBox");
                _manualSourcingButton = this.FindControl<Button>("ManualSourcingButton");
                _autoSourcingTextBox = this.FindControl<TextBox>("SourcingMaterialTextBox");
                _autoSourcingButton = this.FindControl<Button>("AutoSourcingButton");
                _mainProductTextBox = this.FindControl<TextBox>("MainProductTextBox");
                _mainProductButton = this.FindControl<Button>("MainProductButton");
                
                // RealDataContainer 초기화
                RealDataContainer = this.FindControl<StackPanel>("RealDataContainer");
                
                // 상품들의 UI 요소들 초기화
                InitializeProductElements();
                
                // 저장된 썸네일 로드 및 표시
                _ = Task.Run(LoadAndDisplayThumbnails);
                
                // 이벤트 핸들러 등록
                RegisterEventHandlers();
                
                // 초기 상태 설정
                UpdateViewVisibility();
                
                // 크롤링된 데이터 자동 로드
                LoadCrawledData();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SourcingPage 초기화 중 오류: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"스택 트레이스: {ex.StackTrace}");
                // 초기화 오류 시에도 계속 진행
            }
        }

        // 저장된 썸네일 로드 및 표시
        private async Task LoadAndDisplayThumbnails()
        {
            try
            {
                var thumbnails = await _thumbnailService.LoadThumbnailInfoAsync();
                Debug.WriteLine($"📸 {thumbnails.Count}개의 저장된 썸네일 발견");
                
                if (thumbnails.Count > 0)
                {
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        // 첫 번째 썸네일을 메인 상품 이미지로 표시
                        var firstThumbnail = thumbnails[0];
                        if (File.Exists(firstThumbnail.LocalPath))
                        {
                            DisplayThumbnailInMainImage(firstThumbnail.LocalPath);
                            Debug.WriteLine($"✅ 첫 번째 썸네일 표시: {firstThumbnail.ProductTitle}");
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ 썸네일 로드 오류: {ex.Message}");
            }
        }
        
        // 메인 상품 이미지에 썸네일 표시
        private void DisplayThumbnailInMainImage(string imagePath)
        {
            try
            {
                // 모든 Image 요소를 찾아서 첫 번째 큰 이미지에 썸네일 설정
                var images = this.FindAll<Image>();
                var mainImage = images.FirstOrDefault(img => 
                {
                    var parent = img.Parent as Border;
                    return parent != null && parent.Width == 260 && parent.Height == 260;
                });
                
                if (mainImage != null)
                {
                    var bitmap = new Bitmap(imagePath);
                    mainImage.Source = bitmap;
                    Debug.WriteLine($"🖼️ 메인 이미지에 썸네일 설정 완료: {System.IO.Path.GetFileName(imagePath)}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ 이미지 표시 오류: {ex.Message}");
            }
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
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
                    System.Diagnostics.Debug.WriteLine("SourcingPage: 다크모드 적용됨");
                    
                    // 다크모드에서 TextBox 배경색 강제 설정
                    UpdateTextBoxColors("#4A4A4A", "#FFFFFF");
                }
                else
                {
                    this.Classes.Remove("dark-theme");
                    System.Diagnostics.Debug.WriteLine("SourcingPage: 라이트모드 적용됨");
                    
                    // 라이트모드에서 TextBox 배경색 강제 설정
                    UpdateTextBoxColors("#FFDAC4", "#000000");
                }                
                // 기존 키워드들의 색상 업데이트
                UpdateExistingKeywordColors();
            }
            catch
            {
                // 테마 설정 실패시 기본값 유지
                this.Classes.Remove("dark-theme");
            }
        }
        
        // 기존 키워드들의 색상을 현재 테마에 맞게 업데이트
        private void UpdateExistingKeywordColors()
        {
            foreach (var productPair in _productElements)
            {
                var product = productPair.Value;
                
                // ByteCountTextBlock 색상 업데이트
                if (product.ByteCountTextBlock != null)
                {
                    var text = product.ByteCountTextBlock.Text;
                    if (text != null && text.Contains("/50 byte"))
                    {
                        var byteCount = int.Parse(text.Split('/')[0]);
                        if (byteCount > 50)
                        {
                            product.ByteCountTextBlock.Foreground = Brushes.Red;
                        }
                        else
                        {
                            product.ByteCountTextBlock.Foreground = ThemeManager.Instance.IsDarkTheme ? Brushes.LightGray : Brushes.Gray;
                        }
                    }
                }
                
                // 상품명 키워드 패널의 키워드들 색상 업데이트
                if (product.NameKeywordPanel != null)
                {
                    foreach (var child in product.NameKeywordPanel.Children)
                    {
                        if (child is StackPanel stackPanel && stackPanel.Children.Count > 0 && stackPanel.Children[0] is TextBlock textBlock)
                        {
                            textBlock.Foreground = ThemeManager.Instance.IsDarkTheme ? Brushes.White : new SolidColorBrush(Color.Parse("#333333"));
                        }
                    }
                }
            }
        }
        
        // 상품들의 UI 요소들을 초기화
        // 실제 데이터 컨테이너 초기화
        private void InitializeProductElements()
        {
            // 더미데이터 제거됨 - 실제 데이터는 AddProductImageCard 메서드를 통해 동적으로 추가됩니다
            Debug.WriteLine("InitializeProductElements 호출됨");
            
            // 초기화 후에는 데이터를 로드하지 않음 (자동 초기화 완료)
            Debug.WriteLine("초기화 완료 - 빈 상태로 시작");
        }

        // 크롤링된 데이터를 로드하는 메서드
        public void LoadCrawledData()
        {
            try
            {
                var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                var predviaPath = System.IO.Path.Combine(appDataPath, "Predvia");
                var imagesPath = System.IO.Path.Combine(predviaPath, "Images");
                var productDataPath = System.IO.Path.Combine(predviaPath, "ProductData");

                if (!Directory.Exists(imagesPath) || !Directory.Exists(productDataPath))
                {
                    return;
                }

                var imageFiles = Directory.GetFiles(imagesPath, "*_main.jpg");
                
                foreach (var imageFile in imageFiles)
                {
                    var fileName = System.IO.Path.GetFileNameWithoutExtension(imageFile);
                    var parts = fileName.Split('_');
                    
                    if (parts.Length >= 3)
                    {
                        var storeId = parts[0];
                        var productId = parts[1];
                        
                        // UI에 상품 추가
                        Dispatcher.UIThread.Post(() =>
                        {
                            AddProductImageCard(storeId, productId, imageFile);
                        });
                    }
                }
                
                // 데이터가 있으면 표시
                if (imageFiles.Length > 0)
                {
                    Dispatcher.UIThread.Post(() =>
                    {
                        _hasData = true;
                        UpdateViewVisibility();
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ 크롤링 데이터 로드 오류: {ex.Message}");
            }
        }

        // 크롤링된 상품명 읽기
        private string GetOriginalProductName(string storeId, string productId)
        {
            try
            {
                var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                var productDataPath = Path.Combine(appDataPath, "Predvia", "ProductData");
                var nameFile = Path.Combine(productDataPath, $"{storeId}_{productId}_name.txt");
                
                if (File.Exists(nameFile))
                {
                    return File.ReadAllText(nameFile, System.Text.Encoding.UTF8);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ 상품명 읽기 오류: {ex.Message}");
            }
            return "상품명 없음";
        }

        // 크롤링된 리뷰 데이터 읽기
        private List<string> GetProductReviews(string storeId, string productId)
        {
            var reviews = new List<string>();
            try
            {
                var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                var reviewsPath = Path.Combine(appDataPath, "Predvia", "Reviews");
                var reviewFile = Path.Combine(reviewsPath, $"{storeId}_{productId}_reviews.json");
                
                if (File.Exists(reviewFile))
                {
                    var jsonContent = File.ReadAllText(reviewFile, System.Text.Encoding.UTF8);
                    var reviewData = System.Text.Json.JsonSerializer.Deserialize<ReviewFileData>(jsonContent);
                    
                    if (reviewData?.reviews != null)
                    {
                        foreach (var review in reviewData.reviews)
                        {
                            if (!string.IsNullOrEmpty(review.content))
                            {
                                reviews.Add($"⭐ {review.rating}/5 - {review.content}");
                            }
                        }
                    }
                }
                
                if (reviews.Count == 0)
                {
                    reviews.Add("리뷰 없음");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ 리뷰 읽기 오류: {ex.Message}");
                reviews.Add("리뷰 읽기 오류");
            }
            return reviews;
        }

        // 실제 상품 이미지 카드 추가 메서드 (원본 더미데이터와 완전히 똑같이)
        public void AddProductImageCard(string storeId, string productId, string imageUrl)
        {
            try
            {
                var container = this.FindControl<StackPanel>("RealDataContainer");
                if (container == null) return;

                // 전체 상품 컨테이너
                var productContainer = new StackPanel { Spacing = 0, Margin = new Thickness(0, 0, 0, 40) };

                // 1. 카테고리 경로 (체크박스 + 빨간 점 + 텍스트)
                var categoryPanel = new StackPanel 
                { 
                    Orientation = Orientation.Horizontal, 
                    Spacing = 8, 
                    Margin = new Thickness(0, 0, 0, 15)
                };

                var checkBox = new CheckBox { VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center };
                var redDot = new Ellipse 
                { 
                    Width = 8, 
                    Height = 8, 
                    Fill = new SolidColorBrush(Colors.Red),
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
                };
                var categoryText = new TextBlock 
                { 
                    Text = "카테고리 : 홈 > 가구 > 인테", 
                    FontSize = 13,
                    FontFamily = new FontFamily("Malgun Gothic"),
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
                };

                categoryPanel.Children.Add(checkBox);
                categoryPanel.Children.Add(redDot);
                categoryPanel.Children.Add(categoryText);

                // 2. 메인 상품 영역 (이미지 + 정보 + 버튼)
                var mainGrid = new Grid { Margin = new Thickness(0, 0, 0, 20) };
                mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(200) }); // 이미지
                mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // 정보
                mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) }); // 버튼

                // 왼쪽 상품 이미지
                var imageBorder = new Border
                {
                    Width = 180,
                    Height = 180,
                    Background = new SolidColorBrush(Color.Parse("#F5F5F5")),
                    CornerRadius = new CornerRadius(8)
                };
                Grid.SetColumn(imageBorder, 0);

                var image = new Image { Stretch = Stretch.Uniform, Margin = new Thickness(10) };
                try
                {
                    if (File.Exists(imageUrl))
                    {
                        var bitmap = new Avalonia.Media.Imaging.Bitmap(imageUrl);
                        image.Source = bitmap;
                    }
                    else
                    {
                        image.Source = new Avalonia.Media.Imaging.Bitmap(AssetLoader.Open(new Uri("avares://Gumaedaehang/images/product1.png")));
                    }
                }
                catch
                {
                    image.Source = new Avalonia.Media.Imaging.Bitmap(AssetLoader.Open(new Uri("avares://Gumaedaehang/images/product1.png")));
                }
                imageBorder.Child = image;

                // 중간 정보 패널
                var infoPanel = new StackPanel 
                { 
                    Margin = new Thickness(20, 0, 20, 0),
                    Spacing = 15
                };
                Grid.SetColumn(infoPanel, 1);

                // 상품명 라벨 (녹색 점)
                var nameLabel = new StackPanel 
                { 
                    Orientation = Orientation.Horizontal, 
                    Spacing = 8
                };
                var greenDot = new Ellipse 
                { 
                    Width = 8, 
                    Height = 8, 
                    Fill = new SolidColorBrush(Colors.Green),
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
                };
                var nameText = new TextBlock 
                { 
                    Text = "상품명 :", 
                    FontSize = 14,
                    FontFamily = new FontFamily("Malgun Gothic"),
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
                };
                nameLabel.Children.Add(greenDot);
                nameLabel.Children.Add(nameText);

                // 상품명 입력박스 (주황색 테두리, 넓게)
                var nameInputBorder = new Border
                {
                    BorderBrush = new SolidColorBrush(Color.Parse("#FF8A46")),
                    BorderThickness = new Thickness(2),
                    CornerRadius = new CornerRadius(6),
                    Padding = new Thickness(15, 12),
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch
                };

                var nameInputGrid = new Grid();
                nameInputGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                nameInputGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var nameInputText = new TextBlock 
                { 
                    Text = GetOriginalProductName(storeId, productId), 
                    FontSize = 14,
                    FontFamily = new FontFamily("Malgun Gothic"),
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
                };
                var byteCountText = new TextBlock 
                { 
                    Text = "19/50 byte", 
                    FontSize = 12, 
                    FontFamily = new FontFamily("Malgun Gothic"),
                    Foreground = new SolidColorBrush(Colors.Gray),
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
                };

                Grid.SetColumn(nameInputText, 0);
                Grid.SetColumn(byteCountText, 1);
                nameInputGrid.Children.Add(nameInputText);
                nameInputGrid.Children.Add(byteCountText);
                nameInputBorder.Child = nameInputGrid;

                // 원상품명 (실제 크롤링된 상품명 표시)
                var originalNameText = new TextBlock 
                { 
                    Text = GetOriginalProductName(storeId, productId), 
                    FontSize = 13,
                    FontFamily = new FontFamily("Malgun Gothic")
                };

                // 키워드 태그들
                var keywordPanel = new WrapPanel();
                var keyword1 = CreateKeywordTag("가베트345", true);
                var keyword2 = CreateKeywordTag("가베트-553422", true);  
                var keyword3 = CreateKeywordTag("바나나", false);
                
                keywordPanel.Children.Add(keyword1);
                keywordPanel.Children.Add(keyword2);
                keywordPanel.Children.Add(keyword3);

                // 키워드 입력 + 추가 버튼
                var keywordInputPanel = new StackPanel 
                { 
                    Orientation = Orientation.Horizontal, 
                    Spacing = 8
                };
                var keywordInput = new TextBox 
                { 
                    Width = 120, 
                    Height = 30,
                    FontSize = 12,
                    FontFamily = new FontFamily("Malgun Gothic"),
                    Watermark = "키워드 입력"
                };
                var addButton = new Button 
                { 
                    Content = "추가", 
                    Width = 50, 
                    Height = 30,
                    FontSize = 12,
                    FontFamily = new FontFamily("Malgun Gothic"),
                    Background = new SolidColorBrush(Color.Parse("#FF8A46")),
                    Foreground = new SolidColorBrush(Colors.White),
                    HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Center
                };
                keywordInputPanel.Children.Add(keywordInput);
                keywordInputPanel.Children.Add(addButton);

                // 정보 패널에 모든 요소 추가
                infoPanel.Children.Add(nameLabel);
                infoPanel.Children.Add(nameInputBorder);
                infoPanel.Children.Add(originalNameText);
                infoPanel.Children.Add(keywordPanel);
                infoPanel.Children.Add(keywordInputPanel);

                // 우측 버튼들 (세로 배치)
                var buttonPanel = new StackPanel 
                { 
                    Spacing = 10,
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top,
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right
                };
                Grid.SetColumn(buttonPanel, 2);

                var deleteButton = new Button 
                { 
                    Content = "삭제", 
                    Width = 120, 
                    Height = 35,
                    FontSize = 13,
                    FontFamily = new FontFamily("Malgun Gothic"),
                    Background = new SolidColorBrush(Color.Parse("#FF8A46")),
                    Foreground = new SolidColorBrush(Colors.White),
                    HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Center
                };
                var holdButton = new Button 
                { 
                    Content = "상품 보류", 
                    Width = 120, 
                    Height = 35,
                    FontSize = 13,
                    FontFamily = new FontFamily("Malgun Gothic"),
                    Background = new SolidColorBrush(Color.Parse("#CCCCCC")),
                    Foreground = new SolidColorBrush(Colors.Black),
                    HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Center
                };

                buttonPanel.Children.Add(deleteButton);
                buttonPanel.Children.Add(holdButton);

                // 그리드에 모든 요소 추가
                mainGrid.Children.Add(imageBorder);
                mainGrid.Children.Add(infoPanel);
                mainGrid.Children.Add(buttonPanel);

                // 3. 하단 리뷰 영역 (주황색 테두리 - 리뷰만)
                var reviewBorder = new Border
                {
                    BorderBrush = new SolidColorBrush(Color.Parse("#FF8A46")),
                    BorderThickness = new Thickness(2),
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(20, 15)
                };

                var reviewPanel = new StackPanel { Spacing = 8 };

                // 실제 크롤링된 리뷰 데이터 표시
                var reviewTexts = GetProductReviews(storeId, productId);
                foreach (var reviewText in reviewTexts)
                {
                    var reviewBlock = new TextBlock 
                    { 
                        Text = reviewText, 
                        FontSize = 12,
                        FontFamily = new FontFamily("Malgun Gothic")
                    };
                    reviewPanel.Children.Add(reviewBlock);
                }
                
                // 리뷰가 없으면 기본 메시지 표시
                if (reviewTexts.Count == 0)
                {
                    var noReviewText = new TextBlock 
                    { 
                        Text = "리뷰 데이터 로드 중...", 
                        FontSize = 12,
                        FontFamily = new FontFamily("Malgun Gothic"),
                        Foreground = new SolidColorBrush(Colors.Gray)
                    };
                    reviewPanel.Children.Add(noReviewText);
                }
                reviewBorder.Child = reviewPanel;

                // 4. 타오바오 페어링 (주황색 테두리 밖에 별도로)
                var pairingPanel = new StackPanel 
                { 
                    Orientation = Orientation.Horizontal, 
                    Spacing = 10,
                    Margin = new Thickness(0, 15, 0, 15)
                };
                var redDot2 = new Ellipse 
                { 
                    Width = 8, 
                    Height = 8, 
                    Fill = new SolidColorBrush(Colors.Red),
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
                };
                var pairingTitle = new TextBlock 
                { 
                    Text = "타오바오와 페어링", 
                    FontSize = 14,
                    FontFamily = new FontFamily("Malgun Gothic"),
                    FontWeight = FontWeight.Medium,
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
                };
                var pairingButton = new Button 
                { 
                    Content = "페어링", 
                    Width = 70, 
                    Height = 30,
                    FontSize = 12,
                    FontFamily = new FontFamily("Malgun Gothic"),
                    Background = new SolidColorBrush(Color.Parse("#FF8A46")),
                    Foreground = new SolidColorBrush(Colors.White),
                    HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                    VerticalContentAlignment = Avalonia.Layout.VerticalAlignment.Center
                };

                pairingPanel.Children.Add(redDot2);
                pairingPanel.Children.Add(pairingTitle);
                pairingPanel.Children.Add(pairingButton);

                // 5. 상품박스 3개 (PREDVIA 로고)
                var productBoxPanel = new StackPanel 
                { 
                    Orientation = Orientation.Horizontal, 
                    Spacing = 20,
                    Margin = new Thickness(0, 10, 0, 0)
                };

                for (int i = 0; i < 3; i++)
                {
                    var productBox = new StackPanel { Spacing = 10 };
                    
                    // PREDVIA 로고 박스
                    var logoBorder = new Border
                    {
                        Width = 160,
                        Height = 120,
                        Background = new SolidColorBrush(Color.Parse("#F5F5F5")),
                        CornerRadius = new CornerRadius(8),
                        Child = new TextBlock
                        {
                            Text = "🔺 PREDVIA",
                            FontSize = 16,
                            FontFamily = new FontFamily("Malgun Gothic"),
                            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                            Foreground = new SolidColorBrush(Color.Parse("#FF8A46"))
                        }
                    };
                    
                    // 페어링 텍스트
                    var pairingText = new TextBlock
                    {
                        Text = "페어링",
                        FontSize = 12,
                        FontFamily = new FontFamily("Malgun Gothic"),
                        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center
                    };
                    
                    productBox.Children.Add(logoBorder);
                    productBox.Children.Add(pairingText);
                    productBoxPanel.Children.Add(productBox);
                }

                // 전체 컨테이너에 추가
                productContainer.Children.Add(categoryPanel);
                productContainer.Children.Add(mainGrid);
                productContainer.Children.Add(reviewBorder);  // 주황색 테두리 (리뷰만)
                productContainer.Children.Add(pairingPanel);  // 타오바오 페어링 (별도)
                productContainer.Children.Add(productBoxPanel); // 상품박스 3개

                container.Children.Add(productContainer);

                Debug.WriteLine($"✅ 원본과 완전히 똑같은 카드 추가: {storeId}_{productId}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ 상품 카드 추가 실패: {ex.Message}");
            }
        }

        // 키워드 태그 생성 헬퍼 메서드
        private Border CreateKeywordTag(string text, bool isSelected)
        {
            return new Border
            {
                Background = isSelected ? new SolidColorBrush(Color.Parse("#FF8A46")) : new SolidColorBrush(Colors.Transparent),
                BorderBrush = new SolidColorBrush(Color.Parse("#FF8A46")),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(10, 4),
                Margin = new Thickness(0, 0, 6, 4),
                Child = new TextBlock
                {
                    Text = text,
                    FontSize = 11,
                    FontFamily = new FontFamily("Malgun Gothic"),
                    Foreground = isSelected ? new SolidColorBrush(Colors.White) : new SolidColorBrush(Color.Parse("#FF8A46"))
                }
            };
        }
        
        // 실제 크롤링된 상품명 가져오기
        private string GetOriginalProductName(string storeId, string productId)
        {
            try
            {
                var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                var productDataPath = System.IO.Path.Combine(appDataPath, "Predvia", "ProductData");
                var nameFile = System.IO.Path.Combine(productDataPath, $"{storeId}_{productId}_name.txt");
                
                if (File.Exists(nameFile))
                {
                    var productName = File.ReadAllText(nameFile, System.Text.Encoding.UTF8).Trim();
                    return $"원상품명: {productName}";
                }
                else
                {
                    return "원상품명: 상품명 로드 중...";
                }
            }
            catch
            {
                return "원상품명: 상품명 로드 실패";
            }
        }
        
        // 실제 크롤링된 리뷰 데이터 가져오기
        private List<string> GetProductReviews(string storeId, string productId)
        {
            var reviews = new List<string>();
            
            try
            {
                var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                var reviewsPath = System.IO.Path.Combine(appDataPath, "Predvia", "Reviews");
                var reviewFile = System.IO.Path.Combine(reviewsPath, $"{storeId}_{productId}_reviews.json");
                
                if (File.Exists(reviewFile))
                {
                    var reviewJson = File.ReadAllText(reviewFile, System.Text.Encoding.UTF8);
                    
                    // 간단한 JSON 파싱 (System.Text.Json 사용)
                    using var document = System.Text.Json.JsonDocument.Parse(reviewJson);
                    var root = document.RootElement;
                    
                    if (root.TryGetProperty("reviews", out var reviewsArray))
                    {
                        foreach (var review in reviewsArray.EnumerateArray())
                        {
                            var rating = review.TryGetProperty("rating", out var ratingElement) ? ratingElement.GetDouble() : 0.0;
                            var ratingText = review.TryGetProperty("ratingText", out var ratingTextElement) ? ratingTextElement.GetString() : "";
                            var recentRating = review.TryGetProperty("recentRating", out var recentRatingElement) ? recentRatingElement.GetString() : "";
                            var content = review.TryGetProperty("content", out var contentElement) ? contentElement.GetString() : "평점 정보 없음";
                            
                            // 별점 표시 (소수점 지원)
                            var stars = rating switch
                            {
                                >= 4.5 => "⭐⭐⭐⭐⭐",
                                >= 3.5 => "⭐⭐⭐⭐",
                                >= 2.5 => "⭐⭐⭐",
                                >= 1.5 => "⭐⭐",
                                >= 0.5 => "⭐",
                                _ => "☆"
                            };
                            
                            // 새로운 형식으로 표시
                            var displayText = $"{content} {stars}";
                            if (!string.IsNullOrEmpty(recentRating))
                            {
                                displayText += $" {recentRating}";
                            }
                            
                            reviews.Add(displayText);
                            
                            // 최대 3개 리뷰만 표시
                            if (reviews.Count >= 3) break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"리뷰 로드 오류: {ex.Message}");
            }
            
            return reviews;
        }
        
        // 이벤트 핸들러 등록
        private void RegisterEventHandlers()
        {
            // 공통 이벤트 핸들러
            if (_addMoreLink != null)
                _addMoreLink.PointerPressed += AddMoreLink_Click;
                
            if (_testDataButton != null)
                _testDataButton.Click += TestDataButton_Click;
                
            if (_testDataButton2 != null)
                _testDataButton2.Click += TestDataButton_Click;
                
            if (_selectAllCheckBox != null)
            {
                _selectAllCheckBox.IsCheckedChanged += SelectAllCheckBox_Changed;
            }
            
            // 상품별 이벤트 핸들러 등록
            foreach (var product in _productElements.Values)
            {
                RegisterProductEventHandlers(product);
            }
        }
        
        // 개별 상품의 이벤트 핸들러 등록
        private void RegisterProductEventHandlers(ProductUIElements product)
        {
            if (product.CheckBox != null)
            {
                product.CheckBox.IsCheckedChanged += (s, e) => ProductCheckBox_Changed(product.ProductId);
            }
            
            if (product.AddKeywordButton != null)
                product.AddKeywordButton.Click += (s, e) => AddKeywordButton_Click(product.ProductId);
                
            if (product.KeywordInputBox != null)
            {
                product.KeywordInputBox.KeyDown += (s, e) => KeywordInputBox_KeyDown(product.ProductId, e);
                
                // 한글 입력 처리를 위한 PropertyChanged 이벤트
                product.KeywordInputBox.PropertyChanged += (s, e) =>
                {
                    if (e.Property == TextBox.TextProperty)
                    {
                        _currentProductId = product.ProductId;
                        _inputTimer?.Stop();
                        _inputTimer?.Start();
                    }
                };
            }
                
            if (product.DeleteButton != null)
                product.DeleteButton.Click += (s, e) => DeleteButton_Click(product.ProductId);
                
            if (product.HoldButton != null)
                product.HoldButton.Click += (s, e) => HoldButton_Click(product.ProductId);
                
            if (product.TaobaoPairingButton != null)
                product.TaobaoPairingButton.Click += (s, e) => TaobaoPairingButton_Click(product.ProductId);
            
            // 키워드 클릭 이벤트 등록
            RegisterKeywordEvents(product);
            
            // 초기 상태 업데이트
            UpdateProductNameKeywordDisplay(product.ProductId);
            UpdateProductKeywordDisplay(product.ProductId);
            UpdateProductStatusIndicators(product.ProductId);
        }
        
        // 키워드 클릭 이벤트 등록
        private void RegisterKeywordEvents(ProductUIElements product)
        {
            var keywordBorders = new[] { 
                $"Product{product.ProductId}_Keyword1", 
                $"Product{product.ProductId}_Keyword2", 
                $"Product{product.ProductId}_Keyword3" 
            };
            
            foreach (var keywordName in keywordBorders)
            {
                var keyword = this.FindControl<Border>(keywordName);
                if (keyword != null)
                {
                    keyword.PointerPressed += (sender, e) => KeywordBorder_Click(product.ProductId, sender, e);
                }
            }
        }
        
        // 전체 선택 체크박스 변경 이벤트
        private void SelectAllCheckBox_Changed(object? sender, RoutedEventArgs e)
        {
            if (_selectAllCheckBox != null)
            {
                bool isChecked = _selectAllCheckBox.IsChecked ?? false;
                
                foreach (var product in _productElements.Values)
                {
                    if (product.CheckBox != null)
                    {
                        product.CheckBox.IsChecked = isChecked;
                    }
                }
            }
        }
        
        // 개별 상품 체크박스 변경 이벤트
        private void ProductCheckBox_Changed(int productId)
        {
            UpdateSelectAllCheckBoxState();
            Debug.WriteLine($"상품 {productId} 체크박스 상태 변경됨");
        }
        
        // 전체 선택 체크박스 상태 업데이트
        private void UpdateSelectAllCheckBoxState()
        {
            if (_selectAllCheckBox == null || _productElements.Count == 0)
                return;
            
            int checkedCount = 0;
            int totalCount = _productElements.Count;
            
            foreach (var product in _productElements.Values)
            {
                if (product.CheckBox?.IsChecked == true)
                {
                    checkedCount++;
                }
            }
            
            if (checkedCount == 0)
            {
                _selectAllCheckBox.IsChecked = false;
            }
            else if (checkedCount == totalCount)
            {
                _selectAllCheckBox.IsChecked = true;
            }
            else
            {
                _selectAllCheckBox.IsChecked = null; // 부분 선택
            }
        }
        
        // 키워드 추가 버튼 클릭 이벤트
        private void AddKeywordButton_Click(int productId)
        {
            if (_productElements.TryGetValue(productId, out var product))
            {
                AddKeywordFromInput(productId);
                Debug.WriteLine($"상품 {productId} 키워드 추가 버튼 클릭됨");
            }
        }
        
        // 한글 입력 처리용 타이머 이벤트
        private void InputTimer_Tick(object? sender, EventArgs e)
        {
            _inputTimer?.Stop();
            
            if (_productElements.TryGetValue(_currentProductId, out var product) && 
                product.KeywordInputBox != null)
            {
                var text = product.KeywordInputBox.Text;
                if (!string.IsNullOrEmpty(text))
                {
                    // 한글 조합 문자를 완성된 문자로 정규화
                    var normalizedText = text.Normalize(System.Text.NormalizationForm.FormC);
                    if (text != normalizedText)
                    {
                        var caretIndex = product.KeywordInputBox.CaretIndex;
                        product.KeywordInputBox.Text = normalizedText;
                        
                        // 커서 위치 복원
                        Dispatcher.UIThread.Post(() =>
                        {
                            product.KeywordInputBox.CaretIndex = Math.Min(caretIndex, normalizedText.Length);
                        });
                    }
                }
            }
        }
        
        // 키워드 입력창 키 이벤트
        private void KeywordInputBox_KeyDown(int productId, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                AddKeywordFromInput(productId);
                e.Handled = true;
            }
        }
        
        // 입력창에서 키워드 추가
        private void AddKeywordFromInput(int productId)
        {
            if (_productElements.TryGetValue(productId, out var product) && 
                product.KeywordInputBox != null && 
                !string.IsNullOrWhiteSpace(product.KeywordInputBox.Text))
            {
                // 한글 조합 문자를 완성된 문자로 정규화
                var rawText = product.KeywordInputBox.Text.Trim();
                var keyword = rawText.Normalize(System.Text.NormalizationForm.FormC);
                
                if (!string.IsNullOrEmpty(keyword) && !product.ProductNameKeywords.Contains(keyword))
                {
                    product.ProductNameKeywords.Add(keyword);
                    product.SelectedKeywords.Add(keyword);
                    UpdateProductNameKeywordDisplay(productId);
                    UpdateProductKeywordDisplay(productId);
                    product.KeywordInputBox.Text = "";
                }
            }
        }
        
        // 키워드 클릭 이벤트
        private void KeywordBorder_Click(int productId, object? sender, PointerPressedEventArgs e)
        {
            if (sender is Border border && border.Child is TextBlock textBlock && 
                _productElements.TryGetValue(productId, out var product))
            {
                var keywordText = textBlock.Text;
                if (keywordText != null)
                {
                    if (product.SelectedKeywords.Contains(keywordText))
                    {
                        product.SelectedKeywords.Remove(keywordText);
                        product.ProductNameKeywords.Remove(keywordText);
                        UpdateProductNameKeywordDisplay(productId);
                    }
                    else
                    {
                        product.SelectedKeywords.Add(keywordText);
                        if (!product.ProductNameKeywords.Contains(keywordText))
                        {
                            product.ProductNameKeywords.Add(keywordText);
                            UpdateProductNameKeywordDisplay(productId);
                        }
                    }
                    
                    UpdateProductKeywordDisplay(productId);
                }
            }
        }
        
        // 삭제 버튼 클릭 이벤트
        private void DeleteButton_Click(int productId)
        {
            Debug.WriteLine($"상품 {productId} 삭제 버튼 클릭됨");
        }
        
        // 상품 보류 버튼 클릭 이벤트
        private void HoldButton_Click(int productId)
        {
            Debug.WriteLine($"상품 {productId} 상품 보류 버튼 클릭됨");
        }
        
        // 타오바오 페어링 버튼 클릭 이벤트
        private async void TaobaoPairingButton_Click(int productId)
        {
            if (_productElements.TryGetValue(productId, out var product))
            {
                try
                {
                    // 버튼 비활성화
                    if (product.TaobaoPairingButton != null)
                    {
                        product.TaobaoPairingButton.IsEnabled = false;
                        product.TaobaoPairingButton.Content = "연결 중...";
                    }

                    // 선택된 키워드들을 조합하여 검색어 생성
                    var searchKeyword = string.Join(" ", product.SelectedKeywords);
                    
                    if (string.IsNullOrEmpty(searchKeyword))
                    {
                        // 키워드가 없으면 상품명 키워드 사용
                        searchKeyword = string.Join(" ", product.ProductNameKeywords);
                    }

                    if (!string.IsNullOrEmpty(searchKeyword))
                    {
                        // 네이버 스마트스토어 서비스 초기화
                        _naverService ??= new NaverSmartStoreService();
                        
                        // 네이버 스마트스토어 해외직구 페이지 열기
                        await _naverService.OpenNaverSmartStoreWithKeyword(searchKeyword);
                        
                        // 페어링 완료 처리
                        product.IsTaobaoPaired = true;
                        UpdateProductStatusIndicators(productId);
                        
                        Debug.WriteLine($"상품 {productId} 네이버 스마트스토어 연결 완료 - 키워드: {searchKeyword}");
                        
                        // 성공 메시지 표시
                        if (product.TaobaoPairingButton != null)
                        {
                            product.TaobaoPairingButton.Content = "연결 완료";
                            await Task.Delay(1500);
                        }
                    }
                    else
                    {
                        Debug.WriteLine($"상품 {productId} 검색 키워드가 없습니다.");
                        
                        // 키워드 없음 메시지 표시
                        if (product.TaobaoPairingButton != null)
                        {
                            product.TaobaoPairingButton.Content = "키워드 없음";
                            await Task.Delay(2000);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"네이버 스마트스토어 연결 실패: {ex.Message}");
                    
                    // 오류 메시지 표시
                    if (product.TaobaoPairingButton != null)
                    {
                        product.TaobaoPairingButton.Content = "연결 실패";
                        await Task.Delay(2000);
                    }
                }
                finally
                {
                    // 버튼 다시 활성화
                    if (product.TaobaoPairingButton != null)
                    {
                        product.TaobaoPairingButton.IsEnabled = true;
                        product.TaobaoPairingButton.Content = "페어링";
                    }
                }
            }
        }
        
        // 상품명 키워드 표시 업데이트
        private void UpdateProductNameKeywordDisplay(int productId)
        {
            if (_productElements.TryGetValue(productId, out var product) && 
                product.NameKeywordPanel != null)
            {
                product.NameKeywordPanel.Children.Clear();
                
                foreach (var keyword in product.ProductNameKeywords)
                {
                    var keywordTag = CreateKeywordTag(keyword, true, productId);
                    product.NameKeywordPanel.Children.Add(keywordTag);
                }
                
                UpdateProductByteCount(productId);
                UpdateProductStatusIndicators(productId);
            }
        }
        
        // 키워드 표시 업데이트
        private void UpdateProductKeywordDisplay(int productId)
        {
            // 키워드 패널의 색상 업데이트 로직
            var keywordBorders = new[] { 
                $"Product{productId}_Keyword1", 
                $"Product{productId}_Keyword2", 
                $"Product{productId}_Keyword3" 
            };
            
            if (_productElements.TryGetValue(productId, out var product))
            {
                foreach (var keywordName in keywordBorders)
                {
                    var keyword = this.FindControl<Border>(keywordName);
                    if (keyword != null && keyword.Child is TextBlock textBlock && textBlock.Text != null)
                    {
                        if (product.SelectedKeywords.Contains(textBlock.Text))
                        {
                            keyword.Background = ThemeManager.Instance.IsDarkTheme ? 
                                new SolidColorBrush(Color.Parse("#555555")) : 
                                new SolidColorBrush(Color.Parse("#D0D0D0"));
                            textBlock.Foreground = ThemeManager.Instance.IsDarkTheme ? 
                                new SolidColorBrush(Colors.LightGray) : 
                                new SolidColorBrush(Colors.Gray);
                        }
                        else
                        {
                            keyword.Background = new SolidColorBrush(Color.Parse("#F47B20"));
                            textBlock.Foreground = new SolidColorBrush(Colors.White);
                        }
                    }
                }
            }
        }
        
        // 바이트 수 계산 및 업데이트
        private void UpdateProductByteCount(int productId)
        {
            if (_productElements.TryGetValue(productId, out var product) && 
                product.ByteCountTextBlock != null)
            {
                var totalByteCount = 0;
                foreach (var keyword in product.ProductNameKeywords)
                {
                    totalByteCount += CalculateByteCount(keyword);
                }
                
                product.ByteCountTextBlock.Text = $"{totalByteCount}/50 byte";
                
                if (totalByteCount > 50)
                {
                    product.ByteCountTextBlock.Foreground = Brushes.Red;
                }
                else
                {
                    product.ByteCountTextBlock.Foreground = ThemeManager.Instance.IsDarkTheme ? Brushes.LightGray : Brushes.Gray;
                }
            }
        }
        
        // 상태 표시등 업데이트
        private void UpdateProductStatusIndicators(int productId)
        {
            if (_productElements.TryGetValue(productId, out var product))
            {
                bool isNameStatusGreen = false;
                bool isTaobaoPairingStatusGreen = false;
                
                // 상품명 바이트 수 표시등 업데이트
                if (product.NameStatusIndicator != null)
                {
                    var totalByteCount = 0;
                    foreach (var keyword in product.ProductNameKeywords)
                    {
                        totalByteCount += CalculateByteCount(keyword);
                    }
                    
                    if (totalByteCount <= 50)
                    {
                        product.NameStatusIndicator.Fill = new SolidColorBrush(Color.Parse("#53DA4C"));
                        isNameStatusGreen = true;
                    }
                    else
                    {
                        product.NameStatusIndicator.Fill = new SolidColorBrush(Color.Parse("#FF7272"));
                        isNameStatusGreen = false;
                    }
                }
                
                // 타오바오 페어링 상태 표시등 업데이트
                if (product.TaobaoPairingStatusIndicator != null)
                {
                    if (product.IsTaobaoPaired)
                    {
                        product.TaobaoPairingStatusIndicator.Fill = new SolidColorBrush(Color.Parse("#53DA4C"));
                        isTaobaoPairingStatusGreen = true;
                    }
                    else
                    {
                        product.TaobaoPairingStatusIndicator.Fill = new SolidColorBrush(Color.Parse("#FF7272"));
                        isTaobaoPairingStatusGreen = false;
                    }
                }
                
                // 카테고리 상태 표시등 업데이트 (상품명과 타오바오 페어링 상태에 따라)
                if (product.CategoryStatusIndicator != null)
                {
                    if (isNameStatusGreen && isTaobaoPairingStatusGreen)
                    {
                        // 둘 다 초록불이면 카테고리도 초록불
                        product.CategoryStatusIndicator.Fill = new SolidColorBrush(Color.Parse("#53DA4C"));
                    }
                    else
                    {
                        // 둘 중 하나라도 빨간불이면 카테고리도 빨간불
                        product.CategoryStatusIndicator.Fill = new SolidColorBrush(Color.Parse("#FF7272"));
                    }
                }
            }
        }
        
        // 한글 2바이트, 영어 1바이트로 계산
        private int CalculateByteCount(string text)
        {
            int byteCount = 0;
            foreach (char c in text)
            {
                if ((c >= 0xAC00 && c <= 0xD7AF) || 
                    (c >= 0x3131 && c <= 0x318E) || 
                    (c >= 0x1100 && c <= 0x11FF))
                {
                    byteCount += 2;
                }
                else
                {
                    byteCount += 1;
                }
            }
            return byteCount;
        }
        
        // 키워드 태그 생성 (상품명용 - 배경 없이 텍스트만)
        private StackPanel CreateKeywordTag(string keyword, bool isDeletable = false, int productId = 0)
        {
            var stackPanel = new StackPanel
            {
                Orientation = Avalonia.Layout.Orientation.Horizontal,
                Spacing = 4,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left,
                Margin = new Thickness(0, 0, 8, 0)
            };
            
            var textBlock = new TextBlock
            {
                Text = keyword,
                FontSize = 14,
                Foreground = ThemeManager.Instance.IsDarkTheme ? Brushes.White : new SolidColorBrush(Color.Parse("#333333")),
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                TextAlignment = Avalonia.Media.TextAlignment.Center
            };
            
            stackPanel.Children.Add(textBlock);
            
            if (isDeletable)
            {
                var deleteButton = new Button
                {
                    Width = 16,
                    Height = 16,
                    MinWidth = 16,
                    MinHeight = 16,
                    Background = Brushes.Transparent,
                    BorderThickness = new Thickness(0),
                    Padding = new Thickness(0),
                    Margin = new Thickness(0, 0, 0, 0),
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                    HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                    VerticalContentAlignment = Avalonia.Layout.VerticalAlignment.Center
                };
                
                // delete_keyword.png 이미지 로드
                try
                {
                    var deleteImage = new Image
                    {
                        Width = 12,
                        Height = 12,
                        Stretch = Avalonia.Media.Stretch.Uniform
                    };
                    
                    // Avalonia 11에서는 AssetLoader.Open을 직접 사용
                    try
                    {
                        var uri = new Uri("avares://Gumaedaehang/images/delete_keyword.png");
                        using var stream = AssetLoader.Open(uri);
                        deleteImage.Source = new Avalonia.Media.Imaging.Bitmap(stream);
                        deleteButton.Content = deleteImage;
                    }
                    catch
                    {
                        // 이미지 로드 실패 시 텍스트로 대체
                        deleteButton.Content = "×";
                        deleteButton.FontSize = 12;
                        deleteButton.FontWeight = FontWeight.Bold;
                        deleteButton.Foreground = new SolidColorBrush(Color.Parse("#666666"));
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"delete_keyword.png 이미지 로드 실패: {ex.Message}");
                    // 이미지 로드 실패 시 텍스트로 대체
                    deleteButton.Content = "×";
                    deleteButton.FontSize = 12;
                    deleteButton.FontWeight = FontWeight.Bold;
                    deleteButton.Foreground = new SolidColorBrush(Color.Parse("#666666"));
                }
                
                deleteButton.Click += (s, e) => RemoveProductNameKeyword(productId, keyword);
                stackPanel.Children.Add(deleteButton);
            }
            
            return stackPanel;
        }
        
        // 상품명 키워드 삭제
        private void RemoveProductNameKeyword(int productId, string keyword)
        {
            if (_productElements.TryGetValue(productId, out var product))
            {
                product.ProductNameKeywords.Remove(keyword);
                product.SelectedKeywords.Remove(keyword);
                UpdateProductNameKeywordDisplay(productId);
                UpdateProductKeywordDisplay(productId);
            }
        }
        
        // 기타 이벤트 핸들러들
        private void AddMoreLink_Click(object? sender, PointerPressedEventArgs e)
        {
            Debug.WriteLine("추가하기+ 링크 클릭됨");
        }
        
        private void TestDataButton_Click(object? sender, RoutedEventArgs e)
        {
            try
            {
                // 기존 카드들 모두 제거
                var container = this.FindControl<StackPanel>("RealDataContainer");
                if (container != null)
                {
                    container.Children.Clear();
                }
                
                // 크롤링된 실제 데이터 로드
                LoadCrawledData();
                
                Debug.WriteLine("✅ 실제 크롤링 데이터 로드 완료");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ 테스트 데이터 버튼 오류: {ex.Message}");
            }
        }
        
        private void UpdateViewVisibility()
        {
            if (_noDataView != null && _dataAvailableView != null)
            {
                _noDataView.IsVisible = !_hasData;
                _dataAvailableView.IsVisible = _hasData;
            }
        }
        
        public void SetHasData(bool hasData)
        {
            _hasData = hasData;
            UpdateViewVisibility();
        }
        
        public void ResetData()
        {
            _hasData = false;
            
            foreach (var product in _productElements.Values)
            {
                product.IsTaobaoPaired = false;
                if (product.CheckBox != null)
                    product.CheckBox.IsChecked = false;
            }
            
            if (_selectAllCheckBox != null)
                _selectAllCheckBox.IsChecked = false;
                
            UpdateViewVisibility();
            
            foreach (var productId in _productElements.Keys)
            {
                UpdateProductStatusIndicators(productId);
            }
        }
        
        // TextBox 배경색을 강제로 업데이트하는 메서드
        private void UpdateTextBoxColors(string backgroundColor, string foregroundColor)
        {
            try
            {
                var backgroundBrush = Brush.Parse(backgroundColor);
                var foregroundBrush = Brush.Parse(foregroundColor);
                
                // 모든 TextBox 찾아서 색상 업데이트
                UpdateTextBoxColorsRecursive(this, backgroundBrush, foregroundBrush);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"TextBox 색상 업데이트 실패: {ex.Message}");
            }
        }
        
        // 재귀적으로 TextBox를 찾아서 색상 업데이트
        private void UpdateTextBoxColorsRecursive(Control parent, IBrush backgroundBrush, IBrush foregroundBrush)
        {
            if (parent is TextBox textBox)
            {
                textBox.Background = backgroundBrush;
                textBox.Foreground = foregroundBrush;
            }
            
            if (parent is Panel panel)
            {
                foreach (Control child in panel.Children)
                {
                    UpdateTextBoxColorsRecursive(child, backgroundBrush, foregroundBrush);
                }
            }
            else if (parent is ContentControl contentControl && contentControl.Content is Control childControl)
            {
                UpdateTextBoxColorsRecursive(childControl, backgroundBrush, foregroundBrush);
            }
            else if (parent is Decorator decorator && decorator.Child is Control decoratorChild)
            {
                UpdateTextBoxColorsRecursive(decoratorChild, backgroundBrush, foregroundBrush);
            }
        }
        
        // 수동으로 소싱하기 페어링 버튼 클릭
        private async void ManualSourcingButton_Click(object? sender, RoutedEventArgs e)
        {
            await HandlePairingButtonClick(_manualSourcingTextBox, _manualSourcingButton, "수동 소싱");
        }
        
        // 소싱재료 자동찾기 페어링 버튼 클릭
        private async void AutoSourcingButton_Click(object? sender, RoutedEventArgs e)
        {
            await HandlePairingButtonClick(_autoSourcingTextBox, _autoSourcingButton, "자동 소싱");
        }
        
        // 🧹 기존 크롤링 데이터 초기화 메서드 (조용한 버전 - 생성자용)
        private void ClearPreviousCrawlingDataSilent()
        {
            try
            {
                var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                var predviaPath = System.IO.Path.Combine(appDataPath, "Predvia");
                
                int totalDeleted = 0;
                
                // 이미지 폴더 초기화
                var imagesPath = System.IO.Path.Combine(predviaPath, "Images");
                if (Directory.Exists(imagesPath))
                {
                    var fileCount = Directory.GetFiles(imagesPath).Length;
                    Directory.Delete(imagesPath, true);
                    totalDeleted += fileCount;
                }
                
                // 상품명 폴더 초기화
                var productDataPath = System.IO.Path.Combine(predviaPath, "ProductData");
                if (Directory.Exists(productDataPath))
                {
                    var fileCount = Directory.GetFiles(productDataPath).Length;
                    Directory.Delete(productDataPath, true);
                    totalDeleted += fileCount;
                }
                
                // 리뷰 폴더 초기화
                var reviewsPath = System.IO.Path.Combine(predviaPath, "Reviews");
                if (Directory.Exists(reviewsPath))
                {
                    var fileCount = Directory.GetFiles(reviewsPath).Length;
                    Directory.Delete(reviewsPath, true);
                    totalDeleted += fileCount;
                }
                
                // UI에서 기존 카드들 제거
                Dispatcher.UIThread.Post(() =>
                {
                    if (RealDataContainer != null)
                    {
                        var cardCount = RealDataContainer.Children.Count;
                        RealDataContainer.Children.Clear();
                        
                        // 작업로그에 초기화 완료 메시지 추가
                        if (totalDeleted > 0 || cardCount > 0)
                        {
                            LogWindow.AddLogStatic($"🧹 프로그램 시작 시 자동 초기화 완료 (파일 {totalDeleted}개, 카드 {cardCount}개 삭제)");
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                // 오류 시에도 로그에 표시
                Dispatcher.UIThread.Post(() =>
                {
                    LogWindow.AddLogStatic($"❌ 자동 초기화 오류: {ex.Message}");
                });
            }
        }
        
        // 🧹 기존 크롤링 데이터 초기화 메서드
        private void ClearPreviousCrawlingData()
        {
            try
            {
                Debug.WriteLine("🧹 ClearPreviousCrawlingData 시작");
                LogWindow.AddLogStatic("🧹 기존 크롤링 데이터 초기화 시작");
                
                var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                var predviaPath = System.IO.Path.Combine(appDataPath, "Predvia");
                
                Debug.WriteLine($"AppData 경로: {appDataPath}");
                Debug.WriteLine($"Predvia 경로: {predviaPath}");
                
                // 이미지 폴더 초기화
                var imagesPath = System.IO.Path.Combine(predviaPath, "Images");
                Debug.WriteLine($"이미지 폴더 경로: {imagesPath}");
                if (Directory.Exists(imagesPath))
                {
                    var fileCount = Directory.GetFiles(imagesPath).Length;
                    Debug.WriteLine($"삭제할 이미지 파일 개수: {fileCount}");
                    Directory.Delete(imagesPath, true);
                    LogWindow.AddLogStatic($"🗑️ 기존 이미지 파일들 삭제 완료 ({fileCount}개)");
                }
                else
                {
                    Debug.WriteLine("이미지 폴더가 존재하지 않음");
                }
                
                // 상품명 폴더 초기화
                var productDataPath = System.IO.Path.Combine(predviaPath, "ProductData");
                Debug.WriteLine($"상품명 폴더 경로: {productDataPath}");
                if (Directory.Exists(productDataPath))
                {
                    var fileCount = Directory.GetFiles(productDataPath).Length;
                    Debug.WriteLine($"삭제할 상품명 파일 개수: {fileCount}");
                    Directory.Delete(productDataPath, true);
                    LogWindow.AddLogStatic($"🗑️ 기존 상품명 파일들 삭제 완료 ({fileCount}개)");
                }
                else
                {
                    Debug.WriteLine("상품명 폴더가 존재하지 않음");
                }
                
                // 리뷰 폴더 초기화
                var reviewsPath = System.IO.Path.Combine(predviaPath, "Reviews");
                Debug.WriteLine($"리뷰 폴더 경로: {reviewsPath}");
                if (Directory.Exists(reviewsPath))
                {
                    var fileCount = Directory.GetFiles(reviewsPath).Length;
                    Debug.WriteLine($"삭제할 리뷰 파일 개수: {fileCount}");
                    Directory.Delete(reviewsPath, true);
                    LogWindow.AddLogStatic($"🗑️ 기존 리뷰 파일들 삭제 완료 ({fileCount}개)");
                }
                else
                {
                    Debug.WriteLine("리뷰 폴더가 존재하지 않음");
                }
                
                // UI에서 기존 카드들 제거
                Dispatcher.UIThread.Post(() =>
                {
                    if (RealDataContainer != null)
                    {
                        var cardCount = RealDataContainer.Children.Count;
                        RealDataContainer.Children.Clear();
                        Debug.WriteLine($"UI 카드 {cardCount}개 제거 완료");
                        LogWindow.AddLogStatic($"🧹 UI 카드들 초기화 완료 ({cardCount}개)");
                    }
                    else
                    {
                        Debug.WriteLine("RealDataContainer가 null");
                    }
                });
                
                Debug.WriteLine("✅ 초기화 완료");
                LogWindow.AddLogStatic("✅ 기존 크롤링 데이터 초기화 완료");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ 초기화 오류: {ex.Message}");
                LogWindow.AddLogStatic($"❌ 데이터 초기화 오류: {ex.Message}");
            }
        }
        
        // 메인상품 자동찾기 페어링 버튼 클릭
        private async void MainProductButton_Click(object? sender, RoutedEventArgs e)
        {
            await HandlePairingButtonClick(_mainProductTextBox, _mainProductButton, "메인상품");
        }
        
        // 페어링 버튼 공통 처리 메서드
        private async Task HandlePairingButtonClick(TextBox? textBox, Button? button, string type)
        {
            Debug.WriteLine($"🔥 HandlePairingButtonClick 호출됨 - {type}");
            if (textBox == null || button == null) 
            {
                Debug.WriteLine($"❌ TextBox 또는 Button이 null - TextBox: {textBox != null}, Button: {button != null}");
                return;
            }
            
            try
            {
                button.IsEnabled = false;
                button.Content = "연결 중...";
                
                var searchText = textBox.Text?.Trim();
                if (string.IsNullOrEmpty(searchText))
                {
                    button.Content = "입력 필요";
                    await Task.Delay(2000);
                    return;
                }
                
                _extensionService ??= new ChromeExtensionService();
                var success = await _extensionService.SearchWithExtension(searchText);
                
                if (success)
                {
                    button.Content = "연결 완료";
                    Debug.WriteLine($"{type} 확장프로그램 검색 완료 - 키워드: {searchText}");
                }
                else
                {
                    button.Content = "연결 실패";
                    Debug.WriteLine($"{type} 확장프로그램 실행 실패");
                }
                await Task.Delay(1500);
            }
            catch (Exception)
            {
                button.Content = "연결 실패";
                await Task.Delay(2000);
            }
            finally
            {
                if (button != null)
                {
                    button.IsEnabled = true;
                    button.Content = "페어링";
                }
            }
        }
        
        // 리소스 정리
        public void Dispose()
        {
            try
            {
                _naverService?.Close();
                _naverService = null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"리소스 정리 중 오류: {ex.Message}");
            }
        }
        
        private void SetupPlaceholders()
        {
            try
            {
                var manualTextBox = this.FindControl<TextBox>("ManualSourcingTextBox");
                var materialTextBox = this.FindControl<TextBox>("SourcingMaterialTextBox");
                var mainProductTextBox = this.FindControl<TextBox>("MainProductTextBox");
                
                if (manualTextBox != null)
                    SetPlaceholder(manualTextBox, "URL을 입력해주세요.");
                if (materialTextBox != null)
                    SetPlaceholder(materialTextBox, "소싱재료를 입력해주세요.");
                if (mainProductTextBox != null)
                    SetPlaceholder(mainProductTextBox, "메인상품을 입력해주세요.");
            }
            catch { }
        }
        
        private void SetPlaceholder(TextBox textBox, string placeholder)
        {
            if (string.IsNullOrEmpty(textBox.Text))
            {
                textBox.Text = placeholder;
                textBox.Foreground = new SolidColorBrush(Color.Parse("#999999"));
            }
            
            textBox.GotFocus += (s, e) =>
            {
                if (textBox.Text == placeholder)
                {
                    textBox.Text = "";
                    textBox.Foreground = ThemeManager.Instance.IsDarkTheme ? 
                        new SolidColorBrush(Colors.White) : 
                        new SolidColorBrush(Color.Parse("#333333"));
                }
            };
            
            textBox.LostFocus += (s, e) =>
            {
                if (string.IsNullOrEmpty(textBox.Text))
                {
                    textBox.Text = placeholder;
                    textBox.Foreground = new SolidColorBrush(Color.Parse("#999999"));
                }
            };
        }
    }
    
    // 상품별 UI 요소들을 관리하는 클래스
    public class ProductUIElements
    {
        public int ProductId { get; set; }
        public CheckBox? CheckBox { get; set; }
        public Ellipse? CategoryStatusIndicator { get; set; }
        public Ellipse? NameStatusIndicator { get; set; }
        public WrapPanel? NameKeywordPanel { get; set; }
        public TextBlock? ByteCountTextBlock { get; set; }
        public WrapPanel? KeywordPanel { get; set; }
        public TextBox? KeywordInputBox { get; set; }
        public Button? AddKeywordButton { get; set; }
        public Button? DeleteButton { get; set; }
        public Button? HoldButton { get; set; }
        public Ellipse? TaobaoPairingStatusIndicator { get; set; }
        public Button? TaobaoPairingButton { get; set; }
        public List<string> ProductNameKeywords { get; set; } = new List<string>();
        public List<string> SelectedKeywords { get; set; } = new List<string>();
        public bool IsTaobaoPaired { get; set; } = false;
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
        if (parent is T item)
            result.Add(item);

        if (parent is Panel panel)
        {
            foreach (Control child in panel.Children)
                FindAllRecursive(child, result);
        }
        else if (parent is ContentControl contentControl && contentControl.Content is Control childControl)
        {
            FindAllRecursive(childControl, result);
        }
        else if (parent is Border border && border.Child is Control borderChild)
        {
            FindAllRecursive(borderChild, result);
        }
    }
}
