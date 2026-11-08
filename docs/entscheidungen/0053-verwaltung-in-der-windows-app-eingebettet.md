# 0053. Verwaltung in der Windows-App: Seiten der Team-Oberfläche eingebettet

- **Status:** ersetzt durch [0084](0084-alle-masken-nativ-web-fuer-dashboard-und-handy.md)
- **Datum:** 19.10.2026
- **Entschieden von:** Auftraggeber („ja genau so habe ich mir das vorgestellt“), Ausgestaltung Claude

## Kontext

Auftraggeber: „Es sollen generell alle Verwaltungen auch im Windows-Client gemacht werden; überleg dir eine gemeinsame Basis und ob man die Web-Oberfläche auch im Windows-Client verwenden könnte.“ Zum Start gibt es vermutlich nur die Windows-App. Bisher gingen Stammdaten, Firmendaten, Zugänge, Dienstplan, Zeiten, Kassen, Lager, Berichte, Kosten, Buchhaltung und Lohn nur im Browser.

## Optionen

| Option | Bewertung |
| --- | --- |
| Jede Verwaltung zusätzlich nativ in Avalonia bauen | doppelte Oberfläche für über zehn Bereiche, jede Änderung zweimal, beide laufen auseinander |
| Ganze App als Web-Oberfläche in einer Hülle | verliert den Betrieb ohne Verbindung und die Geräte (Bondrucker, Kartenterminal) am Empfang |
| Empfang nativ, Verwaltung als eingebettete Web-Seiten | gewählt |

## Entscheidung

- Der tägliche Empfang bleibt nativ in Avalonia: Kalender, Kunden, Anfragen, Kassa, Geräte, Abgleich, Sicherung, Netz. Er funktioniert ohne Verbindung.
- Alle Verwaltungen gibt es nur einmal, als Seiten der Team-Oberfläche. Die Windows-App zeigt sie unter „Verwaltung“ in einem eingebetteten Browser (`Avalonia.Controls.WebView`, unter Windows WebView2).
- Die Team-Oberfläche hat dafür einen eingebetteten Modus (`/?eingebettet=1`): nur die Verwaltungsseiten, ohne Kalender, Konto, Anmeldung, Passkeys und Assistent. Welche Seiten jemand sieht, entscheiden wie im Browser die Rechte.
- Anmeldung: Die App setzt vor dem ersten Laden ihr Anmelde-Cookie in den eingebetteten Browser. Der Browser läuft InPrivate und speichert nichts dauerhaft. Abmelden in der App beendet auch die Verwaltung.
- Im Mehrplatz-Betrieb (ADR 0049) vertraut der eingebettete Browser nur dem Zertifikat des Haupt-PCs, das die App mit dem Verbindungscode geprüft hat (SPKI-Hash als Ausnahme für genau diesen Schlüssel).
- Links auf fremde Seiten öffnen sich im Standardbrowser, nicht in der App.
- Liefert der Server unter seiner Adresse keine Team-Oberfläche aus (z. B. in der Entwicklung mit getrenntem Web-Server) oder fehlt die Verbindung, sagt die Verwaltung das und bietet „Erneut versuchen“.

## Begründung

Eine Oberfläche für jede Verwaltung heißt: eine Stelle für Fehler und Verbesserungen, im Browser und in der App gleich. WebView2 ist auf Windows 10 und 11 vorinstalliert, bringt also nichts Großes mit. Der Empfang behält, was nur nativ gut geht: Betrieb ohne Verbindung, Bondrucker, Kartenterminal und schnelle Bedienung.

## Folgen und Stand

- Code: `Desktop/Views/AdminView.axaml(.cs)`, `Desktop/ViewModels/AdminViewModel.cs`, eingebetteter Modus in `web/src/App.tsx`.
- Die Verwaltung braucht eine Verbindung zum Server oder zum Haupt-PC; ohne Verbindung geht nur der Empfang.
- Offen: Die Web-Verwaltung bekommt das Aussehen der App (nächster Schritt). Der Ausnahme-Schalter für das Mehrplatz-Zertifikat ist nur unter Windows mit echtem WebView2 zu prüfen.

## Quellen

- PR „Verwaltung in der Windows-App“
