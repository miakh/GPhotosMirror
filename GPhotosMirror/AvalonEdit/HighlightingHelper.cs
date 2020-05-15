using System.Reflection;
using System.Xml;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;

namespace GPhotosMirror.AvalonEdit
{
    public class HighlightingHelper
    {
        public static IHighlightingDefinition LoadHighlightingFromAssembly(Assembly assembly, string name)
        {
            using (var s = assembly.GetManifestResourceStream(name))
            {
                using (var reader = new XmlTextReader(s))
                {
                    return HighlightingLoader.Load(reader, HighlightingManager.Instance);
                }
            }
        }
    }
}