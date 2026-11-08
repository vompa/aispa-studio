# Browser-Spike: Windows-App im Browser (WebAssembly)

Stand: 07.11.2026. Branch `spike/browser-wasm` im Worktree `aispa-studio-browser-spike`, nicht gemergt.

## Frage

Läuft die Avalonia-Windows-App nach der Umstellung „alle Logik in der API“ auch im Browser? Wie groß ist sie, wie lange lädt sie, und ist sie mit Screenreader bedienbar? Die Antwort entscheidet, ob ein ADR für eine Browser-Fassung der Verwaltung sinnvoll ist. ADR 0084 hatte eine zweite Web-Verwaltung abgelehnt, weil jede Maske zweimal gebaut würde. Mit WebAssembly wäre es derselbe Code.

## Aufbau

- Neues Projekt `src/AispaStudio.Browser` (`net10.0-browser`, `Avalonia.Browser` 12.1.4), das die Desktop-Assembly als Bibliothek nutzt.
- `App.axaml.cs` bekommt einen Zweig für eine Ansicht (`ISingleViewApplicationLifetime`): nur Server-Betrieb, die Cookies verwaltet der Browser.
- `MainView` ist eine Kopie des Inhalts von `MainWindow` ohne Fenster. Im Spike ist sie bewusst kopiert und nicht umgebaut, damit die Tests mit `MainWindow` unberührt bleiben.
- Ausgeliefert wird unter derselben Herkunft wie die API: ein kleiner Node-Server liefert die `.br`-Dateien und leitet `/api` und `/hubs` samt WebSocket an die API weiter. CORS ist nicht nötig.
- Der Workload `wasm-tools` ist Pflicht. Ohne ihn baut das Projekt, die Seite bleibt aber weiß (`TypeInitialization … SkiaSharp.SKImageInfo`).

Nötige Spike-Einstellungen im Browser-Projekt:

| Einstellung | Grund |
|---|---|
| `ValidateExecutableReferencesMatchSelfContained=false` | Die Desktop-Exe wird als Bibliothek genutzt (NETSDK1150). |
| `JsonSerializerIsReflectionEnabledByDefault=true` | Beim Trimmen ist die JSON-Reflexion sonst aus; die App bekam Antworten, warf sie aber weg (`ERR_ABORTED`, „Keine Verbindung zum Server“). |
| `TrimmerRootAssembly` für Desktop, Client, Contracts | DTO-Eigenschaften und Reflection-Bindings bleiben erhalten. |

## Ergebnis: Funktion

Geprüft als Inhaberin (Testkonto der Entwicklungsumgebung) gegen die lokale API mit Demo-Daten:

| Bereich | Ergebnis |
|---|---|
| Startbildschirm, Anmeldung | läuft, Schrift und Farben wie in der Windows-App |
| Kalender | läuft mit Terminen, Spalten je Person, Wartezeile |
| Anfragen (Posteingang) | läuft mit KI-Vorschlägen |
| Kunden | läuft, Liste und Suche |
| Kassa | läuft, „Heute abzurechnen“ |
| Geräte | Maske öffnet sich; Bondrucker und Kartenterminal können im Browser nicht arbeiten (kein TCP) |
| Verwaltung | nicht geprüft: zwei Klicks öffneten den Bereich nicht, Ursache offen (Koordinaten in der skalierten Ansicht oder App) |

In der Konsole gab es beim Rundgang keine Fehler außer der erwarteten 401 vor der Anmeldung.

## Ergebnis: Größe und Ladezeit

| Messgröße | Wert | Herleitung |
|---|---|---|
| Bündel `_framework`, Brotli | 8,78 MB (8.777.773 Byte) | Python-Summe der `.br`-Dateien; Gegenprobe `find -printf %s` gleich |
| Bündel unkomprimiert | 34,4 MB (34.374.194 Byte), 102 Dateien | wie oben, beide gleich |
| Übertragen je kaltem Laden | 8.065 KB | CDP `Network.loadingFinished`, in allen 10 Läufen gleich |
| Erstes Bild, ohne Drosselung (lokal) | Median 1,4 s (1,2–1,9 s) | Konsolenmarke `aispa:first-view`, 5 Läufe |
| Verbunden, ohne Drosselung | Median 1,6 s | Konsolenmarke `aispa:connected` |
| Erstes Bild, „Fast 4G“ (9 Mbit/s, 60 ms) | Median 9,0 s (8,7–11,8 s) | wie oben mit CDP-Drosselung |
| Verbunden, „Fast 4G“ | Median 9,1 s | wie oben |

Die größten Brocken (Brotli): Laufzeit `dotnet.native.wasm` 2,6 MB, `AispaStudio.Desktop` 1,4 MB, `System.Private.CoreLib` 0,6 MB, Schrift Inter 0,5 MB, `Avalonia.Base` 0,5 MB, ICU-Daten CJK 0,25 MB. Weniger wäre möglich: ICU nur für Deutsch, eigene Assemblies trimmbar machen. Gemessen ist das nicht.

Einordnung: Am PC im Spa-Netz lädt die App in 1–2 s, beim ersten Aufruf über Mobilfunk in rund 9 s. Für Inhaber von zu Hause ist das tragbar, für die Handy-Apps nicht (die bleiben ohnehin Web, ADR 0084).

## Ergebnis: Barrierefreiheit

| Prüfung | Ergebnis |
|---|---|
| Accessibility-Baum nach der Anmeldung (CDP `Accessibility.getFullAXTree`) | 4 sichtbare Knoten: Seite, 2 × generisch, Canvas; einziger Name ist der Seitentitel |
| Tab-Taste, 6 Schritte | der Fokus bleibt auf der Seite, kein Element der App erscheint |

Ein Screenreader findet damit weder Felder noch Knöpfe noch Texte. Unter Windows meldet dieselbe App alle Elemente über UI Automation (`AccessibleNames`). Im Browser kommt davon nichts an, und die Avalonia-Doku zu WebAssembly erwähnt Barrierefreiheit nicht. Damit verfehlt die Browser-Fassung WCAG 2.2 AA grundsätzlich, nicht nur in Einzelheiten.

## Nicht gemessen

- NVDA oder ein anderer echter Screenreader (Checkliste unten). Möglich ist, dass Avalonia den Baum erst baut, wenn ein Screenreader aktiv ist. Der CDP-Baum zeigt das nicht.
- Andere Browser als Chrome (Firefox, Safari, Edge), Handy und Tablet.
- AOT-Kompilierung (laut Doku schnellerer Start, größerer Download), verkleinerte ICU-Daten.
- Bereich Verwaltung, Drucken, Hilfe, Live-Updates über SignalR (die Verbindung wurde nicht eigens geprüft).
- Die zweite Herleitung der Ladezeit (Zeitpunkt des ersten Bildes per Screenshot) ist entfallen. Die Zeiten beruhen nur auf den Konsolenmarken.

## Checkliste NVDA (für den Auftraggeber)

1. Spike starten (Befehle unten), Chrome öffnen, NVDA starten.
2. `http://localhost:5190/` laden und warten, bis die Anmeldung erscheint.
3. Mit Tab durch die Anmeldung gehen: Liest NVDA „Als Inhaberin“, „Testkonto“ und „Als Testkonto anmelden“ vor?
4. Nach der Anmeldung im Kalender mit Tab und Pfeiltasten: Werden Termine vorgelesen?
5. Ergebnis hier eintragen.

## Empfehlung

Kein ADR für eine Browser-Fassung der Verwaltung, solange Avalonia im Browser keinen Accessibility-Baum liefert. Technisch läuft die App erstaunlich vollständig, und Größe sowie Ladezeit wären für den PC vertretbar. Die fehlende Barrierefreiheit widerspricht aber einer festen Vorgabe des Projekts. Prüfenswert, bevor das Thema geschlossen wird: der NVDA-Test oben und ein Blick, ob Avalonia 12.2 oder neuer Barrierefreiheit im Browser ankündigt.

## Nachstellen

```bash
dotnet workload install wasm-tools
docker compose up -d --build
dotnet publish src/AispaStudio.Browser -c Release -o <ziel>
node tools/browser-spike/spike-server.mjs <ziel>/wwwroot 5190
```

`spike-server.mjs` und das Messskript `measure.mjs` (Playwright, CDP) liegen im Branch `spike/browser-wasm` unter `tools/browser-spike/`.
