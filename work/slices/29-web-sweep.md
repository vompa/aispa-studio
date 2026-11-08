# Slice 29 – Kleinigkeiten Web: Beschriftungen im Buchungsfeld, Standorte als Gruppe

**Ziel:** Kleine Verstöße im Web in einem Durchgang beheben.
**Runde:** R01
**Quelle:** R01 – B8, B34
**Hängt ab von:** –
**Modell:** –

## Umfang
- calendar/BookingPanel.tsx :237-241: vier Felder des Neukunden mit sichtbarer Beschriftung und `autoComplete="off"` (B8)
- admin/StaffPage.tsx :240: Standorte in `<fieldset>` mit `<legend>Standorte</legend>`, HelpButton in der Legende (B34)

## Muster übernehmen
- web/src/stock/stockUi.tsx:23 – vorhandenes `Field` mit Beschriftung

## NICHT bauen
- B9 gemeinsamer Formularbaustein (offene Entscheidung)

## Prompt
```
Lies CLAUDE.md, work/conventions.md, die Rundenakte work/rounds/R01-*.md und diese Slice-Datei.
Setze Slice 29 "Kleinigkeiten Web: Beschriftungen im Buchungsfeld, Standorte als Gruppe" um (diese Datei).
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
- [ ] Given das Buchungsfeld mit „Neukunde“ When das DOM geprüft wird Then hat jedes der vier Felder ein verbundenes `label` und `autoComplete="off"`
- [ ] Given die Seite „Zugänge“ When das DOM geprüft wird Then stehen die Standort-Kontrollkästchen in einem `fieldset` mit der Legende „Standorte“
- [ ] Manuell: Kalender im Web › + Termin › Neukunde: über jedem Feld steht eine Beschriftung

## Nicht in dieser Slice
Neue Felder
