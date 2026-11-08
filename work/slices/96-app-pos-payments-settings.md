# Slice 96 – Native Verwaltung: Kassa, Zahlungsanbieter und Erinnerungen

**Ziel:** Kassa-Einstellungen (RKSV/TSE), Zahlungsanbieter und Erinnerungen an Gäste sind nativ bearbeitbar, ohne an der Kassa-Logik etwas zu ändern.
**Runde:** R05
**Quelle:** R05 – Ä191
**Hängt ab von:** 93
**Modell:** opus
**IDs für neue Befunde:** Ä208–Ä210

## Umfang
- Kassa-Einstellungen (PosTab.tsx) nativ, Signatureinrichtung und Pflichtangaben unverändert über die API
- Zahlungsanbieter (PaymentsTab.tsx) nativ; Schlüssel nur maskiert angezeigt, nie im Klartext zurückgelesen
- Erinnerungen (RemindersTab.tsx) nativ
- Hilfe nachziehen

## Muster übernehmen
- Verwaltungsgerüst und Firmendaten aus Slice 93
- docs/entscheidungen/0010-kassa-compliance-by-design.md

## Voraussichtlich geänderte Dateien
- src/AispaStudio.Desktop/Views/Admin/Pos*
- src/AispaStudio.Desktop/Views/Admin/Payments*
- src/AispaStudio.Desktop/Views/Admin/Reminders*
- tests/AispaStudio.Desktop.Tests/
- web/src/help/content/

## NICHT bauen
- Änderungen an RKSV/TSE-Abläufen
- Funktionen, die Umsätze an der Kasse vorbeiführen

## Prompt
```
Lies CLAUDE.md, work/conventions.md, die Rundenakte work/rounds/R05-*.md und diese Slice-Datei.
Setze Slice 96 "Native Verwaltung: Kassa, Zahlungsanbieter und Erinnerungen" um (diese Datei).
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
- [ ] Given ein hinterlegter Schlüssel eines Zahlungsanbieters When die Maske geöffnet wird Then steht nur „••••“ mit den letzten vier Zeichen
- [ ] Given die Erinnerung „24 Stunden vorher“ When die Inhaberin sie auf 48 Stunden ändert Then plant die API neue Erinnerungen mit 48 Stunden
- [ ] Manuell: Kassa-Einstellungen öffnen, nichts ändern, speichern: kein Eintrag im Verlauf, Kassa bucht wie vorher

## Nicht in dieser Slice
–
