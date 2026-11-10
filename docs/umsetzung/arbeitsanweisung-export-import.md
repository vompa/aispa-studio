# Arbeitsanweisung: Exporte (Excel, CSV, vCard, iCal, Portal-CSV) und Kunden-Import

**Für:** Claude Code im Repository `vompa/aispa-studio`
**Stand:** 09.11.2026, Auftrag des Auftraggebers; läuft parallel zu den Anweisungen [Verlauf](arbeitsanweisung-verlauf.md) und [PDF-Ausgaben](arbeitsanweisung-pdf-ausgaben.md) (andere Dateien; gemeinsame Stellen nur `pr-protokoll.md`, `handtests.md` und `Directory.Packages.props`; Konflikte dort löst die beauftragende Sitzung)
**Ablauf:** Code Guardian nach [docs/arbeitsweise.md](../arbeitsweise.md) Abschnitt 9; eine Sitzung arbeitet die Aufgaben nacheinander in einem Worktree ab, ein Squash-Commit je Aufgabe, **kein Push, kein PR**. Modellwahl je Aufgabe wie in Abschnitt 3; Subagenten bekommen das günstigste Modell, das die Teilaufgabe zuverlässig schafft.

---

## 1. Ziel

Rezeption und Betreiber bekommen jede Liste der App als Datei für Excel, Kontakte als vCard für Outlook, Apple und Google, Termine und Dienste als iCal, eine Kundenliste im Format der Buchungsportale, und einen Import, mit dem ein Spa seine Kunden aus Treatwell, Shore, Phorest oder einer Excel-Liste nach AISPA holt. Alles mit demselben Knopf „Exportieren“ und denselben Regeln: Rechte der Seite, Mandant, nie Gesundheitsdaten.

**Was es heute gibt:** Buchhaltung als DATEV, BMD, RZL und Schweizer CSV (`src/AispaStudio.Api/Accounting/AccountingExport.cs`, App speichert Byte für Byte, `AccountingViewModel`), Lohn-Übergabe (`Payroll/PayrollEndpoints.cs`, `GET /export`), DEP, DSFinV-K und TSE-Export (`berichte-pflichtberichte-abrufen.md`). Kalender-Abos lesen fremde iCal-Feeds (`CalendarSubscriptions/IcsParser.cs`), ein iCal-Export fehlt. Kein Excel-Paket, kein Kunden-Import, kein Feld für eine Werbe-Einwilligung am Gast (`CustomerDto` in `SchedulingContracts.cs`).

**Erfolg ist erreicht, wenn:**

1. Es gibt einen gemeinsamen Export-Dienst in der API (`src/AispaStudio.Api/Exports/`) für Excel (.xlsx), CSV, vCard 4.0 und iCal, und in der App einen Knopf **Exportieren** mit Formatwahl, der überall gleich aussieht und über den vorhandenen Datei-Dialog speichert.
2. Kunden: gefilterte Kundenliste als Excel, CSV und Portal-CSV; Kontakt als vCard einzeln und für die gefilterte Liste; Newsletter-Liste nur mit Werbe-Einwilligung, die als neues Feld am Gast erfasst wird.
3. Termine: Terminliste eines Zeitraums als Excel; Termin als iCal für den Gast (auch als Anhang der Terminbestätigung aus der PDF-Anweisung, sobald beide gemergt sind); Termine und Dienste einer Person als iCal.
4. Kassa, Lager, Personal, Verlauf und Berichte: die Listen aus Abschnitt 3 als Excel (und CSV).
5. Kunden-Import aus Excel oder CSV mit Spaltenzuordnung, Vorschau, Dublettenprüfung, Probelauf und Protokoll; der Import läuft über die Dienste der API (jeder Gast entsteht wie beim Anlegen in der Maske, mit Verlauf und Kanal „Import“).
6. Hilfe je Export, eine Importanleitung mit Beispiel-Dateien, ADR; alle bestehenden Tests grün; jede Datei wird im Test zurückgelesen (Excel mit ClosedXML, CSV geparst, vCard und iCal zeilenweise gegen die Pflichtfelder geprüft).

**Nicht Teil dieser Anweisung:** Import von Terminen, Gutscheinen oder Lagerdaten; Export in die Weboberfläche; automatische Übertragung an Portale oder Newsletter-Dienste über deren API; Änderungen an den bestehenden Buchhaltungs- und Lohnexporten. Nur im PR-Protokoll als „bewusst offen“ nennen.

## 2. Leitplanken für alle Aufgaben

- `CLAUDE.md` gilt vollständig: englische Namen im Code, Deutsch in Oberfläche, Spaltenköpfen, Doku und Commits; Warnungen sind Fehler; Texte nach [texte.md](../gestaltung/texte.md), Meldungen nach [meldungen.md](../gestaltung/meldungen.md), Masken nach [masken.md](../gestaltung/masken.md), Gestaltung nach [design.md](../design.md). Nie der Name des Auftraggebers im Repository.
- **Paket (DEPENDENCY GATE):** Excel mit **ClosedXML** (MIT; `Directory.Packages.props`), Lesen beim Import ebenfalls ClosedXML; CSV, vCard und iCal ohne Paket (eigene kleine Schreiber und Leser im Export-Dienst, Muster `IcsParser`). Vor dem Einbinden Paketname, Version, Herausgeber und Lizenz im PR-Protokoll; `dotnet list package --vulnerable`; Drittanbieter-Hinweise ergänzen. Kein EPPlus (Lizenz mit Bedingungen).
- **Dateiformate:** Excel mit Kopfzeile fett, Autofilter, Spaltenbreite nach Inhalt, Beträge als Zahl mit zwei Stellen, Datum als Datum, Uhrzeit als Uhrzeit, eine Tabelle je Datei, Blattname gleich dem Listennamen; CSV UTF-8 mit BOM, Strichpunkt, Anführungszeichen bei Bedarf, Datum `TT.MM.JJJJ`, Beträge mit Komma (Excel in Österreich und Deutschland öffnet das direkt); vCard 4.0 (`FN`, `N`, `TEL;TYPE=cell`, `EMAIL`, `BDAY`, `ADR`, `NOTE` nur mit dem Vermerk aus der Kundenkarte, nie Wünsche oder Gesundheit, `UID` aus der Kunden-Id, `REV`); iCal 2.0 (`VCALENDAR` mit `PRODID`, je Termin `VEVENT` mit `UID` aus der Termin-Id, `DTSTART`/`DTEND` mit `TZID` des Standorts, `SUMMARY` „Behandlung bei Spa-Name“, `LOCATION` Standortadresse, `DESCRIPTION` Hinweise, `URL` zum Kundenbereich, wenn vorhanden). Dateinamen sprechend und ohne Umlaute im Dateisystem-Teil: `Kunden_2026-11-09.xlsx`, `Termin_2026-11-12_1400.ics`.
- **Datenschutz:** Exporte enthalten personenbezogene Daten; nie Gesundheitsdaten, Anamnese, Wünsche zur Behandlung oder interne Vermerke, außer die Spalte „Vermerk“ bei der Kundenliste für die Inhaberin. Die Newsletter-Liste enthält nur Gäste mit erfasster Werbe-Einwilligung. Jeder Export wird im Verlauf protokolliert („Export Kundenliste (Excel, 312 Zeilen) von Lena“), damit die Leitung sieht, wer Daten herausgeholt hat; dafür ein Eintrag über den bestehenden Verlauf-Mechanismus (eigene Tabelle `aispa.Exports` in `ChangeHistory.Tables`, Zeile je Export mit Art, Format, Zeilenzahl, Filter).
- **Rechte:** jeder Export folgt dem Recht der Seite, die die Daten zeigt; Kundenliste und vCards für alle mit Kundenrecht, Newsletter-Liste, Kassa, Lager, Personal, Verlauf-Export und Import nur Leitung (`IsOwnerLevel`). Mandant und Standort wie die jeweilige Abfrage.
- **Server rechnet, App zeigt:** Jede Datei entsteht in der API (`GET …/export?format=xlsx|csv|vcf|ics` mit den Filtern der Liste, `application/vnd.openxmlformats-officedocument.spreadsheetml.sheet`, `text/csv`, `text/vcard`, `text/calendar`, Dateiname im `Content-Disposition`). Die App lädt Bytes und speichert über `StorageFileSaver`. Höchstens 50 000 Zeilen je Export, sonst Validierungsfehler mit Hinweis auf engere Filter.
- **Import über die Dienste, nie an der Datenbank vorbei:** jeder Gast entsteht über denselben Dienst wie `POST /api/customers`, damit Prüfungen, Verlauf und Kanal stimmen.
- **Chirurgisch:** Neuer Ordner `Exports/` in der API, `Controls/ExportButton*` in der App, `Imports/` für den Kunden-Import; bestehende Listen bekommen nur den Knopf. Toten Code melden, nicht löschen.
- **Prüfen** nach Abschnitt 9: Basisprüfung, zuständige Testklassen, Läufe über 2 Minuten im Hintergrund mit Protokoll in `.test-logs/`. Jede Datei wird im Test zurückgelesen; eine Datei je Format zusätzlich im Scratchpad speichern und von Hand prüfen (Excel per `openpyxl` in Python lesen, vCard und iCal gegen die Pflichtfelder). API-Testkalender März 2027 (`TestSpa.Monday`), Zeitfilter relativ dazu.
- **Prüfen als Rolle:** Rezeption (Kundenliste, vCard, Terminliste, Termin-iCal) und Inhaberin (alles, Import).
- **Abschluss je Aufgabe:** PR-Protokoll-Abschnitt, Handtestblock, Hilfe im selben Commit.
- **Fragen:** offene Entscheidungen mit Optionen und Empfehlung an die beauftragende Sitzung, weiterarbeiten, wo es ohne Antwort geht.

## 3. Aufgaben im Überblick

| Nr. | Aufgabe | Modell | Hängt ab von |
|---|---|---|---|
| A | Grundlage: Export-Dienst (Excel, CSV, vCard, iCal), Export-Protokoll im Verlauf, Knopf „Exportieren“ in der App, ADR | opus | – |
| B | Kunden: Liste als Excel, CSV, Portal-CSV; vCard; Werbe-Einwilligung am Gast; Newsletter-Liste | opus | A |
| C | Termine: Terminliste Excel; Termin-iCal für den Gast; iCal je Person (Termine und Dienste) | sonnet | A |
| D | Kassa, Lager, Personal, Verlauf, Berichte als Excel und CSV | sonnet | A |
| E | Kunden-Import aus Excel und CSV mit Zuordnung, Vorschau, Dubletten, Probelauf, Protokoll | opus | B |
| F | Hilfe, Beispiel-Dateien, Handbuchbilder, Fertigmeldung | sonnet | B–E |

## Aufgabe A: Grundlage

**Was:**

- Paket ClosedXML aufnehmen (siehe Leitplanke). `src/AispaStudio.Api/Exports/TableExport.cs`: eine Tabelle (`Columns` mit Name, Typ Text/Zahl/Betrag/Datum/Uhrzeit/Ja-Nein, `Rows`) wird zu Excel oder CSV; `VCardWriter.cs`, `ICalWriter.cs` mit den Feldern aus der Leitplanke; `ExportLog.cs` schreibt die Zeile in `aispa.Exports` (Tabelle per Migration, in `ChangeHistory.Tables` aufnehmen, Satz im `EntityHistory`-Describer „Export Kundenliste (Excel, 312 Zeilen)“).
- App: `ExportButton` (Knopf mit Menü „Excel“, „CSV“, bei Kontakten „vCard“, bei Terminen „Kalenderdatei (iCal)“, bei Kunden zusätzlich „Portal-CSV (Treatwell u. a.)“), lädt den Export über einen relativen API-Pfad mit den aktuellen Filtern der Liste, speichert über `StorageFileSaver`, Toast „Gespeichert: Kunden_2026-11-09.xlsx“ ohne Punkt; ohne Recht ausgeblendet; während des Ladens gesperrt mit Fortschrittsring (design.md). Tooltip ein Satz. Tastatur: Menü mit Pfeilen, Enter.
- ADR `docs/entscheidungen/00xx-exporte-ueber-einen-gemeinsamen-dienst.md` (Optionen: je Seite eigener Export / gemeinsamer Dienst / Export im Client; Formate; Protokoll; Grenze 50 000 Zeilen).

**Tests zuerst:** `TableExportTests` (Excel: Kopfzeile, Typen, Autofilter, Umlaute; CSV: BOM, Strichpunkt, Anführungszeichen, Komma im Betrag), `VCardWriterTests` (Pflichtfelder, Umbruch langer Zeilen nach RFC 6350, Sonderzeichen), `ICalWriterTests` (TZID, Umbruch, UID stabil, Zeitzone Wien gegen März 2027), `ExportLogTests` (Zeile im Verlauf mit Zeilenzahl), App `ExportButtonTests`, Ansichtstest Knopf und Menü in Hell und Dunkel.

## Aufgabe B: Kunden

**Was:**

- `GET /api/customers/export?format=xlsx|csv|portal&search=&…` mit denselben Filtern wie die Kundenliste. Spalten: Anrede, Titel, Vorname, Nachname, Rufname, E-Mail, Telefon, Geburtstag, Sprache, Adresse (Straße, PLZ, Ort, Land), Werbe-Einwilligung (Ja/Nein, Datum), angelegt am, Kanal, letzter Besuch, Anzahl Besuche; für die Inhaberin zusätzlich „Vermerk“; nie Wünsche, Gesundheit, Rabatt, Rechnungsdaten. Portal-CSV: Spalten in Englisch, wie die Import-Hilfen der Portale sie erwarten (`First name`, `Last name`, `Email`, `Mobile`, `Date of birth`, `Notes` leer, `Marketing consent`); die Sitzung prüft die aktuelle Treatwell-Import-Hilfe und hält im PR-Protokoll fest, welche Spalten belegt sind; lässt sich die Vorlage nicht belegen, heißt das Format in der App „Portal-CSV“ ohne Markennamen, mit einem Hilfesatz zur Zuordnung beim Import im Portal.
- `GET /api/customers/{id}/vcard` und `GET /api/customers/export?format=vcf` (alle gefilterten in einer Datei). Knopf in der Kundenkarte („…“ › „Kontakt als vCard“) und im Export-Menü der Liste.
- **Werbe-Einwilligung:** Feld `MarketingConsent` (`Given`/`Refused`/`Unknown`) mit `MarketingConsentAt` am Gast (Entität, Migration, `CustomerDto`, Kundenkarte: Auswahl „Werbung per Mail: Ja / Nein / nicht gefragt“ mit Datum, im Verlauf). Online-Buchung: bestehendes Häkchen, falls eines existiert, darauf abbilden; sonst unverändert und im PR-Protokoll vermerken.
- `GET /api/customers/export?format=csv&list=newsletter`: nur `Given`, Spalten E-Mail, Vorname, Nachname, Sprache, Anrede (für Brevo und Mailchimp), nur Leitung. Eintrag „Newsletter-Liste“ im Export-Menü.
- `npm run gen:api`.

**Tests zuerst:** `CustomerExportTests` (Filter greifen, Rezeption ohne Vermerk, Inhaberin mit Vermerk, keine Gesundheitsfelder in den Spaltenköpfen, Portal-Spalten, 50 000-Grenze), `CustomerVCardTests`, `MarketingConsentTests` (setzen, Verlauf, Newsletter-Liste nur mit `Given`), App `CustomersExportTests`, `CustomerCardTests` erweitert, Ansichtstests Kundenkarte mit Einwilligung.

## Aufgabe C: Termine

**Was:**

- `GET /api/locations/{id}/appointments/export?from&until&employeeId&roomId&status&format=xlsx|csv`: Tag, Beginn, Ende, Gast, Behandlung, Person, Raum, Status, Preis, Kanal, Notiz (nur Leitung). Knopf im Kalender-Menü („Terminliste exportieren …“ mit Zeitraum-Dialog, Vorbelegung angezeigte Woche).
- `GET /api/appointments/{id}/ics`: ein `VEVENT` für den Gast (Gruppe: alle Teile der Buchung als mehrere `VEVENT`), Erinnerung (`VALARM`) 24 Stunden vorher. Knopf im Termin-Panel hinter „Mehr“ („Kalenderdatei …“). Die PDF-Anweisung hängt diese Datei später an die Terminbestätigung; hier nur der Endpunkt und der Knopf.
- `GET /api/employees/{id}/ics?from&until&include=appointments,shifts`: Termine und Dienste einer Person für den privaten Kalender, nur Leitung oder die Person selbst (eigener Zugang); Knopf auf der Person-Seite („Kalenderdatei …“).

**Tests zuerst:** `AppointmentExportTests` (Filter, Notiz nur Leitung), `AppointmentIcsTests` (TZID, Gruppe, Alarm, UID stabil), `EmployeeIcsTests` (Dienste und Termine, Recht), App-Tests der Knöpfe, Ansichtstest Zeitraum-Dialog.

## Aufgabe D: Kassa, Lager, Personal, Verlauf, Berichte

**Was** (je ein Endpunkt `…/export?format=xlsx|csv` mit den Filtern der jeweiligen Liste, Knopf „Exportieren“ an der Liste, Recht wie die Seite):

- Kassa: Belegliste eines Zeitraums (Nummer, Zeit, Zahlarten, Betrag, Storno, Gast), Umsatz je Tag, Umsatz je Zahlart, Umsatz je Behandlung und Person, Gutscheinbestand (Code, Wert, Rest, gültig bis, Käufer), Tagesabschlüsse (Tag, Kasse, Soll, Gezählt, Differenz).
- Lager: Bestand je Lagerort, Inventur mit Differenzen, Bewegungen eines Zeitraums, Bestellungen, Ablaufliste.
- Personal: Stundenliste je Person und Monat, Abwesenheiten eines Zeitraums, Dienstplan der Woche (Personen als Zeilen, Tage als Spalten).
- Verlauf: der gefilterte Verlauf der neuen Seite (Endpunkt der Verlauf-Anweisung mit `format=xlsx|csv`; existiert er im Worktree noch nicht, den Export so vorbereiten, dass er den `ActivityFeed` aufruft, und im PR-Protokoll vermerken, dass der Knopf nach dem Merge der Verlauf-Anweisung in die Seite kommt).
- Berichte: Kennzahlen eines Zeitraums als eine Tabelle (Kennzahl, Wert, Vorperiode).

**Tests zuerst:** je Export ein Test (Spalten, Filter, Recht, fremder Mandant leer); App-Tests je Knopf; Ansichtstests der Listen mit Knopf.

## Aufgabe E: Kunden-Import

**Was:**

- Ablauf in der App unter Kunden („…“ › „Kunden importieren …“, nur Leitung), als Seitenpanel oder Dialog nach `masken.md` in vier Schritten: (1) Datei wählen (Excel oder CSV, bis 10 MB, erste Zeile Spaltenköpfe), (2) Spalten zuordnen (links die Spalten der Datei mit Beispielwerten, rechts die Felder von AISPA; automatische Vorschläge nach Kopfnamen, auch Englisch und die Kopfnamen der Treatwell-, Shore- und Phorest-Exporte, soweit die Sitzung sie aus deren Hilfen belegen kann; Pflicht: Nachname; Datumsformat erkannt oder wählbar), (3) Vorschau und Prüfung: erste 20 Zeilen, Zähler „n neu, m Dubletten, k fehlerhaft“, Dubletten gegen Nachname plus Telefon oder E-Mail im Bestand und innerhalb der Datei, je Dublette die Wahl „überspringen“ oder „ergänzen“ (leere Felder füllen, nie überschreiben), fehlerhafte Zeilen mit Grund („Telefon unleserlich“, „Geburtstag kein Datum“), (4) Probelauf („Nur prüfen“) und Import mit Fortschritt; Ergebnis als Toast und Protokoll (Datei, Zeilen, neu, ergänzt, übersprungen, Fehler) im Verlauf („Import Kunden: 240 neu, 12 ergänzt, 3 übersprungen von Lena“), Fehlerzeilen als Excel herunterladbar.
- API: `POST /api/customers/import/preview` (Datei als `multipart/form-data` plus Zuordnung als JSON, Antwort Vorschau mit Zählern und Zeilenstatus) und `POST /api/customers/import` (dieselben Eingaben plus Entscheidungen je Dublette; läuft in Blöcken zu 200 über den Kunden-Dienst, Kanal „Import“, Werbe-Einwilligung nur `Given`, wenn die Datei das belegt, sonst `Unknown`). Keine Gesundheitsdaten importieren, Spalten dafür nicht anbietbar. Grenze 20 000 Zeilen.
- Beispiel-Dateien unter `docs/beispiele/kunden-import-beispiel.xlsx` und `.csv` (erfundene Namen), für Hilfe und Tests.

**Tests zuerst:** `CustomerImportTests` (Zuordnung, Datumsformate `TT.MM.JJJJ` und `JJJJ-MM-TT`, Dubletten im Bestand und in der Datei, ergänzen füllt nur Leeres, Probelauf ändert nichts, Fehlerzeilen, Protokoll im Verlauf, Recht Leitung, Kanal „Import“ im Verlauf des Gastes, 20 000-Grenze), App `CustomerImportFlowTests` (vier Schritte, Vorschläge der Zuordnung, Abbruch), Ansichtstests jedes Schritts in Hell und Dunkel.

## Aufgabe F: Hilfe und Fertigmeldung

**Was:**

- Hilfe: `daten-exportieren.md` (Ablauf für alle Listen, Formate, was nie exportiert wird, Protokoll im Verlauf, Datenschutz-Hinweis zur Aufbewahrung beim Empfänger), `kunden-exportieren.md` (Excel, vCard, Portal-CSV, Newsletter-Liste, Werbe-Einwilligung), `kunden-importieren.md` (vier Schritte mit Bildern, Beispiel-Dateien, typische Fehler), `kalender-kalenderdatei.md` (iCal für Gast und Person), Abschnitte in `kassa-*.md`, `lager-*.md`, `dienstplan-*.md`, `zeiten-*.md`, `berichte-*.md`; Stichwörter: Excel, CSV, exportieren, Outlook, Kontakte, vCard, iCal, Kalenderdatei, Treatwell, importieren, Umstieg.
- Handbuchbilder in Hell: Export-Menü an der Kundenliste, Einwilligung in der Kundenkarte, Import Schritt 2 und 3.
- `npm run test` im Ordner `web`, Linkprüfung, Drittanbieter-Hinweise.
- Fertigmeldung nach Abschnitt 4.

## 4. Fertigmeldung

An die beauftragende Sitzung, in dieser Reihenfolge: Branch und Commits je Aufgabe; Paket mit Version und Lizenz; was geprüft wurde (Testklassen mit Zahlen, Dateien von Hand geöffnet, Bildtests in beiden Rollen); was **nicht** geprüft wurde (z. B. Import im echten Treatwell-Portal, Öffnen in Outlook); offene Fragen mit Empfehlung; bewusst offen gelassene Punkte aus Abschnitt 1. Dazu eine Übergabenotiz im Notizordner der Sitzungsfamilie, wenn einer genannt ist.
