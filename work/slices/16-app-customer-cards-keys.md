# Slice 16 – Kundenkarten: Esc, Enter und Startfokus

**Ziel:** Die Kundenkarten bedienen sich mit der Tastatur wie die Kundendaten-Maske.
**Runde:** R01
**Quelle:** R01 – W38 (Teil 2)
**Hängt ab von:** 15
**Modell:** –

## Umfang
- Die sechs Karten: `IsDefault` am Speichern, Esc schließt ohne zu speichern, Startfokus im ersten Feld

## Muster übernehmen
- src/AispaStudio.Desktop/Views/CustomerContactForm.axaml:155 – Speichern mit `IsDefault`
- src/AispaStudio.Desktop/Views/CustomersView.axaml.cs:37 – Startfokus der Kontaktmaske (`FocusFirstField`)

## NICHT bauen
- Maske „Neue Kundin“ (W36, bekannt)

## Prompt
```
Lies CLAUDE.md, work/conventions.md, die Rundenakte work/rounds/R01-*.md und diese Slice-Datei.
Setze Slice 16 "Kundenkarten: Esc, Enter und Startfokus" um (diese Datei).
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
- [ ] Given die Karte „Rabatt“ im Bearbeiten-Zustand mit geändertem Wert When Esc gedrückt wird Then ist die Karte geschlossen und der alte Wert steht
- [ ] Given die Karte „Rabatt“ im Bearbeiten-Zustand When im Feld Enter gedrückt wird Then ist gespeichert und die Karte geschlossen
- [ ] Given eine Karte wird geöffnet When sie erscheint Then liegt der Fokus im ersten Feld
- [ ] Manuell: Kunden › Rabatt bearbeiten: Wert ändern, Esc: alter Wert bleibt; erneut öffnen, Enter: gespeichert

## Nicht in dieser Slice
Neue Tastenkürzel
