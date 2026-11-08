# Slice 31 – KI und Hilfe in allen Masken der Windows-App

**Ziel:** Der runde Knopf mit dem App-Symbol und die Leiste „KI und Hilfe“ sind auf jeder Seite der Windows-App erreichbar, auch neben dem eingebetteten Browser, und verdecken keinen Inhalt.
**Runde:** R02
**Quelle:** R02 – Ä74
**Hängt ab von:** –
**Modell:** opus

## Umfang
- Fehler in der laufenden App nachstellen: Der Auftraggeber sieht den Knopf nur im Kalender, nicht bei Kunden und Posteingang (ein Headless-Test auf `dev` zeigt ihn überall); Ursache finden, bevor geändert wird
- Seiten mit `EmbeddedBrowser` (Verwaltung, Hilfe): Knopf und Leiste neben dem nativen Browserfenster statt darüber
- Inhalt jeder Seite lässt unten rechts Platz für den Knopf (auf „Anfragen“ verdeckt er heute Text der Mail)
- Strg+Leertaste öffnet die Leiste auf jeder Seite

## Muster übernehmen
- src/AispaStudio.Desktop/Views/ShellView.axaml:154 – Knopf `CompanionFab`
- src/AispaStudio.Desktop/Views/EmbeddedBrowser.cs – eingebetteter Browser

## NICHT bauen
- Neue Inhalte der Leiste

## Prompt
```
Lies CLAUDE.md, work/conventions.md, die Rundenakte work/rounds/R02-*.md und diese Slice-Datei.
Setze Slice 31 "KI und Hilfe in allen Masken der Windows-App" um (diese Datei).
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
- [ ] Given jede Seite der Leiste (Kalender, Kunden, Anfragen, Kassa, Geräte, Verwaltung, Hilfe) When sie angezeigt wird Then ist der Knopf `CompanionFab` sichtbar und ein Klick in seine Mitte trifft ihn
- [ ] Given „Anfragen“ mit geöffneter Mail When der Knopf angezeigt wird Then überlappt er keinen Text der Seite
- [ ] Given die Verwaltung (eingebetteter Browser) When Strg+Leertaste gedrückt wird Then ist die Leiste „KI und Hilfe“ sichtbar und nicht vom Browser verdeckt
- [ ] Manuell: App starten, nacheinander Kunden, Anfragen, Kassa, Verwaltung, Hilfe öffnen: unten rechts steht überall der runde Knopf mit dem App-Symbol
- [ ] Manuell: Auf der Verwaltung Strg+Leertaste: die Leiste erscheint neben dem Browser, nicht dahinter

## Nicht in dieser Slice
–
