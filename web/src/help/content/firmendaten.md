---
art: einstellung
bereich: Stammdaten
oberflaeche: verwaltung
rollen: inhaber, manager
seite: verwaltung
---
# Firmendaten

Einstellungen in der Windows-App unter „Verwaltung“ › „Betrieb“ › „Firmendaten“. Hier stehen die Angaben zum Unternehmen, das das Spa betreibt. Sie werden einmal für alle Standorte gepflegt. Sehen und ändern darf sie nur, wer die Firmendaten pflegen darf (Inhaber). Gespeichert wird alles gemeinsam mit „Speichern“.

In der Windows-App:

- **Strg+S** speichert wie der Knopf „Speichern“.
- **Verwerfen** holt den gespeicherten Stand zurück.
- Wer die Firmendaten mit ungespeicherten Änderungen verlässt, etwa zum Kalender wechselt, wird gefragt: „Änderungen speichern?“ mit „Speichern“, „Verwerfen“ und „Abbrechen“. „Abbrechen“ bleibt in der Maske.
- Der Abschnitt „Kundenbereich einbetten“ wird mit demselben „Speichern“ gespeichert.
- **Verlauf** oben in der Maske öffnet rechts eine Seitenleiste: wer die Firmendaten wann geändert hat (siehe unten).

## Betrieb einrichten {#betrieb-einrichten}

Solange die Ersteinrichtung nicht fertig ist, steht oben in der Maske die Karte **Betrieb einrichten**. Sie zählt, welche Schritte erledigt sind: Standort, Öffnungszeiten, Räume, Behandlungen, Team und Kassa. Ein Schritt gilt als erledigt, sobald er für das Buchen taugt: Im Team zählt zum Beispiel nur eine Person mit Arbeitszeiten, die eine Behandlung durchführen kann. **Öffnen** springt in die passende Maske; fehlt dafür das Recht, erscheint ein Hinweis. Sind alle Schritte erledigt, verschwindet die Karte. Sie ist keine Pflicht und sperrt nichts; die Reihenfolge ist ein Vorschlag.

### Assistent {#betrieb-einrichten-assistent}

Der Assistent führt Schritt für Schritt durch dieselben Punkte wie die Karte, bis sich der erste Termin buchen lässt. Er öffnet sich einmal von selbst, wenn **Spa einrichten** fertig ist, und danach mit **Assistent starten** auf der Karte. Er beginnt beim ersten offenen Schritt und zeigt „Schritt 2 von 6“. Was fehlt, steht im Schritt; ist er erledigt, steht dort ein Haken.

- **Öffnungszeiten**: Die Tage mit Öffnung sind angehakt, dazu stehen Beginn und Ende. Hat der Standort noch keine Zeiten, steht Montag bis Samstag von 09:00 bis 18:00 als Vorschlag da; er gilt erst nach **Speichern**, und mindestens ein Tag muss geöffnet sein. **Für alle Tage übernehmen** setzt die Zeiten des ersten geöffneten Tages für alle geöffneten Tage. **Speichern** übernimmt die Zeiten für den Standort; bei mehreren Standorten gilt der erste offene, sein Name steht in der Überschrift.
- **Räume**: Die Liste zeigt die vorhandenen Behandlungsräume. Ein Name im Feld und **Raum hinzufügen** legen einen neuen Raum an.
- **Behandlungen**: Der Startkatalog bietet vier Pakete typischer Behandlungen an, jedes mit Dauer, aber ohne Preise. Ein Häkchen wählt ein Paket, schon vorhandene Behandlungen sind als „vorhanden“ gekennzeichnet und werden nie doppelt angelegt. Die Räume darunter sind alle vorgewählt; die Auswahl gilt für alle gewählten Pakete. **Behandlungen anlegen** legt sie an und nennt, wie viele neu sind und wie viele übersprungen wurden. Preise und eigene Behandlungen kommen danach in der Verwaltung dazu, **In der Verwaltung öffnen** führt dorthin. Ohne Behandlungsraum bleibt der Knopf gesperrt, der Hinweis verweist auf den Schritt Räume. Bei mehreren Standorten steht der Standort, für den der Assistent die Räume und Zeiten bearbeitet, in der Überschrift der Schritte Öffnungszeiten, Räume, Behandlungen und Team.
- **Team**: Die Liste zeigt die vorhandenen Personen. Vorname und Nachname sind Pflicht; alle Qualifikationen sind vorgewählt, was die Person nicht kann, lässt sich abwählen. **Arbeitet zu den Öffnungszeiten** übernimmt die gespeicherten Öffnungszeiten des Standorts als Arbeitszeit, **Person hinzufügen** legt die Person ohne Zugang an. Zugänge und feinere Arbeitszeiten stehen in der Verwaltung.
- **Standort und Kassa**: Der Schritt zeigt, was fehlt. **In der Verwaltung öffnen** führt in die Maske, in der es eingetragen wird; der Assistent schließt sich dabei.

**Fertig**: Hinter dem letzten Schritt prüft die Abschlussseite, ob sich in den nächsten 14 Tagen eine Behandlung buchen lässt. Ist das so, nennt sie die Behandlung und den ersten freien Platz mit Tag, Uhrzeit und Standort; **Probetermin im Kalender** schließt den Assistenten und öffnet den Kalender an diesem Tag und Standort. Wenn nicht, steht der Grund da, und **Zurück** führt in die Schritte. Darunter stehen die noch offenen Schritte, etwa die Kassa oder fehlende Preise, jeder mit **Zum Schritt**. Kein Schritt hält auf. Der ganze Ablauf steht unter [Erste Schritte](hilfe:erste-schritte).

**Weiter** geht zum nächsten Schritt, **Zurück** zum vorigen, **Überspringen** lässt einen offenen Schritt aus, **Schließen** beendet den Assistenten. Nichts davon ist Pflicht: Kein Schritt hält auf, und die Karte bleibt, bis alles erledigt ist. Esc schließt den Assistenten nur, solange nichts Ungespeichertes in einem Feld steht. Nach jedem Speichern lädt der Assistent den Stand neu.

## Firmendaten {#firmendaten}

Wo die Angaben verwendet werden:

- **Belege und Rechnungen**: Firmenwortlaut, UID-Nummer sowie Firmenbuchnummer und Firmenbuchgericht stehen auf jedem Beleg der Kassa, gedruckt und am Bildschirm. Was schon im Belegkopf der Kassa steht, wird nicht doppelt gedruckt.
- **Sofortüberweisung an der Kassa**: Die Bankverbindung füllt den QR-Code für die Überweisung.
- **Impressum der Online-Buchung**: Gäste sehen dort Name, Firmenwortlaut, Anschrift, Kontakt, Register- und Impressumsangaben. Die Bankverbindung erscheint dort nicht.

Ein Standort, der zu einer anderen Gesellschaft gehört, trägt unter „Standorte & Räume“ bei „Eigene Gesellschaft“ eigene Angaben ein. Dann gelten für Belege dieses Standorts nur diese Angaben, und für die Sofortüberweisung wird die Bankverbindung aus den Firmendaten nicht verwendet. Angaben zweier Gesellschaften werden nie gemischt.

Alle Felder außer dem Namen des Spas und dem Land sind freiwillig. Leere Felder erscheinen nirgends.

### Name des Spas {#name}

Der Name, unter dem das Spa auftritt, z. B. „Day Spa Huber“. Pflichtfeld. Er erscheint unter anderem als Absendername in Mails an Gäste, im Betreff des Wiederbuchungs-Hinweises, in Hinweis-Mails an das Team und im Impressum der Online-Buchung. Fehlen Kontoinhaber und Firmenwortlaut, steht er auch als Empfänger im QR-Code der Sofortüberweisung.

### Land des Unternehmenssitzes {#land}

Österreich, Deutschland oder Schweiz. Pflichtfeld; beim Anlegen des Spas gewählt.

- Danach richten sich die Hinweise zur Arbeitszeit im Dienstplan (Höchstarbeitszeit, Ruhezeit, Pausen, Vorlauf für die Einteilung).
- Es erscheint im Impressum und wird bei der Einrichtung der Online-Zahlungen an den Zahlungsanbieter übergeben.
- Steuer und Signatur an der Kassa richten sich nach dem Land, das bei der jeweiligen Kassa eingestellt ist, nicht nach diesem Feld.

## Unternehmen {#unternehmen}

Angaben, die das Gesetz auf Rechnungen und im Impressum verlangt.

### Firmenwortlaut mit Rechtsform {#firmenwortlaut}

Der vollständige Name des Unternehmens laut Firmenbuch bzw. Handelsregister, mit Rechtsform, z. B. „Day Spa Huber GmbH“. Höchstens 200 Zeichen. Steht auf Belegen und im Impressum. Für das Impressum ist er nötig.

### UID-Nummer {#uid}

Die Umsatzsteuer-Identifikationsnummer, z. B. „ATU12345678“. Höchstens 20 Zeichen; wird in Großbuchstaben gespeichert. Steht auf Belegen als „UID …“ und im Impressum.

### Firmenbuch- bzw. Handelsregisternummer {#firmenbuchnummer}

In Österreich die Firmenbuchnummer (z. B. „FN 123456a“), in Deutschland die Handelsregisternummer, in der Schweiz die Nummer im Handelsregister. Höchstens 30 Zeichen. Steht auf Belegen und im Impressum.

### Firmenbuchgericht {#firmenbuchgericht}

Das Gericht, das das Firmenbuch bzw. Handelsregister führt, z. B. „Handelsgericht Wien“. Höchstens 100 Zeichen. Steht auf Belegen nach der Firmenbuchnummer und im Impressum.

## Sitz und Kontakt {#sitz-und-kontakt}

Anschrift und Kontakt des Unternehmens für das Impressum der Online-Buchung. Die Anschrift einzelner Standorte für Gäste wird beim Standort gepflegt.

- Sind Straße und Ort leer, nimmt das Impressum die Anschrift des ersten Standorts (nach Namen sortiert).
- Sind Telefon oder E-Mail leer, nimmt das Impressum die des ersten Standorts.

### Straße und Nummer {#strasse}

Straße und Hausnummer des Unternehmenssitzes. Höchstens 200 Zeichen. Für das Impressum nötig.

### PLZ {#plz}

Postleitzahl des Sitzes. Höchstens 10 Zeichen.

### Ort {#ort}

Ort des Sitzes. Höchstens 100 Zeichen. Für das Impressum nötig.

### Telefon {#telefon}

Telefonnummer des Unternehmens. Höchstens 40 Zeichen. Erscheint im Impressum.

### E-Mail {#email}

Mailadresse des Unternehmens. Muss eine gültige Mailadresse sein. Erscheint im Impressum und ist dafür nötig. Sie wird auch bei der Einrichtung der Online-Zahlungen an den Zahlungsanbieter übergeben.

### Website {#website}

Adresse der eigenen Website, am besten vollständig mit „https://“. Höchstens 200 Zeichen. Erscheint im Impressum.

## Bankverbindung {#bankverbindung}

Das Konto, auf das Gäste per Sofortüberweisung an der Kassa zahlen. An der Kassa erscheint dazu ein QR-Code mit Empfänger, IBAN und offenem Betrag, den der Gast mit seiner Banking-App scannt.

- Eine Kassa mit eigenem Konto verwendet ihres. Ein Standort mit eigener Gesellschaft verwendet die Bankverbindung aus den Firmendaten nicht.
- Sofortüberweisung gibt es nur in Euro. Ohne IBAN (in den Firmendaten oder an der Kassa) ist sie nicht möglich.
- Im Impressum erscheint die Bankverbindung nicht.
- Später soll die Bankverbindung auch für Zahlungsanbieter dienen.

### Kontoinhaber {#kontoinhaber}

Name des Kontoinhabers, wie bei der Bank geführt. Höchstens 70 Zeichen. Steht als Empfänger im QR-Code. Leer nimmt das Programm den Firmenwortlaut, sonst den Namen des Spas.

### IBAN {#iban}

Die IBAN des Kontos. Leerzeichen sind erlaubt und werden beim Speichern entfernt. Das Programm prüft die Prüfziffer; eine falsche IBAN lässt sich nicht speichern.

### BIC {#bic}

Der BIC der Bank, höchstens 11 Zeichen. Freiwillig; wird in Großbuchstaben gespeichert und in den QR-Code übernommen.

## Impressum {#impressum}

Diese Angaben ergänzen das Impressum der Online-Buchung. Firmenwortlaut, Sitz und E-Mail stehen dort ohnehin.

Fehlen Firmenwortlaut, Straße, Ort oder E-Mail (auch nach dem Rückgriff auf den ersten Standort), zeigt das Impressum den Gästen den Hinweis, dass das Spa die Angaben gerade vervollständigt.

### Unternehmensgegenstand {#unternehmensgegenstand}

Womit sich das Unternehmen beschäftigt, z. B. „Betrieb eines Day-Spas“. Höchstens 300 Zeichen.

### Gewerbe- bzw. Aufsichtsbehörde {#aufsichtsbehoerde}

Die Behörde, bei der das Gewerbe angemeldet ist bzw. die das Unternehmen beaufsichtigt, z. B. das zuständige Magistratische Bezirksamt oder die Bezirkshauptmannschaft. Höchstens 200 Zeichen.

### Kammer und berufsrechtliche Vorschriften {#kammer}

Kammerzugehörigkeit und die geltenden berufsrechtlichen Vorschriften mit Fundstelle, z. B. „Mitglied der WKO; Gewerbeordnung, www.ris.bka.gv.at“. Höchstens 300 Zeichen.

## Verlauf {#verlauf}

In der Windows-App öffnet der Knopf **Verlauf** oben in den Firmendaten rechts eine Seitenleiste; **Schließen** oder **Esc** schließt sie wieder. Sie zeigt jede Änderung an Name, Land und den Firmendaten mit altem und neuem Wert, der Person und dem Zeitpunkt, die neueste oben, z. B. „Geändert von Lena: Name Day-Spa Wien → Day-Spa am Ring“.

- Andere Einstellungen des Spas (z. B. Kundenbereich einbetten, Kassa) stehen hier nicht.
- Den Verlauf sieht, wer die Firmendaten ändern darf.
- Einträge bleiben drei Jahre.

## Kundenbereich einbetten {#einbetten}

Mit dieser Einstellung unter „Firmendaten“, Abschnitt „Kundenbereich einbetten“ (in der Windows-App unter „Verwaltung“ › „Betrieb“ › „Firmendaten“), legt das Spa fest, auf welchen Websites die Online-Buchung in einem Rahmen (`iframe`) erscheinen darf. Sehen und ändern darf sie, wer die Firmendaten pflegen darf (Inhaber). „Speichern“ gilt gleich, einen Neustart braucht es nicht.

- Ohne Eintrag lässt sich die Online-Buchung nur auf ihrer eigenen Adresse zeigen. Eine fremde Website, die sie einbettet, zeigt einen leeren Rahmen.
- Der Rahmen braucht die Adresse mit Spa-Kennung, genau wie der Link zur Online-Buchung, z. B. `https://buchung.beispiel.at/buchen/?spa=…`. Ohne `spa=` erkennt die Seite das Spa nur, wenn das Programm genau ein Spa führt.
- Der Browser der Besucherin prüft die Liste beim Laden des Rahmens. Eine Änderung wirkt für Seiten, die danach geladen werden.

### Code kopieren {#einbetten-code}

In der Windows-App legt der Knopf „Code kopieren“ unter der Liste den fertigen Code für die Website in die Zwischenablage, mit der Adresse der Online-Buchung dieses Spas und ihrer Spa-Kennung. Daneben steht „Code kopiert.“. Den Code fügt man in die Seite der eigenen Website ein, dort wo die Online-Buchung erscheinen soll. Der Knopf erscheint, sobald AISPA Studio die Adresse nennt, und braucht kein „Speichern“; die Website, die den Code zeigt, muss aber in der Liste stehen.

### Einbetten erlaubt auf {#einbetten-erlaubt-auf}

Die Websites, die den Kundenbereich einbetten dürfen, eine Adresse je Zeile, höchstens 10.

- Erlaubt ist nur die Adresse der Website mit `https://`, z. B. `https://www.beispiel-spa.at`, bei Bedarf mit Port (`https://beispiel-spa.at:8443`). Ein Schrägstrich am Ende wird entfernt, Großbuchstaben im Namen werden klein, doppelte Einträge entfallen.
- Nicht erlaubt sind `http://`, ein Pfad (`/buchen`), eine Abfrage oder ein Anker, Zugangsdaten in der Adresse und Platzhalter wie `*.beispiel-spa.at`. Die Meldung am Feld nennt die Zeile und den Grund; solange eine Zeile falsch ist, wird nichts gespeichert.
- Trage jede Website einzeln ein, auch `www.beispiel-spa.at` und `beispiel-spa.at` getrennt, denn das sind für den Browser zwei Websites.
