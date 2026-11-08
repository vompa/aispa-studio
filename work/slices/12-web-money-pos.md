# Slice 12 – Geldeingabe an der Kassa im Web mit Fehlertext statt stiller 0

**Ziel:** Ungültige Beträge an der Web-Kassa werden gemeldet statt still zu 0 zu werden.
**Runde:** R01
**Quelle:** R01 – B37
**Hängt ab von:** –
**Modell:** –

## Umfang
- Gemeinsames Geldfeld im Web: Textfeld mit `inputMode="decimal"`, tolerant lesen, beim Verlassen ins Anzeigeformat normalisieren, Fehlertext am Feld
- Parser nach Brief 5.20 ersetzt `parseMoney` (pos/money.ts)
- Die acht Betragsfelder in pos/BillPanel.tsx (:306-334, :375-409) und pos/DayPanel.tsx (:78, :98) darauf umstellen

## Muster übernehmen
- src/AispaStudio.Desktop/Controls/MoneyInputParser.cs:13 – Regeln der Windows-App für Geldeingaben
- web/src/pos/money.ts:8 – heutiger Parser, der ersetzt wird

## NICHT bauen
- B42 Anzeigeformat bei „Rest bar“ und „Rest Karte“ (offene Entscheidung)
- B43 Ansage von „Bar zu zahlen“ (offene Entscheidung)
- B40 Rückfrage beim Tagesabschluss (offene Entscheidung)
- Preise in den Stammdaten (Slice 13)

## Prompt
```
Lies CLAUDE.md, work/conventions.md, die Rundenakte work/rounds/R01-*.md und diese Slice-Datei.
Setze Slice 12 "Geldeingabe an der Kassa im Web mit Fehlertext statt stiller 0" um (diese Datei).
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
- [ ] Given die Eingabe „12,5x“ When das Feld verlassen wird Then steht ein Fehlertext am Feld und Bezahlen löst keine Zahlung aus
- [ ] Given die Eingabe „12.345,50“ When geparst wird Then ist der Betrag 12345,50
- [ ] Given die Eingabe „1e3“ When geparst wird Then gilt sie als ungültig
- [ ] Given die Eingabe „12,505“ When geparst wird Then gilt sie als ungültig (höchstens zwei Nachkommastellen)
- [ ] Given ein leeres Pflichtfeld When gespeichert wird Then gilt es nicht als 0, sondern meldet einen Fehler
- [ ] Given die Eingabe „12,5“ When das Feld verlassen wird Then zeigt es „12,50“
- [ ] Manuell: Kassa im Web › Rechnung › Bar „12,5x“ eingeben und Feld verlassen: Fehlertext, keine Zahlung

## Nicht in dieser Slice
Neue Zahlungsarten
