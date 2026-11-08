---
art: ablauf
bereich: Kalender
oberflaeche: windows-app
rollen: rezeption, therapeut, leitung, inhaber, manager
seite: kalender
stichwoerter: offline, ohne Internet, ohne Verbindung, Internet weg, Server nicht erreichbar, Ausfall, Netzwerkausfall, vorgemerkt, vormerken, Abgleich, synchronisieren, nicht übernommen, Konflikt, offline mode, no connection, sync, pending
---
# Arbeiten ohne Verbindung

Fällt die Verbindung zum Server aus, zeigt der Kalender den zuletzt geladenen Stand, und die wichtigsten Schritte gehen weiter: Sie werden vorgemerkt und beim nächsten Kontakt gesendet. Der Server prüft sie dann wie sonst auch.

## Woran man es erkennt {#erkennen}

- Links in der Leiste steht **Offline** statt **Online** (bzw. **Gleicht ab**, solange Vorgemerktes gesendet wird).
- Oben erscheint eine Warnung: „Keine Verbindung zum Server. Kalender und Kunden zeigen den zuletzt geladenen Stand; …“ mit der Zahl der vorgemerkten Änderungen. Die App prüft alle 15 Sekunden von selbst; **Verbindung prüfen** fragt den Server sofort an, statt auf den nächsten Durchgang zu warten.
- Oben stehen höchstens zwei solche Meldungen. Steht eine dritte an (z. B. die Erinnerung an die Sicherung), zeigt **Alle zeigen** auch sie.
- Unter dem Datum steht „Ohne Verbindung · Stand 19.10. 08:45“.

![Kalender ohne Verbindung](images/app/kalender-offline.png)

## Was weiter geht {#geht}

- [Termin buchen](hilfe:kalender-termin-buchen), auch mit neuer Kundin bzw. neuem Kunden. Die Kundensuche sucht in den zuletzt geladenen Kunden.
- [Verschieben](hilfe:kalender-termin-verschieben) per Ziehen oder Alt + Pfeile.
- [Status setzen](hilfe:kalender-status-setzen): einchecken, erledigt, nicht erschienen.
- Einzelnen Termin [stornieren](hilfe:kalender-termin-stornieren).
- [Paar oder Spa-Tag](hilfe:kalender-paar-und-spa-tag#offline) mit selbst eingetragenem Beginn.
- [Sauna-Eintritt](hilfe:kalender-sauna-eintritt) buchen.

Solche Termine tragen im Kalender „vorgemerkt“. Eine Paar- oder Spa-Tag-Buchung erscheint erst nach dem Abgleich.

## Was nicht geht {#geht-nicht}

- Kassieren.
- Freie Zeit suchen und Wunsch eintippen (KI).
- Pausen, Sperrzeiten, Notizen und Tagesnotizen.
- Krank melden und Ausfall-Assistent.
- Ganze gemeinsame Buchung verschieben oder stornieren, **Rückgängig** nach dem Verschieben.
- Eintritte stornieren oder kassieren.

## Abgleich {#abgleich}

1. Sobald die Verbindung zurück ist, sendet die App das Vorgemerkte der Reihe nach und lädt den Kalender und den Posteingang neu, ebenso die Kundenliste, wenn gerade keine Kundenkarte offen ist.
2. Was der Server ablehnt (z. B. Person inzwischen belegt), steht oben als Fehlermeldung „Beim Abgleich nicht übernommen“, darunter jede Änderung mit der Begründung. Das gilt auch für einen Termin, den inzwischen jemand an einem anderen Arbeitsplatz geändert hat; er bleibt dann so, wie der andere Arbeitsplatz ihn gespeichert hat. Mehrere eigene Änderungen am selben Termin gehen dagegen der Reihe nach durch.
3. Den Fall mit dem Gast klären, neu buchen oder verschieben und dann **Erledigt** neben der Meldung klicken.

## Hinweise {#hinweise}

- Antwortet der Server beim Start nicht gleich (er fährt z. B. noch hoch), zeigt die App den Startbildschirm mit dem Stand, etwa „Datenbank wird vorbereitet …“ oder „Anmeldung wird geladen …“. Nach zehn Sekunden steht darunter „Das dauert länger als üblich.“; **Technische Angaben** zeigt die Adresse des Servers und die Phase. Nach drei Minuten ohne Antwort steht „Keine Verbindung zum Server“ mit **Erneut versuchen**; die Anmeldung bleibt gemerkt.
- Damit ein Ausfall nicht überrascht, lädt die App bei bestehender Verbindung etwa alle zehn Minuten den Kalender der nächsten sieben Tage aller Standorte, die Kundenkarten dieser Gäste und die Kassa-Artikel vor.
- Der lokale Stand liegt verschlüsselt im Windows-Benutzerprofil; Gesundheitsdaten werden nie zwischengespeichert. Beim Abmelden und beim Wechsel zu einer anderen Person am Empfangsgerät wird er gelöscht. Vorgemerktes, das noch nicht gesendet ist, bleibt dabei erhalten und geht mit der nächsten Anmeldung aus demselben Spa an den Server; im Änderungsprotokoll steht dann die Person, die gerade angemeldet ist.
- Im Einzelplatz-Betrieb arbeitet die Windows-App mit eigener Datenbank; wie sie sich mit einer zentralen Datenbank abgleicht, steht unter [Abgleich](hilfe:abgleich).
