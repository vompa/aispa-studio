---
art: ablauf
bereich: Kassa
oberflaeche: windows-app
rollen: rezeption, leitung, inhaber, manager
seite: kassa
stichwoerter: Beleg drucken, Bon drucken, Beleg nochmal, Kopie, Rechnung drucken, Belege ansehen, Belegliste, Belege heute, Quittung, Bondrucker, Beleg als PDF, Beleg per Mail, Beleg schicken, A4, PDF speichern, print receipt, reprint, receipts list, receipt as PDF, email receipt
---
# Belege ansehen und drucken

Unter „Belege“ stehen die Belege eines Tages an der gewählten Kasse, zuerst die von heute. Jeder lässt sich ansehen, erneut drucken, als PDF speichern und dem Gast per Mail schicken.

1. In der Kassa oben **Belege** wählen. Einen anderen Tag zeigen die Pfeile neben „Belege heute“: **Tag davor** und **Tag danach**.
2. Links unter „Belege heute“ den Beleg anklicken. Die Liste zeigt Uhrzeit, Nummer, Zahlungsarten und Betrag, neueste zuerst; der neueste ist schon gewählt.
3. Rechts erscheint der Beleg so, wie er gedruckt wird.
4. Auf **Drucken** klicken, um den Beleg auf dem Bondrucker zu drucken. Daneben steht „Beleg gedruckt.“ oder „Nicht gedruckt: …“ mit dem Grund.
5. Darunter steht der Knopfblock für das PDF, siehe [Als PDF speichern oder per Mail senden](hilfe:kassa-beleg-drucken#pdf).

![Belege des Tages mit Storno](images/app/kassa-belege.png)

## Direkt nach dem Bezahlen {#nach-dem-bezahlen}

Nach dem Bezahlen steht der Beleg in der Mitte der Kassa. Mit eingeschaltetem automatischem Druck kommt er gleich aus dem Drucker, sonst über **Drucken** direkt unter dem Beleg. Ohne Bondrucker ist der Knopf grau und der Hinweis auf „Geräte“ steht daneben. Darunter stehen **A4 drucken**, **PDF speichern** und **Per Mail senden**; sie brauchen keinen Bondrucker.

## Als PDF speichern oder per Mail senden {#pdf}

Unter dem Beleg steht, nach dem Bezahlen wie im Reiter „Belege“, der Knopfblock für das PDF. Er hat vier Knöpfe, der erste ist der Bondrucker.

![Beleg nach dem Bezahlen mit den Knöpfen Drucken, A4 drucken, PDF speichern und Per Mail senden](images/app/kassa-beleg-als-pdf.png)

- **Drucken** druckt auf dem Bondrucker, wie bisher.
- **A4 drucken** schickt den Beleg als A4-Seite an den Standarddrucker von Windows.
- **PDF speichern** speichert den Beleg als A4-Datei `Beleg-<Kasse>-<Nummer>.pdf`. Am Pfeil daneben speichert **PDF im Bon-Format speichern** denselben Inhalt 80 mm schmal wie einen Bon.
- **Per Mail senden** schickt das PDF dem Gast. Die Adresse ist aus der Kundenkarte vorbelegt, der Betreff lautet „Ihr Beleg Nr. … von <Spa>“. Danach steht unter dem Beleg „Per Mail gesendet an … am … von …“.

Das PDF zeigt Belegkopf, Positionen, Summe, Steuerzeile, Zahlung mit Rückgeld und den QR-Code „Signierter Belegcode (RKSV)“. Ein Nachdruck aus dem Reiter „Belege“ trägt den Vermerk „Kopie“, ein Stornobeleg den Titel „Stornobeleg Nr. …“ mit dem Grund und negativen Beträgen. Hat der Beleg einen Empfänger, ist das PDF eine Rechnung: [Rechnung mit Empfänger](hilfe:kassa-rechnung-mit-empfaenger#pdf).

Die Knöpfe sehen nur Personen, die Belege ansehen dürfen. Eine Therapeutin ohne dieses Recht sieht weder die Belege noch ihre PDFs. Wie Speichern, Drucken und Senden im Einzelnen arbeiten: [Dokumente drucken, speichern und senden](hilfe:dokumente-drucken-speichern-senden).

## Gut zu wissen {#hinweise}

- Ohne Bondrucker ist **Drucken** grau und es steht „Kein Bondrucker eingerichtet (unter „Geräte“).“ Einrichten: [Bondrucker einrichten](hilfe:geraete-bondrucker-einrichten). **A4 drucken**, **PDF speichern** und **Per Mail senden** gehen trotzdem.
- „Storno“ kennzeichnet einen Stornobeleg, „storniert“ einen Beleg, zu dem es einen Stornobeleg gibt.
- Die Liste zeigt einen Tag und nur die gewählte Kasse; andere Kassen über die Auswahl der Kasse. Beim nächsten Öffnen von „Belege“ steht wieder heute da. Das Datenerfassungsprotokoll (DEP) lädt die Leitung in der Windows-App unter **Verwaltung** › **Zahlen** › **Berichte** › **Pflichtberichte** herunter.
- Wird ein Beleg zur Rechnung mit Empfänger, druckt er danach als Rechnung: [Rechnung mit Empfänger](hilfe:kassa-rechnung-mit-empfaenger).
