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

            List<ChangeLogEntry> remaining = LoadAll().Where(item => !toRemove.Contains(item.EntryId)).ToList();
            RewriteAll(remaining);
        }

        public static string CreateBackupPath(string entryId, string filePath)
        {
            string directory = Path.Combine(AppPaths.UndoBackupDirectory, entryId);
            Directory.CreateDirectory(directory);
            return Path.Combine(directory, Guid.NewGuid().ToString("N") + Path.GetExtension(filePath));
        }

        public static void DeleteBackupFolder(string entryId)
        {
            string directory = Path.Combine(AppPaths.UndoBackupDirectory, entryId);
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
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
                    entries.Add(Deserialize<ChangeLogEntry>(line));
                }
                catch
                {
                }
            }

            return entries;
        }

        private static void RewriteAll(List<ChangeLogEntry> entries)
        {
            Directory.CreateDirectory(AppPaths.ConfigDirectory);
            using (StreamWriter writer = new StreamWriter(AppPaths.ChangeLogFilePath, false, Encoding.UTF8))
            {
                foreach (ChangeLogEntry entry in entries)
                {
                    writer.WriteLine(Serialize(entry));
                }
            }
        }
    }
}
