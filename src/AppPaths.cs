using System;
using System.IO;

namespace PictureOrganizer
{
    internal static class AppPaths
    {
        public static readonly string ConfigDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "PhotoOrganizer");

        public static readonly string ConfigFilePath = Path.Combine(ConfigDirectory, "PhotoOrganizer.config");
        public static readonly string ChangeLogFilePath = Path.Combine(ConfigDirectory, "PhotoOrganizer.changes.ndjson");
        public static readonly string UndoBackupDirectory = Path.Combine(ConfigDirectory, "UndoBackups");
    }
}
