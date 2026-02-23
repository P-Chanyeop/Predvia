using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using System;
using System.Reflection;
using System.Threading.Tasks;
using Squirrel;

namespace Gumaedaehang
{
    public class App : Application
    {
        public override void Initialize()
        {
            // 🔥 시크릿 환경변수 로드 (가장 먼저 실행)
            Gumaedaehang.Services.EnvLoader.Load();
            
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
            
            // 백그라운드 업데이트 체크
            Task.Run(CheckForUpdates);
        }
        
        private async Task CheckForUpdates()
        {
            try
            {
                using var mgr = new UpdateManager("https://github.com/P-Chanyeop/Predvia/releases/latest/download");
                var updateInfo = await mgr.CheckForUpdate();
                if (updateInfo.ReleasesToApply.Count > 0)
                {
                    var newVersion = updateInfo.FutureReleaseEntry.Version;
                    LogWindow.AddLogStatic($"🔄 업데이트 발견: v{newVersion}");
                    
                    // UI 스레드에서 팝업 표시
                    var doUpdate = await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(async () =>
                    {
                        var tcs = new TaskCompletionSource<bool>();
                        var popup = new Avalonia.Controls.Window
                        {
                            Title = "업데이트 알림",
                            Width = 380, Height = 160,
                            WindowStartupLocation = Avalonia.Controls.WindowStartupLocation.CenterScreen,
                            CanResize = false,
                            Content = new Avalonia.Controls.StackPanel
                            {
                                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                                Spacing = 15,
                                Children =
                                {
                                    new Avalonia.Controls.TextBlock
                                    {
                                        Text = $"새 버전 v{newVersion}이 있습니다.\n업데이트 하시겠습니까?",
                                        FontSize = 15, TextAlignment = Avalonia.Media.TextAlignment.Center
                                    },
                                    new Avalonia.Controls.StackPanel
                                    {
                                        Orientation = Avalonia.Layout.Orientation.Horizontal,
                                        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                                        Spacing = 15,
                                        Children =
                                        {
                                            new Avalonia.Controls.Button { Content = "업데이트", Width = 100, Tag = "yes" },
                                            new Avalonia.Controls.Button { Content = "나중에", Width = 100, Tag = "no" }
                                        }
                                    }
                                }
                            }
                        };
                        var btnPanel = (popup.Content as Avalonia.Controls.StackPanel)?.Children[1] as Avalonia.Controls.StackPanel;
                        (btnPanel?.Children[0] as Avalonia.Controls.Button)!.Click += (s, e) => { tcs.TrySetResult(true); popup.Close(); };
                        (btnPanel?.Children[1] as Avalonia.Controls.Button)!.Click += (s, e) => { tcs.TrySetResult(false); popup.Close(); };
                        popup.Show();
                        return await tcs.Task;
                    });
                    
                    if (doUpdate)
                    {
                        LogWindow.AddLogStatic("⬇️ 업데이트 다운로드 중...");
                        await mgr.UpdateApp();
                        LogWindow.AddLogStatic("✅ 업데이트 완료 - 재시작합니다");
                        UpdateManager.RestartApp();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"업데이트 체크 스킵: {ex.Message}");
            }
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
                    resources["BackgroundSecondaryBrush"] = new SolidColorBrush(Color.Parse("#373737"));
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
