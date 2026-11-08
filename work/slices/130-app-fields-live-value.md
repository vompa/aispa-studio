# Slice 130 – Betrags- und Zahlenfelder übernehmen den Wert schon beim Tippen

**Ziel:** Sobald in einem Betrags- oder Zahlenfeld ein gültiger Wert steht, übernimmt die Maske ihn; man muss das Feld nicht verlassen, damit ein Knopf frei wird oder eine Summe stimmt (z. B. „Wertgutschein verkaufen“ nach dem Eintippen des Betrags).
**Runde:** R06
**Quelle:** R06 – Ä311
**Hängt ab von:** –
**Modell:** opus
**Prüfstufe:** –
**IDs für neue Befunde:** Ä323–Ä325

## Umfang
- `ParsedField` mit `MoneyField` und `NumberField`: Ergibt der getippte Text einen gültigen Wert, meldet das Feld ihn sofort nach außen (`CommittedText`, `Value`), ohne den Text umzuformatieren; ein ungültiger oder leerer Zwischenstand meldet „kein Wert“ (Knopf gesperrt)
- Anzeigeformat (z. B. „5,00“) und Fehlermeldung am Feld weiter erst bei Enter oder beim Verlassen; Esc stellt den Wert vom Betreten des Feldes wieder her
- Datum und Uhrzeit (`DateField`, `TimeField`) bleiben beim Übernehmen erst nach dem Bestätigen, weil Zwischenstände wie „1“ oder „14“ mehrdeutig sind und abhängige Felder (Ende nach Beginn) nicht während des Tippens springen dürfen
- Alle Masken mit Betrags- und Zahlenfeldern prüfen (Kassa, Kosten, Behandlungen, Kassenlade, Lager …): kein ViewModel rechnet bei jedem Tastendruck etwas Teures oder speichert automatisch
- Die Abweichung von Fluent („commit on Enter or blur“) mit Grund in docs/gestaltung/fluent2-ableitung.md, Abschnitt 6, und im Design-Brief festhalten

## Muster übernehmen
- src/AispaStudio.Desktop/Controls/ParsedField.cs – Lesen, Bestätigen, Esc
- die Feldtests in tests/AispaStudio.Desktop.Tests/ (Parser und Headless-Eingabe)
- .claude/skills/fluent2-avalonia (inputs.md)

## Voraussichtlich geänderte Dateien
- src/AispaStudio.Desktop/Controls/ParsedField.cs
- src/AispaStudio.Desktop/Controls/MoneyField.cs
- src/AispaStudio.Desktop/Controls/NumberField.cs
- docs/gestaltung/fluent2-ableitung.md
- docs/design.md
- tests/AispaStudio.Desktop.Tests/

## NICHT bauen
- Umformatieren während des Tippens
- Änderung an Datums- und Uhrzeitfeldern
- automatisches Speichern von Masken

## Prompt
```
Lies CLAUDE.md, work/conventions.md, die Rundenakte work/rounds/R06-*.md und diese Slice-Datei.
Setze Slice 130 "Betrags- und Zahlenfelder übernehmen den Wert schon beim Tippen" um (diese Datei).
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
- [ ] Given die Kassa mit leerem Wertgutschein-Betrag When man „50“ tippt, ohne das Feld zu verlassen Then ist „Wertgutschein verkaufen“ bedienbar und verkauft 50,00 €
- [ ] Given ein Betragsfeld When man „12,345“ tippt Then meldet das Feld keinen Wert und beim Verlassen erscheint der Fehler „Höchstens zwei Nachkommastellen.“
- [ ] Given man tippt „5“ When man das Feld noch nicht verlassen hat Then steht weiter „5“ im Feld und erst nach Tab „5,00“
- [ ] Given im Feld stand 20,00 und man tippt „35“ When man Esc drückt Then steht wieder 20,00 und die Maske rechnet mit 20,00
- [ ] Manuell: Wertgutschein, Rabatt in Euro und Paketpreis an der Kassa eintippen und ohne Tab den Knopf drücken

## Nicht in dieser Slice
–
