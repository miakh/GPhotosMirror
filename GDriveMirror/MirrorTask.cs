using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Google.Apis.Drive.v3;
using Google.Apis.Drive.v3.Data;
using Microsoft.WindowsAPICodePack.Shell.PropertySystem;
using File = Google.Apis.Drive.v3.Data.File;

namespace GDriveMirror
{
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
        public string LocalFilePath { get; }
        public File Parent { get; }

        public long FileSize
        {
            get;
        }

        public override async Task Proceed()
        {
            var filename = Path.GetFileName(LocalFilePath);
            var body = new File()
            {
                Name = filename
            };
            body.Parents= new List<string>();
            body.Parents.Add(Parent.Id);
            var modification = System.IO.File.GetLastWriteTime(LocalFilePath);
            //var modificationTimeString = modification.ToString("yyyy-MM-dd'T'HH:mm:ss.fffzzz", DateTimeFormatInfo.InvariantInfo);

            FilesResource.CreateMediaUpload request;
            using (var stream = new System.IO.FileStream(LocalFilePath,
                System.IO.FileMode.Open))
            {   
                request = _driveService.Files.Create(
                    body, stream, "image/jpeg");
                request.Fields = "id";
                await request.UploadAsync();
            }
            var fileID = request.ResponseBody.Id;
            var file = new File {ModifiedTime = modification};
            var updateRequest = _driveService.Files.Update(file, fileID);
            await updateRequest.ExecuteAsync();
            //move uploaded file to certain folder
            //var updateRequest = _driveService.Files.Update(new File(), file.Id);

            //updateRequest.AddParents = Parent.Id;
            //updateRequest.RemoveParents = file.Parents[0];
            //var movedFile = await updateRequest.ExecuteAsync();
        }

        public UploadPhotoTask(DriveService driveService, string localFilePath, File parent) : base(driveService)
        {
            LocalFilePath = localFilePath;
            FileSize = new FileInfo(LocalFilePath).Length;
            Parent = parent;
        }
    }
}
