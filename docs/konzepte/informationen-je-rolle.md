# Informationen je Rolle: feste Ansichten und MCP-Server

Stand: 03.11.2026. Anlass: Der Auftraggeber möchte, dass Gäste, Mitarbeitende und Management ihre Daten, Buchungen und das Lager selbst abrufen können. Das Dokument ist ein Konzept, keine Entscheidung. Die Entscheidungen gehören in ADR 0063, die für die „offene Schnittstelle für Personal und Kunden“ reserviert ist.

Wie belastbar ist das? Der Ist-Stand stammt aus Code und Doku vom 03.11.2026. Die Angaben zu MCP Apps und zu Azure stammen aus Herstellerseiten und Fachpresse mit Abrufdatum; die Angaben zu Datenzonen sind vor der Umsetzung an den Seiten von Microsoft und Anthropic zu prüfen.

## Empfehlung in Kürze

- Gäste und Mitarbeitende bekommen feste Ansichten in ihren Apps, über die bestehende API und ohne KI.
- Das Management bekommt zusätzlich einen MCP-Server für offene Fragen aus einem KI-Programm wie Claude oder Microsoft 365 Copilot.
- Der MCP-Server liefert Gäste und Mitarbeitende nur als Kennung. Namen setzt erst die Anzeige ein, die AISPA selbst zeichnet.
- Der eingebaute Assistent nutzt denselben Werkzeugkatalog wie der MCP-Server.
- Der KI-Anbieter ist wählbar. Standard im Betrieb ist Azure in der EU-Datenzone; in der Entwicklung bleibt Claude.

## Feste Ansicht oder offene Frage

| Merkmal | Feste Ansicht über die API | Offene Frage über MCP |
|---|---|---|
| Frage | bekannt, immer gleich („Meine Termine“) | offen, wechselnd („Wie lief Massage im Oktober gegen das Vorjahr?“) |
| Wer rechnet | der Server | Werkzeuge des Servers; das Modell wählt und verbindet sie |
| Antwortzeit und Kosten | schnell, ohne Gebühren je Abruf | Sekunden, Gebühren je Frage beim KI-Anbieter |
| Ergebnis | immer gleich, voll testbar | Wortlaut wechselt, Zahlen bleiben gleich |
| Ohne Netz | lesbar aus dem Zwischenspeicher | nicht nutzbar |
| Passt für | Gäste, Mitarbeitende, Tafel, Berichte | Management mit Auswertungsfragen |

Eine feste Ansicht ist richtig, wenn die Frage vorher bekannt ist. Das gilt für fast alles, was Gäste und Mitarbeitende über sich wissen wollen.

## Warum MCP für offene Fragen besser passt als eine API-Anbindung

MCP ersetzt die API nicht. Der MCP-Server ist eine Schicht über denselben Diensten und Rechten.

1. **Das Modell findet die Werkzeuge selbst.** Jedes Werkzeug beschreibt sich mit Name, Zweck und Eingaben. Das KI-Programm wählt passende Werkzeuge und verbindet mehrere zu einer Antwort. Eine API-Anbindung bräuchte für jede Frage eine vorher programmierte Abfolge.
2. **Eine Anbindung für viele KI-Programme.** Claude, ChatGPT und Microsoft 365 Copilot sprechen MCP. Für eine API müsste AISPA je Programm eine eigene Erweiterung bauen und pflegen.
3. **Anmeldung nach Standard.** MCP legt OAuth als Anmeldeweg fest. Die Person meldet sich mit ihrem AISPA-Zugang an, und jedes Werkzeug läuft mit ihren Rechten (ADR 0008).
4. **Anzeige ohne Umweg über das Modell.** Mit MCP Apps zeigt das KI-Programm eine kleine AISPA-Ansicht an. Diese Ansicht lädt Namen direkt bei AISPA nach, ohne dass das Modell sie sieht.
5. **Ein Katalog für drinnen und draußen.** Der eingebaute Assistent ruft dieselben Werkzeuge auf. Rechte, Pseudonymisierung und Tests gibt es einmal statt zweimal.

Wo MCP nicht passt: feste Anzeigen, Pflichtzahlen für Kassa und Behörden, alles ohne Netz und alles, was in Millisekunden kommen muss.

## Was jede Gruppe sieht

### Gäste (Kunden-App, feste Ansichten)

| Bereich | Heute | Neu |
|---|---|---|
| Termine | ansehen, verschieben, absagen, .ics (höchstens 100) | ganzer Verlauf, seitenweise |
| Gutscheine, Warteliste | ja | unverändert |
| Belege und offene Beträge | nein | Belege als PDF, Anzahlungen, offene Ausfallgebühren, Paketstand mit Restbehandlungen |
| Eigene Daten | Name und Telefon ändern | Stammdaten ansehen, Einwilligungen ansehen und widerrufen |
| Auskunft nach Art. 15 DSGVO | nein | Download auf Knopfdruck |
| Anamnese | nur Stand | später: Inhalt lesen, mit zweiter Stufe der Anmeldung |

Gäste bekommen keinen MCP-Zugang. Ein Online-Konto zu löschen ist nicht vorgesehen. Das Datenmodell kennt heute kein Löschen von Kunden (ADR 0061); eine Änderung braucht einen eigenen ADR.

### Mitarbeitende (Mitarbeiter-App, nur eigene Person)

| Bereich | Heute | Neu |
|---|---|---|
| Tag und Woche, Abwesenheitsanträge | ja | unverändert |
| Stunden | keine (Zeiten trägt die Leitung ein, T-015) | Stunden je Woche und Monat, Soll gegen Ist, Saldo |
| Urlaub | eigene Anträge | Anspruch, genommen, Rest, Stand der Anträge |
| Dienstplan | indirekt über den Tagesplan | veröffentlichter Plan der nächsten Wochen als Liste |
| Lohnzettel, eigene Auskunft | nein | später, mit zweiter Stufe der Anmeldung |

Eigene Kennzahlen wie Auslastung oder Wiederbuchungsquote gibt es bewusst nicht (ADR 0008). Stunden und Saldo in der App brauchen einen Nachtrag zu ADR 0084.

Therapeut:innen sehen überall nur eigene Gäste. Das sind Gäste mit einem eigenen vergangenen oder kommenden Termin, in der Windows-App ebenso wie in App, Assistent und MCP. Heute liefern Kundensuche und Terminliste eines Gastes in der Windows-App den ganzen Kundenstamm.

### Management (Windows-App, „Heute im Spa“, MCP)

- Feste Ansichten bleiben die Berichte in der Windows-App und der Reiter „Heute im Spa“ am Handy.
- Neu ist der MCP-Server für offene Fragen. Beispiele: Umsatz einer Behandlung gegen das Vorjahr, Auslastung der Räume am Wochenende, Artikel mit hohem Verbrauch und offene Bestellungen.
- Beträge gibt es weiter nur mit „Berichte mit Beträgen“. Das Backoffice sieht weiter keinen Kalender und keine Gäste (ADR 0070).

## Aufbau des MCP-Servers

- **Modul:** zuschaltbares Webmodul nach ADR 0041. Der Einzelplatz läuft ohne; ein lokaler Zugang für den Einzelplatz folgt später.
- **Werkzeugkatalog:** Die heutigen Antwortgeber des Assistenten (`src/AispaStudio.Api/Assistant/Answers`) und die Berichte (`/api/reports/*`, `/api/stock`) werden zu Werkzeugen. Jedes Werkzeug prüft die Rechte selbst, wie die Antwortgeber heute.
- **Zahlen:** Werkzeuge liefern Werte und Vergleiche fertig gerechnet, mit Vorperiode und Differenz. Das Modell rechnet nicht nach. Pflichtzahlen für Kassa, Steuer und Behörden gibt der MCP-Server nicht aus.
- **Zwei Eingänge:** Der eingebaute Assistent ruft den Katalog im selben Prozess mit den Rechten der Person auf. Das Muster dafür steht in OPCAgentSample (`McpAgentToolbox`). Fremde KI-Programme rufen `/mcp` über das Netz auf.
- **Anmeldung:** OAuth mit dem AISPA-Zugang der Person. Der Kanal `Mcp` lässt wie `StaffApp` nur freigegebene Werkzeuge zu; ein Kanal-Test hält die Liste fest (wie `ChannelScanTests`).
- **MCP App:** eigener kleiner Einstieg im Web-Ordner mit React, TypeScript und Vite (ADR 0004), wie der Kundenbereich ohne Importe aus den anderen Web-Apps. Er läuft im KI-Programm in einem abgeschotteten Rahmen und lädt die Namen selbst über die API.
- **Protokoll:** Jeder Werkzeugaufruf wird mit Person, Werkzeug, Zeit und Anzahl Treffer protokolliert, ohne Werte.

## Pseudonymisieren und Anreichern

Der MCP-Server liefert Personen nur als Kennung, zum Beispiel „Gast G-7F3A“ oder „Person P-02“. Die Namen holt erst die Anzeige über die API, mit den Rechten der angemeldeten Person. So erreichen Gastnamen und Mitarbeiterdaten den KI-Anbieter nicht.

1. **Kennungen je Sitzung.** Die Zuordnung bleibt in AISPA und wechselt je Sitzung. Der KI-Anbieter kann über Sitzungen hinweg kein Profil bilden.
2. **Auflösen in AISPA.** Im eingebauten Assistenten ersetzt die Windows-App die Kennungen vor der Anzeige.
3. **Auflösen per MCP App.** In Claude und Microsoft 365 Copilot zeigt eine AISPA-Ansicht die Namen. Sie lädt direkt bei AISPA nach. In KI-Programmen ohne MCP Apps bleiben Kennung und ein Link „in AISPA öffnen“.
4. **Keine Freitexte.** Notizen, Vermerke und Betreffzeilen gehen nie über MCP, weil sie oft Namen enthalten.
5. **Kleine Gruppen unterdrücken.** Werte über weniger als 5 Gäste oder Personen fasst der Server zusammen oder lässt sie weg. In einem kleinen Team wäre „die einzige Masseurin am Dienstag“ sonst erkennbar.
6. **Nie Gesundheitsdaten.** Auch nicht als Stand oder Kennzeichen (ADR 0013).

Für AISPA bleiben pseudonymisierte Daten personenbezogen. Ob sie es auch für den KI-Anbieter sind, bewertet die Datenschutz-Beratung.

## Excel

Das Management kann den MCP-Server auch in Excel nutzen, auf zwei Wegen:

- **Claude in Excel:** Das Add-in nutzt seit 17.02.2026 MCP-Connectoren. Ein in Claude eingerichteter AISPA-Connector steht in Excel ohne weitere Einrichtung bereit.
- **Microsoft 365 Copilot:** Ein Agent mit MCP-Server läuft laut Microsoft auch in Excel. Die Anbindung von MCP-Servern an solche Agenten ist teils noch in Vorschau; vor der Umsetzung prüfen.

Im Arbeitsblatt stehen dann Kennungen statt Namen, weil die Werte über das Modell kommen. Eine Mappe mit Namen kommt über einen festen Abruf: Power Query liest einen Bericht direkt aus der API, mit der Anmeldung der Person und ohne KI. Dieser Abruf ist eine feste Ansicht im Sinn dieses Konzepts. Eine Mappe verlässt AISPA, sobald sie weitergegeben wird; das Protokoll hält fest, wer welchen Bericht abgerufen hat.

## KI-Anbieter

- **Wählbar:** AISPA stellt den Assistenten von `IAiJson` auf `IChatClient` um, nach dem Muster von OPCAgentSample (`feature/meai-chatclient`). Profile: Azure, Anthropic, OpenAI, lokal über Ollama (ADR 0057). Ein Wechsel ist eine Einstellung.
- **Standard im Betrieb:** Azure in der EU-Datenzone mit einem Modell, das Microsoft selbst betreibt. Vertrag und Rechenzentrum liegen bei Microsoft; das passt zum Hosting in Deutschland (ADR 0016).
- **Entwicklung:** Claude, nur mit Demo-Daten.
- **Claude über Azure:** Nach Berichten der Fachpresse hat Claude in Azure Foundry keine EU-Datenzone (Stand 07–09/2026). Sobald es eine gibt, ist Claude im Betrieb nur eine Einstellung.
- **Fremde KI-Programme:** Wer Claude oder Copilot am MCP-Server nutzt, bringt dessen Modell und Vertrag mit. Die Pseudonymisierung schützt dort die Personendaten.

Eine Anbindung an Azure gibt es in OPCAgentSample noch nicht; das Azure-Profil wird für AISPA neu entworfen.

## Reihenfolge

| Nr. | Schritt | Gruppe | Aufwand |
|---|---|---|---|
| 1 | Therapeut:innen überall nur eigene Gäste | Team | S |
| 2 | Kunden-App: Belege, offene Beträge, Paketstand | Gäste | M |
| 3 | Kunden-App: eigene Daten, Einwilligungen, Art.-15-Auskunft | Gäste | M |
| 4 | Mitarbeiter-App: Stunden und Saldo, Urlaub, Dienstplan | Team | M |
| 5 | KI-Anbieter über `IChatClient`, Azure-Profil | alle | M |
| 6 | Werkzeugkatalog aus den Antwortgebern, Assistent darauf umstellen | Management | L |
| 7 | MCP-Server mit OAuth, Kanal, Protokoll, Pseudonymisierung | Management | L |
| 8 | MCP App zum Auflösen der Kennungen | Management | M |
| 9 | Berichte für Power Query in Excel freigeben | Management | S |

Schritte 1 bis 4 brauchen keine KI und können vor dem MCP-Server kommen.

## Gestaltung der neuen Web-Ansichten

Neue Ansichten in Kunden-App, Mitarbeiter-App und MCP App folgen dem Design-Brief (`docs/design.md`). Erste Tests zeigen, dass das Web heute deutlich anders aussieht als die Windows-App. Gemeinsam geprüft werden bisher nur die Farben. Die Regeln für ein gleiches Bild in beiden Oberflächen erarbeitet ein eigenes Konzept „Ein Design für Web und Windows-App“. Die Web-Schritte dieses Konzepts warten auf dessen Entscheidungen.

## Später und bewusst nicht

- Später: Anamnese in der Kunden-App lesen, Lohnzettel und eigene Auskunft für Mitarbeitende, MCP für Mitarbeitende, MCP im Einzelplatz, standortübergreifende Auswertungen.
- Bewusst nicht: MCP für Gäste, Konto löschen (eigener ADR nötig), eigene Kennzahlen für Mitarbeitende (ADR 0008), Gesundheitsdaten und Freitexte über MCP, Pflichtzahlen über MCP.

## Beim Schreiben festgestellt

- Den Reiter „Heute im Spa“ prüft der Server über `ViewFinance`. Laut ADR 0084 ist er nur für Inhaberin und Managerin; Standortleitung und Backoffice kommen so ebenfalls daran.
- ADR 0003 und die Rollentabelle in `docs/produkt/anforderungen.md` nennen noch „Management im Web“; ein Nachtrag zu ADR 0084 fehlt.
- Die Kanal-Regeln stehen nur im PR-Protokoll, nicht in `docs/architektur.md`.
- Lesezugriffe auf Personendaten werden nirgends protokolliert, obwohl `docs/konzepte/reporting.md` das vorsieht.

## Offene Fragen an den Auftraggeber

1. Wer prüft mit der Datenschutz-Beratung, ob pseudonymisierte Daten für den KI-Anbieter personenbezogen sind?
2. Soll die Mindestgruppe von 5 fest bleiben oder je Betrieb einstellbar sein?
3. Welches Microsoft-Modell wird Standard, wenn die Datenzone geprüft ist?

## Quellen

- MCP Apps, Model Context Protocol: https://modelcontextprotocol.io/extensions/apps/overview (abgerufen 03.11.2026)
- MCP Apps in Microsoft 365 Copilot, Microsoft Learn: https://learn.microsoft.com/it-it/microsoft-365/copilot/extensibility/plugin-mcp-apps (abgerufen 03.11.2026)
- Claude unterstützt MCP Apps, The Register, 26.01.2026: https://www.theregister.com/special-features/2026/01/26/claude-supports-mcp-apps-presents-ui-within-chat-window/4645652 (abgerufen 03.11.2026)
- MCP Apps in Claude und ChatGPT, Alpic: https://alpic.ai/blog/mcp-apps-goes-official-claude-chatgpt-support (abgerufen 03.11.2026)
- Claude in Microsoft Foundry ohne EU-Datenzone, InfoQ: https://www.infoq.com/news/2026/07/claude-foundry-ga-europe/ (abgerufen 03.11.2026)
- Claude in Microsoft Foundry, Datenzonen, Stackmaven: https://stackmaven.io/news/claude-microsoft-foundry-ga-eu-data-zone-gap/ (abgerufen 03.11.2026)
- Claude in Excel mit MCP-Connectoren, Claude auf X, 17.02.2026: https://x.com/claudeai/status/2023817143096406246 (abgerufen 03.11.2026)
- Claude für Excel, Anthropic: https://claude.com/docs/office-agents/excel (abgerufen 03.11.2026)
- Agenten in Microsoft 365 Copilot, Microsoft Learn: https://learn.microsoft.com/microsoft-365/copilot/extensibility/agents-overview (abgerufen 03.11.2026)
- MCP in Copilot Studio, Microsoft: https://www.microsoft.com/en-us/microsoft-copilot/blog/copilot-studio/introducing-model-context-protocol-mcp-in-copilot-studio/ (abgerufen 03.11.2026)
- Ist-Stand im Repository: `src/AispaStudio.Api/Access/Roles.cs`, `Channels.cs`, `Online/CustomerPortalEndpoints.cs`, `Assistant/RoleAssistant.cs`, `web/src/myday`, OPCAgentSample `src/OPCAgentSample.Server/Mcp/McpRegistration.cs`
