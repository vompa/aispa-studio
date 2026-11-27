---
art: ablauf
bereich: Lager
oberflaeche: verwaltung
rollen: rezeption, leitung, inhaber, manager
seite: verwaltung
stichwoerter: Wareneingang, Lieferung, Ware eingegangen, Zugang buchen, Einkauf, Charge, Ablaufdatum, MHD, Lieferschein, goods receipt, delivery, receive stock, batch, expiry
---
# Wareneingang buchen

Mit einem Handscanner geht es schneller: [Mit dem Handscanner arbeiten](hilfe:lager#scanner).

Ein Wareneingang erhöht den Bestand am gewählten Standort, im Hauptlager. Kam die Ware zu einer Bestellung, bucht man sie besser dort als Lieferung: [Bestellen und Lieferung buchen](hilfe:lager-bestellen#lieferung).

## Schritte {#schritte}

1. In der Verwaltung **Lager** öffnen und bei mehreren Standorten oben den **Standort** wählen.
2. Auf **Bewegung buchen** klicken. In der Seitenleiste ist der Reiter **Wareneingang** vorgewählt.
3. Bei **Material** das Material wählen oder den Namen eintippen.
4. Bei mehreren Lagerorten ist bei **Lagerort** das Hauptlager vorbelegt; nur ändern, wenn die Ware gleich an einen anderen Ort kommt.
5. **Menge** in der Einheit des Materials eintragen.
6. Optional **Preis je Einheit (€, netto)**, **Charge** und **Ablaufdatum** eintragen, dazu einen **Grund**.
7. Auf **Buchen** klicken. Unten erscheint kurz „Gebucht“.

## In Packungen oder Gebinden erfassen {#gebinde}

Kennt das Material Packung oder Gebinde, erscheint das Feld **Erfasst in**.

1. Bei **Erfasst in** die Ebene wählen, z. B. „Karton (4 × 500 ml)“.
2. Bei **Menge** die Anzahl Kartons eintragen. Unter dem Feld steht, was das in der Einheit des Materials ergibt, z. B. „= 2.000 ml“.
3. Der **Preis** gilt je Karton. Das Lager rechnet ihn in den Preis je Einheit um.
4. Auf **Buchen** klicken. Unten erscheint kurz die gebuchte Menge, z. B. „Gebucht: 2.000 ml“.

## Gut zu wissen {#hinweise}

- Die Menge muss größer als 0 sein.
- Mit Eingangspreis schreibt das Lager den Einstandspreis als gleitenden Durchschnitt über den Bestand aller Standorte fort. Ohne Preis bleibt er unverändert.
- Mit Ablaufdatum steht die Charge rechtzeitig im Reiter **Zu tun** unter „Was ist zu tun“ und in der Ansicht [Chargen](hilfe:lager-ablauf-aufgaben) des Reiters **Bestand**. Von dort verteilt man sie mit [Nachfüllen](hilfe:lager-nachfuellen) auf andere Lagerorte; Charge und Ablaufdatum wandern mit.
- Charge und Ablaufdatum gibt es nur beim Wareneingang.
