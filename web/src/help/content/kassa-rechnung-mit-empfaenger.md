---
art: ablauf
bereich: Kassa
oberflaeche: windows-app
rollen: rezeption, leitung, inhaber, manager
seite: kassa
stichwoerter: Firmenrechnung, Rechnung mit Empfänger, Rechnung auf Firma, UID, Umsatzsteuer-ID, VIES, Rechnungsadresse, Firmenkunde, Gutschein für Mitarbeiter, Kleinbetragsrechnung, invoice, company invoice, VAT ID
---
# Rechnung mit Empfänger

Ein Beleg wird zur Rechnung an eine Firma, wenn man Name, Anschrift und gegebenenfalls UID-Nummer des Empfängers ergänzt. Das geht nach dem Bezahlen oder später unter „Belege“, einmal je Beleg.

1. Nach dem Bezahlen erscheint unter dem Beleg „Rechnung mit Empfänger“. Später: oben **Belege** wählen und links den Beleg anklicken.
2. Auf **Empfänger erfassen** klicken.
3. „Empfänger (Name oder Firma)“ und „Anschrift“ ausfüllen, bei Bedarf die „UID-Nummer, z. B. ATU12345678“.
4. Optional auf **UID prüfen** klicken. Die Kassa fragt über VIES ab und zeigt z. B. „UID gültig: … · Abfrage …“; leere Felder für Name und Anschrift füllt sie aus der Antwort.
5. Auf **Empfänger übernehmen** klicken. Der Titel wechselt auf „Rechnung Nr. <Kasse>-<Nummer>“, und der Beleg zeigt „Rechnung an:“ mit dem Empfänger.
6. Die Rechnung drucken oder dem Gast mitgeben, siehe [Belege drucken](hilfe:kassa-beleg-drucken).

![Rechnung mit Empfänger und UID](images/app/kassa-rechnung.png)

## Empfänger bei der Kundin merken {#merken}

Hat der Beleg einen Gast, belegt die Kassa die Felder mit dem Rechnungsempfänger aus seiner Kundenkartei vor. Der Haken „Bei … als Rechnungsempfänger merken“ (vorausgewählt) speichert neue oder geänderte Angaben dort, damit sie beim nächsten Mal schon dastehen. Siehe [Kundin bearbeiten](hilfe:kunden-bearbeiten#rechnungsempfaenger).

## Gut zu wissen {#hinweise}

- Über der Grenze für Kleinbetragsrechnungen (Österreich 400 €, Deutschland 250 €, Schweiz 400 CHF) zeigt die Kassa rot: „Über …: Für ein Unternehmen braucht die Rechnung Name und Anschrift des Empfängers.“
- Über 10.000 € ist die UID-Nummer Pflicht.
- Eine gültige VIES-Abfrage wird als Nachweis an die Rechnung gehängt, wenn die UID gleich geblieben ist. Antwortet VIES nicht, steht „VIES hat nicht geantwortet; später nochmals prüfen.“; die Rechnung geht trotzdem.
- Der Empfänger lässt sich nur einmal ergänzen und nicht mehr ändern. Bei einem Fehler den Beleg [stornieren](hilfe:kassa-beleg-stornieren) und neu kassieren.
- Zu Stornobelegen und stornierten Belegen gibt es keine Rechnung.
- Die Unternehmensangaben des Spas (Firmenwortlaut, UID, Firmenbuch) kommen aus den [Firmendaten](hilfe:firmendaten#unternehmen).
