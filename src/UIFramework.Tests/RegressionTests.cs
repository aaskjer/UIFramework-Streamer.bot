using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using Newtonsoft.Json.Linq;
using Xunit;

namespace UIFramework.Tests
{
    /// <summary>What the second look at the fix rounds found: whole numbers past a double, repairs beyond number
    /// boxes, a hidden box without a number, lists in files of their own on Reset all and an unreadable list file.</summary>
    public class RegressionTests
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

        [Fact]
        public void A_whole_number_past_two_to_the_53_stays_exact() => Sta(() =>
        {
            var parts = new WindowParts();
            var settings = JObject.Parse(@"{ ""id"": 1234567890123456789 }");
            var fields = new Fields(parts, settings, JObject.Parse(@"{ ""id"": 0 }"), _ => { });
            var page = new StackPanel();
            page.Children.Add(fields.LongField("Id", "id", ""));
            Assert.Equal("1234567890123456789", parts.Boxes["id"].Text);
            Assert.Equal(1234567890123456789L, fields.Long("id"));
            var saved = new JObject();
            FieldBinding.Save(saved, page);
            Assert.Equal(1234567890123456789L, (long)saved["id"]);
            Assert.Empty(FieldBinding.Repaired(settings, page));
        });

        [Fact]
        public void A_switch_and_a_dropdown_shown_other_than_stored_are_repairs_too() => Sta(() =>
        {
            var parts = new WindowParts();
            var settings = JObject.Parse(@"{ ""on"": ""yes"", ""mode"": 2, ""fine"": true }");
            var fields = new Fields(parts, settings, JObject.Parse(@"{ ""on"": false, ""mode"": ""a"", ""fine"": false }"), _ => { });
            var page = new StackPanel();
            page.Children.Add(fields.Toggle("On", "on", ""));
            page.Children.Add(fields.Toggle("Fine", "fine", ""));
            page.Children.Add(fields.StringComboField("Mode", "mode", new[] { ("a", "A"), ("2", "Two") }, ""));
            // "yes" is no switch value (shown off); 2 and "2" are the same; true is true.
            Assert.Equal(new[] { "on" }, FieldBinding.Repaired(settings, page).ToArray());
        });

        [Fact]
        public void A_hidden_number_box_without_a_number_keeps_the_stored_value() => Sta(() =>
        {
            var parts = new WindowParts();
            var settings = JObject.Parse(@"{ ""n"": 7 }");
            var fields = new Fields(parts, settings, JObject.Parse(@"{ ""n"": 1 }"), _ => { });
            // As in the window: the page host holds the page, the page the field's holder.
            var host = new Grid();
            var page = new StackPanel();
            var holder = new StackPanel();
            holder.Children.Add(fields.IntField("N", "n", ""));
            page.Children.Add(holder);
            host.Children.Add(page);
            parts.Boxes["n"].Text = "";
            holder.Visibility = Visibility.Collapsed;   // hidden by a showWhen
            Assert.Empty(FieldBinding.InvalidNumberFields(host));
            var saved = (JObject)settings.DeepClone();
            FieldBinding.Save(saved, host);
            Assert.Equal(7, (int)saved["n"]);
        });

        [Fact]
        public void Reset_all_leaves_a_list_in_its_own_file_and_a_list_still_in_the_settings_file_moves_there()
        {
            string folder = Path.Combine(Path.GetTempPath(), "UIFramework.Tests", "reset-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            string settingsPath = Path.Combine(folder, "settings.json");
            var schema = new[] { new Setting { Key = "words", Kind = "list", File = "words.json", Default = new JArray() }, new Setting { Key = "n" } };
            var defaults = JObject.Parse(@"{ ""words"": [], ""n"": 1 }");
            var current = JObject.Parse(@"{ ""words"": [ ""kept"" ], ""n"": 5 }");
            var fresh = SettingsStore.ResetShown(current, defaults, schema, new HashSet<string> { "words", "n" });
            Assert.Equal(1, (int)fresh["n"]);
            Assert.Equal("kept", (string)fresh["words"][0]);
            // The theme move on open and Reset all write the settings file this way: nothing changed, the list moves.
            var main = Sidecars.Split(fresh, schema, settingsPath, new HashSet<string>());
            Assert.Null(main["words"]);
            Assert.Contains("kept", File.ReadAllText(Path.Combine(folder, "words.json")));
        }

        [Fact]
        public void A_list_file_that_cannot_be_read_is_named()
        {
            string folder = Path.Combine(Path.GetTempPath(), "UIFramework.Tests", "unreadable-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, "words.json"), "[ broken");
            var schema = new[] { new Setting { Key = "words", Kind = "list", File = "words.json" } };
            var unreadable = new HashSet<string>();
            Sidecars.Load(new JObject(), schema, Path.Combine(folder, "settings.json"), null, "", unreadable);
            Assert.Equal(new[] { "words" }, unreadable.ToArray());
        }
    }
}
