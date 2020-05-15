using Serilog.Events;

namespace GPhotosMirror.Output
{
    public interface IOutputLogFilter
    {
        void InvalidateCache();
        bool Filter(LogEvent logEvent);
        LogEventLevel MinLogLevel { get; set; }
    }
}