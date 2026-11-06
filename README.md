# UIFramework for streamer.bot

A settings-window framework for Streamer.bot extensions. One DLL that fetches and updates itself, one look for
every project that uses it, pages built from data the project declares, and a customise mode that lets users
rename, move and hide things without touching code.

Status: phases 0 to 5 done, phase 7 (the kit) nearly done; all eight hosts run on the generic stub: High-Low (v2.1.0), Classic Chat Gamble (v25.1.0), RhymeTime (v2.2.0), Slot Machine (v16.2.0), Blackjack (v17.2.0), Battleship (v2.2.0), Hangman (v13.0.0) and Trivia (v9.0.0). The loader in the stub fetches the DLL on first use and keeps it current (docs/Loader.md); the customise mode lets users rename, move and hide pages and fields without touching code (docs/Customize.md). `dotnet build UIFramework.slnx` builds the DLL, the tests and the preview; `dotnet test` runs the 73 tests; `src/UIFramework.Preview/bin/Debug/net48/UIFramework.Preview.exe` opens a Blackjack-shaped sample window (`-theme dark` or `light`, `-shots <folder>` renders every page to PNG, `-settle 3000` with that long for emote pictures to arrive; `-schema examples/DemoGame/schema.json` or `-schema hosts/battleship/schema.json` opens the window a game declares).

## Use it in your own project

Two files, nothing to install: [docs/Getting-Started.md](docs/Getting-Started.md) walks through it in ten
minutes, [examples/DemoGame](examples/DemoGame) is the result.

1. Paste the host block from `stub/HostSchema.cs` into your game's action, fill in its table (one line per
   setting) and its window (title, pages, help).
2. Call `UiHost.Handle(args, CPH, ...)` first thing in `Execute()`; it answers the settings window.
3. Make a second action from `stub/Stub.cs`, put your game action's name into `GameActionName`, give it a
   trigger. The window opens with pages, validation, a customise mode, export/import and self-updates.

[docs/Schema.md](docs/Schema.md) lists every kind of setting and every part of the window declaration.
[PLAN.md](PLAN.md) (German) holds the architecture and phases, [docs/Phase0-Konventionen.md](docs/Phase0-Konventionen.md)
(German) the conventions drawn from the seven game GUIs this replaces.

## How it fits together

1. Framework conventions (`conventions/keys.json`): known keys, standard pages, theme defaults.
2. The game's schema: its settings and its window, answered on request through its host block.
3. The settings action: the generic stub, the same file everywhere (hosts not yet moved carry a spec of their own).
4. `uiframework.layout.json` next to the settings file: the user's changes. Survives every import.

## Layout

```
src/UIFramework          the DLL (.NET Framework 4.8, WPF)
src/UIFramework.Tests    xUnit
src/UIFramework.Preview  opens the window without Streamer.bot
stub/                    Stub.cs, the generic settings action (loader + bridge); HostSchema.cs, the host block a
                         game pastes in
examples/DemoGame/       the smallest game with a window: DemoGame.cs, DemoGame.sb (the import), schema.json, a README
hosts/<name>/            schema.json of each host (what its game answers, with the window) and HostBlock.cs, its
                         host block as the game carries it
conventions/             keys.json and lang/<code>.json, embedded into the DLL
tools/                   update-host-block.ps1 (refresh the block in a game, keeping its table), check-stub.ps1
                         (compile the stubs and the demo the Streamer.bot way), build-import.ps1 (keep an export's
                         code on the files), spec-to-window.ps1 (a host's spec.json as a Window declaration, once per
                         host on the way to the generic stub; every host of this repository has made it)
lib/v1/latest.json       what the loader reads: version, download url, sha256
docs/                    Getting-Started, Schema, Loader, Customize, Phase0-Konventionen (German)
```

## Licence

MIT, see [LICENSE](LICENSE).
