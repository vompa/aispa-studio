# 0095. Trinkgeld an der Kassa: durchlaufender Posten für das Team

- **Status:** angenommen, Steuerberatung offen
- **Datum:** 16.11.2026
- **Entschieden von:** Claude, autonom nach ADR 0019 (Auftrag des Auftraggebers vom 20.10.2026: „Startfähigkeit der Kassa“ mit Trinkgeld; Freigabe zum autonomen Abarbeiten am 16.11.2026)

## Kontext

Zahlt ein Gast mit Karte und rundet auf („100, stimmt so“ bei 92,00), ließ sich das bisher nicht buchen: Die Kassa lehnte mehr als den offenen Betrag ab, weil es Rückgeld nur bar gibt. Die Rezeption musste den Betrag am Terminal von Hand trennen oder das Trinkgeld ablehnen.

Rechtlicher Rahmen, Stand der Recherche vom 16.11.2026:

- Trinkgeld, das Gäste dem Team freiwillig geben, ist kein Umsatz des Betriebs. Läuft es über die Kasse, ist es ein durchlaufender Posten.
- Österreich (RKSV): Durchlaufende Posten stehen auf einem signierten Beleg, zählen aber nicht in den Umsatzzähler (Auskünfte des BMF an den Arbeitskreis Kassensoftware, 2015).
- Deutschland (DSFinV-K): Geschäftsvorfall `TrinkgeldAN` (Trinkgeld an Arbeitnehmer), nicht steuerbar (Steuerschlüssel 5). `TrinkgeldAG` wäre Trinkgeld an den Betrieb und damit steuerpflichtiger Umsatz.

## Optionen

| Option | Bewertung |
| --- | --- |
| Trinkgeld als Belegzeile mit 0 % | zählt in Umsatz, Betrag-Satz-Null und Umsatzzähler; jede Auswertung müsste es wieder herausrechnen |
| Trinkgeld als eigene Zahlungsart | Zahlungen wären dann kleiner als das, was am Terminal abgebucht wurde |
| Trinkgeld als eigener Betrag am Beleg | gewählt: Umsatz, Steuer und Erlöskonten bleiben unberührt, die Zahlungen nennen den wirklichen Betrag |
| Trinkgeld an den Betrieb (`TrinkgeldAG`) | nicht gebaut: wäre versteuerter Umsatz und gehört als Artikel auf die Rechnung |

## Entscheidung

1. **Ein Betrag am Beleg:** `Receipt.Tip`, mitgegeben in `CheckoutRequest.Tip`. Es gilt `Summe der Zahlungen = Total + Tip`. Das Trinkgeld ist keine Zeile, hat keinen Steuersatz und kein Erlöskonto.
2. **Bezahlen:** Unbare Zahlungen dürfen den offenen Betrag um genau das Trinkgeld übersteigen. Bar deckt den Rest einschließlich Trinkgeld, darüber gibt es Rückgeld. Ohne eingetragenes Trinkgeld bleibt „Zu viel bezahlt“ wie bisher; die Kassa rät nie, dass ein Überschuss Trinkgeld sei.
3. **Österreich:** Der Beleg wird signiert wie jeder andere. Die fünf Beträge des maschinenlesbaren Codes und der Umsatzzähler enthalten das Trinkgeld nicht.
4. **Deutschland:** Die Vorgangsdaten führen das Trinkgeld im Feld für 0 %, damit Beträge und Zahlungen aufgehen. Die DSFinV-K meldet es als `TrinkgeldAN` mit Steuerschlüssel 5 in `lines.csv`, `lines_vat.csv`, `transactions_vat.csv` und `businesscases.csv`. Der Zähler der Kasse zählt es nicht.
5. **Storno:** Der Stornobeleg trägt das Trinkgeld negativ.
6. **Tagesabschluss:** `Tips` steht neben dem Umsatz. Die Zahlarten enthalten das Trinkgeld. Bares Trinkgeld liegt in der Lade und zählt zum Soll-Bestand, bis es als Entnahme ausbezahlt wird.
7. **Buchhaltung:** Je Beleg eine Buchung vom Geldtransit auf das Konto „Trinkgeld für das Team“ (`AccountingSettings.TipsAccount`, Vorgabe 3790, Verbindlichkeit), ohne Steuer.
8. **Beleg:** Die Zeile „Trinkgeld (kein Umsatz)“ steht bei den Zahlungen, nicht bei den Positionen.

## Begründung

- Wer Umsätze auswertet, soll nichts herausrechnen müssen: Trinkgeld taucht in keiner Umsatz-, Steuer- oder Kontensumme auf ([ADR 0010](0010-kassa-compliance-by-design.md): Pflichtzahlen stimmen ohne Nacharbeit).
- Die Zahlung am Beleg entspricht der Abbuchung am Terminal. Das erleichtert den Abgleich mit der Abrechnung des Zahlungsdienstleisters.
- Das Trinkgeld wird ausgewiesen und gebucht, nicht versteckt: Es steht auf Beleg, Tagesabschluss, Export und in der Buchhaltung.

## Folgen und Stand

- Gebaut (Server): `PosService.BuildCheckoutAsync`, `Receipt.Tip` (Migration `ReceiptTip`), `KassenSichVFiscalization.ProcessData`, `Dsfinvk`, `BuildDayReport`, `AccountingExport`, `ReceiptDocument`, `ReceiptPrinter`, `DayClosingDocument`; Tests in `TipTests`.
- Gebaut (Windows-App, Schritt 2): Feld „Trinkgeld“ unter den Zahlungsarten (`PosViewModel.TipText`), Verweis „Als Trinkgeld eintragen“, sobald unbar mehr als offen eingetragen ist, Zeile „Trinkgeld (kein Umsatz)“ in der Belegvorschau und „davon Trinkgeld (kein Umsatz)“ im Tagesbericht; Hilfe `kassa-bezahlen#trinkgeld` mit Bild `kassa-trinkgeld.png`.
- Gebaut (Schritt 3, je Person): `Receipt.TipEmployeeId` (Migration `ReceiptTipEmployee`). Die Kasse ordnet das Trinkgeld der Person zu, die alle Termine des Belegs behandelt hat; bei mehreren Personen oder ohne Termin gilt es dem Team. `CheckoutRequest.TipEmployeeId` nennt die Person ausdrücklich. Der Tagesabschluss führt `TipsByEmployee`, im PDF und in der App je Person eine Zeile.
- Offen, später: Person beim Bezahlen in der App wählen; Trinkgeld je Person über einen Zeitraum auswerten; Auszahlung über die Lohnabrechnung statt aus der Lade.
- **Steuerberatung:** Behandlung in der Lohnverrechnung (AT: Trinkgeldpauschale der ÖGK; DE: § 3 Nr. 51 EStG), Kontonummer 3790 je Kontenrahmen, Schweiz (Trinkgeld als Lohnbestandteil). Prüfen, ob der österreichische Beleg einen eigenen Hinweis braucht.
- DSFinV-K ist gegen die Fassung 2.3 gebaut; die Prüfung mit dem Prüfprogramm der Finanzverwaltung steht weiter aus ([ADR 0035](0035-test-tse-und-dsfinvk.md)).

## Quellen

- [FAQ des Arbeitskreises Kassensoftware mit Antworten des BMF, 20.12.2015](https://wiki.softengine.de/wp-content/uploads/2015/12/FAQ-Arbeitskreis-Kassensoftware-20151220.pdf)
- [fiskaly: Vorgangstypen und Geschäftsvorfälle der DSFinV-K](https://developer.fiskaly.com/dsfinvk/process_types_business_transaction_types)
- [fiskaltrust: Zuordnung „tip to employee“ zu `TrinkgeldAN`](https://docs.fiskaltrust.eu/docs/poscreators/middleware-doc/germany/reference-tables/ftpayitemcase)
- [ZDH-Praxishilfe Trinkgelder, 08/2022](https://www.hwk-reutlingen.de/fileadmin/hwk/presse_dokumente/2022/zdh-praxishilfe_trinkgelder_202208.pdf)
- [ADR 0010](0010-kassa-compliance-by-design.md), [ADR 0035](0035-test-tse-und-dsfinvk.md)
