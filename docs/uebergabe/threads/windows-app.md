# Übergabe: Windows-App (Thread „Windows-App ausbauen“)

Stand: 18.10.2026, 21:40 UTC. Gestoppt auf Anweisung des Auftraggebers. Repository: `vompa/aispa-studio`, Code unter `src/AispaStudio.Desktop`, Tests unter `tests/AispaStudio.Desktop.Tests`.

## Gebaut und gemergt

| PR | Inhalt |
| --- | --- |
| #32 | Neugestaltung: Hell/Dunkel, Navigation, Termine ziehen, Tagesüberblick; Kunden (Suche, Kundenkarte, Anlegen); Kassa (Rechnung, Zahlungsarten, Beleg) |
| #55 | Anmeldung mit Testkonto in der Entwicklung |
| #59 | Belege, Storno, Einlage/Entnahme, Tagesabschluss, Bondrucker im Netz (ESC/POS), Kartenterminal per ZVT |
| #63 | Betrieb ohne Internet: letzter Stand von Kalender und Kunden, Termine vormerken, Abgleich danach |
| #66 | Paar- und Spa-Tag-Buchung, ganze Buchung verschieben/stornieren, Sauna und Ruheraum mit Belegung |
| #78 | Anfragen der KI-Terminannahme (live, Vorschlag buchen, Antwort senden), Warteliste im Kalender |
| #82 | Gutscheine verkaufen, Rabatt je Position (neuer Endpunkt `PUT /api/pos/bills/{id}/lines/{id}/discount`), CHF für Schweizer Kassen |
| #85 | Rechnung mit Empfänger, UID-Prüfung über VIES, Unternehmensangaben am Beleg |
| #89 | Eintritte in Bereiche an der Kassa abrechnen, auch direkt aus „Sauna und Ruheraum“ |

Jeder PR hat einen Abschnitt in `docs/umsetzung/pr-protokoll.md` mit Detailentscheidungen und offenen Punkten; `docs/stand-und-offene-punkte.md` ist bis #89 nachgezogen.

## Halb fertig

- PR #92 (Schnellwechsel per PIN am Empfangsgerät) wurde nach dem Stopp gemergt, als der Auftraggeber „weitermachen“ schrieb.
- Keine weiteren offenen Branches oder bekannten Fehler aus diesem Thread.

## Entscheidungen unterwegs (Details in den PR-Abschnitten)

- Beträge mit festen Trennzeichen statt Systemkultur (`1.234,50 €`, `CHF 1’234.50`), weil Windows und ICU unterschiedlich formatieren; Währung aus Fiskalland der Kasse bzw. `LocationDto.Currency` (#82).
- Rabatt nur auf offene, unbezahlte Positionen, nicht auf Gutscheine, kleiner als der Betrag (#82).
- UID-Nachweis wird nur angehängt, wenn die gültige VIES-Abfrage genau zur eingegebenen UID passt; Vorbelegung aus VIES überschreibt keine Eingaben (#85).
- Preis bei Eintritten nur als Vorschau; Preis, Steuer, Erlöskonto und Menge bestimmt der Server über den Artikel am Bereich. Bereich ohne Artikel bleibt mit „kein Preis“ sichtbar (#89).
- Schnellwechsel (#92, ADR 0042): Gerätekennzeichen `aispa.device` getrennt von der Anmeldung gespeichert (DPAPI, `empfangsgeraet.bin`), überdauert Abmelden und Wechsel; PIN-Anmeldung wird nicht gemerkt; lokaler Stand der vorigen Person wird beim Wechsel gelöscht; Abmelden nach Leerlauf mit 30 Sekunden Warnung.
- Alle neuen Funktionen nutzen nur Kassa-, Kalender- und Anmelde-Schnittstellen und gehen ohne Webmodule (ADR 0041).
- Abgrenzung: lokale Datenbank/API, klassische Benutzerverwaltung, Abgleich und Kassa-Ersatzbelege ohne Verbindung gehören zum Thread „Windows-App eigenständig mit lokaler Datenbank“ (ADR 0041), nicht hierher.

## Nächste Punkte in der Reihenfolge, die ich gewählt hätte

Ohne Gerät und ohne neue Entscheidung ist hier nichts mehr offen. Als Nächstes bräuchte es: ZVT und OPI an einem echten Terminal prüfen; die Anzahlung anzeigen, sobald der Stand der Online-Buchung gemergt ist (siehe [Übergabe](../../uebergabe.md), Abschnitt 4); die Kassa ohne Verbindung im Thread „Windows-App eigenständig mit lokaler Datenbank“.

Erledigt nach dem Stopp: neue Kunden ohne Verbindung anlegen (#94), Eintritte ohne Verbindung vormerken (#95), Paar und Spa-Tag ohne Verbindung (#97), Gutschrift am Terminal beim Storno (#98), Rechnungsempfänger aus der Kundenkarte vorbelegen (#99), PIN in der App entfernen (#100), OPI für Kartenterminals (#101), Kartenterminals unter „Geräte“ anlegen und entfernen (#102), Rechnungsempfänger in der Web-Kassa vorbelegen (#103), Rechnungsempfänger in der Kundenkarte ändern (#104).

## Überlegungen für später

- ZVT ist nur gegen ein nachgebautes Terminal getestet; vor dem ersten Kunden an einem echten Gerät prüfen.
- Ohne Verbindung arbeitet nach einem Neustart die zuletzt angemeldete Person weiter; am Empfangsgerät braucht der Personenwechsel den Server. Ob das reicht, nach dem ersten Praxistest entscheiden.
- Bildschirmfotos aller Ansichten ohne Windows: `AISPA_SCREENSHOTS=$PWD/bilder dotnet test --project tests/AispaStudio.Desktop.Tests`. Aktuelle Fotos liegen in `/mnt/project-files/windows-app/`.
- Wiederkehrende Fallen: Postgres im Container stoppt zwischen Läufen (`service postgresql start`); `dotnet format` bemängelt die Reihenfolge der `using`-Zeilen; Records mit Listen sind nach JSON-Rundreise nicht gleich (in Tests per Id vergleichen).
