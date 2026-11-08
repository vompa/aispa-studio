# 0045. Windows-App als vollwertiger Empfangsarbeitsplatz, nicht nur Kalender

- **Status:** angenommen
- **Datum:** 18.10.2026, 08:57
- **Entschieden von:** Auftraggeber (Rückmeldung), Ausgestaltung Claude

## Kontext

Nach dem ersten lokalen Start (PR #26) schrieb der Auftraggeber: „Der Windows Client schaut etwas spartanisch aus, ist der schon fertig?, und im development braucht man einen sinnvollen testdatensatz.“ Bis dahin konnte die Windows-App nur anmelden, den Tageskalender zeigen und buchen (PR #20). [ADR 0005](0005-offline-windows-app-am-empfang.md) hatte die App als offline-fähigen Arbeitsplatz der Rezeption vorgesehen, aber der Umfang war offen.

## Optionen

| Option | Bewertung |
| --- | --- |
| Windows-App bleibt Kalender-Ansicht, alles andere im Web | wenig Aufwand, aber die Rezeption müsste zwischen App und Browser wechseln; Kassa und Bondrucker brauchen ohnehin lokale Geräte |
| Windows-App deckt den ganzen Empfangsarbeitsplatz ab | gewählt |
| Web-Oberfläche als Desktop-Hülle (WebView) | ein Code, aber kein echter Offline-Betrieb und keine direkte Geräteanbindung |

## Entscheidung

Die Windows-App wird der vollständige Arbeitsplatz der Rezeption: Kalender mit Ziehen, Kunden, Kassa mit Belegen, Storno, Tagesabschluss, Bondrucker und Kartenterminal, Paar- und Spa-Tag-Buchung, Bereiche, Posteingang der Terminannahme, Warteliste, Gutscheine, Rechnung mit Empfänger, Eintritte, Schnellwechsel per PIN. Sie nutzt nur die Kassa-, Kalender- und Anmelde-Schnittstellen der API, geht ohne Webmodule ([ADR 0041](0041-einzelplatz-standalone-und-zuschaltbare-webmodule.md)) und bekommt eine eigene Gestaltung mit hellem und dunklem Modus. Der Testdatensatz aus demselben Wunsch ist [ADR 0032](0032-testdatensatz-und-testkonten.md).

## Begründung

Die Rezeption arbeitet an einem Gerät mit Drucker und Terminal und muss bei Netzausfall weiterarbeiten können. Das geht nur mit einer nativen App, die alle Empfangsaufgaben kennt. Management und Therapeuten bleiben im Web.

## Folgen und Stand

- Umgesetzt in den PRs #32, #55, #59, #63, #66, #78, #82, #85, #89 und #92 (Schnellwechsel per PIN in der App).
- Offen: Kassa ohne Verbindung, neue Kunden ohne Verbindung (#94), OPI-Terminals (#101), Gutschrift am Terminal (#98) (siehe [Stand](../stand-und-offene-punkte.md)).
- Bildschirmfotos aller Ansichten liegen unter [docs/bilder/windows-app](../bilder/windows-app/) und entstehen neu mit `AISPA_SCREENSHOTS=$PWD/bilder dotnet test --project tests/AispaStudio.Desktop.Tests`.

## Quellen

- Projektchat 18.10.2026, 08:57 UTC
- [Übergabe Windows-App](../uebergabe/threads/windows-app.md)
