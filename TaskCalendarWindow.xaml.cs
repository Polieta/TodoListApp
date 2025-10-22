using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data; // Thêm namespace cho IValueConverter
using System.Windows.Input;
using System.Windows.Media; // Thêm namespace cho Brush, Colors

namespace TodoListApp
{
    public partial class TaskCalendarWindow : Window
    {
        public ObservableCollection<string> Months { get; set; } = new();
        public ObservableCollection<int> Years { get; set; } = new();
        public int CurrentMonth { get; set; }
        public int CurrentYear { get; set; }

        public List<string> DaysOfWeek { get; set; } = new()
        {
            "Thứ hai", "Thứ ba", "Thứ tư", "Thứ năm", "Thứ sáu", "Thứ bảy", "Chủ nhật"
        };

        public ObservableCollection<CalendarDay> CalendarDays { get; set; } = new();

        // Tham chiếu đến MainWindow để lấy dữ liệu task
        private MainWindow _mainWindow;

        public TaskCalendarWindow()
        {
            InitializeComponent();
            DataContext = this;

            // Lấy tham chiếu đến MainWindow
            _mainWindow = Application.Current.MainWindow as MainWindow;

            if (_mainWindow == null)
            {
                MessageBox.Show("Lỗi: Không thể truy cập MainWindow để lấy dữ liệu task.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                this.Close(); // Đóng cửa sổ nếu không có MainWindow
                return;
            }

            InitializeData();
            LoadCalendar();
        }

        private void InitializeData()
        {
            Months = new ObservableCollection<string>
            {
                "Tháng 1", "Tháng 2", "Tháng 3", "Thứ tư", "Thứ năm", "Thứ sáu",
                "Thứ bảy", "Tháng 8", "Tháng 9", "Tháng 10", "Tháng 11", "Tháng 12"
            };

            Years = new ObservableCollection<int>();
            for (int i = DateTime.Now.Year - 1; i <= DateTime.Now.Year + 2; i++)
                Years.Add(i);

            CurrentMonth = DateTime.Now.Month;
            CurrentYear = DateTime.Now.Year;
            MonthYearText.Text = $"{Months[CurrentMonth - 1]} {CurrentYear}";
        }

        private void LoadCalendar()
        {
            CalendarDays.Clear();
            var firstDay = new DateTime(CurrentYear, CurrentMonth, 1);
            var lastDay = firstDay.AddMonths(1).AddDays(-1);

            // Tính thứ của ngày đầu tiên trong tháng (Thứ hai = 1, ..., Chủ nhật = 0 -> chuyển thành 7)
            int startDayOfWeek = (int)firstDay.DayOfWeek;
            if (startDayOfWeek == 0) startDayOfWeek = 7; // Chủ nhật là 0, chuyển thành 7
            startDayOfWeek--; // Chuyển về 0-based index cho cột (0=Thứ hai, 1=Thứ ba, ..., 6=Chủ nhật)

            // Thêm các ngày trống trước ngày đầu tiên
            for (int i = 0; i < startDayOfWeek; i++)
            {
                CalendarDays.Add(new CalendarDay { Day = 0, Date = DateTime.MinValue }); // Dùng DateTime.MinValue cho ngày trống
            }

            // Thêm các ngày trong tháng
            for (int day = 1; day <= lastDay.Day; day++)
            {
                var date = new DateTime(CurrentYear, CurrentMonth, day);
                var tasks = GetTasksByDate(date); // Lấy task có deadline là ngày này
                CalendarDays.Add(new CalendarDay { Day = day, Date = date, Tasks = tasks });
            }

            // Thêm các ngày trống sau cuối tháng để đầy bảng (7 cột * 6 hàng = 42 ô)
            while (CalendarDays.Count < 42)
            {
                CalendarDays.Add(new CalendarDay { Day = 0, Date = DateTime.MinValue });
            }
        }

        private List<TodoTask> GetTasksByDate(DateTime date)
        {
            if (_mainWindow == null) return new List<TodoTask>();

            var tasks = new List<TodoTask>();
            // Kiểm tra trong danh sách Chưa hoàn thành
            foreach (var task in _mainWindow._inProgressTasks)
            {
                if (task.Deadline.HasValue && task.Deadline.Value.Date == date.Date)
                {
                    tasks.Add(task);
                }
            }
            // Kiểm tra trong danh sách Đã hoàn thành (nếu bạn muốn hiển thị cả task hoàn thành)
            // foreach (var task in _mainWindow._completedTasks)
            // {
            //     if (task.Deadline.HasValue && task.Deadline.Value.Date == date.Date)
            //     {
            //         tasks.Add(task);
            //     }
            // }
            return tasks;
        }

        private void RefreshCalendar_Click(object sender, RoutedEventArgs e)
        {
            LoadCalendar();
        }

        private void CalendarDay_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            // Kiểm tra nếu là double click
            if (e.ClickCount == 2)
            {
                var border = sender as Border;
                if (border?.DataContext is CalendarDay day && day.Date != DateTime.MinValue && day.Tasks.Any())
                {
                    // Mở cửa sổ hiển thị danh sách task trong ngày
                    // Bạn cần có một cửa sổ như TaskListDetailWindow
                    var detailWindow = new TaskListDetailWindow(day.Tasks);
                    detailWindow.Show();

                    // Nếu bạn chưa có cửa sổ riêng, có thể hiển thị trong MessageBox hoặc làm khác
                    // Ví dụ đơn giản với MessageBox:
                    //string taskListText = $"Các task trong ngày {day.Date:dd/MM/yyyy}:\n";
                    //foreach (var task in day.Tasks)
                    //{
                    //    taskListText += $"- [{task.PriorityText}] {task.Title}\n";
                    //}
                    //MessageBox.Show(taskListText, $"Task ngày {day.Date:dd/MM/yyyy}", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
        }

        private void MonthComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (MonthComboBox.SelectedItem != null)
            {
                CurrentMonth = Months.IndexOf(MonthComboBox.SelectedItem.ToString()) + 1;
                MonthYearText.Text = $"{Months[CurrentMonth - 1]} {CurrentYear}";
                LoadCalendar();
            }
        }

        private void YearComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (YearComboBox.SelectedItem != null)
            {
                CurrentYear = (int)YearComboBox.SelectedItem;
                MonthYearText.Text = $"{Months[CurrentMonth - 1]} {CurrentYear}";
                LoadCalendar();
            }
        }
    }

    public class CalendarDay
    {
        public int Day { get; set; }
        public DateTime Date { get; set; }
        public List<TodoTask> Tasks { get; set; } = new();
        public int TaskCount => Tasks.Count;
        public bool IsToday => Date != DateTime.MinValue && Date.Date == DateTime.Now.Date; // Kiểm tra ngày trống
        public bool IsEmpty => Date == DateTime.MinValue; // Kiểm tra ô trống
    }
}