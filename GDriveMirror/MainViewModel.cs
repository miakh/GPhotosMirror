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


        private Browser browser = null;
        private string userName;

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

            browser = await Puppeteer.LaunchAsync(new LaunchOptions
            {
                Headless = false,
                UserDataDir = UserDataDirPath,
                ExecutablePath = executableLocalPath,
                IgnoredDefaultArgs = new[] {"--disable-extensions"},
                DefaultViewport = new ViewPortOptions() {Height = 900, Width = 1000}
            });

            await using var page = await browser.NewPageAsync();

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
                        let userMail = elem[elem.Length-1].textContent;
                        return userMail;
                        };
                        username();")).ToObject<string>();


            using var liteDB = new LiteInstance(UserName);
            liteDB.Initialize();

            //now recursively mirror folders
            try
            {
                await MirrorFolderWeb(LocalRoot, page, liteDB);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }


            //var appName = Application.Current.MainWindow.GetType().Assembly.GetName().Name;
            //Task.Run(async () =>
            //{
            //    // Create Drive API service.
            //    var service = new DriveService(new BaseClientService.Initializer()
            //    {
            //        HttpClientInitializer = credential,
            //        ApplicationName = appName,
            //    });

            //    var rootBody = await CreateOrGetRoot(service, LocalRoot);
            //    await MirrorFolder(service, LocalRoot, rootBody);
            //    MTE.PreExecute();

            //}).SafeFireAndForget();
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

        private async Task MirrorFolderWeb(string parent, Page page, LiteInstance liteDB)
        {
            //if localParent contains files
            var localFiles = Directory.GetFiles(parent);
            var filesUp = liteDB.GetFilesFromDirectory(parent);
            IEnumerable<string> filesToGoUp = null;
            if (filesUp != null)
            {
                filesToGoUp = localFiles.Except(filesUp.Select(f => f.LocalPath));
            }
            else
            {
                filesToGoUp = localFiles;
            }

            var filesToGoUpList = filesToGoUp.ToList();
            if (filesToGoUpList.Any())
            {
                var dirUp = liteDB.LiteDirectories.FindOne(d => d.LocalPath == parent);
                if (dirUp == null)
                {
                    if (!page.Url.Equals(Constants.GOOGLE_PHOTOS_URL_SEARCH))
                    {
                        await page.GoToAsync(Constants.GOOGLE_PHOTOS_URL_SEARCH, WaitUntilNavigation.Networkidle0);
                    }

                    var folderName = Path.GetFileName(parent);
                    //try to find or create album named like localParent
                    await page.Keyboard.PressAsync("/");
                    await page.Keyboard.TypeAsync(folderName);

                    var searchHintArea = await page.WaitForSelectorAsync(".u3WVdc.jBmls[data-expanded=true]",
                        Constants.NoTimeoutOptions);
                    await page.WaitForTimeoutAsync(Constants.LongTimeout);
                    var createAlbumTask = new CreateAlbumTask(parent, page, liteDB);

                    if (searchHintArea == null)
                    {
                        MTE.Enqueue(createAlbumTask);
                    }
                    else
                    {
                        var searchHints = await searchHintArea.QuerySelectorAllAsync(".MkjOTb.oKubKe.lySfNc");

                        var remoteAlbums = (await page.EvaluateExpressionAsync(
                            @"let hello = function() {
                            let elem = document.querySelector('.u3WVdc.jBmls[data-expanded=true]');
                            let folders = elem.querySelectorAll('.lROwub');
                            folders = Array.from(folders);
                            return folders.map(f => f.textContent);
                            };
                            hello();")).ToObject<string[]>();
                        var clickIndex = Array.IndexOf(remoteAlbums, folderName);
                        if (clickIndex == -1)
                        {
                            MTE.Enqueue(createAlbumTask);
                        }
                        else
                        {
                            //album already exist
                            await searchHints[clickIndex].ClickAsync();
                        }
                    }
                }
                else
                {
                    await page.GoToAsync(Constants.GOOGLE_PHOTOS_ALBUM_URL + dirUp.Link,
                        WaitUntilNavigation.Networkidle0);
                }

                var uploadPhotos = new UploadPhotosTask(filesToGoUpList, parent, page, liteDB);
                MTE.Enqueue(uploadPhotos);
                await MTE.Execute();
            }

            var localFolders = Directory.GetDirectories(parent);

            foreach (var folder in localFolders)
            {
                await MirrorFolderWeb(folder, page, liteDB);
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

        public ICommand ExecuteCommand
        {
            get
            {
                return executeCommand ??=
                    new RelayCommand(() => Task.Run(async () => { await MTE.Execute(); }).SafeFireAndForget(),
                        () => true);
            }
        }

        private void NotifyPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}