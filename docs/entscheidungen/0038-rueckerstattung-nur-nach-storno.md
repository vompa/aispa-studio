# 0038. Online-Zahlungen nur nach Storno an der Kassa erstatten

- **Status:** angenommen
- **Datum:** 18.10.2026
- **Entschieden von:** Claude (autonom, siehe ADR 0019)

## Kontext

Online-Gutscheine und Ausfallgebühren werden über einen Zahlungsanbieter bezahlt und an der Kassa mit Beleg verbucht ([ADR 0010](0010-kassa-compliance-by-design.md), [ADR 0036](0036-kartenhinterlegung-und-ausfallgebuehr.md)). Gäste wollen Gutscheine zurückgeben, und eine zu Unrecht belastete Ausfallgebühr muss zurück. Offen war, wo die Erstattung ausgelöst wird, ohne dass Geld ohne Beleg fließt.

## Optionen

| Option | Bewertung |
| --- | --- |
| Erstattung direkt beim Anbieter (Stripe-Dashboard) | Geld geht zurück, der Beleg bleibt stehen: Tagesabschluss und Buchhaltung stimmen nicht mehr |
| Eigene Schaltfläche „Erstatten“ an der Bestellung | zweiter Weg neben dem Storno, Beleg und Erstattung können auseinanderlaufen |
| Storno an der Kassa löst die Erstattung aus | gewählt |

## Entscheidung

- Erstattet wird nur, wenn der online bezahlte Beleg an der Kassa storniert wurde (Kassenaufsicht, Grund ist Pflicht). Das Storno meldet `ReceiptVoided` über die Queue; der Kundenbereich erstattet daraufhin den vollen Betrag über den Anbieter, mit Idempotenzschlüssel je Erstattung.
- Ein schon eingelöster Gutschein lässt sich nicht stornieren und damit nicht erstatten.
- Die wieder geöffnete Rechnung wird geschlossen, damit nichts ein zweites Mal kassiert wird.
- Lehnt der Anbieter ab, zeigt der Beleg den Grund; die Kassenaufsicht stößt die Erstattung erneut an. Der Gast bekommt den Stornobeleg per Mail.

## Begründung

Storno und Erstattung sind ein Vorgang: der Stornobeleg mit Signatur steht im Tagesabschluss, die Buchhaltung bucht die Gegenbuchung, und der Anbieter zahlt genau diesen Betrag zurück. Wer trotzdem im Dashboard des Anbieters erstattet, muss den Beleg an der Kassa stornieren; die Erstattung über das System lehnt der Anbieter dann ab, und der Grund steht im Beleg.

## Folgen und Stand

- Code: `Online/OnlineRefunds.cs`, `Online/OnlineRefundEndpoints.cs`, `IOnlinePaymentProvider.RefundAsync`, `PosService.CloseAfterVoidAsync`; Tests in `OnlineRefundTests` und `StripePaymentTests`.
- Nachtrag (PR #161): Auch Anzahlungen und Vorauszahlungen ([ADR 0047](0047-anzahlung-und-vorauszahlung.md)) werden nur über das Storno ihres Anzahlungsbelegs erstattet. Bei einer rechtzeitigen Online-Absage storniert die Queue den Beleg selbst; ein schon beim Kassieren verrechneter Anzahlungsbeleg lässt sich erst nach dem Storno des Behandlungsbelegs stornieren.
- Offen: Teilerstattung, Erstattung auf anderem Weg, Fehlmeldungen des Anbieters nach der Annahme (`charge.refund.updated`).

## Quellen

- [ADR 0010](0010-kassa-compliance-by-design.md), [ADR 0036](0036-kartenhinterlegung-und-ausfallgebuehr.md)
- [PR-Protokoll](../umsetzung/pr-protokoll.md)
