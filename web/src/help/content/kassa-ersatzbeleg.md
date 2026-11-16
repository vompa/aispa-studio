---
art: ablauf
bereich: Kassa
oberflaeche: windows-app
rollen: rezeption, leitung, inhaber, manager
seite: kassa
stichwoerter: Ersatzbeleg, ohne Verbindung, offline, Netzausfall, Ausfall, Internet weg, Server nicht erreichbar, Nacherfassung, nacherfassen, Zweitschrift, Notbetrieb, outage, substitute receipt
---
# Ohne Verbindung kassieren: Ersatzbeleg

Erreicht die App den Server nicht, kassieren Sie trotzdem. Der Gast bekommt einen Ersatzbeleg. Sobald die Verbindung wieder da ist, erstellt die Kasse daraus von selbst den signierten Beleg.

Das gilt für den Betrieb mit Server oder Haupt-PC. Im Einzelplatz liegt die Kasse auf demselben PC; dort gibt es keinen Ersatzbeleg.

## Kassieren {#kassieren}

1. In der Kassa den Termin unter „Heute abzurechnen“ wählen oder im Kalender am Termin **Kassieren** wählen. Artikel kommen wie gewohnt dazu.
2. Unter „Bezahlen“ steht der Hinweis „Ersatzbeleg“.
3. Den Betrag bei **Bar gegeben** oder **Karte** eintragen. Mit Karte kassieren Sie am eigenständigen Kartengerät.
4. Auf **Bezahlen und Beleg erstellen** klicken.

![Kassa ohne Verbindung mit dem Hinweis „Ersatzbeleg“](images/app/kassa-ersatzbeleg-bezahlen.png)

Die Kassa zeigt den Ersatzbeleg mit eigener Nummer, z. B. „Ersatzbeleg E-WIEN-1-20261019-114000-26“, und dem Vermerk „Ersatzbeleg, der signierte Beleg folgt“. Mit **Drucken** kommt er aus dem Bondrucker.

![Ersatzbeleg nach dem Bezahlen ohne Verbindung](images/app/kassa-ersatzbeleg.png)

- Ein noch nicht eingecheckter Termin wird nach der Rückfrage „Einchecken und kassieren?“ eingecheckt; das Einchecken wird vorgemerkt.
- Trinkgeld tragen Sie wie sonst bei **Trinkgeld** ein.
- Der Ersatzbeleg nennt Beträge inklusive Umsatzsteuer. Steuersatz, Belegnummer und Signatur stehen erst auf dem signierten Beleg.

## Was ohne Verbindung nicht geht {#geht-nicht}

- Gutscheine verkaufen oder einlösen, Pakete, Rabatte und Anzahlungen.
- Das angebundene Kartenterminal und die Sofortüberweisung.
- Rechnung parken, Beleg stornieren, Rechnung mit Empfänger, Tagesabschluss.
- Der Barzahlungsrabatt gilt ohne Verbindung nicht.

## Nacherfassung {#nacherfassung}

Die App prüft die Verbindung von selbst. Ist der Server wieder erreichbar, sendet sie jeden Ersatzbeleg einzeln an die Kasse. Dort entsteht ein gewöhnlicher signierter Beleg mit dem Vermerk „Nacherfassung zu Ersatzbeleg … vom …“. Sie finden ihn unter **Belege**.

- Der Ersatzbeleg bleibt verschlüsselt auf diesem Gerät, bis er nacherfasst ist. Er übersteht Abmelden und Neustart.
- Derselbe Ersatzbeleg wird nie zweimal gebucht.
- Der Gast kann den signierten Beleg später bekommen: unter **Belege** öffnen und drucken oder per Mail senden.

## Wenn die Kasse einen Ersatzbeleg nicht übernimmt {#abgelehnt}

Stimmt etwas nicht überein, bucht die Kasse nichts und die App zeigt oben „Beim Abgleich nicht übernommen“ mit dem Ersatzbeleg und dem Grund. Das Geld ist kassiert, deshalb lässt sich der Eintrag nicht abhaken.

1. Den Grund lesen, z. B. „Dieser Termin steht schon auf einer Rechnung.“ oder „Der Ersatzbeleg … nennt 89,00 €, an der Kasse ergeben die Positionen 79,00 €“.
2. Die Ursache an der Kassa klären, z. B. eine doppelt angelegte Rechnung verwerfen.
3. Auf **Erneut senden** klicken.

- Häufige Gründe: Der Termin wurde an einem anderen Gerät schon kassiert, der Preis wurde inzwischen geändert, oder auf dem Termin liegt eine Anzahlung.
- Lässt sich der Grund nicht beheben, den Umsatz an der Kassa von Hand nacherfassen und den Ersatzbeleg aufbewahren. Fragen Sie dazu Ihre Steuerberatung.

## Länger ohne Verbindung {#laenger}

- In Österreich ist ein Ausfall der Registrierkasse von mehr als 48 Stunden über FinanzOnline zu melden.
- Die App erinnert daran: Ist der älteste Ersatzbeleg seit mehr als 48 Stunden nicht nacherfasst, steht im Hinweis „Keine Verbindung zum Server“ zusätzlich „Der älteste Ersatzbeleg ist vom … und seit mehr als 48 Stunden nicht nacherfasst“.
- Nach der Nacherfassung zeigt die Kassa eine Woche lang „Die Kasse war länger als 48 Stunden nicht erreichbar“ mit Beginn und Ende des Ausfalls. Diese beiden Zeitpunkte brauchen Sie für die Meldung.
- Bewahren Sie die gedruckten Ersatzbelege auf, bis die Nacherfassung durch ist.
- Siehe auch [Arbeiten ohne Verbindung](hilfe:kalender-offline) und [Bezahlen](hilfe:kassa-bezahlen).
