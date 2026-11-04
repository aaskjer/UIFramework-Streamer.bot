using System.Linq;
using Xunit;

namespace UIFramework.Tests
{
    /// <summary>The window's own texts: every language file carries every key, and a window in German reads them.</summary>
    public class LangTests
    {
        [Fact]
        public void GermanCarriesEveryTextTheWindowHas()
        {
            var english = Conventions.Default.Lang;
            var german = Conventions.Load("de").Lang;
            Assert.True(english.Ui.Count > 150, "the English ui section is missing or thin");
            var missing = english.Ui.Keys.Where(k => !german.Ui.TryGetValue(k, out var text) || text == english.Ui[k] && !k.StartsWith("btn.ok") && !k.StartsWith("about.version") && !k.StartsWith("about.links") && !k.StartsWith("about.releases") && !k.StartsWith("pill.update")).ToList();
            Assert.True(missing.Count == 0, "not translated: " + string.Join(", ", missing));
            var extra = german.Ui.Keys.Where(k => !english.Ui.ContainsKey(k)).ToList();
            Assert.True(extra.Count == 0, "German has keys English lacks: " + string.Join(", ", extra));
            foreach (var key in english.Ui.Keys)
            {
                // The same placeholders in both, so string.Format cannot fail in one language only.
                var mine = System.Text.RegularExpressions.Regex.Matches(english.Ui[key], @"\{\d\}").Cast<System.Text.RegularExpressions.Match>().Select(m => m.Value).OrderBy(v => v);
                var theirs = System.Text.RegularExpressions.Regex.Matches(german.Ui[key], @"\{\d\}").Cast<System.Text.RegularExpressions.Match>().Select(m => m.Value).OrderBy(v => v);
                Assert.True(mine.SequenceEqual(theirs), key + " has other placeholders in German");
            }
        }

        [Fact]
        public void TextFollowsTheLanguageAndFallsBackToEnglish()
        {
            try
            {
                Text.Use(Conventions.Load("de").Lang);
                Assert.Equal("Speichern", Text.T("btn.save"));
                Assert.Equal("3 Änderungen offen", Text.T("status.unsaved.many", 3));
                Text.Use(Conventions.Load("xx").Lang);   // no such file: English
                Assert.Equal("Save", Text.T("btn.save"));
                Assert.Equal("no.such.key", Text.T("no.such.key"));
            }
            finally { Text.Use(Conventions.Default.Lang); }
        }
    }
}
