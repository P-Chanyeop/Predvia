using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;

namespace Gumaedaehang
{
    public partial class ProductDataPage : SourcingPage
    {
        public ProductDataPage() : base()
        {
            InitializeComponent();

            // ⭐ UI 렌더링 후 JSON 데이터 로드
            Dispatcher.UIThread.Post(() =>
            {
                LogWindow.AddLogStatic("📂 상품데이터 페이지 로드 - 저장된 데이터 불러오는 중...");
                LoadProductCardsFromJsonPublic();
            }, DispatcherPriority.Background);
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
