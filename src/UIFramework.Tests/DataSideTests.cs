using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows.Controls;
using Newtonsoft.Json.Linq;
using Xunit;

namespace UIFramework.Tests
{
    /// <summary>Values as the settings file holds them: the same on every Windows, a bad one never breaking a page,
    /// limits kept, unknown options kept; and the conditions and spellings around them.</summary>
    public class DataSideTests
    {
        private static void Sta(System.Action body, string culture = null)
        {
            System.Exception failure = null;
            var thread = new Thread(() => { try { body(); } catch (System.Exception ex) { failure = ex; } });
            thread.SetApartmentState(ApartmentState.STA);
            if (culture != null)
            {
                thread.CurrentCulture = new CultureInfo(culture);
                thread.CurrentUICulture = new CultureInfo(culture);
            }
            thread.Start();
            thread.Join();
            if (failure != null) throw failure;
        }

        private static (Fields Fields, WindowParts Parts, JObject Settings) Make(string settingsJson, string defaultsJson)
        {
            var parts = new WindowParts();
            var defaults = JObject.Parse(defaultsJson);
            var store = new SettingsStore(Path.GetTempFileName(), defaults);
            var settings = store.Merged(JObject.Parse(settingsJson));
            return (new Fields(parts, settings, defaults, _ => { }), parts, settings);
        }

        private static WindowSpec Spec(string settings, string name, string layout = null, string sections = "")
        {
            string folder = Path.Combine(Path.GetTempPath(), "UIFramework.Tests", name + "-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            if (layout != null) File.WriteAllText(Path.Combine(folder, "uiframework.layout.json"), layout);
            var schema = Schema.Parse(JObject.Parse(@"{ ""host"": ""demo"", ""version"": ""v1"", ""settingsFile"": ""x"", ""settings"": [ " + settings + @" ],
                ""window"": { ""title"": ""T"", ""pages"": [ { ""id"": ""game"", ""fields"": [""*""] } ], ""sections"": [ " + sections + @" ] } }"));
            var spec = Entry.SpecFor(null, schema, Path.Combine(folder, "settings.json"));
            Entry.Prepare(spec, schema);
            return spec;
        }

        [Fact]
        public void Numbers_read_and_write_with_a_point_on_a_German_system() => Sta(() =>
        {
            Assert.Equal("0.5", SettingOption.Parse(new JValue(0.5)).Value);
            Assert.Equal("0.5", FieldBinding.Invariant(new JValue(0.5)));
            var (f, parts, settings) = Make(@"{ ""rate"": 0.25 }", @"{ ""rate"": 0.5, ""name"": """" }");
            Assert.Equal("0.25", f.String("rate"));
            var page = new StackPanel();
            page.Children.Add(f.StringComboField("Rate", "rate", new[] { ("0.25", "Quarter"), ("0.5", "Half") }, ""));
            Assert.Equal("0.25", (string)((ComboBoxItem)parts.Combos["rate"].SelectedItem).Tag);

            var host = UiSettings.Parse(@"{ ""rate"": 0.5, ""text"": ""0,75"", ""huge"": 1e30, ""neg"": -1e30 }");
            Assert.Equal("0.5", host.String("rate"));
            Assert.Equal(0.75, host.Double("text"), 6);
            Assert.Equal(long.MaxValue, host.Long("huge"));
            Assert.Equal(long.MinValue, host.Long("neg"));
        }, "de-DE");

        [Fact]
        public void A_value_that_is_no_number_shows_the_default_and_text_numbers_are_read() => Sta(() =>
        {
            var (f, parts, settings) = Make(@"{ ""n"": ""abc"", ""d"": ""0,5"", ""arr"": [1, 2], ""big"": 1e30 }",
                @"{ ""n"": 7, ""d"": 1.0, ""arr"": 3, ""big"": 0 }");
            var page = new StackPanel();
            page.Children.Add(f.IntField("N", "n", ""));
            page.Children.Add(f.DoubleField("D", "d", ""));
            page.Children.Add(f.IntField("Arr", "arr", ""));
            page.Children.Add(f.LongField("Big", "big", ""));
            Assert.Equal("7", parts.Boxes["n"].Text);
            Assert.Equal("0.5", parts.Boxes["d"].Text);
            Assert.Equal("3", parts.Boxes["arr"].Text);
            Assert.Equal(long.MaxValue.ToString(CultureInfo.InvariantCulture), parts.Boxes["big"].Text);
            Assert.Equal(long.MaxValue, f.Long("big"));
            Assert.Equal(0, f.Long("n"));
            Assert.Equal("", FieldBinding.Display(new FieldTag("x", FieldKind.Double), new JArray(1)));
        });

        [Fact]
        public void A_fraction_in_a_whole_number_field_is_shown_and_named_not_cut() => Sta(() =>
        {
            var (f, parts, settings) = Make(@"{ ""n"": 2.5 }", @"{ ""n"": 1 }");
            var page = new StackPanel();
            page.Children.Add(f.IntField("N", "n", ""));
            Assert.Equal("2.5", parts.Boxes["n"].Text);
            Assert.Contains(FieldBinding.InvalidNumberFields(page), line => line.StartsWith("N:"));
        });

        [Fact]
        public void Min_and_max_hold_for_whole_numbers_and_the_box_shows_what_was_saved() => Sta(() =>
        {
            var (f, parts, settings) = Make("{}", @"{ ""n"": 5, ""l"": 5, ""neg"": 0 }");
            var page = new StackPanel();
            page.Children.Add(f.IntField("N", "n", ""));
            page.Children.Add(f.LongField("L", "l", ""));
            page.Children.Add(f.IntField("Neg", "neg", ""));
            var n = (FieldTag)parts.Boxes["n"].Tag; n.Min = 1; n.Max = 100;
            var l = (FieldTag)parts.Boxes["l"].Tag; l.Min = 10; l.Max = 1000;
            var neg = (FieldTag)parts.Boxes["neg"].Tag; neg.Min = -50; neg.Max = 50;
            parts.Boxes["n"].Text = "500";
            parts.Boxes["l"].Text = "3";
            parts.Boxes["neg"].Text = "-20";
            Assert.Empty(FieldBinding.InvalidNumberFields(page));
            FieldBinding.Save(settings, page);
            Assert.Equal(100, (int)settings["n"]);
            Assert.Equal(10L, (long)settings["l"]);
            Assert.Equal(-20, (int)settings["neg"]);
            Assert.Equal("100", parts.Boxes["n"].Text);
            Assert.Equal("10", parts.Boxes["l"].Text);

            Assert.True(FieldBinding.AllowsNegative(neg));
            Assert.False(FieldBinding.AllowsNegative(new FieldTag("x", FieldKind.Int)));   // no min declared: digits only
            Assert.True(FieldBinding.IsWhole("-3", true));
            Assert.False(FieldBinding.IsWhole("-3", false));
            Assert.False(FieldBinding.IsWhole("-", true));
        });

        [Fact]
        public void Min_and_max_reach_int_long_and_duration_fields_of_a_schema()
        {
            var spec = Spec(@"{ ""key"": ""big"", ""default"": 5000000000, ""kind"": ""int"" },
                             { ""key"": ""small"", ""default"": 5 },
                             { ""key"": ""huge"", ""default"": 9000000000 }", "kinds");
            var byKey = spec.ResolvedSettings.ToDictionary(s => s.Key);
            Assert.Equal("long", byKey["big"].Kind);     // declared int, but its default does not fit one
            Assert.Equal("int", byKey["small"].Kind);
            Assert.Equal("long", byKey["huge"].Kind);
            Assert.Equal("long", Setting.KindOf(new JValue(5000000000L)));
        }

        [Fact]
        public void Typed_decimals_that_could_mean_thousands_are_refused()
        {
            Assert.False(FieldBinding.TryParseTyped("1,000", out _));
            Assert.False(FieldBinding.TryParseTyped("1.000,5", out _));
            Assert.True(FieldBinding.TryParseTyped("1,5", out double a)); Assert.Equal(1.5, a, 6);
            Assert.True(FieldBinding.TryParseTyped("0,500", out double b)); Assert.Equal(0.5, b, 6);
            Assert.True(FieldBinding.TryParseTyped("1000.25", out double c)); Assert.Equal(1000.25, c, 6);
            Assert.True(FieldBinding.TryNumber(new JValue("0,5"), out double d)); Assert.Equal(0.5, d, 6);
            Assert.False(FieldBinding.TryNumber(new JValue("abc"), out _));
        }

        [Fact]
        public void A_stored_value_the_options_lack_is_kept_selected_and_not_a_change() => Sta(() =>
        {
            var (f, parts, settings) = Make(@"{ ""mode"": ""legacy"", ""count"": 7, ""stop"": ""c"" }", @"{ ""mode"": ""a"", ""count"": 1, ""stop"": ""a"" }");
            var page = new StackPanel();
            page.Children.Add(f.StringComboField("Mode", "mode", new[] { ("a", "A"), ("b", "B") }, ""));
            page.Children.Add(f.IntCombo("Count", "count", new[] { (1, "One"), (2, "Two") }, ""));
            page.Children.Add(f.SliderField("Stop", "stop", "", new List<SettingOption> { new SettingOption { Value = "a", Label = "A" }, new SettingOption { Value = "b", Label = "B" } }, 0, 1, 1, true, null));
            Assert.Equal("legacy", (string)((ComboBoxItem)parts.Combos["mode"].SelectedItem).Tag);
            Assert.Equal(7, (int)((ComboBoxItem)parts.Combos["count"].SelectedItem).Tag);
            var baseline = new Dictionary<object, string>();
            FieldBinding.Snapshot(page, baseline);
            Assert.Equal(0, FieldBinding.CountChanged(page, baseline));
            var saved = new JObject();
            FieldBinding.Save(saved, page);
            Assert.Equal("legacy", (string)saved["mode"]);
            Assert.Equal(7, (int)saved["count"]);
            Assert.Equal("c", (string)saved["stop"]);

            var slider = (OptionSlider)parts.Sliders["stop"];
            Assert.True(slider.IsKept);
            slider.Value = 1;                            // moved: the stop it sits on is the value now
            Assert.False(slider.IsKept);
            FieldBinding.Save(saved, page);
            Assert.Equal("b", (string)saved["stop"]);
        });

        [Fact]
        public void A_number_column_keeps_its_value_when_the_cell_is_no_number() => Sta(() =>
        {
            var columns = new List<SettingColumn>
            {
                new SettingColumn { Key = "name", Kind = "string" }, new SettingColumn { Key = "wins", Kind = "int" },
                new SettingColumn { Key = "rate", Kind = "double" }, new SettingColumn { Key = "note", Kind = "text" }
            };
            var table = new ListTable("Rows", columns, new JArray(), () => { });
            table.Set(new JArray(new JObject { ["name"] = "a", ["wins"] = 3L, ["rate"] = 0.5, ["note"] = "" }));
            var records = (System.Collections.IList)typeof(ListTable).GetField("_records", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(table);
            var record = records[0];
            var indexer = record.GetType().GetProperty("Item");
            indexer.SetValue(record, "abc", new object[] { "wins" });
            indexer.SetValue(record, "1,000", new object[] { "rate" });
            Assert.Equal(3L, (long)table.Value[0]["wins"]);
            Assert.Equal(0.5, (double)table.Value[0]["rate"], 6);
            indexer.SetValue(record, "-4", new object[] { "wins" });
            indexer.SetValue(record, "0,25", new object[] { "rate" });
            Assert.Equal(-4L, (long)table.Value[0]["wins"]);
            Assert.Equal(0.25, (double)table.Value[0]["rate"], 6);
        });

        [Fact]
        public void ReadOnly_is_read_in_both_spellings()
        {
            Assert.True(Setting.Parse(JObject.Parse(@"{ ""key"": ""a"", ""default"": """", ""readOnly"": true }")).ReadOnly);
            Assert.True(Setting.Parse(JObject.Parse(@"{ ""key"": ""a"", ""default"": """", ""readonly"": true }")).ReadOnly);
            var written = Setting.Parse(JObject.Parse(@"{ ""key"": ""a"", ""default"": """", ""readonly"": true }")).ToJson();
            Assert.True((bool)written["readOnly"]);
            var list = Setting.Parse(JObject.Parse(@"{ ""key"": ""l"", ""default"": [], ""columns"": [ { ""key"": ""w"", ""kind"": ""int"", ""readonly"": true } ] }"));
            Assert.True(list.Columns[0].ReadOnly);
        }

        [Fact]
        public void The_host_block_carries_readOnly_step_group_and_flags_without_a_label()
        {
            var item = UiSchema.ToJson(UiSchema.S("status", "", readOnly: true, step: 0.5, group: "Status"));
            Assert.True((bool)item["readOnly"]);
            Assert.Equal(0.5, (double)item["step"], 6);
            Assert.Equal("Status", (string)item["group"]);

            var list = UiSchema.ToJson(UiSchema.S("rows", new object[0], columns: new[] { "wins|int|readonly", "face|emote|twitch", "name|string|Player name", "x|string|a | b|kick" }));
            var columns = (JArray)list["columns"];
            Assert.Equal("wins", (string)columns[0]["label"]);
            Assert.True((bool)columns[0]["readOnly"]);
            Assert.Equal("face", (string)columns[1]["label"]);
            Assert.Equal("twitch", (string)columns[1]["platform"]);
            Assert.Equal("Player name", (string)columns[2]["label"]);
            Assert.Equal("a | b", (string)columns[3]["label"]);
            Assert.Equal("kick", (string)columns[3]["platform"]);
        }

        [Fact]
        public void A_null_sender_hides_its_switch_and_a_missing_one_keeps_the_default()
        {
            var spec = SpecJson.Parse(@"{ ""title"": ""Demo"", ""senders"": { ""useBot"": null, ""sendAction"": null, ""cooldownUseBot"": null,
                ""cooldownSendAction"": null, ""cooldownGroup"": null } }");
            Assert.Null(spec.Senders.UseBot);
            Assert.Null(spec.Senders.SendAction);
            Assert.Null(spec.Senders.CooldownUseBot);
            Assert.Null(spec.Senders.CooldownSendAction);
            Assert.Null(spec.Senders.CooldownGroup);
            var kept = SpecJson.Parse(@"{ ""title"": ""Demo"", ""senders"": { ""sendAction"": ""Act"" } }");
            Assert.Equal("useBot", kept.Senders.UseBot);
            Assert.Equal("Act", kept.Senders.SendAction);
        }

        [Fact]
        public void Not_and_a_hidden_gate_read_from_the_saved_value() => Sta(() =>
        {
            var parts = Requirement.Parse("!useBot && name && n>=3 && op=<=");
            Assert.True(parts[0].Not);
            Assert.Equal("useBot", parts[0].Key);
            Assert.Equal("op", parts[3].Key);
            Assert.Equal(new[] { "<=" }, parts[3].Values);

            var (f, window, settings) = Make(@"{ ""useBot"": false, ""name"": """", ""n"": 2 }", @"{ ""useBot"": true, ""name"": ""x"", ""n"": 5 }");
            // None of these is on the window: their saved values decide.
            Assert.True(Requirement.Holds(f, window, Requirement.Parse("!useBot")[0]));
            Assert.False(Requirement.Holds(f, window, Requirement.Parse("useBot")[0]));
            Assert.True(Requirement.Holds(f, window, Requirement.Parse("!name")[0]));
            Assert.False(Requirement.Holds(f, window, Requirement.Parse("n>=3")[0]));
            Assert.True(Requirement.Holds(f, window, Requirement.Parse("ghost=1")[0]));   // not a setting at all: holds
            bool? shown = null;
            Requirement.Wire(f, window, "n>=3", on => shown = on);
            Assert.False(shown);

            var page = new StackPanel();
            page.Children.Add(f.Toggle("Use bot", "useBot", ""));
            Assert.True(Requirement.Holds(f, window, Requirement.Parse("!useBot")[0]));
            window.Switches["useBot"].IsChecked = true;
            Assert.False(Requirement.Holds(f, window, Requirement.Parse("!useBot")[0]));
        });

        [Fact]
        public void A_field_moved_to_another_section_takes_that_sections_condition()
        {
            var spec = Spec(@"{ ""key"": ""source"", ""default"": ""a"", ""kind"": ""options"", ""options"": [ ""a"", ""igdb"" ], ""section"": ""Where"" },
                             { ""key"": ""clientId"", ""default"": """", ""section"": ""IGDB access"" },
                             { ""key"": ""other"", ""default"": """", ""section"": ""Where"" }",
                "moved", @"{ ""fields"": { ""clientId"": { ""section"": ""Where"" }, ""other"": { ""section"": ""IGDB access"" } } }",
                @"{ ""name"": ""IGDB access"", ""showWhen"": ""source=igdb"" }");
            var byKey = spec.ResolvedSettings.ToDictionary(s => s.Key);
            Assert.Null(byKey["clientId"].SectionShowWhen);
            Assert.Equal("source=igdb", byKey["other"].SectionShowWhen);
        }
    }
}
