    // ================================================================================================
    // HOST: Classic Chat Gamble
    // ================================================================================================

    private const string GameActionName = "Classic Chat Gamble";          // answers uiframework.describe
    private const string LogPrefix = "[Classic Chat Gamble/Settings]";
    private const string GambleCmdId = "df4181fc-651f-4fc3-98ea-4dec9b242836";   // !gamble, where the cooldowns live
    private static readonly string SettingsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Chat Games", "Classic Chat Gamble", "configs.json");
    private static readonly string LogsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Chat Games", "Logs");

    private const long SampleBalance = 12340;

    // The stops of the fairness slider and their numbers: jackpot chance, jackpot multiplier, win chance, min and max multiplier.
    private static readonly string[] Presets = { "strict", "houseEdge", "fair", "generous", "classic" };
    private static readonly double[][] PresetValues =
    {
        new[] { 0.1, 43, 22, 1.5, 2.5 },
        new[] { 0.25, 43, 23, 1.5, 2.5 },
        new[] { 0.3, 43, 25, 1.5, 2.5 },
        new[] { 0.6, 43, 28, 1.5, 2.5 },
        new[] { 1.0, 43, 30, 1.5, 2.5 }
    };
    private static readonly string[] PresetNames = { "Strict", "House Edge", "Fair", "Generous", "Classic" };

    /// <summary>What the DLL calls. Public methods, string arguments, no framework types: found by reflection.
    /// Everything that needs CPH is queued as a job and run by Pump on Streamer.bot's thread; the odds, the test
    /// roll and the fairness slider need nothing but the values, so they answer at once.</summary>
    public sealed class Host
    {
        private readonly CPHInline _owner;
        private readonly Random _dice = new Random();
        public Host(CPHInline owner) { _owner = owner; }

        public void Log(string level, string message)
        {
            if (level == "error") _owner.CPH.LogError(message);
            else if (level == "warn") _owner.CPH.LogWarn(message);
            else _owner.CPH.LogInfo(message);
        }

        // The two cooldowns live on the !gamble command, where Streamer.bot reads them.
        public void OnSaved(string settingsJson)
        {
            var settings = JObject.Parse(settingsJson);
            _owner.Enqueue(() =>
            {
                _owner.CPH.CommandSetGlobalCooldownDuration(GambleCmdId, Math.Max(0, (int?)settings["globalCooldown"] ?? 0));
                _owner.CPH.CommandSetUserCooldownDuration(GambleCmdId, Math.Max(0, (int?)settings["userCooldown"] ?? 0));
            });
        }

        public string Test(string name, string settingsJson)
        {
            var settings = JObject.Parse(settingsJson);
            switch (name)
            {
                // The slider moved: its stop fills in the five numbers; on Custom it leaves them alone.
                case "fairness": return Preset(Array.IndexOf(Presets, ((string)settings["fairness"] ?? "").Trim()));
                case "odds": return Odds(settings);
                case "tryRoll": return TryRoll(settings, _dice);
                default: return null;
            }
        }

        public string ReadTable(string key) => null;

        public string EmoteSources() => null;

        // What would not do what it looks like, one problem per line; the window asks before saving anyway.
        public string Validate(string settingsJson)
        {
            var s = JObject.Parse(settingsJson);
            var problems = new List<string>();
            long min = (long?)s["minBet"] ?? 0, max = (long?)s["maxBet"] ?? 0, fallback = (long?)s["defaultBet"] ?? 0;
            if (min > 0 && max > 0 && min > max)
                problems.Add("The minimum bet (" + min + ") is above the maximum (" + max + "), so every bet is refused.");
            if (fallback > 0 && ((min > 0 && fallback < min) || (max > 0 && fallback > max)))
                problems.Add("The default bet (" + fallback + ") is outside the minimum and maximum, so a plain !gamble is refused.");
            double jackpot = (double?)s["jackpotPercent"] ?? 0, win = (double?)s["winPercent"] ?? 0;
            if (jackpot + win > 100)
                problems.Add("The jackpot chance and the win chance add up to more than 100 %, so a gamble can never lose.");
            if (((double?)s["rndMin"] ?? 0) > ((double?)s["rndMax"] ?? 0))
                problems.Add("Min × is above Max ×; swap them.");
            if (!((bool?)s["freeGame"] ?? false) && string.IsNullOrWhiteSpace((string)s["pointsVariable"]))
                problems.Add("Free Game Mode is off but the points variable is empty, so nothing can be staked.");
            if (((bool?)s["isChannelPointsTrigger"] ?? false) && string.IsNullOrWhiteSpace((string)s["rewardId"]))
                problems.Add("Channel points start a gamble, but the reward ID is empty, so no redemption starts one.");
            if (((bool?)s["isChannelPointsTrigger"] ?? false) && ((string)s["cpBetMode"] ?? "cost") == "fixed")
            {
                long fixedBet = (long?)s["fixedBetAmount"] ?? 0;
                if (fixedBet <= 0)
                    problems.Add("Channel points bet a fixed amount, but it is 0, so no redemption can play.");
                else if ((min > 0 && fixedBet < min) || (max > 0 && fixedBet > max))
                    problems.Add("The fixed bet (" + fixedBet + ") is outside the minimum and maximum, so every redemption is refused.");
            }
            return problems.Count == 0 ? null : string.Join("\n", problems);
        }

        public IEnumerable<string> ReadLogLines()
        {
            if (!Directory.Exists(LogsDir))
                return new string[0];
            return Directory.GetFiles(LogsDir, "CCG-log*.txt").OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                .SelectMany(ReadLinesSafe).Where(l => !string.IsNullOrWhiteSpace(l)).ToList();
        }

        private static IEnumerable<string> ReadLinesSafe(string file)
        {
            try { return File.ReadAllLines(file); } catch (Exception) { return new string[0]; }
        }
    }

    // ---- The slider's stops, the odds and the test roll, from the values as they stand in the window ----

    private static string Preset(int index)
    {
        if (index < 0)
            return null;
        var v = PresetValues[index];
        var set = new JObject
        {
            ["jackpotPercent"] = v[0], ["jackpotMultiplier"] = (int)v[1], ["winPercent"] = v[2], ["rndMin"] = v[3], ["rndMax"] = v[4]
        };
        return new JObject { ["set"] = set, ["status"] = PresetNames[index] + " filled in; save to keep it." }.ToString(Newtonsoft.Json.Formatting.None);
    }

    private static double Clamp(double v, double min, double max) => double.IsNaN(v) || double.IsInfinity(v) ? min : v < min ? min : v > max ? max : v;

    private static string Amount(JObject s, double value)
    {
        string sep = (string)s["numberSeparator"] ?? ".";
        string text = Math.Round(value).ToString("N0", CultureInfo.InvariantCulture);
        return string.IsNullOrEmpty(sep) ? text.Replace(",", "") : text.Replace(",", sep);
    }

    private static string Percent(double value) => value.ToString("0.##", CultureInfo.InvariantCulture) + " %";

    private static string Odds(JObject s)
    {
        // The same rounding as the game: chances in hundredths of a percent, so 0.57 % is 57 of 10,000.
        double jackpot = Math.Round(Clamp((double?)s["jackpotPercent"] ?? 0, 0, 100) * 100) / 100.0;
        double win = Math.Min(100.0 - jackpot, Math.Round(Clamp((double?)s["winPercent"] ?? 0, 0, 100) * 100) / 100.0);
        double loss = Math.Max(0.0, 100.0 - jackpot - win);
        double lo = Clamp((double?)s["rndMin"] ?? 1.5, 0, double.MaxValue), hi = Clamp((double?)s["rndMax"] ?? 2.5, 0, double.MaxValue);
        double jm = (int?)s["jackpotMultiplier"] ?? 43; if (jm <= 0) jm = 1;
        double multiplier = Math.Max(0.0, (lo + hi) / 2.0);
        double back = 100.0 * (jackpot / 100.0 * (1 + multiplier * jm) + win / 100.0 * (1 + multiplier));
        int step = -1;
        for (int i = 0; i < PresetValues.Length; i++)
        {
            var v = PresetValues[i];
            if (Math.Abs(((double?)s["jackpotPercent"] ?? 0) - v[0]) < 0.0001 && ((int?)s["jackpotMultiplier"] ?? 0) == (int)v[1]
                && Math.Abs(((double?)s["winPercent"] ?? 0) - v[2]) < 0.0001 && Math.Abs(((double?)s["rndMin"] ?? 0) - v[3]) < 0.0001 && Math.Abs(((double?)s["rndMax"] ?? 0) - v[4]) < 0.0001)
            { step = i; break; }
        }
        string[] descriptions =
        {
            "The house wins: over time viewers lose about a quarter of what they bet, so points drain away.",
            "The house keeps a small cut, so points shrink slowly.",
            "About even: over time viewers get back roughly what they bet.",
            "Viewers come out ahead, so points grow.",
            "The default: rare, huge jackpots. Viewers come out far ahead and points grow fast."
        };
        // The slider follows the numbers: the stop they match, or Custom.
        return new JObject
        {
            ["set"] = new JObject { ["fairness"] = step >= 0 ? Presets[step] : "custom" },
            ["lines"] = new JArray
            {
                new JObject { ["text"] = (step >= 0 ? PresetNames[step] + " · " : "Your own numbers · ") + "A bet of 100 comes back as about " + Amount(s, back), ["big"] = true },
                step >= 0 ? descriptions[step] : "A win pays ×" + lo.ToString("0.##", CultureInfo.InvariantCulture) + " to ×" + hi.ToString("0.##", CultureInfo.InvariantCulture) + ", a jackpot " + jm.ToString("0", CultureInfo.InvariantCulture) + " times that. The average assumes a viewer without Subscriber/VIP boost or Addicted cut."
            },
            ["bar"] = new JArray
            {
                new JObject { ["label"] = "Jackpot", ["value"] = jackpot, ["text"] = Percent(jackpot), ["colour"] = "#F59E0B" },
                new JObject { ["label"] = "Win", ["value"] = win, ["text"] = Percent(win), ["colour"] = "#22C55E" },
                new JObject { ["label"] = "Loss", ["value"] = loss, ["text"] = Percent(loss), ["colour"] = "#6B7280" }
            }
        }.ToString(Newtonsoft.Json.Formatting.None);
    }

    private static string Fill(string template, Dictionary<string, string> values)
    {
        foreach (var pair in values)
            template = template.Replace("{" + pair.Key + "}", pair.Value ?? "");
        return template;
    }

    // The game's gamble, with alice's sample balance: the roll of 1 to 10,000, the multiplier roll, the jackpot
    // multiplier, then the boosts and the Addicted cut.
    private static string TryRoll(JObject s, Random dice)
    {
        string role = ((string)s["tryRole"] ?? "viewer").Trim().ToLowerInvariant();
        long bet = Math.Max(0, (long?)s["tryBet"] ?? 1000);
        string points = (string)s["pointsName"] ?? "Points";
        if (bet <= 0)
            return "Enter a whole bet above 0 first.";
        long balance = Math.Max(SampleBalance, 10 * ((long?)s["defaultBet"] ?? 0));
        var values = new Dictionary<string, string> { ["user"] = "alice", ["pointsName"] = points, ["min"] = Amount(s, (long?)s["minBet"] ?? 0), ["max"] = Amount(s, (long?)s["maxBet"] ?? 0) };
        long min = (long?)s["minBet"] ?? 0, max = (long?)s["maxBet"] ?? 0;
        string reason = null;
        if (bet > balance) reason = (string)s["reasonInsufficientFunds"];
        else if (min > 0 && bet < min) reason = (string)s["reasonBelowMinimum"];
        else if (max > 0 && bet > max) reason = (string)s["reasonAboveMaximum"];
        if (reason != null)
        {
            values["reason"] = Fill(reason ?? "", values);
            return "alice has " + Amount(s, balance) + " " + points + ".\nRefused · " + Fill((string)s["invalidBetMessage"] ?? "", values);
        }

        double jp = Clamp((double?)s["jackpotPercent"] ?? 0, 0, 100), wp = Clamp((double?)s["winPercent"] ?? 0, 0, 100);
        int jackpotThreshold = (int)Math.Round(jp * 100);
        int winUpperBound = jackpotThreshold + (int)Math.Round(wp * 100);
        int roll = dice.Next(1, 10001);
        double lo = Clamp((double?)s["rndMin"] ?? 1.5, 0, double.MaxValue), hi = Clamp((double?)s["rndMax"] ?? 2.5, 0, double.MaxValue);
        double mult = lo + dice.NextDouble() * (hi - lo);
        int jm = (int?)s["jackpotMultiplier"] ?? 43; if (jm <= 0) jm = 1;
        bool addicted = role == "addicted";

        string key;
        double change;
        if (roll <= jackpotThreshold) { key = addicted ? "addictedJackpotMessage" : "jackpotMessage"; change = Math.Floor(bet * mult * jm); }
        else if (roll <= winUpperBound) { key = addicted ? "addictedWinMessage" : "winMessage"; change = Math.Floor(bet * mult); }
        else { key = "lossMessage"; change = -bet; }
        if (change > 0)
        {
            double m = 1.0;
            if (role == "subscriber" && ((bool?)s["subscriberBoostEnabled"] ?? false)) m += Math.Max(0, (double?)s["subscriberBoostPercent"] ?? 0) / 100.0;
            if (role == "vip" && ((bool?)s["vipBoostEnabled"] ?? false)) m += Math.Max(0, (double?)s["vipBoostPercent"] ?? 0) / 100.0;
            if (addicted) m *= 1.0 - Clamp((double?)s["addictedPayoutCut"] ?? 0, 0, 100) / 100.0;
            if (m != 1.0) change = Math.Max(0.0, Math.Round(change * m));
        }
        values["amount"] = Amount(s, Math.Abs(change));
        values["currentPoints"] = Amount(s, Math.Max(0.0, balance + change));
        values["addictedCut"] = Clamp((double?)s["addictedPayoutCut"] ?? 0, 0, 100).ToString("0.##", CultureInfo.InvariantCulture);
        return "alice has " + Amount(s, balance) + " " + points + " and bets " + Amount(s, bet) + " " + points + ".\n"
             + "Roll " + Amount(s, roll) + " of " + Amount(s, 10000) + " · " + Fill((string)s[key] ?? "", values);
    }
