# Übergabe Thread „Kassa“ (Kassa, Finanzen, Lager)

Stand 2026-10-18, 21:40 UTC. Gestoppt auf Anweisung des Auftraggebers. Branch `claude/kassa-yhmip3`.

## Halb fertig

- **PR #91 „Rückverfolgung je Charge bei Rückruf“ ist offen, nicht gemergt.** Auf GitHub ist keine CI gestartet (keine Check-Runs auf Head `84e57c8`). Lokal geprüft: Format, Release-Build, Web (Lint, Typecheck, 138 Tests, Build, Bundle) und Backend (360 Tests) grün.
- Im Backend-Gesamtlauf schlug einmal `CardGuaranteeTests.Online_Termin_ohne_Karte_wird_nach_der_Frist_freigegeben` fehl. Der Test hängt an einer Frist und berührt #91 nicht. Danach lief er dreimal grün, mit und ohne die Änderung. Vermutlich zeitabhängig, sollte robuster werden.
- Sonst keine offenen Branches oder lokalen Änderungen.

## Gebaut und gemergt

| PR | Inhalt |
| --- | --- |
| #5, #7 | Kassa Backend und Web: Rechnung, Beleg, RKSV signiert (Test-Signatureinrichtung AT0), DEP |
| #21 | Digitaler Euro vorbereitet, ausgeschaltet (`Payments:DigitalEuro`) |
| #28 | Schweiz: 5-Rappen-Rundung nur auf den Baranteil, Bondruck Text/ESC-POS |
| #33 | Recht Kassenaufsicht: Storno fremder oder älterer Belege, Entnahmen |
| #39 | Kartenterminal über die Queue und die Empfangs-App |
| #43 | Deutschland: Test-TSE, DSFinV-K-Export |
| #46 | KI-Tagesstart mit Zahlenprüfung |
| #48, #64, #65 | Buchhaltungsexporte Schweiz (Abacus, bexio), RZL, Kontenzuordnung je Land |
| #52, #67 | Lager aus Bewegungen, Lieferantenbestellungen mit Teillieferung |
| #56 | TSE-Export (TAR) über die Queue |
| #71 | Rechnung zum Beleg für Firmenkunden, Firmendaten je Standort |
| #73 | Preise nach Wochentag und Uhrzeit |
| #77 | Chargen und Ablaufdaten am Wareneingang |
| #81 | UID-Prüfung über VIES mit Abfragenummer als Nachweis |
| #87 | Eintritte in Bereiche (Sauna, Ruheraum) an der Kassa abrechnen |

Details zu jedem PR stehen in `docs/umsetzung/pr-protokoll.md`.

## Entscheidungen unterwegs

- Kassenrecht hinter `IFiscalization` je Land; Echtbetrieb ersetzt nur die Signatureinrichtung bzw. den TSE-Client (ADR 0010 Compliance by Design).
- Kartenterminal nie im HTTP-Aufruf, sondern über die Queue; die Windows-App holt Jobs ab (ADR 0034).
- Test-TSE nur mit `Fiscal:TestTse`, ohne TSE keine deutsche Kasse (ADR 0035).
- Lagerbestand ist die Summe unveränderlicher Bewegungen, Korrektur nur als neue Bewegung (ADR 0037).
- Rückerstattung nur nach Storno (ADR 0038).
- Preis einer Buchung wird beim Buchen festgehalten; Verschieben ändert ihn nicht (#73).
- Chargen: „zuerst ablaufend, zuerst verbraucht“ für Anzeige (#77) und Abbuchung (#91); Storno und Rücknahme gehen in die ursprüngliche Charge zurück.
- UID-Prüfung freiwillig, über die Queue, nur EU (VIES); Nachweis muss zur UID gehören und gültig sein (#81).
- Eintritte in Bereiche: Preis, Steuersatz und Erlöskonto kommen von einem Kassa-Artikel am Bereich, keine eigenen Felder; Bereich ohne Artikel ist nicht abrechenbar (#87).
- System schickt nichts an Lieferanten oder Dritte von selbst.

## Nächste offene Punkte, in meiner Reihenfolge

1. PR #91 mergen, sobald CI läuft (oder lokal geprüft nach der Ausfallregel).
2. Zeitabhängigen Test in `CardGuaranteeTests` robust machen.
3. Schweizer UID über das UID-Register des BFS prüfen.
4. Preisregeln nach Wochentag und Uhrzeit auch für Eintritte und Kassa-Artikel.
5. DSFinV-K und TSE-Export mit dem Prüfprogramm der Finanzverwaltung prüfen.
6. Rechnung mit Empfänger in der Windows-App (Windows-Thread hat #85 dazu gemergt, abgleichen).
7. Lager: Charge bei Korrekturen von Hand angeben, Liste der Rückverfolgung exportieren.

## Überlegungen für später

- **RKSV (AT):** Für den Echtbetrieb eine zertifizierte Signatureinrichtung (Karte oder HSM eines Vertrauensdiensteanbieters) hinter `IRksvSignatureDevice`, Registrierung der Kasse bei FinanzOnline, Prüfung des Startbelegs.
- **TSE (DE):** Echte Cloud- oder Hardware-TSE hinter `ITseClient`; Meldung der Kassen nach § 146a Abs. 4 AO über ELSTER.
- **Schweiz:** Keine Signaturpflicht, aber Aufbewahrung und MWST-Abrechnung; CH-Exporte mit echtem Mandanten prüfen.
- **Kassa offline in der Windows-App:** lokale Belegnummern und Nachsignatur bzw. Ersatzbelege nach RKSV (ADR 0005).
- **Rückruf:** Mail an betroffene Kundinnen und Kunden aus der Rückverfolgung, nur nach Freigabe und über die Queue.
- **Lager:** Bestellung direkt an Lieferanten per Mail oder Schnittstelle, Wareneingang per Lieferschein-Foto.
- **Kartenterminal:** Terminalbelege (Gutschrift beim Storno: #98, OPI: #101).
- Main bewegt sich schnell: vor jedem Merge main einmischen; Konflikte in `pr-protokoll.md` und `stand-und-offene-punkte.md` sind normal, Migrationen dann neu erzeugen; ADR-Nummern können parallel kollidieren.
