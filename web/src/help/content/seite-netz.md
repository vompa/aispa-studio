---
art: seite
bereich: Betrieb
oberflaeche: windows-app
seite: netz
stichwoerter: Netz, Server, Serveradresse, Verbindung lösen, von zu Hause, Homeoffice, VPN, AISPA_API_URL, remote, server address
---
# Seite „Netz“ in der Windows-App

Die Seite „Netz“ gibt es nur, wenn die App mit einem Server arbeitet, nicht im Einzelplatz-Betrieb (dort heißt die Seite [Im Netz](hilfe:seite-im-netz)). Sie zeigt, womit die App verbunden ist: mit einem Server, mit einem Haupt-PC im Spa oder über die Umgebungsvariable `AISPA_API_URL`.

## Aufbau {#aufbau}

- **Verbindung**: ein Satz mit der Adresse, mit der die App arbeitet.
- **Verbindung lösen**: löscht die eingetragene Adresse auf diesem PC und startet die App neu. Danach fragt der Startbildschirm wieder nach einem Server. Der Knopf ist gesperrt, wenn nichts eingetragen ist oder wenn `AISPA_API_URL` gilt; diese Variable hat Vorrang und wird in Windows gesetzt, nicht hier.

Wie man verbindet und von zu Hause arbeitet, steht unter [Verwalten von zu Hause](hilfe:verwalten-von-zu-hause). Die Felder der Eingabe erklärt die Einstellungshilfe [Im Netz](hilfe:im-netz#verbinden-server).
