using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Microsoft.Win32.SafeHandles;

namespace PictureOrganizer
{
    internal static class FileSafety
    {
        public static string Hash(string path)
        {
            using (FileStream stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (SHA256 algorithm = SHA256.Create())
                return BitConverter.ToString(algorithm.ComputeHash(stream)).Replace("-", string.Empty);
        }

        public static void RequireHash(string path, string expectedHash, bool allowLegacy = false)
        {
            if (!File.Exists(path)) throw new IOException("Required file is missing: " + path);
            if (string.IsNullOrEmpty(expectedHash))
            {
                if (!allowLegacy) throw new IOException("This older undo entry has no content fingerprint. Confirmation is required: " + path);
                return;
            }
            if (!string.Equals(Hash(path), expectedHash, StringComparison.OrdinalIgnoreCase))
                throw new IOException("The file has changed. Undo will not overwrite or delete it: " + path);
        }

        public static bool Matches(string path, string hash)
        {
            return !string.IsNullOrEmpty(hash) && File.Exists(path)
                && string.Equals(Hash(path), hash, StringComparison.OrdinalIgnoreCase);
        }

        public static string TemporaryPath(string path)
        {
            return Path.Combine(Path.GetDirectoryName(Path.GetFullPath(path)), ".photoorganizer-" + Guid.NewGuid().ToString("N") + Path.GetExtension(path));
        }

        // Never fall back to truncating/copying over the live file if replacement fails.
        public static void Publish(string preparedPath, string targetPath, bool replace)
        {
            using (FileStream stream = File.Open(preparedPath, FileMode.Open, FileAccess.ReadWrite, FileShare.Read)) stream.Flush(true);
            if (replace) File.Replace(preparedPath, targetPath, null);
            else File.Move(preparedPath, targetPath);
        }

        public static void Restore(string backupPath, string targetPath, string backupHash, bool allowLegacy = false)
        {
            RequireHash(backupPath, backupHash, allowLegacy);
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(targetPath)));
            string temp = TemporaryPath(targetPath);
            try
            {
                File.Copy(backupPath, temp, false);
                RequireHash(temp, backupHash, allowLegacy);
                DateTime modified = File.GetLastWriteTime(backupPath);
                Publish(temp, targetPath, File.Exists(targetPath));
                File.SetLastWriteTime(targetPath, modified);
            }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }

        public static void ValidateImage(string path)
        {
            using (Image image = Image.FromFile(path))
            using (Bitmap decoded = new Bitmap(image))
            {
                if (decoded.Width < 1 || decoded.Height < 1) throw new IOException("The edited image could not be validated.");
            }
        }

        public static bool SameFile(string first, string second)
        {
            if (string.Equals(Path.GetFullPath(first).TrimEnd('\\'), Path.GetFullPath(second).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase)) return true;
            if (!File.Exists(first) || !File.Exists(second)) return false;
            using (FileStream a = File.Open(first, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            using (FileStream b = File.Open(second, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            {
                FileInformation ai, bi;
                if (!GetFileInformationByHandle(a.SafeFileHandle, out ai) || !GetFileInformationByHandle(b.SafeFileHandle, out bi))
                    throw new IOException("Cannot verify whether the source and destination identify the same file.");
                return ai.VolumeSerialNumber == bi.VolumeSerialNumber && ai.FileIndexHigh == bi.FileIndexHigh && ai.FileIndexLow == bi.FileIndexLow;
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct FileInformation
        {
            public uint Attributes;
            public System.Runtime.InteropServices.ComTypes.FILETIME CreationTime, AccessTime, WriteTime;
            public uint VolumeSerialNumber, SizeHigh, SizeLow, NumberOfLinks, FileIndexHigh, FileIndexLow;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetFileInformationByHandle(SafeFileHandle handle, out FileInformation information);
    }
}
