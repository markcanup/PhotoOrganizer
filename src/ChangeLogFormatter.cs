using System;
using System.IO;

namespace PictureOrganizer
{
    internal static class ChangeLogFormatter
    {
        public static string ToDisplayText(ChangeLogEntry entry)
        {
            return entry.TimestampUtc.ToLocalTime().ToString("yyyy-MM-dd hh:mm:ss tt") + "  " + GetDetail(entry);
        }

        private static string GetDetail(ChangeLogEntry entry)
        {
            switch (entry.Kind)
            {
                case ChangeLogKind.Copy:
                case ChangeLogKind.Move:
                case ChangeLogKind.Rename:
                    PathPairChange pathChange;
                    if (ChangeLogStore.TryDeserialize<PathPairChange>(entry.PayloadJson, out pathChange))
                    {
                        return entry.Kind + "  " + Path.GetFileName(pathChange.SourcePath) + " -> " + Path.GetFileName(pathChange.DestinationPath);
                    }

                    CopyMoveChangePayload copyMovePayload;
                    if (ChangeLogStore.TryDeserialize<CopyMoveChangePayload>(entry.PayloadJson, out copyMovePayload) && copyMovePayload.Paths.Count > 0)
                    {
                        PathPairChange first = copyMovePayload.Paths[0];
                        return entry.Kind + "  " + Path.GetFileName(first.SourcePath) + " -> " + Path.GetFileName(first.DestinationPath);
                    }
                    break;
                case ChangeLogKind.Convert:
                    ConvertFileChange convertChange;
                    if (ChangeLogStore.TryDeserialize<ConvertFileChange>(entry.PayloadJson, out convertChange))
                    {
                        return "Convert  " + Path.GetFileName(convertChange.SourcePath) + " -> " + Path.GetFileName(convertChange.CreatedPath);
                    }
                    ConvertChangePayload convertPayload;
                    if (ChangeLogStore.TryDeserialize<ConvertChangePayload>(entry.PayloadJson, out convertPayload) && convertPayload.CreatedPaths.Count > 0)
                    {
                        return "Convert  " + Path.GetFileName(convertPayload.CreatedPaths[0]);
                    }
                    break;
                case ChangeLogKind.DateUpdate:
                    DateUpdateFileChange dateChange;
                    if (ChangeLogStore.TryDeserialize<DateUpdateFileChange>(entry.PayloadJson, out dateChange))
                    {
                        return "Date update  " + Path.GetFileName(dateChange.FilePath) + "  " + dateChange.OldDate.ToString("yyyy-MM-dd") + " -> " + dateChange.NewDate.ToString("yyyy-MM-dd");
                    }
                    DateUpdateChangePayload datePayload;
                    if (ChangeLogStore.TryDeserialize<DateUpdateChangePayload>(entry.PayloadJson, out datePayload) && datePayload.Files.Count > 0)
                    {
                        DateUpdateFileChange first = datePayload.Files[0];
                        return "Date update  " + Path.GetFileName(first.FilePath) + "  " + first.OldDate.ToString("yyyy-MM-dd") + " -> " + first.NewDate.ToString("yyyy-MM-dd");
                    }
                    break;
                case ChangeLogKind.Rotate:
                    RotateBackupChange rotateChange;
                    if (ChangeLogStore.TryDeserialize<RotateBackupChange>(entry.PayloadJson, out rotateChange))
                    {
                        return "Rotate  " + Path.GetFileName(rotateChange.FilePath);
                    }
                    RotateChangePayload rotatePayload;
                    if (ChangeLogStore.TryDeserialize<RotateChangePayload>(entry.PayloadJson, out rotatePayload) && rotatePayload.Files.Count > 0)
                    {
                        return "Rotate  " + Path.GetFileName(rotatePayload.Files[0].FilePath);
                    }
                    break;
                case ChangeLogKind.Delete:
                    DeleteBackupChange deleteChange;
                    if (ChangeLogStore.TryDeserialize<DeleteBackupChange>(entry.PayloadJson, out deleteChange))
                    {
                        return "Delete  " + Path.GetFileName(deleteChange.OriginalPath);
                    }
                    DeleteChangePayload deletePayload;
                    if (ChangeLogStore.TryDeserialize<DeleteChangePayload>(entry.PayloadJson, out deletePayload) && deletePayload.Files.Count > 0)
                    {
                        return "Delete  " + Path.GetFileName(deletePayload.Files[0].OriginalPath);
                    }
                    break;
                case ChangeLogKind.Rating:
                    RatingFileChange ratingChange;
                    if (ChangeLogStore.TryDeserialize<RatingFileChange>(entry.PayloadJson, out ratingChange))
                    {
                        string oldRating = ratingChange.OldRatingHasValue ? ratingChange.OldRating + " star(s)" : "None";
                        string newRating = ratingChange.NewRatingHasValue ? ratingChange.NewRating + " star(s)" : "None";
                        return "Rating  " + Path.GetFileName(ratingChange.FilePath) + "  " + oldRating + " -> " + newRating;
                    }
                    RatingChangePayload ratingPayload;
                    if (ChangeLogStore.TryDeserialize<RatingChangePayload>(entry.PayloadJson, out ratingPayload) && ratingPayload.Files.Count > 0)
                    {
                        RatingFileChange first = ratingPayload.Files[0];
                        string oldRating = first.OldRatingHasValue ? first.OldRating + " star(s)" : "None";
                        string newRating = first.NewRatingHasValue ? first.NewRating + " star(s)" : "None";
                        return "Rating  " + Path.GetFileName(first.FilePath) + "  " + oldRating + " -> " + newRating;
                    }
                    break;
                case ChangeLogKind.SessionSettings:
                    SessionSettingsChangePayload sessionChange = ChangeLogStore.Deserialize<SessionSettingsChangePayload>(entry.PayloadJson);
                    return "Session settings  " + (sessionChange.UpdatedSession == null ? entry.SessionName : sessionChange.UpdatedSession.Name);
            }

            return entry.Summary;
        }
    }
}
