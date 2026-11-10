---
art: ablauf
bereich: Lager
oberflaeche: verwaltung
rollen: inhaber, manager, leitung
seite: verwaltung
stichwoerter: bestellen, Bestellung, Bestellvorschlag, nachbestellen, Lieferant, Teillieferung, Lieferung buchen, Bestelltext, Einkauf, purchase order, reorder, supplier, partial delivery, Bestellung als PDF, Bestellung per Mail, Bestellung drucken, Bestellung schicken, PDF an Lieferanten, order PDF, email order
---
# Bestellen und Lieferung buchen

Der Bestellvorschlag rechnet aus dem Tagesverbrauch der letzten 90 Tage, den gebuchten Terminen und der Lieferzeit des Lieferanten, wie viel bis zur Höchstmenge fehlt. Daraus entsteht je Lieferant ein Entwurf. Ohne Bestätigung der Leitung geht nichts an einen Lieferanten.

## Bestellen und senden in der Windows-App {#senden}

1. In der Verwaltung **Lager** öffnen.
2. Den Reiter **Bestellen** öffnen.
3. Den **Bestellvorschlag** prüfen.
4. Auf **Bestellung anlegen** klicken. Je Lieferant entsteht ein Entwurf.
5. Bei Bedarf die Mengen ändern und auf **Mengen speichern** klicken. Eine Menge 0 nimmt die Position heraus; für die letzte Position **Entwurf verwerfen** nehmen.
6. Auf **Bestellen und senden** klicken und die Rückfrage bestätigen. Die Bestellung geht per Mail an den Lieferanten und steht auf „bestellt“.

**Entwurf verwerfen** storniert den Entwurf; an den Lieferanten geht dann nichts. Der Knopf **Bestellen und senden** ist nur aktiv, wenn beim Lieferanten eine Mailadresse eingetragen ist: [Lieferanten](hilfe:lager-lieferanten). Liegt der Entwurf unter dem Mindestbestellwert des Lieferanten, warnt die Maske mit dem fehlenden Betrag; senden lässt er sich trotzdem.

## So rechnet der Vorschlag {#rechnung}

Beispiel: Massageöl mit 30 ml Tagesverbrauch, 5 Tagen Lieferzeit, 100 ml Bestand, 90 ml in gebuchten Terminen und 1.000 ml Höchstmenge. Bis zur Lieferung werden 150 ml und 90 ml gebraucht, der erwartete Stand ist also -140 ml. Bis zur Höchstmenge fehlen 1.140 ml; bei Flaschen zu 500 ml schlägt das Programm 3 Flaschen vor.

- **Tagesverbrauch**: Verbrauch und Verkauf der letzten 90 Tage, geteilt durch 90.
- **Termine**: Material der gebuchten Termine innerhalb der Lieferzeit.
- **Offene Bestellungen** werden abgezogen.
- **Vorschlag**: nur wenn der erwartete Stand unter den Mindestbestand fällt oder bei Mindestbestand 0 nicht über 0 liegt. Aufgefüllt wird bis zur Höchstmenge, mindestens aber bis zum Mindestbestand, auf ganze Packungen aufgerundet.
- **Ohne Lieferzeit** beim Lieferanten zählen die Termine der nächsten 14 Tage und kein Tagesverbrauch.

Die Höchstmenge steht beim Lagerort: [Mindestmengen](hilfe:lager-lagerorte#mindestmengen). Das Ziel des Standorts ist die Summe seiner aktiven Lagerorte. Der Versand braucht einen eingerichteten Mailzugang, sonst bleibt die Bestellung Entwurf.

## Bestellung als PDF drucken oder senden {#pdf}

Unter den Knöpfen jeder Bestellung steht der Knopfblock **Drucken**, **PDF speichern** und **Per Mail senden**. Er steht bei jeder Bestellung.

- Das PDF `Bestellung-<Nummer>-<Lieferant>.pdf` zeigt Lieferant, Ansprechperson, Bestelldatum, „Lieferung an“ (den Standort), die „Erwartete Lieferung“ (nur mit Lieferzeit beim Lieferanten), die Positionen mit Menge, Einheit, Artikelnummer, Bezeichnung, Einzelpreis und Summe, die Gesamtsumme netto und den Hinweis.
- **Per Mail senden** fragt mit der Mailadresse des Lieferanten (leer, wenn keine eingetragen ist), dem Betreff „Bestellung <Nummer> vom <Datum>“ und einem Text mit Gruß. Nach **Senden** steht „Gesendet an …“, die Mail geht mit dem PDF im Anhang hinaus, und unter den Knöpfen steht der Vermerk „Gesendet am … an … von …“. Ein zweites Senden fügt eine zweite Zeile hinzu.
- Der Stand der Bestellung ändert sich dabei nicht: ein Entwurf bleibt Entwurf. Bestellt ist sie erst mit **Bestellen und senden** oder **Als bestellt markieren**.
- Es gilt dieselbe Regel für Vollmachten wie bei **Bestellen und senden**: Fehlt die gültige Vollmacht „Bestellungen auslösen“, nennt die App den Grund, und es geht keine Mail hinaus ([Regelungen und Vollmachten](hilfe:regelungen-vollmachten#vollmacht)).
- Ist beim Lieferanten keine Mailadresse eingetragen, bleibt das Feld **An** leer und wird eingetippt.

Wie die Knöpfe arbeiten: [Dokumente drucken, speichern und senden](hilfe:dokumente-drucken-speichern-senden).

## Selbst beim Lieferanten bestellen {#anlegen}

1. Im Reiter **Bestellen** auf **Bestellung anlegen** klicken.
2. Beim Entwurf auf **Bestelltext** klicken.
3. Auf **Kopieren** klicken.
4. Den Text selbst an den Lieferanten schicken.
5. Auf **Als bestellt markieren** klicken. Das Programm schickt dabei nichts hinaus.

Geänderte Mengen speichert das Programm vor dem Bestelltext und vor dem Markieren. Steht „Es ist nichts zu bestellen, oder dem Material fehlt der Lieferant.“, fehlt nichts oder dem Material ist kein Lieferant zugeordnet.

## Lieferung buchen {#lieferung}

1. Bei der Bestellung (Stand „bestellt“ oder „teilweise geliefert“) bei **Geliefert** die gelieferten Mengen eintragen. Vorbelegt ist die noch offene Menge. Eine Menge 0 heißt: Diese Position kam nicht.
2. Optional je Position **Charge** und **Ablaufdatum** und einmal den **Lieferschein** eintragen.
3. Auf **Lieferung buchen** klicken.

![Lager, Bestellung mit Feldern zur Lieferung und Bestelltext](images/app/verwaltung-lager-bestelltext.png)

Jede Position wird als Wareneingang zum Bestellpreis ins Hauptlager des Standorts gebucht. Fehlt noch etwas, steht die Bestellung auf „teilweise geliefert“. Lädt das Lager neu, bleiben begonnene Eingaben stehen.

## Abschließen oder stornieren {#abschliessen}

- **Stornieren**: eine Bestellung ohne Lieferung verwerfen. Das Programm sagt beim Lieferanten nicht ab; die Absage läuft von Hand.
- **Abschließen**: nach einer Teillieferung, wenn der Rest nicht mehr kommt.

Beides fragt vorher nach.

## Gut zu wissen {#hinweise}

- Hat im Spa jemand eine Vollmacht **Bestellungen auslösen**, auch eine abgelaufene, braucht jede Rolle außer Inhaber und Manager eine heute gültige, um zu senden oder als bestellt zu markieren. Siehe [Regelungen und Vollmachten](hilfe:regelungen-vollmachten#vollmacht).
- Hat jemand im Team die Zuständigkeit Lager, meldet der Kalender den Bestellvorschlag dieser Person statt der Leitung. Siehe [Zuständigkeiten und Zertifikate](hilfe:zustaendigkeiten-zertifikate#lager).
- Nur Entwürfe lassen sich ändern. Eine Lieferung lässt sich erst buchen, wenn die Bestellung als bestellt markiert ist.
- Offene Bestellungen werden vom Bestellvorschlag abgezogen und stehen im Bestand unter **Bestellt**.
- Bestellt und geliefert wird je Standort; die Ware kommt ins Hauptlager und von dort mit [Nachfüllen](hilfe:lager-nachfuellen) an die anderen Lagerorte.

Mehr dazu: [Lieferant](hilfe:lager#lieferant), [Mindestbestand](hilfe:lager#mindestbestand).
