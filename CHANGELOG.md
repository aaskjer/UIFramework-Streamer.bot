# Changelog

The release workflow puts the section for the tagged version into the GitHub release, so every version needs
its section here before the tag is pushed. Everything before 1.0.0 is published as a pre-release.

## [Unreleased]

- **Problems behind a red "i".** What the game declares that does not fit, and what fails while the window is
  open (a page that cannot be built, a handler that throws, a failed save, an unreadable layout file), no longer
  show as a banner and a list on the Log or About page. A red **i** appears in the header instead; a click opens a
  panel that lists them in two groups, the failures with their time, and *Copy* puts the whole list on the
  clipboard for a bug report. Esc, the close button or a click beside it closes it. Every line still goes to
  Streamer.bot's log. The status line's "Something went wrong" points at the **i**. Preview:
  `-windowshots <folder> -problems` photographs the window with the **i**, then with its panel open.

## [0.10.0] - 2026-11-07

A tour of the window on its first open.

- **The window explains itself.** A game declares a tour in its window, `.Tour(Tip(target, title, text), ...)`:
  on the window's first open the window dims and one part after the other is ringed in the accent colour, with a
  note beside it (*Back*, *Next*, *End tour*; Esc ends it, the arrow keys and Enter step through it). A stop
  points at a setting (its page comes to the front), a page (`page:messages`; the page stays bright beside its
  rail entry), a part of the window (`rail`, `save`, `saveExit`, `cancel`, `status`, `reset`, `resetPage`,
  `theme`, `customise`, `tour`) or nothing (`start`, `end`, a note of the game's own). Parts and the standard
  pages bring their own texts in English, German and Spanish, a setting its label and help; the game's own texts
  translate through `Tx`. `"tourSeen": true` in `uiframework.layout.json` keeps it from starting again; the **?**
  at the top right shows it whenever the user wants. A stop the window cannot show (a page the user hid,
  `customise` in a locked window) is left out; a target that is neither a setting, a page nor a part is listed
  like any other mistake in the declaration. See [docs/Schema.md](docs/Schema.md#the-tour).
- **The demo game has a tour**: the pages, a setting, the *Roll* button, the Messages page, the unsaved-changes
  line, saving, resetting, the theme, the customise mode and the **?** itself.
- **Host block:** `UiSchema.Tip` and `UiWindow.Tour`. A game without a tour needs nothing new; an older DLL
  ignores a tour it does not know.
- **Preview:** `-windowshots <folder> -tour` photographs every stop of the tour (`tour-01.png`, ...).

## [0.9.13] - 2026-11-07

Spanish and every language offered, neutral credits, saving that keeps what the game wrote, a sturdier loader
(stub 0.2.0), and the review's findings fixed.

- **Spanish.** `conventions/lang/es.json` translates every known key, page, placeholder and window text, and the
  short wordings German translates.
- **Every language is offered.** The language picker lists every language there are texts for, by its own name
  (English, Deutsch, Español): the DLL's files, a file in `UIFramework\lang\` beside Streamer.bot (a new language
  or corrections, without a release), and the languages a game brings texts for. A game without texts of its own
  gets what the conventions translate. A game that names its window's language has no picker.
- **A game can translate the window itself** for a language the framework lacks: `Ui(key, text)` and
  `Ph(placeholder, help)` in `Lang`.
- **A relabelled known key keeps the game's wording** where the conventions do not translate it; before, a language
  without a conventions file showed the conventions' English label over the game's own.
- **Neutral credits.** The framework names no author: a game without credits shows none, and every About page ends
  with the links `conventions/keys.json` lists (`about.links`): the UIFramework page on aaskjer.github.io,
  Streamer.bot and *Report something that looks wrong* (the game's issues when it names its repository, else the
  framework's). `About(defaultLinks: false)` leaves them out; `authorLink` does nothing any more.
- **More said to a game's author:** a known key declared as another kind of value (it would still take the
  conventions' limits, old keys and translations), a section split by another on its page (its heading would show
  twice), an icon name the framework does not know (it shows a dot instead of the word).
- **Fixes to the fixes:** a request hung in the game action no longer blocks the next ones (taking over a window and
  reopening one work again); a list moving into its own file survives the open-time theme move and Reset all, and
  Reset all leaves lists in their own files; whole numbers stay exact past 2^53; the next save also writes a
  switch, dropdown or slider shown other than stored; a hidden number box without a number keeps its value; a list
  file unreadable at save keeps the window's copy of its untouched lists; `UiSettings.Load` survives a bad path; an
  older DLL gets "no answer" as before instead of the stub's marker; the sample thousands separator follows the
  window's language.

- **The host block needs no usings.** `stub/HostSchema.cs` names every type in full (`Newtonsoft.Json.Linq.JObject`,
  `System.IO.File`, ...), so it compiles in any game action whatever its `using` lines say, right below them, at the
  end or inside `CPHInline`, and a game's own usings can no longer clash with it (a `Path` or `Formatting` of
  another namespace). Before, the game's action had to bring seven usings, and the file pasted alone into a new
  Execute Code failed with some 150 errors ("The type or namespace name 'JObject' could not be found").
  `tools/check-stub.ps1` compiles the block alone now. A game's present block keeps working;
  `tools/update-host-block.ps1` brings the new one, and takes only what stands between the template's markers.
- **Getting-Started is copy and paste.** Five steps to the running demo game, each with the ready file to paste
  (`examples/DemoGame/DemoGame.cs`, `stub/Stub.cs`) and nothing to change, what to do when a step fails, then how
  to make it your own game and how to add the block to a game you already have. The demo game carries its host
  block below the usings like every game, and names its command by its text (`C("!roll", ...)`), so a `!roll`
  made by hand gets the cooldowns too.
- **Twitch emotes show without a Twitch login.** A fresh Streamer.bot has no Twitch token, so the window could not
  ask the Twitch API and Kappa, LUL and the other global emotes stayed without a picture. Their ids now ship in
  `conventions/emotes/twitch.json`; with a login the window still asks Twitch itself, for the channel's emotes too.
  `tools/update-twitch-emotes.ps1` refreshes the list.

- **The coins icon shows.** A page with the conventions' `coins` icon (Points & Bets) showed an empty box: 🪙 is
  missing from the emoji font WPF draws with, so it is 💰 now.
- **Docs with pictures, and the conventions published.** Getting-Started, Customize, Schema and the README show
  the window; [docs/Conventions.md](docs/Conventions.md) lists the standard pages, known keys, placeholders and
  language files, and explains how to add a language. `tools/conventions-doc.ps1` keeps its tables current, and CI
  checks them.
- **German windows show the cooldown senders again.** The Messages page's "send as bot" and "send as action"
  switches for the cooldown messages were missing in German: the message groups are translated ("Abklingzeit"),
  the cooldown group was looked for in English. It is found in the window's language now.
- **A game handler that hangs no longer freezes the window** (stub 0.2.0). A request that has started gets 30
  seconds, then the window carries on without its answer; before, it waited for good and could not be closed. A
  game that has not sent its schema for a minute no longer locks its settings until Streamer.bot restarts: the
  next run opens the window.
- **A declined save leaves nothing behind.** The window's values go into a draft; only a save that is written
  takes them over. Before, a save declined at a warning kept them in the window's copy, and after a theme or
  language switch they looked saved while the file still held the old ones.
- **A number pulled into its limits is written.** A value outside a field's min/max (or no number at all) that the
  window shows as its limit or default is written with the next save, also when the user did not touch that field;
  so is what the game's clamp changed. Before, the box said 25 and the game kept reading 50.
- **A save that cannot read the settings file stops** and says why, instead of writing every list file from the
  copy the window opened with. A list moving out of the settings file into its own lands there with the first
  save.
- **Import reads like the settings file:** old keys with their factors on any Windows language (an old 0.18 is 18 %
  on a German system too), old keys inside an object, the current key winning. It lays the import over the file as
  it is now and writes only the lists it brings.
- **Reset all** keeps read-only settings and those on pages the user hid, and the game hears of the lists in their
  own files as they are, not as defaults.
- **First install checks `minHostStub` too** (stub 0.2.0): a release that needs a newer settings action is not
  installed by an older game; "0.2.0.0" no longer counts as newer than "0.2.0".
- **Loader details** (stub 0.2.0): a DLL that no longer matches its hash is set aside as `.broken` and its version
  offered again, and an installed file that is not the published one is fetched anew; the hash file is written
  before the DLL. Every game uses the UIFramework another game already loaded, so two versions never run side by
  side, and the log says which runs. One request to the game at a time, so two runs never read each other's answer;
  the run serving a reopened window hands the one-window flag back. A `latest.json` version that is no plain number
  is refused in the release check too. A cooldown above a day is set to a day with a line in the log.
- **Host block:** `UiSettings.Long` reads a whole number exactly (no detour through a double), and `Load` never
  throws, also on a path with invalid characters.
- **Conditions compare what is stored, shown or not.** A `percent` compares as its fraction
  (`reshuffleThreshold>=0.5` for 50 %), a `duration` in seconds, an "unlimited" 0 as the number it is saved as; a
  field hidden by the user no longer compares differently. `!list` holds for an empty list.
- **The check names a comparison that never holds:** `<`, `<=`, `>`, `>=` against text, or on a field that holds
  no number. A slider on a scale (`min`, `max`, `step`) is no longer reported as "without options".
- **A huge number in the settings file breaks nothing:** a whole number with more digits than a long reads as the
  largest value instead of failing the page.
- **Saving is not stopped by fields it never writes:** a Try field left empty, or a number hidden by a `showWhen`
  or its section's condition. Fields on another page are still checked.
- **A page pattern leaves the sender switches and the log switch where they are**, so no key gets two fields that
  overwrite each other on save. A page naming one by key is listed as a problem.
- **The placeholder chips' tooltips follow a language switch**; a host's own help is kept.
- **Percent fields keep their decimals.** Leaving the box rewrites only a number outside its range, to that limit;
  12.345 stays 12.345. A value in the file that is no number shows the default instead of an empty box that blocks
  saving.
- **Customise keeps rows together.** A field dropped at the top or bottom of a field that shares a line goes above
  or below the whole line, not between its members. The log page's "log file per day" switch is greyed in the
  customise mode, which has no Save.
- **A slider's kept value goes on Reset page and `set`** also when the slider already sits on the stop it is put
  on; its label follows.
- **German windows have no English left** in the customise tag "hidden", the empty-page note, the path and colour
  tooltips, an action's "done"/"failed" and the table's "Nothing here yet"/"Read again".
- **A layout file another program holds is not lost.** It is read again a few times; if it is still held, the
  window starts from the defaults for once and does not write over it. Only a file that is no JSON goes to `.broken`.
- **Numbers the same on every Windows:** decimal boxes take a minus where the setting allows negatives and refuse a
  paste that is no number; a list cell refuses "1,000" like the table (also when saved while it has the focus) and
  shows "2.5", not "2,5"; a value an action sets finds its option on a German system.
- **Smaller fixes:** emote pictures load over HTTPS only; `update-host-block` sees a change in letter case; the
  release workflow refuses a pre-release tag before publishing and keeps the published DLL on a re-run; the demo's
  dice are thread-safe.

- **Settings action (stub 0.2.0):** a game's answer is unset from Streamer.bot's globals as soon as it is read, so
  the Twitch token in the emote sources no longer stays visible in the variables. A DLL saved from a browser loads
  (it was refused as "downloaded from the internet"). When the newest installed DLL cannot be loaded, the next
  older one opens the window. The hash a download was checked with is kept beside the DLL and checked on every
  load. A release that names a higher `minHostStub` is not offered. A game takes this with its next release, by
  pasting the new `stub/Stub.cs` as its settings action.
- **Nothing unchecked from `latest.json`.** The settings action installs no release without a `sha256`, fetches the
  DLL over `https://` only and refuses a `version` that is not a version number before it becomes a folder name.
- **The release check no longer holds the window up to 10 s.** Offline (unreachable, timed out) it does not try the
  CDN after the API; a network sign-in page instead of `latest.json` falls back to the CDN.
- **A second click brings the window to the front.** While a game's window is open (or still opening), another run
  of its settings action neither asks the game nor checks for a release again, and leaves the first run's queue alone.
- **The hint for a hand-fetched DLL shows offline too.** Its example path no longer fails on `<version>` when
  `latest.json` cannot be read.

- **A save keeps what it did not change.** The window writes the settings file as it is at that moment with the
  fields the user changed laid over it, instead of the copy it read when it opened: a viewer added from chat, a
  question suggested from chat or a status the game wrote while the window was open survive a save that did not
  touch them. A list kept in a file of its own is written only when its list changed.
- **A damaged list file is kept.** One that cannot be read is copied to `.broken` before the window writes over it,
  and a list file (a JSON array) now gets its `.bak` on every save like the settings file.
- **Numbers keep their decimals.** A number field shows up to ten decimals instead of two, so 0.005 no longer
  shows, and saves back, as 0.01, nor 0.001 as 0.
- **The language switch asks first.** In a locked window, picking another language with unsaved changes asks to
  save them, as the theme button does, instead of dropping them.
- **Numbers the same on every Windows.** Option values, dropdowns, sliders, text fields and the host block's
  `UiSettings.String`/`Double`/`Long` read and write numbers with a point, so a German system no longer turns 0.5
  into "0,5"; `Long` no longer overflows on a huge stored value.
- **A bad stored value no longer breaks a page.** A number field whose value is no number shows its default, a
  number stored as text ("0,5" too) is read, and a fraction in a whole-number field is shown instead of cut.
- **`min`/`max` hold for `int`, `long` and `duration`.** A value outside is saved as the nearest limit, and the
  field shows what was saved; a `min` below 0 lets the field take a minus.
- **A big default makes a `long` field.** A whole-number default beyond an int (also where the conventions say
  `int`) is no longer cut to 2147483647 on save.
- **Dropdowns and stop sliders keep an unknown value.** A stored value none of the options has stays as an extra
  entry, selected, and is saved back unchanged instead of the first option.
- **Number columns take numbers.** A table cell of an `int`, `long` or `double` column takes digits (and a minus,
  a decimal mark) only, and a cell that is no number goes back to what it held instead of saving 0.
- **`readOnly` in both spellings; more for the host block.** `readOnly` and `readonly` are read on a setting and a
  column. `S()` takes `readOnly`, `step` and `group`. A column flag right after the kind (`"wins|int|readonly"`,
  `"face|emote|twitch"`) is a flag, not the label.
- **`null` hides any sender switch.** An explicit `null` in `senders` hides `useBot`, `sendAction`,
  `cooldownUseBot`, `cooldownSendAction` and `cooldownGroup`, as documented; before only `numberSeparator` obeyed.
- **Conditions.** `!key` holds while a switch is off or a field is empty. A gate the window does not show is read
  from its saved value instead of counting as met. A field the user's layout moves to another section takes that
  section's condition. An answer box that moves under its row hides with its button.
- **Typed decimals are not guessed.** "1,000" or "1.000,5" in a decimal field is refused as ambiguous instead of
  read as 1.
- **A damaged layout file is kept.** `uiframework.layout.json` that is not JSON is copied to `.broken` and logged;
  a value of the wrong type in it (`"order": "2"`) reads as not set instead of breaking the window.
- **A check that got no answer asks.** When the game did not answer `validate` (a timeout, a disabled action),
  the window asks whether to save unchecked instead of taking it for "no problems" (stub 0.2.0).
- **A hung game action no longer locks the window.** The window can be opened again while a request still hangs;
  a request the window gave up on is skipped instead of run late (stub 0.2.0). An error in a click handler is
  logged and shown in the status line instead of closing the window.
- **Emote sources are asked once per window**, not once per emote cell.
- **Rebuilds no longer leak.** A section heading follows its fields through a binding; every rebuild (language,
  theme, customise mode) kept the old page alive before.
- **Two windows at once** keep their own theme, accent, rainbow and language.
- **Cooldowns are set safely** (stub 0.2.0): a misnamed key leaves the cooldown as it is instead of setting 0, one
  command that fails leaves the others set, and the game always hears of the save.
- **The stub writes the layout file carefully** (stub 0.2.0): only on a change, atomically, never over a file it
  could not read. TLS 1.2 is added without pinning the process; the download mutex is honoured; the dialogs name
  the current version's folder.
- **Customise-mode drops** check that the field or page came from this window; a message template can no longer be
  dropped on another page through the rail, a field dropped on its own page stays where it is, and a field moved
  to another page leaves its old row.
- **Release workflow** builds the tag's own commit, carries `minHostStub` over and never takes `latest.json` back.
- **Docs:** the usings the host block needs; the nested `RunAction` pitfall. `tools/update-host-block.ps1` keeps a
  `.bak` and refuses to drop lines of the game's own unless `-Force`.
- Smaller: Ctrl+S does nothing in the customise mode; a name typed into a name list but not entered counts as a
  change when closing; an answer with only `set`/`status` keeps the result panel's previous lines.
- **Text keeps its spaces.** A `string` field saves what was typed, spaces at either end included (`" | "`,
  `"🎲 "`); names, paths, colours and tokens are still trimmed.
- **Durations in minutes and hours.** A `duration` with `unit` `min` or `h` is typed and shown in that unit and
  stored in seconds (1.5 min is 90); `min`/`max` stay seconds.
- **A greyed-out field looks it** on every kind: a `requires` that does not hold also dims the field.
- **German follows the game's own wording.** A conventions key the game gave a label (or help) of its own is
  translated by the game's `Tx` for that wording; without one the conventions' German for the key is shown, as
  before. `K(key, label)` without a help keeps the conventions' help.
- **`stats` and `channelPoints` pages** have an icon and a place in the order (icon name `gift` is new).
- **Reset all keeps what the window does not show:** hidden settings and keys it has no field for.
- **A page pattern leaves a moved field alone.** A field the user moved to another page is not claimed back by
  `overlay*`; a claimed field's page is the claiming one, and a template a page claims by key leaves the Messages
  picker.
- **More checks:** a section condition for a section no setting is in, an old key that is another setting's key.
- **Aliases:** an old key that is a setting of its own keeps its value instead of being moved and reset; a dotted
  alias feeds shared keys too and its old value goes also when the canonical key is in the file.
- **Host block:** `UiSettings.Load` falls back to the `.bak` of the last save on a damaged file and says so
  (`Problem`, `FromBackup`, an optional log); a relative path starts in the Streamer.bot folder.
- **Docs:** the game action's code is its last sub-action.

## [0.9.12] - 2026-11-07

About links once, no empty logo square, a table hint that fits the table.

- The About page lists every address once: a game whose own links repeat its repository or releases, which the
  framework already shows, no longer gets them twice. Without a declared logo there is no empty square beside
  the name.
- The hint under a list table mentions the checkboxes only when the table has a switch column (a word list has
  none).

## [0.9.11] - 2026-11-07

German only where a game offers it; Lock() keeps a window as declared.

- German is optional for a game: the window offers English and only the languages the game declares with `Lang`.
  A game without `Lang("de")` is never shown half in German, whatever an old layout file asks for; the language
  picker lists only what is offered and is gone when that is English alone. `Lang("de")` with no texts offers
  German from the conventions.
- `Lock()` on the window (`"locked": true` in the schema) locks the layout for everyone: no customise mode, and a
  user's renames, moves, hidden fields and accent colour are not applied. Theme and language stay the user's; the
  language picker moves into the footer. The host block gains `Lock()`; a game takes it with its next release.

- The preview's `-windowshots <folder>` photographs the whole window per page, rail, header and footer included,
  with the page at its full height, for a game's setup guide (`-pages game,emotes`, `-names access=access--cp`).

## [0.9.10] - 2026-11-06

The customise mode in the window's real layout, fields dropped beside each other; the OBS fields on one line.

- The customise mode shows a page as the window lays it out, side by side where fields share a line, instead of
  one field per line. Dropping a field at the left or right edge of another puts it beside that one; at its top
  or bottom, on a line of its own. The layout file keeps it as `fields.<key>.row`.
- The OBS connection, scene and source sit on one line, in every game that declares `obsConnectionId`,
  `obsSceneName` and `obsSourceName` (now keys of the conventions, with a `row`). A number field on its own takes
  a third of the width, as beside two others, instead of the whole line.

## [0.9.9] - 2026-11-06

List tables edited in place; fields, switches and whole sections bound to a dropdown's options.

- The list table is edited in place, as the games' own windows did before UIFramework: a click into a cell edits
  it, a switch column is a checkbox in every row that toggles with one click, several rows can be picked and
  removed at once (Remove row or Delete). Alternating rows and column lines make the cells easy to tell apart.
  The edit panel under the table is gone.
- Fields bound to a dropdown's options, any kind of field. `showWhen` on a setting shows it only while its
  condition holds (`questionSource=igdb`); `=` and `!=` take several values (`questionSource=opentdb|wikidata`, one
  of them; `!=custom|igdb`, none of them), and `&&` joins conditions that must all hold. `shows` on a dropdown
  names per option the fields it shows. `.Section(name, showWhen:)` on the window shows a whole section, heading and
  fields, on a condition. Every condition is checked when the window opens: an unknown key or a value the dropdown
  has no option for is listed on the Log page. `requires` keeps greying out, and its `hide:` form still works. The
  host block gains `showWhen:` and `shows:` on `S(...)` and `Section(...)` on the window; a game takes them with its
  next release.

## [0.9.8] - 2026-11-06

Wide lists as tables in the style of the Log page; rows with buttons; headings that hide with their fields.

- A list with a `text` column or more than three columns is a table now, in the style of the Log page: one line
  per row, a search box above, and the picked row in full underneath to edit, every column a field of its size;
  Add row, Remove row and Restore defaults below. A row keeps the properties no column shows (a timestamp the game
  writes). Lists of up to three short columns (an emote and its weight) keep the row editor.
- The picked row of a grid (the Log page, a list table) is drawn in the accent colour instead of white on white.
- In a named `row` a button takes only the width it needs, and an action's answer box goes under the whole row; the
  box stays hidden until there is an answer. A section heading whose fields are all hidden by `hide:` hides too.
- A column label in the host block may contain a bar (`"Wrong answers (a | b | c)"`); trailing `readonly` and an
  emote platform are told apart from it. German texts for the common column names (Question, Category, Word,
  Hint, Weight, …) in the conventions.
- The preview's `-pick n` picks the n-th row of every grid before a shot, so a list table shows its edit panel.

## [0.9.7] - 2026-11-06

Lists filled by an action, searched and with read-only columns; masked fields; requirements that hide; German deeper.

- What the last hosts asked for: an action's `set` fills a `list` (its rows as an array or as JSON text), a list
  of eight rows or more gets a search box (hidden rows are still saved), a column may be `readonly` (the game
  fills it, the user reads it), a text field may be `masked: true` (dots with an eye button, saved like any
  other), and `requires` with `hide:` in front hides a field instead of greying it out. In another language the
  window now translates list column headers, the name-list placeholders, the log page's switch and the help and
  about pages line by line, through the conventions' `texts` and a game's `Tx`. The preview's `-shots` wires
  requirements and live fields like the window does, so a greyed-out or hidden field shows as such. The host
  block gains `masked:` on `S(...)` and `|readonly` on a column; a host takes it with its next release.
- Hangman (v13.0.0) and Trivia (v9.0.0) are the seventh and eighth hosts on the generic stub; `hosts/hangman` and
  `hosts/trivia` carry their blocks and schemas. Gaps seen there, for later releases: an action's `set` cannot
  fill a `list`; lists have no search, filter or read-only column; no masked field kind for secrets; `requires`
  greys out but cannot hide; list column headers and the namelist placeholder stay English in German.

## [0.9.6] - 2026-11-05

Schema problems on the Log page; Free mode from chat in every host.

- Schema problems are listed on the Log page, highlighted on top of the entries, where the host's author looks
  for what went wrong; the banner points there. A window without a log page keeps them on the About page.
- Host blocks: every host but Classic Chat Gamble gained a Free mode chat switch with a Started and an Ended
  message (Slot Machine v16.2.0, Blackjack v17.2.0, Battleship v2.2.0, RhymeTime v2.2.0, High-Low v2.1.0), High-Low
  and RhymeTime their first custom triggers. Nothing of the framework changed for it.

## [0.9.5] - 2026-11-05

Room for long emote names in a list.

- A number column of a list (`int`, `double`) is half as wide as a text one: a weight is short, the emote beside
  it is what needs the room (Slot Machine's emote lists).

## [0.9.4] - 2026-11-05

Pictures for YouTube's own emojis and for Kick emotes typed short.

- Emote cells and the emotes of a result know YouTube's own emojis: typed as `:name:` (`:face-purple-rain-drops:`),
  their picture comes from a table embedded from `conventions/emotes/youtube.json` (100 of them, gathered from
  live chats; YouTube publishes no list), which the `youtube` object of a game's `emotes` answer may extend. A
  Unicode emoji still shows itself. A Kick emote may be typed short as `ID:name`, without the `[emote:` and `]`
  chat renders it with; the picture and the name beneath a result's emote come from either spelling
  (`EmotePictures.KickCode`, `EmotePictures.NameOf`). The preview's `-settle <ms>` gives emote pictures that long
  to arrive before a `-shots` shot.

## [0.9.3] - 2026-11-05

Every host on the generic stub; the spec-carrying stub retires.

- Battleship Game is the sixth and last host on the generic stub (v2.1.0, in German, 162 of its keys translated in
  the block): the summaries, the presets, the broadcaster lookup, the leaderboard reset, the OBS setup and the
  Heat markers are answered by the game, a save hands the Click to Fire settings to a live overlay through
  `saved:`; custom triggers for every finished battle on top. `hosts/battleship` lost its `spec.json` and
  `host-section.cs`, and with the last spec-carrying host gone `stub/Stub.spec.cs` and `tools/embed-spec.ps1` are removed; `tools/check-stub.ps1` compiles the one stub
  and the demo. Gaps seen there, for a later release: the column headers of the log page and its "Write a log
  file per day" switch stay English.
- Blackjack Game is the fifth host on the generic stub (v17.1.0, in German, 215 of its keys translated in the
  block): the leaderboard reset and the OBS setup run in the game and answer with what happened, the window
  declares the cooldowns of three commands. `hosts/blackjack` lost its `spec.json`, `host-section.cs` and the
  embedded `Stub.cs`; Battleship is the last host on the spec-carrying stub.
- Slot Machine Game is the fourth host on the generic stub (v16.1.0, in German): its test spin is answered by the
  game with the engine that spins in chat, the emote pictures' sources come through `emotes:` of `UiHost.Handle`,
  and `hosts/slotmachine` lost its `spec.json` and `host-section.cs`. Gaps seen there, for a later release: the
  column headers of a list and the placeholders of a tag box ("Add a Streamer.bot group") stay English.
- RhymeTime is the third host on the generic stub (v2.1.0, in German). An action's answer may carry
  `clipboard`: text the window puts on the clipboard, since the game's thread has none (the overlay URL).
- Classic Chat Gamble is the second host on the generic stub (v25.1.0, in German too); `hosts/chatgamble` lost its
  `spec.json` and `host-section.cs`, the schema carries the window. `tools/spec-to-window.ps1` writes an answer
  that is an object as its JSON and takes the log's file pattern from the old host section.
- The host block and the stubs no longer use Regex to clean an action's name (`UiSchema.Clean`): a game action
  that ships without a reference to System.dll compiles again. A game's own code must not use a `JArray`
  collection initializer for the same reason (`new JArray(a, b)` does).
- Conventions: the two boost percentages lost the "(%)" in their labels, which the percent field adds itself.

## [0.9.2] - 2026-11-05

The window in German all the way, a game's own translations, a one-row customise bar.

- German all the way: in another language than English the window now translates what the game declared, not
  only what it left out. The conventions' key texts replace the declared label and help of those keys, pages are
  titled by id, and a new `texts` section of `conventions/lang/de.json` translates declared sections, pill
  texts, page titles, option labels and message groups by their English wording; what nothing knows stays as
  declared, the user's layout still has the last word. A game ships its own translations with
  `Window.Lang("de", K(…), Pg(…), Tx(…))`, which the schema carries under `lang` and the window lays over the
  conventions' texts. The conventions gained the keys the games share (`logToFile`, the OBS trio and
  `setupObsOverlay`, `refundFailedRedemptions`, `duelEnabled`, `resetLeaderboard`, the subscriber and VIP
  boosts, `tryBet`, `tryRole`, `rewardId`), the pages `stats` and `channelPoints`, and German for the sections
  and pills of the six games.

- The customise bar is one row: colour and language with their labels beside them, the layout and file actions
  as icon buttons with tooltips (like the footer's), *Done*, and the hint as the line underneath over the full
  width, so the bar no longer takes a third of a narrow window.

## [0.9.1] - 2026-11-04

The demo as an import, the stubs without WPF references, a points system the window knows about, and the first
host on the generic stub.

- High-Low is the first host moved to the generic stub: its window is declared in its host block (written from
  the old spec by the new `tools/spec-to-window.ps1`, which turns a host's `spec.json` and the cooldowns of its
  host section into the `UiWindow` declaration), its game action answers through `UiHost.Handle`, its settings
  action is `stub/Stub.cs`. `hosts/highlow` keeps the block and the schema; its spec files are gone.
- German: the language in the customise bar now translates the window itself (buttons, status line, dialogs,
  menus, the Messages, Log and About pages' chrome, the customise mode), not only the conventions' labels. The
  texts sit in `conventions/lang/<code>.json` under `ui`; the code reads them through `Text.T(key)`, English
  where a language lacks one. What a game declares (labels, help, the help and about pages) is shown as declared.
- Points systems: `UiCurrency.Detect(CPH, settings)` in the host block finds StreamUP's Currency Core or Tawmae's
  LOYALTY POINTS by their settings global and gives the user variable and the name of the points, the two
  settings `pointsVariable` and `pointsName` as the fallback; the describe answer carries a detected system
  (`currency`), the window shows it as a header pill and says on the two settings that they are not in use. The
  demo pays a win into the viewer's points through it. Before, every game carried the same detection of its own.
- `examples/DemoGame/DemoGame.sb`: the demo as one import (both actions, the `!roll` command with its cooldowns),
  also as an import string to paste in the demo's README. `tools/build-import.ps1` keeps an export's Execute Code
  sub-actions on the files named for them, its meta version on the game's `CurrentVersion` and the README's
  string on the export; `check-stub.ps1` fails when either is behind.
- A command in `Cooldowns(C(…))` is named by its id, as the hosts do; the generic stub also takes its text
  (`!roll`) or its name for a command made by hand, looked up in Streamer.bot, and warns when there is none. The
  demo game declares its two cooldowns; before, its `globalCooldown` was saved but never reached the command.
- The settings action tells the game which global to answer into (`uiframework.var`), so the game's
  `ActionName` constant and the action's name in Streamer.bot cannot drift apart; the host block logs a line when
  it has described its settings, the stub says when no action of that name exists, and its dialog for a game that
  did not answer says where to look.
- The stubs (`stub/Stub.cs`, `stub/Stub.spec.cs`) compile without `PresentationFramework`, `PresentationCore`
  and `WindowsBase` in the action's references: the first-run and update dialogs are Win32 message boxes
  (`user32.dll`) instead of WPF ones. A fresh action with the generic stub pasted in compiled with
  `MessageBoxButton does not exist in the namespace System.Windows` before.

## [0.9.0] - 2026-11-04

The framework as a kit: a project binds it with two files and no tooling. Hosts that still carry a spec in
their settings action run unchanged.

- The game declares its window beside its settings: `UiSchema.Window`, built with `UiWindow` and the
  shorthands `P`, `H`, `Cmd`, `Note`, `Link`, `C` in the host block, goes into the describe answer as `window`.
  A stub that brings no spec takes the window from there; a spec it brings still wins. New there: `log.files`
  (the log files' pattern) and `cooldowns` (commands whose cooldowns follow settings).
- `stub/Stub.cs` is generic: one file for every project, `GameActionName` its only line to fill in. The settings
  file, the log and the cooldowns come from the schema; a Try button, the check before a save, the work after it,
  a table's rows and the emote sources go back to the game action as `uiframework.request` (`test`, `validate`,
  `saved`, `table`, `emotes`), which `UiHost.Handle` in the host block answers. The spec-carrying template
  moved to `stub/Stub.spec.cs`; `tools/embed-spec.ps1` reads it from there.
- What the schema gets wrong (an unknown kind, a requirement on a key that is not a setting, a page naming a
  pattern no setting matches, a minimum above a maximum, a pill or override on a missing key) is shown as a
  banner over the window and listed on the About page; a settings action that cannot open its window says why in
  a dialog instead of a log line.
- The Log page follows its folder: a round played in chat shows up without the reload button (which stays). The
  folder is watched for the files the window declares; a folder that is not there yet is looked for.
- A page's `icon` given as a conventions name (`dice`, `cards`) is turned into its glyph; it was shown as the
  word.
- `tools/update-host-block.ps1` refreshes the host block in a game's file and keeps its Window and Table;
  `tools/check-stub.ps1` compiles the generic stub, the spec stub and `examples/DemoGame`, and writes the demo's
  schema for the preview (`-schema` alone now opens a window declared in the schema).
- Docs in English: `docs/Schema.md` (settings, kinds, answers, the window, the game's side; `docs/Spec.md` went
  into it), `docs/Loader.md`, `docs/Customize.md`, and `docs/Getting-Started.md` with `examples/DemoGame`.
- 49 tests; the host block compiles inside the test project against a stand-in for Streamer.bot's CPH.

## [0.8.1] - 2026-11-03

- The captions of a `slider` sit under its stops; they were spread evenly and missed the thumb.
- A field that a `set` answer changes glows in the accent colour for a moment, so the eye finds what a preset
  stop moved.
- Export and import moved from the footer into the customise mode's bar, next to *Open file*. Import there
  replaces the saved settings at once (the file as it was is kept as `.bak`) and rebuilds the window on them.

## [0.8.0] - 2026-11-03

- Export and import in the footer: export writes every setting as it stands in the window to a file of the
  user's choosing (`{ "uiframework": "settings", "host", "version", "exported", "settings" }`); import reads such a
  file, or a settings file itself, into the window as if typed, old keys through the aliases, and Save is what
  keeps it. A file from another host asks first; what the window does not know is skipped.
- Tooltips have a fixed width and wrap instead of running across the screen.
- `row` on a setting: settings naming the same row, one after the other, share one line of the page whatever
  their kind. Classic Chat Gamble's bet and role to try sit side by side; Slot Machine's platform, bet and role too.
- `slider` kind: a slider over `options` (the stored value is the stop's value, the stops captioned under the
  track) or over `min` to `max` in `step`.
- `live` on a field that is not an action: the host is asked whenever the field changes, and `set` in its answer
  fills in other fields. Classic Chat Gamble's fairness slider is back on it: a stop fills in the five numbers,
  and the odds move the slider to the preset the numbers match, or to Custom.
- A line `{ "emotes": [...], "platform": "twitch" }` in an action's answer shows the emotes as pictures with their
  names beneath. Slot Machine's test spin shows the slots that way.

## [0.7.0] - 2026-11-03

- An old key two settings share (one switch that became two, Battleship's `OverlayShowRoster`) gives both its
  value on the move; before, only the first took it.
- http(s) addresses in a `note` are links.
- `twitchOnly` on a `bool`: the switch carries the "Twitch only" pill that the sender switches have.
- `answers` in the spec: what an action would answer, for the preview and its screenshots, so an `auto` panel
  shows the real thing instead of a placeholder.
- Ctrl+0 opens the tenth page.

## [0.6.0] - 2026-11-03

- `note` kind: a paragraph of text on a page, with no value. Battleship's pages carry their explanations with it.
- An action's answer may carry `set`, values for other fields of the window, put in as if typed and saved only
  with Save, and `status`, a line for the footer. Battleship's fleet presets fill in the board and the ships
  that way.
- Battleship Game v2.0.0 runs on the framework (`hosts/battleship`).

## [0.5.0] - 2026-11-03

- `requires` takes a condition on any field, not only a switch: `numberOfSlots>=5`, `cpBetMode=fixed`,
  `mode!=free`. The field is greyed out while it does not hold and follows the gate live.
- A host can check the settings before a save: `Validate(settingsJson)` returns the problems, one per line, and
  the window asks whether to save anyway. Slot Machine names a minimum above the maximum, Free mode off without a
  points variable, the jackpot pool on with a bet of 0 to win it, and a smallest weight above the largest.
- An action's panel draws an answer given as JSON: `lines` (a line can be a big headline figure) and `bar`
  (segments with label, value, text and colour, drawn proportionally with a legend). Slot Machine's odds are a
  bar of jackpot, win and loss again, with the return of a bet of 100 as the headline.
- The `emote` field kind is rendered: a text box with the emote's picture beside it, `platform` saying where
  the picture comes from, like an `emote` column.

## [0.4.1] - 2026-11-03

- `emote` columns in a `list`: the picture of the emote beside its box. Kick from the `[emote:ID:name]` code, Twitch
  by name from the channel's and the global Twitch, 7TV, BTTV and FFZ emotes (the host hands over the broadcaster's
  id, client id and token in `EmoteSources()`; without it only the global extension sets are asked), YouTube emoji
  show themselves. Slot Machine's emote lists use them, as the old window did.

## [0.4.0] - 2026-11-03

- `action` settings can be `live` (the host gets the values as they stand in the window, unsaved ones included),
  `panel` (the answer is shown in a panel under the button instead of the footer) and `auto` (no button: the host
  is asked when the page is shown and a moment after every change in the window). Slot Machine's odds and its test
  spin are built on them.
- `transient` settings: shown in the window, never saved, never counted as a change; a live snapshot carries them.
  The bet, role and platform of a test spin are such fields.
- An alias with a dot (`PlatformEmotes.twitch`) reaches into an object of the old settings file and moves the value
  out; the object is dropped once it is empty. `UiSettings` in the host block reads such aliases too.
- Slot Machine Game v16.0.0 runs on the framework (`hosts/slotmachine`).

## [0.3.4] - 2026-11-02

- Customise mode: a field let go over another field takes that field's place, after it when it came from above
  and before it when it came from below, wherever on the field the mouse is. Before, only the lower half of the
  field below counted, and that half often sat in the auto-scroll zone at the bottom edge, where the page slid
  away under the mouse; a section of two fields could hardly be turned around. The auto-scroll zone is smaller
  and slower.

## [0.3.3] - 2026-11-01

- Customise mode: a dragged field can be let go anywhere on the page. Over the upper half of a field it lands
  before that field, over the lower half after it, over a heading or below the last field at that spot; the page
  scrolls while the drag is near its top or bottom edge.
- Customise mode: the accent colour was only written on leaving its text box, which never had the focus after the
  picker, so it fell back to empty on the next change. It is now taken once the picker closes, when typing pauses,
  on Enter and on leaving the box. Its help names where the colour shows (primary buttons, switches that are on,
  the active page in the rail, links, the customise bar).

## [0.3.2] - 2026-11-01

- Customise mode: the window no longer closes and reopens after a change. Its content is rebuilt in place on the
  same page and scroll position; the same goes for the theme switch and the reset to defaults.
- Customise mode: the Messages page takes part. Templates are listed with grip and menu and can be reordered,
  renamed, hidden and reset; they stay on their page.
- Header pill: only a framework release the user answered "No" to in the loader's dialog shows as
  `UIFramework x.y.z available` (click opens the releases). The "installed" and "ready" pills are gone, and the
  stub writes `frameworkAvailable` on "No" alone.

## [0.3.1] - 2026-11-01

- Header pill for the framework's own version next to the host's pills.
- Loader: `latest.json` is read through the GitHub contents API, with the raw URL as fallback, because the raw
  CDN caches for five minutes and ignores query strings. Every release check is logged with its outcome.

## [0.3.0] - 2026-11-01

- `list` kind: a row editor for a list of rows with typed columns (`columns`), with Add row, Restore defaults and
  a row count.
- Sidecar files: a setting with `file` (and optionally `field`) is read from and written to that file next to
  the settings file and never lands in the settings file itself, so a host's word bank or question list stays in
  the file its game already reads.
- RhymeTime v2.0.0 runs on the framework.

## [0.2.0] - 2026-11-01

- Loader in the stub: downloads the DLL on first use, offers newer releases (Yes / No / Cancel skips the version),
  `autoUpdate` and `skipVersion` in the layout file.
- Customise mode: rename, move and hide pages and fields; `uiframework.layout.json`; accent colour and language.
- Blackjack v17.0.0 runs on the framework.
