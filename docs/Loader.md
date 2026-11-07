# Loader

Every settings action (the stub) carries the loader. It looks for `UIFramework.dll` under
`<Streamer.bot>\UIFramework\lib\<version>\` and takes the highest version. When that one cannot be loaded (a
damaged file, a build the stub does not fit), it tries the next older one, and only then gives up with a dialog;
the log names every DLL it tried and why it failed. A DLL saved from a browser, which Windows marks as downloaded
from the internet, loads like any other.

**First open without a DLL:** a dialog asks
- **Yes**: read `lib/v1/latest.json` from this repository (version, URL, SHA-256), download the DLL from the
  release, check the hash, put it under `lib\<version>\UIFramework.dll`, open the window.
- **No**: the folder `UIFramework\lib` is created and opened in Explorer, with a note to put the file into a
  version folder underneath (the releases link is in the dialog).
- **Cancel**: nothing happens; the action logs a warning.

**Every later open of the window:** with a DLL in place and `autoUpdate` in `uiframework.layout.json` not
`false`, the stub reads `latest.json` (5 s timeout). When there is a newer version, a dialog asks: **Yes**
downloads it into its own folder; when the old DLL has not been loaded in this run of Streamer.bot yet, the window
opens with the new one at once, otherwise from the next start. **No** asks again next time. **Cancel** writes
`skipVersion` into the layout file: that version is not offered again, the next one is. A running DLL is never
overwritten (it is locked, and .NET Framework does not unload).

**The pill in the window's header:** whoever answers *No* in the update dialog sees the pill
`UIFramework 0.3.2 available` in the window (a click opens the releases) until the version is taken or skipped
with *Cancel*. It rests on `frameworkAvailable` in `uiframework.layout.json`; *Yes* and *Cancel* clear the note.

**Where `latest.json` comes from:** first through the GitHub API (`contents/lib/v1/latest.json`, uncached, 60
calls per hour and IP); when that fails, through raw.githubusercontent.com, whose CDN serves the file up to five
minutes old. Every check writes a line into Streamer.bot's log: the installed version, what `latest.json` says,
whether it asked, skipped or is turned off.

**Safety:** HTTPS, SHA-256 from `latest.json`, a global mutex so that several hosts do not download at once. The
major line is in the path (`lib/v1/`): an incompatible v2 never lands in a v1 host. The hash is kept beside the
DLL (`UIFramework.dll.sha256`) and checked on every load, so a file damaged or swapped since is not used. A release
whose `latest.json` names a `minHostStub` above the stub's own version (`StubVersion`, 0.2.0 now) is not offered to
that stub; the game has to ship the newer settings action first.

**Answers do not linger:** the global variable a game's answer comes back in is unset as soon as the stub has
read it, so an answer that carries a secret (the emote sources hold the Twitch token) is not left readable by
other actions or visible in Streamer.bot's variables.

**References:** none to add. The dialogs are Win32 message boxes (`user32.dll` through P/Invoke), the download
runs through `System.Net.WebClient` from `System.dll`; both come with the references every action has.

**Release:** a tag `vX.Y.Z` (the `<Version>` in `UIFramework.csproj` must match) makes
`.github/workflows/release.yml` build, test, create the release with `UIFramework.dll` and write
`lib/v1/latest.json` on `main`.
