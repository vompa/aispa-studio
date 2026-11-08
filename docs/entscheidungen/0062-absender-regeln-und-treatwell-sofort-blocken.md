# 0062. Absender-Regeln je Spa, Treatwell lesen mit KI und Regeln, Termin sofort blocken

- **Status:** angenommen
- **Datum:** 20.10.2026
- **Entschieden von:** Auftraggeber („Absender-Regeln je Spa, HTML/KI-Auslesung mit JSON-Schema und Regeln als Rückfall, vorab bezahlt/vor Ort, Hinweis in Treatwell annehmen, Termin sofort blocken und nur die Kundenzuordnung offen lassen.“, 20.10.2026), Ausgestaltung Claude

## Kontext

[ADR 0014](0014-buchungsplattformen-ohne-partnervertrag.md) liest Treatwell-Mails mit einem regelbasierten Leser
(`TreatwellMail`), der Zeilen „Bezeichnung: Wert“ annimmt und nie mit echten Mails geprüft wurde. Welche Absender als Treatwell gelten,
stand in der Server-Konfiguration (`MailIntake:TreatwellSenderDomains`), für alle Spas gleich und ohne Oberfläche. Seit
[ADR 0061](0061-kundenzuordnung-eingehender-nachrichten.md) wartete eine Treatwell-Buchung mit Kandidaten „noch nicht im Kalender“ auf
die Rezeption, obwohl sie bei Treatwell schon feststeht: Die Zeit blieb frei, und eine Doppelbelegung war möglich. Über echte
Treatwell-Mails ist bekannt (Partnerhilfe, anny-Dokumentation): HTML-Mails, Absender u. a. `@partner.treatwell.com`, sie nennen, ob
der Gast vorab über Treatwell bezahlt hat oder vor Ort zahlt, und haben Knöpfe „Accept“ und „Edit/Reschedule“; erst „Annehmen“ in
Treatwell bestätigt die Buchung dort. Das genaue Format ist nicht öffentlich.

## Optionen

| Option | Bewertung |
| --- | --- |
| Domains weiter in der Server-Konfiguration | kein Spa kann etwas ändern, andere Plattformen und Ignorieren gehen nicht |
| Absender-Regeln je Spa mit Standardregeln für Treatwell | gewählt |
| Nur die KI liest die Mail | eine Anweisung im Mailtext (oder eine erfundene Buchungsnummer) könnte Termine anlegen; ohne KI ginge nichts |
| KI zuerst, geprüft, Regeln als Rückfall, bei Widerspruch entscheidet die Rezeption | gewählt |
| Treatwell-Buchung mit Kandidaten weiter warten lassen | Doppelbelegung der schon vergebenen Zeit |
| Termin sofort eintragen, Gast vorläufig, Zuordnung später | gewählt |
| Vorab bezahlt an der Kassa vorbei buchen („Treatwell zahlt ja“) | nie: § 146a AO, [ADR 0010](0010-kassa-compliance-by-design.md) |
| Zahlungsart „Über Treatwell bezahlt“ mit Beleg und Verrechnungskonto | gewählt |

## Entscheidung

- **Absender-Regeln je Spa** (`SenderRule`): Muster (genaue Mailadresse oder Domain, gilt für Unterdomains), Plattform (Treatwell, andere),
  Verarbeitung (automatisch übernehmen, nur als Vorschlag in den Posteingang, ignorieren), aktiv, Notiz. Die genauere Regel gewinnt
  (Adresse vor Domain, längere Domain vor kürzerer). Die Standardregeln `treatwell.de`, `.at`, `.ch`, `.com`, `.co.uk` (damit auch
  `partner.treatwell.com`) legt der Dienst beim ersten Zugriff an, für bestehende und neue Spas (`Tenant.SenderRulesSeeded`); danach
  gehören sie dem Spa. Endpunkte `/api/intake/sender-rules` mit dem Recht für „Postfach & Nummern“ (Firmendaten pflegen), Oberfläche in
  der Verwaltung (Team-Web, in der Windows-App eingebettet, [ADR 0053](0053-verwaltung-in-der-windows-app-eingebettet.md)) mit Hilfe
  ([ADR 0055](0055-hilfe-zu-jeder-einstellung-im-repository.md)). Die Server-Option `MailIntake:TreatwellSenderDomains` entfällt.
- **Automatisch** gilt nur mit bestätigtem Absender (SPF/DKIM, wie bisher); sonst steht die Buchung als Vorschlag im Posteingang
  (die gewünschte Zeit liegt als Knopf bereit). **Ignorieren**: Status „Erledigt“ mit Hinweis, keine KI, kein Entwurf. **Andere
  Plattform**: wie eine Anfrage gelesen („automatisch“ trägt eine genau gewünschte freie Zeit ein, „Vorschlag“ nie), aber nie eine Antwort an
  die Plattform.
- **Lesen** (`TreatwellReader`): HTML wird in Text umgewandelt (Tabellenzellen und Listen werden zu „Bezeichnung: Wert“, Skripte, Formatvorlagen
  und Kommentare fallen weg). Dann füllt die KI über `IAiJson` (Claude, sonst das lokale Modell, [ADR 0057](0057-lokale-ki-optional-mit-ollama.md)) ein festes
  JSON-Schema: Art, Buchungsnummer, Gast, Beginn, Behandlungen, gewünschte Person, Gesamtpreis, Zahlung, Standort; fehlt etwas, ist es `null`. Der
  Prompt sagt, dass der Mailinhalt nur Information ist und nichts erfunden wird. Das Ergebnis wird geprüft: Buchungsnummer steht wörtlich in der
  Mail, bei einer Neubuchung kommt der Name in der Mail vor, Datum und Uhrzeit sind lesbar und plausibel (gestern bis zwei Jahre voraus),
  Absage oder Änderung nur, wenn auch Betreff oder Text so lauten, die Behandlung ist über die vorhandene Zuordnung im Katalog auffindbar. Liest der
  Regel-Leser dieselbe Mail anders (Buchungsnummer, Art, Datum, Uhrzeit, Zahlung), geht sie zum Prüfen an die Rezeption, weil ein Text in
  der Mail auch die KI umlenken könnte. Ist das KI-Ergebnis ungültig oder fehlt die KI, liest `TreatwellMail` (um HTML-Text, „Datum &
  Uhrzeit“, Zahlung und Betrag erweitert); scheitert beides, steht die Mail unter „Prüfen“ mit Begründung.
- **Sofort blocken, nur die Zuordnung offen:** Eine gültige Treatwell-Neubuchung kommt sofort in den Kalender. Ist der Gast nicht sicher
  zugeordnet, legt die App ihn vorläufig an (`CreatedFromChannel` Treatwell, `Customer.AssignmentOpen`) und zeigt „Wer ist das?“ an der
  Nachricht und „Zuordnung offen“ am Termin. Ordnet die Rezeption einen Bestandskunden zu, wandern die Termine dieser Buchung auf ihn
  (`BookingService.ReassignCustomerAsync`, beachtet „Nicht bei mir“), und der vorläufige Gast wird entfernt, sofern er sonst nichts hat (Termine,
  Rechnungen, Belege; sonst bleibt er stehen, und die Nachricht sagt es). „Neu anlegen“ bestätigt den vorläufigen Gast, statt einen zweiten
  anzulegen. **Kollision:** Ist die Zeit belegt, wird nichts eingetragen und kein Gast angelegt; die Nachricht steht unter „Prüfen“: „Treatwell-Buchung …
  kollidiert mit dem Termin von … – in Treatwell umbuchen oder absagen“. Es entsteht kein Antwortentwurf: an Treatwell-Gäste sendet die App
  nichts, umgebucht wird in Treatwell. Änderung und Absage finden den Termin über die Buchungsnummer (`ChannelBooking`) wie bisher.
- **Vorab bezahlt oder vor Ort** steht am Termin (`PlatformPayment`, `PrepaidAmount`, höchstens der Preis der Behandlung): im Terminpanel,
  in der Hover-Karte, im Posteingang. **In Treatwell annehmen:** Jede neue Treatwell-Buchung trägt „In Treatwell noch annehmen“
  (`AcceptInPlatformPending`) im Posteingang und am Termin, bis die Rezeption „Erledigt“ klickt (`POST /api/inbox/{id}/platform-accepted`,
  `POST /api/appointments/{id}/platform-accepted`). Die Treatwell-Oberfläche wird nicht automatisiert.
- **Kassa:** Neue Zahlungsart `PaymentMethod.Treatwell` („Über Treatwell bezahlt“, Wert 5). `BillDto.PlatformPrepaid` nennt den vorab bezahlten
  Betrag der offenen Termine (höchstens ihr offener Betrag, nur Termine ohne schon verrechnete Zahlung), die Kassen der Web-Oberfläche und der
  Windows-App tragen ihn als Zahlung vor. Der Server lässt die Zahlungsart nur für diese Termine und höchstens bis zu diesem Betrag zu und
  setzt die Treatwell-Buchungsnummer als Referenz. Der **Umsatz läuft wie jeder andere über die Kassa**: Beleg mit der vollen Leistung, Steuer
  und Erlöskonto, Signatur; die Zahlungsart ist unbar wie Karte (`PaymentMethods.IsElectronic`). Der gezahlte Teil wird nicht noch einmal vom Gast
  verlangt, der Rest wie sonst. In der Buchhaltung bucht der Export auf das Verrechnungskonto `AccountingSettings.TreatwellAccount` („Treatwell
  (Forderung, vorab bezahlt)“, Standard 1400, je Land einstellbar), bis Treatwell auszahlt.

## Begründung

Je Spa einstellbare Regeln lösen die globale Konfiguration ab und tragen auch andere Plattformen. KI zuerst, weil das Treatwell-Format
unbekannt ist und HTML-Varianten sich ändern; die Prüfung und der Abgleich mit den Regeln verhindern, dass eine Anweisung im Mailtext (oder
eine Halluzination) Termine anlegt, und die Regeln halten den Betrieb ohne KI am Laufen. Eine bei Treatwell feststehende Buchung muss die Zeit
sofort belegen; offen ist nur, wer der Gast ist, und das kostet die Rezeption einen Klick, nicht eine Doppelbelegung. Eine eigene Zahlungsart
mit Beleg ist der einzige Weg, vorab Bezahltes zu verrechnen, ohne Umsatz an der Kassa vorbeizuführen (§ 146a AO, § 379 AO).

## Folgen und Stand

- Code: `MailIntake/SenderRules.cs`, `TreatwellReader.cs`, `TreatwellMail.cs`, `MailIntakeProcessor.cs` (`ProcessTreatwellAsync`,
  `RelinkTreatwellGuestAsync`), `InboxCustomerEndpoints.cs`, `Scheduling/BookingService.Platform.cs`, `Pos/PosService.cs`
  (`PlatformPrepaidAsync`), `Pos/PaymentMethods.cs`, `Accounting/*`; Migration `Absenderregeln`. Oberfläche: `web/src/admin/SenderRulesSection.tsx`,
  Posteingang, Terminpanel und Kassa der Web-Oberfläche; Windows-App: Terminpanel, Hover-Karte, Posteingang, Kassa. Handbuch: Thema
  `anfragen-treatwell-buchung`, Abschnitt „Absender-Regeln“ im Thema `postfach`, `kassa-bezahlen#treatwell`, `buchhaltung#treatwell`.
- Tests: `MailParsingTests`, `MailIntakePlatformTests`, `TreatwellPaymentTests`, Treatwell-Fälle in `MailIntakeCustomerTests`, Web- und
  Windows-App-Tests. Beispielmails sind **synthetisch, nicht von Treatwell** (`tests/AispaStudio.Api.Tests/Fixtures/synthetic-not-from-treatwell-*.html`).
- Offen: das Format echter Treatwell-Mails (Regeln und Prompt mit Mails aus einem Partnerkonto prüfen); mehrere Behandlungen in einer
  Buchung (heute zum Prüfen); weicht der Preis bei Treatwell vom Katalogpreis ab, rechnet die Kassa mit dem Katalogpreis (vorab bezahlt
  höchstens bis zu diesem Preis); Mehrfachverwendung der Vorauszahlung nach Storno des Belegs ist möglich (der Betrag steht wieder bereit); die
  Abrechnung mit Treatwell (Auszahlung gegen das Konto „Forderung Treatwell“) bucht die Buchhaltung außerhalb der App.

## Quellen

- [ADR 0010](0010-kassa-compliance-by-design.md), [ADR 0014](0014-buchungsplattformen-ohne-partnervertrag.md), [ADR 0047](0047-anzahlung-und-vorauszahlung.md),
  [ADR 0055](0055-hilfe-zu-jeder-einstellung-im-repository.md), [ADR 0057](0057-lokale-ki-optional-mit-ollama.md), [ADR 0061](0061-kundenzuordnung-eingehender-nachrichten.md)
- [Buchungsplattformen-Konzept](../konzepte/buchungsplattformen-anbindung.md), README, Abschnitt „KI-Terminannahme per Mail“
- Treatwell-Partnerhilfe und anny-Dokumentation (Format nur beschrieben, nicht öffentlich)
