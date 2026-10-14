using System;
using System.IO;

namespace PictureOrganizer
{
    internal sealed class FileOperationJournal
    {
        private readonly string _sessionId, _sessionName;
        private readonly Action<ChangeLogEntry> _append;
        private readonly Action<string> _remove;
        private readonly Func<string, string, string> _backupPath;

        public FileOperationJournal(OrganizerSession session)
            : this(session.SessionId, session.Name, ChangeLogStore.Append,
                id => ChangeLogStore.RemoveEntries(new[] { id }), ChangeLogStore.CreateBackupPath) { }

        internal FileOperationJournal(string sessionId, string sessionName, Action<ChangeLogEntry> append,
            Action<string> remove, Func<string, string, string> backupPath)
        {
            _sessionId = sessionId; _sessionName = sessionName;
            _append = append; _remove = remove; _backupPath = backupPath;
        }

        public string Backup(string entryId, string path)
        {
            string backup = _backupPath(entryId, path);
            File.Copy(path, backup, false);
            using (FileStream stream = File.Open(backup, FileMode.Open, FileAccess.ReadWrite, FileShare.Read)) stream.Flush(true);
            FileSafety.RequireHash(backup, FileSafety.Hash(path));
            return backup;
        }

        public void Execute<T>(string id, ChangeLogKind kind, string summary, T payload, Action commit, Action recover)
        {
            // Persist the complete recovery record before touching any live file.
            _append(new ChangeLogEntry { EntryId = id, SessionId = _sessionId, SessionName = _sessionName,
                TimestampUtc = DateTime.UtcNow, Kind = kind, Summary = summary, PayloadJson = ChangeLogStore.Serialize(payload) });
            try { commit(); }
            catch (Exception failure)
            {
                try { recover(); _remove(id); }
                catch (Exception recoveryFailure)
                {
                    throw new IOException(failure.Message + " Recovery was incomplete; the undo entry and backups were retained. " + recoveryFailure.Message, failure);
                }
                throw;
            }
        }
    }
}
