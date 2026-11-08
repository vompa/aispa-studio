# Slice 104 – API: Rechte je Kanal für Dashboard, Mitarbeiter-App und Kunden-App

**Ziel:** Die API kennt drei eingeschränkte Kanäle: ein gekoppeltes Anzeigegerät (nur lesen, keine Personendaten außer Initialen), die Mitarbeiter-App (nur eigene Daten) und die Kunden-App (nur eigene Termine); jede Freigabe hat Tests für erlaubt und abgelehnt.
**Runde:** R05
**Quelle:** R05 – Ä195
**Hängt ab von:** 92
**Modell:** opus
**IDs für neue Befunde:** Ä232–Ä234

## Umfang
- Gerätezugang für Anzeigen: Kopplung über die native Seite Geräte (Code auf dem Bildschirm), widerrufbar, nur lesend
- Kanal-Kennzeichen am Anmelde-Cookie bzw. Token; `RequirePermission` berücksichtigt den Kanal
- Endpunkt-Liste je Kanal als Allow-Liste mit Scan-Test über alle Endpunkte
- Verlauf (Slice 90) nennt den Kanal

## Muster übernehmen
- src/AispaStudio.Desktop/Views/DevicesView.axaml – Kopplung
- Endpunkte mit `RequirePermission(AccessRight...)`

## Voraussichtlich geänderte Dateien
- src/AispaStudio.Api/
- src/AispaStudio.Desktop/Views/DevicesView.axaml
- tests/AispaStudio.Api.Tests/

## NICHT bauen
- Oberflächen der drei Web-Apps (Slices 105–108)

## Prompt
```
Lies CLAUDE.md, work/conventions.md, die Rundenakte work/rounds/R05-*.md und diese Slice-Datei.
Setze Slice 104 "API: Rechte je Kanal für Dashboard, Mitarbeiter-App und Kunden-App" um (diese Datei).
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
- [ ] Given ein gekoppeltes Anzeigegerät When es einen Termin ändern will Then antwortet die API 403
- [ ] Given Lena in der Mitarbeiter-App When sie den Plan von Sophie abruft Then antwortet die API 403
- [ ] Given ein neuer Endpunkt ohne Kanal-Freigabe When der Scan-Test läuft Then ist er rot
- [ ] Manuell: Anzeigegerät koppeln und in der Geräte-Seite widerrufen: Anzeige meldet sich ab

## Nicht in dieser Slice
–
