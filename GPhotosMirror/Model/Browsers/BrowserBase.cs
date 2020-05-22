using System.Diagnostics;
using System.IO;

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
}
