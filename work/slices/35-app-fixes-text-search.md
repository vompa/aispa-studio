# Slice 35 – Einzelpunkte Windows-App: Technikdeutsch, Hilfesuche, Meldungskarte, Menü

**Ziel:** Vier weitere Einzelabweichungen der Windows-App sind behoben.
**Runde:** R02
**Quelle:** R02 – W8, W27, W57, W58
**Hängt ab von:** –
**Modell:** sonnet

## Umfang
- Technikdeutsch im Fließtext (NetworkView, Sync, Devices) als Kurzsatz plus aufklappbare „Technische Angaben“ (W8)
- Suche der Hilfe als `ui:SearchField`, Trefferzahl höflich angesagt (W27)
- Meldungskarte als `Button`/`ListBoxItem` statt `Border` mit Zeigerereignissen (W57)
- Menü „Neuer Termin“: „Sonderfälle“ nicht als deaktivierter Eintrag, sondern als Gruppe mit Trenner (W58)

## Muster übernehmen
- src/AispaStudio.Desktop/Controls/SearchField.cs – SearchField

## NICHT bauen
–

## Prompt
```
Lies CLAUDE.md, work/conventions.md, die Rundenakte work/rounds/R02-*.md und diese Slice-Datei.
Setze Slice 35 "Einzelpunkte Windows-App: Technikdeutsch, Hilfesuche, Meldungskarte, Menü" um (diese Datei).
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
- [ ] Given die Hilfe When ein Suchbegriff eingegeben wird Then ist das Feld ein SearchField und die Trefferzahl hat LiveSetting Polite
- [ ] Given eine Meldungskarte When ihr Peer abgefragt wird Then ist die Rolle Button und der Name enthält Titel und Hinweis
- [ ] Given das Menü „Neuer Termin“ When der Scan läuft Then gibt es keinen deaktivierten Eintrag „Sonderfälle“
- [ ] Manuell: Hilfe öffnen, „Kasse“ suchen: Lupe, Löschen-Kreuz und Trefferzahl sind da

## Nicht in dieser Slice
–
