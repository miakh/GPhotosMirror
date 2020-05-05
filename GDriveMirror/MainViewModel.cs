using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using AsyncAwaitBestPractices;
using GalaSoft.MvvmLight.Command;
using Microsoft.WindowsAPICodePack.Dialogs;
using PuppeteerSharp;


namespace GDriveMirror
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

            var browser = await Puppeteer.LaunchAsync(new LaunchOptions
            {
                Headless = false,
                UserDataDir = UserDataDirPath,
                ExecutablePath = executableLocalPath,
                IgnoredDefaultArgs = new[] {"--disable-extensions"},
                DefaultViewport = new ViewPortOptions() {Height = 1000, Width = 1000}
            });

            var page = await browser.NewPageAsync();

            //using current Chrome
            //await using var browser = await Puppeteer.ConnectAsync(new ConnectOptions(){ BrowserURL = "http://127.0.0.1:9222", DefaultViewport = new ViewPortOptions(){Height = 800, Width = 1000}});
            //await using var page = await browser.NewPageAsync();

            await page.GoToAsync(Constants.GOOGLE_PHOTOS_URL, WaitUntilNavigation.Networkidle0);
            while (true)
            {
                if (page.Url.Contains(Constants.GOOGLE_PHOTOS_URL))
                {
                    break;
                }

                //wait for login
                await page.WaitForNavigationAsync();
            }

            UserName = (await page.EvaluateExpressionAsync(
                @"let username = function() {
                        let elem = document.querySelectorAll('.gb_pe div');
                        let userMail = elem[elem.length-1].innerText;
                        return userMail;
                        };
                        username();")).ToObject<string>();


            var liteDB = new LiteInstance(UserName);
            liteDB.Initialize();

            MTE.EndingAction = async () =>
            {
                await page.CloseAsync();
                await browser.CloseAsync();
                await page.DisposeAsync();
                await browser.DisposeAsync();
                liteDB.Dispose();
            };
            var rootOpenCreate = new OpenOrCreateAlbumTask(LocalRoot, MTE, page, liteDB);
            MTE.Enqueue(rootOpenCreate);
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