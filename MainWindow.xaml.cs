using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using DailyUptimeWidget.ViewModels;

namespace DailyUptimeWidget
{
    public partial class MainWindow : Window
    {
        [DllImport("user32.dll", SetLastError = true)]
        static extern IntPtr FindWindow(string lpClassName, string? lpWindowName);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr", SetLastError = true)]
        static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [DllImport("user32.dll", EntryPoint = "SetWindowLong", SetLastError = true)]
        static extern int SetWindowLong32(IntPtr hWnd, int nIndex, int dwNewLong);

        static IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong)
        {
            if (IntPtr.Size == 8)
                return SetWindowLongPtr64(hWnd, nIndex, dwNewLong);
            else
                return new IntPtr(SetWindowLong32(hWnd, nIndex, dwNewLong.ToInt32()));
        }

        [DllImport("user32.dll", SetLastError = true)]
        static extern IntPtr SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        private static readonly IntPtr HWND_BOTTOM = new IntPtr(1);
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOACTIVATE = 0x0010;
        private const int GWL_HWNDPARENT = -8;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // 1. Auto-position to Top-Right
            var workArea = SystemParameters.WorkArea;
            var margin = 20;
            this.Left = workArea.Right - this.Width - margin;
            this.Top = workArea.Top + margin;

            // 2. Stable Bottom-Most & Desktop Persistence Trick
            try 
            {
                var helper = new WindowInteropHelper(this);
                IntPtr progman = FindWindow("Progman", null);
                
                if (progman != IntPtr.Zero)
                {
                    // Set Progman as the Owner (not Parent!) to stay on desktop during Win+D
                    SetWindowLongPtr(helper.Handle, GWL_HWNDPARENT, progman);
                    
                    // Push to bottom of Z-order
                    SetWindowPos(helper.Handle, HWND_BOTTOM, 0, 0, 0, 0, SWP_NOSIZE | SWP_NOMOVE | SWP_NOACTIVATE);
                    
                    App.Log("Widget pinned as Shell Owner successfully.");
                }
            }
            catch (Exception ex)
            {
                App.Log($"Error pinning window: {ex.Message}");
            }
        }

        private void TextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is DailyTaskViewModel vm)
            {
                if (vm.FinishEditCommand.CanExecute(null))
                {
                    vm.FinishEditCommand.Execute(null);
                }
            }
        }

        private void MainBorder_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            // Focus common to clear focus from any active editing TextBox
            this.Focus();
        }
    }
}
