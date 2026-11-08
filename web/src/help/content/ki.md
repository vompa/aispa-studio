---
art: einstellung
bereich: Stammdaten
oberflaeche: verwaltung
rollen: inhaber, manager
seite: verwaltung
stichwoerter: künstliche Intelligenz, Claude, offline, ohne Internet, Ollama, lokales Modell, Modell herunterladen, AI
---
# KI

AISPA Studio arbeitet mit zwei KI-Arten, beide sind freiwillig. Claude läuft über das Internet und kann am meisten. Das lokale Modell läuft im eigenen Netz, auch ohne Internet, und übernimmt nur einfache Aufgaben. Ohne beide arbeitet AISPA Studio nach festen Regeln weiter. Pflichtzahlen wie Kasse, Steuer und Berichte für Behörden rechnet nie eine KI, und freie Zeiten rechnet immer der Kalender.

Den Stand beider sieht die Leitung in der Windows-App unter „Verwaltung“ › „Kontakt“ › „KI“. Die Maske zeigt den Stand und lädt das lokale Modell herunter; einen Schlüssel trägt man dort nicht ein.

## Claude {#claude}

Claude schreibt Antwortvorschläge an Gäste, übersetzt Nachrichten, fasst den Tag zusammen, liest Anamnesebögen auf Papier und versteht Terminwünsche aus Mails, SMS und Anrufen. Eingeschaltet ist Claude, sobald der Betreiber des Servers einen API-Schlüssel hinterlegt hat (siehe „Server-Einstellungen“, Abschnitt KI). In der App lässt sich das nicht umstellen, weil der Schlüssel ein Geheimnis des Servers ist.

Jede Nachricht, die eine KI an Gäste schreibt, trägt einen Hinweis darauf. Den Hinweis fügt AISPA Studio selbst an; er lässt sich nicht weglassen.

## Lokales Modell {#lokales-modell}

Ein kleines Sprachmodell (Standard: Gemma 3 mit 4 Milliarden Parametern) läuft auf dem eigenen PC oder Server, ohne Grafikkarte und ohne Internet. Es macht zwei Dinge:

- Es liest Terminwünsche aus Mails und Nachrichten und zerlegt sie in Behandlung, Standort, Person und Wunschzeit, zum Beispiel „morgen 17 Uhr Massage, nicht bei Marco“. Welche Zeiten frei sind, rechnet danach der Kalender.
- Es versteht Fragen ans Team im Assistenten („Was steht morgen an?“, „Wie lief letzte Woche?“, „Wie storniere ich einen Beleg?“). Die Zahlen dazu rechnet der Server, die Antwort auf Hilfefragen kommt aus dem Handbuch.

Antworten an Gäste, Übersetzungen und Zusammenfassungen schreibt es nicht. Kleine Modelle formulieren vor allem auf Französisch und Italienisch zu unsicher; ohne Claude kommen Antworten deshalb aus den festen Textbausteinen.

Ist Claude eingeschaltet, springt das lokale Modell nur ein, wenn Claude nicht antwortet, etwa weil das Internet ausgefallen ist. Ist Claude aus, liest immer das lokale Modell. Antwortet auch das lokale Modell nicht, gelten die festen Regeln. Die Arbeit am Empfang bleibt also nie stehen.

Die Anfragen verlassen das eigene Netz nicht.

### Zustand {#zustand}

- **Nicht eingerichtet:** Auf dem Server ist kein lokales Modell eingetragen. Das ist die Grundeinstellung.
- **Dienst antwortet nicht:** Eingetragen, aber der Dienst (Ollama) läuft nicht. Prüfen, ob der Container oder das Programm gestartet ist.
- **Noch nicht geladen:** Der Dienst läuft, das Modell fehlt. Mit „Modell herunterladen“ laden.
- **Wird heruntergeladen:** mit Fortschritt in Prozent; die Maske fragt alle zwei Sekunden nach. Der Download läuft im Hintergrund weiter, auch wenn man die Seite verlässt.
- **Bereit.**

### Modell herunterladen {#herunterladen}

Das Modell ist etwa 3,3 GB groß und wird einmal aus dem Internet geladen, danach liegt es dauerhaft auf dem PC oder Server. Je nach Leitung dauert das einige Minuten. Ein abgebrochener Download setzt beim nächsten Mal fort.

### Einrichten {#einrichten}

Das lokale Modell braucht den kostenlosen Dienst Ollama. Er läuft auf dem Server oder dem Haupt-PC des Spas.

- **Mit Docker:** In der Datei `.env` neben `compose.yaml` die Zeilen `COMPOSE_PROFILES=ki-lokal` und `AISPA_LOCAL_AI_URL=http://ollama:11434` eintragen und `docker compose up -d` ausführen. Danach unter „Verwaltung“ › „Kontakt“ › „KI“ das Modell herunterladen.
- **Ohne Docker, zum Beispiel beim Einzelplatz:** Ollama für Windows installieren und die Windows-Umgebungsvariable `Ai__Local__Url` auf `http://localhost:11434` setzen (Systemsteuerung, „Umgebungsvariablen für dieses Konto bearbeiten“). Danach AISPA Studio neu starten und das Modell herunterladen. Ein eigener Schalter dafür in der Windows-App ist geplant.

Welche Hardware es braucht, steht unter „Systemanforderungen“, Abschnitt „Lokale KI“.

### Einstellungen am Server {#einstellungen}

- `Ai:Local:Url`: Adresse des Ollama-Dienstes. Leer heißt aus.
- `Ai:Local:Model`: welches Modell, Standard `gemma3:4b`. Andere kleine Modelle wie `qwen3:4b` gehen auch; nach dem Ändern das neue Modell herunterladen.
- `Ai:Local:TimeoutSeconds`: so lange darf eine Antwort dauern, Standard `120`.
- `Ai:Local:KeepAlive`: so lange bleibt das Modell nach der letzten Anfrage im Arbeitsspeicher, Standard `10m`. Danach gibt es den Speicher frei und braucht beim nächsten Mal einige Sekunden zum Laden.
- `Ai:Local:CheckSeconds`: wie oft der Server nachsieht, ob der Dienst läuft, Standard `60`.
- `Ai:Provider` = `RuleBased` schaltet auch das lokale Modell ab.
