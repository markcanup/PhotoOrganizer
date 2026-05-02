using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Runtime.Serialization;

namespace PictureOrganizer
{
    internal static class PhotoMetadataHelper
    {
        private const int ExifDateTakenId = 0x9003;
        private const int ExifDateDigitizedId = 0x9004;
        private const int ExifDateModifiedId = 0x0132;

        public static bool IsSupportedSourceFile(string filePath)
        {
            string extension = Path.GetExtension(filePath);
            return extension != null
                && (extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".png", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".gif", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".tif", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".tiff", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".heic", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".heif", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".pdf", StringComparison.OrdinalIgnoreCase));
        }

        public static bool IsPdfFile(string filePath)
        {
            return string.Equals(Path.GetExtension(filePath), ".pdf", StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsJpegFile(string filePath)
        {
            string extension = Path.GetExtension(filePath);
            return string.Equals(extension, ".jpg", StringComparison.OrdinalIgnoreCase)
                || string.Equals(extension, ".jpeg", StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsPngFile(string filePath)
        {
            return string.Equals(Path.GetExtension(filePath), ".png", StringComparison.OrdinalIgnoreCase);
        }

        public static bool SupportsShellRating(string filePath)
        {
            return IsJpegFile(filePath) || IsPngFile(filePath);
        }

        public static DateTime GetBestDate(string filePath)
        {
            DateTime? exifDate = TryReadExifDateTaken(filePath);
            if (exifDate.HasValue)
            {
                return exifDate.Value;
            }

            return File.GetLastWriteTime(filePath);
        }

        public static DateTime? TryReadExifDateTaken(string filePath)
        {
            if (IsPdfFile(filePath))
            {
                return null;
            }

            try
            {
                using (Image image = Image.FromFile(filePath))
                {
                    int[] ids = { ExifDateTakenId, ExifDateDigitizedId, ExifDateModifiedId };
                    foreach (int id in ids)
                    {
                        PropertyItem property = image.PropertyItems.FirstOrDefault(item => item.Id == id);
                        if (property == null || property.Value == null || property.Value.Length == 0)
                        {
                            continue;
                        }

                        string raw = System.Text.Encoding.ASCII.GetString(property.Value).Trim('\0', ' ');
                        DateTime parsed;
                        if (DateTime.TryParseExact(raw, "yyyy:MM:dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out parsed))
                        {
                            return parsed;
                        }
                    }
                }
            }
            catch
            {
            }

            return null;
        }

        public static void UpdateDateTakenAndModified(string filePath, DateTime newDate)
        {
            if (IsPdfFile(filePath))
            {
                throw new InvalidOperationException("Date update is not supported for PDF files.");
            }

            string extension = Path.GetExtension(filePath);
            bool exifWritable = extension != null
                && (extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase));

            if (exifWritable)
            {
                SaveWithUpdatedExif(filePath, newDate);
            }

            File.SetLastWriteTime(filePath, newDate);
        }

        public static PhotoItem CreatePhotoItem(string filePath, int thumbnailMaxSize)
        {
            bool isPdf = IsPdfFile(filePath);
            FileInfo info = new FileInfo(filePath);
            Image thumbnail = CreateThumbnail(filePath, thumbnailMaxSize);
            Size pixelSize = GetPixelSize(filePath);
            DateTime? exifDateTaken = TryReadExifDateTaken(filePath);
            int pageCount = isPdf ? PdfPhotoProcessor.GetPdfPageCount(filePath) : 0;
            int? rating = SupportsShellRating(filePath) ? ShellRatingHelper.TryReadRating(filePath) : null;
            return new PhotoItem(
                filePath,
                thumbnail,
                info.LastWriteTime,
                info.Exists ? info.Length : 0L,
                pixelSize,
                exifDateTaken,
                isPdf,
                pageCount,
                rating,
                true,
                HasDateDifference(filePath, info.LastWriteTime, exifDateTaken));
        }

        public static PhotoItem CreatePlaceholderPhotoItem(string filePath)
        {
            bool isPdf = IsPdfFile(filePath);
            FileInfo info = new FileInfo(filePath);
            return new PhotoItem(
                filePath,
                null,
                info.Exists ? info.LastWriteTime : DateTime.MinValue,
                info.Exists ? info.Length : 0L,
                Size.Empty,
                null,
                isPdf,
                0,
                null,
                false,
                false);
        }

        public static bool HasDateDifference(string filePath, DateTime lastWriteTime, DateTime? exifDateTaken)
        {
            List<DateTime> dates = new List<DateTime> { lastWriteTime.Date };
            if (exifDateTaken.HasValue)
            {
                dates.Add(exifDateTaken.Value.Date);
            }

            DateTime? fileNameDate = TryReadFilenameDateStamp(filePath);
            if (fileNameDate.HasValue)
            {
                dates.Add(fileNameDate.Value.Date);
            }

            return dates.Distinct().Count() > 1;
        }

        public static DateTime? TryReadFilenameDateStamp(string filePath)
        {
            string name = Path.GetFileNameWithoutExtension(filePath) ?? string.Empty;
            Match compact = Regex.Match(name, "^([0-9]{4})([0-9]{2})([0-9]{2})-");
            if (compact.Success)
            {
                return TryBuildDate(compact.Groups[1].Value, compact.Groups[2].Value, compact.Groups[3].Value);
            }

            Match dashed = Regex.Match(name, "^([0-9]{4})-([0-9]{2})-([0-9]{2})");
            if (dashed.Success)
            {
                return TryBuildDate(dashed.Groups[1].Value, dashed.Groups[2].Value, dashed.Groups[3].Value);
            }

            return null;
        }

        public static Image CreateThumbnail(string filePath, int maxSize)
        {
            if (IsPdfFile(filePath))
            {
                using (Bitmap rendered = PdfPhotoProcessor.RenderPdfPageToBitmap(filePath))
                {
                    return ResizeImage(rendered, maxSize, maxSize);
                }
            }

            using (Image image = Image.FromFile(filePath))
            {
                return ResizeImage(image, maxSize, maxSize);
            }
        }

        public static Size GetPixelSize(string filePath)
        {
            if (IsPdfFile(filePath))
            {
                using (Bitmap rendered = PdfPhotoProcessor.RenderPdfPageToBitmap(filePath))
                {
                    return rendered.Size;
                }
            }

            using (Image image = Image.FromFile(filePath))
            {
                return image.Size;
            }
        }

        public static Image LoadPreviewImage(string filePath)
        {
            if (IsPdfFile(filePath))
            {
                return PdfPhotoProcessor.RenderPdfPageToBitmap(filePath);
            }

            using (Image image = Image.FromFile(filePath))
            {
                return new Bitmap(image);
            }
        }

        public static ImageFormat GetTargetFormat(string extension)
        {
            if (string.Equals(extension, ".png", StringComparison.OrdinalIgnoreCase))
            {
                return ImageFormat.Png;
            }

            if (string.Equals(extension, ".tif", StringComparison.OrdinalIgnoreCase)
                || string.Equals(extension, ".tiff", StringComparison.OrdinalIgnoreCase))
            {
                return ImageFormat.Tiff;
            }

            if (string.Equals(extension, ".gif", StringComparison.OrdinalIgnoreCase))
            {
                return ImageFormat.Gif;
            }

            return ImageFormat.Jpeg;
        }

        private static void SaveWithUpdatedExif(string filePath, DateTime newDate)
        {
            string tempPath = Path.Combine(Path.GetDirectoryName(filePath), Guid.NewGuid().ToString("N") + Path.GetExtension(filePath));
            string exifValue = newDate.ToString("yyyy:MM:dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture) + '\0';

            using (Bitmap bitmap = new Bitmap(filePath))
            {
                SetProperty(bitmap, ExifDateTakenId, exifValue);
                SetProperty(bitmap, ExifDateDigitizedId, exifValue);
                SetProperty(bitmap, ExifDateModifiedId, exifValue);
                PdfPhotoProcessor.SaveBitmap(bitmap, tempPath, GetTargetFormat(Path.GetExtension(filePath)));
            }

            File.Copy(tempPath, filePath, true);
            File.Delete(tempPath);
        }

        private static void SetProperty(Image image, int id, string value)
        {
            PropertyItem property = CreatePropertyItem();
            property.Id = id;
            property.Type = 2;
            property.Value = System.Text.Encoding.ASCII.GetBytes(value);
            property.Len = property.Value.Length;
            image.SetPropertyItem(property);
        }

        private static PropertyItem CreatePropertyItem()
        {
            return (PropertyItem)FormatterServices.GetUninitializedObject(typeof(PropertyItem));
        }

        private static Image ResizeImage(Image image, int maxWidth, int maxHeight)
        {
            Size scaled = GetScaledSize(image.Size, maxWidth, maxHeight);
            Bitmap thumbnail = new Bitmap(Math.Max(1, scaled.Width), Math.Max(1, scaled.Height));
            using (Graphics graphics = Graphics.FromImage(thumbnail))
            {
                graphics.Clear(Color.White);
                graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                graphics.DrawImage(image, new Rectangle(Point.Empty, thumbnail.Size));
            }

            return thumbnail;
        }

        private static Size GetScaledSize(Size source, int maxWidth, int maxHeight)
        {
            double scale = Math.Min((double)maxWidth / Math.Max(1, source.Width), (double)maxHeight / Math.Max(1, source.Height));
            scale = Math.Min(1.0, scale);
            return new Size(
                Math.Max(1, (int)Math.Round(source.Width * scale)),
                Math.Max(1, (int)Math.Round(source.Height * scale)));
        }

        private static DateTime? TryBuildDate(string year, string month, string day)
        {
            DateTime parsed;
            if (DateTime.TryParseExact(year + "-" + month + "-" + day, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out parsed))
            {
                return parsed.Date;
            }

            return null;
        }
    }
}
