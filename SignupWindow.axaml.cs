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
    public partial class SignupWindow : Window
    {
        private TextBox? _usernameTextBox;
        private TextBox? _passwordTextBox;
        private TextBox? _confirmPasswordTextBox;
        private Button? _signupButton;
        private TextBlock? _loginLink;
        private TextBlock? _errorMessage;
        private Button? _themeToggleButton;
        private TextBlock? _themeToggleText;
        
        // API 클라이언트
        private readonly ApiClient _apiClient;
        private bool _isRegistering = false;

        public SignupWindow()
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
            _confirmPasswordTextBox = this.FindControl<TextBox>("confirmPasswordTextBox");
            _signupButton = this.FindControl<Button>("signupButton");
            _loginLink = this.FindControl<TextBlock>("loginLink");
            _errorMessage = this.FindControl<TextBlock>("errorMessage");
            _themeToggleButton = this.FindControl<Button>("themeToggleButton");
            _themeToggleText = this.FindControl<TextBlock>("themeToggleText");
            
            // 이벤트 핸들러 등록
            if (_signupButton != null)
                _signupButton.Click += SignupButton_Click;
            
            if (_loginLink != null)
                _loginLink.PointerPressed += LoginLink_PointerPressed;
                
            if (_themeToggleButton != null)
                _themeToggleButton.Click += ThemeToggleButton_Click;
                
            // 현재 테마에 맞게 버튼 텍스트 업데이트
            UpdateThemeToggleText();
            
            // 테마 변경 이벤트 구독
            ThemeManager.Instance.ThemeChanged += (sender, theme) => UpdateThemeToggleText();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
        
        private async void SignupButton_Click(object? sender, RoutedEventArgs e)
        {
            if (_usernameTextBox == null || _passwordTextBox == null || _confirmPasswordTextBox == null || 
                _signupButton == null || _errorMessage == null)
                return;
                
            string username = _usernameTextBox.Text ?? string.Empty;
            string password = _passwordTextBox.Text ?? string.Empty;
            string confirmPassword = _confirmPasswordTextBox.Text ?? string.Empty;
            
            // 입력 유효성 검사
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password) || 
                string.IsNullOrWhiteSpace(confirmPassword))
            {
                _errorMessage.Text = "모든 필드를 입력해주세요.";
                _errorMessage.IsVisible = true;
                return;
            }
            
            // 비밀번호 일치 검사
            if (password != confirmPassword)
            {
                _errorMessage.Text = "비밀번호와 비밀번호 확인이 일치하지 않습니다.";
                _errorMessage.IsVisible = true;
                return;
            }
            
            // 중복 회원가입 방지
            if (_isRegistering)
                return;
                
            _isRegistering = true;
            _signupButton.IsEnabled = false;
            _errorMessage.IsVisible = false;
            
            try
            {
                // API 회원가입 호출 (테스트 모드)
                var response = await _apiClient.RegisterTestModeAsync(username, password, confirmPassword);
                
                // 회원가입 성공 처리
                AuthManager.Instance.Login(response.Username, response.Token);
                
                // API 키 인증 창으로 이동
                var apiKeyAuthWindow = new ApiKeyAuthWindow();
                apiKeyAuthWindow.Show();
                this.Close();
            }
            catch (ApiException ex)
            {
                // 회원가입 실패 처리
                _errorMessage.Text = ex.ErrorDetails;
                _errorMessage.IsVisible = true;
            }
            catch (Exception)
            {
                // 기타 예외 처리
                _errorMessage.Text = "회원가입 중 오류가 발생했습니다. 다시 시도해주세요.";
                _errorMessage.IsVisible = true;
            }
            finally
            {
                _isRegistering = false;
                _signupButton.IsEnabled = true;
            }
        }
        
        private void LoginLink_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            // 로그인 화면으로 이동
            var loginWindow = new LoginWindow();
            loginWindow.Show();
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
