using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace Gumaedaehang
{
    public partial class LogWindow : Window
    {
        private TextBox? _logTextBox;
        private static LogWindow? _instance;
        private readonly ConcurrentQueue<string> _logQueue = new();
        private readonly Timer _updateTimer;
        private volatile bool _isUpdating = false;
        
        public LogWindow()
        {
            InitializeComponent();
            _logTextBox = this.FindControl<TextBox>("LogTextBox");
            _instance = this;
            
            // 100ms마다 로그 큐 처리
            _updateTimer = new Timer(ProcessLogQueue, null, 100, 100);
        }
        
        private void ProcessLogQueue(object? state)
        {
            if (_isUpdating || _logQueue.IsEmpty) return;
            
            _isUpdating = true;
            
            // 백그라운드 스레드에서 로그 처리
            Task.Run(() =>
            {
                var logs = new System.Text.StringBuilder();
                int processedCount = 0;
                
                // 한 번에 최대 50개 로그 처리
                while (_logQueue.TryDequeue(out string? logMessage) && processedCount < 50)
                {
                    logs.AppendLine(logMessage);
                    processedCount++;
                }
                
                if (processedCount > 0)
                {
                    // UI 업데이트만 메인 스레드에서
                    Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    {
                        if (_logTextBox != null)
                        {
                            _logTextBox.Text += logs.ToString();
                            
                            // 스크롤을 맨 아래로
                            _logTextBox.CaretIndex = _logTextBox.Text.Length;
                        }
                    });
                }
                
                _isUpdating = false;
            });
        }
        
        public void AddLog(string message)
        {
            string timestamp = DateTime.Now.ToString("HH:mm:ss");
            string logEntry = $"[{timestamp}] {message}";
            
            // 큐에 추가 (스레드 안전)
            _logQueue.Enqueue(logEntry);
        }
        
        // 정적 메서드로 어디서든 로그 추가 가능
        public static void AddLogStatic(string message)
        {
            _instance?.AddLog(message);
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
            
            // 큐도 비우기
            while (_logQueue.TryDequeue(out _)) { }
        }
        
        private void CloseButton_Click(object? sender, RoutedEventArgs e)
        {
            Hide();
        }
        
        protected override void OnClosed(EventArgs e)
        {
            _updateTimer?.Dispose();
            base.OnClosed(e);
        }
    }
}
