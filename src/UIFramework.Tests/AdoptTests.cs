using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using Newtonsoft.Json.Linq;
using Xunit;

namespace UIFramework.Tests
{
    /// <summary>The values a version before the window kept in its action (Set Argument sub-actions) come back once:
    /// read from Streamer.bot's actions or its backups while there is no settings file, converted to each setting's
    /// kind, shown as unsaved changes, and never written to the log. The actions here are invented, in the shape
    /// Streamer.bot 1.0.7 writes them.</summary>
    public class AdoptTests : IDisposable
    {
        private const string OldAction = "3f6c1a52-8b0e-4d47-9a61-c2e5d7f04b18";
        private const string OtherAction = "a94d2e70-15c3-4f8b-b6d2-07e3c9815f6a";
        private const string WonMessage = "5d80b7e4-2c19-4a63-8f05-e1b6a3d92c47";
        private const string Secret = "https://hooks.example.test/T0P-S3CRET";

        private readonly string _root;
        private readonly string _folderBefore = Adoption.Folder;

        public AdoptTests()
        {
            // A Streamer.bot folder of the test's own.
            _root = Path.Combine(Path.GetTempPath(), "UIFramework.Tests", "adopt-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
        }

        public void Dispose()
        {
            Adoption.Folder = _folderBefore;
            Text.Use(Conventions.Default.Lang);
            try { Directory.Delete(_root, true); } catch (Exception) { }
        }

        // ---- Where the old action is found ---------------------------------------------------------------------

        [Fact]
        public void TheValuesAreFoundInStreamerBotsActions()
        {
            Live(AnAction(OtherAction, SetArg("stake", "99")), AnAction(OldAction, SetArg("stake", "25"), SetArg("coinName", "Zorkmids"), SetArg("unknownToTheTable", "x")));
            var log = new Lines();
            var adopted = Adoption.Read(Spec(Old(OldAction)), _root, log);

            Assert.Equal(25, (int)adopted.Values["stake"]);
            Assert.Equal("Zorkmids", (string)adopted.Values["coinName"]);
            Assert.Equal(2, adopted.Values.Count);                                  // an argument the table does not know is not taken
            Assert.False(adopted.From["stake"].Backup);
            Assert.Equal(@"data\actions.json", adopted.From["stake"].File);
            Assert.Contains(log.All, l => l.Contains("2 found for the action " + OldAction + @" in data\actions.json"));
            Assert.Equal("2 values taken over from Streamer.bot's actions", adopted.Note(new[] { "stake", "coinName" }, out string why));
            Assert.Contains(@"data\actions.json", why);
        }

        [Fact]
        public void OfTwoBackupsTheNewestIsRead()
        {
            // The old action was deleted before the import: Streamer.bot's actions no longer have it.
            Live(AnAction(OtherAction, SetArg("stake", "99")));
            Backup("backup-20261101-09150012345-1.0.7.zip", AnAction(OldAction, SetArg("stake", "5")));
            Backup("backup-20261105-18300054321-1.0.7.zip", AnAction(OldAction, SetArg("stake", "25")));
            var adopted = Adoption.Read(Spec(Old(OldAction)), _root, null);

            Assert.Equal(25, (int)adopted.Values["stake"]);
            var from = adopted.From["stake"];
            Assert.True(from.Backup);
            Assert.Equal(@"backup\backup-20261105-18300054321-1.0.7.zip", from.File);
            Assert.Equal(new DateTime(2026, 11, 5, 18, 30, 0), from.Date);
            Assert.Equal("1 value taken over from the backup of 2026-11-05", adopted.Note(new[] { "stake" }, out _));
        }

        [Fact]
        public void AnActionTheImportReplacedIsTakenFromTheBackupBeforeIt()
        {
            // The new version's import replaced the action under its id: it is there, without its Set Arguments, and so
            // it is in the backup made since. The one before still has the values.
            Live(AnAction(OldAction, Code()));
            Backup("backup-20261105-18300054321-1.0.7.zip", AnAction(OldAction, Code()));
            Backup("backup-20261101-09150012345-1.0.7.zip", AnAction(OldAction, SetArg("stake", "25")));
            Backup("backup-20261020-07000011111-1.0.7.zip", AnAction(OldAction, SetArg("stake", "5")));
            var adopted = Adoption.Read(Spec(Old(OldAction)), _root, null);

            Assert.Equal(25, (int)adopted.Values["stake"]);
            Assert.Equal(@"backup\backup-20261101-09150012345-1.0.7.zip", adopted.From["stake"].File);
        }

        [Fact]
        public void OnlyTheLastBackupsAreLookedAt()
        {
            // A window that finds nothing looks again at every open until its first save: not through every backup there is.
            Backup("backup-20260901-08000000000-1.0.7.zip", AnAction(OldAction, SetArg("stake", "25")));
            for (int day = 1; day <= Adoption.BackupsLookedAt; day++)
                Backup("backup-202610" + day.ToString("00") + "-08000000000-1.0.7.zip", AnAction(OtherAction, SetArg("stake", "99")));
            var log = new Lines();
            Assert.Null(Adoption.Read(Spec(Old(OldAction)), _root, log));
            Assert.Contains(log.All, l => l.Contains("in " + Adoption.BackupsLookedAt + " backup(s)"));

            File.Delete(Path.Combine(_root, "backup", "backup-20261001-08000000000-1.0.7.zip"));
            Assert.Equal(25, (int)Adoption.Read(Spec(Old(OldAction)), _root, null).Values["stake"]);
        }

        [Fact]
        public void ABackupWithoutActionsOrADamagedOneIsPassedOver()
        {
            Backup("backup-20261107-11482408412-1.0.7.zip");                                        // settings.json only, as a first start writes it
            File.WriteAllText(Path.Combine(_root, "backup", "backup-20261106-10000000000-1.0.7.zip"), "not a zip");
            Backup("backup-20261101-09150012345-1.0.7.zip", AnAction(OldAction, SetArg("stake", "25")));
            var log = new Lines();
            var adopted = Adoption.Read(Spec(Old(OldAction)), _root, log);

            Assert.Equal(25, (int)adopted.Values["stake"]);
            Assert.Contains(log.All, l => l.StartsWith("warn") && l.Contains("backup-20261106-10000000000-1.0.7.zip could not be read"));

            // Nowhere at all: no values, one line, and the window opens on its defaults.
            Assert.Null(Adoption.Read(Spec(Old(OtherAction)), _root, log));
            Assert.Contains(log.All, l => l.Contains("none found for the action " + OtherAction) && l.Contains("3 backup(s)"));
            Assert.Null(Adoption.Read(Spec(Old(OldAction)), Path.Combine(_root, "no such folder"), null));
        }

        [Fact]
        public void EachOldActionIsTakenFromWhereItIs()
        {
            Live(AnAction(OldAction, SetArg("stake", "25"), SetArg("coinName", "Zorkmids")));
            Backup("backup-20261105-18300054321-1.0.7.zip", AnAction(OtherAction, SetArg("coinName", "Quatloos"), SetArg("active", true)));
            var adopted = Adoption.Read(Spec(Old(OldAction) + ", " + Old(OtherAction)), _root, null);

            Assert.Equal(25, (int)adopted.Values["stake"]);
            Assert.Equal("Zorkmids", (string)adopted.Values["coinName"]);          // both have it: the action declared first
            Assert.True((bool)adopted.Values["active"]);
            Assert.False(adopted.From["coinName"].Backup);
            Assert.True(adopted.From["active"].Backup);
            Assert.Equal("2 values taken over from Streamer.bot's actions · 1 value taken over from the backup of 2026-11-05",
                adopted.Note(adopted.Values.Properties().Select(p => p.Name), out _));
        }

        // ---- Which sub-actions count ---------------------------------------------------------------------------

        [Fact]
        public void ASubActionThatIsOffDoesNotCount()
        {
            Live(AnAction(OldAction,
                SetArg("stake", "25"),
                SetArg("stake", "500", enabled: false),                               // an old try, left switched off
                SetArg("coinName", "Zorkmids", enabled: false),
                Group("Off", false, SetArg("active", true)),
                Say(10, WonMessage, "an older wording", enabled: false)));
            var adopted = Adoption.Read(Spec(Old(OldAction, "#" + WonMessage + ">msgWon")), _root, null);

            Assert.Equal(25, (int)adopted.Values["stake"]);
            Assert.Single(adopted.Values.Properties());
        }

        [Fact]
        public void SetArgumentsInsideGroupsCount()
        {
            Live(AnAction(OldAction,
                Comment("Settings"),
                Group("Bets", true, SetArg("stake", "25"), Group("Words", true, SetArg("coinName", "Zorkmids"), Say(4001, WonMessage, "{user} takes the pot"))),
                Code()));
            var adopted = Adoption.Read(Spec(Old(OldAction, "#" + WonMessage + ">msgWon")), _root, null);

            Assert.Equal(25, (int)adopted.Values["stake"]);
            Assert.Equal("Zorkmids", (string)adopted.Values["coinName"]);
            Assert.Equal("{user} takes the pot", (string)adopted.Values["msgWon"]);
        }

        [Fact]
        public void TheLastSetArgumentOfANameCounts()
        {
            // As when the action ran: a later Set Argument overwrote an earlier one.
            Live(AnAction(OldAction, SetArg("stake", "5"), Group("Later", true, SetArg("stake", "25"))));
            Assert.Equal(25, (int)Adoption.Read(Spec(Old(OldAction)), _root, null).Values["stake"]);
        }

        // ---- Which setting a value goes to ---------------------------------------------------------------------

        [Fact]
        public void AMappingRenamesAnOldArgument()
        {
            Live(AnAction(OldAction, SetArg("einsatz", "25"), SetArg("name der punkte", "Zorkmids"), SetArg("coinName", "not this one")));
            var adopted = Adoption.Read(Spec(Old(OldAction, "einsatz>stake", "name der punkte>coinName")), _root, null);

            Assert.Equal(25, (int)adopted.Values["stake"]);
            Assert.Equal("Zorkmids", (string)adopted.Values["coinName"]);            // what the window says outright comes first
        }

        [Fact]
        public void AnOldKeyOfTheTableNeedsNoMapping()
        {
            // OldStake is an alias of stake; OddsFraction one of odds with a factor (a fraction, a percent now);
            // RoundMinutes one of the duration, which is kept in seconds.
            Live(AnAction(OldAction, SetArg("OldStake", "25"), SetArg("OddsFraction", "0,35"), SetArg("RoundMinutes", "1.5")));
            var adopted = Adoption.Read(Spec(Old(OldAction)), _root, null);

            Assert.Equal(25, (int)adopted.Values["stake"]);
            Assert.Equal(35.0, (double)adopted.Values["odds"]);
            Assert.Equal(90, (int)adopted.Values["roundTime"]);

            // The key itself wins over an old key left beside it, as in the settings file.
            Live(AnAction(OldAction, SetArg("OldStake", "5"), SetArg("stake", "25")));
            Assert.Equal(25, (int)Adoption.Read(Spec(Old(OldAction)), _root, null).Values["stake"]);
        }

        [Fact]
        public void ASendMessagesTextGoesToTheSettingItsIdNames()
        {
            foreach (int type in new[] { 10, 4001, 35001 })                          // Twitch, YouTube, Kick
            {
                Live(AnAction(OldAction, Say(type, Guid.NewGuid().ToString(), "another message"), Say(type, WonMessage, "{user} takes the pot")));
                var adopted = Adoption.Read(Spec(Old(OldAction, "#" + WonMessage + ">msgWon")), _root, null);
                Assert.Equal("{user} takes the pot", (string)adopted.Values["msgWon"]);
                Assert.Single(adopted.Values.Properties());
            }
            // A sub-action of another kind is no message, whatever it carries under that id.
            Live(AnAction(OldAction, Say(77777, WonMessage, "{user} takes the pot")));
            Assert.Null(Adoption.Read(Spec(Old(OldAction, "#" + WonMessage + ">msgWon")), _root, null));
        }

        // ---- A value in the setting's kind ---------------------------------------------------------------------

        [Fact]
        public void AValueBecomesTheSettingsKind()
        {
            var spec = Spec(Old(OldAction));
            JToken As(string key, object value, double factor = 1.0) => Adoption.ToKind(spec.ResolvedSettings.First(s => s.Key == key), JToken.FromObject(value), factor);

            // A switch: true/false, yes/no, on/off, ja/nein, 1/0, as text or as the value Streamer.bot stored.
            foreach (object on in new object[] { true, "true", "True", " yes ", "YES", "On", "ja", "1", 1 })
                Assert.True((bool)As("active", on), on + " is on");
            foreach (object off in new object[] { false, "false", "FALSE", "no", "off", "Nein", "0", 0 })
                Assert.False((bool)As("active", off), off + " is off");
            Assert.Null(As("active", "maybe"));
            Assert.Null(As("active", ""));
            Assert.Null(As("active", "2"));

            // A whole number: digits. A fraction, text and a number with a thousands mark are none.
            Assert.Equal(JTokenType.Integer, As("stake", "25").Type);
            Assert.Equal(25, (int)As("stake", " 25 "));
            Assert.Equal(25, (int)As("stake", 25));
            Assert.Equal(25, (int)As("stake", 25.0));
            Assert.Equal(-3, (int)As("stake", "-3"));
            Assert.Null(As("stake", "2.5"));
            Assert.Null(As("stake", "1.000"));
            Assert.Null(As("stake", "twenty"));
            Assert.Null(As("stake", ""));
            Assert.Null(As("stake", true));
            Assert.Equal(90, (int)As("roundTime", "1,5", 60));                         // an old key's factor
            Assert.Null(As("roundTime", "1.01", 60));

            // A decimal: a point or a comma. "1,000" is a thousand to one reader and a one to the next.
            Assert.Equal(JTokenType.Float, As("prize", "2").Type);
            Assert.Equal(2.5, (double)As("prize", "2.5"));
            Assert.Equal(2.5, (double)As("prize", "2,5"));
            Assert.Equal(2.5, (double)As("prize", 2.5));
            Assert.Equal(3.0, (double)As("prize", 3));
            Assert.Equal(29.0, (double)As("odds", "0.29", 100));                      // 28.999999999999996 to a double
            Assert.Null(As("prize", "1,000"));
            Assert.Null(As("prize", "a lot"));

            // Text: as it is, spaces and all; a stored switch or number as its text.
            Assert.Equal(" Zorkmids ", (string)As("coinName", " Zorkmids "));
            Assert.Equal("", (string)As("coinName", ""));
            Assert.Equal("true", (string)As("coinName", true));
            Assert.Equal("0.5", (string)As("coinName", 0.5));
            Assert.Equal("{user} takes the pot", (string)As("msgWon", "{user} takes the pot"));

            // A dropdown: one of its options, in the option's spelling; a slider over options likewise, over a range a number.
            Assert.Equal("hard", (string)As("level", "HARD"));
            Assert.Null(As("level", "brutal"));
            Assert.Equal("2", (string)As("speed", "2.0"));
            Assert.Null(As("speed", "4"));
            Assert.Equal(7, (int)As("volume", "7"));
            Assert.Null(As("volume", "loud"));

            // A button, a list and what the window never saves take nothing.
            Assert.Null(As("tryIt", "x"));
            Assert.Null(As("words", "[]"));
            Assert.Null(As("shown", "x"));
            Assert.Null(Adoption.ToKind(spec.ResolvedSettings.First(s => s.Key == "stake"), JValue.CreateNull()));
        }

        [Fact]
        public void AValueThatIsNoneOfItsKindIsLeftOut()
        {
            Live(AnAction(OldAction, SetArg("stake", "%rawInput%"), SetArg("active", "sometimes"), SetArg("level", "brutal"), SetArg("prize", "2,5")));
            var adopted = Adoption.Read(Spec(Old(OldAction)), _root, null);
            Assert.Equal(2.5, (double)adopted.Values["prize"]);
            Assert.Single(adopted.Values.Properties());
        }

        // ---- What the old version shipped in place of a value --------------------------------------------------

        [Fact]
        public void AValueTheOldVersionShippedInItsPlaceIsLeftOut()
        {
            // The hook was never set: the action still has what it came with. So has the message.
            Live(AnAction(OldAction, SetArg("hook", "<< YOUR_WEBHOOK >>"), SetArg("coinName", "  << your_coin >> "), SetArg("stake", "25"),
                Say(10, WonMessage, "<< YOUR_MESSAGE >>")));
            var adopted = Adoption.Read(Spec(Old(OldAction, "#" + WonMessage + ">msgWon"), "<< YOUR_* >>"), _root, null);
            Assert.Equal(25, (int)adopted.Values["stake"]);
            Assert.Single(adopted.Values.Properties());

            // Undeclared, it comes over as any text does; and a value the user set is taken, whatever is declared.
            Assert.Equal("<< YOUR_WEBHOOK >>", (string)Adoption.Read(Spec(Old(OldAction)), _root, null).Values["hook"]);
            Live(AnAction(OldAction, SetArg("hook", Secret), SetArg("coinName", "YOUR_COIN")));
            adopted = Adoption.Read(Spec(Old(OldAction), "<< YOUR_* >>", "changeme"), _root, null);
            Assert.Equal(Secret, (string)adopted.Values["hook"]);
            Assert.Equal("YOUR_COIN", (string)adopted.Values["coinName"]);

            // An action that holds nothing else has no values: it is looked for further back, as one without Set Arguments.
            Live(AnAction(OldAction, SetArg("hook", "ChangeMe")));
            Backup("backup-20261101-09150012345-1.0.7.zip", AnAction(OldAction, SetArg("hook", Secret)));
            adopted = Adoption.Read(Spec(Old(OldAction), "<< YOUR_* >>", "changeme"), _root, null);
            Assert.Equal(Secret, (string)adopted.Values["hook"]);
            Assert.True(adopted.From["hook"].Backup);
        }

        [Fact]
        public void AStarStandsForAnyText()
        {
            Assert.True(Adoption.Reads("<< YOUR_WEBHOOK >>", "<< YOUR_* >>"));
            Assert.True(Adoption.Reads("  << your_webhook >>  ", "<< YOUR_* >>"));     // capitals and the spaces around do not count
            Assert.True(Adoption.Reads("<< YOUR_ >>", "<< YOUR_* >>"));                // any text, none too
            Assert.True(Adoption.Reads("<< YOUR_A >> and >>", "<< YOUR_* >>"));
            Assert.True(Adoption.Reads("https://example.test/your-webhook-here", "*your-webhook-here"));
            Assert.True(Adoption.Reads("a*b", "a*b"));
            Assert.True(Adoption.Reads("xaybzc", "*a*b*c"));
            Assert.False(Adoption.Reads("<< YOUR_WEBHOOK >", "<< YOUR_* >>"));
            Assert.False(Adoption.Reads("my << YOUR_WEBHOOK >>", "<< YOUR_* >>"));     // the whole value, not a part of it
            Assert.False(Adoption.Reads("changeme!", "changeme"));
            Assert.False(Adoption.Reads("", "changeme"));
            Assert.False(Adoption.Reads(null, "changeme"));

            // A text that names no value, or every value, leaves out none.
            foreach (string none in new[] { null, "", "  ", "*", " ** " })
            {
                Assert.False(Adoption.Reads("anything", none));
                Assert.False(Adoption.Reads("", none));
            }
        }

        [Fact]
        public void WhatIsLeftOutIsDeclaredAsAListOrAsOneText()
        {
            Assert.Equal(new[] { "<< YOUR_* >>", "changeme" }, Spec(Old(OldAction), "<< YOUR_* >>", "changeme").AdoptExcept.ToArray());
            var one = Schema.Parse(SchemaJson(Old(OldAction)).Replace(@"""adopt"":", @"""adoptExcept"": ""changeme"", ""adopt"":"));
            Assert.Equal(new[] { "changeme" }, Entry.SpecFor(one, Path.Combine(_root, "Demo", "settings.json")).AdoptExcept.ToArray());
            Assert.Empty(Spec(Old(OldAction)).AdoptExcept);
            Assert.Empty(Spec(Old(OldAction), "<< YOUR_* >>").Problems);

            var problems = Spec(Old(OldAction), "", " * ", "fine").Problems;
            Assert.Contains(problems, p => p.Contains("that read like ''") && p.Contains("which names none; it is ignored"));
            Assert.Contains(problems, p => p.Contains("that read like ' * '") && p.Contains("which names every one; it is ignored"));
            Assert.Equal(2, problems.Count);
            Assert.Contains(Spec(null, "changeme").Problems, p => p.Contains("leaves out old values") && p.Contains("but takes over none"));
        }

        // ---- The declaration -----------------------------------------------------------------------------------

        [Fact]
        public void ADeclarationThatCannotWorkIsAProblem()
        {
            var spec = Spec(@"{ ""action"": """" }, { ""action"": ""My Old Action"" }, " +
                Old(OldAction, "einsatz>nothere", "no arrow", ">stake", "#" + WonMessage + ">tryIt", "liste>words", "status>shown", "#" + WonMessage + ">ghost", "fine>stake"));
            var problems = spec.Problems.Where(p => p.Contains("old values")).ToList();

            Assert.Contains(problems, p => p.Contains("from an action without an id"));
            Assert.Contains(problems, p => p.Contains("'My Old Action', which is no action id"));
            Assert.Contains(problems, p => p.Contains("'" + OldAction + "' map 'einsatz' to 'nothere', which is not a setting"));
            Assert.Contains(problems, p => p.Contains("the mapping 'no arrow', which is neither"));
            Assert.Contains(problems, p => p.Contains("the mapping '>stake', which is neither"));
            Assert.Contains(problems, p => p.Contains("take the text of the sub-action '" + WonMessage + "' into 'tryIt', an action field, which takes no value"));
            Assert.Contains(problems, p => p.Contains("map 'liste' to 'words', a list field, which takes no value"));
            Assert.Contains(problems, p => p.Contains("map 'status' to 'shown', which the window never saves (readOnly)"));
            Assert.Contains(problems, p => p.Contains("into 'ghost', which is not a setting"));
            Assert.Equal(9, problems.Count);                                            // 'fine>stake' is fine
            Assert.Equal(problems.Count, spec.Problems.Count);

            // A setting the project locks when the window opens is declared right: no problem, and no value for it.
            var lockedNow = Schema.Parse(SchemaJson(Old(OldAction, "einsatz>stake")).Replace(@"""host"": ""demo"",", @"""host"": ""demo"", ""lockedFields"": { ""stake"": ""Set by the tournament."" },"));
            var lockedSpec = Entry.SpecFor(lockedNow, Path.Combine(_root, "Demo", "settings.json"));
            Entry.Prepare(lockedSpec, lockedNow);
            Assert.Empty(lockedSpec.Problems);
            Live(AnAction(OldAction, SetArg("einsatz", "25"), SetArg("coinName", "Zorkmids")));
            var besideLocked = Adoption.Read(lockedSpec, _root, null);
            Assert.Null(besideLocked.Values["stake"]);
            Assert.Equal("Zorkmids", (string)besideLocked.Values["coinName"]);

            // A declaration without a mistake has no problem, however the id is written; an entry may be the id alone.
            Assert.Empty(Spec(Old("{" + OldAction.ToUpperInvariant() + "}", "einsatz>stake") + ", \"" + OtherAction + "\"").Problems);
            Live(AnAction(OldAction, SetArg("einsatz", "25")), AnAction(OtherAction, SetArg("coinName", "Zorkmids")));
            var adopted = Adoption.Read(Spec(Old("{" + OldAction.ToUpperInvariant() + "}", "einsatz>stake") + ", \"" + OtherAction + "\""), _root, null);
            Assert.Equal(25, (int)adopted.Values["stake"]);
            Assert.Equal("Zorkmids", (string)adopted.Values["coinName"]);
        }

        // ---- The window ----------------------------------------------------------------------------------------

        [Fact]
        public void TheValuesArriveInTheWindowAsUnsavedChanges()
        {
            Live(AnAction(OldAction, SetArg("stake", "25"), SetArg("active", true), SetArg("level", "hard"), SetArg("hookUrl", Secret),
                Say(10, WonMessage, "{user} takes the pot"), SetArg("shown", "never"), SetArg("prize", "a lot")));
            Adoption.Folder = _root;
            using (var w = new Opened(SchemaJson(Old(OldAction, "hookUrl>hook", "#" + WonMessage + ">msgWon"))))
            {
                Assert.Equal("25", w.On(() => w.Box("stake").Text));
                Assert.True(w.On(() => w.Context.Parts.Switches["active"].IsChecked == true));
                Assert.Equal("hard", w.On(() => (w.Context.Parts.Combos["level"].SelectedItem as ComboBoxItem)?.Tag as string));
                Assert.Equal(Secret, w.On(() => w.Box("hook").Text));
                Assert.Equal("{user} takes the pot", w.On(() => w.Box("msgWon").Text));
                Assert.Equal("2", w.On(() => w.Box("prize").Text));                    // no number: the default stays

                // Said in the status line, with where they came from in its tooltip; nothing is written yet.
                string note = w.On(() => Text.T("adopt.actions.many", 5));
                Assert.Equal("5 values taken over from Streamer.bot's actions", note);
                Assert.True(w.On(() => w.Shows(note)), "the status does not say what was taken over");
                Assert.Contains(@"data\actions.json", w.On(() => w.Status().ToolTip as string));
                Assert.False(File.Exists(w.SettingsPath));

                // A rebuild before the first save (the theme, the language) starts from the defaults: the values come again.
                w.On(() => w.Context.Reopen(null));
                Assert.Equal("25", w.On(() => w.Box("stake").Text));
                Assert.True(w.On(() => w.Shows(note)));

                // The user's own change counts with them, and Save writes them all.
                w.On(() => w.Box("stake").Text = "30");
                Assert.True(w.On(() => w.Shows(Text.T("status.unsaved.many", 5))));
                w.On(() => w.Click(Text.T("btn.save")));
                var saved = w.Saved();
                Assert.Equal(30, (int)saved["stake"]);
                Assert.True((bool)saved["active"]);
                Assert.Equal("hard", (string)saved["level"]);
                Assert.Equal(Secret, (string)saved["hook"]);
                Assert.Equal("{user} takes the pot", (string)saved["msgWon"]);
                Assert.Equal(2.0, (double)saved["prize"]);

                // Saved: from now on the file has the last word. A rebuild shows what was saved, not the old 25 again.
                w.On(() => w.Context.Reopen(null));
                Assert.Equal("30", w.On(() => w.Box("stake").Text));
                Assert.True(w.On(() => w.Shows(Text.T("status.clean"))));
            }
        }

        [Fact]
        public void NothingIsTakenOverOnceASettingsFileExists()
        {
            Live(AnAction(OldAction, SetArg("stake", "25"), SetArg("coinName", "Zorkmids")));
            Adoption.Folder = _root;
            var log = new Lines();
            using (var w = new Opened(SchemaJson(Old(OldAction)), log, folder => File.WriteAllText(Path.Combine(folder, "settings.json"), @"{ ""stake"": 7 }")))
            {
                Assert.Equal("7", w.On(() => w.Box("stake").Text));
                Assert.Equal("Points", w.On(() => w.Box("coinName").Text));
                Assert.True(w.On(() => w.Shows(Text.T("status.clean"))));
            }
            Assert.DoesNotContain(log.All, l => l.Contains("Old values"));

            // The .bak of a save whose file is gone is the saved settings too.
            log = new Lines();
            using (var w = new Opened(SchemaJson(Old(OldAction)), log, folder => File.WriteAllText(Path.Combine(folder, "settings.json.bak"), @"{ ""stake"": 8 }")))
                Assert.Equal("8", w.On(() => w.Box("stake").Text));
            Assert.DoesNotContain(log.All, l => l.Contains("Old values"));

            // And a window that declares no old action looks for none.
            log = new Lines();
            using (var w = new Opened(SchemaJson(null), log))
                Assert.Equal("10", w.On(() => w.Box("stake").Text));
            Assert.DoesNotContain(log.All, l => l.Contains("Old values"));
        }

        [Fact]
        public void NoValueReachesTheLog()
        {
            // From Streamer.bot's actions and from a backup, read alone and through the window, saved in the end.
            Live(AnAction(OldAction, SetArg("hookUrl", Secret), SetArg("coinName", "Zorkmids"), SetArg("stake", "4711"), SetArg("active", "sometimes"),
                Say(35001, WonMessage, "{user} takes the pot")));
            Backup("backup-20261105-18300054321-1.0.7.zip", AnAction(OtherAction, SetArg("prize", "8,25"), SetArg("level", "hard")));
            File.WriteAllText(Path.Combine(_root, "backup", "backup-20261106-10000000000-1.0.7.zip"), Secret);
            string adopt = Old(OldAction, "hookUrl>hook", "#" + WonMessage + ">msgWon") + ", " + Old(OtherAction);
            var log = new Lines();

            Assert.Equal(6, Adoption.Read(Spec(adopt), _root, log).Values.Count);
            Adoption.Folder = _root;
            using (var w = new Opened(SchemaJson(adopt), log))
            {
                Assert.Equal(Secret, w.On(() => w.Box("hook").Text));
                w.On(() => w.Click(Text.T("btn.save")));
                Assert.Equal(Secret, (string)w.Saved()["hook"]);
            }

            // What is said: how many, for which action, where they were found.
            Assert.Contains(log.All, l => l.Contains("4 found for the action " + OldAction + @" in data\actions.json"));
            Assert.Contains(log.All, l => l.Contains("2 found for the action " + OtherAction + @" in backup\backup-20261105-18300054321-1.0.7.zip"));
            Assert.Contains(log.All, l => l.Contains("6 of 6 shown in the window"));
            Assert.Contains(log.All, l => l.StartsWith("warn") && l.Contains("backup-20261106-10000000000-1.0.7.zip could not be read"));
            // What is not: any value, taken or not, or the name it had.
            foreach (string value in new[] { Secret, "T0P-S3CRET", "hooks.example", "Zorkmids", "4711", "sometimes", "takes the pot", "8,25", "8.25", "hard", "hookUrl" })
                Assert.DoesNotContain(log.All, l => l.Contains(value));
        }

        // ---- Actions as Streamer.bot writes them ---------------------------------------------------------------

        private static JObject SetArg(string name, object value, bool enabled = true) => new JObject
        {
            ["variableName"] = name, ["value"] = JToken.FromObject(value), ["autoType"] = false,
            ["id"] = Guid.NewGuid().ToString(), ["weight"] = 0.0, ["type"] = 123, ["parentId"] = null, ["enabled"] = enabled, ["index"] = 0
        };

        // Send Message: 10 to Twitch, 4001 to YouTube, 35001 to Kick.
        private static JObject Say(int type, string id, string text, bool enabled = true) => new JObject
        {
            ["text"] = text, ["useBot"] = true, ["fallback"] = true,
            ["id"] = id, ["weight"] = 0.0, ["type"] = type, ["parentId"] = null, ["enabled"] = enabled, ["index"] = 0
        };

        private static JObject Group(string name, bool enabled, params JObject[] subActions) => new JObject
        {
            ["name"] = name, ["random"] = false, ["color"] = null, ["subActions"] = new JArray(subActions.Cast<object>().ToArray()),
            ["id"] = Guid.NewGuid().ToString(), ["weight"] = 0.0, ["type"] = 99900, ["parentId"] = null, ["enabled"] = enabled, ["index"] = 0
        };

        // A comment is a sub-action too, always switched off.
        private static JObject Comment(string text, string id = null) => new JObject
        {
            ["value"] = text, ["color"] = "#FF808080",
            ["id"] = id ?? Guid.NewGuid().ToString(), ["weight"] = 0.0, ["type"] = 1009, ["parentId"] = null, ["enabled"] = false, ["index"] = 0
        };

        private static JObject Code() => new JObject
        {
            ["name"] = null, ["description"] = null, ["references"] = new JArray("mscorlib.dll"), ["byteCode"] = "dXNpbmcgU3lzdGVtOw==", ["precompile"] = false,
            ["delayStart"] = false, ["saveResultToVariable"] = false, ["saveToVariable"] = null,
            ["id"] = Guid.NewGuid().ToString(), ["weight"] = 0.0, ["type"] = 99999, ["parentId"] = null, ["enabled"] = true, ["index"] = 0
        };

        private static JObject AnAction(string id, params JObject[] subActions) => new JObject
        {
            ["id"] = id, ["queue"] = "00000000-0000-0000-0000-000000000000", ["enabled"] = true, ["excludeFromHistory"] = false, ["excludeFromPending"] = false,
            ["name"] = "Invented", ["group"] = "", ["alwaysRun"] = false, ["randomAction"] = false, ["concurrent"] = false,
            ["triggers"] = new JArray(), ["subActions"] = new JArray(subActions.Cast<object>().ToArray()), ["collapsedGroups"] = new JArray()
        };

        private static string ActionsJson(params JObject[] actions) => new JObject
        {
            ["version"] = 24, ["t"] = "2026-11-05T18:30:00.0000000+01:00", ["blocking"] = false, ["collapsedGroups"] = new JArray(),
            ["groups"] = new JArray(), ["queues"] = new JArray(), ["actions"] = new JArray(actions.Cast<object>().ToArray())
        }.ToString(Newtonsoft.Json.Formatting.None);

        /// <summary>data\actions.json, with the byte order mark Streamer.bot writes.</summary>
        private void Live(params JObject[] actions)
        {
            Directory.CreateDirectory(Path.Combine(_root, "data"));
            File.WriteAllText(Path.Combine(_root, "data", "actions.json"), ActionsJson(actions), new UTF8Encoding(true));
        }

        /// <summary>A backup zip as Streamer.bot names it, written here; without actions it holds settings.json alone.</summary>
        private void Backup(string name, params JObject[] actions)
        {
            Directory.CreateDirectory(Path.Combine(_root, "backup"));
            using (var file = File.Create(Path.Combine(_root, "backup", name)))
            using (var zip = new ZipArchive(file, ZipArchiveMode.Create))
            {
                Put(zip, "settings.json", "{}");
                if (actions.Length > 0) Put(zip, "actions.json", ActionsJson(actions));
            }
        }

        private static void Put(ZipArchive zip, string name, string text)
        {
            using (var writer = new StreamWriter(zip.CreateEntry(name).Open(), new UTF8Encoding(true)))
                writer.Write(text);
        }

        // ---- The window's declaration --------------------------------------------------------------------------

        /// <summary>One entry of window.adopt, as UiSchema.Old and UiWindow.Adopt write it.</summary>
        private static string Old(string actionId, params string[] map) =>
            new JObject { ["action"] = actionId, ["map"] = new JArray(map.Cast<object>().ToArray()) }.ToString(Newtonsoft.Json.Formatting.None);

        private static string SchemaJson(string adopt, params string[] except) => @"{ ""host"": ""demo"", ""version"": ""v1"", ""settingsFile"": ""x"", ""settings"": [
            { ""key"": ""active"", ""default"": false, ""page"": ""game"", ""label"": ""Active"" },
            { ""key"": ""stake"", ""default"": 10, ""page"": ""game"", ""label"": ""Stake"", ""aliases"": [""OldStake""] },
            { ""key"": ""odds"", ""default"": 45.0, ""page"": ""game"", ""label"": ""Odds"", ""aliases"": [""OddsFraction""], ""aliasFactors"": { ""OddsFraction"": 100 } },
            { ""key"": ""prize"", ""default"": 2.0, ""page"": ""game"", ""label"": ""Prize"" },
            { ""key"": ""roundTime"", ""default"": 60, ""kind"": ""duration"", ""page"": ""game"", ""label"": ""Round"", ""aliases"": [""RoundMinutes""], ""aliasFactors"": { ""RoundMinutes"": 60 } },
            { ""key"": ""level"", ""default"": ""easy"", ""kind"": ""options"", ""options"": [""easy"", ""hard""], ""page"": ""game"", ""label"": ""Level"" },
            { ""key"": ""speed"", ""default"": 1, ""kind"": ""slider"", ""options"": [""1"", ""2"", ""3""], ""page"": ""game"", ""label"": ""Speed"" },
            { ""key"": ""volume"", ""default"": 5, ""kind"": ""slider"", ""min"": 0, ""max"": 10, ""page"": ""game"", ""label"": ""Volume"" },
            { ""key"": ""coinName"", ""default"": ""Points"", ""page"": ""game"", ""label"": ""Coin name"" },
            { ""key"": ""hook"", ""default"": """", ""page"": ""game"", ""label"": ""Webhook"", ""masked"": true },
            { ""key"": ""msgWon"", ""default"": ""{user} wins"", ""kind"": ""text"", ""page"": ""game"", ""label"": ""Won"" },
            { ""key"": ""shown"", ""default"": ""idle"", ""page"": ""game"", ""label"": ""Shown"", ""readOnly"": true },
            { ""key"": ""words"", ""default"": [ { ""word"": ""a"" } ], ""kind"": ""list"", ""page"": ""game"", ""label"": ""Words"", ""columns"": [ ""word"" ] },
            { ""key"": ""tryIt"", ""default"": """", ""kind"": ""action"", ""page"": ""game"", ""label"": ""Try"" } ],
            ""window"": { ""title"": ""Demo"", ""pages"": [ ""game"", { ""id"": ""probe"", ""title"": ""Probe"", ""custom"": true } ]"
            + (adopt == null ? "" : @", ""adopt"": [ " + adopt + " ]")
            + (except.Length == 0 ? "" : @", ""adoptExcept"": " + new JArray(except.Cast<object>().ToArray()).ToString(Newtonsoft.Json.Formatting.None)) + " } }";

        /// <summary>The window resolved, as the shell has it when it looks for the old values.</summary>
        private WindowSpec Spec(string adopt, params string[] except)
        {
            var schema = Schema.Parse(SchemaJson(adopt, except));
            var spec = Entry.SpecFor(schema, Path.Combine(_root, "Demo", "settings.json"));
            Entry.Prepare(spec, schema);
            return spec;
        }

        private sealed class Lines : IHostLog
        {
            public readonly List<string> All = new List<string>();
            public void Info(string message) { lock (All) All.Add("info " + message); }
            public void Warn(string message) { lock (All) All.Add("warn " + message); }
            public void Error(string message) { lock (All) All.Add("error " + message); }
        }

        /// <summary>The window, opened as a settings action opens it; a custom page hands the test each build's context,
        /// and with it the window's thread and controls.</summary>
        private sealed class Opened : NoHooks, IDisposable
        {
            public readonly string Folder;
            public readonly string SettingsPath;

            private readonly Thread _runner;
            private readonly ManualResetEventSlim _built = new ManualResetEventSlim();
            private volatile PageContext _context;
            private volatile FrameworkElement _probe;

            public Opened(string schemaJson, IHostLog log = null, Action<string> files = null)
            {
                Folder = Path.Combine(Path.GetTempPath(), "UIFramework.Tests", "adopt-window-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(Folder);
                SettingsPath = Path.Combine(Folder, "settings.json");
                files?.Invoke(Folder);
                var schema = UIFramework.Schema.Parse(schemaJson);
                var spec = Entry.SpecFor(schema, SettingsPath);
                _runner = new Thread(() => { try { Entry.Show(spec, schema, log ?? new Lines(), this); } catch (Exception) { } }) { IsBackground = true };
                _runner.Start();
                if (!_built.Wait(15000))
                    throw new TimeoutException("The window was not built.");
                // Up and idle: shown, laid out, the first status written.
                Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
            }

            public PageContext Context => _context;
            private Dispatcher Dispatcher => _probe.Dispatcher;
            private Window Window => (Window)Up(_probe, n => n is Window);

            public override FrameworkElement BuildPage(string pageId, PageContext context)
            {
                _context = context;
                _probe = new TextBlock { Text = "probe" };
                _built.Set();
                return _probe;
            }

            public T On<T>(Func<T> body) => Dispatcher.Invoke(body);
            public void On(Action body) => Dispatcher.Invoke(body);

            public JObject Saved() => JObject.Parse(File.ReadAllText(SettingsPath));
            public TextBox Box(string key) => _context.Parts.Boxes[key];

            public void Click(string text)
            {
                var button = All(Window).OfType<Button>().First(b => b.Content as string == text);
                button.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, button));
            }

            public bool Shows(string text) => All(Window).OfType<TextBlock>().Any(t => t.Text == text);

            /// <summary>The status line: the footer's text with a tooltip of its own.</summary>
            public TextBlock Status() => All(Window).OfType<TextBlock>().First(t => t.TextTrimming == TextTrimming.CharacterEllipsis && t.TextAlignment == TextAlignment.Right);

            private static object Up(DependencyObject node, Func<DependencyObject, bool> wanted)
            {
                while (node != null && !wanted(node))
                    node = LogicalTreeHelper.GetParent(node) ?? (node is System.Windows.Media.Visual ? System.Windows.Media.VisualTreeHelper.GetParent(node) : null);
                return node;
            }

            private static IEnumerable<object> All(object node)
            {
                yield return node;
                if (node is DependencyObject d)
                    foreach (var child in LogicalTreeHelper.GetChildren(d))
                        foreach (var below in All(child))
                            yield return below;
            }

            public void Dispose()
            {
                try
                {
                    Shell.Close(SettingsPath);
                    var watch = Stopwatch.StartNew();
                    while (_runner.IsAlive && watch.ElapsedMilliseconds < 15000)
                        Thread.Sleep(50);
                }
                finally
                {
                    Text.Use(Conventions.Default.Lang);
                    try { Directory.Delete(Folder, true); } catch (Exception) { }
                }
            }
        }
    }
}
