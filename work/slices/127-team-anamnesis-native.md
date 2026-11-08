# Slice 127 – Termin: Anamnese im Team nativ

**Ziel:** Was zur Anamnese nur im Web-Kalender liegt, wird nativ am Termin: Bogen lesen, Link fürs Tablet erzeugen, Papierbogen drucken, Bogen fotografieren, Auswertung prüfen und bestätigen.
**Runde:** R05
**Quelle:** R05 – Ä300
**Hängt ab von:** 126
**Modell:** opus
**IDs für neue Befunde:** Ä301–Ä303

## Umfang
- Am Termin: Stand wie bisher, dazu die vier Abläufe aus `AnamnesisSection` (web/src/calendar/AppointmentPanel.tsx) über die bestehenden API-Endpunkte
- Rechte genau wie heute im Web und in der API (Lesen mit `ViewCalendar`, Abläufe mit `Book`); die App blendet nur aus, der Server prüft; die Klärung des Leserechts macht Slice 128
- Foto: Auswertung ist ein Vorschlag, gespeichert wird erst nach Prüfen und Bestätigen durch das Team
- Keine Gesundheitsdaten in Log, Temp-Dateien oder Fehlermeldungen; ausgewähltes Foto und erzeugter Druck bleiben nur so lange wie nötig
- Masken-Inventar und Hilfe nachziehen

## Muster übernehmen
- `AnamnesisSection` in web/src/calendar/AppointmentPanel.tsx – Verhalten und Texte
- die Termin-Karten aus den Slices 124 und 126
- .claude/skills/fluent2-avalonia (responsible-ai.md für die Auswertung)

## Voraussichtlich geänderte Dateien
- src/AispaStudio.Desktop/Views/CalendarView.axaml
- src/AispaStudio.Desktop/ViewModels/
- docs/umsetzung/masken-inventar.md
- tests/AispaStudio.Desktop.Tests/
- web/src/help/content/

## NICHT bauen
- neue Regeln oder Rechte in der App; alles kommt aus der API
- Auswertung ohne Bestätigung durch das Team
- Entfernen der Web-Masken (macht Slice 103)

## Prompt
```
Lies CLAUDE.md, work/conventions.md, die Rundenakte work/rounds/R05-*.md und diese Slice-Datei.
Setze Slice 127 "Termin: Anamnese im Team nativ" um (diese Datei).
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
- [ ] Given eine Person mit Recht auf den Bogen und ein Termin mit ausgefülltem Bogen When sie ihn öffnet Then stehen dort die Antworten
- [ ] Given eine Therapeutin ohne das Recht `Book` When sie den Termin öffnet Then liest sie die Antworten, kann aber weder Tablet-Link, Papierbogen noch Foto auslösen
- [ ] Given ein Termin ohne Bogen When das Team den Link fürs Tablet erzeugt Then zeigt der Termin den Link der API
- [ ] Given ein Termin When das Team den Papierbogen druckt Then kommt der Bogen aus der API-Vorgabe
- [ ] Given ein Foto eines Bogens When die Auswertung vorliegt und das Team nicht bestätigt Then wird nichts gespeichert
- [ ] Given eine geprüfte Auswertung When das Team bestätigt Then zeigt der Termin den Stand „vorhanden“
- [ ] Manuell: Link am echten Tablet, Papierbogen am Drucker und Foto mit Prüfen und Bestätigen als Inhaberin im echten Fenster durchgehen

## Nicht in dieser Slice
–
