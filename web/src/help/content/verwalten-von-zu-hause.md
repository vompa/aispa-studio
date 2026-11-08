---
art: ablauf
bereich: Betrieb
oberflaeche: windows-app
rollen: inhaber, manager, leitung
seite: netz
stichwoerter: von zu Hause, Homeoffice, Verwaltung zu Hause, remote, VPN, Portweiterleitung, Port öffnen, Server verbinden, verlorenes Gerät, Laptop weg, Zugang sperren, Laufwerksverschlüsselung, BitLocker, work from home, lost device
---
# Verwalten von zu Hause

Von zu Hause verwaltet die Leitung mit derselben Windows-App und derselben Anmeldung wie im Spa. Es gibt keine eigene Heimversion und keine Verwaltung im Browser.

## Voraussetzungen {#voraussetzungen}

- Die App ist auf dem Heim-PC installiert.
- Das Spa betreibt einen Server mit gültigem Zertifikat, oder das Spa hat ein VPN zum Haupt-PC (siehe unten).
- Man hat einen Zugang mit dem Recht, das man zu Hause braucht, z. B. Inhaber oder Standortleitung.

## Mit einem Server verbinden {#server}

1. Die App am Heim-PC starten. Findet sie keinen Server, zeigt der Startbildschirm „Keine Verbindung zum Server“.
2. Unter **Mit Server verbinden** die Serveradresse eintragen, z. B. `https://spa.example.at`, und auf **Server verbinden** klicken.
3. Die App prüft die Verbindung und das Zertifikat, merkt sich die Adresse und startet neu.
4. Wie im Spa mit Benutzername und Passwort anmelden.

Unter **Netz** in der Seitenleiste steht danach, mit welchem Server die App arbeitet. **Verbindung lösen** trägt die Adresse wieder aus. Die Felder erklärt [Im Netz](hilfe:im-netz#verbinden-server).

## Haupt-PC im Spa: VPN statt Portweiterleitung {#vpn}

Arbeitet das Spa ohne Server, läuft AISPA Studio auf dem Haupt-PC (siehe [Im Netz](hilfe:im-netz)). Dieser PC lauscht im Netz des Spas auf allen Netzwerkkarten. Das Programm öffnet aber nichts nach außen, und das soll so bleiben.

- Von zu Hause geht es nur über ein VPN, das das Spa oder sein IT-Betreuer einrichtet. Der Heim-PC ist dann wie im Spa-Netz und verbindet sich mit „Mit Haupt-PC im Netz verbinden“.
- Keine Portweiterleitung im Router einrichten. Sonst steht der Haupt-PC mit Kundendaten im offenen Internet.

> Achtung: Eine Portweiterleitung auf den Haupt-PC macht Kundendaten aus dem Internet erreichbar. Dafür gibt es VPN oder einen Server.

## Verlorenes oder gestohlenes Gerät {#verlorenes-geraet}

Auf jedem PC bleibt der zuletzt geladene Stand (Kalender, Kundenkarten) verschlüsselt im Windows-Konto, damit die App ohne Verbindung weiterarbeitet. Dieser Schutz hängt am Windows-Konto. Deshalb:

1. Den Zugang der betroffenen Person sperren, wie unter [Zugänge sperren](hilfe:zugaenge-sperren) beschrieben. Das beendet die Anmeldung; neue Daten kommen danach nicht mehr auf das Gerät.
2. Das Windows-Konto am Gerät durch ein Kennwort schützen und einen Bildschirmschoner mit Sperre einstellen.
3. Die Laufwerksverschlüsselung von Windows (BitLocker bzw. „Geräteverschlüsselung“) einschalten. Ohne sie lässt sich der Offline-Stand eines gestohlenen Geräts mit etwas Aufwand auslesen.

> Hinweis: „Zugang sperren“ löscht den Stand auf dem Gerät nicht, und gesperrt lässt sich dort auch nicht mehr abmelden. Der Stand bleibt auf dem Gerät, bis es sich mit einem gültigen Zugang wieder anmeldet und abmeldet. Wer ein Gerät aus der Hand gibt, meldet sich vorher ab; Abmelden löscht den lokal gespeicherten Stand.
