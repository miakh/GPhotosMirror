using System;
using System.IO;
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
}