# Slice 106 – Web: Handy-App für Mitarbeitende

**Ziel:** Aus „Mein Tag“ wird die installierbare Handy-App für Mitarbeitende: eigener Plan, nächster Termin mit kurzen Hinweisen zum Gast, Termin begonnen oder beendet melden, Abwesenheit beantragen, Lesen ohne Netz.
**Runde:** R05
**Quelle:** R05 – Ä193
**Hängt ab von:** 104
**Modell:** opus
**IDs für neue Befunde:** Ä238–Ä240

## Umfang
- PWA mit Manifest und Service Worker, installierbar auf Android und iOS
- Tagesplan und Woche, nächster Termin mit Hinweisen (Wünsche, Allergie-Kennzeichen ohne Inhalt aus `health`)
- Termin begonnen / beendet melden (API prüft, Kalender zeigt es)
- Abwesenheit beantragen; Genehmigung in der Windows-App (Slice 99). Slice 99 legt den Vertrag `AbsenceRequestDto` (`RosterContracts.cs`) und die Routen fest, die die Windows-App aufruft: `GET api/absence-requests` (offene Anträge, Recht ManageLocation), `POST api/absence-requests/{id}/approve` und `POST api/absence-requests/{id}/reject` (je ohne Körper, Antwort 204); bis dahin antwortet die API mit 404, und die Liste in der Windows-App ist leer. Beim Genehmigen trägt die API die Abwesenheit im Dienstplan ein und prüft Termine wie `PUT …/roster/{employeeId}/{date}`
- Anmeldung mit Passkey (web/src/auth/passkeys.ts)
- Offline: letzter Stand lesbar mit Hinweis auf das Alter

## Muster übernehmen
- web/src/myday/ – Plan, offline.ts
- web/src/auth/passkeys.ts

## Voraussichtlich geänderte Dateien
- web/src/myday/
- src/AispaStudio.Api/
- web/src/help/content/

## NICHT bauen
- Buchen neuer Termine
- Kassa
- Push (zurückgestellt, E7)
- Zeiterfassung per Handy, Dienste tauschen, eigene Zertifikate (E6)

## Prompt
```
Lies CLAUDE.md, work/conventions.md, die Rundenakte work/rounds/R05-*.md und diese Slice-Datei.
Setze Slice 106 "Web: Handy-App für Mitarbeitende" um (diese Datei).
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
- [ ] Given Lena meldet „begonnen“ When die Rezeption den Kalender ansieht Then steht der Termin als laufend
- [ ] Given Lena beantragt Freitag frei When die Managerin die Windows-App öffnet Then steht der Antrag in der Liste offener Anträge
- [ ] Given kein Netz When Lena die App öffnet Then sieht sie den letzten Plan mit „Stand 08:12“
- [ ] Given die Managerin lehnt Lenas Antrag ab oder genehmigt ihn When Lena die App öffnet Then sieht sie die Entscheidung (Pflicht: die Windows-App sagt bewusst noch nicht, dass die Person davon erfährt; danach Rückfrage und Hilfe `dienstplan-antrag-entscheiden.md` ergänzen)
- [ ] Manuell: App auf einem Handy installieren, Flugmodus an, Plan öffnen

## Nicht in dieser Slice
–
