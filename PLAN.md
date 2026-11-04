# UIFramework for streamer.bot: Umbauplan

Stand: 2026-10-31. Ausgangslage: sieben Streamer.bot-Projekte mit je eigener WPF-Settings-GUI
(Blackjack, Slot Machine, Battleship, Hangman, Classic Chat Gamble, Trivia, RhymeTime), 3 600 bis 6 800
Zeilen pro Datei, 85 gleiche Hilfsmethoden, sieben auseinandergelaufene `Theme`-Kopien.

## 1. Ziel

Ein Fenster-Framework, das

- in einer DLL liegt, die sich selbst aus GitHub holt und aktualisiert (kein manueller Ordner-Schritt),
- bei allen Projekten gleich aussieht, weil Theme, Rahmen und Bausteine nur einmal existieren,
- seine Seiten aus Daten baut, die das jeweilige Projekt liefert (Schema), nicht aus projekteigenem WPF-Code,
- Nutzern erlaubt, Seiten umzubenennen, Felder zu verschieben oder auszublenden, per "Anpassen"-Modus im
  Fenster oder per Hand in einer JSON-Datei, ohne Code und ohne Verlust beim nächsten Import,
- nicht an Spiele gebunden ist: jedes Projekt mit einer Settings-Datei kann es nutzen.

Nicht Ziel: Spiellogik, Overlays, Chat-Ausgabe. Die bleiben in den Projekten.

## 2. Begriffe

| Begriff   | Bedeutung                                                                                     |
|-----------|-----------------------------------------------------------------------------------------------|
| Framework | Die DLL `UIFramework.dll`: Shell, Theme, Controls, Schema-Renderer, Anpassen-Modus, Loader-Gegenstück |
| Host      | Ein Projekt, das das Framework nutzt (Blackjack, Trivia, …)                                   |
| Schema    | Was der Host an Settings kennt: Key, Default, Typ, Hinweise. Kommt vom Host, auf Anfrage      |
| Spec      | Wie der Host ausgeliefert aussieht: Seiten, Reihenfolge, Zuordnung der Keys. Liegt im Stub    |
| Layout    | Nutzeranpassungen: `uiframework.layout.json` neben der Settings-Datei. Höchste Priorität     |
| Stub      | Die Settings-Aktion des Hosts in Streamer.bot: Loader + Spec + Hooks, 200 bis 400 Zeilen      |
| Hooks     | Callbacks des Hosts, die das Framework ruft: Anwenden, Testen, Sonderseite bauen              |

Schichten, von unten nach oben, jede überschreibt die darunter:

1. Framework-Konventionen (bekannte Keys, Standardseiten, Theme-Defaults)
2. Schema des Hosts
3. Spec im Stub
4. Layout des Nutzers

## 3. Repository `aaskjer/UIFramework`

```
UIFramework/
  src/UIFramework/
    UIFramework.csproj         .NET Framework 4.8, x64, Referenzen: Streamer.bot.Plugin.Interface, Newtonsoft.Json,
                               PresentationFramework/Core, WindowsBase, System.Xaml (GAC)
    Entry.cs                   public static bool Show(IInlineInvokeProxy cph, string specJson, IHostHooks hooks)
    Shell/Window.cs            Fenster, Rail (Sidebar), PageStack, Titelzeile, Statuszeile, Ungespeichert-Zähler
    Shell/Claim.cs             ClaimWindow/ReleaseWindow: ein Fenster pro Host, STA-Thread, Application-Instanz
    Shell/Theme.cs             Farben hell/dunkel, Rainbow, Akzentfarbe aus Settings, Colour-Picker
    Shell/Icons.cs             Glyphen, Logo-Laden (URL, Cache)
    Controls/*.cs              Row, Labeled, Note, Pill, NameChip, NameList, IntField, DoubleField, TextField,
                               Toggle, Combo, ColourField, EmoteField, DurationField, PathField, ListEditor, Table
    Schema/Schema.cs           Modell: Setting { Key, Default, Kind, Page, Label, Help, Min, Max, Unit, Options }
    Schema/Conventions.cs      Wörterbuch bekannter Keys -> Seite, Kind, Label, Help
    Schema/Resolve.cs          Konventionen + Schema + Spec + Layout -> fertige Seitenliste
    Pages/General.cs           Bot-Konto, Plattformen, Sprache (aus Konventionen)
    Pages/Access.cs            Erlaubte/gesperrte Nutzer und Gruppen
    Pages/Messages.cs          Textvorlagen mit Platzhaltern und Chat-Vorschau
    Pages/Log.cs               Aktionslog des Hosts
    Pages/Help.cs              Markdown-ähnliche Hilfe aus der Spec
    Pages/About.cs             Version Host + Framework, Update-Check, Links
    Customize/Mode.cs          Anpassen-Modus: Umbenennen, Verschieben, Ausblenden, Zurücksetzen
    Customize/Layout.cs        uiframework.layout.json lesen/schreiben (atomar, .bak)
    Settings/Store.cs          Settings-Datei lesen/schreiben, Migration alter Keys, KeepDamagedCopy
    Update/Check.cs            Release-Check des Hosts (GitHub Releases) und des Frameworks
  src/UIFramework.Tests/       xUnit: Resolve, Layout-Merge, Store, Schema-Parsing, Conventions
  src/UIFramework.Preview/     kleine EXE: öffnet das Fenster mit einem Beispiel-Schema ohne Streamer.bot
  stub/
    Stub.cs                    Vorlage für die Settings-Aktion eines Hosts (Loader + Spec + Hooks)
    Loader.cs                  Der Loader-Block, wird in Stub.cs eingebettet
  conventions/
    keys.json                  bekannte Keys (Quelle für Conventions.cs, zur Build-Zeit eingebettet)
    lang/en.json, de.json      Labels/Hilfetexte der Konventionen
  lib/v1/latest.json           { "version", "url", "sha256", "minHostStub" }
  docs/
    Schema.md, Spec.md, Layout.md, Hooks.md, Customize.md
  .github/workflows/
    ci.yml                     build + tests bei jedem Push
    release.yml                bei Tag vX.Y.Z: build, test, DLL als Release-Asset, lib/v1/latest.json aktualisieren
```

Der Release-Workflow ist der Ort für die eingangs empfohlene Actions-Struktur: `setup`, `build`, `test`, `publish` als
Composite Actions unter `.github/actions/`, die `ci.yml` und `release.yml` teilen.

## 4. Datenformate

### 4.1 Schema (vom Host, auf Anfrage)

Der Host hält eine Tabelle, aus der er selbst seine Settings liest. Auf Anfrage gibt er sie als JSON aus.

```json
{
  "host": "blackjack", "version": "2.3.0",
  "settingsFile": "Blackjack Game/BJG-Settings.json",
  "settings": [
    { "key": "useBot",          "default": true },
    { "key": "numDecks",        "default": 6,    "min": 1, "max": 8 },
    { "key": "blackjackPayout", "default": 1.5,  "kind": "double", "min": 1, "max": 3, "step": 0.5,
      "label": "Blackjack payout", "help": "Multiplier on the bet for a natural blackjack." },
    { "key": "overlayFolder",   "default": "",   "kind": "path" },
    { "key": "cardDisplayMode", "default": "unicode", "options": ["unicode", "text", "emotes"] },
    { "key": "blockedUsers",    "default": "",   "kind": "namelist" }
  ]
}
```

Regeln: `kind` ist optional und wird sonst aus dem Default abgeleitet (bool, int, double, string). `useBot`
und `blockedUsers` brauchen nichts weiter, sie stehen in den Konventionen. Werte liefert das Schema nie.

Abfrage aus dem Stub, synchron:

```csharp
CPH.SetArgument("uiframework.describe", "1");
CPH.RunAction("Blackjack Game", true);
string schema = CPH.GetGlobalVar<string>("uiframework.schema.blackjack", false);
```

Im Host, erste Zeilen von `Execute()`:

```csharp
if (args.ContainsKey("uiframework.describe")) { CPH.SetGlobalVar("uiframework.schema.blackjack", Schema.ToJson(), false); return true; }
```

### 4.2 Spec (im Stub, Auslieferungszustand)

```json
{
  "title": "Blackjack",
  "logo": "https://raw.githubusercontent.com/aaskjer/Blackjack/main/Assets/logo.png",
  "releases": "aaskjer/Blackjack",
  "pages": [
    { "id": "general" },
    { "id": "table",   "title": "Table",   "icon": "cards",
      "fields": ["numDecks", "dealerStandsOn", "reshuffleThreshold", "blackjackPayout"] },
    { "id": "rules",   "title": "Rules",   "icon": "list",
      "fields": ["allowDoubleDown", "allowSplit", "allowResplit", "maxSplitHands", "allowInsurance", "allowSurrender"] },
    { "id": "overlay", "title": "Overlay", "icon": "monitor", "fields": ["overlay*"] },
    { "id": "stats",   "title": "Stats",   "icon": "chart",   "custom": "stats" },
    { "id": "access" }, { "id": "messages" }, { "id": "log" }, { "id": "help" }, { "id": "about" }
  ],
  "help": "…",
  "overrides": { "useBot": { "page": "table" } }
}
```

Standardseiten (`general`, `access`, `messages`, `log`, `help`, `about`) brauchen nur ihre id. `fields` erlaubt
Wildcards. Keys des Schemas, die keine Seite nennt, landen auf `game` (wird bei Bedarf angelegt). `custom` ruft
`hooks.BuildPage("stats")`.

### 4.3 Layout (vom Nutzer, `uiframework.layout.json` neben der Settings-Datei)

```json
{
  "pages": {
    "table":  { "title": "Tisch", "order": 1 },
    "rules":  { "hidden": true },
    "general": { "icon": "gear" }
  },
  "fields": {
    "useBot":    { "page": "table", "order": 0 },
    "egg":       { "hidden": true },
    "numDecks":  { "label": "Anzahl Decks" }
  },
  "theme": { "accent": "#7c3aed", "dark": true }
}
```

Nur Präsentation. Ein ausgeblendetes Feld behält seinen Wert. Ein Key, den das Schema nicht mehr kennt, wird
ignoriert und beim nächsten Speichern entfernt. Ein `Zurücksetzen` im Anpassen-Modus löscht die Datei.

## 5. Anpassen-Modus

Umschalter in der Titelzeile (Stift-Icon). Im Modus:

- Seiten in der Rail: Rechtsklick -> Umbenennen, Icon wählen, Ausblenden; Drag zum Sortieren.
- Felder: Griff links, Drag innerhalb der Seite oder auf eine andere Seite in der Rail; Rechtsklick ->
  Umbenennen, Ausblenden, Auf Standard.
- Standardseiten lassen sich umbenennen und verschieben, `about` nicht ausblenden.
- Jede Änderung schreibt sofort `uiframework.layout.json` (atomar). Kein eigener Speichern-Schritt.
- Statuszeile zeigt "Angepasst (n Änderungen)" mit Link `Zurücksetzen` und `Datei öffnen`.
- Verlassen des Modus: Fenster rendert neu aus Schichten 1 bis 4.

Nicht im Modus: Felder erzeugen, Typen ändern, Werte anfassen. Das wäre Schema, nicht Layout.

## 6. Hooks (Host-Seite, im Stub)

```csharp
public interface IHostHooks
{
    void OnApply(JObject settings);                 // nach Speichern: Cooldowns setzen, Timer, OBS, Websocket
    string Test(string name, JObject settings);     // "Test"-Buttons, Rückgabe = Statuszeile
    FrameworkElement BuildPage(string id, IPageTools tools);   // Sonderseiten; tools gibt Theme + Controls
    IEnumerable<LogEntry> ReadLog();                // Log-Seite; null = Seite aus
}
```

`IPageTools` stellt den Sonderseiten dieselben Controls und Farben zur Verfügung wie dem Framework, damit eine
Sonderseite nicht anders aussieht. Hooks werden über eine Schnittstelle aus `Streamer.bot.Plugin.Interface`-
freien Typen gereicht; der Stub implementiert sie als innere Klasse.

## 7. Loader (im Stub)

- Ordner: `<Streamer.bot>\UIFramework\lib\<version>\UIFramework.dll`; Fallback `%LOCALAPPDATA%\UIFramework\lib`.
- `Init()`: asynchron `lib/v1/latest.json` lesen (Timeout 3 s). Neue Version -> Download nach `.tmp`, SHA-256
  prüfen, in Versionsordner verschieben. Alte Ordner, die nicht geladen sind, löschen.
- `Execute()`: neueste lokal vorhandene Version per `Assembly.LoadFrom`, Delegate auf `Entry.Show` einmal
  erzeugen und statisch cachen. Fehlt jede Version: Toast, Log, `return true`. Settings bleiben per Datei nutzbar.
- Named Mutex `Global\UIFramework.Loader` um Download und Erstladen, damit mehrere Hosts beim Start nicht
  parallel laden.
- Major-Version gepinnt (`v1`). `minHostStub` in `latest.json`: ist der Stub älter, zeigt das Framework im
  About einen Hinweis, dass der Host einen neuen Import braucht, und läuft im Kompatibilitätsmodus weiter.
- Setting `uiframework.autoUpdate` (Default an) in `uiframework.layout.json`; aus = nur lokal vorhandene DLL.
- Alle Stubs tragen denselben Loader-Block; `stub/Loader.cs` ist die Quelle, ein Drift-Check im Host-Projekt
  vergleicht dagegen (dasselbe Muster wie der ChatCalendar-Sync).

## 8. Phasen

Stand 31.10.2026: Phase 0 und 1 fertig, Phase 2 auf Framework-Seite fertig (Schema, Konventionen eingebettet,
Resolve, Spec-JSON, Reflection-Host, Alias-Migration, Blackjack-Schema und -Spec aus der alten GUI erzeugt und
in der Preview geprüft, Stub und Host-Block kompilieren als Streamer.bot-Aktion). Blackjack ist umgestellt
(31.10.): `UiSchema.Table` mit 262 Settings im Spiel und in der Stats-Aktion, `Settings`-Fassade über `UiSettings`
mit den alten Clamps, `uiframework.describe`, die Settings-Aktion ist der Stub mit Blackjack-Host-Sektion
(Cooldowns, Leaderboard-Reset, OBS-Setup als Jobs auf Streamer.bots Thread). Die alte GUI (4.800 Zeilen WPF) und
ihre Harnesse sind weg; `run-window-tests.ps1` öffnet das echte Fenster über den Stub und schreibt
`hosts/blackjack/schema.json`. Phase 3 (01.11.): Loader mit Erststart-Dialog, Download über `lib/v1/latest.json` mit SHA-256,
Hintergrund-Update in `Init`, Release-Workflow; v0.1.0 und v0.1.1 sind veröffentlicht. Phase 4 (01.11.):
Layout-Datei als vierte Schicht (Seiten: Titel, Icon, Reihenfolge, ausblenden; Felder: Seite, Reihenfolge, Label,
Abschnitt, ausblenden), Anpassen-Modus im Fenster mit Griff, Drag auf Felder und Seiten, Kontextmenüs, Dialogen,
Akzentfarbe, Sprache, Reset; `docs/Customize.md`. Offen aus Phase 4: Screenshots für die Docs. Update-Dialog
statt stillem Download (v0.2.0). Phase 5 (01.11.): `list`-Kind mit Zeilen-Editor und Ablage in eigener Datei
(`file`/`field`, `Sidecars`) für Wortbank und Fragenlisten (v0.3.0); Blackjack v17.0.0 als erster Host
veröffentlicht; RhymeTime v2.0.0 umgestellt (Host-Block in Game und Battle, Wortbank als drei `list`-Settings in
`RT-wordbank.json`, Overlay-Seite im Game-Script, Stub aus `hosts/rhymetime`); Release v2.0.0 als PR und
Draft-Release vorbereitet (03.11.). Nach dem ersten Praxistest (01./02.11., v0.3.1 bis v0.3.4): Pille im Kopf nur
nach "Nein" im Update-Dialog, Anpassen-Modus baut das Fenster an Ort und Stelle neu (kein Schließen/Öffnen mehr),
Nachrichten-Seite anpassbar, nachsichtiges Drag&Drop, Akzentfarbe wird beim Picker-Schließen übernommen; Releases
tragen den CHANGELOG-Abschnitt als Notes und sind bis 1.0.0 Pre-Releases. Slot Machine v16.0.0 umgestellt (03.11.,
v0.4.0): `action` mit `live`/`panel`/`auto` und `transient`-Felder für Quoten und Probe-Spin aus der Spiel-Engine
(gemeinsamer `SlotEngine`-Block in Spiel und Stub), Emotes als drei `list`-Settings mit Punkt-Alias aus
`PlatformEmotes`, Log-Seite aus der Spec; nachgerüstet v0.4.1 bis v0.6.0: Emote-Bilder (Spalte und Feld),
`requires` mit Bedingung, `Validate` vor dem Speichern, Balken-Panels, `note`-Kind, Aktionen, die Felder setzen.
Battleship v2.0.0 umgestellt (03.11., v0.6.0): Presets über `set`, Flotten- und Uhr-Zusammenfassung als
`auto`-Panels, Heat-Marker und OBS-Setup als Jobs, Scoreboard mit demselben Host-Block. Classic Chat Gamble v25.0.0 umgestellt (03.11.): Nachrichten bleiben als
Sidecar-Felder in `messages.json`, Fairness-Presets als Buttons, das Spiel schreibt Free-Game-Zustand und
Addicted-Liste unter den neuen Keys zurück. Offen in Phase 5: Hangman, Trivia. Screenshots für alle Docs bleiben für
den Abschluss (Phase 6).

### Phase 0: Entscheidungen festhalten (½ Tag)
- Projektname und Namespace `UIFramework`, Repo anlegen, Lizenz wie die Spiele.
- Schema-Kinds festlegen: bool, int, double, string, text (mehrzeilig), options, namelist, path, colour, emote,
  duration, list (Editor), table (nur Anzeige).
- Konventionsliste aus den sieben GUIs ziehen: alle Keys, die in mindestens drei Projekten vorkommen.

### Phase 1: Framework-Kern aus Blackjack + RhymeTime (1 Woche)
Beide teilen heute dasselbe `Theme`. Aus ihren GUIs entstehen Shell, Theme, Icons, Controls, Pages (General,
Access, Messages, Log, Help, About), Store, Update-Check. Noch ohne Schema: `Entry.Show` nimmt vorerst eine
hart codierte Seitenliste. Preview-EXE läuft. Tests für Store und Theme.
Fertig, wenn: Blackjack-Fenster aus der DLL sieht aus wie heute, Preview-EXE zeigt es ohne Streamer.bot.

### Phase 2: Schema und Spec (3 Tage)
Schema-Modell, Conventions, Resolve, Spec-Parser. Blackjack-Host bekommt die Settings-Tabelle und
`uiframework.describe`. Stub für Blackjack: Loader lokal (DLL aus Build-Ordner, kein Download), Spec, Hooks
für Cooldowns und OBS. Stats-Seite als erste `custom`-Seite.
Fertig, wenn: Blackjack läuft vollständig über Stub + DLL, `check-settings.ps1` wird nicht mehr gebraucht.

### Phase 3: Loader und Release (2 Tage)
`release.yml`, `latest.json`, Hash, Loader mit Download, Mutex, Versionsordner, `minHostStub`, Auto-Update-Schalter.
Test: frische Streamer.bot-Instanz, Blackjack importieren, Fenster öffnen, DLL kommt an. Zweite Version taggen,
Neustart, Update greift. Netz trennen, Fenster öffnet aus Cache.

### Phase 4: Anpassen-Modus und Layout (4 Tage)
Layout-Datei, Merge in Resolve, Modus-UI (Umbenennen, Drag, Ausblenden, Zurücksetzen), Theme-Overrides.
Tests für Merge und Dateiformat. `docs/Customize.md` mit Screenshots.

### Phase 5: Weitere Hosts (1 bis 2 Tage je Host)
Reihenfolge nach Sonderseiten, leichteste zuerst: RhymeTime, Slot Machine (Emotes-Editor -> `list`), Battleship,
Classic Chat Gamble, Hangman (Wortliste -> `list`, Evil -> custom), Trivia (Fragenquelle, Fragen-Editor,
Vorschläge -> teils `list`, teils custom). Bei jedem Host: Settings-Tabelle in den Host, Stub, Spec, alte GUI-Datei
löschen, Release.

### Phase 6: Abschluss (1 Tag)
READMEs der Hosts: Abschnitt "Settings-Fenster wird automatisch geladen, so schaltest du es ab". Konventionen
und Sprachdateien veröffentlichen. ChatCalendar und die übrigen Nicht-Spiele-Projekte als Kandidaten bewerten.

Summe: etwa vier bis fünf Arbeitswochen bis alle sieben Spiele umgestellt sind.

### Phase 7: Baukasten (etwa zwei Wochen, nach Phase 6)

Ziel: Ein fremdes Projekt bindet das Framework mit zwei Dateien an, ohne dieses Repository zu klonen und ohne
PowerShell: den Host-Block in die eigene Action, den generischen Stub unverändert als Settings-Action, dann die
Tabelle ausfüllen. Stand heute braucht ein Host dazu `hosts/<host>/spec.json`, einen handgeschriebenen
`host-section.cs` (Settings-Pfad, Log-Ordner, Cooldown-Zuordnung, `Host`-Klasse mit `OnSaved`/`Test`/
`Validate`/`ReadLogLines`, bei Slot Machine 400 Zeilen Engine-Kopie) und `tools/embed-spec.ps1`, das beides in
`stub/Stub.cs` spleißt; die Doku ist deutsch.

#### 7.1 Das Fenster wird im Spiel deklariert (DLL 0.9.0)

Die Describe-Antwort trägt neben `host`, `version`, `settingsFile`, `settings` ein `window`-Objekt mit dem
Inhalt der heutigen Spec (`title`, `releases`, `logo`, `logPrefix`, `pages`, `senders`, `samples`, `answers`,
`headerPills`, `log`, `help`, `about`, `width`, `height`). Der Host-Block bekommt dafür kleine Builder, damit
die Deklaration im C# lesbar bleibt und der Compiler Tippfehler findet:

```csharp
public static readonly UiWindow Window = new UiWindow("Blackjack Game", releases: "aaskjer/Blackjack-Game", logo: "https://…")
    .Pages(P("table", "Table", "🃏"), P("duel", "Duel", "⚔️"), P("stats", "Stats", "🏆", "leaderboard*", "statsTable", "resetLeaderboard"), "log", P("help", "How to play"), "about")
    .Log(folder: "Chat Games/Logs", files: "BJG-log*.txt", writeLogKey: "logToFile", columns: new[] { "Player|User", "Bet" }, results: new[] { "Win", "Loss", "Push" })
    .Cooldowns(C("dc2d4f17-…", "globalCooldown", "userCooldown"))
    .Pill("freeGame", "Free game on", "Free game off", tooltip: "…")
    .Help(H("Playing a hand", Cmd("!blackjack <bet>", "Deals in."), Note("…"), Link("Guide", "https://…")))
    .About(credits: "Responsible: aaskjer", links: new[] { Link("Discord", "https://…") });
```

Neu gegenüber der Spec: `log.files` (das Dateimuster, heute in `ReadLogLines` fest verdrahtet) und
`cooldowns` (Command-Id plus die Keys des globalen und des Nutzer-Cooldowns, heute die Tabelle im
Host-Abschnitt). `Entry.Show(specJson, …)` bleibt: ist `specJson` leer, nimmt die DLL `window` aus dem Schema;
eine mitgegebene Spec gewinnt weiterhin (die fünf bestehenden Stubs laufen unverändert weiter, der Umstieg ist je
Host möglich). `docs/Spec.md` geht in `docs/Schema.md` auf.

#### 7.2 Der generische Stub

`stub/Stub.cs` verliert die HOST-Marker und die `Spec`-Konstante. Am Kopf steht eine Zeile, die ein fremdes
Projekt anpasst: `GameActionName`. `SettingsPath` kommt aus `settingsFile` des Schemas (relativ zum
Streamer.bot-Ordner). Die `Host`-Klasse wird generisch, weil alles, was das Spiel weiß, über denselben Weg wie
`uiframework.describe` zurück in die Spiel-Action läuft; die hat ohnehin den Code und die Settings:

| Stub-Hook                  | Argument an die Spiel-Action                          | Antwort (nicht-persistente Globale `UiSchema.Var(...)`) |
|----------------------------|-------------------------------------------------------|---------------------------------------------------------|
| `OnSaved(settings)`        | `uiframework.saved` = Settings-JSON                   | keine; der Stub setzt vorher die `cooldowns` selbst      |
| `Test(name, settings)`     | `uiframework.test` = name, `uiframework.values` = JSON | Text oder JSON wie heute                                |
| `Validate(settings)`       | `uiframework.validate` = JSON                         | Liste der Probleme                                       |
| `ReadTable(key)`           | `uiframework.table` = key                             | JSON-Array                                               |
| `EmoteSources()`           | `uiframework.emotes` = 1                              | JSON                                                     |
| `ReadLogLines()`           | keins; der Stub liest `log.folder` + `log.files`      | Zeilen                                                   |

Der Host-Block bekommt dazu einen Verteiler, den die Spiel-Action als erstes aufruft:

```csharp
if (UiHost.Handle(args, CPH, ActionName, CurrentVersion, SettingsFile,
        test: (name, s) => name == "tryHand" ? TryHand(s) : null,
        validate: s => Problems(s),
        saved: s => SetupOverlay(s)))
    return true;
```

Jeder Delegat ist optional; ein Spiel ohne Test-Buttons gibt nur die drei Konstanten. Die Zweige müssen rein
bleiben (keine Chat-Ausgabe, keine Zustandsänderung), wie Describe heute. Laufzeit: das Fenster läuft auf einem
eigenen Thread, `RunAction` braucht Streamer.bots Thread, also stellt der Stub den Aufruf in die Job-Queue, die
`Pump` alle 100 ms leert, und wartet mit Timeout (5 s) auf die Globale. Für Buttons unmerklich; für `auto`- und
`live`-Actions (Odds bei Classic Chat Gamble, der Fairness-Slider) sind 100 ms Verzug in Ordnung. Ein Spiel, das
mehr braucht als den Verteiler, schreibt seinen Code in die Spiel-Action; die Settings-Action bleibt überall
dieselbe Datei. `tools/check-stub.ps1` prüft dann nur noch die eine Datei, `tools/embed-spec.ps1` entfällt.

Gewinn nebenbei: der Test-Spin der Slot Machine läuft mit der echten Engine statt mit ihrer Kopie, und bei jedem
Spiel verschwindet die zweite Stelle, die den Settings-Pfad, die Log-Dateien und die Command-Ids kennt.

#### 7.3 Fehler im Fenster statt nur im Log

Heute landet ein unbekanntes `kind`, ein doppelter Key, eine Option ohne `|`, ein `requires` auf einen fremden
Key oder eine Seite ohne Settings als `LogWarn` in Streamer.bot. `Entry.Prepare` sammelt diese Befunde und das
Fenster zeigt sie als gelbes Banner über der ersten Seite ("3 problems in the schema, see About"), die
About-Seite listet sie. Antwortet die Spiel-Action nicht auf Describe, zeigt der Stub einen Dialog mit Ursache
und Abhilfe (falscher Action-Name, alte Spielversion) statt einer Logzeile.

#### 7.4 Englische Doku, Beispielspiel, Quickstart

- `docs/Schema.md` (mit dem Inhalt von `Spec.md`), `Loader.md`, `Customize.md` auf Englisch; `PLAN.md` und
  `Phase0-Konventionen.md` bleiben deutsch (interne Planung).
- `docs/Getting-Started.md`: in zehn Minuten von null zum Fenster, mit Screenshots aus der Preview-EXE.
- `examples/DemoGame/`: `DemoGame.sb` (eine Spiel-Action mit drei Settings und einer Nachricht, die
  Settings-Action ist der generische Stub) plus README; die Beispiel-Action ist zugleich der Testfall für den
  Stub ohne Host-Code. `tools/check-stub.ps1` kompiliert beide.
- README: Abschnitt "Use it in your own project" mit den drei Schritten, Link auf Getting-Started.

#### 7.5 Umstellung der bestehenden Hosts

Ein Einmal-Werkzeug `tools/spec-to-window.ps1` schreibt aus `hosts/<host>/spec.json` den `Window`-Block als C#,
damit die Hilfe- und About-Texte nicht abgetippt werden. Je Host: `Window` in den Host-Block, die Logik der
`Host`-Klasse in die `uiframework.*`-Zweige der Spiel-Action, die GUI-Action durch den generischen Stub
ersetzen, Harness auf die neuen Argumente, Release. Reihenfolge nach Umfang des Host-Abschnitts: High-Low,
RhymeTime, Classic Chat Gamble, Slot Machine, Blackjack, Battleship. `hosts/<host>/schema.json` bleibt für
Preview und Tests (es trägt nun `window`); `spec.json` und `host-section.cs` werden gelöscht, zuletzt
`embed-spec.ps1`.

#### Reihenfolge und Aufwand

| Schritt                                           | Aufwand |
|---------------------------------------------------|---------|
| 7.1 Window im Schema, Builder, Tests              | 1 Tag   |
| 7.2 generischer Stub, `UiHost.Handle`, Demo-Spiel | 1½ Tage |
| 7.3 Banner und Describe-Dialog                    | ½ Tag   |
| Release 0.9.0 (beide Stub-Arten laufen)           |         |
| 7.4 Doku und Quickstart                           | 2 Tage  |
| 7.5 sechs Hosts umstellen, je Release             | 5 Tage  |

Entschieden (04.11.2026): (a) Fenster-Deklaration als C#-Builder im Host-Block; (b) keine Escape-Luke, der Stub
ist eine Datei für alle Projekte, jede Logik läuft über `uiframework.*` in der Spiel-Action; (c) der
Log-Auto-Refresh (FileSystemWatcher auf `log.folder`, der Button bleibt als Fallback) geht mit in 0.9.0.

Stand 04.11.2026: 7.1 bis 7.4 mit Release 0.9.0 erledigt (Window-Builder, generischer `stub/Stub.cs`,
`UiHost.Handle`, Banner und Dialog, Log-Watcher, englische Doku, `examples/DemoGame`, `tools/update-host-block.ps1`);
das Demo-`.sb` liegt bei (`tools/build-import.ps1` hält es aktuell). 7.5 begonnen: `tools/spec-to-window.ps1`
schreibt den Window-Block aus der Spec; High-Low ist umgestellt (Host-Block mit Window in `hosts/highlow/HostBlock.cs`,
Spiel-Action mit `UiHost.Handle` und `UiCurrency`, GUI-Action = `stub/Stub.cs`, `spec.json` und `host-section.cs`
gelöscht, `schema.json` schreibt der Testlauf des Spiels). Stand 05.11.2026: Classic Chat Gamble ebenso (v25.1.0,
mit `.Lang("de", …)`; die Antworten des alten Stubs sitzen als `WindowAnswers` neben dem Block im Spiel, der Test-
Harness spielt das Spiel über `RunAction`). Offen: RhymeTime, Slot Machine, Blackjack, Battleship; zuletzt
`embed-spec.ps1` und `Stub.spec.cs`.

## 9. Tests und Werkzeuge

- Framework: xUnit-Projekt, läuft in `ci.yml`. Resolve (alle vier Schichten), Layout-Merge, Store-Migration,
  Schema aus JSON, Conventions vollständig gegen `keys.json`.
- Preview-EXE: Fenster mit Beispiel-Schema öffnen, Screenshots pro Seite in hell/dunkel für die Docs; ersetzt
  `gui-shots.ps1` der einzelnen Spiele.
- Host-Projekte: ihr bestehendes `check-all.ps1` behält Import-Prüfung und Verhaltenstests; `check-settings.ps1`
  entfällt; neu: Stub-Drift-Check gegen `stub/Loader.cs` und Schema-Roundtrip (Describe liefert jeden Key, den
  das Spiel liest).

## 10. Risiken

| Risiko | Umgang |
|---|---|
| Nutzer misstrauen Selbst-Update | README, Versionsanzeige im About, Schalter, Hash über HTTPS, Quellcode offen |
| GitHub nicht erreichbar | Cache, kurzer Timeout, Fenster öffnet immer |
| Streamer.bot unter Program Files, kein Schreibrecht | Fallback `%LOCALAPPDATA%` |
| Zwei Hosts öffnen gleichzeitig ein Fenster | Claim in der DLL, eine Application-Instanz, ein Fenster pro Host |
| Sonderseiten sehen anders aus | `IPageTools` liefert dieselben Controls; Review beim Umstellen |
| Schema und Spec nennen verschiedene Keys | Resolve loggt Keys ohne Seite und Spec-Felder ohne Schema; Statuszeile zeigt es |
| Breaking Change im Framework | Major-Version pinnen, `minHostStub`, alte v1-Linie bleibt stehen |
| Layout-Datei zeigt auf gelöschte Seite | ignorieren, beim Speichern bereinigen |

## 11. Entscheidungen (31.10.2026)

1. **Sprache:** englisch als Basis, Sprachdateien wie bei TwitchSentry (`conventions/lang/<code>.json`, Fallback
   `en.json`). Die Sprache ist eine Framework-Einstellung in der Layout-Datei, nicht je Host.
2. **Textvorlagen:** Schema-Kind `template` mit `placeholders: [...]`. Das Framework zeigt die Platzhalter als
   Chips unter dem Textfeld, Klick fügt `{name}` an der Cursorposition ein. Bekannte Platzhalter (`user`,
   `pointsName`, `amount`, …) bekommen Hilfetexte aus den Konventionen.
3. **Reihenfolge:** innerhalb einer Seite die Deklarationsreihenfolge des Schemas; die Spec überschreibt per
   `fields`, das Layout per `order`.
4. **Keys vereinheitlichen:** ein kanonischer, camelCase-Name je Bedeutung, an Streamer.bots eigenen Namen
   orientiert (`useBot`, `sendAction`, `globalCooldown`, `userCooldown`, `userName`-Stil). Alte Namen stehen als
   `aliases` in `conventions/keys.json`; der Store liest Aliase beim Laden und schreibt kanonisch zurück, so
   wandert jede Settings-Datei beim ersten Speichern ohne Nutzerzutun. Ergebnis von Phase 0:
   `docs/Phase0-Konventionen.md`.
5. **Framework-Einstellungen wandern aus den Hosts heraus:** `darkMode`, `egg` (Rainbow) und `language` sind
   Aussehen des Fensters, nicht Spiel. Sie liegen künftig in `uiframework.layout.json` und gelten für alle Hosts.
   Der Store übernimmt den Wert aus der ersten Host-Datei, die ihn noch trägt, und entfernt ihn dort.
