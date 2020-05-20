using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace GPhotosMirror.Model
{
    public class GUser:INotifyPropertyChanged
    {
        private bool _isSignedIn;
        private bool _isSigningIn;
        private string _userName;

        public bool IsSignedIn
        {
            get => _isSignedIn;
            set
            {
                _isSignedIn = value;
                if (UserSettings.Default.WasSignedIn != value)
                {
                    UserSettings.Default.WasSignedIn = value;
                    UserSettings.Default.Save();
                }

                if (!value)
                {
                    UserName = "";
                }

                OnPropertyChanged();
            }
        }

        public bool IsSigningIn
        {
            get => _isSigningIn;
            set
            {
                _isSigningIn = value;
                OnPropertyChanged();
            }
        }

        public string UserName
        {
            set
            {
                _userName = value;
                OnPropertyChanged();
            }
            get => !string.IsNullOrEmpty(_userName) ? _userName : "";
        }

        public event PropertyChangedEventHandler PropertyChanged;

        [Annotations.NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
