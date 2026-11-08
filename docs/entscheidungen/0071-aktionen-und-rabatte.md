# 0071. Aktionen und Rabatte als eigener Fachbereich

- **Status:** angenommen
- **Datum:** 22.10.2026
- **Entschieden von:** Auftraggeber (Runde 5, Ä36: Wunsch für Assistent und Kassa), Ausgestaltung Claude

## Kontext

Der Auftraggeber will, dass der Assistent Fragen wie „Welche Rabatte oder Aktionen laufen gerade?“ beantworten kann und dass die Kassa die laufende Aktion beim Kassieren vorschlägt. Dafür muss es die Aktionen als Daten geben. Heute kennt das Spa nur drei Rabatte: den von Hand gegebenen Rabatt je Position, den dauerhaften Kundenrabatt aus der Kundenkartei und den Barzahlungsrabatt; alle drei wirken an der Kassa und stehen auf dem Beleg ([ADR 0010](0010-kassa-compliance-by-design.md): jeder Umsatz läuft über Beleg und Signatur). Eine zeitlich begrenzte Aktion („Herbst: 10 % auf alle Massagen bis Ende Oktober“) gibt es nur im Kopf der Rezeption.

## Optionen

| Option | Bewertung |
| --- | --- |
| Freitext „Aktuelle Aktionen“ in den Einstellungen | schnell, aber für Assistent und Kassa nicht auswertbar: kein Zeitraum, keine Behandlung, kein Rabatt, nichts, was die Kassa vorschlagen könnte |
| Rabattcodes, die der Gast nennt | andere Aufgabe (Gutscheinähnlich, Missbrauch, Verteilung der Codes); der Auftraggeber wollte, dass die Kassa die passende Aktion selbst findet |
| Aktion als Preisregel an der Behandlung (`TreatmentPriceRule`) | die Regel ändert den Listenpreis des Termins schon beim Buchen und macht den Rabatt unsichtbar; Beleg und Kassa würden die Aktion nicht nennen; Pakete und Standort-Auswahl passen nicht |
| Eigenes Entity `Promotion` im Kern, der Rabatt bleibt ein Kassa-Rabatt | gewählt |

## Entscheidung

- **Eigenes Entity im Kern** (Ordner `Promotions`, mandantengetrennt, `ValueGeneratedNever` auf den Ids): `Promotion` mit Name (bis 40 Zeichen, steht auf dem Beleg), Gästetext (bis 200 Zeichen, für Assistent und Gäste), Zeitraum von/bis als Kalendertage am Standort (beide einschließlich), Standorte (alle oder Auswahl, `PromotionLocation`), Rabatt als **Prozent oder Betrag** (genau eines, auch per Prüfbedingung in der Datenbank), Gültigkeit (`PromotionScope`: alle Behandlungen, ausgewählte Behandlungen, Pakete ausgewählter Behandlungen; `PromotionTreatment`), Zustand aktiv/pausiert und `ArchivedAt`. Der Zustand am heutigen Tag (laufend, geplant, pausiert, abgelaufen) wird nie gespeichert, sondern aus Zeitraum und Schalter gerechnet (`PromotionRules.PhaseOf`).
- **Verlauf bleibt.** Aktionen werden nie gelöscht, nur archiviert; archivierte laufen nie mehr und stehen nicht mehr in der Liste, lassen sich aber zurückholen. Bereits gegebene Rabatte auf Belegen ändern sich bei keiner Änderung der Aktion.
- **Der Rabatt bleibt ein Kassa-Rabatt.** Die Aktion ändert nur den Rabatt der Position (`BillLine.Discount`, `DiscountNote`, `Amount`); den Rabatt rechnet ausschließlich die Kassa (`PosService.ApplyPromotionAsync`, Rechenregeln in `PromotionRules`, gemeinsam mit dem Verlauf der Beispieldaten). Es gibt keinen neuen Weg an Beleg, Steuer und Signatur vorbei: der Beleg trägt den Rabatt wie jeden anderen (Zeile „Aktion Herbst 10 %“, Summe, Steuer und Signatur aus dem Betrag nach Rabatt). Betrags-Aktionen nennen auf dem Beleg wie jeder Rabatt den Prozentsatz („Aktion Fünfer 6,3 %“).
- **Nur auf Klick.** Die Kassa schlägt vor (`GET /api/pos/bills/{id}/promotions`, ändert nichts) und setzt den Rabatt erst mit **Anwenden** (`POST /api/pos/bills/{id}/promotions/{promotionId}/apply`). Nichts wird automatisch angewendet. Der Vorschlag erscheint nur, wenn die Aktion am heutigen Tag des Standorts der Kasse läuft (aktiv, nicht archiviert, im Zeitraum, am Standort gültig) und mindestens eine offene Position passt.
- **Rabatte stapeln sich nicht.** Eine Position mit Rabatt (Kundenrabatt, von Hand, andere Aktion) bekommt keine Aktion; auch bezahlte Positionen nicht. Positionen, bei denen der Betrag nicht kleiner als der Preis wäre, werden nicht vorgeschlagen.
- **Keine Freigabe über der Rabattgrenze.** Die Rabattgrenze des Spas (Standard 20 %) schützt vor Rabatten, die jede Person nach Belieben gibt; eine Aktion legen Inhaber oder Standortleitung bewusst fest und sie gilt für alle Gäste gleich. Die Aktion braucht deshalb keine PIN-Freigabe an der Kassa.
- **„Pakete“ heißt: der Verkauf eines Pakets (Mehrfachkarte) der gewählten Behandlungen.** Es gibt keinen Katalog von Paketen; ein Paket entsteht an der Kassa als Einzweckgutschein mit Anzahl. Die Aktion senkt den Preis dieser Position; der Gutschein trägt den bezahlten Wert (Wert des Pakets = Betrag der Position, wie bei jedem frei gesetzten Paketpreis). Auf Wertgutscheine, Anzahlungen, Artikel und Eintritte wirkt keine Aktion.
- **Rechte:** Verwalten braucht `ManageCatalog` (Inhaber und Standortleitung). Die Standortleitung sieht und ändert nur Aktionen, die ganz in ihren Standorten liegen, und legt keine für „alle Standorte“ an. Die laufenden Aktionen eines Standorts liest jeder Zugang mit `ViewCalendar` (`GET /api/promotions/active`), weil Kassa und Assistent sie brauchen.
- **Schnittstelle für den Assistenten:** `PromotionService.GetActiveAsync(locationId, date, ct)` liefert die laufenden Aktionen mit Gästetext, Rabatt (`DiscountText`, Prozent oder Betrag) und Gültigkeit (Behandlungen mit Namen). Ein späterer Block (Assistent, Ä36 K2) beantwortet damit „Welche Aktionen laufen gerade?“; `TodayAsync(locationId)` liefert „heute“ am Standort.
- **Oberflächen:** Verwaltung (Web, in der Windows-App eingebettet): Seite „Aktionen“ mit Liste nach Zustand und Maske; Kassa der Windows-App: Hinweis mit Knopf. Der Kundenbereich bleibt unberührt.

## Begründung

Ein eigenes Entity ist die einzige Form, die Kassa und Assistent auswerten können, und lässt Zeitraum, Standort und Gültigkeit prüfen. Dass der Rabatt ein gewöhnlicher Kassa-Rabatt bleibt, hält [ADR 0010](0010-kassa-compliance-by-design.md): Es entsteht kein zweiter Weg zu einem Umsatz, die Signatur sieht nur den Betrag nach Rabatt wie bisher. Der Klick der Rezeption hält die Verantwortung bei der Person an der Kassa und vermeidet Überraschungen („die Kassa hat von selbst rabattiert“).

## Folgen und Stand

- Neue Tabellen `Promotions`, `PromotionLocations`, `PromotionTreatments` (Migration `Aktionen`), Endpunkte unter `/api/promotions`, zwei Endpunkte an der Kassa, Seite „Aktionen“ in der Verwaltung, Hinweis in der Kassa der Windows-App, auf dem Bildschirmbeleg Zeilen mit Preis davor und Rabatt darunter wie auf dem gedruckten Beleg.
- Beispieldaten: „Herbstzauber“ (läuft) und „Sommerfrische“ (seit drei Tagen vorbei, nur Wien); die Kassa der Beispieldaten wendet sie an den Tagen ihrer Laufzeit an.
- Offen: die Antwort des Assistenten (K2); Auswertung der Wirkung einer Aktion (Umsatz mit Aktion) in den Berichten; Aktionen in der Online-Buchung (der Kundenbereich zeigt noch keine); Aktion auf Artikel und Eintritte.

## Quellen

- [ADR 0010](0010-kassa-compliance-by-design.md), [ADR 0067](0067-masken-und-meldungen-nach-material-3-und-fluent-2.md), [ADR 0055](0055-hilfe-zu-jeder-einstellung-im-repository.md)
- [PR-Protokoll, Runde 5: Block K1 (Aktionen)](../umsetzung/pr-protokoll.md)
- Hilfe: `web/src/help/content/aktionen.md`, `aktionen-anlegen.md`, `kassa-aktion-anwenden.md`
