using System;

namespace Gumaedaehang.Services
{
    public class AuthManager
    {
        private static AuthManager _instance;
        public static AuthManager Instance => _instance ??= new AuthManager();

        private AuthManager() { }

        // 인증 상태
        public bool IsAuthenticated { get; private set; }
        public string Username { get; private set; }
        public string Token { get; private set; }

        // 인증 상태 변경 이벤트
        public event EventHandler<AuthStateChangedEventArgs> AuthStateChanged;

        // 로그인 처리
        public void Login(string username, string token)
        {
            Username = username;
            Token = token;
            IsAuthenticated = true;
            
            // 이벤트 발생
            AuthStateChanged?.Invoke(this, new AuthStateChangedEventArgs(true, username));
        }

        // 로그아웃 처리
        public void Logout()
        {
            string oldUsername = Username;
            
            Username = null;
            Token = null;
            IsAuthenticated = false;
            
            // 이벤트 발생
            AuthStateChanged?.Invoke(this, new AuthStateChangedEventArgs(false, oldUsername));
        }
    }

    // 인증 상태 변경 이벤트 인자
    public class AuthStateChangedEventArgs : EventArgs
    {
        public bool IsAuthenticated { get; }
        public string Username { get; }

        public AuthStateChangedEventArgs(bool isAuthenticated, string username)
        {
            IsAuthenticated = isAuthenticated;
            Username = username;
        }
    }
}
