using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace PictureOrganizer
{
    [DataContract]
    internal sealed class ChangeLogEntry
    {
        [DataMember]
        public string EntryId { get; set; }

        [DataMember]
        public string SessionId { get; set; }

        [DataMember]
        public string SessionName { get; set; }

        [DataMember]
        public DateTime TimestampUtc { get; set; }

        [DataMember]
        public ChangeLogKind Kind { get; set; }

        [DataMember]
        public string Summary { get; set; }

        [DataMember]
        public string PayloadJson { get; set; }
    }

    [DataContract]
    internal enum ChangeLogKind
    {
        [EnumMember]
        Copy,
        [EnumMember]
        Move,
        [EnumMember]
        DateUpdate,
        [EnumMember]
        Rename,
        [EnumMember]
        Convert,
        [EnumMember]
        Rotate,
        [EnumMember]
        Delete,
        [EnumMember]
        Rating,
        [EnumMember]
        SessionSettings
    }

    [DataContract]
    internal sealed class PathPairChange
    {
        [DataMember]
        public string SourcePath { get; set; }

        [DataMember]
        public string DestinationPath { get; set; }
    }

    [DataContract]
    internal sealed class CopyMoveChangePayload
    {
        [DataMember]
        public List<PathPairChange> Paths { get; set; }

        public CopyMoveChangePayload() { Paths = new List<PathPairChange>(); }
    }

    [DataContract]
    internal sealed class RenameChangePayload
    {
        [DataMember]
        public List<PathPairChange> Paths { get; set; }

        public RenameChangePayload() { Paths = new List<PathPairChange>(); }
    }

    [DataContract]
    internal sealed class ConvertChangePayload
    {
        [DataMember]
        public List<string> CreatedPaths { get; set; }

        public ConvertChangePayload() { CreatedPaths = new List<string>(); }
    }

    [DataContract]
    internal sealed class ConvertFileChange
    {
        [DataMember]
        public string SourcePath { get; set; }

        [DataMember]
        public string CreatedPath { get; set; }
    }

    [DataContract]
    internal sealed class DeleteBackupChange
    {
        [DataMember]
        public string OriginalPath { get; set; }

        [DataMember]
        public string BackupPath { get; set; }
    }

    [DataContract]
    internal sealed class DeleteChangePayload
    {
        [DataMember]
        public List<DeleteBackupChange> Files { get; set; }

        public DeleteChangePayload() { Files = new List<DeleteBackupChange>(); }
    }

    [DataContract]
    internal sealed class RotateBackupChange
    {
        [DataMember]
        public string FilePath { get; set; }

        [DataMember]
        public string BackupPath { get; set; }
    }

    [DataContract]
    internal sealed class RotateChangePayload
    {
        [DataMember]
        public List<RotateBackupChange> Files { get; set; }

        public RotateChangePayload() { Files = new List<RotateBackupChange>(); }
    }

    [DataContract]
    internal sealed class DateUpdateFileChange
    {
        [DataMember]
        public string FilePath { get; set; }

        [DataMember]
        public DateTime OldDate { get; set; }

        [DataMember]
        public DateTime NewDate { get; set; }
    }

    [DataContract]
    internal sealed class DateUpdateChangePayload
    {
        [DataMember]
        public List<DateUpdateFileChange> Files { get; set; }

        public DateUpdateChangePayload() { Files = new List<DateUpdateFileChange>(); }
    }

    [DataContract]
    internal sealed class RatingFileChange
    {
        [DataMember]
        public string FilePath { get; set; }

        [DataMember]
        public int OldRating { get; set; }

        [DataMember]
        public bool OldRatingHasValue { get; set; }

        [DataMember]
        public int NewRating { get; set; }

        [DataMember]
        public bool NewRatingHasValue { get; set; }
    }

    [DataContract]
    internal sealed class RatingChangePayload
    {
        [DataMember]
        public List<RatingFileChange> Files { get; set; }

        public RatingChangePayload() { Files = new List<RatingFileChange>(); }
    }

    [DataContract]
    internal sealed class SessionSettingsChangePayload
    {
        [DataMember]
        public OrganizerSession PreviousSession { get; set; }

        [DataMember]
        public OrganizerSession UpdatedSession { get; set; }
    }
}
