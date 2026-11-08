# Slice 25 – Dienstplan lesbar, Meldungen der Kasse und Buchhaltung mit Rolle

**Ziel:** Das Dienstplan-Raster ist für Screenreader lesbar, Warnungen stehen sichtbar, und Meldungen sind am Ort der Aktion mit passender Rolle.
**Runde:** R01
**Quelle:** R01 – B45, B46, B38, B53
**Hängt ab von:** –
**Modell:** –

## Umfang
- roster/RosterPage.tsx: Zellenknopf ohne überschreibendes `aria-label`, Person und Tag als versteckter Vorspann (B45); Warnungen sichtbar statt nur im `title` (B46)
- pos/PosPage.tsx :123-127: Ausfall des Signaturgeräts mit `role="alert"`, übrige Warnungen `role="status"` (B38)
- accounting/AccountingPage.tsx und payroll/PayrollPage.tsx: Meldung unter der jeweiligen Aktionsleiste, Fehler mit `role="alert"` (B53)

## Muster übernehmen
- web/src/accounting/AccountingPage.tsx:114 – heutige Meldung mit `role="status"`

## NICHT bauen
- B47 Rückfrage beim Verwerfen (Slice 14)
- B48 bis B52 weitere Dienstplan-Punkte (offene Entscheidung)

## Prompt
```
Lies CLAUDE.md, work/conventions.md, die Rundenakte work/rounds/R01-*.md und diese Slice-Datei.
Setze Slice 25 "Dienstplan lesbar, Meldungen der Kasse und Buchhaltung mit Rolle" um (diese Datei).
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
- [ ] Given eine Dienstplan-Zelle mit Schicht 09:00–17:00 und einer Warnung When ihr zugänglicher Name berechnet wird Then enthält er Person, Tag, „09:00–17:00“ und den Warntext
- [ ] Given eine Zeilenwarnung „Stunden“ When die Tabelle gerendert wird Then steht der Warntext sichtbar und nicht nur im `title`
- [ ] Given das Signaturgerät ist ausgefallen When die Kasse lädt Then hat die Meldung `role="alert"`
- [ ] Given ein Fehler „BU-Schlüssel fehlt“ beim Speichern unten When er erscheint Then steht er unter dieser Aktionsleiste mit `role="alert"`
- [ ] Manuell: Dienstplan öffnen: Warnungen stehen sichtbar in der Zelle; Buchhaltung › Speichern ohne BU-Schlüssel: Fehler direkt unter dem Knopf

## Nicht in dieser Slice
Neue Dienstplan-Funktionen
