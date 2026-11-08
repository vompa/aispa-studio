# 0057. Lokale KI optional mit Ollama im Docker, Claude bleibt für Texte an Gäste

- **Status:** angenommen
- **Datum:** 19.10.2026
- **Entschieden von:** Auftraggeber („das im docker lauft“, „ja optional klingt gut“, 19.10.2026), Ausgestaltung Claude

## Kontext

Die KI lief bisher nur über Claude und damit nur mit Internet. Ohne Schlüssel oder ohne Verbindung arbeiten Terminannahme und Assistent nach festen Regeln, die nur einfache Sätze verstehen („Massage am 12.03. um 10 Uhr“). Der Auftraggeber fragte, ob ein kleines Modell offline in der App laufen kann. Auf die Empfehlung (kleines offenes Modell für Hilfe und das Zerlegen von Terminwünschen, Claude für Texte an Gäste, optionaler Download) antwortete er, dass es im Docker laufen soll und optional sein soll.

## Optionen

| Option | Bewertung |
| --- | --- |
| A: Ollama als eigener Container neben der API, Modell auf Wunsch geladen | gewählt. Eine bewährte, kostenlose Laufzeit mit HTTP-Schnittstelle und JSON-Schema-Ausgabe; läuft auf CPU, unter Docker und als Windows-Programm gleich. Die API bleibt schlank. |
| B: Modell im Prozess der API (LLamaSharp oder ONNX Runtime GenAI) | Für den Einzelplatz ohne Docker eleganter, aber: native Bibliotheken je Prozessor und Windows-Version (über 100 MB), das Modell belegt 3 bis 5 GB im Prozess der API und nimmt sie bei Speichermangel mit, eigene Pflege der Laufzeit. Zurückgestellt. |
| C: Ein großes Modell lokal für alles, auch Antworten an Gäste | Braucht eine Grafikkarte oder ist auf CPU zu langsam; kleine Modelle formulieren auf Französisch und Italienisch zu unsicher für Texte, die an Gäste gehen. |
| D: Nur Claude, ohne lokale Möglichkeit | Ohne Internet bleibt es bei den Regeln. Der Auftraggeber wollte die Offline-Möglichkeit. |

## Entscheidung

- Ollama läuft als optionaler Dienst: in `compose.yaml` als Container `ollama` im Profil `ki-lokal`, im Aspire-AppHost mit `--LocalAi:Enabled=true`. Ohne Docker, etwa beim Einzelplatz, ist es das Windows-Programm von Ollama. Die API spricht nur mit `Ai:Local:Url`; leer heißt aus, und es ändert sich nichts.
- Standardmodell `gemma3:4b` (Gemma 3, 4 Mrd. Parameter, etwa 3,3 GB, gut auf Deutsch, läuft ohne Grafikkarte). Austauschbar über `Ai:Local:Model`.
- Das Modell lädt die Leitung in den Stammdaten unter „KI“ herunter. Der Download läuft über die Queue, nie im HTTP-Aufruf; ein Hintergrunddienst prüft jede Minute, ob Dienst und Modell da sind, und die Oberfläche liest nur diesen Stand.
- Das lokale Modell übernimmt nur zwei Aufgaben: Mails und Nachrichten in Bausteine zerlegen (`MailAnalysis`: Absicht, Behandlung, Standort, Person, Wunschzeiten) und Fragen an den Assistenten einordnen (`AssistantIntent`), darunter Hilfefragen, die es in die Worte des Handbuchs übersetzt; gesucht wird im Handbuch weiter ohne KI ([ADR 0056](0056-handbuch-mit-ablaeufen-und-bildern-als-grundlage-der-ki-hilfe.md)). Es nutzt dieselben Anweisungen und dasselbe JSON-Schema wie Claude; Ids außerhalb der Auswahllisten verwirft die Terminannahme wie bisher. Freie Zeiten rechnet weiter der Planer.
- Reihenfolge: Mit Claude liest zuerst Claude; antwortet Claude nicht, übernimmt das lokale Modell (`FallbackAiJson`). Ohne Claude liest immer das lokale Modell; antwortet es nicht oder ist es nicht geladen, gelten die festen Regeln (`LocalBookingAssistant`, `LocalAssistantClassifier`).
- Antworten an Gäste, Übersetzen, Überarbeiten, Zusammenfassungen, Anamnese vom Papier und Texte für die Wiederbuchung bleiben bei Claude. Ohne Claude kommen Antworten an Gäste aus den festen Textbausteinen.
- `Ai:Provider=RuleBased` schaltet auch das lokale Modell ab.

## Begründung

Ein Container hält die API und die Windows-App klein und trennt den Speicherbedarf des Modells vom Kassen- und Kalenderbetrieb: läuft der PC voll, stirbt höchstens Ollama, nicht die Kassa. Ollama bietet die strukturierte Ausgabe nach JSON-Schema, sodass das lokale Modell genauso eingeschränkt ist wie Claude: es liefert nur Daten und löst nichts aus. Zerlegen und Einordnen sind Aufgaben, die ein Modell mit 4 Mrd. Parametern verlässlich kann; dort bringt es gegenüber den Regeln am meisten (freie Formulierungen wie „morgen nach der Arbeit“, „nicht bei Marco“). Texte an Gäste verlangen sichere Formulierung in vier Sprachen und bleiben deshalb bei Claude oder den geprüften Bausteinen.

## Folgen und Stand

- Code: `src/AispaStudio.Api/LocalAi/` (Optionen, Ollama-Client, Status, Download, lokale Assistenten, Endpunkte `GET /api/system/ai` und `POST /api/system/ai/local/download`), `IAiJson` und `FallbackAiJson` in `MailIntake/Assistant/IAiJson.cs`, Auswahl in `MailIntakeSetup`. Oberfläche: `web/src/admin/AiTab.tsx`, Hilfe `web/src/help/content/ki.md`. Tests: `LocalAiTests`, `AiTab.test.tsx`.
- Hardware: siehe Hilfethema „Systemanforderungen“, Abschnitt „Lokale KI“. Das Ollama-Image ist 3,8 GB groß (gemessen), das Modell 3,3 GB; im Betrieb braucht der PC mindestens 8 GB, besser 16 GB Arbeitsspeicher.
- Gegen ein echtes Modell konnte in der Entwicklungsumgebung nicht geprüft werden, weil das Modellverzeichnis dort gesperrt ist; geprüft sind die Schnittstelle gegen Ollama 0.40.6 (Liste, Download mit Fehler, „Modell fehlt“) und das Verhalten mit nachgestellten Antworten.
- Offen:
  - Ein Schalter in der Windows-App für den Einzelplatz, der Ollama mitinstalliert oder findet und `Ai__Local__Url` setzt. Bis dahin per Umgebungsvariable.
  - Option B neu bewerten, wenn Einzelplatz-Kunden ohne Ollama die lokale KI wollen.
  - Prüfen, wie gut `gemma3:4b` echte Mails des Testdatensatzes liest, sobald ein Modell verfügbar ist, und ggf. `qwen3:4b` vergleichen.

## Quellen

- Nachrichten des Auftraggebers im Projektchat vom 19.10.2026, 19:33 und 19:34 UTC
- [ADR 0041](0041-einzelplatz-standalone-und-zuschaltbare-webmodule.md) (Einzelplatz und Webmodule), [ADR 0055](0055-hilfe-zu-jeder-einstellung-im-repository.md) (Hilfe zu jeder Einstellung)
- Hilfethemen „KI“ und „Systemanforderungen“, [ADR 0056](0056-handbuch-mit-ablaeufen-und-bildern-als-grundlage-der-ki-hilfe.md) (Handbuch als Grundlage der KI-Hilfe)
