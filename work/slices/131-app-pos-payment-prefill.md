# Slice 131 – Kassa: Betrag der Zahlart mit dem offenen Rest vorbelegen

**Ziel:** Klickt man in das Betragsfeld einer Zahlart (Bar, Karte, Gutschein, Überweisung …), steht dort sofort der noch offene Betrag der Rechnung, markiert und überschreibbar.
**Runde:** R06
**Quelle:** R06 – Ä312
**Hängt ab von:** 129, 130
**Modell:** sonnet
**Prüfstufe:** –
**IDs für neue Befunde:** Ä326–Ä328

## Umfang
- Ein leeres Betragsfeld einer Zahlart bekommt beim Klick oder Fokus den offenen Rest (Summe minus schon eingetragene Zahlungen und Anzahlung), wie ihn die Kassa schon anzeigt; keine neue Rechnung in der App
- Der vorbelegte Betrag ist ganz markiert, Tippen ersetzt ihn (z. B. gegebener Barbetrag); Rückgeld wie bisher
- Ein schon befülltes Feld bleibt unverändert; bei offenem Rest 0 wird nichts vorbelegt
- Handbuch „Bezahlen“ nachziehen

## Muster übernehmen
- src/AispaStudio.Desktop/Views/PosView.axaml – Zahlungszeilen (`MoneyField` an `Amount`)
- src/AispaStudio.Desktop/ViewModels/PosViewModel.cs – offener Betrag und Rückgeld

## Voraussichtlich geänderte Dateien
- src/AispaStudio.Desktop/Views/PosView.axaml
- src/AispaStudio.Desktop/ViewModels/PosViewModel.cs
- tests/AispaStudio.Desktop.Tests/
- web/src/help/content/

## NICHT bauen
- neue Zahlarten oder Rundungsregeln

## Prompt
```
Lies CLAUDE.md, work/conventions.md, die Rundenakte work/rounds/R06-*.md und diese Slice-Datei.
Setze Slice 131 "Kassa: Betrag der Zahlart mit dem offenen Rest vorbelegen" um (diese Datei).
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
- [ ] Given eine Rechnung über 99,00 € ohne Zahlung When man in das Feld „Bar“ klickt Then steht dort markiert 99,00
- [ ] Given 50,00 € sind schon mit Karte eingetragen When man in das Feld „Bar“ klickt Then steht dort 49,00
- [ ] Given im Feld „Bar“ steht 99,00 markiert When man „100“ tippt Then zeigt die Kassa 1,00 € Rückgeld
- [ ] Given im Feld „Karte“ steht schon 20,00 When man erneut hineinklickt Then bleibt 20,00
- [ ] Manuell: An der Kassa bar, mit Karte und geteilt bezahlen, jedes Mal nur in das Feld klicken und bestätigen

## Nicht in dieser Slice
–
