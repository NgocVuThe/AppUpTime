using System;

namespace DailyUptimeWidget.Services
{
    public class BootTimeResult
    {
        public DateTime BootTime { get; set; }
        public string Source { get; set; } = "Unknown";
    }

    public interface IBootTimeService
    {
        BootTimeResult CheckAndGetFirstBootTime();
        void ResetState();
        bool GetSavedAutoStartEnabled();
        void SaveAutoStartEnabled(bool enabled);
        System.Collections.Generic.List<string> GetDailyTasks();
        void SaveDailyTasks(System.Collections.Generic.List<string> tasks);
        void InvalidateCache();
    }
}
