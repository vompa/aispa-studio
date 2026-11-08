# Slice 95 – Native Verwaltung: Standorte, Räume, Schließzeiten, Raumsperren und Kalender-Abos

**Ziel:** Standorte mit Öffnungszeiten, Räume mit Checkliste, Schließzeiten, Raumsperren und Kalender-Abos für Feiertage und Ferien sind nativ bearbeitbar; die Sprungziele aus Kalender und Assistent führen in die native Maske.
**Runde:** R05
**Quelle:** R05 – Ä191
**Hängt ab von:** 93
**Modell:** sonnet
**IDs für neue Befunde:** Ä205–Ä207

## Umfang
- Standorte & Räume (LocationsTab.tsx) mit Öffnungszeiten, Anfahrt, Kontakt und Raum-Checkliste (RoomChecklist.tsx) nativ
- Schließzeiten (web/src/closures/) nativ
- Raumsperren (Reiter `roomClosures`) nativ
- Kalender-Abos für Feiertage und Ferien (web/src/subscriptions/, ADR 0075) nativ
- Sprungziele Kalender (Raum bearbeiten, Raum sperren, Schließtag) und Assistent (`locations`, `closures`, `subscriptions`) auf die native Maske
- Hilfe nachziehen

## Muster übernehmen
- Verwaltungsgerüst und Firmendaten aus Slice 93
- web/src/closures/ – Regeln der Schließzeiten

## Voraussichtlich geänderte Dateien
- src/AispaStudio.Desktop/Views/Admin/Locations*
- src/AispaStudio.Desktop/ViewModels/Admin/Locations*
- tests/AispaStudio.Desktop.Tests/
- web/src/help/content/
- src/AispaStudio.Desktop/Views/Admin/Closures*
- src/AispaStudio.Desktop/Views/Admin/CalendarSubscriptions*

## NICHT bauen
- neue Regeln für Öffnungszeiten

## Prompt
```
Lies CLAUDE.md, work/conventions.md, die Rundenakte work/rounds/R05-*.md und diese Slice-Datei.
Setze Slice 95 "Native Verwaltung: Standorte, Räume, Schließzeiten, Raumsperren und Kalender-Abos" um (diese Datei).
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
- [ ] Given ein Standort When die Inhaberin den Samstag auf 9–14 Uhr setzt Then bietet „Freie Zeit suchen“ am Samstag nach 14 Uhr nichts mehr an
- [ ] Given eine Schließzeit 24.12.–26.12. When sie nativ angelegt wird Then zeigt der Kalender diese Tage als geschlossen
- [ ] Manuell: Raum sperren und im Kalender prüfen, dass er nicht mehr buchbar ist

## Nicht in dieser Slice
–
