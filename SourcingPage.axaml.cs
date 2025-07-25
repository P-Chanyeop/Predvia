using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using System.Diagnostics;

namespace Gumaedaehang
{
    public partial class SourcingPage : UserControl
    {
        private Grid? _noDataView;
        private Grid? _dataAvailableView;
        private TextBlock? _addMoreLink;
        private Button? _testDataButton;
        private Button? _testDataButton2;
        private bool _hasData = false;
        
        public SourcingPage()
        {
            InitializeComponent();
            
            // UI 요소 참조 가져오기
            _noDataView = this.FindControl<Grid>("NoDataView");
            _dataAvailableView = this.FindControl<Grid>("DataAvailableView");
            _addMoreLink = this.FindControl<TextBlock>("AddMoreLink");
            _testDataButton = this.FindControl<Button>("TestDataButton");
            _testDataButton2 = this.FindControl<Button>("TestDataButton2");
            
            // 이벤트 핸들러 등록
            if (_addMoreLink != null)
                _addMoreLink.PointerPressed += AddMoreLink_Click;
                
            if (_testDataButton != null)
                _testDataButton.Click += TestDataButton_Click;
                
            if (_testDataButton2 != null)
                _testDataButton2.Click += TestDataButton_Click;
                
            // 초기 상태 설정 (데이터 없음)
            UpdateViewVisibility();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
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
            UpdateViewVisibility();
        }
    }
}
