---
art: einstellung
bereich: Stammdaten
oberflaeche: verwaltung
rollen: inhaber, manager
seite: verwaltung
---
# Kassa

Einstellungen in der Windows-App unter „Verwaltung“ › „Kassa & Geld“ › „Kassa“. Sie gelten für alle Kassen und Standorte des Spas; ändern darf sie, wer die Firmendaten pflegen darf (Inhaber). „Speichern“ ohne Änderung schickt nichts; an Belegen, RKSV und Signatur ändert sich dadurch nichts.

![Kassa-Einstellungen in der Windows-App](images/app/verwaltung-kassa.png)

## Rabatte {#rabatte}

Zwei Einstellungen: bis wohin jede Person Rabatt geben darf, und ob Barzahlung günstiger ist.

### Rabatt ohne Freigabe bis (%) {#rabattgrenze}

Legt fest, bis zu welchem Prozentsatz jede Person an der Kassa und in der Kundenkartei Rabatt geben darf. Standard sind 20 %.

- Rabatt gibt man an einer Position in Prozent oder als Betrag ein. Ein Betrag wird in Prozent der Position umgerechnet; der Beleg nennt immer den Prozentsatz, z. B. „Rabatt 12,7 %“.
- Über der Grenze geben nur Inhaber und Standortleitung Rabatt. Ist eine andere Person angemeldet, blendet die Windows-App „Freigabe durch“ ein: die Leitung wählt ihren Namen und tippt ihre PIN ein, ohne dass sich jemand ab- oder anmeldet. Wer freigegeben hat, wird gespeichert.
- Freigeben kann nur, wer eine PIN festgelegt hat (über das eigene Profilbild bzw. „PIN festlegen“). Nach fünf falschen PINs ist die PIN 15 Minuten gesperrt.
- Die Grenze gilt auch für den dauerhaften Rabatt in der Kundenkartei.

### Barzahlungsrabatt (%, 0 = aus) {#barzahlungsrabatt}

Ein Nachlass, wenn der Gast den ganzen Beleg bar bezahlt. Erlaubt sind 0 bis 10 %; 0 schaltet ihn aus (Standard).

- Die offene Rechnung zeigt dann z. B. „bar 4,16 € günstiger (3 % Barzahlungsrabatt)“, und das Rückgeld wird mit dem verminderten Betrag gerechnet.
- Auf dem Beleg steht der Nachlass als eigene Zeile „Barzahlungsrabatt 3 %“, getrennt je Steuersatz. Die Umsatzsteuer wird vom verminderten Betrag gerechnet; Signatur, Tagesabschluss und Buchhaltungsexport enthalten ihn ohne weiteres Zutun.
- Verkaufte Gutscheine bleiben beim vollen Wert.
- Zahlt der Gast auch nur teilweise mit Karte, Überweisung oder Gutschein, gibt es keinen Barzahlungsrabatt.
- Ein Storno dreht die Rabattzeile mit um.
