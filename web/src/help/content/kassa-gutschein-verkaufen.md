---
art: ablauf
aufbau: schritte
bereich: Kassa
oberflaeche: windows-app
rollen: rezeption, leitung, inhaber, manager
seite: kassa
stichwoerter: Gutschein verkaufen, Wertgutschein, Behandlungsgutschein, Einzweckgutschein, Mehrzweckgutschein, Geschenk, Geschenkgutschein, Gutscheincode, voucher, gift card, gift certificate, Gutschein drucken, Gutschein als PDF, Gutschein per Mail, Gutschein übergeben, Geschenkgutschein PDF, Gutscheinliste, Gutscheinliste drucken, offene Gutscheine, Restwerte, Verbindlichkeit, voucher list, voucher PDF
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

Auf dem Beleg steht unter „Ausgestellte Gutscheine“ der Code, z. B. „Gutschein K7M3-Q9XA-B2CD“ (drei Blöcke zu vier Zeichen, ohne 0, O, 1, I und L). Den Beleg drucken, den Gutschein als Geschenk-PDF drucken oder senden ([Gutschein als PDF](hilfe:kassa-gutschein-verkaufen#pdf)) oder den Code dem Gast mitgeben. Einlösen: [Gutschein einlösen](hilfe:kassa-gutschein-einloesen).

## Wenn etwas schiefgeht {#wenn-etwas-schiefgeht}

- Gutscheine werden immer zum vollen Wert verkauft: kein Rabatt, kein Kundenrabatt, kein Barzahlungsrabatt. Ein Gutschein auf der Rechnung lässt sich nicht teilweise bezahlen.
- Ein Verkauf lässt sich nur stornieren, solange vom Gutschein nichts eingelöst ist: [Beleg stornieren](hilfe:kassa-beleg-stornieren).



## Gutschein für eine Behandlung {#behandlung}

1. Unter „Gutschein verkaufen“ in der Auswahl **Behandlung** die Behandlung aussuchen.
2. Auf **Behandlung verkaufen** klicken. Der Preis ist der Preis dieser Behandlung am Standort der Kasse.
3. [Bezahlen](hilfe:kassa-bezahlen). Auf dem Beleg steht unter „Ausgestellte Gutscheine“ „Gutschein … (Behandlung)“.

![Beleg nach dem Bezahlen](images/app/kassa-beleg.png)

## Gutschein als PDF übergeben {#pdf}

Unter dem Beleg steht für jeden verkauften Gutschein eine eigene Zeile „Gutschein <Code> als Geschenk“ mit dem Knopfblock **Drucken**, **PDF speichern** und **Per Mail senden** und dem Knopf **Übergeben**.

- Das PDF `Gutschein-<Code>.pdf` ist eine A4-Seite zum Verschenken: Spa-Name groß, „Geschenkgutschein“, der Wert (oder die Behandlung), der Code mit QR-Code, „Gültig bis“ (ohne Ablaufdatum „ohne Befristung“), „Einlösbar in“ mit den Standorten und „Ausgestellt am“.
- **Per Mail senden** ist mit der Adresse der Käuferin vorbelegt; hat der Beleg keinen Gast, bleibt das Feld leer und die Adresse wird eingetippt. Der Betreff lautet „Ihr Gutschein von <Spa>“.
- **Übergeben** vermerkt für die Gutscheinliste, dass der Gutschein persönlich ausgehändigt wurde; danach steht „Gutschein … als übergeben vermerkt“.
- Nach einem Storno des Verkaufs steht auf dem PDF „Dieser Gutschein ist storniert und nicht mehr einlösbar.“.

Gutscheine drucken und senden darf, wer buchen oder Belege ansehen darf.

## Gutscheinliste drucken {#gutscheinliste}

Die Gutscheinliste zeigt alle Gutscheine des Standorts mit Werten, Status, Käufern und Versandvermerk, am Ende mit der Summe der offenen Restwerte.

1. In der Kassa in der Kopfzeile auf **Gutscheinliste …** klicken. Es öffnet sich die Karte „Gutscheinliste“.
2. Bei Bedarf filtern: **Ausgestellt von** und **bis**, **Status** (Alle, aktiv, aufgebraucht, abgelaufen, storniert) und die Haken **läuft in 30 Tagen ab** und **noch nicht versendet**.
3. Auf **Drucken** oder **PDF speichern** klicken. **Per Mail senden** gibt es hier nicht.

![Kassa mit geöffneter Karte Gutscheinliste, Filtern und den Knöpfen Drucken und PDF speichern](images/app/kassa-gutscheinliste.png)

Das PDF `Gutscheinliste-<Standort>-<Datum>.pdf` ist A4 quer. Es hat eine Zeile je Gutschein mit Code, Art, Ausgestellt, Ausgangswert, Restwert, Gültig bis, Status, Käufer, Empfänger, Kanal (Kassa oder Online) und Versand. Die Kopfzeile nennt die gewählten Filter. Unten stehen „Offene Restwerte (Verbindlichkeit)“ mit Betrag und Zahl der aktiven Gutscheine, „Abgelaufen, noch nicht ausgebucht“ und ein Satz zur Berechnung. Bei vielen Gutscheinen läuft die Tabelle über mehrere Seiten mit wiederholtem Kopf.

Die Spalte „Versand“ zeigt, wie der Gutschein zum Gast kam: „Mail am <Datum>“ mit „an <Adresse>“, „Gedruckt am <Datum>“ oder „Übergeben am <Datum>“. Ein Druck, der am Drucker scheitert, hinterlässt keinen Vermerk. Mit **noch nicht versendet** stehen nur Gutscheine ohne einen solchen Vermerk auf der Liste.

Die Karte lässt sich immer öffnen. Drucken und Speichern sind gesperrt, wenn das Ende vor dem Beginn liegt („Das Ende darf nicht vor dem Beginn liegen.“) oder wenn das Recht fehlt: „Die Gutscheinliste druckt nur die Kassenaufsicht oder wer „Berichte mit Beträgen“ sehen darf.“ Wie die Knöpfe arbeiten: [Dokumente drucken, speichern und senden](hilfe:dokumente-drucken-speichern-senden).

## Gut zu wissen {#hinweise}

- Gutscheine werden immer zum vollen Wert verkauft: kein Rabatt, kein Kundenrabatt, kein Barzahlungsrabatt.
- Ein Gutschein auf der Rechnung lässt sich nicht teilweise bezahlen.
- Der Wertgutschein ist beim Verkauf nicht steuerbar; versteuert wird erst die Leistung beim Einlösen. Der Behandlungsgutschein wird schon beim Verkauf versteuert.
- Ein Verkauf lässt sich nur stornieren, solange vom Gutschein nichts eingelöst ist.
- Einlösen: [Gutschein einlösen](hilfe:kassa-gutschein-einloesen).
