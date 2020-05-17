using System.IO;

namespace GPhotosMirror.Output
{
    public interface IOutput
    {
        public string DisplayName { get; }
        TextWriter Writer { get; }
        void AppendLine(string text);
        void Append(string text);
        void LoadView(object view);
        void UnloadView();
        void Clear();

    }
}
