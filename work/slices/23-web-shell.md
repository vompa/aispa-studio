# Slice 23 – Shell der Verwaltung: main, h1, Skip-Link, Seitentitel, Kontomenü als Popover

**Ziel:** Die angemeldete Verwaltung hat Landmarken, eine Überschrift je Seite, einen Skip-Link, einen Seitentitel und ein Kontomenü, das mit Esc schließt.
**Runde:** R01
**Quelle:** R01 – B12, B13, B16
**Hängt ab von:** –
**Modell:** –

## Umfang
- App.tsx: Seiteninhalt in `<main id="inhalt" tabIndex={-1}>`, Skip-Link, genau ein `h1` je Seite (B12)
- `document.title` je Seite „{Seite} · AISPA Studio“ (B13)
- Kontomenü (App.tsx :328) als `popover="auto"` mit `popovertarget` statt `<details>` (B16)

## Muster übernehmen
- web/src/booking/BookingApp.tsx:84 – Skip-Link des Kundenbereichs
- web/src/promotions/PromotionsPage.tsx:389 – Popover-Menü

## NICHT bauen
- B14 Fokus nach dem Seitenwechsel und B15 `aria-current` der Seitenreiter (offene Entscheidung)

## Prompt
```
Lies CLAUDE.md, work/conventions.md, die Rundenakte work/rounds/R01-*.md und diese Slice-Datei.
Setze Slice 23 "Shell der Verwaltung: main, h1, Skip-Link, Seitentitel, Kontomenü als Popover" um (diese Datei).
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
- [ ] Given eine angemeldete Inhaberin auf einer beliebigen Seite When das DOM geprüft wird Then gibt es genau ein `main#inhalt` und genau ein `h1`, und das erste fokussierbare Element ist der Skip-Link
- [ ] Given die Seite „Kalender“ When sie angezeigt wird Then lautet `document.title` „Kalender · AISPA Studio“
- [ ] Given das offene Kontomenü When Esc gedrückt wird Then ist es geschlossen und der Fokus liegt auf dem Kontoknopf
- [ ] Manuell: Verwaltung im Browser: Tab nach dem Laden zeigt „Zum Inhalt“; der Reiter des Browsers nennt die Seite

## Nicht in dieser Slice
Neue Navigation
