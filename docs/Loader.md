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
`false`, the stub reads `latest.json` (5 s timeout; offline, the window opens after that one try). When there is a newer version, a dialog asks: **Yes**
downloads it into its own folder; when the old DLL has not been loaded in this run of Streamer.bot yet, the window
opens with the new one at once, otherwise from the next start. **No** asks again next time. **Cancel** writes
`skipVersion` into the layout file: that version is not offered again, the next one is. A running DLL is never
overwritten (it is locked, and .NET Framework does not unload).

**The pill in the window's header:** whoever answers *No* in the update dialog sees the pill
`UIFramework 0.3.2 available` in the window (a click opens the releases) until the version is taken or skipped
with *Cancel*. It rests on `frameworkAvailable` in `uiframework.layout.json`; *Yes* and *Cancel* clear the note.

**Where `latest.json` comes from:** first through the GitHub API (`contents/lib/v1/latest.json`, uncached, 60
calls per hour and IP); when the API refuses (its rate limit) or answers with something that is no `latest.json` (a
network's sign-in page), through raw.githubusercontent.com, whose CDN serves the file up to five minutes old. When
the network is unreachable or the API timed out, the CDN is not tried: it would only keep the window waiting. Every check writes a line into Streamer.bot's log: the installed version, what `latest.json` says,
whether it asked, skipped or is turned off.

**Safety:** HTTPS (TLS 1.2 is added only where the process pinned older protocols; Windows' own choice stays otherwise),
SHA-256 from `latest.json`, a global mutex so that several hosts do not download at once (one still waiting after
a minute gives up with a message instead of downloading beside it). The
major line is in the path (`lib/v1/`): an incompatible v2 never lands in a v1 host. The hash is kept beside the
DLL (`UIFramework.dll.sha256`) and checked on every load, so a file damaged or swapped since is not used. A release
whose `latest.json` names a `minHostStub` above the stub's own version (`StubVersion`, 0.2.0 now) is not offered to
that stub; the game has to ship the newer settings action first. Nothing from `latest.json` is taken unchecked: a
release without a `sha256` is not installed, a `url` that is not `https://` is not fetched, and a `version` that is
not two to four numbers with dots (`1.2.0`, a leading `v` allowed) never becomes a folder name.

**One window per game:** a second run of the settings action while its window is open (or still opening) brings
that window to the front and does nothing else: the game is not asked again, the release check does not run again,
and the first run keeps serving the window. A run while the game's answer or a dialog of the first one is still
pending only logs that.

**Answers do not linger:** the global variable a game's answer comes back in is unset as soon as the stub has
read it, so an answer that carries a secret (the emote sources hold the Twitch token) is not left readable by
other actions or visible in Streamer.bot's variables.

**When the game does not answer:** each request to the game action waits five seconds to start. One the window
stopped waiting for is skipped when Streamer.bot's thread gets to it, not run late. One that has started gets 30
seconds more; a handler that runs longer goes on, but the window stops waiting and carries on as with no answer,
so it never freezes. A check before saving that got no answer (a timeout, a disabled action) is not taken for "no
problems": the window asks whether to save unchecked. A window whose request hangs can be closed and opened again,
and when the game has not sent its schema for a minute, the next run opens the window instead of waiting for it.

**Actions a game runs from its handlers see the request:** the stub hands each request to the game as arguments
(`uiframework.request`, `uiframework.name`, `uiframework.values` with the settings as JSON, `uiframework.var`), and
Streamer.bot passes a running action's arguments on to the actions it runs. An action the game runs with
`CPH.RunAction` from `test`, `validate` or `saved` therefore sees them too: the settings are readable there, and
when that action is a UIFramework game itself, its `UiHost.Handle` takes them for a request to it and the action
does nothing else. Clear the request first: `CPH.SetArgument("uiframework.request", "");` before the
`CPH.RunAction`.

**Cooldowns:** on save, each command's cooldowns are set on their own; a key the window names for a cooldown
that the settings do not hold as a number leaves that cooldown as it is (with a warning in the log) instead of
setting it to 0, and the game hears of the save whatever happened.

**The layout file** (`uiframework.layout.json`): the stub's notes in it (`skipVersion`, `frameworkAvailable`)
are written only when they change, through a file beside it that is swapped in, and never over a file it could
not read. The DLL keeps a layout file it cannot read as `uiframework.layout.json.broken` and logs why.

**References:** none to add. The dialogs are Win32 message boxes (`user32.dll` through P/Invoke), the download
runs through `System.Net.WebClient` from `System.dll`; both come with the references every action has.

**Release:** a tag `vX.Y.Z` (the `<Version>` in `UIFramework.csproj` must match) makes
`.github/workflows/release.yml` build the tag's own commit (which must be on `main`), test, create the release with
`UIFramework.dll` and write `lib/v1/latest.json` on `main`. `minHostStub` is carried over from main's
`latest.json` (raise it there when a release needs a newer stub), and an older tag released late never takes
`latest.json` back.
