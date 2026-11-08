# Slice 38 – Ein gemeinsamer Rückfragedialog im Web

**Ziel:** Alle Rückfragen der Verwaltung nutzen einen Baustein mit Fokus auf Abbrechen, Fokus zurück zum Auslöser, Beschreibung und Alert-Rolle.
**Runde:** R02
**Quelle:** R02 – B2
**Hängt ab von:** –
**Modell:** sonnet

## Umfang
- Komponente `ConfirmDialog` (Titel, Text, Bestätigen, Abbrechen, optional Grund) in `web/src/components/`, die sieben vorhandenen Rückfragen darauf umstellen
- Fokus beim Schließen zurück zum Auslöser, `aria-describedby` am Text, `role="alertdialog"`, Innenabstand 24

## Muster übernehmen
- web/src/vitest.setup.ts – browsergetreues `showModal()` aus Slice 14

## NICHT bauen
–

## Prompt
```
Lies CLAUDE.md, work/conventions.md, die Rundenakte work/rounds/R02-*.md und diese Slice-Datei.
Setze Slice 38 "Ein gemeinsamer Rückfragedialog im Web" um (diese Datei).
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
- [ ] Given eine Rückfrage When sie sich öffnet Then liegt der Fokus auf „Abbrechen“ und der Text ist per `aria-describedby` verbunden
- [ ] Given eine Rückfrage When sie geschlossen wird (Abbrechen, Esc, Bestätigen) Then liegt der Fokus wieder auf dem Auslöser
- [ ] Given die Quelltexte When ein Prüftest `showModal` außerhalb von ConfirmDialog sucht Then gibt es keinen Treffer in Rückfragen
- [ ] Manuell: Schließzeit löschen, Rückfrage mit Esc schließen: der Fokus steht wieder auf „Löschen“

## Nicht in dieser Slice
–
