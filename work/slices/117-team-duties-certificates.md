# Slice 117 – Team: Zuständigkeiten und Zertifikate mit Erneuerung

**Ziel:** Die Personenkarte zeigt Zuständigkeiten (Ersthelfer, Sicherheitsvertrauensperson, Lager, Brandschutz, Hygiene …) und Zertifikate mit Gültig-bis als Kennzeichen; eine Übersicht „Wer ist wofür zuständig?“ und „Was läuft bald ab?“ steht im Bereich Team, und vor dem Ablauf eines Zertifikats bekommen Leitung und Person eine Erinnerung.
**Runde:** R05
**Quelle:** R05 – Ä274
**Hängt ab von:** 98
**Modell:** opus
**IDs für neue Befunde:** Ä275–Ä277

## Umfang
- Zuständigkeiten aus einer pflegbaren Liste (Vorgaben: Ersthelfer, Sicherheitsvertrauensperson, Lager, Brandschutz, Hygiene, Kassa-Verantwortung, Ausbildung) mit Gültig-von/-bis und Vertretung
- Zertifikate je Person: Bezeichnung, ausgestellt am, gültig bis, ausstellende Stelle, Scan als Anhang (verschlüsselt wie die Personalakte), Erinnerungsfrist (Vorgabe 60 Tage)
- Eine Zuständigkeit kann ein Zertifikat verlangen (Ersthelfer → Erste-Hilfe-Kurs); fehlt es oder läuft es ab, zeigt die Karte eine Warnung
- Erinnerung vor dem Ablauf an Leitung und Person (Handy-App, wenn vorhanden), Logik in der API wie die Erinnerungen aus ADR 0083
- Übersichten im Bereich Team: „Wer ist wofür zuständig?“ und „Was läuft bald ab?“
- Lagerwarnungen und Bestellvorschläge (Slices 112, 113) gehen an die Person mit der Zuständigkeit Lager, sonst an die Leitung
- Dienstplan warnt mit „Trotzdem“, wenn in einer Schicht weniger Ersthelferinnen mit gültigem Kurs im Dienst sind als die Inhaberin festlegt (Vorgabe 1, E14); das Programm prüft keine Rechtsvorschrift
- Hilfe nachziehen

## Muster übernehmen
- Personenkarte im Bereich Team aus Slice 98
- Kennzeichen oben auf der Karte aus Slice 111
- Erinnerung nach Datum aus ADR 0083

## Voraussichtlich geänderte Dateien
- src/AispaStudio.Api/Staff/
- src/AispaStudio.Desktop/Views/Admin/Team*
- src/AispaStudio.Desktop/ViewModels/Admin/Team*
- tests/AispaStudio.Api.Tests/
- tests/AispaStudio.Desktop.Tests/
- web/src/help/content/

## NICHT bauen
- Buchung von Kursen
- Rechtliche Prüfung, wie viele Ersthelfer nötig sind
- Anzeige der Zuständigen am Dashboard (E5)

## Prompt
```
Lies CLAUDE.md, work/conventions.md, die Rundenakte work/rounds/R05-*.md und diese Slice-Datei.
Setze Slice 117 "Team: Zuständigkeiten und Zertifikate mit Erneuerung" um (diese Datei).
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
- [ ] Given Lenas Erste-Hilfe-Kurs gilt bis 31.12. und die Frist ist 60 Tage When der 1.11. kommt Then haben Inhaberin und Lena eine Erinnerung „Erste-Hilfe-Kurs läuft am 31.12. ab“
- [ ] Given Lena ist Ersthelferin und ihr Kurs ist abgelaufen When die Inhaberin die Teamliste öffnet Then steht bei Lena eine Warnung am Kennzeichen Ersthelfer
- [ ] Given Sophie hat die Zuständigkeit Lager When ein Artikel unter die Mindestmenge fällt Then geht die Aufgabe an Sophie
- [ ] Given die Übersicht „Wer ist wofür zuständig?“ When sie geöffnet wird Then steht je Zuständigkeit Person, Vertretung und Gültigkeit
- [ ] Given am Samstag ist keine Ersthelferin mit gültigem Kurs im Dienst When die Managerin den Dienstplan speichert Then warnt die Maske „Keine Ersthelferin im Dienst“
- [ ] Manuell: Zertifikat mit Ablauf in 30 Tagen anlegen: Erinnerung erscheint, Kennzeichen zeigt die Frist

## Nicht in dieser Slice
–
