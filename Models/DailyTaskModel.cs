using System;

namespace DailyUptimeWidget.Models
{
    public class DailyTaskModel
    {
        public string Text { get; set; } = string.Empty;
        public DateTime? NotificationTime { get; set; }
        public bool IsNotificationEnabled { get; set; }
    }
}
