using Hardcodet.Wpf.TaskbarNotification;
using System.Windows;

namespace TodoListApp
{
    public partial class App : Application
    {
        public ReminderService? _reminderService;
        private MainWindow? _mainWindow; // Giữ tham chiếu

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Đăng ký xử lý exception chưa được bắt trên UI Thread (Tùy chọn nhưng rất hữu ích)
            this.DispatcherUnhandledException += App_DispatcherUnhandledException;

            // Khởi tạo ReminderService
            _reminderService = new ReminderService();
            // ReminderService sẽ kích hoạt sự kiện OnReminderTriggered khi tới thời điểm nhắc nhở
            // App sẽ lắng nghe sự kiện này và yêu cầu MainWindow hiển thị thông báo

            // Hiển thị MainWindow khi khởi động
            ShowMainWindow();
        }

        // Phương thức xử lý sự kiện từ ReminderService
        private void ShowNotification(string title, string message, TodoTask task)
        {
            // Không gọi _reminderService?.PlayNotificationSound(); ở đây nữa
            // Logic phát âm thanh sẽ do MainWindow xử lý để tuân thủ Phương án 1

            // Sử dụng Dispatcher để đảm bảo code chạy trên UI Thread
            this.Dispatcher.Invoke(() =>
            {
                if (_mainWindow != null)
                {
                    // Gọi phương thức xử lý trên MainWindow
                    _mainWindow.HandleReminderTriggered(title, message, task);
                }
                else
                {
                    // Trường hợp hiếm: MainWindow không tồn tại
                    System.Diagnostics.Debug.WriteLine("[App] MainWindow is null in ShowNotification.");
                }
            });
        }

        public void ShowMainWindow()
        {
            if (_mainWindow == null)
            {
                // Tạo MainWindow và truyền ReminderService vào
                _mainWindow = new MainWindow(_reminderService);
                // Đăng ký sự kiện OnReminderTriggered của ReminderService với phương thức ShowNotification của App
                if (_reminderService != null)
                {
                    _reminderService.OnReminderTriggered += ShowNotification;
                }
                // Gán event handler để xử lý khi MainWindow bị đóng
                _mainWindow.Closed += (sender, args) =>
                {
                    // Khi MainWindow bị đóng (ẩn), đặt lại tham chiếu
                    _mainWindow = null;
                };
            }
            _mainWindow.Show();
            _mainWindow.Activate();
        }

        public void HideMainWindow()
        {
            _mainWindow?.Hide();
        }

        // Phương thức thoát ứng dụng hoàn toàn
        public void ExitApplication()
        {
            // Dừng dịch vụ nhắc nhở
            _reminderService?.Stop();

            // Đóng cửa sổ chính nếu có
            _mainWindow?.Close(); // Điều này sẽ kích hoạt OnClosing và Closed

            // Shutdown() sẽ dừng ứng dụng hoàn toàn
            Shutdown();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _reminderService?.Stop();
            base.OnExit(e);
        }

        // Xử lý exception chưa được bắt (Tùy chọn)
        private void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            // Ghi log lỗi chi tiết vào Output Window
            System.Diagnostics.Debug.WriteLine($"[App] Unhandled UI Exception: {e.Exception}");
            // Có thể ghi log vào file
            // System.IO.File.AppendAllText("app_error.log", $"[{DateTime.Now}] [App] Unhandled UI Exception: {e.Exception}\n");

            // Quan trọng: Nếu không set e.Handled = true, ứng dụng sẽ tắt.
            // Nếu set e.Handled = true, ứng dụng sẽ tiếp tục chạy (có thể không ổn định).
            // Tốt nhất là ghi log và để ứng dụng tắt.
            // e.Handled = true; // <-- CHỈ nên set nếu bạn *rất chắc chắn* có thể tiếp tục
        }
    }
}