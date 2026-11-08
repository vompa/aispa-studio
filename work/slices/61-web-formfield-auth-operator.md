# Slice 61 – Formularbaustein in Anmeldung, Einrichtung und Betreiber

**Ziel:** Anmeldung, Wiederherstellung, Einrichtung, Registrierung, Passwortänderung und die Formulare des Betreibers prüfen selbst und melden Fehler am Feld statt per Browser-Sprechblase.
**Runde:** R03
**Quelle:** R03 – B72, B80
**Hängt ab von:** –
**Modell:** sonnet
**IDs für neue Befunde:** B94–B98

## Umfang
- `auth/StandaloneAuth.tsx`, `auth/SignupForm.tsx` und die Formulare in `operator/` (Anmeldung, Spa sperren, Nummer zuordnen, Tarif) auf `FormField` mit eigener Prüfung (`noValidate`), Pflicht mit Stern
- „Passwörter stimmen nicht überein“ am zweiten Passwortfeld; „Nummer zuordnen“ meldet den Fehler am Feld statt als `alert` unter dem Formular
- Der Betreiberbereich bleibt in seinem Bundle-Budget und importiert nichts, was der Kundenbereich nicht darf

## Muster übernehmen
- web/src/components/FormField.tsx:14 – FormField

## NICHT bauen
- Neue Anmeldewege
- Änderungen an der Passwortregel

## Prompt
```
Lies CLAUDE.md, work/conventions.md, die Rundenakte work/rounds/R03-*.md und diese Slice-Datei.
Setze Slice 61 "Formularbaustein in Anmeldung, Einrichtung und Betreiber" um (diese Datei).
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
- [ ] Given die Registrierung mit zwei verschiedenen Passwörtern When abgeschickt wird Then steht „Passwörter stimmen nicht überein“ am zweiten Feld und ist per `aria-describedby` verbunden
- [ ] Given „Nummer zuordnen“ ohne Nummer When abgeschickt wird Then steht der Fehler am Feld und der Fokus liegt im Feld
- [ ] Given die Formulare unter `auth/` und `operator/` When ein Prüftest `required` ohne `noValidate` am Formular sucht Then gibt es keinen Treffer
- [ ] Manuell: Einrichtung im Einzelplatz: zwei verschiedene Passwörter eingeben: der Fehler steht am zweiten Feld, keine Browser-Sprechblase

## Nicht in dieser Slice
–
