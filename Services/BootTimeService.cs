using System;
using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader; // specific for EventLogReader
using System.IO;
using System.Linq;
using System.Text.Json;
using DailyUptimeWidget.Models;

namespace DailyUptimeWidget.Services
{
    public class BootTimeService : IBootTimeService
    {
        private readonly string _stateFilePath;
        private BootTimeResult? _cachedResult;

        public BootTimeService()
        {
            var appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DailyUptimeWidget");
            Directory.CreateDirectory(appDataPath);
            _stateFilePath = Path.Combine(appDataPath, "appstate.json");
        }

        public void ResetState()
        {
            if (File.Exists(_stateFilePath))
            {
                File.Delete(_stateFilePath);
            }
        }

        public BootTimeResult CheckAndGetFirstBootTime()
        {
            if (_cachedResult != null)
            {
                App.Log($"Returning CACHED BootTime: {_cachedResult.BootTime} (Source: {_cachedResult.Source})");
                return _cachedResult;
            }

            var today = DateTime.Today;
            App.Log($"Checking boot time sources for {today:yyyy-MM-dd}...");

            // Sources
            DateTime? wmiBootTime = GetBootTimeFromWmi();
            DateTime? eventLogBootTime = GetFirstBootTimeFromEventLog(today);
            
            // Build a list of candidates
            var candidates = new List<(DateTime Time, string Source)>();

            // 1. WMI
            if (wmiBootTime.HasValue && wmiBootTime.Value.Date == today)
            {
                candidates.Add((wmiBootTime.Value, "WMI"));
                App.Log($"Candidate WMI: {wmiBootTime.Value}");
            }

            // 2. Event Log
            if (eventLogBootTime.HasValue)
            {
                candidates.Add((eventLogBootTime.Value, "EventLog"));
                App.Log($"Candidate EventLog: {eventLogBootTime.Value}");
            }

            // 3. Fallback (System Tick)
            var tickBootTime = DateTime.Now - TimeSpan.FromMilliseconds(Environment.TickCount64);
            if (tickBootTime.Date == today)
            {
                 candidates.Add((tickBootTime, "SystemTick"));
                 App.Log($"Candidate SystemTick: {tickBootTime}");
            }

            // 4. Saved State
            BootState? state = LoadState();
            if (state != null && state.LastRecordedDate.Date == today)
            {
                candidates.Add((state.FirstBootTime, state.Source ?? "SavedState"));
                App.Log($"Candidate SavedState: {state.FirstBootTime} (from {state.Source})");
            }

            if (candidates.Any())
            {
                var bestCandidate = candidates.OrderBy(c => c.Time).First();
                App.Log($"Selected Best Candidate: {bestCandidate.Time} from {bestCandidate.Source}");

                // If the best candidate is effectively different from saved state, update state
                bool needSave = false;
                if (state == null || state.LastRecordedDate.Date != today)
                {
                    var existingTasks = state?.DailyTasks ?? new List<string>();
                    var autoStart = state?.IsAutoStartEnabled ?? false;
                    state = new BootState 
                    { 
                        LastRecordedDate = today,
                        DailyTasks = existingTasks,
                        IsAutoStartEnabled = autoStart
                    };
                    needSave = true;
                }

                if (Math.Abs((state.FirstBootTime - bestCandidate.Time).TotalSeconds) > 10)
                {
                    state.FirstBootTime = bestCandidate.Time;
                    state.Source = bestCandidate.Source;
                     needSave = true;
                     App.Log($"Updating saved state with new best candidate.");
                }

                if (needSave) SaveState(state);

                _cachedResult = new BootTimeResult 
                { 
                    BootTime = state.FirstBootTime, 
                    Source = state.Source ?? "Unknown" 
                };
                return _cachedResult;
            }
            
            App.Log("WARNING: No candidates found. Using SystemTick emergency fallback.");
            _cachedResult = new BootTimeResult 
            { 
                BootTime = tickBootTime, 
                Source = "SystemTick (Emergency)" 
            };
            return _cachedResult;
        }

        public void InvalidateCache()
        {
            _cachedResult = null;
            App.Log("BootTimeService Cache Invalidated.");
        }

        private DateTime? GetBootTimeFromWmi()
        {
            try
            {
                // Needs System.Management
                using var searcher = new System.Management.ManagementObjectSearcher("SELECT LastBootUpTime FROM Win32_OperatingSystem");
                foreach (var mo in searcher.Get())
                {
                    var lastBootUpTime = mo["LastBootUpTime"]?.ToString();
                    if (!string.IsNullOrEmpty(lastBootUpTime))
                    {
                        return System.Management.ManagementDateTimeConverter.ToDateTime(lastBootUpTime);
                    }
                }
            }
            catch 
            {
                // Ignore WMI errors
            }
            return null;
        }

        private DateTime? GetFirstBootTimeFromEventLog(DateTime date)
        {
            try
            {
                // Fix: Convert Local 'Today' to UTC for the query, because EventLog TimeCreated is UTC-indexed.
                // date (00:00 Local) -> ToUniversalTime() might be yesterday UTC.
                // We want events that happened AFTER 'Yesterday UTC' corresponding to 'Today Local 00:00'.
                var utcStartTime = date.ToUniversalTime();
                
                string queryString = "*[System[(EventID=1 or EventID=12 or EventID=6005 or EventID=6009) and TimeCreated[@SystemTime>='" 
                                     + utcStartTime.ToString("yyyy-MM-ddTHH:mm:ss.0000000Z") + "']]]";

                var query = new EventLogQuery("System", PathType.LogName, queryString);
                using var reader = new EventLogReader(query);

                DateTime? earliest = null;

                for (EventRecord eventInstance = reader.ReadEvent(); 
                     eventInstance != null; 
                     eventInstance = reader.ReadEvent())
                {
                    var time = eventInstance.TimeCreated;
                    if (time.HasValue)
                    {
                        if (earliest == null || time.Value < earliest.Value)
                        {
                            earliest = time.Value;
                        }
                    }
                }
                
                return earliest;
            }
            catch
            {
                return null;
            }
        }

        private BootState? LoadState()
        {
            if (!File.Exists(_stateFilePath)) return null;
            try { return JsonSerializer.Deserialize<BootState>(File.ReadAllText(_stateFilePath)); }
            catch { return null; }
        }

        private void SaveState(BootState state)
        {
            try { File.WriteAllText(_stateFilePath, JsonSerializer.Serialize(state)); }
            catch { }
        }

        public bool GetSavedAutoStartEnabled()
        {
            var state = LoadState();
            return state?.IsAutoStartEnabled ?? false;
        }

        public void SaveAutoStartEnabled(bool enabled)
        {
            var state = LoadState() ?? new BootState { LastRecordedDate = DateTime.Today };
            state.IsAutoStartEnabled = enabled;
            SaveState(state);
        }

        public System.Collections.Generic.List<string> GetDailyTasks()
        {
            var state = LoadState();
            return state?.DailyTasks ?? new List<string>();
        }

        public void SaveDailyTasks(System.Collections.Generic.List<string> tasks)
        {
            var state = LoadState() ?? new BootState { LastRecordedDate = DateTime.Today };
            state.DailyTasks = tasks;
            SaveState(state);
        }
    }
}
