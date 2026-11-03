# UIFramework for streamer.bot

A settings-window framework for Streamer.bot extensions. One DLL that fetches and updates itself, one look for
every project that uses it, pages built from data the project declares, and a customise mode that lets users
rename, move and hide things without touching code.

Status: phases 0 to 5 under way; Blackjack (v17.0.0), RhymeTime (v2.0.0), Slot Machine (v16.0.0), Battleship (v2.0.0) and Classic Chat Gamble (v25.0.0) run on it (each game declares the schema, its settings action is the stub generated from `hosts/<name>`). A `list` kind with a row editor keeps a host's word bank or question list in the file the game already reads (`file`/`field` in docs/Schema.md). The loader in the stub fetches the DLL on first use and keeps it current (docs/Loader.md); the customise mode lets users rename, move and hide pages and fields without touching code (docs/Customize.md). `dotnet build UIFramework.slnx` builds the DLL, the tests and the preview; `dotnet test` runs the 44 tests; `src/UIFramework.Preview/bin/Debug/net48/UIFramework.Preview.exe` opens a Blackjack-shaped sample window (`-theme dark` or `light`, `-shots <folder>` renders every page to PNG; `-spec hosts/blackjack/spec.json -schema hosts/blackjack/schema.json` takes the schema-driven path a stub takes).

See [PLAN.md](PLAN.md) for the architecture and phases, [docs/Schema.md](docs/Schema.md) for
how a host declares its settings, and [docs/Phase0-Konventionen.md](docs/Phase0-Konventionen.md) for the
conventions drawn from the seven game GUIs this replaces.

## How it fits together

1. Framework conventions (`conventions/keys.json`): known keys, standard pages, theme defaults.
2. Host schema: what the project knows, answered on request.
3. Spec in the host's stub: how the project ships.
4. `uiframework.layout.json` next to the settings file: the user's changes. Survives every import.

## Layout

```
src/UIFramework          the DLL (.NET Framework 4.8, WPF)
src/UIFramework.Tests    xUnit
src/UIFramework.Preview  opens the window without Streamer.bot
stub/                    Stub.cs, a host's settings action (loader + bridge); HostSchema.cs, the game's schema block
hosts/<name>/            schema.json, spec.json and the generated Stub.cs of each host
conventions/             keys.json and lang/<code>.json, embedded into the DLL
tools/                   embed-spec.ps1 (spec into stub), check-stub.ps1 (compile both the Streamer.bot way)
lib/v1/latest.json       what the loader reads: version, download url, sha256
docs/                    Schema, Spec, Loader, Customize, Phase0-Konventionen
```

## Licence

MIT, see [LICENSE](LICENSE).
