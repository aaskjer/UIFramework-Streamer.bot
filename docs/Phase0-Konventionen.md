# Phase 0: Konventionsliste aus den sieben Spiele-GUIs

Stand 2026-10-31. Quelle: die Settings-Klassen der GUI-Dateien (Blackjack, Slot Machine, Battleship, Hangman,
Classic Chat Gamble, Trivia, RhymeTime). Gezählt wurden serialisierte Properties; `[JsonIgnore]` und berechnete
Properties sind ausgenommen.

## Umfang

| Projekt     | Settings-Keys | davon Textvorlagen (`Msg*`) | Overlay | Besonderheiten                             |
|-------------|--------------:|----------------------------:|--------:|--------------------------------------------|
| Blackjack   |           263 |                         167 |      15 | Duel, House, Leaderboard                   |
| Battleship  |           179 |                          70 |      20 | Ship, Heat, Duel, Leaderboard              |
| Trivia      |           177 |                           – |      22 | Suggest (18), Question, Auto, Scoreboard   |
| Hangman     |           146 |                          45 |      20 | Blitz, Evil, Reset, Hint, Streak           |
| CCG         |            63 |                           – |       0 | Reason (5), Jackpot, Free                  |
| RhymeTime   |            61 |                           – |      15 | Battle, Reason                             |
| Slot        |            60 |                           – |       0 | Reason, Free, Jackpot                      |

Textvorlagen machen in den großen Spielen zwei Drittel der Keys aus. Das Schema-Kind `template` mit Chips ist
damit der wichtigste Baustein nach den Standardfeldern.

## Keys in allen oder fast allen Projekten

| Bisherige Namen                       | Projekte | Kanonisch              | Kind      | Seite     |
|---------------------------------------|---------:|------------------------|-----------|-----------|
| UseBot, IsBot, useBot                 |        7 | `useBot`               | bool      | general   |
| SendAction, sendAction                |        7 | `sendAction`           | bool      | general   |
| UseBotCD                              |        7 | `cooldownUseBot`       | bool      | cooldowns |
| SendActionCD                          |        7 | `cooldownSendAction`   | bool      | cooldowns |
| NumberSeparator                       |        7 | `numberSeparator`      | options   | general   |
| BlockedGroups                         |        7 | `blockedGroups`        | grouplist | access    |
| DarkMode                              |        7 | `darkMode`             | bool      | Framework |
| Egg                                   |        7 | `rainbow`              | bool      | Framework |
| BlockedUsers                          |        6 | `blockedUsers`         | namelist  | access    |
| AddictedUsers                         |        6 | `addictedUsers`        | namelist  | access    |
| AddictedGroups                        |        6 | `addictedGroups`       | grouplist | access    |
| CooldownGlobalSeconds, GlobalCooldown |      5+2 | `globalCooldown`       | duration  | cooldowns |
| CooldownUserSeconds, UserCooldown     |      5+2 | `userCooldown`         | duration  | cooldowns |
| PointsName, PointsVarName             |      5+2 | `pointsName`           | string    | points    |
| PointsVar, PointsVariable             |      4+2 | `pointsVariable`       | string    | points    |
| DefaultBet                            |        4 | `defaultBet`           | int       | points    |
| Reason{NegativeAmount,InsufficientFunds,BelowMinimum,AboveMaximum} | 3 | `reason*`  | template  | messages  |
| IsChannelPointsTrigger                |        3 | `isChannelPointsTrigger` | bool    | general   |
| AddictedPayoutCut, AddictedPointsPenaltyPercent | 3 | `addictedPayoutCut` | percent   | access    |

`DarkMode` und `Egg` wandern ins Framework (Layout-Datei, gilt für alle Hosts). `Language` gab es bisher nur in
Trivia und wandert ebenfalls dorthin.

## Familien in zwei Projekten, die Konventionen werden

Sie stehen heute in zwei Spielen, passen aber erkennbar zu weiteren (Trivia und Hangman haben eigene Namen für
dasselbe):

- **Leaderboard** (Blackjack, Battleship; Hangman/Trivia als `Scoreboard*`): `leaderboardSize`
  (LeaderboardSize, ScoreboardTopN), `leaderboardShowMedals`, `leaderboardExcludeBroadcaster`,
  `leaderboardExcludeBot`, `leaderboardSendAction`, `leaderboardAutoReset`, `leaderboardResetScope`,
  `leaderboardUseBot`, `leaderboardHeader/Entry/Empty` (MsgScoreboard*, Scoreboard*, MsgNoLeaderboard,
  MsgNoStatsFound).
- **Overlay** (fünf Spiele, je 15 bis 22 Keys, Namen uneinheitlich): gemeinsam sind `overlayEnabled`,
  `overlayFolder`, `overlayScale` (OverlayScale, OverlayScalePercent), `overlayFontScale`, `overlayPosition`,
  `overlayPort`, `overlayAnimations` (OverlayAnimations, OverlayAnimationsEnabled), `overlayResultSeconds`
  (OverlayResultSeconds, OverlayGameOverFadeSec), `overlayHideOnFreeGame`, `overlayTextColour`,
  `overlayBackgroundColour`, `overlayBackgroundOpacity`, `overlayAccentColour`. Der Rest (`OverlayShow*`,
  Figurenfarben, Kartenfarben) ist spielspezifisch und bleibt im Host-Schema auf Seite `overlay`.
- **Cooldown-Nachrichten**: `globalCooldownMessage`, `userCooldownMessage` (GlobalCooldownMsg,
  GlobalCooldownMessage, …), `maxGamesPerHour`, `messageMaxGamesPerHour`, `turnTimeout`.
- **Zugriff**: `botAllowed`, `broadcasterAllowed` (Hangman, Trivia).
- **Einsätze**: `minBet`, `maxBet`, `fixedBetAmount`, `cpBetMode`, `freeGame`.

## Was spielspezifisch bleibt

Duel*, House*, Ship*, Heat*, Blitz*, Evil*, Suggest*, Question*, Battle*, Jackpot*, Hint*, Streak*, Reset{Twitch,
YouTube,Kick}*, alle `Msg*`-Vorlagen außer den oben genannten. Diese Keys deklariert der Host selbst, mit
`page`, `label`, `help`, `placeholders`. Für Reset-pro-Plattform lohnt später ein eigenes Kind oder eine
Konvention, sobald ein drittes Spiel es braucht.

## Naming-Regeln (Entscheidung 4)

1. camelCase, englisch, keine Abkürzungen außer eingeführten (`cp` für Channel Points bleibt in `cpBetMode`).
2. Wo Streamer.bot einen Namen hat, gilt er: `useBot` (Sub-Aktion "Use Bot Account"), `sendAction`,
   `globalCooldown`/`userCooldown` (CommandSetGlobalCooldownDuration/CommandSetUserCooldownDuration),
   `userName`-Stil für Nutzerbezüge.
3. Zeiten in Sekunden ohne Suffix im Key (`globalCooldown`, nicht `globalCooldownSeconds`); das Kind `duration`
   trägt die Einheit.
4. Prozentwerte als ganze Zahl 0 bis 100 (`overlayScale: 100`), nicht als Bruch.
5. Britische Schreibweise `colour` in Keys, weil `ColourPicker` und `AccentColour` im bestehenden Code so heißen.
   Aliase fangen `Color` ab.
6. Nachrichten heißen `*Message`, Ablehnungsgründe `reason*`, Bestenlisten-Zeilen `leaderboard*`.

Migration: der Store liest beim Laden jeden Alias und schreibt beim nächsten Speichern den kanonischen Key. Die
Spiele selbst lesen über `Get<T>("useBot")`, das ebenfalls Aliase kennt. Kein Nutzer muss eine Datei anfassen.

## Offene Punkte für Phase 1

1. `useBot`, `cooldownUseBot` und `leaderboardUseBot` sind drei Schalter für dieselbe Frage. Prüfen, ob die
   Spiele die Trennung wirklich nutzen; sonst in Phase 1 auf `useBot` zusammenführen und die anderen als Aliase
   führen.
2. `numberSeparator` hat heute die Defaults `,` und `.` je nach Spiel. Kanonischer Default `,`; die Alias-
   Migration behält den gespeicherten Wert, nur Neuinstallationen bekommen den neuen Default.
3. `addictedPayoutCut` ist in Blackjack ein Bruch (`0.5`), in Hangman Prozent (`50`). Die Migration muss den
   Alias `AddictedPayoutCut` mit 100 multiplizieren, wenn der Wert kleiner oder gleich 1 ist.
4. Platzhalter: `{pointsName}` und `{user}` sind überall gleich, Blackjack nutzt zusätzlich eigene. Die Liste in
   keys.json ist der Startbestand; jeder Host ergänzt in seinem Schema.
