---
art: einstellung
bereich: Betriebsinformationen
oberflaeche: verwaltung
rollen: rezeption, therapeut, leitung, inhaber, manager, backoffice
seite: verwaltung
stichwoerter: Betriebsinformationen, Betriebsinfo, Kontakte, Ansprechpartner, Ansprechperson, Versicherung, Polizze, Polizzennummer, Schadenshotline, Handwerker, Installateur, Elektriker, Techniker, Lieferant, Behörde, Buchhaltung, Lohnverrechnung, Steuerberatung, Steuerberater, Vertrauensperson, Notfall, Notdienst, zuständig, Zuständigkeit, Telefonnummer, Nummer, Vertragsnummer, Kundennummer, Leitung, Vertraulichkeit, business contacts, insurance, craftsman, plumber, emergency contact, accountant, payroll, responsible
---
# Betriebsinformationen

Die Betriebsinformationen sind das Verzeichnis der Kontakte rund um den Betrieb: Versicherung, Installateur, Elektriker, Lieferanten, Buchhaltung, Lohnverrechnung, Steuerberatung, Notdienste und Vertrauenspersonen außerhalb des Betriebs, jeweils mit Ansprechperson, Telefon, Mail und dem, wofür sie zuständig sind („Wasserschaden, Heizung“). Gäste stehen nicht hier, sondern in der Kundenkartei.

**Keine Passwörter oder Zugangsdaten eintragen.** Das Verzeichnis ist für Nummern und Zuständigkeiten gedacht, nicht für Geheimnisse.

Im Reiter „Betriebsinfo“ lesen Inhaber, Manager, Standortleitung, das Backoffice und die Rezeption; die Rezeption sieht nur Einträge mit der Sichtbarkeit „Team“. Eintragen, ändern und archivieren dürfen Inhaber, Manager und Standortleitung (Recht „Standort verwalten“), die Standortleitung nur Einträge ihrer eigenen Standorte. Der Assistent beantwortet damit Fragen wie „Wer ist unser Installateur?“, „Nummer der Versicherung“ oder „Wer ist für die Heizung zuständig?“ ([KI-Assistent fragen](hilfe:ki-assistent-fragen#kontakte)).

## Nachschlagen und verwalten {#verwalten}

1. In der Windows-App unter „Verwaltung“ **Zahlen** öffnen und den Reiter **Betriebsinfo** wählen.
2. Links stehen die Einträge nach Kategorie. Mit **Suchen** (Bezeichnung, Firma, Ansprechperson, Zuständigkeit) und **Kategorie** engt man die Liste ein.
3. Einen Eintrag anklicken. Rechts stehen Telefon, Mobil und E-Mail als Links zum Anrufen und Schreiben; **Kopieren** legt die Nummer oder die Adresse in die Zwischenablage, **Alles kopieren** alle Angaben.
4. Wer verwalten darf, ändert die Felder und klickt auf **Speichern**, oder legt mit **Neuer Eintrag** einen neuen an. Mit **Archivieren** verschwindet ein Eintrag aus der Liste; über **Archivierte zeigen** und **Zurückholen** kommt er wieder.

Archivieren löscht nichts: der Eintrag bleibt erhalten, steht aber nicht mehr in der Liste, und der Assistent nennt ihn nicht mehr.

![Betriebsinformationen nach Kategorie](images/app/verwaltung-betriebsinfo.png)

## Eintrag {#eintrag}

- **Kategorie**: Versicherung, Handwerker und Technik, Lieferant, Behörde, Buchhaltung, Lohnverrechnung, Steuerberatung, Vertrauensperson, Notfall oder Sonstiges. Die Kategorie ordnet die Liste und lässt den Assistenten eingrenzen („Nummer der Versicherung“).
- **Bezeichnung**: wofür der Eintrag steht, z. B. „Installateur“. Pflicht, höchstens 80 Zeichen.
- **Firma** und **Ansprechperson**: Name der Firma und der Person, die man dort anruft.
- **Zuständig für**: Freitext, wofür man den Kontakt anruft, z. B. „Wasserschaden, Heizung“. Der Assistent sucht auch hier („Wer ist für die Heizung zuständig?“).

Mindestens eines von Firma, Ansprechperson, Telefon, Mobil und E-Mail muss ausgefüllt sein, sonst wüsste niemand, wen man erreicht.

## Erreichbarkeit {#erreichbarkeit}

- **Telefon** und **Mobil**: mit Landesvorwahl, z. B. „+43 1 234 56 78“. Erlaubt sind Ziffern, Leerzeichen und die Zeichen + ( ) / . -; mindestens drei Ziffern.
- **E-Mail**: eine gültige Mailadresse.
- **Adresse**: Anschrift als Freitext.

Telefon und Mail erscheinen in der Liste und im Eintrag als Links (`tel:` und `mailto:`): ein Klick ruft an bzw. öffnet das Mailprogramm, sofern der Computer das kann.

## Versicherung {#versicherung}

Diese Gruppe erscheint nur bei der Kategorie **Versicherung**.

- **Versicherer**: die Gesellschaft, bei der die Polizze liegt.
- **Polizzennummer**: die Nummer der Polizze.
- **Schadenshotline**: die Nummer, die man im Schadensfall anruft.
- **Deckung**: frei beschrieben, z. B. „Betriebshaftpflicht bis 3 Mio. EUR, Selbstbehalt 500 EUR“.

Wechselt man die Kategorie, fallen die Versicherungsfelder beim Speichern weg.

## Sichtbarkeit und Standorte {#sichtbarkeit}

- **Team**: alle Zugänge lesen den Eintrag (Rezeption, Therapeut:innen, Standortleitung, Manager, Inhaber, Backoffice).
- **Leitung**: nur Inhaber, Manager, Standortleitung und Backoffice lesen den Eintrag. Für alle anderen schickt der Server ihn gar nicht erst; auch der Assistent nennt ihn ihnen nicht und antwortet „Dafür fehlt die Berechtigung“. Geeignet für Polizzennummern, Steuerberatung, Lohnverrechnung und Vertrauenspersonen.
- **Alle Standorte** oder **Ausgewählte Standorte**: wo der Eintrag gilt. Einträge für alle Standorte legt nur der Inhaber (oder Manager) an; die Standortleitung liest sie, ändert aber nur Einträge, die ganz in ihren Standorten liegen. Der Assistent nennt am gefragten Standort die Einträge für alle Standorte und die für diesen Standort ausgewählten.

## Weitere Angaben {#weitere-angaben}

- **Kunden-/Vertragsnummer**: die Nummer, unter der das Spa dort geführt wird. Nur eintragen, wenn man sie am Telefon nennen muss.
- **Notizen**: alles Weitere, z. B. „Schaden innerhalb von 48 Stunden melden.“ Keine Passwörter oder Zugangsdaten.

![Eintrag bearbeiten](images/app/verwaltung-betriebsinfo.png)
