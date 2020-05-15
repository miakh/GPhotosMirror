using Serilog.Core;
using System;
using Serilog.Events;
using Serilog.Formatting;

namespace GPhotosMirror.Output
{
    public class OutputSink : ILogEventSink
    {
        readonly ITextFormatter _textFormatter;
        readonly object _syncRoot = new object();
        private readonly Func<IOutput> _outputProvider;
        private readonly Func<IOutputLogFilter> _outputLogFilterProvider;
        private IOutput _output;
        private IOutputLogFilter _outputLogFilter;

        private IOutput Output => _output ?? (_output = _outputProvider());

        private IOutputLogFilter OutputLogFilter
        {
            get
            {
                if (_outputLogFilterProvider == null)
                {
                    return null;
                }

                return _outputLogFilter ?? (_outputLogFilter = _outputLogFilterProvider());
            }
        }

        public OutputSink(Func<IOutput> outputProvider, ITextFormatter textFormatter, Func<IOutputLogFilter> outputLogFilterProvider = null)
        {
            _textFormatter = textFormatter ?? throw new ArgumentNullException(nameof(textFormatter));
            _outputProvider = outputProvider;
            _outputLogFilterProvider = outputLogFilterProvider;
        }

        public void Emit(LogEvent logEvent)
        {
            if (logEvent == null) throw new ArgumentNullException(nameof(logEvent));
            if(Output == null)
            {
                return;
            }

            var outputLogFilter = OutputLogFilter;
            if (outputLogFilter != null)
            {
                var filter = outputLogFilter.Filter(logEvent);
                if(!filter)
                {
                    return;
                }
            }

            var textWriter = Output.Writer;
            lock (_syncRoot)
            {
                _textFormatter.Format(logEvent, textWriter);
                textWriter.Flush();
            }
        }
    }
}