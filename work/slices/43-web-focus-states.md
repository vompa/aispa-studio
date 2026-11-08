# Slice 43 – Fokus und Zustände im Web

**Ziel:** Fokus nach Seitenwechsel und Austausch, Zustände in der Navigation und an Auswahl, Sperrgrund und Fehlerübersicht sind für Tastatur und Screenreader richtig.
**Runde:** R02
**Quelle:** R02 – B14, B15, B33, B39, B41, B59, B61
**Hängt ab von:** –
**Modell:** sonnet

## Umfang
- Fokus nach Seitenwechsel auf die Überschrift (B14); `aria-current="page"` statt `aria-pressed` an den Seitenreitern (B15)
- Personenliste mit `aria-current` (B33); „Abrechnen“ je Zeile mit Bezug, Chips mit Zustand (B39)
- „Stornieren“ gesperrt mit `aria-disabled` und sichtbarem Grund (B41); Fokus nach Austausch im Betreiberbereich (B59); Fehlerübersicht im Kundenbereich mit Sprunglinks (B61)

## Muster übernehmen
–

## NICHT bauen
–

## Prompt
```
Lies CLAUDE.md, work/conventions.md, die Rundenakte work/rounds/R02-*.md und diese Slice-Datei.
Setze Slice 43 "Fokus und Zustände im Web" um (diese Datei).
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
- [ ] Given die Verwaltung When ein Seitenreiter gewählt wird Then liegt der Fokus auf der Überschrift der Seite und der Reiter hat `aria-current="page"`
- [ ] Given ein gesperrtes „Stornieren“ When es den Fokus bekommt Then ist der Grund sichtbar und per `aria-describedby` verbunden
- [ ] Given der Kundenbereich mit zwei Fehlern When abgeschickt wird Then nennt die Übersicht beide Felder als Links
- [ ] Manuell: Verwaltung nur mit Tastatur: nach Seitenwechsel steht der Fokus auf der Überschrift

## Nicht in dieser Slice
–
