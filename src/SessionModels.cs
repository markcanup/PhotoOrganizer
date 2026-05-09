using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;

namespace PictureOrganizer
{
    [DataContract]
    public sealed class AppConfig
    {
        [DataMember]
        public List<OrganizerSession> Sessions { get; set; }

        [DataMember]
        public List<RenameRule> RenameRules { get; set; }

        [DataMember]
        public string LastSessionName { get; set; }

        [DataMember]
        public List<FileRatingEntry> Ratings { get; set; }

        [DataMember]
        public string LastBrowsedFolder { get; set; }

        public AppConfig()
        {
            Sessions = new List<OrganizerSession>();
            RenameRules = new List<RenameRule>();
            LastSessionName = string.Empty;
            Ratings = new List<FileRatingEntry>();
            LastBrowsedFolder = string.Empty;
        }
    }

    [DataContract]
    public sealed class OrganizerSession
    {
        [DataMember]
        public string Name { get; set; }

        [DataMember]
        public string SessionId { get; set; }

        [DataMember]
        public string SourceFolder { get; set; }

        [DataMember]
        public List<string> SourceFolders { get; set; }

        [DataMember]
        public List<string> DestinationFolders { get; set; }

        [DataMember]
        public List<SessionActionType> VisibleActions { get; set; }

        [DataMember]
        public int ThumbnailSize { get; set; }

        [DataMember]
        public bool ShowFileName { get; set; }

        [DataMember]
        public SessionSortOrder SortOrder { get; set; }

        [DataMember]
        public int InfoPanePercent { get; set; }

        [DataMember]
        public bool RecurseSubdirectories { get; set; }

        [DataMember]
        public bool HighlightDateDifferences { get; set; }

        [DataMember]
        public bool ShowActionsInInfoPanel { get; set; }

        public OrganizerSession()
        {
            Name = string.Empty;
            SessionId = Guid.NewGuid().ToString("N");
            SourceFolder = string.Empty;
            SourceFolders = new List<string>();
            DestinationFolders = new List<string>();
            VisibleActions = SessionActionCatalog.GetDefaultVisibleActions().ToList();
            ThumbnailSize = 150;
            ShowFileName = false;
            SortOrder = SessionSortOrder.FileNameAscending;
            InfoPanePercent = 25;
            RecurseSubdirectories = false;
            HighlightDateDifferences = false;
            ShowActionsInInfoPanel = false;
        }

        public OrganizerSession Clone()
        {
            return new OrganizerSession
            {
                Name = Name,
                SessionId = SessionId,
                SourceFolder = SourceFolder,
                SourceFolders = SourceFolders == null ? new List<string>() : new List<string>(SourceFolders),
                DestinationFolders = DestinationFolders == null ? new List<string>() : new List<string>(DestinationFolders),
                VisibleActions = VisibleActions == null ? SessionActionCatalog.GetDefaultVisibleActions().ToList() : new List<SessionActionType>(VisibleActions),
                ThumbnailSize = ThumbnailSize,
                ShowFileName = ShowFileName,
                SortOrder = SortOrder,
                InfoPanePercent = InfoPanePercent,
                RecurseSubdirectories = RecurseSubdirectories,
                HighlightDateDifferences = HighlightDateDifferences,
                ShowActionsInInfoPanel = ShowActionsInInfoPanel
            };
        }

        public List<string> GetSourceFolders()
        {
            if (SourceFolders != null && SourceFolders.Count > 0)
            {
                return SourceFolders
                    .Where(path => !string.IsNullOrWhiteSpace(path))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }

            return string.IsNullOrWhiteSpace(SourceFolder)
                ? new List<string>()
                : new List<string> { SourceFolder };
        }
    }

    [DataContract]
    public sealed class FileRatingEntry
    {
        [DataMember]
        public string FilePath { get; set; }

        [DataMember]
        public int Rating { get; set; }
    }

    [DataContract]
    public sealed class RenameRule
    {
        [DataMember]
        public string Name { get; set; }

        [DataMember]
        public RenameRuleType RuleType { get; set; }

        [DataMember]
        public string Value1 { get; set; }

        [DataMember]
        public string Value2 { get; set; }

        public RenameRule()
        {
            Name = "New Rule";
            RuleType = RenameRuleType.AddTextToStart;
            Value1 = string.Empty;
            Value2 = string.Empty;
        }

        public RenameRule Clone()
        {
            return new RenameRule
            {
                Name = Name,
                RuleType = RuleType,
                Value1 = Value1,
                Value2 = Value2
            };
        }

        public override string ToString() { return Name; }
    }

    [DataContract]
    public enum RenameRuleType
    {
        [EnumMember]
        AddTextToStart,
        [EnumMember]
        AddTextToEnd,
        [EnumMember]
        RemoveText,
        [EnumMember]
        SubstituteText,
        [EnumMember]
        ReplaceFullFilename
    }

    [DataContract]
    public enum SessionSortOrder
    {
        [EnumMember]
        FileNameAscending,
        [EnumMember]
        FileNameDescending,
        [EnumMember]
        ModifiedDateAscending,
        [EnumMember]
        ModifiedDateDescending,
        [EnumMember]
        FileSizeAscending,
        [EnumMember]
        FileSizeDescending
    }

    [DataContract]
    public enum SessionActionType
    {
        [EnumMember]
        View,
        [EnumMember]
        Fullscreen,
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
        Autocrop,
        [EnumMember]
        Rotate,
        [EnumMember]
        Delete,
        [EnumMember]
        Rating,
        [EnumMember]
        Compare,
        [EnumMember]
        Edit
    }

    internal static class SessionActionCatalog
    {
        public static SessionActionType[] GetAll()
        {
            return new[]
            {
                SessionActionType.View,
                SessionActionType.Copy,
                SessionActionType.Move,
                SessionActionType.DateUpdate,
                SessionActionType.Rename,
                SessionActionType.Convert,
                SessionActionType.Autocrop,
                SessionActionType.Rotate,
                SessionActionType.Delete,
                SessionActionType.Rating,
                SessionActionType.Compare,
                SessionActionType.Edit
            };
        }

        public static SessionActionType[] GetDefaultVisibleActions()
        {
            return new[]
            {
                SessionActionType.View,
                SessionActionType.Copy,
                SessionActionType.Move,
                SessionActionType.Rename,
                SessionActionType.Delete
            };
        }

        public static string GetDisplayName(SessionActionType actionType)
        {
            switch (actionType)
            {
                case SessionActionType.View: return "View";
                case SessionActionType.Fullscreen: return "Fullscreen";
                case SessionActionType.Copy: return "Copy";
                case SessionActionType.Move: return "Move";
                case SessionActionType.DateUpdate: return "Date update";
                case SessionActionType.Rename: return "Rename";
                case SessionActionType.Convert: return "Convert";
                case SessionActionType.Autocrop: return "Autocrop";
                case SessionActionType.Rotate: return "Rotate";
                case SessionActionType.Delete: return "Delete";
                case SessionActionType.Rating: return "Rating";
                case SessionActionType.Compare: return "Compare";
                case SessionActionType.Edit: return "Edit";
                default: return actionType.ToString();
            }
        }
    }

    internal static class SessionSortCatalog
    {
        public static SessionSortOrder[] GetAll()
        {
            return new[]
            {
                SessionSortOrder.FileNameAscending,
                SessionSortOrder.FileNameDescending,
                SessionSortOrder.ModifiedDateAscending,
                SessionSortOrder.ModifiedDateDescending,
                SessionSortOrder.FileSizeAscending,
                SessionSortOrder.FileSizeDescending
            };
        }

        public static string GetDisplayName(SessionSortOrder sortOrder)
        {
            switch (sortOrder)
            {
                case SessionSortOrder.FileNameAscending: return "By filename (A to Z)";
                case SessionSortOrder.FileNameDescending: return "By filename (Z to A)";
                case SessionSortOrder.ModifiedDateAscending: return "By modified date (Old to New)";
                case SessionSortOrder.ModifiedDateDescending: return "By modified date (New to Old)";
                case SessionSortOrder.FileSizeAscending: return "By file size (Small to Large)";
                case SessionSortOrder.FileSizeDescending: return "By file size (Large to Small)";
                default: return sortOrder.ToString();
            }
        }
    }

    internal static class RenameRuleCatalog
    {
        public static RenameRuleType[] GetAll()
        {
            return new[]
            {
                RenameRuleType.AddTextToStart,
                RenameRuleType.AddTextToEnd,
                RenameRuleType.RemoveText,
                RenameRuleType.SubstituteText,
                RenameRuleType.ReplaceFullFilename
            };
        }

        public static string GetDisplayName(RenameRuleType type)
        {
            switch (type)
            {
                case RenameRuleType.AddTextToStart: return "Add text to start of filename";
                case RenameRuleType.AddTextToEnd: return "Add text to end of filename";
                case RenameRuleType.RemoveText: return "Remove text from filename";
                case RenameRuleType.SubstituteText: return "Substitute text in filename";
                case RenameRuleType.ReplaceFullFilename: return "Replace full filename";
                default: return type.ToString();
            }
        }
    }
}
