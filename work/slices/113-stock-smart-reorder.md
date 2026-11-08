# Slice 113 – Lager: Bestellvorschlag nach Verbrauch, Höchstmenge und Lieferzeit

**Ziel:** Der Bestellvorschlag rechnet aus dem Tagesverbrauch der letzten 90 Tage, den gebuchten Terminen, der Lieferzeit und einer Höchstmenge je Artikel; die Leitung gibt Bestellungen frei und schickt sie mit einem Knopf per Mail an den Lieferanten.
**Runde:** R05
**Quelle:** R05 – Ä251
**Hängt ab von:** 112
**Modell:** opus
**IDs für neue Befunde:** Ä262–Ä264

## Umfang
- Höchstmenge bzw. Bestellziel je Artikel und Lagerort
- Rechnung in `PurchaseOrders`: Bedarf bis Lieferung = Tagesverbrauch × Lieferzeit + gebuchte Termine aus `StockDemand`; aufgerundet auf Packungseinheiten, Mindestbestellwert geprüft; nur in der API, nie in der KI
- Bestellung per Mail an den Lieferanten über die Wolverine-Queue, nur nach Freigabe in der Maske (Knopf „Bestellen und senden“), Vollmacht „Bestellungen auslösen“ aus Slice 111, wenn vorhanden
- Hilfe nachziehen

## Muster übernehmen
- native Lager-Maske aus Slice 101
- docs/konzepte/lager-konkurrenz.md – Ist-Stand, Konkurrenz, Empfehlungen
- Zenoti: Nachbestellung aus 90-Tage-Verbrauch und geschätzter Lieferzeit, Entwürfe zur Freigabe

## Voraussichtlich geänderte Dateien
- src/AispaStudio.Api/Stock/
- src/AispaStudio.Desktop/Views/Admin/Stock*
- tests/AispaStudio.Api.Tests/
- tests/AispaStudio.Desktop.Tests/
- web/src/help/content/

## NICHT bauen
- automatisches Senden ohne Freigabe
- Schnittstellen zu Lieferantenshops

## Prompt
```
Lies CLAUDE.md, work/conventions.md, die Rundenakte work/rounds/R05-*.md und diese Slice-Datei.
Setze Slice 113 "Lager: Bestellvorschlag nach Verbrauch, Höchstmenge und Lieferzeit" um (diese Datei).
1. Stelle zuerst einen Plan vor (Dateien, Klassen, Tests) mit einem Abschnitt
   „Annahmen (bei Freigabe bestätigen)“ für jeden offenen Punkt und einem kurzen „Review-Fokus“,
   speichere ihn unter work/plans/ und warte auf Freigabe.
2. Schreibe die Abnahmetests dieser Slice als fehlschlagende Tests (ROT).
3. Implementiere das Minimum bis grün. Halte jede Regel in CLAUDE.md und work/conventions.md ein.
4. Prüfe die Definition of Done. Halte Entscheidungen, Überraschungen und zurückgestellte
   Kleinigkeiten in der Rundenakte und in der Protokolldatei fest, die work/conventions.md nennt.
```

## Definition of Done
- [ ] Prüfungen aus work/conventions.md grün
- [ ] Kein Test abgeschwächt oder übersprungen
- [ ] Keine neuen Pakete ohne Freigabe
- [ ] Entscheidungen festgehalten
- [ ] Given 30 ml Tagesverbrauch, Lieferzeit 5 Tage, Bestand 100 ml und gebuchte Termine mit 90 ml When der Vorschlag gerechnet wird Then schlägt er so viele Flaschen vor, dass nach der Lieferung die Höchstmenge erreicht ist
- [ ] Given ein Vorschlag unter dem Mindestbestellwert When die Leitung bestellen will Then warnt die Maske mit dem fehlenden Betrag
- [ ] Given „Bestellen und senden“ When die Leitung bestätigt Then geht die Mail über die Queue und die Bestellung steht auf „bestellt“
- [ ] Manuell: Bestellung freigeben und in Mailpit prüfen

## Nicht in dieser Slice
–
