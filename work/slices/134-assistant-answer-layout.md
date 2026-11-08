# Slice 134 – Assistent: Antworten als kurze Liste, Frage bleibt stehen

**Ziel:** Antworten des Assistenten sind ein kurzer Kopfsatz und eine einzige Liste mit anklickbaren Zeilen statt Fließtext plus Knopfliste; die Frage verschwindet nie ohne Antwort, und wenn etwas schiefgeht, sagt die Leiste es und bietet „Erneut fragen“ an.
**Runde:** R06
**Quelle:** R06 – Ä316
**Hängt ab von:** 133
**Modell:** opus
**Prüfstufe:** –
**IDs für neue Befunde:** Ä335–Ä337

## Umfang
- Antwortform für alle Listen-Antworten (Tagesüberblick, freie Zeiten, Gäste, Personen …): Kopfsatz („Dienstag, 3. November, Wien Innere Stadt: 15 Termine“), darunter eine Liste; keine zweite Liste mit denselben Einträgen
- Zeile zweizeilig wie „Heute abzurechnen“ (Uhrzeit und Gast, darunter Behandlung und Person); die ganze Zeile ist der Sprung in Kalender, Kundenkarte oder Dienstplan, kein Text-Knopf „Im Kalender zeigen“ je Zeile
- Bis 10 Einträge alle, darüber „Alle zeigen“ statt „und 7 weitere“; Umbruch nur an Wortgrenzen, lange Wörter mit Auslassungspunkten und Tooltip
- Bedienung: Die Frage bleibt im Feld bzw. als Kopf der Antwort, bis die Antwort angezeigt ist; Ladezustand während der Antwort; Fehler oder leere Antwort → Meldung nach docs/gestaltung/meldungen.md mit „Erneut fragen“; Pfeil nach oben holt die letzte Frage zurück
- Nicht verstanden: sagen, was erkannt wurde, und zwei bis drei Vorschläge zum Antippen anbieten
- KI-Kennzeichen und „Warum diese Antwort?“ bleiben
- Hilfe zum Assistenten mit neuen Bildern nachziehen

## Muster übernehmen
- src/AispaStudio.Desktop/Views/CompanionContent.axaml und ViewModels/CompanionViewModel.Block.cs
- src/AispaStudio.Api/Assistant/Answers/Blocks.cs und DayAnswers.cs
- die Liste „Heute abzurechnen“ in src/AispaStudio.Desktop/Views/PosView.axaml
- .claude/skills/fluent2-avalonia (data-display.md, responsible-ai.md)

## Voraussichtlich geänderte Dateien
- src/AispaStudio.Desktop/Views/CompanionContent.axaml
- src/AispaStudio.Desktop/ViewModels/CompanionViewModel.cs
- src/AispaStudio.Desktop/ViewModels/CompanionViewModel.Block.cs
- src/AispaStudio.Api/Assistant/Answers/
- src/AispaStudio.Contracts/
- web/openapi/aispa-api.json
- tests/AispaStudio.Api.Tests/
- tests/AispaStudio.Desktop.Tests/
- web/src/help/content/
- web/src/help/images/app/

## NICHT bauen
- Verlauf mehrerer Fragen und Antworten (Chat-Verlauf)
- neue Themen (Slices 135, 136)

## Prompt
```
Lies CLAUDE.md, work/conventions.md, die Rundenakte work/rounds/R06-*.md und diese Slice-Datei.
Setze Slice 134 "Assistent: Antworten als kurze Liste, Frage bleibt stehen" um (diese Datei).
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
- [ ] Given 15 Termine morgen When jemand „termine morgen“ fragt Then steht oben ein Satz mit der Zahl und darunter eine Liste mit 10 Zeilen und „Alle zeigen“, ohne zweite Liste
- [ ] Given eine Antwortliste mit Terminen When man auf eine Zeile klickt Then zeigt der Kalender diesen Termin
- [ ] Given die API antwortet mit einem Fehler When jemand fragt Then steht die Frage weiter im Feld und die Leiste zeigt eine Meldung mit „Erneut fragen“
- [ ] Given eine Frage wurde beantwortet und das Feld ist leer When man Pfeil nach oben drückt Then steht die letzte Frage im Feld
- [ ] Given eine Behandlung mit langem Namen When sie in einer Zeile steht Then bricht der Name nicht mitten im Wort um
- [ ] Manuell: Als Rezeption „termine morgen“, „wer arbeitet heute“ und eine unverständliche Frage stellen, mit Tastatur und Maus durch die Antworten gehen

## Nicht in dieser Slice
–
