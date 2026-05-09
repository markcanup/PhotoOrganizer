using System;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Json;

namespace PictureOrganizer
{
    internal static class SessionConfigStore
    {
        private static readonly DataContractJsonSerializer Serializer = new DataContractJsonSerializer(typeof(AppConfig));

        public static AppConfig Load()
        {
            try
            {
                if (!File.Exists(AppPaths.ConfigFilePath))
                {
                    return new AppConfig();
                }

                using (FileStream stream = File.OpenRead(AppPaths.ConfigFilePath))
                {
                    AppConfig loaded = (AppConfig)Serializer.ReadObject(stream);
                    if (loaded == null)
                    {
                        return new AppConfig();
                    }

                    if (loaded.Sessions == null)
                    {
                        loaded.Sessions = new System.Collections.Generic.List<OrganizerSession>();
                    }

                    if (loaded.RenameRules == null)
                    {
                        loaded.RenameRules = new System.Collections.Generic.List<RenameRule>();
                    }

                    if (loaded.Ratings == null)
                    {
                        loaded.Ratings = new System.Collections.Generic.List<FileRatingEntry>();
                    }

                    if (loaded.LastBrowsedFolder == null)
                    {
                        loaded.LastBrowsedFolder = string.Empty;
                    }

                    NormalizeSessions(loaded);
                    return loaded;
                }
            }
            catch
            {
                return new AppConfig();
            }
        }

        public static void Save(AppConfig config)
        {
            if (config == null)
            {
                throw new ArgumentNullException("config");
            }

            NormalizeSessions(config);
            Directory.CreateDirectory(AppPaths.ConfigDirectory);
            using (FileStream stream = File.Create(AppPaths.ConfigFilePath))
            {
                Serializer.WriteObject(stream, config);
            }
        }

        public static void SaveSession(OrganizerSession session)
        {
            if (session == null)
            {
                throw new ArgumentNullException("session");
            }

            AppConfig config = Load();
            OrganizerSession existing = config.Sessions
                .FirstOrDefault(item =>
                    (!string.IsNullOrWhiteSpace(session.SessionId) && string.Equals(item.SessionId, session.SessionId, StringComparison.OrdinalIgnoreCase))
                    || string.Equals(item.Name, session.Name, StringComparison.OrdinalIgnoreCase));

            if (existing != null)
            {
                config.Sessions.Remove(existing);
            }

            config.Sessions.Add(session.Clone());
            config.Sessions = config.Sessions
                .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
            config.LastSessionName = session.Name;
            Save(config);
        }

        private static void NormalizeSessions(AppConfig config)
        {
            foreach (OrganizerSession session in config.Sessions.Where(item => item != null))
            {
                if (session.DestinationFolders == null)
                {
                    session.DestinationFolders = new System.Collections.Generic.List<string>();
                }

                if (session.SourceFolders == null)
                {
                    session.SourceFolders = new System.Collections.Generic.List<string>();
                }

                if (session.SourceFolders.Count == 0 && !string.IsNullOrWhiteSpace(session.SourceFolder))
                {
                    session.SourceFolders.Add(session.SourceFolder);
                }

                session.SourceFolders = session.SourceFolders
                    .Where(path => !string.IsNullOrWhiteSpace(path))
                    .Select(path => path.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
                session.SourceFolder = session.SourceFolders.FirstOrDefault() ?? string.Empty;

                if (string.IsNullOrWhiteSpace(session.SessionId))
                {
                    session.SessionId = Guid.NewGuid().ToString("N");
                }

                if (session.VisibleActions == null || session.VisibleActions.Count == 0)
                {
                    session.VisibleActions = SessionActionCatalog.GetDefaultVisibleActions().ToList();
                }
                else
                {
                    session.VisibleActions = NormalizeActions(session.VisibleActions);
                }

                if (session.ThumbnailSize < 80)
                {
                    session.ThumbnailSize = 150;
                }

                if (session.InfoPanePercent < 1 || session.InfoPanePercent > 50)
                {
                    session.InfoPanePercent = 25;
                }
            }
        }

        private static System.Collections.Generic.List<SessionActionType> NormalizeActions(System.Collections.Generic.IEnumerable<SessionActionType> actions)
        {
            System.Collections.Generic.List<SessionActionType> normalized = new System.Collections.Generic.List<SessionActionType>();
            foreach (SessionActionType action in actions)
            {
                SessionActionType mapped = action == SessionActionType.Fullscreen || action == SessionActionType.Compare
                    ? SessionActionType.View
                    : action;
                if (!normalized.Contains(mapped))
                {
                    normalized.Add(mapped);
                }
            }

            return normalized
                .Where(action => SessionActionCatalog.GetAll().Contains(action))
                .ToList();
        }

        public static string GetDefaultBrowseFolder(AppConfig config, string preferredPath)
        {
            if (!string.IsNullOrWhiteSpace(preferredPath) && Directory.Exists(preferredPath))
            {
                return preferredPath;
            }

            if (config != null && !string.IsNullOrWhiteSpace(config.LastBrowsedFolder) && Directory.Exists(config.LastBrowsedFolder))
            {
                return config.LastBrowsedFolder;
            }

            string pictures = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
            return Directory.Exists(pictures) ? pictures : Environment.GetFolderPath(Environment.SpecialFolder.Personal);
        }

        public static int? GetRating(AppConfig config, string filePath)
        {
            if (config == null || config.Ratings == null)
            {
                return null;
            }

            FileRatingEntry entry = config.Ratings.FirstOrDefault(item => string.Equals(item.FilePath, filePath, StringComparison.OrdinalIgnoreCase));
            return entry == null ? (int?)null : entry.Rating;
        }

        public static void SetRating(AppConfig config, string filePath, int? rating)
        {
            if (config == null)
            {
                throw new ArgumentNullException("config");
            }

            if (config.Ratings == null)
            {
                config.Ratings = new System.Collections.Generic.List<FileRatingEntry>();
            }

            FileRatingEntry existing = config.Ratings.FirstOrDefault(item => string.Equals(item.FilePath, filePath, StringComparison.OrdinalIgnoreCase));
            if (!rating.HasValue || rating.Value <= 0)
            {
                if (existing != null)
                {
                    config.Ratings.Remove(existing);
                }

                return;
            }

            if (existing == null)
            {
                config.Ratings.Add(new FileRatingEntry
                {
                    FilePath = filePath,
                    Rating = rating.Value
                });
            }
            else
            {
                existing.Rating = rating.Value;
            }
        }

        public static void MoveRating(AppConfig config, string oldPath, string newPath)
        {
            if (config == null || config.Ratings == null)
            {
                return;
            }

            FileRatingEntry existing = config.Ratings.FirstOrDefault(item => string.Equals(item.FilePath, oldPath, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                existing.FilePath = newPath;
            }
        }

        public static void CopyRating(AppConfig config, string sourcePath, string destinationPath)
        {
            int? rating = GetRating(config, sourcePath);
            if (rating.HasValue)
            {
                SetRating(config, destinationPath, rating.Value);
            }
        }

        public static void RemoveRating(AppConfig config, string filePath)
        {
            SetRating(config, filePath, null);
        }
    }
}
