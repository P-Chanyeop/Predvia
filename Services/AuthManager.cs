using System;

namespace Gumaedaehang.Services
{
    public class AuthManager
    {
        private static AuthManager? _instance;
        public static AuthManager Instance => _instance ??= new AuthManager();

        private AuthManager() { }

        // 관리자 API 키
        private const string ADMIN_API_KEY = "PREDVIA-ADMIN-2026";

        // 인증 상태
        public bool IsAuthenticated { get; private set; }
        public bool IsLoggedIn => IsAuthenticated;
        public string? Username { get; private set; }
        public string? Token { get; private set; }
        public int RemainingDays { get; private set; }
        public bool IsAdmin { get; private set; }

        // 인증 상태 변경 이벤트
        public event EventHandler<AuthStateChangedEventArgs>? AuthStateChanged;

        // 관리자 키 확인
        public static bool IsAdminKey(string apiKey) => apiKey == ADMIN_API_KEY;

        // 로그인 처리
        public void Login(string? username, string? token, int remainingDays = 0)
        {
            Username = username ?? "Unknown";
            Token = token ?? "";
            RemainingDays = remainingDays;
            IsAdmin = token == ADMIN_API_KEY;
            IsAuthenticated = true;
            
            AuthStateChanged?.Invoke(this, new AuthStateChangedEventArgs(true, Username));
        }

        // 로그아웃 처리
        public void Logout()
        {
            string? oldUsername = Username;
            
            Username = null;
            Token = null;
            RemainingDays = 0;
            IsAdmin = false;
            IsAuthenticated = false;
            
            AuthStateChanged?.Invoke(this, new AuthStateChangedEventArgs(false, oldUsername));
        }
    }

    public class AuthStateChangedEventArgs : EventArgs
    {
        public bool IsAuthenticated { get; }
        public string? Username { get; }

        public AuthStateChangedEventArgs(bool isAuthenticated, string? username)
        {
            IsAuthenticated = isAuthenticated;
            Username = username;
        }
    }
}
