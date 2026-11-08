# Slice 75 – Ablauf im Termin-Panel und klare Warteliste

**Ziel:** Die Aktionen im Termin-Panel stehen in einer klaren Reihenfolge, „Stornieren“ ist ohne Einchecken erreichbar, und in der Warteliste ist „Erledigt“ verständlich.
**Runde:** R03
**Quelle:** R03 – Ä81, Ä77
**Hängt ab von:** 74
**Modell:** sonnet
**IDs für neue Befunde:** Ä125–Ä127

## Umfang
- Ablauf der Aktionen je Status festlegen und zeigen: Gebucht (Einchecken, Verschieben, Stornieren), Eingecheckt (Kassieren, Einchecken zurücknehmen), Kassiert; Stornieren ohne Einchecken erreichbar; Vorschlag zuerst im Plan, dann umsetzen
- Warteliste: „Erledigt …“ heißt „Anfrage abschließen …“ mit Grund (gebucht, abgesagt, nicht erreichbar); ein gebuchter Eintrag verschwindet aus der Liste oder steht klar als „Gebucht“

## Muster übernehmen
- src/AispaStudio.Desktop/ViewModels/AppointmentViewModel.Panel.cs – Aktionen im Panel
- src/AispaStudio.Desktop/ViewModels/CalendarViewModel.Waitlist.cs – Warteliste
- tests/AispaStudio.Desktop.Tests/WaitlistTests.cs – Tests der Warteliste

## NICHT bauen
- Neue Status

## Prompt
```
Lies CLAUDE.md, work/conventions.md, die Rundenakte work/rounds/R03-*.md und diese Slice-Datei.
Setze Slice 75 "Ablauf im Termin-Panel und klare Warteliste" um (diese Datei).
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
- [ ] Given ein gebuchter Termin When sein Panel geöffnet wird Then steht „Stornieren“ ohne vorheriges Einchecken zur Verfügung
- [ ] Given ein Eintrag der Warteliste When „Anfrage abschließen …“ gewählt wird Then nennt die Auswahl die Gründe „Gebucht“, „Abgesagt“ und „Nicht erreichbar“
- [ ] Given ein gebuchter Wartelisteneintrag When die Liste neu geladen wird Then steht er nicht mehr als „Wartet“ da
- [ ] Manuell: Warteliste öffnen, „Anfrage abschließen …“ drücken: die Gründe sind klar, der Eintrag verschwindet oder steht als „Gebucht“

## Nicht in dieser Slice
–
