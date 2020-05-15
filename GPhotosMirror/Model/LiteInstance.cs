using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LiteDB;

namespace GPhotosMirror
{
    public class LiteInstance:IDisposable
    {
        private string userName;

        public LiteInstance(string userName)
        {
            this.userName = userName;
        }

        public IEnumerable<LiteFile> GetFilesFromDirectory(string dirPath)
        {
            var dir = LiteDirectories.Include(d=>d.LiteFiles).FindOne(d => d.LocalPath == dirPath);
            return dir?.LiteFiles;
        }

        public void DirectoryUp(string path, string link)
        {
            var dir = new LiteDirectory(){Link = link, LocalPath = path};
            LiteDirectories.Insert(dir);
        }
        public void FilesUp(IEnumerable<string> files, string directoryPath)
        {
            var dir = LiteDirectories.FindOne(d => d.LocalPath == directoryPath);
            var liteFiles = files.Select(f => new LiteFile()
                {LocalPath = f, Uploaded = true, LastEdit = File.GetLastWriteTime(f)}).ToList();
            LiteFiles.Upsert(liteFiles);
            dir.LiteFiles.AddRange(liteFiles);
            LiteDirectories.Update(dir);
        }
        public LiteDatabase LDB { get; set; }

        public void Initialize()
        {
            // Re-use mapper from global instance
            var mapper = BsonMapper.Global;

            // "Products" and "Customer" are from other collections (not embedded document)
            mapper.Entity<LiteDirectory>()
                .DbRef(x => x.LiteFiles, Constants.LITE_FILE);

            LDB = new LiteDatabase(userName+Constants.DatabaseFileName);
            LiteDirectories = LDB.GetCollection<LiteDirectory>(Constants.LITE_DIRECTORY);
            LiteFiles = LDB.GetCollection<LiteFile>(Constants.LITE_FILE);
        }

        private ILiteCollection<LiteFile> LiteFiles
        { get; set; }

        public ILiteCollection<LiteDirectory> LiteDirectories { get; set; }

        public void Dispose()
        {
            LDB?.Dispose();
        }
    }
}