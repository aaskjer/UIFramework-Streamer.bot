# Demo Game

The smallest game with a UIFramework settings window: a `!roll` that rolls a die, three settings, one chat
message, a Try button. The walk-through is [docs/Getting-Started.md](../../docs/Getting-Started.md).

| File             | What it is                                                                                   |
|------------------|----------------------------------------------------------------------------------------------|
| `DemoGame.cs`    | the game action: `UiHost.Handle` first, then the roll; the host block with its Window and Table at the end |
| `schema.json`    | what the game answers the window's `describe` with, written by `tools\check-stub.ps1`; the preview opens it with `UIFramework.Preview.exe -schema examples\DemoGame\schema.json` |
| `../../stub/Stub.cs` | the settings action, unchanged but for `GameActionName = "Demo Game"`                     |

In Streamer.bot:

1. Action **Demo Game**, sub-action *Execute Code* with `DemoGame.cs`; trigger: command `!roll`.
2. Action **Demo Game Settings**, sub-action *Execute Code* with `stub/Stub.cs`, no references to add; trigger:
   command `!demosettings`, or a button.
3. Run the settings action. It offers to download `UIFramework.dll`; the window opens.
