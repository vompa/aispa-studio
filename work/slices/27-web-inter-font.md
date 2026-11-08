# Slice 27 – Hausschrift Inter in der Verwaltung ausliefern

**Ziel:** Die Verwaltung im WebView2 zeigt dieselbe Schrift wie die Windows-App.
**Runde:** R01
**Quelle:** R01 – B62
**Hängt ab von:** –
**Modell:** –

## Umfang
- Inter als lokale Variable-WOFF2 (Teilmenge Latin) mit `@font-face`, `font-display: swap` und `<link rel="preload">` in der Team-Oberfläche
- Kundenbereich bleibt bei der Systemschrift; das wird im Brief vermerkt

## Muster übernehmen
- web/src/index.css:49 – heutige Schriftliste mit Inter zuerst

## NICHT bauen
- Neues npm-Paket ohne Freigabe: braucht die Schriftdatei ein Paket, ist das eine Frage im Plan (NEEDS-DECISION)

## Prompt
```
Lies CLAUDE.md, work/conventions.md, die Rundenakte work/rounds/R01-*.md und diese Slice-Datei.
Setze Slice 27 "Hausschrift Inter in der Verwaltung ausliefern" um (diese Datei).
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
- [ ] Given der Build When `dist` geprüft wird Then enthält er eine Inter-WOFF2 und das CSS der Team-Oberfläche ein `@font-face` für Inter
- [ ] Given der Kundenbereich When `npm run check:bundle` läuft Then bleibt er unter 100 kB gzip und lädt keine Inter-Datei
- [ ] Manuell: Verwaltung in der Windows-App öffnen: die Schrift gleicht der App (Inter)

## Nicht in dieser Slice
Neue Schriftgrößen
