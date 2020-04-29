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
using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Drive.v3.Data;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using Microsoft.WindowsAPICodePack.Dialogs;
using PuppeteerSharp;
using PuppeteerSharp.Input;
using File = Google.Apis.Drive.v3.Data.File;
using Mouse = PuppeteerSharp.Input.Mouse;

namespace GDriveMirror
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private string _localRoot = UserSettings.Default.RootPath;
        private ICommand changePath;
        static string[] Scopes = {DriveService.Scope.Drive};
        UserCredential credential;
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


            //find or create rootdir
            await page.GoToAsync(Constants.GOOGLE_DRIVE_URL, WaitUntilNavigation.Networkidle0);

            //show MyDrive folders
            await page.Keyboard.PressAsync("g");
            await page.Keyboard.PressAsync("f");
            await page.Keyboard.PressAsync("ArrowRight");
            await page.Keyboard.PressAsync("ArrowDown");

            var driveElemetn = await page.WaitForSelectorAsync("DIV.a-U-Li.a-U-Li-Um-mt");
            await page.WaitForFunctionAsync("(d)=>d.getAttribute('aria-hidden') == 'false'", driveElemetn);


            var treeitems = await driveElemetn.QuerySelectorAllAsync(".a-U-Li div > span");
            var innerTexts =
                treeitems.Select(t => t.EvaluateFunctionHandleAsync("(s)=> s.getAttribute('aria-label')", t));
            var list = await Task.WhenAll(innerTexts);
            var remoteDirsInRoot = await Task.WhenAll(list.Select(i => i.JsonValueAsync<string>()));

            //if folder is not found, create new
            var rootFolderIndex = Array.IndexOf(remoteDirsInRoot, LocalRootName);
            if (rootFolderIndex == -1)
            {
                var createNewFolder = new CreateFolderTask(page, LocalRootName);
                await createNewFolder.Proceed();
                //enter to the folder
                await page.Keyboard.PressAsync("Enter");
            }
            else
            {
                await treeitems[rootFolderIndex].FocusAsync();
                await page.WaitForTimeoutAsync(Constants.ShortTimeout);

                await treeitems[rootFolderIndex].ClickAsync();
            }

            //use list view
            var listViewButton = await page.QuerySelectorAsync(
                "div.a-s-tb-sc-Ja-Q.a-s-tb-sc-Ja-Q-Nm.a-Ba-Ed.a-s-Ba-dj > div > div:nth-child(8) path[d='M3,5v14h18V5H3z M7,7v2H5V7H7z M5,13v-2h2v2H5z M5,15h2v2H5V15z M19,17H9v-2h10V17z M19,13H9v-2h10V13z M19,9H9V7h10V9z'");
            if (listViewButton != null)
            {
                try
                {
                    await listViewButton.ClickAsync();
                    await page.WaitForTimeoutAsync(Constants.ShortTimeout);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
            }

            //hide details
            var hideDetailsButton = await page.QuerySelectorAsync(".a-ub-va-d");
            if (hideDetailsButton != null && await hideDetailsButton.IsIntersectingViewportAsync())
            {
                try
                {
                    await hideDetailsButton.ClickAsync();
                    await page.WaitForTimeoutAsync(Constants.ShortTimeout);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
            }

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
            //wait to load

            var folderArea = await page.WaitForSelectorAsync("DIV.WYuW0e.RDfNAe.dPmH0b > DIV");
            await page.WaitForFunctionAsync("(d)=>d.getAttribute('tabindex') == '0'", folderArea);

            //get folders
            var remoteFolders = (await page.EvaluateExpressionAsync(
                @"let hello = function() {
    let elements = document.querySelectorAll('.Zz99o');
    let elem = elements[elements.length - 2];
    let folders = elem.querySelectorAll('.iZmuQc > .pmHCK .KL4NAf');
    folders = Array.from(folders);
    return folders.map(f=>f.textContent);
};
hello();")).ToObject<string[]>();

            var localFolders = Directory.GetDirectories(localParentPath).Select(Path.GetFileName);
            var foldersToCreate = localFolders.Except(remoteFolders);
            foreach (var f in foldersToCreate)
            {
                var createFolderTask = new CreateFolderTask(page, f);
                MTE.Enqueue(createFolderTask);
            }

            //get files
            var remoteFiles = (await page.EvaluateExpressionAsync(
                @"let getFiles = function() {
    let elements = document.querySelectorAll('.Zz99o');
    let elem = elements[elements.length - 1];
    let folders = elem.querySelectorAll('.iZmuQc > .pmHCK .KL4NAf');
    folders = Array.from(folders);
    return folders.map(f=>f.textContent);
};
getFiles();")).ToObject<string[]>();

            var localFiles = Directory.GetFiles(localParentPath);

            var toUpload = localFiles.Select(Path.GetFileName).Except(remoteFiles);
            foreach (var f in toUpload)
            {
                var uploadPhotoTask = new UploadPhotoTask(page, Path.Combine(localParentPath, f));
                MTE.Enqueue(uploadPhotoTask);
            }

            await MTE.Execute();
            await page.WaitForTimeoutAsync(500);
        }

        public MirrorTaskExecutioner MTE { get; set; } = new MirrorTaskExecutioner();

        private async Task MirrorFolder(DriveService driveService, string localParentPath, File parentFolder)
        {
            var parentName = Path.GetFileName(Path.GetDirectoryName(localParentPath));

            //create task for creating new subfolders if not exist, or get their IDs
            FilesResource.ListRequest listRequest = driveService.Files.List();

            listRequest.PageSize = 1000;
            listRequest.Q = $"'{parentFolder.Id}' in parents and trashed = false";
            listRequest.Fields = "nextPageToken, files(id, name, mimeType)";
            IList<File> files = (await listRequest.ExecuteAsync())
                .Files;

            var remoteFolders = files.Where(f => f.MimeType == Constants.MIME_FOLDER_TYPE).Select(f => f.Name);
            var localFolders = Directory.GetDirectories(localParentPath).Select(Path.GetFileName);
            var foldersToCreate = localFolders.Except(remoteFolders);
            foreach (var f in foldersToCreate)
            {
            }

            //create tasks for uploading photos in localParentPath, which don't exist
            var localFiles = Directory.GetFiles(localParentPath);

            var toUpload = localFiles.Select(Path.GetFileName)
                .Except(files.Where(f => f.MimeType != Constants.MIME_FOLDER_TYPE).Select(f => f.Name));


            foreach (var f in toUpload)
            {
            }

            //Recursively Mirror local subfolders
        }

        private async Task<File> CreateOrGetRoot(DriveService service, string localRootPath)
        {
            var rootName = Path.GetFileName(localRootPath);


            // Define parameters of request.
            FilesResource.ListRequest listRequest = service.Files.List();

            listRequest.PageSize = 10;
            //find or create mirror folder in _localRoot

            listRequest.Q = $"'root' in parents and name = '{rootName}'";
            listRequest.Fields = "nextPageToken, files(id, name)";

            IList<File> files = (await listRequest.ExecuteAsync())
                .Files;

            File rootBody = files.FirstOrDefault();
            if (!files.Any())
            {
                rootBody = new File();
                rootBody.Name = rootName;
                rootBody.MimeType = Constants.MIME_FOLDER_TYPE;
                await service.Files.Create(rootBody).ExecuteAsync();
            }

            return rootBody;
        }


        public async void Logout()
        {
            await credential.RevokeTokenAsync(CancellationToken.None);
            credential = null;
            NotifyPropertyChanged(nameof(UserName));
        }

        public async Task Authorize()
        {
            var path = Environment.ExpandEnvironmentVariables(
                @"%APPDATA%\Microsoft\UserSecrets\618749fb-9d4e-4fa6-a163-0438054eddd6\secrets.json");
            using (var stream =
                new FileStream(path, FileMode.Open, FileAccess.Read))
            {
                // The file token.json stores the user's access and refresh tokens, and is created
                // automatically when the authorization flow completes for the first time.
                string credPath = "token.json";
                credential = GoogleWebAuthorizationBroker.AuthorizeAsync(
                    GoogleClientSecrets.Load(stream).Secrets,
                    Scopes,
                    "user",
                    CancellationToken.None,
                    new FileDataStore(credPath, true)).Result;
                Debug.WriteLine("Credential file saved to: " + credPath);
            }

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
                if (credential != null && !string.IsNullOrEmpty(credential.UserId))
                {
                    var localTime = TimeZoneInfo.ConvertTimeFromUtc(credential.Token.IssuedUtc, TimeZoneInfo.Local);
                    return "Logged since " + localTime.ToString("F");
                }

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