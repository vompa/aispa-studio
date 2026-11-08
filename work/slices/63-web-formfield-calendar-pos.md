# Slice 63 – Formularbaustein in Kalender, Posteingang und Kassa

**Ziel:** Die Felder in Kalender, Posteingang und Kassa hängen Hilfetext und Fehler ans Feld; Knöpfe in Formularen tragen `type`.
**Runde:** R03
**Quelle:** R03 – B84, B87
**Hängt ab von:** –
**Modell:** sonnet
**IDs für neue Befunde:** B104–B108

## Umfang
- `calendar/AppointmentPanel.tsx`, `BookingPanel.tsx`, `AreaPanel.tsx`, `inbox/InboxPage.tsx`, `pos/BillPanel.tsx`, `ReceiptView.tsx`, `DayPanel.tsx` auf `FormField`; die Fehlermeldungen unter Aktionsleisten ans Feld, wo sie ein Feld betreffen
- Jeder Knopf, der dadurch in einem `<form>` steht, trägt `type` (B87)
- Das Geldfeld `pos/MoneyField.tsx` (Slice 12) und die Rückfragen aus Slice 39 bleiben

## Muster übernehmen
- web/src/components/FormField.tsx:14 – FormField
- web/src/pos/MoneyField.tsx – Geldfeld mit Fehler am Feld

## NICHT bauen
- Neue Felder
- Umgestaltung von Kalender oder Kassa

## Prompt
```
Lies CLAUDE.md, work/conventions.md, die Rundenakte work/rounds/R03-*.md und diese Slice-Datei.
Setze Slice 63 "Formularbaustein in Kalender, Posteingang und Kassa" um (diese Datei).
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
- [ ] Given „Neuer Kunde“ im Buchungsfeld ohne Vor- und Nachnamen When gebucht wird Then steht der Fehler am Feld, ist per `aria-describedby` verbunden und der Fokus liegt im Feld
- [ ] Given die Dateien unter `calendar/`, `inbox/` und `pos/` When ein Prüftest `<button` ohne `type` innerhalb eines `<form>` sucht Then gibt es keinen Treffer
- [ ] Manuell: Kalender › Termin anlegen › Neuer Kunde ohne Namen buchen: Fehler am Feld, Fokus im Feld

## Nicht in dieser Slice
–
