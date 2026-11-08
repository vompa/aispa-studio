---
art: einstellung
bereich: Einzelplatz
oberflaeche: windows-app, verwaltung
rollen: inhaber, manager
---
# Einzelplatz

Im Einzelplatz-Betrieb läuft AISPA Studio ganz auf einem Windows-PC, ohne Server und ohne Internet. Die Windows-App startet dabei selbst eine eigene Datenbank (PostgreSQL) und AISPA Studio im Hintergrund. Kalender, Kunden, Kassa, Dienstplan, Berichte und Zugänge funktionieren wie mit Server. Die Webmodule (Online-Buchung, Terminannahme über Mail und Telefon, KI-Assistent, Erinnerungen) sind im Einzelplatz in der Grundeinstellung ausgeschaltet.

Weitere PCs im Spa verbinden sich bei Bedarf mit diesem PC (siehe „Im Netz“). Eine zentrale Datenbank für mehrere Standorte lässt sich später dazuschalten (siehe „Abgleich“).

## Einzelplatz einschalten {#einschalten}

Die App wird einmal mit dem Schalter `--standalone` gestartet, z. B. über eine Verknüpfung. Sie merkt sich das; jeder weitere Start läuft dann ohne Schalter als Einzelplatz. Mit `--server` schaltet man auf den Betrieb mit Server zurück, auch das wird gemerkt. Die Daten des Einzelplatzes bleiben dabei auf dem PC liegen.

## Wo die Daten liegen {#daten}

- Alle Daten liegen im Ordner `%LOCALAPPDATA%\AispaStudio\daten` des Windows-Kontos, mit dem die App läuft. Die Datenbank ist nur auf diesem PC selbst erreichbar.
- Das Passwort der Datenbank und der Schlüssel für die Gesundheitsdaten entstehen beim ersten Start zufällig und liegen mit Windows geschützt in `zugang.bin`. Lesen kann sie nur dasselbe Windows-Konto auf demselben PC. Deshalb immer mit demselben Windows-Konto arbeiten.
- Gesundheitsdaten (Anamnesen) sind in der Datenbank zusätzlich verschlüsselt.
- Die Seite „Anmelden“ (`/anmelden/`) und „Mein Tag“ (`/mein-tag/`) sind im Browser auf diesem PC unter der Adresse erreichbar, die beim Einrichten angezeigt wird (`http://127.0.0.1:…`). Verwaltet wird nur in der Windows-App.
- Startet der Einzelplatz nicht, zeigt die App „Einzelplatz startet nicht“ mit dem Grund und dem Pfad zum Protokoll. Von dort geht auch „Aus einer Sicherung wiederherstellen“.

Weil der PC ohne Abgleich die einzige Kopie aller Daten ist, gehört zum Einzelplatz immer die tägliche Sicherung (siehe „Sicherung“).

## Spa einrichten {#einrichten}

Beim allerersten Start ist die Datenbank leer, und die App zeigt „Spa einrichten“. Das geht nur direkt an diesem PC, nicht über das Netz, und nur einmal. Danach legt die Leitung alle weiteren Zugänge unter „Zugänge“ an.

### Name des Spas {#spa-name}

Der Name des Betriebs, wie er in der App erscheint. Pflichtfeld.

### Land {#land}

Österreich (Standard), Deutschland oder Schweiz. Das Land wird beim Spa gespeichert und legt für den ersten Standort Zeitzone und Währung fest: Österreich Wien und EUR, Deutschland Berlin und EUR, Schweiz Zürich und CHF.

### Erster Standort {#erster-standort}

Name des ersten Standorts, z. B. Ort oder Adresse. Pflichtfeld. Weitere Standorte legt der Inhaber später an.

### Ihr Name {#inhaber-name}

Der Name der Person, die einrichtet. Sie bekommt die Rolle Inhaber mit Zugang zu allen Standorten. Pflichtfeld.

### Benutzername und Passwort {#zugang}

Damit meldet sich der Inhaber künftig an. Der Benutzername ist frei wählbar (Umlaute erlaubt). Das Passwort hat mindestens 8 Zeichen, sonst gibt es keine Vorgaben; es muss zweimal gleich eingegeben werden.

„Spa einrichten“ legt Spa, ersten Standort und Inhaber-Konto an und meldet gleich an.

## Mit einem bestehenden Spa verbinden {#verbinden}

Für einen weiteren PC eines Spas, das schon mit einer zentralen Datenbank abgleicht, z. B. ein zweiter Standort mit eigenem PC. Statt ein neues Spa einzurichten, holt dieser PC beim ersten Abgleich das ganze Spa samt Zugängen. Der Link „Mit einem bestehenden Spa verbinden …“ steht unter „Spa einrichten“.

### Verbindung zur zentralen Datenbank {#verbinden-verbindung}

Dieselbe Art Angabe wie unter „Abgleich“, z. B. `Host=db.meinspa.at;Database=aispadb;Username=badischl;Password=…;SSL Mode=Require`. Die Leitung bekommt sie vom Betreiber der zentralen Datenbank; am besten ein eigenes Konto für jeden PC. Die Eingabe wird verdeckt angezeigt.

### Schlüsselcode des Spas {#verbinden-code}

Der Code aus 24 Zeichen, den die Leitung am ersten PC unter „Abgleich“ mit „Schlüsselcode anzeigen“ sieht. An ihm erkennt dieser PC, zu welchem Spa er gehört; ohne ihn geht das Verbinden nicht.

„Verbinden“ geht, sobald beides eingetragen ist. Danach zeigt die App „Mit dem Spa verbinden“ und prüft alle fünf Sekunden, ob das Spa angekommen ist. Beim ersten Mal dauert das etwas. Schlägt der Abgleich fehl, steht der Grund dort; „Neu prüfen“ fragt sofort nach. Sobald das Spa da ist, erscheint die Anmeldung, und jede Person meldet sich mit ihrem gewohnten Benutzernamen und Passwort an.

## Aus einer Sicherung wiederherstellen {#wiederherstellen}

Für einen neuen PC, nachdem der alte ausgefallen ist, oder wenn die Zugangsdaten auf diesem PC nicht mehr lesbar sind (z. B. nach einem neuen Windows-Konto). Der Link „Aus einer Sicherung wiederherstellen …“ steht unter „Spa einrichten“ und auf der Seite „Einzelplatz startet nicht“.

- Wiederherstellen geht nur, solange auf diesem PC noch kein Spa eingerichtet ist. So wird nie etwas überschrieben.
- Vorhandene Daten auf diesem PC werden beiseitegelegt (Ordner mit „vor-wiederherstellung“ und Datum im Namen), nicht gelöscht.
- Danach meldet sich jede Person an wie zum Zeitpunkt der Sicherung. Was nach der Sicherung eingegeben wurde, fehlt.
- War der PC „Im Netz“ freigegeben, muss er neu freigegeben werden; dabei entsteht ein neuer Verbindungscode.

### Sicherungsdatei {#sicherungsdatei}

Der volle Pfad der Sicherung, z. B. `D:\Sicherung\aispa-20261019-080000.dump`, oder über „Datei wählen“. Die Schlüsseldatei mit demselben Namen und der Endung `.schluessel.json` muss im selben Ordner liegen. Meist nimmt man die neueste Sicherung.

### Wiederherstellungsschlüssel {#wiederherstellen-schluessel}

Der Code aus 24 Zeichen, der beim Einrichten der Sicherung notiert wurde. Bindestriche sind egal. Passt er nicht zur Sicherung, meldet die App „Der Wiederherstellungsschlüssel passt nicht zu dieser Sicherung.“ Wurde zwischendurch ein neuer Schlüssel erzeugt, brauchen ältere Sicherungen den alten.

„Wiederherstellen“ geht, sobald beide Felder ausgefüllt sind. Die App zeigt die Schritte an und startet danach mit den wiederhergestellten Daten.

## Anmelden {#anmelden}

Im Einzelplatz meldet sich jede Person mit Benutzername und Passwort an. Nach fünf falschen Passwörtern ist das Konto fünf Minuten gesperrt. Neue Personen bekommen von der Leitung unter „Zugänge“ einen Benutzernamen und ein Startpasswort.

### Eigenes Passwort wählen {#passwort-waehlen}

Wer mit einem Startpasswort angemeldet ist, muss zuerst ein eigenes Passwort mit mindestens 8 Zeichen wählen und zweimal eingeben. Vorher geht nichts anderes.

### Passwort vergessen {#passwort-vergessen}

Ein vergessenes Passwort setzt normalerweise die Leitung unter „Zugänge“ neu. Gibt es niemanden mit diesem Recht (z. B. die Inhaberin hat ihr eigenes vergessen), geht es mit dem Wiederherstellungsschlüssel:

- **Benutzername**: das Konto, dessen Passwort neu gesetzt wird.
- **Wiederherstellungsschlüssel**: der Code aus 24 Zeichen von der Seite „Sicherung“.
- **Neues Passwort** und **Neues Passwort wiederholen**: mindestens 8 Zeichen.

„Passwort setzen und anmelden“ setzt das Passwort, hebt eine Sperre nach Fehlversuchen auf, beendet alte Anmeldungen und meldet an. Das geht nur direkt am Haupt-PC, nicht von einem anderen PC im Netz, und nur, wenn eine Sicherung mit Wiederherstellungsschlüssel eingerichtet ist.
