---
art: einstellung
bereich: Lager
oberflaeche: verwaltung
rollen: leitung, inhaber, manager
seite: verwaltung
stichwoerter: Lagerort, Lagerorte, Hauptlager, Standardlager, Schrank, Raumschrank, Verkaufsregal, Regal, Wagen, Mindestmenge, Raum nachfüllen, storage location, shelf, cabinet, stock room
---
# Lagerorte

Ein Lagerort ist ein Platz, an dem Material liegt: das Hauptlager, der Schrank eines Behandlungsraums, das Verkaufsregal an der Rezeption oder ein Wagen. Jeder Standort hat sein Hauptlager (das Standardlager) und kann bis zu neun weitere Lagerorte haben, zusammen höchstens 10. Der Bestand eines Standorts ist die Summe seiner Lagerorte; Bedarfsprüfung, Bestellvorschlag und Lagerwert rechnen weiter mit dem ganzen Standort.

Lagerorte pflegen dürfen Inhaber und Standortleitung an ihren Standorten, in der Windows-App unter „Verwaltung“ › „Lager“ im Reiter **Lagerorte**. Links steht die Liste, rechts das Formular; **Neuer Lagerort** legt einen weiteren an.

## Lagerorte anlegen und ändern {#lagerorte}

1. In der Verwaltung **Lager** öffnen, bei mehreren Standorten den **Standort** wählen und den Reiter **Lagerorte** öffnen.
2. In der Liste einen Lagerort wählen oder auf **Neuer Lagerort** klicken.
3. **Name**, **Art** und bei Bedarf **Raum**, **Beschreibung** und **Mindestmengen** eintragen.
4. Auf **Speichern** klicken. Es erscheint „Gespeichert.“

Mit **Verwerfen** nimmt man ungespeicherte Änderungen zurück. **Löschen** geht nur, solange der Lagerort keine Bewegungen hat; sonst den Lagerort mit **Aktiv** ausschalten.

### Name {#name}

Wie der Lagerort in Auswahllisten und im Bestand heißt, zum Beispiel „Schrank Lotus“ oder „Keller“. Pflicht, höchstens 60 Zeichen, je Standort nur einmal.

### Art {#art}

Wofür der Lagerort da ist. Davon hängt ab, wohin das Programm selbst bucht:

- **Hauptlager**: gibt es je Standort genau einmal, es entsteht mit dem Standort und lässt sich umbenennen, aber weder löschen noch ausschalten. Wareneingang und Lieferungen kommen hierher.
- **Raum**: der Schrank eines Behandlungsraums. Wird eine Behandlung in diesem Raum abgeschlossen, bucht das Lager das Material von hier ab, wenn genug da ist; sonst vom Hauptlager.
- **Verkauf**: das Verkaufsregal. Verkäufe an der Kassa buchen vom ersten aktiven Verkaufsregal des Standorts ab, ein Storno bucht dorthin zurück. Ohne Verkaufsregal gilt das Hauptlager.
- **Sonstiger Ort**: alles andere, zum Beispiel ein Wagen oder ein Keller. Das Programm bucht dort nichts selbst.

### Raum {#raum}

Nur bei der Art „Raum“: zu welchem Behandlungsraum des Standorts der Schrank gehört. Pro Raum gibt es höchstens einen Lagerort. Ohne Raum bucht das Programm von diesem Schrank nichts ab.

### Beschreibung {#beschreibung}

Eine kurze Notiz für das Team, höchstens 160 Zeichen, zum Beispiel „Regal im Keller, Schlüssel bei der Rezeption“. Freiwillig.

### Aktiv {#aktiv}

Ausgeschaltet erscheint der Lagerort nicht mehr als Ziel beim Nachfüllen und Umbuchen; sein Bestand bleibt sichtbar und lässt sich mit Inventur oder Korrektur abbauen. Das Hauptlager bleibt immer aktiv.

## Mindestmengen {#mindestmengen}

Je Lagerort lässt sich zu jedem Material eine Mindestmenge eintragen, zum Beispiel „Massageöl 500 ml“ im Schrank eines Raums. Liegt der Bestand dort darunter, zeigt der Bestand des Lagerorts die Zeile mit „unter Minimum“, und man füllt aus dem Hauptlager nach: [Nachfüllen](hilfe:lager-nachfuellen).

- **Mindestmenge hinzufügen** fügt eine Zeile hinzu, das Kreuz rechts entfernt sie.
- Ohne Eintrag gilt am Lagerort kein Mindestwert.
- Der **Mindestbestand je Standort** beim Material bleibt davon unberührt und gilt für die Summe aller Lagerorte.
- Die **Höchstmenge** in derselben Zeile ist freiwillig und nicht kleiner als die Mindestmenge. Die Höchstmengen aller Lagerorte zusammen sind das Ziel des [Bestellvorschlags](hilfe:lager-bestellen#rechnung). Die Mindestmenge darf leer bleiben, sie zählt dann als 0.

### Raumausstattung {#raumausstattung}

Bei einem Lagerort der Art **Raum** sind die Mindestmengen die Raumausstattung: was im Schrank des Raums liegen soll, z. B. „Massageöl neutral 500 ml, Handtücher 6 Stück, Einweg-Liegenauflage 10 Stück“. Nach jeder Behandlung zeigt die Raumpflege, was darunter liegt, und das Team füllt aus dem Hauptlager nach: [Raum nach der Behandlung prüfen](hilfe:kalender-raumcheck). Ohne Eintrag gibt es für den Raum keinen Raumcheck.

### Von Raum übernehmen {#uebernehmen}

Damit die Raumausstattung nicht für jeden Raum einzeln getippt wird, gibt es unter den Mindestmengen die Auswahl **Lagerort** und den Knopf **Von Raum … übernehmen**.

1. Den Lagerort wählen, der die gewünschten Mindestmengen schon hat (zur Auswahl stehen nur Lagerorte mit Mindestmengen).
2. Auf **Von Raum … übernehmen** klicken. Es erscheint „Mindestmengen übernommen.“

Die Mindestmengen des gewählten Lagerorts ersetzen die vorhandenen des aktuellen Lagerorts und sind sofort gespeichert; der Knopf ist erst aktiv, wenn keine ungespeicherten Änderungen offen sind. Danach lassen sich einzelne Zeilen ändern. Das dürfen Inhaber und Standortleitung.
