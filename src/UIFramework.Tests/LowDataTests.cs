using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using Newtonsoft.Json.Linq;
using Xunit;

namespace UIFramework.Tests
{
    /// <summary>Conditions compare stored values shown or hidden, the checks name what never holds, a huge number
    /// does not break a page, a save is not stopped by fields it never writes, the Messages page keeps its keys.</summary>
    public class LowDataTests
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

        private static WindowSpec Spec(string settings, string name, string fields = @"""*""")
        {
            var schema = Schema.Parse(JObject.Parse(@"{ ""host"": ""demo"", ""version"": ""v1"", ""settingsFile"": ""x"", ""settings"": [ " + settings + @" ],
                ""window"": { ""title"": ""T"", ""pages"": [ { ""id"": ""game"", ""fields"": [" + fields + @"] } ] } }"));
            var spec = Entry.SpecFor(null, schema, Path.Combine(Path.GetTempPath(), "UIFramework.Tests", name, "settings.json"));
            Entry.Prepare(spec, schema);
            return spec;
        }

        [Fact]
        public void A_condition_compares_the_stored_value_shown_or_hidden() => Sta(() =>
        {
            var defaults = JObject.Parse(@"{ ""pct"": 0.5, ""rounds"": 0, ""words"": [] }");
            var saved = JObject.Parse(@"{ ""pct"": 0.6, ""rounds"": 2147483647, ""words"": [] }");

            // Shown: a percent typed as 60 is stored as 0.6, an "unlimited" 0 as int.MaxValue.
            var parts = new WindowParts();
            var shown = new Fields(parts, saved, defaults, _ => { });
            shown.PercentField("Pct", "pct", 0.01, "");
            shown.IntBox("Rounds", "rounds", "", unlimitedAtZero: true);
            Assert.Equal("60", parts.Boxes["pct"].Text);
            Assert.Equal("0", parts.Boxes["rounds"].Text);

            // Hidden: the same values read from the file.
            var hiddenParts = new WindowParts();
            var hidden = new Fields(hiddenParts, saved, defaults, _ => { });

            foreach (var (f, p) in new[] { (shown, parts), (hidden, hiddenParts) })
            {
                Assert.True(Requirement.Holds(f, p, "pct", ">=", "0.5"));
                Assert.False(Requirement.Holds(f, p, "pct", ">=", "50"));
                Assert.True(Requirement.Holds(f, p, "rounds", ">", "1000"));
            }

            // An empty list is empty; one with entries is not.
            Assert.True(Requirement.Holds(hidden, hiddenParts, Requirement.Parse("!words")[0]));
            Assert.False(Requirement.Holds(hidden, hiddenParts, Requirement.Parse("words")[0]));
            var filled = new Fields(new WindowParts(), JObject.Parse(@"{ ""words"": [ ""a"" ] }"), defaults, _ => { });
            Assert.True(Requirement.Holds(filled, new WindowParts(), Requirement.Parse("words")[0]));
        });

        [Fact]
        public void A_slider_on_a_scale_is_no_problem_and_an_order_comparison_on_text_is()
        {
            var spec = Spec(@"{ ""key"": ""level"", ""default"": 3, ""kind"": ""slider"", ""min"": 1, ""max"": 5, ""step"": 1 },
                              { ""key"": ""slots"", ""default"": 4, ""kind"": ""options"", ""options"": [ { ""value"": ""3"", ""label"": ""3"" }, { ""value"": ""4"", ""label"": ""4"" } ] },
                              { ""key"": ""mode"", ""default"": ""a"" },
                              { ""key"": ""x"", ""default"": 1, ""requires"": ""slots>=4"" },
                              { ""key"": ""y"", ""default"": 1, ""requires"": ""mode>3"" },
                              { ""key"": ""z"", ""default"": 1, ""showWhen"": ""level<high"" }", "low-checks");
            Assert.DoesNotContain(spec.Problems, p => p.Contains("'level' is a slider"));
            Assert.DoesNotContain(spec.Problems, p => p.Contains("slots>=4"));
            Assert.Contains(spec.Problems, p => p.Contains("'mode>3'") && p.Contains("holds no number"));
            Assert.Contains(spec.Problems, p => p.Contains("'level<high'") && p.Contains("is no number"));

            var plain = Spec(@"{ ""key"": ""level"", ""default"": 3, ""kind"": ""slider"" }", "low-checks-plain");
            Assert.Contains(plain.Problems, p => p.Contains("'level' is a slider without options"));
        }

        [Fact]
        public void A_number_with_more_digits_than_a_long_breaks_nothing()
        {
            var huge = JObject.Parse(@"{ ""x"": 123456789012345678901234 }")["x"];
            Assert.True(FieldBinding.TryNumber(huge, out double d));
            Assert.True(d > 1e23);
            Assert.False(Setting.FitsInt(huge));
            Assert.Equal(long.MaxValue.ToString(System.Globalization.CultureInfo.InvariantCulture), FieldBinding.Display(new FieldTag("x", FieldKind.Long), huge));
        }

        [Fact]
        public void A_save_is_not_stopped_by_a_field_it_never_writes_or_one_its_page_hides() => Sta(() =>
        {
            var parts = new WindowParts();
            var defaults = JObject.Parse(@"{ ""shown"": 1, ""hidden"": 1, ""bet"": 1, ""other"": 1 }");
            var fields = new Fields(parts, (JObject)defaults.DeepClone(), defaults, _ => { });
            var page = new StackPanel();
            page.Children.Add(fields.IntField("Shown", "shown", ""));
            var hiddenRow = new Border { Child = fields.IntField("Hidden", "hidden", ""), Visibility = Visibility.Collapsed };
            page.Children.Add(hiddenRow);
            page.Children.Add(fields.IntField("Bet", "bet", ""));
            ((FieldTag)parts.Boxes["bet"].Tag).Transient = true;
            // Another page, not the one showing: still checked.
            var host = new Grid();
            var otherPage = new ScrollViewer { Content = new StackPanel(), Visibility = Visibility.Collapsed };
            ((StackPanel)otherPage.Content).Children.Add(fields.IntField("Other", "other", ""));
            host.Children.Add(new ScrollViewer { Content = page });
            host.Children.Add(otherPage);
            foreach (var key in new[] { "shown", "hidden", "bet", "other" })
                parts.Boxes[key].Text = "";

            var invalid = FieldBinding.InvalidNumberFields(host);
            Assert.Equal(2, invalid.Count);
            Assert.Contains(invalid, i => i.StartsWith("Shown"));
            Assert.Contains(invalid, i => i.StartsWith("Other"));
        });

        [Fact]
        public void A_page_pattern_leaves_the_sender_switches_to_the_Messages_page()
        {
            const string settings = @"{ ""key"": ""useBot"", ""default"": true },
                                      { ""key"": ""bet"", ""default"": 1 },
                                      { ""key"": ""msgWin"", ""default"": ""{user} wins"", ""kind"": ""template"" }";
            var spec = Spec(settings, "low-claim");
            Assert.NotEqual("game", spec.ResolvedSettings.First(s => s.Key == "useBot").Page);
            Assert.Equal("game", spec.ResolvedSettings.First(s => s.Key == "bet").Page);
            Assert.DoesNotContain(spec.Problems, p => p.Contains("'useBot'"));

            var named = Spec(settings, "low-claim-named", @"""useBot"", ""bet""");
            Assert.Contains(named.Problems, p => p.Contains("'useBot'") && p.Contains("Messages"));
        }

        [Fact]
        public void The_placeholder_help_follows_a_language_switch()
        {
            var schema = Schema.Parse(JObject.Parse(@"{ ""host"": ""demo"", ""version"": ""v1"", ""settingsFile"": ""x"",
                ""settings"": [ { ""key"": ""msgWin"", ""default"": ""{user} wins"", ""kind"": ""template"" } ], ""lang"": { ""de"": { } } }"));
            var spec = Entry.SpecFor(null, schema, Path.Combine(Path.GetTempPath(), "UIFramework.Tests", "low-tokens", "settings.json"));
            try
            {
                spec.Language = "de";
                Entry.Prepare(spec, schema);
                string german = spec.TokenHelp("msgWin", "user");
                spec.Language = "en";
                Entry.Prepare(spec, schema);
                Assert.NotEqual(german, spec.TokenHelp("msgWin", "user"));
                Assert.Equal(Conventions.Default.Lang.Placeholder("user"), spec.TokenHelp("msgWin", "user"));

                // A host's own help stays.
                spec.TokenHelp = (key, token) => "mine";
                Entry.Prepare(spec, schema);
                Assert.Equal("mine", spec.TokenHelp("msgWin", "user"));
            }
            finally { Text.Use(Conventions.Default.Lang); }
        }
    }
}
