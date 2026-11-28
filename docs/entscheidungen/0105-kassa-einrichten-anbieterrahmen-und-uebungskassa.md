# 0105. Kassa einrichten: Anbieterrahmen und Übungskassa

- **Status:** angenommen
- **Datum:** 27.11.2026
- **Entschieden von:** Auftraggeber (Übungskassa in drei Ländern, eigener Assistent und eigene Karte); Ausführung Codex im beauftragten Automodus

## Kontext

Der [Kassa-Befund](../umsetzung/kassa-assistent-befund.md) beschreibt die fehlende Einrichtung in der Windows-App. Die API kann Kassen anlegen, die App bietet dafür bisher keinen Weg. Beauftragt sind die Empfehlungen 1 bis 3: Anbieterrahmen, Übungskassa sowie FinanzOnline-Daten und Jahresbeleg. Echte Signaturanbieter und deren Zugänge gehören nicht zu diesem Lauf.

## Optionen

| Option | Bewertung |
| --- | --- |
| Kassa als weiteren Schritt in „Betrieb einrichten“ belassen | Vermischt die allgemeine Einrichtung mit den länderspezifischen Schritten. |
| Eigene Karte und eigener Assistent „Kassa einrichten“ | Gewählt; der Betrieb ist nach fünf Schritten eingerichtet, die Kassa hat einen eigenen Bereitschaftsstand. |

## Entscheidung

Die Verwaltung erhält neben „Kassa“ für Rabattgrenzen die neue Maske „Kassen“. Das Recht `ManageLocation` schützt Anlegen, Außerbetriebnahme, Zugangsdaten, Registrierung und Bestätigungen. Der Anbieterkatalog ist mit `Book` lesbar. Der Server prüft bei einer einzelnen Kassa deren tatsächlichen Standort und Mandanten; ein mitgeschickter Standort ersetzt diese Prüfung nicht.

Ein Katalog liefert Anbieter je Land mit Kennung, Art, Übungskennzeichen, Zugangsfeldern und Hilfethema. In diesem Lauf gibt es AT `test`, DE `test` und CH `none`. Signatur, Erreichbarkeitsprüfung, Zertifikat und TSE-Export wählen denselben Anbieter je Kassa. Für spätere zugangspflichtige Adapter müssen die geschützten Zugangsdaten bereits vor Initialisierung und Startbeleg verfügbar sein.

Eine ausdrücklich bestätigte Übungskassa ist in Österreich, Deutschland und der Schweiz möglich, auch im Einzelplatz. Ein Testanbieter erzwingt den Übungsstatus; in der Schweiz wählt die Einrichtung ihn ausdrücklich. Bestehende österreichische und deutsche Kassen werden als Übungskassen gekennzeichnet, weil die bisherigen Adapter Testeinrichtungen sind. Die Einstellung `Fiscal:TestTse` schaltet die Verfügbarkeit des deutschen Testadapters künftig nicht mehr ab.

Der Übungsstatus ist unveränderlich. Für den Echtbetrieb wird die Übungskassa außer Betrieb genommen und eine neue Kassa mit neuer Kennung und eigener Belegkette angelegt. Übungsbelege tragen dauerhaft „ÜBUNGSKASSA – kein gültiger Beleg“: in der Maske, im PDF, auf dem Bon, in der nativen Belegansicht und bei lokalen Ersatzbelegen einschließlich Zweitschrift. Die Übungsumsätze bleiben in Berichten, Abschlüssen und Exporten enthalten und werden dort gekennzeichnet. Signierte Originaldaten und vorgeschriebene Exportformate werden nicht umgeschrieben; nötigenfalls steht der Hinweis in einer Begleitinformation. Das führt [ADR 0010](0010-kassa-compliance-by-design.md) fort.

Die Karte „Kassa einrichten“ unterscheidet:

- **offen:** keine an dieser Stelle verwendbare Kassa; die Antwort nennt den Grund auch für eine vorhandene, aber nicht bereite echte Kassa;
- **Übungsbetrieb:** eine verwendbare Übungskassa, aber keine bereite echte Kassa;
- **bereit:** mindestens eine aktive echte Kassa an dieser Stelle kann Belege bilden; Startbeleg und erreichbare Einrichtung sind vorhanden.

Eine gespiegelte Kassa einer anderen Stelle macht diese Installation nicht bereit. AT und DE erreichen den Echtbetrieb erst mit einem später angebundenen echten Anbieter; CH braucht keine Signatur.

Zugangsdaten werden mit ASP.NET Core Data Protection, Zweck `Pos.Fiscal.Credentials`, Mandant und Kassa geschützt. Antworten liefern ausschließlich den maskierten Stand. FinanzOnline-Schlüssel werden nur über den gesonderten berechtigten Online-Abruf angezeigt; weder Kartenantworten noch der Desktop-Offlinespeicher enthalten sie.

Die österreichische Registrierungsansicht zeigt Kassa-ID, AES-Schlüssel, Prüfwert, Zertifikatsseriennummer und Anbieter. Übungskassen erhalten den ausdrücklichen Hinweis, diese Daten nicht für eine Registrierung zu verwenden. Bestätigungen für Registrierung, Startbeleg, Jahresbeleg und die deutsche Mitteilung speichern Datum und handelnde Person. Die Bestätigung eines Jahresbelegs bezieht sich zusätzlich auf den konkreten gültigen Abschlussbeleg und dessen Abschlussjahr.

Der österreichische Jahresbeleg kann ab 15. Dezember manuell erstellt werden. Bei laufender API prüft ein Hintergrunddienst am 31. Dezember ab 23:30 Uhr Ortszeit. Der erste folgende Beleg bleibt ein weiterer Auslöser für einen fehlenden Abschluss. Alle Wege entscheiden innerhalb derselben Kassensperre. Sie unterscheiden das Abschlussjahr vom Ausstellungszeitpunkt, erzeugen bei unverändertem Kettenstand keinen doppelten Abschluss und lassen nach einem weiteren Dezemberumsatz einen erneuten Abschluss und dessen Prüfung offen. Eine ausgefallene Signatur zählt nicht als erfolgreich geprüfter Abschluss. Hinweise beginnen am 15. Dezember und erinnern anschließend an die Prüfung bis 15. Februar des Folgejahres. Der Zeitplan kann bei geschlossener Einzelplatz-App nicht laufen.

Anleitungen werden als Hilfethemen mit amtlichen Links angeboten; die App baut keine FinanzOnline- oder ELSTER-Formulare nach.

## Begründung

Der eigene Ablauf macht den Unterschied zwischen Üben und Echtbetrieb sichtbar. Die vorhandenen Länderadapter, Kassentransaktionen, Mandantentrennung und nativen Assistenten werden erweitert. Ein weiterer Anbieter soll keinen eigenen Einrichtungsablauf benötigen. Bestehende API-Aufrufe und Belegketten bleiben bei der Erweiterung ausdrücklich Gegenstand der Abnahme.

## Folgen und Stand

Die Umsetzung erfolgt in T-138 bis T-144: API-Grundlage, Zugangsdaten, FinanzOnline und Jahresbeleg, Kassenmaske, Assistent und Karte, Integration sowie Handbuch und Bilder. Der Kassa-Schritt der neuen allgemeinen Einrichtung wandert in die eigene Karte; der Verwaltungsweg des Standortschritts bleibt erhalten. Die genaue rückwärtskompatible Antwortform wird im Umsetzungsprotokoll festgehalten.

Der bestehende Endpunkt `GET /api/setup/progress` behält standardmäßig sechs Schritte. Die neue Windows-App fragt mit `includeRegister=false` die fünf allgemeinen Schritte ab; der Abschluss wird über genau diese fünf berechnet. Für alte deutsche Anlageanfragen ohne Anbieter- und Übungsauswahl gilt weiterhin der effektive Wert von `Fiscal:TestTse`. Die bloße Registrierung des Testadapters erweitert diese Zulassung nicht. Eine ausdrückliche Auswahl `FiscalProvider=test` oder `Practice=true` ermöglicht den neuen Übungsweg; widersprüchliche Angaben werden abgelehnt.

Automatische Online-Vorgänge bevorzugen eine verwendbare echte Kassa und behalten einen geeigneten, deterministischen Rückfall auf eine Übungskassa. Eine bereits bezahlte Buchung darf dabei weder verschwinden noch bei einer Wiederholung einer zweiten Kassa zugeordnet werden. Die bestehende Prüfung vor Belastung einer Kartengarantie bleibt bestehen. Eine neue Pflicht zu einer echten Kassa vor Annahme von Online-Zahlungen ist nicht Teil dieses Laufs; sie müsste Zahlungsbeginn, Belastung, Rückmeldungen und Wiederholungen gemeinsam regeln. Diese drei Ergänzungen wurden im beauftragten Automodus durch fünf unabhängige Council-Berater, anonyme Gegenprüfungen und einen Chairman entschieden.

Die neuen Funktionen setzen einen bestätigten gemeinsamen Upgrade-Stand aller gekoppelten Stellen voraus. Alte Empfänger haben für deutsche Test-TSE-Kennungen nur 30 Zeichen und weisen längere Kennungen mit `22001` zurück. Die vereinbarten 50 Zeichen bleiben erhalten. Vor der Umstellung werden alle Schreiber und Abgleiche angehalten; auch Offline-PCs werden vor dem Wiederanlauf aktualisiert. Unbekannte Stellen oder unbestätigter Stillstand halten den Rollout an. Die [Upgrade-Checkliste](../installation/cash-register-upgrade.md) beschreibt den Nachweis. Diese Betriebsgrenze wurde durch einen weiteren vollständigen Council im beauftragten Automodus entschieden; eine automatische Versionssperre im Abgleich ist damit nicht behauptet.

Noch nicht umgesetzt: echte Anbieter, automatischer FinanzOnline-Webservice, ELSTER-Mitteilung, Cloud-Kartenterminals und Empfehlungen 4 bis 12 des Befunds. Druck auf einem echten Bondrucker und Eingaben bei FinanzOnline bleiben ausdrücklich Handtests. Dieser ADR behauptet keine Freigabe für den steuerlichen Echtbetrieb.

## Quellen

- [ADR 0085: additive API-Verträge](0085-api-version-in-den-pfaden.md), [ADR 0103: geschützte Schlüssel](0103-claude-schluessel-je-betrieb-in-der-app.md), [ADR 0104: Ersteinrichtung](0104-gefuehrte-ersteinrichtung.md).
- [BMF: Detailfragen zur RKSV, Abschnitt 6 zum AES-Prüfwert](https://www.bmf.gv.at/dam/jcr%3A66a8edac-3dbf-4e67-b768-634ce2e5bb1e/20160905-Detailfragen-RKSV-V1.2.pdf), abgerufen am 27.11.2026.
- [BMF: Erlass zur Einzelaufzeichnungs-, Registrierkassen- und Belegerteilungspflicht](https://findok.bmf.gv.at/findok/volltext?segmentId=b30705c6-b74c-4a24-a33b-db227df155a6), Jahresbeleg und letzter Umsatztag, abgerufen am 27.11.2026.
