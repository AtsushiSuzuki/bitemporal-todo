using System;

namespace bitemporal_todo
{
    public class TaskItem
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public DateTime? DueDate { get; set; }
        public DateTime? CompletedAt { get; set; }
    }
}
