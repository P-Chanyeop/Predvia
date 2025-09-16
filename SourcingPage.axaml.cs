using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Input;
using Avalonia.Platform;
using Avalonia.Threading;
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
    public partial class SourcingPage : UserControl
    {
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
                _autoSourcingTextBox = this.FindControl<TextBox>("AutoSourcingTextBox");
                _autoSourcingButton = this.FindControl<Button>("AutoSourcingButton");
                _mainProductTextBox = this.FindControl<TextBox>("MainProductTextBox");
                _mainProductButton = this.FindControl<Button>("MainProductButton");
                
                // 상품들의 UI 요소들 초기화
                InitializeProductElements();
                
                // 이벤트 핸들러 등록
                RegisterEventHandlers();
                
                // 초기 상태 설정
                UpdateViewVisibility();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SourcingPage 초기화 중 오류: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"스택 트레이스: {ex.StackTrace}");
                throw; // 예외를 다시 던져서 상위에서 처리하도록 함
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
        private void InitializeProductElements()
        {
            var product1 = new ProductUIElements
            {
                ProductId = 1,
                CheckBox = this.FindControl<CheckBox>("Product1CheckBox"),
                CategoryStatusIndicator = this.FindControl<Ellipse>("Product1CategoryStatusIndicator"),
                NameStatusIndicator = this.FindControl<Ellipse>("Product1NameStatusIndicator"),
                NameKeywordPanel = this.FindControl<WrapPanel>("Product1NameKeywordPanel"),
                ByteCountTextBlock = this.FindControl<TextBlock>("Product1ByteCountTextBlock"),
                KeywordPanel = this.FindControl<WrapPanel>("Product1KeywordPanel"),
                KeywordInputBox = this.FindControl<TextBox>("Product1KeywordInputBox"),
                AddKeywordButton = this.FindControl<Button>("Product1AddKeywordButton"),
                DeleteButton = this.FindControl<Button>("Product1DeleteButton"),
                HoldButton = this.FindControl<Button>("Product1HoldButton"),
                TaobaoPairingStatusIndicator = this.FindControl<Ellipse>("Product1TaobaoPairingStatusIndicator"),
                TaobaoPairingButton = this.FindControl<Button>("Product1TaobaoPairingButton"),
                ProductNameKeywords = new List<string> { "카페드345", "바나나" },
                SelectedKeywords = new List<string> { "카페드345", "바나나" },
                IsTaobaoPaired = false
            };
            
            var product2 = new ProductUIElements
            {
                ProductId = 2,
                CheckBox = this.FindControl<CheckBox>("Product2CheckBox"),
                CategoryStatusIndicator = this.FindControl<Ellipse>("Product2CategoryStatusIndicator"),
                NameStatusIndicator = this.FindControl<Ellipse>("Product2NameStatusIndicator"),
                NameKeywordPanel = this.FindControl<WrapPanel>("Product2NameKeywordPanel"),
                ByteCountTextBlock = this.FindControl<TextBlock>("Product2ByteCountTextBlock"),
                KeywordPanel = this.FindControl<WrapPanel>("Product2KeywordPanel"),
                KeywordInputBox = this.FindControl<TextBox>("Product2KeywordInputBox"),
                AddKeywordButton = this.FindControl<Button>("Product2AddKeywordButton"),
                DeleteButton = this.FindControl<Button>("Product2DeleteButton"),
                HoldButton = this.FindControl<Button>("Product2HoldButton"),
                TaobaoPairingStatusIndicator = this.FindControl<Ellipse>("Product2TaobaoPairingStatusIndicator"),
                TaobaoPairingButton = this.FindControl<Button>("Product2TaobaoPairingButton"),
                ProductNameKeywords = new List<string> { "스테인리스", "주방용품세트", "고급형" },
                SelectedKeywords = new List<string> { "스테인리스", "주방용품세트" },
                IsTaobaoPaired = false
            };
            
            var product3 = new ProductUIElements
            {
                ProductId = 3,
                CheckBox = this.FindControl<CheckBox>("Product3CheckBox"),
                CategoryStatusIndicator = this.FindControl<Ellipse>("Product3CategoryStatusIndicator"),
                NameStatusIndicator = this.FindControl<Ellipse>("Product3NameStatusIndicator"),
                NameKeywordPanel = this.FindControl<WrapPanel>("Product3NameKeywordPanel"),
                ByteCountTextBlock = this.FindControl<TextBlock>("Product3ByteCountTextBlock"),
                KeywordPanel = this.FindControl<WrapPanel>("Product3KeywordPanel"),
                KeywordInputBox = this.FindControl<TextBox>("Product3KeywordInputBox"),
                AddKeywordButton = this.FindControl<Button>("Product3AddKeywordButton"),
                DeleteButton = this.FindControl<Button>("Product3DeleteButton"),
                HoldButton = this.FindControl<Button>("Product3HoldButton"),
                TaobaoPairingStatusIndicator = this.FindControl<Ellipse>("Product3TaobaoPairingStatusIndicator"),
                TaobaoPairingButton = this.FindControl<Button>("Product3TaobaoPairingButton"),
                ProductNameKeywords = new List<string> { "티셔츠", "면소재" },
                SelectedKeywords = new List<string> { "티셔츠", "면소재" },
                IsTaobaoPaired = false
            };
            
            _productElements[1] = product1;
            _productElements[2] = product2;
            _productElements[3] = product3;
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
            _hasData = !_hasData;
            UpdateViewVisibility();
            Debug.WriteLine($"데이터 상태 변경: {(_hasData ? "데이터 있음" : "데이터 없음")}");
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
        
        // 메인상품 자동찾기 페어링 버튼 클릭
        private async void MainProductButton_Click(object? sender, RoutedEventArgs e)
        {
            await HandlePairingButtonClick(_mainProductTextBox, _mainProductButton, "메인상품");
        }
        
        // 페어링 버튼 공통 처리 메서드
        private async Task HandlePairingButtonClick(TextBox? textBox, Button? button, string type)
        {
            if (textBox == null || button == null) return;
            
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
                
                _naverService ??= new NaverSmartStoreService();
                await _naverService.OpenNaverSmartStoreWithKeyword(searchText);
                
                button.Content = "연결 완료";
                await Task.Delay(1500);
            }
            catch (Exception ex)
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
