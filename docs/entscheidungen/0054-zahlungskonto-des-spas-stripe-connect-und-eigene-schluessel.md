# 0054. Zahlungskonto des Spas: Stripe Connect als Standard, eigene Schlüssel als Alternative

- **Status:** angenommen
- **Datum:** 19.10.2026
- **Entschieden von:** Auftraggeber (Auswahl „Beides“ im Projekt-Chat), Ausgestaltung Claude

## Kontext

Auftraggeber: „Die Firmendaten bzw. SPA-Studio-Daten müssen einstellbar sein … für Zahlungen, Anbindungen, Impressum usw.“ und später „Ich sehe keine Auswahl.“ Bisher gab es für Online-Zahlungen (Gutscheine, hinterlegte Karten, Ausfallgebühren) nur einen Stripe-Schlüssel in der Konfiguration des Servers. Auf einem gemeinsamen Server wären damit alle Zahlungen aller Spas auf einem Konto gelandet, also bei AISPA statt beim Spa. Das darf nicht sein: AISPA soll kein fremdes Geld halten (sonst Zahlungsdienst mit Lizenzpflicht), und jedes Spa muss seine Umsätze selbst vereinnahmen.

## Optionen

| Option | Bewertung |
| --- | --- |
| Nur eigene Stripe-Schlüssel je Spa | geht sofort, aber das Spa muss Schlüssel und Webhook im Stripe-Dashboard selbst einrichten; fehleranfällig |
| Nur Stripe Connect | bequem, braucht aber die Plattform-Freischaltung bei Stripe; ohne sie geht gar nichts, und Spas mit bestehendem Konto samt Entwickler müssten umstellen |
| Beides: Connect als Standard, eigene Schlüssel als Alternative | gewählt |

## Entscheidung

- Jedes Spa hat ein Zahlungskonto (Tabelle `PaymentAccounts`, eine Zeile je Spa) mit einer von drei Arten: Stripe Connect, eigene Stripe-Schlüssel oder keine Online-Zahlung. Eingestellt wird es in den Stammdaten unter „Zahlungen“, im Browser und in der Windows-App (ADR 0053), mit dem Recht „Unternehmen verwalten“.
- **Stripe Connect** (Standard, wenn der Server als Plattform eingerichtet ist: `OnlineVouchers:Stripe:Connect=true` mit dem Plattform-Schlüssel): „Mit Stripe verbinden“ legt über die Queue ein Standard-Konto an und holt einen Einrichtungslink; Stripe fragt Firmendaten und Bankkonto selbst ab. Zahlungen laufen als direkte Zahlungen auf dem verbundenen Konto (`Stripe-Account`), das Geld gehört dem Spa, Stripe rechnet die Gebühren mit dem Spa ab. Bereit ist das Konto, sobald Stripe `charges_enabled` meldet (Abfrage über die Queue, nach der Rückkehr von Stripe und bei `account.updated` über den Connect-Webhook).
- **Eigene Schlüssel:** geheimer Schlüssel (`sk_…` oder eingeschränkt `rk_…`) und Webhook-Geheimnis (`whsec_…`) des Spas. Sie liegen nur verschlüsselt in der Datenbank (Data Protection, gebunden an Spa und Zweck) und kommen nie zurück, nur ihr Anfang und Ende. Nach dem Speichern prüft die Queue den Schlüssel bei Stripe (`GET v1/account`); kennt Stripe ihn nicht, ist das Konto nicht bereit. Der Webhook des Spas ist `/api/hooks/payments/stripe/{Spa}`; er meldet nur Zahlungen von Standorten dieses Spas, damit niemand mit seinem eigenen Geheimnis eine Zahlung eines anderen Spas bestätigen kann.
- **Konto des Servers:** Ist der Server nicht als Plattform eingerichtet, aber mit eigenem Schlüssel (`Connect=false`), gilt dieser für Spas ohne Zahlungskonto. Das ist der Einzelbetrieb, in dem der Server dem Spa gehört. Auf einer Plattform zahlt der Plattform-Schlüssel nie selbst.
- Ohne bereites Konto verkauft der Kundenbereich keine Gutscheine und verlangt keine Ausfallgebühr; mit `PaymentProvider=Test` (Entwicklung) wird bis dahin nur simuliert. `PaymentProvider=None` schaltet Online-Zahlung ganz ab.
- Alle Aufrufe an Stripe laufen über die Queue, nie im HTTP-Aufruf. Jede Online-Zahlung bekommt weiterhin ihren Beleg an der Kassa (ADR 0010).

## Begründung

Connect ist für ein Spa ohne Technikwissen der kürzeste Weg und hält das Geld rechtlich sauber beim Spa. Die eigenen Schlüssel decken Spas ab, die schon ein Stripe-Konto mit eigener Einrichtung haben, und funktionieren auch, solange die Plattform-Freischaltung bei Stripe noch fehlt. Direkte Zahlungen statt „Destination Charges“: Das Spa ist Händler gegenüber dem Gast und trägt Rückbuchungen selbst, AISPA bleibt reiner Softwareanbieter.

## Folgen und Stand

- Code: `Online/PaymentAccounts.cs`, `Online/StripeConnect.cs`, `Online/PaymentAccountEndpoints.cs`, Anpassungen in `StripePayments.cs` und den Handlern; Oberfläche `web/src/admin/PaymentsTab.tsx`.
- Wechselt ein Spa das Konto, gehören hinterlegte Karten und frühere Zahlungen zum alten Konto; Belastung und Erstattung über AISPA gehen dann nicht mehr, nur im Stripe-Dashboard. Die Oberfläche warnt vor dem Wechsel.
- Schlüssel sind an den Schlüsselring des Servers gebunden. Online-Zahlungen laufen dort, wo der Kundenbereich und der Webhook erreichbar sind; auf einen anderen Server abgeglichen, gelten sie als nicht hinterlegt und müssen dort neu eingetragen werden.
- Offen: Stripe-Freischaltung als Connect-Plattform (Vertrag mit Stripe, Plattform-Profil) und echter Durchlauf mit einem Testkonto; das braucht Zustimmung des Auftraggebers, weil es eine Vereinbarung mit Stripe ist. Weitere Anbieter (Mollie, Adyen) bleiben über `IOnlinePaymentProvider` möglich.

## Quellen

- Stripe: Connect-Kontotypen (Standard), direkte Zahlungen mit `Stripe-Account`, Account Links, Webhooks für verbundene Konten.
- ADR 0010 (Kassa-Compliance), ADR 0052 (Firmendaten), ADR 0053 (Verwaltung in der Windows-App).
