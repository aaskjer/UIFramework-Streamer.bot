using System;
using System.IO;
using Newtonsoft.Json.Linq;
using Xunit;

namespace UIFramework.Tests
{
    public class SettingsStoreTests : IDisposable
    {
        private readonly string _folder = Path.Combine(Path.GetTempPath(), "UIFramework.Tests", Guid.NewGuid().ToString("N"));
        private string File_ => Path.Combine(_folder, "settings.json");
        private static JObject Defaults => JObject.Parse(@"{ ""a"": 1, ""b"": ""two"", ""flag"": true }");

        public SettingsStoreTests() { Directory.CreateDirectory(_folder); }
        public void Dispose() { try { Directory.Delete(_folder, true); } catch { } }

        [Fact]
        public void MissingFileGivesDefaults()
        {
            var load = new SettingsStore(File_, Defaults).Load();
            Assert.Null(load.Damaged);
            Assert.Equal(1, (int)load.Settings["a"]);
            Assert.Equal("two", (string)load.Settings["b"]);
        }

        [Fact]
        public void FileValuesWinOverDefaultsAndUnknownKeysSurvive()
        {
            File.WriteAllText(File_, @"{ ""a"": 5, ""extra"": ""kept"" }");
            var store = new SettingsStore(File_, Defaults);
            var settings = store.Load().Settings;
            Assert.Equal(5, (int)settings["a"]);
            Assert.Equal("two", (string)settings["b"]);
            Assert.Equal("kept", (string)settings["extra"]);

            settings["a"] = 7;
            store.Save(settings);
            var again = JObject.Parse(File.ReadAllText(File_));
            Assert.Equal(7, (int)again["a"]);
            Assert.Equal("kept", (string)again["extra"]);
        }

        [Fact]
        public void SaveKeepsBackupAndDamagedFileFallsBackToIt()
        {
            var store = new SettingsStore(File_, Defaults);
            var first = store.Load().Settings;
            first["a"] = 10;
            store.Save(first);
            first["a"] = 11;
            store.Save(first);
            Assert.True(File.Exists(store.BackupPath));
            Assert.Equal(10, (int)JObject.Parse(File.ReadAllText(store.BackupPath))["a"]);

            File.WriteAllText(File_, "{ this is not json");
            var load = store.Load();
            Assert.NotNull(load.Damaged);
            Assert.True(load.FromBackup);
            Assert.Equal(10, (int)load.Settings["a"]);
            Assert.True(File.Exists(store.BrokenPath));
        }

        [Fact]
        public void DamagedFileWithoutBackupGivesDefaults()
        {
            File.WriteAllText(File_, "[]");
            var load = new SettingsStore(File_, Defaults).Load();
            Assert.NotNull(load.Damaged);
            Assert.False(load.FromBackup);
            Assert.Equal(1, (int)load.Settings["a"]);
        }

        [Fact]
        public void DatesStayText()
        {
            File.WriteAllText(File_, @"{ ""b"": ""2026-01-01"" }");
            var settings = new SettingsStore(File_, Defaults).Load().Settings;
            Assert.Equal(JTokenType.String, settings["b"].Type);
            Assert.Equal("2026-01-01", (string)settings["b"]);
        }
    }
}
