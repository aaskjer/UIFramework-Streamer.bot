# Changelog

The release workflow puts the section for the tagged version into the GitHub release, so every version needs
its section here before the tag is pushed. Everything before 1.0.0 is published as a pre-release.

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
