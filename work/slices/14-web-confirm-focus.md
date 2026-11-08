# Slice 14 – Rückfragen im Web: Fokus auf Abbrechen, Dienstplan fragt vor dem Verwerfen

**Ziel:** Keine Rückfrage im Web legt den Fokus auf die zerstörende Aktion, und „Verwerfen“ im Dienstplan fragt vorher.
**Runde:** R01
**Quelle:** R01 – B1, B47
**Hängt ab von:** –
**Modell:** –

## Umfang
- Anfangsfokus nach `showModal()` auf „Abbrechen“ in ClosuresPage :544, RoomClosuresPage :432, PromotionsPage :618, SubscriptionsPage :618, BusinessInfoPage :685 (Fokus nach dem Öffnen setzen oder HTML-Attribut `autofocus`)
- RosterPage „Verwerfen“ :126: Rückfrage mit der Zahl der Entwürfe in einem `<dialog>`
- Test, der den Fokus nach dem Öffnen prüft (jsdom sieht ihn nicht; der Plan nennt den Weg)

## Muster übernehmen
- web/src/closures/ClosuresPage.tsx:533 – heutiger Dialog-Effekt mit `showModal()`

## NICHT bauen
- B2 einen gemeinsamen Rückfrage-Baustein für alle sieben Dialoge (offene Entscheidung)
- B35, B40, B52, B57 weitere fehlende Rückfragen (offene Entscheidung)

## Prompt
```
Lies CLAUDE.md, work/conventions.md, die Rundenakte work/rounds/R01-*.md und diese Slice-Datei.
Setze Slice 14 "Rückfragen im Web: Fokus auf Abbrechen, Dienstplan fragt vor dem Verwerfen" um (diese Datei).
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
- [ ] Given jede der fünf Rückfragen When sie sich öffnet Then ist `document.activeElement` der Knopf „Abbrechen“
- [ ] Given drei Entwürfe im Dienstplan When „Verwerfen“ gedrückt wird Then erscheint eine Rückfrage, die „3 Entwürfe“ nennt, und ohne Bestätigung bleiben alle drei erhalten
- [ ] Manuell: Schließzeiten › eine Schließzeit archivieren: der Fokus liegt auf „Abbrechen“; Enter schließt ohne zu archivieren
- [ ] Manuell: Dienstplan › Entwürfe › Verwerfen: Rückfrage mit Zahl der Entwürfe

## Nicht in dieser Slice
Neue Texte der Rückfragen
