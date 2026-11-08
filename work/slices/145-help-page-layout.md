# Slice 145 – Hilfe: ruhiges Layout mit Bereichen und Inhaltsspalte

**Ziel:** Die Hilfe ordnet die Themen nach Bereichen mit Rollenfilter, zeigt den Inhalt in einer lesbaren Spalte und das Inhaltsverzeichnis daneben statt davor.
**Runde:** R07
**Quelle:** R07 – Ä354
**Hängt ab von:** –
**Modell:** opus
**Prüfstufe:** –
**IDs für neue Befunde:** Ä379–Ä381

## Umfang
- Linke Navigation (etwa 280 px): Suche oben, Chip „Meine Rolle: …“ als Filter (abschaltbar), Gruppen nach Bereich (Erste Schritte, Kalender, Kassa, Kunden, Verwaltung …), einklappbar; jedes Thema einmal; einheitliche Einrückung, aktiver Eintrag als Pille, keine Unterstreichung
- Inhalt höchstens etwa 720 px breit mit großzügigem Abstand oben; Brotkrumen als schlichter kleiner Text; Rollen als Chips unter dem Titel
- „Auf dieser Seite“ rechts als schmale, mitlaufende Liste mit markiertem Abschnitt; zu schmal (etwa mit offenem Panel) → aufklappbares „Inhalt“ über dem Text
- Schritte als nummerierte Blöcke, Bilder mit abgerundeten Ecken, Hinweise und „Wenn etwas schiefgeht“ als Hinweiskästen
- Links im Fließtext unterstrichen, in Navigation und Inhaltsverzeichnis nicht; ein Scrollbereich je Spalte
- Web nach modern-web-guidance und fluent2-web, Werte aus docs/design.md; die Kundenseite bleibt unter 100 kB gzip

## Muster übernehmen
- src/AispaStudio.Desktop/Views/HandbookView.axaml
- src/AispaStudio.Desktop/ViewModels/HandbookViewModel.cs
- web/src/help/HelpTopicParts.tsx
- web/src/help/README.md (Format, Rollen je Thema)
- .claude/skills/fluent2-web

## Voraussichtlich geänderte Dateien
- src/AispaStudio.Desktop/Views/HandbookView.axaml
- src/AispaStudio.Desktop/ViewModels/HandbookViewModel.cs
- web/src/help/
- tests/AispaStudio.Desktop.Tests/
- docs/design.md

## NICHT bauen
- neue oder umgeschriebene Hilfetexte
- eine zweite Hilfe-App

## Prompt
```
Lies CLAUDE.md, work/conventions.md, die Rundenakte work/rounds/R07-*.md und diese Slice-Datei.
Setze Slice 145 "Hilfe: ruhiges Layout mit Bereichen und Inhaltsspalte" um (diese Datei).
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
- [ ] Given das Thema „Status setzen“ gilt für Rezeption und Therapie When man die Navigation öffnet Then steht es genau einmal unter „Kalender“
- [ ] Given als Rezeption angemeldet When man die Hilfe öffnet Then filtert der Chip „Meine Rolle: Rezeption“ und lässt sich auf „Alle“ stellen
- [ ] Given ein Thema mit 15 Abschnitten bei 1920 px When man es öffnet Then steht das Inhaltsverzeichnis rechts und der erste Abschnitt ohne Scrollen sichtbar
- [ ] Given dasselbe Thema bei 1280 px mit offenem Panel When man es öffnet Then ist das Inhaltsverzeichnis ein zugeklapptes „Inhalt“ über dem Text
- [ ] Manuell: Als Rezeption und als Inhaberin „Termin buchen“ und „Gutschein“ suchen und lesen, bei 1280 px und voller Breite, mit und ohne „KI und Hilfe“

## Nicht in dieser Slice
–
