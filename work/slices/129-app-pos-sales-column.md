# Slice 129 – Kassa: eigene Spalte für Artikel und Gutscheine, einklappbar

**Ziel:** Artikel und Gutscheine (Wertgutschein, Behandlung verkaufen, Paket verkaufen) stehen an der Kassa in einer eigenen Spalte ganz links, die sich einklappen lässt; die bisherige linke Spalte mit Code scannen, „Heute abzurechnen“ und geparkten Rechnungen wird schmäler und muss nicht mehr rollen.
**Runde:** R06
**Quelle:** R06 – Ä310
**Hängt ab von:** –
**Modell:** sonnet
**Prüfstufe:** –
**IDs für neue Befunde:** Ä320–Ä322

## Umfang
- Neue Spalte links in `PosView.axaml`: Artikel-Kacheln, „Wertgutschein verkaufen“, „Behandlung verkaufen“, „Paket verkaufen“; die zweite Spalte behält Code scannen, „Heute abzurechnen“ und „Geparkte Rechnungen“ und wird schmäler; Rechnung und Bezahlen bleiben, wie sie sind
- Einklappen über einen Knopf im Spaltenkopf; eingeklappt bleibt eine schmale Leiste mit Symbol und Name, ein Klick klappt wieder auf; der Zustand bleibt je Arbeitsplatz gemerkt (wie andere gemerkte Ansichtszustände der App)
- Tastatur und Screenreader: Knopf in der Tab-Reihenfolge, Name „Artikel und Gutscheine“ und Zustand aufgeklappt/eingeklappt über einen Peer mit `IExpandCollapseProvider` (wie `AccordionHeader`)
- Breiten nach dem Design-Brief; bei 1280 px Fensterbreite bleiben Rechnung und Bezahlen voll nutzbar, die Spalten rollen jede für sich
- Handbuch der Kassa und betroffene Bilder nachziehen

## Muster übernehmen
- src/AispaStudio.Desktop/Views/PosView.axaml – heutige linke Spalte
- src/AispaStudio.Desktop/Controls/AccordionHeader.cs – Peer für Auf- und Zuklappen
- docs/design.md (Kassa) und docs/gestaltung/masken.md
- .claude/skills/fluent2-avalonia (navigation.md, accessibility.md)

## Voraussichtlich geänderte Dateien
- src/AispaStudio.Desktop/Views/PosView.axaml
- src/AispaStudio.Desktop/ViewModels/PosViewModel.cs
- tests/AispaStudio.Desktop.Tests/
- web/src/help/content/
- web/src/help/images/app/

## NICHT bauen
- neue Verkaufsarten oder Änderungen an Rechnung und Bezahlen
- frei verschiebbare oder in der Breite ziehbare Spalten

## Prompt
```
Lies CLAUDE.md, work/conventions.md, die Rundenakte work/rounds/R06-*.md und diese Slice-Datei.
Setze Slice 129 "Kassa: eigene Spalte für Artikel und Gutscheine, einklappbar" um (diese Datei).
1. Stelle zuerst einen Plan vor (Dateien, Klassen, Tests) mit einem Abschnitt
   „Annahmen (bei Freigabe bestätigen)“ für jeden offenen Punkt und einem kurzen „Review-Fokus“,
   speichere ihn unter work/plans/ und warte auf Freigabe.
2. Schreibe die Abnahmetests dieser Slice als fehlschlagende Tests (ROT).
3. Implementiere das Minimum bis grün. Halte jede Regel in CLAUDE.md und work/conventions.md ein.
4. Prüfe die Definition of Done. Halte Entscheidungen, Überraschungen und zurückgestellte
   Kleinigkeiten in der Rundenakte und in der Protokolldatei fest, die work/conventions.md nennt.
```

## Definition of Done
- [ ] Prüfungen aus work/conventions.md grün
- [ ] Kein Test abgeschwächt oder übersprungen
- [ ] Keine neuen Pakete ohne Freigabe
- [ ] Entscheidungen festgehalten
- [ ] Given die Kassa ist offen When man auf „Kassieren“ geht Then stehen Artikel und Gutscheine in einer eigenen Spalte links und daneben Code scannen, „Heute abzurechnen“ und „Geparkte Rechnungen“
- [ ] Given die Spalte ist aufgeklappt When man im Spaltenkopf auf Einklappen klickt Then bleibt eine schmale Leiste und die Rechnung wird breiter
- [ ] Given die Spalte ist eingeklappt When die App neu startet Then ist sie weiter eingeklappt
- [ ] Given der Fokus liegt auf dem Knopf zum Einklappen When man die Leertaste drückt Then klappt die Spalte und der Screenreader meldet „eingeklappt“
- [ ] Manuell: Kassa als Inhaberin bei 1280 px und bei voller Breite öffnen, Spalte ein- und ausklappen, Artikel und Wertgutschein verkaufen

## Nicht in dieser Slice
–
