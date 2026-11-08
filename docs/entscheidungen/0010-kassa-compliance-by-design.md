# 0010. Eigene Kassa mit Compliance by Design, keine Umsatzverkürzung

- **Status:** angenommen
- **Datum:** 17.10.2026
- **Entschieden von:** Auftraggeber (Anforderung und Klarstellung), Claude (Ablehnung der Umsatzverkürzung)

## Kontext

Der Auftraggeber wollte zunächst „ca. 20 % des Geschäftes unversteuert erledigen“. Claude hat abgelehnt, weil schon die Herstellung solcher Software verboten ist (§ 146a, § 379 AO). Der Auftraggeber stellte klar, dass ein eigenes, versteuertes Konto gemeint war („das war ein Missverständnis“), und verlangte eine Kassa „in jeder Form: bar, Gutscheine, Kartenzahlung, Sofortüberweisung, Splitten der Rechnungen“ für DACH.

## Optionen

| Option | Bewertung |
| --- | --- |
| Fremde Kasse anbinden (ready2order, orderbird) | zwei Systeme, Gutscheine und Tagesrechnung brächen am Übergang |
| Ein fester Zahlungsanbieter | teurer für Kunden, häufige Kritik am Markt |
| Hardware-TSE | Cloud-TSE ist für mehrere Standorte einfacher |
| Eigene Kassa, jede Buchung signiert | gewählt |

## Entscheidung

Vollwertige Kassa direkt am Kalender. Jede Buchung über RKSV-Signatur (AT) bzw. TSE (DE), Schweizer Belege per Hash-Kette. Kein Löschen, nur Storno mit Begründung. Getrennte Geschäftsbereiche ausschließlich über eigene, voll versteuerte Erlöskonten und Kostenstellen. Funktionen zum Verbergen von Umsätzen werden nie gebaut.

## Begründung

Ohne erfüllte Kassenpflichten dürfen Kunden in DE und AT nicht verkaufen. Eine Funktion zum Verbergen von Umsätzen wäre strafbar und würde den Auftraggeber und seine Kunden haftbar machen.

## Folgen und Stand

Kassenrecht hinter `IFiscalization` je Land (PR #5, #7). Österreich vollständig mit Test-Signatureinrichtung (`AT0`), Echtbetrieb ersetzt nur `IRksvSignatureDevice`. Deutschland über `ITseClient` vorbereitet; ohne TSE keine deutsche Kasse. Offen: zertifizierte Signatureinrichtung/TSE, Kartenterminal, Belegdruck, CH-Rundung.
