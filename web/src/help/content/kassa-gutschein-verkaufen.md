---
art: ablauf
aufbau: schritte
bereich: Kassa
oberflaeche: windows-app
rollen: rezeption, leitung, inhaber, manager
seite: kassa
stichwoerter: Gutschein verkaufen, Wertgutschein, Behandlungsgutschein, Einzweckgutschein, Mehrzweckgutschein, Geschenk, Geschenkgutschein, Gutscheincode, voucher, gift card, gift certificate
---
# Gutschein verkaufen

Die Kassa verkauft zwei Arten von Gutscheinen: einen **Wertgutschein** über einen frei gewählten Betrag und einen Gutschein für eine bestimmte **Behandlung**. Den Code stellt der Server beim Bezahlen aus; er steht auf dem Beleg.

## Voraussetzungen {#voraussetzungen}

- Rezeption, Standortleitung oder Inhaber; ohne Verbindung zum Server geht der Verkauf nicht.
- Die Kassa ist offen, bei Bedarf mit einer offenen Rechnung; ohne offene Rechnung legt sie eine für „Laufkundschaft“ an.

## Schritte {#schritte}

### 1. Wert eintragen

In der Kassa das Panel „Artikel und Gutscheine“ neben der Rechnung aufklappen und unter „Gutschein verkaufen“ den Wert eintragen, z. B. `50`. Für einen Behandlungsgutschein stattdessen in der Auswahl **Behandlung** die Behandlung aussuchen, siehe [Gutschein für eine Behandlung](hilfe:kassa-gutschein-verkaufen#behandlung).

![Kassa mit markiertem Feld Wert unter Gutschein verkaufen](images/app/gutschein-verkaufen-1.png)

### 2. Gutschein auf die Rechnung setzen

Auf **Wertgutschein verkaufen** klicken. Der Gutschein steht auf der offenen Rechnung.

![Kassa mit markiertem Knopf Wertgutschein verkaufen](images/app/gutschein-verkaufen-2.png)

### 3. Bezahlen

Die Zahlung eintragen und auf **Bezahlen und Beleg erstellen** klicken, siehe [Bezahlen](hilfe:kassa-bezahlen).

![Kassa mit markiertem Knopf Bezahlen und Beleg erstellen](images/app/gutschein-verkaufen-3.png)

## Was danach {#was-danach}

Auf dem Beleg steht unter „Ausgestellte Gutscheine“ der Code, z. B. „Gutschein K7M3-Q9XA-B2CD“ (drei Blöcke zu vier Zeichen, ohne 0, O, 1, I und L), darunter bei einem Ablaufdatum „Einzulösen bis …“, siehe [Ablaufdatum](hilfe:kassa-gutschein-verkaufen#ablaufdatum). Den Beleg drucken oder den Code dem Gast mitgeben. Einlösen: [Gutschein einlösen](hilfe:kassa-gutschein-einloesen).

## Wenn etwas schiefgeht {#wenn-etwas-schiefgeht}

- Gutscheine werden immer zum vollen Wert verkauft: kein Rabatt, kein Kundenrabatt, kein Barzahlungsrabatt. Ein Gutschein auf der Rechnung lässt sich nicht teilweise bezahlen.
- Ein Verkauf lässt sich nur stornieren, solange vom Gutschein nichts eingelöst ist: [Beleg stornieren](hilfe:kassa-beleg-stornieren).



## Gutschein für eine Behandlung {#behandlung}

1. Unter „Gutschein verkaufen“ in der Auswahl **Behandlung** die Behandlung aussuchen.
2. Auf **Behandlung verkaufen** klicken. Der Preis ist der Preis dieser Behandlung am Standort der Kasse.
3. [Bezahlen](hilfe:kassa-bezahlen). Auf dem Beleg steht unter „Ausgestellte Gutscheine“ „Gutschein … (Behandlung)“.

![Beleg nach dem Bezahlen](images/app/kassa-beleg.png)

## Ablaufdatum {#ablaufdatum}

Oben unter „Gutschein verkaufen“ steht das Feld **Einzulösen bis**. Es gilt für Wertgutschein, Behandlungsgutschein und [Paket](hilfe:kassa-paket). Die Kassa belegt es nach dem Land der Kasse vor:

- Deutschland: 31.12. des dritten Jahres nach dem Kauf, z. B. Kauf am 09.11.2026 → 31.12.2029.
- Schweiz: fünf Jahre ab dem Kauf, z. B. 09.11.2026 → 09.11.2031.
- Österreich: leer, also unbefristet.

Das Datum lässt sich ändern; ein leeres Feld heißt unbefristet. Liegt das Datum vor dieser Frist, steht unter dem Feld ein Hinweis, z. B. „Kürzer als die übliche Frist bis 31.12.2029; eine kürzere Frist kann unwirksam sein.“ In Österreich steht bei jedem Datum „In Österreich sind Fristen unter 5 Jahren ohne Auszahlungsmöglichkeit unwirksam.“ Verkaufen geht in beiden Fällen.

Das Datum steht auf Beleg und Gutschein als „Einzulösen bis 31.12.2029“ und gilt beim [Einlösen](hilfe:kassa-gutschein-einloesen#fehler). Es zählt ab dem Tag, an dem der Gutschein auf die Rechnung kommt. Online gekaufte Gutscheine bekommen dieselbe Frist; die Mail mit dem Code nennt das Datum.

## Gut zu wissen {#hinweise}

- Gutscheine werden immer zum vollen Wert verkauft: kein Rabatt, kein Kundenrabatt, kein Barzahlungsrabatt.
- Ein Gutschein auf der Rechnung lässt sich nicht teilweise bezahlen.
- Der Wertgutschein ist beim Verkauf nicht steuerbar; versteuert wird erst die Leistung beim Einlösen. Der Behandlungsgutschein wird schon beim Verkauf versteuert.
- Ein Verkauf lässt sich nur stornieren, solange vom Gutschein nichts eingelöst ist.
- Einlösen: [Gutschein einlösen](hilfe:kassa-gutschein-einloesen).
