using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace GPhotosMirror.Model.Browsers
{
    public class Chrome : ILocalBrowser
    {
        public string BrowserID => "Chrome";

        public async Task<string> GetExecutable()
        {
            string executableLocalPath = null;
            // try get chrome path from registers
            using (RegistryKey key =
                Registry.LocalMachine.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\App Paths\\chrome.exe"))
            {
                object o = key?.GetValue("Path");
                if (o != null)
                {
                    executableLocalPath = (o as string) + "\\chrome.exe";
                    if (File.Exists(executableLocalPath))
                    {
                        return executableLocalPath;
                    }
                }
            }


            // try other option
            var executable = "Google\\Chrome\\Application\\chrome.exe";
            var programfiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            executableLocalPath = Path.Combine(programfiles, executable);
            if (File.Exists(executableLocalPath))
            {
                return executableLocalPath;
            }

            return null;
        }
    }
}
