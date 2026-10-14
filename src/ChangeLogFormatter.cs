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
            try
            {
                switch (entry.Kind)
                {
                    case ChangeLogKind.Copy:
                    case ChangeLogKind.Move:
                    case ChangeLogKind.Rename:
                        PathPairChange path = ChangePayloadReader.Paths(entry.PayloadJson)[0];
                        return entry.Kind + "  " + Path.GetFileName(path.SourcePath) + " -> " + Path.GetFileName(path.DestinationPath);
                    case ChangeLogKind.Convert:
                        ConvertFileChange converted = ChangePayloadReader.Conversions(entry.PayloadJson)[0];
                        return "Convert  " + Path.GetFileName(converted.SourcePath) + " -> " + Path.GetFileName(converted.CreatedPath);
                    case ChangeLogKind.DateUpdate:
                        DateUpdateFileChange date = ChangePayloadReader.Dates(entry.PayloadJson)[0];
                        return "Date update  " + Path.GetFileName(date.FilePath) + "  " + date.OldDate.ToString("yyyy-MM-dd") + " -> " + date.NewDate.ToString("yyyy-MM-dd");
                    case ChangeLogKind.Rotate:
                    case ChangeLogKind.Autocrop:
                        return entry.Kind + "  " + Path.GetFileName(ChangePayloadReader.Edits(entry.PayloadJson)[0].FilePath);
                    case ChangeLogKind.Delete:
                        return "Delete  " + Path.GetFileName(ChangePayloadReader.Deletes(entry.PayloadJson)[0].OriginalPath);
                    case ChangeLogKind.Rating:
                        RatingFileChange rating = ChangePayloadReader.Ratings(entry.PayloadJson)[0];
                        return "Rating  " + Path.GetFileName(rating.FilePath) + "  " + (rating.OldRatingHasValue ? rating.OldRating + " star(s)" : "None") + " -> " + (rating.NewRatingHasValue ? rating.NewRating + " star(s)" : "None");
                    case ChangeLogKind.SessionSettings:
                        SessionSettingsChangePayload session = ChangeLogStore.Deserialize<SessionSettingsChangePayload>(entry.PayloadJson);
                        return "Session settings  " + (session.UpdatedSession == null ? entry.SessionName : session.UpdatedSession.Name);
                }
            }
            catch { return entry.Summary + " (invalid undo payload)"; }
            return entry.Summary;
        }
    }
}
