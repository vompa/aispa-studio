# Slice 21 – Ein Primärknopf je Fläche im Termin-Panel, Abbrechen als Ghost

**Ziel:** Im Termin-Panel ist nur die Aktion der gerade bearbeiteten Karte hervorgehoben, und „Abbrechen“ tritt zurück.
**Runde:** R01
**Quelle:** R01 – W53, W-Muster 4 (Teil)
**Hängt ab von:** 07
**Modell:** –

## Umfang
- Primärknöpfe im Termin-Panel (:61, :125, :193, :228, :304/:311, :348) nur in der Karte, die gerade bearbeitet wird
- „Abbrechen“ als Ghost an :70, :124, :297, :350, :418, :598, :1682

## Muster übernehmen
- src/AispaStudio.Desktop/Views/CustomerContactForm.axaml:155 – ein Primärknopf je Maske

## NICHT bauen
- W56 „Ganze Buchung stornieren“ als `danger` (offene Entscheidung)

## Prompt
```
Lies CLAUDE.md, work/conventions.md, die Rundenakte work/rounds/R01-*.md und diese Slice-Datei.
Setze Slice 21 "Ein Primärknopf je Fläche im Termin-Panel, Abbrechen als Ghost" um (diese Datei).
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
- [ ] Given das Termin-Panel eines eingecheckten Termins mit offener Notiz When ein Headless-Test die sichtbaren `primary`-Knöpfe zählt Then ist es genau einer
- [ ] Given die sieben genannten „Abbrechen“-Knöpfe When sie gerendert werden Then tragen sie die Klasse `ghost`
- [ ] Manuell: Kalender › Termin öffnen › Notiz bearbeiten: nur „Notiz speichern“ ist hervorgehoben

## Nicht in dieser Slice
Neuer Aufbau des Panels
