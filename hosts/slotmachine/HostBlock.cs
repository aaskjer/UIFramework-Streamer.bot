// --- UIFramework host block (keep identical in every action of this host that reads the settings) ---
// From UIFramework/stub/HostSchema.cs. The game declares its settings once in UiSchema.Table, reads them through
// UiSettings, and answers the window's describe request with UiSchema.ToJson(). One table, so the game and its
// window can never disagree. Compiles in any Streamer.bot action with no reference beyond Newtonsoft.Json.
//
// In Execute(), first thing:
//     if (args.ContainsKey("uiframework.describe"))
//     {
//         CPH.SetGlobalVar("uiframework.schema." + ActionName, UiSchema.ToJson("slotmachine", CurrentVersion, "Chat Games/Slot Machine Game/SMG-Settings.json"), false);
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
        string platform = null, bool twitchOnly = false)
    {
        return new Setting
        {
            Key = key, Default = @default, Page = page, Label = label, Help = help, Kind = kind, Section = section, Min = min, Max = max,
            Unit = unit, Factor = factor, UnlimitedAtZero = unlimitedAtZero, Options = options, Requires = requires, Confirm = confirm,
            Hidden = hidden, Aliases = aliases, AliasFactor = aliasFactor, Columns = columns, File = file, Field = field,
            Transient = transient, Live = live, Panel = panel, Auto = auto, Platform = platform, TwitchOnly = twitchOnly
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
            if (s.TwitchOnly) item["twitchOnly"] = true;
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
        S("numberOfSlots", 4, page: "game", label: "Slots", help: "How many slots a spin shows. All of them alike is the jackpot.", section: "Slots and odds", options: new[] { "3", "4", "5", "6" }, aliases: new[] { "NumberOfSlots" }),
        S("matchChance", 18, page: "game", label: "Match chance", help: "Chance in percent that a spin may land matching slots: they're then drawn freely, so a match is likely but not certain. The rest of the time a repeat is rerolled to avoid a match. The odds below show what really comes out.", kind: "percent", section: "Slots and odds", min: 0.0, max: 100.0, aliases: new[] { "AllowMatchChance" }, aliasFactor: 100.0),
        S("jackpotChance", 1, page: "game", label: "Jackpot chance", help: "Chance in percent that every slot shows the same emote - the jackpot. '1' is 1 in 100. A free draw can land all alike too, so the real chance is a little higher.", kind: "percent", section: "Slots and odds", min: 0.0, max: 100.0, aliases: new[] { "AllowJackpotChance" }, aliasFactor: 100.0),
        S("rerollAttempts", 2, page: "game", label: "Rerolls", help: "How hard a spin that isn't meant to match avoids a repeat: each slot is drawn up to this many times. A repeat can still slip through, most of all with few emotes in play.", section: "Slots and odds", min: 0.0, aliases: new[] { "RerollAttempts" }),
        S("emotePoolSize", 10, page: "game", label: "Emote pool (0 = all)", help: "How many of the emotes in play are drawn for each spin. Fewer emotes make matches more likely. 0, or more than there are, uses them all.", section: "Slots and odds", min: 0.0, aliases: new[] { "EmotePoolSize" }),
        S("minEmoteWeight", 1, page: "game", label: "Min weight", help: "Only emotes with at least this weight play. Low weights come up often and pay little, so raising this takes out the cheapest emotes.", section: "Slots and odds", min: 1.0, aliases: new[] { "MinEmoteWeight" }),
        S("maxEmoteWeight", 10, page: "game", label: "Max weight", help: "Only emotes with at most this weight play. High weights are rare and pay a lot, so lowering this takes out the most valuable emotes.", section: "Slots and odds", min: 1.0, aliases: new[] { "MaxEmoteWeight" }),
        S("tryPlatform", "twitch", page: "game", label: "Emotes of", help: "Whose emotes the odds and the test spin use. Subscriber and VIP boosts only exist on Twitch. Not saved.", kind: "options", section: "Slots and odds", options: new[] { "twitch|Twitch", "youtube|YouTube", "kick|Kick" }, transient: true),
        S("odds", null, page: "game", label: "What really comes out", help: "Worked out from 30,000 test spins with the emotes, weights, chances and payouts as they stand on this page, for a viewer without Subscriber/VIP boost or Addicted cut, and without the jackpot pool.", kind: "action", section: "Slots and odds", live: true, panel: true, auto: true),
        S("defaultBet", 1234, page: "game", label: "Default bet", help: "The bet for a spin with no amount. Viewers can also bet a number, all, half, third, quarter or random.", section: "Bets and points", min: 0.0, aliases: new[] { "DefaultBet" }),
        S("minBet", 1, page: "game", label: "Minimum (0 = none)", help: "The smallest bet allowed. 0 turns the limit off. 'random' bets between Minimum and Maximum. Also applies to channel points in 'Typed amount' and 'Fixed' mode.", section: "Bets and points", min: 0.0, aliases: new[] { "MinBet" }),
        S("maxBet", 9999999, page: "game", label: "Maximum (0 = none)", help: "The largest bet allowed. 0 turns the limit off. Also applies to channel points in 'Typed amount' and 'Fixed' mode.", section: "Bets and points", min: 0.0, aliases: new[] { "MaxBet" }),
        S("freeGame", false, page: "game", label: "Free mode", help: "Every spin is free: nobody pays or wins points, the jackpot pool doesn't grow, and chat gets the Free messages. For a single free spin, a trigger of your own can pass the argument free=1 instead.", section: "Bets and points", aliases: new[] { "IsFree" }),
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
        S("tryBet", 1000, page: "game", label: "Bet to try", help: "A bet to try the odds and messages with. It isn't saved.", section: "Try a spin", min: 1.0, transient: true),
        S("tryRole", "viewer", page: "game", label: "Spin as", help: "Who spins: boosts and the Addicted cut change what a win pays. The platform is the one picked under 'Slots and odds'.", kind: "options", section: "Try a spin", options: new[] { "viewer|Viewer", "subscriber|Subscriber", "vip|VIP", "addicted|Addicted" }, transient: true),
        S("trySpin", null, page: "game", label: "Spin", help: "Plays one spin with the settings as they stand on this page, saved or not, and shows the slots and the message chat would get. Nothing is sent and nothing is saved.", kind: "action", section: "Try a spin", live: true, panel: true),
        // ---- emotes ----
        S("emotesTwitch", JArray.Parse("[{\"Code\":\"Kappa\",\"Weight\":1,\"Name\":\"Kappa\"},{\"Code\":\"PogChamp\",\"Weight\":2,\"Name\":\"PogChamp\"},{\"Code\":\"LUL\",\"Weight\":3,\"Name\":\"LUL\"},{\"Code\":\"NotLikeThis\",\"Weight\":4,\"Name\":\"NotLikeThis\"},{\"Code\":\"BigPhish\",\"Weight\":5,\"Name\":\"BigPhish\"},{\"Code\":\"RitzMitz\",\"Weight\":6,\"Name\":\"RitzMitz\"},{\"Code\":\"FrankerZ\",\"Weight\":7,\"Name\":\"FrankerZ\"},{\"Code\":\"SeemsGood\",\"Weight\":8,\"Name\":\"SeemsGood\"},{\"Code\":\"Kreygasm\",\"Weight\":9,\"Name\":\"Kreygasm\"},{\"Code\":\"FailFish\",\"Weight\":10,\"Name\":\"FailFish\"}]"), page: "emotes", label: "Twitch", help: "The emote exactly as it's typed in chat, like Kappa, with its weight: a low weight comes up often and pays little, a high one is rare and pays a lot. Its picture appears beside it once it is known to Twitch, 7TV, BTTV or FFZ. Chat only shows it as an emote if the account sending the messages can use it, and a 7TV, BTTV or FFZ one only to viewers with that extension. Which weights play is set on the Game page.", kind: "list", section: "Twitch", columns: new[] { "Code|emote|Emote|twitch", "Weight|int|Weight", "Name|string|Name" }, aliases: new[] { "PlatformEmotes.twitch" }),
        S("emotesYoutube", JArray.Parse("[{\"Code\":\"🍇\",\"Weight\":1,\"Name\":\"Grapes\"},{\"Code\":\"🍈\",\"Weight\":2,\"Name\":\"Melon\"},{\"Code\":\"🍉\",\"Weight\":3,\"Name\":\"Watermelon\"},{\"Code\":\"🍊\",\"Weight\":4,\"Name\":\"Tangerine\"},{\"Code\":\"🍋\",\"Weight\":5,\"Name\":\"Lemon\"},{\"Code\":\"🍌\",\"Weight\":6,\"Name\":\"Banana\"},{\"Code\":\"🍍\",\"Weight\":7,\"Name\":\"Pineapple\"},{\"Code\":\"🍎\",\"Weight\":8,\"Name\":\"Red Apple\"},{\"Code\":\"🍏\",\"Weight\":9,\"Name\":\"Green Apple\"},{\"Code\":\"🍑\",\"Weight\":10,\"Name\":\"Peach\"}]"), page: "emotes", label: "YouTube", help: "The emoji or text chat gets for this slot, like 🍒, with its weight.", kind: "list", section: "YouTube", columns: new[] { "Code|emote|Emoji or text|youtube", "Weight|int|Weight", "Name|string|Name" }, aliases: new[] { "PlatformEmotes.youtube" }),
        S("emotesKick", JArray.Parse("[{\"Code\":\"[emote:1730803:emojiRich]\",\"Weight\":1,\"Name\":\"emojiRich\"},{\"Code\":\"[emote:1579059:emojiSwearing]\",\"Weight\":2,\"Name\":\"emojiSwearing\"},{\"Code\":\"[emote:3419630:emojiTire]\",\"Weight\":3,\"Name\":\"emojiTire\"},{\"Code\":\"[emote:1730772:emojiFire]\",\"Weight\":4,\"Name\":\"emojiFire\"},{\"Code\":\"[emote:1730758:emojiClown]\",\"Weight\":5,\"Name\":\"emojiClown\"},{\"Code\":\"[emote:1730752:emojiAngel]\",\"Weight\":6,\"Name\":\"emojiAngel\"},{\"Code\":\"[emote:1730790:emojiKiss]\",\"Weight\":7,\"Name\":\"emojiKiss\"},{\"Code\":\"[emote:1730753:emojiAngry]\",\"Weight\":8,\"Name\":\"emojiAngry\"},{\"Code\":\"[emote:1730807:emojiShocked]\",\"Weight\":9,\"Name\":\"emojiShocked\"},{\"Code\":\"[emote:1730792:emojiLoading]\",\"Weight\":10,\"Name\":\"emojiLoading\"}]"), page: "emotes", label: "Kick", help: "Kick shows [emote:ID:name] as the emote. Open the emote's picture in a new browser tab: the number in its address is the ID (https://files.kick.com/emotes/1730803/fullsize is emojiRich).", kind: "list", section: "Kick", columns: new[] { "Code|emote|Emote, as [emote:ID:name]|kick", "Weight|int|Weight", "Name|string|Name" }, aliases: new[] { "PlatformEmotes.kick" }),
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
