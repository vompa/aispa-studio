# 0090. Live-Tests der Windows-App mit FlaUI

- **Status:** angenommen (Probelauf Rezeption)
- **Datum:** 08.11.2026
- **Entschieden von:** Auftraggeber (Probelauf freigegeben), Umsetzung Claude nach ADR 0019

## Kontext

Der Auftraggeber möchte drei Tester, die als Inhaberin, Rezeption und Mitarbeiterin die ganze App live durchspielen. Man soll zusehen, anhalten, Bilder machen und alles protokollieren können. Die bestehenden Ansichtstests (`AispaStudio.Desktop.Tests`, Avalonia headless) prüfen einzelne Masken ohne Fenster und ohne Server; einen sichtbaren Durchlauf durch die echte App gegen einen echten Server gab es nicht. Freigegeben ist zuerst ein Probelauf für die Rezeption („Ja, Probelauf“, 08.11.2026).

## Optionen

| Option | Bewertung |
| --- | --- |
| A: FlaUI (UI Automation von Windows) | gewählt: steuert die echte, gebaute App mit Maus und Tastatur; Avalonia 12 liefert die Bedienungshilfen selbst; MIT; nur Windows |
| B: Avalonia headless erweitern | kein sichtbares Fenster, kein echter Server-Betrieb, kein Zusehen |
| C: WinAppDriver oder Appium | WinAppDriver wird nicht mehr gepflegt, Appium braucht einen weiteren Server |

## Entscheidung

- Neues Projekt `tests/AispaStudio.LiveTests`: Konsolen-App für `net10.0-windows` mit dem Paket `FlaUI.UIA3` 5.0.0 (MIT, Version nur in `Directory.Packages.props`). Kein Testprojekt und **nicht in `AispaStudio.slnx`**, weil die CI die Solution unter Linux baut.
- Aufruf: `pwsh scripts/live-tests.ps1 -Role Reception [-Speed slow|normal|fast] [-Step]`. Das Skript prüft den Server (`/api/system/status`), baut App und Tester und startet den Lauf; Docker startet es nicht.
- Die App startet mit `--server` und `AISPA_API_URL` gegen den Server mit Demo-Daten. Jeder Schritt hat einen deutschen Namen, hebt das Zielelement kurz hervor, wartet die eingestellte Pause und macht ein Bild des Fensters.
- Steuerung in der Konsole: Leertaste Pause/Weiter, Enter Einzelschritt, `S` Bild, `Q` Stopp.
- Bericht `.test-logs/live/<Zeitstempel>-<rolle>/bericht.md` mit Schritt, Start, Dauer, Ergebnis, Bild und Ereignissen. Exitcode 0 bestanden, 1 Schritt gescheitert (mit Fehlerbild), 2 Aufruf oder Server, 3 mit `Q` gestoppt.
- Gesucht wird über `AutomationProperties.Name`, Beschriftung und Text, sonst über `x:Name` (in Avalonia die `AutomationId`). Neue `AutomationId`s nur, wo die Suche nicht eindeutig ist; im Probelauf war keine nötig.

## Begründung

Nur die echte App gegen einen echten Server zeigt, was eine Rezeptionistin sieht, und nur ein sichtbarer Lauf lässt sich anhalten und belegen. FlaUI ist die verbreitete .NET-Bibliothek für UI Automation und braucht keinen weiteren Dienst. Die Live-Tests ersetzen weder die Ansichtstests noch die CI; sie sind ein Werkzeug zum Zusehen und Belegen.

## Folgen und Stand

- Probelauf Rezeption: anmelden als Nina Kovač, nächster Tag, neue Kundin mit eindeutigem Namen und Telefon, „Anlegen und Termin buchen“, Behandlung wählen, „Freie Zeit suchen“, erste freie Zeit buchen, Prüfung an der Kalenderkarte. Zehn Schritte, rund 75 Sekunden bei `-Speed slow`.
- Eine feste Uhrzeit hält nicht, weil jeder Lauf einen Termin in der Datenbank hinterlässt; die Rezeption sucht deshalb freie Zeit wie im Betrieb.
- Kassieren ist noch nicht dabei: Es braucht „Ist da“, und das geht nur am Tag des Termins. Der Bericht vermerkt das.
- Offen: Rolle Inhaberin (Windows-App), Rolle Mitarbeiterin (Handy-App im Web, sichtbar per Playwright), Kassieren mit einem Termin am selben Tag, Lauf in der CI (braucht eine echte Bildschirmsitzung).
- Erkenntnisse zur Steuerung: Die Auswahl „Behandlung“ ist virtualisiert, Buchstaben lösen im Kalender Tastenkürzel aus; gesucht wird deshalb nur unter `ComboBoxItem`. Gleichnamige Texte stehen oft auch im Kalender, Prüfungen nennen deshalb die Art des Elements (`AppointmentCard`).

## Nachtrag 08.11.2026: Starter-App und Themen

Der Auftraggeber wollte eine kleine Windows-App, aus der man die Live-Tests mit einer Auswahl der Themen startet. Entschieden hat er „Themen je Rolle“ und „Knöpfe in der App“.

- Der Tester ist zugleich Bibliothek: Eine Rolle ist ein `LiveJourney` mit festem Vorlauf (abmelden, anmelden, nächster Tag) und Themen. Die Rezeption hat „Kundin anlegen“ und „Termin buchen“; ohne neue Kundin bucht „Termin buchen“ für Ingrid Wallner aus den Demo-Daten. Die Konsole nimmt `--topics customer,appointment`, das Skript `-Topics`; ohne Angabe laufen alle Themen.
- Neue Starter-App `tests/AispaStudio.LiveTests.Launcher` (Avalonia, FluentTheme, CommunityToolkit.Mvvm, wie der Tester nicht in `AispaStudio.slnx`), Start mit `pwsh scripts/live-tests.ps1 -Gui`. Sie zeigt Rolle (Inhaberin und Mitarbeiterin ausgegraut, „kommt noch“), Themen zum Anhaken, Tempo, „Angehalten beginnen“, die Serveradresse mit Prüfung über `/api/system/status`, die Schritte live mit ✓/✗ und die Knöpfe Start, Pause/Weiter, Schritt, Bild, Stopp, „Bericht öffnen“ und „Ordner öffnen“.
- Die Starter-App liegt schmal und immer oben am rechten Rand; der Tester setzt das App-Fenster über UI Automation (`WindowPattern`, `TransformPattern`) auf die Fläche links daneben. So treffen Klicks und Bilder die Starter-App nicht.
- Die Konsolentasten bleiben für den Konsolenbetrieb; beide Wege rufen dieselben Befehle in `LiveControl`.

## Nachtrag 08.11.2026: Tagesregel beim Einchecken

Der Knopf heißt **Einchecken**, nicht „Ist da“. Bis zur Aufgabe B der [Arbeitsanweisung Rezeptions-Lücken](../umsetzung/arbeitsanweisung-rezeption-luecken.md) prüfte weder der Server noch die App den Tag; seit dieser Aufgabe gilt die Tagesregel wirklich (Einchecken nur am Tag des Termins, Kassieren nur eingecheckter oder erledigter Termine). Ein Live-Test des Kassierens braucht deshalb einen Termin am Tag des Laufs; die Live-Test-Sitzung setzt das um, hier nur vermerkt.

## Quellen

- [FlaUI](https://github.com/FlaUI/FlaUI) (MIT)
- [Arbeitsweise, Abschnitt Live-Tests](../arbeitsweise.md#live-tests-der-windows-app)
- `tests/AispaStudio.LiveTests`, `scripts/live-tests.ps1`
