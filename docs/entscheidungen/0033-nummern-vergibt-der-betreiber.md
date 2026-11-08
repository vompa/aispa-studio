# 0033. Postfach richtet das Spa ein, Nummern vergibt der Betreiber

- **Status:** angenommen
- **Datum:** 18.10.2026
- **Entschieden von:** Claude (autonom, siehe ADR 0019)

## Kontext

Bisher gab es ein Postfach und eine Nummer für die ganze Installation; welches Spa gemeint ist, stand in der Konfiguration (`MailIntake:TenantId`) oder in der Webhook-Adresse (`?spa=`). Mit mehreren Spas braucht jedes ein eigenes Postfach, und eine SMS oder WhatsApp muss beim Spa landen, dessen Nummer der Gast angeschrieben hat.

## Optionen

| Option | Bewertung |
| --- | --- |
| Postfach und Nummern trägt das Spa selbst ein | einfach, aber ein Spa könnte die Nummer eines anderen beanspruchen und dessen Nachrichten lesen |
| Beides nur über die Konfiguration | sicher, aber jede Änderung braucht einen Neustart, und Spas können ihr Postfach nicht selbst einrichten |
| Postfach trägt das Spa ein, Nummern ordnet der Betreiber zu | gewählt |

## Entscheidung

- Das Spa richtet sein Postfach (IMAP oder Microsoft 365) unter „Stammdaten“ › „Postfach & Nummern“ selbst ein. Passwort bzw. Client-Geheimnis liegen mit ASP.NET Data Protection verschlüsselt in der Datenbank, gebunden an Spa und Postfach, und werden nie zurückgegeben.
- Nummern (und Sammeladressen) ordnet der Betreiber über `/api/operator/routes` zu, geschützt mit `Operator:ApiKey`. Eine Nummer gehört genau einem Spa (eindeutiger Index über alle Spas).
- Eingehend gilt: `?spa=` in der Webhook-Adresse, sonst die angeschriebene Nummer, sonst `MailIntake:TenantId`, sonst das einzige Spa.
- Antworten und Erinnerungen gehen von der Nummer des Spas (Twilio: Absendernummer, Meta: Phone-Number-ID).

## Begründung

Wer ein Postfach einträgt, beweist mit dem Passwort, dass es ihm gehört. Bei Nummern gibt es diesen Beweis nicht: sie liegen im Anbieterkonto des Betreibers, und nur er weiß, welche Nummer er für welches Spa gekauft hat.

## Folgen und Stand

- Code: `MailIntake/IntakeSources.cs`, `IntakeEndpoints.cs`, Abruf in `MailboxPoller.PollSpaMailboxesAsync`.
- Im Echtbetrieb müssen die Data-Protection-Schlüssel dauerhaft und geschützt liegen (Azure Blob plus Key Vault, ADR 0016); gehen sie verloren, muss jedes Spa sein Passwort neu eingeben, der Fehler steht dann beim Postfach.
- Nachtrag (PR #45): Antworten und Erinnerungen per Mail gehen auf Wunsch aus dem Postfach des Spas (SMTP des Spas oder Microsoft 365), sonst über den gemeinsamen Versand mit dem Postfach als Antwortadresse. Die Seite `/betreiber/` zeigt alle Spas und ordnet Nummern zu; angemeldet wird weiter mit `Operator:ApiKey`, nicht mit einem Konto, weil der Betreiber zu keinem Spa gehört und es bisher nur eine Person in dieser Rolle gibt.

## Quellen

- PR #40, PR #45
- README, Abschnitt Terminannahme
