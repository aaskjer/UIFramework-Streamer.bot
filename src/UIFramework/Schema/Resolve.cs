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
            spec.Problems.AddRange(spec.ReadProblems);
            CheckRequires(spec);

            // 1. Complete every setting.
            var settings = new List<Setting>();
            var declaredSections = new HashSet<string>(StringComparer.Ordinal);
            // Where the game put each field, before the user's layout moves it: what the check of split sections reads.
            var asDeclared = new List<(string Key, string Page, int Order, string Section, bool Hidden)>();
            // What the game hides (`hidden`), before the user's layout: the same with every resolve.
            var hiddenByGame = new HashSet<string>(StringComparer.Ordinal);
            // Each field's order as the project declares it, and the fields the user's layout gives an order of its own:
            // what places a field the user never placed on a page the user sorted.
            var declaredOrder = new Dictionary<string, int>(StringComparer.Ordinal);
            var placedByUser = new HashSet<string>(StringComparer.Ordinal);
            foreach (var declared in schema.Settings)
            {
                // A known key declared as another kind of value is most likely the game's own idea under a taken name:
                // it would still get the conventions' limits, old keys and translations.
                if (declared.Kind != null && conventions.Keys.TryGetValue(declared.Key, out var knownKey) && knownKey.Kind != null
                    && KindFamily(declared.Kind) != KindFamily(knownKey.Kind))
                    spec.Problems.Add("'" + declared.Key + "' is a known key of the conventions (" + (Conventions.Default.Lang.Key(declared.Key).Label ?? declared.Key) +
                        ", " + knownKey.Kind + "), declared as '" + declared.Kind + "': it still takes the conventions' limits, old keys and translations. A key of the game's own fits another meaning.");
                var setting = conventions.Complete(declared);
                // A value a Set Argument of the project's action sets, or one the project locks: shown, not edited.
                // A button, a note and a table hold no value to lock.
                if (setting.Kind != "action" && setting.Kind != "note" && setting.Kind != "table")
                {
                    if (schema.Arguments.TryGetValue(setting.Key, out var argument)) { setting.Argument = argument; setting.ReadOnly = true; }
                    else if (schema.LockedFields.TryGetValue(setting.Key, out var why)) { setting.LockReason = why ?? ""; setting.ReadOnly = true; }
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
                    string declaredLabel = declared.Label, declaredHelp = declared.Help;
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
                asDeclared.Add((setting.Key, setting.Page, setting.Order, setting.Section, setting.Hidden));
                declaredOrder[setting.Key] = setting.Order;
                if (setting.Hidden) hiddenByGame.Add(setting.Key);
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
                    if (mine.Order.HasValue)
                    {
                        setting.Order = mine.Order.Value;
                        placedByUser.Add(setting.Key);
                    }
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
            CheckGlobals(spec, settings);
            Shows(spec, settings);
            spec.ResolvedSettings.Clear();
            spec.ResolvedSettings.AddRange(settings);
            Check(spec, settings, declaredSections);
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

            // 3. Sender switches exist only when the schema declares them and the game does not hide them: a hidden one,
            // like the Log page's switch, is a setting kept in the file without a field. The Messages and the Log page
            // drew them hidden or not.
            var keys = new HashSet<string>(settings.Select(s => s.Key), StringComparer.Ordinal);
            string Present(string key) => key != null && keys.Contains(key) && !hiddenByGame.Contains(key) ? key : null;
            spec.Senders.UseBot = Present(spec.Senders.UseBot);
            spec.Senders.SendAction = Present(spec.Senders.SendAction);
            spec.Senders.CooldownUseBot = Present(spec.Senders.CooldownUseBot);
            spec.Senders.CooldownSendAction = Present(spec.Senders.CooldownSendAction);
            spec.Senders.NumberSeparator = Present(spec.Senders.NumberSeparator);
            if (spec.Log.WriteLogKey != null && hiddenByGame.Contains(spec.Log.WriteLogKey))
                spec.Log.WriteLogKey = null;
            var senderKeys = new HashSet<string>(new[] { spec.Senders.UseBot, spec.Senders.SendAction, spec.Senders.CooldownUseBot, spec.Senders.CooldownSendAction, spec.Senders.NumberSeparator }.Where(k => k != null), StringComparer.Ordinal);

            // 4. Templates on the messages page become the picker's entries. Their groups are in the window's language,
            // so the cooldown group is looked for in that language too.
            spec.Senders.CooldownGroupShown = lang.Translates ? lang.Text(spec.Senders.CooldownGroup) : null;
            spec.Messages.Clear();
            foreach (var setting in settings.Where(s => s.Kind == "template" && s.Page == "messages" && !s.Hidden).OrderBy(s => s.Order))
                spec.Messages.Add(new MessageDef(setting.Group ?? lang.Page("messages") ?? "Messages", setting.Key, setting.Label, setting.Help ?? "", (setting.Placeholders ?? new List<string>()).ToArray())
                {
                    Percent = TokenStyle.IsPercent(setting.PlaceholderStyle ?? spec.PlaceholderStyle),
                    LockNote = setting.Argument != null ? Text.T("field.setArgument.why", setting.Argument)
                        : setting.LockReason == null ? null : setting.LockReason.Trim().Length > 0 ? setting.LockReason : Text.T("field.locked")
                });
            if (spec.SampleValues == null)
                spec.SampleValues = ctx => SampleValues(ctx, settings);
            // Set again on every resolve, so a language switch reaches the placeholder chips' tooltips too.
            if (spec.TokenHelp == null || ReferenceEquals(spec.TokenHelp, spec.TokenHelpFromConventions))
                spec.TokenHelp = spec.TokenHelpFromConventions = (key, token) => lang.Placeholder(token);

            // 5. Pages: the spec's order first, then whatever the settings still need, standard pages last.
            var claimed = new HashSet<string>(StringComparer.Ordinal);
            var pageFields = new Dictionary<string, List<Setting>>(StringComparer.Ordinal);
            var pageOrder = new List<SpecPage>();
            // A page's claim on a sender switch waits until the Messages page's entries are known, and with them
            // whether it draws the switch.
            var senderClaims = new List<(SpecPage Page, string Pattern, Setting Setting)>();
            // The page that claims each field before the user's layout has its say: where the game puts it.
            var claimedByWindow = new Dictionary<string, string>(StringComparer.Ordinal);

            void Claim(SpecPage page, Setting setting, List<Setting> mine)
            {
                if (!claimedByWindow.ContainsKey(setting.Key))
                    claimedByWindow[setting.Key] = page.Id;
                // The user's layout has the last word: a field the user moved to another page stays there.
                string moved = layout?.Field(setting.Key)?.Page;
                if (moved != null && moved != page.Id)
                    return;
                claimed.Add(setting.Key);
                // Where it sits now, for the customise mode's menu and everything else that asks.
                setting.Page = page.Id;
                mine.Add(setting);
            }

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
                        if (senderKeys.Contains(setting.Key))
                        {
                            senderClaims.Add((page, pattern, setting));
                            continue;
                        }
                        // The Log page builds its switch whatever a page claims: a second field for the key would have
                        // the last word on save. It stays there.
                        if (setting.Key == spec.Log.WriteLogKey)
                        {
                            if (pattern == setting.Key)
                                spec.Problems.Add("Page '" + page.Id + "' names '" + pattern + "', which the Log page shows; it stays there.");
                            continue;
                        }
                        Claim(page, setting, mine);
                    }
                }
                pageFields[page.Id] = mine;
            }

            // A template a page claimed by its key is that page's field, not also an entry of the Messages picker.
            spec.Messages.RemoveAll(m => claimed.Contains(m.Key) && settings.Any(s => s.Key == m.Key && s.Page != "messages"));

            // The sender switches the Messages page draws, as StandardPages.BuildMessages does: the game's while it has
            // entries, the cooldown pair while one of them is in the cooldown group and the bot switch for it is
            // declared. One it does not draw is a field like any other, on its page: a setting the game reads always has
            // one. Those it draws stay there whatever a page claims, since a second field would have the last word on save.
            var drawn = new HashSet<string>(StringComparer.Ordinal);
            if (spec.Messages.Count > 0)
            {
                drawn.UnionWith(new[] { spec.Senders.UseBot, spec.Senders.SendAction, spec.Senders.NumberSeparator }.Where(k => k != null));
                if (spec.Senders.CooldownUseBot != null && spec.Messages.Any(m => spec.Senders.IsCooldownGroup(m.Group)))
                    drawn.UnionWith(new[] { spec.Senders.CooldownUseBot, spec.Senders.CooldownSendAction }.Where(k => k != null));
            }
            foreach (var (page, pattern, setting) in senderClaims)
            {
                if (drawn.Contains(setting.Key))
                {
                    if (pattern == setting.Key)
                        spec.Problems.Add("Page '" + page.Id + "' names '" + pattern + "', which the Messages page shows; it stays there.");
                }
                else if (!claimed.Contains(setting.Key))
                    Claim(page, setting, pageFields[page.Id]);
            }
            CheckSplitSections(spec, asDeclared, claimedByWindow);

            foreach (var setting in settings.OrderBy(s => s.Order))
            {
                if (claimed.Contains(setting.Key) || (setting.Hidden && !customizing) || drawn.Contains(setting.Key))
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
            // A field the user never placed on a page the user sorted: by the page the field ends up on, hidden ones
            // included, as the customise mode counts a page's fields; not by what the Messages and Log pages draw.
            foreach (var page in settings.Where(s => !drawn.Contains(s.Key) && s.Key != spec.Log.WriteLogKey && !(s.Kind == "template" && s.Page == "messages"))
                .GroupBy(s => s.Page))
                PlaceUnsorted(page.ToList(), placedByUser, declaredOrder);

            // Standard pages the spec did not name: messages when there are templates, help when there are help
            // sections, custom triggers or methods to list, about always.
            if (spec.Messages.Count > 0 && pageOrder.All(p => p.Id != "messages"))
                pageOrder.Add(new SpecPage { Id = "messages" });
            if ((spec.Help.Count > 0 || spec.Triggers.Count > 0 || spec.Methods.Count > 0) && pageOrder.All(p => p.Id != "help"))
                pageOrder.Add(new SpecPage { Id = "help" });
            if (pageOrder.All(p => p.Id != "about"))
                pageOrder.Add(new SpecPage { Id = "about", Bottom = true });
            // The Log page comes only where the window names it: a log declared with its folder and files (what the
            // settings action reads it by) but without the page has none, and its switch no field.
            if (!string.IsNullOrEmpty(spec.Log.Folder) && !string.IsNullOrEmpty(spec.Log.Files) && pageOrder.All(p => p.Id != "log"))
                spec.Problems.Add("The window declares a log, but its pages do not name 'log': there is no Log page"
                    + (string.IsNullOrEmpty(spec.Log.WriteLogKey) ? "" : ", and '" + spec.Log.WriteLogKey + "' has no switch") + ". Add \"log\" to the pages.");

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

            // The groups of pages in the rail: a group sits where the first of its pages does, with all its pages under
            // its heading; a page in no group stays where the order above puts it.
            var groupOf = new Dictionary<string, string>(StringComparer.Ordinal);
            var declaredIn = new Dictionary<string, string>(StringComparer.Ordinal);
            pageOrder = Grouped(spec, pageOrder, layout, lang, UserOrder, groupOf, declaredIn);

            spec.Pages.Clear();
            foreach (var page in pageOrder)
            {
                var userPage = layout?.Page(page.Id);
                bool hidden = userPage?.Hidden == true && page.Id != "about";
                if (hidden && !customizing)
                    continue;
                // The conventions' title for the id where it is a translation: a language without page titles of its own
                // has the English ones, which never replace what the game titled the page itself (as with a key's label).
                string idTitle = lang.Page(page.Id);
                if (page.Title != null && idTitle == Conventions.Default.Lang.Page(page.Id))
                    idTitle = null;
                string title = userPage?.Title
                    ?? (lang.Translates ? lang.OwnPage(page.Id) ?? lang.TextOrNull(page.Title) ?? idTitle ?? page.Title : page.Title ?? lang.Page(page.Id))
                    ?? Conventions.Humanize(page.Id);
                string icon = userPage?.Icon != null ? IconGlyph(userPage.Icon) : page.Icon != null ? IconGlyph(page.Icon) : conventions.Pages.TryGetValue(page.Id, out var c) ? IconGlyph(c.Icon) : "•";
                if (page.Icon != null && UnknownIcon(page.Icon))
                    spec.Problems.Add("The page '" + page.Id + "' has the icon '" + page.Icon + "', which the framework does not know; it shows a dot. An emoji works, or one of gear, dice, coins, clock, shield, chat, trophy, monitor, list, book, info, cards, chart, gift.");
                // Help, Log and About sit in their own group at the bottom of the rail, apart from the settings pages.
                bool bottom = page.Bottom || Array.IndexOf(TrailingPages, page.Id) >= 0;
                var fields = pageFields.TryGetValue(page.Id, out var list) ? list : new List<Setting>();

                // A page the window names by its id alone that no setting is on and that is no standard page: an empty
                // page in the rail, and most likely a slip of the pen ("mesages").
                if (page.Title == null && page.Icon == null && page.Fields.Count == 0 && fields.Count == 0 && !page.Custom
                    && explicitIds.Contains(page.Id) && page.Id != "messages" && Array.IndexOf(TrailingPages, page.Id) < 0
                    && !settings.Any(s => s.Page == page.Id))
                    spec.Problems.Add("The window names the page '" + page.Id + "', which has no title and no setting on it; it shows empty. The standard pages are messages, log, help and about.");

                Func<PageContext, System.Windows.UIElement> build;
                if (page.Custom && !spec.HostBuildsPages)
                    spec.Problems.Add("The page '" + page.Id + "' is marked custom, but a settings action builds no pages of its own: the page is left out"
                        + (fields.Count > 0 ? ", and with it " + string.Join(", ", fields.Select(f => "'" + f.Key + "'")) : "") + ".");
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
                spec.Pages.Add(new PageSpec(page.Id, title, icon, build, bottom)
                {
                    Hidden = hidden,
                    Group = groupOf.TryGetValue(page.Id, out var inGroup) ? inGroup : null,
                    DeclaredGroup = declaredIn.TryGetValue(page.Id, out var declaredGroup) ? declaredGroup : null
                });
            }
        }

        /// <summary>The pages in the rail's order with the window's groups (<see cref="WindowSpec.Groups"/>): each group
        /// where the first of its pages sits in <paramref name="pageOrder"/>, its pages together in the order the group
        /// names them (the user's order first, where the customise mode wrote one); a page in no group where it was; the
        /// pages at the foot last, in no group. The user's layout moves a page into another group or out of every group
        /// (<c>pages.&lt;id&gt;.group</c>) and renames a heading (<c>groups.&lt;id&gt;.title</c>). What a declaration gets
        /// wrong is named among the problems. Fills <see cref="WindowSpec.RailGroups"/>, and for every page in a group
        /// <paramref name="groupOf"/> (after the layout) and <paramref name="declaredIn"/> (as declared).</summary>
        private static List<SpecPage> Grouped(WindowSpec spec, List<SpecPage> pageOrder, LayoutFile layout, Lang lang, Func<SpecPage, int?> userOrder,
            Dictionary<string, string> groupOf, Dictionary<string, string> declaredIn)
        {
            spec.RailGroups.Clear();
            if (spec.Groups.Count == 0) return pageOrder;
            bool AtFoot(SpecPage p) => p.Bottom || Array.IndexOf(TrailingPages, p.Id) >= 0;
            var byId = new Dictionary<string, SpecPage>(StringComparer.Ordinal);
            foreach (var page in pageOrder)
                if (page.Id != null && !byId.ContainsKey(page.Id)) byId[page.Id] = page;

            // The pages each group names, in its order, as far as they can be its pages.
            var named = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            foreach (var group in spec.Groups)
            {
                string id = (group.Id ?? "").Trim();
                if (id.Length == 0)
                {
                    spec.Problems.Add("A group of pages" + (string.IsNullOrWhiteSpace(group.Title) ? "" : " ('" + group.Title.Trim() + "')")
                        + " has no id; it is left out, and its pages stay in no group. Write Group(\"id\", \"Title\", pages…).");
                    continue;
                }
                if (named.ContainsKey(id))
                {
                    spec.Problems.Add("Two groups of pages have the id '" + id + "'; the second is left out, and its pages stay where they are.");
                    continue;
                }
                if (string.IsNullOrWhiteSpace(group.Title))
                    spec.Problems.Add("The group '" + id + "' has no title; its heading shows '" + Conventions.Humanize(id) + "'.");
                var pages = named[id] = new List<string>();
                foreach (var pageId in group.Pages)
                {
                    if (pageId == null || !byId.TryGetValue(pageId, out var page))
                        spec.Problems.Add("The group '" + id + "' names the page '" + pageId + "', which the window does not have.");
                    else if (AtFoot(page))
                        spec.Problems.Add("The group '" + id + "' names the page '" + pageId + "', which sits at the foot of the rail; it stays there, in no group.");
                    else if (declaredIn.TryGetValue(pageId, out var other))
                    {
                        if (other != id)
                            spec.Problems.Add("The groups '" + other + "' and '" + id + "' both name the page '" + pageId + "'; it stays in '" + other + "'.");
                    }
                    else
                    {
                        declaredIn[pageId] = id;
                        pages.Add(pageId);
                    }
                }
                // A declared text, like a section's name: in another language through the project's texts. The user's
                // name for the heading has the last word.
                string title = string.IsNullOrWhiteSpace(group.Title) ? Conventions.Humanize(id) : lang.Translates ? lang.Text(group.Title) : group.Title;
                spec.RailGroups.Add(new PageGroup { Id = id, Title = layout?.Group(id)?.Title ?? title });
            }

            var top = pageOrder.Where(p => !AtFoot(p)).ToList();
            foreach (var page in top)
            {
                // "" is out of every group; a group the window no longer has is as if the user had said nothing.
                string mine = layout?.Page(page.Id)?.Group;
                if (mine != null && mine.Length > 0 && !named.ContainsKey(mine)) mine = null;
                string group = mine ?? (declaredIn.TryGetValue(page.Id, out var declared) ? declared : null);
                if (!string.IsNullOrEmpty(group)) groupOf[page.Id] = group;
            }
            int NamedAt(SpecPage p)
            {
                int at = named[groupOf[p.Id]].IndexOf(p.Id);
                return at < 0 ? int.MaxValue : at;
            }
            var members = top.Where(p => groupOf.ContainsKey(p.Id)).GroupBy(p => groupOf[p.Id], StringComparer.Ordinal).ToDictionary(g => g.Key,
                g => g.OrderBy(p => userOrder(p).HasValue ? 0 : 1).ThenBy(p => userOrder(p) ?? 0).ThenBy(NamedAt).ToList(), StringComparer.Ordinal);

            var rail = new List<SpecPage>();
            var placed = new HashSet<string>(StringComparer.Ordinal);
            foreach (var page in top)
            {
                if (!groupOf.TryGetValue(page.Id, out var group)) rail.Add(page);
                else if (placed.Add(group)) rail.AddRange(members[group]);
            }
            rail.AddRange(pageOrder.Where(AtFoot));
            foreach (var group in spec.RailGroups)
                if (members.TryGetValue(group.Id, out var its))
                    group.Pages.AddRange(its.Select(p => p.Id));
            return rail;
        }

        private static readonly HashSet<string> Kinds = new HashSet<string>(StringComparer.Ordinal)
        {
            "bool", "int", "long", "duration", "double", "percent", "options", "namelist", "grouplist", "colour", "color",
            "path", "emote", "slider", "note", "text", "string", "template", "action", "table", "list", "multi", "time"
        };

        private static readonly string[] PlatformNames = { "twitch", "youtube", "kick" };

        // "a slider", "an options field": the kind a declaration's mistake is named with.
        private static string Article(string kind)
        {
            kind = string.IsNullOrEmpty(kind) ? "text" : kind;
            return ("aeiou".IndexOf(kind[0]) >= 0 ? "an " : "a ") + kind + " field";
        }

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
            // "hide:" in front of a requirement hid the field instead of greying it, until showWhen did the same.
            if (text.StartsWith("hide:", StringComparison.OrdinalIgnoreCase))
            {
                spec.Problems.Add(owner + " " + what + " '" + text + "': 'hide:' in front of a requirement is no longer read, and the field always shows. Hide it with showWhen: \"" + text.Substring(5).Trim() + "\".");
                return;
            }
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
                // A dropdown the project names the options of (optionsFrom) may hold what it declares no option for.
                if ((part.Op == "=" || part.Op == "!=") && gate.Options != null && gate.Options.Count > 0 && (gate.Kind == "slider" || gate.Kind == "multi" || (gate.Kind == "options" && gate.OptionsFrom == null)))
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
                    if (s.Kind == "options" && s.OptionsFrom != null) return true;   // the project's options: not known here
                    return s.Options == null || s.Options.Count == 0 ? s.Kind == "slider" : s.Options.All(o => FieldBinding.TryParseInvariantDouble(o.Value, out _));
                default:
                    return false;
            }
        }

        /// <summary>A number as a declaration writes it, whatever the system's decimal mark: 0.5.</summary>
        private static string Number(double value) => value.ToString("R", System.Globalization.CultureInfo.InvariantCulture);

        /// <summary>Kinds that hold the same sort of value: a whole number, a decimal, text, a switch, a list, a colour.</summary>
        private static string KindFamily(string kind)
        {
            switch (kind)
            {
                case "int": case "long": case "duration": return "whole";
                case "double": case "percent": return "decimal";
                case "slider": return "slider";
                case "bool": return "bool";
                case "list": case "namelist": case "grouplist": case "multi": case "table": return "list";
                case "colour": case "color": return "colour";
                default: return "text";   // string, options, text, template, path, emote, time, note, action
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

        private static void Check(WindowSpec spec, List<Setting> settings, ISet<string> declaredSections)
        {
            var keys = new HashSet<string>(settings.Select(s => s.Key), StringComparer.Ordinal);
            var byKey = settings.GroupBy(s => s.Key, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
            foreach (var section in spec.SectionConditions)
            {
                CheckCondition(spec, byKey, "The section '" + section.Key + "'", "is shown when", section.Value);
                if (!declaredSections.Contains(section.Key))
                    spec.Problems.Add("The section '" + section.Key + "' has a condition, but no setting is in a section of that name.");
            }
            foreach (var s in settings)
            {
                if (s.Kind != null && !Kinds.Contains(s.Kind))
                    spec.Problems.Add("'" + s.Key + "' has the kind '" + s.Kind + "', which the framework does not know; it is shown as text. The kinds are " + string.Join(", ", Kinds.Where(k => k != "color").OrderBy(k => k, StringComparer.Ordinal)) + ".");
                // What only one kind of field has does nothing on another, and nobody is told: said here.
                if (s.Kind != "action")
                {
                    if (s.Auto) spec.Problems.Add("'" + s.Key + "' has auto, which only a button has (kind action); it does nothing on " + Article(s.Kind) + ".");
                    else if (s.Panel) spec.Problems.Add("'" + s.Key + "' has panel, which only a button has (kind action); it does nothing on " + Article(s.Kind) + ".");
                    if (s.Confirm != null) spec.Problems.Add("'" + s.Key + "' has confirm, which only a button has (kind action); it does nothing on " + Article(s.Kind) + ".");
                    // With auto beside it, what is said of auto above says it: the kind is missing.
                    if (s.Every.HasValue && !s.Auto) spec.Problems.Add("'" + s.Key + "' has every, which only an auto action has (kind action with auto); it does nothing on " + Article(s.Kind) + ".");
                    if (s.SaveFirst) spec.Problems.Add("'" + s.Key + "' has saveFirst, which only a button has (kind action); it does nothing on " + Article(s.Kind) + ".");
                }
                else
                {
                    // An interval belongs to the panel that asks by itself, a save before the question to the button
                    // that is pressed.
                    if (s.Every.HasValue && !s.Auto)
                        spec.Problems.Add("'" + s.Key + "' has every, which only an auto action has; a button asks when it is pressed. Add auto for a panel that asks by itself.");
                    else if (s.Every.HasValue && s.Every.Value < SchemaPage.ShortestEvery)
                        spec.Problems.Add("'" + s.Key + "' asks every " + s.Every.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) + " seconds; the shortest is "
                            + SchemaPage.ShortestEvery + ", which it takes instead.");
                    if (s.SaveFirst && s.Auto)
                        spec.Problems.Add("'" + s.Key + "' has saveFirst, which only a button has; an auto action has none to press, and nothing is saved before it asks.");
                }
                if (s.TwitchOnly && s.Kind == "note")
                    spec.Problems.Add("'" + s.Key + "' has twitchOnly, which a note cannot show: it has no label to carry the mark.");
                if (s.Placeholders != null && s.Placeholders.Count > 0 && s.Kind != "template")
                    spec.Problems.Add("'" + s.Key + "' has placeholders, which only a message has (kind template); they do nothing on " + Article(s.Kind) + ".");
                if (s.Platform != null && Array.IndexOf(PlatformNames, s.Platform.Trim().ToLowerInvariant()) < 0)
                    spec.Problems.Add("'" + s.Key + "' names the platform '" + s.Platform + "'; it is one of " + string.Join(", ", PlatformNames) + ".");
                foreach (var column in s.Columns ?? new List<SettingColumn>())
                    if (column.Platform != null && Array.IndexOf(PlatformNames, column.Platform.Trim().ToLowerInvariant()) < 0)
                        spec.Problems.Add("The column '" + column.Key + "' of '" + s.Key + "' names the platform '" + column.Platform + "'; it is one of " + string.Join(", ", PlatformNames) + ".");
                if (s.PlaceholderStyle != null && s.Kind != "template")
                    spec.Problems.Add("'" + s.Key + "' has a placeholderStyle, which only a message has (kind template); it does nothing on " + Article(s.Kind) + ".");
                else if (!TokenStyle.Known(s.PlaceholderStyle))
                    spec.Problems.Add("'" + s.Key + "' has the placeholderStyle '" + s.PlaceholderStyle + "'; it is " + TokenStyle.Braces + " ({name}) or " + TokenStyle.Percent + " (%name%). Its placeholders are read as {name}.");
                // A placeholder in a message's own text that its list lacks gets no chip, no help and no sample in
                // the preview, and is easily one the game does not fill. Read in the message's style: {name}, or %name%.
                if (s.Kind == "template" && s.Placeholders != null && s.Placeholders.Count > 0 && s.Default?.Type == Newtonsoft.Json.Linq.JTokenType.String)
                {
                    bool percent = TokenStyle.IsPercent(s.PlaceholderStyle ?? spec.PlaceholderStyle);
                    foreach (var token in TokenStyle.In((string)s.Default, percent))
                        if (!s.Placeholders.Contains(token, StringComparer.OrdinalIgnoreCase))
                            spec.Problems.Add("'" + s.Key + "' uses " + TokenStyle.Write(token, percent) + " in its default text, which its placeholders do not list.");
                }
                // A dropdown that takes its options from the project (optionsFrom) needs none of its own.
                if (((s.Kind == "options" && s.OptionsFrom == null) || s.Kind == "multi" || (s.Kind == "slider" && !(s.Min.HasValue && s.Max.HasValue))) && (s.Options == null || s.Options.Count == 0))
                    spec.Problems.Add("'" + s.Key + "' is " + (s.Kind == "slider" ? "a slider" : s.Kind == "multi" ? "a multi field" : "an options field") + " without options"
                        + (s.Kind == "multi" ? "; it is shown as text." : "."));
                if (s.Kind == "multi")
                    CheckMulti(spec, s);
                if (s.Kind == "time")
                    CheckTime(spec, s);
                if (s.OptionsFrom != null)
                {
                    // What the project is asked again for is a field of the window; "groups" and the empty text name none.
                    string from = s.OptionsFrom.Trim();
                    if (s.Kind != "options")
                        spec.Problems.Add("'" + s.Key + "' has optionsFrom, which only a dropdown has (kind options); it does nothing on " + Article(s.Kind) + ".");
                    else if (from == s.Key)
                        spec.Problems.Add("'" + s.Key + "' names itself in optionsFrom: its own choice does not change its options, so the project is asked for them once. Name the field they depend on, or \"\" to ask once.");
                    else if (from == "groups" && keys.Contains(from))
                        spec.Problems.Add("'" + s.Key + "' takes its options from 'groups' (optionsFrom), which stands for Streamer.bot's groups, not for the setting 'groups': the project is not asked. A setting of another key can be named.");
                    else if (from.Length > 0 && from != "groups" && !keys.Contains(from))
                        spec.Problems.Add("'" + s.Key + "' takes its options from '" + from + "' (optionsFrom), which is not a setting: the project is asked for them once and never again. It is a setting's key, \"groups\" for Streamer.bot's groups, or \"\" to ask once.");
                }
                if (s.Kind == "list" && (s.Columns == null || s.Columns.Count == 0))
                    spec.Problems.Add("'" + s.Key + "' is a list without columns.");
                CheckProjectList(spec, byKey, s);
                if (!string.IsNullOrWhiteSpace(s.Requires))
                    CheckCondition(spec, byKey, "'" + s.Key + "'", "requires", s.Requires);
                if (!string.IsNullOrWhiteSpace(s.ShowWhen))
                    CheckCondition(spec, byKey, "'" + s.Key + "'", "is shown when", s.ShowWhen);
                if (s.Min.HasValue && s.Max.HasValue && s.Min > s.Max)
                    spec.Problems.Add("'" + s.Key + "' has a minimum above its maximum.");
                // A slider over a range holds what its default is (docs/Schema.md): a whole number keeps it on whole numbers,
                // so a place a step, minimum or maximum with a fraction gives it is saved rounded (0.5 as 0, 1.5 as 2), the
                // same as the place beside it.
                if (s.Kind == "slider" && (s.Options == null || s.Options.Count == 0) && s.Default?.Type == Newtonsoft.Json.Linq.JTokenType.Integer)
                {
                    bool Fraction(double? v) => v.HasValue && v.Value != Math.Floor(v.Value);
                    string limit = Fraction(s.Step) ? "step " + Number(s.Step.Value) : Fraction(s.Min) ? "minimum " + Number(s.Min.Value)
                        : Fraction(s.Max) ? "maximum " + Number(s.Max.Value) : null;
                    if (limit != null)
                        spec.Problems.Add("'" + s.Key + "' has the " + limit + ", but its default " + FieldBinding.Invariant(s.Default) + " is a whole number, so the slider"
                            + " holds whole numbers and saves a place between two of them rounded. Write the default as " + FieldBinding.Invariant(s.Default) + ".0 for a slider of decimals.");
                }
                foreach (var alias in s.Aliases)
                    if (alias != s.Key && keys.Contains(alias))
                        spec.Problems.Add("'" + s.Key + "' has the old key '" + alias + "', which is a setting of its own; that old key is not moved.");
            }
            if (!string.IsNullOrEmpty(spec.Log.WriteLogKey) && !keys.Contains(spec.Log.WriteLogKey))
                spec.Problems.Add("The log page's switch '" + spec.Log.WriteLogKey + "' is not a setting.");
            foreach (var key in spec.HeaderPillKeys)
                if (!keys.Contains(key))
                    spec.Problems.Add("The header pill '" + key + "' is not a setting.");
            if (!TokenStyle.Known(spec.PlaceholderStyle))
                spec.Problems.Add("The window has the placeholderStyle '" + spec.PlaceholderStyle + "'; it is " + TokenStyle.Braces + " ({name}) or " + TokenStyle.Percent + " (%name%). The placeholders are read as {name}.");
            // A field the project locks (locked: in UiHost.Handle) has to be one: a key that is none locks nothing.
            if (spec.Schema != null)
                foreach (var key in spec.Schema.LockedFields.Keys)
                    if (!keys.Contains(key))
                        spec.Problems.Add("The project locks '" + key + "' (locked: in UiHost.Handle, or \"lockedFields\" in the schema), which is not a setting.");
            // The Log page's declared colours: a style the framework does not have is not applied.
            foreach (var style in spec.Log.Styles)
                if (!LogPage.TryStyle(style.Value, out _))
                    spec.Problems.Add("The log colours '" + style.Key + "' as '" + style.Value + "'; a style is one of " + string.Join(", ", LogPage.StyleNames) + ".");
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
            // The old values the window takes over: an action is found by its id, and a mapping leads to a setting that
            // takes a value. An old argument the table does not know is no mistake; it is simply not taken.
            foreach (var source in spec.Adopt)
            {
                string id = (source.ActionId ?? "").Trim();
                if (id.Length == 0)
                {
                    spec.Problems.Add("The window takes over old values (UiWindow.Adopt, or \"adopt\" in the schema) from an action without an id; nothing is taken over from it.");
                    continue;
                }
                string owner = "The old values of the action '" + id + "'";
                if (!Guid.TryParse(id, out _))
                    spec.Problems.Add("The window takes over old values from the action '" + id + "', which is no action id: Streamer.bot's are GUIDs (36 characters with four dashes), and an action is not found by its name.");
                foreach (var declared in source.Map)
                {
                    if (!Adoption.TryMapping(declared, out bool text, out string old, out string key))
                    {
                        spec.Problems.Add(owner + " have the mapping '" + declared + "', which is neither \"old>new\" nor \"#subActionId>key\"; it is left out.");
                        continue;
                    }
                    string what = text ? " take the text of the sub-action '" + old + "' into '" + key + "'" : " map '" + old + "' to '" + key + "'";
                    if (!byKey.TryGetValue(key, out var target))
                        spec.Problems.Add(owner + what + ", which is not a setting; that value is not taken over.");
                    else if (!Adoption.TakesValue(target))
                        spec.Problems.Add(owner + what + ", " + Article(target.Kind) + ", which takes no value from an old action; that value is not taken over.");
                    else if (target.Transient || target.ReadOnly)
                        spec.Problems.Add(owner + what + ", which the window never saves (" + (target.ReadOnly ? "readOnly" : "transient") + "); that value is not taken over.");
                }
            }
        }

        /// <summary>A multi field stores its ticked values as one text, separated by commas, in the options' own spelling:
        /// an option that cannot be stored so, and a default that names what no option has or is written another way.</summary>
        private static void CheckMulti(WindowSpec spec, Setting s)
        {
            foreach (var option in s.Options ?? new List<SettingOption>())
                if (string.IsNullOrWhiteSpace(option.Value) || option.Value.Contains(","))
                    spec.Problems.Add("'" + s.Key + "' has the option '" + option.Value + "', which a multi field cannot store: its ticked values are kept as one text, separated by commas, so a value is neither empty nor holds a comma. That option is left out.");
            if (s.Default == null || s.Default.Type == JTokenType.Null) return;
            if (s.Default.Type != JTokenType.String)
            {
                spec.Problems.Add("'" + s.Key + "' is a multi field whose default is no text: it is the ticked values, separated by commas (\"a,b\"), or \"\" for none.");
                return;
            }
            var choices = Fields.MultiChoices(s.Options).Select(o => o.Value).ToArray();
            if (choices.Length == 0) return;   // without options it is shown as text, which is said already
            var values = FieldBinding.MultiValues(choices, (string)s.Default);
            var unknown = values.Where(value => !choices.Contains(value, StringComparer.OrdinalIgnoreCase)).ToList();
            foreach (var value in unknown)
                spec.Problems.Add("'" + s.Key + "' has '" + value + "' in its default, but it has no option '" + value + "'.");
            // The project reads the default as it is declared (UiSettings.String), the window writes the options' spelling.
            if (unknown.Count == 0 && string.Join(",", values) != (string)s.Default)
                spec.Problems.Add("'" + s.Key + "' has the default '" + (string)s.Default + "', which the window writes as '" + string.Join(",", values)
                    + "': the options' own spelling, in their order, no blanks. Declare it so, since the project reads the default as it is declared.");
        }

        /// <summary>A time field holds "HH:mm": limits do nothing on it, and its default is such a time or empty.</summary>
        private static void CheckTime(WindowSpec spec, Setting s)
        {
            if (s.Min.HasValue || s.Max.HasValue)
                spec.Problems.Add("'" + s.Key + "' has " + (s.Min.HasValue && s.Max.HasValue ? "a min and a max, which do" : s.Min.HasValue ? "a min, which does" : "a max, which does")
                    + " nothing on a time field: it takes any time of day, 00:00 to 23:59.");
            if (s.Default == null || s.Default.Type == JTokenType.Null) return;
            string text = s.Default.Type == JTokenType.String ? (string)s.Default : null;
            if (text == null)
                spec.Problems.Add("'" + s.Key + "' is a time field whose default is no text: it is a time of day written \"HH:mm\" (\"09:00\"), or \"\" for none.");
            else if (text.Trim().Length == 0)
                return;
            else if (!FieldBinding.TryTime(text, out string time))
                spec.Problems.Add("'" + s.Key + "' has the default '" + text + "', which is no time of day: a time field's default is written \"HH:mm\" (\"09:00\"), or \"\" for none. The field starts empty and has to be filled in before a save.");
            else if (time != text)
                spec.Problems.Add("'" + s.Key + "' has the default '" + text + "', which the window writes as '" + time + "'. Declare it so, since the project reads the default as it is declared.");
        }

        /// <summary>A setting kept in a persisted global of Streamer.bot (<c>global</c>, see <see cref="Globals"/>). One
        /// declared so that it cannot work is named, and so that its value never falls back to the settings file its
        /// field is locked and the value neither read nor saved. So is every such field while the host has no globals
        /// (a settings action before stub 0.3.3, a preview), with the reason in the window's language.</summary>
        private static void CheckGlobals(WindowSpec spec, List<Setting> settings)
        {
            const string locked = " The field is locked, and its value is neither read nor saved.";
            // The first setting that names each global: a second one would show the same value and write over it.
            var namedBy = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var s in Globals.Of(settings))
            {
                if (s.Kind == "action" || s.Kind == "note" || s.Kind == "table")
                {
                    // Nothing to lock: it holds no value.
                    s.GlobalOff = true;
                    spec.Problems.Add("'" + s.Key + "' has global, which only a field that holds a value has; it does nothing on " + (s.Kind == "action" ? "a button" : "a " + s.Kind) + ".");
                    continue;
                }
                string wrong = null;
                if (KindFamily(s.Kind) == "list")
                    wrong = "'" + s.Key + "' has global, which " + Article(s.Kind) + " cannot have: a global holds one text, not rows.";
                else if (string.IsNullOrWhiteSpace(s.Global))
                    wrong = "'" + s.Key + "' has global without a name; write the global's name, global: \"MyExtension_token\".";
                else if (!string.IsNullOrWhiteSpace(s.File))
                    wrong = "'" + s.Key + "' has global and file: a value kept in a global is in no file, so one of the two has to go.";
                else if (namedBy.TryGetValue(s.Global, out string first))
                    wrong = "'" + s.Key + "' names the global '" + s.Global + "', which '" + first + "' names already: two fields cannot share one.";
                else
                    namedBy[s.Global] = s.Key;
                if (wrong != null) spec.Problems.Add(wrong + locked);
                else if (spec.HostHasGlobals) continue;
                s.GlobalOff = true;
                s.ReadOnly = true;
                // A Set Argument's lock and the project's own say why already.
                if (!s.Locked) s.LockReason = wrong != null ? "" : Text.T("field.global.locked");
            }
        }

        /// <summary>The oldest UIFramework the window works with (<c>requires</c>). A settings action from stub 0.3.3 on
        /// offers the update instead of opening the window with an older DLL; one before it opens the window all the
        /// same, and the window says here what that means.</summary>
        private static void CheckRequires(WindowSpec spec)
        {
            if (string.IsNullOrWhiteSpace(spec.Requires)) return;
            var needs = VersionOf(spec.Requires);
            var own = VersionOf(Entry.FrameworkVersion);
            if (needs == null)
                spec.Problems.Add("The window requires UIFramework '" + spec.Requires.Trim() + "', which is no version number like 2.1.0; it is not checked.");
            else if (own != null && needs > own)
                spec.Problems.Add("The window requires UIFramework " + spec.Requires.Trim().TrimStart('v', 'V') + " or newer, and this is " + Entry.FrameworkVersion.TrimStart('v')
                    + ": what it declares for the newer one may not work. A settings action from stub 0.3.3 on offers the update instead of opening the window; update the extension.");
        }

        /// <summary>A version as the settings action reads one: numbers with dots, a leading v dropped, a number alone
        /// as "2.0", missing parts as 0. Null for anything else ("soon", "2.1-beta").</summary>
        private static Version VersionOf(string text)
        {
            text = (text ?? "").Trim().TrimStart('v', 'V');
            if (!Version.TryParse(text.Contains(".") ? text : text + ".0", out var version)) return null;
            return new Version(version.Major, version.Minor, Math.Max(0, version.Build), Math.Max(0, version.Revision));
        }

        /// <summary>A list the project fills as well: <c>rowKey</c>, <c>fixedRows</c> and <c>filter</c> belong to a list,
        /// a key column is one of its columns, no two default rows share a key, and a filter condition has an operator,
        /// a left side that is a column or a setting, and only settings as <c>{key}</c>.</summary>
        private static void CheckProjectList(WindowSpec spec, Dictionary<string, Setting> byKey, Setting s)
        {
            var keys = ListMerge.KeyColumns(s.RowKey);
            var filter = ListFilter.Parse(s.Filter);
            if (s.Kind != "list")
            {
                if (keys.Count > 0) spec.Problems.Add("'" + s.Key + "' has a rowKey, which only a list has (kind list); it does nothing on " + Article(s.Kind) + ".");
                if (s.FixedRows) spec.Problems.Add("'" + s.Key + "' has fixedRows, which only a list has (kind list); it does nothing on " + Article(s.Kind) + ".");
                if (filter != null) spec.Problems.Add("'" + s.Key + "' has a filter, which only a list has (kind list); it does nothing on " + Article(s.Kind) + ".");
                return;
            }
            var columns = s.Columns ?? new List<SettingColumn>();
            foreach (var key in keys.Where(k => !columns.Any(c => c.Key == k)).Distinct(StringComparer.Ordinal))
                spec.Problems.Add("'" + s.Key + "' has the rowKey '" + s.RowKey.Trim() + "', but the list has no column '" + key + "'. A row that lacks a part of its key is told from the others by all its cells.");
            foreach (var twice in ListMerge.Duplicates(s.Default as JArray, keys, columns))
                spec.Problems.Add("'" + s.Key + "' has two default rows with the key '" + twice + "' (rowKey '" + s.RowKey.Trim() + "'); a save tells rows of one key apart by their order only.");
            foreach (var condition in filter?.Conditions ?? new List<ListFilter.Condition>())
            {
                const string shows = " That condition counts as holding, so every row shows.";
                if (condition.Op == null)
                {
                    spec.Problems.Add("'" + s.Key + "' has the filter condition '" + condition.Text + "', which has no operator (=, != or ~)." + shows);
                    continue;
                }
                if (!columns.Any(c => c.Key == condition.Left) && !byKey.ContainsKey(condition.Left))
                    spec.Problems.Add("'" + s.Key + "' filters by '" + condition.Left + "' in '" + condition.Text + "', which is neither a column of the list nor a setting." + shows);
                foreach (var key in ListFilter.Keys(condition.Value).Where(k => !byKey.ContainsKey(k)).Distinct(StringComparer.Ordinal))
                    spec.Problems.Add("'" + s.Key + "' has {" + key + "} in its filter condition '" + condition.Text + "', which is not a setting." + shows);
            }
        }

        /// <summary>A section whose fields are not one after the other gets its heading twice: said, so the game can order
        /// them. As the game placed them, on its own page or the one the window's pages claim it for: where the user's
        /// layout moved a field is not the game author's to fix.</summary>
        private static void CheckSplitSections(WindowSpec spec, List<(string Key, string Page, int Order, string Section, bool Hidden)> asDeclared,
            Dictionary<string, string> claimedByWindow)
        {
            foreach (var page in asDeclared.Where(s => !s.Hidden).GroupBy(s => claimedByWindow.TryGetValue(s.Key, out var claimedBy) ? claimedBy : s.Page ?? "game"))
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
        }

        /// <summary>A page the user sorted in the customise mode has an order in the layout for every field it showed then
        /// (0, 10, 20 ...). A field without one, which the project added in an update or "Reset this field" gave back,
        /// kept its place among all the project's settings, which fell anywhere among those orders, often inside another
        /// section, whose heading then showed twice. It now goes behind the field the project declares before it on the
        /// page when it shares that one's section, else to the end of its own section there, else to the end of the page.
        /// The page's fields are then numbered in that order.</summary>
        private static void PlaceUnsorted(List<Setting> page, ISet<string> placed, IDictionary<string, int> declared)
        {
            if (!page.Any(s => placed.Contains(s.Key)) || page.All(s => placed.Contains(s.Key))) return;
            var order = page.Where(s => placed.Contains(s.Key)).OrderBy(s => s.Order).ToList();
            Setting before = null;
            foreach (var setting in page.OrderBy(s => declared[s.Key]))
            {
                if (!placed.Contains(setting.Key))
                {
                    // Behind the field declared before it, or first when none is. A field without a section takes the
                    // section it lands in; one with a section goes only where that section is.
                    int at = before == null ? 0 : order.IndexOf(before) + 1;
                    string there = before != null ? before.Section : order.Count > 0 ? order[0].Section : setting.Section;
                    if (setting.Section != null && setting.Section != there)
                    {
                        int last = order.FindLastIndex(s => s.Section == setting.Section);
                        at = last >= 0 ? last + 1 : order.Count;
                    }
                    order.Insert(at, setting);
                }
                before = setting;
            }
            for (int i = 0; i < order.Count; i++)
                order[i].Order = i;
        }

        /// <summary>A points system the game found installed: a pill in the header, and the two points settings say
        /// they are the fallback while it is there.</summary>
        private static void Currency(WindowSpec spec, Schema schema, List<Setting> settings)
        {
            if (spec.CurrencyPill != null) { spec.HeaderPills.Remove(spec.CurrencyPill); spec.CurrencyPill = null; }
            string source = (string)schema.Currency?["source"];
            if (string.IsNullOrWhiteSpace(source)) return;
            string variable = (string)schema.Currency["variable"] ?? "", name = (string)schema.Currency["name"] ?? "";
            // In the window's language, like the help it is appended to (it was English in every language).
            string Said(string key, string english)
            {
                var ui = spec.Conventions?.Lang?.Ui;
                return Text.Fill(ui != null && ui.TryGetValue(key, out var own) ? own : english, source, variable, name);
            }
            string note = Said("currency.note", "{0} is installed: the points live in the user variable '{1}' and are called '{2}'. This setting is used only without it.");
            foreach (var s in settings)
                if (s.Key == "pointsVariable" || s.Key == "pointsName")
                    s.Help = string.IsNullOrEmpty(s.Help) ? note : s.Help + " " + note;
            string text = source + ": " + variable;
            string tooltip = Said("currency.pill", "Points by {0}, in the user variable '{1}', called '{2}'. The Points variable and Points name settings are not used while it is installed.");
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
