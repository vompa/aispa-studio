# 0028. Digitaler Euro als Zahlungsart vorbereitet, ausgeschaltet

- **Status:** angenommen
- **Datum:** 18.10.2026
- **Entschieden von:** Auftraggeber (Frage), Umsetzung Claude

## Kontext

Auftraggeber: „Kann man den digitalen EUR schon irgendwie vorbereiten?“ Die Ausgabe durch die EZB ist frühestens 2029 zu erwarten, das Regelwerk ist noch nicht fertig.

## Optionen

| Option | Bewertung |
| --- | --- |
| Abwarten | später Umbau an Kasse und Export |
| Zahlungsart und Konto vorbereiten, Schalter aus | gewählt |

## Entscheidung

`PaymentMethod.DigitalEuro`, Schalter `Payments:DigitalEuro` (aus), Buchhaltungskonto `DigitalEuroAccount` (Standard 1362), nicht für Schweizer Kassen. Zahlungsart-Logik zentral in `Pos/PaymentMethods.cs`.

## Begründung

Kasse, Belege und Export kennen die Zahlungsart schon; die Abwicklung über Terminal oder Zahlungsdienstleister kommt mit dem EZB-Regelwerk.

## Folgen und Stand

PR #21.

## Quellen

- [Funktionen, Kassa](../features.md#kassa)
