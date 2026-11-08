# Slice 28 – Kleinigkeiten Windows-App: Dialogfokus, Datumsauswahl, Richtungswörter, KI-Knöpfe

**Ziel:** Kleine Verstöße der Windows-App in einem Durchgang beheben.
**Runde:** R01
**Quelle:** R01 – W24, W60, W-Muster 10, W-Muster 13 (Rückfragedialog)
**Hängt ab von:** 06, 08, 09, 10
**Modell:** –

## Umfang
- ConfirmDialogView: bei einem Eingabefeld (z. B. Grund beim Storno) Anfangsfokus auf das Feld (W24)
- Datumsauswahl: Tage des Nachbarmonats in `Fg2Brush` statt `Fg3Brush` (Styles.axaml, W60)
- Richtungswörter ersetzen: CustomersView :140, InboxView :68 und :212, InboxViewModel :587, LoginView :64 und :196, PosView :188; `AiLabels.cs` bleibt (dokumentiert, Brief 5.28)
- ConfirmDialogView :92-94 „Freundlicher formulieren (KI)“, „Förmlicher formulieren (KI)“

## Muster übernehmen
- src/AispaStudio.Desktop/Views/ConfirmDialogView.axaml.cs:70 – heutige Fokuslogik des Dialogs

## NICHT bauen
- Andere Stellen außer den genannten

## Prompt
```
Lies CLAUDE.md, work/conventions.md, die Rundenakte work/rounds/R01-*.md und diese Slice-Datei.
Setze Slice 28 "Kleinigkeiten Windows-App: Dialogfokus, Datumsauswahl, Richtungswörter, KI-Knöpfe" um (diese Datei).
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
- [ ] Given eine Storno-Rückfrage mit Feld „Grund“ When sie sich öffnet Then liegt der Fokus im Feld „Grund“
- [ ] Given die Datumsauswahl When ein Tag des Nachbarmonats gerendert wird Then nutzt er `Fg2Brush`
- [ ] Given die sieben genannten Texte When sie gelesen werden Then enthält keiner „links“, „rechts“, „oben“ oder „unten“
- [ ] Manuell: Kassa › Beleg stornieren: der Cursor steht im Feld „Grund“

## Nicht in dieser Slice
Neue Texte außer den genannten
