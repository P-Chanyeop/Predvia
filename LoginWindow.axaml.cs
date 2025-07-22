using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

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

        public LoginWindow()
        {
            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif
            
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
                _loginButton.Click += LoginButton_Click;
            
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
                LoginButton_Click(sender, new RoutedEventArgs());
            }
        }
        
        private void LoginButton_Click(object? sender, RoutedEventArgs e)
        {
            if (_usernameTextBox == null || _passwordTextBox == null)
                return;
                
            string username = _usernameTextBox.Text ?? string.Empty;
            string password = _passwordTextBox.Text ?? string.Empty;
            
            // 로그인 로직 구현
            // 실제 구현에서는 데이터베이스나 API를 통해 인증 처리
            if (AuthenticateUser(username, password))
            {
                // 로그인 성공 시 메인 창으로 이동
                var mainWindow = new MainWindow();
                mainWindow.SetUsername(username); // 사용자 아이디 전달
                mainWindow.Show();
                this.Close();
            }
            else
            {
                // 로그인 실패 시 오류 메시지 표시
                if (_errorMessage != null)
                {
                    _errorMessage.Text = "아이디 또는 비밀번호가 올바르지 않습니다.";
                    _errorMessage.IsVisible = true;
                }
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
        
        // 사용자 인증 메서드 (실제 구현에서는 데이터베이스나 API 사용)
        private bool AuthenticateUser(string username, string password)
        {
            // 테스트용 계정
            return (username == "admin" && password == "admin") || 
                   (username == "test" && password == "test");
        }
    }
}
