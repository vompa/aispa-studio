# Slice 143 – Lager: Unterreiter und „Was ist zu tun“

**Ziel:** Das Lager teilt sich in Übersicht, Bestand, Bestellen, Chargen und Bewegungen; die Übersicht sagt, was zu tun ist, der Bestand hat vier Spalten.
**Runde:** R07
**Quelle:** R07 – Ä351
**Hängt ab von:** –
**Modell:** opus
**Prüfstufe:** –
**IDs für neue Befunde:** Ä373–Ä375

## Umfang
- Unterreiter: Übersicht · Bestand · Bestellen · Chargen · Bewegungen; keine lange Seite mehr
- Übersicht mit „Was ist zu tun“: nachbestellen (Zahl, Link auf den Bestellvorschlag), was nur noch für wenige Behandlungen reicht, ablaufende Chargen; Lagerwert klein darunter
- Ein Feld „Material suchen oder scannen“ oben statt der breiten Scan-Zeile mit Erklärtext (der Text geht in die Hilfe)
- Bestand mit vier Spalten: Material, Bestand mit Balken gegen den Mindestbestand, „Reicht für“, Status-Chip; die übrigen Werte im Seitenpanel einer Zeile
- „Bestellen“ fasst Bestellvorschlag und Bedarfsprüfung zusammen (Zeitraum, Liste, „Bestellung anlegen“); die gelbe Textwand entfällt
- Grundsatz Ä353: Alltag oben, Verwaltung dahinter (Unterreiter rechts, Seitenpanel, „Bearbeiten“, Menü „…“); was ein Recht verlangt, erscheint ohne das Recht nicht, die API prüft jede Aktion selbst; Rezeption sieht Übersicht, Bestand und „Bewegung buchen“, Bestellen, Chargen, Werte und Preise nur mit Recht
- Werte dunkel in der Hauptfarbe, Bezeichnungen klein in der Sekundärfarbe, keine Daten in Hellgrau; Abschnitte mit Abstand und feinen Linien auf einer Fläche statt vieler gerahmter Karten; Werte nach docs/design.md, neue Bausteine (Chip, Status) dort festhalten
- Handbuch Lager und Bilder nachziehen

## Muster übernehmen
- src/AispaStudio.Desktop/Views/StockView.axaml
- src/AispaStudio.Desktop/Views/StockTasksSection.axaml
- src/AispaStudio.Desktop/Views/StockOverviewSection.axaml
- src/AispaStudio.Desktop/Views/StockDemandSection.axaml
- src/AispaStudio.Desktop/ViewModels/StockViewModel.cs
- Reiter der Verwaltung aus Slice 125
- docs/design.md und docs/gestaltung/masken.md (Kurz-Checkliste)
- .claude/skills/fluent2-avalonia (foundations.md, data-display.md, accessibility.md)

## Voraussichtlich geänderte Dateien
- src/AispaStudio.Desktop/Views/StockView.axaml
- src/AispaStudio.Desktop/Views/StockOverviewSection.axaml
- src/AispaStudio.Desktop/Views/StockDemandSection.axaml
- src/AispaStudio.Desktop/Views/StockOrdersSection.axaml
- src/AispaStudio.Desktop/Views/StockTasksSection.axaml
- src/AispaStudio.Desktop/ViewModels/StockViewModel.cs
- src/AispaStudio.Desktop/ViewModels/StockOverviewViewModel.cs
- tests/AispaStudio.Desktop.Tests/
- docs/design.md
- web/src/help/content/
- web/src/help/images/app/

## NICHT bauen
- neue Lagerberechnungen
- Bestellungen an Lieferanten senden

## Prompt
```
Lies CLAUDE.md, work/conventions.md, die Rundenakte work/rounds/R07-*.md und diese Slice-Datei.
Setze Slice 143 "Lager: Unterreiter und „Was ist zu tun“" um (diese Datei).
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
- [ ] Given 4 Positionen im Bestellvorschlag und eine Charge läuft am 10.11. ab When man das Lager öffnet Then zeigt die Übersicht beide Punkte mit Link
- [ ] Given Einweg-Kosmetiktücher reichen nur noch für 2 Behandlungen When man „Bestand“ öffnet Then steht in der Zeile „Reicht für 2 Behandlungen“ und ein Chip „nachbestellen“
- [ ] Given man scannt einen Materialcode in das Suchfeld When das Material existiert Then ist seine Zeile gewählt
- [ ] Given eine Rezeptionistin ohne Lagerrecht When sie das Lager öffnet Then sieht sie die Reiter „Bestellen“ und „Chargen“ nicht
- [ ] Manuell: Als Inhaberin alle Reiter durchgehen, einen Bestellvorschlag anlegen, eine Bewegung buchen; als Rezeption dieselbe Seite ansehen

## Nicht in dieser Slice
–
