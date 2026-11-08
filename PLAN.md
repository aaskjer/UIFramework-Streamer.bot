# UIFramework for streamer.bot: was noch offen ist

Stand 08.11.2026.

**Erledigt:** Die Phasen 0 bis 7 des Umbauplans vom 31.10. sind abgeschlossen.
- Das Framework ist stabil: 1.0.0 vom 07.11., 1.1.0 bis 1.4.0 vom 08.11. (1.4.0: ein Update-Hinweis pro Start von Streamer.bot, im Chat oder als Windows-Benachrichtigung).
- Alle acht Spiele laufen auf dem generischen Stub: Battleship v2.2.0, Blackjack v17.3.0, Classic Chat Gamble v25.1.0, Hangman v13.1.1, High-Low v2.2.0, RhymeTime v2.3.0, Slot Machine v16.3.0, Trivia v9.1.0.
- Der Praxistest in einer frischen Streamer.bot-Instanz ist mehrfach bestanden: DLL beim ersten Öffnen, Update, Start ohne Netz.

Wie das Framework gebaut ist und benutzt wird, steht nicht mehr hier, sondern in der Doku:
- [docs/Getting-Started.md](docs/Getting-Started.md)
- [docs/Schema.md](docs/Schema.md)
- [docs/Loader.md](docs/Loader.md)
- [docs/Customize.md](docs/Customize.md)
- [docs/Conventions.md](docs/Conventions.md)

Die Konventionen aus Phase 0 stehen in [docs/Phase0-Konventionen.md](docs/Phase0-Konventionen.md).

## Grundsätze, die weiter gelten

1. **Eine Datei als Settings-Action** (`stub/Stub.cs`) für alle Projekte. Was ein Projekt kann, läuft über `UiHost.Handle` in seiner eigenen Action.
2. **Englisch als Basis.** Die Sprachdateien (`conventions/lang/<code>.json`) und `Lang` je Spiel übersetzen. Die Sprache ist eine Einstellung der Layout-Datei.
3. **Kanonische Keys in camelCase**, alte Namen als `aliases`. Der Store liest die alten Namen und schreibt die kanonischen zurück. Ausnahme: Projekte, deren Datei mehrere Actions lesen (siehe 8.0).
4. **Was der Nutzer am Fenster ändert, liegt in `uiframework.layout.json`** und übersteht jeden Import.
5. **Kein Breaking Change für die Spiele.** Neue Optionen sind optional. Eine ältere DLL zeigt Unbekanntes als Problem hinter dem roten **i**, statt abzustürzen.

## Offen im Framework und in den Spielen

1. **Spanisch für die Spiele.** Die eigenen Texte des Frameworks sind vollständig spanisch (`es.json`: alle Einträge von `en`/`de`). Die Spaltenköpfe der Listen und Log-Seiten sind es seit 1.1.0. Die Spiele selbst haben aber nur `.Lang("de", …)`. Im spanischen Fenster bleiben daher Labels, Hilfen, Seiten- und Abschnittsnamen englisch, soweit sie nicht in den Konventionen stehen.
   - Umfang je Spiel als `K` / `Pg` / `Tx`:
     - Battleship 166 / 3 / 26
     - Blackjack 220 / 3 / 30
     - Classic Chat Gamble 48 / 0 / 16
     - Hangman 155 / 2 / 61
     - High-Low 30 / 0 / 14
     - RhymeTime 56 / 0 / 23
     - Slot Machine 62 / 1 / 14
     - Trivia 207 / 3 / 68
   - Vorgehen: je Spiel ein `.Lang("es", …)` mit denselben Einträgen wie `de`, aus der deutschen Liste per Skript als Gerüst erzeugt und dann übersetzt. Je Spiel ein Patch-Release.
2. **Ergebnisfarben der Log-Seite deklarierbar machen.**
   - `WindowSpec.ResultStyle` und `Outcome` sind C#-Delegaten. Aus dem Schema eines Spiels sind sie nicht erreichbar.
   - `LogPage.DefaultStyle` rät deshalb nach Stichwörtern. Ein Hangman-`MISS-LTTR` oder `GAME-OVER` erscheint in der Akzentfarbe statt gedämpft.
   - Vorschlag: `.Log(…, styles: new[] { "GAME-OVER|muted", "WIN-*|success" }, outcome: new[] { "DUEL WIN|WIN" })`, im Schema `log.styles` und `log.outcome`.
3. **Engine-Tests für Hangman.** `tools\run-engine-tests.ps1` überspringt sich, weil `Hangman-Game.cs` keine `ENGINE START`-/`ENGINE END`-Marker hat. Die Rundenlogik ist nur über Kompilieren und den Fenstertest abgedeckt.
4. **Befunde des Audits vom 08.11.** 1.2.0 behebt die klaren Fehler; was offen ist, steht unter [Audit](#audit-vom-08112026).

## Phase 8: Nicht-Spiel-Projekte

Stand 08.11.2026, gegen den Code geprüft. Korrekturen zur Bewertung vom 07.11.:
- BsA hat 13 Seiten und 152 Properties (148 Settings und 4 Fensterschalter), nicht 14 und 158.
- SuTo schickt die Chat-Bestätigung nicht aus dem Code, sondern aus Send-Message-Sub-Actions in einem Switch hinter dem Code.
- GameCheck hat 49 Settings (26 `free`, 23 `sales`).
- ChatCalendar hat keine Set Arguments, liest aber wie GameCheck nach der Regel "ein gleichnamiges Argument gewinnt".

`Discord.Bot` ruht weiter: 3 409 Zeilen GUI, überschneidet sich mit BsA, nach BsA neu bewerten. `TwitchSentry` bleibt außen vor.

### 8.0 Regeln für alle sechs

- **Ein Import überschreibt Set Arguments.** Streamer.bot ersetzt beim Import die Action samt Sub-Actions (AutoBotCheck, Setup-Guide: "The import resets the group names"). Was heute in Set Arguments steht, geht bei jedem Update verloren. Die Umstellung holt diese Werte einmal zurück (F2) und legt sie danach in die Settings-Datei, die kein Import anfasst.
- **Ein Set Argument gewinnt weiter** (F1). GameCheck und ChatCalendar dokumentieren das so. Wer nur den Code einfügt, behält seine Werte, und Werte je Action bleiben möglich (dryRun nur für ISDS). Damit entfällt die Lücke "Werte je Action". Wo Actions wirklich verschiedene Werte brauchen, bekommen die Keys einen Präfix.
- **Projekte mit mehreren Actions:** Genau eine Action trägt `UiHost.Handle`. Das ist eine eigene `… Settings Host`-Action, wenn die Arbeits-Actions dauerhaft laufen (BsA-Presence) oder einzeln gelöscht werden dürfen (SuTo). Die übrigen Actions lesen die flache Datei selbst (F4).
- **Keine Key-Umbenennung in Projekten mit mehreren Lesern** (ChatCalendar, BsA). Der Store schreibt beim Speichern den kanonischen Key zurück, die übrigen Actions fänden den alten nicht mehr. Deshalb kommt `sendAsMe` auch nicht als Alias in `conventions/keys.json`.
- **Langläufer** (Webpost, Download, Prüflauf) laufen nie im `test`-Zweig, wegen des 5-s-Budgets. Der Zweig startet `CPH.RunAction(x, false)` und antwortet "gestartet"; ein `auto`-Panel mit `every` (F8) zeigt danach das Ergebnis.
- **`UiHost.Handle` steht vor jedem Lock, Lauf-Check und Timer der Action** (`RunGate` in GameCheck, `running` in AutoBotCheck).
- Die bestehende Settings-Action behält Id und Trigger und bekommt `stub/Stub.cs` als Code.
- Jedes Projekt bekommt eine Tour, `tools\StubHarness.cs` und `tools\run-window-tests.ps1` nach dem Muster von Hangman, und `hosts/<id>/schema.json` im Framework-Repo.
- Vor jedem Versionssprung des Frameworks `git fetch` und rebase, weil parallele Sessions ebenfalls releasen.

### 8.1 OBS Notes (Ordner `OBS Notepad`, v1.0.2 → v2.0.0) – S, 2 Tage

**Heute:**
- Eine Action `OBS Notes` (`OBS-Notes.cs`, 1 128 Zeilen). Sie hat vier Befehle über Command-Ids und einen Test-Trigger, dessen Trigger-Variable `AddOBSOverlayTo` die OBS-Verbindung nennt und die Browserquelle anlegt.
- Keine GUI, keine Settings-Datei. 7 Set Arguments in der Gruppe "Settings (Important to check!)": `useBot`, `sendAsMe`, `language` (DE/EN), `hideAfterMinutes` (0–1440, mit Dezimalstellen), `colorfulNotes`, `showGameName`, `layout` (vertical/horizontal).
- Gelesen werden sie aus `args` über `ArgBool`, `ArgNumber`, `ReadEnglish` und `ReadHorizontal`; die nehmen auch ja/nein und h…/v….
- Daten: `OBS Notes\<Kategorie>-Notes.json` plus `.bak` als Zeilenprotokoll, dazu `notes_state.json` fürs Overlay.

**Abbildung:**
- `SettingsFile = "OBS Notes/OBSNotes-Settings.json"`, Host `obsnotes`.
- Tabelle: `useBot` (Konvention), `sendAction` (Konvention, `aliases: sendAsMe`), `language` (`DE|Deutsch`, `EN|English`).
- Seite `overlay`: `layout`, `colorfulNotes`, `showGameName`, `hideAfterMinutes`, `obsConnectionId` (Konvention), `addToObs` (`action` mit `panel`).
- `hideAfterMinutes` wird `double` mit unit `min`; der Key bleibt. `duration` passt nicht, weil es Sekunden speichert.
- Fenster: `overlay`, `help` (alle Befehle mit ihren Aliasen), `about`. Kein Log, das Projekt schreibt keins.
- Optional: die Notizen der laufenden Kategorie als `table` über `table:`.

**Handle:** `test: (n, s) => n == "addToObs" ? AddObsOverlay(s) : null`, vor dem `EventType.Test`-Zweig.

**Lücken:** F1, F2.

**Schritte:**
- Host-Block einsetzen. Werte kommen aus `UiSettings.Load(SettingsFile).With(args)`; die toleranten Parser bleiben für Set-Argument-Text.
- Der Test-Trigger mit `AddOBSOverlayTo` bleibt; ohne ihn gilt `obsConnectionId`.
- `.Adopt(Old("<Id von OBS Notes>"))`.
- `.sb`: die Settings-Gruppe raus, Action `OBS Notes Settings` (Stub, `GameActionName = "OBS Notes"`) dazu, `build-import.ps1` anpassen.
- Tests: `Harness.cs` liest aus der Datei, prüft "Set Argument gewinnt" und Adopt aus einem Test-Zip; dazu `run-window-tests.ps1`.
- Doku: Setup-Guide (Settings-Abschnitt neu, Abschnitt Upgrade), FAQ, ein README-Bild.
- Release v2.0.0.

### 8.2 Suggestions To (v1 → v2.0.0) – S–M, 3 Tage

**Heute:**
- Drei Actions `[SuTo] ... Discord|Stoat|GameVox` (`extracted\SuTo-*.cs`: 397, 352 und 364 Zeilen), je ein Befehl auf `!suggestion`/`!vorschlag`. Nach dem Import sind sie aus; sind mehrere an, geht der Post an jeden Server.
- Alles wird per `CPH.TryGetArg` gelesen:
  - Discord, 12: `discordWebhookUrl`, `suggestionTemplate`, `showFooter`, `showPlatformTag`, `showTimestamp`, `randomColour`, `colourTwitch|YouTube|Kick`, und für Forum/Thread `discordForumWebhookUrl`, `discordThreadName`, `discordThreadId`. Diese drei sind ab Werk deaktivierte Sub-Actions; "an" heißt aktivieren.
  - Stoat, 8: `stoatSuggestionWebhook`, `timestampFormat` u. a., dazu 4 nur dokumentierte: `suggestionFooterTemplate`, `iconTwitch|YouTube|Kick`.
  - GameVox, 10.
- Webhooks sind Secrets; mehrere werden per Komma getrennt.
- Platzhalter sind `%suggestion%`, `%user%`, `%platform%`, im Thread-Namen `%targetUser%` und `%userName%`.
- Die Bestätigung im Chat kommt aus dem Switch auf `%suggestionPlatform%` mit drei Send-Message-Sub-Actions (Twitch, YouTube, Kick).

**Abbildung:**
- Eine flache Datei `Suggestions To/SuTo-Settings.json` mit einem Präfix je Ziel, 38 Keys.
- Seiten:
  - `discord`: 12 Felder, die drei Forum-Felder in einer Sektion "optional, leer = aus", die Thread-Id schlägt den Namen wie bisher.
  - `stoat`: 12 Felder, `stoatWebhookUrl` mit `aliases: stoatSuggestionWebhook`.
  - `gamevox`: 10 Felder.
  - `messages`: `msgConfirmTwitch|YouTube|Kick` und `useBot`.
  - `help`, `about`.
- Webhooks `masked`, die Farben eines Ziels in einer `row`.

**Handle:** in einer neuen Action `[SuTo] Settings Host`, die nur den Host-Block trägt.
- `test`: "Testvorschlag senden" je Ziel über `RunAction(ziel, false)`.
- `validate`: Form der URL; die Thread-Id nur aus Ziffern.

**Lücken:** F1, F2 (einschließlich der Send-Message-Texte), F3, F4.

**Schritte:**
- Die Bestätigung kommt in den Code, nach erfolgreichem Post, mit einer Plattformweiche wie `Say` in OBS Notes. Dann überlebt sie Importe und steht im Fenster. Der Switch entfällt.
- Die drei Skripte lesen über `HostReader` mit `With(args)`.
- `.sb` nach der Clone-Regel aus CLAUDE.md bauen, `$SuToScripts` in `sb-export.ps1` ergänzen.
- Tests: `Harness.cs` (lokaler Webhook-Server) auf Datei und Args umstellen; `run-window-tests.ps1`; `sb-shots.ps1` liefert Fensterbilder.
- Doku: Setup-Guide (die Argumenttabellen werden Seiten), Manual.md in `aaskjer/SuTo`.
- Release v2.0.0.

### 8.3 GameCheck (Ordner `FreeGameCheck`, v1.0.1 → v2.0.0) – M, 5 Tage

**Heute:**
- Zwei Actions `Free Game Check` und `Game Sale Check`, gebaut aus `src\core` und `src\free|sales` durch `tools\build.ps1`. Regeln dort: kein System.Core, C# 7, keine String-Interpolation.
- Die Settings-Action `[CG] - Settings` hat eine eigene GUI (`src\gui\gamecheck-settings.cs`, 3 739 Zeilen) mit den Seiten Discord, Free games, Sales, Schedule, Status, About und einer Suche.
- 49 Settings in `GameCheck\GameCheck-Settings.json`, verschachtelt: `{"version":1,"free":{…},"sales":{…}}`, Keys = Argumentnamen.
- Die Secrets `botToken`, `webhookUrl`, `itadKey` liegen je Action in persistenten Globals `FreeGameCheck_*`/`GameSaleCheck_*`, nie in der Datei.
- Vorrang: nicht-leeres Set Argument > Datei > Default (`ArgText`, `SecretArg`, `GetColorArg`).
- Jeder Lauf schreibt `GameCheck_free|sales` (actionId, updated, gesetzte Argumente); die GUI sperrt diese Felder.
- Die Status-Seite liest `checkFreeGames`/`checkGameSales` (Message-Ids, lastSent). "Check now" ruft `RunActionById(id, false)` auf, nachdem es gefragt hat, ob gespeichert werden soll.
- Der Fensterzustand liegt in `GameCheck-Window.json`.

**Abbildung:**
- Der Host-Block kommt nach `src\core`, beide Actions tragen ihn. `GameActionName = "Free Game Check"`, Host `gamecheck`.
- Keys werden flach mit Präfix (`freeBotToken` … `salesEmbedColorDeals`), je mit `aliases: "free.botToken"`. Gepunktete Aliase gibt es schon. Der Leser in `Run.cs` liest flach und bis zum ersten Speichern verschachtelt.
- Seiten:
  - `discord`: Secrets `masked` und `global:` (F5). "Both actions use the same bot and channel" wird `salesDiscord` mit `same|own` und `shows`. Die Sales-Action setzt den Key bei ihrem ersten Lauf auf `same`, wenn alle drei Werte gleich sind.
  - `free`, `sales`.
  - `schedule`: `*CheckIntervalMinutes` als `int` mit unit `min` (5–1440), `*DeleteOldOnResend` mit `requires: …ResendIntervalDays>0`.
  - `status`: je Action ein `auto`-Panel mit `every: 10` (Pille, Action-Id mit `copy`, Discord-Modus, Message-Ids mit Zeit) und `checkNow` mit `saveFirst`.
  - `about`.
- `*ExcludePlatforms` über F6. `*Country` als feste `options`.
- Farben werden ohne `#` gespeichert. Im Fenstertest prüfen, ob das Feld das liest; sonst ist ein Fix im Framework fällig.

**Handle:**
- `test`: die Panels aus den Globals, `checkNow`.
- `validate`: Bot ohne Kanal-Id; weder Bot noch Webhook gesetzt.

**Lücken:** F1 (ersetzt `GameCheck_*.arguments` für die Sperre), F5, F6, F7, F8, F9.

**Schritte:**
- Secrets bleiben in ihren Globals, es werden keine Daten verschoben.
- `build.ps1` laufen lassen; `check-compile.ps1` kompiliert den Host-Block gegen die Referenzliste in `src\import\GameCheck.json`.
- `[CG] - Settings` bekommt den Stub.
- Weg: `src\gui`, `tools\gui\GuiHarness.cs`, `gui-build.ps1`, `check-gui.ps1`, `run-gui-tests.ps1`, `gui-shots.ps1`, `GameCheck-Window.json`.
- `FreeGameHarness.cs` und `GameSaleHarness.cs` prüfen: flach, verschachtelt, Set Argument gewinnt, Secret aus der Globale.
- `run-window-tests.ps1` nur mit erfundenen Ids.
- 14 Screenshots neu, README und Setup-Guide anpassen.
- Release v2.0.0.

### 8.4 ChatCalendar (v1.0.1 → v2.0.0) – M–L, 6 Tage

**Heute:**
- Sechs Actions (`[CC] - Calendar Setup`, `[CC] - .Settings`, `Upcoming Holidays`, `Check today's holiday & firstMessage`, `Announce Holidays`, `Birthday Command`), zusammen 8 705 Zeilen. Gemeinsame Blöcke stehen als Kopien in mehreren Dateien (REFACTOR-PLAN.md).
- GUI `ChatCalendar-Settings.cs` (3 230 Zeilen) mit 9 Seiten; Holidays hat zwei Tabs (Official/Own).
- Keine Set Arguments. `Chat Calendar\configs.json` ist flach und hat 29 Keys, darunter `darkMode` und drei Webhooks im Klartext. Ein gleichnamiges Argument gewinnt.
- `holidays.json`: `officialHolidays` von date.nager.at mit editierbarer `CustomHolidayMessage`, gefiltert nach `Counties` und Region; dazu `customHolidays`.
- `birthdays.json`: Einträge mit name, platform, userId, date. Der Befehl im Chat schreibt dazu.
- Log: `Logs\ChatCalendar-yyyy-MM-dd.txt`, Zeilen `HH:mm:ss|kind|platform|user|text`.

**Abbildung:**
- Host ist `[CC] - Calendar Setup`, weil dort CalFile und der Download sitzen. Host-Id `chatcalendar`.
- Die Keys bleiben, damit die fünf Leser unverändert laufen. `.Senders(sendAction: "sendAsMe")`. `darkMode` wandert von selbst ins Layout.
- Seiten:
  - `chat`.
  - `announcements`: `announceTime` (F12), Webhooks `masked`, `testAnnounce` über `RunActionById(Announce, false)` mit einem Panel und `every`.
  - `calendar`: `countryCode` mit 204 festen Optionen, `region` und `timeZone` über F10, Status-Panel, `download` mit `saveFirst`.
  - `holidays`: `officialHolidays` als `list` mit `file: holidays.json` und `field: officialHolidays`. Datum, Name und Counties sind `readonly`, Greeting editierbar; dazu `fixedRows`, `rowKey: "Date,Name"` und `filter` (F11).
  - `ownHolidays`: `customHolidays`.
  - `birthdays`: `list` mit `rowKey: "name,platform"`.
  - `messages` (`{userName}`, `{holidayName}`, `{pronounType}`, `{age}`), `log`, `help`, `about`.
- Die beiden Tabs werden zwei Seiten.

**Handle:**
- `options`: Regionen und Zeitzonen.
- `test`: Status, Download, Testankündigung.
- `saved`: Ist das Land oder die Region geändert, startet `RunActionById(Setup, false)` den Download.

**Lücken:** F8, F10, F11, F12.

**Schritte:**
- `CalLog` (5 Kopien) schreibt das Framework-Format `[yyyy-MM-dd HH:mm:ss] KIND | Platform: … | User: … | Text: …`; alte Logdateien laufen aus. Am besten nach `sync-shared.ps1` aus REFACTOR-PLAN.md, dann ist es eine Stelle.
- `[CC] - .Settings` (Id `c4d0a1f6-…`) bekommt den Stub.
- Weg: `check-settings.ps1`, `test-settings-window.ps1`, `tests\SettingsThreads.cs`, `gui-shots.ps1`, `gui-stage.ps1`.
- `Harness.cs` prüft die Leser gegen eine Datei, die das Fenster geschrieben hat.
- `run-window-tests.ps1`: Setup und der Geburtstagsbefehl schreiben, während das Fenster offen ist (prüft `rowKey`).
- Doku, 11 Screenshots neu.
- Release v2.0.0.

### 8.5 AutoBotCheck (v1.1.0 → v2.0.0) – S, 2 Tage

**Heute:**
- Drei Actions `TwitchInsight`, `TwitchBots.Info`, `ISDS.BanLists` in der Queue `Backend - Bot`, alle mit demselben Code (`src\AutoBotCheck.cs`, 1 068 Zeilen; `src\build_sb.py` baut die `.sb`).
- Set Arguments je Action: `botGroupName`, `whitelistGroupName`, `botSource` (wählt die Liste), `dryRun`; ISDS hat zusätzlich `isdsLists`. Gelesen werden sie mit `GetArg(name, fallback)`.
- Ein Test-Trigger mit `command=killprocess` stoppt den Lauf. Cache `AutoBotCheck\NonExistentBot.txt`.
- Ergebnisse stehen nur im Streamer.bot-Log. Es gibt keinen Harness.

**Abbildung:**
- `AutoBotCheck/AutoBotCheck-Settings.json`. Der Host-Block sitzt im gemeinsamen Code, `GameActionName = "TwitchInsight"`.
- Tabelle:
  - `botGroupName` und `whitelistGroupName` über F10 (`optionsFrom: "groups"`).
  - `isdsLists` über F6.
  - `dryRun`.
  - `runInsights|runBotsInfo|runIsds` (`confirm`, `RunAction(x, false)`).
  - `stopRun` (setzt `stopRequested`).
  - Ein Status-Panel mit `every: 5`.
- `botSource` bleibt ein Set Argument und kommt nicht in die Tabelle. Ein `dryRun` je Action ist weiter per Set Argument möglich (F1).
- Die Log-Seite liest eine eigene Datei im Framework-Format (eine Zeile je Lauf und je Bann).

**Handle:** `test` (Läufe, Stopp, Status).

**Lücken:** F1, F2, F6, F8, F10.

**Schritte:**
- `build_sb.py`: alle Set Arguments außer `botSource` raus, Settings-Action dazu.
- Adopt aus allen drei Ids.
- Ein neuer kleiner Harness und `run-window-tests.ps1`.
- Setup-Guide: "Arguments" und "Upgrading" neu.
- Release v2.0.0.

### 8.6 BetterStreamAnnouncements (v2.1.1 → v3.0.0) – L, 9 Tage

**Heute:**
- 14 Actions: 13 Arbeits-Actions (`bsa-*.cs`, zusammen 28 064 Zeilen; darunter die Gateway-Action Presence, die dauerhaft läuft) und die GUI `bsa-settings.cs` (5 398 Zeilen).
- `Announcements\Discord\settings.json` ist flach, die Klasse `BsASettings` hat 152 Properties:
  - Präfixe `SF_` 74, `BA_` 33, `SE_` 15, `BOT_` 13, `PS_` 8, `CC_` 5.
  - Dazu `DarkMode`, `Egg`, `ShowDebugSections`, `HomeShowStartHere`.
- Jede Action liest per `JsonConvert.DeserializeObject<BsASettings>`.
- 19 Zugangsdaten stehen im Klartext.
- `/bsa setup`, `/streamerfriends` und `/crosspost` schreiben die Datei auch, während das Fenster offen ist.
- `SF_*DiscordMap` ist Text der Form `login:id,…`.
- Der Crosspost-Schlüssel liegt per DPAPI in `BsA_Crosspost.key`; das Fenster zeigt den Sicherungscode und stellt den Schlüssel wieder her.
- Fenster:
  - 13 Seiten in vier einklappbaren Rail-Gruppen, mit doppelten Titeln (Online, Offline, Platforms).
  - Ein Home-Dashboard.
  - "Status Log" ist das Streamer.bot-Log `logs\log_yyyyMMdd.log`, gefiltert.
  - Eine Suche.

**Abbildung:**
- Die Keys bleiben (PascalCase ist ein freier Key, die Leser bleiben unverändert). Labels und Hilfen kommen aus der alten GUI. Host `bsa`.
- Neue Action `[BsA] - Settings Host`, weil die Presence-Action belegt ist.
- `DarkMode` und `Egg` übernimmt das Framework. `ShowDebugSections` wird ein `bool` mit `showWhen` auf die Debug-Sektionen, `HomeShowStartHere` wird `hidden`.
- Freunde und `*RoleIds` werden `namelist`, `*DiscordMap` F15 (vorerst `text`).
- Home wird `auto`-Panels mit `every`, gespeist aus `BsA_*_Sessions` und den Tageszahlen.
- Der Crosspost-Schlüssel läuft über Aktionen mit `panel` und `clipboard`; Wiederherstellen über ein `transient`-Feld und eine `live`-Aktion.
- Rail-Gruppen F13, Status Log F14, Suche F7.

**Handle:**
- `test`: Panels, Testposts per `RunAction(x, false)`, Schlüssel.
- `validate`.
- `saved`: Presence neu laden; heute geschieht das per `CPH.ExecuteMethod` auf die Gateway-Action.

**Lücken:** F4, F7, F8, F13, F14, F15.

**Schritte:**
- Die 148 Tabellenzeilen per Skript aus `BsASettings` und den GUI-Texten erzeugen (wie `spec-to-window.ps1`).
- Die Settings-Action bekommt den Stub.
- Weg: die GUI, `check-settings.ps1`, `GuiShots.cs`, `gui-shots.ps1`.
- `check-compile.ps1` und `check-actions.ps1` um die Host-Action erweitern.
- `run-window-tests.ps1` mit einem Schreibvorgang per Slash-Befehl bei offenem Fenster.
- Setup-Guide, FAQ und Bilder.
- Release v3.0.0.

### 8.7 Framework-Features

**1.5.0 (4½ Tage)**
- **F1 Set-Argument-Brücke.**
  - Im Host-Block `UiSettings.With(args)`: Ein nicht-leeres Argument, dessen Name ein Key oder Alias der Tabelle ist, gewinnt und wird nach Kind gewandelt. Im HostReader geht das mit einer Key-Liste.
  - `UiHost.Handle(…, arguments: true)` liefert im Describe `arguments: ["dryRun", …]` mit. Das Fenster zeigt diese Felder gesperrt mit der Pille "Set Argument" und einem Hinweis, wo es steht.
  - Der Stub gibt `uiframework.callerArgs` mit; seine eigenen Trigger-Args zählen nicht.
  - Allgemein: `locked: s => "key|Grund"`.
  - Projekte: OBS, SuTo, GameCheck, ChatCalendar, AutoBotCheck.
- **F2 Übernahme aus der alten Version.**
  - `.Adopt(Old(actionId, "alt>neu", "#subActionId>key"), …)`, im Schema `window.adopt`.
  - Nur ohne Settings-Datei: Die DLL sucht die Id in `<SB>\data\actions.json`, sonst im neuesten `backup\backup-*.zip` (in jedem liegt `actions.json`).
  - Sie nimmt aktive Set-Argument-Sub-Actions (type 123, auch in Gruppen; `enabled:false` zählt nicht), mit `#id` auch den `text` von Send-Message-Sub-Actions (types 10, 4001, 35001).
  - Die Werte kommen wie `set` als ungespeicherte Änderungen ins Fenster, mit dem Hinweis "N Werte aus der Sicherung vom … übernommen". Werte gehen nie ins Log.
  - Projekte: OBS, SuTo, AutoBotCheck.
- **F3 Platzhalter `%x%`.**
  - `UiWindow(…, placeholderStyle: "percent")`, auch je Setting.
  - Chips und Vorschau arbeiten mit `%name%`.
  - Projekt: SuTo.
- **F4 Projekte mit mehreren Actions.**
  - `stub/HostReader.cs`: nur `UiSettings` (Load mit `.bak`-Rückfall, `With(args, keys)`), ohne Tabelle. `update-host-block.ps1` und `check-stub.ps1` kennen die Datei.
  - Ein neues Kapitel in Getting-Started: "A project with several actions" (Host-Action, Leser, keine Umbenennung, Langläufer).
  - Projekte: SuTo, ChatCalendar, BsA.

**1.6.0 (4½ Tage)**
- **F5 Secrets in Globals.**
  - `S("freeBotToken", "", masked: true, global: "FreeGameCheck_botToken")`.
  - Der Stub liest die persistente Globale vor dem Öffnen und schreibt sie nach dem Speichern über die Job-Queue. Nie in Datei, Export oder Log; "Reset" lässt sie stehen.
  - Ohne passenden Stub zeigt die DLL das Feld gesperrt und schreibt es nicht in die Datei.
  - Projekt: GameCheck.
- **F6 Kind `multi`.**
  - Häkchen über `options`, gespeichert als Komma-Text wie `namelist`.
  - Eine ältere DLL zeigt ein Textfeld und das rote i; der Wert bleibt.
  - Projekte: GameCheck (`*ExcludePlatforms`), AutoBotCheck (`isdsLists`).
- **F7 Suche.**
  - Ein Feld oben in der Rail (Strg+F). Es filtert Seiten und Felder nach Label, Hilfe und Key und hebt Treffer hervor.
  - `UiWindow.Search(false)` schaltet es ab.
  - Projekte: GameCheck, BsA. Beide GUIs haben die Suche heute; ohne F7 fiele sie weg.
- **F8 Status-Panels.**
  - `auto` mit `every: 10` (nur solange die Seite sichtbar ist).
  - Antwortzeilen `{ "label", "text", "copy": true }` und `{ "pill", "tone": "ok|warn|muted" }`.
  - `action` mit `saveFirst: true`.
  - Projekte: GameCheck, ChatCalendar, AutoBotCheck, BsA.
- **F9 Mindestversion.**
  - `UiWindow(…, requires: "1.6.0")`. Ein Stub ab 1.6 vergleicht mit der geladenen DLL und bietet das Update an, statt das Fenster zu öffnen.
  - Schützt F5 vor einer DLL ≤ 1.5.

**1.7.0 (3½ Tage)**
- **F10 Optionen vom Spiel.**
  - `S("region", "", kind: "options", optionsFrom: "countryCode")`, ein neuer Request `options` und der Delegat `options: (key, s) => new[] { "DE-BY|Bayern" }`.
  - Gefragt wird beim Öffnen und wenn sich ein genanntes Feld ändert. `optionsFrom: "groups"` liefert die Streamer.bot-Gruppen.
  - Ein gespeicherter Wert ohne passende Option bleibt als Extra-Eintrag stehen.
  - Projekte: ChatCalendar, AutoBotCheck.
- **F11 Listen, die das Spiel füllt.**
  - `rowKey`: Speichern mischt mit dem Stand der Datei. Zeilen des Spiels bleiben, Änderungen des Nutzers gewinnen.
  - `fixedRows`: kein Hinzufügen und Löschen.
  - `filter: "region= || Global=true || Counties~{region}"`: `~` heißt "enthält", `{key}` ist ein Feldwert; gefilterte Zeilen werden ausgeblendet, nicht gelöscht.
  - Arrays in `readonly`-Spalten werden kommagetrennt gezeigt.
  - Projekt: ChatCalendar.
- **F12 Kind `time`.**
  - `HH:mm` als Text.
  - Projekt: ChatCalendar.

**1.8.0 (3½ Tage)**
- **F13 Rail-Gruppen.**
  - `.Group("broadcaster", "Broadcaster", "baOnline", "baOffline", …)`. Nicht über `P(…)`, weil `fields` dort `params` ist.
  - Die Überschriften sind einklappbar; der Anpassen-Modus verschiebt Seiten zwischen Gruppen (`pages.<id>.group`).
  - Eine ältere DLL zeigt die Seiten flach.
  - Projekt: BsA.
- **F14 Streamer.bot-Log.**
  - `.StreamerBotLog(match: new[] { "[BsA/", "'[BsA] - ", "'[SF] - " })`.
  - Liest `<SB>\logs\log_yyyyMMdd.log` (Zeilen `[… LVL] Quelle :: Text`), filtert nach Stufe und Quelle, der Tag ist wählbar.
  - Projekt: BsA.
- **F15 Paarliste als Text.**
  - `list` mit `format: "pairs"`: speichert `a:b,c:d`, liest `,`/`;` und `:`/`=`.
  - Projekt: BsA (`SF_*DiscordMap` ×7).

**Breaking:** keins für die acht Spiele.
- Alle Felder sind optional.
- Die neuen Parameter von `Handle` sind benannt; den neuen Host-Block per `update-host-block.ps1` brauchen nur Projekte, die sie nutzen.
- Neue Stub-Methoden laufen über `ReflectionHost`; fehlt eine, ist das Feature aus.

Zu beachten:
- F5 mit einer alten DLL ist ein Sicherheitsrisiko; F9 fängt das ab, GameCheck setzt `requires`.
- `sendAsMe` kommt nicht in die Konventionen (siehe 8.0).

### 8.8 Reihenfolge und Meilensteine

| Meilenstein | Release                          | Aufwand |
|-------------|----------------------------------|---------|
| M1          | UIFramework 1.5.0 (F1–F4)        | 4½ T    |
|             | OBS Notes v2.0.0                 | 2 T     |
|             | Suggestions To v2.0.0            | 3 T     |
| M2          | UIFramework 1.6.0 (F5–F9)        | 4½ T    |
|             | GameCheck v2.0.0                 | 5 T     |
| M3          | UIFramework 1.7.0 (F10–F12)      | 3½ T    |
|             | ChatCalendar v2.0.0              | 6 T     |
|             | AutoBotCheck v2.0.0              | 2 T     |
| M4          | UIFramework 1.8.0 (F13–F15)      | 3½ T    |
|             | BetterStreamAnnouncements v3.0.0 | 9 T     |

Summe: etwa 16 Tage Framework und 27 Tage Projekte, rund achteinhalb Wochen.

Abweichung von der Reihenfolge vom 07.11.: AutoBotCheck kommt vor BsA. Es braucht nur F1, F2, F6, F8 und F10, und ab dann setzt ein Import die Gruppennamen nicht mehr zurück. BsA kommt zuletzt, weil F13 und F14 nur BsA dienen.

Fertig je Projekt, wenn:
- die alte GUI bzw. die Set-Argument-Gruppe weg ist,
- ein Import über die alte Version im Fenstertest ohne Werteverlust bleibt (F2 aus einem Test-Zip),
- `hosts/<id>/schema.json` im Framework-Repo liegt,
- die Doku Bilder hat.

## Audit vom 08.11.2026

Vier Durchgänge: Stub und Loader, Daten und Schema, Fenster, Doku und API. 47 Befunde. Behoben in 1.2.0:
- die Hilfezeile in benannten Zeilen,
- Listendateien, die überschrieben werden konnten,
- *Seite zurücksetzen*,
- Theme-Keys eines Spiels,
- `!key=wert`,
- Cooldown-Keys,
- die Versionsprüfung,
- *Zeile hinzufügen* bei Suche,
- die Argumente in `UiHost.Handle`,
- das Update-Zeichen der Demo,
- die Doku-Fehler.

Offen:

### Stub 0.3.0 (danach jedes Spiel neu importieren)

- **Fenster friert ein:** Test, Prüfen und Tabellen warten auf dem Fensterthread bis zu 35 s auf das Spiel; Windows meldet "Keine Rückmeldung". Loader.md sagt das Gegenteil. Hooks asynchron oder ein kurzes Budget für alles außer `saved`.
- **Keine Antwort wird zu "erledigt":** Bei Zeitüberschreitung gibt `Host.Test` null zurück, das Fenster zeigt das grün als erledigt. Eine Fehlerantwort (oder die Marke `\u0001uiframework:no-answer`) und im Fenster die Fehlerfarbe.
- **Update-Dialog:** *Abbrechen*, auch X und Esc, überspringt die Version für immer. Überspringen als eigene Wahl; X und Esc heißt "später fragen".
- **Download mit Wiederholung:** Ein 5xx oder Timeout von GitHub beim Laden der DLL bricht sofort ab und zeigt die rohe Meldung (am 08.11. ein 504 für das frische Asset von 1.4.1; Sekunden später lud es mit dem richtigen Hash). Zwei- bis dreimal mit ein paar Sekunden Pause wiederholen; danach "GitHub hat nicht geantwortet, versuch es gleich noch einmal" statt der Ausnahme.
- **Antwortvariable je Anfrage:** Nach 35 s wird das Tor ersetzt, und eine hängende und eine neue Anfrage teilen sich `AnswerVar`. Je Anfrage eine eigene Variable mit Guid, im `finally` geleert.
- **Loader-Einstellungen je Spiel:** `autoUpdate`, `skipVersion` und `frameworkAvailable` liegen in der Layout-Datei jedes Spiels, gelten aber für die eine DLL. Eine gemeinsame Datei unter `UIFramework\lib` oder klar dokumentieren.
- **Kleinere Punkte:**
  - `ExamplePath()` lädt `latest.json` im Zweig "Nein" erneut (offline 5–15 s Stillstand, falscher Beispielordner bei `minHostStub`).
  - Der Log-Leser öffnet ohne `FileShare.ReadWrite`, also fehlen die Zeilen von heute, während das Spiel schreibt.
  - Versionsvergleich ohne `Full()` ("1.1" gilt als älter als "1.1.0").
  - Der Fehlerdialog sagt, der Host-Block stehe "am Ende".
  - Drei Log-Präfixe für ein Spiel.
- **Einfacher:** Sieben statische Flags und eine eigene Fenster-Sperre neben `Shell.Claim`. Für `Stub.cs` gibt es keinen Unit-Test; `check-stub.ps1` braucht ein lokales Streamer.bot.

### Framework

- **Import ungeprüft:** Ein Import schreibt Werte ohne min/max, Typprüfung, `Validate` und den `readOnly`-Filter und gibt sie gleich ans Spiel. Er soll über denselben Weg wie *Speichern* laufen.
- **Werte außerhalb der Grenzen:** Das Fenster zeigt sie, wie sie in der Datei stehen; erst ein späteres Speichern korrigiert sie, ohne Hinweis. Als ungespeicherte Änderung zählen oder in der Fußzeile nennen.
- **"Abschnitt geteilt"** wird nach dem Layout geprüft und gibt dem Spiel die Schuld, wenn der Nutzer ein Feld verschoben hat. Die Prüfung soll die deklarierte Reihenfolge ansehen; ein Ablegen übernimmt den Abschnitt des Ziels.
- **Bedingung auf eine Liste:** `Requirement` kennt `Parts.Lists` nicht und folgt der laufenden Liste nie.
- **Die Spielseite kennt die Konventionen nicht:** `UiSettings` im Spiel kennt die Aliase und Defaults aus `keys.json` nicht. Dokumentieren oder mitgeben.
- **Fenster:**
  - Auf Deutsch und Spanisch werden bei minimaler Breite Schalterlabels ohne "…" abgeschnitten, die Fußzeile zeigt "Ke…".
  - Die Seitenleiste und der Anpassen-Modus gehen nur mit der Maus.
  - *Hilfe unter den Feldern* erreicht die Felder des Frameworks nicht (Nachrichten-Seite, Log-Schalter), und ein gesperrtes Fenster kann sie nicht einschalten.
  - Eine `readOnly`-Einstellung ignoriert `showWhen`/`requires`, die Bedingung ihres Abschnitts und ihre Hilfe.
  - Der Hinweis im Anpassen-Modus zählt den Hilfe-Schalter nicht; *Layout zurücksetzen* tut bei 0 Änderungen nichts.
  - Ein leerer Akzent zeigt ein schwarzes Feld, und der Picker öffnet auf Gelb.
  - Die beiden Zurücksetzen-Knöpfe sehen gleich aus, mit gespiegelten Pfeilen; derselbe Pfeil heißt auf Log und Tabellen "neu lesen".
  - Ordner öffnen hat zwei verschiedene Icons.
- **Texte:**
  - Dialogtitel in gemischter Schreibweise.
  - Die Tour nennt Strg+0 nicht.
  - `cust.iconHelp` ohne "gift"; ein unbekanntes Icon wird stumm zum Punkt.
  - `cust.page.tip` ohne "Icon ändern".
  - Import sagt "ersetzen", mischt aber.
- **Verdacht:** Der Akzent wird bei `LostFocus` übernommen und baut das Fenster mitten im Klick neu; der Klick auf *Fertig* oder *Export* geht womöglich verloren.

### API und Einfachheit

- **Bedingungen:** Fünf Wege, ein Feld bedingt zu machen: `requires`, `requires: "hide:"`, `showWhen`, `shows`, Abschnittsbedingung. `hide:` ist dasselbe wie `showWhen`. Dokumentieren: `requires` graut aus, `showWhen` blendet aus. `hide:` weiter lesen, aber nicht mehr empfehlen; Trivia umstellen.
- **`auto`** folgt nur mit `live` den getippten Werten und braucht `panel`, obwohl es selbst eine Box hat. `auto` soll `live` und `panel` mitbringen. `live` auf einem Nicht-Knopf bekommt einen eigenen Namen.
- **Altlasten:**
  - die Spec-Schicht (`overrides`, `UiWindow.Override`, von keinem Spiel genutzt),
  - `hosts/rhymetime/spec.json` und `host-section.cs`,
  - eine `custom`-Seite, die mit dem generischen Stub stumm samt ihren Feldern verschwindet,
  - `About(authorLink:)` ohne Wirkung,
  - der Key `advanced` ohne Nutzen,
  - `.Answer(...)` (nur für die Vorschau) im Demo-Spiel.
- **Toter Code:** `Sidecars.Strip`, `Requirement.Holds(op)`, `SettingsStore.LoadOrDefaults`, `Conventions.Aliases()`/`FrameworkKeys`, `LayoutFile.OverriddenPages`/`Fields`, `Text.Or`. Dazu `Schema.Defaults()`/`Aliases()`: Die Tests nutzen sie statt des Wegs, den das Fenster geht.
- **Das rote i übersieht einiges:**
  - `panel`/`auto`/`confirm` auf einem Nicht-Knopf,
  - `twitchOnly` auf einem Nicht-Schalter,
  - `placeholders` auf einer Nicht-Vorlage,
  - eine falsche `platform`,
  - `{x}` im Default, das in `placeholders` fehlt,
  - Tippfehler in Seiten-Ids (`"mesages"` wird eine leere Seite).

  Die Meldung zum unbekannten Kind nennt die gültigen nicht; "The spec overrides" ist Innensprache.
- **Builder und JSON:**
  - Schema.md gibt Builder-Schreibweisen als JSON aus: `"a|Label"`, Spalten als Text, `aliasFactor`. `Setting.Parse` liest sie nicht.
  - Seiten-`bottom` und Fenster-`language` hat nur JSON.
  - `Log(writeLogLabel:)` ist nicht dokumentiert.
  - `colour`/`color` wird nur bei `kind` angenommen.
  - `Tip`, `Cmd`, `Link` und `Note` teilen sich einen Typ, also kompiliert Falsches und fällt stumm weg.
- **Namen:** Sieben Namen in zwei Dateien. `ActionName` ist nur noch ein Log-Label: aus Streamer.bots `actionName` ableiten, in der Doku eine Tabelle.
