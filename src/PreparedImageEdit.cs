using System;
using System.Collections.Generic;
using System.IO;

namespace PictureOrganizer
{
    internal sealed class PreparedImageEdit : IDisposable
    {
        public string SourcePath { get; private set; }
        public string PreparedPath { get; private set; }
        public string OriginalHash { get; private set; }
        public string EditedHash { get; set; }

        public PreparedImageEdit(string path)
        {
            SourcePath = path;
            OriginalHash = FileSafety.Hash(path);
            PreparedPath = FileSafety.TemporaryPath(path);
        }

        public void Dispose() { if (File.Exists(PreparedPath)) File.Delete(PreparedPath); }
    }

    internal sealed class FileBatchResult<T>
    {
        public readonly List<T> Completed = new List<T>();
        public readonly List<string> Failures = new List<string>();
    }
}
