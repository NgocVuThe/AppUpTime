using System;

namespace DailyUptimeWidget.Models
{
    public class BootState
    {
        public DateTime LastRecordedDate { get; set; }
        public DateTime FirstBootTime { get; set; }
        public string Source { get; set; } = "Unknown";
        public bool IsAutoStartEnabled { get; set; }
        public System.Collections.Generic.List<DailyTaskModel> DailyTasks { get; set; } = new();
    }
}
