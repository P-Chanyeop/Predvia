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
                    _isLoaded = true;
                }
                else
                {
                    // 파일이 없으면 기본 이미지 (회색 배경)
                    LogWindow.AddLogStatic($"⚠️ 이미지 파일 없음: {_imagePath}");
                    _isLoaded = true;
                }
            }
            catch (Exception ex)
            {
                LogWindow.AddLogStatic($"❌ 이미지 로드 실패: {ex.Message}");
                _isLoaded = true;
            }
        }
    }
}
