# Slice 79 – Ein Begriff „Mitarbeitende“, Tooltips durchsehen

**Ziel:** Die Oberfläche nennt die Menschen im Kalender einheitlich „Mitarbeitende“, und alle Tooltips sind kurz und nach den Textregeln.
**Runde:** R03
**Quelle:** R03 – Ä101, Ä102
**Hängt ab von:** 75, 76, 77
**Modell:** sonnet
**IDs für neue Befunde:** Ä137–Ä139

## Umfang
- Begriff in `docs/gestaltung/texte.md` festlegen („Mitarbeitende“ als Sammelbegriff, „Team“ für die Ansicht, „Therapeutin/Therapeut“ nur, wo die Behandlung gemeint ist) und in Windows-App, Web und Hilfe ersetzen; Prüftest wie die übrigen Textregeln
- Alle Tooltips der Windows-App und des Web durchsehen: ein Satz, kein Wiederholen der Beschriftung, kein Aufzählen; Beispiel „Alle zeigen (3)“ wird „Auch Mitarbeitende ohne Dienst an diesem Standort, mit Grund“

## Muster übernehmen
- web/src/help/textRules.test.ts – Prüfung der Textregeln der Hilfe
- docs/gestaltung/texte.md – Textregeln

## NICHT bauen
- Neue Texte außerhalb dieser Begriffe

## Prompt
```
Lies CLAUDE.md, work/conventions.md, die Rundenakte work/rounds/R03-*.md und diese Slice-Datei.
Setze Slice 79 "Ein Begriff „Mitarbeitende“, Tooltips durchsehen" um (diese Datei).
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
- [ ] Given die Oberflächentexte und die Hilfe When ein Prüftest „MA“, „Personen ohne Dienst“ und „Therapeuten“ als Sammelbegriff sucht Then gibt es keinen Treffer
- [ ] Given ein Tooltip der Windows-App When seine Länge geprüft wird Then hat er höchstens einen Satz und wiederholt die Beschriftung nicht
- [ ] Manuell: Kalender: „Alle zeigen“ ansehen: Tooltip ist ein kurzer Satz mit „Mitarbeitende“

## Nicht in dieser Slice
–
