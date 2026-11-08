# Slice 42 – Seitenpanel und Dienstplan im Web

**Ziel:** Seitenpanels nehmen den Fokus, schließen mit Esc und geben den Fokus zurück; der Dienstplan ist per Tastatur bedienbar.
**Runde:** R02
**Quelle:** R02 – B48, B49, B51
**Hängt ab von:** –
**Modell:** sonnet

## Umfang
- Gemeinsame Komponente `SidePanel` (Fokus auf Überschrift, Esc, Fokus zurück) für Dienstplan-DayEditor und Zeiterfassung-DayPanel (B48)
- Wiederholte Steuerelemente mit Bezug benennen („Dienst 2 entfernen“) (B49)
- Scrollbereich des Dienstplans fokussierbar mit `role="region"` und Namen (B51)

## Muster übernehmen
–

## NICHT bauen
–

## Prompt
```
Lies CLAUDE.md, work/conventions.md, die Rundenakte work/rounds/R02-*.md und diese Slice-Datei.
Setze Slice 42 "Seitenpanel und Dienstplan im Web" um (diese Datei).
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
- [ ] Given der Dienstplan When ein Tag geöffnet und Esc gedrückt wird Then ist das Panel zu und der Fokus auf der Zelle
- [ ] Given ein Tag mit zwei Diensten When die Namen geprüft werden Then heißen die Knöpfe „Dienst 1 entfernen“ und „Dienst 2 entfernen“
- [ ] Given der Dienstplan When per Tab navigiert wird Then erreicht der Fokus den Scrollbereich „Dienstplan“
- [ ] Manuell: Dienstplan: Tag öffnen, Esc: Panel zu, Fokus wieder in der Zelle

## Nicht in dieser Slice
–
