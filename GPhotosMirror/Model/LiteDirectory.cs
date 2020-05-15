using System.Collections.Generic;

namespace GPhotosMirror
{
    public class LiteDirectory
    {
        public int LiteDirectoryId { get; set; }
        public string LocalPath { get; set; }
        public string Link { get; set; }
        public List<LiteFile> LiteFiles { get; set; } = new List<LiteFile>();
    }
}