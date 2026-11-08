# Slice 24 – Hilfe, Begleiter, Assistent und Abgleich im Web: Fokus und Ansagen

**Ziel:** Hilfe und Begleiter nehmen und geben den Fokus richtig, Esc schließt nur die oberste Ebene, Antworten werden angesagt.
**Runde:** R01
**Quelle:** R01 – B18, B19, B21, B23
**Hängt ab von:** –
**Modell:** –

## Umfang
- help/HelpDrawer.tsx: Esc nur, wenn unbehandelt und Fokus in der Leiste; Fokus beim Öffnen auf die Überschrift, beim Schließen zurück zum Auslöser (B18)
- companion/Companion.tsx: Knopf bleibt im DOM oder Fokus per ref; beim Schließen zurück zum Knopf (B19)
- admin/SyncStatus.tsx: `popover="auto"` statt Eigenbau, ohne `role="dialog"` (B21)
- assistant/AssistantPanel.tsx: Live-Region `role="status"` dauerhaft im DOM, Antwort darin (B23)

## Muster übernehmen
- web/src/promotions/PromotionsPage.tsx:389 – Popover
- web/src/booking/BookingApp.tsx:63 – Fokus per ref

## NICHT bauen
- B22 Bildmaße der Handbuchbilder (offene Entscheidung)
- B20 Größe des Fragezeichens (offene Entscheidung)

## Prompt
```
Lies CLAUDE.md, work/conventions.md, die Rundenakte work/rounds/R01-*.md und diese Slice-Datei.
Setze Slice 24 "Hilfe, Begleiter, Assistent und Abgleich im Web: Fokus und Ansagen" um (diese Datei).
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
- [ ] Given die Hilfe ist offen und ein Dialog darüber When Esc gedrückt wird Then schließt nur der Dialog
- [ ] Given die Hilfe wurde über ein Fragezeichen geöffnet When sie geschlossen wird Then liegt der Fokus wieder auf diesem Fragezeichen
- [ ] Given der Begleiter wird geöffnet und geschlossen When der Fokus geprüft wird Then lag er beim Öffnen in der Leiste und danach auf dem Knopf
- [ ] Given der Assistent vor der ersten Frage When das DOM geprüft wird Then steht schon ein leeres Element mit `role="status"`, in das die Antwort kommt
- [ ] Given der Abgleich-Status ist offen When Esc gedrückt wird Then ist er geschlossen
- [ ] Manuell: Verwaltung › Fragezeichen an einer Einstellung: Hilfe öffnet mit Fokus auf der Überschrift, Esc schließt und der Fokus steht wieder am Fragezeichen

## Nicht in dieser Slice
Neue Hilfe-Inhalte
