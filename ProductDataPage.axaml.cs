using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Gumaedaehang
{
    public partial class ProductDataPage : SourcingPage
    {
        public ProductDataPage() : base()
        {
            InitializeComponent();

            // ⭐ 페이지 로드 시 JSON 데이터 자동 로드
            this.Loaded += (s, e) =>
            {
                LogWindow.AddLogStatic("📂 상품데이터 페이지 로드 - 저장된 데이터 불러오는 중...");
                LoadProductCardsFromJsonPublic();
            };
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
