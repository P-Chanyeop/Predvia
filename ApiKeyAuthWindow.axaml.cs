using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Gumaedaehang.Services;
using System;
using System.Threading.Tasks;
using System.Net.Http;
using System.Text.Json;
using System.Net.NetworkInformation;
using System.Linq;

namespace Gumaedaehang
{
    public partial class ApiKeyAuthWindow : Window
    {
        private TextBox? _apiKeyTextBox;
        private Button? _authenticateButton;
        private TextBlock? _errorMessage;
        
        private bool _isAuthenticating = false;
        private const long PRODUCT_ID = 13; // Predvia 상품 ID

        public ApiKeyAuthWindow()
        {
            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif

            var screen = Screens.Primary;
            if (screen != null)
            {
                Position = screen.WorkingArea.TopLeft;
                WindowState = WindowState.Maximized;
            }

            _apiKeyTextBox = this.FindControl<TextBox>("apiKeyTextBox");
            _authenticateButton = this.FindControl<Button>("authenticateButton");
            _errorMessage = this.FindControl<TextBlock>("errorMessage");
            
            if (_authenticateButton != null)
                _authenticateButton.Click += async (s, e) => await AuthenticateButton_Click(s, e);
                
            if (_apiKeyTextBox != null)
                _apiKeyTextBox.KeyDown += ApiKeyTextBox_KeyDown;
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
        
        private void ApiKeyTextBox_KeyDown(object? sender, Avalonia.Input.KeyEventArgs e)
        {
            if (e.Key == Avalonia.Input.Key.Enter)
            {
                _ = AuthenticateButton_Click(sender, new RoutedEventArgs());
            }
        }
        
        private string GetMacAddress()
        {
            var networkInterface = NetworkInterface.GetAllNetworkInterfaces()
                .FirstOrDefault(nic => nic.OperationalStatus == OperationalStatus.Up &&
                                       nic.NetworkInterfaceType != NetworkInterfaceType.Loopback);
            if (networkInterface == null)
                return "Unknown";

            return string.Join(":", networkInterface.GetPhysicalAddress()
                .GetAddressBytes()
                .Select(b => b.ToString("X2")));
        }
        
        private async Task AuthenticateButton_Click(object? sender, RoutedEventArgs e)
        {
            if (_apiKeyTextBox == null || _authenticateButton == null || _errorMessage == null)
                return;
                
            string apiKey = _apiKeyTextBox.Text ?? string.Empty;
            
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                _errorMessage.Text = "API 키를 입력해주세요.";
                _errorMessage.IsVisible = true;
                return;
            }
            
            if (_isAuthenticating)
                return;
                
            _isAuthenticating = true;
            _authenticateButton.IsEnabled = false;
            _errorMessage.IsVisible = false;
            
            try
            {
                // ⭐ 관리자 키 확인 - API 인증 건너뛰기
                if (AuthManager.IsAdminKey(apiKey))
                {
                    AuthManager.Instance.Login("관리자", apiKey, 9999);
                    var adminWindow = new MainWindow();
                    adminWindow.Show();
                    this.Close();
                    return;
                }
                
                using var client = new HttpClient();
                
                // 1. API 키 인증
                string authUrl = $"http://13.209.199.124:8080/api/subscription/hash-key-auth/temp?id={PRODUCT_ID}&hashKey={apiKey}";
                var authResponse = await client.GetAsync(authUrl);
                
                if (!authResponse.IsSuccessStatusCode)
                {
                    _errorMessage.Text = "API 인증에 실패하였습니다.";
                    _errorMessage.IsVisible = true;
                    return;
                }
                
                string authJson = await authResponse.Content.ReadAsStringAsync();
                var authDoc = JsonDocument.Parse(authJson);
                
                // 2. MAC 주소 검증
                string macAddress = GetMacAddress();
                string macUrl = $"http://13.209.199.124:8080/api/subscription/verify-mac/temp?id={PRODUCT_ID}&hashKey={apiKey}&macAddress={macAddress}";
                var macResponse = await client.PostAsync(macUrl, null);
                
                string macJson = await macResponse.Content.ReadAsStringAsync();
                var macDoc = JsonDocument.Parse(macJson);
                
                if (macDoc.RootElement.GetProperty("result").GetString() == "fail")
                {
                    _errorMessage.Text = "MAC 주소 인증에 실패하였습니다.";
                    _errorMessage.IsVisible = true;
                    return;
                }
                
                // 3. 사용자 정보 추출
                string nickname = authDoc.RootElement.GetProperty("name").GetString() ?? "사용자";
                int remainingDays = authDoc.RootElement.GetProperty("remainingDays").GetInt32();
                
                // 4. AuthManager에 저장
                AuthManager.Instance.Login(nickname, apiKey, remainingDays);
                
                // 5. 메인 창으로 이동
                var mainWindow = new MainWindow();
                mainWindow.Show();
                this.Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[오류] {ex.Message}");
                _errorMessage.Text = $"연결 오류: {ex.Message}";
                _errorMessage.IsVisible = true;
            }
            finally
            {
                _isAuthenticating = false;
                _authenticateButton.IsEnabled = true;
            }
        }
    }
}
