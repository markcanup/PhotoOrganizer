using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace PictureOrganizer
{
    internal static class ImageFileOperations
    {
        public static FileTransferResult CopyFiles(IEnumerable<string> paths, string folder, Func<string, string, ConflictResolutionChoice> resolver, FileOperationJournal journal)
        {
            return TransferFiles(paths, folder, false, resolver, journal);
        }

        public static FileTransferResult MoveFiles(IEnumerable<string> paths, string folder, Func<string, string, ConflictResolutionChoice> resolver, FileOperationJournal journal)
        {
            return TransferFiles(paths, folder, true, resolver, journal);
        }

        public static string RenameFile(string path, string name, FileOperationJournal journal)
        {
            string safeName = SanitizeBaseName(name);
            if (safeName.Length == 0) throw new InvalidOperationException("The new file name cannot be empty.");
            string requested = Path.Combine(Path.GetDirectoryName(path), safeName + Path.GetExtension(path));
            if (string.Equals(Path.GetFullPath(path), Path.GetFullPath(requested), StringComparison.OrdinalIgnoreCase)) return path;
            return RenameTo(path, EnsureUniquePath(Path.GetDirectoryName(path), Path.GetFileName(requested)), journal).DestinationPath;
        }

        private static PathPairChange RenameTo(string path, string target, FileOperationJournal journal)
        {
            PathPairChange change = new PathPairChange { SourcePath = path, DestinationPath = target, DestinationHash = FileSafety.Hash(path) };
            journal.Execute(Guid.NewGuid().ToString("N"), ChangeLogKind.Rename, "Renamed " + Path.GetFileName(path) + " -> " + Path.GetFileName(target), change,
                delegate { FileSafety.RequireHash(path, change.DestinationHash); File.Move(path, target); },
                delegate
                {
                    if (File.Exists(path)) FileSafety.RequireHash(path, change.DestinationHash);
                    else { FileSafety.RequireHash(target, change.DestinationHash); File.Move(target, path); }
                });
            return change;
        }

        public static FileBatchResult<PathPairChange> ApplyRenameRule(IEnumerable<string> paths, RenameRule rule, FileOperationJournal journal)
        {
            FileBatchResult<PathPairChange> result = new FileBatchResult<PathPairChange>();
            foreach (string path in paths.OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
            {
                try
                {
                    string name = BuildName(Path.GetFileNameWithoutExtension(path), path, rule) + Path.GetExtension(path);
                    if (string.Equals(Path.GetFileName(path), name, StringComparison.OrdinalIgnoreCase)) continue;
                    result.Completed.Add(RenameTo(path, EnsureUniquePath(Path.GetDirectoryName(path), name), journal));
                }
                catch (Exception ex) { result.Failures.Add(path + ": " + ex.Message); }
            }
            return result;
        }

        public static FileBatchResult<ConvertFileChange> ConvertFiles(IEnumerable<string> paths, ImageFormat format, FileOperationJournal journal)
        {
            return ConvertFiles(paths, format, false, journal);
        }

        public static FileBatchResult<ConvertFileChange> ConvertToPdf(IEnumerable<string> paths, FileOperationJournal journal)
        {
            return ConvertFiles(paths, null, true, journal);
        }

        private static FileBatchResult<ConvertFileChange> ConvertFiles(IEnumerable<string> paths, ImageFormat format, bool pdf, FileOperationJournal journal)
        {
            FileBatchResult<ConvertFileChange> result = new FileBatchResult<ConvertFileChange>();
            foreach (string path in paths)
            {
                string temp = FileSafety.TemporaryPath(path);
                try
                {
                    if (PhotoMetadataHelper.IsPdfFile(path)) throw new InvalidOperationException("Choose a photo to convert, rather than an existing PDF.");
                    string extension = pdf ? ".pdf" : format.Guid == ImageFormat.Png.Guid ? ".png" : ".jpg";
                    string target = EnsureUniquePath(Path.GetDirectoryName(path), Path.GetFileNameWithoutExtension(path) + extension);
                    temp = FileSafety.TemporaryPath(target);
                    using (Bitmap bitmap = pdf ? PhotoMetadataHelper.LoadBitmapForPdf(path) : PhotoMetadataHelper.LoadBitmap(path))
                    {
                        if (pdf) PhotoPdfWriter.Save(bitmap, temp);
                        else PdfPhotoProcessor.SaveBitmap(bitmap, temp, format);
                    }
                    PreserveRatingAndTime(path, temp, File.GetLastWriteTime(path));
                    if (pdf)
                    {
                        if (PdfPhotoProcessor.GetPdfPageCount(temp) != 1) throw new InvalidDataException("The converted PDF did not contain one page.");
                        using (Bitmap preview = PdfPhotoProcessor.RenderPdfPageToBitmap(temp, 360)) { }
                    }
                    else FileSafety.ValidateImage(temp);
                    ConvertFileChange change = new ConvertFileChange { SourcePath = path, CreatedPath = target, CreatedHash = FileSafety.Hash(temp) };
                    journal.Execute(Guid.NewGuid().ToString("N"), ChangeLogKind.Convert, "Converted " + Path.GetFileName(path) + " -> " + Path.GetFileName(target), change,
                        delegate { FileSafety.Publish(temp, target, false); },
                        delegate { if (!File.Exists(temp)) { FileSafety.RequireHash(target, change.CreatedHash); File.Delete(target); } });
                    result.Completed.Add(change);
                }
                catch (Exception ex) { result.Failures.Add(path + ": " + ex.Message); }
                finally { if (File.Exists(temp)) File.Delete(temp); }
            }
            return result;
        }

        public static PreparedImageEdit PrepareAutoCrop(string path)
        {
            RequireEditableImage(path);
            return PrepareImage(path, delegate(string temp)
            {
                using (Bitmap original = PhotoMetadataHelper.LoadBitmap(path))
                using (Bitmap cropped = PdfPhotoProcessor.AutoCropBitmap(original))
                    PdfPhotoProcessor.SaveBitmap(cropped, temp, PhotoMetadataHelper.GetTargetFormat(Path.GetExtension(path)));
            }, File.GetLastWriteTime(path));
        }

        public static void ApplyAutoCrop(PreparedImageEdit edit, FileOperationJournal journal)
        {
            CommitImageEdit(edit, ChangeLogKind.Autocrop, journal);
        }

        public static void RotateFile(string path, RotateFlipType rotation, FileOperationJournal journal)
        {
            RequireEditableImage(path);
            using (PreparedImageEdit edit = PrepareImage(path, delegate(string temp)
            {
                using (Bitmap bitmap = PhotoMetadataHelper.LoadBitmap(path))
                {
                    bitmap.RotateFlip(rotation);
                    PdfPhotoProcessor.SaveBitmap(bitmap, temp, PhotoMetadataHelper.GetTargetFormat(Path.GetExtension(path)));
                }
            }, File.GetLastWriteTime(path))) CommitImageEdit(edit, ChangeLogKind.Rotate, journal);
        }

        public static void UpdateDate(string path, DateTime date, FileOperationJournal journal)
        {
            if (PhotoMetadataHelper.IsPdfFile(path)) throw new InvalidOperationException("Date update is not supported for PDF files.");
            string temp = FileSafety.TemporaryPath(path);
            string before = FileSafety.Hash(path);
            try
            {
                File.Copy(path, temp, false);
                PhotoMetadataHelper.UpdateDateTakenAndModified(temp, date);
                // The date operation changes bytes only for JPEG; HEIF and other types change the timestamp.
                string id = Guid.NewGuid().ToString("N");
                string backup = journal.Backup(id, path);
                FileSafety.RequireHash(backup, before);
                DateUpdateFileChange change = new DateUpdateFileChange { FilePath = path, OldDate = File.GetLastWriteTime(path), NewDate = date,
                    BackupPath = backup, BackupHash = before, EditedHash = FileSafety.Hash(temp) };
                journal.Execute(id, ChangeLogKind.DateUpdate, "Updated date for " + Path.GetFileName(path), change,
                    delegate { FileSafety.RequireHash(path, before); FileSafety.Publish(temp, path, true); File.SetLastWriteTime(path, date); },
                    delegate { RecoverImage(path, temp, backup, before, change.EditedHash); });
            }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }

        public static void DeleteFile(string path, FileOperationJournal journal)
        {
            string id = Guid.NewGuid().ToString("N");
            string backup = journal.Backup(id, path);
            DeleteBackupChange change = new DeleteBackupChange { OriginalPath = path, BackupPath = backup, BackupHash = FileSafety.Hash(backup) };
            journal.Execute(id, ChangeLogKind.Delete, "Deleted " + Path.GetFileName(path), change,
                delegate
                {
                    FileSafety.RequireHash(path, change.BackupHash);
                    if (!RecycleBinHelper.TrySendFileToRecycleBin(path)) File.Delete(path);
                    if (File.Exists(path)) throw new IOException("The file was not deleted: " + path);
                },
                delegate
                {
                    if (File.Exists(path)) FileSafety.RequireHash(path, change.BackupHash);
                    else FileSafety.Restore(backup, path, change.BackupHash);
                });
        }

        private static PreparedImageEdit PrepareImage(string path, Action<string> write, DateTime modified)
        {
            PreparedImageEdit edit = new PreparedImageEdit(path);
            try
            {
                write(edit.PreparedPath);
                PreserveRatingAndTime(path, edit.PreparedPath, modified);
                FileSafety.ValidateImage(edit.PreparedPath);
                FileSafety.RequireHash(path, edit.OriginalHash);
                edit.EditedHash = FileSafety.Hash(edit.PreparedPath);
                return edit;
            }
            catch { edit.Dispose(); throw; }
        }

        private static void CommitImageEdit(PreparedImageEdit edit, ChangeLogKind kind, FileOperationJournal journal)
        {
            FileSafety.RequireHash(edit.SourcePath, edit.OriginalHash);
            FileSafety.RequireHash(edit.PreparedPath, edit.EditedHash);
            string id = Guid.NewGuid().ToString("N");
            string backup = journal.Backup(id, edit.SourcePath);
            FileSafety.RequireHash(backup, edit.OriginalHash);
            RotateBackupChange change = new RotateBackupChange { FilePath = edit.SourcePath, BackupPath = backup,
                BackupHash = edit.OriginalHash, EditedHash = edit.EditedHash };
            journal.Execute(id, kind, kind + " " + Path.GetFileName(edit.SourcePath), change,
                delegate { FileSafety.RequireHash(edit.SourcePath, edit.OriginalHash); FileSafety.Publish(edit.PreparedPath, edit.SourcePath, true); },
                delegate { RecoverImage(edit.SourcePath, edit.PreparedPath, backup, edit.OriginalHash, edit.EditedHash); });
        }

        private static void RecoverImage(string path, string temp, string backup, string before, string after)
        {
            if (File.Exists(temp)) FileSafety.RequireHash(path, before);
            else { FileSafety.RequireHash(path, after); FileSafety.Restore(backup, path, before); }
        }

        private static void PreserveRatingAndTime(string source, string target, DateTime modified)
        {
            int? rating = PhotoMetadataHelper.SupportsShellRating(source) ? ShellRatingHelper.TryReadRating(source) : null;
            if (rating.HasValue && PhotoMetadataHelper.SupportsShellRating(target)) ShellRatingHelper.WriteRating(target, rating);
            File.SetLastWriteTime(target, modified);
        }

        private static void RequireEditableImage(string path)
        {
            if (PhotoMetadataHelper.IsHeifFile(path) || PhotoMetadataHelper.IsPdfFile(path))
                throw new InvalidOperationException("Rotate and autocrop are not supported for HEIC/HEIF or PDF files.");
        }

        public static void OpenExternalEditor(string path)
        {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }

        private static FileTransferResult TransferFiles(IEnumerable<string> paths, string folder, bool move, Func<string, string, ConflictResolutionChoice> resolver, FileOperationJournal journal)
        {
            Directory.CreateDirectory(folder);
            FileTransferResult result = new FileTransferResult();
            ConflictResolutionChoice allChoice = null;
            foreach (string path in paths)
            {
                string temp = null;
                try
                {
                    string target = Path.Combine(folder, Path.GetFileName(path));
                    if (FileSafety.SameFile(path, target)) throw new IOException("Source and destination identify the same file. No transfer was made.");
                    bool replace = File.Exists(target);
                    if (replace)
                    {
                        ConflictResolutionChoice choice = allChoice ?? (resolver == null ? new ConflictResolutionChoice { Resolution = ConflictResolutionOption.Rename } : resolver(path, target));
                        if (choice == null || choice.FollowUp == ConflictFollowUpOption.CancelOperation) { result.Cancelled = true; break; }
                        if (choice.FollowUp == ConflictFollowUpOption.ApplyToAll) allChoice = choice;
                        if (choice.Resolution == ConflictResolutionOption.Skip) { result.SkippedPaths.Add(path); continue; }
                        if (choice.Resolution == ConflictResolutionOption.Rename) { target = EnsureUniquePath(folder, Path.GetFileName(path)); replace = false; }
                    }
                    string id = Guid.NewGuid().ToString("N");
                    PathPairChange change = new PathPairChange { SourcePath = path, DestinationPath = target, DestinationHash = FileSafety.Hash(path) };
                    if (replace)
                    {
                        change.ReplacedBackupPath = journal.Backup(id, target);
                        change.ReplacedHash = FileSafety.Hash(change.ReplacedBackupPath);
                    }
                    temp = FileSafety.TemporaryPath(target);
                    File.Copy(path, temp, false);
                    FileSafety.RequireHash(temp, change.DestinationHash);
                    journal.Execute(id, move ? ChangeLogKind.Move : ChangeLogKind.Copy,
                        (move ? "Moved " : "Copied ") + Path.GetFileName(path) + " -> " + Path.GetFileName(target), change,
                        delegate
                        {
                            FileSafety.RequireHash(path, change.DestinationHash);
                            if (replace) FileSafety.RequireHash(target, change.ReplacedHash);
                            FileSafety.Publish(temp, target, replace);
                            if (move) { FileSafety.RequireHash(path, change.DestinationHash); File.Delete(path); }
                        },
                        delegate
                        {
                            if (File.Exists(temp))
                            {
                                FileSafety.RequireHash(path, change.DestinationHash);
                                if (replace) FileSafety.RequireHash(target, change.ReplacedHash);
                            }
                            else
                            {
                                FileSafety.RequireHash(target, change.DestinationHash);
                                if (move && !File.Exists(path)) FileSafety.Restore(target, path, change.DestinationHash);
                                if (move) FileSafety.RequireHash(path, change.DestinationHash);
                                if (replace) FileSafety.Restore(change.ReplacedBackupPath, target, change.ReplacedHash);
                                else File.Delete(target);
                            }
                        });
                    result.CompletedTransfers.Add(new FileTransferPair { SourcePath = path, DestinationPath = target });
                    result.DestinationPathsWritten.Add(target);
                    if (move) result.SourcePathsRemoved.Add(path);
                }
                catch (Exception ex) { result.Failures.Add(path + ": " + ex.Message); }
                finally { if (temp != null && File.Exists(temp)) File.Delete(temp); }
            }
            return result;
        }
        private static string BuildName(string currentBaseName, string filePath, RenameRule rule)
        {
            string value1 = ExpandMacros(rule.Value1 ?? string.Empty, currentBaseName, filePath);
            string value2 = ExpandMacros(rule.Value2 ?? string.Empty, currentBaseName, filePath);
            string result;
            switch (rule.RuleType)
            {
                case RenameRuleType.AddTextToStart:
                    result = value1 + currentBaseName;
                    break;
                case RenameRuleType.AddTextToEnd:
                    result = currentBaseName + value1;
                    break;
                case RenameRuleType.RemoveText:
                    result = currentBaseName.Replace(value1, string.Empty);
                    break;
                case RenameRuleType.SubstituteText:
                    result = currentBaseName.Replace(value1, value2);
                    break;
                case RenameRuleType.ReplaceFullFilename:
                    result = value1;
                    break;
                default:
                    result = currentBaseName;
                    break;
            }
            result = SanitizeBaseName(result);
            if (result.Length == 0) throw new InvalidOperationException("Rename rule produced an empty file name.");
            return result;
        }

        private static string ExpandMacros(string text, string currentBaseName, string filePath)
        {
            DateTime modified = File.GetLastWriteTime(filePath);
            string replaced = text.Replace("%%", "\u0001");
            replaced = Regex.Replace(replaced, "%dateh%", modified.ToString("yyyy-MM-dd"), RegexOptions.IgnoreCase);
            replaced = Regex.Replace(replaced, "%date%", modified.ToString("yyyyMMdd"), RegexOptions.IgnoreCase);
            replaced = Regex.Replace(replaced, "%time%", modified.ToString("HH-mm-ss"), RegexOptions.IgnoreCase);
            replaced = Regex.Replace(replaced, "%char(\\d+)%", match =>
            {
                int count;
                if (!int.TryParse(match.Groups[1].Value, out count)) return string.Empty;
                count = Math.Max(0, Math.Min(count, currentBaseName.Length));
                return currentBaseName.Substring(0, count);
            }, RegexOptions.IgnoreCase);
            return replaced.Replace("\u0001", "%");
        }

        private static string SanitizeBaseName(string baseName)
        {
            string result = (baseName ?? string.Empty).Trim();
            foreach (char value in Path.GetInvalidFileNameChars()) result = result.Replace(value.ToString(), string.Empty);
            return result.Trim();
        }

        private static string EnsureUniquePath(string directory, string fileName)
        {
            string candidate = Path.Combine(directory, fileName);
            if (!File.Exists(candidate)) return candidate;
            string baseName = Path.GetFileNameWithoutExtension(fileName);
            string extension = Path.GetExtension(fileName);
            int suffix = 1;
            while (true)
            {
                string numbered = Path.Combine(directory, baseName + " (" + suffix + ")" + extension);
                if (!File.Exists(numbered)) return numbered;
                suffix++;
            }
        }

    }

    internal sealed class FileTransferResult
    {
        public List<string> DestinationPathsWritten { get; private set; }
        public List<string> SourcePathsRemoved { get; private set; }
        public List<string> SkippedPaths { get; private set; }
        public List<FileTransferPair> CompletedTransfers { get; private set; }
        public List<string> Failures { get; private set; }
        public bool Cancelled { get; set; }

        public FileTransferResult()
        {
            DestinationPathsWritten = new List<string>();
            SourcePathsRemoved = new List<string>();
            SkippedPaths = new List<string>();
            CompletedTransfers = new List<FileTransferPair>();
            Failures = new List<string>();
        }
    }

    internal sealed class FileTransferPair
    {
        public string SourcePath { get; set; }
        public string DestinationPath { get; set; }
    }
}

