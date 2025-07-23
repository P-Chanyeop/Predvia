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
        private Button? _searchButton;
        private Button? _noDataSearchButton;
        private bool _hasData = false;
        
        public SourcingPage()
        {
            InitializeComponent();
            
            // UI 요소 참조 가져오기
            _noDataView = this.FindControl<Grid>("NoDataView");
            _dataAvailableView = this.FindControl<Grid>("DataAvailableView");
            _searchButton = this.FindControl<Button>("SearchButton");
            _noDataSearchButton = this.FindControl<Button>("NoDataSearchButton");
            
            // 이벤트 핸들러 등록
            if (_searchButton != null)
                _searchButton.Click += SearchButton_Click;
                
            if (_noDataSearchButton != null)
                _noDataSearchButton.Click += NoDataSearchButton_Click;
                
            // 초기 상태 설정 (데이터 없음)
            UpdateViewVisibility();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
        
        // 검색 버튼 클릭 이벤트 핸들러
        private void SearchButton_Click(object? sender, RoutedEventArgs e)
        {
            // 실제 검색 로직 구현 (여기서는 데모로 데이터가 있는 상태로 전환)
            _hasData = true;
            UpdateViewVisibility();
        }
        
        // 데이터 없음 화면의 검색 버튼 클릭 이벤트 핸들러
        private void NoDataSearchButton_Click(object? sender, RoutedEventArgs e)
        {
            // 실제 검색 로직 구현 (여기서는 데모로 데이터가 있는 상태로 전환)
            _hasData = true;
            UpdateViewVisibility();
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
    }
}
