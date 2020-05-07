using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using PuppeteerSharp;

namespace GDriveMirror
{
    public class BrowserInstance
    {
        public Browser CurrentBrowser;
        public Page CurrentPage;
        private string UserDataDirPath { get; }

        public BrowserInstance(string UserDataDirPath)
        {
            this.UserDataDirPath = UserDataDirPath;
        }

        public async Task Close()
        {
            await CurrentPage.CloseAsync();
            await CurrentBrowser.CloseAsync();
            await CurrentPage.DisposeAsync();
            CurrentPage = null;
            await CurrentBrowser.DisposeAsync();
            CurrentBrowser = null;
        }

        public async Task LaunchIfClosed()
        {
            if (this.CurrentBrowser == null)
            {
                var executable = "Google\\Chrome\\Application\\chrome.exe";
                var programfiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
                var executableLocalPath = Path.Combine(programfiles, executable);
                if (!System.IO.File.Exists(executableLocalPath))
                {
                    await new BrowserFetcher().DownloadAsync(BrowserFetcher.DefaultRevision);
                    executableLocalPath = null;
                }

                //close your browser if exception
                //or start bundled

                CurrentBrowser = await Puppeteer.LaunchAsync(new LaunchOptions
                {
                    Headless = false,
                    UserDataDir = UserDataDirPath,
                    ExecutablePath = executableLocalPath,
                    IgnoredDefaultArgs = new[] { "--disable-extensions" },
                    DefaultViewport = new ViewPortOptions() { Height = 600, Width = 1000 }
                });
            }

            if (this.CurrentPage == null)
            {
                var pages = await CurrentBrowser.PagesAsync();
                CurrentPage = null;
                if (pages.Any() && pages.Last().Url == "about:blank")
                {
                    CurrentPage = pages.Last();
                }
                else
                {
                    CurrentPage = await CurrentBrowser.NewPageAsync();
                }

                //await CurrentPage.SetViewportAsync();
            }
            

            

        }
    }
}