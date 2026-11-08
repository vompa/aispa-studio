# 0035. Test-TSE für Entwicklung und Vorführung, DSFinV-K aus den Belegen

- **Status:** angenommen
- **Datum:** 18.10.2026
- **Entschieden von:** Claude (autonom, siehe ADR 0019)

## Kontext

Deutschland ist der zweite Markt ([ADR 0003](README.md)). Ohne TSE lässt sich keine deutsche Kasse anlegen, und für die Kassen-Nachschau und Außenprüfung verlangt das Finanzamt die DSFinV-K. Eine zertifizierte Cloud-TSE (fiskaly, Deutsche Fiskal, Swissbit) braucht einen Vertrag; bis dahin soll die deutsche Kasse trotzdem durchgehend vorführbar und testbar sein.

## Optionen

| Option | Bewertung |
| --- | --- |
| Warten, bis ein TSE-Vertrag steht | Deutschland bleibt ungetestet; Fehler im Belegformat fallen erst spät auf |
| Sandbox eines Anbieters | braucht Zugangsdaten und Netz, in der CI und im Container schwierig |
| Eigene Test-TSE hinter `ITseClient` | gewählt: gleiche Schnittstelle wie eine echte TSE, ohne Vertrag und Netz |

## Entscheidung

- `TestTseClient` signiert mit ECDSA P-256 (`ecdsa-plain-SHA256`), zählt Transaktionen und Signaturen je Kasse und speichert den Schlüssel in der Datenbank. Eingeschaltet nur mit `Fiscal:TestTse` (in `appsettings.Development.json` an, sonst aus).
- Die Kasse warnt dauerhaft „Nur Test-TSE angebunden“, solange die Test-TSE aktiv ist.
- Vorgangsart `Kassenbeleg-V1`, Vorgangsdaten und QR-Code nach DSFinV-K, Zeiten als `utcTime`.
- `GET /api/pos/registers/{id}/dsfinvk?from&until` erzeugt die DSFinV-K als ZIP je Tagesabschluss aus den gespeicherten Belegen (Einzelaufzeichnung, Stamm- und Kassenabschlussmodul, `index.xml`). Nur mit Recht `ViewFinance`, höchstens ein Jahr.

## Begründung

Die Test-TSE hält Belegformat, QR-Code und Export schon jetzt in den Tests, ohne dass ein Vertrag nötig ist. Die echte TSE ersetzt später nur `ITseClient`; die Kasse und der Export bleiben gleich.

## Folgen und Stand

- Code: `Pos/Fiscal/TestTse.cs`, `Pos/Fiscal/KassenSichVFiscalization.cs`, `Pos/Fiscal/Dsfinvk.cs`; Tests in `KassenSichVTests`.
- TSE-Export (TAR) über `ITseClient.ExportAsync`, angefordert über die Queue (PR #56). Die Test-TSE schreibt dafür ein Protokoll je Transaktion; ihre Protokolldateien enthalten Text statt ASN.1.
- Offen: Vertrag und Adapter für eine zertifizierte Cloud-TSE, Prüfung des Exports mit dem Prüfprogramm der Finanzverwaltung (AmadeusVerify bzw. IDEA) und durch die Steuerberatung, ELSTER-Meldung der Kasse.

## Quellen

- DSFinV-K 2.4, BSI TR-03153, § 146a AO, KassenSichV
- [PR-Protokoll](../umsetzung/pr-protokoll.md)
