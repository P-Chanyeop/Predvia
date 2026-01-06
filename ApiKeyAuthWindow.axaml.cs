using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Gumaedaehang.Services;
using System;
using System.Threading.Tasks;

namespace Gumaedaehang
{
    public partial class ApiKeyAuthWindow : Window
    {
        private TextBox? _apiKeyTextBox;
        private Button? _authenticateButton;
        private TextBlock? _errorMessage;
        private Button? _themeToggleButton;
        private TextBlock? _themeToggleText;
        
        // API 키 인증 클라이언트
        private readonly ApiKeyAuthClient _apiKeyAuthClient;
        private bool _isAuthenticating = false;

        public ApiKeyAuthWindow()
        {
            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif

            // 주 모니터 전체화면 설정
            var screen = Screens.Primary;
            if (screen != null)
            {
                Position = screen.WorkingArea.TopLeft;
                WindowState = WindowState.Maximized;
            }

            // API 키 인증 클라이언트 초기화
            _apiKeyAuthClient = new ApiKeyAuthClient();
            
            // UI 요소 참조 가져오기
            _apiKeyTextBox = this.FindControl<TextBox>("apiKeyTextBox");
            _authenticateButton = this.FindControl<Button>("authenticateButton");
            _errorMessage = this.FindControl<TextBlock>("errorMessage");
            _themeToggleButton = this.FindControl<Button>("themeToggleButton");
            _themeToggleText = this.FindControl<TextBlock>("themeToggleText");
            
            // 이벤트 핸들러 등록
            if (_authenticateButton != null)
                _authenticateButton.Click += async (s, e) => await AuthenticateButton_Click(s, e);
                
            if (_themeToggleButton != null)
                _themeToggleButton.Click += ThemeToggleButton_Click;
                
            // 엔터키 이벤트 등록
            if (_apiKeyTextBox != null)
                _apiKeyTextBox.KeyDown += ApiKeyTextBox_KeyDown;
                
            // 현재 테마에 맞게 버튼 텍스트 업데이트
            UpdateThemeToggleText();
            
            // 테마 변경 이벤트 구독
            ThemeManager.Instance.ThemeChanged += (sender, theme) => UpdateThemeToggleText();
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
        
        private async Task AuthenticateButton_Click(object? sender, RoutedEventArgs e)
        {
            if (_apiKeyTextBox == null || _authenticateButton == null || _errorMessage == null)
                return;
                
            string apiKey = _apiKeyTextBox.Text ?? string.Empty;
            
            // 입력 유효성 검사
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                _errorMessage.Text = "API 키를 입력해주세요.";
                _errorMessage.IsVisible = true;
                return;
            }
            
            // 중복 인증 방지
            if (_isAuthenticating)
                return;
                
            _isAuthenticating = true;
            _authenticateButton.IsEnabled = false;
            _errorMessage.IsVisible = false;
            
            try
            {
                // API 키 인증 호출 (테스트 모드)
                var response = await _apiKeyAuthClient.AuthenticateWithApiKeyTestModeAsync(apiKey);
                
                // 인증 성공 처리
                AuthManager.Instance.Login(response.CompanyName, apiKey);
                
                // 메인 창으로 이동
                var mainWindow = new MainWindow();
                mainWindow.Show();
                this.Close();
            }
            catch (ApiException ex)
            {
                // 인증 실패 처리
                _errorMessage.Text = ex.ErrorDetails;
                _errorMessage.IsVisible = true;
            }
            catch (Exception ex)
            {
                // 기타 예외 처리 - 디버깅을 위해 실제 예외 정보 출력
                System.Diagnostics.Debug.WriteLine($"API 키 인증 중 예외 발생: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"스택 트레이스: {ex.StackTrace}");
                _errorMessage.Text = $"인증 중 오류가 발생했습니다. 다시 시도해주세요. (오류: {ex.Message})";
                _errorMessage.IsVisible = true;
            }
            finally
            {
                _isAuthenticating = false;
                _authenticateButton.IsEnabled = true;
            }
        }
        
        private void ThemeToggleButton_Click(object? sender, RoutedEventArgs e)
        {
            // 테마 전환
            ThemeManager.Instance.ToggleTheme();
        }
        
        private void UpdateThemeToggleText()
        {
            if (_themeToggleText != null)
            {
                _themeToggleText.Text = ThemeManager.Instance.IsDarkTheme ? "라이트모드" : "다크모드";
            }
        }
    }
}
