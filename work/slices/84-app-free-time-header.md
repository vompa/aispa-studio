# Slice 84 – Freie Zeit suchen als Hauptweg im Kalenderkopf, Sprache ausblenden

**Ziel:** Neben der Tagesauswahl steht der Knopf „Freie Zeit suchen“ als Hauptweg zum Anlegen; das Feld „Sprache des Gastes“ ist ausgeblendet.
**Runde:** R04
**Quelle:** R04 – Ä150, Ä157
**Hängt ab von:** –
**Modell:** sonnet
**IDs für neue Befunde:** Ä165–Ä167

## Umfang
- Knopf „Freie Zeit suchen“ im Kalenderkopf neben Heute/‹ ›/Datum, als primärer Knopf; „+ Termin“ wird ein normaler Knopf, öffnet weiter sofort „Neuer Termin“, das Menü daneben bleibt (Ä150, Ruling)
- Tastenleiste und Tooltip nennen Strg+N
- „Sprache des Gastes“ in „Freie Zeit suchen“ samt Fragezeichen ausblenden; Logik bleibt, Vermerk an Feld und ViewModel „ausgeblendet am 27.10.2026 (Auftraggeber), für eine mögliche spätere Nutzung“; Hilfe nennt das Feld nicht mehr (Ä157)
- Hilfe „Freie Zeit suchen“ und „Kalender“ samt Handbuchbildern nachziehen

## Muster übernehmen
- src/AispaStudio.Desktop/Views/CalendarView.axaml – Kalenderkopf mit „+ Termin“
- src/AispaStudio.Desktop/ViewModels/FreeTimeViewModel.cs – Sprachen-Auswahl

## Voraussichtlich geänderte Dateien
- src/AispaStudio.Desktop/Views/CalendarView.axaml
- src/AispaStudio.Desktop/ViewModels/FreeTimeViewModel.cs
- web/src/help/content/

## NICHT bauen
- Entfernen der Sprach-Logik
- Änderungen an der Sprache in Kundenkarte, Team und Kalenderkürzel

## Prompt
```
Lies CLAUDE.md, work/conventions.md, die Rundenakte work/rounds/R04-*.md und diese Slice-Datei.
Setze Slice 84 "Freie Zeit suchen als Hauptweg im Kalenderkopf, Sprache ausblenden" um (diese Datei).
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
- [ ] Given der Kalender When man den Kopf ansieht Then steht „Freie Zeit suchen“ als primärer Knopf neben der Tagesauswahl und öffnet die Suche
- [ ] Given „Freie Zeit suchen“ When das Panel offen ist Then ist kein Feld „Sprache des Gastes“ sichtbar
- [ ] Manuell: Kalenderkopf: „Freie Zeit suchen“ neben dem Datum, Klick öffnet die Suche
- [ ] Manuell: Freie Zeit suchen: kein Feld Sprache

## Nicht in dieser Slice
–
