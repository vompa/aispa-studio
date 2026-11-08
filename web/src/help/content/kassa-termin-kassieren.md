---
art: ablauf
aufbau: schritte
bereich: Kassa
oberflaeche: windows-app
rollen: rezeption, leitung, inhaber, manager
seite: kassa
stichwoerter: Termin kassieren, Behandlung abrechnen, abkassieren, Gast zahlt, Rechnung erstellen, Termin bezahlen, auschecken, Kassieren-Knopf, Taste K, checkout, bill appointment, pay treatment
---
# Termin an der Kassa abrechnen

Ein Termin kommt mit Behandlung und Preis auf die Rechnung, entweder direkt aus dem Kalender oder aus der Liste „Heute abzurechnen“ in der Kassa.

## Voraussetzungen {#voraussetzungen}

- Rezeption, Standortleitung oder Inhaber; ohne Verbindung zum Server geht Kassieren nicht.
- Der Termin ist eingecheckt oder erledigt, sonst fehlt **Kassieren** im Kalender, siehe [Status setzen](hilfe:kalender-status-setzen#einchecken).
- Gehört der Termin zu einem anderen Standort, wählt die Kassa die Kasse dieses Standorts, sofern es dort eine gibt.

## Schritte {#schritte}

### 1. Termin anklicken

Im Kalender den Termin anklicken.

![Kalender mit markiertem Termin von Markus Steiner](images/app/termin-kassieren-1.png)

### 2. Kassieren wählen

Auf **Kassieren** klicken. Alternativ den Termin mit der rechten Maustaste anklicken und **Kassieren** wählen, oder den mit der Tastatur markierten Termin mit der Taste **K** kassieren.

![Terminfenster mit markiertem Knopf Kassieren](images/app/termin-kassieren-2.png)

### 3. Rechnung prüfen

Die App wechselt zur Kassa (immer auf den Reiter **Kassieren**, auch wenn zuletzt „Belege“ offen war) und legt eine Rechnung für den Gast an, mit dem Termin als erster Position. Weitere Positionen ergänzen, falls nötig (z. B. ein Produkt oder [ein Rabatt](hilfe:kassa-rabatt-geben)).

![Kassa mit markierter offener Rechnung des Gastes](images/app/termin-kassieren-3.png)

### 4. Bezahlen

Die Beträge der Zahlungsarten eintragen und auf **Bezahlen und Beleg erstellen** klicken, siehe [Bezahlen](hilfe:kassa-bezahlen).

![Kassa mit markiertem Knopf Bezahlen und Beleg erstellen](images/app/termin-kassieren-4.png)

## Was danach {#was-danach}

Der Beleg wird am Server signiert und im Kassenjournal gespeichert. Der Termin bleibt im Kalender, kassieren ändert seinen Status nicht; mehr zum Weg aus dem Kalender steht unter [Kassieren im Kalender](hilfe:kalender-kassieren). Am Ende des Tages folgt der [Tagesabschluss](hilfe:kassa-tagesabschluss).

## Wenn etwas schiefgeht {#wenn-etwas-schiefgeht}

- Ein Termin kann nur auf einer Rechnung stehen. Sonst kommt „Dieser Termin steht schon auf einer Rechnung.“
- Eine falsche Position entfernt das × rechts daneben. **Verwerfen** löscht die ganze Rechnung, solange nichts darauf bezahlt ist; die Termine stehen danach wieder unter „Heute abzurechnen“.
- Ein bezahlter Beleg mit Fehler lässt sich [stornieren](hilfe:kassa-beleg-stornieren).



## Aus „Heute abzurechnen“ {#heute-abzurechnen}

1. In der Navigation **Kassa** öffnen, oben ist **Kassieren** gewählt. Der Cursor steht im Suchfeld „Gast, Code oder Artikel“.
2. Unter „Wartet auf Abrechnung“ den Termin anklicken, oder den Namen des Gastes tippen, mit **Pfeil nach unten** die Zeile wählen und **Enter** drücken.
3. Der Termin steht nun auf der offenen Rechnung neben der Liste.
4. [Bezahlen](hilfe:kassa-bezahlen).

![Kassa mit der Liste Heute abzurechnen](images/app/kassa-heute-abzurechnen.png)

- „Wartet auf Abrechnung“ zeigt die fertigen Termine, die noch auf keiner Rechnung stehen, mit „fertig seit …“; „Später heute“ die übrigen Termine und Eintritte des Tages an diesem Standort, auch solche, die noch nicht eingecheckt sind. Stornierte Termine fehlen.
- Ein Termin mit bezahlter Anzahlung steht weiter in der Liste, bis der Rest kassiert ist; die Anzahlung zieht die Kassa auf der Rechnung ab.
- Eine geparkte Rechnung nimmt ihre Termine aus der Liste; sie stehen unter „Geparkte Rechnungen“. Bezahlte Termine stehen zugeklappt unter „Erledigt“.
- Sind alle abgerechnet, steht dort „Alle Termine und Eintritte von heute sind abgerechnet.“

## Mehrere Termine auf eine Rechnung {#mehrere-termine}

Ist schon eine Rechnung offen, kommt jeder weitere angeklickte Termin auf dieselbe Rechnung, z. B. wenn ein Gast für die Begleitung mitbezahlt. Für getrennte Rechnungen die erste zuerst bezahlen oder [parken](hilfe:kassa-rechnung-parken).

## Gut zu wissen {#hinweise}

- Hat der Gast in der Kundenkartei einen [dauerhaften Rabatt](hilfe:kunden-rabatt), zieht die Kassa ihn selbst ab; die Position zeigt dann „Kundenrabatt … %“.
- Läuft für die Behandlung eine Aktion, zeigt die Kassa unten bei der Summe den Vorschlag „Aktion … anwenden“; der Rabatt wird erst mit **Anwenden** gesetzt: [Aktion anwenden](hilfe:kassa-aktion-anwenden).
- Ein Termin kann nur auf einer Rechnung stehen. Sonst kommt „Dieser Termin steht schon auf einer Rechnung.“
- Eine falsche Position entfernt das × rechts daneben. **Verwerfen** löscht die ganze Rechnung, solange nichts darauf bezahlt ist; die Termine stehen danach wieder unter „Heute abzurechnen“.
- Ohne Verbindung zum Server geht Kassieren nicht.
- Siehe auch [Rabatt geben](hilfe:kassa-rabatt-geben) und [Eintritt abrechnen](hilfe:kassa-eintritt-verkaufen).
