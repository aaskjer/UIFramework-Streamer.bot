# Demo Game

The smallest game with a UIFramework settings window: a `!roll` that rolls a die, three settings, one chat
message, a Try button. The walk-through is [docs/Getting-Started.md](../../docs/Getting-Started.md).

| File             | What it is                                                                                   |
|------------------|----------------------------------------------------------------------------------------------|
| `DemoGame.cs`    | the game action: `UiHost.Handle` first, then the roll; the host block with its Window and Table at the end |
| `schema.json`    | what the game answers the window's `describe` with, written by `tools\check-stub.ps1`; the preview opens it with `UIFramework.Preview.exe -schema examples\DemoGame\schema.json` |
| `../../stub/Stub.cs` | the settings action, unchanged but for `GameActionName = "Demo Game"`                     |
| `DemoGame.sb`    | the two actions and the `!roll` command as one import; `tools\build-import.ps1` keeps its code on the two files |

In Streamer.bot, the short way: *Import*, drop `DemoGame.sb` in. That gives the action **Demo Game** (the game,
with the command `!roll`) and **.Demo GUI** (the settings action); run the latter, it offers to download
`UIFramework.dll`, the window opens.

By hand, which is what a project of your own does:

1. Action **Demo Game**, sub-action *Execute Code* with `DemoGame.cs`; trigger: command `!roll`.
2. Action **.Demo GUI**, sub-action *Execute Code* with `stub/Stub.cs`, no references to add; trigger:
   a command, a hotkey, a button.
3. Run the settings action. It offers to download `UIFramework.dll`; the window opens.

A win pays `pointsPerWin` into the viewer's points: the user variable of Currency Core or LOYALTY POINTS when
one is installed (the window shows which), else the one named in the Points settings.

The command's cooldowns follow the window's settings through the command's id in `DemoGame.cs` (`Cooldowns(C(…))`);
a command you make by hand has another id, so name it by its text there (`C("!roll", …)`) or copy its id.
