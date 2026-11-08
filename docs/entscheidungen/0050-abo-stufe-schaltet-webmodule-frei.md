# 0050. Abo-Stufe je Spa schaltet Webmodule frei, nie den Kern

- **Status:** angenommen
- **Datum:** 19.10.2026
- **Entschieden von:** Auftraggeber (Wahl „alle“ offenen Einzelplatz-Punkte), Ausgestaltung Claude (autonom nach ADR 0019)

## Kontext

[ADR 0015](0015-lizenzmodell-abo-pro-standort.md) legt drei Abo-Stufen fest (Start, Spa, Spa Plus) und sagt: „Funktionen schaltet eine Berechtigung aus dem Vertrag frei, nicht ein Programmstand. Die Kasse wird nie gesperrt.“ [ADR 0041](0041-einzelplatz-standalone-und-zuschaltbare-webmodule.md) hat die Schalter `Modules:<Name>` je Installation gebaut und angekündigt, dass das Freischalten je Spa darauf aufbaut. Bisher hatte jedes Spa auf einem Server alle Module.

## Optionen

| Option | Bewertung |
| --- | --- |
| Stufen schalten auch Kernfunktionen (Kosten, Berichte, Lager) | widerspricht „Kasse nie sperren“ und dem Kern aus ADR 0041; jede Kernfunktion bräuchte zwei Wege; scheidet aus |
| Freie Modulliste je Spa statt Stufen | flexibel, aber Vertrag und Software laufen auseinander; erst sinnvoll mit einem Vertragssystem |
| Eine Stufe je Spa; jede Stufe schaltet eine feste Menge Webmodule frei, höhere Stufen enthalten die niedrigeren | gewählt: entspricht dem Preisblatt, eine Zahl im Datensatz, prüfbar |

## Entscheidung

- Jedes Spa hat eine Abo-Stufe (`Tenant.Plan`): `Start`, `Spa` oder `SpaPlus`. Der Betreiber setzt sie auf der Betreiber-Seite oder mit `PUT /api/operator/spas/{id}/plan`.
- Zuordnung (`Plans.From`, im Code):
  - Start: Online-Buchung, Erinnerungen
  - Spa: dazu Assistent, Wiederbuchung
  - Spa Plus: dazu Terminannahme (alle Kanäle), Benchmark
- Kalender, Kassa, Stammdaten, Dienstplan, Kosten, Berichte, Exporte und alles andere aus dem Kern gehören zu jeder Stufe.
- Ein Modul ist für ein Spa an, wenn die Installation es einschaltet (`Modules:<Name>`, ADR 0041) **und** die Stufe es enthält. Fehlt es in der Installation, antwortet es weiter mit 404; fehlt es nur in der Stufe, mit 403 „Nicht in der Abo-Stufe.“ und der nötigen Stufe. `GET /api/system/modules` meldet den Stand für das angemeldete Spa samt Stufe, die Oberflächen blenden danach aus wie bisher.
- Hintergrundläufe (Erinnerungen, Wiederbuchung, Postfach-Abruf) überspringen Spas ohne das Modul. Eingehende Nachrichten an ein Spa ohne Terminannahme werden gespeichert, aber nicht von der KI bearbeitet; nach einem Upgrade sind sie in der Inbox.
- Vorgabe ist Spa Plus, für bestehende und neue Spas: Funktionen werden niemandem weggenommen, und der Einzelplatz-Betrieb steuert weiter nur über die Schalter der Installation.
- Ein Wechsel der Stufe löscht keine Daten und wirkt sofort, bei mehreren API-Instanzen nach spätestens 30 Sekunden (Zwischenspeicher der Sperre, ADR 0039).

## Begründung

Gesperrt werden nur Webmodule, also Funktionen mit laufenden Kosten (KI, SMS, Hosting), die das Preisblatt auch nach Stufen trennt. Der Kern bleibt gleich, damit kein Spa wegen einer Stufe seine Kasse, Daten oder Pflichtberichte verliert. 403 statt 404 sagt dem Team, dass es das Modul gibt und welche Stufe es braucht.

## Folgen und Stand

- Umgesetzt mit PR #121: `Plans`, `Tenant.Plan` (Migration `AboStufe`), Prüfung in `RequireModule`, Stand in `GET /api/system/modules`, Auswahl auf der Betreiber-Seite.
- Die Zuordnung steht im Code; ändert sich das Preisblatt, ändert sich `Plans.From` mit einem PR.
- Offen: Telefon-KI als eigener Zusatz (heute Teil der Terminannahme in Spa Plus); Abo-Stufe aus einem Vertragssystem statt von Hand; Stufe je Standort statt je Spa (ADR 0015 rechnet je Standort ab, alle Standorte eines Spas haben heute dieselbe Stufe); signierte Lizenzdatei für den Einzelplatz.

## Quellen

- [ADR 0015](0015-lizenzmodell-abo-pro-standort.md), [ADR 0041](0041-einzelplatz-standalone-und-zuschaltbare-webmodule.md), [ADR 0039](0039-spa-sperren.md)
- PR #121
