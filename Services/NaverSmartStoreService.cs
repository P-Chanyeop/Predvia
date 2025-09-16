using System;
using System.Threading.Tasks;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using WebDriverManager;
using WebDriverManager.DriverConfigs.Impl;

namespace Gumaedaehang.Services
{
    public class NaverSmartStoreService
    {
        private IWebDriver? _driver;
        private bool _isDisposed = false;

        public async Task OpenNaverSmartStoreWithKeyword(string keyword)
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(NaverSmartStoreService));

            try
            {
                // 기존 드라이버가 있으면 정리
                if (_driver != null)
                {
                    try
                    {
                        _driver.Quit();
                        _driver.Dispose();
                    }
                    catch
                    {
                        // 무시
                    }
                    _driver = null;
                }

                // 크롬 드라이버 자동 다운로드 및 설정
                await Task.Run(() =>
                {
                    new DriverManager().SetUpDriver(new ChromeConfig());
                });

                // 크롬 옵션 설정
                var options = new ChromeOptions();
                options.AddArgument("--start-maximized");
                options.AddArgument("--disable-blink-features=AutomationControlled");
                options.AddExcludedArgument("enable-automation");
                options.AddAdditionalOption("useAutomationExtension", false);
                options.AddArgument("--disable-web-security");
                options.AddArgument("--allow-running-insecure-content");

                // 크롬 드라이버 시작
                _driver = new ChromeDriver(options);

                // 네이버 스마트스토어 해외직구 페이지로 이동
                string encodedKeyword = Uri.EscapeDataString(keyword);
                string url = $"https://smartstore.naver.com/globalshop/search?q={encodedKeyword}";
                
                await Task.Run(() => _driver.Navigate().GoToUrl(url));

                // 페이지 로드 대기
                await Task.Delay(3000);
            }
            catch (Exception ex)
            {
                // 오류 발생 시 드라이버 정리
                try
                {
                    _driver?.Quit();
                    _driver?.Dispose();
                    _driver = null;
                }
                catch
                {
                    // 무시
                }
                
                throw new Exception($"네이버 스마트스토어 연결 실패: {ex.Message}");
            }
        }

        public void Close()
        {
            if (_isDisposed)
                return;

            try
            {
                _driver?.Quit();
            }
            catch
            {
                // 무시
            }
            finally
            {
                try
                {
                    _driver?.Dispose();
                }
                catch
                {
                    // 무시
                }
                _driver = null;
                _isDisposed = true;
            }
        }

        public void Dispose()
        {
            Close();
        }
    }
}
