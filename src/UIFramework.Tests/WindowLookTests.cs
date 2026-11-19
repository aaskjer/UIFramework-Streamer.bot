using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Newtonsoft.Json.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using Xunit;

namespace UIFramework.Tests
{
    /// <summary>What 2.2.0 adds to how a window looks: a masked field that keeps its width when revealed, a hint inside
    /// an empty text field, the kind <c>country</c>, a header pill of several states, a section heading that reads
    /// otherwise on a condition, check boxes in columns, a link that reads as words, and an answer that copies one
    /// field into another.</summary>
    public class WindowLookTests
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

        private static WindowSpec Prepared(string schemaJson)
        {
            try
            {
                var schema = Schema.Parse(schemaJson);
                var spec = Entry.SpecFor(schema, null);
                Entry.Prepare(spec, schema);
                return spec;
            }
            finally { Text.Use(Conventions.Default.Lang); }
        }

        private static WindowSpec Settings(params string[] settings) => Prepared(Head + string.Join(", ", settings) + " ] }");

        private static WindowSpec Windowed(string window, params string[] settings) =>
            Prepared(Head + string.Join(", ", settings) + @" ], ""window"": " + window + " }");

        private static IEnumerable<T> Find<T>(object node) where T : class
        {
            if (node is T hit) yield return hit;
            if (node is DependencyObject d)
                foreach (var child in LogicalTreeHelper.GetChildren(d))
                    foreach (var found in Find<T>(child))
                        yield return found;
        }

        private static string Shown(object node) => string.Join(" | ", Find<TextBlock>(node)
            .Select(t => t.Inlines.Count > 0 ? string.Concat(t.Inlines.OfType<Run>().Select(r => r.Text)) : t.Text).Where(t => !string.IsNullOrEmpty(t)));

        private static void Layout(FrameworkElement element, double width)
        {
            element.Measure(new Size(width, 400));
            element.Arrange(new Rect(0, 0, width, element.DesiredSize.Height));
            element.UpdateLayout();
        }

        /// <summary>The fields of a schema as a page builds them, over the given stored values.</summary>
        private sealed class Page
        {
            public readonly WindowSpec Spec;
            public readonly WindowParts Parts = new WindowParts();
            public readonly JObject Stored;
            public readonly PageContext Ctx;
            public readonly StackPanel Root = new StackPanel();
            public readonly Dictionary<string, FrameworkElement> Fields = new Dictionary<string, FrameworkElement>();

            public Page(WindowSpec spec, string storedJson = "{}")
            {
                Spec = spec;
                Stored = (JObject)spec.Defaults.DeepClone();
                foreach (var property in JObject.Parse(storedJson).Properties())
                    Stored[property.Name] = property.Value;
                Ctx = Entry.ContextFor(spec, Stored, Parts, null, null);
                foreach (var setting in spec.ResolvedSettings.Where(s => !s.Hidden))
                {
                    Fields[setting.Key] = SchemaPage.Render(Ctx, setting);
                    Root.Children.Add(Fields[setting.Key]);
                }
                foreach (var wire in Ctx.AfterBuild) wire();
            }

            public JObject Saved()
            {
                var saved = (JObject)Stored.DeepClone();
                FieldBinding.Save(saved, Root);
                return saved;
            }
        }

        // ---- a masked field --------------------------------------------------------------------------------------

        [Fact]
        public void AMaskedFieldIsAsWideRevealedAsItIsMasked() => Sta(() =>
        {
            var page = new Page(Settings(@"{ ""key"": ""token"", ""default"": """", ""masked"": true }"), @"{ ""token"": ""abc"" }");
            var field = page.Fields["token"];
            var masked = Find<PasswordBox>(field).Single();
            var box = page.Parts.Boxes["token"];
            Layout(field, 420);
            double hidden = masked.ActualWidth;
            Assert.True(hidden > 300, "the masked box fills the field");
            Assert.Same(masked.Parent, box.Parent);   // one cell for the two

            Find<Button>(field).Single().RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            Layout(field, 420);
            Assert.Equal(Visibility.Visible, box.Visibility);
            Assert.Equal(Visibility.Collapsed, masked.Visibility);
            Assert.Equal(hidden, box.ActualWidth, 1);   // it was the width of "abc"
        });

        // ---- a hint ----------------------------------------------------------------------------------------------

        [Fact]
        public void AHintStandsInAnEmptyTextFieldMaskedOrNot() => Sta(() =>
        {
            var page = new Page(Settings(
                @"{ ""key"": ""channel"", ""default"": """", ""hint"": ""not set"" }",
                @"{ ""key"": ""token"", ""default"": """", ""masked"": true, ""hint"": ""optional"" }",
                @"{ ""key"": ""plain"", ""default"": """" }"));
            Assert.Empty(page.Spec.Problems);
            foreach (var (key, hint) in new[] { ("channel", "not set"), ("token", "optional") })
            {
                var text = Find<TextBlock>(page.Fields[key]).Single(t => Equals(t.Tag, Ui.HintTag));
                Assert.Equal(hint, text.Text);
                Assert.Equal(Visibility.Visible, text.Visibility);
                page.Parts.Boxes[key].Text = "x";
                Assert.Equal(Visibility.Collapsed, text.Visibility);
                page.Parts.Boxes[key].Text = "";
                Assert.Equal(Visibility.Visible, text.Visibility);
            }
            Assert.DoesNotContain(Find<TextBlock>(page.Fields["plain"]), t => Equals(t.Tag, Ui.HintTag));
            // The masked field's dots lie under the hint, not over it.
            var cell = (Grid)page.Parts.Boxes["token"].Parent;
            Assert.True(cell.Children.IndexOf(Find<PasswordBox>(cell).Single()) < cell.Children.IndexOf(Find<TextBlock>(cell).Single()));
        });

        [Fact]
        public void AHintTravelsThroughTheSchemaAndIsNamedWhereNoFieldShowsIt()
        {
            var declared = UiSchema.ToJson(UiSchema.S("channel", "", hint: "not set"));
            Assert.Equal("not set", (string)declared["hint"]);
            Assert.Null(UiSchema.ToJson(UiSchema.S("channel", ""))["hint"]);
            Assert.Equal("not set", Setting.Parse(Setting.Parse(declared).ToJson()).Hint);

            var spec = Settings(@"{ ""key"": ""limit"", ""default"": 5, ""hint"": ""a number"" }");
            Assert.Contains(spec.Problems, p => p.Contains("'limit'") && p.Contains("hint"));
        }

        // ---- country ---------------------------------------------------------------------------------------------

        [Fact]
        public void TheCountriesAreTheCodesOfIso3166WithAName()
        {
            Assert.Equal(250, Countries.Codes.Count);   // 249 of ISO 3166-1, and Kosovo
            Assert.Equal(Countries.Codes.Count, Countries.Codes.Distinct().Count());
            Assert.All(Countries.Codes, code => Assert.Matches("^[A-Z]{2}$", code));
            Assert.All(Countries.Codes, code => Assert.False(string.IsNullOrWhiteSpace(Countries.English(code))));
            Assert.Equal("Germany", Countries.English("de"));
            Assert.Null(Countries.English("ZZ"));
            Assert.True(Countries.IsCode(" us "));
            Assert.False(Countries.IsCode("USA"));
            Assert.Equal("Germany", Countries.Name("DE", "en"));
            Assert.Equal("ZZ", Countries.Name("zz", "de"));   // no country: itself
            // Another language's names are Windows' own; where it has none, the English ones stand.
            Assert.Equal(Countries.SystemNames ? "Deutschland" : "Germany", Countries.Name("DE", "de"));
            Assert.Equal(Countries.SystemNames ? "Alemanha" : "Germany", Countries.Name("DE", "pt-BR"));
            Assert.False(string.IsNullOrWhiteSpace(Countries.Name("DE", "no-such-language")));

            var listed = Countries.Listed("en");
            Assert.Equal(250, listed.Count);
            Assert.Contains(("DE", "Germany (DE)"), listed);
            // By name, not by code: Åland sorts with the A's, the United States after Uganda.
            Assert.True(listed.FindIndex(c => c.Code == "AX") < listed.FindIndex(c => c.Code == "AL"));
            Assert.True(listed.FindIndex(c => c.Code == "UG") < listed.FindIndex(c => c.Code == "US"));
        }

        [Fact]
        public void ACountryFieldIsADropdownThatStoresTheCode() => Sta(() =>
        {
            var page = new Page(Settings(@"{ ""key"": ""country"", ""default"": """", ""kind"": ""country"" }"), @"{ ""country"": ""de"" }");
            Assert.Empty(page.Spec.Problems);
            var combo = page.Parts.Combos["country"];
            var items = combo.Items.OfType<ComboBoxItem>().ToList();
            Assert.Equal(251, items.Count);                                   // no country, and the 250
            Assert.Equal("", (string)items[0].Tag);
            Assert.Equal("No country", (string)items[0].Content);
            Assert.Equal("Germany (DE)", (string)((ComboBoxItem)combo.SelectedItem).Content);   // "de" in the file is Germany
            Assert.Equal("DE", (string)page.Saved()["country"]);              // and is written as the code is

            combo.SelectedItem = items.Single(i => (string)i.Tag == "US");
            Assert.Equal("US", (string)page.Saved()["country"]);
            combo.SelectedItem = items[0];
            Assert.Equal("", (string)page.Saved()["country"]);
        });

        [Fact]
        public void ACountryFieldKeepsWhatItDoesNotKnowAndPicksNoCountryByItself() => Sta(() =>
        {
            // A code that is none stays, as an entry of its own.
            var odd = new Page(Settings(@"{ ""key"": ""country"", ""default"": """", ""kind"": ""country"" }"), @"{ ""country"": ""UK"" }");
            Assert.Equal("UK", (string)((ComboBoxItem)odd.Parts.Combos["country"].SelectedItem).Tag);
            Assert.Equal("UK", (string)odd.Saved()["country"]);

            // A default of its own: no entry for "no country"...
            var fixedDefault = new Page(Settings(@"{ ""key"": ""country"", ""default"": ""US"", ""kind"": ""country"" }"));
            Assert.Empty(fixedDefault.Spec.Problems);
            Assert.DoesNotContain(fixedDefault.Parts.Combos["country"].Items.OfType<ComboBoxItem>(), i => (string)i.Tag == "");
            Assert.Equal("US", (string)fixedDefault.Saved()["country"]);
            // ...unless the file holds none: the window then shows none, and does not pick the first of the list.
            var emptied = new Page(Settings(@"{ ""key"": ""country"", ""default"": ""US"", ""kind"": ""country"" }"), @"{ ""country"": """" }");
            Assert.Equal("", (string)((ComboBoxItem)emptied.Parts.Combos["country"].SelectedItem).Tag);
            Assert.Equal("", (string)emptied.Saved()["country"]);
        });

        [Fact]
        public void ACountryFieldsOptionsNarrowTheListAndNameNoCountry() => Sta(() =>
        {
            var page = new Page(Settings(@"{ ""key"": ""country"", ""default"": """", ""kind"": ""country"",
                ""options"": [ ""|Everywhere"", ""US"", ""DE"", ""AT|Austria, of all places"" ] }"));
            Assert.Empty(page.Spec.Problems);
            var items = page.Parts.Combos["country"].Items.OfType<ComboBoxItem>().ToList();
            Assert.Equal(new[] { "", "AT", "DE", "US" }, items.Select(i => (string)i.Tag).ToArray());   // by name
            Assert.Equal(new[] { "Everywhere", "Austria, of all places", "Germany (DE)", "United States (US)" }, items.Select(i => (string)i.Content).ToArray());
        });

        [Fact]
        public void ACountryFieldsDeclarationIsChecked()
        {
            Assert.Contains(Settings(@"{ ""key"": ""c"", ""default"": ""Germany"", ""kind"": ""country"" }").Problems, p => p.Contains("'Germany'") && p.Contains("no country code"));
            Assert.Contains(Settings(@"{ ""key"": ""c"", ""default"": ""de"", ""kind"": ""country"" }").Problems, p => p.Contains("'DE'"));
            Assert.Contains(Settings(@"{ ""key"": ""c"", ""default"": 49, ""kind"": ""country"" }").Problems, p => p.Contains("no text"));
            Assert.Contains(Settings(@"{ ""key"": ""c"", ""default"": """", ""kind"": ""country"", ""options"": [ ""ZZ"" ] }").Problems, p => p.Contains("'ZZ'"));
            Assert.Empty(Settings(@"{ ""key"": ""c"", ""default"": ""DE"", ""kind"": ""country"" }").Problems);
        }

        [Fact]
        public void ACountryFollowsConditionsLikeAnyDropdown() => Sta(() =>
        {
            var page = new Page(Settings(
                @"{ ""key"": ""country"", ""default"": """", ""kind"": ""country"" }",
                @"{ ""key"": ""mode"", ""default"": ""hide"", ""requires"": ""country"" }"));
            Assert.Empty(page.Spec.Problems);
            Assert.False(page.Fields["mode"].IsEnabled);
            var combo = page.Parts.Combos["country"];
            combo.SelectedItem = combo.Items.OfType<ComboBoxItem>().Single(i => (string)i.Tag == "DE");
            Assert.True(page.Fields["mode"].IsEnabled);
        });

        [Fact]
        public void ATypedLetterJumpsToTheFirstCountryThatStartsWithIt() => Sta(() =>
        {
            var page = new Page(Settings(@"{ ""key"": ""country"", ""default"": """", ""kind"": ""country"" }"));
            var combo = page.Parts.Combos["country"];
            var window = new Window { Content = page.Root, Width = 320, Height = 200, Left = -20000, Top = -20000,
                ShowInTaskbar = false, ShowActivated = false, WindowStartupLocation = WindowStartupLocation.Manual };
            window.Show();
            try
            {
                void Type(string text) => combo.RaiseEvent(new System.Windows.Input.TextCompositionEventArgs(System.Windows.Input.InputManager.Current.PrimaryKeyboardDevice,
                    new System.Windows.Input.TextComposition(System.Windows.Input.InputManager.Current, combo, text)) { RoutedEvent = UIElement.TextInputEvent });
                Type("g");
                Assert.StartsWith("G", (string)((ComboBoxItem)combo.SelectedItem).Content);
                Type("e");
                Assert.StartsWith("Ge", (string)((ComboBoxItem)combo.SelectedItem).Content);   // "ge": Georgia, Germany
            }
            finally { window.Close(); }
        });

        // ---- a header pill of several states ---------------------------------------------------------------------

        private const string Connection = @"{ ""key"": ""botToken"", ""default"": """", ""masked"": true }, { ""key"": ""channelId"", ""default"": """" }, { ""key"": ""webhookUrl"", ""default"": """" }";
        private const string Pills = @"{ ""title"": ""T"", ""headerPills"": [ { ""states"": [
            { ""when"": ""botToken && channelId"", ""text"": ""Free: bot"", ""tone"": ""ok"", ""tooltip"": ""Posts with a bot."" },
            { ""when"": ""webhookUrl"", ""text"": ""Free: webhook"", ""tone"": ""ok"" },
            { ""text"": ""Free: not set up"", ""tooltip"": ""No Discord connection yet."" } ] } ] }";

        [Fact]
        public void AHeaderPillSaysTheFirstStateThatHoldsAsTheWindowIsTypedIn() => Sta(() =>
        {
            var spec = Windowed(Pills, Connection);
            Assert.Empty(spec.Problems);
            var page = new Page(spec);
            var pill = spec.HeaderPills.Single();
            Assert.Equal(("Free: not set up", PillStyle.Muted), pill.Read(page.Ctx));
            Assert.Equal("No Discord connection yet.", pill.ReadTooltip(page.Ctx));

            page.Parts.Boxes["webhookUrl"].Text = "https://discord.example/hook";
            Assert.Equal(("Free: webhook", PillStyle.Success), pill.Read(page.Ctx));
            Assert.Null(pill.ReadTooltip(page.Ctx));

            page.Parts.Boxes["botToken"].Text = "t";
            Assert.Equal(("Free: webhook", PillStyle.Success), pill.Read(page.Ctx));   // a token without a channel is no bot
            page.Parts.Boxes["channelId"].Text = "1";
            Assert.Equal(("Free: bot", PillStyle.Success), pill.Read(page.Ctx));
            Assert.Equal("Posts with a bot.", pill.ReadTooltip(page.Ctx));
        });

        [Fact]
        public void AHeaderPillWithoutAStateThatHoldsIsNotShown() => Sta(() =>
        {
            var spec = Windowed(@"{ ""title"": ""T"", ""headerPills"": [ { ""states"": [ { ""when"": ""webhookUrl"", ""text"": ""Webhook"", ""tone"": ""warn"" } ] } ] }", Connection);
            var page = new Page(spec);
            Assert.Null(spec.HeaderPills.Single().Read(page.Ctx).Text);
            page.Parts.Boxes["webhookUrl"].Text = "x";
            Assert.Equal(("Webhook", PillStyle.Warning), spec.HeaderPills.Single().Read(page.Ctx));
        });

        [Fact]
        public void AFieldASetArgumentSetsCountsAsFilledInForAHeaderPill() => Sta(() =>
        {
            var spec = Prepared(Head + Connection + @" ], ""arguments"": { ""botToken"": ""botToken"", ""channelId"": ""channelId"" }, ""window"": " + Pills + " }");
            var page = new Page(spec);
            Assert.Equal("Free: bot", spec.HeaderPills.Single().Read(page.Ctx).Text);
        });

        [Fact]
        public void AHeaderPillsDeclarationIsCheckedAndTravelsFromTheHostBlock()
        {
            var bad = Windowed(@"{ ""title"": ""T"", ""headerPills"": [ { ""states"": [ { ""when"": ""nothere"", ""text"": ""A"", ""tone"": ""green"" } ] } ] }", Connection);
            Assert.Contains(bad.Problems, p => p.Contains("'A'") && p.Contains("'nothere'"));
            Assert.Contains(bad.Problems, p => p.Contains("'A'") && p.Contains("'green'"));

            var window = new UiWindow("Demo").Pill(
                UiSchema.State("botToken && channelId", "Free: bot", "ok", "Posts with a bot."),
                UiSchema.State(null, "Free: not set up"));
            var states = (JArray)window.ToJson()["headerPills"][0]["states"];
            Assert.Equal(@"{""text"":""Free: bot"",""when"":""botToken && channelId"",""tone"":""ok"",""tooltip"":""Posts with a bot.""}", states[0].ToString(Newtonsoft.Json.Formatting.None));
            Assert.Equal(@"{""text"":""Free: not set up""}", states[1].ToString(Newtonsoft.Json.Formatting.None));
            var spec = SpecJson.Parse(window.ToJson());
            Assert.Equal(new[] { "Free: bot", "Free: not set up" }, spec.HeaderPills.Single().States.Select(s => s.Text).ToArray());
            // The pill that follows a switch is declared as it was.
            Assert.Equal("freeGame", (string)new UiWindow("Demo").Pill("freeGame", "On").ToJson()["headerPills"][0]["key"]);
        }

        // ---- a section's heading on a condition ------------------------------------------------------------------

        private static List<TextBlock> Headings(UIElement page) =>
            Find<TextBlock>(page).Where(t => t.FontWeight == FontWeights.SemiBold && Math.Abs(t.FontSize - 11.5) < 0.01).ToList();

        [Fact]
        public void ASectionsHeadingReadsOtherwiseWhileItsConditionHolds() => Sta(() =>
        {
            var spec = Windowed(@"{ ""title"": ""T"", ""pages"": [ { ""id"": ""discord"", ""title"": ""Discord"" }, { ""id"": ""schedule"", ""title"": ""Schedule"" } ],
                    ""sections"": [ { ""name"": ""Free"", ""page"": ""discord"", ""title"": ""Free and Sales"", ""titleWhen"": ""same"" } ] }",
                @"{ ""key"": ""same"", ""default"": false, ""page"": ""discord"", ""section"": ""How"" }",
                @"{ ""key"": ""channel"", ""default"": """", ""page"": ""discord"", ""section"": ""Free"" }",
                @"{ ""key"": ""every"", ""default"": 60, ""page"": ""schedule"", ""section"": ""Free"" }");
            Assert.Empty(spec.Problems);
            var parts = new WindowParts();
            var ctx = Entry.ContextFor(spec, (JObject)spec.Defaults.DeepClone(), parts, null, null);
            var discord = SchemaPage.Build(ctx, spec.ResolvedSettings.Where(s => s.Page == "discord").ToList(), "discord");
            var schedule = SchemaPage.Build(ctx, spec.ResolvedSettings.Where(s => s.Page == "schedule").ToList(), "schedule");
            foreach (var wire in ctx.AfterBuild) wire();

            Assert.Equal(new[] { "How", "Free" }, Headings(discord).Select(h => h.Text).ToArray());
            parts.Switches["same"].IsChecked = true;
            Assert.Equal(new[] { "How", "Free and Sales" }, Headings(discord).Select(h => h.Text).ToArray());
            Assert.Equal(new[] { "Free" }, Headings(schedule).Select(h => h.Text).ToArray());   // the other page's keeps its name
            parts.Switches["same"].IsChecked = false;
            Assert.Equal(new[] { "How", "Free" }, Headings(discord).Select(h => h.Text).ToArray());
        });

        [Fact]
        public void ASectionsOtherTitleIsCheckedAndTravelsFromTheHostBlock()
        {
            var bad = Windowed(@"{ ""title"": ""T"", ""sections"": [ { ""name"": ""Typo"", ""title"": ""X"", ""titleWhen"": ""nothere"" } ] }",
                @"{ ""key"": ""a"", ""default"": 1, ""section"": ""Real"" }");
            Assert.Contains(bad.Problems, p => p.Contains("'Typo'") && p.Contains("'nothere'"));
            Assert.Contains(bad.Problems, p => p.Contains("'Typo'") && p.Contains("no setting is in a section"));

            var json = new UiWindow("Demo")
                .Section("Free", title: "Free and Sales", titleWhen: "same", page: "discord")
                .Section("IGDB access", "source=igdb").ToJson();
            Assert.Equal(@"[{""name"":""Free"",""title"":""Free and Sales"",""titleWhen"":""same"",""page"":""discord""},{""name"":""IGDB access"",""showWhen"":""source=igdb""}]",
                json["sections"].ToString(Newtonsoft.Json.Formatting.None));
            var spec = SpecJson.Parse(json);
            Assert.Equal("discord", spec.SectionTitles.Single().Page);
            Assert.Equal("source=igdb", spec.SectionConditions["IGDB access"]);
            Assert.False(spec.SectionConditions.ContainsKey("Free"));
        }

        // ---- check boxes under a heading of the same name --------------------------------------------------------

        [Fact]
        public void AMultiFieldNamedLikeItsHeadingShowsNoCaptionOfItsOwn() => Sta(() =>
        {
            const string options = @"""kind"": ""multi"", ""options"": [ ""Steam"", ""GOG"" ]";
            var page = new Page(Settings(
                @"{ ""key"": ""left"", ""default"": """", ""label"": ""Platforms left out"", ""section"": ""Platforms left out"", " + options + " }",
                @"{ ""key"": ""other"", ""default"": """", ""label"": ""Stores"", ""section"": ""Platforms left out"", " + options + " }"));
            Assert.Equal("Steam | GOG", Shown(page.Fields["left"]));
            Assert.Equal("Stores | Steam | GOG", Shown(page.Fields["other"]));
        });

        // ---- a link that reads as words --------------------------------------------------------------------------

        [Fact]
        public void ANoteShowsALinkAsItsWordsOrAsItsAddress() => Sta(() =>
        {
            var opened = new List<string>();
            var note = Ui.NoteWithLinks("See [the Developer Portal](https://discord.com/developers/applications), or https://example.org/a. [Not a link] stays.", opened.Add);
            var links = note.Inlines.OfType<Hyperlink>().ToList();
            Assert.Equal(new[] { "the Developer Portal", "https://example.org/a" }, links.Select(l => string.Concat(l.Inlines.OfType<Run>().Select(r => r.Text))).ToArray());
            Assert.Equal("https://discord.com/developers/applications", (string)links[0].ToolTip);
            Assert.Equal("See , or . [Not a link] stays.", string.Concat(note.Inlines.OfType<Run>().Select(r => r.Text)));
            foreach (var link in links) link.DoClick();
            Assert.Equal(new[] { "https://discord.com/developers/applications", "https://example.org/a" }, opened.ToArray());
        });

        // ---- an answer that copies -------------------------------------------------------------------------------

        [Fact]
        public void AnAnswersCopyPutsOneFieldsValueIntoAnother() => Sta(() =>
        {
            var page = new Page(Settings(
                @"{ ""key"": ""freeKey"", ""default"": """", ""masked"": true }",
                @"{ ""key"": ""salesKey"", ""default"": """", ""masked"": true }"), @"{ ""freeKey"": ""saved"" }");
            page.Parts.Boxes["freeKey"].Text = "typed, not saved";
            LiveValues.Copy(page.Ctx.Fields, page.Parts, JObject.Parse(@"{ ""salesKey"": ""freeKey"", ""nothere"": ""freeKey"", ""freeKey"": ""nothere"" }"));
            Assert.Equal("typed, not saved", page.Parts.Boxes["salesKey"].Text);
            Assert.Equal("typed, not saved", Find<PasswordBox>(page.Fields["salesKey"]).Single().Password);
            Assert.Equal("typed, not saved", page.Parts.Boxes["freeKey"].Text);   // a key that is none copies nothing
            Assert.Equal("typed, not saved", (string)page.Saved()["salesKey"]);
        });
    }
}
