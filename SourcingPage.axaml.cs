using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Input;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System;
using System.Text;

namespace Gumaedaehang
{
    public partial class SourcingPage : UserControl
    {
        private Grid? _noDataView;
        private Grid? _dataAvailableView;
        private TextBlock? _addMoreLink;
        private Button? _testDataButton;
        private Button? _testDataButton2;
        private TextBox? _keywordInputBox;
        private Button? _addKeywordButton;
        private WrapPanel? _productNameKeywordPanel;
        private WrapPanel? _keywordPanel1;
        private TextBlock? _byteCountTextBlock;
        private CheckBox? _selectAllCheckBox;
        private CheckBox? _product1CheckBox;
        private Ellipse? _productNameStatusIndicator;
        private Ellipse? _taobaoPairingStatusIndicator;
        private Button? _taobaoPairingButton;
        private bool _hasData = false;
        private bool _isTaobaoPaired = false;
        
        // 상품명 키워드 목록
        private List<string> _productNameKeywords = new List<string>();
        
        // 키워드 데이터
        private List<string> _selectedKeywords1 = new List<string>();
        private List<string> _availableKeywords1 = new List<string> 
        { 
            "카페드345", "카페드553422", "카페드1", "바나나", "시럽", "사탕", "아이스크림" 
        };
        
        public SourcingPage()
        {
            InitializeComponent();
            
            // UI 요소 참조 가져오기
            _noDataView = this.FindControl<Grid>("NoDataView");
            _dataAvailableView = this.FindControl<Grid>("DataAvailableView");
            _addMoreLink = this.FindControl<TextBlock>("AddMoreLink");
            _testDataButton = this.FindControl<Button>("TestDataButton");
            _testDataButton2 = this.FindControl<Button>("TestDataButton2");
            _keywordInputBox = this.FindControl<TextBox>("KeywordInputBox");
            _addKeywordButton = this.FindControl<Button>("AddKeywordButton");
            _productNameKeywordPanel = this.FindControl<WrapPanel>("ProductNameKeywordPanel");
            _keywordPanel1 = this.FindControl<WrapPanel>("KeywordPanel1");
            _selectAllCheckBox = this.FindControl<CheckBox>("SelectAllCheckBox");
            _product1CheckBox = this.FindControl<CheckBox>("Product1CheckBox");
            _productNameStatusIndicator = this.FindControl<Ellipse>("ProductNameStatusIndicator");
            _taobaoPairingStatusIndicator = this.FindControl<Ellipse>("TaobaoPairingStatusIndicator");
            _taobaoPairingButton = this.FindControl<Button>("TaobaoPairingButton");
            
            // 바이트 수 표시 TextBlock 찾기 (상품명 옆의 "0/50 byte" 텍스트)
            _byteCountTextBlock = this.FindControl<TextBlock>("ByteCountTextBlock");
            
            // 이벤트 핸들러 등록
            if (_addMoreLink != null)
                _addMoreLink.PointerPressed += AddMoreLink_Click;
                
            if (_testDataButton != null)
                _testDataButton.Click += TestDataButton_Click;
                
            if (_testDataButton2 != null)
                _testDataButton2.Click += TestDataButton_Click;
                
            if (_addKeywordButton != null)
                _addKeywordButton.Click += AddKeywordButton_Click;
                
            if (_keywordInputBox != null)
                _keywordInputBox.KeyDown += KeywordInputBox_KeyDown;
                
            if (_selectAllCheckBox != null)
            {
                _selectAllCheckBox.Checked += SelectAllCheckBox_Changed;
                _selectAllCheckBox.Unchecked += SelectAllCheckBox_Changed;
            }
                
            if (_product1CheckBox != null)
            {
                _product1CheckBox.Checked += ProductCheckBox_Changed;
                _product1CheckBox.Unchecked += ProductCheckBox_Changed;
            }
                
            if (_taobaoPairingButton != null)
                _taobaoPairingButton.Click += TaobaoPairingButton_Click;
                
            // 키워드 클릭 이벤트 등록
            RegisterKeywordEvents();
            
            // 초기 키워드 설정
            _productNameKeywords.AddRange(new[] { "카페드345", "카페드553422" });
            _selectedKeywords1.AddRange(new[] { "카페드345", "카페드553422" });
            UpdateProductNameKeywordDisplay();
            UpdateKeywordDisplay();
                
            // 초기 상태 설정 (데이터 없음)
            UpdateViewVisibility();
            UpdateKeywordDisplay();
            UpdateStatusIndicators();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
        
        // 전체 선택 체크박스 변경 이벤트
        private void SelectAllCheckBox_Changed(object? sender, RoutedEventArgs e)
        {
            if (_selectAllCheckBox != null && _product1CheckBox != null)
            {
                bool isChecked = _selectAllCheckBox.IsChecked ?? false;
                _product1CheckBox.IsChecked = isChecked;
            }
        }
        
        // 개별 상품 체크박스 변경 이벤트
        private void ProductCheckBox_Changed(object? sender, RoutedEventArgs e)
        {
            // 개별 체크박스 상태에 따라 전체 선택 체크박스 상태 업데이트
            if (_selectAllCheckBox != null && _product1CheckBox != null)
            {
                bool allChecked = _product1CheckBox.IsChecked ?? false;
                // 여러 상품이 있을 경우 모든 상품의 체크 상태를 확인해야 함
                _selectAllCheckBox.IsChecked = allChecked;
            }
        }
        
        // 타오바오 페어링 버튼 클릭 이벤트
        private void TaobaoPairingButton_Click(object? sender, RoutedEventArgs e)
        {
            _isTaobaoPaired = true;
            UpdateStatusIndicators();
            Debug.WriteLine("타오바오 페어링 완료");
        }
        
        // 상태 표시등 업데이트
        private void UpdateStatusIndicators()
        {
            // 상품명 바이트 수 표시등 업데이트
            if (_productNameStatusIndicator != null)
            {
                var totalByteCount = 0;
                foreach (var keyword in _productNameKeywords)
                {
                    totalByteCount += CalculateByteCount(keyword);
                }
                
                if (totalByteCount <= 50)
                {
                    _productNameStatusIndicator.Fill = new SolidColorBrush(Color.Parse("#53DA4C"));
                }
                else
                {
                    _productNameStatusIndicator.Fill = new SolidColorBrush(Color.Parse("#FF7272"));
                }
            }
            
            // 타오바오 페어링 상태 표시등 업데이트
            if (_taobaoPairingStatusIndicator != null)
            {
                if (_isTaobaoPaired)
                {
                    _taobaoPairingStatusIndicator.Fill = new SolidColorBrush(Color.Parse("#53DA4C"));
                }
                else
                {
                    _taobaoPairingStatusIndicator.Fill = new SolidColorBrush(Color.Parse("#FF7272"));
                }
            }
        }
        
        // 키워드 클릭 이벤트 등록
        private void RegisterKeywordEvents()
        {
            for (int i = 1; i <= 11; i++)
            {
                var keyword = this.FindControl<Border>($"Keyword1_{i}");
                if (keyword != null)
                {
                    keyword.PointerPressed += (sender, e) => KeywordBorder_Click(sender, e);
                }
            }
        }
        
        // 키워드 클릭 이벤트 핸들러
        private void KeywordBorder_Click(object? sender, Avalonia.Input.PointerPressedEventArgs e)
        {
            if (sender is Border border && border.Child is TextBlock textBlock)
            {
                var keywordText = textBlock.Text;
                if (keywordText != null)
                {
                    if (_selectedKeywords1.Contains(keywordText))
                    {
                        // 이미 선택된 키워드면 제거 (상품명에서도 제거)
                        _selectedKeywords1.Remove(keywordText);
                        _productNameKeywords.Remove(keywordText);
                        UpdateProductNameKeywordDisplay();
                    }
                    else
                    {
                        // 선택되지 않은 키워드면 추가 (상품명에도 추가)
                        _selectedKeywords1.Add(keywordText);
                        if (!_productNameKeywords.Contains(keywordText))
                        {
                            _productNameKeywords.Add(keywordText);
                            UpdateProductNameKeywordDisplay();
                        }
                    }
                    
                    UpdateKeywordDisplay();
                }
            }
        }
        
        // 키워드 추가 버튼 클릭 이벤트
        private void AddKeywordButton_Click(object? sender, RoutedEventArgs e)
        {
            AddKeywordFromInput();
        }
        
        // 키워드 입력창 키 이벤트 (엔터키 처리)
        private void KeywordInputBox_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                AddKeywordFromInput();
                e.Handled = true;
            }
        }
        
        // 입력창에서 키워드 추가
        private void AddKeywordFromInput()
        {
            if (_keywordInputBox != null && !string.IsNullOrWhiteSpace(_keywordInputBox.Text))
            {
                var keyword = _keywordInputBox.Text.Trim();
                if (!string.IsNullOrEmpty(keyword) && !_productNameKeywords.Contains(keyword))
                {
                    _productNameKeywords.Add(keyword);
                    _selectedKeywords1.Add(keyword);
                    UpdateProductNameKeywordDisplay();
                    UpdateKeywordDisplay();
                    _keywordInputBox.Text = ""; // 입력창 초기화
                }
            }
        }
        
        // 상품명 입력 키 이벤트 처리 (기존 메서드 - 더 이상 사용하지 않음)
        private void ProductNameInputBox_KeyDown(object? sender, KeyEventArgs e)
        {
            // 이 메서드는 더 이상 사용하지 않음 - 별도 입력창으로 대체됨
        }
        
        // 상품명 키워드 표시 업데이트
        private void UpdateProductNameKeywordDisplay()
        {
            if (_productNameKeywordPanel == null) return;
            
            _productNameKeywordPanel.Children.Clear();
            
            foreach (var keyword in _productNameKeywords)
            {
                var keywordTag = CreateKeywordTag(keyword, true);
                _productNameKeywordPanel.Children.Add(keywordTag);
            }
            
            // 바이트 수 업데이트
            UpdateByteCount();
            // 상태 표시등 업데이트
            UpdateStatusIndicators();
        }
        
        // 바이트 수 계산 및 업데이트
        private void UpdateByteCount()
        {
            if (_byteCountTextBlock == null) return;
            
            // 키워드 텍스트만 계산 (공백 제외, X 버튼 제외)
            var totalByteCount = 0;
            foreach (var keyword in _productNameKeywords)
            {
                totalByteCount += CalculateByteCount(keyword);
            }
            
            _byteCountTextBlock.Text = $"{totalByteCount}/50 byte";
            
            // 50바이트 초과 시 빨간색으로 표시
            if (totalByteCount > 50)
            {
                _byteCountTextBlock.Foreground = Brushes.Red;
            }
            else
            {
                _byteCountTextBlock.Foreground = Brushes.Gray;
            }
        }
        
        // 한글 2바이트, 영어 1바이트로 계산
        private int CalculateByteCount(string text)
        {
            int byteCount = 0;
            foreach (char c in text)
            {
                // 한글 범위 체크 (가-힣, ㄱ-ㅎ, ㅏ-ㅣ)
                if ((c >= 0xAC00 && c <= 0xD7AF) || // 완성형 한글
                    (c >= 0x3131 && c <= 0x318E) || // 자음, 모음
                    (c >= 0x1100 && c <= 0x11FF))   // 조합용 자모
                {
                    byteCount += 2; // 한글은 2바이트
                }
                else
                {
                    byteCount += 1; // 영어 및 기타 문자는 1바이트
                }
            }
            return byteCount;
        }
        
        // 키워드 태그 생성 (삭제 가능한 태그)
        private Border CreateKeywordTag(string keyword, bool isDeletable = false)
        {
            var border = new Border
            {
                Background = new SolidColorBrush(Color.Parse("#F47B20")),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(8, 4),
                Margin = new Thickness(2, 2),
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left
            };
            
            var stackPanel = new StackPanel
            {
                Orientation = Avalonia.Layout.Orientation.Horizontal,
                Spacing = 4,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center
            };
            
            var textBlock = new TextBlock
            {
                Text = keyword,
                FontSize = 11,
                Foreground = Brushes.White,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                TextAlignment = Avalonia.Media.TextAlignment.Center
            };
            
            stackPanel.Children.Add(textBlock);
            
            if (isDeletable)
            {
                var deleteButton = new Button
                {
                    Content = "×",
                    FontSize = 14,
                    FontWeight = FontWeight.Bold,
                    Foreground = Brushes.White,
                    Background = Brushes.Transparent,
                    BorderThickness = new Thickness(0),
                    Padding = new Thickness(0),
                    Margin = new Thickness(0),
                    Width = 16,
                    Height = 16,
                    MinWidth = 16,
                    MinHeight = 16,
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                    HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                    VerticalContentAlignment = Avalonia.Layout.VerticalAlignment.Center
                };
                
                deleteButton.Click += (s, e) => RemoveProductNameKeyword(keyword);
                stackPanel.Children.Add(deleteButton);
            }
            
            border.Child = stackPanel;
            return border;
        }
        
        // 상품명 키워드 삭제
        private void RemoveProductNameKeyword(string keyword)
        {
            _productNameKeywords.Remove(keyword);
            _selectedKeywords1.Remove(keyword); // 키워드 목록에서도 선택 해제
            UpdateProductNameKeywordDisplay(); // 이미 UpdateByteCount() 포함됨
            UpdateKeywordDisplay(); // 키워드 목록 색상도 업데이트
        }
        
        // 상품명 텍스트박스 변경 이벤트 (기존 메서드 수정)
        private void ProductNameTextBox_TextChanged(object? sender, RoutedEventArgs? e)
        {
            // 이 메서드는 더 이상 사용하지 않음 - 키워드 태그 시스템으로 대체됨
        }
        
        // 키워드 표시 업데이트
        private void UpdateKeywordDisplay()
        {
            for (int i = 1; i <= 11; i++)
            {
                var keyword = this.FindControl<Border>($"Keyword1_{i}");
                if (keyword != null && keyword.Child is TextBlock textBlock && textBlock.Text != null)
                {
                    if (_selectedKeywords1.Contains(textBlock.Text))
                    {
                        // 선택된 키워드는 회색으로
                        keyword.Background = new SolidColorBrush(Color.Parse("#D0D0D0"));
                        textBlock.Foreground = new SolidColorBrush(Colors.Gray);
                    }
                    else
                    {
                        // 선택되지 않은 키워드는 주황색으로
                        keyword.Background = new SolidColorBrush(Color.Parse("#F47B20"));
                        textBlock.Foreground = new SolidColorBrush(Colors.White);
                    }
                }
            }
        }
        
        // 추가하기+ 링크 클릭 이벤트 핸들러
        private void AddMoreLink_Click(object? sender, Avalonia.Input.PointerPressedEventArgs e)
        {
            // 실제 추가 로직 구현
            Debug.WriteLine("추가하기+ 링크 클릭됨");
        }
        
        // 테스트 버튼 클릭 이벤트 핸들러
        private void TestDataButton_Click(object? sender, RoutedEventArgs e)
        {
            // 데이터 상태 토글
            _hasData = !_hasData;
            UpdateViewVisibility();
            
            Debug.WriteLine($"데이터 상태 변경: {(_hasData ? "데이터 있음" : "데이터 없음")}");
        }
        
        // 데이터 유무에 따라 화면 업데이트
        private void UpdateViewVisibility()
        {
            if (_noDataView != null && _dataAvailableView != null)
            {
                _noDataView.IsVisible = !_hasData;
                _dataAvailableView.IsVisible = _hasData;
            }
        }
        
        // 외부에서 데이터 상태를 설정할 수 있는 메서드
        public void SetHasData(bool hasData)
        {
            _hasData = hasData;
            UpdateViewVisibility();
        }
        
        // 데이터 초기화 메서드 (테스트용)
        public void ResetData()
        {
            _hasData = false;
            _isTaobaoPaired = false;
            UpdateViewVisibility();
            UpdateStatusIndicators();
        }
    }
}
