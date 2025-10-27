using System;
using System.IO;
using System.Linq;
using System.Media;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging; // Thêm namespace cho BitmapImage
using System.Windows.Threading;

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

        private TodoTask? _associatedTask;

        public NotificationWindow(string title, string message, string icon = "🔔", TodoTask? task = null)
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
                _associatedTask = task;

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

        private void CustomPostponeButton_Click(object sender, RoutedEventArgs e)
        {
            // Đảm bảo task hợp lệ
            if (_associatedTask == null || !_associatedTask.Deadline.HasValue)
            {
                MessageBox.Show("Task này không có deadline, không thể đặt nhắc lại tùy chỉnh.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Warning);
                Close();
                return;
            }

            var now = DateTime.Now;
            var deadline = _associatedTask.Deadline.Value;

            // Tính số phút tối đa có thể chọn: (deadline - now) - 1 phút
            var totalMinutesUntilDeadline = (int)(deadline - now).TotalMinutes - 1;

            if (totalMinutesUntilDeadline < 1)
            {
                MessageBox.Show("Thời gian còn lại đến deadline quá ngắn để đặt nhắc lại.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Warning);
                Close();
                return;
            }

            // Giới hạn tối đa 60 phút để tránh nhập số quá lớn
            int maxMinutes = Math.Min(totalMinutesUntilDeadline, 60);

            // Hiển thị hộp thoại nhập phút
            var inputDialog = new Window
            {
                Title = "Nhắc lại sau...",
                Width = 300,
                Height = 180,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                ResizeMode = ResizeMode.NoResize,
                Topmost = true
            };

            var stack = new StackPanel { Margin = new Thickness(15) };
            stack.Children.Add(new TextBlock { Text = $"Nhập số phút (1–{maxMinutes}):", Margin = new Thickness(0, 0, 0, 10) });

            var textBox = new TextBox
            {
                Width = 200,
                Height = 30,
                HorizontalAlignment = HorizontalAlignment.Left
            };
            stack.Children.Add(textBox);

            var buttonPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 20, 0, 0) };
            var okButton = new Button { Content = "OK", Width = 80, Margin = new Thickness(0, 0, 10, 0) };
            var cancelButton = new Button { Content = "Hủy", Width = 80 };

            okButton.Click += (s, ev) =>
            {
                if (int.TryParse(textBox.Text, out int minutes) && minutes >= 1 && minutes <= maxMinutes)
                {
                    // ✅ Cập nhật ReminderTime
                    _associatedTask.ReminderTime = now.AddMinutes(minutes);
                    _associatedTask.UpdatedDate = DateTime.Now;

                    // Lưu vào DB (giả sử bạn có cách truy cập DatabaseService)
                    try
                    {
                        var dbService = new DatabaseService(); // Hoặc inject từ MainWindow
                        dbService.UpdateTask(_associatedTask);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Lỗi khi lưu nhắc lại: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                    }

                    // Gửi tín hiệu đóng thông báo (không hoàn thành task)
                    NotificationResult?.Invoke(this, new NotificationResultEventArgs(false));
                    inputDialog.Close();
                    Close();
                }
                else
                {
                    MessageBox.Show($"Vui lòng nhập số nguyên từ 1 đến {maxMinutes}.", "Giá trị không hợp lệ", MessageBoxButton.OK, MessageBoxImage.Warning);
                    textBox.Focus();
                }
            };

            cancelButton.Click += (s, ev) => inputDialog.Close();

            buttonPanel.Children.Add(okButton);
            buttonPanel.Children.Add(cancelButton);
            stack.Children.Add(buttonPanel);
            inputDialog.Content = stack;
            inputDialog.ShowDialog();
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