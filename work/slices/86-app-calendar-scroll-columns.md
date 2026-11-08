# Slice 86 – Kalender: Scrollstelle erhalten, weitere Spalten erkennbar

**Ziel:** Nach dem Speichern bleibt der Kalender an seiner Scrollstelle, und verborgene Spalten links und rechts sind mit einem Pfeil-Knopf erkennbar und erreichbar.
**Runde:** R04
**Quelle:** R04 – Ä152, Ä154
**Hängt ab von:** 82
**Modell:** opus
**IDs für neue Befunde:** Ä171–Ä173

## Umfang
- Nachstellen und beheben: nach dem Speichern einer Notiz in einer rechts außerhalb liegenden Spalte springt der Kalender an den Anfang; Scrollstelle waagrecht und senkrecht bleibt, der neue Eintrag bleibt im Bild; gilt für Termin, Pause, Sperrzeit, Notiz, Sauna und Verschieben (Ä152)
- Weitere Spalten (Team- und Raumansicht): weicher Verlauf am Rand des Rasters mit rundem Knopf „‹“ bzw. „›“ und Zahl der verborgenen Spalten („+3“), Klick schiebt um eine Bildschirmbreite; Tooltip und Screenreader nennen die verborgenen Namen; ohne verborgene Spalten auf einer Seite kein Verlauf (Ä154, Ruling Variante 2)

## Muster übernehmen
- src/AispaStudio.Desktop/Views/CalendarView.axaml.cs – Scrollen und `RevealPreviewColumn`
- src/AispaStudio.Desktop/ViewModels/CalendarViewModel.Rooms.cs – Spalte ins Bild holen

## Voraussichtlich geänderte Dateien
- src/AispaStudio.Desktop/Views/CalendarView.axaml
- src/AispaStudio.Desktop/Views/CalendarView.axaml.cs
- src/AispaStudio.Desktop/ViewModels/CalendarViewModel.cs

## NICHT bauen
- Zählerzeile „6 von 9 Räumen“ im Kopf
- Spaltenbreite mit angeschnittener Nachbarspalte

## Prompt
```
Lies CLAUDE.md, work/conventions.md, die Rundenakte work/rounds/R04-*.md und diese Slice-Datei.
Setze Slice 86 "Kalender: Scrollstelle erhalten, weitere Spalten erkennbar" um (diese Datei).
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
- [ ] Given Sophie Gruber rechts außerhalb und waagrecht zu ihr gescrollt When eine Notiz in ihrer Spalte gespeichert wird Then bleibt die waagrechte Scrollstelle und die Notiz ist sichtbar
- [ ] Given die Raumansicht mit drei Räumen rechts außerhalb When man das Raster ansieht Then steht rechts der Knopf „›“ mit „+3“ und links keiner
- [ ] Given der Knopf „›“ When er geklickt wird Then rückt das Raster um eine Bildschirmbreite weiter und links erscheint „‹“
- [ ] Manuell: Nach rechts zu Sophie scrollen, Notiz anlegen und speichern: Ansicht bleibt
- [ ] Manuell: Raumansicht: Pfeil-Knopf rechts mit Zahl, Klick schiebt weiter

## Nicht in dieser Slice
–
