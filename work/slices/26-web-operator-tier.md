# Slice 26 – Abo-Stufe im Betreiberbereich nur mit Knopf und Rückfrage

**Ziel:** Die Abo-Stufe eines Spas ändert sich erst nach Knopfdruck und Bestätigung, nicht schon beim Blättern in der Auswahl.
**Runde:** R01
**Quelle:** R01 – B58
**Hängt ab von:** 14
**Modell:** –

## Umfang
- operator/OperatorApp.tsx :233: Auswahl plus Knopf „Stufe ändern“, Rückfrage in einem `<dialog>`, Erfolg mit `role="status"`

## Muster übernehmen
- die Rückfrage mit Fokus auf „Abbrechen“ aus Slice 14

## NICHT bauen
- B59 Fokus nach dem Ersetzen von Knöpfen (offene Entscheidung)

## Prompt
```
Lies CLAUDE.md, work/conventions.md, die Rundenakte work/rounds/R01-*.md und diese Slice-Datei.
Setze Slice 26 "Abo-Stufe im Betreiberbereich nur mit Knopf und Rückfrage" um (diese Datei).
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
- [ ] Given die Betreiber-Seite When im geschlossenen Auswahlfeld Pfeil runter gedrückt wird Then geht kein Aufruf an die API
- [ ] Given eine neue Stufe ist gewählt When „Stufe ändern“ gedrückt und die Rückfrage bestätigt wird Then geht genau ein Aufruf an die API und „Stufe geändert“ erscheint mit `role="status"`
- [ ] Manuell: Betreiberbereich › ein Spa › Stufe wählen: erst „Stufe ändern“ und Bestätigen ändert sie

## Nicht in dieser Slice
Neue Abo-Stufen
