# Übergabe: Thread „Windows-App ausbauen“ (Stand 2026-10-20, 04:00)

Alles ist auf `main` gemergt. Es gibt keinen offenen PR und keinen halbfertigen Branch. Der Branch `claude/project-thread-h5vria` ist auf dem Stand von `main` (nur Merge-Commits) und kann gelöscht werden.

## Fertig in diesem Abschnitt (alle auf main)

| PR | Inhalt |
|---|---|
| #139 | DATEV-BU-Schlüssel je Land der Kassa; fehlende Schlüssel sperren den Export mit Hinweis |
| #145 | Verwaltung in der App findet die Team-Oberfläche auch beim Start über Aspire (`AISPA_TEAM_WEB_URL`, `GET /api/system/team-web`, AppHost-Ressource „windows-app“ mit `WithExplicitStart`); Web-Kopfzeile mit Konto-Menü; kurze Termine im Web-Kalender einzeilig |
| #147 | „KI und Hilfe“ rechts in Windows-App und Web: Lotus-Knopf unten rechts oder Strg+Leertaste, Erklärung zur Seite oben, KI-Assistent darunter, ausblendbar, Zustand gemerkt (`DesktopSettings.CompanionOpen`, `localStorage aispa.companion.open`) |
| #152 | Leiste mit dem Handbuch (#149) verbunden: Erklärung aus `seite-<seite>`, Frage mit `Page`/`Surface`, App zeigt besten Hilfe-Treffer mit `HelpSectionView`; reparierte den Build von main nach #147 + #149 |
| #170 | Vorschläge des Assistenten in der App als Knöpfe: Buchen … (Kunde suchen, Termin buchen), Kunde öffnen, Im Kalender zeigen; Handbuch `ki-assistent-fragen.md` mit Bild `ki-und-hilfe-buchen.png` |

## Wichtige Stellen im Code

- App: `src/AispaStudio.Desktop/ViewModels/CompanionViewModel.cs` und `CompanionViewModel.Suggestions.cs`; Knopf, Leiste und Strg+Leertaste (Tunnel-Handler) in `Views/ShellView.axaml(.cs)`; Verdrahtung in `ShellViewModel` (`Companion`, `PageKey`, `ShowAppointment`).
- Web: `web/src/companion/Companion.tsx` (Seitenkennung = Seitenkennung der Oberfläche kleingeschrieben, z. B. `masterdata`), `web/src/assistant/AssistantPanel.tsx` (Prop `page`, `surface: 'verwaltung'`).
- Tests: `tests/AispaStudio.Desktop.Tests/CompanionTests.cs`, Ansichtstests `KI_und_Hilfe_rechts`, `KI_und_Hilfe_bucht_einen_Vorschlag` in `ViewTests.cs`; `web/src/companion/Companion.test.tsx`.

## Offen (Reihenfolge nach Nutzen)

1. Neuen Kunden direkt aus der Leiste anlegen; bisher nur Suche (`CustomerSearchViewModel`).
2. Bild `web/src/help/bilder/web-calendar-assistent.png` zeigt noch den alten Assistent-Knopf der Team-Oberfläche: `web/scripts/hilfe-bilder.mjs` laufen lassen.
3. Der Lotus-Knopf (48 px, Abstand 20) liegt auf manchen Seiten über der rechten unteren Ecke des Inhalts (z. B. Kundenliste). Falls störend: Seiten unten rechts etwas Abstand geben oder den Knopf kleiner machen.
4. Verwaltung über Aspire: beim Auftraggeber war die Ursache, warum die Weiterleitung API → Vite nicht ankam, nicht geklärt; #145 umgeht sie. Falls die Verwaltung dort weiter nicht öffnet, zeigt die Fehlermeldung jeden Versuch mit Adresse und Antwort.

## Hinweise

- CI läuft (Actions-Budget 60 $): auf grüne CI warten, nur ohne CI mergen, wenn die Jobs wirklich nicht starten.
- Strg+K (KI-Anfrage) und F1 (Tastenhilfe) sind im Kalender belegt, deshalb Strg+Leertaste für die Leiste.
- Der Thread „Kalender aus Sicht der Rezeption“ baut die Kundenkartei (`CustomersViewModel.Card.cs`); Kalenderdateien gehören ihm.
- Vitest zeigt manchmal veraltete Ergebnisse (z. B. „Bild fehlt“): `web/node_modules/.vitest-cache` und `.vite` löschen.
- `scripts/hilfe-bilder-verkleinern.py` verkleinert alle Bilder; nur die eigenen committen.
