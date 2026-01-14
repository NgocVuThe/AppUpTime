using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
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

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
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

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern bool Shell_NotifyIcon(uint dwMessage, [In] ref NOTIFYICONDATA lpData);

        [DllImport("user32.dll")]
        private static extern IntPtr CreatePopupMenu();

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool AppendMenu(IntPtr hMenu, uint uFlags, uint uIDNewItem, string lpNewItem);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);

        [DllImport("user32.dll")]
        private static extern bool DestroyIcon(IntPtr hIcon);

        [StructLayout(LayoutKind.Sequential)]
        private struct ICONINFO
        {
            public bool fIcon;
            public int xHotspot;
            public int yHotspot;
            public IntPtr hbmMask;
            public IntPtr hbmColor;
        }

        [DllImport("user32.dll")]
        private static extern IntPtr CreateIconIndirect(ref ICONINFO icon);

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
        private Window? _trayWindow; // Keep reference to prevent GC

        public void Initialize(Action onShowCommand, Action onExitCommand)
        {
            _onShow = onShowCommand;
            _onExit = onExitCommand;

            // Create a hidden window to handle tray messages
            _trayWindow = new Window { Width = 0, Height = 0, WindowStyle = WindowStyle.None, ShowInTaskbar = false, Visibility = Visibility.Hidden };
            _trayWindow.Show();
            var helper = new WindowInteropHelper(_trayWindow);
            var hWnd = helper.Handle;

            _hwndSource = HwndSource.FromHwnd(hWnd);
            _hwndSource.AddHook(WndProc);

            // Load icon
            try
            {
                var uri = new System.Uri("pack://application:,,,/Assets/clock_icon.png");
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = uri;
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                _hIcon = GetHIcon(bitmap);
                App.Log($"Tray Icon loaded successfully: {_hIcon}");
            }
            catch (Exception ex) 
            { 
                _hIcon = IntPtr.Zero; 
                App.Log($"Tray Icon load failed: {ex.Message}");
            }

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

            bool result = Shell_NotifyIcon(NIM_ADD, ref nid);
            App.Log($"Tray Icon NIM_ADD result: {result}");
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
            Application.Current.Dispatcher.Invoke(() =>
            {
                try
                {
                    var noti = new NotificationWindow(title, message);
                    noti.Show();
                }
                catch (Exception ex)
                {
                    // Fallback to native log if window fails
                    App.Log($"Custom Notification Error: {ex.Message}");
                }
            });

            // Optional: Keep native NIF_INFO as backup or remove it?
            // User specifically asked for custom design, so we skip the native balloon.
            /*
            if (_hwndSource == null || _trayWindow == null) return;
            var nid = new NOTIFYICONDATA
            {
                cbSize = Marshal.SizeOf(typeof(NOTIFYICONDATA)),
                hWnd = _hwndSource.Handle,
                uID = TRAY_ID,
                uFlags = NIF_INFO,
                szInfo = message,
                szInfoTitle = title,
                dwInfoFlags = 0x01
            };
            Shell_NotifyIcon(NIM_MODIFY, ref nid);
            */
        }

        public void Dispose()
        {
            if (_hwndSource != null)
            {
                var nid = new NOTIFYICONDATA { cbSize = Marshal.SizeOf(typeof(NOTIFYICONDATA)), hWnd = _hwndSource.Handle, uID = TRAY_ID };
                Shell_NotifyIcon(NIM_DELETE, ref nid);
            }
            _trayWindow?.Close();
        }

        private IntPtr GetHIcon(BitmapSource bitmap)
        {
            try
            {
                // Ensure 32x32 for high quality tray scaling
                var scaledBitmap = new TransformedBitmap(bitmap, new ScaleTransform(32.0 / bitmap.PixelWidth, 32.0 / bitmap.PixelHeight));

                using (var ms = new System.IO.MemoryStream())
                {
                    var encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(scaledBitmap));
                    encoder.Save(ms);
                    ms.Position = 0;
                    
                    using (var gdiBitmap = new System.Drawing.Bitmap(ms))
                    {
                        // To preserve alpha channel in HICON, we use CreateIconIndirect with a dummy mask
                        IntPtr hBitmap = gdiBitmap.GetHbitmap(System.Drawing.Color.FromArgb(0, 0, 0, 0));
                        
                        // Create a purely black mask of the same size
                        using (var maskBitmap = new System.Drawing.Bitmap(gdiBitmap.Width, gdiBitmap.Height))
                        {
                            IntPtr hMask = maskBitmap.GetHbitmap();
                            
                            ICONINFO ii = new ICONINFO
                            {
                                fIcon = true,
                                hbmColor = hBitmap,
                                hbmMask = hMask
                            };

                            IntPtr hIcon = CreateIconIndirect(ref ii);

                            // Clean up GDI handles immediately
                            DeleteObject(hBitmap);
                            DeleteObject(hMask);

                            return hIcon;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                App.Log($"GetHIcon primary failed: {ex.Message}. Using fallback.");
                try
                {
                    string? exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
                    if (exePath != null)
                    {
                        using (var sysIcon = System.Drawing.Icon.ExtractAssociatedIcon(exePath))
                        {
                            return sysIcon?.Handle ?? IntPtr.Zero;
                        }
                    }
                }
                catch { }
                return IntPtr.Zero;
            }
        }
    }
}
