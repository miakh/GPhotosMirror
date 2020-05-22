using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace GPhotosMirror.Model.Browsers
{
    public class BrowserBase
    {
        protected bool CanUseExecutable(string executable)
        {
            if (!File.Exists(executable))
            {
                return false;
            }

            var versionInfo = FileVersionInfo.GetVersionInfo(executable);
            // use only if MS Edge uses Chromium (Constants.PuppeteerMinimalVersion is enough to verify that.)
            return versionInfo.ProductMajorPart >= Constants.PuppeteerMinimalVersion;
        }
    }
    public class MSEdge : BrowserBase, ILocalBrowser
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
                    if (CanUseExecutable(executableLocalPath))
                    {
                        return executableLocalPath;
                    }
                }
            }

            return null;
        }
    }
}
