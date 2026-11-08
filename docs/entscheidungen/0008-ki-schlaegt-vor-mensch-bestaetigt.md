# 0008. KI in jeder Ansicht: sie schlägt vor, der Mensch bestätigt

- **Status:** angenommen
- **Datum:** 17.10.2026
- **Entschieden von:** Auftraggeber (Anforderung), Grundregel Claude, mit dem Entscheidungsdokument angenommen

## Kontext

Auftraggeber: „Es soll auch von Beginn an eine KI integriert sein und bei jedem Fall unterstützend eingreifen können.“ Der EU AI Act verlangt Kennzeichnung (Art. 50) und macht automatische Personalentscheidungen zur Hochrisiko-KI.

## Optionen

| Option | Bewertung |
| --- | --- |
| KI nur als separater Chat-Reiter | wird kaum genutzt |
| KI handelt vollautomatisch | Fehler bei Gesundheit oder Buchung schädigen Vertrauen, Rechtsrisiko |
| Ein Modellanbieter fest verdrahtet | Preise und Qualität ändern sich schnell |
| Eigenes Modell trainieren | teuer, kein Vorteil zum Start |
| Rollenbewusster Assistent, nur Vorschläge, Anbieter gekapselt | gewählt |

## Entscheidung

Ein KI-Assistent ist überall eingebaut und kennt die Rolle. Er ruft dieselben geprüften Funktionen auf wie die Oberfläche, mit den Rechten der angemeldeten Person. Pflichtzahlen (Kasse, Steuer) rechnet nie die KI. Jede KI-Nachricht nach außen trägt den Hinweis auf die KI. Dienstplan-KI nur als Vorschlag, keine Leistungsbewertung von Mitarbeitenden.

## Begründung

Eine KI, die man auf Deutsch alles zum eigenen Betrieb fragen kann, war in der Recherche (Oktober 2026) nicht zu finden. „Vorschlag statt Automatik“ hält das Produkt außerhalb der Hochrisiko-Pflichten.

## Folgen und Stand

Claude ist das Modell, sobald `Ai:Claude:ApiKey` gesetzt ist; ohne Schlüssel läuft eine regelbasierte Variante (auch in Tests). Gemeinsamer Aufruf in `ClaudeJson`. Assistent je Rolle in PR #18. Offen: AV-Vertrag mit Anthropic (DSGVO).

## Quellen

- [Ergänzende Themen, Abschnitt 2](../konzepte/ergaenzende-themen.md#2-ki-recht-und-ki-telefon)
