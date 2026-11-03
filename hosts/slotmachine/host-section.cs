    // ================================================================================================
    // HOST: Slot Machine Game
    // ================================================================================================

    private const string GameActionName = "[SMG] - Slot Machine Game ";   // answers uiframework.describe; the export's name, trailing space and all
    private const string LogPrefix = "[Slot Machine Game/Settings]";
    private const string SlotsCmdId = "f8a9c5fa-edb7-4de5-bb92-c0fdf53f005e";
    private static readonly string SettingsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Chat Games", "Slot Machine Game", "SMG-Settings.json");
    private static readonly string LogsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Chat Games", "Logs");

    private const long SampleBalance = 100000;
    private const long SamplePool = 23400;

    /// <summary>What the DLL calls. Public methods, string arguments, no framework types: found by reflection.
    /// Everything that needs CPH is queued as a job and run by Pump on Streamer.bot's thread; the odds and the
    /// test spin need nothing but the engine, so they answer at once.</summary>
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

        // The two cooldowns live on the !slots command, where Streamer.bot reads them.
        public void OnSaved(string settingsJson)
        {
            var settings = JObject.Parse(settingsJson);
            _owner.Enqueue(() =>
            {
                _owner.CPH.CommandSetGlobalCooldownDuration(SlotsCmdId, Math.Max(0, (int?)settings["globalCooldown"] ?? 0));
                _owner.CPH.CommandSetUserCooldownDuration(SlotsCmdId, Math.Max(0, (int?)settings["userCooldown"] ?? 0));
            });
        }

        public string Test(string name, string settingsJson)
        {
            var settings = JObject.Parse(settingsJson);
            switch (name)
            {
                case "trySpin": return TrySpin(settings, _dice);
                default: return null;
            }
        }

        public string ReadTable(string key) => null;

        // What would not do what it looks like, one problem per line; the window asks before saving anyway.
        public string Validate(string settingsJson)
        {
            var s = JObject.Parse(settingsJson);
            var problems = new List<string>();
            long min = (long?)s["minBet"] ?? 0, max = (long?)s["maxBet"] ?? 0;
            if (min > 0 && max > 0 && min > max)
                problems.Add("The minimum bet (" + min + ") is above the maximum (" + max + "), so every bet is refused.");
            if (!((bool?)s["freeGame"] ?? false) && string.IsNullOrWhiteSpace((string)s["pointsVariable"]))
                problems.Add("Free mode is off but the points variable is empty, so nobody can stake anything.");
            if (((bool?)s["useJackpotPool"] ?? false) && ((long?)s["jackpotBet"] ?? 0) <= 0)
                problems.Add("The jackpot pool is on with a bet of 0 to win it, so every jackpot takes the whole pool.");
            if (((int?)s["minEmoteWeight"] ?? 1) > ((int?)s["maxEmoteWeight"] ?? 10))
                problems.Add("The smallest emote weight is above the largest, so no emote is within the weights and all of them play.");
            if (((bool?)s["isChannelPointsTrigger"] ?? false) && string.IsNullOrWhiteSpace((string)s["rewardId"]))
                problems.Add("Channel points start a spin, but the reward ID is empty, so no redemption starts one.");
            if (((bool?)s["isChannelPointsTrigger"] ?? false) && ((string)s["cpBetMode"] ?? "cost") == "fixed" && ((long?)s["fixedBetAmount"] ?? 0) <= 0)
                problems.Add("Channel points bet a fixed amount, but it is 0, so no redemption can spin.");
            return problems.Count == 0 ? null : string.Join("\n", problems);
        }

        // What the emote pictures of the Twitch list are looked up with: the broadcaster for the channel's 7TV, BTTV,
        // FFZ and Twitch emotes, client id and token for the Twitch API. Read where the DLL asks, like the old window did.
        public string EmoteSources()
        {
            try
            {
                return new JObject
                {
                    ["twitchUserId"] = _owner.CPH.TwitchGetBroadcaster()?.UserId,
                    ["clientId"] = _owner.CPH.TwitchClientId,
                    ["token"] = _owner.CPH.TwitchOAuthToken
                }.ToString(Newtonsoft.Json.Formatting.None);
            }
            catch (Exception ex)
            {
                _owner.CPH.LogDebug(LogPrefix + " No Twitch connection for emote pictures: " + ex.Message);
                return null;
            }
        }

        public IEnumerable<string> ReadLogLines()
        {
            if (!Directory.Exists(LogsDir))
                return new string[0];
            return Directory.GetFiles(LogsDir, "SMG-log*.txt").OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                .SelectMany(ReadLinesSafe).Where(l => !string.IsNullOrWhiteSpace(l)).ToList();
        }

        private static IEnumerable<string> ReadLinesSafe(string file)
        {
            try { return File.ReadAllLines(file); } catch (Exception) { return new string[0]; }
        }
    }

    // ---- The odds and the test spin, from the settings as they stand in the window ----

    private static string PlatformKey(JObject s) => ((string)s["tryPlatform"] ?? "twitch").Trim().ToLowerInvariant();
    private static string PlatformName(string key) => key == "youtube" ? "YouTube" : key == "kick" ? "Kick" : "Twitch";
    private static List<SlotEmote> EmotesOf(JObject s, string platform) =>
        SlotEngine.EmotesFrom(s[platform == "youtube" ? "emotesYoutube" : platform == "kick" ? "emotesKick" : "emotesTwitch"]);

    private static SlotEngine.Rules RulesOf(JObject s) => new SlotEngine.Rules
    {
        Slots = Clamp((int?)s["numberOfSlots"] ?? 4, 3, 6),
        PoolSize = Math.Max(0, (int?)s["emotePoolSize"] ?? 10),
        MinWeight = Math.Max(1, (int?)s["minEmoteWeight"] ?? 1),
        MaxWeight = Math.Max(1, (int?)s["maxEmoteWeight"] ?? 10),
        Rerolls = Math.Max(0, (int?)s["rerollAttempts"] ?? 2),
        MatchChance = Clamp((double?)s["matchChance"] ?? 18, 0, 100) / 100.0,
        JackpotChance = Clamp((double?)s["jackpotChance"] ?? 1, 0, 100) / 100.0
    };

    private static double[] PayoutsOf(JObject s) => new[]
    {
        0, 0,
        Math.Max(0, (double?)s["twoMatchPayout"] ?? 150),
        Math.Max(0, (double?)s["threeMatchPayout"] ?? 300),
        Math.Max(0, (double?)s["fourMatchPayout"] ?? 500),
        Math.Max(0, (double?)s["fiveMatchPayout"] ?? 1000),
        Math.Max(0, (double?)s["sixMatchPayout"] ?? 1500)
    };

    private static int Clamp(int v, int min, int max) => v < min ? min : v > max ? max : v;
    private static double Clamp(double v, double min, double max) => v < min ? min : v > max ? max : v;

    private static string SlotsDescription(int slots)
    {
        switch (slots)
        {
            case 3: return "Two alike pay; all three alike is the jackpot.";
            case 5: return "Two to four alike pay; all five alike is the jackpot.";
            case 6: return "Two to five alike pay; all six alike is the jackpot.";
            default: return "Two or three alike pay; all four alike is the jackpot.";
        }
    }

    private static string Percent(double value) =>
        value.ToString(value > 0 && value < 1 ? "0.##" : "0.#", CultureInfo.InvariantCulture) + " %";

    private static string Amount(JObject s, double value)
    {
        string sep = (string)s["numberSeparator"] ?? ".";
        string text = Math.Round(value).ToString("N0", CultureInfo.InvariantCulture);
        return string.IsNullOrEmpty(sep) ? text.Replace(",", "") : text.Replace(",", sep);
    }

    private static string TrySpin(JObject s, Random dice)
    {
        string platform = PlatformKey(s);
        string role = ((string)s["tryRole"] ?? "viewer").Trim().ToLowerInvariant();
        bool free = (bool?)s["freeGame"] ?? false;
        long bet = Math.Max(0, (long?)s["tryBet"] ?? 1000);
        string currency = (string)s["pointsName"] ?? "Points";
        bool pool = (bool?)s["useJackpotPool"] ?? true;
        long jackpotBet = Math.Max(0, (long?)s["jackpotBet"] ?? 50000);
        bool addicted = role == "addicted";

        if (!free)
        {
            if (bet <= 0)
                return "Enter a whole bet above 0 first.";
            long min = Math.Max(0, (long?)s["minBet"] ?? 0), max = Math.Max(0, (long?)s["maxBet"] ?? 0);
            string reason = null;
            if (bet > SampleBalance) reason = (string)s["reasonInsufficientFunds"];
            else if (min > 0 && bet < min) reason = (string)s["reasonBelowMinimum"];
            else if (max > 0 && bet > max) reason = (string)s["reasonAboveMaximum"];
            if (reason != null)
            {
                var refusal = new Dictionary<string, string>
                {
                    ["user"] = "alice", ["amount"] = Amount(s, bet), ["currency"] = currency,
                    ["min"] = Amount(s, min), ["max"] = Amount(s, max)
                };
                string text = Fill(reason ?? "", refusal);
                return "Refused · " + Fill(((string)s["invalidBetMessage"] ?? "").Replace("{reason}", text), refusal);
            }
        }

        var emotes = EmotesOf(s, platform);
        if (emotes.Count == 0)
            return PlatformName(platform) + " has no emotes yet: the game logs an error and chat gets nothing.";

        var rules = RulesOf(s);
        var spin = SlotEngine.Roll(rules, SlotEngine.Pool(rules, emotes, dice), dice);
        string slotsLine = string.Join(" || ", spin.Emotes.Select(e => e.Code));
        if (platform != "kick")
            slotsLine = System.Text.RegularExpressions.Regex.Replace(slotsLine, @"\[emote:([0-9]+):([^\]]+)\]", "$2");

        long payout = 0, poolWon = 0;
        if (spin.IsWin)
        {
            payout = SlotEngine.BasePayout(bet, SlotEngine.PayoutPercent(spin, PayoutsOf(s)), spin.Matched.Weight);
            double boost = 1.0;
            if (platform == "twitch")
            {
                if (role == "subscriber" && ((bool?)s["subscriberBoostEnabled"] ?? false)) boost += Math.Max(0, (double?)s["subscriberBoostPercent"] ?? 0) / 100.0;
                if (role == "vip" && ((bool?)s["vipBoostEnabled"] ?? false)) boost += Math.Max(0, (double?)s["vipBoostPercent"] ?? 0) / 100.0;
            }
            if (boost > 1.0) payout = (long)Math.Round(payout * boost);
            if (spin.IsJackpot && !free && pool && bet >= jackpotBet)
            {
                poolWon = SamplePool;
                payout += poolWon;
            }
            if (addicted)
            {
                double keep = 1.0 - Clamp((double?)s["addictedPayoutCut"] ?? 50, 0, 99) / 100.0;
                payout = (long)Math.Round(payout * keep);
                poolWon = (long)Math.Round(poolWon * keep);
            }
        }

        long newBalance = free ? SampleBalance : SampleBalance - bet + (spin.IsWin ? payout : 0);
        bool feedsPool = !spin.IsWin && !free && !addicted && bet > 0;
        bool poolInText = pool && (spin.IsWin || feedsPool);
        long poolAfter = (poolWon > 0 ? 0 : SamplePool) + (feedsPool ? bet : 0);
        var values = new Dictionary<string, string>
        {
            ["user"] = "alice",
            ["slots"] = slotsLine,
            ["amount"] = free ? "" : Amount(s, spin.IsWin ? payout - poolWon : bet),
            ["newBalance"] = Amount(s, newBalance),
            ["currency"] = free ? "" : currency,
            ["addictedCutPercent"] = Clamp((double?)s["addictedPayoutCut"] ?? 50, 0, 99).ToString("0.##", CultureInfo.InvariantCulture),
            ["jackpotMinBet"] = pool && !free ? Amount(s, jackpotBet) : "",
            ["jackpotLossContribution"] = pool && feedsPool ? Amount(s, bet) : "",
            ["jackpotPoolCurrent"] = pool ? Amount(s, poolAfter) : "",
            ["jackpotPoolWinAmount"] = pool && poolWon > 0 ? Amount(s, poolWon) : ""
        };
        string kind = spin.IsJackpot ? "Jackpot" : spin.IsWin ? "Win" : "Loss";
        string key = free ? "freeGame" + kind + "Message" : addicted ? "addicted" + kind + "Message" : char.ToLowerInvariant(kind[0]) + kind.Substring(1) + "Message";
        string template = JackpotBlock((string)s[key] ?? "", poolInText);
        string message = Fill(template, values);
        string prefix = spin.IsJackpot ? "Jackpot" : spin.IsWin ? spin.MatchCount + " alike" : "No match";
        // The window draws the slots as the emotes' pictures (UIFramework docs/Schema.md, action: an "emotes" line).
        return new JObject
        {
            ["lines"] = new JArray
            {
                "alice has " + Amount(s, SampleBalance) + " " + currency + " and spins " + (free ? "for free" : "for " + Amount(s, bet) + " " + currency) + " on " + PlatformName(platform) + ".",
                new JObject { ["emotes"] = new JArray(spin.Emotes.Select(e => e.Code)), ["platform"] = platform },
                prefix + " · " + message
            }
        }.ToString(Newtonsoft.Json.Formatting.None);
    }

    private static string Fill(string template, Dictionary<string, string> values)
    {
        foreach (var pair in values)
            template = template.Replace("{" + pair.Key + "}", pair.Value ?? "");
        return template;
    }

    // The same as the game's ApplyJackpotBlock: with the pool in play the markers go, without it the part between them.
    private static string JackpotBlock(string template, bool poolInText)
    {
        if (poolInText)
            return template.Replace("{jackpot}", "").Replace("{/jackpot}", "");
        string stripped = System.Text.RegularExpressions.Regex.Replace(template, @"\{jackpot\}.*?\{/jackpot\}", "", System.Text.RegularExpressions.RegexOptions.Singleline);
        return System.Text.RegularExpressions.Regex.Replace(stripped, @"[ \t]{2,}", " ").Trim();
    }

    // --- Slot Machine engine (keep identical in SlotMachineGame.cs and SMG-GUI.cs) ---
    // The spin as the game plays it, with no Streamer.bot in it: the settings window's stub carries the same block
    // so its odds and its test spin come out of the very code that spins in chat (tools\check-settings.ps1 holds
    // the two copies together).
    public class SlotEmote
    {
        public string Name { get; set; }
        public string Code { get; set; }
        public int Weight { get; set; } = 5;
        public string Id { get; set; }
    }

    public static class SlotEngine
    {
        public sealed class Rules
        {
            public int Slots = 4, PoolSize = 10, MinWeight = 1, MaxWeight = 10, Rerolls = 2;
            public double MatchChance = 0.18, JackpotChance = 0.01;
        }

        public sealed class Spin
        {
            public readonly List<SlotEmote> Emotes = new List<SlotEmote>();
            public int MatchCount;
            public bool IsWin, IsJackpot;
            /// <summary>The emote of the biggest group, whose weight sets the payout.</summary>
            public SlotEmote Matched;
        }

        public sealed class Odds
        {
            public double Jackpot, Win, Loss, Return;
        }

        /// <summary>The emotes a list setting holds: rows without a code are skipped, a weight below 1 is 1.</summary>
        public static List<SlotEmote> EmotesFrom(JToken rows)
        {
            var list = new List<SlotEmote>();
            if (!(rows is JArray array))
                return list;
            foreach (var row in array.OfType<JObject>())
            {
                string code = ((string)row["Code"] ?? (string)row["code"] ?? "").Trim();
                if (code.Length == 0)
                    continue;
                int weight = (int?)row["Weight"] ?? (int?)row["weight"] ?? 5;
                list.Add(new SlotEmote { Code = code, Weight = Math.Max(1, weight), Name = (string)row["Name"] ?? (string)row["name"], Id = (string)row["Id"] ?? (string)row["id"] });
            }
            return list;
        }

        /// <summary>The emotes a spin draws from: those within the weights, shuffled, cut to the pool size; all of
        /// them when nothing is within the weights.</summary>
        public static List<SlotEmote> Pool(Rules rules, List<SlotEmote> available, Random rnd)
        {
            var filtered = available.Where(e => e.Weight >= rules.MinWeight && e.Weight <= rules.MaxWeight).ToList();
            filtered = filtered.OrderBy(x => rnd.Next()).ToList();
            List<SlotEmote> pool = rules.PoolSize > 0 && rules.PoolSize <= filtered.Count
                ? filtered.Take(rules.PoolSize).ToList()
                : new List<SlotEmote>(filtered);
            if (pool.Count == 0)
                pool = new List<SlotEmote>(available);
            return pool;
        }

        /// <summary>One spin over a pool: the jackpot roll, the match roll, and the rerolls that keep a spin that
        /// isn't meant to match from repeating an emote.</summary>
        public static Spin Roll(Rules rules, List<SlotEmote> pool, Random rnd)
        {
            var spin = new Spin();
            bool isJackpot = rnd.NextDouble() < rules.JackpotChance;
            bool allowMatches = !isJackpot && rnd.NextDouble() < rules.MatchChance;

            if (isJackpot)
            {
                var jackpotEmote = WeightedPick(pool, rnd);
                for (int i = 0; i < rules.Slots; i++)
                    spin.Emotes.Add(jackpotEmote);
            }
            else
            {
                for (int i = 0; i < rules.Slots; i++)
                {
                    SlotEmote emote;
                    int attempt = 0;
                    bool repeatUnavoidable = pool.All(p => spin.Emotes.Any(e => e.Code == p.Code));
                    if (allowMatches || repeatUnavoidable)
                        emote = WeightedPick(pool, rnd);
                    else
                    {
                        do
                        {
                            emote = WeightedPick(pool, rnd);
                            attempt++;
                        }
                        while (i > 0 && spin.Emotes.Take(i).Any(e => e.Code == emote.Code) && attempt < rules.Rerolls);
                    }
                    spin.Emotes.Add(emote);
                }
            }

            var groups = spin.Emotes.GroupBy(e => e.Code).OrderByDescending(g => g.Count()).ToList();
            var biggest = groups.First();
            spin.MatchCount = biggest.Count();
            spin.Matched = biggest.First();
            spin.IsWin = spin.MatchCount >= 2;
            spin.IsJackpot = spin.MatchCount == rules.Slots;
            return spin;
        }

        /// <summary>Rare, heavy emotes come up less: the chance of each is the square root of (max weight + 1 - its weight).</summary>
        public static SlotEmote WeightedPick(List<SlotEmote> emotes, Random rnd)
        {
            int maxW = emotes.Max(e => e.Weight);
            double exponent = 0.5;
            var invertedWeights = emotes.Select(e => Math.Pow(maxW + 1 - e.Weight, exponent)).ToList();
            double total = invertedWeights.Sum();
            double roll = rnd.NextDouble() * total;
            double acc = 0;
            for (int i = 0; i < emotes.Count; i++)
            {
                acc += invertedWeights[i];
                if (roll < acc)
                    return emotes[i];
            }
            return emotes[0];
        }

        /// <summary>The payout tier of a spin in percent: by how many are alike, the jackpot tier when all are.
        /// payouts holds two to six alike at indexes 2 to 6.</summary>
        public static double PayoutPercent(Spin spin, double[] payouts)
        {
            if (spin.IsJackpot) return payouts[6];
            int alike = Math.Min(6, Math.Max(2, spin.MatchCount));
            return payouts[alike];
        }

        /// <summary>What a win pays before boosts, pool and cut: bet × payout % × weight ÷ 100, never less than the bet.</summary>
        public static long BasePayout(long bet, double percent, int weight)
        {
            double raw = bet * percent * weight / 100.0;
            return Math.Max((long)Math.Round(raw), bet);
        }

        /// <summary>What the rules really give over many spins: how often the jackpot, a win and a loss come out, and
        /// what a bet of 100 comes back as on average, for a plain viewer without boosts, pool or cut.</summary>
        public static Odds Simulate(Rules rules, List<SlotEmote> available, double[] payouts, int spins, int seed)
        {
            if (available == null || available.Count == 0 || spins <= 0)
                return null;
            var rnd = new Random(seed);
            int jackpots = 0, wins = 0;
            double back = 0;
            for (int i = 0; i < spins; i++)
            {
                var spin = Roll(rules, Pool(rules, available, rnd), rnd);
                if (!spin.IsWin)
                    continue;
                if (spin.IsJackpot) jackpots++;
                else wins++;
                back += BasePayout(100, PayoutPercent(spin, payouts), spin.Matched.Weight);
            }
            return new Odds
            {
                Jackpot = 100.0 * jackpots / spins,
                Win = 100.0 * wins / spins,
                Loss = 100.0 * (spins - jackpots - wins) / spins,
                Return = back / spins
            };
        }
    }
    // --- end Slot Machine engine ---
