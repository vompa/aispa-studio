# Slice 112 – Lager: Lieferantenstamm, Packungseinheiten und Ablaufwarnung

**Ziel:** Lieferanten werden ein eigener Stamm mit Kontakt, Lieferzeit, Mindestbestellwert und Bestellweg; Artikel kennen Packungseinheiten (Karton à 4, Flasche 500 ml, 15 ml je Behandlung); Chargen, die bald ablaufen, erscheinen als Aufgabe mit Abverkaufsliste.
**Runde:** R05
**Quelle:** R05 – Ä251
**Hängt ab von:** 101
**Modell:** opus
**IDs für neue Befunde:** Ä259–Ä261

## Umfang
- Entity Lieferant mit Kontakt, Mail, Lieferzeit in Tagen, Mindestbestellwert; Migration der Textfelder `Supplier`/`SupplierArticleNumber` auf den Stamm (bestehende Texte werden zu Lieferanten)
- Packungseinheit und Umrechnung am Artikel; Bewegungen bleiben in der Basiseinheit
- Ablaufwarnung aus `ExpiresOn` der Chargen (Frist je Artikel, Vorgabe 30 Tage) als Aufgabe in der Windows-App; vorher prüfen, ob es sie schon gibt
- Beispieldaten (Bulk und Services gleich, `DemoBulkTests`), Hilfe nachziehen

## Muster übernehmen
- native Lager-Maske aus Slice 101
- docs/konzepte/lager-konkurrenz.md – Ist-Stand, Konkurrenz, Empfehlungen
- src/AispaStudio.Api/Stock/Stock.cs
- src/AispaStudio.Api/Stock/PurchaseOrders.cs

## Voraussichtlich geänderte Dateien
- src/AispaStudio.Api/Stock/
- src/AispaStudio.Desktop/Views/Admin/Stock*
- tests/AispaStudio.Api.Tests/
- tests/AispaStudio.Desktop.Tests/
- web/src/help/content/

## NICHT bauen
- Bestellung per Mail (Slice 113)
- Barcode (Slice 114)

## Prompt
```
Lies CLAUDE.md, work/conventions.md, die Rundenakte work/rounds/R05-*.md und diese Slice-Datei.
Setze Slice 112 "Lager: Lieferantenstamm, Packungseinheiten und Ablaufwarnung" um (diese Datei).
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
- [ ] Given ein Artikel mit Lieferant „Haslauer“ als Text When die Migration läuft Then gibt es einen Lieferanten „Haslauer“ und der Artikel zeigt auf ihn
- [ ] Given Massageöl in Flaschen à 500 ml und 15 ml je Behandlung When ein Karton à 4 eingeht Then steigt der Bestand um 2000 ml
- [ ] Given eine Charge läuft in 20 Tagen ab When die Leitung die Aufgaben öffnet Then steht sie mit Menge und Lagerort in „Bald ablaufend“
- [ ] Manuell: Lieferant anlegen, Artikel zuordnen, Wareneingang in Kartons buchen

## Nicht in dieser Slice
–
