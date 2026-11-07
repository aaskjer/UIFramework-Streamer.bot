using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using Xunit;

namespace UIFramework.Tests
{
    /// <summary>Spanish: the file itself carries every key, page, placeholder and window text of the English one, with
    /// the same placeholders, and a window in Spanish reads it.</summary>
    public class SpanishTests
    {
        private static JObject File_(string code)
        {
            // The files as they ship inside the DLL, not merged over English: a key left out of es.json would
            // otherwise pass unseen.
            using (var stream = typeof(Conventions).Assembly.GetManifestResourceStream("UIFramework.conventions.lang." + code + ".json"))
            {
                Assert.True(stream != null, code + ".json is not embedded in UIFramework.dll");
                using (var reader = new StreamReader(stream))
                    return JObject.Parse(reader.ReadToEnd());
            }
        }

        /// <summary>Every text in a section by key ("label" and "help" of a key apart).</summary>
        private static Dictionary<string, string> Texts(JObject root, string section)
        {
            var texts = new Dictionary<string, string>();
            foreach (var p in (root[section] as JObject)?.Properties() ?? Enumerable.Empty<JProperty>())
            {
                if (p.Value is JObject o)
                {
                    foreach (var part in o.Properties())
                        texts[p.Name + "." + part.Name] = (string)part.Value;
                }
                else
                    texts[p.Name] = (string)p.Value;
            }
            return texts;
        }

        private static IEnumerable<string> Placeholders(string text) =>
            Regex.Matches(text ?? "", @"\{[^{}\s]+\}").Cast<Match>().Select(m => m.Value).OrderBy(v => v, System.StringComparer.Ordinal);

        [Theory]
        [InlineData("pages")]
        [InlineData("framework")]
        [InlineData("keys")]
        [InlineData("placeholders")]
        [InlineData("ui")]
        public void SpanishCarriesEverySectionEntryOfEnglishWithTheSamePlaceholders(string section)
        {
            var english = Texts(File_("en"), section);
            var spanish = Texts(File_("es"), section);
            Assert.True(english.Count > 0, section + " is empty in English");
            var missing = english.Keys.Where(k => !spanish.ContainsKey(k) || string.IsNullOrWhiteSpace(spanish[k]) && !string.IsNullOrWhiteSpace(english[k])).ToList();
            Assert.True(missing.Count == 0, section + ": missing in es.json: " + string.Join(", ", missing));
            var extra = spanish.Keys.Where(k => !english.ContainsKey(k)).ToList();
            Assert.True(extra.Count == 0, section + ": es.json has entries English lacks: " + string.Join(", ", extra));
            foreach (var key in english.Keys)
                Assert.True(Placeholders(english[key]).SequenceEqual(Placeholders(spanish[key])), section + "." + key + " has other placeholders in Spanish");
        }

        [Fact]
        public void SpanishTranslatesTheShortWordingsGermanDoes()
        {
            var german = (JObject)File_("de")["texts"];
            var spanish = (JObject)File_("es")["texts"];
            var missing = german.Properties().Select(p => p.Name).Where(k => spanish[k] == null || string.IsNullOrWhiteSpace((string)spanish[k])).ToList();
            Assert.True(missing.Count == 0, "texts missing in es.json: " + string.Join(", ", missing));
        }

        [Fact]
        public void AWindowInSpanishReadsIt()
        {
            var spanish = Conventions.Load("es").Lang;
            Assert.Equal("es", spanish.Code);
            Assert.True(spanish.Translates);
            Assert.Equal("Guardar", spanish.Ui["btn.save"]);
            Assert.Equal("Cancelar", spanish.Ui["btn.cancel"]);
            Assert.Equal("Juego", spanish.Page("game"));
            Assert.Equal("Apuesta máxima", spanish.Key("maxBet").Label);
            Assert.Equal("Puntos", spanish.Text("Points"));
            Assert.Equal("Tiempo de espera", spanish.Text("Cooldown"));
            Assert.Null(spanish.TextOrNull("Shoe"));   // what the file does not know stays as declared
        }
    }
}
