using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using System;

namespace Gumaedaehang
{
    public partial class MainProductFinderPage : UserControl
    {
        public MainProductFinderPage()
        {
            InitializeComponent();
            
            // 초기 테마 적용
            Dispatcher.UIThread.Post(() =>
            {
                UpdateTheme();
            });
        }

        public void UpdateTheme()
        {
            try
            {
                var isDarkMode = ThemeManager.Instance.IsDarkTheme;
                
                if (isDarkMode)
                {
                    Classes.Add("dark");
                    this.Background = Avalonia.Media.Brushes.Transparent;
                }
                else
                {
                    Classes.Remove("dark");
                    this.Background = Avalonia.Media.Brushes.White;
                }

                // UI 강제 업데이트
                InvalidateVisual();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"테마 업데이트 오류: {ex.Message}");
            }
        }

        // 버튼 클릭 이벤트 핸들러들 (향후 구현)
        private void OnDetailPageButtonClick(object? sender, RoutedEventArgs e)
        {
            // 상세페이지 만들기 기능 구현 예정
            Console.WriteLine("상세페이지 만들기 (BETA) 클릭됨");
        }

        private void OnThumbnailButtonClick(object? sender, RoutedEventArgs e)
        {
            // 썸네일 만들기 기능 구현 예정
            Console.WriteLine("썸네일 만들기 (BETA) 클릭됨");
        }

        private void OnTaobaoLinkButtonClick(object? sender, RoutedEventArgs e)
        {
            // 타오바오 링크 재배어링 기능 구현 예정
            Console.WriteLine("타오바오 링크 재배어링 하기 클릭됨");
        }
    }
}
