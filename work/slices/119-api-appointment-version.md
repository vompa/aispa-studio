# Slice 119 – Termin-Konflikt: Version mitschicken statt still überschreiben

**Ziel:** Wer einen Termin von einem veralteten Stand aus ändert, bekommt eine Konfliktmeldung mit dem aktuellen Stand; die Änderung eines anderen wird nicht mehr still überschrieben. Die Prüfung liegt in der API, ältere Clients bleiben lauffähig.
**Runde:** R06
**Quelle:** R06 – Ä281
**Hängt ab von:** –
**Modell:** opus
**IDs für neue Befunde:** –

## Umfang
- `AppointmentDto` liefert die Version (`Appointment.Version`, PostgreSQL xmin); die ändernden Termin-Anfragen (`MoveAppointmentRequest` und die übrigen Änderungen im Termin-Panel) tragen die erwartete Version als optionales Feld
- API vergleicht in `BookingService` die erwartete Version mit dem Stand und antwortet bei Abweichung mit 409 und dem aktuellen Termin; ohne Version verhält sie sich wie bisher (abwärtskompatibel)
- Kalender und Termin-Panel schicken die Version mit, zeigen den Konflikt verständlich an (nach docs/gestaltung/meldungen.md) und laden den aktuellen Stand; kein stilles Überschreiben, kein automatisches Zweitspeichern
- Offline-Warteschlange (`PostOrQueueAsync`): Eine beim Abgleich veraltete Änderung erscheint wie andere Ablehnungen in der Liste „nicht übernommen“
- Live-Update während offener Bearbeitung: Die gemerkte Version bleibt die des Bearbeitungsbeginns
- Hilfe nachziehen

## Muster übernehmen
- src/AispaStudio.Api/Scheduling/BookingService.cs – `IsConcurrencyConflict`
- src/AispaStudio.Desktop/Services/ApiClient.cs – `Rejection` bei 409
- docs/entscheidungen/0005-offline-windows-app-am-empfang.md
- docs/gestaltung/meldungen.md

## Voraussichtlich geänderte Dateien
- src/AispaStudio.Contracts/SchedulingContracts.cs
- src/AispaStudio.Api/Scheduling/
- src/AispaStudio.Desktop/ViewModels/CalendarViewModel.cs
- src/AispaStudio.Desktop/ViewModels/AppointmentViewModel.cs
- web/openapi/aispa-api.json
- tests/
- web/src/help/content/

## NICHT bauen
- Versionsprüfung für andere Entitäten (später nach demselben Muster)
- Zusammenführen zweier Änderungen
- ETag-Header (die Version steht im Vertrag, das passt zu `ClientId` und Warteschlange)

## Prompt
```
Lies CLAUDE.md, work/conventions.md, die Rundenakte work/rounds/R06-*.md und diese Slice-Datei.
Setze Slice 119 "Termin-Konflikt: Version mitschicken statt still überschreiben" um (diese Datei).
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
- [ ] Given zwei Arbeitsplätze zeigen denselben Termin When A ihn verschiebt und B danach von seinem alten Stand aus verschiebt Then bekommt B die Konfliktmeldung, der Termin bleibt bei A's Zeit
- [ ] Given ein Client ohne Versionsfeld When er einen Termin verschiebt Then gilt das bisherige Verhalten
- [ ] Given B ändert offline von einem veralteten Stand aus When er wieder online ist Then steht die Änderung unter „nicht übernommen“ und der Termin bleibt unverändert
- [ ] Manuell: Zwei Arbeitsplätze öffnen, denselben Termin nacheinander verschieben: der zweite sieht den Konflikt und danach den aktuellen Stand

## Nicht in dieser Slice
–
