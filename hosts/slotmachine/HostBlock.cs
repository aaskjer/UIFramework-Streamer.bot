// --- UIFramework host block (keep identical in every action of this host that reads the settings) ---
// From UIFramework/stub/HostSchema.cs (tools/update-host-block.ps1 refreshes it). The game declares its settings
// once in UiSchema.Table and its window in UiSchema.Window, reads the settings through UiSettings, and lets
// UiHost.Handle answer the settings window's requests. One table, so the game and its window can never disagree.
// Needs no usings and no reference beyond Newtonsoft.Json, which every action has: paste it as it is, right below
// the action's using lines (anywhere after them works too: at the end, or inside the CPHInline class).
//
// In Execute(), first thing:
//     if (UiHost.Handle(args, CPH, ActionName, "demo", CurrentVersion, "Demo Game/settings.json"))
//         return true;
// With a Try button, a check before saving or work after it (see UiHost.Handle):
//     if (UiHost.Handle(args, CPH, ActionName, "demo", CurrentVersion, SettingsFile,
//             test: (name, s) => name == "tryRoll" ? TryRoll(s) : null, validate: s => Problems(s), saved: s => SetupOverlay(s)))
//         return true;
public static class UiSchema
{
    public sealed class Setting
    {
        public string Key; public object Default; public string Kind, Page, Section, Group, Label, Help, Unit, Requires, Confirm, Platform, Row;
        public double? Min, Max, Factor; public bool UnlimitedAtZero, Hidden;
        public string[] Options, Placeholders, Aliases; public double? AliasFactor;
        // For a list: its columns as "key|kind|label" (kinds: string, text, int, double, bool, emote; a trailing "readonly"
        // makes it a column the game fills and the user only reads; an emote column
        // adds "|twitch", "|youtube" or "|kick" for its picture), and the file next to the settings file that holds
        // the rows, with the property in it (no field: the file is the array).
        public string[] Columns; public string File, Field;
        // Transient: shown, never saved. For an action: Live hands the host the window's current values, Panel shows
        // its answer under the button, Auto asks it on show and after every change without a button. Live on any
        // other field asks the host whenever the field changes (a preset slider whose answer fills in other fields).
        // Row: settings naming the same row, one after the other, share one line of the page.
        public bool Transient, Live, Panel, Auto;
        // TwitchOnly: a switch that only matters on Twitch carries a "Twitch only" pill.
        public bool TwitchOnly;
        // Masked: a text field shown as dots until its eye button reveals it (a token, a client secret).
        public bool Masked;
        // ShowWhen: shown only while the condition holds ("questionSource=igdb", "mode=a|b", "x!=free && useBot").
        // Shows, on a dropdown: per option, the fields it shows, "igdb: igdbClientId, igdbClientSecret".
        public string ShowWhen; public string[] Shows;
        // ReadOnly: shown, not editable (a status the game writes). Step: a slider's step.
        public bool ReadOnly; public double? Step;
    }

    /// <summary>A setting: key and default, then whatever the conventions cannot know. Keys the conventions know
    /// (useBot, globalCooldown, blockedUsers, ...) need nothing but their default. An option is "value" or
    /// "value|label". aliasFactor multiplies the first alias's value on the move (a fraction to percent: 100).
    /// requires is a switch's key, or a condition on any field: "numberOfSlots>=5", "cpBetMode=fixed", "mode!=free"; with
    /// "hide:" in front the field is hidden instead of greyed out while it does not hold. masked shows a text field as dots.
    /// showWhen shows the field only while its condition holds: "questionSource=igdb", several values "mode=a|b", several
    /// conditions "mode!=free && useBot", "!key" for a switch that is off. On a dropdown, shows names per option the fields it
    /// shows: "igdb: clientId, clientSecret". readOnly shows a value the game writes without letting the user edit it; step is
    /// a slider's step; group puts a message template under a heading of its own.</summary>
    public static Setting S(string key, object @default, string page = null, string label = null, string help = null, string kind = null,
        string section = null, double? min = null, double? max = null, string unit = null, double? factor = null, bool unlimitedAtZero = false,
        string[] options = null, string requires = null, string confirm = null, bool hidden = false, string[] aliases = null, double? aliasFactor = null,
        string[] columns = null, string file = null, string field = null, bool transient = false, bool live = false, bool panel = false, bool auto = false,
        string platform = null, bool twitchOnly = false, string row = null, bool masked = false, string showWhen = null, string[] shows = null,
        bool readOnly = false, double? step = null, string group = null)
    {
        return new Setting
        {
            Key = key, Default = @default, Page = page, Label = label, Help = help, Kind = kind, Section = section, Min = min, Max = max,
            Unit = unit, Factor = factor, UnlimitedAtZero = unlimitedAtZero, Options = options, Requires = requires, Confirm = confirm,
            Hidden = hidden, Aliases = aliases, AliasFactor = aliasFactor, Columns = columns, File = file, Field = field,
            Transient = transient, Live = live, Panel = panel, Auto = auto, Platform = platform, TwitchOnly = twitchOnly, Row = row, Masked = masked,
            ShowWhen = showWhen, Shows = shows, ReadOnly = readOnly, Step = step, Group = group
        };
    }

    /// <summary>A message template: group, key, default text, label, help, placeholders without braces, old key; file and
    /// field when the text lives in a file of its own next to the settings file (a messages.json).</summary>
    public static Setting Msg(string group, string key, string @default, string label, string help, string[] placeholders, string alias = null,
        string file = null, string field = null)
        => new Setting { Key = key, Default = @default, Kind = "template", Page = "messages", Group = group, Label = label, Help = help,
                         Placeholders = placeholders, Aliases = alias == null ? null : new[] { alias }, File = file, Field = field };

    public static string ToJson(string host, string version, string settingsFile) => ToJson(host, version, settingsFile, null);

    /// <summary>The schema with the points system the window should show as detected (UiCurrency.Detect).</summary>
    public static string ToJson(string host, string version, string settingsFile, UiCurrency currency)
    {
        var settings = new Newtonsoft.Json.Linq.JArray();
        foreach (var s in Table)
            settings.Add(ToJson(s));
        var root = new Newtonsoft.Json.Linq.JObject { ["host"] = host, ["version"] = version, ["settingsFile"] = settingsFile, ["settings"] = settings };
        if (Window != null) root["window"] = Window.ToJson();
        if (Window != null && Window.LangMap.Count > 0) root["lang"] = Window.LangMap;
        if (currency != null && currency.Detected) root["currency"] = currency.ToJson();
        return root.ToString(Newtonsoft.Json.Formatting.None);
    }

    /// <summary>One setting as the window reads it.</summary>
    public static Newtonsoft.Json.Linq.JObject ToJson(Setting s)
    {
        var item = new Newtonsoft.Json.Linq.JObject { ["key"] = s.Key, ["default"] = s.Default == null ? Newtonsoft.Json.Linq.JValue.CreateNull() : Newtonsoft.Json.Linq.JToken.FromObject(s.Default) };
        if (s.Kind != null) item["kind"] = s.Kind;
        if (s.Page != null) item["page"] = s.Page;
        if (s.Section != null) item["section"] = s.Section;
        if (s.Group != null) item["group"] = s.Group;
        if (s.Label != null) item["label"] = s.Label;
        if (s.Help != null) item["help"] = s.Help;
        if (s.Min.HasValue) item["min"] = s.Min.Value;
        if (s.Max.HasValue) item["max"] = s.Max.Value;
        if (s.Unit != null) item["unit"] = s.Unit;
        if (s.Platform != null) item["platform"] = s.Platform;
        if (s.Factor.HasValue) item["factor"] = s.Factor.Value;
        if (s.UnlimitedAtZero) item["unlimitedAtZero"] = true;
        if (s.Hidden) item["hidden"] = true;
        if (s.Requires != null) item["requires"] = s.Requires;
        if (s.Confirm != null) item["confirm"] = s.Confirm;
        if (s.Options != null)
        {
            var options = new Newtonsoft.Json.Linq.JArray();
            foreach (var option in s.Options)
            {
                int bar = option.IndexOf('|');
                options.Add(bar < 0 ? (Newtonsoft.Json.Linq.JToken)option : new Newtonsoft.Json.Linq.JObject { ["value"] = option.Substring(0, bar), ["label"] = option.Substring(bar + 1) });
            }
            item["options"] = options;
        }
        if (s.Placeholders != null) item["placeholders"] = new Newtonsoft.Json.Linq.JArray(s.Placeholders);
        if (s.Columns != null)
        {
            var columns = new Newtonsoft.Json.Linq.JArray();
            foreach (var column in s.Columns)
            {
                var parts = new System.Collections.Generic.List<string>(column.Split('|'));
                var cell = new Newtonsoft.Json.Linq.JObject { ["key"] = parts[0], ["kind"] = parts.Count > 1 ? parts[1] : "string" };
                // Flags at the end: readonly (the game fills it, the user only reads), an emote column's platform.
                // What stands between kind and flags is the label, which may itself contain a | ("a | b | c").
                // A flag right after the kind ("key|int|readonly") counts as one too; the label is then the key.
                while (parts.Count > 3 || (parts.Count == 3 && (parts[2] == "readonly" || parts[2] == "twitch" || parts[2] == "youtube" || parts[2] == "kick")))
                {
                    string last = parts[parts.Count - 1];
                    if (last == "readonly") cell["readOnly"] = true;
                    else if (last == "twitch" || last == "youtube" || last == "kick") cell["platform"] = last;
                    else break;
                    parts.RemoveAt(parts.Count - 1);
                }
                cell["label"] = parts.Count > 2 ? string.Join("|", parts.GetRange(2, parts.Count - 2).ToArray()) : parts[0];
                columns.Add(cell);
            }
            item["columns"] = columns;
        }
        if (s.File != null) item["file"] = s.File;
        if (s.Transient) item["transient"] = true;
        if (s.Live) item["live"] = true;
        if (s.Panel) item["panel"] = true;
        if (s.Auto) item["auto"] = true;
        if (s.TwitchOnly) item["twitchOnly"] = true;
        if (s.Masked) item["masked"] = true;
        if (s.ReadOnly) item["readOnly"] = true;
        if (s.Step.HasValue) item["step"] = s.Step.Value;
        if (s.ShowWhen != null) item["showWhen"] = s.ShowWhen;
        if (s.Shows != null)
        {
            var shows = new Newtonsoft.Json.Linq.JObject();
            foreach (var entry in s.Shows)
            {
                int colon = entry.IndexOf(':');
                if (colon <= 0) continue;
                var keys = shows[entry.Substring(0, colon).Trim()] as Newtonsoft.Json.Linq.JArray;
                if (keys == null) shows[entry.Substring(0, colon).Trim()] = keys = new Newtonsoft.Json.Linq.JArray();
                foreach (var key in entry.Substring(colon + 1).Split(','))
                    if (key.Trim().Length > 0) keys.Add(key.Trim());
            }
            item["shows"] = shows;
        }
        if (s.Row != null) item["row"] = s.Row;
        if (s.Field != null) item["field"] = s.Field;
        if (s.Aliases != null) item["aliases"] = new Newtonsoft.Json.Linq.JArray(s.Aliases);
        if (s.AliasFactor.HasValue && s.Aliases != null && s.Aliases.Length > 0) item["aliasFactors"] = new Newtonsoft.Json.Linq.JObject { [s.Aliases[0]] = s.AliasFactor.Value };
        return item;
    }

    // ---- Shorthands for the window: a page, a help section and its lines, a cooldown. ----
    public static UiPage P(string id, string title = null, string icon = null, params string[] fields) => new UiPage(id, title, icon, fields);
    public static UiSection H(string heading, params object[] lines) => new UiSection(heading, lines);
    public static UiLine Cmd(string command, string what) => new UiLine(new Newtonsoft.Json.Linq.JObject { ["command"] = command, ["what"] = what });
    public static UiLine Note(string note) => new UiLine(new Newtonsoft.Json.Linq.JObject { ["note"] = note });
    public static UiLine Link(string text, string url) => new UiLine(new Newtonsoft.Json.Linq.JObject { ["link"] = text, ["url"] = url });
    /// <summary>A command's cooldowns, set after every save: the command as its text ("!roll"), its name or its id in
    /// Streamer.bot, the setting of its global cooldown and the setting of its per-user cooldown (either may be null).</summary>
    public static string[] C(string command, string globalKey, string userKey) => new[] { command, globalKey, userKey };
    /// <summary>A stop of the window's tour (UiWindow.Tour): what it points at, a heading and a text. The target is a
    /// setting's key, "page:id" for a page in the rail, a part of the window ("start", "end", "rail", "save",
    /// "saveExit", "cancel", "status", "reset", "resetPage", "theme", "customise", "tour"), or null for a note in the
    /// middle. Left out, title and text are the framework's own for a part or a page, a setting's label and help.
    /// act: the user may use the ringed part while the stop is up (press the Try button, type into the field).</summary>
    public static UiLine Tip(string target, string title = null, string text = null, bool act = false)
    {
        var tip = new Newtonsoft.Json.Linq.JObject();
        if (target != null) tip["target"] = target;
        if (title != null) tip["title"] = title;
        if (text != null) tip["text"] = text;
        if (act) tip["act"] = true;
        return new UiLine(tip);
    }

    // ---- Shorthands for UiWindow.Lang: a key's texts, a page title, a declared text. ----
    public static UiText K(string key, string label, string help = null) => new UiText("keys", key, help == null ? (Newtonsoft.Json.Linq.JToken)label : new Newtonsoft.Json.Linq.JObject { ["label"] = label, ["help"] = help });
    public static UiText Pg(string id, string title) => new UiText("pages", id, title);
    public static UiText Tx(string declared, string translation) => new UiText("texts", declared, translation);
    /// <summary>One of the window's own texts by its key in conventions/lang/en.json's ui section ("btn.save"), for a
    /// language the framework has no file for yet.</summary>
    public static UiText Ui(string key, string text) => new UiText("ui", key, text);
    /// <summary>The help of a placeholder chip ({user}, {amount} …) in the game's language.</summary>
    public static UiText Ph(string placeholder, string help) => new UiText("placeholders", placeholder, help);

    /// <summary>The global the stub reads the schema from: the action's name, with the characters Streamer.bot's
    /// store refuses (brackets, a trailing space) replaced. Must match the stub's SchemaVar.</summary>
    public static string Var(string actionName) => "uiframework.schema." + Clean(actionName);

    /// <summary>An action's name as a global's name: letters, digits, space, dot and dash stay, the rest is "_".
    /// No Regex: a game action may ship without a reference to System.dll.</summary>
    public static string Clean(string actionName)
    {
        var chars = (actionName ?? "").Trim().ToCharArray();
        for (int i = 0; i < chars.Length; i++)
            if (!char.IsLetterOrDigit(chars[i]) && chars[i] != '_' && chars[i] != ' ' && chars[i] != '.' && chars[i] != '-') chars[i] = '_';
        return new string(chars);
    }

    public static Setting Find(string key)
    {
        foreach (var s in Table)
            if (s.Key == key) return s;
        return null;
    }

    // ---- The window: title, pages, help, about, log. Null: the settings action carries a spec of its own. ----
    public static readonly UiWindow Window = new UiWindow("Slot Machine Game", releases: "aaskjer/Slot-Machine-Game", logo: "https://github.com/aaskjer/Slot-Machine-Game/blob/main/Assets/logo-small.png?raw=true", logPrefix: "[Slot Machine Game/Settings]")
        .Pages(P("game", "Game", "🎰"), P("emotes", "Emotes", "😀"), P("access", "Access", "👥"), P("messages", "Messages", "💬"), P("log", "Log", "📜"), P("help", "How to play", "📖"), P("about", "About", "ℹ️"))
        .Senders(useBot: "useBot", sendAction: "sendAction", cooldownUseBot: "cooldownUseBot", cooldownSendAction: "cooldownSendAction", numberSeparator: "numberSeparator", cooldownGroup: "Cooldown")
        .Cooldowns(C("f8a9c5fa-edb7-4de5-bb92-c0fdf53f005e", "globalCooldown", "userCooldown"))   // !slots, where the cooldowns live
        // German: the game's own keys and texts; sections, pages and the shared keys come from the conventions.
        .Lang("de",
            Pg("emotes", "Emotes"),
            K("numberOfSlots", "Walzen", "Wie viele Walzen eine Drehung zeigt. Alle gleich ist der Jackpot."),
            K("matchChance", "Treffer-Chance", "Chance in Prozent, dass eine Drehung gleiche Walzen zeigen darf: sie werden dann frei gezogen, ein Treffer ist also wahrscheinlich, aber nicht sicher. Sonst wird eine Wiederholung neu gezogen, um einen Treffer zu vermeiden. Die Quoten darunter zeigen, was wirklich herauskommt."),
            K("jackpotChance", "Jackpot-Chance", "Chance in Prozent, dass jede Walze dasselbe Emote zeigt - der Jackpot. '1' ist 1 von 100. Auch eine freie Ziehung kann alle gleich landen, die echte Chance ist also etwas höher."),
            K("rerollAttempts", "Neuziehungen", "Wie hartnäckig eine Drehung, die nicht treffen soll, eine Wiederholung vermeidet: jede Walze wird bis zu so oft gezogen. Eine Wiederholung kann trotzdem durchrutschen, vor allem mit wenigen Emotes im Spiel."),
            K("emotePoolSize", "Emote-Pool (0 = alle)", "Wie viele der Emotes im Spiel für jede Drehung gezogen werden. Weniger Emotes machen Treffer wahrscheinlicher. 0, oder mehr als es gibt, nimmt alle."),
            K("minEmoteWeight", "Mindestgewicht", "Nur Emotes mit mindestens diesem Gewicht spielen. Niedrige Gewichte kommen oft und zahlen wenig, ein höherer Wert nimmt also die billigsten Emotes heraus."),
            K("maxEmoteWeight", "Höchstgewicht", "Nur Emotes mit höchstens diesem Gewicht spielen. Hohe Gewichte sind selten und zahlen viel, ein niedrigerer Wert nimmt also die wertvollsten Emotes heraus."),
            K("defaultBet", "Standardeinsatz", "Der Einsatz für eine Drehung ohne Betrag. Zuschauer können auch eine Zahl, all, half, third, quarter oder random setzen."),
            K("minBet", "Minimum (0 = keins)", "Der kleinste erlaubte Einsatz. 0 schaltet die Grenze ab. 'random' setzt zwischen Minimum und Maximum. Gilt auch für Kanalpunkte in den Modi 'Eingegebener Betrag' und 'Fest'."),
            K("maxBet", "Maximum (0 = keins)", "Der größte erlaubte Einsatz. 0 schaltet die Grenze ab. Gilt auch für Kanalpunkte in den Modi 'Eingegebener Betrag' und 'Fest'."),
            K("freeGame", "Freier Modus", "Jede Drehung ist frei: niemand zahlt oder gewinnt Punkte, der Jackpot-Topf wächst nicht, und der Chat bekommt die Nachrichten unter Freier Modus. !SetFreeSlots und !EndFreeSlots schalten ihn aus dem Chat (Moderatoren). Für eine einzelne freie Drehung kann ein eigener Trigger stattdessen das Argument free=1 mitgeben."),
            K("pointsVariable", "Punkte-Variable", "Die Variable, in der deine Punkte liegen. Currency Core von StreamUP und LOYALTY POINTS von tawmae werden von selbst gefunden; nur für ein eigenes Punktesystem ändern."),
            K("pointsName", "Im Chat genannt als", "Der Name, den {currency} in den Chat setzt, etwa 'Coins' oder 'Gems'. Currency Core und LOYALTY POINTS bringen ihren eigenen Namen mit, der dann gilt."),
            K("twoMatchPayout", "2 gleiche", "Ein Gewinn zahlt Einsatz × Auszahlung % × Gewicht des Emotes ÷ 100, und nie weniger als den Einsatz. Alle Walzen gleich ist der Jackpot, der die Jackpot-Stufe zahlt, egal wie viele Walzen."),
            K("threeMatchPayout", "3 gleiche", "Mit 3 Walzen ist drei gleich der Jackpot und diese Stufe wird nicht gebraucht."),
            K("fourMatchPayout", "4 gleiche", "Nur mit 5 oder 6 Walzen; weniger Walzen landen vier gleiche nur als Jackpot."),
            K("fiveMatchPayout", "5 gleiche", "Nur mit 6 Walzen."),
            K("sixMatchPayout", "Jackpot", "Alle Walzen gleich, egal wie viele: 3 von 3 genauso wie 6 von 6."),
            K("subscriberBoostEnabled", "Abonnenten-Bonus", "Gibt Twitch-Abonnenten eine größere Auszahlung bei Gewinnen und Jackpots. Nur Twitch: auf YouTube und Kick prüft das Spiel keine Abonnenten."),
            K("vipBoostEnabled", "VIP-Bonus", "Gibt Twitch-VIPs eine größere Auszahlung bei Gewinnen und Jackpots. Nur Twitch: auf YouTube und Kick prüft das Spiel keine VIPs."),
            K("subscriberBoostPercent", "Abonnenten-Bonus", "Um wie viel größer der Gewinn eines Abonnenten ist: '10' macht aus 100 110. Addiert sich mit dem VIP-Bonus, wer beides ist."),
            K("vipBoostPercent", "VIP-Bonus", "Um wie viel größer der Gewinn eines VIPs ist: '20' macht aus 100 120. Addiert sich mit dem Abonnenten-Bonus, wer beides ist."),
            K("addictedPayoutCut", "Abzug für Süchtige", "Wie viel vom Gewinn eines süchtigen Zuschauers abgezogen wird, von 0 (nichts) bis 99 - ein Gewinn zahlt immer etwas. Wird zuletzt angewandt, kürzt also auch einen Bonus-Gewinn und den Jackpot-Topf. Wer als süchtig gilt, steht auf der Seite Zugriff."),
            K("useJackpotPool", "Jackpot-Topf", "Jeder verlorene Einsatz geht in einen Topf, einen je Plattform, und ein Jackpot mit groß genugem Einsatz gewinnt ihn obendrauf. Verluste von Süchtigen und freie Drehungen gehen nicht hinein. Nachrichten können, was sie über den Topf sagen, in {jackpot}…{/jackpot} einschließen: mit dem Topf aus bleibt dieser Teil weg."),
            K("jackpotBet", "Einsatz für den Topf", "Der kleinste Einsatz, der mit einem Jackpot den Topf gewinnt. Ein kleinerer Einsatz bekommt trotzdem die Jackpot-Auszahlung, nur nicht den Topf."),
            K("tryPlatform", "Plattform", "Wessen Emotes die Probedrehung nimmt: ein Twitch-Emote gibt es nur auf Twitch, ein Kick-Emote nur auf Kick. Abonnenten- und VIP-Bonus gibt es nur auf Twitch. Wird nicht gespeichert."),
            K("tryBet", "Probeeinsatz", "Ein Einsatz, um Quoten und Nachrichten auszuprobieren. Wird nicht gespeichert."),
            K("tryRole", "Drehen als", "Wer dreht: Boni und der Abzug für Süchtige ändern, was ein Gewinn zahlt."),
            K("trySpin", "Drehen", "Spielt eine Drehung mit den Einstellungen, wie sie auf dieser Seite stehen, gespeichert oder nicht, und zeigt die Walzen und die Nachricht, die der Chat bekäme. Nichts wird gesendet, nichts gespeichert."),
            Tx("Emote, as :name:", "Emote, als :name:"),
            Tx("Emote, as ID:name", "Emote, als ID:name"),
            K("emotesTwitch", "Twitch", "Das Emote genau, wie es im Chat getippt wird, etwa Kappa (Groß- und Kleinschreibung zählt), mit seinem Gewicht: ein niedriges Gewicht kommt oft und zahlt wenig, ein hohes ist selten und zahlt viel. Sein Bild erscheint daneben, sobald Twitch, 7TV, BTTV oder FFZ es kennt. Der Chat zeigt es nur als Emote, wenn der sendende Account es nutzen darf, und ein 7TV-, BTTV- oder FFZ-Emote nur Zuschauern mit der Erweiterung. Welche Gewichte spielen, steht auf der Seite Spiel."),
            K("emotesYoutube", "YouTube", "Eines von YouTubes eigenen Emojis, wie der Chat es tippt, etwa :face-purple-rain-drops:, mit seinem Gewicht; sein Bild erscheint daneben. Ein Unicode-Emoji wie 🍒 geht auch und zeigt sich selbst. Emotes von BTTV oder 7TV sehen nur Zuschauer mit der Erweiterung als Emote."),
            K("emotesKick", "Kick", "ID und Name des Emotes als ID:name, etwa 1730803:emojiRich; das Spiel sendet es als [emote:ID:name], was Kick als Emote zeigt (diese lange Form geht hier auch). Das Bild des Emotes in einem neuen Browser-Tab öffnen: die Zahl in seiner Adresse ist die ID (https://files.kick.com/emotes/1730803/fullsize ist emojiRich)."),
            K("blockedGroups", "Gesperrte Gruppen", "Jeder in diesen Streamer.bot-Gruppen wird ignoriert, wenn er den Befehl ausführt oder die Belohnung einlöst (Settings > Groups). Eine Gruppe, die es nicht gibt, wird übersprungen und protokolliert."),
            K("addictedUsers", "Süchtige Zuschauer", "Diese Zuschauer spielen weiter, aber jeder Gewinn wird um den Abzug für Süchtige von der Seite Spiel gekürzt, ihre Verluste füllen den Jackpot-Topf nicht, und die Abklingzeit je Zuschauer hält sie auch bei Kanalpunkt-Einlösungen."),
            K("addictedGroups", "Süchtige Gruppen", "Jeder in diesen Streamer.bot-Gruppen gilt als süchtig."),
            K("isChannelPointsTrigger", "Kanalpunkte starten eine Drehung", "Auf Twitch dreht eine Einlösung der Belohnung darunter statt des Chat-Befehls, der dann auf Twitch aus ist. Die Slot Machine nimmt Einlösungen nur von Twitch, auf YouTube und Kick läuft der Befehl also weiter."),
            K("rewardId", "Belohnungs-ID", "Die ID der Kanalpunkt-Belohnung, die eine Drehung startet. Andere Belohnungen werden ignoriert; ohne eine startet keine Einlösung eine Drehung. https://ayrtonalexis.com/reward_checker/reward_check.html listet deine Belohnungen mit ihren IDs."),
            K("cpBetMode", "Was gesetzt wird", "Preis der Belohnung: der Preis ist der Einsatz, bezahlt in Kanalpunkten; ein Verlust rührt weder die Punkte des Zuschauers noch den Jackpot-Topf an, ein Gewinn wird in Punkten gezahlt. Eingegebener Betrag: Zuschauer tippen einen Einsatz in eigenen Punkten in die Belohnung, geprüft gegen ihr Guthaben wie eine Chat-Drehung; reicht es nicht, wird die Einlösung erstattet. Fest: jede Einlösung setzt den festen Betrag darunter, Preis und Text werden ignoriert, die Kanalpunkte sind der Einsatz. Frei: nichts wird gewonnen oder verloren und die Nachrichten unter Freier Modus werden genutzt."),
            K("fixedBetAmount", "Fester Einsatz", "Was jede Einlösung im Modus Fest setzt. Muss über 0 liegen."),
            K("refundFailedRedemptions", "Einlösungen erstatten, die keine Drehung ergeben", "Wenn eine Einlösung keine Drehung ergeben kann - eine gesperrte Gruppe, ein Einsatz, der nicht geht, ein Süchtiger noch in der Abklingzeit, ein Guthaben, das nicht lesbar ist, oder keine Emotes - wird sie abgebrochen und die Kanalpunkte gehen zurück. Das geht nur, solange die Einlösung noch in der Warteschlange steht: eine Belohnung, die die Warteschlange überspringt, lässt sich nicht erstatten."),
            K("globalCooldown", "Alle", "Wie lange nach einer Drehung niemand drehen kann, in Sekunden. 0 schaltet sie ab. Speichern schreibt sie auf den Chat-Befehl in Streamer.bot, Kanalpunkt-Einlösungen warten also nicht darauf."),
            K("userCooldown", "Je Zuschauer", "Wie lange ein Zuschauer bis zur nächsten Drehung wartet, in Sekunden. 0 schaltet sie ab. Speichern schreibt sie auf den Chat-Befehl in Streamer.bot, Kanalpunkt-Einlösungen warten also nicht darauf - außer süchtige Zuschauer, die das Spiel selbst daran hält."),
            K("logToFile", "Eine Protokolldatei pro Tag schreiben", "Schreibt jede Drehung - Gewinn, Verlust, Jackpot - mit Einsatz und Guthaben in eine datierte 'SMG-log-yyyy-MM-dd.txt' im Ordner Logs, eine Datei pro Tag."),
            K("winMessage", "Gewinn", "Nach den Walzen gesendet bei einem Gewinn, der kein Jackpot ist. Die Walzen selbst gehen kurz davor als eigene Nachricht raus."),
            K("lossMessage", "Verlust", "Nach den Walzen gesendet bei einem Verlust. Mit dem Jackpot-Topf an geht der verlorene Einsatz hinein."),
            K("jackpotMessage", "Jackpot", "Nach den Walzen gesendet, wenn alle gleich sind."),
            K("freeGameWinMessage", "Gewinn", "Nach den Walzen gesendet bei einem Gewinn, solange der freie Modus an ist."),
            K("freeGameLossMessage", "Verlust", "Nach den Walzen gesendet bei einem Verlust, solange der freie Modus an ist. Nichts wird abgezogen."),
            K("freeGameJackpotMessage", "Jackpot", "Nach den Walzen gesendet bei einem Jackpot, solange der freie Modus an ist."),
            K("freeGameStartMessage", "Gestartet", "Einmal gesendet, wenn !SetFreeSlots den freien Modus einschaltet, in den Chat, aus dem der Befehl kam. Anders eingeschaltet, etwa per Execute C# Method auf StartFreeGame in einer eigenen Action, geht sie an jede Plattform mit einem Konto in Streamer.bot - an YouTube nur, solange eine Übertragung beobachtet wird."),
            K("freeGameEndMessage", "Beendet", "Einmal gesendet, wenn !EndFreeSlots den freien Modus ausschaltet, in den Chat, aus dem der Befehl kam. Anders ausgeschaltet, etwa per Execute C# Method auf EndFreeGame, geht sie an jede Plattform mit einem Konto in Streamer.bot - an YouTube nur, solange eine Übertragung beobachtet wird."),
            K("addictedWinMessage", "Gewinn", "Statt der Gewinn-Nachricht an Zuschauer auf der Süchtigen-Liste gesendet."),
            K("addictedLossMessage", "Verlust", "Statt der Verlust-Nachricht an Zuschauer auf der Süchtigen-Liste gesendet. Ihre Verluste gehen nicht in den Topf."),
            K("addictedJackpotMessage", "Jackpot", "Statt der Jackpot-Nachricht an Zuschauer auf der Süchtigen-Liste gesendet."),
            K("invalidBetMessage", "Einsatz abgelehnt", "Der Rahmen um die vier Gründe darunter; {reason} wird durch den zutreffenden ersetzt."),
            K("reasonNegativeAmount", "Grund: kein gültiger Betrag", "Füllt {reason}, wenn der Betrag negativ, keine Zahl, mit Dezimalstellen oder zu groß ist."),
            K("reasonInsufficientFunds", "Grund: nicht genug Punkte", "Füllt {reason}, wenn der Einsatz mehr ist, als der Zuschauer hat."),
            K("reasonBelowMinimum", "Grund: unter dem Minimum", "Füllt {reason}, wenn der Einsatz unter dem Minimum auf der Seite Spiel liegt."),
            K("reasonAboveMaximum", "Grund: über dem Maximum", "Füllt {reason}, wenn der Einsatz über dem Maximum auf der Seite Spiel liegt."),
            K("globalCooldownMessage", "Abklingzeit für alle", "Gesendet, solange die Abklingzeit für alle läuft."),
            K("userCooldownMessage", "Abklingzeit eines Zuschauers", "Gesendet, solange die eigene Abklingzeit dieses Zuschauers läuft."),
            K("redemptionRefundedMessage", "Erstattet", "Gesendet, wenn eine Einlösung erstattet wird."),
            Tx("Slots and odds", "Walzen und Quoten"),
            Tx("Bets and points", "Einsätze und Punkte"),
            Tx("Free mode", "Freier Modus"),
            Tx("Results", "Ergebnisse"),
            Tx("Viewer", "Zuschauer"),
            Tx("Subscriber", "Abonnent"),
            Tx("Reward cost", "Preis der Belohnung"),
            Tx("Typed amount", "Eingegebener Betrag"),
            Tx("Fixed", "Fest"),
            Tx("Free", "Frei"),
            Tx("Free mode, from the Game page or !SetFreeSlots. While it is on, nobody stakes or wins anything and the jackpot pool doesn't grow.", "Freier Modus, von der Seite Spiel oder !SetFreeSlots. Solange er an ist, setzt und gewinnt niemand etwas, und der Jackpot-Topf wächst nicht."),
            Tx("How viewers spin on Twitch, from the Access page. YouTube and Kick always use the chat command.", "Wie Zuschauer auf Twitch drehen, von der Seite Zugriff. YouTube und Kick nutzen immer den Chat-Befehl."))
        .Pill("freeGame", "Free mode on", "Free mode off", tooltip: "Free mode, from the Game page or !SetFreeSlots. While it is on, nobody stakes or wins anything and the jackpot pool doesn't grow.")
        .Pill("isChannelPointsTrigger", "Channel points", "Chat command", tooltip: "How viewers spin on Twitch, from the Access page. YouTube and Kick always use the chat command.")
        .Samples("user", "alice", "amount", "1,500", "currency", "Points", "newBalance", "12,345", "jackpotMinBet", "50,000", "jackpotLossContribution", "100", "jackpotPoolCurrent", "23,400", "jackpotPoolWinAmount", "23,400", "addictedCutPercent", "50", "jackpot", "", "/jackpot", "", "reason", "your bet is below the minimum (1 Points).", "seconds", "12", "min", "1", "max", "9,999,999")
        .Answer("trySpin", "{\"lines\":[\"alice has 12,340 Points and spins for 1,000 Points on Twitch.\",{\"emotes\":[\"Kappa\",\"LUL\",\"Kappa\",\"FailFish\"],\"platform\":\"twitch\"},\"No match · @alice spun Kappa || LUL || Kappa || FailFish and lost 1,000 Points. New balance: 11,340 Points.\"]}")
        .Log("Chat Games/Logs", "SMG-log*.txt", writeLogKey: "logToFile", columns: new[] { "Player|User", "Platform|Platform", "Trigger|Trigger", "Bet|Bet", "Change|Change", "After|After" }, results: new[] { "WIN", "LOSS", "JACKPOT" }, platforms: new[] { "twitch", "youtube", "kick" }, emptyText: "No spins logged yet. Every spin lands here once 'Write a log file per day' is on.")
        .Help(
            H("A spin",
                Note("A viewer runs the slots command with a bet. The bet is taken, the reels roll, and the game settles on the spot: two or more matching slots win, all of them alike is the jackpot, anything else loses the bet. The slots go out as one message and the result as the next."),
                Cmd("!slots", "spins at the default bet."),
                Cmd("!slots 500", "spins for 500, within the minimum and maximum."),
                Cmd("!slots all", "also half, third, quarter and random."),
                Cmd("!SetFreeSlots", "moderators: turns Free mode on, every spin is free. !EndFreeSlots turns it off."),
                Note("A trigger of your own can pass the argument free=1 for a single free spin; Free mode on the Game page makes every spin free.")),
            H("What a win pays",
                Note("The bet × the payout % of the tier (2, 3, 4 or 5 alike, or the jackpot) × the emote's weight ÷ 100, and never less than the bet. A low weight comes up often and pays little, a high one is rare and pays a lot; which weights play is set on the Game page, the weights themselves on the Emotes page."),
                Note("With the jackpot pool on, every lost bet goes into a pool per platform, and a jackpot with a bet at or above 'Bet to win the pool' takes the pool on top. Twitch subscribers and VIPs can get a boost; viewers on the Addicted list get a cut."),
                Note("'Try a spin' on the Game page plays one round with the game's own engine and the exact chat message, from the settings as they stand in the window, without sending or saving anything.")),
            H("Channel points",
                Note("On Twitch a channel point reward can start the spin instead of the command (Access page): the reward's price is the bet, or a typed amount, a fixed amount, or nothing at all. YouTube and Kick keep the chat command. A redemption that can't end in a spin is refunded while it is still in the request queue.")),
            H("For your own actions",
                Note("Every settled spin fires a custom trigger - SMG: Spin Win, SMG: Spin Loss or SMG: Spin Jackpot, and SMG: Spin Free on top when the spin was free (Custom > Chat Game > Slot Machine Game) - with the spin as arguments: spinOutcome, spinUser, spinPlatform, spinSlots, spinBet, spinAmount, spinPreviousPoints, spinNewPoints, spinCurrency, spinJackpotPoolWon and spinFree. GetLastSpinResult, run with an Execute C# Method sub-action on the game action, hands the same arguments to any other action. SMG: Free Game Start and SMG: Free Game End fire when Free mode is switched from chat or by an Execute C# Method on StartFreeGame or EndFreeGame."),
                Note("Every spin also updates the viewer's own variables for a leaderboard of your own, on the platform the spin was made (Streamer.bot's user variables, Twitch, YouTube and Kick): smg_spins, smg_wins, smg_losses, smg_jackpots, smg_totalBet, smg_totalWon, smg_totalLost, smg_netProfit, smg_biggestWin, smg_biggestBet, smg_poolWon, smg_currentStreak, smg_bestStreak, smg_userName, smg_firstPlayed and smg_lastPlayed. Free spins count as spins, wins and jackpots but move no points.")),
            H("Where things are kept",
                Cmd("SMG-Settings.json", "everything on these pages, the emotes included."),
                Cmd("uiframework.layout.json", "your layout of this window: theme, renamed or hidden pages and fields."),
                Cmd("Logs\\SMG-log-yyyy-MM-dd.txt", "every spin, one file per day, while 'Write a log file per day' is on."),
                Note("All of it sits in Streamer.bot's folder under Chat Games. A damaged settings file falls back to the .bak beside it and the broken one is kept as .broken.")))
        .About(credits: "Slot Machine Game by aaskjer / yBO",
            sections: new[]
            {
                H("Needs a points system",
                    Note("The slot machine plays with the points of a currency system - channel point spins too, unless they're free. These two are found on their own:"),
                    Link("Currency Core by StreamUP", "https://streamup.tips/product/currency-core"),
                    Link("LOYALTY POINTS by tawmae", "https://tawmae.xyz/loyalty-points"))
            }, links: new[] { Link("Slot Machine Game on the Streamer.bot Discord", "https://discord.com/channels/834650675224248362/1446889756297527530/1446889756297527530") })
        // The tour on the window's first open (the ? in the header shows it again); the framework brings the texts.
        .Tour(Tip("start"), Tip("rail"), Tip("save"), Tip("customise"), Tip("end"));

    // ---- The table: every setting the host reads, in page order. ----
    public static readonly Setting[] Table =
    {
        // ---- (conventions) ----
        S("useBot", true, aliases: new[] { "UseBot" }),
        S("sendAction", false, aliases: new[] { "SendAction" }),
        S("cooldownUseBot", true, aliases: new[] { "UseBotCD" }),
        S("cooldownSendAction", false, aliases: new[] { "SendActionCD" }),
        S("numberSeparator", ".", aliases: new[] { "NumberSeparator" }),
        // ---- game ----
        S("numberOfSlots", 4, page: "game", label: "Slots", help: "How many slots a spin shows. All of them alike is the jackpot.", section: "Slots and odds", options: new[] { "3", "4", "5", "6" }, aliases: new[] { "NumberOfSlots" }),
        S("matchChance", 18, page: "game", label: "Match chance", help: "Chance in percent that a spin may land matching slots: they're then drawn freely, so a match is likely but not certain. The rest of the time a repeat is rerolled to avoid a match. The odds below show what really comes out.", kind: "percent", section: "Slots and odds", min: 0.0, max: 100.0, aliases: new[] { "AllowMatchChance" }, aliasFactor: 100.0),
        S("jackpotChance", 1, page: "game", label: "Jackpot chance", help: "Chance in percent that every slot shows the same emote - the jackpot. '1' is 1 in 100. A free draw can land all alike too, so the real chance is a little higher.", kind: "percent", section: "Slots and odds", min: 0.0, max: 100.0, aliases: new[] { "AllowJackpotChance" }, aliasFactor: 100.0),
        S("rerollAttempts", 2, page: "game", label: "Rerolls", help: "How hard a spin that isn't meant to match avoids a repeat: each slot is drawn up to this many times. A repeat can still slip through, most of all with few emotes in play.", section: "Slots and odds", min: 0.0, aliases: new[] { "RerollAttempts" }),
        S("emotePoolSize", 10, page: "game", label: "Emote pool (0 = all)", help: "How many of the emotes in play are drawn for each spin. Fewer emotes make matches more likely. 0, or more than there are, uses them all.", section: "Slots and odds", min: 0.0, aliases: new[] { "EmotePoolSize" }),
        S("minEmoteWeight", 1, page: "game", label: "Min weight", help: "Only emotes with at least this weight play. Low weights come up often and pay little, so raising this takes out the cheapest emotes.", section: "Slots and odds", min: 1.0, aliases: new[] { "MinEmoteWeight" }),
        S("maxEmoteWeight", 10, page: "game", label: "Max weight", help: "Only emotes with at most this weight play. High weights are rare and pay a lot, so lowering this takes out the most valuable emotes.", section: "Slots and odds", min: 1.0, aliases: new[] { "MaxEmoteWeight" }),
        S("defaultBet", 1234, page: "game", label: "Default bet", help: "The bet for a spin with no amount. Viewers can also bet a number, all, half, third, quarter or random.", section: "Bets and points", min: 0.0, aliases: new[] { "DefaultBet" }),
        S("minBet", 1, page: "game", label: "Minimum (0 = none)", help: "The smallest bet allowed. 0 turns the limit off. 'random' bets between Minimum and Maximum. Also applies to channel points in 'Typed amount' and 'Fixed' mode.", section: "Bets and points", min: 0.0, aliases: new[] { "MinBet" }),
        S("maxBet", 9999999, page: "game", label: "Maximum (0 = none)", help: "The largest bet allowed. 0 turns the limit off. Also applies to channel points in 'Typed amount' and 'Fixed' mode.", section: "Bets and points", min: 0.0, aliases: new[] { "MaxBet" }),
        S("freeGame", false, page: "game", label: "Free mode", help: "Every spin is free: nobody pays or wins points, the jackpot pool doesn't grow, and chat gets the Free mode messages. !SetFreeSlots and !EndFreeSlots switch it from chat (moderators). For a single free spin, a trigger of your own can pass the argument free=1 instead.", section: "Bets and points", aliases: new[] { "IsFree" }),
        S("pointsVariable", "points", page: "game", label: "Points variable", help: "The variable your points live in. Currency Core by StreamUP and LOYALTY POINTS by tawmae are found on their own; only change this for a points system of your own.", section: "Bets and points", aliases: new[] { "PointsVar" }),
        S("pointsName", "Points", page: "game", label: "Shown in chat as", help: "The name {currency} puts in chat, like 'Coins' or 'Gems'. Currency Core and LOYALTY POINTS bring their own name, which is used instead.", section: "Bets and points", aliases: new[] { "PointsName" }),
        S("twoMatchPayout", 150.0, page: "game", label: "2 alike", help: "A win pays the bet × payout % × the emote's weight ÷ 100, and never less than the bet. All slots alike is the jackpot, which pays the Jackpot tier whatever the number of slots.", kind: "double", section: "Payouts", min: 0.0, unit: "%", aliases: new[] { "TwoMatchPayout" }),
        S("threeMatchPayout", 300.0, page: "game", label: "3 alike", help: "With 3 slots, three alike is the jackpot and this tier is not used.", kind: "double", section: "Payouts", min: 0.0, unit: "%", requires: "numberOfSlots>=4", aliases: new[] { "ThreeMatchPayout" }),
        S("fourMatchPayout", 500.0, page: "game", label: "4 alike", help: "Only with 5 or 6 slots; fewer slots never land four alike short of the jackpot.", kind: "double", section: "Payouts", min: 0.0, unit: "%", requires: "numberOfSlots>=5", aliases: new[] { "FourMatchPayout" }),
        S("fiveMatchPayout", 1000.0, page: "game", label: "5 alike", help: "Only with 6 slots.", kind: "double", section: "Payouts", min: 0.0, unit: "%", requires: "numberOfSlots>=6", aliases: new[] { "FiveMatchPayout" }),
        S("sixMatchPayout", 1500.0, page: "game", label: "Jackpot", help: "All slots alike, whatever their number: 3 of 3 as much as 6 of 6.", kind: "double", section: "Payouts", min: 0.0, unit: "%", aliases: new[] { "SixMatchPayout" }),
        S("subscriberBoostEnabled", false, page: "game", label: "Subscriber boost", help: "Gives Twitch subscribers a bigger payout on wins and jackpots. Twitch only: on YouTube and Kick the game doesn't check for subscribers.", section: "Payouts", aliases: new[] { "SubscriberBoostEnabled" }, twitchOnly: true),
        S("vipBoostEnabled", false, page: "game", label: "VIP boost", help: "Gives Twitch VIPs a bigger payout on wins and jackpots. Twitch only: on YouTube and Kick the game doesn't check for VIPs.", section: "Payouts", aliases: new[] { "VipBoostEnabled" }, twitchOnly: true),
        S("subscriberBoostPercent", 10.0, page: "game", label: "Subscriber boost", help: "How much bigger a subscriber's win is: '10' turns 100 into 110. Adds up with the VIP boost for someone who is both.", kind: "double", section: "Payouts", min: 0.0, unit: "%", requires: "subscriberBoostEnabled", aliases: new[] { "SubscriberBoostPercent" }),
        S("vipBoostPercent", 20.0, page: "game", label: "VIP boost", help: "How much bigger a VIP's win is: '20' turns 100 into 120. Adds up with the Subscriber boost for someone who is both.", kind: "double", section: "Payouts", min: 0.0, unit: "%", requires: "vipBoostEnabled", aliases: new[] { "VipBoostPercent" }),
        S("addictedPayoutCut", 50, page: "game", label: "Addicted cut", help: "How much of an addicted viewer's win is cut, from 0 (nothing) to 99 - a win always pays something. Applied last, so it cuts a boosted win and the jackpot pool too. Who counts as addicted is set on the Access page.", kind: "percent", section: "Payouts", min: 0.0, max: 99.0, aliases: new[] { "AddictedPayoutCut" }, aliasFactor: 100.0),
        S("useJackpotPool", true, page: "game", label: "Jackpot pool", help: "Every lost bet goes into a pool, one per platform, and a jackpot with a big enough bet wins it all on top. Losses of addicted viewers and free spins don't go in. Messages can wrap what they say about the pool in {jackpot}…{/jackpot}: with the pool off, that part is left out.", section: "Jackpot pool", aliases: new[] { "UseJackpotPool" }),
        S("jackpotBet", 50000, page: "game", label: "Bet to win the pool", help: "The smallest bet that wins the pool with a jackpot. A smaller bet still gets the jackpot payout, just not the pool.", section: "Jackpot pool", min: 0.0, aliases: new[] { "JackpotBet" }),
        S("tryPlatform", "twitch", page: "game", label: "Platform", help: "Whose emotes the test spin uses: a Twitch emote only exists on Twitch, a Kick emote only on Kick. Subscriber and VIP boosts only exist on Twitch. Not saved.", kind: "options", section: "Try a spin", options: new[] { "twitch|Twitch", "youtube|YouTube", "kick|Kick" }, transient: true, row: "try"),
        S("tryBet", 1000, page: "game", label: "Bet to try", help: "A bet to try the odds and messages with. It isn't saved.", section: "Try a spin", min: 1.0, transient: true, row: "try"),
        S("tryRole", "viewer", page: "game", label: "Spin as", help: "Who spins: boosts and the Addicted cut change what a win pays.", kind: "options", section: "Try a spin", options: new[] { "viewer|Viewer", "subscriber|Subscriber", "vip|VIP", "addicted|Addicted" }, transient: true, row: "try"),
        S("trySpin", null, page: "game", label: "Spin", help: "Plays one spin with the settings as they stand on this page, saved or not, and shows the slots and the message chat would get. Nothing is sent and nothing is saved.", kind: "action", section: "Try a spin", live: true, panel: true),
        // ---- emotes ----
        S("emotesTwitch", JArray.Parse("[{\"Code\":\"Kappa\",\"Weight\":1},{\"Code\":\"PogChamp\",\"Weight\":2},{\"Code\":\"LUL\",\"Weight\":3},{\"Code\":\"NotLikeThis\",\"Weight\":4},{\"Code\":\"BigPhish\",\"Weight\":5},{\"Code\":\"RitzMitz\",\"Weight\":6},{\"Code\":\"FrankerZ\",\"Weight\":7},{\"Code\":\"SeemsGood\",\"Weight\":8},{\"Code\":\"Kreygasm\",\"Weight\":9},{\"Code\":\"FailFish\",\"Weight\":10}]"), page: "emotes", label: "Twitch", help: "The emote exactly as it's typed in chat, like Kappa (names are case-sensitive), with its weight: a low weight comes up often and pays little, a high one is rare and pays a lot. Its picture appears beside it once it is known to Twitch, 7TV, BTTV or FFZ. Chat only shows it as an emote if the account sending the messages can use it, and a 7TV, BTTV or FFZ one only to viewers with that extension. Which weights play is set on the Game page.", kind: "list", section: "Twitch", columns: new[] { "Code|emote|Emote|twitch", "Weight|int|Weight" }, aliases: new[] { "PlatformEmotes.twitch" }),
        S("emotesYoutube", JArray.Parse("[{\"Code\":\":face-blue-smiling:\",\"Weight\":1},{\"Code\":\":hand-pink-waving:\",\"Weight\":2},{\"Code\":\":face-green-smiling:\",\"Weight\":3},{\"Code\":\":face-orange-raised-eyebrow:\",\"Weight\":4},{\"Code\":\":face-purple-rain-drops:\",\"Weight\":5},{\"Code\":\":face-fuchsia-tongue-out:\",\"Weight\":6},{\"Code\":\":face-blue-heart-eyes:\",\"Weight\":7},{\"Code\":\":face-red-droopy-eyes:\",\"Weight\":8},{\"Code\":\":text-green-game-over:\",\"Weight\":9},{\"Code\":\":yt:\",\"Weight\":10}]"), page: "emotes", label: "YouTube", help: "One of YouTube's own emojis as chat types it, like :face-purple-rain-drops:, with its weight; its picture appears beside it. A Unicode emoji like 🍒 works too and shows itself. Emotes of BTTV or 7TV only show as emotes to viewers with that extension.", kind: "list", section: "YouTube", columns: new[] { "Code|emote|Emote, as :name:|youtube", "Weight|int|Weight" }, aliases: new[] { "PlatformEmotes.youtube" }),
        S("emotesKick", JArray.Parse("[{\"Code\":\"1730803:emojiRich\",\"Weight\":1},{\"Code\":\"1579059:emojiSwearing\",\"Weight\":2},{\"Code\":\"3419630:emojiTire\",\"Weight\":3},{\"Code\":\"1730772:emojiFire\",\"Weight\":4},{\"Code\":\"1730758:emojiClown\",\"Weight\":5},{\"Code\":\"1730752:emojiAngel\",\"Weight\":6},{\"Code\":\"1730790:emojiKiss\",\"Weight\":7},{\"Code\":\"1730753:emojiAngry\",\"Weight\":8},{\"Code\":\"1730807:emojiShocked\",\"Weight\":9},{\"Code\":\"1730792:emojiLoading\",\"Weight\":10}]"), page: "emotes", label: "Kick", help: "The emote's ID and name as ID:name, like 1730803:emojiRich; the game sends it as [emote:ID:name], which Kick shows as the emote (that full form works here too). Open the emote's picture in a new browser tab: the number in its address is the ID (https://files.kick.com/emotes/1730803/fullsize is emojiRich).", kind: "list", section: "Kick", columns: new[] { "Code|emote|Emote, as ID:name|kick", "Weight|int|Weight" }, aliases: new[] { "PlatformEmotes.kick" }),
        // ---- access ----
        S("blockedGroups", "", page: "access", label: "Blocked groups", help: "Everyone in these Streamer.bot groups is ignored when they run the command or redeem the reward (Settings > Groups). A group that does not exist is skipped and logged.", section: "Who may not play", aliases: new[] { "BlockedGroups" }),
        S("addictedUsers", "", page: "access", label: "Addicted viewers", help: "These viewers still play, but every win is cut by the Addicted cut on the Game page, their losses don't feed the jackpot pool, and the per-viewer cooldown holds them on channel point redemptions too.", section: "Addicted", aliases: new[] { "AddictedUsers" }),
        S("addictedGroups", "", page: "access", label: "Addicted groups", help: "Everyone in these Streamer.bot groups counts as addicted.", section: "Addicted", aliases: new[] { "AddictedGroups" }),
        S("isChannelPointsTrigger", false, page: "access", label: "Channel points start a spin", help: "On Twitch, a redemption of the reward below spins instead of the chat command, which is then off on Twitch. The slot machine takes redemptions from Twitch only, so on YouTube and Kick the command keeps working.", section: "Channel points (Twitch)", aliases: new[] { "IsChannelPointsTrigger" }, twitchOnly: true),
        S("rewardId", "", page: "access", label: "Reward ID", help: "The ID of the channel point reward that starts a spin. Other rewards are ignored; without one, no redemption starts a spin. https://ayrtonalexis.com/reward_checker/reward_check.html lists your rewards with their IDs.", section: "Channel points (Twitch)", requires: "isChannelPointsTrigger", aliases: new[] { "TargetRewardId" }),
        S("cpBetMode", "cost", page: "access", label: "What is bet", help: "Reward cost: the reward's price is the bet, paid in channel points; a loss doesn't touch the viewer's points or the jackpot pool, a win is paid in points. Typed amount: viewers type a bet in their own points into the reward, checked against their balance like a chat spin; not enough, and the redemption is refunded. Fixed: every redemption bets the fixed amount below, the price and the text are ignored, the channel points are the stake. Free: nothing is won or lost and the Free messages are used.", kind: "options", section: "Channel points (Twitch)", options: new[] { "cost|Reward cost", "input|Typed amount", "fixed|Fixed", "free|Free" }, requires: "isChannelPointsTrigger", aliases: new[] { "CPBetMode" }),
        S("fixedBetAmount", 0, page: "access", label: "Fixed bet", help: "What every redemption bets in Fixed mode. Must be more than 0.", section: "Channel points (Twitch)", min: 0.0, requires: "cpBetMode=fixed", aliases: new[] { "FixedBetAmount" }),
        S("refundFailedRedemptions", true, page: "access", label: "Refund redemptions that don't end in a spin", help: "When a redemption can't end in a spin - a blocked group, a bet that doesn't work, an addicted viewer still on cooldown, a balance that can't be read or no emotes - it's cancelled and the channel points go back. That only works while the redemption is still in the request queue: a reward that skips the queue can't be refunded.", section: "Channel points (Twitch)", requires: "isChannelPointsTrigger", aliases: new[] { "RefundFailedRedemptions" }),
        S("globalCooldown", 0, page: "access", label: "Everyone", help: "How long nobody can spin after any spin, in seconds. 0 turns it off. Saving writes it onto the chat command in Streamer.bot, so channel point redemptions don't wait for it.", section: "Cooldowns", min: 0.0, aliases: new[] { "CooldownGlobalSeconds" }),
        S("userCooldown", 0, page: "access", label: "Per viewer", help: "How long one viewer waits before their next spin, in seconds. 0 turns it off. Saving writes it onto the chat command in Streamer.bot, so channel point redemptions don't wait for it - except for addicted viewers, whom the game holds to it itself.", section: "Cooldowns", min: 0.0, aliases: new[] { "CooldownUserSeconds" }),
        // ---- log ----
        S("logToFile", true, page: "log", label: "Write a log file per day", help: "Writes every spin - win, loss, jackpot - with the bet and the balance to a dated 'SMG-log-yyyy-MM-dd.txt' in the Logs folder, one file per day.", aliases: new[] { "LogToFile" }),
        // ---- messages ----
        Msg("Results", "winMessage", "@{user}, you WON! {amount} {currency}! 🎉🎉{jackpot} (Min. bet for the Jackpool: {jackpotMinBet} {currency}){/jackpot} -- Your new balance: {newBalance} {currency}", "Win", "Sent after the slots on a win that isn't the jackpot. The slots themselves go out as a message of their own just before.", new[] { "user", "amount", "currency", "newBalance", "jackpotMinBet", "jackpot", "/jackpot" }, alias: "WinMessage"),
        Msg("Results", "lossMessage", "@{user}, you LOST! Better luck next time!{jackpot} 💸 {jackpotLossContribution} {currency} got added to the Jackpool of now {jackpotPoolCurrent} {currency}!{/jackpot} -- Your new balance: {newBalance} {currency}", "Loss", "Sent after the slots on a loss. With the jackpot pool on, the lost bet goes into it.", new[] { "user", "amount", "currency", "newBalance", "jackpotLossContribution", "jackpotPoolCurrent", "jackpot", "/jackpot" }, alias: "LossMessage"),
        Msg("Results", "jackpotMessage", "🔥 J A C K P O T 🔥 @{user}, YOU WON {amount} {currency}{jackpot} + {jackpotPoolWinAmount} {currency} from the Jackpool!{/jackpot} -- Your new balance: {newBalance} {currency}", "Jackpot", "Sent after the slots when all of them are alike.", new[] { "user", "amount", "currency", "newBalance", "jackpotPoolWinAmount", "jackpot", "/jackpot" }, alias: "JackpotMessage"),
        Msg("Free mode", "freeGameWinMessage", "🎉 @{user}, you WON! Lucky spin!", "Win", "Sent after the slots on a win while Free mode is on.", new[] { "user" }, alias: "FreeGameWinMessage"),
        Msg("Free mode", "freeGameLossMessage", "💸 @{user}, unlucky! Better luck next spin!", "Loss", "Sent after the slots on a loss while Free mode is on. Nothing is taken.", new[] { "user" }, alias: "FreeGameLossMessage"),
        Msg("Free mode", "freeGameJackpotMessage", "🔥 J A C K P O T 🔥 @{user}, incredible! You hit the jackpot on a free spin!", "Jackpot", "Sent after the slots on a jackpot while Free mode is on.", new[] { "user" }, alias: "FreeGameJackpotMessage"),
        Msg("Free mode", "freeGameStartMessage", "🎰 Free mode is ON! Spin for free - no {currency} needed!", "Started", "Sent once when Free mode is turned on by !SetFreeSlots, in the chat the command came from. Turned on another way, say by an Execute C# Method on StartFreeGame in an action of your own, it goes to every platform with an account in Streamer.bot - to YouTube only while a broadcast is monitored.", new[] { "currency" }, alias: "FreeGameStartMessage"),
        Msg("Free mode", "freeGameEndMessage", "⏹️ Free mode has ended. Spins cost {currency} again!", "Ended", "Sent once when Free mode is turned off by !EndFreeSlots, in the chat the command came from. Turned off another way, say by an Execute C# Method on EndFreeGame, it goes to every platform with an account in Streamer.bot - to YouTube only while a broadcast is monitored.", new[] { "currency" }, alias: "FreeGameEndMessage"),
        Msg("Addicted", "addictedWinMessage", "@{user}, you WON! {amount} {currency}! 🎉🎉 -- Addiction Fee: your win was cut by {addictedCutPercent}% -- Your new balance: {newBalance} {currency}", "Win", "Sent instead of the Win message to viewers on the Addicted list.", new[] { "user", "amount", "currency", "newBalance", "addictedCutPercent" }, alias: "AddictedWinMessage"),
        Msg("Addicted", "addictedLossMessage", "@{user}, you LOST! Better luck next time! 💸 -- Your new balance: {newBalance} {currency}", "Loss", "Sent instead of the Loss message to viewers on the Addicted list. Their losses don't go into the pool.", new[] { "user", "amount", "currency", "newBalance" }, alias: "AddictedLossMessage"),
        Msg("Addicted", "addictedJackpotMessage", "🔥 J A C K P O T 🔥 @{user}, YOU WON {amount} {currency}{jackpot} + {jackpotPoolWinAmount} {currency} from the Jackpool!{/jackpot} -- Addiction Fee: your win was cut by {addictedCutPercent}% -- Your new balance: {newBalance} {currency}", "Jackpot", "Sent instead of the Jackpot message to viewers on the Addicted list.", new[] { "user", "amount", "currency", "newBalance", "jackpotPoolWinAmount", "addictedCutPercent", "jackpot", "/jackpot" }, alias: "AddictedJackpotMessage"),
        Msg("Bets", "invalidBetMessage", "❌ @{user}, {reason}", "Bet refused", "The frame around the four reasons below; {reason} is replaced by whichever one applies.", new[] { "user", "reason", "currency" }, alias: "InvalidBetMessage"),
        Msg("Bets", "reasonNegativeAmount", "no decimals, negative, invalid or too large amounts.", "Reason: not a valid amount", "Fills {reason} when the amount is negative, not a number, has decimals, or is too large.", new[] { "currency" }, alias: "ReasonNegativeAmount"),
        Msg("Bets", "reasonInsufficientFunds", "you don't have enough {currency}.", "Reason: not enough points", "Fills {reason} when the bet is more than the viewer has.", new[] { "currency" }, alias: "ReasonInsufficientFunds"),
        Msg("Bets", "reasonBelowMinimum", "your bet is below the minimum ({min} {currency}).", "Reason: below the minimum", "Fills {reason} when the bet is below the minimum on the Game page.", new[] { "min", "currency" }, alias: "ReasonBelowMinimum"),
        Msg("Bets", "reasonAboveMaximum", "your bet is above the maximum ({max} {currency}).", "Reason: above the maximum", "Fills {reason} when the bet is above the maximum on the Game page.", new[] { "max", "currency" }, alias: "ReasonAboveMaximum"),
        Msg("Cooldown", "globalCooldownMessage", "Please wait '{seconds}' seconds before trying again ⏳", "Everyone's cooldown", "Sent while the cooldown for everyone runs.", new[] { "seconds", "user" }, alias: "GlobalMessage"),
        Msg("Cooldown", "userCooldownMessage", "@{user}, please wait '{seconds}' seconds before trying again ⏳", "A viewer's cooldown", "Sent while this viewer's own cooldown runs.", new[] { "user", "seconds" }, alias: "UserMessage"),
        Msg("Channel points", "redemptionRefundedMessage", "@{user}, your Channel Points were refunded since the redemption couldn't be completed.", "Refunded", "Sent when a redemption is refunded.", new[] { "user" }, alias: "RedemptionRefundedMessage"),
    };
}

/// <summary>How the window is called and laid out, declared beside the table: what the framework's docs call the
/// spec (docs/Schema.md, "The window"). Every part is optional; a page a setting names but the window does not is
/// made up on the way, help and about always sit at the bottom.</summary>
public sealed class UiWindow
{
    public string Title, Releases, Logo, LogPrefix, Language;
    public bool Locked;
    public double? Width, Height;
    public readonly System.Collections.Generic.List<object> PageList = new System.Collections.Generic.List<object>();
    public readonly Newtonsoft.Json.Linq.JObject SampleMap = new Newtonsoft.Json.Linq.JObject(), AnswerMap = new Newtonsoft.Json.Linq.JObject(), OverrideMap = new Newtonsoft.Json.Linq.JObject();
    public Newtonsoft.Json.Linq.JObject SenderMap, LogMap, AboutMap;
    public readonly Newtonsoft.Json.Linq.JArray PillList = new Newtonsoft.Json.Linq.JArray(), CooldownList = new Newtonsoft.Json.Linq.JArray(), HelpList = new Newtonsoft.Json.Linq.JArray(), SectionList = new Newtonsoft.Json.Linq.JArray();
    public readonly Newtonsoft.Json.Linq.JObject LangMap = new Newtonsoft.Json.Linq.JObject();
    public readonly Newtonsoft.Json.Linq.JArray TourList = new Newtonsoft.Json.Linq.JArray();

    /// <param name="title">Window title and About heading.</param>
    /// <param name="releases">"owner/repo" on GitHub: the repository and releases links, the update check.</param>
    /// <param name="logo">URL of the logo, for the window icon and About.</param>
    public UiWindow(string title, string releases = null, string logo = null, string logPrefix = null, double? width = null, double? height = null)
    {
        Title = title; Releases = releases; Logo = logo; LogPrefix = logPrefix; Width = width; Height = height;
    }

    /// <summary>The game's own texts in another language, laid over the conventions' when the window is in it:
    /// UiSchema.K (a key's label and help), Pg (a page's title by id), Tx (a declared text by its English wording:
    /// a section, a pill text, a page title, an option label, a message group), Ui (one of the window's own texts by
    /// its key) and Ph (a placeholder's help). Several calls add up.</summary>
    public UiWindow Lang(string code, params UiText[] texts)
    {
        var lang = LangMap[code] as Newtonsoft.Json.Linq.JObject;
        if (lang == null) LangMap[code] = lang = new Newtonsoft.Json.Linq.JObject();
        foreach (var t in texts)
        {
            var section = lang[t.Section] as Newtonsoft.Json.Linq.JObject;
            if (section == null) lang[t.Section] = section = new Newtonsoft.Json.Linq.JObject();
            section[t.Name] = t.Value;
        }
        return this;
    }

    /// <summary>Locks the layout: the window has no customise mode, and a user's renames, moves, hidden fields and
    /// accent colour are not applied. Theme and language stay the user's to pick.</summary>
    public UiWindow Lock(bool locked = true) { Locked = locked; return this; }

    /// <summary>The pages in order: a UiPage (UiSchema.P) or the id of a standard page ("messages", "log", "help", "about").</summary>
    public UiWindow Pages(params object[] pages) { PageList.AddRange(pages); return this; }

    /// <summary>The Log page: the folder (relative to the Streamer.bot folder) and the files in it, "BJG-log*.txt";
    /// the bool setting behind "Write a log file per day"; the columns after Time and Result as "Header|field" or a
    /// field; the values of the Result filter.</summary>
    public UiWindow Log(string folder, string files, string writeLogKey = null, string writeLogHelp = null, string[] columns = null, string[] results = null,
        string[] platforms = null, string emptyText = null, string writeLogLabel = null)
    {
        LogMap = new Newtonsoft.Json.Linq.JObject { ["folder"] = folder, ["files"] = files };
        if (writeLogKey != null) LogMap["writeLogKey"] = writeLogKey;
        if (writeLogLabel != null) LogMap["writeLogLabel"] = writeLogLabel;
        if (writeLogHelp != null) LogMap["writeLogHelp"] = writeLogHelp;
        if (emptyText != null) LogMap["emptyText"] = emptyText;
        if (columns != null)
        {
            var list = new Newtonsoft.Json.Linq.JArray();
            foreach (var column in columns)
            {
                int bar = column.IndexOf('|');
                list.Add(bar < 0 ? (Newtonsoft.Json.Linq.JToken)column : new Newtonsoft.Json.Linq.JObject { ["header"] = column.Substring(0, bar), ["field"] = column.Substring(bar + 1) });
            }
            LogMap["columns"] = list;
        }
        if (results != null) LogMap["results"] = new Newtonsoft.Json.Linq.JArray(results);
        if (platforms != null) LogMap["platforms"] = new Newtonsoft.Json.Linq.JArray(platforms);
        return this;
    }

    /// <summary>Commands whose cooldowns follow settings (UiSchema.C): the settings action sets them after every save.</summary>
    public UiWindow Cooldowns(params string[][] cooldowns)
    {
        foreach (var c in cooldowns)
            CooldownList.Add(new Newtonsoft.Json.Linq.JObject { ["command"] = c[0], ["global"] = c.Length > 1 ? c[1] : null, ["user"] = c.Length > 2 ? c[2] : null });
        return this;
    }

    /// <summary>A section (by the name its settings declare) shown only while a condition holds, heading and fields
    /// together: "questionSource=igdb", "mode=a|b", "mode!=free && useBot".</summary>
    public UiWindow Section(string name, string showWhen)
    {
        SectionList.Add(new Newtonsoft.Json.Linq.JObject { ["name"] = name, ["showWhen"] = showWhen });
        return this;
    }

    /// <summary>A pill in the window's header that follows a bool setting.</summary>
    public UiWindow Pill(string key, string on, string off = null, string tooltip = null)
    {
        var pill = new Newtonsoft.Json.Linq.JObject { ["key"] = key, ["on"] = on };
        if (off != null) pill["off"] = off;
        if (tooltip != null) pill["tooltip"] = tooltip;
        PillList.Add(pill);
        return this;
    }

    /// <summary>The keys of the sender switches on the Messages page, where they differ from the conventions'
    /// (useBot, sendAction, cooldownUseBot, cooldownSendAction, numberSeparator). Null hides one.</summary>
    public UiWindow Senders(string useBot = "", string sendAction = "", string cooldownUseBot = "", string cooldownSendAction = "", string numberSeparator = "", string cooldownGroup = "")
    {
        SenderMap = SenderMap ?? new Newtonsoft.Json.Linq.JObject();
        if (useBot != "") SenderMap["useBot"] = useBot;
        if (sendAction != "") SenderMap["sendAction"] = sendAction;
        if (cooldownUseBot != "") SenderMap["cooldownUseBot"] = cooldownUseBot;
        if (cooldownSendAction != "") SenderMap["cooldownSendAction"] = cooldownSendAction;
        if (numberSeparator != "") SenderMap["numberSeparator"] = numberSeparator;
        if (cooldownGroup != "") SenderMap["cooldownGroup"] = cooldownGroup;
        return this;
    }

    /// <summary>Sample values for the chat preview's placeholders, as pairs: "user", "alice", "bet", "1,500".</summary>
    public UiWindow Samples(params string[] pairs)
    {
        for (int i = 0; i + 1 < pairs.Length; i += 2) SampleMap[pairs[i]] = pairs[i + 1];
        return this;
    }

    /// <summary>What an action would answer, for the framework's preview and screenshots: text, or a JSON object.</summary>
    public UiWindow Answer(string action, string answer)
    {
        Newtonsoft.Json.Linq.JToken value;
        try { value = answer != null && answer.TrimStart().StartsWith("{") ? Newtonsoft.Json.Linq.JObject.Parse(answer) : (Newtonsoft.Json.Linq.JToken)answer; }
        catch (System.Exception) { value = answer; }
        AnswerMap[action] = value;
        return this;
    }

    /// <summary>What the window changes about one setting of the table, where the table itself should not say it.</summary>
    public UiWindow Override(string key, string page = null, string label = null, string help = null, string section = null, bool? hidden = null, int? order = null)
    {
        var o = new Newtonsoft.Json.Linq.JObject();
        if (page != null) o["page"] = page;
        if (label != null) o["label"] = label;
        if (help != null) o["help"] = help;
        if (section != null) o["section"] = section;
        if (hidden.HasValue) o["hidden"] = hidden.Value;
        if (order.HasValue) o["order"] = order.Value;
        OverrideMap[key] = o;
        return this;
    }

    /// <summary>A tour of the window, stop by stop (UiSchema.Tip): it starts by itself on the window's first open and
    /// again from the ? in the header. Its texts translate through Lang's Tx like every declared text.</summary>
    public UiWindow Tour(params UiLine[] tips)
    {
        foreach (var tip in tips) TourList.Add(tip.Json);
        return this;
    }

    /// <summary>The Help page's sections (UiSchema.H).</summary>
    public UiWindow Help(params UiSection[] sections)
    {
        foreach (var section in sections) HelpList.Add(section.ToJson());
        return this;
    }

    /// <summary>The About page: the game's credits under its name, sections like help, the game's own links. The page
    /// ends with the framework's links (its page, Streamer.bot, where to report a problem); defaultLinks: false leaves
    /// them out, streamerBotLink: false only that one. authorLink is kept for older games and no longer does anything.</summary>
    public UiWindow About(string credits = null, UiSection[] sections = null, UiLine[] links = null, bool streamerBotLink = true, bool authorLink = true, bool defaultLinks = true)
    {
        AboutMap = new Newtonsoft.Json.Linq.JObject();
        if (credits != null) AboutMap["credits"] = credits;
        if (sections != null) AboutMap["sections"] = new Newtonsoft.Json.Linq.JArray(System.Linq.Enumerable.Select(sections, s => s.ToJson()));
        if (links != null) AboutMap["links"] = new Newtonsoft.Json.Linq.JArray(System.Linq.Enumerable.Select(links, l => new Newtonsoft.Json.Linq.JObject { ["text"] = l.Json["link"], ["url"] = l.Json["url"] }));
        if (!streamerBotLink) AboutMap["streamerBotLink"] = false;
        if (!defaultLinks) AboutMap["defaultLinks"] = false;
        return this;
    }

    public Newtonsoft.Json.Linq.JObject ToJson()
    {
        var root = new Newtonsoft.Json.Linq.JObject { ["title"] = Title };
        if (Releases != null) root["releases"] = Releases;
        if (Logo != null) root["logo"] = Logo;
        if (LogPrefix != null) root["logPrefix"] = LogPrefix;
        if (Language != null) root["language"] = Language;
        if (Locked) root["locked"] = true;
        if (Width.HasValue) root["width"] = Width.Value;
        if (Height.HasValue) root["height"] = Height.Value;
        if (PageList.Count > 0) root["pages"] = new Newtonsoft.Json.Linq.JArray(System.Linq.Enumerable.Select(PageList, p => p is UiPage page ? page.ToJson() : (Newtonsoft.Json.Linq.JToken)p.ToString()));
        if (OverrideMap.Count > 0) root["overrides"] = OverrideMap;
        if (SenderMap != null) root["senders"] = SenderMap;
        if (SampleMap.Count > 0) root["samples"] = SampleMap;
        if (AnswerMap.Count > 0) root["answers"] = AnswerMap;
        if (PillList.Count > 0) root["headerPills"] = PillList;
        if (SectionList.Count > 0) root["sections"] = SectionList;
        if (CooldownList.Count > 0) root["cooldowns"] = CooldownList;
        if (LogMap != null) root["log"] = LogMap;
        if (HelpList.Count > 0) root["help"] = HelpList;
        if (AboutMap != null) root["about"] = AboutMap;
        if (TourList.Count > 0) root["tour"] = TourList;
        return root;
    }
}

/// <summary>A page of the window: its id, title and icon (an emoji, or a conventions name like "gear"), and the
/// settings it claims ahead of their own page (keys, or patterns like "overlay*").</summary>
public sealed class UiPage
{
    public string Id, Title, Icon; public string[] Fields; public bool Custom, Bottom;
    public UiPage(string id, string title, string icon, string[] fields) { Id = id; Title = title; Icon = icon; Fields = fields; }
    public Newtonsoft.Json.Linq.JObject ToJson()
    {
        var page = new Newtonsoft.Json.Linq.JObject { ["id"] = Id };
        if (Title != null) page["title"] = Title;
        if (Icon != null) page["icon"] = Icon;
        if (Fields != null && Fields.Length > 0) page["fields"] = new Newtonsoft.Json.Linq.JArray(Fields);
        if (Custom) page["custom"] = true;
        if (Bottom) page["bottom"] = true;
        return page;
    }
}

/// <summary>A section of the Help or About page: a heading and lines, each a note (a string), UiSchema.Cmd or UiSchema.Link.</summary>
public sealed class UiSection
{
    public string Heading; public object[] Lines;
    public UiSection(string heading, object[] lines) { Heading = heading; Lines = lines; }
    public Newtonsoft.Json.Linq.JObject ToJson() => new Newtonsoft.Json.Linq.JObject { ["heading"] = Heading, ["lines"] = new Newtonsoft.Json.Linq.JArray(System.Linq.Enumerable.Select(Lines, l => l is UiLine line ? line.Json : (Newtonsoft.Json.Linq.JToken)l.ToString())) };
}

/// <summary>One translated text for UiWindow.Lang: the section of the language file it goes into, its name there, its value.</summary>
public sealed class UiText
{
    public readonly string Section, Name; public readonly Newtonsoft.Json.Linq.JToken Value;
    public UiText(string section, string name, Newtonsoft.Json.Linq.JToken value) { Section = section; Name = name; Value = value; }
}

public sealed class UiLine
{
    public readonly Newtonsoft.Json.Linq.JObject Json;
    public UiLine(Newtonsoft.Json.Linq.JObject json) { Json = json; }
}

/// <summary>Answers the settings window's requests, which its generic settings action sends through this game's
/// arguments: "describe" (the table and the window), "test" (a Try button or a live field, with the window's current
/// values), "validate" (before a save; answer the problems, one per line, or nothing), "saved" (after a save, with the
/// saved values), "table" (the rows of a table field) and "emotes" (the emote sources, as JSON). The answers go back
/// through a non-persistent global; the describe answer through UiSchema.Var, the rest through UiHost.AnswerVar.
/// Every request must leave the game as it was: no chat output, no state written.</summary>
public static class UiHost
{
    public static string AnswerVar(string actionName) => "uiframework.answer." + UiSchema.Clean(actionName);

    /// <param name="args">The action's arguments.</param>
    /// <param name="CPH">The action's CPH.</param>
    /// <param name="actionName">This action's name, as the settings action knows it.</param>
    /// <param name="host">A short name of the game for the framework's files, "blackjack".</param>
    /// <param name="version">The game's version, "v17.0.0".</param>
    /// <param name="settingsFile">The settings file, relative to the Streamer.bot folder.</param>
    /// <param name="test">Answers a Try button or a live field by its key with the window's current values: a line for the footer, or JSON (docs/Schema.md, kind action). Null for a key: "no answer".</param>
    /// <param name="validate">Looks at the values about to be saved; answers the problems, one per line, or null.</param>
    /// <param name="saved">Runs after a save with the saved values: an overlay to write, a timer to restart. The cooldowns the window declares are set by the settings action itself.</param>
    /// <param name="table">The rows of a table field by its key, as a JSON array of objects.</param>
    /// <param name="emotes">The emote sources for emote fields, as JSON (docs/Schema.md).</param>
    /// <returns>True when the action was called by the settings window and has answered; return true at once then.</returns>
    public static bool Handle(System.Collections.Generic.IDictionary<string, object> args, IInlineInvokeProxy CPH, string actionName, string host, string version, string settingsFile,
        System.Func<string, UiSettings, string> test = null, System.Func<UiSettings, string> validate = null, System.Action<UiSettings> saved = null,
        System.Func<string, string> table = null, System.Func<string> emotes = null)
    {
        string request = Arg(args, "uiframework.request");
        if (request == null && args.ContainsKey("uiframework.describe")) request = "describe";
        if (string.IsNullOrEmpty(request))
            return false;
        // The settings action names the global it will read; without that (an older stub) the name is built here.
        string var = Arg(args, "uiframework.var");
        if (request == "describe")
        {
            var currency = UiCurrency.Detect(CPH, null);   // only a detected system goes into the answer; the window shows it
            CPH.SetGlobalVar(string.IsNullOrEmpty(var) ? UiSchema.Var(actionName) : var, UiSchema.ToJson(host, version, settingsFile, currency), false);
            CPH.LogInfo("[" + actionName + "] Settings described for the window" + (currency.Detected ? " (" + currency.Source + ": '" + currency.Variable + "')." : "."));
            return true;
        }
        string name = Arg(args, "uiframework.name") ?? "";
        var values = UiSettings.Parse(Arg(args, "uiframework.values"));
        string answer = null;
        try
        {
            switch (request)
            {
                case "test": answer = test?.Invoke(name, values); break;
                case "validate": answer = validate?.Invoke(values); break;
                case "saved": saved?.Invoke(values); break;
                case "table": answer = table?.Invoke(name); break;
                case "emotes": answer = emotes?.Invoke(); break;
                default: CPH.LogWarn("[" + actionName + "] The settings window asked for '" + request + "', which this game does not know."); break;
            }
        }
        catch (System.Exception ex)
        {
            CPH.LogError("[" + actionName + "] The settings window's request '" + request + "' failed: " + ex.Message);
            answer = request == "saved" ? null : "The game could not answer: " + ex.Message;
        }
        CPH.SetGlobalVar(string.IsNullOrEmpty(var) ? AnswerVar(actionName) : var, answer ?? "", false);
        return true;
    }

    private static string Arg(System.Collections.Generic.IDictionary<string, object> args, string key) => args != null && args.TryGetValue(key, out var value) ? value as string : null;
}

/// <summary>The points system of this Streamer.bot, the way the games share it. StreamUP's Currency Core and
/// Tawmae's LOYALTY POINTS each keep their settings in a persisted global that names the user variable the points
/// live in and what they are called; when one is installed, the game uses those and its own two settings
/// (pointsVariable, pointsName) are only the fallback. Detect on every run: the streamer may install one later.
/// Reading and writing a viewer's points stays the game's: CPH.GetTwitchUserVarById / SetTwitchUserVarById (and
/// the YouTube and Kick ones) on Variable.</summary>
public sealed class UiCurrency
{
    /// <summary>The user variable the points live in.</summary>
    public string Variable = "points";
    /// <summary>What the points are called in chat.</summary>
    public string Name = "Points";
    /// <summary>"Currency Core", "LOYALTY POINTS", or null when the settings are in use.</summary>
    public string Source;
    public bool Detected => Source != null;

    /// <summary>Looks for an installed system; without one, the two settings (or their defaults when settings is null).</summary>
    public static UiCurrency Detect(IInlineInvokeProxy CPH, UiSettings settings)
    {
        var currency = new UiCurrency();
        string variable = settings?.String("pointsVariable"), name = settings?.String("pointsName");
        if (!string.IsNullOrWhiteSpace(variable)) currency.Variable = variable.Trim();
        if (!string.IsNullOrWhiteSpace(name)) currency.Name = name.Trim();
        if (!currency.Read(CPH, "currency_ProductSettings", "pointsVar", "pointsName", "Currency Core"))
            currency.Read(CPH, "tawmae_Settings_LOYALTY POINTS", "globalVariableName", "pointsName", "LOYALTY POINTS");
        return currency;
    }

    private bool Read(IInlineInvokeProxy CPH, string global, string variableKey, string nameKey, string source)
    {
        string json;
        try { json = CPH.GetGlobalVar<string>(global, true); }
        catch (System.Exception) { return false; }
        if (string.IsNullOrWhiteSpace(json)) return false;
        try
        {
            var settings = Newtonsoft.Json.Linq.JObject.Parse(json);
            string variable = (string)settings[variableKey];
            if (string.IsNullOrWhiteSpace(variable)) return false;
            Variable = variable.Trim();
            string name = (string)settings[nameKey];
            if (!string.IsNullOrWhiteSpace(name)) Name = name.Trim();
            Source = source;
            return true;
        }
        catch (System.Exception ex)
        {
            CPH.LogWarn("[UIFramework] The settings of " + source + " (" + global + ") could not be read: " + ex.Message);
            return false;
        }
    }

    public Newtonsoft.Json.Linq.JObject ToJson() => new Newtonsoft.Json.Linq.JObject { ["source"] = Source, ["variable"] = Variable, ["name"] = Name };
}

/// <summary>The settings file as the game reads it: every key through its default, old key names tolerated. Values
/// an alias stored in another scale (a fraction where percent is stored now) are converted on the way.</summary>
public sealed class UiSettings
{
    private readonly Newtonsoft.Json.Linq.JObject _root;

    private UiSettings(Newtonsoft.Json.Linq.JObject root) { _root = root; }

    /// <summary>Settings from JSON text, for tests and callers that already hold the values; empty text is empty settings.</summary>
    public static UiSettings Parse(string json)
    {
        try
        {
            using (var reader = new Newtonsoft.Json.JsonTextReader(new System.IO.StringReader(string.IsNullOrWhiteSpace(json) ? "{}" : json)) { DateParseHandling = Newtonsoft.Json.DateParseHandling.None })
                return new UiSettings(Newtonsoft.Json.Linq.JObject.Load(reader));
        }
        catch (System.Exception)
        {
            return new UiSettings(new Newtonsoft.Json.Linq.JObject());
        }
    }

    /// <summary>The values as they are, for a host that reads them its own way.</summary>
    public Newtonsoft.Json.Linq.JObject Raw => _root;

    /// <summary>Why the settings did not come from the file, or null: it could not be read, and the backup of the
    /// window's last save (FromBackup) or the defaults stand in for it.</summary>
    public string Problem { get; private set; }
    public bool FromBackup { get; private set; }

    /// <summary>Never throws: an unreadable file gives the .bak the window's last save kept, else the defaults, and
    /// the game keeps running; Problem says so, and log (CPH.LogWarn, say) hears it. A relative path starts in the
    /// Streamer.bot folder, whatever the current directory.</summary>
    public static UiSettings Load(string path, System.Action<string> log = null)
    {
        if (string.IsNullOrEmpty(path)) return new UiSettings(new Newtonsoft.Json.Linq.JObject());
        try
        {
            if (!System.IO.Path.IsPathRooted(path)) path = System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, path);
            if (!System.IO.File.Exists(path)) return new UiSettings(new Newtonsoft.Json.Linq.JObject());
            return new UiSettings(Read(path));
        }
        catch (System.Exception ex)
        {
            string file;
            try { file = System.IO.Path.GetFileName(path); } catch (System.Exception) { file = path; }
            string why = file + " could not be read (" + ex.Message + ")";
            UiSettings stand;
            try { stand = System.IO.File.Exists(path + ".bak") ? new UiSettings(Read(path + ".bak")) { FromBackup = true, Problem = why + "; the backup of the last save is used." } : null; }
            catch (System.Exception) { stand = null; }
            if (stand == null) stand = new UiSettings(new Newtonsoft.Json.Linq.JObject()) { Problem = why + "; the defaults are used." };
            if (log != null) try { log(stand.Problem); } catch (System.Exception) { }
            return stand;
        }
    }

    private static Newtonsoft.Json.Linq.JObject Read(string path)
    {
        using (var reader = new Newtonsoft.Json.JsonTextReader(new System.IO.StringReader(ReadWithRetry(path))) { DateParseHandling = Newtonsoft.Json.DateParseHandling.None })
            return Newtonsoft.Json.Linq.JObject.Load(reader);
    }

    private static string ReadWithRetry(string path)
    {
        for (int attempt = 1; ; attempt++)
        {
            try { return System.IO.File.ReadAllText(path); }
            catch (System.IO.IOException) when (attempt < 5) { System.Threading.Thread.Sleep(40); }
        }
    }

    public Newtonsoft.Json.Linq.JToken Token(string key)
    {
        var token = _root[key];
        if (token != null && token.Type != Newtonsoft.Json.Linq.JTokenType.Null) return token;
        var setting = UiSchema.Find(key);
        if (setting?.Aliases != null)
            for (int i = 0; i < setting.Aliases.Length; i++)
            {
                // An alias with a dot reaches into an object of the old file ("PlatformEmotes.twitch").
                string alias = setting.Aliases[i];
                int dot = alias.IndexOf('.');
                var old = dot > 0 ? (_root[alias.Substring(0, dot)] as Newtonsoft.Json.Linq.JObject)?[alias.Substring(dot + 1)] : _root[alias];
                if (old == null || old.Type == Newtonsoft.Json.Linq.JTokenType.Null) continue;
                if (i == 0 && setting.AliasFactor.HasValue && (old.Type == Newtonsoft.Json.Linq.JTokenType.Integer || old.Type == Newtonsoft.Json.Linq.JTokenType.Float))
                    return System.Convert.ToDouble(((Newtonsoft.Json.Linq.JValue)old).Value, System.Globalization.CultureInfo.InvariantCulture) * setting.AliasFactor.Value;
                return old;
            }
        return setting?.Default == null ? null : Newtonsoft.Json.Linq.JToken.FromObject(setting.Default);
    }

    public bool Bool(string key) { var t = Token(key); return t != null && t.Type == Newtonsoft.Json.Linq.JTokenType.Boolean && (bool)t; }

    public long Long(string key)
    {
        // A whole number as it is: through a double, one past 2^53 would lose its last digits.
        var t = Token(key);
        if (t != null && t.Type == Newtonsoft.Json.Linq.JTokenType.Integer)
        {
            try { return (long)t; }
            catch (System.Exception) { return t.ToString().TrimStart().StartsWith("-") ? long.MinValue : long.MaxValue; }
        }
        double d = Double(key);
        if (d >= long.MaxValue) return long.MaxValue;
        if (d <= long.MinValue) return long.MinValue;
        return (long)System.Math.Round(d);
    }

    public int Int(string key) { long l = Long(key); return l > int.MaxValue ? int.MaxValue : l < int.MinValue ? int.MinValue : (int)l; }

    public double Double(string key)
    {
        var t = Token(key);
        if (t == null) return 0;
        double d;
        if (t.Type == Newtonsoft.Json.Linq.JTokenType.Integer || t.Type == Newtonsoft.Json.Linq.JTokenType.Float) d = System.Convert.ToDouble(((Newtonsoft.Json.Linq.JValue)t).Value, System.Globalization.CultureInfo.InvariantCulture);
        else if (!double.TryParse(t.ToString().Trim().Replace(',', '.'), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out d)) return 0;
        return double.IsNaN(d) || double.IsInfinity(d) ? 0 : d;
    }

    /// <summary>The value as text, numbers with a point whatever the system's culture ("0.5", not "0,5").</summary>
    public string String(string key)
    {
        var t = Token(key);
        if (t == null || t.Type == Newtonsoft.Json.Linq.JTokenType.Null) return "";
        if (t.Type == Newtonsoft.Json.Linq.JTokenType.String) return (string)t;
        if (t.Type == Newtonsoft.Json.Linq.JTokenType.Float) return System.Convert.ToDouble(((Newtonsoft.Json.Linq.JValue)t).Value, System.Globalization.CultureInfo.InvariantCulture).ToString("R", System.Globalization.CultureInfo.InvariantCulture);
        if (t.Type == Newtonsoft.Json.Linq.JTokenType.Integer) return System.Convert.ToString(((Newtonsoft.Json.Linq.JValue)t).Value, System.Globalization.CultureInfo.InvariantCulture);
        if (t.Type == Newtonsoft.Json.Linq.JTokenType.Boolean) return (bool)t ? "true" : "false";
        return t.ToString(Newtonsoft.Json.Formatting.None);
    }

    /// <summary>A ceiling that stores "no limit" as 0: read as long.MaxValue.</summary>
    public long Ceiling(string key) { long l = Long(key); return l <= 0 ? long.MaxValue : l; }
    public int CeilingInt(string key) { long l = Long(key); return l <= 0 || l > int.MaxValue ? int.MaxValue : (int)l; }
    public static int Clamp(int value, int min, int max) => value < min ? min : value > max ? max : value;
    public static long Clamp(long value, long min, long max) => value < min ? min : value > max ? max : value;
    public static double Clamp(double value, double min, double max) => value < min ? min : value > max ? max : value;
}
// --- end UIFramework host block ---
