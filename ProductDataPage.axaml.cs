using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;

namespace Gumaedaehang
{
    public partial class ProductDataPage : SourcingPage
    {
        private CheckBox? _pdSelectAllCheckBox;
        
        public ProductDataPage() : base()
        {
            InitializeComponent();
            
            // 전체선택 체크박스 찾기 및 이벤트 연결
            _pdSelectAllCheckBox = this.FindControl<CheckBox>("SelectAllCheckBox");
            if (_pdSelectAllCheckBox != null)
            {
                _pdSelectAllCheckBox.Click += PDSelectAllCheckBox_Click;
            }

            // ⭐ UI 렌더링 후 JSON 데이터 로드
            Dispatcher.UIThread.Post(() =>
            {
                LogWindow.AddLogStatic("📂 상품데이터 페이지 로드 - 저장된 데이터 불러오는 중...");
                LoadProductCardsFromJsonPublic();
                
                // 로드 완료 후 이벤트 재연결
                if (_pdSelectAllCheckBox == null)
                {
                    _pdSelectAllCheckBox = this.FindControl<CheckBox>("SelectAllCheckBox");
                    if (_pdSelectAllCheckBox != null)
                    {
                        _pdSelectAllCheckBox.Click += PDSelectAllCheckBox_Click;
                    }
                }
            }, DispatcherPriority.Background);
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
        
        private void PDSelectAllCheckBox_Click(object? sender, RoutedEventArgs e)
        {
            LogWindow.AddLogStatic($"🔄 [상품데이터] 전체선택 클릭: {_pdSelectAllCheckBox?.IsChecked}");
            
            bool isChecked = _pdSelectAllCheckBox?.IsChecked ?? false;
            int count = 0;
            
            foreach (var kvp in _productElements)
            {
                if (kvp.Value.CheckBox != null)
                {
                    kvp.Value.CheckBox.IsChecked = isChecked;
                    count++;
                }
            }
            
            LogWindow.AddLogStatic($"✅ [상품데이터] {count}개 체크박스 {(isChecked ? "선택" : "해제")} 완료");
        }
    }
}
