# Slice 81 – Prüfschritt für Eingaben am Rundenende

**Ziel:** Ein Vorschlag zeigt, ob und wie eine systematische Prüfung von Eingaben und Formularen am Ende jeder Runde läuft.
**Runde:** R03
**Quelle:** R03 – Ä98
**Hängt ab von:** –
**Modell:** sonnet
**IDs für neue Befunde:** Ä143–Ä145

## Umfang
- Die Skills `input-validation` und `form-validation` aus dem öffentlichen Verzeichnis lesen (nicht installieren): was sie wirklich tun, von wem sie stammen, ob sie zu den Bausteinen `FormField`, `MoneyField`, den Prüflisten von fluent2-web und den axe-Läufen des Projekts passen
- Vorschlag für einen festen Prüfschritt im Rundenabschluss (welche Prüfung, wann, mit welchem Ergebnisformat) in `docs/konzepte/eingabepruefung-rundenabschluss.md`

## Muster übernehmen
- docs/konzepte/ – bestehende Konzeptdokumente
- .claude/skills/fluent2-web/ – vorhandene Prüflisten

## NICHT bauen
- Installation fremder Skills
- Änderung des Rundenabschlusses im Plugin

## Prompt
```
Lies CLAUDE.md, work/conventions.md, die Rundenakte work/rounds/R03-*.md und diese Slice-Datei.
Setze Slice 81 "Prüfschritt für Eingaben am Rundenende" um (diese Datei).
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
- [ ] Given `docs/konzepte/eingabepruefung-rundenabschluss.md` When man es liest Then stehen Herkunft und Inhalt beider Skills, der Abgleich mit den vorhandenen Bausteinen und ein Vorschlag mit Aufwand da
- [ ] Manuell: Das Dokument lesen: die Frage „lohnt sich ein fester Schritt am Rundenende“ ist beantwortet

## Nicht in dieser Slice
–
