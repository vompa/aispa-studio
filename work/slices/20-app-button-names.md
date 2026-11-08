# Slice 20 – Knopfnamen mit Bezug statt Zahl, Uhrzeit oder Initialen

**Ziel:** Knöpfe mit mehreren Texten und gleichlautende Zeilenknöpfe haben Namen, die sagen, was sie tun und worauf.
**Runde:** R01
**Quelle:** R01 – W14, W20, W61, W47 (Zeilen), W-Muster 6
**Hängt ab von:** 04, 07, 08, 09
**Modell:** –

## Umfang
- SyncView :54 und ShellView :134 „Erledigt: {Bereich} {Zeit}“ (W14)
- ShellView: Konto „Konto {UserText}“, Farbschema „Farbschema: {ThemeText}“, Navigationszeile „{Title}, {Badge} offen“ (W20)
- CalendarView: Kennzahlen :2128-2155 „{Label}, {Wert}“, „Als Nächstes“ :2193, Buchungsteil :208, „Baustein entfernen“ :1019, „Gast entfernen“ :1395, :1421 (W61)
- PosView: offene Termine :58 mit Gast, Zeilenknöpfe :286 und :290 mit Bezug (W47); CustomersView :227 „Adresse entfernen“ mit Bezug

## Muster übernehmen
- src/AispaStudio.Desktop/Views/ShellView.axaml:14 – Name per Bindung statt aus dem Inhalt

## NICHT bauen
- Knöpfe „Weniger“ und „Mehr“ beim Zähler (entfallen mit Slice 07)

## Prompt
```
Lies CLAUDE.md, work/conventions.md, die Rundenakte work/rounds/R01-*.md und diese Slice-Datei.
Setze Slice 20 "Knopfnamen mit Bezug statt Zahl, Uhrzeit oder Initialen" um (diese Datei).
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
- [ ] Given die Kennzahl „Termine“ mit dem Wert 12 When der Peer abgefragt wird Then lautet der Name „Termine, 12“
- [ ] Given der Posteingang hat 3 offene Nachrichten When die Navigationszeile abgefragt wird Then lautet ihr Name „Posteingang, 3 offen“
- [ ] Given eine abgelehnte Änderung im Abgleich When ihr Knopf abgefragt wird Then beginnt sein Name mit „Erledigt:“ und nennt Bereich und Zeit
- [ ] Manuell: Narrator an, Navigation durchgehen: „Posteingang, 3 offen“; Kalender › Kennzahlen: „Termine, 12“

## Nicht in dieser Slice
Neue Knöpfe
