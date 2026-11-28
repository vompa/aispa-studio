---
art: einstellung
bereich: Kassa
oberflaeche: verwaltung
rollen: leitung, inhaber
seite: verwaltung
stichwoerter: Kassen, Kassen-ID, Übungskassa, Testkassa, Signatur, Anbieter, RKSV, TSE, FinanzOnline, FON, ELSTER, Startbeleg, Jahresbeleg
---
# Kassa einrichten

Unter **Verwaltung** › **Kassa & Geld** › **Kassen** stehen die Kassen mit Standort, Status und Signatureinrichtung. **Kassa anlegen** öffnet den Assistenten. Solange die Kassa noch offen oder im Übungsbetrieb ist, führt auch **Kassa einrichten** auf der gleichnamigen Karte unter [Firmendaten](hilfe:firmendaten#kassa-einrichten) dorthin.

## Kassen und Bereitschaft {#kassen}

Die ausgewählte Kassa zeigt bei Bedarf, warum sie noch keine Belege ausstellen kann. **Außer Betrieb nehmen** beendet das Kassieren an dieser Kassa. Wer den Standort verwalten darf, darf dort Kassen anlegen und außer Betrieb nehmen, Zugangsdaten verwalten, Registrierungsdaten abrufen und Bestätigungen pflegen.

![Kassenverwaltung mit einer österreichischen Übungskassa und einer Schweizer Kassa für den Echtbetrieb](images/app/verwaltung-kassen-aktiv.png)

Die Karte „Kassa einrichten“ zeigt den Stand an dieser Stelle.

- „Offen“ bedeutet, dass hier eine verwendbare Kassa fehlt. Die Karte nennt den Grund, auch wenn bereits eine Kassa angelegt ist.
- „Übungsbetrieb“ bedeutet, dass hier eine Übungskassa verwendbar ist.
- „Bereit“ bedeutet, dass hier eine aktive Kassa für den Echtbetrieb Belege ausstellen kann. In Österreich und Deutschland gehören dazu ein Startbeleg und eine erreichbare Signatureinrichtung.

## Angaben im Assistenten {#angaben}

- „Standort“ ordnet die Kassa einem Standort zu. „Land der Kassa“ bestimmt Kassenrecht, Steuersätze und Signatureinrichtung.
- „Signatureinrichtung“ zeigt die verfügbaren Anbieter. Österreich verwendet „AT0-Testeinrichtung“, Deutschland verwendet „Test-TSE“ und die Schweiz verwendet „Ohne Signatureinrichtung“. Diese Auswahlmöglichkeiten brauchen keine Zugangsdaten. Für den Echtbetrieb in Österreich und Deutschland braucht es einen echten Signaturanbieter. In dieser Version stehen dort Testanbieter für Übungskassen zur Verfügung.
- Die Testanbieter legen immer eine Übungskassa an. In der Schweiz ist „Übungsbetrieb wählen“ eine eigene Auswahl. „Anlage als Übungskassa bestätigen“ ist bei jeder Übungskassa erforderlich.
- „Kassen-ID“ ist die Kennung der Kassa. „Belegkopf“ enthält den Firmennamen und die Anschrift, wie sie auf den Belegen stehen.

![Assistent mit Standort „Salzburg“ und Land der Kassa „Österreich“](images/app/kassa-einrichten-standort.png)

![AT0-Testeinrichtung mit bestätigter Anlage als Übungskassa](images/app/kassa-einrichten-uebungsbetrieb.png)

![Kassen-ID und Belegkopf einer neuen Kassa](images/app/kassa-einrichten-belegkopf.png)

![Österreichischer Abschluss des Assistenten mit Abschnitten für FinanzOnline und Bestätigungen](images/app/kassa-einrichten-at.png)

![Deutscher Abschluss des Assistenten mit ELSTER-Hinweis für die Übungskassa](images/app/kassa-einrichten-de.png)

![Schweizer Abschluss des Assistenten für eine Kassa im Echtbetrieb](images/app/kassa-einrichten-ch.png)

## Übungskassa {#uebungskassa}

Eine Übungskassa bleibt dauerhaft im Übungsbetrieb. Für den Wechsel zum Echtbetrieb die Übungskassa außer Betrieb nehmen und eine eigene Kassa mit neuer „Kassen-ID“ anlegen. Diese beginnt mit einer eigenen Belegkette.

Übungsbelege tragen den Hinweis „ÜBUNGSKASSA – kein gültiger Beleg“. Übungsumsätze bleiben in Berichten, Abschlüssen und Exporten enthalten und gekennzeichnet. Bei vorgeschriebenen Exportformaten kann der Hinweis in einer Begleitinformation stehen.

![Tagesabschluss einer Übungskassa mit dauerhaftem Übungsband und ausgewiesenen Umsätzen](images/app/kassa-uebungsabschluss.png)

## Signatureinrichtung und Zugangsdaten {#signatureinrichtung}

**Verbindung prüfen** prüft die gewählte Signatureinrichtung und zeigt das Ergebnis in der Kassenverwaltung. Bei einem eingebauten Testanbieter prüft sie den Testbetrieb.

Für Anbieter mit Zugangsfeldern zeigt der Abschnitt den maskierten Stand der gespeicherten Zugangsdaten. Bei einer Neuanlage benötigen solche Anbieter die Pflichtangaben bereits für den Startbeleg. Für neue Zugangsdaten alle Felder ausfüllen und **Zugangsdaten speichern** wählen. **Zugangsdaten löschen** entfernt die hinterlegten Angaben. Die App ruft den Stand nur mit Verbindung zum Haupt-PC oder Server ab. Zugangsdaten sind nur online verfügbar.

## Für FinanzOnline {#finanzonline}

Diesen Abschnitt gibt es nur bei österreichischen Kassen. **Registrierungsdaten anzeigen** ruft die Werte online ab. **Kopieren** übernimmt den Wert des jeweiligen Felds in die Zwischenablage.

- „Kassen-ID“ ist die Kennung der Kassa.
- „Umsatzzählerschlüssel“ ist der AES-Schlüssel. „Prüfwert“ ist der zugehörige Kontrollwert. Der Schlüssel ist nur online verfügbar.
- „Anbieterkennung“ bezeichnet die Signatureinrichtung. „Zertifikatsseriennummer“ erscheint, wenn der Anbieter sie liefert.

> Achtung: Daten einer Übungskassa nicht in FinanzOnline registrieren.

![FinanzOnline-Abschnitt mit öffentlichen Musterwerten für Umsatzzählerschlüssel und Prüfwert](images/app/verwaltung-finanzonline-musterwerte.png)

Das BMF erklärt die Anmeldung im Handbuch [„Registrierkassen in FinanzOnline“](https://finanzonline.bmf.gv.at/eLearning/BMF_Handbuch_Registrierkassen.pdf) und den Prüfwert in den [Detailfragen zur RKSV](https://www.bmf.gv.at/dam/jcr%3A66a8edac-3dbf-4e67-b768-634ce2e5bb1e/20160905-Detailfragen-RKSV-V1.2.pdf).

## Bestätigungen {#bestaetigungen}

Bei österreichischen Kassen können berechtigte Personen die „Registrierung in FinanzOnline“ und die Prüfung des Startbelegs und des jeweiligen Jahresbelegs festhalten. Diesen Abschnitt bei einer Übungskassa nur zur Orientierung verwenden.

**Bestätigen** speichert Datum und Person. **Zurücknehmen** nimmt die Bestätigung zurück. Die Meldung und die Prüfung erledigt eine Person außerhalb der App. Der gespeicherte Stand dokumentiert deren Bestätigung.

### Meldung in Deutschland {#elster}

Bei einer deutschen Kassa für den Echtbetrieb dokumentiert „Meldung über ELSTER“ die Bestätigung der zuständigen Person. Die Meldung erfolgt außerhalb der App. Eine Übungskassa dort nicht melden.

## Österreichischer Jahresbeleg {#jahresbeleg}

Die App kann den Jahresbeleg auf drei Wegen erstellen.

- Ab 15. Dezember lässt sich der Beleg für das laufende Abschlussjahr über **Jahresbeleg erstellen** manuell anlegen, sobald die Kassa dafür bereit ist.
- Am 31. Dezember prüft der laufende Haupt-PC oder Server ab 23:30 Uhr Ortszeit automatisch, ob der Abschluss fehlt. Im Einzelplatz muss die App dafür laufen.
- Fehlt der Abschluss danach noch, löst der erste folgende Beleg die Erstellung aus.

Ein weiterer Umsatz im Dezember macht einen vorgezogenen Jahresbeleg als Abschluss unvollständig. Die App zeigt Abschluss und Prüfung wieder als offen. Die Bestätigung gilt für das Abschlussjahr und den konkreten gültigen, signierten Beleg. Das Ausstellungsdatum kann im Folgejahr liegen.

Die Hinweise beginnen am 15. Dezember und erinnern danach an die Prüfung bis 15. Februar des Folgejahres. **Beleg anzeigen** öffnet den Beleg zur Prüfung.

> Achtung: Warnt die App vor einem fehlenden gültigen Abschluss oder einem unklaren oder falschen Zählerstand aus einem früheren Jahr, den Sachverhalt zuerst klären. Einen solchen Beleg nicht als gültigen Vorjahresabschluss bestätigen.

Die amtlichen Hinweise zum Jahresbeleg und zum letzten Umsatztag stehen im [Erlass des BMF](https://findok.bmf.gv.at/findok/volltext?segmentId=b30705c6-b74c-4a24-a33b-db227df155a6).

## Verwandte Einstellungen {#einstellungen}

Die Regeln für Rabatte gelten für alle Kassen des Spas und stehen unter [Kassa](hilfe:kassa#rabatte). Das Kassieren und den Tagesabschluss beschreibt die [Seite „Kassa“](hilfe:seite-kassa).
