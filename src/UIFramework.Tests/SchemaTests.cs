using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using Xunit;

namespace UIFramework.Tests
{
    public class SchemaTests
    {
        private const string SchemaJson = @"{
          ""host"": ""demo"", ""version"": ""v1.2.0"",
          ""settings"": [
            { ""key"": ""useBot"", ""default"": true },
            { ""key"": ""globalCooldown"", ""default"": 10 },
            { ""key"": ""addictedPayoutCut"", ""default"": 50 },
            { ""key"": ""numDecks"", ""default"": 6, ""page"": ""table"", ""section"": ""Shoe"", ""min"": 1, ""max"": 8 },
            { ""key"": ""payout"", ""default"": 1.5, ""page"": ""table"", ""options"": [""1"", ""1.5"", ""2""] },
            { ""key"": ""overlayScale"", ""default"": 100 },
            { ""key"": ""customThing"", ""default"": ""x"" },
            { ""key"": ""msgWin"", ""default"": ""@{user} wins {amount}"", ""kind"": ""template"", ""group"": ""Wins"", ""label"": ""Win"", ""placeholders"": [""user"", ""amount""] },
            { ""key"": ""resetBoard"", ""kind"": ""action"", ""page"": ""stats"", ""label"": ""Reset"" },
            { ""key"": ""tryBet"", ""default"": 1000, ""transient"": true },
            { ""key"": ""odds"", ""kind"": ""action"", ""live"": true, ""panel"": true, ""auto"": true },
            { ""key"": ""emotes"", ""default"": [], ""kind"": ""list"", ""columns"": [ { ""key"": ""Code"", ""kind"": ""emote"", ""label"": ""Emote"", ""platform"": ""kick"" }, { ""key"": ""Weight"", ""kind"": ""int"" } ] },
            { ""key"": ""winEmote"", ""default"": ""Kappa"", ""kind"": ""emote"", ""platform"": ""twitch"", ""requires"": ""numDecks>=4"" },
            { ""key"": ""aboutShoe"", ""kind"": ""note"", ""page"": ""table"", ""help"": ""The shoe is shuffled when it runs low."" },
            { ""key"": ""whisper"", ""default"": true, ""twitchOnly"": true, ""aliases"": [""Whisper""] },
            { ""key"": ""whisperTwice"", ""default"": false, ""aliases"": [""Whisper""] }
          ] }";

        [Fact]
        public void ParseReadsEveryField()
        {
            var schema = Schema.Parse(SchemaJson);
            Assert.Equal("demo", schema.Host);
            Assert.Equal(16, schema.Settings.Count);
            Assert.True(schema.Find("whisper").TwitchOnly);
            Assert.True((bool)schema.Find("whisper").ToJson()["twitchOnly"]);
            Assert.Equal("note", schema.Find("aboutShoe").Kind);
            Assert.Equal("twitch", schema.Find("winEmote").Platform);
            Assert.Equal("numDecks>=4", (string)schema.Find("winEmote").ToJson()["requires"]);
            var emotes = schema.Find("emotes");
            Assert.Equal("kick", emotes.Columns[0].Platform);
            Assert.Equal("kick", (string)emotes.ToJson()["columns"][0]["platform"]);
            Assert.Null(emotes.Clone().Columns[1].Platform);
            Assert.True(schema.Find("tryBet").Transient);
            var odds = schema.Find("odds");
            Assert.True(odds.Live && odds.Panel && odds.Auto);
            Assert.True((bool)odds.ToJson()["auto"]);
            var decks = schema.Find("numDecks");
            Assert.Equal("Shoe", decks.Section);
            Assert.Equal(1, decks.Min);
            Assert.Equal(3, schema.Find("payout").Options.Count);
            Assert.Equal(new[] { "user", "amount" }, schema.Find("msgWin").Placeholders);
            Assert.Throws<ArgumentException>(() => Schema.Parse(@"{ ""settings"": [ { ""key"": ""a"" }, { ""key"": ""a"" } ] }"));
        }

        [Fact]
        public void ConventionsCompleteKnownKeys()
        {
            var conventions = Conventions.Default;
            var cooldown = conventions.Complete(new Setting { Key = "globalCooldown", Default = 10 });
            Assert.Equal("duration", cooldown.Kind);
            Assert.Equal("cooldowns", cooldown.Page);
            Assert.Equal("Global cooldown", cooldown.Label);
            Assert.Contains("CooldownGlobalSeconds", cooldown.Aliases);

            var cut = conventions.Complete(new Setting { Key = "addictedPayoutCut", Default = 50 });
            Assert.Equal("percent", cut.Kind);
            Assert.Equal(100, cut.AliasFactors["AddictedPayoutCut"]);

            var unknown = conventions.Complete(new Setting { Key = "customThing", Default = "x" });
            Assert.Equal("string", unknown.Kind);
            Assert.Equal("game", unknown.Page);
            Assert.Equal("Custom thing", unknown.Label);

            var hostLabel = conventions.Complete(new Setting { Key = "useBot", Default = true, Label = "Bot sends" });
            Assert.Equal("Bot sends", hostLabel.Label);
        }

        [Fact]
        public void ResolveBuildsPagesDefaultsMessagesAndAliases()
        {
            var spec = SpecJson.Parse(@"{ ""title"": ""Demo"", ""pages"": [ { ""id"": ""table"", ""title"": ""Table"", ""icon"": ""🃏"" }, ""stats"" ] }", Path.GetTempFileName());
            Entry.Prepare(spec, Schema.Parse(SchemaJson));

            var ids = spec.Pages.Select(p => p.Id).ToList();
            // useBot is a sender switch and lives on the Messages page, so no General page is needed; customThing opens Game.
            Assert.Equal(new[] { "table", "stats", "game", "cooldowns", "access", "messages", "overlay", "about" }, ids);
            Assert.Equal("Table", spec.Pages[0].Title);
            Assert.Equal("Cooldowns", spec.Pages.First(p => p.Id == "cooldowns").Title);
            Assert.True(spec.Pages.Last().Bottom);

            Assert.Equal(6, (int)spec.Defaults["numDecks"]);
            Assert.Null(spec.Defaults["resetBoard"]);
            Assert.Single(spec.Messages);
            Assert.Equal("Wins", spec.Messages[0].Group);
            Assert.Equal("globalCooldown", spec.Aliases["CooldownGlobalSeconds"]);
            Assert.Equal(100, spec.AliasFactors["AddictedPayoutCut"]);
            Assert.Equal("v1.2.0", spec.Version);
            Assert.Equal("useBot", spec.Senders.UseBot);
            Assert.Null(spec.Senders.SendAction);
        }

        [Fact]
        public void WildcardsClaimFieldsButNotTemplates()
        {
            var spec = SpecJson.Parse(@"{ ""pages"": [ { ""id"": ""mine"", ""fields"": [""msg*"", ""num*""] } ] }", Path.GetTempFileName());
            Entry.Prepare(spec, Schema.Parse(SchemaJson));
            Assert.Contains(spec.Pages, p => p.Id == "mine");
            Assert.Contains(spec.Pages, p => p.Id == "messages");
            Assert.Single(spec.Messages);
        }

        [Fact]
        public void StoreMovesAliasesWithFactor()
        {
            string folder = Path.Combine(Path.GetTempPath(), "UIFramework.Tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            try
            {
                string file = Path.Combine(folder, "s.json");
                File.WriteAllText(file, @"{ ""CooldownGlobalSeconds"": 45, ""AddictedPayoutCut"": 0.35, ""useBot"": false, ""UseBot"": true }");
                var spec = SpecJson.Parse("{}", file);
                Entry.Prepare(spec, Schema.Parse(SchemaJson));
                var store = new SettingsStore(file, spec.Defaults).WithAliases(spec.Aliases, spec.AliasFactors);
                var settings = store.Load().Settings;
                Assert.Equal(45, (int)settings["globalCooldown"]);
                Assert.Equal(35, (int)settings["addictedPayoutCut"]);
                Assert.False((bool)settings["useBot"]);          // canonical wins over the old key
                Assert.Null(settings["CooldownGlobalSeconds"]);  // the old key is gone
                store.Save(settings);
                var written = JObject.Parse(File.ReadAllText(file));
                Assert.Null(written["AddictedPayoutCut"]);
                Assert.Equal(35, (int)written["addictedPayoutCut"]);
            }
            finally { try { Directory.Delete(folder, true); } catch { } }
        }

        [Fact]
        public void SpecJsonReadsHelpAboutLogAndSenders()
        {
            var spec = SpecJson.Parse(@"{
                ""title"": ""Demo"", ""releases"": ""aaskjer/Demo"",
                ""senders"": { ""useBot"": ""UseBot"", ""numberSeparator"": null },
                ""help"": [ { ""heading"": ""Play"", ""lines"": [ ""a note"", { ""command"": ""!x"", ""what"": ""does x"" } ] } ],
                ""about"": { ""links"": [ { ""text"": ""Discord"", ""url"": ""https://d"" } ] },
                ""log"": { ""folder"": ""Logs"", ""columns"": [""Bet"", { ""header"": ""Player"", ""field"": ""User"" }], ""results"": [""Win""] },
                ""headerPills"": [ { ""key"": ""freeGame"", ""on"": ""Free"", ""off"": ""Paid"" } ]
            }");
            Assert.Equal("https://github.com/aaskjer/Demo/releases", spec.ReleasesUrl);
            Assert.Equal("UseBot", spec.Senders.UseBot);
            Assert.Null(spec.Senders.NumberSeparator);
            Assert.Single(spec.Help);
            Assert.Equal(2, spec.Help[0].Lines.Count);
            Assert.Equal("Discord", spec.About.Links[0].Text);
            Assert.Equal("Player", spec.Log.Columns[1].Header);
            Assert.Single(spec.HeaderPills);
        }

        [Fact]
        public void ReflectionHostFindsOptionalMethods()
        {
            var host = new FakeHost();
            var bridge = new ReflectionHost(host);
            bridge.Warn("careful");
            Assert.Equal("warn:careful", host.LastLog);
            bridge.OnSaved(new JObject { ["a"] = 1 });
            Assert.Equal("{\"a\":1}", host.Saved);
            Assert.Equal("ok", bridge.Test("x", new JObject()));
            var log = bridge.ReadLog().ToList();
            Assert.Single(log);
            Assert.Equal("alice", log[0].User);
            Assert.Null(new ReflectionHost(new object()).ReadLog());
        }

        public sealed class FakeHost
        {
            public string LastLog, Saved;
            public void Log(string level, string message) => LastLog = level + ":" + message;
            public void OnSaved(string json) => Saved = json;
            public string Test(string name, string json) => "ok";
            public string[] ReadLogLines() => new[] { "[2026-01-01 10:00:00] WIN | User: alice | Platform: twitch" };
        }

        [Fact]
        public void BlackjackHostFilesResolve()
        {
            string root = FindRepoRoot();
            var spec = SpecJson.Parse(File.ReadAllText(Path.Combine(root, "hosts", "blackjack", "spec.json")), Path.GetTempFileName());
            var schema = Schema.Parse(File.ReadAllText(Path.Combine(root, "hosts", "blackjack", "schema.json")));
            Entry.Prepare(spec, schema);
            Assert.Equal(10, spec.Pages.Count);
            Assert.Equal("table", spec.Pages[0].Id);
            Assert.Equal("about", spec.Pages.Last().Id);
            Assert.True(spec.Messages.Count > 150);
            Assert.Equal("useBot", spec.Aliases["UseBot"]);
            Assert.Equal(spec.ResolvedSettings.Count, spec.ResolvedSettings.Select(s => s.Key).Distinct().Count());
        }

        private static string FindRepoRoot()
        {
            var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "PLAN.md")))
                dir = dir.Parent;
            return dir?.FullName ?? throw new InvalidOperationException("repo root not found");
        }
    }
}
