# Slice 140 – Neue Kundin: Name und Telefon zuerst, Dubletten beim Tippen

**Ziel:** Eine neue Kundin ist an der Theke in drei Feldern angelegt, Doppelanlagen fallen beim Tippen auf, und die Buchung schließt direkt an.
**Runde:** R07
**Quelle:** R07 – Ä348
**Hängt ab von:** –
**Modell:** sonnet
**Prüfstufe:** –
**IDs für neue Befunde:** Ä364–Ä366

## Umfang
- Reihenfolge: Vorname | Nachname, darunter Telefon | E-Mail; Fokus beim Öffnen im Vornamen
- „Weitere Angaben“ zugeklappt: Anrede, Titel, Rufname im Haus, Notiz
- Telefon in jeder Schreibweise; nach dem Verlassen im Format „+43 664 1234567“, ohne Vorwahl +43; die Normalisierung macht die API (vorhandene prüfen, sonst dort ergänzen)
- Beim Tippen von Nachname oder Telefon: „Schon vorhanden?“ mit Treffern aus der Kundensuche der API und „Diese Kundin nehmen“
- Leere Suche links bietet „Neue Kundin ‚<Suchtext>‘ anlegen“ mit vorbelegtem Namen
- Knöpfe: „Anlegen und Termin buchen“ (Hauptknopf, öffnet die Buchung mit dem Gast), „Anlegen“, „Abbrechen“; Enter legt an, Esc bricht ab; gesperrtes „Anlegen“ nennt den Grund
- Hinweistext kürzen auf „Nur ein Name ist Pflicht.“; Handbuch „Neue Kundin oder neuer Kunde“ und Bilder nachziehen

## Muster übernehmen
- src/AispaStudio.Desktop/Views/CustomerContactForm.axaml
- src/AispaStudio.Desktop/ViewModels/CustomerContactFormViewModel.cs
- src/AispaStudio.Desktop/ViewModels/CustomerSearchViewModel.cs
- src/AispaStudio.Desktop/ViewModels/CustomersViewModel.Booking.cs
- docs/design.md und docs/gestaltung/masken.md (Kurz-Checkliste)
- .claude/skills/fluent2-avalonia (foundations.md, data-display.md, accessibility.md)

## Voraussichtlich geänderte Dateien
- src/AispaStudio.Desktop/Views/CustomersView.axaml
- src/AispaStudio.Desktop/Views/CustomerContactForm.axaml
- src/AispaStudio.Desktop/ViewModels/CustomerContactFormViewModel.cs
- src/AispaStudio.Desktop/ViewModels/CustomersViewModel.cs
- src/AispaStudio.Api/Customers/
- tests/AispaStudio.Desktop.Tests/
- tests/AispaStudio.Api.Tests/
- web/src/help/content/
- web/src/help/images/app/

## NICHT bauen
- Zusammenführen von Dubletten
- neue Kundenfelder

## Prompt
```
Lies CLAUDE.md, work/conventions.md, die Rundenakte work/rounds/R07-*.md und diese Slice-Datei.
Setze Slice 140 "Neue Kundin: Name und Telefon zuerst, Dubletten beim Tippen" um (diese Datei).
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
- [ ] Given die Maske „Neue Kundin“ öffnet When man nichts klickt Then steht der Fokus im Vornamen und Anrede, Titel, Rufname und Notiz sind zugeklappt
- [ ] Given es gibt „Huber, Anna“ mit +43 664 1234567 When man im Nachnamen „Huber“ tippt Then erscheint „Schon vorhanden?“ mit „Huber, Anna“
- [ ] Given man tippt „0664 1234567“ ins Telefon When man das Feld verlässt und anlegt Then speichert die API „+43 664 1234567“
- [ ] Given die Suche „Gruber“ findet nichts When man „Neue Kundin ‚Gruber‘ anlegen“ wählt Then steht „Gruber“ im Nachnamen
- [ ] Given Vor- und Nachname sind ausgefüllt When man „Anlegen und Termin buchen“ wählt Then ist der Gast angelegt und die Buchung mit ihm geöffnet
- [ ] Manuell: Als Rezeption eine neue Kundin nur per Tastatur anlegen (Vorname, Tab, Nachname, Tab, Telefon, Enter), eine Dublette auslösen, aus der leeren Suche anlegen, „Anlegen und Termin buchen“

## Nicht in dieser Slice
–
