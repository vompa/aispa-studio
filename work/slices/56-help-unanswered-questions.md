# Slice 56 – Hilfe: unbeantwortete Fragen an die KI für die Leitung

**Ziel:** Fragen, die der KI-Assistent nicht beantworten konnte, sammelt die App auf diesem PC und zeigt sie der Leitung als Liste, damit die Hilfe nachwächst.
**Runde:** R02
**Quelle:** R02 – Ruling zur Pflege der Hilfe (Brief Ä76)
**Hängt ab von:** –
**Modell:** opus

## Umfang
- Zuerst Datenschutz prüfen und im PR-Protokoll festhalten: was gespeichert wird (Frage ohne Gästedaten, Zeit, Seite), wie lange (z. B. 90 Tage), wer es sieht (Leitung, Inhaber); Gästenamen und Kontaktdaten vor dem Speichern entfernen oder die Frage verwerfen
- Assistent (`CompanionViewModel`, Web `assistant/`): eine Antwort ohne Treffer legt die Frage lokal ab
- Liste „Offene Fragen an die Hilfe“ in der Verwaltung oder unter Hilfe für Leitung und Inhaber, mit „Erledigt“ je Eintrag; Hilfetext dazu

## Muster übernehmen
- src/AispaStudio.Desktop/Services/AiFeedbackLog.cs:23 – lokale Ablage ohne Gästedaten

## NICHT bauen
- Zentrale Sammlung
- Automatisches Schreiben von Hilfetexten

## Prompt
```
Lies CLAUDE.md, work/conventions.md, die Rundenakte work/rounds/R02-*.md und diese Slice-Datei.
Setze Slice 56 "Hilfe: unbeantwortete Fragen an die KI für die Leitung" um (diese Datei).
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
- [ ] Given eine Frage ohne Antwort When der Assistent sie meldet Then liegt sie ohne Gästedaten mit Zeit und Seite im lokalen Speicher
- [ ] Given eine Frage mit einem Gästenamen aus dem Kalender When sie abgelegt wird Then ist der Name entfernt oder die Frage nicht gespeichert
- [ ] Given die Liste als Rezeption When sie aufgerufen wird Then ist sie nicht sichtbar; als Leitung sichtbar mit „Erledigt“ je Eintrag
- [ ] Manuell: Assistent fragen „Wie drucke ich den Dienstplan?“ (keine Antwort): als Leitung erscheint die Frage in der Liste; als Rezeption nicht

## Nicht in dieser Slice
Statistik über mehrere PCs
