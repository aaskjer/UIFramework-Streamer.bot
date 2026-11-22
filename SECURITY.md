# Security

UIFramework runs inside Streamer.bot with the user's rights, and its settings action downloads the DLL and loads
it. A hole there reaches every project built on it, so please report one privately.

## Reporting

[Report a vulnerability](https://github.com/aaskjer/UIFramework-Streamer.bot/security/advisories/new) (the
*Security* tab of the repository). Only the maintainer sees the report. Please open no public issue or pull
request for it.

Say which UIFramework version and which settings action (the stub's version) you looked at, and how to reproduce
it. One person maintains this, so an answer can take a few days.

## What counts

- A way to make the loader fetch or load a DLL other than the published one: how `latest.json` is read, the
  download, the SHA-256 check. [docs/Loader.md](docs/Loader.md) says what it checks.
- A settings file, layout file, export, language file or schema that makes the window run code, or write files
  where it should not.
- A secret that ends up where it does not belong: a value kept in a global (`global:`) that reaches the settings
  file, an export or the log, or a project's answer that stays readable in Streamer.bot's variables.

## What does not

- Someone who can already write to the Streamer.bot folder. The hash beside the DLL catches a damaged file, not
  that: a DLL put in by hand has no hash file and is loaded as it is.
- A `masked` field without `global:`. It is shown as dots, but its value is saved in the settings file like any
  other.
- Streamer.bot itself, and the projects built on UIFramework. Those go to their own makers, unless the cause is
  in the stub or the host block they carry; then it belongs here.

## Versions

| Line | Fixes |
|---|---|
| 2.x | in a new release, which every window offers the next time it opens |
| 1.x | serious problems only |

A settings action reads the releases of its own line, so a project still on a 1.x stub is not offered a 2.x fix
until it ships the newer stub. A user who turned the release check off (`"autoUpdate": false` in
`UIFramework\loader.json`) is offered none.
