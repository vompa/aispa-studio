# Slice 87 – Hilfe aus „Freie Zeit suchen“ öffnet das Thema statt einer 404

**Ziel:** Jeder Hilfe-Knopf der Windows-App öffnet sein Thema; eine fehlende Seite zeigt eine verständliche Meldung statt JSON.
**Runde:** R04
**Quelle:** R04 – Ä155
**Hängt ab von:** –
**Modell:** sonnet
**IDs für neue Befunde:** Ä174–Ä176

## Umfang
- Ursache nachstellen: Klick auf Hilfe in „Freie Zeit suchen“ zeigt `{"title":"Not Found","status":404,…}` (Adresse der Hilfe, Server statt Hilfe-Ordner, Pfad `?thema=`)
- Scan-Test: jeder Hilfe-Knopf und jedes Fragezeichen der Windows-App löst ein vorhandenes Thema (und Abschnitt) auf
- Fehlerseite der Hilfe: „Thema nicht gefunden“ mit Link zur Übersicht, nie JSON

## Muster übernehmen
- src/AispaStudio.Desktop/Services/HelpContent.cs – Themen der Hilfe
- src/AispaStudio.Desktop/Controls/HelpTeaser.cs – Hilfe-Auszüge

## Voraussichtlich geänderte Dateien
- src/AispaStudio.Desktop/Services/
- web/src/help/

## NICHT bauen
- Neue Hilfethemen

## Prompt
```
Lies CLAUDE.md, work/conventions.md, die Rundenakte work/rounds/R04-*.md und diese Slice-Datei.
Setze Slice 87 "Hilfe aus „Freie Zeit suchen“ öffnet das Thema statt einer 404" um (diese Datei).
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
- [ ] Given „Freie Zeit suchen“ When auf Hilfe geklickt wird Then öffnet die Hilfe das Thema „Freie Zeit suchen“
- [ ] Given alle Hilfe-Knöpfe der Windows-App When ihre Ziele aufgelöst werden Then gibt es zu jedem ein Thema
- [ ] Manuell: Freie Zeit suchen → Hilfe: das Thema erscheint

## Nicht in dieser Slice
–
