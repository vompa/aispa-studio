# Dokumentation AISPA Studio

Hier liegt alles, was zur Definition des Produkts und zu den Entscheidungswegen gehört, damit man ohne den Projektchat weiterarbeiten kann. Einstieg für die Weiterarbeit: [uebergabe.md](uebergabe.md). Einstieg im [README des Repositorys](../README.md); Installation und Systemvoraussetzungen in [installation/](installation/README.md), Funktionen und Einstellungen in [features.md](features.md), Befehle für die Entwicklung in [development.md](development.md), Hinweise für Claude Code in `CLAUDE.md`.

## Wo was steht

| Datei | Inhalt |
| --- | --- |
| [uebergabe.md](uebergabe.md) | Einstieg: wo was liegt, lokal starten, Halbfertiges außerhalb von `main`, offene Arbeit in Reihenfolge, Fallen, Überlegungen, Arbeitsregeln des Auftraggebers |
| [arbeitsweise.md](arbeitsweise.md) | wie mit Branches, Pull Requests, CI und Merge gearbeitet wird: Ablauf, Prüfkette, PR-Text, PR-Protokoll, Modelle |
| [produkt/anforderungen.md](produkt/anforderungen.md) | Produktdefinition: Ziel, Zielgruppe, Anforderungen des Auftraggebers mit Zitaten, Leitlinien, Rollen, Abgrenzung |
| [produkt/systemanforderungen.md](produkt/systemanforderungen.md) | Messprotokoll der Systemanforderungen (Windows schlank und mit Zusatzpaketen); die Anforderungen selbst stehen im Hilfethema |
| [produkt/kalender-rezeption.md](produkt/kalender-rezeption.md) | Kalender aus Sicht der Rezeption: Wünsche, Ausblendbares, Tasten, Kennzeichen, KI-Anfrage, Kundenkartei, Konkurrenzvergleich, Umsetzungsschritte |
| [design.md](design.md) | Design-Brief (verbindlich): Token für Farben, Schrift, Abstände, Radien; jede Komponente mit festen Werten für Windows-App und Web; Seitenaufbau der Verwaltung; Umstellungsliste und Abnahme-Checkliste |
| [entscheidungen/](entscheidungen/README.md) | Entscheidungsprotokoll: 46 Entscheidungen im ADR-Stil mit Kontext, Optionen, Entscheidung, wer und wann, Begründung |
| [architektur.md](architektur.md) | Aufbau des Codes, Fachmodule, verbindliche Muster |
| [stand-und-offene-punkte.md](stand-und-offene-punkte.md) | was gebaut ist, was für den Echtbetrieb fehlt, offene Funktionen |
| [umsetzung/pr-protokoll.md](umsetzung/pr-protokoll.md) | Detailentscheidungen und offene Punkte jedes gemergten Pull Requests |
| [testdaten.md](testdaten.md) | Testdatensatz des Beispiel-Spas und Testkonten für jede Rolle |
| [uebergabe/threads/](uebergabe/threads/) | wörtliche Übergaben der sechs Threads vom 18.10.2026 (Kalender-Kern, Kassa, KI-Terminannahme, Online-Buchung, Windows-App, Einzelplatz), der Abschluss von „Windows-App ausbauen“ vom 20.10.2026 (`windows-app-2.md`) und der Patch-Entwurf des Einzelplatz-Threads |
| [bilder/](bilder/) | Bildschirmfotos aus dem Projekt je Bereich |

## Konzepte

Die Konzepte sind die Grundlage der Entscheidungen. Sie wurden am 17.10.2026 im Projekt erarbeitet und hier unverändert übernommen; Preise und Rechtsangaben haben den Stand Oktober 2026.

| Konzept | Worum es geht |
| --- | --- |
| [Technik-Stack Oberflächen](konzepte/technik-stack-oberflaechen.md) | Avalonia und React, Rollen je Oberfläche, Benutzerverwaltung |
| [Reporting-Konzept](konzepte/reporting.md) | Berichte je Rolle, Behandlungskarte, Pflichtberichte je Land |
| [Buchungsplattformen](konzepte/buchungsplattformen-anbindung.md) | Treatwell, Google, Gutscheinportale, Regeln gegen Doppelbuchungen |
| [Ergänzende Themen](konzepte/ergaenzende-themen.md) | DSGVO, AI Act, Sicherheit, Backup, Gutscheine, Migration, Barrierefreiheit u. a. mit Priorität |
| [Mehrsprachigkeit](konzepte/mehrsprachigkeit.md) | Entscheidungsvorlage für eine mehrsprachige Windows-App und Web: Bestand in Zahlen, drei Wege für die Ressourcen, Empfehlung, Aufwand in Slices (Runde R03, Slice 80) |
| [Mehrinstanz-Betrieb](konzepte/mehrinstanz-betrieb.md) | Instanzen im Cloud-Betrieb (Migration, API, Worker, Web, Redis) und Prüfliste für neue Hintergrunddienste, Zwischenspeicher und Endpunkte ([ADR 0087](entscheidungen/0087-mehrinstanz-betrieb.md)) |
| [Texte: Regeln für Hilfe, Oberfläche, ADRs, PR-Texte und Schnittstelle](gestaltung/texte.md) | 21 Regeln aus Google Developer Documentation Style Guide und Microsoft Writing Style Guide mit Beispielen aus dem Projekt, Prüfsatz fürs PR-Protokoll, Test über die Hilfetexte ([ADR 0077](entscheidungen/0077-textregeln-nach-google-und-microsoft.md)) |
| [Fluent 2 für die Windows-App](gestaltung/fluent2-ableitung.md) | Fluent 2 und WinUI vollständig gelesen und abgeleitet (21.10.2026): Regeln und Zahlen je Thema und Komponente, Feldverhalten (Text, Zahl, Betrag, Uhrzeit, Datum), Content design, Responsible AI, Abweichungen zum Design-Brief, Umsetzungsliste F1 bis F30 |

## Pflege

- Neue Entscheidung: nächste Nummer in `entscheidungen/` nach der [Vorlage](entscheidungen/vorlage.md), Zeile im Index ergänzen.
- Neuer Pull Request: Detailentscheidungen im PR-Text und als Abschnitt im PR-Protokoll; erledigte Punkte in `stand-und-offene-punkte.md` nachziehen.
- Konzepte bleiben als Stand vom 17.10.2026 erhalten. Ändert sich etwas, wird es als Entscheidung festgehalten, nicht im Konzept überschrieben.
