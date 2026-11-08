# Slice 90 – Änderungsverlauf für Termine

**Ziel:** Jeder Termin zeigt im Termin-Panel seinen Verlauf: angelegt über welchen Kanal, von wem und wann, jede Änderung mit altem und neuem Wert, Storno und Nicht erschienen; Grundlage ist ein allgemeiner Verlauf aus dem bestehenden Änderungs-Trigger.
**Runde:** R04
**Quelle:** R04 – Ä184
**Hängt ab von:** –
**Modell:** opus
**IDs für neue Befunde:** Ä185–Ä187

## Umfang
- ADR 0082 „Änderungsverlauf über den Änderungs-Trigger“ mit den Rulings zu Ä184 (3 Jahre, Löschung mit dem Gast, Sichtbarkeit, `health` ohne Werte, Umfang in zwei Schritten)
- Trigger aus `Sync/ChangeLog.cs` erweitert oder daneben: Verlaufstabelle mit Tabelle, Schlüssel, Mandant, Art, Zeitpunkt, Person (Benutzer-Id aus einer Transaktionseinstellung, die die API je Anfrage setzt), Kanal (Team, Online, SMS, Mail, Treatwell, KI, Hintergrundjob) sowie alten und neuen Werten der geänderten Spalten; für Tabellen im Schema `health` nur Art, Person und Zeit
- API setzt Person und Kanal je Anfrage und in Hintergrundjobs (`TenantContext`), Abgleich-Einspielungen bleiben als solche erkennbar
- Endpunkt Verlauf eines Termins (Recht wie der Termin selbst), Texte für Menschen: „Angelegt per SMS am 3.11. um 10:12“, „Verschoben von Lena: 14:00 → 15:30“, „Storniert von …“
- Abschnitt „Verlauf“ im Termin-Panel der Windows-App (eingeklappt, neueste oben)
- Löschjob: Einträge älter als 3 Jahre; beim Löschen oder Anonymisieren eines Gastes fällt der Verlauf seiner Datensätze mit
- Beispieldaten erzeugen einen plausiblen Verlauf (Bulk und Services gleich, `DemoBulkTests`); Hilfe zum Verlauf

## Muster übernehmen
- src/AispaStudio.Api/Sync/ChangeLog.cs – Trigger an allen Fachtabellen
- src/AispaStudio.Api/Scheduling/Entities.cs – Termin mit `Source`, `CreatedAt`, `UpdatedAt`
- docs/entscheidungen/vorlage.md – ADR-Vorlage

## Voraussichtlich geänderte Dateien
- src/AispaStudio.Api/Sync/
- src/AispaStudio.Api/Scheduling/
- src/AispaStudio.Desktop/Views/
- src/AispaStudio.Desktop/ViewModels/AppointmentViewModel.cs
- docs/entscheidungen/
- web/src/help/content/

## NICHT bauen
- Verlauf für andere Masken als Termine (Slice 91)
- Inhalte von Gesundheitsdaten im Verlauf
- Wiederherstellen alter Stände aus dem Verlauf
- Verlauf für Kassa-Belege (RKSV/TSE bleibt die Quelle)

## Prompt
```
Lies CLAUDE.md, work/conventions.md, die Rundenakte work/rounds/R04-*.md und diese Slice-Datei.
Setze Slice 90 "Änderungsverlauf für Termine" um (diese Datei).
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
- [ ] Given ein Termin, den die Terminannahme aus einer SMS angelegt hat When die Rezeption den Verlauf öffnet Then steht „Angelegt per SMS“ mit Datum und Uhrzeit
- [ ] Given Lena verschiebt einen Termin von 14:00 auf 15:30 When der Verlauf geöffnet wird Then steht „Verschoben von Lena: 14:00 → 15:30“ mit Datum und Uhrzeit
- [ ] Given ein stornierter Termin When der Verlauf geöffnet wird Then nennt er, wer storniert hat und wann
- [ ] Given eine Änderung an einer Tabelle im Schema health When sie protokolliert wird Then enthält der Eintrag keine Werte
- [ ] Given ein Verlaufseintrag älter als 3 Jahre When der Löschjob läuft Then ist er weg
- [ ] Given ein Gast wird gelöscht When die Löschung läuft Then ist der Verlauf seiner Termine weg
- [ ] Manuell: Termin anlegen, verschieben, Behandlung ändern, stornieren: der Verlauf im Termin-Panel zeigt alle vier Schritte mit Person und Uhrzeit

## Nicht in dieser Slice
–
