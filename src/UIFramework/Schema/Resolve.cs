using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace UIFramework
{
    /// <summary>
    /// Turns the four layers into the pages the shell shows: the conventions complete each setting, the schema
    /// says what exists, the spec says where it goes and what the window is called, the layout (phase 4) is the
    /// user's last word. The result lands in <see cref="WindowSpec.Pages"/>, <see cref="WindowSpec.Defaults"/>,
    /// <see cref="WindowSpec.Messages"/> and the alias maps.
    /// </summary>
    public static class Resolve
    {
        private static readonly string[] TrailingPages = { "log", "help", "about" };

        public static void Apply(WindowSpec spec, Schema schema, Conventions conventions, LayoutFile layout = null)
        {
            conventions = conventions ?? Conventions.Default;
            var lang = conventions.Lang;
            bool customizing = spec.Customizing;

            spec.Problems.Clear();

            // 1. Complete every setting and lay the spec's overrides on top.
            var settings = new List<Setting>();
            foreach (var declared in schema.Settings)
            {
                var setting = conventions.Complete(declared);
                if (spec.Overrides.TryGetValue(setting.Key, out var over))
                {
                    setting.Page = over.Page ?? setting.Page;
                    setting.Label = over.Label ?? setting.Label;
                    setting.Help = over.Help ?? setting.Help;
                    setting.Section = over.Section ?? setting.Section;
                    if (over.Hidden.HasValue) setting.Hidden = over.Hidden.Value;
                    if (over.Order.HasValue) setting.Order = over.Order.Value;
                }
                // A section shown only on a condition: its fields carry it, looked up by the declared name.
                if (setting.Section != null && spec.SectionConditions.TryGetValue(setting.Section, out var sectionCondition))
                    setting.SectionShowWhen = sectionCondition;
                // Another language than the game's: the conventions (and the game's own translations, laid over
                // them) translate what they know; the rest stays as declared. The options are the setting's own copy.
                if (lang.Translates)
                {
                    var text = lang.Key(setting.Key);
                    setting.Label = text.Label ?? lang.Text(setting.Label);
                    setting.Help = text.Help ?? lang.Text(setting.Help);
                    setting.Section = lang.Text(setting.Section);
                    setting.Group = lang.Text(setting.Group);
                    if (setting.Options != null)
                        foreach (var option in setting.Options)
                            option.Label = lang.Text(option.Label);
                }
                // The user's layout has the last word on where a field sits and what it is called.
                var mine = layout?.Field(setting.Key);
                if (mine != null)
                {
                    setting.Page = mine.Page ?? setting.Page;
                    setting.Label = mine.Label ?? setting.Label;
                    setting.Section = mine.Section ?? setting.Section;
                    if (mine.Hidden.HasValue) setting.Hidden = mine.Hidden.Value;
                    if (mine.Order.HasValue) setting.Order = mine.Order.Value;
                    // "" is a line of its own: a row nobody else names.
                    if (mine.Row != null) setting.Row = mine.Row.Length == 0 ? "~" + setting.Key : mine.Row;
                }
                settings.Add(setting);
            }
            Shows(spec, settings);
            spec.ResolvedSettings.Clear();
            spec.ResolvedSettings.AddRange(settings);
            Check(spec, settings);
            foreach (var pill in spec.HeaderPills)
                pill.Tooltip = lang.Text(pill.Declared);
            Currency(spec, schema, settings);

            // 2. Defaults and aliases for the store.
            spec.Defaults = new JObject();
            spec.Aliases.Clear();
            spec.AliasFactors.Clear();
            spec.AliasesShared.Clear();
            foreach (var setting in settings)
            {
                if (setting.Kind != "table" && setting.Kind != "action" && setting.Kind != "note")
                    spec.Defaults[setting.Key] = setting.Default?.DeepClone() ?? JValue.CreateNull();
                foreach (var alias in setting.Aliases)
                {
                    if (!spec.Aliases.ContainsKey(alias))
                        spec.Aliases[alias] = setting.Key;
                    else if (spec.Aliases[alias] != setting.Key)
                    {
                        // One old key for two settings: both take its value on the move.
                        if (!spec.AliasesShared.TryGetValue(alias, out var more))
                            spec.AliasesShared[alias] = more = new List<string>();
                        if (!more.Contains(setting.Key))
                            more.Add(setting.Key);
                    }
                }
                foreach (var factor in setting.AliasFactors)
                    spec.AliasFactors[factor.Key] = factor.Value;
            }
            if (string.IsNullOrEmpty(spec.Version) && !string.IsNullOrEmpty(schema.Version))
                spec.Version = schema.Version;

            // 3. Sender switches exist only when the schema declares them.
            var keys = new HashSet<string>(settings.Select(s => s.Key), StringComparer.Ordinal);
            string Present(string key) => key != null && keys.Contains(key) ? key : null;
            spec.Senders.UseBot = Present(spec.Senders.UseBot);
            spec.Senders.SendAction = Present(spec.Senders.SendAction);
            spec.Senders.CooldownUseBot = Present(spec.Senders.CooldownUseBot);
            spec.Senders.CooldownSendAction = Present(spec.Senders.CooldownSendAction);
            spec.Senders.NumberSeparator = Present(spec.Senders.NumberSeparator);
            var senderKeys = new HashSet<string>(new[] { spec.Senders.UseBot, spec.Senders.SendAction, spec.Senders.CooldownUseBot, spec.Senders.CooldownSendAction, spec.Senders.NumberSeparator }.Where(k => k != null), StringComparer.Ordinal);

            // 4. Templates on the messages page become the picker's entries.
            spec.Messages.Clear();
            foreach (var setting in settings.Where(s => s.Kind == "template" && s.Page == "messages" && !s.Hidden).OrderBy(s => s.Order))
                spec.Messages.Add(new MessageDef(setting.Group ?? lang.Page("messages") ?? "Messages", setting.Key, setting.Label, setting.Help ?? "", (setting.Placeholders ?? new List<string>()).ToArray()));
            if (spec.SampleValues == null)
                spec.SampleValues = ctx => SampleValues(ctx, settings);
            if (spec.TokenHelp == null)
                spec.TokenHelp = (key, token) => lang.Placeholder(token);

            // 5. Pages: the spec's order first, then whatever the settings still need, standard pages last.
            var claimed = new HashSet<string>(StringComparer.Ordinal);
            var pageFields = new Dictionary<string, List<Setting>>(StringComparer.Ordinal);
            var pageOrder = new List<SpecPage>();

            foreach (var page in spec.SpecPages)
            {
                pageOrder.Add(page);
                var mine = new List<Setting>();
                foreach (var pattern in page.Fields)
                {
                    if (!Match(pattern, settings).Any())
                        spec.Problems.Add("Page '" + page.Id + "' names '" + pattern + "', which no setting matches.");
                    foreach (var setting in Match(pattern, settings).Where(s => !claimed.Contains(s.Key)))
                    {
                        claimed.Add(setting.Key);
                        mine.Add(setting);
                    }
                }
                pageFields[page.Id] = mine;
            }

            foreach (var setting in settings.OrderBy(s => s.Order))
            {
                if (claimed.Contains(setting.Key) || (setting.Hidden && !customizing) || senderKeys.Contains(setting.Key))
                    continue;
                if (setting.Key == spec.Log.WriteLogKey)
                    continue;
                if (setting.Kind == "template" && setting.Page == "messages")
                    continue;
                string pageId = setting.Page;
                if (!pageFields.ContainsKey(pageId))
                {
                    pageFields[pageId] = new List<Setting>();
                    pageOrder.Add(new SpecPage { Id = pageId });
                }
                pageFields[pageId].Add(setting);
            }

            // Standard pages the spec did not name: messages when there are templates, about always.
            if (spec.Messages.Count > 0 && pageOrder.All(p => p.Id != "messages"))
                pageOrder.Add(new SpecPage { Id = "messages" });
            if (pageOrder.All(p => p.Id != "about"))
                pageOrder.Add(new SpecPage { Id = "about", Bottom = true });

            // Pages the spec did not order: by convention order, trailing standard pages last.
            var explicitIds = new HashSet<string>(spec.SpecPages.Select(p => p.Id), StringComparer.Ordinal);
            int Rank(SpecPage p)
            {
                if (explicitIds.Contains(p.Id)) return spec.SpecPages.FindIndex(x => x.Id == p.Id);
                int trailing = Array.IndexOf(TrailingPages, p.Id);
                if (trailing >= 0) return 10000 + trailing;
                return 1000 + (conventions.Pages.TryGetValue(p.Id, out var c) ? c.Order : 500);
            }
            // Pages the user put in order come first, in that order; the rest follow by rank. The customise mode
            // writes an order for every page in the top group, so a drag never mixes the two.
            int? UserOrder(SpecPage p) => Array.IndexOf(TrailingPages, p.Id) >= 0 || p.Bottom ? null : layout?.Page(p.Id)?.Order;
            pageOrder = pageOrder.OrderBy(p => UserOrder(p).HasValue ? 0 : 1).ThenBy(p => UserOrder(p) ?? 0).ThenBy(Rank).ToList();

            spec.Pages.Clear();
            foreach (var page in pageOrder)
            {
                var userPage = layout?.Page(page.Id);
                bool hidden = userPage?.Hidden == true && page.Id != "about";
                if (hidden && !customizing)
                    continue;
                string title = userPage?.Title
                    ?? (lang.Translates ? lang.TextOrNull(page.Title) ?? lang.Page(page.Id) ?? page.Title : page.Title ?? lang.Page(page.Id))
                    ?? Conventions.Humanize(page.Id);
                string icon = userPage?.Icon != null ? IconGlyph(userPage.Icon) : page.Icon != null ? IconGlyph(page.Icon) : conventions.Pages.TryGetValue(page.Id, out var c) ? IconGlyph(c.Icon) : "•";
                // Help and About sit in their own group at the bottom of the rail, apart from the settings pages.
                bool bottom = page.Bottom || page.Id == "about" || page.Id == "help";
                var fields = pageFields.TryGetValue(page.Id, out var list) ? list : new List<Setting>();

                Func<PageContext, System.Windows.UIElement> build;
                if (page.Custom)
                    build = null;
                else if (page.Id == "messages")
                    build = StandardPages.BuildMessages;
                else if (page.Id == "log")
                    build = StandardPages.BuildLog;
                else if (page.Id == "help")
                    build = StandardPages.BuildHelp;
                else if (page.Id == "about")
                    build = StandardPages.BuildAbout;
                else
                {
                    var captured = fields;
                    string pageId = page.Id;
                    build = ctx => SchemaPage.Build(ctx, captured, pageId);
                }
                // Fields the spec put on a standard page sit above its own content.
                if (build != null && fields.Count > 0 && (page.Id == "messages" || page.Id == "log" || page.Id == "help" || page.Id == "about"))
                {
                    var inner = build;
                    var captured = fields;
                    build = ctx => SchemaPage.Prepend(ctx, captured, inner(ctx));
                }
                spec.Pages.Add(new PageSpec(page.Id, title, icon, build, bottom) { Hidden = hidden });
            }
        }

        private static readonly HashSet<string> Kinds = new HashSet<string>(StringComparer.Ordinal)
        {
            "bool", "int", "long", "duration", "double", "percent", "options", "namelist", "grouplist", "colour", "color",
            "path", "emote", "slider", "note", "text", "string", "template", "action", "table", "list"
        };

        /// <summary>A dropdown's <c>shows</c> becomes a <c>showWhen</c> on every field it names: shown while one of the
        /// options naming it is picked. A field that has a condition already gets this one on top (both must hold).</summary>
        private static void Shows(WindowSpec spec, List<Setting> settings)
        {
            var byKey = settings.GroupBy(s => s.Key, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
            var gathered = new Dictionary<string, Dictionary<string, List<string>>>(StringComparer.Ordinal);   // field -> dropdown -> values
            foreach (var dropdown in settings.Where(s => s.Shows != null))
                foreach (var option in dropdown.Shows)
                    foreach (var key in option.Value)
                    {
                        if (!byKey.ContainsKey(key))
                        {
                            spec.Problems.Add("'" + dropdown.Key + "' shows '" + key + "' with '" + option.Key + "', which is not a setting.");
                            continue;
                        }
                        if (!gathered.TryGetValue(key, out var gates)) gathered[key] = gates = new Dictionary<string, List<string>>(StringComparer.Ordinal);
                        if (!gates.TryGetValue(dropdown.Key, out var values)) gates[dropdown.Key] = values = new List<string>();
                        if (!values.Contains(option.Key)) values.Add(option.Key);
                    }
            foreach (var entry in gathered)
            {
                var field = byKey[entry.Key];
                var conditions = entry.Value.Select(g => g.Key + "=" + string.Join("|", g.Value)).ToList();
                if (!string.IsNullOrWhiteSpace(field.ShowWhen)) conditions.Insert(0, field.ShowWhen);
                field.ShowWhen = string.Join(" && ", conditions);
            }
        }

        /// <summary>A condition's gates are settings, and a dropdown's wanted values are among its options.</summary>
        private static void CheckCondition(WindowSpec spec, Dictionary<string, Setting> byKey, string owner, string what, string condition)
        {
            string text = (condition ?? "").Trim();
            if (text.StartsWith("hide:", StringComparison.OrdinalIgnoreCase)) text = text.Substring(5);
            foreach (var part in Requirement.Parse(text))
            {
                string key = part.Key.TrimStart('!').Trim();
                if (!byKey.TryGetValue(key, out var gate))
                {
                    spec.Problems.Add(owner + " " + what + " '" + key + "', which is not a setting.");
                    continue;
                }
                if ((part.Op == "=" || part.Op == "!=") && gate.Options != null && gate.Options.Count > 0 && (gate.Kind == "options" || gate.Kind == "slider"))
                    foreach (var value in part.Values)
                        if (!gate.Options.Any(o => string.Equals(o.Value, value, StringComparison.OrdinalIgnoreCase)))
                            spec.Problems.Add(owner + " " + what + " '" + key + "=" + value + "', but '" + key + "' has no option '" + value + "'.");
            }
        }

        /// <summary>What a host's author got wrong, in words: an unknown kind, a requirement on a key that is not
        /// there, a page or pill naming a setting that does not exist.</summary>
        private static void Check(WindowSpec spec, List<Setting> settings)
        {
            var keys = new HashSet<string>(settings.Select(s => s.Key), StringComparer.Ordinal);
            var byKey = settings.GroupBy(s => s.Key, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
            foreach (var section in spec.SectionConditions)
                CheckCondition(spec, byKey, "The section '" + section.Key + "'", "is shown when", section.Value);
            foreach (var s in settings)
            {
                if (s.Kind != null && !Kinds.Contains(s.Kind))
                    spec.Problems.Add("'" + s.Key + "' has the kind '" + s.Kind + "', which the framework does not know; it is shown as text.");
                if ((s.Kind == "options" || s.Kind == "slider") && (s.Options == null || s.Options.Count == 0))
                    spec.Problems.Add("'" + s.Key + "' is " + (s.Kind == "slider" ? "a slider" : "an options field") + " without options.");
                if (s.Kind == "list" && (s.Columns == null || s.Columns.Count == 0))
                    spec.Problems.Add("'" + s.Key + "' is a list without columns.");
                if (!string.IsNullOrWhiteSpace(s.Requires))
                    CheckCondition(spec, byKey, "'" + s.Key + "'", "requires", s.Requires);
                if (!string.IsNullOrWhiteSpace(s.ShowWhen))
                    CheckCondition(spec, byKey, "'" + s.Key + "'", "is shown when", s.ShowWhen);
                if (s.Min.HasValue && s.Max.HasValue && s.Min > s.Max)
                    spec.Problems.Add("'" + s.Key + "' has a minimum above its maximum.");
            }
            if (!string.IsNullOrEmpty(spec.Log.WriteLogKey) && !keys.Contains(spec.Log.WriteLogKey))
                spec.Problems.Add("The log page's switch '" + spec.Log.WriteLogKey + "' is not a setting.");
            foreach (var key in spec.HeaderPillKeys)
                if (!keys.Contains(key))
                    spec.Problems.Add("The header pill '" + key + "' is not a setting.");
            foreach (var key in spec.Overrides.Keys)
                if (!keys.Contains(key))
                    spec.Problems.Add("The spec overrides '" + key + "', which is not a setting.");
        }

        /// <summary>A points system the game found installed: a pill in the header, and the two points settings say
        /// they are the fallback while it is there.</summary>
        private static void Currency(WindowSpec spec, Schema schema, List<Setting> settings)
        {
            if (spec.CurrencyPill != null) { spec.HeaderPills.Remove(spec.CurrencyPill); spec.CurrencyPill = null; }
            string source = (string)schema.Currency?["source"];
            if (string.IsNullOrWhiteSpace(source)) return;
            string variable = (string)schema.Currency["variable"] ?? "", name = (string)schema.Currency["name"] ?? "";
            string note = source + " is installed: the points live in the user variable '" + variable + "' and are called '" + name + "'. " +
                "This setting is used only without it.";
            foreach (var s in settings)
                if (s.Key == "pointsVariable" || s.Key == "pointsName")
                    s.Help = string.IsNullOrEmpty(s.Help) ? note : s.Help + " " + note;
            string text = source + ": " + variable;
            string tooltip = "Points by " + source + ", in the user variable '" + variable + "', called '" + name + "'. The Points variable and Points name settings are not used while it is installed.";
            spec.CurrencyPill = new HeaderPill(tooltip, ctx => (text, PillStyle.Accent));
            spec.HeaderPills.Add(spec.CurrencyPill);
        }

        private static IEnumerable<Setting> Match(string pattern, List<Setting> settings)
        {
            if (!pattern.Contains("*"))
                return settings.Where(s => string.Equals(s.Key, pattern, StringComparison.Ordinal));
            var regex = new Regex("^" + Regex.Escape(pattern).Replace("\\*", ".*") + "$", RegexOptions.IgnoreCase);
            // A wildcard never pulls a message template off the Messages page; name it to move it.
            return settings.Where(s => regex.IsMatch(s.Key) && !(s.Kind == "template" && s.Page == "messages")).OrderBy(s => s.Order);
        }

        /// <summary>The emoji for a convention icon name, so a spec may name either.</summary>
        public static string IconGlyph(string name)
        {
            switch ((name ?? "").ToLowerInvariant())
            {
                case "gear": return "⚙️";
                case "dice": return "🎲";
                case "coins": return "🪙";
                case "clock": return "⏱️";
                case "shield": return "👥";
                case "chat": return "💬";
                case "trophy": return "🏆";
                case "monitor": return "📺";
                case "list": return "📜";
                case "book": return "📖";
                case "info": return "ℹ️";
                case "cards": return "🃏";
                case "chart": return "📊";
                case "": return "•";
                default: return name;
            }
        }

        private static Dictionary<string, string> SampleValues(PageContext ctx, List<Setting> settings)
        {
            var f = ctx.Fields;
            var keys = new HashSet<string>(settings.Select(s => s.Key), StringComparer.Ordinal);
            string separator = ctx.Spec.Senders.NumberSeparator ?? "numberSeparator";
            string Amount(double value) => f.Amount(value, separator);
            string pointsName = keys.Contains("pointsName") ? f.LiveText("pointsName", "Points").Trim() : "Points";
            var samples = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                { "user", "alice" }, { "target", "bob" }, { "pointsName", pointsName }, { "points", pointsName }, { "currency", pointsName },
                { "amount", Amount(1500) }, { "currentPoints", Amount(11500) },
                { "min", Amount(keys.Contains("minBet") ? f.LiveNumber("minBet", 50) : 50) },
                { "max", Amount(keys.Contains("maxBet") ? Math.Max(1, f.LiveNumber("maxBet", 10000)) : 10000) },
                { "remaining", "12" }, { "reason", "not enough points" }, { "rank", "1" }, { "score", Amount(2500) },
                { "medal", "🥇" }, { "count", "5" }, { "latestTag", "v2.0.0" }, { "error", "timeout" },
                { "addictedCut", (keys.Contains("addictedPayoutCut") ? f.LiveNumber("addictedPayoutCut", 50) : 50).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) }
            };
            foreach (var fixedSample in ctx.Spec.Samples)
                samples[fixedSample.Key] = fixedSample.Value;
            return samples;
        }
    }
}
