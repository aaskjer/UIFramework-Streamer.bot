using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace UIFramework
{
    /// <summary>A page as the spec names it, before the schema fills it. See docs/Schema.md, "The window".</summary>
    public sealed class SpecPage
    {
        public string Id;
        public string Title;
        public string Icon;
        /// <summary>Keys or wildcard patterns ("overlay*") this page claims, in order.</summary>
        public readonly List<string> Fields = new List<string>();
        /// <summary>Built by the host (IHostHooks.BuildPage), not from the schema.</summary>
        public bool Custom;
        public bool Bottom;
    }

    /// <summary>What the spec changes about one setting: where it sits, what it is called, whether it shows.</summary>
    public sealed class FieldOverride
    {
        public string Page, Label, Help, Section;
        public bool? Hidden;
        public int? Order;
    }

    /// <summary>
    /// Reads the spec JSON a stub carries, or the window a schema declares (the same JSON), into a
    /// <see cref="WindowSpec"/>. The pages it names are kept as <see cref="WindowSpec.SpecPages"/> for
    /// <see cref="Resolve"/> to fill from the schema.
    /// </summary>
    public static class SpecJson
    {
        public static WindowSpec Parse(string json, string settingsPath = null) => Parse(Root(json), settingsPath);

        /// <summary>The spec JSON as an object; empty text is an empty object.</summary>
        public static JObject Root(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return new JObject();
            using (var reader = new JsonTextReader(new System.IO.StringReader(json)) { DateParseHandling = DateParseHandling.None })
                return JObject.Load(reader);
        }

        public static WindowSpec Parse(JObject root, string settingsPath = null)
        {
            var spec = new WindowSpec
            {
                Title = (string)root["title"] ?? "Settings",
                Version = (string)root["version"],
                GitHubRepo = (string)root["releases"] ?? (string)root["gitHubRepo"],
                LogoUrl = (string)root["logo"],
                SettingsPath = settingsPath ?? (string)root["settingsFile"],
                LogPrefix = (string)root["logPrefix"] ?? ("[" + ((string)root["title"] ?? "UIFramework") + "/Settings]")
            };
            if (root["width"] != null) spec.Width = (double)root["width"];
            if (root["height"] != null) spec.Height = (double)root["height"];
            if (root["language"] != null) spec.Language = (string)root["language"];
            spec.Locked = root["locked"]?.Type == JTokenType.Boolean && (bool)root["locked"];

            if (root["pages"] is JArray pages)
            {
                foreach (var item in pages)
                {
                    var page = new SpecPage();
                    if (item.Type == JTokenType.String)
                        page.Id = item.ToString();
                    else if (item is JObject o)
                    {
                        page.Id = (string)o["id"];
                        page.Title = (string)o["title"];
                        page.Icon = (string)o["icon"];
                        page.Custom = o["custom"]?.Type == JTokenType.Boolean && (bool)o["custom"] || o["custom"]?.Type == JTokenType.String;
                        page.Bottom = o["bottom"]?.Type == JTokenType.Boolean && (bool)o["bottom"];
                        if (o["fields"] is JArray fields)
                            page.Fields.AddRange(fields.Select(f => f.ToString()));
                    }
                    if (!string.IsNullOrWhiteSpace(page.Id))
                        spec.SpecPages.Add(page);
                }
            }

            if (root["overrides"] is JObject overrides)
                foreach (var property in overrides.Properties())
                {
                    var o = property.Value as JObject ?? new JObject();
                    spec.Overrides[property.Name] = new FieldOverride
                    {
                        Page = (string)o["page"],
                        Label = (string)o["label"],
                        Help = (string)o["help"],
                        Section = (string)o["section"],
                        Hidden = o["hidden"]?.Type == JTokenType.Boolean ? (bool?)(bool)o["hidden"] : null,
                        Order = (int?)o["order"]
                    };
                }

            if (root["samples"] is JObject samples)
                foreach (var property in samples.Properties())
                    spec.Samples[property.Name] = property.Value.ToString();

            // What an action would answer, for the preview and the screenshots: text, or a JSON object as the host would send it.
            if (root["answers"] is JObject answers)
                foreach (var property in answers.Properties())
                    spec.Answers[property.Name] = property.Value.Type == JTokenType.String ? (string)property.Value : property.Value.ToString(Newtonsoft.Json.Formatting.None);

            if (root["senders"] is JObject senders)
            {
                // A key that is there with null hides that switch (docs/Schema.md); a key left out keeps the default.
                string Sender(string name, string fallback) =>
                    senders[name] == null ? fallback : senders[name].Type == JTokenType.Null ? null : (string)senders[name];
                spec.Senders.UseBot = Sender("useBot", spec.Senders.UseBot);
                spec.Senders.SendAction = Sender("sendAction", spec.Senders.SendAction);
                spec.Senders.CooldownUseBot = Sender("cooldownUseBot", spec.Senders.CooldownUseBot);
                spec.Senders.CooldownSendAction = Sender("cooldownSendAction", spec.Senders.CooldownSendAction);
                spec.Senders.CooldownGroup = Sender("cooldownGroup", spec.Senders.CooldownGroup);
                if (senders["numberSeparator"] != null)
                    spec.Senders.NumberSeparator = senders["numberSeparator"].Type == JTokenType.Null ? null : (string)senders["numberSeparator"];
            }

            if (root["help"] is JArray help)
                foreach (var section in help.OfType<JObject>())
                    spec.Help.Add(ParseSection(section, spec, "help"));

            ParseDeclared(root["triggers"] as JArray, spec.Triggers, needsCode: true);
            ParseDeclared(root["methods"] as JArray, spec.Methods, needsCode: false);

            if (root["about"] is JObject about)
            {
                if (about["credits"] != null) spec.About.Credits = (string)about["credits"];
                if (about["sections"] is JArray sections)
                    foreach (var section in sections.OfType<JObject>())
                        spec.About.Sections.Add(ParseSection(section, spec, "About"));
                if (about["links"] is JArray links)
                    foreach (var link in links.OfType<JObject>())
                    {
                        // The builder hands every line as one type, so a Cmd, a Note or a Tip compiles here too.
                        if (string.IsNullOrWhiteSpace((string)link["text"]) || string.IsNullOrWhiteSpace((string)link["url"]))
                            spec.ReadProblems.Add("A link of the About page has no text or no url (a Cmd, a Note or a Tip where a Link belongs?); it is left out.");
                        else
                            spec.About.Links.Add(new Link((string)link["text"], (string)link["url"]));
                    }
                if (about["streamerBotLink"]?.Type == JTokenType.Boolean) spec.About.ShowStreamerBotLink = (bool)about["streamerBotLink"];
                if (about["defaultLinks"]?.Type == JTokenType.Boolean) spec.About.ShowDefaultLinks = (bool)about["defaultLinks"];
                // "authorLink" of older host blocks is read no more: the framework names no author of its own.
            }

            if (root["log"] is JObject log)
            {
                spec.Log.Folder = (string)log["folder"];
                spec.Log.Files = (string)log["files"];
                spec.Log.WriteLogKey = (string)log["writeLogKey"];
                if (log["writeLogLabel"] != null) spec.Log.WriteLogLabel = (string)log["writeLogLabel"];
                spec.Log.WriteLogHelp = (string)log["writeLogHelp"];
                if (log["emptyText"] != null) spec.Log.EmptyText = (string)log["emptyText"];
                if (log["columns"] is JArray columns)
                    foreach (var column in columns)
                    {
                        if (column is JObject c) spec.Log.Columns.Add(new LogColumn((string)c["header"] ?? (string)c["field"], (string)c["field"]));
                        else spec.Log.Columns.Add(new LogColumn(column.ToString(), column.ToString()));
                    }
                if (log["results"] is JArray results)
                    spec.Log.Results.AddRange(results.Select(r => r.ToString()));
                Pairs(log["styles"], spec.Log.Styles);
                Pairs(log["outcome"], spec.Log.Outcomes);
                if (log["platforms"] is JArray platforms)
                {
                    spec.Log.Platforms.Clear();
                    spec.Log.Platforms.AddRange(platforms.Select(p => p.ToString()));
                }
            }

            if (root["sections"] is JArray sectionList)
                foreach (var section in sectionList.OfType<JObject>())
                    if (!string.IsNullOrWhiteSpace((string)section["name"]) && !string.IsNullOrWhiteSpace((string)section["showWhen"]))
                        spec.SectionConditions[(string)section["name"]] = (string)section["showWhen"];

            if (root["tour"] is JArray tour)
                foreach (var stop in tour)
                {
                    // A stop is an object, or just the name of its target ("save"): the framework's own text then.
                    if (stop.Type == JTokenType.String) spec.Tour.Add(new TourStop(stop.ToString()));
                    else if (stop is JObject t)
                    {
                        string target = (string)t["target"], title = (string)t["title"], text = (string)t["text"];
                        if (string.IsNullOrWhiteSpace(target) && string.IsNullOrWhiteSpace(title) && string.IsNullOrWhiteSpace(text))
                        {
                            // The builder hands every line as one type, so a Cmd, a Link or a Note compiles in a tour.
                            if (t.Count > 0)
                                spec.ReadProblems.Add("A stop of the tour has neither a target nor a title nor a text (a Cmd, a Link or a Note where a Tip belongs?); it is left out.");
                            continue;
                        }
                        spec.Tour.Add(new TourStop(string.IsNullOrWhiteSpace(target) ? null : target.Trim(), title, text)
                            { Act = t["act"]?.Type == JTokenType.Boolean && (bool)t["act"] });
                    }
                }

            if (root["headerPills"] is JArray pillList)
                foreach (var pill in pillList.OfType<JObject>())
                {
                    string key = (string)pill["key"];
                    string on = (string)pill["on"], off = (string)pill["off"], tooltip = (string)pill["tooltip"];
                    if (string.IsNullOrEmpty(key)) continue;
                    spec.HeaderPillKeys.Add(key);
                    // A switch the window does not show (its page or field hidden in the customise mode) is read from
                    // the saved value: the pill said "off" for whatever was not on the window.
                    bool On(PageContext ctx) => ctx.Parts != null && ctx.Parts.Switches.ContainsKey(key)
                        ? ctx.Fields.LiveSwitch(key)
                        : ctx.Fields.Token(key) is JToken saved && saved.Type == JTokenType.Boolean && (bool)saved;
                    spec.HeaderPills.Add(new HeaderPill(tooltip, ctx => On(ctx)
                        ? (ctx.Spec.Conventions.Lang.Text(on), PillStyle.Success)
                        : (ctx.Spec.Conventions.Lang.Text(off), off == null ? PillStyle.None : PillStyle.Muted)));
                }

            return spec;
        }

        // Pairs in order: an object ({ "WIN-*": "success" }), or a list of "WIN-*|success" as the builder takes them.
        private static void Pairs(JToken declared, List<KeyValuePair<string, string>> into)
        {
            if (declared is JObject map)
                foreach (var property in map.Properties())
                    into.Add(new KeyValuePair<string, string>(property.Name, property.Value?.ToString()));
            else if (declared is JArray list)
                foreach (var item in list)
                {
                    string text = item.ToString();
                    int bar = text.LastIndexOf('|');
                    if (bar > 0) into.Add(new KeyValuePair<string, string>(text.Substring(0, bar).Trim(), text.Substring(bar + 1).Trim()));
                }
        }

        // A trigger without a code cannot be fired and one without a name cannot be shown: both are left out.
        private static void ParseDeclared(JArray list, List<Declared> into, bool needsCode)
        {
            if (list == null) return;
            foreach (var o in list.OfType<JObject>())
            {
                string name = (string)o["name"], code = (string)o["code"];
                if (string.IsNullOrWhiteSpace(name) || (needsCode && string.IsNullOrWhiteSpace(code))) continue;
                var declared = new Declared { Name = name.Trim(), Code = code?.Trim(), What = (string)o["what"] };
                if (o["args"] is JArray args)
                    foreach (var arg in args)
                    {
                        string argName = arg is JObject a ? (string)a["name"] : arg.ToString();
                        if (!string.IsNullOrWhiteSpace(argName))
                            declared.Args.Add((argName.Trim(), arg is JObject t ? (string)t["text"] : null));
                    }
                into.Add(declared);
            }
        }

        private static HelpSection ParseSection(JObject o, WindowSpec spec = null, string where = null)
        {
            var section = new HelpSection((string)o["heading"]);
            if (o["lines"] is JArray lines)
                foreach (var line in lines)
                {
                    if (line.Type == JTokenType.String) { section.Note(line.ToString()); continue; }
                    if (!(line is JObject l)) continue;
                    if (l["note"] != null) section.Note((string)l["note"]);
                    else if (l["command"] != null) section.Command((string)l["command"], (string)l["what"] ?? "");
                    else if (l["link"] != null) section.Link((string)l["link"], (string)l["url"]);
                    // The builder hands every line as one type, so a Tip compiles in a help section.
                    else spec?.ReadProblems.Add("A line of the " + where + " section '" + (string)o["heading"] + "' is neither a note, a command nor a link (a Tip?); it is left out.");
                }
            return section;
        }
    }
}
