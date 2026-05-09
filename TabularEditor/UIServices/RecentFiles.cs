using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace TabularEditor.UIServices
{
    public class RecentFiles
    {
        public List<string> RecentHistory = new List<string>();
        public RecentModel LastOpenedModel;

        [JsonIgnore]
        static readonly string RECENTFILES_PATH = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + @"\TabularEditor\RecentFiles.json";
        private static RecentFiles _current = null;
        public static RecentFiles Current
        {
            get
            {
                if (_current != null) return _current;
                if (File.Exists(RECENTFILES_PATH))
                {
                    try
                    {
                        var json = File.ReadAllText(RECENTFILES_PATH);
                        _current = JsonConvert.DeserializeObject<RecentFiles>(json);
                    }
                    catch { }
                }
                if (_current == null) _current = new RecentFiles();
                return _current;
            }
        }

        public static IEnumerable<string> GetLast(int n = 10)
        {
            return Enumerable.Reverse(Current.RecentHistory).Take(n);
        }

        public static void Save()
        {
            var json = JsonConvert.SerializeObject(Current, Formatting.Indented);
            (new FileInfo(RECENTFILES_PATH)).Directory.Create();
            File.WriteAllText(RECENTFILES_PATH, json);
        }

        public static void Add(string fileName)
        {
            if (Current.RecentHistory.Contains(fileName, StringComparer.InvariantCultureIgnoreCase))
                Current.RecentHistory.Remove(fileName);

            Current.RecentHistory.Add(fileName);
        }

        public static void SetLastOpenedFile(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName)) return;

            Current.LastOpenedModel = new RecentModel
            {
                SourceType = RecentModelSourceType.File,
                Path = fileName
            };
        }

        public static void SetLastOpenedDatabase(string connectionString, string databaseName)
        {
            if (string.IsNullOrWhiteSpace(connectionString)) return;

            Current.LastOpenedModel = new RecentModel
            {
                SourceType = RecentModelSourceType.Database,
                ConnectionString = connectionString,
                DatabaseName = databaseName
            };
        }
    }

    public enum RecentModelSourceType
    {
        File,
        Database
    }

    public class RecentModel
    {
        public RecentModelSourceType SourceType { get; set; }
        public string Path { get; set; }
        public string ConnectionString { get; set; }
        public string DatabaseName { get; set; }
    }
}
