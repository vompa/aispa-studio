# Slice 34 – Einzelpunkte Windows-App: Standardknöpfe, Ansage, Konflikt, Stornieren

**Ziel:** Vier Einzelabweichungen der Windows-App sind behoben.
**Runde:** R02
**Quelle:** R02 – W4, W19, W25, W56
**Hängt ab von:** –
**Modell:** sonnet

## Umfang
- Anmeldung: `IsDefault` nur an sichtbaren Formularen, genau ein sichtbarer Standardknopf (W4)
- Verbindungsfehler: Statustext mit `LiveSetting="Assertive"` (W19)
- Konfliktmeldung im Kalender als `ui:InfoBar` mit Aktion statt eigenem Border (W25)
- „Ganze Buchung stornieren“ mit `Classes="danger"` statt lokalem Foreground (W56)

## Muster übernehmen
- src/AispaStudio.Desktop/Controls/InfoBar.cs:23 – InfoBar

## NICHT bauen
–

## Prompt
```
Lies CLAUDE.md, work/conventions.md, die Rundenakte work/rounds/R02-*.md und diese Slice-Datei.
Setze Slice 34 "Einzelpunkte Windows-App: Standardknöpfe, Ansage, Konflikt, Stornieren" um (diese Datei).
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
- [ ] Given die Anmeldeseite in jedem Zustand When die sichtbaren Knöpfe gezählt werden Then hat genau einer `IsDefault`
- [ ] Given ein Buchungskonflikt When er erscheint Then ist er eine InfoBar mit Severity Error und einem Aktionsknopf
- [ ] Given „Ganze Buchung stornieren“ When gerendert Then trägt der Knopf die Klasse `danger`
- [ ] Manuell: Termin auf eine belegte Zeit verschieben: Konflikt erscheint als rote Meldung mit Knopf

## Nicht in dieser Slice
–
