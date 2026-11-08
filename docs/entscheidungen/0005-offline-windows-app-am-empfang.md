# 0005. Rezeption arbeitet mit einer offline-fähigen Windows-App

- **Status:** angenommen
- **Datum:** 17.10.2026
- **Entschieden von:** Auftraggeber

## Kontext

Offene Entscheidung aus dem Entscheidungsdokument: Muss die Rezeption bei Internetausfall weiterarbeiten können? Davon hing ab, ob der Kalender als Web-Kalender in einer WebView2-Hülle reicht.

## Optionen

| Option | Bewertung |
| --- | --- |
| Web-Kalender in WebView2-Hülle | Kalender nur einmal bauen, aber kein Offline-Betrieb |
| Eigene Avalonia-App mit lokalem Stand | gewählt |

## Entscheidung

Ja. Die Rezeption bekommt die Avalonia-App mit eigenem Kalender, der ohne Internet weiterläuft und danach abgleicht. Kasse und Kundenkarten liegen lokal vor.

## Begründung

Ein Spa mit vollem Kalender verliert bei einem Ausfall sofort Umsatz und Vertrauen. Für Österreich muss die Kasse auch bei Ausfall der Signatureinrichtung weiterkassieren können.

## Folgen und Stand

- PR #20 lieferte die Windows-App mit Anmeldung per Einmal-Code, Tageskalender, Buchen und Live-Updates; #32 und #59 Ziehen zum Verschieben und die Kassa.
- Seit PR #63 läuft die App ohne Verbindung weiter: Kalender, Stammdaten und Kundenkarten aus dem zuletzt geladenen, verschlüsselten Stand (`src/AispaStudio.Desktop/Services/Offline.cs`), Terminänderungen werden vorgemerkt und danach abgeglichen. Über die Belegung entscheidet auch dann nur der Server; Buchungen tragen dafür eine eigene Id, damit eine Wiederholung nicht doppelt bucht.
- Offen: Kassieren ohne Verbindung. Belege, Signatur und Kassenjournal entstehen bisher nur am Server; offline braucht es lokale Belegnummern mit Nachsignatur bzw. Ersatzbelege nach RKSV.

## Quellen

- [Ergänzende Themen, Abschnitt 4](../konzepte/ergaenzende-themen.md)
