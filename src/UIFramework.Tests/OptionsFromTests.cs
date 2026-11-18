using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows.Controls;
using System.Windows.Threading;
using Newtonsoft.Json.Linq;
using Xunit;

namespace UIFramework.Tests
{
    /// <summary>A dropdown whose options come from the project (<c>optionsFrom</c>): when it asks and with what, what
    /// it offers and keeps, and what the check says of a declaration that cannot work. The host block's side is in
    /// <see cref="HostBlockDeclarationTests"/>.</summary>
    public class OptionsFromTests : IDisposable
    {
        private const int Delay = 40;
        private readonly int _realDelay = SchemaPage.OptionsFromDelayMs;

        public OptionsFromTests() { SchemaPage.OptionsFromDelayMs = Delay; }
        public void Dispose() { SchemaPage.OptionsFromDelayMs = _realDelay; }

        private static void Sta(Action body)
        {
            Exception failure = null;
            var thread = new Thread(() => { try { body(); } catch (Exception ex) { failure = ex; } });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            if (failure != null) throw failure;
        }

        private static void Pump(int milliseconds)
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            do
            {
                var frame = new DispatcherFrame();
                Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(() => frame.Continue = false));
                Dispatcher.PushFrame(frame);
                Thread.Sleep(10);
            }
            while (watch.ElapsedMilliseconds < milliseconds);
        }

        /// <summary>Long enough for a change to be asked about and a quick answer to be in the dropdown.</summary>
        private static void Settle() => Pump(Delay + 160);

        private const string Head = @"{ ""host"": ""demo"", ""version"": ""v1"", ""settings"": [ ";
        private const string Country = @"{ ""key"": ""countryCode"", ""default"": ""DE"", ""kind"": ""options"", ""options"": [""DE"", ""FR""] }, ";
        private const string Region = @"{ ""key"": ""region"", ""default"": """", ""kind"": ""options"", ""optionsFrom"": ""countryCode"" }, ";
        private const string Bet = @"{ ""key"": ""bet"", ""default"": 10 } ] }";

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

        /// <summary>The project behind the window: names the options as told and keeps what it was asked with.</summary>
        private sealed class Project : NoHooks
        {
            private readonly Func<JObject, string> _options;
            public readonly List<(string Key, JObject Values)> Asked = new List<(string Key, JObject Values)>();
            public string GroupNames;
            public int GroupsAsked;
            public Project(Func<JObject, string> options = null) { _options = options; }
            public override string Options(string key, JObject settings)
            {
                lock (Asked) Asked.Add((key, settings));
                return _options?.Invoke(settings);
            }
            public override string Groups() { Interlocked.Increment(ref GroupsAsked); return GroupNames; }
        }

        private sealed class Lines : IHostLog
        {
            public readonly List<string> Warnings = new List<string>();
            public void Info(string message) { }
            public void Warn(string message) => Warnings.Add(message);
            public void Error(string message) { }
        }

        /// <summary>The regions of the country picked, in the three forms an answer may take.</summary>
        private static string Regions(JObject values) => (string)values["countryCode"] == "FR"
            ? @"[""BRE|Bretagne"", ""IDF|Île-de-France"", ""ALL|Everywhere""]"
            : @"[""BY|Bayern"", { ""value"": ""BE"", ""label"": ""Berlin"" }, ""HH"", ""ALL|Everywhere""]";

        private static void Set(PageContext ctx, string property, object value) =>
            typeof(PageContext).GetProperty(property).SetValue(ctx, value);

        /// <summary>The settings as fields on one page, wired as the shell wires them: <paramref name="stored"/> is what
        /// the file holds beside the defaults.</summary>
        private static (WindowParts Parts, StackPanel Page, JObject Saved) Built(WindowSpec spec, IHostHooks hooks, string stored = null, IHostLog log = null)
        {
            var saved = (JObject)spec.Defaults.DeepClone();
            if (stored != null)
                foreach (var property in JObject.Parse(stored).Properties())
                    saved[property.Name] = property.Value;
            var parts = new WindowParts();
            var ctx = Entry.ContextFor(spec, saved, parts, log, hooks);
            var page = new StackPanel();
            foreach (var setting in spec.ResolvedSettings)
                page.Children.Add(SchemaPage.Render(ctx, setting));
            Set(ctx, "LiveSettings", (Func<JObject>)(() =>
            {
                var live = (JObject)saved.DeepClone();
                FieldBinding.Save(live, page, transientToo: true);
                return live;
            }));
            foreach (var wire in ctx.AfterBuild)
                wire();
            return (parts, page, saved);
        }

        private static string[] Values(ComboBox combo) => combo.Items.OfType<ComboBoxItem>().Select(i => (string)i.Tag).ToArray();
        private static string[] Labels(ComboBox combo) => combo.Items.OfType<ComboBoxItem>().Select(i => (string)i.Content).ToArray();
        private static string Held(ComboBox combo) => (string)((ComboBoxItem)combo.SelectedItem).Tag;
        private static void Pick(ComboBox combo, string value) =>
            combo.SelectedItem = combo.Items.OfType<ComboBoxItem>().First(i => (string)i.Tag == value);

        // ---- When the project is asked ------------------------------------------------------------------------

        [Fact]
        public void TheProjectIsAskedWhenThePageIsBuiltWithTheValuesInTheWindow() => Sta(() =>
        {
            var project = new Project(Regions);
            var (parts, _, _) = Built(Prepared(Head + Country + Region + Bet), project, @"{ ""countryCode"": ""FR"", ""bet"": 25 }");
            var asked = Assert.Single(project.Asked);
            Assert.Equal("region", asked.Key);
            Assert.Equal("FR", (string)asked.Values["countryCode"]);
            Assert.Equal(25, (int)asked.Values["bet"]);
            // The answer's options, and above them the empty value the file holds: nothing is picked for the user.
            var region = parts.Combos["region"];
            Assert.Equal(new[] { "", "BRE", "IDF", "ALL" }, Values(region));
            Assert.Equal(new[] { "", "Bretagne", "Île-de-France", "Everywhere" }, Labels(region));
            Assert.Equal("", Held(region));
        });

        [Fact]
        public void ItIsAskedAgainWhenTheNamedFieldChangesAndNotWhenAnotherDoes() => Sta(() =>
        {
            var project = new Project(Regions);
            var (parts, _, _) = Built(Prepared(Head + Country + Region + Bet), project);
            Assert.Single(project.Asked);
            Assert.Equal(new[] { "", "BY", "BE", "HH", "ALL" }, Values(parts.Combos["region"]));
            Assert.Equal(new[] { "", "Bayern", "Berlin", "HH", "Everywhere" }, Labels(parts.Combos["region"]));

            parts.Boxes["bet"].Text = "25";
            Pick(parts.Combos["region"], "BE");
            Settle();
            Assert.Single(project.Asked);

            Pick(parts.Combos["countryCode"], "FR");
            // Not with the change itself: a moment after the last one.
            Assert.Single(project.Asked);
            Settle();
            Assert.Equal(2, project.Asked.Count);
            // With the values as they stand in the window, the unsaved ones too.
            Assert.Equal("FR", (string)project.Asked[1].Values["countryCode"]);
            Assert.Equal(25, (int)project.Asked[1].Values["bet"]);
            Assert.Equal("BE", (string)project.Asked[1].Values["region"]);
            Assert.Equal(new[] { "", "BRE", "IDF", "ALL" }, Values(parts.Combos["region"]));
        });

        [Fact]
        public void SeveralChangesInARowAskOnceAndThereAndBackAsksNothing() => Sta(() =>
        {
            var spec = Prepared(Head + @"{ ""key"": ""city"", ""default"": """" }, { ""key"": ""street"", ""default"": """", ""kind"": ""options"", ""optionsFrom"": ""city"" }, " + Bet);
            var project = new Project(values => new JArray((string)values["city"] + " lane").ToString());
            var (parts, _, _) = Built(spec, project);
            Assert.Single(project.Asked);

            foreach (string typed in new[] { "U", "Ul", "Ulm" })
                parts.Boxes["city"].Text = typed;
            Settle();
            Assert.Equal(2, project.Asked.Count);
            Assert.Equal("Ulm", (string)project.Asked[1].Values["city"]);
            Assert.Equal(new[] { "", "Ulm lane" }, Values(parts.Combos["street"]));

            // Back where the last question was asked from before the moment is over: the same question is not asked twice.
            parts.Boxes["city"].Text = "Ulm!";
            parts.Boxes["city"].Text = "Ulm";
            Settle();
            Assert.Equal(2, project.Asked.Count);
        });

        [Fact]
        public void AnEmptyTextAsksOnce() => Sta(() =>
        {
            var spec = Prepared(Head + Country + @"{ ""key"": ""region"", ""default"": """", ""kind"": ""options"", ""optionsFrom"": """" }, " + Bet);
            var project = new Project(Regions);
            var (parts, _, _) = Built(spec, project);
            Assert.Single(project.Asked);
            Assert.Equal(new[] { "", "BY", "BE", "HH", "ALL" }, Values(parts.Combos["region"]));

            Pick(parts.Combos["countryCode"], "FR");
            parts.Boxes["bet"].Text = "25";
            Pick(parts.Combos["region"], "HH");
            Settle();
            Assert.Single(project.Asked);
        });

        [Fact]
        public void GroupsOfferStreamerBotsGroupsWithoutAskingTheProject() => Sta(() =>
        {
            var spec = Prepared(Head + @"{ ""key"": ""vipGroup"", ""default"": """", ""kind"": ""options"", ""optionsFrom"": ""groups"" }, "
                + @"{ ""key"": ""modGroup"", ""default"": """", ""kind"": ""options"", ""optionsFrom"": ""groups"" }, " + Bet);
            var project = new Project(Regions) { GroupNames = @"[""VIPs"", ""Mods""]" };
            var (parts, _, _) = Built(spec, project, @"{ ""modGroup"": ""Helpers"" }");
            Assert.Empty(project.Asked);
            // One question for the window, whatever the number of fields.
            Assert.Equal(1, project.GroupsAsked);
            Assert.Equal(new[] { "", "Mods", "VIPs" }, Values(parts.Combos["vipGroup"]));
            Assert.Equal("", Held(parts.Combos["vipGroup"]));
            // A group that is gone from Streamer.bot stays the value until another is picked.
            Assert.Equal(new[] { "", "Mods", "VIPs", "Helpers" }, Values(parts.Combos["modGroup"]));
            Assert.Equal("Helpers", Held(parts.Combos["modGroup"]));
        });

        // ---- What the dropdown offers ---------------------------------------------------------------------------

        private const string Declared = @"{ ""key"": ""region"", ""default"": ""a"", ""kind"": ""options"", ""options"": [""a|Alpha"", ""b|Beta""], ""optionsFrom"": ""countryCode"" }, ";

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("[]")]
        [InlineData("null")]
        [InlineData("The project could not answer: boom")]
        [InlineData("no answer")]
        [InlineData("throw")]
        public void TheDeclaredOptionsStayTheChoiceWhenTheProjectNamesNone(string answer) => Sta(() =>
        {
            // No answer at all is the settings action's marker: the project's action is disabled, or it took too long.
            var project = new Project(values => answer == "throw" ? throw new InvalidOperationException("boom")
                : answer == "no answer" ? Patient.NoAnswer + ": the action is disabled" : answer);
            var log = new Lines();
            var (parts, _, _) = Built(Prepared(Head + Country + Declared + Bet), project, log: log);
            Assert.Single(project.Asked);
            Assert.Equal(new[] { "a", "b" }, Values(parts.Combos["region"]));
            Assert.Equal(new[] { "Alpha", "Beta" }, Labels(parts.Combos["region"]));
            Assert.Equal("a", Held(parts.Combos["region"]));
            // No answer at all and one that cannot be read leave a line in the log; a project that names none does not.
            bool unread = answer != null && answer.Length > 4;
            Assert.Equal(unread ? 1 : 0, log.Warnings.Count(w => w.Contains("'region'")));
        });

        [Fact]
        public void AnOldSettingsActionLeavesTheDeclaredOptions() => Sta(() =>
        {
            // One without Options (before stub 0.3.3): the bridge answers null.
            var (parts, _, _) = Built(Prepared(Head + Country + Declared + Bet), new ReflectionHost(new object()));
            Assert.Equal(new[] { "a", "b" }, Values(parts.Combos["region"]));
        });

        [Fact]
        public void TheDeclaredOptionsShowUntilTheAnswerIsThere() => Sta(() =>
        {
            var project = new Project(values => { Thread.Sleep(Patient.QuickMs + 250); return @"[""x|Xi"", ""a|Aleph""]"; });
            var (parts, _, _) = Built(Prepared(Head + Country + Declared + Bet), project);
            var region = parts.Combos["region"];
            int selections = 0;
            region.SelectionChanged += (sender, e) => selections++;
            // The window went on while the project is asked.
            Assert.Equal(new[] { "a", "b" }, Values(region));
            Pump(Patient.QuickMs + 700);
            Assert.Equal(new[] { "x", "a" }, Values(region));
            // What was selected stays selected, under the label the project gives it, and nothing hears of a change.
            Assert.Equal("a", Held(region));
            Assert.Equal("Aleph", (string)region.SelectionBoxItem);
            Assert.Equal(0, selections);
        });

        [Fact]
        public void AStoredValueNoOptionHasStaysAnEntryThroughARefresh() => Sta(() =>
        {
            var project = new Project(Regions);
            var (parts, page, _) = Built(Prepared(Head + Country + Region + Bet), project, @"{ ""region"": ""BY"" }");
            var region = parts.Combos["region"];
            var baseline = new Dictionary<object, string>();
            FieldBinding.Snapshot(page, baseline);
            int selections = 0;
            region.SelectionChanged += (sender, e) => selections++;
            Assert.Equal("BY", Held(region));
            Assert.Equal("Bayern", (string)region.SelectionBoxItem);

            Pick(parts.Combos["countryCode"], "FR");
            Settle();
            // France has no Bayern: it stays as an entry of its own behind the options, reading as it did, and selected.
            Assert.Equal(new[] { "", "BRE", "IDF", "ALL", "BY" }, Values(region));
            Assert.Equal("Bayern", Labels(region).Last());
            Assert.Equal("BY", Held(region));
            Assert.Equal(0, selections);
            Assert.Equal(new[] { "countryCode" }, FieldBinding.ChangedKeys(page, baseline).ToArray());
            var written = new JObject();
            FieldBinding.Save(written, page);
            Assert.Equal("BY", (string)written["region"]);

            // A value the project never named reads as it is stored.
            var (other, _, _) = Built(Prepared(Head + Country + Region + Bet), new Project(Regions), @"{ ""region"": ""Atlantis"" }");
            Assert.Equal(new[] { "", "BY", "BE", "HH", "ALL", "Atlantis" }, Values(other.Combos["region"]));
            Assert.Equal("Atlantis", Labels(other.Combos["region"]).Last());
            Assert.Equal("Atlantis", Held(other.Combos["region"]));
        });

        [Fact]
        public void APickStaysWhileTheNewOptionsHaveItAndGoesBackToTheStoredValueWhenTheyDoNot() => Sta(() =>
        {
            var project = new Project(Regions);
            var (parts, _, _) = Built(Prepared(Head + Country + Region + Bet), project, @"{ ""region"": ""HH"" }");
            var region = parts.Combos["region"];
            Pick(region, "ALL");
            int selections = 0;
            region.SelectionChanged += (sender, e) => selections++;

            Pick(parts.Combos["countryCode"], "FR");
            Settle();
            Assert.Equal(new[] { "", "BRE", "IDF", "ALL", "HH" }, Values(region));
            Assert.Equal("ALL", Held(region));
            Assert.Equal(0, selections);

            Pick(region, "BRE");
            Pick(parts.Combos["countryCode"], "DE");
            Settle();
            // Germany has no Bretagne: back to what the file holds, said once to whatever follows the field.
            Assert.Equal(new[] { "", "BY", "BE", "HH", "ALL" }, Values(region));
            Assert.Equal("HH", Held(region));
            Assert.Equal(2, selections);
        });

        [Fact]
        public void ALateAnswerDoesNotReplaceANewerOne() => Sta(() =>
        {
            // The question for France waits behind another request of the settings action; the next one is answered at once.
            var project = new Project(values =>
            {
                if ((string)values["countryCode"] == "FR") Thread.Sleep(Patient.QuickMs + 500);
                return Regions(values);
            });
            var (parts, _, _) = Built(Prepared(Head + Country + Region + Bet), project);
            var region = parts.Combos["region"];
            Pick(parts.Combos["countryCode"], "FR");
            Settle();
            Assert.Equal(2, project.Asked.Count);
            Pick(parts.Combos["countryCode"], "DE");
            Settle();
            Assert.Equal(3, project.Asked.Count);
            Assert.Equal(new[] { "", "BY", "BE", "HH", "ALL" }, Values(region));
            Pump(Patient.QuickMs + 900);
            // France's answer came last and was put over Germany's, which the window shows.
            Assert.Equal(3, project.Asked.Count);
            Assert.Equal(new[] { "", "BY", "BE", "HH", "ALL" }, Values(region));
        });

        // ---- Everything a dropdown does -------------------------------------------------------------------------

        [Fact]
        public void AResetAnImportAndAnAnswersSetGoThroughTheField() => Sta(() =>
        {
            var project = new Project(Regions);
            var spec = Prepared(Head + Country + Region + Bet);
            var (parts, page, _) = Built(spec, project, @"{ ""region"": ""BY"" }");
            var region = parts.Combos["region"];

            // An answer's `set` picks an option, as on any dropdown.
            Assert.True(LiveValues.Set(parts, "region", "be"));
            Assert.Equal("BE", Held(region));
            Assert.False(LiveValues.Set(parts, "region", "Atlantis"));

            // A reset finds the default, an entry of its own where no option has it.
            FieldBinding.ResetToDefaults(page, spec.Defaults);
            Assert.Equal("", Held(region));
            Assert.Equal("", FieldBinding.ControlValue(region));

            // An import takes an option as the dropdown has it, and a value it does not offer right now as text: the
            // file may hold one (another country's region), and the window then keeps it as an entry of its own.
            Assert.Null(FieldBinding.AsSaved(page, "region", "hh", out var saved));
            Assert.Equal("HH", (string)saved);
            Assert.Null(FieldBinding.AsSaved(page, "region", "BRE", out saved));
            Assert.Equal("BRE", (string)saved);
            Assert.NotNull(FieldBinding.AsSaved(page, "region", new JArray("BRE"), out _));
            // A dropdown with its own options still takes those only.
            Assert.NotNull(FieldBinding.AsSaved(page, "countryCode", "IT", out _));
        });

        [Fact]
        public void AnAnswersOptionsAreReadInEveryForm()
        {
            var options = SchemaPage.OfferedOptions(@"[ ""a"", ""b|Beta"", { ""value"": ""c"", ""label"": ""Gamma"" }, { ""value"": 4 }, 5, 0.5, { ""label"": ""no value"" }, null, [ ""x"" ], true ]");
            Assert.Equal(new[] { "a", "b", "c", "4", "5", "0.5" }, options.Select(o => o.Value).ToArray());
            Assert.Equal(new[] { "a", "Beta", "Gamma", "4", "5", "0.5" }, options.Select(o => o.Label).ToArray());
            Assert.Empty(SchemaPage.OfferedOptions(null));
            Assert.Empty(SchemaPage.OfferedOptions("  "));
            Assert.Empty(SchemaPage.OfferedOptions("[]"));
            Assert.Null(SchemaPage.OfferedOptions("no JSON"));
            Assert.Null(SchemaPage.OfferedOptions(@"{ ""value"": ""a"" }"));
        }

        // ---- What the check names -------------------------------------------------------------------------------

        [Fact]
        public void ADeclarationThatCannotWorkIsNamed()
        {
            var spec = Prepared(Head + Country
                + @"{ ""key"": ""nick"", ""default"": """", ""optionsFrom"": ""countryCode"" }, "
                + @"{ ""key"": ""town"", ""default"": """", ""kind"": ""options"", ""optionsFrom"": ""county"" }, "
                + @"{ ""key"": ""self"", ""default"": """", ""kind"": ""options"", ""optionsFrom"": ""self"" }, " + Bet);
            Assert.Contains(spec.Problems, p => p.StartsWith("'nick' has optionsFrom, which only a dropdown has (kind options); it does nothing on a string field."));
            Assert.Contains(spec.Problems, p => p.StartsWith("'town' takes its options from 'county' (optionsFrom), which is not a setting"));
            Assert.Contains(spec.Problems, p => p.StartsWith("'self' names itself in optionsFrom"));
            Assert.Equal(3, spec.Problems.Count);
        }

        [Fact]
        public void ADeclarationThatWorksIsNotNamed()
        {
            // No options of its own, a field, Streamer.bot's groups, the empty text; and a condition on a value the
            // project names, which the declared options do not have.
            var spec = Prepared(Head + Country + Region
                + @"{ ""key"": ""vipGroup"", ""default"": """", ""kind"": ""options"", ""optionsFrom"": ""groups"" }, "
                + @"{ ""key"": ""preset"", ""default"": ""a"", ""kind"": ""options"", ""options"": [""a""], ""optionsFrom"": """" }, "
                + @"{ ""key"": ""capital"", ""default"": """", ""showWhen"": ""preset=b && region=BE"" }, " + Bet);
            Assert.Empty(spec.Problems);

            // "groups" is Streamer.bot's, also beside a setting of that name: said, since the project is not asked.
            var taken = Prepared(Head + @"{ ""key"": ""groups"", ""default"": """" }, { ""key"": ""vipGroup"", ""default"": """", ""kind"": ""options"", ""optionsFrom"": ""groups"" }, " + Bet);
            Assert.Contains("Streamer.bot's groups, not for the setting 'groups'", Assert.Single(taken.Problems));
        }
    }
}
