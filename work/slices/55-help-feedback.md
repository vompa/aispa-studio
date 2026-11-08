# Slice 55 – Hilfe: Rückmeldung je Thema

**Ziel:** Unter jedem Hilfethema können Leserinnen und Leser sagen, ob es geholfen hat; die Antwort bleibt auf diesem PC.
**Runde:** R02
**Quelle:** R02 – Ruling zur Rückmeldung (Brief Ä76)
**Hängt ab von:** 54
**Modell:** sonnet

## Umfang
- Fuß jedes Themas in `help/HelpApp.tsx` und `HelpDrawer.tsx`: „War das hilfreich?“ mit Ja/Nein und optionalem Textfeld, Dank nach dem Senden
- Speicherung wie die KI-Rückmeldung (`IAiFeedbackStore`-Muster der Windows-App bzw. localStorage im Browser), nur Themenkennung, Antwort, Text, Zeit; keine Gästedaten
- Hilfetext zur Rückmeldung in `web/src/help/content/` (ADR 0055)

## Muster übernehmen
- src/AispaStudio.Desktop/Services/AiFeedbackLog.cs:23 – lokale KI-Rückmeldung (`FileAiFeedbackStore`)

## NICHT bauen
- Zentrale Sammlung oder Auswertung (braucht einen ADR)

## Prompt
```
Lies CLAUDE.md, work/conventions.md, die Rundenakte work/rounds/R02-*.md und diese Slice-Datei.
Setze Slice 55 "Hilfe: Rückmeldung je Thema" um (diese Datei).
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
- [ ] Given ein Hilfethema When „Nein“ und ein Text gesendet werden Then liegt genau ein Eintrag mit Themenkennung, Antwort, Text und Zeit im lokalen Speicher und die Seite zeigt den Dank
- [ ] Given eine gesendete Rückmeldung When das Thema erneut geöffnet wird Then steht die Frage nicht noch einmal
- [ ] Given eine Rückmeldung When der Eintrag geprüft wird Then enthält er keine Gästedaten und keinen Namen der angemeldeten Person
- [ ] Manuell: Hilfe › „Termin verschieben“ › „Nein“ › Text › Senden: Dank erscheint, beim Neuöffnen keine Frage mehr

## Nicht in dieser Slice
Auswertung über mehrere PCs
