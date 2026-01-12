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

        public DailyTaskViewModel(string text, Action onSaveRequested, Action<DailyTaskViewModel> onDeleteRequested)
        {
            _text = text;
            _onSaveRequested = onSaveRequested;
            _onDeleteRequested = onDeleteRequested;
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
    }
}
