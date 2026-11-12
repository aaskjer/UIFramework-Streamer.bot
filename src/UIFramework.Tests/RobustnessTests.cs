using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using Xunit;

namespace UIFramework.Tests
{
    /// <summary>What a damaged file, a second window or a slow host must not break.</summary>
    public class RobustnessTests : IDisposable
    {
        private readonly string _folder = Path.Combine(Path.GetTempPath(), "UIFramework.Tests", Guid.NewGuid().ToString("N"));
        private string SettingsPath => Path.Combine(_folder, "settings.json");
        private string LayoutPath => Path.Combine(_folder, LayoutFile.FileName);

        public RobustnessTests() { Directory.CreateDirectory(_folder); }
        public void Dispose() { try { Directory.Delete(_folder, true); } catch { } }

        private static void Sta(Action body)
        {
            Exception failure = null;
            var thread = new Thread(() => { try { body(); } catch (Exception ex) { failure = ex; } });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            if (failure != null) throw failure;
        }

        [Fact]
        public void ALayoutFileThatIsNotJsonIsKeptAsBroken()
        {
            File.WriteAllText(LayoutPath, "{ \"theme\": { \"dark\": fals");
            var layout = LayoutFile.Load(SettingsPath);
            Assert.NotNull(layout.Problem);
            Assert.Equal(0, layout.ChangeCount);
            Assert.True(File.Exists(LayoutPath + ".broken"));

            layout.Accent = "#123456";
            layout.Save();
            Assert.Equal("{ \"theme\": { \"dark\": fals", File.ReadAllText(LayoutPath + ".broken"));
            Assert.Null(LayoutFile.Load(SettingsPath).Problem);
        }

        [Fact]
        public void ValuesOfTheWrongTypeReadAsNotSet()
        {
            File.WriteAllText(LayoutPath, @"{ ""theme"": ""dark"",
                ""pages"": { ""one"": { ""order"": ""2"", ""title"": 5, ""hidden"": ""yes"" } },
                ""fields"": { ""a"": { ""order"": 2.6, ""row"": 3, ""label"": { ""x"": 1 } } } }");
            var layout = LayoutFile.Load(SettingsPath);
            Assert.Null(layout.Problem);
            Assert.Null(layout.Dark);
            Assert.False(layout.Rainbow);
            Assert.Equal("", layout.Accent);
            var page = layout.Page("one");
            Assert.Null(page.Order);
            Assert.Null(page.Title);
            Assert.Null(page.Hidden);
            var field = layout.Field("a");
            Assert.Equal(3, field.Order);
            Assert.Null(field.Row);
            Assert.Null(field.Label);
            layout.Dark = false;   // a theme that was a string becomes an object again
            Assert.False(layout.Dark);
        }

        [Fact]
        public void TwoWindowThreadsKeepTheirOwnThemeAndLanguage()
        {
            bool wasDark = Theme.IsDark;
            var wasAccent = Theme.AccentOverride;
            bool? darkA = null, darkB = null;
            System.Windows.Media.Color? accentA = null, accentB = null;
            using (var aSet = new ManualResetEventSlim())
            using (var bSet = new ManualResetEventSlim())
            {
                var a = new Thread(() =>
                {
                    Theme.IsDark = true;
                    Theme.AccentOverride = System.Windows.Media.Colors.Red;
                    aSet.Set();
                    bSet.Wait();
                    darkA = Theme.IsDark;
                    accentA = Theme.AccentOverride;
                });
                var b = new Thread(() =>
                {
                    aSet.Wait();
                    Theme.IsDark = false;
                    Theme.AccentOverride = null;
                    bSet.Set();
                    darkB = Theme.IsDark;
                    accentB = Theme.AccentOverride;
                });
                a.Start(); b.Start(); a.Join(); b.Join();
            }
            Assert.True(darkA);
            Assert.Equal(System.Windows.Media.Colors.Red, accentA);
            Assert.False(darkB);
            Assert.Null(accentB);
            // A thread that set nothing reads the last values set anywhere.
            bool? fresh = null;
            var c = new Thread(() => fresh = Theme.IsDark);
            c.Start(); c.Join();
            Assert.False(fresh);
            Theme.IsDark = wasDark;
            Theme.AccentOverride = wasAccent;
        }

        [Fact]
        public void AnAnswerWithOnlyAStatusKeepsThePanel()
        {
            Sta(() =>
            {
                var panel = new StackPanel();
                ResultPanel.Render(panel, "{ \"lines\": [\"one\", \"two\"] }");
                Assert.Equal(2, panel.Children.Count);
                ResultPanel.Render(panel, "{ \"status\": \"saved\", \"set\": { \"a\": 1 } }");
                Assert.Equal(2, panel.Children.Count);
                var empty = new StackPanel();
                ResultPanel.Render(empty, "{ \"status\": \"saved\" }");
                Assert.Single(empty.Children);
            });
        }

        private sealed class CountingHooks : NoHooks
        {
            public int Asked;
            public override string EmoteSources() { Asked++; return "{ \"youtube\": { \":own:\": \"https://example.invalid/own\" } }"; }
        }

        [Fact]
        public void EmoteSourcesAreAskedOncePerWindow()
        {
            var hooks = new CountingHooks();
            for (int i = 0; i < 5; i++)
                Assert.StartsWith("https://example.invalid/own", EmotePictures.YouTubeUrl(":own:", hooks));
            Assert.Equal(1, hooks.Asked);
            var other = new CountingHooks();
            EmotePictures.YouTubeUrl(":own:", other);
            Assert.Equal(1, other.Asked);
        }

        [Fact]
        public void ASectionHeadingFollowsItsFields()
        {
            Sta(() =>
            {
                var heading = new TextBlock();
                var one = new Border();
                var two = new Border();
                VisibilityWatch.ShowWhileAnyVisible(heading, new List<FrameworkElement> { one, two });
                Assert.Equal(Visibility.Visible, heading.Visibility);
                one.Visibility = Visibility.Collapsed;
                Assert.Equal(Visibility.Visible, heading.Visibility);
                two.Visibility = Visibility.Collapsed;
                Assert.Equal(Visibility.Collapsed, heading.Visibility);
                two.Visibility = Visibility.Visible;
                Assert.Equal(Visibility.Visible, heading.Visibility);
            });
        }

        private static string Repo([System.Runtime.CompilerServices.CallerFilePath] string here = null) =>
            Path.GetFullPath(Path.Combine(Path.GetDirectoryName(here), "..", ".."));

        private static (int Code, string Output) HostBlockScript(string file, string extra = "")
        {
            var start = new ProcessStartInfo("powershell.exe",
                "-NoProfile -ExecutionPolicy Bypass -File \"" + Path.Combine(Repo(), "tools", "update-host-block.ps1") + "\" -File \"" + file + "\" " + extra)
            { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
            using (var process = Process.Start(start))
            {
                string output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
                process.WaitForExit();
                return (process.ExitCode, output);
            }
        }

        [Fact]
        public void TheHostBlockScriptRefusesToDropTheGamesOwnLines()
        {
            string demo = File.ReadAllText(Path.Combine(Repo(), "examples", "DemoGame", "DemoGame.cs"));
            const string marker = "// --- end UIFramework host block ---";
            int end = demo.IndexOf(marker, StringComparison.Ordinal);
            Assert.True(end > 0);
            int lineStart = demo.LastIndexOf('\n', end) + 1;
            string game = demo.Substring(0, lineStart) + "public static class MyOwnHelper { }\n" + demo.Substring(lineStart);
            string file = Path.Combine(_folder, "Game.cs");
            File.WriteAllText(file, game);

            var refused = HostBlockScript(file);
            Assert.Equal(1, refused.Code);
            Assert.Contains("MyOwnHelper", refused.Output);
            Assert.Equal(game, File.ReadAllText(file));
            Assert.False(File.Exists(file + ".bak"));

            var forced = HostBlockScript(file, "-Force");
            Assert.Equal(0, forced.Code);
            Assert.DoesNotContain("MyOwnHelper", File.ReadAllText(file));
            Assert.Equal(game, File.ReadAllText(file + ".bak"));
        }
    }
}
