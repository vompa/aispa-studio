# Slice 121 – API-Version in den Pfaden

**Ziel:** Die Windows-App spricht die API unter `/api/v1/…` an, die alten Pfade bleiben für bereits ausgelieferte Clients erreichbar; Breaking Changes kommen nur über eine neue Version. ADR 0085 hält die Regel fest.
**Runde:** R06
**Quelle:** R06 – Ä283
**Hängt ab von:** 120
**Modell:** opus
**IDs für neue Befunde:** –

## Umfang
- ADR 0085 nach der Vorlage: Pfadversion, Regel für Breaking Changes, wie lange alte Pfade bleiben
- `/api/v1` als gemeinsamer Präfix aller Team-Endpunkte; `/api/…` bleibt als Alias, damit ausgelieferte Clients und die Web-Apps weiterlaufen
- Rechte und Kanal-Sperre (Slice 104) gelten unter beiden Pfaden gleich; ein Test durchläuft alle Endpunkte
- Windows-App (gemeinsamer Client aus Slice 120) und Hilfe-Links auf v1; OpenAPI und `npm run gen:api` nachziehen
- Live-Hub, öffentliche Endpunkte und Sprungziele prüfen und im ADR einordnen

## Muster übernehmen
- src/AispaStudio.Api/Program.cs
- src/AispaStudio.Api/Auth/
- docs/entscheidungen/vorlage.md

## Voraussichtlich geänderte Dateien
- src/AispaStudio.Api/
- src/AispaStudio.Desktop/
- web/openapi/aispa-api.json
- web/src/
- docs/entscheidungen/
- tests/

## NICHT bauen
- Version 2 oder Abschalten alter Pfade
- Paket für Versionierung, solange ein Pfadpräfix genügt

## Prompt
```
Lies CLAUDE.md, work/conventions.md, die Rundenakte work/rounds/R06-*.md und diese Slice-Datei.
Setze Slice 121 "API-Version in den Pfaden" um (diese Datei).
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
- [ ] Given ein Endpunkt mit Rechteprüfung When er über `/api/v1` und über `/api/` ohne Recht aufgerufen wird Then lehnen beide gleich ab
- [ ] Given die Windows-App When sie einen Termin lädt Then geht der Aufruf an `/api/v1`
- [ ] Given ein Client mit altem Pfad When er einen Termin lädt Then bekommt er dieselbe Antwort


## Nicht in dieser Slice
–
