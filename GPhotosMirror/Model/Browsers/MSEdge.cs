using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace GPhotosMirror.Model.Browsers
{
    public class MSEdge : ILocalBrowser
    {
        public string BrowserID => "MSEdge";

        public async Task<string> GetExecutable()
        {
            string executableLocalPath = null;

            // try get edge path from registers
            using (RegistryKey key =
                Registry.LocalMachine.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\App Paths\\msedge.exe"))
            {
                object o = key?.GetValue("Path");
                if (o != null)
                {
                    executableLocalPath = (o as string) + "\\msedge.exe";
                    if (File.Exists(executableLocalPath))
                    {
                        var versionInfo = FileVersionInfo.GetVersionInfo(executableLocalPath);
                        // use only if MS Edge uses Chromium (Constants.PuppeteerMinimalVersion is enough to verify that.
                        if (versionInfo.ProductMajorPart >= Constants.PuppeteerMinimalVersion)
                        {
                            return executableLocalPath;
                        }
                    }
                }
            }

            return null;
        }
    }
}
