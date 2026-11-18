using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using Xunit;

namespace UIFramework.Tests
{
    /// <summary>A field a Set Argument of the project's action sets, and one the project locks itself: named in the
    /// schema (<c>arguments</c>, <c>lockedFields</c>), shown with a pill and the reason, never edited or saved.</summary>
    public class LockedFieldTests
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

        private const string Settings = @"{ ""host"": ""demo"", ""version"": ""v1"", ""settings"": [
            { ""key"": ""dryRun"", ""default"": false, ""page"": ""game"", ""label"": ""Dry run"" },
            { ""key"": ""token"", ""default"": """", ""page"": ""game"", ""label"": ""Token"", ""masked"": true },
            { ""key"": ""perHour"", ""default"": 6, ""page"": ""game"", ""label"": ""Per hour"" },
            { ""key"": ""check"", ""kind"": ""action"", ""page"": ""game"", ""label"": ""Check"" } ]";

        private static WindowSpec Prepared(string rest)
        {
            try
            {
                var schema = Schema.Parse(Settings + rest + " }");
                var spec = Entry.SpecFor(schema, null);
                Entry.Prepare(spec, schema);
                return spec;
            }
            finally { Text.Use(Conventions.Default.Lang); }
        }

        private static IEnumerable<DependencyObject> Walk(DependencyObject node)
        {
            yield return node;
            foreach (var child in LogicalTreeHelper.GetChildren(node).OfType<DependencyObject>())
                foreach (var below in Walk(child))
                    yield return below;
        }

        private static string TextOf(DependencyObject root) =>
            string.Join(" | ", Walk(root).OfType<TextBlock>().Select(t => t.Inlines.Count > 0 ? string.Concat(t.Inlines.OfType<Run>().Select(r => r.Text)) : t.Text).Where(t => !string.IsNullOrEmpty(t)));

        [Fact]
        public void TheSchemaNamesTheArgumentsByKeyOrAsAList()
        {
            var byName = Schema.Parse(Settings + @", ""arguments"": { ""dryRun"": ""dry_run"", ""perHour"": """" } }");
            Assert.Equal("dry_run", byName.Arguments["dryRun"]);
            Assert.Equal("perHour", byName.Arguments["perHour"]);           // no name: the argument is called as the key
            var listed = Schema.Parse(Settings + @", ""arguments"": [ ""dryRun"" ], ""lockedFields"": { ""token"": ""Held by the bot."" } }");
            Assert.Equal("dryRun", listed.Arguments["dryRun"]);
            Assert.Equal("Held by the bot.", listed.LockedFields["token"]);
            // Both go back out as they came in.
            var again = Schema.Parse(listed.ToJson());
            Assert.Equal("dryRun", again.Arguments["dryRun"]);
            Assert.Equal("Held by the bot.", again.LockedFields["token"]);
            Assert.Empty(Schema.Parse(Settings + " }").Arguments);
        }

        [Fact]
        public void ALockedFieldIsShownWithItsPillAndReasonAndNotEdited() => Sta(() =>
        {
            var spec = Prepared(@", ""arguments"": { ""dryRun"": ""dry_run"", ""check"": ""check"" },
                ""lockedFields"": { ""token"": ""Held by the bot."", ""perHour"": """" }");
            Assert.Empty(spec.Problems);
            Assert.True(spec.ResolvedSettings.First(s => s.Key == "dryRun").Locked);
            Assert.False(spec.ResolvedSettings.First(s => s.Key == "check").Locked);      // a button holds no value to lock

            var parts = new WindowParts();
            var settings = new JObject { ["dryRun"] = true, ["token"] = "secret-value", ["perHour"] = 9 };
            var ctx = Entry.ContextFor(spec, settings, parts, null, new NoHooks());
            var page = (DependencyObject)spec.Pages.First(p => p.Id == "game").Build(ctx);
            string text = TextOf(page);

            // The argument's field: pill and where the value comes from, by the argument's own name; no value, since
            // the file's is not the one that counts.
            Assert.Contains("Dry run", text);
            Assert.Contains(Text.T("field.setArgument"), text);
            Assert.Contains(Text.T("field.setArgument.why", "dry_run"), text);
            // The project's: pill, reason and the value; a secret as dots.
            Assert.Contains(Text.T("field.locked"), text);
            Assert.Contains("Held by the bot.", text);
            Assert.DoesNotContain("secret-value", text);
            Assert.Contains("9", text);
            // None of the three has a field to type into.
            Assert.DoesNotContain(Walk(page).OfType<CheckBox>(), box => true);
            Assert.DoesNotContain(Walk(page).OfType<TextBox>(), box => true);
            Assert.DoesNotContain(Walk(page).OfType<PasswordBox>(), box => true);
        });

        [Fact]
        public void LockingAKeyThatIsNoSettingIsNamed()
        {
            var spec = Prepared(@", ""lockedFields"": { ""nothere"": ""x"" }");
            var problem = Assert.Single(spec.Problems);
            Assert.Contains("'nothere'", problem);
            Assert.Contains("locked:", problem);
        }
    }
}
