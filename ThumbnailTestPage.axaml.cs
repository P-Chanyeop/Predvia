using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Gumaedaehang.Services;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace Gumaedaehang
{
    public partial class ThumbnailTestPage : UserControl
    {
        private readonly ThumbnailService _thumbnailService;
        private WrapPanel? _thumbnailContainer;

        public ThumbnailTestPage()
        {
            InitializeComponent();
            _thumbnailService = new ThumbnailService();
            
            // UI 요소 참조
            _thumbnailContainer = this.FindControl<WrapPanel>("ThumbnailContainer");
            
            // 페이지 로드 시 썸네일 표시
            _ = Task.Run(LoadThumbnailsAsync);
        }

        // 새로고침 버튼 클릭
        private async void RefreshButton_Click(object? sender, RoutedEventArgs e)
        {
            await LoadThumbnailsAsync();
        }

        // 폴더 열기 버튼 클릭
        private void OpenFolderButton_Click(object? sender, RoutedEventArgs e)
        {
            _thumbnailService.OpenThumbnailFolder();
        }

        // 썸네일 로드 및 표시
        private async Task LoadThumbnailsAsync()
        {
            try
            {
                var thumbnails = await _thumbnailService.LoadThumbnailInfoAsync();
                
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    _thumbnailContainer?.Children.Clear();
                    
                    foreach (var thumbnail in thumbnails)
                    {
                        if (File.Exists(thumbnail.LocalPath))
                        {
                            var card = CreateThumbnailCard(thumbnail);
                            _thumbnailContainer?.Children.Add(card);
                        }
                    }
                    
                    Debug.WriteLine($"📸 {thumbnails.Count}개 썸네일 표시됨");
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"썸네일 로드 오류: {ex.Message}");
            }
        }

        // 썸네일 카드 생성
        private Border CreateThumbnailCard(ThumbnailInfo thumbnail)
        {
            var card = new Border
            {
                Background = Avalonia.Media.Brushes.White,
                BorderBrush = Avalonia.Media.Brushes.LightGray,
                BorderThickness = new Avalonia.Thickness(1),
                CornerRadius = new Avalonia.CornerRadius(8),
                Margin = new Avalonia.Thickness(10),
                Padding = new Avalonia.Thickness(10),
                Width = 180,
                Height = 260
            };

            var stackPanel = new StackPanel
            {
                Spacing = 8
            };

            try
            {
                // 썸네일 이미지
                var bitmap = new Bitmap(thumbnail.LocalPath);
                var image = new Image
                {
                    Source = bitmap,
                    Width = 160,
                    Height = 120,
                    Stretch = Avalonia.Media.Stretch.UniformToFill
                };
                stackPanel.Children.Add(image);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"이미지 로드 오류: {ex.Message}");
                
                // 이미지 로드 실패 시 플레이스홀더
                var placeholder = new Border
                {
                    Background = Avalonia.Media.Brushes.LightGray,
                    Width = 160,
                    Height = 120,
                    Child = new TextBlock
                    {
                        Text = "🖼️",
                        FontSize = 32,
                        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                        VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
                    }
                };
                stackPanel.Children.Add(placeholder);
            }

            // 상품명
            var titleText = new TextBlock
            {
                Text = thumbnail.ProductTitle.Length > 30 ? 
                       thumbnail.ProductTitle.Substring(0, 30) + "..." : 
                       thumbnail.ProductTitle,
                FontSize = 12,
                FontWeight = Avalonia.Media.FontWeight.Bold,
                TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                MaxHeight = 40
            };
            stackPanel.Children.Add(titleText);

            // 다운로드 시간
            var timeText = new TextBlock
            {
                Text = thumbnail.DownloadedAt.ToString("MM/dd HH:mm"),
                FontSize = 10,
                Foreground = Avalonia.Media.Brushes.Gray
            };
            stackPanel.Children.Add(timeText);

            // 파일 경로 (디버그용)
            var pathText = new TextBlock
            {
                Text = System.IO.Path.GetFileName(thumbnail.LocalPath),
                FontSize = 9,
                Foreground = Avalonia.Media.Brushes.DarkGray,
                TextWrapping = Avalonia.Media.TextWrapping.Wrap
            };
            stackPanel.Children.Add(pathText);

            card.Child = stackPanel;
            return card;
        }
    }
}
