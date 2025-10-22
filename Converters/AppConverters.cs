using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace TodoListApp.Converters // Đặt trong namespace cha + thư mục Converters
{
    // Converter từ MainWindow
    public class PriorityToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int priority)
            {
                // Cập nhật lại logic phù hợp với giá trị Priority thực tế (0, 1, 2)
                // File này được dùng chung, nên cần logic linh hoạt hoặc comment rõ ràng
                // Giả sử theo logic mới: 0=Thấp, 1=TB, 2=Cao như trong TaskCalendarWindow
                switch (priority)
                {
                    case 2: return new SolidColorBrush(Colors.Red);    // Cao
                    case 1: return new SolidColorBrush(Colors.Orange); // TB
                    case 0: return new SolidColorBrush(Colors.Green);  // Thấp
                                                                       // Nếu theo logic cũ trong MainWindow (1=Cao, 2=TB, 3=Thấp), thì:
                                                                       // case 1: return new SolidColorBrush(Colors.Red);
                                                                       // case 2: return new SolidColorBrush(Colors.Orange);
                                                                       // case 3: return new SolidColorBrush(Colors.Green);
                                                                       // -> Bạn cần chọn 1 logic chung hoặc tạo converter riêng cho từng mục đích.
                }
                // Hoặc dùng một logic chung, ví dụ: 0=Thấp, 1=TB, 2=Cao
                // Thì giữ nguyên switch case phía trên.
            }
            return new SolidColorBrush(Colors.Gray);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class StatusToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is TaskStatus status)
            {
                switch (status)
                {
                    case TaskStatus.InProgress: return new SolidColorBrush(Colors.Orange);
                    case TaskStatus.Completed: return new SolidColorBrush(Colors.Green);
                }
            }
            return new SolidColorBrush(Colors.Gray);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    // Converter từ TaskCalendarWindow
    public class BoolToVisibilityConverter : IValueConverter
    {
        public bool Inverted { get; set; } // Thuộc tính để đảo ngược giá trị

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b)
            {
                return (Inverted ? !b : b) ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
            }
            return System.Windows.Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is System.Windows.Visibility v)
            {
                bool result = v == System.Windows.Visibility.Visible;
                return Inverted ? !result : result;
            }
            return false;
        }
    }

    public class EmptyDayBackgroundConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isEmpty)
            {
                return isEmpty ? new SolidColorBrush(Colors.LightGray) : new SolidColorBrush(Colors.White);
            }
            return new SolidColorBrush(Colors.White);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}