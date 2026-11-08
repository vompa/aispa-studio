# Slice 133 – Assistent: unscharfe Namen und Bezug auf den offenen Gast

**Ziel:** Der Assistent versteht Fragen, wie die Rezeption sie tippt: „wann hat herr steiner seinen termin“ beantwortet er für Christian Steiner, bei mehreren passenden Gästen bietet er sie zur Auswahl an, und „sie“ oder „der Gast“ meint den offenen Termin oder die offene Kundenkarte.
**Runde:** R06
**Quelle:** R06 – Ä315
**Hängt ab von:** –
**Modell:** opus
**Prüfstufe:** –
**IDs für neue Befunde:** Ä332–Ä334

## Umfang
- Gemeinsame Namenssuche (`NameMatch`) für Gäste und Personen: Kleinschreibung, Anrede (Herr, Frau, Hr., Fr.), Titel, Nachname allein, Vorname allein, Rufname, „die Moser“/„der Steiner“, ein Tippfehler ab fünf Buchstaben
- Die Anrede grenzt nach der Anrede in der Kundenkartei ein (Herr Steiner = Christian, nicht Eva)
- Ein Treffer → die Antwort; mehrere → ein Auswahlblock (Name, Ort, nächster Termin), ein Klick beantwortet die ursprüngliche Frage für diesen Gast; keiner → „Keinen Gast ‚Steiner‘ gefunden“ mit „Gast suchen“
- „seinen/ihren Termin“ = der nächste Termin, ohne kommenden der letzte
- Bezug: „sie“, „er“, „der Gast“ meinen den offenen Termin, sonst die offene Kundenkarte, sonst den zuletzt genannten Gast dieser Leiste; die App schickt den Bezug mit, die API löst ihn auf
- Gleiches Verhalten mit und ohne Claude-Schlüssel; die Logik liegt in der API (ADR 0072)
- Hilfe zum Assistenten nachziehen

## Muster übernehmen
- src/AispaStudio.Api/Assistant/Answers/NameMatch.cs und GuestAnswers.cs
- src/AispaStudio.Api/Assistant/AssistantIntent.cs – regelbasierte Erkennung
- docs/konzepte/assistent-rezeption-fragen.md – Grundsätze 2 und 3
- docs/entscheidungen ADR 0072

## Voraussichtlich geänderte Dateien
- src/AispaStudio.Api/Assistant/
- src/AispaStudio.Contracts/
- src/AispaStudio.Desktop/ViewModels/CompanionViewModel.cs
- web/openapi/aispa-api.json
- tests/AispaStudio.Api.Tests/
- tests/AispaStudio.Desktop.Tests/
- web/src/help/content/

## NICHT bauen
- Suche über Lautähnlichkeit (Kölner Phonetik) oder mehr als einen Tippfehler
- neue Themen (Slices 135, 136)

## Prompt
```
Lies CLAUDE.md, work/conventions.md, die Rundenakte work/rounds/R06-*.md und diese Slice-Datei.
Setze Slice 133 "Assistent: unscharfe Namen und Bezug auf den offenen Gast" um (diese Datei).
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
- [ ] Given Christian und Eva Steiner sind Gäste When jemand „wann hat herr steiner seinen termin“ fragt Then nennt die Antwort den nächsten Termin von Christian Steiner
- [ ] Given zwei Gäste heißen Steiner When jemand „wann kommt steiner“ fragt Then bietet die Antwort beide zur Auswahl an und ein Klick auf einen beantwortet die Frage
- [ ] Given ein Gast Christian Steiner When jemand „termin herr steinr“ fragt Then erkennt der Assistent Christian Steiner
- [ ] Given der Termin von Petra Schwarz ist offen When jemand „wann war sie zuletzt da“ fragt Then antwortet der Assistent für Petra Schwarz
- [ ] Given kein Claude-Schlüssel When dieselben Fragen gestellt werden Then sind die Antworten dieselben
- [ ] Manuell: Als Rezeption ein Dutzend Fragen aus docs/konzepte/assistent-rezeption-fragen.md zu Kunden klein und mit Tippfehlern stellen

## Nicht in dieser Slice
–
