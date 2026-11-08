# Slice 32 – Maße der Windows-App auf das Raster des Briefs

**Ziel:** Kartenränder, Eingabehöhen, Abstände und Radien der Windows-App folgen dem Raster des Briefs; einzige Ausnahme ist die Höhe des Bezahlknopfs der Kassa.
**Runde:** R02
**Quelle:** R02 – W-Muster 9, W3, W23, W41, W59
**Hängt ab von:** –
**Modell:** sonnet

## Umfang
- Kartenrand 18 → 16/20 (W-Muster 9, 11 Dateien), Eingabehöhen 40–52 → 34 (W3), Abstände Feld/Gruppe/Überschrift → 12/20/8 (W41), Hinweise und InfoBar Radius 8, Innenabstand 10/12 (W59)
- Seitenleiste „KI und Hilfe“ und Kalender-Seitenleiste ohne Schatten und Rand ringsum wie `Border.sidepanel` (W23)
- Brief 4.4: Höhe des Bezahlknopfs der Kassa als einzige Ausnahme eintragen

## Muster übernehmen
–

## NICHT bauen
- Neue Gestaltung von Seiten

## Prompt
```
Lies CLAUDE.md, work/conventions.md, die Rundenakte work/rounds/R02-*.md und diese Slice-Datei.
Setze Slice 32 "Maße der Windows-App auf das Raster des Briefs" um (diese Datei).
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
- [ ] Given alle AXAML-Dateien unter src/AispaStudio.Desktop When ein Prüftest `Padding="18"` an Karten sucht Then gibt es keinen Treffer
- [ ] Given eine InfoBar When sie gerendert wird Then hat sie Radius 8
- [ ] Given Brief 4.4 When man ihn liest Then steht der Bezahlknopf der Kassa als Ausnahme mit seiner Höhe
- [ ] Manuell: Sicherung, Geräte, Kassa im hellen und dunklen Design ansehen: Karten wirken gleich weit eingerückt, nichts springt

## Nicht in dieser Slice
–
