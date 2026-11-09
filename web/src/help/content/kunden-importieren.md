---
art: ablauf
bereich: Kunden
oberflaeche: windows-app
rollen: leitung, inhaber, manager
seite: kunden
stichwoerter: Kunden importieren, Import, Kundenliste übernehmen, Umstieg, Wechsel, Treatwell, Shore, Excel, CSV, Dubletten, Probelauf, Gäste übernehmen, import customers, migration
---
# Kunden importieren

Beim Umstieg von einem anderen Programm übernimmt der Import die Gäste aus einer Excel- oder CSV-Datei. Importieren dürfen nur Inhaberin und Geschäftsführung.

1. **Kunden** öffnen, neben **Exportieren** auf **…** klicken und **Kunden importieren …** wählen.
2. **Schritt 1 von 4 · Datei wählen**: **Datei wählen …** klicken und die Excel- (`.xlsx`) oder CSV-Datei öffnen. In der ersten Zeile müssen die Spaltenköpfe stehen.
3. **Schritt 2 von 4 · Spalten zuordnen**: Die App schlägt zu jeder Spalte ein Feld vor (Vorname, Nachname, E-Mail, Telefon, Geburtstag, Anrede, Straße, PLZ, Ort, Land, Werbe-Einwilligung) und zeigt drei Beispielwerte. Vorschläge prüfen, Spalten ohne Gegenstück auf **nicht importieren** lassen. Der Nachname muss zugeordnet sein. Beim **Datumsformat** passt meist **Automatisch**; sonst das Format der Datei wählen, z. B. **TT.MM.JJJJ**.
4. **Schritt 3 von 4 · Vorschau und Prüfung**: Oben steht, wie viele Zeilen neu, Dubletten und fehlerhaft sind. Jede Dublette steht auf **Überspringen**, dann bleibt der Gast im Bestand unverändert; mit **Ergänzen** füllt der Import beim vorhandenen Gast nur leere Felder, er überschreibt nichts. Fehlerhafte Zeilen stehen darunter mit Grund.
5. **Schritt 4 von 4 · Probelauf und Import**: Zuerst **Nur prüfen** klicken. Der Probelauf zählt nur, z. B. „Probelauf: 240 neu, 0 ergänzt, 12 übersprungen. Es wurde nichts gespeichert.“ Passt das Ergebnis, auf **Importieren** klicken. Mit **Zurück** lässt sich jeder Schritt vorher noch ändern.

Danach schließt der Dialog, die Kundenliste lädt neu und kurz steht z. B. „Importiert: 240 neu, 0 ergänzt, 12 übersprungen“.

## Beispieldateien {#beispiele}

Zum Ausprobieren liegen im Repository unter `docs/beispiele/` die Dateien `kunden-import-beispiel.xlsx` und `kunden-import-beispiel.csv`. Sie haben sieben Zeilen: vier neue Gäste, eine Dublette in der Datei und zwei fehlerhafte Zeilen.

## Dubletten {#dubletten}

Eine Zeile ist eine Dublette, wenn ein Gast mit gleichem Nachnamen und gleicher Telefonnummer oder E-Mail-Adresse schon in der Kundenliste steht oder weiter oben in derselben Datei. Die Vorschau nennt den Grund, z. B. „gleicher Nachname und gleiche E-Mail-Adresse wie Lena Beispiel“ oder „doppelt in der Datei (Zeile 2)“.

## Typische Fehler {#fehler}

| Meldung in der Vorschau | Was tun |
|---|---|
| Nachname fehlt | In der Datei den Nachnamen ergänzen oder die richtige Spalte zuordnen. |
| Geburtstag kein Datum | Datum berichtigen (z. B. 31.02.) oder in Schritt 2 das Datumsformat wählen. |
| Telefon unleserlich | Nummer mit Vorwahl schreiben, z. B. +43 660 1234567. |

Fehlerhafte Zeilen werden nicht importiert. In Schritt 3 speichert **Fehlerzeilen als Excel …** sie als Datei (mit Zeile, Grund und allen Spalten der Datei): berichtigen und nur diese Zeilen erneut importieren.

## Gut zu wissen {#hinweise}

- Eine Datei darf höchstens 10 MB und 20 000 Zeilen haben; größere Bestände in mehrere Dateien teilen.
- CSV-Dateien dürfen mit Strichpunkt, Komma oder Tabulator getrennt sein. Umlaute stimmen auch bei CSV-Dateien, die Excel ohne UTF-8 gespeichert hat.
- Spaltenköpfe aus Shore-Exporten (z. B. `first_name`, `mobile`, `birthday`) und gängige englische Köpfe wie „First name“ erkennt die App von selbst; Exporte anderer Programme wie Treatwell in Schritt 2 von Hand zuordnen.
- Die Werbe-Einwilligung wird nur übernommen, wenn die Spalte zugeordnet ist und dort „Ja“ steht; sie erscheint in der Kundenkarte als „aus einem Import übernommen“.
- Vermerke, Gesundheitsangaben und Anamnese übernimmt der Import nicht.
- Jeder Import steht im Protokoll, z. B. „Import Kunden: 240 neu, 0 ergänzt, 12 übersprungen (kunden.xlsx, 252 Zeilen) von Lena“, siehe [Listen exportieren](hilfe:daten-exportieren#protokoll).
