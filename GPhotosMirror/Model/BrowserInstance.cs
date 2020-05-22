using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using GPhotosMirror.Model.Browsers;
using PuppeteerSharp;
using ErrorEventArgs = PuppeteerSharp.ErrorEventArgs;

namespace GPhotosMirror.Model
{
    public class BrowserInstance
    {
        private readonly GUser _gUser;
        private readonly List<ILocalBrowser> _localBrowsers;
        private readonly Settings _settings;
        public Browser CurrentBrowserInstance;
        public Page CurrentPageInstance;

        public BrowserInstance(IEnumerable<ILocalBrowser> localBrowsers, GUser gUser, Settings settings)
        {
            _gUser = gUser;
            _settings = settings;
            _localBrowsers = localBrowsers.ToList();
        }
        private void EnsureDirectoryExist(string directory)
        {
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }
        public string UserDataDirPath()
        {
            string userPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string dataDirPath = $"AppData\\Local\\GDriveMirror\\User Data";
            string userDataDirPath = Path.Combine(userPath, dataDirPath);
            EnsureDirectoryExist(userDataDirPath);
            return userDataDirPath;
        }

        public async Task Close()
        {
            if (CurrentBrowserInstance == null)
            {
                return;
            }

            // remove user closed browser action
            CurrentBrowserInstance.Closed -= OnUserClosedBrowser;

            // remove Methods on actions
            CurrentPageInstance.FrameNavigated -= OnCurrentPageInstanceOnFrameNavigated;
            CurrentPageInstance.Load -= OnCurrentPageInstanceOnLoad;
            CurrentPageInstance.Error -= OnCurrentPageInstanceOnError;
            CurrentPageInstance.DOMContentLoaded -= OnCurrentPageInstanceOnDomContentLoaded;
            CurrentPageInstance.RequestFailed -= OnCurrentPageInstanceOnRequestFailed;
            CurrentPageInstance.Close -= OnCurrentPageInstanceOnClose;
            CurrentPageInstance.PageError -= OnCurrentPageInstanceOnPageInstanceError;
            CurrentPageInstance.Dialog -= OnCurrentPageInstanceOnDialog;
            CurrentBrowserInstance.Closed -= OnCurrentBrowserInstanceOnClosed;

            await CurrentPageInstance.CloseAsync();
            await CurrentBrowserInstance.CloseAsync();

            await CurrentPageInstance.DisposeAsync();
            CurrentPageInstance = null;
            await CurrentBrowserInstance.DisposeAsync();
            CurrentBrowserInstance = null;
        }

        public async Task LaunchIfClosed()
        {
            if (CurrentBrowserInstance == null)
            {
                string executableLocalPath = null;

                // prioritize last used browser
                var useBrowser = _localBrowsers.FirstOrDefault(b => b.BrowserID == _settings.UsedBrowser);
                //var useBrowser = _localBrowsers.FirstOrDefault(b => b is BundledChromium);

                if (useBrowser != null)
                {
                    _localBrowsers.Remove(useBrowser);
                    _localBrowsers.Insert(0, useBrowser);
                }

                foreach (ILocalBrowser localBrowser in _localBrowsers)
                {
                    executableLocalPath = await localBrowser.GetExecutable();
                    if (executableLocalPath == null)
                    {
                        continue;
                    }

                    // Sign out if browser is changed
                    if (localBrowser.BrowserID != _settings.UsedBrowser)
                    {
                        _gUser.IsSignedIn = false;
                        DeleteUserData();
                        _settings.UsedBrowser = localBrowser.BrowserID;
                    }

                    break;
                }

                if (string.IsNullOrEmpty(executableLocalPath))
                {
                    // Failed to run any Browser
                    return;
                }

                CurrentBrowserInstance = await LaunchBrowser(UserDataDirPath(), executableLocalPath);

                // user closes browser scenario
                CurrentBrowserInstance.Closed += OnUserClosedBrowser;
            }

            if (CurrentPageInstance == null)
            {
                var pages = await CurrentBrowserInstance.PagesAsync();
                CurrentPageInstance = null;
                if (pages.Any() && pages.Last().Url == "about:blank")
                {
                    CurrentPageInstance = pages.Last();
                }
                else
                {
                    CurrentPageInstance = await CurrentBrowserInstance.NewPageAsync();
                }

                // configure Logger
                CurrentPageInstance.FrameNavigated += OnCurrentPageInstanceOnFrameNavigated;
                CurrentPageInstance.Load += OnCurrentPageInstanceOnLoad;
                CurrentPageInstance.Error += OnCurrentPageInstanceOnError;
                CurrentPageInstance.DOMContentLoaded += OnCurrentPageInstanceOnDomContentLoaded;
                CurrentPageInstance.RequestFailed += OnCurrentPageInstanceOnRequestFailed;
                CurrentPageInstance.Close += OnCurrentPageInstanceOnClose;
                CurrentPageInstance.PageError += OnCurrentPageInstanceOnPageInstanceError;
                CurrentPageInstance.Dialog += OnCurrentPageInstanceOnDialog;
                CurrentBrowserInstance.Closed += OnCurrentBrowserInstanceOnClosed;

                //CurrentPageInstance.Request += (sender, args) =>
                //{
                //    App.PuppeteerLogger.Information($"{args.Request.Method.Method} {args.Request.Url}");
                //};
                //CurrentPageInstance.Response += (sender, args) =>
                //{
                //    App.PuppeteerLogger.Information($"{args.Response.Ok} {args.Response.Url}");
                //};
                //await CurrentPageInstance.SetViewportAsync();
            }
        }

        private async void OnUserClosedBrowser(object? sender, EventArgs e) => await Close();

        private static async Task<Browser> LaunchBrowser(string userDataDirPath, string executableLocalPath) =>
            await Puppeteer.LaunchAsync(new LaunchOptions
            {
                Headless = false,
                UserDataDir = userDataDirPath,
                ExecutablePath = executableLocalPath,
                //Args = new []{
                //    "--disable-background-timer-throttling",
                //    "--disable-backgrounding-occluded-windows",
                //    "--disable-renderer-backgrounding"
                //},
                IgnoredDefaultArgs = new[] {"--disable-extensions"},
                DefaultViewport = new ViewPortOptions() {Height = 600, Width = 1000}
            });

        private void OnCurrentBrowserInstanceOnClosed(object? sender, EventArgs args) =>
            App.PuppeteerLogger.Information($"Browser closed.");

        private void OnCurrentPageInstanceOnDialog(object? sender, DialogEventArgs args) =>
            App.PuppeteerLogger.Information($"{args.Dialog.DialogType} {args.Dialog.Message}");

        private void OnCurrentPageInstanceOnPageInstanceError(object? sender, PageErrorEventArgs args) =>
            App.PuppeteerLogger.Error($"PageError {args.Message}");

        private void OnCurrentPageInstanceOnClose(object? sender, EventArgs args) =>
            App.PuppeteerLogger.Information($"Page closed.");

        private void OnCurrentPageInstanceOnRequestFailed(object? sender, RequestEventArgs args) =>
            App.PuppeteerLogger.Error($"{args.Request.Method.Method} {args.Request.Url}");

        private void OnCurrentPageInstanceOnDomContentLoaded(object? sender, EventArgs args) =>
            App.PuppeteerLogger.Information($"DOMLoaded {CurrentPageInstance.Url}");

        private void OnCurrentPageInstanceOnError(object? sender, ErrorEventArgs args) =>
            App.PuppeteerLogger.Error($"{args.Error}");

        private void OnCurrentPageInstanceOnLoad(object? sender, EventArgs args) =>
            App.PuppeteerLogger.Information($"Loaded {CurrentPageInstance.Url}");

        private void OnCurrentPageInstanceOnFrameNavigated(object? sender, FrameEventArgs args) =>
            App.PuppeteerLogger.Information($"Navigated to {args.Frame.Url}");

        public void DeleteUserData() => Directory.Delete(UserDataDirPath(), true);
    }
}
