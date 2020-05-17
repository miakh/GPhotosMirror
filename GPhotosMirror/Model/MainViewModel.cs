﻿using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using AsyncAwaitBestPractices;
using Enterwell.Clients.Wpf.Notifications;
using GalaSoft.MvvmLight.Command;
using GPhotosMirror.Model;
using GPhotosMirror.Output;
using GPhotosMirror.Output.UI;
using GPhotosMirror.Views;
using MahApps.Metro.Controls;
using Microsoft.WindowsAPICodePack.Dialogs;
using Onova;
using Onova.Services;
using PuppeteerSharp;
using Serilog;
using Serilog.Events;
using Brushes = System.Drawing.Brushes;
using Color = System.Drawing.Color;

namespace GPhotosMirror
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly NotificationMessageManager _notificationMessageManager;
        private string _localRoot;
        private ICommand changePath;
        private RelayCommand logoutCommand;
        private RelayCommand executeCommand;


        public event PropertyChangedEventHandler PropertyChanged;

        private TaskScheduler TScheduler { get; }

        public async Task OnUIContext(Action action, CancellationToken cancellationToken = default)
        {
            await Task.Factory.StartNew(action, cancellationToken, TaskCreationOptions.None, TScheduler);
        }

        public string LocalRoot
        {
            get => _localRoot;
            set
            {
                _localRoot = value;
                NotifyPropertyChanged();
            }
        }

        public MainViewModel(NotificationMessageManager notificationMessageManager)
        {
            _notificationMessageManager = notificationMessageManager;
            TScheduler = TaskScheduler.FromCurrentSynchronizationContext();
            Initialize();
        }


        private string userName;
        private RelayCommand stopExecutionCommand;
        private bool _isSignedIn;
        private RelayCommand signInCommand;
        private bool _isSigningIn;

        public async void Initialize()
        {
            CheckAndUpdate().SafeFireAndForget();

            Log.Information($"Welcome in GPhotosMirror (version {Assembly.GetExecutingAssembly().GetName().Version.ToString(3)})!");

            //Load local root folder from settings
            LocalRoot = UserSettings.Default.RootPath;

            if (string.IsNullOrWhiteSpace(LocalRoot) || !UserSettings.Default.WasSignedIn)
            {
                Log.Information($"Sign in and choose folder you want to enable upload to Google Photos.");
                Log.Information($"Folder and all the subfolders will be uploaded as independent albums.");
                Log.Information($"If you the photo is already uploaded then it is skipped.");

            }

            // Sign in just to make sure it is possible
            if (UserSettings.Default.WasSignedIn)
            {
                SignIn().SafeFireAndForget();
            }

            MTE.PropertyChanged += (sender, args) =>
            {
                if (args.PropertyName == nameof(MTE.IsExecuteButtonEnabled))
                {
                    NotifyPropertyChanged(nameof(CanUpload));
                }
            };

        }

        private async Task CheckAndUpdate()
        {
            // Check for updates
            var manager = new UpdateManager(
                new GithubPackageResolver("miakh", "GPhotosMirror", "GPhotosMirror*.zip"),
                new ZipPackageExtractor());


            var result = await manager.CheckForUpdatesAsync();
            if (result.CanUpdate)
            {
                NotificationMessageBuilder()
                    .Animates(true)
                    .AnimationInDuration(0.5)
                    .AnimationOutDuration(0)
                    .HasMessage($"New version is available ({result.LastVersion}).")
                    .Dismiss().WithButton("Update now", async button =>
                    {
                        var downloadingBar = new MetroProgressBar()
                        {
                            VerticalAlignment = VerticalAlignment.Bottom,
                            HorizontalAlignment = HorizontalAlignment.Stretch,
                            Height = 3,
                            MinHeight = 3,
                            Foreground = Application.Current.FindResource("MahApps.Brushes.Accent") as Brush,
                            BorderThickness = new Thickness(0),
                            Background = System.Windows.Media.Brushes.Transparent,
                            IsIndeterminate = false
                        };
                        var message = NotificationMessageBuilder()
                            .HasMessage("Downloading update...")
                            .WithOverlay(downloadingBar)
                            .Queue();

                        Log.Information($"Downloading update...");
   
                        await manager.PrepareUpdateAsync(result.LastVersion, new Progress<double>(
                            p => downloadingBar.Value = p * 100)
                        );
                        _notificationMessageManager.Dismiss(message);
                        Log.Information($"Installing update...");

                        manager.LaunchUpdater(result.LastVersion);

                        // Terminate the running application so that the updater can overwrite files
                        Environment.Exit(0);
                    })
                    .Dismiss().WithButton("Later", button => { })
                    .Queue();


            }
        }

        private NotificationMessageBuilder NotificationMessageBuilder()
        {
            NotificationMessageBuilder messageBuilder = _notificationMessageManager.CreateMessage();
            messageBuilder.SetForeground((Application.Current.FindResource("MahApps.Brushes.ThemeBackground") as Brush));
            messageBuilder.SetBackground(Application.Current.FindResource("MahApps.Brushes.ThemeForeground") as Brush);
            messageBuilder.SetAccent(Application.Current.FindResource("MahApps.Brushes.Accent") as Brush);
            return messageBuilder;
        }


        private string UserDataDirPath
        {
            get
            {
                var userPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                var dataDirPath = "AppData\\Local\\GDriveMirror\\User Data";
                var userDataDirPath = Path.Combine(userPath, dataDirPath);
                return userDataDirPath;
            }
        }

        public MirrorTaskExecutioner MTE { get; set; } = new MirrorTaskExecutioner();

        public void Logout()
        {
            // Deletes cached users cookies
            Directory.Delete(UserDataDirPath, true);
            IsSignedIn = false;
            NotifyPropertyChanged(nameof(UserName));
            Log.Information($"Now you are signed out.");

        }

        private void EnsureDirectoryExist(string directory)
        {
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }

        public void ChangePath()
        {
            CommonOpenFileDialog dialog = new CommonOpenFileDialog();
            dialog.IsFolderPicker = true;
            string synchronizePath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) +
                                     Constants.DelimiterInWindowsPath + Constants.ProgramName;
            if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
            {
                synchronizePath = dialog.FileName;
            }

            UserSettings.Default.RootPath = synchronizePath;
            UserSettings.Default.Save();
            LocalRoot = synchronizePath;
            EnsureDirectoryExist(LocalRoot);
            NotifyPropertyChanged(nameof(CanUpload));
            Log.Information($"Directory with photos set to {LocalRoot}.");

        }

        public ICommand ChangePathCommand
        {
            get { return changePath ??= new RelayCommand(ChangePath); }
        }

        public string UserName
        {
            set
            {
                userName = value;
                NotifyPropertyChanged();
            }
            get => !string.IsNullOrEmpty(userName) ? userName : "";
        }


        public ICommand LogoutCommand
        {
            get { return logoutCommand ??= new RelayCommand(Logout); }
        }

        public ICommand StopExecutionCommand
        {
            get
            {
                return stopExecutionCommand ??=
                    new RelayCommand(() => Task.Run(async () => { await MTE.StopExecution(); }).SafeFireAndForget());
            }
        }

        public ICommand ExecuteCommand
        {
            get
            {
                return executeCommand ??=
                    new RelayCommand(() => Task.Run(async () => { await MTE.Execute(); }).SafeFireAndForget());
            }
        }

        public bool IsSignedIn
        {
            get => _isSignedIn;
            set
            {
                _isSignedIn = value;
                if (UserSettings.Default.WasSignedIn != value)
                {
                    UserSettings.Default.WasSignedIn = value;
                    UserSettings.Default.Save();
                }
                NotifyPropertyChanged();
                NotifyPropertyChanged(nameof(CanUpload));
            }
        }

        public ICommand SignInCommand
        {
            get
            {
                return signInCommand ??=
                    new RelayCommand(() => Task.Run(async () => { await SignIn(); }).SafeFireAndForget());
            }
        }

        public bool IsSigningIn
        {
            get => _isSigningIn;
            set
            {
                _isSigningIn = value; 
                NotifyPropertyChanged();
            }
        }

        public bool CanUpload
        {
            get { return !string.IsNullOrEmpty(UserSettings.Default.RootPath) && MTE.IsExecuteButtonEnabled; }
        }

        private async Task SignIn()
        {
            IsSigningIn = true;
            Log.Information($"Signing in...");

            try
            {
                Browser = new BrowserInstance(UserDataDirPath);
                await Browser.LaunchIfClosed();
                //using current Chrome
                //await using var Browser = await Puppeteer.ConnectAsync(new ConnectOptions(){ BrowserURL = "http://127.0.0.1:9222", DefaultViewport = new ViewPortOptions(){Height = 800, Width = 1000}});
                //await using var page = await Browser.NewPageAsync();

                await Browser.CurrentPage.GoToAsync(Constants.GOOGLE_PHOTOS_URL, WaitUntilNavigation.Networkidle0);
                while (true)
                {
                    if (Browser.CurrentPage.Url.Contains(Constants.GOOGLE_PHOTOS_URL))
                    {
                        break;
                    }

                    //wait for login
                    await Browser.CurrentPage.WaitForNavigationAsync();
                }

                UserName = (await Browser.CurrentPage.EvaluateExpressionAsync(
                    @"let username = function() {
                            let elem = document.querySelectorAll('.gb_pe div');
                            let userMail = elem[elem.length-1].innerText;
                            return userMail;
                            };
                            username();")).ToObject<string>();

                IsSignedIn = true;
                Log.Information($"Signed in as {UserName}.");

                var liteDB = new LiteInstance(UserName);
                liteDB.Initialize();

                MTE.StartAction = async () =>
                {
                    await Browser.LaunchIfClosed();
                    var page = Browser.CurrentPage;
                    var rootOpenCreate = new OpenOrCreateAlbumTask(LocalRoot, MTE, page, liteDB);
                    MTE.Enqueue(rootOpenCreate);

                };
                MTE.EndingAction = async () =>
                {
                    await Browser.Close();
                };

            }
            catch (Exception e)
            {
                if (!IsSignedIn)
                {
                    Log.Error($"Signing in ended with error: {e}");
                }
                Console.WriteLine(e);
            }

            IsSigningIn = false;

        }

        public BrowserInstance Browser { get; set; }


        private void NotifyPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}