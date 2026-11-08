# Slice 93 – Native Verwaltung: Gerüst und Firmendaten als Vorbild

**Ziel:** Die Windows-App bekommt eine native Seite „Verwaltung“ mit Bereichsnavigation, Listen-Detail-Muster, Speichern und Verwerfen, Rückfrage bei ungespeicherten Änderungen, Hilfe-Fragezeichen und Verlauf; Firmendaten mit Impressum sind die erste Maske darin.
**Runde:** R05
**Quelle:** R05 – Ä191
**Hängt ab von:** 92
**Modell:** opus
**IDs für neue Befunde:** Ä199–Ä201

## Umfang
- Seite „Verwaltung“ mit Navigation der Bereiche nach dem Inventar; Bereiche ohne native Maske öffnen bis Slice 103 weiter die eingebettete Verwaltung an derselben Stelle
- Wiederverwendbare Bausteine in Controls/: Bereichsseite, Liste mit Detail, Leiste Speichern/Verwerfen (Strg+S aus `SaveShortcut`), Rückfrage beim Verlassen mit ungespeicherten Änderungen, Feld mit Hilfe-Fragezeichen
- Firmendaten (heute web/src/admin/CompanyTab.tsx) nativ: alle Felder, Prüfungen und Hilfetexte wie im Web, Verlauf-Abschnitt aus Slice 91
- Sprungziel „Jetzt ergänzen“ des Assistenten (`masterData/company` mit `feld`) führt auf das native Feld
- Scan-Tests (Tab-Reihenfolge, Screenreader-Namen, Bindungsfehler) erfassen die neue Seite

## Muster übernehmen
- src/AispaStudio.Desktop/Views/CustomersView.axaml – Listen-Detail und Kundendaten-Maske
- src/AispaStudio.Desktop/Controls/SaveShortcut.cs
- docs/gestaltung/masken.md – Kurz-Checkliste
- Skill fluent2-avalonia

## Voraussichtlich geänderte Dateien
- src/AispaStudio.Desktop/Controls/
- src/AispaStudio.Desktop/Views/AdminView.axaml
- src/AispaStudio.Desktop/ViewModels/AdminViewModel.cs
- src/AispaStudio.Desktop/ViewModels/ShellViewModel.cs
- src/AispaStudio.Desktop/Themes/Styles.axaml
- tests/AispaStudio.Desktop.Tests/
- web/src/help/content/

## NICHT bauen
- weitere Verwaltungsbereiche (Slices 94–102)
- Entfernen der eingebetteten Verwaltung (Slice 103)
- neue Felder, die es im Web nicht gibt

## Prompt
```
Lies CLAUDE.md, work/conventions.md, die Rundenakte work/rounds/R05-*.md und diese Slice-Datei.
Setze Slice 93 "Native Verwaltung: Gerüst und Firmendaten als Vorbild" um (diese Datei).
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
- [ ] Given die Inhaberin öffnet Verwaltung → Firmendaten When sie den Firmennamen ändert und Strg+S drückt Then ist er gespeichert und der Verlauf zeigt die Änderung
- [ ] Given ungespeicherte Änderungen in Firmendaten When die Inhaberin zum Kalender wechselt Then fragt die App „Änderungen speichern?“ mit Speichern, Verwerfen und Abbrechen
- [ ] Given eine Rezeptionistin ohne Recht ManageOrganization When sie die Verwaltung öffnet Then fehlt der Bereich Firmendaten
- [ ] Manuell: Firmendaten nativ ändern, App neu starten: Werte stehen da, Impressum im Kundenbereich zeigt den neuen Stand

## Nicht in dieser Slice
–
