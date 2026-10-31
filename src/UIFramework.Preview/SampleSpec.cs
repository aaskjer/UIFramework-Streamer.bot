using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using Newtonsoft.Json.Linq;

namespace UIFramework.Preview
{
    /// <summary>A cut-down Blackjack: enough pages to see every building block, with the keys the game uses today.</summary>
    internal static class SampleSpec
    {
        public static WindowSpec Blackjack(string settingsPath)
        {
            var spec = new WindowSpec
            {
                Title = "Blackjack Game",
                Version = "v16.3.1",
                GitHubRepo = "aaskjer/Blackjack-Game",
                LogoUrl = "https://github.com/aaskjer/Blackjack-Game/blob/main/Assets/logo.png?raw=true",
                LogPrefix = "[Blackjack Game/Settings]",
                SettingsPath = settingsPath,
                Defaults = JObject.Parse(@"{
                    'UseBot': true, 'SendAction': false, 'UseBotCD': true, 'SendActionCD': false, 'NumberSeparator': ',',
                    'PointsVariable': 'points', 'PointsVarName': 'Points', 'CardDisplayMode': 'emojivalue',
                    'FreeGame': false, 'MinBet': 50, 'MaxBet': 0, 'DefaultBet': 100,
                    'DealerStandsOn': 17, 'NumDecks': 6, 'BlackjackPayout': 1.5, 'ReshuffleThreshold': 0.25,
                    'AllowDoubleDown': true, 'AllowSplit': true, 'AllowInsurance': false, 'AllowSurrender': true,
                    'BlockedUsers': '', 'BlockedGroups': '', 'AddictedUsers': '', 'AddictedGroups': '', 'AddictedPayoutCut': 0.5,
                    'GlobalCooldown': 0, 'UserCooldown': 30, 'MaxGamesPerHour': 0,
                    'OverlayFolder': '', 'OverlayScale': 1.0, 'OverlayCardRedColor': '#E74C3C', 'OverlayBackgroundColor': '#1E1E1E',
                    'LogToFile': true,
                    'MsgGameStart': '@{user} is dealt {cards} ({value}).',
                    'MsgBlackjackWin': 'Blackjack! @{user} wins {amount} {points}.',
                    'MsgRegularWin': '@{user} wins {amount} {points}.',
                    'MsgLoss': '@{user} loses {amount} {points}.',
                    'GlobalCooldownMsg': 'The table is still being cleared, {0} seconds left.',
                    'UserCooldownMsg': '@{0}, you can play again in {1} seconds.'
                }".Replace('\'', '"'))
            };

            spec.Messages.AddRange(new[]
            {
                new MessageDef("Playing a hand", "MsgGameStart", "Hand dealt", "Opens a round. The hand and the dealer's upcard are appended to it.", "user", "cards", "value"),
                new MessageDef("Wins", "MsgBlackjackWin", "Blackjack", "Sent on a natural blackjack.", "user", "amount", "points"),
                new MessageDef("Wins", "MsgRegularWin", "Win", "Sent on an ordinary 1:1 win.", "user", "amount", "points"),
                new MessageDef("Losses", "MsgLoss", "Loss", "Sent when the dealer wins.", "user", "amount", "points"),
                new MessageDef("Cooldown", "GlobalCooldownMsg", "Global cooldown", "Sent while the table-wide cooldown runs.", "0"),
                new MessageDef("Cooldown", "UserCooldownMsg", "User cooldown", "Sent while a player's own cooldown runs.", "0", "1"),
            });
            spec.Senders = new SenderKeys { UseBot = "UseBot", SendAction = "SendAction", CooldownUseBot = "UseBotCD", CooldownSendAction = "SendActionCD", NumberSeparator = "NumberSeparator" };
            spec.SampleValues = ctx =>
            {
                var f = ctx.Fields;
                string points = f.LiveText("PointsVarName", "Points").Trim();
                return new Dictionary<string, string>
                {
                    { "user", "alice" }, { "points", points }, { "amount", f.Amount(1500, "NumberSeparator") },
                    { "cards", "🂫(10) 🂺(10)" }, { "value", "20" }, { "0", "alice" }, { "1", "12" }
                };
            };
            spec.TokenHelp = (key, token) =>
            {
                switch (token)
                {
                    case "user": return "The player's name, without the @";
                    case "points": return "What your currency is called";
                    case "amount": return "The amount won, lost or returned";
                    case "cards": return "The cards in the hand";
                    case "value": return "The hand total";
                    case "0": return key == "UserCooldownMsg" ? "The player's name" : "Seconds left";
                    case "1": return "Seconds left";
                    default: return null;
                }
            };

            spec.HeaderPills.Add(new HeaderPill("Free Game, from the Table page. While it is on, nobody stakes or wins anything.",
                ctx => ctx.Fields.LiveSwitch("FreeGame") ? ("Free game on", PillStyle.Success) : ("Free game off", PillStyle.Muted)));

            spec.Log.Folder = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(settingsPath), "Logs");
            spec.Log.Columns.AddRange(new[] { new LogColumn("Player", "User"), new LogColumn("Bet", "Bet"), new LogColumn("Change", "Change"), new LogColumn("After", "After") });
            spec.Log.Results.AddRange(new[] { "Win", "Blackjack", "Loss", "Bust", "Push" });
            spec.Log.WriteLogKey = "LogToFile";
            spec.Log.WriteLogHelp = "Writes every settled hand to a dated BJG-log-yyyy-MM-dd.txt in the Logs folder.";

            spec.Help.Add(new HelpSection("The idea")
                .Note("Get closer to 21 than the dealer without going past it."));
            spec.Help.Add(new HelpSection("Playing a hand")
                .Command("!blackjack <bet>", "Deals in.")
                .Command("hit", "Draw another card.")
                .Command("stand", "Stop and keep the total you have."));
            spec.Help.Add(new HelpSection("This table, right now")
                .Live(ctx => new[]
                {
                    $"The dealer draws until reaching {ctx.Fields.LiveNumber("DealerStandsOn", 17):0}, then stops.",
                    $"A natural blackjack pays {ctx.Fields.LiveNumber("BlackjackPayout", 1.5).ToString("0.##", CultureInfo.InvariantCulture)}x the bet.",
                    ctx.Fields.LiveSwitch("FreeGame") ? "Free Game is on: nothing is staked." : $"Minimum bet {ctx.Fields.Amount(ctx.Fields.LiveNumber("MinBet", 50), "NumberSeparator")}."
                }));

            spec.About.Sections.Add(new HelpSection("Needs a points system")
                .Note("Hands are played with the points of a currency system. These two are picked up without any setup:")
                .Link("Currency Core by StreamUP", "https://streamup.tips/product/currency-core")
                .Link("LOYALTY POINTS by tawmae", "https://tawmae.xyz/loyalty-points"));

            spec.Clamp = s =>
            {
                if ((int?)s["NumDecks"] < 1) s["NumDecks"] = 1;
                if ((int?)s["NumDecks"] > 8) s["NumDecks"] = 8;
            };
            spec.Warnings = s =>
            {
                var problems = new List<string>();
                if (!(bool)s["FreeGame"] && string.IsNullOrWhiteSpace((string)s["PointsVariable"]))
                    problems.Add("Free Game is off and no points variable is named, so no stake can ever be taken.");
                return problems;
            };

            spec.Pages.Add(new PageSpec("table", "Table", "🃏", BuildTable));
            spec.Pages.Add(new PageSpec("access", "Access", "👥", BuildAccess));
            spec.Pages.Add(new PageSpec("overlay", "Overlay", "📺", BuildOverlay));
            spec.Pages.Add(StandardPages.Messages());
            spec.Pages.Add(StandardPages.Log());
            spec.Pages.Add(StandardPages.Help("How to play"));
            spec.Pages.Add(StandardPages.About());
            return spec;
        }

        private static UIElement BuildTable(PageContext ctx)
        {
            var f = ctx.Fields;
            var page = Ui.PageStack();
            page.Children.Add(Ui.Section("Stakes", first: true));
            page.Children.Add(Ui.Row(
                f.LongField("Minimum bet", "MinBet", "The smallest stake a hand can be played for."),
                f.LongField("Maximum bet", "MaxBet", "0 means no ceiling.", unlimitedAtZero: true),
                f.LongField("Default bet", "DefaultBet", "Used when a player gives no amount.")));
            page.Children.Add(f.Toggle("Free Game", "FreeGame", "Nothing is staked and nothing is won."));

            page.Children.Add(Ui.Section("Dealer and shoe"));
            page.Children.Add(Ui.Row(
                f.IntCombo("Dealer stands on", "DealerStandsOn", Ui.Range(16, 21), "The total the dealer stops drawing at."),
                f.IntCombo("Decks in the shoe", "NumDecks", Ui.Range(1, 8), "How many 52-card decks are shuffled together."),
                f.DoubleCombo("Blackjack pays", "BlackjackPayout", new[] { (1.0, "1:1"), (1.2, "6:5"), (1.5, "3:2"), (2.0, "2:1") }, "The bonus on a natural.")));
            page.Children.Add(f.PercentField("Reshuffle when this much is left", "ReshuffleThreshold", 0.01, "The shoe is shuffled when it runs this low.", 1, 90));

            page.Children.Add(Ui.Section("Moves"));
            page.Children.Add(Ui.Row(
                f.Toggle("Double down", "AllowDoubleDown", "Double the bet for exactly one more card."),
                f.Toggle("Split", "AllowSplit", "Split a starting pair into two hands.")));
            page.Children.Add(Ui.Row(
                f.Toggle("Insurance", "AllowInsurance", "Stake half the bet against a dealer blackjack."),
                f.Toggle("Surrender", "AllowSurrender", "Fold a fresh hand and keep half the bet.")));
            return Ui.Scroll(page);
        }

        private static UIElement BuildAccess(PageContext ctx)
        {
            var f = ctx.Fields;
            var page = Ui.PageStack();
            page.Children.Add(Ui.Section("Currency", first: true));
            page.Children.Add(Ui.Row(
                f.TextField("Points variable", "PointsVariable", "The name of the Streamer.bot user variable your currency is kept in."),
                f.TextField("Points name", "PointsVarName", "What the currency is called in chat messages.")));
            page.Children.Add(f.StringComboField("Cards in chat", "CardDisplayMode",
                new[] { ("emojivalue", "Card and number - 🂡(11)"), ("emoji", "Card only - 🂡"), ("value", "Number only - 11") },
                "How cards are written in chat."));

            page.Children.Add(Ui.Section("Kept out"));
            page.Children.Add(Ui.SmallLabel("Blocked viewers", 0));
            page.Children.Add(f.NameList("Blocked viewers", "BlockedUsers", "Add a name and press Enter", "Names matched without regard to case."));
            page.Children.Add(Ui.SmallLabel("Blocked groups", 10));
            page.Children.Add(f.NameList("Blocked groups", "BlockedGroups", "Add a Streamer.bot group", "Streamer.bot user groups, matched exactly."));

            page.Children.Add(Ui.Section("Addicted list"));
            page.Children.Add(Ui.SmallLabel("Addicted viewers", 0));
            page.Children.Add(f.NameList("Addicted viewers", "AddictedUsers", "Add a name and press Enter", "These viewers win less."));
            page.Children.Add(Ui.Row(new[] { 1.0, 2.0 },
                f.PercentField("Payout cut", "AddictedPayoutCut", 0.01, "How much of an addicted player's winnings is withheld.", 0, 99),
                new System.Windows.Controls.TextBlock()));

            page.Children.Add(Ui.Section("Cooldowns"));
            page.Children.Add(Ui.Row(
                f.IntField("Table, everyone", "GlobalCooldown", "Seconds before anyone can start another hand."),
                f.IntField("Table, per player", "UserCooldown", "Seconds one player waits between hands."),
                f.IntField("Games per hour", "MaxGamesPerHour", "0 means no limit.", unlimitedAtZero: true)));
            return Ui.Scroll(page);
        }

        private static UIElement BuildOverlay(PageContext ctx)
        {
            var f = ctx.Fields;
            var page = Ui.PageStack();
            page.Children.Add(Ui.Section("Files", first: true));
            page.Children.Add(f.TextField("Overlay folder", "OverlayFolder", "Where the overlay files are written. Empty uses the default."));
            page.Children.Add(Ui.Section("Look"));
            page.Children.Add(Ui.Row(
                f.ColourField("Red suits", "OverlayCardRedColor", "Hearts and diamonds."),
                f.ColourField("Background", "OverlayBackgroundColor", "Behind the table.")));
            page.Children.Add(f.PercentField("Scale", "OverlayScale", 0.01, "Size of the overlay.", 25, 400));
            return Ui.Scroll(page);
        }
    }
}
