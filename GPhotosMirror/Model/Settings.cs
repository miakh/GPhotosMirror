using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace GPhotosMirror.Model
{
    public class Settings : INotifyPropertyChanged
    {
        public string LocalRoot
        {
            get => UserSettings.Default.RootPath;
            set
            {
                if (UserSettings.Default.RootPath != value)
                {
                    UserSettings.Default.RootPath = value;
                    UserSettings.Default.Save();
                    OnPropertyChanged();
                }
            }
        }

        public bool WasSignedIn
        {
            get => UserSettings.Default.WasSignedIn;
            set
            {
                if (UserSettings.Default.WasSignedIn != value)
                {
                    UserSettings.Default.WasSignedIn = value;
                    UserSettings.Default.Save();
                }
            }
        }

        public string UsedBrowser
        {
            get => UserSettings.Default.UsedBrowser;
            set
            {
                if (UserSettings.Default.UsedBrowser != value)
                {
                    UserSettings.Default.UsedBrowser = value;
                    UserSettings.Default.Save();
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        [Annotations.NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        public void Reload()
        {
            if (UserSettings.Default.Upgrade)
            {
                UserSettings.Default.Upgrade();
                UserSettings.Default.Upgrade = false;
                UserSettings.Default.Save();
            }
            UserSettings.Default.Reload();
            OnPropertyChanged(nameof(LocalRoot));
        }
    }
}
