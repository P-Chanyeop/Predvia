using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using System;
using System.IO;

namespace Gumaedaehang
{
    public class LazyImage : Image
    {
        private string? _imagePath;
        private bool _isLoaded = false;

        public string? ImagePath
        {
            get => _imagePath;
            set
            {
                _imagePath = value;
                LoadImageIfVisible();
            }
        }

        protected override void OnAttachedToVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);
            LoadImageIfVisible();
        }

        private void LoadImageIfVisible()
        {
            if (_isLoaded || string.IsNullOrEmpty(_imagePath)) return;

            try
            {
                if (File.Exists(_imagePath))
                {
                    var bitmap = new Bitmap(_imagePath);
                    Source = bitmap;
                }
                else
                {
                    // 기본 이미지 로드
                    Source = new Bitmap(AssetLoader.Open(new Uri("avares://Gumaedaehang/images/product1.png")));
                }
                _isLoaded = true;
            }
            catch
            {
                // 오류 시 기본 이미지
                try
                {
                    Source = new Bitmap(AssetLoader.Open(new Uri("avares://Gumaedaehang/images/product1.png")));
                }
                catch { }
                _isLoaded = true;
            }
        }
    }
}
