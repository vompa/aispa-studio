# Slice 137 – Kassa: „Heute abzurechnen“ als Hauptliste

**Ziel:** An der Kassa ist „Heute abzurechnen“ die breite Hauptliste links mit Suche, ungekürzten Zeilen und den Gruppen „Wartet auf Abrechnung“, „Später heute“ und „Erledigt“; Artikel und Gutscheine treten zurück.
**Runde:** R07
**Quelle:** R07 – Ä344, Ä345
**Hängt ab von:** –
**Modell:** opus
**Prüfstufe:** –
**IDs für neue Befunde:** Ä355–Ä357

## Umfang
- „Heute abzurechnen“ wird die Hauptspalte links (Mindestbreite etwa 380 px, volle Höhe); die Karte „Code scannen“ entfällt, ein Suchfeld „Gast, Code oder Artikel“ oben in der Liste nimmt auch den Handscanner an
- Jede Zeile zweizeilig und ungekürzt: oben Uhrzeit, Gast und Betrag, darunter Behandlung, Person und Raum (Umbruch statt „…“)
- Drei Gruppen: „Wartet auf Abrechnung“ (fertige Behandlungen, hervorgehoben, mit „fertig seit 10:55“), „Später heute“ (zurückgenommen), „Erledigt (n)“ zugeklappt; die Einteilung liefert die API, die App zeichnet nur
- Pfeiltasten wählen in der Liste, Enter öffnet die Rechnung; Fokus beim Öffnen der Kassa im Suchfeld
- „Artikel und Gutscheine“ (Slice 129) steht als einklappbares Panel rechts neben der Rechnung, standardmäßig zugeklappt; gemerkter Zustand bleibt; Wertgutschein, Behandlung und Paket stehen darin unten
- Ohne offene Rechnung zeigt die Mitte nur einen kleinen Hinweis
- Bei offenem Panel „KI und Hilfe“ (Ä345) behält die Liste ihre Mindestbreite; Platz geben zuerst Rechnung und Artikel-Panel ab
- Grundsatz Ä353 (eine Maske je Bereich, Tiefe nach Rolle) in docs/gestaltung/masken.md festhalten
- Handbuch der Kassa und betroffene Bilder nachziehen

## Muster übernehmen
- src/AispaStudio.Desktop/Views/PosView.axaml – heutige Spalten (Slice 129)
- src/AispaStudio.Desktop/ViewModels/PosViewModel.Day.cs – Tagesliste
- src/AispaStudio.Desktop/Controls/AccordionHeader.cs – Auf- und Zuklappen
- docs/design.md und docs/gestaltung/masken.md (Kurz-Checkliste)
- .claude/skills/fluent2-avalonia (foundations.md, data-display.md, accessibility.md)

## Voraussichtlich geänderte Dateien
- src/AispaStudio.Desktop/Views/PosView.axaml
- src/AispaStudio.Desktop/ViewModels/PosViewModel.cs
- src/AispaStudio.Desktop/ViewModels/PosViewModel.Day.cs
- src/AispaStudio.Api/Pos/
- tests/AispaStudio.Desktop.Tests/
- tests/AispaStudio.Api.Tests/
- docs/gestaltung/masken.md
- web/src/help/content/
- web/src/help/images/app/

## NICHT bauen
- Änderungen an Bezahlen, Zahlarten oder Beleg
- frei verschiebbare oder ziehbare Spalten

## Prompt
```
Lies CLAUDE.md, work/conventions.md, die Rundenakte work/rounds/R07-*.md und diese Slice-Datei.
Setze Slice 137 "Kassa: „Heute abzurechnen“ als Hauptliste" um (diese Datei).
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
- [ ] Given heute sind zwei Behandlungen fertig, eine kommt noch und eine ist bezahlt When man die Kassa öffnet Then stehen oben unter „Wartet auf Abrechnung“ die zwei fertigen, darunter „Später heute“ mit einer und „Erledigt (1)“ zugeklappt
- [ ] Given ein Termin „Aroma-Ganzkörpermassage“ für „Christian Steiner“ When die Kassa bei 1280 px offen ist Then stehen Name und Behandlung vollständig in der Zeile
- [ ] Given der Fokus liegt im Suchfeld When man Pfeil nach unten und Enter drückt Then ist die Rechnung des ersten wartenden Termins offen
- [ ] Given der Handscanner schickt einen Artikelcode mit Enter in das Suchfeld When die Kassa offen ist Then steht der Artikel mit Menge 1 auf der Rechnung
- [ ] Given das Panel „KI und Hilfe“ ist bei 1920 px offen When man die Kassa ansieht Then ist „Heute abzurechnen“ mindestens 380 px breit
- [ ] Manuell: Als Rezeption und als Inhaberin die Kassa bei 1280 px und voller Breite öffnen, mit und ohne „KI und Hilfe“; einen wartenden Gast per Tastatur abrechnen, einen Artikel scannen, „Erledigt“ aufklappen

## Nicht in dieser Slice
–
