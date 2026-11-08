# Slice 122 – Muster für die Masken festhalten und absichern

**Ziel:** Das Vorgehen je Maske ist dokumentiert, und Tests halten die Regeln: kein Datenbankzugriff in der Windows-App, kein blockierender Aufruf in den ViewModels.
**Runde:** R06
**Quelle:** R06 – Ä284
**Hängt ab von:** 119, 120, 121
**Modell:** sonnet
**IDs für neue Befunde:** –

## Umfang
- docs/architektur.md: Abschnitt „Client gegen API“ mit dem Vorgehen je Maske (Endpunkt, Client, ViewModel async, Lade- und Fehlerzustand, Konflikt, Live-Update)
- Architekturtest: Die Windows-App referenziert kein EF Core und kein Npgsql
- Architekturtest: Kein `.Wait()`, `.Result` auf Tasks oder `GetAwaiter().GetResult()` unter `ViewModels/`; die zwei bekannten Stellen außerhalb (`App.axaml.cs` beim Beenden, `ScheduledBackup`) sind begründet
- Stand und Übergabe nachziehen; Azure-Rollout als offenen Punkt mit Freigabe durch den Auftraggeber nennen

## Muster übernehmen
- docs/architektur.md
- tests/AispaStudio.Desktop.Tests/DesignTokenTests.cs – Muster für Quelltext-Scans

## Voraussichtlich geänderte Dateien
- docs/architektur.md
- tests/AispaStudio.Desktop.Tests/
- docs/stand-und-offene-punkte.md
- docs/uebergabe.md

## NICHT bauen
- Azure-Rollout
- Umbau weiterer Masken

## Prompt
```
Lies CLAUDE.md, work/conventions.md, die Rundenakte work/rounds/R06-*.md und diese Slice-Datei.
Setze Slice 122 "Muster für die Masken festhalten und absichern" um (diese Datei).
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
- [ ] Given ein ViewModel mit `.Wait()` When der Architekturtest läuft Then ist er rot und nennt die Datei
- [ ] Given die Projektdatei der Windows-App mit einem Npgsql-Verweis When der Architekturtest läuft Then ist er rot


## Nicht in dieser Slice
–
