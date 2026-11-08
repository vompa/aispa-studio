---
art: einstellung
bereich: Lohn
oberflaeche: verwaltung
rollen: inhaber, manager
seite: verwaltung
---
# Lohn

In der Windows-App liegt der Lohn im Bereich **Verwaltung** › **Team**: die Ansicht **Lohn** übergibt die Monatswerte jeder Person an die Lohnverrechnung (Stunden, davon Sonntags-, Feiertags- und Nachtstunden, dazu Urlaubs-, Krankenstands- und Schulungstage), die Personalnummer jeder Person steht auf ihrer Karte im Reiter **Lohn**. Die Ansicht und alle Einstellungen darin gelten für das ganze Unternehmen, deshalb ändern sie nur Inhaber und Manager. Das Backoffice sieht die Ansicht und exportiert, ändert aber nichts.

Übergeben werden nur Mengen. Löhne, Zuschläge und Abgaben rechnet die Lohnverrechnung selbst, über die Lohnart.

## Export für die Lohnverrechnung {#export}

### Monat {#monat}

Der Abrechnungsmonat, gewählt im Feld **Monat** (die letzten 24 Monate). Vorbelegt ist der Vormonat.

### Monatswerte anzeigen {#monatswerte}

Zeigt je Person die Positionen mit Menge und Lohnart, dazu Hinweise. So entstehen die Werte:

- **Stunden gesamt**: an Tagen mit erfassten Zeiten die erfasste Zeit; sonst die geplante Zeit laut Dienstplan, ohne Dienstplan die Wochenarbeitszeit der Person.
- **davon Feiertag**: Stunden an einem gesetzlichen Feiertag am Standort des Dienstes, nach Bundesland oder Kanton des Standorts.
- **davon Sonntag**: Stunden an einem Sonntag, der kein Feiertag ist. Ein Feiertag am Sonntag zählt nur als Feiertag.
- **davon Nacht**: der Teil eines Dienstes, der in die Nachtzeit fällt (siehe „Nacht ab“ und „Nacht bis“). Nachtstunden zählen zusätzlich zu Sonntag oder Feiertag.
- **Urlaub**, **Krankenstand**, **Schulung**: Tage laut Dienstplan, je Tag einmal, auch wenn am selben Tag ein kurzer Dienst steht.
- Auch Personen, die inzwischen ausgeschieden sind, erscheinen, wenn sie im Monat eingeteilt waren.

Mögliche Hinweise: eine Person ohne Personalnummer, ein Standort ohne Bundesland oder Kanton (dann zählen nur die landesweiten Feiertage), ein noch nicht abgeschlossener Monat (die restlichen Tage kommen aus dem Plan), Diensttage ohne Stempelung und Positionen ohne Lohnart.

### Dateien {#dateien}

- **BMD Lohn**: CSV mit Dienstnehmernummer, Lohnart, Menge, Einheit und Abrechnungsmonat. Die Feldzuordnung legt man in BMD einmal als Importvorlage an.
- **DATEV Lodas**: Textdatei für den ASCII-Import der Bewegungsdaten, mit Berater- und Mandantennummer im Kopf.
- **DATEV Lohn und Gehalt**: Textdatei mit Berater- und Mandantennummer und Monat in der ersten Zeile, danach je Zeile Personalnummer, Lohnart und Wert.

In die Dateien kommen nur Personen mit Personalnummer und nur Positionen mit Lohnart. Beim Klick auf einen Dateinamen fragt Windows, wo die Datei liegen soll; die Datei ist dieselbe, die der bisherige Export im Browser geladen hat.

## Personalnummern {#personalnummern}

Die Nummer jeder Person bei der Lohnverrechnung. Sie steht auf der Karte der Person im Reiter **Lohn** und wird mit **Speichern** der Karte (Strg+S) gesichert, zusammen mit den übrigen Änderungen der Karte.

- Höchstens 20 Zeichen. Jede Nummer darf nur einmal vergeben sein.
- Ein leeres Feld entfernt die Nummer. Ohne Nummer wird für die Person nichts übergeben.
- Unter der Nummer stehen die Monatswerte der Person für den gewählten Monat.
- Ausgeschiedene Personen stehen unter **Ausgeschiedene** und sind nur lesbar.

## Einstellungen {#einstellungen}

Gespeichert wird mit **Speichern** unter den Einstellungen (Strg+S). Die Einstellungen gelten für das ganze Unternehmen.

### Feiertage {#feiertage}

Österreich oder Deutschland. Vorgabe Österreich.

- Das Land wird nur für Standorte verwendet, die weder ein Bundesland oder einen Kanton (unter „Verwaltung“ › „Betrieb“ › „Standorte & Räume“, Feld „Bundesland / Kanton“) noch eine Kassa haben. Sonst bestimmt das Bundesland bzw. Kanton oder das Land der Kassa die Feiertage.
- Es wirkt auf die Feiertagsstunden hier und auf die Feiertage, die das Programm für solche Standorte anzeigt.

### Nacht ab und Nacht bis {#nachtzeit}

Beginn und Ende der Nachtzeit für die Position „davon Nacht“, als Uhrzeit. Vorgabe 22:00 bis 06:00. Beginn und Ende müssen sich unterscheiden; eine Nachtzeit über Mitternacht ist möglich.

Welche Nachtzeit für Zuschläge gilt, regeln Gesetz und Kollektivvertrag; mit der Lohnverrechnung abstimmen.

### DATEV-Beraternummer {#datev-beraternummer}

Bis zu 7 Ziffern, leer ist erlaubt. Steht im Kopf der Dateien für DATEV LODAS und DATEV Lohn und Gehalt.

### DATEV-Mandantennummer {#datev-mandantennummer}

Bis zu 5 Ziffern, leer ist erlaubt. Steht im Kopf der Dateien für DATEV LODAS und DATEV Lohn und Gehalt.

### Lohnarten {#lohnarten}

Je Position die Lohnart, unter der die Lohnverrechnung die Menge abrechnet: „Lohnart Stunden“, „Lohnart Sonntag“, „Lohnart Feiertag“, „Lohnart Nacht“, „Lohnart Urlaub“, „Lohnart Krankenstand“ und „Lohnart Schulung“.

- Bis zu 10 Ziffern oder Buchstaben.
- Leer heißt: diese Position wird nicht übergeben.
- Vorbelegt sind Platzhalter: 100 (Stunden), 110 (Sonntag), 120 (Feiertag), 130 (Nacht), 200 (Urlaub), 210 (Krankenstand), 220 (Schulung). Die richtigen Lohnarten legt die Lohnverrechnung bzw. Steuerberatung fest.
- Stunden gehen in Stunden hinaus, Urlaub, Krankenstand und Schulung in Tagen.
