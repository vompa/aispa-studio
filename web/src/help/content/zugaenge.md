---
art: einstellung
bereich: Zugänge
oberflaeche: verwaltung
rollen: inhaber, manager, leitung
seite: verwaltung
---
# Zugänge

Der Zugang legt fest, wer sich anmelden darf, mit welcher Rolle und für welche Standorte. In der Windows-App ist er der Reiter **Zugang und Rolle** der Personenkarte unter „Verwaltung“ › „Team“; einen eigenen Eintrag „Zugänge“ gibt es dort nicht mehr. Sehen dürfen ihn Inhaber, Manager und Standortleitung.

![Einladen in der Windows-App](images/app/verwaltung-team-einladen.png)

- Inhaber verwalten alle Zugänge. Nur sie legen Inhaber- und Manager-Zugänge an, ändern und entfernen sie.
- Ein Manager verwaltet alle übrigen Zugänge. Inhaber- und Manager-Zugänge sieht er nicht, und die Rollen „Inhaber“ und „Manager“ kann er nicht vergeben.
- Eine Standortleitung sieht und verwaltet nur Zugänge für ihre eigenen Standorte, ebenfalls ohne Inhaber und Manager.
- Den eigenen Zugang kann niemand selbst ändern, damit sich niemand versehentlich aussperrt. Das macht eine andere Person mit Verwaltungsrecht.

Die Liste zeigt alle Personen mit Zugang, mit Rolle und Zusätzen wie „gesperrt“, „abgelaufen“ oder „endet bald“. Ein Klick öffnet den Zugang zum Bearbeiten. In der Windows-App sind das die Knöpfe **Person einladen** und im Einzelplatz **Zugang anlegen** der Teamliste; mit **Speichern** schließt die Karte ab.

## Rollen und was sie dürfen {#rollen}

Jeder Zugang hat genau eine Rolle. Was eine Rolle darf, ist fest vorgegeben; einzeln vergeben wird nur das Recht „Berichte mit Beträgen“ (siehe unten). Unter dem Feld „Rolle“ steht jeweils eine Kurzbeschreibung.

### Inhaber {#rolle-inhaber}

Darf alles an allen Standorten und bleibt Eigentümer des Kontos. Nur Inhaber dürfen:

- Inhaber- und Manager-Zugänge anlegen, ändern und entfernen, also andere Personen zu Inhabern oder Managern machen,
- Lizenz und Abrechnung kündigen.

Standorte anlegen sowie Firmendaten, Kassa-Einstellungen, Zahlungen, Erinnerungen, Postfach & Nummern, die Kontenzuordnung für die Buchhaltung und den Bereich „Lohn“ bearbeiten auch Manager.

Inhaber-Zugänge lassen sich nicht befristen.

### Manager {#rolle-manager}

Die Geschäftsleitung neben dem Inhaber: darf alles, was ein Inhaber darf, an allen Standorten, mit den beiden Ausnahmen oben. Ein Manager kann also keinen Inhaber und keinen weiteren Manager ernennen, ändern oder entfernen und weder Lizenz noch Abrechnung kündigen. Manager-Zugänge dürfen befristet sein. Für die PIN gelten wie beim Inhaber 6 bis 8 Ziffern.

### Standortleitung {#rolle-standortleitung}

Für die eigenen Standorte:

- Kalender, Team, Behandlungen und Kunden ansehen; Termine buchen, verschieben und stornieren, Sperrzeiten setzen, Kunden anlegen; Status von Terminen setzen.
- Öffnungszeiten, Räume und Geräte pflegen; Team, Behandlungen und Qualifikationen pflegen.
- Personen einladen, Rollen (außer Inhaber) und Standorte zuweisen, Zugänge sperren, Empfangsgeräte anmelden.
- Im Management-Cockpit und in den Pflichtberichten die Kennzahlen ohne Beträge sehen (Auslastung, Anzahlen, Quoten, Arbeitszeitaufzeichnung). Umsätze, Kosten, Margen und den Export für die Buchhaltung nur mit dem Recht „Berichte mit Beträgen“.
- Kassenaufsicht: jeden Beleg stornieren und Bargeld aus der Lade entnehmen. Über der Rabattgrenze Rabatt geben bzw. mit der PIN freigeben.
- Anamnese lesen: die Antworten und fotografierte Bögen ansehen, prüfen und übernehmen.

### Rezeption {#rolle-rezeption}

Für die eigenen Standorte: Kalender, Team, Behandlungen und Kunden ansehen; Termine buchen, verschieben und stornieren, Sperrzeiten setzen, Kunden anlegen; Gäste einchecken und den Status von Terminen setzen. Die Rezeption arbeitet auch mit dem Posteingang der Terminannahme und darf kassieren und den Tagesabschluss machen. Stammdaten, Zugänge und Berichte mit Beträgen sieht sie nicht, es sei denn, ihr ist das Recht „Berichte mit Beträgen“ einzeln vergeben. Sie liest die Anamnese und lädt und prüft Fotos von Papierbögen.

### Backoffice {#rolle-backoffice}

Für Buchhaltung und Lohnverrechnung, für alle Standorte. Das Backoffice darf lesen und exportieren, aber nichts buchen und nichts ändern:

- alle Berichte mit Beträgen (Umsatz je Behandlung, Person und Standort, Kosten und Margen), den Export für die Buchhaltung, Gutschein- und Paketbestände sowie DEP, DSFinV-K und TSE-Export,
- Belege und Tagesabschlüsse der Kassen,
- Dienstplan, Zeiten (Arbeitszeiten, Abwesenheiten, Urlaub), die Stammdaten des Teams ohne Gesundheits- und Zugangsdaten sowie den Export für die Lohnverrechnung.

Nicht möglich sind Kassieren, Tagesabschluss, Buchen, Kalender und Kundendaten (außer dem Namen auf dem Beleg), Einstellungen und das Ändern von Dienstplan oder Zeiten.

### Therapeut:in {#rolle-therapeutin}

Sieht den Kalender der eigenen Standorte und setzt den Status der eigenen Termine (z. B. abgeschlossen, nicht erschienen). Liest die Anamnese und prüft fotografierte Bögen. Bucht nicht und kassiert nicht. Ein Zugang mit dieser Rolle braucht immer eine „Person im Team“, damit das Programm die eigenen Termine erkennt. Der Assistent beantwortet ihr Auswertungen ohne Beträge (Top-Behandlungen, Auslastung, Termine je Person, Nicht-erschienen, Top-Gäste nach Besuchen); das lässt sich je Zugang abschalten, siehe [Auswertungen ohne Beträge](hilfe:zugaenge#auswertungen-ohne-betraege).


## Berichte mit Beträgen {#berichte-mit-betraegen}

Auswertungen mit Geldbeträgen über den Betrieb sieht nicht jede Person, die Berichte öffnen darf. „Berichte mit Beträgen“ ist ein eigenes Recht:

- **Immer** haben es Inhaber, Manager und Backoffice.
- **Nur wenn es einzeln vergeben ist** haben es Standortleitung und Rezeption. Das Recht vergeben und entziehen nur Inhaber und Manager, beim Einladen, Anlegen oder Bearbeiten eines Zugangs mit dem Häkchen „Berichte mit Beträgen“. Es gilt sofort.
- Alle anderen Rollen haben es nie.

Was dazugehört: Umsätze und Umsatz je Behandlung, Person, Konto und Zahlart, Durchschnittsbon, Kosten und Margen (Seite „Kosten“), Gutscheinsummen, Zahlen der Tagesabschlüsse, Top-Kunden nach Ausgaben auf der Kundenkarte, die Buchhaltung samt Export sowie DEP, DSFinV-K und TSE-Export.

Ohne das Recht bleiben die Berichte ohne Beträge: Auslastung in Prozent, Anzahl der Behandlungen, No-Show- und Storno-Quote, Gäste und Stammgäste, Arbeitszeitaufzeichnung. In „Berichte“ fehlen dann die Beträge, dort steht „Beträge nur mit Berechtigung“. Der Server prüft das bei jeder Abfrage; die Oberfläche blendet nur aus.

Kosten ändern darf zusätzlich nur, wer Behandlungen und Team pflegen darf (Inhaber, Manager, Standortleitung). Rezeption mit dem Recht liest die Kosten nur.

## Auswertungen ohne Beträge {#auswertungen-ohne-betraege}

Die Rolle Therapeut:in sieht den Kalender und bekommt deshalb im Assistenten auch die **Auswertungen ohne Beträge**: Top-Behandlungen nach Anzahl, Auslastung in Prozent, Termine je Person, Nicht-erschienen und Top-Gäste nach Besuchen. Beträge sieht sie nie. Wer das für einen einzelnen Zugang nicht möchte, nimmt das Häkchen **Auswertungen ohne Beträge im Assistenten** weg:

- Das Häkchen gibt es **nur bei der Rolle Therapeut:in**, beim Einladen, Anlegen und Bearbeiten eines Zugangs. Es ist standardmäßig **an**; bei allen anderen Rollen ändert sich dadurch nichts (Rezeption, Standortleitung, Manager, Inhaber und Backoffice haben ihre Rechte wie bisher).
- Setzen dürfen es **Inhaber, Manager und Standortleitung** (die Standortleitung für Zugänge an ihren Standorten). Anders als bei den Berichten mit Beträgen wird dabei nichts freigegeben, deshalb braucht es keine Inhaber-Rolle.
- Ist es abgeschaltet, antwortet der Assistent auf solche Fragen mit „Dafür fehlt die Berechtigung.“ Der Server prüft das bei jeder Frage; die Änderung gilt sofort.

## Person einladen {#person-einladen}

Im normalen Betrieb meldet sich jede Person mit einem Passkey an (Fingerabdruck, Gesichtserkennung oder Geräte-PIN), nicht mit Passwort. Neue Personen kommen über einen Einladungslink dazu.

1. Name, E-Mail, Rolle, Person im Team, Befristung und Standorte ausfüllen.
2. „Einladungslink erstellen“ klicken.
3. Den angezeigten Link der Person schicken, z. B. per Mail oder Messenger. „Einladungslink kopieren“ legt ihn in die Zwischenablage und meldet „Link kopiert.“; ohne Zwischenablage markiert die Seite den Link im Feld, dann mit Strg+C kopieren.
4. Die Person öffnet den Link, bestätigt und legt dabei ihren Passkey an. Ab dann gilt der Zugang.

In der Windows-App schickt **Einladung senden** die Einladung als Mail mit dem Link an die Adresse der Person. Es erscheint „Einladung an … geschickt.“ **Einladungslink kopieren** kopiert den Link für die Weitergabe auf anderem Weg („Link kopiert.“). Fehlen Name oder E-Mail, steht dort „Name und E-Mail fehlen.“ Die Mail trägt den Betreff „Einladung zu“ und den Namen des Spas. Nach dem Senden leeren sich die Felder, damit dieselbe Einladung nicht doppelt rausgeht. Die Person nimmt die Einladung auf der Seite „Anmelden“ im Browser an: [Seite „Anmelden“ im Browser](hilfe:anmelden-web).

Der Link ist 7 Tage gültig und nur einmal verwendbar. Er wird nur dieses eine Mal angezeigt. Eine neue Einladung an dieselbe Adresse ersetzt eine noch offene.

### Name {#einladen-name}

Der Name, unter dem die Person im Programm erscheint, z. B. in der Liste der Zugänge und beim Wechsel am Empfangsgerät. Pflichtfeld.

### E-Mail {#einladen-email}

Die Mailadresse der Person. Pflichtfeld. Groß- und Kleinschreibung spielen keine Rolle. Gibt es für diese Adresse schon ein Konto, ist keine Einladung möglich.

## Zugang anlegen (Einzelplatz) {#zugang-anlegen}

Im Einzelplatz-Betrieb (eine Installation mit lokaler Datenbank) gibt es keine Einladungen. Zugänge werden direkt mit Benutzername und Startpasswort angelegt. Die Person meldet sich damit an und muss dabei ein eigenes Passwort wählen. Nach fünf falschen Passwörtern ist die Anmeldung fünf Minuten gesperrt.

### Name {#anlegen-name}

Der Name, unter dem die Person im Programm erscheint. Pflichtfeld.

### Benutzername {#anlegen-benutzername}

Der Name für die Anmeldung, z. B. „anna“. Pflichtfeld, muss eindeutig sein. Erlaubt sind Buchstaben (auch Umlaute und ß), Ziffern und die Zeichen - . _ @ +, keine Leerzeichen.

### Startpasswort (mindestens 8 Zeichen) {#startpasswort}

Ein vorläufiges Passwort mit mindestens 8 Zeichen; sonst gibt es keine Vorgaben. Es ist im Feld lesbar, damit man es der Person weitergeben kann. Bei der ersten Anmeldung muss die Person es durch ein eigenes ersetzen. Hier nie ein Passwort verwenden, das woanders gilt.

## Rolle, Person im Team und Befristung {#zugang-felder}

Diese Felder gibt es beim Einladen, beim Anlegen und beim Bearbeiten eines Zugangs.

### Rolle {#rolle}

Die Rolle bestimmt, was die Person darf (siehe „Rollen und was sie dürfen“). Standard beim Einladen ist „Rezeption“. „Inhaber“ und „Manager“ kann nur der Inhaber auswählen. Bei Standortleitung und Rezeption erscheint das Häkchen „Berichte mit Beträgen“ (siehe [Berichte mit Beträgen](hilfe:zugaenge#berichte-mit-betraegen)). Eine Änderung gilt sofort. Die Windows-App lässt auch eine Rolle wählen, die man nicht vergeben darf; die Server-Prüfung lehnt dann beim Speichern ab, und die Meldung nennt den Grund über der Leiste. Daneben steht der Hinweis „Vergeben kann das nur der Inhaber.“

### Person im Team {#person-im-team}

Verknüpft den Zugang mit einer Person aus „Verwaltung“ › „Team“. Zur Auswahl stehen Personen, die noch keinen eigenen Zugang haben. Eine Person, die im Team nicht mehr aktiv ist, lässt sich nicht verknüpfen.

- Für Therapeut:innen Pflicht: Nur so erkennt das Programm ihre eigenen Termine.
- Für andere Rollen freiwillig („Keine (z. B. nur Rezeption)“). Sinnvoll, wenn die Person auch behandelt.

### Befristet bis einschließlich {#befristung}

Der letzte Tag, an dem der Zugang gilt, z. B. für Aushilfen oder Praktika. Leer heißt unbefristet (Standard). Das Feld fehlt bei der Rolle „Inhaber“, weil Inhaber-Zugänge nie befristet sind; Manager-Zugänge dürfen befristet sein.

- Der Tag muss in der Zukunft liegen und höchstens zwei Jahre entfernt sein.
- Ab Mitternacht nach dem letzten Tag (Zeitzone des Standorts) wirkt der Zugang wie gesperrt. In der Liste steht dann „abgelaufen“.
- Sieben Tage vor dem Ende steht in der Liste „endet bald“. Die Person und alle, die ihren Zugang verwalten dürfen, bekommen dann einmal eine Mail, sofern eine Mailadresse hinterlegt ist.
- Beim Bearbeiten verlängert „Um 30 Tage verlängern“ das Ende um 30 Tage, bei einem schon abgelaufenen Zugang ab heute. Danach „Speichern“ klicken.

## Standorte {#standorte}

Für welche Standorte der Zugang gilt. Mindestens ein Standort ist Pflicht. Die Person sieht und bearbeitet nur Termine, Kassen und Daten dieser Standorte. Gibt es nur einen Standort, ist er beim Einladen schon angehakt.

- **Alle Standorte, auch künftige**: gilt für alle bestehenden und alle später angelegten Standorte. Diese Möglichkeit erscheint nur, wenn man selbst Zugang zu allen Standorten hat.
- Sonst die einzelnen Standorte anhaken. Manager und Standortleitung können nur ihre eigenen Standorte vergeben.
- Das Backoffice gilt immer für alle Standorte; die Auswahl entfällt, weil Buchhaltung und Lohn fürs ganze Unternehmen gelten.

## Zugang bearbeiten {#zugang-bearbeiten}

Ein Klick auf eine Person in der Liste öffnet ihren Zugang. Unter dem Namen steht die Mailadresse und die Zahl der Passkeys, im Einzelplatz-Betrieb der Benutzername und gegebenenfalls „Startpasswort noch nicht geändert“.

Rolle, Person im Team, Befristung und Standorte ändert man wie beim Einladen. „Speichern“ übernimmt die Änderungen, sie gelten sofort.

### Zugang aktiv {#zugang-aktiv}

Ausschalten sperrt den Zugang sofort, auch auf Geräten, auf denen die Person gerade angemeldet ist. Die Person bleibt in der Liste mit „gesperrt“ und kann jederzeit wieder freigeschaltet werden. Danach „Speichern“ klicken.

### Neues Startpasswort {#neues-startpasswort}

Nur im Einzelplatz-Betrieb, z. B. wenn jemand sein Passwort vergessen hat. Ein neues Startpasswort mit mindestens 8 Zeichen eingeben und „Passwort zurücksetzen“ klicken (in der Windows-App **Passwort zurücksetzen**).

- Das alte Passwort gilt sofort nicht mehr, laufende Anmeldungen der Person enden, und eine Sperre nach Fehlversuchen wird aufgehoben.
- Bei der nächsten Anmeldung muss die Person wieder ein eigenes Passwort wählen.
- Das eigene Passwort ändert man über „Passwort ändern“, nicht hier.

## Verlauf {#verlauf}

Inhaber und Manager sehen am Ende des Zugangs den Abschnitt **Verlauf**, in der Windows-App den Reiter **Verlauf** der Karte: wer Rolle, Standorte, Sperre oder Befristung wann geändert hat. Siehe [Verlauf eines Zugangs](hilfe:zugaenge-verlauf).

## Offene Einladungen {#offene-einladungen}

Einladungen, die noch nicht angenommen, nicht zurückgezogen und nicht abgelaufen sind, mit Name, Mailadresse, Rolle und Ablaufdatum. Die Liste erscheint nur, wenn es solche Einladungen gibt. Eine Standortleitung sieht nur Einladungen für ihre Standorte.

„Zurückziehen“ macht den Link sofort ungültig. In der Windows-App heißt der Knopf auf der Karte der eingeladenen Person **Einladung zurückziehen**; die Rückfrage „Einladung zurückziehen?“ bestätigt **Zurückziehen**. Wer ihn danach öffnet, kann sich damit nicht mehr anmelden.

## Empfangsgerät {#empfangsgeraet}

Am gemeinsamen Gerät der Rezeption wechselt das Team mit Name und PIN statt mit Passkey. Dazu meldet eine Person mit Verwaltungsrecht (Inhaber, Manager oder Standortleitung) die Windows-App auf diesem PC einmal als Empfangsgerät eines Standorts an, unter **Geräte**. Name des Geräts, Standort und Leerlaufzeit stehen dort bei [Empfangsgerät](hilfe:geraete#empfangsgeraet).

- Am Empfangsgerät erscheinen alle Personen mit einem aktiven Zugang für diesen Standort, die eine PIN festgelegt haben. Man wählt den eigenen Namen und tippt die PIN.
- Für die angemeldete Person gelten Rolle, Standorte, Sperre und Befristung wie sonst auch.
- Die Anmeldung per PIN endet nach der eingestellten Zeit ohne Eingabe. Die App warnt vorher.
- Einen Browser als Empfangsgerät gibt es nicht.

### PIN {#pin}

Die PIN legt jede Person selbst fest, in der Windows-App im Menü hinter dem eigenen Kürzel in der linken Leiste. Niemand sonst kennt oder vergibt sie.

- 4 bis 8 Ziffern, für Inhaber und Manager 6 bis 8 Ziffern. Nicht nur dieselbe Ziffer (z. B. nicht „1111“).
- Nach fünf falschen Eingaben ist die PIN 15 Minuten gesperrt. In dieser Zeit meldet sich die Person mit ihrem Passkey an.
- Dieselbe PIN dient der Standortleitung auch zur Freigabe von Rabatten über der Rabattgrenze an der Kassa.
