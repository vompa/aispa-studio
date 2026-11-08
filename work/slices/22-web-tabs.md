# Slice 22 – Reiter im Web mit Pfeiltasten, Roving-Tabindex und Tabpanel

**Ziel:** Alle Reiterschienen der Verwaltung bedienen sich wie ARIA-Tabs: ein Tab-Stopp, Pfeiltasten, Pos1 und Ende.
**Runde:** R01
**Quelle:** R01 – B3
**Hängt ab von:** –
**Modell:** –

## Umfang
- Gemeinsame Komponente `Tabs` aus stock/stockUi.tsx herausziehen: Pfeile, Pos1/Ende, `aria-controls`, `role="tabpanel"`
- Die sechs Stellen umstellen: admin/MasterDataPage.tsx, admin/TreatmentsTab.tsx, calendar/CalendarPage.tsx, reports/BenchmarkView.tsx, reports/ReportsPage.tsx, stock/stockUi.tsx
- `nav role="tablist"` durch `div` ersetzen

## Muster übernehmen
- web/src/stock/stockUi.tsx:120 – Pfeiltasten
- web/src/stock/stockUi.tsx:139 – Roving-Tabindex

## NICHT bauen
- Neues Aussehen der Reiter

## Prompt
```
Lies CLAUDE.md, work/conventions.md, die Rundenakte work/rounds/R01-*.md und diese Slice-Datei.
Setze Slice 22 "Reiter im Web mit Pfeiltasten, Roving-Tabindex und Tabpanel" um (diese Datei).
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
- [ ] Given die Reiter der Stammdaten mit Fokus auf dem ersten When Pfeil rechts gedrückt wird Then liegen Fokus und Auswahl auf dem zweiten; Ende wählt den letzten, Pos1 den ersten
- [ ] Given ein gewählter Reiter When das DOM geprüft wird Then zeigt `aria-controls` auf ein Element mit `role="tabpanel"`, und nur der gewählte Reiter hat `tabIndex` 0
- [ ] Manuell: Stammdaten öffnen, mit Tab zu den Reitern, mit den Pfeiltasten wechseln

## Nicht in dieser Slice
Neue Reiter
