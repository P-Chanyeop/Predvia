using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Gumaedaehang.Services;
using System;
using System.Threading.Tasks;

namespace Gumaedaehang
{
    public partial class LoginWindow : Window
    {
        private TextBox? _usernameTextBox;
        private TextBox? _passwordTextBox;
        private Button? _loginButton;
        private TextBlock? _signupLink;
        private TextBlock? _errorMessage;
        private Button? _themeToggleButton;
        private TextBlock? _themeToggleText;
        
        // API 클라이언트
        private readonly ApiClient _apiClient;
        private bool _isLoggingIn = false;

        public LoginWindow()
        {
            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif
            
            // API 클라이언트 초기화
            _apiClient = new ApiClient();
            
            // UI 요소 참조 가져오기
            _usernameTextBox = this.FindControl<TextBox>("usernameTextBox");
            _passwordTextBox = this.FindControl<TextBox>("passwordTextBox");
            _loginButton = this.FindControl<Button>("loginButton");
            _signupLink = this.FindControl<TextBlock>("signupLink");
            _errorMessage = this.FindControl<TextBlock>("errorMessage");
            _themeToggleButton = this.FindControl<Button>("themeToggleButton");
            _themeToggleText = this.FindControl<TextBlock>("themeToggleText");
            
            // 이벤트 핸들러 등록
            if (_loginButton != null)
                _loginButton.Click += async (s, e) => await LoginButton_Click(s, e);
            
            if (_signupLink != null)
                _signupLink.PointerPressed += SignupLink_PointerPressed;
                
            if (_themeToggleButton != null)
                _themeToggleButton.Click += ThemeToggleButton_Click;
                
            // 엔터키 이벤트 등록
            if (_passwordTextBox != null)
                _passwordTextBox.KeyDown += PasswordTextBox_KeyDown;
                
            // 현재 테마에 맞게 버튼 텍스트 업데이트
            UpdateThemeToggleText();
            
            // 테마 변경 이벤트 구독
            ThemeManager.Instance.ThemeChanged += (sender, theme) => UpdateThemeToggleText();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
        
        private void PasswordTextBox_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                _ = LoginButton_Click(sender, new RoutedEventArgs());
            }
        }
        
        private async Task LoginButton_Click(object? sender, RoutedEventArgs e)
        {
            if (_usernameTextBox == null || _passwordTextBox == null || _loginButton == null || _errorMessage == null)
                return;
                
            string username = _usernameTextBox.Text ?? string.Empty;
            string password = _passwordTextBox.Text ?? string.Empty;
            
            // 입력 유효성 검사
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                _errorMessage.Text = "아이디와 비밀번호를 모두 입력해주세요.";
                _errorMessage.IsVisible = true;
                return;
            }
            
            // 중복 로그인 방지
            if (_isLoggingIn)
                return;
                
            _isLoggingIn = true;
            _loginButton.IsEnabled = false;
            _errorMessage.IsVisible = false;
            
            try
            {
                // API 로그인 호출 (테스트 모드)
                var response = await _apiClient.LoginTestModeAsync(username, password);
                
                // 로그인 성공 처리
                AuthManager.Instance.Login(response.User?.Username ?? username, response.Token);
                
                // API 키 인증 창으로 이동
                var apiKeyAuthWindow = new ApiKeyAuthWindow();
                apiKeyAuthWindow.Show();
                this.Close();
            }
            catch (ApiException ex)
            {
                // 로그인 실패 처리
                _errorMessage.Text = ex.ErrorDetails;
                _errorMessage.IsVisible = true;
            }
            catch (Exception)
            {
                // 기타 예외 처리
                _errorMessage.Text = "로그인 중 오류가 발생했습니다. 다시 시도해주세요.";
                _errorMessage.IsVisible = true;
            }
            finally
            {
                _isLoggingIn = false;
                _loginButton.IsEnabled = true;
            }
        }
        
        private void SignupLink_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            // 회원가입 화면으로 이동
            var signupWindow = new SignupWindow();
            signupWindow.Show();
            this.Close();
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
