# Slice 09 – Feldnamen und Knopfverben in der Kassa

**Ziel:** Alle Felder der Kassa sind beschriftet, und die Verkaufsknöpfe tragen ein Verb.
**Runde:** R01
**Quelle:** R01 – W45, W49 (Verben), W-Muster 1 (Teil), W-Muster 13 (Teil)
**Hängt ab von:** 05
**Modell:** –

## Umfang
- 14 Felder in FormField: Rechnungsempfänger (:220-223, :513-516), Gutschein-Code :436, PIN :359, Grund Storno :538, Notiz Abschluss :627, Grund Einlage :644 (W45)
- Knöpfe :144, :157, :171 als „Wertgutschein verkaufen“, „Behandlung verkaufen“, „Paket verkaufen“ (W49, W-Muster 13)

## Muster übernehmen
- src/AispaStudio.Desktop/Views/CustomerContactForm.axaml:25 – FormField im Einsatz

## NICHT bauen
- Höhen 46 und 42 der Kassa-Knöpfe (offene Entscheidung zu W-Muster 9)
- Reiterschienen der Kassa (Slice 19)

## Prompt
```
Lies CLAUDE.md, work/conventions.md, die Rundenakte work/rounds/R01-*.md und diese Slice-Datei.
Setze Slice 09 "Feldnamen und Knopfverben in der Kassa" um (diese Datei).
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
- [ ] Given die Kassa in den Zuständen Kassieren, Rechnungsempfänger, Storno, Abschluss und Einlage When der Namens-Scan läuft Then hat jedes Feld einen Namen, der nicht aus dem Platzhalter stammt
- [ ] Given „Kassieren“ When die Knöpfe gerendert werden Then heißen sie „Wertgutschein verkaufen“, „Behandlung verkaufen“ und „Paket verkaufen“
- [ ] Manuell: Kassa › Kassieren: die Verkaufsknöpfe tragen ein Verb; Rechnungsempfänger öffnen: Beschriftungen über den Feldern

## Nicht in dieser Slice
Ablauf des Kassierens
