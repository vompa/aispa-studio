# 0097. Urlaubskonto: Arbeitstage je Kalenderjahr, Anspruch von der Leitung

- **Status:** angenommen, arbeitsrechtliche Punkte offen
- **Datum:** 16.11.2026
- **Entschieden von:** Claude, autonom nach ADR 0019 (Auftrag des Auftraggebers vom 16.11.2026: „Mach weiter mit dem Block Team“; Reihenfolge vom 20.10.2026: „Team (Zeiterfassung am Empfangsgerät, Urlaubskonto)“)

## Kontext

Urlaub steht als Abwesenheit im Dienstplan und geht als Urlaubstage in den Lohn-Export. Wie viel Urlaub eine Person hat und was davon übrig ist, wusste das Programm nicht; die Leitung führte das neben dem Programm. Beim Genehmigen eines Antrags fehlte damit die wichtigste Zahl.

Der gesetzliche Anspruch hängt von Land, Arbeitstagen je Woche, Dienstalter und Vertrag ab (Österreich: 30 Werktage, bei fünf Arbeitstagen 25 Arbeitstage, ab dem 26. Dienstjahr mehr; Deutschland: mindestens 24 Werktage, bei fünf Arbeitstagen 20). Das Urlaubsjahr ist in Österreich ohne Vereinbarung das Arbeitsjahr ab Eintritt, in der Praxis oft auf das Kalenderjahr umgestellt. Ein Eintrittsdatum oder einen Vertrag kennt das Programm nicht.

## Optionen

| Option | Bewertung |
| --- | --- |
| Anspruch aus Land und Arbeitstagen errechnen | braucht Eintrittsdatum, Dienstalter und Vertrag; ein falsch gerechneter Anspruch wäre schlimmer als keiner |
| Anspruch je Person und Jahr von der Leitung eintragen, Verbrauch aus dem Dienstplan zählen | gewählt: wenig Annahmen, die Zahl kommt von der Stelle, die den Vertrag kennt |
| Urlaubskonto in Stunden | genauer bei Teilzeit mit ungleichen Tagen, aber der Dienstplan kennt Urlaub nur je Tag, und der Lohn-Export zählt Tage |
| Jeden eingetragenen Urlaubstag zählen, auch am Wochenende | einfach, aber eine genehmigte Urlaubswoche hätte sieben Tage |

## Entscheidung

1. **Einheit und Jahr:** Das Urlaubskonto zählt Arbeitstage je Kalenderjahr.
2. **Anspruch und Übertrag:** Beide trägt die Leitung je Person und Jahr ein (`EmployeeLeaveEntitlement`), in ganzen oder halben Tagen. Das Programm nimmt keinen Anspruch an: Ohne Eintrag zeigt das Konto nur genommen und geplant, keinen Rest.
3. **Verbrauch:** Gezählt wird Urlaub aus dem veröffentlichten Dienstplan an den Wochentagen, an denen die Person laut Wochenarbeitszeit arbeitet; ohne hinterlegte Arbeitszeit Montag bis Freitag. Ein Urlaubstag mit Dienst zählt halb. Tage bis heute sind genommen, spätere geplant. Entwürfe zählen nicht.
4. **Rest:** Anspruch plus Übertrag, abzüglich genommen und geplant. Ein negativer Rest wird gezeigt und hervorgehoben, aber nichts wird gesperrt.
5. **Übertrag ins nächste Jahr:** Kein Automatismus. Die Leitung trägt den Übertrag ein; Verfall und Verjährung entscheidet sie.
6. **Rechte:** Lesen darf, wer Standorte verwaltet oder Personalunterlagen liest; ändern nur, wer Standorte verwaltet. Die Mitarbeiter-App sieht das Konto vorerst nicht.
7. **Ort:** Auf der Karte der Person im Reiter „Dienstplan und Abwesenheit“, unter der Woche. Kein eigener Reiter.

## Begründung

- Die Zahl, die rechtlich zählt, kommt von der Leitung; das Programm rechnet nur, was es sicher weiß.
- Arbeitstage passen zum Dienstplan und zu dem, was die Leitung im Kopf hat („25 Tage“).
- Anträge schreiben jeden Kalendertag des Zeitraums in den Dienstplan. Die Zählung nach Arbeitstagen macht daraus die richtige Zahl, ohne dass Anträge anders geschrieben werden müssen.

## Folgen und Stand

- Gebaut: `Roster/LeaveAccount.cs` (Tabelle, Zählung, `GET` und `PUT /api/employees/{id}/leave-account/{jahr}`), Migration `EmployeeLeaveEntitlement`, `LeaveAccountViewModel` und `LeaveAccountView` in der Windows-App, Hilfe „Urlaubskonto einer Person“. Tests in `LeaveAccountTests` (API und Windows-App) und ein Ansichtstest.
- Der Lohn-Export zählt Urlaub weiter je eingetragenem Tag, auch am Wochenende. Das ist ein bekannter Unterschied und steht in der Hilfe; ob der Export auf Arbeitstage umgestellt wird, ist offen.
- Wechselt die Wochenarbeitszeit im Jahr, zählt das Konto das ganze Jahr nach dem aktuellen Stand. Eine Wochenarbeitszeit mit Gültigkeitsdatum gibt es nicht.
- Feiertage zieht das Konto nicht ab: Liegt Urlaub auf einem Feiertag, der ein Arbeitstag der Person wäre, zählt er. Das Programm führt keinen Feiertagskalender.
- Änderungen an Anspruch und Übertrag speichern Zeitpunkt und Benutzer an der Zeile; im Verlauf der Karte erscheinen sie noch nicht.
- Seit 16.11.2026 sieht jede Person ihr eigenes Konto in „Mein Tag“ im Reiter „Abwesenheit“ (`GET /api/absence-requests/mine/leave-account/{jahr}`, nur lesen). Punkt 6 der Entscheidung ist damit erweitert; ändern kann weiter nur die Leitung.
- Offen, später: Rest beim Entscheiden eines Antrags anzeigen; Übersicht über alle Personen; Urlaubsjahr ab Eintritt.
- **Arbeitsrecht, vom Auftraggeber oder der Lohnverrechnung zu bestätigen:** Kalenderjahr als Urlaubsjahr (in Österreich braucht die Umstellung eine Vereinbarung); Umgang mit Feiertagen und mit Krankheit im Urlaub; ob halbe Urlaubstage im Betrieb vorkommen.

## Quellen

- [§ 2 Urlaubsgesetz (Österreich)](https://www.ris.bka.gv.at/GeltendeFassung.wxe?Abfrage=Bundesnormen&Gesetzesnummer=10008376)
- [§ 3 Bundesurlaubsgesetz (Deutschland)](https://www.gesetze-im-internet.de/burlg/__3.html)
- [ADR 0031](0031-zeiterfassung-abschnitte-mit-protokoll.md), `docs/umsetzung/pr-protokoll.md`
