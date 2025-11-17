using System;
using System.IO;
using System.Linq;
using System.Media;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Windows.Media;

namespace TodoListApp
{
    public delegate void NotificationResultEventHandler(object sender, NotificationResultEventArgs e);

    public class NotificationResultEventArgs : EventArgs
    {
        public bool MarkAsCompleted { get; set; }
        public NotificationResultEventArgs(bool markAsCompleted)
        {
            MarkAsCompleted = markAsCompleted;
        }
    }

    public partial class NotificationWindow : Window
    {
        // --- THÊM: Các hằng số cho đường dẫn thư mục ---
        private static readonly string AppDirectory = AppDomain.CurrentDomain.BaseDirectory;
        private static readonly string SoundDirectory = Path.Combine(AppDirectory, "sound");
        private static readonly string ImageDirectory = Path.Combine(AppDirectory, "image");

        private SoundPlayer? _soundPlayer;
        private DispatcherTimer? _rainbowTimer;
        private int _rainbowColorIndex = 0;
        private const int RAINBOW_TRIGGER_SECONDS = 300;
        private readonly Color[] _rainbowColors =
        {
            Colors.Red,
            Colors.Orange,
            Colors.Yellow,
            Colors.Green,
            Colors.Blue,
            Colors.Indigo,
            Colors.Violet
        };

        private string _notificationTitle = string.Empty;
        private string _notificationMessage = string.Empty;
        public event NotificationResultEventHandler? NotificationResult;

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
                _associatedTask = task;

                // --- XỬ LÝ ẢNH NỀN (MỚI) ---
                SetupBackgroundImage();

                SetupRainbowTimer();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[NotificationWindow] Lỗi trong constructor: {ex}");
            }
        }

        // --- THÊM: Hàm xử lý ảnh nền ---
        private void SetupBackgroundImage()
        {
            string savedImagePath = Properties.Settings.Default.NotificationBackgroundImage;
            string finalImagePath = null;

            if (!string.IsNullOrEmpty(savedImagePath))
            {
                // Kiểm tra xem đường dẫn trong Settings có tồn tại không
                // Ưu tiên kiểm tra theo đường dẫn tuyệt đối, sau đó là tương đối so với AppDirectory
                string absolutePath = Path.IsPathRooted(savedImagePath) ? savedImagePath : Path.Combine(AppDirectory, savedImagePath);
                if (File.Exists(absolutePath))
                {
                    finalImagePath = absolutePath;
                }
                // Nếu không tồn tại, có thể file đã bị xóa, bỏ qua
            }

            // Nếu finalImagePath vẫn null (không có ảnh trong Settings hoặc ảnh bị mất)
            if (string.IsNullOrEmpty(finalImagePath))
            {
                // Tìm một tệp ảnh mặc định trong thư mục image
                finalImagePath = FindDefaultImage();
            }

            // Nếu tìm thấy ảnh (từ Settings hoặc mặc định), gán nó
            if (!string.IsNullOrEmpty(finalImagePath))
            {
                // Chuyển đường dẫn tuyệt đối thành tương đối so với AppDirectory để binding
                BackgroundImagePath = MakeRelativePath(AppDirectory, finalImagePath);
                this.DataContext = this;
            }
            else
            {
                // Nếu không tìm thấy ảnh nào, không gán DataContext, giữ nguyên giao diện mặc định
                this.DataContext = this;
            }
        }

        // --- THÊM: Hàm tìm ảnh mặc định ---
        private string? FindDefaultImage()
        {
            if (!Directory.Exists(ImageDirectory))
            {
                System.Diagnostics.Debug.WriteLine($"[NotificationWindow] Thư mục ảnh không tồn tại: {ImageDirectory}");
                return null;
            }

            var imageFiles = Directory.GetFiles(ImageDirectory, "*.*", SearchOption.TopDirectoryOnly)
                                      .Where(f => IsImageFile(f))
                                      .ToArray();

            if (imageFiles.Length > 0)
            {
                // Trả về tệp đầu tiên tìm được
                return imageFiles[0]; // Trả về đường dẫn tuyệt đối
            }

            return null;
        }

        // --- THÊM: Hàm kiểm tra file là ảnh ---
        private static bool IsImageFile(string filePath)
        {
            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            return extension == ".jpg" || extension == ".jpeg" || extension == ".png" || extension == ".bmp" || extension == ".gif";
        }

        // --- THÊM: Hàm tạo đường dẫn tương đối ---
        private static string MakeRelativePath(string fromPath, string toPath)
        {
            var fromUri = new Uri(fromPath);
            var toUri = new Uri(toPath);

            if (fromUri.Scheme != toUri.Scheme) { return toPath; } // Các ổ đĩa khác nhau

            var relativeUri = fromUri.MakeRelativeUri(toUri);
            return Uri.UnescapeDataString(relativeUri.ToString()).Replace('/', Path.DirectorySeparatorChar);
        }

        // --- SỬA: Hàm phát âm thanh (MỚI) ---
        private void PlayNotificationSound()
        {
            string soundPath = Properties.Settings.Default.NotificationSound;
            string finalSoundPath = null;

            if (!string.IsNullOrEmpty(soundPath))
            {
                // Kiểm tra xem đường dẫn trong Settings có tồn tại không
                string absolutePath = Path.IsPathRooted(soundPath) ? soundPath : Path.Combine(AppDirectory, soundPath);
                if (File.Exists(absolutePath))
                {
                    finalSoundPath = absolutePath;
                }
            }

            // Nếu finalSoundPath vẫn null (không có âm thanh trong Settings hoặc bị mất)
            if (string.IsNullOrEmpty(finalSoundPath))
            {
                // Tìm một tệp âm thanh mặc định trong thư mục sound
                finalSoundPath = FindDefaultSound();
            }

            // Nếu tìm thấy âm thanh (từ Settings hoặc mặc định), phát nó
            if (!string.IsNullOrEmpty(finalSoundPath) && File.Exists(finalSoundPath))
            {
                try
                {
                    _soundPlayer = new SoundPlayer(finalSoundPath);
                    _soundPlayer.PlayLooping(); // Hoặc Play() nếu chỉ muốn phát một lần
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[NotificationWindow] Lỗi phát âm thanh: {ex.Message}");
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[NotificationWindow] Không tìm thấy tệp âm thanh nào để phát.");
            }
        }

        // --- THÊM: Hàm tìm âm thanh mặc định ---
        private string? FindDefaultSound()
        {
            if (!Directory.Exists(SoundDirectory))
            {
                System.Diagnostics.Debug.WriteLine($"[NotificationWindow] Thư mục âm thanh không tồn tại: {SoundDirectory}");
                return null;
            }

            var soundFiles = Directory.GetFiles(SoundDirectory, "*.*", SearchOption.TopDirectoryOnly)
                                      .Where(f => IsSoundFile(f))
                                      .ToArray();

            if (soundFiles.Length > 0)
            {
                // Trả về tệp đầu tiên tìm được
                return soundFiles[0]; // Trả về đường dẫn tuyệt đối
            }

            return null;
        }

        // --- THÊM: Hàm kiểm tra file là âm thanh ---
        private static bool IsSoundFile(string filePath)
        {
            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            return extension == ".wav" || extension == ".mp3" || extension == ".wma" || extension == ".ogg" || extension == ".flac";
        }

        private void SetupRainbowTimer()
        {
            if (_rainbowTimer == null)
            {
                _rainbowTimer = new DispatcherTimer();
                _rainbowTimer.Interval = TimeSpan.FromSeconds(RAINBOW_TRIGGER_SECONDS); // 5 phút
                _rainbowTimer.Tick += (s, e) =>
                {
                    _rainbowTimer.Stop(); // Dừng timer 5 phút
                    StartRainbowEffect();  // Bắt đầu hiệu ứng cầu vồng
                };
                _rainbowTimer.Start();
            }
        }

        private void StartRainbowEffect()
        {
            _rainbowTimer?.Stop();
            _rainbowTimer = new DispatcherTimer();
            _rainbowTimer.Interval = TimeSpan.FromMilliseconds(500); // Đổi màu mỗi 0.5 giây, có thể điều chỉnh
            _rainbowTimer.Tick += (s, e) =>
            {
                var currentColor = _rainbowColors[_rainbowColorIndex];
                var brush = new SolidColorBrush(currentColor);

                TitleTextBlock.Foreground = brush;
                MessageTextBlock.Foreground = brush;

                _rainbowColorIndex = (_rainbowColorIndex + 1) % _rainbowColors.Length;
            };
            _rainbowTimer.Start();
        }

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

                PlayNotificationSound();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[NotificationWindow] Lỗi trong NotificationWindow_Loaded: {ex}");
            }
        }

        private void StopSound()
        {
            try
            {
                _soundPlayer?.Stop();
                _soundPlayer?.Dispose();
                _soundPlayer = null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[NotificationWindow] Lỗi khi dừng âm thanh: {ex.Message}");
            }
        }

        private void PostponeButton_Click(object sender, RoutedEventArgs e)
        {
            StopSound(); // Dừng âm thanh
            StopRainbowTimer(); // Dừng hiệu ứng cầu vồng khi người dùng tương tác
            NotificationResult?.Invoke(this, new NotificationResultEventArgs(false));
            Close();
        }

        private void CompleteTaskButton_Click(object sender, RoutedEventArgs e)
        {
            StopSound(); // Dừng âm thanh
            StopRainbowTimer(); // Dừng hiệu ứng cầu vồng khi người dùng tương tác
            NotificationResult?.Invoke(this, new NotificationResultEventArgs(true));
            Close();
        }

        private void StopRainbowTimer()
        {
            _rainbowTimer?.Stop();
            // Trả màu về mặc định khi đóng
            TitleTextBlock.Foreground = SystemColors.ControlTextBrush;
            MessageTextBlock.Foreground = SystemColors.ControlTextBrush;
        }

        protected override void OnClosed(EventArgs e)
        {
            StopRainbowTimer();
            StopSound(); // Gọi StopSound để đảm bảo dọn dẹp âm thanh
            base.OnClosed(e);
        }

        // --- THÊM: Hàm sao chép và lưu âm thanh mới ---
        public static void SaveSelectedSound(string selectedFilePath)
        {
            if (!File.Exists(selectedFilePath))
            {
                System.Diagnostics.Debug.WriteLine($"[NotificationWindow] Tệp âm thanh không tồn tại: {selectedFilePath}");
                return;
            }

            try
            {
                // Tạo thư mục nếu chưa tồn tại
                Directory.CreateDirectory(SoundDirectory);

                // Lấy tên tệp gốc
                string fileName = Path.GetFileName(selectedFilePath);

                // Tạo đường dẫn đích trong thư mục sound
                string destinationPath = Path.Combine(SoundDirectory, fileName);

                // Nếu tên tệp đã tồn tại, thêm số vào cuối
                int counter = 1;
                string fileNameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
                string fileExtension = Path.GetExtension(fileName);
                while (File.Exists(destinationPath))
                {
                    fileName = $"{fileNameWithoutExt}_{counter}{fileExtension}";
                    destinationPath = Path.Combine(SoundDirectory, fileName);
                    counter++;
                }

                // Sao chép tệp
                File.Copy(selectedFilePath, destinationPath, overwrite: false);

                // Tạo đường dẫn tương đối để lưu vào Settings
                string relativePath = MakeRelativePath(AppDirectory, destinationPath);

                // Lưu đường dẫn tương đối vào Settings
                Properties.Settings.Default.NotificationSound = relativePath;
                Properties.Settings.Default.Save();

                System.Diagnostics.Debug.WriteLine($"[NotificationWindow] Đã sao chép âm thanh vào: {destinationPath}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[NotificationWindow] Lỗi khi sao chép âm thanh: {ex.Message}");
            }
        }

        // --- THÊM: Hàm sao chép và lưu ảnh mới ---
        public static void SaveSelectedImage(string selectedFilePath)
        {
            if (!File.Exists(selectedFilePath))
            {
                System.Diagnostics.Debug.WriteLine($"[NotificationWindow] Tệp ảnh không tồn tại: {selectedFilePath}");
                return;
            }

            try
            {
                // Tạo thư mục nếu chưa tồn tại
                Directory.CreateDirectory(ImageDirectory);

                // Lấy tên tệp gốc
                string fileName = Path.GetFileName(selectedFilePath);

                // Tạo đường dẫn đích trong thư mục image
                string destinationPath = Path.Combine(ImageDirectory, fileName);

                // Nếu tên tệp đã tồn tại, thêm số vào cuối
                int counter = 1;
                string fileNameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
                string fileExtension = Path.GetExtension(fileName);
                while (File.Exists(destinationPath))
                {
                    fileName = $"{fileNameWithoutExt}_{counter}{fileExtension}";
                    destinationPath = Path.Combine(ImageDirectory, fileName);
                    counter++;
                }

                // Sao chép tệp
                File.Copy(selectedFilePath, destinationPath, overwrite: false);

                // Tạo đường dẫn tương đối để lưu vào Settings
                string relativePath = MakeRelativePath(AppDirectory, destinationPath);

                // Lưu đường dẫn tương đối vào Settings
                Properties.Settings.Default.NotificationBackgroundImage = relativePath;
                Properties.Settings.Default.Save();

                System.Diagnostics.Debug.WriteLine($"[NotificationWindow] Đã sao chép ảnh vào: {destinationPath}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[NotificationWindow] Lỗi khi sao chép ảnh: {ex.Message}");
            }
        }

        private void CustomPostponeButton_Click(object sender, RoutedEventArgs e)
        {
            if (_associatedTask == null || !_associatedTask.Deadline.HasValue)
            {
                MessageBox.Show("Task này không có deadline, không thể đặt nhắc lại tùy chỉnh.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Warning);
                Close();
                return;
            }

            var now = DateTime.Now;
            var deadline = _associatedTask.Deadline.Value;

            var totalMinutesUntilDeadline = (int)(deadline - now).TotalMinutes - 1;

            if (totalMinutesUntilDeadline < 1)
            {
                MessageBox.Show("Thời gian còn lại đến deadline quá ngắn để đặt nhắc lại.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Warning);
                Close();
                return;
            }

            int maxMinutes = Math.Min(totalMinutesUntilDeadline, 60);

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
                    _associatedTask.ReminderTime = now.AddMinutes(minutes);
                    _associatedTask.UpdatedDate = DateTime.Now;
                    try
                    {
                        var dbService = new DatabaseService();
                        dbService.UpdateTask(_associatedTask);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Lỗi khi lưu nhắc lại: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
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