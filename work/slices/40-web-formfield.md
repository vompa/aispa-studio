# Slice 40 – Gemeinsamer Formularbaustein im Web

**Ziel:** Formulare der Verwaltung nutzen einen Baustein für Beschriftung, Hilfe, Fehler und Sprung zum ersten Fehler.
**Runde:** R02
**Quelle:** R02 – B9
**Hängt ab von:** –
**Modell:** sonnet

## Umfang
- `web/src/components/FormField.tsx` und Hook `useFocusFirstInvalid(ref)` aus den Kopien in BusinessInfoPage, PromotionsPage, Closures und Lager zusammenführen; Aufrufer umstellen

## Muster übernehmen
–

## NICHT bauen
–

## Prompt
```
Lies CLAUDE.md, work/conventions.md, die Rundenakte work/rounds/R02-*.md und diese Slice-Datei.
Setze Slice 40 "Gemeinsamer Formularbaustein im Web" um (diese Datei).
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
- [ ] Given BusinessInfoPage und PromotionsPage When ein Prüftest eigene `Row`/`Group`-Definitionen sucht Then gibt es keine mehr
- [ ] Given ein Formular mit zwei ungültigen Feldern When gespeichert wird Then liegt der Fokus im ersten ungültigen Feld
- [ ] Manuell: Aktion anlegen ohne Namen, speichern: Fokus springt ins Feld Name, Fehlertext darunter

## Nicht in dieser Slice
–
