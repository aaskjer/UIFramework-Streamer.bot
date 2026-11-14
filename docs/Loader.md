# Loader

Every settings action (the stub) carries the loader. It looks for `UIFramework.dll` under
`<Streamer.bot>\UIFramework\lib\<version>\` and takes the highest version. When that one cannot be loaded (a
damaged file, a build the stub does not fit), it tries the next older one and gives up with a dialog only after the
last; the log names every DLL it tried and why it failed. A DLL saved from a browser, which Windows marks as
downloaded from the internet, loads like any other.

**First open without a DLL:** a dialog asks
- **Yes**: read `lib/v1/latest.json` from this repository (version, URL, SHA-256), download the DLL from the
  release, check the hash, put it under `lib\<version>\UIFramework.dll`, open the window.
- **No**: the folder `UIFramework\lib` is created and opened in Explorer, with a note to put the file into a
  version folder underneath (the releases link is in the dialog).
- **Cancel**: nothing happens; the action logs a warning.

The same dialog comes, saying so, when every DLL installed needs a newer settings action than this one. A download
that fails shows why, with the releases link, and opens the `lib` folder. *Yes* ends that way too when the newest
release needs a newer settings action: nothing is installed, and the message says to update the project first or
to put an older `UIFramework.dll` into its version's folder.

**Every later open of the window:** with a DLL in place and `autoUpdate` not `false` (see *What holds for every
project* below), the stub reads `latest.json`, with a 5 s timeout; offline, the window opens after that one try.
When there is a newer version, a dialog asks. **Yes** downloads it into its own folder. If nothing has loaded
UIFramework in this run of Streamer.bot yet, the window opens with the new version at once, otherwise from the next
start: every project uses the one that is running, so two versions never run side by side, and the log names the
one in use. A project's action loads the DLL without its window too, for the update notice at the start
(`UiUpdateNotice.Startup` in `Init()`) and for `UiLive`, so with such a project imported "from the next start" is
the usual case. The dialog says which of the two it is: that Streamer.bot needs no restart, or that the new version
takes effect once Streamer.bot is restarted and which version runs until then. After such a download a second note
says it again (restart Streamer.bot; no hurry, everything goes on with the running version, so it can wait until
after the stream), since the window then opens as before and nothing else shows that an update is waiting (stub
0.3.1; 0.3.0 said "used from the next start" in the dialog only). **No** skips that version: no project is offered
it again, the update notice at the start does not name it (UIFramework 1.10.0 and on), and the next one is offered
as usual. **Cancel**, the dialog's X and Esc mean "not now": the next open asks again. (A stub before 0.3.0 had it
the other way round, so closing the dialog skipped the version for good.) A running DLL is never overwritten: it is
locked, and .NET Framework does not unload it.

**A download that GitHub does not deliver** (a 5xx, a 429, a connection that fails, breaks or times out, a file
that does not match its hash, as one cut short does; a fresh release's file once answered 504 and loaded seconds
later) is tried three times, a few seconds apart. A try gives up when GitHub sends nothing for a minute. What is
left after the last try reads "GitHub did not deliver the file after 3 tries ... Try again in a moment.", or, for a
file that still does not match, that it does not match the published hash.

**The pill in the window's header:** a user who answers "not now" in the update dialog, or whose download after
*Yes* failed, sees the pill `UIFramework <version> available` in the window (a click opens the releases) until the
version is taken or skipped. It rests on `frameworkAvailable` in `uiframework.layout.json`; a *Yes* whose download
succeeds and a *No* clear the note.

**What holds for every project:** all projects share the one DLL, so what is decided about it is kept once, beside
it, in `<Streamer.bot>\UIFramework\loader.json`: `"skipVersion": "1.9.0"` (written by *No* in the update dialog)
and `"autoUpdate": false` (written by hand: no release check in any project, and from UIFramework 1.10.0 the update
notice at the start leaves UIFramework's releases out). A stub before 0.3.0 kept both in each project's
`uiframework.layout.json`, where they held for that project alone. They are still read there, so a check switched
off or a version skipped in one project's layout file stays so for that project; the update notice then leaves
UIFramework out only when every project of the run says so. A `loader.json` that cannot be read counts as empty and
is not written over (stub 0.3.2; the log says so): *No* then keeps the skip in the project's layout file.

**Where `latest.json` comes from:** first through the GitHub API (`contents/lib/v1/latest.json`, uncached, 60
calls per hour and IP). When the API refuses (its rate limit) or answers with something that is no `latest.json`
(a network's sign-in page), the stub falls back to raw.githubusercontent.com, whose CDN serves the file up to five
minutes old. When the network is unreachable or the API timed out, the CDN is not tried: it would only keep the
window waiting. Every check writes a line into Streamer.bot's log: the installed version, what `latest.json` says,
whether it asked, skipped or is turned off.

**Safety:** HTTPS (TLS 1.2 is added only where the process pinned older protocols; Windows' own choice stays
otherwise), SHA-256 from `latest.json`, and a global mutex so that several hosts do not download at once (one still
waiting after a minute gives up with a message instead of downloading beside it). The major line is in the path of
`latest.json` (`lib/v1/`): the release workflow writes 2.0.0 and what follows it to `lib/v2/latest.json`, which
only a stub made for that line reads (its two `latest.json` addresses name `lib/v2/`), so a v1 stub is never
offered a 2.x. The DLL's own update check reads the file of its own major line too. The DLLs themselves are not
kept apart by line: every version goes under `lib\<version>\`, and a stub takes the highest one there that it fits.
A 2.x that a project with the v2 stub has installed is kept from a v1 settings action by the settings action it
names (`MinHostStub`, below).

The hash is kept beside the DLL (`UIFramework.dll.sha256`, written before the DLL is moved in) and checked on every
load. A file damaged since is set aside as `UIFramework.dll.broken`, an older one opens the window, and the next
open offers that version again. From stub 0.3.2 that holds also while another UIFramework runs already (loaded, for
instance, by a host block as Streamer.bot started); the window then opens with the running one. A project's host
block checks the same hash before it loads the DLL for the update notice or the live check (host block of 1.8.1 and
on; an older one loaded it unchecked, and the settings action then used what was loaded). It leaves a damaged file
where it is, for the settings action to set aside, and from 1.10.0 goes on to the next older DLL. It also leaves
out a DLL that needs a newer settings action than the one released with it (`StubVersion` in the host block, kept
equal to the stub's): only one UIFramework runs in a Streamer.bot, and once the notice had loaded such a DLL, the
project's own window could not open until a restart (an older host block took the newest DLL). This catches damage,
not someone who can write to the Streamer.bot folder: a DLL put in by hand has no hash file and is loaded as it is.

A release whose `latest.json` names a `minHostStub` above the stub's own version (`StubVersion`, 0.3.2 now) is not
offered to that stub; the project has to ship the newer settings action first. The same holds for a DLL that is
there already, installed by another project. A DLL names the stub it needs in its product version
(`1.8.4+stub.0.1.0`, from `MinHostStub` in `UIFramework.csproj`); a stub from 0.2.1 on reads that without loading
the file and passes such a DLL over for an older one that fits, and the DLL itself (1.8.4 and on) opens no window
for an older stub but says which one it needs. That also covers a stub before 0.2.1 and a DLL another project has
loaded already. A stub from 0.3.2 on does not take such a DLL that is running already either (a host block of
1.9.0 or before loads the newest DLL as Streamer.bot starts, whichever stub it needs): its dialog names the running
version and the stub it needs, and says that updating the project opens the window.

Nothing from `latest.json` is taken unchecked: a release without a `sha256` is not installed, a `url` that is not
`https://` is not fetched, and a `version` that is not two to four numbers with dots (`1.2.0`, a leading `v`
allowed) never becomes a folder name.

**One window per project:** a second run of the settings action while its window is open brings that window to the
front and does nothing else: the project is not asked again, the release check does not run again, and the first
run keeps serving the window. A run while the project's answer or a dialog of the first run is still pending only
logs that.

**Answers do not linger:** the global variable a project's answer comes back in is unset as soon as the stub has
read it, also when the run failed. An answer that carries a secret (the emote sources hold the Twitch token) is
therefore neither left readable by other actions nor visible in Streamer.bot's variables. From stub 0.3.0 on every
request has a variable of its own (`uiframework.answer.<action>.<id>`, passed to the project's action in
`uiframework.var`): a request that hung and answers late writes into its own, not into the one the next request
reads. A host block from before the variable was passed answers into the old one, which is still read.

**When the project does not answer:** each request to the project's action gets five seconds to start. One the
window stopped waiting for is skipped when Streamer.bot's thread gets to it, not run late. One that has started
gets 30 seconds more; a handler that runs longer goes on, but the window stops waiting and carries on as with no
answer. A handler that takes long on purpose (an import) calls `UiHost.StillWorking()` every few seconds, and the
stub waits 30 seconds from each call (stub 0.3.2 and on; with an older one the call does nothing). The window does
not stand still meanwhile (UIFramework 1.9.0 and on, with any stub): the DLL asks the project from another thread.
The answers to a Try button and to a trigger's Fire button on the Help page, the emote sources of emote fields and
a table's rows arrive when they arrive (the Fire button and the emote sources from 1.10.0), the button greyed and
the table saying that it is asking. The check before a save waits for its answer with the window alive: it paints
and moves but takes no input, since what the answer decides must not be overtaken. A value that reaches a field
meanwhile stays unsaved, and the questions that follow are modal to the window. Before 1.9.0 the window's own
thread waited, and Windows called a window "not responding" whose project took its time.

No answer is not taken for an answer. A check before saving that got none (a timeout, a disabled action, an action
that ran without answering) does not count as "no problems": the window asks whether to save unchecked. A Try
button says that the project did not answer, in the colour of a failure, instead of "done" (a Fire button on the
Help page, from 1.10.0, gives the settings action's reason where it has one instead of the hint at an older
version), and a table says so instead of showing as empty (stub 0.3.0 and on; an older stub cannot tell the two
apart). A window whose request hangs can be closed and opened again, and when the project has not sent its schema
for a minute, the next run opens the window instead of waiting for it; should the project answer after all, the
first run ends there.

**One prefix in the log:** the lines of a project's settings start with the prefix its window declares
(`UiWindow(..., logPrefix:)`), else with `[<GameActionName>/Settings]`, from the first line ("the window cannot
open") to the DLL's own; the project's action writes its lines under `[<its name>]`. Before stub 0.3.0 the stub
switched to the window's title once the project had described itself, so one project wrote under three names.

**Actions a project runs from its handlers see the request:** the stub hands each request to the project's action
as arguments (`uiframework.request`, `uiframework.name`, `uiframework.values` with the settings as JSON,
`uiframework.var`), and Streamer.bot passes a running action's arguments on to the actions it runs. So an action
started with `CPH.RunAction` from `test`, `validate` or `saved` sees them too: the settings are readable there, and
when that action uses UIFramework itself, its `UiHost.Handle` took them for a request to it and the action did
nothing else. Since UIFramework 1.2.0 the host block clears them (`CPH.SetArgument(key, "")`) once it has read
them, before it calls the project's handler. With an older host block, clear the request yourself before the
`CPH.RunAction`: `CPH.SetArgument("uiframework.request", "");`.

**Cooldowns:** on save, each command's cooldowns are set on their own. A key the window names for a cooldown that
the settings do not hold as a number leaves that cooldown as it is (with a warning in the log) instead of setting
it to 0. A cooldown longer than a day is set to a day (86400 seconds), and a command that does not exist gets none
set; the log warns of both. The project hears of the save whatever happened. Its "saved" request runs from the
settings action's queue, and when it finds another request still running it is sent again, three tries in all;
what still fails is said in the log and in a note beside the window (stub 0.3.2).

**The layout file** (`uiframework.layout.json`): the stub's note in it (`frameworkAvailable`) is written only when
it changes, through a file beside it that is swapped in, and never over a file it could not read. The DLL keeps a
layout file that is not JSON as `uiframework.layout.json.broken` and logs why; one that another program holds is
read again a few times and otherwise left as it is (the window starts from the defaults once).

**References:** none to add. The dialogs are Win32 message boxes (`user32.dll` through P/Invoke), and the download
runs through `System.Net.WebClient` from `System.dll`; both come with the references every action has.

**Release:** a tag `vX.Y.Z` (the `<Version>` in `UIFramework.csproj` must match) makes
`.github/workflows/release.yml` build the tag's own commit (which must be on `main`), test, create the release with
`UIFramework.dll` and write `lib/v<major>/latest.json` on `main` (`lib/v1/` for 1.x.y, `lib/v2/` from 2.0.0 on; the
first release of a line makes its folder). `minHostStub` is `MinHostStub` from the tag's `UIFramework.csproj`
(raise it there when a release needs a newer stub; the DLL then carries it too). An older tag released late never
takes `latest.json` back.

**A new major line (2.0.0):** a major version is for what breaks a project as it is (a builder call removed, a
schema key no longer read). Nothing in a release is special about it except the stub: `FrameworkLine` in
`stub/Stub.cs` becomes `"v2"`, the one word both `latest.json` addresses are built from, and `<Version>` in
`UIFramework.csproj` becomes `2.0.0`, with `MinHostStub` raised to that stub's version when the DLL needs it. A
test (`TheStubsLineIsTheFrameworksMajorVersion`) fails as long as the two disagree, so neither is forgotten. The
workflow writes `lib/v2/latest.json` by itself, and the DLL reads the file of its own line by itself. A project on
the v1 stub goes on reading `lib/v1/latest.json` and keeps the last 1.x until it ships the new stub, unless another
project has installed a 2.x that names no newer settings action (see *Safety*). What 2.0.0 is to take away is
planned outside this repository.
