# 0103. Claude-Schlüssel je Betrieb in der App

- **Status:** angenommen
- **Datum:** 26.11.2026
- **Entschieden von:** Auftraggeber (Plan zu T-111, T-098 Punkt 9), Umsetzung Claude, autonom nach ADR 0019

## Kontext

Bisher las die API den Claude-Schlüssel nur aus der Konfiguration (`Ai:Claude:ApiKey`), und nur beim Start: Ob Claude, das lokale Modell oder die festen Regeln arbeiten, legte die Registrierung der Dienste einmal fest. Ein Spa im Einzelplatz musste dafür User-Secrets oder Umgebungsvariablen setzen und die App neu starten; die Maske „KI“ zeigte nur „Eingeschaltet“ oder „Aus“. T-098 Punkt 9 verlangt, dass die Inhaberin den Schlüssel in der Windows-App eintragen, prüfen und entfernen kann.

## Optionen

| Option | Bewertung |
| --- | --- |
| A: weiter nur Konfiguration | sicher und einfach, aber im Einzelplatz für Laien kaum machbar und nur mit Neustart |
| B: ein Schlüssel für den ganzen Server in der Datenbank (`KeyStoreDbContext`) | ein Eintrag genügt, aber ein Spa könnte im Mehrmandantenbetrieb den Schlüssel aller ändern; der Schlüsselspeicher ist für die Schlüssel von Data Protection da |
| C: Schlüssel je Mandant, verschlüsselt mit Data Protection, Konfiguration hat Vorrang | gewählt |

## Entscheidung

- Der Schlüssel gilt **je Betrieb (Mandant)** und liegt verschlüsselt (ASP.NET Data Protection, Zweck `Ai.Claude.ApiKey`, an den Mandanten gebunden) in der mandantengetrennten Tabelle `ClaudeKeySetting` des `AispaDbContext`.
- **Vorrang:** Ist `Ai:Claude:ApiKey` in der Konfiguration gesetzt, gilt er für alle Betriebe. Die App zeigt dann „Aus der Konfiguration gesetzt“, Speichern und Entfernen antworten mit 409. `Ai:Provider=RuleBased` schaltet Claude weiter ganz ab, auch mit Schlüssel.
- **Wirkung ohne Neustart:** `ClaudeKeyStore` liefert den Schlüssel je Mandant (eine Minute im Speicher, Speichern und Entfernen leeren den Eintrag sofort). Welche Umsetzung ein Dienst bekommt (Claude, lokales Modell, feste Regeln oder „nicht verfügbar“), entscheidet sich je Scope, also je Anfrage oder je Hintergrundauftrag nach `TenantContext.Use`. Ohne Schlüssel gelten genau die Umsetzungen, die früher ohne Claude registriert waren.
- **Endpunkte** unter `/api/system/ai/claude-key` (GET Stand mit Maske, PUT speichern, POST `/check` prüfen, DELETE entfernen), alle mit `AccessRight.ManageOrganization` (Inhaber und Geschäftsführung). Der Schlüssel kommt nie zurück und wird nie protokolliert.

## Begründung

Der Schlüssel gehört zum Vertrag des Betriebs mit Anthropic, nicht zum Server: Im Mehrmandantenbetrieb zahlt jedes Spa seine eigene Nutzung. Data Protection verschlüsselt schon die Geheimnisse der Spa-Postfächer; derselbe Weg braucht keine neue Technik. Der Vorrang der Konfiguration erhält den bisherigen Betrieb: Wer den Schlüssel heute als Server-Einstellung setzt, merkt keinen Unterschied. Die Regel „keine Geheimnisse ins Repository“ bleibt gültig; der Schlüssel steht nur verschlüsselt in der Datenbank des Betriebs.

## Folgen und Stand

- Code: `src/AispaStudio.Api/MailIntake/Assistant/ClaudeKeys.cs` (Entity, Speicher, Zugriff je Scope, Clients je Schlüssel), `ClaudeKeyEndpoints.cs`, Registrierung in `MailIntakeSetup.AddMailIntake` (`AddClaudeOr`), Maske `AiView.axaml`. Hilfe: `ki-schluessel-eintragen`.
- Die Dienste mit Claude sind jetzt je Scope registriert statt als Singleton; ein Client je Schlüssel bleibt für die Laufzeit der Anwendung im Speicher.
- Der Leser für Papierbögen setzt vor dem Lesen den Mandanten der Kundin, weil der Hintergrundauftrag sonst keinen Mandanten kennt.
- Mehrere Knoten (Rollen Api und Worker, ADR 0087) sehen eine Änderung spätestens nach einer Minute.
- Geht der Schlüsselring von Data Protection verloren, zählt der gespeicherte Schlüssel als nicht gesetzt; die Inhaberin trägt ihn neu ein.

## Quellen

- T-111, T-098 Punkt 9 (Aufgabenliste), Abschnitt T-111 im [PR-Protokoll](../umsetzung/pr-protokoll.md)
- ADR 0057 (lokales Modell), ADR 0041 (Einzelplatz), ADR 0087 (Rollen Api und Worker)
