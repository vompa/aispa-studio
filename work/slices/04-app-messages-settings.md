# Slice 04 – Meldungen und Primärknöpfe in Sicherung, Abgleich und Geräten

**Ziel:** Sicherung, Abgleich und Geräte zeigen Warnungen in Warnfarbe mit Symbol, sagen Statusmeldungen an und haben je Seite genau einen Primärknopf.
**Runde:** R01
**Quelle:** R01 – W10, W11, W13, W16, W17, W-Muster 2, 3, 4, 13 (Teil)
**Hängt ab von:** –
**Modell:** –

## Umfang
- BackupView: Warnung „Wiederherstellungsschlüssel notieren“ als `InfoBar Severity="Warning"` mit Aktion „Verstanden“ statt Primärknopf „Notiert“; Ordnerfeld :59 mit Namen (W10, W11)
- SyncView: Warnungen :26-28 und :79-86 als InfoBar, „Notiert“ → Aktion „Verstanden“, Felder :72 und :76 mit Beschriftung über `ui:FormField` (W13)
- DevicesView: Warnung :59-62 und Fehler :165-167 als InfoBar; „Diesen PC als Empfangsgerät anmelden“ sekundär (W16, W17)
- Statuszeilen Backup :45, Sync :94, Devices :38, :92, :100 mit `LiveSetting="Polite"` (W-Muster 3)
- AdminView: `Border.alert` als InfoBar

## Muster übernehmen
- src/AispaStudio.Desktop/Controls/InfoBar.cs:23 – InfoBar
- src/AispaStudio.Desktop/Controls/FormField.cs:27 – Feld mit Beschriftung, Hilfe, Pflicht und Fehler
- src/AispaStudio.Desktop/Views/CustomerContactForm.axaml:25 – FormField im Einsatz

## NICHT bauen
- W12 Ordner wählen und Speichern/Sichern trennen (dokumentierte Abweichung)
- W15 Beschriftungen der Geräteseite auf FormField (dokumentierte Abweichung)
- W8 Technikdeutsch (offene Entscheidung)
- Maße der Karten (offene Entscheidung zu W-Muster 9)

## Prompt
```
Lies CLAUDE.md, work/conventions.md, die Rundenakte work/rounds/R01-*.md und diese Slice-Datei.
Setze Slice 04 "Meldungen und Primärknöpfe in Sicherung, Abgleich und Geräten" um (diese Datei).
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
- [ ] Given die Sicherungsseite mit offener Schlüssel-Warnung When sie gerendert wird Then ist die Warnung eine InfoBar mit Severity Warning und kein Text darin nutzt `CritBrush`
- [ ] Given Sicherung, Abgleich und Geräte When ein Headless-Test die sichtbaren Knöpfe mit Klasse `primary` je Seite zählt Then ist es jeweils genau einer
- [ ] Given eine Sicherung ist gelaufen When die Statuszeile sich ändert Then hat sie LiveSetting Polite
- [ ] Given die Warnungen in Sicherung und Abgleich When man sie bestätigt Then heißt die Aktion „Verstanden“
- [ ] Manuell: Einstellungen › Sicherung öffnen: die Schlüssel-Warnung erscheint in Warnfarbe mit Symbol, nicht in roter Schrift, und es gibt nur einen hervorgehobenen Knopf

## Nicht in dieser Slice
Ablauf der Sicherung selbst
