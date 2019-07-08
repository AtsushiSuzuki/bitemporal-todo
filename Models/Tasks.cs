using System;

namespace bitemporal_todo
{
    public class TaskItem
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public DateTime? Completed { get; set; }
        public DateTime? Deleted { get; set; }
    }

    public class TaskHistoryItem
    {
        public long HistoryId { get; set; }
        public string Id { get; set; }
        public string Title { get; set; }
        public DateTime? Completed { get; set; }
        public DateTime? ValidFrom { get; set; }
        public DateTime? ValidTo { get; set; }
    }
}
