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
                // The user's layout has the last word on where a field sits and what it is called.
                var mine = layout?.Field(setting.Key);
                if (mine != null)
                {
                    setting.Page = mine.Page ?? setting.Page;
                    setting.Label = mine.Label ?? setting.Label;
                    setting.Section = mine.Section ?? setting.Section;
                    if (mine.Hidden.HasValue) setting.Hidden = mine.Hidden.Value;
                    if (mine.Order.HasValue) setting.Order = mine.Order.Value;
                }
                settings.Add(setting);
            }
            spec.ResolvedSettings.Clear();
            spec.ResolvedSettings.AddRange(settings);

            // 2. Defaults and aliases for the store.
            spec.Defaults = new JObject();
            spec.Aliases.Clear();
            spec.AliasFactors.Clear();
            foreach (var setting in settings)
            {
                if (setting.Kind != "table" && setting.Kind != "action")
                    spec.Defaults[setting.Key] = setting.Default?.DeepClone() ?? JValue.CreateNull();
                foreach (var alias in setting.Aliases)
                    if (!spec.Aliases.ContainsKey(alias))
                        spec.Aliases[alias] = setting.Key;
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
                    foreach (var setting in Match(pattern, settings).Where(s => !claimed.Contains(s.Key)))
                    {
                        claimed.Add(setting.Key);
                        mine.Add(setting);
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
                string title = userPage?.Title ?? page.Title ?? lang.Page(page.Id) ?? Conventions.Humanize(page.Id);
                string icon = userPage?.Icon != null ? IconGlyph(userPage.Icon) : page.Icon ?? (conventions.Pages.TryGetValue(page.Id, out var c) ? IconGlyph(c.Icon) : "•");
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
                    build = ctx => SchemaPage.Build(ctx, captured);
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
