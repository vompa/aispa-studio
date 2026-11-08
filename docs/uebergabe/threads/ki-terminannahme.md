# Übergabe: Thread „KI-Terminannahme“ (Stand 18.10.2026, 21:40 UTC)

Gestoppt auf Anweisung des Auftraggebers vom 18.10.2026, 21:34. Es gibt keinen offenen PR. Der Branch `claude/ki-terminannahme-rci4gj` steht auf `main` (9d53ea3), es liegt nichts Ungemergtes darauf.

## Gebaut und gemergt

| PR | Inhalt |
| --- | --- |
| #8 | KI-Terminannahme per Mail, Treatwell-Übernahme (nur mit bestätigtem Absender), ICS-Feeds |
| #13 | Echte Postfächer über IMAP und Microsoft Graph |
| #17 | SMS, WhatsApp (Twilio, Meta Cloud API) und Anrufe als Abschrift im selben Posteingang |
| #18 | KI-Assistent je Rolle (Buchungshilfe, Auswertungen, Terminvorschläge für Gäste) |
| #30 | Posteingang live per SignalR |
| #34 | Terminerinnerungen 48 h und 2 h vorher per Mail, SMS, WhatsApp-Vorlage |
| #40 | Postfach je Spa, Nummern je Spa nur über den Betreiber, Spracherkennung Azure nur EU/CH |
| #45 | Gäste-Mails aus dem Postfach des Spas, Betreiber-Seite `/betreiber/` |
| #60 | Kopie gesendeter Mails im IMAP-Ordner „Gesendet“ |
| #62 | Betreiber sperrt ein Spa (Lesen und Export bleiben) |
| #69 | Erinnerung an die nächste Behandlung, Text von der KI, nur mit Double-Opt-in |
| #79 | KI-Antwortvorschlag auf Fragen von Gästen, nur aus den Stammdaten des Spas |
| #84 | Sprachassistent: Twilio nimmt Anrufe an, Ansage, Aufnahme, Abschrift, Antwort per SMS |

Code: `src/AispaStudio.Api/MailIntake` (mit `Assistant/`, `Channels/`, `Mailboxes/`), `src/AispaStudio.Api/Assistant`, `src/AispaStudio.Api/Reminders`, `src/AispaStudio.Api/Retention`. Details je PR in `docs/umsetzung/pr-protokoll.md`.

## Entscheidungen

- ADR 0033: Postfach richtet das Spa ein, Nummern vergibt der Betreiber.
- ADR 0039: Spa sperren statt löschen.
- ADR 0040: Erinnerung an die nächste Behandlung nur mit bestätigter Einwilligung (Double-Opt-in, eine Adresse, Grenzen für die Häufigkeit).
- ADR 0043: Anrufe mit Ansage (Hinweis auf Aufnahme und KI), einer Aufnahme und Antwort per SMS nach Freigabe; kein Gesprächs-Bot.
- Ohne ADR, im PR-Protokoll begründet:
  - Die KI schlägt nur vor, gebucht wird über `BookingService`. `AutoBook` ist an (nur bei freier Wunschzeit), `AutoSend` ist aus.
  - Der KI-Hinweis wird immer beim Senden angehängt, bei SMS und WhatsApp in Kurzform.
  - KI-Texte an Gäste bekommen keine Zahlen, die die KI erfinden könnte. Steht im Text eine Zahl, die nicht in den Angaben vorkommt, wird er verworfen (Erinnerung, Antwortvorschlag, Tagesstart).
  - Claude nur mit `Ai:Claude:ApiKey`, sonst regelbasiert ohne KI (auch in den Tests).
  - Treatwell-Mails werden nur mit bestätigtem Absender (SPF/DKIM/DMARC) übernommen.

## Halb fertig, bekannte Fehler

- Kein offener PR, keine halbfertige Arbeit.
- Behoben in #84: Ein Test zur Kartenfrist (`CardGuaranteeTests`, aus #76) war wackelig, weil er den Status vor dem Storno las.
- Das Treatwell-Mailformat ist angenommen. Es muss mit echten Mails geprüft werden, bevor ein Spa es nutzt.

## Nächste Punkte, in dieser Reihenfolge

1. KI-Vorschläge für Absagen und Verschiebungen im Posteingang: betroffenen Termin finden, Storno oder freie Ersatzzeiten vorschlagen, die Rezeption bestätigt.
2. Wissensbasis des Spas (Parken, Anreise, Gutscheine, Hausregeln) als weitere Quelle für Antwortvorschläge.
3. Einwilligung zur Erinnerung beim Online-Buchen anbieten (eigenes Häkchen, nicht vorausgewählt) und `List-Unsubscribe` in der Mail.
4. Ansage am Telefon je Spa und in weiteren Sprachen; Rückruf statt SMS für Festnetz-Anrufer.
5. KI-Vorschläge im Dienstplan, nur als Vorschlag. Vorher mit dem Kalender-Thread abstimmen, es gibt schon einen Entwurf (`RosterDraftTests`).

## Für später

- Auftragsverarbeitung: AV-Verträge mit Anthropic (Claude), Microsoft (Azure Speech, Graph) und Twilio, bevor echte Gästedaten fließen.
- Data-Protection-Schlüssel dauerhaft speichern (z. B. Key Vault). Sonst sind nach einem Neustart die Passwörter der Spa-Postfächer nicht mehr lesbar.
- Echte Treatwell-Mails sammeln und den Parser daran testen; besser wäre eine offizielle Schnittstelle. Die Partneranfrage ruht, sie wird nur mit Freigabe des Auftraggebers verschickt.
- WhatsApp: Erinnerungen und Werbung brauchen freigegebene Vorlagen. Für Werbung braucht es eine eigene Einwilligung, für die Schweiz eine eigene Prüfung (UWG Art. 3 Abs. 1 lit. o).
- Rechtlich gegenlesen lassen: die Texte der Einwilligung, die Ansage am Telefon und die französischen und italienischen Gästetexte.
- Konten beim Betreiber: eigenes Betreiber-Konto statt `Operator:ApiKey`; Daten gesperrter Spas nach Ablauf der Aufbewahrungsfrist löschen.
- Arbeitsweise: ADR-Nummern kollidieren zwischen parallelen Threads. Die Nummer erst kurz vor dem Merge auf `main` prüfen. Nach jedem Merge von `main` die Migrationen neu erzeugen, mit dem Snapshot von `main` als Grundlage.
