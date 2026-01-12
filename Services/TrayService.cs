using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace DailyUptimeWidget.Services
{
    public interface ITrayService
    {
        void Initialize(Action onShowCommand, Action onExitCommand);
        void ShowNotification(string title, string message);
        void Dispose();
    }

    public class TrayService : ITrayService
    {
        private const int WM_TRAYICON = 0x0400 + 1;
        private const int TRAY_ID = 1;

        private const uint NIM_ADD = 0x00000000;
        private const uint NIM_MODIFY = 0x00000001;
        private const uint NIM_DELETE = 0x00000002;
        private const uint NIF_MESSAGE = 0x00000001;
        private const uint NIF_ICON = 0x00000002;
        private const uint NIF_TIP = 0x00000004;
        private const uint NIF_INFO = 0x00000010;

        private const int WM_LBUTTONDBLCLK = 0x0203;
        private const int WM_RBUTTONUP = 0x0205;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public struct NOTIFYICONDATA
        {
            public int cbSize;
            public IntPtr hWnd;
            public int uID;
            public uint uFlags;
            public int uCallbackMessage;
            public IntPtr hIcon;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string szTip;
            public int dwState;
            public int dwStateMask;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public string szInfo;
            public int uTimeoutOrVersion;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
            public string szInfoTitle;
            public int dwInfoFlags;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        private static extern bool Shell_NotifyIcon(uint dwMessage, [In] ref NOTIFYICONDATA lpData);

        [DllImport("user32.dll")]
        private static extern IntPtr CreatePopupMenu();

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool AppendMenu(IntPtr hMenu, uint uFlags, uint uIDNewItem, string lpNewItem);

        [DllImport("user32.dll")]
        private static extern uint TrackPopupMenu(IntPtr hMenu, uint uFlags, int x, int y, int nReserved, IntPtr hWnd, IntPtr prcRect);

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool DestroyMenu(IntPtr hMenu);

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT { public int X; public int Y; }

        private HwndSource? _hwndSource;
        private IntPtr _hIcon;
        private Action? _onShow;
        private Action? _onExit;

        public void Initialize(Action onShowCommand, Action onExitCommand)
        {
            _onShow = onShowCommand;
            _onExit = onExitCommand;

            // Create a hidden window to handle tray messages
            var window = new Window { Width = 0, Height = 0, WindowStyle = WindowStyle.None, ShowInTaskbar = false, Visibility = Visibility.Hidden };
            window.Show();
            var helper = new WindowInteropHelper(window);
            var hWnd = helper.Handle;

            _hwndSource = HwndSource.FromHwnd(hWnd);
            _hwndSource.AddHook(WndProc);

            // Load icon
            try
            {
                var uri = new System.Uri("pack://application:,,,/Assets/clock_icon.png");
                var bitmap = new BitmapImage(uri);
                _hIcon = GetHIcon(bitmap);
            }
            catch { _hIcon = IntPtr.Zero; }

            var nid = new NOTIFYICONDATA
            {
                cbSize = Marshal.SizeOf(typeof(NOTIFYICONDATA)),
                hWnd = hWnd,
                uID = TRAY_ID,
                uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP,
                uCallbackMessage = WM_TRAYICON,
                hIcon = _hIcon,
                szTip = "Daily Uptime Widget"
            };

            Shell_NotifyIcon(NIM_ADD, ref nid);
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_TRAYICON)
            {
                int eventMsg = (int)lParam;
                if (eventMsg == WM_LBUTTONDBLCLK)
                {
                    _onShow?.Invoke();
                    handled = true;
                }
                else if (eventMsg == WM_RBUTTONUP)
                {
                    ShowContextMenu(hwnd);
                    handled = true;
                }
            }
            return IntPtr.Zero;
        }

        private void ShowContextMenu(IntPtr hWnd)
        {
            IntPtr hMenu = CreatePopupMenu();
            AppendMenu(hMenu, 0, 1, "Show Widget");
            AppendMenu(hMenu, 0x00000800, 0, "-"); // MFT_SEPARATOR
            AppendMenu(hMenu, 0, 2, "Exit");

            GetCursorPos(out POINT point);
            SetForegroundWindow(hWnd);
            uint selected = TrackPopupMenu(hMenu, 0x0100, point.X, point.Y, 0, hWnd, IntPtr.Zero); // TPM_RETURNCMD

            if (selected == 1) _onShow?.Invoke();
            else if (selected == 2) _onExit?.Invoke();

            DestroyMenu(hMenu);
        }

        public void ShowNotification(string title, string message)
        {
            if (_hwndSource == null) return;
            var nid = new NOTIFYICONDATA
            {
                cbSize = Marshal.SizeOf(typeof(NOTIFYICONDATA)),
                hWnd = _hwndSource.Handle,
                uID = TRAY_ID,
                uFlags = NIF_INFO,
                szInfo = message,
                szInfoTitle = title,
                dwInfoFlags = 0x01 // NIIF_INFO
            };
            Shell_NotifyIcon(NIM_MODIFY, ref nid);
        }

        public void Dispose()
        {
            if (_hwndSource != null)
            {
                var nid = new NOTIFYICONDATA { cbSize = Marshal.SizeOf(typeof(NOTIFYICONDATA)), hWnd = _hwndSource.Handle, uID = TRAY_ID };
                Shell_NotifyIcon(NIM_DELETE, ref nid);
            }
        }

        private IntPtr GetHIcon(BitmapSource bitmap)
        {
            int width = (int)bitmap.Width;
            int height = (int)bitmap.Height;
            int stride = width * ((bitmap.Format.BitsPerPixel + 7) / 8);
            byte[] bits = new byte[height * stride];
            bitmap.CopyPixels(bits, stride, 0);

            // Simple conversion (might need improvement for transparency, but enough for now)
            using (var ms = new System.IO.MemoryStream())
            {
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bitmap));
                encoder.Save(ms);
                using (var iconBitmap = new System.Drawing.Bitmap(ms))
                {
                    return iconBitmap.GetHicon();
                }
            }
        }
    }
}
