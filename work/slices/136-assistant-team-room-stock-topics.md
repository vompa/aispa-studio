# Slice 136 – Assistent: Personal, Räume und Lager

**Ziel:** Der Assistent beantwortet die Fragen der Rezeption zu Dienst und Pausen, Ersthelfern, freien Zeiten eines Raums und zum Lager aus den Daten.
**Runde:** R06
**Quelle:** R06 – Ä318
**Hängt ab von:** 135
**Modell:** sonnet
**Prüfstufe:** –
**IDs für neue Befunde:** Ä341–Ä343

## Umfang
- Personal: Pausen und Dienstende einer Person, Ersthelfer im Dienst heute, Zuständigkeiten (Daten aus Slice 117)
- Räume: freie Zeiten eines Raums an einem Tag, Raumsperren mit Grund
- Lager: Bestand eines Artikels je Lagerort, Artikel unter Mindestbestand, Verkaufspreis eines Artikels, offene Bestellungen mit Liefertermin
- Öffnungszeit und Feiertag in einer Antwort („haben wir am 8. dezember offen“)
- Stand in docs/konzepte/assistent-rezeption-fragen.md und die Hilfe nachziehen

## Muster übernehmen
- src/AispaStudio.Api/Assistant/Answers/TeamAnswers.cs, RoomAnswers.cs, OpeningHoursAnswers.cs, HolidayAnswers.cs
- die Antwortgeber aus Slice 135
- docs/konzepte/assistent-rezeption-fragen.md – Abschnitte Personal, Räume, Lager

## Voraussichtlich geänderte Dateien
- src/AispaStudio.Api/Assistant/
- tests/AispaStudio.Api.Tests/
- docs/konzepte/assistent-rezeption-fragen.md
- web/src/help/content/

## NICHT bauen
- Bestellen oder Umbuchen über den Assistenten
- Personaldaten über den Dienstplan hinaus

## Prompt
```
Lies CLAUDE.md, work/conventions.md, die Rundenakte work/rounds/R06-*.md und diese Slice-Datei.
Setze Slice 136 "Assistent: Personal, Räume und Lager" um (diese Datei).
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
- [ ] Given Sophie hat heute Dienst bis 18 Uhr mit Pause 13–13:30 When jemand „wann hat sophie pause“ fragt Then nennt die Antwort 13:00–13:30 und Dienstende 18:00
- [ ] Given Lena ist Ersthelferin mit gültigem Kurs und heute im Dienst When jemand „wer ist heute ersthelfer“ fragt Then nennt die Antwort Lena
- [ ] Given das Arganöl hat 2 Stück im Verkaufslager und Mindestbestand 5 When jemand „haben wir das arganöl noch“ fragt Then nennt die Antwort 2 Stück und den Hinweis unter Mindestbestand
- [ ] Given der Paarraum ist morgen ab 15 Uhr frei When jemand „wann ist der paarraum morgen frei“ fragt Then nennt die Antwort die freien Zeiten
- [ ] Manuell: Als Rezeption die Fragen der Abschnitte Personal, Räume und Lager aus docs/konzepte/assistent-rezeption-fragen.md stellen

## Nicht in dieser Slice
–
