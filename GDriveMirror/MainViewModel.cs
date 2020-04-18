using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using AsyncAwaitBestPractices;
using GalaSoft.MvvmLight.Command;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using Microsoft.WindowsAPICodePack.Dialogs;

namespace GDriveMirror
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private string root;
        private string test2;
        private string lol;
        private ICommand changePath;
        static string[] Scopes = { DriveService.Scope.Drive };
        UserCredential credential;
        private RelayCommand logoutCommand;


        public event PropertyChangedEventHandler PropertyChanged;

        public string Root
        {
            get { return root; }
            set
            {
                root = value;
                NotifyPropertyChanged();
            }
        }

        public MainViewModel()
        {
            Initialize();
        }
        public async void Initialize()
        {
            var synchronizePath = UserSettings.Default.RootPath;
            if (string.IsNullOrEmpty(synchronizePath))
            {
               ChangePath();
            }

            await Authorize();

            Task.Run(() =>
            {
                //UpdateMeta();
            }).SafeFireAndForget();
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


            // Create Drive API service.
            var service = new DriveService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = credential,
                ApplicationName = Application.Current.MainWindow.GetType().Assembly.GetName().Name,
            });

            // Define parameters of request.
            FilesResource.ListRequest listRequest = service.Files.List();
            listRequest.PageSize = 10;
            listRequest.Fields = "nextPageToken, files(id, name)";

            // List files.
            IList<Google.Apis.Drive.v3.Data.File> files = listRequest.Execute()
                .Files;

            Debug.WriteLine("Files:");
            if (files != null && files.Count > 0)
            {
                foreach (var file in files)
                {
                    Debug.WriteLine("{0} ({1})", file.Name, file.Id);
                }
            }
            else
            {
                Debug.WriteLine("No files found.");
            }
            Console.Read();

            //OnPropertyChanged(nameof(IsLoggedIn));
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
            Root = synchronizePath;
            EnsureDirectoryExist(Root);
            
            
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

        private void NotifyPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}