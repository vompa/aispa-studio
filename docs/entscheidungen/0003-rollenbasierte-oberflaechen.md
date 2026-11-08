# 0003. Oberfläche passt sich der Rolle an

- **Status:** angenommen
- **Datum:** 17.10.2026
- **Entschieden von:** Auftraggeber (Anforderung), Empfehlung Claude, vom Auftraggeber angenommen

## Kontext

Auftraggeber: „Auch soll sich die Oberfläche klar an die jeweiligen Benutzer vom Management bis zum Kunden anpassen.“ Viele Programme zeigen allen Rollen dasselbe Menü.

## Optionen

| Option | Bewertung |
| --- | --- |
| Eine Oberfläche für alle, Rechte als ausgegraute Menüpunkte | überladen, langsamer Einstieg |
| Native iOS- und Android-Apps für Therapeuten | Store-Freigaben und doppelte Entwicklung; eine PWA kann Push, Offline und Startbildschirm |
| Rezeption nur im Web | billiger, verliert aber den robusten Offline-Betrieb |
| Je Rolle eigenes Gerät und eigene Startseite | gewählt |

## Entscheidung

Rezeption: Windows-App (Avalonia). Therapeut: Web als PWA auf Handy oder Tablet. Management: Web. Kunde: eigener schlanker Web-Bereich. Rechte (Rolle, Standort, Einzelrechte) prüft immer der Server, die Oberfläche blendet nur aus. Im Web wird je Rolle nur der eigene Teil geladen.

## Begründung

Rezeption und Geschäftsführung brauchen zur selben Zeit völlig andere Dinge. Für den Behandlungsraum reicht laut Entscheidung des Auftraggebers das Handy; Tablet ist möglich, Ausdruck nur Notlösung.

## Folgen und Stand

Umgesetzt mit vier festen Rollen (Inhaber, Standortleitung, Rezeption, Therapeut:in) und `RequirePermission(AccessRight)` in PR #6. Der Kundenbereich ist ein eigenes Bundle unter `/buchen/` mit Budget 100 kB gzip (PR #9).

## Quellen

- [Technik-Stack Oberflächen](../konzepte/technik-stack-oberflaechen.md)
