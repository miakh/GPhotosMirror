using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace GDriveMirror
{
    public static class Extensions
    {
        public static IEnumerable<string> FilterPhotosVideos(this string[] str)
        {
            foreach (var s in str)
            {
                var extension = Path.GetExtension(s)?.ToLowerInvariant();
                if(Constants.AllowedExtensions.Contains(extension))
                {
                    yield return s;
                }
            }
        }

    }
}