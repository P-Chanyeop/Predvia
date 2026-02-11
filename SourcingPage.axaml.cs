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
using ClosedXML.Excel;

namespace Gumaedaehang
{
    // 리뷰 데이터 구조
    public class ReviewItem
    {
        public string rating { get; set; } = "0";
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
        private Button? _deleteSelectedButton;
        private Button? _saveDataButton;
        protected ToggleSwitch? _taobaoSearchModeSwitch; // 타오바오 검색 방식 스위치
        private bool _hasData = false;
        
        // ⭐ 로딩 오버레이 UI 요소
        private Grid? _loadingOverlay;
        private TextBlock? _loadingText;
        
        // 한글 입력 처리를 위한 타이머
        private DispatcherTimer? _inputTimer;
        private int _lastActiveProductId = 1; // 마지막으로 활성화된 상품 ID
        
        // 키워드 태그 자동 생성을 위한 타이머
        private DispatcherTimer? _keywordCheckTimer;
        private string _keywordSourceProductKey = ""; // 키워드를 생성한 상품 키 (storeId_productId)
        private Dictionary<int, List<string>> _productKeywords = new(); // 상품별 키워드 저장
        private ChromeExtensionService? _extensionService;
        
        // 상품별 UI 요소들을 관리하는 딕셔너리
        protected Dictionary<int, ProductUIElements> _productElements = new Dictionary<int, ProductUIElements>();
        
        // ⭐ 페이지네이션 변수
        private List<ProductCardData> _allProductCards = new(); // 전체 상품 데이터
        private int _currentPage = 1;
        private const int _itemsPerPage = 10;
        private TextBlock? _pageInfoText;
        
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
        
        // 중복 로드 방지 플래그
        private bool _dataAlreadyLoaded = false;
        private bool _isLoadingData = false; // 로딩 중 플래그
        
        public SourcingPage()
        {
            try
            {
                // 플래그 초기화
                _isTaobaoSearchRunning = false;

                InitializeComponent();

                // 타오바오 테스트 버튼 이벤트 연결
                var taobaoTestButton = this.FindControl<Button>("TaobaoTestButton");
                if (taobaoTestButton != null)
                {
                    taobaoTestButton.Click += TaobaoTestButton_Click;
                }
                
                // 🧹 자동 초기화 비활성화 - 엑셀 추출 시에만 삭제
                // ClearPreviousCrawlingDataSilent();
                
                // 초기화 시작 메시지 (지연 후 표시)
                // Task.Delay(500).ContinueWith(_ =>
                // {
                //     Dispatcher.UIThread.Post(() =>
                //     {
                //         LogWindow.AddLogStatic("🧹 프로그램 시작 - 이전 크롤링 데이터 자동 초기화 중...");
                //     });
                // });
                
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
                LogWindow.AddLogStatic($"🔍 SelectAllCheckBox 찾기 결과: {(_selectAllCheckBox != null ? "성공" : "실패")}");
                _deleteSelectedButton = this.FindControl<Button>("DeleteSelectedButton");
                _saveDataButton = this.FindControl<Button>("SaveDataButton");

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
                
                // ⭐ 자동 로드 제거 - 각 페이지 접속 시에만 로드
                // _ = Task.Run(() => LoadCrawledData());
                
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
        public async Task LoadCrawledData()
        {
            // 중복 로드 방지
            if (_dataAlreadyLoaded)
            {
                Debug.WriteLine("⚠️ LoadCrawledData 중복 호출 방지 - 이미 로드됨");
                return;
            }
            
            _dataAlreadyLoaded = true;
            
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
                var nameFiles = Directory.GetFiles(productDataPath, "*_name.txt");
                
                LogWindow.AddLogStatic($"🔍 파일 개수 확인: 이미지 {imageFiles.Length}개, 상품명 {nameFiles.Length}개");
                
                // 이미지 파일과 상품명 파일을 모두 수집
                var allProducts = new HashSet<(string storeId, string productId)>();
                
                // 이미지 파일에서 상품 정보 추출
                foreach (var imageFile in imageFiles)
                {
                    var fileName = System.IO.Path.GetFileNameWithoutExtension(imageFile);
                    var parts = fileName.Split('_');
                    
                    if (parts.Length >= 3)
                    {
                        var productId = parts[parts.Length - 2];
                        var storeId = string.Join("_", parts.Take(parts.Length - 2));
                        allProducts.Add((storeId, productId));
                    }
                }
                
                // 상품명 파일에서 상품 정보 추출 (이미지 없어도 카드 생성)
                foreach (var nameFile in nameFiles)
                {
                    var fileName = System.IO.Path.GetFileNameWithoutExtension(nameFile);
                    var parts = fileName.Split('_');
                    
                    if (parts.Length >= 3 && parts[parts.Length - 1] == "name")
                    {
                        var productId = parts[parts.Length - 2];
                        var storeId = string.Join("_", parts.Take(parts.Length - 2));
                        allProducts.Add((storeId, productId));
                    }
                }
                
                LogWindow.AddLogStatic($"✅ 실제 크롤링 데이터 로드 완료: {allProducts.Count}개 상품");
                
                // 모든 상품에 대해 카드 생성 (배치 처리로 UI 렉 방지)
                var productList = allProducts.ToList();
                const int batchSize = 10;
                
                for (int i = 0; i < productList.Count; i += batchSize)
                {
                    var batch = productList.Skip(i).Take(batchSize).ToList();
                    
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        foreach (var (storeId, productId) in batch)
                        {
                            var imageFile = System.IO.Path.Combine(imagesPath, $"{storeId}_{productId}_main.jpg");
                            if (!File.Exists(imageFile)) imageFile = "";
                            AddProductImageCard(storeId, productId, imageFile);
                        }
                    });
                    
                    // 배치 사이에 약간의 딜레이로 UI 반응성 유지
                    if (i + batchSize < productList.Count)
                        await Task.Delay(10);
                }
                
                // 데이터가 있으면 표시
                if (allProducts.Count > 0)
                {
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        _hasData = true;
                        UpdateViewVisibility();
                        
                        // ⭐ 카드 생성 완료 후 이벤트 핸들러 재등록
                        LogWindow.AddLogStatic($"🔗 {allProducts.Count}개 카드 생성 완료 - 이벤트 핸들러 재등록");
                        foreach (var product in _productElements.Values)
                        {
                            RegisterProductEventHandlers(product);
                        }
                        LogWindow.AddLogStatic($"✅ 모든 체크박스 이벤트 등록 완료");
                    });
                }
                
                // ⭐ 데이터 로드 완료 후 전체선택 체크박스 이벤트 재연결
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    if (_selectAllCheckBox == null)
                    {
                        _selectAllCheckBox = this.FindControl<CheckBox>("SelectAllCheckBox");
                    }
                    if (_selectAllCheckBox != null)
                    {
                        _selectAllCheckBox.IsCheckedChanged -= SelectAllCheckBox_Changed;
                        _selectAllCheckBox.IsCheckedChanged += SelectAllCheckBox_Changed;
                        LogWindow.AddLogStatic($"✅ 전체선택 체크박스 이벤트 연결 완료 (상품 {_productElements.Count}개)");
                    }
                });
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
                                reviews.Add($"⭐{review.rating} {review.content}");
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

        // ⭐ 리뷰 UI 업데이트 메서드
        public void UpdateProductReviews(string storeId, string productId, List<Services.ReviewData> reviews)
        {
            try
            {
                Dispatcher.UIThread.Post(() =>
                {
                    foreach (var kvp in _productElements)
                    {
                        var elements = kvp.Value;
                        if (elements.StoreId == storeId && elements.RealProductId == productId)
                        {
                            // 리뷰 패널 찾기 (Container 내부에서)
                            if (elements.Container != null)
                            {
                                var reviewPanel = FindReviewPanel(elements.Container);
                                if (reviewPanel != null)
                                {
                                    reviewPanel.Children.Clear();
                                    foreach (var review in reviews.Take(3)) // 최대 3개
                                    {
                                        var reviewText = new TextBlock
                                        {
                                            Text = $"⭐{review.Rating} {review.Content}",
                                            FontSize = 12,
                                            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                                            Margin = new Thickness(0, 2, 0, 2)
                                        };
                                        reviewPanel.Children.Add(reviewText);
                                    }
                                    Debug.WriteLine($"✅ 리뷰 UI 업데이트: {storeId}/{productId} - {reviews.Count}개");
                                }
                            }
                            break;
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ 리뷰 UI 업데이트 오류: {ex.Message}");
            }
        }
        
        private StackPanel? FindReviewPanel(Control parent)
        {
            if (parent is StackPanel sp && sp.Classes.Contains("review-panel"))
                return sp;
            
            if (parent is Panel panel)
            {
                foreach (var child in panel.Children)
                {
                    if (child is Control ctrl)
                    {
                        var result = FindReviewPanel(ctrl);
                        if (result != null) return result;
                    }
                }
            }
            return null;
        }

        // 카테고리 데이터 추가 메서드
        public void AddCategoryData(CategoryData categoryData)
        {
            try
            {
                Debug.WriteLine($"📂 카테고리 데이터 추가: {categoryData.StoreId} - {categoryData.Categories.Count}개");
                
                // 카테고리 데이터를 메모리에 저장
                _categoryDataCache[categoryData.StoreId] = categoryData;
                
                // ⭐ 기존 카드의 카테고리 텍스트 업데이트
                Dispatcher.UIThread.Post(() =>
                {
                    try
                    {
                        foreach (var kvp in _productElements)
                        {
                            var elements = kvp.Value;
                            if (elements.StoreId == categoryData.StoreId && elements.CategoryTextBlock != null)
                            {
                                var categoryInfo = GetCategoryInfo(categoryData.StoreId, elements.RealProductId ?? "");
                                if (!string.IsNullOrEmpty(categoryInfo))
                                {
                                    elements.CategoryTextBlock.Text = categoryInfo;
                                    Debug.WriteLine($"✅ 카테고리 업데이트: {categoryData.StoreId}/{elements.RealProductId} -> {categoryInfo}");
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"❌ 카드 카테고리 업데이트 오류: {ex.Message}");
                    }
                });
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
            AddProductImageCard(storeId, productId, imageUrl, null);
        }
        
        // 상품명과 함께 카드 추가 (오버로드)
        public void AddProductImageCard(string storeId, string productId, string imageUrl, string? productName)
        {
            try
            {
                var container = this.FindControl<StackPanel>("RealDataContainer");
                if (container == null) return;

                // ⭐ 중복 체크 제거 - 상품 추가 크롤링 지원
                // 중복 상품도 허용하여 화면 전환 시 데이터가 사라지지 않도록 함

                // ⭐ 카드 순서 기반 ID 생성 (1부터 시작) - _productElements 기준
                var cardId = _productElements.Count + 1;
                LogWindow.AddLogStatic($"🆔 새 카드 ID 생성: {cardId}");

                // 전체 상품 컨테이너
                var productContainer = new StackPanel 
                { 
                    Spacing = 0, 
                    Margin = new Thickness(0, 0, 0, 40),
                    Tag = $"{storeId}_{productId}" // ⭐ Excel 내보내기를 위한 Tag 설정
                };

                // 1. 카테고리 경로 (체크박스 + 빨간 점 + 텍스트)
                var categoryPanel = new StackPanel 
                { 
                    Orientation = Orientation.Horizontal, 
                    Spacing = 8, 
                    Margin = new Thickness(0, 0, 0, 15)
                };

                var checkBox = new CheckBox { VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center };
                
                // 체크박스 이벤트는 RegisterProductEventHandlers에서 등록
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
                    // ⭐ imageUrl이 없거나 파일이 없으면 동적으로 경로 생성
                    ImagePath = GetValidImagePath(imageUrl, storeId, productId)
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
                    Text = "", // ⭐ 사용자가 직접 입력하는 부분 - 비워둠
                    FontSize = 14,
                    FontFamily = new FontFamily("Malgun Gothic"),
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                    Background = Brushes.Transparent,
                    BorderThickness = new Thickness(0)
                };
                
                // ⭐ 초기 바이트 계산
                var initialByteCount = 0;
                var byteCountText = new TextBlock 
                { 
                    Text = "0/50 byte", 
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

                // 원상품명 (실제 크롤링된 상품명 표시) - 클릭 시 상품 상세페이지로 이동
                var originalProductName = !string.IsNullOrEmpty(productName) ? productName : GetOriginalProductName(storeId, productId);
                var originalNameText = new TextBlock 
                { 
                    Text = "원상품명: " + originalProductName, 
                    FontSize = 13,
                    FontFamily = new FontFamily("Malgun Gothic"),
                    Foreground = new SolidColorBrush(Color.Parse("#0066CC")), // 링크 색상
                    TextDecorations = TextDecorations.Underline, // 밑줄
                    Cursor = new Cursor(StandardCursorType.Hand) // 손가락 커서
                };
                
                // 원상품명 클릭 이벤트 - 상품 상세페이지로 이동
                originalNameText.PointerPressed += (s, e) => {
                    try 
                    {
                        var productUrl = $"https://smartstore.naver.com/{storeId}/products/{productId}";
                        LogWindow.AddLogStatic($"🔗 상품 상세페이지 열기: {productUrl}");
                        
                        var startInfo = new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = productUrl,
                            UseShellExecute = true
                        };
                        System.Diagnostics.Process.Start(startInfo);
                    }
                    catch (Exception ex)
                    {
                        LogWindow.AddLogStatic($"❌ 상품 페이지 열기 오류: {ex.Message}");
                    }
                };
                
                // ⭐ 상품명 입력칸은 비워둠 - 사용자가 키워드 조합해서 입력

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
                    Content = "🔍 키워드 검색", 
                    Width = 110, 
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

                // 상품명 직접 입력 + 첨부 버튼 + 배대지 비용
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
                    Watermark = "직접 입력"
                };
                var attachButton = new Button 
                { 
                    Content = "➕ 상품명에 추가", 
                    Width = 120, 
                    Height = 30,
                    FontSize = 12,
                    FontFamily = new FontFamily("Malgun Gothic"),
                    Background = new SolidColorBrush(Color.Parse("#FF8A46")),
                    Foreground = new SolidColorBrush(Colors.White),
                    HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Center
                };
                
                // 배대지 비용 라벨 + 입력칸
                var shippingLabel = new TextBlock
                {
                    Text = "배대지:",
                    FontSize = 12,
                    FontFamily = new FontFamily("Malgun Gothic"),
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                    Margin = new Thickness(15, 0, 0, 0)
                };
                var shippingInput = new TextBox
                {
                    Width = 70,
                    Height = 30,
                    FontSize = 12,
                    FontFamily = new FontFamily("Malgun Gothic"),
                    Watermark = "0",
                    Name = $"ShippingCost_{cardId}"
                };
                var shippingUnit = new TextBlock
                {
                    Text = "원",
                    FontSize = 12,
                    FontFamily = new FontFamily("Malgun Gothic"),
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
                };
                
                // 첨부 버튼 이벤트 연결
                attachButton.Click += (s, e) => {
                    LogWindow.AddLogStatic($"📎 첨부 버튼 클릭 감지됨! CardId: {cardId}");
                    AttachNameButton_Click(cardId, nameDirectInput);
                };
                
                nameDirectInputPanel.Children.Add(nameDirectInput);
                nameDirectInputPanel.Children.Add(attachButton);
                nameDirectInputPanel.Children.Add(shippingLabel);
                nameDirectInputPanel.Children.Add(shippingInput);
                nameDirectInputPanel.Children.Add(shippingUnit);

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
                
                // 페어링 버튼 클릭 이벤트 연결
                var cardIdForPairing = cardId; // 클로저 변수
                pairingButton.Click += (s, e) => TaobaoPairingButton_Click(cardIdForPairing);

                pairingPanel.Children.Add(redDot2);
                pairingPanel.Children.Add(pairingTitle);
                pairingPanel.Children.Add(pairingButton);

                // 5. 상품박스 5개 (타오바오 상품 표시용)
                var productBoxPanel = new StackPanel 
                { 
                    Orientation = Orientation.Horizontal, 
                    Spacing = 20,
                    Margin = new Thickness(0, 10, 0, 0),
                    Name = $"TaobaoProductPanel_{cardId}"  // 나중에 찾기 위한 이름
                };

                for (int i = 0; i < 5; i++)
                {
                    var productBox = new StackPanel { Spacing = 10 };
                    var currentIndex = i; // 클로저용 변수
                    
                    // 타오바오 상품 이미지 박스 (클릭 가능)
                    var logoBorder = new Border
                    {
                        Width = 160,
                        Height = 120,
                        Background = new SolidColorBrush(Color.Parse("#F5F5F5")),
                        CornerRadius = new CornerRadius(8),
                        Cursor = new Cursor(StandardCursorType.Hand),
                        Tag = $"{cardId}_{currentIndex}_url_",  // cardId_index_url_ 형식
                        BorderBrush = new SolidColorBrush(Color.Parse("#CCCCCC")),
                        BorderThickness = new Thickness(1),
                        Child = new Grid
                        {
                            Children =
                            {
                                // 기본 PREDVIA 로고
                                new TextBlock
                                {
                                    Text = "🔺 PREDVIA",
                                    FontSize = 16,
                                    FontFamily = new FontFamily("Malgun Gothic"),
                                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                                    Foreground = new SolidColorBrush(Color.Parse("#FF8A46")),
                                    Name = $"PlaceholderText_{cardId}_{i}"
                                },
                                // 타오바오 상품 이미지 (처음엔 숨김)
                                new Image
                                {
                                    Width = 150,
                                    Height = 110,
                                    Stretch = Avalonia.Media.Stretch.Uniform,
                                    Margin = new Thickness(5),
                                    IsVisible = false,
                                    Name = $"TaobaoImage_{cardId}_{i}"
                                }
                            }
                        }
                    };
                    
                    // 클릭 이벤트 추가
                    logoBorder.PointerPressed += OnTaobaoProductClick;
                    
                    // 가격 + 판매량 텍스트
                    var infoText = new TextBlock
                    {
                        Text = "페어링",
                        FontSize = 12,
                        FontFamily = new FontFamily("Malgun Gothic"),
                        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                        Name = $"TaobaoInfo_{cardId}_{i}"
                    };
                    
                    // 상품 페이지 열기 버튼
                    var openUrlButton = new Button
                    {
                        Content = "상품 페이지 열기",
                        FontSize = 10,
                        FontFamily = new FontFamily("Malgun Gothic"),
                        Width = 100,
                        Height = 26,
                        Background = new SolidColorBrush(Color.Parse("#E67E22")),
                        Foreground = new SolidColorBrush(Colors.White),
                        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                        HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                        VerticalContentAlignment = Avalonia.Layout.VerticalAlignment.Center,
                        IsVisible = false, // 처음엔 숨김, 페어링 후 표시
                        Name = $"TaobaoOpenUrl_{cardId}_{currentIndex}"
                    };
                    var capturedCardId = cardId;
                    var capturedIndex = currentIndex;
                    openUrlButton.Click += (s, e) => OpenTaobaoProductUrl(capturedCardId, capturedIndex);
                    
                    productBox.Children.Add(logoBorder);
                    productBox.Children.Add(infoText);
                    productBox.Children.Add(openUrlButton);
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
                var shippingInputBox = this.FindControl<TextBox>($"ShippingCost_{cardId}");
                var productElement = new ProductUIElements
                {
                    ProductId = cardId,
                    StoreId = storeId,
                    RealProductId = productId,
                    ImagePath = imageUrl, // 실제 이미지 파일 경로 저장 (imageUrl이 실제로는 파일 경로)
                    Container = productContainer, // 컨테이너 참조 추가
                    CheckBox = checkBox, // 체크박스 참조 추가 - 메서드 시작 부분의 checkBox 변수
                    CategoryTextBlock = categoryText, // ⭐ 카테고리 텍스트블록 참조 추가
                    NameInputBox = nameInputText,
                    ByteCountTextBlock = byteCountText,
                    KeywordPanel = keywordPanel,
                    KeywordInputBox = keywordInput,
                    ShippingCostInput = shippingInput, // ⭐ 배대지 비용 입력박스
                    AddKeywordButton = addButton,
                    DeleteButton = deleteButton, // 삭제 버튼 참조 추가
                    HoldButton = holdButton, // 보류 버튼 참조 추가
                    TaobaoPairingButton = pairingButton,
                    TaobaoProductsPanel = productBoxPanel // ⭐ 타오바오 상품 패널 추가
                };
                
                _productElements[cardId] = productElement;
                
                // 이벤트 등록 - 체크박스가 null이 아닌지 확인
                if (checkBox != null)
                {
                    LogWindow.AddLogStatic($"🔗 체크박스 참조 확인: 상품 {cardId}, CheckBox != null: {checkBox != null}");
                }
                
                RegisterProductEventHandlers(productElement);
                
                // 전체선택 체크박스 상태 업데이트
                UpdateSelectAllCheckBoxState();
                
                LogWindow.AddLogStatic($"✅ 상품 카드 생성 완료 - CardId: {cardId}, StoreId: {storeId}, ProductId: {productId}");

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
                _testDataButton2.Click += TestDataButton_Click; // 같은 핸들러 사용
                
            if (_selectAllCheckBox != null)
            {
                _selectAllCheckBox.IsCheckedChanged += SelectAllCheckBox_Changed;
                LogWindow.AddLogStatic($"✅ SelectAllCheckBox 이벤트 연결 완료");
            }
            else
            {
                LogWindow.AddLogStatic($"❌ SelectAllCheckBox가 null - 이벤트 연결 실패");
            }
            
            if (_deleteSelectedButton != null)
            {
                _deleteSelectedButton.Click += DeleteSelectedButton_Click;
            }

            if (_saveDataButton != null)
            {
                _saveDataButton.Click += SaveDataButton_Click;
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
                LogWindow.AddLogStatic($"🔗 체크박스 이벤트 등록: 상품 {product.ProductId}");
                // 이벤트 핸들러에서 상태 변경하지 않고 단순 로그만
                product.CheckBox.IsCheckedChanged += (s, e) => {
                    LogWindow.AddLogStatic($"✅ 체크박스 클릭됨: 상품 {product.ProductId}, 상태: {product.CheckBox.IsChecked}");
                    // ProductCheckBox_Changed 호출 제거 - 자연스러운 체크박스 동작 허용
                };
            }
            else
            {
                LogWindow.AddLogStatic($"❌ 체크박스가 null: 상품 {product.ProductId}");
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
            
            // TaobaoPairingButton 이벤트는 AddProductImageCard에서 이미 등록됨 (중복 방지)
            
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
            SelectAllCheckBox_Click(sender, e);
        }
        
        private void SelectAllCheckBox_Click(object? sender, RoutedEventArgs e)
        {
            LogWindow.AddLogStatic($"🔄 전체선택 클릭됨: {_selectAllCheckBox?.IsChecked}");
            LogWindow.AddLogStatic($"🔄 _productElements 개수: {_productElements.Count}");
            
            if (_selectAllCheckBox != null)
            {
                bool isChecked = _selectAllCheckBox.IsChecked ?? false;
                
                foreach (var kvp in _productElements)
                {
                    var product = kvp.Value;
                    if (product.CheckBox != null)
                    {
                        product.CheckBox.IsChecked = isChecked;
                    }
                }
                
                LogWindow.AddLogStatic($"✅ 전체선택 완료: {isChecked}");
            }
        }
        
        // 개별 상품 체크박스 변경 이벤트
        private void ProductCheckBox_Changed(int productId)
        {
            // 전체선택 상태 업데이트는 잠시 비활성화
            // UpdateSelectAllCheckBoxState();
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
        
        // 선택된 카드 삭제 버튼 클릭
        protected void DeleteSelectedButton_Click(object? sender, RoutedEventArgs e)
        {
            try
            {
                if (_allProductCards.Count == 0 && _productElements.Count == 0)
                {
                    LogWindow.AddLogStatic("❌ 삭제할 상품이 없습니다.");
                    return;
                }
                
                var totalCount = _allProductCards.Count;
                LogWindow.AddLogStatic($"🗑️ 전체 {totalCount}개 상품 삭제 시작");
                
                // UI 컨테이너 비우기
                var container = this.FindControl<StackPanel>("RealDataContainer");
                container?.Children.Clear();
                
                // 모든 데이터 폴더 비우기
                var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                var predviaPath = System.IO.Path.Combine(appDataPath, "Predvia");
                foreach (var folder in new[] { "Images", "ProductData", "Reviews", "Categories", "TaobaoImages" })
                {
                    var folderPath = System.IO.Path.Combine(predviaPath, folder);
                    if (Directory.Exists(folderPath))
                    {
                        foreach (var file in Directory.GetFiles(folderPath))
                            File.Delete(file);
                    }
                }
                
                // 모든 데이터 클리어
                _allProductCards.Clear();
                _productElements.Clear();
                _currentPage = 1;
                
                // 전체선택 체크박스 해제
                if (_selectAllCheckBox != null)
                    _selectAllCheckBox.IsChecked = false;
                
                // JSON 파일 삭제
                var jsonPath = System.IO.Path.Combine(predviaPath, "product_cards.json");
                if (File.Exists(jsonPath)) File.Delete(jsonPath);
                
                // 페이지 정보 업데이트
                UpdatePageInfo();
                
                LogWindow.AddLogStatic($"✅ {totalCount}개 상품 전체 삭제 완료");
            }
            catch (Exception ex)
            {
                LogWindow.AddLogStatic($"❌ 전체 삭제 오류: {ex.Message}");
            }
        }
        
        // 개별 상품 파일 삭제
        private void DeleteProductFiles(string predviaPath, string storeId, string productId)
        {
            try
            {
                // 이미지 파일
                var imagePath = System.IO.Path.Combine(predviaPath, "Images", $"{storeId}_{productId}_main.jpg");
                if (File.Exists(imagePath)) File.Delete(imagePath);
                
                // 상품명 파일
                var namePath = System.IO.Path.Combine(predviaPath, "ProductData", $"{storeId}_{productId}_name.txt");
                if (File.Exists(namePath)) File.Delete(namePath);
                
                // 리뷰 파일
                var reviewPath = System.IO.Path.Combine(predviaPath, "Reviews", $"{storeId}_{productId}_reviews.json");
                if (File.Exists(reviewPath)) File.Delete(reviewPath);
                
                // 카테고리 파일
                var categoryPath = System.IO.Path.Combine(predviaPath, "Categories", $"{storeId}_{productId}_categories.json");
                if (File.Exists(categoryPath)) File.Delete(categoryPath);
            }
            catch (Exception ex)
            {
                LogWindow.AddLogStatic($"⚠️ 파일 삭제 오류 ({storeId}/{productId}): {ex.Message}");
            }
        }

        // 💾 저장 버튼 클릭 이벤트
        private void SaveDataButton_Click(object? sender, RoutedEventArgs e)
        {
            try
            {
                LogWindow.AddLogStatic("💾 상품 데이터 저장 시작...");
                SaveProductCardsToJson();
                LogWindow.AddLogStatic("✅ 상품 데이터 저장 완료!");
            }
            catch (Exception ex)
            {
                LogWindow.AddLogStatic($"❌ 저장 실패: {ex.Message}");
            }
        }

        // 키워드 추가 버튼 클릭 이벤트
        private async void AddKeywordButton_Click(int productId)
        {
            LogWindow.AddLogStatic($"🔥 키워드 추가 버튼 클릭됨 - 상품 ID: {productId}");
            
            // ⭐ 키워드 생성한 상품 키 저장 (storeId_productId)
            if (_productElements.TryGetValue(productId, out var productElem))
            {
                _keywordSourceProductKey = $"{productElem.StoreId}_{productElem.RealProductId}";
                LogWindow.AddLogStatic($"🔑 키워드 소스 키 저장: {_keywordSourceProductKey}");
            }
            
            // ⭐ 추가 버튼은 크롤링 플래그 리셋
            await ResetCrawlingAllowed();
            
            // ⭐ 서버에 현재 상품 ID 설정
            await SetCurrentProductId(productId);
            
            if (_productElements.TryGetValue(productId, out var product))
            {
                // ⭐ 키워드 먼저 가져오고 나서 입력창 비우기
                var keyword = product.KeywordInputBox?.Text?.Trim();
                
                if (!string.IsNullOrEmpty(keyword))
                {
                    product.KeywordInputBox!.Text = ""; // 입력창 비우기
                    LogWindow.AddLogStatic($"🔍 입력된 키워드: {keyword}");
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
                if (keywordText == null || product.NameInputBox == null) return;
                
                var currentText = product.NameInputBox.Text ?? "";
                var isUsed = product.SelectedKeywords.Contains(keywordText);
                
                if (isUsed)
                {
                    // ⭐ 이미 사용 중 → 상품명에서 제거 + 주황색으로 복원
                    product.SelectedKeywords.Remove(keywordText);
                    
                    // 상품명에서 키워드 제거
                    var newText = currentText.Replace(keywordText, "").Replace("  ", " ").Trim();
                    product.NameInputBox.Text = newText;
                    
                    // 태그 주황색으로 복원
                    border.Background = new SolidColorBrush(Color.Parse("#FF8A46"));
                    textBlock.Foreground = Brushes.White;
                }
                else
                {
                    // ⭐ 미사용 → 상품명에 추가 + 회색으로 변경
                    product.SelectedKeywords.Add(keywordText);
                    
                    // 상품명에 키워드 추가
                    var newText = string.IsNullOrEmpty(currentText) ? keywordText : $"{currentText} {keywordText}";
                    product.NameInputBox.Text = newText;
                    
                    // 태그 회색으로 변경
                    border.Background = new SolidColorBrush(Color.Parse("#CCCCCC"));
                    textBlock.Foreground = new SolidColorBrush(Color.Parse("#666666"));
                }
                
                // 바이트 수 업데이트
                if (product.ByteCountTextBlock != null)
                {
                    var byteCount = CalculateByteCount(product.NameInputBox.Text ?? "");
                    product.ByteCountTextBlock.Text = $"{byteCount}/50 byte";
                    product.ByteCountTextBlock.Foreground = byteCount > 50 ? Brushes.Red : new SolidColorBrush(Colors.Gray);
                }
            }
        }
        
        // 삭제 버튼 클릭 이벤트
        private void DeleteButton_Click(int productId)
        {
            try
            {
                LogWindow.AddLogStatic($"🗑️ 개별 삭제 버튼 클릭: 상품 {productId}");
                
                if (_productElements.TryGetValue(productId, out var product) && product.Container != null)
                {
                    var storeId = product.StoreId;
                    var realProductId = product.RealProductId;
                    
                    // UI에서 제거
                    var container = this.FindControl<StackPanel>("RealDataContainer");
                    if (container != null)
                    {
                        container.Children.Remove(product.Container);
                    }
                    
                    // 파일 삭제
                    var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                    var predviaPath = System.IO.Path.Combine(appDataPath, "Predvia");
                    DeleteProductFiles(predviaPath, storeId, realProductId);
                    
                    // 메모리에서 제거
                    _productElements.Remove(productId);
                    
                    // JSON 파일 업데이트
                    SaveProductCardsToJson();
                    
                    LogWindow.AddLogStatic($"✅ 상품 {productId} 삭제 완료 (UI + 파일)");
                }
                else
                {
                    LogWindow.AddLogStatic($"❌ 상품 {productId}를 찾을 수 없음");
                }
            }
            catch (Exception ex)
            {
                LogWindow.AddLogStatic($"❌ 개별 삭제 오류: {ex.Message}");
            }
        }
        
        // 상품 보류 버튼 클릭 이벤트
        private void HoldButton_Click(int productId)
        {
            Debug.WriteLine($"상품 {productId} 상품 보류 버튼 클릭됨");
        }
        
        // 타오바오 페어링 버튼 클릭 이벤트
        private async void TaobaoPairingButton_Click(int productId)
        {
            // 스위치 상태에 따라 검색 방식 분기
            bool useLoginMode = _taobaoSearchModeSwitch?.IsChecked ?? true;
            
            if (useLoginMode)
            {
                await TaobaoPairingButton_LoginMode(productId);
            }
            else
            {
                await TaobaoPairingButton_GoogleLensMode(productId);
            }
        }
        
        // 구글렌즈 방식 (비로그인)
        private async Task TaobaoPairingButton_GoogleLensMode(int productId)
        {
            LogWindow.AddLogStatic($"🔍 [구글렌즈 검색] 상품 ID: {productId}");
            
            if (!_productElements.TryGetValue(productId, out var product)) return;
            
            try
            {
                // 버튼 비활성화
                if (product.TaobaoPairingButton != null)
                {
                    product.TaobaoPairingButton.IsEnabled = false;
                    product.TaobaoPairingButton.Content = "검색 중...";
                }
                
                // 이미지 파일 경로 - storeId_realProductId_main.jpg 패턴으로 찾기
                var imagesPath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Predvia", "Images");
                var imagePath = System.IO.Path.Combine(imagesPath, $"{product.StoreId}_{product.RealProductId}_main.jpg");
                
                LogWindow.AddLogStatic($"📷 이미지 경로: {imagePath}");
                
                if (!File.Exists(imagePath))
                {
                    LogWindow.AddLogStatic($"❌ 이미지 파일 없음: {product.StoreId}_{product.RealProductId}_main.jpg");
                    return;
                }
                
                // 이미지를 Base64로 변환
                var imageBytes = await File.ReadAllBytesAsync(imagePath);
                var base64Image = Convert.ToBase64String(imageBytes);
                
                // 서버에 구글렌즈 검색 요청
                using var client = new HttpClient();
                client.Timeout = TimeSpan.FromSeconds(60);
                
                var response = await client.PostAsync("http://localhost:8080/api/google-lens/search",
                    new StringContent(JsonSerializer.Serialize(new { productId, imageBase64 = base64Image }), 
                    System.Text.Encoding.UTF8, "application/json"));
                
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    LogWindow.AddLogStatic($"📥 응답: {json.Substring(0, Math.Min(200, json.Length))}");
                    
                    if (string.IsNullOrWhiteSpace(json))
                    {
                        LogWindow.AddLogStatic("❌ 빈 응답");
                        return;
                    }
                    
                    var result = JsonSerializer.Deserialize<JsonElement>(json);
                    
                    if (result.TryGetProperty("success", out var success) && success.GetBoolean())
                    {
                        if (result.TryGetProperty("products", out var products))
                        {
                            LogWindow.AddLogStatic($"✅ 구글렌즈 검색 완료: {products.GetArrayLength()}개 상품");
                            await Dispatcher.UIThread.InvokeAsync(() => DisplayTaobaoProducts(productId, products));
                        }
                    }
                    else
                    {
                        var error = result.TryGetProperty("error", out var e) ? e.GetString() : "알 수 없는 오류";
                        LogWindow.AddLogStatic($"❌ 검색 실패: {error}");
                    }
                }
                else
                {
                    LogWindow.AddLogStatic($"❌ HTTP 오류: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                LogWindow.AddLogStatic($"❌ 구글렌즈 검색 오류: {ex.Message}");
            }
            finally
            {
                if (product.TaobaoPairingButton != null)
                {
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        product.TaobaoPairingButton.IsEnabled = true;
                        product.TaobaoPairingButton.Content = "타오바오 페어링";
                    });
                }
            }
        }
        
        // 로그인 방식 (기존 API)
        private async Task TaobaoPairingButton_LoginMode(int productId)
        {
            LogWindow.AddLogStatic($"🔥 [타오바오 페어링] 상품 ID: {productId}");
            
            if (!_productElements.TryGetValue(productId, out var product)) return;
            
            try
            {
                // 버튼 비활성화
                if (product.TaobaoPairingButton != null)
                {
                    product.TaobaoPairingButton.IsEnabled = false;
                    product.TaobaoPairingButton.Content = "확인 중...";
                }
                
                // 0. 쿠키 상태 확인
                using var checkClient = new HttpClient();
                checkClient.Timeout = TimeSpan.FromSeconds(5);
                
                bool hasToken = false;
                try
                {
                    var cookieResponse = await checkClient.GetAsync("http://localhost:8080/api/taobao/cookies");
                    var cookieJson = await cookieResponse.Content.ReadAsStringAsync();
                    
                    if (!string.IsNullOrWhiteSpace(cookieJson))
                    {
                        var cookieData = JsonSerializer.Deserialize<JsonElement>(cookieJson);
                        hasToken = cookieData.TryGetProperty("hasToken", out var ht) && ht.GetBoolean();
                    }
                }
                catch
                {
                    // 서버 연결 실패 시 토큰 없음으로 처리
                }
                
                if (!hasToken)
                {
                    LogWindow.AddLogStatic("⚠️ 타오바오 로그인 필요");
                    
                    // 메시지박스 표시
                    var msgBox = new Window
                    {
                        Title = "타오바오 로그인 필요",
                        Width = 300, Height = 120,
                        WindowStartupLocation = WindowStartupLocation.CenterScreen,
                        CanResize = false
                    };
                    var panel = new StackPanel { Margin = new Thickness(20), Spacing = 15 };
                    panel.Children.Add(new TextBlock { Text = "타오바오에 로그인 후 다시 시도하세요.", TextAlignment = TextAlignment.Center });
                    var okBtn = new Button { Content = "확인", HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center, Padding = new Thickness(20, 5) };
                    okBtn.Click += (s, e) => msgBox.Close();
                    panel.Children.Add(okBtn);
                    msgBox.Content = panel;
                    msgBox.Show();
                    return;
                }
                
                if (product.TaobaoPairingButton != null)
                    product.TaobaoPairingButton.Content = "검색 중...";
                
                // 1. 이미지 경로 가져오기
                string? imagePath = null;
                if (product.StoreId != null && product.RealProductId != null)
                    imagePath = FindProductImagePath(product.StoreId, product.RealProductId);
                
                if (string.IsNullOrEmpty(imagePath) || !File.Exists(imagePath))
                {
                    LogWindow.AddLogStatic($"❌ 이미지 없음");
                    return;
                }
                
                LogWindow.AddLogStatic($"📷 이미지: {System.IO.Path.GetFileName(imagePath)}");
                
                // 2. 프록시 기반 서버 측 검색 요청
                using var client = new HttpClient();
                client.Timeout = TimeSpan.FromSeconds(60);
                
                var requestData = new { imagePath = imagePath, productId = productId };
                var json = JsonSerializer.Serialize(requestData);
                var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
                
                LogWindow.AddLogStatic("🔍 프록시 기반 타오바오 검색 중...");
                var response = await client.PostAsync("http://localhost:8080/api/taobao/proxy-search", content);
                var resultJson = await response.Content.ReadAsStringAsync();
                
                LogWindow.AddLogStatic($"📥 응답: {resultJson.Substring(0, Math.Min(200, resultJson.Length))}...");
                
                if (response.IsSuccessStatusCode && !string.IsNullOrEmpty(resultJson))
                {
                    var resultData = JsonSerializer.Deserialize<JsonElement>(resultJson);
                    
                    if (resultData.TryGetProperty("products", out var products) && products.GetArrayLength() > 0)
                    {
                        LogWindow.AddLogStatic($"✅ 타오바오 상품 {products.GetArrayLength()}개 발견!");
                        
                        // UI에 타오바오 상품 표시
                        await Dispatcher.UIThread.InvokeAsync(() =>
                        {
                            DisplayTaobaoProducts(productId, products);
                        });
                    }
                    else
                    {
                        LogWindow.AddLogStatic($"⚠️ products 없음. keys: {string.Join(",", resultData.EnumerateObject().Select(p => p.Name))}");
                    }
                }
                else
                {
                    LogWindow.AddLogStatic($"❌ HTTP 실패: {response.StatusCode}, body: {resultJson.Substring(0, Math.Min(100, resultJson.Length))}");
                }
            }
            catch (Exception ex)
            {
                LogWindow.AddLogStatic($"❌ 오류: {ex.Message}");
            }
            finally
            {
                if (product.TaobaoPairingButton != null)
                {
                    product.TaobaoPairingButton.IsEnabled = true;
                    product.TaobaoPairingButton.Content = "페어링";
                }
            }
        }
        
        // 타오바오 상품 UI 표시
        private void DisplayTaobaoProducts(int productId, JsonElement products)
        {
            if (!_productElements.TryGetValue(productId, out var product)) return;
            if (product.TaobaoProductsPanel == null) return;
            
            // ⭐ 타오바오 데이터를 TaobaoProductData 리스트로 변환하여 저장
            var taobaoList = new List<TaobaoProductData>();
            foreach (var item in products.EnumerateArray())
            {
                var data = new TaobaoProductData
                {
                    Nid = item.TryGetProperty("nid", out var n) ? n.GetString() ?? "" : "",
                    Title = item.TryGetProperty("title", out var t) ? t.GetString() ?? "" : "",
                    Price = item.TryGetProperty("price", out var p) ? p.GetString() ?? "" : "",
                    ImageUrl = item.TryGetProperty("img", out var img) ? img.GetString() ?? "" : "",
                    ProductUrl = item.TryGetProperty("url", out var u) ? u.GetString() ?? "" : "",
                    Sales = item.TryGetProperty("sales", out var s) ? s.GetString() ?? "" : ""
                };
                taobaoList.Add(data);
            }
            product.TaobaoProducts = taobaoList;
            product.IsTaobaoPaired = taobaoList.Count > 0;
            LogWindow.AddLogStatic($"💾 상품 {productId}에 타오바오 데이터 {taobaoList.Count}개 저장됨");
            
            int count = 0;
            foreach (var item in products.EnumerateArray())
            {
                if (count >= 5) break;
                
                // 기존 productBoxPanel의 자식 StackPanel 가져오기
                if (count >= product.TaobaoProductsPanel.Children.Count) break;
                var productBox = product.TaobaoProductsPanel.Children[count] as StackPanel;
                if (productBox == null || productBox.Children.Count < 2) { count++; continue; }
                
                var logoBorder = productBox.Children[0] as Border;
                var infoText = productBox.Children[1] as TextBlock;
                var openUrlButton = productBox.Children.Count > 2 ? productBox.Children[2] as Button : null;
                if (logoBorder == null) { count++; continue; }
                
                // JSON 필드명: nid, img, price, url, sales (서버 TaobaoProduct 클래스 기준)
                var nid = item.TryGetProperty("nid", out var n) ? n.GetString() ?? "" : "";
                var price = item.TryGetProperty("price", out var p) ? p.GetString() ?? "" : "";
                var imageUrl = item.TryGetProperty("img", out var img) ? img.GetString() ?? "" : "";
                var productUrl = item.TryGetProperty("url", out var u) ? u.GetString() ?? "" : "";
                var sales = item.TryGetProperty("sales", out var s) ? s.GetString() ?? "" : "";
                
                LogWindow.AddLogStatic($"🔍 상품{count}: nid={nid}, img={!string.IsNullOrEmpty(imageUrl)}, url={productUrl}");
                
                // URL 설정 (타오바오 링크)
                if (string.IsNullOrEmpty(productUrl) && !string.IsNullOrEmpty(nid))
                    productUrl = $"https://item.taobao.com/item.htm?id={nid}";
                logoBorder.Tag = $"{productId}_{count}_url_{productUrl}"; // cardId_index_url_URL 형식
                
                // 이미지 로드 (로컬 저장)
                if (!string.IsNullOrEmpty(imageUrl) && logoBorder.Child is Grid grid)
                {
                    foreach (var child in grid.Children)
                    {
                        if (child is TextBlock placeholder) placeholder.IsVisible = false;
                        if (child is Avalonia.Controls.Image taobaoImg)
                        {
                            taobaoImg.IsVisible = true;
                            LoadTaobaoImage(taobaoImg, imageUrl, productId, count);
                        }
                    }
                }
                else
                {
                    LogWindow.AddLogStatic($"⚠️ 이미지 없음 또는 Grid 없음: imageUrl={imageUrl}");
                }
                
                // 가격 + 판매량 표시
                if (infoText != null)
                {
                    var priceStr = "";
                    if (!string.IsNullOrEmpty(price) && price != "0")
                    {
                        var priceNum = price.Replace("CN¥", "").Replace("¥", "").Trim();
                        priceStr = $"{priceNum} 위안";
                    }
                    
                    var salesStr = "";
                    if (!string.IsNullOrEmpty(sales) && sales != "0")
                    {
                        salesStr = $" | 판매량 {sales}";
                    }
                    
                    infoText.Text = priceStr + salesStr;
                    infoText.Foreground = Avalonia.Media.Brushes.Red;
                }
                
                // 상품 페이지 열기 버튼 표시
                if (openUrlButton != null)
                {
                    openUrlButton.IsVisible = true;
                }
                
                count++;
            }
            
            LogWindow.AddLogStatic($"✅ 타오바오 상품 {count}개 UI 표시 완료");
        }
        
        // 타오바오 이미지 로드 (로컬 저장 후 표시)
        private async void LoadTaobaoImage(Avalonia.Controls.Image imageControl, string url, int cardId = 0, int index = 0)
        {
            try
            {
                if (string.IsNullOrEmpty(url)) return;
                if (url.StartsWith("//")) url = "https:" + url;
                
                // 로컬 저장 경로 - URL 해시로 고유 파일명 생성
                var taobaoDir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Predvia", "TaobaoImages");
                Directory.CreateDirectory(taobaoDir);
                var urlHash = url.GetHashCode().ToString("X8");
                var fileName = $"taobao_{cardId}_{index}_{urlHash}.jpg";
                var localPath = System.IO.Path.Combine(taobaoDir, fileName);
                
                // 이미 있으면 로컬에서 로드
                if (File.Exists(localPath))
                {
                    LogWindow.AddLogStatic($"📁 캐시 사용: {fileName}");
                    await Dispatcher.UIThread.InvokeAsync(() => {
                        imageControl.Source = new Avalonia.Media.Imaging.Bitmap(localPath);
                    });
                    return;
                }
                
                // 다운로드
                LogWindow.AddLogStatic($"⬇️ 다운로드: {url.Substring(0, Math.Min(60, url.Length))}...");
                using var client = new HttpClient();
                client.Timeout = TimeSpan.FromSeconds(15);
                client.DefaultRequestHeaders.Add("Referer", "https://www.taobao.com/");
                client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0");
                var bytes = await client.GetByteArrayAsync(url);
                
                // 로컬 저장
                await File.WriteAllBytesAsync(localPath, bytes);
                LogWindow.AddLogStatic($"✅ 저장 완료: {fileName} ({bytes.Length/1024}KB)");
                
                // 표시
                await Dispatcher.UIThread.InvokeAsync(() => {
                    imageControl.Source = new Avalonia.Media.Imaging.Bitmap(localPath);
                });
            }
            catch (Exception ex) { LogWindow.AddLogStatic($"❌ 이미지 로드 실패: {ex.Message}"); }
        }
        
        // ===== 기존 Python 방식 (백업) =====
        private async void TaobaoPairingButton_Click_OLD(int productId)
        {
            LogWindow.AddLogStatic($"🔥 [타오바오 페어링 버튼] 클릭됨 - 상품 ID: {productId}");
            
            // 전역 중복 실행 방지
            if (_isTaobaoSearchRunning)
            {
                LogWindow.AddLogStatic($"⏳ 타오바오 검색이 이미 진행 중입니다. 잠시 후 다시 시도해주세요.");
                return;
            }
            
            if (_productElements.TryGetValue(productId, out var product))
            {
                try
                {
                    // 버튼별 중복 실행 방지
                    if (product.TaobaoPairingButton != null && !product.TaobaoPairingButton.IsEnabled)
                    {
                        LogWindow.AddLogStatic($"⏳ 상품 {productId} 타오바오 페어링이 이미 진행 중입니다...");
                        return;
                    }

                    // 전역 플래그 설정
                    _isTaobaoSearchRunning = true;
                    
                    LogWindow.AddLogStatic($"🔍 타오바오 페어링 시작: 카드 ID {productId}");
                    
                    // 버튼 비활성화
                    if (product.TaobaoPairingButton != null)
                    {
                        product.TaobaoPairingButton.IsEnabled = false;
                        product.TaobaoPairingButton.Content = "쿠키 확인 중...";
                    }
                    
                    // 1단계: 쿠키 유효성 확인
                    LogWindow.AddLogStatic("🔍 저장된 타오바오 쿠키 확인 중...");
                    
                    var cookieFilePath = System.IO.Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                        "Predvia",
                        "taobao_cookies.json"
                    );
                    
                    bool needNewCookie = true;
                    
                    if (File.Exists(cookieFilePath))
                    {
                        try
                        {
                            var cookieJson = File.ReadAllText(cookieFilePath);
                            var cookies = JsonSerializer.Deserialize<Dictionary<string, string>>(cookieJson);
                            
                            if (cookies != null && cookies.ContainsKey("_m_h5_tk"))
                            {
                                var token = cookies["_m_h5_tk"];
                                var tokenParts = token.Split('_');
                                
                                if (tokenParts.Length >= 2 && long.TryParse(tokenParts[1], out long timestamp))
                                {
                                    var tokenTime = DateTimeOffset.FromUnixTimeMilliseconds(timestamp).LocalDateTime;
                                    var now = DateTime.Now;
                                    var diff = now - tokenTime;
                                    
                                    if (diff.TotalHours < 2) // 2시간 이내면 유효
                                    {
                                        needNewCookie = false;
                                        LogWindow.AddLogStatic($"✅ 유효한 쿠키 발견 (생성: {tokenTime:HH:mm:ss}, 경과: {diff.TotalMinutes:F0}분)");
                                    }
                                    else
                                    {
                                        LogWindow.AddLogStatic($"⚠️ 쿠키 만료됨 (생성: {tokenTime:HH:mm:ss}, 경과: {diff.TotalHours:F1}시간)");
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            LogWindow.AddLogStatic($"⚠️ 쿠키 파일 읽기 실패: {ex.Message}");
                        }
                    }
                    else
                    {
                        LogWindow.AddLogStatic("⚠️ 저장된 쿠키 없음");
                    }
                    
                    // 2단계: 필요한 경우에만 쿠키 수집
                    if (needNewCookie)
                    {
                        LogWindow.AddLogStatic($"⚠️ 타오바오 이미지 검색을 위해 Chrome이 열립니다 (네이버 크롤링 아님)");
                        LogWindow.AddLogStatic("🍪 타오바오 페이지 열어서 쿠키 수집 중...");
                        
                        try
                        {
                            // Chrome으로 타오바오 페이지 열기 (확장프로그램이 자동으로 쿠키 수집)
                            var chromeProcessInfo = new System.Diagnostics.ProcessStartInfo
                            {
                                FileName = "chrome",
                                Arguments = "--new-tab https://www.taobao.com",
                                UseShellExecute = true
                            };
                            
                            System.Diagnostics.Process.Start(chromeProcessInfo);
                            LogWindow.AddLogStatic("✅ 타오바오 페이지 열림 - 확장프로그램이 쿠키 수집 중...");
                            
                            // 쿠키 수집 대기
                            await Task.Delay(5000);
                        }
                        catch (Exception ex)
                        {
                            LogWindow.AddLogStatic($"❌ Chrome 열기 실패: {ex.Message}");
                        }
                    }
                    else
                    {
                        LogWindow.AddLogStatic("✅ 기존 쿠키 사용 - Chrome 열기 생략");
                    }
                    
                    // 3단계: 서버에서 쿠키 상태 확인
                        using var client = new HttpClient();
                        client.Timeout = TimeSpan.FromSeconds(5);
                        
                        var cookieResponse = await client.GetAsync("http://localhost:8080/api/taobao/cookies");
                        if (cookieResponse.IsSuccessStatusCode)
                        {
                            var cookieResponseText = await cookieResponse.Content.ReadAsStringAsync();
                            LogWindow.AddLogStatic($"✅ 쿠키 상태: {cookieResponseText}");
                        }
                        else
                        {
                            LogWindow.AddLogStatic("⚠️ 쿠키 상태 확인 실패");
                        }
                    }
                    catch (Exception ex)
                    {
                        LogWindow.AddLogStatic($"⚠️ 쿠키 수집 오류: {ex.Message}");
                    }

                    // 버튼 상태 업데이트
                    if (product.TaobaoPairingButton != null)
                    {
                        product.TaobaoPairingButton.Content = "이미지 업로드 중...";
                    }

                    // 상품 이미지 경로 찾기
                    string? imagePath = null;
                    if (product.StoreId != null && product.RealProductId != null)
                    {
                        imagePath = FindProductImagePath(product.StoreId, product.RealProductId);
                    }
                    
                    if (string.IsNullOrEmpty(imagePath))
                    {
                        LogWindow.AddLogStatic($"❌ 상품 {productId} 이미지를 찾을 수 없습니다");
                        
                        if (product.TaobaoPairingButton != null)
                        {
                            product.TaobaoPairingButton.Content = "이미지 없음";
                            await Task.Delay(2000);
                        }
                        return;
                    }
                    
                    LogWindow.AddLogStatic($"📷 상품 {productId} 이미지 경로: {imagePath}");

                    // 이미지 파일 존재 여부 확인
                    if (!File.Exists(imagePath))
                    {
                        LogWindow.AddLogStatic($"❌ [디버그] 이미지 파일이 존재하지 않음: {imagePath}");
                        if (product.TaobaoPairingButton != null)
                        {
                            product.TaobaoPairingButton.Content = "파일 없음";
                            await Task.Delay(2000);
                        }
                        return;
                    }
                    else
                    {
                        var fileInfo = new FileInfo(imagePath);
                        LogWindow.AddLogStatic($"✅ [디버그] 이미지 파일 확인 - 크기: {fileInfo.Length} bytes, 수정시간: {fileInfo.LastWriteTime}");
                    }

                    // 1. 먼저 파이썬 실행
                    LogWindow.AddLogStatic("🐍 파이썬 run.py 실행 중...");

                    List<object> pythonProducts = new List<object>();

                    try
                    {
                        var pythonPath = System.IO.Path.Combine(Directory.GetCurrentDirectory(), "image_search_products-master");
                        var processInfo = new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = "python",
                            Arguments = $"run.py \"{imagePath}\"",
                            WorkingDirectory = pythonPath,
                            UseShellExecute = false,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            CreateNoWindow = true
                        };

                        LogWindow.AddLogStatic($"🔧 [디버그] Python 명령: python run.py \"{imagePath}\"");
                        
                        // UTF-8 인코딩 설정
                        processInfo.EnvironmentVariables["PYTHONIOENCODING"] = "utf-8";
                        processInfo.StandardOutputEncoding = System.Text.Encoding.UTF8;
                        processInfo.StandardErrorEncoding = System.Text.Encoding.UTF8;
                        
                        // 타오바오 토큰을 환경변수로 전달
                        var taobaoToken = Services.ThumbnailWebServer.GetTaobaoToken();
                        if (!string.IsNullOrEmpty(taobaoToken))
                        {
                            processInfo.EnvironmentVariables["TAOBAO_TOKEN"] = taobaoToken;
                            LogWindow.AddLogStatic($"🔑 타오바오 토큰을 Python에 전달: {taobaoToken.Substring(0, Math.Min(10, taobaoToken.Length))}...");
                        }
                        
                        using var process = System.Diagnostics.Process.Start(processInfo);
                        if (process != null)
                        {
                            // 출력 읽기
                            var output = await process.StandardOutput.ReadToEndAsync();
                            var error = await process.StandardError.ReadToEndAsync();

                            await process.WaitForExitAsync();

                            if (process.ExitCode == 0)
                            {
                                LogWindow.AddLogStatic("✅ 파이썬 실행 성공");
                                LogWindow.AddLogStatic($"📤 [디버그] Python 출력 (첫 5000자): {output.Substring(0, Math.Min(5000, output.Length))}");

                                // ⭐ _m_h5_tk 쿠키 오류 또는 TOKEN_EXPIRED 확인
                                if (output.Contains("_m_h5_tk not found") || output.Contains("TOKEN_EXOIRED") || output.Contains("TOKEN_EXPIRED") || output.Contains("令牌过期"))
                                {
                                    LogWindow.AddLogStatic("⚠️ 타오바오 토큰 만료 또는 쿠키 오류 감지 - 쿠키 재수집 시작...");

                                    // 기존 Chrome에 새 탭으로 타오바오 열기 (확장프로그램 사용)
                                    try
                                    {
                                        var retryProcessInfo = new System.Diagnostics.ProcessStartInfo
                                        {
                                            FileName = "chrome",
                                            Arguments = "https://www.taobao.com",
                                            UseShellExecute = true
                                        };

                                        System.Diagnostics.Process.Start(retryProcessInfo);
                                        LogWindow.AddLogStatic("✅ 기존 Chrome에 타오바오 탭 열림 - 확장프로그램이 쿠키 재수집 중...");

                                        // 쿠키 재수집 대기
                                        await Task.Delay(8000); // 8초 대기

                                        // ⭐ 서버에서 새로운 타오바오 토큰 가져오기
                                        LogWindow.AddLogStatic("🔄 새로운 타오바오 토큰 가져오는 중...");
                                        try
                                        {
                                            using var httpClient = new HttpClient();
                                            var tokenResponse = await httpClient.GetAsync("http://localhost:8080/api/taobao/cookies");
                                            if (tokenResponse.IsSuccessStatusCode)
                                            {
                                                var tokenJson = await tokenResponse.Content.ReadAsStringAsync();
                                                var tokenData = JsonSerializer.Deserialize<JsonElement>(tokenJson);

                                                if (tokenData.TryGetProperty("token", out var newTokenElement))
                                                {
                                                    var newToken = newTokenElement.GetString();
                                                    if (!string.IsNullOrEmpty(newToken))
                                                    {
                                                        taobaoToken = newToken;
                                                        LogWindow.AddLogStatic($"✅ 새로운 토큰 획득: {newToken.Substring(0, Math.Min(20, newToken.Length))}...");
                                                    }
                                                }
                                            }
                                        }
                                        catch (Exception tokenEx)
                                        {
                                            LogWindow.AddLogStatic($"⚠️ 새 토큰 가져오기 실패 (기존 토큰 사용): {tokenEx.Message}");
                                        }

                                        // Python 재실행 (User-Agent 변경)
                                        LogWindow.AddLogStatic("🐍 쿠키 재수집 완료 - User-Agent 변경하여 Python 재실행...");
                                        var retryPythonInfo = new System.Diagnostics.ProcessStartInfo
                                        {
                                            FileName = "python",
                                            Arguments = $"run.py \"{imagePath}\"",
                                            WorkingDirectory = pythonPath,
                                            UseShellExecute = false,
                                            RedirectStandardOutput = true,
                                            RedirectStandardError = true,
                                            CreateNoWindow = true
                                        };

                                        retryPythonInfo.EnvironmentVariables["PYTHONIOENCODING"] = "utf-8";
                                        retryPythonInfo.StandardOutputEncoding = System.Text.Encoding.UTF8;
                                        retryPythonInfo.StandardErrorEncoding = System.Text.Encoding.UTF8;

                                        // User-Agent 변경 플래그 설정
                                        retryPythonInfo.EnvironmentVariables["CHANGE_USER_AGENT"] = "true";
                                        LogWindow.AddLogStatic("🔄 User-Agent 변경 플래그 설정됨");

                                        // ⭐ 새로운 토큰으로 환경변수 설정
                                        if (!string.IsNullOrEmpty(taobaoToken))
                                        {
                                            retryPythonInfo.EnvironmentVariables["TAOBAO_TOKEN"] = taobaoToken;
                                            LogWindow.AddLogStatic($"🔑 새 토큰으로 환경변수 설정: {taobaoToken.Substring(0, Math.Min(20, taobaoToken.Length))}...");
                                        }

                                        using var retryProcess = System.Diagnostics.Process.Start(retryPythonInfo);
                                        if (retryProcess != null)
                                        {
                                            output = await retryProcess.StandardOutput.ReadToEndAsync();
                                            await retryProcess.WaitForExitAsync();
                                            LogWindow.AddLogStatic("✅ Python 재실행 완료");
                                        }
                                    }
                                    catch (Exception retryEx)
                                    {
                                        LogWindow.AddLogStatic($"⚠️ 쿠키 재수집 오류: {retryEx.Message}");
                                    }
                                }

                                // Full response 파싱
                                var lines = output.Split('\n');
                                LogWindow.AddLogStatic($"🔍 [디버그] 총 {lines.Length}개 라인 검색 중...");

                                foreach (var line in lines)
                                {
                                    if (line.Trim().StartsWith("Full response:"))
                                    {
                                        LogWindow.AddLogStatic($"✅ [디버그] Full response 라인 발견!");

                                        string jsonStr = ""; // ⭐ catch 블록에서도 접근 가능하도록 선언

                                        try
                                        {
                                            jsonStr = line.Substring(line.IndexOf('{'));

                                            // Python 딕셔너리 형식을 JSON으로 변환 (작은따옴표 → 큰따옴표)
                                            jsonStr = jsonStr.Replace("'", "\"")
                                                           .Replace("True", "true")
                                                           .Replace("False", "false")
                                                           .Replace("None", "null");

                                            LogWindow.AddLogStatic($"🔍 [디버그] JSON 문자열 길이: {jsonStr.Length}자");

                                            // ⭐ 잘못된 이스케이프 시퀀스 정리 (JSON 파싱 오류 방지)
                                            jsonStr = CleanInvalidJsonEscapes(jsonStr);

                                            var jsonResponse = JsonSerializer.Deserialize<JsonElement>(jsonStr);
                                            LogWindow.AddLogStatic($"✅ [디버그] JSON 역직렬화 성공!");
                                            
                                            // CAPTCHA 또는 오류 체크
                                            if (jsonResponse.TryGetProperty("ret", out var retElement) &&
                                                retElement.ValueKind == JsonValueKind.Array)
                                            {
                                                var retArray = retElement.EnumerateArray().ToList();
                                                LogWindow.AddLogStatic($"⚠️ [디버그] API 응답 ret 배열: {string.Join(", ", retArray.Select(r => r.GetString()))}");

                                                if (retArray.Any(r => r.GetString()?.Contains("FAIL_SYS_USER_VALIDATE") == true ||
                                                                     r.GetString()?.Contains("哎哟喂,被挤爆啦") == true))
                                                {
                                                    LogWindow.AddLogStatic("❌ CAPTCHA 감지됨 - Python에서 프록시 변경 재시도 중...");
                                                    // Python 코드에서 자동으로 프록시 변경하여 재시도
                                                }
                                            }
                                            
                                            // 정상 응답 처리
                                            LogWindow.AddLogStatic($"🔍 [디버그] 'data' 속성 확인 중...");

                                            if (jsonResponse.TryGetProperty("data", out var dataElement))
                                            {
                                                LogWindow.AddLogStatic($"✅ [디버그] 'data' 속성 발견!");

                                                // ⭐ data 내부 구조 전체 출력
                                                LogWindow.AddLogStatic($"🔍 [디버그] data 내용: {dataElement.GetRawText()}");

                                                LogWindow.AddLogStatic($"🔍 [디버그] 'itemsArray' 속성 확인 중...");

                                                if (dataElement.TryGetProperty("itemsArray", out var itemsArrayElement))
                                                {
                                                    LogWindow.AddLogStatic($"✅ [디버그] 'itemsArray' 발견! 상품 개수: {itemsArrayElement.GetArrayLength()}");

                                                    var taobaoProducts = new List<TaobaoProductData>();
                                                    foreach (var item in itemsArrayElement.EnumerateArray())
                                                    {
                                                        if (taobaoProducts.Count >= 5) break;

                                                        var taobaoProduct = new TaobaoProductData
                                                        {
                                                            Nid = item.TryGetProperty("nid", out var nidElement) ? nidElement.GetString() ?? "" : "",
                                                            Title = item.TryGetProperty("title", out var titleElement) ? titleElement.GetString() ?? "" : "",
                                                            Price = this.ExtractPrice(item),
                                                            ProductUrl = item.TryGetProperty("auctionUrl", out var urlElement) ? urlElement.GetString() ?? "" : "",
                                                            Reviews = this.ExtractReviewCount(item).ToString(),
                                                            Sales = this.ExtractShopName(item),
                                                            ImageUrl = this.ExtractImageUrl(item)
                                                        };

                                                        // ⭐ 파싱된 데이터 로그 (디버깅용)
                                                        LogWindow.AddLogStatic($"📦 상품 {taobaoProducts.Count + 1}: 가격=¥{taobaoProduct.Price}, 리뷰={taobaoProduct.Reviews}개, 이미지={(!string.IsNullOrEmpty(taobaoProduct.ImageUrl) ? "O" : "X")}");

                                                        taobaoProducts.Add(taobaoProduct);
                                                    }

                                                    LogWindow.AddLogStatic($"✅ 타오바오 상품 {taobaoProducts.Count}개 파싱 완료");

                                                    // UI 업데이트
                                                    LogWindow.AddLogStatic($"🎨 [디버그] UI 업데이트 시작...");
                                                    await Dispatcher.UIThread.InvokeAsync(() =>
                                                    {
                                                        UpdateTaobaoProductBoxes(productId, taobaoProducts);
                                                    });
                                                    LogWindow.AddLogStatic($"✅ [디버그] UI 업데이트 완료!");

                                                    // 페어링 완료 처리
                                                    product.IsTaobaoPaired = true;
                                                    UpdateProductStatusIndicators(productId);
                                                }
                                                else
                                                {
                                                    LogWindow.AddLogStatic($"❌ [디버그] 'itemsArray' 속성을 찾을 수 없음!");

                                                    // ⭐ 'result' 속성 확인 (일부 응답에서 result 사용)
                                                    if (dataElement.TryGetProperty("result", out var resultElement))
                                                    {
                                                        LogWindow.AddLogStatic($"🔍 [디버그] 'result' 속성 발견!");

                                                        if (resultElement.ValueKind == JsonValueKind.Array)
                                                        {
                                                            var resultArray = resultElement.EnumerateArray().ToList();
                                                            LogWindow.AddLogStatic($"📊 [디버그] result 배열 길이: {resultArray.Count}");

                                                            if (resultArray.Count == 0)
                                                            {
                                                                LogWindow.AddLogStatic($"⚠️ 타오바오에서 해당 이미지로 검색 결과를 찾지 못했습니다.");
                                                                LogWindow.AddLogStatic($"💡 다른 이미지를 사용하거나 직접 타오바오에서 검색해보세요.");
                                                            }
                                                        }
                                                    }
                                                    else
                                                    {
                                                        LogWindow.AddLogStatic($"⚠️ [디버그] 'result' 속성도 찾을 수 없음 - data 구조가 예상과 다름");
                                                    }
                                                }
                                            }
                                            else
                                            {
                                                LogWindow.AddLogStatic($"❌ [디버그] 'data' 속성을 찾을 수 없음!");
                                            }
                                        }
                                        catch (JsonException parseEx)
                                        {
                                            LogWindow.AddLogStatic($"❌ Full response 파싱 오류: {parseEx.Message}");

                                            // ⭐ 파싱 실패한 위치 주변 JSON 출력 (디버깅용)
                                            try
                                            {
                                                // BytePositionInLine에서 오류 발생 위치 추출
                                                var errorMsg = parseEx.Message;
                                                if (errorMsg.Contains("BytePositionInLine"))
                                                {
                                                    var posMatch = System.Text.RegularExpressions.Regex.Match(errorMsg, @"BytePositionInLine:\s*(\d+)");
                                                    if (posMatch.Success && int.TryParse(posMatch.Groups[1].Value, out int errorPos))
                                                    {
                                                        int start = Math.Max(0, errorPos - 100);
                                                        int length = Math.Min(200, jsonStr.Length - start);
                                                        string snippet = jsonStr.Substring(start, length);
                                                        LogWindow.AddLogStatic($"🔍 [디버그] 오류 위치 주변 (위치 {errorPos}): ...{snippet}...");
                                                    }
                                                }
                                            }
                                            catch { }

                                            LogWindow.AddLogStatic($"⚠️ [디버그] JSON 파싱 실패 - 타오바오 검색 결과를 사용할 수 없습니다.");
                                            LogWindow.AddLogStatic($"💡 [디버그] 이 상품은 타오바오 API 응답에 잘못된 문자가 포함되어 있어 건너뜁니다.");
                                        }
                                        catch (Exception parseEx)
                                        {
                                            LogWindow.AddLogStatic($"❌ Full response 파싱 오류: {parseEx.Message}");
                                            LogWindow.AddLogStatic($"❌ [디버그] 스택 트레이스: {parseEx.StackTrace}");
                                        }
                                        break;
                                    }
                                }
                            }
                            else
                            {
                                LogWindow.AddLogStatic($"❌ 파이썬 실행 실패 (코드: {process.ExitCode})");
                                LogWindow.AddLogStatic($"🔴 [디버그] Python 오류 출력: {error}");
                                LogWindow.AddLogStatic($"🔴 [디버그] Python 표준 출력: {output}");
                            }
                        }
                        else
                        {
                            LogWindow.AddLogStatic("❌ 파이썬 프로세스 시작 실패");
                        }
                    }
                    catch (Exception pythonEx)
                    {
                        LogWindow.AddLogStatic($"❌ 파이썬 실행 오류: {pythonEx.Message}");
                    }
                finally
                {
                    // 전역 플래그 해제
                    _isTaobaoSearchRunning = false;
                    
                    // 버튼 다시 활성화
                    if (product.TaobaoPairingButton != null)
                    {
                        product.TaobaoPairingButton.IsEnabled = true;
                        product.TaobaoPairingButton.Content = "페어링";
                    }
                }
            }
        }
        
        // 상품 이미지 경로 찾기
        private string? FindProductImagePath(string storeId, string productId)
        {
            try
            {
                var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                var imagesPath = System.IO.Path.Combine(appDataPath, "Predvia", "Images");
                
                if (!Directory.Exists(imagesPath))
                    return null;
                
                // {storeId}_{productId}_main.jpg 정확한 파일명으로 검색
                var fileName = $"{storeId}_{productId}_main.jpg";
                var fullPath = System.IO.Path.Combine(imagesPath, fileName);
                
                return File.Exists(fullPath) ? fullPath : null;
            }
            catch (Exception ex)
            {
                LogWindow.AddLogStatic($"❌ 이미지 경로 찾기 오류: {ex.Message}");
                return null;
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
                // 이미 로딩 중이면 무시
                if (_isLoadingData)
                {
                    LogWindow.AddLogStatic("⚠️ 이미 데이터 로딩 중 - 중복 클릭 무시");
                    return;
                }
                
                _isLoadingData = true;
                LogWindow.AddLogStatic("🔥 '데이터 있는 화면 보기' 버튼 클릭됨");
                
                // 기존 카드들 확인
                var container = this.FindControl<StackPanel>("RealDataContainer");
                if (container != null)
                {
                    var cardCount = container.Children.Count;
                    LogWindow.AddLogStatic($"🔥 기존 카드 {cardCount}개 제거");
                    
                    // 카드가 있으면 플래그 리셋 안 함 (중복 로드 방지)
                    if (cardCount == 0)
                    {
                        _dataAlreadyLoaded = false;
                    }
                    
                    container.Children.Clear();
                }
                else
                {
                    LogWindow.AddLogStatic("❌ RealDataContainer를 찾을 수 없음");
                    _dataAlreadyLoaded = false;
                }
                
                // 크롤링된 실제 데이터 로드
                LogWindow.AddLogStatic("🔥 LoadCrawledData() 호출");
                LoadCrawledData();
                
                // 화면 전환
                _hasData = true;
                UpdateViewVisibility();
                
                // ⭐ 카드 생성 완료 후 키워드 복원 (지연 실행)
                Dispatcher.UIThread.Post(() =>
                {
                    RestoreSavedKeywords();
                    _isLoadingData = false; // 로딩 완료
                }, DispatcherPriority.Background);
                
                LogWindow.AddLogStatic("✅ 실제 크롤링 데이터 로드 완료");
            }
            catch (Exception ex)
            {
                _isLoadingData = false; // 오류 시에도 플래그 해제
                LogWindow.AddLogStatic($"❌ 테스트 데이터 버튼 오류: {ex.Message}");
                LogWindow.AddLogStatic($"❌ 스택: {ex.StackTrace}");
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
        
        // ⭐ 로딩 오버레이 표시
        private void ShowLoadingOverlay(string message)
        {
            if (_loadingOverlay == null)
            {
                _loadingOverlay = new Grid
                {
                    Background = new SolidColorBrush(Color.Parse("#80000000")),
                    ZIndex = 9999
                };
                
                var panel = new StackPanel
                {
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                    Spacing = 15
                };
                
                // 스피너 (회전하는 원)
                var spinner = new Border
                {
                    Width = 40,
                    Height = 40,
                    CornerRadius = new CornerRadius(20),
                    BorderThickness = new Thickness(4),
                    BorderBrush = new SolidColorBrush(Color.Parse("#E67E22")),
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center
                };
                
                _loadingText = new TextBlock
                {
                    Text = message,
                    FontSize = 16,
                    Foreground = Brushes.White,
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center
                };
                
                panel.Children.Add(spinner);
                panel.Children.Add(_loadingText);
                _loadingOverlay.Children.Add(panel);
                
                // Content가 Grid면 거기에 추가, 아니면 새 Grid로 감싸기
                if (this.Content is Grid contentGrid)
                {
                    Grid.SetRowSpan(_loadingOverlay, 10);
                    Grid.SetColumnSpan(_loadingOverlay, 10);
                    contentGrid.Children.Add(_loadingOverlay);
                }
                else if (this.Content is Control existingContent)
                {
                    var wrapper = new Grid();
                    this.Content = null;
                    wrapper.Children.Add(existingContent);
                    wrapper.Children.Add(_loadingOverlay);
                    this.Content = wrapper;
                }
            }
            
            if (_loadingText != null)
                _loadingText.Text = message;
            _loadingOverlay.IsVisible = true;
        }
        
        // ⭐ 로딩 오버레이 업데이트
        private void UpdateLoadingOverlay(string message)
        {
            if (_loadingText != null)
                _loadingText.Text = message;
        }
        
        // ⭐ 로딩 오버레이 숨기기
        private void HideLoadingOverlay()
        {
            if (_loadingOverlay != null)
                _loadingOverlay.IsVisible = false;
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
                // 플래그 리셋
                _dataAlreadyLoaded = false;
                
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
                
                // 카테고리 폴더 초기화
                var categoriesPath = System.IO.Path.Combine(predviaPath, "Categories");
                if (Directory.Exists(categoriesPath))
                {
                    var fileCount = Directory.GetFiles(categoriesPath).Length;
                    Directory.Delete(categoriesPath, true);
                    totalDeleted += fileCount;
                }
                
                // 로그 출력 (지연 후)
                if (totalDeleted > 0)
                {
                    Task.Delay(500).ContinueWith(_ =>
                    {
                        Dispatcher.UIThread.Post(() =>
                        {
                            try
                            {
                                LogWindow.AddLogStatic($"🧹 자동 초기화 완료 (파일 {totalDeleted}개 삭제)");
                            }
                            catch { }
                        });
                    });
                }
                
                // 지연 시간 증가 (제거)
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
                
                // ⭐ 키워드 타이머 중단 (API 직접 호출이므로 불필요)
                if (_keywordCheckTimer != null)
                {
                    _keywordCheckTimer.Stop();
                    _keywordCheckTimer = null;
                }
                
                // ⭐ API 직접 호출 방식으로 변경
                var keywords = await FetchNaverShoppingKeywords(keyword);
                
                if (keywords.Count > 0)
                {
                    LogWindow.AddLogStatic($"✅ {keywords.Count}개 키워드 추출 완료");
                    
                    // ⭐ 키워드 태그 바로 표시 (productKey 기반)
                    await Dispatcher.UIThread.InvokeAsync(() => {
                        CreateKeywordTagsByKey(keywords, _keywordSourceProductKey);
                    });
                }
                else
                {
                    LogWindow.AddLogStatic("⚠️ 키워드를 찾을 수 없습니다.");
                }
            }
            catch (Exception ex)
            {
                LogWindow.AddLogStatic($"❌ 네이버 가격비교 검색 오류: {ex.Message}");
            }
        }
        
        // ⭐ 네이버 쇼핑 공식 API 호출
        private async Task<List<string>> FetchNaverShoppingKeywords(string keyword)
        {
            var keywords = new HashSet<string>();
            
            try
            {
                using var client = new HttpClient();
                client.Timeout = TimeSpan.FromSeconds(15);
                
                // ⭐ 네이버 공식 API 인증 헤더
                client.DefaultRequestHeaders.Add("X-Naver-Client-Id", "Zz3SveXPGR6zk23yhvMc");
                client.DefaultRequestHeaders.Add("X-Naver-Client-Secret", "obIzHCgU2g");
                
                // 네이버 쇼핑 검색 API 호출
                var encodedKeyword = Uri.EscapeDataString(keyword);
                var apiUrl = $"https://openapi.naver.com/v1/search/shop.json?query={encodedKeyword}&display=100&sort=sim";
                
                LogWindow.AddLogStatic($"📡 네이버 공식 API 요청: {keyword}");
                
                var response = await client.GetStringAsync(apiUrl);
                
                // JSON 파싱
                using var doc = JsonDocument.Parse(response);
                var root = doc.RootElement;
                
                if (root.TryGetProperty("items", out var items))
                {
                    foreach (var item in items.EnumerateArray())
                    {
                        if (item.TryGetProperty("title", out var title))
                        {
                            var name = title.GetString();
                            if (!string.IsNullOrEmpty(name))
                            {
                                // HTML 태그 제거
                                name = System.Text.RegularExpressions.Regex.Replace(name, "<.*?>", "");
                                
                                // 상품명을 단어별로 분리
                                var words = name.Split(new[] { ' ', ',', '/', '(', ')', '[', ']', '+', '-', '·' }, 
                                    StringSplitOptions.RemoveEmptyEntries);
                                
                                foreach (var word in words)
                                {
                                    var cleanWord = word.Trim();
                                    // 한글만 추출 (2글자 이상)
                                    if (cleanWord.Length >= 2 && cleanWord.Any(c => c >= 0xAC00 && c <= 0xD7AF))
                                    {
                                        keywords.Add(cleanWord);
                                    }
                                }
                            }
                        }
                    }
                }
                
                LogWindow.AddLogStatic($"✅ API 응답 파싱 완료: {keywords.Count}개 키워드");
            }
            catch (Exception ex)
            {
                LogWindow.AddLogStatic($"❌ API 호출 오류: {ex.Message}");
            }
            
            return keywords.ToList();
        }
        
        // 서버에 키워드 전송
        private async Task SendKeywordsToServer(List<string> keywords)
        {
            try
            {
                var json = JsonSerializer.Serialize(new { keywords });
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                
                using var client = new HttpClient();
                await client.PostAsync("http://localhost:8080/api/smartstore/keywords", content);
            }
            catch (Exception ex)
            {
                LogWindow.AddLogStatic($"❌ 키워드 전송 오류: {ex.Message}");
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
                LogWindow.AddLogStatic($"✅ 크롤링 플래그 설정 완료 - {type}");

                // ⭐ 네이버 가격비교 페이지 열기 (백그라운드 렌더링)
                var encodedKeyword = Uri.EscapeDataString(searchText);
                var searchUrl = $"https://search.shopping.naver.com/search/all?query={encodedKeyword}&productSet=overseas";

                LogWindow.AddLogStatic($"🌐 크롤링 시작: {searchUrl}");

                _extensionService ??= new ChromeExtensionService();
                var success = await _extensionService.OpenNaverPriceComparison(searchUrl);

                if (success)
                {
                    button.Content = "페이지 로딩 중";
                    LogWindow.AddLogStatic($"✅ {type} 브라우저 열기 완료 - 페이지 로딩 대기 중...");

                    // ⭐ 페이지 로딩 대기 (3초)
                    await Task.Delay(3000);

                    // ⭐ 영수증 CAPTCHA 감지
                    var hasCaptcha = await CheckForReceiptCaptcha();
                    if (hasCaptcha)
                    {
                        LogWindow.AddLogStatic($"⚠️ 영수증 인증 CAPTCHA 감지됨");

                        // 메시지 박스 표시
                        var messageBox = new Window
                        {
                            Title = "로그인 필요",
                            Width = 400,
                            Height = 200,
                            WindowStartupLocation = WindowStartupLocation.CenterScreen,
                            CanResize = false
                        };

                        var okButton = new Button
                        {
                            Content = "확인",
                            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                            Padding = new Thickness(40, 10)
                        };

                        okButton.Click += (s, e) => messageBox.Close();

                        messageBox.Content = new StackPanel
                        {
                            Margin = new Thickness(20),
                            Spacing = 20,
                            Children =
                            {
                                new TextBlock
                                {
                                    Text = "네이버에 로그인이 필요합니다.\n먼저 로그인을 완료해주세요.",
                                    FontSize = 16,
                                    TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center
                                },
                                okButton
                            }
                        };

                        await messageBox.ShowDialog((Window)this.VisualRoot!);

                        // 브라우저 종료
                        await ChromeExtensionService.CloseNaverPriceComparisonWindowByTitle();
                        LogWindow.AddLogStatic($"🔒 영수증 CAPTCHA로 인해 브라우저 종료됨");

                        button.Content = "로그인 필요";
                        await Task.Delay(2000);
                        return;
                    }

                    button.Content = "크롤링 중";
                    LogWindow.AddLogStatic($"✅ {type} 크롤링 시작 완료");
                }
                else
                {
                    button.Content = "연결 실패";
                    LogWindow.AddLogStatic($"❌ {type} 크롤링 시작 실패");
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

        // ⭐ 영수증 CAPTCHA 감지 메서드 (서버 플래그 확인)
        private async Task<bool> CheckForReceiptCaptcha()
        {
            try
            {
                // Chrome 확장이 div.captcha_img_cover를 감지하고 서버에 알렸는지 확인
                await Task.Delay(100); // 비동기 호환성
                return ThumbnailWebServer.Instance?.CheckAndResetCaptcha() ?? false;
            }
            catch (Exception ex)
            {
                LogWindow.AddLogStatic($"❌ CAPTCHA 확인 오류: {ex.Message}");
                return false;
            }
        }

        // ⭐ 크롤링 버튼 클릭 핸들러 (OpenNaverPriceComparison 사용)
        private async Task HandleCrawlingButtonClick(TextBox? textBox, Button? button, string type)
        {
            Debug.WriteLine($"🔥 HandleCrawlingButtonClick 호출됨 - {type}");
            if (textBox == null || button == null)
            {
                Debug.WriteLine($"❌ TextBox 또는 Button이 null");
                return;
            }

            var mainWindow = (MainWindow?)this.VisualRoot;

            try
            {
                // 🔄 로딩창 표시
                mainWindow?.ShowLoading();

                button.IsEnabled = false;
                button.Content = "크롤링 중...";

                var searchText = textBox.Text?.Trim();
                if (string.IsNullOrEmpty(searchText))
                {
                    button.Content = "입력 필요";
                    await Task.Delay(2000);
                    return;
                }

                // ⭐ 크롤링 허용 플래그 설정
                await SetCrawlingAllowed();
                LogWindow.AddLogStatic($"✅ 크롤링 플래그 설정 완료 - {type}");

                // ⭐ 네이버 가격비교 페이지 열기 (크롤링 모드)
                var encodedKeyword = Uri.EscapeDataString(searchText);
                var searchUrl = $"https://search.shopping.naver.com/search/all?query={encodedKeyword}&productSet=overseas";

                LogWindow.AddLogStatic($"🌐 크롤링 시작: {searchUrl}");

                _extensionService ??= new ChromeExtensionService();
                var success = await _extensionService.OpenNaverPriceComparison(searchUrl);

                if (success)
                {
                    button.Content = "크롤링 시작됨";
                    LogWindow.AddLogStatic($"✅ {type} 크롤링 시작 완료");
                }
                else
                {
                    button.Content = "연결 실패";
                    LogWindow.AddLogStatic($"❌ {type} 크롤링 시작 실패");
                }
                await Task.Delay(1500);
            }
            catch (Exception ex)
            {
                button.Content = "연결 실패";
                LogWindow.AddLogStatic($"❌ 크롤링 버튼 오류: {ex.Message}");
                await Task.Delay(2000);
            }
            finally
            {
                if (button != null)
                {
                    button.IsEnabled = true;
                    button.Content = "페어링";
                }
                mainWindow?.HideLoading();
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

        // ⭐ 서버에 현재 상품 ID 설정
        private async Task SetCurrentProductId(int productId)
        {
            try
            {
                using var client = new HttpClient();
                var content = new StringContent(
                    JsonSerializer.Serialize(new { productId = productId }),
                    System.Text.Encoding.UTF8,
                    "application/json"
                );
                var response = await client.PostAsync("http://localhost:8080/api/smartstore/set-current-product", content);
                
                if (response.IsSuccessStatusCode)
                {
                    LogWindow.AddLogStatic($"✅ 서버에 현재 상품 ID 설정 완료: {productId}");
                }
                else
                {
                    LogWindow.AddLogStatic($"❌ 서버에 현재 상품 ID 설정 실패: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                LogWindow.AddLogStatic($"❌ 현재 상품 ID 설정 오류: {ex.Message}");
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
                    await CheckAndCreateKeywordTags();
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
                var currentProductKey = _keywordSourceProductKey;
                if (string.IsNullOrEmpty(currentProductKey)) return;
                
                // productKey에서 productId 추출 (기존 API 호환)
                var product = _productElements.Values.FirstOrDefault(p => $"{p.StoreId}_{p.RealProductId}" == currentProductKey);
                if (product == null) return;
                
                var keywords = await GetLatestKeywordsFromServer(product.ProductId);
                
                if (keywords != null && keywords.Count > 0)
                {
                    LogWindow.AddLogStatic($"🏷️ 키워드 {keywords.Count}개 발견 - 태그 생성 시작");
                    
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        CreateKeywordTagsByKey(keywords, currentProductKey);
                    });
                    
                    LogWindow.AddLogStatic("✅ 키워드 태그 자동 생성 완료");
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
                // ⭐ 현재 상품 키를 로컬 변수로 캡처
                var currentProductKey = _keywordSourceProductKey;
                if (string.IsNullOrEmpty(currentProductKey))
                {
                    LogWindow.AddLogStatic("⚠️ 키워드 소스 상품 키가 없습니다.");
                    return;
                }
                
                // productKey에서 productId 추출 (기존 API 호환)
                var product = _productElements.Values.FirstOrDefault(p => $"{p.StoreId}_{p.RealProductId}" == currentProductKey);
                if (product == null)
                {
                    LogWindow.AddLogStatic($"⚠️ 키 {currentProductKey}에 해당하는 상품을 찾을 수 없습니다.");
                    return;
                }
                
                LogWindow.AddLogStatic($"🏷️ SourcingPage - 키워드 태그 생성 시작 (키: {currentProductKey})");
                
                // ⭐ 실제 서버에서 키워드 받아오기
                var keywords = await GetLatestKeywordsFromServer(product.ProductId);
                
                if (keywords != null)
                {
                    if (keywords.Count > 0)
                    {
                        LogWindow.AddLogStatic($"🏷️ 서버에서 키워드 {keywords.Count}개 수신: {string.Join(", ", keywords.Take(5))}...");
                    }
                    else
                    {
                        LogWindow.AddLogStatic($"🏷️ 서버에서 빈 키워드 수신 (키: {currentProductKey})");
                    }
                    
                    // ⭐ 상품별로 키워드 저장
                    _productKeywords[product.ProductId] = keywords;
                    
                    // ⭐ 키워드가 있든 없든 무조건 UI 업데이트 (기존 태그 제거 포함)
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        CreateKeywordTagsByKey(keywords, currentProductKey);
                    });
                    
                    LogWindow.AddLogStatic($"✅ 키워드 태그 UI 업데이트 완료 (키: {currentProductKey}, 키워드 {keywords.Count}개)");
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

        // ⭐ 키워드 태그 UI 생성 (productKey 기반 - 삭제 후에도 정확한 카드 찾기)
        private void CreateKeywordTagsByKey(List<string> keywords, string targetProductKey)
        {
            try
            {
                LogWindow.AddLogStatic($"🏷️ {keywords.Count}개 키워드 태그 생성 시작 (키: {targetProductKey})");
                
                if (string.IsNullOrEmpty(targetProductKey))
                {
                    LogWindow.AddLogStatic("❌ 대상 상품 키가 없습니다.");
                    return;
                }
                
                // ⭐ RealDataContainer에서 Tag로 상품 카드 찾기
                var container = this.FindControl<StackPanel>("RealDataContainer");
                if (container == null)
                {
                    LogWindow.AddLogStatic("❌ RealDataContainer를 찾을 수 없습니다.");
                    return;
                }

                // Tag가 targetProductKey와 일치하는 카드 찾기
                var targetProductCard = container.Children.OfType<StackPanel>()
                    .FirstOrDefault(sp => sp.Tag?.ToString() == targetProductKey);

                if (targetProductCard == null)
                {
                    LogWindow.AddLogStatic($"❌ 키 {targetProductKey}에 해당하는 카드를 찾을 수 없습니다.");
                    return;
                }
                
                LogWindow.AddLogStatic($"🎯 키 {targetProductKey}에 해당하는 카드 발견");

                // ⭐ 기존 키워드 패널 완전 제거
                var existingKeywordPanel = targetProductCard.Children.OfType<StackPanel>()
                    .FirstOrDefault(sp => sp.Name == "KeywordTagPanel");
                if (existingKeywordPanel != null)
                {
                    targetProductCard.Children.Remove(existingKeywordPanel);
                }

                if (keywords == null || keywords.Count == 0) return;

                // ⭐ 키워드 태그 패널 생성
                var keywordPanel = new StackPanel
                {
                    Name = "KeywordTagPanel",
                    Orientation = Orientation.Vertical,
                    Margin = new Thickness(0, 15, 0, 15),
                    Spacing = 10
                };

                var keywordBorder = new Border
                {
                    BorderBrush = new SolidColorBrush(Color.Parse("#FF8A46")),
                    BorderThickness = new Thickness(2),
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(10, 10),
                    Height = 170,
                    Width = 1150,
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left,
                    Background = new SolidColorBrush(Colors.Transparent)
                };

                var keywordScrollViewer = new ScrollViewer
                {
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
                };

                var keywordWrapPanel = new StackPanel { Orientation = Orientation.Vertical, Spacing = 5 };
                var currentRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
                double currentRowWidth = 0;
                const double maxRowWidth = 1100;

                foreach (var keyword in keywords)
                {
                    var keywordTag = new Border
                    {
                        Background = new SolidColorBrush(Color.Parse("#E67E22")),
                        CornerRadius = new CornerRadius(12),
                        Padding = new Thickness(10, 5),
                        Cursor = new Cursor(StandardCursorType.Hand),
                        Tag = false, // ⭐ 사용 여부 추적
                        Child = new TextBlock
                        {
                            Text = keyword,
                            Foreground = Brushes.White,
                            FontSize = 11,
                            FontWeight = FontWeight.Medium,
                            FontFamily = new FontFamily("Malgun Gothic")
                        }
                    };

                    // ⭐ 키워드 태그 클릭 - 토글 방식
                    keywordTag.PointerPressed += (s, e) =>
                    {
                        if (s is Border border && border.Child is TextBlock tb)
                        {
                            var product = _productElements.Values.FirstOrDefault(p => $"{p.StoreId}_{p.RealProductId}" == targetProductKey);
                            if (product?.NameInputBox == null) return;
                            
                            var kw = tb.Text ?? "";
                            var isUsed = (bool)(border.Tag ?? false);
                            var currentText = product.NameInputBox.Text ?? "";
                            
                            if (isUsed)
                            {
                                // 사용 중 → 제거 + 주황색 복원
                                var newText = currentText.Replace(kw, "").Replace("  ", " ").Trim();
                                product.NameInputBox.Text = newText;
                                border.Background = new SolidColorBrush(Color.Parse("#E67E22"));
                                tb.Foreground = Brushes.White;
                                border.Tag = false;
                            }
                            else
                            {
                                // 미사용 → 추가 + 회색 변경
                                var newText = string.IsNullOrEmpty(currentText) ? kw : $"{currentText} {kw}";
                                product.NameInputBox.Text = newText;
                                border.Background = new SolidColorBrush(Color.Parse("#CCCCCC"));
                                tb.Foreground = new SolidColorBrush(Color.Parse("#666666"));
                                border.Tag = true;
                            }
                            
                            // 바이트 수 업데이트
                            if (product.ByteCountTextBlock != null)
                            {
                                var byteCount = CalculateByteCount(product.NameInputBox.Text ?? "");
                                product.ByteCountTextBlock.Text = $"{byteCount}/50 byte";
                                product.ByteCountTextBlock.Foreground = byteCount > 50 ? Brushes.Red : new SolidColorBrush(Colors.Gray);
                            }
                        }
                    };

                    double tagWidth = keyword.Length * 12 + 30;
                    if (currentRowWidth + tagWidth > maxRowWidth && currentRow.Children.Count > 0)
                    {
                        keywordWrapPanel.Children.Add(currentRow);
                        currentRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
                        currentRowWidth = 0;
                    }

                    currentRow.Children.Add(keywordTag);
                    currentRowWidth += tagWidth;
                }

                if (currentRow.Children.Count > 0)
                    keywordWrapPanel.Children.Add(currentRow);

                keywordScrollViewer.Content = keywordWrapPanel;
                keywordBorder.Child = keywordScrollViewer;
                keywordPanel.Children.Add(keywordBorder);

                // 리뷰 Border 앞에 삽입
                var insertIndex = -1;
                if (targetProductCard.Children.Count > 2 && targetProductCard.Children[2] is Border)
                    insertIndex = 2;

                if (insertIndex >= 0)
                    targetProductCard.Children.Insert(insertIndex, keywordPanel);
                else
                    targetProductCard.Children.Add(keywordPanel);

                LogWindow.AddLogStatic($"✅ 키워드 태그 {keywords.Count}개 UI 생성 완료 (키: {targetProductKey})");
            }
            catch (Exception ex)
            {
                LogWindow.AddLogStatic($"❌ 키워드 태그 생성 오류: {ex.Message}");
            }
        }
        
        // ⭐ 키워드 태그 클릭 이벤트 (productKey 기반)
        private void OnKeywordTagClickedByKey(string keyword, string productKey)
        {
            try
            {
                var product = _productElements.Values.FirstOrDefault(p => $"{p.StoreId}_{p.RealProductId}" == productKey);
                if (product?.NameInputBox != null)
                {
                    var currentText = product.NameInputBox.Text ?? "";
                    product.NameInputBox.Text = string.IsNullOrEmpty(currentText) ? keyword : $"{currentText} {keyword}";
                    LogWindow.AddLogStatic($"🏷️ 키워드 '{keyword}' 추가됨 (키: {productKey})");
                }
            }
            catch (Exception ex)
            {
                LogWindow.AddLogStatic($"❌ 키워드 태그 클릭 오류: {ex.Message}");
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

                // ⭐ 기존 키워드 패널 완전 제거 (강제)
                var existingKeywordPanel = targetProductCard.Children.OfType<StackPanel>()
                    .FirstOrDefault(sp => sp.Name == "KeywordTagPanel");
                if (existingKeywordPanel != null)
                {
                    targetProductCard.Children.Remove(existingKeywordPanel);
                    LogWindow.AddLogStatic($"🧹 기존 키워드 패널 제거 완료 (상품 ID: {targetProductId})");
                }
                else
                {
                    LogWindow.AddLogStatic($"ℹ️ 기존 키워드 패널 없음 (상품 ID: {targetProductId})");
                }

                // ⭐ 키워드가 없으면 빈 패널만 생성하고 종료
                if (keywords == null || keywords.Count == 0)
                {
                    LogWindow.AddLogStatic($"ℹ️ 키워드 없음 - 패널 생성 안함 (상품 ID: {targetProductId})");
                    return;
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
                    Padding = new Thickness(10, 10),
                    Height = 170,
                    Width = 1150, // ⭐ 너비 1150px 고정
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left,
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
                const double maxRowWidth = 1100; // ⭐ 1150px - 패딩20px - 스크롤바30px

                // 키워드 태그 생성 (전체)
                foreach (var keyword in keywords)
                {
                    var keywordTag = new Border
                    {
                        Background = new SolidColorBrush(Color.Parse("#E67E22")), // 주황색 (활성)
                        CornerRadius = new CornerRadius(12),
                        Padding = new Thickness(10, 5),
                        Cursor = new Cursor(StandardCursorType.Hand),
                        Tag = false, // ⭐ 사용 여부 추적
                        Child = new TextBlock
                        {
                            Text = keyword,
                            Foreground = Brushes.White,
                            FontSize = 11,
                            FontWeight = FontWeight.Medium,
                            FontFamily = new FontFamily("Malgun Gothic")
                        }
                    };

                    // ⭐ 키워드 태그 클릭 이벤트 - 토글 방식
                    keywordTag.PointerPressed += (s, e) =>
                    {
                        if (s is Border border && border.Child is TextBlock tb &&
                            _productElements.TryGetValue(targetProductId, out var product) &&
                            product.NameInputBox != null)
                        {
                            var kw = tb.Text ?? "";
                            var isUsed = (bool)(border.Tag ?? false);
                            var currentText = product.NameInputBox.Text ?? "";
                            
                            if (isUsed)
                            {
                                // 사용 중 → 제거 + 주황색 복원
                                var newText = currentText.Replace(kw, "").Replace("  ", " ").Trim();
                                product.NameInputBox.Text = newText;
                                border.Background = new SolidColorBrush(Color.Parse("#E67E22"));
                                tb.Foreground = Brushes.White;
                                border.Tag = false;
                            }
                            else
                            {
                                // 미사용 → 추가 + 회색 변경
                                var newText = string.IsNullOrEmpty(currentText) ? kw : $"{currentText} {kw}";
                                product.NameInputBox.Text = newText;
                                border.Background = new SolidColorBrush(Color.Parse("#CCCCCC"));
                                tb.Foreground = new SolidColorBrush(Color.Parse("#666666"));
                                border.Tag = true;
                            }
                            
                            // 바이트 수 업데이트
                            if (product.ByteCountTextBlock != null)
                            {
                                var byteCount = CalculateByteCount(product.NameInputBox.Text ?? "");
                                product.ByteCountTextBlock.Text = $"{byteCount}/50 byte";
                                product.ByteCountTextBlock.Foreground = byteCount > 50 ? Brushes.Red : new SolidColorBrush(Colors.Gray);
                            }
                        }
                    };

                    // 예상 태그 너비 계산 (한글 기준 - FontSize 11, 여유있게)
                    double tagWidth = keyword.Length * 12 + 30; // 한글 글자당 12px + 패딩30

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
                var byteCount = CalculateByteCount(text); // 통일된 계산 방식 사용
                
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
        
        // 가격 필터 설정 버튼 클릭
        private async void PriceFilterButton_Click(object? sender, RoutedEventArgs e)
        {
            try
            {
                LogWindow.AddLogStatic($"🔍 UI 요소 체크 - MinPriceTextBox: {MinPriceTextBox != null}");
                LogWindow.AddLogStatic($"🔍 UI 요소 체크 - MaxPriceTextBox: {MaxPriceTextBox != null}");
                
                if (MinPriceTextBox == null || MaxPriceTextBox == null)
                {
                    LogWindow.AddLogStatic("❌ UI 요소를 찾을 수 없습니다. FindControl로 다시 시도합니다.");
                    
                    var minBox = this.FindControl<TextBox>("MinPriceTextBox");
                    var maxBox = this.FindControl<TextBox>("MaxPriceTextBox");
                    
                    LogWindow.AddLogStatic($"🔍 FindControl 결과 - Min: {minBox != null}, Max: {maxBox != null}");
                    
                    if (minBox != null && maxBox != null)
                    {
                        LogWindow.AddLogStatic($"🔍 FindControl 값 - Min: '{minBox.Text}', Max: '{maxBox.Text}'");
                        
                        var minText = minBox.Text?.Replace(",", "").Replace("원", "").Trim() ?? "";
                        var maxText = maxBox.Text?.Replace(",", "").Replace("원", "").Trim() ?? "";
                        
                        if (int.TryParse(minText, out int minPrice) && int.TryParse(maxText, out int maxPrice))
                        {
                            LogWindow.AddLogStatic($"✅ 가격 파싱 성공: {minPrice} ~ {maxPrice}");
                            
                            // 서버에 가격 필터 설정 전송
                            var settings = new { enabled = true, minPrice = minPrice, maxPrice = maxPrice };
                            using var client = new HttpClient();
                            var json = JsonSerializer.Serialize(settings);
                            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
                            var response = await client.PostAsync("http://localhost:8080/api/price-filter/settings", content);
                            
                            if (response.IsSuccessStatusCode)
                            {
                                LogWindow.AddLogStatic($"✅ 가격 필터 설정 완료: {minPrice:N0}원 ~ {maxPrice:N0}원");
                            }
                            else
                            {
                                LogWindow.AddLogStatic($"❌ 가격 필터 설정 실패: {response.StatusCode}");
                            }
                            return;
                        }
                        else
                        {
                            LogWindow.AddLogStatic($"❌ 가격 파싱 실패 - Min: '{minText}', Max: '{maxText}'");
                        }
                    }
                    return;
                }
                
                // UI에서 가격 값 가져오기
                var minPriceText = MinPriceTextBox?.Text?.Replace(",", "").Replace("원", "").Trim();
                var maxPriceText = MaxPriceTextBox?.Text?.Replace(",", "").Replace("원", "").Trim();
                
                LogWindow.AddLogStatic($"🔍 디버그 - 최소가격: '{MinPriceTextBox?.Text}' → '{minPriceText}'");
                LogWindow.AddLogStatic($"🔍 디버그 - 최대가격: '{MaxPriceTextBox?.Text}' → '{maxPriceText}'");
                
                if (int.TryParse(minPriceText, out int minPrice2) && int.TryParse(maxPriceText, out int maxPrice2))
                {
                    LogWindow.AddLogStatic($"✅ 가격 필터 설정 완료: {minPrice2:N0}원 ~ {maxPrice2:N0}원");
                }
                else
                {
                    LogWindow.AddLogStatic($"❌ 가격 파싱 실패 - 최소: '{minPriceText}', 최대: '{maxPriceText}'");
                }
            }
            catch (Exception ex)
            {
                LogWindow.AddLogStatic($"❌ 가격 필터 설정 오류: {ex.Message}");
            }
        }
        
        // ⭐ 타오바오 상품 박스 업데이트
        private void UpdateTaobaoProductBoxes(int cardId, List<TaobaoProductData> products)
        {
            try
            {
                // ⭐ ProductUIElements에 타오바오 데이터 저장
                if (_productElements.TryGetValue(cardId, out var productElement))
                {
                    productElement.TaobaoProducts = products;
                    productElement.IsTaobaoPaired = products.Count > 0;
                    LogWindow.AddLogStatic($"💾 상품 {cardId}에 타오바오 데이터 {products.Count}개 저장");
                }
                else
                {
                    return;
                }

                if (productElement.TaobaoProductsPanel == null) return;
                
                // 최대 5개 상품 표시
                for (int i = 0; i < Math.Min(5, products.Count); i++)
                {
                    var product = products[i];
                    
                    // 기존 productBoxPanel의 자식 StackPanel 가져오기
                    if (i >= productElement.TaobaoProductsPanel.Children.Count) break;
                    var productBox = productElement.TaobaoProductsPanel.Children[i] as StackPanel;
                    if (productBox == null || productBox.Children.Count < 2) continue;
                    
                    var logoBorder = productBox.Children[0] as Border;
                    var infoText = productBox.Children[1] as TextBlock;
                    var openUrlButton = productBox.Children.Count > 2 ? productBox.Children[2] as Button : null;
                    if (logoBorder == null) continue;
                    
                    // URL 설정
                    var productUrl = product.ProductUrl;
                    if (string.IsNullOrEmpty(productUrl) && !string.IsNullOrEmpty(product.Nid))
                        productUrl = $"https://item.taobao.com/item.htm?id={product.Nid}";
                    logoBorder.Tag = $"{cardId}_{i}_url_{productUrl}";
                    
                    // ⭐ 클릭 이벤트 등록 (중복 방지)
                    logoBorder.PointerPressed -= OnTaobaoProductClick;
                    logoBorder.PointerPressed += OnTaobaoProductClick;
                    
                    // 이미지 로드
                    if (!string.IsNullOrEmpty(product.ImageUrl) && logoBorder.Child is Grid grid)
                    {
                        foreach (var child in grid.Children)
                        {
                            if (child is TextBlock placeholder) placeholder.IsVisible = false;
                            if (child is Avalonia.Controls.Image taobaoImg)
                            {
                                taobaoImg.IsVisible = true;
                                LoadTaobaoImage(taobaoImg, product.ImageUrl, cardId, i);
                            }
                        }
                    }
                    
                    // 가격 + 판매량 표시
                    if (infoText != null)
                    {
                        var priceStr = "";
                        if (!string.IsNullOrEmpty(product.Price) && product.Price != "0")
                        {
                            var priceNum = product.Price.Replace("CN¥", "").Replace("¥", "").Trim();
                            priceStr = $"{priceNum} 위안";
                        }
                        
                        var salesStr = "";
                        if (!string.IsNullOrEmpty(product.Sales) && product.Sales != "0")
                        {
                            salesStr = $" | 판매량 {product.Sales}";
                        }
                        
                        infoText.Text = priceStr + salesStr;
                        infoText.Foreground = Avalonia.Media.Brushes.Red;
                    }
                    
                    // ⭐ 상품 페이지 열기 버튼 표시
                    if (openUrlButton != null)
                    {
                        openUrlButton.IsVisible = true;
                    }
                }
                
                // ⭐ 선택된 인덱스 UI 업데이트
                UpdateTaobaoSelectionUI(cardId, productElement.SelectedTaobaoIndex);
                
                LogWindow.AddLogStatic($"✅ 타오바오 상품 박스 업데이트 완료: {cardId}");
            }
            catch (Exception ex)
            {
                LogWindow.AddLogStatic($"❌ 타오바오 상품 박스 업데이트 오류: {ex.Message}");
            }
        }

        // ⭐ 타오바오 이미지를 로컬에 다운로드
        private async Task<string?> DownloadTaobaoImageToLocal(string imageUrl, int cardId, int index)
        {
            try
            {
                // 로컬 저장 경로 (Predvia/TaobaoImages 폴더)
                var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                var taobaoImagesPath = System.IO.Path.Combine(appDataPath, "Predvia", "TaobaoImages");

                if (!Directory.Exists(taobaoImagesPath))
                {
                    Directory.CreateDirectory(taobaoImagesPath);
                }

                // 파일명 생성 (URL 해시로 고유 파일명)
                var urlHash = imageUrl.GetHashCode().ToString("X8");
                var fileName = $"taobao_{cardId}_{index}_{urlHash}.jpg";
                var localFilePath = System.IO.Path.Combine(taobaoImagesPath, fileName);

                // 이미 다운로드된 파일이 있으면 재사용
                if (File.Exists(localFilePath))
                {
                    LogWindow.AddLogStatic($"✅ 타오바오 이미지 캐시 사용: {fileName}");
                    return localFilePath;
                }

                // HTTP로 이미지 다운로드
                using var httpClient = new HttpClient();
                httpClient.Timeout = TimeSpan.FromSeconds(15);
                httpClient.DefaultRequestHeaders.Add("User-Agent",
                    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
                httpClient.DefaultRequestHeaders.Add("Referer", "https://www.taobao.com/");

                var imageBytes = await httpClient.GetByteArrayAsync(imageUrl);

                // 로컬에 저장
                await File.WriteAllBytesAsync(localFilePath, imageBytes);

                LogWindow.AddLogStatic($"✅ 타오바오 이미지 다운로드 완료: {fileName} ({imageBytes.Length} bytes)");
                return localFilePath;
            }
            catch (Exception ex)
            {
                LogWindow.AddLogStatic($"⚠️ 타오바오 이미지 다운로드 실패 ({imageUrl}): {ex.Message}");
                return null;
            }
        }

    private bool _isTaobaoSearchRunning = false; // 중복 실행 방지 플래그
    
    // 타오바오 테스트 버튼 클릭 이벤트
        private async void TaobaoTestButton_Click(object? sender, RoutedEventArgs e)
        {
            try
            {
                // 중복 실행 방지
                if (_isTaobaoSearchRunning)
                {
                    LogWindow.AddLogStatic("⏳ 타오바오 이미지 검색이 이미 진행 중입니다...");
                    return;
                }
                
                _isTaobaoSearchRunning = true;
                
                LogWindow.AddLogStatic("🧪 타오바오 이미지 검색 테스트 시작");
                
                // 버튼 비활성화
                var button = sender as Button;
                if (button != null)
                {
                    button.IsEnabled = false;
                    button.Content = "쿠키 수집 중...";
                }
                
                // 1단계: 타오바오 페이지를 열어서 쿠키 수집 트리거 (기존 탭 확인)
                LogWindow.AddLogStatic("🍪 타오바오 페이지 열어서 쿠키 수집 중...");
                
                // 기존 Chrome 프로세스에서 타오바오 탭이 있는지 확인
                var existingChromeProcesses = System.Diagnostics.Process.GetProcessesByName("chrome");
                bool shouldOpenNewTab = existingChromeProcesses.Length == 0;
                
                try
                {
                    // Chrome으로 타오바오 페이지 열기 (확장프로그램이 자동으로 쿠키 수집)
                    var chromeProcessInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "chrome",
                        Arguments = "--new-tab https://www.taobao.com",
                        UseShellExecute = true
                    };
                    
                    System.Diagnostics.Process.Start(chromeProcessInfo);
                    LogWindow.AddLogStatic("✅ 타오바오 페이지 열림 - 확장프로그램이 쿠키 수집 중...");
                    
                    // 쿠키 수집 대기
                    await Task.Delay(5000);
                    
                    // 서버에서 쿠키 상태 확인
                    using var client = new HttpClient();
                    client.Timeout = TimeSpan.FromSeconds(5);
                    
                    var response = await client.GetAsync("http://localhost:8080/api/taobao/cookies");
                    if (response.IsSuccessStatusCode)
                    {
                        var responseText = await response.Content.ReadAsStringAsync();
                        LogWindow.AddLogStatic($"✅ 쿠키 상태: {responseText}");
                    }
                    else
                    {
                        LogWindow.AddLogStatic("⚠️ 쿠키 상태 확인 실패");
                    }
                }
                catch (Exception ex)
                {
                    LogWindow.AddLogStatic($"⚠️ 쿠키 수집 오류: {ex.Message}");
                }
                
                // 버튼 상태 업데이트
                if (button != null)
                {
                    button.Content = "파이썬 실행 중...";
                }
                
                // 2단계: 파이썬 run.py 실행
                LogWindow.AddLogStatic("🐍 파이썬 run.py 실행 중...");
                
                var pythonPath = System.IO.Path.Combine(Directory.GetCurrentDirectory(), "image_search_products-master");
                var imagePath = System.IO.Path.Combine(Directory.GetCurrentDirectory(), "images", "10.png");
                
                if (!File.Exists(imagePath))
                {
                    LogWindow.AddLogStatic($"❌ 테스트 이미지를 찾을 수 없습니다: {imagePath}");
                    return;
                }
                
                // 파이썬 프로세스 실행
                var processInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "python",
                    Arguments = "run.py",
                    WorkingDirectory = pythonPath,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };
                
                // UTF-8 인코딩 설정
                processInfo.EnvironmentVariables["PYTHONIOENCODING"] = "utf-8";
                processInfo.StandardOutputEncoding = System.Text.Encoding.UTF8;
                processInfo.StandardErrorEncoding = System.Text.Encoding.UTF8;
                
                // 타오바오 토큰을 환경변수로 전달
                var taobaoToken = Services.ThumbnailWebServer.GetTaobaoToken();
                if (!string.IsNullOrEmpty(taobaoToken))
                {
                    processInfo.EnvironmentVariables["TAOBAO_TOKEN"] = taobaoToken;
                    LogWindow.AddLogStatic($"🔑 타오바오 토큰을 Python에 전달: {taobaoToken.Substring(0, Math.Min(10, taobaoToken.Length))}...");
                }
                
                using var process = System.Diagnostics.Process.Start(processInfo);
                if (process != null)
                {
                    // 출력 읽기
                    var output = await process.StandardOutput.ReadToEndAsync();
                    var error = await process.StandardError.ReadToEndAsync();
                    
                    await process.WaitForExitAsync();
                    
                    if (process.ExitCode == 0)
                    {
                        LogWindow.AddLogStatic("✅ 파이썬 실행 성공");
                        LogWindow.AddLogStatic($"출력: {output}");
                    }
                    else
                    {
                        LogWindow.AddLogStatic($"❌ 파이썬 실행 실패 (코드: {process.ExitCode})");
                        LogWindow.AddLogStatic($"오류: {error}");
                    }
                }
                else
                {
                    LogWindow.AddLogStatic("❌ 파이썬 프로세스 시작 실패");
                }
            }
            catch (Exception ex)
            {
                LogWindow.AddLogStatic($"❌ 타오바오 테스트 오류: {ex.Message}");
            }
            finally
            {
                // 플래그 해제
                _isTaobaoSearchRunning = false;
                
                // 버튼 상태 복원
                var button = sender as Button;
                if (button != null)
                {
                    button.IsEnabled = true;
                    button.Content = "타오바오 페어링 테스트";
                }
            }
        }
        
        // ⭐ 타오바오 상품 클릭 이벤트 - 선택 기능만 (URL 열기는 버튼으로)
        private void OnTaobaoProductClick(object? sender, Avalonia.Input.PointerPressedEventArgs e)
        {
            try
            {
                if (sender is Border border)
                {
                    // Tag에서 cardId와 index 추출 (형식: "cardId_index_url_URL")
                    var tagParts = (border.Tag as string)?.Split(new[] { "_url_" }, 2, StringSplitOptions.None);
                    if (tagParts == null || tagParts.Length < 1) return;
                    
                    var idParts = tagParts[0].Split('_');
                    if (idParts.Length < 2) return;
                    
                    if (!int.TryParse(idParts[0], out int cardId)) return;
                    if (!int.TryParse(idParts[1], out int index)) return;
                    
                    // 선택된 인덱스 저장
                    if (_productElements.TryGetValue(cardId, out var product))
                    {
                        product.SelectedTaobaoIndex = index;
                        LogWindow.AddLogStatic($"✅ 상품 {cardId}: 타오바오 상품 {index + 1}번 선택됨");
                        
                        // UI 업데이트 - 선택된 상품 테두리 강조
                        UpdateTaobaoSelectionUI(cardId, index);
                    }
                }
            }
            catch (Exception ex)
            {
                LogWindow.AddLogStatic($"❌ 타오바오 상품 선택 오류: {ex.Message}");
            }
        }
        
        // ⭐ 타오바오 선택 UI 업데이트
        private void UpdateTaobaoSelectionUI(int cardId, int selectedIndex)
        {
            if (!_productElements.TryGetValue(cardId, out var product))
            {
                LogWindow.AddLogStatic($"⚠️ 선택 UI 업데이트 실패: cardId {cardId} 없음");
                return;
            }
            if (product.TaobaoProductsPanel == null)
            {
                LogWindow.AddLogStatic($"⚠️ 선택 UI 업데이트 실패: TaobaoProductsPanel null");
                return;
            }
            
            int index = 0;
            foreach (var child in product.TaobaoProductsPanel.Children)
            {
                if (child is StackPanel stackPanel && stackPanel.Children.Count > 0)
                {
                    var border = stackPanel.Children[0] as Border;
                    if (border != null)
                    {
                        // 선택된 상품은 주황색 테두리 3px, 나머지는 회색 1px
                        if (index == selectedIndex)
                        {
                            border.BorderBrush = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#E67E22"));
                            border.BorderThickness = new Thickness(3);
                            LogWindow.AddLogStatic($"🔶 상품 {cardId}: {index + 1}번 테두리 강조");
                        }
                        else
                        {
                            border.BorderBrush = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#CCCCCC"));
                            border.BorderThickness = new Thickness(1);
                        }
                    }
                    index++;
                }
            }
        }
        
        // ⭐ 타오바오 상품 URL 열기
        private void OpenTaobaoProductUrl(int cardId, int index)
        {
            try
            {
                if (!_productElements.TryGetValue(cardId, out var product)) return;
                if (product.TaobaoProducts == null || index >= product.TaobaoProducts.Count) return;
                
                var url = product.TaobaoProducts[index].ProductUrl;
                if (string.IsNullOrEmpty(url))
                {
                    var nid = product.TaobaoProducts[index].Nid;
                    if (!string.IsNullOrEmpty(nid))
                        url = $"https://item.taobao.com/item.htm?id={nid}";
                }
                
                if (!string.IsNullOrEmpty(url))
                {
                    LogWindow.AddLogStatic($"🔗 타오바오 상품 페이지 열기: {url}");
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = url,
                        UseShellExecute = true
                    });
                }
            }
            catch (Exception ex)
            {
                LogWindow.AddLogStatic($"❌ URL 열기 오류: {ex.Message}");
            }
        }

        // 파이썬 출력 파싱 헬퍼 메서드들
        private string ExtractPrice(JsonElement item)
        {
            try
            {
                // ⭐ 1순위: priceInfo 객체에서 추출
                if (item.TryGetProperty("priceInfo", out var priceInfoElement))
                {
                    double? priceValue = null;

                    // pcFinalPrice → wapFinalPrice → reservePrice 순서로 시도
                    if (priceInfoElement.TryGetProperty("pcFinalPrice", out var pcPriceElement))
                    {
                        if (pcPriceElement.ValueKind == JsonValueKind.Number)
                            priceValue = pcPriceElement.GetDouble();
                        else if (pcPriceElement.ValueKind == JsonValueKind.String)
                        {
                            var str = pcPriceElement.GetString();
                            if (double.TryParse(str, out var parsed))
                                priceValue = parsed;
                        }
                    }
                    else if (priceInfoElement.TryGetProperty("wapFinalPrice", out var wapPriceElement))
                    {
                        if (wapPriceElement.ValueKind == JsonValueKind.Number)
                            priceValue = wapPriceElement.GetDouble();
                        else if (wapPriceElement.ValueKind == JsonValueKind.String)
                        {
                            var str = wapPriceElement.GetString();
                            if (double.TryParse(str, out var parsed))
                                priceValue = parsed;
                        }
                    }
                    else if (priceInfoElement.TryGetProperty("reservePrice", out var reservePriceElement))
                    {
                        if (reservePriceElement.ValueKind == JsonValueKind.Number)
                            priceValue = reservePriceElement.GetDouble();
                        else if (reservePriceElement.ValueKind == JsonValueKind.String)
                        {
                            var str = reservePriceElement.GetString();
                            if (double.TryParse(str, out var parsed))
                                priceValue = parsed;
                        }
                    }

                    if (priceValue.HasValue)
                    {
                        return priceValue.Value.ToString("0.##");
                    }
                }

                // ⭐ 2순위: price 필드에서 직접 추출
                if (item.TryGetProperty("price", out var priceElement))
                {
                    if (priceElement.ValueKind == JsonValueKind.Number)
                        return priceElement.GetDouble().ToString("0.##");
                    else if (priceElement.ValueKind == JsonValueKind.String)
                    {
                        var priceStr = priceElement.GetString();
                        if (!string.IsNullOrEmpty(priceStr))
                        {
                            priceStr = System.Text.RegularExpressions.Regex.Replace(priceStr, @"[^\d\.]", "");
                            if (!string.IsNullOrEmpty(priceStr))
                                return priceStr;
                        }
                    }
                }

                // ⭐ 3순위: zkFinalPrice 시도
                if (item.TryGetProperty("zkFinalPrice", out var zkPriceElement))
                {
                    if (zkPriceElement.ValueKind == JsonValueKind.Number)
                        return zkPriceElement.GetDouble().ToString("0.##");
                    else if (zkPriceElement.ValueKind == JsonValueKind.String)
                    {
                        var zkPrice = zkPriceElement.GetString();
                        if (!string.IsNullOrEmpty(zkPrice))
                        {
                            zkPrice = System.Text.RegularExpressions.Regex.Replace(zkPrice, @"[^\d\.]", "");
                            if (!string.IsNullOrEmpty(zkPrice))
                                return zkPrice;
                        }
                    }
                }
            }
            catch { }
            return "";
        }
        
        private int ExtractReviewCount(JsonElement item)
        {
            try
            {
                if (item.TryGetProperty("comments", out var commentsElement) &&
                    commentsElement.TryGetProperty("nums", out var numsElement))
                {
                    return numsElement.GetInt32();
                }
            }
            catch { }
            return 0;
        }
        
        private string ExtractShopName(JsonElement item)
        {
            try
            {
                if (item.TryGetProperty("sellerInfo", out var sellerInfoElement) &&
                    sellerInfoElement.TryGetProperty("shopTitle", out var shopTitleElement))
                {
                    return shopTitleElement.GetString() ?? "";
                }
            }
            catch { }
            return "";
        }
        
        private string ExtractImageUrl(JsonElement item)
        {
            try
            {
                if (item.TryGetProperty("pics", out var picsElement) &&
                    picsElement.TryGetProperty("mainPic", out var imgElement))
                {
                    var imgUrl = imgElement.GetString() ?? "";
                    if (!string.IsNullOrEmpty(imgUrl))
                    {
                        // HTTPS 프로토콜 추가
                        if (!imgUrl.StartsWith("http"))
                            imgUrl = "https:" + imgUrl;

                        // ⭐ 고화질 이미지로 변경 (_sum.jpg, _q90.jpg 등 저화질 파라미터 제거)
                        imgUrl = System.Text.RegularExpressions.Regex.Replace(imgUrl, @"_\d+x\d+\.jpg", ".jpg"); // _300x300.jpg 제거
                        imgUrl = System.Text.RegularExpressions.Regex.Replace(imgUrl, @"_sum\.jpg", ".jpg");      // _sum.jpg 제거
                        imgUrl = System.Text.RegularExpressions.Regex.Replace(imgUrl, @"_q\d+\.jpg", ".jpg");     // _q90.jpg 제거

                        // ⭐ .jpg.jpg 중복 확장자 제거
                        imgUrl = System.Text.RegularExpressions.Regex.Replace(imgUrl, @"\.jpg\.jpg$", ".jpg");

                        return imgUrl;
                    }
                }
            }
            catch { }
            return "";
        }

        // ⭐ JSON 파싱 오류 방지: 잘못된 이스케이프 시퀀스 정리
        private static string CleanInvalidJsonEscapes(string jsonStr)
        {
            try
            {
                var sb = new System.Text.StringBuilder(jsonStr.Length);
                bool inString = false;
                bool escaped = false;

                for (int i = 0; i < jsonStr.Length; i++)
                {
                    char c = jsonStr[i];

                    // 문자열 내부인지 추적 (큰따옴표로만 판단)
                    if (c == '"' && !escaped)
                    {
                        inString = !inString;
                        sb.Append(c);
                        continue;
                    }

                    // 백슬래시 처리
                    if (c == '\\' && !escaped && inString)
                    {
                        if (i + 1 < jsonStr.Length)
                        {
                            char next = jsonStr[i + 1];

                            // 유효한 이스케이프 시퀀스: ", \, /, b, f, n, r, t, u
                            if (next == '"' || next == '\\' || next == '/' ||
                                next == 'b' || next == 'f' || next == 'n' ||
                                next == 'r' || next == 't')
                            {
                                sb.Append(c); // 백슬래시 유지
                                escaped = true;
                            }
                            else if (next == 'u')
                            {
                                // \uXXXX 형식 확인 (유니코드)
                                if (i + 5 < jsonStr.Length &&
                                    IsHexDigit(jsonStr[i + 2]) &&
                                    IsHexDigit(jsonStr[i + 3]) &&
                                    IsHexDigit(jsonStr[i + 4]) &&
                                    IsHexDigit(jsonStr[i + 5]))
                                {
                                    sb.Append(c); // 유효한 \uXXXX
                                    escaped = true;
                                }
                                else
                                {
                                    // 잘못된 \u 시퀀스 - 백슬래시를 이스케이프 처리
                                    sb.Append("\\\\");
                                }
                            }
                            else
                            {
                                // 잘못된 이스케이프 시퀀스 (예: \x) - 백슬래시를 이스케이프 처리
                                sb.Append("\\\\");
                            }
                        }
                        else
                        {
                            // 문자열 끝의 백슬래시 - 이스케이프 처리
                            sb.Append("\\\\");
                        }
                    }
                    else
                    {
                        sb.Append(c);
                        escaped = false;
                    }
                }

                return sb.ToString();
            }
            catch (Exception ex)
            {
                LogWindow.AddLogStatic($"⚠️ JSON 정리 실패: {ex.Message}");
                return jsonStr; // 실패 시 원본 반환
            }
        }

        // 16진수 문자 확인
        private static bool IsHexDigit(char c)
        {
            return (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
        }

        // ⭐ 상품 카드 데이터를 JSON으로 저장
        private async void SaveProductCardsToJson()
        {
            try
            {
                var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                var predviaPath = System.IO.Path.Combine(appDataPath, "Predvia");

                if (!Directory.Exists(predviaPath))
                {
                    Directory.CreateDirectory(predviaPath);
                }

                var jsonFilePath = System.IO.Path.Combine(predviaPath, "product_cards.json");

                // 기존 JSON 로드 (순서 유지)
                var productCards = new List<ProductCardData>();
                if (File.Exists(jsonFilePath))
                {
                    var existingJson = File.ReadAllText(jsonFilePath);
                    productCards = JsonSerializer.Deserialize<List<ProductCardData>>(existingJson) ?? new List<ProductCardData>();
                }

                // 현재 UI 데이터를 딕셔너리로
                var uiData = new Dictionary<string, ProductUIElements>();
                foreach (var p in _productElements.Values.Where(p => p.StoreId != null && p.RealProductId != null))
                {
                    uiData[$"{p.StoreId}_{p.RealProductId}"] = p;
                }

                // 기존 순서 유지하면서 업데이트
                foreach (var card in productCards)
                {
                    var key = $"{card.StoreId}_{card.RealProductId}";
                    if (uiData.TryGetValue(key, out var p))
                    {
                        int shippingCost = 0;
                        if (p.ShippingCostInput != null && !string.IsNullOrEmpty(p.ShippingCostInput.Text))
                        {
                            int.TryParse(p.ShippingCostInput.Text.Replace(",", ""), out shippingCost);
                        }
                        
                        card.ProductName = p.NameInputBox?.Text ?? card.ProductName;
                        card.IsTaobaoPaired = p.IsTaobaoPaired || card.IsTaobaoPaired;
                        card.TaobaoProducts = p.TaobaoProducts?.Count > 0 ? p.TaobaoProducts : card.TaobaoProducts;
                        card.ShippingCost = shippingCost > 0 ? shippingCost : card.ShippingCost;
                        card.SelectedTaobaoIndex = p.SelectedTaobaoIndex;
                    }
                }

                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                };

                var json = JsonSerializer.Serialize(productCards, options);
                File.WriteAllText(jsonFilePath, json);

                LogWindow.AddLogStatic($"💾 상품 데이터 저장 완료: {productCards.Count}개 상품 ({jsonFilePath})");
                
                // ⭐ 저장 완료 피드백
                await Dispatcher.UIThread.InvokeAsync(async () =>
                {
                    var msgBox = new Window
                    {
                        Title = "저장 완료",
                        Width = 300,
                        Height = 120,
                        WindowStartupLocation = WindowStartupLocation.CenterOwner,
                        CanResize = false,
                        Content = new StackPanel
                        {
                            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                            Children =
                            {
                                new TextBlock { Text = $"✅ {productCards.Count}개 상품 저장 완료!", FontSize = 16, Margin = new Thickness(0, 0, 0, 15) },
                                new Button { Content = "확인", Width = 80, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center }
                            }
                        }
                    };
                    ((msgBox.Content as StackPanel)?.Children[1] as Button)!.Click += (s, e) => msgBox.Close();
                    msgBox.Show();
                    await Task.Delay(2000);
                    if (msgBox.IsVisible) msgBox.Close();
                });
            }
            catch (Exception ex)
            {
                LogWindow.AddLogStatic($"❌ 상품 데이터 저장 실패: {ex.Message}");
            }
        }

        // ⭐ 이미지 URL 가져오기
        private string? GetProductImageUrl(int productId)
        {
            try
            {
                if (_productElements.TryGetValue(productId, out var product))
                {
                    var container = product.Container;
                    if (container != null)
                    {
                        var imageControl = container.FindAll<Avalonia.Controls.Image>().FirstOrDefault();
                        if (imageControl?.Source is Bitmap bitmap)
                        {
                            // 이미지 경로 추출
                            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                            var imagesPath = System.IO.Path.Combine(appDataPath, "Predvia", "Images");
                            return System.IO.Path.Combine(imagesPath, $"{product.StoreId}_{product.RealProductId}.jpg");
                        }
                    }
                }
            }
            catch { }
            return null;
        }
        
        // ⭐ 유효한 이미지 경로 반환 (동적 생성)
        private string GetValidImagePath(string? imageUrl, string storeId, string productId)
        {
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var imagesPath = System.IO.Path.Combine(appDataPath, "Predvia", "Images");
            var localPath = System.IO.Path.Combine(imagesPath, $"{storeId}_{productId}_main.jpg");
            
            // 로컬 파일이 있으면 사용
            if (File.Exists(localPath))
                return localPath;
            
            // imageUrl이 유효하고 파일이 있으면 사용
            if (!string.IsNullOrEmpty(imageUrl) && File.Exists(imageUrl))
                return imageUrl;
            
            // 없으면 로컬 경로 반환 (LazyImage에서 처리)
            return localPath;
        }

        // ⭐ JSON에서 상품 카드 데이터 로드 (페이지네이션)
        private async void LoadProductCardsFromJson()
        {
            try
            {
                var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                var predviaPath = System.IO.Path.Combine(appDataPath, "Predvia");
                var jsonFilePath = System.IO.Path.Combine(predviaPath, "product_cards.json");

                if (!File.Exists(jsonFilePath))
                {
                    return;
                }

                var json = File.ReadAllText(jsonFilePath);
                _allProductCards = JsonSerializer.Deserialize<List<ProductCardData>>(json) ?? new();

                if (_allProductCards.Count == 0)
                {
                    return;
                }
                
                LogWindow.AddLogStatic($"📂 JSON 파일에서 {_allProductCards.Count}개 상품 로드");
                
                _currentPage = 1;
                await LoadCurrentPage();
                
                // ⭐ 전체선택 체크박스 이벤트 연결
                if (_selectAllCheckBox == null)
                {
                    _selectAllCheckBox = this.FindControl<CheckBox>("SelectAllCheckBox");
                }
                if (_selectAllCheckBox != null)
                {
                    _selectAllCheckBox.Click -= SelectAllCheckBox_Click;
                    _selectAllCheckBox.Click += SelectAllCheckBox_Click;
                }
            }
            catch (Exception ex)
            {
                HideLoadingOverlay();
                LogWindow.AddLogStatic($"❌ 상품 데이터 로드 실패: {ex.Message}");
            }
        }
        
        // ⭐ 현재 페이지 로드
        private async Task LoadCurrentPage()
        {
            var container = this.FindControl<StackPanel>("RealDataContainer");
            if (container == null) return;
            
            // 기존 카드 초기화
            container.Children.Clear();
            _productElements.Clear();
            
            // 현재 페이지 데이터 가져오기
            var totalPages = (int)Math.Ceiling((double)_allProductCards.Count / _itemsPerPage);
            var pageCards = _allProductCards
                .Skip((_currentPage - 1) * _itemsPerPage)
                .Take(_itemsPerPage)
                .ToList();
            
            LogWindow.AddLogStatic($"📄 페이지 {_currentPage}/{totalPages} 로드 중... ({pageCards.Count}개)");
            
            int count = 0;
            foreach (var card in pageCards)
            {
                if (card.StoreId != null && card.RealProductId != null)
                {
                    AddProductImageCard(card.StoreId, card.RealProductId, card.ImageUrl ?? "", card.ProductName);
                    count++;
                    
                    // 타오바오 매칭 데이터 복원
                    if (card.TaobaoProducts != null && card.TaobaoProducts.Count > 0)
                    {
                        if (_productElements.TryGetValue(count, out var elem))
                        {
                            elem.SelectedTaobaoIndex = card.SelectedTaobaoIndex;
                        }
                        UpdateTaobaoProductBoxes(count, card.TaobaoProducts);
                    }
                }
            }
            
            // 페이지 정보 업데이트
            UpdatePageInfo();
            LogWindow.AddLogStatic($"✅ 페이지 {_currentPage}/{totalPages} 로드 완료");
        }
        
        // ⭐ 페이지 정보 업데이트
        private void UpdatePageInfo()
        {
            var totalPages = Math.Max(1, (int)Math.Ceiling((double)_allProductCards.Count / _itemsPerPage));
            _pageInfoText = this.FindControl<TextBlock>("PageInfoText");
            if (_pageInfoText != null)
            {
                _pageInfoText.Text = $"{_currentPage} / {totalPages} 페이지 (총 {_allProductCards.Count}개)";
            }
        }
        
        // ⭐ 이전 페이지
        protected async void PrevPage_Click(object? sender, RoutedEventArgs e)
        {
            if (_currentPage > 1)
            {
                _currentPage--;
                await LoadCurrentPage();
            }
        }
        
        // ⭐ 다음 페이지
        protected async void NextPage_Click(object? sender, RoutedEventArgs e)
        {
            var totalPages = (int)Math.Ceiling((double)_allProductCards.Count / _itemsPerPage);
            if (_currentPage < totalPages)
            {
                _currentPage++;
                await LoadCurrentPage();
            }
        }

        // ⭐ 외부에서 JSON 저장을 호출할 수 있는 public 메서드
        public void SaveProductCardsToJsonPublic()
        {
            SaveProductCardsToJson();
        }

        // ⭐ 외부에서 JSON 로드를 호출할 수 있는 public 메서드
        public void LoadProductCardsFromJsonPublic()
        {
            LogWindow.AddLogStatic("📂 상품데이터 페이지 진입 - 저장된 상품 데이터 로드 중...");
            LoadProductCardsFromJson();
        }

        // 📊 Excel 내보내기 버튼 클릭 이벤트
        protected async void ExportExcelButton_Click(object? sender, RoutedEventArgs e)
        {
            try
            {
                var mainWindow = (MainWindow?)TopLevel.GetTopLevel(this);
                
                // ⭐ 선택된 상품 ID 가져오기 (UI에서)
                var selectedProductIds = _productElements.Values
                    .Where(p => p.CheckBox?.IsChecked == true)
                    .Select(p => $"{p.StoreId}_{p.RealProductId}")
                    .ToHashSet();
                
                if (selectedProductIds.Count == 0)
                {
                    LogWindow.AddLogStatic("⚠️ 선택된 상품이 없습니다. 내보낼 상품을 선택해주세요.");
                    await ShowMessageBox(mainWindow, "선택된 상품이 없습니다.\n내보낼 상품을 선택해주세요.");
                    return;
                }
                
                // ⭐ JSON 파일에서 선택된 상품 데이터 가져오기
                var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                var jsonFilePath = System.IO.Path.Combine(appDataPath, "Predvia", "product_cards.json");
                
                if (!File.Exists(jsonFilePath))
                {
                    LogWindow.AddLogStatic("❌ 저장된 상품 데이터가 없습니다.");
                    await ShowMessageBox(mainWindow, "저장된 상품 데이터가 없습니다.\n먼저 저장해주세요.");
                    return;
                }
                
                var json = File.ReadAllText(jsonFilePath);
                var allCards = JsonSerializer.Deserialize<List<ProductCardData>>(json) ?? new List<ProductCardData>();
                
                // ⭐ 선택된 상품만 필터링 (JSON 기준)
                var selectedCards = allCards
                    .Where(c => selectedProductIds.Contains($"{c.StoreId}_{c.RealProductId}"))
                    .ToList();
                
                // ⭐ UI에서 최신 상품명 가져와서 반영
                foreach (var card in selectedCards)
                {
                    var key = $"{card.StoreId}_{card.RealProductId}";
                    var uiElement = _productElements.Values.FirstOrDefault(p => $"{p.StoreId}_{p.RealProductId}" == key);
                    if (uiElement?.NameInputBox != null)
                    {
                        card.ProductName = uiElement.NameInputBox.Text ?? "";
                    }
                }
                
                // ⭐ 타오바오 페어링 안 된 상품 체크 (JSON 기준)
                var notPairedCards = selectedCards
                    .Where(c => c.TaobaoProducts == null || c.TaobaoProducts.Count == 0)
                    .ToList();
                
                if (notPairedCards.Count > 0)
                {
                    LogWindow.AddLogStatic($"⚠️ 선택된 {selectedCards.Count}개 중 타오바오 페어링 안 된 상품: {notPairedCards.Count}개");
                    await ShowMessageBox(mainWindow, $"선택된 {selectedCards.Count}개 상품 중\n타오바오 페어링이 안 된 상품이 {notPairedCards.Count}개 있습니다.\n먼저 페어링을 진행해주세요.");
                    return;
                }

                // ⭐ 엑셀 다운로드 API 호출 (관리자는 건너뛰기)
                if (!AuthManager.Instance.IsAdmin)
                {
                    int downloadCount = selectedCards.Count;
                    using var httpClient = new HttpClient();
                    
                    string apiUrl = $"http://13.209.199.124:8080/api/excel/request-download?apiKey={AuthManager.Instance.Token}&count={downloadCount}";
                    var apiResponse = await httpClient.PostAsync(apiUrl, null);
                    string apiJson = await apiResponse.Content.ReadAsStringAsync();
                    var apiDoc = JsonDocument.Parse(apiJson);
                    
                    bool success = apiDoc.RootElement.GetProperty("success").GetBoolean();
                    if (!success)
                    {
                        string message = apiDoc.RootElement.GetProperty("message").GetString() ?? "다운로드 권한이 없습니다.";
                        LogWindow.AddLogStatic($"❌ 엑셀 다운로드 실패: {message}");
                        await ShowMessageBox(mainWindow, message);
                        
                        if (mainWindow != null)
                            await mainWindow.RefreshExcelDownloadCount();
                        return;
                    }
                    
                    LogWindow.AddLogStatic($"✅ 엑셀 다운로드 권한 확인 완료 ({downloadCount}개 차감)");
                };

                LogWindow.AddLogStatic($"📊 Excel 내보내기 시작... (선택된 상품: {selectedCards.Count}개)");
                
                // 현재 날짜+시간으로 파일명 자동 생성
                var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HHmmss");
                var defaultFileName = $"{timestamp}_결과물추출.xlsx";
                
                var saveDialog = new SaveFileDialog
                {
                    Title = "Excel 파일 저장",
                    InitialFileName = defaultFileName,
                    DefaultExtension = "xlsx",
                    Filters = new List<FileDialogFilter>
                    {
                        new FileDialogFilter { Name = "Excel 파일", Extensions = new List<string> { "xlsx" } }
                    }
                };

                if (mainWindow == null)
                {
                    LogWindow.AddLogStatic("❌ MainWindow를 찾을 수 없습니다.");
                    return;
                }

                var result = await saveDialog.ShowAsync(mainWindow);
                if (string.IsNullOrEmpty(result))
                {
                    LogWindow.AddLogStatic("⚠️ 파일 저장 취소됨");
                    return;
                }

                await ExportToExcelFromJson(result, selectedCards);
                
                // ⭐ 내보내기 완료 후 선택된 상품 삭제
                await DeleteExportedProductsFromJson(selectedCards, selectedProductIds);
                
                // ⭐ 완료 메시지 박스 표시
                await ShowMessageBox(mainWindow, $"Excel 내보내기 완료!\n{selectedCards.Count}개 상품이 저장되었습니다.");
                LogWindow.AddLogStatic($"✅ Excel 파일 저장 완료: {result}");
                
                // ⭐ MainWindow의 횟수 갱신
                if (mainWindow != null)
                    await mainWindow.RefreshExcelDownloadCount();
            }
            catch (Exception ex)
            {
                LogWindow.AddLogStatic($"❌ Excel 내보내기 실패: {ex.Message}");
                LogWindow.AddLogStatic($"스택: {ex.StackTrace}");
            }
        }
        
        // ⭐ 내보낸 상품 삭제 (JSON 기반)
        private async Task DeleteExportedProductsFromJson(List<ProductCardData> cards, HashSet<string> selectedProductIds)
        {
            try
            {
                LogWindow.AddLogStatic($"🗑️ 내보낸 {cards.Count}개 상품 삭제 중...");
                
                var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                var predviaPath = System.IO.Path.Combine(appDataPath, "Predvia");
                
                foreach (var card in cards)
                {
                    var storeId = card.StoreId;
                    var productId = card.RealProductId;
                    
                    // 이미지 파일 삭제
                    var imagePath = System.IO.Path.Combine(predviaPath, "Images", $"{storeId}_{productId}_main.jpg");
                    if (File.Exists(imagePath)) File.Delete(imagePath);
                    
                    // 상품명 파일 삭제
                    var namePath = System.IO.Path.Combine(predviaPath, "ProductData", $"{storeId}_{productId}_name.txt");
                    if (File.Exists(namePath)) File.Delete(namePath);
                    
                    // 리뷰 파일 삭제
                    var reviewPath = System.IO.Path.Combine(predviaPath, "Reviews", $"{storeId}_{productId}_reviews.json");
                    if (File.Exists(reviewPath)) File.Delete(reviewPath);
                    
                    // 카테고리 파일 삭제
                    var categoryPath = System.IO.Path.Combine(predviaPath, "Categories", $"{storeId}_{productId}_categories.json");
                    if (File.Exists(categoryPath)) File.Delete(categoryPath);
                }
                
                // ⭐ JSON 파일 업데이트 (내보낸 상품 제외)
                var jsonFilePath = System.IO.Path.Combine(predviaPath, "product_cards.json");
                var json = File.ReadAllText(jsonFilePath);
                var allCards = JsonSerializer.Deserialize<List<ProductCardData>>(json) ?? new List<ProductCardData>();
                var remainingCards = allCards.Where(c => !selectedProductIds.Contains($"{c.StoreId}_{c.RealProductId}")).ToList();
                
                var options = new JsonSerializerOptions { WriteIndented = true };
                File.WriteAllText(jsonFilePath, JsonSerializer.Serialize(remainingCards, options));
                
                // ⭐ UI에서도 삭제
                var container = this.FindControl<StackPanel>("RealDataContainer");
                var toRemove = _productElements.Values
                    .Where(p => selectedProductIds.Contains($"{p.StoreId}_{p.RealProductId}"))
                    .ToList();
                
                foreach (var product in toRemove)
                {
                    if (product.Container != null)
                        container?.Children.Remove(product.Container);
                    _productElements.Remove(product.ProductId);
                }
                
                LogWindow.AddLogStatic($"✅ {cards.Count}개 상품 삭제 완료");
            }
            catch (Exception ex)
            {
                LogWindow.AddLogStatic($"❌ 상품 삭제 오류: {ex.Message}");
            }
        }
        
        // 📊 Excel 파일 생성 메서드 (JSON 기반)
        private async Task ExportToExcelFromJson(string filePath, List<ProductCardData> selectedCards)
        {
            LogWindow.AddLogStatic($"📊 {selectedCards.Count}개 상품 Excel 내보내기 중...");

            await Task.Run(() =>
            {
                using var workbook = new XLWorkbook();
                var worksheet = workbook.Worksheets.Add("상품수집");

                // 헤더 행 (1행)
                worksheet.Cell(1, 1).Value = "카테고리";
                worksheet.Cell(1, 2).Value = "상품명";
                worksheet.Cell(1, 3).Value = "글자수(Byte)";
                worksheet.Cell(1, 4).Value = "배대지 비용";
                worksheet.Cell(1, 5).Value = "수집링크";
                worksheet.Cell(1, 6).Value = "보스 메시지";
                worksheet.Cell(1, 7).Value = "메모 글자수";
                worksheet.Cell(1, 8).Value = "주의사항";

                // 2행 양식 설명
                for (int col = 1; col <= 8; col++)
                {
                    worksheet.Cell(2, col).Value = "양식맞춤2줄";
                }

                // 데이터 행 (3행부터)
                int row = 3;
                
                foreach (var card in selectedCards)
                {
                    // 선택된 타오바오 상품 가져오기
                    var selectedIndex = Math.Min(card.SelectedTaobaoIndex, card.TaobaoProducts!.Count - 1);
                    selectedIndex = Math.Max(0, selectedIndex);
                    var selectedTaobao = card.TaobaoProducts[selectedIndex];
                    
                    var taobaoUrl = !string.IsNullOrEmpty(selectedTaobao.ProductUrl) 
                        ? selectedTaobao.ProductUrl 
                        : $"https://item.taobao.com/item.htm?id={selectedTaobao.Nid}";
                    
                    // ⭐ 모바일 URL을 PC URL로 변환
                    taobaoUrl = ConvertTaobaoMobileToPC(taobaoUrl);
                    
                    var categoryInfo = GetCategoryInfo(card.StoreId, card.RealProductId);
                    var productName = card.ProductName ?? "";
                    var byteCount = CalculateByteCount(productName);

                    worksheet.Cell(row, 1).Value = categoryInfo;
                    worksheet.Cell(row, 2).Value = productName;
                    worksheet.Cell(row, 3).Value = byteCount;
                    worksheet.Cell(row, 4).Value = card.ShippingCost;
                    worksheet.Cell(row, 5).Value = taobaoUrl;
                    worksheet.Cell(row, 6).Value = "";
                    worksheet.Cell(row, 7).Value = 0;
                    worksheet.Cell(row, 8).Value = "";

                    row++;
                }

                workbook.SaveAs(filePath);
            });
        }
        
        // ⭐ 타오바오 모바일 URL을 PC URL로 변환
        private string ConvertTaobaoMobileToPC(string url)
        {
            if (string.IsNullOrEmpty(url)) return url;
            
            // 모바일 URL 패턴: http://a.m.taobao.com/i700410428401.htm
            var mobileMatch = System.Text.RegularExpressions.Regex.Match(url, @"m\.taobao\.com/i(\d+)");
            if (mobileMatch.Success)
            {
                var itemId = mobileMatch.Groups[1].Value;
                return $"https://item.taobao.com/item.htm?id={itemId}";
            }
            
            // 이미 PC URL이면 그대로 반환
            return url;
        }
        
        // ⭐ 메시지 박스 표시
        private async Task ShowMessageBox(Window? parent, string message)
        {
            var msgBox = new Window
            {
                Title = "알림",
                Width = 350,
                Height = 150,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                CanResize = false
            };
            
            var panel = new StackPanel
            {
                Margin = new Thickness(20),
                Spacing = 20,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
            };
            
            panel.Children.Add(new TextBlock
            {
                Text = message,
                TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center
            });
            
            var okButton = new Button
            {
                Content = "확인",
                Width = 80,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                Background = new SolidColorBrush(Color.Parse("#E67E22")),
                Foreground = Brushes.White
            };
            okButton.Click += (s, e) => msgBox.Close();
            panel.Children.Add(okButton);
            
            msgBox.Content = panel;
            
            if (parent != null)
                await msgBox.ShowDialog(parent);
            else
                msgBox.Show();
        }

        // 상품명 파일에서 가져오기
        private string GetProductNameFromFile(string? storeId, string? productId)
        {
            if (string.IsNullOrEmpty(storeId) || string.IsNullOrEmpty(productId))
                return "";

            try
            {
                var predviaPath = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "Predvia"
                );
                var namePath = System.IO.Path.Combine(predviaPath, "ProductData", $"{storeId}_{productId}_name.txt");
                
                if (File.Exists(namePath))
                {
                    return File.ReadAllText(namePath, Encoding.UTF8).Trim();
                }
            }
            catch { }

            return "";
        }

        // 타오바오 상품 정보 파일에서 가져오기
        private List<TaobaoProductData> GetTaobaoProductsFromFile(string? storeId, string? productId)
        {
            if (string.IsNullOrEmpty(storeId) || string.IsNullOrEmpty(productId))
                return new List<TaobaoProductData>();

            try
            {
                var predviaPath = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "Predvia"
                );
                var taobaoPath = System.IO.Path.Combine(predviaPath, "TaobaoProducts", $"{storeId}_{productId}_taobao.json");
                
                if (File.Exists(taobaoPath))
                {
                    var json = File.ReadAllText(taobaoPath, Encoding.UTF8);
                    var products = JsonSerializer.Deserialize<List<TaobaoProductData>>(json);
                    return products ?? new List<TaobaoProductData>();
                }
            }
            catch { }

            return new List<TaobaoProductData>();
        }
    }

    // ⭐ JSON 직렬화용 데이터 클래스
    public class ProductCardData
    {
        [JsonPropertyName("productId")]
        public int ProductId { get; set; }

        [JsonPropertyName("storeId")]
        public string? StoreId { get; set; }

        [JsonPropertyName("realProductId")]
        public string? RealProductId { get; set; }

        [JsonPropertyName("imageUrl")]
        public string? ImageUrl { get; set; }

        [JsonPropertyName("productName")]
        public string? ProductName { get; set; }

        [JsonPropertyName("productNameKeywords")]
        public List<string> ProductNameKeywords { get; set; } = new();

        [JsonPropertyName("selectedKeywords")]
        public List<string> SelectedKeywords { get; set; } = new();

        [JsonPropertyName("isTaobaoPaired")]
        public bool IsTaobaoPaired { get; set; }

        [JsonPropertyName("taobaoProducts")]
        public List<TaobaoProductData> TaobaoProducts { get; set; } = new();
        
        [JsonPropertyName("shippingCost")]
        public int ShippingCost { get; set; } = 0; // 배대지 비용
        
        [JsonPropertyName("selectedTaobaoIndex")]
        public int SelectedTaobaoIndex { get; set; } = 0; // 선택된 타오바오 상품 인덱스
    }

    // 상품별 UI 요소들을 관리하는 클래스
    public class ProductUIElements
    {
        public int ProductId { get; set; }
        public string? StoreId { get; set; } // 실제 스토어 ID
        public string? RealProductId { get; set; } // 실제 상품 ID
        public string? ImagePath { get; set; } // 실제 이미지 파일 경로
        public StackPanel? Container { get; set; } // 상품 카드 컨테이너
        public CheckBox? CheckBox { get; set; }
        public TextBlock? CategoryTextBlock { get; set; } // ⭐ 카테고리 텍스트블록
        public Ellipse? CategoryStatusIndicator { get; set; }
        public Ellipse? NameStatusIndicator { get; set; }
        public WrapPanel? NameKeywordPanel { get; set; }
        public TextBox? NameInputBox { get; set; } // 상품명 입력박스 추가
        public TextBlock? ByteCountTextBlock { get; set; }
        public WrapPanel? KeywordPanel { get; set; }
        public TextBox? KeywordInputBox { get; set; }
        public TextBox? ShippingCostInput { get; set; } // ⭐ 배대지 비용 입력박스
        public Button? AddKeywordButton { get; set; }
        public Button? DeleteButton { get; set; }
        public Button? HoldButton { get; set; }
        public Ellipse? TaobaoPairingStatusIndicator { get; set; }
        public Button? TaobaoPairingButton { get; set; }
        public StackPanel? TaobaoProductsPanel { get; set; } // ⭐ 타오바오 상품 표시 패널
        public List<string> ProductNameKeywords { get; set; } = new List<string>();
        public List<string> SelectedKeywords { get; set; } = new List<string>();
        public bool IsTaobaoPaired { get; set; } = false;
        public List<TaobaoProductData> TaobaoProducts { get; set; } = new(); // ⭐ 타오바오 상품 데이터 저장
        public int SelectedTaobaoIndex { get; set; } = 0; // ⭐ 선택된 타오바오 상품 인덱스 (기본 0번)
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

// ⭐ 타오바오 상품 데이터 모델
public class TaobaoProductData
{
    [JsonPropertyName("nid")]
    public string Nid { get; set; } = string.Empty;

    [JsonPropertyName("imageUrl")]
    public string ImageUrl { get; set; } = string.Empty;

    [JsonPropertyName("price")]
    public string Price { get; set; } = string.Empty;

    [JsonPropertyName("sales")]
    public string Sales { get; set; } = string.Empty;

    [JsonPropertyName("reviews")]
    public string Reviews { get; set; } = string.Empty;

    [JsonPropertyName("productUrl")]
    public string ProductUrl { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;
}


