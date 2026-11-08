# Slice 13 – Preise in den Stammdaten als Geldfeld

**Ziel:** Preise in Behandlungen und Preisregeln werden wie an der Kassa eingegeben, ohne dass Pfeiltasten oder Mausrad Beträge ändern.
**Runde:** R01
**Quelle:** R01 – B7
**Hängt ab von:** 12
**Modell:** –

## Umfang
- admin/TreatmentsTab.tsx :258 und admin/PriceRules.tsx :108 auf das Geldfeld aus Slice 12
- Dauer-Felder (:254, :325) bleiben `type="number"`

## Muster übernehmen
- das Geldfeld aus Slice 12

## NICHT bauen
- Neue Preislogik

## Prompt
```
Lies CLAUDE.md, work/conventions.md, die Rundenakte work/rounds/R01-*.md und diese Slice-Datei.
Setze Slice 13 "Preise in den Stammdaten als Geldfeld" um (diese Datei).
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
- [ ] Given das Preisfeld einer Behandlung mit 89,90 When Pfeil hoch gedrückt oder das Mausrad gedreht wird Then bleibt der Betrag 89,90
- [ ] Given die Eingabe „89,9“ When gespeichert wird Then geht 89,90 an die API
- [ ] Given die Eingabe „89,9x“ When das Feld verlassen wird Then steht ein Fehlertext am Feld
- [ ] Manuell: Stammdaten › Behandlungen › Preis auf „89,9“ ändern und speichern: angezeigt wird 89,90 €

## Nicht in dieser Slice
Preise je Standort
