# Slice 41 – Farbtoken an einer Stelle im Web

**Ziel:** Die schlanken Oberflächen (Kundenbereich, Mein Tag, Betreiber, Lager) nutzen eine gemeinsame Token-Datei statt eigener Kopien.
**Runde:** R02
**Quelle:** R02 – B28, B69
**Hängt ab von:** –
**Modell:** haiku

## Umfang
- `--stock-*` durch die Token aus `index.css` ersetzen (B28)
- Gemeinsame `tokens.css` für booking, myday, operator, stock ohne Import aus der Team-Oberfläche; Prüftest auf alle CSS-Dateien erweitern (B69)

## Muster übernehmen
–

## NICHT bauen
–

## Prompt
```
Lies CLAUDE.md, work/conventions.md, die Rundenakte work/rounds/R02-*.md und diese Slice-Datei.
Setze Slice 41 "Farbtoken an einer Stelle im Web" um (diese Datei).
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
- [ ] Given booking.css, myday.css, operator.css und stock.css When ein Prüftest eigene Farbdefinitionen der Palette sucht Then stehen sie nur in tokens.css
- [ ] Given der Kundenbereich When gebaut Then bleibt er unter 100 kB gzip und importiert nichts aus der Team-Oberfläche
- [ ] Manuell: Kundenbereich und Lager hell und dunkel ansehen: Farben wie vorher

## Nicht in dieser Slice
Neue Farben
