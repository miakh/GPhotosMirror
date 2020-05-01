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

            var userPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var dataDirPath = "AppData\\Local\\GDriveMirror\\User Data";
            var userDataDirPath = Path.Combine(userPath, dataDirPath);

            //close your browser if exception
            //or start bundled

            browser = await Puppeteer.LaunchAsync(new LaunchOptions
            {
                Headless = false,
                UserDataDir = userDataDirPath,
                ExecutablePath = executableLocalPath,
                IgnoredDefaultArgs = new[] {"--disable-extensions"},
                
                DefaultViewport = new ViewPortOptions() {Height = 800, Width = 1000}
            });

            var page = await browser.NewPageAsync();

            //using current Chrome
            //await using var browser = await Puppeteer.ConnectAsync(new ConnectOptions(){ BrowserURL = "http://127.0.0.1:9222", DefaultViewport = new ViewPortOptions(){Height = 800, Width = 1000}});
            //await using var page = await browser.NewPageAsync();



            
            //now recursively mirror folders
            await MirrorFolderWeb(page, LocalRoot);


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

        private async Task MirrorFolderWeb(Page page, string localParentPath)
        {
            await page.GoToAsync(Constants.GOOGLE_PHOTOS_URL_SEARCH, WaitUntilNavigation.Networkidle0);

            //if localParent contains files
            var localFilesNames = Directory.GetFiles(localParentPath);
            if (localFilesNames.Any())
            {
                var localFiles = Directory.GetFiles(localParentPath);
                var uploadPhotos = new UploadPhotosTask(page, localFiles);

                var folderName = Path.GetFileName(localParentPath);
                //try to find or create album named like localParent
                await page.Keyboard.PressAsync("/");
                //var searchInput = await page.QuerySelectorAsync("DIV.d1dlne");
                //await searchInput.FocusAsync();
                //await page.WaitForSelectorAsync("BODY.EIlDfe");

                //await page.WaitForSelectorAsync("DIV.d1dlne[data-expanded='true']");
                await page.Keyboard.TypeAsync(folderName);

                //await page.Keyboard.TypeAsync();
                var searchHintArea = await page.WaitForSelectorAsync(".u3WVdc.jBmls[data-expanded=true]", Constants.NoTimeoutOptions);
                await page.WaitForTimeoutAsync(Constants.LongTimeout);
                var createAlbumTask = new CreateAlbumTask(page, folderName);

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
                MTE.Enqueue(uploadPhotos);
                await MTE.Execute();

            }
            var localFolders = Directory.GetDirectories(localParentPath);
            
            foreach (var folder in localFolders)
            {
                await MirrorFolderWeb(page, folder);
            }
        }

        public MirrorTaskExecutioner MTE { get; set; } = new MirrorTaskExecutioner();



        public async void Logout()
        {
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
            get
            {

                return "Not logged in";
            }
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