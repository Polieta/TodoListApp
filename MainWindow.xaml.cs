using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using System.Xml.Linq;


namespace TodoListApp
{


    public partial class MainWindow : Window
    {

        private DatabaseService _databaseService;
        public ObservableCollection<TodoTask> _inProgressTasks;
        private ObservableCollection<TodoTask> _completedTasks;
        private ReminderService _reminderService;
        private bool _isFormattingTextBox = false;

        private int _inProgressCurrentPage = 1;
        private int _completedCurrentPage = 1;
        private const int PageSize = 8;

        private ICollectionView _inProgressView;
        private ICollectionView _completedView;

        private Predicate<object> _originalInProgressFilter;
        private Predicate<object> _originalCompletedFilter;
        

        private TodoTask? _draggedTask;
        private Point _startPoint;
        private bool _isDragging;

        private DispatcherTimer _startupTimer;

        private string _currentSearchTerm = string.Empty;
        private DateTime? _filteredDate = null;

        public object NetSparkleAppConfig { get; private set; }

        public MainWindow(ReminderService reminderService) : this()
        {
            _reminderService = reminderService ?? throw new ArgumentNullException(nameof(reminderService));
            _reminderService.OnReminderTriggered += HandleReminderTriggered;
        }
        public MainWindow() : base()
        {
            InitializeComponent();
            InitializeData();
            LoadTasks();

            _startupTimer = new DispatcherTimer(DispatcherPriority.Background);
            _startupTimer.Interval = TimeSpan.FromMilliseconds(500); // Chờ một chút sau khi window loaded
            _startupTimer.Tick += StartupTimer_Tick;
        }
        private void TaskCard_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            var border = sender as Border;
            _draggedTask = border?.DataContext as TodoTask;
            _startPoint = e.GetPosition(null);
            _isDragging = false;
        }

        private void SetupSorting(ICollectionView view)
        {
            if (view == null) return;

            // Xóa các SortDescription cũ (nếu có)
            view.SortDescriptions.Clear();

            // 1. Sắp xếp theo ngày của Deadline/ReminderTime (sớm nhất trước)
            //    Nếu task không có deadline/reminder, đưa về cuối (sắp xếp theo ngày rất xa hoặc null cuối cùng)
            var dateSortDesc = new SortDescription("Deadline", ListSortDirection.Ascending);
            view.SortDescriptions.Add(dateSortDesc);

            // 2. Trong cùng một ngày, sắp xếp theo giờ (sớm nhất trước)
            var timeSortDesc = new SortDescription("DeadlineTimeOnly", ListSortDirection.Ascending); // Cần property riêng cho giờ
            view.SortDescriptions.Add(timeSortDesc);

            // 3. Nếu cùng ngày và giờ, ưu tiên theo mức độ ưu tiên (Cao -> Thấp)
            var prioritySortDesc = new SortDescription("Priority", ListSortDirection.Descending); // Giả sử 2=Cao, 1=TB, 0=Thấp
            view.SortDescriptions.Add(prioritySortDesc);

            // 4. Cuối cùng, nếu mọi thứ đều giống nhau, sắp xếp theo tiêu đề (tùy chọn)
            var titleSortDesc = new SortDescription("Title", ListSortDirection.Ascending);
            view.SortDescriptions.Add(titleSortDesc);
        }

        private void InitializeData()
        {
            _databaseService = new DatabaseService();

            // 1. Khởi tạo ObservableCollection trước
            _inProgressTasks = new ObservableCollection<TodoTask>();
            _completedTasks = new ObservableCollection<TodoTask>();

            // 2. Khởi tạo ICollectionView từ ObservableCollection
            _inProgressView = CollectionViewSource.GetDefaultView(_inProgressTasks);
            _completedView = CollectionViewSource.GetDefaultView(_completedTasks);

            // 3. Thiết lập sắp xếp mặc định cho cả hai view
            SetupSorting(_inProgressView);
            SetupSorting(_completedView);

            // 4. Gán ItemsSource cho ListBox
            InProgressTasksList.ItemsSource = _inProgressView;
            CompletedTasksList.ItemsSource = _completedView;

            // 5. Lưu filter gốc (ban đầu không có filter đặc biệt ngoài phân trang)
            // Chúng ta sẽ lưu logic phân trang vào một biến có thể gọi lại
            _originalInProgressFilter = null; // Hoặc lưu delegate nếu cần
            _originalCompletedFilter = null;
        }
        public void HandleReminderTriggered(string title, string message, TodoTask task)
        {
            // --- SỬA LỖI: Tìm lại task trong danh sách của MainWindow ---
            var taskInMainWindow = _inProgressTasks.FirstOrDefault(t => t.Id == task.Id);
            if (taskInMainWindow == null)
            {
                // Nếu không tìm thấy trong InProgress, kiểm tra trong Completed (ít phổ biến hơn)
                taskInMainWindow = _completedTasks.FirstOrDefault(t => t.Id == task.Id);
            }

            // Nếu vẫn không tìm thấy, có thể task đã bị xóa hoặc chưa được tải vào danh sách
            if (taskInMainWindow == null)
            {
                System.Diagnostics.Debug.WriteLine($"[HandleReminderTriggered] Không tìm thấy task với Id {task.Id} trong danh sách của MainWindow.");
                return; // Dừng xử lý
            }

            //_reminderService?.PlayNotificationSound();
            ShowTaskNotification(title, message, taskInMainWindow); // <-- Dùng taskInMainWindow

            if (taskInMainWindow != null && taskInMainWindow.Status != TaskStatus.Completed)
            {
                var now = DateTime.Now;
                bool isDeadline = taskInMainWindow.Deadline.HasValue &&
                                  taskInMainWindow.Deadline.Value <= now.AddSeconds(10) &&
                                  taskInMainWindow.Deadline.Value >= now.AddSeconds(-10);

                if (taskInMainWindow.IsRepeating && taskInMainWindow.RepeatFrequency != RepeatFrequency.None)
                {
                    // Nếu task là lặp lại
                    if (isDeadline)
                    {
                        // Tính ngày lặp lại tiếp theo
                        DateTime? nextRepeatDate = taskInMainWindow.GetNextRepeatDate(now);
                        if (nextRepeatDate.HasValue)
                        {
                            // Cập nhật deadline và reminder cho lần lặp tiếp theo
                            taskInMainWindow.Deadline = nextRepeatDate;
                            if (taskInMainWindow.ReminderEnabled)
                            {
                                int minutesBefore = 5; // Lấy từ cài đặt nếu có
                                if (taskInMainWindow.Deadline.Value.AddMinutes(-minutesBefore) <= now)
                                {
                                    // Nếu thời gian nhắc nhở mới vẫn <= now, có thể đặt lại hoặc tăng phút
                                    taskInMainWindow.ReminderTime = now.AddMinutes(5); // Ví dụ: đặt nhắc nhở sau 5 phút nữa
                                }
                                else
                                {
                                    taskInMainWindow.ReminderTime = taskInMainWindow.Deadline.Value.AddMinutes(-minutesBefore);
                                }
                            }
                            taskInMainWindow.UpdatedDate = DateTime.Now;

                            try
                            {
                                _databaseService.UpdateTask(taskInMainWindow);
                                // Cập nhật UI (nếu task đang ở InProgress)
                                if (taskInMainWindow.Status == TaskStatus.InProgress)
                                {
                                    UpdateInProgressTasksView(); //ICollectionView sẽ tự cập nhật UI
                                }
                                else if (taskInMainWindow.Status == TaskStatus.Completed)
                                {
                                    UpdateCompletedTasksView();
                                }
                                StatusText.Text = $"Task '{taskInMainWindow.Title}' đã được lập lại cho: {taskInMainWindow.Deadline.Value:dd/MM/yyyy HH:mm}";
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"Lỗi cập nhật task lặp lại trong MainWindow: {ex.Message}");
                            }
                        }
                        else
                        {
                            // Nếu không thể tính ngày lặp lại, có thể bỏ qua hoặc xử lý khác
                            System.Diagnostics.Debug.WriteLine($"Không thể tính ngày lặp lại cho task {taskInMainWindow.Id}");
                        }
                    }
                }
                else
                {
                }
            }
        }
        // Trong MainWindow.xaml.cs
        private void ShowTaskNotification(string title, string message, TodoTask? task = null)
        {
            // Sử dụng Dispatcher của MainWindow để đảm bảo an toàn
            this.Dispatcher.Invoke(() =>
            {
                try
                {
                    string fullMessage = $"{message}";
                    if (task != null)
                    {
                        fullMessage += $"\nTiêu đề: {task.Title}"; // \n để xuống dòng
                        if (!string.IsNullOrWhiteSpace(task.Description))
                        {
                            fullMessage += $"\nMô tả: {task.Description}";
                        }
                        if (task.Deadline.HasValue)
                        {
                            fullMessage += $"\nDeadline: {task.Deadline.Value:dd/MM/yyyy HH:mm}";
                        }
                        // Thêm ReminderTime nếu có và khác deadline
                        if (task.ReminderTime.HasValue && (!task.Deadline.HasValue || task.ReminderTime.Value != task.Deadline.Value))
                        {
                            fullMessage += $"\nNhắc nhở lúc: {task.ReminderTime.Value:dd/MM/yyyy HH:mm}";
                        }
                        if (task.IsRepeating)
                        {
                            fullMessage += $"\nLặp lại: {task.RepeatFrequencyText}";
                        }
                    }

                    // *** TẠO NotificationWindow Ở ĐÂY, TRÊN UI THREAD ***
                    var notificationWindow = new NotificationWindow(title, fullMessage, "🔔", task);

                    // *** QUAN TRỌNG: Đăng ký sự kiện NotificationResult ***
                    notificationWindow.NotificationResult += (sender, e) =>
                    {
                        // Dừng âm thanh khi có phản hồi từ người dùng
                        // Đảm bảo rằng _reminderService không null trước khi gọi
                        _reminderService?.StopActiveNotificationSound();

                        if (e.MarkAsCompleted)
                        {
                            // --- NGƯỜI DÙNG CHỌN "CHUYỂN TRẠNG THÁI" ---
                            // Nếu task hợp lệ và chưa hoàn thành
                            if (task != null && task.Status != TaskStatus.Completed)
                            {
                                try
                                {
                                    // Xóa khỏi danh sách Chưa xong
                                    var tasksToRemove = _inProgressTasks.Where(t => t.Id == task.Id).ToList();
                                    foreach (var taskToRemove in tasksToRemove)
                                    {
                                        _inProgressTasks.Remove(taskToRemove);
                                    }
                                    // --- HẾT PHẦN GIỮ NGUYÊN THUỘC TÍNH ---
                                    task.Status = TaskStatus.Completed;
                                    task.ReminderEnabled = false; // Tắt nhắc nhở khi hoàn thành
                                    task.UpdatedDate = DateTime.Now;
                                    // Lưu thay đổi vào DB
                                    _databaseService.UpdateTask(task);
                                    // Thêm vào danh sách Hoàn thành
                                    _completedTasks.Insert(0, task);
                                    // Cập nhật giao diện người dùng
                                    UpdateInProgressTasksView();
                                    UpdateCompletedTasksView();
                                    UpdateTaskCounts();
                                    UpdateReminderStatus();
                                    StatusText.Text = $"Đã hoàn thành task: {task.Title}";
                                }
                                catch (Exception ex)
                                {
                                    System.Diagnostics.Debug.WriteLine($"Lỗi khi chuyển task sang hoàn thành từ thông báo: {ex.Message}");
                                    MessageBox.Show($"Lỗi khi cập nhật task: {ex.Message}", "Lỗi",
                                        MessageBoxButton.OK, MessageBoxImage.Error);
                                }
                            }
                        }
                        else
                        {
                        }
                    };

                    notificationWindow.Topmost = true;
                    notificationWindow.Show();
                }
                catch (Exception ex)
                {
                    // Bắt lỗi nếu có vấn đề khi tạo hoặc hiển thị thông báo trên UI thread
                    System.Diagnostics.Debug.WriteLine($"[MainWindow] Lỗi trong ShowTaskNotification (Dispatcher.Invoke): {ex}");
                    MessageBox.Show($"Lỗi khi hiển thị thông báo: {ex.Message}", "Lỗi Thông Báo", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            });
            // *** HẾT GỌI DISPATCHER ***
        }
        public void ShowTasksForDateOnMain(DateTime dateToFilter)
        {
            _filteredDate = dateToFilter.Date; // Lưu lại ngày đang lọc
            _currentSearchTerm = string.Empty;

            // Cập nhật lại view để hiển thị kết quả
            _inProgressCurrentPage = 1; // Luôn về trang 1 khi áp dụng filter mới
            _completedCurrentPage = 1;
            UpdateInProgressTasksView();
            UpdateCompletedTasksView();
            UpdateTaskCounts();
            StatusText.Text = $"Hiển thị task cho ngày {_filteredDate.Value:dd/MM/yyyy}.";
        }
        private void UpdateInProgressTasksView()
        {
            // Tính toán totalTasks dựa trên bộ lọc hiện tại (search và date)
            int totalTasks;
            if (!string.IsNullOrEmpty(_currentSearchTerm))
            {
                totalTasks = _inProgressTasks.Count(t => IsTaskMatch(t, _currentSearchTerm));
            }
            else if (_filteredDate.HasValue)
            {
                totalTasks = _inProgressTasks.Count(t => t.Deadline.HasValue && t.Deadline.Value.Date == _filteredDate.Value);
            }
            else
            {
                totalTasks = _inProgressTasks.Count;
            }

            var totalPages = (totalTasks + PageSize - 1) / PageSize;
            if (_inProgressCurrentPage > totalPages && totalPages > 0)
                _inProgressCurrentPage = totalPages;
            else if (totalTasks == 0)
                _inProgressCurrentPage = 1;

            var startIndex = (_inProgressCurrentPage - 1) * PageSize;
            var endIndex = startIndex + PageSize - 1; // <-- TÍNH endIndex Ở ĐÂY, TRONG PHẠM VI PHƯƠNG THỨC

            // GẮN LẠI ItemsSource ĐỂ ĐẢM BẢO ListBox bind đến _inProgressView
            InProgressTasksList.ItemsSource = _inProgressView;

            // Quan trọng: Gọi Refresh để áp dụng lại Filter (và Sort nếu có)
            _inProgressView.Refresh();

            // --- Xây dựng Filter kết hợp ---
            // Biến đếm item đã qua filter tìm kiếm và ngày
            int itemIndex = -1;
            _inProgressView.Filter = item => // <-- Filter là một hàm ẩn danh
            {
                var task = item as TodoTask;
                if (task == null) return false;

                // 1. Kiểm tra điều kiện tìm kiếm (nếu có)
                bool matchesSearch = string.IsNullOrEmpty(_currentSearchTerm) || IsTaskMatch(task, _currentSearchTerm);

                // 2. Kiểm tra điều kiện lọc theo ngày (nếu có)
                bool matchesDate = !_filteredDate.HasValue || (task.Deadline.HasValue && task.Deadline.Value.Date == _filteredDate.Value);

                // Nếu không khớp tìm kiếm HOẶC ngày, không hiển thị
                if (!matchesSearch || !matchesDate)
                {
                    return false; // Bị loại khỏi kết quả lọc
                }

                // Nếu khớp tìm kiếm và ngày, tăng bộ đếm cho mục đích phân trang
                itemIndex++;

                // 3. Kiểm tra điều kiện phân trang DỰA TRÊN itemIndex đã tăng
                // startIndex và endIndex được bắt (capture) từ phạm vi bên ngoài phương thức này
                bool isInPageRange = itemIndex >= startIndex && itemIndex <= endIndex;

                // Trả về true nếu thỏa mãn điều kiện lọc (đã xong) VÀ nằm trong trang hiện tại
                return isInPageRange;
            };
            // --- Hết xây dựng Filter kết hợp ---


            // --- Cập nhật UI phân trang và số lượng ---
            if (!string.IsNullOrEmpty(_currentSearchTerm) || _filteredDate.HasValue)
            {
                InProgressCountText.Text = $"Tìm thấy {totalTasks} task(s)";
            }
            else
            {
                InProgressCountText.Text = $"Tìm thấy {totalTasks} tasks";
            }
            InProgressPageText.Text = string.IsNullOrEmpty(_currentSearchTerm) && !_filteredDate.HasValue ? $"Trang {_inProgressCurrentPage}" : $"Trang {_inProgressCurrentPage} (/{totalPages})";
            PrevInProgressBtn.Visibility = _inProgressCurrentPage > 1 ? Visibility.Visible : Visibility.Collapsed;
            NextInProgressBtn.Visibility = (_inProgressCurrentPage < totalPages && totalPages > 0) ? Visibility.Visible : Visibility.Collapsed;
            NoInProgressTasksText.Visibility = totalTasks == 0 ? Visibility.Visible : Visibility.Collapsed;
            InProgressTasksList.Visibility = totalTasks == 0 ? Visibility.Collapsed : Visibility.Visible;
            // --- Hết cập nhật UI ---
        }
        private void UpdateCompletedTasksView()
        {
            // --- Tính toán totalTasks dựa trên chế độ ---
            int totalTasks;
            if (!string.IsNullOrEmpty(_currentSearchTerm))
            {
                // Nếu đang tìm kiếm, totalTasks là số lượng task trong _completedTasks khớp với _currentSearchTerm
                totalTasks = _completedTasks.Count(t => IsTaskMatch(t, _currentSearchTerm));
            }
            else
            {
                // Nếu không tìm kiếm, totalTasks là tổng số task trong _completedTasks
                totalTasks = _completedTasks.Count;
            }
            // --- Hết tính toán totalTasks ---

            var totalPages = (totalTasks + PageSize - 1) / PageSize;
            if (_completedCurrentPage > totalPages && totalPages > 0)
                _completedCurrentPage = totalPages;
            else if (totalTasks == 0)
                _completedCurrentPage = 1;

            var startIndex = (_completedCurrentPage - 1) * PageSize;
            _completedView.Refresh();

            // --- Xây dựng Filter kết hợp ---
            _completedView.Filter = item =>
            {
                var task = item as TodoTask;
                if (task == null) return false;

                // 1. Kiểm tra điều kiện tìm kiếm (nếu có)
                bool matchesSearch = string.IsNullOrEmpty(_currentSearchTerm) || IsTaskMatch(task, _currentSearchTerm);
                var filteredItems = _completedView.OfType<TodoTask>().ToList();
                // 2. Kiểm tra điều kiện phân trang
                // Lưu ý: Tương tự như trên, logic phân trang cần xem xét lại khi có filter tìm kiếm.
                var index = _completedTasks.IndexOf(task);
                bool isInPageRange = index >= startIndex && index < startIndex + PageSize;

                // Trả về true nếu thỏa mãn cả hai điều kiện
                return matchesSearch && isInPageRange;
            };
            // --- Hết xây dựng Filter kết hợp ---

            // --- Cập nhật UI phân trang và số lượng ---
            // Cập nhật Text cho CompletedCountText dựa trên chế độ
            if (!string.IsNullOrEmpty(_currentSearchTerm))
            {
                // Nếu đang tìm kiếm, hiển thị "Tìm thấy ..."
                CompletedCountText.Text = $"Tìm thấy {totalTasks} task(s)";
            }
            else
            {
                // Nếu không tìm kiếm, hiển thị số lượng bình thường
                CompletedCountText.Text = $"{totalTasks} tasks";
            }

            // Cập nhật Text cho CompletedPageText
            CompletedPageText.Text = string.IsNullOrEmpty(_currentSearchTerm) ? $"Trang {_completedCurrentPage}" : $"Trang {_completedCurrentPage} (/{totalPages})";

            // Cập nhật Visibility cho các nút phân trang và thông báo không có task
             CompletedCountText.Text = string.IsNullOrEmpty(_currentSearchTerm) && !_filteredDate.HasValue ? $"{totalTasks} tasks" : $"Tìm thấy {totalTasks} task(s)";
        CompletedPageText.Text = string.IsNullOrEmpty(_currentSearchTerm) && !_filteredDate.HasValue ? $"Trang {_completedCurrentPage}" : $"Trang {_completedCurrentPage} (/{totalPages})";
            PrevCompletedBtn.Visibility = _completedCurrentPage > 1 ? Visibility.Visible : Visibility.Collapsed;
            NextCompletedBtn.Visibility = (_completedCurrentPage < totalPages && totalPages > 0) ? Visibility.Visible : Visibility.Collapsed;
            NoCompletedTasksText.Visibility = totalTasks == 0 ? Visibility.Visible : Visibility.Collapsed;
            CompletedTasksList.Visibility = totalTasks == 0 ? Visibility.Collapsed : Visibility.Visible;
            // --- Hết cập nhật UI ---
        }
        private void ShowCalendar_Click(object sender, RoutedEventArgs e)
        {
            var calendar = new System.Windows.Controls.Calendar();

            if (DateTime.TryParseExact(DeadlineTextBox.Text, "dd/MM/yyyy", null, System.Globalization.DateTimeStyles.None, out DateTime currentDate))
            {
                calendar.SelectedDate = currentDate;
                calendar.DisplayDate = currentDate;
            }

            calendar.SelectionMode = CalendarSelectionMode.SingleDate;

            var popup = new Popup
            {
                Child = calendar,
                PlacementTarget = sender as UIElement,
                Placement = PlacementMode.Bottom,
                StaysOpen = false,
                IsOpen = true
            };

            calendar.SelectedDatesChanged += (s, args) =>
            {
                if (calendar.SelectedDate.HasValue)
                {
                    DeadlineTextBox.Text = calendar.SelectedDate.Value.ToString("dd/MM/yyyy");
                    popup.IsOpen = false;
                }
            };
        }
        public void ApplyMahAppsTheme(bool isDark)
        {
            // Xóa toàn bộ ResourceDictionary cũ
            Resources.MergedDictionaries.Clear();

            // Thêm theme mới
            var themeDict = new ResourceDictionary();
            if (isDark)
            {
                themeDict.Source = new Uri("Themes/DarkTheme.xaml", UriKind.Relative);
            }
            else
            {
                themeDict.Source = new Uri("Themes/LightTheme.xaml", UriKind.Relative); // Nếu bạn muốn tạo LightTheme.xaml
            }

            Resources.MergedDictionaries.Add(themeDict);

            // Áp dụng màu nền cho window
            this.Background = (Brush)Resources["WindowBackground"];
        }
        private void ShowSettings_Click(object sender, RoutedEventArgs e)
        {
            var settingsWindow = new SettingsWindow();
            settingsWindow.Owner = this; // ✅ Rất quan trọng
            settingsWindow.ShowDialog();
        }
        private void TaskCard_MouseEnter(object sender, MouseEventArgs e)
        {
            if (sender is Border border)
            {
                foreach (var child in FindVisualChildren<TextBlock>(border))
                {
                    // Không đổi màu nếu TextBlock có Tag="Number" hoặc là số
                    if (child.Tag?.ToString() == "Number") continue;

                    // Không đổi màu nếu nội dung là định dạng thời gian, deadline, v.v.
                    if (IsLikelyANumber(child.Text)) continue;

                    child.Foreground = new SolidColorBrush(Colors.Blue); // Xanh nước biển
                }
            }
        }
        private void TaskCard_MouseLeave(object sender, MouseEventArgs e)
        {
            if (sender is Border border)
            {
                foreach (var child in FindVisualChildren<TextBlock>(border))
                {
                    if (child.Tag?.ToString() == "Number") continue;
                    if (IsLikelyANumber(child.Text)) continue;

                    // Trả lại màu gốc dựa trên vai trò
                    if (child.FontWeight == FontWeights.Bold && child.FontSize == 14)
                        child.Foreground = Brushes.Black; // Tiêu đề
                    else
                        child.Foreground = new SolidColorBrush(Color.FromRgb(0x66, 0x66, 0x66)); // Mô tả
                }
            }
        }
        private static IEnumerable<T> FindVisualChildren<T>(DependencyObject depObj) where T : DependencyObject
        {
            if (depObj == null) yield break;
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
            {
                var child = VisualTreeHelper.GetChild(depObj, i);
                if (child is T t) yield return t;
                foreach (var childOfChild in FindVisualChildren<T>(child))
                    yield return childOfChild;
            }
        }
        private bool IsLikelyANumber(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;
            return text.Contains(":") || // 14:30
                   text.Contains("/") || // 12/05/2025
                   text.Contains("⏰") || // Deadline
                   text.Contains("🔔") || // Nhắc nhở
                   double.TryParse(text, out _);
        }
        private void ShowStatistics_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var allTasks = _inProgressTasks.Concat(_completedTasks).ToList();
                var statisticsWindow = new StatisticsWindow(allTasks);
                statisticsWindow.Owner = this; // Đặt cửa sổ mẹ để căn giữa
                statisticsWindow.ShowDialog(); // Mở ở chế độ dialog
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi mở cửa sổ thống kê: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void PriorityTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isFormattingTextBox) return;
            _isFormattingTextBox = true;
            try
            {
                var tb = sender as TextBox;
                if (tb == null) return;

                var text = tb.Text.Trim();
                if (text == "1") tb.Text = "🔴 Cao";
                else if (text == "2") tb.Text = "🟡 Trung bình";
                else if (text == "3") tb.Text = "🟢 Thấp";
            }
            finally
            {
                _isFormattingTextBox = false;
            }
        }
        private void UpdateReminderStatus()
        {
            var now = DateTime.Now;
            var startOfDay = now.Date; // 00:00:00 hôm nay
            var endOfDay = startOfDay.AddDays(1).AddTicks(-1); // 23:59:59.9999999 hôm nay

            // Đếm Reminder trong ngày
            //int upcomingReminders = _inProgressTasks
            //    .Count(t => t.ReminderEnabled && t.ReminderTime.HasValue &&
            //                t.ReminderTime.Value >= startOfDay && t.ReminderTime.Value <= endOfDay);

            // Đếm Deadline trong ngày
            int upcomingDeadlines = _inProgressTasks
                .Count(t => t.Deadline.HasValue &&
                            t.Deadline.Value >= startOfDay && t.Deadline.Value <= endOfDay);

            int totalUpcoming = upcomingDeadlines;

            ReminderStatusText.Text = $"🔔 {totalUpcoming} nhắc nhở sắp tới";
        }
        private void DateTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !Regex.IsMatch(e.Text, "^[0-9]+$");
        }
        private void TimeTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !Regex.IsMatch(e.Text, "^[0-9]+$");
        }
        private void MaskedTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Back || e.Key == Key.Delete || e.Key == Key.Left || e.Key == Key.Right || e.Key == Key.Tab)
            {
                e.Handled = false;
                return;
            }
        }
        private void DateTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isFormattingTextBox) return;
            _isFormattingTextBox = true;
            try
            {
                var tb = sender as TextBox;
                if (tb == null) return;
                int selStart = tb.SelectionStart;
                var digits = Regex.Replace(tb.Text, "\\D", "");
                if (digits.Length > 8) digits = digits.Substring(0, 8);
                string formatted = digits;
                if (digits.Length <= 2)
                    formatted = digits;
                else if (digits.Length <= 4)
                    formatted = digits.Substring(0, 2) + "/" + digits.Substring(2);
                else // 5..8
                    formatted = digits.Substring(0, 2) + "/" + digits.Substring(2, 2) + "/" + digits.Substring(4);

                tb.Text = formatted;

                // restore cursor near the end
                tb.SelectionStart = Math.Min(formatted.Length, selStart + (tb.Text.Length > selStart ? 0 : 0));
            }
            finally
            {
                _isFormattingTextBox = false;
            }
        }
        private void TimeTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isFormattingTextBox) return;
            _isFormattingTextBox = true;
            try
            {
                var tb = sender as TextBox;
                if (tb == null) return;

                int selStart = tb.SelectionStart;
                var originalText = tb.Text;

                // Giữ lại chỉ các chữ số
                var digits = Regex.Replace(originalText, "\\D", "");

                // Giới hạn tối đa 4 chữ số
                if (digits.Length > 4)
                    digits = digits.Substring(0, 4);

                string formatted = digits;

                // Định dạng HH:mm
                if (digits.Length >= 3) // Có ít nhất 3 chữ số -> cần dấu :
                {
                    // Luôn chèn dấu : sau 2 chữ số đầu
                    formatted = digits.Substring(0, 2) + ":" + digits.Substring(2);
                }
                if (digits.Length == 4)
                {
                    if (int.TryParse(digits.Substring(0, 2), out int hour) &&
                        int.TryParse(digits.Substring(2, 2), out int minute))
                    {
                        if (hour > 23 || minute > 59)
                        {
                            StatusText.Text = "⚠ Giờ hoặc phút không hợp lệ!";
                        }
                    }
                }
                if (tb.Text != formatted)
                {
                    tb.Text = formatted;
                    int newCursorPos = formatted.Length;
                    // else newCursorPos = formatted.Length;
                    tb.SelectionStart = newCursorPos;
                }
            }
            finally
            {
                _isFormattingTextBox = false;
            }
        }
        private void LoadTasks()
        {
            try
            {
                var allTasks = _databaseService.GetAllTasks();

                _inProgressTasks.Clear();
                _completedTasks.Clear();

                foreach (var task in allTasks)
                {
                    switch (task.Status)
                    {
                        case TaskStatus.InProgress:
                            _inProgressTasks.Add(task);
                            break;
                        case TaskStatus.Completed:
                            _completedTasks.Add(task);
                            break;
                    }
                }

                // Reset trang
                _inProgressCurrentPage = 1;
                _completedCurrentPage = 1;

                UpdateInProgressTasksView();
                UpdateCompletedTasksView();

                UpdateTaskCounts();
                StatusText.Text = "Đã tải tasks thành công";
                UpdateReminderStatus();
                ResetSearchFilters();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi tải tasks: {ex.Message}", "Lỗi",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void PrevPage_Click(object sender, RoutedEventArgs e)
        {
            if (sender == PrevInProgressBtn)
            {
                if (_inProgressCurrentPage > 1)
                {
                    _inProgressCurrentPage--;
                    // --- Sửa lỗi CS0136: Chỉ khai báo currentView MỘT LẦN trong phạm vi này ---
                    // Kiểm tra ItemsSource hiện tại của ListBox và KHAI BÁO currentView
                    if (InProgressTasksList.ItemsSource is ICollectionView currentView)
                    {
                        // Nếu ItemsSource là ICollectionView (có thể là _inProgressView hoặc view kết quả lọc)
                        // Ta cần xác định xem nó là view của danh sách gốc hay danh sách lọc để áp dụng đúng logic

                        // Cách đơn giản: Kiểm tra SourceCollection của ICollectionView
                        if (currentView.SourceCollection is ObservableCollection<TodoTask> sourceCollection)
                        {
                            // Nếu sourceCollection giống _inProgressTasks thì đang ở chế độ bình thường
                            if (sourceCollection == _inProgressTasks)
                            {
                                // Chế độ bình thường
                                UpdateInProgressTasksView();
                            }
                            else
                            {
                                // Chế độ tìm kiếm: sourceCollection là danh sách kết quả lọc
                                // Cần áp dụng lại phân trang cho view kết quả
                                var filteredTasks = sourceCollection.ToList(); // Lấy danh sách đã lọc
                                var filteredView = CollectionViewSource.GetDefaultView(filteredTasks);
                                ApplyPagingToFilteredViewSimple(filteredView, filteredTasks, _inProgressCurrentPage);
                                InProgressTasksList.ItemsSource = filteredView; // Gán lại để cập nhật UI

                                // Cập nhật UI phân trang
                                var totalTasks = filteredTasks.Count;
                                var totalPages = (totalTasks + PageSize - 1) / PageSize;
                                InProgressPageText.Text = $"Trang {_inProgressCurrentPage} (/{totalPages})";
                                PrevInProgressBtn.Visibility = _inProgressCurrentPage > 1 ? Visibility.Visible : Visibility.Collapsed;
                                NextInProgressBtn.Visibility = (_inProgressCurrentPage < totalPages && totalPages > 0) ? Visibility.Visible : Visibility.Collapsed;
                            }
                        }
                        else
                        {
                            // Fallback nếu SourceCollection không phải ObservableCollection<TodoTask>
                            UpdateInProgressTasksView();
                        }
                    }
                    else
                    {
                        // Fallback nếu ItemsSource không phải ICollectionView (hiếm khi xảy ra)
                        UpdateInProgressTasksView();
                    }
                }
            }
            else if (sender == PrevCompletedBtn)
            {
                // --- Sửa lỗi CS0136: Chỉ khai báo currentView MỘT LẦN trong phạm vi này ---
                if (_completedCurrentPage > 1)
                {
                    _completedCurrentPage--;
                    // Kiểm tra ItemsSource hiện tại của ListBox và KHAI BÁO currentView
                    if (CompletedTasksList.ItemsSource is ICollectionView currentView)
                    {
                        if (currentView.SourceCollection is ObservableCollection<TodoTask> sourceCollection)
                        {
                            if (sourceCollection == _completedTasks)
                            {
                                UpdateCompletedTasksView();
                            }
                            else
                            {
                                var filteredTasks = sourceCollection.ToList();
                                var filteredView = CollectionViewSource.GetDefaultView(filteredTasks);
                                ApplyPagingToFilteredViewSimple(filteredView, filteredTasks, _completedCurrentPage);
                                CompletedTasksList.ItemsSource = filteredView;

                                var totalTasks = filteredTasks.Count;
                                var totalPages = (totalTasks + PageSize - 1) / PageSize;
                                CompletedPageText.Text = $"Trang {_completedCurrentPage} (/{totalPages})";
                                PrevCompletedBtn.Visibility = _completedCurrentPage > 1 ? Visibility.Visible : Visibility.Collapsed;
                                NextCompletedBtn.Visibility = (_completedCurrentPage < totalPages && totalPages > 0) ? Visibility.Visible : Visibility.Collapsed;
                            }
                        }
                        else
                        {
                            UpdateCompletedTasksView();
                        }
                    }
                    else
                    {
                        UpdateCompletedTasksView();
                    }
                }
            }
        }
        // Hàm phụ trợ (giả định bạn đã có hoặc cần thêm vào nếu chưa có)
        private void ApplyPagingToFilteredViewSimple(ICollectionView view, List<TodoTask> sourceList, int currentPage)
        {
            var startIndex = (currentPage - 1) * PageSize;
            view.Refresh();
            view.Filter = item =>
            {
                var index = sourceList.IndexOf((TodoTask)item);
                return index >= startIndex && index < startIndex + PageSize;
            };
        }
        // --- Tương tự, sửa NextPage_Click ---
        private void NextPage_Click(object sender, RoutedEventArgs e)
        {
            if (sender == NextInProgressBtn)
            {
                // Tính totalPages dựa trên nguồn dữ liệu thực tế của view đang hiển thị
                int totalPages = (_inProgressTasks.Count + PageSize - 1) / PageSize; // Giá trị mặc định
                if (InProgressTasksList.ItemsSource is ICollectionView currentView)
                {
                    if (currentView.SourceCollection is ObservableCollection<TodoTask> sourceCollection)
                    {
                        totalPages = (sourceCollection.Count + PageSize - 1) / PageSize;
                    }
                }

                if (_inProgressCurrentPage < totalPages)
                {
                    _inProgressCurrentPage++;
                    // --- Sửa lỗi CS0136: Chỉ khai báo currentView MỘT LẦN trong phạm vi này ---
                    // Kiểm tra ItemsSource hiện tại của ListBox và KHAI BÁO currentView
                    if (InProgressTasksList.ItemsSource is ICollectionView currentView1)
                    {
                        if (currentView1.SourceCollection is ObservableCollection<TodoTask> sourceCollection)
                        {
                            if (sourceCollection == _inProgressTasks)
                            {
                                UpdateInProgressTasksView();
                            }
                            else
                            {
                                var filteredTasks = sourceCollection.ToList();
                                var filteredView = CollectionViewSource.GetDefaultView(filteredTasks);
                                ApplyPagingToFilteredViewSimple(filteredView, filteredTasks, _inProgressCurrentPage);
                                InProgressTasksList.ItemsSource = filteredView;

                                totalPages = (filteredTasks.Count + PageSize - 1) / PageSize; // Tính lại
                                InProgressPageText.Text = $"Trang {_inProgressCurrentPage} (/{totalPages})";
                                PrevInProgressBtn.Visibility = _inProgressCurrentPage > 1 ? Visibility.Visible : Visibility.Collapsed;
                                NextInProgressBtn.Visibility = (_inProgressCurrentPage < totalPages && totalPages > 0) ? Visibility.Visible : Visibility.Collapsed;
                            }
                        }
                        else
                        {
                            UpdateInProgressTasksView();
                        }
                    }
                    else
                    {
                        UpdateInProgressTasksView();
                    }
                }
            }
            else if (sender == NextCompletedBtn)
            {
                // Tương tự cho Completed
                int totalPages = (_completedTasks.Count + PageSize - 1) / PageSize; // Giá trị mặc định
                if (CompletedTasksList.ItemsSource is ICollectionView currentView)
                {
                    if (currentView.SourceCollection is ObservableCollection<TodoTask> sourceCollection)
                    {
                        totalPages = (sourceCollection.Count + PageSize - 1) / PageSize;
                    }
                }

                if (_completedCurrentPage < totalPages)
                {
                    _completedCurrentPage++;
                    // --- Sửa lỗi CS0136: Chỉ khai báo currentView MỘT LẦN trong phạm vi này ---
                    // Kiểm tra ItemsSource hiện tại của ListBox và KHAI BÁO currentView
                    if (CompletedTasksList.ItemsSource is ICollectionView currentView2)
                    {
                        if (currentView2.SourceCollection is ObservableCollection<TodoTask> sourceCollection)
                        {
                            if (sourceCollection == _completedTasks)
                            {
                                UpdateCompletedTasksView();
                            }
                            else
                            {
                                var filteredTasks = sourceCollection.ToList();
                                var filteredView = CollectionViewSource.GetDefaultView(filteredTasks);
                                ApplyPagingToFilteredViewSimple(filteredView, filteredTasks, _completedCurrentPage);
                                CompletedTasksList.ItemsSource = filteredView;

                                totalPages = (filteredTasks.Count + PageSize - 1) / PageSize; // Tính lại
                                CompletedPageText.Text = $"Trang {_completedCurrentPage} (/{totalPages})";
                                PrevCompletedBtn.Visibility = _completedCurrentPage > 1 ? Visibility.Visible : Visibility.Collapsed;
                                NextCompletedBtn.Visibility = (_completedCurrentPage < totalPages && totalPages > 0) ? Visibility.Visible : Visibility.Collapsed;
                            }
                        }
                        else
                        {
                            UpdateCompletedTasksView();
                        }
                    }
                    else
                    {
                        UpdateCompletedTasksView();
                    }
                }
            }
        }
        private void AddTask_Click(object sender, RoutedEventArgs e)
        {
            var title = TitleTextBox.Text.Trim();
            var description = DescriptionTextBox.Text.Trim();
            if (string.IsNullOrEmpty(title))
            {
                MessageBox.Show("Vui lòng nhập tiêu đề task.", "Thiếu thông tin",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                TitleTextBox.Focus();
                return;
            }
            try
            {
                // Lấy priority
                int priority = 1; // Mặc định là Trung bình
                string priorityText = PriorityTextBox.Text.Trim();
                if (priorityText.Contains("Cao")) priority = 2;
                else if (priorityText.Contains("Trung bình")) priority = 1;
                else if (priorityText.Contains("Thấp")) priority = 0;

                // --- Cập nhật logic lấy Deadline THEO THỨ TỰ MỚI ---
                DateTime? deadline = null;
                string dateText = DeadlineTextBox.Text.Trim();
                string timeText = TimeTextBox.Text.Trim();
                DateTime now = DateTime.Now;

                // 1. Kiểm tra người dùng có nhập ngày không?
                if (string.IsNullOrWhiteSpace(dateText))
                {
                    // 1.a. Không nhập ngày -> Dùng ngày hiện tại
                    // 2. Kiểm tra người dùng có nhập giờ không?
                    if (string.IsNullOrWhiteSpace(timeText))
                    {
                        // 2.a. Không nhập giờ -> Dùng ngày hiện tại + 1 giờ
                        deadline = now.AddHours(1);
                    }
                    else
                    {
                        // 2.b. Có nhập giờ -> Dùng ngày hiện tại + giờ người dùng nhập
                        if (DateTime.TryParseExact(timeText, "HH:mm", null, System.Globalization.DateTimeStyles.None, out DateTime parsedTime))
                        {
                            deadline = new DateTime(now.Year, now.Month, now.Day, parsedTime.Hour, parsedTime.Minute, 0);
                        }
                        else
                        {
                            MessageBox.Show("Định dạng giờ không hợp lệ. Vui lòng nhập đúng định dạng HH:mm (VD: 14:30).", "Lỗi giờ", MessageBoxButton.OK, MessageBoxImage.Error);
                            TimeTextBox.Focus();
                            return;
                        }
                    }
                }
                else
                {
                    // 1.b. Có nhập ngày -> Parse ngày
                    if (DateTime.TryParseExact(dateText, "dd/MM/yyyy", null, System.Globalization.DateTimeStyles.None, out DateTime parsedDate))
                    {
                        // 2. Kiểm tra người dùng có nhập giờ không?
                        if (string.IsNullOrWhiteSpace(timeText))
                        {
                            // 2.a. Không nhập giờ -> Dùng ngày người dùng nhập + 1 giờ tính từ BÂY GIỜ (trên ngày đó)
                            deadline = parsedDate.Date.AddHours(now.Hour).AddMinutes(now.Minute).AddSeconds(now.Second).AddHours(1);
                            // Hoặc đơn giản hơn: deadline = parsedDate.Date.AddHours(now.Hour + 1).AddMinutes(now.Minute);
                            // Hoặc theo logic cũ: deadline = parsedDate.Date.AddHours(23).AddMinutes(59); // Giữ nguyên logic cũ nếu muốn
                            // *** Dựa trên yêu cầu "nếu người dùng nhập ngày mà giờ người dùng không nhập thì thời gian hiện tại thêm 1h" ***
                            // Cách hiểu 1: Lấy ngày người dùng nhập, giờ là now + 1h -> deadline = parsedDate.Date.AddHours(now.Hour + 1).AddMinutes(now.Minute);
                            // Cách hiểu 2: Lấy ngày người dùng nhập, giờ là 23:59 -> deadline = parsedDate.Date.AddHours(23).AddMinutes(59);
                            // Mình chọn cách hiểu 1 vì nó sát với "thời gian hiện tại thêm 1h" hơn.
                            deadline = parsedDate.Date.AddHours(now.Hour).AddMinutes(now.Minute).AddSeconds(now.Second).AddHours(1);
                        }
                        else
                        {
                            // 2.b. Có nhập giờ -> Dùng ngày và giờ người dùng nhập (để nguyên)
                            if (DateTime.TryParseExact(timeText, "HH:mm", null, System.Globalization.DateTimeStyles.None, out DateTime parsedTime))
                            {
                                deadline = new DateTime(parsedDate.Year, parsedDate.Month, parsedDate.Day, parsedTime.Hour, parsedTime.Minute, 0);
                            }
                            else
                            {
                                MessageBox.Show("Định dạng giờ không hợp lệ. Vui lòng nhập đúng định dạng HH:mm (VD: 14:30).", "Lỗi giờ", MessageBoxButton.OK, MessageBoxImage.Error);
                                TimeTextBox.Focus();
                                return;
                            }
                        }
                    }
                    else
                    {
                        MessageBox.Show("Định dạng ngày không hợp lệ. Vui lòng nhập đúng định dạng dd/MM/yyyy.", "Lỗi ngày", MessageBoxButton.OK, MessageBoxImage.Error);
                        DeadlineTextBox.Focus();
                        return;
                    }
                }

                // Kiểm tra deadline không phải là quá khứ (nên có)
                if (deadline <= now)
                {
                    MessageBox.Show("Ngày/Giờ Deadline không thể là thời gian trong quá khứ hoặc hiện tại. Vui lòng chọn thời gian trong tương lai.", "Lỗi Deadline", MessageBoxButton.OK, MessageBoxImage.Warning);
                    // Focus vào control lỗi, ví dụ ô giờ nếu lỗi do giờ, hoặc ô ngày nếu lỗi do ngày
                    if (!string.IsNullOrWhiteSpace(dateText) && string.IsNullOrWhiteSpace(timeText))
                    {
                        TimeTextBox.Focus(); // Nếu có ngày nhưng không có giờ, và giờ tính ra <= now
                    }
                    else if (string.IsNullOrWhiteSpace(dateText))
                    {
                        // Nếu không có ngày, deadline được tính từ now, nên nếu lỗi có thể do now thay đổi rất nhanh
                        // Hoặc do logic cộng thêm 1h không đủ. Focus vào giờ để người dùng dễ sửa.
                        TimeTextBox.Focus();
                    }
                    else
                    {
                        DeadlineTextBox.Focus(); // Nếu cả ngày và giờ đều nhập
                    }
                    return;
                }
                // --- Hết phần cập nhật logic lấy Deadline ---

                // --- Thêm logic cho Reminder ---
                DateTime? reminderTime = null;
                bool isReminderEnabled = EnableReminderCheckBox.IsChecked == true;
                if (isReminderEnabled && deadline.HasValue)
                {
                    int minutesBefore = 5; // Giá trị mặc định
                    if (ReminderOffsetComboBox.SelectedItem is ComboBoxItem selectedItem &&
                        int.TryParse(selectedItem.Tag.ToString(), out int parsedMinutes))
                    {
                        minutesBefore = parsedMinutes;
                    }
                    reminderTime = deadline.Value.AddMinutes(-minutesBefore);

                    // Kiểm tra reminderTime không phải là quá khứ (nếu cần kiểm tra chặt chẽ hơn)
                    if (reminderTime <= DateTime.Now)
                    {
                        MessageBox.Show("Thời gian nhắc nhở tính ra là thời gian trong quá khứ. Vui lòng chọn thời gian deadline hợp lệ hoặc giảm số phút nhắc nhở trước.", "Lỗi Nhắc nhở", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                }
                // --- Hết phần thêm logic cho Reminder ---

                // --- Cập nhật logic cho Lặp lại ---
                bool isRepeating = RepeatTaskCheckBox.IsChecked == true;
                RepeatFrequency repeatFreq = RepeatFrequency.None; // Mặc định

                if (isRepeating)
                {
                    // Nếu người dùng chọn "Lặp lại task"
                    // Kiểm tra nếu không chọn tần suất cụ thể trong ComboBox
                    if (RepeatFrequencyComboBox.SelectedItem == null || RepeatFrequencyComboBox.SelectedIndex == -1)
                    {
                        // Mặc định là Hàng ngày nếu chỉ check box
                        repeatFreq = RepeatFrequency.Daily;
                        // Có thể chọn cách khác: hiện thông báo yêu cầu chọn tần suất
                        // MessageBox.Show("Vui lòng chọn tần suất lặp lại (Hàng ngày, Hàng tuần, Hàng tháng).", "Thiếu thông tin lặp lại", MessageBoxButton.OK, MessageBoxImage.Warning);
                        // RepeatFrequencyComboBox.Focus();
                        // return;
                    }
                    else
                    {
                        // Người dùng đã chọn tần suất
                        if (RepeatFrequencyComboBox.SelectedItem is ComboBoxItem freqItem)
                        {
                            string freqText = freqItem.Content.ToString();
                            if (freqText == "Hàng ngày") repeatFreq = RepeatFrequency.Daily;
                            else if (freqText == "Hàng tuần") repeatFreq = RepeatFrequency.Weekly;
                            else if (freqText == "Hàng tháng") repeatFreq = RepeatFrequency.Monthly;
                        }
                    }
                }
                else
                {
                    // Nếu không chọn lặp lại, đảm bảo tần suất là None
                    repeatFreq = RepeatFrequency.None;
                }
                // --- Hết phần cập nhật logic cho Lặp lại ---

                var status = TaskStatus.InProgress;
                var newTask = new TodoTask(title, description) // <-- Gọi constructor CÓ THAM SỐ
                {
                    Deadline = deadline, // <-- Dùng deadline đã tính ở trên
                    ReminderTime = reminderTime, // <-- Dùng reminderTime đã tính ở trên
                    ReminderEnabled = isReminderEnabled, // <-- Dùng isReminderEnabled đã xác định ở trên
                    Priority = priority, // hoặc giá trị priority đã xác định
                    Status = TaskStatus.InProgress,
                    UpdatedDate = DateTime.Now,
                    IsRepeating = isRepeating, // <-- Gán IsRepeating
                    RepeatFrequency = repeatFreq // <-- Gán RepeatFrequency
                };

                var taskId = _databaseService.InsertTask(newTask);
                newTask.Id = taskId;
                _inProgressTasks.Insert(0, newTask);

                // Clear form
                TitleTextBox.Clear();
                DescriptionTextBox.Clear();
                PriorityTextBox.Text = "🟡 Trung bình";
                DeadlineTextBox.Clear();
                TimeTextBox.Clear();
                // --- Cập nhật clear các control lặp lại ---
                RepeatTaskCheckBox.IsChecked = false;
                RepeatFrequencyComboBox.SelectedIndex = -1; // Đặt lại lựa chọn
                                                            // --- Hết phần clear ---
                TitleTextBox.Focus();

                _inProgressCurrentPage = 1; // Luôn về trang 1 khi thêm mới
                UpdateInProgressTasksView();
                UpdateTaskCounts();
                UpdateReminderStatus();
                StatusText.Text = $"Đã thêm task: {title} {(deadline.HasValue ? $"(Deadline: {deadline.Value:dd/MM/yyyy HH:mm})" : "")}";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi thêm task: {ex.Message}", "Lỗi",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void DeleteTask_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var task = button?.Tag as TodoTask;

            if (task == null) return;

            var result = MessageBox.Show(
                $"Bạn có chắc chắn muốn xóa task '{task.Title}'?",
                "Xác nhận xóa",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    _databaseService.DeleteTask(task.Id);

                    // Remove from appropriate collection
                    switch (task.Status)
                    {
                        case TaskStatus.InProgress:
                            _inProgressTasks.Remove(task);
                            break;
                        case TaskStatus.Completed:
                            _completedTasks.Remove(task);
                            break;
                    }

                    // Đảm bảo trang hợp lệ
                    int inProgressTotalPages = (_inProgressTasks.Count + PageSize - 1) / PageSize;
                    int completedTotalPages = (_completedTasks.Count + PageSize - 1) / PageSize;

                    _inProgressCurrentPage = Math.Max(1, Math.Min(_inProgressCurrentPage, inProgressTotalPages));
                    _completedCurrentPage = Math.Max(1, Math.Min(_completedCurrentPage, completedTotalPages));

                    UpdateInProgressTasksView();
                    UpdateCompletedTasksView();

                    UpdateTaskCounts();
                    UpdateReminderStatus();
                    StatusText.Text = $"Đã xóa task: {task.Title}";
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Lỗi khi xóa task: {ex.Message}", "Lỗi",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
        private void EditTask_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var task = button?.Tag as TodoTask;

            if (task == null) return;

            var editWindow = new EditTaskWindow(task);
            // Hiển thị EditTaskWindow dưới dạng hộp thoại
            if (editWindow.ShowDialog() == true)
            {
                try
                {
                    // Cập nhật task đã chỉnh sửa vào cơ sở dữ liệu
                    _databaseService.UpdateTask(task);

                    // Cập nhật thời gian UpdatedDate (nếu EditTaskWindow chưa làm)
                    task.UpdatedDate = DateTime.Now;

                    // Cập nhật giao diện người dùng để phản ánh các thay đổi
                    UpdateInProgressTasksView();
                    UpdateCompletedTasksView();
                    UpdateTaskCounts();
                    UpdateReminderStatus(); // Cập nhật trạng thái reminder nếu cần

                    // Cập nhật thanh trạng thái
                    StatusText.Text = $"Đã cập nhật task: {task.Title}";
                }
                catch (Exception ex)
                {
                    // Xử lý lỗi nếu có vấn đề khi cập nhật cơ sở dữ liệu
                    MessageBox.Show($"Lỗi khi cập nhật task: {ex.Message}", "Lỗi",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
        private void TaskCard_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed && _draggedTask != null)
            {
                Point mousePos = e.GetPosition(null);
                Vector diff = _startPoint - mousePos;

                if (!_isDragging &&
                    (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                     Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance))
                {
                    _isDragging = true;

                    System.Windows.DataObject dragData = new System.Windows.DataObject("TodoTask", _draggedTask);
                    DragDrop.DoDragDrop((DependencyObject)sender, dragData, DragDropEffects.Move);

                    _isDragging = false;
                    _draggedTask = null;
                }
            }
        }
        private void Column_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent("TodoTask"))
            {
                e.Effects = DragDropEffects.Move;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
            e.Handled = true;
        }
        private void Column_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent("TodoTask"))
            {
                var droppedTask = e.Data.GetData("TodoTask") as TodoTask;
                var targetBorder = sender as Border;
                var newStatusString = targetBorder?.Tag.ToString();

                if (droppedTask != null && newStatusString != null)
                {
                    TaskStatus newStatus;
                    switch (newStatusString)
                    {
                        case "InProgress":
                            newStatus = TaskStatus.InProgress;
                            break;
                        case "Completed":
                            newStatus = TaskStatus.Completed;
                            break;
                        default:
                            return;
                    }

                    // Don't do anything if dropped on same column
                    if (droppedTask.Status == newStatus)
                        return;

                    try
                    {
                        // Remove from current collection
                        switch (droppedTask.Status)
                        {
                            case TaskStatus.InProgress:
                                _inProgressTasks.Remove(droppedTask);
                                break;
                            case TaskStatus.Completed:
                                _completedTasks.Remove(droppedTask);
                                break;
                        }

                        // Update task status
                        TaskStatus oldStatus = droppedTask.Status; // Ghi lại trạng thái cũ
                        // Update task status
                        droppedTask.Status = newStatus;

                        // --- Thêm logic kiểm tra và chỉnh sửa Deadline ---
                        if (oldStatus == TaskStatus.Completed && newStatus == TaskStatus.InProgress && droppedTask.Deadline.HasValue)
                        {
                            var currentDeadline = droppedTask.Deadline.Value;
                            var now = DateTime.Now;
                            var tomorrow = now.Date.AddDays(1); // Ngày mai, chỉ phần ngày
                            int targetMonth = now.Month; // Tháng hiện tại
                            int targetYear = now.Year;   // Năm hiện tại
                            int hour = currentDeadline.Hour; // Giữ nguyên giờ
                            int minute = currentDeadline.Minute; // Giữ nguyên phút
                            int second = currentDeadline.Second; // Giữ nguyên giây (nếu cần)

                            // Cố gắng tạo ngày mới
                            DateTime newDeadlineDate;
                            try
                            {
                                int nextDay = tomorrow.Day; // Lấy ngày của ngày mai
                                int nextMonth = targetMonth; // Ghi đè bằng tháng hiện tại
                                int nextYear = targetYear;   // Ghi đè bằng năm hiện tại

                                // Kiểm tra nếu ngày này không hợp lệ trong tháng/năm mới, chọn ngày cuối tháng
                                int daysInTargetMonth = DateTime.DaysInMonth(nextYear, nextMonth);
                                if (nextDay > daysInTargetMonth)
                                {
                                    nextDay = daysInTargetMonth; // Chọn ngày cuối tháng nếu ngày "tiếp theo" không tồn tại
                                }

                                newDeadlineDate = new DateTime(nextYear, nextMonth, nextDay, hour, minute, second);
                                // --- Thêm kiểm tra thời gian quá khứ ---
                                if (newDeadlineDate <= DateTime.Now)
                                {
                                    if (newDeadlineDate <= DateTime.Now)
                                    {
                                        // Deadline mới vẫn <= thời điểm hiện tại
                                        // Có thể tăng ngày lên 1 đơn vị nữa, đảm bảo cùng tháng/năm hiện tại nếu có thể
                                        DateTime tempDate = newDeadlineDate.AddDays(1);
                                        if (tempDate.Month == targetMonth && tempDate.Year == targetYear)
                                        {
                                            newDeadlineDate = tempDate; // Duy trì cùng tháng/năm nếu có thể
                                        }
                                        else
                                        {
                                            DateTime realTomorrow = now.Date.AddDays(1);
                                            // Gán lại tháng/năm hiện tại
                                            int realDay = realTomorrow.Day;
                                            if (realDay > daysInTargetMonth)
                                            {
                                                realDay = daysInTargetMonth;
                                            }
                                            newDeadlineDate = new DateTime(targetYear, targetMonth, realDay, hour, minute, second);
                                            // Kiểm tra lại sau khi chỉnh sửa
                                            if (newDeadlineDate <= DateTime.Now)
                                            {
                                                // Vẫn <= Now, có thể do giờ phút cũ.
                                                // Lấy ngày mai thực sự, không ép tháng.
                                                newDeadlineDate = realTomorrow.AddHours(hour).AddMinutes(minute).AddSeconds(second);
                                                // Kiểm tra lại
                                                if (newDeadlineDate <= DateTime.Now)
                                                {
                                                    // Vẫn <= Now, có thể tăng phút lên 5 phút sau thời điểm hiện tại
                                                    newDeadlineDate = DateTime.Now.AddMinutes(5);
                                                }
                                            }
                                        }
                                    }
                                }
                                // --- Hết phần kiểm tra thời gian quá khứ ---
                            }
                            catch (ArgumentOutOfRangeException)
                            {
                                // Trường hợp rất hiếm khi DateTime constructor lỗi
                                // Fallback: đặt lại deadline là ngày mai thực sự với giờ cũ
                                newDeadlineDate = now.Date.AddDays(1).AddHours(hour).AddMinutes(minute).AddSeconds(second);
                                // Kiểm tra lại sau fallback
                                if (newDeadlineDate <= DateTime.Now)
                                {
                                    newDeadlineDate = DateTime.Now.AddMinutes(5);
                                }
                            }

                            droppedTask.Deadline = newDeadlineDate;
                            // Cập nhật lại ReminderTime nếu cần
                            if (droppedTask.ReminderEnabled)
                            {
                                // Tính lại reminder time dựa trên deadline mới
                                int minutesBefore = 5; // Hoặc lấy giá trị từ cài đặt nếu có
                                if (ReminderOffsetComboBox.SelectedItem is ComboBoxItem selectedItem &&
                                    int.TryParse(selectedItem.Tag.ToString(), out int parsedMinutes))
                                {
                                    minutesBefore = parsedMinutes;
                                }
                                droppedTask.ReminderTime = droppedTask.Deadline.Value.AddMinutes(-minutesBefore);
                            }
                        }
                        // --- Hết phần logic chỉnh sửa Deadline ---

                        // Nếu chuyển sang trạng thái InProgress, bật nhắc nhở 5 phút trước deadline
                        if (newStatus == TaskStatus.InProgress)
                        {
                            if (droppedTask.Deadline.HasValue)
                            {
                                droppedTask.ReminderEnabled = true;
                                droppedTask.ReminderTime = droppedTask.Deadline.Value.AddMinutes(-5);
                            }
                            else
                            {
                                droppedTask.ReminderEnabled = false;
                                droppedTask.ReminderTime = null;
                                StatusText.Text = $"Task '{droppedTask.Title}' không có deadline, không thể đặt nhắc nhở khi kéo sang Chưa hoàn thành.";
                            }
                        }
                        else if (newStatus == TaskStatus.Completed)
                        {
                            droppedTask.ReminderEnabled = false;
                            droppedTask.ReminderTime = null;
                        }

                        // Update in database
                        droppedTask.UpdatedDate = DateTime.Now;
                        _databaseService.UpdateTask(droppedTask);

                        // Add to new collection
                        switch (newStatus)
                        {
                            case TaskStatus.InProgress:
                                _inProgressTasks.Insert(0, droppedTask);
                                break;
                            case TaskStatus.Completed:
                                _completedTasks.Insert(0, droppedTask);
                                break;
                        }
                        UpdateInProgressTasksView();
                        UpdateCompletedTasksView();

                        UpdateTaskCounts();
                        UpdateReminderStatus();
                        StatusText.Text = $"Đã chuyển '{droppedTask.Title}' sang {droppedTask.StatusText}";
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Lỗi khi cập nhật task: {ex.Message}", "Lỗi",
                            MessageBoxButton.OK, MessageBoxImage.Error);

                        // Reload tasks to ensure consistency
                        LoadTasks();
                    }
                }
            }
        }
        private void UpdateTaskCounts()
        {
            InProgressCountText.Text = $"{_inProgressTasks.Count} tasks";
            CompletedCountText.Text = $"{_completedTasks.Count} tasks";

            var totalTasks = _inProgressTasks.Count + _completedTasks.Count;
            ReminderStatusText.Text = $"🔔 {totalTasks} tasks | {_inProgressTasks.Concat(_completedTasks).Count(t => t.ReminderEnabled)} nhắc nhở";
        }
        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.Key == Key.Enter && (TitleTextBox.IsFocused || DescriptionTextBox.IsFocused))
            {
                AddTask_Click(this, new RoutedEventArgs());
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                TitleTextBox.Clear();
                DescriptionTextBox.Clear();
                TitleTextBox.Focus();
                e.Handled = true;
            }
            else if (e.Key == Key.F5)
            {
                LoadTasks();
                e.Handled = true;
            }

            base.OnKeyDown(e);
        }
        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            if (MyNotifyIcon != null)
            {
                MyNotifyIcon.ToolTipText = "TodoList App (Đang chạy nền - Click chuột phải để mở)";
            }
            e.Cancel = true;
            this.Hide();
        }
        private void ShowMainWindow_Click(object sender, RoutedEventArgs e)
        {
            var app = (App)Application.Current;
            app.ShowMainWindow();
        }
        private void ExitApplication_Click(object sender, RoutedEventArgs e)
        {
            var app = (App)Application.Current;
            app.ExitApplication();
        }
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            TitleTextBox.Focus();
            StatusText.Text = "Sẵn sàng - Kéo thả task để thay đổi trạng thái";
            ApplyMahAppsTheme(Properties.Settings.Default.IsDarkMode);
            UpdateReminderStatus();

            _startupTimer.Start();

        }
        private async void CheckForUpdates_Click(object sender, RoutedEventArgs e)
        {
            const string AppCastUrl = "https://raw.githubusercontent.com/Polieta/TodoListApp/main/AppCast.xml";
            try
            {
                // 1. Lấy phiên bản hiện tại
                var currentVersion = Assembly.GetExecutingAssembly().GetName().Version;

                // 2. Tải và parse AppCast.xml
                using var client = new HttpClient();
                client.Timeout = TimeSpan.FromSeconds(10);
                string xml = await client.GetStringAsync(AppCastUrl);
                var doc = XDocument.Parse(xml);

                // 3. Lấy thông tin phiên bản mới nhất
                var item = doc.Descendants("item").FirstOrDefault();
                if (item == null) { MessageBox.Show("Không tìm thấy cập nhật."); return; }

                var enclosure = item.Element("enclosure");
                var versionStr = enclosure?.Attribute(XName.Get("version", "http://www.andymatuschak.org/xml-namespaces/sparkle"))?.Value;
                var downloadUrl = enclosure?.Attribute("url")?.Value?.Trim(); // ⚠️ Trim để loại bỏ dấu cách thừa

                if (string.IsNullOrEmpty(versionStr) || string.IsNullOrEmpty(downloadUrl))
                { MessageBox.Show("Dữ liệu cập nhật không hợp lệ."); return; }

                // 4. So sánh phiên bản
                if (!Version.TryParse(versionStr, out Version latestVersion))
                { MessageBox.Show("Phiên bản không hợp lệ."); return; }

                if (latestVersion <= currentVersion)
                {
                    MessageBox.Show("Bạn đang dùng phiên bản mới nhất.", "Cập nhật", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                // 5. Hỏi người dùng
                var result = MessageBox.Show(
                    $"Có phiên bản mới ({latestVersion})!\n\nBạn có muốn cập nhật ngay?",
                    "Cập nhật sẵn sàng",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result != MessageBoxResult.Yes) return;

                // 6. Tải file mới về thư mục tạm
                string tempExe = Path.Combine(Path.GetTempPath(), "TodoListApp_Update.exe");

                // ✅ SỬA LỖI: Dùng ReadAsStreamAsync thay vì DownloadFileTaskAsync
                using var response = await client.GetAsync(downloadUrl);
                response.EnsureSuccessStatusCode();
                using var stream = await response.Content.ReadAsStreamAsync();
                using var fileStream = new FileStream(tempExe, FileMode.Create, FileAccess.Write, FileShare.None);
                await stream.CopyToAsync(fileStream);

                // 7. Bắt đầu cập nhật tự động
                SelfUpdater.ApplyUpdateAndRestart(tempExe);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Không thể cập nhật:\n{ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Event handler cho timer
        private void StartupTimer_Tick(object? sender, EventArgs e)
        {
            // Dừng timer
            _startupTimer.Stop();
            // Tải tasks trước
            LoadTasks();
            // Kiểm tra và cập nhật các task quá khứ
            CheckAndFixPastTasks();

            // Sau khi xử lý xong, tải lại tasks để đảm bảo UI cập nhật
            LoadTasks();
        }
        private void CheckAndFixPastTasks()
        {
            var now = DateTime.Now;
            var tasksToUpdate = new List<TodoTask>();

            // --- Kiểm tra trong danh sách Chưa hoàn thành (_inProgressTasks) ---
            foreach (var task in _inProgressTasks.ToList()) // ToList để tránh lỗi khi thay đổi collection trong loop
            {
                if (task.Deadline.HasValue && task.Deadline.Value < now)
                {
                    if (task.IsRepeating)
                    {
                        // --- XỬ LÝ TASK CÓ LẶP LẠI ---
                        DateTime? originalDeadline = task.Deadline;
                        DateTime? newDeadline = null;

                        if (originalDeadline.HasValue)
                        {
                            // Tính ngày lặp tiếp theo cho đến khi >= now
                            newDeadline = CalculateNextValidRepeatDate(originalDeadline.Value, task.RepeatFrequency, now);
                        }

                        // Cập nhật Deadline
                        task.Deadline = newDeadline;

                        // Cập nhật ReminderTime nếu cần (dựa trên Deadline mới hoặc giữ nguyên logic cũ nếu có ReminderTime riêng)
                        if (task.ReminderEnabled)
                        {
                            // Giả sử ReminderTime luôn được tính từ Deadline mới
                            int minutesBefore = 5; // Mặc định
                            if (ReminderOffsetComboBox.SelectedItem is ComboBoxItem selectedItem &&
                                int.TryParse(selectedItem.Tag.ToString(), out int parsedMinutes))
                            {
                                minutesBefore = parsedMinutes;
                            }

                            if (task.Deadline.HasValue)
                            {
                                DateTime calculatedReminderTime = task.Deadline.Value.AddMinutes(-minutesBefore);

                                // Đảm bảo ReminderTime cũng >= now
                                if (calculatedReminderTime < now)
                                {
                                    // Nếu ReminderTime tính ra vẫn < now, có thể tăng nó lên 5 phút sau now
                                    // Hoặc có thể tính lại ReminderTime dựa trên Deadline mới và offset
                                    // Ở đây, ta chọn tăng ReminderTime lên 5 phút sau now nếu nó < now
                                    task.ReminderTime = now.AddMinutes(5);
                                }
                                else
                                {
                                    task.ReminderTime = calculatedReminderTime;
                                }
                            }
                            else
                            {
                                // Nếu không có Deadline mới, tắt reminder hoặc đặt mặc định
                                task.ReminderTime = null; // hoặc now.AddMinutes(5);
                            }
                        }

                        task.UpdatedDate = DateTime.Now;
                        tasksToUpdate.Add(task);
                        StatusText.Text = $"Đã cập nhật task lặp lại '{task.Title}' đến {task.Deadline?.ToString("dd/MM/yyyy HH:mm") ?? "Không có deadline mới"}";
                    }
                    else
                    {
                        // --- XỬ LÝ TASK KHÔNG LẶP LẠI (Giữ nguyên logic cũ) ---
                        DateTime newDate = now.Date; // Đặt lại ngày thành hôm nay
                        TimeSpan deadlineTime = task.Deadline?.TimeOfDay ?? TimeSpan.Zero;
                        TimeSpan reminderTime = task.ReminderTime?.TimeOfDay ?? TimeSpan.Zero;

                        DateTime newDeadline = newDate.Add(deadlineTime);
                        DateTime newReminderTime = newDate.Add(reminderTime);

                        // Nếu giờ đã qua, chuyển sang ngày mai
                        if (newDeadline <= now)
                        {
                            newDeadline = newDeadline.AddDays(1);
                        }
                        if (newReminderTime <= now)
                        {
                            newReminderTime = newReminderTime.AddDays(1);
                        }

                        // Cập nhật task
                        task.Deadline = newDeadline;
                        if (task.ReminderEnabled)
                        {
                            task.ReminderTime = newReminderTime;
                        }
                        task.UpdatedDate = DateTime.Now;

                        tasksToUpdate.Add(task);
                        StatusText.Text = $"Đã điều chỉnh task quá khứ '{task.Title}' sang {task.Deadline?.ToString("dd/MM/yyyy HH:mm")}";
                    }
                }
            }

            // --- Cập nhật vào Database ---
            foreach (var task in tasksToUpdate)
            {
                try
                {
                    _databaseService.UpdateTask(task);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Lỗi cập nhật task {task.Id} trong CheckAndFixPastTasks: {ex.Message}");
                    StatusText.Text = $"Lỗi khi cập nhật task '{task.Title}': {ex.Message}";
                }
            }

            if (tasksToUpdate.Any())
            {
                StatusText.Text = $"Đã cập nhật {tasksToUpdate.Count} task có deadline/reminder trong quá khứ.";
            }
            // --- Hết cập nhật vào Database ---
        }

        private DateTime? CalculateNextValidRepeatDate(DateTime originalDate, RepeatFrequency frequency, DateTime now)
        {
            DateTime nextDate = originalDate;

            // Lặp cho đến khi nextDate >= now
            while (nextDate < now)
            {
                switch (frequency)
                {
                    case RepeatFrequency.Daily:
                        nextDate = nextDate.AddDays(1);
                        break;
                    case RepeatFrequency.Weekly:
                        nextDate = nextDate.AddDays(7);
                        break;
                    case RepeatFrequency.Monthly:
                        // Thử thêm 1 tháng. Nếu ngày không tồn tại (ví dụ: 31/01 -> 31/02), DateTime sẽ tự điều chỉnh hoặc ném lỗi.
                        try
                        {
                            nextDate = nextDate.AddMonths(1);
                        }
                        catch (ArgumentOutOfRangeException)
                        {
                            int targetDay = nextDate.Day;
                            int targetMonth = nextDate.Month;
                            int targetYear = nextDate.Year;

                            int nextMonth = targetMonth + 1;
                            int nextYear = targetYear;
                            if (nextMonth > 12)
                            {
                                nextMonth = 1;
                                nextYear++;
                            }

                            // Tìm ngày cuối tháng của tháng tiếp theo
                            int daysInNextMonth = DateTime.DaysInMonth(nextYear, nextMonth);
                            int finalDay = targetDay > daysInNextMonth ? daysInNextMonth : targetDay;

                            nextDate = new DateTime(nextYear, nextMonth, finalDay, nextDate.Hour, nextDate.Minute, nextDate.Second);
                            // Sau khi điều chỉnh, có thể vẫn < now, vòng lặp while sẽ tiếp tục xử lý.
                        }
                        break;
                    default:
                        // Nếu không phải Daily, Weekly, Monthly, coi như không lặp hoặc lỗi
                        return null; // hoặc nextDate = nextDate.AddDays(1); // Fallback?
                }

                // Bảo vệ chống lặp vô hạn (rất khó xảy ra, nhưng tốt hơn là có)
                if (nextDate <= originalDate)
                {
                    // Điều này có thể xảy ra nếu AddDays/AddMonths không hoạt động như mong đợi hoặc now rất xa trong tương lai
                    // Fallback an toàn
                    return now.Date.AddDays(1); // Đặt sang ngày mai
                }
            }

            return nextDate;
        }
        public void ResetSearchFilters()
        {
            // Reset filter tìm kiếm
            _currentSearchTerm = string.Empty;
            // Reset filter theo ngày
            _filteredDate = null;

            // Reset lại trang
            _inProgressCurrentPage = 1;
            _completedCurrentPage = 1;

            InProgressTasksList.ItemsSource = _inProgressView; // <-- THÊM DÒNG NÀY
            CompletedTasksList.ItemsSource = _completedView;

            // Gọi Update...View sẽ áp dụng lại Filter (chỉ phân trang, không có tìm kiếm hoặc filter theo ngày)
            UpdateInProgressTasksView();
            UpdateCompletedTasksView();
            UpdateTaskCounts();
            StatusText.Text = "Đã reset bộ lọc.";
        }
        private void SearchTask_Click(object sender, RoutedEventArgs e)
        {
            var input = SearchTextBox.Text.Trim();
            _currentSearchTerm = input; // Lưu lại từ khóa tìm kiếm
            _filteredDate = null;
            if (string.IsNullOrEmpty(input))
            {
                // --- Nếu ô tìm kiếm trống -> Hiển thị tất cả (Reset tìm kiếm) ---
                ResetSearchFilters();
                return;
            }

            // --- Nếu có từ khóa -> Cập nhật lại view với filter tìm kiếm ---
            // Reset lại trang về 1 khi tìm kiếm mới
            _inProgressCurrentPage = 1;
            _completedCurrentPage = 1;
            InProgressTasksList.ItemsSource = _inProgressView; // <-- THÊM DÒNG NÀY
            CompletedTasksList.ItemsSource = _completedView; // <-- THÊM DÒNG NÀY
            // Gọi Update...View để áp dụng filter tìm kiếm + phân trang mới
            UpdateInProgressTasksView();
            UpdateCompletedTasksView();
            UpdateTaskCounts();
            // Cập nhật status text
            // Đếm số lượng task khớp trong cả hai danh sách
            int inProgressMatchCount = _inProgressTasks.Count(t => IsTaskMatch(t, input));
            int completedMatchCount = _completedTasks.Count(t => IsTaskMatch(t, input));
            StatusText.Text = $"Tìm thấy {inProgressMatchCount + completedMatchCount} task(s) cho '{input}'.";
        }
        private bool IsTaskMatch(TodoTask task, string searchTerm)
        {
            if (task == null || string.IsNullOrWhiteSpace(searchTerm))
                return false;

            // Tìm theo ID nếu searchTerm là số
            if (int.TryParse(searchTerm, out int id) && task.Id == id)
                return true;

            // Tìm theo Tiêu đề (không phân biệt hoa thường)
            if (task.Title.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
                return true;

            // Tìm theo Mô tả (không phân biệt hoa thường) - Có thể thêm nếu muốn
            if (task.Description.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
                return true;

            // Tìm theo Mức độ ưu tiên (dựa trên text)
            string priorityText = task.PriorityText.ToLowerInvariant(); // Giả định PriorityText trả về "Cao", "Trung bình", "Thấp"
            if (priorityText.Contains(searchTerm.ToLowerInvariant()))
                return true;

            // Có thể thêm tìm theo trạng thái, deadline, v.v. nếu cần

            return false; // Không khớp
        }
        private void ShowPriorityPopup_Click(object sender, RoutedEventArgs e)
        {
            var listBox = new ListBox();
            listBox.Items.Add(new ListBoxItem { Content = "🔴 Cao", Tag = "1" });
            listBox.Items.Add(new ListBoxItem { Content = "🟡 Trung bình", Tag = "2" });
            listBox.Items.Add(new ListBoxItem { Content = "🟢 Thấp", Tag = "3" });

            var popup = new Popup
            {
                Child = listBox,
                PlacementTarget = sender as UIElement,
                Placement = PlacementMode.Bottom,
                StaysOpen = false,
                IsOpen = true
            };

            listBox.SelectionChanged += (s, args) =>
            {
                if (listBox.SelectedItem is ListBoxItem item)
                {
                    PriorityTextBox.Text = item.Content.ToString();
                    popup.IsOpen = false;
                }
            };
        }
        private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T t) return t;
                var result = FindVisualChild<T>(child);
                if (result != null) return result;
            }
            return null;
        }
        private void ShowTaskCalendar_Click(object sender, RoutedEventArgs e)
        {
            var window = new TaskCalendarWindow();
            window.Show();
        }
    }
}