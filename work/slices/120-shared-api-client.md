# Slice 120 – API-Client in die gemeinsame Bibliothek

**Ziel:** Der typisierte Zugriff auf die API liegt in einer Bibliothek ohne Oberflächen- und Plattformabhängigkeit, damit ein späterer Client ihn mitnutzen kann; die ViewModels hängen an einer Schnittstelle, nicht an HTTP.
**Runde:** R06
**Quelle:** R06 – Ä282
**Hängt ab von:** –
**Modell:** opus
**IDs für neue Befunde:** –

## Umfang
- HTTP-Teil von `src/AispaStudio.Desktop/Services/ApiClient.cs` (Senden, Fehler, 409-Ablehnung, Auth-Cookie, Zeitlimit, Wiederholung) in die gemeinsame Bibliothek; `AispaStudio.Contracts` bleibt reine Verträge, wenn der Client sonst zusätzliche Pakete in die API zöge (dann eigenes Projekt `AispaStudio.ApiClient`, im Plan begründen)
- Schnittstelle für die ViewModels; Offline-Warteschlange, Dateien und DPAPI bleiben in der Windows-App hinter dieser Schnittstelle
- Bestehender eigener Client bleibt (kein Refit, keine Code-Erzeugung), weil Warteschlange und `ClientId` daran hängen
- Tests umziehen, Verhalten bleibt gleich; Test-`StubApi` weiter nutzbar

## Muster übernehmen
- src/AispaStudio.Desktop/Services/ApiClient.cs
- src/AispaStudio.Contracts/AispaStudio.Contracts.csproj
- docs/architektur.md

## Voraussichtlich geänderte Dateien
- src/AispaStudio.Desktop/Services/ApiClient.cs
- src/AispaStudio.Contracts/
- src/AispaStudio.Desktop/ViewModels/
- tests/AispaStudio.Desktop.Tests/
- AispaStudio.slnx

## NICHT bauen
- neuer Web-Client
- Umstellen aller ViewModels auf neue Endpunkte
- neue Funktionen

## Prompt
```
Lies CLAUDE.md, work/conventions.md, die Rundenakte work/rounds/R06-*.md und diese Slice-Datei.
Setze Slice 120 "API-Client in die gemeinsame Bibliothek" um (diese Datei).
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
- [ ] Given die gemeinsame Bibliothek When ihre Abhängigkeiten geprüft werden Then enthält sie weder Avalonia noch Windows-Pakete
- [ ] Given die volle Desktop-Suite When sie nach dem Umzug läuft Then ist sie ohne geänderte Erwartungen grün


## Nicht in dieser Slice
–
