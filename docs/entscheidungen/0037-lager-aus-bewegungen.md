# 0037. Lager aus Bewegungen, Verbrauch über die Queue

- **Status:** angenommen
- **Datum:** 18.10.2026
- **Entschieden von:** Claude (autonom, siehe ADR 0019)

## Kontext

Die Vollkosten je Behandlung rechnen Material bisher mit Mengen und Preisen, die an jeder Behandlung von Hand stehen. Spas wollen wissen, was im Regal liegt, was nachbestellt werden muss und was das Material wirklich kostet. Der Koordinator nannte „Materialbestand/Lager für die Vollkosten“ als nächsten Punkt für Kassa und Finanzen.

## Optionen

| Option | Bewertung |
| --- | --- |
| Bestand als Zahl am Artikel, bei jeder Buchung geändert | einfach, aber nicht nachvollziehbar und anfällig bei gleichzeitigen Buchungen |
| Bestand als Summe unveränderlicher Bewegungen | gewählt: jede Änderung hat Art, Zeit, Grund und Bezug (Termin, Beleg), Inventurdifferenzen sind sichtbar |
| Anbindung an ein Warenwirtschaftssystem | für Day-Spas bis 50 Mitarbeitende zu groß; als Export später möglich |

## Entscheidung

- Lagerartikel (`StockItem`) gelten für das ganze Spa, der Bestand je Standort ist die Summe der Bewegungen (`StockMovement`), die nie geändert oder gelöscht werden.
- Bewegungsarten: Wareneingang (gleitender Durchschnittspreis), Verbrauch (aus der Rezeptur, wenn ein Termin abgeschlossen ist), Verkauf und Storno (aus dem Kassenbeleg über den verknüpften Artikel), Inventur (bucht die Differenz zum gezählten Bestand), Korrektur (nur mit Grund) und Umbuchung zwischen Standorten.
- Verbrauch und Verkauf bucht nur das System, und zwar über die Wolverine-Queue: Terminänderungen (`AppointmentChangedMessage`) und Kassenbelege lösen einen Abgleich aus, der die Differenz zwischen Soll und schon Gebuchtem schreibt. Der Abgleich ist wiederholbar; ein Termin, der nicht mehr abgeschlossen ist, bucht zurück.
- Die Rezeptur einer Behandlung kann eine Zeile mit einem Lagerartikel verknüpfen; die Vollkosten nehmen dann dessen aktuellen Einstandspreis.
- Bestellvorschlag: Mindestbestand plus Verbrauch laut Rezeptur der gebuchten Termine der nächsten 14 Tage, abzüglich Bestand.

## Begründung

Bewegungen statt Zahl machen den Bestand prüfbar und vertragen gleichzeitige Buchungen ohne Sperren am Artikel. Die Queue hält Kasse und Kalender schnell, und ein Fehler im Lager kann weder den Kassenbeleg noch den Termin verhindern. Der Lagerwert ist eine Auswertung und keine Pflichtzahl der Kasse; die Kassa bleibt die einzige Quelle für Umsätze.

## Folgen und Stand

- Code: `src/AispaStudio.Api/Stock`, Seite „Lager“ in `web/src/stock`, Rezepturzeile mit Lagerartikel auf der Seite „Kosten“.
- Offen: Bestellungen an Lieferanten, Chargen und Ablaufdaten, Lagerbewertung für den Jahresabschluss, Wareneingang per Lieferschein-Foto mit KI.

**Nachtrag 21.10.2026: Lagerorte je Standort, siehe [ADR 0068](0068-lagerorte-je-standort.md).** Der Bestand je Standort bleibt die Summe der Bewegungen, jede Bewegung trägt aber zusätzlich einen Lagerort (Hauptlager, Raumschrank, Verkaufsregal); Bedarfsprüfung, Bestellvorschlag, Lagerwert und Vollkosten rechnen unverändert mit der Standortsumme.

## Quellen

- [Ergänzende Themen](../konzepte/ergaenzende-themen.md)
- README, Abschnitt „Lager“
