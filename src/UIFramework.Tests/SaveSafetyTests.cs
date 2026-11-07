using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Controls;
using Newtonsoft.Json.Linq;
using Xunit;

namespace UIFramework.Tests
{
    /// <summary>A save keeps what it did not change: keys the game wrote meanwhile, a list file it did not touch, a
    /// damaged list file, the decimals of a number.</summary>
    public class SaveSafetyTests
    {
        private static void Sta(System.Action body)
        {
            System.Exception failure = null;
            var thread = new Thread(() => { try { body(); } catch (System.Exception ex) { failure = ex; } });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            if (failure != null) throw failure;
        }

        private static string Folder()
        {
            string folder = Path.Combine(Path.GetTempPath(), "UIFramework.Tests", "save-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            return folder;
        }

        [Fact]
        public void Only_the_controls_the_user_changed_count_as_changed() => Sta(() =>
        {
            var parts = new WindowParts();
            var defaults = JObject.Parse(@"{ ""a"": 1, ""b"": 2 }");
            var store = new SettingsStore(Path.GetTempFileName(), defaults);
            var fields = new Fields(parts, store.Merged(JObject.Parse(@"{ ""a"": 1, ""b"": 2 }")), defaults, _ => { });
            var page = new StackPanel();
            page.Children.Add(fields.IntField("A", "a", ""));
            page.Children.Add(fields.IntField("B", "b", ""));
            var baseline = new Dictionary<object, string>();
            FieldBinding.Snapshot(page, baseline);
            parts.Boxes["a"].Text = "7";
            Assert.Equal(new[] { "a" }, FieldBinding.ChangedKeys(page, baseline).ToArray());
        });

        [Fact]
        public void A_list_file_nobody_changed_is_not_written_and_a_damaged_one_is_kept()
        {
            string folder = Folder();
            string settingsPath = Path.Combine(folder, "settings.json");
            string listPath = Path.Combine(folder, "words.json");
            var schema = new[] { new Setting { Key = "words", Kind = "list", File = "words.json" } };

            File.WriteAllText(listPath, @"[ { ""Word"": ""from the game"" } ]");
            var untouched = JObject.Parse(@"{ ""words"": [ { ""Word"": ""old window copy"" } ], ""x"": 1 }");
            var main = Sidecars.Split(untouched, schema, settingsPath, new HashSet<string> { "x" });
            Assert.Null(main["words"]);
            Assert.Contains("from the game", File.ReadAllText(listPath));   // not written over

            File.WriteAllText(listPath, @"[ { ""Word"": ""broken"", } ");
            Sidecars.Split(JObject.Parse(@"{ ""words"": [ { ""Word"": ""new"" } ] }"), schema, settingsPath, new HashSet<string> { "words" });
            Assert.True(File.Exists(listPath + ".broken"));
            Assert.Contains("broken", File.ReadAllText(listPath + ".broken"));
            Assert.Contains("new", File.ReadAllText(listPath));
        }

        [Fact]
        public void A_list_file_gets_its_bak_like_the_settings_file()
        {
            string folder = Folder();
            string listPath = Path.Combine(folder, "words.json");
            File.WriteAllText(listPath, @"[ 1, 2 ]");
            Assert.True(SettingsStore.IsReadable(listPath));
            SettingsStore.WriteAllTextAtomic(listPath, "[ 3 ]");
            Assert.True(File.Exists(listPath + ".bak"));
        }

        [Theory]
        [InlineData(0.005, 1.0, "0.005")]
        [InlineData(0.001, 1.0, "0.001")]
        [InlineData(0.12345, 0.01, "12.345")]
        [InlineData(0.1, 0.01, "10")]
        public void A_double_shows_its_decimals(double stored, double factor, string shown)
        {
            var tag = new FieldTag("x", FieldKind.Double) { Factor = factor };
            Assert.Equal(shown, FieldBinding.Display(tag, new JValue(stored)));
        }
    }
}
