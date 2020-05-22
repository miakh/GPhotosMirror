using System;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using AsyncAwaitBestPractices;
using Enterwell.Clients.Wpf.Notifications;
using GalaSoft.MvvmLight.Command;
using Microsoft.WindowsAPICodePack.Dialogs;
using Onova;
using Onova.Models;
using Onova.Services;
using PuppeteerSharp;
using Serilog;

namespace GPhotosMirror.Model
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly GPhotosNotifications _notificationMessageManager;
        private ICommand _changePath;
        private RelayCommand _executeCommand;
        private RelayCommand _logoutCommand;
        private RelayCommand _signInCommand;
        private RelayCommand _stopExecutionCommand;


        public MainViewModel(GPhotosNotifications notificationMessageManager, BrowserInstance browserInstance,
            GUser gUser, Settings settings)
        {
            Browser = browserInstance;
            _notificationMessageManager = notificationMessageManager;
            User = gUser;
            Settings = settings;
            TScheduler = TaskScheduler.FromCurrentSynchronizationContext();
        }

        private TaskScheduler TScheduler { get; }

        public GUser User { get; }

        public Settings Settings { get; }

        public MirrorTaskExecutioner MTE { get; set; } = new MirrorTaskExecutioner();

        public ICommand ChangePathCommand => _changePath ??= new RelayCommand(ChangePath);


        public ICommand LogoutCommand => _logoutCommand ??= new RelayCommand(Logout);

        public ICommand StopExecutionCommand =>
            _stopExecutionCommand ??=
                new RelayCommand(() => MTE.StopExecution().SafeFireAndForget());

        public ICommand ExecuteCommand =>
            _executeCommand ??=
                new RelayCommand(() => MTE.Execute().SafeFireAndForget());


        public ICommand SignInCommand =>
            _signInCommand ??=
                new RelayCommand(() => SignIn().SafeFireAndForget());


        public bool CanUpload => !string.IsNullOrEmpty(Settings.LocalRoot) && MTE.IsExecuteButtonEnabled;

        public BrowserInstance Browser { get; set; }


        public void ViewModelLoaded() => Initialize();

        public async Task OnUIContext(Action action, CancellationToken cancellationToken = default) =>
            await Task.Factory.StartNew(action, cancellationToken, TaskCreationOptions.None, TScheduler);

        public async void Initialize()
        {
            CheckAndUpdate().SafeFireAndForget();

            Log.Information(
                $"Welcome in GPhotosMirror (version {Assembly.GetExecutingAssembly().GetName().Version.ToString(3)})!");

            Settings.Reload();

            if (string.IsNullOrWhiteSpace(Settings.LocalRoot) || !Settings.WasSignedIn)
            {
                Log.Information($"Sign in and choose folder you want to enable upload to Google Photos.");
                Log.Information($"Folder and all the subfolders will be uploaded as independent albums.");
                Log.Information($"If you the photo is already uploaded then it is skipped.");
            }

            // Sign in just to make sure it is possible
            if (Settings.WasSignedIn)
            {
                SignIn().SafeFireAndForget();
            }

            MTE.PropertyChanged += (sender, args) =>
            {
                if (args.PropertyName == nameof(MTE.IsExecuteButtonEnabled))
                {
                    OnPropertyChanged(nameof(CanUpload));
                }
            };

            User.PropertyChanged += (sender, args) =>
            {
                if (args.PropertyName == nameof(User.IsSignedIn))
                {
                    OnPropertyChanged(nameof(CanUpload));
                }
            };
        }

        private async Task CheckAndUpdate()
        {
            try
            {
                // Check for updates
                UpdateManager manager = new UpdateManager(
                    new GithubPackageResolver("miakh", "GPhotosMirror", "GPhotosMirror*.zip"),
                    new ZipPackageExtractor());

                CheckForUpdatesResult result = await manager.CheckForUpdatesAsync();
                if (result.CanUpdate)
                {
                    _notificationMessageManager
                        .NotificationMessageBuilder()
                        .Animates(true)
                        .AnimationInDuration(0.5)
                        .AnimationOutDuration(0)
                        .HasMessage($"New version is available ({result.LastVersion}).")
                        .Dismiss().WithButton("Update now", async button =>
                        {
                            var downloadingBar = _notificationMessageManager.DownloadingBar;
                            INotificationMessage message = _notificationMessageManager
                                .NotificationMessageBuilder()
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

                            // Close browser
                            if (Browser != null)
                            {
                                await Browser.Close();
                            }

                            // Terminate the running application so that the updater can overwrite files
                            Environment.Exit(0);
                        })
                        .Dismiss().WithButton("Later", button => { })
                        .Queue();
                }
            }
            catch (Exception e)
            {
                Log.Error($"Error while checking or getting new version.");
            }
        }

        public void Logout()
        {
            // Deletes cached users cookies
            Browser.DeleteUserData();
            User.IsSignedIn = false;
            OnPropertyChanged(nameof(User.UserName));
            Log.Information($"Now you are signed out.");
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

            Settings.LocalRoot = synchronizePath;

            OnPropertyChanged(nameof(CanUpload));
            Log.Information($"Directory with photos set to {Settings.LocalRoot}.");
        }

        private async Task SignIn()
        {
            try
            {
                User.IsSigningIn = true;
                Log.Information($"Signing in...");

                await Browser.LaunchIfClosed();
                //using current Chrome
                //await using var Browser = await Puppeteer.ConnectAsync(new ConnectOptions(){ BrowserURL = "http://127.0.0.1:9222", DefaultViewport = new ViewPortOptions(){Height = 800, Width = 1000}});
                //await using var page = await Browser.NewPageAsync();

                await Browser.CurrentPageInstance.GoToAsync(Constants.GOOGLE_PHOTOS_LOGIN_URL,
                    WaitUntilNavigation.Networkidle0);
                while (true)
                {
                    if (Browser.CurrentPageInstance.Url.Contains(Constants.GOOGLE_PHOTOS_URL))
                    {
                        break;
                    }

                    //wait for login
                    await Browser.CurrentPageInstance.WaitForNavigationAsync(Constants.NoNavigationTimeoutOptions);
                }

                User.UserName = (await Browser.CurrentPageInstance.EvaluateExpressionAsync(
                    @"let username = function() {
                            let elem = document.querySelectorAll('.gb_pe div');
                            let userMail = elem[elem.length-1].innerText;
                            return userMail;
                            };
                            username();")).ToObject<string>();

                User.IsSignedIn = true;
                Log.Information($"Signed in as {User.UserName}.");

                LiteInstance liteDB = new LiteInstance(User.UserName, Settings);
                liteDB.Initialize();

                MTE.StartAction = async () =>
                {
                    await Browser.LaunchIfClosed();
                    Page page = Browser.CurrentPageInstance;
                    OpenOrCreateAlbumTask rootOpenCreate =
                        new OpenOrCreateAlbumTask(Settings.LocalRoot, MTE, page, liteDB);
                    MTE.Enqueue(rootOpenCreate);
                };
                MTE.EndingAction = async () => { await Browser.Close(); };
            }
            catch (Exception e)
            {
                if (!User.IsSignedIn)
                {
                    if (e is TargetClosedException || (Browser.CurrentPageInstance!=null&&Browser.CurrentPageInstance.IsClosed))
                    {
                        Log.Error($"Browser closed. Signing in ended.");
                    }
                    else
                    {
                        Log.Error($"Signing in ended with error: {e}");
                    }
                }
                else
                {
                    Log.Error($"Error: {e}");
                }

            }

            User.IsSigningIn = false;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        [Annotations.NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
