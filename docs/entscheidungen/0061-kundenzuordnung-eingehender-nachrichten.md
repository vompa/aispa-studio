# 0061. Kundenzuordnung eingehender Nachrichten: sicher, Vorschlag oder neu

- **Status:** angenommen
- **Datum:** 20.10.2026
- **Entschieden von:** Auftraggeber („Bau zuerst die gestufte Kundenzuordnung für alle Kanäle (Vorschläge, Neuanlage nur ohne Kandidaten, mehrere Mailadressen, Datenschutz)“, 20.10.2026), Ausgestaltung Claude

## Kontext

Die KI-Terminannahme ordnete Absender bisher nur über die genau gleiche Mailadresse bzw. Nummer einem Kunden zu und legte sonst beim Buchen still einen neuen an, auch in der Rezeption beim Bestätigen im Posteingang. Das erzeugt Dubletten („Sonja Winkler“ schreibt von der privaten statt der geschäftlichen Adresse) und, schlimmer, ein Datenschutzrisiko: Wer unter fremdem oder falschem Absender „absagen“ schreibt, bekäme im Antwortentwurf einen Termin aus der Kundenkarte genannt, sobald die Zuordnung falsch ist. Kunden hatten außerdem nur eine Mailadresse. Nach [ADR 0008](0008-ki-schlaegt-vor-mensch-bestaetigt.md) schlägt die KI vor, der Mensch bestätigt; [ADR 0040](0040-einwilligung-erinnerung-naechste-behandlung.md) und [ADR 0057](0057-lokale-ki-optional-mit-ollama.md) halten fest, dass Daten über Gäste nur dorthin gehen, wo es nötig und nachvollziehbar ist.

## Optionen

| Option | Bewertung |
| --- | --- |
| Wie bisher, nur Namen vergleichen und bei gleichem Namen zuordnen | schnell, aber ein Namensvetter bekäme Termine und Guthaben eines anderen; verletzt ADR 0008 |
| Immer die Rezeption entscheiden lassen | sicher, aber jede Nachricht eines Stammgasts bräuchte einen Klick; die Terminannahme verlöre ihren Sinn |
| Gestuft: sicher nur bei gleicher Adresse oder Nummer, sonst Vorschläge, neu nur ohne Kandidaten | gewählt |

## Entscheidung

- **Ein Dienst, `CustomerMatcher`**, für alle Kanäle (Mail, SMS, WhatsApp, Anruf-Abschrift, Treatwell) und für den Dublettenhinweis beim Anlegen. Ergebnis: ein sicherer Treffer oder höchstens drei Kandidaten mit Stufe und deutscher Begründung.
  - **Stufe 1 (sicher):** Mailadresse gleich (Hauptadresse oder eine weitere, Groß-/Kleinschreibung egal) oder Absendernummer gleich nach Normalisierung auf E.164 (AT, DE, CH; „0660 1234567“ gilt im Land des Spas als „+436601234567“). Teilen sich mehrere Kunden die Adresse oder Nummer, ist es nicht sicher.
  - **Stufe 2:** die von der KI aus Text oder Signatur gelesene Nummer passt genau.
  - **Stufe 3:** Name passt (ohne Unterschied bei Groß-/Kleinschreibung, ä/ae, ß/ss, Akzenten) und ein zweites Merkmal: die letzten vier Ziffern der Nummer, der Geburtstag (wenn genannt) oder ein Termin in den letzten 24 Monaten oder danach.
  - **Stufe 4:** nur ähnlicher Name: gleicher Nachname und Vorname-Anfang, oder kleine Tippfehler (Abstand 1, bei langen Namen 2).
  - Der Mandantenfilter gilt automatisch. Kunden mit „Nicht mehr buchen“ bleiben Kandidaten und sind im Grund gekennzeichnet; gelöschte oder anonymisierte Kunden gibt es im Modell nicht (kein Löschen von Kunden), es gibt also nichts auszuschließen.
- **Terminannahme:** Zugeordnet und gebucht wird nur bei Stufe 1 oder wenn es gar keinen Kandidaten gibt. Dann entsteht der Kunde neu, mit Notiz „Aus <Kanal> angelegt“ und dem Feld `CreatedFromChannel`, das die Kundenkarte als „bitte Angaben prüfen“ zeigt. Gibt es Kandidaten, bleibt die Nachricht auf „Prüfen“, bucht nichts, legt niemanden an, speichert die Kandidaten an der Nachricht (`CustomerCandidates`, jsonb) und zeigt im Posteingang „Wer ist das?“. Die KI schlägt trotzdem Zeiten vor und entwirft die Antwort, ohne Daten aus einer Kundenkarte.
- **Datenschutz:** `InboundMail.CustomerConfirmed` ist die einzige Freigabe: sicher erkannt oder von der Rezeption bestätigt. Solange sie fehlt, nennen Antwortentwurf, Hinweis, Terminsuche, Stornoknopf und Prompts an die KI nichts aus der Kundenkarte (Termine, Guthaben, Pakete, Vorlieben, Kennzeichen, Anamnese). Das Stornieren aus dem Posteingang und das Neuentwerfen der Bestätigung brauchen die Freigabe.
- **Rezeption entscheidet:** `POST /api/inbox/{id}/customer/assign` (optional Adresse oder Nummer in der Kundenkarte merken), `…/customer/new` (vorbefüllt aus Name, Telefon und Mail der Nachricht) und `…/customer/release` (Zuordnung lösen; der Kunde wird dann ausgeschlossen). Alle mit dem Recht „Buchen“. Das Buchen aus dem Posteingang legt bei offenen Kandidaten nicht mehr still neu an, sondern antwortet mit „Wer ist das? …“.
- **Mehrere Mailadressen:** Tabelle `CustomerEmails` (Kind von `Customers`, `ValueGeneratedNever`), Hauptadresse bleibt `Customer.Email`. Endpunkte unter `/api/customers/{id}/emails`. Der Abgleich und die Suche kennen sie. Das Änderungsprotokoll für den Einzelplatz erfasst die neue Tabelle wie jede Fachtabelle (Trigger an allen Tabellen mit Primärschlüssel, Mandant über `TenantId`); ein Test prüft es.
- **Dublettenhinweis:** `POST /api/customers/duplicates` (Angaben im Rumpf, nicht in der Adresse) nutzt denselben Dienst; die Windows-App zeigt beim Anlegen „Gibt es schon?“ mit „Übernehmen“.

## Begründung

Sicherheit entsteht aus dem, was der Absender nicht fälschen kann, ohne dass die Rezeption es merkt: eine Adresse, die in der Kundenkarte steht, oder eine Nummer, von der die Nachricht kommt. Namen und Nummern im Text kann jeder angeben; sie sind Hinweise, keine Beweise. Ein Vorschlag mit Begründung kostet die Rezeption einen Klick, ein falsch zugeordneter Kunde dagegen im schlimmsten Fall eine Datenschutzverletzung. Dass die KI trotzdem Zeiten vorschlägt, hält die Terminannahme nützlich, solange die Zuordnung offen ist.

## Folgen und Stand

- Code: `MailIntake/CustomerMatcher.cs`, `MailIntake/InboxCustomerEndpoints.cs`, `Scheduling/CustomerEmails.cs`, `PhoneNumbers.ToE164`; Migration `Kundenzuordnung` (bestehende Zuordnungen gelten als bestätigt). Windows-App: Abschnitt „WER IST DAS?“ im Posteingang, „Gibt es schon?“, weitere Adressen in der Kundenkarte; Web-Posteingang: Vorschläge, Zuordnen, Neu anlegen, Lösen.
- Treatwell-Buchungen mit Kandidaten stehen „noch nicht im Kalender“, bis die Rezeption entscheidet, und werden danach übernommen. Weil die Buchung bei Treatwell schon feststeht, kann in dieser Zeit eine Doppelbelegung entstehen; die Rezeption sieht die Nachricht im Posteingang.
- Offen: Geburtstag aus dem Nachrichtentext (die KI liest ihn noch nicht), mehrere Telefonnummern je Kunde (eine Nummer wird nur gemerkt, wenn die Karte noch keine hat), Vorschläge nach der Zuordnung mit Wünschen und Kennzeichen des Kunden neu rechnen, Kundenkonten des Online-Bereichs über weitere Adressen verknüpfen (bewusst unverändert).

## Quellen

- [ADR 0008](0008-ki-schlaegt-vor-mensch-bestaetigt.md), [ADR 0041](0041-einzelplatz-standalone-und-zuschaltbare-webmodule.md), README, Abschnitt „KI-Terminannahme per Mail“
- Handbuch: `web/src/help/content/anfragen-wer-ist-das.md`
