﻿using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace GPhotosMirror.Model.Browsers
{
    public class Chrome : BrowserBase, ILocalBrowser
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
                    if (CanUseExecutable(executableLocalPath))
                    {
                        return executableLocalPath;
                    }
                }
            }

            // try local data
            var executable = "Google\\Chrome\\Application\\chrome.exe";
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            executableLocalPath = Path.Combine(localAppData, executable);
            if (CanUseExecutable(executableLocalPath))
            {
                return executableLocalPath;
            }

            // try program files
            var programfiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            executableLocalPath = Path.Combine(programfiles, executable);
            if (CanUseExecutable(executableLocalPath))
            {
                return executableLocalPath;
            }

            return null;
        }
    }
}
