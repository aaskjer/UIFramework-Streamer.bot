# Loader

Jede Settings-Aktion (der Stub) trägt den Loader. Er sucht `UIFramework.dll` unter
`<Streamer.bot>\UIFramework\lib\<version>\` und nimmt die höchste Version.

**Erster Start ohne DLL:** ein Dialog fragt
- **Ja**: `lib/v1/latest.json` aus diesem Repository lesen (Version, URL, SHA-256), die DLL vom Release laden,
  Hash prüfen, nach `lib\<version>\UIFramework.dll` legen, Fenster öffnen.
- **Nein**: der Ordner `UIFramework\lib` wird angelegt und im Explorer geöffnet, mit dem Hinweis, die Datei in einen
  Versionsordner darunter zu legen (Releases-Link im Dialog).
- **Abbrechen**: nichts passiert; die Aktion loggt eine Warnung.

**Jedes spätere Öffnen des Fensters:** liegt eine DLL vor und `autoUpdate` in `uiframework.layout.json` ist nicht
`false`, liest der Stub `latest.json` (5 s Timeout). Gibt es eine neuere Version, fragt ein Dialog: **Ja** lädt
sie in ihren eigenen Ordner; war die alte DLL in diesem Streamer.bot-Lauf noch nicht geladen, öffnet sich das
Fenster gleich mit der neuen, sonst ab dem nächsten Start. **Nein** fragt beim nächsten Öffnen wieder.
**Abbrechen** schreibt `skipVersion` in die Layout-Datei; diese Version wird nicht mehr angeboten, die nächste
schon. Die laufende DLL wird nie überschrieben (sie ist gesperrt und .NET Framework entlädt nicht).

**Pille im Kopf des Fensters:** wer im Update-Dialog *Nein* drückt, sieht im Fenster die Pille
`UIFramework 0.3.2 available` (Klick öffnet die Releases), bis die Version genommen oder mit *Abbrechen*
übersprungen ist. Grundlage ist `frameworkAvailable` in `uiframework.layout.json`; *Ja* und *Abbrechen* löschen
die Notiz wieder.

**Woher `latest.json` kommt:** zuerst über die GitHub-API (`contents/lib/v1/latest.json`, ungecacht, 60 Abrufe je
Stunde und IP); schlägt das fehl, über raw.githubusercontent.com, dessen CDN die Datei bis zu fünf Minuten alt
liefert. Jede Prüfung schreibt eine Zeile ins Streamer.bot-Log: installierte Version, was `latest.json` sagt, ob
gefragt, übersprungen oder abgeschaltet wurde.

**Schutz:** HTTPS, SHA-256 aus `latest.json`, ein globaler Mutex, damit mehrere Hosts nicht gleichzeitig laden.
Die Major-Linie steckt im Pfad (`lib/v1/`): ein inkompatibles v2 landet nie in einem v1-Host.

**Referenzen:** der Dialog ist eine WPF-MessageBox; die Settings-Aktion braucht `PresentationFramework`,
`PresentationCore` und `WindowsBase` in ihrer Referenzliste (ein aus einer WPF-Settings-Aktion gewachsener Export
hat sie). Der Download läuft über `System.Net.WebClient` aus `System.dll`.

**Release:** ein Tag `vX.Y.Z` (die `<Version>` in `UIFramework.csproj` muss gleich sein) lässt
`.github/workflows/release.yml` bauen, testen, das Release mit `UIFramework.dll` anlegen und `lib/v1/latest.json`
auf `main` schreiben.
