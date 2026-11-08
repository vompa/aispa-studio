# Slice 47 – Content-Security-Policy für den Kundenbereich

**Ziel:** Der Kundenbereich `/buchen/` liefert eine Content-Security-Policy aus, zuerst nur als Bericht.
**Runde:** R02
**Quelle:** R02 – B31
**Hängt ab von:** –
**Modell:** opus

## Umfang
- Header `Content-Security-Policy-Report-Only` für `/buchen/` (`default-src 'self'`, Stripe-Quellen gezielt)
- ADR 0080 „CSP für den Kundenbereich“ (Grundsatz), Weg zum Umschalten auf durchsetzend

## Muster übernehmen
–

## NICHT bauen
- CSP für die Team-Oberfläche

## Prompt
```
Lies CLAUDE.md, work/conventions.md, die Rundenakte work/rounds/R02-*.md und diese Slice-Datei.
Setze Slice 47 "Content-Security-Policy für den Kundenbereich" um (diese Datei).
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
- [ ] Given eine Anfrage an `/buchen/` When die Antwort geprüft wird Then trägt sie `Content-Security-Policy-Report-Only` mit `default-src 'self'`
- [ ] Given der Kundenbereich mit Bezahlung When er im Browser läuft Then meldet die Konsole keinen CSP-Verstoß
- [ ] Manuell: Kundenbereich im Browser öffnen, Konsole: keine CSP-Meldungen

## Nicht in dieser Slice
–
