# 0047. Anzahlung und Vorauszahlung mit Anzahlungsbeleg der Kassa

- **Status:** angenommen
- **Datum:** 18.10.2026
- **Entschieden von:** Claude, autonom nach ADR 0019

## Kontext

Die Kartenhinterlegung ([ADR 0036](0036-kartenhinterlegung-und-ausfallgebuehr.md)) schützt vor No-Shows, ohne dass vor der Leistung Geld fließt. Die [ergänzenden Themen](../konzepte/ergaenzende-themen.md) (Abschnitt 5) verlangen zusätzlich Anzahlung und Vorauszahlung je Behandlung oder für neue Gäste. Sobald Geld vor der Leistung fließt, ist es ein Umsatz: In Österreich ist die Anzahlung bei Zahlung umsatzsteuerpflichtig (Mindest-Istbesteuerung, § 19 Abs. 2 Z 1 lit. a UStG) und als Barumsatz mit signiertem Beleg zu erfassen (RKSV). In Deutschland gilt dasselbe nach § 13 Abs. 1 Nr. 1 lit. a Satz 4 UStG und § 146a AO. An der Kassa vorbei darf nichts laufen ([ADR 0010](0010-kassa-compliance-by-design.md)).

## Optionen

| Option | Bewertung |
| --- | --- |
| Zahlung nur beim Anbieter festhalten, Beleg erst beim Kassieren | Geld fließt ohne Beleg und ohne Umsatzsteuer zum Zahlungszeitpunkt; nicht zulässig |
| Anzahlung als Gutschein ausgeben und beim Kassieren einlösen | Mehrzweckgutscheine werden erst beim Einlösen versteuert, eine Anzahlung aber schon bei Zahlung; falsch |
| Anzahlungsbeleg an der Kassa mit dem Steuersatz der Behandlung, beim Kassieren als Abzug mit gleichem Satz | gewählt |

## Entscheidung

- Je Standort eine Anzahlung in Prozent des Preises (`DepositPercent`, 0 = keine, 100 = Vorauszahlung), je Behandlung abweichend (`Treatment.DepositPercent`, leer = Regel des Standorts, 0 = keine). Die Regel „nur neue Gäste“ des Standorts gilt auch für die Anzahlung.
- Eine Anzahlung ersetzt die Kartenhinterlegung für diesen Termin. Sie gilt nur mit Zahlungsanbieter und aktiver Kasse am Standort; sonst gilt wie bisher die Ausfallgebühr mit Karte.
- Der Gast stimmt beim Buchen den Bedingungen zu (dasselbe Feld wie bei der Ausfallgebühr) und bezahlt danach über den Anbieter. Mit Frist (`CardHoldHours`) wird ein unbezahlter Termin freigegeben.
- Nach der Zahlung erstellt die Queue an der Kassa des Standorts eine Rechnung mit der Position „Anzahlung …“ (neue Art `BillLineKind.Deposit`, Steuersatz und Erlöskonto der Behandlung), bezahlt mit „Karte“ und der Referenz des Anbieters. Der Beleg ist der Anzahlungsbeleg; der Gast bekommt ihn per Mail. Die Kassa merkt sich die Anzahlung (`AppointmentDeposit`).
- Die Rezeption kann eine Anzahlung auch selbst kassieren (Position „Anzahlung“ auf einen gebuchten Termin, beliebige Zahlart, kein Rabatt, höchstens der Preis).
- Beim Kassieren der Behandlung zieht die Kassa die Anzahlung als negative Zeile „Anzahlung verrechnet (Beleg …)“ mit dem Steuersatz des Anzahlungsbelegs ab. Die Behandlung wird so nur einmal versteuert, und der Beleg weist den Rest aus.
- Rechtzeitige Online-Absage: Die Queue storniert den Anzahlungsbeleg, und die Erstattung folgt wie in [ADR 0038](0038-rueckerstattung-nur-nach-storno.md). Bei „Nicht erschienen“ oder einer späten Absage bleibt die Anzahlung. Soll der Gast sie trotzdem zurückbekommen, storniert die Kassenaufsicht den Anzahlungsbeleg. Sagt das Team ab, erstattet es ebenso über das Storno.
- Ein verrechneter Anzahlungsbeleg lässt sich erst stornieren, wenn der Beleg der Behandlung storniert ist. Danach steht die Anzahlung wieder zur Verrechnung bereit.

## Begründung

Der Anzahlungsbeleg erfüllt die Belegpflicht zum Zahlungszeitpunkt und versteuert die Anzahlung, wenn sie zufließt. Der Abzug mit demselben Steuersatz beim Kassieren hält die Steuer insgesamt richtig, ohne Sonderlogik im Tagesabschluss. Storno als einziger Weg zur Erstattung gilt schon für Gutscheine und Ausfallgebühren; Buchhaltung und Anbieter bleiben dadurch deckungsgleich.

## Folgen und Stand

- Code: `Online/Deposits.cs`, `Online/DepositHandlers.cs`, `Online/DepositEndpoints.cs`, `PosService` (Art `Deposit`, Abzug beim Kassieren, Storno-Regeln), `Pos/PosEntities.cs` (`AppointmentDeposit`); Tests in `DepositTests`.
- Die Rechnung zeigt `DepositCredit` gesamt und je Position. Beim Teilen der Rechnung wird die Anzahlung von der Position des Termins abgezogen.
- Eine Zahlung, die nach Freigabe oder Absage noch eingeht, bekommt trotzdem einen Beleg und wird dann über das Storno erstattet.
- Offen: Teilerstattung einer Anzahlung, Erinnerung vor Ablauf der Zahlungsfrist, Zahlungslink per SMS, Anzahlung für Termine der Rezeption per Link.

## Quellen

- [Ergänzende Themen, Abschnitt 5](../konzepte/ergaenzende-themen.md)
- [ADR 0010](0010-kassa-compliance-by-design.md), [ADR 0036](0036-kartenhinterlegung-und-ausfallgebuehr.md), [ADR 0038](0038-rueckerstattung-nur-nach-storno.md)
- § 19 Abs. 2 Z 1 lit. a UStG (AT), § 13 Abs. 1 Nr. 1 lit. a Satz 4 UStG (DE), RKSV, § 146a AO
- [PR-Protokoll](../umsetzung/pr-protokoll.md)
