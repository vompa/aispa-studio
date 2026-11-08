# Slice 33 – Tooltip-Inhalte und Sperrgrund auch per Tastatur

**Ziel:** Was heute nur im Tooltip steht (gekürzte Texte, Sperrgrund), ist auch mit der Tastatur und dem Screenreader erreichbar.
**Runde:** R02
**Quelle:** R02 – W-Muster 12, W40
**Hängt ab von:** –
**Modell:** sonnet

## Umfang
- Kunden: Sterne, Adresszeile, Anrede-Zeile mit sichtbarem Hilfetext oder per Tastatur erreichbarer Hover-Karte; „Adresse entfernen“ je Zeile mit Bezug im Namen (W40)
- Kalender: Kennzahlen im Kopf per Tastatur erreichbar; gesperrte Knöpfe nennen den Grund zusätzlich als Hilfetext (`ui:DisabledHint`)

## Muster übernehmen
- src/AispaStudio.Desktop/Controls/DisabledHint.cs – vorhandener Sperrgrund

## NICHT bauen
–

## Prompt
```
Lies CLAUDE.md, work/conventions.md, die Rundenakte work/rounds/R02-*.md und diese Slice-Datei.
Setze Slice 33 "Tooltip-Inhalte und Sperrgrund auch per Tastatur" um (diese Datei).
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
- [ ] Given die Kundenkarte mit zwei Adressen When der Namens-Scan läuft Then heißen die Knöpfe „Adresse 1 entfernen“ und „Adresse 2 entfernen“ (oder mit Straße)
- [ ] Given ein gesperrter Knopf mit DisabledHint When er den Tastaturfokus bekommt Then ist der Grund ohne Maus lesbar (Hilfetext oder Ansage)
- [ ] Manuell: Kundenkarte nur mit der Tastatur durchgehen: Sterne und Adresszeile sind erreichbar und werden vorgelesen

## Nicht in dieser Slice
–
