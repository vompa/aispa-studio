# Slice 108 – Web: Handy-App für Kunden zur Terminkontrolle

**Ziel:** Der Kundenbereich wird zur installierbaren Handy-App mit „Meine Termine“ im Mittelpunkt: ansehen, verschieben und absagen nach den Fristen der API, in den eigenen Kalender übernehmen, Gutscheine ansehen, Anamnese vorab ausfüllen; Buchen bleibt in derselben App.
**Runde:** R05
**Quelle:** R05 – Ä194
**Hängt ab von:** 104
**Modell:** opus
**IDs für neue Befunde:** Ä244–Ä246

## Umfang
- Startseite nach Anmeldung: nächster Termin, darunter alle Termine
- Verschieben und Absagen (web/src/booking/Move.tsx, ManagePage.tsx) mit Fristen und Gebühren nur aus der API
- Termin als .ics und „Zum Kalender hinzufügen“
- Gutscheine (Vouchers.tsx) mit Restwert
- Anamnese vorab (AnamnesisPage.tsx), verschlüsselt im Schema `health`
- PWA installierbar; Bundle bleibt unter 100 kB gzip, CSP nach ADR 0080

## Muster übernehmen
- web/src/booking/MyAppointmentsPage.tsx
- docs/entscheidungen/0080-csp-fuer-den-kundenbereich.md

## Voraussichtlich geänderte Dateien
- web/src/booking/
- src/AispaStudio.Api/
- web/src/help/content/

## NICHT bauen
- Kundenkonto-Pflicht zum Buchen
- Zahlung in der App über das heutige Maß hinaus

## Prompt
```
Lies CLAUDE.md, work/conventions.md, die Rundenakte work/rounds/R05-*.md und diese Slice-Datei.
Setze Slice 108 "Web: Handy-App für Kunden zur Terminkontrolle" um (diese Datei).
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
- [ ] Given ein Termin in 12 Stunden und Absagefrist 24 Stunden When der Gast absagen will Then zeigt die App die Gebühr aus der API vor dem Bestätigen
- [ ] Given ein Termin When der Gast „Zum Kalender hinzufügen“ wählt Then lädt eine .ics mit Zeit, Behandlung und Adresse
- [ ] Given der Build When check:bundle läuft Then ist der Kundenbereich unter 100 kB gzip
- [ ] Manuell: Als Gast am Handy anmelden, Termin verschieben, an der Rezeption gegenprüfen

## Nicht in dieser Slice
–
