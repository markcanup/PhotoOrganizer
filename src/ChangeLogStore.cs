using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Json;
using System.Text;

namespace PictureOrganizer
{
    internal static class ChangeLogStore
    {
        public static void Append(ChangeLogEntry entry)
        {
            Directory.CreateDirectory(AppPaths.ConfigDirectory);
            using (FileStream stream = new FileStream(AppPaths.ChangeLogFilePath, FileMode.Append, FileAccess.Write, FileShare.Read))
            {
                byte[] bytes = Encoding.UTF8.GetBytes(Serialize(entry) + Environment.NewLine);
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush(true);
            }
        }

        public static List<ChangeLogEntry> LoadForSession(string sessionId)
        {
            return LoadAll().Where(item => string.Equals(item.SessionId, sessionId, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(item => item.TimestampUtc)
                .ToList();
        }

        public static void RemoveEntries(IEnumerable<string> entryIds)
        {
            HashSet<string> toRemove = new HashSet<string>(entryIds ?? Enumerable.Empty<string>(), StringComparer.OrdinalIgnoreCase);
            if (toRemove.Count == 0)
            {
                return;
            }

            // Preserve unreadable lines rather than discarding unrelated recovery records during compaction.
            List<string> remaining = new List<string>();
            if (File.Exists(AppPaths.ChangeLogFilePath))
            {
                foreach (string line in File.ReadAllLines(AppPaths.ChangeLogFilePath))
                {
                    ChangeLogEntry entry;
                    if (!TryDeserialize<ChangeLogEntry>(line, out entry) || entry == null || !toRemove.Contains(entry.EntryId)) remaining.Add(line);
                }
            }
            RewriteAll(remaining);
        }

        public static string CreateBackupPath(string entryId, string filePath)
        {
            ValidateEntryId(entryId);
            string directory = Path.Combine(AppPaths.UndoBackupDirectory, entryId);
            Directory.CreateDirectory(directory);
            return Path.Combine(directory, Guid.NewGuid().ToString("N") + Path.GetExtension(filePath));
        }

        public static void DeleteBackupFolder(string entryId)
        {
            ValidateEntryId(entryId);
            string directory = Path.Combine(AppPaths.UndoBackupDirectory, entryId);
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
        }

        private static void ValidateEntryId(string entryId)
        {
            Guid parsed;
            if (!Guid.TryParseExact(entryId, "N", out parsed)) throw new InvalidDataException("Invalid undo entry ID.");
        }

        public static string Serialize<T>(T value)
        {
            DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(T));
            using (MemoryStream stream = new MemoryStream())
            {
                serializer.WriteObject(stream, value);
                return Encoding.UTF8.GetString(stream.ToArray());
            }
        }

        public static T Deserialize<T>(string json)
        {
            DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(T));
            using (MemoryStream stream = new MemoryStream(Encoding.UTF8.GetBytes(json ?? string.Empty)))
            {
                return (T)serializer.ReadObject(stream);
            }
        }

        public static bool TryDeserialize<T>(string json, out T value)
        {
            try
            {
                value = Deserialize<T>(json);
                return true;
            }
            catch
            {
                value = default(T);
                return false;
            }
        }

        private static List<ChangeLogEntry> LoadAll()
        {
            if (!File.Exists(AppPaths.ChangeLogFilePath))
            {
                return new List<ChangeLogEntry>();
            }

            List<ChangeLogEntry> entries = new List<ChangeLogEntry>();
            foreach (string line in File.ReadAllLines(AppPaths.ChangeLogFilePath))
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                try
                {
                    ChangeLogEntry entry = Deserialize<ChangeLogEntry>(line);
                    if (entry != null) entries.Add(entry);
                }
                catch
                {
                }
            }

            return entries;
        }

        private static void RewriteAll(List<string> entries)
        {
            Directory.CreateDirectory(AppPaths.ConfigDirectory);
            string temporaryPath = FileSafety.TemporaryPath(AppPaths.ChangeLogFilePath);
            try
            {
                using (StreamWriter writer = new StreamWriter(temporaryPath, false, Encoding.UTF8))
                {
                    foreach (string entry in entries) writer.WriteLine(entry);
                }
                FileSafety.Publish(temporaryPath, AppPaths.ChangeLogFilePath, File.Exists(AppPaths.ChangeLogFilePath));
            }
            finally { if (File.Exists(temporaryPath)) File.Delete(temporaryPath); }
        }
    }
}
