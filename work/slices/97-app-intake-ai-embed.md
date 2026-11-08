# Slice 97 – Native Verwaltung: Anfragen-Annahme, KI und Online-Buchung einbetten

**Ziel:** Einstellungen der Anfragen-Annahme mit Absenderregeln, der KI und das Einbetten der Online-Buchung sind nativ bearbeitbar.
**Runde:** R05
**Quelle:** R05 – Ä191
**Hängt ab von:** 93
**Modell:** sonnet
**IDs für neue Befunde:** Ä211–Ä213

## Umfang
- Annahme (IntakeTab.tsx) und Absenderregeln (SenderRulesSection.tsx) nativ
- KI (AiTab.tsx) nativ; Schlüssel wie in Slice 96 maskiert
- Einbetten der Online-Buchung (EmbedSection.tsx) nativ mit Knopf „Code kopieren“
- Reiter Handbuch (HandbookTab.tsx) wird ein Sprung in die Hilfe; die Hilfe bleibt im eingebetteten Browser (E1)
- Hilfe nachziehen

## Muster übernehmen
- Verwaltungsgerüst und Firmendaten aus Slice 93

## Voraussichtlich geänderte Dateien
- src/AispaStudio.Desktop/Views/Admin/Intake*
- src/AispaStudio.Desktop/Views/Admin/Ai*
- src/AispaStudio.Desktop/Views/Admin/Embed*
- tests/AispaStudio.Desktop.Tests/
- web/src/help/content/

## NICHT bauen
- neue KI-Funktionen

## Prompt
```
Lies CLAUDE.md, work/conventions.md, die Rundenakte work/rounds/R05-*.md und diese Slice-Datei.
Setze Slice 97 "Native Verwaltung: Anfragen-Annahme, KI und Online-Buchung einbetten" um (diese Datei).
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
- [ ] Given eine Absenderregel „newsletter@“ When sie nativ angelegt wird Then landen Mails dieses Absenders nicht mehr im Posteingang
- [ ] Given die Seite Einbetten When die Inhaberin „Code kopieren“ wählt Then liegt der Einbettungscode in der Zwischenablage
- [ ] Manuell: KI-Schlüssel eintragen, Posteingang öffnen: Antwortentwurf kommt

## Nicht in dieser Slice
–
