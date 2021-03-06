﻿using System;
using System.Net;
using System.Threading.Tasks;
using ByteSizeLib;
using Enterwell.Clients.Wpf.Notifications;
using PuppeteerSharp;
using Serilog;

namespace GPhotosMirror.Model.Browsers
{
    public class BundledChromium : BrowserBase, ILocalBrowser
    {
        private readonly GPhotosNotifications _notificationMessageManager;

        public BundledChromium(GPhotosNotifications _notificationMessageManager) =>
            this._notificationMessageManager = _notificationMessageManager;

        public string BrowserID => "BundledChromium";

        public async Task<string> GetExecutable()
        {
            string executableLocalPath = null;
            // try get executable of bundled
            try
            {
                executableLocalPath = Puppeteer.GetExecutablePath();
            }
            catch (Exception e)
            {
                // Executable not found
            }

            // Download bundled Chromium
            if (!CanUseExecutable(executableLocalPath))
            {
                executableLocalPath = await DownloadBundledChromium();
            }

            return executableLocalPath;
        }

        private async Task<string> DownloadBundledChromium()
        {

            INotificationMessage message = null;
            try
            {
                string executableLocalPath;
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
                GetDownloadSize = (o, args) =>
                {
                    messageBuilder.HasMessage(
                        $"Downloading Chromium ({new ByteSize((double)args.TotalBytesToReceive)})...");
                    Log.Information(
                        $"Chromium not found. Downloading Chromium ({new ByteSize((double)args.TotalBytesToReceive)})...");
                    GetDownloadSize = null;
                };

                fetcher.DownloadProgressChanged += (sender, args) =>
                {
                    downloadingBar.Value = (double)args.BytesReceived / args.TotalBytesToReceive * 100;
                    GetDownloadSize?.Invoke(sender, args);
                };
                message = messageBuilder.Queue();
                await fetcher.DownloadAsync(BrowserFetcher.DefaultRevision);
                _notificationMessageManager.Dismiss(message);
                Log.Information($"Chromium Downloaded.");
                executableLocalPath = Puppeteer.GetExecutablePath();
                return executableLocalPath;
            }
            catch (Exception e)
            {
                _notificationMessageManager.Dismiss(message);
                Log.Error("Failed to download Chromium.");
            }

            return null;
        }
    }
}
