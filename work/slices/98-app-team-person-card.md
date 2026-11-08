# Slice 98 – Native Verwaltung: Bereich Team mit Personenkarte

**Ziel:** Alles zu einer Person steht an einer Stelle: der Bereich „Team“ zeigt die Liste aller Mitarbeitenden und je Person eine Karte mit Reitern Stammdaten, Zugang und Rolle, Qualifikationen und Verlauf; Dienstplan, Zeiten und Lohn hängen die Slices 99 und 100 als weitere Reiter derselben Karte an.
**Runde:** R05
**Quelle:** R05 – Ä191, Ä250
**Hängt ab von:** 93
**Modell:** opus
**IDs für neue Befunde:** Ä214–Ä216

## Umfang
- Bereich „Team“ in der Verwaltung: Liste mit Suche, Rolle und Status (aktiv, eingeladen, ausgeschieden), daneben die Personenkarte
- Reiter Stammdaten (heute StaffPage.tsx, TeamTab.tsx) nativ
- Reiter Zugang und Rolle: Einladung über die Wolverine-Queue wie heute, Rolle und Rechte, Zugang sperren
- Reiter Qualifikationen (heute QualificationsTab.tsx in den Stammdaten): welche Behandlungen die Person kann
- Reiter Verlauf aus Slice 91
- Die Karte ist der einzige Ort zum Ändern von Personendaten; andere Masken (Behandlungen, Kalender) zeigen nur und springen hierher
- Masken-Inventar: alle heutigen Personal-Teile (Personal, Team, Qualifikationen, Zugänge, Dienstplan, Zeiterfassung, Lohn) zeigen auf den Bereich Team
- Hilfe nachziehen

## Muster übernehmen
- Verwaltungsgerüst und Firmendaten aus Slice 93
- web/src/admin/StaffPage.tsx
- src/AispaStudio.Desktop/Views/CustomersView.axaml – Liste mit Karte und Reitern

## Voraussichtlich geänderte Dateien
- src/AispaStudio.Desktop/Views/Admin/Team*
- src/AispaStudio.Desktop/ViewModels/Admin/Team*
- tests/AispaStudio.Desktop.Tests/
- web/src/help/content/

## NICHT bauen
- neue Rollen oder Rechte
- Dienstplan, Zeiten und Lohn (Slices 99, 100)

## Prompt
```
Lies CLAUDE.md, work/conventions.md, die Rundenakte work/rounds/R05-*.md und diese Slice-Datei.
Setze Slice 98 "Native Verwaltung: Bereich Team mit Personenkarte" um (diese Datei).
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
- [ ] Given die Inhaberin öffnet Team → Lena When sie die Reiter durchgeht Then findet sie Stammdaten, Zugang und Rolle, Qualifikationen und Verlauf auf derselben Karte
- [ ] Given die Behandlung „Hot Stone 60“ zeigt „Wer kann das?“ When die Inhaberin auf Lena klickt Then öffnet Team → Lena beim Reiter Qualifikationen
- [ ] Given eine neue Mitarbeiterin When die Inhaberin sie mit Mailadresse einlädt Then geht eine Einladung über die Queue und die Mitarbeiterin steht als „eingeladen“ in der Liste
- [ ] Given eine Managerin When sie einer Rezeptionistin die Rolle Inhaberin geben will Then lehnt die API ab und die Maske zeigt den Grund
- [ ] Manuell: Mitarbeiterin einladen, Einladung in Mailpit öffnen, anmelden: Rolle stimmt

## Nicht in dieser Slice
–
