# Slice 123 – Lager: Bestellungen, Bedarfsprüfung und Chargen-Rückverfolgung nativ

**Ziel:** Was vom Lager nach den Slices 101, 112 und 113 nur im Web liegt, wird nativ: Bestellungen mit Wareneingang, Bedarfsprüfung und Chargen-Rückverfolgung.
**Runde:** R05
**Quelle:** R05 – Ä223
**Hängt ab von:** 113
**Modell:** opus
**IDs für neue Befunde:** Ä287–Ä289

## Umfang
- Zuerst den Stand nach Slice 113 feststellen (Rundenakte, native Lager-Maske) und nur die verbleibenden Teile bauen
- Bestellungen: Liste, Stand, Wareneingang, soweit Slice 113 sie nicht schon nativ zeigt (Web: `PurchaseOrders.tsx`)
- Bedarfsprüfung: Bedarf aus gebuchten Terminen gegen den Bestand (Web: `StockDemand.tsx`)
- Chargen-Rückverfolgung: von der Charge zu Bewegungen und Terminen (Web: `BatchTrace.tsx`)
- Masken-Inventar und Hilfe nachziehen

## Muster übernehmen
- native Lager-Maske aus den Slices 101, 112 und 113
- web/src – die drei genannten Web-Masken als fachliche Vorlage

## Voraussichtlich geänderte Dateien
- src/AispaStudio.Desktop/Views/Admin/Stock*
- src/AispaStudio.Desktop/ViewModels/Admin/Stock*
- docs/umsetzung/masken-inventar.md
- tests/AispaStudio.Desktop.Tests/
- web/src/help/content/

## NICHT bauen
- neue Lagerlogik über das hinaus, was die API schon kann
- Entfernen der Web-Masken (macht Slice 103)

## Prompt
```
Lies CLAUDE.md, work/conventions.md, die Rundenakte work/rounds/R05-*.md und diese Slice-Datei.
Setze Slice 123 "Lager: Bestellungen, Bedarfsprüfung und Chargen-Rückverfolgung nativ" um (diese Datei).
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
- [ ] Given eine bestellte Lieferung When der Wareneingang in der Windows-App gebucht wird Then steigt der Bestand und die Bestellung steht auf „geliefert“
- [ ] Given gebuchte Termine mit höherem Verbrauch als der Bestand When die Bedarfsprüfung geöffnet wird Then zeigt sie den Artikel mit der Fehlmenge
- [ ] Given eine Charge mit Verbrauch in einem Termin When die Rückverfolgung geöffnet wird Then zeigt sie Bewegungen und Termin dieser Charge
- [ ] Manuell: Bestellung, Bedarfsprüfung und Rückverfolgung als Inhaberin im echten Fenster durchgehen

## Nicht in dieser Slice
–
