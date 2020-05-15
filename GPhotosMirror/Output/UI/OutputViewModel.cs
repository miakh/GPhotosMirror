using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;

namespace GPhotosMirror.Output.UI
{
    public class OutputViewModel : IOutput, INotifyPropertyChanged
    {
        private readonly StringBuilder _stringBuilder;
        private readonly OutputWriter _writer;
        private IOutputView _view;
        private IDictionary<string, string> _outputSource;
        private string _selectedOutputSource;

        public TextWriter Writer => _writer;

        public OutputViewModel()
        {
            DisplayName = "Output";

            _stringBuilder = new StringBuilder();

            _writer = new OutputWriter(this);

            OutputSource = new Dictionary<string, string>
            {
                {"app_logger","Application" }
            };
        }

        public string DisplayName { get; set; }

        public IDictionary<string, string> OutputSource
        {
            get => _outputSource;
            private set
            {
                _outputSource = value;
                if (string.IsNullOrEmpty(SelectedOutputSource) || !_outputSource.ContainsKey(SelectedOutputSource))
                    SelectedOutputSource = _outputSource.FirstOrDefault().Key;
            }
        }

        public string SelectedOutputSource
        {
            get => _selectedOutputSource;
            set
            {
                if (value == _selectedOutputSource) return;
                _selectedOutputSource = value;
                OnPropertyChanged();
            }
        }

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

        public void OnViewLoaded(object view)
        {
            _view = (IOutputView)view;
            _view.SetText(_stringBuilder.ToString());
            _view.ScrollToEnd();
        }

        public event PropertyChangedEventHandler PropertyChanged;

        [Annotations.NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}