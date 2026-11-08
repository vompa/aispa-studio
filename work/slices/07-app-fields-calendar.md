# Slice 07 – Feldnamen im Kalender und Zähler „Personen“ als Zahlenfeld

**Ziel:** Alle Felder, Auswahllisten und Kontrollkästchen des Kalenders haben echte Namen, und „Personen“ ist ein Zahlenfeld.
**Runde:** R01
**Quelle:** R01 – W51, W52, W55, W-Muster 1 (Teil)
**Hängt ab von:** 05
**Modell:** –

## Umfang
- 26 Felder ohne FormField in CalendarView mit Beschriftung über `ui:FormField` oder Verbindung zum vorhandenen `TextBlock.label`; die 13 ComboBoxen ohne Namen aus W51 (Serienintervall, Freigabe, Freie Zeit, Behandlung, Gruppenbuchung, Bereich, Grund, Standort)
- Felder der Maske „Neuer Termin“ (:1312-1316, :1341) bekommen Namen; der Umbau der Maske bleibt draußen
- Kontrollkästchen der Serienvorschau und des Ausfall-Assistenten mit Namen aus dem Datum (W52)
- Zähler „Personen“ (:1605-1616) als `ui:NumberField ShowSpinner="True"` (W55)

## Muster übernehmen
- src/AispaStudio.Desktop/Views/CalendarView.axaml:51 – NumberField mit Pfeilspalte
- src/AispaStudio.Desktop/Controls/FormField.cs:27 – FormField

## NICHT bauen
- W54 Umbau der Maske „Neuer Termin“ (bekannt, masken.md §8)
- Primärknöpfe im Termin-Panel (Slice 21)

## Prompt
```
Lies CLAUDE.md, work/conventions.md, die Rundenakte work/rounds/R01-*.md und diese Slice-Datei.
Setze Slice 07 "Feldnamen im Kalender und Zähler „Personen“ als Zahlenfeld" um (diese Datei).
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
- [ ] Given CalendarView mit offenem Panel „Neuer Eintrag“, offener Gruppenbuchung und offener Freien-Zeit-Suche When der Namens-Scan läuft Then hat jedes Feld einen Namen, der nicht aus dem Platzhalter stammt
- [ ] Given die Gruppenbuchung mit 2 Personen When der Fokus im Feld „Personen“ steht und Pfeil hoch gedrückt wird Then steht dort 3
- [ ] Given die Serienvorschau mit drei Terminen When der Scan läuft Then trägt jedes Kontrollkästchen sein Datum als Namen
- [ ] Manuell: Kalender › Neuer Termin › Gruppe: „Personen“ mit Pfeil hoch und runter ändern

## Nicht in dieser Slice
Neue Felder
