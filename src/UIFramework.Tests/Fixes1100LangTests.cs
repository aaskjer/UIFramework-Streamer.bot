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
    /// <summary>1.10.0, languages: a code means its language in any case, from a region to its language and back.</summary>
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
            var spec = Entry.SpecFor(null, schema, Path.Combine(folder, "settings.json"));
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
    }
}
