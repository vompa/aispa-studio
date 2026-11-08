# 0078. Windows-Paket nur für win-x64, selbstständig und ohne Symbole der Natives

- **Status:** angenommen
- **Datum:** 23.10.2026
- **Entschieden von:** Claude, autonom nach ADR 0019 (Auftrag T-011 vom Auftraggeber: Auslieferung schmaler)

## Kontext

Der Build-Ordner der Windows-App war 590 MB groß. Ursache: SkiaSharp und HarfBuzzSharp liefern ihre Natives für 17 Plattformen mit (Linux, macOS, Android, drei Windows-Architekturen) und dazu je Windows-Architektur eine `.pdb` (zusammen rund 100 MB je Architektur). Die CI veröffentlichte schon `-r win-x64 --self-contained` (222 MB); die `.pdb` der Natives lagen aber weiter im Paket. Zielgeräte sind Windows-PCs am Empfang ([ADR 0059](0059-zum-start-nur-die-windows-app.md)), keine ARM- oder Linux-Geräte. Die Messung steht in [systemanforderungen.md](../produkt/systemanforderungen.md). Container-/Paketgrößen über 200 MB sind ausdrücklich als groß zu kennzeichnen.

## Optionen

| Option | Bewertung |
| --- | --- |
| Ausgangsstand (alle Plattformen) | 579 MB, 17 Fremdplattformen; verworfen |
| Nur win-x64, abhängig vom installierten .NET | 45 MB ohne `.pdb`; Kundinnen müssten .NET 10 installieren, Installation und Updates liegen bei ihnen; verworfen |
| **Nur win-x64, selbstständig, ohne `.pdb` der Natives** | 122 MB entpackt, 56 MB ZIP; keine Voraussetzung auf dem PC; gewählt |
| Eine Datei (komprimiert) | 61 MB, aber im ZIP nur 4 MB kleiner; entpackt Natives beim Start nach `%TEMP%`, mehr Virenscanner-Aufmerksamkeit; verworfen |
| Trimmen | Build scheitert an Reflexion (`System.Text.Json` in `AispaStudio.Contracts`, dazu SignalR und Bindungen); hohes Laufzeitrisiko für wenig Gewinn; verworfen |
| ReadyToRun | 23 MB größer, Startzeit im Messrauschen; verworfen |

## Entscheidung

`AispaStudio.Desktop.csproj` setzt **nur beim Veröffentlichen** (`_IsPublishing`) `RuntimeIdentifier=win-x64` und `SelfContained=true`. Ein Ziel nach `ComputeResolvedFilesToPublishList` nimmt die nativen `.pdb` aus der Veröffentlichung. Der normale Build und die Tests bleiben unverändert. Die CI ruft `dotnet publish src/AispaStudio.Desktop -c Release -o publish/desktop` ohne Zusatzparameter auf. Kein Trimmen, keine einzelne Datei, kein ReadyToRun.

## Begründung

Das Paket verliert 100 MB ohne Funktionsverlust und ohne Voraussetzung auf dem Kundengerät. Die Varianten mit eigener Rückfalllogik (Trimmen, eine Datei) sparen im ZIP nichts oder gefährden den Start; die Messung stützt das.

## Folgen und Stand

- Windows-App allein: 122 MB entpackt, 56 MB ZIP (vorher 222 und 81 MB). Das ganze Paket mit API (179 MB), Web und PostgreSQL bleibt entpackt bei rund 500 MB (**groß**), als ZIP rund 185 MB statt 201 MB.
- Windows auf ARM (win-arm64) und 32-Bit-Windows werden nicht mehr bedient; nachrüsten wäre ein zweiter Paketlauf mit `-r win-arm64`.
- Ohne die `.pdb` der Natives gibt es bei einem Absturz in Skia keine Symbole; die verwalteten `.pdb` bleiben im Paket.
- Offen, größerer Hebel: API und App bringen je eine eigene .NET-Laufzeit mit; eine gemeinsame Laufzeit oder ein schlankeres PostgreSQL würden das Paket weiter verkleinern. Nicht Teil dieser Entscheidung.
- Echter Einzelplatz-Start mit dem Paket läuft weiter im CI-Test `LocalServerPackageTests`.

## Quellen

- [Messprotokoll](../produkt/systemanforderungen.md)
- [ADR 0041](0041-einzelplatz-standalone-und-zuschaltbare-webmodule.md), [ADR 0059](0059-zum-start-nur-die-windows-app.md)
- [PR-Protokoll, T-011](../umsetzung/pr-protokoll.md)
