# 0069. Fluent 2 als Grundlage der Windows-App

- **Status:** angenommen
- **Datum:** 21.10.2026
- **Entschieden von:** Auftraggeber (Freigabe der Empfehlungen aus Abschnitt 6 der Ableitung und drei Einzelentscheidungen, 21.10.2026), Ausgestaltung Claude

## Kontext

Der Auftraggeber wollte für die Windows-App ein einheitliches Verhalten der Eingabefelder (Betreten, Tippen, Pfeiltasten, Esc, Enter, Fehler) und Regeln für den verantwortungsvollen Einsatz von KI, und zwar nicht aus dem Bauch, sondern von Microsoft: „lies einfach das ganze, alle Themen, und leite daraus alles ab“. Daraus entstand [docs/gestaltung/fluent2-ableitung.md](../gestaltung/fluent2-ableitung.md): Fluent 2 (fluent2.microsoft.design), die WinUI-3-Seiten von Microsoft Learn und der offene Quelltext von Fluent UI sind vollständig gelesen (18 Themenseiten, alle 47 Web-Komponenten, 31 Learn-Seiten, Token und Komponenten-Quelltext). Die Ableitung ändert den [Design-Brief](../design.md) nicht selbst; ihr Abschnitt 6 listet 26 Abweichungen mit einer Empfehlung je Zeile („Brief anpassen“, „ergänzen“, „behalten“, „entfernen“, „der Auftraggeber entscheidet“).

Der Auftraggeber hat die Empfehlungen aus Abschnitt 6 freigegeben und drei offene Punkte entschieden (siehe Entscheidung).

## Optionen

| Option | Bewertung |
| --- | --- |
| Feldverhalten und Bedienung weiter einzeln entscheiden, Brief unverändert | Jede Sitzung entscheidet neu, die Zeit-, Datums- und Betragsfelder bleiben Textfelder ohne Regeln |
| Fluent 2 1:1 übernehmen (Radius 4, Höhe 32, Segoe UI Variable, 7 s Toast, Hover heller) | Verliert Marke, Raster und die gleiche Oberfläche in App und Web |
| Fluent 2 als Grundlage für Verhalten und Zustände, der Brief bleibt die Wertequelle, Abweichungen stehen mit Grund im Brief | gewählt |
| Feste Maskeneingabe für Uhrzeit und Datum (`__:__`) | Erschwert Einfügen und Korrigieren, die Rezeption tippt schnell; verworfen zugunsten tolerantem Lesen |
| Vorlage „design.md“ aus dem Marktplatz übernehmen | Verworfen: enthält nur Farbwerte, kein Verhalten und keine Zustände; der Brief hat die Farben schon (Abschnitt 2) |

## Entscheidung

- **Fluent 2 (WinUI 3) ist die Grundlage für Verhalten und Zustände der Windows-App.** Der [Design-Brief](../design.md) bleibt die verbindliche Wertequelle: Wo er etwas festlegt, gilt der Brief; wo er schweigt, gelten die Fluent-Werte aus der Ableitung, nachdem sie als Abschnitt in den Brief eingetragen sind. Jede Abweichung von Fluent 2 steht im Brief als „gewollte Abweichung von Fluent 2“ mit einem Satz Grund (Brief, Abschnitt 0.1).
- **Der Brief ist angepasst** (Zeilen aus Abschnitt 6 der Ableitung):
  - Angepasst bzw. ergänzt (Zeilen 3, 4, 7, 8, 9, 10, 12, 16, 17, 18, 19, 23): Fokus-Unterstrich 2 px accent an Eingabe- und Auswahlfeldern (auch bei Mausfokus; 200 ms ein, 50 ms aus); Schriftstufen Mini, Klein und Beschriftung 12, Zwischentitel 16/22 Semibold; Feldunterkante 1 px fg-3 und korrigierte Kontrastwerte (fg-3 4,18:1 hell, 5,09:1 dunkel, „großer Text“ ab 24 px normal oder 18,5 px fett); Abstände 2, 6, 10 erlaubt; Hover und Fokus 100 ms, Kurven `(0.33,0,0.67,1)`, `(0,0,0,1)`, `(1,0,1,1)`; Tooltip mit Grund an deaktivierten Knöpfen; Toast unter 60 Zeichen ohne Punkt (Dauer 5 s und mittig bleiben); Rückfragedialog auch in der App mit Innenabstand 24 und der Reihenfolge Aktion links, Abbrechen rechts; Überlauf-Menü „⋯“ in der Kalender-Kopfzeile statt waagrechtem Scrollen; `TextBlock.overline` verboten.
  - Neue Komponentenabschnitte: Verhalten der Eingabefelder, Datumsfeld, Uhrzeitfeld, Zahlenfeld, Betragsfeld, Menü und Kontextmenü, Tooltip, Schalter, Link, Fortschrittsbalken und Skeleton, Tag-Picker, Rückfragedialog (Brief 5.16 bis 5.27).
  - Als gewollte Abweichung von Fluent 2 mit Grund vermerkt (Zeilen 1, 2, 5, 6, 11, 13, 14, 15, 20, 21, 22, 24, 25): Radien 8 und 12, Höhe 34, Gewicht 500, Schrift Inter, Auswahl nach 4 und 12 Werten, Pflichtstern in Textfarbe, Hover dunkler statt heller, Hilfetext mit Punkt, WCAG 2.2, Symbolgrößen, Reiter als Schiene, Einheit in der Beschriftung, Karte und Tabelle im Raster des Briefs.
- **Drei Einzelentscheidungen vom Auftraggeber:**
  - Schriftstufen: der **Titel bleibt 22/28**; Mini und Klein werden 12, der Zwischentitel 16/22 Semibold, die Überschrift 18/24 bleibt.
  - Fokus bei Formularfehler: auf das **erste ungültige Feld** (wie masken.md R8); die InfoBar wird zusätzlich angezeigt und nimmt den Fokus nicht. Fluent 2 würde der Leiste den Fokus geben.
  - Uhrzeitfelder: **tolerant lesen und nach dem Verlassen normalisieren** (`930` wird `09:30`), keine feste Maske `__:__`; eine ungültige Eingabe ergibt einen Fehlertext, nie einen Absturz und nie stilles Runden. Dasselbe Grundprinzip gilt für Datum, Zahl und Betrag.
- **Umsetzung auf einem eigenen Branch `fluent2`**, damit sie zurücknehmbar bleibt. Die Punkte F1 bis F25 der Ableitung stehen als „offen“ in Brief 11.4.
- **Skill `fluent2-avalonia`** ist die wiederverwendbare Arbeitsanweisung für den Umbau und wird in `.claude/skills/` abgelegt; Quelle und Pflege ist das eigene Repository `fluent2-avalonia-skill`. Er ist nicht Teil dieses Commits.
- **Nicht Teil dieser Entscheidung:** F15 (Reiter-Tastatur), F26 bis F30 (Hochkontrast, F6-Bereichswechsel, Narrator-Prüfung, Tastenkürzel, KI-Kennzeichnung) und der Abschnitt „Responsible AI“ der Ableitung. Sie bleiben Vorschläge, bis der Auftraggeber sie freigibt.

## Begründung

Fluent 2 und WinUI 3 beschreiben genau das, was dem Brief fehlte: Zustände, Tastaturbedienung, Feldverhalten und Inhalt, nicht nur Farben und Maße. Der Brief behält die Hoheit über Werte, weil die Marke (Teal, Inter, weiche Radien, 34 px hohe Bedienelemente) und die gleiche Oberfläche in App und Web ([ADR 0004](0004-oberflaechen-avalonia-und-react.md)) wichtiger sind als ein 1:1-Nachbau von Microsoft. Jede Abweichung mit Grund aufzuschreiben macht sie prüfbar und verhindert, dass spätere Sitzungen sie stillschweigend „korrigieren“. Das tolerante Lesen der Uhrzeit passt zur Rezeption, die schnell tippt; die Fehlerregel („nie Absturz, nie stilles Runden“) folgt aus [ADR 0010](0010-kassa-compliance-by-design.md) und dem Grundsatz, dass Pflichtzahlen nie stumm verändert werden.

## Folgen und Stand

- Geändert sind nur Dokumente: `docs/design.md` (Abschnitt 0.1, 2.1, 2.6, 3, 4.1 bis 4.7, 5 bis 5.27, 6, 7, 9 bis 13, neu 11.4), `docs/gestaltung/meldungen.md` (M1, M2, Checkliste), `docs/gestaltung/masken.md` (R8, R12, R13, Checkliste), `CLAUDE.md`, dieses Protokoll. **Kein Code, keine Token-Werte:** der Token-Vergleichstest (`DesignTokenTests`, `designTokens.test.ts`) prüft nur die Farben aus Brief 2.1 bis 2.4, und diese ändern sich nicht (`fg-3` bleibt `#757e7a` / `#7e8f8a`; die Feldunterkante nutzt `fg-3` und braucht kein neues Token).
- Der Code (Styles, Steuerelemente, Tests) ist nicht umgebaut; der Umbau läuft auf `fluent2` (Brief 11.4). Bis dahin entspricht der Code teils den alten Werten (11,5 / 12,5 / 15 px, 120 ms, Radius, Textfelder für Zeit und Datum); der Brief gilt für jede neue und jede angefasste Stelle.
- **Offene Punkte:** Die Ableitung schlägt zusätzliche Schattenstufen für Popups und Dialog vor; Werte nennt sie nicht, sie sind nicht freigegeben. Menü, Auswahlliste, Kalender-Popover und Tooltip tragen deshalb den bestehenden Schatten `shadow`, der Rückfragedialog keinen. Popups erscheinen ohne Bewegung (keine Dauer in der Ableitung). Der Fokus-Unterstrich ist für die Windows-App festgelegt, im Web nicht. Skeleton ist statisch, Ladespinner bleiben verboten (Ableitung nannte einen Ring für blockierende Vorgänge: stattdessen unbestimmter Balken).
- **Grenzen der Quellen:** Zahlen von fluent2.microsoft.design stammen aus Zusammenfassungen des Abrufwerkzeugs, nicht aus dem Wortlaut; Komponenten-Quelltext ist Fluent UI React (Web), nicht das Windows-Aussehen. Alle Feldregeln für Uhrzeit, Datum, Zahl und Betrag sind unsere Ableitung, Fluent liefert nur die Bedienprinzipien. Es gibt keinen visuellen Vergleich mit Fluent-Komponenten (Ableitung, Abschnitt 8).

## Nachtrag 22.10.2026: Microsofts Fluent-Steuerelemente geprüft und verworfen

Der Auftraggeber hat gefragt, wie viel Aufwand die Fluent-2-XAML-Steuerelemente von Microsoft wären. Befund: Microsoft liefert sie nur für WinUI 3 (Windows App SDK), nicht für Avalonia. Drei Wege wurden abgewogen: (1) Umstellung der Windows-App auf WinUI 3 (Neubau der Oberflächenschicht samt eigener Steuerelemente und rund 1.300 Headless-Tests, kein Headless-Testen in WinUI, nur Windows, Wochen bis Monate, hohes Risiko; der Brief weicht ohnehin bewusst von Fluent-Werten ab), (2) FluentAvalonia als Community-Port einzelner WinUI-Steuerelemente (Tage je Steuerelement, Theme-Werte des Briefs müssten erneut überlagert werden, Abhängigkeit von einem Einzelprojekt), (3) beim jetzigen Weg bleiben: Avalonias Fluent-Theme plus Brief plus Skill `fluent2-avalonia`, Verhalten und Zustände nach Fluent 2, Optik nach Brief.

**Entscheidung (Auftraggeber): Weg 3.** Kein Wechsel auf WinUI 3, kein Theme-Wechsel. FluentAvalonia bleibt die Option für ein einzelnes fehlendes Steuerelement (z. B. Teaching tip), zu prüfen je Fall gegen Avalonia 12. Fluent UI React v9 für die Web-Verwaltung ist aus denselben Gründen nicht vorgesehen.

## Quellen

- [Ableitung Fluent 2 für die Windows-App](../gestaltung/fluent2-ableitung.md), Abschnitt 6 (Abweichungen) und Abschnitt 7 (Umsetzungsliste)
- [Design-Brief](../design.md), Abschnitt 0.1 und 11.4
- [ADR 0067](0067-masken-und-meldungen-nach-material-3-und-fluent-2.md) (Masken und Meldungen), [ADR 0066](0066-seitenleisten-ein-muster-und-ki-wirkt-auf-die-offene-seite.md) (Seitenleisten)
- Fluent 2: https://fluent2.microsoft.design; WinUI 3 auf Microsoft Learn; Fluent UI (`microsoft/fluentui`)
