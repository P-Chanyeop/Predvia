using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Gumaedaehang
{
    public partial class LogWindow : Window
    {
        private TextBox? _logTextBox;
        private static LogWindow? _instance;
        public static LogWindow? Instance => _instance;
        private readonly ConcurrentQueue<string> _logQueue = new();
        private readonly Timer _updateTimer;
        private volatile bool _isUpdating = false;
        private string _lastLogMessage = "";
        private DateTime _lastLogTime = DateTime.MinValue;
        
        public LogWindow()
        {
            InitializeComponent();
            _logTextBox = this.FindControl<TextBox>("LogTextBox");
            _instance = this;
            
            // 이벤트 핸들러 등록
            var copyButton = this.FindControl<Button>("CopyButton");
            var clearButton = this.FindControl<Button>("ClearButton");
            var closeButton = this.FindControl<Button>("CloseButton");
            
            if (copyButton != null)
                copyButton.Click += async (s, e) => await CopyButton_Click(s, e);
            if (clearButton != null)
                clearButton.Click += ClearButton_Click;
            if (closeButton != null)
                closeButton.Click += CloseButton_Click;
            
            // 로그 업데이트 간격을 1초로 증가 (성능 개선)
            _updateTimer = new Timer(ProcessLogQueue, null, 1000, 1000);
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
                
                // 배치 처리 크기를 100개로 증가
                while (_logQueue.TryDequeue(out string? logMessage) && processedCount < 100)
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
                            
                            // 로그 텍스트 길이 제한 (50,000자 초과 시 앞부분 제거)
                            if (_logTextBox.Text?.Length > 50000)
                            {
                                var lines = _logTextBox.Text.Split(Environment.NewLine);
                                var keepLines = lines.Skip(lines.Length / 2).ToArray();
                                _logTextBox.Text = string.Join(Environment.NewLine, keepLines);
                            }
                            
                            // 스크롤을 맨 아래로
                            _logTextBox.CaretIndex = _logTextBox.Text?.Length ?? 0;
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
            
            // 중복 로그 방지 (1초 이내 같은 메시지 무시)
            if (_lastLogMessage == message && DateTime.Now - _lastLogTime < TimeSpan.FromSeconds(1))
            {
                return;
            }
            
            _lastLogMessage = message;
            _lastLogTime = DateTime.Now;
            
            // 큐에 추가 (스레드 안전)
            _logQueue.Enqueue(logEntry);
        }
        
        // 정적 메서드로 어디서든 로그 추가 가능
        public static void AddLogStatic(string message)
        {
            _instance?.AddLog(message);
        }
        
        private async Task CopyButton_Click(object? sender, RoutedEventArgs e)
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
