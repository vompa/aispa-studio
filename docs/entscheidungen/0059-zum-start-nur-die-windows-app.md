# 0059. Zum Start wird nur die Windows-App ausgeliefert

- **Status:** angenommen
- **Datum:** 19.10.2026
- **Entschieden von:** Auftraggeber

## Kontext

Bis zum 19.10.2026 gab es drei Oberflächen für das Team: die Windows-App am Empfang, die Team-Oberfläche im Browser und „Mein Tag“ am Handy. Verwaltungsseiten (Stammdaten, Firmendaten, Zugänge, Dienstplan, Kassen, Berichte und weitere) gingen nur im Browser. Der Auftraggeber legte am 19.10.2026 im Projektchat fest, dass zum Start nur der Windows-Client ausgeliefert wird und dass Personal, Firmendaten (mit Zahlungsanbietern und Impressum) und alle Verwaltungsseiten in der Windows-App voll nutzbar sein müssen, auch im Einzelplatz-Betrieb ohne Server.

## Optionen

| Option | Bewertung |
| --- | --- |
| Windows-App und Team-Oberfläche im Browser gleichzeitig ausliefern | zwei Oberflächen zu prüfen, zu dokumentieren und zu unterstützen; Einzelplatz braucht trotzdem alles in der App |
| Nur die Windows-App, Verwaltung darin voll nutzbar | gewählt |

## Entscheidung

Zum Start bekommt ein Spa nur die Windows-App. Kalender, Kunden, Kassa, Anfragen und Geräte bleiben nativ in der App; die Verwaltungsseiten werden einmal als Webseiten gebaut und in der App eingebettet ([ADR 0053](0053-verwaltung-in-der-windows-app-eingebettet.md)). Im Einzelplatz liefert die lokale API diese Seiten aus ([ADR 0041](0041-einzelplatz-standalone-und-zuschaltbare-webmodule.md)). Online-Buchung, „Mein Tag“ und die übrigen Webmodule bleiben gebaut und zuschaltbar, sind aber nicht Teil des Starts.

## Begründung

Am Empfang eines Day-Spas steht ein Windows-PC; ein Programm, das auch ohne Internet läuft, ist dort leichter einzuführen als mehrere Oberflächen. Eine einzige ausgelieferte Oberfläche hält Prüfung, Handbuch und Unterstützung klein.

## Folgen und Stand

- Neue Verwaltungsfunktionen müssen in der eingebetteten Verwaltung der App funktionieren (WebView2, Anmeldung aus der App), auch im Einzelplatz.
- Rezeptionsfunktionen des Kalenders wurden ab #130 zuerst in der Windows-App gebaut; die Team-Oberfläche im Browser folgt nur, wo es nötig ist.
- Umgesetzt mit #120, #122, #124, #125, #127 und #145. Unter Windows mit WebView2 noch nicht vom Auftraggeber ausprobiert.

## Quellen

- Projektchat 19.10.2026 (Auftraggeber, Festlegung zum Start; Zustimmung zum eingebetteten Ansatz um 12:31 UTC)
- [ADR 0053](0053-verwaltung-in-der-windows-app-eingebettet.md), [ADR 0045](0045-windows-app-vollwertiger-empfangsarbeitsplatz.md)

## Nachtrag Runde R05, Slice 92 (29.10.2026): Verwaltung nativ

Die Verwaltungsseiten werden nicht mehr eingebettet, sondern nativ in der Windows-App gebaut ([ADR 0084](0084-alle-masken-nativ-web-fuer-dashboard-und-handy.md), ersetzt [ADR 0053](0053-verwaltung-in-der-windows-app-eingebettet.md)). Die Vorgabe dieses ADR bleibt: zum Start nur die Windows-App, alle Verwaltung darin voll nutzbar, auch im Einzelplatz. „Mein Tag“ und der Kundenbereich werden zu den Handy-Apps für Mitarbeitende und Kunden.
