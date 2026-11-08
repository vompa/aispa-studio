# Funktionen im Detail

Was AISPA Studio fachlich kann, mit Einstellungen und Schnittstellen je Bereich. Die Bedienung erklärt das Handbuch in der App (Quelle: [`web/src/help/content/`](../web/src/help/content/)).

## Kalender

| Begriff | Bedeutung |
| --- | --- |
| Standort | Zeitzone, Währung, Öffnungszeiten je Wochentag. Kundenstamm und Behandlungen gelten für alle Standorte. |
| Raum | gehört zu einem Standort; Raumpflege nach jedem Termin sperrt ihn zusätzlich |
| Gerät | gehört zu einem Standort, z. B. Hot-Stone-Wärmer |
| Person | Qualifikationen und Wochenarbeitszeiten je Standort |
| Behandlung | Dauer, Vor- und Nachbereitung, Preis (auch je Standort und nach Wochentag und Uhrzeit), nötige Qualifikationen, geeignete Räume und Geräte |
| Termin | belegt Person (mit Vor-/Nachbereitung), Raum (zusätzlich Raumpflege) und optional ein Gerät gemeinsam |
| Sperrzeit | Belegung ohne Termin: Urlaub, Schulung, Wartung |

- Jede Belegung steht in `ResourceBookings`. Eine Ausschlussbedingung in PostgreSQL verhindert, dass sich
  zwei Belegungen derselben Ressource überschneiden, auch wenn zwei Personen gleichzeitig buchen.
  Der Planer (`ResourcePlanner`) prüft vorher und begründet Ablehnungen (HTTP 409 mit `BookingRejected`).
- Ohne Wunsch wählt der Server freie Person, Raum und Gerät; Personen werden gleichmäßig ausgelastet.
- Alle Zeiten sind in der API UTC; angezeigt wird in der Zeitzone des Standorts (inkl. Sommerzeit).
- Änderungen gehen über die Queue per SignalR an alle, die den Standort beobachten (`WatchLocation`).
- Windows-App: Der Kalender nutzt die ganze Breite. Klick auf das Datum öffnet den Monat (und die Standortwahl),
  Tagesüberblick, Sauna und Warteliste sind Zähler in der Kopfzeile. Der Tag passt ohne Scrollen ins Fenster;
  Strg + Mausrad zoomt, „Tag einpassen“ stellt zurück (je Arbeitsplatz gemerkt). Ein Klick markiert einen Termin,
  Doppelklick oder Enter öffnet ihn; Pfeiltasten, E (einchecken), K (kassieren), F (Folgetermin), Alt + Pfeile (verschieben)
  und Entf (stornieren mit Grund) wirken auf den markierten Termin. Strg+F sucht Gast, Telefon oder Behandlung,
  Rechtsklick zeigt die passenden Schritte, „?“ alle Tasten. Termine tragen Kennzeichen („!“ Hinweis, „★“ Stammgast,
  „N“ Erstbesuch), werden orange, wenn der Gast überfällig ist, und Paar oder Spa-Tag tragen ein Etikett in Gruppenfarbe.
  Die Raumansicht zeigt, wer behandelt. Strg+N sucht freie Zeit für eine Behandlung: die passenden Zeiten leuchten
  im Kalender, Pfeiltasten wechseln, Enter bucht. Eine gelbe Leiste über dem Kalender zeigt Tagesnotizen (Taste „.“),
  wer heute abwesend ist und wem keine Zeit mehr für die gesetzliche Pause bleibt. Rechtsklick auf eine freie Stelle
  trägt eine Pause, eine Notiz oder eine Sperrzeit (Taste B) ein; Vor- und Nachbereitung stehen als Streifen am Termin,
  in der Raumansicht auch die Raumpflege. Termine bei krank gemeldeten Personen sind rot umrandet; „3 Termine umbuchen“ im
  Spaltenkopf öffnet den Ausfall-Assistenten, Rechtsklick auf den Kopf meldet die Person krank (Standortleitung).
  Strg+K nimmt einen Wunsch in eigenen Worten an („Massage morgen 17 Uhr, nicht bei Marco“): daraus werden Bausteine
  zum Entfernen und die drei besten Vorschläge mit Person, Raum und Preis, die zugleich im Kalender leuchten; Enter bucht
  den ersten, Strg+2 und Strg+3 die anderen, ein Nachsatz („lieber Donnerstag“) ergänzt den Wunsch. Ein Klick auf eine
  Zahl im Tagesüberblick („Noch erwartet“, „Eingecheckt“, „Termine“) hebt diese Termine hervor und springt zum ersten,
  „Auslastung“ öffnet „Freie Zeit suchen“; Esc oder der Chip in der Kopfzeile zeigt wieder alles. In der Kundenkarte
  stehen unter „Termine am liebsten“ die bevorzugte oder feste Therapeut:in und Wunschtage und -zeiten, auf Wunsch aus den
  bisherigen Besuchen vorgeschlagen; der Kalender zeigt FEST am Termin und warnt beim Verschieben zu jemand anderem, die
  Terminannahme schlägt die bevorzugte Person zuerst vor. „Wiederholen …“ im Termin legt eine Serie an (jede Woche bis
  alle 8 Wochen oder jeweils nach dem letzten Besuch); „Termine prüfen“ zeigt Konflikte und Feiertage vorher, im Kalender
  steht „↻ 3/6“. „Kommt später“ im Rechtsklick zeigt die Ankunft am Termin („kommt 10:15“); der Termin bleibt an seiner Zeit,
  auch wenn sein Beginn schon vorbei ist (kein Kürzen, kein Nach-hinten-Schieben). Gruppen bucht
  „Paar oder Spa-Tag“ mit bis zu acht Gästen, auch ohne Namen („Name folgt“); die Namen trägt man später im Termin nach. Im Spa-Tag lassen sich Sauna und
  Ruheraum (30, 60 oder 90 Minuten) wie Behandlungen wählen; „Reihenfolge vom Planer“ sucht die Reihenfolge mit den
  wenigsten Wartezeiten, und im Kalender zählt die Sauna im Etikett mit („Spa-Tag 2/2“, „Dazu: Sauna 10:00–11:00“). Pakete wie ein
  10er-Block Massage verkauft die Kassa unter „Gutschein verkaufen“; beim nächsten Termin bietet sie „1 Termin abziehen“ an, die
  Hover-Karte nennt „Paket Massage: 7 von 10 offen“, ein P erscheint bei der letzten Behandlung oder kurz vor Ablauf. Die Kundenkarte zeigt
  Kennzahlen aus Kalender und Kassa (Besuche, üblicher Abstand, je Person, nicht erschienen, späte Absagen, Pakete; Umsatz nur mit
  „Zahlen sehen“), berechnete Sterne (★ bis ★★★) und lässt Wünsche des Gastes sowie einen Rezeptionsvermerk nur für Rezeption und
  Leitung pflegen, mit Hinweis bei Gesundheitswörtern. Kennzeichen wirken im Kalender: „Braucht mehr Zeit“ (+10 Minuten Puffer),
  „Leitung informieren“, „Nicht bei mir“ (die Therapeut:in wird dem Gast nicht mehr vorgeschlagen) und „Nicht mehr buchen“ (Online
  und KI geben an die Rezeption ab). Die Sprache des Gastes steht als Kürzel (IT, EN …) am Termin, die Sprachen der Mitarbeitenden
  pflegt die Leitung unter „Team“, und „Freie Zeit suchen“ zeigt zuerst, wer die Sprache spricht; Termine aus Online-Buchung,
  KI-Terminannahme oder Treatwell nennen ihre Quelle. Konzept und nächste Schritte:
  [docs/produkt/kalender-rezeption.md](produkt/kalender-rezeption.md).
- Web: Tagesansicht nach Team oder Räumen, Klick auf eine freie Stelle bucht, Termine lassen sich ziehen,
  eigene Änderungen erscheinen sofort und werden zurückgenommen, wenn der Server ablehnt.
- Freie Zeiten sind danach bewertet, ob sie bei der Person eine Lücke lassen, in die keine Behandlung des Standorts passt.
  „Lücken schließen“ schlägt vor, gebuchte Einzeltermine um diese paar Minuten zu verschieben (gleiche Person, keine neue Lücke);
  verschoben wird erst auf Knopfdruck, nach Absprache mit dem Gast.

## Kassa

| Begriff | Bedeutung |
| --- | --- |
| Kasse | gehört zu einem Standort; Kassen-ID (z. B. `WIEN-1`), Land (AT, DE, CH), Belegkopf, IBAN für Sofortüberweisung |
| Rechnung | offen, sammelt Termine und Eintritte aus dem Kalender, Artikel, Gutscheine und freie Positionen; ein Termin oder Eintritt steht auf höchstens einer Rechnung |
| Beleg | entsteht beim Bezahlen, unveränderlich, signiert; Korrektur nur per Stornobeleg mit Grund |
| Erlöskonto | optional mit Kostenstelle; eigene Geschäftsbereiche bekommen eigene Konten, Umsätze darauf werden normal versteuert und signiert |
| Gutschein | Wertgutschein (Mehrzweck: beim Verkauf 0 %, versteuert bei Einlösung) oder Behandlungsgutschein (Einzweck: beim Verkauf versteuert) |

- Bezahlen mit bar (Rückgeld), Karte, Gutschein und Sofortüberweisung (EPC-QR-Code), auch gemischt. Rechnungssplitting:
  `checkout` mit Beträgen je Position erzeugt einen eigenen Beleg für jeden Teil.
- Eintritte in Bereiche (Sauna, Ruheraum): Ein Bereich bekommt in den Stammdaten (Standorte › Räume › „Eintritt“) einen Kassa-Artikel;
  Preis, Steuersatz und Erlöskonto kommen von diesem Artikel. Die Kassa listet die Eintritte des Tages unter „Heute abzurechnen“
  (`GET /api/pos/locations/{id}/unbilled-visits?date=…`) und rechnet sie je Person ab (`BillInput.AreaVisitIds`, Positionsart `AreaVisit`).
  Ein Bereich ohne Artikel (z. B. Ruheraum zum Termin) lässt sich nicht abrechnen.
- Digitaler Euro: als Zahlungsart vorbereitet (`PaymentMethod.DigitalEuro`, Buchhaltungskonto `DigitalEuroAccount`, Standard 1362),
  aber ausgeschaltet. Eingeschaltet wird er mit `Payments:DigitalEuro=true`, sobald die EZB ihn ausgibt und der Zahlungsdienstleister
  ihn abwickelt; dann bieten ihn Kassen in AT und DE an (`CashRegisterDto.PaymentMethods`), Schweizer Kassen nicht. Welche Zahlungsart
  unbar ist, wie sie heißt und wo sie verfügbar ist, steht an einer Stelle in `Pos/PaymentMethods.cs`.
- Österreich (RKSV): jeder Beleg trägt den maschinenlesbaren Code mit AES-verschlüsseltem Umsatzzähler, Verkettung über die vorige
  Signatur und ES256-Signatur. Start-, Monats-, Jahres-, Null- und Schlussbeleg erzeugt die Kasse selbst; fällt die Signatureinrichtung aus,
  wird weiter kassiert („Sicherheitseinrichtung ausgefallen“) und danach ein Nullbeleg erstellt. Das DEP gibt es unter
  `GET /api/pos/registers/{id}/dep`. Für den Prototyp signiert eine Test-Signatureinrichtung (`AT0`); für den Echtbetrieb ersetzt eine
  zertifizierte Einrichtung nur `IRksvSignatureDevice`.
- Deutschland (KassenSichV): jeder Beleg wird über `ITseClient` signiert, QR-Code nach DSFinV-K; ohne angebundene TSE lässt sich keine
  deutsche Kasse anlegen. In der Entwicklung signiert eine Test-TSE (`Fiscal:TestTse`). Die DSFinV-K gibt es unter
  `GET /api/pos/registers/{id}/dsfinvk?from=…&until=…`. Den TSE-Export (TAR) fordert man unter „Berichte“ › „Pflichtberichte“ bzw.
  `POST /api/pos/registers/{id}/tse-exports` an; er läuft über die Queue, die Datei liegt danach unter `GET /api/pos/tse-exports/{id}/file`.
  Schweiz: keine Signaturpflicht, Belege sind trotzdem nummeriert und per Hash verkettet.
- Rechnung an Firmenkunden: Zu einem Verkaufsbeleg ergänzt man einmal Name, Anschrift und gegebenenfalls UID-Nummer des Empfängers
  (`POST /api/pos/receipts/{id}/recipient`, Seite „Kassa“ › „Rechnung ausstellen“). Danach druckt der Beleg als Rechnung mit Empfänger und
  Leistungsdatum. Bis zur Kleinbetragsgrenze (AT 400 €, DE 250 €, CH 400 CHF, `ReceiptDto.SmallInvoiceLimit`) reicht der Beleg; darüber
  weist die Kassa darauf hin. In Österreich verlangt eine Rechnung über 10.000 € die UID des Empfängers. Firmenwortlaut, UID- und
  Firmenbuchnummer pflegt man je Standort (`LocationInput.Company`); sie stehen auf Beleg und Rechnung, sofern der Belegkopf sie nicht
  schon nennt.
- UID-Prüfung: „UID prüfen“ fragt die UID-Nummer des Empfängers bei VIES ab, dem MwSt-Informationsaustauschsystem der EU
  (`POST /api/pos/vat-checks`, über die Queue; Ergebnis unter `GET /api/pos/vat-checks/{id}`). Mit der UID des Standorts als Anfragende ist
  die Abfrage qualifiziert: VIES liefert Name, Anschrift und eine Abfragenummer als Nachweis, der an der Rechnung gespeichert wird
  (`InvoiceRecipient.VatIdCheckId`). Jede Abfrage bleibt nachlesbar (`GET /api/pos/vat-checks?vatId=…`). Schweizer UID-Nummern prüft VIES
  nicht. Ohne Netz: `VatIdCheck:Provider=Test` (in der Entwicklung an).
- Belegnummern, Umsatzzähler und Kette bleiben lückenlos, weil jeder Beleg in einer Transaktion entsteht, die die Kasse sperrt.
- Web: Seite „Kassa“ mit den heute abzurechnenden Terminen, offenen Rechnungen, Bezahlen (auch geteilt), Beleg mit QR-Code, Storno,
  Einlagen/Entnahmen und Tagesabschluss.
- Tagesabschluss (`day-closings`) nach Zahlungsart, Steuersatz und Erlöskonto mit Soll- und Ist-Bestand der Kassenlade; Einlagen und Entnahmen
  über `cash-movements`.

## Kundenbereich (Online-Buchung)

Eigene, schlanke Web-Oberfläche unter `/buchen/` (lokal http://localhost:5173/buchen/), getrennt vom Bundle der Rezeption
(`web/buchen/index.html`, Code in `web/src/booking`). Die CI prüft, dass sie unter 100 kB gzip bleibt und nichts aus den
anderen Web-Apps lädt (`npm run check:bundle`, prüft auch „Mein Tag“).

- Standort und Behandlung wählen, freie Zeiten sehen, als Gast oder angemeldet buchen; Bestätigung per Mail mit Links zum
  Stornieren und zum Gesundheitsfragebogen.
- Regeln je Standort unter Stammdaten → Standorte → „Online-Buchung“: online buchbar ja/nein, Vorlauf, Buchungshorizont,
  Stornofrist, Raster der Startzeiten und ein Hinweis für Gäste. Ohne eigene Einstellung gelten die Vorgaben aus dem
  Abschnitt `OnlineBooking` (120 Minuten, 90 Tage, 24 Stunden, 15 Minuten).
- Anmeldung ohne Passwort: Code per Mail, Passkey, Google oder Apple (aktiv, sobald `OnlineBooking:Google`/`:Apple`
  eingerichtet sind). Kundenkonten sind vom Team getrennt (eigene Tabelle `online.CustomerAccounts`, eigenes Cookie
  `aispa.kunde`); ein Kundenkonto öffnet keinen Endpunkt der Rezeption.
- Anamnese: Antworten AES-256-GCM-verschlüsselt im Schema `health` (Schlüssel `HealthData:Keys`, in Produktion aus einem
  Geheimnisspeicher). Das Team liest sie über `GET /api/customers/{id}/anamnesis`.
- Anamnese-Wege im Termin (Kalender → Termin → „Anamnese“): per Link aus der Mail, am Tablet des Empfangs (QR-Code,
  30 Minuten gültig, nur einmal), als Papierbogen mit QR-Code, oder ausgefüllt fotografiert. Das Foto liest
  `IAnamnesisReader` im Hintergrund: Claude, sobald `Ai:Claude:ApiKey` gesetzt ist (derselbe Schlüssel wie bei der
  Terminannahme), sonst wird von Hand übertragen. Unsicher gelesene Felder sind gelb; übernommen wird erst nach Prüfung.
  Foto und Vorschlag liegen verschlüsselt in `health.AnamnesisScans`.
- Warteliste: Ist nichts Passendes frei, trägt man sich für Behandlung, Zeitraum (bis 60 Tage) und Tageszeit ein. Wird
  im Kalender storniert oder verschoben (die alte Zeit wird frei), schickt `CheckWaitlistHandler` passenden Einträgen die freien Zeiten mit einem Link direkt zur
  Zeitauswahl; wer zuerst bucht, bekommt den Termin. Das Team sieht die Wünsche im Kalender unter „Warteliste“.
- Gutscheine (`#/gutschein`): Wertgutschein oder Gutschein für eine Behandlung, mit Empfänger und Nachricht. Bezahlt wird
  über `IOnlinePaymentProvider`; erst nach der Zahlung stellt die Kasse des Standorts den Gutschein mit Beleg aus
  (`IssueOnlineVoucherHandler`, Zahlart Karte mit Referenz des Anbieters), dann geht er per Mail raus. In der Entwicklung
  gilt `OnlineVouchers:PaymentProvider=Test` (Testzahlung per Knopf); ohne Anbieter verkauft der Kundenbereich keine
  Gutscheine. Ohne aktive Kasse am Standort ebenfalls nicht.
- Rückerstattung: Storniert die Kassenaufsicht einen online bezahlten Beleg (Gutschein, Ausfallgebühr), erstattet
  `RefundOnlinePaymentHandler` den vollen Betrag über den Anbieter und mailt den Stornobeleg. Eingelöste Gutscheine lassen
  sich nicht stornieren. Der Beleg zeigt den Stand (`GET /api/pos/receipts/{id}/online-refund`); nach einer Ablehnung
  stößt `POST` dort erneut an.
- Stripe ([ADR 0054](entscheidungen/0054-zahlungskonto-des-spas-stripe-connect-und-eigene-schluessel.md)): Jedes Spa
  richtet sein Zahlungskonto in den Stammdaten unter „Zahlungen“ ein, entweder mit Stripe Connect oder mit eigenen
  Schlüsseln (Webhook dann `POST /api/hooks/payments/stripe/{Spa}`). Als Connect-Plattform läuft der Server mit
  `OnlineVouchers:Stripe:Connect=true`, `:SecretKey` (Plattform) und `:ConnectWebhookSecret` (Connect-Webhook mit
  `account.updated` und den Checkout-Ereignissen); ohne `Connect` gilt `:SecretKey` mit `:WebhookSecret` als Konto des
  Servers für Spas ohne eigenes Konto (Einzelbetrieb). Schlüssel nur per user-secrets oder Geheimnisspeicher;
  `PaymentProvider=None` schaltet Online-Zahlung ab. Die Queue legt die Checkout-Sitzung an (`StartOnlinePaymentHandler`), die
  Bestellseite bietet sie per Knopf an. Stripe meldet das Ergebnis an `POST /api/hooks/payments/stripe` (Ereignisse
  `checkout.session.completed`, `…async_payment_succeeded`, `…async_payment_failed`, `…expired`); die Signatur wird
  geprüft. Lokal: `stripe listen --forward-to localhost:5086/api/hooks/payments/stripe`.
- Ausfallgebühr (No-Show-Schutz): je Standort in Prozent des Preises unter „Online-Buchung“ (`NoShowFeePercent`, Vorgabe
  `OnlineBooking:NoShowFeePercent`, 0 = aus). Der Gast stimmt beim Buchen zu und hinterlegt danach eine Karte über denselben
  `IOnlinePaymentProvider` (Stripe Checkout im Modus „setup“, im Test per Knopf). Bei „Nicht erschienen“ oder einer Absage nach
  der Stornofrist kann die Kassenaufsicht im Termin „Ausfallgebühr belasten“; `ChargeNoShowFeeHandler` belastet die Karte und
  verbucht die Gebühr an der Kassa des Standorts (Beleg per Mail). In den Demo-Daten verlangt Salzburg 50 %. Für Termine der
  Rezeption gibt es im Termin „Karte per Link anfordern“: Der Gast stimmt über den Link aus der Mail zu und hinterlegt dann die Karte
  (`CardRequests`, `POST /api/appointments/{id}/card-guarantee/request`).
  14 Tage nach dem Termin löscht `SavedCardCleanup` die Karte beim Zahlungsanbieter (bei Stripe den Kunden samt Karte); danach
  lässt sich nichts mehr belasten (`OnlineBooking:CardRetentionDays`, Takt `OnlineBooking:CardCleanupPollSeconds`, 0 = aus).
  Mit „Ohne Karte freigeben nach … Stunden“ (`CardHoldHours`, Vorgabe `OnlineBooking:CardHoldHours`, 0 = nie) gibt
  `UnsecuredBookingRelease` einen online gebuchten Termin frei, wenn in der Frist keine Karte hinterlegt wurde; der Gast bekommt
  eine Mail, die Warteliste rückt nach (Takt `OnlineBooking:CardHoldPollSeconds`, Vorgabe 300 Sekunden).
  `PaymentRecovery` holt nach, wenn eine bezahlte Anzahlung oder ein bezahlter Gutschein nach 15 Minuten keinen Beleg hat oder eine
  Erstattung hängt, bis zu zwei Tage lang (Takt `OnlineBooking:PaymentRecoveryPollSeconds`, Vorgabe 600 Sekunden). Was danach noch hängt
  und die Fehlerablage der Queue zeigt `GET /api/operator/queue-health`. Eine fehlgeschlagene Nachricht wiederholt die Queue dreimal
  (`Queue:RetrySeconds`, Vorgabe 60, 300 und 900 Sekunden), erst dann kommt sie in die Fehlerablage.
  Eine Behandlung kann in den Stammdaten eigene Prozent haben (vor der Regel des Standorts, 0 = keine Gebühr), und ein Standort kann
  die Gebühr nur von neuen Gästen verlangen (`NoShowFeeNewCustomersOnly`): wer im Spa schon eine abgeschlossene Behandlung hatte,
  hinterlegt keine Karte.
- Anzahlung und Vorauszahlung ([ADR 0047](entscheidungen/0047-anzahlung-und-vorauszahlung.md)): je Standort (`DepositPercent`,
  Vorgabe `OnlineBooking:DepositPercent`, 0 = aus, 100 = Vorauszahlung) oder je Behandlung. Sie ersetzt die Karte und gilt nur mit
  Zahlungsanbieter und aktiver Kasse. Nach der Zahlung erstellt `ReceiptOnlineDepositHandler` an der Kassa den Anzahlungsbeleg
  (Steuersatz der Behandlung, Mail an den Gast); beim Kassieren der Behandlung steht sie als Abzug auf dem Beleg. Die Rezeption kann
  an der Kassa auch selbst eine Position „Anzahlung“ auf einen Termin kassieren. Rechtzeitige Online-Absage: Storno des
  Anzahlungsbelegs und Erstattung über die Queue; bei „Nicht erschienen“ bleibt sie, erstattet wird dann nur über das Storno.
  Unbezahlte Termine gibt `UnsecuredBookingRelease` nach `CardHoldHours` frei. Der Termin zeigt den Stand
  (`GET /api/appointments/{id}/deposit`).
- Sprachen: Oberfläche, Gäste-Mails, Erinnerungen und Anamnesebogen (auch auf Papier) auf Deutsch, Englisch, Französisch
  und Italienisch (`Online/OnlineLanguage.cs`). Ohne Angabe gilt Deutsch, eine andere Sprache bekommt Englisch.
- Barrierefreiheit (BFSG, EN 301 549 / WCAG 2.1 AA): Tests prüfen jede Ansicht mit axe-core.
- Eigener EF-Kontext `OnlineDbContext` mit eigenen Migrationen:
  `dotnet ef migrations add <Name> -p src/AispaStudio.Api --context OnlineDbContext -o Online/Migrations`.
  Dritter Kontext ist der Schlüsselring: `--context KeyStoreDbContext -o System/KeyStoreMigrations`.
- Mails gehen über die Queue (`SendMail`). Ohne `Mail:Smtp:Host` landen sie in der Entwicklung im Log; mit Aspire fängt
  Mailpit sie ab (Weboberfläche im Dashboard).

## Mein Tag (Handy-App für Therapeut:innen)

Dritte, eigene Web-Oberfläche unter `/mein-tag/` (lokal http://localhost:5173/mein-tag/), gebaut fürs Handy: eine Spalte,
große Bedienflächen, hell und dunkel. Code in `web/src/myday`, Einstieg `web/mein-tag/index.html`; sie lädt nichts aus
den anderen Web-Apps oder dem Kundenbereich. `npm run check:bundle` hält sie unter 120 kB gzip und ohne SignalR.

- Anmeldung mit Passkey, im Einzelplatz mit Benutzername und Passwort über die Seite `/anmelden/` (`?zurueck=/mein-tag/` führt
  danach hierher zurück). Ohne Anmeldung zeigt die Seite den Passkey-Knopf und den Link „Mit Passwort anmelden“.
- Der eigene Tagesplan (`GET /api/reports/locations/{id}/day-plan`
  ohne Person): Dienst, Hinweise und je Termin eine Karte mit Zeit, Behandlung, Raum, Gast (Vorname und Initiale),
  Vorlieben, Notiz vom letzten Mal, Vorbereitung zum Abhaken und Notiz für die nächste Behandlung. Der laufende bzw.
  nächste Termin ist hervorgehoben; Vortag und Folgetag per Knopf. Mehrere Standorte: Auswahl oben, das Gerät merkt sie sich.
- Aufs Handy holen: Seite im Browser öffnen, dann „Zum Startbildschirm“ (Safari: Teilen → „Zum Home-Bildschirm“; Chrome:
  Menü → „App installieren“). Sie startet dann ohne Browserleiste (`web/public/mein-tag/manifest.webmanifest`).
- Offline lesen: Ein kleiner Service Worker (`web/public/mein-tag/sw.js`, nur im Produktions-Build) hält die App und die
  zuletzt geladenen Tagespläne (höchstens sieben Tage) auf dem Gerät. Ohne Netz zeigt die Seite den gespeicherten Stand
  mit „Offline: Stand von HH:MM“; Abhaken und Notizen gehen dann nicht; gestempelt wird in „Mein Tag“ nicht (T-015). Gespeichert wird nur der Tagesplan, keine
  Gesundheitsdaten (die Karte trägt nur den Stand des Fragebogens). Abmelden oder eine abgelaufene Anmeldung löscht ihn.

## Buchhaltung

Export der Kassenbelege für die Steuerberatung, Seite „Buchhaltung“ bzw. `GET /api/accounting/export?format=Datev|Bmd|Swiss&from=…&until=…`.

- Je Beleg: Erlöse je Erlöskonto und Steuersatz an das Verrechnungskonto, Zahlungen vom Geldkonto der Zahlungsart (Kassa, Karte,
  Bank, Gutscheine) an das Verrechnungskonto. Das Verrechnungskonto ist je Beleg ausgeglichen, Stornos sind Gegenbuchungen.
  Einlagen und Entnahmen der Kassenlade laufen über den Geldtransit, Kassendifferenzen aus dem Tagesabschluss als Fehlbetrag (4900)
  bzw. Überschuss (2700).
- Getrennte Erlöskonten bleiben getrennt und tragen ihre Kostenstelle (DATEV KOST1, BMD `kost`).
- DATEV: Buchungsstapel im DATEV-Format (EXTF 700, Kategorie 21, Windows-1252), BU-Schlüssel je Steuersatz und Land der Kassa einstellbar („20=“ für Automatikkonten); fehlt für einen Steuersatz der Schlüssel, verweigert der Export die Datei und nennt die fehlenden Sätze.
  BMD: NTCS-Buchungsimport (CSV) mit Prozentsatz, Steuercode und Steuerbetrag.
  RZL: CSV für den Buchungsimport von RZL FIBU bzw. RZL Board (`format=Rzl`, Windows-1252): Belegdatum, Belegnummer, Konto, Gegenkonto,
  Betrag (immer positiv) mit S/H-Kennzeichen, Steuerprozent, Steuerbetrag, Text, Kostenstelle; Spalten einmal einer Importvorlage zuordnen.
- Kontenzuordnung unter `/api/accounting/settings` (ändern nur Inhaber und Manager); vorbelegt mit SKR03-Konten, für Österreich (EKR/BMD) im Web anpassen.
  Ohne Land ist es der Standard für alle Kassen; mit `?country=AT|DE|CH` legt man eine eigene Zuordnung für die Kassen eines Landes an
  (`DELETE ?country=…` setzt sie auf den Standard zurück). `revenueAccountCodes` schlüsselt Erlöskonten auf den Kontenrahmen des Landes um,
  z. B. `4000=3400`. DATEV-Kopfdaten kommen aus der Zuordnung für Deutschland, die MWST-Codes aus der für die Schweiz.
- Export und Vorschau: wer das Recht „Berichte mit Beträgen“ (`ViewAmountReports`) hat: Inhaber, Manager und Backoffice immer, Standortleitung und Rezeption nur, wenn es vergeben ist ([ADR 0070](entscheidungen/0070-rollen-manager-und-backoffice.md)); die Standortleitung bekommt nur Kassen der eigenen Standorte.
- DATEV, BMD und RZL enthalten nur die Kassen in Euro. Schweizer Kassen (CHF) gehen mit `format=Swiss` als CSV (UTF-8, Semikolon) an Abacus
  oder bexio: Datum, Beleg, Soll, Haben, Betrag, Währung, MWST-Code, MWST-Satz, MWST-Betrag, Text, Kostenstelle; Beträge positiv, Stornos
  mit getauschtem Soll und Haben. Die Spalten ordnet man beim ersten Import einmal zu, die MWST-Codes je Satz stehen in den Einstellungen
  (`swissTaxCodes`, z. B. `8.1=UN81`). Ein Unternehmen mit Standorten in der Schweiz und der EU legt für die Schweiz eine eigene
  Zuordnung nach KMU-Kontenrahmen an.

## Lohnverrechnung

Monatswerte aus dem Dienstplan für die Lohnverrechnung, Seite „Lohn“ bzw. `GET /api/payroll/export?format=BmdLohn|DatevLodas|DatevLohnUndGehalt&year=…&month=…`.

- Je Person: gearbeitete Stunden, davon Sonntags-, Feiertags- und Nachtstunden (Nachtzeit einstellbar, Vorgabe 22 bis 6 Uhr),
  dazu Urlaubs-, Kranken- und Schulungstage. Grundlage ist der geltende Plan (Soll-Zeiten); Ist-Zeiten folgen mit der Zeiterfassung.
- Feiertage: die bundesweiten gesetzlichen Feiertage von Österreich oder Deutschland (`Payroll/PublicHolidays.cs`); Landesfeiertage
  folgen mit dem Bundesland je Standort.
- Übergeben werden Mengen mit Lohnart; Zuschlagssätze kennt die Lohnverrechnung. Lohnarten und Personalnummern stellt man auf der Seite ein,
  ohne Personalnummer oder Lohnart wird nichts übergeben (die Vorschau warnt).
- Formate: BMD Lohn (CSV, Importvorlage in BMD einmalig anlegen), DATEV LODAS (ASCII-Import `u_lod_bwd_buchung_standard`,
  Bearbeitungsschlüssel 1 = Stunden, 2 = Tage) und DATEV Lohn und Gehalt (Bewegungsdaten). Alle in Windows-1252.
- Nur Inhaber (Recht `ManageOrganization`), weil die Übergabe das ganze Unternehmen betrifft.

## Vollkosten pro Behandlung

Seite „Kosten“ bzw. `GET /api/costs/locations/{id}/analysis?month=JJJJ-MM`: je Behandlung und Termin, netto.

| Teil | Rechnung |
| --- | --- |
| Material | erfasste Mengen × Preis je Einheit (`/api/costs/treatments/{id}/materials`) |
| Personal | Vor-, Behandlungs- und Nachbereitungszeit × Stundensatz der qualifizierten Personen am Standort (sonst Standardsatz), geteilt durch den produktiven Anteil der Arbeitszeit |
| Raum | Miete und Nebenkosten des Raums ÷ Öffnungsminuten des Monats bei geplanter Auslastung, für die Zeit, die der Raum belegt ist (inkl. Reinigung) |
| Raumpflege | Reinigungszeit des Raums × Stundensatz Raumpflege + Pflegematerial |
| Gemeinkosten | Rezeption, Energie, Versicherung usw. pro Monat, wie die Miete auf die Raumminuten verteilt |

Dazu Nettopreis, Marge, Termine im Monat und Deckungsbeitrag; fehlende Angaben stehen bei der Behandlung.
Kostensätze und Stundensätze sehen nur Personen mit dem Recht „Berichte mit Beträgen“ (`ViewAmountReports`); ändern dürfen Inhaber, Manager und Standortleitung (`ManageCatalog`).

## Lager

Seite „Lager“ bzw. `/api/stock`: Materialien und Verkaufsprodukte mit Einheit, Einstandspreis und Mindestbestand je Standort. Die Seite hat die Reiter Bestand, Bewegungen, Bestellungen, Inventur und Lagerorte (die drei letzten für Standortleitung und Inhaber).

| Bewegung | Wer bucht |
| --- | --- |
| Verbrauch | das System, sobald ein Termin abgeschlossen ist, laut Rezeptur der Behandlung (Seite „Kosten“, Zeile „Aus dem Lager“) vom Raumschrank des Termins, wenn dort genug liegt, sonst vom Hauptlager (reicht beides nicht, ins Minus am Hauptlager mit Hinweis im Grund); zurück, wenn er es nicht mehr ist |
| Verkauf, Storno | das System aus dem Kassenbeleg, wenn der Artikel mit einem Lagerartikel verknüpft ist, vom ersten aktiven Verkaufsregal des Standorts (ohne Verkaufsregal vom Hauptlager); das Storno geht an denselben Lagerort zurück |
| Wareneingang | Rezeption und Standortleitung, ins Hauptlager; der Einstandspreis wird gleitend gemittelt |
| Nachfüllen | Rezeption und Standortleitung, vom Hauptlager (oder einem anderen Lagerort) an einen Lagerort desselben Standorts; Charge und Ablaufdatum wandern mit |
| Inventur | Standortleitung mit dem gezählten Bestand eines Lagerorts; gebucht wird die Differenz nur dort |
| Korrektur | Rezeption und Standortleitung, an einem Lagerort, nur mit Grund (z. B. Bruch) |
| Umbuchung | Rezeption und Standortleitung, an einen anderen Lagerort oder Standort (dort ins Hauptlager) |

**Lagerorte** ([ADR 0068](entscheidungen/0068-lagerorte-je-standort.md), `/api/stock/locations/{id}/storage-places`, Recht „Standort verwalten“ zum Schreiben): Jeder Standort hat genau ein Hauptlager (entsteht mit dem Standort, nicht löschbar und nicht deaktivierbar) und höchstens neun weitere Lagerorte der Art Raum, Verkauf oder Sonstiger Ort, mit Beschreibung und Mindestmengen je Material. `GET …/storage-places/{placeId}/levels` liefert den Bestand eines Lagerorts mit Chargen, `GET /api/stock/locations/{id}` je Material zusätzlich `places`. Eine Bewegung mit einem Lagerort eines anderen Standorts wird mit 400 abgelehnt.

Der Bestand ist die Summe der Bewegungen, die nie geändert werden; der Bestand eines Standorts ist die Summe seiner Lagerorte. Der Bestellvorschlag ergänzt bis zum Mindestbestand plus dem Verbrauch der gebuchten Termine der nächsten 14 Tage ([ADR 0037](entscheidungen/0037-lager-aus-bewegungen.md)).
Die Vollkosten nehmen für verknüpfte Rezepturzeilen den aktuellen Einstandspreis des Lagerartikels.

**Bestellungen** (Standortleitung, `/api/stock/locations/{id}/orders`, `/api/stock/orders/{id}`): Jedes Material kann einen Lieferanten und
dessen Artikelnummer haben. „Bestellvorschlag übernehmen“ legt je Lieferant einen Entwurf mit den vorgeschlagenen Mengen an; Materialien ohne
Lieferant bleiben draußen. Den Entwurf passt man an, kopiert den Bestelltext (`/text`) in eine Mail an den Lieferanten und markiert ihn als
bestellt. Das System schickt selbst nichts. Die Lieferung (`/deliveries`, auch in Teilen, mit Lieferscheinnummer und Preis laut Lieferschein)
bucht den Wareneingang. Offen bestellte Mengen stehen in der Spalte „Bestellt“ und mindern den Bestellvorschlag. Ohne Lieferung storniert
`/close` die Bestellung, nach einer Teillieferung schließt es sie ab.

**Chargen und Ablaufdaten** (`/api/stock/locations/{id}/batches`): Beim Wareneingang und bei der Lieferung einer Bestellung lassen sich
Charge und Ablaufdatum erfassen. Das Lager rechnet „zuerst ablaufend, zuerst verbraucht“: Der Bestand gehört den zuletzt ablaufenden
Eingängen. Die Spalte „Läuft ab“ zeigt das nächste Ablaufdatum, „Ablaufende Chargen“ alles, was abgelaufen ist oder in 30 Tagen abläuft.
„Ausbuchen“ bucht den Rest als Korrektur mit Grund „Abgelaufen“ aus.

**Rückverfolgung bei Rückruf** (`GET /api/stock/batch-usage?itemId=…&batch=…`): Verbrauch laut Rezeptur und Verkauf an der Kassa buchen aus
den Chargen ab, die zuerst ablaufen, und tragen deren Charge. Eine Rücknahme (Termin nicht mehr abgeschlossen) und ein Storno gehen in die
Chargen zurück, aus denen gebucht war. „Charge zurückverfolgen“ auf der Seite „Lager“ zeigt über alle Standorte, wer Material der Charge in
einer Behandlung oder beim Kauf bekommen hat, mit Kunde, Behandlung oder Beleg. Ältere Bewegungen ohne Charge bleiben ohne.

**Bedarfsprüfung** (`GET /api/stock/locations/{id}/demand?from=…&until=…`, Recht „Buchen“): rechnet den Verbrauch der gebuchten, noch offenen
Termine (Material im Rezept plus die Mengen bei „Zum Termin bereitstellen“, aus einer Quelle, `StockRecipes`) zeitlich geordnet gegen den
Bestand und meldet „reicht nicht für alle gebuchten Termine“ (mit der Zeit, ab der es fehlt), „bleibt unter der Mindestmenge“ und „reicht noch für
N Behandlungen“ (ab höchstens drei). Beim Buchen (`POST /api/appointments`) steht dieselbe Meldung in `Warnings`, wenn der neue Termin eine Grenze
reißt (nur Hinweis; geprüft werden die nächsten 14 Tage); die Vorschau vor dem Buchen liefert
`GET /api/stock/locations/{id}/demand/booking?treatmentId=…&start=…`. Ein Vorbereitungsschritt kann auf einen Lagerartikel des Rezepts verweisen
(`StockItemId`), bucht aber nichts: abgebucht wird weiter einmal beim Abschließen der Behandlung.

## Betrieb im Haus

Verwaltung und API für den Alltag der Rezeption (Anzeige im Windows-Kalender folgt, siehe [ADR 0065](entscheidungen/0065-raumpflege-tagesstart-und-abnahme.md)):

- **Zum Termin bereitstellen** (Behandlungen › „Zum Termin bereitstellen“, `/api/treatments/{id}/extras`): Getränke, Früchteteller und Extras mit
  Zeitpunkt (bei Ankunft, N Minuten vor Ende, nach der Behandlung) und optional Lagerartikel mit Menge je Termin. Je Termin entstehen Einträge mit
  Uhrzeit (`GET /api/locations/{id}/servings?date=…`, Haken mit Person und Zeit per `PUT /api/appointments/{id}/extras/{extraId}/check`), dazu die
  „Wünsche des Gastes“ der Kundenkarte (nie Gesundheitsdaten, nur der Hinweis „Anamnese beachten“). Der Tagesstart (`BriefingDto`) enthält die
  Summen („6 × Früchteteller: 11:20, 12:40 …“) und die Handbuch-Erinnerungen.
- **Rezeptionshandbuch** (Stammdaten › „Handbuch“, `/api/handbook`): Einträge mit Titel, Text, Bereich, „wichtig – am Tagesstart erinnern“ und
  optionalen Wochentagen; Lesen für alle Angemeldeten, Pflegen mit „Standort verwalten“. Gedacht auch als Wissensquelle der KI-Terminannahme (noch nicht angebunden).
- **Raumpflege, Tagesstart-Kontrolle und Abnahme** ([ADR 0065](entscheidungen/0065-raumpflege-tagesstart-und-abnahme.md)): Raumpflege als
  abhakbare Aufgabe mit Zustand offen, erledigt, überfällig (`CalendarDay.RoomCare`, `PUT /api/appointments/{id}/room-care`); Checkliste je Raum
  (Stammdaten › Standorte & Räume, `/api/rooms/{id}/start-checklist`), je Tag abgehakt (`/api/rooms/{id}/start-checks/{itemId}`); Abnahme durch die
  Standortleitung, auch per PIN (`POST /api/rooms/{id}/approvals`); Hinweise in `Warnings` beim Einchecken; Hygieneprotokoll je Tag und Raum als
  Daten und CSV (`/api/locations/{id}/hygiene-log[.csv]`) im Reiter „Hygiene“ unter „Berichte“.

## Berichte

Seite „Berichte“, je Rolle (`/api/reports/...`):

- **Mein Tag** (alle; Therapeuten nur der eigene): Dienst, Hinweise (Raumwechsel, knappe Wechselzeit, Termin außerhalb des Dienstes,
  Arbeitszeit) und je Termin eine **Behandlungskarte**: Raum, Gast mit Besuchszahl und Anlass (Geburtstag), Stand des
  Gesundheitsfragebogens (nie dessen Inhalt), Vorlieben, letzte drei Behandlungen mit Notizen und die **Vorbereitungs-Todo**
  aus Behandlungsrezept (abhakbar, mit Uhrzeit), Vorlieben, letzter Notiz und Anlass. Nachbereitung: Notiz für die nächste Karte.
- **Tagesstart** (Rezeption aufwärts): Zusammenfassung, Personal mit Abwesenheiten, Handlungsbedarf (Termin ohne Dienst,
  Erstbesuch ohne Fragebogen, gestern nicht abgerechnet), Gäste, gesperrte Räume, Kassa (Anfangsbestand, Warnungen).
- **Management** (`ViewFinance`): Umsatz brutto/netto aus den Belegen, Durchschnittsbon, Auslastung Personal und Räume,
  No-Show- und Storno-Quote, Gäste, neue Gäste, Stammgäste, Folgetermine, offene Gutscheine, jeweils mit Vorperiode;
  Umsatz nach Erlöskonto/Kostenstelle und Zahlungsart, Personal, Behandlungen mit Marge aus der Vollkostenrechnung, Alarme.
- **Vergleich mit anderen Spas** (Knopf im Management, `ViewFinance`): acht Kennzahlen der letzten 30 Tage, 90 Tage oder
  12 Monate neben Mitte und mittlerer Hälfte vergleichbarer Day-Spas (gleiches Land, Größenklasse bis 10 / 11–25 / 26–50
  Mitarbeitende), mit Hebel, wo der Standort unter der Mitte liegt. Andere Spas erscheinen nur als Verteilung, und nur bei
  mindestens 10 Spas in der Gruppe. Im Prototyp liefert `SampleBenchmarkPeers` Beispieldaten (so gekennzeichnet); ein
  zentraler Vergleichsdienst mit freiwilliger Teilnahme ersetzt später nur `IBenchmarkPeers`.
- **Pflichtberichte** (`ViewFinance`): Kassen mit Pflichten je Land (RKSV, § 146a AO, GeBüV), DEP-Download, Fristen,
  Gutscheinverbindlichkeiten, Arbeitszeitaufzeichnung des Monats aus dem Dienstplan.
- **Vorbereitung** (`ManageCatalog`): Behandlungsrezept je Behandlung.

Windows-App: `dotnet run --project src/AispaStudio.Desktop` (Adresse der API über `AISPA_API_URL`). Termine verschiebt man
mit der Maus auf eine andere Zeit, Person oder einen anderen Raum; das Farbschema (System, hell, dunkel) wechselt
links unten. Beim Termin führen „Kassieren“ und „Kundenkarte“ direkt in die Kassa bzw. zu den Kunden. Die Kassa
rechnet heutige Termine und Artikel ab, auch mit mehreren Zahlungsarten (z. B. Gutschein und Rest bar), und zeigt den
signierten Beleg; „Gutschein verkaufen“ setzt Wert- oder Behandlungsgutscheine auf die Rechnung (Code steht auf dem Beleg), das
Prozent-Symbol an einer Position gibt Rabatt in Prozent oder als Betrag (der Beleg nennt immer den Prozentsatz; über der
Rabattgrenze unter „Stammdaten“ › „Kassa“, Standard 20 %, gibt die Standortleitung mit Name und PIN am selben Platz frei), und ein Rabatt aus der Kundenkarte
zieht die Kassa selbst ab. Ist dort ein Barzahlungsrabatt eingestellt (Standard aus), zeigt die Rechnung „bar X € günstiger“, und bei
reiner Barzahlung steht der Nachlass als eigene Zeile je Steuersatz auf dem Beleg. Eine Schweizer Kasse zeigt CHF. „Empfänger erfassen“ am Beleg macht
ihn zur Rechnung für Firmenkunden, „UID prüfen“ fragt VIES ab. Unter „Belege“ lassen sich Belege nachdrucken und stornieren, unter „Kassenlade & Abschluss“
Einlagen, Entnahmen und der Tagesabschluss buchen; der DEP-Export bleibt im Browser. Meldet die Leitung den PC unter „Geräte“ als Empfangsgerät an, wechselt das Team dort mit Name und PIN („Person wechseln“); die eigene PIN legt jede Person über ihr Profilbild fest, und nach der eingestellten Zeit ohne Eingabe meldet die App ab. Unter „Geräte“ trägt man außerdem je PC den
Bondrucker (Netzwerk, ESC/POS, Port 9100) und das Kartenterminal (ZVT, Port 20007) ein; ein Terminal steuert immer nur der
PC an, an dem es gewählt ist. „Paar / Spa-Tag“ bucht mehrere Gäste und Abschnitte gemeinsam zu einer vom Server
vorgeschlagenen Zeit; im Termin lässt sich die ganze Buchung verschieben oder stornieren. Hat der Standort Bereiche, zeigt
die Seitenleiste „Sauna und Ruheraum“ mit Belegung, Eintritten und „Neuer Eintritt“; das Kassa-Symbol bei einem Eintritt rechnet ihn an der Kassa ab, die Eintritte des Tages stehen dort auch unter „Heute abzurechnen“. Die Karte „Warteliste“ zeigt die Wünsche der Gäste und springt
zum gewünschten Tag. Unter „Anfragen“ stehen die Nachrichten der KI-Terminannahme live mit der Zahl offener Anfragen:
Vorschlag buchen, Antwort bearbeiten, speichern und senden (der Hinweis auf die KI wird angehängt) oder als erledigt ablegen. Fällt die Verbindung aus, zeigt die App den zuletzt geladenen Stand (die nächsten sieben Tage
werden alle zehn Minuten vorausgeladen), merkt Buchungen und Statusänderungen vor und sendet sie danach; der lokale Stand
liegt mit DPAPI verschlüsselt in `%LOCALAPPDATA%\AispaStudio\offline.bin` und wird beim Abmelden gelöscht. Bildschirmfotos aller Ansichten ohne Windows:
`AISPA_SCREENSHOTS=$PWD/bilder dotnet test --project tests/AispaStudio.Desktop.Tests`.

## Hilfe und Handbuch

Die Hilfe ist das Handbuch der App ([ADR 0055](entscheidungen/0055-hilfe-zu-jeder-einstellung-im-repository.md),
[ADR 0056](entscheidungen/0056-handbuch-mit-ablaeufen-und-bildern-als-grundlage-der-ki-hilfe.md)): jede Seite erklärt, jeder Ablauf
Schritt für Schritt mit Bild (Termin buchen, verschieben, Rechnung mit Empfänger, Person einladen, Export für die Buchhaltung …) und jede
Einstellung, nach Rolle und Oberfläche gekennzeichnet. In der Windows-App steht „Hilfe“ in der Navigation (vor der Anmeldung als
eigenes Fenster), das Fragezeichen neben einer Einstellung öffnet die passende Stelle. Fragt man den Assistenten „Wie mache ich …?“, zeigt er den passenden Ablauf mit Schritten und Bild aus dem Handbuch.

- Texte: Markdown unter `web/src/help/content/`, Format und Schreibregeln in [web/src/help/README.md](../web/src/help/README.md). Sie sind in
  beide Oberflächen und in die API eingebaut und gehen ohne Server und Internet.
- Bilder: `scripts/help-images.sh` erzeugt die der Windows-App aus den Bildtests; `cd web && node scripts/help-images.mjs` fotografiert
  die Handy-Seite „Mein Tag“ an einer laufenden Umgebung mit Testdaten (Liste in `web/scripts/help-images.json`, Browser: installiertes
  Chrome oder `CHROMIUM_PATH`); danach `python3 scripts/help-images-shrink.py`.
- Die Anleitung zur Anmeldung als Stripe-Connect-Plattform steht im Thema „Zahlungen“, alle Optionen des Servers im Thema
  „Server-Einstellungen“.

## Rollen und Zugänge

Jede Person hat genau eine Rolle, gültig für alle oder ausgewählte Standorte. Der Server prüft bei jedem Aufruf Recht und Standort (`RequirePermission` in `src/AispaStudio.Api/Access`), die Oberfläche blendet nur aus.

| Rolle | Darf |
| --- | --- |
| Inhaber | alles an allen Standorten, Standorte anlegen, Inhaber ernennen |
| Standortleitung | buchen, Stammdaten und Team pflegen, Personen für die eigenen Standorte einladen, Buchhaltungsexport der eigenen Kassen |
| Rezeption | buchen, verschieben, stornieren, einchecken, Kunden anlegen |
| Therapeut:in | Kalender ansehen, Status der eigenen Termine setzen |

Neue Personen kommen per Einladungslink unter „Zugänge“ (7 Tage gültig, einmal verwendbar) und legen beim Annehmen ihren Passkey an. Lokal legt „Testkonto anlegen“ ein Konto mit der gewählten Rolle an.

Aushilfen, Praktika und Saisonkräfte bekommen einen befristeten Zugang („Befristet bis einschließlich“, höchstens zwei Jahre, nie für Inhaber); danach wirkt er wie gesperrt. Sieben Tage vor dem Ende (`AccessExpiry:NoticeDays`) bekommen die Person und alle, die den Zugang verwalten dürfen, einmal eine Mail; nach einer Verlängerung gibt es für das neue Ende wieder eine. Geprüft wird alle `AccessExpiry:PollSeconds` Sekunden (Vorgabe 3600, 0 = aus).

**Schnellwechsel am Empfang** ([ADR 0042](entscheidungen/0042-schnellwechsel-pin-nur-am-empfangsgeraet.md)): Die Leitung meldet unter „Zugänge“ den Browser der Rezeption als Empfangsgerät eines Standorts an. Jede Person legt oben rechts ihre PIN fest (4 bis 8 Ziffern, Inhaber mindestens 6). Am Empfangsgerät wechselt man dann mit „Person wechseln“, Name und PIN; nach fünf Fehlversuchen ist die PIN 15 Minuten gesperrt. Auf anderen Geräten gilt die PIN nicht, dort bleibt es beim Passkey. Nach einigen Minuten ohne Eingabe (beim Anmelden des Geräts einstellbar, Standard 5) meldet das Empfangsgerät ab; 30 Sekunden vorher erscheint ein Hinweis mit „Weiter arbeiten“.

## Dienstplan

Die Wochenarbeitszeit aus den Stammdaten ist der Normalfall. Im „Dienstplan“ lässt sich jeder Tag einer Person abweichend planen: anderer Dienst, Aushilfe an einem anderen Standort, Urlaub, Krankenstand, Schulung oder frei. Kalender und Buchung richten sich sofort danach. Hinweise zu Arbeits- und Ruhezeiten (`Roster/WorkingTime.cs`) erscheinen direkt im Plan:
- mehr als 10 Stunden am Tag;
- weniger als 11 Stunden Ruhe;
- mehr als 48 Stunden pro Woche;
- 7 Tage am Stück ohne freien Tag.

Fällt jemand aus, schlägt „Ersatz vorschlagen“ für jeden Termin des Tages eine Lösung vor: gleiche Zeit bei einer Kollegin, sonst die nächste freie Zeit am selben Tag. Geht am eigenen Standort nichts, sucht der Assistent an den anderen Standorten, die man selbst sieht. Solche Vorschläge sind nicht angehakt; erst nach Rücksprache mit dem Gast übernehmen. Der Preis bleibt wie gebucht, die Mail an den Gast nennt den neuen Standort.

## KI-Terminannahme per Mail

| Teil | Wo | Austauschbar über |
| --- | --- | --- |
| Postfach lesen | `MailIntake/Mailboxes` | `MailIntake:Mailbox`: `Imap`, `Graph` (Microsoft 365) oder ohne Angabe das Test-Postfach `TestInbox` |
| Antworten senden | `MailIntake/SpaMailSender.cs`, `Mail/Mail.cs` | aus dem Postfach des Spas (eigener SMTP-Server bzw. Microsoft 365), sonst gemeinsamer `IMailSender` (SMTP über `Mail:Smtp`, ohne Zugang nur ins Log); Antwort im selben Verlauf |
| KI | `MailIntake/Assistant` | `IBookingAssistant`: Claude, sobald `Ai:Claude:ApiKey` gesetzt ist, sonst regelbasiert ohne KI |

- Ablauf: Postfach → Tabelle `InboundMails` (jede Message-ID nur einmal) → Wolverine-Queue `mail-intake` (eine Mail nach der
  anderen; mit `Api` und `Worker` als PostgreSQL-Queue im Worker) → KI erkennt Absicht, Sprache, Behandlung, Standort, Wunschzeit und Gast → der Kalender prüft wie bei der Rezeption.
- Ist die Wunschzeit genau angegeben und frei, wird eingetragen (`MailIntake:AutoBook`), sonst werden bis zu drei freie Zeiten
  nahe am Wunsch vorgeschlagen. Absagen, Verschiebungen und Fragen gehen an die Rezeption.
- Die Antwort entsteht als Entwurf in der Sprache des Gasts (de, en, fr, it; sonst Englisch). Gesendet wird erst nach Freigabe
  (`MailIntake:AutoSend` ist aus). Beim Senden kommt immer ein fester Hinweis dazu, dass ein KI-Assistent die Nachricht erstellt
  hat (AI Act Art. 50); weder KI noch Bearbeitung können ihn entfernen.
- Fragen („Wie lange haben Sie offen?“, „Was kostet die Hot-Stone-Massage?“): Mit Claude schreibt die KI einen Antwortvorschlag
  aus Öffnungszeiten, Adresse, Stornofrist und den Behandlungen mit Dauer und Preis je Standort, in der Sprache des Gasts. Er geht
  nie automatisch hinaus. Kann die KI die Frage aus diesen Angaben nicht beantworten, ist sie medizinisch oder nennt der Vorschlag
  eine Zahl, die weder in den Angaben noch in der Nachricht steht, bleibt es beim Entwurf „Das Team meldet sich“.
- Die KI liest die Mail nur als Daten und liefert ein festes JSON-Format; Ids, die nicht in den Stammdaten stehen, werden verworfen.
- Kundenzuordnung ([ADR 0061](entscheidungen/0061-kundenzuordnung-eingehender-nachrichten.md), `CustomerMatcher`, für alle Kanäle und Treatwell):
  **sicher** ist nur eine gleiche Mailadresse (Haupt- oder weitere Adresse des Kunden, Groß-/Kleinschreibung egal) oder gleiche
  Absendernummer (E.164, „0660 1234567“ im Land des Spas = „+43 660 1234567“; `PhoneNumbers.ToE164`, AT/DE/CH). Sonst gibt es
  höchstens drei **Vorschläge** mit Stufe und Grund: 2 = von der KI gelesene Nummer passt, 3 = Name plus zweites Merkmal (letzte
  vier Ziffern, Geburtstag, Termin in den letzten 24 Monaten), 4 = ähnlicher Name (ä/ae, ß/ss, Akzente, Tippfehler). Nur bei einem
  sicheren Treffer oder ganz ohne Kandidaten bucht die Terminannahme selbst (ohne Kandidaten legt sie den Gast neu an, Notiz „Aus
  Mail angelegt“, `Customer.CreatedFromChannel`). Mit Kandidaten bleibt die Nachricht auf „Prüfen“ („Wer ist das?“), bucht nichts
  und legt niemanden an; die KI schlägt trotzdem Zeiten vor. Bis die Rezeption zuordnet (`POST /api/inbox/{id}/customer/assign`,
  optional Adresse oder Nummer merken), neu anlegt (`…/customer/new`) oder die Zuordnung löst (`…/customer/release`), enthält
  kein Entwurf und keine Anfrage an die KI etwas aus der Kundenkarte (`InboundMail.CustomerConfirmed`). Weitere Mailadressen
  je Kunde: `GET/POST /api/customers/{id}/emails`, `DELETE …/emails/{emailId}`; „mögliche Dubletten“ beim Anlegen:
  `POST /api/customers/duplicates`. Treatwell-Buchungen blockt die Zeit dagegen sofort (siehe unten, ADR 0062).
- Absender-Regeln je Spa ([ADR 0062](entscheidungen/0062-absender-regeln-und-treatwell-sofort-blocken.md), `SenderRule`,
  `GET/POST /api/intake/sender-rules`, `PUT/DELETE …/{ruleId}`, Recht „Firmendaten pflegen“, Oberfläche unter Stammdaten ›
  „Postfach & Nummern“): ein Muster (genaue Adresse oder Domain mit Unterdomains), die Plattform (Treatwell oder andere) und die
  Verarbeitung (automatisch übernehmen, nur als Vorschlag in den Posteingang, ignorieren). Jedes Spa hat die Domains
  `treatwell.de`, `.at`, `.ch`, `.com` und `.co.uk` vorbelegt (beim ersten Zugriff angelegt, auch für bestehende Spas); die frühere
  Server-Option `MailIntake:TreatwellSenderDomains` entfällt. „Automatisch“ verlangt weiter einen bestätigten Absender (SPF/DKIM,
  meldet das Postfach), sonst bleibt die Buchung als Vorschlag im Posteingang. „Ignorieren“ lässt die Mail sichtbar, aber ohne KI
  und ohne Antwortentwurf. Mails anderer Plattformen werden wie Anfragen gelesen, nie an die Plattform beantwortet.
- Treatwell (ADR 0062): Die Mail wird in Text umgewandelt (HTML, Tabellen und Listen werden zu „Bezeichnung: Wert“) und von der KI
  über ein festes JSON-Schema gelesen (`TreatwellReader`, `IAiJson`: Claude, sonst das lokale Modell). Das Ergebnis wird geprüft
  (Buchungsnummer steht wörtlich in der Mail, Name, plausibles Datum, Art passt zum Betreff); stimmen KI und Regeln bei
  Buchungsnummer, Art, Datum, Uhrzeit oder Zahlung nicht überein, entscheidet die Rezeption (Schutz vor Anweisungen im Mailtext).
  Ist das KI-Ergebnis ungültig oder fehlt es, liest `TreatwellMail` die Mail nach festen Regeln; scheitert beides, steht sie unter
  „Prüfen“ mit Begründung. Eine gültige Neubuchung kommt **sofort** in den Kalender, auch wenn der Kunde nicht sicher zugeordnet ist:
  dann ist er vorläufig angelegt (`Customer.AssignmentOpen`, „Zuordnung offen“), „Wer ist das?“ erscheint, und die Zuordnung hängt
  den Termin auf den Bestandskunden um und entfernt den vorläufigen Gast, wenn er sonst nichts hat. Kollidiert die Zeit, wird nichts
  eingetragen (Hinweis „kollidiert mit … In Treatwell umbuchen oder absagen“). Änderung → Termin verschieben, Absage → Termin
  stornieren (nur mit Regel „automatisch“); die Buchungsnummer verhindert doppelte Übernahme. Mehrere Behandlungen in einer Buchung
  trägt die Terminannahme nicht selbst ein. Abweichende Namen von Leistungen, Personen und Standorten über
  `POST /api/channels/Treatwell/mappings`.
- Treatwell, Zahlung und Annahme: Der Termin trägt „vorab über Treatwell bezahlt“ (mit Betrag) oder „zahlt vor Ort“
  (`Appointment.PlatformPayment`, `PrepaidAmount`) und „in Treatwell noch annehmen“ (`AcceptInPlatformPending`, bis die Rezeption
  `POST /api/inbox/{id}/platform-accepted` bzw. `POST /api/appointments/{id}/platform-accepted` ruft); die App bedient Treatwell nicht.
  An der Kassa gibt es dafür die Zahlungsart „Über Treatwell bezahlt“ (`PaymentMethod.Treatwell`): `BillDto.PlatformPrepaid`
  schlägt den Betrag vor, die Kassa lässt die Zahlungsart nur für vorab bezahlte Termine und höchstens bis zu diesem Betrag zu, der
  Umsatz läuft mit Beleg über die Kassa, und die Buchhaltung bucht auf das Konto `AccountingSettings.TreatwellAccount` (Standard 1400,
  Forderung an Treatwell).
- ICS-Feed: `POST /api/locations/{id}/calendar-feeds` liefert eine geheime Adresse `/ics/{schlüssel}.ics` mit der Belegung
  einer Person (für Treatwell je Mitarbeiter:in) oder eines Standorts, nur „Belegt“ ohne Namen. Mit `channel: "Treatwell"`
  fehlen Termine, die von Treatwell selbst kommen.
- Web: „Posteingang“ in der Kopfzeile. Neue Nachrichten und Ergebnisse der KI erscheinen live (SignalR, Anzeige „Live“); ohne Verbindung fragt die Seite alle 10 Sekunden nach. In der Entwicklung wirft „Test-Mail einwerfen“ eine Mail ins Test-Postfach.
- Windows-App: Nachrichten in einer anderen Sprache als Deutsch erscheinen übersetzt („Aus dem Italienischen übersetzt“),
  „Original“ (Taste O) schaltet um. Unter dem Antwortentwurf steht seine deutsche Fassung zur Kontrolle. „Mit KI
  überarbeiten“ macht den Entwurf freundlicher, kürzer, förmlicher, persönlicher, ergänzt eine Entschuldigung oder setzt
  einen eigenen Hinweis um; Strg+Z nimmt das zurück. Übersetzen und Überarbeiten laufen über die Queue und brauchen
  `Ai:Claude:ApiKey`. Weicht in der Übersetzung eine Zahl vom Original ab, zeigt der Posteingang das Original; bringt die
  Überarbeitung eine Zahl, die nicht im Entwurf stand, bleibt der Entwurf, wie er war.
- „Neu vorschlagen“ (Windows-App, `POST /api/inbox/{id}/repropose`) rechnet neue Zeiten: die gezeigten gelten als
  abgelehnt, ein Hinweis wie „lieber am Nachmittag“ ergänzt den Wunsch, die Antwort wird neu entworfen. Antwortet der
  Gast innerhalb von 14 Tagen auf einen gesendeten Vorschlag, verknüpft der Posteingang die Nachricht mit der Anfrage,
  übernimmt Behandlung und Wunsch und lässt die schon vorgeschlagenen Zeiten aus.

SMS, WhatsApp und Anrufe laufen durch dieselbe Terminannahme und erscheinen im selben Posteingang; geantwortet wird kurz
per SMS bzw. WhatsApp (Anrufe per SMS), mit kurzem KI-Hinweis. Anbieter und Zugänge unter `TextChannels`
(`MailIntake/Channels`), Geheimnisse nur über User-Secrets oder den Geheimnisspeicher:

| Kanal | Eingang (Webhook, ohne Anmeldung, signiert) | Versand |
| --- | --- | --- |
| SMS | `POST /api/hooks/twilio/messages` (Signatur `X-Twilio-Signature`, `Twilio:WebhookUrl` = eingetragene Adresse) | `SmsProvider: Twilio` |
| WhatsApp | Twilio wie SMS, oder Meta Cloud API: `GET/POST /api/hooks/whatsapp` (`Meta:VerifyToken`, Signatur mit `Meta:AppSecret`) | `WhatsAppProvider: Twilio` oder `Meta` |
| Anruf über Twilio | `POST /api/hooks/twilio/voice` (Ansage mit Hinweis auf Aufnahme und KI, dann Aufnahme) und `…/twilio/voice/recording` (Aufnahme fertig), signiert wie SMS; Host aus `Twilio:WebhookUrl`, Pfad wie aufgerufen | Antwort per SMS |
| Anruf über eigene Telefonanlage | `POST /api/hooks/voice/recordings?from=…&callId=…` mit Aufnahme im Body und `X-Aispa-Voice-Key` (`Voice:ApiKey`) | Antwort per SMS |

Bei Twilio trägt man die Adresse `…/api/hooks/twilio/voice?spa=<Id>` als Voice-Webhook der Nummer ein. Der Gast hört die
Ansage, spricht sein Anliegen (höchstens `Voice:MaxRecordingSeconds`, Vorgabe 120), die Queue holt die Aufnahme, schreibt
sie ab, löscht sie bei Twilio und stellt die Abschrift als Anruf in den Posteingang; scheitert die Abschrift, steht der
Anruf mit Link zur Aufnahme dort ([ADR 0043](entscheidungen/0043-anrufe-ansage-aufnahme-antwort-per-sms.md)).
Ohne Anbieter werden Antworten nur ins Log geschrieben. Spracherkennung (`ISpeechToText`): mit
`TextChannels:Voice:SpeechProvider = Azure` schreibt Azure AI Speech (Fast Transcription) die Aufnahme ab, nur in einer
EU-Region (`TextChannels:Voice:Azure:Region`, Vorgabe `westeurope`; andere Regionen verweigert die API beim Start),
Schlüssel in `TextChannels:Voice:Azure:Key`, Sprachen in `Locales`. Ohne Anbieter nimmt `FakeSpeechToText`
Text-Aufnahmen als Abschrift. WhatsApp erlaubt
freie Antworten nur innerhalb von 24 Stunden nach der letzten Nachricht des Gasts. In der Entwicklung wirft „Test-Nachricht
einwerfen“ im Posteingang auch SMS, WhatsApp oder eine Anruf-Abschrift ein.

Terminerinnerungen: Gäste bekommen 48 und 2 Stunden vor dem Termin eine Erinnerung mit Link zum Ansehen oder Absagen
(einstellbar unter „Stammdaten“ › „Erinnerungen“, `GET/PUT /api/reminders/settings`, nur Inhaber). Kanal Mail, SMS oder
WhatsApp; fehlt die Nummer, der Anbieter oder bei WhatsApp die freigegebene Vorlage, geht sie per SMS bzw. Mail. Die
WhatsApp-Vorlage (Meta: Name, Twilio: Content-SID) hat die Platzhalter {{1}} Vorname, {{2}} Behandlung, {{3}} Datum und
Uhrzeit, {{4}} Standort, {{5}} Link. Wer kurzfristig bucht, bekommt nur die Erinnerungen, die noch bevorstehen; nach dem
Verschieben gibt es neue. Geprüft wird alle `Reminders:PollSeconds` Sekunden (Vorgabe 300, 0 = aus). Den Stand sieht
die Rezeption im Termin.

Erinnerung an die nächste Behandlung: Nach einer abgeschlossenen Behandlung bekommt der Gast eine Mail mit Link zum Buchen,
wenn der Abstand für diese Behandlung vorbei ist und kein Termin gebucht ist (einstellbar je Behandlung unter „Stammdaten“ ›
„Erinnerungen“, `GET/PUT /api/retention/settings`, ohne Eintrag aus). Das ist Werbung: nur für Gäste, die eingewilligt haben.
Die Rezeption fragt im Termin per Mail an, der Gast bestätigt im Kundenbereich unter `#/einwilligung/{code}` (Double-Opt-in,
[ADR 0040](entscheidungen/0040-einwilligung-erinnerung-naechste-behandlung.md)); jede Erinnerung hat einen Abmeldelink.
Zwei, drei persönliche Sätze schreibt die KI (ohne Preise, Angebote oder Zahlen, sonst gilt ein fester Text), die Mail trägt
dann den Hinweis darauf. Höchstens eine Erinnerung je Gast in 30 Tagen, keine verspätete mehr als 14 Tage nach dem Fälligwerden.
Geprüft wird alle `Retention:PollSeconds` Sekunden (Vorgabe 3600, 0 = aus).

Echtes Postfach (Zugangsdaten nur über User-Secrets oder den Geheimnisspeicher):

```bash
# IMAP, z. B. beim Hoster; verarbeitete Mails bekommen das Schlagwort $AispaVerarbeitet und bleiben ungelesen
dotnet user-secrets set "MailIntake:Mailbox" "Imap" --project src/AispaStudio.Api
dotnet user-secrets set "MailIntake:Imap:Host" "imap.example.at" --project src/AispaStudio.Api
dotnet user-secrets set "MailIntake:Imap:UserName" "termine@spa.at" --project src/AispaStudio.Api
dotnet user-secrets set "MailIntake:Imap:Password" "<passwort>" --project src/AispaStudio.Api

# Microsoft 365 über Graph: App-Registrierung mit Anwendungsrecht Mail.ReadWrite, per Application Access Policy
# auf das Termine-Postfach beschränkt; verarbeitete Mails bekommen die Kategorie „AISPA verarbeitet“
dotnet user-secrets set "MailIntake:Mailbox" "Graph" --project src/AispaStudio.Api
dotnet user-secrets set "MailIntake:Graph:TenantId" "<tenant-id>" --project src/AispaStudio.Api
dotnet user-secrets set "MailIntake:Graph:ClientId" "<client-id>" --project src/AispaStudio.Api
dotnet user-secrets set "MailIntake:Graph:ClientSecret" "<geheimnis>" --project src/AispaStudio.Api
dotnet user-secrets set "MailIntake:Graph:Mailbox" "termine@spa.at" --project src/AispaStudio.Api
```

Mehrere Spas: Jedes Spa richtet sein eigenes Postfach unter „Stammdaten“ › „Postfach & Nummern“ ein (IMAP oder
Microsoft 365, `PUT /api/intake/mailbox`); das Passwort liegt verschlüsselt (Data Protection) in der Datenbank und wird
nie angezeigt. Mit „Aus diesem Postfach senden“ gehen alle Mails an Gäste aus diesem Postfach (Antworten, Erinnerungen,
Buchungsbestätigungen, Warteliste, Gutscheine): bei IMAP über den
SMTP-Server des Spas mit demselben Benutzer und Passwort, bei Microsoft 365 über Graph (die App braucht dann zusätzlich
`Mail.Send`; die Mail liegt unter „Gesendete Elemente“). Ohne Häkchen gehen sie über den gemeinsamen Versand, mit dem
Namen des Spas als Absender und dem Postfach als Antwortadresse. Ein fehlgeschlagener Versand steht beim Postfach.
Bei IMAP legt AISPA jede gesendete Mail zusätzlich im Ordner „Gesendet“ ab (gekennzeichneter Ordner oder üblicher
Name, sonst den Ordner beim Postfach angeben); bei Gmail das Häkchen abschalten, Gmail legt selbst ab.
„Jetzt prüfen“ ruft das Postfach sofort ab und prüft bei IMAP auch die Anmeldung am SMTP-Server
(`POST /api/intake/mailbox/check`, über die Queue). Anmeldecodes für das Kundenkonto gehen immer über den gemeinsamen
Versand.

Nummern für SMS, WhatsApp und Anrufe ordnet der Betreiber zu, weil sie in seinem Anbieterkonto liegen
([ADR 0033](entscheidungen/0033-nummern-vergibt-der-betreiber.md)). Dafür gibt es die Seite `/betreiber/`
(lokal http://localhost:5173/betreiber/): alle Spas mit Standorten, Stand des Postfachs und Nummern; Nummern zuordnen
und lösen; die Abo-Stufe ändern; ein Spa mit Grund sperren und wieder entsperren (gesperrt sieht und exportiert das Team weiter alles, kann aber
nichts ändern, online gibt es keine neuen Buchungen; [ADR 0039](entscheidungen/0039-spa-sperren.md)). Angemeldet wird mit dem Betreiber-Schlüssel, der nur im Tab bleibt. Ohne `Operator:ApiKey` gibt es die
Endpunkte nicht. Dasselbe per Kommandozeile:

```bash
dotnet user-secrets set "Operator:ApiKey" "<schlüssel>" --project src/AispaStudio.Api
curl -X POST http://localhost:5086/api/operator/routes -H "X-Aispa-Operator-Key: <schlüssel>" -H "Content-Type: application/json" \
  -d '{"tenantId":"<spa-id>","channel":"Sms","address":"+43 660 1234567","senderId":null}'
# Meta: senderId = Phone-Number-ID der Nummer, damit Antworten von dort gehen
```

Eine Nachricht gehört dann dem Spa aus `?spa=` der Webhook-Adresse, sonst dem Spa der angeschriebenen Nummer, sonst
`MailIntake:TenantId`. Antworten und Erinnerungen gehen von der Nummer des Spas.

Ob ein Absender bestätigt ist (wichtig für Treatwell, Regel „automatisch“), liest die Terminannahme aus „Authentication-Results“ des eigenen
Empfangsservers. Mit `TrustedAuthServers` (z. B. `spf.protection.outlook.com`) zählen nur dessen Einträge, sonst nur der oberste.

Claude lokal einschalten (der Schlüssel gehört nie ins Repository):

```bash
dotnet user-secrets set "Ai:Claude:ApiKey" "<schlüssel>" --project src/AispaStudio.Api
```

Tests ohne Docker: `AISPA_TEST_POSTGRES="Host=localhost;Username=postgres;Password=…"` zeigt auf einen vorhandenen
PostgreSQL-Server; jeder Testlauf legt dort eine eigene Datenbank an. Ebenso zeigt `AISPA_TEST_REDIS="localhost:6379"` für die Tests der
Backplane (`LiveBackplaneTests`) auf einen vorhandenen Redis statt auf einen Container.

## Mandanten

Jedes Spa-Unternehmen ist ein Mandant (`Tenancy/`). Alle Spas liegen in derselben Datenbank, sehen einander aber nie:
- Jede Fachtabelle hat eine Spalte `TenantId`, jede Abfrage filtert automatisch danach, und neue Einträge bekommen beim Speichern den Mandanten des Aufrufs. Das gilt auch für neue Tabellen im Namensraum `AispaStudio.Api`, ohne dass der Fachcode etwas tun muss.
- Der Mandant kommt aus dem Anmelde-Cookie. Öffentliche Abläufe ohne Anmeldung (Einladung annehmen, Online-Buchung) und Hintergrundaufträge setzen ihn mit `TenantContext.Use(...)`; ohne Mandanten wird nichts gespeichert.
- Der Kundenbereich (`/api/online`) erkennt das Spa am Link der Buchungsseite (`/buchen/?spa=<Id>`, an die API als Kopfzeile `X-Aispa-Spa`), am Standort, am Link aus einer Mail oder am angemeldeten Kundenkonto (`Tenancy/OnlineTenant.cs`). Gibt es nur ein Spa, gilt dieses.
- Hintergrundaufträge setzen den Mandanten aus ihren Daten (Terminannahme: Mandant der Mail; Abruf des Postfachs: `MailIntake:TenantId`, sonst das einzige Spa). Der iCalendar-Feed ermittelt ihn aus dem Feed.
- Webhooks für SMS, WhatsApp und Anrufe (`/api/hooks`) tragen das Spa in der eingetragenen Adresse (`?spa=<Id>`); ohne Angabe gilt `MailIntake:TenantId` bzw. das einzige Spa.
- Ein neues Spa registriert sich auf der Startseite („Neues Spa registrieren“) und wird Inhaber:in. Lokal legt „Testkonto anlegen“ das Konto im Beispiel-Spa an.

## KI-Assistent je Rolle

Der runde Knopf mit dem AISPA-Zeichen unten rechts (oder **Strg+Leertaste**) öffnet in der Windows-App die Leiste **KI und Hilfe**: oben die Erklärung zur offenen Seite, darunter der Assistent. „Ausblenden“ oder Strg+Leertaste schließt sie; ob sie offen ist, merkt sich die App. Was der Assistent beantwortet, hängt an den Rechten der Person:

- **Rezeption:** freie Zeiten („Wann ist morgen Nachmittag eine Massage bei Anna frei?“), Kunden suchen („Wer ist Frau Gruber?“, auch per Telefon oder Mail, mit nächstem Termin) und der Tagesüberblick.
- **Inhaber und Standortleitung:** dazu Auswertungen („Wie war der Umsatz letzte Woche?“): Termine, Terminwert, Kassenumsatz, Auslastung laut Arbeitszeiten, No-Shows und Stornos, jeweils mit dem gleich langen Zeitraum davor.
- **Therapeut:innen:** der eigene Tagesüberblick und freie Zeiten, ohne Kontaktdaten und ohne Buchen.
- **Kunden** schreiben in der Online-Buchung ihren Wunsch in eigenen Worten und bekommen bis zu drei freie Zeiten (`POST /api/online/locations/{id}/assistant`).

Der Assistent schlägt nur vor. Eine freie Zeit wird erst gebucht, wenn die Person einen Kunden wählt und bestätigt, über dieselbe Buchung wie im Kalender. Die KI versteht nur die Frage; Zeiten und Zahlen rechnet der Server aus Kalender und Kassa, und jede Antwort trägt den Hinweis auf den KI-Assistenten. Mit `Ai:Claude:ApiKey` versteht Claude die Frage, ohne Schlüssel eine regelbasierte Variante (Deutsch und Englisch, wie bei der Terminannahme). Ist das lokale Modell eingerichtet (`Ai:Local:Url`), versteht es die Frage, wenn Claude nicht antwortet oder kein Schlüssel gesetzt ist.
