using Avalonia;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using System;

namespace Gumaedaehang
{
    public class ThemeManager
    {
        public enum ThemeType
        {
            Light,
            Dark
        }
        
        private static ThemeManager? _instance;
        public static ThemeManager Instance => _instance ??= new ThemeManager();
        
        public event EventHandler<ThemeType>? ThemeChanged;
        
        private ThemeType _currentTheme = ThemeType.Light;
        
        public ThemeType CurrentTheme
        {
            get => _currentTheme;
            set
            {
                if (_currentTheme != value)
                {
                    _currentTheme = value;
                    ApplyTheme();
                    ThemeChanged?.Invoke(this, _currentTheme);
                }
            }
        }
        
        private ThemeManager()
        {
            // 초기 테마 설정
            _currentTheme = ThemeType.Light;
        }
        
        public void ToggleTheme()
        {
            CurrentTheme = CurrentTheme == ThemeType.Light ? ThemeType.Dark : ThemeType.Light;
        }
        
        private void ApplyTheme()
        {
            var app = Application.Current;
            if (app != null)
            {
                // Avalonia 0.10.18 버전에서는 다음과 같이 테마를 변경합니다
                foreach (var style in app.Styles)
                {
                    if (style is FluentTheme fluentTheme)
                    {
                        fluentTheme.Mode = CurrentTheme == ThemeType.Light ? FluentThemeMode.Light : FluentThemeMode.Dark;
                        break;
                    }
                }
            }
        }
        
        public bool IsDarkTheme => CurrentTheme == ThemeType.Dark;
    }
}
