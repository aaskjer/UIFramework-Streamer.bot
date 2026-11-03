// --- UIFramework host block (keep identical in every action of this host that reads the settings) ---
// From UIFramework/stub/HostSchema.cs. The game declares its settings once in UiSchema.Table, reads them through
// UiSettings, and answers the window's describe request with UiSchema.ToJson(). One table, so the game and its
// window can never disagree. Compiles in any Streamer.bot action with no reference beyond Newtonsoft.Json.
//
// In Execute(), first thing:
//     if (args.ContainsKey("uiframework.describe"))
//     {
//         CPH.SetGlobalVar("uiframework.schema." + ActionName, UiSchema.ToJson("battleship", CurrentVersion, "Chat Games/Battleship Game/BSG-Settings.json"), false);
//         return true;
//     }
public static class UiSchema
{
    public sealed class Setting
    {
        public string Key; public object Default; public string Kind, Page, Section, Group, Label, Help, Unit, Requires, Confirm, Platform;
        public double? Min, Max, Factor; public bool UnlimitedAtZero, Hidden;
        public string[] Options, Placeholders, Aliases; public double? AliasFactor;
        // For a list: its columns as "key|kind|label" (kinds: string, text, int, double, bool, emote; an emote column
        // adds "|twitch", "|youtube" or "|kick" for its picture), and the file next to the settings file that holds
        // the rows, with the property in it (no field: the file is the array).
        public string[] Columns; public string File, Field;
        // Transient: shown, never saved. For an action: Live hands the host the window's current values, Panel shows
        // its answer under the button, Auto asks it on show and after every change without a button.
        public bool Transient, Live, Panel, Auto;
    }

    /// <summary>A setting: key and default, then whatever the conventions cannot know. Keys the conventions know
    /// (useBot, globalCooldown, blockedUsers, ...) need nothing but their default. An option is "value" or
    /// "value|label". aliasFactor multiplies the first alias's value on the move (a fraction to percent: 100).
    /// requires is a switch's key, or a condition on any field: "numberOfSlots>=5", "cpBetMode=fixed", "mode!=free".</summary>
    public static Setting S(string key, object @default, string page = null, string label = null, string help = null, string kind = null,
        string section = null, double? min = null, double? max = null, string unit = null, double? factor = null, bool unlimitedAtZero = false,
        string[] options = null, string requires = null, string confirm = null, bool hidden = false, string[] aliases = null, double? aliasFactor = null,
        string[] columns = null, string file = null, string field = null, bool transient = false, bool live = false, bool panel = false, bool auto = false,
        string platform = null)
    {
        return new Setting
        {
            Key = key, Default = @default, Page = page, Label = label, Help = help, Kind = kind, Section = section, Min = min, Max = max,
            Unit = unit, Factor = factor, UnlimitedAtZero = unlimitedAtZero, Options = options, Requires = requires, Confirm = confirm,
            Hidden = hidden, Aliases = aliases, AliasFactor = aliasFactor, Columns = columns, File = file, Field = field,
            Transient = transient, Live = live, Panel = panel, Auto = auto, Platform = platform
        };
    }

    /// <summary>A message template: group, key, default text, label, help, placeholders without braces, old key.</summary>
    public static Setting Msg(string group, string key, string @default, string label, string help, string[] placeholders, string alias = null)
        => new Setting { Key = key, Default = @default, Kind = "template", Page = "messages", Group = group, Label = label, Help = help,
                         Placeholders = placeholders, Aliases = alias == null ? null : new[] { alias } };

    public static string ToJson(string host, string version, string settingsFile)
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
            if (s.Field != null) item["field"] = s.Field;
            if (s.Aliases != null) item["aliases"] = new JArray(s.Aliases);
            if (s.AliasFactor.HasValue && s.Aliases != null && s.Aliases.Length > 0) item["aliasFactors"] = new JObject { [s.Aliases[0]] = s.AliasFactor.Value };
            settings.Add(item);
        }
        return new JObject { ["host"] = host, ["version"] = version, ["settingsFile"] = settingsFile, ["settings"] = settings }.ToString(Formatting.None);
    }

    public static Setting Find(string key)
    {
        foreach (var s in Table)
            if (s.Key == key) return s;
        return null;
    }

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
        S("freeGame", false, page: "game", label: "Free game", help: "Battles for bragging rights: ships still sink and the overlay still runs, but no points are won or lost. Chat gets the Free game messages instead, so nothing mentions points.", section: "Points", aliases: new[] { "FreeGame" }),
        S("pointsVariable", "points", page: "game", label: "Points variable", help: "The Streamer.bot user variable that holds each viewer's points. Currency Core by StreamUP and LOYALTY POINTS by tawmae are found on their own and win over this box; only change it for a points system of your own.", section: "Points", aliases: new[] { "PointsVariable" }),
        S("pointsName", "Points", page: "game", label: "Shown in chat as", help: "What {points} says in chat, like 'Coins' or 'Doubloons'. Only the wording changes; the points variable stays the same.", section: "Points", aliases: new[] { "PointsVarName" }),
        S("winBonus", 0L, page: "game", label: "Win bonus", help: "Paid on top when a player sinks the whole enemy fleet. 0 for none: a win already pays every ship it sank. The chat's battle is played for fun and pays nothing.", kind: "long", section: "Economy", min: 0.0, aliases: new[] { "WinBonus" }),
        S("minBalanceToPlay", 0L, page: "game", label: "Minimum balance", help: "What a player needs to start a battle. 0 asks for the value of their own fleet, so nobody plays a battle they couldn't pay for losing.", kind: "long", section: "Economy", min: 0.0, aliases: new[] { "MinBalanceToPlay" }),
        S("dailyWinCap", 0L, page: "game", label: "Daily winnings cap", help: "The most a player can win per day. 0 for no cap. Once it is reached, further wins pay nothing until the next day.", kind: "long", section: "Economy", min: 0.0, aliases: new[] { "DailyWinCap" }),
        S("chargeDamagedOnAbandon", true, page: "game", label: "Charge for damaged ships when a player runs out of time", help: "Without this, walking away one hit before a Carrier sinks dodges its whole value. A Carrier hit 4 times out of 5 costs 4/5 of it. Never applies during normal play.", section: "Economy", aliases: new[] { "ChargeDamagedOnAbandon" }),
        S("clampBalanceAtZero", true, page: "game", label: "Never let a balance go below zero", help: "A player who can't cover a loss is charged only what they have.", section: "Economy", aliases: new[] { "ClampBalanceAtZero" }),
        S("economyNote", null, page: "game", help: "Nothing is staked up front: sinking one of the Admiral's ships pays its value at once, and losing one of your own costs its value. The ship values are on the Fleet page.", kind: "note", section: "Economy"),
        S("admiralDifficulty", "captain", page: "game", label: "Skill", help: "Ensign fires at random and never repeats a square, about 59 shots to clear a Quick board. Captain fires at random until it lands a hit, then works the squares around it and follows a line of hits to both ends, about 36 shots. Admiral hunts like the Captain, and between hits only checks squares spaced to the shortest ship still afloat, so nothing can hide between them, about 30 shots.", kind: "options", section: "The Admiral", options: new[] { "ensign|Ensign", "captain|Captain", "admiral|Admiral" }, aliases: new[] { "AdmiralDifficulty" }),
        S("admiralBlunderChance", 0, page: "game", label: "Blunder chance", help: "How often the Admiral throws away its plan and fires blind, from 0 (never) to 100 (always). A handicap that works on any skill level without letting it cheat.", kind: "percent", section: "The Admiral", min: 0.0, max: 100.0, aliases: new[] { "AdmiralBlunderChance" }, aliasFactor: 100.0),
        S("admiralDelayMs", 700, page: "game", label: "Return fire delay", help: "Pause between a shot and the Admiral's reply, in milliseconds, so the overlay reads as an exchange rather than one flash. 0 to 5000.", section: "The Admiral", min: 0.0, max: 5000.0, unit: "ms", aliases: new[] { "AdmiralDelayMs" }),
        S("admiralNote", null, page: "game", help: "The Admiral never sees your board. It only knows where it has fired, what it found there and which ship lengths are still afloat, the same things you know about its fleet.", kind: "note", section: "The Admiral"),
        S("deployTimeoutSec", 60, page: "game", label: "To open fire", help: "From starting a battle to the first shot. Nothing has sunk yet, so running out here costs nothing. At least 5.", kind: "duration", section: "Battle clock", min: 5.0, aliases: new[] { "DeployTimeoutSec" }),
        S("turnTimeout", 90, page: "game", label: "Per turn", help: "From the Admiral's reply to the next shot. Running out ends the battle and settles whatever has sunk. There is no command to leave a battle: this clock is the way out. At least 5.", kind: "duration", section: "Battle clock", min: 5.0, aliases: new[] { "TurnTimeoutSec" }),
        S("turnWarnFirstSec", 30, page: "game", label: "First warning", help: "Seconds left on the turn when chat is warned and the countdown ring turns amber. 0 turns it off.", kind: "duration", section: "Battle clock", min: 0.0, aliases: new[] { "TurnWarnFirstSec" }),
        S("turnWarnSecondSec", 15, page: "game", label: "Final warning", help: "Seconds left on the turn when chat is warned again and the ring pulses red. 0 turns it off.", kind: "duration", section: "Battle clock", min: 0.0, aliases: new[] { "TurnWarnSecondSec" }),
        S("clockSummary", null, page: "game", label: "The clock in short", help: "What the four values above add up to, as they stand.", kind: "action", section: "Battle clock", live: true, panel: true, auto: true),
        // ---- fleet ----
        S("presetBlitz", null, page: "fleet", label: "Blitz · 6×6 · 3 ships", help: "A 6×6 board with a Carrier of 4, a Cruiser of 3 and a Destroyer of 2. Fills in the board and the ships; nothing is saved until you save.", kind: "action", section: "Presets"),
        S("presetQuick", null, page: "fleet", label: "Quick · 8×8 · 3 ships", help: "An 8×8 board with a Carrier of 5, a Cruiser of 3 and a Destroyer of 2: the default, about 36 shots per battle.", kind: "action", section: "Presets"),
        S("presetClassic", null, page: "fleet", label: "Classic · 10×10 · 5 ships", help: "The classic 10×10 board with all five ships.", kind: "action", section: "Presets"),
        S("presetNote", null, page: "fleet", help: "A preset fills in the board size and which ships sail, with their lengths. Change anything afterwards as you like.", kind: "note", section: "Presets"),
        S("gridSize", 8, page: "fleet", label: "Grid size", help: "Squares per side, from 5 (A1 to E5) to 12 (A1 to L12). Bigger boards make longer battles.", section: "Board", options: new[] { "5|5 × 5", "6|6 × 6", "7|7 × 7", "8|8 × 8", "9|9 × 9", "10|10 × 10", "11|11 × 11", "12|12 × 12" }, aliases: new[] { "GridSize" }),
        S("noTouchingRule", true, page: "fleet", label: "Ships may not touch each other", help: "Keeps a one-square gap around every ship, so two ships never read as one long one and hunting is fairer.", section: "Board", aliases: new[] { "NoTouchingRule" }),
        S("shipCarrierEnabled", true, page: "fleet", label: "Carrier", help: "Sail with a Carrier. Both fleets always have the same ships.", section: "Ships", aliases: new[] { "ShipCarrierEnabled" }),
        S("shipCarrierLength", 5, page: "fleet", label: "Carrier length", help: "How many squares the Carrier takes up.", section: "Ships", min: 1.0, max: 12.0, requires: "shipCarrierEnabled", aliases: new[] { "ShipCarrierLength" }),
        S("shipCarrierValue", 500L, page: "fleet", label: "Carrier value", help: "What sinking the Carrier pays, and what losing it costs.", kind: "long", section: "Ships", min: 0.0, requires: "shipCarrierEnabled", aliases: new[] { "ShipCarrierValue" }),
        S("shipBattleshipEnabled", false, page: "fleet", label: "Battleship", help: "Sail with a Battleship.", section: "Ships", aliases: new[] { "ShipBattleshipEnabled" }),
        S("shipBattleshipLength", 4, page: "fleet", label: "Battleship length", help: "How many squares the Battleship takes up.", section: "Ships", min: 1.0, max: 12.0, requires: "shipBattleshipEnabled", aliases: new[] { "ShipBattleshipLength" }),
        S("shipBattleshipValue", 400L, page: "fleet", label: "Battleship value", help: "What sinking the Battleship pays, and what losing it costs.", kind: "long", section: "Ships", min: 0.0, requires: "shipBattleshipEnabled", aliases: new[] { "ShipBattleshipValue" }),
        S("shipCruiserEnabled", true, page: "fleet", label: "Cruiser", help: "Sail with a Cruiser.", section: "Ships", aliases: new[] { "ShipCruiserEnabled" }),
        S("shipCruiserLength", 3, page: "fleet", label: "Cruiser length", help: "How many squares the Cruiser takes up.", section: "Ships", min: 1.0, max: 12.0, requires: "shipCruiserEnabled", aliases: new[] { "ShipCruiserLength" }),
        S("shipCruiserValue", 300L, page: "fleet", label: "Cruiser value", help: "What sinking the Cruiser pays, and what losing it costs.", kind: "long", section: "Ships", min: 0.0, requires: "shipCruiserEnabled", aliases: new[] { "ShipCruiserValue" }),
        S("shipSubmarineEnabled", false, page: "fleet", label: "Submarine", help: "Sail with a Submarine.", section: "Ships", aliases: new[] { "ShipSubmarineEnabled" }),
        S("shipSubmarineLength", 3, page: "fleet", label: "Submarine length", help: "How many squares the Submarine takes up.", section: "Ships", min: 1.0, max: 12.0, requires: "shipSubmarineEnabled", aliases: new[] { "ShipSubmarineLength" }),
        S("shipSubmarineValue", 300L, page: "fleet", label: "Submarine value", help: "What sinking the Submarine pays, and what losing it costs.", kind: "long", section: "Ships", min: 0.0, requires: "shipSubmarineEnabled", aliases: new[] { "ShipSubmarineValue" }),
        S("shipDestroyerEnabled", true, page: "fleet", label: "Destroyer", help: "Sail with a Destroyer.", section: "Ships", aliases: new[] { "ShipDestroyerEnabled" }),
        S("shipDestroyerLength", 2, page: "fleet", label: "Destroyer length", help: "How many squares the Destroyer takes up.", section: "Ships", min: 1.0, max: 12.0, requires: "shipDestroyerEnabled", aliases: new[] { "ShipDestroyerLength" }),
        S("shipDestroyerValue", 200L, page: "fleet", label: "Destroyer value", help: "What sinking the Destroyer pays, and what losing it costs.", kind: "long", section: "Ships", min: 0.0, requires: "shipDestroyerEnabled", aliases: new[] { "ShipDestroyerValue" }),
        S("fleetSummary", null, page: "fleet", label: "The fleet in short", help: "The board, the ships that sail and about how many shots a battle takes, as the values stand.", kind: "action", section: "Ships", live: true, panel: true, auto: true),
        S("fleetNote", null, page: "fleet", help: "Both sides always sail the same fleet. A ship's value is what sinking it pays and what losing it costs, settled the moment it goes down.", kind: "note", section: "Ships"),
        // ---- modes ----
        S("soloEnabled", true, page: "modes", label: "Solo: one viewer against the Admiral", help: "Started with !battleship. Each player has their own battle and settles every sinking as it happens.", section: "Game modes", aliases: new[] { "SoloEnabled" }),
        S("chatBattleEnabled", true, page: "modes", label: "Chat vs the Admiral: the whole chat votes on every shot", help: "Started with !seeschlacht. Anybody can take part, by clicking on stream or with !fire. Played for fun: no points.", section: "Game modes", aliases: new[] { "ChatBattleEnabled" }),
        S("duelEnabled", true, page: "modes", label: "Team duel: one chat team against another", help: "A viewer challenges another, both sides take on players, and the winning side shares everyone's entry.", section: "Game modes", aliases: new[] { "DuelEnabled" }),
        S("chatNote", null, page: "modes", help: "The whole chat is one fleet, played for fun: nobody pays and nobody wins points. Each round every viewer has one vote, a click on enemy waters or !fire B7, and when the round ends the square with the most votes is fired at.", kind: "note", section: "Chat vs the Admiral"),
        S("chatTeamName", "Chat", page: "modes", label: "Team name", help: "What the chat's fleet is called on the overlay and in chat, as {team}.", section: "Chat vs the Admiral", aliases: new[] { "ChatTeamName" }),
        S("chatRoundSec", 15, page: "modes", label: "Seconds per round", help: "Make it longer than your stream delay, or viewers are still voting on a picture from before the last shot. At least 5.", kind: "duration", section: "Chat vs the Admiral", min: 5.0, aliases: new[] { "ChatRoundSec" }),
        S("chatNote2", null, page: "modes", help: "Every viewer's click counts, logged in to Twitch or not - nothing needs linking. Somebody logged out can vote again by reloading the page, which does no harm in a battle played for fun. A chat battle starts only when no other battle is running, and if nobody votes for as long as the turn clock allows, the chat breaks it off.", kind: "note", section: "Chat vs the Admiral"),
        S("duelNote", null, page: "modes", help: "A viewer challenges another by name, or anybody with no name, and captains Team A; whoever accepts captains Team B. Everyone else then picks a side, and every member pays the entry fee. The sides take turns, one voting round each, and the side that sinks the other's fleet shares the pot. Each winner gets their own entry back whole; only the winnings on top take the Addicted cut and the daily cap.", kind: "note", section: "Team duel"),
        S("duelEntryFee", 100L, page: "modes", label: "Entry fee", help: "What every member pays to join a side. 0 for a duel played for the record alone. With Free game on, nobody pays.", kind: "long", section: "Team duel", min: 0.0, aliases: new[] { "DuelEntryFee" }),
        S("duelAcceptSec", 60, page: "modes", label: "Seconds to accept", help: "A challenge nobody takes up in time is dropped and the entry refunded. At least 10.", kind: "duration", section: "Team duel", min: 10.0, aliases: new[] { "DuelAcceptSec" }),
        S("duelJoinSec", 60, page: "modes", label: "Seconds to sign up", help: "From the moment the challenge is accepted. At least 10.", kind: "duration", section: "Team duel", min: 10.0, aliases: new[] { "DuelJoinSec" }),
        S("duelRoundSec", 20, page: "modes", label: "Seconds per round", help: "One round is one side's shot. Longer than your stream delay, as for the chat's battle. At least 5.", kind: "duration", section: "Team duel", min: 5.0, aliases: new[] { "DuelRoundSec" }),
        S("duelMaxTeamGap", 0, page: "modes", label: "Lead limit (0 = none)", help: "How many more players one side may have than the other. With 1, a side is never more than one player ahead. 0 lets anybody join either side.", section: "Team duel", min: 0.0, aliases: new[] { "DuelMaxTeamGap" }),
        S("duelMaxMissedTurns", 3, page: "modes", label: "Turns a side may pass", help: "A side that lets this many turns in a row pass without a vote gives up, and the other side wins. If nobody fired at all, the duel is called off and every entry refunded. At least 1.", section: "Team duel", min: 1.0, aliases: new[] { "DuelMaxMissedTurns" }),
        S("duelNote2", null, page: "modes", help: "A duel can be challenged only while nothing else runs, and nothing else starts until its last shot. Anonymous Heat clicks never count in a duel, so members vote with !fire or link their clicks once with !fire link.", kind: "note", section: "Team duel"),
        S("stopNote", null, page: "modes", help: "The stop command ends whatever is running, solo battles, the chat's battle or a duel, and pays nothing more out: no win bonus, no pot, no charge for damaged ships. A duel's entries are refunded. Who may use it is set on the command in Streamer.bot.", kind: "note", section: "Stopping a game"),
        // ---- overlay ----
        S("overlayNote", null, page: "overlay", help: "The overlay is battleship_overlay.html in the folder below. The game writes it there and keeps it up to date, saving your previous copy as a .bak. Its background is transparent, so it layers over any scene.", kind: "note", section: "Browser source"),
        S("overlayFolder", "Overlay", page: "overlay", label: "Overlay folder", help: "Relative to the Battleship Game folder, or a full path on any drive. Empty uses 'Overlay'.", kind: "path", section: "Browser source", aliases: new[] { "OverlayFolder" }),
        S("obsNote", null, page: "overlay", help: "Creates the scene, and in it a browser source the size of your canvas pointed at the overlay file. OBS has to be connected in Streamer.bot under Stream Apps > OBS Studio.", kind: "note", section: "OBS auto-setup"),
        S("obsConnectionId", 0, page: "overlay", label: "Connection #", help: "Which of Streamer.bot's OBS connections to use, if you have more than one. 0 is the first.", section: "OBS auto-setup", min: 0.0, aliases: new[] { "ObsConnectionId" }),
        S("obsSceneName", "Battleship", page: "overlay", label: "Scene", help: "The scene the source is added to. It is created if it doesn't exist yet.", section: "OBS auto-setup", aliases: new[] { "ObsSceneName" }),
        S("obsSourceName", "Battleship Overlay", page: "overlay", label: "Source", help: "The browser source created in that scene. Nothing is touched if OBS already has a source of this name.", section: "OBS auto-setup", aliases: new[] { "ObsSourceName" }),
        S("setupObsOverlay", null, page: "overlay", label: "Set the overlay up in OBS", help: "Writes the overlay file if it is missing, then creates the scene and the browser source, the size of your canvas. Save first: the setup reads the saved folder and names.", kind: "action", section: "OBS auto-setup", confirm: "This creates the scene if it is missing and adds a browser source the size of your canvas pointing at the overlay file, over the OBS connection set above. Save your changes first if you edited the folder or the names. Go ahead?"),
        S("overlayScale", 100, page: "overlay", label: "Scale", help: "Scales the whole overlay. 100 is full size.", kind: "percent", section: "Size", min: 10.0, max: 300.0, aliases: new[] { "OverlayScale" }, aliasFactor: 100.0),
        S("overlayFontScale", 100, page: "overlay", label: "Text size", help: "Scales the text only, on top of the overall scale.", kind: "percent", section: "Size", min: 10.0, max: 300.0, aliases: new[] { "OverlayFontScale" }, aliasFactor: 100.0),
        S("overlayResultSeconds", 8, page: "overlay", label: "Result shown", help: "How long a finished battle stays up before it fades out. At least 1.", kind: "duration", section: "Size", min: 1.0, aliases: new[] { "OverlayGameOverFadeSec" }),
        S("overlayAdmiralName", "The Admiral", page: "overlay", label: "Opponent name", help: "What the CPU is called on the overlay and in chat, as {admiral}.", section: "Content", aliases: new[] { "OverlayAdmiralName" }),
        S("overlayShowPlayerRoster", true, page: "overlay", label: "The player's fleet roster", help: "The ship list with damage pips under the player's own board.", section: "Content", aliases: new[] { "OverlayShowPlayerRoster", "OverlayShowRoster" }),
        S("overlayShowEnemyRoster", true, page: "overlay", label: "The opponent's fleet roster", help: "The same list under enemy waters. Its pips show which of their ships your hits landed on; switch it off to keep that hidden. Whichever roster is hidden, the two boards stay level.", section: "Content", aliases: new[] { "OverlayShowEnemyRoster" }),
        S("overlayShowCoords", true, page: "overlay", label: "Coordinates", help: "The letters and numbers along the boards.", section: "Content", aliases: new[] { "OverlayShowCoords" }),
        S("overlayShowTurnTimer", true, page: "overlay", label: "Turn countdown ring", help: "Turns amber at the first warning and pulses red at the final one.", section: "Content", aliases: new[] { "OverlayShowTurnTimer" }),
        S("overlayShowScore", true, page: "overlay", label: "Running score and accuracy", help: "Shots, hits and the battle's running total.", section: "Content", aliases: new[] { "OverlayShowScore" }),
        S("overlayAnimations", true, page: "overlay", label: "Animations", help: "Impact bursts, the marker on the last shot and the pulsing final-warning ring.", section: "Content", aliases: new[] { "OverlayAnimationsEnabled" }),
        S("overlayHideOnFreeGame", false, page: "overlay", label: "Hide the overlay during Free game", help: "Nothing is drawn while Free game is on.", section: "Content", aliases: new[] { "OverlayHideOnFreeGame" }),
        S("overlayHitColour", "#ff5a3c", page: "overlay", label: "Hit", help: "A square where a shot hit a ship.", kind: "colour", section: "Colours", aliases: new[] { "OverlayHitColor" }),
        S("overlayMissColour", "#8fb8d8", page: "overlay", label: "Miss", help: "A square where a shot found only water.", kind: "colour", section: "Colours", aliases: new[] { "OverlayMissColor" }),
        S("overlaySunkColour", "#d3283a", page: "overlay", label: "Sunk", help: "A ship that has gone down.", kind: "colour", section: "Colours", aliases: new[] { "OverlaySunkColor" }),
        S("overlayShipColour", "#5b6b7c", page: "overlay", label: "Ship hull", help: "Your own ships on the left-hand board.", kind: "colour", section: "Colours", aliases: new[] { "OverlayShipColor" }),
        S("overlayGridColour", "#4a7fa5", page: "overlay", label: "Grid lines", help: "The lines between the squares.", kind: "colour", section: "Colours", aliases: new[] { "OverlayGridColor" }),
        S("overlayWaterColour", "#0d2438", page: "overlay", label: "Water", help: "The squares nobody has fired at.", kind: "colour", section: "Colours", aliases: new[] { "OverlayWaterColor" }),
        S("overlayBackgroundColour", "#000000", page: "overlay", label: "Background", help: "The panel behind the boards, as solid as the opacity below makes it.", kind: "colour", section: "Colours", aliases: new[] { "OverlayBackgroundColor" }),
        S("overlayBackgroundOpacity", 0, page: "overlay", label: "Background opacity", help: "0 keeps the overlay fully transparent, which is what most scenes want.", kind: "percent", section: "Colours", min: 0.0, max: 100.0, factor: 1.0, aliases: new[] { "OverlayBackgroundOpacity" }, aliasFactor: 100.0),
        S("coloursNote", null, page: "overlay", help: "Your own fleet is drawn in full and the Admiral's waters stay fogged until something is hit. The Admiral reads neither chat nor the overlay, so showing your ships gives nothing away.", kind: "note", section: "Colours"),
        // ---- clicks ----
        S("clicksNote", null, page: "clicks", help: "Heat is a free Twitch extension that passes viewers' clicks on your video to the overlay. With this on, the player whose battle is on screen can fire by clicking a square in enemy waters, and !fire keeps working as before.", kind: "note", section: "Click to Fire"),
        S("heatEnabled", false, page: "clicks", label: "Fire by clicking the stream", help: "Twitch only: Heat is a Twitch extension. Needs Heat on your channel and Streamer.bot's WebSocket server running, see Setup below.", section: "Click to Fire", aliases: new[] { "HeatEnabled" }),
        S("heatAutoLink", true, page: "clicks", label: "First click takes control", help: "The first click in a player's battle takes control: Heat doesn't say who clicked, so the first viewer to click enemy waters in a player's battle is taken to be that player, for that battle. It holds back when another viewer clicked there in the last 20 seconds, or when the clicker is linked to a different player. A player whose battle was taken over clicks once and types !fire link, which also makes a link that lasts. Off, every player links with !fire link.", section: "Click to Fire", aliases: new[] { "HeatAutoLink" }),
        S("setupNote", null, page: "clicks", help: "1. Install Heat on your channel and activate it as an Overlay extension: https://dashboard.twitch.tv/extensions/cr20njfkgll4okyrhag7xxph270sqk\n2. Start Streamer.bot's WebSocket server: Servers/Clients > WebSocket Server, tick Auto Start, then Start Server. The overlay hands every click to the Battleship Game action through it.\n3. Players just click. If someone else's click got in first, or the switch above is off, the player clicks once and types !fire link; that link is remembered for later battles.\nHeat works in desktop browsers only: Twitch shows no overlay extensions in its mobile app, so phone viewers keep using !fire.", kind: "note", section: "Setup"),
        S("heatChannelId", "", page: "clicks", label: "Twitch channel ID", help: "The number Heat files your viewers' clicks under: your channel's user ID, not its name. Leave it empty to use the broadcaster account connected to Streamer.bot.", section: "Connection", aliases: new[] { "HeatChannelId" }),
        S("heatStreamerBotUrl", "ws://127.0.0.1:8080/", page: "clicks", label: "Streamer.bot WebSocket", help: "Where the overlay reaches Streamer.bot: the address and port under Servers/Clients > WebSocket Server. Usually ws://127.0.0.1:8080/", section: "Connection", aliases: new[] { "HeatStreamerBotUrl" }),
        S("broadcasterFound", null, page: "clicks", label: "The broadcaster account", help: "The Twitch account connected to Streamer.bot, used whenever the channel ID box is empty.", kind: "action", section: "Connection", live: true, panel: true, auto: true),
        S("heatStreamerBotPassword", "", page: "clicks", label: "WebSocket password", help: "Only if you switched on authentication for the WebSocket server. It is stored in plain text next to the overlay, because the overlay has to read it.", section: "Connection", aliases: new[] { "HeatStreamerBotPassword" }),
        S("alignNote", null, page: "clicks", help: "Heat reports each click as a spot on the whole stream picture. That lines up by itself when the overlay's browser source covers the whole canvas, which is how OBS auto-setup creates it. If you moved or resized the source, say where it sits in percent of the canvas: a source shrunk to half size in the bottom-right corner is 50 / 50 / 50 / 50.", kind: "note", section: "Lining up the clicks"),
        S("heatAreaLeft", 0, page: "clicks", label: "Left", help: "How far from the left edge of the canvas the source starts.", kind: "percent", section: "Lining up the clicks", min: 0.0, max: 100.0, aliases: new[] { "HeatAreaLeft" }, aliasFactor: 100.0),
        S("heatAreaTop", 0, page: "clicks", label: "Top", help: "How far from the top of the canvas the source starts.", kind: "percent", section: "Lining up the clicks", min: 0.0, max: 100.0, aliases: new[] { "HeatAreaTop" }, aliasFactor: 100.0),
        S("heatAreaWidth", 100, page: "clicks", label: "Width", help: "How much of the canvas width the source covers. 100 is all of it.", kind: "percent", section: "Lining up the clicks", min: 1.0, max: 400.0, aliases: new[] { "HeatAreaWidth" }, aliasFactor: 100.0),
        S("heatAreaHeight", 100, page: "clicks", label: "Height", help: "How much of the canvas height the source covers. 100 is all of it.", kind: "percent", section: "Lining up the clicks", min: 1.0, max: 400.0, aliases: new[] { "HeatAreaHeight" }, aliasFactor: 100.0),
        S("heatShowClicks", false, page: "clicks", label: "Show every click on the overlay (alignment test)", help: "Draws a marker wherever anyone clicks, with the connection status in the top-left corner. Viewers see it too, so switch it off once the markers land where you click.", section: "Lining up the clicks", aliases: new[] { "HeatShowClicks" }),
        S("alignNote2", null, page: "clicks", help: "To check, switch the test on and save, then while you are live open your channel in a browser and click around the video. Each marker should land under the pointer. The player's own clicks show in gold, named after the square they fire at.", kind: "note", section: "Lining up the clicks"),
        S("markersNote", null, page: "clicks", help: "Optional. Heat's own Markers overlay pins a marker where each viewer last clicked, so everyone sees the clicks land. The button adds it to the scene named on the Overlay page, at the size of your canvas, over the same OBS connection.", kind: "note", section: "Heat's click markers"),
        S("heatObsSourceName", "Heat Click Markers", page: "clicks", label: "Markers source", help: "The browser source created for Heat's markers. Nothing is touched if OBS already has a source of this name.", section: "Heat's click markers", aliases: new[] { "HeatObsSourceName" }),
        S("addHeatMarkers", null, page: "clicks", label: "Add to OBS", help: "Adds a browser source showing https://heat.j38.net/markers/?channel=<your channel ID> to the overlay's scene. Save first: it reads the saved channel ID and names.", kind: "action", section: "Heat's click markers", confirm: "This adds a browser source showing Heat's click markers for your channel to the overlay's scene (created if it is missing), at the size of your canvas, over the OBS connection on the Overlay page. Save first if you changed the channel ID or the names. Go ahead?"),
        S("markersNote2", null, page: "clicks", help: "What chat is told about linking is on the Messages page, under Click to Fire.", kind: "note", section: "Heat's click markers"),
        // ---- access ----
        S("platformsNote", null, page: "access", help: "Twitch, YouTube and Kick all play, and every reply goes to the chat the command came from. To leave a platform out, untick it on the game's commands in Streamer.bot, for example 'Kick Message'. Click to Fire and the fleet whisper work on Twitch only.", kind: "note", section: "Platforms"),
        S("whisperOwnFleet", true, page: "access", label: "Whisper each player their fleet", help: "Twitch only: Streamer.bot can't whisper on YouTube or Kick. Twitch drops whispers from accounts without a verified phone number; a failure is logged and the battle goes on. The overlay already shows the player their fleet, so this is a private second copy.", section: "Platforms", aliases: new[] { "WhisperOwnFleet" }),
        S("globalCooldown", 0, page: "access", label: "Everyone", help: "How long everyone waits after anybody starts a battle with !battleship, in seconds. 0 turns it off. Saving writes it onto the command in Streamer.bot.", section: "Cooldowns", min: 0.0, aliases: new[] { "GlobalCooldown" }),
        S("userCooldown", 0, page: "access", label: "Per viewer", help: "How long one viewer waits before starting their next battle, in seconds. 0 turns it off. Saving writes it onto the command in Streamer.bot.", section: "Cooldowns", min: 0.0, aliases: new[] { "UserCooldown" }),
        S("maxGamesPerHour", 0, page: "access", label: "Battles per hour (0 = none)", help: "How many battles one viewer may start per hour. 0 for no limit.", section: "Cooldowns", min: 0.0, unlimitedAtZero: true, aliases: new[] { "MaxGamesPerHour" }),
        S("cooldownNote", null, page: "access", help: "What chat is told while a cooldown runs is on the Messages page, under Cooldown.", kind: "note", section: "Cooldowns"),
        S("blockedUsers", "", page: "access", label: "Blocked viewers", help: "Viewers who can't play at all, by their user name.", section: "Who may not play", aliases: new[] { "BlockedUsers" }),
        S("blockedGroups", "ExampleGroup, TooAddictedToPlay, YourCustomBlockedGroup", page: "access", label: "Blocked groups", help: "Streamer.bot user groups whose members can't play. Names must match the groups exactly.", section: "Who may not play", aliases: new[] { "BlockedGroups" }),
        S("addictedUsers", "", page: "access", label: "Addicted viewers", help: "Viewers who play as usual but win less: their winnings are cut by the Addicted cut below.", section: "Addicted", aliases: new[] { "AddictedUsers" }),
        S("addictedGroups", "", page: "access", label: "Addicted groups", help: "Members of these Streamer.bot groups count as addicted too.", section: "Addicted", aliases: new[] { "AddictedGroups" }),
        S("addictedPayoutCut", 50, page: "access", label: "Addicted cut", help: "How much of an addicted viewer's winnings is held back, from 0 (nothing) to 100 (all of it). 50 means they earn half. Losses are unaffected.", kind: "percent", section: "Addicted", min: 0.0, max: 100.0, aliases: new[] { "AddictedPayoutCut" }, aliasFactor: 100.0),
        // ---- stats ----
        S("leaderboardRankBy", "net", page: "stats", label: "Rank players by", help: "What !ballstats ranks by. Net is the fair one, since losing ships costs points.", kind: "options", section: "Ranking", options: new[] { "net|Net: everything won minus everything lost", "won|Points won, ignoring losses", "sunk|Ships sunk", "accuracy|Accuracy, from 25 shots on" }, aliases: new[] { "LeaderboardRankBy" }),
        S("leaderboardSize", 5, page: "stats", label: "Places shown", help: "How many players !ballstats lists.", section: "Ranking", options: new[] { "1", "2", "3", "4", "5", "6", "7", "8", "9", "10" }, aliases: new[] { "LeaderboardSize" }),
        S("leaderboardShowMedals", true, page: "stats", label: "Medals for the top three", help: "🥇🥈🥉 in front of the first three places.", section: "Ranking", aliases: new[] { "LeaderboardShowMedals" }),
        S("statsNote", null, page: "stats", help: "Every tracked value is kept in a persisted bsg_* user variable, so any other Streamer.bot action can read a player's record.", kind: "note", section: "Ranking"),
        S("leaderboardExcludeBroadcaster", true, page: "stats", label: "Hide the broadcaster", help: "Matched by user ID, so a name change can't put them back on the board.", section: "Who appears", aliases: new[] { "LeaderboardExcludeBroadcaster" }),
        S("leaderboardExcludeBot", true, page: "stats", label: "Hide the bot account", help: "Matched by user ID.", section: "Who appears", aliases: new[] { "LeaderboardExcludeBot" }),
        S("leaderboardUseBot", true, page: "stats", label: "Bot account", help: "Send the leaderboard from your Streamer.bot bot account instead of the broadcaster account.", section: "Leaderboard sent as", aliases: new[] { "LeaderboardUseBot" }),
        S("leaderboardSendAction", false, page: "stats", label: "Send as /me", help: "Twitch only: YouTube and Kick have no /me, so there the leaderboard always goes out as plain chat. On Twitch, sends it as a /me action.", section: "Leaderboard sent as", aliases: new[] { "LeaderboardSendAction" }),
        S("sentNote", null, page: "stats", help: "The !bstats and !ballstats replies are on the Messages page, under Stats, and go out like the game's messages.", kind: "note", section: "Leaderboard sent as"),
        S("leaderboardAutoReset", "off", page: "stats", label: "Automatic reset", help: "Clears the leaderboard on its own when the period rolls over, using the scope below. Switching it on never wipes anything at once: the current period is recorded first, and the reset happens at the next boundary.", kind: "options", section: "Seasons", options: new[] { "off|Off", "weekly|Weekly, on Mondays", "monthly|Monthly, on the 1st" }, aliases: new[] { "LeaderboardAutoReset" }),
        S("leaderboardResetScope", "totals", page: "stats", label: "What a reset clears", help: "Totals only: points won, points lost and net. Battles, streaks and records are kept. Everything: every statistic of every player, a clean slate.", kind: "options", section: "Seasons", options: new[] { "totals|Totals only", "all|Everything" }, aliases: new[] { "LeaderboardResetScope" }),
        S("resetLeaderboard", null, page: "stats", label: "Reset the leaderboard now", help: "Clears what 'What a reset clears' says, for every player, straight away. Save first: the reset reads the saved scope.", kind: "action", section: "Seasons", confirm: "This permanently clears the leaderboard for every player according to the reset scope above (saved). It cannot be undone. Continue?"),
        // ---- log ----
        S("logToFile", true, page: "log", label: "Write a log file per day", help: "Writes every finished battle - win, loss, timeout, the chat's battle and the duel - to a dated 'BSG-log-yyyy-MM-dd.txt' in the Logs folder, one file per day.", aliases: new[] { "LogToFile" }),
        // ---- messages ----
        Msg("Solo battle", "msgGameStart", "🚢 {user} puts to sea against {admiral}! {ships} ships on a {size}x{size} grid. Fire with !fire B7 — {deploy}s to open fire.", "Battle started", "Posted when a player starts a battle with !battleship.", new[] { "user", "admiral", "ships", "size", "deploy" }, alias: "MsgGameStart"),
        Msg("Solo battle", "msgFleetRerolled", "🔄 {user}, your fleet has been redeployed.", "Fleet redeployed", "Confirms !fleet, which redeploys the ships before the first shot.", new[] { "user" }, alias: "MsgFleetRerolled"),
        Msg("Solo battle", "msgPlayerMiss", "🌊 {user} fires at {coord} — splash, nothing there.", "Miss", "The player's shot found only water.", new[] { "user", "coord", "admiral" }, alias: "MsgPlayerMiss"),
        Msg("Solo battle", "msgPlayerHit", "💥 {user} fires at {coord} — HIT!", "Hit", "The player's shot hit a ship.", new[] { "user", "coord", "admiral" }, alias: "MsgPlayerHit"),
        Msg("Solo battle", "msgPlayerSunk", "🔥 {user} SINKS the {admiral}'s {ship} at {coord}! +{amount} {points} — {enemyleft} enemy ships left.", "Enemy ship sunk", "The player sank one of the Admiral's ships. The points are paid before this is posted.", new[] { "user", "coord", "ship", "admiral", "amount", "points", "enemyleft" }, alias: "MsgPlayerSunk"),
        Msg("Solo battle", "msgPlayerSunkFree", "🔥 {user} SINKS the {admiral}'s {ship} at {coord}! {enemyleft} enemy ships left.", "Enemy ship sunk (Free game)", "Used instead while Free game is on, so nothing mentions points.", new[] { "user", "coord", "ship", "admiral", "enemyleft" }, alias: "MsgPlayerSunkFree"),
        Msg("Solo battle", "msgBoardStatus", "🎯 {user}: {shots} shots, {hits} hits ({accuracy}%). Your fleet: {yourleft} — {admiral}'s fleet: {enemyleft}. Running total: {net} {points}.", "Board status", "Posted by !board during a solo battle.", new[] { "user", "admiral", "shots", "hits", "accuracy", "yourleft", "enemyleft", "net", "points" }, alias: "MsgBoardStatus"),
        Msg("Return fire", "msgAdmiralMiss", "🌊 {admiral} returns fire at {coord} — miss.", "Miss", "The Admiral's shot found only water.", new[] { "user", "coord", "admiral" }, alias: "MsgAdmiralMiss"),
        Msg("Return fire", "msgAdmiralHit", "💢 {admiral} returns fire at {coord} — your ship is hit!", "Hit", "The Admiral hit one of the player's ships.", new[] { "user", "coord", "admiral" }, alias: "MsgAdmiralHit"),
        Msg("Return fire", "msgAdmiralSunk", "☠️ {admiral} sinks your {ship} at {coord}! −{amount} {points} — {yourleft} of your ships left.", "Your ship sunk", "The Admiral sank one of the player's ships. The points are taken before this is posted.", new[] { "user", "coord", "ship", "admiral", "amount", "points", "yourleft" }, alias: "MsgAdmiralSunk"),
        Msg("Return fire", "msgAdmiralSunkFree", "☠️ {admiral} sinks your {ship} at {coord}! {yourleft} of your ships left.", "Your ship sunk (Free game)", "Used instead while Free game is on.", new[] { "user", "coord", "ship", "admiral", "yourleft" }, alias: "MsgAdmiralSunkFree"),
        Msg("Battle result", "msgWin", "🏆 {user} has sunk the {admiral}'s entire fleet in {shots} shots! Net for the battle: {net} {points}.", "Win", "{net} is the battle's total, everything sunk minus everything lost, with its sign.", new[] { "user", "admiral", "shots", "net", "points" }, alias: "MsgWin"),
        Msg("Battle result", "msgWinFree", "🏆 {user} has sunk the {admiral}'s entire fleet in {shots} shots!", "Win (Free game)", "Used instead while Free game is on.", new[] { "user", "admiral", "shots" }, alias: "MsgWinFree"),
        Msg("Battle result", "msgLoss", "💀 {admiral} has sunk {user}'s fleet. Net for the battle: {net} {points}.", "Loss", "{net} is the battle's total, with its sign.", new[] { "user", "admiral", "shots", "net", "points" }, alias: "MsgLoss"),
        Msg("Battle result", "msgLossFree", "💀 {admiral} has sunk {user}'s fleet. Better luck next time.", "Loss (Free game)", "Used instead while Free game is on.", new[] { "user", "admiral", "shots" }, alias: "MsgLossFree"),
        Msg("Battle result", "msgTurnTimeout", "⏰ {user} abandoned the battle. Net for the battle: {net} {points}.", "Ran out of time", "The turn clock ran out mid-battle. Whatever had sunk is settled.", new[] { "user", "admiral", "shots", "net", "points" }, alias: "MsgTurnTimeout"),
        Msg("Battle result", "msgTurnTimeoutFree", "⏰ {user} abandoned the battle.", "Ran out of time (Free game)", "Used instead while Free game is on.", new[] { "user", "admiral", "shots" }, alias: "MsgTurnTimeoutFree"),
        Msg("Battle result", "msgDeployTimeout", "⚓ {user} never gave the order to fire — the fleet returns to port. Nothing lost.", "Never opened fire", "Nothing had sunk, so nothing is charged and it doesn't count as a battle.", new[] { "user" }, alias: "MsgDeployTimeout"),
        Msg("Timer warnings", "msgTurnWarnFirst", "⏳ @{user}, {seconds} seconds to fire before you forfeit the battle!", "First warning", "Posted when the turn clock reaches the first warning. The countdown ring turns amber.", new[] { "user", "seconds" }, alias: "MsgTurnWarnFirst"),
        Msg("Timer warnings", "msgTurnWarnSecond", "⚠️ @{user}, {seconds} SECONDS — fire now!", "Final warning", "Posted when the turn clock reaches the final warning. The ring pulses red.", new[] { "user", "seconds" }, alias: "MsgTurnWarnSecond"),
        Msg("Chat vs the Admiral", "msgChatStarted", "⚓ {team} takes on {admiral}! Click enemy waters on stream or vote with !fire B7 — every {round}s the square with the most votes is fired at. Just for fun: no points to win or lose.", "Battle started", "Posted when !seeschlacht starts the chat's battle. Misses and hits use the solo messages, with the team name as {user}.", new[] { "team", "admiral", "round", "ships", "size" }, alias: "MsgChatStarted"),
        Msg("Chat vs the Admiral", "msgChatSinks", "🔥 {team} SINKS {admiral}'s {ship} at {coord}! {enemyleft} enemy ships left.", "Chat sinks a ship", "Played for fun: nothing is paid.", new[] { "team", "admiral", "ship", "coord", "enemyleft" }, alias: "MsgChatSinks"),
        Msg("Chat vs the Admiral", "msgChatLosesShip", "☠️ {admiral} sinks {team}'s {ship} at {coord}! {yourleft} ships left.", "Chat loses a ship", "Played for fun: nothing is charged.", new[] { "team", "admiral", "ship", "coord", "yourleft" }, alias: "MsgChatLosesShip"),
        Msg("Chat vs the Admiral", "msgChatWins", "🏆 {team} has sunk {admiral}'s entire fleet in {shots} shots — {voters} of you voted. Well played!", "Chat wins", "{voters} is how many viewers voted over the battle.", new[] { "team", "admiral", "shots", "voters" }, alias: "MsgChatWins"),
        Msg("Chat vs the Admiral", "msgChatLoses", "💀 {admiral} has sunk {team}'s fleet after {shots} shots. Better luck next time!", "Chat loses", "The Admiral sank the chat's fleet.", new[] { "team", "admiral", "shots", "voters" }, alias: "MsgChatLoses"),
        Msg("Chat vs the Admiral", "msgChatAbandoned", "⏰ Nobody voted for {seconds}s — {team} breaks off the battle against {admiral}.", "Chat stops voting", "Nobody voted for as long as the battle clock allows.", new[] { "team", "admiral", "seconds" }, alias: "MsgChatAbandoned"),
        Msg("Team duel", "msgDuelChallenge", "⚔️ {challenger} challenges {target} to a team duel! {target}, accept with !accept within {seconds}s. Entry: {fee} {points}.", "Challenge", "!duel with a name. Write your own command names into the duel messages.", new[] { "challenger", "target", "seconds", "fee", "points" }, alias: "MsgDuelChallenge"),
        Msg("Team duel", "msgDuelOpenChallenge", "⚔️ {challenger} calls for a team duel! Anyone can accept with !accept within {seconds}s. Entry: {fee} {points}.", "Open challenge", "!duel with no name: anybody may accept.", new[] { "challenger", "seconds", "fee", "points" }, alias: "MsgDuelOpenChallenge"),
        Msg("Team duel", "msgDuelAccepted", "⚔️ {target} accepts! Team A ({challenger}) against Team B ({target}). Join a side with !team A or !team B — {seconds}s to sign up. Entry: {fee} {points}.", "Challenge accepted", "Sign-up opens. {target} is whoever accepted.", new[] { "challenger", "target", "seconds", "fee", "points" }, alias: "MsgDuelAccepted"),
        Msg("Team duel", "msgDuelJoined", "➕ {user} joins {team} ({count} aboard).", "Player joins a side", "Leave it empty to keep a busy sign-up quiet.", new[] { "user", "team", "count" }, alias: "MsgDuelJoined"),
        Msg("Team duel", "msgDuelStart", "🚢 The duel begins: Team A ({a} players) against Team B ({b} players), pot {pot} {points}. {team} fires first — vote with !fire B7, one round of {round}s per shot.", "Duel begins", "{team} is the side that fires first, picked at random.", new[] { "team", "a", "b", "pot", "points", "round", "ships", "size" }, alias: "MsgDuelStart"),
        Msg("Team duel", "msgDuelTurn", "🎯 {team}'s turn — vote with !fire B7.", "Next side's turn", "Posted at the start of every round after the first. Misses and hits use the solo messages, with the sides as {user} and {admiral}.", new[] { "team", "round" }, alias: "MsgDuelTurn"),
        Msg("Team duel", "msgDuelTurnPassed", "⌛ {team} lets its turn pass ({missed} of {max}).", "Side lets its turn pass", "Nobody on that side voted.", new[] { "team", "missed", "max" }, alias: "MsgDuelTurnPassed"),
        Msg("Team duel", "msgDuelSunk", "🔥 {team} SINKS {enemy}'s {ship} at {coord}! {enemyleft} of their ships left.", "Side sinks a ship", "Posted when a side sinks one of the other side's ships.", new[] { "team", "enemy", "ship", "coord", "enemyleft" }, alias: "MsgDuelSunk"),
        Msg("Team duel", "msgDuelStatus", "🎯 {teama}: {aleft} ships afloat — {teamb}: {bleft} ships afloat. {turn}'s turn, {shots} shots so far, pot {pot} {points}.", "Duel status", "Posted by !board during a duel.", new[] { "teama", "teamb", "aleft", "bleft", "turn", "shots", "pot", "points" }, alias: "MsgDuelStatus"),
        Msg("Team duel", "msgDuelWin", "🏆 {team} wins the duel in {shots} shots! {pot} {points} shared between {players} players — {share} each.", "Side wins", "{share} is each winner's share, their own entry included, before the Addicted cut and the daily cap.", new[] { "team", "shots", "pot", "points", "players", "share" }, alias: "MsgDuelWin"),
        Msg("Team duel", "msgDuelWinFree", "🏆 {team} wins the duel in {shots} shots!", "Side wins (nothing staked)", "Free game, or an entry fee of 0.", new[] { "team", "shots" }, alias: "MsgDuelWinFree"),
        Msg("Team duel", "msgDuelForfeit", "🏳️ {team} let {turns} turns pass in a row and gives up the duel.", "Side gives up", "It let too many turns pass in a row. The win message follows.", new[] { "team", "winner", "turns" }, alias: "MsgDuelForfeit"),
        Msg("Team duel", "msgDuelExpired", "⌛ Nobody took up {challenger}'s challenge — the entry is refunded.", "Challenge not taken up", "The challenger's entry is refunded.", new[] { "challenger", "target" }, alias: "MsgDuelExpired"),
        Msg("Team duel", "msgDuelCancelled", "⌛ Neither side fired a shot — the duel is called off and every entry is refunded.", "Duel called off", "Neither side ever voted. Every entry is refunded.", new string[0], alias: "MsgDuelCancelled"),
        Msg("Moderator stop", "msgModStop", "🛑 {user} has stopped {game}'s battle — nothing more is paid out.", "Battle stopped", "{user} is the moderator, {game} the player or the chat's team name. Nothing more is paid out.", new[] { "user", "game", "admiral" }, alias: "MsgModStop"),
        Msg("Moderator stop", "msgModStopDuel", "🛑 {user} has stopped the duel — nothing is paid out, every entry is refunded.", "Duel stopped", "Every entry is refunded.", new[] { "user" }, alias: "MsgModStopDuel"),
        Msg("Moderator stop", "msgModStopNothing", "@{user}, there is no battle or duel to stop.", "Nothing to stop", "The stop command with no battle or duel running.", new[] { "user" }, alias: "MsgModStopNothing"),
        Msg("Click to Fire", "msgHeatAutoHint", "🖱️ {user}, you can also fire by clicking enemy waters on stream — your first click there takes control. Did someone else's click get in first? Click once more, then type !fire link.", "How to fire by clicking", "Sent to a Twitch player without a link when their battle comes on screen, while the first click takes control. Leave it empty to send nothing.", new[] { "user", "admiral" }, alias: "MsgHeatAutoHint"),
        Msg("Click to Fire", "msgHeatLinkHint", "🖱️ {user}, you can also fire by clicking enemy waters on stream: click any square, then type !fire link. Only once — after that your clicks fire on their own.", "How to link", "Sent at the same moment when the first click doesn't take control, so every player links with !fire link.", new[] { "user", "admiral" }, alias: "MsgHeatLinkHint"),
        Msg("Click to Fire", "msgHeatLinked", "🖱️ {user}, your clicks are linked — from now on just click enemy waters on stream to fire.", "Linked", "Confirms !fire link. The click that made the link fires straight after.", new[] { "user", "admiral" }, alias: "MsgHeatLinked"),
        Msg("Click to Fire", "msgHeatLinkNoClick", "@{user}, no click on enemy waters reached me in the last {seconds} seconds. Click a square on stream first, then type !fire link.", "Link: no click seen", "!fire link found no click on enemy waters in the last seconds.", new[] { "user", "seconds" }, alias: "MsgHeatLinkNoClick"),
        Msg("Click to Fire", "msgHeatLinkAmbiguous", "@{user}, more than one person clicked enemy waters just now, so I can't tell which clicks are yours. Click again in a moment, then type !fire link.", "Link: more than one clicker", "Several viewers clicked in that time, so the link is refused rather than guessed.", new[] { "user", "seconds" }, alias: "MsgHeatLinkAmbiguous"),
        Msg("Errors", "msgBadCoord", "@{user}, '{input}' is not a square on this board. Use a letter and a number, like B7 (A1 to {max}).", "Not a square on the board", "!fire with something that isn't a square, like 'Z9'.", new[] { "user", "input", "max" }, alias: "MsgBadCoord"),
        Msg("Errors", "msgNoCoord", "@{user}, name a square to fire at, like !fire B7", "No square given", "!fire on its own.", new[] { "user" }, alias: "MsgNoCoord"),
        Msg("Errors", "msgAlreadyFired", "@{user}, you have already fired at {coord}. Pick another square — this one is free.", "Already fired there", "A repeat shot never costs the turn or the clock.", new[] { "user", "coord" }, alias: "MsgAlreadyFired"),
        Msg("Errors", "msgNoGame", "@{user}, you have no battle running. Start one with !battleship", "No battle running", "!fire, !fleet or !board from a viewer with no battle.", new[] { "user" }, alias: "MsgNoGame"),
        Msg("Errors", "msgAlreadyInGame", "@{user}, you are already at sea. Fire with !fire B7.", "Already in a battle", "!battleship from a viewer whose battle is still running.", new[] { "user" }, alias: "MsgAlreadyInGame"),
        Msg("Errors", "msgFleetTooLate", "@{user}, the battle has already started — you can only redeploy before your first shot.", "Too late to redeploy", "!fleet after the first shot.", new[] { "user" }, alias: "MsgFleetTooLate"),
        Msg("Errors", "msgNotEnoughPoints", "@{user}, you need at least {required} {points} to put a fleet to sea. You have {balance}.", "Not enough points", "Below the minimum balance on the Game page, or short of a duel's entry fee.", new[] { "user", "required", "balance", "points" }, alias: "MsgNotEnoughPoints"),
        Msg("Errors", "msgModeDisabled", "@{user}, that game mode is switched off.", "Game mode switched off", "A mode switched off on the Modes page.", new[] { "user" }, alias: "MsgModeDisabled"),
        Msg("Errors", "msgChatBusy", "@{user}, a battle is already running — {team} can take on {admiral} once it is over.", "Chat battle has to wait", "The chat's battle starts only when no other battle is running.", new[] { "user", "team", "admiral" }, alias: "MsgChatBusy"),
        Msg("Errors", "msgMaxGamesPerHour", "@{user}, you have hit the limit of {max} battles per hour.", "Too many battles this hour", "The limit on the Access page.", new[] { "user", "max" }, alias: "MsgMaxGamesPerHour"),
        Msg("Errors", "msgDailyCapReached", "@{user}, you have reached today's winnings cap of {cap} {points}. Further wins pay nothing until tomorrow.", "Daily winnings cap reached", "The cap on the Game page.", new[] { "user", "cap", "points" }, alias: "MsgDailyCapReached"),
        Msg("Duel errors", "msgDuelBusy", "@{user}, a battle or a duel is already on — try again once it is over.", "Duel has to wait", "A duel is challenged only while nothing else runs, and nothing else starts while one does.", new[] { "user" }, alias: "MsgDuelBusy"),
        Msg("Duel errors", "msgDuelNoChallenge", "@{user}, there is no challenge waiting for you.", "No challenge to accept", "Nothing is waiting, it is somebody else's challenge, it is the caller's own, or its time has run out.", new[] { "user" }, alias: "MsgDuelNoChallenge"),
        Msg("Duel errors", "msgDuelNotJoining", "@{user}, no duel is taking players right now.", "Sign-up isn't open", "The join command while no duel takes players.", new[] { "user" }, alias: "MsgDuelNotJoining"),
        Msg("Duel errors", "msgDuelAlreadyJoined", "@{user}, you are already in {team}.", "Already on a side", "The join command from somebody who already joined.", new[] { "user", "team" }, alias: "MsgDuelAlreadyJoined"),
        Msg("Duel errors", "msgDuelTeamFull", "@{user}, {team} already has {gap} more players than {other} — join {other} instead.", "Side too far ahead", "Only with a lead limit on the Modes page.", new[] { "user", "team", "other", "gap" }, alias: "MsgDuelTeamFull"),
        Msg("Duel errors", "msgDuelBadTeam", "@{user}, pick a side: !team A or !team B — or just !team to fill the smaller one.", "No such side", "The join command named neither A nor B nor anybody on a side.", new[] { "user" }, alias: "MsgDuelBadTeam"),
        Msg("Duel errors", "msgDuelNotInDuel", "@{user}, only the two teams vote in this duel.", "Not in the duel", "A !fire vote from somebody on neither side.", new[] { "user" }, alias: "MsgDuelNotInDuel"),
        Msg("Duel errors", "msgDuelNotYourTurn", "@{user}, it is {team}'s turn.", "Not this side's turn", "{team} is the side whose turn it is.", new[] { "user", "team" }, alias: "MsgDuelNotYourTurn"),
        Msg("Whisper", "msgWhisperFleet", "Your fleet is deployed: {fleet}. The Admiral cannot read this — or your chat. Good hunting!", "Your fleet", "Whispered to a solo player when their battle starts, while the whisper is on.", new[] { "user", "fleet" }, alias: "MsgWhisperFleet"),
        Msg("Cooldown", "globalCooldownMessage", "Battleship is on global cooldown. Try again in {seconds} seconds.", "Everyone", "Sent while the cooldown for everyone runs.", new[] { "seconds", "user" }, alias: "GlobalCooldownMsg"),
        Msg("Cooldown", "userCooldownMessage", "@{user}, you're on cooldown. Try again in {seconds} seconds.", "Per viewer", "Sent while this viewer's own cooldown runs.", new[] { "user", "seconds" }, alias: "UserCooldownMsg"),
        Msg("Stats", "msgNoStatsFound", "@{user}: no battles fought yet. Start one with !battleship", "No record yet", "!bstats for a player with no battles.", new[] { "user" }, alias: "MsgNoStatsFound"),
        Msg("Stats", "msgNoLeaderboard", "No battle records yet.", "Leaderboard empty", "!ballstats before anybody has a record.", new string[0], alias: "MsgNoLeaderboard"),
    };
}

/// <summary>The settings file as the game reads it: every key through its default, old key names tolerated. Values
/// an alias stored in another scale (a fraction where percent is stored now) are converted on the way.</summary>
public sealed class UiSettings
{
    private readonly JObject _root;

    private UiSettings(JObject root) { _root = root; }

    /// <summary>Settings from JSON text, for tests and callers that already hold the file.</summary>
    public static UiSettings Parse(string json)
    {
        using (var reader = new JsonTextReader(new StringReader(json ?? "{}")) { DateParseHandling = DateParseHandling.None })
            return new UiSettings(JObject.Load(reader));
    }

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
