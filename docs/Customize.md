# Anpassen-Modus und Layout-Datei

Der Stift-Knopf unten links schaltet den Anpassen-Modus ein. Darin lässt sich das Fenster umbauen, ohne ein
Script anzufassen; jede Änderung landet sofort in `uiframework.layout.json` neben der Settings-Datei und
überlebt jeden Import des Hosts.

## Im Modus

- **Felder** haben einen Griff `⋮⋮`. Ziehen und irgendwo auf der Seite loslassen sortiert: über der oberen Hälfte
  eines Felds landet es davor, über der unteren dahinter, unter dem letzten Feld ans Ende; nahe am Rand scrollt die
  Seite mit. Ziehen auf eine Seite in der Leiste verschiebt dorthin. Rechtsklick: *Rename*, *Hide* / *Show*,
  *Move to page*, *Reset this field*. Werte sind im Modus nicht editierbar.
- **Seiten** in der Leiste: Ziehen sortiert die obere Gruppe; Rechtsklick: *Rename*, *Change icon* (Emoji oder
  Konventionsname wie `gear`, `dice`, `coins`), *Hide* / *Show*, *Reset this page*. `About` lässt sich nicht
  ausblenden; `Help` und `About` bleiben unten.
- **Ausgeblendetes** wird im Modus halbtransparent mit dem Vermerk `hidden` gezeigt, damit es sich wieder
  einblenden lässt. Ein ausgeblendetes Feld behält seinen Wert in der Settings-Datei.
- **Leiste oben:** Akzentfarbe (blaue Knöpfe, eingeschaltete Schalter, aktive Seite in der Leiste, Links, die
  Leiste selbst; gilt für jedes UIFramework-Fenster dieses Streamer.bot; übernommen, sobald der Picker zu ist oder
  das Tippen pausiert), Sprache (`en`, `de`), *Reset layout* (alles außer Theme), *Open file*, *Done*.

- **Messages:** die Vorlagen lassen sich auf ihrer Seite umsortieren, umbenennen und ausblenden; auf eine andere
  Seite ziehen lassen sie sich nicht.

Nach jeder Änderung baut sich der Inhalt des Fensters an Ort und Stelle neu auf, auf derselben Seite und
an derselben Scrollposition; das Fenster selbst bleibt, wo es ist.

## Die Datei

```json
{
  "theme": { "dark": true, "rainbow": false, "accent": "#7C3AED" },
  "language": "de",
  "autoUpdate": true,
  "pages": {
    "table": { "title": "Tisch", "icon": "cards", "order": 10 },
    "duel":  { "hidden": true }
  },
  "fields": {
    "useBot":         { "page": "table", "order": 0, "label": "Bot sends" },
    "dealerStandsOn": { "hidden": true },
    "numDecks":       { "page": "house", "section": "Shoe" }
  }
}
```

| Schlüssel                     | Bedeutung                                                                  |
|-------------------------------|----------------------------------------------------------------------------|
| `theme.dark`                  | dunkel (`true`) oder hell; fehlt: dunkel                                   |
| `theme.rainbow`               | Regenbogen-Modus, nur für ein Öffnen                                       |
| `theme.accent`                | Akzentfarbe `#rrggbb`; leer: Standardblau                                  |
| `language`                    | Sprachcode für Labels und Hilfetexte der Konventionen; fehlt: `en`         |
| `autoUpdate`                  | `false`: der Loader fragt nicht mehr nach neuen Versionen                   |
| `skipVersion`                 | vom Update-Dialog mit *Abbrechen* gesetzt: diese Version wird nicht mehr angeboten |
| `frameworkAvailable`          | vom Update-Dialog mit *Nein* gesetzt: diese Version zeigt das Fenster als Pille im Kopf, bis sie genommen oder übersprungen ist |
| `pages.<id>.title`, `.icon`   | Anzeige der Seite                                                          |
| `pages.<id>.order`            | Reihenfolge in der oberen Gruppe; Seiten mit `order` kommen zuerst         |
| `pages.<id>.hidden`           | Seite weg (außer `about`)                                                  |
| `fields.<key>.page`           | auf welche Seite das Feld gehört                                           |
| `fields.<key>.order`          | Reihenfolge innerhalb der Seite                                            |
| `fields.<key>.label`, `.section` | Beschriftung, Abschnittsüberschrift                                     |
| `fields.<key>.hidden`         | Feld weg; der Wert bleibt in der Settings-Datei                            |

Die Layout-Datei ist die vierte und letzte Schicht über Konventionen, Schema und Spec. Keys, die das Schema nicht
mehr kennt, werden ignoriert. Hand-Edits sind erlaubt; das Fenster liest die Datei bei jedem Öffnen.
