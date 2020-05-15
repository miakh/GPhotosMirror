using System;
using Serilog;
using Serilog.Configuration;
using Serilog.Events;
using Serilog.Formatting.Display;

namespace GPhotosMirror.Output
{
    public static class OutputLoggerExtensions
    {
        private const string DefaultConsoleOutputTemplate =
            "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level}] {Message}{NewLine}{Exception}";

        public static LoggerConfiguration OutputModule(this LoggerSinkConfiguration sinkConfiguration,
            Func<IOutput> factory, Func<IOutputLogFilter> outputLogFilterProvider = null, LogEventLevel restrictedToMinimumLevel = LevelAlias.Minimum,
            string outputTemplate = DefaultConsoleOutputTemplate, IFormatProvider formatProvider = null)
        {
            if (sinkConfiguration == null) throw new ArgumentNullException(nameof(sinkConfiguration));
            if (outputTemplate == null) throw new ArgumentNullException(nameof(outputTemplate));

            var formatter = new MessageTemplateTextFormatter(outputTemplate, formatProvider);
            return sinkConfiguration.Sink(new OutputSink(factory, formatter, outputLogFilterProvider), restrictedToMinimumLevel);
        }
    }
}