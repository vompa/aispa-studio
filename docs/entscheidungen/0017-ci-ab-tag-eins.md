# 0017. Continuous Integration ab dem ersten Commit

- **Status:** angenommen
- **Datum:** 17.10.2026
- **Entschieden von:** Auftraggeber (Anforderung), Umsetzung Claude

## Kontext

Der Auftraggeber verlangt CI von Anfang an und eine Oberfläche, die so schnell wie möglich reagiert. Kassen- und Arbeitszeitrecht sind Haftungsthemen.

## Optionen

| Option | Bewertung |
| --- | --- |
| Azure DevOps | gleichwertig; GitHub passt besser und ist für kleine Teams günstiger |
| Tests später ergänzen | Nachrüsten kostet ein Vielfaches |
| Nur manuelle Abnahme | zu langsam |
| GitHub Actions mit Pflichtprüfungen | gewählt |

## Entscheidung

GitHub Actions baut und testet bei jeder Änderung Backend, Windows-App und Web: Format, Build mit Warnungen als Fehler, Integrationstests gegen echtes PostgreSQL, aktuelle OpenAPI-Typen, Lint, Typecheck, Vitest mit axe-core, Bundle-Budget des Kundenbereichs, Docker-Compose-Lauf.

## Begründung

Langsamkeit und Abstürze sind die häufigste Beschwerde über Konkurrenzprodukte. Ein Budget in der CI verhindert, dass das Produkt mit der Zeit langsam wird.

## Folgen und Stand

`.github/workflows/ci.yml`. Nach jedem grünen Lauf auf `main` ersetzt die CI die Windows-App im Release `windows-app`. Ein Test schlägt fehl, wenn eine EF-Migration fehlt.
