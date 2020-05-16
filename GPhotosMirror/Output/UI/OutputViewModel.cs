using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;

namespace GPhotosMirror.Output.UI
{
    public class Output : IOutput
    {
        private readonly StringBuilder _stringBuilder;
        private IOutputView _view;

        public string DisplayName { get; }

        public Output(string displayName)
        {
            DisplayName = displayName;
            _stringBuilder = new StringBuilder();
            _writer = new OutputWriter(this);

        }
        public TextWriter Writer => _writer;
        private readonly OutputWriter _writer;

        public void Clear()
        {
            _stringBuilder.Clear();

            _view?.Clear();
        }

        public void AppendLine(string text)
        {
            Append(text + Environment.NewLine);
        }

        public void Append(string text)
        {
            _stringBuilder.Append(text);

            _view?.AppendText(text);
        }

        public void LoadView(object view)
        {
            _view = (IOutputView)view;
            _view.SetText(_stringBuilder.ToString());
            _view.ScrollToEnd();
        }

        public void UnloadView()
        {
            _view = null;
        }
    }

    public class OutputViewModel : INotifyPropertyChanged
    {
        private IOutputView _view;
        private IOutput _selectedOutputSource;

        public OutputViewModel(List<IOutput> outputs)
        {
            OutputSource = outputs;
        }
        public List<IOutput> OutputSource
        {
            get;
            set;
        }

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
            this._view = (OutputView)_view;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        [Annotations.NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}