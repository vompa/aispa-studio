# 0004. Oberflächen-Technik: Avalonia für Windows, React mit TypeScript und Vite fürs Web

- **Status:** angenommen
- **Datum:** 17.10.2026
- **Entschieden von:** Auftraggeber („ja lass uns das so andenken“), Empfehlung Claude

## Kontext

Auftraggeber: „Oberfläche würde ich einmal für Windows in .NET 10 nativ und in einem sehr schnellen JS-Framework gestalten, mit der besten Anbindung in die .NET-10-Landschaft.“

## Optionen

| Option | Bewertung |
| --- | --- |
| WPF | sehr reif, fertige Kalender zu kaufen; nur Windows, ältere Rendering-Basis; gute Wahl nur beim Kauf eines Kalenders |
| WinUI 3 | modernste Microsoft-Optik, aber weniger Komponenten, zähere Auslieferung |
| .NET MAUI | eine Codebasis für Desktop und Mobil, aber dichter Desktop-Kalender schwierig |
| Blazor | C# auch im Web, aber größerer Download bzw. Serververbindung je Klick, kleineres Ökosystem |
| Svelte 5 oder SolidJS | minimal schneller, aber kleineres Ökosystem und weniger Fachkräfte in DACH |
| Nur Web, Windows als WebView2-Hülle | kein vollwertiger Offline-Betrieb |
| Avalonia + React/TypeScript/Vite | gewählt |

## Entscheidung

Avalonia (.NET 10) für die Windows-App am Empfang, React mit TypeScript und Vite fürs Web, beide am selben ASP.NET-Core-10-Backend. Die API wird mit OpenAPI 3.1 beschrieben, daraus entstehen die TypeScript-Typen; die Windows-App nutzt dieselben C#-Klassen (`AispaStudio.Contracts`). Live-Änderungen per SignalR.

## Begründung

Avalonia rendert über die GPU (Skia), ist XAML/MVVM wie WPF und läuft bei Bedarf auch auf dem Mac. Bei React, Svelte und Solid liegen nur Millisekunden dazwischen; spürbar schnell wird die Oberfläche durch die Architektur (lokaler Cache, optimistische Anzeige, Live-Updates). React hat die beste .NET-Anbindung (Vorlagen, Aspire) und die größte Auswahl an Bausteinen.

## Folgen und Stand

Risiko: Der Kalender wird zweimal gezeichnet; die Logik bleibt einmal im Backend. Umgesetzt im Grundgerüst (PR #1); `web/openapi/aispa-api.json` und `web/src/api/schema.d.ts` werden committet und in der CI geprüft.

## Quellen

- [Technik-Stack Oberflächen](../konzepte/technik-stack-oberflaechen.md)
