---
art: ablauf
bereich: Anfragen
oberflaeche: windows-app
rollen: rezeption, leitung, inhaber, manager
seite: anfragen
stichwoerter: Treatwell, Buchung, annehmen, in Treatwell annehmen, vorab bezahlt, Treatwell Pay, Zahlung im Salon, vor Ort, Zuordnung offen, vorläufiger Gast, Kollision, umbuchen, Absender-Regel, Plattform, booking platform, accept, prepaid
---
# Treatwell-Buchung im Posteingang

Eine Buchung über Treatwell kommt als Mail. Die App liest sie (mit KI, ersatzweise nach festen Regeln) und trägt sie sofort in den Kalender ein, damit niemand die Zeit noch einmal vergibt. Offen bleibt nur, was die Rezeption entscheiden muss: Wer ist der Gast, und ist die Buchung in Treatwell angenommen? Ob eine Mail überhaupt als Treatwell gilt und ob sie automatisch übernommen wird, bestimmen die [Absender-Regeln](hilfe:postfach#absender-regeln).

![Treatwell-Buchung im Posteingang](images/app/posteingang-treatwell.png)

## Was die Nachricht zeigt {#anzeige}

- **Erkannt von der KI** nennt Buchungsnummer, Behandlung, Zeit, Gast, die Zahlung und womit die Mail gelesen wurde („gelesen mit KI“ oder „gelesen mit Regeln“).
- **Zahlung**: „Vorab über Treatwell bezahlt: 35,00 €“ (der Gast hat bei Treatwell schon bezahlt) oder „Zahlt vor Ort“. Das steht auch am Termin: im Terminpanel und in der Hover-Karte des Kalenders. Beim Kassieren schlägt die Kassa den vorab bezahlten Betrag vor, siehe [Bezahlen](hilfe:kassa-bezahlen#treatwell).
- **In Treatwell noch annehmen**: Eine neue Buchung gilt bei Treatwell erst, wenn man sie dort annimmt. Das passiert in Treatwell selbst; die App bedient Treatwell nicht.

## In Treatwell annehmen {#annehmen}

1. In Treatwell anmelden und die Buchung annehmen (bei Bedarf dort bearbeiten oder verschieben).
2. In der Nachricht unter **In Treatwell noch annehmen** auf **Erledigt** klicken. Der Hinweis verschwindet auch am Termin.

Dasselbe geht im Terminpanel des Kalenders (**Erledigt** unter „In Treatwell noch annehmen“). Bis dahin zeigt die Hover-Karte des Termins „In Treatwell noch annehmen“ in Orange.

![Termin aus einer Treatwell-Buchung im Kalender](images/app/kalender-treatwell-termin.png)

## Wer ist das? {#gast}

Kennt die App den Gast nicht sicher, steht der Termin trotzdem im Kalender. Der Gast ist dann vorläufig angelegt, im Terminpanel steht „Zuordnung offen“, und in der Nachricht steht **Wer ist das?** mit Vorschlägen.

1. Beim richtigen Kunden auf **Zuordnen** klicken. Der Termin wandert auf diesen Kunden, der vorläufige Gast wird entfernt, sofern er sonst nichts hat (keine weiteren Termine, Rechnungen oder Belege); sonst bleibt er stehen, und die Nachricht sagt es.
2. Ist der Gast neu, auf **Als neuen Kunden bestätigen** klicken. Der vorläufige Gast wird zum Kunden; es entsteht kein zweiter.

Mehr dazu: [Wer ist das?](hilfe:anfragen-wer-ist-das). Ist der Gast sicher erkannt (gleiche Mailadresse oder Nummer wie in der Kundenkarte), gibt es diese Frage nicht.

## Änderung und Absage {#aenderung}

Treatwell schickt Änderung und Absage mit derselben Buchungsnummer; die App findet den Termin darüber. Eine Verschiebung bewegt den Termin, eine Absage storniert ihn. Bei der Regel „nur als Vorschlag“ oder bei nicht bestätigtem Absender wird die Absage nur gemeldet, und der Termin wird von Hand storniert.

## Wenn die Zeit schon belegt ist {#kollision}

Kollidiert die Buchung mit einem Termin im Kalender, trägt die App **nichts** ein und legt keinen Gast an. Die Nachricht steht unter „Prüfen“, z. B.: „Treatwell-Buchung TW-… (24.10.2026 14:00) kollidiert mit dem Termin von Eva Muster (Massage, 14:00–14:50). In Treatwell umbuchen oder absagen.“

1. Den Gast in Treatwell umbuchen oder die Buchung dort absagen.
2. Die Nachricht mit **Erledigt, ohne Termin und Antwort** ablegen. Bucht Treatwell um, kommt eine Änderung mit derselben Nummer, und die App trägt den neuen Termin ein, wenn er frei ist.

Die freien Zeiten darunter helfen bei der Suche. An Gäste von Treatwell sendet die App nichts; Antworten gehen über Treatwell.

## Gut zu wissen {#hinweise}

- Enthält eine Buchung **mehrere Behandlungen**, trägt die App sie nicht selbst ein: Die Nachricht steht unter „Prüfen“, und die Termine werden von Hand eingetragen.
- Kann die App die Mail nicht lesen (keine Buchungsnummer, KI und Regeln widersprechen sich), steht sie unter „Prüfen“ mit der Begründung, und die Buchung wird von Hand übernommen. Das Format echter Treatwell-Mails ist nicht öffentlich; die Regeln werden mit Mails aus einem Partnerkonto nachgeschärft.
- Der Text in einer Mail ist für die KI nur Information. Anweisungen darin befolgt sie nicht; weichen KI und Regeln bei Buchungsnummer, Art, Datum, Uhrzeit oder Zahlung voneinander ab, entscheidet die Rezeption.
- „Zahlt vor Ort“ heißt: Der Gast bezahlt wie jeder andere an der Kassa.
