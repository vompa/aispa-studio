---
art: ablauf
bereich: Kassa
oberflaeche: windows-app
rollen: rezeption, leitung, inhaber, manager
seite: kassa
stichwoerter: bezahlen, Zahlung, bar, Bargeld, Karte, Bankomat, EC-Karte, Kreditkarte, Kartenterminal, Terminal, Überweisung, Sofortüberweisung, geteilt zahlen, Split, aufteilen, Rückgeld, Wechselgeld, pay, cash, card payment, split payment, change
---
# Bezahlen

Rechts in der Kassa unter „Bezahlen“ steht für jede Zahlungsart der Kasse ein Betragsfeld. Ein Beleg entsteht erst mit **Bezahlen und Beleg erstellen**; der Server signiert ihn und speichert ihn im Kassenjournal.

## Bar {#bar}

1. Bei **Bar gegeben** den Betrag eintragen, den der Gast hinlegt, z. B. `100`. Ein Klick in ein leeres Betragsfeld trägt schon den offenen Betrag ein und markiert ihn; Tippen ersetzt ihn. **Rest** trägt genau den offenen Betrag ein, auch in ein befülltes Feld.
2. Unten erscheint das Rückgeld, z. B. „Rückgeld: 11,00 €“.
3. Auf **Bezahlen und Beleg erstellen** klicken.

- Ist ein [Barzahlungsrabatt](hilfe:kassa#barzahlungsrabatt) eingestellt, steht unter der Summe z. B. „Bar 4,16 € günstiger (3 % Barzahlungsrabatt)“. Er gilt nur, wenn alles bar bezahlt wird; **Rest** bei „Bar gegeben“ zieht ihn dann schon ab.

## Karte am eigenständigen Gerät {#karte}

Ohne angebundenes Kartenterminal tippt man den Betrag am Kartengerät selbst ein und erfasst die Zahlung in der Kassa.

1. Den Betrag am Kartengerät kassieren und die Genehmigung abwarten.
2. In der Kassa bei **Karte** auf **Rest** klicken oder den Betrag eintragen.
3. Auf **Bezahlen und Beleg erstellen** klicken.

![Beleg nach dem Bezahlen mit Karte](images/app/kassa-beleg.png)

## Am angebundenen Kartenterminal {#kartenterminal}

Ist am Standort der Kasse ein Kartenterminal eingerichtet, steht unter dem Feld **Karte** der Knopf **Am Terminal zahlen**.

1. Bei **Karte** nichts eintragen (dann geht der offene Rest ans Terminal) oder einen Teilbetrag eintragen.
2. Auf **Am Terminal zahlen** klicken. Es erscheint „Bitte Karte am Terminal …“.
3. Der Gast zahlt am Terminal. Die Kassa zeigt dann z. B. „Genehmigt · Mastercard •••• 4711“ oder „Nicht genehmigt: …“.
4. Ist die Zahlung genehmigt, auf **Bezahlen und Beleg erstellen** klicken.

![Genehmigte Zahlung am Terminal](images/app/kassa-terminal.png)

- Solange das Terminal wartet, bricht **Abbrechen** die Zahlung ab. Eine genehmigte, noch nicht kassierte Zahlung storniert **Stornieren** am Terminal.
- Eine genehmigte Kartenzahlung muss auf einen Beleg: Parken, Verwerfen und eine andere Rechnung öffnen gehen erst, wenn bezahlt oder am Terminal storniert ist.
- Das Terminal wählt man unter „Geräte“ an dem PC, neben dem es steht: [Kartenterminal einrichten](hilfe:geraete-kartenterminal-einrichten).

## Sofortüberweisung {#sofortueberweisung}

1. Bei **Sofortüberweisung** auf **Zahlungsdaten anzeigen** klicken. Die Kassa trägt den Betrag ein (den Betrag des Felds, sonst den offenen Rest) und zeigt Empfänger, IBAN, Betrag und einen QR-Code, den der Gast mit seiner Banking-App scannt.
2. Der Gast überweist an diese Daten. Ist das Geld da, auf **Bezahlen und Beleg erstellen** klicken.

- Der QR-Code enthält genau die Daten der Kassa. Ändern Sie den Betrag, verschwindet er; klicken Sie dann erneut auf **Zahlungsdaten anzeigen**.
- Den Zahlungstext (EPC) kann man aus dem Feld darunter kopieren.
- Fehlt dem Konto der Kasse die Bankverbindung, nennt die Kassa den Grund und lässt das Feld leer.

## Über Treatwell bezahlt {#treatwell}

Hat der Gast eine Treatwell-Buchung schon vorab über Treatwell bezahlt, steht beim Kassieren dieses Termins ein grüner Hinweis: „Vorab über Treatwell bezahlt: 35,00 € (Buchung TW-…)“. Dazu erscheint das Feld **Über Treatwell bezahlt** mit dem vorab bezahlten Betrag; es erscheint nur bei solchen Terminen.

1. Den Termin wie gewohnt kassieren ([Termin kassieren](hilfe:kalender-kassieren)). Das Feld **Über Treatwell bezahlt** ist schon ausgefüllt, höchstens mit dem Preis des Termins.
2. Kostet die Behandlung mehr als vorab bezahlt, zahlt der Gast den Rest mit einer anderen Zahlungsart, z. B. bar bei **Bar gegeben**.
3. Auf **Bezahlen und Beleg erstellen** klicken.

- Der Betrag wird **nicht noch einmal vom Gast verlangt**, aber der Umsatz läuft ganz normal über die Kassa: Der Beleg nennt die volle Leistung mit Steuer und Erlöskonto und zeigt die Zahlungsart „Über Treatwell bezahlt“ mit der Buchungsnummer. Treatwell rechnet mit dem Spa ab; in der Buchhaltung steht der Betrag auf dem Konto „Treatwell (Forderung, vorab bezahlt)“, siehe [Kontenzuordnung](hilfe:buchhaltung#treatwell).
- Die Zahlungsart gilt nur für Termine mit Vorauszahlung und nur bis zu diesem Betrag; sonst meldet die Kassa „… keine Vorauszahlung über Treatwell“. Sie lässt sich je Termin nur einmal verwenden; nach einem Storno des Belegs wieder.
- Weicht der Preis bei Treatwell vom Preis der Behandlung ab, rechnet die Kassa mit dem Preis der Behandlung; der vorab bezahlte Betrag ist auf den Preis begrenzt. Eine Differenz beim Kassieren klären.

## Geteilt mit mehreren Zahlungsarten {#geteilt}

Ein Beleg kann mit mehreren Zahlungsarten bezahlt werden, z. B. ein Teil mit Gutschein, ein Teil mit Karte und der Rest bar.

1. Bei der ersten Zahlungsart den Teilbetrag eintragen, z. B. bei **Gutschein** `50` und den Code.
2. Bei der nächsten Zahlungsart auf **Rest** klicken oder einen weiteren Teilbetrag eintragen.
3. Unter den Feldern steht „Noch offen: …“, bis alles gedeckt ist, dann „Alles gedeckt“.
4. Auf **Bezahlen und Beleg erstellen** klicken.

- Rückgeld gibt es nur bar. Mit Karte, Sofortüberweisung oder Gutschein zusammen nicht mehr als den offenen Betrag eintragen, sonst kommt „Zu viel bezahlt: …“.
- Eine Rechnung auf einzelne Positionen oder Personen aufteilen (Teilbeträge) gibt es nicht. Für getrennte Zahlungen die Positionen auf getrennte Rechnungen setzen.

## Nach dem Bezahlen {#nach-dem-bezahlen}

Die Mitte zeigt „Bezahlt“ mit dem Beleg, der Signatur („Signiert nach RKSV“, in Deutschland „Signiert (TSE)“) und ausgestellten Gutscheinen. Ist unter „Geräte“ automatisches Drucken eingeschaltet, druckt der Bondrucker sofort; sonst klicken Sie auf **Drucken**. Ohne eingerichteten Bondrucker ist der Knopf grau, daneben steht der Hinweis auf „Geräte“. Haben Sie einen Empfänger erfasst, druckt er die Rechnung mit Empfänger. Für eine Firmenrechnung siehe [Rechnung mit Empfänger](hilfe:kassa-rechnung-mit-empfaenger). Das × im Kopf des Belegs schließt den Beleg.

## Gut zu wissen {#hinweise}

- **Bezahlen und Beleg erstellen** bleibt grau, solange etwas offen ist, ein Betrag nicht lesbar ist („Betrag nicht lesbar“), ein Gutschein-Code fehlt oder das Terminal noch wartet.
- Beträge mit Komma oder Punkt eingeben, z. B. `12,50` oder `12.50`.
- Steht „Sicherheitseinrichtung ausgefallen“ auf dem Beleg, ist die Signatur gerade nicht erreichbar; der Beleg gilt trotzdem und trägt den vorgeschriebenen Hinweis.
- Siehe auch [Gutschein einlösen](hilfe:kassa-gutschein-einloesen) und [Beleg stornieren](hilfe:kassa-beleg-stornieren).
