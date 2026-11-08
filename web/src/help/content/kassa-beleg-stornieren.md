---
art: ablauf
aufbau: schritte
bereich: Kassa
oberflaeche: windows-app
rollen: rezeption, leitung, inhaber, manager
seite: kassa
stichwoerter: Storno, stornieren, Beleg stornieren, Rechnung stornieren, rückgängig, falsch kassiert, Rückerstattung, Geld zurück, Fehlbuchung, Stornobeleg, void, refund, cancel receipt
---
# Beleg stornieren

Ein falscher Beleg wird nicht gelöscht, sondern mit einem Stornobeleg aufgehoben. Der Stornobeleg nennt einen Grund und wird wie jeder Beleg signiert.

## Voraussetzungen {#voraussetzungen}

- Inhaber und Standortleitung stornieren jeden Verkaufsbeleg (einen Anzahlungsbeleg nur Inhaber und Manager). Die Rezeption storniert nur eigene Belege und nur bis 15 Minuten nach dem Kassieren, siehe [Wer darf stornieren](hilfe:kassa-beleg-stornieren#rechte).
- Der Beleg ist ein Verkaufsbeleg, noch nicht storniert, und ein mit ihm verkaufter Gutschein ist noch nicht eingelöst.

## Schritte {#schritte}

### 1. Belege öffnen

In der Kassa oben **Belege** wählen.

![Kassa mit markiertem Reiter Belege](images/app/beleg-stornieren-1.png)

### 2. Beleg wählen

Links den Beleg anklicken.

![Kassa mit markierter Liste der Belege von heute](images/app/beleg-stornieren-2.png)

### 3. Grund eintragen

Rechts unter „Beleg stornieren“ den Grund eintragen, z. B. „falsche Behandlung erfasst“ (mindestens drei Zeichen).

![Kassa mit markiertem Feld für den Grund des Stornos](images/app/beleg-stornieren-3.png)

### 4. Stornieren

Auf **Stornieren** klicken.

![Kassa mit markiertem Knopf Stornieren](images/app/beleg-stornieren-4.png)

## Was danach {#was-danach}

- Der Stornobeleg erscheint oben in der Liste mit „Storno“, der alte Beleg trägt „storniert“. Mit automatischem Druck wird der Stornobeleg gleich gedruckt.
- Die Rechnung ist wieder offen und steht links unter „Geparkte Rechnungen“. Ihre Positionen lassen sich neu bezahlen, ändern oder entfernen, oder die Rechnung **Verwerfen**.
- Eingelöste Gutscheine bekommen ihren Wert zurück. Mit dem Beleg verkaufte Gutscheine werden ungültig.
- Wurde am angebundenen Kartenterminal bezahlt, schickt die Kassa die Gutschrift von selbst an das Terminal. Bar Bezahltes gibt man aus der Lade zurück.

## Wenn etwas schiefgeht {#wenn-etwas-schiefgeht}

- „Stornieren darf die Standortleitung. Eigene Belege kann die Rezeption bis 15 Minuten nach dem Kassieren selbst stornieren.“ Dann die Leitung stornieren lassen.
- Nur Verkaufsbelege lassen sich stornieren, und jeder nur einmal. Bei Stornobelegen und stornierten Belegen fehlt „Beleg stornieren“.
- Ist von einem mit dem Beleg verkauften Gutschein schon etwas eingelöst, geht kein Storno: „Gutschein … wurde schon eingelöst; der Beleg kann nicht storniert werden.“
- Eine noch nicht bezahlte Rechnung storniert man nicht, man verwirft sie: [Rechnung parken und verwerfen](hilfe:kassa-rechnung-parken#verwerfen).



## Online bezahlte Belege {#online}

Unter „Belege“ nennt die Karte über **Beleg stornieren**, was online bezahlt wurde und über welchen Zahlungsanbieter. Ein Storno gibt das Geld über den Anbieter zurück und schickt dem Gast den Stornobeleg. Danach zeigt die Karte den Stand der Erstattung.

Steht dort „Nicht erstattet: …“, klickt die Kassenaufsicht auf **Erstattung erneut anstoßen**.

## Wer darf stornieren {#rechte}

- Inhaber und Standortleitung stornieren jeden Beleg, **außer einen Anzahlungsbeleg**: den stornieren nur Inhaber und Manager.
- Die Rezeption storniert nur eigene Belege und nur bis 15 Minuten nach dem Kassieren. Sonst kommt „Stornieren darf die Standortleitung. Eigene Belege kann die Rezeption bis 15 Minuten nach dem Kassieren selbst stornieren.“ Dann die Leitung stornieren lassen.

## Gut zu wissen {#hinweise}

- Hat im Spa jemand eine Vollmacht **Kassa-Storno**, auch eine abgelaufene, braucht jede Rolle außer Inhaber und Manager eine heute gültige. Sonst lehnt die App ab und nennt den Grund. Die Vollmacht vergibt die Leitung.
- Ein **Anzahlungsbeleg** (die Einzahlung einer Anzahlung auf einen Termin) lässt sich nur als Inhaber oder Manager stornieren. Bei Standortleitung und Rezeption ist **Stornieren** gesperrt, der Tooltip nennt den Grund („Anzahlungsbelege storniert nur die Leitung.“), und der Server lehnt den Aufruf mit 403 ab. Üblicher Weg ist der Knopf **Anzahlung retournieren …** (mit Pflichtgrund): [Anzahlung retournieren](hilfe:kassa-anzahlung-retournieren).
- Nur Verkaufsbelege lassen sich stornieren, und jeder nur einmal. Bei Stornobelegen und stornierten Belegen fehlt „Beleg stornieren“.
- Ist von einem mit dem Beleg verkauften Gutschein schon etwas eingelöst, geht kein Storno: „Gutschein … wurde schon eingelöst; der Beleg kann nicht storniert werden.“
- Eine noch nicht bezahlte Rechnung storniert man nicht, man verwirft sie: [Rechnung parken und verwerfen](hilfe:kassa-rechnung-parken#verwerfen).
