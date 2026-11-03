using System.Collections.Generic;
using System.Linq;
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
        public void LiveValuesSetSwitchesCombosAndBoxes() => Sta(() =>
        {
            var (f, parts, _, _) = Make("{}", @"{ ""slots"": 4, ""mode"": ""cost"", ""on"": false, ""bet"": 10, ""rate"": 1.5 }");
            var page = new StackPanel();
            page.Children.Add(f.IntCombo("Slots", "slots", new[] { (3, "3"), (4, "4"), (5, "5") }, ""));
            page.Children.Add(f.StringComboField("Mode", "mode", new[] { ("cost", "Cost"), ("fixed", "Fixed") }, ""));
            page.Children.Add(f.Toggle("On", "on", ""));
            page.Children.Add(f.IntField("Bet", "bet", ""));
            page.Children.Add(f.DoubleField("Rate", "rate", ""));
            LiveValues.Apply(parts, JObject.Parse(@"{ ""slots"": 5, ""mode"": ""fixed"", ""on"": true, ""bet"": 250, ""rate"": 2.25, ""nothing"": 1 }"));
            Assert.Equal(5, (int)((ComboBoxItem)parts.Combos["slots"].SelectedItem).Tag);
            Assert.Equal("fixed", (string)((ComboBoxItem)parts.Combos["mode"].SelectedItem).Tag);
            Assert.True(parts.Switches["on"].IsChecked);
            Assert.Equal("250", parts.Boxes["bet"].Text);
            Assert.Equal("2.25", parts.Boxes["rate"].Text);
            Assert.NotNull(ResultPanel.TryParse(@"{ ""set"": {} }"));
            Assert.Null(ResultPanel.TryParse("plain text"));
        });

        [Fact]
        public void SlidersSaveSetFollowRequirementsAndTakeImports() => Sta(() =>
        {
            var (f, parts, _, _) = Make("{}", @"{ ""fairness"": ""fair"", ""level"": 3, ""bet"": 10 }");
            var page = new StackPanel();
            var stops = new List<SettingOption>
            {
                new SettingOption { Value = "strict", Label = "Strict" },
                new SettingOption { Value = "fair", Label = "Fair" },
                new SettingOption { Value = "custom", Label = "Custom" }
            };
            page.Children.Add(f.SliderField("Fairness", "fairness", "", stops, 0, 0, 1, true, null));
            page.Children.Add(f.SliderField("Level", "level", "", null, 1, 5, 1, true, null));
            page.Children.Add(f.IntField("Bet", "bet", ""));
            Assert.Equal(1, parts.Sliders["fairness"].Index);
            Assert.Equal(3.0, parts.Sliders["level"].Value);

            bool custom = false;
            Requirement.Wire(f, parts, "fairness=custom", v => custom = v);
            Assert.False(custom);
            LiveValues.Apply(parts, JObject.Parse(@"{ ""fairness"": ""custom"", ""level"": 9 }"));
            Assert.True(custom);
            Assert.Equal("custom", f.LiveText("fairness", ""));
            Assert.Equal(5.0, f.LiveNumber("level", 0));   // clamped to the range

            var saved = new JObject();
            FieldBinding.Save(saved, page);
            Assert.Equal("custom", (string)saved["fairness"]);
            Assert.Equal(5, (int)saved["level"]);

            // An import puts only the keys it names into the controls, and says which it placed.
            int changes = 0;
            FieldBinding.HookChangeTracking(page, () => changes++);
            var placed = FieldBinding.Apply(page, JObject.Parse(@"{ ""level"": 2, ""bet"": 250, ""nothing"": 1 }"), true);
            Assert.Equal(new[] { "level", "bet" }, placed.OrderByDescending(k => k).ToArray());
            Assert.Equal(2, changes);
            Assert.Equal(2.0, parts.Sliders["level"].Value);
            Assert.Equal("250", parts.Boxes["bet"].Text);
            Assert.Equal(1, parts.Sliders["fairness"].Index == 2 ? 1 : 0);

            // Reset to defaults reaches the slider too.
            FieldBinding.ResetToDefaults(page, JObject.Parse(@"{ ""fairness"": ""strict"", ""level"": 4, ""bet"": 10 }"));
            Assert.Equal(0, parts.Sliders["fairness"].Index);
            Assert.Equal(4.0, parts.Sliders["level"].Value);
        });

        [Fact]
        public void ResultPanelDrawsEmoteLinesAsPictures() => Sta(() =>
        {
            var panel = new StackPanel();
            ResultPanel.Render(panel, @"{ ""lines"": [ ""alice spins"", { ""emotes"": [""🍇"", ""🍈"", ""🍇""], ""platform"": ""youtube"", ""text"": ""The slots"" }, ""No match"" ] }");
            Assert.Equal(4, panel.Children.Count);
            var pictures = Assert.IsType<WrapPanel>(panel.Children[2]);
            Assert.Equal(3, pictures.Children.Count);
            var cell = Assert.IsType<StackPanel>(pictures.Children[0]);
            Assert.Equal("🍇", ((TextBlock)cell.Children[0]).Text);
            Assert.Equal("🍇", ((TextBlock)cell.Children[1]).Text);

            panel.Children.Clear();
            ResultPanel.Render(panel, @"{ ""lines"": [ { ""emotes"": [""[emote:1730803:emojiRich]""], ""platform"": ""kick"" } ] }");
            var kick = Assert.IsType<StackPanel>(((WrapPanel)panel.Children[0]).Children[0]);
            Assert.IsType<System.Windows.Controls.Image>(kick.Children[0]);
            Assert.Equal("emojiRich", ((TextBlock)kick.Children[1]).Text);
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
