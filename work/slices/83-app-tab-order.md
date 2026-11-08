# Slice 83 – Tab-Reihenfolge in allen Masken

**Ziel:** Tab erreicht in jeder Maske der Windows-App jedes bedienbare Element genau einmal, vorwärts wie rückwärts, in sichtbarer Reihenfolge.
**Runde:** R04
**Quelle:** R04 – Ä146, Ä147
**Hängt ab von:** 82
**Modell:** opus
**IDs für neue Befunde:** Ä162–Ä164

## Umfang
- Fehler nachstellen und beheben: in „Neuer Termin“ kommt Tab von oben nicht über „Ändern“ (gewählter Gast) hinaus, Umschalt+Tab geht (Ä146)
- Scan-Test „Jede_Seite_…Tab…“ über alle Seiten, Seitenpanels und Dialoge: Tab vorwärts und rückwärts durchlaufen, jedes sichtbare, bedienbare Element genau einmal, Reihenfolge wie die Lage im Bild (oben nach unten, links nach rechts), kein Hängenbleiben (Ä147)
- Gefundene Stellen beheben (Quelltextreihenfolge, `IsTabStop`, `KeyboardNavigation.TabNavigation`), Fallen aus pitfalls (TabIndex wirkt nicht, DockPanel) beachten

## Muster übernehmen
- tests/AispaStudio.Desktop.Tests/ViewTests.cs – Scan-Tests „…jeder_Seite…“ und `Owner()`
- src/AispaStudio.Desktop/Views/CalendarView.axaml – DataTemplate BookingViewModel

## Voraussichtlich geänderte Dateien
- src/AispaStudio.Desktop/Views/
- tests/AispaStudio.Desktop.Tests/

## NICHT bauen
- Neue Tastenkürzel

## Prompt
```
Lies CLAUDE.md, work/conventions.md, die Rundenakte work/rounds/R04-*.md und diese Slice-Datei.
Setze Slice 83 "Tab-Reihenfolge in allen Masken" um (diese Datei).
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
- [ ] Given „Neuer Termin“ mit gewähltem Gast When Tab von der Kundensuche aus gedrückt wird Then folgt nach „Ändern“ der Vorschlag, dann „Behandlung“, „Freie Zeit suchen“, „Datum“, „Beginn“, „Notiz“, „Termin buchen“
- [ ] Given jede Seite und jedes Seitenpanel der Windows-App When Tab und Umschalt+Tab einmal ganz durchlaufen Then wird jedes sichtbare bedienbare Element genau einmal erreicht
- [ ] Manuell: Neuer Termin mit gewähltem Gast: mit Tab bis „Termin buchen“ und zurück

## Nicht in dieser Slice
–
