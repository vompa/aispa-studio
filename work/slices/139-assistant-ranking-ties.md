# Slice 139 – Assistent: Ranglisten ohne Wiederholung und mit Gleichstand

**Ziel:** Ranglisten des Assistenten (etwa „Top-Behandlungen“) wiederholen die Liste nicht im Kopfsatz und zeigen keine Reihenfolge, wo Werte gleich sind.
**Runde:** R07
**Quelle:** R07 – Ä346
**Hängt ab von:** –
**Modell:** sonnet
**Prüfstufe:** –
**IDs für neue Befunde:** Ä361–Ä363

## Umfang
- Kopfsatz fasst zusammen statt aufzuzählen, z. B. „7 Behandlungen mit je 2 Terminen, danach 3 mit je 1 (01.11.–03.11.2026)“
- Gleiche Werte bekommen keine aufsteigenden Platznummern: Einträge gruppiert nach Wert bzw. gleicher Rang
- Gilt für alle Ranglisten-Antworten (Behandlungen, Artikel, Umsatz je Person …); per Suche finden
- Hilfe zum Assistenten nachziehen, falls dort Beispiele stehen

## Muster übernehmen
- src/AispaStudio.Api/Assistant/Answers/ReportAnswers.cs
- Listen-Antworten aus Slice 134

## Voraussichtlich geänderte Dateien
- src/AispaStudio.Api/Assistant/
- tests/AispaStudio.Api.Tests/
- web/src/help/content/

## NICHT bauen
- neue Ranglisten oder Kennzahlen

## Prompt
```
Lies CLAUDE.md, work/conventions.md, die Rundenakte work/rounds/R07-*.md und diese Slice-Datei.
Setze Slice 139 "Assistent: Ranglisten ohne Wiederholung und mit Gleichstand" um (diese Datei).
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
- [ ] Given 7 Behandlungen haben je 2 Termine und 3 je 1 When jemand „top behandlungen diesen monat“ fragt Then nennt der Kopfsatz die Gruppen und keinen Namen, und die Liste trägt keine Plätze 1 bis 7
- [ ] Given drei Behandlungen haben 5, 3 und 1 Termine When dieselbe Frage kommt Then stehen sie mit Platz 1, 2 und 3
- [ ] Manuell: Als Inhaberin „top behandlungen diesen monat“ und „welche artikel verkaufen sich am besten“ fragen

## Nicht in dieser Slice
–
