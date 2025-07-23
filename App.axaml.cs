using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using System;
using System.Reflection;

namespace Gumaedaehang
{
    public class App : Application
    {
        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
            
            // 테마 변경 이벤트 구독
            ThemeManager.Instance.ThemeChanged += OnThemeChanged;
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                // API 키 인증 창을 시작 창으로 설정
                desktop.MainWindow = new ApiKeyAuthWindow();
            }

            base.OnFrameworkInitializationCompleted();
        }
        
        private void OnThemeChanged(object? sender, ThemeManager.ThemeType theme)
        {
            // 테마 변경 시 동적 리소스 업데이트
            if (Current != null)
            {
                var resources = Current.Resources;
                
                if (theme == ThemeManager.ThemeType.Dark)
                {
                    resources["BackgroundBrush"] = new SolidColorBrush(Color.Parse("#1E1E1E"));
                    resources["BackgroundSecondaryBrush"] = new SolidColorBrush(Color.Parse("#2D2D2D"));
                    resources["ForegroundBrush"] = new SolidColorBrush(Color.Parse("#FFFFFF"));
                    resources["BorderBrush"] = new SolidColorBrush(Color.Parse("#444444"));
                    resources["AccentBrush"] = new SolidColorBrush(Color.Parse("#F47B20"));
                    resources["DarkModeIconBrush"] = new SolidColorBrush(Color.Parse("#FFFFFF"));
                }
                else
                {
                    resources["BackgroundBrush"] = new SolidColorBrush(Color.Parse("#FFFFFF"));
                    resources["BackgroundSecondaryBrush"] = new SolidColorBrush(Color.Parse("#FFF8F3"));
                    resources["ForegroundBrush"] = new SolidColorBrush(Color.Parse("#333333"));
                    resources["BorderBrush"] = new SolidColorBrush(Color.Parse("#DDDDDD"));
                    resources["AccentBrush"] = new SolidColorBrush(Color.Parse("#F47B20"));
                    resources["DarkModeIconBrush"] = new SolidColorBrush(Color.Parse("#333333"));
                }
            }
        }
    }
}
