---
art: einstellung
bereich: Einzelplatz
oberflaeche: windows-app, verwaltung
rollen: inhaber, manager, leitung
seite: abgleich
---
# Abgleich

Die Seite „Abgleich“ gibt es nur im Einzelplatz-Betrieb der Windows-App. Dieser PC arbeitet dann mit seiner eigenen Datenbank. Wird eine zentrale Datenbank (PostgreSQL) eingetragen, gleicht er sich mit ihr ab, und über sie mit anderen Standorten oder PCs, die ebenfalls mit ihr abgleichen.

Den Stand und abgelehnte Einträge sehen alle, die angemeldet sind. Als erledigt markieren darf, wer Termine buchen darf (Inhaber, Standortleitung, Rezeption). Die zentrale Datenbank eintragen, austragen und den Schlüsselcode anzeigen darf nur, wer den Standort verwalten darf (Inhaber, Standortleitung).

## Was der Abgleich macht {#ablauf}

- Ist eine zentrale Datenbank eingetragen, gleicht AISPA Studio jede Minute in beide Richtungen ab: Änderungen dieses PCs gehen hinaus, Änderungen der anderen Stellen kommen herein.
- Beim ersten Mal wird der ganze Bestand übertragen; das kann etwas dauern.
- Ohne Verbindung (z. B. Internet weg) arbeitet der PC normal weiter und holt den Abgleich nach, sobald die zentrale Datenbank wieder erreichbar ist. Auch die Kassa signiert in dieser Zeit lokal und überträgt die Belege danach.
- Ohne zentrale Datenbank liegen alle Daten nur auf diesem PC; die tägliche Sicherung ist dann die einzige Kopie (siehe „Sicherung“).

## Stand {#stand}

Zeigt, welche zentrale Datenbank eingetragen ist (Server und Name der Datenbank, nie das Passwort), wann zuletzt abgeglichen wurde und wie viele Änderungen dieses PCs noch auf den nächsten Abgleich warten („Alle Änderungen dieses PCs sind übertragen.“ oder „… Änderung(en) warten auf den nächsten Abgleich.“).

Ist der letzte Abgleich fehlgeschlagen, steht der Grund in einer Warnung unter „Stand“, z. B. keine Verbindung zum Server oder ein Schlüsselcode, der nicht passt. Der nächste Versuch folgt von selbst. „Neu laden“ holt den aktuellen Stand, ohne einen Abgleich auszulösen.

## Beim Abgleich abgelehnt {#abgelehnt}

Hier stehen Einträge, die eine Seite nicht übernehmen konnte, mit Bereich (z. B. Termine, Eintritte, Kunden, Dienstplan, Kassa), Zeitpunkt und Grund. Zwei Gründe kommen vor:

- „Raum, Person oder Gerät ist zur selben Zeit schon gebucht.“: Zwei Stellen haben gleichzeitig dieselbe Zeit vergeben.
- „schon vorhanden“: Ein Eintrag, der eindeutig sein muss, existiert schon.

„Zentral abgelehnt“ heißt, die zentrale Datenbank hat die Zeile dieses PCs nicht übernommen; „Hier abgelehnt“ heißt, dieser PC hat eine Zeile von dort nicht übernommen. Jede Seite behält ihre eigene Zeile, nichts wird verworfen. Klären, welche gilt, die andere anpassen oder löschen (z. B. den Termin verschieben) und dann „Erledigt“ wählen. „Erledigt“ entfernt nur den Hinweis aus der Liste; es ändert keine Daten. Die Zahl neben „Abgleich“ in der Navigation zeigt, wie viele Hinweise offen sind.

## Zentrale Datenbank {#zentrale-datenbank}

Nur für Inhaber und Standortleitung sichtbar.

### Verbindung {#verbindung}

Die Verbindungsangabe der zentralen PostgreSQL-Datenbank, z. B. `Host=db.meinspa.at;Database=aispadb;Username=gmunden;Password=…;SSL Mode=Require`.

- Pflicht sind Server (`Host`), Datenbank (`Database`) und Konto (`Username`); dazu meist `Password`. Ohne Angabe gilt Port 5432.
- Am besten bekommt jede Stelle ein eigenes Datenbankkonto und die Verbindung ist mit TLS verschlüsselt (`SSL Mode=Require`).
- Die Datenbank dieses PCs selbst wird abgelehnt („Das ist die Datenbank dieses PCs.“).
- Die App prüft beim Eintragen nur die Form. Ob der Server antwortet, zeigt der erste Abgleich unter „Stand“.
- Die Angabe liegt geschützt nur auf diesem PC (an das Windows-Konto gebunden) und wird nach dem Eintragen nicht mehr angezeigt.

### Schlüsselcode des Spas {#schluesselcode}

Ein Code aus 24 Zeichen in sechs Vierergruppen, z. B. `K7QM-2XRT-H9PW-4CNE-YB3D-ZF6A`. Mit ihm lesen alle Stellen die Gesundheitsdaten (Anamnesen) der anderen. In der zentralen Datenbank liegen die Schlüssel dafür nur mit diesem Code verschlüsselt; der Code selbst liegt nie in einer Datenbank und nie neben einer Sicherung.

- **Erste Stelle**: Feld leer lassen. Die App erzeugt den Code und zeigt ihn einmal in der Warnung „Schlüsselcode notieren“. Notieren und sicher aufbewahren, dann „Verstanden“.
- **Jede weitere Stelle**: den Code der ersten Stelle eintragen. Groß- und Kleinschreibung und Bindestriche spielen keine Rolle.
- Ist auf diesem PC schon ein Code gespeichert, kann das Feld leer bleiben; dann gilt der gespeicherte weiter. Ein eingetragener Code ersetzt den gespeicherten.
- Passt der Code nicht zu dem der anderen Stellen, bricht der Abgleich ab, bevor etwas übertragen wird, und „Stand“ zeigt das.
- Einen Code für alle Stellen gleichzeitig wechseln geht noch nicht. Wird er an einer Stelle geändert, melden die anderen „Schlüsselcode passt nicht“, bis sie den neuen eintragen.

### Eintragen {#eintragen}

Geht, sobald eine Verbindung eingetragen ist. Speichert Verbindung und Schlüsselcode und startet AISPA Studio auf diesem PC kurz neu, damit der Abgleich gleich beginnt; die Datenbank läuft dabei weiter. Danach erscheint „Gespeichert. Der Abgleich startet jetzt; beim ersten Mal wird der ganze Bestand übertragen.“ Eine schon eingetragene Verbindung wird so auch geändert.

### Schlüsselcode anzeigen {#schluesselcode-anzeigen}

Zeigt den auf diesem PC gespeicherten Schlüsselcode noch einmal an, z. B. um eine weitere Stelle oder einen neuen PC zu verbinden. Nur für Inhaber und Standortleitung; der Knopf erscheint, sobald ein Code gespeichert ist.

### Abgleich ausschalten {#ausschalten}

Trägt die zentrale Datenbank aus und startet AISPA Studio auf diesem PC kurz neu. Die Daten bleiben auf diesem PC, die zentrale Datenbank behält ihren Stand. Der Schlüsselcode bleibt gespeichert. Später lässt sich der Abgleich mit „Eintragen“ wieder einschalten.

## Was bei Widersprüchen gilt {#regeln}

- **Stammdaten, Kunden, Dienstplan**: Die zuletzt gespeicherte Änderung gilt.
- **Termine**: Über die Belegung entscheidet die zentrale Datenbank. Kollidiert ein hier gebuchter Termin, wird er nicht gelöscht, sondern unter „Beim Abgleich abgelehnt“ gezeigt.
- **Kassa**: Jede Kasse gehört genau einer Stelle. Belege, Kassenjournal und Tagesabschlüsse werden nur übertragen, nie überschrieben und nie an einer anderen Stelle nachsigniert.
- **Gesundheitsdaten** bleiben verschlüsselt; übertragen wird nur der verschlüsselte Inhalt.
