---
art: ablauf
bereich: Stammdaten
oberflaeche: verwaltung
rollen: inhaber
seite: verwaltung
stichwoerter: Claude-Schlüssel, API-Schlüssel, API Key, Schlüssel eintragen, Schlüssel prüfen, Schlüssel entfernen, Claude einschalten, Claude Console, Guthaben, enter api key, claude key
---
# Claude-Schlüssel eintragen

Claude braucht einen API-Schlüssel von Anthropic. Die Inhaberin trägt ihn in der Windows-App ein; er gilt nur für diesen Betrieb und sofort, ohne Neustart. AISPA Studio speichert ihn verschlüsselt und zeigt danach nur noch den Anfang und die letzten vier Zeichen, zum Beispiel „sk-ant-…a1b2“.

## Schlüssel eintragen {#eintragen}

1. In der Claude Console (`console.anthropic.com`) unter **API Keys** einen Schlüssel erstellen und kopieren. Er beginnt mit „sk-ant-“.
2. In der Verwaltung **KI** öffnen.
3. Den Schlüssel bei **Neuer Schlüssel** einfügen.
4. Auf **Speichern** klicken. Die Zeile darüber zeigt „Eingetragen: sk-ant-…“, und bei Claude steht „Eingeschaltet.“
5. Auf **Prüfen** klicken. AISPA Studio schickt eine kurze Testanfrage an Claude und meldet das Ergebnis.

Ein neuer Schlüssel ersetzt den alten: einfach wieder bei **Neuer Schlüssel** einfügen und speichern.

## Ergebnis der Prüfung {#pruefen}

- **Gültig:** Claude hat geantwortet. Alles ist bereit.
- **Ungültig:** Anthropic lehnt den Schlüssel ab, etwa weil er gelöscht wurde. In der Claude Console einen neuen erstellen und eintragen.
- **Kein Guthaben oder Limit erreicht:** Der Schlüssel stimmt, aber das Konto hat kein Guthaben mehr oder ein Limit ist erreicht. In der Claude Console unter **Billing** prüfen.
- **Nicht erreichbar:** Claude antwortet gerade nicht, meist fehlt die Internetverbindung. Später noch einmal prüfen.

## Schlüssel entfernen {#entfernen}

1. Auf **Entfernen** klicken.
2. Die Rückfrage mit **Entfernen** bestätigen.

Claude ist danach aus. Antworten an Gäste kommen wieder aus den festen Textbausteinen, und Terminwünsche liest das lokale Modell oder die festen Regeln. Mehr dazu: [KI](hilfe:ki#claude).

## Schlüssel in der Konfiguration {#konfiguration}

Hat der Betreiber des Servers den Schlüssel in der Konfiguration hinterlegt (`Ai:Claude:ApiKey`), gilt dieser für alle Betriebe des Servers. Die Zeile zeigt dann „Aus der Konfiguration gesetzt: sk-ant-…“, und Feld, **Speichern** und **Entfernen** sind gesperrt. **Prüfen** geht trotzdem. Ändern lässt sich dieser Schlüssel nur am Server, siehe „Server-Einstellungen“, Abschnitt KI.
