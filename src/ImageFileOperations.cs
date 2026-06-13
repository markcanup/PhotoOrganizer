using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace PictureOrganizer
{
    internal static class ImageFileOperations
    {
        public static FileTransferResult CopyFiles(IEnumerable<string> filePaths, string destinationFolder, Func<string, string, ConflictResolutionChoice> conflictResolver)
        {
            return TransferFiles(filePaths, destinationFolder, false, conflictResolver);
        }

        public static FileTransferResult MoveFiles(IEnumerable<string> filePaths, string destinationFolder, Func<string, string, ConflictResolutionChoice> conflictResolver)
        {
            return TransferFiles(filePaths, destinationFolder, true, conflictResolver);
        }

        public static string RenameFile(string filePath, string newBaseName)
        {
            string extension = Path.GetExtension(filePath);
            string safeBaseName = SanitizeBaseName(newBaseName);
            if (safeBaseName.Length == 0) throw new InvalidOperationException("The new file name cannot be empty.");
            string renamedPath = EnsureUniquePath(Path.GetDirectoryName(filePath), safeBaseName + extension);
            File.Move(filePath, renamedPath);
            return renamedPath;
        }

        public static List<string> ApplyRenameRule(IEnumerable<string> filePaths, RenameRule rule)
        {
            List<string> ordered = filePaths.OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToList();
            List<string> renamed = new List<string>();
            foreach (string path in ordered)
            {
                string currentBaseName = Path.GetFileNameWithoutExtension(path);
                string newBaseName = BuildName(currentBaseName, path, rule);
                string newPath = EnsureUniquePath(Path.GetDirectoryName(path), newBaseName + Path.GetExtension(path));
                File.Move(path, newPath);
                renamed.Add(newPath);
            }
            return renamed;
        }

        public static List<string> ConvertFiles(IEnumerable<string> filePaths, ImageFormat targetFormat)
        {
            List<string> createdPaths = new List<string>();
            foreach (string filePath in filePaths)
            {
                DateTime originalLastWriteTime = File.GetLastWriteTime(filePath);
                int? originalRating = PhotoMetadataHelper.SupportsShellRating(filePath) ? ShellRatingHelper.TryReadRating(filePath) : null;
                using (Bitmap bitmap = PhotoMetadataHelper.LoadBitmap(filePath))
                {
                    string extension = targetFormat.Guid == ImageFormat.Png.Guid ? ".png" : ".jpg";
                    string newPath = EnsureUniquePath(Path.GetDirectoryName(filePath), Path.GetFileNameWithoutExtension(filePath) + extension);
                    PdfPhotoProcessor.SaveBitmap(bitmap, newPath, targetFormat);
                    File.SetLastWriteTime(newPath, originalLastWriteTime);
                    if (originalRating.HasValue && PhotoMetadataHelper.SupportsShellRating(newPath))
                    {
                        ShellRatingHelper.WriteRating(newPath, originalRating);
                    }
                    createdPaths.Add(newPath);
                }
            }
            return createdPaths;
        }

        public static void AutoCropFiles(IEnumerable<string> filePaths)
        {
            foreach (string filePath in filePaths)
            {
                if (PhotoMetadataHelper.IsHeifFile(filePath))
                {
                    throw new InvalidOperationException("Autocrop is not currently supported for HEIC/HEIF files.");
                }

                using (Bitmap bitmap = PhotoMetadataHelper.LoadBitmap(filePath))
                using (Bitmap cropped = PdfPhotoProcessor.AutoCropBitmap(bitmap))
                {
                    string tempPath = Path.Combine(Path.GetDirectoryName(filePath), Guid.NewGuid().ToString("N") + Path.GetExtension(filePath));
                    PdfPhotoProcessor.SaveBitmap(cropped, tempPath, PhotoMetadataHelper.GetTargetFormat(Path.GetExtension(filePath)));
                    File.Copy(tempPath, filePath, true);
                    File.Delete(tempPath);
                }
            }
        }

        public static void DeleteFiles(IEnumerable<string> filePaths)
        {
            foreach (string filePath in filePaths) File.Delete(filePath);
        }

        public static void RotateFiles(IEnumerable<string> filePaths, RotateFlipType rotateFlipType)
        {
            foreach (string filePath in filePaths)
            {
                if (PhotoMetadataHelper.IsHeifFile(filePath))
                {
                    throw new InvalidOperationException("Rotate is not currently supported for HEIC/HEIF files.");
                }

                DateTime originalLastWriteTime = File.GetLastWriteTime(filePath);
                int? originalRating = PhotoMetadataHelper.SupportsShellRating(filePath) ? ShellRatingHelper.TryReadRating(filePath) : null;
                string tempOriginalPath = Path.Combine(Path.GetDirectoryName(filePath), Guid.NewGuid().ToString("N") + Path.GetExtension(filePath));
                File.Move(filePath, tempOriginalPath);
                using (Image image = PhotoMetadataHelper.LoadBitmap(tempOriginalPath))
                {
                    image.RotateFlip(rotateFlipType);
                    SaveImageWithOriginalFormat(image, filePath, tempOriginalPath);
                }

                if (originalRating.HasValue && PhotoMetadataHelper.SupportsShellRating(filePath))
                {
                    ShellRatingHelper.WriteRating(filePath, originalRating);
                }

                File.SetLastWriteTime(filePath, originalLastWriteTime);
                if (!RecycleBinHelper.TrySendFileToRecycleBin(tempOriginalPath) && File.Exists(tempOriginalPath))
                {
                    File.Delete(tempOriginalPath);
                }
            }
        }

        public static void OpenExternalEditor(string filePath)
        {
            Process.Start(new ProcessStartInfo(filePath) { UseShellExecute = true });
        }

        private static FileTransferResult TransferFiles(IEnumerable<string> filePaths, string destinationFolder, bool moveFiles, Func<string, string, ConflictResolutionChoice> conflictResolver)
        {
            Directory.CreateDirectory(destinationFolder);
            FileTransferResult result = new FileTransferResult();
            ConflictResolutionChoice applyToAllChoice = null;
            foreach (string filePath in filePaths)
            {
                string fileName = Path.GetFileName(filePath);
                string candidatePath = Path.Combine(destinationFolder, fileName);
                string targetPath = candidatePath;
                if (File.Exists(candidatePath))
                {
                    ConflictResolutionChoice choice = applyToAllChoice ?? (conflictResolver == null ? new ConflictResolutionChoice { Resolution = ConflictResolutionOption.Rename, FollowUp = ConflictFollowUpOption.AskEach } : conflictResolver(filePath, candidatePath));
                    if (choice == null || choice.FollowUp == ConflictFollowUpOption.CancelOperation) { result.Cancelled = true; break; }
                    if (choice.FollowUp == ConflictFollowUpOption.ApplyToAll) applyToAllChoice = choice;
                    if (choice.Resolution == ConflictResolutionOption.Skip) { result.SkippedPaths.Add(filePath); continue; }
                    if (choice.Resolution == ConflictResolutionOption.Rename) targetPath = EnsureUniquePath(destinationFolder, fileName);
                }
                if (moveFiles)
                {
                    if (File.Exists(targetPath)) File.Delete(targetPath);
                    File.Move(filePath, targetPath);
                    result.SourcePathsRemoved.Add(filePath);
                }
                else
                {
                    File.Copy(filePath, targetPath, true);
                }
                result.DestinationPathsWritten.Add(targetPath);
                result.CompletedTransfers.Add(new FileTransferPair
                {
                    SourcePath = filePath,
                    DestinationPath = targetPath
                });
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

        private static void SaveImageWithOriginalFormat(Image image, string outputPath, string sourcePath)
        {
            ImageFormat format = PhotoMetadataHelper.GetTargetFormat(Path.GetExtension(sourcePath));
            using (Bitmap bitmap = new Bitmap(image)) PdfPhotoProcessor.SaveBitmap(bitmap, outputPath, format);
        }
    }

    internal sealed class FileTransferResult
    {
        public List<string> DestinationPathsWritten { get; private set; }
        public List<string> SourcePathsRemoved { get; private set; }
        public List<string> SkippedPaths { get; private set; }
        public List<FileTransferPair> CompletedTransfers { get; private set; }
        public bool Cancelled { get; set; }

        public FileTransferResult()
        {
            DestinationPathsWritten = new List<string>();
            SourcePathsRemoved = new List<string>();
            SkippedPaths = new List<string>();
            CompletedTransfers = new List<FileTransferPair>();
        }
    }

    internal sealed class FileTransferPair
    {
        public string SourcePath { get; set; }
        public string DestinationPath { get; set; }
    }
}
