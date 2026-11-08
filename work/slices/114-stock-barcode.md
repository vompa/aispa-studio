# Slice 114 – Lager: Barcode und Scannen bei Wareneingang, Inventur und Kassa

**Ziel:** Artikel tragen einen Barcode (EAN); ein Handscanner als Tastatureingabe findet den Artikel beim Wareneingang, in der Inventur und an der Kassa.
**Runde:** R05
**Quelle:** R05 – Ä251
**Hängt ab von:** 101
**Modell:** sonnet
**IDs für neue Befunde:** Ä265–Ä267

## Umfang
- Feld Barcode am Artikel (eindeutig je Mandant), Suche per Code in der API
- Wareneingang, Inventur und Kassa nehmen einen USB-Handscanner als Tastatureingabe (schnelle Eingabe mit Enter) und wählen den Artikel; keine eigene Hardware-Anbindung (E13); unbekannter Code fragt „Artikel anlegen oder zuordnen?“
- Hilfe nachziehen

## Muster übernehmen
- native Lager-Maske aus Slice 101
- src/AispaStudio.Desktop/Views/PosView.axaml – Eingabe an der Kassa
- docs/konzepte/lager-konkurrenz.md – Ist-Stand, Konkurrenz, Empfehlungen

## Voraussichtlich geänderte Dateien
- src/AispaStudio.Api/Stock/
- src/AispaStudio.Desktop/Views/Admin/Stock*
- src/AispaStudio.Desktop/Views/PosView.axaml
- tests/AispaStudio.Api.Tests/
- tests/AispaStudio.Desktop.Tests/
- web/src/help/content/

## NICHT bauen
- Scannen mit der Handy-Kamera (Slice 116)
- Etikettendruck

## Prompt
```
Lies CLAUDE.md, work/conventions.md, die Rundenakte work/rounds/R05-*.md und diese Slice-Datei.
Setze Slice 114 "Lager: Barcode und Scannen bei Wareneingang, Inventur und Kassa" um (diese Datei).
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
- [ ] Given ein Artikel mit EAN 9001234567890 When an der Kassa dieser Code gescannt wird Then steht der Artikel mit Menge 1 auf dem Beleg
- [ ] Given zwei Artikel mit demselben Code When der zweite gespeichert wird Then lehnt die API ab
- [ ] Manuell: Mit einem USB-Handscanner Wareneingang und Kassa ausprobieren

## Nicht in dieser Slice
–
