---
art: einstellung
bereich: Einzelplatz
oberflaeche: windows-app
rollen: inhaber, manager, leitung
seite: im-netz
---
# Im Netz

Mit „Im Netz“ arbeiten mehrere PCs im Spa mit derselben Datenbank, ohne dass es einen Server braucht. Ein PC im Einzelplatz-Betrieb ist der Haupt-PC: Auf ihm liegt die Datenbank. Weitere PCs, z. B. in den Behandlungsräumen oder an einer zweiten Kassa, verbinden sich mit ihm über das Netzwerk des Spas.

- Die Verbindung ist verschlüsselt (HTTPS). Die Datenbank selbst bleibt nur auf dem Haupt-PC erreichbar; die anderen PCs sprechen nur mit AISPA Studio auf dem Haupt-PC.
- Der Haupt-PC muss an sein und die App offen haben. Ist er aus, zeigen die anderen PCs ihren zuletzt geladenen Stand und können Termine vormerken, bis er wieder erreichbar ist.
- Rechte, Kalender und Kassa gelten an allen PCs gleich, weil alle dieselbe Datenbank benutzen.

## Freigabe {#freigabe}

Die Seite „Im Netz“ gibt es nur am Haupt-PC (Einzelplatz-Betrieb). Den Stand sehen alle, die angemeldet sind; freigeben und zurücknehmen darf nur, wer den Standort verwalten darf (Inhaber, Standortleitung). Standard: nicht freigegeben, dann läuft AISPA Studio nur auf diesem PC.

### Im Netz freigeben {#freigeben}

Schaltet die Freigabe ein. Beim ersten Mal erstellt der PC ein eigenes Zertifikat (zehn Jahre gültig) und wählt einen Port, bevorzugt 55443; ist dieser belegt, einen freien. Danach startet AISPA Studio auf diesem PC kurz neu; die Datenbank läuft dabei weiter.

Fragt Windows danach nach der Firewall, **private Netzwerke** erlauben. Ohne diese Erlaubnis erreichen die anderen PCs den Haupt-PC nicht. Erscheint „Gespeichert. Gilt ab dem nächsten Start der App.“, gilt die Freigabe erst nach einem Neustart der App.

### Adresse (eine davon) {#adresse}

Unter diesen Adressen erreichen die anderen PCs den Haupt-PC: der Name des PCs und jede IPv4-Adresse im Netz, jeweils mit Port, z. B. `https://empfang-pc:55443/` oder `https://192.168.1.20:55443/`. Am anderen PC reicht eine davon. Der PC-Name ist bequemer; klappt er nicht, eine IP-Adresse nehmen. Damit sich die IP-Adresse nicht ändert, sollte der Haupt-PC im Router eine feste Adresse bekommen.

### Verbindungscode {#verbindungscode}

20 Zeichen (Ziffern und A bis F) in Vierergruppen, z. B. `3F9A-12C4-77B0-E1D2-9A0B`. Er ist der Fingerabdruck des Zertifikats dieses PCs. Die anderen PCs nehmen nur ein Zertifikat mit genau diesem Code an; so kann sich kein anderes Gerät im Netz als Haupt-PC ausgeben.

Der Code bleibt gleich, solange das Zertifikat bleibt, auch nach dem Zurücknehmen und erneuten Freigeben. Nach dem Wiederherstellen aus einer Sicherung fehlt das Zertifikat: erneutes Freigeben erzeugt ein neues mit neuem Code, und die anderen PCs müssen neu verbinden.

Der Browser eines anderen PCs warnt einmal vor dem eigenen Zertifikat, weil er den Code nicht kennt.

### Freigabe zurücknehmen {#zuruecknehmen}

Schaltet die Freigabe ab und startet AISPA Studio auf diesem PC kurz neu. Die anderen PCs sind danach getrennt. Zertifikat, Port und Verbindungscode bleiben gespeichert, damit beim erneuten Freigeben an den anderen PCs nichts neu einzutragen ist.

## Mit Haupt-PC im Netz verbinden {#verbinden}

So verbindet sich ein weiterer PC. Die App am weiteren PC wird **ohne** `--standalone` gestartet (Server-Betrieb). Findet sie keinen Server, zeigt sie „Keine Verbindung zum Server“ und darunter „Mit Haupt-PC im Netz verbinden“. Wurde die App auf diesem PC früher als Einzelplatz benutzt, einmal mit `--server` starten; die App merkt sich das.

### Adresse {#verbinden-adresse}

Eine der Adressen, die am Haupt-PC unter „Im Netz“ steht. Erlaubt sind `empfang-pc`, `192.168.1.20:55443` oder die volle Form `https://empfang-pc:55443`. Ohne Port nimmt die App 55443. Unverschlüsselte Adressen mit `http://` lehnt sie ab.

### Verbindungscode {#verbinden-code}

Der Verbindungscode vom Haupt-PC. Groß- oder Kleinschreibung, Leerzeichen und Bindestriche spielen keine Rolle, es müssen aber genau 20 Zeichen sein.

„Verbinden“ geht, sobald beide Felder ausgefüllt sind. Die App prüft, ob der Haupt-PC antwortet und der Code zu seinem Zertifikat passt. Erst dann merkt sie sich Adresse und Code auf diesem PC und startet neu. Angemeldet wird danach wie am Haupt-PC mit Benutzername und Passwort.

### Eintrag vergessen {#vergessen}

Erscheint, wenn schon ein Haupt-PC eingetragen ist („Eingetragen: …“). Löscht Adresse und Code auf diesem PC und startet die App neu. Danach verbindet sie sich wieder mit dem voreingestellten Server, z. B. nach einem Umzug auf einen echten Server.

## Mit Server verbinden {#verbinden-server}

Läuft AISPA Studio auf einem Server, z. B. für die Verwaltung von zu Hause, trägt man dessen Adresse ein. Die Karte „Mit Server verbinden“ erscheint auf dem Startbildschirm unter „Keine Verbindung zum Server“, direkt unter „Mit Haupt-PC im Netz verbinden“. Der ganze Ablauf steht unter [Verwalten von zu Hause](hilfe:verwalten-von-zu-hause).

### Serveradresse {#verbinden-server-adresse}

Die Adresse des Servers, z. B. `https://spa.example.at`. Ohne `https://` ergänzt die App es. Unverschlüsselte Adressen mit `http://` lehnt sie ab. Einen Port nur eintragen, wenn der Server nicht auf dem üblichen Port 443 läuft.

„Server verbinden“ geht, sobald das Feld ausgefüllt ist. Die App prüft, ob der Server antwortet und sein Zertifikat gültig ist, so wie ein Browser es prüft. Erst dann merkt sie sich die Adresse auf diesem PC und startet neu. Klappt die Prüfung nicht, steht die Meldung unter dem Feld und nichts wird gemerkt. Einen Verbindungscode braucht es nicht; der gehört nur zum Haupt-PC im Spa.

Die Umgebungsvariable `AISPA_API_URL` hat Vorrang vor einer eingetragenen Adresse. Sie ist für Entwicklung und Tests gedacht.

## Wenn etwas nicht geht {#probleme}

- „… ist nicht erreichbar“: Ist der Haupt-PC an, die App dort offen und „Im Netz freigeben“ eingeschaltet? Hat Windows am Haupt-PC die Firewall für private Netzwerke erlaubt? Sind beide PCs im selben Netz (nicht im Gäste-WLAN)?
- „Der Verbindungscode passt nicht zu …“: Den Code am Haupt-PC unter „Im Netz“ neu ablesen. Nach einer Wiederherstellung hat der Haupt-PC einen neuen Code.
- Die Einrichtung eines Spas und „Passwort vergessen“ mit dem Wiederherstellungsschlüssel gehen nur direkt am Haupt-PC, nicht über das Netz.
