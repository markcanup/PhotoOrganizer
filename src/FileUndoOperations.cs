using System;
using System.IO;

namespace PictureOrganizer
{
    internal static class FileUndoOperations
    {
        public static void UndoCopy(PathPairChange change, bool allowLegacy)
        {
            RequireReplacementBackup(change, allowLegacy);
            if (FileSafety.Matches(change.DestinationPath, change.ReplacedHash))
            { FileSafety.Restore(change.ReplacedBackupPath, change.DestinationPath, change.ReplacedHash, allowLegacy); return; }
            FileSafety.RequireHash(change.DestinationPath, change.DestinationHash, allowLegacy);
            if (!string.IsNullOrEmpty(change.ReplacedBackupPath))
                FileSafety.Restore(change.ReplacedBackupPath, change.DestinationPath, change.ReplacedHash, allowLegacy);
            else File.Delete(change.DestinationPath);
        }

        public static void UndoMove(PathPairChange change, bool allowLegacy)
        {
            RequireReplacementBackup(change, allowLegacy);
            if (FileSafety.Matches(change.DestinationPath, change.ReplacedHash) && FileSafety.Matches(change.SourcePath, change.DestinationHash))
            { FileSafety.Restore(change.ReplacedBackupPath, change.DestinationPath, change.ReplacedHash, allowLegacy); return; }
            FileSafety.RequireHash(change.DestinationPath, change.DestinationHash, allowLegacy);
            if (File.Exists(change.SourcePath))
            {
                // A prepared transfer or a retry may already have restored the source.
                if (string.IsNullOrEmpty(change.DestinationHash)) throw new IOException("The original path is already occupied: " + change.SourcePath);
                FileSafety.RequireHash(change.SourcePath, change.DestinationHash);
            }
            else FileSafety.Restore(change.DestinationPath, change.SourcePath, change.DestinationHash, allowLegacy);
            UndoCopy(change, allowLegacy);
        }

        public static void UndoRename(PathPairChange change, bool allowLegacy)
        {
            if (!File.Exists(change.DestinationPath) && FileSafety.Matches(change.SourcePath, change.DestinationHash)) return;
            FileSafety.RequireHash(change.DestinationPath, change.DestinationHash, allowLegacy);
            if (File.Exists(change.SourcePath)) throw new IOException("The original path is already occupied: " + change.SourcePath);
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(change.SourcePath)));
            File.Move(change.DestinationPath, change.SourcePath);
        }

        public static void UndoConvert(ConvertFileChange change, bool allowLegacy)
        {
            FileSafety.RequireHash(change.CreatedPath, change.CreatedHash, allowLegacy);
            File.Delete(change.CreatedPath);
        }

        public static void UndoEdit(RotateBackupChange change, bool allowLegacy)
        {
            FileSafety.RequireHash(change.BackupPath, change.BackupHash, allowLegacy);
            if (FileSafety.Matches(change.FilePath, change.BackupHash))
            { FileSafety.Restore(change.BackupPath, change.FilePath, change.BackupHash, allowLegacy); return; }
            FileSafety.RequireHash(change.FilePath, change.EditedHash, allowLegacy);
            FileSafety.Restore(change.BackupPath, change.FilePath, change.BackupHash, allowLegacy);
        }

        public static void UndoDelete(DeleteBackupChange change, bool allowLegacy)
        {
            FileSafety.RequireHash(change.BackupPath, change.BackupHash, allowLegacy);
            if (File.Exists(change.OriginalPath))
            {
                if (FileSafety.Matches(change.OriginalPath, change.BackupHash)) return;
                throw new IOException("The original path is already occupied: " + change.OriginalPath);
            }
            FileSafety.Restore(change.BackupPath, change.OriginalPath, change.BackupHash, allowLegacy);
        }

        private static void RequireReplacementBackup(PathPairChange change, bool allowLegacy)
        {
            if (FileSafety.SameFile(change.SourcePath, change.DestinationPath)) throw new IOException("Invalid undo entry: source and destination identify the same file.");
            if (!string.IsNullOrEmpty(change.ReplacedBackupPath)) FileSafety.RequireHash(change.ReplacedBackupPath, change.ReplacedHash, allowLegacy);
        }
    }
}
