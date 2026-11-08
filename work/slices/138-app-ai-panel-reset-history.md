# Slice 138 – KI und Hilfe: leer nach dem Schließen, „Zuletzt gefragt“

**Ziel:** „KI und Hilfe“ beginnt nach dem Schließen wieder wie beim ersten Aufruf und bietet die Fragen von heute zum erneuten Stellen an.
**Runde:** R07
**Quelle:** R07 – Ä347
**Hängt ab von:** –
**Modell:** sonnet
**Prüfstufe:** –
**IDs für neue Befunde:** Ä358–Ä360

## Umfang
- Schließen (X oder Esc) setzt das Panel zurück: beim nächsten Öffnen Hilfe zur aktuellen Seite, leeres Fragefeld mit Fokus, keine alte Antwort
- Knopf „Neu beginnen“ im Kopf neben dem X setzt zurück, ohne zu schließen
- „Zuletzt gefragt“ unter dem Fragefeld: die letzten 5 Fragen von heute als kurze Zeilen; ein Klick stellt die Frage neu (frische Antwort, keine gespeicherte); doppelte Fragen einmal, nach oben gerückt
- Der Verlauf gehört der angemeldeten Person, gilt nur für heute, ist nach Mitternacht und beim Abmelden leer und liegt nur lokal auf dem Gerät; Antworten werden nicht gespeichert
- Pfeil nach oben im leeren Fragefeld holt die letzte Frage
- Auf der Seite „Hilfe“ entfällt der Teil zur Seite („Zu jeder Einstellung …“, „Alle Hilfethemen“); es bleibt der Assistent
- Hilfe zu „KI und Hilfe“ nachziehen

## Muster übernehmen
- src/AispaStudio.Desktop/Views/CompanionContent.axaml
- src/AispaStudio.Desktop/ViewModels/CompanionViewModel.cs
- gemerkte Ansichtszustände der App (wie die Spalte aus Slice 129)
- .claude/skills/fluent2-avalonia (responsible-ai.md, accessibility.md)

## Voraussichtlich geänderte Dateien
- src/AispaStudio.Desktop/Views/CompanionContent.axaml
- src/AispaStudio.Desktop/ViewModels/CompanionViewModel.cs
- src/AispaStudio.Desktop/ViewModels/CompanionViewModel.List.cs
- tests/AispaStudio.Desktop.Tests/
- web/src/help/content/
- web/src/help/images/app/

## NICHT bauen
- Verlauf auf dem Server oder über Geräte hinweg
- gespeicherte Antworten

## Prompt
```
Lies CLAUDE.md, work/conventions.md, die Rundenakte work/rounds/R07-*.md und diese Slice-Datei.
Setze Slice 138 "KI und Hilfe: leer nach dem Schließen, „Zuletzt gefragt“" um (diese Datei).
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
- [ ] Given im Panel steht eine Antwort When man es schließt und wieder öffnet Then ist das Fragefeld leer und fokussiert und keine Antwort zu sehen
- [ ] Given man hat heute „wer ist heute noch frei“ gefragt When man das Panel neu öffnet und die Frage unter „Zuletzt gefragt“ anklickt Then wird die Frage neu an die API geschickt
- [ ] Given Person A hat heute gefragt When Person B sich am selben Gerät anmeldet Then ist „Zuletzt gefragt“ leer
- [ ] Given eine Frage von gestern When man heute öffnet Then steht sie nicht unter „Zuletzt gefragt“
- [ ] Given das Fragefeld ist leer und fokussiert When man Pfeil nach oben drückt Then steht die letzte Frage im Feld
- [ ] Manuell: Als Rezeption fragen, schließen, neu öffnen, „Zuletzt gefragt“ nutzen, „Neu beginnen“; abmelden, als Inhaberin anmelden und den leeren Verlauf sehen

## Nicht in dieser Slice
–
