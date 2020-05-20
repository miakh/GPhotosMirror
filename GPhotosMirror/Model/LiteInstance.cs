using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LiteDB;

namespace GPhotosMirror.Model
{
    public class LiteInstance : IDisposable
    {
        private readonly string _userName;
        private readonly Settings _settings;

        public LiteInstance(string userName, Settings settings)
        {
            this._userName = userName;
            _settings = settings;
        }

        public LiteDatabase LDB { get; set; }

        private ILiteCollection<LiteFile> _liteFiles;

        private ILiteCollection<LiteDirectory> _liteDirectories;

        public void Dispose() => LDB?.Dispose();

        public IEnumerable<LiteFile> GetFilesFromDirectory(string dirPath)
        {
            var relativePath = GetRelativePath(dirPath);
            var dir = _liteDirectories.Include(d => d.LiteFiles).FindOne(d => d.RelativePath == relativePath);
            dir?.LiteFiles.ForEach(
                lf =>
                {
                    lf.LocalPath = CombinePath(dir, lf);
                });

            return dir?.LiteFiles;
        }

        private string CombinePath(LiteDirectory dir, LiteFile lf)
        {
            if (string.IsNullOrEmpty(dir.RelativePath))
            {
                return Path.Combine(_settings.LocalRoot, lf.FileName);
            }
            return Path.Combine(_settings.LocalRoot, dir.RelativePath, lf.FileName);
        }

        public void DirectoryUp(LiteDirectory dir)
        {
            _liteDirectories.Upsert(dir);
        }
        public void DirectoryUpFromLocalPath(string localPath, string link)
        {
            string relativePath = GetRelativePath(localPath);
            var dir = new LiteDirectory() {Link = link, RelativePath = relativePath };
            _liteDirectories.Insert(dir);
        }

        private string GetRelativePath(string localPath)
        {
            string relativePath = null;
            if (!localPath.Equals(_settings.LocalRoot))
            {
                relativePath = Path.GetRelativePath(_settings.LocalRoot, localPath);
            }

            return relativePath;
        }

        public void FilesUp(IEnumerable<string> localFiles, string directoryPath)
        {
            var relativePath = GetRelativePath(directoryPath);
            var dir = _liteDirectories.FindOne(d => d.RelativePath == relativePath);
            var liteFiles = localFiles.Select(f => new LiteFile()
            {
                FileName = Path.GetFileName(f), Uploaded = true, LastEdit = File.GetLastWriteTime(f)
            }).ToList();
            _liteFiles.Upsert(liteFiles);
            dir.LiteFiles.AddRange(liteFiles);
            _liteDirectories.Update(dir);
        }

        public LiteDirectory GetDirectory(string path)
        {
            var relativePath = GetRelativePath(path);
            var directory = _liteDirectories.FindOne(d => d.RelativePath == relativePath);
            if (directory == null)
            {
                return null;
            }
            directory.LocalPath = path;
            return directory;
        }
        public void Initialize()
        {
            // Re-use mapper from global instance
            var mapper = BsonMapper.Global;

            mapper.Entity<LiteDirectory>()
                .Ignore(x => x.LocalPath)
                .DbRef(x => x.LiteFiles, Constants.LITE_FILE);

            mapper.Entity<LiteFile>()
                .Ignore(x => x.LocalPath);

            LDB = new LiteDatabase(_userName + Constants.DatabaseFileName);
            _liteDirectories = LDB.GetCollection<LiteDirectory>(Constants.LITE_DIRECTORY);
            _liteFiles = LDB.GetCollection<LiteFile>(Constants.LITE_FILE);

    }
}
}
