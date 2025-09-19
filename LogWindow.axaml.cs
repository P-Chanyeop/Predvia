using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using System;

namespace Gumaedaehang
{
    public partial class LogWindow : Window
    {
        private TextBox? _logTextBox;
        private static LogWindow? _instance;
        
        public LogWindow()
        {
            InitializeComponent();
            _logTextBox = this.FindControl<TextBox>("LogTextBox");
            _instance = this;
        }
        
        public void AddLog(string message)
        {
            if (_logTextBox != null)
            {
                string timestamp = DateTime.Now.ToString("HH:mm:ss");
                _logTextBox.Text += $"[{timestamp}] {message}\n";
            }
        }
        
        // 정적 메서드로 어디서든 로그 추가 가능
        public static void AddLogStatic(string message)
        {
            if (_instance != null)
            {
                // UI 스레드에서 실행
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    _instance.AddLog(message);
                });
            }
        }
        
        private async void CopyButton_Click(object? sender, RoutedEventArgs e)
        {
            if (_logTextBox != null && !string.IsNullOrEmpty(_logTextBox.Text))
            {
                var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
                if (clipboard != null)
                {
                    await clipboard.SetTextAsync(_logTextBox.Text);
                }
            }
        }
        
        private void ClearButton_Click(object? sender, RoutedEventArgs e)
        {
            if (_logTextBox != null)
            {
                _logTextBox.Text = "";
            }
        }
        
        private void CloseButton_Click(object? sender, RoutedEventArgs e)
        {
            Hide();
        }
    }
}
