---
art: einstellung
bereich: Buchhaltung
oberflaeche: verwaltung
rollen: inhaber, manager
seite: verwaltung
---
# Buchhaltung

Die Seite „Buchhaltung“ bereitet die Kassenbelege für die Steuerberatung auf: Export als Datei, eigene Erlöskonten und die Zuordnung der Konten. Die Seite sehen Personen mit dem Recht „Berichte mit Beträgen“ ([Berichte mit Beträgen](hilfe:zugaenge#berichte-mit-betraegen)): Inhaber, Manager und Backoffice immer, Standortleitung und Rezeption nur, wenn es vergeben ist. Jeder Beleg ist voll versteuert und signiert; der Export lässt nichts weg, getrennte Erlöskonten trennen nur die Auswertung.

## Export für die Buchhaltung {#export}

Erzeugt die Buchungen eines Zeitraums aus den Kassenbelegen. Die Standortleitung bekommt nur die Kassen ihrer eigenen Standorte, Inhaber, Manager und Backoffice alle.

### Von und Bis {#zeitraum}

Erster und letzter Tag des Zeitraums, beide Tage eingeschlossen, nach der Ortszeit des jeweiligen Standorts. Vorbelegt sind der Monatserste und heute. Der Zeitraum darf höchstens ein Jahr lang sein, und „Bis“ darf nicht vor „Von“ liegen.

### Buchungen anzeigen {#vorschau}

Zeigt die Buchungen am Bildschirm, ohne Datei. Die Vorschau enthält alle Kassen mit ihrer Währung, auch Schweizer Kassen in CHF.

### Dateien {#dateien}

- **DATEV-Buchungsstapel**: CSV im DATEV-Format „Buchungsstapel“ mit Kopfzeile. Enthält die Kassen in Euro. Jeder Steuersatz braucht einen BU-Schlüssel im Land der Kassa, siehe „DATEV-BU-Schlüssel je Steuersatz“ weiter unten.
- **BMD-Import**: CSV für den Buchungsimport von BMD NTCS, mit Steuersatz, Steuerbetrag und Kostenstelle. Enthält die Kassen in Euro.
- **RZL-Import**: CSV für den Buchungsimport von RZL, mit Soll/Haben-Kennzeichen. Die Spalten ordnet man in RZL beim ersten Import einmal einer Importvorlage zu. Enthält die Kassen in Euro.
- **Schweiz (Abacus, bexio)**: CSV mit Soll, Haben, Betrag, Währung und MWST. Enthält nur die Kassen in CHF. Die Spalten ordnet man in Abacus oder bexio beim ersten Import einmal zu.

DATEV, BMD und RZL sind in Windows-Zeichensatz (ANSI) geschrieben, die Schweizer Datei in UTF-8.

### Wie gebucht wird {#buchungslogik}

- Je Beleg: die Erlöse je Erlöskonto und Steuersatz vom **Verrechnungskonto Kassa** an das Erlöskonto, mit Steuersatz und Kostenstelle des Erlöskontos.
- Je Beleg und Zahlungsart: vom Geldkonto der Zahlungsart (Bar, Karte, Sofortüberweisung, digitaler Euro, Gutschein) an das Verrechnungskonto. Danach ist das Verrechnungskonto je Beleg ausgeglichen.
- Ein Storno erscheint als Gegenbuchung mit dem Text „Storno Kassa …“.
- Einlagen in die Kassenlade: Kassa (Bar) an Geldtransit. Entnahmen umgekehrt.
- Eine Differenz im Tagesabschluss: Fehlbetrag an Kassa (Bar), Überschuss von Kassa (Bar) an das Überschusskonto.
- Belegnummer ist die Kassennummer mit der Belegnummer, zum Beispiel „K1-123“.

## Erlöskonten {#erloeskonten}

Liste der Erlöskonten des Unternehmens. Drei gibt es immer: 4000 „Erlöse Behandlungen“, 4100 „Erlöse Produkte“ und 3600 „Mehrzweckgutscheine (nicht steuerbar)“. Weitere Konten trennen Geschäftsbereiche, zum Beispiel Shop oder Hotelgäste. Jeder Umsatz darauf wird ganz normal versteuert und signiert.

Anlegen dürfen Inhaber, Manager und Standortleitung. Ein angelegtes Konto lässt sich auf dieser Seite nicht mehr ändern oder löschen.

### Kontonummer {#kontonummer}

Die Nummer des Erlöskontos, wie sie in der Buchhaltung geführt wird. Pflicht, höchstens 20 Zeichen, jede Nummer nur einmal. Sie steht im Export als Habenkonto, außer sie wird unter „Erlöskonten umschlüsseln“ umgeschrieben.

### Bezeichnung {#bezeichnung}

Name des Kontos, zum Beispiel „Erlöse Shop“. Pflicht, höchstens 200 Zeichen. Er erscheint in dieser Liste und mit der Kontonummer im Bericht „Management“ unter „Umsatz nach Erlöskonto“. In den Export geht nur die Kontonummer.

### Kostenstelle {#kostenstelle}

Freiwillig, höchstens 50 Zeichen. Steht bei jeder Erlösbuchung auf dieses Konto in der Spalte Kostenstelle (DATEV: KOST1) und neben dem Umsatz im Bericht „Management“ unter „Umsatz nach Erlöskonto“.

## Kontenzuordnung {#kontenzuordnung}

Welche Konten der Export für Geld, Verrechnung und Differenzen verwendet, dazu die Angaben für DATEV und die Schweiz. Sehen dürfen alle, die die Seite öffnen können; speichern und zurücksetzen nur Inhaber und Manager, weil die Zuordnung für alle Standorte eines Landes gilt. Alle Kontonummern bestehen nur aus Ziffern, höchstens 20.

Die Vorgaben entsprechen dem deutschen Kontenrahmen SKR03. Für Österreich oder die Schweiz mit der Steuerberatung abstimmen und anpassen.

### Gilt für {#gilt-fuer}

- **Standard (alle Länder)**: gilt für alle Kassen, deren Land keine eigene Zuordnung hat.
- **Österreich**, **Deutschland**, **Schweiz**: eine eigene Zuordnung für die Kassen dieses Landes. Solange keine gespeichert ist, zeigt die Seite den Standard; erst „Speichern“ legt die eigene an.
- „Auf Standard zurücksetzen“ löscht die eigene Zuordnung eines Landes; danach gilt dort wieder der Standard.
- Jede Kassa wird mit der Zuordnung ihres Landes gebucht.

### Verrechnungskonto Kassa {#verrechnungskonto}

Zwischenkonto für jeden Beleg: Erlöse gehen von hier ab, Zahlungen hier zu. Vorgabe 1360.

### Kassa (Bar) {#kassa-bar}

Geldkonto für Barzahlungen; auch Gegenkonto bei Einlagen, Entnahmen und Kassendifferenzen. Vorgabe 1000.

### Karte (Verrechnung Terminal) {#karte}

Geldkonto für Kartenzahlungen, bis der Zahlungsanbieter auszahlt. Vorgabe 1361.

### Bank (Sofortüberweisung) {#sofortueberweisung}

Geldkonto für Zahlungen per Sofortüberweisung. Vorgabe 1200.

### Gutscheine (Verbindlichkeit) {#gutscheine}

Konto für Zahlungen mit einem Gutschein; dorthin geht auch jede Zahlungsart, die keinem anderen Konto zugeordnet ist. Vorgabe 3600.

### Geldtransit (Einlage/Entnahme) {#geldtransit}

Gegenkonto für Einlagen in die und Entnahmen aus der Kassenlade, zum Beispiel wenn Bargeld zur Bank gebracht wird. Vorgabe 1460.

### Digitaler Euro (Verrechnung) {#digitaler-euro}

Geldkonto für Zahlungen mit dem digitalen Euro, bis der Zahlungsdienstleister sie dem Bankkonto gutschreibt. Vorgabe 1362.

### Treatwell (Forderung, vorab bezahlt) {#treatwell}

Konto für Termine, die der Gast vorab über Treatwell bezahlt hat und die an der Kassa mit der Zahlungsart „Über Treatwell bezahlt“ verrechnet wurden. Es ist eine Forderung des Spas an Treatwell, bis Treatwell sie auszahlt; der Umsatz selbst läuft über das Erlöskonto der Behandlung. Vorgabe 1400.

### Kassenfehlbetrag (Tagesabschluss) {#kassenfehlbetrag}

Aufwandskonto, wenn beim Tagesabschluss weniger Bargeld gezählt wird als laut Kassa da sein sollte. Vorgabe 4900.

### Kassenüberschuss (Tagesabschluss) {#kassenueberschuss}

Ertragskonto, wenn beim Tagesabschluss mehr Bargeld gezählt wird als laut Kassa da sein sollte. Vorgabe 2700.

### DATEV-Beraternummer {#datev-beraternummer}

Nummer der Steuerberatung bei DATEV, bis zu 7 Ziffern; leer ist erlaubt. Steht in der Kopfzeile des DATEV-Buchungsstapels.

### DATEV-Mandantennummer {#datev-mandantennummer}

Nummer des Unternehmens bei der Steuerberatung, bis zu 5 Ziffern; leer ist erlaubt. Steht in der Kopfzeile des DATEV-Buchungsstapels.

### Sachkontenlänge {#sachkontenlaenge}

Wie viele Stellen die Sachkonten in DATEV haben, 4 bis 8. Vorgabe 4. Steht in der Kopfzeile des DATEV-Buchungsstapels und muss zum Kontenrahmen der Steuerberatung passen.

### Beginn Wirtschaftsjahr (Monat) {#wirtschaftsjahr}

Monat, in dem das Wirtschaftsjahr beginnt, 1 bis 12. Vorgabe 1 (Jänner). Daraus setzt die DATEV-Datei den Beginn des Wirtschaftsjahrs in die Kopfzeile.

### DATEV-BU-Schlüssel je Steuersatz {#datev-bu-schluessel}

Feld „DATEV-BU-Schlüssel je Steuersatz (z. B. 19=3;7=2)“: welchen BU-Schlüssel DATEV für welchen Steuersatz bekommt, im Format `19=3;7=2`: Steuersatz, Gleichheitszeichen, Schlüssel, mehrere Paare mit Strichpunkt getrennt. Ein Schlüssel hat höchstens 4 Zeichen. Vorgabe `19=3;7=2` (deutsche Sätze, SKR03/SKR04 ohne Automatikkonten).

- Jede Kassa bekommt den Schlüssel aus der Zuordnung ihres Landes, ohne eigene Zuordnung aus dem Standard. Eine österreichische Kassa mit 20 % braucht also einen Eintrag `20=…` in der Zuordnung für Österreich (oder im Standard), auch wenn die Datei für eine deutsche Steuerberatung ist. Die Schlüssel nennt die Steuerberatung; AISPA erfindet keine.
- `20=` ohne Schlüssel heißt: das Erlöskonto ist ein Automatikkonto, DATEV rechnet die Steuer selbst; das Feld BU-Schlüssel bleibt leer.
- Fehlt für einen Steuersatz einer Euro-Kassa der Eintrag ganz, gibt es keine DATEV-Datei. Stattdessen sagt AISPA, welche Sätze in welchem Land fehlen, auch schon unter „Buchungen anzeigen“. So landet kein Erlös ohne Umsatzsteuer in DATEV.
- Für Österreich passen meist BMD oder RZL: Diese Dateien geben den Steuersatz selbst mit und brauchen keinen Schlüssel.

Kopfdaten der DATEV-Datei (Beraternummer, Mandantennummer, Sachkontenlänge, Wirtschaftsjahr) kommen aus der Zuordnung für Deutschland, ohne eigene Zuordnung aus dem Standard.

### MWST-Codes Schweiz je Satz {#mwst-codes}

Feld „MWST-Codes Schweiz je Satz (z. B. 8.1=UN81)“: welchen MWST-Code Abacus oder bexio für welchen Satz bekommt, im Format `8.1=UN81;2.6=UR26`. Ein Code hat höchstens 10 Zeichen. Vorgabe leer.

- Ohne Eintrag bleibt die Spalte MWST-Code leer; Satz und MWST-Betrag stehen trotzdem in der Datei.
- Gilt für die Schweizer Datei aus der Zuordnung für die Schweiz, ohne eigene Zuordnung aus dem Standard.

### Erlöskonten umschlüsseln {#umschluesseln}

Feld „Erlöskonten umschlüsseln (z. B. 4000=3400)“: schreibt im Export ein Erlöskonto auf ein anderes Konto um, im Format `4000=3400;4800=3200`, also Erlöskonto im Programm, Gleichheitszeichen, Konto in der Buchhaltung. Beide Seiten nur Ziffern. Vorgabe leer.

- Gedacht für einen anderen Kontenrahmen in einem Land, zum Beispiel die Schweizer Kassen mit eigenen Erlöskonten.
- Nicht genannte Erlöskonten gehen mit ihrer eigenen Nummer hinaus. Kostenstelle und die Trennung der Bereiche bleiben gleich.
