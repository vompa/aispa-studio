# Slice 85 – Buchen außerhalb der Öffnungszeit mit „Trotzdem buchen“

**Ziel:** Die Rezeption kann nach dem Hinweis „Der Standort hat zu dieser Zeit geschlossen.“ ausdrücklich trotzdem buchen; Online-Buchung, freie Zeit und KI bieten solche Zeiten weiter nicht an.
**Runde:** R04
**Quelle:** R04 – Ä151
**Hängt ab von:** –
**Modell:** opus
**IDs für neue Befunde:** Ä168–Ä170

## Umfang
- API: Buchung außerhalb der Öffnungszeit und außerhalb des Dienstes mit ausdrücklicher Bestätigung annehmen (z. B. Feld `OutsideHoursConfirmed` an `AppointmentRequest`), nur mit `RequirePermission` zum Buchen; ohne Bestätigung bleibt die Ablehnung mit der Meldung
- Windows-App: der Hinweis der Vorabprüfung bleibt als Warnung, mit Aktion „Trotzdem buchen“; dasselbe für Pause, Sperrzeit, Paar/Spa und Sauna prüfen
- Online-Buchung, „Freie Zeit suchen“, Warteliste und KI-Terminannahme bieten Zeiten außerhalb der Öffnungszeit weiter nicht an
- Nachtrag zu ADR 0073; Hilfe „Termin buchen“

## Muster übernehmen
- src/AispaStudio.Api/Scheduling/ResourcePlanner.cs – OpeningRules und Schließtage (ADR 0073, Nachtrag R03)
- src/AispaStudio.Desktop/ViewModels/BookingViewModel.cs – `ClosedHint` (Slice 78)

## Voraussichtlich geänderte Dateien
- src/AispaStudio.Api/
- src/AispaStudio.Contracts/
- src/AispaStudio.Desktop/ViewModels/BookingViewModel.cs
- docs/entscheidungen/

## NICHT bauen
- Buchen außerhalb der Öffnungszeit für Gäste oder die KI

## Prompt
```
Lies CLAUDE.md, work/conventions.md, die Rundenakte work/rounds/R04-*.md und diese Slice-Datei.
Setze Slice 85 "Buchen außerhalb der Öffnungszeit mit „Trotzdem buchen“" um (diese Datei).
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
- [ ] Given Sophie Gruber (Dienst bis 17:00) und 18:45 When ohne Bestätigung gebucht wird Then lehnt der Server mit „Der Standort hat zu dieser Zeit geschlossen.“ ab
- [ ] Given derselbe Termin When „Trotzdem buchen“ gewählt wird Then ist der Termin gebucht
- [ ] Given die Online-Buchung When Zeiten gesucht werden Then ist keine Zeit außerhalb der Öffnungszeit dabei
- [ ] Manuell: Neuer Termin um 18:45 bei Sophie Gruber: Hinweis erscheint, „Trotzdem buchen“ bucht

## Nicht in dieser Slice
–
