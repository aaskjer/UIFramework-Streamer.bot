using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using Xunit;

namespace UIFramework.Tests
{
    /// <summary>What the audit of 08.11.2026 found and 1.2.0 fixes: a version that is no number, a game setting named like
    /// a theme key, a condition with "!" in front of a comparison, a cooldown that reads no setting, and list files that
    /// are held open or could not be read when the window opened.</summary>
    public class AuditFixTests
    {
        private static string Folder()
        {
            string folder = Path.Combine(Path.GetTempPath(), "UIFramework.Tests", "audit-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            return folder;
        }

        [Fact]
        public void A_version_that_is_no_number_is_never_behind()
        {
            Assert.False(Web.IsNewerVersion("v1.1.0", "dev"));
            Assert.False(Web.IsNewerVersion("v1.1.0", ""));
            Assert.False(Web.IsNewerVersion("v1.1.0", null));
            Assert.True(Web.IsNewerVersion("v1.1.0", "v1.0.0"));
        }

        [Fact]
        public void A_game_setting_named_like_a_theme_key_stays_in_the_game_file()
        {
            var layout = new LayoutFile(Path.Combine(Folder(), "settings.json"));
            var host = new JObject { ["egg"] = true, ["darkMode"] = false };
            Assert.False(layout.AdoptFromHost(host, new HashSet<string> { "egg", "darkMode" }, "darkMode", "DarkMode"));
            Assert.True((bool)host["egg"]);
            Assert.False((bool)host["darkMode"]);
            Assert.Null(layout.Dark);
            Assert.True(layout.AdoptFromHost(host, new HashSet<string>(), "darkMode"));   // not declared: the theme moves
            Assert.Null(host["darkMode"]);
        }

        private static WindowSpec Prepared(string settings, string window)
        {
            var root = JObject.Parse(@"{ ""host"": ""demo"", ""version"": ""v1"", ""settingsFile"": ""x"", ""settings"": [" + settings + @"], ""window"": " + window + " }");
            var schema = Schema.Parse(root);
            var spec = Entry.SpecFor(null, schema, Path.Combine(Folder(), "settings.json"));
            Entry.Prepare(spec, schema);
            return spec;
        }

        [Fact]
        public void A_bang_before_a_comparison_is_named_with_the_way_to_write_it()
        {
            var spec = Prepared(@"{ ""key"": ""mode"", ""default"": ""a"", ""kind"": ""options"", ""options"": [""a"", ""free""] },
                                  { ""key"": ""bet"", ""default"": 1, ""page"": ""game"", ""showWhen"": ""!mode=free"" }",
                                @"{ ""title"": ""T"", ""pages"": [ { ""id"": ""game"", ""fields"": [""*""] } ] }");
            Assert.Contains(spec.Problems, p => p.Contains("'!mode=free'") && p.Contains("'mode!=free'"));
        }

        [Fact]
        public void A_cooldown_that_reads_no_setting_is_named()
        {
            var spec = Prepared(@"{ ""key"": ""globalCooldown"", ""default"": 10 }",
                                @"{ ""title"": ""T"", ""pages"": [ { ""id"": ""game"", ""fields"": [""*""] } ], ""cooldowns"": [ { ""command"": ""!roll"", ""global"": ""globalCooldown"", ""user"": ""userCooldown"" } ] }");
            Assert.Contains(spec.Problems, p => p.Contains("'!roll'") && p.Contains("'userCooldown'"));
            Assert.DoesNotContain(spec.Problems, p => p.Contains("'globalCooldown', which"));
        }

        private static List<Setting> ListSchema() => new List<Setting>
        {
            new Setting { Key = "words", Kind = "list", File = "words.json", Default = new JArray("a") }
        };

        [Fact]
        public void A_list_file_held_open_stops_the_write_instead_of_being_written_over()
        {
            string folder = Folder();
            string settingsPath = Path.Combine(folder, "settings.json");
            string wordsPath = Path.Combine(folder, "words.json");
            File.WriteAllText(wordsPath, "[\"from the game\"]");
            var settings = new JObject { ["words"] = new JArray("from the window") };
            using (new FileStream(wordsPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                Assert.ThrowsAny<IOException>(() => Sidecars.Split(settings, ListSchema(), settingsPath));
            Assert.Contains("from the game", File.ReadAllText(wordsPath));
            Assert.False(File.Exists(wordsPath + ".broken"));
        }

        [Fact]
        public void A_list_the_window_could_not_read_is_never_written()
        {
            string folder = Folder();
            string settingsPath = Path.Combine(folder, "settings.json");
            string wordsPath = Path.Combine(folder, "words.json");
            File.WriteAllText(wordsPath, "[\"the real list\"]");
            var settings = new JObject { ["words"] = new JArray("a default"), ["other"] = 1 };
            var main = Sidecars.Split(settings, ListSchema(), settingsPath, null, new HashSet<string> { "words" });
            Assert.Contains("the real list", File.ReadAllText(wordsPath));
            Assert.Null(main["words"]);
            Assert.Equal(1, (int)main["other"]);
        }
    }
}
