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
        // The group at the foot of the rail, in this order whatever order the game declares them in: how to play, what
        // happened, who made it.
        private static readonly string[] TrailingPages = { "help", "log", "about" };

        public static void Apply(WindowSpec spec, Schema schema, Conventions conventions, LayoutFile layout = null)
        {
            conventions = conventions ?? Conventions.Default;
            var lang = conventions.Lang;
            bool customizing = spec.Customizing;

            spec.Problems.Clear();

            // 1. Complete every setting and lay the spec's overrides on top.
            var settings = new List<Setting>();
            var declaredSections = new HashSet<string>(StringComparer.Ordinal);
            // Where the game put each field, before the user's layout moves it: what the check of split sections reads.
            var asDeclared = new List<(string Page, int Order, string Section, bool Hidden)>();
            foreach (var declared in schema.Settings)
            {
                // A known key declared as another kind of value is most likely the game's own idea under a taken name:
                // it would still get the conventions' limits, old keys and translations.
                if (declared.Kind != null && conventions.Keys.TryGetValue(declared.Key, out var knownKey) && knownKey.Kind != null
                    && KindFamily(declared.Kind) != KindFamily(knownKey.Kind))
                    spec.Problems.Add("'" + declared.Key + "' is a known key of the conventions (" + (Conventions.Default.Lang.Key(declared.Key).Label ?? declared.Key) +
                        ", " + knownKey.Kind + "), declared as '" + declared.Kind + "': it still takes the conventions' limits, old keys and translations. A key of the game's own fits another meaning.");
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
                string declaredSection = setting.Section;
                if (declaredSection != null) declaredSections.Add(declaredSection);
                // Another language than the game's: the conventions (and the game's own translations, laid over
                // them) translate what they know; the rest stays as declared. The options are the setting's own copy.
                // In this order: the game's own text for the key (K); the game's translation of the wording it declared
                // (Tx), when that wording is its own and not the conventions' English; the conventions' text for the
                // key; the declared wording. A game that relabels a conventions key without translating the new label
                // so still gets the conventions' translation rather than its own English.
                if (lang.Translates)
                {
                    var text = lang.Key(setting.Key);
                    var own = lang.OwnKey(setting.Key);
                    var english = lang.EnglishKey(setting.Key);
                    string declaredLabel = over?.Label ?? declared.Label, declaredHelp = over?.Help ?? declared.Help;
                    string ownLabel = declaredLabel != null && declaredLabel != english.Label ? lang.TextOrNull(setting.Label) : null;
                    string ownHelp = declaredHelp != null && declaredHelp != english.Help ? lang.TextOrNull(setting.Help) : null;
                    // The key's text where it is a translation: a language without a conventions file of its own has
                    // the English one, which never replaces what the game worded itself.
                    string keyLabel = text.Label != null && (declaredLabel == null || text.Label != english.Label) ? text.Label : null;
                    string keyHelp = text.Help != null && (declaredHelp == null || text.Help != english.Help) ? text.Help : null;
                    setting.Label = own.Label ?? ownLabel ?? keyLabel ?? lang.Text(setting.Label);
                    setting.Help = own.Help ?? ownHelp ?? keyHelp ?? lang.Text(setting.Help);
                    setting.Section = lang.Text(setting.Section);
                    setting.Group = lang.Text(setting.Group);
                    if (setting.Options != null)
                        foreach (var option in setting.Options)
                            option.Label = lang.Text(option.Label);
                }
                asDeclared.Add((setting.Page, setting.Order, setting.Section, setting.Hidden));
                // The user's layout has the last word on where a field sits and what it is called. A section it names
                // by a name the game declared reads in the window's language, as the game's own sections do.
                var mine = layout?.Field(setting.Key);
                setting.SectionId = mine?.Section ?? declaredSection;
                if (mine != null)
                {
                    setting.Page = mine.Page ?? setting.Page;
                    setting.Label = mine.Label ?? setting.Label;
                    setting.Section = mine.Section != null ? lang.Text(mine.Section) : setting.Section;
                    if (mine.Hidden.HasValue) setting.Hidden = mine.Hidden.Value;
                    if (mine.Order.HasValue) setting.Order = mine.Order.Value;
                    // "" is a line of its own: a row nobody else names.
                    if (mine.Row != null) setting.Row = mine.Row.Length == 0 ? "~" + setting.Key : mine.Row;
                }
                // A section shown only on a condition: its fields carry it, the section they sit in after the user's
                // layout, by the declared name or its translation.
                string sectionName = mine?.Section != null
                    ? spec.SectionConditions.Keys.FirstOrDefault(k => k == mine.Section || lang.Text(k) == mine.Section) ?? mine.Section
                    : declaredSection;
                setting.SectionShowWhen = sectionName != null && spec.SectionConditions.TryGetValue(sectionName, out var sectionCondition) ? sectionCondition : null;
                settings.Add(setting);
            }
            Shows(spec, settings);
            spec.ResolvedSettings.Clear();
            spec.ResolvedSettings.AddRange(settings);
            Check(spec, settings, declaredSections, asDeclared);
            CheckCooldowns(spec, schema, settings);
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

            // 4. Templates on the messages page become the picker's entries. Their groups are in the window's language,
            // so the cooldown group is looked for in that language too.
            spec.Senders.CooldownGroupShown = lang.Translates ? lang.Text(spec.Senders.CooldownGroup) : null;
            spec.Messages.Clear();
            foreach (var setting in settings.Where(s => s.Kind == "template" && s.Page == "messages" && !s.Hidden).OrderBy(s => s.Order))
                spec.Messages.Add(new MessageDef(setting.Group ?? lang.Page("messages") ?? "Messages", setting.Key, setting.Label, setting.Help ?? "", (setting.Placeholders ?? new List<string>()).ToArray()));
            if (spec.SampleValues == null)
                spec.SampleValues = ctx => SampleValues(ctx, settings);
            // Set again on every resolve, so a language switch reaches the placeholder chips' tooltips too.
            if (spec.TokenHelp == null || ReferenceEquals(spec.TokenHelp, spec.TokenHelpFromConventions))
                spec.TokenHelp = spec.TokenHelpFromConventions = (key, token) => lang.Placeholder(token);

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
                        // The Messages page builds the sender switches and the Log page its switch whatever a page
                        // claims: a second field for the key would have the last word on save. They stay there.
                        if (senderKeys.Contains(setting.Key) || setting.Key == spec.Log.WriteLogKey)
                        {
                            if (pattern == setting.Key)
                                spec.Problems.Add("Page '" + page.Id + "' names '" + pattern + "', which the " + (senderKeys.Contains(setting.Key) ? "Messages" : "Log") + " page shows; it stays there.");
                            continue;
                        }
                        // The user's layout has the last word: a field the user moved to another page stays there.
                        string moved = layout?.Field(setting.Key)?.Page;
                        if (moved != null && moved != page.Id)
                            continue;
                        claimed.Add(setting.Key);
                        // Where it sits now, for the customise mode's menu and everything else that asks.
                        setting.Page = page.Id;
                        mine.Add(setting);
                    }
                }
                pageFields[page.Id] = mine;
            }

            // A template a page claimed by its key is that page's field, not also an entry of the Messages picker.
            spec.Messages.RemoveAll(m => claimed.Contains(m.Key) && settings.Any(s => s.Key == m.Key && s.Page != "messages"));

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
                int trailing = Array.IndexOf(TrailingPages, p.Id);
                if (trailing >= 0) return 10000 + trailing;
                // A page of the game's own at the foot of the rail goes above Help, Log and About.
                if (p.Bottom) return 9000 + Math.Max(0, spec.SpecPages.FindIndex(x => x.Id == p.Id));
                if (explicitIds.Contains(p.Id)) return spec.SpecPages.FindIndex(x => x.Id == p.Id);
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
                    ?? (lang.Translates ? lang.OwnPage(page.Id) ?? lang.TextOrNull(page.Title) ?? lang.Page(page.Id) ?? page.Title : page.Title ?? lang.Page(page.Id))
                    ?? Conventions.Humanize(page.Id);
                string icon = userPage?.Icon != null ? IconGlyph(userPage.Icon) : page.Icon != null ? IconGlyph(page.Icon) : conventions.Pages.TryGetValue(page.Id, out var c) ? IconGlyph(c.Icon) : "•";
                if (page.Icon != null && UnknownIcon(page.Icon))
                    spec.Problems.Add("The page '" + page.Id + "' has the icon '" + page.Icon + "', which the framework does not know; it shows a dot. An emoji works, or one of gear, dice, coins, clock, shield, chat, trophy, monitor, list, book, info, cards, chart, gift.");
                // Help, Log and About sit in their own group at the bottom of the rail, apart from the settings pages.
                bool bottom = page.Bottom || Array.IndexOf(TrailingPages, page.Id) >= 0;
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
                if (part.Op != null && part.Key.StartsWith("!", StringComparison.Ordinal))
                {
                    // "!mode=free" reads as a key "!mode", which no field has, so it would always hold.
                    string bare = part.Key.TrimStart('!').Trim();
                    spec.Problems.Add(owner + " " + what + " '" + part.Key + part.Op + string.Join("|", part.Values) + "': a '!' goes only in front of a key on its own; write '"
                        + bare + (part.Op == "=" ? "!=" : part.Op == "!=" ? "=" : part.Op) + string.Join("|", part.Values) + "'" + (part.Op == "=" || part.Op == "!=" ? "" : " the other way round") + ".");
                    continue;
                }
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
                // <, <=, >, >= compare numbers: against text, or on a field that holds none, they never hold.
                if (part.Op != null && part.Op != "=" && part.Op != "!=")
                {
                    string wanted = part.Values.FirstOrDefault() ?? "";
                    if (!FieldBinding.TryParseInvariantDouble(wanted, out _))
                        spec.Problems.Add(owner + " " + what + " '" + key + part.Op + wanted + "', but '" + wanted + "' is no number; such a comparison never holds.");
                    else if (!HoldsNumber(gate))
                        spec.Problems.Add(owner + " " + what + " '" + key + part.Op + wanted + "', but '" + key + "' holds no number; such a comparison never holds.");
                }
            }
        }

        /// <summary>Whether a setting holds a number a condition can compare with &lt; or &gt;: a number kind, a slider on a
        /// scale, or options whose values are all numbers.</summary>
        private static bool HoldsNumber(Setting s)
        {
            switch (s.Kind)
            {
                case "int": case "long": case "double": case "duration": case "percent":
                    return true;
                case "slider": case "options":
                    return s.Options == null || s.Options.Count == 0 ? s.Kind == "slider" : s.Options.All(o => FieldBinding.TryParseInvariantDouble(o.Value, out _));
                default:
                    return false;
            }
        }

        /// <summary>Kinds that hold the same sort of value: a whole number, a decimal, text, a switch, a list, a colour.</summary>
        private static string KindFamily(string kind)
        {
            switch (kind)
            {
                case "int": case "long": case "duration": return "whole";
                case "double": case "percent": return "decimal";
                case "slider": return "slider";
                case "bool": return "bool";
                case "list": case "namelist": case "grouplist": case "table": return "list";
                case "colour": case "color": return "colour";
                default: return "text";   // string, options, text, template, path, emote, note, action
            }
        }

        /// <summary>What a host's author got wrong, in words: an unknown kind, a requirement on a key that is not
        /// there, a page or pill naming a setting that does not exist.</summary>
        /// <summary>The cooldowns the window declares read settings: a key that is none would set nothing, and only the
        /// settings action's log line on every save would say so.</summary>
        private static void CheckCooldowns(WindowSpec spec, Schema schema, List<Setting> settings)
        {
            if (!(schema?.Window?["cooldowns"] is Newtonsoft.Json.Linq.JArray list)) return;
            var keys = new HashSet<string>(settings.Select(s => s.Key), StringComparer.Ordinal);
            foreach (var item in list.OfType<Newtonsoft.Json.Linq.JObject>())
                foreach (var part in new[] { "global", "user" })
                {
                    string key = item[part]?.Type == Newtonsoft.Json.Linq.JTokenType.String ? (string)item[part] : null;
                    if (!string.IsNullOrWhiteSpace(key) && !keys.Contains(key))
                        spec.Problems.Add("The cooldown of '" + (string)item["command"] + "' reads '" + key + "', which is not a setting: declare it (S(\"" + key
                            + "\", 0)) or put null in its place.");
                }
        }

        private static void Check(WindowSpec spec, List<Setting> settings, ISet<string> declaredSections, List<(string Page, int Order, string Section, bool Hidden)> asDeclared)
        {
            var keys = new HashSet<string>(settings.Select(s => s.Key), StringComparer.Ordinal);
            var byKey = settings.GroupBy(s => s.Key, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
            foreach (var section in spec.SectionConditions)
            {
                CheckCondition(spec, byKey, "The section '" + section.Key + "'", "is shown when", section.Value);
                if (!declaredSections.Contains(section.Key))
                    spec.Problems.Add("The section '" + section.Key + "' has a condition, but no setting is in a section of that name.");
            }
            // A section whose fields are not one after the other gets its heading twice: said, so the game can order
            // them. As the game declared them: where the user's layout moved a field is not the game author's to fix.
            foreach (var page in asDeclared.Where(s => !s.Hidden).GroupBy(s => s.Page ?? "game"))
            {
                var seen = new HashSet<string>(StringComparer.Ordinal);
                var told = new HashSet<string>(StringComparer.Ordinal);
                string previous = null;
                foreach (var s in page.OrderBy(x => x.Order))
                {
                    if (s.Section == null || s.Section == previous) { previous = s.Section ?? previous; continue; }
                    if (!seen.Add(s.Section) && told.Add(s.Section))
                        spec.Problems.Add("The section '" + s.Section + "' on the page '" + page.Key + "' is split by another section, so its heading shows twice; declare its settings one after the other.");
                    previous = s.Section;
                }
            }
            foreach (var s in settings)
            {
                if (s.Kind != null && !Kinds.Contains(s.Kind))
                    spec.Problems.Add("'" + s.Key + "' has the kind '" + s.Kind + "', which the framework does not know; it is shown as text.");
                if ((s.Kind == "options" || (s.Kind == "slider" && !(s.Min.HasValue && s.Max.HasValue))) && (s.Options == null || s.Options.Count == 0))
                    spec.Problems.Add("'" + s.Key + "' is " + (s.Kind == "slider" ? "a slider" : "an options field") + " without options.");
                if (s.Kind == "list" && (s.Columns == null || s.Columns.Count == 0))
                    spec.Problems.Add("'" + s.Key + "' is a list without columns.");
                if (!string.IsNullOrWhiteSpace(s.Requires))
                    CheckCondition(spec, byKey, "'" + s.Key + "'", "requires", s.Requires);
                if (!string.IsNullOrWhiteSpace(s.ShowWhen))
                    CheckCondition(spec, byKey, "'" + s.Key + "'", "is shown when", s.ShowWhen);
                if (s.Min.HasValue && s.Max.HasValue && s.Min > s.Max)
                    spec.Problems.Add("'" + s.Key + "' has a minimum above its maximum.");
                foreach (var alias in s.Aliases)
                    if (alias != s.Key && keys.Contains(alias))
                        spec.Problems.Add("'" + s.Key + "' has the old key '" + alias + "', which is a setting of its own; that old key is not moved.");
            }
            if (!string.IsNullOrEmpty(spec.Log.WriteLogKey) && !keys.Contains(spec.Log.WriteLogKey))
                spec.Problems.Add("The log page's switch '" + spec.Log.WriteLogKey + "' is not a setting.");
            foreach (var key in spec.HeaderPillKeys)
                if (!keys.Contains(key))
                    spec.Problems.Add("The header pill '" + key + "' is not a setting.");
            foreach (var key in spec.Overrides.Keys)
                if (!keys.Contains(key))
                    spec.Problems.Add("The spec overrides '" + key + "', which is not a setting.");
            // The tour: a stop names a setting, a page or a part of the window; one naming nothing known is skipped.
            var pageIds = new HashSet<string>(spec.SpecPages.Select(p => p.Id).Concat(settings.Select(s => s.Page)).Where(id => id != null), StringComparer.Ordinal)
                { "messages", "log", "help", "about" };
            foreach (var stop in spec.Tour)
            {
                if (stop.Target == null) continue;
                if (stop.Target.StartsWith("page:", StringComparison.Ordinal))
                {
                    if (!pageIds.Contains(stop.Target.Substring(5)))
                        spec.Problems.Add("The tour points at the page '" + stop.Target.Substring(5) + "', which the window does not have; that stop is skipped.");
                }
                else if (!keys.Contains(stop.Target) && Array.IndexOf(TourStop.Parts, stop.Target) < 0)
                    spec.Problems.Add("The tour points at '" + stop.Target + "', which is neither a setting nor a part of the window (" + string.Join(", ", TourStop.Parts) + "); that stop is skipped.");
            }
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
                case "coins": return "💰";   // 🪙 (Unicode 13) is missing from the emoji font WPF draws with
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
                case "gift": return "🎁";
                case "": return "•";
                // A word the framework does not know shows a dot, not the word; anything else is taken as an emoji.
                default: return IsIconName(name) ? "•" : name;
            }
        }

        /// <summary>A name rather than an emoji: letters, digits, '-' and '_' only.</summary>
        private static bool IsIconName(string name) => name.Length > 1 && name.All(c => c < 128 && (char.IsLetterOrDigit(c) || c == '-' || c == '_'));

        /// <summary>An icon that is a word the framework does not know (it shows as a dot).</summary>
        public static bool UnknownIcon(string name) => !string.IsNullOrEmpty(name) && IsIconName(name) && IconGlyph(name) == "•";

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
