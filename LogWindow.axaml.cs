using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using System;

namespace Gumaedaehang
{
    public partial class LogWindow : Window
    {
        private TextBox? _logTextBox;
        
        public LogWindow()
        {
            InitializeComponent();
            _logTextBox = this.FindControl<TextBox>("LogTextBox");
        }
        
        public void AddLog(string message)
        {
            if (_logTextBox != null)
            {
                string timestamp = DateTime.Now.ToString("HH:mm:ss");
                _logTextBox.Text += $"[{timestamp}] {message}\n";
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
