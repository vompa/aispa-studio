---
art: ablauf
bereich: Kalender
oberflaeche: windows-app
rollen: rezeption, therapeut, leitung, inhaber, manager
seite: kalender
stichwoerter: Meldungen, Warnung, Hinweis, Fehler, Glocke, Benachrichtigung, wegklicken, Was tun, Pause fehlt, Lager, Raumpflege überfällig, Tagesstart offen, Mindestbestand, Symbol im Spaltenkopf, notifications, alerts
---
# Meldungen: Warnungen und Hinweise des Tages

Alles, was am Tag Aufmerksamkeit braucht, steht an einer Stelle: in der Seitenleiste **Meldungen**. Der Kalender selbst bleibt frei von gelben Bändern. Meldungen entstehen und verschwinden von selbst, wenn sich etwas ändert, auch durch einen anderen Arbeitsplatz; neu laden muss niemand.

![Seitenleiste Meldungen neben der Raumansicht](images/app/kalender-betrieb.png)

## Schaltfläche, Taste und Symbol {#schaltflaeche}

- Die Schaltfläche **Meldungen** steht im Kalenderkopf neben **Termin**. Mit offenen Meldungen zeigt sie die Zahl („Meldungen 2“): orange, sobald eine Warnung dabei ist, rot bei einem Fehler, sonst grau. Ohne Meldungen steht nur „Meldungen“. Gibt es Tagesnotizen, trägt die Schaltfläche daneben einen kleinen Notiz-Punkt; die Zahl zählt nur Meldungen.
- Die Taste **M** öffnet die Seitenleiste, ein zweites Mal schließt sie.
- Im Kopf einer Person oder eines Raums steht höchstens ein kleines Symbol (Dreieck bei Warnungen, Kreis bei Fehlern). Der Hinweis daran nennt kurz, worum es geht, z. B. „2 Raumpflege offen · Tagesstart offen“. Ein Klick öffnet **Meldungen** nur mit den Meldungen dieser Spalte; **Filter aufheben** zeigt wieder alle. „3 Termine umbuchen“ bei einer Krankmeldung bleibt unverändert im Kopf.

## Notizen heute {#notizen}

Ganz oben steht die Gruppe **Notizen heute** mit den Tagesnotizen ohne Uhrzeit: Kästchen zum Abhaken, der Text, ein Stift zum Ändern und ein Papierkorb zum Löschen (mit Rückfrage); **+ Notiz** eine neue an. Ohne Notizen zeigt die Gruppe nur **+ Notiz**. Notizen sind keine Meldungen: sie zählen nicht in der Zahl der Schaltfläche, die Schaltfläche trägt aber einen kleinen Notiz-Punkt, sobald es welche gibt. Die Taste **.** öffnet die Seitenleiste mit dem Notizfeld. Ein Filter auf eine Person oder einen Raum blendet die Gruppe aus. Mehr unter [Pausen, Sperrzeiten und Notizen](hilfe:kalender-pausen-und-notizen#tagesnotiz).

![Meldungen mit der Gruppe Notizen heute](images/app/meldungen-notizen.png)

## Gruppen und Karten {#gruppen}

Die Seitenleiste ordnet nach Schwere: **Fehler**, **Warnungen**, **Hinweise** und darunter blass **Erledigt** (was in dieser Sitzung behoben wurde). Jede Gruppe zeigt ihre Zahl. Eine Karte nennt:

- das Symbol der Art, den Titel und die Art („Pause“, „Lager“, „Raumpflege“, „Tagesstart“, „Mindestbestand“, „Handbuch“, „Ausfall“, „Schließzeit“, „Raumsperre“),
- einen Kurztext und seit wann die Meldung besteht,
- **eine** passende Aktion und **Wegklicken**.

Welche Aktion zu welcher Meldung gehört:

- **Pause** (keine Zeit mehr für die gesetzliche Pause): **Pause eintragen** öffnet das Fenster für die Pause.
- **Lager** (Bestand reicht nicht oder unterschreitet die Mindestmenge): **Anzeigen** öffnet das Lager in der Verwaltung.
- **Raumpflege** überfällig oder offen: **Abhaken**; sind im Raumcheck Zeilen offen, öffnet es die Seitenleiste Raumpflege.
- **Tagesstart offen:** **Tagesstart öffnen** zeigt die Checkliste des Raums.
- **Räume unter Mindestbestand:** **Raumansicht** wechselt zu den Räumen.
- **Abwesenheit** mit offenen Terminen: **Ersatz suchen** öffnet den Ausfall-Assistenten.
- **Schließzeit** („3 Termine in der Schließzeit“): **Termine absagen …** öffnet die Rückfrage mit den Terminen, dem Text der Nachricht und der Zahl der Nachrichten; erst nach dem Klick auf den roten Knopf wird abgesagt und gesendet ([Termine einer Schließzeit absagen](hilfe:schliesszeiten-termine-absagen)). Die Meldung bleibt, solange offene Termine in der Schließzeit liegen. Gesendet wird nichts von selbst.
- **Raumsperre** („Raum Lotus ist gesperrt (…): 4 Termine umbuchen“): **Umbuchen …** öffnet die Umbuchungshilfe mit einem Vorschlag je Termin (anderer Raum zur selben Zeit, sonst die nächste freie Zeit); erst nach **Gewählte übernehmen** wird umgebucht ([Termine im gesperrten Raum umbuchen](hilfe:raumsperren-termine-umbuchen)). Die Meldung bleibt, solange offene Termine im gesperrten Raum liegen.

Abhaken und Tagesstart brauchen das Recht, den Status von Terminen zu ändern, Pause eintragen das Recht zu buchen, Termine absagen die Rechte „Standorte verwalten“ und „Buchen“ zusammen, Umbuchen das Recht zu buchen; die Meldungen selbst sehen alle mit Zugang zum Kalender.

![Meldung „4 Termine in der Schließzeit“ mit dem Knopf Termine absagen](images/app/meldung-schliesszeit.png)

## Meldungen zu Kalender-Abos {#abos}

Zu den [Kalender-Abos](hilfe:kalender-abos-anzeigen#meldungen) erscheinen vier Arten von Karten mit der Pille **Kalender-Abo**: eine Änderung nach einem Abruf (Hinweis, **Erledigt**), ein Fehler beim Abruf (Warnung, **Erneut versuchen** nur für die Leitung; die zuletzt gespeicherte Kopie gilt weiter), die Erinnerung im November („Feiertage für das übernächste Jahr prüfen“, nur Leitung) und eine „Falsch gemeldet“-Meldung einer Person (nur Leitung). Sie zählen wie die anderen Meldungen.

## Vormerkung ohne Antwort {#vormerkung}

Hat die Rezeption im Posteingang eine Zeit für einen Gast vorgemerkt und der Gast sich nicht gemeldet, erscheint die Warnung **Gast hat sich nicht gemeldet** mit Gast, Zeit und Behandlung; **Zur Anfrage** öffnet die Anfrage im Posteingang mit **Bestätigen** und **Freigeben**. Wann sie erscheint, richtet sich nach dem Termin: am selben Tag eine Stunde nach dem Vormerken (spätestens eine Stunde vor Beginn), für morgen bis 12 Uhr vorgemerkt drei Stunden später, sonst zum Tagesstart des Termintags, für spätere Tage zum Tagesstart am Vortag. Antwortet der Gast, verschwindet die Warnung. Siehe [Anfrage annehmen](hilfe:anfragen-annehmen#keine-antwort).

## Wegklicken {#wegklicken}

**Wegklicken** blendet eine Meldung aus, bis sich ihr Auslöser ändert, z. B. wenn aus „Raumpflege offen“ „überfällig“ wird. Das gilt je Tag und Standort, solange die App läuft. Eine Warnung ist nie abhakbar: ist der Zustand behoben, verschwindet sie von selbst und steht kurz unter **Erledigt**.

## Detail und „Was tun?“ {#detail}

Ein Klick auf eine Karte öffnet die **Detailansicht** als eigenen Reiter **Meldung** unten in der Seitenleiste:

- der volle Wortlaut mit weiteren Zeilen (z. B. welches Material fehlt),
- **Betrifft** mit Links: eine Person oder ein Raum bringt die Spalte im Kalender ins Bild, ein Material öffnet das Lager,
- seit wann, und die Aktion als Hauptknopf,
- bei **Tagesstart offen** die Checkliste des Raums zum Abhaken samt **Abnehmen**,
- die Karte **Was tun?**: der Assistent beantwortet, was in dieser Lage zu tun ist, mit Vorschlägen als Knöpfe (ausgeführt wird erst nach dem Klick) und Verweisen ins Handbuch. Antwortet der Assistent nicht oder ist er abgeschaltet, steht der Abschnitt des Handbuchs zur Art der Meldung da.

## Ohne Verbindung {#offline}

Die Seitenleiste zeigt den letzten Stand und einen Hinweis „Ohne Verbindung“. Die Aktionen sind abgeschaltet, **Wegklicken** geht weiter.

## Gut zu wissen {#gut-zu-wissen}

- Hinweise zu einem einzelnen Termin (z. B. Anamnese fehlt) stehen im Termin, nicht hier.
- Tagesnotizen stehen in der Gruppe **Notizen heute** ganz oben in der Seitenleiste; wer heute nicht da ist, zeigen die Spalten, und hat die Person offene Termine, steht hier die Meldung **Abwesenheit**. Siehe [Pausen, Sperrzeiten und Notizen](hilfe:kalender-pausen-und-notizen#tagesleiste).
- Mehr zu Tagesstart und Raumpflege: [Betrieb im Kalender](hilfe:kalender-betrieb); zum Material im Raum: [Raum nach der Behandlung prüfen](hilfe:kalender-raumcheck).
