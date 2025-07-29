using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Gumaedaehang
{
    public partial class MarketCheckPage : UserControl
    {
        public MarketCheckPage()
        {
            InitializeComponent();
        }

        private void OnMarketRegistrationClick(object? sender, RoutedEventArgs e)
        {
            // 마켓등록 페이지로 이동하는 로직
            // MainWindow의 인스턴스를 찾아서 마켓등록 페이지로 전환
            var mainWindow = TopLevel.GetTopLevel(this) as MainWindow;
            if (mainWindow != null)
            {
                // 마켓등록 페이지로 이동
                mainWindow.NavigateToMarketRegistration();
                System.Diagnostics.Debug.WriteLine("마켓등록 페이지로 이동했습니다.");
            }
        }
    }
}
