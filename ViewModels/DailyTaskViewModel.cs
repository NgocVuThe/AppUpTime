using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;

namespace DailyUptimeWidget.ViewModels
{
    public partial class DailyTaskViewModel : ObservableObject
    {
        private readonly Action _onSaveRequested;
        private readonly Action<DailyTaskViewModel> _onDeleteRequested;

        private string? _originalText;

        [ObservableProperty]
        private string _text;

        [ObservableProperty]
        private bool _isEditing;

        [ObservableProperty]
        private bool _isConfirming;

        [ObservableProperty]
        private bool _isDeleting;

        [ObservableProperty]
        private bool _isRemoving;

        [ObservableProperty]
        private bool _isPickingTime;

        [ObservableProperty]
        private bool _isConfirmingNotification;

        [ObservableProperty]
        private bool _isConfirmingDisableNotification;

        [ObservableProperty]
        private DateTime? _notificationTime;

        [ObservableProperty]
        private bool _isNotificationEnabled;

        [ObservableProperty]
        private string _timePart = DateTime.Now.ToString("HH:mm");

        [ObservableProperty]
        private DateTime? _tempNotificationTime;

        private string _tempTimePart = DateTime.Now.ToString("HH:mm");
        public string TempTimePart
        {
            get => _tempTimePart;
            set
            {
                var sanitized = SanitizeTime(value);
                if (SetProperty(ref _tempTimePart, sanitized))
                {
                    OnPropertyChanged(nameof(FormattedTempTime));
                }
            }
        }

        private string SanitizeTime(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return "00:00";

            // Only allow digits and colon
            var digitsOnly = new string(System.Linq.Enumerable.ToArray(System.Linq.Enumerable.Where(input, c => char.IsDigit(c))));
            
            // Pad or truncate to 4 digits
            if (digitsOnly.Length < 4) digitsOnly = digitsOnly.PadRight(4, '0');
            if (digitsOnly.Length > 4) digitsOnly = digitsOnly.Substring(0, 4);

            int hours = int.Parse(digitsOnly.Substring(0, 2));
            int mins = int.Parse(digitsOnly.Substring(2, 2));

            if (hours > 23) hours = 23;
            if (mins > 59) mins = 59;

            return $"{hours:D2}:{mins:D2}";
        }

        public string FormattedTempTime => TempNotificationTime?.ToString("dd/MM/yyyy") + " " + TempTimePart;

        public DateTime? LastNotifiedTime { get; set; }

        public DailyTaskViewModel(string text, Action onSaveRequested, Action<DailyTaskViewModel> onDeleteRequested, DateTime? notificationTime = null, bool isNotificationEnabled = false)
        {
            _text = text;
            _onSaveRequested = onSaveRequested;
            _onDeleteRequested = onDeleteRequested;
            _notificationTime = notificationTime;
            _isNotificationEnabled = isNotificationEnabled;
            if (notificationTime.HasValue)
            {
                _timePart = notificationTime.Value.ToString("HH:mm");
            }
        }

        [RelayCommand]
        private void Delete()
        {
            IsEditing = false; // Hide TextBox immediately
            IsConfirming = false;
            IsDeleting = true;
        }

        [RelayCommand]
        private void ConfirmDelete()
        {
            IsDeleting = false;
            _onDeleteRequested?.Invoke(this);
        }

        [RelayCommand]
        private void CancelDelete()
        {
            IsDeleting = false;
        }

        [RelayCommand]
        private void EnableEdit()
        {
            _originalText = Text;
            IsEditing = true;
            IsConfirming = false;
            IsDeleting = false;
        }

        [RelayCommand]
        private void FinishEdit()
        {
            if (!IsEditing) return;

            // 1. Check if text actually changed
            if (Text == _originalText)
            {
                IsEditing = false;
                IsConfirming = false;
                return;
            }

            // 2. Clear editing state and show confirmation overlay
            IsEditing = false; 
            IsConfirming = true;
        }

        [RelayCommand]
        private void ConfirmSave()
        {
            IsEditing = false;
            IsConfirming = false;
            _onSaveRequested?.Invoke();
        }

        [RelayCommand]
        private void CancelSave()
        {
            // Revert
            Text = _originalText ?? Text;
            IsEditing = false;
            IsConfirming = false;
        }

        [RelayCommand]
        private void ToggleNotification()
        {
            if (IsNotificationEnabled)
            {
                IsConfirmingDisableNotification = true;
                IsPickingTime = false;
                IsConfirmingNotification = false;
            }
            else
            {
                var now = DateTime.Now;
                TempNotificationTime = NotificationTime ?? now.Date;
                TempTimePart = NotificationTime.HasValue ? TimePart : now.ToString("HH:mm");
                
                IsPickingTime = true;
                IsConfirmingDisableNotification = false;
                IsConfirmingNotification = false;
            }
        }

        [RelayCommand]
        private void ConfirmDisableNotification()
        {
            IsNotificationEnabled = false;
            IsConfirmingDisableNotification = false;
            _onSaveRequested?.Invoke();
        }

        [RelayCommand]
        private void CancelDisableNotification()
        {
            IsConfirmingDisableNotification = false;
        }

        [RelayCommand]
        private void CancelPicking()
        {
            IsPickingTime = false;
        }

        [RelayCommand]
        private void RequestSaveNotification()
        {
            // Ensure format is correct HH:mm
            if (string.IsNullOrWhiteSpace(TempTimePart))
            {
                TempTimePart = "08:00";
            }
            
            if (!TimeSpan.TryParse(TempTimePart, out _))
            {
                // Try to fix it or default
                TempTimePart = "08:00";
            }

            IsPickingTime = false;
            OnPropertyChanged(nameof(FormattedTempTime));
            IsConfirmingNotification = true;
        }

        public void AdjustTime(bool isHour, int delta)
        {
            if (!TimeSpan.TryParse(TempTimePart, out var ts))
                ts = new TimeSpan(8, 0, 0);

            if (isHour)
            {
                var hours = (ts.Hours + delta) % 24;
                if (hours < 0) hours += 24;
                ts = new TimeSpan(hours, ts.Minutes, 0);
            }
            else
            {
                var mins = (ts.Minutes + delta) % 60;
                if (mins < 0) mins += 60;
                ts = new TimeSpan(ts.Hours, mins, 0);
            }

            TempTimePart = ts.ToString(@"hh\:mm");
        }

        [RelayCommand]
        private void ConfirmNotification()
        {
            if (TempNotificationTime.HasValue && TimeSpan.TryParse(TempTimePart, out var ts))
            {
                NotificationTime = TempNotificationTime.Value.Date.Add(ts);
                TimePart = TempTimePart;
                IsNotificationEnabled = true;
                LastNotifiedTime = null; // Important: Clear cooldown on new schedule
            }
            IsConfirmingNotification = false;
            _onSaveRequested?.Invoke();
        }

        [RelayCommand]
        private void RejectNotification()
        {
            IsConfirmingNotification = false;
            // Back to picking or exit? User said "quay về trạng thái ban đầu", likely exit.
        }

        [RelayCommand]
        private void SetNotification(DateTime? time)
        {
            NotificationTime = time;
            if (time != null) IsNotificationEnabled = true;
            _onSaveRequested?.Invoke();
        }
    }
}
