using Avalonia;
using Avalonia.Controls;
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
            
            // 이벤트 핸들러 등록
            if (_loginButton != null)
                _loginButton.Click += LoginButton_Click;
            
            if (_signupLink != null)
                _signupLink.PointerPressed += SignupLink_PointerPressed;
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
        
        private void LoginButton_Click(object? sender, RoutedEventArgs e)
        {
            if (_usernameTextBox == null || _passwordTextBox == null)
                return;
                
            string username = _usernameTextBox.Text ?? string.Empty;
            string password = _passwordTextBox.Text ?? string.Empty;
            
            // 간단한 유효성 검사
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                ShowError("아이디와 비밀번호를 모두 입력해주세요.");
                return;
            }
            
            // 로그인 로직 구현
            // 실제 구현에서는 데이터베이스나 API를 통해 인증 처리
            if (AuthenticateUser(username, password))
            {
                // 로그인 성공 시 메인 창으로 이동
                var mainWindow = new MainWindow();
                mainWindow.Show();
                this.Close();
            }
            else
            {
                // 로그인 실패 처리
                ShowError("아이디 또는 비밀번호가 올바르지 않습니다.");
            }
        }
        
        private void SignupLink_PointerPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
        {
            // 회원가입 페이지로 이동
            var signupWindow = new SignupWindow();
            signupWindow.Show();
            this.Hide();
        }
        
        private bool AuthenticateUser(string username, string password)
        {
            // 실제 구현에서는 데이터베이스나 API를 통해 인증
            // 임시 구현: 아이디가 "admin"이고 비밀번호가 "password"인 경우 로그인 성공
            return username == "admin" && password == "password";
        }
        
        private void ShowError(string message)
        {
            if (_errorMessage != null)
            {
                _errorMessage.Text = message;
                _errorMessage.IsVisible = true;
            }
        }
    }
}
