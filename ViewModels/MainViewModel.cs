using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DailyUptimeWidget.Services;
using System;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Win32;
using Application = System.Windows.Application; // Fix ambiguity

namespace DailyUptimeWidget.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly IBootTimeService _bootTimeService;
        private readonly IStartupRegistryService _startupRegistryService;
        private readonly ITrayService _trayService; // Injected
        private DateTime _firstBootTime;
        private DispatcherTimer _timer;
        private bool _hasNotifiedTarget = false;

        [ObservableProperty]
        private string _uptimeString = "00:00:00";

        [ObservableProperty]
        private bool _isAutoStartEnabled;

        [ObservableProperty]
        private System.Collections.ObjectModel.ObservableCollection<DailyTaskViewModel> _tasks = new();

        public MainViewModel(IBootTimeService bootTimeService, IStartupRegistryService startupRegistryService, ITrayService trayService)
        {
            _bootTimeService = bootTimeService;
            _startupRegistryService = startupRegistryService;
            _trayService = trayService;
            Initialize();

            // Timer setup
            _timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromSeconds(1) };
            _timer.Tick += Timer_Tick;
            _timer.Start();

            SystemEvents.PowerModeChanged += OnPowerModeChanged;

            // Initial tick
            Timer_Tick(null, null);
        }

        private void Initialize()
        {
            // Initialize properties that depend on services
            var result = _bootTimeService.CheckAndGetFirstBootTime();
            _firstBootTime = result.BootTime;
            App.Log($"Detected BootTime: {_firstBootTime}, Source: {result.Source}");
            
            // Apply 8:00 AM Rule
            var limit = DateTime.Today.AddHours(8); // 8:00 AM today
            
            _effectiveStartTime = _firstBootTime < limit ? limit : _firstBootTime;
            
            if (_firstBootTime < limit)
            {
                TimeSource = $"{result.Source} | {result.BootTime:HH:mm} (Starts 08:00)";
            }
            else
            {
                TimeSource = $"{result.Source} | {result.BootTime:HH:mm:ss}";
            }

            IsAutoStartEnabled = _startupRegistryService.IsAutoStartEnabled;
            
            var savedTasks = _bootTimeService.GetDailyTasks();
            Tasks.Clear();
            foreach (var t in savedTasks)
            {
                Tasks.Add(new DailyTaskViewModel(t.Text, SaveTasks, RemoveTask, t.NotificationTime, t.IsNotificationEnabled));
            }
        }

        private void SaveTasks()
        {
            var taskModels = new System.Collections.Generic.List<Models.DailyTaskModel>();
            foreach(var t in Tasks)
            {
                taskModels.Add(new Models.DailyTaskModel 
                { 
                    Text = t.Text, 
                    NotificationTime = t.NotificationTime, 
                    IsNotificationEnabled = t.IsNotificationEnabled 
                });
            }
            _bootTimeService.SaveDailyTasks(taskModels);
        }

        private async void RemoveTask(DailyTaskViewModel task)
        {
            if (task != null && Tasks.Contains(task))
            {
                task.IsRemoving = true;
                await System.Threading.Tasks.Task.Delay(300); // Wait for animation
                Tasks.Remove(task);
                SaveTasks();
            }
        }

        [ObservableProperty]
        private string _currentTaskInput = "";

        [RelayCommand]
        private void AddTask()
        {
            if (string.IsNullOrWhiteSpace(CurrentTaskInput)) return;
            
            if (Tasks.Count < 5)
            {
                Tasks.Add(new DailyTaskViewModel(CurrentTaskInput.Trim(), SaveTasks, RemoveTask, null, false));
                SaveTasks();
                CurrentTaskInput = "";
            }
        }

        [ObservableProperty]
        private string _timeSource = "Initializing...";

        [ObservableProperty]
        private string _subStatusString = ""; // e.g. "Remaining: 03:00:00"

        private int _tickCount = 0;
        private DateTime _effectiveStartTime;

        private void Timer_Tick(object? sender, EventArgs? e)
        {
            try 
            {
                _tickCount++;
                var now = DateTime.Now;

                // Calculate duration from EFFECTIVE start time
                var workDuration = CalculateWorkDuration(_effectiveStartTime, now);
                
                UptimeString = workDuration.ToString(@"hh\:mm\:ss");

                // Target check (8 hours)
                var target = TimeSpan.FromHours(8);
                var remaining = target - workDuration;
                
                // Lunch logic info
                var lunchStart = DateTime.Today.AddHours(12);
                var lunchEnd = DateTime.Today.AddHours(13);
                bool isLunchNow = now >= lunchStart && now < lunchEnd;

                if (remaining.TotalSeconds > 0)
                {
                    // Calculate Estimated End Time
                    var estimatedEnd = _effectiveStartTime.AddHours(8);
                    
                    if (_effectiveStartTime < lunchStart && estimatedEnd > lunchStart)
                    {
                        estimatedEnd = estimatedEnd.AddHours(1);
                    }

                    string lunchStatus = isLunchNow ? " (Lunch Break)" : "";
                    SubStatusString = $"Rem: {remaining:hh\\:mm} | End: {estimatedEnd:HH:mm}{lunchStatus}";
                    _hasNotifiedTarget = false; 
                }
                else
                {
                    SubStatusString = "Target Reached! (8h)";
                    
                    // PUSH NOTIFICATION
                    if (!_hasNotifiedTarget)
                    {
                        _hasNotifiedTarget = true;
                        _trayService.ShowNotification("Daily Target Reached!", "You have worked for 8 hours. Good job!");
                    }
                }

                // CHECK TASK NOTIFICATIONS
                foreach (var task in Tasks)
                {
                    if (task.IsNotificationEnabled && task.NotificationTime.HasValue)
                    {
                        var triggerTime = task.NotificationTime.Value;
                        if (now >= triggerTime)
                        {
                            // Check for 10-minute repeat
                            if (task.LastNotifiedTime == null || (now - task.LastNotifiedTime.Value).TotalMinutes >= 10)
                            {
                                task.LastNotifiedTime = now;
                                _trayService.ShowNotification("Task Reminder", task.Text);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                App.Log($"Timer Tick Error: {ex.Message}");
            }
        }

        private TimeSpan CalculateWorkDuration(DateTime start, DateTime now)
        {
            if (now < start) return TimeSpan.Zero;
            
            // Lunch is 12:00 to 13:00
            
            var lunchStart = now.Date.AddHours(12);
            var lunchEnd = now.Date.AddHours(13);

            // 1. Calculate raw span
            var rawSpan = now - start;

            // 2. Calculate overlap with lunch
            // OverlapStart = Max(start, lunchStart)
            // OverlapEnd = Min(now, lunchEnd)
            
            var overlapStart = start > lunchStart ? start : lunchStart;
            var overlapEnd = now < lunchEnd ? now : lunchEnd;

            if (overlapStart < overlapEnd)
            {
                var overlap = overlapEnd - overlapStart;
                return rawSpan - overlap;
            }

            return rawSpan;
        }

        partial void OnIsAutoStartEnabledChanged(bool value)
        {
            // Sync with Registry whenever the property changes (UI toggle or code)
            _startupRegistryService.ToggleAutoStart(value);
        }


        [RelayCommand]
        private void ResetData()
        {
            _bootTimeService.ResetState();
            // Re-initialize to fetch fresh data
            Initialize();
        }

        [RelayCommand]
        private void ExitApp()
        {
            _trayService.Dispose(); // Ensure icon removal
            Application.Current.Shutdown();
        }

        [RelayCommand]
        private void MinimizeApp()
        {
             // Hide main window
             Application.Current.MainWindow.Hide();
             _trayService.ShowNotification("Widget Hidden", "Double-click the tray icon to restore.");
        }

        [RelayCommand]
        private void CloseApp()
        {
            ExitApp();
        }

        private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
        {
            if (e.Mode == PowerModes.Resume)
            {
                App.Log("System Resume Detected - Invalidating Cache & Re-checking...");
                _bootTimeService.InvalidateCache();

                // Wait a moment for network/services to stabilize if needed, then check
                System.Threading.Tasks.Task.Delay(2000).ContinueWith(_ => 
                {
                    try 
                    {
                        var result = _bootTimeService.CheckAndGetFirstBootTime();
                        
                        Application.Current.Dispatcher.InvokeAsync(() => 
                        {
                             // Update logic similar to Initialize
                            _firstBootTime = result.BootTime;
                            App.Log($"Resumed BootTime: {_firstBootTime}, Source: {result.Source}");
                            
                            var limit = DateTime.Today.AddHours(8);
                            if (_firstBootTime < limit)
                            {
                                _effectiveStartTime = limit;
                                TimeSource = $"{result.Source} | {result.BootTime:HH:mm} (Starts 08:00) [Resumed]";
                            }
                            else
                            {
                                _effectiveStartTime = _firstBootTime;
                                TimeSource = $"{result.Source} | {result.BootTime:HH:mm:ss} [Resumed]";
                            }
                        });
                    }
                    catch(Exception ex)
                    {
                        App.Log($"Resume Check Error: {ex.Message}");
                    }
                });
            }
        }

    }
}
