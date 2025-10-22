using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;

namespace TodoListApp
{
    public enum TaskStatus
    {
        InProgress,
        Completed
    }
    public enum RepeatFrequency
    {
        None, // Không lặp
        Daily, // Hàng ngày
        Weekly, // Hàng tuần
        Monthly // Hàng tháng
    }
    public class TodoTask : INotifyPropertyChanged
    {
        private int _id;
        private string _title;
        private string _description;
        private TaskStatus _status;
        private int _priority;
        private DateTime? _deadline;
        private bool _reminderEnabled;
        private DateTime? _reminderTime;
        private DateTime _createdDate;
        private DateTime _updatedDate;

        private bool _isRepeating;
        private RepeatFrequency _repeatFrequency;

        public int Id
        {
            get => _id;
            set { _id = value; OnPropertyChanged(); }
        }

        public string Title
        {
            get => _title;
            set { _title = value; OnPropertyChanged(); }
        }

        public string Description
        {
            get => _description;
            set { _description = value; OnPropertyChanged(); }
        }

        public TaskStatus Status
        {
            get => _status;
            set { _status = value; OnPropertyChanged(); OnPropertyChanged(nameof(StatusText)); }
        }

        public int Priority
        {
            get => _priority;
            set { _priority = value; OnPropertyChanged(); OnPropertyChanged(nameof(PriorityText)); }
        }

        public DateTime? Deadline
        {
            get => _deadline;
            set { _deadline = value; OnPropertyChanged(); OnPropertyChanged(nameof(FormattedDeadline)); }
        }

        public bool ReminderEnabled
        {
            get => _reminderEnabled;
            set { _reminderEnabled = value; OnPropertyChanged(); }
        }

        public DateTime? ReminderTime
        {
            get => _reminderTime;
            set { _reminderTime = value; OnPropertyChanged(); OnPropertyChanged(nameof(FormattedReminderTime)); }
        }

        public DateTime CreatedDate
        {
            get => _createdDate;
            set { _createdDate = value; OnPropertyChanged(); }
        }

        public DateTime UpdatedDate
        {
            get => _updatedDate;
            set { _updatedDate = value; OnPropertyChanged(); }
        }

        public bool IsRepeating
        {
            get => _isRepeating;
            set { _isRepeating = value; OnPropertyChanged(); }
        }

        public RepeatFrequency RepeatFrequency
        {
            get => _repeatFrequency;
            set { _repeatFrequency = value; OnPropertyChanged(); OnPropertyChanged(nameof(RepeatFrequencyText)); }
        }

        public string StatusText => Status switch
        {
            TaskStatus.InProgress => "Chưa xong",
            TaskStatus.Completed => "Hoàn thành",
            _ => "Không xác định"
        };

        public string PriorityText => Priority switch
        {
            0 => "Thấp",
            1 => "Trung bình",
            2 => "Cao",
            _ => "Không xác định"
        };

        public string FormattedDeadline => Deadline?.ToString("dd/MM/yyyy HH:mm") ?? "Không có";

        public string FormattedReminderTime => ReminderTime?.ToString("dd/MM/yyyy HH:mm") ?? "";

        public string RepeatFrequencyText => RepeatFrequency switch
        {
            RepeatFrequency.None => "Không lặp",
            RepeatFrequency.Daily => "Hàng ngày",
            RepeatFrequency.Weekly => "Hàng tuần",
            RepeatFrequency.Monthly => "Hàng tháng",
            _ => "Không xác định"
        };

        public DateTime? GetNextRepeatDate(DateTime currentDate)
        {
            if (!IsRepeating || RepeatFrequency == RepeatFrequency.None || !Deadline.HasValue)
                return null;

            DateTime nextDate = Deadline.Value.Date; // Lấy ngày từ Deadline gốc
            TimeSpan deadlineTime = Deadline.Value.TimeOfDay; // SỬA: Dùng TimeSpan, không phải DateTime

            switch (RepeatFrequency)
            {
                case RepeatFrequency.Daily:
                    nextDate = currentDate.Date.AddDays(1); // Lặp hàng ngày -> ngày mai
                    break;
                case RepeatFrequency.Weekly:
                    // Lặp hàng tuần -> ngày trong tuần tiếp theo (cùng thứ)
                    int daysUntilNextOccurrence = ((int)nextDate.DayOfWeek - (int)currentDate.DayOfWeek + 7) % 7;
                    if (daysUntilNextOccurrence == 0 && nextDate.TimeOfDay <= currentDate.TimeOfDay)
                    {
                        daysUntilNextOccurrence = 7; // Nếu hôm nay là ngày lặp nhưng giờ đã qua, lặp lại vào tuần sau
                    }
                    nextDate = currentDate.Date.AddDays(daysUntilNextOccurrence);
                    break;
                case RepeatFrequency.Monthly:
                    // Lặp hàng tháng -> cùng ngày trong tháng tiếp theo
                    int targetDay = nextDate.Day;
                    nextDate = currentDate.Date.AddMonths(1);
                    // Nếu ngày mục tiêu không tồn tại trong tháng mới (ví dụ: 31/04), chọn ngày cuối tháng
                    int daysInTargetMonth = DateTime.DaysInMonth(nextDate.Year, nextDate.Month);
                    if (targetDay > daysInTargetMonth)
                    {
                        targetDay = daysInTargetMonth;
                    }
                    nextDate = new DateTime(nextDate.Year, nextDate.Month, targetDay);
                    break;
                default:
                    return null; // Không hỗ trợ
            }
            return nextDate.Add(deadlineTime); // Add(TimeSpan) sẽ cộng khoảng thời gian vào ngày
        }

        public TodoTask(string title, string description)
        {
            Title = title;
            Description = description;
            Deadline = DateTime.Now;
            ReminderTime = DateTime.Now;
            ReminderEnabled= false;
            Status = TaskStatus.InProgress;
            Priority = 1; // Trung bình
            CreatedDate = DateTime.Now;
            UpdatedDate = DateTime.Now;
            IsRepeating = false;
            RepeatFrequency = RepeatFrequency.None;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}