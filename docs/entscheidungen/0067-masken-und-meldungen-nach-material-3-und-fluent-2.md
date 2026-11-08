# 0067. Masken und Meldungen nach Material 3 und Fluent 2

- **Status:** angenommen
- **Datum:** 21.10.2026
- **Entschieden von:** Auftraggeber (Aufträge vom 21.10.2026), Ausgestaltung Claude

## Kontext

Auftraggeber zur Kundendaten-Maske: „Viel zu unruhig – schau nach, was Google aktuell für Masken-Design vorschlägt, und erstelle daraus Vorgaben für die gesamte Anwendung.“ Und zum Kalender: „Der Meldungsbereich im Kalender gefällt mir nicht – schau bei Microsoft und Google, was für Meldungen vorgeschlagen wird.“

Die Masken der Windows-App und der Verwaltung hatten keine gemeinsamen Regeln für Gruppen, Raster, Abstände, Hilfe-Fragezeichen, Pflichtfelder und Fehler; die Kundendaten-Maske mischt 18 Chips, vier Fragezeichen und sieben Spaltenaufteilungen. Meldungen sind im Kalender auf rote Fehlerblöcke, eine gelbe Tagesleiste mit Notizen und Warnungen, eine Betriebsleiste mit Pillen und einen Toast mit bis zu vier Aktionen verteilt.

## Optionen

| Option | Bewertung |
| --- | --- |
| Masken und Meldungen weiter einzeln gestalten | Weiter Unruhe, jede Sitzung entscheidet neu |
| Vorgaben aus Material 3 und Fluent 2 (und Microsoft „Forms“, Google web.dev) als verbindliche Dokumente im Repository | gewählt: prüfbar, eine Quelle für Windows-App und Web |
| Ein fremdes Komponentenpaket (z. B. Material-Komponenten) einbauen | verlässt den Fluent-Stil der Windows-App ([ADR 0059](0059-zum-start-nur-die-windows-app.md)), Aufwand ohne Gewinn |

## Entscheidung

- Neue und geänderte Masken folgen [docs/gestaltung/masken.md](../gestaltung/masken.md), Meldungen folgen [docs/gestaltung/meldungen.md](../gestaltung/meldungen.md). Jeder Pull Request mit einer Maske oder Meldung hakt die Kurz-Checkliste des jeweiligen Dokuments ab.
- Kernregeln Masken: Gruppen mit Überschrift nach Häufigkeit; Seltenes eingeklappt („Weitere Angaben“); eine Hauptspalte, Paare nur für Kurzfelder, festes Raster mit Feldbreite nach Eingabe; Beschriftung über dem Feld, Hilfetext darunter; Pflicht mit Stern; Fehler am Feld; höchstens ein Hilfe-Fragezeichen je Gruppe (die Hilfe-Pflicht nach [ADR 0055](0055-hilfe-zu-jeder-einstellung-im-repository.md) bleibt: jedes Feld steht im Hilfethema); Auswahl mit vielen Werten als Eingabe mit Vorschlagsliste statt Chips; eine primäre Aktion je Maske, unten links bzw. fest im Fuß; Abstände im 4er-/8er-Raster aus `Styles.axaml`.
- Kernregeln Meldungen: Toast unten, eine Zeile, höchstens eine Aktion, 5 s, Fehler bleiben; InfoBar oben über dem Inhalt mit Schweregrad, Symbol und Farbe, schließbar, höchstens zwei sichtbar plus „n weitere“; Hinweise zu einem Objekt am Objekt; Tagesnotizen getrennt von Warnungen; Texte mit Problem und Lösung, ohne Schuld.
- Windows-App: Farben und Steuerelemente im Fluent-Stil; Aufbau, Raster und Verhalten nach den Vorgaben. Verwaltung (Web): dieselben Regeln mit nativen Elementen (`fieldset`, `details`, `dialog`, `datalist`).
- Altbestand wird nicht auf einmal umgebaut: beim nächsten Anfassen einer Maske im selben Pull Request nachziehen.

## Begründung

Material 3 und Fluent 2 sind die beiden Systeme, die der Auftraggeber genannt hat und die zur Windows-App passen; Microsofts „Forms“-Richtlinie und Googles web.dev decken sich bei Beschriftung über dem Feld, Pflichtkennzeichnung, Fehler am Feld, einspaltigem Aufbau und Vorschlagslisten. Feste Regeln machen Masken prüfbar und ruhig, ohne jede Entscheidung neu zu führen.

## Folgen und Stand

- Dokumente: `docs/gestaltung/masken.md` (mit Umbauvorschlag für die Kundendaten-Maske, Abschnitt 7) und `docs/gestaltung/meldungen.md` (mit Skizze für den Kalender, Abschnitt 4). Code ist noch nicht umgebaut.
- **Grenzen der Recherche:** Die Seiten von m3.material.io (Text fields, Snackbar, Banner) sind JavaScript-Seiten und waren nicht lesbar; M3-Aussagen stammen aus Material Web, Material 2 und Zweitquellen. Fluent-2-Seiten zu Message bar und Toast sowie Microsofts „Forms“ und web.dev wurden vollständig gelesen. Zahlen wie 5 Sekunden für Toasts, „höchstens zwei sichtbar“ und die Abstände 16/32 px sind Projektfestlegungen auf dieser Grundlage und sind im Fenster (auch bei 125 und 150 Prozent Skalierung) zu prüfen.
- Offen: `InfoBar`-Steuerelement, Umbau der Kundendaten-Maske und des Kalender-Meldungsbereichs, danach Handbuchbilder neu ([ADR 0056](0056-handbuch-mit-ablaeufen-und-bildern-als-grundlage-der-ki-hilfe.md)).

## Quellen

- [Microsoft: Forms](https://learn.microsoft.com/windows/apps/design/controls/forms), [Labels](https://learn.microsoft.com/windows/apps/design/controls/labels)
- [Fluent 2: Message bar](https://fluent2.microsoft.design/components/web/react/core/messagebar/usage), [Toast](https://fluent2.microsoft.design/components/web/react/core/toast/usage)
- [Google web.dev: Form design basics](https://web.dev/learn/forms/design-basics)
- [Material Web: Text field](https://material-web.dev/components/text-field), [Material: Snackbars](https://material.io/components/snackbars), [Banners](https://material.io/components/banners)
- [Material Design 3: Text fields](https://m3.material.io/components/text-fields/guidelines) (nicht lesbar abgerufen)

## Nachtrag 21.10.2026: Abgleich mit dem Design-Brief

Der Design-Brief [docs/design.md](../design.md) (am selben Tag bestellt, auf `dev` entstanden) und diese Entscheidung widersprachen sich in 24 Punkten. Aufgelöst ist so, dass es **eine Quelle** gibt:

- **Werte** (Größen, Abstände, Feldhöhe, Farben, Radien, Dauer, Knopfarten) stehen nur in `design.md`. Die Angaben in dieser Entscheidung ([masken.md](../gestaltung/masken.md), [meldungen.md](../gestaltung/meldungen.md)) gelten dort, wo `design.md` nichts anderes sagt; bei einem Widerspruch gilt `design.md`. Das betrifft vor allem „Abstände im 4er-/8er-Raster aus `Styles.axaml`“ (jetzt 12/12/20 nach `design.md` 4.1), die Feldhöhe (34 px), den Hilfetext (`Fg2`, nicht `Fg3`) und die Farben der Meldungen.
- **Aufbau und Verhalten** bleiben wie hier entschieden und stehen in `design.md` (Abschnitte 5.14 und 5.15, Hilfe-Knopf 5.11, Toast 5.10) nur als Zusammenfassung: Gruppen mit höchstens einem Hilfe-Fragezeichen, Seltenes eingeklappt, Fehler am Feld, Toast eine Zeile mit höchstens einer Aktion (5 s, mit „Rückgängig“ 8 s, Warnung und Fehler bleiben), InfoBar mit Schließen-Kreuz und höchstens zwei sichtbar, Tagesnotizen neutral und getrennt von Warnungen.
- Die Widerspruchstabelle steht im [PR-Protokoll](../umsetzung/pr-protokoll.md) (Abschnitt „ADR 0067 mit dem Design-Brief abgeglichen“). Code, der noch von `design.md` abweicht (InfoBar, Kundendaten-Maske, Toast), steht als A15 bis A17 in der Umstellungsliste (`design.md` 11.2).
