﻿using System;

namespace GDriveMirror
{
    public class LiteFile
    {
        public int LiteFileId { get; set; }
        public string LocalPath { get; set; }
        public bool  Uploaded { get; set; }
        public DateTime LastEdit { get; set; }
    }
}