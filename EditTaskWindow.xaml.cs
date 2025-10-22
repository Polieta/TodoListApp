using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace TodoListApp
{
    public partial class EditTaskWindow : Window
    {
        private TodoTask _task;
        private bool _isFormattingTextBox = false;

        public EditTaskWindow(TodoTask task)
        {
            InitializeComponent();
            _task = task ?? throw new ArgumentNullException(nameof(task));
            LoadTaskData();
        }

        private void LoadTaskData()
        {
            TitleTextBox.Text = _task.Title;
            DescriptionTextBox.Text = _task.Description;

            PriorityTextBox.Text = _task.Priority switch
            {
                2 => "🔴 Cao",
                1 => "🟡 Trung bình",
                0 => "🟢 Thấp",
                _ => "🟡 Trung bình"
            };

            if (_task.Deadline.HasValue)
            {
                DeadlineTextBox.Text = _task.Deadline.Value.ToString("dd/MM/yyyy");
                TimeTextBox.Text = _task.Deadline.Value.ToString("HH:mm");
            }

            EnableReminderCheckBox.IsChecked = _task.ReminderEnabled;
            SetReminderOffset();

            RepeatTaskCheckBox.IsChecked = _task.IsRepeating;
            if (_task.IsRepeating)
            {
                switch (_task.RepeatFrequency)
                {
                    case RepeatFrequency.Daily:
                        RepeatFrequencyComboBox.SelectedIndex = 0;
                        break;
                    case RepeatFrequency.Weekly:
                        RepeatFrequencyComboBox.SelectedIndex = 1;
                        break;
                    case RepeatFrequency.Monthly:
                        RepeatFrequencyComboBox.SelectedIndex = 2;
                        break;
                    default:
                        RepeatFrequencyComboBox.SelectedIndex = -1;
                        break;
                }
            }
            else
            {
                RepeatFrequencyComboBox.SelectedIndex = -1;
            }
        }

        private void SetReminderOffset()
        {
            if (_task.ReminderTime.HasValue && _task.Deadline.HasValue)
            {
                var timeDiff = _task.Deadline.Value - _task.ReminderTime.Value;
                int minutesBefore = (int)Math.Round(timeDiff.TotalMinutes);

                var itemToSelect = ReminderOffsetComboBox.Items
                    .OfType<ComboBoxItem>()
                    .FirstOrDefault(item =>
                        int.TryParse(item.Tag?.ToString(), out int mins) &&
                        mins == minutesBefore);

                if (itemToSelect != null)
                {
                    ReminderOffsetComboBox.SelectedItem = itemToSelect;
                }
                else
                {
                    ReminderOffsetComboBox.SelectedIndex = 0;
                }
            }
            else
            {
                ReminderOffsetComboBox.SelectedIndex = 0;
            }
        }

        private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left && e.ClickCount == 1)
            {
                this.DragMove();
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidateInput())
            {
                return;
            }

            SaveTaskData();
            DialogResult = true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private bool ValidateInput()
        {
            var title = TitleTextBox.Text.Trim();
            if (string.IsNullOrEmpty(title))
            {
                MessageBox.Show("Vui lòng nhập tiêu đề.", "Thiếu thông tin", MessageBoxButton.OK, MessageBoxImage.Warning);
                TitleTextBox.Focus();
                return false;
            }

            string dateText = DeadlineTextBox.Text.Trim();
            string timeText = TimeTextBox.Text.Trim();

            DateTime? parsedDeadline = null;
            if (!string.IsNullOrWhiteSpace(dateText))
            {
                if (!DateTime.TryParseExact(dateText, "dd/MM/yyyy", null, System.Globalization.DateTimeStyles.None, out DateTime parsedDate))
                {
                    MessageBox.Show("Định dạng ngày không hợp lệ. Vui lòng nhập đúng định dạng dd/MM/yyyy.", "Lỗi ngày", MessageBoxButton.OK, MessageBoxImage.Error);
                    DeadlineTextBox.Focus();
                    return false;
                }

                if (!string.IsNullOrWhiteSpace(timeText))
                {
                    if (DateTime.TryParseExact(timeText, "HH:mm", null, System.Globalization.DateTimeStyles.None, out DateTime parsedTime))
                    {
                        parsedDeadline = new DateTime(parsedDate.Year, parsedDate.Month, parsedDate.Day, parsedTime.Hour, parsedTime.Minute, 0);
                    }
                    else
                    {
                        MessageBox.Show("Định dạng giờ không hợp lệ. Vui lòng nhập đúng định dạng HH:mm (VD: 14:30).", "Lỗi giờ", MessageBoxButton.OK, MessageBoxImage.Error);
                        TimeTextBox.Focus();
                        return false;
                    }
                }
                else
                {
                    parsedDeadline = parsedDate.Date.AddHours(23).AddMinutes(59);
                }

                if (parsedDeadline <= DateTime.Now)
                {
                    MessageBox.Show("Ngày/Giờ Deadline không thể là thời gian trong quá khứ.", "Lỗi Deadline", MessageBoxButton.OK, MessageBoxImage.Warning);
                    if (!string.IsNullOrWhiteSpace(timeText))
                    {
                        TimeTextBox.Focus();
                    }
                    else
                    {
                        DeadlineTextBox.Focus();
                    }
                    return false;
                }
            }

            if (EnableReminderCheckBox.IsChecked == true)
            {
                if (string.IsNullOrWhiteSpace(dateText))
                {
                    MessageBox.Show("Vui lòng nhập deadline khi bật nhắc nhở.", "Thiếu Deadline", MessageBoxButton.OK, MessageBoxImage.Warning);
                    DeadlineTextBox.Focus();
                    return false;
                }
                else
                {
                    int minutesBefore = 5;
                    if (ReminderOffsetComboBox.SelectedItem is ComboBoxItem selectedItem &&
                        int.TryParse(selectedItem.Tag.ToString(), out int parsedMinutes))
                    {
                        minutesBefore = parsedMinutes;
                    }

                    DateTime? tempReminderTime = parsedDeadline?.AddMinutes(-minutesBefore);
                    if (tempReminderTime.HasValue && tempReminderTime.Value <= DateTime.Now)
                    {
                        MessageBox.Show("Thời gian nhắc nhở tính ra là thời gian trong quá khứ. Vui lòng chọn thời gian deadline hợp lệ hoặc giảm số phút nhắc nhở trước.", "Lỗi Nhắc nhở", MessageBoxButton.OK, MessageBoxImage.Warning);
                        ReminderOffsetComboBox.Focus();
                        return false;
                    }
                }
            }

            if (RepeatTaskCheckBox.IsChecked == true)
            {
                if (RepeatFrequencyComboBox.SelectedItem == null || RepeatFrequencyComboBox.SelectedIndex == -1)
                {
                    MessageBox.Show("Task được đánh dấu lặp lại. Vui lòng chọn tần suất lặp lại (Hàng ngày, Hàng tuần, Hàng tháng).", "Thiếu thông tin lặp lại", MessageBoxButton.OK, MessageBoxImage.Warning);
                    RepeatFrequencyComboBox.Focus();
                    return false;
                }
            }

            return true;
        }

        private void SaveTaskData()
        {
            _task.Title = TitleTextBox.Text.Trim();
            _task.Description = DescriptionTextBox.Text.Trim();
            _task.Priority = GetPriorityValue();
            _task.Deadline = ParseDeadline();
            _task.ReminderEnabled = EnableReminderCheckBox.IsChecked == true;
            _task.ReminderTime = CalculateReminderTime();
            _task.UpdatedDate = DateTime.Now;

            _task.IsRepeating = RepeatTaskCheckBox.IsChecked == true;
            if (_task.IsRepeating)
            {
                switch (RepeatFrequencyComboBox.SelectedIndex)
                {
                    case 0:
                        _task.RepeatFrequency = RepeatFrequency.Daily;
                        break;
                    case 1:
                        _task.RepeatFrequency = RepeatFrequency.Weekly;
                        break;
                    case 2:
                        _task.RepeatFrequency = RepeatFrequency.Monthly;
                        break;
                    default:
                        _task.RepeatFrequency = RepeatFrequency.None;
                        _task.IsRepeating = false;
                        break;
                }
            }
            else
            {
                _task.RepeatFrequency = RepeatFrequency.None;
            }
        }

        private void RepeatTaskCheckBox_CheckedOrUnchecked(object sender, RoutedEventArgs e)
        {
            // Không tự động chọn mặc định nữa
        }

        private int GetPriorityValue()
        {
            return PriorityTextBox.Text switch
            {
                string s when s.Contains("Cao") => 2,
                string s when s.Contains("Trung bình") => 1,
                string s when s.Contains("Thấp") => 0,
                _ => 1
            };
        }

        private DateTime? ParseDeadline()
        {
            string dateText = DeadlineTextBox.Text.Trim();
            string timeText = TimeTextBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(dateText))
                return null;

            if (DateTime.TryParseExact(dateText, "dd/MM/yyyy", null, System.Globalization.DateTimeStyles.None, out DateTime date))
            {
                if (!string.IsNullOrWhiteSpace(timeText))
                {
                    if (DateTime.TryParseExact(timeText, "HH:mm", null, System.Globalization.DateTimeStyles.None, out DateTime time))
                    {
                        return new DateTime(date.Year, date.Month, date.Day, time.Hour, time.Minute, 0);
                    }
                }
                else
                {
                    return date.Date.AddHours(23).AddMinutes(59);
                }
            }
            return null;
        }

        private DateTime? CalculateReminderTime()
        {
            if (EnableReminderCheckBox.IsChecked != true || !_task.Deadline.HasValue)
                return null;

            int minutesBefore = 5;
            if (ReminderOffsetComboBox.SelectedItem is ComboBoxItem selectedItem &&
                int.TryParse(selectedItem.Tag.ToString(), out int parsedMinutes))
            {
                minutesBefore = parsedMinutes;
            }

            DateTime calculatedReminderTime = _task.Deadline.Value.AddMinutes(-minutesBefore);
            return calculatedReminderTime;
        }

        private void ShowPriorityPopup_Click(object sender, RoutedEventArgs e)
        {
            var listBox = new ListBox();
            listBox.Items.Add(new ListBoxItem { Content = "🔴 Cao", Tag = 2 });
            listBox.Items.Add(new ListBoxItem { Content = "🟡 Trung bình", Tag = 1 });
            listBox.Items.Add(new ListBoxItem { Content = "🟢 Thấp", Tag = 0 });

            string currentText = PriorityTextBox.Text.Trim();
            for (int i = 0; i < listBox.Items.Count; i++)
            {
                if (listBox.Items[i] is ListBoxItem item && item.Content.ToString().Trim() == currentText)
                {
                    listBox.SelectedIndex = i;
                    break;
                }
            }

            var popup = new Popup
            {
                Child = listBox,
                PlacementTarget = sender as UIElement,
                Placement = PlacementMode.Bottom,
                StaysOpen = false,
                IsOpen = true,
                AllowsTransparency = true
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

        private void ShowCalendar_Click(object sender, RoutedEventArgs e)
        {
            var calendar = new Calendar();
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

        private void DateTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !Regex.IsMatch(e.Text, "^[0-9]+$");
        }

        private void TimeTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !Regex.IsMatch(e.Text, "^[0-9]+$");
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
                else
                    formatted = digits.Substring(0, 2) + "/" + digits.Substring(2, 2) + "/" + digits.Substring(4);
                tb.Text = formatted;
                tb.SelectionStart = Math.Min(formatted.Length, selStart);
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
                var digits = Regex.Replace(originalText, "\\D", "");
                if (digits.Length > 4)
                    digits = digits.Substring(0, 4);

                string formatted = digits;
                if (digits.Length >= 3)
                {
                    formatted = digits.Substring(0, 2) + ":" + digits.Substring(2);
                }
                if (tb.Text != formatted)
                {
                    tb.Text = formatted;
                    tb.SelectionStart = formatted.Length;
                }
            }
            finally
            {
                _isFormattingTextBox = false;
            }
        }

        private void ReminderCheckBox_Checked(object sender, RoutedEventArgs e)
        {
        }

        private void ReminderCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
        }
    }
}