# Technik-Stack der Oberflächen und Benutzerverwaltung

Stand: 17.10.2026. Zusammenfassung des Projekt-Threads „Technik-Stack Oberflächen .NET 10“. Der Auftraggeber hat die Empfehlung am 17.10.2026 angenommen („ja lass uns das so andenken“). Entscheidungen dazu: [ADR 0003](../entscheidungen/0003-rollenbasierte-oberflaechen.md), [ADR 0004](../entscheidungen/0004-oberflaechen-avalonia-und-react.md), [ADR 0007](../entscheidungen/0007-anmeldung-passkeys-identity.md).

## Vorgabe

Auftraggeber: „Oberfläche würde ich einmal für Windows in .NET 10 nativ und in einem sehr schnellen JS-Framework gestalten, mit der besten Anbindung in die .NET-10-Landschaft.“ Dazu kamen zwei Ergänzungen: Die Oberfläche soll sich klar an jede Rolle anpassen, vom Management bis zum Kunden, und die Benutzerverwaltung soll so einfach wie möglich sein.

## Empfehlung

**Avalonia** für Windows und **React mit TypeScript und Vite** fürs Web, beide am selben ASP.NET-Core-10-Backend.

**Windows:** Avalonia läuft auf .NET 10, rendert über die GPU (Skia) und eignet sich damit gut für einen selbst gebauten, flüssigen Kalender. Es ist XAML/MVVM wie WPF und läuft bei Bedarf auch auf dem Mac am Empfang. WPF wäre die Alternative, falls eine fertige Kalender-Komponente gekauft wird (DevExpress, Syncfusion, Telerik). Von WinUI 3 wird abgeraten, weil es weniger Komponenten gibt und Auslieferung sowie Entwicklung zäher sind.

**Web:** Ob React, Svelte oder Solid, macht bei der Geschwindigkeit nur wenige Millisekunden aus. Spürbar schnell wird die Oberfläche durch die Architektur: lokaler Cache, sofortige Anzeige vor der Serverantwort und Live-Updates. React passt am besten zu .NET: Vorlagen in Visual Studio, Aspire startet es direkt mit, und die Auswahl an Kalender- und KI-Chat-Bausteinen ist am größten. Wäre reine Rendering-Geschwindigkeit wichtiger als das Ökosystem, wäre Svelte 5 die Alternative.

**Gemeinsame Anbindung an .NET 10:**

- Das Backend beschreibt seine Schnittstelle mit OpenAPI 3.1; daraus werden die TypeScript-Clients automatisch erzeugt. Die Windows-App nutzt direkt dieselben C#-Klassen.
- Über SignalR sehen alle Standorte und Geräte eine Kalenderänderung sofort.
- Länger laufende Vorgänge (Buchungseingang, KI, Kasse) laufen entkoppelt über eine Nachrichten-Queue (Wolverine).
- .NET Aspire orchestriert alles lokal, eine GitHub-Actions-Pipeline baut und testet beide Oberflächen ab dem ersten Commit.

**Risiko:** Der Kalender als Kern wird zweimal gebaut. Das ist bewusst so gewählt, damit die native App am Empfang auch bei Internetausfall mit lokalem Stand weiterläuft. Ohne diese Anforderung hätte ein gemeinsamer Web-Kalender in der Windows-App (WebView2) viel Aufwand gespart. Der Auftraggeber hat den Offline-Betrieb bestätigt ([ADR 0005](../entscheidungen/0005-offline-windows-app-am-empfang.md)).

## Rollen und Oberflächen

| Rolle | Oberfläche | Inhalt |
| --- | --- | --- |
| Management | Web | Kennzahlen, echte Behandlungskosten, Benchmark; von überall und über alle Standorte |
| Rezeption | Windows-App | läuft den ganzen Tag, muss am schnellsten sein; Kasse und Kalender bleiben offline nutzbar |
| Therapeuten | Web als installierbare App (PWA) auf Tablet oder Handy | eigener Tagesplan, Kundennotizen, Schichten |
| Kunden | eigene, sehr schlanke Web-Oberfläche | Buchen, Gutscheine, Termine; Kunden installieren nichts |

So funktioniert die Anpassung:

- Das Backend legt pro Benutzer fest, was er darf und sieht (Rolle, Standort, Einzelrechte). Beide Oberflächen lesen dieselben Regeln. Geprüft wird immer auf dem Server, die Oberfläche blendet nur aus.
- Im Web werden die Bereiche je Rolle getrennt geladen; ein Kunde lädt nur den Buchungsteil. In Avalonia werden die Module je Rolle beim Start eingebunden.
- Jeder Benutzer kann innerhalb seiner Rolle sein Layout anpassen (Kalenderansicht, Spalten, Startseite), gespeichert am Server und damit auf jedem Gerät gleich.
- Der KI-Assistent kennt die Rolle: Auswertungen für das Management, Buchungshilfe für die Rezeption, Terminvorschläge für Kunden.

## Benutzerverwaltung

Ganz ohne Passwörter, Rechte nur über Vorlagen:

- **Einladen statt Anlegen:** Die Leitung gibt Name, Handynummer und Rolle ein. Die Person bekommt einen Link per SMS oder Mail oder scannt einen QR-Code und richtet beim ersten Öffnen einen Passkey ein (Fingerabdruck oder Gesichtserkennung). .NET 10 unterstützt Passkeys direkt in ASP.NET Core Identity.
- **Rollenvorlagen statt Häkchenlisten:** Rezeption, Therapeut, Standortleitung, Management, Buchhaltung, jeweils pro Standort. Ausnahmen sind einzelne Schalter, z. B. „darf Rabatte geben“.
- **Schneller Wechsel am Empfang:** mehrere Mitarbeitende teilen sich einen Rechner und melden sich per PIN oder Chipkarte in einer Sekunde an. Das braucht auch die Kasse, weil jeder Kassiervorgang einer Person zugeordnet sein muss.
- **Zeitlich begrenzter Zugang:** Aushilfen und freie Therapeuten bekommen Zugang bis zu einem Datum. Beim Austritt deaktiviert ein Klick alles, auch Schichtplan und Kasse.
- **Selbstverwaltung:** Mitarbeitende pflegen eigene Daten, Verfügbarkeiten und Urlaubswünsche selbst.
- **KI:** „Lege Anna als Therapeutin in Wien ab 1. November an“ reicht, die KI füllt die Einladung aus.
- **Kunden:** Gast, Code per SMS oder Mail, Passkey, Apple oder Google; kein Passwort.
- **Single Sign-on:** für größere Ketten optional mit Microsoft- oder Google-Firmenkonto über OpenID Connect.

Technisch selbst auf ASP.NET Core Identity mit Passkeys statt auf Auth0 oder Entra: Daten bleiben in der EU, keine Kosten pro Benutzer, SSO lässt sich später ergänzen.
