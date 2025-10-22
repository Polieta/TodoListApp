using System;
using System.IO;
using System.Media;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media.Imaging; // Thêm namespace cho BitmapImage
using System.Windows.Threading;
using System.Linq;

namespace TodoListApp
{
    // Định nghĩa delegate cho sự kiện tùy chỉnh
    public delegate void NotificationResultEventHandler(object sender, NotificationResultEventArgs e);

    // Lớp đối số cho sự kiện
    public class NotificationResultEventArgs : EventArgs
    {
        public bool MarkAsCompleted { get; set; } // true nếu nhấn "Chuyển trạng thái", false nếu "Từ từ"
        public NotificationResultEventArgs(bool markAsCompleted)
        {
            MarkAsCompleted = markAsCompleted;
        }
    }

    public partial class NotificationWindow : Window
    {
        private SoundPlayer? _soundPlayer;
        // Xóa SpeechSynthesizer và các phương thức liên quan
        private DispatcherTimer? _autoCloseTimer;
        private const int AUTO_CLOSE_SECONDS = 120;

        private string _notificationTitle = string.Empty;
        private string _notificationMessage = string.Empty;
        public event NotificationResultEventHandler? NotificationResult;

        // Thêm thuộc tính để binding ảnh nền
        public string? BackgroundImagePath { get; set; }

        public NotificationWindow(string title, string message, string icon = "🔔")
        {
            try
            {
                InitializeComponent();
                TitleTextBlock.Text = title;
                MessageTextBlock.Text = message;
                IconTextBlock.Text = icon;
                _notificationTitle = title;
                _notificationMessage = message;
                SetupAutoCloseTimer();

                // --- LẤY ĐƯỜNG DẪN ẢNH TỪ SETTINGS ---
                string savedImagePath = Properties.Settings.Default.NotificationBackgroundImage;
                if (!string.IsNullOrEmpty(savedImagePath) && File.Exists(savedImagePath))
                {
                    BackgroundImagePath = savedImagePath;
                    // DataContext là chính cửa sổ để binding hoạt động
                    this.DataContext = this;
                }
                else
                {
                    // Nếu không có ảnh, có thể giữ lại nền động hoặc để trống
                    // Ở đây, để đơn giản, ta không làm gì cả nếu không có ảnh.
                    this.DataContext = this;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[NotificationWindow] Lỗi trong constructor: {ex}");
            }
        }

        // Event handler cho sự kiện Loaded để đặt vị trí cửa sổ
        private void NotificationWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                var workArea = SystemParameters.WorkArea;
                double windowWidth = this.Width;
                double windowHeight = this.Height;

                if (!double.IsNaN(windowWidth) && !double.IsNaN(windowHeight) &&
                    windowWidth > 0 && windowHeight > 0)
                {
                    this.Left = Math.Max(workArea.Left, workArea.Right - windowWidth);
                    this.Top = Math.Max(workArea.Top, workArea.Bottom - windowHeight);
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("[NotificationWindow] Cảnh báo: Width hoặc Height không hợp lệ trong sự kiện Loaded.");
                }

                // --- PHÁT ÂM THANH TỪ SETTINGS ---
                string soundPath = Properties.Settings.Default.NotificationSound;
                if (!string.IsNullOrEmpty(soundPath) && File.Exists(soundPath))
                {
                    try
                    {
                        _soundPlayer = new SoundPlayer(soundPath);
                        // Phát âm thanh một cách bất đồng bộ (không chờ kết thúc)
                        _soundPlayer.PlayLooping(); // Hoặc Play() nếu chỉ muốn phát một lần
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[NotificationWindow] Lỗi phát âm thanh: {ex.Message}");
                    }
                }
                // Ghi chú: Tạm khóa TTS như yêu cầu
                // SpeakNotification(_notificationTitle, _notificationMessage);

            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[NotificationWindow] Lỗi trong NotificationWindow_Loaded: {ex}");
            }
        }

        // Xóa phương thức SpeakNotification và StopSpeech

        private void StopSound()
        {
            try
            {
                // Dừng phát âm thanh nếu đang chạy
                _soundPlayer?.Stop();
                _soundPlayer?.Dispose();
                _soundPlayer = null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[NotificationWindow] Lỗi khi dừng âm thanh: {ex.Message}");
            }
        }

        private void SetupAutoCloseTimer()
        {
            if (_autoCloseTimer == null)
            {
                _autoCloseTimer = new DispatcherTimer();
                _autoCloseTimer.Interval = TimeSpan.FromSeconds(AUTO_CLOSE_SECONDS);
                _autoCloseTimer.Tick += (s, e) =>
                {
                    _autoCloseTimer.Stop();
                    NotificationResult?.Invoke(this, new NotificationResultEventArgs(false));
                    Close();
                };
                _autoCloseTimer.Start();
            }
        }

        private void PostponeButton_Click(object sender, RoutedEventArgs e)
        {
            StopSound(); // Dừng âm thanh
            _autoCloseTimer?.Stop();
            NotificationResult?.Invoke(this, new NotificationResultEventArgs(false));
            Close();
        }

        private void CompleteTaskButton_Click(object sender, RoutedEventArgs e)
        {
            StopSound(); // Dừng âm thanh
            _autoCloseTimer?.Stop();
            NotificationResult?.Invoke(this, new NotificationResultEventArgs(true));
            Close();
        }

        protected override void OnClosed(EventArgs e)
        {
            _autoCloseTimer?.Stop();
            StopSound(); // Gọi StopSound để đảm bảo dọn dẹp âm thanh
            base.OnClosed(e);
        }

        private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            this.DragMove();
        }

        // Các phương thức Create... giữ nguyên
        public static NotificationWindow CreateWarning(string title, string message)
        {
            return new NotificationWindow(title, message, "⚠️");
        }

        public static NotificationWindow CreateSuccess(string title, string message)
        {
            return new NotificationWindow(title, message, "✅");
        }

        public static NotificationWindow CreateError(string title, string message)
        {
            return new NotificationWindow(title, message, "❌");
        }

        public static NotificationWindow CreateReminder(string title, string message)
        {
            return new NotificationWindow(title, message, "⏰");
        }
    }
}