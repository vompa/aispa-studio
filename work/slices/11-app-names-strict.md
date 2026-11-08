# Slice 11 – Namen nie aus Platzhaltern, Listenzeilen benannt, Namens-Scan streng

**Ziel:** Fehlende Beschriftungen fallen im Namens-Scan auf, und Listenzeilen heißen nach ihrem Inhalt statt nach dem Record-Typ.
**Runde:** R01
**Quelle:** R01 – W64, W-Muster 7
**Hängt ab von:** 06, 07, 08, 09, 10
**Modell:** –

## Umfang
- `AccessibleNames.NameFromPlaceholder` entfernen (W64)
- ListBoxItem-Stil mit `AutomationProperties.Name` an die Anzeige-Eigenschaft für die 10 ListBoxen aus W-Muster 7 (Customers, Calendar ×5, Companion, Help, Inbox, Pos)
- Namens-Scan erkennt Record-Texte (enthält „{“ und endet mit „}“) und läuft mit gefüllten Listen

## Muster übernehmen
- src/AispaStudio.Desktop/Views/ShellView.axaml:14 – Stil, der den Zeilennamen an die Anzeige bindet
- tests/AispaStudio.Desktop.Tests/ViewTests.FocusOrder.cs:210 – `HasUnreadableName`
- src/AispaStudio.Desktop/Controls/AccessibleNames.cs:30 – heutige Ableitung aus dem Platzhalter

## NICHT bauen
- W65 Namens-Scan je Zustand für alle Seitenleisten und Dialoge (offene Entscheidung)

## Prompt
```
Lies CLAUDE.md, work/conventions.md, die Rundenakte work/rounds/R01-*.md und diese Slice-Datei.
Setze Slice 11 "Namen nie aus Platzhaltern, Listenzeilen benannt, Namens-Scan streng" um (diese Datei).
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
- [ ] Given AccessibleNames When man den Code liest Then leitet nichts mehr einen Namen aus `PlaceholderText` ab, und der Namens-Scan aller Seiten bleibt grün
- [ ] Given die Kundensuche mit drei Treffern When der Scan läuft Then heißt jede Zeile nach dem Namen der Person und kein Name enthält „{“
- [ ] Given ein Element mit dem Namen „FreeSlotRow { Start = 09:00 }“ When `HasUnreadableName` prüft Then gilt der Name als unlesbar
- [ ] Manuell: Narrator an, Kunden suchen, mit Pfeil durch die Treffer: Narrator liest die Namen, nicht „CustomerDto“

## Nicht in dieser Slice
Neue Barrierefreiheits-Werkzeuge
