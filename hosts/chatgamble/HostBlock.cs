// --- UIFramework host block (keep identical in every action of this host that reads the settings) ---
// From UIFramework/stub/HostSchema.cs (tools/update-host-block.ps1 refreshes it). The game declares its settings
// once in UiSchema.Table and its window in UiSchema.Window, reads the settings through UiSettings, and lets
// UiHost.Handle answer the settings window's requests. One table, so the game and its window can never disagree.
// Compiles in any Streamer.bot action with no reference beyond Newtonsoft.Json.
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
        // For a list: its columns as "key|kind|label" (kinds: string, text, int, double, bool, emote; an emote column
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
    }

    /// <summary>A setting: key and default, then whatever the conventions cannot know. Keys the conventions know
    /// (useBot, globalCooldown, blockedUsers, ...) need nothing but their default. An option is "value" or
    /// "value|label". aliasFactor multiplies the first alias's value on the move (a fraction to percent: 100).
    /// requires is a switch's key, or a condition on any field: "numberOfSlots>=5", "cpBetMode=fixed", "mode!=free".</summary>
    public static Setting S(string key, object @default, string page = null, string label = null, string help = null, string kind = null,
        string section = null, double? min = null, double? max = null, string unit = null, double? factor = null, bool unlimitedAtZero = false,
        string[] options = null, string requires = null, string confirm = null, bool hidden = false, string[] aliases = null, double? aliasFactor = null,
        string[] columns = null, string file = null, string field = null, bool transient = false, bool live = false, bool panel = false, bool auto = false,
        string platform = null, bool twitchOnly = false, string row = null)
    {
        return new Setting
        {
            Key = key, Default = @default, Page = page, Label = label, Help = help, Kind = kind, Section = section, Min = min, Max = max,
            Unit = unit, Factor = factor, UnlimitedAtZero = unlimitedAtZero, Options = options, Requires = requires, Confirm = confirm,
            Hidden = hidden, Aliases = aliases, AliasFactor = aliasFactor, Columns = columns, File = file, Field = field,
            Transient = transient, Live = live, Panel = panel, Auto = auto, Platform = platform, TwitchOnly = twitchOnly, Row = row
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
        var settings = new JArray();
        foreach (var s in Table)
        {
            var item = new JObject { ["key"] = s.Key, ["default"] = s.Default == null ? JValue.CreateNull() : JToken.FromObject(s.Default) };
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
                var options = new JArray();
                foreach (var option in s.Options)
                {
                    int bar = option.IndexOf('|');
                    options.Add(bar < 0 ? (JToken)option : new JObject { ["value"] = option.Substring(0, bar), ["label"] = option.Substring(bar + 1) });
                }
                item["options"] = options;
            }
            if (s.Placeholders != null) item["placeholders"] = new JArray(s.Placeholders);
            if (s.Columns != null)
            {
                var columns = new JArray();
                foreach (var column in s.Columns)
                {
                    var parts = column.Split('|');
                    var cell = new JObject { ["key"] = parts[0], ["kind"] = parts.Length > 1 ? parts[1] : "string", ["label"] = parts.Length > 2 ? parts[2] : parts[0] };
                    if (parts.Length > 3) cell["platform"] = parts[3];
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
            if (s.Row != null) item["row"] = s.Row;
            if (s.Field != null) item["field"] = s.Field;
            if (s.Aliases != null) item["aliases"] = new JArray(s.Aliases);
            if (s.AliasFactor.HasValue && s.Aliases != null && s.Aliases.Length > 0) item["aliasFactors"] = new JObject { [s.Aliases[0]] = s.AliasFactor.Value };
            settings.Add(item);
        }
        var root = new JObject { ["host"] = host, ["version"] = version, ["settingsFile"] = settingsFile, ["settings"] = settings };
        if (Window != null) root["window"] = Window.ToJson();
        if (Window != null && Window.LangMap.Count > 0) root["lang"] = Window.LangMap;
        if (currency != null && currency.Detected) root["currency"] = currency.ToJson();
        return root.ToString(Formatting.None);
    }

    // ---- Shorthands for the window: a page, a help section and its lines, a cooldown. ----
    public static UiPage P(string id, string title = null, string icon = null, params string[] fields) => new UiPage(id, title, icon, fields);
    public static UiSection H(string heading, params object[] lines) => new UiSection(heading, lines);
    public static UiLine Cmd(string command, string what) => new UiLine(new JObject { ["command"] = command, ["what"] = what });
    public static UiLine Note(string note) => new UiLine(new JObject { ["note"] = note });
    public static UiLine Link(string text, string url) => new UiLine(new JObject { ["link"] = text, ["url"] = url });
    /// <summary>A command's cooldowns, set after every save: the command as its text ("!roll"), its name or its id in
    /// Streamer.bot, the setting of its global cooldown and the setting of its per-user cooldown (either may be null).</summary>
    public static string[] C(string command, string globalKey, string userKey) => new[] { command, globalKey, userKey };

    // ---- Shorthands for UiWindow.Lang: a key's texts, a page title, a declared text. ----
    public static UiText K(string key, string label, string help = null) => new UiText("keys", key, help == null ? (JToken)label : new JObject { ["label"] = label, ["help"] = help });
    public static UiText Pg(string id, string title) => new UiText("pages", id, title);
    public static UiText Tx(string declared, string translation) => new UiText("texts", declared, translation);

    /// <summary>The global the stub reads the schema from: the action's name, with the characters Streamer.bot's
    /// store refuses (brackets, a trailing space) replaced. Must match the stub's SchemaVar.</summary>
    public static string Var(string actionName) =>
        "uiframework.schema." + System.Text.RegularExpressions.Regex.Replace((actionName ?? "").Trim(), @"[^\w .-]", "_");

    public static Setting Find(string key)
    {
        foreach (var s in Table)
            if (s.Key == key) return s;
        return null;
    }

    // ---- The window: title, pages, help, about, log. Null: the settings action carries a spec of its own. ----
    public static readonly UiWindow Window = null; // __WINDOW__

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
        S("fairness", "classic", page: "game", label: "Preset", help: "One slider for the odds, from Strict, where the house wins and points drain away, to Classic, the default with rare, huge jackpots where points grow fast. A stop fills in the five numbers below; nothing is saved until you save. Custom is where the slider goes when the numbers are your own.", kind: "slider", section: "Fairness", options: new[] { "strict|Strict", "houseEdge|House Edge", "fair|Fair", "generous|Generous", "classic|Classic", "custom|Custom" }, transient: true, live: true),
        S("presetNote", null, page: "game", help: "A stop of the slider sets the jackpot and win chance, the jackpot multiplier and the win multiplier range in one go. The game only ever reads those five numbers, so you can still fine-tune them: the slider then shows Custom, or the stop they match.", kind: "note", section: "Fairness"),
        S("jackpotPercent", 1.0, page: "game", label: "Jackpot chance", help: "Chance in percent, from 0 (never) to 100 (always), that a gamble hits the jackpot. Keep it small so jackpots stay rare: '1' is 1 in 100.", kind: "percent", section: "Fairness", min: 0.0, max: 100.0, aliases: new[] { "JackpotPercent" }),
        S("jackpotMultiplier", 43, page: "game", label: "Jackpot ×", help: "Multiplier stacked on top of the win roll when a jackpot hits. With '43', a 2× roll pays 2 × 43 = 86 times the bet.", section: "Fairness", min: 1.0, aliases: new[] { "JackpotMultiplier" }),
        S("winPercent", 30.0, page: "game", label: "Win chance", help: "Chance in percent that a gamble is a normal win, on top of the jackpot chance. Jackpot '1' and Win '30' leave a 69 % chance to lose.", kind: "percent", section: "Fairness", min: 0.0, max: 100.0, aliases: new[] { "WinPercent" }),
        S("rndMin", 1.5, page: "game", label: "Min ×", help: "The smallest win multiplier. Every win rolls a multiplier between Min and Max, and a jackpot stacks on that roll.", kind: "double", section: "Fairness", min: 0.0, aliases: new[] { "RndMin" }),
        S("rndMax", 2.5, page: "game", label: "Max ×", help: "The largest win multiplier.", kind: "double", section: "Fairness", min: 0.0, aliases: new[] { "RndMax" }),
        S("odds", null, page: "game", label: "What really comes out", help: "The chance of a jackpot, a win and a loss, and what a bet of 100 comes back as on average, from the five numbers as they stand, for a viewer without Subscriber/VIP boost or Addicted cut.", kind: "action", section: "Fairness", live: true, panel: true, auto: true),
        S("defaultBet", 1234, page: "game", label: "Default bet", help: "The bet for a plain !gamble with no amount. 0 means viewers always have to name one. Viewers can also bet a number, a percentage, shorthand like 10k, or all, half, third, quarter and random.", section: "Bets and points", min: 0.0, aliases: new[] { "DefaultBet" }),
        S("minBet", 0, page: "game", label: "Minimum (0 = none)", help: "The smallest bet allowed. 0 turns the limit off. Also applies to channel points in 'Typed amount' and 'Fixed' mode.", section: "Bets and points", min: 0.0, aliases: new[] { "MinBet" }),
        S("maxBet", 9999999, page: "game", label: "Maximum (0 = none)", help: "The largest bet allowed. 0 turns the limit off. Also applies to channel points in 'Typed amount' and 'Fixed' mode.", section: "Bets and points", min: 0.0, aliases: new[] { "MaxBet" }),
        S("freeGame", false, page: "game", label: "Free Game Mode", help: "Nobody pays or wins points, channel point redemptions included, and chat gets the Free game messages. !SetFreeGamble and !EndFreeGamble switch it from chat; starting it there puts the Default bet to 0 and ending it brings the previous one back.", section: "Bets and points", aliases: new[] { "FreeGameActive" }),
        S("previousDefaultBet", 1234, page: "game", help: "The Default bet from before !SetFreeGamble, brought back by !EndFreeGamble.", section: "Bets and points", min: 0.0, hidden: true, aliases: new[] { "PreviousDefaultBet" }),
        S("pointsVariable", "points", page: "game", label: "Points variable", help: "The variable your points live in. Currency Core by StreamUP and LOYALTY POINTS by tawmae are found on their own; only change this for a points system of your own.", section: "Bets and points", aliases: new[] { "PointsVar" }),
        S("pointsName", "Points", page: "game", label: "Shown in chat as", help: "The name {pointsName} puts in chat, like 'Coins' or 'Gems'. Currency Core and LOYALTY POINTS bring their own name, which is used instead.", section: "Bets and points", aliases: new[] { "PointsName" }),
        S("subscriberBoostEnabled", false, page: "game", label: "Subscriber boost", help: "Gives Twitch subscribers a bigger payout on wins and jackpots. Twitch only: on YouTube and Kick the game doesn't check for subscribers.", section: "Payouts", aliases: new[] { "SubscriberBoostEnabled" }, twitchOnly: true),
        S("vipBoostEnabled", false, page: "game", label: "VIP boost", help: "Gives Twitch VIPs a bigger payout on wins and jackpots. Twitch only: on YouTube and Kick the game doesn't check for VIPs.", section: "Payouts", aliases: new[] { "VipBoostEnabled" }, twitchOnly: true),
        S("subscriberBoostPercent", 10.0, page: "game", label: "Subscriber boost", help: "How much bigger a subscriber's win is: '10' turns 100 into 110. Adds up with the VIP boost for someone who is both.", kind: "double", section: "Payouts", min: 0.0, unit: "%", requires: "subscriberBoostEnabled", aliases: new[] { "SubscriberBoostPercent" }),
        S("vipBoostPercent", 20.0, page: "game", label: "VIP boost", help: "How much bigger a VIP's win is: '20' turns 100 into 120. Adds up with the Subscriber boost for someone who is both.", kind: "double", section: "Payouts", min: 0.0, unit: "%", requires: "vipBoostEnabled", aliases: new[] { "VipBoostPercent" }),
        S("addictedPayoutCut", 50, page: "game", label: "Addicted cut", help: "How much of an addicted viewer's win is cut, from 0 (nothing) to 100 (all of it). Applied last, so it cuts a boosted win too - part of the boost is kept. Who counts as addicted is set on the Access page.", kind: "percent", section: "Payouts", min: 0.0, max: 100.0, aliases: new[] { "AddictedPayoutCutPercent" }),
        S("tryBet", 1000, page: "game", label: "Bet to try", help: "A bet to try the odds and messages with. It isn't saved.", section: "Try a bet", min: 1.0, transient: true, row: "try"),
        S("tryRole", "viewer", page: "game", label: "Play as", help: "Who plays: boosts and the Addicted cut change what a win pays.", kind: "options", section: "Try a bet", options: new[] { "viewer|Viewer", "subscriber|Subscriber", "vip|VIP", "addicted|Addicted" }, transient: true, row: "try"),
        S("tryRoll", null, page: "game", label: "Roll", help: "Plays one gamble with the settings as they stand on this page, saved or not, and shows the roll and the message chat would get. Nothing is sent and nothing is saved.", kind: "action", section: "Try a bet", live: true, panel: true),
        // ---- access ----
        S("platformsNote", null, page: "access", help: "Twitch, YouTube and Kick all play, and every reply goes to the chat the command or redemption came from. To leave a platform out, untick it on the !gamble command in Streamer.bot, for example 'Kick Message'.", kind: "note", section: "Platforms"),
        S("isChannelPointsTrigger", false, page: "access", label: "Channel points start a gamble", help: "On Twitch, a redemption of the reward below plays instead of !gamble, which is then off on Twitch. Redemptions only come from Twitch, so on YouTube and Kick viewers keep playing with !gamble.", section: "Channel points (Twitch)", aliases: new[] { "IsChannelPointsTrigger" }, twitchOnly: true),
        S("rewardId", "", page: "access", label: "Reward ID", help: "The ID of the channel point reward that starts a gamble. Other rewards are ignored; without one, no redemption starts a gamble. https://ayrtonalexis.com/reward_checker/reward_check.html lists your rewards with their IDs.", section: "Channel points (Twitch)", requires: "isChannelPointsTrigger", aliases: new[] { "TargetRewardId" }),
        S("cpBetMode", "cost", page: "access", label: "What is bet", help: "Reward cost: the reward's price is the bet, so viewers don't type anything; a loss doesn't touch their points, a win is paid in points. Typed amount: viewers type a bet into the reward, paid from their own points, checked against their balance; without enough points the redemption is refunded. Fixed: every redemption bets the fixed amount below, the price and the text are ignored, a loss doesn't touch the viewer's points. Free: nothing is won or lost. Minimum and Maximum bet apply to Typed amount and Fixed.", kind: "options", section: "Channel points (Twitch)", options: new[] { "cost|Reward cost", "input|Typed amount", "fixed|Fixed", "free|Free" }, requires: "isChannelPointsTrigger", aliases: new[] { "CPBetMode" }),
        S("fixedBetAmount", 0, page: "access", label: "Fixed bet", help: "What every redemption bets in Fixed mode. Must be more than 0.", section: "Channel points (Twitch)", min: 0.0, requires: "cpBetMode=fixed", aliases: new[] { "FixedBetAmount" }),
        S("refundFailedRedemptions", true, page: "access", label: "Refund redemptions that don't end in a gamble", help: "Viewer blocked, cooldown running or a bet that doesn't work: the redemption is cancelled and the channel points go back. Only works while the redemption is still queued - a reward that skips the request queue can't be refunded.", section: "Channel points (Twitch)", requires: "isChannelPointsTrigger", aliases: new[] { "RefundFailedRedemptions" }),
        S("globalCooldown", 0, page: "access", label: "Everyone", help: "How long everyone waits after any gamble, in seconds. 0 turns it off. Saving writes it onto the !gamble command. A channel point redemption during the cooldown is refunded; redemptions keep a clock of their own, so they don't hold up !gamble on YouTube or Kick, nor the other way round.", section: "Cooldowns", min: 0.0, aliases: new[] { "CooldownGlobalSeconds" }),
        S("userCooldown", 0, page: "access", label: "Per viewer", help: "How long one viewer waits before their next gamble, in seconds. 0 turns it off. Saving writes it onto the !gamble command. A channel point redemption during the cooldown is refunded.", section: "Cooldowns", min: 0.0, aliases: new[] { "CooldownUserSeconds" }),
        S("cooldownNote", null, page: "access", help: "What chat is told while a cooldown runs is on the Messages page, under Cooldown.", kind: "note", section: "Cooldowns"),
        S("blockedUsers", "", page: "access", label: "Blocked viewers", help: "Viewers who can't play. A name or a platform user ID - with the ID, a name change doesn't get around it.", section: "Who may not play", aliases: new[] { "BlockedUsers" }),
        S("blockedGroups", "", page: "access", label: "Blocked groups", help: "Streamer.bot user groups whose members can't play. Names must match the groups exactly.", section: "Who may not play", aliases: new[] { "BlockedGroups" }),
        S("addictedUsers", "", page: "access", label: "Addicted viewers", help: "Their wins are cut by the Addicted cut on the Game page, and they get the Addicted messages. !gambleaddict adds and removes them from chat too.", section: "Addicted", aliases: new[] { "AddictedUsers" }),
        S("addictedGroups", "", page: "access", label: "Addicted groups", help: "Members of these Streamer.bot groups count as addicted too.", section: "Addicted", aliases: new[] { "AddictedGroups" }),
        // ---- log ----
        S("logToFile", true, page: "log", label: "Write a log file per day", help: "Writes every gamble and refund - win, loss, jackpot - with the bet and the balance to a dated 'CCG-log-yyyy-MM-dd.txt' in the Logs folder, one file per day.", aliases: new[] { "LogToFile" }),
        // ---- messages, kept in messages.json next to configs.json ----
        Msg("Results", "winMessage", "@{user}, WINS! 🎉 You got {amount} {pointsName}! New balance: {currentPoints} {pointsName}.", "Win", "Sent on a normal win.", new[] { "user", "amount", "pointsName", "currentPoints" }, file: "messages.json", field: "WinMessage"),
        Msg("Results", "lossMessage", "@{user}, unlucky! You LOST {amount} {pointsName}. New balance: {currentPoints} {pointsName}.", "Loss", "Sent on a loss.", new[] { "user", "amount", "pointsName", "currentPoints" }, file: "messages.json", field: "LossMessage"),
        Msg("Results", "jackpotMessage", "🎰 J A C K P O T 🎰 @{user} you WON the JACKPOT of: {amount} {pointsName}! New balance: {currentPoints} {pointsName}.", "Jackpot", "Sent on a jackpot.", new[] { "user", "amount", "pointsName", "currentPoints" }, file: "messages.json", field: "JackpotMessage"),
        Msg("Free game", "freeGameMessage", "@{user}, gamble runs in 'Free Mode'! Enjoy playing without spending any {pointsName}.", "Notice, once per viewer", "Sent once to each viewer who plays while Free Game Mode is on.", new[] { "user", "pointsName" }, file: "messages.json", field: "FreeGameMessage"),
        Msg("Free game", "freeGameWinMessage", "🎉 @{user}, you WON! Lucky you! 🎉", "Win", "Sent on a win while Free Game Mode is on.", new[] { "user" }, file: "messages.json", field: "FreeGameWinMessage"),
        Msg("Free game", "freeGameLossMessage", "@{user}, unlucky! Luckily it's Free Mode, so you lost nothing. 🍀", "Loss", "Sent on a loss while Free Game Mode is on. Nothing is taken.", new[] { "user", "pointsName" }, file: "messages.json", field: "FreeGameLossMessage"),
        Msg("Free game", "freeGameJackpotMessage", "🎰 J A C K P O T 🎰 @{user}, you hit the JACKPOT! Incredible! 🎰", "Jackpot", "Sent on a jackpot while Free Game Mode is on.", new[] { "user" }, file: "messages.json", field: "FreeGameJackpotMessage"),
        Msg("Free game", "freeGameStartMessage", "🎲 Free Gamble Mode is now ACTIVE! Gamble for free — no points needed!", "Started", "Sent once when Free Game Mode is turned on, in the chat the command or redemption came from. Started any other way, say by an Execute C# Method on StartFreeGame in an action of your own, it goes to every platform with an account in Streamer.bot - to YouTube only while a broadcast is monitored.", new string[0], file: "messages.json", field: "FreeGameStartMessage"),
        Msg("Free game", "freeGameEndMessage", "⏹️ Free Gamble Mode has ended. Normal betting is back, time to waste points!", "Ended", "Sent once when Free Game Mode ends and the Default bet is back, in the chat the command or redemption came from. Ended any other way, say by an Execute C# Method on EndFreeGame in an action of your own, it goes to every platform with an account in Streamer.bot - to YouTube only while a broadcast is monitored.", new string[0], file: "messages.json", field: "FreeGameEndMessage"),
        Msg("Addicted", "addictedWinMessage", "@{user}, WINS! 🎉 You got {amount} {pointsName}! -- Addiction Fee: your win was cut by {addictedCut}% -- New balance: {currentPoints} {pointsName}.", "Win", "Sent instead of the Win message to viewers on the Addicted list.", new[] { "user", "amount", "pointsName", "currentPoints", "addictedCut" }, file: "messages.json", field: "AddictedWinMessage"),
        Msg("Addicted", "addictedJackpotMessage", "🎰 J A C K P O T 🎰 @{user} you WON the JACKPOT of: {amount} {pointsName}! -- Addiction Fee: your win was cut by {addictedCut}% -- New balance: {currentPoints} {pointsName}.", "Jackpot", "Sent instead of the Jackpot message to viewers on the Addicted list.", new[] { "user", "amount", "pointsName", "currentPoints", "addictedCut" }, file: "messages.json", field: "AddictedJackpotMessage"),
        Msg("Addicted", "addictedAdded", "@{user}, {target} has been added to the addicted list.", "Added", "Sent when !gambleaddict puts someone on the list.", new[] { "user", "target" }, file: "messages.json", field: "AddictedAdded"),
        Msg("Addicted", "addictedRemoved", "@{user}, {target} has been removed from the addicted list.", "Removed", "Sent when !gambleaddict remove takes someone off the list.", new[] { "user", "target" }, file: "messages.json", field: "AddictedRemoved"),
        Msg("Addicted", "addictedAlreadyOnList", "@{user}, {target} is already on the addicted list.", "Already on the list", "Sent when !gambleaddict names someone who is already on the list.", new[] { "user", "target" }, file: "messages.json", field: "AddictedAlreadyOnList"),
        Msg("Addicted", "addictedNotOnList", "@{user}, {target} is not on the addicted list.", "Not on the list", "Sent when !gambleaddict remove names someone who isn't on the list.", new[] { "user", "target" }, file: "messages.json", field: "AddictedNotOnList"),
        Msg("Addicted", "addictedUsage", "@{user}, usage: !gambleaddict add <user> or !gambleaddict remove <user>", "Usage", "Sent when !gambleaddict comes without a name.", new[] { "user" }, file: "messages.json", field: "AddictedUsage"),
        Msg("Bets", "invalidBetMessage", "❌ @{user}, {reason}", "Bet refused", "The frame around the five reasons below; {reason} is replaced by whichever one applies.", new[] { "user", "reason" }, file: "messages.json", field: "InvalidBetMessage"),
        Msg("Bets", "reasonNegativeAmount", "that's not a valid amount: no decimals, nothing negative and nothing too large.", "Reason: not a valid amount", "Fills {reason} when the amount is negative, not a number, has decimals, or is too large.", new string[0], file: "messages.json", field: "ReasonNegativeAmount"),
        Msg("Bets", "reasonInsufficientFunds", "you don't have enough {pointsName}.", "Reason: not enough points", "Fills {reason} when the bet is more than the viewer has.", new[] { "pointsName" }, file: "messages.json", field: "ReasonInsufficientFunds"),
        Msg("Bets", "reasonBelowMinimum", "your bet is below the allowed minimum ({min} {pointsName}).", "Reason: below the minimum", "Fills {reason} when the bet is below the minimum on the Game page.", new[] { "min", "pointsName" }, file: "messages.json", field: "ReasonBelowMinimum"),
        Msg("Bets", "reasonAboveMaximum", "your bet is above the allowed maximum ({max} {pointsName}).", "Reason: above the maximum", "Fills {reason} when the bet is above the maximum on the Game page.", new[] { "max", "pointsName" }, file: "messages.json", field: "ReasonAboveMaximum"),
        Msg("Bets", "reasonNoInput", "please name an amount, or ask the streamer to set a Default bet.", "Reason: no amount", "Fills {reason} when no amount was given: Default bet is 0, or a Typed amount redemption came without one.", new string[0], file: "messages.json", field: "ReasonNoInput"),
        Msg("Cooldown", "globalCooldownMessage", "Please wait '{seconds}' seconds before trying again ⏳", "Everyone", "Sent while the cooldown for everyone runs.", new[] { "seconds", "user" }, file: "messages.json", field: "GlobalMessage"),
        Msg("Cooldown", "userCooldownMessage", "@{user}, please wait '{seconds}' seconds before trying again ⏳", "Per viewer", "Sent while this viewer's own cooldown runs.", new[] { "user", "seconds" }, file: "messages.json", field: "UserMessage"),
        Msg("Channel points", "redemptionRefundedMessage", "↩️ @{user}, your channel point redemption was refunded.", "Refunded", "Sent when a redemption is refunded.", new[] { "user" }, file: "messages.json", field: "RedemptionRefunded"),
    };
}

/// <summary>How the window is called and laid out, declared beside the table: what the framework's docs call the
/// spec (docs/Schema.md, "The window"). Every part is optional; a page a setting names but the window does not is
/// made up on the way, help and about always sit at the bottom.</summary>
public sealed class UiWindow
{
    public string Title, Releases, Logo, LogPrefix, Language;
    public double? Width, Height;
    public readonly List<object> PageList = new List<object>();
    public readonly JObject SampleMap = new JObject(), AnswerMap = new JObject(), OverrideMap = new JObject();
    public JObject SenderMap, LogMap, AboutMap;
    public readonly JArray PillList = new JArray(), CooldownList = new JArray(), HelpList = new JArray();
    public readonly JObject LangMap = new JObject();

    /// <param name="title">Window title and About heading.</param>
    /// <param name="releases">"owner/repo" on GitHub: the repository and releases links, the update check.</param>
    /// <param name="logo">URL of the logo, for the window icon and About.</param>
    public UiWindow(string title, string releases = null, string logo = null, string logPrefix = null, double? width = null, double? height = null)
    {
        Title = title; Releases = releases; Logo = logo; LogPrefix = logPrefix; Width = width; Height = height;
    }

    /// <summary>The game's own texts in another language, laid over the conventions' when the window is in it:
    /// UiSchema.K (a key's label and help), Pg (a page's title by id), Tx (a declared text by its English wording:
    /// a section, a pill text, a page title, an option label, a message group). Several calls add up.</summary>
    public UiWindow Lang(string code, params UiText[] texts)
    {
        var lang = LangMap[code] as JObject;
        if (lang == null) LangMap[code] = lang = new JObject();
        foreach (var t in texts)
        {
            var section = lang[t.Section] as JObject;
            if (section == null) lang[t.Section] = section = new JObject();
            section[t.Name] = t.Value;
        }
        return this;
    }

    /// <summary>The pages in order: a UiPage (UiSchema.P) or the id of a standard page ("messages", "log", "help", "about").</summary>
    public UiWindow Pages(params object[] pages) { PageList.AddRange(pages); return this; }

    /// <summary>The Log page: the folder (relative to the Streamer.bot folder) and the files in it, "BJG-log*.txt";
    /// the bool setting behind "Write a log file per day"; the columns after Time and Result as "Header|field" or a
    /// field; the values of the Result filter.</summary>
    public UiWindow Log(string folder, string files, string writeLogKey = null, string writeLogHelp = null, string[] columns = null, string[] results = null,
        string[] platforms = null, string emptyText = null, string writeLogLabel = null)
    {
        LogMap = new JObject { ["folder"] = folder, ["files"] = files };
        if (writeLogKey != null) LogMap["writeLogKey"] = writeLogKey;
        if (writeLogLabel != null) LogMap["writeLogLabel"] = writeLogLabel;
        if (writeLogHelp != null) LogMap["writeLogHelp"] = writeLogHelp;
        if (emptyText != null) LogMap["emptyText"] = emptyText;
        if (columns != null)
        {
            var list = new JArray();
            foreach (var column in columns)
            {
                int bar = column.IndexOf('|');
                list.Add(bar < 0 ? (JToken)column : new JObject { ["header"] = column.Substring(0, bar), ["field"] = column.Substring(bar + 1) });
            }
            LogMap["columns"] = list;
        }
        if (results != null) LogMap["results"] = new JArray(results);
        if (platforms != null) LogMap["platforms"] = new JArray(platforms);
        return this;
    }

    /// <summary>Commands whose cooldowns follow settings (UiSchema.C): the settings action sets them after every save.</summary>
    public UiWindow Cooldowns(params string[][] cooldowns)
    {
        foreach (var c in cooldowns)
            CooldownList.Add(new JObject { ["command"] = c[0], ["global"] = c.Length > 1 ? c[1] : null, ["user"] = c.Length > 2 ? c[2] : null });
        return this;
    }

    /// <summary>A pill in the window's header that follows a bool setting.</summary>
    public UiWindow Pill(string key, string on, string off = null, string tooltip = null)
    {
        var pill = new JObject { ["key"] = key, ["on"] = on };
        if (off != null) pill["off"] = off;
        if (tooltip != null) pill["tooltip"] = tooltip;
        PillList.Add(pill);
        return this;
    }

    /// <summary>The keys of the sender switches on the Messages page, where they differ from the conventions'
    /// (useBot, sendAction, cooldownUseBot, cooldownSendAction, numberSeparator). Null hides one.</summary>
    public UiWindow Senders(string useBot = "", string sendAction = "", string cooldownUseBot = "", string cooldownSendAction = "", string numberSeparator = "", string cooldownGroup = "")
    {
        SenderMap = SenderMap ?? new JObject();
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
        JToken value;
        try { value = answer != null && answer.TrimStart().StartsWith("{") ? JObject.Parse(answer) : (JToken)answer; }
        catch (Exception) { value = answer; }
        AnswerMap[action] = value;
        return this;
    }

    /// <summary>What the window changes about one setting of the table, where the table itself should not say it.</summary>
    public UiWindow Override(string key, string page = null, string label = null, string help = null, string section = null, bool? hidden = null, int? order = null)
    {
        var o = new JObject();
        if (page != null) o["page"] = page;
        if (label != null) o["label"] = label;
        if (help != null) o["help"] = help;
        if (section != null) o["section"] = section;
        if (hidden.HasValue) o["hidden"] = hidden.Value;
        if (order.HasValue) o["order"] = order.Value;
        OverrideMap[key] = o;
        return this;
    }

    /// <summary>The Help page's sections (UiSchema.H).</summary>
    public UiWindow Help(params UiSection[] sections)
    {
        foreach (var section in sections) HelpList.Add(section.ToJson());
        return this;
    }

    /// <summary>The About page: the credits line, sections like help, links; the Streamer.bot and author links are on unless turned off.</summary>
    public UiWindow About(string credits = null, UiSection[] sections = null, UiLine[] links = null, bool streamerBotLink = true, bool authorLink = true)
    {
        AboutMap = new JObject();
        if (credits != null) AboutMap["credits"] = credits;
        if (sections != null) AboutMap["sections"] = new JArray(sections.Select(s => s.ToJson()));
        if (links != null) AboutMap["links"] = new JArray(links.Select(l => new JObject { ["text"] = l.Json["link"], ["url"] = l.Json["url"] }));
        if (!streamerBotLink) AboutMap["streamerBotLink"] = false;
        if (!authorLink) AboutMap["authorLink"] = false;
        return this;
    }

    public JObject ToJson()
    {
        var root = new JObject { ["title"] = Title };
        if (Releases != null) root["releases"] = Releases;
        if (Logo != null) root["logo"] = Logo;
        if (LogPrefix != null) root["logPrefix"] = LogPrefix;
        if (Language != null) root["language"] = Language;
        if (Width.HasValue) root["width"] = Width.Value;
        if (Height.HasValue) root["height"] = Height.Value;
        if (PageList.Count > 0) root["pages"] = new JArray(PageList.Select(p => p is UiPage page ? page.ToJson() : (JToken)p.ToString()));
        if (OverrideMap.Count > 0) root["overrides"] = OverrideMap;
        if (SenderMap != null) root["senders"] = SenderMap;
        if (SampleMap.Count > 0) root["samples"] = SampleMap;
        if (AnswerMap.Count > 0) root["answers"] = AnswerMap;
        if (PillList.Count > 0) root["headerPills"] = PillList;
        if (CooldownList.Count > 0) root["cooldowns"] = CooldownList;
        if (LogMap != null) root["log"] = LogMap;
        if (HelpList.Count > 0) root["help"] = HelpList;
        if (AboutMap != null) root["about"] = AboutMap;
        return root;
    }
}

/// <summary>A page of the window: its id, title and icon (an emoji, or a conventions name like "gear"), and the
/// settings it claims ahead of their own page (keys, or patterns like "overlay*").</summary>
public sealed class UiPage
{
    public string Id, Title, Icon; public string[] Fields; public bool Custom, Bottom;
    public UiPage(string id, string title, string icon, string[] fields) { Id = id; Title = title; Icon = icon; Fields = fields; }
    public JObject ToJson()
    {
        var page = new JObject { ["id"] = Id };
        if (Title != null) page["title"] = Title;
        if (Icon != null) page["icon"] = Icon;
        if (Fields != null && Fields.Length > 0) page["fields"] = new JArray(Fields);
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
    public JObject ToJson() => new JObject { ["heading"] = Heading, ["lines"] = new JArray(Lines.Select(l => l is UiLine line ? line.Json : (JToken)l.ToString())) };
}

/// <summary>One translated text for UiWindow.Lang: the section of the language file it goes into, its name there, its value.</summary>
public sealed class UiText
{
    public readonly string Section, Name; public readonly JToken Value;
    public UiText(string section, string name, JToken value) { Section = section; Name = name; Value = value; }
}

public sealed class UiLine
{
    public readonly JObject Json;
    public UiLine(JObject json) { Json = json; }
}

/// <summary>Answers the settings window's requests, which its generic settings action sends through this game's
/// arguments: "describe" (the table and the window), "test" (a Try button or a live field, with the window's current
/// values), "validate" (before a save; answer the problems, one per line, or nothing), "saved" (after a save, with the
/// saved values), "table" (the rows of a table field) and "emotes" (the emote sources, as JSON). The answers go back
/// through a non-persistent global; the describe answer through UiSchema.Var, the rest through UiHost.AnswerVar.
/// Every request must leave the game as it was: no chat output, no state written.</summary>
public static class UiHost
{
    public static string AnswerVar(string actionName) =>
        "uiframework.answer." + System.Text.RegularExpressions.Regex.Replace((actionName ?? "").Trim(), @"[^\w .-]", "_");

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
    public static bool Handle(IDictionary<string, object> args, IInlineInvokeProxy CPH, string actionName, string host, string version, string settingsFile,
        Func<string, UiSettings, string> test = null, Func<UiSettings, string> validate = null, Action<UiSettings> saved = null,
        Func<string, string> table = null, Func<string> emotes = null)
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
        catch (Exception ex)
        {
            CPH.LogError("[" + actionName + "] The settings window's request '" + request + "' failed: " + ex.Message);
            answer = request == "saved" ? null : "The game could not answer: " + ex.Message;
        }
        CPH.SetGlobalVar(string.IsNullOrEmpty(var) ? AnswerVar(actionName) : var, answer ?? "", false);
        return true;
    }

    private static string Arg(IDictionary<string, object> args, string key) => args != null && args.TryGetValue(key, out var value) ? value as string : null;
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
        catch (Exception) { return false; }
        if (string.IsNullOrWhiteSpace(json)) return false;
        try
        {
            var settings = JObject.Parse(json);
            string variable = (string)settings[variableKey];
            if (string.IsNullOrWhiteSpace(variable)) return false;
            Variable = variable.Trim();
            string name = (string)settings[nameKey];
            if (!string.IsNullOrWhiteSpace(name)) Name = name.Trim();
            Source = source;
            return true;
        }
        catch (Exception ex)
        {
            CPH.LogWarn("[UIFramework] The settings of " + source + " (" + global + ") could not be read: " + ex.Message);
            return false;
        }
    }

    public JObject ToJson() => new JObject { ["source"] = Source, ["variable"] = Variable, ["name"] = Name };
}

/// <summary>The settings file as the game reads it: every key through its default, old key names tolerated. Values
/// an alias stored in another scale (a fraction where percent is stored now) are converted on the way.</summary>
public sealed class UiSettings
{
    private readonly JObject _root;

    private UiSettings(JObject root) { _root = root; }

    /// <summary>Settings from JSON text, for tests and callers that already hold the values; empty text is empty settings.</summary>
    public static UiSettings Parse(string json)
    {
        try
        {
            using (var reader = new JsonTextReader(new StringReader(string.IsNullOrWhiteSpace(json) ? "{}" : json)) { DateParseHandling = DateParseHandling.None })
                return new UiSettings(JObject.Load(reader));
        }
        catch (Exception)
        {
            return new UiSettings(new JObject());
        }
    }

    /// <summary>The values as they are, for a host that reads them its own way.</summary>
    public JObject Raw => _root;

    /// <summary>Never throws: an unreadable file gives the defaults, and the game keeps running.</summary>
    public static UiSettings Load(string path)
    {
        try
        {
            if (!File.Exists(path)) return new UiSettings(new JObject());
            using (var reader = new JsonTextReader(new StringReader(ReadWithRetry(path))) { DateParseHandling = DateParseHandling.None })
                return new UiSettings(JObject.Load(reader));
        }
        catch (Exception)
        {
            return new UiSettings(new JObject());
        }
    }

    private static string ReadWithRetry(string path)
    {
        for (int attempt = 1; ; attempt++)
        {
            try { return File.ReadAllText(path); }
            catch (IOException) when (attempt < 5) { Thread.Sleep(40); }
        }
    }

    public JToken Token(string key)
    {
        var token = _root[key];
        if (token != null && token.Type != JTokenType.Null) return token;
        var setting = UiSchema.Find(key);
        if (setting?.Aliases != null)
            for (int i = 0; i < setting.Aliases.Length; i++)
            {
                // An alias with a dot reaches into an object of the old file ("PlatformEmotes.twitch").
                string alias = setting.Aliases[i];
                int dot = alias.IndexOf('.');
                var old = dot > 0 ? (_root[alias.Substring(0, dot)] as JObject)?[alias.Substring(dot + 1)] : _root[alias];
                if (old == null || old.Type == JTokenType.Null) continue;
                if (i == 0 && setting.AliasFactor.HasValue && (old.Type == JTokenType.Integer || old.Type == JTokenType.Float))
                    return Convert.ToDouble(((JValue)old).Value, CultureInfo.InvariantCulture) * setting.AliasFactor.Value;
                return old;
            }
        return setting?.Default == null ? null : JToken.FromObject(setting.Default);
    }

    public bool Bool(string key) { var t = Token(key); return t != null && t.Type == JTokenType.Boolean && (bool)t; }

    public long Long(string key)
    {
        var t = Token(key);
        if (t == null) return 0;
        if (t.Type == JTokenType.Integer || t.Type == JTokenType.Float) return Convert.ToInt64(((JValue)t).Value, CultureInfo.InvariantCulture);
        return long.TryParse(t.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out long l) ? l : 0;
    }

    public int Int(string key) { long l = Long(key); return l > int.MaxValue ? int.MaxValue : l < int.MinValue ? int.MinValue : (int)l; }

    public double Double(string key)
    {
        var t = Token(key);
        if (t == null) return 0;
        if (t.Type == JTokenType.Integer || t.Type == JTokenType.Float) return Convert.ToDouble(((JValue)t).Value, CultureInfo.InvariantCulture);
        return double.TryParse(t.ToString().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out double d) ? d : 0;
    }

    public string String(string key)
    {
        var t = Token(key);
        return t == null ? "" : t.Type == JTokenType.String ? (string)t : t.ToString();
    }

    /// <summary>A ceiling that stores "no limit" as 0: read as long.MaxValue.</summary>
    public long Ceiling(string key) { long l = Long(key); return l <= 0 ? long.MaxValue : l; }
    public int CeilingInt(string key) { long l = Long(key); return l <= 0 || l > int.MaxValue ? int.MaxValue : (int)l; }
    public static int Clamp(int value, int min, int max) => value < min ? min : value > max ? max : value;
    public static long Clamp(long value, long min, long max) => value < min ? min : value > max ? max : value;
    public static double Clamp(double value, double min, double max) => value < min ? min : value > max ? max : value;
}
// --- end UIFramework host block ---
