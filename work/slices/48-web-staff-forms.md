# Slice 48 – Formulare in Personal, Buchhaltung und Lohn

**Ziel:** Anlegen, Einladen und Speichern sind echte Formulare mit Enter und sichtbarem Grund am gesperrten Knopf.
**Runde:** R02
**Quelle:** R02 – B36
**Hängt ab von:** 40
**Modell:** sonnet

## Umfang
- `<form onSubmit>` um InviteForm, AccountForm, MemberForm (StaffPage) sowie die Formulare in Buchhaltung und Lohn
- Gesperrter Knopf mit `aria-disabled` und `aria-describedby` auf einen Hilfetext

## Muster übernehmen
–

## NICHT bauen
–

## Prompt
```
Lies CLAUDE.md, work/conventions.md, die Rundenakte work/rounds/R02-*.md und diese Slice-Datei.
Setze Slice 48 "Formulare in Personal, Buchhaltung und Lohn" um (diese Datei).
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
- [ ] Given das Einladungsformular mit Name und E-Mail When im Feld E-Mail Enter gedrückt wird Then geht die Einladung ab
- [ ] Given das Einladungsformular ohne E-Mail When „Einladen“ den Fokus bekommt Then nennt ein Hilfetext den Grund
- [ ] Manuell: Personal › Einladen: Name und E-Mail eintragen, Enter: Einladung erscheint in der Liste

## Nicht in dieser Slice
–
