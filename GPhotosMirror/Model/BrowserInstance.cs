using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Windows;
using ByteSizeLib;
using Enterwell.Clients.Wpf.Notifications;
using Microsoft.Win32;
using PuppeteerSharp;
using Serilog;
using ErrorEventArgs = PuppeteerSharp.ErrorEventArgs;

namespace GPhotosMirror.Model
{
    public class BrowserInstance
    {
        private readonly GPhotosNotifications _notificationMessageManager;
        public Browser CurrentBrowser;
        public Page CurrentPage;
        public string UserDataDirPath
        {
            get
            {
                string userPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                string dataDirPath = "AppData\\Local\\GDriveMirror\\User Data";
                string userDataDirPath = Path.Combine(userPath, dataDirPath);
                return userDataDirPath;
            }
        }

        public BrowserInstance(GPhotosNotifications notificationMessageManager)
        {
            _notificationMessageManager = notificationMessageManager;
        }

        public async Task Close()
        {
            if (CurrentBrowser == null)
            {
                return;
            }

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
                // try get chrome path from registers
                string pathFromRegisters = null;
                using (RegistryKey key = Registry.LocalMachine.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\App Paths\\chrome.exe"))
                {
                    Object o = key?.GetValue("Path");
                    if (o != null)
                    {
                        pathFromRegisters = (o as string) + "\\chrome.exe"; 
                    }
                }

                var executableLocalPath = pathFromRegisters;

                // try other option
                if (!File.Exists(executableLocalPath))
                {
                    var executable = "Google\\Chrome\\Application\\chrome.exe";
                    var programfiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
                    executableLocalPath = Path.Combine(programfiles, executable);
                }

                // try get executable of bundled
                if (!File.Exists(executableLocalPath))
                {
                    executableLocalPath = Puppeteer.GetExecutablePath();
                }

                // Download bundled Chromium
                if (!File.Exists(executableLocalPath))
                {
                    var downloadingBar = _notificationMessageManager.DownloadingBar;
                    
                    var fetcher = new BrowserFetcher();
                    var p = fetcher.DownloadsFolder;
                    var messageBuilder = _notificationMessageManager
                        .NotificationMessageBuilder()
                        .Animates(true)
                        .AnimationInDuration(0.5)
                        .AnimationOutDuration(0)
                        .HasMessage($"Downloading Chromium...")
                        .WithOverlay(downloadingBar);
                    Action<object, DownloadProgressChangedEventArgs> GetDownloadSize = null;
                    GetDownloadSize = ((o, args) =>
                    {
                        messageBuilder.HasMessage(
                            $"Downloading Chromium ({new ByteSize((double)args.TotalBytesToReceive)})...");
                        Log.Information(
                            $"Chromium not found. Downloading Chromium ({new ByteSize((double)args.TotalBytesToReceive)})...");
                        GetDownloadSize = null;
                    });

                    fetcher.DownloadProgressChanged += (sender, args) =>
                    {
                        downloadingBar.Value = (double)args.BytesReceived/args.TotalBytesToReceive*100;
                        GetDownloadSize?.Invoke(sender, args);
                    };
                    INotificationMessage message = messageBuilder.Queue();
                    await fetcher.DownloadAsync(BrowserFetcher.DefaultRevision);
                    _notificationMessageManager.Dismiss(message);
                    Log.Information($"Chromium Downloaded.");
                    executableLocalPath = Puppeteer.GetExecutablePath();
                }

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
