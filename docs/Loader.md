# Loader

Jede Settings-Aktion (der Stub) trägt den Loader. Er sucht `UIFramework.dll` unter
`<Streamer.bot>\UIFramework\lib\<version>\` und nimmt die höchste Version.

**Erster Start ohne DLL:** ein Dialog fragt
- **Ja**: `lib/v1/latest.json` aus diesem Repository lesen (Version, URL, SHA-256), die DLL vom Release laden,
  Hash prüfen, nach `lib\<version>\UIFramework.dll` legen, Fenster öffnen.
- **Nein**: der Ordner `UIFramework\lib` wird angelegt und im Explorer geöffnet, mit dem Hinweis, die Datei in einen
  Versionsordner darunter zu legen (Releases-Link im Dialog).
- **Abbrechen**: nichts passiert; die Aktion loggt eine Warnung.

**Jeder spätere Start (`Init`):** liegt eine DLL vor und `autoUpdate` in `uiframework.layout.json` ist nicht
`false`, prüft ein Hintergrund-Thread `latest.json`. Eine neuere Version wird in ihren eigenen Ordner geladen und
ab dem nächsten Start von Streamer.bot verwendet; die laufende DLL wird nie überschrieben (sie ist gesperrt und
.NET Framework entlädt nicht).

**Schutz:** HTTPS, SHA-256 aus `latest.json`, ein globaler Mutex, damit mehrere Hosts nicht gleichzeitig laden.
Die Major-Linie steckt im Pfad (`lib/v1/`): ein inkompatibles v2 landet nie in einem v1-Host.

**Referenzen:** der Dialog ist eine WPF-MessageBox; die Settings-Aktion braucht `PresentationFramework`,
`PresentationCore` und `WindowsBase` in ihrer Referenzliste (ein aus einer WPF-Settings-Aktion gewachsener Export
hat sie). Der Download läuft über `System.Net.WebClient` aus `System.dll`.

**Release:** ein Tag `vX.Y.Z` (die `<Version>` in `UIFramework.csproj` muss gleich sein) lässt
`.github/workflows/release.yml` bauen, testen, das Release mit `UIFramework.dll` anlegen und `lib/v1/latest.json`
auf `main` schreiben.
