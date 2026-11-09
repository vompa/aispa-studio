# Arbeitsanweisung: Seite „Verlauf“ – alles, was im Betrieb passiert ist, durchsuchbar

**Für:** Claude Code im Repository `vompa/aispa-studio`
**Stand:** 09.11.2026, Auftrag des Auftraggebers nach der Live-Sichtprüfung (PRs #11 bis #17 auf `dev`)
**Ablauf:** Code Guardian nach [docs/arbeitsweise.md](../arbeitsweise.md) Abschnitt 9; eine Sitzung arbeitet die Aufgaben nacheinander in einem Worktree ab, ein Squash-Commit je Aufgabe, **kein Push, kein PR** (das entscheidet der Auftraggeber nach der Fertigmeldung). Modellwahl je Aufgabe wie in Abschnitt 3; Subagenten bekommen das günstigste Modell, das die Teilaufgabe zuverlässig schafft.

---

## 1. Ziel

Rezeption und Inhaberin sollen auf einer Seite sehen, was im Betrieb passiert ist: Buchungen, Verschiebungen, Stornos, Status (eingecheckt, erledigt, nicht erschienen), Zahlungen zum Termin (bar, Karte, Gutschein, Anzahlung, Beleg storniert), neue Kunden und Änderungen an Kundendaten, Behandlungen und Preisen, Dienstplan und Zugängen. Sie können nach Zeitraum, Art, Person, Kanal und Gast filtern, frei suchen und von jedem Eintrag zum Termin oder zur Kundenkarte springen. Die Seite sieht aus und bedient sich wie der Rest der App.

Die Grundlage gibt es: Seit [ADR 0082](../entscheidungen/0082-aenderungsverlauf-ueber-den-aenderungs-trigger.md) schreibt ein Trigger jede Änderung an den Tabellen in `ChangeHistory.Tables` nach `sync.history` (Tabelle, Schlüssel, Mandant, Gast, Art I/U/D, Zeitpunkt, Person, Kanal, alte und neue Werte). `EntityHistory` und `AppointmentHistory` machen daraus Sätze, aber nur je Datensatz (`GET /api/customers/{id}/history`, `GET /api/appointments/{id}/history` usw.). Zahlungen stehen nicht im Trigger-Verlauf, sondern in der Kassa (`Bill`, `BillLine.AppointmentId`, `Bill.Payments`, Anzahlungen in `DepositReturns`).

**Erfolg ist erreicht, wenn:**

1. `GET /api/locations/{locationId}/history` liefert die Einträge eines Zeitraums seitenweise, neueste zuerst, mit Art, Satz, Gast, Person, Kanal und Verweis auf das Objekt; Filter nach Arten, Person, Kanal, Gast und Suchtext; wer welche Arten sieht, folgt den Rechten der bestehenden Verlauf-Endpunkte (Abschnitt 2).
2. Zahlungen zum Termin erscheinen als eigene Art („Bezahlt bar: 85,00 €“, „Bezahlt Karte + Gutschein: 120,00 €“, „Anzahlung 30,00 € bar“, „Beleg storniert“) in der Verlaufsseite **und** im Verlauf des Termin-Panels.
3. In der App gibt es den Navigationseintrag **Verlauf** mit Suchfeld, Zeitraum-Chips, Art-Chips, Auswahl Person und Kanal, einer nach Tagen gruppierten Liste mit Nachladen beim Scrollen und einem Seitenpanel je Eintrag mit allen Werten alt → neu und den Sprüngen „Termin öffnen“, „Kundenkarte öffnen“, bei Stornos „Stornierung zurücknehmen“.
4. In der Kalender-Kopfzeile steht neben „Warteliste n“ der Zähler „Storniert n“ für den angezeigten Tag; er öffnet den Verlauf mit vorgewähltem Filter „Stornos, dieser Tag“.
5. Jeder Eintrag ist verlinkt: zum Gast (Kundenkarte), zum Termin (Kalender, auch bei stornierten Terminen) und zur Rechnung (Beleg in der Kassa). Aus dem Verlauf lässt sich eine Stornierung zurücknehmen; ist die Zeit inzwischen belegt oder liegt der Termin in der Vergangenheit, führt die App ohne Umweg zur Wahl einer neuen Zeit und nimmt die Stornierung zu dieser Zeit zurück (Aufgabe E).
6. Hilfe: eine Seitenbeschreibung `seite-verlauf.md` und ein Ablauf `verlauf-suchen.md`, Handbuchbilder in Hell, Verweise aus `kalender-termin-verlauf.md`, `kalender-termin-stornieren.md` und `kunden-kundenkarte.md`.
7. Alle bestehenden Tests bleiben grün; jede Regel (Rechte, Filter, Suche, Seitenweise) hat einen Test, der ohne die Regel rot ist; die Ansicht hat Tests in Hell und Dunkel; `DesignTokenTests`, `TermsAndTooltipTests`, `GridValueTests` und die AXAML-Regeltests laufen grün.

**Nicht Teil dieser Anweisung:** Anfragen (Mail-Eingang, Treatwell), Lagerbewegungen, Kassen-Einlagen und -Entnahmen ohne Terminbezug, Abgleich-Vorgänge, Export (CSV, Druck), Anonymisieren im Verlauf, Verlauf im Web. Alles davon nur im PR-Protokoll als „bewusst offen“ nennen.

## 2. Leitplanken für alle Aufgaben

- `CLAUDE.md` gilt vollständig: englische Namen im Code, Deutsch in Oberfläche, Doku und Commits; Warnungen sind Fehler; Texte nach [docs/gestaltung/texte.md](../gestaltung/texte.md), Meldungen nach [docs/gestaltung/meldungen.md](../gestaltung/meldungen.md), Masken nach [docs/gestaltung/masken.md](../gestaltung/masken.md), Gestaltung nach [docs/design.md](../design.md) (Listen 5.6, Chips und Pillen 5.8, Seitenleiste 5.12 mit der Warteliste als Vorbild, Navigation 5.13). Nie der Name des Auftraggebers im Repository, nur „der Auftraggeber“.
- **Chirurgisch:** Neue Dateien statt Umbauten; bestehende Verlauf-Klassen (`EntityHistory`, `AppointmentHistory`) erweitern, nicht ersetzen. Die Satzbildung (`Describer`) wird wiederverwendet, damit Termin-Panel, Kundenkarte und Verlaufsseite denselben Wortlaut zeigen. Toten Code melden, nicht löschen.
- **Rechte wie bisher** (`HistoryEndpoints`, ADR 0082 Punkt 5): Termine, Kunden, Behandlungen sieht, wer `ViewCalendar` hat; Dienstplan, Zugänge und Firmendaten nur `IsOwnerLevel` (Inhaber, Manager); Gesundheitsdaten (`health`) und Personalakte (`hr`) nie mit Inhalten, nur „geändert von … am …“; Zahlungen sieht, wer die Kassa-Seite sieht (dasselbe Recht wie die Lesezugriffe unter `/api/pos`). Fehlt ein Recht, fehlen die Arten in der Antwort **und** die Chips in der App; eine Rezeption sieht keinen leeren Filter „Dienstplan“.
- **Mandant und Standort:** Der Mandantenfilter gilt wie überall; der Standort schränkt Termine und Zahlungen ein, Kunden, Behandlungen und Zugänge sind mandantenweit und erscheinen an jedem Standort.
- **Server zuerst, Oberfläche folgt:** Filtern, Suchen und Seitenweise rechnet die API; die App zeigt nur an. Keine Suche über alle geladenen Einträge im Client.
- **Prüfen** nach Abschnitt 9: Basisprüfung (`dotnet format … --verify-no-changes`, `dotnet build … -c Release`), zuständige Testklassen der API (`tests/AispaStudio.Api.Tests`) und der Windows-App (`tests/AispaStudio.Desktop.Tests`, `ViewTests` nur die eigenen Methoden plus die Regeltests), Hilfe `npm run test` im Ordner `web`, Handbuchbilder mit `scripts/help-images.sh`. Läufe über 2 Minuten im Hintergrund mit Protokoll in `.test-logs/`. Der API-Testkalender liegt im März 2027 (`TestSpa.Monday`), Zeitfilter darum relativ zum Testdatum bauen.
- **Prüfen als Rolle:** Bildtests und Durchsichten als Inhaberin (alle Arten) **und** als Rezeption (nur Termine, Kunden, Zahlungen); beide Zustände als Ansichtstest.
- **Abschluss je Aufgabe:** PR-Protokoll-Abschnitt in `docs/umsetzung/pr-protokoll.md` (Auftrag, Was, Detailentscheidungen, Geprüft, Nicht geprüft, Text-Prüfung), Handtestblock in `docs/umsetzung/handtests.md`, Hilfe im selben Commit, ADR 0082 unter „Folgen und Stand“ ergänzen (Aufgabe A).
- **Fragen:** Bei einer offenen Entscheidung nicht raten, sondern Optionen mit Empfehlung an die beauftragende Sitzung stellen und weiterarbeiten, wo es ohne Antwort geht.

## 3. Aufgaben im Überblick

| Nr. | Aufgabe | Modell | Hängt ab von |
|---|---|---|---|
| A | API: Verlauf je Standort mit Filtern, Suche, Seiten und Zahlungen; Zahlungen auch im Termin-Verlauf | opus | – |
| B | App: Seite „Verlauf“ mit Suchfeld, Chips, Liste nach Tagen, Nachladen | sonnet | A |
| C | App: Seitenpanel je Eintrag mit Werten und Sprüngen; Zähler „Storniert n“ im Kalender | sonnet | B |
| E | Rücknahme aus dem Verlauf: belegte Zeit und Vergangenheit führen zur neuen Zeit; Sprünge zu Gast, Termin, Rechnung | opus | C |
| D | Hilfe, Handbuchbilder, Verweise, Fertigmeldung (zuletzt, nach E) | sonnet | C, E |

## Aufgabe A: API-Endpunkt Verlauf je Standort

**Was:**

- Vertrag in `src/AispaStudio.Contracts/HistoryContracts.cs`: `HistoryEntry` bleibt für die bestehenden Endpunkte unverändert. Neu `ActivityEntry(long Seq, DateTimeOffset At, ActivityKind Kind, string Text, string? Actor, ActivityChannel Channel, Guid? CustomerId, string? CustomerName, Guid? AppointmentId, Guid? TreatmentId, Guid? EmployeeId, IReadOnlyList<ActivityValue> Values, bool? LateCancellation)` mit `ActivityValue(string Label, string? Old, string? New)`; `ActivityKind`: `Booked, Moved, Cancelled, Restored, StatusChanged, AppointmentChanged, Paid, Deposit, ReceiptVoided, CustomerCreated, CustomerChanged, TreatmentChanged, RosterChanged, AccessChanged, CompanyChanged`; `ActivityChannel`: `Reception, Online, Assistant, Background` (Zuordnung aus `channel` und `origin` der Verlaufszeile wie in `AppointmentHistory.Describer`). Antwort `ActivityPage(IReadOnlyList<ActivityEntry> Items, long? NextBefore)`.
- Endpunkt `GET /api/locations/{locationId:guid}/history` in einer neuen Datei `src/AispaStudio.Api/Sync/ActivityEndpoints.cs` mit Query `from`, `until` (Ortszeit des Standorts, Tagesgrenzen wie der Kalender), `kinds` (mehrfach), `actor` (User-Id), `channel`, `customerId`, `search`, `before` (Seitenzeiger), `take` (Standard 50, höchstens 200). Recht `ViewCalendar` für die Gruppe; Arten außerhalb der Rechte werden still weggelassen (kein 403 für die Seite, 403 nur, wenn **ausschließlich** verbotene Arten angefragt sind, mit der Meldung aus `HistoryEndpoints.LeadershipOnly`).
- Dienst `ActivityFeed` in `src/AispaStudio.Api/Sync/ActivityFeed.cs`: liest `sync.history` mit parametrisierten Bedingungen (Muster `EntityHistory.ReadAsync`: nur feste Bedingungstexte, alle Werte als Parameter), begrenzt auf den Standort (Termine über `context`/`changes` `LocationId`; Kunden, Behandlungen, Zugänge, Firmendaten mandantenweit; Dienstplan über die Personen des Standorts) und mischt die Zahlungen aus der Kassa dazu: `Bill` mit `BillLine.AppointmentId` im Zeitraum (Art `Paid`, Text „Bezahlt bar: 85,00 €“ mit `PaymentMethods.Way`, Kassierer, Belegnummer), Anzahlungen (`Deposit`), stornierte Belege (`ReceiptVoided`). Sortierung nach Zeit; der Seitenzeiger `before` ist das Paar (Zeit, Seq), Zahlungen bekommen dafür eine negative Pseudo-Seq aus ihrer Id, so dass Seiten stabil bleiben. Sätze über die vorhandenen `Describer`; den Termin-Describer so öffnen, dass er ohne Termin-Id mit den Werten aus der Verlaufszeile arbeitet (Behandlung, Gast und Zeit stehen in `context`).
- Suche: `search` trifft Gastname (Join `Customers`), Behandlungsname (Join `Treatments` über die Ids in `context`/`changes`), Personenname (Join `Users`/`Employees`), Storno-Grund (`changes->'CancelReason'`) und Belegnummer; Groß- und Kleinschreibung egal (`ILIKE`). Keine Suche im fertigen Satz.
- `LateCancellation`: bei `Cancelled` wahr, wenn die Absage innerhalb der Stornofrist des Standorts lag (`CancellationDeadlineHours` aus den Online-Einstellungen des Standorts; ohne Einstellung die 24 Stunden aus `CustomerInsightsService.LateCancellation`), gerechnet aus `changed_at` und dem Beginn des Termins.
- Termin-Verlauf (`GET /api/appointments/{id}/history`) bekommt die Zahlungen des Termins als zusätzliche `HistoryEntry`-Zeilen, zeitlich einsortiert, mit demselben Wortlaut.
- `web`: `npm run gen:api` ausführen, damit `web/openapi/aispa-api.json` und `src/api/schema.d.ts` den neuen Endpunkt kennen.

**Tests zuerst** (`tests/AispaStudio.Api.Tests/ActivityFeedTests.cs`, Muster der bestehenden Verlauf-Tests): Buchen, Verschieben, Stornieren mit Grund, Status, Kassieren bar und Karte, Anzahlung, Beleg-Storno, Kundin anlegen und ändern, Preis ändern, Dienst eintragen, Rolle ändern; dann je Filter ein Test (Zeitraum, Art, Person, Kanal, Gast, Suche nach Gast, Suche nach Grund, Suche nach Belegnummer), Seitenweise mit `take=2` über drei Seiten ohne Lücke und ohne Doppelte, Rechte (Rezeption sieht keinen Dienstplan, kein Verbot bei gemischter Anfrage, 403 bei nur verbotenen Arten), fremder Standort und fremder Mandant leer, `health` ohne Werte, `LateCancellation` an der Frist (eine Minute davor wahr, eine Minute danach falsch). Termin-Verlauf mit Zahlung: ein Test, der ohne Aufgabe A rot ist.

**Prüfen:** Basisprüfung, neue Klasse, die bestehenden Verlauf-Tests (Kunden, Termine, Endpunkte), die zuständigen Kassa-Tests, Rechte-Tests. ADR 0082 unter „Folgen und Stand“ um den Verlauf je Standort und die Zahlungen ergänzen.

## Aufgabe B: Seite „Verlauf“ in der App

**Was:**

- Navigationseintrag `new NavItem("Verlauf", Icons.History, Activity)` in `ShellViewModel` nach „Kunden“; fehlt `Icons.History`, ein Uhr-mit-Pfeil-Symbol im Stil der übrigen (`Icons.cs`, Strichstärke 1,5, Ansichtsfeld 24) ergänzen. Der Eintrag ist für alle sichtbar, die den Kalender sehen.
- `ActivityViewModel` (`src/AispaStudio.Desktop/ViewModels/ActivityViewModel.cs`, Teildateien für Filter und Laden wie bei `CalendarViewModel`) und `ActivityView.axaml`. Aufbau nach `design.md` 5.14 und dem Muster der Kundenseite: Kopfzeile mit Titel, Suchfeld (`Strg+F`, Platzhalter „Gast, Behandlung, Person oder Grund“), Zeitraum-Chips „Heute · Gestern · 7 Tage · 30 Tage · Zeitraum“ (Zeitraum öffnet zwei Datumsfelder nach 5.17); zweite Zeile Art-Chips (mehrfach wählbar) „Buchungen · Verschoben · Stornos · Status · Zahlungen · Neue Kunden · Kundendaten · Behandlungen · Dienstplan · Zugänge“ nur für die Arten, die die Antwort überhaupt enthalten darf (Rechte aus der Sitzung), dazu Auswahlfelder „Person“ und „Kanal“ und ein Chip „Zurücksetzen“, sobald ein Filter gesetzt ist.
- Liste nach Tagen gruppiert: Tageskopf „Heute, Montag, 9. November“ wie im Kalender, je Eintrag Uhrzeit (feste Spalte), Symbol der Art in Bedeutungsfarbe (Storno warn, Zahlung ok, Neu Akzent, sonst neutral), der Satz als Haupttext, darunter in `fg-2` Gast und Person („Anna Huber · von Lena · Online“), rechts Pillen „Kurzfristig“ (nur `LateCancellation`), „Online“, „Hintergrund“. Zeilen nach 5.6, Mindesthöhe 34, Pillen nach 5.8. Nachladen beim Scrollen über `NextBefore`, zusätzlich ein Knopf „Weitere laden“ am Fuß, damit es ohne Maus geht.
- Leerzustände nach `texte.md`: „Im gewählten Zeitraum ist nichts passiert.“ und bei Filtern „Nichts gefunden. Filter zurücksetzen?“ mit dem Knopf. Ladezustand wie die Kundenliste. Ohne Verbindung die übliche Meldung, keine Rückfrage.
- Tastatur: Pfeile in der Liste, Enter öffnet das Panel (Aufgabe C), `Esc` leert die Suche, `Strg+F` fokussiert sie; Tastenhilfe-Eintrag der Seite.
- `AutomationProperties.Name` an Liste („Verlauf“), Zeilen (der Satz) und Chips; Tooltips ein Satz (`TermsAndTooltipTests`).

**Tests zuerst** (`tests/AispaStudio.Desktop.Tests/ActivityTests.cs` mit `StubApi`): Laden für „Heute“ ruft den Endpunkt mit den Tagesgrenzen des Standorts; Chips setzen `kinds`; Suche mit 300 ms Verzögerung und einer Anfrage; Nachladen hängt die zweite Seite an und hält die Tagesgruppen zusammen; Rezeption sieht keine Chips für Dienstplan und Zugänge; Leerzustände. Ansichtstests `ViewTests.Activity.cs`: Seite in Hell und Dunkel mit Einträgen aller Arten, als Inhaberin und als Rezeption, Leerzustand; Regeltests grün.

**Prüfen:** Basisprüfung, neue Klassen, die Navigationstests der Shell, `TermsAndTooltipTests`, `GridValueTests`, `DesignTokenTests`.

## Aufgabe C: Seitenpanel und Storno-Zähler im Kalender

**Was:**

- Seitenpanel nach `design.md` 5.12 (Vorbild Warteliste): Kopf mit Art und Zeitpunkt, darunter der Satz, Gast mit Telefon (für den Rückruf), Person, Kanal, bei Stornos Grund und „kurzfristig“-Hinweis mit Frist und Ausfallgebühr-Regel des Standorts wie im Storno-Dialog (M3 am Objekt). Abschnitt „Werte“: Tabelle Feld · vorher · nachher aus `Values`. Knöpfe nach 5.1: „Termin öffnen“ (wechselt in den Kalender auf den Tag und wählt den Termin; einen stornierten über die Kundenkarte, weil der Kalender ihn nicht zeigt), „Kundenkarte öffnen“, bei `Cancelled` „Stornierung zurücknehmen“ (bestehende Rückfrage und Logik des Termin-Panels wiederverwenden, Erfolgs-Toast „Termin wiederhergestellt: …“ ohne Punkt, Liste lädt neu), bei `Paid` „Beleg anzeigen“ (Kassa-Seite, Beleg suchen). Knöpfe, die ein Recht brauchen, mit `ui:DisabledHint.Text`.
- Kalender-Kopfzeile: neben `Waitlist.Count` ein `OverflowStrip`-Eintrag „Storniert {0}“ mit dem Zähler der stornierten Termine des angezeigten Tags (die Daten sind im `CalendarDay` schon enthalten, die App filtert `Status != Cancelled` in `CalendarViewModel.Filter.cs`; der Zähler zählt dieselbe Menge). Klick wechselt zur Seite Verlauf mit Zeitraum „dieser Tag“ und Art „Stornos“. Bei 0 ist der Eintrag ausgeblendet. Tooltip ein Satz.

**Tests zuerst:** `ActivityPanelTests` (Panel öffnet mit Werten, Sprung in den Kalender setzt Tag und Auswahl, Rücknahme ruft `restore` und lädt neu, Rechte sperren die Knöpfe mit Hinweis), `CalendarTests` (Zähler zählt stornierte des Tags, 0 blendet aus, Klick übergibt Tag und Art). Ansichtstests Panel in Hell und Dunkel, Kalender-Kopf mit Zähler.

**Prüfen:** Basisprüfung, neue Klassen, `CalendarTests`, `CalendarKeyboardTests`, `AppointmentPanelFlowTests`, Regeltests.

## Aufgabe E: Stornierung aus dem Verlauf zurücknehmen; Sprünge zu Gast, Termin und Rechnung

**Was heute gilt:** Im Panel eines stornierten Termins fragt `AppointmentViewModel.RestoreAsync` zuerst `GET /appointments/{id}/restoration` ab (Absage-Nachricht, Anzahlung), stellt dann über `POST /appointments/{id}/restore` wieder her; antwortet die API 409 (Zeit, Person oder Raum belegt), fragt die App „Zu anderer Zeit zurücknehmen?“ und öffnet mit „Verschieben …“ die Verschieben-Karte (`IsMovingToDay`), die den `restore` mit neuem `start` schickt. Was fehlt: Der `restore`-Endpunkt prüft **nicht**, ob der Termin in der Vergangenheit liegt (`PastBookings` sitzt an Buchen und Verschieben, nicht an `restore`), die Vergangenheit erzeugt also heute keinen Konflikt, sondern einen Termin in der Vergangenheit; und aus dem Verlauf heraus gibt es den Ablauf noch nicht.

**Was:**

- **API:** `POST /appointments/{id}/restore` ohne `start` prüft `PastBookings.CheckAsync` für den Beginn des Termins (Meldung „Der Termin liegt in der Vergangenheit. Die Stornierung lässt sich nur zu einer neuen Zeit zurücknehmen.“, 409 wie bei belegter Zeit, als `BookingRejected`); mit `start` gilt `PastBookings.CheckMoveAsync` wie beim Verschieben. `GET /appointments/{id}/restoration` bekommt zusätzlich `InPast` (bool) und `Conflict` (die Belegungen, die `MoveAsync` melden würde, ohne zu speichern), damit die App **vor** der Rückfrage weiß, ob eine neue Zeit nötig ist. `npm run gen:api`.
- **App, Verlauf-Panel:** „Stornierung zurücknehmen“ nutzt denselben Ablauf wie das Termin-Panel (die Logik aus `AppointmentViewModel` in einen gemeinsamen Dienst `AppointmentRestorer` ziehen, der von beiden Panels aufgerufen wird; keine Kopie). Ist weder `InPast` noch `Conflict` gesetzt: Rückfrage „Stornierung zurücknehmen?“ mit den bekannten Hinweisen (Absage-Nachricht, Anzahlung), danach Toast „Termin wiederhergestellt: <Gast>, <Tag> <Zeit>“ und der Verlauf lädt neu. Ist eine neue Zeit nötig: Rückfrage „Zu neuer Zeit zurücknehmen?“ mit dem Grund („Die Zeit ist inzwischen belegt: …“ oder „Der Termin liegt in der Vergangenheit.“) und dem Knopf „Neue Zeit wählen“; der wechselt in den Kalender auf den nächsten freien Tag, öffnet den stornierten Termin im Panel mit offener Verschieben-Karte und zeigt darunter die Freie-Zeit-Vorschläge für Gast, Behandlung und Person (bestehende Freie-Zeit-Suche mit Vorbelegung); Bestätigen schickt `restore` mit dem neuen `start`. Bricht die Rezeption ab, bleibt der Termin storniert und der Verlauf zeigt ihn weiter.
- **Sprünge:** „Kundenkarte öffnen“ wechselt zur Kundenseite und wählt den Gast; „Termin öffnen“ wechselt in den Kalender auf den Tag des Termins und wählt ihn, bei einem stornierten Termin öffnet es das Panel des stornierten Termins (wie über die Kundenkarte, Hilfe `kalender-termin-stornieren.md` Abschnitt „zurücknehmen“); „Beleg anzeigen“ wechselt in die Kassa, Reiter Belege, auf den Tag des Belegs und wählt ihn (Beleg-Id aus dem Eintrag). Einträge zu Kunden ohne Termin zeigen nur „Kundenkarte öffnen“; Einträge zu Behandlungen „Behandlung öffnen“ (Stammdaten), zu Dienstplan „Dienstplan öffnen“ auf der Woche, zu Zugängen „Zugang öffnen“, jeweils nur mit dem passenden Recht (`ui:DisabledHint.Text` sonst).
- Hilfe (in D): Abschnitt „Aus dem Verlauf zurücknehmen“ in `verlauf-suchen.md` und ein Satz in `kalender-termin-stornieren.md`, dass ein vergangener Termin nur zu einer neuen Zeit zurückkommt.

**Tests zuerst:** API `PastBookingTests`: Rücknahme eines gestrigen Termins ohne `start` ergibt 409 mit der Meldung; mit `start` morgen klappt sie; `restoration` meldet `InPast` und `Conflict` (Test mit belegter Zeit durch einen zweiten Termin). App `ActivityPanelTests`: frei → Rückfrage → `restore` → Toast und Neuladen; belegt → Rückfrage „Zu neuer Zeit zurücknehmen?“ → Kalender mit Verschieben-Karte und Vorschlägen; Abbruch lässt alles stehen; Sprünge setzen Seite, Tag und Auswahl; Rechte sperren mit Hinweis. `AppointmentPanelFlowTests`: der bestehende Ablauf im Termin-Panel ist unverändert (gleicher gemeinsamer Dienst). Ansichtstest der Rückfrage mit Grund in Hell und Dunkel.

**Prüfen:** Basisprüfung, `PastBookingTests`, `AppointmentRestoreTests` (oder die zuständige Klasse der Rücknahme), `ClosureCancellationTests` (Absage-Nachricht bleibt korrekt), `AppointmentPanelFlowTests`, `CalendarTests`, Regeltests. PR-Protokoll mit der Entscheidung, dass die Vergangenheit am `restore`-Endpunkt sitzt (wie `PastBookings` sonst) und nicht im `BookingService`.

## Aufgabe D: Hilfe und Fertigmeldung

**Was:**

- Aufgabe D läuft zuletzt, nach E.
- `web/src/help/content/seite-verlauf.md` (Frontmatter wie `seite-kunden.md`, Rollen `rezeption, leitung, inhaber, manager`) mit Aufbau, Arten, Rechten („Was die Rezeption sieht“), Pillen; `verlauf-suchen.md` als Ablauf (Zeitraum wählen, Filter setzen, suchen, Eintrag öffnen, zum Gast, Termin oder Beleg springen, Stornierung zurücknehmen, auch zu neuer Zeit) mit Beispielen aus den Beispieldaten; Verweise aus `kalender-termin-verlauf.md` (Zahlungen im Verlauf, Link zur Seite), `kalender-termin-stornieren.md` (Storno-Zähler, Rückrufe über den Verlauf), `kunden-kundenkarte.md`, `seite-kalender.md` (Zähler „Storniert“). Stichwörter: Verlauf, Protokoll, Historie, Journal, Aktivitäten, wer hat was gemacht, Stornos heute, bezahlt bar, bezahlt Karte.
- Handbuchbilder in Hell über `scripts/help-images.sh` als Inhaberin (Seite mit Filtern, Panel eines Stornos, Kalender-Kopf mit Zähler), Größe unter 300 KB (`help-images-shrink.py`).
- `npm run test` im Ordner `web` (Textregeln der Hilfe), Linkprüfung.
- Fertigmeldung nach Abschnitt 4.

## 4. Fertigmeldung

An die beauftragende Sitzung, in dieser Reihenfolge: Branch und Commits je Aufgabe; was geprüft wurde (Testklassen mit Zahlen, Bildtests in beiden Rollen); was **nicht** geprüft wurde; offene Fragen mit Empfehlung; bewusst offen gelassene Punkte aus Abschnitt 1. Dazu eine Übergabenotiz im Notizordner der Sitzungsfamilie, wenn einer genannt ist.
