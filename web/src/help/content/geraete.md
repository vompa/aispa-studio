---
art: einstellung
bereich: Geräte
oberflaeche: windows-app
rollen: rezeption, leitung, inhaber, manager
seite: geraete
---
# Geräte

Die Seite „Geräte“ in der Windows-App gilt nur für den PC, an dem Sie gerade sitzen: Bondrucker, Kartenterminal und ob dieser PC ein Empfangsgerät ist. Dazu koppelt die Leitung hier die Anzeigen im Pausenraum. Die Seite sehen alle, die Termine buchen dürfen (Inhaber, Standortleitung, Rezeption).

## Bondrucker {#bondrucker}

Ein Bondrucker im Netzwerk, der ESC/POS versteht (übliche Geräte von Epson, Star oder Bixolon). Die App schickt die Daten direkt über Port 9100 an den Drucker; einen Windows-Druckertreiber braucht es nicht. Den Beleg samt QR-Code und Papierschnitt erzeugt der Server, die App reicht ihn nur weiter.

Die Einstellungen gelten nur an diesem PC und werden erst mit „Speichern“ übernommen. Ändern darf sie jede Person, die die Seite sieht.

### Adresse {#drucker-adresse}

Die IP-Adresse oder der Name des Druckers im Netz, z. B. `192.168.1.60`. Ohne Port verwendet die App den Port 9100; ein anderer Port wird mit Doppelpunkt angehängt, z. B. `192.168.1.60:9100`.

- Leer (Standard) bedeutet: kein Bondrucker an diesem PC. Dann druckt die Kassa nichts; beim Beleg steht „Kein Bondrucker eingerichtet (unter „Geräte“).“ und „Drucken“ ist ausgegraut.
- Die Adresse steht meist auf dem Selbsttest-Ausdruck des Druckers. Damit sie sich nicht ändert, sollte der Drucker im Router eine feste Adresse bekommen.
- Antwortet der Drucker nicht innerhalb von 10 Sekunden, meldet die App „Bondrucker unter … nicht erreichbar.“

### Zeichen pro Zeile {#zeichen-pro-zeile}

Wie breit der Beleg gesetzt wird. Zur Wahl stehen 42, 48 und 32 Zeichen; Standard sind 42 Zeichen.

- 80 mm Papier: 42 oder 48 Zeichen, je nach Drucker und Schrift. Bricht der Beleg mitten in der Zeile um, 42 wählen; bleibt rechts viel Platz, 48.
- 58 mm Papier: 32 Zeichen.

Die Einstellung wirkt auf jeden Beleg, den dieser PC druckt. Für den Probedruck spielt sie keine Rolle.

### Nach dem Kassieren und Stornieren automatisch drucken {#automatisch-drucken}

Eingeschaltet (Standard) druckt dieser PC den Beleg gleich nach dem Kassieren und nach einem Storno, ohne weiteren Klick. Ausgeschaltet druckt er nur, wenn jemand den Beleg in der Kassa ausdrücklich druckt.

Ohne Adresse eines Bondruckers hat der Haken keine Wirkung.

### Probedruck {#probedruck}

Schickt sofort einen kurzen Zettel mit „AISPA Studio“, „Probedruck“, Datum und Uhrzeit an die eingetragene Adresse und schneidet das Papier ab. Der Probedruck geht direkt an den Drucker, ohne Beleg und ohne Server; er braucht kein vorheriges „Speichern“.

Daneben steht das Ergebnis: „Probedruck geschickt.“ oder die Fehlermeldung. Ist keine Adresse eingetragen, erscheint „Erst die Adresse des Druckers eintragen.“

## Kartenterminal {#kartenterminal}

Ein Kartenterminal im Netz, das AISPA Studio selbst ansteuert: Die Kassa schickt den Betrag, das Terminal fragt die Karte ab, und das Ergebnis kommt automatisch zurück. Unterstützt werden die Protokolle ZVT (Deutschland, Österreich) und OPI (z. B. Worldline, Schweiz).

Wichtig: Ein Terminal darf nur an **einem** PC gewählt sein, an dem PC, neben dem es steht. Nur dieser PC schickt Zahlungen, Stornos und Gutschriften an das Gerät; Kassen auf anderen PCs geben ihre Kartenzahlungen über den Server an ihn weiter. Die App muss an diesem PC dafür offen sein.

### Terminal an diesem PC {#terminal-auswahl}

Die Auswahlliste zeigt alle Terminals im Netz der Standorte, die Sie sehen dürfen, jeweils mit Name, Standort, Protokoll und Adresse, z. B. „Empfang · Gmunden · ZVT 192.168.1.50:20007“.

- „Keines (Karte am eigenständigen Gerät oder anderer PC)“ ist der Standard. Dann steuert dieser PC kein Terminal an; Kartenzahlungen tippt man am eigenständigen Gerät ein oder sie laufen über das Terminal eines anderen PCs.
- Die Wahl gilt erst nach „Speichern“. Danach verbindet sich dieser PC mit dem Terminal und fragt alle zwei Sekunden beim Server nach, ob eine Zahlung, ein Storno oder eine Gutschrift wartet.
- Wählen und speichern darf jede Person, die die Seite sieht.

Unter der Auswahl steht, was das Terminal zuletzt gemeldet hat, z. B. „Wartet auf Zahlungen.“ Ein gelber Hinweis erscheint, wenn die Verbindung zum Terminal mitten in einer Zahlung abgebrochen ist. Dann ist unklar, ob die Karte belastet wurde: am Terminal oder auf dem Terminal-Beleg nachsehen und in der Kassa entsprechend weiterarbeiten. Die App meldet so einen Fall nicht von selbst als bezahlt.

### Neues Terminal im Netz {#neues-terminal}

Nur für Personen, die den Standort verwalten dürfen (Inhaber, Standortleitung). Die Felder legen ein Terminal für das ganze Spa an:

- **Name**: frei wählbar, 1 bis 100 Zeichen, Vorschlag „Empfang“. Der Name erscheint in der Auswahl und in der Kassa.
- **Standort**: zu welchem Standort das Terminal gehört. Die Kassen dieses Standorts können damit kassieren. Der Standort bestimmt auch die Währung, die an das Terminal geht (CHF in der Schweiz, sonst EUR).
- **Protokoll**: „ZVT (Deutschland, Österreich; meist Port 20007)“ (Standard) oder „OPI (z. B. Worldline, Schweiz; meist Port 20002)“. Welches Protokoll das Terminal spricht, sagt der Zahlungsdienstleister bei der Einrichtung.
- **Adresse**: IP-Adresse und Port des Terminals, z. B. `192.168.1.50:20007`. Ohne Port nimmt die App 20007 bei ZVT und 20002 bei OPI.

„Terminal anlegen“ ist erst möglich, wenn Name, Standort und Adresse ausgefüllt sind. Das neue Terminal wird gleich in der Auswahl oben gewählt; angesteuert wird es aber erst nach „Speichern“.

### Gewähltes Terminal entfernen {#terminal-entfernen}

Nur für Inhaber und Standortleitung. Legt das oben gewählte Terminal still: Es steht danach an keinem PC und in keiner Kassa mehr zur Wahl. Dieser PC stellt auf „Keines“ um und speichert das gleich. Bisherige Zahlungen und Belege bleiben unverändert.

## Anzeigen im Pausenraum {#anzeigen}

Ein Bildschirm im Pausenraum zeigt den Tag des Standorts: wer wann Termine hat, welche Räume frei sind und die nächsten Termine. Statt der Gastnamen stehen dort nur Initialen, Gesundheitsdaten nie. Die Anzeige liest nur; sie kann keinen Termin anlegen, verschieben oder absagen.

Den Abschnitt sehen nur Inhaber, Manager und Standortleitung (Recht „Standort verwalten“). Jede gekoppelte Anzeige steht mit ihrem Namen in der Liste.

### Code vom Bildschirm {#anzeige-code}

Die Anzeige zeigt nach dem Öffnen einen Code aus acht Zeichen, z. B. „K7QM-2XRT“. Groß- und Kleinschreibung und der Bindestrich sind egal. Der Code gilt 10 Minuten.

### Name der Anzeige {#anzeige-name}

Ein Name, an dem man die Anzeige in der Liste erkennt, z. B. „Pausenraum“ (Vorschlag). 1 bis 100 Zeichen.

### Standort {#anzeige-standort}

Der Standort, dessen Tag die Anzeige zeigt. Vorausgewählt ist der Standort dieses PCs, wenn er ein Empfangsgerät ist, sonst der erste der Liste.

### Anzeige koppeln {#anzeige-koppeln}

Koppelt die Anzeige mit dem Code an den Standort. Wenige Sekunden später zeigt sie den Tag. Steht „Diesen Code kennt keine Anzeige, oder er ist abgelaufen.“, den Code noch einmal vom Bildschirm abtippen.

### Widerrufen {#anzeige-widerrufen}

Nimmt einer Anzeige den Zugang, nach einer Rückfrage. Die Anzeige meldet sich bei ihrer nächsten Anfrage ab und zeigt wieder einen Code. Das ist der Weg, wenn ein Bildschirm ausgetauscht wird oder verloren geht.

## Speichern {#speichern}

Übernimmt Bondrucker, „Zeichen pro Zeile“, den Haken für automatisches Drucken und die Wahl des Terminals für diesen PC. Gespeichert wird in den Einstellungen dieses Windows-Kontos auf diesem PC (`einstellungen.json` im Ordner `%LOCALAPPDATA%\AispaStudio`), nicht bei der Person. Wer sich an diesem PC anmeldet, druckt also auf denselben Drucker. Danach steht „Gespeichert für diesen PC.“

Das Empfangsgerät weiter unten braucht kein „Speichern“; es hat einen eigenen Knopf.

## Empfangsgerät {#empfangsgeraet}

Am Empfangsgerät wechselt das Team mit Name und PIN statt mit einem neuen Anmeldecode. So bleibt jede Buchung und jeder Beleg der richtigen Person zugeordnet, ohne dass sich alle jedes Mal neu anmelden.

- Die PIN gilt nur auf angemeldeten Empfangsgeräten und nur für Personen, deren Zugang den Standort des Geräts umfasst.
- Jede Person legt ihre eigene PIN nach der Anmeldung über ihr Profilbild in der Navigation fest (4 bis 8 Ziffern, Inhaber mindestens 6). Nach fünf falschen PINs ist die PIN 15 Minuten gesperrt.
- Am Gerät wählt man bei „Wer meldet sich an?“ seinen Namen und tippt die PIN ein. Wer noch keine PIN hat, meldet sich über „Anderes Konto“ wie gewohnt an.
- Die Anmeldung per PIN merkt sich die App nicht; nach einem Neustart oder nach der Leerlaufzeit wählt man wieder seinen Namen.

Anmelden und abmelden darf das Gerät nur, wer Zugänge verwalten darf (Inhaber, Standortleitung). Ist dieser PC schon Empfangsgerät, sehen alle anderen nur den Satz „Dieser PC ist Empfangsgerät … in … Nach … Minuten ohne Eingabe meldet die App ab.“

### Name des Geräts {#geraetename}

Ein Name, an dem man das Gerät erkennt, z. B. „Empfang“ (Vorschlag) oder „Kassa 2“. 1 bis 100 Zeichen. Der Name erscheint auf der Anmeldeseite bei „Wer meldet sich an?“.

### Standort {#geraet-standort}

Der Standort, an dem dieser PC steht. Am Gerät erscheinen nur Personen, deren Zugang diesen Standort umfasst und die eine PIN haben. Vorausgewählt ist der erste Standort der Liste.

### Abmelden ohne Eingabe nach {#leerlauf}

Wie lange die App ohne Maus- oder Tastatureingabe angemeldet bleibt. Zur Wahl stehen 2, 5, 10, 15, 30 und 60 Minuten; Standard sind 5 Minuten. 30 Sekunden vorher warnt die App und bietet „Weiter arbeiten“ an. Danach meldet sie ab und zeigt wieder die Auswahl der Namen. Kürzer ist sicherer, wenn der Empfang oft unbesetzt ist.

### Diesen PC als Empfangsgerät anmelden {#geraet-anmelden}

Meldet diesen PC mit Name, Standort und Leerlaufzeit als Empfangsgerät an. Das Kennzeichen des Geräts liegt verschlüsselt auf diesem PC und gilt ein Jahr. Um Name, Standort oder Zeit zu ändern, das Gerät abmelden und neu anmelden.

„Kein Empfangsgerät mehr“ meldet den PC wieder ab. Danach gilt die PIN hier nicht mehr, und jede Person meldet sich wieder mit Code (Server-Betrieb) oder Passwort (Einzelplatz) an.
