using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Newtonsoft.Json.Linq;
using Xunit;

namespace UIFramework.Tests
{
    /// <summary>2.0.0, languages: a code means its language in any case, from a region to its language and back; an
    /// en.json beside Streamer.bot corrects English; the picker fits its longest name; amounts are grouped as the
    /// language does; the emote tooltip is a window text; every language file the DLL carries is held to English's.</summary>
    public class Fixes1100LangTests
    {
        private static void Sta(Action body)
        {
            Exception failure = null;
            var thread = new Thread(() => { try { body(); } catch (Exception ex) { failure = ex; } });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            if (failure != null) throw failure;
        }

        /// <summary>Lets the dispatcher run what is queued (Loaded, layout) until it is idle.</summary>
        private static void Pump()
        {
            var frame = new DispatcherFrame();
            Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(() => frame.Continue = false));
            Dispatcher.PushFrame(frame);
        }

        private const string Plain =@"{ ""host"": ""demo"", ""version"": ""1"", ""settings"": [ { ""key"": ""maxBet"", ""default"": 100 } ] }";

        /// <summary>A window prepared with this language in its layout file (and a fixed one, if given).</summary>
        private static WindowSpec Prepared(string schemaJson, string layoutLanguage, string fixedLanguage = null)
        {
            string folder = Path.Combine(Path.GetTempPath(), "UIFramework.Tests", "lang-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            if (layoutLanguage != null)
                File.WriteAllText(Path.Combine(folder, LayoutFile.FileName), new JObject { ["language"] = layoutLanguage }.ToString());
            var schema = Schema.Parse(schemaJson);
            var spec = Entry.SpecFor(schema, Path.Combine(folder, "settings.json"));
            spec.Language = fixedLanguage;
            Entry.Prepare(spec, schema);
            return spec;
        }

        /// <summary>Language files in UIFramework\lang beside the test run for the time of the body, as a user puts them
        /// beside Streamer.bot; removed again afterwards.</summary>
        private static void WithExternal(Dictionary<string, string> files, Action body)
        {
            string folder = Conventions.ExternalFolder;
            bool made = !Directory.Exists(folder);
            Directory.CreateDirectory(folder);
            try
            {
                foreach (var file in files)
                    File.WriteAllText(Path.Combine(folder, file.Key), file.Value);
                body();
            }
            finally
            {
                foreach (var file in files)
                    File.Delete(Path.Combine(folder, file.Key));
                if (made)
                    Directory.Delete(folder, true);
                Text.Use(Conventions.Default.Lang);
            }
        }

        // ---- L2: a language code in any case, from a region to its language and back ---------------------------------

        [Fact]
        public void A_code_stands_for_its_language_in_any_case_and_across_a_region()
        {
            var codes = new[] { "en", "de", "es", "pt-BR" };
            Assert.Equal("pt-BR", Conventions.Match("pt-br", codes));
            Assert.Equal("pt-BR", Conventions.Match(" PT-BR ", codes));
            Assert.Equal("pt-BR", Conventions.Match("pt", codes));        // a language to its one regional file
            Assert.Equal("de", Conventions.Match("de-AT", codes));        // a region to its language
            Assert.Equal("en", Conventions.Match("EN-gb", codes));
            Assert.Equal("de", Conventions.Match("de", codes));
            Assert.Null(Conventions.Match("pt-PT", codes));                // another region is not this one
            Assert.Null(Conventions.Match("pt", new[] { "en", "pt-BR", "pt-PT" }));   // two regions: neither
            Assert.Equal("pt", Conventions.Match("pt-BR", new[] { "en", "pt" }));
            Assert.Null(Conventions.Match("nl", codes));
            Assert.Null(Conventions.Match("", codes));
            Assert.Null(Conventions.Match(null, codes));
        }

        [Fact]
        public void The_layout_files_language_is_found_in_another_case_or_region()
        {
            try
            {
                foreach (var written in new[] { "DE", "de-AT", "De" })
                {
                    var spec = Prepared(Plain, written);
                    Assert.Equal("de", spec.Conventions.Lang.Code);
                    Assert.Equal("Speichern", Text.T("btn.save"));   // before: English, the code was not "de" to the letter
                    Assert.Equal(new[] { "en", "de", "es" }, spec.Languages.Take(3));
                }
                Assert.Equal("en", Prepared(Plain, "pt-PT").Conventions.Lang.Code);   // nothing Portuguese here: English
            }
            finally { Text.Use(Conventions.Default.Lang); }
        }

        [Fact]
        public void A_games_own_words_in_another_spelling_go_over_the_conventions_of_that_language()
        {
            var schema = JObject.Parse(Plain);
            schema["lang"] = new JObject
            {
                ["DE"] = new JObject { ["keys"] = new JObject { ["maxBet"] = "Höchster Einsatz" } },
                ["nl"] = new JObject { ["ui"] = new JObject { ["btn.save"] = "Opslaan" } },
                ["NL"] = new JObject { ["ui"] = new JObject { ["btn.cancel"] = "Annuleren" } }
            };
            try
            {
                var spec = Prepared(schema.ToString(), "de");
                // One German in the picker, not "de" and "DE", and the game's words are on it.
                Assert.Equal(1, spec.Languages.Count(c => string.Equals(c, "de", StringComparison.OrdinalIgnoreCase)));
                Assert.Equal("Höchster Einsatz", spec.ResolvedSettings.Single(s => s.Key == "maxBet").Label);
                Assert.Equal("Speichern", Text.T("btn.save"));
                // Two codes of the game's own that differ in case are one language too, with both sets of words.
                Assert.Equal(1, spec.Languages.Count(c => string.Equals(c, "nl", StringComparison.OrdinalIgnoreCase)));
                var dutch = Prepared(schema.ToString(), "Nl");
                Assert.Equal("nl", dutch.Conventions.Lang.Code);
                Assert.Equal("Opslaan", Text.T("btn.save"));
                Assert.Equal("Annuleren", Text.T("btn.cancel"));
            }
            finally { Text.Use(Conventions.Default.Lang); }
        }

        [Fact]
        public void A_fixed_language_in_another_case_is_that_language()
        {
            try
            {
                var spec = Prepared(Plain, null, "DE");
                Assert.Equal("de", spec.Language);
                Assert.Equal("de", spec.Conventions.Lang.Code);
                Assert.DoesNotContain("DE", spec.Languages);
            }
            finally { Text.Use(Conventions.Default.Lang); }
        }

        [Fact]
        public void A_regional_file_beside_streamerbot_is_found_by_its_language_and_named_as_it_is_spelt() =>
            WithExternal(new Dictionary<string, string>
            {
                ["pt-BR.json"] = @"{ ""ui"": { ""btn.save"": ""Salvar"" } }",
                ["ES.json"] = @"{ ""ui"": { ""btn.cancel"": ""Cancelar ya"" } }"
            }, () =>
            {
                var offered = Conventions.Available();
                Assert.Contains("pt-BR", offered);
                Assert.DoesNotContain("ES", offered);   // the DLL's Spanish, corrected: not a second Spanish
                Assert.Equal("Cancelar ya", Conventions.Load("es").Lang.Ui["btn.cancel"]);

                foreach (var written in new[] { "pt", "PT-br", "pt-BR" })
                {
                    var spec = Prepared(Plain, written);
                    Assert.Equal("pt-BR", spec.Conventions.Lang.Code);
                    Assert.Equal("Salvar", Text.T("btn.save"));
                }
                Assert.Equal("pt-BR", Conventions.Load("pt").Lang.Code);   // what the update notice reads the layout file with

                // A game's Lang("pt") is the Portuguese there is: no "pt" of its own in the picker, its words over pt-BR.
                var schema = JObject.Parse(Plain);
                schema["lang"] = new JObject { ["pt"] = new JObject { ["ui"] = new JObject { ["btn.cancel"] = "Cancelar" } } };
                var game = Prepared(schema.ToString(), "pt-BR");
                Assert.DoesNotContain("pt", game.Languages);
                Assert.Equal("Salvar", Text.T("btn.save"));
                Assert.Equal("Cancelar", Text.T("btn.cancel"));

                // The picker shows the language the window is in, in the spelling the file has.
                Sta(() =>
                {
                    var layout = new LayoutFile(Path.Combine(Path.GetTempPath(), "UIFramework.Tests", "picker-" + Guid.NewGuid().ToString("N"), "settings.json"));
                    layout.Language = "pt-br";
                    var picker = Customize.LanguagePicker(game, layout, () => { });
                    Assert.Equal(Array.IndexOf(game.Languages.ToArray(), "pt-BR"), picker.SelectedIndex);
                });
            });

        // ---- L4: an en.json beside Streamer.bot corrects English ------------------------------------------------------

        [Fact]
        public void An_english_file_beside_streamerbot_corrects_the_dlls_english()
        {
            WithExternal(new Dictionary<string, string>
            {
                ["en.json"] = @"{ ""ui"": { ""btn.save"": ""Store"" }, ""keys"": { ""maxBet"": { ""label"": ""Highest bet"" } } }"
            }, () =>
            {
                var spec = Prepared(Plain, null);
                Assert.Equal("Store", Text.T("btn.save"));                    // before: "Save", the file was never read
                Assert.Equal("Highest bet", spec.ResolvedSettings.Single(s => s.Key == "maxBet").Label);
                Assert.Equal("Cancel", Text.T("btn.cancel"));                 // what it leaves out is the DLL's
                Assert.Equal("en", spec.Conventions.Lang.Code);
                Assert.False(spec.Conventions.Lang.Translates);
                Assert.Equal(1, spec.Languages.Count(c => c == "en"));
                // The DLL's English stays as it is, for every language to fall back on.
                Assert.Equal("Save", Conventions.Default.Lang.Ui["btn.save"]);
                Prepared(Plain, "de");
                Assert.Equal("Speichern", Text.T("btn.save"));
            });
            // Without the file: the English loaded once, as before.
            try { Assert.Same(Conventions.Default, Prepared(Plain, null).Conventions); }
            finally { Text.Use(Conventions.Default.Lang); }
        }

        [Fact]
        public void An_unreadable_english_file_beside_streamerbot_is_named() =>
            WithExternal(new Dictionary<string, string> { ["en.json"] = @"{ ""ui"": { ""btn.save"": " }, () =>
            {
                var spec = Prepared(Plain, null);
                Assert.Contains(spec.Problems, p => p.StartsWith("en.json could not be read"));
                Assert.Equal("Save", Text.T("btn.save"));
            });

        // ---- L1: the language picker fits its longest name ------------------------------------------------------------

        [Fact]
        public void The_language_picker_is_as_wide_as_its_longest_name() => Sta(() =>
        {
            ComboBox Shown(params string[] codes)
            {
                var spec = new WindowSpec { Languages = codes.ToList() };
                var layout = new LayoutFile(Path.Combine(Path.GetTempPath(), "UIFramework.Tests", "settings.json"));
                return Customize.LanguagePicker(spec, layout, () => { });
            }
            var portuguese = Shown("en", "de", "pt-BR");
            var short_ = Shown("en", "de", "es");
            Assert.Contains("Português (Brasil)", portuguese.Items.Cast<string>());
            // In a window with the settings window's font, beside each other as in the footer.
            var window = new Window
            {
                Width = 500, Height = 100, FontSize = 13, ShowInTaskbar = false, ShowActivated = false, Left = -20000, Top = -20000,
                WindowStartupLocation = WindowStartupLocation.Manual,
                Content = new StackPanel { Orientation = Orientation.Horizontal, Children = { portuguese, short_ } }
            };
            window.Show();
            try
            {
                Pump();
                window.UpdateLayout();
                var typeface = new Typeface(portuguese.FontFamily, portuguese.FontStyle, portuguese.FontWeight, portuguese.FontStretch);
                double text = new FormattedText("Português (Brasil)", System.Globalization.CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, typeface,
                    portuguese.FontSize, Brushes.Black, VisualTreeHelper.GetDpi(portuguese).PixelsPerDip).Width;
                // The combo shows its text 8 px from the left and keeps 26 px for the arrow (Ui.ComboTemplate).
                Assert.True(portuguese.ActualWidth - 8 - 26 >= text, "the name needs " + text + " px, the combo leaves " + (portuguese.ActualWidth - 34));
                Assert.Equal(110.0, short_.ActualWidth);   // short names: as wide as it always was
            }
            finally { window.Close(); }
        });

        // ---- L3: the thousands separator of a game without the setting, by language -----------------------------------

        [Theory]
        [InlineData("en", "11,500")]
        [InlineData("de", "11.500")]
        [InlineData("es", "11.500")]
        [InlineData("pt-BR", "11.500")]
        [InlineData("fr", "11 500")]       // before: 11.500; French groups with a space, here the plain one
        [InlineData("zz-qq", "11.500")]    // a code Windows does not know: the point, as before
        public void A_game_without_a_separator_setting_groups_amounts_as_its_language_does(string code, string shown) => Sta(() =>
        {
            try
            {
                Text.Use(new Lang { Code = code });
                var fields = new Fields(new WindowParts(), new JObject(), new JObject(), _ => { });
                Assert.Equal(shown, fields.Amount(11500));
            }
            finally { Text.Use(Conventions.Default.Lang); }
        });

        // ---- L5: the tooltip of a 7TV, BTTV or FFZ emote picture is a window text ----------------------------------------

        [Fact]
        public void The_tooltip_of_an_extensions_emote_is_in_the_windows_language()
        {
            try
            {
                Assert.Equal("A 7TV emote: chat only shows it as one to viewers with the 7TV extension.", Text.T("field.emote.extension", "7TV"));
                Text.Use(Conventions.Load("de").Lang);
                Assert.Equal("Ein BTTV-Emote: Der Chat zeigt es nur Zuschauern mit der BTTV-Erweiterung als Emote.", Text.T("field.emote.extension", "BTTV"));
                Text.Use(Conventions.Load("es").Lang);
                Assert.Contains("extensión FFZ", Text.T("field.emote.extension", "FFZ"));
            }
            finally { Text.Use(Conventions.Default.Lang); }
        }

        // ---- L6: every language file the DLL carries holds what the window relies on --------------------------------------

        private const string LangResource = "UIFramework.conventions.lang.";

        /// <summary>Every language file embedded in the DLL but English, by code: a new file is checked without being
        /// named anywhere here.</summary>
        public static IEnumerable<object[]> EmbeddedLanguages() =>
            typeof(Conventions).Assembly.GetManifestResourceNames()
                .Where(n => n.StartsWith(LangResource, StringComparison.Ordinal) && n.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                .Select(n => n.Substring(LangResource.Length, n.Length - LangResource.Length - ".json".Length))
                .Where(code => code != "en")
                .Select(code => new object[] { code });

        /// <summary>The file as it ships inside the DLL, not laid over English: an entry left out would pass unseen.</summary>
        private static JObject Raw(string code)
        {
            using (var stream = typeof(Conventions).Assembly.GetManifestResourceStream(LangResource + code + ".json"))
            {
                Assert.True(stream != null, code + ".json is not embedded in UIFramework.dll");
                using (var reader = new StreamReader(stream))
                    return JObject.Parse(reader.ReadToEnd());
            }
        }

        /// <summary>Every text of a section by key, a key's "label" and "help" apart.</summary>
        private static Dictionary<string, string> Entries(JObject root, string section)
        {
            var entries = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var p in (root[section] as JObject)?.Properties() ?? Enumerable.Empty<JProperty>())
            {
                if (p.Value is JObject o)
                    foreach (var part in o.Properties())
                        entries[p.Name + "." + part.Name] = (string)part.Value;
                else
                    entries[p.Name] = (string)p.Value;
            }
            return entries;
        }

        /// <summary>{0}, {updates}, {count} in a text, sorted, a repeated one as often as it occurs.</summary>
        private static string Placeholders(string text) =>
            string.Join(" ", System.Text.RegularExpressions.Regex.Matches(text ?? "", @"\{[^{}\s]+\}").Cast<System.Text.RegularExpressions.Match>()
                .Select(m => m.Value).OrderBy(v => v, StringComparer.Ordinal));

        [Fact]
        public void The_languages_checked_are_those_the_dll_carries()
        {
            var codes = EmbeddedLanguages().Select(d => (string)d[0]).ToList();
            Assert.Contains("de", codes);
            Assert.Contains("es", codes);
            Assert.All(codes, c => Assert.Contains(c, Conventions.Available()));   // and every one is offered
        }

        [Theory]
        [MemberData(nameof(EmbeddedLanguages))]
        public void Every_language_file_has_every_entry_of_english_with_its_placeholders(string code)
        {
            var english = Raw("en");
            var other = Raw(code);
            var wrong = new List<string>();
            foreach (var section in new[] { "pages", "framework", "keys", "placeholders", "ui" })
            {
                var theirs = Entries(english, section);
                var mine = Entries(other, section);
                Assert.True(theirs.Count > 0, section + " is empty in en.json");
                foreach (var entry in theirs)
                {
                    if (!mine.TryGetValue(entry.Key, out var text))
                        wrong.Add(section + "." + entry.Key + ": missing");
                    else if (string.IsNullOrWhiteSpace(text) && !string.IsNullOrWhiteSpace(entry.Value))
                        wrong.Add(section + "." + entry.Key + ": blank");
                    else if (Placeholders(text) != Placeholders(entry.Value))
                        wrong.Add(section + "." + entry.Key + ": placeholders " + Placeholders(text) + " where English has " + Placeholders(entry.Value));
                }
                wrong.AddRange(mine.Keys.Where(k => !theirs.ContainsKey(k)).Select(k => section + "." + k + ": not in en.json"));
            }
            Assert.True(wrong.Count == 0, code + ".json:\n" + string.Join("\n", wrong));
        }

        [Theory]
        [MemberData(nameof(EmbeddedLanguages))]
        public void Every_language_files_window_texts_keep_the_shape_the_code_puts_them_in(string code)
        {
            var english = Entries(Raw("en"), "ui");
            var mine = Entries(Raw(code), "ui");
            var wrong = new List<string>();
            // The file dialogs take export.filter as it is: label|*.json|label|*.*.
            foreach (var filter in new[] { ("en", english["export.filter"]), (code, mine.TryGetValue("export.filter", out var own) ? own : "") })
            {
                var parts = filter.Item2.Split('|');
                if (parts.Length != 4 || parts[1] != "*.json" || parts[3] != "*.*" || string.IsNullOrWhiteSpace(parts[0]) || string.IsNullOrWhiteSpace(parts[2]))
                    wrong.Add(filter.Item1 + ".json: export.filter is not label|*.json|label|*.*: " + filter.Item2);
            }
            // Texts the window strings together (" – Settings" after the title, "\nBetween 1 and 100%." after a help)
            // keep the spaces and line breaks at their edges, and every text as many line breaks as English.
            string Lead(string text) => text.Substring(0, text.Length - text.TrimStart().Length);
            string Tail(string text) => text.Substring(text.TrimEnd().Length);
            foreach (var entry in english)
            {
                if (!mine.TryGetValue(entry.Key, out var text) || text == null)
                    continue;   // missing: the test above says so
                if (Lead(text) != Lead(entry.Value) || Tail(text) != Tail(entry.Value))
                    wrong.Add(entry.Key + ": other spaces or line breaks at its edges than English");
                if (text.Count(c => c == '\n') != entry.Value.Count(c => c == '\n'))
                    wrong.Add(entry.Key + ": " + text.Count(c => c == '\n') + " line breaks where English has " + entry.Value.Count(c => c == '\n'));
            }
            Assert.True(wrong.Count == 0, code + ".json:\n" + string.Join("\n", wrong));
        }

        [Theory]
        [MemberData(nameof(EmbeddedLanguages))]
        public void Every_language_with_short_wordings_has_the_ones_german_has(string code)
        {
            if (!(Raw(code)["texts"] is JObject texts))
                return;   // a language without them: the declared wordings stay English
            var german = (JObject)Raw("de")["texts"];
            var missing = german.Properties().Select(p => p.Name).Where(k => texts[k] == null || string.IsNullOrWhiteSpace((string)texts[k])).ToList();
            var extra = texts.Properties().Select(p => p.Name).Where(k => german[k] == null).ToList();
            Assert.True(missing.Count == 0, code + ".json texts missing or blank: " + string.Join(", ", missing));
            Assert.True(extra.Count == 0, code + ".json texts German does not have: " + string.Join(", ", extra));
        }
    }
}
