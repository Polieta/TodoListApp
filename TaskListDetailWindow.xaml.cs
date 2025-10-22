using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace TodoListApp
{
    public partial class TaskListDetailWindow : Window
    {
        private List<TodoTask> _tasksForDay; // Danh sách task được truyền vào

        // Constructor nhận danh sách task
        public TaskListDetailWindow(List<TodoTask> tasks)
        {
            InitializeComponent();
            _tasksForDay = tasks ?? new List<TodoTask>(); // Đảm bảo không null
            this.DataContext = _tasksForDay; // Gán danh sách vào DataContext để ListBox binding
        }

        private void ViewOnMainButton_Click(object sender, RoutedEventArgs e)
        {
            // Lấy MainWindow hiện tại
            var mainWindow = Application.Current.MainWindow as MainWindow;
            if (mainWindow != null)
            {
                // Giả sử tất cả các task trong _tasksForDay đều có cùng ngày (ngày được chọn từ lịch)
                // Ta sẽ lấy ngày từ một task bất kỳ để làm tiêu chí lọc.
                // Nếu danh sách trống, có thể chọn reset hoặc thông báo.
                if (_tasksForDay.Any())
                {
                    DateTime targetDate = _tasksForDay.First().Deadline?.Date ?? DateTime.Now.Date;
                    // Gọi phương thức trong MainWindow để áp dụng filter theo ngày
                    mainWindow.ShowTasksForDateOnMain(targetDate);
                    this.Close();
                }
                else
                {
                    mainWindow.ResetSearchFilters(); // Nếu không có task, reset filter
                    mainWindow.StatusText.Text = "Không có task nào để hiển thị.";
                }
            }
            else
            {
                MessageBox.Show("Không thể tìm thấy cửa sổ chính.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Đóng cửa sổ chi tiết
            this.Close();
        }
    }
}