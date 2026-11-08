---
art: einstellung
bereich: Einzelplatz
oberflaeche: windows-app
rollen: inhaber, manager, leitung
seite: sicherung
---
# Sicherung

Die Seite „Sicherung“ gibt es nur im Einzelplatz-Betrieb der Windows-App, also wenn die Datenbank auf diesem PC liegt. Solange keine zentrale Datenbank für den Abgleich eingetragen ist, ist dieser PC die einzige Kopie aller Daten, auch der Kassa-Daten. Kassa-Daten müssen sieben Jahre (Österreich) bzw. zehn Jahre (Deutschland) aufbewahrt werden. Deshalb sichert die App jeden Tag in einen Ordner, den die Leitung festlegt.

Den Stand sehen alle, die angemeldet sind. Den Ordner wählen und einen neuen Wiederherstellungsschlüssel erzeugen darf nur, wer den Standort verwalten darf (Inhaber, Standortleitung).

## Wie die Sicherung arbeitet {#ablauf}

- Solange die App offen ist, prüft sie laufend, ob die letzte Sicherung 24 Stunden oder älter ist, und sichert dann selbst. Schlägt eine Sicherung fehl, versucht sie es frühestens nach 30 Minuten wieder.
- Unter Windows legt die App zusätzlich eine Windows-Aufgabe „AISPA Studio Sicherung“ an. Sie startet jeden Abend um 23:00 Uhr, auch wenn die App geschlossen ist, und sichert, wenn die letzte Sicherung mindestens 12 Stunden zurückliegt. War der PC um 23:00 Uhr aus, holt Windows die Sicherung beim nächsten Start nach. Die Aufgabe läuft im angemeldeten Windows-Konto, weil nur dieses die geschützten Zugangsdaten der Datenbank lesen kann.
- Im Ordner bleiben die letzten 14 Sicherungen; ältere löscht die App. Andere Dateien im Ordner fasst sie nicht an.
- Gesichert wird erst, wenn ein Ordner gewählt und ein Wiederherstellungsschlüssel erzeugt ist. Beides passiert zusammen mit „Speichern und sichern“.

## Stand {#stand}

Zeigt, wohin gesichert wird („Sicherung täglich nach …“ oder „Noch kein Ordner für die Sicherung gewählt.“), wann die letzte Sicherung gelang, und ob Windows auch bei geschlossener App sichert. Ließ sich die Windows-Aufgabe nicht anlegen, steht hier der Grund; die App sichert dann weiter, solange sie läuft.

Ist die letzte Sicherung fehlgeschlagen, steht die Fehlermeldung darunter, z. B. wenn der Ordner nicht erreichbar ist.

### Jetzt sichern {#jetzt-sichern}

Sichert sofort, unabhängig vom Zeitplan. Der Knopf geht, sobald ein Ordner eingetragen ist, und steht allen zur Verfügung, die die Seite sehen. Daneben steht danach der Name der neuen Datei, z. B. „Gesichert: aispa-20261019-080000.dump“.

### Erinnerung {#erinnerung}

Über allen Seiten der App erscheint ein gelber Hinweis, solange

- noch kein Ordner gewählt ist,
- noch keine Sicherung gelungen ist, oder
- die letzte gelungene Sicherung älter als sieben Tage ist.

Der Hinweis verschwindet von selbst nach der nächsten gelungenen Sicherung.

## Ordner {#ordner}

Nur für Inhaber und Standortleitung sichtbar.

### Ordner für die Sicherung {#ordner-feld}

Der vollständige Pfad des Ordners, z. B. `D:\Sicherung` oder `\\server\freigabe\aispa`. Ein Pfad ohne Laufwerk oder Server (z. B. nur `Sicherung`) wird abgelehnt. Standard: leer, also keine Sicherung.

- Am besten ein zweites Laufwerk, ein USB-Laufwerk, das angesteckt bleibt, oder ein Netzordner.
- Nicht dieselbe Festplatte wie Windows: fällt sie aus, ist sonst auch die Sicherung weg.
- Der Ordner muss für das Windows-Konto, mit dem die App läuft, beschreibbar sein.

„Speichern und sichern“ merkt sich den Ordner auf diesem PC, erzeugt beim ersten Mal den Wiederherstellungsschlüssel, richtet die Windows-Aufgabe ein (oder erneuert sie) und sichert sofort. Ein späterer Wechsel des Ordners erzeugt keinen neuen Schlüssel; die bisherigen Sicherungen bleiben im alten Ordner liegen.

## Wiederherstellungsschlüssel {#wiederherstellungsschluessel}

Ein Code aus 24 Zeichen in sechs Vierergruppen, z. B. `K7QM-2XRT-H9PW-4CNE-YB3D-ZF6A`. Er entsteht beim ersten „Speichern und sichern“ und wird in der Warnung „Wiederherstellungsschlüssel notieren“ genau einmal angezeigt. Die App speichert ihn nicht; nach „Verstanden“ ist er nirgends mehr zu sehen.

Wozu er gebraucht wird:

- **Wiederherstellen**: Die Gesundheitsdaten (Anamnesen) in einer Sicherung sind verschlüsselt. Auf einem anderen PC sind sie nur mit diesem Schlüssel lesbar; ohne ihn lässt sich eine Sicherung nicht einspielen.
- **Passwort vergessen**: Am Haupt-PC setzt der Schlüssel das Passwort jeder Person neu, auch das der Inhaberin.

Ausdrucken oder aufschreiben und bei der Leitung aufbewahren, getrennt vom PC und nicht am Empfang. Wer den Schlüssel hat, kann jedes Konto übernehmen.

### Neuen Wiederherstellungsschlüssel erzeugen {#neuer-schluessel}

Nur für Inhaber und Standortleitung. Für den Fall, dass der alte Schlüssel verloren ist oder jemand ihn gesehen hat, der ihn nicht kennen soll. Der neue Schlüssel wird einmal angezeigt.

- Ab sofort gilt nur noch der neue Schlüssel für „Passwort vergessen“.
- Neue Sicherungen brauchen ab der nächsten Sicherung den neuen Schlüssel.
- Ältere Sicherungen brauchen weiter den alten Schlüssel. Wer den alten nicht mehr hat, sollte gleich „Jetzt sichern“ wählen, damit es eine Sicherung mit dem neuen gibt.

## Was im Ordner liegt {#dateien}

Jede Sicherung besteht aus zwei Dateien mit Datum und Uhrzeit im Namen:

- `aispa-JJJJMMTT-HHMMSS.dump`: die ganze Datenbank des Spas.
- `aispa-JJJJMMTT-HHMMSS.schluessel.json`: der Schlüssel für die Gesundheitsdaten, mit dem Wiederherstellungsschlüssel verschlüsselt. Ohne den Code ist diese Datei nutzlos.

Zum Wiederherstellen braucht es beide Dateien und den Wiederherstellungsschlüssel; siehe „Einzelplatz“, Abschnitt „Aus einer Sicherung wiederherstellen“. Ein Protokoll der Sicherungen bei geschlossener App steht in `%LOCALAPPDATA%\AispaStudio\daten\protokolle\sicherung.log`.
