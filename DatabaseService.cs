// DatabaseService.cs
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Globalization; // Thêm để sử dụng DateTimeStyles

namespace TodoListApp
{
    public class DatabaseService
    {
        private readonly string _connectionString;
        private readonly string _databasePath;

        public DatabaseService()
        {
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var appFolder = Path.Combine(appDataPath, "TodoListApp");
            Directory.CreateDirectory(appFolder);

            _databasePath = Path.Combine(appFolder, "TodoList.db");
            _connectionString = $"Data Source={_databasePath}";

            InitializeDatabase();
        }

        private void InitializeDatabase()
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var createTableCommand = @"
            CREATE TABLE IF NOT EXISTS Tasks (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Title TEXT NOT NULL,
                Description TEXT,
                Status INTEGER NOT NULL DEFAULT 0,
                Priority INTEGER NOT NULL DEFAULT 2,
                Deadline TEXT,
                ReminderEnabled INTEGER NOT NULL DEFAULT 0,
                ReminderTime TEXT,
                CreatedDate TEXT NOT NULL,
                UpdatedDate TEXT NOT NULL,
                IsRepeating INTEGER NOT NULL DEFAULT 0,
                RepeatFrequency INTEGER NOT NULL DEFAULT 0
            );";

            using var command = new SqliteCommand(createTableCommand, connection);
            command.ExecuteNonQuery();

            // Tạo index cho tìm kiếm nhanh hơn theo Status và Deadline/ReminderTime nếu chưa có
            // Index cho Status
            var createIndexStatusCommand = "CREATE INDEX IF NOT EXISTS idx_tasks_status ON Tasks(Status);";
            using var indexStatusCommand = new SqliteCommand(createIndexStatusCommand, connection);
            indexStatusCommand.ExecuteNonQuery();

            // Index cho Deadline (cho truy vấn nhanh hơn)
            var createIndexDeadlineCommand = "CREATE INDEX IF NOT EXISTS idx_tasks_deadline ON Tasks(Deadline);";
            using var indexDeadlineCommand = new SqliteCommand(createIndexDeadlineCommand, connection);
            indexDeadlineCommand.ExecuteNonQuery();

            // Index cho ReminderTime (cho truy vấn nhanh hơn)
            var createIndexReminderTimeCommand = "CREATE INDEX IF NOT EXISTS idx_tasks_remindertime ON Tasks(ReminderTime);";
            using var indexReminderTimeCommand = new SqliteCommand(createIndexReminderTimeCommand, connection);
            indexReminderTimeCommand.ExecuteNonQuery();
        }

        public List<TodoTask> GetAllTasks()
        {
            var tasks = new List<TodoTask>();

            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var selectCommand = @"
    SELECT Id, Title, Description, Status, Priority, Deadline, ReminderEnabled, ReminderTime, CreatedDate, UpdatedDate, IsRepeating, RepeatFrequency
    FROM Tasks 
    ORDER BY UpdatedDate DESC";

            using var command = new SqliteCommand(selectCommand, connection);
            using var reader = command.ExecuteReader();

            while (reader.Read())
            {
                // Đọc và parse CreatedDate, UpdatedDate (không thể null)
                // Sử dụng DateTimeStyles.AssumeLocal để xử lý tốt hơn nếu chuỗi không có timezone
                if (!DateTime.TryParse(reader.GetString("CreatedDate"), null, DateTimeStyles.AssumeLocal, out DateTime createdDate))
                    createdDate = DateTime.Now; // Fallback nếu parse thất bại

                if (!DateTime.TryParse(reader.GetString("UpdatedDate"), null, DateTimeStyles.AssumeLocal, out DateTime updatedDate))
                    updatedDate = DateTime.Now; // Fallback nếu parse thất bại

                DateTime? deadline = null;
                if (!reader.IsDBNull("Deadline"))
                {
                    // Sử dụng DateTimeStyles.AssumeLocal
                    if (DateTime.TryParse(reader.GetString("Deadline"), null, DateTimeStyles.AssumeLocal, out DateTime dl))
                        deadline = dl;
                }

                DateTime? reminderTime = null;
                if (!reader.IsDBNull("ReminderTime"))
                {
                    // Sử dụng DateTimeStyles.AssumeLocal
                    if (DateTime.TryParse(reader.GetString("ReminderTime"), null, DateTimeStyles.AssumeLocal, out DateTime rt))
                        reminderTime = rt;
                }
                bool isRepeating = reader.GetBoolean("IsRepeating");
                RepeatFrequency repeatFreq = (RepeatFrequency)reader.GetInt32("RepeatFrequency");


                // Sử dụng constructor có tham số
                var task = new TodoTask(
                    reader.GetString("Title"),
                    reader.IsDBNull("Description") ? "" : reader.GetString("Description")
                )
                {
                    Id = reader.GetInt32("Id"),
                    Status = (TaskStatus)reader.GetInt32("Status"),
                    CreatedDate = createdDate, // Sử dụng giá trị đã parse
                    UpdatedDate = updatedDate, // Sử dụng giá trị đã parse
                    Priority = reader.GetInt32("Priority"),
                    Deadline = deadline,
                    ReminderEnabled = reader.GetBoolean("ReminderEnabled"),
                    ReminderTime = reminderTime,
                    IsRepeating = isRepeating, // Gán giá trị
                    RepeatFrequency = repeatFreq // Gán giá trị
                };

                tasks.Add(task);
            }

            return tasks;
        }

        /// <summary>
        /// Lấy tất cả các task có Status = InProgress và có ReminderTime hoặc Deadline trong ngày hôm nay.
        /// </summary>
        /// <returns>Danh sách TodoTask phù hợp.</returns>
        public static List<TodoTask> GetTodaysInProgressTasks() // Làm static để ReminderService có thể gọi dễ dàng
        {
            var tasks = new List<TodoTask>();
            // Cần lấy connectionString. Cách đơn giản nhất là tạo một instance mới để lấy nó.
            // Hoặc có thể lưu connectionString vào một static variable (ít được khuyến khích vì có thể gây rắc rối với lifecycle).
            // Cách tốt hơn là truyền instance DatabaseService vào ReminderService, nhưng yêu cầu thay đổi thiết kế.
            // Ở đây, ta sẽ tạo instance tạm thời để lấy connectionString.
            var tempService = new DatabaseService();
            var connectionString = tempService._connectionString; // Truy cập private field - không lý tưởng nhưng tạm thời ổn.

            using var connection = new SqliteConnection(connectionString);
            connection.Open();

            var now = DateTime.Now;
            var startOfDay = now.Date.ToString("o"); // ISO 8601 format cho ngày bắt đầu
            var endOfDay = now.Date.AddDays(1).AddTicks(-1).ToString("o"); // ISO 8601 format cho ngày kết thúc

            // Truy vấn để lấy task InProgress có ReminderTime HOẶC Deadline trong ngày hôm nay
            // Sử dụng OR và kiểm tra NULL
            var selectCommand = @"
                SELECT Id, Title, Description, Status, Priority, Deadline, ReminderEnabled, ReminderTime, CreatedDate, UpdatedDate 
                FROM Tasks 
                WHERE Status = @Status -- Chỉ lấy task Chưa hoàn thành
                  AND (
                    (ReminderTime >= @StartOfDay AND ReminderTime <= @EndOfDay) OR
                    (Deadline >= @StartOfDay AND Deadline <= @EndOfDay)
                  )
                ORDER BY UpdatedDate DESC";

            using var command = new SqliteCommand(selectCommand, connection);
            command.Parameters.AddWithValue("@Status", (int)TaskStatus.InProgress); // Chuyển enum sang int
            command.Parameters.AddWithValue("@StartOfDay", startOfDay);
            command.Parameters.AddWithValue("@EndOfDay", endOfDay);

            using var reader = command.ExecuteReader();

            while (reader.Read())
            {
                // Parse ngày tháng tương tự như trong GetAllTasks
                if (!DateTime.TryParse(reader.GetString("CreatedDate"), null, DateTimeStyles.AssumeLocal, out DateTime createdDate))
                    createdDate = DateTime.Now;

                if (!DateTime.TryParse(reader.GetString("UpdatedDate"), null, DateTimeStyles.AssumeLocal, out DateTime updatedDate))
                    updatedDate = DateTime.Now;

                DateTime? deadline = null;
                if (!reader.IsDBNull("Deadline"))
                {
                    if (DateTime.TryParse(reader.GetString("Deadline"), null, DateTimeStyles.AssumeLocal, out DateTime dl))
                        deadline = dl;
                }

                DateTime? reminderTime = null;
                if (!reader.IsDBNull("ReminderTime"))
                {
                    if (DateTime.TryParse(reader.GetString("ReminderTime"), null, DateTimeStyles.AssumeLocal, out DateTime rt))
                        reminderTime = rt;
                }

                var task = new TodoTask(
                    reader.GetString("Title"),
                    reader.IsDBNull("Description") ? "" : reader.GetString("Description")
                )
                {
                    Id = reader.GetInt32("Id"),
                    Status = (TaskStatus)reader.GetInt32("Status"),
                    CreatedDate = createdDate,
                    UpdatedDate = updatedDate,
                    Priority = reader.GetInt32("Priority"),
                    Deadline = deadline,
                    ReminderEnabled = reader.GetBoolean("ReminderEnabled"),
                    ReminderTime = reminderTime,
                };

                tasks.Add(task);
            }

            return tasks;
        }

        public int InsertTask(TodoTask task)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var insertCommand = @"
            INSERT INTO Tasks (Title, Description, Status, Priority, Deadline, ReminderEnabled, ReminderTime, CreatedDate, UpdatedDate, IsRepeating, RepeatFrequency)
            VALUES (@Title, @Description, @Status, @Priority, @Deadline, @ReminderEnabled, @ReminderTime, @CreatedDate, @UpdatedDate, @IsRepeating, @RepeatFrequency);
            SELECT last_insert_rowid();";

            using var command = new SqliteCommand(insertCommand, connection);

            command.Parameters.AddWithValue("@Title", task.Title);
            command.Parameters.AddWithValue("@Description", task.Description ?? "");
            command.Parameters.AddWithValue("@Status", (int)task.Status);
            command.Parameters.AddWithValue("@Priority", task.Priority);
            command.Parameters.AddWithValue("@CreatedDate", task.CreatedDate.ToString("o"));
            command.Parameters.AddWithValue("@UpdatedDate", task.UpdatedDate.ToString("o"));
            command.Parameters.AddWithValue("@Deadline", task.Deadline?.ToString("o") ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@ReminderEnabled", task.ReminderEnabled);
            command.Parameters.AddWithValue("@ReminderTime", task.ReminderTime?.ToString("o") ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@IsRepeating", task.IsRepeating);
            command.Parameters.AddWithValue("@RepeatFrequency", (int)task.RepeatFrequency);

            var result = command.ExecuteScalar();
            return Convert.ToInt32(result);
        }

        public bool UpdateTask(TodoTask task)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var updateCommand = @"
            UPDATE Tasks 
            SET Title = @Title, 
                Description = @Description, 
                Status = @Status, 
                Priority = @Priority,
                Deadline = @Deadline,
                ReminderEnabled = @ReminderEnabled,
                ReminderTime = @ReminderTime,
                UpdatedDate = @UpdatedDate,
                IsRepeating = @IsRepeating,
                RepeatFrequency = @RepeatFrequency
            WHERE Id = @Id";

            using var command = new SqliteCommand(updateCommand, connection);
            command.Parameters.AddWithValue("@Id", task.Id);
            command.Parameters.AddWithValue("@Title", task.Title);
            command.Parameters.AddWithValue("@Description", task.Description ?? "");
            command.Parameters.AddWithValue("@Status", (int)task.Status);
            command.Parameters.AddWithValue("@Priority", task.Priority);
            command.Parameters.AddWithValue("@UpdatedDate", task.UpdatedDate.ToString("o"));
            command.Parameters.AddWithValue("@Deadline", task.Deadline?.ToString("o") ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@ReminderEnabled", task.ReminderEnabled);
            command.Parameters.AddWithValue("@ReminderTime", task.ReminderTime?.ToString("o") ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@IsRepeating", task.IsRepeating);
            command.Parameters.AddWithValue("@RepeatFrequency", (int)task.RepeatFrequency);

            return command.ExecuteNonQuery() > 0;
        }

        public bool DeleteTask(int taskId)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var deleteCommand = "DELETE FROM Tasks WHERE Id = @Id";
            using var command = new SqliteCommand(deleteCommand, connection);
            command.Parameters.AddWithValue("@Id", taskId);

            return command.ExecuteNonQuery() > 0;
        }

        public List<TodoTask> GetTasksByStatus(TaskStatus status)
        {
            // *** SỬA LỖI: Parse ngày tháng chính xác hơn ***
            var tasks = new List<TodoTask>();

            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var selectCommand = @"
            SELECT Id, Title, Description, Status, Priority, Deadline, ReminderEnabled, ReminderTime, CreatedDate, UpdatedDate 
            FROM Tasks 
            WHERE Status = @Status
            ORDER BY UpdatedDate DESC";

            using var command = new SqliteCommand(selectCommand, connection);
            command.Parameters.AddWithValue("@Status", (int)status);
            using var reader = command.ExecuteReader();

            while (reader.Read())
            {
                // Parse ngày tháng tương tự như trong GetAllTasks
                if (!DateTime.TryParse(reader.GetString("CreatedDate"), null, DateTimeStyles.AssumeLocal, out DateTime createdDate))
                    createdDate = DateTime.Now;

                if (!DateTime.TryParse(reader.GetString("UpdatedDate"), null, DateTimeStyles.AssumeLocal, out DateTime updatedDate))
                    updatedDate = DateTime.Now;

                DateTime? deadline = null;
                if (!reader.IsDBNull("Deadline"))
                {
                    if (DateTime.TryParse(reader.GetString("Deadline"), null, DateTimeStyles.AssumeLocal, out DateTime dl))
                        deadline = dl;
                }

                DateTime? reminderTime = null;
                if (!reader.IsDBNull("ReminderTime"))
                {
                    if (DateTime.TryParse(reader.GetString("ReminderTime"), null, DateTimeStyles.AssumeLocal, out DateTime rt))
                        reminderTime = rt;
                }

                var task = new TodoTask(
                    reader.GetString("Title"),
                    reader.IsDBNull("Description") ? "" : reader.GetString("Description")
                )
                {
                    Id = reader.GetInt32("Id"),
                    Status = (TaskStatus)reader.GetInt32("Status"),
                    CreatedDate = createdDate,
                    UpdatedDate = updatedDate,
                    Priority = reader.GetInt32("Priority"),
                    Deadline = deadline,
                    ReminderEnabled = reader.GetBoolean("ReminderEnabled"),
                    ReminderTime = reminderTime,
                };

                tasks.Add(task);
            }

            return tasks;
        }

        public void ClearAllTasks()
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var deleteCommand = "DELETE FROM Tasks";
            using var command = new SqliteCommand(deleteCommand, connection);
            command.ExecuteNonQuery();
        }
    }
}