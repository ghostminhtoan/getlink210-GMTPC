using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;

namespace get_link_manga
{
    [DataContract]
    public class BookmarkEntry
    {
        [DataMember] public string Name { get; set; }
        [DataMember] public string Url { get; set; }
        [DataMember] public string SourceDomain { get; set; }
        [DataMember] public DateTime BookmarkedAt { get; set; }
        [DataMember] public string Notes { get; set; }
    }

    [DataContract]
    public class HistoryEntry
    {
        [DataMember] public string Name { get; set; }
        [DataMember] public string Url { get; set; }
        [DataMember] public string SourceDomain { get; set; }
        [DataMember] public DateTime DownloadedAt { get; set; }
        [DataMember] public int ChaptersDownloaded { get; set; }
        [DataMember] public string DownloadPath { get; set; }
    }

    [DataContract]
    public class BookmarkData
    {
        [DataMember] public List<BookmarkEntry> Bookmarks { get; set; } = new List<BookmarkEntry>();
    }

    [DataContract]
    public class HistoryData
    {
        [DataMember] public List<HistoryEntry> Entries { get; set; } = new List<HistoryEntry>();
    }

    public class BookmarkHistoryManager
    {
        private readonly string _dataDir;
        private readonly string _bookmarksPath;
        private readonly string _historyPath;

        public BookmarkHistoryManager()
        {
            _dataDir = AppDomain.CurrentDomain.BaseDirectory;
            _bookmarksPath = Path.Combine(_dataDir, "bookmarks.json");
            _historyPath = Path.Combine(_dataDir, "history.json");

            if (!Directory.Exists(_dataDir))
                Directory.CreateDirectory(_dataDir);
        }

        // ===== BOOKMARKS =====

        public List<BookmarkEntry> GetBookmarks()
        {
            var data = LoadJson<BookmarkData>(_bookmarksPath);
            return data?.Bookmarks ?? new List<BookmarkEntry>();
        }

        public void AddBookmark(BookmarkEntry entry)
        {
            var data = LoadJson<BookmarkData>(_bookmarksPath) ?? new BookmarkData();
            // Prevent duplicates by URL
            if (!data.Bookmarks.Any(b => b.Url.Equals(entry.Url, StringComparison.OrdinalIgnoreCase)))
            {
                data.Bookmarks.Insert(0, entry); // Newest first
                SaveJson(_bookmarksPath, data);
            }
        }

        public void RemoveBookmark(string url)
        {
            var data = LoadJson<BookmarkData>(_bookmarksPath);
            if (data == null) return;
            data.Bookmarks.RemoveAll(b => b.Url.Equals(url, StringComparison.OrdinalIgnoreCase));
            SaveJson(_bookmarksPath, data);
        }

        public bool IsBookmarked(string url)
        {
            var bookmarks = GetBookmarks();
            return bookmarks.Any(b => b.Url.Equals(url, StringComparison.OrdinalIgnoreCase));
        }

        // ===== HISTORY =====

        public List<HistoryEntry> GetHistory()
        {
            var data = LoadJson<HistoryData>(_historyPath);
            return data?.Entries ?? new List<HistoryEntry>();
        }

        public void AddHistory(HistoryEntry entry)
        {
            var data = LoadJson<HistoryData>(_historyPath) ?? new HistoryData();
            data.Entries.Insert(0, entry); // Newest first
            // Keep max 500 entries
            if (data.Entries.Count > 500)
                data.Entries = data.Entries.Take(500).ToList();
            SaveJson(_historyPath, data);
        }

        public void ClearHistory()
        {
            SaveJson(_historyPath, new HistoryData());
        }

        public void ExportBookmarks(string destPath)
        {
            var data = LoadJson<BookmarkData>(_bookmarksPath) ?? new BookmarkData();
            SaveJson(destPath, data);
        }

        public bool ImportBookmarks(string srcPath)
        {
            var data = LoadJson<BookmarkData>(srcPath);
            if (data != null && data.Bookmarks != null)
            {
                var current = GetBookmarks();
                foreach (var b in data.Bookmarks)
                {
                    if (!current.Any(x => x.Url.Equals(b.Url, StringComparison.OrdinalIgnoreCase)))
                    {
                        current.Add(b);
                    }
                }
                SaveJson(_bookmarksPath, new BookmarkData { Bookmarks = current });
                return true;
            }
            return false;
        }

        public void ExportHistory(string destPath)
        {
            var data = LoadJson<HistoryData>(_historyPath) ?? new HistoryData();
            SaveJson(destPath, data);
        }

        public bool ImportHistory(string srcPath)
        {
            var data = LoadJson<HistoryData>(srcPath);
            if (data != null && data.Entries != null)
            {
                var current = GetHistory();
                current.InsertRange(0, data.Entries);
                var merged = current.OrderByDescending(x => x.DownloadedAt).Take(500).ToList();
                SaveJson(_historyPath, new HistoryData { Entries = merged });
                return true;
            }
            return false;
        }

        // ===== JSON helpers (DataContractJsonSerializer for .NET Framework 4.8 compatibility) =====

        private T LoadJson<T>(string path) where T : class
        {
            if (!File.Exists(path)) return null;
            try
            {
                string json = File.ReadAllText(path, Encoding.UTF8);
                if (string.IsNullOrWhiteSpace(json)) return null;

                var serializer = new DataContractJsonSerializer(typeof(T), new DataContractJsonSerializerSettings
                {
                    DateTimeFormat = new DateTimeFormat("yyyy-MM-ddTHH:mm:ssZ")
                });
                using (var ms = new MemoryStream(Encoding.UTF8.GetBytes(json)))
                {
                    return serializer.ReadObject(ms) as T;
                }
            }
            catch
            {
                return null;
            }
        }

        private void SaveJson<T>(string path, T data) where T : class
        {
            try
            {
                var serializer = new DataContractJsonSerializer(typeof(T), new DataContractJsonSerializerSettings
                {
                    DateTimeFormat = new DateTimeFormat("yyyy-MM-ddTHH:mm:ssZ")
                });
                using (var ms = new MemoryStream())
                {
                    serializer.WriteObject(ms, data);
                    string json = Encoding.UTF8.GetString(ms.ToArray());
                    File.WriteAllText(path, json, Encoding.UTF8);
                }
            }
            catch
            {
                // Silently fail — bookmark/history is non-critical
            }
        }
    }
}
