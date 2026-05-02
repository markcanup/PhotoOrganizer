using System;
using System.Drawing;
using System.IO;

namespace PictureOrganizer
{
    internal sealed class PhotoItem : IDisposable
    {
        public string FilePath { get; private set; }
        public string DisplayName { get; private set; }
        public string FolderPath { get; private set; }
        public string FileExtension { get; private set; }
        public Image Thumbnail { get; private set; }
        public DateTime LastWriteTime { get; private set; }
        public long FileSizeBytes { get; private set; }
        public Size PixelSize { get; private set; }
        public DateTime? ExifDateTaken { get; private set; }
        public bool IsPdf { get; private set; }
        public int PageCount { get; private set; }
        public int? Rating { get; private set; }
        public bool MetadataLoaded { get; private set; }
        public bool HasDateDifference { get; private set; }

        public PhotoItem(
            string filePath,
            Image thumbnail,
            DateTime lastWriteTime,
            long fileSizeBytes,
            Size pixelSize,
            DateTime? exifDateTaken,
            bool isPdf,
            int pageCount,
            int? rating,
            bool metadataLoaded,
            bool hasDateDifference)
        {
            FilePath = filePath;
            DisplayName = Path.GetFileName(filePath);
            FolderPath = Path.GetDirectoryName(filePath) ?? string.Empty;
            FileExtension = (Path.GetExtension(filePath) ?? string.Empty).TrimStart('.').ToUpperInvariant();
            Thumbnail = thumbnail;
            LastWriteTime = lastWriteTime;
            FileSizeBytes = fileSizeBytes;
            PixelSize = pixelSize;
            ExifDateTaken = exifDateTaken;
            IsPdf = isPdf;
            PageCount = pageCount;
            Rating = rating;
            MetadataLoaded = metadataLoaded;
            HasDateDifference = hasDateDifference;
        }

        public void Dispose()
        {
            if (Thumbnail != null)
            {
                Thumbnail.Dispose();
                Thumbnail = null;
            }
        }

        public static int CompareByName(PhotoItem first, PhotoItem second)
        {
            return string.Compare(first.FilePath, second.FilePath, StringComparison.OrdinalIgnoreCase);
        }
    }
}
