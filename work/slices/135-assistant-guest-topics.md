# Slice 135 – Assistent: Verlauf des Gastes, Termine und Absagen

**Ziel:** Der Assistent beantwortet die häufigen Fragen der Rezeption zu Gästen, Terminen und Absagen aus den Daten: „Historie Christian Steiner“ zeigt den Verlauf des Gastes statt eines Hilfethemas.
**Runde:** R06
**Quelle:** R06 – Ä317
**Hängt ab von:** 134
**Modell:** opus
**Prüfstufe:** –
**IDs für neue Befunde:** Ä338–Ä340

## Umfang
- Verlauf des Gastes („historie“, „verlauf“, „besuche“): letzte Besuche mit Behandlung und Person, Käufe, nächster Termin, Nichterscheinen und Absagen, Sprung zur Kundenkarte; Hilfethemen nur ergänzend darunter
- Offene Gutscheine und Paket-Einheiten des Gastes mit Restwert; offene oder geparkte Rechnung und Anzahlung; Kundenrabatt aus der Kartei
- Stand der Anamnese (ausgefüllt, fällig, fehlt); Hinweise wie Allergien nur mit `AccessRight.ViewHealthData`
- Termine: wer kommt als Nächstes, wer ist nach Beginn noch nicht eingecheckt, „heute nachmittag“ als Zeitraum
- Absagen: wer hat heute abgesagt, fällt bei einer Absage jetzt eine Gebühr an (nach der Stornoregel), Warteliste mit Wunschzeit
- Rechte wie im Rest der API; fehlt ein Recht, sagt die Antwort es
- Stand in docs/konzepte/assistent-rezeption-fragen.md und die Hilfe nachziehen

## Muster übernehmen
- src/AispaStudio.Api/Assistant/Answers/GuestAnswers.cs, CancellationAnswers.cs, BusinessIntents.cs, BusinessTopic.cs
- docs/konzepte/assistent-rezeption-fragen.md – Abschnitte Kunden, Termine, Absagen
- docs/entscheidungen ADR 0072

## Voraussichtlich geänderte Dateien
- src/AispaStudio.Api/Assistant/
- src/AispaStudio.Contracts/
- web/openapi/aispa-api.json
- tests/AispaStudio.Api.Tests/
- docs/konzepte/assistent-rezeption-fragen.md
- web/src/help/content/

## NICHT bauen
- Aufträge an den Assistenten wie „buche“, „verschiebe“, „sag ab“ (zurückgestellt, Ä319)
- Beträge oder Umsätze ohne das passende Recht

## Prompt
```
Lies CLAUDE.md, work/conventions.md, die Rundenakte work/rounds/R06-*.md und diese Slice-Datei.
Setze Slice 135 "Assistent: Verlauf des Gastes, Termine und Absagen" um (diese Datei).
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
- [ ] Given Christian Steiner war dreimal da und hat einen Termin nächste Woche When jemand „historie christian steiner“ fragt Then zeigt die Antwort die drei Besuche, den nächsten Termin und den Sprung zur Kundenkarte
- [ ] Given ein Gast hat einen Wertgutschein mit 30 € Rest When jemand „hat sie noch einen gutschein“ zu seinem offenen Termin fragt Then nennt die Antwort den Gutschein mit 30,00 €
- [ ] Given ein Termin hat vor 10 Minuten begonnen und ist nicht eingecheckt When jemand „wer ist noch nicht da“ fragt Then steht dieser Termin in der Antwort
- [ ] Given die Stornoregel verlangt 50 % unter 24 Stunden When jemand für einen Termin in 5 Stunden „fällt eine gebühr an“ fragt Then nennt die Antwort 50 % mit Betrag
- [ ] Given eine Therapeutin ohne Recht auf Beträge When sie nach offenen Rechnungen fragt Then sagt die Antwort, dass das Recht fehlt
- [ ] Manuell: Als Rezeption die Fragen der Abschnitte Kunden, Termine und Absagen aus docs/konzepte/assistent-rezeption-fragen.md stellen

## Nicht in dieser Slice
–
