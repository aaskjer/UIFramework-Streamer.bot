# UIFramework for streamer.bot

A settings-window framework for Streamer.bot extensions. One DLL that fetches and updates itself, one look for
every project that uses it, pages built from data the project declares, and a customise mode that lets users
rename, move and hide things without touching code.

Status: planning. See [PLAN.md](PLAN.md) for the architecture and phases, [docs/Schema.md](docs/Schema.md) for
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
stub/                    template for a host's settings action (loader + spec + hooks)
conventions/             keys.json and lang/<code>.json
lib/v1/latest.json       what the loader reads: version, download url, sha256
docs/                    Schema, Spec, Layout, Hooks, Customize
```

## Licence

MIT, see [LICENSE](LICENSE).
