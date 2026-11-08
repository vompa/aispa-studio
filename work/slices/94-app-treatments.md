# Slice 94 – Native Verwaltung: Behandlungen

**Ziel:** Behandlungen mit allen Abschnitten (Preise und Preisregeln, Ablauf und Vorbereitung, Material, Extras, Wiederbuchen, Online-Regeln) sind nativ bearbeitbar; der Kalender springt bei „Behandlung bearbeiten“ in die native Maske. Welche Personen eine Behandlung können, steht nur lesend mit Sprung in den Bereich Team.
**Runde:** R05
**Quelle:** R05 – Ä191
**Hängt ab von:** 93
**Modell:** opus
**IDs für neue Befunde:** Ä202–Ä204

## Umfang
- Behandlungen (web/src/admin/TreatmentsTab.tsx mit PriceRules, PreparationSection, MaterialSection, ExtrasSection, RebookSection, OnlineRulesSection) nativ
- Abschnitt „Wer kann das?“ nur lesend mit Sprung in die Personenkarte (Qualifikationen pflegt Slice 98 im Bereich Team)
- Sprungziele aus Kalender und Assistent (`seite=masterData&reiter=treatments&id=…&bereich=…`) öffnen die native Maske an der Stelle
- Hilfe und Handbuchbilder der Abläufe neu

## Muster übernehmen
- Verwaltungsgerüst und Firmendaten aus Slice 93
- web/src/admin/TreatmentsTab.tsx – Felder und Regeln

## Voraussichtlich geänderte Dateien
- src/AispaStudio.Desktop/Views/Admin/Treatments*
- src/AispaStudio.Desktop/ViewModels/Admin/Treatments*
- tests/AispaStudio.Desktop.Tests/
- web/src/help/content/

## NICHT bauen
- neue Preis- oder Ablauflogik (bleibt in der API)
- Kosten je Behandlung (Slice 102)
- neue Lagerfunktionen am Abschnitt Material (Slices des Lagers)

## Prompt
```
Lies CLAUDE.md, work/conventions.md, die Rundenakte work/rounds/R05-*.md und diese Slice-Datei.
Setze Slice 94 "Native Verwaltung: Behandlungen" um (diese Datei).
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
- [ ] Given eine Behandlung „Hot Stone 60“ When die Inhaberin den Preis von 89 auf 95 Euro ändert und speichert Then bucht der Kalender ab sofort mit 95 Euro und der Verlauf zeigt 89 → 95
- [ ] Given der Kalender zeigt einen Termin mit „Hot Stone 60“ When die Rezeption „Behandlung bearbeiten“ wählt Then öffnet die native Maske beim Abschnitt Ablauf
- [ ] Given eine Behandlung ohne Qualifikation When sie gespeichert wird Then meldet die Maske die fehlende Qualifikation am Feld
- [ ] Manuell: Neue Behandlung mit Vorbereitungszeit anlegen und im Kalender buchen: Vor- und Nachbereitung erscheinen

## Nicht in dieser Slice
–
