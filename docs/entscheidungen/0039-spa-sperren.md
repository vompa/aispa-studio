# 0039. Gesperrtes Spa: ansehen und exportieren ja, ändern nein

- **Status:** angenommen
- **Datum:** 18.10.2026
- **Entschieden von:** Claude (autonom nach ADR 0019)

## Kontext

Der Betreiber muss ein Spa sperren können, etwa wenn der Vertrag endet oder nicht bezahlt wird (offener Punkt seit PR #45). Ein Spa führt in AISPA aber seine Kassa, Buchhaltung und Kundendaten. Für Kassenbelege und Buchungsunterlagen gilt eine Aufbewahrungspflicht von sieben Jahren (AT § 132 BAO) bzw. zehn Jahren (DE § 147 AO). Das Spa muss sie auch nach dem Ende des Vertrags lesen und exportieren können, und Gäste haben Termine, die sie absagen dürfen.

## Optionen

| Option | Bewertung |
| --- | --- |
| A: Anmeldung des Teams ganz sperren | einfach, aber das Spa käme nicht mehr an Belege, DEP und DSFinV-K; Gäste könnten Termine nicht mehr absagen |
| B: nur lesen und exportieren, nichts ändern; online keine neuen Buchungen | gewählt |
| C: Spa löschen | nicht umkehrbar und mit der Aufbewahrungspflicht unvereinbar; bewusst nicht gebaut |

## Entscheidung

- Der Betreiber sperrt auf `/betreiber/` mit einem Grund, den das Team sieht (`POST /api/operator/spas/{id}/lock`), und entsperrt wieder (`DELETE …/lock`).
- Gesperrt weist die API jede ändernde Anfrage des Teams mit 403 und dem Grund ab. Lesen und alle Exporte sind GET und gehen weiter, Anmelden und Abmelden auch.
- Im Kundenbereich sind nur neue Online-Buchungen, Gutscheinbestellungen und Wartelisten-Einträge gesperrt. Gäste sehen und verwalten ihre Termine weiter, Zahlungs-Webhooks laufen weiter.
- Die Terminannahme sammelt eingehende Nachrichten weiter, damit keine Anfrage eines Gasts verloren geht. Erinnerungen an bestehende Termine gehen weiter.
- Die Oberfläche des Teams zeigt oben einen Hinweis mit dem Grund.

## Begründung

Eine Sperre soll das Arbeiten beenden, nicht den Zugriff auf Unterlagen, die das Spa von Gesetzes wegen aufbewahren und vorlegen muss. Die Grenze „GET ja, alles andere nein“ ist einfach zu prüfen und gilt für jeden künftigen Endpunkt ohne eigene Regel.

## Folgen und Stand

- Code: `Tenancy/TenantLocks.cs` (Middleware nach Anmeldung und Kundenbereich, Stand je Spa 30 Sekunden zwischengespeichert), Felder `LockedAt` und `LockReason` am Mandanten.
- Mit mehreren API-Instanzen greift die Sperre dort spätestens nach 30 Sekunden.
- Die Kassa kann gesperrt keine Belege mehr erstellen. Eine Außerbetriebnahme der Registrierkasse (Schlussbeleg, Meldung an FinanzOnline) bleibt Sache des Spas vor dem Vertragsende.
- Offen: Daten nach Ablauf der Aufbewahrungsfrist löschen; eigenes Betreiber-Konto statt Schlüssel.

## Quellen

- [ADR 0033](0033-nummern-vergibt-der-betreiber.md), [ADR 0010](0010-kassa-compliance-by-design.md)
- PR #62 im PR-Protokoll
