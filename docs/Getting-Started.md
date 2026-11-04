# Getting started: a settings window for your own Streamer.bot project

Ten minutes from a game that reads a JSON file to a settings window with pages, validation, a customise mode and
automatic updates of the window itself. Nothing to install: the settings action fetches `UIFramework.dll` on
first use and keeps it current.

You need two actions in Streamer.bot. `examples/DemoGame` has both as files and as an import (`DemoGame.sb`);
everything below is what they do.

## 1. The game action gets the host block

Paste the block from [`stub/HostSchema.cs`](../stub/HostSchema.cs) (everything between
`// --- UIFramework host block` and `// --- end UIFramework host block ---`) at the end of your action's code,
after your `CPHInline` class. It compiles in any action; it needs nothing beyond Newtonsoft.Json, which every
action has.

Fill in the two spots marked in it:

**The table**, one line per setting your game reads (`// __TABLE__`):

```csharp
public static readonly Setting[] Table =
{
    S("useBot", true),
    S("globalCooldown", 10),
    S("sides", 6, page: "game", label: "Sides of the die", help: "The highest one wins.", section: "The die", min: 2, max: 100),
    S("tryRoll", null, page: "game", label: "Roll", kind: "action", section: "Try a roll", live: true, panel: true),
    Msg("Rolls", "msgRoll", "@{user} rolls a {roll}: {result}.", "Roll", "What chat gets.", new[] { "user", "roll", "result" }),
};
```

Keys the framework knows (`useBot`, `globalCooldown`, `userCooldown`, `blockedUsers`, ... see
[`conventions/keys.json`](../conventions/keys.json)) need only their default: label, help, page and limits come
with them. `S(key, default, ...)` takes everything else as named arguments; `Msg(...)` is a chat message with
placeholders. [docs/Schema.md](Schema.md) lists every argument and kind (numbers, switches, dropdowns, sliders,
colours, paths, emotes, lists with a row editor, buttons that ask your game something).

**The window** (`// __WINDOW__`), as much or as little as you like:

```csharp
public static readonly UiWindow Window = new UiWindow("Demo Game", releases: "you/your-repo")
    .Pages(P("game", "Game", "dice"), "messages", "help", "about")
    .Cooldowns(C("364c549b-0294-486b-80ae-ea838e14ec18", "globalCooldown", "userCooldown"))
    .Help(H("Playing", Cmd("!roll", "Rolls the die once.")))
    .About(credits: "By you.");
```

`Cooldowns` is how the cooldown settings reach the command: after every save the settings action sets the
`!roll` command's global and per-user cooldown in Streamer.bot from those two keys. Without it they are just
numbers in the file. The command is named by its id (right-click it in Streamer.bot, *Copy Id*); an export and
import keep the id, so this is what to ship. For a command made by hand its text (`"!roll"`) works too.

`releases` is your GitHub repository: it gives the About page its links and the window its "new version of the
game" check.

## 2. The game action answers the window

First thing in `Execute()`:

```csharp
if (UiHost.Handle(args, CPH, ActionName, "demo", CurrentVersion, SettingsFile,
        test: (name, s) => name == "tryRoll" ? Roll(s, "alice") : null))
    return true;
```

`ActionName` is this action's exact name in Streamer.bot, `SettingsFile` the settings file relative to the
Streamer.bot folder (`"Demo Game/DemoGame-Settings.json"`); the window writes it, your game reads it with
`UiSettings.Load(path)` and `s.Int("sides")`, `s.Bool("useBot")`, `s.String("msgRoll")`. The optional delegates
are how the window talks to your game: `test` answers a Try button or a live field, `validate` checks before a
save, `saved` runs after one, `table` and `emotes` fill special fields. All are optional; a game with plain
settings passes none.

## 3. The settings action is the stub, unchanged

Make a second action, paste [`stub/Stub.cs`](../stub/Stub.cs) as its Execute Code and change its one line:

```csharp
private const string GameActionName = "Demo Game";
```

Nothing to add to the action's references. Give it a trigger: a command, a hotkey, a button in a deck.

That is all. Run the settings action: the first time it asks whether to download `UIFramework.dll` into
`<Streamer.bot>\UIFramework\lib\`; then the window opens with your pages, a Messages page with a chat preview,
Help and About, a customise mode (the pen) and export/import. Whenever a newer framework is released, the stub
offers it on the next open ([docs/Loader.md](Loader.md)).

## What you get without writing it

- Pages from the table, sections where `section` changes, numbers three to a line, switches two.
- Validation: limits, a `requires` that greys a field out, your `validate` answer before a save.
- A Messages page with placeholder chips and a live preview, sender switches, a cooldown group.
- A Log page that follows your log folder as the game writes it (`.Log(folder, files)` in the window).
- Reset to defaults, per page or all; unsaved-change tracking; dark and light theme; an accent colour.
- The customise mode: users rename, move and hide fields and pages, in `uiframework.layout.json` next to the
  settings file, surviving every import of your game ([docs/Customize.md](Customize.md)).
- Export and import of the settings.
- A banner and a list on the About page when the table or the window declares something the framework cannot
  use (an unknown kind, a requirement on a key that is not there), so you see it without reading the log.

## Keeping up

The host block is the same in every game. When a new framework release changes it, run

```powershell
tools\update-host-block.ps1 -File "C:\...\YourGame.cs"
```

which swaps the block and keeps your Window and Table. The settings action never changes per project; replace it
with the new `stub/Stub.cs` when a release says so.
