using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace GPhotosMirror.Output.UI
{
    public class OutputViewModel : INotifyPropertyChanged
    {
        private IOutputView _view;
        private IOutput _selectedOutputSource;

        public OutputViewModel(List<IOutput> outputs)
        {
            OutputSource = outputs;
        }

        public List<IOutput> OutputSource { get; set; }

        public IOutput SelectedOutputSource
        {
            get
            {
                if (_selectedOutputSource == null)
                {
                    _selectedOutputSource = OutputSource.FirstOrDefault();
                    _selectedOutputSource?.LoadView(_view);
                }

                return _selectedOutputSource;
            }
            set
            {
                if (value == _selectedOutputSource) return;
                _selectedOutputSource.UnloadView();
                _selectedOutputSource = value;
                _selectedOutputSource.LoadView(_view);
                OnPropertyChanged();
            }
        }

        public void OnViewLoaded(object _view)
        {
            this._view = (OutputView) _view;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        [Annotations.NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
