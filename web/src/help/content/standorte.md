---
art: einstellung
bereich: Stammdaten
oberflaeche: verwaltung
rollen: inhaber, manager, leitung
seite: verwaltung
---
# Standorte & Räume

Einstellungen in der Windows-App unter „Verwaltung“ › „Betrieb“ › „Standorte & Räume“. Links steht die Liste der Standorte, rechts alles zum gewählten Standort: Stammdaten, Online-Buchung, Räume, Tagesstart-Kontrolle und Geräte.

Wer darf was:

- Den Bereich sehen und ändern dürfen Inhaber und Standortleitung. Eine Standortleitung sieht nur die Standorte, für die sie freigeschaltet ist.
- Einen neuen Standort anlegen („Standort anlegen“) darf nur der Inhaber.
- Rezeption und Therapeut:innen sehen diesen Bereich nicht.

Änderungen gelten ab dem Speichern für neue Buchungen. Bereits gebuchte Termine bleiben unverändert.

In der Windows-App:

- Das Formular des Standorts (Name bis Öffnungszeiten) speichert **Speichern** unter den Öffnungszeiten, auch für einen neuen Standort. **Strg+S** speichert wie der Knopf, **Verwerfen** holt den gespeicherten Stand zurück.
- Wer den Standort mit ungespeicherten Änderungen verlässt, etwa einen anderen Standort wählt oder zum Kalender wechselt, wird gefragt: „Änderungen speichern?“ mit „Speichern“, „Verwerfen“ und „Abbrechen“. „Abbrechen“ bleibt beim Standort.
- Online-Buchung, Räume, Tagesstart-Kontrolle und Geräte speichern für sich, mit dem Knopf im Abschnitt bzw. in der Zeile; sie zählen nicht zu den Änderungen, nach denen die Rückfrage fragt. Eine Raumzeile mit eigenen Änderungen zeigt „Nicht gespeichert“ und den Knopf „Speichern“.
- Uhrzeiten lassen sich kurz eintippen: „9“ wird zu 09:00, „1430“ zu 14:30.
- Aus dem Kalender führt „Raum bearbeiten“ direkt in die Zeile des Raums, und der Assistent führt mit „Jetzt ergänzen“ in das fehlende Feld.

## Standort {#standort}

Die Grunddaten des Standorts. Gespeichert wird mit „Speichern“ unter den Öffnungszeiten; dabei werden auch Anschrift, eigene Gesellschaft und Öffnungszeiten gespeichert.

### Name {#name}

Name des Standorts, z. B. „Wien Innere Stadt“. Pflichtfeld. Er erscheint in der Standortliste, im Kalender, im Kundenbereich der Online-Buchung und in den Mails an Gäste.

### Zeitzone {#zeitzone}

Zur Auswahl stehen `Europe/Vienna`, `Europe/Berlin` und `Europe/Zurich`. Standard ist `Europe/Vienna`.

Alle Uhrzeiten des Standorts gelten in dieser Zeitzone: Öffnungszeiten, Arbeitszeiten des Teams, Termine und Preise nach Uhrzeit. Auch Sommer- und Winterzeit richten sich danach.

### Währung {#waehrung}

`EUR` (Standard) oder `CHF`. In dieser Währung zeigt der Kundenbereich die Preise, und in ihr laufen Online-Zahlungen, Gutscheinverkauf im Kundenbereich und die Ausfallgebühr über den Zahlungsanbieter.

### Bundesland / Kanton {#bundesland-kanton}

Bestimmt, welche gesetzlichen Feiertage am Standort gelten. Zur Auswahl stehen die Bundesländer Österreichs und Deutschlands und die Kantone der Schweiz, die die Software kennt.

- Leer („nur landesweite Feiertage“) gelten nur die Feiertage, die im ganzen Land gelten.
- Die Feiertage zählen in der Übergabe an die Lohnverrechnung: Stunden an einem Feiertag am Standort werden als Feiertagsstunden ausgewiesen.
- Der Kalender schließt an Feiertagen nicht von selbst. Soll der Standort zu sein, legt man dafür eine [Schließzeit](hilfe:schliesszeiten) an (**Verwaltung** › **Betrieb** › **Schließzeiten**).
- Zusätzlich zu den Feiertagsregeln gelten die Tage aktiver [Kalender-Abos](hilfe:kalender-abos) für gesetzliche Feiertage (zum Beispiel ein halber Tag); sie ergänzen die Regeln im Kalender, in der Feiertagsliste und beim Assistenten, nicht in der Lohnverrechnung. Feiertage ändern keine Öffnungszeit und keine Buchung; wer an einem Feiertag geschlossen hat, trägt eine [Schließzeit](hilfe:schliesszeiten) ein.
- Der [KI-Assistent](hilfe:ki-assistent-fragen) beantwortet „Wann ist der nächste Feiertag?“ und „Ist der 1. November ein Feiertag?“ aus dieser Auswahl. Ohne Eintrag nennt er nur die landesweiten Feiertage und weist darauf hin; Schweizer Kantone außerhalb der Auswahl kennt die Software noch nicht.

## Anschrift und Kontakt {#anschrift}

Diese Angaben erscheinen im Kundenbereich der Online-Buchung und in den Mails an Gäste. Alle Felder sind freiwillig; leere Felder werden nicht angezeigt.

- **Straße und Nummer**: höchstens 200 Zeichen.
- **PLZ**: höchstens 10 Zeichen.
- **Ort**: höchstens 100 Zeichen.
- **Telefon**: höchstens 40 Zeichen.
- **E-Mail für Gäste**: muss eine gültige E-Mail-Adresse sein. An diese Adresse wenden sich Gäste mit Fragen.

Fehlen in den Firmendaten des Spas Anschrift, Telefon oder E-Mail, verwendet das Impressum der Online-Buchung die Angaben eines Standorts.

Der [KI-Assistent](hilfe:ki-assistent-fragen) antwortet auf „Unsere Kontaktdaten“, „Wie lautet unsere Telefonnummer?“ oder „Wie ist unsere Adresse?“ aus diesen Feldern und bietet **Kopieren** an. Fehlt ein Feld, sagt er „Dazu ist nichts hinterlegt“ und verweist auf diese Stelle.

### Anreise und Parkplätze {#anreise}

Freier Text, höchstens 500 Zeichen, z. B. „U1 bis Stephansplatz, Garage Am Graben 2 Minuten zu Fuß“. Leer ist erlaubt.

- Der [KI-Assistent](hilfe:ki-assistent-fragen) antwortet auf „Wie kommt der Gast zu uns, Parkplätze?“ mit diesem Text; ohne Eintrag sagt er „Dazu ist nichts hinterlegt“ und verweist hierher.
- Gäste sehen den Text im Kundenbereich bei ihrem Termin, unter der Anschrift und dem Knopf **Route planen**, und in der Bestätigungsmail der Online-Buchung (Zeile „Anreise“, dazu der Link zur Routenplanung, wenn eine Anschrift eingetragen ist). Ohne Text fehlt die Zeile; der Knopf braucht die Anschrift.
- Fehlt der Text, nennt der Assistent das im Abschnitt „Unvollständig“ und führt mit **Jetzt ergänzen** direkt in dieses Feld: [Unvollständig: Angaben ergänzen](hilfe:ki-assistent-fragen#unvollstaendig).

## Eigene Gesellschaft {#eigene-gesellschaft}

Nur ausfüllen, wenn dieser Standort zu einer anderen Gesellschaft gehört als das Spa, z. B. eine eigene GmbH für einen zweiten Standort.

- **Firmenwortlaut**: höchstens 200 Zeichen, z. B. „Day Spa Salzburg GmbH“.
- **UID-Nummer**: höchstens 20 Zeichen.
- **Firmenbuch- bzw. Handelsregisternummer**: höchstens 30 Zeichen.
- **Firmenbuchgericht**: höchstens 100 Zeichen.

Bleiben alle vier Felder leer, gelten die Firmendaten des Spas („Verwaltung“ › „Betrieb“ › „Firmendaten“). Sobald auch nur ein Feld ausgefüllt ist, gelten für diesen Standort ausschließlich die Angaben von hier; dann alle vier vollständig ausfüllen.

Die Angaben stehen auf Kassenbeleg und Rechnung der Kassen dieses Standorts.

## Öffnungszeiten {#oeffnungszeiten}

Je Wochentag ein Häkchen und die Zeiten „öffnet“ und „schließt“. Ohne Häkchen ist der Tag geschlossen. Ein neuer Standort ist anfangs Montag bis Samstag von 09:00 bis 20:00 Uhr geöffnet; ein neu angehakter Tag beginnt mit 09:00 bis 18:00 Uhr. Die Öffnung muss vor der Schließung liegen.

Wirkung:

- Termine sind nur buchbar, wenn die Behandlung selbst ganz in der Öffnungszeit liegt. Vor- und Nachbereitung dürfen davor bzw. danach liegen.
- Die Online-Buchung bietet Startzeiten ab der Öffnung im eingestellten Raster an, so dass die Behandlung bis zur Schließung endet.
- Die Vollkosten je Behandlung rechnen mit den Öffnungsminuten des Monats als Kapazität der Räume.
- Wer wann arbeitet, steht nicht hier, sondern unter „Team“ und im Dienstplan.

## Online-Buchung {#online-buchung}

Regeln für die Online-Buchung im Kundenbereich, je Standort. Solange nichts geändert wurde, gelten die Vorgaben für alle Standorte (Hinweis „Es gelten die Vorgaben für alle Standorte“). Gespeichert wird mit „Regeln speichern“; danach gelten die Regeln nur für diesen Standort.

Ändern dürfen Inhaber und Standortleitung; alle anderen sehen die Regeln nur.

Fest eingebaut, unabhängig von diesen Regeln:

- Ein Gast kann höchstens 3 offene Termine online gebucht haben; weitere bucht die Rezeption. Eine Paar- oder Spa-Tag-Buchung zählt einmal.
- Wer in den letzten zwölf Monaten zweimal nicht erschienen ist, bucht online nur noch mit Karte oder Anzahlung. Verlangt der Standort beides nicht, bittet der Kundenbereich ihn anzurufen; den Grund nennt er nicht.

### Termine an diesem Standort online buchbar {#online-buchbar}

Schaltet die Online-Buchung für den Standort ein oder aus. Standard: eingeschaltet. Ausgeschaltet bietet der Kundenbereich an diesem Standort keine Behandlungen und keine Termine an. Bestehende Online-Termine bleiben bestehen.

### Vorlauf (Minuten) {#vorlauf}

So viele Minuten nach jetzt liegt der früheste online buchbare Beginn. Erlaubt sind 0 bis 10080 Minuten (7 Tage). Vorgabe: 120 Minuten. Beispiel: Mit 120 kann um 10:00 Uhr frühestens ein Termin ab 12:00 Uhr gebucht werden.

### Buchbar im Voraus (Tage) {#buchbar-im-voraus}

So viele Tage ab heute zeigt der Kundenbereich freie Termine. Erlaubt sind 1 bis 365 Tage. Vorgabe: 90 Tage.

### Online-Storno bis (Stunden vorher) {#online-storno}

Bis so viele Stunden vor Beginn können Gäste ihren Termin selbst online stornieren. Erlaubt sind 0 bis 336 Stunden (14 Tage). Vorgabe: 24 Stunden.

- Innerhalb derselben Frist können Gäste ihren Termin auch selbst verschieben, höchstens zweimal und nur auf eine Zeit, die online frei ist. Danach geht es nur noch über die Rezeption. Gruppen- und Paarbuchungen lassen sich online nicht verschieben. Eine Zusage („Ich komme“) gilt nach dem Verschieben nicht mehr.

- Eine Absage nach dieser Frist gilt als späte Absage; dann kann eine Ausfallgebühr belastet werden.
- Für einen Termin gilt die Frist, die beim Buchen eingestellt war. Spätere Änderungen betreffen nur neue Buchungen.
- Die KI-Terminannahme nennt Gästen diese Frist.

### Ausfallgebühr (% des Preises, 0 = keine) {#ausfallgebuehr}

Prozent des Terminpreises, die bei Nichterscheinen oder später Absage verlangt werden können. Erlaubt sind 0 bis 100. Vorgabe: 0 (keine Gebühr).

- Ist ein Wert über 0 eingestellt, stimmt der Gast beim Online-Buchen der Gebühr zu und hinterlegt danach eine Karte beim Zahlungsanbieter.
- Belastet wird nie automatisch, sondern nur auf Knopfdruck im Termin, wenn er auf „Nicht erschienen“ steht oder nach der Stornofrist abgesagt wurde. Auslösen dürfen Inhaber und Standortleitung. Die Gebühr wird an der Kassa mit Beleg verbucht.
- Die Gebühr wird auf den Cent gerundet. Eine Behandlung kann unter „Behandlungen“ eigene Prozent haben; die gelten dann statt dieses Werts.
- Wirkt nur, wenn unter „Verwaltung“ › „Kassa & Geld“ › „Zahlungen“ ein Zahlungsanbieter eingerichtet ist. Sonst wird keine Karte verlangt.

### Anzahlung (% des Preises, 0 = keine, 100 = Vorauszahlung) {#anzahlung}

Prozent des Terminpreises, die der Gast beim Online-Buchen im Voraus bezahlt. Erlaubt sind 0 bis 100; 100 heißt Vorauszahlung. Vorgabe: 0 (keine Anzahlung).

- Der Gast stimmt beim Buchen den Bedingungen zu und bezahlt danach beim Zahlungsanbieter. Die Kassa des Standorts stellt dafür einen Anzahlungsbeleg aus, versteuert wie die Behandlung.
- Beim Kassieren der Behandlung zieht die Kassa die Anzahlung als eigene Zeile ab; offen bleibt nur der Rest.
- Sagt der Gast online innerhalb der Stornofrist ab, wird der Anzahlungsbeleg storniert und das Geld erstattet. Bei „Nicht erschienen“ oder später Absage bleibt die Anzahlung beim Spa, bis Inhaber oder Manager sie mit „Anzahlung retournieren“ zurückzahlen (die Standortleitung storniert keinen Anzahlungsbeleg).
- Verlangt ein Termin eine Anzahlung, braucht er keine Karte; die Anzahlung ersetzt die Ausfallgebühr. Eine Behandlung kann unter „Behandlungen“ eigene Prozent haben.
- Wirkt nur mit eingerichtetem Zahlungsanbieter und aktiver Kasse am Standort. Sonst gilt die Ausfallgebühr mit Karte, falls eingestellt.

### Ohne Karte oder Anzahlung freigeben nach (Stunden, 0 = nie) {#karte-freigeben}

Hinterlegt ein Gast nach dem Online-Buchen keine Karte oder bezahlt die Anzahlung nicht, wird der Termin nach so vielen Stunden automatisch freigegeben: storniert, die Warteliste rückt nach, und der Gast bekommt eine Mail. Erlaubt sind 0 bis 168 Stunden (eine Woche). 0 heißt: nie freigeben. Vorgabe: 0.

- Gilt nur für Termine mit Ausfallgebühr oder Anzahlung.
- Liegt der Zeitpunkt erst nach Terminbeginn, wird nicht freigegeben.
- Die Prüfung läuft regelmäßig im Hintergrund, standardmäßig alle 5 Minuten; die Freigabe kann daher einige Minuten nach Fristablauf erfolgen.

### Startzeiten im Raster {#startzeiten-raster}

Abstand der Startzeiten, die der Kundenbereich anbietet: 5, 10, 15, 20, 30 oder 60 Minuten. Vorgabe: 15 Minuten. Das Raster beginnt mit der Öffnungszeit. Beispiel: Öffnung 09:00 Uhr und 30 Minuten ergibt 09:00, 09:30, 10:00 und so weiter, soweit Team, Raum und Gerät frei sind.

### Ausfallgebühr und Anzahlung nur von neuen Gästen verlangen {#nur-neue-gaeste}

Ist das Häkchen gesetzt, verlangen nur Gäste eine Karte oder Anzahlung, die im Spa noch keine abgeschlossene Behandlung hatten. Stammgäste buchen ohne Karte, Gebühr und Anzahlung. Gilt auch für Behandlungen mit eigenen Prozent. Standard: aus.

- Wer in den letzten zwölf Monaten zweimal nicht erschienen ist, gilt hier wieder als neu und muss Karte oder Anzahlung geben.

### Stornoregel {#stornoregel}

Die Regel in eigenen Worten, z. B. „Absage bis 24 Stunden vorher ist kostenlos, danach 50 % des Preises.“ Höchstens 500 Zeichen, leer ist erlaubt.

- Der Text ist für das Team und den [KI-Assistenten](hilfe:ki-assistent-fragen): auf „Was ist unsere Stornoregel?“ nennt er ihn und dazu die festen Werte dieser Seite (Frist der Online-Absage, Ausfallgebühr, Anzahlung).
- Er steuert nichts: die Frist, die Gebühr und die Anzahlung gelten nur über die Felder oben. Der Text erscheint nicht im Kundenbereich.
- Ohne eigene Regeln des Standorts (solange „Es gelten die Vorgaben“ dasteht) und ohne Text sagt der Assistent „Dazu ist nichts hinterlegt“.

### Hinweis für Gäste {#hinweis-fuer-gaeste}

Freier Text, den Gäste im Kundenbereich vor dem Buchen sehen, z. B. „Bitte kommen Sie 10 Minuten vor Ihrem Termin.“ Höchstens 300 Zeichen. Leer erscheint kein Hinweis.

### Vorgaben wiederherstellen {#vorgaben}

Erscheint, sobald der Standort eigene Regeln hat. Löscht die eigenen Regeln; danach gelten wieder die Vorgaben für alle Standorte. Die Vorgaben legt der Betreiber des Servers fest. Ohne Änderung sind das: online buchbar, 120 Minuten Vorlauf, 90 Tage im Voraus, Storno bis 24 Stunden vorher, keine Ausfallgebühr, keine Anzahlung, keine Freigabefrist, Raster 15 Minuten.

## Räume {#raeume}

Die Räume des Standorts, eine Zeile je Raum. Eine neue Zeile wird mit „Hinzufügen“ angelegt, Änderungen an einer bestehenden Zeile mit „Speichern“ in dieser Zeile. Welche Behandlung in welchem Raum stattfinden kann, wird bei der Behandlung festgelegt.

### Name {#raumname}

Name des Raums, z. B. „Raum Zeder“. Pflichtfeld. Er erscheint im Kalender als Spalte bzw. Ressource.

### Raumpflege (min) {#raumpflege}

So viele Minuten bleibt der Raum nach jeder Behandlung (samt Nachbereitung) zusätzlich gesperrt, z. B. zum Lüften und Neubeziehen. Erlaubt sind 0 bis 240 Minuten, Standard 10. Die Person ist in dieser Zeit frei. Eine Änderung gilt für neue Buchungen. Die Vollkosten je Behandlung rechnen die Raumpflege mit dem Stundensatz für die Raumpflege ein.

### Plätze {#plaetze}

Bei einem Behandlungsraum: wie viele Gäste einer gemeinsamen Buchung hier gleichzeitig behandelt werden können, z. B. 2 für eine Paarsuite. Erlaubt sind 1 bis 8, Standard 1. Gemeinsame Buchungen kommen bevorzugt in einen Raum mit genug Plätzen; einzelne Termine nutzen Räume mit mehreren Plätzen zuletzt, damit sie für Paare frei bleiben.

Bei einem Bereich: wie viele Gäste gleichzeitig darin sein dürfen. Erlaubt sind 1 bis 200.

### Bereich {#bereich}

Häkchen für einen Gemeinschaftsbereich wie Sauna, Ruheraum oder Pool. Gäste buchen dort einen Eintritt und teilen sich die Plätze; der Bereich ist für Behandlungen nicht wählbar.

- Ein Raum, der noch Behandlungen zugeordnet ist, kann nicht zum Bereich werden. Zuerst bei den Behandlungen abwählen.
- Ein Bereich, für den es schon Eintritte gibt, kann nicht wieder zum Behandlungsraum werden. Dafür einen neuen Raum anlegen.

### Eintritt {#eintritt}

Nur bei einem Bereich: der Kassa-Artikel, mit dem ein Eintritt je Person abgerechnet wird. Preis, Steuersatz und Erlöskonto kommen vom Artikel. Mit „Nicht an der Kassa“ ist kein Artikel gewählt; dann lässt sich ein Eintritt in diesen Bereich nicht auf eine Rechnung setzen, bis ein Artikel gewählt ist.

### Entfernen {#raum-entfernen}

Nimmt den Raum aus der Planung; in der Windows-App ist es der Papierkorb am Ende der Zeile. Das geht nur, wenn keine künftigen Termine mehr darin liegen; sonst erscheint, wie viele es sind. Diese zuerst verschieben oder stornieren. Vergangene Termine behalten ihren Raum.

## Tagesstart-Kontrolle {#tagesstart-kontrolle}

Unter den Räumen eines Standorts (in der Windows-App nach den Räumen, vor den Geräten): die Checkliste, die das Team vor dem ersten Termin in einem Raum oder Bereich abhakt, z. B. „Sauna aufheizen“. Ändern dürfen Inhaber und Standortleitung an ihren Standorten. Mit **Raum** wählt man den Raum, bei **Punkte** steht je Zeile ein Punkt (höchstens 30, je höchstens 200 Zeichen), **Checkliste speichern** speichert die ganze Liste. Eine leere Liste schaltet die Kontrolle für den Raum aus.

- Entfernte Punkte bleiben im [Hygieneprotokoll](hilfe:hygiene-protokoll-laden) an den Tagen stehen, an denen sie abgehakt wurden.
- Wo abgehakt und abgenommen wird: [Hygiene](hilfe:hygiene).

## Geräte {#geraete}

Geräte des Standorts, die eine Behandlung braucht und die nur einmal gleichzeitig verwendet werden können, z. B. ein Dampfbad-Kabinett oder ein Hydrojet. Neues Gerät: Namen eingeben und „Hinzufügen“.

- Bei der Behandlung wird festgelegt, welche Geräte sie braucht. Ein Gerät ist nur während der Behandlung selbst belegt, nicht während Vor- und Nachbereitung.
- „Entfernen“ nimmt das Gerät aus der Planung. Das geht nur, wenn es für keinen künftigen Termin mehr gebraucht wird.
