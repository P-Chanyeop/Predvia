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
using System.Text.Json.Serialization;
using System;
using System.Text;
using System.IO;
using System.Threading.Tasks;
using System.Net.Http;
using System.Text.Json;
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

    // 카테고리 데이터 구조
    public class CategoryData
    {
        [JsonPropertyName("storeId")]
        public string StoreId { get; set; } = "";
        
        [JsonPropertyName("categories")]
        public List<CategoryInfo> Categories { get; set; } = new();
        
        [JsonPropertyName("pageUrl")]
        public string PageUrl { get; set; } = "";
        
        [JsonPropertyName("extractedAt")]
        public string ExtractedAt { get; set; } = "";
    }

    public class CategoryInfo
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = "";
        
        [JsonPropertyName("url")]
        public string Url { get; set; } = "";
        
        [JsonPropertyName("categoryId")]
        public string CategoryId { get; set; } = "";
        
        [JsonPropertyName("order")]
        public int Order { get; set; }
    }
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
        private int _lastActiveProductId = 1; // 마지막으로 활성화된 상품 ID
        
        // 키워드 태그 자동 생성을 위한 타이머
        private DispatcherTimer? _keywordCheckTimer;
        private bool _keywordTagsCreated = false;
        private int _keywordSourceProductId = -1; // 키워드를 생성한 상품 ID 추적
        private Dictionary<int, List<string>> _productKeywords = new(); // 상품별 키워드 저장
        private ChromeExtensionService? _extensionService;
        
        // 상품별 UI 요소들을 관리하는 딕셔너리
        private Dictionary<int, ProductUIElements> _productElements = new Dictionary<int, ProductUIElements>();
        
        // 카테고리 데이터 캐시
        private Dictionary<string, CategoryData> _categoryDataCache = new Dictionary<string, CategoryData>();
        
        // 네이버 스마트스토어 서비스
        private NaverSmartStoreService? _naverService;
        
        // UI 요소 참조
        private TextBox? _manualSourcingTextBox;
        private Button? _manualSourcingButton;
        private TextBox? _autoSourcingTextBox;
        private Button? _autoSourcingButton;
        private TextBox? _mainProductTextBox;
        private Button? _mainProductButton;
        
        public SourcingPage()
        {
            try
            {
                InitializeComponent();
                
                // 🧹 프로그램 시작 시 자동 초기화 (조용히)
                ClearPreviousCrawlingDataSilent();
                
                // 초기화 시작 메시지 (지연 후 표시)
                Task.Delay(500).ContinueWith(_ =>
                {
                    Dispatcher.UIThread.Post(() =>
                    {
                        LogWindow.AddLogStatic("🧹 프로그램 시작 - 이전 크롤링 데이터 자동 초기화 중...");
                    });
                });
                
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
                
                // 키워드 체크 타이머는 "추가" 버튼 클릭 시에만 시작
                // StartKeywordCheckTimer(); // 제거
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
                
                // 테스트 로그 추가
                LogWindow.AddLogStatic("🔥 SourcingPage 초기화 완료 - 버튼 테스트 준비됨");
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
                var categoriesPath = System.IO.Path.Combine(predviaPath, "Categories");

                if (!Directory.Exists(imagesPath) || !Directory.Exists(productDataPath))
                {
                    return;
                }

                // 카테고리 데이터 먼저 로드
                Debug.WriteLine($"🔍 카테고리 폴더 확인: {categoriesPath}");
                if (Directory.Exists(categoriesPath))
                {
                    var categoryFiles = Directory.GetFiles(categoriesPath, "*_categories.json");
                    Debug.WriteLine($"🔍 카테고리 파일 개수: {categoryFiles.Length}개");
                    
                    foreach (var categoryFile in categoryFiles)
                    {
                        try
                        {
                            Debug.WriteLine($"🔍 카테고리 파일 로드 시도: {System.IO.Path.GetFileName(categoryFile)}");
                            var json = File.ReadAllText(categoryFile, System.Text.Encoding.UTF8);
                            Debug.WriteLine($"🔍 JSON 내용 길이: {json.Length} 문자");
                            
                            var categoryData = JsonSerializer.Deserialize<CategoryData>(json);
                            
                            if (categoryData != null)
                            {
                                _categoryDataCache[categoryData.StoreId] = categoryData;
                                Debug.WriteLine($"📂 카테고리 데이터 로드 성공: {categoryData.StoreId} - {categoryData.Categories.Count}개");
                                
                                // 카테고리 내용도 출력
                                foreach (var cat in categoryData.Categories)
                                {
                                    Debug.WriteLine($"   - {cat.Name} (순서: {cat.Order})");
                                }
                            }
                            else
                            {
                                Debug.WriteLine($"❌ 카테고리 데이터 역직렬화 실패: {System.IO.Path.GetFileName(categoryFile)}");
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"❌ 카테고리 파일 로드 오류: {System.IO.Path.GetFileName(categoryFile)} - {ex.Message}");
                        }
                    }
                    
                    Debug.WriteLine($"🔍 최종 카테고리 캐시 상태: {_categoryDataCache.Count}개 스토어");
                }
                else
                {
                    Debug.WriteLine($"⚠️ 카테고리 폴더 없음: {categoriesPath}");
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

        // 카테고리 정보 가져오기 - 개별 상품 카테고리 파일에서 직접 읽기
        private string GetCategoryInfo(string storeId, string productId = "")
        {
            try
            {
                Debug.WriteLine($"🔍 GetCategoryInfo 호출: storeId = '{storeId}', productId = '{productId}'");
                
                var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                var categoriesPath = System.IO.Path.Combine(appDataPath, "Predvia", "Categories");
                
                // 개별 상품 카테고리 파일 우선 확인
                if (!string.IsNullOrEmpty(productId))
                {
                    var productCategoryFile = System.IO.Path.Combine(categoriesPath, $"{storeId}_{productId}_categories.json");
                    if (File.Exists(productCategoryFile))
                    {
                        Debug.WriteLine($"🔍 개별 상품 카테고리 파일 발견: {productCategoryFile}");
                        var json = File.ReadAllText(productCategoryFile);
                        var categoryData = JsonSerializer.Deserialize<CategoryData>(json);
                        
                        if (categoryData?.Categories != null)
                        {
                            var categoryNames = categoryData.Categories
                                .Where(c => !string.IsNullOrEmpty(c.Name) && 
                                           c.Name != "전체상품" && 
                                           c.Name != "홈" && 
                                           c.Name != "Home")
                                .Select(c => c.Name)
                                .ToList();
                            
                            if (categoryNames.Count > 0)
                            {
                                var result = string.Join(" > ", categoryNames);
                                Debug.WriteLine($"✅ 개별 상품 카테고리 결과: '{result}'");
                                return result;
                            }
                        }
                    }
                }
                
                // 캐시에서 확인 (전체 스토어 카테고리)
                if (_categoryDataCache.ContainsKey(storeId))
                {
                    var cachedData = _categoryDataCache[storeId];
                    Debug.WriteLine($"🔍 캐시에서 발견: {storeId} - 카테고리 {cachedData.Categories.Count}개");
                    
                    var categoryNames = cachedData.Categories
                        .Where(c => !string.IsNullOrEmpty(c.Name) && 
                                   c.Name != "전체상품" && 
                                   c.Name != "홈" && 
                                   c.Name != "Home")
                        .Select(c => c.Name)
                        .ToList();
                    
                    Debug.WriteLine($"🔍 필터링된 카테고리: [{string.Join(", ", categoryNames)}]");
                    
                    if (categoryNames.Count > 0)
                    {
                        var result = string.Join(" > ", categoryNames);
                        Debug.WriteLine($"✅ 최종 카테고리 결과: '{result}'");
                        return result;
                    }
                    else
                    {
                        Debug.WriteLine($"⚠️ {storeId}: 유효한 카테고리 없음 (전체상품만 있음)");
                        return "카테고리 없음";
                    }
                }
                
                Debug.WriteLine($"⚠️ {storeId}: 캐시에 카테고리 없음");
                return "카테고리 로드 안됨";
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ 카테고리 오류: {ex.Message}");
                return "카테고리 오류";
            }
        }

        // 크롤링된 상품명 읽기
        private string GetOriginalProductName(string storeId, string productId)
        {
            try
            {
                var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                var productDataPath = System.IO.Path.Combine(appDataPath, "Predvia", "ProductData");
                var nameFile = System.IO.Path.Combine(productDataPath, $"{storeId}_{productId}_name.txt");
                
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
                var reviewsPath = System.IO.Path.Combine(appDataPath, "Predvia", "Reviews");
                var reviewFile = System.IO.Path.Combine(reviewsPath, $"{storeId}_{productId}_reviews.json");
                
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

        // 테스트용 카테고리 데이터 생성 (비활성화)
        private void CreateTestCategoryData()
        {
            // 더미 데이터 생성 비활성화 - 실제 크롤링 데이터만 사용
            Debug.WriteLine("🚫 더미 카테고리 데이터 생성 비활성화 - 실제 크롤링 데이터만 사용");
        }

        // 카테고리 데이터 추가 메서드
        public void AddCategoryData(CategoryData categoryData)
        {
            try
            {
                Debug.WriteLine($"📂 카테고리 데이터 추가: {categoryData.StoreId} - {categoryData.Categories.Count}개");
                
                // 카테고리 정보를 상품 카드에 표시하기 위해 저장
                // 실제로는 각 상품 카드의 카테고리 정보를 업데이트해야 함
                
                // 로그 출력
                foreach (var category in categoryData.Categories)
                {
                    Debug.WriteLine($"  - {category.Name} (순서: {category.Order})");
                }
                
                // 카테고리 데이터를 메모리에 저장 (나중에 상품 카드에서 사용)
                _categoryDataCache[categoryData.StoreId] = categoryData;
                
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ 카테고리 데이터 추가 오류: {ex.Message}");
            }
        }

        // 카테고리 캐시 새로고침 (크롤링 완료 후 호출)
        public void RefreshCategoryCache()
        {
            try
            {
                var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                var categoriesPath = System.IO.Path.Combine(appDataPath, "Predvia", "Categories");
                
                if (Directory.Exists(categoriesPath))
                {
                    var categoryFiles = Directory.GetFiles(categoriesPath, "*_categories.json");
                    Debug.WriteLine($"🔄 카테고리 캐시 새로고침: {categoryFiles.Length}개 파일 발견");
                    
                    foreach (var categoryFile in categoryFiles)
                    {
                        try
                        {
                            var json = File.ReadAllText(categoryFile, System.Text.Encoding.UTF8);
                            var categoryData = JsonSerializer.Deserialize<CategoryData>(json);
                            
                            if (categoryData != null)
                            {
                                _categoryDataCache[categoryData.StoreId] = categoryData;
                                Debug.WriteLine($"🔄 카테고리 캐시 업데이트: {categoryData.StoreId} - {categoryData.Categories.Count}개");
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"❌ 카테고리 파일 로드 오류: {ex.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ 카테고리 캐시 새로고침 오류: {ex.Message}");
            }
        }

        // 실제 상품 이미지 카드 추가 메서드 (원본 더미데이터와 완전히 똑같이)
        public void AddProductImageCard(string storeId, string productId, string imageUrl)
        {
            try
            {
                var container = this.FindControl<StackPanel>("RealDataContainer");
                if (container == null) return;

                // ⭐ 카드 순서 기반 ID 생성 (1부터 시작) - 추가 전에 미리 계산
                var cardId = container.Children.OfType<StackPanel>().Count() + 1;
                LogWindow.AddLogStatic($"🆔 새 카드 ID 생성: {cardId}");

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
                    Text = GetCategoryInfo(storeId, productId), // productId 전달
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

                var image = new LazyImage 
                { 
                    Stretch = Stretch.Uniform, 
                    Margin = new Thickness(10),
                    ImagePath = imageUrl
                };
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

                var nameInputText = new TextBox 
                { 
                    Text = "", 
                    FontSize = 14,
                    FontFamily = new FontFamily("Malgun Gothic"),
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                    Background = Brushes.Transparent,
                    BorderThickness = new Thickness(0)
                };
                var byteCountText = new TextBlock 
                { 
                    Text = "0/0 byte", 
                    FontSize = 12, 
                    FontFamily = new FontFamily("Malgun Gothic"),
                    Foreground = new SolidColorBrush(Colors.Gray),
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
                };

                // 바이트 계산 이벤트 연결
                nameInputText.TextChanged += (s, e) => UpdateByteCount(cardId, nameInputText, byteCountText);

                Grid.SetColumn(nameInputText, 0);
                Grid.SetColumn(byteCountText, 1);
                nameInputGrid.Children.Add(nameInputText);
                nameInputGrid.Children.Add(byteCountText);
                nameInputBorder.Child = nameInputGrid;

                // 중복 카테고리 제거됨

                // 원상품명 (실제 크롤링된 상품명 표시)
                var originalNameText = new TextBlock 
                { 
                    Text = "원상품명: " + GetOriginalProductName(storeId, productId), 
                    FontSize = 13,
                    FontFamily = new FontFamily("Malgun Gothic")
                };

                // 키워드 태그들 (더미데이터 제거됨)
                var keywordPanel = new WrapPanel();

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
                
                // 🔥 즉시 이벤트 연결 (버튼 생성 직후)
                addButton.Click += (s, e) => {
                    LogWindow.AddLogStatic($"🔥🔥🔥 추가 버튼 클릭 감지됨! CardId: {cardId}");
                    AddKeywordButton_Click(cardId);
                };
                
                keywordInputPanel.Children.Add(keywordInput);
                keywordInputPanel.Children.Add(addButton);

                // 상품명 직접 입력 + 첨부 버튼
                var nameDirectInputPanel = new StackPanel 
                { 
                    Orientation = Orientation.Horizontal, 
                    Spacing = 8
                };
                var nameDirectInput = new TextBox 
                { 
                    Width = 120, 
                    Height = 30,
                    FontSize = 12,
                    FontFamily = new FontFamily("Malgun Gothic"),
                    Watermark = "키워드 입력"
                };
                var attachButton = new Button 
                { 
                    Content = "첨부", 
                    Width = 50, 
                    Height = 30,
                    FontSize = 12,
                    FontFamily = new FontFamily("Malgun Gothic"),
                    Background = new SolidColorBrush(Color.Parse("#FF8A46")),
                    Foreground = new SolidColorBrush(Colors.White),
                    HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Center
                };
                
                // 첨부 버튼 이벤트 연결
                attachButton.Click += (s, e) => {
                    LogWindow.AddLogStatic($"📎 첨부 버튼 클릭 감지됨! CardId: {cardId}");
                    AttachNameButton_Click(cardId, nameDirectInput);
                };
                
                nameDirectInputPanel.Children.Add(nameDirectInput);
                nameDirectInputPanel.Children.Add(attachButton);

                // 정보 패널에 모든 요소 추가
                infoPanel.Children.Add(nameLabel);
                infoPanel.Children.Add(nameInputBorder);
                infoPanel.Children.Add(originalNameText);
                infoPanel.Children.Add(keywordPanel);
                infoPanel.Children.Add(keywordInputPanel);
                infoPanel.Children.Add(nameDirectInputPanel); // 새로운 첨부 패널 추가

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

                // ProductUIElements 생성 및 저장
                var productElement = new ProductUIElements
                {
                    ProductId = cardId,
                    NameInputBox = nameInputText,
                    ByteCountTextBlock = byteCountText,
                    KeywordPanel = keywordPanel,
                    KeywordInputBox = keywordInput,
                    AddKeywordButton = addButton
                };
                
                _productElements[cardId] = productElement;
                
                LogWindow.AddLogStatic($"✅ 상품 카드 생성 완료 - CardId: {cardId}");

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
        
        // 이벤트 핸들러 등록
        private void RegisterEventHandlers()
        {
            // 공통 이벤트 핸들러
            if (_addMoreLink != null)
                _addMoreLink.PointerPressed += AddMoreLink_Click;
                
            if (_testDataButton != null)
                _testDataButton.Click += TestDataButton_Click;
                
            if (_testDataButton2 != null)
                _testDataButton2.Click += TestDataButton2_Click;
                
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
                        _lastActiveProductId = product.ProductId;
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
        private async void AddKeywordButton_Click(int productId)
        {
            LogWindow.AddLogStatic($"🔥 키워드 추가 버튼 클릭됨 - 상품 ID: {productId}");
            
            // ⭐ 키워드 생성한 상품 ID 저장
            _keywordSourceProductId = productId;
            
            // ⭐ 키워드 태그 생성 플래그 리셋 (새 검색 허용)
            _keywordTagsCreated = false;
            
            // ⭐ 추가 버튼은 크롤링 플래그 리셋
            await ResetCrawlingAllowed();
            
            if (_productElements.TryGetValue(productId, out var product))
            {
                AddKeywordFromInput(productId);
                Debug.WriteLine($"상품 {productId} 키워드 추가 버튼 클릭됨");
                
                // 키워드 입력 박스에서 키워드 가져와서 네이버 가격비교 검색
                if (product.KeywordInputBox?.Text?.Trim() is { Length: > 0 } keyword)
                {
                    LogWindow.AddLogStatic($"🔍 입력된 키워드: {keyword} (크롤링 비활성화)");
                    await SearchNaverPriceComparison(keyword);
                }
                else
                {
                    LogWindow.AddLogStatic("❌ 키워드가 입력되지 않았습니다.");
                }
            }
            else
            {
                LogWindow.AddLogStatic($"❌ 상품 ID {productId}를 찾을 수 없습니다.");
            }
        }
        
        // 한글 입력 처리용 타이머 이벤트
        private void InputTimer_Tick(object? sender, EventArgs e)
        {
            _inputTimer?.Stop();
            
            if (_productElements.TryGetValue(_lastActiveProductId, out var product) && 
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
        
        // 입력창에서 키워드 추가 (UI 표시 안 함, 검색만)
        private async void AddKeywordFromInput(int productId)
        {
            if (_productElements.TryGetValue(productId, out var product) && 
                product.KeywordInputBox != null && 
                !string.IsNullOrWhiteSpace(product.KeywordInputBox.Text))
            {
                // 한글 조합 문자를 완성된 문자로 정규화
                var rawText = product.KeywordInputBox.Text.Trim();
                var keyword = rawText.Normalize(System.Text.NormalizationForm.FormC);
                
                if (!string.IsNullOrEmpty(keyword))
                {
                    product.KeywordInputBox.Text = "";
                    
                    // 🔍 네이버 가격비교에서 키워드 검색만 (UI 표시 안 함)
                    await SearchNaverPriceComparison(keyword);
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
        private async void AddMoreLink_Click(object? sender, PointerPressedEventArgs e)
        {
            LogWindow.AddLogStatic("🔥 추가하기+ 버튼 클릭됨!");
            Debug.WriteLine("추가하기+ 링크 클릭됨");
            
            // ⭐ 데이터 있는 화면으로 전환 + 키워드 복원
            _hasData = true;
            UpdateViewVisibility();
            
            // ⭐ 키워드 복원 (지연 실행)
            Dispatcher.UIThread.Post(() =>
            {
                RestoreSavedKeywords();
            }, DispatcherPriority.Background);
            
            // ⭐ 추가 버튼은 크롤링 플래그 리셋 후 페이지만 열기
            try
            {
                // 크롤링 플래그 리셋
                await ResetCrawlingAllowed();
                
                var keyword = "테스트키워드";
                var encodedKeyword = Uri.EscapeDataString(keyword);
                var searchUrl = $"https://search.shopping.naver.com/search/all?query={encodedKeyword}&productSet=overseas";
                
                LogWindow.AddLogStatic($"🌐 페이지만 열기 (크롤링 비활성화): {searchUrl}");
                
                _extensionService ??= new ChromeExtensionService();
                await _extensionService.OpenNaverPriceComparison(searchUrl);
                LogWindow.AddLogStatic("✅ 네이버 가격비교 페이지가 새 탭에서 열렸습니다 (크롤링 없음).");
                
                // ⭐ 키워드 태그 생성을 위해 잠시 대기 후 서버에서 키워드 받아오기
                LogWindow.AddLogStatic("⏳ Chrome 확장프로그램 상품명 전송 대기 중...");
                await Task.Delay(3000); // 3초 대기
                LogWindow.AddLogStatic("🏷️ 키워드 태그 생성 시작");
                
                // ⭐ 키워드 태그 자동 생성 (5초마다 3번 시도)
                for (int i = 0; i < 3; i++)
                {
                    await CreateKeywordTagsFromServer();
                    await Task.Delay(2000); // 2초 간격으로 재시도
                }
            }
            catch (Exception ex)
            {
                LogWindow.AddLogStatic($"❌ 페이지 열기 오류: {ex.Message}");
            }
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
                
                // ⭐ 카드 생성 완료 후 키워드 복원 (지연 실행)
                Dispatcher.UIThread.Post(() =>
                {
                    RestoreSavedKeywords();
                }, DispatcherPriority.Background);
                
                Debug.WriteLine("✅ 실제 크롤링 데이터 로드 완료");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ 테스트 데이터 버튼 오류: {ex.Message}");
            }
        }
        
        // 현재 키워드 저장 (크롤링 키워드 포함)
        private void SaveCurrentKeywords()
        {
            try
            {
                _productKeywords.Clear();
                
                var container = this.FindControl<StackPanel>("RealDataContainer");
                if (container == null) return;
                
                var productCards = container.Children.OfType<StackPanel>().ToList();
                
                for (int i = 0; i < productCards.Count; i++)
                {
                    var productId = i + 1; // 1-based
                    var productCard = productCards[i];
                    var keywords = new List<string>();
                    
                    // ⭐ KeywordTagPanel에서 크롤링된 키워드 추출
                    var keywordTagPanel = productCard.Children.OfType<StackPanel>()
                        .FirstOrDefault(sp => sp.Name == "KeywordTagPanel");
                    
                    if (keywordTagPanel != null)
                    {
                        // Border > ScrollViewer > StackPanel > StackPanel(행) > Border(태그)
                        var border = keywordTagPanel.Children.OfType<Border>().FirstOrDefault();
                        if (border?.Child is ScrollViewer scrollViewer &&
                            scrollViewer.Content is StackPanel wrapPanel)
                        {
                            foreach (var row in wrapPanel.Children.OfType<StackPanel>())
                            {
                                foreach (var tag in row.Children.OfType<Border>())
                                {
                                    if (tag.Child is TextBlock textBlock)
                                    {
                                        var keyword = textBlock.Text?.Trim();
                                        if (!string.IsNullOrEmpty(keyword))
                                        {
                                            keywords.Add(keyword);
                                        }
                                    }
                                }
                            }
                        }
                    }
                    
                    if (keywords.Count > 0)
                    {
                        _productKeywords[productId] = keywords;
                        Debug.WriteLine($"✅ 상품 {productId}: {keywords.Count}개 크롤링 키워드 저장");
                    }
                }
                
                Debug.WriteLine($"✅ 전체 키워드 저장 완료: {_productKeywords.Count}개 상품");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ 키워드 저장 오류: {ex.Message}");
            }
        }
        
        // 저장된 키워드 복원 (크롤링 키워드 복원)
        private void RestoreSavedKeywords()
        {
            try
            {
                Debug.WriteLine($"🔄 키워드 복원 시작: {_productKeywords.Count}개 상품");
                
                foreach (var kvp in _productKeywords)
                {
                    var productId = kvp.Key;
                    var keywords = kvp.Value;
                    
                    Debug.WriteLine($"🔄 상품 {productId}: {keywords.Count}개 키워드 복원 시도");
                    
                    // CreateKeywordTags 메서드 재사용
                    CreateKeywordTags(keywords, productId);
                }
                
                Debug.WriteLine($"✅ 전체 키워드 복원 완료");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ 키워드 복원 오류: {ex.Message}");
            }
        }
        
        // 단일 키워드 태그 생성
        private void CreateSingleKeywordTag(string keyword, WrapPanel container, int productId)
        {
            var keywordBorder = new Border
            {
                Background = new SolidColorBrush(Color.Parse("#FFDAC4")),
                BorderBrush = new SolidColorBrush(Color.Parse("#E67E22")),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(15),
                Padding = new Thickness(12, 6),
                Margin = new Thickness(0, 0, 8, 8)
            };

            var keywordText = new TextBlock
            {
                Text = keyword,
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.Parse("#333333")),
                FontFamily = new FontFamily("Malgun Gothic")
            };

            keywordBorder.Child = keywordText;
            container.Children.Add(keywordBorder);
        }
        
        private void TestDataButton2_Click(object? sender, RoutedEventArgs e)
        {
            try
            {
                // 현재 키워드 저장
                SaveCurrentKeywords();
                
                // 카드는 그대로 두고 화면 전환만
                _hasData = false;
                UpdateViewVisibility();
                
                Debug.WriteLine("✅ 데이터 없는 화면으로 전환 완료 (카드 유지)");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ 데이터 없는 화면 전환 오류: {ex.Message}");
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
        private async void ClearPreviousCrawlingDataSilent()
        {
            try
            {
                await Task.Run(async () =>
                {
                    var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                    var predviaPath = System.IO.Path.Combine(appDataPath, "Predvia");
                    
                    int totalDeleted = 0;
                    int cardCount = 0;
                    
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
                    
                    // 카테고리 폴더 초기화
                    var categoriesPath = System.IO.Path.Combine(predviaPath, "Categories");
                    if (Directory.Exists(categoriesPath))
                    {
                        var fileCount = Directory.GetFiles(categoriesPath).Length;
                        Directory.Delete(categoriesPath, true);
                        totalDeleted += fileCount;
                    }
                    
                    // UI에서 기존 카드들 제거
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        var realDataContainer = this.FindControl<StackPanel>("RealDataContainer");
                        if (realDataContainer != null)
                        {
                            cardCount = realDataContainer.Children.Count;
                            realDataContainer.Children.Clear();
                        }
                    });
                    
                    // 지연 시간 증가
                    await Task.Delay(1500);
                    
                    // 작업로그에 초기화 완료 메시지 추가
                    if (totalDeleted > 0 || cardCount > 0)
                    {
                        // LogWindow가 준비될 때까지 잠시 기다림
                        int maxWaitTime = 5000; // 5초
                        int waitTime = 0;
                        while (LogWindow.Instance == null && waitTime < maxWaitTime)
                        {
                            await Task.Delay(100);
                            waitTime += 100;
                        }
                        
                        await Dispatcher.UIThread.InvokeAsync(() =>
                        {
                            LogWindow.AddLogStatic($"초기화 완료 (파일 {totalDeleted}개, 카드 {cardCount}개 삭제)");
                        });
                    }
                });
            }
            catch (Exception ex)
            {
                // 오류 시에도 로그에 표시 (지연 후)
                _ = Task.Delay(1000).ContinueWith(_ =>
                {
                    Dispatcher.UIThread.Post(() =>
                    {
                        LogWindow.AddLogStatic($"❌ 자동 초기화 오류: {ex.Message}");
                    });
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
                    var realDataContainer = this.FindControl<StackPanel>("RealDataContainer");
                    if (realDataContainer != null)
                    {
                        var cardCount = realDataContainer.Children.Count;
                        realDataContainer.Children.Clear();
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
        
        // 네이버 가격비교 검색 메서드
        private async Task SearchNaverPriceComparison(string keyword = "무선이어폰")
        {
            try
            {
                LogWindow.AddLogStatic($"🔍 네이버 가격비교 검색 시작: {keyword}");
                
                // ⭐ 키워드 타이머 재시작 (기존 타이머 중단 후 새로 시작)
                if (_keywordCheckTimer != null)
                {
                    _keywordCheckTimer.Stop();
                    _keywordCheckTimer = null;
                }
                StartKeywordCheckTimer();
                
                // URL 인코딩
                var encodedKeyword = Uri.EscapeDataString(keyword);
                var searchUrl = $"https://search.shopping.naver.com/search/all?query={encodedKeyword}&productSet=overseas";
                
                LogWindow.AddLogStatic($"🌐 검색 URL: {searchUrl}");
                
                // Chrome 확장프로그램 서비스 초기화
                _extensionService ??= new ChromeExtensionService();
                
                // Chrome 확장프로그램을 통해 새 탭에서 검색 실행
                await _extensionService.OpenNaverPriceComparison(searchUrl);
                LogWindow.AddLogStatic("✅ 네이버 가격비교 페이지가 새 탭에서 열렸습니다.");
            }
            catch (Exception ex)
            {
                LogWindow.AddLogStatic($"❌ 네이버 가격비교 검색 오류: {ex.Message}");
            }
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
            
            var mainWindow = (MainWindow?)this.VisualRoot;
            
            try
            {
                // 🔄 로딩창 표시
                mainWindow?.ShowLoading();
                
                button.IsEnabled = false;
                button.Content = "연결 중...";
                
                var searchText = textBox.Text?.Trim();
                if (string.IsNullOrEmpty(searchText))
                {
                    button.Content = "입력 필요";
                    await Task.Delay(2000);
                    return;
                }
                
                // ⭐ 크롤링 허용 플래그 설정
                await SetCrawlingAllowed();
                
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

        // ⭐ 크롤링 허용 플래그 설정 메서드
        private async Task SetCrawlingAllowed()
        {
            try
            {
                using var client = new HttpClient();
                await client.PostAsync("http://localhost:8080/api/crawling/allow", null);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"크롤링 허용 설정 오류: {ex.Message}");
            }
        }

        // ⭐ 크롤링 플래그 리셋 메서드
        private async Task ResetCrawlingAllowed()
        {
            try
            {
                using var client = new HttpClient();
                await client.DeleteAsync("http://localhost:8080/api/crawling/allow");
                LogWindow.AddLogStatic("🔄 크롤링 플래그 리셋 완료");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"크롤링 플래그 리셋 오류: {ex.Message}");
            }
        }

        // ⭐ 키워드 체크 타이머 시작
        private void StartKeywordCheckTimer()
        {
            try
            {
                _keywordCheckTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(2) // 2초마다 체크
                };
                
                _keywordCheckTimer.Tick += async (sender, e) =>
                {
                    if (!_keywordTagsCreated)
                    {
                        await CheckAndCreateKeywordTags();
                    }
                };
                
                _keywordCheckTimer.Start();
                LogWindow.AddLogStatic("🔄 키워드 자동 체크 타이머 시작 (2초 간격)");
            }
            catch (Exception ex)
            {
                LogWindow.AddLogStatic($"❌ 키워드 체크 타이머 시작 오류: {ex.Message}");
            }
        }

        // ⭐ 키워드 체크 및 태그 생성
        private async Task CheckAndCreateKeywordTags()
        {
            try
            {
                var currentProductId = _keywordSourceProductId;
                var keywords = await GetLatestKeywordsFromServer(currentProductId);
                
                if (keywords != null && keywords.Count > 0 && !_keywordTagsCreated)
                {
                    LogWindow.AddLogStatic($"🏷️ 키워드 {keywords.Count}개 발견 - 태그 생성 시작");
                    
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        CreateKeywordTags(keywords, currentProductId);
                        _keywordTagsCreated = true; // 한 번만 생성
                        _keywordCheckTimer?.Stop(); // 타이머 중지
                    });
                    
                    LogWindow.AddLogStatic("✅ 키워드 태그 자동 생성 완료");
                }
                else if (_keywordTagsCreated)
                {
                    // 이미 키워드 태그가 생성되었으면 타이머 중지
                    _keywordCheckTimer?.Stop();
                }
            }
            catch (Exception ex)
            {
                LogWindow.AddLogStatic($"❌ 키워드 체크 오류: {ex.Message}");
            }
        }

        // ⭐ 키워드 타이머 완전 중단 (크롤링 완료 시 호출)
        public void StopKeywordTimer()
        {
            try
            {
                _keywordCheckTimer?.Stop();
                _keywordCheckTimer = null;
                LogWindow.AddLogStatic("🛑 키워드 자동 체크 타이머 완전 중단");
            }
            catch (Exception ex)
            {
                LogWindow.AddLogStatic($"❌ 키워드 타이머 중단 오류: {ex.Message}");
            }
        }
        public async Task CreateKeywordTagsFromServer()
        {
            try
            {
                // ⭐ 현재 상품 ID를 로컬 변수로 캡처 (전역 변수가 변경되어도 안전)
                var currentProductId = _keywordSourceProductId;
                LogWindow.AddLogStatic($"🏷️ SourcingPage - 키워드 태그 생성 시작 (상품 ID: {currentProductId})");
                
                // ⭐ 실제 서버에서 키워드 받아오기 (상품 ID 전달)
                var keywords = await GetLatestKeywordsFromServer(currentProductId);
                
                if (keywords != null && keywords.Count > 0)
                {
                    LogWindow.AddLogStatic($"🏷️ 서버에서 키워드 {keywords.Count}개 수신: {string.Join(", ", keywords.Take(5))}...");
                    
                    // ⭐ 상품별로 키워드 저장
                    _productKeywords[currentProductId] = keywords;
                    
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        CreateKeywordTags(keywords, currentProductId);
                        _keywordTagsCreated = true; // ⭐ 플래그 설정
                        _keywordCheckTimer?.Stop(); // ⭐ 타이머 중지
                    });
                    
                    LogWindow.AddLogStatic($"✅ 키워드 태그 {keywords.Count}개 UI 생성 완료 (상품 ID: {currentProductId})");
                }
                else
                {
                    LogWindow.AddLogStatic("❌ 서버에서 키워드를 받아오지 못했습니다.");
                }
            }
            catch (Exception ex)
            {
                LogWindow.AddLogStatic($"❌ SourcingPage 키워드 태그 생성 오류: {ex.Message}");
            }
        }

        // ⭐ 서버에서 최신 키워드 받아오기
        private async Task<List<string>?> GetLatestKeywordsFromServer(int productId)
        {
            try
            {
                LogWindow.AddLogStatic($"🌐 서버에서 키워드 조회 중... (상품 ID: {productId})");
                using var client = new HttpClient();
                var response = await client.GetAsync($"http://localhost:8080/api/smartstore/latest-keywords?productId={productId}");
                
                LogWindow.AddLogStatic($"📡 서버 응답 상태: {response.StatusCode}");
                
                if (response.IsSuccessStatusCode)
                {
                    var jsonContent = await response.Content.ReadAsStringAsync();
                    LogWindow.AddLogStatic($"📄 서버 응답 내용: {jsonContent.Substring(0, Math.Min(100, jsonContent.Length))}...");
                    
                    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    var result = JsonSerializer.Deserialize<KeywordResponse>(jsonContent, options);
                    
                    if (result?.Keywords != null)
                    {
                        LogWindow.AddLogStatic($"✅ 키워드 {result.Keywords.Count}개 수신: {string.Join(", ", result.Keywords.Take(5))}");
                        return result.Keywords;
                    }
                    else
                    {
                        LogWindow.AddLogStatic("❌ 키워드 데이터가 null입니다.");
                    }
                }
                else
                {
                    LogWindow.AddLogStatic($"❌ 서버 응답 실패: {response.StatusCode}");
                }
                
                return null;
            }
            catch (Exception ex)
            {
                LogWindow.AddLogStatic($"❌ 서버에서 키워드 받아오기 오류: {ex.Message}");
                Debug.WriteLine($"서버에서 키워드 받아오기 오류: {ex.Message}");
                return null;
            }
        }

        // ⭐ 키워드 태그 UI 생성 (특정 상품 카드에만)
        private void CreateKeywordTags(List<string> keywords, int targetProductId = -1)
        {
            try
            {
                LogWindow.AddLogStatic($"🏷️ {keywords.Count}개 키워드 태그 생성 시작 (상품 ID: {targetProductId})");
                
                // ⭐ RealDataContainer에서 상품 카드들을 찾아서 키워드 태그 추가
                var container = this.FindControl<StackPanel>("RealDataContainer");
                if (container == null)
                {
                    LogWindow.AddLogStatic("❌ RealDataContainer를 찾을 수 없습니다.");
                    return;
                }

                StackPanel? targetProductCard = null;

                // 특정 상품 ID가 지정된 경우 해당 상품 카드 찾기
                if (targetProductId > 0)
                {
                    // 상품 카드들을 순회하면서 해당 productId의 카드 찾기
                    var productCards = container.Children.OfType<StackPanel>().ToList();
                    if (targetProductId <= productCards.Count)
                    {
                        targetProductCard = productCards[targetProductId - 1]; // 1-based index
                        LogWindow.AddLogStatic($"🎯 상품 ID {targetProductId}에 해당하는 카드 발견");
                    }
                }
                else
                {
                    // 기본값: 첫 번째 상품 카드
                    targetProductCard = container.Children.OfType<StackPanel>().FirstOrDefault();
                    LogWindow.AddLogStatic("🎯 기본값으로 첫 번째 상품 카드 선택");
                }

                if (targetProductCard == null)
                {
                    LogWindow.AddLogStatic("❌ 대상 상품 카드를 찾을 수 없습니다.");
                    return;
                }

                // 기존 키워드 패널 제거
                var existingKeywordPanel = targetProductCard.Children.OfType<StackPanel>()
                    .FirstOrDefault(sp => sp.Name == "KeywordTagPanel");
                if (existingKeywordPanel != null)
                {
                    targetProductCard.Children.Remove(existingKeywordPanel);
                }

                // ⭐ 키워드 태그 패널 생성 (스크롤 가능한 박스)
                var keywordPanel = new StackPanel
                {
                    Name = "KeywordTagPanel",
                    Orientation = Orientation.Vertical,
                    Margin = new Thickness(0, 15, 0, 15),
                    Spacing = 10
                };

                // 키워드 박스 (리뷰 박스와 동일한 스타일)
                var keywordBorder = new Border
                {
                    BorderBrush = new SolidColorBrush(Color.Parse("#FF8A46")), // 주황색 테두리
                    BorderThickness = new Thickness(2),
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(15, 10),
                    Height = 170, // 4줄 적절한 높이로 조정
                    Background = new SolidColorBrush(Colors.Transparent)
                };

                // 스크롤 가능한 영역
                var keywordScrollViewer = new ScrollViewer
                {
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
                };

                // 키워드 태그들을 여러 줄로 배치 (WrapPanel 효과)
                var keywordWrapPanel = new StackPanel
                {
                    Orientation = Orientation.Vertical,
                    Spacing = 5
                };

                var currentRow = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 8
                };

                double currentRowWidth = 0;
                const double maxRowWidth = 750; // 스크롤바 공간 고려하여 조금 줄임

                // 키워드 태그 생성 (전체)
                foreach (var keyword in keywords)
                {
                    var keywordTag = new Border
                    {
                        Background = new SolidColorBrush(Color.Parse("#E67E22")), // 주황색
                        CornerRadius = new CornerRadius(12), // 둥근 모서리
                        Padding = new Thickness(10, 5),
                        Cursor = new Cursor(StandardCursorType.Hand), // 클릭 가능 표시
                        Child = new TextBlock
                        {
                            Text = keyword,
                            Foreground = Brushes.White,
                            FontSize = 11,
                            FontWeight = FontWeight.Medium,
                            FontFamily = new FontFamily("Malgun Gothic")
                        }
                    };

                    // 키워드 태그 클릭 이벤트 추가
                    keywordTag.PointerPressed += (s, e) => OnKeywordTagClicked(keyword, targetProductId);

                    // 예상 태그 너비 계산 (대략적)
                    double tagWidth = keyword.Length * 8 + 30; // 글자당 8px + 패딩

                    // 현재 행에 추가할 수 있는지 확인
                    if (currentRowWidth + tagWidth > maxRowWidth && currentRow.Children.Count > 0)
                    {
                        // 현재 행을 완료하고 새 행 시작
                        keywordWrapPanel.Children.Add(currentRow);
                        currentRow = new StackPanel
                        {
                            Orientation = Orientation.Horizontal,
                            Spacing = 8
                        };
                        currentRowWidth = 0;
                    }

                    currentRow.Children.Add(keywordTag);
                    currentRowWidth += tagWidth;
                }

                // 마지막 행 추가
                if (currentRow.Children.Count > 0)
                {
                    keywordWrapPanel.Children.Add(currentRow);
                }

                // 스크롤 영역에 키워드 패널 추가
                keywordScrollViewer.Content = keywordWrapPanel;
                keywordBorder.Child = keywordScrollViewer;
                keywordPanel.Children.Add(keywordBorder);

                // ⭐ 리뷰 Border 찾기 (간단하게 - 인덱스 2번이 리뷰 Border)
                var insertIndex = -1;
                
                // 로그에서 확인: 인덱스 2번이 항상 Border (리뷰)
                if (targetProductCard.Children.Count > 2 && targetProductCard.Children[2] is Border)
                {
                    insertIndex = 2; // 리뷰 Border 바로 앞에 삽입
                    LogWindow.AddLogStatic($"🎯 리뷰 Border(인덱스 2) 발견! 삽입 예정");
                }

                // 키워드 태그 삽입
                if (insertIndex >= 0 && insertIndex <= targetProductCard.Children.Count)
                {
                    targetProductCard.Children.Insert(insertIndex, keywordPanel);
                    LogWindow.AddLogStatic($"✅ 키워드 태그를 상품 ID {targetProductId}의 {insertIndex}번째 위치에 삽입 완료");
                }
                else
                {
                    // 찾지 못하면 맨 끝에 추가
                    targetProductCard.Children.Add(keywordPanel);
                    LogWindow.AddLogStatic($"❌ 삽입 위치를 찾지 못해 상품 ID {targetProductId} 맨 끝에 추가");
                }

                LogWindow.AddLogStatic($"✅ 키워드 태그 {keywords.Count}개 UI 생성 완료 (상품 ID: {targetProductId})");
            }
            catch (Exception ex)
            {
                LogWindow.AddLogStatic($"❌ 키워드 태그 생성 오류: {ex.Message}");
            }
        }

        // ⭐ 키워드 태그 클릭 이벤트 핸들러
        private void OnKeywordTagClicked(string keyword, int productId)
        {
            try
            {
                if (_productElements.TryGetValue(productId, out var product) && 
                    product.NameInputBox != null)
                {
                    // 현재 텍스트에 키워드 추가 (띄어쓰기 포함)
                    var currentText = product.NameInputBox.Text ?? "";
                    var newText = string.IsNullOrEmpty(currentText) ? keyword : currentText + " " + keyword;
                    
                    product.NameInputBox.Text = newText;
                    LogWindow.AddLogStatic($"🏷️ 키워드 '{keyword}' 추가됨 - 상품 ID: {productId}");
                }
            }
            catch (Exception ex)
            {
                LogWindow.AddLogStatic($"❌ 키워드 클릭 처리 오류: {ex.Message}");
            }
        }

        // ⭐ 바이트 계산 및 표시 업데이트
        private void UpdateByteCount(int productId, TextBox nameInputBox, TextBlock byteCountText)
        {
            try
            {
                var text = nameInputBox.Text ?? "";
                var byteCount = System.Text.Encoding.UTF8.GetByteCount(text);
                
                byteCountText.Text = $"{byteCount}/50 byte";
                
                // 50바이트 초과 시 빨간색으로 변경
                if (byteCount > 50)
                {
                    byteCountText.Foreground = Brushes.Red;
                }
                else
                {
                    byteCountText.Foreground = new SolidColorBrush(Colors.Gray);
                }
            }
            catch (Exception ex)
            {
                LogWindow.AddLogStatic($"❌ 바이트 계산 오류: {ex.Message}");
            }
        }

        // ⭐ 첨부 버튼 클릭 이벤트 핸들러
        private void AttachNameButton_Click(int productId, TextBox nameDirectInput)
        {
            try
            {
                if (_productElements.TryGetValue(productId, out var product) && 
                    product.NameInputBox != null)
                {
                    var inputText = nameDirectInput.Text?.Trim() ?? "";
                    if (!string.IsNullOrEmpty(inputText))
                    {
                        // 상품명 입력박스에 추가 (기존 내용 보존)
                        var existingText = product.NameInputBox.Text?.Trim() ?? "";
                        product.NameInputBox.Text = string.IsNullOrEmpty(existingText) 
                            ? inputText 
                            : $"{existingText} {inputText}";
                        
                        // 입력박스 내용 지우기
                        nameDirectInput.Text = "";
                        
                        LogWindow.AddLogStatic($"📎 상품명 '{inputText}' 첨부됨 - 상품 ID: {productId}");
                    }
                    else
                    {
                        LogWindow.AddLogStatic("❌ 첨부할 내용이 없습니다.");
                    }
                }
            }
            catch (Exception ex)
            {
                LogWindow.AddLogStatic($"❌ 첨부 버튼 처리 오류: {ex.Message}");
            }
        }

        // ⭐ 39.png 스타일의 키워드 태그 생성
        private Border CreateKeywordTag(string keyword)
        {
            var tag = new Border
            {
                Background = new SolidColorBrush(Color.Parse("#E67E22")), // 주황색 배경
                CornerRadius = new CornerRadius(12), // 둥근 모서리
                Padding = new Thickness(8, 4),
                Margin = new Thickness(0, 0, 5, 5),
                Child = new TextBlock
                {
                    Text = keyword,
                    Foreground = Brushes.White, // 흰색 텍스트
                    FontSize = 12,
                    FontWeight = FontWeight.Medium,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };

            return tag;
        }

        // ⭐ 키워드 컨테이너 찾기
        private Panel? FindKeywordContainer(Control parent)
        {
            // 상품 카드에서 키워드 태그를 표시할 WrapPanel 찾기
            if (parent is Panel panel)
            {
                foreach (var child in panel.Children)
                {
                    if (child is WrapPanel wrapPanel)
                    {
                        return wrapPanel;
                    }
                    else if (child is Control control)
                    {
                        var found = FindKeywordContainer(control);
                        if (found != null) return found;
                    }
                }
            }
            else if (parent is ContentControl contentControl && contentControl.Content is Control childControl)
            {
                return FindKeywordContainer(childControl);
            }
            else if (parent is Border border && border.Child is Control borderChild)
            {
                return FindKeywordContainer(borderChild);
            }

            return null;
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
        public TextBox? NameInputBox { get; set; } // 상품명 입력박스 추가
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

// ⭐ 키워드 응답 모델
public class KeywordResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }
    
    [JsonPropertyName("keywords")]
    public List<string> Keywords { get; set; } = new();
    
    [JsonPropertyName("filteredCount")]
    public int FilteredCount { get; set; }
}
