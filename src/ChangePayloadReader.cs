using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace PictureOrganizer
{
    internal static class ChangePayloadReader
    {
        // The serializer accepts unrelated objects with empty properties; validate shape before accepting a single-file payload.
        private static List<T> Read<T, TWrapper>(string json, Func<T, bool> valid, Func<TWrapper, List<T>> unwrap)
        {
            T single;
            if (ChangeLogStore.TryDeserialize<T>(json, out single) && single != null && valid(single)) return new List<T> { single };
            TWrapper wrapper;
            if (ChangeLogStore.TryDeserialize<TWrapper>(json, out wrapper) && wrapper != null)
            {
                List<T> items = unwrap(wrapper);
                if (items != null && items.Count > 0 && items.All(item => item != null && valid(item))) return items;
            }
            throw new InvalidDataException("The undo payload is empty or invalid. Its entry has been retained.");
        }

        public static List<PathPairChange> Paths(string json)
        {
            return Read<PathPairChange, CopyMoveChangePayload>(json, item => !string.IsNullOrEmpty(item.SourcePath) && !string.IsNullOrEmpty(item.DestinationPath), wrapper => wrapper.Paths);
        }
        public static List<DateUpdateFileChange> Dates(string json)
        {
            return Read<DateUpdateFileChange, DateUpdateChangePayload>(json, item => !string.IsNullOrEmpty(item.FilePath), wrapper => wrapper.Files);
        }
        public static List<RotateBackupChange> Edits(string json)
        {
            return Read<RotateBackupChange, RotateChangePayload>(json, item => !string.IsNullOrEmpty(item.FilePath) && !string.IsNullOrEmpty(item.BackupPath), wrapper => wrapper.Files);
        }
        public static List<DeleteBackupChange> Deletes(string json)
        {
            return Read<DeleteBackupChange, DeleteChangePayload>(json, item => !string.IsNullOrEmpty(item.OriginalPath) && !string.IsNullOrEmpty(item.BackupPath), wrapper => wrapper.Files);
        }
        public static List<RatingFileChange> Ratings(string json)
        {
            return Read<RatingFileChange, RatingChangePayload>(json, item => !string.IsNullOrEmpty(item.FilePath), wrapper => wrapper.Files);
        }
        public static List<ConvertFileChange> Conversions(string json)
        {
            return Read<ConvertFileChange, ConvertChangePayload>(json, item => !string.IsNullOrEmpty(item.CreatedPath),
                wrapper => wrapper.CreatedPaths == null ? null : wrapper.CreatedPaths.Select(path => new ConvertFileChange { CreatedPath = path }).ToList());
        }

        public static bool NeedsLegacyConfirmation(ChangeLogEntry entry)
        {
            switch (entry.Kind)
            {
                case ChangeLogKind.Copy:
                case ChangeLogKind.Move:
                case ChangeLogKind.Rename: return Paths(entry.PayloadJson).Any(item => string.IsNullOrEmpty(item.DestinationHash));
                case ChangeLogKind.Convert: return Conversions(entry.PayloadJson).Any(item => string.IsNullOrEmpty(item.CreatedHash));
                case ChangeLogKind.Rotate:
                case ChangeLogKind.Autocrop: return Edits(entry.PayloadJson).Any(item => string.IsNullOrEmpty(item.EditedHash) || string.IsNullOrEmpty(item.BackupHash));
                case ChangeLogKind.Delete: return Deletes(entry.PayloadJson).Any(item => string.IsNullOrEmpty(item.BackupHash));
                case ChangeLogKind.DateUpdate: return Dates(entry.PayloadJson).Any(item => string.IsNullOrEmpty(item.BackupPath));
                default: return false;
            }
        }
    }
}
