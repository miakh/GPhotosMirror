using PuppeteerSharp;

namespace GDriveMirror
{
    internal class Constants
    {
        public const string GOOGLE_PHOTOS_URL_SEARCH = "https://photos.google.com/search";
        public const string GOOGLE_PHOTOS_URL = "https://photos.google.com/";
        public const string GOOGLE_PHOTOS_ALBUM_URL = "https://photos.google.com/album/";

        public static string[] AllowedExtensions = new[]
        {
            ".3fr", ".3gp", ".arw", ".avi", ".cr2", ".crw", ".dc2", ".dcr", ".dng", ".erf", ".heic", ".jpeg", ".k25",
            ".kdc", ".mdc", ".mef", ".mkv", ".mos", ".mov", ".mrw", ".mts", ".nef", ".nrw", ".orf", ".pef", ".qtk",
            ".raf", ".raw", ".rdc", ".rw2", ".sr2", ".srf", ".x3f"
        };

        public const string LITE_FILE = "LiteFile";
        public const string LITE_DIRECTORY = "LiteDirectory";

        public const string DatabaseFileName = "DB.db";

        public const string ProgramName = "GoogleDrive";
        public const string DelimiterInWindowsPath = "\\";
        public const string DelimiterExtension = ".";

        public const int ShortTimeout = 250;
        public const int LongTimeout = 1000;
        public static WaitForSelectorOptions NoTimeoutOptions = new WaitForSelectorOptions() { Timeout = 0 };
        public static WaitForSelectorOptions NoTimeoutOptionsHidden = new WaitForSelectorOptions() { Timeout = 0""",""" Hidden = true };
    }
}