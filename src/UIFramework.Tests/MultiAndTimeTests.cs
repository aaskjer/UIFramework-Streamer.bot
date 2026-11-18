using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using Xunit;

namespace UIFramework.Tests
{
    /// <summary>Two kinds of setting: <c>multi</c>, several of a fixed set of choices as check boxes, stored as the
    /// ticked values between commas; and <c>time</c>, a time of day stored as "HH:mm".</summary>
    public class MultiAndTimeTests
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

        private const string Head = @"{ ""host"": ""demo"", ""version"": ""v1"", ""settings"": [ ";
        private const string Options = @"""options"": [ ""steam|Steam"", ""epic|Epic Games"", ""gog|GOG"" ]";
        private const string Platforms = @"{ ""key"": ""platforms"", ""default"": """", ""kind"": ""multi"", ""label"": ""Left out"", " + Options + " }";

        private static WindowSpec Prepared(string schemaJson, bool customizing = false)
        {
            try
            {
                var schema = Schema.Parse(schemaJson);
                var spec = Entry.SpecFor(schema, null);
                spec.Customizing = customizing;
                Entry.Prepare(spec, schema);
                return spec;
            }
            finally { Text.Use(Conventions.Default.Lang); }
        }

        private static WindowSpec Settings(params string[] settings) => Prepared(Head + string.Join(", ", settings) + " ] }");

        private static IEnumerable<T> Find<T>(object node) where T : class
        {
            if (node is T hit) yield return hit;
            if (node is DependencyObject d)
                foreach (var child in LogicalTreeHelper.GetChildren(d))
                    foreach (var found in Find<T>(child))
                        yield return found;
        }

        /// <summary>The texts under an element, in order: a block's runs where it has some (a line with links).</summary>
        private static string Shown(object node) => string.Join(" | ", Find<TextBlock>(node)
            .Select(t => t.Inlines.Count > 0 ? string.Concat(t.Inlines.OfType<Run>().Select(r => r.Text)) : t.Text).Where(t => !string.IsNullOrEmpty(t)));

        /// <summary>The fields of a schema as a page builds them, over the given stored values.</summary>
        private sealed class Page
        {
            public readonly WindowSpec Spec;
            public readonly WindowParts Parts = new WindowParts();
            public readonly JObject Stored;
            public readonly PageContext Ctx;
            public readonly StackPanel Root = new StackPanel();
            public readonly Dictionary<string, FrameworkElement> Fields = new Dictionary<string, FrameworkElement>();

            public Page(WindowSpec spec, string storedJson = "{}", params string[] only)
            {
                Spec = spec;
                Stored = (JObject)spec.Defaults.DeepClone();
                foreach (var property in JObject.Parse(storedJson).Properties())
                    Stored[property.Name] = property.Value;
                Ctx = Entry.ContextFor(spec, Stored, Parts, null, null);
                foreach (var setting in spec.ResolvedSettings.Where(s => !s.Hidden && (only.Length == 0 || only.Contains(s.Key))))
                {
                    Fields[setting.Key] = SchemaPage.Render(Ctx, setting);
                    Root.Children.Add(Fields[setting.Key]);
                }
                foreach (var wire in Ctx.AfterBuild) wire();
            }

            public TextBox Box(string key) => Parts.Boxes[key];
            public CheckBox Tick(string key, string value) => Find<CheckBox>(Fields[key]).Single(b => (string)b.Tag == value);
            public List<string> Ticked(string key) => Find<CheckBox>(Fields[key]).Where(b => b.IsChecked == true).Select(b => (string)b.Tag).ToList();
            public List<string> Kept(string key) => Find<Border>(Fields[key]).Where(b => Equals(b.Tag, UIFramework.Fields.MultiKeptTag)).Select(Shown).ToList();

            /// <summary>Presses the cross of the chip a value none of the options has shows as.</summary>
            public void TakeOut(string key, string value)
            {
                var chip = Find<Border>(Fields[key]).Single(b => Equals(b.Tag, UIFramework.Fields.MultiKeptTag) && Shown(b) == value);
                Find<Button>(chip).Single().RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            }

            public JObject Saved()
            {
                var saved = (JObject)Stored.DeepClone();
                FieldBinding.Save(saved, Root);
                return saved;
            }
        }

        // ---- multi: what is stored -------------------------------------------------------------------------------

        [Fact]
        public void TheTickedValuesAreReadWhateverTheirCaseAndWrittenInTheOptionsSpellingAndOrder()
        {
            var choices = new[] { "steam", "epic", "gog" };
            Assert.Equal(new[] { "steam", "gog" }, FieldBinding.MultiValues(choices, " GOG , Steam,gog,,"));
            Assert.Equal("steam,gog", FieldBinding.MultiText(choices, " GOG , Steam"));
            Assert.Equal("", FieldBinding.MultiText(choices, " , "));
            Assert.Equal("", FieldBinding.MultiText(choices, null));
            // A value none of the options has is kept, behind those they have, as it is stored.
            Assert.Equal("epic,itch.io,Origin", FieldBinding.MultiText(choices, "itch.io, EPIC ,Origin, ITCH.IO"));
            // An array in a file written by hand is read as its values.
            Assert.Equal("steam,gog", FieldBinding.MultiText(choices, JArray.Parse(@"[ ""gog"", null, ""Steam"", { ""x"": 1 } ]")));
        }

        [Fact]
        public void AMultiFieldIsCheckBoxesOverTheOptionsAndStoresTheTickedValuesAsText() => Sta(() =>
        {
            var page = new Page(Settings(Platforms));
            var boxes = Find<CheckBox>(page.Fields["platforms"]).ToList();
            Assert.Equal(new[] { "Steam", "Epic Games", "GOG" }, boxes.Select(Shown));                 // the label is shown
            Assert.Equal(new[] { "steam", "epic", "gog" }, boxes.Select(b => (string)b.Tag));          // the value is stored
            Assert.All(boxes, b => Assert.Null(FieldTag.Of(b.Tag)));                                   // furniture: the hidden box is the setting
            Assert.IsType<WrapPanel>(boxes[0].Parent);                                                 // several per line
            Assert.Contains("Left out", Shown(page.Fields["platforms"]));
            Assert.Empty(page.Ticked("platforms"));
            Assert.Equal("", (string)page.Saved()["platforms"]);                                       // nothing ticked is ""

            // In the order of the options, whatever the order they were ticked in.
            page.Tick("platforms", "gog").IsChecked = true;
            page.Tick("platforms", "steam").IsChecked = true;
            var saved = page.Saved()["platforms"];
            Assert.Equal(JTokenType.String, saved.Type);                                               // plain text, as a namelist's
            Assert.Equal("steam,gog", (string)saved);
            page.Tick("platforms", "steam").IsChecked = false;
            Assert.Equal("gog", (string)page.Saved()["platforms"]);
            page.Tick("platforms", "gog").IsChecked = false;
            Assert.Equal("", (string)page.Saved()["platforms"]);
        });

        [Fact]
        public void AStoredValueIsTickedWhateverItsCaseAndBlanks() => Sta(() =>
        {
            var page = new Page(Settings(Platforms), @"{ ""platforms"": "" GOG , Steam "" }");
            Assert.Equal(new[] { "steam", "gog" }, page.Ticked("platforms"));
            Assert.Empty(page.Kept("platforms"));
            Assert.Equal("steam,gog", (string)page.Saved()["platforms"]);
            // The file's spelling is put right with the next save, which is nothing to tell the user about.
            Assert.Contains("platforms", FieldBinding.Repaired(page.Stored, page.Root));
            Assert.DoesNotContain("platforms", FieldBinding.Repaired(page.Stored, page.Root, shownOtherwise: true));
            var asStored = new Page(Settings(Platforms), @"{ ""platforms"": ""steam,gog"" }");
            Assert.Empty(FieldBinding.Repaired(asStored.Stored, asStored.Root));
        });

        [Fact]
        public void AStoredValueNoOptionHasIsKeptAndCanBeTakenOut() => Sta(() =>
        {
            var page = new Page(Settings(Platforms), @"{ ""platforms"": ""itch.io, Steam ,origin"" }");
            Assert.Equal(new[] { "steam" }, page.Ticked("platforms"));
            Assert.Equal(new[] { "itch.io", "origin" }, page.Kept("platforms"));                       // shown as chips behind the boxes
            Assert.Equal("steam,itch.io,origin", (string)page.Saved()["platforms"]);                   // kept by a save that leaves the field alone
            var chip = Find<Border>(page.Fields["platforms"]).First(b => Equals(b.Tag, Fields.MultiKeptTag));
            Assert.Equal(Text.T("field.multi.unknown"), chip.ToolTip);
            Assert.Equal(Text.T("field.remove", "itch.io"), Find<Button>(chip).Single().ToolTip);

            // Ticking and unticking leaves it where it is.
            page.Tick("platforms", "gog").IsChecked = true;
            page.Tick("platforms", "steam").IsChecked = false;
            Assert.Equal("gog,itch.io,origin", (string)page.Saved()["platforms"]);

            // Its cross takes it out; the others stay.
            page.TakeOut("platforms", "itch.io");
            Assert.Equal(new[] { "origin" }, page.Kept("platforms"));
            Assert.Equal(new[] { "gog" }, page.Ticked("platforms"));
            Assert.Equal("gog,origin", (string)page.Saved()["platforms"]);
            page.TakeOut("platforms", "origin");
            Assert.Empty(page.Kept("platforms"));
            Assert.Equal("gog", (string)page.Saved()["platforms"]);
        });

        // ---- multi: what every field does -------------------------------------------------------------------------

        [Fact]
        public void ATickCountsAsAnUnsavedChangeAndGoesAgainWhenUndone() => Sta(() =>
        {
            var page = new Page(Settings(Platforms), @"{ ""platforms"": ""steam"" }");
            var baseline = new Dictionary<object, string>();
            FieldBinding.Snapshot(page.Root, baseline);
            int told = 0;
            FieldBinding.HookChangeTracking(page.Root, () => told++);
            Assert.Equal(0, FieldBinding.CountChanged(page.Root, baseline));
            Assert.Contains("platforms", FieldBinding.ShownKeys(page.Root));

            page.Tick("platforms", "epic").IsChecked = true;
            Assert.Equal(1, told);
            Assert.Equal(1, FieldBinding.CountChanged(page.Root, baseline));                           // one field, not one per box
            Assert.Equal(new[] { "platforms" }, FieldBinding.ChangedKeys(page.Root, baseline));
            page.Tick("platforms", "epic").IsChecked = false;
            Assert.Equal(0, FieldBinding.CountChanged(page.Root, baseline));
        });

        [Fact]
        public void ResetPutsTheDefaultBackAndAnImportFollowsTheSameRulesAsTheFile() => Sta(() =>
        {
            var spec = Settings(@"{ ""key"": ""platforms"", ""default"": ""epic,gog"", ""kind"": ""multi"", " + Options + " }");
            var page = new Page(spec, @"{ ""platforms"": ""steam,itch.io"" }");
            FieldBinding.ResetToDefaults(page.Root, spec.Defaults);
            Assert.Equal(new[] { "epic", "gog" }, page.Ticked("platforms"));
            Assert.Empty(page.Kept("platforms"));
            Assert.Equal("epic,gog", (string)page.Saved()["platforms"]);

            // An import: read whatever the case and the blanks, written in the options' spelling, an unknown value kept.
            Assert.Null(FieldBinding.AsSaved(page.Root, "platforms", " GOG ,Steam, itch.io", out var taken));
            Assert.Equal("steam,gog,itch.io", (string)taken);
            Assert.Null(FieldBinding.AsSaved(page.Root, "platforms", "", out taken));
            Assert.Equal("", (string)taken);
            Assert.Null(FieldBinding.AsSaved(page.Root, "platforms", JArray.Parse(@"[ ""GOG"", ""steam"" ]"), out taken));
            Assert.Equal("steam,gog", (string)taken);
            Assert.Equal(Text.T("import.why.text"), FieldBinding.AsSaved(page.Root, "platforms", JObject.Parse(@"{ ""steam"": true }"), out taken));

            // Placed in the field as an import and an answer's `set` place it.
            FieldBinding.Apply(page.Root, new JObject { ["platforms"] = "GOG, origin" }, true);
            Assert.Equal(new[] { "gog" }, page.Ticked("platforms"));
            Assert.Equal(new[] { "origin" }, page.Kept("platforms"));
            Assert.True(LiveValues.Set(page.Parts, "platforms", "Steam"));
            Assert.Equal(new[] { "steam" }, page.Ticked("platforms"));
            Assert.Empty(page.Kept("platforms"));
        });

        [Fact]
        public void TheLiveValuesCarryTheTicksAndATransientFieldIsNeverSaved() => Sta(() =>
        {
            var page = new Page(Settings(Platforms, @"{ ""key"": ""tryWith"", ""default"": ""steam"", ""kind"": ""multi"", ""transient"": true, " + Options + " }"));
            page.Tick("platforms", "epic").IsChecked = true;
            page.Tick("tryWith", "gog").IsChecked = true;
            var live = new JObject();
            FieldBinding.Save(live, page.Root, transientToo: true);
            Assert.Equal("epic", (string)live["platforms"]);
            Assert.Equal("steam,gog", (string)live["tryWith"]);
            Assert.Equal("epic", page.Ctx.Fields.LiveText("platforms", null));
            var saved = new JObject();
            FieldBinding.Save(saved, page.Root);
            Assert.Null(saved["tryWith"]);
            Assert.DoesNotContain("tryWith", FieldBinding.ShownKeys(page.Root));
        });

        [Fact]
        public void AConditionAsksWhetherAValueIsTicked() => Sta(() =>
        {
            var page = new Page(Settings(Platforms,
                @"{ ""key"": ""steamKey"", ""default"": """", ""showWhen"": ""platforms=steam"" }",
                @"{ ""key"": ""storeKey"", ""default"": """", ""showWhen"": ""platforms=steam|gog"" }",
                @"{ ""key"": ""noStore"", ""default"": """", ""showWhen"": ""platforms!=steam|gog"" }",
                @"{ ""key"": ""allOfThem"", ""default"": false, ""requires"": ""!platforms"" }",
                @"{ ""key"": ""someLeftOut"", ""default"": false, ""requires"": ""platforms"" }"));
            Assert.Empty(page.Spec.Problems);
            bool Visible(string key) => page.Fields[key].Visibility == Visibility.Visible;
            bool Enabled(string key) => page.Fields[key].IsEnabled;

            // Nothing ticked: !platforms holds.
            Assert.False(Visible("steamKey"));
            Assert.False(Visible("storeKey"));
            Assert.True(Visible("noStore"));
            Assert.True(Enabled("allOfThem"));
            Assert.False(Enabled("someLeftOut"));

            page.Tick("platforms", "gog").IsChecked = true;
            Assert.False(Visible("steamKey"));
            Assert.True(Visible("storeKey"));                                                          // one of them
            Assert.False(Visible("noStore"));
            Assert.False(Enabled("allOfThem"));
            Assert.True(Enabled("someLeftOut"));

            page.Tick("platforms", "steam").IsChecked = true;
            Assert.True(Visible("steamKey"));                                                          // ticked beside another
            page.Tick("platforms", "gog").IsChecked = false;
            page.Tick("platforms", "steam").IsChecked = false;
            page.Tick("platforms", "epic").IsChecked = true;
            Assert.False(Visible("storeKey"));
            Assert.True(Visible("noStore"));                                                           // none of the two
            Assert.False(Enabled("allOfThem"));
        });

        [Fact]
        public void AConditionOnAMultiFieldTheWindowDoesNotShowReadsItsSavedValueTheSameWay() => Sta(() =>
        {
            var spec = Settings(@"{ ""key"": ""platforms"", ""default"": """", ""kind"": ""multi"", ""hidden"": true, " + Options + " }",
                @"{ ""key"": ""gogKey"", ""default"": """", ""showWhen"": ""platforms=gog"" }",
                @"{ ""key"": ""epicKey"", ""default"": """", ""showWhen"": ""platforms=epic"" }",
                @"{ ""key"": ""noStore"", ""default"": """", ""showWhen"": ""!platforms"" }");
            var page = new Page(spec, @"{ ""platforms"": "" GOG ,itch.io"" }");
            Assert.False(page.Parts.Boxes.ContainsKey("platforms"));
            Assert.Equal(Visibility.Visible, page.Fields["gogKey"].Visibility);                        // in "GOG ,itch.io", not equal to it
            Assert.Equal(Visibility.Collapsed, page.Fields["epicKey"].Visibility);
            Assert.Equal(Visibility.Collapsed, page.Fields["noStore"].Visibility);
            var none = new Page(spec, @"{ ""platforms"": """" }");
            Assert.Equal(Visibility.Collapsed, none.Fields["gogKey"].Visibility);
            Assert.Equal(Visibility.Visible, none.Fields["noStore"].Visibility);
        });

        [Fact]
        public void TheSearchFindsAMultiFieldByItsChoices()
        {
            var entries = new[]
            {
                new SearchEntry { Key = "platforms", Title = "Left out", Also = "Steam Epic Games GOG" },
                new SearchEntry { Key = "other", Title = "Other", Help = "Nothing of the kind." }
            };
            Assert.Equal(new[] { "platforms" }, SearchIndex.Find(entries, "epic").Select(e => e.Key));
            Assert.Equal(new[] { "platforms" }, SearchIndex.Find(entries, "left gog").Select(e => e.Key));
            Assert.Empty(SearchIndex.Find(entries, "itch"));
        }

        [Fact]
        public void AReadOnlyOrLockedMultiFieldShowsTheLabelsOfWhatIsTicked() => Sta(() =>
        {
            var readOnly = new Page(Settings(@"{ ""key"": ""platforms"", ""default"": """", ""kind"": ""multi"", ""label"": ""Left out"", ""readOnly"": true, " + Options + " }"),
                @"{ ""platforms"": ""gog, EPIC,itch.io"" }");
            Assert.Equal("Left out | Epic Games, GOG, itch.io", Shown(readOnly.Fields["platforms"]));
            Assert.Empty(Find<CheckBox>(readOnly.Fields["platforms"]));
            Assert.False(readOnly.Parts.Boxes.ContainsKey("platforms"));                               // shown, never saved
            Assert.DoesNotContain("platforms", FieldBinding.ShownKeys(readOnly.Root));

            var locked = new Page(Prepared(Head + Platforms + @" ], ""lockedFields"": { ""platforms"": ""Set by the bundle."" } }"), @"{ ""platforms"": ""steam"" }");
            string text = Shown(locked.Fields["platforms"]);
            Assert.Contains("Steam", text);
            Assert.Contains(Text.T("field.locked"), text);
            Assert.Contains("Set by the bundle.", text);
            Assert.Empty(Find<CheckBox>(locked.Fields["platforms"]));
        });

        [Fact]
        public void TheCustomiseModeShowsTheFieldWithItsBoxesOutOfReach() => Sta(() =>
        {
            var spec = Prepared(Head + Platforms + @", { ""key"": ""at"", ""default"": ""09:00"", ""kind"": ""time"" } ] }", customizing: true);
            try
            {
                var parts = new WindowParts();
                var stored = (JObject)spec.Defaults.DeepClone();
                stored["platforms"] = "steam";
                var ctx = Entry.ContextFor(spec, stored, parts, null, null);
                var page = SchemaPage.Build(ctx, spec.ResolvedSettings, "game");
                var boxes = Find<CheckBox>(page).Where(b => b.Tag is string).ToList();
                Assert.Equal(3, boxes.Count);
                Assert.All(boxes, b => Assert.False(b.IsEnabled));
                Assert.Equal(new[] { "steam" }, boxes.Where(b => b.IsChecked == true).Select(b => (string)b.Tag));
                Assert.False(parts.Boxes["at"].IsEnabled);
                Assert.Equal("09:00", parts.Boxes["at"].Text);
            }
            finally { Text.Use(Conventions.Default.Lang); }
        });

        // ---- multi: what the red "i" names --------------------------------------------------------------------------

        [Fact]
        public void TheCheckNamesAMultiFieldWithoutOptionsAndADefaultNoOptionHas() => Sta(() =>
        {
            var bare = Settings(@"{ ""key"": ""platforms"", ""default"": """", ""kind"": ""multi"" }");
            Assert.Contains(bare.Problems, p => p.Contains("'platforms' is a multi field without options"));
            Assert.DoesNotContain(bare.Problems, p => p.Contains("does not know"));                    // the kind is one of the framework's
            // Nothing to tick: a text field, which keeps the value as it is.
            var page = new Page(bare, @"{ ""platforms"": ""steam,gog"" }");
            Assert.Empty(Find<CheckBox>(page.Fields["platforms"]));
            Assert.Equal("steam,gog", page.Box("platforms").Text);

            var stray = Settings(@"{ ""key"": ""platforms"", ""default"": ""steam,itch.io"", ""kind"": ""multi"", " + Options + " }");
            Assert.Contains(stray.Problems, p => p.Contains("'platforms'") && p.Contains("no option 'itch.io'"));
            Assert.DoesNotContain(stray.Problems, p => p.Contains("no option 'steam'"));

            Assert.Empty(Settings(@"{ ""key"": ""platforms"", ""default"": ""steam,gog"", ""kind"": ""multi"", " + Options + " }").Problems);
            // The project reads the default as it is declared: one in another spelling or order is named.
            Assert.Contains(Settings(@"{ ""key"": ""platforms"", ""default"": ""GOG, steam"", ""kind"": ""multi"", " + Options + " }").Problems,
                p => p.Contains("'GOG, steam'") && p.Contains("'steam,gog'"));
            Assert.Contains(Settings(@"{ ""key"": ""platforms"", ""default"": [ ""steam"" ], ""kind"": ""multi"", " + Options + " }").Problems,
                p => p.Contains("'platforms'") && p.Contains("default is no text"));
            // A value with a comma cannot be stored between commas: named, and not offered.
            var comma = Settings(@"{ ""key"": ""platforms"", ""default"": """", ""kind"": ""multi"", ""options"": [ ""steam"", ""a,b|A and B"" ] }");
            Assert.Contains(comma.Problems, p => p.Contains("'a,b'") && p.Contains("comma"));
            Assert.Equal(new[] { "steam" }, Find<CheckBox>(new Page(comma).Fields["platforms"]).Select(b => (string)b.Tag));
            // A condition that asks for a value no option has.
            Assert.Contains(Settings(Platforms, @"{ ""key"": ""x"", ""default"": """", ""showWhen"": ""platforms=itch"" }").Problems,
                p => p.Contains("'platforms' has no option 'itch'"));
        });

        // ---- multi: the project's side ------------------------------------------------------------------------------

        // The project's table, for one test: the template's own is empty.
        private static void WithTable(Action test, params UiSchema.Setting[] table)
        {
            var field = typeof(UiSchema).GetField("Table");
            object before = field.GetValue(null);
            try
            {
                field.SetValue(null, table);
                test();
            }
            finally { field.SetValue(null, before); }
        }

        [Fact]
        public void TheHostBlockReadsTheTickedValuesAsAnArray()
        {
            var options = new[] { "steam|Steam", "epic|Epic Games", "gog|GOG" };
            WithTable(() =>
            {
                var settings = UiSettings.Parse(@"{ ""freeExcludePlatforms"": "" GOG , Steam,itch.io,, steam "", ""blockedUsers"": ""alice, Bob ,ALICE"",
                    ""none"": """", ""byHand"": [ ""gog"", 3, null, ""Steam"" ] }");
                // The options' own spelling and order, whatever the file's; a value none of them has behind them.
                Assert.Equal(new[] { "steam", "gog", "itch.io" }, settings.Values("freeExcludePlatforms"));
                Assert.Equal("steam,gog", string.Join(",", UiSettings.Parse(@"{ ""freeExcludePlatforms"": ""steam,gog"" }").Values("freeExcludePlatforms")));
                Assert.Empty(settings.Values("none"));
                Assert.Equal(new[] { "epic", "gog" }, settings.Values("byDefault"));                   // the file has none: the table's default
                Assert.Empty(settings.Values("notASetting"));
                Assert.Equal(new[] { "steam", "gog", "3" }, settings.Values("byHand"));
                // A namelist is stored the same way and has no options: its names as they are.
                Assert.Equal(new[] { "alice", "Bob" }, settings.Values("blockedUsers"));
                // An argument of the action over the file.
                var with = settings.With(new Dictionary<string, object> { ["freeExcludePlatforms"] = "Epic" });
                Assert.Equal(new[] { "epic" }, with.Values("freeExcludePlatforms"));
                // The window reads the same table.
                var schema = Schema.Parse(UiSchema.ToJson("demo", "v1", "Demo/settings.json"));
                Assert.Equal("multi", schema.Find("freeExcludePlatforms").Kind);
                Assert.Equal(new[] { "Steam", "Epic Games", "GOG" }, schema.Find("freeExcludePlatforms").Options.Select(o => o.Label));
            },
            UiSchema.S("freeExcludePlatforms", "", kind: "multi", options: options), UiSchema.S("none", "", kind: "multi", options: options),
            UiSchema.S("byDefault", "epic,gog", kind: "multi", options: options), UiSchema.S("byHand", "", kind: "multi", options: options),
            UiSchema.S("blockedUsers", "", kind: "namelist"), UiSchema.S("announceTime", "09:00", kind: "time"));
        }

        [Fact]
        public void TheHostReaderReadsTheTickedValuesWithAFallback()
        {
            var settings = UiReaderSettings.Parse(@"{ ""freeExcludePlatforms"": "" gog , steam,itch.io,, STEAM "", ""none"": """", ""byHand"": [ ""gog"", ""steam"" ], ""empty"": null }");
            Assert.Equal(new[] { "gog", "steam", "itch.io" }, settings.Values("freeExcludePlatforms"));    // as the file has them: it knows no table
            Assert.Empty(settings.Values("none", "steam"));                                               // the file says none
            Assert.Equal(new[] { "gog", "steam" }, settings.Values("byHand"));
            Assert.Equal(new[] { "steam", "gog" }, settings.Values("missing", "steam, gog"));
            Assert.Equal(new[] { "steam" }, settings.Values("empty", "steam"));                            // null in the file is no value
            Assert.Empty(settings.Values("missing"));
            var with = settings.With(new Dictionary<string, object> { ["freeExcludePlatforms"] = "epic" }, "freeExcludePlatforms");
            Assert.Equal(new[] { "epic" }, with.Values("freeExcludePlatforms"));
        }

        // ---- time: what the field takes -------------------------------------------------------------------------------

        [Theory]
        [InlineData("9:5", "09:05")]
        [InlineData("09:05", "09:05")]
        [InlineData("0905", "09:05")]
        [InlineData("9", "09:00")]
        [InlineData("905", "09:05")]
        [InlineData("17", "17:00")]
        [InlineData("0", "00:00")]
        [InlineData("23:59", "23:59")]
        [InlineData(" 7:30 ", "07:30")]
        [InlineData("9.05", "09:05")]
        [InlineData("9h05", "09:05")]
        [InlineData("21h", "21:00")]
        [InlineData("9:", "09:00")]
        public void ATimeIsTakenInTheseForms(string typed, string written)
        {
            Assert.True(FieldBinding.TryTime(typed, out string time), typed);
            Assert.Equal(written, time);
        }

        [Theory]
        [InlineData("")]
        [InlineData("  ")]
        [InlineData("24")]
        [InlineData("24:00")]
        [InlineData("2400")]
        [InlineData("9:60")]
        [InlineData("960")]
        [InlineData("12345")]
        [InlineData("1:2:3")]
        [InlineData("9:005")]
        [InlineData(":30")]
        [InlineData("9 05")]
        [InlineData("-1")]
        [InlineData("9pm")]
        [InlineData("noon")]
        [InlineData("9,5")]
        public void WhatIsNoTimeOfDayIsRefused(string typed)
        {
            Assert.False(FieldBinding.TryTime(typed, out string time), typed);
            Assert.Null(time);
        }

        /// <summary>A time field alone on a page, over a stored value.</summary>
        private static (Page Page, TextBox Box) Time(string @default, string storedJson = "{}", string more = "")
        {
            var page = new Page(Settings(@"{ ""key"": ""at"", ""default"": """ + @default + @""", ""kind"": ""time"", ""label"": ""Announce at""" + more + " }"), storedJson);
            return (page, page.Parts.Boxes.TryGetValue("at", out var box) ? box : null);
        }

        [Fact]
        public void ATimeIsWrittenAsHoursAndMinutesHoweverItWasTyped() => Sta(() =>
        {
            var (page, box) = Time("09:00");
            Assert.Equal("09:00", box.Text);
            Assert.Equal(FieldKind.Time, ((FieldTag)box.Tag).Kind);
            Assert.Contains(Text.T("field.time.tip"), (string)box.ToolTip);

            box.Text = "9:5";
            Assert.Empty(FieldBinding.InvalidTimeFields(page.Root));
            var saved = (JObject)page.Stored.DeepClone();
            FieldBinding.Save(saved, page.Root);
            Assert.Equal(JTokenType.String, saved["at"].Type);
            Assert.Equal("09:05", (string)saved["at"]);
            Assert.Equal("09:05", box.Text);                                                           // the box shows what was saved

            // And as soon as the cursor leaves the box.
            box.Text = "1730";
            box.RaiseEvent(new RoutedEventArgs(UIElement.LostFocusEvent));
            Assert.Equal("17:30", box.Text);
            box.Text = "7";
            box.RaiseEvent(new RoutedEventArgs(UIElement.LostFocusEvent));
            Assert.Equal("07:00", box.Text);

            // The live values and a condition have it as it would be stored.
            box.Text = "8:5";
            var live = new JObject();
            FieldBinding.Save(live, page.Root, transientToo: true);
            Assert.Equal("08:05", (string)live["at"]);
            Assert.Equal("8:5", box.Text);                                                             // read, not touched
            Assert.True(Requirement.Holds(page.Ctx.Fields, page.Parts, Requirement.Parse("at=08:05")[0]));
            Assert.False(Requirement.Holds(page.Ctx.Fields, page.Parts, Requirement.Parse("!at")[0]));
        });

        [Fact]
        public void TextThatIsNoTimeIsNamedBeforeSavingAsANumberFieldsIs() => Sta(() =>
        {
            var (page, box) = Time("09:00", @"{ ""at"": ""18:00"" }");
            box.Text = "25:00";
            box.RaiseEvent(new RoutedEventArgs(UIElement.LostFocusEvent));
            Assert.Equal("25:00", box.Text);                                                           // it stays, for the check to name
            Assert.Equal(new[] { "Announce at: '25:00'" }, FieldBinding.InvalidTimeFields(page.Root));
            Assert.Empty(FieldBinding.InvalidNumberFields(page.Root));
            string why = FieldBinding.WhyNotSaved(page.Root, out string title);
            Assert.Equal(Text.T("dialog.invalidTimes.text", "Announce at: '25:00'"), why);
            Assert.Equal(Text.T("dialog.invalidTimes.title"), title);
            // Saved all the same (only a field a condition hides gets that far), it keeps what is stored.
            Assert.Equal("18:00", (string)page.Saved()["at"]);
            // A live snapshot hands the project what the box shows.
            var live = new JObject();
            FieldBinding.Save(live, page.Root, transientToo: true);
            Assert.Equal("25:00", (string)live["at"]);

            box.Text = "18:30";
            Assert.Null(FieldBinding.WhyNotSaved(page.Root, out title));
            Assert.Null(title);
        });

        [Fact]
        public void ANumberAndATimeThatCannotBeSavedAreNamedInOneBox() => Sta(() =>
        {
            var page = new Page(Settings(@"{ ""key"": ""at"", ""default"": ""09:00"", ""kind"": ""time"", ""label"": ""Announce at"" }",
                @"{ ""key"": ""every"", ""default"": 5, ""label"": ""Every"" }"));
            page.Box("at").Text = "";
            page.Box("every").Text = "";
            string why = FieldBinding.WhyNotSaved(page.Root, out string title);
            Assert.Equal(Text.T("dialog.invalidNumbers.text", "Every: " + Text.T("field.empty")) + "\n\n"
                + Text.T("dialog.invalidTimes.text", "Announce at: " + Text.T("field.empty")), why);
            Assert.Equal(Text.T("dialog.invalidNumbers.title"), title);
            // A field a condition hides stops no save. (The pages sit under what is checked; a page itself may be hidden.)
            var pages = new StackPanel();
            pages.Children.Add(page.Root);
            Assert.Single(FieldBinding.InvalidTimeFields(pages));
            page.Fields["at"].Visibility = Visibility.Collapsed;
            Assert.Empty(FieldBinding.InvalidTimeFields(pages));
        });

        [Fact]
        public void OnlyDigitsAndWhatStandsBetweenHoursAndMinutesAreTyped() => Sta(() =>
        {
            var (_, box) = Time("09:00");
            bool Typed(string text)
            {
                var input = new TextCompositionEventArgs(Keyboard.PrimaryDevice, new TextComposition(InputManager.Current, box, text)) { RoutedEvent = TextCompositionManager.PreviewTextInputEvent };
                box.RaiseEvent(input);
                return !input.Handled;
            }
            Assert.True(Typed("7"));
            Assert.True(Typed(":"));
            Assert.True(Typed("."));
            Assert.True(Typed("h"));
            Assert.False(Typed("a"));
            Assert.False(Typed("-"));
            Assert.False(Typed(" "));
            Assert.Equal(5, box.MaxLength);
        });

        // ---- time: an empty field ---------------------------------------------------------------------------------

        [Fact]
        public void AnEmptyFieldIsTakenOnlyWhereTheDefaultIsEmpty() => Sta(() =>
        {
            var (optional, none) = Time("");
            Assert.Equal("", none.Text);
            Assert.Empty(FieldBinding.InvalidTimeFields(optional.Root));
            Assert.Equal("", (string)optional.Saved()["at"]);
            Assert.True(Requirement.Holds(optional.Ctx.Fields, optional.Parts, Requirement.Parse("!at")[0]));
            none.Text = "6";
            Assert.Equal("06:00", (string)optional.Saved()["at"]);
            none.Text = " ";
            Assert.Equal("", (string)optional.Saved()["at"]);

            var (due, box) = Time("09:00");
            box.Text = "";
            Assert.Equal(new[] { "Announce at: " + Text.T("field.empty") }, FieldBinding.InvalidTimeFields(due.Root));
            Assert.Equal("09:00", (string)due.Saved()["at"]);                                          // not saved empty
        });

        [Fact]
        public void AStoredValueTheFieldCannotTakeShowsTheDefaultAsANumberFieldDoes() => Sta(() =>
        {
            foreach (string stored in new[] { @"""""", @"""soon""", @"""25:00""", "[ 9 ]" })
            {
                var (page, box) = Time("09:00", @"{ ""at"": " + stored + " }");
                Assert.Equal("09:00", box.Text);
                Assert.Empty(FieldBinding.InvalidTimeFields(page.Root));
                // Written with the next save, and said, since the box reads other than the file.
                Assert.Contains("at", FieldBinding.Repaired(page.Stored, page.Root));
                Assert.Contains("at", FieldBinding.Repaired(page.Stored, page.Root, shownOtherwise: true));
                Assert.Equal("09:00", (string)page.Saved()["at"]);
            }

            // Another spelling of a time is that time: put right with the next save, nothing to tell.
            var (byHand, shown) = Time("09:00", @"{ ""at"": ""7:5"" }");
            Assert.Equal("07:05", shown.Text);
            Assert.Contains("at", FieldBinding.Repaired(byHand.Stored, byHand.Root));
            Assert.Empty(FieldBinding.Repaired(byHand.Stored, byHand.Root, shownOtherwise: true));
            var (asStored, _) = Time("09:00", @"{ ""at"": ""07:05"" }");
            Assert.Empty(FieldBinding.Repaired(asStored.Stored, asStored.Root));

            // A field that may be empty: nothing stored is nothing, text that is no time goes with the next save.
            var (optional, none) = Time("", @"{ ""at"": """" }");
            Assert.Equal("", none.Text);
            Assert.Empty(FieldBinding.Repaired(optional.Stored, optional.Root));
            var (lost, empty) = Time("", @"{ ""at"": ""soon"" }");
            Assert.Equal("", empty.Text);
            Assert.Contains("at", FieldBinding.Repaired(lost.Stored, lost.Root, shownOtherwise: true));
        });

        // ---- time: what every field does ----------------------------------------------------------------------------

        [Fact]
        public void ATimeIsResetImportedSetAndCountedLikeAnyField() => Sta(() =>
        {
            var (page, box) = Time("09:00", @"{ ""at"": ""18:00"" }");
            var baseline = new Dictionary<object, string>();
            FieldBinding.Snapshot(page.Root, baseline);
            box.Text = "19:00";
            Assert.Equal(1, FieldBinding.CountChanged(page.Root, baseline));
            Assert.Equal(new[] { "at" }, FieldBinding.ChangedKeys(page.Root, baseline));

            FieldBinding.ResetToDefaults(page.Root, page.Spec.Defaults);
            Assert.Equal("09:00", box.Text);

            // An import goes through the field: taken as typed, or refused with the reason.
            Assert.Null(FieldBinding.AsSaved(page.Root, "at", "7", out var taken));
            Assert.Equal("07:00", (string)taken);
            Assert.Null(FieldBinding.AsSaved(page.Root, "at", 1730, out taken));
            Assert.Equal("17:30", (string)taken);
            Assert.Equal(Text.T("import.why.time"), FieldBinding.AsSaved(page.Root, "at", "25:00", out taken));
            Assert.Equal(Text.T("import.why.time"), FieldBinding.AsSaved(page.Root, "at", "", out taken));          // a time is due here
            Assert.Equal(Text.T("import.why.time"), FieldBinding.AsSaved(page.Root, "at", new JArray("09:00"), out taken));
            var (optional, _) = Time("");
            Assert.Null(FieldBinding.AsSaved(optional.Root, "at", "", out taken));
            Assert.Equal("", (string)taken);

            // An answer's `set`.
            Assert.True(LiveValues.Set(page.Parts, "at", "6:15"));
            Assert.Equal("06:15", box.Text);
            // An old action's value: a time, or left out.
            var setting = page.Spec.ResolvedSettings.Single(s => s.Key == "at");
            Assert.Equal("21:05", (string)Adoption.ToKind(setting, "21.05"));
            Assert.Null(Adoption.ToKind(setting, "in the evening"));
        });

        [Fact]
        public void AReadOnlyTimeShowsAsItIsWritten() => Sta(() =>
        {
            var (page, box) = Time("09:00", @"{ ""at"": ""7:5"" }", @", ""readOnly"": true");
            Assert.Null(box);
            Assert.Equal("Announce at | 07:05", Shown(page.Fields["at"]));
            var (odd, _) = Time("09:00", @"{ ""at"": ""at dawn"" }", @", ""readOnly"": true");
            Assert.Equal("Announce at | at dawn", Shown(odd.Fields["at"]));                            // what the project wrote, as it is
        });

        [Fact]
        public void AUnitGoesBehindTheLabelOfATimeAsBehindANumbers() => Sta(() =>
        {
            var (page, box) = Time("09:00", "{}", @", ""unit"": ""UTC""");
            Assert.Equal("Announce at (UTC)", Shown(page.Fields["at"]));
            box.Text = "";
            Assert.Equal(new[] { "Announce at (UTC): " + Text.T("field.empty") }, FieldBinding.InvalidTimeFields(page.Root));
        });

        [Fact]
        public void ATimeLinesUpWithTheNumbers()
        {
            var fields = new List<Setting>
            {
                new Setting { Key = "every", Kind = "int" }, new Setting { Key = "at", Kind = "time" }, new Setting { Key = "most", Kind = "int" },
                new Setting { Key = "platforms", Kind = "multi" }, new Setting { Key = "until", Kind = "time" }
            };
            Assert.Equal(new[] { 0, 0, 0, 1, 2 }, Customize.Lines(fields));
        }

        // ---- time: what the red "i" names -----------------------------------------------------------------------------

        [Fact]
        public void TheCheckNamesLimitsOnATimeAndADefaultThatIsNone()
        {
            Assert.Empty(Settings(@"{ ""key"": ""at"", ""default"": ""09:00"", ""kind"": ""time"" }").Problems);
            Assert.Empty(Settings(@"{ ""key"": ""at"", ""default"": """", ""kind"": ""time"" }").Problems);

            var limits = Settings(@"{ ""key"": ""at"", ""default"": ""09:00"", ""kind"": ""time"", ""min"": 6, ""max"": 22 }").Problems;
            Assert.Contains(limits, p => p.Contains("'at' has a min and a max, which do nothing on a time field"));
            Assert.Contains(Settings(@"{ ""key"": ""at"", ""default"": ""09:00"", ""kind"": ""time"", ""max"": 22 }").Problems,
                p => p.Contains("'at' has a max, which does nothing on a time field"));

            Assert.Contains(Settings(@"{ ""key"": ""at"", ""default"": ""25:00"", ""kind"": ""time"" }").Problems,
                p => p.Contains("'at'") && p.Contains("'25:00'") && p.Contains("no time of day"));
            Assert.Contains(Settings(@"{ ""key"": ""at"", ""default"": 900, ""kind"": ""time"" }").Problems,
                p => p.Contains("'at'") && p.Contains("default is no text"));
            // The project reads the default as it is declared.
            Assert.Contains(Settings(@"{ ""key"": ""at"", ""default"": ""9:5"", ""kind"": ""time"" }").Problems,
                p => p.Contains("'9:5'") && p.Contains("'09:05'"));
            Assert.DoesNotContain(Settings(@"{ ""key"": ""at"", ""default"": ""09:00"", ""kind"": ""time"" }").Problems, p => p.Contains("does not know"));
        }
    }
}
