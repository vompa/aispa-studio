# Slice 89 – Hover-Karten und Popups lesbar und einheitlich

**Ziel:** Hover-Karten und Popups der Windows-App zeigen Gast, Behandlung, Tag und Zeit sowie Raum groß und fett, kürzen auf das Nötige und folgen einem Muster.
**Runde:** R04
**Quelle:** R04 – Ä158
**Hängt ab von:** –
**Modell:** sonnet
**IDs für neue Befunde:** Ä180–Ä182

## Umfang
- Hover-Karte am Termin: Gast, Behandlung, Tag und Uhrzeit, Raum in Grundschrift fett; Person normal (in der Raumansicht die Person hervorgehoben)
- Kürzen: Besuche, Nicht erschienen, Ablauf nur, wenn sie etwas sagen („Nicht erschienen“ ab 1× als Warnung); Rest im Termin-Panel
- Fehler „Nicht erschienen1×“ (Etikett überlappt die Wertespalte)
- Alle Hover-Karten, Tooltips mit Inhalt und Flyouts durchsehen (Kalender, Raumansicht, Kunden, Kassa, Meldungen) und einheitlich gestalten; Muster im Design-Brief und in `meldungen.md`; Bildtests hell und dunkel

## Muster übernehmen
- src/AispaStudio.Desktop/Views/CalendarView.axaml – Hover-Karte am Termin
- docs/design.md – Wertequelle

## Voraussichtlich geänderte Dateien
- src/AispaStudio.Desktop/Views/
- docs/design.md

## NICHT bauen
- Neue Angaben in der Hover-Karte

## Prompt
```
Lies CLAUDE.md, work/conventions.md, die Rundenakte work/rounds/R04-*.md und diese Slice-Datei.
Setze Slice 89 "Hover-Karten und Popups lesbar und einheitlich" um (diese Datei).
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
- [ ] Given die Hover-Karte eines Termins When sie aufgeht Then stehen Gast, Behandlung, Tag mit Uhrzeit und Raum fett in Grundschrift
- [ ] Given ein Gast ohne Ausfälle When die Hover-Karte aufgeht Then fehlt die Zeile „Nicht erschienen“
- [ ] Given ein Gast mit 1 Ausfall When die Hover-Karte aufgeht Then überlappen Etikett und Wert nicht
- [ ] Manuell: Hover über Termin: Hauptangaben groß und fett, nichts überlappt

## Nicht in dieser Slice
–
