using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using PuppeteerSharp;
using ErrorEventArgs = PuppeteerSharp.ErrorEventArgs;

namespace GPhotosMirror.Model
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
            if (CurrentBrowser == null)
            {
                return;
            }
            await CurrentPage.CloseAsync();
            await CurrentBrowser.CloseAsync();

            // remove Methods on actions
            CurrentPage.FrameNavigated -= OnCurrentPageOnFrameNavigated;
            CurrentPage.Load -= OnCurrentPageOnLoad;
            CurrentPage.Error -= OnCurrentPageOnError;
            CurrentPage.DOMContentLoaded -= OnCurrentPageOnDomContentLoaded;
            CurrentPage.RequestFailed -= OnCurrentPageOnRequestFailed;
            CurrentPage.Close -= OnCurrentPageOnClose;
            CurrentPage.PageError -= OnCurrentPageOnPageError;
            CurrentPage.Dialog -= OnCurrentPageOnDialog;
            CurrentBrowser.Closed -= OnCurrentBrowserOnClosed;

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

                // configure Logger
                CurrentPage.FrameNavigated += OnCurrentPageOnFrameNavigated;
                CurrentPage.Load += OnCurrentPageOnLoad;
                CurrentPage.Error += OnCurrentPageOnError;
                CurrentPage.DOMContentLoaded += OnCurrentPageOnDomContentLoaded;
                CurrentPage.RequestFailed += OnCurrentPageOnRequestFailed;
                CurrentPage.Close += OnCurrentPageOnClose;
                CurrentPage.PageError += OnCurrentPageOnPageError;
                CurrentPage.Dialog += OnCurrentPageOnDialog;
                CurrentBrowser.Closed += OnCurrentBrowserOnClosed;

                //CurrentPage.Request += (sender, args) =>
                //{
                //    App.PuppeteerLogger.Information($"{args.Request.Method.Method} {args.Request.Url}");
                //};
                //CurrentPage.Response += (sender, args) =>
                //{
                //    App.PuppeteerLogger.Information($"{args.Response.Ok} {args.Response.Url}");
                //};
                //await CurrentPage.SetViewportAsync();
            }

        }

        private void OnCurrentBrowserOnClosed(object? sender, EventArgs args)
        {
            App.PuppeteerLogger.Information($"Browser closed.");
        }

        private void OnCurrentPageOnDialog(object? sender, DialogEventArgs args)
        {
            App.PuppeteerLogger.Information($"{args.Dialog.DialogType} {args.Dialog.Message}");
        }

        private void OnCurrentPageOnPageError(object? sender, PageErrorEventArgs args)
        {
            App.PuppeteerLogger.Error($"PageError {args.Message}");
        }

        private void OnCurrentPageOnClose(object? sender, EventArgs args)
        {
            App.PuppeteerLogger.Information($"Page closed.");
        }

        private void OnCurrentPageOnRequestFailed(object? sender, RequestEventArgs args)
        {
            App.PuppeteerLogger.Error($"{args.Request.Method.Method} {args.Request.Url}");
        }

        private void OnCurrentPageOnDomContentLoaded(object? sender, EventArgs args)
        {
            App.PuppeteerLogger.Information($"DOMLoaded {CurrentPage.Url}");
        }

        private void OnCurrentPageOnError(object? sender, ErrorEventArgs args)
        {
            App.PuppeteerLogger.Error($"{args.Error}");
        }

        private void OnCurrentPageOnLoad(object? sender, EventArgs args)
        {
            App.PuppeteerLogger.Information($"Loaded {CurrentPage.Url}");
        }

        private void OnCurrentPageOnFrameNavigated(object? sender, FrameEventArgs args)
        {
            App.PuppeteerLogger.Information($"Navigated to {args.Frame.Url}");
        }
    }
}
