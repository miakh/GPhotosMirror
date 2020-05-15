using System.IO;

namespace GPhotosMirror.Output
{
    public interface IOutput
    {
        TextWriter Writer { get; }
        void AppendLine(string text);
        void Append(string text);
        void OnViewLoaded(object view);
        void Clear();

    }
}