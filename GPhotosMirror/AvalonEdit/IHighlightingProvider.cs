using CSharpFunctionalExtensions;
using ICSharpCode.AvalonEdit.Highlighting;

namespace GPhotosMirror.AvalonEdit
{
    public interface IHighlightingProvider
    {
        IHighlightingDefinition LoadDefinition(Maybe<string> theme);
    }
}