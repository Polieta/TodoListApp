using LiveCharts;
using LiveCharts.Wpf;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace TodoListApp
{
    // Lớp ViewModel để binding dữ liệu cho biểu đồ
    public class StatisticsViewModel
    {
        public SeriesCollection StatusSeries { get; set; }
        public SeriesCollection PrioritySeries { get; set; }
        public SeriesCollection StatusPieSeries { get; set; }
        public SeriesCollection PriorityPieSeries { get; set; }

        // Các Axis cho biểu đồ cột (nếu cần binding)
        // public Axis[] StatusXAxes { get; set; }
        // public Axis[] PriorityXAxes { get; set; }
    }

    public partial class StatisticsWindow : Window
    {
        private readonly IEnumerable<TodoTask> _allTasks;

        public StatisticsWindow(IEnumerable<TodoTask> allTasks)
        {
            InitializeComponent();
            _allTasks = allTasks;
            this.Title = "📊 Thống kê Công việc";

            // Tạo và gán ViewModel cho các biểu đồ trạng thái và ưu tiên
            var viewModel = CreateViewModel();
            this.DataContext = viewModel;

            MakeYAxisInteger(StatusChart);
            MakeYAxisInteger(PriorityChart);

            // Tạo biểu đồ theo tháng (hàm của bạn)
            CreateMonthlyChart();
        }

        // Ép trục Y hiển thị số nguyên (step = 1, format F0)
        private void MakeYAxisInteger(LiveCharts.Wpf.CartesianChart chart, string title = "Số lượng Task")
        {
            if (chart == null) return;

            // Xoá trục hiện tại (nếu có) rồi thêm trục mới
            chart.AxisY.Clear();

            var axisY = new Axis
            {
                Title = title,
                MinValue = 0, // nếu muốn bắt đầu từ 0
                LabelFormatter = value => value.ToString("F0"), // hiển thị không có chữ số thập phân
                Separator = new Separator { Step = 1 } // mỗi tick cách nhau 1 (integer ticks)
            };

            chart.AxisY.Add(axisY);
        }


        private StatisticsViewModel CreateViewModel()
        {
            var viewModel = new StatisticsViewModel();

            // --- Tạo dữ liệu cho Biểu đồ Cột ---

            // 1. Biểu đồ cột theo Trạng thái
            int inProgressCount = _allTasks.Count(t => t.Status == TaskStatus.InProgress);
            int completedCount = _allTasks.Count(t => t.Status == TaskStatus.Completed);

            viewModel.StatusSeries = new SeriesCollection
            {
                new ColumnSeries
                {
                    Title = "Chưa xong",
                    Values = new ChartValues<int> { inProgressCount },
                    Fill = new SolidColorBrush(Colors.Orange)
                },
                new ColumnSeries
                {
                    Title = "Hoàn thành",
                    Values = new ChartValues<int> { completedCount },
                    Fill = new SolidColorBrush(Colors.Green)
                }
            };

            // 2. Biểu đồ cột theo Ưu tiên
            int lowCount = _allTasks.Count(t => t.Priority == 0);
            int mediumCount = _allTasks.Count(t => t.Priority == 1);
            int highCount = _allTasks.Count(t => t.Priority == 2);

            viewModel.PrioritySeries = new SeriesCollection
            {
                new ColumnSeries
                {
                    Title = "Thấp",
                    Values = new ChartValues<int> { lowCount },
                    Fill = new SolidColorBrush(Colors.LightBlue)
                },
                new ColumnSeries
                {
                    Title = "Trung bình",
                    Values = new ChartValues<int> { mediumCount },
                    Fill = new SolidColorBrush(Colors.Blue)
                },
                new ColumnSeries
                {
                    Title = "Cao",
                    Values = new ChartValues<int> { highCount },
                    Fill = new SolidColorBrush(Colors.Red)
                }
            };

            // --- Tạo dữ liệu cho Biểu đồ Tròn ---

            // 1. Biểu đồ tròn theo Trạng thái
            viewModel.StatusPieSeries = new SeriesCollection
            {
                new PieSeries
                {
                    Title = "Chưa xong",
                    Values = new ChartValues<int> { inProgressCount },
                    Fill = new SolidColorBrush(Colors.Orange)
                },
                new PieSeries
                {
                    Title = "Hoàn thành",
                    Values = new ChartValues<int> { completedCount },
                    Fill = new SolidColorBrush(Colors.Green)
                }
            };

            // 2. Biểu đồ tròn theo Ưu tiên
            viewModel.PriorityPieSeries = new SeriesCollection
            {
                new PieSeries
                {
                    Title = "Thấp",
                    Values = new ChartValues<int> { lowCount },
                    Fill = new SolidColorBrush(Colors.LightGreen)
                },
                new PieSeries
                {
                    Title = "Trung bình",
                    Values = new ChartValues<int> { mediumCount },
                    Fill = new SolidColorBrush(Colors.Orange)
                },
                new PieSeries
                {
                    Title = "Cao",
                    Values = new ChartValues<int> { highCount },
                    Fill = new SolidColorBrush(Colors.Red)
                }
            };

            return viewModel;
        }

        // Hàm mới để tạo biểu đồ theo tháng
        private void CreateMonthlyChart()
        {
            var now = DateTime.Now;
            var currentYear = now.Year;

            // Tạo mảng 12 phần tử, mỗi phần tử đại diện cho số task của tháng tương ứng (0-11)
            var monthlyCounts = new int[12];

            // Duyệt qua tất cả task và đếm theo tháng tạo (CreatedDate)
            foreach (var task in _allTasks)
            {
                // --- SỬA: Kiểm tra Deadline.HasValue trước khi truy cập Value ---
                if (!task.Deadline.HasValue)
                {
                    continue; // Bỏ qua task nếu không có Deadline
                }

                var taskDate = task.Deadline.Value; // Lấy ngày từ Deadline

                // Chỉ tính task trong năm hiện tại
                if (taskDate.Year == currentYear)
                {
                    // Tháng trong DateTime là 1-12, mảng là 0-11
                    monthlyCounts[taskDate.Month - 1]++;
                }
            }

            // --- Tạo Series cho biểu đồ ---
            var monthlySeries = new ColumnSeries
            {
                Title = $"Năm {currentYear}",
                Values = new ChartValues<int>(monthlyCounts),
                Fill = new SolidColorBrush(Colors.CornflowerBlue)
            };

            // --- Tạo trục X với tên tháng ---
            var monthLabels = Enumerable.Range(1, 12)
                                        .Select(i => CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(i))
                                        .ToArray();

            var xAxis = new Axis
            {
                Title = "Tháng",
                Labels = monthLabels,
                Separator = new Separator { Step = 1 } // Đảm bảo mỗi cột ứng với một nhãn
            };

            // --- Tạo trục Y ---
            var yAxis = new Axis
            {
                Title = "Số lượng Task",
                MinValue = 0,
                LabelFormatter = value => value.ToString("F0"),
                Separator = new Separator { Step = 1 } // <-- quan trọng
            };

            // --- Gán dữ liệu vào biểu đồ ---
            if (MonthlyTasksChart != null)
            {
                MonthlyTasksChart.Series = new SeriesCollection { monthlySeries };
                // Xóa trục cũ (nếu cần) để tránh dồn trục khi gọi lại phương thức
                MonthlyTasksChart.AxisX.Clear();
                MonthlyTasksChart.AxisY.Clear();
                MonthlyTasksChart.AxisX.Add(xAxis);
                MonthlyTasksChart.AxisY.Add(yAxis);
                MonthlyTasksChart.LegendLocation = LegendLocation.Right;
            }
        }
    }
}