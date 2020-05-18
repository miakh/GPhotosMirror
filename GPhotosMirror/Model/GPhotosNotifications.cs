using System.Windows;
using System.Windows.Media;
using Enterwell.Clients.Wpf.Notifications;
using MahApps.Metro.Controls;

namespace GPhotosMirror.Model
{
    public class GPhotosNotifications : NotificationMessageManager
    {
        public NotificationMessageBuilder NotificationMessageBuilder()
        {
            NotificationMessageBuilder messageBuilder = this.CreateMessage();
            messageBuilder.SetForeground(Application.Current.FindResource("MahApps.Brushes.ThemeBackground") as Brush);
            messageBuilder.SetBackground(Application.Current.FindResource("MahApps.Brushes.ThemeForeground") as Brush);
            messageBuilder.SetAccent(Application.Current.FindResource("MahApps.Brushes.Accent") as Brush);
            return messageBuilder;
        }

        public MetroProgressBar DownloadingBar
        {
            get
            {
                return  new MetroProgressBar()
                {
                    VerticalAlignment = VerticalAlignment.Bottom,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    Height = 5,
                    MinHeight = 5,
                    Foreground = Application.Current.FindResource("MahApps.Brushes.Accent") as Brush,
                    BorderThickness = new Thickness(0),
                    Background = Brushes.Transparent,
                    IsIndeterminate = false
                };
            }
        }
    }
}
