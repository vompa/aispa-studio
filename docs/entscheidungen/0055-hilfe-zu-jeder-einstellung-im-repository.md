# 0055. Hilfe zu jeder Einstellung: Texte im Repository, eingebaut in Web und Windows-App

- **Status:** angenommen
- **Datum:** 19.10.2026
- **Entschieden von:** Auftraggeber („es soll auch eine genaue Beschreibung dazu dahinterliegen und aus der App geöffnet werden können, auch bei allen anderen Einstellungen“), Ausgestaltung Claude

## Kontext

Der Auftraggeber will zu jeder Einstellung eine genaue Beschreibung, die man direkt aus der App öffnet, ausdrücklich auch für Stripe Connect samt Anleitung zur Plattform-Anmeldung. Die Windows-App läuft auch im Einzelplatz ohne Internet, und die Verwaltung ist dort als Seiten der Team-Oberfläche eingebettet (ADR 0053), Geräte, Sicherung, Netz und Abgleich sind dagegen eigene Ansichten der App.

## Optionen

| Option | Bewertung |
| --- | --- |
| Hilfe-Website oder Wiki außerhalb der App | braucht Internet, veraltet unbemerkt, nicht beim Feld |
| Tooltips an jedem Feld | zu kurz für Schritt-für-Schritt-Anleitungen, schlecht lesbar |
| Hilfetexte als Markdown im Repository, in Web und Windows-App eingebaut, Fragezeichen öffnet die passende Stelle | gewählt |

## Entscheidung

- Hilfetexte liegen als Markdown unter `web/src/help/content/<thema>.md`, ein Thema je Bereich, Abschnitte mit festem Anker (`## Titel {#anker}`). Erlaubt ist eine kleine Teilmenge: Überschriften, Absätze, Listen, fett, Code, Links.
- Die Team-Oberfläche lädt die Texte aus dem eigenen Build (kein Server, kein Internet nötig) und zeigt sie in einer Seitenleiste; „Hilfe“ in der Kopfzeile öffnet die Übersicht, das Fragezeichen neben einer Einstellung (`<HelpButton topic section>`) den Abschnitt.
- Die Windows-App baut dieselben Dateien als Ressource ein und zeigt sie unter „Hilfe“ in der Navigation bzw. vor der Anmeldung in einem eigenen Fenster; ihre Ansichten nutzen `<ui:HelpButton Topic Section>`.
- Tests in beiden Oberflächen prüfen, dass jedes Fragezeichen auf ein vorhandenes Thema und einen vorhandenen Anker zeigt und alle Texte eingebaut sind.
- Regel ab jetzt: Eine neue Einstellung bekommt im selben Pull Request ihren Hilfetext und ihr Fragezeichen.
- Stripe Connect bleibt eine Konfigurationsoption des Servers (ADR 0054); die Anleitung zur Plattform-Anmeldung steht im Thema „Zahlungen“, die Optionen des Servers im Thema „Server-Einstellungen“.

## Begründung

Eine Quelle für beide Oberflächen, offline lesbar, im selben Pull Request wie die Einstellung gepflegt und durch Tests an die Oberfläche gebunden. Ohne Bibliothek, weil die Teilmenge klein ist und der Kundenbereich davon nichts lädt.

## Folgen und Stand

- Code: `web/src/help/` (Leser, Seitenleiste, Fragezeichen), `src/AispaStudio.Desktop/Services/HelpContent.cs`, `Controls/HelpButton.cs`, `Views/HelpView.axaml`, `Views/HelpWindow.cs`.
- **Nachtrag (T-010, 23.10.2026):** Die Darstellung der Hilfe ist seit [ADR 0079](0079-eine-hilfe-fuer-windows-app-und-web.md) nur noch die Hilfe-Seite `/help/`; die Avalonia-Ansicht (`HelpView` mit eigener Liste, `HelpSectionView`, Zeilenvorlagen) entfällt, `HelpButton`, `HelpContent` und `HelpWindow` bleiben.
- Die Texte beschreiben das Verhalten der Software; ändert sich das Verhalten, ist der Hilfetext Teil der Änderung.
- Offen: Suche in der Hilfe, Hilfetexte in weiteren Sprachen.
