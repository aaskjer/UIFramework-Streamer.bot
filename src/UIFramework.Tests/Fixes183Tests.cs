using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Newtonsoft.Json.Linq;
using Xunit;

namespace UIFramework.Tests
{
    /// <summary>1.8.3: the accent takes a colour's name and no alpha; latest.json lives under its major line.</summary>
    public class Fixes183Tests
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

        [Theory]
        [InlineData("red", 255, 0, 0)]
        [InlineData("Blue", 0, 0, 255)]
        [InlineData(" YELLOW ", 255, 255, 0)]
        [InlineData("darkorange", 255, 140, 0)]
        [InlineData("grey", 128, 128, 128)]
        [InlineData("DarkSlateGrey", 47, 79, 79)]
        public void A_colour_is_read_by_its_name(string text, byte r, byte g, byte b)
        {
            Assert.True(Colours.TryParseName(text, out var colour));
            Assert.Equal(Color.FromRgb(r, g, b), colour);
            Assert.True(Colours.TryParseSolid(text, out var solid));
            Assert.Equal(colour, solid);
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        [InlineData("rot")]
        [InlineData("no colour")]
        [InlineData("FF0000")]      // a code, not a name
        [InlineData("#F00")]
        [InlineData("AliceBlue2")]
        public void Anything_else_is_no_name(string text) => Assert.False(Colours.TryParseName(text, out _));

        [Theory]
        [InlineData("#F00", true)]
        [InlineData("FF0000", true)]
        [InlineData("#ff0000", true)]
        [InlineData("#80FF0000", false)]    // an alpha: not for the accent
        [InlineData("#8F00", false)]
        [InlineData("transparent", false)]
        [InlineData("reddish", false)]
        [InlineData("", false)]
        public void The_accent_takes_solid_colours_only(string text, bool taken) => Assert.Equal(taken, Colours.TryParseSolid(text, out _));

        [Fact]
        public void A_stored_accent_by_name_is_applied_and_one_with_an_alpha_is_not() => Sta(() =>
        {
            string folder = Path.Combine(Path.GetTempPath(), "uif-183-" + Path.GetRandomFileName());
            Directory.CreateDirectory(folder);
            var was = Theme.AccentOverride;
            bool wasDark = Theme.IsDark;
            try
            {
                var layout = new LayoutFile(Path.Combine(folder, "settings.json"));
                layout.Accent = "yellow";
                layout.ApplyTheme();
                Assert.Equal(Colors.Yellow, Theme.AccentOverride);
                Assert.Equal(Colors.Yellow, Theme.Accent);

                layout.Accent = "#80FF0000";
                layout.ApplyTheme();
                Assert.Null(Theme.AccentOverride);
                Assert.Equal(Theme.DefaultAccent, Theme.Accent);
            }
            finally
            {
                Theme.AccentOverride = was;
                Theme.IsDark = wasDark;
                Directory.Delete(folder, true);
            }
        });

        [Fact]
        public void The_accent_swatch_shows_what_the_window_takes() => Sta(() =>
        {
            var was = Theme.AccentOverride;
            try
            {
                Theme.AccentOverride = Color.FromRgb(1, 2, 3);
                Assert.Equal(Colors.Red, Customize.AccentSwatch("red").Color);
                Assert.Equal(Color.FromRgb(0x2E, 0xCC, 0x71), Customize.AccentSwatch("2ECC71").Color);
                // Not taken: the swatch stays on the accent in use instead of showing a colour the window will not get.
                Assert.Equal(Color.FromRgb(1, 2, 3), Customize.AccentSwatch("#80FF0000").Color);
                Assert.Equal(Color.FromRgb(1, 2, 3), Customize.AccentSwatch("redd").Color);
                // Empty is the default blue, which is what an empty box gives.
                Assert.Equal(Theme.DefaultAccent, Customize.AccentSwatch("").Color);

                // A field built with it follows its box.
                var parts = new WindowParts();
                var fields = new Fields(parts, JObject.Parse(@"{ ""accent"": ""blue"" }"), new JObject(), _ => { });
                var row = fields.ColourField("Accent", "accent", "", labelled: false, swatchOf: Customize.AccentSwatch);
                var swatch = ((Panel)row).Children.OfType<Border>().First(b => b.Width == 28);
                Assert.Equal(Colors.Blue, ((SolidColorBrush)swatch.Background).Color);
                parts.Boxes["accent"].Text = "yellow";
                Assert.Equal(Colors.Yellow, ((SolidColorBrush)swatch.Background).Color);
            }
            finally { Theme.AccentOverride = was; }
        });

        [Fact]
        public void A_colour_field_of_a_game_still_shows_a_code_with_an_alpha() => Sta(() =>
        {
            // Only the accent is solid: a game's own colour setting keeps its alpha, as since 1.8.2.
            Assert.Equal(Color.FromArgb(0x80, 255, 0, 0), Colours.ParseHexBrush("#80FF0000").Color);
            Assert.True(Colours.TryParse("#80FF0000", out var withAlpha));
            Assert.Equal(0x80, withAlpha.A);
        });

        [Fact]
        public void The_update_check_reads_the_latest_json_of_its_own_major_line()
        {
            Assert.EndsWith("/main/lib/v1/latest.json", UpdateNotices.LatestUrlOf(1));
            Assert.EndsWith("/main/lib/v2/latest.json", UpdateNotices.LatestUrlOf(2));
            int major = typeof(UpdateNotices).Assembly.GetName().Version.Major;
            Assert.Equal(UpdateNotices.LatestUrlOf(major), UpdateNotices.FrameworkLatestUrl);
        }

        [Fact]
        public void The_release_workflow_writes_latest_json_under_the_tags_major_line()
        {
            string root = System.AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\', '/');
            while (root != null && !File.Exists(Path.Combine(root, "PLAN.md"))) root = Path.GetDirectoryName(root);
            Assert.NotNull(root);
            string workflow = File.ReadAllText(Path.Combine(root, ".github", "workflows", "release.yml"));
            Assert.Contains("lib/v$($version.Split('.')[0])/latest.json", workflow);
            // No step writes a fixed lib/v1 any more: a 2.0.0 would have landed in the v1 hosts.
            Assert.DoesNotContain("'lib/v1/latest.json'", workflow);
            Assert.DoesNotContain("git add lib/v1", workflow);
        }
    }
}
