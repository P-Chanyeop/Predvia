using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

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
        private Button? _backButton;

        public SignupWindow()
        {
            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif
            
            // UI 요소 참조 가져오기
            _usernameTextBox = this.FindControl<TextBox>("usernameTextBox");
            _passwordTextBox = this.FindControl<TextBox>("passwordTextBox");
            _confirmPasswordTextBox = this.FindControl<TextBox>("confirmPasswordTextBox");
            _signupButton = this.FindControl<Button>("signupButton");
            _loginLink = this.FindControl<TextBlock>("loginLink");
            _errorMessage = this.FindControl<TextBlock>("errorMessage");
            _backButton = this.FindControl<Button>("backButton");
            
            // 이벤트 핸들러 등록
            if (_signupButton != null)
                _signupButton.Click += SignupButton_Click;
            
            if (_loginLink != null)
                _loginLink.PointerPressed += LoginLink_PointerPressed;
                
            if (_backButton != null)
                _backButton.Click += BackButton_Click;
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
        
        private void SignupButton_Click(object? sender, RoutedEventArgs e)
        {
            if (_usernameTextBox == null || _passwordTextBox == null || _confirmPasswordTextBox == null)
                return;
                
            string username = _usernameTextBox.Text ?? string.Empty;
            string password = _passwordTextBox.Text ?? string.Empty;
            string confirmPassword = _confirmPasswordTextBox.Text ?? string.Empty;
            
            // 유효성 검사
            if (string.IsNullOrWhiteSpace(username))
            {
                ShowError("아이디를 입력해주세요.");
                return;
            }
            
            if (string.IsNullOrWhiteSpace(password))
            {
                ShowError("비밀번호를 입력해주세요.");
                return;
            }
            
            if (password != confirmPassword)
            {
                ShowError("비밀번호가 일치하지 않습니다.");
                return;
            }
            
            if (password.Length < 6)
            {
                ShowError("비밀번호는 6자 이상이어야 합니다.");
                return;
            }
            
            // 회원가입 로직 구현
            // 실제 구현에서는 데이터베이스나 API를 통해 사용자 등록
            if (RegisterUser(username, password))
            {
                // 회원가입 성공 시 로그인 창으로 이동
                var loginWindow = new LoginWindow();
                loginWindow.Show();
                this.Close();
            }
            else
            {
                ShowError("이미 사용 중인 아이디입니다.");
            }
        }
        
        private void LoginLink_PointerPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
        {
            // 로그인 페이지로 이동
            var loginWindow = new LoginWindow();
            loginWindow.Show();
            this.Close();
        }
        
        private void BackButton_Click(object? sender, RoutedEventArgs e)
        {
            // 로그인 페이지로 돌아가기
            var loginWindow = new LoginWindow();
            loginWindow.Show();
            this.Close();
        }
        
        private bool RegisterUser(string username, string password)
        {
            // 실제 구현에서는 데이터베이스나 API를 통해 사용자 등록
            // 임시 구현: 아이디가 "admin"이 아닌 경우 회원가입 성공
            return username != "admin";
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
