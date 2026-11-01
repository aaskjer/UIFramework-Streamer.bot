# Spec

Die Spec beschreibt, wie das Fenster eines Hosts ausgeliefert aussieht: Titel, Logo, Release-Repo, Seiten
und ihre Reihenfolge, was auf welcher Seite steht. Sie liegt als JSON-String im Stub (`tools\embed-spec.ps1`
schreibt `hosts/<host>/spec.json` hinein). Was sie nicht nennt, füllen Schema und Konventionen.

```json
{
  "title": "Blackjack Game",
  "version": "v16.3.1",
  "releases": "aaskjer/Blackjack-Game",
  "logo": "https://…/logo.png",
  "logPrefix": "[Blackjack Game/Settings]",
  "pages": [
    { "id": "table", "title": "Table", "icon": "🃏" },
    { "id": "stats", "title": "Stats", "icon": "🏆", "fields": ["leaderboard*", "resetLeaderboard"] },
    "messages", "log", { "id": "help", "title": "How to play" }, "about"
  ],
  "overrides": { "useBot": { "page": "table" }, "egg": { "hidden": true } },
  "senders": { "useBot": "useBot", "cooldownUseBot": "cooldownUseBot", "cooldownGroup": "Cooldown", "numberSeparator": "numberSeparator" },
  "samples": { "cards": "🂫(10) 🂺(10)", "0": "alice" },
  "headerPills": [ { "key": "freeGame", "on": "Free game on", "off": "Free game off", "tooltip": "…" } ],
  "log": { "folder": "Chat Games/Logs", "writeLogKey": "logToFile", "columns": [ { "header": "Player", "field": "User" }, "Bet" ], "results": ["Win", "Loss"] },
  "help": [ { "heading": "Playing a hand", "lines": [ "a note", { "command": "!blackjack <bet>", "what": "Deals in." }, { "link": "Guide", "url": "https://…" } ] } ],
  "about": { "credits": "Responsible: aaskjer", "sections": [ … ], "links": [ { "text": "Discord", "url": "https://…" } ] }
}
```

## Felder

| Feld          | Bedeutung                                                                                             |
|---------------|-------------------------------------------------------------------------------------------------------|
| `title`       | Fenstertitel und About-Überschrift                                                                    |
| `version`     | Release-Tag des Hosts; fehlt es, gilt die Version aus dem Schema                                      |
| `releases`    | `owner/repo` auf GitHub: Repository-Link, Releases-Link, Update-Prüfung                               |
| `logo`        | URL des Logos (Fenster-Icon und About)                                                                |
| `logPrefix`   | Präfix jeder Logzeile des Frameworks für diesen Host                                                  |
| `language`    | Sprachcode für Labels aus den Konventionen; sonst Layout-Datei, sonst `en`                             |
| `pages`       | Reihenfolge der Seiten. Ein String ist eine Standardseite mit ihrer id. Ein Objekt hat `id`, optional `title`, `icon` (Emoji oder Konventionsname wie `gear`), `fields` (Keys oder Muster mit `*`), `custom` (Host baut die Seite), `bottom` |
| `overrides`   | Je Key: `page`, `label`, `help`, `section`, `hidden`, `order`                                         |
| `senders`     | Die Keys der Sender-Schalter der Nachrichten-Seite; `null` blendet einen aus. Keys, die das Schema nicht kennt, erscheinen nicht |
| `samples`     | Feste Beispielwerte für Platzhalter der Chat-Vorschau; Live-Werte (`pointsName`, `amount`, `min`, `max`) ergänzt das Framework |
| `headerPills` | Pills im Kopf: `key` eines bool, Text für `on`/`off`, `tooltip`                                        |
| `log`         | `folder`, `writeLogKey`, `writeLogLabel`, `writeLogHelp`, `columns`, `results`, `platforms`, `emptyText` |
| `help`        | Abschnitte für die Hilfe-Seite: `heading`, `lines` mit `note`, `command`+`what` oder `link`+`url`      |
| `about`       | `credits`, `sections` (wie `help`), `links`, `streamerBotLink`, `authorLink`                           |
| `width`, `height` | Fenstergröße                                                                                      |

## Wie Seiten entstehen

1. Die Seiten aus `pages` in genau dieser Reihenfolge. `fields` beansprucht Settings, Muster wie `overlay*`
   greifen in Schema-Reihenfolge. Ein Muster zieht keine Nachrichtenvorlage von der Nachrichten-Seite weg,
   ein voller Key schon.
2. Jedes Setting, das keine Seite beansprucht hat, geht auf seine Seite (Spec-Override, dann Schema, dann
   Konvention, dann `game`). Fehlt die Seite in `pages`, wird sie angelegt: Titel aus den Sprachdateien,
   Icon aus den Konventionen, einsortiert nach Konventions-Reihenfolge vor `log`, `help`, `about`.
3. Die Nachrichten-Seite erscheint, sobald es Vorlagen gibt; `about` immer. `help` und `about` bilden die
   Gruppe am unteren Ende der Leiste (`bottom`), getrennt von den Einstellungsseiten.
4. Innerhalb einer Seite: Deklarationsreihenfolge des Schemas (`order` im Override verschiebt), eine
   Überschrift, wo `section` wechselt; Zahlen zu dritt in einer Zeile, Schalter zu zweit, der Rest einzeln.
5. Settings, die eine Spec auf eine Standardseite legt (etwa einen Schalter auf `log`), stehen oben über dem
   Inhalt der Seite.

Die vierte Schicht, die Layout-Datei des Nutzers (`docs/Customize.md`), überschreibt Titel, Icons, Reihenfolge,
Zuordnung und Sichtbarkeit zuletzt.
