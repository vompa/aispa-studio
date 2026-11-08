# Slice 08 – Feldnamen, Warnfarbe und Speichern-Hinweis in den Kunden

**Ziel:** Die Kundenkarten haben beschriftete Felder, Warnungen in Warnfarbe mit Symbol, und Speichern nennt den Grund, wenn es gesperrt ist.
**Runde:** R01
**Quelle:** R01 – W37, W39, W43, W-Muster 1 (Teil)
**Hängt ab von:** 05
**Modell:** –

## Umfang
- Kundenwünsche und Rechnungsempfänger: neun Felder (:371-378, :478-480) mit FormField, ComboBoxen :345, :430, :435 benannt, „Wünscht“ als Beschriftung über dem Feld (W37)
- Felder der Maske „Neue Kundin“ (:59-136) bekommen Namen; der Umbau auf CustomerContactForm (W36) bleibt draußen
- Warnzeilen der Kennzahlen (`IsWarning`, :305-309) in `WarnBrush` mit Warnsymbol statt `CritBrush` (W39)
- CustomerContactForm: Speichern mit `ui:DisabledHint.Text="Verfügbar, sobald ein Vor- oder Nachname eingetragen ist."` (W43)

## Muster übernehmen
- src/AispaStudio.Desktop/Views/CustomerContactForm.axaml:25 – FormField im Einsatz
- src/AispaStudio.Desktop/Controls/DisabledHint.cs:14 – Grund am gesperrten Knopf

## NICHT bauen
- W36 Anlage über CustomerContactForm (bekannt)
- W40 Tooltip-Hilfen der Kundenkarte (offene Entscheidung)
- Bearbeiten-Verhalten der Karten (Slices 15 und 16)

## Prompt
```
Lies CLAUDE.md, work/conventions.md, die Rundenakte work/rounds/R01-*.md und diese Slice-Datei.
Setze Slice 08 "Feldnamen, Warnfarbe und Speichern-Hinweis in den Kunden" um (diese Datei).
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
- [ ] Given eine Kundin mit einer Kennzahl `IsWarning` When die Kundenkarte sie zeigt Then nutzt die Zeile `WarnBrush` und ein Warnsymbol, nicht `CritBrush`
- [ ] Given die Kundendaten-Maske ohne Vor- und Nachnamen When man auf „Speichern“ zeigt Then erscheint „Verfügbar, sobald ein Vor- oder Nachname eingetragen ist.“
- [ ] Given Kundenwünsche und Rechnungsempfänger im Bearbeiten-Zustand When der Namens-Scan läuft Then hat jedes Feld einen Namen, der nicht aus dem Platzhalter stammt
- [ ] Manuell: Kunden › eine Kundin › Wünsche bearbeiten: jedes Feld hat eine Beschriftung darüber

## Nicht in dieser Slice
Neue Kundenfelder
