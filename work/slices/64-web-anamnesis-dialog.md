# Slice 64 – Anamnese-Fenster als natives Dialogfenster

**Ziel:** Das Anamnese-Fenster ist ein natives `<dialog>` mit Fokusfang, inertem Hintergrund und Rückkehr des Fokus; die Druckansicht des Papierbogens bleibt.
**Runde:** R03
**Quelle:** R03 – B73
**Hängt ab von:** –
**Modell:** sonnet
**IDs für neue Befunde:** B109–B111

## Umfang
- `anamnesis/Modal.tsx` als `<dialog>` mit `showModal()`, Esc über das Dialogfenster statt am `document`, Titel-ID per `useId` statt fest `modal-title`
- Druckansicht (`@media print`, bisher `.modal-backdrop`) auf das Dialogfenster umstellen und im Browser prüfen

## Muster übernehmen
- web/src/components/ConfirmDialog.tsx – natives Dialogfenster mit Fokus zurück zum Auslöser
- web/src/vitest.setup.ts – browsergetreues `showModal()` für Tests

## NICHT bauen
- Änderungen am Inhalt des Anamnesebogens

## Prompt
```
Lies CLAUDE.md, work/conventions.md, die Rundenakte work/rounds/R03-*.md und diese Slice-Datei.
Setze Slice 64 "Anamnese-Fenster als natives Dialogfenster" um (diese Datei).
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
- [ ] Given das Anamnese-Fenster offen When Esc gedrückt wird Then schließt es und der Fokus liegt wieder auf dem Auslöser
- [ ] Given zwei Anamnese-Fenster nacheinander When ihre Titel-IDs geprüft werden Then gibt es keine feste ID `modal-title`
- [ ] Manuell: Anamnesebogen öffnen und drucken (Druckvorschau): der Bogen sieht aus wie vorher, ohne grauen Hintergrund

## Nicht in dieser Slice
–
