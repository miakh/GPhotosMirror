using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using GDriveMirror.Annotations;
using Google.Apis.Drive.v3;
using Google.Apis.Drive.v3.Data;
using Microsoft.WindowsAPICodePack.Shell.PropertySystem;
using File = Google.Apis.Drive.v3.Data.File;

namespace GDriveMirror
{

    public class MirrorTaskExecutioner:INotifyPropertyChanged
    {
        private Queue<MirrorTask> MirrorTasks = new Queue<MirrorTask>();
        private bool _isExecuting;

        public MirrorTaskExecutioner()
        {
        }

        public bool IsExecuting
        {
            get => _isExecuting;
            set
            {
                _isExecuting = value;
                OnPropertyChanged();
            }
        }

        public async Task Execute()
        {
            IsExecuting = true;
            while (MirrorTasks.Count>0)
            {
                var task = MirrorTasks.Dequeue();
                await task.Proceed();
            }
            IsExecuting = false;
        }

        public async Task PauseExecution()
        {

        }

        public void Enqueue(MirrorTask mt)
        {
            MirrorTasks.Enqueue(mt);
        }

        public MirrorTask Dequeue()
        {
            return MirrorTasks.Dequeue();
        }

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
    public class MirrorTask
    {
        protected readonly DriveService _driveService;

        public MirrorTask(DriveService driveService)
        {
            _driveService = driveService;
        }
        public async virtual Task Proceed()
        {

        }
    }



    public class CreateFolderTask:MirrorTask
    {
        public File Parent { get; }
        public string LocalFoldername { get; }

        public override async Task Proceed()
        {
            var body = new File {Name = LocalFoldername, MimeType = "application/vnd.google-apps.folder"};
            body.Parents = new List<string>();
            body.Parents.Add(Parent.Id);

            await _driveService.Files.Create(body).ExecuteAsync();
        }
        public CreateFolderTask(DriveService driveService, string localFoldername, File parent) : base(driveService)
        {
            LocalFoldername = localFoldername;
            Parent = parent;
        }
    }

    public class UploadPhotoTask : MirrorTask
    {
        public string LocalFilename { get; }
        public File Parent { get; }
        public override async Task Proceed()
        {
            var filename = Path.GetFileName(LocalFilename);
            var body = new File()
            {
                Name = filename
            };
            body.Parents= new List<string>();
            body.Parents.Add(Parent.Id);
            var modification = System.IO.File.GetLastWriteTime(@"C:\test.txt");
            //var modificationTimeString = modification.ToString("yyyy-MM-dd'T'HH:mm:ss.fffzzz", DateTimeFormatInfo.InvariantInfo);

            FilesResource.CreateMediaUpload request;
            using (var stream = new System.IO.FileStream(LocalFilename,
                System.IO.FileMode.Open))
            {   
                request = _driveService.Files.Create(
                    body, stream, "image/jpeg");
                request.Fields = "id";
                await request.UploadAsync();
            }
            var file = request.ResponseBody;
            file.ModifiedTime = modification;
            var updateRequest = _driveService.Files.Update(file, file.Id);
            await updateRequest.ExecuteAsync();
            //move uploaded file to certain folder
            //var updateRequest = _driveService.Files.Update(new File(), file.Id);

            //updateRequest.AddParents = Parent.Id;
            //updateRequest.RemoveParents = file.Parents[0];
            //var movedFile = await updateRequest.ExecuteAsync();
        }

        public UploadPhotoTask(DriveService driveService, string localFilename, File parent) : base(driveService)
        {
            LocalFilename = localFilename;
            Parent = parent;
        }
    }
}
