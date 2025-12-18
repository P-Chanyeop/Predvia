using System;
using System.Threading.Tasks;

namespace Gumaedaehang.Services
{
    public class NaverSmartStoreService
    {
        private readonly ChromeExtensionService _chromeExtensionService;
        private bool _isDisposed = false;

        public NaverSmartStoreService()
        {
            _chromeExtensionService = new ChromeExtensionService();
        }

        public async Task OpenNaverSmartStoreWithKeyword(string keyword)
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(NaverSmartStoreService));

            try
            {
                // ChromeExtensionService를 사용하여 앱 모드로 열기
                await _chromeExtensionService.SearchWithExtension(keyword);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"네이버 스마트스토어 열기 실패: {ex.Message}", ex);
            }
        }

        public void Close()
        {
            if (!_isDisposed)
            {
                _isDisposed = true;
            }
        }

        public void Dispose()
        {
            Close();
        }
    }
}
