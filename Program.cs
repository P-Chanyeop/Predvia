using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Squirrel;

[assembly: System.Reflection.AssemblyMetadata("SquirrelAwareVersion", "1")]

namespace Gumaedaehang
{
    class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            // .env 암호화 모드
            if (args.Length >= 3 && args[0] == "--encrypt-env")
            {
                Services.EnvLoader.EncryptEnvFile(args[1], args[2]);
                Console.WriteLine($"Encrypted: {args[1]} -> {args[2]}");
                return;
            }

            // Squirrel 설치/제거 이벤트 처리
            SquirrelAwareApp.HandleEvents(
                onInitialInstall: (_, v) => { },
                onAppUninstall: (_, v) => { },
                onEveryRun: (_, v, _) => { }
            );

            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }

        public static AppBuilder BuildAvaloniaApp()
            => AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .WithInterFont()
                .With(new Win32PlatformOptions
                {
                    RenderingMode = new[] { Win32RenderingMode.AngleEgl }
                })
                .LogToTrace();
    }
}
