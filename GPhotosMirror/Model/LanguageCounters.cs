using System;

namespace GPhotosMirror.Model
{
    public static class LanguageCounters
    {
        public static string DefaultEnglishCounter(this string s, int count)
        {
            if (count == 0)
            {
                return $"no {s}";
            }
            if (count == 1)
            {
                return $"1 {s}";
            }
            if (count > 1)
            {
                return $"{count} {s}s";
            }

            throw new NotImplementedException();
        }
    }
}
