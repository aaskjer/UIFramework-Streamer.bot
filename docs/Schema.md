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
| `requires`     | nein    | Key eines `bool`; Feld ist ausgegraut, solange der false ist                               |
| `advanced`     | nein    | true: nur sichtbar, wenn der Nutzer "Erweitert" einschaltet                                |
| `readonly`     | nein    | true: Anzeige, kein Eingabefeld (Statuswerte, die der Host schreibt)                       |

## Kinds

| Kind        | Default-Typ | Control                                    | Hinweise                                              |
|-------------|-------------|--------------------------------------------|-------------------------------------------------------|
| `bool`      | bool        | Toggle                                     |                                                       |
| `int`       | int         | Zahlenfeld mit Spinner                     | `min`, `max`, `unit`                                  |
| `double`    | double      | Zahlenfeld                                 | `min`, `max`, `step`; Dezimaltrenner nach Systemkultur |
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
| `list`      | array       | Tabelle mit Zeilen-Editor                  | `columns: [{key, kind, label}]` Pflicht               |
| `table`     | array       | Tabelle, nur Anzeige                       | für Statistiken, die der Host schreibt                |
| `action`    | –           | Button                                     | kein Wert; ruft `hooks.Test(key)`; Rückgabe = Status  |

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

`Get<T>` liest aus der geladenen Settings-Datei, nimmt den Default aus `Schema`, kennt die Aliase aus den
Konventionen. `Schema.ToJson()` ist die Antwort auf `uiframework.describe`.
