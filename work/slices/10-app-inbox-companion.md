# Slice 10 – Posteingang und Leiste „KI und Hilfe“: Namen, KI-Kennzeichen, ein Primärknopf

**Ziel:** Posteingang und Leiste „KI und Hilfe“ haben beschriftete Felder, kennzeichnen jede KI-Ausgabe, sagen Antworten an und zeigen einen Primärknopf.
**Runde:** R01
**Quelle:** R01 – W28, W29, W30, W32, W34, W35, W-Muster 1, 4, 13 (Teil)
**Hängt ab von:** 05
**Modell:** –

## Umfang
- CompanionContent: „Fragen“ primär, „Termin buchen“ und die Hilfe-Aktion sekundär (W28); Antwortkarte mit `LiveSetting="Polite"` (W29); „Für wen?“ als FormField, `BookedText` mit Haken-Symbol (W30)
- InboxView: Antworttext als FormField mit `Label="{Binding ReplyLabel}"`, Betreff, Wunsch und Hinweis benannt (W32); `AiPill` vor der KI-Zusammenfassung in der Listenzeile (W34); Knöpfe „Freundlicher formulieren“, „Kürzer formulieren“, „Duzen“, „Entschuldigung ergänzen“ (W35)
- HelpView: Suchfeld mit Namen (W-Muster 1)

## Muster übernehmen
- src/AispaStudio.Desktop/Controls/FormField.cs:27 – FormField

## NICHT bauen
- W31 Ausschalter je KI-Funktion und Erstöffnungskarte (bekannt)
- W27 Suchfeld der Hilfe als SearchField (offene Entscheidung)

## Prompt
```
Lies CLAUDE.md, work/conventions.md, die Rundenakte work/rounds/R01-*.md und diese Slice-Datei.
Setze Slice 10 "Posteingang und Leiste „KI und Hilfe“: Namen, KI-Kennzeichen, ein Primärknopf" um (diese Datei).
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
- [ ] Given die Leiste mit Antwort, Buchungsvorschlag und Hilfe-Aktion When ein Headless-Test die sichtbaren `primary`-Knöpfe zählt Then ist es genau einer („Fragen“)
- [ ] Given eine Frage an den Assistenten When die Antwortkarte erscheint Then hat sie LiveSetting Polite
- [ ] Given eine Posteingangszeile mit KI-Zusammenfassung When sie gerendert wird Then steht ein `AiPill` in der Zeile
- [ ] Given ein Antwortentwurf When die Überarbeiten-Knöpfe gerendert werden Then heißen sie „Freundlicher formulieren“, „Kürzer formulieren“, „Duzen“, „Entschuldigung ergänzen“
- [ ] Manuell: Strg+Leertaste, Frage stellen: nur „Fragen“ ist hervorgehoben
- [ ] Manuell: Posteingang › Nachricht mit KI-Zusammenfassung: die Zeile zeigt das KI-Kennzeichen

## Nicht in dieser Slice
Neue KI-Funktionen
