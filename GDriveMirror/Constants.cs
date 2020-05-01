using PuppeteerSharp;

namespace GDriveMirror
{
    internal class Constants
    {
        public const string GOOGLE_DRIVE_URL = "https://drive.google.com/";
        public const string GOOGLE_PHOTOS_URL_SEARCH = "https://photos.google.com/search";
        public const string GOOGLE_PHOTOS_URL = "https://photos.google.com/";


        public const string MIME_FOLDER_TYPE = "application/vnd.google-apps.folder";

        public const string ProgramName = "GoogleDrive";
        public const string DelimiterInWindowsPath = "\\";
        public const string DelimiterExtension = ".";

        public const int ShortTimeout = 250;
        public const int LongTimeout = 1000;
        public static WaitForSelectorOptions NoTimeoutOptions = new WaitForSelectorOptions() { Timeout = 0 };
        public static WaitForSelectorOptions NoTimeoutOptionsHidden = new WaitForSelectorOptions(){Timeout = 0, Hidden = true};
    }
}