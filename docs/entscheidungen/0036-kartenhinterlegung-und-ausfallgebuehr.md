# 0036. Kartenhinterlegung und Ausfallgebühr nur auf Knopfdruck und über die Kassa

- **Status:** angenommen
- **Datum:** 18.10.2026
- **Entschieden von:** Claude (autonom, siehe ADR 0019)

## Kontext

No-Shows kosten Day-Spas einen spürbaren Teil des Umsatzes; Anzahlung und Kartenhinterlegung sind laut [ergänzenden Themen](../konzepte/ergaenzende-themen.md) (Abschnitt 5) das wirksamste Mittel. Offen war, wie die Gebühr eingezogen und verbucht wird, ohne an der Kassa vorbeizulaufen ([ADR 0010](0010-kassa-compliance-by-design.md)), und ob gleich eine Anzahlung kommt.

## Optionen

| Option | Bewertung |
| --- | --- |
| Anzahlung beim Buchen | Geld fließt vor der Leistung: braucht einen Anzahlungsbeleg nach RKSV/KassenSichV und die Verrechnung beim Kassieren; aufwendiger, später |
| Karte hinterlegen, Gebühr automatisch bei „Nicht erschienen“ | bequem, aber ein Fehlklick an der Rezeption belastet sofort einen Gast |
| Karte hinterlegen, Gebühr nur auf Knopfdruck der Kassenaufsicht, Beleg an der Kassa | gewählt |

## Entscheidung

- Je Standort eine Ausfallgebühr in Prozent des Preises (Stammdaten → Standorte → Online-Buchung, `NoShowFeePercent`, 0 = keine). Sie gilt nur mit eingerichtetem Zahlungsanbieter.
- Beim Online-Buchen sieht der Gast Betrag und Regel und muss ausdrücklich zustimmen; gespeichert werden Prozent, Betrag, Stornofrist und Zeitpunkt der Zustimmung (`online.CardGuarantees`).
- Danach hinterlegt der Gast eine Karte beim Zahlungsanbieter (Stripe Checkout im Modus „setup“, nur Karte, mit starker Kundenauthentifizierung). Gespeichert werden nur die Kennungen beim Anbieter, nie Kartendaten.
- Fällig ist die Gebühr bei „Nicht erschienen“ oder einer Absage nach der beim Buchen geltenden Stornofrist. Belastet wird nur, wenn die Kassenaufsicht (`SuperviseCash`) es im Termin auslöst und bestätigt.
- Die Belastung läuft über die Queue: erst wird geprüft, dass eine aktive Kassa am Standort existiert, dann belastet (Idempotenzschlüssel je Hinterlegung), dann an der Kassa verbucht als Position „Ausfallgebühr …“ mit Steuersatz und Erlöskonto der Behandlung, bezahlt mit „Karte“ und der Referenz des Anbieters. Der Gast bekommt den Beleg per Mail.

## Begründung

Die Kartenhinterlegung bewegt bis zum No-Show kein Geld und braucht deshalb keinen Anzahlungsbeleg. Die Gebühr landet wie jeder Umsatz mit Signatur im Tagesabschluss und in der Buchhaltung. Der ausdrückliche Knopfdruck schützt Gäste vor Fehlbuchungen; ob eine Gebühr fällig ist, prüft der Server.

## Folgen und Stand

- Code: `Online/CardGuarantees.cs`, `Online/CardGuaranteeHandlers.cs`, `Online/CardGuaranteeEndpoints.cs`, `StripePaymentProvider.StartCardSetupAsync`/`ChargeSavedCardAsync`; Tests in `CardGuaranteeTests` und `StripePaymentTests`.
- Steuer: Die Gebühr wird vorsichtshalber wie die Behandlung versteuert (EuGH C-295/17 „MEO“ sieht Entgelt, wenn die Leistung bereitstand). Ob in Österreich echter Schadenersatz ohne Umsatzsteuer vorliegt, klärt der Auftraggeber mit der Steuerberatung; dann reicht eine Einstellung des Steuersatzes.
- Offen: Link für Buchungen an der Rezeption auch per SMS. (Anzahlung und Vorauszahlung: [ADR 0047](0047-anzahlung-und-vorauszahlung.md).)
- Nachtrag (PR #70): Für Buchungen an der Rezeption schickt der Termin einen Link per Mail. Die Zustimmung des Gastes holt die Terminseite ein (Status `AwaitingConsent`); vorher wird weder hinterlegt noch belastet.
- Nachtrag (PR #74): Die Ausfallgebühr lässt sich bis 14 Tage nach dem Ende des Termins belasten (`OnlineBooking:CardRetentionDays`). Danach löscht ein stündlicher Lauf die Karte beim Anbieter (bei Stripe den Kunden, den das Hinterlegen angelegt hat) und vergisst ihre Kennungen; Zahlungen und Erstattungen bleiben beim Anbieter nachvollziehbar. Grund: Datenminimierung (Art. 5 Abs. 1 lit. c und e DSGVO), die Karte ist nur für diesen einen Termin hinterlegt.
- Nachtrag (PR #76): Je Standort lässt sich eine Frist für die Karte einstellen (`CardHoldHours`, 0 = nie, höchstens 168 Stunden). Ist bei einem online gebuchten Termin nach so vielen Stunden keine Karte hinterlegt, wird er freigegeben (Status `Released`), die Warteliste rückt nach und der Gast bekommt eine Mail. Die Frist wird beim Buchen festgehalten und steht in der Bestätigung; Termine der Rezeption und Fristen, die erst nach Terminbeginn enden, werden nie freigegeben.
- Nachtrag (PR #83): Eine Behandlung kann eigene Prozent haben (`Treatment.NoShowFeePercent`, leer = Regel des Standorts, 0 = keine); sie gelten an jedem Standort, weil die Gebühr am Wert der Behandlung hängt. Ein Standort kann die Gebühr auf neue Gäste beschränken (`NoShowFeeNewCustomersOnly`): neu ist, wer im Spa noch keine abgeschlossene Behandlung hatte, standortübergreifend, weil der Kundenstamm geteilt ist. Beim Online-Buchen zählt der schon bekannte Gast (Konto oder E-Mail); Unbekannte gelten als neu.
- Nachtrag (PR #161): Verlangt ein Standort oder eine Behandlung eine Anzahlung ([ADR 0047](0047-anzahlung-und-vorauszahlung.md)) und ist sie möglich (Zahlungsanbieter und aktive Kasse), ersetzt sie für diesen Termin die Kartenhinterlegung. Frist (`CardHoldHours`) und „nur neue Gäste“ gelten für beides.

## Quellen

- [Ergänzende Themen, Abschnitt 5](../konzepte/ergaenzende-themen.md)
- [ADR 0010](0010-kassa-compliance-by-design.md), PSD2 (händlerinitiierte Zahlungen), EuGH C-295/17
- [PR-Protokoll](../umsetzung/pr-protokoll.md)
