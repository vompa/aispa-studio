# Slice 39 – Fehlende Rückfragen im Web

**Ziel:** Unumkehrbare Aktionen in Personal, Kasse, Zeiterfassung und Buchhaltung fragen vorher nach.
**Runde:** R02
**Quelle:** R02 – B35, B40, B52, B57
**Hängt ab von:** 38
**Modell:** sonnet

## Umfang
- „Zurückziehen“ einer Einladung mit Rückfrage und Fehlertext (B35)
- „Tagesabschluss“ mit Rückfrage samt gezähltem Bestand (B40)
- Zeit nachtragen: „Grund“ als Pflicht markiert, Hinweis „Endet am Folgetag“, Rückfrage vor dem Entfernen (B52)
- „Auf Standard zurücksetzen“ in der Buchhaltung mit Rückfrage und Folge (B57)

## Muster übernehmen
–

## NICHT bauen
–

## Prompt
```
Lies CLAUDE.md, work/conventions.md, die Rundenakte work/rounds/R02-*.md und diese Slice-Datei.
Setze Slice 39 "Fehlende Rückfragen im Web" um (diese Datei).
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
- [ ] Given eine offene Einladung When „Zurückziehen“ gedrückt und die Rückfrage abgebrochen wird Then geht kein Aufruf an die API
- [ ] Given gezählt 245,50 € When „Tagesabschluss“ gedrückt wird Then nennt die Rückfrage 245,50 €
- [ ] Given eine eigene Zuordnung für AT When „Auf Standard zurücksetzen“ gedrückt wird Then nennt die Rückfrage „Die eigene Zuordnung für AT geht verloren.“
- [ ] Manuell: Kassa › Tagesabschluss: Rückfrage mit dem gezählten Betrag erscheint, Abbrechen ändert nichts

## Nicht in dieser Slice
–
