using CSharpFunctionalExtensions;
using ICSharpCode.AvalonEdit.Highlighting;

namespace GPhotosMirror.AvalonEdit.Highlighting
{
    public class LogHighlightingProvider : IHighlightingProvider
    {
        public IHighlightingDefinition LoadDefinition(Maybe<string> theme)
        {
            return HighlightingHelper.LoadHighlightingFromAssembly(typeof(HighlightingHelper).Assembly,
                @"GPhotosMirror.AvalonEdit.Highlighting.Log.xshd");
        }
    }
}