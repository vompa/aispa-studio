# Slice 05 – Meldungen in Kalender, Kunden, Kassa und Posteingang; Stil Border.alert entfällt

**Ziel:** Alle übrigen Meldungen der Windows-App sind InfoBars oder angesagte Statuszeilen, und der rot-feste Stil `Border.alert` ist entfernt.
**Runde:** R01
**Quelle:** R01 – W33, W46, W48, W63, W-Muster 2 (Rest), W-Muster 3 (Rest)
**Hängt ab von:** 03, 04
**Modell:** –

## Umfang
- CalendarView (8 Stellen), CustomersView (5), PosView, InboxView: `Border.alert` als InfoBar mit passender Severity
- InboxView `Notice` :287-290 als InfoBar mit Severity nach `NoticeIsError` (W33)
- PosView Kartenzahlung :419-435: Ergebnis mit Symbol, LiveSetting Assertive bei Fehlschlag und Polite bei Erfolg; Wechselgeld :443 und Druckstatus :248, :498 Polite (W46)
- PosView `InvoiceHint` :212, :505 als InfoBar oder Fehler am Feld (W48)
- Stil `Border.alert` in Themes/Styles.axaml entfernen (W63)

## Muster übernehmen
- src/AispaStudio.Desktop/Controls/InfoBar.cs:23 – InfoBar
- src/AispaStudio.Desktop/Views/CalendarView.axaml:602 – Fehler einer Aktion als InfoBar

## NICHT bauen
- Toasts statt Zeilentext neu einführen, wo heute keiner ist
- Feldnamen dieser Seiten (Slices 07 bis 10)

## Prompt
```
Lies CLAUDE.md, work/conventions.md, die Rundenakte work/rounds/R01-*.md und diese Slice-Datei.
Setze Slice 05 "Meldungen in Kalender, Kunden, Kassa und Posteingang; Stil Border.alert entfällt" um (diese Datei).
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
- [ ] Given alle AXAML-Dateien unter src/AispaStudio.Desktop When ein Test sie liest Then kommt `Classes="alert"` nicht mehr vor und Styles.axaml enthält keinen Selektor `Border.alert`
- [ ] Given im Posteingang ist `Notice` gesetzt und `NoticeIsError` wahr When die Meldung erscheint Then ist sie eine InfoBar mit Severity Error
- [ ] Given eine abgelehnte Kartenzahlung When das Ergebnis erscheint Then trägt es ein Fehlersymbol und LiveSetting Assertive
- [ ] Manuell: Kassa › Kassieren › Karte mit dem Test-Terminal ablehnen lassen: Meldung mit Fehlersymbol

## Nicht in dieser Slice
Neue Meldungstexte
