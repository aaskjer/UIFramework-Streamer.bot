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

**Every later open of the window:** with a DLL in place and `autoUpdate` not `false` (see *What holds for every
game* below), the stub reads `latest.json` (5 s timeout; offline, the window opens after that one try). When there is a newer version, a dialog asks: **Yes**
downloads it into its own folder; when nothing has loaded UIFramework in this run of Streamer.bot yet, the window
opens with the new one at once, otherwise from the next start (every game uses the one that is running, so two
versions never run side by side, and the log names the one in use). A game loads it without its window too: for
the update notice at the start (`UiUpdateNotice.Startup` in `Init()`) and for `UiLive`. With such a game imported,
"from the next start" is the usual case. The dialog says which of the two it is: that Streamer.bot needs no
restart, or that the new version takes effect once Streamer.bot is restarted and which version runs until then.
After such a download a second note says it again (restart Streamer.bot; no hurry, everything goes on with the
running version, so it can wait until after the stream), because the window then opens as before and nothing else
shows that an update is waiting (stub 0.3.1; 0.3.0 said "used from the next start" in the dialog only). **No** skips that version: it is not offered
again, to any game, and the next one is. **Cancel**, the dialog's X and Esc mean "not now": the next open asks
again. (A stub before 0.3.0 had it the other way round, so closing the dialog skipped the version for good.) A
running DLL is never overwritten (it is locked, and .NET Framework does not unload).

**A download that GitHub does not deliver** (a 5xx, a 429, a connection that breaks or times out; a fresh
release's file answered 504 once and loaded seconds later) is tried three times, a few seconds apart. What is left
after the last try reads "GitHub did not deliver the file after 3 tries ... Try again in a moment."

**The pill in the window's header:** whoever answers "not now" in the update dialog sees the pill
`UIFramework 0.3.2 available` in the window (a click opens the releases) until the version is taken or skipped.
It rests on `frameworkAvailable` in `uiframework.layout.json`; *Yes* and *No* clear the note.

**What holds for every game:** the one DLL is every game's, so what is decided about it is kept once, beside it,
in `<Streamer.bot>\UIFramework\loader.json`: `"skipVersion": "1.9.0"` (written by *No* in the update dialog) and
`"autoUpdate": false` (written by hand: no release check in any game). A stub before 0.3.0 kept both in each game's
`uiframework.layout.json`, where they held for that game alone; they are still read there, so a check switched off
or a version skipped in one game's layout file stays so for that game.

**Where `latest.json` comes from:** first through the GitHub API (`contents/lib/v1/latest.json`, uncached, 60
calls per hour and IP); when the API refuses (its rate limit) or answers with something that is no `latest.json` (a
network's sign-in page), through raw.githubusercontent.com, whose CDN serves the file up to five minutes old. When
the network is unreachable or the API timed out, the CDN is not tried: it would only keep the window waiting. Every check writes a line into Streamer.bot's log: the installed version, what `latest.json` says,
whether it asked, skipped or is turned off.

**Safety:** HTTPS (TLS 1.2 is added only where the process pinned older protocols; Windows' own choice stays otherwise),
SHA-256 from `latest.json`, a global mutex so that several hosts do not download at once (one still waiting after
a minute gives up with a message instead of downloading beside it). The
major line is in the path (`lib/v1/`): an incompatible v2 never lands in a v1 host, because the release workflow
writes 2.0.0 and what follows it to `lib/v2/latest.json`, which only a stub made for that line reads (its two
`latest.json` addresses name `lib/v2/`). The DLL's own update check reads the file of its own major line too. The hash is kept beside the
DLL (`UIFramework.dll.sha256`, written before the DLL is moved in) and checked on every load: a file damaged
since is set aside as `UIFramework.dll.broken`, an older one opens the window, and the next open offers that
version again. A game's host block checks the same hash before it loads the DLL for the update notice or the live
check (host block of 1.8.1 and on; an older one loaded it unchecked, and the settings action then used what was
loaded); it leaves a damaged file where it is, for the settings action to set aside. This catches damage, not someone who can write to the Streamer.bot folder: a DLL put in by hand
has no hash file and is loaded as it is. A release
whose `latest.json` names a `minHostStub` above the stub's own version (`StubVersion`, 0.3.1 now) is not offered to
that stub; the game has to ship the newer settings action first. The same holds for a DLL that is there already,
installed by another game: a DLL names the stub it needs in its product version (`1.8.4+stub.0.1.0`, from
`MinHostStub` in `UIFramework.csproj`), a stub from 0.2.1 on reads that without loading the file and passes such a
DLL over for an older one that fits, and the DLL itself (1.8.4 and on) opens no window for an older stub but says
which one it needs. That also covers a stub before 0.2.1 and a DLL another game has loaded already. Nothing from `latest.json` is taken unchecked: a
release without a `sha256` is not installed, a `url` that is not `https://` is not fetched, and a `version` that is
not two to four numbers with dots (`1.2.0`, a leading `v` allowed) never becomes a folder name.

**One window per game:** a second run of the settings action while its window is open (or still opening) brings
that window to the front and does nothing else: the game is not asked again, the release check does not run again,
and the first run keeps serving the window. A run while the game's answer or a dialog of the first one is still
pending only logs that.

**Answers do not linger:** the global variable a game's answer comes back in is unset as soon as the stub has
read it, also when the game's run failed, so an answer that carries a secret (the emote sources hold the Twitch
token) is not left readable by other actions or visible in Streamer.bot's variables. From stub 0.3.0 on every
request has a variable of its own (`uiframework.answer.<action>.<id>`, named to the game in `uiframework.var`): a
request that hung and answers late writes into its own, not into the one the next request reads. A host block
from before the variable was named answers into the old one, which is still read.

**When the game does not answer:** each request to the game action waits five seconds to start. One the window
stopped waiting for is skipped when Streamer.bot's thread gets to it, not run late. One that has started gets 30
seconds more; a handler that runs longer goes on, but the window stops waiting and carries on as with no answer.
The window does not stand still for it (UIFramework 1.9.0 and on, with any stub): the DLL asks the game from another
thread. A Try button's answer and a table's rows arrive when they arrive, the button greyed and the table saying
that it is asking; the check before a save waits for its answer with the window alive (it paints and moves, and
takes no input: what the answer decides must not be overtaken). Before, the window's own thread waited, and Windows
called a window "not responding" whose game took its time. No answer is not taken for an answer: a check before
saving that got none (a timeout, a disabled action) is not "no problems", the window asks whether to save
unchecked; a Try button says that the game did not answer, in the colour of a failure, instead of "done", and a
table says so instead of showing as empty (stub 0.3.0 and on; an older stub cannot tell the two apart). A window
whose request hangs can be closed and opened again, and when the game has not sent its schema for a minute, the
next run opens the window instead of waiting for it.

**One prefix in the log:** the lines of a game's settings start with the prefix its window declares
(`UiWindow(..., logPrefix:)`), else with `[<game action's name>/Settings]`, from the first line ("the window cannot
open") to the DLL's own; the game action's lines start with `[<its name>]`. Before stub 0.3.0 the stub changed to
the window's title once the game had described itself, so one game wrote under three names.

**Actions a game runs from its handlers see the request:** the stub hands each request to the game as arguments
(`uiframework.request`, `uiframework.name`, `uiframework.values` with the settings as JSON, `uiframework.var`), and
Streamer.bot passes a running action's arguments on to the actions it runs. An action the game runs with
`CPH.RunAction` from `test`, `validate` or `saved` therefore sees them too: the settings are readable there, and
when that action is a UIFramework game itself, its `UiHost.Handle` took them for a request to it and the action
did nothing else. Since UIFramework 1.2.0 the host block clears them (`CPH.SetArgument(key, "")`) once it has read
them, before it calls the game's handler. With an older host block, clear the request yourself before the
`CPH.RunAction`: `CPH.SetArgument("uiframework.request", "");`.

**Cooldowns:** on save, each command's cooldowns are set on their own; a key the window names for a cooldown
that the settings do not hold as a number leaves that cooldown as it is (with a warning in the log) instead of
setting it to 0, and the game hears of the save whatever happened.

**The layout file** (`uiframework.layout.json`): the stub's note in it (`frameworkAvailable`)
is written only when it changes, through a file beside it that is swapped in, and never over a file it could
not read. The DLL keeps a layout file that is no JSON as `uiframework.layout.json.broken` and logs why; one another
program holds is read again a few times and otherwise left as it is (the window starts from the defaults once).

**References:** none to add. The dialogs are Win32 message boxes (`user32.dll` through P/Invoke), the download
runs through `System.Net.WebClient` from `System.dll`; both come with the references every action has.

**Release:** a tag `vX.Y.Z` (the `<Version>` in `UIFramework.csproj` must match) makes
`.github/workflows/release.yml` build the tag's own commit (which must be on `main`), test, create the release with
`UIFramework.dll` and write `lib/v<major>/latest.json` on `main` (`lib/v1/` for 1.x.y, `lib/v2/` from 2.0.0 on; the
first release of a line makes its folder). `minHostStub` is `MinHostStub` from the
tag's `UIFramework.csproj` (raise it there when a release needs a newer stub; the DLL then carries it too), and an
older tag released late never takes `latest.json` back.

**A new major line (2.0.0):** a major version is for what breaks a game as it is (a builder call removed, a schema
key no longer read). Nothing in a release is special about it except the stub: `FrameworkLine` in `stub/Stub.cs`
becomes `"v2"`, the one word both `latest.json` addresses are built from, and `<Version>` in `UIFramework.csproj`
becomes `2.0.0`, with `MinHostStub` raised to that stub's version when the DLL needs it. A test
(`TheStubsLineIsTheFrameworksMajorVersion`) fails as long as the two disagree, so neither is forgotten. The
workflow writes `lib/v2/latest.json` by itself, the DLL reads the file of its own line by itself, and a game on
the v1 stub goes on reading `lib/v1/latest.json`: it keeps the last 1.x until it ships the new stub. What 2.0.0 is
to take away is planned outside this repository.
