using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace TodoListApp
{
    public class PriorityToBackgroundConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            int priority = (int)value;
            switch (priority)
            {
                case 0: // Thấp
                    return new SolidColorBrush(Colors.LightGreen);
                case 1: // Trung bình
                    return new SolidColorBrush(Colors.Yellow);
                case 2: // Cao
                    return new SolidColorBrush(Colors.Red);
                default:
                    return new SolidColorBrush(Colors.White);
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}