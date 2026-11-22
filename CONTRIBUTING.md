# Contributing

Using UIFramework in your own project, or changing it for yourself, needs no permission: it is MIT, see
[LICENSE](LICENSE). This page is for what should come back into this repository. One person maintains it, so an
answer can take a few days.

## Where things go

- **A text of the window that is wrong, awkward or missing in your language:** the
  [Fix a translation](https://github.com/aaskjer/UIFramework-Streamer.bot/issues/new?template=translation.yml)
  form. No pull request needed.
- **A new language:** one file, `conventions/lang/<code>.json`.
  [Adding a language](docs/Conventions.md#adding-a-language) has the steps.
- **A bug in the window, the loader, the settings action or the host block:** an
  [issue](https://github.com/aaskjer/UIFramework-Streamer.bot/issues). Say which UIFramework version runs
  (Streamer.bot's log names it), what you did and what happened instead, and add the lines the settings wrote into
  the log.
- **A problem with a project built on UIFramework** (its own settings, its chat messages, what it does): that
  project's repository.
- **A security problem:** [SECURITY.md](SECURITY.md), not an issue.
- **A larger change** (a new kind of setting, anything in the schema, the stub or the host block): open an issue
  before you write it. The stub and the host block are pasted into other people's projects and stay there, so a
  change has to keep the ones already out there working.

## Building

Windows and the .NET SDK 10.0.x, which is what CI uses.

```
dotnet build UIFramework.slnx
dotnet test
```

`src/UIFramework.Preview/bin/Debug/net48/UIFramework.Preview.exe` opens a sample window without Streamer.bot; the
Development section of the [README](README.md) lists its switches and what lives in which folder.

## What a pull request has to pass

Pull requests go against `main`. CI builds, runs the tests and checks that three things made from the sources
still match them. Run the script that belongs to what you changed, and commit what it writes:

| You changed | Run |
|---|---|
| a file in `conventions/` | `tools/conventions-doc.ps1` (the tables in `docs/Conventions.md`) |
| `stub/HostSchema.cs` | `tools/update-host-block.ps1 -File <file>` for `examples/DemoGame/DemoGame.cs` and for each `hosts/<name>/HostBlock.cs` |
| `examples/DemoGame/DemoGame.cs` or `stub/Stub.cs` | `tools/build-import.ps1` (the demo's import) |

Each script takes `-Check` and then only reports, the way CI calls it.

`tools/check-stub.ps1` compiles the stub and the demo the way Streamer.bot does. It needs a Streamer.bot install
and a RoslynHost, so CI leaves it out; run it if you have both and touched `stub/`.

A fix comes with a test in `src/UIFramework.Tests` wherever a test can show it.

## Texts

- A new text of the window goes into every file in `conventions/lang/`. The tests want each entry of `en.json` in
  all of them, with the same `{placeholders}`.
- Docs and window texts speak of a project or an extension. UIFramework is for any Streamer.bot extension; "game"
  stays where a game is meant (the demo game, the list of games built on it).
- What the docs say and what the code does should agree. If your change makes one of them wrong, change the other
  in the same pull request.

## Versions and releases

Versions, the sections in [CHANGELOG.md](CHANGELOG.md) and the tags are the maintainer's. A pull request leaves
`<Version>` in `UIFramework.csproj`, the changelog and `lib/v*/latest.json` alone; the release workflow writes the
last one. Say in the pull request what a user of the window or of the host block will notice, so that it can go
into the changelog.

## Licence

What you contribute is under the MIT licence, like the rest.
