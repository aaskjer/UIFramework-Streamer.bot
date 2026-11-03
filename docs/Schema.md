# Schema

Das Schema ist die Liste der Settings, die ein Host kennt. Der Host hält sie als Tabelle, liest seine Werte
darüber und gibt sie auf Anfrage (`uiframework.describe`) als JSON aus. Werte stehen nie im Schema, nur in der
Settings-Datei.

```json
{
  "host": "blackjack",
  "version": "2.3.0",
  "settingsFile": "Blackjack Game/BJG-Settings.json",
  "settings": [ { "key": "...", "default": ..., "kind": "...", ... } ]
}
```

## Felder eines Settings

| Feld           | Pflicht | Bedeutung                                                                                  |
|----------------|---------|--------------------------------------------------------------------------------------------|
| `key`          | ja      | camelCase, kanonisch (siehe `conventions/keys.json`). Eigene Keys frei, aber camelCase     |
| `default`      | ja      | JSON-Wert. Bestimmt ohne `kind` den Typ                                                    |
| `kind`         | nein    | eines der Kinds unten; ohne Angabe aus `default` abgeleitet                                |
| `page`         | nein    | Seiten-id; Konventionen oder Spec setzen sie sonst, Rest landet auf `game`                 |
| `label`        | nein    | englischer Anzeigename; bei bekannten Keys aus den Konventionen                            |
| `help`         | nein    | ein Satz unter dem Feld                                                                    |
| `min`, `max`   | nein    | für `int`, `double`, `duration`, `percent`                                                 |
| `step`         | nein    | Schrittweite für `double`                                                                  |
| `unit`         | nein    | Anzeige hinter dem Feld (`s`, `%`, `px`)                                                   |
| `options`      | nein    | Liste für `options`: `["a", "b"]` oder `[{"value": "a", "label": "A"}]`                    |
| `placeholders` | nein    | für `template`: Namen ohne Klammern, werden als Chips gezeigt                              |
| `section`      | nein    | Überschrift; wo sie wechselt, beginnt ein neuer Abschnitt auf der Seite                     |
| `group`        | nein    | für `template`: die Gruppe im Nachrichten-Picker                                           |
| `factor`       | nein    | für `percent`: gespeicherter Wert = getippt × Faktor (0.01 speichert einen Bruch; Standard 1) |
| `unlimitedAtZero` | nein | für `int`/`long`: 0 im Feld heißt "keine Grenze" und wird als MaxValue gespeichert         |
| `aliases`      | nein    | alte Keys; der Store liest sie und schreibt den kanonischen Key zurück. Ein Alias mit Punkt (`PlatformEmotes.twitch`) holt den Wert aus einem Objekt der alten Datei; das Objekt fällt weg, sobald es leer ist |
| `aliasFactors` | nein    | je Alias ein Faktor für den Wert beim Umzug (Bruch zu Prozent: 100)                        |
| `hidden`       | nein    | true: hat kein Feld, bleibt aber in der Datei                                              |
| `confirm`      | nein    | für `action`: Frage (Ja/Nein), bevor der Host gerufen wird                                 |
| `requires`     | nein    | Key eines `bool`; Feld ist ausgegraut, solange der false ist                               |
| `advanced`     | nein    | true: nur sichtbar, wenn der Nutzer "Erweitert" einschaltet                                |
| `readonly`     | nein    | true: Anzeige, kein Eingabefeld (Statuswerte, die der Host schreibt)                       |
| `transient`    | nein    | true: wird angezeigt, aber nie gespeichert und nie als Änderung gezählt; nur ein Live-Abzug für den Host trägt den Wert (Eingaben eines Probe-Buttons) |
| `live`         | nein    | für `action`: `Test` bekommt die Werte, wie sie im Fenster stehen, ungespeicherte und flüchtige eingeschlossen |
| `panel`        | nein    | für `action`: die Antwort steht in einem Feld unter dem Button statt in der Fußzeile       |
| `auto`         | nein    | für `action`: kein Button; der Host wird beim Anzeigen der Seite und kurz nach jeder Änderung im Fenster gefragt (Quoten, die mitlaufen) |
| `columns`      | für `list`, `table` | `[{key, kind, label}]`; Kinds `string`, `text` (mehrzeilig, doppelt breit), `int`, `double`, `bool`, `emote` (Textfeld mit Bild daneben; `platform` sagt `twitch`, `youtube` oder `kick`: Kick aus `[emote:ID:name]`, Twitch per Name aus Twitch/7TV/BTTV/FFZ mit dem, was `EmoteSources()` des Hosts liefert, YouTube zeigt das Emoji selbst) |
| `file`         | nein    | Datei neben der Settings-Datei, in der dieser Wert liegt statt in ihr (Wortbank, Fragenliste); relativ oder absolut |
| `field`        | nein    | mit `file`: die Property in dieser Datei, die den Wert hält; ohne `field` ist die Datei selbst der Wert |

## Kinds

| Kind        | Default-Typ | Control                                    | Hinweise                                              |
|-------------|-------------|--------------------------------------------|-------------------------------------------------------|
| `bool`      | bool        | Toggle                                     |                                                       |
| `int`       | int         | Zahlenfeld; mit `options` ein Dropdown     | `min`, `max`, `unit`, `unlimitedAtZero`                |
| `double`    | double      | Zahlenfeld; mit `options` ein Dropdown     | `min`, `max`, `step`; Punkt oder Komma                |
| `percent`   | int/double  | Zahlenfeld 0 bis 100 mit `%`               | gespeichert als Zahl, nicht als Bruch                 |
| `duration`  | int         | Zahlenfeld mit Einheit                     | gespeichert in Sekunden; Anzeige wählt s/min/h        |
| `string`    | string      | einzeiliges Textfeld                       |                                                       |
| `text`      | string      | mehrzeiliges Textfeld                      |                                                       |
| `template`  | string      | mehrzeilig + Platzhalter-Chips + Vorschau  | `placeholders`; Vorschau ersetzt mit Beispielwerten   |
| `options`   | string      | Dropdown                                   | `options` Pflicht                                     |
| `namelist`  | string      | Chips, Enter fügt hinzu                    | gespeichert kommagetrennt (kompatibel zu heute)       |
| `grouplist` | string      | Chips mit Vorschlägen aus Streamer.bot-Gruppen | gespeichert kommagetrennt                         |
| `path`      | string      | Textfeld + Ordner-/Datei-Dialog            | relativ zum Streamer.bot-Ordner erlaubt               |
| `colour`    | string      | Farbfeld + Picker                          | `#rrggbb` oder `#aarrggbb`                            |
| `emote`     | string      | Textfeld mit Emote-Vorschau                | Plattform-Emotes als Text                             |
| `list`      | array       | Zeilen-Editor: je Zeile eine Zelle pro Spalte, Entfernen, *Add row*, *Restore defaults* | `columns` Pflicht; leere Zeilen fallen beim Speichern weg; mit `file`/`field` in eigener Datei |
| `table`     | array       | Tabelle, nur Anzeige                       | für Statistiken, die der Host schreibt                |
| `action`    | –           | Button                                     | kein Wert; ruft `Test(key, settingsJson)` am Host; Rückgabe = Status, mit `panel` ein Feld unter dem Button, mit `live` aus den aktuellen Werten, mit `auto` ohne Button und von selbst |

`table` und `action` haben keinen Wert in der Datei. `table` holt seine Zeilen mit `ReadTable(key)` vom Host als
JSON-Array von Objekten, deren Felder die `columns` nennen.

**Eigene Datei (`file`, `field`):** der Wert wird beim Öffnen aus `<Ordner der Settings-Datei>\<file>` gelesen
(Property `field`, oder die Datei selbst) und beim Speichern dorthin geschrieben, atomar mit `.bak`; alles andere
in der Datei bleibt stehen. In die Settings-Datei kommt der Key nie; *Reset all* setzt nur die Settings-Datei zurück
und lässt die eigene Datei stehen (ihr Editor hat *Restore defaults*). Fehlt die Datei, zeigt das Fenster den
`default`; so bleibt die Datei, die das Spiel ohnehin liest (`RT-wordbank.json`), das einzige Exemplar.

Ableitung ohne `kind`: bool → `bool`, ganze Zahl → `int`, Kommazahl → `double`, string → `string`, Array → `list`
(braucht dann `columns`). Keys aus den Konventionen bringen ihr Kind mit.

## Beispiel

```json
{ "key": "reasonInsufficientFunds", "default": "{user}, you only have {currentPoints} {pointsName}.",
  "kind": "template", "page": "messages", "placeholders": ["user", "currentPoints", "pointsName", "amount"] }
```

```json
{ "key": "wordlist", "default": [], "kind": "list", "page": "words",
  "columns": [ { "key": "word", "kind": "string", "label": "Word" },
               { "key": "category", "kind": "string", "label": "Category" },
               { "key": "hint", "kind": "text", "label": "Hint" } ] }
```

## Host-Seite (C#)

```csharp
static readonly Setting[] Schema =
{
    S("useBot", true),
    S("globalCooldown", 10),
    S("maxBet", 1000, page: "game", min: 1, help: "Per round and player."),
    S("reasonInsufficientFunds", "{user}, you only have {currentPoints} {pointsName}.",
      kind: "template", placeholders: new[] { "user", "currentPoints", "pointsName", "amount" }),
};

bool UseBot => Get<bool>("useBot");
```

Die fertige Fassung dieses Blocks ist `stub/HostSchema.cs`: `UiSchema.Table`, `UiSchema.ToJson(...)` als
Antwort auf `uiframework.describe`, und `UiSettings.Load(path)` mit `Bool`, `Int`, `Long`, `Double`, `String`,
`Ceiling`, `Names`, die Aliase tolerieren. Er kompiliert ohne Referenz auf die DLL in jeder Streamer.bot-Aktion
(`tools\check-stub.ps1`).
