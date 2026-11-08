# Slice 141 – Kundenkarte: ruhiger Kopf, „Gut zu wissen“ und Verlauf

**Ziel:** Die Kundenkarte zeigt oben Gast, Kontakt und „Termin buchen“, darunter „Gut zu wissen“ als Chips und rechts den Verlauf; Leeres kostet keinen Platz.
**Runde:** R07
**Quelle:** R07 – Ä349
**Hängt ab von:** 140
**Modell:** opus
**Prüfstufe:** –
**IDs für neue Befunde:** Ä367–Ä369

## Umfang
- Kopf: Avatar, Name, Chip „★ Stammgast“; Telefon und E-Mail als schlichte Zeile (Klick kopiert); rechts „Termin buchen“ (Hauptknopf) und „Kassieren“, wenn etwas offen ist; „Bearbeiten“, „Weitere E-Mail-Adresse“ und Anrede im Menü „…“
- Kennzahlen als eine Zeile („4 Besuche in 12 Monaten · zuletzt am 15.10. · meist alle 16 Tage · nie gefehlt“) statt vier Kacheln; „Nächster Termin“ hervorgehoben mit „buchen“
- „Gut zu wissen“ als erster Block: Wünsche als Chips, „Nicht behandeln: …“ als Warn-Chip, Kennzeichen, Rezeptionsvermerk und „Am liebsten: …“ (Person, Zeiten), soweit gesetzt
- Nicht Erfasstes in einer Zeile „Noch nicht erfasst: + Kennzeichen · + Rezeptionsvermerk · + Termine am liebsten“; Erklärtexte nur in Tooltip und Hilfe
- Zwei Spalten: links „Gut zu wissen“, rechts Verlauf als schlanke Zeitleiste (nächste und letzte Termine mit Behandlung und Person, offene Gutscheine und Pakete mit Restwert, letzte Käufe; „Je Person“ geht darin auf)
- „Ändern“ als Stift beim Überfahren bzw. Fokus eines Abschnitts; ein Fragezeichen im Kopf statt je Abschnitt
- Werte dunkel in der Hauptfarbe, Bezeichnungen klein in der Sekundärfarbe, keine Daten in Hellgrau; Abschnitte mit Abstand und feinen Linien auf einer Fläche statt vieler gerahmter Karten; Werte nach docs/design.md, neue Bausteine (Chip, Status) dort festhalten
- Grundsatz Ä353: Alltag oben, Verwaltung dahinter (Unterreiter rechts, Seitenpanel, „Bearbeiten“, Menü „…“); was ein Recht verlangt, erscheint ohne das Recht nicht, die API prüft jede Aktion selbst; Verwaltungsaktionen, die es schon gibt, stehen im Menü „…“ nur mit Recht
- Handbuch der Kundenkarte und Bilder nachziehen

## Muster übernehmen
- src/AispaStudio.Desktop/Views/CustomersView.axaml
- src/AispaStudio.Desktop/Views/CustomerOverviewView.axaml
- src/AispaStudio.Desktop/Views/HistorySectionView.axaml
- src/AispaStudio.Desktop/ViewModels/CustomersViewModel.Card.cs
- src/AispaStudio.Desktop/ViewModels/CustomersViewModel.Flags.cs
- docs/design.md und docs/gestaltung/masken.md (Kurz-Checkliste)
- .claude/skills/fluent2-avalonia (foundations.md, data-display.md, accessibility.md)

## Voraussichtlich geänderte Dateien
- src/AispaStudio.Desktop/Views/CustomersView.axaml
- src/AispaStudio.Desktop/Views/CustomerOverviewView.axaml
- src/AispaStudio.Desktop/Views/HistorySectionView.axaml
- src/AispaStudio.Desktop/ViewModels/CustomersViewModel.cs
- src/AispaStudio.Desktop/ViewModels/CustomersViewModel.Card.cs
- src/AispaStudio.Desktop/ViewModels/CustomerOverviewViewModel.cs
- tests/AispaStudio.Desktop.Tests/
- docs/design.md
- web/src/help/content/
- web/src/help/images/app/

## NICHT bauen
- neue Kundendaten oder Kennzahlen
- neue Verwaltungsfunktionen (Zusammenführen, Löschen), wo es sie noch nicht gibt

## Prompt
```
Lies CLAUDE.md, work/conventions.md, die Rundenakte work/rounds/R07-*.md und diese Slice-Datei.
Setze Slice 141 "Kundenkarte: ruhiger Kopf, „Gut zu wissen“ und Verlauf" um (diese Datei).
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
- [ ] Given Helga Auer hat die Wünsche „Druck sanft“ und „nicht behandeln: Füße (kitzlig)“ When man ihre Karte öffnet Then steht „Nicht behandeln: Füße“ als Warn-Chip im ersten Block
- [ ] Given Kennzeichen, Rezeptionsvermerk und Lieblingstermine sind leer When man die Karte öffnet Then gibt es dafür keine eigenen Abschnitte, nur die Zeile „Noch nicht erfasst“
- [ ] Given der Gast hat keinen nächsten Termin When man die Karte öffnet Then steht oben „Nächster Termin: keiner“ mit „buchen“, und „Termin buchen“ ist der Hauptknopf
- [ ] Given eine Rezeptionistin ohne Verwaltungsrecht When sie das Menü „…“ öffnet Then sieht sie keine Verwaltungsaktionen
- [ ] Given jede Bezeichnung und jeder Wert der Karte When man ihre Farben misst Then haben Werte die Hauptfarbe und mindestens 4,5:1 Kontrast
- [ ] Manuell: Als Rezeption und als Inhaberin Helga Auer und einen Gast ohne Angaben öffnen; Wünsche ändern, „Termin buchen“, Menü „…“ mit beiden Rollen

## Nicht in dieser Slice
–
