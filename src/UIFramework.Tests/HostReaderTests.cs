using System.Text.RegularExpressions;
using Xunit;

namespace UIFramework.Tests
{
    /// <summary>The host reader (stub/HostReader.cs, compiled into this project as UiReaderSettings): what an action
    /// without the table reads, with a fallback per call and its own run's arguments over the file.</summary>
    public class HostReaderTests
    {
        private static string Repo([System.Runtime.CompilerServices.CallerFilePath] string here = null) =>
            Path.GetFullPath(Path.Combine(Path.GetDirectoryName(here), "..", ".."));

        [Fact]
        public void AKeyTheFileLacksReadsAsTheFallbackOfTheCall()
        {
            var settings = UiReaderSettings.Parse(@"{ ""perHour"": 12, ""chance"": 0.25, ""dryRun"": true, ""name"": ""bot"", ""empty"": null }");
            Assert.Equal(12, settings.Int("perHour", 6));
            Assert.Equal(0.25, settings.Double("chance", 1));
            Assert.True(settings.Bool("dryRun"));
            Assert.Equal("bot", settings.String("name", "none"));
            Assert.Equal("12", settings.String("perHour"));

            Assert.Equal(6, settings.Int("missing", 6));
            Assert.Equal(1.5, settings.Double("missing", 1.5));
            Assert.True(settings.Bool("missing", true));
            Assert.Equal("none", settings.String("missing", "none"));
            Assert.Equal("none", settings.String("empty", "none"));     // null in the file is no value
            Assert.False(settings.Has("empty"));
            Assert.True(settings.Has("perHour"));
        }

        [Fact]
        public void TextIsReadAsTheHostActionReadsAnArgument()
        {
            var settings = UiReaderSettings.Parse(@"{ ""a"": ""Yes"", ""b"": ""off"", ""c"": ""maybe"", ""n"": ""2,5"", ""w"": ""7"", ""t"": ""many"" }");
            Assert.True(settings.Bool("a"));
            Assert.False(settings.Bool("b", true));
            Assert.True(settings.Bool("c", true));                      // no switch: the fallback
            Assert.Equal(2.5, settings.Double("n"));
            Assert.Equal(7, settings.Int("w"));
            Assert.Equal(3, settings.Int("t", 3));
        }

        [Fact]
        public void AnArgumentOfANamedKeyWinsOverTheFile()
        {
            var file = UiReaderSettings.Parse(@"{ ""dryRun"": false, ""perHour"": 2, ""webhookUrl"": ""https://file.test"" }");
            var with = file.With(new Dictionary<string, object>
            {
                ["dryRun"] = "true", ["perHour"] = 9, ["webhookUrl"] = "  ", ["other"] = "x"
            }, "dryRun", "perHour", "webhookUrl");
            Assert.True(with.Bool("dryRun"));
            Assert.Equal(9, with.Int("perHour"));
            Assert.Equal("https://file.test", with.String("webhookUrl"));   // an empty argument does not win
            Assert.False(with.Has("other"));                                // not named: not read
            Assert.False(file.Bool("dryRun"));                              // the file's settings stay as they were
            Assert.Same(file, file.With(new Dictionary<string, object> { ["dryRun"] = "true" }));   // no key named
            Assert.Same(file, file.With(null, "dryRun"));
        }

        [Fact]
        public void LoadIsTheHostBlocksLoad()
        {
            // The reader reads the file exactly as the host action does (the .bak, a save under way): the three methods
            // are the block's, text for text.
            string block = File.ReadAllText(Path.Combine(Repo(), "stub", "HostSchema.cs")).Replace("\r\n", "\n");
            string reader = File.ReadAllText(Path.Combine(Repo(), "stub", "HostReader.cs")).Replace("\r\n", "\n");
            foreach (string signature in new[]
            {
                "public static UiSettings Load(string path, System.Action<string> log = null)",
                "private static Newtonsoft.Json.Linq.JObject Read(string path)",
                "private static string ReadWithRetry(string path)",
                "public static UiSettings Parse(string json)"
            })
            {
                string Body(string source)
                {
                    var match = Regex.Match(source, Regex.Escape("    " + signature) + @"\n    \{\n.*?\n    \}\n", RegexOptions.Singleline);
                    Assert.True(match.Success, signature);
                    return match.Value;
                }
                Assert.Equal(Body(block), Body(reader));
            }
        }

        [Fact]
        public void WhatAProjectPastesInNeedsNoSystemCore()
        {
            // An action may leave System.Core out of its references (no LINQ, no HashSet there): the host block and
            // the host reader then still have to compile. The tools here compile with System.Core at hand, so the
            // sources are read for what would need it.
            foreach (string file in new[] { "HostSchema.cs", "HostReader.cs" })
            {
                string source = File.ReadAllText(Path.Combine(Repo(), "stub", file));
                foreach (string needsIt in new[] { "System.Linq", "HashSet<", "SortedSet<", "ISet<", "System.Threading.Tasks.Parallel", " dynamic " })
                    Assert.False(source.Contains(needsIt), "stub/" + file + " uses " + needsIt.Trim() + ", which needs a reference to System.Core");
            }
        }

        [Fact]
        public void TheReaderLoadsTheFileAndItsBackup()
        {
            string file = Path.Combine(Path.GetTempPath(), "UIFramework.Tests", "reader-" + Guid.NewGuid().ToString("N"), "settings.json");
            Directory.CreateDirectory(Path.GetDirectoryName(file));
            Assert.Null(UiReaderSettings.Load(file).Problem);              // never saved: nothing, without a word
            File.WriteAllText(file, @"{ ""perHour"": 12 }");
            Assert.Equal(12, UiReaderSettings.Load(file).Int("perHour"));
            File.WriteAllText(file + ".bak", @"{ ""perHour"": 8 }");
            File.WriteAllText(file, "{ broken");
            var heard = new List<string>();
            var backup = UiReaderSettings.Load(file, heard.Add);
            Assert.True(backup.FromBackup);
            Assert.Equal(8, backup.Int("perHour"));
            Assert.Single(heard);
        }
    }
}
