---
art: ablauf
bereich: Lohn
oberflaeche: verwaltung
rollen: inhaber, manager
seite: verwaltung
stichwoerter: Lohnexport, Lohnverrechnung, Monatsabschluss, Stunden übergeben, Lohndatei, BMD Lohn, DATEV LODAS, DATEV Lohn und Gehalt, Monatswerte, Sonntagsstunden, Feiertagsstunden, Nachtstunden, Urlaubstage, Krankenstandstage, payroll export, wages
---
# Lohnexport für den Monat erstellen

In der Ansicht „Lohn“ des Bereichs „Team“ entsteht für einen Monat eine Datei mit Stunden und Abwesenheiten jeder Person, die man der Lohnverrechnung schickt.

## Monatswerte prüfen {#pruefen}

1. **Lohn** öffnen (Windows-App: **Verwaltung**, dann **Team**, oben die Ansicht **Lohn**).
2. Im Feld **Monat** den Abrechnungsmonat wählen. Vorbelegt ist der Vormonat.
3. **Monatswerte anzeigen** anklicken.
4. Die Hinweise über der Tabelle lesen, z. B. eine Person ohne Personalnummer, Diensttage ohne Stempelung oder ein noch nicht abgeschlossener Monat.
5. Die Tabelle prüfen: je Person die Positionen (Stunden gesamt, davon Sonntag, davon Feiertag, davon Nacht, Urlaub, Krankenstand, Schulung) mit Menge und Lohnart.

## Datei herunterladen {#datei}

1. Den Monat wählen.
2. Auf das Programm der Lohnverrechnung klicken: **BMD Lohn**, **DATEV Lodas** oder **DATEV Lohn und Gehalt**.
3. Im Speicherdialog von Windows den Ort wählen und speichern. Die Meldung „… gespeichert.“ nennt den Dateinamen, z. B. „lohn-BmdLohn-2026-10.csv“. Die Datei an die Lohnverrechnung schicken und dort wie bisher einlesen, z. B. in Excel öffnen, um sie anzusehen.

Vorher holt das Programm die Monatswerte und zeigt sie an. Könnte keine Zeile übergeben werden, weil Personalnummern oder Lohnarten fehlen oder der Monat leer ist, entsteht keine Datei, sondern eine Meldung, was fehlt. Fehlt es nur bei einzelnen Personen oder Positionen, kommen sie nicht in die Datei; die Hinweise über der Tabelle der Monatswerte nennen sie. Die Nummer auf der Karte der Person im Reiter **Lohn**, die Lohnart unter **Einstellungen der Lohnverrechnung** eintragen und den Export erneut starten. Schlägt der Export aus anderem Grund fehl, steht ebenfalls eine Meldung da; Verbindung prüfen und noch einmal versuchen.

## Hinweise {#hinweise}

- Inhaber, Manager und Backoffice.
- In die Datei kommen nur Personen mit Personalnummer und nur Positionen mit Lohnart. Fehlt eine Nummer: [Personalnummern eintragen](hilfe:lohn-personalnummern-eintragen).
- An Tagen mit erfassten Zeiten zählt die erfasste Zeit, sonst die geplante laut Dienstplan. Fehlende Zeiten vorher in **Verwaltung** › **Team** › **Zeiten** nachtragen lassen.
- Lohnarten, Nachtzeit und Feiertage stellt man unter **Einstellungen** ein: [Lohn, Einstellungen](hilfe:lohn#einstellungen). Die richtigen Lohnarten nennt die Lohnverrechnung.
- Wie die Werte entstehen: [Lohn, Monatswerte anzeigen](hilfe:lohn#monatswerte); die Dateiformate: [Lohn, Dateien](hilfe:lohn#dateien).
