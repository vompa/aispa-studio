# Slice 132 – Kassa: Drucken direkt in der Ansicht „Bezahlt“

**Ziel:** Nach dem Kassieren lässt sich der Beleg in der Ansicht „Bezahlt“ drucken, ohne in „Belege“ zu wechseln; ohne Bondrucker sagt die Ansicht, warum es nicht geht.
**Runde:** R06
**Quelle:** R06 – Ä314
**Hängt ab von:** 131
**Modell:** sonnet
**Prüfstufe:** –
**IDs für neue Befunde:** Ä329–Ä331

## Umfang
- Der Knopf „Nochmals drucken“ in der Ansicht „Bezahlt“ ist heute ohne eingerichteten Bondrucker unsichtbar (`IsVisible` an `HasPrinter`) → immer sichtbar als „Drucken“, ohne Drucker gesperrt mit dem Hinweis „Kein Bondrucker eingerichtet (unter „Geräte“).“ wie in der Belegansicht
- Nach „Empfänger erfassen“ druckt der Knopf die Rechnung mit Empfänger
- Druckstand (`PrintText`) wie bisher als Live-Region
- Handbuch „Bezahlen“ nachziehen

## Muster übernehmen
- src/AispaStudio.Desktop/Views/PosView.axaml – Druckknopf der Belegansicht (`IsEnabled` an `HasPrinter`, Hinweis daneben)

## Voraussichtlich geänderte Dateien
- src/AispaStudio.Desktop/Views/PosView.axaml
- tests/AispaStudio.Desktop.Tests/
- web/src/help/content/

## NICHT bauen
- Druck auf A4 oder als PDF über den Windows-Drucker (eigener Befund, falls gewünscht)
- automatischer Druck nach dem Bezahlen

## Prompt
```
Lies CLAUDE.md, work/conventions.md, die Rundenakte work/rounds/R06-*.md und diese Slice-Datei.
Setze Slice 132 "Kassa: Drucken direkt in der Ansicht „Bezahlt“" um (diese Datei).
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
- [ ] Given ein Bondrucker ist eingerichtet When ein Beleg bezahlt ist Then druckt „Drucken“ in der Ansicht „Bezahlt“ den Beleg
- [ ] Given kein Bondrucker ist eingerichtet When ein Beleg bezahlt ist Then ist „Drucken“ sichtbar, gesperrt und daneben steht der Hinweis auf „Geräte“
- [ ] Given der Empfänger ist erfasst When man „Drucken“ wählt Then wird die Rechnung mit Empfänger gedruckt
- [ ] Manuell: Mit und ohne eingerichteten Bondrucker kassieren und in der Ansicht „Bezahlt“ drucken

## Nicht in dieser Slice
–
