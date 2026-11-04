# Customise mode and the layout file

The pen button at the bottom left turns the customise mode on. In it the window can be rearranged without
touching a script; every change lands at once in `uiframework.layout.json` next to the settings file and
survives every import of the host.

## In the mode

- **Fields** have a handle `⋮⋮`. Dragging one and dropping it anywhere on the page sorts: over another field it
  takes that field's place (behind it when coming from above, in front when coming from below), over a heading
  or a gap it lands between, under the last field at the end; at the edge the page scrolls along slowly.
  Dropping on a page in the rail moves it there. Right-click: *Rename*, *Hide* / *Show*, *Move to page*,
  *Reset this field*. Values are not editable in the mode.
- **Pages** in the rail: dragging sorts the top group; right-click: *Rename*, *Change icon* (an emoji or a
  conventions name like `gear`, `dice`, `coins`), *Hide* / *Show*, *Reset this page*. `About` cannot be hidden;
  `Help` and `About` stay at the bottom.
- **Hidden things** are shown half-transparent with the note `hidden` in the mode, so they can be shown again.
  A hidden field keeps its value in the settings file.
- **The bar at the top:** the accent colour (blue buttons, switches that are on, the active page in the rail,
  links, the bar itself; for every UIFramework window of this Streamer.bot; taken once the picker closes or the
  typing pauses), the language (`en`, `de`), *Reset layout* (everything but the theme), *Export* and *Import* of
  the settings (see docs/Schema.md), *Open file*, *Done*.
- **Messages:** the templates can be sorted, renamed and hidden on their page; they cannot be dragged to
  another page.

After every change the window's content is rebuilt in place, on the same page and at the same scroll position;
the window itself stays where it is.

## The file

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

| Key                            | Meaning                                                                      |
|--------------------------------|------------------------------------------------------------------------------|
| `theme.dark`                   | dark (`true`) or light; missing: dark                                        |
| `theme.rainbow`                | rainbow mode, for one open only                                              |
| `theme.accent`                 | the accent colour `#rrggbb`; empty: the default blue                         |
| `language`                     | `en` or `de`: the window's own texts (buttons, status line, dialogs, menus, the Messages, Log and About pages' chrome) and the conventions' labels and help texts; missing: `en`. What the game declares (its labels and help, the help and about pages) stays as declared |
| `autoUpdate`                   | `false`: the loader no longer asks about new versions                        |
| `skipVersion`                  | set by the update dialog's *Cancel*: this version is not offered again       |
| `frameworkAvailable`           | set by the update dialog's *No*: the window shows this version as a pill in the header until it is taken or skipped |
| `pages.<id>.title`, `.icon`    | how the page is shown                                                        |
| `pages.<id>.order`             | the order in the top group; pages with an `order` come first                 |
| `pages.<id>.hidden`            | the page is gone (except `about`)                                            |
| `fields.<key>.page`            | which page the field belongs to                                              |
| `fields.<key>.order`           | the order within the page                                                    |
| `fields.<key>.label`, `.section` | the label, the section heading                                             |
| `fields.<key>.hidden`          | the field is gone; the value stays in the settings file                      |

The layout file is the fourth and last layer over the conventions, the schema and the window declaration. Keys
the schema no longer knows are ignored. Editing by hand is fine; the window reads the file on every open.
