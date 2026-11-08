# Slice 58 – Kundenbereich auf der eigenen Website einbetten

**Ziel:** Ein Spa kann den Kundenbereich auf seiner eigenen Website einbetten, aber nur von Websites, die es eingetragen hat.
**Runde:** R03
**Quelle:** R03 – Ruling Slice 47 aus R02
**Hängt ab von:** –
**Modell:** opus
**IDs für neue Befunde:** –

## Umfang
- Einstellung „Einbetten erlaubt auf“ je Mandant: eine Liste von Websites (nur `https://`, ohne Pfad), in der Verwaltung mit Hilfetext und Fragezeichen (ADR 0055), Endpunkt mit `RequirePermission`
- `frame-ancestors` der Content-Security-Policy von `/buchen/` aus dieser Liste, ohne Eintrag `'self'`; klären, wie `docker/nginx.conf` zur Liste kommt, sonst setzt nur die API den Header
- Nachtrag zu ADR 0080 „CSP für den Kundenbereich“ mit der Regel und dem Weg zum Umschalten

## Muster übernehmen
- src/AispaStudio.Api/System/BookingContentSecurityPolicy.cs:8 – Policy des Kundenbereichs
- tests/AispaStudio.Api.Tests/TeamWebRootTests.cs – Prüfung des Headers

## NICHT bauen
- CSP für die Team-Oberfläche
- Umschalten von Report-Only auf durchsetzend

## Prompt
```
Lies CLAUDE.md, work/conventions.md, die Rundenakte work/rounds/R03-*.md und diese Slice-Datei.
Setze Slice 58 "Kundenbereich auf der eigenen Website einbetten" um (diese Datei).
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
- [ ] Given keine eingetragene Website When `/buchen/` abgerufen wird Then enthält die Policy `frame-ancestors 'self'`
- [ ] Given eingetragen `https://www.beispiel-spa.at` When `/buchen/` abgerufen wird Then enthält die Policy `frame-ancestors 'self' https://www.beispiel-spa.at`
- [ ] Given die Eingabe `http://www.beispiel-spa.at` oder eine Adresse mit Pfad When gespeichert wird Then nennt ein Fehler am Feld den Grund und nichts wird gespeichert
- [ ] Manuell: Website eintragen und den Kundenbereich auf einer Testseite per `iframe` einbetten: die Konsole meldet keinen Verstoß gegen `frame-ancestors`

## Nicht in dieser Slice
–
