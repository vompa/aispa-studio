# Einspielen der Beispieldaten: Messung

Ziel: Das Einspielen des Beispiel-Spas (`Database:SeedDemoData`, Standard 8 Wochen zurück und 4 voraus) soll in Sekunden bis wenigen zehn Sekunden gehen. Gemessen mit dem Benchmark-Test `DemoBenchTests` (läuft nur mit `AISPA_DEMO_BENCH=1`, eigene Datenbank in einem Testcontainer `postgres:17-alpine`, Windows 11, Docker Desktop, Release-Build). Die Zeiten schwanken mit der Last des Rechners (andere Builds liefen nebenher) um etwa ±15 %.

Aufruf:

```bash
AISPA_DEMO_BENCH=1 dotnet test --project tests/AispaStudio.Api.Tests -c Release --filter-method "*Einspielen_mit_Standardwerten*"
```

Das Ergebnis steht in der Datei, die `AISPA_DEMO_BENCH_OUT` nennt (Standard: `aispa-demo-bench.txt` im Temp-Ordner). Die API schreibt dieselben Zeiten je Abschnitt nach dem Einspielen ins Protokoll (Stufe Information, „Beispieldaten, Zeit: …“).

## Phase 1: Stand vorher (Weg „Services“, Dienste wie im Betrieb)

Gesamt **641 bis 762 s** (drei Läufe), danach beschäftigt die Queue noch 20 bis 204 s.

| Abschnitt | Sekunden | Aufrufe |
| --- | ---: | ---: |
| Stammdaten, Mandant, Standorte | 1,3 | 1 |
| Kassa-Artikel, Lagerartikel, Personal, Preisregeln | 2,2 | 4 |
| Konten | 2 bis 5 | 1 |
| **Verlauf gesamt** | **496 bis 585** | 1 |
| davon Buchen (Tag × Standort) | 222 bis 286 | 207 |
| davon freie Zeiten laden und planen | 97 bis 132 | 3.648 |
| davon `BookAsync` | 102 bis 135 | 2.443 |
| davon Status, Absage, Check-in | 33 bis 42 | 414 |
| davon Kassieren (Tag × Standort) | 273 bis 298 | 139 |
| davon Termine kassieren | 223 bis 240 | 1.749 |
| davon Gutscheine verkaufen | 19 bis 23 | 175 |
| davon Artikel und Saunaeintritt | 24 bis 28 | 252 |
| davon Tagesabschluss | 6,5 bis 7,1 | 136 |
| Extras (Anamnesen, Konten, Posteingang …) | 2 | 1 |
| **Lagerbewegungen** (Verbrauch und Verkäufe nachbuchen) | **134 bis 171** | 1 |

Datenbankzugriffe im Lauf: **250.000 bis 278.000 Befehle** in **14.700 bis 19.100** `SaveChanges`-Aufrufen (rund 17 Befehle je Speichern). Ein einfacher `SELECT 1` braucht in dieser Umgebung **1,6 ms** (Docker-Desktop-Portweiterleitung); 250.000 Befehle × 1,6 ms sind rund 400 s, also mehr als die Hälfte der Gesamtzeit reine Wartezeit auf die Datenbank.

Erzeugte Zeilen: Lagerbewegungen 8.033 (nach Löschen und Neubuchen 8.000 übrig), Belegungen 5.330, Belegzeilen 2.478, Termine 2.443, Rechnungszeilen 2.383, Zahlungen 2.336, Belege 2.213, Rechnungen 2.178, Online-Buchungen 532, Kunden 261, Gutscheine 177, Nachbereitungen 171, Tagesabschlüsse 136, Kassenbewegungen 136, dazu rund 400 Stammdatenzeilen. Insgesamt rund 30.000 Zeilen.

### Hat die Queue Einfluss?

| Frage | Befund |
| --- | --- |
| Laufen Wolverine-Nachrichten an? | Ja. Jede Buchung und jede Statusänderung (`BookingService`) stellt eine `AppointmentChangedMessage` in die Queue. Daraus entstehen Lagerabgleich, Warteliste-Prüfung, Serienprüfung und SignalR-Meldung: **11.398 Nachrichten** (`wolverine_incoming_envelopes`) für 2.443 Termine. |
| Nutzen? | Keinen. Das Einspielen gleicht das Lager selbst ab (löscht alle Bewegungen und bucht neu); der Abgleich der Queue lief daneben mit der echten Uhr und gegen dieselben Zeilen. SignalR und Warteliste ohne Zuhörer sind Lärm. |
| Kosten | Der Verlauf selbst wurde nicht messbar langsamer (496 s mit Queue, 545 bis 585 s ohne; die Schwankung des Rechners ist größer). Der **Rückstau** nach dem Verlauf kostet aber **204 s**, in denen die API „Beispiel-Spa wird eingerichtet“ nicht verlassen darf, weil Wolverine die Nachrichten mit denselben Verbindungen abarbeitet. Ohne diese Nachrichten bleiben 39 (die gewollten aus `DemoExtras`). |
| Maßnahme | `BookingService` bekommt beim Einspielen einen stummen Nachrichtenbus (`SilentMessageBus`). |

### Kosten der Abgleich-Trigger (Schema `sync`)

Dieselbe Änderung aller Zeilen in einer zurückgerollten Transaktion, mit und ohne Trigger:

| Tabelle | Zeilen | mit Trigger | ohne Trigger |
| --- | ---: | ---: | ---: |
| `ReceiptLine` | 2.483 | 95 ms | 40 ms |
| `ResourceBookings` | 5.017 | 353 ms | 219 ms |
| `Appointments` | 2.452 | 186 ms | 102 ms |

Rund 30 bis 50 µs je Zeile; bei 30.000 Zeilen insgesamt etwa 1 bis 2 s. **Die Trigger sind nicht der Engpass.**

### Parallele Hintergrunddienste (Beobachtung unter Aspire: über 8 Minuten statt 3)

Unter Aspire (Debug-Build, Debugger) liefen mit dem Stand 2be9d32 die übrigen Dienste neben dem Einspielen: Wolverine-Queue (siehe oben, der größte Posten), Erinnerungen, Wiedervorlage, Zugangsablauf, Kartenaufräumung, Freigabe ungesicherter Buchungen, Postfachabruf und Abgleich. Die getakteten Dienste fragen ohnehin nur alle 30 s bis 1 h und lesen Fachdaten mandantenweit; im Test (Takte abgeschaltet) sind sie nicht enthalten. Unabhängig vom Umbau warten sie jetzt bis `StartupState` = Ready (`StartupState.WaitUntilReadyAsync`, vor jedem Lauf).

### Entscheidung am Prüfpunkt

Es gibt **keinen einzelnen Engpass**: kein fehlender Index, keine auffällige N+1-Abfrage (die ~17 Befehle je Speichern sind das Laden von Kontext, Kasse, Beleg und Zeilen je Fachvorgang), die Trigger kosten unter 1 %, die Queue nur ihren Rückstau (behoben). Dominiert wird die Zeit vom **einzelnen Speichern und den Roundtrips** (250.000 Befehle mit je 1,6 ms Wartezeit plus EF-Overhead). Auch eine schnellere Datenbank würde ihn höchstens halbieren. Damit ist **Phase 2** (Generator im Speicher, Einspielen per `COPY`) nötig.

## Phase 2: Verlauf im Speicher, Einspielen per COPY

Gebaut (`DemoData:Mode`, Standard `Bulk`; `Services` bleibt als alter Weg wählbar):

- **Ein Ablauf, zwei Umsetzungen.** `DemoHistory` (Buchen, Absagen, Kassieren, Gutscheine, Storno, Kassenlade, Tagesabschluss, mit dem festen Zufall `Random(1810)`) läuft unverändert gegen die Schnittstelle `IDemoOps`. `ServiceDemoOps` reicht an `BookingService` und `PosService` weiter (der alte Weg), `MemoryDemoOps` rechnet im Speicher (`Demo/Memory`).
- **Fachlogik aufgerufen, nicht nachgebaut.** Aus den Diensten sind reine Rechenschritte herausgelöst, die Dienst und Speicher gemeinsam nutzen: `BookingService.NewAppointment/ApplyPlan/ResourcesAt/BookingWindow`, `RosterService.ShiftsOf`, `CustomerRules.From`, `PosService.NewBillLine/DiscountLines/BuildCheckoutAsync/BuildVoidAsync/IssueCoreAsync/BuildDayReport/NewDayClosing`, `StockService.SharesOf/Allocate/AverageCost`. Der `ResourcePlanner` war schon rein. Die Tests der Dienste blieben unverändert grün. Im Speicher bleiben nur das Nachschlagen (statt Abfragen) und das Zurückrollen bei einem abgelehnten Vorgang, das sonst die Transaktion übernimmt.
- **Lager** (`DemoStockBulk`): dasselbe Vorgehen wie `DemoStock.AddMovementsAsync` (Bedarf ermitteln, Wareneingänge mit Charge zum Beginn des Verlaufs, Verbrauch und Verkäufe nach „zuerst ablaufend“ buchen) mit einem Hauptbuch im Speicher. Die Bestellungen (`AddOrdersAsync`) laufen danach unverändert über die Dienste.
- **COPY** (`BulkWriter`): Npgsql-Binärimport je Tabelle in Reihenfolge der Fremdschlüssel (Termine, Belegungen, Rechnungen, Belege, Gutscheine, Belegzeilen und Zahlungen, Kassenbewegungen, Abschlüsse), alles in einer Transaktion zusammen mit dem Stand der Kassen. Spalten, Typen, Wertumwandlungen und die Schattenspalte `TenantId` kommen aus dem EF-Modell; `xmin`, berechnete Spalten und Zeilenversionen schreibt die Datenbank selbst.
- **Abgleich-Trigger** feuern bei `COPY` wie bei `INSERT`. `DemoBulkTests` prüft, dass jede eingespielte Zeile der Fachtabellen als Einfügung mit Mandant im Änderungsprotokoll steht.
- **Beträge** bringt `DbScale` auf die Nachkommastellen der Spalte (20 wird 20,00), weil der Tagesabschluss seine Zahlen als Text festschreibt und dort sonst Unterschiede in der Schreibweise stünden.
- **Reproduzierbar gemacht:** Die Kundenliste des Verlaufs ist jetzt nach Datum, Nachname und Vorname sortiert. Vorher entschied bei gleichem Datum (kam vor) die Datenbank, damit zwei Läufe nicht dieselben Gäste buchten.

Vorher und nachher (Standardwerte 8/4 Wochen, Release-Build, Testcontainer):

| Abschnitt | Services (vorher) | Bulk (nachher) |
| --- | ---: | ---: |
| Stammdaten, Artikel, Personal, Preisregeln, Konten | 7 bis 9 s | 4,8 s |
| Dienstplan | 0,2 s | 0,2 s |
| Buchen (3.648 Planungen, 2.450 Termine, Status) | 222 bis 286 s | 3,3 s |
| Kassieren (1.760 Termine, Gutscheine, Artikel, Abschlüsse) | 273 bis 298 s | 4,2 s |
| Schreiben (COPY, eine Transaktion) | – | 2,6 s |
| Extras (Anamnesen, Konten, Posteingang) | 2 s | 3,1 s |
| Lagerbewegungen | 134 bis 171 s | 1,7 s |
| **Gesamt** | **641 bis 762 s** | **21,9 s** |
| Datenbankbefehle | 250.000 bis 278.000 | 1.372 |
| `SaveChanges`-Aufrufe | 14.700 bis 19.100 | 195 |

Mit Debug-Build (ohne Debugger) 18,5 s. Unter dem Visual-Studio-Debugger sind Schleifen mit LINQ deutlich langsamer; die Zeit dort wurde nicht gemessen. Die Wartezeit auf die Datenbank spielt kaum noch eine Rolle (1.372 statt 250.000 Befehle). Der größte Posten sind jetzt die Stammdaten und Konten (rund 5 s), die weiter über die Dienste entstehen, und der Planer mit 3 s für 3.648 Berechnungen.

### Vergleichstest

`DemoBulkTests.Einspielen_im_Speicher_ergibt_dieselben_Daten_wie_ueber_die_Dienste` spielt dasselbe Spa (eine Woche zurück, eine voraus, gleiche Uhr) auf beiden Wegen in zwei Datenbanken ein und vergleicht je Tabelle jede Zeile ohne Ids und Zufallswerte (Gutscheincodes, Signaturen): Termine, Belegungen, Online-Buchungen, Nachbereitungen, Rechnungen mit Positionen, Belege mit Zeilen und Zahlungen, Gutscheine, Kassenbewegungen, Tagesabschlüsse samt festgeschriebenem Bericht, Stand der Kassen und alle Lagerbewegungen mit Charge. Dazu: lückenlose, gültig signierte Belegkette je Kasse im Datenerfassungsprotokoll (Signatur gegen das Zertifikat und Verkettung zum Vorgänger), Einfügungen im Änderungsprotokoll, gleiche Management-Berichte je Standort. Das Ergebnis ist **identisch**, es gibt keine Toleranz. Was die Queue nach dem Einspielen mit der echten Uhr anlegt (Treatwell-Termine, online gekaufte Gutscheine), gehört nicht zum Verlauf und ist ausgenommen.

### Entscheidungen, Grenzen, Risiken

- **Kein JSON-Zwischenspeicher** (`.aispa-backups/demo-JJJJ-MM-TT.json`): Das Erzeugen im Speicher dauert rund 6 s; ein Zwischenspeicher sparte davon wenig und müsste bei jeder Änderung von Datenmodell oder Fachlogik erneuert werden. Der Ordner `.aispa-backups` steht auf `dev` schon in `.gitignore`.
- **Atomar ist der Verlauf, nicht das ganze Einspielen.** Stammdaten, Konten, Extras und die Bestellungen legen weiter die Dienste in eigenen Transaktionen an; der Verlauf und das Lager gehen je in einer Transaktion in die Datenbank. Bricht eines ab, bleibt davon nichts zurück, und das Merkmal „vollständig“ (Lagerbestellung) fehlt weiter, die API meldet „unvollständig“.
- **Wer den Verlauf ändert, pflegt zwei Umsetzungen** (`ServiceDemoOps`, `MemoryDemoOps`). Der Vergleichstest meldet jede Abweichung. Im Speicher nicht unterstützt (wirft `NotSupportedException`): Termine mit vorgegebener Id, Plattformbuchungen, Anzahlungen, Pakete, Eintritte, Kartenzahlungen am Terminal; der Verlauf braucht sie nicht.
- Der Vergleichstest dauert rund 3 Minuten (der Weg über die Dienste ist der langsame), `DemoSeederTests` läuft jetzt über den schnellen Weg.
