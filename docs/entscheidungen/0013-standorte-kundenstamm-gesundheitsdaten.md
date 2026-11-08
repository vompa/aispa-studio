# 0013. Gemeinsamer Kundenstamm über Standorte, Gesundheitsdaten getrennt, Anamnese auf drei Wegen

- **Status:** angenommen
- **Datum:** 17.10.2026
- **Entschieden von:** Auftraggeber

## Kontext

Auftraggeber: mehrere Standorte, „die sich den Kundenpool usw. teilen“. Anamnesen enthalten Gesundheitsdaten nach Art. 9 DSGVO. Für den Anamnesebogen verlangte der Auftraggeber, dass Papier immer möglich bleibt.

## Optionen

| Option | Bewertung |
| --- | --- |
| Getrennte Konten je Standort | kein gemeinsamer Kundenstamm |
| Eine Firma für alles | passt nicht zu Gruppen mit getrennten Gesellschaften |
| Gesundheitsdaten frei teilen | rechtlich nicht haltbar ohne Einwilligung |
| Rein digitaler Anamnesebogen | Gäste ohne Handy und Internetausfall bleiben außen vor |
| Gemeinsames Profil, Gesundheitsdaten als eigene Klasse, drei Anamnese-Wege | gewählt |

## Entscheidung

Ein Kundenprofil für alle Standorte, jeder Standort kann eine eigene Firma sein. Vorlieben sehen alle Standorte, Gesundheitsdaten nur mit Einwilligung; die Rezeption sieht nur „Hinweis vorhanden“. Anamnese gleichwertig per Link am Handy, am Tablet per QR-Code oder auf Papier mit Foto und KI-Übernahme; unsichere Felder werden gelb markiert und geprüft.

## Begründung

Kein geprüfter Anbieter belegt, dass getrennte Firmen Kunden und Gutscheine teilen können; kleine Day-Spa-Gruppen sind aber oft so organisiert.

## Folgen und Stand

Anamnese AES-256-GCM verschlüsselt im Schema `health` (PR #9), Tablet, Papier und Foto in PR #15. Behandlungskarte zeigt nur den Stand des Fragebogens, nie den Inhalt (PR #16). Offen: protokollierter Abruf der Gesundheitshinweise auf der Karte.

Nachtrag (02.11.2026, Runde R05 Slice 128, Ä304): API und Web ließen die Rezeption bis dahin die Antworten lesen. Der Auftraggeber hat am 31.10.2026 entschieden, die API zu verschärfen, und am 02.11.2026 festgelegt, wer fotografierte Bögen prüft. Seither gilt das Recht „Anamnese lesen“ (`ViewHealthData`): Inhaber, Manager, Standortleitung und Therapeut:innen lesen die Antworten und dürfen fotografierte Bögen ansehen, prüfen, übernehmen und verwerfen, auch ohne Recht zum Buchen. Die Rezeption sieht nur den Stand und den Hinweis „Anamnese prüfen“; Tablet-Link, Papierbogen und Hochladen eines Fotos bleiben ihr, weil sie keine Antworten liefern. Das Backoffice sieht nichts. Die Abstimmung mit der Datenschutz-Beratung ist offen.

Nachtrag (02.11.2026, Quick-Fix Ä309): Der Auftraggeber hat entschieden, dass für die Anamnese dieselben Grenzen gelten wie für die Kundendaten: Wer im Programm mit Kunden arbeitet, darf sie lesen. Seither hat auch die Rezeption das Recht „Anamnese lesen“ und prüft fotografierte Bögen; das Backoffice sieht weiter nichts, die Berichte nur den Stand. Das Recht bleibt ein eigenes, damit eine spätere Einschränkung nur die Rollentabelle ändert. Die Abstimmung mit der Datenschutz-Beratung gilt nun dieser Regel und ist offen.

Nachtrag (05.11.2026, Aufgabe T-013): Die Regel aus Ä309 ist bestätigt (Auftraggeber); die Abstimmung ist abgeschlossen. Die Tabelle zeigt den Stand nach Ä309.

| Aufruf | Inhaber, Manager, Standortleitung | Rezeption | Therapeut:in | Backoffice |
| --- | --- | --- | --- | --- |
| Stand und Hinweis „Anamnese prüfen“ | ja | ja | ja | nein |
| Antworten lesen | ja | ja | ja | nein |
| Tablet-Link, Papierbogen, Foto hochladen | ja | ja | nein | nein |
| Foto ansehen, prüfen, übernehmen, verwerfen | ja | ja | ja | nein |

## Quellen

- [Ergänzende Themen, Abschnitt 1](../konzepte/ergaenzende-themen.md#1-datenschutz-und-dsgvo)
