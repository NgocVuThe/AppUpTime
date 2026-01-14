using System;
using System.Windows;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace DailyUptimeWidget
{
    public partial class NotificationWindow : Window
    {
        private DispatcherTimer _closeTimer;

        public NotificationWindow(string title, string message)
        {
            InitializeComponent();
            TxtTitle.Text = title;
            TxtMessage.Text = message;
            
            this.Loaded += NotificationWindow_Loaded;
            
            _closeTimer = new DispatcherTimer();
            _closeTimer.Interval = TimeSpan.FromSeconds(8);
            _closeTimer.Tick += (s, e) => CloseWithAnim();
        }

        private void NotificationWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Position at Bottom-Right
            var desktopWorkingArea = SystemParameters.WorkArea;
            this.Left = desktopWorkingArea.Right - this.Width - 10;
            this.Top = desktopWorkingArea.Bottom - this.Height - 10;

            // Start show animation
            if (Resources["ShowAnim"] is Storyboard sb)
            {
                sb.Begin(this);
            }

            _closeTimer.Start();
        }

        private void CloseWithAnim()
        {
            _closeTimer.Stop();
            if (Resources["HideAnim"] is Storyboard sb)
            {
                sb.Completed += (s, e) => this.Close();
                sb.Begin(this);
            }
            else
            {
                this.Close();
            }
        }
    }
}
