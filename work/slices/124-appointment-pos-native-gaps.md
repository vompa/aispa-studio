# Slice 124 – Termin und Kassa: Kartengarantie, Anzahlung, Erinnerungsverlauf, Rückerstattung und Gutscheinabfrage nativ

**Ziel:** Was an Termin und Kassa nur im Web liegt, wird nativ: Kartengarantie, Stand der Anzahlung und Erinnerungsverlauf am Termin, Info zur Online-Rückerstattung und Gutscheinabfrage an der Kassa.
**Runde:** R05
**Quelle:** R05 – Ä285
**Hängt ab von:** 94, 96
**Modell:** opus
**IDs für neue Befunde:** Ä290–Ä292

## Umfang
- Termin: Kartengarantie anzeigen und anfordern, Stand der Anzahlung anzeigen (Retournieren besteht schon, kein Knopf „Anfordern“), Erinnerungsverlauf anzeigen
- Kassa: Info zur Online-Rückerstattung, Gutschein abfragen (Restwert, Gültigkeit)
- Web-Stellen über die API-Aufrufe der Team-Oberfläche finden, die in der Windows-App fehlen; dabei weitere Lücken als neue Befunde festhalten
- Masken-Inventar und Hilfe nachziehen

## Muster übernehmen
- Termin-Details im Kalender und `src/AispaStudio.Desktop/Views/PosView.axaml`
- docs/gestaltung/masken.md und docs/gestaltung/meldungen.md

## Voraussichtlich geänderte Dateien
- src/AispaStudio.Desktop/Views/CalendarView.axaml
- src/AispaStudio.Desktop/ViewModels/
- src/AispaStudio.Desktop/Views/PosView.axaml
- docs/umsetzung/masken-inventar.md
- tests/AispaStudio.Desktop.Tests/
- web/src/help/content/

## NICHT bauen
- neue Zahlungsabläufe oder neue Rechenregeln in der App; Beträge und Regeln kommen aus der API
- Entfernen der Web-Masken (macht Slice 103)

## Prompt
```
Lies CLAUDE.md, work/conventions.md, die Rundenakte work/rounds/R05-*.md und diese Slice-Datei.
Setze Slice 124 "Termin und Kassa: Kartengarantie, Anzahlung, Erinnerungsverlauf, Rückerstattung und Gutscheinabfrage nativ" um (diese Datei).
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
- [ ] Given ein Termin mit Anzahlung When seine Details geöffnet werden Then zeigt der Termin ihren Stand
- [ ] Given ein Termin mit versendeten Erinnerungen When seine Details geöffnet werden Then steht dort der Erinnerungsverlauf
- [ ] Given ein gültiger Gutschein When sein Code an der Kassa abgefragt wird Then zeigt die Kassa Restwert und Gültigkeit
- [ ] Manuell: Stand der Anzahlung, Kartengarantie und Gutscheinabfrage als Inhaberin im echten Fenster mit Testdaten durchgehen

## Nicht in dieser Slice
–
