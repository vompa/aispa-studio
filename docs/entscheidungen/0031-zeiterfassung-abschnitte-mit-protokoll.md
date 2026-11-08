# 0031. Zeiterfassung in Abschnitten, Korrekturen nur mit Grund und Protokoll

- **Status:** angenommen
- **Datum:** 18.10.2026
- **Entschieden von:** Claude (autonom, siehe ADR 0019)

## Kontext

Arbeitgeber müssen Arbeitszeiten aufzeichnen: in Österreich nach § 26 AZG, in Deutschland nach dem Urteil des BAG vom 13.09.2022 (1 ABR 22/21), in der Schweiz nach Art. 46 ArG mit Art. 73 ArGV 1. Bisher kannte AISPA Studio nur Soll-Zeiten aus dem Dienstplan, und Lohnexport und Hinweise beruhten darauf. Die Grenzwerte waren fest auf Österreich und Deutschland eingestellt.

## Optionen

| Option | Bewertung |
| --- | --- |
| Tageszeile mit Beginn, Ende, Pausendauer | einfach, aber Pausen sind nicht nachweisbar und nicht stempelbar |
| Abschnitte (Kommen bis Pause, Weiter bis Gehen); Pausen sind die Lücken | gewählt |
| Freie Ereignisliste (Kommen, Pause, Gehen als einzelne Stempel) | flexibel, aber fehlerhafte Folgen (zweimal Kommen) müssen ständig repariert werden |

## Entscheidung

- Jede Person mit verknüpftem Teamkonto stempelt selbst: Kommen bzw. Weiter öffnet einen Abschnitt, Pause bzw. Gehen schließt ihn. Je Person läuft höchstens ein Abschnitt; das sichert ein Index in der Datenbank.
- Nachtragen, Ändern und Entfernen darf nur die Standortleitung (Recht `ManageLocation`), immer mit Grund. Jeder Schritt steht mit altem und neuem Stand, Person und Zeitpunkt im Änderungsprotokoll. Entfernte Abschnitte bleiben gespeichert.
- Mitarbeitende sehen nur ihre eigene Zeit. Die Zeiten der Kolleginnen sieht nur die Standortleitung.
- Die Grenzwerte gelten je Land des Unternehmens (`WorkingTimeRules`): AT und DE 10 Stunden am Tag, CH 14 Stunden Tagesrahmen; 11 Stunden Ruhe; 48 Stunden pro Woche (CH 50); Pausen nach § 11 AZG, § 4 ArbZG bzw. Art. 15 ArG.
- Pausen prüft nur die Zeiterfassung. Im Dienstplan stehen Dienste meist ohne die Pause darin.
- Der Lohnexport nimmt Ist-Zeiten, wo an einem Tag gestempelt wurde, und sonst den Plan. Er nennt die Zahl der Diensttage ohne Stempel.

## Begründung

Abschnitte lassen sich mit zwei Knöpfen bedienen, ergeben Pausen ohne Zusatzeingabe und lassen sich in jedem Land gleich prüfen. Ein Protokoll statt Überschreiben macht die Aufzeichnung als Nachweis gegenüber Arbeitsinspektorat, Zoll und Lohnverrechnung brauchbar.

## Folgen und Stand

- Code in `src/AispaStudio.Api/TimeTracking/`, Regeln in `Roster/WorkingTime.cs`. Die Oberfläche hat die Stempeluhr im Kopf und die Seite „Zeiten“.
- Offen:
  - Stempeln am Empfangstablet per PIN.
  - Eigene Korrekturwünsche der Mitarbeitenden mit Freigabe.
  - Monatsbestätigung durch die Mitarbeitenden.
  - Kollektivvertragliche Abweichungen je Unternehmen.
  - Zeiterfassung in der Windows-App.
- Mustervorlage für die Betriebsvereinbarung: siehe [Ergänzende Themen](../konzepte/ergaenzende-themen.md). Zeiterfassung ist eine Kontrolleinrichtung nach § 96 ArbVG bzw. § 87 BetrVG.
- **Nachtrag (05.11.2026, T-015):** Keine Zeiterfassung per Handy (E6, Auftraggeber). Die Stempeluhr in „Mein Tag“ ist entfernt, und die Mitarbeiter-App erreicht `/api/time/me` nicht mehr. Die Endpunkte bleiben für den Arbeitsplatz-Kanal bestehen, ohne Oberfläche; Arbeitszeiten trägt die Leitung in „Zeiten“ ein. Stempeln in der Windows-App (M2 im Vergleich Team, Berichte, Lager) wird nicht gebaut.

## Quellen

- § 26 AZG, § 11 AZG, ARG; §§ 3–5 ArbZG, BAG 1 ABR 22/21; Art. 9, 15, 15a, 46 ArG
- [ADR 0025](0025-dienstplan-hinweise-statt-sperren.md)
