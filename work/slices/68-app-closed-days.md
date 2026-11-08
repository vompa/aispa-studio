# Slice 68 – Schließtage: die Rezeption darf alles eintragen

**Ziel:** An Schließtagen und an Tagen ohne Dienst darf die Rezeption Termine, Pausen, Notizen, Sperrzeiten und Sauna-Eintritte ganz normal eintragen; die Bearbeitung zeigt nur einen Hinweis.
**Runde:** R03
**Quelle:** R03 – Ä83, Ä89 (Ruling)
**Hängt ab von:** –
**Modell:** opus
**IDs für neue Befunde:** Ä104–Ä106

## Umfang
- Buchen im Haus (Windows-App und Web-Kalender) lehnt an einem Schließtag oder Tag ohne Dienst nicht mehr ab; die Antwort der API trägt einen Hinweis „Der Standort hat an diesem Tag geschlossen.“ und die Maske zeigt ihn, ohne etwas zu sperren
- Die Online-Buchung bleibt gesperrt: Gäste sehen an Schließtagen keine buchbaren Zeiten
- Nachtrag zu ADR 0073 „Schließzeiten und Terminabsage“ mit der Regel (Rezeption trägt ein, Gäste nicht) und dem Hinweistext; Hilfetext in `kalender-*` und `schliesszeiten.md`

## Muster übernehmen
- src/AispaStudio.Api/Scheduling/ResourcePlanner.cs – Prüfung auf Öffnungszeiten und Schließzeiten
- src/AispaStudio.Api/Closures/ClosureCancellations.cs – Umgang mit Terminen in Schließzeiten
- src/AispaStudio.Desktop/ViewModels/CalendarViewModel.Closures.cs – Anzeige von Schließzeiten im Kalender

## NICHT bauen
- Online-Buchung ändern
- Neue Rechte

## Prompt
```
Lies CLAUDE.md, work/conventions.md, die Rundenakte work/rounds/R03-*.md und diese Slice-Datei.
Setze Slice 68 "Schließtage: die Rezeption darf alles eintragen" um (diese Datei).
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
- [ ] Given ein Schließtag When die Rezeption einen Termin bucht Then wird er gespeichert und die Antwort trägt den Hinweis „Der Standort hat an diesem Tag geschlossen.“
- [ ] Given ein Schließtag When ein Gast online Zeiten abfragt Then gibt es keine buchbaren Zeiten
- [ ] Given die Maske „Neuer Termin“ für einen Schließtag When sie geöffnet wird Then steht der Hinweis in der Maske und „Termin buchen“ bleibt aktiv
- [ ] Manuell: Neuen Termin am Sonntag buchen: Hinweis „geschlossen“ erscheint, der Termin wird gespeichert und steht im Kalender

## Nicht in dieser Slice
–
