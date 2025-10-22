using System.Collections.Generic;

namespace TodoListApp
{
    public static class TodoManager
    {
        public static List<TodoTask> Tasks { get; set; } = new();
    }
}