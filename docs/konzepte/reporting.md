# Reporting-Konzept SPA-Management

Stand: 17.10.2026. Ursprünglich als Claude-Dokument im Projekt erstellt, hier unverändert übernommen.

## Kurzfassung

Reporting wird bei uns kein Menüpunkt "Berichte", sondern ein rollenbasiertes System: Jede Rolle bekommt zur richtigen Zeit genau die Information, die sie zum Handeln braucht, ohne danach zu suchen. Der Therapeut sieht morgens seine Behandlungskarten mit Raum, Kunde, Vorlieben und Vorbereitung; die Rezeption startet mit einem Tagesbriefing; das Management sieht Auslastung und echte Marge pro Behandlung; Behörden bekommen exportfertige Pflichtdaten je Land.

Drei Prinzipien heben uns von der Konkurrenz ab:

1. **Aktiv statt passiv.** Berichte kommen zur Person (Push aufs Tablet, Mail, Druck um 7:30), statt dass jemand sie suchen muss.
2. **Handlungsorientiert.** Jeder Bericht endet in Aufgaben, die man abhaken kann (Raum vorbereiten, Gutschein prüfen, Kunden anrufen), nicht nur in Zahlen.
3. **KI-Zusammenfassung oben.** Jeder Bericht beginnt mit zwei bis drei Sätzen, was heute oder diese Woche wichtig ist und was ungewöhnlich ist.

Alle Berichte laufen auf einer gemeinsamen Datenbasis über alle Standorte, sind standortweise oder gesamt filterbar und in Deutsch, Englisch und weiteren Sprachen verfügbar.

## Therapeuten: Tagesplan und Behandlungskarte

Der Therapeut bekommt jeden Morgen seinen Tagesplan und für jede Behandlung eine Behandlungskarte mit Vorbereitungs-Todo, am Tablet, am Handy oder ausgedruckt. Die Karte wird automatisch aus Buchung, Kundenprofil, Behandlungsrezept und Raumplanung erzeugt und aktualisiert sich live bei Änderungen.

### Tagesplan (pro Therapeut)

- Zeitachse des Tages mit allen Behandlungen, Pausen, Rüst- und Reinigungszeiten
- Je Termin: Uhrzeit, Behandlung, Dauer, Raum, Kunde (mit Hinweis "Erstbesuch", "Stammkunde", "VIP", "Paar-Behandlung")
- Raumwechsel und Wege zwischen Terminen markiert, damit niemand zu spät kommt
- Gesetzliche Pausen und Arbeitszeitende sichtbar (Hinweis bei drohender Überschreitung)
- Live-Status: Kunde eingecheckt, Kunde verspätet, Termin verschoben
- Tagesziel optional: z. B. Produktempfehlungen, Folgetermine

### Behandlungskarte mit Vorbereitungs-Todo (pro Behandlung)

| Bereich | Inhalt | Quelle |
| --- | --- | --- |
| Termin | Uhrzeit, Dauer, Behandlung, Variante (z. B. 60 statt 90 Min.) | Buchung |
| Raum | Raumname, Liege oder Wanne, Temperatur, Licht, Musik, Besonderheiten | Raumplanung |
| Kunde | Name, Foto (optional), Anrede, Sprache, Besuchsanzahl, letzter Besuch | Kundenprofil |
| Gesundheit | Allergien, Unverträglichkeiten, Kontraindikationen (Schwangerschaft, Bluthochdruck, Medikamente), Anamnese-Status | Anamnesebogen |
| Vorlieben | Druckstärke, Duftöl, Raumtemperatur, Musik, Gespräch oder Ruhe, Problemzonen, Tabuzonen, bevorzugter Therapeut | Kundenprofil, Notizen der letzten Behandlungen |
| Historie | Letzte drei Behandlungen mit Notiz des Kollegen, gekaufte Produkte, Beschwerden | Behandlungsverlauf |
| Vorbereitung | Checkliste: Produkte und Mengen, Geräte, Handtücher, Steine aufheizen, Wanne befüllen, Getränk | Behandlungsrezept |
| Anlass | Geburtstag, Gutschein-Einlösung, Paket-Teil 3 von 5, Jubiläum | Buchung, Gutschein |
| Nachbereitung | Notizfeld, Produktverbrauch bestätigen, Empfehlung, Folgetermin vorschlagen | wird zurückgeschrieben |

### Beispiel einer Vorbereitungs-Todo

**10:30 Hot-Stone-Massage 90 Min., Raum Lotus, Frau Müller (6. Besuch)**

- [ ] Steine ab 10:00 auf 55 °C aufheizen
- [ ] Liege mit Wärmedecke, Raum auf 24 °C
- [ ] Lavendelöl bereitstellen (Vorliebe), kein Eukalyptus (Unverträglichkeit)
- [ ] Ruhige Musik, Kundin wünscht wenig Gespräch
- [ ] Fokus Nacken und Schultern (Notiz vom letzten Mal)
- [ ] Geburtstag diese Woche: Grußkarte von der Rezeption

### Warum das besser ist als heute üblich

- Die Karte lernt mit: Was der Therapeut nach der Behandlung notiert ("mag festeren Druck"), steht beim nächsten Kollegen automatisch auf der Karte. So bekommt der Gast an jedem Standort dieselbe persönliche Behandlung.
- Der Materialverbrauch aus der Nachbereitung fließt direkt in Lager und echte Kosten pro Behandlung.
- Kritische Gesundheitsinfos (Allergie, Schwangerschaft) sind rot hervorgehoben und müssen bei Erstkontakt quittiert werden.

## Rezeption und Tagesstart

Die Rezeption startet den Tag mit einem Tagesbriefing pro Standort und schließt ihn mit einem geführten Tagesabschluss. Dazwischen arbeitet sie mit Live-Listen statt mit Berichten.

### Tagesbriefing (automatisch z. B. 30 Min. vor Öffnung)

- **KI-Zusammenfassung:** "Heute 64 Behandlungen, Auslastung 87 %. Zwei Therapeuten krank, drei Termine noch ohne Ersatz. Zwei VIP-Gäste, ein Erstbesuch mit offenem Anamnesebogen."
- Personal: wer ist da, wer fehlt, Schichtlücken, Vertretungsvorschläge
- Termine mit Handlungsbedarf: ohne Therapeut, Raumkonflikt, unbestätigt, offene Anzahlung
- Gäste: Erstbesucher, VIPs, Geburtstage, Gutschein-Einlösungen, Paket-Abschlüsse, Beschwerden vom letzten Besuch
- Räume und Geräte: gesperrte Räume, Wartungen, Sauna-Aufheizzeiten
- Lücken im Kalender mit Vorschlag: Warteliste anschreiben, Last-Minute-Angebot
- Kassa: Wechselgeld-Anfangsbestand, TSE- oder Signaturgerät-Status

### Live-Listen während des Tages

- Ankünfte der nächsten 60 Minuten mit Check-in-Status und Verspätungen
- Wer ist gerade in welchem Raum, was wird wann frei
- Offene Zahlungen und Rechnungen, nicht abgeholte Gutscheine
- No-Shows und späte Absagen mit Vorschlag für Ausfallgebühr
- Eingehende Buchungsanfragen (Mail, Telefon, SMS, Sprache), die die KI nicht automatisch buchen konnte

### Tagesabschluss

1. Kassenbericht pro Kassa: Bar, Karte, Sofortüberweisung, Gutschein, Split-Zahlungen, Trinkgeld getrennt
2. Kassensturz mit Soll-Ist-Vergleich und Begründungspflicht bei Differenz
3. Z-Bon bzw. Tagesabschluss gemäß Landesrecht (DE: Kassenabschluss TSE, AT: Tagesbericht mit Signatur, Monatsbeleg am Monatsende)
4. Gutschein-Bewegungen: verkauft, eingelöst, Restwerte
5. Offene Punkte für morgen: Anrufe, Nachbuchungen, Beschwerden
6. Übergabenotiz an die Frühschicht, erscheint im nächsten Tagesbriefing

## Management

Das Management bekommt ein Cockpit mit wenigen Kennzahlen, die echte Entscheidungen auslösen, plus einen automatischen Wochen- und Monatsbericht mit KI-Kommentar. Jede Kennzahl ist per Klick bis zur einzelnen Buchung aufschlüsselbar und nach Standort, Therapeut, Behandlung und Zeitraum filterbar.

### Kennzahlen

| Bereich | Kennzahl | Wofür |
| --- | --- | --- |
| Auslastung | Auslastung je Therapeut, Raum und Standort; Umsatz pro Raumstunde und pro Therapeutenstunde | Personal- und Raumplanung, Preise |
| Ertrag | Umsatz, Deckungsbeitrag und echte Marge pro Behandlung (Material, Personal, Raummiete, Reinigung) | Welche Behandlungen sich lohnen |
| Kunden | Neukunden, Wiederkehrrate, Rebooking-Quote, Kundenwert, abwandernde Stammkunden | Bindung, Marketing |
| Ausfälle | No-Show- und Storno-Quote, entgangener Umsatz, eingenommene Ausfallgebühren | Regeln für Anzahlung und Stornierung |
| Personal | Produktivität, Provisionen, Überstunden, Krankenstand, Kundenzufriedenheit je Therapeut | Mitarbeitergespräche, Lohnabrechnung |
| Verkauf | Produktverkauf pro Behandlung, Gutscheinverkauf, offene Gutscheinverbindlichkeiten | Upselling, Liquidität, Bilanz |
| Kanäle | Buchungen je Kanal (Web, Telefon, KI-Mail, Treatwell) inkl. Provisionskosten | Kanal-Strategie |
| Lager | Verbrauch Soll gegen Ist, Schwund, Mindestbestände | Einkauf |

### Standortvergleich und Benchmark

- Alle Kennzahlen nebeneinander für jeden Standort, Abweichungen farblich markiert
- Benchmark mit anderen Spas per Klick: anonymisiert, nur aus Kunden, die zustimmen, gruppiert nach Größe und Region (z. B. "Ihre Massage-Marge liegt 8 Punkte unter vergleichbaren Day-Spas in Österreich")

### Automatische Berichte

- **Wochenbericht** jeden Montag: was lief gut, was schlecht, drei konkrete Empfehlungen
- **Monatsbericht**: Ergebnis je Standort, Kostenstellen und Erlöskonten wie in der Buchhaltung, Vergleich zu Vormonat und Vorjahr
- **Prognose**: erwartete Auslastung und Umsatz der nächsten vier Wochen aus dem Buchungsstand
- **Alarme** statt Berichte, wenn eine Kennzahl kippt (z. B. No-Show-Quote eines Standorts über 10 %)

## Behörden und Steuer

Pflichtberichte entstehen bei uns nicht am Jahresende in Handarbeit, sondern laufend und prüfungsfest: Jeder Standort hat einen Bereich "Prüfung und Export", der auf Knopfdruck das liefert, was Finanzamt, Arbeitsinspektorat oder Steuerberater verlangen. Die Liste unten ist aus unserem Fachwissen zusammengestellt und muss vor Umsetzung mit einem Steuerberater je Land geprüft werden.

| Land | Pflicht | Bericht oder Export im System |
| --- | --- | --- |
| DE | KassenSichV, TSE | TSE-Status, Kassenabschluss, DSFinV-K-Export für Kassen-Nachschau und Außenprüfung |
| DE | Mitteilungspflicht Kassen (§ 146a AO) | Liste aller Kassen und TSE mit Seriennummern, Inbetriebnahme, Außerbetriebnahme für die ELSTER-Meldung |
| DE | GoBD | Unveränderbares Journal, Verfahrensdokumentation, Datenträgerüberlassung (Z3) |
| DE | Arbeitszeit (ArbZG, MiLoG) | Arbeitszeitnachweise je Mitarbeiter mit Beginn, Ende, Pausen |
| AT | RKSV | Startbeleg, Monatsbeleg, Jahresbeleg mit Prüferinnerung, DEP-Export (Datenerfassungsprotokoll), Status der Signaturerstellungseinheit |
| AT | Arbeitszeitgesetz | Arbeitszeitaufzeichnungen je Mitarbeiter für das Arbeitsinspektorat |
| AT | Bäderhygiene (falls Pool oder Whirlpool) | Betriebsbuch mit Wasserwerten und Kontrollen |
| CH | MWST (effektiv oder Saldosteuersatz) | MWST-Abrechnungsgrundlage pro Quartal bzw. Semester |
| CH | Geschäftsbücherverordnung (GeBüV) | Revisionssicheres Journal und Belegarchiv |
| CH | Arbeitsgesetz | Arbeitszeiterfassung je Mitarbeiter |
| alle | DSGVO bzw. revDSG | Auskunftsbericht pro Kunde, Löschprotokoll, Verzeichnis der Verarbeitungen |
| alle | Buchhaltung | Export an DATEV (DE), BMD und RZL (AT), Abacus, Bexio, Banana (CH), getrennt nach Erlöskonten und Kostenstellen |
| alle | Gutscheine | Gutscheinverbindlichkeiten zum Stichtag für den Jahresabschluss |

### Grundsätze

- Kassendaten werden nie geändert, nur storniert und neu gebucht; jede Korrektur ist im Journal sichtbar.
- Aufbewahrungsfristen je Land sind hinterlegt, das System löscht nichts vor Ablauf und erinnert danach.
- Ein Prüfermodus gibt einem Behördenvertreter Lesezugriff auf genau die nötigen Daten, protokolliert.
- Das System erinnert an Fristen: Jahresbeleg in AT, Meldung neuer Kassen in DE, Umsatzsteuer-Termine.

## Weitere Rollen

Dieselben Daten versorgen auch die Rollen, die in Spas oft vergessen werden, jeweils mit einer eigenen Tagesliste.

- **Housekeeping und Raumpflege:** Reinigungsplan aus dem Kalender abgeleitet (Raum frei um 11:30, Reinigung 15 Min., nächste Behandlung 11:45), Wäschebedarf pro Tag, Sauna- und Wannen-Reinigung mit Abhakliste und Hygieneprotokoll.
- **Lager und Einkauf:** Bedarf der nächsten Tage aus den gebuchten Behandlungen und Behandlungsrezepten, Bestellvorschläge, Ablaufdaten von Produkten.
- **Schichtleitung und Personalplanung:** Wochen- und Dienstplan mit Prüfung gegen Arbeitszeitgesetz und Ruhezeiten, Qualifikationsmatrix (wer darf welche Behandlung), Urlaubs- und Krankenstandsübersicht.
- **Mitarbeiter selbst:** persönlicher Monatsbericht mit Stunden, Provisionen, Trinkgeld, Kundenbewertungen und Weiterbildungen.
- **Haustechnik:** Wartungsplan für Geräte, Sauna, Pool; Sperrungen von Räumen fließen direkt in den Kalender.

## KI im Reporting

Die KI macht aus Berichten Antworten: Sie fasst zusammen, erklärt Abweichungen und schlägt Maßnahmen vor, rechnet aber nie selbst Pflichtzahlen. Steuer- und Kassenwerte kommen immer aus dem deterministischen Rechenkern.

- **Fragen in Alltagssprache:** "Welche Behandlung hatte im September in Graz die schlechteste Marge?" ergibt Antwort, Tabelle und Link zu den Buchungen.
- **Zusammenfassung oben** in jedem Bericht, in der Sprache des Lesers.
- **Abweichungen erkennen:** ungewöhnliche Kassendifferenzen, steigende No-Shows, Therapeut mit sinkender Bewertung, Produktschwund.
- **Behandlungskarte anreichern:** aus Freitextnotizen der letzten Besuche werden strukturierte Vorlieben ("festerer Druck", "Nackenfokus") vorgeschlagen; der Therapeut bestätigt.
- **Sprachausgabe für Therapeuten:** Briefing zur nächsten Behandlung vorlesen lassen, Nachbereitung per Sprache diktieren.
- **Berichte selbst bauen:** Manager beschreibt einen Bericht in Worten, die KI erstellt ihn und speichert ihn als wiederkehrenden Bericht.

## Datenschutz und Berechtigungen

Allergien, Kontraindikationen und Anamnese sind Gesundheitsdaten (Art. 9 DSGVO, im revDSG besonders schützenswert) und brauchen eine ausdrückliche Einwilligung des Gastes sowie strenge Zugriffsregeln.

- Der Therapeut sieht Gesundheitsdaten nur für seine eigenen Behandlungen und nur am Behandlungstag.
- Die Rezeption sieht nur "Hinweis vorhanden", nicht den Inhalt.
- Das Management sieht Kennzahlen, aber keine Gesundheitsdaten einzelner Gäste.
- Jeder Zugriff auf Gesundheitsdaten wird protokolliert.
- Ausgedruckte Behandlungskarten zeigen Gesundheitsinfos nur als Kurzhinweis; für Papier gibt es eine Einstellung pro Standort.
- Der Benchmark mit anderen Spas nutzt nur aggregierte, anonymisierte Kennzahlen und ist Opt-in.
- Vorlieben (Duft, Musik, Druck) sind keine Gesundheitsdaten und dürfen standortübergreifend geteilt werden, sofern der Gast dem gemeinsamen Kundenstamm zugestimmt hat.

## Technische Architektur des Reportings

Berichte rechnen nie live auf den Buchungs- und Kassentabellen, sondern lesen vorberechnete Lesemodelle. So bleiben Kalender und Kassa auch bei großen Monatsberichten sofort reaktionsfähig, wie es die Vorgabe "UI so schnell wie möglich, Backend entkoppelt" verlangt.

```
Fachmodule (Kalender, Kasse, Kunden, Personal) → Ereignisse → Hintergrunddienste → Lesemodelle → Ausgabe (App, Web, PDF, Druck, Mail, Push)
Kassenjournal (unveränderbar) → Pflicht-Exporte (getrennt, nie aus Lesemodellen oder KI)
```

- **Ereignisgesteuert (CQRS):** Jede Buchung, Zahlung oder Änderung erzeugt ein Ereignis; Hintergrunddienste in .NET 10 aktualisieren daraus die Lesemodelle innerhalb von Sekunden.
- **Ein Berichtskern, viele Ausgaben:** Ein Bericht wird einmal definiert und erscheint in der Therapeuten-App, im Windows-Client, im Web, als PDF, Druck, Mail oder Push.
- **Zeitpläne und Auslöser:** Tagesbriefing um eine feste Uhrzeit, Wochenbericht am Montag, Alarm bei Schwellwert; alles pro Standort einstellbar.
- **Pflichtdaten getrennt:** Kassenjournal und Steuerexporte kommen aus dem unveränderbaren Journal, nie aus Lesemodellen oder der KI.
- **Offline-fähig:** Die Behandlungskarten des Tages liegen auf dem Tablet zwischengespeichert, falls das WLAN im Behandlungsraum ausfällt.
- **Erweiterbar:** Eigene Berichte per Baukasten oder KI, Datenexport über eine offene Schnittstelle für Power BI oder Excel.

## Priorisierung und offene Fragen

Mein Vorschlag: zuerst das, was der Betrieb täglich braucht und was rechtlich Pflicht ist, danach Management-Auswertungen und KI-Komfort.

| Stufe | Inhalt | Begründung |
| --- | --- | --- |
| 1 | Kassenabschluss und Pflicht-Exporte je Land, Behandlungskarte mit Vorbereitungs-Todo, Tagesplan Therapeut, Tagesbriefing Rezeption | Ohne Kassenpflichten kein Verkauf; Behandlungskarte ist das sichtbarste Alleinstellungsmerkmal |
| 2 | Management-Cockpit, Wochen- und Monatsbericht, Standortvergleich, Housekeeping- und Lagerlisten | Braucht Daten aus Stufe 1 und echte Kostenrechnung |
| 3 | KI-Fragen in Alltagssprache, Berichte per KI bauen, Benchmark mit anderen Spas, Prognosen | Braucht Datenmenge und mehrere teilnehmende Spas |

### Offene Fragen an dich

- [ ] Sollen Therapeuten Tablets im Behandlungsraum haben, oder reicht Handy und Ausdruck?
- [ ] Sollen Behandlungskarten auch bei anderen Standorten die Notizen der Kollegen zeigen (gemeinsamer Kundenstamm)?
- [ ] Welche Buchhaltungssysteme nutzen deine ersten Wunschkunden (DATEV, BMD, RZL, Bexio, Abacus)?
- [ ] Gibt es Spas mit Pool oder Whirlpool, für die wir das Hygiene-Betriebsbuch brauchen?
- [ ] Wer soll die Gesundheitsdaten-Einwilligung einholen: Online bei der Buchung, am Tablet beim Check-in, oder beides?
