---
art: seite
bereich: Lager
oberflaeche: verwaltung
rollen: inhaber, manager, leitung, rezeption
seite: verwaltung
stichwoerter: Lager, Bestand, Lagerbestand, Lagerort, Hauptlager, Material, Verbrauchsmaterial, Produkte, Warenwirtschaft, Bestellung, Mindestbestand, Ablaufdatum, Charge, nachfüllen, Schrank, Export, Excel, CSV, Bestand exportieren, Inventurliste, inventory, stock, warehouse, supplies
---
# Seite „Lager“

In der Windows-App steht das Lager unter „Verwaltung“ › „Lager“. Dort sind **Übersicht**, **Bestand**, **Bestellen** und **Inventur** nativ, dazu die Seite **Einrichtung** mit **Lagerorte** und **Lieferanten**. Auch das Buchen von Lieferungen, die Bedarfsprüfung und **Charge zurückverfolgen** gibt es dort.

Das Lager zeigt je Standort den Bestand an Verbrauchsmaterial und Verkaufsprodukten mit Wert, geplantem Verbrauch und Bestellvorschlag, auf Wunsch je Lagerort (Hauptlager, Schrank eines Raums, Verkaufsregal). Verbrauch durch Behandlungen und Verkäufe an der Kassa bucht das Programm selbst; von Hand kommen Wareneingang, Nachfüllen, Umbuchen, Korrektur und Inventur.

## Wer was sieht {#rechte}

- **Rezeption**: sieht Übersicht, Bestand (Ansichten **Material** und **Bewegungen**) und im Reiter **Bestellen** die Bedarfsprüfung (ohne Bestellvorschlag und Bestellungen) und bucht Wareneingang, Nachfüllen, Umbuchen und Korrektur über **Bewegung buchen**, an ihrem Standort.
- **Standortleitung**: zusätzlich die Reiter **Bestellen** und **Inventur**, im Bestand die Ansicht **Chargen**, die **Einrichtung** mit **Lagerorte** und **Ausbuchen** ablaufender Chargen, an ihren eigenen Standorten. Mit dem Recht auf den Katalog auch **Material anlegen** und in der **Einrichtung** die **Lieferanten**.
- **Inhaber**: alles an allen Standorten.

## Aufbau {#aufbau}

Oben stehen die vier Reiter **Übersicht**, **Bestand**, **Bestellen** und **Inventur**. Rechts daneben, bei schmalem Fenster in der Zeile darunter, stehen das Feld **Material suchen oder scannen**, bei mehr als einem Standort die Auswahl **Standort**, **Exportieren**, **Einrichtung**, das Fragezeichen und **Bewegung buchen**. Im Bestand steht außerdem die Auswahl **Ansicht**, daneben das Feld **Lagerort** („Alle Lagerorte“ zuerst), das für Material und Bewegungen gilt.

- **Übersicht**: oben Kennzahl-Kacheln (**Lagerwert**, **Nachbestellen**, **Unter Minimum**, **Chargen laufen ab**, mit dem Recht auf Kennzahlen des Betriebs **Mehrverbrauch**), darunter „Was ist zu tun“ mit Links und der Abschnitt **Verbrauch** (Soll und Ist). Mehr dazu: [Übersicht](hilfe:lager#uebersicht).
- **Bestand, Ansicht Material**: Material, Bestand mit Balken, „Reicht für“ und Status; die übrigen Werte stehen im Seitenpanel einer Zeile. Darunter **Material anlegen**. Mehr dazu: [Bestand lesen](hilfe:lager#bestand).
- **Bestand, Ansicht Chargen**: bald ablaufende Chargen, die Abverkaufsliste und Abgelaufenes zum Ausbuchen.
- **Bestand, Ansicht Bewegungen**: alle Zu- und Abgänge im gewählten Zeitraum mit Art, Material, Lagerort, Menge, Preis und Grund; darunter **Charge zurückverfolgen**.
- **Bestellen**: **Bedarfsprüfung**, Bestellvorschlag mit **Bestellung anlegen**, Entwürfe ändern und nach Bestätigung per Mail senden; Lieferungen kommen ins Hauptlager.
- **Inventur**: einen Lagerort zählen.
- **Einrichtung**: öffnet eine eigene Seite mit **Zurück**. Dort stehen **Lagerorte** (Lagerorte anlegen und pflegen, mit Mindestmengen) und **Lieferanten** (mit Kontakt, Lieferzeit, Mindestbestellwert und Bestellweg); mit beiden Rechten wählt man oben zwischen beiden. Wer keines der beiden Rechte hat, sieht die Schaltfläche nicht.

In der Kopfzeile öffnet **Exportieren** einen Dialog. Dort wählt man den **Inhalt** (Bestand je Lagerort, Lagerbewegungen, Bestellungen, Ablaufliste oder Inventur), das **Format** Excel oder CSV und bei den Lagerbewegungen den Zeitraum, zuerst die letzten 30 Tage. Der Dialog steht zuerst auf dem Inhalt, der zum offenen Reiter passt, und die App merkt sich das Format. Was es am Standort noch nicht gibt, ist gesperrt und nennt den Grund, z. B. „Noch keine Zählung an diesem Standort“; die Inventur nimmt die zuletzt eingereichte Zählung. Nach dem Speichern steht unten „Gespeichert: …“ mit **Ordner öffnen**. Den Knopf sehen nur Inhaberin und Geschäftsführung; jeder Export steht im Verlauf.

![Dialog „Lager exportieren“ mit fünf Inhalten, dem Format Excel oder CSV und dem Knopf Exportieren](images/app/lager-exportieren.png)

Der geplante Verbrauch kommt aus der Rezeptur der Behandlungen (Seite „Kosten“) für die gebuchten Termine der nächsten 14 Tage.

## Abläufe {#ablaeufe}

- [Lagerorte anlegen und Mindestmengen festlegen](hilfe:lager-lagerorte)
- [Material anlegen](hilfe:lager-material-anlegen)
- [Lieferanten pflegen](hilfe:lager-lieferanten)
- [Bald ablaufende Chargen abarbeiten](hilfe:lager-ablauf-aufgaben)
- [Wareneingang buchen](hilfe:lager-wareneingang-buchen)
- [Aus dem Hauptlager nachfüllen](hilfe:lager-nachfuellen)
- [Bestellen und Lieferung buchen](hilfe:lager-bestellen)
- [Inventur buchen](hilfe:lager-inventur)
- [Bruch, Schwund oder Abgelaufenes ausbuchen](hilfe:lager-korrektur-buchen)
- [An einen anderen Standort umbuchen](hilfe:lager-umbuchen)
