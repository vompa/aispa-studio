# 0034. Kartenterminal über die Queue, Geräte im Netz über die Windows-App

- **Status:** angenommen
- **Datum:** 18.10.2026
- **Entschieden von:** Claude (autonom, siehe ADR 0019)

## Kontext

Die Kassa soll Kartenzahlungen am Terminal auslösen, statt den Betrag nur abzutippen. Terminals in Day-Spas sprechen meist ZVT (Deutschland, Österreich) oder OPI (Schweiz) im lokalen Netz am Empfang; einige Anbieter haben Cloud-Schnittstellen. Der Server in der Cloud erreicht ein Gerät im Netz am Empfang nicht direkt.

## Optionen

| Option | Bewertung |
| --- | --- |
| Server spricht direkt mit dem Gerät | geht nur mit Portfreigabe oder VPN je Spa, unsicher und aufwendig |
| Nur Cloud-Anbieter | bindet Spas an wenige Anbieter, ihre vorhandenen Geräte fallen weg |
| Server führt die Zahlung als Auftrag, die Windows-App am Empfang spricht mit dem Gerät | gewählt |

## Entscheidung

- Eine Kartenzahlung ist ein eigener Datensatz zur offenen Rechnung (`CardPayment`). Der Auftrag geht über die Wolverine-Queue an eine Umsetzung von `ICardTerminal`.
- `Test`: Testterminal ohne Gerät für Prototyp und Tests. `Local`: Gerät im Netz am Empfang; die Windows-App holt Aufträge ab (`GET /api/pos/terminals/{id}/jobs`) und meldet das Ergebnis (`POST /api/pos/card-payments/{id}/result`). Cloud-Anbieter kommen als weitere Umsetzung dazu.
- Kassiert wird mit der genehmigten Zahlung (`PaymentInput.CardPaymentId`): genau einmal, in voller Höhe, für dieselbe Rechnung.
- Was das Terminal meldet, zählt. Eine Genehmigung nach einem Abbruch an der Kasse wird gespeichert. Genehmigtes Geld ohne Beleg sperrt den Tagesabschluss, bis kassiert oder am Terminal storniert ist.
- Die Karte an einem eigenständigen Gerät nur zu erfassen, bleibt möglich.

## Begründung

So braucht kein Spa eine Portfreigabe, vorhandene Geräte bleiben nutzbar, und der HTTP-Aufruf wartet nie auf das Gerät. Dass genehmigtes Geld nicht ohne Beleg bleiben kann, folgt aus ADR 0010: Umsätze laufen immer über die Kasse.

## Folgen und Stand

- Code: `src/AispaStudio.Api/Pos/Terminals/`, Weboberfläche `web/src/pos/BillPanel.tsx`.
- ZVT in der Windows-App seit PR #59 (`src/AispaStudio.Desktop/Services/Zvt`, `TerminalAgent`): nur der unter „Geräte“ gewählte PC holt Aufträge ab, jeder Auftrag geht höchstens einmal ans Gerät.
- Offen: Händlerbeleg und Kundenbeleg vom Terminal, Cloud-Anbieter. Die Gutschrift am Terminal beim Storno eines Belegs kam mit #98, OPI als zweites Protokoll mit #101.

## Quellen

- [ADR 0010](0010-kassa-compliance-by-design.md)
- [PR-Protokoll](../umsetzung/pr-protokoll.md)
