# 0029. Demo-Daten nach Wiener Day-Spas

- **Status:** angenommen
- **Datum:** 18.10.2026
- **Entschieden von:** Auftraggeber

## Kontext

Auftraggeber: „Für Beispiel-Behandlungen nimm die Seite von Le Siam Spa oder andere Top-Spas in Wien.“

## Optionen

| Option | Bewertung |
| --- | --- |
| Erfundene Behandlungen | wenig glaubwürdig im Test |
| Katalog nach öffentlichen Preislisten Wiener Day-Spas | gewählt |

## Entscheidung

Das Beispiel-Spa bekommt 30 Behandlungen (Massagen, Thai-Massagen, Gesicht, Körper und Hamam, Rituale, Hand und Fuß) mit Material, Vorbereitungsschritten und Kostensätzen, angelehnt an öffentliche Preislisten. Namen echter Spas werden nie als Mandant verwendet.

## Begründung

Ein realistischer Katalog macht Kalender, Kosten und Berichte im Prototyp aussagekräftig.

## Folgen und Stand

`DemoData.SeedAsync` in PR #23, aktiv mit `Database:SeedDemoData` in der Entwicklung. Offen: Feld Kategorie an der Behandlung.

## Quellen

- [Entwicklung, Lokal starten](../development.md#lokal-starten)
