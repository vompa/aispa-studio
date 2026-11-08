# Slice 110 – Team: Personalakte mit Gesprächen, Anmerkungen und Verwarnungen

**Ziel:** Die Personenkarte bekommt den Reiter „Personalakte“: Mitarbeitergespräche mit Teilnehmenden, Notizen, Vereinbarungen und Folgetermin, Anmerkungen der Leitung und Verwarnungen mit Grund, Datum und Ablaufdatum; nur Inhaberin und Managerin sehen und ändern sie, die Person selbst kann ihre Einträge auf Anfrage erhalten.
**Runde:** R05
**Quelle:** R05 – Ä252
**Hängt ab von:** 98
**Modell:** opus
**IDs für neue Befunde:** Ä253–Ä255

## Umfang
- API: Einträge der Personalakte (Art Gespräch, Anmerkung, Verwarnung) mit eigenem Recht, getrennt von den Stammdaten; Inhalte verschlüsselt im eigenen Schema `hr` wie die Gesundheitsdaten (E8)
- Mitarbeitergespräch: Datum, Art (Jahresgespräch, Feedback, Rückkehr, Anlass), Teilnehmende, Notizen, Vereinbarungen als Liste mit Erledigt, Folgetermin; der Folgetermin erscheint als Erinnerung bei der Leitung (Erinnerungslogik in der API)
- Verwarnung: Grund, Datum, wer sie ausgesprochen hat, ob schriftlich übergeben, Ablaufdatum frei wählbar mit Vorgabe 2 Jahre (E9); nach dem Ablauf löscht ein Hintergrundjob sie; Hinweis in der Hilfe, die Frist arbeitsrechtlich abzuklären
- Export der Einträge einer Person als PDF für die Auskunft nach Art. 15 DSGVO
- Verlauf zeigt nur „geändert von … am …“ ohne Inhalt (wie Gesundheitsdaten)
- Hilfe nachziehen

## Muster übernehmen
- Personenkarte im Bereich Team aus Slice 98
- Gesundheitsdaten im Schema `health` – Verschlüsselung und Verlauf ohne Inhalt
- Erinnerung nach Datum aus ADR 0083 (Vormerkung)

## Voraussichtlich geänderte Dateien
- src/AispaStudio.Api/Staff/
- src/AispaStudio.Desktop/Views/Admin/Team*
- src/AispaStudio.Desktop/ViewModels/Admin/Team*
- tests/AispaStudio.Api.Tests/
- tests/AispaStudio.Desktop.Tests/
- web/src/help/content/

## NICHT bauen
- Arbeitsrechtliche Bewertung oder KI-Textvorschläge für Verwarnungen
- Einsicht der betroffenen Person in der Handy-App (nur Export auf Anfrage)
- Gehaltsdaten (Reiter Lohn, Slice 100)

## Prompt
```
Lies CLAUDE.md, work/conventions.md, die Rundenakte work/rounds/R05-*.md und diese Slice-Datei.
Setze Slice 110 "Team: Personalakte mit Gesprächen, Anmerkungen und Verwarnungen" um (diese Datei).
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
- [ ] Given eine Rezeptionistin When sie die Personalakte einer Kollegin abruft Then antwortet die API 403 und der Reiter fehlt
- [ ] Given ein Jahresgespräch mit Folgetermin 15.03. When der 15.03. kommt Then hat die Inhaberin eine Erinnerung „Folgegespräch mit Lena“
- [ ] Given eine Verwarnung mit Ablaufdatum gestern When der Hintergrundjob läuft Then ist sie gelöscht und der Verlauf nennt nur die Löschung
- [ ] Given eine Anmerkung zu Lena When der Verlauf geöffnet wird Then steht „geändert von … am …“ ohne Inhalt
- [ ] Manuell: Gespräch mit zwei Vereinbarungen anlegen, eine abhaken, PDF-Export öffnen

## Nicht in dieser Slice
–
