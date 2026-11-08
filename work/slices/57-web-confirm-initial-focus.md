# Slice 57 – Rückfragedialog: Startfokus nach Brief 5.27

**Ziel:** Der gemeinsame Rückfragedialog setzt den Startfokus nach Brief 5.27: bei zerstörenden Aktionen auf „Abbrechen“, bei neutralen Rückfragen auf den primären Knopf, mit Eingabefeld ins Feld.
**Runde:** R03
**Quelle:** R03 – Ruling Slice 38 aus R02
**Hängt ab von:** –
**Modell:** sonnet
**IDs für neue Befunde:** –

## Umfang
- Option am Baustein `ConfirmDialog` für den Startfokus (Abbrechen, Bestätigen, erstes Feld); ohne Angabe folgt sie `danger` (zerstörend: Abbrechen, sonst Bestätigen)
- Neutrale Rückfragen (Aktion und Betriebsinformation archivieren, Abo-Stufe ändern) auf den primären Knopf, Rückfragen mit Feld (Anzahlung retournieren, Termine absagen mit Nachricht) ins Feld
- Den Abweichungsvermerk aus Slice 38 in der Rundenakte R02 und, falls vorhanden, in `docs/design.md` und `docs/gestaltung/meldungen.md` streichen

## Muster übernehmen
- web/src/components/ConfirmDialog.tsx:51 – heutiger Startfokus auf „Abbrechen“
- src/AispaStudio.Desktop/Views/ConfirmDialogView.axaml.cs – Rückfrage mit Feld fokussiert das Feld (R01 Slice 28)

## NICHT bauen
- Neue Rückfragen
- Änderungen an den Texten der Rückfragen

## Prompt
```
Lies CLAUDE.md, work/conventions.md, die Rundenakte work/rounds/R03-*.md und diese Slice-Datei.
Setze Slice 57 "Rückfragedialog: Startfokus nach Brief 5.27" um (diese Datei).
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
- [ ] Given die Rückfrage „Schließzeit löschen?“ When sie sich öffnet Then liegt der Fokus auf „Abbrechen“
- [ ] Given die Rückfrage „Aktion archivieren?“ When sie sich öffnet Then liegt der Fokus auf „Archivieren“
- [ ] Given die Rückfrage „Anzahlung retournieren?“ mit dem Feld „Grund“ When sie sich öffnet Then liegt der Fokus im Feld „Grund“
- [ ] Manuell: Aktionen › eine Aktion archivieren: die Rückfrage öffnet mit dem Fokus auf „Archivieren“, Esc schließt ohne Archivieren

## Nicht in dieser Slice
–
