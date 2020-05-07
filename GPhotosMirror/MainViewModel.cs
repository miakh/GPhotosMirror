using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using AsyncAwaitBestPractices;
using GalaSoft.MvvmLight.Command;
using Microsoft.WindowsAPICodePack.Dialogs;
using PuppeteerSharp;

namespace GPhotosMirror
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private string _localRoot = UserSettings.Default.RootPath;
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

        public string LocalRootName
        {
            get => new DirectoryInfo(LocalRoot).Name;
        }

        public MainViewModel()
        {
            TScheduler = TaskScheduler.FromCurrentSynchronizationContext();
            Initialize();
        }


        private string userName;
        private RelayCommand stopExecutionCommand;

        public async void Initialize()
        {
            if (string.IsNullOrEmpty(UserSettings.Default.RootPath))
            {
                ChangePath();
            }

            var browser = new BrowserInstance(UserDataDirPath);
            await browser.LaunchIfClosed();
            //using current Chrome
            //await using var browser = await Puppeteer.ConnectAsync(new ConnectOptions(){ BrowserURL = "http://127.0.0.1:9222", DefaultViewport = new ViewPortOptions(){Height = 800, Width = 1000}});
            //await using var page = await browser.NewPageAsync();

            await browser.CurrentPage.GoToAsync(Constants.GOOGLE_PHOTOS_URL, WaitUntilNavigation.Networkidle0);
            while (true)
            {
                if (browser.CurrentPage.Url.Contains(Constants.GOOGLE_PHOTOS_URL))
                {
                    break;
                }

                //wait for login
                await browser.CurrentPage.WaitForNavigationAsync();
            }

            UserName = (await browser.CurrentPage.EvaluateExpressionAsync(
                @"let username = function() {
                        let elem = document.querySelectorAll('.gb_pe div');
                        let userMail = elem[elem.length-1].innerText;
                        return userMail;
                        };
                        username();")).ToObject<string>();


            var liteDB = new LiteInstance(UserName);
            liteDB.Initialize();

            MTE.StartAction = async () =>
            {
                await browser.LaunchIfClosed();
                var page = browser.CurrentPage;
                var rootOpenCreate = new OpenOrCreateAlbumTask(LocalRoot, MTE, page, liteDB);
                MTE.Enqueue(rootOpenCreate);

            };
            MTE.EndingAction = async () =>
            {
                await browser.Close();
            };

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

        public async void Logout()
        {
            Directory.Delete(UserDataDirPath, true);
            NotifyPropertyChanged(nameof(UserName));
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
            get => !string.IsNullOrEmpty(userName) ? userName : "Not logged in";
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

        private void NotifyPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}