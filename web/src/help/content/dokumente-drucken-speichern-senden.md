---
art: ablauf
bereich: Dokumente
oberflaeche: windows-app, verwaltung
rollen: rezeption, therapeut, leitung, inhaber, manager
seite: kassa
stichwoerter: PDF, PDF speichern, drucken, ausdrucken, per Mail senden, Rechnung schicken, Rechnung per Mail, Beleg per Mail, Kopie, Nachdruck, Dokument, Anhang, Standarddrucker, A4, Bon-Format, Vermerk, Gesendet an, print, save as PDF, send by email, email invoice, attachment, copy of receipt
---
# Dokumente drucken, als PDF speichern und per Mail senden

Beleg, Rechnung, Terminbestätigung, Tagesplan und viele weitere Dokumente gibt es als PDF. Überall steht derselbe Block mit den Knöpfen **Drucken**, **PDF speichern** und **Per Mail senden**. Was im PDF steht, rechnet der Server; die App zeigt es nur an.

## Der Knopfblock {#knopfblock}

Der Block steht dort, wo das Dokument entsteht, und sieht überall gleich aus: drei weiße Knöpfe mit Rahmen und Symbol.

![Beleg in der Kassa mit den Knöpfen Drucken, PDF speichern und Per Mail senden](images/app/kassa-beleg-als-pdf.png)

- **Kassa**: Beleg, Rechnung, Gutschein, Tagesabschluss und Gutscheinliste, siehe [Belege ansehen und drucken](hilfe:kassa-beleg-drucken), [Gutschein verkaufen](hilfe:kassa-gutschein-verkaufen#pdf) und [Tagesabschluss](hilfe:kassa-tagesabschluss#pdf).
- **Kalender**: Terminbestätigung, Tagesplan und Warteliste, siehe [Terminbestätigung](hilfe:kalender-terminbestaetigung), [Tagesplan drucken](hilfe:kalender-tagesplan-drucken) und [Warteliste ansehen](hilfe:kalender-warteliste#pdf).
- **Kundenkarte**: Auskunft über gespeicherte Daten und Stammblatt, siehe [Kundenkarte ansehen](hilfe:kunden-kundenkarte#pdf).
- **Verwaltung**: Dienstplan-Aushang, Stundenliste, Zählliste und Inventurliste, Bestellung und Ablaufliste, siehe [Dienstplan veröffentlichen](hilfe:dienstplan-veroeffentlichen#aushang), [Arbeitszeit korrigieren](hilfe:zeiten-korrigieren#stundenliste), [Inventur buchen](hilfe:lager-inventur#pdf), [Bestellen](hilfe:lager-bestellen#pdf) und [Bald ablaufende Chargen](hilfe:lager-ablauf-aufgaben#pdf).

Nicht jedes Dokument lässt sich senden: Wo es keinen Empfänger gibt (Tagesabschluss, Stundenliste, Auskunft), fehlt der Knopf **Per Mail senden**.

## Drucken {#drucken}

**Drucken** schickt das PDF an den Standarddrucker von Windows, ohne eigenen Druckdialog. Danach steht kurz „Gedruckt“. Dafür braucht der PC einen Standarddrucker und ein Programm für PDF-Dateien. Fehlt eines, steht „Kein Drucker eingerichtet“ oder „Kein PDF-Programm zum Drucken eingerichtet“.

In der Kassa gibt es zwei Druckknöpfe: **Drucken** bedient wie bisher den Bondrucker, **A4 drucken** schickt den Beleg als A4-Seite an den Standarddrucker.

## PDF speichern {#speichern}

1. Auf **PDF speichern** klicken. Der Speicherdialog von Windows öffnet sich mit einem Dateinamen, den der Server vorschlägt, z. B. `Rechnung-K1-142.pdf`.
2. Ordner wählen und speichern. Danach steht kurz „Gespeichert: …“ mit dem Dateinamen.

Beim Beleg hat **PDF speichern** einen Pfeil: **PDF im Bon-Format speichern** erzeugt dasselbe Dokument schmal wie einen Bon (80 mm).

## Per Mail senden {#per-mail}

1. Auf **Per Mail senden** klicken. Es öffnet sich die Rückfrage „… per Mail senden?“.
2. **An** prüfen. Die Adresse ist vorbelegt, bei Beleg und Gutschein aus der Kundenkarte, bei der Bestellung aus den Daten des Lieferanten. Ist keine bekannt, bleibt das Feld leer.
3. **Betreff** und **Text** prüfen und bei Bedarf ändern. Beide sind vorbelegt, der Text mit Gruß.
4. Auf **Senden** klicken. **Senden** ist grau, solange die Adresse fehlt oder keine Mailadresse ist („Das ist keine gültige Mailadresse.“) oder der Betreff leer ist. **Abbrechen** oder Esc schließt die Rückfrage ohne Versand.

![Rückfrage Per Mail senden mit Adresse, Betreff und Text](images/app/dokumente-per-mail-senden.png)

Der Empfänger bekommt das PDF als Anhang. Absender ist der Name des Spas, Antworten gehen an das Postfach des Spas. Mit dem Anhang darf die Mail höchstens 5 MB groß sein.

Nach dem Senden steht kurz „Gesendet an …“. Bei Beleg, Rechnung, Terminbestätigung, Bestellung und Gutschein bleibt ein Vermerk: wohin die Mail ging, wann und von wem.

- Beleg und Rechnung: unter dem Beleg „Per Mail gesendet an … am … von …“.
- Terminbestätigung: im [Verlauf des Termins](hilfe:kalender-termin-verlauf) „Bestätigung per Mail an … gesendet von …“.
- Bestellung: unter den Knöpfen „Gesendet am … an … von …“; ein zweites Senden fügt eine zweite Zeile hinzu.
- Gutschein: in der Gutscheinliste, siehe [Gutscheinliste drucken](hilfe:kassa-gutschein-verkaufen#gutscheinliste).

## Wenn ein Knopf grau ist {#gesperrt}

Ein grauer Knopf nennt den Grund, sobald der Mauszeiger darauf steht; Screenreader lesen ihn als Hilfetext des Knopfes vor. Die Dokumente folgen den Rechten der Daten, aus denen sie entstehen:

- „Dafür braucht es das Recht „Berichte mit Beträgen“.“ beim Tagesabschluss.
- „Die Gutscheinliste druckt nur die Kassenaufsicht oder wer „Berichte mit Beträgen“ sehen darf.“ bei der Gutscheinliste.
- „Den Aushang druckt nur die Standortleitung.“ beim Dienstplan-Aushang.
- „Die Stundenliste druckt nur, wer die Zeiten lesen darf.“ bei der Stundenliste.
- „Die Inventur druckt nur die Standortleitung.“ bei Zählliste und Inventurliste, „Die Ablaufliste druckt nur die Standortleitung.“ bei der Ablaufliste.
- „Das Ende darf nicht vor dem Beginn liegen.“ in der Gutscheinliste, wenn die Daten nicht stimmen.

Während ein Dokument lädt, sind alle Knöpfe des Blocks kurz grau. Fehler stehen als rote Meldung unter den Knöpfen und lassen sich mit dem Kreuz schließen, z. B. „Nicht gespeichert: …“ oder „Das Dokument kam nicht an.“.

## Gut zu wissen {#hinweise}

- Jedes PDF trägt oben Spa-Name und Firmendaten ([Firmendaten](hilfe:firmendaten)) und unten „Erstellt am … von …“ mit Vorname und Seitenzahl. Es zeigt nur Schwarz und Grau und druckt deshalb auch auf einem Schwarzweißdrucker sauber.
- Ein Nachdruck eines Belegs trägt den Vermerk „Kopie“.
- Gesundheitsangaben stehen in keinem Dokument außer der Auskunft über gespeicherte Daten, und dort nur mit dem Recht für die Anamnese. Sonst steht der Satz „Gesundheitsangaben: vorhanden, Auskunft durch die Leitung“.
- Die Dokumente brauchen eine Verbindung zum Server, weil er sie erstellt.
