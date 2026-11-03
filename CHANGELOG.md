# Changelog

The release workflow puts the section for the tagged version into the GitHub release, so every version needs
its section here before the tag is pushed. Everything before 1.0.0 is published as a pre-release.

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
