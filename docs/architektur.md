# Architektur

Wie der Code aufgebaut ist und welche Muster gelten. Die Gründe stehen in den ADRs [0004](entscheidungen/0004-oberflaechen-avalonia-und-react.md), [0006](entscheidungen/0006-backend-modularer-monolith.md), [0023](entscheidungen/0023-doppelbuchungsschutz-in-der-datenbank.md) und [0024](entscheidungen/0024-mandantentrennung-eine-datenbank.md); Funktionen und Einstellungen beschreibt [Funktionen im Detail](features.md), Betrieb und Einstellungen die [Installation](installation/README.md).

## Überblick

```
Windows-App (Avalonia)   Web Team (React)   Kundenbereich /buchen/ (React, eigenes Bundle)
          │                     │                      │
          └──── HTTPS (OpenAPI 3.1, Cookies) + SignalR /hubs/live ────┘
                                │
                ASP.NET Core 10 API (modularer Monolith)
                                │
     PostgreSQL: Fachdaten (Schema public), Kundenkonten (online), Gesundheitsdaten (health),
                 Wolverine-Queue (dauerhaft)
                                │
     Hintergrund über Wolverine: Mails, KI, Terminannahme, Warteliste, Gutscheine, Live-Updates
```

Lokal startet .NET Aspire (`src/AispaStudio.AppHost`) PostgreSQL, API, Web und Mailpit. Ohne .NET und Node geht es mit `docker compose up -d --build`.

## Projekte

| Projekt | Inhalt |
| --- | --- |
| `src/AispaStudio.Api` | Backend, ein Ordner je Fachmodul (siehe unten) |
| `src/AispaStudio.Contracts` | DTOs für API, Windows-App und Tests |
| `src/AispaStudio.Client` | HTTP-Zugang zur API ohne Oberfläche und Plattform (`HttpApiClient`, `IAispaApi`, `ApiResult`); hängt nur an Contracts, für die Windows-App und spätere Clients |
| `src/AispaStudio.ServiceDefaults` | Health-Checks, OpenTelemetry, Resilienz |
| `src/AispaStudio.AppHost` | Aspire-Orchestrierung |
| `src/AispaStudio.Desktop` | Windows-App (Avalonia 12, MVVM: `Views`, `ViewModels`, `Services`); ViewModels nutzen die API über `IWorkstationApi`, umgesetzt von `ApiClient` (erbt von `HttpApiClient`, ergänzt den Betrieb ohne Verbindung) |
| `web` | React + TypeScript + Vite; `web/src/<modul>` je Bereich, Kundenbereich in `web/src/booking` mit Einstieg `web/buchen/index.html`, „Mein Tag“ in `web/src/myday` (`web/mein-tag/`), Betreiber-Seite in `web/src/operator` (`web/betreiber/`) |
| `tests/AispaStudio.Api.Tests` | Integrationstests gegen echtes PostgreSQL (Testcontainers) |
| `tests/AispaStudio.Desktop.Tests` | Tests der Windows-App |

## Fachmodule im Backend

| Ordner | Aufgabe |
| --- | --- |
| `Scheduling` | Stammdaten, Kalender, `ResourcePlanner`, `ResourceBookings`, freie Zeiten |
| `Roster` | Dienstplan; `RosterService.ShiftsAsync` ist die einzige Quelle für Arbeitszeiten in Kalender und Planer |
| `Access` | sechs Rollen, `AccessRight`, Endpunktfilter `RequirePermission` und `AlsoAllow`, Einladungen, Einmal-Code für die Windows-App (`DeviceSignIn`); Tabelle unten |
| `Auth` | Anmeldung des Teams mit Passkeys (ASP.NET Core Identity); im Einzelplatz-Betrieb Benutzername und Passwort (`StandaloneAuth`, ADR 0041) |
| `Tenancy` | Mandantentrennung (`TenantId`, globaler Filter, `TenantContext`, `OnlineTenant`) |
| `Pos` | Kassa, Belege, Gutscheine, Tagesabschluss; `IFiscalization` je Land (RKSV, TSE, CH) |
| `Accounting` | Buchhaltungsexport DATEV/BMD, Schweiz (Abacus/bexio), Kontenzuordnung |
| `Payroll` | Lohnexport BMD Lohn, DATEV LODAS, DATEV Lohn und Gehalt; Feiertage |
| `Promotions` | Aktionen ([ADR 0071](entscheidungen/0071-aktionen-und-rabatte.md)): `Promotion` (Zeitraum, Standorte, Prozent oder Betrag, Gültigkeit, aktiv/pausiert, archiviert), `PromotionService` (Verwaltung, `GetActiveAsync(locationId, date)` für Kassa und Assistent), `PromotionRules` (läuft, passt, Rabatt; gemeinsam mit der Kassa und den Beispieldaten). Der Rabatt bleibt ein Kassa-Rabatt: `PosService.ApplyPromotionAsync` setzt ihn auf Klick, den Beleg trägt `BillLine.DiscountNote` |
| `CalendarSubscriptions` | Kalender-Abos ([ADR 0075](entscheidungen/0075-kalender-abos-fuer-feiertage-und-ferien.md)): `CalendarSubscription` (Adresse, Art, Region, Aktiv, Stand des Abrufs) mit lokaler Kopie (`CalendarSubscriptionEntry`), Ausblenden je Zugang (`CalendarSubscriptionHidden`), Entscheidungen zu Widersprüchen (`CalendarSubscriptionDecision`) und Meldungen (`CalendarSubscriptionNotice`); `IcsParser` (RFC 5545 mit Wiederholungen, kein Fremdpaket) und `CsvCalendarParser`; Abruf nur im Hintergrund (`FetchCalendarSubscription` über Wolverine, `CalendarSubscriptionScheduler` täglich, `ICalendarFeedClient` mit Größen-, Zeit- und Zielgrenzen); `HolidayCalendar` ist die eine Quelle für „ist das ein Feiertag?“ (Regeln aus `PublicHolidays` plus Tage aktiver Abos, Tagesfakten für den Kalenderkopf, Ferien für den Assistenten); die Lohnverrechnung liest weiter nur die Regeln; Voreinstellungen je Land beim Anlegen eines Spas (`CalendarSubscriptionDefaults`), Adressen und Prüfdatum in `CalendarFeedSources` |
| `BusinessInfo` | Betriebsinformationen ([ADR 0074](entscheidungen/0074-betriebsinformationen-und-kontakte.md)): `BusinessContact` (Kategorie, Bezeichnung, Firma, Ansprechperson, Telefon, Mail, Zuständigkeit, bei Versicherungen Polizze und Schadenshotline, Standorte, Stufe „Team“ oder „Leitung“, archiviert), `BusinessContactService` (Lesen nach Stufe und Standorten, Verwalten mit `ManageLocation`), `BusinessContactRules` (dieselbe Prüfung für Endpunkte und Assistent, `UserAccess.SeesManagementContacts`). Der Assistent liest über `ForLocationAsync` und das Thema `BusinessContacts` |
| `Closures` | Schließzeiten je Standort ([ADR 0073](entscheidungen/0073-schliesszeiten-und-terminabsage.md)): `Closure` (Zeitraum mit optionaler Uhrzeit, Art, interner Grund, Text für Gäste; Zustand gerechnet), `ClosureService` (Verwalten, Abfragen für Planer, Kalender, Kundenbereich und Assistent), `ClosureCancellations` (Rückfrage, Absage der gewählten Termine mit `Appointment.CancelledByBusiness`, KI-Umformulieren auf Knopfdruck), `ClosureNotices` (Protokoll `ClosureNotice` und Versand `SendClosureNotice` über die Queue). Wirkung im `ResourcePlanner` (`PlanningContext.Closed`, `OpeningWindows`): keine freien Zeiten und keine Buchung in der Zeit, ohne dass ein Buchungsweg davon wissen muss. Dazu die **Raumsperre** ([ADR 0076](entscheidungen/0076-raumsperren.md)): `RoomClosure` (ein Raum, Zeitraum mit optionaler Uhrzeit, Art Wartung, Schaden, Umbau, Sonstiges, Grund), `RoomClosureService` (Verwalten, Abfragen für Planer, Kalender, Bereiche), `RoomClosureEndpoints`; Wirkung in `PlanningContext.ClosedRooms` und `ResourcePlanner.PickRoom` (der gesperrte Raum wird nie gewählt), Umbuchungshilfe über `AbsenceAssistant.ProposeForRoomClosureAsync` |
| `Costs` | Vollkosten je Behandlung |
| `Stock` | Lager: Bestand als Summe der Bewegungen, Verbrauch und Verkauf über die Queue (`ReconcileAppointmentStock`, `BookReceiptStock`), Bestellvorschlag, Bestellungen (`PurchaseOrders`) mit Lieferung als Wareneingang |
| `Reporting` | Mein Tag, Behandlungskarte, Tagesstart, Management, Pflichtberichte, Benchmark |
| `Online` | Kundenbereich: `OnlineDbContext`, Kundenkonten, Buchung, Anamnese, Warteliste, Gutscheinkauf |
| `Mail` | `IMailSender`, Mailversand über die Queue (`SendMail`) |
| `MailIntake` | Terminannahme: Postfächer (auch je Spa), Nummern je Spa, SMS/WhatsApp/Anruf, `IBookingAssistant`, Treatwell, ICS |
| `Assistant` | KI-Assistent je Rolle. Fragen zum Betrieb ([ADR 0072](entscheidungen/0072-assistent-beantwortet-betriebsfragen.md)): Ordner `Answers` mit Absichtserkennung per Regeln (`BusinessIntents`, `AnswerDates`) vor dem Sprachmodell, einem Antwortgeber je Thema (`IAnswerGiver`: Öffnungszeiten (mit Schließzeiten), Kontakt, Feiertage, Stornoregel, Team, Tag, Räume, Katalog, Gäste, Gutscheine, Aktionen, Auswertungen, Kontakte des Betriebs), dem Antwortblock (`AssistantBlock`: Satz, Tabelle, Quelle, Verweis, Kopiertext) und `IAnswerPhraser` (Claude glättet den Satz, `AnswerGuard` prüft die Zahlen); Rechte im Antwortgeber, Zahlen aus den bestehenden Diensten (`ManagementReports`, `PromotionService`, `PublicHolidays`, `RosterService`) |
| `Reminders` | Terminerinnerungen: `ReminderScheduler` plant je Spa, Versand über die Queue (`SendReminder`) |
| `Retention` | Erinnerung an die nächste Behandlung: Einwilligung mit Double-Opt-in (`MarketingConsents`), `RebookScheduler` plant je Spa, Text der KI und Versand über die Queue (`SendRebookReminder`) |
| `Live` | SignalR-Hub, Gruppen je Standort (Kalender) und je Spa (Posteingang) |
| `Data` | `AispaDbContext`, Migrationen |
| `Demo` | Testdatensatz für die Entwicklung (`DemoSeeder`, Testkonten); Stammdaten in `Scheduling/DemoData` |
| `System` | Systemendpunkte, Ping über Queue und SignalR (das Pong geht nur an den Aufrufer), Betriebsart und Modulschalter (`ModuleSwitches`, `RequireModule`), Web-Oberfläche aus der API mit `Hosting:WebRoot` (`TeamWebRoot`), geordnetes Beenden im Einzelplatz über `Hosting:StopFile` (`StopFileWatcher`) |
| `Sync` | Abgleich im Einzelplatz-Betrieb: Änderungsprotokoll (`ChangeLog`, Schema `sync`, Trigger an allen Fachtabellen), Abgleich mit der zentralen Datenbank (`SyncRunner`, `SyncWorker`, `Sync:CentralConnectionString`), Stand und abgelehnte Einträge (`SyncEndpoints`, `GET /api/system/sync`), Kennung der Datenbank (`sync.site`, `SiteIdentity`; bindet jede Kasse an ihre Stelle), Schlüssel für Gesundheitsdaten der Stellen (`sync.health_keys`, `HealthKeyExchange`, `HealthKeyRing`, ADR 0048), Mandant von Zeilen ohne `TenantId` über ihre Verweise (`sync.row_tenant` beim Schreiben, `sync.key_tenant` beim Holen), neuer PC ohne Spa findet sein Spa über den Schlüsselcode (`HealthKeyExchange.FindTenantAsync`), zentrales Protokoll nach `Sync:CentralRetentionDays` aufgeräumt (`sync.pruned`; wer dahinter liegt, holt den ganzen Bestand); in der Windows-App `SyncViewModel`, `JoinViewModel` (Verbinden statt Einrichten) und `LocalServer.SetCentralAsync`. Mehrplatz im Netz (ADR 0049) liegt nur in der Windows-App: `NetworkShare` (Zertifikat, Verbindungscode), `LocalServer.SetSharedAsync`, `NetworkViewModel`, `NetworkConnectViewModel` |

## Rollen und Rechte

Die Rollen sind eine feste Tabelle in `Access/Roles.cs` ([ADR 0003](entscheidungen/0003-rollenbasierte-oberflaechen.md), [ADR 0070](entscheidungen/0070-rollen-manager-und-backoffice.md)). Einzeln vergeben wird nur ein Recht: „Berichte mit Beträgen“ (`ViewAmountReports`) an Standortleitung und Rezeption, gespeichert als `AmountReports` am Zugang.

| Recht | Inhaber | Manager | Standortleitung | Rezeption | Therapeut:in | Backoffice |
| --- | --- | --- | --- | --- | --- | --- |
| `ViewCalendar` Kalender, Team, Behandlungen, Kunden sehen | ja | ja | ja | ja | ja | nein |
| `Book` buchen, kassieren | ja | ja | ja | ja | nein | nein |
| `ChangeStatus` Status von Terminen (Therapeut:in nur eigene) | ja | ja | ja | ja | ja | nein |
| `ManageLocation` Öffnungszeiten, Schließzeiten (Termine dazu absagen zusätzlich mit `Book`), Räume, Geräte, Dienstplan, Zeiten, Lager | ja | ja | ja | nein | nein | nein |
| `ManageCatalog` Team, Behandlungen, Qualifikationen, Kostensätze | ja | ja | ja | nein | nein | nein |
| `ManageStaff` Zugänge verwalten (ohne Inhaber und Manager) | ja (alle) | ja | ja | nein | nein | nein |
| `ManageOrganization` Standorte, Firmendaten, Zahlungen, Kassa-Einstellungen, Lohn ändern | ja | ja | nein | nein | nein | nein |
| `ViewFinance` Berichte ohne Beträge (Auslastung, Anzahlen, Quoten, Pflichtberichte) | ja | ja | ja | nur mit Beträge-Recht | nein | ja |
| `SuperviseCash` Kassenaufsicht | ja | ja | ja | nein | nein | nein |
| `ViewAmountReports` Berichte mit Beträgen (Umsatz, Kosten, Buchhaltung, Exporte) | ja | ja | nur vergeben | nur vergeben | nein | ja |
| `ViewReceipts` Belege und Tagesabschlüsse lesen | ja | ja | über `Book` | über `Book` | nein | ja |
| `ViewStaffRecords` Dienstplan, Zeiten, Team, Lohn lesen | ja | ja | über `ViewCalendar` und `ManageLocation` | über `ViewCalendar` | über `ViewCalendar` | ja |
| `ViewHealthData` Anamnese lesen: Antworten, fotografierte Bögen prüfen und übernehmen (ADR 0013, Nachtrag) | ja | ja | ja | ja | ja | nein |
| `ManageHrFile` Personalakte sehen, ändern und als PDF ausgeben; Hinweise, Sonderregelungen und Vollmachten pflegen, „Wer darf was?“; Zuständigkeiten und Zertifikate samt Scans pflegen, ihre Übersichten | ja | ja | nein | nein | nein | nein |

Nicht in der Tabelle, weil kein Recht: Inhaber- und Manager-Zugänge anlegen, ändern und entfernen sowie Lizenz und Abrechnung kündigen darf nur der Inhaber (`UserAccess.IsOwner`, `CanManage`). Das Backoffice gilt immer für alle Standorte. `ViewAmountReports` schließt `ViewFinance` ein. Die Stufe „Leitung“ der Betriebsinformationen ([ADR 0074](entscheidungen/0074-betriebsinformationen-und-kontakte.md)) liest, wer `ManageLocation` hat oder Backoffice ist (`UserAccess.SeesManagementContacts`); es gibt dafür kein eigenes Recht.

`AlsoAllow(alternative, insteadOf)` lässt an einem einzelnen Endpunkt zusätzlich zum Gruppenrecht ein zweites Recht zu (so liest das Backoffice einzelne Abfragen der Kassa-, Dienstplan- und Stammdaten-Gruppen, ohne deren Schreib-Endpunkte zu erreichen). Berichte mit Beträgen prüft der Berichtsdienst: ohne `ViewAmountReports` fehlen die Beträge in der Antwort (`AmountsHidden`).

## Client gegen API

Die Windows-App liest und schreibt nur über die API ([ADR 0084](entscheidungen/0084-alle-masken-nativ-web-fuer-dashboard-und-handy.md)). Jede Maske folgt denselben Schritten:

1. **Endpunkt.** Die Logik und die Rechte liegen auf dem Server: neuer Endpunkt unter `/api/…` mit `RequirePermission(AccessRight...)`. Clients rufen ihn unter `/api/v1/…` auf ([ADR 0085](entscheidungen/0085-api-version-in-den-pfaden.md)). Die Oberfläche blendet nur aus und rechnet nichts nach.
2. **Aufruf über den Client.** Das ViewModel bekommt `IWorkstationApi` (Windows-App, erweitert `IAispaApi` aus `AispaStudio.Client`) und ruft `GetAsync`, `PostAsync` usw. auf. Es baut keinen eigenen `HttpClient`, verlangt nie den konkreten `ApiClient` und greift nie auf die Datenbank zu. Der Client liefert ein `ApiResult<T>` mit `Value`, `Error` und Status; er wiederholt keine Aufrufe.
3. **ViewModel asynchron.** Laden und Speichern sind `async Task`-Methoden mit `.ConfigureAwait(true)`, damit der Rest auf dem Oberflächen-Thread läuft. Mehrere Abfragen starten gemeinsam und warten mit `await Task.WhenAll(…)`; erst danach darf `task.Result` gelesen werden. Blockierendes Warten (`.Wait()`, `.GetAwaiter().GetResult()`, `.Result` auf einem offenen Task) ist in `ViewModels/` verboten.
4. **Lade- und Fehlerzustand.** Neue Masken führen `IsLoaded` und `LoadError` (mit `HasLoadError`) wie `AbsenceRequestsViewModel`; die Ansicht zeigt den Fehler in einer `ui:InfoBar` und nicht als nackten `TextBlock`. Ein leerer Zustand ist erst nach dem Laden „leer“ (`IsEmpty`).
5. **Konflikt.** Der Stand steht im Vertrag, nicht in einem ETag: `AppointmentDto.Version` kommt mit dem Termin, ändernde Anfragen schicken ihn als `ExpectedVersion` zurück. Weicht der Stand ab, antwortet die API mit 409 und dem aktuellen Termin in `BookingRejected.Current`; die Maske zeigt den Konflikt und den neuen Stand. Version 0 heißt „unbekannt“ und wird nicht geprüft; ohne `ExpectedVersion` ändert der Server wie bisher. Andere Entitäten bekommen dasselbe Muster, sobald dort ein Konflikt stört.
6. **Live-Update.** Der Server meldet jede Änderung per SignalR, auch an den auslösenden Arbeitsplatz. Die Maske überspringt einen gleichen Stand (`CalendarViewModel.IsSameState`) und baut das Terminfenster nicht neu auf, solange dort eine Karte bearbeitet wird (`IsEditingCard`, Slice 119), damit nichts Eingetipptes verloren geht.

Zwei Regeln sichert `tests/AispaStudio.Desktop.Tests/ClientArchitectureTests.cs`: Die Projekte der Windows-App (`Desktop`, `Client`, `Contracts`, `Help`) verweisen weder auf EF Core noch auf Npgsql noch auf `AispaStudio.Api`, und in `ViewModels/` wartet nichts blockierend auf einen Task. Begründete Ausnahmen, die der zweite Test nicht erfasst, weil sie außerhalb der ViewModels liegen: `App.axaml.cs` stoppt beim Beenden die lokale API mit `GetAwaiter().GetResult()`, damit sie geordnet endet, bevor der Prozess schließt; `ScheduledBackup.RunFromCommandLine` läuft aus `Program.Main` ohne Fenster und ohne Oberflächen-Thread. `LocalServer` wartet in `Task.Run` auf das Ende der Prozessausgabe (`CountdownEvent`), nicht auf einen Task. In `ViewModels/` liest `SubscriptionsViewModel` die Eigenschaft `Result` eines Ergebnisses (`imported.Result`), die kein Task ist; der Test kennt sie als einzige Ausnahme.

## Muster und Regeln

- **Webmodule zuschaltbar.** Endpunkte eines Webmoduls hängen in `Program.cs` an `app.ForModule(AppModule.X)` (oder einzeln `.RequireModule(...)`), Hintergrundläufe nur bei `modules.IsOn(...)` registrieren und Spas überspringen, deren Abo-Stufe das Modul nicht enthält (`Plans.With(...)`, [ADR 0050](entscheidungen/0050-abo-stufe-schaltet-webmodule-frei.md)). Der Kern hängt nie an einer Stufe. Neue Kundenbereichs-, Kanal- oder KI-Funktionen gehören in ein Modul, nicht in den Kern ([ADR 0041](entscheidungen/0041-einzelplatz-standalone-und-zuschaltbare-webmodule.md)).
- **API-Version im Pfad.** Die Team-API heißt nach außen `/api/v1/…`; ein Startfilter (`ApiVersion`) schreibt das vor dem Routing auf die Endpunkte unter `/api/…` um, die ohne Version als Alias für v1 erreichbar bleiben. Neue Endpunkte werden weiter unter `/api/…` registriert; Breaking Changes nur über eine neue Version. Kundenbereich (`/api/online`), Webhooks (`/api/hooks`) und Betreiber (`/api/operator`) haben keine Version ([ADR 0085](entscheidungen/0085-api-version-in-den-pfaden.md)).
- **Server prüft alles.** Jeder neue Endpunkt bekommt `RequirePermission(AccessRight...)`; der Filter löst den Standort aus Route oder Body auf. Die Oberfläche blendet nur aus.
- **Mandant immer gesetzt.** Neue Entities in `AispaStudio.Api` bekommen `TenantId` automatisch. Hintergrundjobs ermitteln den Mandanten aus ihren Daten (`TenantModel.TenantOfAsync`) und rufen `TenantContext.Use(id)`. Ohne Mandant wirft `SaveChanges`.
- **Langsames über die Queue.** Mails, KI, externe Dienste und Live-Benachrichtigungen laufen als Wolverine-Nachrichten. Handler-Klassen müssen `public` sein und auf `Handler` enden, sonst werden sie still ignoriert; Dienste über `IServiceScopeFactory`.
- **Austauschbar hinter Schnittstellen.** Echte Dienste ersetzen nur eine Schnittstelle: `IRksvSignatureDevice`, `ITseClient`, `IOnlinePaymentProvider`, `IBenchmarkPeers`, `ISpeechToText`, `IAnamnesisReader`, `IBookingAssistant`, `IMailSender`, `ITextMessageSender`, `IMailbox`, `ISpaMailboxFactory`. Ohne Konfiguration läuft eine Test- oder regelbasierte Variante.
- **KI nur mit Schlüssel.** Claude läuft, sobald `Ai:Claude:ApiKey` gesetzt ist (gemeinsamer Aufruf `ClaudeJson`), sonst regelbasiert. Kein Schlüssel im Repository.
- **Zeiten.** API in UTC, Anzeige in der Zeitzone des Standorts.
- **Kassenbelege unveränderlich.** Belege entstehen in einer Transaktion mit `SELECT FOR UPDATE` auf der Kasse; Korrektur nur per Storno.
- **Gesundheitsdaten getrennt.** AES-256-GCM im Schema `health`, Schlüssel `HealthData:Keys`; Berichte sehen nur den Stand, nie den Inhalt; Antworten und fotografierte Bögen nur mit dem Recht `ViewHealthData`, das jede Rolle mit Kundenzugriff hat, nicht das Backoffice (ADR 0013, Nachträge).
- **API-Vertrag committet.** `dotnet build` schreibt `web/openapi/aispa-api.json`, `npm run gen:api` erzeugt `web/src/api/schema.d.ts`; beides wird committet und von der CI geprüft.
- **Migrationen.** Zwei EF-Kontexte: `AispaDbContext` (`Data/Migrations`) und `OnlineDbContext` (`Online/Migrations`). Ein Test schlägt fehl, wenn eine Migration fehlt. Alle Ids mit `ValueGeneratedNever`, sonst macht EF bei vorab vergebener Guid ein UPDATE.
- **Web-Budget.** Der Kundenbereich bleibt unter 100 kB gzip und lädt nichts aus dem Team-Bundle (`npm run check:bundle`); axe-core prüft die Barrierefreiheit in Vitest.
- **Weichzeichner nur hinter Overlays.** In der Windows-App verschwimmt Inhalt nur, solange ein Overlay davor liegt (Abmelde-Warnung, Tastenhilfe; Klassen `blurhost`, `blurred`, `scrim` in `Themes/Styles.axaml`), nie auf Arbeitsflächen wie Kalender, Kassa, Belegen, Kundenkarte oder Posteingang.
