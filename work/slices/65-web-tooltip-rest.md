# Slice 65 – Restliche Hinweise auf den Tooltip-Baustein, Kopieren mit Rückmeldung

**Ziel:** Hinweise, die noch nur im `title` stehen, erscheinen über den Tooltip-Baustein; Kopieren des Einladungslinks meldet sich und funktioniert ohne Zwischenablage.
**Runde:** R03
**Quelle:** R03 – B74, B75, B86
**Hängt ab von:** 60
**Modell:** sonnet
**IDs für neue Befunde:** B112–B115

## Umfang
- `title`-Hinweise an `span`, `td`, `li` und Spaltenköpfen auf `Tooltip` umstellen: `closures/CancelAppointmentsDialog.tsx`, `ClosuresPage.tsx`, Mitteilungstabelle, `admin/LocationsTab.tsx`, `admin/RebookSection.tsx`, `assistant/AnswerBlock.tsx`, KI-Pille (B74, B86)
- „Einladungslink kopieren“ (`admin/StaffPage.tsx`) meldet „Link kopiert.“ mit `role="status"`; ohne Zwischenablage steht der Link markierbar da; „Diesen Browser als Empfangsgerät anmelden“ nennt den Sperrgrund (B75)
- Den Prüftest aus Slice 45 auf `title` an `span`, `td`, `li` und `th` erweitern

## Muster übernehmen
- web/src/components/Tooltip.tsx:76 – Tooltip-Baustein (Slice 45)

## NICHT bauen
- Reine Namens-`title` umstellen

## Prompt
```
Lies CLAUDE.md, work/conventions.md, die Rundenakte work/rounds/R03-*.md und diese Slice-Datei.
Setze Slice 65 "Restliche Hinweise auf den Tooltip-Baustein, Kopieren mit Rückmeldung" um (diese Datei).
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
- [ ] Given die Quelltexte When ein Prüftest erklärende `title` an `span`, `td`, `li` oder `th` sucht Then gibt es keinen Treffer
- [ ] Given „Einladungslink kopieren“ When geklickt wird Then meldet eine `role="status"`-Region „Link kopiert.“
- [ ] Given ein Browser ohne Zwischenablage When „Einladungslink kopieren“ geklickt wird Then steht der Link markierbar im Feld
- [ ] Manuell: Personal › Einladen › „Einladungslink kopieren“: „Link kopiert.“ erscheint, der Link liegt in der Zwischenablage

## Nicht in dieser Slice
–
