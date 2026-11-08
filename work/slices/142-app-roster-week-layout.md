# Slice 142 – Dienstplan: lesbare Woche mit Schicht-Chips

**Ziel:** Der Dienstplan zeigt die Woche lesbar: Schichten als Chips, Urlaub als Balken, Stunden beim Namen, heute hervorgehoben, Entwurf als Band.
**Runde:** R07
**Quelle:** R07 – Ä350
**Hängt ab von:** –
**Modell:** opus
**Prüfstufe:** –
**IDs für neue Befunde:** Ä370–Ä372

## Umfang
- Schichten als gefüllte Chips in der Farbe der Person mit dunkler Zeit („9–17“); freie Tage ohne leeren Kasten (schwaches „frei“)
- Urlaub und Abwesenheit als durchgehender Balken über die Tage („Urlaub Di–Fr“)
- Heutige Spalte hervorgehoben; Stunden neben dem Namen („Lena Huber · 8 / 40 h“, geplant gegen Wochenarbeitszeit)
- Zeile „Besetzung“ je Tag (Zahl im Dienst); eine Warnung nur, wenn eine Mindestbesetzung schon hinterlegt ist
- Entwurf: geänderte Zellen gestrichelt, oben Band „Entwurf: n Änderungen nicht veröffentlicht – Veröffentlichen“; die Legendenzeile entfällt
- Klick auf eine Zelle öffnet das bestehende Bearbeiten (Zeit, frei, Urlaub) als Popover an der Zelle
- Grundsatz Ä353: Alltag oben, Verwaltung dahinter (Unterreiter rechts, Seitenpanel, „Bearbeiten“, Menü „…“); was ein Recht verlangt, erscheint ohne das Recht nicht, die API prüft jede Aktion selbst; Rezeption sieht die Woche nur, Bearbeiten und Veröffentlichen nur mit Recht
- Werte dunkel in der Hauptfarbe, Bezeichnungen klein in der Sekundärfarbe, keine Daten in Hellgrau; Abschnitte mit Abstand und feinen Linien auf einer Fläche statt vieler gerahmter Karten; Werte nach docs/design.md, neue Bausteine (Chip, Status) dort festhalten
- Handbuch Dienstplan und Bilder nachziehen

## Muster übernehmen
- src/AispaStudio.Desktop/Views/RosterPlanView.axaml
- src/AispaStudio.Desktop/Views/RosterDayEditorView.axaml
- src/AispaStudio.Desktop/ViewModels/RosterPlanViewModel.cs
- docs/design.md und docs/gestaltung/masken.md (Kurz-Checkliste)
- .claude/skills/fluent2-avalonia (foundations.md, data-display.md, accessibility.md)

## Voraussichtlich geänderte Dateien
- src/AispaStudio.Desktop/Views/RosterPlanView.axaml
- src/AispaStudio.Desktop/Views/RosterDayEditorView.axaml
- src/AispaStudio.Desktop/ViewModels/RosterPlanViewModel.cs
- src/AispaStudio.Desktop/ViewModels/RosterDayEditorViewModel.cs
- tests/AispaStudio.Desktop.Tests/
- docs/design.md
- web/src/help/content/
- web/src/help/images/app/

## NICHT bauen
- neue Regeln zur Mindestbesetzung
- Änderungen an der Planungslogik

## Prompt
```
Lies CLAUDE.md, work/conventions.md, die Rundenakte work/rounds/R07-*.md und diese Slice-Datei.
Setze Slice 142 "Dienstplan: lesbare Woche mit Schicht-Chips" um (diese Datei).
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
- [ ] Given Lena Huber hat Montag Dienst und Dienstag bis Freitag Urlaub When man die Woche öffnet Then steht ein Balken „Urlaub Di–Fr“ und neben dem Namen „8 / 40 h“
- [ ] Given eine Schicht 09:00–17:00 When man ihre Farben misst Then hat die Zeit mindestens 4,5:1 Kontrast
- [ ] Given eine Zelle ist geändert und nicht veröffentlicht When man die Woche ansieht Then trägt sie einen gestrichelten Rand und oben steht das Entwurfsband mit „Veröffentlichen“
- [ ] Given eine Rezeptionistin ohne Planungsrecht When sie auf eine Zelle klickt Then öffnet sich kein Bearbeiten
- [ ] Manuell: Als Inhaberin eine Woche planen, ändern und veröffentlichen; als Rezeption dieselbe Woche ansehen

## Nicht in dieser Slice
–
