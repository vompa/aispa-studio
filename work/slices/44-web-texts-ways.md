# Slice 44 – Texte und Auswege im Web

**Ziel:** Leer- und Fehlerzustände nennen einen Weg, Knöpfe tragen Verben, Exporte zeigen Zustand und Fehler.
**Runde:** R02
**Quelle:** R02 – B44, B50, B54, B55, B60, B66, B67
**Hängt ab von:** –
**Modell:** sonnet

## Umfang
- Leerzustand Kasse mit Weg und Link (B44); „OK“ → „Schließen“ (B50); „+ Termin“ usw. → Verben (B67)
- Exporte mit „Wird erstellt …“ und Fehlerweg, Lohn-Export mit derselben Prüfung wie DATEV (B54); Monatsauswahl ohne `type="month"` (B55)
- Fehlerzustände im Kundenbereich mit „Erneut versuchen“ (B60); allgemeine Fehlertexte mit Ursache und Weg (B66)

## Muster übernehmen
–

## NICHT bauen
–

## Prompt
```
Lies CLAUDE.md, work/conventions.md, die Rundenakte work/rounds/R02-*.md und diese Slice-Datei.
Setze Slice 44 "Texte und Auswege im Web" um (diese Datei).
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
- [ ] Given keine Kasse eingerichtet When die Kassa öffnet Then steht ein Link zu „Stammdaten, Zahlungen“
- [ ] Given die Quelltexte When ein Prüftest Beschriftungen mit „+ “ am Anfang oder „OK“ als Knopftext sucht Then gibt es keinen Treffer
- [ ] Given ein fehlgeschlagener Export When er erscheint Then steht ein Fehlertext mit Weg statt eines leeren Downloads
- [ ] Manuell: Lohn › Export ohne Personalnummern: Fehlermeldung mit Weg statt leerer Datei

## Nicht in dieser Slice
–
