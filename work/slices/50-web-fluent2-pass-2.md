# Slice 50 – fluent2-web über Verwaltung, Personal und Stammdaten

**Ziel:** Die Masken Stammdaten, Personal, Betriebsinformationen, Aktionen, Schließzeiten, Kosten, Abos, Anamnese und Einladung halten die Regeln von fluent2-web ein; Reste sind als Befunde festgehalten.
**Runde:** R02
**Quelle:** R02 – Auftraggeber, 25.10.2026 (fluent2-web über alle Web-Masken)
**Hängt ab von:** 40, 48
**Modell:** sonnet

## Umfang
- Je Maske die Prüflisten des Skills `fluent2-web` durchgehen (Felder, Knöpfe, Dialoge, Tastatur, Fokus, Zustände, ARIA, Texte); was in die Sitzung passt, beheben, den Rest als Befund mit Kennung ab B71 in der Rundenakte unter „Überraschungen“ festhalten (fortlaufend über die vier Durchgänge)
- Masken: `admin/`, `businessinfo/`, `promotions/`, `closures/`, `costs/`, `subscriptions/`, `anamnesis/`, `auth/`

## Muster übernehmen
–

## NICHT bauen
- Neue Funktionen; Befunde, die eine Sitzung sprengen (werden Befund)

## Prompt
```
Lies CLAUDE.md, work/conventions.md, die Rundenakte work/rounds/R02-*.md und diese Slice-Datei.
Setze Slice 50 "fluent2-web über Verwaltung, Personal und Stammdaten" um (diese Datei).
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
- [ ] Given jede Maske der Gruppe When die Prüflisten von `fluent2-web` durchlaufen sind Then ist jeder Befund entweder behoben oder mit Kennung in der Rundenakte
- [ ] Given die geänderten Masken When die vorhandenen Tests samt axe-Prüfung laufen Then gibt es keinen neuen Verstoß
- [ ] Manuell: Stammdaten und Personal nur mit Tastatur durchgehen: Formulare, Reiter und Listen sind bedienbar

## Nicht in dieser Slice
–
