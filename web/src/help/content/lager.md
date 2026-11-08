---
art: einstellung
bereich: Lager
oberflaeche: verwaltung
rollen: rezeption, leitung, inhaber, manager
seite: verwaltung
---
# Lager

Das Lager führt den Bestand an Verbrauchsmaterial und Verkaufsprodukten je Standort und je Lagerort (Hauptlager, Schrank eines Raums, Verkaufsregal): [Lagerorte](hilfe:lager-lagerorte). Verbrauch durch Behandlungen und Verkäufe an der Kassa bucht das Programm selbst; von Hand kommen Wareneingang, Nachfüllen, Inventur, Korrektur und Umbuchung.

## Übersicht {#uebersicht}

Der Reiter **Übersicht** öffnet das Lager und sagt, was zu tun ist. Jeder Punkt hat einen Link in den Reiter, in dem man ihn erledigt.

![Die Übersicht des Lagers mit „Was ist zu tun“](images/app/verwaltung-lager-uebersicht.png)

- **Positionen nachbestellen:** so viele Positionen stehen im Bestellvorschlag. Der Link **Bestellen öffnen** führt in den Reiter **Bestellen**.
- **Material unter dem Mindestbestand** und **Material, das nur noch für wenige Behandlungen reicht** (höchstens 5): beide führen in den **Bestand**.
- **Chargen, die bald ablaufen** oder abgelaufen sind. Der Link **Chargen öffnen** führt in den Reiter **Chargen**.

Darunter steht klein der Lagerwert des Standorts. Den Punkt „nachbestellen“ und die Links zu **Bestellen** und **Chargen** sehen nur Personen mit dem Recht für den Standort; die Rezeption sieht die Übersicht, den Bestand, die Bewegungen und im Reiter **Bestellen** nur die Bedarfsprüfung.

## Bestand lesen {#bestand}

Der Reiter **Bestand** zeigt vier Spalten: **Material**, **Bestand** mit einem Balken gegen den Mindestbestand (voll bei dem Doppelten des Mindestbestands), **Reicht für** (Behandlungen laut Richtwert des Materials, bei gewähltem Lagerort mit der Menge an diesem Ort) und **Status**. Unter der Überschrift steht klein der Lagerwert.

![Der Bestand mit vier Spalten und dem Seitenpanel einer Zeile](images/app/verwaltung-lager-bestand.png)

- Ein Klick auf eine Zeile öffnet rechts das Seitenpanel mit den übrigen Werten: Einheit, Mindestbestand, Verbrauch geplant, Bestellt, Bestellvorschlag, Läuft ab, Wert und die Mengen je Lagerort. Dort stehen auch **Buchen**, **Nachfüllen** und **Ändern**.
- Bei **Alle Lagerorte** ist der Bestand die Summe des Standorts. Mit einem gewählten **Lagerort** zeigt er nur dessen Bestand, Mindestmenge und Wert; „Verbrauch geplant“, „Bestellt“ und „Bestellvorschlag“ gelten nur für den ganzen Standort und entfallen dort.
- Der Chip **nachbestellen** steht, wenn der Bestand unter der Mindestmenge liegt oder nur noch für wenige Behandlungen reicht; **abgelaufen**, wenn eine Charge abgelaufen ist. **Nur unter Minimum** zeigt nur Zeilen unter der Mindestmenge.
- Das Feld **Material suchen oder scannen** über dem Reiter filtert den Bestand beim Tippen nach dem Namen; tippt man in der Übersicht, wechselt das Lager zum Bestand.

## Material anlegen {#material-anlegen}

Legt ein neues Material an. Es gilt für alle Standorte; den Bestand führt das Lager je Standort. Anlegen dürfen Inhaber und Standortleitung, nur sie sehen diesen Abschnitt. **Ändern** in der Zeile des Materials lädt es in dasselbe Formular.

### Bezeichnung {#bezeichnung}

Name des Materials, zum Beispiel „Massageöl Lavendel“. Pflicht, höchstens 200 Zeichen. Unter diesem Namen erscheint es im Bestand, in Bestellungen und in der Rezeptur einer Behandlung.

### Einheit {#einheit}

In welcher Einheit gezählt wird, zum Beispiel ml, g oder Stück. Pflicht, höchstens 20 Zeichen. Vorbelegt ist „Stück“. Alle Mengen dieses Materials, auch in der Rezeptur einer Behandlung, gelten in dieser Einheit.

### Einstandspreis je Einheit (netto) {#einstandspreis}

Was eine Einheit im Einkauf kostet, ohne Umsatzsteuer. Ab 0, höchstens vier Nachkommastellen; leer zählt als 0.

- Daraus rechnet das Lager den Lagerwert und die Seite „Kosten“ die Materialkosten einer Behandlung.
- Wird beim Wareneingang ein Preis angegeben, schreibt das Lager den Einstandspreis als gleitenden Durchschnitt über den Bestand aller Standorte fort.

### Mindestbestand je Standort {#mindestbestand}

Wie viel an jedem Standort mindestens vorrätig sein soll, in der Einheit des Materials. Ab 0; leer zählt als 0. Der Wert gilt für jeden Standort gleich und für die Summe aller seiner Lagerorte. Mindestmengen je Lagerort stehen bei den [Lagerorten](hilfe:lager-lagerorte#mindestmengen).

- Liegt der Bestand darunter, ist die Zeile im Bestand hervorgehoben („unter Mindestbestand“).
- Fällt der erwartete Stand bis zur Lieferung darunter, schlägt das Lager eine Bestellung vor: [So rechnet der Vorschlag](hilfe:lager-bestellen#rechnung).

### Verkaufsprodukt der Kassa {#verkaufsprodukt}

Verknüpft das Material mit einem Artikel der Kassa. Vorbelegt ist „keines“.

- Jeder Verkauf dieses Artikels bucht die verkaufte Stückzahl als Einheiten des Materials am Standort der Kassa ab, und zwar vom ersten aktiven Verkaufsregal des Standorts, ohne Verkaufsregal vom Hauptlager. Ein Storno bucht an denselben Lagerort zurück.
- Ein Artikel kann nur mit einem Material verknüpft sein.

### Lieferant {#lieferant}

Bei wem das Material bestellt wird. Freiwillig. Zur Auswahl stehen die aktiven Lieferanten aus dem Reiter **Lieferanten**: [Lieferanten pflegen](hilfe:lager-lieferanten).

- **Bestellung anlegen** legt je Lieferant eine Bestellung mit allen vorgeschlagenen Mengen an. Material ohne Lieferant kommt dort nicht hinein.

### Artikelnummer beim Lieferanten {#artikelnummer}

Freiwillig, höchstens 50 Zeichen. Steht im Bestelltext vor der Bezeichnung, damit der Lieferant das Material eindeutig erkennt.

### Barcode {#barcode}

Freiwillig, 3 bis 50 Zeichen ohne Leerzeichen, in Ihrem Spa nur für ein Material. Das ist der Code auf der Verpackung, meist eine EAN. Mit dem Code finden Handscanner das Material im Lager und an der Kassa: [Mit dem Handscanner arbeiten](hilfe:lager#scanner). Die Prüfziffer wird nicht geprüft, denn auch Lieferantencodes sind erlaubt.

### Packung und Gebinde {#packung}

Wie das Material geliefert wird, zum Beispiel Flasche mit 500 ml im Karton mit 4 Flaschen. Freiwillig. Packung und Menge je Packung gehören zusammen, ebenso Gebinde und Packungen je Gebinde; ein Gebinde braucht eine Packung.

- Der Wareneingang lässt sich dann in Packungen oder Gebinden erfassen. Gebucht wird immer in der Einheit des Materials: [Wareneingang buchen](hilfe:lager-wareneingang-buchen#gebinde).

### Menge je Behandlung {#behandlung}

Richtwert in der Einheit des Materials, zum Beispiel 15 ml. Freiwillig, größer als 0. Der Bestand nennt dann, für wie viele Behandlungen er reicht. Abgebucht wird weiter nach der Rezeptur der Behandlung.

### Ablaufwarnung in Tagen {#ablaufwarnung}

Wie viele Tage vor dem Ablauf eine Charge dieses Materials im Reiter **Chargen** steht. Von 1 bis 365, leer zählt als 30: [Bald ablaufende Chargen abarbeiten](hilfe:lager-ablauf-aufgaben).

## Mit dem Handscanner arbeiten {#scanner}

Ein USB-Handscanner tippt den Code wie eine Tastatur und schließt mit Enter ab. Sie brauchen keine Einstellung. Klicken Sie in das Feld **Material suchen oder scannen** über dem Reiter und scannen Sie. Dasselbe Feld filtert den Bestand, wenn Sie tippen und nicht Enter drücken.

- Der Bestand wählt die Zeile des Materials. Mit dem Recht zu buchen öffnet sich zugleich die Buchung **Wareneingang**, und der Cursor steht im Feld **Menge**.
- In der laufenden **Inventur** springt der Cursor in das Feld **Gezählt** der Zeile. Ohne gestartete Inventur erscheint ein Hinweis.
- Gehört der Code zu keinem Material, fragt das Lager, ob Sie ein Material **anlegen** oder dem Code ein vorhandenes Material **zuordnen** wollen. Beim Anlegen steht der Code schon im Feld **Barcode**. Beim Zuordnen wählen Sie die Zeile des Materials und klicken im Seitenpanel auf **Ändern**; der Code steht danach im Formular, und Sie speichern.

Scannen an der Kassa: [Artikel verkaufen](hilfe:kassa-artikel-verkaufen#scannen).

## Bedarfsprüfung {#bedarfspruefung}

Rechnet den Verbrauch der gebuchten, noch offenen Termine eines Zeitraums gegen den Bestand am gewählten Standort. Gezählt wird das Material im Rezept der Behandlungen (Kosten) und die Mengen bei „Zum Termin bereitstellen“. Die Prüfung bucht nichts; abgebucht wird weiter erst, wenn eine Behandlung abgeschlossen ist. Sehen dürfen sie alle, die buchen dürfen.

Die Bedarfsprüfung steht im Reiter **Bestellen**. Mit **Zeitraum** wählt man heute, die nächsten 7 oder die nächsten 14 Tage. Darunter steht eine Zeile **Hinweise zur Bedarfsprüfung**, die man aufklappt, und eine Tabelle mit Bestand, Bedarf, „Fehlt“, Terminen und „Danach noch für“ (für wie viele weitere Behandlungen der Rest reicht):

- „Bestand … reicht nicht für alle gebuchten Termine“, mit der Zeit, ab der es fehlt.
- „Nach den gebuchten Terminen bleiben … unter der Mindestmenge“.
- „Reicht … noch für N Behandlungen“, ab höchstens drei weiteren Behandlungen.

Beim Buchen eines Termins erscheint dieselbe Warnung, wenn der neue Termin eine dieser Grenzen reißt. Es ist nur ein Hinweis, die Buchung bleibt gültig. Geprüft werden dabei die nächsten 14 Tage; was weiter entfernt liegt, wird nicht gemeldet, weil bis dahin nachbestellt werden kann. Bestellte, noch nicht gelieferte Ware zählt nicht mit.

## Verbrauch (Soll und Ist) {#verbrauch}

Zeigt je Material, wie viel laut Rezept verbraucht sein sollte und wie viel die Inventuren tatsächlich ergeben haben. So fällt Schwund auf. Der Reiter **Verbrauch** erscheint für Personen mit dem Recht, Kennzahlen des Betriebs zu sehen.

Mit **Zeitraum** wählt man die letzten 7, 30 oder 90 Tage bis heute. Die Tabelle nennt je Material:

- **Soll:** der Verbrauch, der laut Rezept der gebuchten Behandlungen entstanden ist.
- **Ist:** das Soll plus der Mehrverbrauch.
- **Mehrverbrauch:** was die Inventuren im Zeitraum darüber hinaus als Schwund gefunden haben. Eine negative Zahl heißt, die Inventuren fanden mehr als erwartet.
- **Wert:** der Mehrverbrauch zum Einstandspreis. Den Wert zeigt der Bericht nur mit dem Recht „Berichte mit Beträgen“; ohne das Recht steht ein Hinweis.

Unter jedem Material stehen die Behandlungen, die es verbrauchen, mit ihrem Soll und der Zahl der Termine. Korrekturen mit Grund, z. B. Bruch, und Verkäufe an der Kassa zählen nicht mit. Ohne Inventur im Zeitraum gibt es keinen Mehrverbrauch. Der Bericht bucht nichts.

## Charge zurückverfolgen {#rueckverfolgung}

Bei einem Rückruf zeigt diese Liste über alle Standorte, wer Material einer Charge in einer Behandlung oder beim Kauf bekommen hat. Verbrauch und Verkauf buchen zuerst die Charge, die zuerst abläuft. Die Liste steht im Reiter **Bewegungen**; die App zeigt sie Inhabern und der Standortleitung.

1. Bei **Material der Charge** das Material wählen.
2. Bei **Chargennummer** die Nummer eintragen.
3. Auf **Zurückverfolgen** klicken.

Oben steht, wie viel erhalten und verwendet wurde und wie viele Kundinnen und Kunden betroffen sind. Darunter steht jede Buchung mit Zeit, Standort, Art, Menge, Kunde und Behandlung oder Beleg. Steht „Zu dieser Charge ist nichts gebucht.“, stimmen Material oder Nummer nicht oder die Charge wurde nie erfasst. Die Liste findet nur Chargen, die beim Wareneingang eingetragen wurden.
