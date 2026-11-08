---
art: ablauf
aufbau: schritte
bereich: Kassa
oberflaeche: windows-app
rollen: rezeption, leitung, inhaber, manager
seite: kassa
stichwoerter: Tagesabschluss, Kassenabschluss, Z-Bericht, X-Bericht, Zwischenbericht, Kassensturz, Bargeld zählen, Kassa zählen, Differenz, Fehlbetrag, Überschuss, Soll-Bestand, end of day, cash count, Z report, closing
---
# Tagesabschluss

Am Ende des Tages zählt man das Bargeld in der Lade und speichert den Tagesabschluss (Z-Bericht). Er wird unveränderlich gespeichert und schließt den Zeitraum seit dem letzten Abschluss dieser Kasse ab.

## Voraussetzungen {#voraussetzungen}

- Jede Person, die kassieren darf, auch die Rezeption.
- Alle Zahlungen sind einem Beleg zugeordnet: Ist am Kartenterminal eine Zahlung genehmigt, aber keinem Beleg zugeordnet, geht kein Abschluss.
- Die richtige Kasse ist gewählt.

## Schritte {#schritte}

### 1. Kassenlade öffnen

In der Kassa oben **Kassenlade & Abschluss** wählen. Ist die richtige Kasse gewählt?

![Kassa mit markiertem Reiter Kassenlade und Abschluss](images/app/tagesabschluss-1.png)

### 2. Zwischenbericht ansehen

Links steht der „Zwischenbericht seit dem letzten Abschluss“: Zeitraum mit Zahl der Belege und Stornos, Umsatz, „Zahlungsarten“, „Steuern“, „Erlöskonten“ und „Bargeld in der Lade“ (Anfangsbestand, Bar eingenommen, Einlagen, Entnahmen) mit dem **Soll-Bestand**. Das Bargeld in der Lade zählen.

![Kassa mit markiertem Zwischenbericht seit dem letzten Abschluss](images/app/tagesabschluss-2.png)

### 3. Gezählten Bestand eintragen

Rechts unter „Tagesabschluss“ den „Gezählter Bestand“ eintragen. Darunter erscheint „Stimmt mit dem Soll überein“ oder z. B. „Differenz −1,50 €“. Bei einer Differenz in „Notiz, z. B. Grund einer Differenz“ den Grund eintragen.

![Kassa mit markiertem Feld Gezählter Bestand und angezeigter Differenz](images/app/tagesabschluss-3.png)

### 4. Tagesabschluss durchführen

Auf **Tagesabschluss durchführen** klicken.

![Kassa mit markiertem Knopf Tagesabschluss durchführen](images/app/tagesabschluss-4.png)

## Was danach {#was-danach}

Links steht nun „Tagesabschluss gespeichert“ mit „Keine Differenz“ oder der gespeicherten Differenz. Ein gespeicherter Abschluss lässt sich nicht ändern; fehlt danach noch eine Buchung, gehört sie in den nächsten Abschluss. Die Differenz bucht der Buchhaltungsexport auf die Konten für [Kassenfehlbetrag und -überschuss](hilfe:buchhaltung#kassenfehlbetrag). Wechselgeld für morgen: der gezählte Bestand bleibt als Anfangsbestand in der Lade; Geld zur Bank als Entnahme buchen: [Einlage oder Entnahme](hilfe:kassa-einlage-entnahme).

## Wenn etwas schiefgeht {#wenn-etwas-schiefgeht}

- „Kartenzahlung über … ist genehmigt, aber noch keinem Beleg zugeordnet: zuerst kassieren oder am Terminal stornieren.“ Erst den Beleg erstellen oder die Zahlung am Terminal stornieren, dann erneut abschließen.
- **Tagesabschluss durchführen** bleibt grau, solange der gezählte Bestand fehlt.
- Eine falsche Buchung vor dem Abschluss: den Beleg [stornieren](hilfe:kassa-beleg-stornieren).



## Zwischenstand ohne Abschluss {#zwischenstand}

Der Bericht links ist jederzeit ein Zwischenstand (X-Bericht): **Kassenlade & Abschluss** öffnen und ansehen, ohne etwas einzutragen. Ein erneuter Klick auf **Kassenlade & Abschluss** lädt den Stand neu.

## Gut zu wissen {#hinweise}

- Den Tagesabschluss darf jede Person machen, die kassieren darf, auch die Rezeption.
- Ist am Kartenterminal eine Zahlung genehmigt, aber noch keinem Beleg zugeordnet, geht kein Abschluss: „Kartenzahlung über … ist genehmigt, aber noch keinem Beleg zugeordnet: zuerst kassieren oder am Terminal stornieren.“
- Ein gespeicherter Abschluss lässt sich nicht ändern. Fehlt danach noch eine Buchung, gehört sie in den nächsten Abschluss.
- Wechselgeld für morgen: der gezählte Bestand bleibt als Anfangsbestand in der Lade. Geld zur Bank als Entnahme buchen: [Einlage oder Entnahme](hilfe:kassa-einlage-entnahme).
- Die Differenz bucht der Buchhaltungsexport auf die Konten für [Kassenfehlbetrag und -überschuss](hilfe:buchhaltung#kassenfehlbetrag).
