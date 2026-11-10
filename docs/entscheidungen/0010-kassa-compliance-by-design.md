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

Nachtrag (09.11.2026, Gutschein-Fristen): Beim Verkauf von Gutscheinen (Kassa und Online-Shop) belegt die API ein Ablaufdatum nach dem Land der Kasse vor (`VoucherExpiry`, Kaufdatum = Ortsdatum des Standorts). Der Auftraggeber hat die Regel am 09.11.2026 freigegeben:

- **Deutschland:** Wertgutschein, Behandlungsgutschein und Paket bis 31.12. des dritten Jahres nach dem Kaufjahr (Kauf 09.11.2026 → 31.12.2029). Laut Verbraucherzentrale und IHK gelten drei Jahre ab Jahresende; eine Frist von einem Jahr hat das OLG München (29 U 3193/07) gekippt.
- **Schweiz:** fünf Jahre ab dem Kaufdatum (Art. 127/128 OR laut law.ch).
- **Österreich:** kein Ablaufdatum. Laut WKO und AK Oberösterreich gelten 30 Jahre (OGH 7 Ob 22/12d); fünf Jahre nur mit Auszahlungsmöglichkeit.

Das Datum ist am Verkauf änderbar und darf leer bleiben (unbefristet). Ein Datum vor der Frist des Landes verbietet die Kassa nicht, sie zeigt einen Hinweis; in Österreich bei jedem Datum „In Österreich sind Fristen unter 5 Jahren ohne Auszahlungsmöglichkeit unwirksam.“ Beleg, Gutschein und Mail nennen „Einzulösen bis TT.MM.JJJJ“; das Einlösen prüft das Datum. Schon verkaufte Gutscheine bleiben unverändert.

Quellen (abgerufen am 09.11.2026; die genannten Urteile wurden nicht im Volltext geprüft):

- Verbraucherzentrale, „Gutscheine als Geschenk: Gerichte verlangen lange Fristen“: <https://www.verbraucherzentrale.de/wissen/vertraege-reklamation/kundenrechte/gutscheine-als-geschenk-gerichte-verlangen-lange-fristen-13861>
- IHK Kassel-Marburg, „Rechtsgrundlage bei Gutscheinen“ (drei Jahre ab Jahresende, ein Jahr von OLG München 29 U 3193/07 gekippt): <https://www.ihk.de/kassel-marburg/beratung-service/recht/wirtschaftsrecht/allgemeines-vertragsrecht/rechtsgrundlage-bei-gutscheinen-4057952>
- WKO, „Gutscheine – Befristung“: <https://www.wko.at/vertragsrecht/gutscheine-befristung>
- Arbeiterkammer Oberösterreich, „Gutscheine“ (30 Jahre, OGH 7 Ob 22/12d, fünf Jahre nur mit Auszahlung): <https://ooe.arbeiterkammer.at/beratung/konsumentenschutz/konsumentenrecht/konsumentenrecht/Gutscheine.html>
- law.ch, „Geschenkgutscheine: Geltendmachung und Gültigkeitsdauer“ (Art. 127/128 OR, fünf Jahre): <https://law.ch/lawnews/2024/01/geschenkgutscheine-geltendmachung-gueltigkeitsdauer/>

Die Regel soll vor dem Einsatz anwaltlich geprüft werden.
