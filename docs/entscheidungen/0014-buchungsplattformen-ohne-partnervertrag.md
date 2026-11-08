# 0014. Buchungsplattformen: Treatwell über Mail und Kalender-Feed, Partneranfragen zurückgestellt

- **Status:** angenommen
- **Datum:** 17.10.2026
- **Entschieden von:** Auftraggeber

## Kontext

Auftraggeber: „Wie könnte man externe Buchungsplattformen automatisiert anbinden, z. B. Treatwell?“ und „Treatwell schickt Mails bei neuen Buchungen, die könnte man verwerten.“ Treatwell hat keine öffentliche API; Echtzeit nur mit Partnervertrag. Google hat mit dem Actions Center eine offizielle Schnittstelle.

## Optionen

| Option | Bewertung |
| --- | --- |
| Warten, bis der Treatwell-Partnervertrag steht | Kunden brauchen Treatwell vom ersten Tag an |
| Portale per Bildschirm-Automatisierung bedienen | brüchig, nur letzter Ausweg |
| Plattformen gar nicht anbinden | Treatwell ist ein wichtiger Neukundenkanal |
| Kanal-Adapter hinter der Queue, Treatwell zuerst per Mail und ICS | gewählt |

## Entscheidung

Ein Kanal-Adapter je Plattform hinter der Queue; unser Buchungsdienst ist die einzige Wahrheit über freie Slots. Treatwell-Buchungen kommen über die Auswertung der Bestätigungsmails, unsere Belegung geht als ICS-Feed zurück. Partneranfragen an Treatwell und Google sind auf Wunsch des Auftraggebers zurückgestellt und werden nicht versendet.

## Begründung

So ist Treatwell sofort nutzbar, ohne auf einen Vertrag zu warten. Der Auftraggeber will die Partnergespräche erst später führen.

## Folgen und Stand

Umgesetzt in PR #8 (`TreatwellMail`, nur mit bestätigtem Absender; ICS-Feeds je Person oder Standort). Das Mail-Format ist angenommen und muss mit echten Treatwell-Mails geprüft werden. Entwürfe der Anfragen: partneranfragen-entwurf.md.

## Quellen

- [Buchungsplattformen-Konzept](../konzepte/buchungsplattformen-anbindung.md)
