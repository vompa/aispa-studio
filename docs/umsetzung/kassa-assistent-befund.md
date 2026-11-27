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

## Nicht geklärt

- Welche Anbieter für Signatureinrichtung und TSE im Handel zulässig und für die App geeignet sind (Preise, Schnittstellen, Einzelplatz); nur die Namen im Code und in ADR 0035 (A-Trust, Global Trust, PrimeSign, fiskaly, Deutsche Fiskal, Swissbit) sind belegt, nicht ihre Eignung.
- Wie genau FinanzOnline die Registrierung der Kassa verlangt (Felder, ob der AES-Schlüssel dort oder nur im Registrierkassen-System liegt) und ob der Schlüssel der App in der Form vorliegt, die FinanzOnline erwartet. Im Code ist nur die Erzeugung (`RksvCode.NewTurnoverKey`) belegt, nicht der Gebrauch bei der Anmeldung.
- Ob zu Ende des Jahres ein gesonderter Schritt am Assistenten nötig ist (Jahresbeleg wird automatisch gebucht; nur die Prüfung ist Sache der Person).
- Wie sich ein Wechsel von einer Test-Signatur zu einer zertifizierten Einrichtung auf eine schon benutzte Kassa auswirkt (neue Kassa-ID nötig? Der Code kennt kein Umstellen; `CertificateSerial` wird nur beim Anlegen gesetzt).
- Ob das Recht der Schweiz weitere Pflichten für eine Kassa stellt als die im Code genannten (Aufbewahrung nach GeBüV, MWST); ADR 0010 und `docs/stand-und-offene-punkte.md` nennen die Rappenrundung und die MWST-Prüfung mit echtem Mandanten als offen.
- Ob die Windows-App auf einem PC mit Server-Betrieb eine Kassa der anderen Stelle sinnvoll anzeigen soll (Hinweis `OtherSite`); der Assistent legt nur eine eigene Kassa an.
- Die Maske-Hilfetexte (`web/src/help/content/`): ob dort eine Anleitung zum Anlegen einer Kassa steht. Die Suche nach „Kassa anlegen“, „Kassa-ID“ und „neue Kassa“ in `web/src/help/content` fand keine; geprüft wurden aber nur diese Suchbegriffe.
- Ob `GET /api/pos/registers` im Einzelplatz nach der Anlage durch `SiteId` zuverlässig dem eigenen PC zugeordnet wird; nur `IsOtherSite` und `ClaimForThisSite` gelesen, kein Lauf.
