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
using File = Google.Apis.Drive.v3.Data.File;

namespace GDriveMirror
{
    public class MainViewModel : INotifyPropertyChanged
    {

        private string _localRoot = UserSettings.Default.RootPath;
        private ICommand changePath;
        static string[] Scopes = { DriveService.Scope.Drive };
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
                IgnoredDefaultArgs = new[] { "--disable-extensions" },
                DefaultViewport = new ViewPortOptions() { Height = 800, Width = 1000 }
            });

            var page = await browser.NewPageAsync();

            //using current Chrome
            //await using var browser = await Puppeteer.ConnectAsync(new ConnectOptions(){ BrowserURL = "http://127.0.0.1:9222", DefaultViewport = new ViewPortOptions(){Height = 800, Width = 1000}});
            //await using var page = await browser.NewPageAsync();

            await page.GoToAsync(Constants.GOOGLE_DRIVE_URL, WaitUntilNavigation.Networkidle0);
            await page.WaitForRequestAsync(
                r => r.Method == HttpMethod.Get && r.Url.Contains(Constants.GOOGLE_DRIVE_URL),new WaitForOptions(){Timeout = 0});
            await page.EvaluateExpressionAsync(script: "() => alert('This message is inside an alert box')");

            //await page.WaitForSelectorAsync(".js-stream-tweet");

            //var linkButtons = await page.QuerySelectorAllAsync(".js-stream-tweet:not(.favorited) .js-actionFavorite");
            //await linkButtons[0].ClickAsync();

            //await Authorize();

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

            var remoteFolders = files.Where(f => f.MimeType == Constants.MIME_FOLDER_TYPE).Select(f=>f.Name);
            var localFolders = Directory.GetDirectories(localParentPath).Select(Path.GetFileName);
            var foldersToCreate = localFolders.Except(remoteFolders);
            foreach (var f in foldersToCreate)
            {
                var createFolderTask = new CreateFolderTask(driveService, f, parentFolder);
                MTE.Enqueue(createFolderTask);
            }

            //create tasks for uploading photos in localParentPath, which don't exist
            var localFiles = Directory.GetFiles(localParentPath);

            var toUpload = localFiles.Select(Path.GetFileName).Except(files.Where(f => f.MimeType != Constants.MIME_FOLDER_TYPE).Select(f => f.Name));


            foreach (var f in toUpload)
            {
                var uploadPhotoTask = new UploadPhotoTask(driveService,Path.Combine(localParentPath,f), parentFolder);
                MTE.Enqueue(uploadPhotoTask);
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
            get
            { 
                return changePath ??= new RelayCommand(ChangePath);
            }
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
            get { return executeCommand ??= new RelayCommand(()=>Task.Run(async () =>
            {
                await MTE.Execute();
            }).SafeFireAndForget(), () => true);
            }
        }

        private void NotifyPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}