# 0068. Lagerorte je Standort und neue Lager-Maske

- **Status:** angenommen
- **Datum:** 21.10.2026
- **Entschieden von:** Auftraggeber (Vorgabe vom 21.10.2026: „jeder Standort hat ein eigenes bzw. mehrere Lager“; „die Maske muss professioneller werden“), Ausgestaltung Claude

## Kontext

Seit [ADR 0037](0037-lager-aus-bewegungen.md) führt das Lager den Bestand je Standort als Summe unveränderlicher Bewegungen. Ein Standort hat damit genau einen Bestand je Material. In Day-Spas liegt Material aber an mehreren Orten: im Hauptlager, im Schrank des Behandlungsraums, im Verkaufsregal an der Rezeption, auf einem Wagen. Inventur je Ort, Nachfüllen der Räume aus dem Hauptlager und die Frage „was liegt im Raum Lotus“ sind heute nicht abbildbar. Die Lager-Seite der Verwaltung ist eine lange Tabelle ohne Gliederung, die Spaltenköpfe sind linksbündig über rechtsbündigen Zahlen, Aktionen fehlen in der Übersicht.

## Optionen

| Option | Bewertung |
| --- | --- |
| Lagerorte als eigene Standorte anlegen | missbraucht den Standort (Öffnungszeiten, Kassa, Dienstplan hängen daran); Umbuchungen zwischen „Standorten“ im selben Haus |
| Nur ein Feld „Lagerort“ als freier Text an der Bewegung | keine Bestände je Ort, keine Inventur je Ort, keine Pflege |
| Entity `StoragePlace` je Standort, jede Bewegung trägt einen Lagerort, Bestand je Lagerort, Standortsumme bleibt | gewählt: Bewegungslogik bleibt, nur eine Dimension mehr |
| Bestand je Lagerort und Charge als eigene Tabelle | widerspricht ADR 0037 (Bestand nur aus Bewegungen); nicht nötig |

## Entscheidung

### Modell

- Neue Entity `StoragePlace`: `Id`, `LocationId`, `Name` (höchstens 60 Zeichen), `Description` (kurze Beschreibung, höchstens 160 Zeichen, z. B. „Regal im Keller, Schlüssel bei der Rezeption“), `Kind` (`Main`, `Room`, `Sales`, `Other`), `RoomId` (optional, nur bei `Room`, Raum desselben Standorts), `IsActive`, `Position` (Sortierung). Je Standort genau ein Lagerort der Art `Main` als Standardlager; er heißt „Hauptlager“ und entsteht mit dem Standort (Migration legt ihn für bestehende Standorte an; das Anlegen eines Standorts legt ihn mit an). Er lässt sich umbenennen und beschreiben, nicht löschen, nicht deaktivieren. **Höchstens 10 Lagerorte je Standort** (Auftraggeber, 21.10.2026: „default ein Standardlager, optional bis zu 10 Lager“); der elfte wird mit „Höchstens 10 Lagerorte je Standort.“ abgelehnt. Mit nur dem Hauptlager zeigt die Maske keine Lagerort-Auswahl.
- `StockMovement.StoragePlaceId` (Pflicht). Die Migration setzt alle bestehenden Bewegungen auf das Hauptlager ihres Standorts. `TargetStoragePlaceId` für Umbuchungen innerhalb des Standorts; `TargetLocationId` bleibt für Umbuchungen zwischen Standorten (die landen im Hauptlager des Ziels).
- Neue Bewegungsart `Refill` („Nachfüllen“): Menge > 0 vom Hauptlager (oder einem anderen Lagerort) an einen Lagerort desselben Standorts; gebucht wie `Transfer` als zwei Bewegungen (negativ am Ausgang, positiv am Ziel), Charge und Ablaufdatum wandern mit (zuerst ablaufend zuerst).
- Bestand je Lagerort = Summe seiner Bewegungen; Bestand je Standort = Summe aller Lagerorte. Chargen gelten je Lagerort.
- `StockItem.MinimumQuantity` bleibt die Mindestmenge je Standort. Zusätzlich optional `StoragePlaceMinimum` (Lagerort, Material, Mindestmenge) für „Raum nachfüllen, wenn unter …“; ohne Eintrag gilt kein Mindestwert je Lagerort.

### Regeln für automatische Bewegungen

| Vorgang | Lagerort |
| --- | --- |
| Wareneingang, Lieferung zu einer Bestellung | Hauptlager des Standorts |
| Verbrauch beim Abschluss eines Termins | Lagerort der Art `Room` mit `RoomId` = Raum des Termins, wenn dort genug Bestand ist; sonst Hauptlager. Reicht beides nicht, bucht das Lager wie heute ins Minus am Hauptlager und warnt |
| Verkauf an der Kassa (Artikel verknüpft) | erster aktiver Lagerort der Art `Sales` des Standorts der Kasse; ohne einen solchen das Hauptlager |
| Storno eines Verkaufs | derselbe Lagerort wie der Verkauf |
| Inventur | je Lagerort (die Zählung gilt für den gewählten Lagerort; die Differenz wird dort gebucht) |
| Korrektur | gewählter Lagerort |
| Bedarfsprüfung, Bestellvorschlag, Lagerwert, Vollkosten | Standortsumme (unverändert) |

### Schnittstelle

- `GET/POST/PUT /api/stock/locations/{locationId}/storage-places`, Recht `ManageLocation` für Schreiben; Lesen mit `ViewCalendar` (wie die übrigen Lese-Endpunkte des Lagers).
- `GET /api/stock/locations/{locationId}` liefert je Material weiterhin die Standortwerte und zusätzlich `Places: [{StoragePlaceId, Quantity}]`.
- `GET /api/stock/locations/{locationId}/storage-places/{placeId}/levels` liefert die Bestandsliste eines Lagerorts (Menge, Mindestmenge je Lagerort, Chargen).
- `StockMovementInput` bekommt `StoragePlaceId` (Pflicht außer bei `GoodsReceipt`, dort Hauptlager) und `TargetStoragePlaceId` (nur `Refill`). `StockMovementDto` nennt `StoragePlaceId` und `StoragePlaceName`.
- Alle Endpunkte prüfen, dass der Lagerort zum Standort gehört; sonst 400 „Der Lagerort gehört nicht zu diesem Standort.“

### Verwaltung › Lager (neue Maske, nach `docs/design.md`)

1. **Kopf:** Reiterschiene mit **Bestand**, **Bewegungen**, **Bestellungen**, **Inventur**, **Lagerorte** (letzter nur mit `ManageLocation`). Darunter eine Filterzeile: Auswahl **Standort** (nur sichtbar bei mehr als einem), Auswahl **Lagerort** („Alle Lagerorte“ als erster Eintrag, dann die Lagerorte in `Position`), rechts außen der primäre Knopf der Seite („Bewegung buchen“ auf Bestand, „Bestellung anlegen“ auf Bestellungen, „Zählung starten“ auf Inventur).
2. **Bestand:** drei Kennzahl-Kacheln (Lagerwert, Bestellvorschlag in Positionen, Unter Mindestbestand in Positionen) in einer Reihe. Darunter die Tabelle: Material, Einheit, Bestand (bei „Alle Lagerorte“ die Standortsumme, aufklappbar je Lagerort), Mindestbestand, Verbrauch geplant, Bestellt, Bestellvorschlag, Läuft ab, Wert; alle Zahlenspalten rechtsbündig samt Kopf, tabular. Positionen unter Mindestbestand bekommen eine Pille `warn` „unter Minimum“ hinter dem Namen, abgelaufene Chargen eine Pille `crit` „abgelaufen“. Suchfeld über der Tabelle (Material), Filter „Nur unter Minimum“ als Auswahl-Chip. Je Zeile rechts zwei Symbol-Knöpfe klein: „Nachfüllen“ (nur bei gewähltem Lagerort ungleich Hauptlager) und „Bewegung buchen“.
3. **Bewegung buchen** öffnet eine Seitenleiste (5.12 im Design-Brief) mit Art als Reiter (Wareneingang, Nachfüllen, Umbuchen, Korrektur), Material (Auswahl mit Suche), Lagerort (vorbelegt), Menge, bei Nachfüllen und Umbuchen das Ziel, bei Wareneingang Preis, Charge, Ablaufdatum, bei Korrektur der Grund (Pflicht). Primär „Buchen“, Meldung unten „Gebucht.“
4. **Bewegungen:** Filterzeile Von/Bis, Tabelle Datum, Art (Pille), Material, Lagerort, Menge (rechts, negativ `danger`), Preis, Grund/Bezug (Termin, Beleg, Bestellung als Link).
5. **Bestellungen:** wie heute, in Tabellenform nach 5.7; Lieferung bucht ins Hauptlager.
6. **Inventur:** Lagerort wählen (Pflicht), Tabelle mit Soll (ausblendbar), Zählfeld je Zeile (Zahl, Breite 120), Differenz und Wert live, primär „Inventur buchen“ mit Rückfrage (Modal, gefährlich bestätigt).
7. **Lagerorte:** Liste links (5.6) mit Name und Art, rechts Formular: Name, Art, Raum (nur bei Raum), Aktiv, Mindestmengen je Material (Zeilenliste). „+ Neuer Lagerort“. Hauptlager ohne Löschen/Deaktivieren.
8. Jede Einstellung hat ihren Hilfe-Knopf; Hilfethemen `lager-lagerorte.md` (neu), `lager-nachfuellen.md` (neu), bestehende `lager-*.md` um den Lagerort ergänzen.

### Wer

Die Rechte sind die bestehenden des Lagers (`StockEndpoints.cs`): Sehen mit `ViewCalendar`; Wareneingang, Nachfüllen, Umbuchen und Korrektur mit `Book` (Rezeption, an ihrem Standort); Lagerorte anlegen, Inventur buchen und Bestellungen mit `ManageLocation` (Inhaber, Standortleitung). Verbrauch und Verkauf bucht das System.

### Raumcheck nach der Behandlung (vom Auftraggeber am 21.10.2026 freigegeben; eigener Schritt nach den Lagerorten)

Frage des Auftraggebers: Soll jeder Raum einen Mindestbestand an Utensilien haben, den die Mitarbeitenden nach der Behandlung kontrollieren und auffüllen? Entscheidung: ja, so:

- **Daten:** die Mindestmengen je Lagerort (`StoragePlaceMinimum`) am Raumschrank sind die „Raumausstattung“ („Massageöl neutral 500 ml, Handtücher 6 Stk, Einweg-Liegenauflage 10 Stk“). Gepflegt im Reiter Lagerorte, mit „Von Raum … übernehmen“, damit nicht jeder Raum einzeln getippt wird.
- **Auslöser:** Der Verbrauch wird beim Abschluss des Termins automatisch vom Raumschrank gebucht. Danach steht die bestehende Raumpflege-Aufgabe (ADR 0065, Streifen in der Raumansicht, Karte in „Mein Tag“) an; sie bekommt zusätzlich die Zeilen des Raumschranks, die unter ihrer Mindestmenge liegen: „Massageöl neutral: 200 von 500 ml, 300 ml nachfüllen“.
- **Bedienung:** Je Zeile zwei Knöpfe: **Aufgefüllt** (bucht `Refill` vom Hauptlager in den Raumschrank mit der vorgeschlagenen Menge, änderbar) und **Reicht noch** (lässt die Zeile stehen, bis zum nächsten Check). Die Raumpflege gilt erst als erledigt, wenn jede Zeile beantwortet ist. Keine Zeilen unter Minimum: Raumpflege wie heute.
- **Sichtbar:** in der Raumansicht des Windows-Kalenders (Klick auf den Raumpflege-Streifen öffnet die Liste in der Seitenleiste), in „Mein Tag“ auf der Karte nach der Behandlung, in der Tagesstart-Kontrolle je Raum (ADR 0065) als eigener Punkt „Ausstattung vollständig“, in der Betriebsleiste als Zähler „2 Räume unter Mindestbestand“.
- **Nicht:** keine Sperre des nächsten Termins, kein automatisches Nachfüllen ohne Bestätigung, keine Zählung des ganzen Schranks nach jedem Termin (das bleibt die Inventur je Lagerort).

- **Wann:** nur, wenn nach dem Verbrauch mindestens eine Zeile unter ihrer Mindestmenge liegt; nicht nach jeder Behandlung, damit der Klick nicht zur Routine ohne Blick wird.

Umfang: ein eigener Schritt nach der Lagerort-Umsetzung (Auftrag T-009), weil er Kalender (Raumpflege-Streifen, Seitenleiste), „Mein Tag“ und Tagesstart berührt.

## Begründung

Eine zusätzliche Dimension an der Bewegung hält die Grundregel von ADR 0037 (Bestand nur aus Bewegungen, nichts wird geändert oder gelöscht) und macht Inventur, Nachfüllen und Raumbestände möglich, ohne das Standort-Modell zu verbiegen. Standortsummen bleiben für Bedarf und Kosten, damit nichts Bestehendes anders rechnet. Die neue Maske folgt dem Design-Brief, damit sie wie die übrige Verwaltung aussieht und schwächere Modelle sie ohne Deutung umsetzen können.

## Folgen und Stand

- Umsetzung auf `dev` (Auftrag T-008 in der Wissensbasis). Migration `Lagerorte` legt Hauptlager an und setzt bestehende Bewegungen darauf.
- Gebaut auf `dev` (Commit siehe PR-Protokoll).
- Raumcheck gebaut auf `dev` (Commit siehe PR-Protokoll).
- Demo-Daten: jeder Standort bekommt ein Hauptlager, ein Verkaufsregal und je Behandlungsraum einen Raumschrank mit kleinem Anfangsbestand (Nachfüllung aus dem Hauptlager am ersten Demo-Tag), damit Verbrauch aus dem Raum und Rückfall sichtbar sind.
- Vollkosten, Bedarfsprüfung und Bestellvorschlag bleiben unverändert (Standortsumme).
- Offen: Barcode-Scan und Zählliste nach Zone (Lager-KI-Konzept S1), Mindestmengen je Lagerort als Vorschlag der KI.

## Quellen

- [ADR 0037](0037-lager-aus-bewegungen.md)
- [Design-Brief](../design.md)
- [Hilfe: Lager](../../web/src/help/content/lager.md)
