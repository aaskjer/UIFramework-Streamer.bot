using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace UIFramework.Tests
{
    /// <summary>1.10.0, what was left: a field the user never placed on a page the user sorted.</summary>
    public class Fixes1100LeftoverTests
    {
        /// <summary>A window with its settings file in a folder of its own, so its layout file is its own too.</summary>
        private static WindowSpec Opened(Schema schema, Action<LayoutFile> layout = null)
        {
            string folder = Path.Combine(Path.GetTempPath(), "UIFramework.Tests", "leftover-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            var spec = Entry.SpecFor(null, schema, Path.Combine(folder, "settings.json"));
            if (layout != null)
            {
                var file = LayoutFile.Load(spec.SettingsPath);
                layout(file);
                file.Save();
            }
            Entry.Prepare(spec, schema);
            return spec;
        }

        /// <summary>A page's fields as it draws them, each with the section it sits in: "b:A".</summary>
        private static string[] Drawn(WindowSpec spec, string pageId) =>
            spec.ResolvedSettings.Where(s => s.Page == pageId).OrderBy(s => s.Order).Select(s => s.Key + ":" + s.Section).ToArray();

        /// <summary>Whether a section's heading shows more than once on the page.</summary>
        private static bool SplitsASection(WindowSpec spec, string pageId)
        {
            var headings = new List<string>();
            string section = null;
            foreach (var s in spec.ResolvedSettings.Where(x => x.Page == pageId).OrderBy(x => x.Order))
                if (s.Section != null && s.Section != section)
                    headings.Add(section = s.Section);
            return headings.Count != headings.Distinct().Count();
        }

        // ---- A field the user never placed on a page the user sorted ------------------------------------------

        [Fact]
        public void A_field_added_in_an_update_goes_behind_the_one_declared_before_it_on_a_sorted_page()
        {
            // n came with an update, declared after b in section A. The user had put section B above A, so the page holds
            // the orders c 0, d 10, a 20, b 30, and n none.
            string Declared(string first) => @"{ ""host"": ""demo"", ""settings"": [ " + first + @"
                { ""key"": ""a"", ""default"": ""x"", ""page"": ""one"", ""section"": ""A"" },
                { ""key"": ""b"", ""default"": ""x"", ""page"": ""one"", ""section"": ""A"" },
                { ""key"": ""n"", ""default"": ""x"", ""page"": ""one"", ""section"": ""A"" },
                { ""key"": ""c"", ""default"": ""x"", ""page"": ""one"", ""section"": ""B"" },
                { ""key"": ""d"", ""default"": ""x"", ""page"": ""one"", ""section"": ""B"" },
                { ""key"": ""m"", ""default"": ""x"", ""page"": ""one"", ""section"": ""M"" } ],
                ""window"": { ""title"": ""Demo"", ""pages"": [ ""one"" ] } }";
            void Sorted(LayoutFile layout)
            {
                layout.SetField("c", "order", 0);
                layout.SetField("d", "order", 10);
                layout.SetField("a", "order", 20);
                layout.SetField("b", "order", 30);
                layout.SetField("m", "order", 40);
            }
            var spec = Opened(Schema.Parse(Declared("")), Sorted);
            // Before, n kept its place among all settings, 2, and sat between c and d: A and B both showed twice.
            Assert.Equal(new[] { "c:B", "d:B", "a:A", "b:A", "n:A", "m:M" }, Drawn(spec, "one"));
            Assert.False(SplitsASection(spec, "one"));

            // A field declared first, in a section that is not the page's first, goes to the end of its own section.
            spec = Opened(Schema.Parse(Declared(@"{ ""key"": ""f"", ""default"": ""x"", ""page"": ""one"", ""section"": ""A"" },")), Sorted);
            Assert.Equal(new[] { "c:B", "d:B", "a:A", "b:A", "n:A", "f:A", "m:M" }, Drawn(spec, "one"));
            Assert.False(SplitsASection(spec, "one"));
        }

        [Fact]
        public void A_field_given_back_by_Reset_this_field_returns_to_its_own_section_on_a_sorted_page()
        {
            var schema = Schema.Parse(@"{ ""host"": ""demo"", ""settings"": [
                { ""key"": ""a"", ""default"": ""x"", ""page"": ""one"", ""section"": ""A"" },
                { ""key"": ""b"", ""default"": ""x"", ""page"": ""one"", ""section"": ""A"" },
                { ""key"": ""c"", ""default"": ""x"", ""page"": ""one"", ""section"": ""B"" },
                { ""key"": ""d"", ""default"": ""x"", ""page"": ""one"", ""section"": ""B"" },
                { ""key"": ""g"", ""default"": ""x"", ""page"": ""one"", ""section"": ""G"" } ],
                ""window"": { ""title"": ""Demo"", ""pages"": [ ""one"" ] } }");
            // The user dragged b between c and d, where it took section B, and put A below B.
            var spec = Opened(schema, layout =>
            {
                layout.SetField("c", "order", 0);
                layout.SetField("b", "order", 10);
                layout.SetField("b", "section", "B");
                layout.SetField("d", "order", 20);
                layout.SetField("a", "order", 30);
                layout.SetField("g", "order", 40);
            });
            Assert.Equal(new[] { "c:B", "b:B", "d:B", "a:A", "g:G" }, Drawn(spec, "one"));

            // Reset this field takes b's entry away: its own section A again, and no order. Before, it went back to its
            // place among all settings, 1, between c and d, and both headings showed twice.
            spec.Layout.ResetField("b");
            spec.Layout.Save();
            Entry.Prepare(spec, schema);
            Assert.Equal(new[] { "c:B", "d:B", "a:A", "b:A", "g:G" }, Drawn(spec, "one"));
            Assert.False(SplitsASection(spec, "one"));

            // A field without a section sits wherever it lands: behind the field declared before it, in that one's section.
            var plain = Schema.Parse(@"{ ""host"": ""demo"", ""settings"": [
                { ""key"": ""a"", ""default"": ""x"", ""page"": ""one"", ""section"": ""A"" },
                { ""key"": ""p"", ""default"": ""x"", ""page"": ""one"" },
                { ""key"": ""c"", ""default"": ""x"", ""page"": ""one"", ""section"": ""B"" } ],
                ""window"": { ""title"": ""Demo"", ""pages"": [ ""one"" ] } }");
            spec = Opened(plain, layout => { layout.SetField("c", "order", 0); layout.SetField("a", "order", 10); });
            Assert.Equal(new[] { "c:B", "a:A", "p:" }, Drawn(spec, "one"));
        }
    }
}
