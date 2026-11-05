# Changelog

The release workflow puts the section for the tagged version into the GitHub release, so every version needs
its section here before the tag is pushed. Everything before 1.0.0 is published as a pre-release.

## [Unreleased]

- Battleship Game is the sixth and last host on the generic stub (v2.1.0, in German, 162 of its keys translated in
  the block): the summaries, the presets, the broadcaster lookup, the leaderboard reset, the OBS setup and the
  Heat markers are answered by the game, a save hands the Click to Fire settings to a live overlay through
  `saved:`. `hosts/battleship` lost its `spec.json` and `host-section.cs`, and with the last spec-carrying host
  gone `stub/Stub.spec.cs` and `tools/embed-spec.ps1` are removed; `tools/check-stub.ps1` compiles the one stub
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
