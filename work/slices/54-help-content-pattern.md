# Slice 54 – Hilfe: Muster für Abläufe und Einstieg je Rolle

**Ziel:** Abläufe folgen einem festen Muster mit Bild je Schritt, „Was danach“ und „Wenn etwas schiefgeht“, und die Startseite führt je Rolle zu den ersten Schritten.
**Runde:** R02
**Quelle:** R02 – Auftraggeber, 25.10.2026 (Hilfe professioneller); Brief der Rundenakte
**Hängt ab von:** 53, 49, 50
**Modell:** sonnet

## Umfang
- Muster in `web/src/help/README.md`: Kurzsatz, Voraussetzungen, Schritte mit je einem Bild (Markierung des Zielelements im Bildtest), „Was danach“, „Wenn etwas schiefgeht“, verwandte Themen; Prüfregel in `textRules.test.ts`
- Die zehn meistgenutzten Abläufe von Kalender und Kassa auf das Muster umstellen, Bilder neu erzeugen (`scripts/help-images.sh`)
- Startseite `HelpIndex.tsx`: Einstieg je Rolle (Rezeption, Therapeut, Leitung, Inhaber) mit den ersten Schritten, dann Bereiche

## Muster übernehmen
–

## NICHT bauen
- Alle 189 Themen umstellen (Rest in der nächsten Runde)
- Videos und GIFs (offene Entscheidung)

## Prompt
```
Lies CLAUDE.md, work/conventions.md, die Rundenakte work/rounds/R02-*.md und diese Slice-Datei.
Setze Slice 54 "Hilfe: Muster für Abläufe und Einstieg je Rolle" um (diese Datei).
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
- [ ] Given die zehn umgestellten Abläufe When `textRules.test.ts` läuft Then hat jeder die Abschnitte des Musters und ein Bild je Schritt mit Alternativtext
- [ ] Given die Startseite der Hilfe When sie angezeigt wird Then gibt es je Rolle einen Einstieg mit mindestens drei Abläufen
- [ ] Given ein Bildtest eines Ablaufs When er rendert Then markiert er das Zielelement des Schritts
- [ ] Manuell: Hilfe › Startseite: als Rezeption „Erste Schritte“ öffnen, „Termin buchen“ folgen: jeder Schritt hat ein Bild mit Markierung

## Nicht in dieser Slice
–
