# Slice 147 – Assistent: Aufträge öffnen die vorausgefüllte Maske

**Ziel:** Aufträge wie „buche“, „verschiebe“, „sag ab“, „Bogen senden“ und „anbieten“ öffnen die passende Maske vorausgefüllt; gespeichert wird erst per Knopf der Person.
**Runde:** R07
**Quelle:** R07 – Ä319
**Hängt ab von:** 138, 139
**Modell:** opus
**Prüfstufe:** –
**IDs für neue Befunde:** Ä385–Ä387

## Umfang
- Die API erkennt den Auftrag und liefert Art und Vorschlag (Gast, Behandlung, Tag, Zeit, Termin); die App öffnet damit die bestehende Maske (Neuer Termin, Verschieben, Absage mit Gebühr, Bogen senden, Anbieten aus der Warteliste)
- „offene Rechnung“ springt in die Kassa mit der Rechnung
- Die KI speichert nie selbst; die Maske zeigt den Hinweis auf den Vorschlag der KI; fehlt etwas, bleibt das Feld leer und die Maske fragt
- Nachtrag zu ADR 0072 (Aufträge nur als Vorschlag in der Maske); Hilfe zum Assistenten nachziehen
- Rechte: der Auftrag erscheint nur, wenn die Person die Maske nutzen darf

## Muster übernehmen
- src/AispaStudio.Api/Assistant/AssistantIntent.cs
- src/AispaStudio.Api/Assistant/RoleAssistant.cs
- src/AispaStudio.Desktop/ViewModels/CompanionViewModel.Suggestions.cs
- docs/konzepte/assistent-rezeption-fragen.md (Vorschlag Aufträge)
- docs/entscheidungen/0072-assistent-beantwortet-betriebsfragen.md
- .claude/skills/fluent2-avalonia (responsible-ai.md)

## Voraussichtlich geänderte Dateien
- src/AispaStudio.Api/Assistant/
- src/AispaStudio.Desktop/ViewModels/CompanionViewModel.cs
- src/AispaStudio.Desktop/ViewModels/CompanionViewModel.Suggestions.cs
- src/AispaStudio.Desktop/Views/CompanionContent.axaml
- tests/AispaStudio.Api.Tests/
- tests/AispaStudio.Desktop.Tests/
- docs/entscheidungen/0072-assistent-beantwortet-betriebsfragen.md
- docs/konzepte/assistent-rezeption-fragen.md
- web/src/help/content/

## NICHT bauen
- Speichern ohne Knopfdruck der Person
- Aufträge an Lager oder Dienstplan

## Prompt
```
Lies CLAUDE.md, work/conventions.md, die Rundenakte work/rounds/R07-*.md und diese Slice-Datei.
Setze Slice 147 "Assistent: Aufträge öffnen die vorausgefüllte Maske" um (diese Datei).
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
- [ ] Given Christian Steiner ist Gast When jemand „buche steiner morgen 10 uhr massage“ schreibt Then öffnet sich „Neuer Termin“ mit Gast, Behandlung, Tag und 10:00, und es ist nichts gebucht
- [ ] Given Steiner hat morgen einen Termin When jemand „sag den termin von steiner ab“ schreibt Then öffnet sich die Absage dieses Termins mit der Gebühr, und abgesagt ist erst nach dem Knopf
- [ ] Given eine Therapeutin ohne Buchungsrecht When sie „buche …“ schreibt Then bietet die Antwort keine Buchungsmaske an
- [ ] Manuell: Als Rezeption „buche …“, „verschiebe …“, „sag … ab“, „schick … den bogen“ und „offene rechnung …“ ausprobieren, jeweils abbrechen und einmal speichern

## Nicht in dieser Slice
–
