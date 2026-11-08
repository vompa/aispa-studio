# Externe Buchungsplattformen automatisiert anbinden

Stand: 17.10.2026 · Konzept; Treatwell per Mail ist umgesetzt, seit 20.10.2026 mit Absender-Regeln, KI-Auslesung und sofortigem Blocken ([ADR 0062](../entscheidungen/0062-absender-regeln-und-treatwell-sofort-blocken.md))

## 1. Welche Plattformen für Day-Spas in DACH zählen

| Plattform | Art | Offizielle Schnittstelle | Realistischer Weg |
|---|---|---|---|
| **Treatwell** (DE, AT, CH) | Marktplatz, größter Kanal für Beauty/Spa | Keine öffentliche API. Es gibt „integrierte Software-Anbieter“ mit Partnervertrag (Treatwell-Partnerseite listet sie). | Phase 1: E-Mail-Parsing + ICS-Feed (so macht es z. B. anny). Phase 2: Partnervertrag für Echtzeit-API anfragen. |
| **Reserve with Google** (Actions Center) | Buchen direkt aus Google Suche/Maps | Ja: Feeds (Merchant, Services, Availability) + Booking Server API (BatchAvailabilityLookup, CreateBooking, UpdateBooking, GetBookingStatus) + Echtzeit-Updates | Als Software-Partner bewerben (Interessenformular). Google rechnet mit 12–16 Wochen Integration und Sandbox-Abnahme. Ob Wellness/Massage in DACH freigeschaltet ist, muss man mit Google klären. |
| **Booksy, Fresha, Planity** | Marktplätze mit eigener Salonsoftware | Keine offene API für Fremdsysteme gefunden; sie wollen selbst das Kassensystem sein | Nur über ICS/E-Mail oder Partnergespräch; niedrige Priorität |
| **Jochen Schweizer / mydays** (eine Gruppe), Smartbox, Wellnessbox (CH) | Erlebnis-Gutscheine | Keine öffentliche API; Partner lösen über Partnerportal ein | Gutschein-Code beim Buchen erfassen, Einlösung im Partnerportal bestätigen (später ggf. Portal-Automatisierung bzw. Partner-API anfragen). Wichtig für Kasse: als fremder Gutschein/Forderung verbuchen, nicht als Umsatz bei Verkauf. |
| **ClassPass, Urban Sports Club, Wellpass** | Mitgliedschafts-Plattformen mit Spa-/Massage-Angeboten | Teils Partner-APIs für Check-in/Kontingente | Später, wenn Kunden es nachfragen |
| **Hotel-Channel-Manager** (für Spas mit Hotelpartnern) | Hotelgäste buchen Behandlungen | Je nach Anbieter | Optional, nicht Kernzielgruppe |

**Empfehlung zur Reihenfolge:** 1. Treatwell (E-Mail + ICS), 2. Reserve with Google (offizielle API, großer Hebel, Bewerbung früh starten weil lange Vorlaufzeit), 3. Gutscheinportale, 4. Rest nach Nachfrage.

## 2. Architektur: ein Kanal-Adapter pro Plattform

```
Plattform ──► Adapter (Inbound) ──► Queue ──► Buchungsdienst (einzige Wahrheit)
                                                   │
Plattform ◄── Adapter (Outbound) ◄── Queue ◄── Verfügbarkeits-Events
```

- Ein gemeinsames Interface `IChannelAdapter` (Inbound: Buchung anlegen/ändern/stornieren; Outbound: Verfügbarkeit, Leistungen, Preise). Neue Plattformen = neuer Adapter, Kern bleibt unberührt.
- Adapter laufen als eigene Worker-Dienste in ASP.NET Core 10 hinter der Message-Queue. Fällt eine Plattform aus, staut sich nur ihre Queue.
- Adapter-Typen, die wir brauchen: **API** (Google), **E-Mail-Parser** (Treatwell, Gutscheine; KI-gestützt, passt zur geplanten KI-Mail-Terminannahme), **ICS-Feed** (ausgehend als Notlösung), **Portal-Automatisierung** (letzter Ausweg).
- Mapping-Tabellen pro Standort: Plattform-Leistung ↔ eigene Behandlung, Plattform-Mitarbeiter ↔ Therapeut. Die Plattform kennt meist keinen Raum und kein Gerät, die ergänzt unser Kalender automatisch.

## 3. Keine Doppelbuchungen: die Regeln

1. **Eine Wahrheit.** Nur unser Buchungsdienst entscheidet, ob ein Slot frei ist. Plattformen bekommen Verfügbarkeit nur als Kopie.
2. **Ressourcen-Sperre statt Zeitfenster.** Ein Termin belegt Therapeut + Raum + Gerät gleichzeitig in einer Transaktion (DB-Constraint bzw. Exclusion-Lock auf Ressource+Zeitraum). Zwei gleichzeitige Anfragen: eine gewinnt, die andere wird abgelehnt.
3. **Kurzzeit-Reservierung (Hold).** Echtzeit-Kanäle wie Google fragen erst Verfügbarkeit ab und buchen dann. Wir setzen beim Abfragen nichts, aber bei `CreateBooking` erst einen Hold (z. B. 2 Min.), dann Bestätigung. Idempotenz-Schlüssel der Plattform verhindert Doppelanlage bei Wiederholungen.
4. **Puffer pro Kanal.** Für Kanäle ohne Echtzeit (Treatwell per Mail/ICS, 30–60 Sek. Verzug) nur einen Teil der Kapazität freigeben oder kurz vor Termin Slots zumachen („Kontingent“ und „Buchungsschluss“ pro Kanal).
5. **Sofortiges Zurückmelden.** Jede Änderung im eigenen Kalender erzeugt ein Event; die Outbound-Adapter melden Belegung sofort an alle Kanäle (Push wo möglich, sonst ICS mit kurzer Cache-Zeit).
6. **Konfliktfall automatisch lösen.** Kommt trotzdem eine überschneidende Buchung (Mail-Kanal), schlägt der Kalender automatisch Alternativen vor: anderer Therapeut gleicher Qualifikation, anderer Raum, anderer Standort. Erst wenn das nicht geht, Aufgabe an die Rezeption + KI-Textvorschlag an den Gast.
7. **Abgleich nachts.** Ein Reconciliation-Job vergleicht alle Kanal-Buchungen mit unseren und meldet Abweichungen.

## 4. Nächste Schritte

- Bei Treatwell nach dem Partnerprogramm für Software-Anbieter fragen (Echtzeit statt Mail-Parsing).
- Bei Google Actions Center als Partner registrieren und klären, ob Spa/Massage in DE/AT/CH zugelassen ist.
- Jochen Schweizer/mydays: Partnerbedingungen und Einlöseweg für Wellness-Partner anfragen.
- Im Datenmodell von Anfang an `Channel`, `ExternalBookingId`, `IdempotencyKey` und Kanal-Kontingente vorsehen.

## Quellen

- Treatwell Partnerseite (integrierte Software-Anbieter): https://www.treatwell.co.uk/partners?p=9657
- Beispiel Treatwell-Anbindung per E-Mail + ICS (anny): https://docs.anny.co/en/treatwell.md
- Google Actions Center, Integrationsübersicht: https://developers.google.com/actions-center/verticals/local-services/e2e/integration-steps/overview?hl=en
- Google Actions Center, Plattformrichtlinien: https://developers.google.com/actions-center/verticals/reservations/e2e/policies/platform-policies
- Jochen Schweizer Gutschein-Einlösung (Beispiel Partnersicht): https://intercom.help/spassimteam/de/articles/8864241-wie-lose-ich-einen-jochen-schweizer-gutschein-ein

Hinweis: Dass Booksy, Fresha, Planity und die Gutscheinportale keine öffentliche API haben, ist aus der Recherche abgeleitet (nichts gefunden), nicht offiziell bestätigt.
