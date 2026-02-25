using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using System;
using System.IO;
using System.Net.Http;

namespace Gumaedaehang
{
    public class LazyImage : Image
    {
        private static readonly HttpClient _http = new();
        private string? _imagePath;
        private bool _isLoaded = false;

        public string? ImagePath
        {
            get => _imagePath;
            set
            {
                _imagePath = value;
                _isLoaded = false;
                LoadImageIfVisible();
            }
        }

        protected override void OnAttachedToVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);
            LoadImageIfVisible();
        }

        private async void LoadImageIfVisible()
        {
            if (_isLoaded || string.IsNullOrEmpty(_imagePath)) return;
            _isLoaded = true;

            try
            {
                // URL이면 HTTP로 다운로드
                if (_imagePath.StartsWith("http"))
                {
                    var bytes = await _http.GetByteArrayAsync(_imagePath);
                    using var ms = new MemoryStream(bytes);
                    var bitmap = new Bitmap(ms);
                    await Dispatcher.UIThread.InvokeAsync(() => Source = bitmap);
                }
                else if (File.Exists(_imagePath))
                {
                    Source = new Bitmap(_imagePath);
                }
                else
                {
                    LogWindow.AddLogStatic($"⚠️ 이미지 파일 없음: {_imagePath}");
                }
            }
            catch (Exception ex)
            {
                LogWindow.AddLogStatic($"❌ 이미지 로드 실패: {ex.Message}");
            }
        }
    }
}
