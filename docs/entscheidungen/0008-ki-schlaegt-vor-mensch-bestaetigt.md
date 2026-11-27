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

- Nachtrag (26.11.2026, Entscheidung des Auftraggebers): Im Management-Bericht gehen an die KI (Kurzkommentar und Auswertung per Knopf) das Personal mit dem **Namen im Haus**, ersatzweise mit dem Namen der Person, und die Zahlen je Person. Nie geht der volle bürgerliche Name an die KI, wenn ein Name im Haus eingetragen ist; ein Test hält das fest. **Gästenamen und Gesundheitsdaten gehen nie** an die KI, auch nicht aus den Gästelisten des Berichts. Die Grenze „keine Leistungsbewertung von Mitarbeitenden“ gilt weiter: Der Prompt erlaubt Zahlen je Person, aber keine Bewertung einzelner Personen und keine arbeitsrechtlichen Schritte gegen sie; das ist vorläufig so umgesetzt und wartet auf die Bestätigung des Auftraggebers. Offen bleibt der Vertrag zur Auftragsverarbeitung mit Anthropic: Die Fakten enthalten jetzt Namen im Haus von Mitarbeitenden, er sollte vor dem ersten Kunden mit Schlüssel stehen.

## Quellen

- [Ergänzende Themen, Abschnitt 2](../konzepte/ergaenzende-themen.md#2-ki-recht-und-ki-telefon)
