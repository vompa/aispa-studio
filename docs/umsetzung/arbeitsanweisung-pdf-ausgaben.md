# Arbeitsanweisung: PDF-Ausgaben – Drucken, Speichern, per Mail senden (Stufe 1 und 2)

**Für:** Claude Code im Repository `vompa/aispa-studio`
**Stand:** 09.11.2026, Auftrag des Auftraggebers; läuft parallel zur Anweisung [Verlauf](arbeitsanweisung-verlauf.md) (andere Dateien; gemeinsame Stellen nur `pr-protokoll.md` und `handtests.md`, Konflikte dort löst die beauftragende Sitzung)
**Ablauf:** Code Guardian nach [docs/arbeitsweise.md](../arbeitsweise.md) Abschnitt 9; eine Sitzung arbeitet die Aufgaben nacheinander in einem Worktree ab, ein Squash-Commit je Aufgabe, **kein Push, kein PR** (das entscheidet der Auftraggeber nach der Fertigmeldung). Modellwahl je Aufgabe wie in Abschnitt 3; Subagenten bekommen das günstigste Modell, das die Teilaufgabe zuverlässig schafft.

---

## 1. Ziel

Rezeption und Betreiber sollen die wichtigen Dinge der App als Dokument in der Hand oder im Postfach haben: Rechnungen und Belege nachdrucken, als PDF speichern und dem Gast per Mail schicken; Terminbestätigung, Kundenkarte und den kompletten Kundenverlauf als Auskunft; Tagesplan, Gutschein, Warteliste; Tagesabschluss, Dienstplan-Aushang, Stundenliste, Inventurliste, Bestellung an den Lieferanten, Ablaufliste. Alle Dokumente sehen gleich aus (ein Kopf, eine Schrift, dieselben Tabellen), und überall stehen dieselben drei Knöpfe: **Drucken**, **PDF speichern**, **Per Mail senden**.

**Was es heute gibt:** `SimplePdf` (`src/AispaStudio.Api/HrFile/SimplePdf.cs`) schreibt reinen Text als PDF ohne Paket, genutzt für die Personalakte (`HrFilePdf`, Endpunkt in `HrFileEndpoints`, App speichert über `StorageFileSaver`/`Files.SaveAsync` in `PersonHrFileViewModel`). Belege gibt es als Text und ESC/POS (`ReceiptPrinter`, `GET /api/pos/receipts/{id}/print`, Recht `ViewReceipts`). Mails gehen über die Queue (`Mail.cs`: `OutgoingMail`, `SendMail`, MailKit), ohne Anhang. `QRCoder` ist als Paket vorhanden. Der Bondrucker-Druck der Kassa bleibt, wie er ist.

**Erfolg ist erreicht, wenn:**

1. Es gibt einen gemeinsamen Dokument-Dienst in der API (`src/AispaStudio.Api/Documents/`), der aus Daten ein PDF mit Kopf (Spa-Name, Firmendaten, Standort), Fußzeile (Seite n von m, Erstellt am), Überschriften, Absätzen, Tabellen mit Spaltenausrichtung und QR-Code macht; A4 hoch und quer sowie Bon-Breite 80 mm. Alle Dokumente dieser Anweisung laufen darüber; `SimplePdf` wird darin aufgenommen oder durch ihn ersetzt (Personalakte danach über den neuen Dienst, Inhalt unverändert).
2. `OutgoingMail` kann Anhänge tragen; die Queue versendet sie; ein Dokument lässt sich an eine Adresse schicken, vorbelegt mit der Mail des Gastes oder Lieferanten, mit fertigem Betreff und Text, den die Rezeption nur bestätigt oder anpasst.
3. In der App gibt es einen Knopfblock `DocumentActions` (Drucken · PDF speichern · Per Mail senden), der überall gleich aussieht und sich gleich verhält: Speichern über den vorhandenen Datei-Dialog mit sprechendem Dateinamen, Senden mit Rückfrage (Adresse, Betreff), Drucken über den Standarddrucker von Windows (PDF an den Drucker, nicht an den Bondrucker); Meldungen als Toast.
4. Stufe 1: Rechnung und Beleg (nachdrucken, speichern, senden, mit RKSV-QR), Terminbestätigung, Kundenverlauf als Datenschutz-Auskunft, Tagesabschluss.
5. Stufe 2: Tagesplan, Gutschein, Kundenkarte, Warteliste des Tages; Dienstplan-Aushang, Stundenliste, Inventurliste, Bestellung an den Lieferanten (mit Senden), Ablaufliste.
6. Rechte: jedes Dokument folgt dem Recht der Daten, die es zeigt (Beleg `ViewReceipts`, Kundenverlauf wie Kundenkarte, Stundenliste und Dienstplan nur Leitung, Lager wie die Lagerseite); Gesundheitsdaten stehen nie in einem Dokument außer in der Datenschutz-Auskunft für den Gast selbst, und dort nur mit dem Recht, das die Anamnese in der App zeigt.
7. Hilfe je Dokument, Handbuchbilder der Dialoge, ADR zur Dokumenterzeugung; alle bestehenden Tests grün, jede Regel mit einem Test, der ohne sie rot ist; jedes Dokument hat einen Test, der das PDF öffnet und Texte darin findet.

**Nicht Teil dieser Anweisung (Stufe 3, später):** Kennzahlen-Bericht, Kassenbuch des Monats, gefilterter Verlauf-Export, Umsatz je Behandlung und Person, Anamnesebogen leer und ausgefüllt, Drucken aus der Weboberfläche. Nur im PR-Protokoll als „bewusst offen“ nennen.

## 2. Leitplanken für alle Aufgaben

- `CLAUDE.md` gilt vollständig: englische Namen im Code, Deutsch in Oberfläche, Doku, Dokumenten und Commits; Warnungen sind Fehler; Texte nach [texte.md](../gestaltung/texte.md), Meldungen nach [meldungen.md](../gestaltung/meldungen.md), Gestaltung der App nach [design.md](../design.md). Nie der Name des Auftraggebers im Repository.
- **Paket (DEPENDENCY GATE):** PDF mit **PDFsharp** (Paket `PDFsharp`, Version 6.x, MIT, empira; über `Directory.Packages.props` wie die übrigen Pakete), QR-Codes mit dem vorhandenen `QRCoder`. Kein QuestPDF (Lizenz mit Umsatzgrenze). Vor dem Einbinden Paketname, Version, Herausgeber und Lizenz im PR-Protokoll festhalten; `dotnet list package --vulnerable` laufen lassen; Lizenzhinweis in `scripts/third-party-notices.py` bzw. der Drittanbieter-Liste ergänzen. Schrift: eine eingebettete freie Schrift mit Umlauten und Euro (z. B. Noto Sans oder Liberation Sans, Lizenz SIL OFL, Dateien unter `src/AispaStudio.Api/Documents/Fonts/` mit Lizenztext), damit „ü“, „€“ und „–“ überall stimmen; keine System-Schriften, der Server läuft in Linux-Containern.
- **Gestaltung der Dokumente** in `docs/gestaltung/dokumente.md` (neu, Aufgabe A): Kopf mit Spa-Name groß, Firmendaten klein rechts, Standort darunter; Titel des Dokuments; Datum; Tabellen mit Linien nur waagrecht, Beträge rechtsbündig, Summen fett; Fußzeile mit Seitenzahl und „Erstellt am … von …“ (Vorname der Person, wenn sie das Dokument erzeugt). Farben nur Schwarz und ein Grau, kein Akzent (druckt auf jedem Drucker). Ein Logo kommt erst, wenn die Firmendaten eines haben (heute nicht; nur im PR-Protokoll vermerken).
- **Server rechnet, App zeigt:** Jedes PDF entsteht in der API (`GET …/pdf` liefert `application/pdf` mit Dateiname), Mail-Versand über `POST …/send` (API legt die Mail mit Anhang in die Queue). Die App lädt nur Bytes, speichert, druckt oder bestätigt das Senden. Der Dateiname kommt aus dem `Content-Disposition` der API.
- **Mail-Anhang:** `OutgoingMail` bekommt `IReadOnlyList<MailAttachment> Attachments` (`FileName`, `ContentType`, `Content` als Bytes); die Queue-Nachricht trägt den Anhang mit (Größe begrenzen: höchstens 5 MB je Mail, sonst Validierungsfehler). Absender wie bei den Absage-Nachrichten (`ClosureNotices`), Antwortadresse das Postfach des Spas. Jede gesendete Mail zu einem Beleg oder Termin steht danach im Verlauf des Termins bzw. als Vermerk am Beleg („Per Mail gesendet an …, 09.11.2026 10:12“), damit die Rezeption Doppelsendungen sieht.
- **Datenschutz:** Belege und Auskünfte gehen nur an die Adresse des Gastes aus der Kundenkarte oder eine ausdrücklich eingetippte; die Rückfrage zeigt die Adresse. Die Datenschutz-Auskunft zeigt Anamnese nur, wenn die aufrufende Person sie in der App sehen darf, sonst „Gesundheitsangaben: vorhanden, Auskunft durch die Leitung“.
- **Chirurgisch:** Neue Ordner `Documents/` in API und `Controls/DocumentActions*` in der App; bestehende Masken bekommen nur den Knopfblock. Toten Code melden, nicht löschen.
- **Prüfen** nach Abschnitt 9: Basisprüfung (`dotnet format … --verify-no-changes`, `dotnet build … -c Release`), zuständige Testklassen; Läufe über 2 Minuten im Hintergrund mit Protokoll in `.test-logs/`. PDF-Tests lesen das PDF mit PDFsharp zurück (Seitenzahl) und prüfen den Text über eine kleine Hilfsklasse im Testprojekt, die die Textoperatoren der Seite liest (ein Test je Dokument: Titel, ein Name, eine Summe). Ein Dokument je Art zusätzlich als Datei im Scratchpad speichern und mit `pymupdf` (`page.get_pixmap`) als Bild ansehen (Sichtprüfung durch die Sitzung, Ergebnis im PR-Protokoll).
- **Prüfen als Rolle:** Rezeption (Beleg, Terminbestätigung, Kundenkarte, Tagesplan, Gutschein, Warteliste) und Inhaberin (alles); Knöpfe ohne Recht mit `ui:DisabledHint.Text`.
- **Abschluss je Aufgabe:** PR-Protokoll-Abschnitt (`docs/umsetzung/pr-protokoll.md`), Handtestblock (`docs/umsetzung/handtests.md`), Hilfe im selben Commit.
- **Fragen:** offene Entscheidungen mit Optionen und Empfehlung an die beauftragende Sitzung, weiterarbeiten, wo es ohne Antwort geht.

## 3. Aufgaben im Überblick

| Nr. | Aufgabe | Modell | Hängt ab von |
|---|---|---|---|
| A | Grundlage: Dokument-Dienst (PDFsharp, Schrift, Kopf, Tabellen, QR), Mail-Anhang, Knopfblock in der App, ADR, Gestaltungsdoku; Personalakte auf den Dienst umgestellt | opus | – |
| B | Rechnung und Beleg: nachdrucken, PDF, Mail; Vermerk am Beleg | opus | A |
| C | Terminbestätigung und Kundenverlauf als Datenschutz-Auskunft | sonnet | A |
| D | Tagesabschluss als PDF | sonnet | A |
| E | Rezeption Stufe 2: Tagesplan, Gutschein, Kundenkarte, Warteliste | sonnet | A |
| F | Betreiber Stufe 2: Dienstplan-Aushang, Stundenliste, Inventurliste, Bestellung mit Senden, Ablaufliste | sonnet | A |
| G | Hilfe, Handbuchbilder, Fertigmeldung | sonnet | B–F |

## Aufgabe A: Grundlage

**Was:**

- Paket `PDFsharp` 6.x aufnehmen (siehe Leitplanke Paket), Schrift einbetten, `IFontResolver` für die Schrift.
- `src/AispaStudio.Api/Documents/DocumentBuilder.cs`: fließender Aufbau (`Title`, `Paragraph`, `KeyValues`, `Table(columns, rows)` mit Ausrichtung je Spalte, `QrCode(text, size)`, `PageBreak`), Seitenumbruch in Tabellen mit wiederholter Kopfzeile, Formate A4 hoch, A4 quer, Bon 80 mm (Breite fest, Höhe wächst). Kopf und Fuß aus `DocumentHeader` (Spa-Name, Firmendaten aus `BusinessInfo`/Tenant, Standort, Erstellt am und von). Zahlen- und Datumsformat `de-AT` wie `ReceiptPrinter`.
- `SimplePdf` wird nicht mehr direkt genutzt: `HrFilePdf` baut über `DocumentBuilder` (Inhalt unverändert, Test `HrFileTests` bleibt grün, PDF-Text wird weiter gefunden). `SimplePdf` danach entfernen, wenn es keinen weiteren Aufrufer hat (Grep belegt das im PR-Protokoll).
- `Mail.cs`: `MailAttachment`, `OutgoingMail.Attachments` (leer als Standard), `MailMessages` hängt sie an die `MimeMessage`; `LoggingMailSender` nennt Anhänge mit Name und Größe; Größenprüfung.
- App: `DocumentActions` (Control oder UserControl in `src/AispaStudio.Desktop/Controls/`) mit `DocumentSource` (lädt die Bytes von einem relativen API-Pfad), Knöpfe „Drucken“, „PDF speichern“, „Per Mail senden“; Speichern über `StorageFileSaver` mit Dateiname aus der API; Drucken: PDF in eine Temp-Datei und über die Windows-Shell mit dem Verb `print` an den Standarddrucker (kein eigener Druckdialog); ohne Standarddrucker Toast „Kein Drucker eingerichtet“. Senden: Rückfrage-Dialog `SendDocumentDialog` (Adresse vorbelegt, Betreff vorbelegt, ein Satz Text, Knopf „Senden“) → `POST …/send`. Tastatur: Tab-Reihenfolge, Enter sendet, Esc schließt. Tooltips ein Satz. Toasts ohne Punkt: „Gespeichert: Beleg-K1-142.pdf“, „Gesendet an anna@…“, „Gedruckt“.
- ADR `docs/entscheidungen/00xx-dokumente-als-pdf-ueber-pdfsharp.md` (Kontext, Optionen SimplePdf erweitern / PDFsharp / QuestPDF / Browser-Druck, Entscheidung, Folgen); `docs/gestaltung/dokumente.md`.

**Tests zuerst:** `DocumentBuilderTests` (Seitenumbruch bei 80 Zeilen, Kopfzeile auf Seite 2, Umlaute und Euro im Text, QR vorhanden, Bon-Format wächst), `MailAttachmentTests` (Anhang in der MIME-Nachricht, Größenlimit), `HrFileTests` unverändert grün, App `DocumentActionsTests` (Speichern ruft den Dialog mit dem Dateinamen der API, Senden mit Rückfrage und Adresse, Rechte sperren), Ansichtstest Knopfblock und Dialog in Hell und Dunkel.

## Aufgabe B: Rechnung und Beleg

**Was:**

- `GET /api/pos/receipts/{id}/pdf` (Recht wie `print`): Beleg oder Rechnung mit Empfänger als A4 (Rechnung) bzw. Bon-Format (Beleg; die App bietet beides an: „PDF A4“ als Standard, Bon-Format im Menü), mit allen Zeilen, Steuern, Zahlungen, RKSV-QR (Inhalt wie `ReceiptPrinter.EscPos`), Hinweis „Kopie“ beim Nachdruck. Stornobelege gekennzeichnet wie im Druck.
- `POST /api/pos/receipts/{id}/send` mit `SendDocumentRequest(string To, string? Subject, string? Text)`: legt die Mail mit dem PDF in die Queue, Vermerk am Beleg (`ReceiptSentNote` oder vorhandener Verlauf), Antwort 202. Vorbelegung der Adresse aus `CustomerId` des Belegs (Kundenkarte) oder dem Empfänger der Rechnung.
- App: Knopfblock in der Kassa unter dem Beleg (Mitte nach dem Bezahlen) und im Reiter „Belege“ neben „Drucken“ (Bondrucker bleibt der erste Knopf); „Per Mail senden“ nur mit Adresse oder eingetippter Adresse. Vermerk „Per Mail gesendet an … am …“ unter dem Beleg.
- Aus dem Termin-Panel (Abschnitt Kassa/Rechnung, sofern vorhanden) und aus dem Verlauf (Sprung „Beleg anzeigen“ der parallelen Anweisung) führt der Weg zum Beleg; hier nichts doppeln.

**Tests zuerst:** API `ReceiptPdfTests` (A4 und Bon, Text mit Belegnummer, Summe, Zahlart, QR; Rechnung mit Empfängerzeile; Storno gekennzeichnet; Recht `ViewReceipts`; fremder Mandant 404), `ReceiptSendTests` (Mail in der Queue mit Anhang und Betreff „Ihre Rechnung Nr. … von …“, Vermerk gesetzt, ungültige Adresse 400). App `PosReceiptDocumentTests`, Ansichtstest Kassa mit Knopfblock.

## Aufgabe C: Terminbestätigung und Kundenverlauf

**Was:**

- `GET /api/appointments/{id}/confirmation/pdf` und `POST …/confirmation/send`: Terminbestätigung mit Gast, Behandlung(en) der Buchung (Gruppe: alle Teile), Tag, Uhrzeit, Dauer, Person (nur Vorname), Standort mit Adresse, Preis, Hinweise zur Stornofrist des Standorts, Absatz „Was mitbringen“ aus der Behandlung, wenn vorhanden. Betreff „Ihre Terminbestätigung: … am …“. Versand steht danach im Verlauf des Termins („Bestätigung per Mail an … gesendet von Lena“).
- `GET /api/customers/{id}/history/pdf` (Recht wie Kundenkarte): Datenschutz-Auskunft nach Art. 15 DSGVO: Stammdaten, Kontakt, Wünsche und Vermerke, alle Termine mit Status, Zahlungen, Gutscheine auf Namen, Online-Konto (ja/nein), Anamnese nur mit Recht (sonst der feste Satz aus der Leitplanke), Verlauf der Änderungen (aus `EntityHistory.ForCustomerAsync`), Erstellt am. Kein Versand, nur Speichern und Drucken (die Auskunft übergibt die Leitung persönlich oder schickt sie selbst).
- App: Knopfblock im Termin-Panel (hinter „Mehr“: „Bestätigung …“ öffnet den Dialog mit Speichern und Senden) und in der Kundenkarte („…“ › „Auskunft als PDF“).

**Tests zuerst:** `AppointmentConfirmationTests` (PDF mit Gast, Zeit, Standort; Gruppe mit beiden Teilen; Versand in der Queue; Verlaufseintrag), `CustomerDisclosureTests` (Anamnese mit und ohne Recht, Termine und Zahlungen enthalten, Verlauf enthalten), App `AppointmentPanelFlowTests` erweitert, `CustomerCardTests` erweitert, Ansichtstests.

## Aufgabe D: Tagesabschluss

**Was:** `GET /api/pos/registers/{id}/closings/{closingId}/pdf` (oder der zum vorhandenen `DayClosingDto` passende Pfad): Kasse, Tag, Belege von–bis, Summen je Zahlart, Steuern, Soll-Bargeld, gezähltes Bargeld, Differenz mit Notiz, Einlagen und Entnahmen des Tages, Unterschriftenzeile. Recht wie der Tagesabschluss. App: Knopfblock in der Abschlussmaske nach dem Abschluss und in der Liste früherer Abschlüsse. Kein Versand (Speichern und Drucken).

**Tests zuerst:** `DayClosingPdfTests` (Summen und Differenz im Text, Unterschriftenzeile, Recht), App-Test für den Knopfblock, Ansichtstest.

## Aufgabe E: Rezeption Stufe 2

**Was:**

- **Tagesplan** `GET /api/locations/{id}/day-plan/pdf?date=`: A4 quer, je Person eine Spalte oder je Raum (Parameter `by=team|rooms`), Termine mit Zeit, Gast, Behandlung, Status; Pausen und Sperrzeiten grau; Fußnote Warteliste-Anzahl. Knopf im Kalender-Menü („Tagesplan drucken …“).
- **Gutschein** `GET /api/pos/vouchers/{id}/pdf` und `…/send`: A4 als Geschenk (Spa-Name groß, Wert oder Behandlung, Code, QR mit Code, gültig bis, „Einlösbar in …“), Senden an den Käufer oder eine eingetippte Adresse. Knopfblock nach dem Verkauf und in der Gutscheinliste.
- **Kundenkarte** `GET /api/customers/{id}/card/pdf`: Stammblatt mit Kontakt, Wünschen, nächsten Terminen, letzten fünf Besuchen; keine Gesundheitsdaten. Knopf in der Kundenkarte („…“ › „Stammblatt als PDF“).
- **Warteliste** `GET /api/locations/{id}/waitlist/pdf`: Tagesliste mit Gast, Telefon, Wunsch, Zeitraum, Stand. Knopf in der Wartelisten-Leiste.

**Tests zuerst:** je Dokument ein API-Test (Inhalt und Recht), je Knopf ein App-Test, Ansichtstests der neuen Menüeinträge.

## Aufgabe F: Betreiber Stufe 2

**Was:**

- **Dienstplan-Aushang** `GET /api/locations/{id}/roster/pdf?week=`: A4 quer, Personen als Zeilen, Tage als Spalten, Dienste, Abwesenheiten, Notizen; nur veröffentlichter Stand; Recht Leitung. Knopf auf der Dienstplan-Seite.
- **Stundenliste** `GET /api/employees/{id}/timesheet/pdf?month=`: Tage mit Kommen, Gehen, Pausen, Ist, Soll, Saldo, Monatssumme, Unterschriftenzeilen; aus `TimesheetDay`/`TimesheetRow`; Recht Leitung. Knopf auf der Zeiten-Seite je Person.
- **Inventurliste** `GET /api/locations/{id}/stock-counts/{countId}/pdf`: Material, Einheit, Soll, Gezählt, Differenz, Wert, Summe; vor dem Zählen als Zählliste mit leerer Spalte „Gezählt“ (Parameter `blank=true`). Knopf in der Inventur.
- **Bestellung** `GET /api/purchase-orders/{id}/pdf` und `…/send`: Lieferant, Positionen mit Menge, Einheit, Einzelpreis, Summe, Liefertermin, Hinweis; Senden an die Mail des Lieferanten (vorbelegt aus den Lieferantendaten), Betreff „Bestellung … vom …“; Vermerk an der Bestellung „Gesendet am … an …“. Knopfblock in der Bestellmaske.
- **Ablaufliste** `GET /api/locations/{id}/stock/expiring/pdf?days=`: Chargen mit Ablaufdatum in den nächsten n Tagen, Lagerort, Menge. Knopf in „Ablauf-Aufgaben“.

**Tests zuerst:** je Dokument ein API-Test (Inhalt, Recht, fremder Mandant), `PurchaseOrderSendTests` (Queue, Vermerk), App-Tests der Knöpfe, Ansichtstests.

## Aufgabe G: Hilfe und Fertigmeldung

**Was:**

- Hilfe: `dokumente-drucken-speichern-senden.md` (Ablauf, gilt überall; Frontmatter wie die übrigen Abläufe, Rollen alle), je Dokument ein Abschnitt in der zuständigen Hilfeseite (`kassa-beleg-drucken.md` um PDF und Mail, `kassa-rechnung-mit-empfaenger.md`, `kalender-termin-buchen.md` oder eine neue `kalender-terminbestaetigung.md`, `kunden-kundenkarte.md` Auskunft und Stammblatt, `kassa-tagesabschluss.md`, `berichte-tagesplan-ansehen.md`, `kassa-gutschein-verkaufen.md`, `kalender-warteliste.md`, `dienstplan-veroeffentlichen.md`, `zeiten-*.md`, `lager-inventur.md`, `lager-bestellen.md`, `lager-ablauf-aufgaben.md`), Stichwörter mit „PDF“, „drucken“, „per Mail“, „Rechnung schicken“, „Kopie“.
- Handbuchbilder in Hell: Knopfblock in der Kassa, Dialog „Per Mail senden“, Kundenkarte mit „Auskunft als PDF“; ein Beispiel-PDF (Rechnung) als Bild im Handbuch (`help-images-shrink.py`, unter 300 KB).
- `npm run test` im Ordner `web`, Linkprüfung; Drittanbieter-Hinweise aktualisiert.
- Fertigmeldung nach Abschnitt 4.

## 4. Fertigmeldung

An die beauftragende Sitzung, in dieser Reihenfolge: Branch und Commits je Aufgabe; Paket mit Version und Lizenz; was geprüft wurde (Testklassen mit Zahlen, Sichtprüfung der PDFs als Bild, Bildtests in beiden Rollen); was **nicht** geprüft wurde (z. B. echter Drucker, echter SMTP-Versand); offene Fragen mit Empfehlung; bewusst offen gelassene Punkte aus Abschnitt 1. Dazu eine Übergabenotiz im Notizordner der Sitzungsfamilie, wenn einer genannt ist.
