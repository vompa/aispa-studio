# Kassa-Assistent: Befund (T-137)

Befund zu [ADR 0104](../entscheidungen/0104-gefuehrte-ersteinrichtung.md), Entscheidung 4 (Kassa mit eigenem Assistenten, Auftraggeber, 27.11.2026). Stand 27.11.2026, Stand des Codes auf `dev` (Commit `8b630edf`). Gelesen wurde nur; nichts gebaut, nichts gestartet, nichts getestet. Rechtliche Aussagen geben wieder, was Code und ADRs sagen, keine Rechtsauskunft.

## Empfehlung

| Punkt | Empfehlung |
| --- | --- |
| Reihenfolge | Zuerst die Lücke im Bestand schließen: **Es gibt heute keine Maske, die eine Kassa anlegt** (Punkt 1). Ohne sie hat der Assistent nichts zu speichern. |
| Schnitt | Ein Assistent mit gemeinsamem Anfang (Land, Kassa-ID, Belegkopf) und einem Zweig je Land. Der Zweig bestimmt nur die Schritte nach dem Anlegen. |
| Ehrlicher Stand | Solange nur die Test-Signatur (AT) bzw. keine TSE (DE) vorhanden ist, darf der Assistent „fertig“ nur als „Übungskassa“ melden, nie als „bereit für den Echtbetrieb“ (Punkte 2 und 3). |
| API | Eine Bereitschaftsprüfung für die Kassa fehlt und ist die Voraussetzung (Punkt 4). „Es gibt eine Kassa“ reicht nicht. |
| Vorab entscheiden | Welche Signatureinrichtung (AT) und welche TSE (DE) kommt, und ob Österreich ohne sie als „Übungskassa“ ausgeliefert wird (Abschnitt „Offene Entscheidungen“). |

## 1. Was gehört heute zur Einrichtung, was ist Pflicht

**Befund.**

| Bestandteil | Wo heute | Pflicht, bevor ein Beleg gebucht wird? |
| --- | --- | --- |
| Kassa anlegen: Standort, Kassa-ID, Land, Belegkopf, Zahlungsempfänger, IBAN, BIC | nur API `POST /api/pos/registers` (Eingabe `CashRegisterInput`); **keine Maske in der Windows-App** | ja: ohne Kassa kein Beleg |
| Kassa-ID | Feld `Code` | Pflicht: Buchstaben, Ziffern, Bindestrich, höchstens 50 Zeichen; eindeutig |
| Belegkopf | Feld `ReceiptHeader`, freier Text | Pflicht |
| Zahlungsempfänger, IBAN, BIC | optional; IBAN wird geprüft; nur für die Sofortüberweisung per QR-Code | nein |
| Land | `FiscalCountry` AT, DE, CH je Kassa (nicht aus dem Spa abgeleitet) | Pflicht; bestimmt die Signatur |
| Steuersätze | fest je Land in `TaxRates`, nicht einstellbar; je Behandlung und Artikel wählbar über `TaxCategory`, Vorgabe Normalsatz | nein, Vorgabe genügt |
| Erlöskonten | drei feste Konten (4000 Behandlungen, 4100 Produkte, 3600 Mehrzweckgutscheine) gibt es immer; weitere unter „Buchhaltung“ | nein |
| Zahlungsarten | folgen aus dem Land der Kassa (`PaymentMethods.Available`), nicht einstellbar; Digitaler Euro nur mit Schalter | nein |
| Bondrucker | nur in den Einstellungen der App („Geräte“, Adresse und Breite), nicht in der API | nein: ohne Drucker „passiert nichts“ |
| Kartenterminal | Geräte im Netz, nur in der App | nein |
| Rabattgrenze, Barzahlungsrabatt | Maske „Verwaltung › Kassa“ (`PosSettingsView`), `PUT /api/pos/settings` | nein, Vorgaben 20 % und aus |
| Signatur (RKSV, TSE) | hinter `IFiscalization` je Land, keine Eingabe in der App | ja, aber nur als Zustand der Installation (Punkte 2 und 3) |

Was beim Anlegen fehlt, meldet `CreateRegisterAsync` im Wortlaut:

| Fehlt | Meldung |
| --- | --- |
| Kassa-ID leer oder ungültig | „Kassen-ID aus Buchstaben, Ziffern und Bindestrich, höchstens 50 Zeichen, z. B. „WIEN-1“.“ |
| Belegkopf leer | „Belegkopf mit Firmenname und Anschrift ist Pflicht.“ |
| IBAN ungültig | „Keine gültige IBAN.“ |
| Kassa-ID doppelt | „Diese Kassen-ID gibt es schon.“ |
| Signatureinrichtung ausgefallen | „Die Signatureinrichtung ist nicht erreichbar; der Startbeleg muss signiert sein.“ |
| DE ohne TSE | „Für Deutschland ist noch keine TSE angebunden. Bitte eine Cloud-TSE oder USB-TSE einrichten.“ |

Beim Kassieren: ohne Kassa zeigt `PosViewModel` „An diesem Standort ist noch keine Kassa eingerichtet. Kassen legt die Standortleitung unter „Verwaltung“ → „Kassa“ an.“; die Maske „Kassa“ kann das aber nicht. Eine außer Betrieb genommene Kassa antwortet „Die Kassa ist außer Betrieb.“; eine Kassa eines anderen PCs „Diese Kassa kassiert an einer anderen Stelle (an einem anderen PC). Hier nur ansehen; zum Kassieren hier eine eigene Kassa anlegen.“

**Beleg.**

- `src/AispaStudio.Api/Pos/PosService.cs:33-104` (`CreateRegisterAsync`; Meldungen Zeile 41, 46, 52, 57, 90), `:2131-2137` (`EnsureActive`), `:2112-2113` (andere Stelle).
- `src/AispaStudio.Contracts/PosContracts.cs:119-126` (`CashRegisterInput`).
- `src/AispaStudio.Api/Pos/PosEndpoints.cs:150-163` (Endpunkt, Recht `ManageLocation`), `:441-473` (Einstellungen).
- `src/AispaStudio.Api/Pos/TaxRates.cs` (Sätze), `Pos/PaymentMethods.cs:39-43`, `Pos/PosModel.cs:12-31` (Konten).
- `src/AispaStudio.Desktop/ViewModels/AdminPageViewModel.cs:51` und `:274` (Reiter „Kassa“ ist `PosSettingsViewModel`); `Views/PosSettingsView.axaml` hat nur die Abschnitte Rabatte und Barzahlungsrabatt.
- Suche nach `api/pos/registers` in `src/AispaStudio.Desktop` und `src/AispaStudio.Client`: nur Lesezugriffe auf die Liste (`PosViewModel.cs:584`, `ShellViewModel.cs:776`); einen `POST` auf `api/pos/registers` ohne Unterpfad gibt es dort nicht. Die übrigen Aufrufe mit `api/pos/registers/{id}/…` betreffen Belege, Kassenlade, Tagesabschluss, Ersatzbelege und TSE-Export (z. B. `ComplianceReportViewModel.cs:84`), nicht das Anlegen. Im Quelltext legt nur `PosDemoData.cs:77` Kassen an (Beispieldaten, fest AT).
- Kassa-Meldungen: `PosViewModel.cs:615` und `:621`.
- Bondrucker: `src/AispaStudio.Desktop/Services/Settings.cs:15-16`, `:33-34`; `PosViewModel.cs:273`, `:1527` (Methode), `:1538` (Abruf als ESC/POS).
- Es gibt in der Windows-App auch keinen Aufruf von `PUT /api/pos/treatments/{id}/settings` oder `POST /api/pos/articles` (Suche nach `pos/treatments` und `api/pos/articles`: nur Lesezugriff `LocationsViewModel.cs:280`). Steuersatz und Konto einer Behandlung sind dort also nicht änderbar; es gilt die Vorgabe (`PosService.cs`, Vorgabe `TaxCategory.Normal` und Konto 4000).

**Folge für den Assistenten.**

1. Der Schritt „Kassa einrichten“ in `SetupProgress.cs:53` springt in eine Maske, die keine Kassa anlegt. Das ist ein bestehender Fehler (nicht Teil dieses Auftrags, hier nur gemeldet): Wer in einem leeren Betrieb auf „Öffnen“ klickt, findet kein Feld.
2. Der Assistent muss `POST /api/pos/registers` selbst anbieten. Er ist damit auch die erste Maske, die das kann.
3. Steuersatz je Behandlung, Artikel und Erlöskonten gehören nicht in den Assistenten: Die Vorgabe genügt, und es gibt keine Maske dafür. Das ist ein eigener Punkt außerhalb des Assistenten.

## 2. Österreich: RKSV

**Befund.**

| Schritt nach RKSV | Was der Code macht | Wer macht es |
| --- | --- | --- |
| Signaturerstellungseinheit beschaffen | `IRksvSignatureDevice` ist die Schnittstelle (Signaturkarte, HSM, Cloud). Registriert ist **nur** `TestSignatureDevice` (Kennung `AT0`): ECDSA-Schlüssel und selbst ausgestelltes Zertifikat je Kassa in der Datenbank | die Person außerhalb der App (Anbieter wählen, Vertrag); im Code fehlt die zertifizierte Einrichtung ganz |
| Schlüssel für den Umsatzzähler (AES-256) | `InitializeAsync` erzeugt ihn beim Anlegen und speichert ihn in der Kassa (`TurnoverKey`) | App; **kein Weg, ihn der Person zu zeigen oder auszugeben** (Suche: `TurnoverKey` kommt nur in Erzeugung, Nutzung und Entity vor) |
| Startbeleg | `CreateRegisterAsync` bucht ihn im selben Schritt (Betrag 0, Art `Start`) und bricht ohne Signatur ab | App |
| Registrierung von Kassa und Signatureinrichtung in FinanzOnline | kein Code. Nur Hinweise: der Text der Kassa-Warnung (nur solange Test-Signatur) und die Pflichtenliste im Bericht | Person, außerhalb; die App zeigt keinen Nachweis und merkt sich nichts |
| Prüfung des Startbelegs mit der BMF-Belegcheck-App | kein Code; nur Hinweis. Der QR-Code steht auf dem Beleg, ein Nullbeleg lässt sich auf Wunsch erzeugen (`POST /api/pos/registers/{id}/null-receipt`) | Person, außerhalb |
| Monatsbeleg, Jahresbeleg | automatisch: der erste Beleg eines neuen Monats bzw. Jahres löst ihn aus (`IssueCoreAsync`). Der Bericht erinnert bis 15. Februar an die Prüfung des Jahresbelegs | App bucht, Person druckt, bewahrt auf (7 Jahre) und prüft |
| Ausfall der Signatur | Belege mit Hinweis, Nullbeleg nach Wiederkehr; Warnung ab 48 Stunden „bitte unverzüglich über FinanzOnline melden“ | App warnt, Person meldet |
| Datenerfassungsprotokoll (DEP) | `GET /api/pos/registers/{id}/dep` | App liefert, Person sichert (Hinweis: mindestens vierteljährlich extern) |
| Außerbetriebnahme | `POST /api/pos/registers/{id}/decommission` (Schlussbeleg); Hinweis „Danach in FinanzOnline abmelden.“ | App und Person |

**Heute nur Test.** Die Test-Signatur trägt die Kennung `AT0`. Die Kassa-Warnung sagt es selbst: „Test-Signatureinrichtung: nicht für den Echtbetrieb. Vor dem Start eine zertifizierte Signatureinrichtung anbinden, Kassa und Signatureinrichtung in FinanzOnline registrieren und den Startbeleg mit der BMF-Belegcheck-App prüfen.“ Registriert ist sie fest: `Program.cs` hängt `TestSignatureDevice` ohne Schalter ein, ein anderer Ersatz fehlt. Die Anleitung zum Einzelplatz warnt entsprechend: „Kassa nur mit Test-Signatur. Nicht für den Alltag eines Spas verwenden.“

**Beleg.**

- `src/AispaStudio.Api/Pos/Fiscal/Rksv/SignatureDevice.cs:13-27` (Schnittstelle), `:43-90` (Test-Einrichtung, `ZdaId` Zeile 45); `Rksv/RksvFiscalization.cs:15-18` (Hinweis), `:20-25` (`InitializeAsync`); `Program.cs:285`.
- `PosService.cs:86-91` (Startbeleg), `:1984-2043` (Monats- und Jahresbeleg, Nullbeleg), `:112-146` (Warnungen).
- `Reporting/ManagementReports.cs:540-550` (Pflichten AT), `:565-568` (Erinnerung 15. Februar); `PosEndpoints.cs:210-231` (DEP), `:165-174` (Nullbeleg, Außerbetriebnahme).
- `docs/installation/standalone.md:10`; ADR 0041, Abschnitt 5 (Signaturkarte im Kartenleser oder Cloud-Signatur, Startbeleg, Monats- und Jahresbeleg wie im Server-Betrieb).
- `docs/stand-und-offene-punkte.md:384` (zertifizierte Einrichtung, Registrierung bei FinanzOnline und Prüfung des Startbelegs als offen).

**Folge für den Assistenten.** Drei der fünf Schritte nach RKSV liegen außerhalb der App (Beschaffen, FinanzOnline, Belegcheck) und können nur als Anleitung mit Bestätigung durch die Person erscheinen. Ob die App ihre Bestätigung speichert, ist eine offene Entscheidung (Frage 3). Die App kann nichts davon prüfen: FinanzOnline hat keine Anbindung im Code.

## 3. Deutschland und Schweiz

**Deutschland (TSE, § 146a AO).**

| Bestandteil | Stand im Code |
| --- | --- |
| Schnittstelle `ITseClient` (Registrierung, Signatur je Beleg, Schlüssel, TSE-Export), Belegformat und QR-Code nach DSFinV-K | vorhanden |
| DSFinV-K-Export (ZIP) und TSE-Export (TAR über die Queue) | vorhanden (`GET …/dsfinvk`, `POST/GET …/tse-exports`); Prüfung mit dem Prüfprogramm der Finanzverwaltung offen |
| Zertifizierte TSE (Cloud oder USB) | **fehlt**; es gibt nur `TestTseClient` |
| Meldung der Kassa nach § 146a Abs. 4 AO über ELSTER | kein Code; nur Hinweistext („innerhalb eines Monats nach Anschaffung“) und Pflichtenliste |
| Test-TSE im Einzelplatz | **nicht wirksam**: `Fiscal:TestTse` steht nur in `appsettings.Development.json`; `LocalServer` startet die API mit `ASPNETCORE_ENVIRONMENT=Production`. Eine deutsche Kassa lässt sich im Einzelplatz deshalb nicht anlegen (Meldung „Für Deutschland ist noch keine TSE angebunden …“) |

**Schweiz.** Keine Signaturpflicht. `SwissFiscalization` nummeriert lückenlos und verkettet per SHA-256, ohne Zertifikat und ohne Gerät; `InitializeAsync` und `IsDeviceAvailableAsync` tun nichts. Die Kassa lässt sich ohne weitere Voraussetzung anlegen. Offen laut ADR 0010 und Stand-Liste: Rappenrundung (CH-Rundung), Aufbewahrung und MWST-Abrechnung mit echtem Mandanten prüfen. Der Pflichtenbericht nennt: Belege und Journal 10 Jahre aufbewahren (GeBüV). Die Steuersätze der Schweiz stehen im Code (8,1, 2,6, 3,8, 0).

**Beleg.**

- `Pos/Fiscal/KassenSichVFiscalization.cs:10-27` (Schnittstelle), `:46-48` (Hinweise), `:59` (Fehler ohne TSE); `Pos/Fiscal/TestTse.cs:36-40`; `Pos/Fiscal/Dsfinvk.cs`, `Pos/Fiscal/TseExports.cs`.
- `Program.cs:287-291` (Schalter); `appsettings.Development.json:26-29`; `src/AispaStudio.Desktop/Services/LocalServer.cs:632`.
- `Pos/Fiscal/SwissFiscalization.cs:8-33`; `Reporting/ManagementReports.cs:551-559` (Pflichten DE, CH).
- ADR 0035 (Test-TSE, Folgen: Vertrag und Adapter, ELSTER-Meldung offen), ADR 0041 Abschnitt 5 (lokale TSE im Einzelplatz empfohlen, Cloud-TSE braucht Internet).
- `docs/stand-und-offene-punkte.md:122` und `:384`.

**Folge für den Assistenten.** Für Deutschland kann der Assistent heute im Einzelplatz nicht fertig werden: Ohne TSE scheitert schon das Anlegen. Für die Schweiz ist der Weg kurz (nur Anlegen, kein Signaturschritt). Für Österreich und Deutschland steht die Echtbetriebsfähigkeit an einer Entscheidung des Auftraggebers über Anbieter und Zertifizierung (Frage 1).

## 4. API: Endpunkte und Bereitschaft

**Befund.**

| Zweck | Endpunkt | Recht |
| --- | --- | --- |
| Kassen listen, eine lesen | `GET /api/pos/registers`, `GET /api/pos/registers/{id}` | `Book` (alternativ `ViewReceipts`) |
| Kassa anlegen (inkl. Startbeleg) | `POST /api/pos/registers` | `ManageLocation` |
| Nullbeleg | `POST /api/pos/registers/{id}/null-receipt` | `Book` |
| Außer Betrieb nehmen | `POST /api/pos/registers/{id}/decommission` | `ManageLocation` |
| DEP (AT) | `GET /api/pos/registers/{id}/dep` | `ViewAmountReports` |
| DSFinV-K (DE) | `GET /api/pos/registers/{id}/dsfinvk?from&until` | `ViewAmountReports` |
| TSE-Export (DE) | `POST` und `GET /api/pos/registers/{id}/tse-exports`, `GET /api/pos/tse-exports/{id}/file` | `ViewAmountReports` |
| Pflichtenbericht je Standort | `ComplianceAsync` (Bericht „Pflichtberichte“) | `ViewAmountReports` |
| Einstellungen (Rabatt) | `GET`/`PUT /api/pos/settings` | Lesen `Book`, Schreiben `ManageOrganization` |
| Erlöskonten | `GET`/`POST /api/pos/revenue-accounts` | Lesen `Book`, Schreiben `ManageCatalog` |
| Steuersatz und Konto einer Behandlung | `GET`/`PUT /api/pos/treatments/{id}/settings` | Schreiben `ManageCatalog` |
| Schritte der Ersteinrichtung | `GET /api/setup/progress` (mit `readiness=true`) | `ManageOrganization` |

Ein Recht, das nur zum Anlegen der Kassa berechtigt, ist `ManageLocation`; der Reiter „Kassa“ der Verwaltung verlangt dagegen `ManageOrganization`. Wer den Assistenten öffnen darf, ist damit nicht eindeutig (Frage 5).

**Bereitschaftsprüfung.** Für die Kassa gibt es keine. `SetupReadiness` rechnet nur die Buchbarkeit (Standort, Öffnungszeiten, Räume, Behandlungen, Team); `AnyRegister` ist `db.CashRegisters.AnyAsync(r => r.IsActive)` und zählt auch Kassen anderer Stellen. Der Schritt „Kassa einrichten“ gilt damit als erledigt, sobald irgendeine aktive Kassa existiert.

**Reicht das? Nein.** Eine Kassa mit Test-Signatur oder ohne Eintrag in FinanzOnline gilt heute als „erledigt“; eine Kassa, deren Signatur ausgefallen ist, ebenso. Ein Schritt „erledigt“ bedeutet für die Kassa mehr als für die anderen: Er behauptet, dass sich rechtmäßig kassieren lässt. Der Zustand, den die API schon kennt (Test-Einrichtung, Ausfall, außer Betrieb, andere Stelle, Hinweistexte aus `IFiscalization.Notice`), steht bisher nur in `CashRegisterDto.Warnings`.

**Beleg.**

- `Pos/PosEndpoints.cs:131-173`, `:210-272`, `:441-473`, `:35-61`, `:93-127`; Rechte in den jeweiligen `RequirePermission`/`AlsoAllow`.
- `Scheduling/SetupProgress.cs:53` (Schritt), `:15-19` (Endpunkt, Recht); `Scheduling/SetupReadiness.cs:30`, `:61` (`AnyRegister`).
- `Pos/PosService.cs:112-150` (`ToDto` mit Warnungen); `Contracts/PosContracts.cs:133-145` (`CashRegisterDto`).

**Folge für den Assistenten.** Es braucht eine Bereitschaftsprüfung der Kassa in der API (Vorschlag in Punkt 6), damit die App nur anzeigt (ADR 0104, Entscheidung 6). Sie ist als eigener Satz „fertig“ oder „Grund“ gedacht, wie bei den anderen Schritten, und trennt zwei Aussagen: „Kassa ist angelegt und kann Belege bilden“ und „Kassa ist für den Echtbetrieb zugelassen“. Das Zweite kann die App nur über Angaben der Person wissen (Frage 3).

## 5. Einschlägige ADRs

| ADR | Was sie für den Assistenten vorgeben |
| --- | --- |
| [0010](../entscheidungen/0010-kassa-compliance-by-design.md) | Jede Buchung signiert, kein Löschen, nur Storno; getrennte Bereiche nur über eigene, versteuerte Erlöskonten; **nie eine Funktion, die Umsätze verbirgt**. Der Assistent darf also keinen Schritt „ohne Signatur kassieren“ und keine Abkürzung am Startbeleg bieten. Ein Erlöskonto-Schritt darf nur versteuerte Konten anlegen. |
| [0035](../entscheidungen/0035-test-tse-und-dsfinvk.md) | Die Test-TSE ist nur für Entwicklung und Vorführung; die Kasse warnt dauerhaft „Nur Test-TSE angebunden“. Die echte TSE ersetzt nur `ITseClient`. Der Assistent darf einen Test-Zustand nie als Echtbetrieb ausgeben. |
| [0041](../entscheidungen/0041-einzelplatz-standalone-und-zuschaltbare-webmodule.md) | Jede Kassa gehört genau einer Stelle; keine Nachsignatur an anderer Stelle. Im Einzelplatz AT: Signaturkarte im Kartenleser oder Cloud-Signatur; DE: lokale TSE empfohlen. Die Kassa gehört zum Kern, nicht zu einem Modul. |
| [0096](../entscheidungen/0096-kassieren-ohne-verbindung-ersatzbeleg-und-nacherfassung.md) | Ersatzbeleg und Nacherfassung nach Ausfall; Meldung über FinanzOnline bei mehr als 48 Stunden. Für den Assistenten nur als Hinweis auf „Wenn etwas schiefgeht“. |
| [0104](../entscheidungen/0104-gefuehrte-ersteinrichtung.md) | Der Assistent schreibt über dieselben Endpunkte wie die Masken (Entscheidung 5), die API entscheidet, was fertig ist (6), nichts sperrt (7), jeder Schritt lässt sich überspringen. „Belege bleiben in jedem Fall bei der Kassa“. Überspringen darf nie dazu führen, dass ohne Kassa Umsatz erfasst wird: ohne Kassa gibt es keinen Beleg (Punkt 1). |

**Pflichtzahlen.** `CLAUDE.md` des Repositorys: Pflichtzahlen (Kasse, Steuer, Berichte für Behörden) rechnet nie die KI. Der Assistent besteht aus festen Schritten und Texten; die KI-Eingabe aus ADR 0104 Option C ist für die Kassa **nicht** vorzusehen (kein Vorschlag von Steuersätzen, Belegkopf oder Signaturdaten durch die KI). Der Belegkopf darf aus den Firmendaten vorbelegt werden (feste Zusammenstellung, keine KI).

**Beleg.** Wortlaut der ADRs; `CLAUDE.md` Abschnitt „Regeln im Code“; ADR 0104 Zeile „Belege bleiben in jedem Fall bei der Kassa“.

## 6. Vorschlag: Schritte des Kassa-Assistenten

Der Assistent heißt „Kassa einrichten“ und folgt ADR 0104: Weiter und Zurück, jeder Schritt überspringbar, Stand sind die Daten selbst.

### Gemeinsamer Anfang (alle Länder)

| Schritt | Die Person gibt ein | Speichert | „Fertig“ erkennbar an |
| --- | --- | --- | --- |
| 1. Land und Standort | Land der Kassa (Vorschlag aus dem Land des Spas bei „Spa einrichten“), Standort | noch nichts | Land gewählt (nur Anzeige; die Wahl ist danach nicht mehr änderbar, weil Land die Signatur bestimmt) |
| 2. Kassa-ID und Belegkopf | Kassa-ID (Vorschlag aus dem Standort, z. B. „WIEN-1“), Belegkopf (Vorschlag aus Firmenname, Anschrift, UID der Firmendaten) | noch nichts | Eingaben gültig (gleiche Regeln wie die API, Meldungen unverändert) |
| 3. Bankverbindung (freiwillig) | Zahlungsempfänger, IBAN, BIC für die Sofortüberweisung | noch nichts | IBAN gültig oder leer |

### Zweig Österreich

| Schritt | Die Person gibt ein / tut | Speichert | „Fertig“ erkennbar an |
| --- | --- | --- | --- |
| 4a. Signatureinrichtung | liest, was nötig ist (Anbieter, Karte oder Cloud); wählt bei Test „Übungskassa“ | nichts oder Wahl der Einrichtung (fehlt heute, Paket 3) | API meldet die Einrichtung (`ZdaId` ungleich `AT0`) oder „Übungskassa“ bewusst gewählt |
| 4b. Kassa anlegen und Startbeleg | bestätigt | `POST /api/pos/registers` (erzeugt Schlüssel und Startbeleg) | Kassa vorhanden, Startbeleg gebucht (`Start` in den Belegen) |
| 4c. FinanzOnline | meldet Kassa und Signatureinrichtung selbst an; die App zeigt Schlüssel, Kassa-ID und Zertifikatsnummer zum Übertragen (fehlt heute, Paket 3) | Bestätigung der Person (Frage 3) | Bestätigt |
| 4d. Startbeleg prüfen | druckt den Startbeleg, prüft ihn mit der BMF-Belegcheck-App; bei Bedarf Nullbeleg | `POST …/null-receipt` nur auf Wunsch | Bestätigt |
| 5. Bondrucker und Kartenterminal (freiwillig) | Adresse des Druckers, Probebeleg | Einstellung der App | Probedruck ohne Fehler |

### Zweig Deutschland

| Schritt | Die Person gibt ein / tut | Speichert | „Fertig“ erkennbar an |
| --- | --- | --- | --- |
| 4a. TSE | wählt Cloud-TSE oder USB-TSE und hinterlegt die Zugangsdaten des Anbieters (fehlt heute: keine Maske, keine Ablage von Zugangsdaten, kein Adapter, Paket 3) | Einstellung der TSE | API meldet eine TSE ohne Testkennzeichen (`ITseClient.IsTest` falsch) |
| 4b. Kassa anlegen | bestätigt | `POST /api/pos/registers` | Kassa vorhanden, Startbeleg gebucht |
| 4c. Meldung an das Finanzamt | meldet Kasse und TSE innerhalb eines Monats über ELSTER (§ 146a Abs. 4 AO); die App zeigt Kassa-ID und TSE-Seriennummer | Bestätigung der Person (Frage 3) | Bestätigt |
| 5. Bondrucker und Kartenterminal | wie Österreich | wie Österreich | wie Österreich |

### Zweig Schweiz

| Schritt | Die Person gibt ein / tut | Speichert | „Fertig“ erkennbar an |
| --- | --- | --- | --- |
| 4. Kassa anlegen | bestätigt; die App sagt, dass keine Signatur nötig ist und dass Aufbewahrung (10 Jahre) und MWST-Abrechnung Sache der Person bleiben | `POST /api/pos/registers` | Kassa vorhanden, Startbeleg gebucht |
| 5. Bondrucker und Kartenterminal | wie Österreich | wie Österreich | wie Österreich |

### Abschluss (alle Länder)

Ein Probeverkauf ist **kein** Schritt: Er würde einen echten, nicht löschbaren Beleg erzeugen (ADR 0010, kein Löschen). Stattdessen: Hinweis auf den Nullbeleg und auf das Handbuch (Bezahlen, Tagesabschluss, Monats- und Jahresbeleg, Pflichtenbericht). Der Assistent nennt zum Schluss, was die Person künftig selbst tun muss (AT: Jahresbeleg bis 15. Februar prüfen, DEP sichern; DE: DSFinV-K bereithalten; CH: Journal aufbewahren), so wie der Pflichtenbericht es heute tut.

### Was in der API fehlt

| Lücke | Vorschlag |
| --- | --- |
| Bereitschaftsprüfung der Kassa | Antwort je Kassa mit Land, Stand der Signatur (angelegt, Test, zertifiziert, ausgefallen), Startbeleg vorhanden, außer Betrieb, andere Stelle, und einem fertigen Satz „Grund“; in `GET /api/setup/progress` einhängen, Schritt „register“ wird erst bei „kann Belege bilden“ erledigt. Neue Felder nur ergänzen (ADR 0085). |
| Test- oder echte Signatur als Merkmal | `CashRegisterDto` kennt es nur als Text in `Warnings`; ein eigenes Feld (z. B. `IsTestSetup`) vermeidet, dass die App Texte auswertet. |
| Zertifizierte Einrichtung AT | Umsetzung von `IRksvSignatureDevice` für den gewählten Anbieter, Schalter statt fester Registrierung. |
| Zertifizierte TSE DE | Umsetzung von `ITseClient`; im Einzelplatz ein Weg zur Zulassung der Test-TSE für „Übungskassa“ (`LocalServer` setzt `Production`). |
| Schlüssel und Zertifikat zum Übertragen | AT: Endpunkt, der Kassa-ID, Schlüssel (AES) und Zertifikatsseriennummer für die Anmeldung in FinanzOnline liefert (heute nicht abrufbar); Recht `ManageLocation`; der Schlüssel erscheint nur einmal bzw. nur für diese Aufgabe. |
| Bestätigungen der Person | Merker je Kassa „in FinanzOnline angemeldet“, „Startbeleg geprüft“, „bei ELSTER gemeldet“ mit Datum (nur falls Frage 3 mit Ja beantwortet wird). |
| Recht für den Assistenten | Entscheidung nötig (Frage 5); heute `ManageLocation` zum Anlegen und `ManageOrganization` für den Reiter. |
| Kassa-Maske in der Verwaltung | Kassen anzeigen, Kassa anlegen ohne Assistent (der volle Weg, ADR 0104 Entscheidung 7), außer Betrieb nehmen. Heute fehlt das ganz. |
| Steuersatz je Behandlung | Nicht Teil des Assistenten (Punkt 1); eigener Befund, falls gewünscht. |

### Grober Schnitt in Pakete

| Paket | Inhalt | Dauer | Modell |
| --- | --- | --- | --- |
| P1 | API: Bereitschaftsprüfung der Kassa, Feld für Test- oder echte Einrichtung, Schritt „register“ erst bei „kann Belege bilden“; Tests für Erlaubtes und Abgelehntes | 40 Minuten | Sonnet |
| P2 | Maske „Kassa“ in der Verwaltung: Liste der Kassen, Kassa anlegen, Hinweise; der volle Weg ohne Assistent | 45 Minuten | Sonnet |
| P3 | Assistent: gemeinsamer Anfang und Zweig Schweiz (der kürzeste, ohne Zulassung) | 45 Minuten | Sonnet |
| P4 | Assistent: Zweig Österreich mit Anleitungsschritten (FinanzOnline, Belegcheck), Bestätigung der Person, Anzeige von Schlüssel und Zertifikat | 45 Minuten | Sonnet; Opus nur für die Texte, wenn der Auftraggeber rechtlich geprüfte Wortlaute verlangt |
| P5 | Assistent: Zweig Deutschland (TSE, ELSTER-Anleitung), Test-TSE im Einzelplatz für die Übungskassa | 45 Minuten | Sonnet |
| P6 | Bondrucker und Kartenterminal als Schritt; Abschluss; Handbuch und Bilder | 40 Minuten | Sonnet (Haiku für das Bildersammeln) |
| P7 (getrennt, groß) | Zertifizierte Signatureinrichtung AT und TSE DE: Anbieterwahl, Adapter, Abnahme (kein Paket von 45 Minuten; erst nach Frage 1 zu schneiden) | offen | Opus für den Plan, Sonnet für die Umsetzung |

Reihenfolge: P1 vor P3 bis P5, P2 vor oder neben P3 (keine gemeinsamen Dateien außer der Verwaltung; ein Schreibender je Datei). P7 hängt nicht von den übrigen ab, entscheidet aber, ob der Assistent je „Echtbetrieb“ melden darf.

## Offene Entscheidungen des Auftraggebers

| Nr. | Frage | Empfehlung |
| --- | --- | --- |
| 1 | Welche zertifizierte Signatureinrichtung (AT) und welche TSE (DE) soll die App anbinden, und bis wann? Ohne sie bleibt jede Kassa eine Übungskassa. | Zuerst die Wahl treffen, bevor P4 und P5 enden; der Assistent bleibt bis dahin ehrlich („Übungskassa“). Eine Cloud-Lösung ist im Einzelplatz einfacher einzurichten, braucht aber Internet (ADR 0041, Abschnitt 5). |
| 2 | Darf der Einzelplatz eine „Übungskassa“ anlegen (AT mit Test-Signatur, DE mit Test-TSE), die sichtbar nicht für den Alltag ist? | Ja, mit der bestehenden Dauerwarnung und einem Schritt, der die Person ausdrücklich bestätigen lässt; ohne das lässt sich der Assistent weder zeigen noch testen. |
| 3 | Soll die App die Bestätigungen der Person speichern („in FinanzOnline angemeldet“, „Startbeleg geprüft“, „bei ELSTER gemeldet“)? | Ja, mit Datum, als Erinnerung und Nachweis für die Person, nie als Bescheinigung. Die App kann es nicht prüfen und sagt das dort. |
| 4 | Darf der Assistent die Schritte außerhalb der App (FinanzOnline, ELSTER, Belegcheck) als Anleitung mit Link zeigen, und wer prüft die Wortlaute rechtlich? | Ja; die Texte stützen sich auf Code und ADRs. ADR 0010 verlangt für die Gutschein-Fristen eine anwaltliche Prüfung vor dem Einsatz; Entsprechendes für diese Texte ist ratsam. |
| 5 | Welches Recht öffnet den Assistenten: `ManageLocation` (wie das Anlegen) oder `ManageOrganization` (wie der Reiter „Kassa“)? | `ManageLocation` für das Anlegen, weil die API es so verlangt; der Reiter braucht eine Ausnahme oder ein zweites Recht. |
| 6 | Bleibt „Schritt erledigt“ gleich bedeutend mit „Kassa vorhanden“, oder verlangt die Karte „Betrieb einrichten“ künftig „kann Belege bilden“? | Letzteres, mit sichtbarem Unterschied zu „zugelassen für den Echtbetrieb“. |
| 7 | Gehört die Kassa weiter in die Karte „Betrieb einrichten“ (Schritt 6 von 6) oder erst nach dem Probetermin als eigene Karte? | Eigene Karte „Kassa einrichten“ nach dem Betrieb, damit ein Spa ohne Kassa (nur Kalender) die erste Karte schließen kann. ADR 0104 sagt, die Kassa bekommt „danach“ einen eigenen Assistenten. |

## Nachtrag vom 27.11.2026: Anbieter, FinanzOnline, Jahresende

Auftrag des Auftraggebers vom 27.11.2026: die ersten drei Punkte aus „Nicht geklärt“ klären. Recherche im Netz, Abruf aller Quellen am 27.11.2026. Die Quellen wurden gelesen, nicht nachgerechnet; die Paragrafen der RKSV lagen nur als Zusammenfassung vor, Erlass und Handbuch als PDF. Was „nicht belegt“ heißt, ist offen. Vor einem Handbuchtext den Wortlaut gegen das RIS prüfen.

### Quellen

| Kürzel | Quelle |
| --- | --- |
| RKSV | [RIS, geltende Fassung](https://www.ris.bka.gv.at/GeltendeFassung.wxe?Abfrage=Bundesnormen&Gesetzesnummer=20009390) |
| Erlass | [BMF-Erlass zur Einzelaufzeichnungs-, Registrierkassen- und Belegerteilungspflicht](https://findok.bmf.gv.at/findok/iwg/84/84454/84454.1.pdf), GZ 2026-0.531.449 vom 23.06.2026 |
| Handbuch | [BMF, „Registrierkassen in FinanzOnline“](https://finanzonline.bmf.gv.at/eLearning/BMF_Handbuch_Registrierkassen.pdf), Stand 07.01.2021 |
| Webservice | [BMF, „Registrierkassen-Webservice“](https://www.bmf.gv.at/dam/jcr:19c193f4-99cd-42ff-9b23-655f2ab5734e/BMF_Registrierkassen_Webservice.pdf) |
| Prüfwert | [BMF, „Prüfwertberechnung AES Schlüssel“](https://www.bmf.gv.at/dam/jcr:c2e5b691-af1b-4bbe-90ae-66887c811818/AES_Pruefsummenberechnung_200716_3.pdf) |
| BSI | [BSI, Liste der zertifizierten technischen Sicherheitseinrichtungen](https://www.bsi.bund.de/DE/Themen/Unternehmen-und-Organisationen/Standards-und-Zertifizierung/Zertifizierung-und-Anerkennung/Listen/Zertifizierte-Produkte-nach-TR/Technische_Sicherheitseinrichtungen/TSE_node.html) |

### Anbieter in Österreich (Signatureinrichtung)

Die Webservice-Beschreibung des BMF nennt drei Anbieter mit Kennung: AT1 A-Trust, AT2 GLOBALTRUST, AT3 PrimeSign. Weitere wurden nicht gefunden; Wiederverkäufer wie fiskaly SIGN AT sind nicht geprüft.

| | A-Trust (AT1) | GLOBALTRUST (AT2) | PrimeSign (AT3) |
| --- | --- | --- | --- |
| Karte, signiert ohne Internet | a.sign RK CHIP | RKS CARD mit Kartenleser | RKSV Smart Card |
| Cloud, braucht Internet | a.sign RK HSM (Basic, Advanced, Premium) | RKS CLOUD | RKSV Remote Signing Account |
| Schnittstelle der Cloud | [Doku](https://docs.a-trust.at/apis/RKHSM/) mit Abnahmesystem | SOAP (WSDL), Beispielcode in Java und C# | REST, öffentliche Anleitungen, Testsystem auf Anfrage |
| Preis ohne USt | nicht öffentlich (Partner, Webshop) | Karte mit Leser 99 EUR für 3 Jahre oder 149 EUR für 5 Jahre; Cloud unter 0,5 Cent je Signatur | Cloud ab 196 EUR im Jahr; Karte nicht genannt |
| Quelle | [Portfolio](https://a-trust.at/en/products/cash_register/rksv_portfolio) | [Produktseite](https://globaltrust.eu/en/rks/), [Fragen und Antworten](https://globaltrust.eu/faq-globaltrust-rksv/) | [Produktseite](https://primesign.cryptas.com/de/primesign-rksv) |

- **Kartentausch:** Ältere Kartentypen sind laut [BMF](https://www.bmf.gv.at/public/informationen/ablauf-bescheinigung-rksv-signaturkarten.html) nicht mehr normgerecht und bis Mai 2027 zu tauschen. Cloud-Lösungen sind laut [WKO](https://www.wko.at/oe/steuern/ablauf-bescheinigung-rksv-signaturkarten) nicht betroffen.
- **Nicht belegt:**
  - wie die Karten aus .NET angesprochen werden (PC/SC, APDU),
  - ob der Softwarehersteller oder das Spa bestellt,
  - der Eintrag der Anbieter in der EU-Vertrauensliste.
  - Die Preisseiten tragen kein Datum.

### Anbieter in Deutschland (TSE)

Gültige Zertifikate laut BSI-Liste (Seiten 1 bis 3 von 4 gelesen):

| Anbieter und Produkt | Art | BSI-K-TR | Gültig bis | Preis | Ohne Internet |
| --- | --- | --- | --- | --- | --- |
| Swissbit TSE 2.0 | USB, SD, microSD | 0800-2026 | 20.04.2034 | 245 bis 269 EUR (Listenpreis eines Händlers, ohne Datum) | ja |
| Epson USB TSE, microSD TSE | USB, microSD | 0781-2025 | 29.04.2033 | 234 bis 245 EUR (Listenpreis eines Händlers, ohne Datum) | ja |
| fiskaly sign Cloud-TSE | Cloud | 0717-2025 | 30.03.2033 | nicht öffentlich | nein |
| Deutsche Fiskal, Fiskal Cloud-TSE | Cloud | 0525-2022 | 15.05.2029 | nicht öffentlich | nur Sondervariante über den Vertrieb |
| D-Trust, Web-Dienst für TSE | Cloud | 0524-2024 | 25.02.2032 | nicht öffentlich | nein |

- **Lebensdauer der Hardware:** Das Signaturzertifikat im Stück läuft bei Swissbit 5 Jahre und 6 Monate; danach ist die TSE zu tauschen.
- **Schnittstellen:**
  - fiskaly: REST mit [öffentlicher Doku](https://workspace.fiskaly.com/api/sign-de) und kostenlosem Test, kein gepflegtes SDK.
  - Swissbit und Epson: Windows-Treiber und eine C-Bibliothek.
  - Ein .NET-SDK liefert kein Anbieter.
- **Meldung ans Finanzamt (§ 146a Abs. 4 AO):** seit 01.01.2025 über Mein ELSTER oder aus der Software, binnen eines Monats nach Anschaffung oder Außerbetriebnahme; je Betriebsstätte eine Mitteilung.
- **Nicht belegt:**
  - Preise und Vertragspartner bei fiskaly, Deutsche Fiskal und D-Trust,
  - die Bedingungen der SDKs von Swissbit und Epson,
  - ein Zertifikat für A-Trust in Deutschland.

### Einschätzung für den Einzelplatz

Wer ohne dauerhaftes Internet arbeiten soll, braucht in Österreich eine Karte und in Deutschland eine Hardware-TSE. Die Cloud ist einfacher anzubinden, signiert aber ohne Leitung nicht; das ist nur über die Ausfallregel tragbar, nicht als Regelbetrieb. Am besten dokumentiert sind GLOBALTRUST und PrimeSign (Österreich) sowie fiskaly und Swissbit (Deutschland). Das ist eine Einschätzung aus den Quellen, keine Probe mit einem Gerät.

### Registrierung in FinanzOnline

- **Zwei Registrierungen** (RKSV § 16, Handbuch 3.2.1 und 3.3.1):
  - Signatureinrichtung: Art der Einheit, Vertrauensdiensteanbieter, Seriennummer des Zertifikats.
  - Kassa: Kassenidentifikationsnummer, Benutzerschlüssel AES-256, Prüfwert (4 Zeichen, freiwillig), Bemerkung.
- **Form des Schlüssels:** 44 Zeichen (Handbuch), Base64 (Webservice). Die App speichert 32 Zufallsbytes als Base64 (`RksvFiscalization.cs`, `RksvCode.NewTurnoverKey`), das sind 44 Zeichen. Die Form passt.
- **Prüfwert:** SHA-256 über den Base64-Text des Schlüssels, die ersten 3 Bytes, wieder Base64 (Quelle „Prüfwert“). FinanzOnline rechnet gleich und meldet eine Abweichung.
- **Lücke im Code:** Kein Vertrag der API gibt den Schlüssel heraus (`TurnoverKey` steht nur in `AispaStudio.Api`), und den Prüfwert berechnet der Code nicht. Ohne beides lässt sich die Kassa nicht registrieren, auch nicht mit einer zertifizierten Einrichtung.
- **Kassenidentifikationsnummer:** Sie ist der Code der Kassa (`CashRegister.Code`) und bis zum geprüften Startbeleg änderbar, danach nicht mehr (Handbuch 3.3.3).
- **Drei Wege** (Handbuch 1.3):
  - Eingabe von Hand,
  - XML-Datei hochladen,
  - Webservice aus der Kassensoftware, mit eigenem Webservice-Benutzer.
  - Über Webservice und Datei entfällt die Prüfung des Startbelegs mit der Belegcheck-App.
- **Reihenfolge** (Handbuch 1.4): Einrichtung in Betrieb nehmen, Einrichtung registrieren, Kassa registrieren, Startbeleg erstellen, Startbeleg prüfen, Betrieb.
- **Frist:** Registrierung binnen einer Woche nach Inbetriebnahme (RKSV § 6 Abs. 3). Für die Prüfung des Startbelegs nennt der Erlass einmal „nach der Inbetriebnahme“ (3.5.1) und einmal „nach der Registrierung“ (3.5.3.2); die strengere Lesart ist die sichere.
- **Nicht belegt:** ob es neuere Fassungen von Handbuch (2021) und Webservice-Beschreibung gibt und ob der Webservice ein Feld für den Prüfwert kennt.

### Jahresende und laufende Pflichten

- **Jahresbeleg** (RKSV § 8 Abs. 3, Erlass 4.5.7): Er ist der Monatsbeleg für Dezember, grundsätzlich bis 31. Dezember herzustellen, auszudrucken, aufzubewahren und bis 15. Februar des Folgejahres zu prüfen. Saisonbetriebe erstellen ihn zum Saisonende, spätestens vor Beginn der Tätigkeit im neuen Jahr.
- **Abweichung im Code:** `PosService.IssueCoreAsync` erzeugt den Jahresbeleg erst mit dem ersten Beleg im neuen Jahr. Bei Betriebsferien im Jänner entsteht er nach dem Jahreswechsel und möglicherweise nach dem 15. Februar. Ob die Praxis des ersten Belegs im neuen Jahr geduldet wird, ist nicht belegt. Der Assistent braucht deshalb einen eigenen Schritt zum Jahresende, oder die Kassa bietet den Jahresbeleg ab dem letzten Öffnungstag an.
- **Prüfung ohne App:** Über den Webservice prüft FinanzOnline den Jahresbeleg und meldet das Ergebnis an die Kassa zurück (Handbuch 1.3.1). Dass damit auch der Ausdruck entfällt, sagt nur die WKO, nicht der Erlass.
- **Weitere Pflichten, die der Assistent nennen muss:**
  - Monatsbeleg zu jedem Monatsende; für Monate ohne Betrieb entfällt er (RKSV § 8 Abs. 2, Erlass 4.5.7).
  - Sicherung des Datenerfassungsprotokolls mindestens vierteljährlich auf ein externes Medium (RKSV § 7 Abs. 3).
  - Ausfall der Signatureinrichtung: Bis 48 Stunden gilt er als vorübergehend; darüber ist er binnen einer Woche zu melden, sein Ende ebenfalls (RKSV § 17, Erlass 3.6).
  - Schlussbeleg bei planmäßiger Außerbetriebnahme (RKSV § 17 Abs. 8).
  - Betriebsferien sind weder Ausfall noch Außerbetriebnahme (Erlass 3.6).

### Folgen für die Pakete

| Ergänzung | Warum |
| --- | --- |
| Schlüssel und Prüfwert in der API und im Assistenten zeigen, nur mit dem Recht, das die Kassa anlegt | ohne sie keine Registrierung |
| Schritt zum Jahresende oder Jahresbeleg ab dem letzten Öffnungstag | Erlass 4.5.7 |
| Später: Webservice zu FinanzOnline für Registrierung, Start- und Jahresbeleg | nimmt der Person die Belegcheck-App ab |

## Nachtrag vom 27.11.2026: Einfacher Weg für ein kleines Spa

Frage des Auftraggebers: Wie kommt ein kleines Spa (eine Kassa, keine IT-Kenntnisse) mit möglichst wenigen Schritten zu einer echten Kassa und zur Kartenzahlung, und wie zeigt die App die Anbieter je Land?

Zwei Recherche-Läufe haben die Seiten der Anbieter am 27.11.2026 gelesen. „Nicht belegt“ heißt: Auf der Seite des Anbieters stand dazu nichts. Preise stammen von Werbeseiten, teils als Aktion, und sind vor einer Entscheidung neu zu prüfen.

### Vorschlag in drei Stufen (Österreich zuerst)

| Stufe | Was | Was das Spa tut | Was zu bauen ist |
| --- | --- | --- | --- |
| 1 | Übungskassa mit Test-Signatur und Dauerwarnung | nichts | vorhanden; hängt an Frage 2 |
| 2 | Echte Kassa ohne Hardware: Online-Signatur, zuerst GLOBALTRUST RKS CLOUD | bestellt online, trägt die Zugangsdaten ein, meldet Kassa und Signatureinrichtung in FinanzOnline an | Anbieterwahl je Kassa, eine Umsetzung von `IRksvSignatureDevice`, Ablage der Zugangsdaten, Schlüssel und Prüfwert anzeigen |
| 3 | Kartenzahlung: neues Terminal über eine Cloud-Schnittstelle (zuerst SumUp Solo) oder vorhandenes Terminal über ZVT | koppelt das Gerät mit einem Code oder trägt die Adresse des Terminals ein | eine Umsetzung von `ICardTerminal` für die Cloud; ZVT mit einer erprobten Bibliothek |

Warum GLOBALTRUST RKS CLOUD zuerst: Die Kassa führt Belegkette, Umsatzzähler, Protokoll und QR-Code schon selbst (Abschnitt 2); es fehlt nur die Signatur. Laut Anbieter ist das Zertifikat kostenlos, eine Signatur kostet unter 0,5 Cent, es gibt eine Testumgebung und Beispiele für C# ([globaltrust.eu/en/rks](https://globaltrust.eu/en/rks/)). Die Karte desselben Anbieters (99 € für drei Jahre mit Lesegerät) bleibt der Weg ohne Internet.

Deutschland folgt danach. Der Weg mit den wenigsten Schritten ist dort eine Cloud-TSE; bei fiskaly wäre der Hersteller der Kassensoftware Vertragspartner und reicht die Nutzung weiter, der Preis ist nicht öffentlich.

### Anbieterwahl in der App

- Die Person beantwortet zuerst eine Frage, erst danach wählt sie einen Anbieter: „Wie soll die Kassa signieren?“ mit **Online-Dienst**, **Karte im Lesegerät** (Deutschland: **TSE als Stick oder im Drucker**) und **Übungskassa**.
- Das Land der Kassa steht aus Schritt 1 fest und filtert die Liste. Die Schweiz zeigt keine Wahl, nur den Satz, dass keine Signatur nötig ist.
- Die Liste kommt aus der API (Anbieter, Art, nötige Felder, verfügbar oder nicht). Ein neuer Anbieter braucht keine neue Maske.
- Anbieter ohne Anbindung bleiben sichtbar und gesperrt, mit dem Hinweis, dass die Anbindung folgt.
- Nach der Wahl folgen nur die Felder dieses Anbieters und **Verbindung prüfen** mit einer Probesignatur. Erst danach legt der Assistent die Kassa an.
- Die Kassa trägt den gewählten Anbieter. Heute ist `TestSignatureDevice` fest registriert, und die deutsche Seite nimmt die erste TSE, die sie findet.
- Zugangsdaten liegen verschlüsselt je Spa, nach dem Muster des Claude-Schlüssels (`ClaudeKeyStore`). Die App zeigt sie nach dem Speichern nicht mehr an.
- Eine Karte im Lesegerät hängt am PC, nicht am Server. Sie läuft wie das Kartenterminal über die App und über PC/SC, das es auch auf dem Mac gibt.
- Der Wechsel des Anbieters bei einer benutzten Kassa ist ein eigener Ablauf und nicht Teil des ersten Schritts (siehe „Nicht geklärt“).

### Fiskal-Dienste im Vergleich

| Anbieter | Länder | Hardware | Vertragspartner des Spas | Preis laut Seite | Einschätzung |
| --- | --- | --- | --- | --- | --- |
| GLOBALTRUST RKS CLOUD | AT | keine | bestellt selbst online | Zertifikat kostenlos, unter 0,5 Cent je Signatur | passt zur vorhandenen Kassa; erster Anbieter |
| GLOBALTRUST RKS CARD | AT | Karte und Lesegerät | bestellt selbst online | 99 € für 3 Jahre, 149 € für 5 Jahre | Weg ohne Internet |
| A-Trust a.sign RK HSM | AT | keine | nur über Partner | nicht öffentlich | das Spa kann nicht selbst bestellen |
| fiskaltrust | AT, DE und weitere | keine in der Cloud-Variante | nicht belegt (Händler, Partner oder Hersteller) | CloudCashBox 74 € je Jahr; Paket mit Online-Signatur 300 bis 499 € je Jahr und Standort | führt Belegkette und Protokoll selbst, die die Kassa schon hat; Alternative für viele Länder |
| fiskaly SIGN AT, SIGN DE | AT, DE | keine | der Hersteller der Kassensoftware | nicht öffentlich | für Deutschland der kürzeste Weg; für Österreich Zertifikat und FinanzOnline nicht belegt |
| efsta | AT, DE | lokaler Dienst | über Partner, Prüfung 2 bis 4 Wochen | ein Beispiel: 10,82 € je Kassa und Monat | kein schneller Einstieg; Mac nicht genannt |

Quellen: [fiskaltrust, Bestandteile](https://docs.fiskaltrust.eu/docs/poscreators/middleware-doc/general/components), [fiskaltrust CloudCashBox](https://fiskaltrust.eu/en-at/partner/product/cloudcashbox), [fiskaly SIGN AT](https://workspace.fiskaly.com/pm-hub/service-descriptions/sign-at/2026-03-24), [fiskaly SIGN DE](https://workspace.fiskaly.com/pm-hub/service-descriptions/sign-de/2026-03-24), [A-Trust, RKSV-Angebot](https://a-trust.at/en/products/cash_register/rksv_portfolio), [efsta, Österreich](https://docs.efsta.eu/efr/AT).

Die .NET-Pakete der Dienste sind alt (`fiskaltrust.interface` 1.3.45 vom April 2023, `fiskaly-dotnet-sdk` 1.2.200 vom Mai 2021). Eine Anbindung spricht die Schnittstellen besser direkt an.

### Kartenterminal

Stand im Code: Die API legt Terminals je Standort an und schickt Zahlungen über die Queue; die Windows-App spricht ein Gerät im Netz über ZVT oder OPI an (`ZvtClient.cs`, `OpiClient.cs`, ADR 0034). Beides ist nur gegen nachgebaute Terminals getestet.

| Weg | Anbieter | Schritte für das Spa | Kosten laut Seite | Offen |
| --- | --- | --- | --- | --- |
| Cloud | SumUp Solo | Konto online, Kopplungscode eintippen | 1,39 % ohne Monatsgebühr | Ergebnis kommt als Webhook; Partnerschlüssel; Länderliste |
| Cloud | Stripe Terminal | Konto online, Gerät anmelden | Gerät ab 199 €, 1,4 % + 0,10 € | Girocard und Debitkarten am Gerät |
| Gerät im Netz, ZVT | hobex, Worldline, Payone, Global Payments, Nexi | Adresse des Terminals eintragen; der Anbieter schaltet ZVT frei | Vertrag des Anbieters | Test am echten Gerät |
| Gerät im Netz, OPI | Nexi, Worldline | wie ZVT | Vertrag des Anbieters | keine freie .NET-Bibliothek gefunden |
| Cloud, nur Schweiz | wallee | nicht belegt | nicht belegt | nur CH und CHF |

Adyen (Vertrag über den Vertrieb), Zettle (Schnittstelle für Desktop-Kassen nicht belegt) und myPOS (Anbindung nur über Bluetooth oder USB) sind für ein einzelnes Spa umständlicher.

Bibliothek für ZVT: [`Portalum.Zvt`](https://github.com/Portalum/Portalum.Zvt), MIT-Lizenz, Fassung 3.4.0 vom November 2024, Vorabfassung vom April 2026. Sie deckt Zahlung, Storno, Gutschrift, Kassenschnitt und Abbruch ab und ist laut Beschreibung an Terminals von CardComplete, hobex, Worldline/PAYONE und Global Payments in Österreich getestet. Der mitgelieferte Simulator genehmigt immer und kennt weder Ablehnung noch Abbruch. Ein Tausch der eigenen Umsetzung (436 Zeilen) gegen die Bibliothek ändert Verhalten und braucht vorher die Zustimmung des Auftraggebers.

### Offen vor dem Bau

- **Webhook im Einzelplatz:** SumUp meldet das Ergebnis einer Zahlung per Webhook. Die API des Einzelplatzes läuft nur auf dem Empfangs-PC und ist von außen nicht erreichbar. Ob sich das Ergebnis abfragen lässt, ist nicht belegt.
- **Partnerschlüssel von SumUp:** Wie ein Hersteller ihn bekommt, steht auf den gelesenen Seiten nicht.
- **Testzugänge:** Die Testumgebung von GLOBALTRUST und ein Entwicklerkonto bei SumUp legt der Auftraggeber an.
- **Ausfall ohne Internet:** Bei der Online-Signatur gilt die Signatureinrichtung dann als ausgefallen. Die Kassa kennt den Fall; was die RKSV bei häufigen kurzen Ausfällen verlangt, ist hier nicht geprüft.
- **Echtes Gerät:** Auch mit der Bibliothek bleibt ein Test an einem Terminal nötig, vor allem für Abbruch, Zeitüberschreitung und Storno nach einem Neustart.
- **Externe Kassensysteme:** Eine fremde Kassa hinter der eigenen Oberfläche (zum Beispiel über die Schnittstelle eines Kassenherstellers) hielte Artikel, Steuersätze und Gutscheine doppelt; sie ist nicht Teil des Vorschlags. Ein Spa ohne eigene Kassa bleibt möglich (Frage 7), dann erzeugt die App keine Belege.

Die ersten vier Punkte beantwortet der folgende Nachtrag.

## Nachtrag vom 27.11.2026: Offene Punkte geklärt, Deutschland, Schweiz, Empfehlungen

Auftrag des Auftraggebers: die offenen Punkte klären, dasselbe für Deutschland und die Schweiz erarbeiten und alle Empfehlungen eintragen. Drei Recherche-Läufe haben die Quellen am 27.11.2026 gelesen. Keine Rechtsberatung; „nicht belegt“ heißt, dass die gelesene Quelle dazu nichts sagt.

### Österreich: geklärte Punkte

| Punkt | Ergebnis | Quelle |
| --- | --- | --- |
| SumUp ohne Webhook | Möglich. `return_url` ist beim Anlegen der Zahlung keine Pflicht. Das Ergebnis lässt sich mit der `client_transaction_id` über `GET /v2.1/merchants/{merchant_code}/transactions` abfragen (`SUCCESSFUL`, `FAILED`, `CANCELLED`, `PENDING`). Ob der Stand bei Zahlungen am Solo sofort stimmt, ist nicht belegt; das zeigt ein Lauf gegen den „Virtual Solo“. | [Zahlung anlegen](https://developer.sumup.com/api/readers/create-checkout), [Transaktion abfragen](https://developer.sumup.com/api/transactions/get) |
| Partnerschlüssel von SumUp | Selbstbedienung im SumUp-Konto unter „For developers“, ohne Antrag. Dazu gibt das Spa der Kassa seinen API-Schlüssel oder stimmt per OAuth zu. | [Affiliate Keys](https://developer.sumup.com/tools/authorization/affiliate-keys/), [Anmeldung](https://developer.sumup.com/tools/authorization/) |
| Gutschrift bei SumUp | Über die API möglich, ganz oder als Teilbetrag. | [Refund](https://developer.sumup.com/api/transactions/refund) |
| Stripe ohne Webhook | Möglich. Der Stand steht am Reader (`action.status`) und am PaymentIntent; Stripe empfiehlt selbst einen Knopf „Status prüfen“. | [Stripe Terminal, servergesteuert](https://docs.stripe.com/terminal/payments/collect-card-payment?terminal-sdk-platform=server-driven) |
| Ausfall der Signatureinrichtung | Jeder nicht nur vorübergehende Ausfall ist ohne unnötigen Aufschub zu melden (§ 17 RKSV). Dauert er länger als 48 Stunden, ist binnen einer Woche zu melden. Belege tragen „Sicherheitseinrichtung ausgefallen“; danach folgt ein signierter Sammelbeleg mit Betrag null. Ob ein Ausfall des Internets bei einer Online-Signatur als Ausfall zählt und was bei häufigen kurzen Ausfällen gilt, ist nicht belegt. | [BMF-Handbuch Registrierkassen](https://finanzonline.bmf.gv.at/eLearning/BMF_Handbuch_Registrierkassen.pdf), Stand Jänner 2021 |
| FinanzOnline per Software | Möglich. Der Registrierkassen-Webservice registriert Signatureinrichtung und Kassa, prüft Belege und meldet Ausfall und Wiederinbetriebnahme. Das Spa legt dafür in FinanzOnline einen Webservice-Benutzer an (Teilnehmer-ID, Benutzer-ID, PIN). | [BMF, Registrierkassen-Webservice](https://www.bmf.gv.at/dam/jcr%3A19c193f4-99cd-42ff-9b23-655f2ab5734e/BMF_Registrierkassen_Webservice.pdf), Stand Mai 2017 |
| GLOBALTRUST RKS CLOUD | Schnittstelle über WSDL mit Mustercode für C#; Kennung in FinanzOnline `AT2`. Pakete: BASIC mit 10.000 Signaturen je Jahr und einem Zertifikat, größere Pakete für mehrere Unternehmen. Ein Preis in Euro je Paket steht nicht öffentlich; welche Unterlagen die Bestellung verlangt, ist nicht belegt. | [Datenblatt](http://service.globaltrust.eu/static/datasheet-rks.pdf) |
| PrimeSign Remote Signing | Konto ab 196 € netto je Jahr ohne Grenze für Signaturen, REST-Schnittstelle, Kennung `AT3`. Test ohne Vertrag nicht belegt. | [PrimeSign RKSV](https://primesign.cryptas.com/en/primesign-rksv) |
| Testgerät für ZVT | hobex liefert zu seiner Java-Schnittstelle einen Terminalsimulator. Ein Testterminal für Kassenhersteller nennt kein Anbieter öffentlich. | [hobex, ECR-Schnittstelle](https://www.hobex.at/technet/ecr/api/java/doc/index.html) |

Der Preis der GLOBALTRUST-Karte steht je nach Seite mit 99 € oder 109 € für drei Jahre.

### Deutschland

**Wege zur TSE.**

| Weg | Wer bestellt | Preis und Laufzeit | Ohne Internet | Mac |
| --- | --- | --- | --- | --- |
| fiskaly SIGN DE (Cloud) | der Hersteller der Kassensoftware, der die Nutzung weitergibt | Preis nicht öffentlich; Zertifizierung laut Anbieter bis 2033 | keine Signatur, Kassa läuft im Ausfallmodus weiter | ja, nur REST |
| Deutsche Fiskal und D-Trust (Cloud) | über Kassenanbieter | nicht belegt; Zertifikat fünf Jahre | braucht zusätzlich ein lokales Sicherheitsmodul auf dem PC | nicht belegt |
| Swissbit (USB oder SD) | das Spa über Händler | rund 220 €, Zertifikat rund fünf Jahre | läuft | nicht genannt |
| Epson (im Drucker oder USB) | das Spa über Händler | rund 245 € | läuft | nicht belegt |

Quellen: [fiskaly SIGN DE](https://www.fiskaly.com/de/signde), [D-Trust, Fiskalisierung](https://www.d-trust.net/en/support/fiscalization), [Swissbit USB-TSE](https://www.swissbit.com/en/products/point-of-sale/usb-tse).

**Pflichten und Stand der Kassa.**

| Pflicht | Quelle | Stand im Code |
| --- | --- | --- |
| Zertifizierte TSE; die Kassa selbst braucht keine Zulassung | [§ 146a AO](https://www.gesetze-im-internet.de/ao_1977/__146a.html) | fehlt, nur Test-TSE |
| Mitteilung an das Finanzamt über Mein ELSTER, innerhalb eines Monats nach Anschaffung, mit Art und Seriennummer der TSE und Datum | § 146a Abs. 4 AO, [BMF-Schreiben vom 28.06.2024](https://www.ihk.de/blueprint/servlet/resource/blob/6325954/f6aea825353bceaa32423ad1d885358f/bmf-schreiben-28-06-2024-data.pdf) | nur Hinweistext |
| Beleg zu jedem Vorgang, auf Papier oder elektronisch | § 146a Abs. 2 AO | vorhanden |
| Pflichtangaben auf dem Beleg; QR-Code freiwillig | [§ 6 KassenSichV](https://www.gesetze-im-internet.de/kassensichv/__6.html) | vorhanden |
| Export nach DSFinV-K und der TSE-Daten | Merkblätter der Länder | vorhanden, Prüfprogramm offen |
| Ausfall der TSE: Zeit und Grund festhalten, Beleg kennzeichnen, Ursache unverzüglich beheben; keine feste Frist | AEAO zu § 146a, Fassung 2023 | Ausfall wird festgehalten; Kennzeichnung prüfen |
| Aufbewahrung: Quellen nennen 8 oder 10 Jahre | [§ 147 AO](https://www.gesetze-im-internet.de/ao_1977/__147.html) | 10 Jahre vorsehen |

**Kartenzahlung.** In Deutschland zählt die girocard. Der SumUp Solo nimmt sie seit August 2025 an; ob das auch über die Cloud-Schnittstelle gilt, ist nicht belegt. Bei Stripe nimmt nur der Reader S700 die girocard an, der WisePOS E nicht. Vorhandene Terminals der Netzbetreiber laufen über ZVT ([SumUp, girocard](https://www.sumup.com/de-de/press/girolaunch/), [Stripe, Deutschland](https://docs.stripe.com/terminal/payments/regional.md?integration-country=DE)).

**Empfehlung für Deutschland.**

1. Übungskassa im Einzelplatz ermöglichen. Heute scheitert dort schon das Anlegen einer deutschen Kassa (Abschnitt 3).
2. Echte Kassa mit der Cloud-TSE von fiskaly: keine Hardware, läuft auf dem Mac, das Spa schließt keinen eigenen Vertrag. Die Kosten trägt zuerst der Hersteller der Kassensoftware; das gehört ins Preismodell.
3. USB-TSE von Swissbit als Weg ohne Internet, erst wenn ein Spa sie verlangt; sie bindet an Windows oder Linux.
4. Der Assistent zeigt ein Datenblatt für Mein ELSTER (Art und Seriennummer der TSE, Seriennummer der Kassa, Datum) und speichert die Bestätigung der Person.
5. Kartenzahlung wie in Österreich; beim Cloud-Terminal zuerst die girocard mit dem „Virtual Solo“ und einem echten Gerät prüfen.

### Schweiz

**Recht.**

| Punkt | Ergebnis | Quelle |
| --- | --- | --- |
| Signatur oder Fiskalisierung | Keine Pflicht gefunden. Eine amtliche Aussage, die sie verneint, liegt nicht vor. | Seiten der ESTV zur MWST |
| Aufbewahrung | 10 Jahre ab Ende des Geschäftsjahres | Art. 958f OR |
| Elektronische Aufbewahrung | Änderungen dürfen nicht unbemerkt möglich sein. Auf Festplatte oder SSD verlangt die Verordnung ein Verfahren für die Integrität, einen nachweisbaren Zeitpunkt der Speicherung, Protokolle und eine Beschreibung des Verfahrens. | [GeBüV](https://media.bs.ch/original_file/8a6dfb6490d154b7ac741a0c483338dfa6ac7ba0/221-431.pdf) Art. 3, 4 und 9 |
| Angaben auf dem Beleg | MWST-Nummer, Leistung, Entgelt, Steuersatz; Kassenzettel bis 400 Franken ohne Angaben zum Empfänger | Art. 26 MWSTG, Art. 57 MWSTV |
| Steuersätze | 8,1 %, 2,6 %, 3,8 % (Beherbergung, befristet). Am 29.11.2026 stimmt die Schweiz über eine Erhöhung ab, frühestens ab 2028. | Art. 25 MWSTG, [BSV](https://www.bsv.admin.ch/de/abstimmung-13-ahv-rente) |
| Steuerpflicht | erst ab 100.000 Franken Umsatz im Jahr; darunter ohne MWST auf dem Beleg | Art. 10 MWSTG |
| Rundung auf 5 Rappen | Keine Rechtsgrundlage gefunden; ob sie auch bei Kartenzahlung gilt, ist nicht belegt. | – |

**Kartenzahlung.** In der Schweiz zählen TWINT und die PostFinance Card.

| Anbieter | Schnittstelle | TWINT, PostFinance Card | Kosten laut Seite | Offen |
| --- | --- | --- | --- | --- |
| PostFinance-Terminal (PAX) mit wallee | lokal im Netz über TCP; die Antwort kommt mit der Anfrage zurück | beide ohne weiteren Vertrag | 9,90 Franken je Monat, 1,2 % bis 1,5 % | Testgerät; das Spa braucht ein Geschäftskonto bei PostFinance; das Modell A50s lässt sich nicht an eine Kassa anbinden |
| Worldline (TIM) | lokal im Netz, Bibliothek für .NET | TWINT laut Werbung | nicht genannt | Bezug der Bibliothek, Zertifizierung |
| SumUp Solo | Cloud | TWINT nach eigener Anmeldung des Spas | 1,5 % bis 2,5 % | PostFinance Card nicht belegt |
| Stripe Terminal | Cloud | am Terminal nicht belegt | nicht belegt | für die Schweiz kaum geeignet |

Quellen: [PostFinance, Terminal](https://checkout.postfinance.ch/de-ch/doc/payment/terminal), [wallee, lokale Schnittstelle](https://lti.docs.wallee.com/lti_2.54/), [Worldline TIM](https://six-tim.github.io/timapi/doc/cs/guide.html), [SumUp Schweiz](https://www.sumup.com/de-ch/preise/).

TWINT ohne Terminal gibt es als Aufkleber mit QR-Code oder als direkte Anbindung der Kassa; was ein Hersteller für die direkte Anbindung braucht, ist nicht belegt ([TWINT, Kassensysteme](https://www.twint.ch/en/business-customers/products/twint-on-site/pos-systems/)).

Für die Zahlung auf Rechnung gilt die QR-Rechnung. Die Bibliothek [`Codecrete.SwissQRBill.Generator`](https://www.nuget.org/packages/Codecrete.SwissQRBill.Generator) steht unter MIT-Lizenz (Fassung 3.5.1 vom September 2026).

**Empfehlung für die Schweiz.**

1. Kassa ohne Signaturschritt, wie heute. Die Verkettung der Belege per SHA-256 passt zur Forderung der GeBüV; der Zeitpunkt der Speicherung und eine Beschreibung des Verfahrens sind zu ergänzen und zu prüfen.
2. Rundung auf 5 Rappen als Einstellung, nur für Barzahlung, im Vorschlag eingeschaltet. Das ist eine Setzung nach der Praxis, nicht nach einer Norm.
3. Spa ohne Steuerpflicht: Beleg ohne MWST. Ob die Kassa das heute kann, ist zu prüfen.
4. Steuersätze mit Gültigkeitsdatum führen, weil sich die Sätze ab 2028 ändern können.
5. Kartenzahlung zuerst über ein Terminal im Netz (PostFinance mit wallee), weil nur dieser Weg TWINT und PostFinance Card in einem Vertrag bringt. Das ist eine dritte Umsetzung neben ZVT und OPI in der Windows-App. SumUp bleibt der Weg ohne Netzwerkeinstellung.
6. QR-Rechnung als Ergänzung zur Bankverbindung in Schritt 3, erst nach Kassa und Terminal.

### Was die App je Land anbietet

| Schritt | Österreich | Deutschland | Schweiz |
| --- | --- | --- | --- |
| Signatur | Online-Dienst (GLOBALTRUST, PrimeSign) · Karte im Lesegerät · Übungskassa | Cloud-TSE (fiskaly) · TSE als Stick · Übungskassa | entfällt |
| Meldung an die Behörde | FinanzOnline: von Hand mit Schlüssel und Prüfwert, später über den Webservice | Mein ELSTER: von Hand mit Datenblatt | entfällt |
| Kartenterminal | SumUp Solo · Terminal im Netz (ZVT) | SumUp Solo · Stripe S700 · Terminal im Netz (ZVT) | PostFinance-Terminal · SumUp Solo · Worldline |
| Besonderheit | Jahresbeleg bis 31. Dezember | Export für die Kassen-Nachschau | Rundung auf 5 Rappen, QR-Rechnung |

### Empfehlungen im Überblick

| Nr. | Empfehlung | Braucht |
| --- | --- | --- |
| 1 | Rahmen für die Anbieterwahl bauen: Liste je Land aus der API, Anbieter je Kassa, Zugangsdaten verschlüsselt, **Verbindung prüfen** | Freigabe des Plans |
| 2 | Übungskassa in allen drei Ländern, mit Dauerwarnung; in Deutschland auch im Einzelplatz | Antwort auf Frage 2 |
| 3 | Schlüssel und Prüfwert für FinanzOnline anzeigen; Jahresbeleg rechtzeitig vor dem 31. Dezember | – |
| 4 | Österreich: zuerst eine Online-Signatur. GLOBALTRUST RKS CLOUD, wenn der Paketpreis unter dem von PrimeSign liegt, sonst PrimeSign (196 € je Jahr, REST) | Testzugang, den der Auftraggeber anlegt |
| 5 | Österreich, danach: Registrierung und Belegprüfung über den Webservice von FinanzOnline. Das nimmt dem Spa die meisten Schritte außerhalb der App ab. | Test-Benutzer in FinanzOnline |
| 6 | Kartenterminal über die Cloud: SumUp Solo mit Abfrage statt Webhook; Stripe als zweiter Weg, weil Stripe für Online-Zahlungen schon angebunden ist | Entwicklerkonto, das der Auftraggeber anlegt |
| 7 | ZVT mit `Portalum.Zvt` hinter der bestehenden Schnittstelle, danach Test an einem echten Gerät | Zustimmung zum Tausch; Leihgerät oder Testvertrag |
| 8 | Deutschland: Cloud-TSE von fiskaly, Datenblatt für Mein ELSTER | Anfrage beim Anbieter und Entscheidung zum Preismodell |
| 9 | Schweiz: Rundung, Beleg ohne MWST, Steuersätze mit Datum, danach Terminal im Netz | Testgerät |
| 10 | Kein fremdes Kassensystem hinter der eigenen Oberfläche; kein Fiskal-Dienst, der die Belegkette selbst führt, solange die eigene Kassa sie führt | – |
| 11 | Fragen 3 bis 5: Bestätigungen mit Datum speichern, Anleitungen mit Link zeigen, Recht `ManageLocation` | – |
| 12 | Fragen 6 und 7: „erledigt“ heißt „kann Belege bilden“; die Kassa bekommt eine eigene Karte nach „Betrieb einrichten“ | Antwort des Auftraggebers |

Reihenfolge: 1 bis 3 brauchen keinen Vertrag und machen den Assistenten in Österreich fertig. 4 und 6 folgen mit den Testzugängen. Deutschland und die Schweiz folgen nach dem ersten echten Beleg in Österreich.

### Weiter offen

- Preis des Pakets BASIC von GLOBALTRUST und die Unterlagen für die Bestellung.
- Ob ein Ausfall des Internets bei einer Online-Signatur als Ausfall nach § 17 RKSV zählt.
- Preis und Vertrag von fiskaly; ob die Cloud-TSE ein lokales Sicherheitsmodul verlangt.
- Die girocard über die Cloud-Schnittstelle von SumUp.
- Ob die Schweiz wirklich keine Pflicht zur Signatur kennt; eine Anfrage bei der ESTV wäre der Beleg und braucht die Freigabe des Auftraggebers.
- Testgeräte: kein Anbieter nennt öffentlich einen Weg für Kassenhersteller.
- Mehrere Quellen sind alt (BMF-Handbuch 2021, Webservice-Beschreibung 2017, AEAO 2023); neuere Fassungen wurden nicht gesucht.

## Nicht geklärt

Die Punkte zu Anbietern, FinanzOnline und Jahresende sind im Nachtrag vom 27.11.2026 beantwortet; dort stehen auch die Reste, die nicht belegt sind.

- Wie sich ein Wechsel von einer Test-Signatur zu einer zertifizierten Einrichtung auf eine schon benutzte Kassa auswirkt (neue Kassa-ID nötig? Der Code kennt kein Umstellen; `CertificateSerial` wird nur beim Anlegen gesetzt).
- Ob das Recht der Schweiz weitere Pflichten für eine Kassa stellt als die im Code genannten (Aufbewahrung nach GeBüV, MWST); ADR 0010 und `docs/stand-und-offene-punkte.md` nennen die Rappenrundung und die MWST-Prüfung mit echtem Mandanten als offen.
- Ob die Windows-App auf einem PC mit Server-Betrieb eine Kassa der anderen Stelle sinnvoll anzeigen soll (Hinweis `OtherSite`); der Assistent legt nur eine eigene Kassa an.
- Die Maske-Hilfetexte (`web/src/help/content/`): ob dort eine Anleitung zum Anlegen einer Kassa steht. Die Suche nach „Kassa anlegen“, „Kassa-ID“ und „neue Kassa“ in `web/src/help/content` fand keine; geprüft wurden aber nur diese Suchbegriffe.
- Ob `GET /api/pos/registers` im Einzelplatz nach der Anlage durch `SiteId` zuverlässig dem eigenen PC zugeordnet wird; nur `IsOtherSite` und `ClaimForThisSite` gelesen, kein Lauf.
