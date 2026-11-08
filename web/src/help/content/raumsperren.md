---
art: einstellung
bereich: Raumsperren
oberflaeche: verwaltung
rollen: leitung, inhaber, manager
seite: verwaltung
stichwoerter: Raumsperre, Raum sperren, Raum gesperrt, Wartung, Schaden, Wasserschaden, Umbau, Renovierung, Raum nicht buchbar, Raum außer Betrieb, Raum Urlaub, Zimmer gesperrt, Raum zu, halber Tag, room closure, block room, maintenance, renovation, room out of service
---
# Raumsperren

Eine Raumsperre sagt, dass ein einzelner Raum in einem Zeitraum nicht benutzbar ist: Wartung, ein Schaden (zum Beispiel ein Wasserschaden), ein Umbau oder etwas anderes. Sie wirkt wie der Urlaub einer Person: der Raum bleibt im Kalender, ist aber in der Zeit nicht buchbar. Für den ganzen Standort gibt es die [Schließzeit](hilfe:schliesszeiten); für ein paar Stunden genügt die [Sperrzeit](hilfe:kalender-pausen-und-notizen#sperrzeit) im Kalender.

Verwalten dürfen Inhaber, Manager und Standortleitung (Recht „Standorte verwalten“, wie beim Anlegen von Räumen), die Standortleitung nur für ihre Standorte. Lesen können alle mit Zugang zum Kalender, weil Kalender und Meldungen die Raumsperren zeigen.

## Was eine Raumsperre bewirkt {#wirkung}

- **Der Raum wird nicht gebucht:** Online-Buchung, „Freie Zeit suchen“, Warteliste, Buchen, Verschieben und Serien im Kalender, die KI-Terminannahme und Paar- oder Spa-Tag-Buchungen wählen den Raum in der Zeit nie; es gibt andere freie Zeiten in anderen Räumen. Wer den gesperrten Raum ausdrücklich wählt, bekommt die Antwort „ist gesperrt (Wartung)“. Bei einem halben Tag bleibt der Rest des Tages buchbar, ein Termin muss aber vor der Sperre enden. Bei Sauna und Ruheraum gilt es genauso: ein Eintritt in der Sperre wird abgelehnt.
- **Kalender, Raumansicht:** die gesperrte Zeit ist in der Spalte des Raums schraffiert wie bei einer Person ohne Dienst, mit einer Kapsel für Art und Grund. Im Kopf steht „gesperrt: Wartung bis 3.12.“, im Hinweis der Grund. Termine im gesperrten Raum tragen den roten Rand „umbuchen“.
- **Kalender, Teamansicht:** Termine, deren Raum gesperrt ist, tragen denselben Rand, und die Hover-Karte sagt „Raum Lotus ist gesperrt: bitte umbuchen“.
- **Bestehende Termine bleiben gebucht.** Nichts wird von selbst umgebucht oder abgesagt, niemand benachrichtigt. Die Seitenleiste **Meldungen** zeigt „Raum Lotus ist gesperrt (…): 4 Termine umbuchen“ mit dem Knopf **Umbuchen …**, gleich wie fern die Sperre liegt: als Warnung, solange sie später als in 14 Tagen beginnt, als Fehler, wenn sie läuft oder in 14 Tagen beginnt. Wie es weitergeht, steht unter [Termine im gesperrten Raum umbuchen](hilfe:raumsperren-termine-umbuchen).

![Raumansicht mit gesperrtem Raum Lotus: schraffiert, mit Kapsel und dem Weg zum Umbuchen im Kopf](images/app/kalender-raumsperre.png)

## Raumsperren verwalten {#raumsperren}

1. In der Windows-App **Verwaltung** › **Betrieb** › **Raumsperren** öffnen. Gibt es mehrere Standorte, oben den **Standort** wählen. Aus dem Kalender kommt man mit einem Rechtsklick auf den Kopf eines Raums und **Raum sperren …** gleich dorthin: ein neues Formular mit dem Raum schon gewählt.
2. Oben den Reiter **Aktuell** (aktiv und geplant) oder **Beendet** wählen. Die Zahl nennt, wie viele Sperren dort stehen.
3. Links eine Sperre wählen oder auf **Neue Raumsperre** klicken, rechts die Maske ausfüllen und auf **Speichern** klicken (in der Windows-App auch **Strg+S**). Es erscheint „Gespeichert“.

In der Windows-App gilt zusätzlich:

- **Verwerfen** neben **Speichern** holt den gespeicherten Stand zurück. Wer mit ungespeicherten Änderungen eine andere Raumsperre, einen anderen Reiter, Standort oder Bereich wählt, wird gefragt: „Änderungen speichern?“ mit **Speichern**, **Verwerfen** und **Abbrechen**.
- Ein Fehler steht am Feld, und der Cursor springt ins erste Feld mit einem Fehler.
- Datum und Uhrzeit lassen sich kurz eintippen: „24.12.“, „9“ wird 09:00, „1430“ wird 14:30.
- **Löschen** fragt vorher nach: „Raumsperre löschen?“.

Der Zustand wird gerechnet: **Geplant** beginnt später, **Aktiv** läuft gerade, **Beendet** ist vorbei. Zwei Sperren desselben Raums dürfen sich nicht überschneiden; für verschiedene Räume gibt es keine Grenze. **Löschen** hebt die Sperre auf, der Raum ist sofort wieder buchbar.

## Raum {#raum}

Der Raum, der gesperrt wird; zur Wahl stehen die Räume des gewählten Standorts (auch Sauna und Ruheraum). Der Raum einer bestehenden Sperre lässt sich nicht ändern; ist der falsche Raum gesperrt, die Sperre löschen und eine neue anlegen.

## Zeitraum {#zeitraum}

**Von** und **Bis** sind Kalendertage am Standort, beide Tage zählen mit; das Ende darf nicht vor dem Beginn liegen, höchstens 366 Tage. Mit **Nur ein Teil der Tage (mit Uhrzeit)** kommen **Ab Uhrzeit** (am ersten Tag) und **Bis Uhrzeit** (am letzten Tag) dazu. So entsteht ein halber Tag, zum Beispiel „ab 13:00 Uhr“, wenn mittags ein Schaden auffällt. Ohne Uhrzeit gilt der erste Tag ab 0:00 Uhr und der letzte bis zum Ende des Tages.

## Art und Grund {#art}

- **Wartung**: geplant, zum Beispiel für die Lüftung oder das Dampfbad.
- **Schaden (zum Beispiel Wasserschaden)**: ungeplant. Im Kalender in Rot.
- **Umbau oder Renovierung**: in Orange.
- **Sonstiges**: alles übrige.

Die Art bestimmt die Farbe der Kapsel im Kalender und das Wort in Meldung und Kopf. **Grund für das Team** (höchstens 200 Zeichen, zum Beispiel „Wasserschaden, Trocknung bis Freitag“) steht in der Kapsel und im Hinweis am Kopf des Raums; Gäste sehen ihn nie. Unter den Feldern steht, wer die Sperre wann angelegt hat.
