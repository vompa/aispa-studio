# Slice 91 – Änderungsverlauf für Kunden, Behandlungen, Dienstplan und Rechte

**Ziel:** Kundendaten, Behandlungen und Preise, Dienstplan und Abwesenheiten sowie Benutzer und Rechte zeigen ihren Verlauf wie Termine; Dienstplan, Benutzer und Rechte sieht nur die Leitung.
**Runde:** R04
**Quelle:** R04 – Ä184
**Hängt ab von:** 90
**Modell:** sonnet
**IDs für neue Befunde:** Ä188–Ä190

## Umfang
- Verlauf-Endpunkte für Kunde, Behandlung, Dienstplan/Abwesenheit, Benutzer/Rolle mit den Rechten nach Ruling (Kunde und Behandlung wie der Datensatz, Dienstplan, Benutzer und Rechte nur Inhaberin und Manager)
- Abschnitt „Verlauf“ in den jeweiligen Masken der Windows-App, gleiches Muster wie im Termin-Panel
- Gesundheitsdaten des Gastes: nur „geändert von … am …“
- Hilfe zu den Masken ergänzt

## Muster übernehmen
- Abschnitt „Verlauf“ im Termin-Panel aus Slice 90

## Voraussichtlich geänderte Dateien
- src/AispaStudio.Api/
- src/AispaStudio.Desktop/Views/
- web/src/help/content/

## NICHT bauen
- Verlauf für einzelne Einstellungen (Farben, Layout, Anzeige)
- Wiederherstellen alter Stände

## Prompt
```
Lies CLAUDE.md, work/conventions.md, die Rundenakte work/rounds/R04-*.md und diese Slice-Datei.
Setze Slice 91 "Änderungsverlauf für Kunden, Behandlungen, Dienstplan und Rechte" um (diese Datei).
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
- [ ] Given die Rezeption ändert die Telefonnummer eines Gastes When der Verlauf der Kundendaten geöffnet wird Then steht die Änderung mit altem und neuem Wert, Person und Uhrzeit
- [ ] Given eine Rezeptionistin When sie den Verlauf von Benutzern und Rechten abruft Then wird er verweigert (403)
- [ ] Given die Anamnese eines Gastes wird geändert When der Verlauf geöffnet wird Then steht nur „geändert von … am …“ ohne Inhalt
- [ ] Manuell: Preis einer Behandlung ändern: Verlauf zeigt alten und neuen Preis mit Person und Uhrzeit

## Nicht in dieser Slice
–
