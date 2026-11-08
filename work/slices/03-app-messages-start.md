# Slice 03 – Meldungen der Anmelde-, Start- und Verbindungsseiten als InfoBar

**Ziel:** Meldungen auf Anmeldung, Hauptfenster, Shell und den Verbindungsseiten tragen Symbol, Aktion und werden angesagt.
**Runde:** R01
**Quelle:** R01 – W5, W9, W18, W22, W-Muster 2 (Teil), W-Muster 3 (Teil)
**Hängt ab von:** –
**Modell:** –

## Umfang
- LoginView: vier `Border.alert` (Fehler der Anmeldung) als `ui:InfoBar Severity="Error"` (W5)
- MainWindow: Leerlauf-Warnung als `InfoBar Severity="Warning"` mit Aktion „Weiter arbeiten“, angesagt (W18)
- ShellView: Banner Offline, Abgleich, Sicherung als InfoBar mit Aktion („Verbindung prüfen“, „Sicherung öffnen“), höchstens zwei gleichzeitig, danach „n weitere“ (W22)
- JoinView, RestoreView, NetworkConnectView: `Border.alert` als InfoBar; NetworkView :34 und RestoreView :23 Rückmeldung mit `AutomationProperties.LiveSetting="Polite"` (W9)

## Muster übernehmen
- src/AispaStudio.Desktop/Controls/InfoBar.cs:23 – InfoBar mit Severity, Aktion, Schließen und Live-Region
- src/AispaStudio.Desktop/Views/CalendarView.axaml:23 – Verwendung einer InfoBar an eine ViewModel-Eigenschaft gebunden

## NICHT bauen
- W19 Ansage des Verbindungsfehlers im Hauptfenster (offene Entscheidung)
- Feldnamen der Anmeldung (Slice 06)
- Namen der Knöpfe „Erledigt“ (Slice 20)
- Entfernen des Stils `Border.alert` (Slice 05)

## Prompt
```
Lies CLAUDE.md, work/conventions.md, die Rundenakte work/rounds/R01-*.md und diese Slice-Datei.
Setze Slice 03 "Meldungen der Anmelde-, Start- und Verbindungsseiten als InfoBar" um (diese Datei).
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
- [ ] Given die Leerlauf-Abmeldung steht bevor When die Warnung erscheint Then ist sie eine InfoBar mit Severity Warning, LiveSetting Assertive und der Aktion „Weiter arbeiten“
- [ ] Given ein falsches Passwort When die Anmeldung scheitert Then erscheint eine InfoBar mit Severity Error
- [ ] Given Offline, Abgleichfehler und fehlende Sicherung zugleich When die Shell sie zeigt Then stehen zwei Banner und der Hinweis „1 weitere“
- [ ] Given LoginView, MainWindow, ShellView, JoinView, RestoreView, NetworkConnectView, NetworkView When ein Test die AXAML liest Then enthält keine `Classes="alert"`
- [ ] Manuell: Abmelden, mit falschem Passwort anmelden: Meldung mit Fehlersymbol über dem Formular
- [ ] Manuell: API stoppen (Aspire-Dashboard): Banner „Offline“ mit Knopf „Verbindung prüfen“

## Nicht in dieser Slice
Neue Texte der Meldungen
