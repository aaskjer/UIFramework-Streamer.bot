using System;
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
    /// <summary>1.10.0: the customise mode moves what the page shows, one place at a time.</summary>
    public class Fixes1100CustomizeTests
    {
        private static void Sta(Action body)
        {
            Exception failure = null;
            var thread = new Thread(() => { try { body(); } catch (Exception ex) { failure = ex; } });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            if (failure != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
        }

        private static string Folder()
        {
            string folder = Path.Combine(Path.GetTempPath(), "UIFramework.Tests", "customize-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            return folder;
        }

        private sealed class QuietLog : IHostLog
        {
            public void Info(string message) { }
            public void Warn(string message) { }
            public void Error(string message) { }
        }

        /// <summary>A window in the customise mode, its settings file in a folder of its own.</summary>
        private static WindowSpec Customising(Schema schema)
        {
            var spec = Entry.SpecFor(null, schema, Path.Combine(Folder(), "settings.json"));
            spec.Customizing = true;
            Entry.Prepare(spec, schema);
            return spec;
        }

        /// <summary>An entry of a field's menu in the customise mode, by its text.</summary>
        private static MenuItem MenuEntry(WindowSpec spec, string key, string pageId, string text)
        {
            var ctx = Entry.ContextFor(spec, new JObject(), new WindowParts(), new QuietLog(), null);
            var box = (Border)Customize.Wrap(ctx, spec.ResolvedSettings.First(s => s.Key == key), new TextBlock(), pageId);
            return box.ContextMenu.Items.OfType<MenuItem>().First(i => (string)i.Header == text);
        }

        private static void Click(MenuItem item) => item.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));

        // ---- What another page draws is not a field of this one ---------------------------------------------

        [Fact]
        public void Move_up_and_down_pass_over_what_the_Messages_page_draws_elsewhere() => Sta(() =>
        {
            // Blackjack's Messages page: its sender switches are declared among the templates, and the Messages page
            // draws them under the picker, not in the customise mode's list.
            var messages = Schema.Parse(@"{ ""host"": ""demo"", ""settings"": [
                { ""key"": ""m1"", ""kind"": ""template"", ""page"": ""messages"", ""default"": ""one"", ""group"": ""Game"", ""label"": ""One"" },
                { ""key"": ""m2"", ""kind"": ""template"", ""page"": ""messages"", ""default"": ""two"", ""group"": ""Game"", ""label"": ""Two"" },
                { ""key"": ""useBot"", ""default"": true, ""page"": ""messages"" },
                { ""key"": ""sendAction"", ""default"": false, ""page"": ""messages"" },
                { ""key"": ""m3"", ""kind"": ""template"", ""page"": ""messages"", ""default"": ""three"", ""group"": ""Game"", ""label"": ""Three"" } ],
                ""window"": { ""title"": ""Demo"" } }");
            var spec = Customising(messages);
            Click(MenuEntry(spec, "m2", "messages", Text.T("cust.moveDown")));
            Entry.Prepare(spec, messages);
            // One press, one place: before, it went past useBot and needed two more to pass m3.
            Assert.Equal(new[] { "m1", "m3", "m2" }, spec.Messages.Select(m => m.Key).ToArray());

            // High-Low's Cooldowns page: the cooldown senders are on it by the conventions, drawn on the Messages page.
            var cooldowns = Schema.Parse(@"{ ""host"": ""demo"", ""settings"": [
                { ""key"": ""cooldownUseBot"", ""default"": true },
                { ""key"": ""a"", ""default"": 1, ""page"": ""cooldowns"" },
                { ""key"": ""b"", ""default"": 2, ""page"": ""cooldowns"" },
                { ""key"": ""cooldownSendAction"", ""default"": false, ""section"": ""Chat"" },
                { ""key"": ""msgWait"", ""kind"": ""template"", ""page"": ""messages"", ""default"": ""wait"", ""group"": ""Cooldown"" } ],
                ""window"": { ""title"": ""Demo"" } }");
            spec = Customising(cooldowns);
            Assert.Equal("cooldowns", spec.ResolvedSettings.First(s => s.Key == "cooldownUseBot").Page);
            Assert.False(MenuEntry(spec, "a", "cooldowns", Text.T("cust.moveUp")).IsEnabled);     // the first the page shows
            var down = MenuEntry(spec, "b", "cooldowns", Text.T("cust.moveDown"));
            Assert.False(down.IsEnabled);                                                        // and the last
            Click(down);
            // Nothing to move past: nothing written, and no section taken from a switch the page does not show.
            Assert.Null(LayoutFile.Load(spec.SettingsPath).Field("b"));
        });
    }
}
