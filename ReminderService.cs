using System;
using System.Collections.Generic;
using System.Linq;
using System.Timers; // Sử dụng System.Timers.Timer như yêu cầu
using System.Windows;
using System.Media;
using System.IO;
using System.Windows.Media;

namespace TodoListApp
{
    public class ReminderService
    {
        private System.Timers.Timer _timer; // Sử dụng System.Timers.Timer
        private MediaPlayer _activeNotificationPlayer = null;

        // --- CẬP NHẬT: Sử dụng Dictionary<int, string> để theo dõi các loại thông báo đã gửi trong ngày ---
        // Key: TaskId
        // Value: Chuỗi chứa các ký tự đại diện cho loại thông báo đã gửi ('R' cho Reminder, 'D' cho Deadline)
        private Dictionary<int, string> _notifiedTasksToday = new Dictionary<int, string>();
        // --- HẾT CẬP NHẬT ---

        // Sự kiện để MainWindow xử lý hiển thị thông báo
        public event Action<string, string, TodoTask> OnReminderTriggered;

        public ReminderService()
        {
            // Giảm khoảng thời gian kiểm tra xuống 5 giây để tăng độ chính xác
            _timer = new System.Timers.Timer(5000); // Kiểm tra mỗi 5 giây
            _timer.Elapsed += Timer_Elapsed;
            _timer.AutoReset = true;
            _timer.Start();
        }

        private void Timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            CheckReminders();
        }

        private void CheckReminders()
        {
            try
            {
                var now = DateTime.Now;
                var startOfDay = now.Date; // 00:00:00 hôm nay
                var endOfDay = startOfDay.AddDays(1).AddTicks(-1);
                var todaysTasks = DatabaseService.GetTodaysInProgressTasks(); // Gọi phương thức tĩnh hoặc thông qua instance nếu cần

                // --- CẬP NHẬT: Dọn dẹp _notifiedTasksToday khi sang ngày mới ---
                // Nếu đã qua nửa đêm (00:00:00), dọn dẹp danh sách
                if (_notifiedTasksToday.Any() && now.TimeOfDay < TimeSpan.FromSeconds(10))
                {
                    _notifiedTasksToday.Clear();
                }
                // --- HẾT CẬP NHẬT ---

                foreach (var task in todaysTasks)
                {
                    // --- KIỂM TRA NHẮC NHỞ (ReminderTime) ---
                    if (task.ReminderEnabled && task.ReminderTime.HasValue)
                    {
                        var reminderTime = task.ReminderTime.Value;

                        // --- CẬP NHẬT: Kiểm tra nếu GIỜ và PHÚT của ReminderTime trùng với GIỜ và PHÚT của now ---
                        // Ví dụ: now = 10:30:45, reminderTime = 10:30:00 -> Hour=10, Minute=30 -> True
                        // Ví dụ: now = 10:30:45, reminderTime = 10:31:00 -> Hour=10, Minute=31 -> False
                        if (reminderTime.Hour == now.Hour && reminderTime.Minute == now.Minute)
                        {
                            // --- CẬP NHẬT: Kiểm tra xem thông báo Reminder cho task này đã được gửi chưa trong ngày hôm nay ---
                            // Kiểm tra nếu task.Id chưa có trong dictionary HOẶC nếu đã có nhưng chưa được đánh dấu là đã gửi Reminder ('R')
                            if (!_notifiedTasksToday.ContainsKey(task.Id) ||
                                !_notifiedTasksToday[task.Id].Contains("R"))
                            {
                                OnReminderTriggered?.Invoke(
                                    $"🔔 Nhắc nhở: {task.Title}",
                                    $"Thời gian: {reminderTime:dd/MM/yyyy HH:mm}",
                                    task
                                );
                                // --- CẬP NHẬT: Đánh dấu đã thông báo Reminder ---
                                // Nếu task.Id chưa có trong dictionary, thêm mới với chuỗi rỗng
                                if (!_notifiedTasksToday.ContainsKey(task.Id))
                                    _notifiedTasksToday[task.Id] = "";
                                // Thêm ký tự 'R' vào chuỗi để đánh dấu đã gửi Reminder
                                _notifiedTasksToday[task.Id] += "R";
                                // --- HẾT CẬP NHẬT ---
                            }
                        }
                    }
                    // --- HẾT KIỂM TRA NHẮC NHỞ ---

                    // --- KIỂM TRA DEADLINE ---
                    if (task.Deadline.HasValue)
                    {
                        var deadline = task.Deadline.Value;

                        // --- CẬP NHẬT: Kiểm tra nếu GIỜ và PHÚT của Deadline trùng với GIỜ và PHÚT của now ---
                        if (deadline.Hour == now.Hour && deadline.Minute == now.Minute)
                        {
                            // --- CẬP NHẬT: Kiểm tra xem thông báo Deadline cho task này đã được gửi chưa trong ngày hôm nay ---
                            // (Tránh gửi cả Reminder và Deadline nếu trùng thời gian)
                            // Kiểm tra nếu task.Id chưa có trong dictionary HOẶC nếu đã có nhưng chưa được đánh dấu là đã gửi Deadline ('D')
                            if (!_notifiedTasksToday.ContainsKey(task.Id) ||
                                !_notifiedTasksToday[task.Id].Contains("D"))
                            {
                                OnReminderTriggered?.Invoke(
                                    $"⏰ Deadline: {task.Title}",
                                    $"Đã đến hạn: {deadline:dd/MM/yyyy HH:mm}",
                                    task
                                );
                                // --- CẬP NHẬT: Đánh dấu đã thông báo Deadline ---
                                // Nếu task.Id chưa có trong dictionary, thêm mới với chuỗi rỗng
                                if (!_notifiedTasksToday.ContainsKey(task.Id))
                                    _notifiedTasksToday[task.Id] = "";
                                // Thêm ký tự 'D' vào chuỗi để đánh dấu đã gửi Deadline
                                _notifiedTasksToday[task.Id] += "D";
                                // --- HẾT CẬP NHẬT ---
                            }
                        }
                    }
                    // --- HẾT KIỂM TRA DEADLINE ---
                }

            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ReminderService] Lỗi trong CheckReminders: {ex.Message}");
                // Có thể log lỗi ở đây
            }
        }

        public void Stop()
        {
            _timer?.Stop();
            StopActiveNotificationSound();
        }

        public void PlayNotificationSound()
        {
            string soundPath = Properties.Settings.Default.NotificationSound;
            if (!string.IsNullOrEmpty(soundPath) && File.Exists(soundPath))
            {
                try
                {
                    // Sử dụng SoundPlayer hoặc cách khác để phát âm thanh
                    // Ví dụ:
                    System.Media.SoundPlayer player = new System.Media.SoundPlayer(soundPath);
                    player.Play();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Lỗi phát âm thanh từ ReminderService: {ex.Message}");
                }
            }
            else
            {
                soundPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Sounds", "alert.wav");
            }
        }
        public void StopActiveNotificationSound()
        {
            if (_activeNotificationPlayer != null)
            {
                try
                {
                    _activeNotificationPlayer.Stop();
                    _activeNotificationPlayer.Close();
                }
                catch { }
                finally
                {
                    _activeNotificationPlayer = null;
                }
            }
        }
    }
}