# Schema: what a game declares

The schema is everything the settings window needs to know about a game: the settings it reads, and the window
they are shown in. The game holds both in its host block (`stub/HostSchema.cs`, pasted into the game's action),
reads its values through the same table, and answers the window's `describe` request with the whole thing as
JSON. Values never sit in the schema, only in the settings file.

```json
{
  "host": "blackjack",
  "version": "v17.0.0",
  "settingsFile": "Chat Games/Blackjack Game/BJG-Settings.json",
  "settings": [ { "key": "...", "default": ..., "kind": "...", ... } ],
  "window": { "title": "Blackjack Game", "pages": [ ... ], "help": [ ... ], ... }
}
```

The host block builds this from two declarations:

```csharp
public static readonly UiWindow Window = new UiWindow("Blackjack Game", releases: "aaskjer/Blackjack-Game")
    .Pages(P("table", "Table", "cards"), P("stats", "Stats", "trophy", "leaderboard*", "resetLeaderboard"), "messages", "log", "help", "about")
    .Log("Chat Games/Logs", "BJG-log*.txt", writeLogKey: "logToFile")
    .Help(H("Playing a hand", Cmd("!blackjack <bet>", "Deals you in."), "A hand ends when everyone stands or busts."));

public static readonly Setting[] Table =
{
    S("useBot", true),
    S("globalCooldown", 10),
    S("maxBet", 1000, page: "table", min: 1, help: "Per hand and player."),
    Msg("Wins", "msgWin", "@{user} wins {amount} {pointsName}!", "Win", "Sent when a hand wins.", new[] { "user", "amount", "pointsName" }),
};
```

Keys the conventions know (`conventions/keys.json`: `useBot`, `globalCooldown`, `blockedUsers`, ...) need nothing
but their default; label, help, kind, page and limits come from the conventions. Everything else says what the
conventions cannot know.

## A setting

| Field          | Required | Meaning                                                                                   |
|----------------|----------|-------------------------------------------------------------------------------------------|
| `key`          | yes      | camelCase; canonical for keys the conventions know, free otherwise                          |
| `default`      | yes      | a JSON value; without `kind` it decides the kind                                            |
| `kind`         | no       | one of the kinds below; derived from `default` when missing                                 |
| `page`         | no       | the page's id; the conventions or the window set it otherwise, the rest lands on `game`     |
| `label`        | no       | shown above the field; known keys take it from the conventions                              |
| `help`         | no       | one sentence under the field                                                                |
| `min`, `max`   | no       | for `int`, `double`, `duration`, `percent`                                                  |
| `step`         | no       | the step of a `double`                                                                      |
| `unit`         | no       | shown after the field (`s`, `%`, `px`)                                                      |
| `options`      | no       | the choices of `options` or a `slider`: `["a", "b"]` or `["a|Label A", "b|Label B"]`       |
| `placeholders` | no       | for `template`: the names without braces, shown as chips                                    |
| `section`      | no       | a heading; where it changes, a new section of the page begins                               |
| `group`        | no       | for `template`: the group in the message picker                                             |
| `factor`       | no       | for `percent`: stored = typed × factor (0.01 stores a fraction; default 1)                  |
| `unlimitedAtZero` | no    | for `int`/`long`: 0 in the field means "no limit" and is stored as MaxValue                 |
| `aliases`      | no       | old keys: the store reads them and writes the canonical key back. An alias with a dot (`PlatformEmotes.twitch`) takes the value out of an object of the old file; the object goes once it is empty. Two settings naming the same alias (one old switch that became two) both take its value |
| `aliasFactor`  | no       | multiplies the first alias's value on the move (a fraction to percent: 100)                 |
| `hidden`       | no       | true: no field, but the value stays in the file                                             |
| `confirm`      | no       | for `action`: a yes/no question before the game is asked                                    |
| `row`          | no       | the name of a row: settings that name the same row one after the other share one line of the page, whatever their kind (a bet, a role and a Try button); without it numbers sit three to a line, switches two, everything else alone |
| `requires`     | no       | a `bool`'s key, or a condition on any field (`numberOfSlots>=5`, `cpBetMode=fixed`, `mode!=free`; `=`, `!=`, `<`, `<=`, `>`, `>=`): the field is greyed out while it does not hold |
| `readOnly`     | no       | true: shown, not editable (a status the game writes)                                        |
| `twitchOnly`   | no       | for `bool`: the switch carries a "Twitch only" pill                                         |
| `transient`    | no       | true: shown, never saved, never counted as a change; only a live snapshot for the game carries it (the inputs of a Try button) |
| `live`         | no       | for `action`: the game gets the values as they stand in the window, unsaved and transient ones included. On any other field: the game is asked whenever the field changes (`test` with the field's key and the live values), and `set` in its answer fills other fields (a preset slider that sets five numbers) |
| `panel`        | no       | for `action`: the answer is shown in a box under the button instead of the footer           |
| `auto`         | no       | for `action`: no button; the game is asked when the page is shown and shortly after every change in the window (odds that follow the settings) |
| `columns`      | for `list`, `table` | `"key|kind|label"` per column; kinds `string`, `text` (multi-line, double width), `int`, `double`, `bool`, `emote` (a text field with the picture beside it; a fourth part `|twitch`, `|youtube` or `|kick` says where the picture comes from: Kick from `[emote:ID:name]`, Twitch by name from Twitch/7TV/BTTV/FFZ with what the game's `emotes` answer lists, YouTube shows the emoji itself) |
| `file`         | no       | a file next to the settings file that holds this value instead (a word bank, a question list); relative or absolute |
| `field`        | no       | with `file`: the property in that file that holds the value; without it the file itself is the value |
| `platform`     | no       | for `emote`: `twitch`, `youtube` or `kick`, where the picture comes from                     |

Without `kind`: bool → `bool`, whole number → `int`, decimal → `double`, string → `string`, array → `list`
(which then needs `columns`).

## Kinds

| Kind        | Default     | Control                                    | Notes                                                 |
|-------------|-------------|--------------------------------------------|-------------------------------------------------------|
| `bool`      | bool        | toggle                                     |                                                       |
| `int`       | int         | number field; with `options` a dropdown    | `min`, `max`, `unit`, `unlimitedAtZero`                |
| `long`      | long        | number field                               | like `int`, for big balances                          |
| `double`    | double      | number field; with `options` a dropdown    | `min`, `max`, `step`; dot or comma                    |
| `percent`   | int/double  | number field 0 to 100 with `%`             | stored as the number, not a fraction (see `factor`)   |
| `duration`  | int         | number field with a unit                   | stored in seconds; shown as s/min/h                   |
| `string`    | string      | one-line text field                        |                                                       |
| `text`      | string      | multi-line text field                      |                                                       |
| `template`  | string      | multi-line, placeholder chips, chat preview| `placeholders`; the preview fills them with samples   |
| `options`   | string      | dropdown                                   | `options` required                                    |
| `namelist`  | string      | chips, Enter adds one                      | stored comma-separated                                |
| `grouplist` | string      | chips with Streamer.bot's groups suggested | stored comma-separated                                |
| `path`      | string      | text field with a folder/file dialog       | relative to the Streamer.bot folder is fine           |
| `colour`    | string      | colour field with a picker                 | `#rrggbb` or `#aarrggbb`                              |
| `emote`     | string      | text field with the emote's picture beside | `platform` says `twitch`, `youtube` or `kick`         |
| `slider`    | string/int/double | slider                               | with `options` a slider over the stops (the stop's value is stored, the labels sit under the stops); otherwise from `min` to `max` in `step`, the value beside the label; a default with a decimal makes it a `double` |
| `list`      | array       | row editor: a cell per column, remove, *Add row*, *Restore defaults* | `columns` required; empty rows are dropped on save; with `file`/`field` in a file of its own |
| `table`     | array       | a table, read-only                         | for statistics the game writes; rows from the game's `table` answer |
| `note`      | –           | a paragraph on the page (`help`)           | no value; explains what follows; http(s) addresses are links |
| `action`    | –           | a button                                   | no value; asks the game (`test` with the key); the answer is the footer line, with `panel` a box under the button, with `live` from the current values, with `auto` without a button and by itself. See *Answers* below |

### Answers of an action

The game answers `test` with a line of text, or with a JSON object:

```json
{
  "lines": [ "Jackpot every 2,400 spins.", { "text": "27 %", "big": true },
             { "emotes": ["Kappa", "LUL"], "platform": "twitch", "text": "The slots" } ],
  "bar": [ { "label": "Win", "value": 27, "text": "27 %", "colour": "#16a34a" }, { "label": "Loss", "value": 73 } ],
  "set": { "gridSize": 8, "shipCarrierEnabled": true },
  "status": "Fleet preset applied."
}
```

- `lines` are drawn one under the other; a line with `emotes` shows them as pictures with their names beneath
  (YouTube as the emoji), after its `text` when there is one.
- `bar` is a stacked bar with a legend.
- `set` puts values into other fields of the window as if typed (nothing is saved until Save: a preset button); a
  field whose value changes glows in the accent colour for a moment.
- `status` is the line for the footer.
- `clipboard` is text the window puts on the clipboard (an overlay URL): the game runs on Streamer.bot's thread,
  which has no clipboard, the window's has.

## The window

The window declaration says how the window is called and laid out: what used to be a spec JSON in the settings
action. Everything is optional; a page a setting names but the window does not is made up on the way, `messages`
appears once there are templates, `help` and `about` sit at the bottom of the rail.

```csharp
public static readonly UiWindow Window = new UiWindow("Blackjack Game", releases: "aaskjer/Blackjack-Game", logo: "https://…/logo.png")
    .Pages(P("table", "Table", "cards"), P("stats", "Stats", "trophy", "leaderboard*", "statsTable", "resetLeaderboard"), "messages", "log", P("help", "How to play"), "about")
    .Log("Chat Games/Logs", "BJG-log*.txt", writeLogKey: "logToFile", columns: new[] { "Player|User", "Bet" }, results: new[] { "Win", "Loss", "Push" })
    .Cooldowns(C("dc2d4f17-…", "globalCooldown", "userCooldown"))
    .Pill("freeGame", "Free game on", "Free game off", tooltip: "While it is on, nobody stakes or wins anything.")
    .Senders(cooldownGroup: "Cooldown")
    .Samples("user", "alice", "bet", "1,500", "cards", "🂫(10) 🂺(10)")
    .Answer("resetLeaderboard", "The leaderboard is empty.")
    .Override("egg", hidden: true)
    .Help(H("Playing a hand", Cmd("!blackjack <bet>", "Deals you in."), "A note.", Link("Guide", "https://…")))
    .About(credits: "Responsible: aaskjer", links: new[] { Link("Discord", "https://…") });
```

| Part                 | Meaning                                                                                              |
|----------------------|------------------------------------------------------------------------------------------------------|
| `UiWindow(title, …)` | the window title and About heading; `releases` is `owner/repo` on GitHub (repository and releases links, the update check); `logo` a URL; `logPrefix` for the framework's log lines (default `[Title/Settings]`); `width`, `height` |
| `Pages(…)`           | the pages in order: `P(id, title, icon, fields…)` or the id of a standard page (`messages`, `log`, `help`, `about`). `icon` is an emoji or a conventions name (`gear`, `dice`, `coins`, `clock`, `shield`, `chat`, `trophy`, `monitor`, `list`, `book`, `info`, `cards`, `chart`). `fields` are keys or patterns like `overlay*` the page claims ahead of their own page; a pattern never takes a template off the Messages page, a full key does |
| `Log(folder, files, …)` | the Log page: the folder (relative to the Streamer.bot folder) and the files in it (`BJG-log*.txt`), which the settings action reads and the page follows as they change; `writeLogKey` the bool behind "Write a log file per day", `writeLogHelp`, `columns` after Time and Result as `"Header|field"`, `results` for the filter, `platforms`, `emptyText` |
| `Cooldowns(C(command, global, user), …)` | commands whose cooldowns follow settings: the settings action sets them in Streamer.bot after every save. The command by its id (right-click the command in Streamer.bot, *Copy Id*; an import keeps it, so it is the robust way), or by its text (`!roll`) or name for a command made by hand; `global` and `user` are the keys of the two durations, either may be null |
| `Pill(key, on, off, tooltip)` | a pill in the header that follows a bool setting                                             |
| `Senders(…)`         | the keys of the sender switches on the Messages page where they differ from the conventions' (`useBot`, `sendAction`, `cooldownUseBot`, `cooldownSendAction`, `numberSeparator`); null hides one; `cooldownGroup` names the group the cooldown senders sit in |
| `Samples("k", "v", …)` | fixed sample values for the chat preview's placeholders; live ones (`pointsName`, `amount`, `min`, `max`) the window adds |
| `Answer(action, text)` | what an action would answer, for the framework's preview and screenshots only                      |
| `Override(key, …)`   | `page`, `label`, `help`, `section`, `hidden`, `order` of one setting, where the table should not say it |
| `Help(H(heading, lines…), …)` | the Help page: a line is a note (a string), `Cmd(command, what)` or `Link(text, url)`         |
| `Lang(code, texts…)` | the game's own texts in another language (see *Languages* below): `K(key, label, help)`, `Pg(id, title)`, `Tx(declared, translation)` |
| `About(credits, sections, links, streamerBotLink, authorLink)` | the About page; sections like help; the Streamer.bot and author links are on unless turned off |

### Languages

What the game declares is English. When the layout file names another language (`de`), the window translates
what the conventions know: the texts of the conventions' keys (`conventions/lang/de.json`, `keys`) replace the
declared label and help of those keys, pages are titled by id (`pages`), and short declared texts are looked up
by their English wording (`texts`): section headings, pill texts, page titles, option labels, message groups.
What nothing knows stays as declared. The user's own layout (a renamed field or page) still has the last word.

A game adds what the conventions cannot know, its own keys and sections, with `Lang` on the window; several
calls add up, and these texts are laid over the conventions' for that language:

```csharp
    .Lang("de", K("numDecks", "Decks im Schuh", "Wie viele Decks der Dealer mischt."), K("egg", "Ei"),
                Pg("fleet", "Flotte"), Tx("Shoe", "Schuh"), Tx("Free game on", "Freispiel an"))
```

In the schema this is `lang: { "de": { "keys": …, "pages": …, "texts": … } }`, the shape of a conventions
language file. The help and about pages are shown as declared; a game that wants them in another language
declares them in that language.

Settings the window puts on a standard page (a switch on `log`) sit above that page's own content. Within a
page the table's order holds (`Override(order:)` moves one); a heading where `section` changes.

What the game declares is checked when the window opens: an unknown kind, a requirement on a key that is not a
setting, a page naming a pattern no setting matches, a minimum above a maximum. The window shows a banner and
lists the findings on the About page, so the game's author sees them without reading Streamer.bot's log.

A settings action that still carries a spec of its own (`stub/Stub.spec.cs`, the JSON with the same parts as the
builder, see the git history of `docs/Spec.md`) wins over the window in the schema.

## The game's side

`UiHost.Handle(args, CPH, actionName, host, version, settingsFile, …)` is the first thing the game's action calls.
It answers the settings action's requests and returns true, so the action returns at once; otherwise it returns
false and the game plays. Every request must leave the game as it was: no chat output, no state written. The
request comes in the arguments `uiframework.request`, `uiframework.name`, `uiframework.values` and
`uiframework.var` (the global the answer goes into, so the name the game knows itself by never matters); the
describe answer leaves a line in Streamer.bot's log.

| Request    | When                                     | The game's part                                                     |
|------------|------------------------------------------|---------------------------------------------------------------------|
| `describe` | the window opens                         | nothing: `UiSchema.ToJson` answers with the table and the window    |
| `test`     | a Try button, a live field, an auto action | `test: (name, s) => …` answers by the setting's key with the window's current values; null for "no answer" |
| `validate` | before a save                            | `validate: s => …` answers the problems one per line ("Minimum bet is above the maximum"), or null; the window shows them and asks whether to save anyway |
| `saved`    | after a save, with the saved values      | `saved: s => …` writes an overlay, restarts a timer; the cooldowns the window declares are set by the settings action before this |
| `table`    | a `table` field is shown                 | `table: key => …` answers a JSON array of objects whose fields the columns name |
| `emotes`   | the window opens, for emote pictures     | `emotes: () => …` answers the sources as JSON: `{ "twitch": { "channelId": "…", "sources": ["twitch", "7tv", "bttv", "ffz"] } }` |

**The points system.** `UiCurrency.Detect(CPH, settings)` gives the user variable the points live in
(`Variable`), what they are called (`Name`) and where that came from (`Source`): StreamUP's Currency Core
(`currency_ProductSettings`) or Tawmae's LOYALTY POINTS (`tawmae_Settings_LOYALTY POINTS`) when one is installed,
else the settings `pointsVariable` and `pointsName` (declare both; they are conventions keys with a page of their
own). Detect on every run. The describe answer carries a detected system as `currency`, and the window shows it
as a pill (`Currency Core: points`) and says on the two settings that they are the fallback. Reading and
writing a viewer's points stays the game's: `CPH.GetTwitchUserVarById<long>(userId, currency.Variable, true)` and
`SetTwitchUserVarById` (the YouTube and Kick ones likewise); both systems keep the balance right there.

The values arrive as `UiSettings` with `Bool`, `Int`, `Long`, `Double`, `String`, `Ceiling` (0 as "no limit"),
`Clamp` and `Raw` (the JSON object). `UiSettings.Load(path)` reads the settings file the same way, old keys
through the aliases.

**A file of its own (`file`, `field`):** the value is read on open from `<folder of the settings file>\<file>`
(the property `field`, or the file itself) and written back there on save, atomically with a `.bak`; the rest of
the file stays. The key never enters the settings file; *Reset all* resets only the settings file and leaves
this one (its editor has *Restore defaults*). A missing file shows the `default`, so the file the game reads
anyway (`RT-wordbank.json`) stays the only copy.

## Export and import

The customise mode's bar (the pen, bottom left) has *Export* and *Import*: Export writes the saved settings
(transient ones left out, values from files of their own included) as
`{ "uiframework": "settings", "host", "version", "exported", "settings": { … } }` to a file of the user's
choosing; Import replaces the saved settings, after asking, with such a file or a settings file itself (old keys
through the aliases), keeps the file as it was as `.bak` and rebuilds the window on them. A file of another host
asks; what the window does not know is skipped.

Tooltips have a fixed width and wrap instead of running across the screen.
