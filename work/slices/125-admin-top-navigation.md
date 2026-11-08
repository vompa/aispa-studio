# Slice 125 – Verwaltung: Navigation oben mit Gruppen statt Liste

**Ziel:** Die Auswahl der Verwaltungsmasken ist keine Liste mehr, sondern ein heller Balken oben im Stil der linken Leiste mit sieben Gruppen; die Masken einer Gruppe stehen darunter als ruhige Reiter.
**Runde:** R05
**Quelle:** R05 – Ä293
**Hängt ab von:** 123
**Modell:** opus
**IDs für neue Befunde:** Ä294–Ä296

## Umfang
- Gruppen: Betrieb (Firmendaten, Standorte & Räume, Schließzeiten, Raumsperren, Kalender-Abos, Handbuch), Angebot (Behandlungen, Aktionen), Team, Lager, Kassa & Geld (Kassa, Zahlungen, Kosten, Buchhaltung), Kontakt (Erinnerungen, Postfach & Nummern, KI), Zahlen (Berichte, Betriebsinfo)
- Balken: Symbol 22 px, Text 12 px, Radius 12 wie die linke Leiste; Sandstrich unter dem gewählten Eintrag; Fläche `rail-hover`, gewählt `rail-selected`
- Neuer Zwischenwert für das Überfahren mit der Maus zuerst in docs/design.md (hell und dunkel, Kontrast je Zustand gemessen), dazu ein Abschnitt zur Navigation der Verwaltung im Brief
- Reiterzeile nur bei mehreren Masken; Team und Lager behalten darunter ihre eigenen Reiter
- Gruppen ohne Rechte verschwinden, jede Gruppe merkt sich ihre zuletzt gewählte Maske, bestehende Sprungziele bleiben gültig
- Tastatur: Balken ist ein Tab-Stopp, Pfeiltasten wechseln, F6 springt zwischen den Bereichen; zu schmales Fenster: Einträge wandern in ein Menü „Mehr“, kein Umbruch
- Hilfe und betroffene Handbuchbilder nachziehen

## Muster übernehmen
- `ListBox.nav` in src/AispaStudio.Desktop/Views/ShellView.axaml und Themes/Styles.axaml
- src/AispaStudio.Desktop/ViewModels/AdminPageViewModel.cs – Liste der Masken mit Rechten
- docs/gestaltung/fluent2-ableitung.md und .claude/skills/fluent2-avalonia (navigation.md, accessibility.md)

## Voraussichtlich geänderte Dateien
- docs/design.md
- src/AispaStudio.Desktop/Themes/Colors.axaml
- src/AispaStudio.Desktop/Themes/Styles.axaml
- src/AispaStudio.Desktop/ViewModels/AdminPageViewModel.cs
- src/AispaStudio.Desktop/Views/
- tests/AispaStudio.Desktop.Tests/
- web/src/help/content/

## NICHT bauen
- Änderung an der linken Leiste
- neue Masken oder geänderte Rechte

## Prompt
```
Lies CLAUDE.md, work/conventions.md, die Rundenakte work/rounds/R05-*.md und diese Slice-Datei.
Setze Slice 125 "Verwaltung: Navigation oben mit Gruppen statt Liste" um (diese Datei).
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
- [ ] Given die Inhaberin When sie die Verwaltung öffnet Then zeigt der Balken die sieben Gruppen und keine Liste
- [ ] Given die Gruppe „Betrieb“ mit gewählten Schließzeiten When zu „Lager“ und zurück gewechselt wird Then sind wieder die Schließzeiten gewählt
- [ ] Given eine Person ohne Recht auf eine ganze Gruppe When sie die Verwaltung öffnet Then fehlt diese Gruppe
- [ ] Given ein Sprungziel zu einer Maske aus Kalender, Assistent oder Hilfe When es geöffnet wird Then sind die richtige Gruppe und Maske gewählt
- [ ] Given ein 1024 px breites Fenster When die Verwaltung offen ist Then bricht der Balken nicht um und die übrigen Einträge sind per Tastatur im Menü „Mehr“ erreichbar
- [ ] Given jeder Zustand des Balkens in hell und dunkel When der Kontrast gemessen wird Then hat Text mindestens 4,5:1
- [ ] Manuell: Verwaltung als Inhaberin im echten Fenster bei 1366 px und bei voller Breite durchgehen, nur mit Tastatur und mit Screenreader

## Nicht in dieser Slice
–
