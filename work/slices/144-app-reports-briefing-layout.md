# Slice 144 – Zahlen: eine Reiterebene, Tagesstart als Übersicht

**Ziel:** „Zahlen“ hat eine Reiterebene, und Tagesstart, Mein Tag und Hygiene zeigen oben den Status, dann Werte und Listen statt Fließtext.
**Runde:** R07
**Quelle:** R07 – Ä352
**Hängt ab von:** –
**Modell:** opus
**Prüfstufe:** –
**IDs für neue Befunde:** Ä376–Ä378

## Umfang
- Eine Reiterebene: Tagesstart · Mein Tag · Hygiene · Berichte · Betriebsinfo; Standort und Datum rechts im Kopf
- Tagesstart: oben Status („✓ Nichts Dringendes offen“ bzw. die offenen Punkte), eine Zeile großer Werte (Behandlungen, Auslastung, im Dienst, abwesend), Personal als kleine Zeitleiste von Öffnung bis Schluss mit Chip „Urlaub“, Kassa mit Anfangsbestand als Wert und dem Hinweis zur Signatur als Warnband
- Mein Tag und Hygiene nach demselben Muster (Status, Werte, Liste)
- Grundsatz Ä353: Alltag oben, Verwaltung dahinter (Unterreiter rechts, Seitenpanel, „Bearbeiten“, Menü „…“); was ein Recht verlangt, erscheint ohne das Recht nicht, die API prüft jede Aktion selbst; Berichte und Betriebsinfo nur mit Recht
- Werte dunkel in der Hauptfarbe, Bezeichnungen klein in der Sekundärfarbe, keine Daten in Hellgrau; Abschnitte mit Abstand und feinen Linien auf einer Fläche statt vieler gerahmter Karten; Werte nach docs/design.md, neue Bausteine (Chip, Status) dort festhalten
- Handbuch „Tagesstart-Kontrolle“ und Bilder nachziehen

## Muster übernehmen
- src/AispaStudio.Desktop/Views/ReportsView.axaml
- src/AispaStudio.Desktop/Views/BriefingReportView.axaml
- src/AispaStudio.Desktop/Views/DayPlanReportView.axaml
- src/AispaStudio.Desktop/Views/HygieneReportView.axaml
- src/AispaStudio.Desktop/ViewModels/BriefingReportViewModel.cs
- docs/design.md und docs/gestaltung/masken.md (Kurz-Checkliste)
- .claude/skills/fluent2-avalonia (foundations.md, data-display.md, accessibility.md)

## Voraussichtlich geänderte Dateien
- src/AispaStudio.Desktop/Views/ReportsView.axaml
- src/AispaStudio.Desktop/Views/BriefingReportView.axaml
- src/AispaStudio.Desktop/Views/DayPlanReportView.axaml
- src/AispaStudio.Desktop/Views/HygieneReportView.axaml
- src/AispaStudio.Desktop/ViewModels/ReportsViewModel.cs
- src/AispaStudio.Desktop/ViewModels/BriefingReportViewModel.cs
- tests/AispaStudio.Desktop.Tests/
- docs/design.md
- web/src/help/content/
- web/src/help/images/app/

## NICHT bauen
- neue Kennzahlen oder Berichte
- Änderungen an den Berichten unter „Berichte“

## Prompt
```
Lies CLAUDE.md, work/conventions.md, die Rundenakte work/rounds/R07-*.md und diese Slice-Datei.
Setze Slice 144 "Zahlen: eine Reiterebene, Tagesstart als Übersicht" um (diese Datei).
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
- [ ] Given heute 15 Behandlungen, 5 im Dienst und nichts Dringendes When man „Zahlen“ öffnet Then steht oben „Nichts Dringendes offen“ und darunter die Werte 15 und 5
- [ ] Given Lena Huber hat Urlaub When man den Tagesstart ansieht Then steht sie in der Zeitleiste mit Chip „Urlaub“
- [ ] Given „Zahlen“ ist offen When man die Reiter ansieht Then gibt es genau eine Reiterebene
- [ ] Given eine Rezeptionistin ohne Berichtsrecht When sie „Zahlen“ öffnet Then sieht sie „Berichte“ und „Betriebsinfo“ nicht
- [ ] Manuell: Als Inhaberin und als Rezeption Tagesstart, Mein Tag und Hygiene öffnen, Datum und Standort wechseln

## Nicht in dieser Slice
–
