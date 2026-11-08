# Slice 101 – Native Verwaltung: Lager und Aktionen

**Ziel:** Lager mit Lagerorten und Bewegungen und Aktionen sind nativ bearbeitbar; die Lager-Maske ist die Grundlage der Lager-Slices 112–116.
**Runde:** R05
**Quelle:** R05 – Ä191
**Hängt ab von:** 93
**Modell:** sonnet
**IDs für neue Befunde:** Ä223–Ä225

## Umfang
- Lager (web/src/stock/) nativ: Artikel, Lagerorte, Zu- und Abgänge, Inventur
- Aktionen (web/src/promotions/) nativ
- Sprungziele Kalender (Hinweise zum Lager) und Assistent „Jetzt ergänzen“ (`promotions`) auf die native Maske
- Hilfe nachziehen

## Muster übernehmen
- Verwaltungsgerüst und Firmendaten aus Slice 93
- web/src/stock/

## Voraussichtlich geänderte Dateien
- src/AispaStudio.Desktop/Views/Admin/Stock*
- src/AispaStudio.Desktop/Views/Admin/Promotions*
- tests/AispaStudio.Desktop.Tests/
- web/src/help/content/

## NICHT bauen
- neue Lagerlogik (eigene Lager-Slices dieser Runde)

## Prompt
```
Lies CLAUDE.md, work/conventions.md, die Rundenakte work/rounds/R05-*.md und diese Slice-Datei.
Setze Slice 101 "Native Verwaltung: Lager und Aktionen" um (diese Datei).
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
- [ ] Given 10 Flaschen Massageöl When die Kassa 2 verkauft Then zeigt das native Lager 8
- [ ] Given eine Aktion „Herbst -10 %“ When sie nativ angelegt wird Then rechnet die Kassa den Rabatt
- [ ] Manuell: Inventur für einen Lagerort durchführen und Abweichung buchen

## Nicht in dieser Slice
–
