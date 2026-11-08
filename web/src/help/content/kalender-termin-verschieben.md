---
art: ablauf
aufbau: schritte
bereich: Kalender
oberflaeche: windows-app
rollen: rezeption, leitung, inhaber, manager
seite: kalender
stichwoerter: verschieben, verlegen, umbuchen, umlegen, später, früher, andere Uhrzeit, andere Person, anderer Raum, anderer Tag, ziehen, Drag and Drop, Pfeiltasten, rückgängig, Strg+Z, passt nicht, Vorbereitung ändern, Nachbereitung ändern, Zeiten je Termin, Puffer, Verspätung, kommt später, zu spät, verspätet, late, move, reschedule, drag, undo
---
# Termin verschieben

Termine lassen sich im Kalender ziehen, mit Alt + Pfeiltasten oder über das Terminfenster auf einen anderen Tag verschieben. Der Termin steht sofort an der neuen Stelle; der Server prüft im Hintergrund, ob Person, Raum und Gerät frei sind.

## Voraussetzungen {#voraussetzungen}

- Rezeption, Standortleitung oder Inhaber.
- Der Termin ist gebucht oder eingecheckt; erledigte, nicht erschienene und stornierte Termine lassen sich nicht verschieben.
- Für einen anderen Tag, per Tastatur oder für eine ganze Buchung siehe die Abschnitte darunter.

## Schritte {#schritte}

### 1. Termin fassen

Den Termin mit der Maus fassen. Der Mauszeiger wird über einem Termin, den man verschieben darf, zur Hand.

![Kalender mit markiertem Termin von Brigitte Müller](images/app/termin-verschieben-1.png)

### 2. An die neue Zeit ziehen

An die neue Uhrzeit bzw. in die Spalte einer anderen Person ziehen. Eine Vorschau zeigt Uhrzeit und Spalte, darüber und darunter in Grau die gebundene Zeit mit Vorbereitung und Nachbereitung. Der Beginn rastet beim Ziehen auf 5 Minuten ein (Alt + Pfeiltasten und die Buchungsvorschau bleiben bei der Viertelstunde). Doch nicht verschieben? Vor dem Loslassen **Esc** drücken oder mit der Maus aus dem Kalender hinausfahren (über die Zeitleiste, die Kopfzeile oder an den Rand des Fensters): das Ziehen bricht ab, der Termin bleibt an seinem Platz.

![Kalender mit markierter Vorschau des Termins an der neuen Stelle](images/app/termin-verschieben-2.png)

### 3. Loslassen

Die Maustaste loslassen. Unten erscheint z. B. „Verschoben auf 10:00–11:20 (Lena Huber, Raum Zeder)“ mit **Rückgängig**. Die Meldung verschwindet nach 8 Sekunden, solange die Maus nicht darüber steht.

![Meldung unten im Kalender mit dem Knopf Rückgängig](images/app/termin-verschieben-3.png)

## Was danach {#was-danach}

Die alte Zeit gilt als frei; der Server schreibt passenden Gästen auf der [Warteliste](hilfe:kalender-warteliste). Innerhalb der Meldungsdauer lässt sich die Verschiebung [rückgängig machen](hilfe:kalender-termin-verschieben#rueckgaengig). Ändert sich nur die Vor- oder Nachbereitung, siehe [Zeiten je Termin](hilfe:kalender-termin-verschieben#zeiten-je-termin).

## Wenn etwas schiefgeht {#wenn-etwas-schiefgeht}

- Lehnt der Server ab (z. B. Person, Raum oder Gerät belegt), springt der Termin zurück und unten steht „Nicht verschoben: …“ mit der Begründung; belegte Zeiträume stehen darin in Ortszeit des Standorts („Zeitraum ist belegt. Termin · 11:00–11:50 · Clara Fischer“).
- Erledigte, nicht erschienene und stornierte Termine lassen sich nicht verschieben, eingecheckte schon.
- Auf einen vergangenen Tag lässt sich kein Termin verschieben, auch keine ganze Buchung: Das Feld „Tag“ nimmt nur heute oder einen späteren Tag an, **−1 Tag** und **Verschieben** sind dann gesperrt („Verfügbar für heute oder einen späteren Tag.“). Ziehen und Alt + Pfeiltasten bleiben ohnehin im angezeigten Tag.
- Hat jemand den Termin inzwischen an einem anderen Arbeitsplatz geändert, verschiebt der Server nicht. Oben steht „Nicht verschoben“ mit „Der Termin wurde inzwischen an einem anderen Arbeitsplatz geändert. Bitte den aktuellen Stand prüfen.“, und der Kalender zeigt den Termin so, wie ihn der andere Arbeitsplatz gespeichert hat. Dasselbe gilt im Terminfenster für Zeiten, Notiz, Stornieren, Status und Zurücknehmen; die Meldung steht dann oben im Terminfenster. Die eigene Änderung bei Bedarf neu eingeben.
- Fehlermeldungen bleiben stehen, bis man sie mit dem Kreuz schließt.
- **V** auf einem markierten, gebuchten Termin öffnet die Verschieben-Karte im Terminfenster ([anderer Tag](hilfe:kalender-termin-verschieben#anderer-tag)); bei eingecheckten Terminen und gemeinsamen Buchungen erinnert die Taste nur, wie das Verschieben geht.
- Ohne Verbindung wird die Verschiebung vorgemerkt; siehe [Arbeiten ohne Verbindung](hilfe:kalender-offline).



## Mit der Tastatur {#tastatur}

1. Den Termin anklicken (markieren).
2. **Alt+↑** bzw. **Alt+↓** verschiebt ihn um eine Viertelstunde früher bzw. später.
3. **Alt+←** bzw. **Alt+→** verschiebt ihn in die Nachbarspalte (andere Person bzw. anderer Raum).

## Andere Person oder anderer Raum {#andere-person}

- In der Ansicht **Team** wechselt das Ziehen in eine andere Spalte die Person. Der Raum bleibt, wenn er zur neuen Zeit frei ist; sonst wählt der Server einen passenden anderen.
- In der Ansicht **Räume** wechselt das Ziehen den Raum, die Person bleibt. Siehe [Raumansicht](hilfe:kalender-raumansicht).

## Rückgängig {#rueckgaengig}

Innerhalb der 8 Sekunden, die die Meldung unten steht (ohne **Rückgängig** sind es 5), **Rückgängig** anklicken oder **Strg+Z** drücken. Der Termin geht an die alte Stelle zurück, wieder über den Server; ist die alte Stelle inzwischen belegt, steht oben im Kalender „Nicht zurückgenommen“ mit dem Grund und der Schaltfläche **Erneut versuchen**. Hat jemand den Termin inzwischen an einem anderen Arbeitsplatz geändert, steht dort „Nicht zurückgenommen“ ohne **Erneut versuchen**, und der Kalender zeigt den Termin so, wie ihn der andere Arbeitsplatz gespeichert hat. Hat jemand den Termin inzwischen an einem anderen Arbeitsplatz geändert, steht dort „Nicht zurückgenommen“ ohne **Erneut versuchen**, und der Kalender zeigt den Termin so, wie ihn der andere Arbeitsplatz gespeichert hat.

![Meldung nach dem Verschieben mit Rückgängig](images/app/kalender-verschoben.png)

## An einen anderen Tag {#anderer-tag}

Ziehen und Alt + Pfeile bleiben im angezeigten Tag. Für einen anderen Tag:

1. Den Termin anklicken; rechts öffnet sich das Terminfenster.
2. Auf **Verschieben …** klicken (der Knopf über die volle Breite unter dem Hauptknopf), oder bei markiertem Termin **V** drücken.
3. Bei „Tag“ das Datum eintippen (`21.10.2026`, kurz auch `21102026` oder `21.10.`) oder mit **+1 Tag**, **+1 Woche** bzw. **−1 Tag** weiterzählen, im Feld auch mit **↑ ↓**. Bei „Beginn“ die Uhrzeit, z. B. `14:00` oder `1400`. Ungültiges meldet das Feld, siehe [Felder ausfüllen](hilfe:kalender-tastenkuerzel#felder).
4. Auf **Verschieben** klicken. Der Kalender springt auf den neuen Tag und markiert den Termin; unten steht die Meldung mit **Rückgängig**.

![Termin auf einen anderen Tag verschieben](images/app/kalender-anderer-tag.png)

- Person und Raum bleiben, wenn sie zur neuen Zeit frei sind; sonst wählt der Server passende. Die Meldung nennt, wer und welcher Raum es geworden ist.
- Das geht für gebuchte Einzeltermine. Eine gemeinsame Buchung zieht nur als Ganzes um (siehe unten), ein eingecheckter Termin nur innerhalb des Tages.

## Paar oder Spa-Tag {#gemeinsam}

Einzelne Teile einer gemeinsamen Buchung lassen sich wie jeder Termin ziehen. Die ganze Buchung auf einmal verschieben:

1. Einen Termin der Buchung anklicken.
2. Im Kasten **GEMEINSAM GEBUCHT** den neuen Beginn eintippen (z. B. `15:00`).
3. Auf **Ganze Buchung verschieben** klicken. Der ganze Ablauf rückt mit, am selben Tag.

- Das geht nur, solange noch niemand der Gruppe eingecheckt ist.

## Gast kommt später {#verspaetung}

Ruft ein Gast an, dass er später kommt:

1. Rechtsklick auf den Termin › z. B. **Kommt 15 Min. später** (es gibt 5, 10 und 15 Minuten; kommt der Gast noch später, den Termin [auf eine andere Zeit verschieben](hilfe:kalender-termin-verschieben#tastatur)).
2. Der Termin bleibt an seiner Zeit stehen, auch wenn der Beginn schon vorbei ist. Die Karte zeigt „kommt 10:45“, und als überfällig gilt er erst ab dieser Zeit. Die Meldung unten bestätigt es, z. B. „Brigitte Müller kommt 15 Min. später (10:45)“.

![Gast kommt später: Meldung unten, „kommt 10:45“ am Termin](images/app/kalender-verspaetung.png)

- Der Termin wird nicht verkürzt und nicht verschoben, der Preis bleibt.
- **Doch pünktlich** im selben Menü nimmt die Verspätung zurück. Wird der Termin verschoben, ist sie erledigt.

## Vor-/Nachbereitung je Termin ändern {#zeiten-je-termin}

Jeder Termin trägt seine eigene Vorbereitung und Nachbereitung. Beim Buchen kommen sie von der Behandlung; ein Gast mit dem Kennzeichen „Braucht mehr Zeit“ bekommt dazu 10 Minuten **Puffer**, der im Streifen, in der Hover-Karte und im Panel getrennt steht („Nachb. 10 · Puffer 10 Min.“). Soll nur ein einzelner Termin mehr oder weniger Zeit davor oder danach binden:

1. Den Termin anklicken; rechts öffnet sich das Terminfenster.
2. Im **Zeitbalken** unter der Uhrzeit auf den Abschnitt **Vorbereitung** oder **Nachbereitung** klicken (der Zeiger wird zur Hand), oder auf **Mehr** und dann auf **Zeiten dieses Termins ändern …** (auch per Rechtsklick auf den Termin bzw. einen Streifen **Zeiten dieses Termins ändern …**).
3. Bei **Vorbereitung (min)** und **Nachbereitung (min)** die Minuten eintippen oder mit den Pfeilen in 5er-Schritten ändern (0 bis 120). Die Felder zeigen zuerst die Werte des Termins.
4. Auf **Übernehmen** klicken. **Abbrechen** oder **Esc** lässt alles, wie es war.

![Vor- und Nachbereitung eines Termins ändern](images/app/kalender-panel-zeiten.png)

- Der Server prüft dieselbe Person, denselben Raum und dasselbe Gerät zur selben Startzeit mit den neuen Minuten. Passt es nicht, etwa weil die längere Vorbereitung in den Termin davor ragt, bleibt der Termin unverändert und unten steht „Zeiten nicht geändert: …“ mit der Begründung.
- Der Puffer bleibt, wie er ist.
- Weichen die Minuten von der Behandlung ab, steht im Terminfenster und in der Hover-Karte „(geändert)“ dahinter.
- Das Verschieben rechnet mit den Minuten des Termins. Eine spätere Änderung der Behandlung im Katalog gilt nur für neue Buchungen.
- Das geht für gebuchte und eingecheckte Termine, für alle, die buchen dürfen (Rezeption, Standortleitung, Inhaber). Bei Paar und Spa-Tag gilt es je Termin, nicht für die ganze Buchung.

## Hinweise {#hinweise}

- Verschieben dürfen Rezeption, Standortleitung und Inhaber.
- Ohne Verbindung wird die Verschiebung vorgemerkt („vorgemerkt“ am Termin) und erst beim Abgleich geprüft; **Rückgängig** gibt es dann nicht. Siehe [Arbeiten ohne Verbindung](hilfe:kalender-offline).
- Wird ein Termin verschoben, gilt die alte Zeit als frei; der Server schreibt passenden Gästen auf der [Warteliste](hilfe:kalender-warteliste).
- Fällt eine Person aus, verschiebt der [Ausfall-Assistent](hilfe:kalender-krankmeldung) alle ihre Termine auf einmal.
