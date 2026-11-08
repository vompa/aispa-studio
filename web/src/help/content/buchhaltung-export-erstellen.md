---
art: ablauf
bereich: Buchhaltung
oberflaeche: verwaltung
rollen: leitung, inhaber, manager
seite: verwaltung
stichwoerter: Export, Buchhaltungsexport, Steuerberatung, Steuerberater, Monatsabschluss, DATEV, Buchungsstapel, BMD, NTCS, RZL, Abacus, bexio, CSV, Datei herunterladen, Buchungen, BU-Schlüssel fehlt, export, accountant, download
---
# Export für die Buchhaltung erstellen

Auf der Seite „Buchhaltung“ entsteht aus den Kassenbelegen eines Zeitraums eine Datei für DATEV, BMD, RZL oder die Schweiz (Abacus, bexio), die man der Steuerberatung schickt.

## Datei erstellen {#datei}

1. In der Windows-App unter „Verwaltung“ **Buchhaltung** öffnen.
2. Unter **Export für die Buchhaltung** **Von** und **Bis** setzen, z. B. den ersten und letzten Tag des Vormonats. Vorbelegt sind der Monatserste und heute.
3. Auf die Datei für die Buchhaltung der Steuerberatung klicken: **DATEV-Buchungsstapel**, **BMD-Import**, **RZL-Import** oder **Schweiz (Abacus, bexio)**.
4. Sobald die Datei fertig ist, fragt die App, wo sie die Datei speichern soll. Sie an die Steuerberatung schicken. Schlägt der Export fehl, steht eine Meldung da (Verbindung prüfen und noch einmal versuchen) und es entsteht keine Datei.

![Export für die Buchhaltung mit Vorschau](images/app/verwaltung-buchhaltung.png)

## Vorher am Bildschirm prüfen {#vorschau}

1. **Von** und **Bis** setzen.
2. **Buchungen anzeigen** anklicken.
3. Die Tabelle zeigt je Buchung Datum, Beleg, Soll, Haben, Betrag, USt, Kostenstelle und Text. Ohne Belege steht dort „Keine Buchungen im Zeitraum.“

## DATEV: wenn ein BU-Schlüssel fehlt {#bu-schluessel}

Vor dem DATEV-Download prüft das Programm, ob jeder Steuersatz einen BU-Schlüssel hat. Fehlt einer, gibt es keine Datei, sondern eine Meldung wie „Für 20 % (Kassen in Österreich) fehlt der DATEV-BU-Schlüssel.“

1. Unter **Kontenzuordnung** bei **Gilt für** das genannte Land wählen.
2. Im Feld **DATEV-BU-Schlüssel je Steuersatz (z. B. 19=3;7=2)** die Schlüssel der Steuerberatung ergänzen, z. B. `20=…`, oder `20=` ohne Schlüssel für ein Automatikkonto.
3. **Speichern** (nur Inhaber und Manager) und den Export noch einmal starten.

Für Österreich passen meist BMD oder RZL; diese Dateien brauchen keinen BU-Schlüssel.

## Hinweise {#hinweise}

- Wer das Recht „Berichte mit Beträgen“ hat ([Berichte mit Beträgen](hilfe:zugaenge#berichte-mit-betraegen)); die Standortleitung bekommt nur die Kassen ihrer Standorte.
- Der Zeitraum darf höchstens ein Jahr lang sein, beide Tage zählen mit.
- DATEV, BMD und RZL enthalten die Kassen in Euro, die Schweizer Datei die Kassen in CHF.
- Bei RZL, Abacus und bexio ordnet man die Spalten beim ersten Import einmal zu.
- Was in den Dateien steht und wie gebucht wird: [Buchhaltung, Export](hilfe:buchhaltung#export) und [Wie gebucht wird](hilfe:buchhaltung#buchungslogik).
- Konten anpassen: [Kontenzuordnung anpassen](hilfe:buchhaltung-kontenzuordnung-anpassen).
