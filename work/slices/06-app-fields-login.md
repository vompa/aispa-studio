# Slice 06 – Feldnamen und Anrede in Anmeldung, Einrichtung und Verbindung

**Ziel:** Jedes Feld der Anmeldung, Einrichtung und Verbindungsseiten hat eine echte Beschriftung, und die Texte siezen.
**Runde:** R01
**Quelle:** R01 – W1, W2, W6, W7, W21, W-Muster 1 (Teil), W-Muster 11
**Hängt ab von:** 03
**Modell:** –

## Umfang
- LoginView: 18 Felder in `ui:FormField` mit Label und Hilfetext (z. B. „mindestens 8 Zeichen“ als Hilfe statt im Platzhalter), Pflichtfelder gekennzeichnet (W1)
- PIN :86, Land :151, Testkonto :239 benannt; Namens-Scan um die Anmeldeseite erweitern (W2)
- JoinView, RestoreView, NetworkConnectView: Felder „Verbindung zur zentralen Datenbank“, „Schlüsselcode“, „Adresse“, „Verbindungscode“ mit kurzem Hilfetext (W6); „Verbinden“ als `primary` (W7)
- ShellView: PIN-Feld im Konto-Flyout als FormField „PIN“, Satz :97 in Sie (W21)
- Anrede „du“ in LoginView (:60, :153, :170) und LoginViewModel in „Sie“ oder unpersönlich (W-Muster 11)

## Muster übernehmen
- src/AispaStudio.Desktop/Controls/FormField.cs:27 – FormField
- src/AispaStudio.Desktop/Views/CustomerContactForm.axaml:25 – FormField im Einsatz
- tests/AispaStudio.Desktop.Tests/ViewTests.FocusOrder.cs:210 – Namens-Scan (`HasUnreadableName`)

## NICHT bauen
- W3 Höhen und Abstände der Anmeldung (offene Entscheidung)
- W4 Bedingungen der Standardknöpfe (offene Entscheidung)
- Entfernen der Namensableitung aus Platzhaltern (Slice 11)

## Prompt
```
Lies CLAUDE.md, work/conventions.md, die Rundenakte work/rounds/R01-*.md und diese Slice-Datei.
Setze Slice 06 "Feldnamen und Anrede in Anmeldung, Einrichtung und Verbindung" um (diese Datei).
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
- [ ] Given die Anmeldeseite in den Zuständen Anmeldung, Wiederherstellung und Einrichtung When der Namens-Scan läuft Then hat jedes Feld einen Namen, der nicht aus dem Platzhalter stammt
- [ ] Given LoginView, ShellView und LoginViewModel When ein Test die Oberflächentexte nach den Wörtern „du“, „dein“, „deinem“, „Du“ durchsucht Then gibt es 0 Treffer
- [ ] Given NetworkConnectView When sie gerendert wird Then hat „Verbinden“ die Klasse `primary`
- [ ] Manuell: Abmelden: über jedem Feld der Anmeldung steht eine Beschriftung; „Einrichtung“ öffnen: die Texte siezen

## Nicht in dieser Slice
Neuer Aufbau der Anmeldung
