# Slice 17 – Terminkarten mit Namen und Rolle für Screenreader

**Ziel:** Ein Screenreader erkennt jede Terminkarte, jeden Zettel und jeden Streifen im Kalender mit Namen, Rolle und Auswahl.
**Runde:** R01
**Quelle:** R01 – W50 (Teil 1)
**Hängt ab von:** –
**Modell:** –

## Umfang
- Eigener Automation-Peer für `Border.appt` (:2448-2455) mit Name „{Zeit} {Gast}, {Behandlung}, {Status}“, Rolle und Auswahlzustand; Zettel (:2550) und Streifen (:2411) ebenso benannt

## Muster übernehmen
- src/AispaStudio.Desktop/Controls/AccordionHeader.cs:24 – eigener Peer mit zusätzlichem Provider

## NICHT bauen
- Ansage beim Wechsel mit den Pfeiltasten (Slice 18)

## Prompt
```
Lies CLAUDE.md, work/conventions.md, die Rundenakte work/rounds/R01-*.md und diese Slice-Datei.
Setze Slice 17 "Terminkarten mit Namen und Rolle für Screenreader" um (diese Datei).
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
- [ ] Given ein gebuchter Termin um 09:30 mit Gast und Behandlung When der Peer der Karte abgefragt wird Then lautet der Name „09:30 <Gast>, <Behandlung>, <Status>“ und die Rolle ist nicht Group
- [ ] Given dieser Termin ist ausgewählt When der Peer abgefragt wird Then meldet er den Zustand „ausgewählt“
- [ ] Manuell: Narrator an, Kalender öffnen, mit dem Narrator-Scan durch die Termine: jeder Termin wird mit Zeit, Gast und Behandlung gelesen

## Nicht in dieser Slice
Änderungen am Aussehen der Karten
