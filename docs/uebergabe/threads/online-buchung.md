# Übergabe: Online-Buchung und Kundenbereich

Stand 18.10.2026, 21:40 UTC. Gestoppt auf Anweisung des Auftraggebers.

## Gebaut und gemergt

Der Kundenbereich (`web/src/booking`, Backend `src/AispaStudio.Api/Online`) entstand in diesem Thread. Die Abschnitte im [PR-Protokoll](https://github.com/vompa/aispa-studio/blob/main/docs/umsetzung/pr-protokoll.md) beschreiben jeden PR im Detail. Die wichtigsten PRs dieses Threads sind:

- #9: Online-Buchung, Kundenkonten mit Passkey oder Code, Anamnese per Link.
- #37: Französisch und Italienisch, Barrierefreiheit.
- Online-Gutscheine mit Zahlungsanbieter (Test und Stripe), Ausgabe über die Kassa.
- #53: Kartenhinterlegung und Ausfallgebühr ([ADR 0036](https://github.com/vompa/aispa-studio/blob/main/docs/entscheidungen/0036-kartenhinterlegung-und-ausfallgebuehr.md)).
- #57: Rückerstattung nur nach Storno an der Kassa ([ADR 0038](https://github.com/vompa/aispa-studio/blob/main/docs/entscheidungen/0038-rueckerstattung-nur-nach-storno.md)).
- #70: Karte per Link für Termine der Rezeption.
- #74: Karten 14 Tage nach dem Termin löschen.
- #76: Termine ohne Karte nach Frist freigeben.
- #83: Ausfallgebühr je Behandlung und nur für neue Gäste. Das ist der letzte Merge auf `main` (ddd60b5).

## Halb fertig: Anzahlung und Vorauszahlung

- Der Commit 3bb721e liegt auf dem Branch `claude/online-buchung-ceyrvq`. Er ist gepusht, aber es gibt keinen PR und nichts ist gemergt.
- Der Inhalt ist vollständig:
  - Backend mit beiden Migrationen `Anzahlung`.
  - Kundenbereich in 4 Sprachen.
  - Anzeige im Termin und in der Web-Kassa.
  - Demo-Daten für Graz und das Hamam-Ritual.
  - Neuer ADR 0042 (`docs/entscheidungen/0047-anzahlung-und-vorauszahlung.md`) mit Nachträgen zu ADR 0036 und 0038, README, Stand und Testdaten.
  - Im PR-Protokoll steht noch der Platzhalter `161` statt der PR-Nummer.
- Geprüft:
  - Die neuen Tests waren grün: `DepositTests`, `DemoSeederTests`, Booking-, Kassa-, Stammdaten- und `DepositSection`-Tests.
  - Der Typecheck des Webs war grün.
  - Der volle lokale Lauf (Format, Build, alle Tests, Lint, Bundle-Größe) wurde beim Stopp abgebrochen. Bis dahin lief er ohne Fehler, bei den API-Tests war er bei 349 von etwa 400 Tests.
- Zum Abschließen:
  1. Den vollen Lauf wiederholen.
  2. `main` hineinmergen.
  3. Den PR öffnen und `161` in `docs/` durch die Nummer ersetzen (`sed -i 's/161/N/g'`).
  4. Mergen.

## Entscheidungen (Details im PR-Protokoll und in den ADRs)

**Ausfallgebühr und Karte (ADR 0036)**
- Die Ausfallgebühr wird nur auf Knopfdruck der Kassenaufsicht belastet und immer mit Beleg an der Kassa verbucht.
- Steuersatz und Erlöskonto kommen von der Behandlung.
- Die Karte bleibt 14 Tage nach dem Termin gespeichert, danach wird sie beim Anbieter gelöscht.

**Erstattung (ADR 0038)**
- Erstattet wird nur über das Storno des Belegs, immer der volle Betrag.

**Anzahlung (ADR 0042)**
- Eine Anzahlung ersetzt die Karte. Sie geht nur mit Zahlungsanbieter und aktiver Kasse.
- Der Anzahlungsbeleg hat den Steuersatz der Behandlung. Beim Kassieren der Behandlung wird er als negative Zeile mit gleichem Satz abgezogen.
- Ein schon verrechneter Anzahlungsbeleg ist erst stornierbar, wenn der Beleg der Behandlung storniert ist.
- Bei rechtzeitiger Online-Absage storniert und erstattet das System selbst. Bei Nichterscheinen oder Absage durch das Team erstattet nur das Storno durch die Kassenaufsicht.
- Frist (`CardHoldHours`) und „nur neue Gäste“ gelten für Karte und Anzahlung gemeinsam.

**Prozente**
- Prozente der Behandlung gehen vor der Regel des Standorts, an jedem Standort.

## Nächste offene Punkte (gewählte Reihenfolge)

1. Den Anzahlungs-PR wie oben abschließen.
2. Den Gast vor Ablauf der Frist für Karte oder Anzahlung erinnern.
3. Anzahlung für Termine der Rezeption per Link, analog zu „Karte per Link anfordern“.
4. Zahlungs- und Kartenlink auch per SMS.
5. Stripe-Kunden aus abgebrochenen Hinterlegungen löschen.
6. Bevorzugte Sprache des Gastes im Kundenstamm speichern.
7. Teilerstattung.
8. Online-Bestellungen als Liste in der Kassa.
9. Anzahlung in der Windows-App anzeigen.

## Überlegungen für später

- Steuerberatung: Klären, ob die Ausfallgebühr in Österreich echter Schadenersatz ohne Umsatzsteuer ist. Sie wird heute vorsichtshalber versteuert.
- Regeln für Ausfallgebühr und Anzahlung je Kundengruppe, Tageszeit oder vorhergesagtem No-Show-Risiko (siehe Konkurrenzanalyse).
- Eigene Wissensbasis des Spas für Fragen im Kundenbereich.
- Partneranfragen an Treatwell und Google sind weiter angehalten und nicht verschickt.
