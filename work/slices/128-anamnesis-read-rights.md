# Slice 128 – Anamnese: Leserecht der Rezeption klären und durchsetzen

**Ziel:** Der Widerspruch zwischen ADR 0013 und dem tatsächlichen Leserecht der Rezeption ist entschieden, in API, Oberflächen und Doku einheitlich umgesetzt und mit Rollentests festgelegt; vor der ersten Auslieferung fertig.
**Runde:** R05
**Quelle:** R05 – Ä304
**Hängt ab von:** 127
**Modell:** opus
**IDs für neue Befunde:** Ä305–Ä307

## Umfang
- Zuerst die Entscheidung des Auftraggebers nach Rücksprache mit der Datenschutz-Beratung einholen: Rezeption nur Stand (dann klären, wer fotografierte Bögen prüft) oder Festlegung ändern (Nachtrag zu ADR 0013)
- API-Rollentests für Stand, Antworten und alle Abläufe je Rolle
- Rechte in der API und ihre Anzeige in der Windows-App nach der Entscheidung; die App blendet nur aus
- ADR, docs/architektur.md, CLAUDE.md und Hilfe einheitlich nachziehen

## Muster übernehmen
- docs/entscheidungen ADR 0013
- tests/AispaStudio.Api.Tests/AnamnesisChannelTests.cs

## Voraussichtlich geänderte Dateien
- src/AispaStudio.Api/
- src/AispaStudio.Desktop/
- tests/AispaStudio.Api.Tests/
- tests/AispaStudio.Desktop.Tests/
- docs/entscheidungen/
- docs/architektur.md
- web/src/help/content/

## NICHT bauen
- Umsetzung vor der Entscheidung des Auftraggebers
- rechtliche Bewertung durch Claude als Ersatz für die Datenschutz-Beratung

## Prompt
```
Lies CLAUDE.md, work/conventions.md, die Rundenakte work/rounds/R05-*.md und diese Slice-Datei.
Setze Slice 128 "Anamnese: Leserecht der Rezeption klären und durchsetzen" um (diese Datei).
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
- [ ] Given jede Rolle When sie Stand, Antworten und jeden Ablauf der Anamnese aufruft Then entspricht die Antwort der API der entschiedenen Rechtetabelle
- [ ] Given die Entscheidung When die Doku gelesen wird Then widersprechen sich ADR, Architektur, CLAUDE.md und Hilfe nicht mehr
- [ ] Manuell: Anamnese je Rolle im echten Fenster durchgehen

## Nicht in dieser Slice
–
