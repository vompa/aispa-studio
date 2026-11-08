# 0064. Namensregeln: gültiger Name, Rufname, Titel und Anrede je Land

- **Status:** angenommen
- **Datum:** 20.10.2026
- **Entschieden von:** Auftraggeber (Vorgaben vom 20.10.2026), Ausgestaltung Claude

Die Nummer 0063 ist für die offene Schnittstelle für Personal und Kunden vorgesehen und bleibt dafür frei.

## Kontext

Bisher hatte ein Kunde nur Vor- und Nachname. Day-Spas in Österreich, Deutschland und der Schweiz brauchen mehr: Stammgäste heißen im Haus oft anders („Steffi“ statt Stefanie), Belege und Briefe müssen den richtigen Namen tragen, und akademische Titel gehören in Österreich zur höflichen Anrede. Vorgabe vom Auftraggeber: Anrede (Frau, Herr, divers, keine Angabe) mit ♀/♂ am Termin; Rufname im Haus, gültiger Name nach außen; Titel vor- und nachgestellt; Anrede nach Land des Standorts (AT vorangestellte Titel, bei mehreren der höchste, nie nachgestellte; DE nur Dr./Prof.; CH keine), je Kunde übersteuerbar; Änderungen am gültigen Namen protokolliert, ausgestellte Belege bleiben unverändert. Der Vergleich der Kundenkartei fand bei der Konkurrenz kein Titelfeld mit Anrederegeln.

## Optionen

| Option | Bewertung |
| --- | --- |
| Ein freies Feld „Anzeigename“ | einfach, aber Belege und Nachrichten wüssten nicht, welcher Name gilt |
| Rufname ersetzt den Vornamen im Haus, Vor- und Nachname bleiben der gültige Name | gewählt: eine Regel, überall gleich, Suche findet beide |
| Länderregeln als Einstellung je Spa | flexibel, aber niemand soll Anredegepflogenheiten pflegen müssen; je Kunde übersteuerbar genügt |
| Zusätzliches Feld „Geschlecht“ | doppelt zur Anrede (divers, keine Angabe), ohne Zweck in der Software; widerspricht der Datenminimierung |

## Entscheidung

- **Gültiger Name** = Vor- und Nachname. Er steht auf Rechnungen, Belegen mit Empfänger, Anzahlungsbelegen, Gutscheinen auf Namen, im Buchhaltungsexport, in DSGVO-Auskunft, Online-Konto, Anamnese und in allen Nachrichten an den Gast (Mail, SMS, Erinnerung, KI-Antwort). Jede Änderung wird mit Zeitpunkt und Person protokolliert (`CustomerNameChanges`); ausgestellte Belege behalten den Namen von damals.
- **Rufname** (optional) ersetzt im Haus den Vornamen: Kalender, Hover-Karte, Suche, Kopf der Kundenkarte, Posteingang, Warteliste, „Mein Tag“, Kassa-Oberfläche (Rufname groß, gültiger Name klein). Nie in Nachrichten nach außen.
- **Anrede**: Frau, Herr, divers, keine Angabe; freiwillig. Frau/Herr zeigen klein ♀/♂ hinter dem Namen (anders dargestellt als der Wunsch „Therapeutin/Therapeut“, der ein Kennzeichen ist). Ohne Frau/Herr sprechen Nachrichten mit dem vollen Namen an („Guten Tag Kim Berger“).
- **Pronomen** (freiwillig, Nachtrag 21.10.2026 auf Wunsch des Auftraggebers): frei eingetragen, wenn der Gast sie nennt, höchstens 40 Zeichen; die Kundenkarte zeigt sie in Klammern hinter dem Namen. Nur für das Team, nie in Nachrichten, auf Belegen oder im Online-Konto.
- **Rufname für Mitarbeitende** (Nachtrag 21.10.2026 auf Wunsch des Auftraggebers): Thailändische Namen im Team sind lang und schwer auszusprechen. Wie beim Gast hat auch eine Person im Team einen freiwilligen **Rufnamen** („Name im Haus“, höchstens 40 Zeichen, z. B. „Siri“ für „Siriporn Kaewdee“). Er ersetzt im Haus den vollständigen Namen: Kalender (Spaltenkopf, Initialen, Hover-Karte), „Mein Tag“, Tagesstart, Managementbericht, „Freie Zeit suchen“, KI-Anfrage im Kalender, Assistent und Posteingang; Sortierung nach dem Namen im Haus. Lohn, Export, Zugänge, Einladungen und Belege behalten den vollständigen Namen. Leer heißt: der vollständige Name gilt auch im Haus. Die Texterkennung (KI-Anfrage, Assistent, Posteingang) trifft Wörter beider Namen: „bei Siri“ und „bei Siriporn“ meinen dieselbe Person. Regel an einer Stelle: `EmployeeNames.InHouse` in `src/AispaStudio.Contracts/EmployeeNames.cs`.
- **Titel**: vorangestellt mit Leerzeichen („Mag. Dr.“), nachgestellt mit Komma („MSc, MBA“); Auswahl und frei. Die Adresszeile nennt alle: „Frau Mag. Stefanie Huber, MSc“.
- **Titel in der Anrede**, Land aus der Zeitzone des Standorts: AT der höchste vorangestellte (Rang Prof. > DDr. > Dr. > MMag. > Mag. > DI > Ing. > Berufstitel), DE nur Dr. und Prof., CH keiner; nachgestellte nie. Je Kunde „ja“ (höchster Titel in jedem Land) oder „nein“.
- **Suche und Kundenzuordnung** (ADR 0061) kennen den Rufname und vergleichen Namen ohne Titel.
- Die KI liest Anrede und Titel aus Mail und Signatur nur als **Vorschlag** am Posteingang; übernommen wird nichts von selbst, auch nicht bei automatisch angelegten Kunden ([ADR 0008](0008-ki-schlaegt-vor-mensch-bestaetigt.md)).

## Begründung

Eine feste Regel statt Einstellungen hält die Rezeption frei von Pflegeaufwand und macht Belege eindeutig. Der Rufname als Ersatz des Vornamens deckt den häufigsten Fall ab und lässt den gültigen Namen unangetastet. Die Länderregeln entsprechen der Vorgabe; wo sie für einen Gast nicht passen, genügt der Schalter je Kunde.

## Folgen und Stand

- Regeln in `src/AispaStudio.Contracts/PersonNames.cs` (geteilt von API und Windows-App), Felder an `Customer`, Protokoll `CustomerNameChangeEntry`, Begrüßung in Nachrichten über `src/AispaStudio.Api/Mail/GuestName.cs`.
- Postanschrift des Gastes kommt dazu (getrennt vom Rechnungsempfänger); die Kassa schlägt sie für die Rechnung mit Empfänger vor.
- Nicht deutsche Nachrichten (en, fr, it) sprechen weiter mit dem Vornamen an.
- Offen: Ansprache Sie/Du je Gast, Rangfolge der Titel gegen eine Quelle (Knigge, Post-Richtlinien) prüfen, Einwilligungsmail ohne Standort nimmt Österreich.

## Quellen

- [Hilfe: Name, Anrede und Titel](../../web/src/help/content/kunden-name-und-anrede.md)
- [ADR 0061](0061-kundenzuordnung-eingehender-nachrichten.md)
