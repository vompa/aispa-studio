# Slice 60 – Formularbaustein in Stammdaten und Personal

**Ziel:** Die Reiter der Stammdaten und das Personal nutzen den Formularbaustein: Beschriftung, Hilfetext und Fehler hängen am Feld, Speichern springt zum ersten Fehler.
**Runde:** R03
**Quelle:** R03 – B71, B76
**Hängt ab von:** –
**Modell:** sonnet
**IDs für neue Befunde:** B89–B93

## Umfang
- Reiter der Stammdaten (`admin/*Tab.tsx`: Standorte, Team, Behandlungen, Postfach, Firmendaten, Zahlungen, Kassa, KI und weitere) und `admin/StaffPage.tsx` auf `FormField`/`FormGroup` und `useFocusFirstInvalid`; Fehler am Feld statt als Sammelmeldung unter der Aktionsleiste
- Jeder Knopf, der dadurch in einem `<form>` steht, trägt `type` (B76)
- Zeigt sich die Slice als zu groß für eine Sitzung, die restlichen Reiter als Befund festhalten statt halb umzustellen

## Muster übernehmen
- web/src/components/FormField.tsx:14 – FormField, :57 – FormGroup
- web/src/components/useFocusFirstInvalid.ts – Sprung zum ersten Fehler
- web/src/businessinfo/BusinessInfoPage.tsx – erster Nutzer des Bausteins (Slice 40)

## NICHT bauen
- Neue Felder
- Umgestaltung der Reiter

## Prompt
```
Lies CLAUDE.md, work/conventions.md, die Rundenakte work/rounds/R03-*.md und diese Slice-Datei.
Setze Slice 60 "Formularbaustein in Stammdaten und Personal" um (diese Datei).
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
- [ ] Given die Dateien unter `admin/` When ein Prüftest Eingabefelder in einem umhüllenden `<label>` ohne `FormField` sucht Then gibt es keinen Treffer
- [ ] Given ein Reiter mit leerem Pflichtfeld When gespeichert wird Then steht der Fehler am Feld, ist per `aria-describedby` verbunden und der Fokus liegt im Feld
- [ ] Given die Dateien unter `admin/` When ein Prüftest `<button` ohne `type` innerhalb eines `<form>` sucht Then gibt es keinen Treffer
- [ ] Manuell: Stammdaten › Standorte: den Namen leeren und speichern: Fehler am Feld, Fokus im Feld

## Nicht in dieser Slice
–
