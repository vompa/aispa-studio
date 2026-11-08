---
art: ablauf
bereich: Kalender
oberflaeche: windows-app
rollen: rezeption, therapeut, leitung, inhaber
seite: kalender
stichwoerter: Kalender-Abo, Abo, Feiertag, Feiertage, Schulferien, Ferien, Hinweis, ausblenden, einblenden, zeigen, Chip Kalender, Falsch melden, Datumstitel, Tooltip, Stand, Quelle, calendar subscription, hide holidays
---
# Kalender-Abos im Kalender ansehen

Der Kalender zeigt Feiertage, Schulferien und Hinweise aus den [Kalender-Abos](hilfe:kalender-abos) des Spas an dem Tag, an dem sie gelten. Jede Person stellt für sich ein, welche Abos sie im Kalender sehen will. Das ändert nur die Anzeige für diese Person, nicht die Öffnungszeiten, die Buchung und die Antworten des Assistenten.

## Feiertag im Datum {#datum}

Ein gesetzlicher Feiertag steht im Titel des Tages, in der Team- und der Raumansicht gleich, zum Beispiel **Montag, 26. Oktober 2026 · Nationalfeiertag**. Mehrere Einträge stehen mit „ · “ getrennt; ist der Platz knapp, wird der Titel gekürzt.

![Kalenderkopf: Feiertag hinter dem Datum, daneben der Chip Kalender](images/app/kalender-abos-titel.png)

Ein **Tooltip** am Datum nennt alles vollständig mit Quelle und Stand: wo der Eintrag herkommt („Feiertagsregeln Österreich, Wien“ oder „Abo ‚Feiertage Salzburg‘“) und wann das Abo zuletzt abgerufen wurde („Stand gestern“).

Schulferien, Hinweise und Sonstiges stehen **nicht im Titel**, sondern im Tooltip am Datum und im Tagesüberblick: Ein Klick auf den Zähler in der Kopfzeile („17 Termine · 2 da“) zeigt oben die Zeile **Heute:**, zum Beispiel „Heute: Nationalfeiertag · Herbstferien bis 2.11.“. Die Spalten und Köpfe der Räume bleiben unverändert.

Ein Feiertag sperrt den Tag nicht: wie bisher kann gebucht werden, der Titel weist darauf hin.

## Abos ein- oder ausblenden {#zeigen}

1. In der zweiten Zeile der Kopfzeile auf den Chip **Kalender** klicken.
2. In der Liste bei einem Abo den Schalter **Zeigen** umlegen. Es wirkt sofort, ein Neuladen ist nicht nötig.

![Chip Kalender mit der Liste der Abos, Schaltern Zeigen und Aktiv für alle](images/app/kalender-abos-liste.png)

Die Liste nennt zu jedem Abo den Namen, die Art (Feiertage, Schulferien, Hinweise), den Stand des letzten Abrufs und bei einem fehlgeschlagenen Abruf den Grund. Sind Abos ausgeblendet, trägt der Chip **Kalender** einen kleinen Punkt. Der Feiertag aus den Feiertagsregeln des Programms bleibt auch bei ausgeblendetem Abo im Titel stehen.

Bei Abos für gesetzliche Feiertage steht der Hinweis „Nur zur Information, ändert keine Buchung; aktiv/inaktiv nur Leitung“: Ausblenden gilt nur für Sie, ob das Abo überhaupt gilt, bestimmt die Leitung.

Die Leitung (Inhaber, Manager, Standortleitung) sieht bei jedem Abo zusätzlich den Schalter **Aktiv (für alle)**. Ausgeschaltet gilt das Abo für alle nirgends mehr. Angelegt und ausführlich bearbeitet werden Abos in der Windows-App unter **Verwaltung** › **Betrieb** › **Kalender-Abos** ([Kalender-Abos](hilfe:kalender-abos)). Dort steht auch der Haken **Im Kalender zeigen**, der dasselbe umschaltet wie der Schalter **Zeigen**.

## Falsch melden {#falsch-melden}

Stimmt ein Tag eines Abos nicht (zum Beispiel ist am angezeigten Feiertag geschlossen oder geöffnet), meldet man das der Leitung:

1. Im Chip **Kalender** bei dem Abo auf **Falsch melden** klicken.
2. Den **Grund** eintragen (3 bis 300 Zeichen). Gemeldet wird der Tag, der im Kalender angezeigt ist; er steht im Text der Rückfrage.
3. Auf **Melden** klicken.

![Rückfrage Falsch melden mit dem Feld Grund](images/app/kalender-abos-falsch-melden-dialog.png)

Das Abo ändert sich dadurch nicht. Die Leitung bekommt eine Meldung in der Seitenleiste [Meldungen](hilfe:kalender-meldungen) mit Tag und Grund und entscheidet in der Verwaltung, ob sie das Abo ausschaltet, den Tag [ignoriert](hilfe:kalender-abos#widersprueche) oder die Quelle tauscht.

## Meldungen zu Abos {#meldungen}

Die Seitenleiste [Meldungen](hilfe:kalender-meldungen) zeigt zu den Abos:

- **Kalender-Abo geändert**: nach einem Abruf, zum Beispiel „Abo ‚Feiertage Salzburg‘: 2 neue Tage, 1 Tag entfernt“. **Erledigt** blendet sie für alle aus.
- **Kalender-Abo nicht abgerufen**: der Abruf ist fehlgeschlagen; die zuletzt gespeicherte Kopie gilt weiter. **Erneut versuchen** stößt den Abruf an (nur Leitung).
- **Feiertage für das übernächste Jahr prüfen** (jedes Jahr im November, nur Leitung).
- **Falsch gemeldet**: eine Meldung einer Person mit Tag und Grund (nur Leitung).
