---
art: ablauf
bereich: Stammdaten
oberflaeche: verwaltung
rollen: inhaber, manager, leitung
seite: verwaltung
stichwoerter: Route planen, Anreise im Kundenbereich, Karten-App, Online-Buchung, Onlinebuchung einstellen, online buchbar, Vorlauf, Stornofrist, No-Show, Ausfallgebühr, Kreditkarte hinterlegen, Raster, Hinweis für Gäste, online booking rules, cancellation policy
---
# Online-Buchung eines Standorts einstellen

Je Standort lässt sich festlegen, ob und wie Gäste online buchen: Vorlauf, wie weit im Voraus, Stornofrist, Ausfallgebühr und ein Hinweis für Gäste.

## Schritte {#schritte}

1. In der Verwaltung **Stammdaten** öffnen, Reiter **Standorte & Räume**.
2. Links den Standort anklicken und rechts zum Abschnitt **Online-Buchung** gehen.
3. **Termine an diesem Standort online buchbar** an- oder abhaken.
4. Die Werte einstellen, z. B. **Vorlauf (Minuten)**, **Buchbar im Voraus (Tage)**, **Online-Storno bis (Stunden vorher)** und **Startzeiten im Raster**.
5. Für eine Ausfallgebühr **Ausfallgebühr (% des Preises, 0 = keine)** setzen, bei Bedarf **Ohne Karte freigeben nach (Stunden, 0 = nie)** und **Ausfallgebühr nur von neuen Gästen verlangen …**.
6. Optional **Hinweis für Gäste (erscheint vor dem Buchen)** eintragen.
7. Auf **Regeln speichern** klicken.

## Zurück zu den Vorgaben {#vorgaben}

Hat ein Standort eigene Regeln, setzt **Vorgaben wiederherstellen** ihn auf die Vorgaben für alle Standorte zurück. Solange ein Standort keine eigenen Regeln hat, steht oben „Es gelten die Vorgaben für alle Standorte. …“.

## Was Gäste beim Standort sehen: Route planen {#route}

Bei einem gebuchten Termin zeigt der Kundenbereich unter dem Standort die Anschrift und den Knopf **Route planen**. Er öffnet die Karten-App des Geräts mit der Anschrift als Ziel (Apple Karten auf iPhone, iPad und Mac, sonst Google Maps). Der Startpunkt ist der aktuelle Standort des Geräts; den erfragt die Karten-App selbst, der Kundenbereich fragt den Standort nie ab und speichert nichts. Darunter steht, wenn eingetragen, der Text aus [Anreise und Parkplätze](hilfe:standorte#anreise). Dieselbe Anreise steht in der Bestätigungsmail der Buchung, mit dem Link zur Routenplanung.

![Kundenbereich: Termin mit Anschrift, Route planen und Anreise](images/web/kundenbereich-route.png)

- Ohne Anschrift gibt es keinen Knopf; ohne Anreise-Text keine Zeile „Anreise“.
- Beides pflegt man bei **Stammdaten › Standorte & Räume › Anschrift und Kontakt** bzw. **Anreise und Parkplätze**.

## Gut zu wissen {#hinweise}

- Eine [Schließzeit](hilfe:schliesszeiten) des Standorts sperrt die Online-Buchung in ihrer Zeit: der Kundenbereich zeigt keine freien Zeiten und bei der Buchung den Hinweis „Vom … bis … geschlossen“ mit dem Text für Gäste.
- Ändern dürfen Inhaber und Standortleitung; alle anderen sehen die Regeln nur.
- Eine Ausfallgebühr wirkt nur mit eingerichtetem Zahlungsanbieter: [Zahlungskonto verbinden](hilfe:stammdaten-zahlungskonto-verbinden).
- Eine Behandlung kann eigene Prozent für die Ausfallgebühr haben: [Behandlung ändern](hilfe:stammdaten-behandlung-anlegen#aendern).
- Die Online-Buchung ist ein Webmodul; im Einzelplatz-Betrieb ist sie meist ausgeschaltet: [Einzelplatz](hilfe:einzelplatz).

Felder im Einzelnen: [Online-Buchung](hilfe:standorte#online-buchung).
