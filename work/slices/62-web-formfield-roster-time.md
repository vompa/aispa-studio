# Slice 62 – Formularbaustein in Dienstplan, Zeiten, Ausfall-Assistent und Berichten

**Ziel:** Die Felder in Dienstplan, Zeiterfassung, Ausfall-Assistent und Berichten hängen Hilfetext und Fehler ans Feld, gesperrte Knöpfe nennen ihren Grund.
**Runde:** R03
**Quelle:** R03 – B77, B78
**Hängt ab von:** –
**Modell:** sonnet
**IDs für neue Befunde:** B99–B103

## Umfang
- `roster/RosterPage.tsx` (`DayEditor`), `time/TimesheetPage.tsx` (Nachtrag), `roster/AbsenceAssistant.tsx` und die Felder in `reports/*View.tsx` auf `FormField`
- Gesperrte Knöpfe mit `aria-disabled` und sichtbarem Grund per `aria-describedby`: „Übernehmen (n)“ im Ausfall-Assistenten, „Tagesstart abnehmen“ und „Raumpflege … abnehmen“ in `reports/HygieneView.tsx`, „Sofort gültig, ohne Veröffentlichen“ und weitere, die die Durchsicht nennt
- Was Slice 39 (Pflicht-Grund, „Endet am Folgetag“) und Slice 42 (`SidePanel`) gebaut haben, bleibt erhalten

## Muster übernehmen
- web/src/components/FormField.tsx:14 – FormField
- web/src/admin/StaffPage.tsx – gesperrter Knopf mit sichtbarem Grund (Slice 48)

## NICHT bauen
- Neue Funktionen in Dienstplan und Berichten

## Prompt
```
Lies CLAUDE.md, work/conventions.md, die Rundenakte work/rounds/R03-*.md und diese Slice-Datei.
Setze Slice 62 "Formularbaustein in Dienstplan, Zeiten, Ausfall-Assistent und Berichten" um (diese Datei).
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
- [ ] Given der Ausfall-Assistent ohne Auswahl When „Übernehmen“ den Fokus bekommt Then ist der Grund sichtbar und per `aria-describedby` verbunden
- [ ] Given das Tagespanel des Dienstplans und das Nachtragsformular When ein Prüftest Eingabefelder in einem umhüllenden `<label>` ohne `FormField` sucht Then gibt es keinen Treffer
- [ ] Manuell: Hygiene-Bericht: „Tagesstart abnehmen“ ohne Prüferin per Tab fokussieren: der Grund steht sichtbar daneben

## Nicht in dieser Slice
–
