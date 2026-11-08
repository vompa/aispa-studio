# Slice 146 – Kassa: der Reiter „Belege“ zeigt die Belege der Beispieldaten

**Ziel:** Mit frisch eingespielten Beispieldaten zeigt der Reiter „Belege“ der Kassa Belege von heute und den Vortagen.
**Runde:** R07
**Quelle:** R07 – Ä313
**Hängt ab von:** 137
**Modell:** sonnet
**Prüfstufe:** –
**IDs für neue Befunde:** Ä382–Ä384

## Umfang
- Zuerst nachstellen: Beispieldaten frisch einspielen, Kassa → „Belege“ öffnen, mit einem Test zeigen, was fehlt (heute, Vortage, gewählte Kasse)
- Ursache dort beheben, wo sie liegt: Beispieldaten (beide Wege von `IDemoOps`, geprüft mit `DemoBulkTests`), Filter der API oder Anzeige
- Testdaten-Doku docs/testdaten.md nachziehen

## Muster übernehmen
- src/AispaStudio.Api/Demo/
- src/AispaStudio.Desktop/Views/PosView.axaml (Reiter „Belege“)
- docs/testdaten.md

## Voraussichtlich geänderte Dateien
- src/AispaStudio.Api/Demo/
- src/AispaStudio.Api/Pos/
- src/AispaStudio.Desktop/Views/PosView.axaml
- src/AispaStudio.Desktop/ViewModels/PosViewModel.cs
- tests/AispaStudio.Api.Tests/
- tests/AispaStudio.Desktop.Tests/
- docs/testdaten.md

## NICHT bauen
- neue Belegarten
- Eingangsrechnungen oder Lieferscheine

## Prompt
```
Lies CLAUDE.md, work/conventions.md, die Rundenakte work/rounds/R07-*.md und diese Slice-Datei.
Setze Slice 146 "Kassa: der Reiter „Belege“ zeigt die Belege der Beispieldaten" um (diese Datei).
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
- [ ] Given frisch eingespielte Beispieldaten When man an Kasse WIEN-1 den Reiter „Belege“ für heute öffnet Then stehen dort Belege, sofern der Verlauf heute schon kassiert hat
- [ ] Given dieselben Daten When man einen Vortag wählt Then stehen die Belege dieses Tages dort
- [ ] Manuell: Beispieldaten neu laden (docker compose down -v), als Rezeption Kassa → „Belege“ für heute und gestern öffnen

## Nicht in dieser Slice
–
