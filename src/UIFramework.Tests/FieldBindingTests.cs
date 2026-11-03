using System.Collections.Generic;
using System.Threading;
using System.Windows.Controls;
using Newtonsoft.Json.Linq;
using Xunit;

namespace UIFramework.Tests
{
    /// <summary>WPF controls need an STA thread; each test builds its tree on one.</summary>
    public class FieldBindingTests
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

        private static (Fields Fields, WindowParts Parts, JObject Settings, JObject Defaults) Make(string settingsJson, string defaultsJson)
        {
            var parts = new WindowParts();
            var defaults = JObject.Parse(defaultsJson);
            var store = new SettingsStore(System.IO.Path.GetTempFileName(), defaults);
            var settings = store.Merged(JObject.Parse(settingsJson));
            return (new Fields(parts, settings, defaults, _ => { }), parts, settings, defaults);
        }

        [Fact]
        public void SaveWritesEveryKindByKey() => Sta(() =>
        {
            var (f, parts, settings, defaults) = Make("{}", @"{ ""n"": 3, ""big"": 0, ""pct"": 0.25, ""name"": "" x "", ""on"": false, ""mode"": ""a"", ""tpl"": ""hi"" }");
            var page = new StackPanel();
            page.Children.Add(f.IntField("N", "n", ""));
            page.Children.Add(f.LongField("Big", "big", "", unlimitedAtZero: true));
            page.Children.Add(f.PercentField("Pct", "pct", 0.01, ""));
            page.Children.Add(f.TextField("Name", "name", ""));
            page.Children.Add(f.Toggle("On", "on", ""));
            page.Children.Add(f.StringComboField("Mode", "mode", new[] { ("a", "A"), ("b", "B") }, ""));
            page.Children.Add(f.TemplateBox("Tpl", "tpl"));

            parts.Boxes["n"].Text = "12";
            parts.Boxes["big"].Text = "0";
            parts.Boxes["pct"].Text = "40";
            parts.Boxes["name"].Text = "  trimmed  ";
            parts.Switches["on"].IsChecked = true;
            parts.Combos["mode"].SelectedIndex = 1;
            parts.Boxes["tpl"].Text = "  kept as typed ";

            FieldBinding.Save(settings, page);
            Assert.Equal(12, (int)settings["n"]);
            Assert.Equal(long.MaxValue, (long)settings["big"]);
            Assert.Equal(0.4, (double)settings["pct"], 6);
            Assert.Equal("trimmed", (string)settings["name"]);
            Assert.True((bool)settings["on"]);
            Assert.Equal("b", (string)settings["mode"]);
            Assert.Equal("  kept as typed ", (string)settings["tpl"]);
        });

        [Fact]
        public void DisplayShowsStoredValuesTheWayTheyAreTyped() => Sta(() =>
        {
            var (f, parts, _, _) = Make(@"{ ""big"": 9223372036854775807, ""pct"": 0.5 }", @"{ ""big"": 0, ""pct"": 0.1 }");
            f.LongField("Big", "big", "", unlimitedAtZero: true);
            f.PercentField("Pct", "pct", 0.01, "");
            Assert.Equal("0", parts.Boxes["big"].Text);
            Assert.Equal("50", parts.Boxes["pct"].Text);
        });

        [Fact]
        public void ResetPutsDefaultsBackAndChangeCountFollows() => Sta(() =>
        {
            var (f, parts, _, defaults) = Make(@"{ ""n"": 9, ""on"": true }", @"{ ""n"": 3, ""on"": false }");
            var page = new StackPanel();
            page.Children.Add(f.IntField("N", "n", ""));
            page.Children.Add(f.Toggle("On", "on", ""));

            var baseline = new Dictionary<object, string>();
            FieldBinding.Snapshot(page, baseline);
            Assert.Equal(0, FieldBinding.CountChanged(page, baseline));

            FieldBinding.ResetToDefaults(page, defaults);
            Assert.Equal("3", parts.Boxes["n"].Text);
            Assert.False(parts.Switches["on"].IsChecked);
            Assert.Equal(2, FieldBinding.CountChanged(page, baseline));
        });

        [Fact]
        public void InvalidNumbersAreNamedByLabel() => Sta(() =>
        {
            var (f, parts, _, _) = Make("{}", @"{ ""n"": 3, ""d"": 1.5 }");
            var page = new StackPanel();
            page.Children.Add(f.IntField("Decks", "n", ""));
            page.Children.Add(f.DoubleField("Payout", "d", ""));
            parts.Boxes["n"].Text = "";
            parts.Boxes["d"].Text = "1,5";
            var invalid = FieldBinding.InvalidNumberFields(page);
            Assert.Single(invalid);
            Assert.StartsWith("Decks", invalid[0]);
        });

        [Fact]
        public void RequirementsFollowSwitchesCombosAndNumbers() => Sta(() =>
        {
            var (f, parts, settings, defaults) = Make("{}", @"{ ""slots"": 4, ""mode"": ""cost"", ""on"": false, ""bet"": 10 }");
            var page = new StackPanel();
            page.Children.Add(f.IntCombo("Slots", "slots", new[] { (3, "3"), (4, "4"), (5, "5") }, ""));
            page.Children.Add(f.StringComboField("Mode", "mode", new[] { ("cost", "Cost"), ("fixed", "Fixed") }, ""));
            page.Children.Add(f.Toggle("On", "on", ""));
            page.Children.Add(f.IntField("Bet", "bet", ""));
            bool five = false, fixedMode = false, on = false, bigBet = false, notCost = false;
            Requirement.Wire(f, parts, "slots>=5", v => five = v);
            Requirement.Wire(f, parts, "mode=fixed", v => fixedMode = v);
            Requirement.Wire(f, parts, "on", v => on = v);
            Requirement.Wire(f, parts, "bet>100", v => bigBet = v);
            Requirement.Wire(f, parts, "mode!=cost", v => notCost = v);
            Assert.False(five); Assert.False(fixedMode); Assert.False(on); Assert.False(bigBet); Assert.False(notCost);
            parts.Combos["slots"].SelectedIndex = 2;
            parts.Combos["mode"].SelectedIndex = 1;
            parts.Switches["on"].IsChecked = true;
            parts.Boxes["bet"].Text = "500";
            Assert.True(five); Assert.True(fixedMode); Assert.True(on); Assert.True(bigBet); Assert.True(notCost);
        });

        [Fact]
        public void TransientFieldsAreNeitherSavedNorCounted() => Sta(() =>
        {
            var (f, parts, settings, _) = Make(@"{ ""n"": 3 }", @"{ ""n"": 3, ""tryBet"": 1000 }");
            var page = new StackPanel();
            page.Children.Add(f.IntField("Decks", "n", ""));
            var tryBet = f.IntField("Bet to try", "tryBet", "");
            FieldBinding.MarkTransient(tryBet);
            page.Children.Add(tryBet);
            var baseline = new System.Collections.Generic.Dictionary<object, string>();
            FieldBinding.Snapshot(page, baseline);
            parts.Boxes["tryBet"].Text = "500";
            Assert.Equal(0, FieldBinding.CountChanged(page, baseline));
            FieldBinding.Save(settings, page);
            Assert.Equal(1000, (int)settings["tryBet"]);                  // the loaded default, not the typed value
            var live = (JObject)settings.DeepClone();
            FieldBinding.Save(live, page, transientToo: true);
            Assert.Equal(500, (int)live["tryBet"]);
        });

        [Fact]
        public void NameListStoresCommaSeparatedWithoutDuplicates() => Sta(() =>
        {
            var (f, parts, settings, _) = Make(@"{ ""users"": ""alice"" }", @"{ ""users"": """" }");
            var page = new StackPanel();
            page.Children.Add(f.NameList("Users", "users", "hint", ""));
            parts.Boxes["users"].Text = "alice,Bob";
            FieldBinding.Save(settings, page);
            Assert.Equal("alice,Bob", (string)settings["users"]);
            Assert.True(Fields.ContainsName(Fields.SplitNames("alice,Bob"), "bob"));
        });
    }
}
