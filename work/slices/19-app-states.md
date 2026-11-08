# Slice 19 – Reiter und Aufklapp-Köpfe melden Auswahl und Aufklappen

**Ziel:** Reiterschienen melden den gewählten Reiter, Aufklapp-Köpfe melden „aufgeklappt“ statt „aktiviert“.
**Runde:** R01
**Quelle:** R01 – W26, W47 (Reiter), W-Muster 5
**Hängt ab von:** 07, 09
**Modell:** –

## Umfang
- Fünf Reiterschienen mit Auswahlzustand: PosView :18-22 und :635-638, CalendarView :1228, :2046/:2054, Seitenleisten-Reiter :2872
- Vier `ToggleButton.more` auf `ui:AccordionHeader`: AiFooter :16, CalendarView :354 und :1086, CustomerContactForm :101 (W26)

## Muster übernehmen
- src/AispaStudio.Desktop/Controls/AccordionHeader.cs:14 – Kopf mit Expand-Peer
- src/AispaStudio.Desktop/Views/CalendarView.axaml:672 – AccordionHeader im Einsatz
- src/AispaStudio.Desktop/Controls/TabStripBehavior.cs:14 – vorhandenes Verhalten der Reiterschienen

## NICHT bauen
- Neues Aussehen der Reiter

## Prompt
```
Lies CLAUDE.md, work/conventions.md, die Rundenakte work/rounds/R01-*.md und diese Slice-Datei.
Setze Slice 19 "Reiter und Aufklapp-Köpfe melden Auswahl und Aufklappen" um (diese Datei).
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
- [ ] Given die Kassa mit gewähltem Reiter „Belege“ When die Peers abgefragt werden Then meldet „Belege“ ausgewählt und die anderen Reiter nicht
- [ ] Given „Warum dieser Vorschlag?“ ist aufgeklappt When der Peer abgefragt wird Then meldet er ExpandCollapseState Expanded
- [ ] Manuell: Narrator an, Kassa, Reiter wechseln: Narrator sagt „ausgewählt“; „Warum dieser Vorschlag?“ öffnen: Narrator sagt „erweitert“

## Nicht in dieser Slice
Neue Reiter
