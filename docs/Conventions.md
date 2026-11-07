# Conventions and language files

The conventions are what every window knows before a game declares anything: the standard pages, the settings
most chat games share, the placeholders of chat messages, and the translations. They are plain JSON in this
repository and ship inside `UIFramework.dll`, so a game never carries them; a new release of the framework brings
new ones to every game at once.

| File | What it holds |
|---|---|
| [`conventions/keys.json`](../conventions/keys.json) | Standard pages (order, icon), the known keys (kind, default, page, options, limits, aliases), the framework's own keys and the known placeholders |
| [`conventions/lang/en.json`](../conventions/lang/en.json) | English: labels and help of the known keys, page titles, placeholder help, and `ui`, every text of the window itself |
| [`conventions/lang/de.json`](../conventions/lang/de.json) | German, the same shape, plus `texts`: short English wordings (section headings, option labels, column headers) and their German |
| [`conventions/emotes/youtube.json`](../conventions/emotes/youtube.json) | YouTube's own emojis by `:shortcode:` and their picture, for emote fields and lists |
| [`conventions/emotes/twitch.json`](../conventions/emotes/twitch.json) | Twitch's global emotes by name and their id, so Kappa and the like show their picture before Streamer.bot has a Twitch login; `tools/update-twitch-emotes.ps1` refreshes it |

Raw files for tools and translators:
`https://raw.githubusercontent.com/aaskjer/UIFramework-Streamer.bot/main/conventions/keys.json`, and likewise
`conventions/lang/en.json`, `conventions/lang/de.json`.

## Using them in a game

A game that declares a known key names only what differs: `S("useBot")` is a switch on the General page with
its label, help and default from here, translated wherever the window is. The game's own keys, sections and help
are its own; [Schema.md](Schema.md) explains how a game adds texts for them with `Lang`.

The known keys are taken names: a game that declares `maxBet` gets the conventions' limits, old keys, sample
value and translation for it, whatever it labels the field. For a meaning of its own a game uses a key of its own
(`maxSpins`); a known key declared as another kind of value is listed as a problem when the window opens.

`keys.json`'s `about` section holds the links every About page ends with (the UIFramework page, Streamer.bot,
where to report a problem: `{issues}` is the game's issue tracker when it names its repository, else the
framework's). Edit them there; a text is a key of the `ui` section or the text itself. A game turns them off with
`About(defaultLinks: false)` and adds its own credits and links.

## Adding a language

1. Copy `conventions/lang/en.json` to `conventions/lang/<code>.json` (`fr`, `nl`, `es`, a two-letter code) and
   translate the values; keep every key and every `{placeholder}` as it is. A missing entry falls back to English,
   so a partial file works.
2. Add a `texts` section like the German one for the short English wordings games use most.
3. Run `tools/conventions-doc.ps1` so the table below counts the new file, build, and open a pull request.

The build embeds every file in `conventions/lang/`, nothing else to register, and every window offers it.

Without a release: the same file in `UIFramework\lang\<code>.json` beside Streamer.bot (next to
`UIFramework\lib`) is read when the window opens. It adds a language the DLL does not carry, or is laid over one
it does (a correction, a better word), and a file that cannot be read is listed under the red "i" in the window's header. A game can bring
the window's own texts for a language itself as well (`Ui(key, text)` in `Lang`, see Schema.md).

## Contents

Generated from the files by `tools/conventions-doc.ps1`; edit the JSON, not this part.

<!-- generated:start -->
### Standard pages

| Page | Title | Icon | Order |
|---|---|---|---|
| `general` | General | `gear` | 10 |
| `game` | Game | `dice` | 20 |
| `points` | Points & Bets | `coins` | 30 |
| `channelPoints` | Channel points | `gift` | 35 |
| `cooldowns` | Cooldowns | `clock` | 40 |
| `access` | Access | `shield` | 50 |
| `messages` | Messages | `chat` | 60 |
| `leaderboard` | Leaderboard | `trophy` | 70 |
| `stats` | Stats | `chart` | 75 |
| `overlay` | Overlay | `monitor` | 80 |
| `log` | Log | `list` | 90 |
| `help` | Help | `book` | 95 |
| `about` | About | `info` | 99 |

### Known keys

A game that declares one of these keys gets its page, kind, default, label and help from here; it only
names what differs. The aliases are the names the games used before; a settings file that still carries one
is read and written back under the key.

| Key | Kind | Default | Page | Label | Aliases |
|---|---|---|---|---|---|
| `useBot` | bool | `true` | general | Send as bot account | `UseBot`, `IsBot` |
| `sendAction` | bool | `false` | general | Send as /me action | `SendAction` |
| `numberSeparator` | options | `,` | general | Thousands separator | `NumberSeparator` |
| `isChannelPointsTrigger` | bool | `false` | general | Triggered by channel points | `IsChannelPointsTrigger` |
| `pointsVariable` | string | `points` | points | Points variable | `PointsVariable`, `PointsVar` |
| `pointsName` | string | `Points` | points | Points name | `PointsName`, `PointsVarName` |
| `defaultBet` | int | `100` | points | Default bet | `DefaultBet` |
| `minBet` | int | `1` | points | Minimum bet | `MinBet`, `MinimumBet` |
| `maxBet` | int | `0` | points | Maximum bet | `MaxBet`, `MaximumBet` |
| `fixedBetAmount` | int | `0` | points | Fixed bet amount | `FixedBetAmount` |
| `cpBetMode` | options | `input` | points | Channel point bet | `CPBetMode` |
| `freeGame` | bool | `false` | points | Free game | `FreeGame` |
| `globalCooldown` | duration | `10` | cooldowns | Global cooldown | `CooldownGlobalSeconds`, `GlobalCooldown` |
| `userCooldown` | duration | `30` | cooldowns | Per-user cooldown | `CooldownUserSeconds`, `UserCooldown` |
| `maxGamesPerHour` | int | `0` | cooldowns | Games per hour | `MaxGamesPerHour` |
| `turnTimeout` | duration | `60` | cooldowns | Turn timeout | `TurnTimeoutSec` |
| `cooldownUseBot` | bool | `true` | cooldowns | Cooldown messages as bot | `UseBotCD` |
| `cooldownSendAction` | bool | `false` | cooldowns | Cooldown messages as /me | `SendActionCD` |
| `globalCooldownMessage` | template | `""` | cooldowns | Global cooldown message | `GlobalCooldownMsg`, `GlobalCooldownMessage` |
| `userCooldownMessage` | template | `""` | cooldowns | User cooldown message | `UserCooldownMsg`, `UserCooldownMessage` |
| `blockedUsers` | namelist | `""` | access | Blocked users | `BlockedUsers` |
| `blockedGroups` | grouplist | `""` | access | Blocked groups | `BlockedGroups` |
| `addictedUsers` | namelist | `""` | access | Addicted users | `AddictedUsers` |
| `addictedGroups` | grouplist | `""` | access | Addicted groups | `AddictedGroups` |
| `addictedPayoutCut` | percent | `50` | access | Addicted payout cut | `AddictedPayoutCut`, `AddictedPointsPenaltyPercent` |
| `botAllowed` | bool | `false` | access | Bot account may play | `BotAllowed` |
| `broadcasterAllowed` | bool | `true` | access | Broadcaster may play | `BroadcasterAllowed` |
| `reasonNegativeAmount` | template | `""` | messages | Negative amount | `ReasonNegativeAmount` |
| `reasonInsufficientFunds` | template | `""` | messages | Not enough points | `ReasonInsufficientFunds`, `MsgNotEnoughPoints` |
| `reasonBelowMinimum` | template | `""` | messages | Below minimum bet | `ReasonBelowMinimum` |
| `reasonAboveMaximum` | template | `""` | messages | Above maximum bet | `ReasonAboveMaximum` |
| `reasonNoInput` | template | `""` | messages | No amount given | `ReasonNoInput` |
| `messageMaxGamesPerHour` | template | `""` | messages | Games per hour reached | `MsgMaxGamesPerHour` |
| `leaderboardSize` | int | `5` | leaderboard | Entries shown | `LeaderboardSize`, `ScoreboardTopN` |
| `leaderboardShowMedals` | bool | `true` | leaderboard | Medals for the top three | `LeaderboardShowMedals` |
| `leaderboardExcludeBroadcaster` | bool | `false` | leaderboard | Hide broadcaster | `LeaderboardExcludeBroadcaster` |
| `leaderboardExcludeBot` | bool | `true` | leaderboard | Hide bot account | `LeaderboardExcludeBot` |
| `leaderboardUseBot` | bool | `true` | leaderboard | Send as bot account | `LeaderboardUseBot` |
| `leaderboardSendAction` | bool | `false` | leaderboard | Send as /me action | `LeaderboardSendAction` |
| `leaderboardAutoReset` | options | `never` | leaderboard | Automatic reset | `LeaderboardAutoReset` |
| `leaderboardResetScope` | options | `all` | leaderboard | Reset scope | `LeaderboardResetScope` |
| `leaderboardHeader` | template | `""` | leaderboard | Header line | `MsgScoreboardHeader`, `ScoreboardHeader` |
| `leaderboardEntry` | template | `""` | leaderboard | Entry line | `MsgScoreboardEntry`, `ScoreboardEntry` |
| `leaderboardEmpty` | template | `""` | leaderboard | Empty board | `MsgScoreboardEmpty`, `ScoreboardEmpty`, `MsgNoLeaderboard`, `MsgNoStatsFound` |
| `overlayEnabled` | bool | `false` | overlay | Overlay enabled | `OverlayEnabled` |
| `obsConnectionId` | int | `0` | overlay | OBS connection | `ObsConnectionId` |
| `obsSceneName` | string | `""` | overlay | Scene | `ObsSceneName` |
| `obsSourceName` | string | `""` | overlay | Source | `ObsSourceName` |
| `overlayFolder` | path | `""` | overlay | Overlay folder | `OverlayFolder` |
| `overlayPort` | int | `0` | overlay | Overlay port | `OverlayPort` |
| `overlayPosition` | options | `bottom-left` | overlay | Position | `OverlayPosition` |
| `overlayScale` | percent | `100` | overlay | Scale | `OverlayScale`, `OverlayScalePercent` |
| `overlayFontScale` | percent | `100` | overlay | Font scale | `OverlayFontScale`, `OverlayFontScalePercent` |
| `overlayAnimations` | bool | `true` | overlay | Animations | `OverlayAnimations`, `OverlayAnimationsEnabled` |
| `overlayResultSeconds` | duration | `8` | overlay | Result shown for | `OverlayResultSeconds`, `OverlayGameOverFadeSec` |
| `overlayHideOnFreeGame` | bool | `false` | overlay | Hide in free games | `OverlayHideOnFreeGame` |
| `overlayTextColour` | colour | `""` | overlay | Text colour | `OverlayTextColor` |
| `overlayBackgroundColour` | colour | `""` | overlay | Background colour | `OverlayBackgroundColor` |
| `overlayBackgroundOpacity` | percent | `100` | overlay | Background opacity | `OverlayBackgroundOpacity` |
| `overlayAccentColour` | colour | `""` | overlay | Accent colour | `OverlayAccentColor` |

### Known placeholders

| Placeholder | Meaning |
|---|---|
| `{user}` | The viewer's display name |
| `{target}` | The other player |
| `{pointsName}` | Name of the points |
| `{currentPoints}` | The viewer's points right now |
| `{amount}` | The bet |
| `{min}` | Minimum bet |
| `{max}` | Maximum |
| `{remaining}` | Seconds left on the cooldown |
| `{reason}` | Why it did not work |
| `{rank}` | Place on the board |
| `{score}` | The entry's score |
| `{medal}` | Medal emoji for the top three, else empty |
| `{count}` | Number of entries |
| `{addictedCut}` | Payout cut in percent |
| `{latestTag}` | Newest release version |
| `{error}` | Error text |

### Languages

| File | Keys | Pages | Placeholders | Texts | Window texts |
|---|---|---|---|---|---|
| `lang/de.json` | 72 of 72 | 13 of 13 | 16 of 16 | 120 | 239 of 239 |
| `lang/en.json` | 72 | 13 | 16 | 0 | 239 |
| `lang/es.json` | 72 of 72 | 13 of 13 | 16 of 16 | 120 | 239 of 239 |
<!-- generated:end -->
