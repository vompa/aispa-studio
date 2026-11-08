# Slice 111 – Team: Hinweise, Sonderregelungen und Vollmachten auf einen Blick

**Ziel:** Oben auf der Personenkarte stehen Kennzeichen für Hinweise, Sonderregelungen und Vollmachten; der Reiter „Regelungen und Vollmachten“ pflegt sie mit Gültig-von und Gültig-bis. Sonderregelungen warnen im Dienstplan, Vollmachten zeigen, wer etwa an der Kassa stornieren oder den Tresor öffnen darf.
**Runde:** R05
**Quelle:** R05 – Ä252
**Hängt ab von:** 98
**Modell:** opus
**IDs für neue Befunde:** Ä256–Ä258

## Umfang
- Hinweise: kurzer Text, Sichtbarkeit (nur Leitung oder ganzes Team), optional als Symbol mit Tooltip im Dienstplan und in der Spaltenüberschrift des Kalenders
- Sonderregelungen: Art (keine Termine nach Uhrzeit, höchstens Stunden pro Tag, feste freie Wochentage, Schutzfrist), Werte, Gültig-von/-bis, Grund nur für die Leitung; die API prüft Dienstplan-Einträge und Buchungen im Kalender dagegen und liefert eine Warnung mit „Trotzdem“, nie eine Sperre (E10)
- Vollmachten aus fester Liste (Kassa-Storno, Kassa-Abschluss, Tresor, Bankeinzahlung, Schlüssel, Lieferungen annehmen, Bestellungen auslösen, Vertretung der Leitung) mit Gültig-von/-bis und „erteilt von“; Übersicht „Wer darf was?“ im Bereich Team
- Kassa-Storno und Bestellungen auslösen prüft die API zusätzlich zur Rolle; Tresor, Kassa-Abschluss, Bank, Schlüssel, Lieferungen und Vertretung sind nur Anzeige (E11)
- Kennzeichen oben auf der Karte und in der Teamliste; abgelaufene Einträge ausgegraut
- Verlauf aus Slice 91, Hilfe nachziehen

## Muster übernehmen
- Personenkarte im Bereich Team aus Slice 98
- docs/gestaltung/meldungen.md – Warnung mit „Trotzdem“
- Hover-Karten aus Slice 89 für Tooltips der Kennzeichen

## Voraussichtlich geänderte Dateien
- src/AispaStudio.Api/Staff/
- src/AispaStudio.Api/Scheduling/
- src/AispaStudio.Desktop/Views/Admin/Team*
- src/AispaStudio.Desktop/ViewModels/Admin/Team*
- tests/AispaStudio.Api.Tests/
- tests/AispaStudio.Desktop.Tests/
- web/src/help/content/

## NICHT bauen
- neue Rollen
- Ersatz des Rechtesystems durch Vollmachten
- automatisches Umplanen bei Verstößen

## Prompt
```
Lies CLAUDE.md, work/conventions.md, die Rundenakte work/rounds/R05-*.md und diese Slice-Datei.
Setze Slice 111 "Team: Hinweise, Sonderregelungen und Vollmachten auf einen Blick" um (diese Datei).
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
- [ ] Given Lena hat die Sonderregelung „keine Termine nach 16 Uhr“ bis 31.12. When die Managerin ihr am Dienstag Dienst bis 18 Uhr einträgt Then warnt die Maske mit der Regelung und speichert erst nach Bestätigung
- [ ] Given Sophie hat die Vollmacht „Kassa-Storno“ bis gestern When sie heute storniert Then lehnt die API ab und nennt die abgelaufene Vollmacht
- [ ] Given die Übersicht „Wer darf was?“ When die Inhaberin sie öffnet Then steht je Vollmacht, wer sie hat und bis wann
- [ ] Given ein Hinweis mit Sichtbarkeit Team When der Kalender Lenas Spalte zeigt Then steht ein Symbol mit dem Hinweis im Tooltip
- [ ] Given Lena hat die Sonderregelung „keine Termine nach 16 Uhr“ When die Rezeption ihr einen Termin um 17 Uhr bucht Then warnt der Kalender mit der Regelung und bucht erst nach „Trotzdem buchen“
- [ ] Manuell: Vollmacht vergeben, Ablaufdatum auf morgen setzen, am nächsten Tag Storno versuchen

## Nicht in dieser Slice
–
