Status: closed

# Runde R01 – Durchsicht der Oberflächen

## Quelle
Befundliste docs/umsetzung/aenderungsliste-oberflaechen-2026-10-23.md (eingecheckt, Stand `ee66289`): W1 bis W68 und W-Muster 1 bis 13 (Windows-App), B1 bis B70 und B-Muster 1 bis 4 (Web). Zeilenangaben in der Liste und in den Slices beziehen sich auf diesen Stand und können sich verschoben haben; vor dem Ändern die Stelle im Code suchen.

Umgang je Einstufung der Liste:
- **verstößt** (W 53, B 25): in dieser Runde geschnitten, siehe „Slices“.
- **weicht ab, nicht dokumentiert** (W 18, B 39): offene Entscheidungen, gebündelt unter „Offene Entscheidungen“; nicht geschnitten.
- **weicht ab, dokumentiert** (W-Muster 8, W12, W15, W42, W44, W67): in Ordnung laut Liste, keine Änderung.
- **bekannt** (W31, W36, W54, W68, B1a, B10, B25, B70): stehen schon in docs/stand-und-offene-punkte.md bzw. im Brief 11.4; nicht in dieser Runde.
- Zurückgezogen: B26. Nicht vergeben: B17, B27.

## Intake
Befunde je Slice (Einstufung „verstößt“); Größe nach der Liste (S, M, L), L-Befunde sind auf zwei Slices geteilt.

| Kennung | Slice | Größe |
|---|---|---|
| B4, B56 | 01 | S |
| B5, B6, B64 | 02 | S |
| W5, W9, W18, W22, W-Muster 2 und 3 (Teil) | 03 | S–M |
| W10, W11, W13, W16, W17, W-Muster 2, 3, 4, 13 (Teil) | 04 | M |
| W33, W46, W48, W63, W-Muster 2 und 3 (Rest) | 05 | M |
| W1, W2, W6, W7, W21, W-Muster 1 (Teil), W-Muster 11 | 06 | M |
| W51, W52, W55, W-Muster 1 (Teil) | 07 | M |
| W37, W39, W43, W-Muster 1 (Teil) | 08 | M |
| W45, W49, W-Muster 1 und 13 (Teil) | 09 | M |
| W28, W29, W30, W32, W34, W35, W-Muster 1, 4, 13 (Teil) | 10 | M |
| W64, W-Muster 7 | 11 | M |
| B37 | 12 | M |
| B7 | 13 | M |
| B1, B47 | 14 | S |
| W38 (Teil 1), W-Muster 4 (Teil) | 15 | L → M |
| W38 (Teil 2) | 16 | L → M |
| W50 (Teil 1) | 17 | L → M |
| W50 (Teil 2) | 18 | L → M |
| W26, W47 (Reiter), W-Muster 5 | 19 | M |
| W14, W20, W61, W47 (Zeilen), W-Muster 6 | 20 | M |
| W53, W-Muster 4 (Teil) | 21 | M |
| B3 | 22 | M |
| B12, B13, B16 | 23 | M |
| B18, B19, B21, B23 | 24 | S–M |
| B45, B46, B38, B53 | 25 | S–M |
| B58 | 26 | S |
| B62 | 27 | S |
| W24, W60, W-Muster 10, W-Muster 13 (Rückfragedialog) | 28 | S |
| B8, B34 | 29 | S |

## Slices
| Slice | Name | Befunde |
|---|---|---|
| 01 | Tabellenköpfe mit scope und Zahlenspalten rechts | B4, B56 |
| 02 | Bewegung, Hochkontrast und Druckzustand im Web | B5, B6, B64 |
| 03 | Meldungen der Anmelde-, Start- und Verbindungsseiten als InfoBar | W5, W9, W18, W22, W-Muster 2, 3 |
| 04 | Meldungen und Primärknöpfe in Sicherung, Abgleich und Geräten | W10, W11, W13, W16, W17, W-Muster 2, 3, 4, 13 |
| 05 | Meldungen in Kalender, Kunden, Kassa und Posteingang; Stil Border.alert entfällt | W33, W46, W48, W63, W-Muster 2, 3 |
| 06 | Feldnamen und Anrede in Anmeldung, Einrichtung und Verbindung | W1, W2, W6, W7, W21, W-Muster 1, 11 |
| 07 | Feldnamen im Kalender und Zähler „Personen“ als Zahlenfeld | W51, W52, W55, W-Muster 1 |
| 08 | Feldnamen, Warnfarbe und Speichern-Hinweis in den Kunden | W37, W39, W43, W-Muster 1 |
| 09 | Feldnamen und Knopfverben in der Kassa | W45, W49, W-Muster 1, 13 |
| 10 | Posteingang und Leiste „KI und Hilfe“: Namen, KI-Kennzeichen, ein Primärknopf | W28, W29, W30, W32, W34, W35, W-Muster 1, 4, 13 |
| 11 | Namen nie aus Platzhaltern, Listenzeilen benannt, Namens-Scan streng | W64, W-Muster 7 |
| 12 | Geldeingabe an der Kassa im Web mit Fehlertext statt stiller 0 | B37 |
| 13 | Preise in den Stammdaten als Geldfeld | B7 |
| 14 | Rückfragen im Web: Fokus auf Abbrechen, Dienstplan fragt vor dem Verwerfen | B1, B47 |
| 15 | Kundenkarten: nur eine Karte im Bearbeiten-Zustand | W38, W-Muster 4 |
| 16 | Kundenkarten: Esc, Enter und Startfokus | W38 |
| 17 | Terminkarten mit Namen und Rolle für Screenreader | W50 |
| 18 | Pfeiltasten-Auswahl im Kalender wird angesagt | W50 |
| 19 | Reiter und Aufklapp-Köpfe melden Auswahl und Aufklappen | W26, W47, W-Muster 5 |
| 20 | Knopfnamen mit Bezug statt Zahl, Uhrzeit oder Initialen | W14, W20, W61, W47, W-Muster 6 |
| 21 | Ein Primärknopf je Fläche im Termin-Panel, Abbrechen als Ghost | W53, W-Muster 4 |
| 22 | Reiter im Web mit Pfeiltasten, Roving-Tabindex und Tabpanel | B3 |
| 23 | Shell der Verwaltung: main, h1, Skip-Link, Seitentitel, Kontomenü als Popover | B12, B13, B16 |
| 24 | Hilfe, Begleiter, Assistent und Abgleich im Web: Fokus und Ansagen | B18, B19, B21, B23 |
| 25 | Dienstplan lesbar, Meldungen der Kasse und Buchhaltung mit Rolle | B45, B46, B38, B53 |
| 26 | Abo-Stufe im Betreiberbereich nur mit Knopf und Rückfrage | B58 |
| 27 | Hausschrift Inter in der Verwaltung ausliefern | B62 |
| 28 | Kleinigkeiten Windows-App: Dialogfokus, Datumsauswahl, Richtungswörter, KI-Knöpfe | W24, W60, W-Muster 10, 13 |
| 29 | Kleinigkeiten Web: Beschriftungen im Buchungsfeld, Standorte als Gruppe | B8, B34 |

## Entscheidungen
Ruling: work/conventions.md und der Slice-Zuschnitt (29 Slices) bestätigt; die offenen Entscheidungen bleiben offen – Auftraggeber, 24.10.2026
Ruling: Slice 01 – Plan mit den Annahmen 1–5 freigegeben („Soll“/„Haben“ bleiben links, „USt“ ist Zahlenspalte, leerer Kopf der Kassa heißt „Aktionen“, Prüfung liest den Quelltext, kein Vermerk in der Befundliste) – Auftraggeber, 24.10.2026
Ruling: Slice 02 – Plan mit den Annahmen 1–5 freigegeben (Skalierung 0,97 nach Brief 4.6 statt 0,98, Druckfläche surface-3 nur für Knöpfe ohne eigene Farbe, Terminkarten/Links/Lager ohne Druckzustand, Hover in @media (hover: hover) in allen CSS-Dateien der Verwaltung, globaler Block für reduzierte Bewegung, kein eigener forced-colors-Block) – Auftraggeber, 24.10.2026
Ruling: Slice 03 – Plan mit den Annahmen 1–5 freigegeben (Abgleich-Banner als Fehler mit Liste darunter, „Verbindung prüfen“ und „Sicherung öffnen“ ohne Schließen-Kreuz, höchstens zwei Banner mit „Alle zeigen“, Leerlauf oben mittig, Fehler der Anmelde- und Verbindungsseiten ohne eigene Aktion) – Auftraggeber, 24.10.2026
Ruling: Slice 12 – Plan mit den Annahmen 1–7 freigegeben (Fehlertexte ohne „bitte“ nach texte.md T8, die Windows-App zieht später nach; Knöpfe bleiben gesperrt statt Fokussprung ins erste ungültige Feld; Fehler ab dem ersten Verlassen und beim Tippen neu geprüft, von der Kassa geleerte Felder starten ohne Fehler; Pflichtfeld nur „Gezählter Bestand“; Baustein `MoneyField` in `web/src/pos/`, Beschriftungen, Platzhalter und Breite 98 px unverändert; Anzeigeformat mit festen Trennzeichen; ein Hilfepunkt in `seite-pos.md` ohne neues Bild, kein ADR) – Auftraggeber, 25.10.2026
Ruling: Slice 29 – Plan mit den Annahmen 1–7 freigegeben (umschließendes `<label>` wie beim Suchfeld „Kunde“ statt `Field` aus dem Lager, Platzhalter entfallen, `autoComplete="off"` an allen vier Feldern, Gruppe heißt per `aria-labelledby` nur „Standorte“, Aussehen ohne Rahmen wie bisher, bestehender Test greift über die Beschriftung, keine Hilfe- oder Bildänderung, kein ADR) – Auftraggeber, 25.10.2026
Ruling: Slice 13 – Plan mit den Annahmen 1–7 freigegeben (Preis der Behandlung und der Preisregel ist Pflicht, 0 bleibt erlaubt; „Speichern“ bzw. „Preisregeln speichern“ gesperrt statt Fokussprung; Preis als Text im Formular; `MoneyField` bekommt `dataField` für das Sprungziel des Assistenten; Breite 98 px und Beschriftung „Preis“ wie an der Kassa; „Preis je Standort“ bleibt `type="number"`; Hilfe in `behandlungen.md` ohne neues Bild, kein ADR) – Auftraggeber, 25.10.2026
Ruling: Slice 04 – Plan mit den Annahmen 1–8 freigegeben (InfoBar bekommt `Detail` für einmal gezeigte Codes; Schlüssel-Warnung der Sicherung und Schlüsselcode des Abgleichs als Warnung mit Aktion „Verstanden“; Fehler der Sicherung, der Geräte und der Verwaltung als Error, Abgleichfehler und Terminal-Status als Warning; Abgleich-Felder in `FormField`, Sicherungsordner mit Namen statt FormField; „Diesen PC als Empfangsgerät anmelden“ sekundär; Statuszeilen polite statt Toast; Hilfe mit „Warnung“ und „Verstanden“, nur `sicherung.png` neu, kein ADR) – Auftraggeber, 25.10.2026
Ruling: Slice 27 – Plan mit den Annahmen 1–8 freigegeben (Inter 3.19 aus dem offiziellen Release wie die App mit 3.019, OFL 1.1 mit Lizenztext und Herkunftsnotiz im Repository; Teilmenge einmalig lokal mit fonttools erzeugt, kein npm-Paket; Teilmenge Latin, Gewicht 400–600, kein Kursiv; Datei unter `web/src/assets/fonts/` mit Hash-Namen; Hilfe erbt Inter über `index.css`, Preload nur in der Team-Oberfläche; Kundenbereich, „Mein Tag“ und Betreiber bleiben bei der Systemschrift, vermerkt im Brief 3; kein ADR, keine Hilfeänderung) – Auftraggeber, 25.10.2026
Ruling: Slice 22 – Plan mit den Annahmen 1–7 freigegeben (alle elf Reiterschienen samt den fünf Lager-Nutzern, ein Panel je Schiene mit `aria-controls` von jedem Reiter, automatische Aktivierung mit Umlauf wie im Lager, Panel ohne eigenen Tab-Stopp, Panel ersetzt `.calendar-body` bzw. bekommt `.admin-panel`/`.stock-tab-panel` für gleiche Abstände, Komponente in `web/src/Tabs.tsx`, nur ein Satz im Brief 5.4, keine Hilfe, kein Bild, kein ADR) – Auftraggeber, 25.10.2026
Ruling: Slice 14 – Plan mit den Annahmen 1–8 freigegeben („Abbrechen“ hat den Fokus in allen fünf Rückfragen, auch bei „Archivieren“; Fokus per `ref` nach `showModal()` statt `autoFocus`; Setup-Datei `web/src/vitest.setup.ts` mit browsergetreuem `showModal()` für alle Web-Tests; eigener `DiscardDraftsDialog` im Dienstplan ohne gemeinsamen Baustein; Hilfesatz zum Verwerfen geändert; Rückkehr des Fokus zum Auslöser bleibt für B2; kein Vermerk in der Befundliste; Testliste schreibt „löschen“ statt „archivieren“) – Auftraggeber, 25.10.2026
Ruling: Slice 14 – Rückfrage zur Anzahlung (ReturnDepositDialog) ebenfalls auf Abbrechen fokussiert, weil ihr Test es verlangt; CancelAppointmentsDialog bleibt für B2 – Entscheidung beim Bau, 25.10.2026
Ruling: Slice 26 – Plan mit den Annahmen 1–8 freigegeben (Fokus der Rückfrage auf „Abbrechen“ wie Slice 14, obwohl Brief 5.27 bei neutraler Rückfrage den primären Knopf nennt, die Aktion bleibt primär statt rot; Titel „Abo-Stufe ändern?“, Text mit Spa, alter und neuer Stufe und deren Modulen, Erfolg „Stufe geändert.“ mit `role="status"`; „Stufe ändern“ gesperrt, solange die Auswahl der aktuellen Stufe entspricht; Abbrechen setzt die Auswahl zurück; eigenes Dialog-Stück in `operator.css`; keine Hilfe, kein Bild, kein ADR; Rückkehr des Fokus bleibt für B2) – Auftraggeber, 25.10.2026
Ruling: Slice 26 – nach dem Schließen der Rückfrage (Abbrechen, Esc, Bestätigen) geht der Fokus ins Auswahlfeld „Abo-Stufe“, weil der Auslöser „Stufe ändern“ dann gesperrt ist und der Browser den Fokus sonst auf `body` fallen lässt (in der Sichtprüfung gefunden, mit Test) – Entscheidung beim Bau, 25.10.2026
Ruling: Slice 25 – Plan mit den Annahmen 1–8 freigegeben (Vorspann „Name, Tag:“ statt `aria-label`, bestehende Tests greifen mit Komma; globale Klasse `.visually-hidden` in `index.css`; alle Zellenwarnungen sichtbar; Zeilenwarnung unter der Stundenzahl; Kasse `role="alert"` nach derselben Bedingung wie die Farbe; Baustein `ActionNotice` unter der Aktionsleiste des Abschnitts, bei Personalnummern unter der Tabelle; fehlender BU-Schlüssel beim DATEV-Export als Fehler; keine Hilfe- oder Bildänderung, kein ADR) – Auftraggeber, 25.10.2026
Ruling: Slice 23 – Plan mit den Annahmen 1–8 freigegeben (unsichtbares `h1` mit dem Namen des Seitenreiters, sichtbare `h2` der Seiten bleiben; eingebettet dasselbe; Skip-Link „Zum Inhalt springen“ wie im Kundenbereich; abgemeldet und in der Einladung Titel „AISPA Studio“; `main` umfasst PIN-Formular, Meldung, Sperrhinweis und Seite; Popover nach dem Muster der Aktionen ohne `role="menu"`; Popover-Näherung in `vitest.setup.ts` statt neuem Paket, einmal im echten Chrome nachgesehen; keine Hilfe, kein Bild, kein ADR) – Auftraggeber, 25.10.2026
Ruling: Slice 17 – Plan mit den Annahmen 1–5 freigegeben (Name nennt nur den Beginn, Status immer im Namen auch „Gebucht“, Karte ist ein Listeneintrag mit Auswahlzustand, „Auswählen“ per Screenreader-Befehl ohne Wirkung und ohne Auswahlbehälter, Zettel und Streifen mit Rolle Text über `AutomationProperties` und dem Vorsatz „Zettel“, kein Tab-Stopp, keine Hilfe, kein Bild, kein ADR) – Auftraggeber, 25.10.2026
Ruling: Slice 05 – Plan mit den Annahmen 1–8 freigegeben (Fehler als Error, Warnungen der Kunden und der Kassa sowie der Rechnungshinweis als Warning, Posteingang ohne Fehler als Info; Gesundheitswort-Warnungen beim Erscheinen sofort angesagt; Kartenzahlung als InfoBar mit „Abbrechen“/„Stornieren“ als Aktion, der Tooltip entfällt; KI-Wunsch mit dem Weg ohne KI als Detail und „Freie Zeit suchen“ als Aktion; Fehler der drei Buchungsleisten über dem Buchen-Knopf; kein „Erneut versuchen“ und kein Schließen-Kreuz ohne vorhandenen Befehl; Wechselgeld und Druckstatus höflich angesagt; HelpView mitgezogen, nur meldungen.md nachgezogen, keine Hilfeänderung, kein ADR) – Auftraggeber, 25.10.2026
Ruling: Slice 24 – Plan mit den Annahmen 1–6 freigegeben (Fokus beim Öffnen der Hilfe auf die Überschrift, nicht aufs Suchfeld; Themenwechsel in der offenen Hilfe verschieben den Fokus nicht, ein anderes Fragezeichen auf der Seite wird der neue Auslöser; Knopf des Begleiters bleibt bedingt gerendert, Fokus per `ref`; Strg+Leertaste von der Seite aus lässt den Fokus, wo er ist; Abgleich-Popover ohne `aria-expanded` und `aria-controls` von Hand; Live-Region umfasst die ganze Antwort, „Gebucht …“ und „Daten gespeichert …“ bleiben) – Auftraggeber, 25.10.2026
Ruling: Slice 08 – Plan mit den Annahmen 1–7 freigegeben (Beschriftungen der Wünsche aus den bisherigen Platzhaltern, „Raum“ heißt „Raumtemperatur“; Pflichtstern bei „Empfänger“ und „Anschrift“; auch „Person“ bei „Termine am liebsten“ sowie „Kennzeichen“ und „Grund“ beim Kennzeichen-Setzen in `FormField`, Freigabe und PIN beim Rabatt nicht; „Neue Kundin“ nur mit `AutomationProperties.Name`; Fragezeichen bleibt neben „Wünscht“; Hilfe „Termine am liebsten“ nennt „Person“, „Frühester Beginn“, „Spätestes Ende“; Warnsymbol ohne eigenen Namen) – Auftraggeber, 25.10.2026
Ruling: Slice 06 – Plan mit den Annahmen 1–7 freigegeben („Wer meldet sich an?“, „Ihr Name“ und Sätze in Sie ohne „bitte“; die Web-Anmeldung zieht mit, die Ausnahme `appLabelsInDuForm` in `textRules.test.ts` entfällt; Regeln wie „mindestens 8 Zeichen“ als Hilfe unter dem Feld, Platzhalter nur noch als Beispiel; Stern an jedem Feld, das der Knopf der Maske verlangt, auch in der Einrichtung; Konto-Flyout „PIN“ bzw. „Neue PIN“ mit der Ziffernspanne als Hilfe; nur `anmeldung.png` und `anmeldung-empfang.png` neu; W3, W4 und `NameFromPlaceholder` unverändert) – Auftraggeber, 25.10.2026
Ruling: Slice 10 – Plan mit den Annahmen 1–7 freigegeben (Live-Region am Antworttext statt am Rahmen der Karte, weil Avalonia nur Namensänderungen ansagt; Frage nur mit `AutomationProperties.Name`; Betreff vor dem Antworttext, „Antwort an …“ wird Beschriftung des Textfelds; „förmlicher (Sie)“ heißt „Siezen“, kein „(KI)“ an den Knöpfen; HelpView unverändert, weil das Suchfeld seit T-010 in der Web-Hilfe liegt; Platzhalter nur als Beispiel; sechs Hilfebilder neu, Pille in der Zeile ohne eigenen Namen) – Auftraggeber, 25.10.2026
Ruling: Slice 07 – Plan mit den Annahmen 1–7 freigegeben (der Zähler „Personen“ ist der des Sauna-Eintritts, die Abnahme „2 Personen“ prüft ihn dort; Neue Kundin im Termin-Panel mit sichtbaren Beschriftungen statt Platzhalter, ohne Umbau nach W54; Beschriftungen mit Fragezeichen und Überschriften per `LabeledBy`; Freigabe-Auswahl mit der Beschriftung „Leitung“; Kontrollkästchen im Ausfall-Assistenten heißen „Gast, bisheriger Termin“; `NameFromPlaceholder` bleibt bis Slice 11; nur Schritt 5 der Hilfe „Sauna- und Ruheraum-Eintritt“ ändert sich, kein ADR) – Auftraggeber, 25.10.2026
Ruling: Slice 15 – Plan mit den Annahmen 1–6 freigegeben (Öffnen einer anderen Karte verwirft die Eingabe der offenen wie „Abbrechen“, ohne Rückfrage; die Stammdaten im Kopf zählen als siebte Bearbeitung; „Neue Kundin / neuer Kunde“ ist kein Primärknopf, solange etwas bearbeitet wird oder „Neue Kundin“ offen ist; „Kennzeichen setzen“ ist der Bearbeiten-Zustand der Karte „Kennzeichen“; zwei Bildtests aus Slice 08 prüfen die Karten nacheinander; kein ADR) – Auftraggeber, 25.10.2026
Ruling: Slice 16 – Plan mit den Annahmen 1–6 freigegeben (Esc in Stufen: offene Auswahl, dann ungespeichertes Feld, dann Karte, ohne Rückfrage; Enter löst „Speichern“ der offenen Karte aus, im mehrzeiligen Feld Strg+Enter, gesperrtes Speichern bleibt gesperrt; nur die sechs Karten, nicht die Stammdaten im Kopf; Fokus nach dem Schließen zurück auf den Öffner der Karte, wenn er in der Karte lag oder weg ist; Startfokus „Kennzeichen“ bzw. ohne Recht „Abbrechen“, „Termine am liebsten“ bei „Person“; ein Hilfesatz, Bilder nur bei sichtbarer Änderung, kein ADR) – Auftraggeber, 25.10.2026
Ruling: Slice 16 – Enter speichert nur aus Eingabefeldern, nicht aus einer Auswahl, einem Kontrollkästchen oder den Wochentagen (Abweichung von Annahme 2): Avalonia öffnet die Auswahl bzw. löst den Knopf mit Enter selbst aus, und masken.md R13 sowie fluent2-avalonia („Combo box: Enter selects, closes“) verlangen den Standardknopf nur aus dem einzeiligen Feld; Hilfe und Plan sagen es so – Entscheidung beim Bau, 25.10.2026

Ruling: Slice 09 – Plan mit den Annahmen 1–6 freigegeben (Beschriftungen „Behandlung“, „Freigabe durch“, „PIN“, „Gutschein-Code“, „Name oder Firma“, „Anschrift“, „UID-Nummer“, „Grund“, „Notiz“; Platzhalter nur als „z. B.“-Beispiel; Pflichtstern nach der Freigaberegel der Knöpfe; Rabattfeld bleibt; Abstände in den Karten bleiben; `NameFromPlaceholder` bleibt für Slice 11; Hilfe nur an den genannten Stellen, kein ADR) – Auftraggeber, 25.10.2026
Ruling: Slice 09 – „Wertgutschein verkaufen“ und „Behandlung verkaufen“ stehen unter ihrem Feld statt daneben, weil die längeren Knöpfe die Felder in der 330 px breiten Spalte auf rund 50 px zusammendrückten (im Bild gefunden, mit Breitenprüfung im Test) – Entscheidung beim Bau, 25.10.2026
Ruling: Slice 28 – Plan mit den Annahmen 1–5 freigegeben (Fokus im Feld auch beim zerstörenden Storno, Marke am Ende des vorbelegten Grundes; Nachbartage in fg-2 zeigen statt ausblenden; die sieben Ersatztexte ohne Richtung; „Mit Entschuldigung (KI)“ bleibt; kein ADR, Satz in Brief 5.27) – Auftraggeber, 25.10.2026
Ruling: Slice 28 – „Freundlicher formulieren (KI)“ und „Förmlicher formulieren (KI)“ bleiben im Wortlaut der Befundliste, obwohl sie über 26 Zeichen haben; sie stehen als begründete Ausnahme (umbrechendes `WrapPanel`) im Längentest – Auftraggeber, 25.10.2026
Ruling: Slice 19 – Plan mit den Annahmen 1–5 freigegeben (eigene Unterklasse `TabButton` statt `RadioButton`/`ListBox`, die Klasse `on` setzt der Reiter aus `IsSelected`; Rolle TabItem an allen sieben Stellen samt „Einlage/Entnahme“, „Team/Räume“ und den Einträgen der Klappliste „⋯ n“, die Zustandschips `HeadClosure` und `HeadStaffFilter` bleiben Knöpfe; „Auswählen“ per Screenreader wirkt wie ein Klick, ohne Auswahlbehälter; Aussehen, Hilfe, Bilder und ADR unverändert; Quelltextprüfung als Test der Desktop-Suite) – Auftraggeber, 25.10.2026
Ruling: Slice 20 – Plan mit den Annahmen 1–4 freigegeben (Wortlaut der Namen wie im Plan, Banner der Shell „Erledigt: {Beschreibung}“ ohne eigene Zeit; `AccessibleName` im Modell bei Navigationszeile, Als Nächstes, Teilen der Buchung und offenen Terminen der Kassa; „Abschnitt entfernen“ gehört dazu; keine Hilfe, keine Bilder, kein ADR) – Auftraggeber, 25.10.2026
Ruling: Slice 21 – Plan mit den Annahmen 1–6 freigegeben (Bestätigen der offenen Bearbeiten-Karte ist der eine Primärknopf, Einchecken/Kassieren dann sekundär; „Gast eintragen“ und „Gruppe einchecken“ immer sekundär; zwei gleichzeitig offene Karten bleiben zurückgestellt; Reihenfolge der Knöpfe bleibt; „Abbrechen“ auch im Tagesbetrieb und in der Warteliste Ghost; Satz in Brief 5.12 Punkt 6, kein ADR) – Auftraggeber, 25.10.2026

Ruling: Slice 11 – Plan mit den Annahmen 1–5 freigegeben (Zeilenname ist der sichtbare Text der Zeile in Lesereihenfolge; Stil je ListBox in `ListBox.Styles`; „Freigabe durch“ und „PIN“ der Kundenkarte als Pflichtfelder in `FormField` wie in der Kassa, „Weitere E-Mail-Adresse“ nur mit Namen; Quelltextprüfung der Felder für alle Ansichten plus Prüfung der Listen; keine Bilder, kein ADR) – Auftraggeber, 25.10.2026
Ruling: Slice 18 – Plan mit den Annahmen 1–5 freigegeben (Fokus auf die Karte statt Ansage, weil Avalonia 12 keine UIA-Notification kennt; nur Pfeiltasten verschieben den Fokus, Klick, Suche, Meldungen, Alt+Pfeile und Esc nicht; Karte ohne Tab-Stopp und ohne eigenen Fokusrahmen, die Stelle zeigt der Auswahlring; keine Hilfe, kein Bild, kein ADR) – Auftraggeber, 25.10.2026
Ruling: Slice 18 – die Karte bekommt den Fokus ohne Navigationsart (`Focus()` statt `NavigationMethod.Directional`), weil `ToolTipBehavior` bei Pfeil-Fokus nach der Verzögerung die Hover-Karte öffnet und sie bei jedem Pfeil über den Nachbarterminen läge (in der Durchsicht gefunden, mit Test) – Entscheidung beim Bau, 25.10.2026
## Überraschungen
- Slice 01: B56 war teils veraltet. „Soll“ und „Haben“ der Buchhaltung zeigen Kontonummern, keine Beträge (bleiben links); die Köpfe der Kosten-Seite trugen `amount` schon. Rechtsbündig fehlten nur „Betrag“, „USt“ (Buchhaltung) und „Menge“ (Lohn).
- Slice 02: Slice und Befund B64 nannten `scale(0.98)`, Brief 4.6 verlangt 0,97 (umgesetzt nach Brief). Zwei Regeln bündelten Hover mit einem Dauerzustand (`.topbar .account[open] summary`, `.stock-toast button.stock-btn.ghost`); beim Umhüllen wären diese Zustände auf Touch-Geräten verloren gegangen. Getrennt und als zusätzliche Prüfung im Test festgehalten (in `@media (hover: hover)` nur Hover-Selektoren). Der Bildvergleich im Browser aus dem Plan entfiel: Kontrastdesign und „Animationseffekte aus“ lassen sich nur unter Windows echt prüfen, sie stehen in der manuellen Testliste.
- Slice 03: Der Test für „Verbindung prüfen“ war zuerst falsch gebaut (`StubApi` zeichnet bei `Down` keine Aufrufe auf); korrigiert und um „danach wieder online“ ergänzt. Der Test für „Sicherung öffnen“ entstand erst nach dem Code. `ViewTests` brauchen allein mehr als 10 Minuten; die Desktop-Suite lief deshalb in zwei Teilen. Das Handbuch `kalender-offline` beschrieb die Banner noch als „gelb“ und „rot“ und ist angepasst.
- Slice 12: Der Befund nennt acht Betragsfelder; die genannten Stellen sind zehn, dazu kommt der Anteil je Position beim Teilen der Rechnung: elf Felder, alle umgestellt. `Intl.NumberFormat('de-AT')` ohne Währung trennt Tausender mit geschütztem Leerzeichen statt Punkt; das Anzeigeformat im Feld nutzt deshalb feste Trennzeichen wie die Windows-App. Die Durchsicht fand drei Lücken, die der Plan nicht vorsah: ausgeblendete Felder (Anteile ohne „Rechnung teilen“, Betrag eines Behandlungsgutscheins) sperrten „Bezahlen“ ohne sichtbaren Grund; „Rest bar“, „Rest Karte“, QR und Terminal rechneten ein unlesbares Feld als 0; das Verlassen des Treatwell-Felds fror den Vorschlag ein. Alle drei behoben, je mit Test; der Fehler erscheint jetzt nur beim Verlassen und steht beim Tippen, bis der Wert gültig ist (wie `ParsedField` der App).
- Slice 29: Der obere Rand einer `legend` wirkt im `fieldset` nicht; der Abstand über „Standorte“ steht deshalb am `fieldset` (Plan: an der Legende). Im `fieldset` fallen Ränder nicht mehr zusammen: der Hinweis beim Backoffice rückte 12 px weiter von Legende und Folgezeile ab (24/28 statt 12/16 px, von der Durchsicht gefunden, in der Vorschau gemessen); `.admin fieldset.group-fieldset > p` setzt den Rand auf 0, danach gleich wie vorher. `CustomerPicker` steht auch im Assistenten außerhalb von `.panel`; die Beschriftungen dort bekamen dieselbe Regel (`.new-customer label`) für Lage, Gewicht und Farbe.
- Slice 13: Der Pflichtstern ändert den Text der Beschriftung zu „Preis *“; der bestehende Preisregel-Test griff das Feld exakt mit „Preis“ und fand es nicht mehr, die Abfrage heißt jetzt `/^Preis( \*)?$/` (ein loses `/^Preis/` trifft auch die Gruppen „Preisregel 1“ und „Preise nach Zeit“). Die Durchsicht fand zwei Lücken: Die Preisregeln hatten `key={index}`, beim Entfernen einer Regel wanderte der Fehlerzustand des Preisfelds zur nächsten Regel (Fehlertext weg, Knopf grau ohne Grund); jetzt hat jede Regel einen eigenen Schlüssel. Der Platzhalter von „Preis je Standort“ zeigte den Eingabetext des Grundpreises („1.234,00“, auch „89,9x“) in einem Zahlenfeld; jetzt die gelesene Zahl. Beides mit Test.
- Slice 04: Das Handbuchbild `sicherung.png` zeigte trotz der Bildunterschrift „mit neuem Wiederherstellungsschlüssel“ keinen Schlüssel und noch das Sicherungs-Banner im Stil vor Slice 03; der Bildtest `Sicherung_mit_neuem_Wiederherstellungsschluessel` erzeugt den Schlüssel jetzt wirklich. Außer den drei geplanten Hilfestellen sprach auch `abgleich.md` (Abgleichfehler „im gelben Kasten“) vom alten Kasten; angepasst, ebenso die Doc-Kommentare in `BackupViewModel` und `SyncViewModel`. In der Prüfkette blieben zwei Fremdtests rot, auch auf `dev` und bei geringer Last: `ViewTests.Schliessen_blendet_die_InfoBar_aus_und_der_Toast_bleibt` (Toast läuft nach 5 s echter Zeit ab) und `web/src/help/helpPage.test.tsx` (Hilfe-Index lädt länger als 1 s); nicht in dieser Slice behoben, Slice trotzdem geschlossen.
- Slice 27: Die Windows-App bettet Inter 3.019 als statische TTF ein; das Web nimmt dieselbe Version als Variable-WOFF2 (39,8 kB nach Teilmenge und Begrenzung auf 400–600). Vite schreibt den Preload in `dist/index.html` als `crossorigin />`; die Dist-Prüfung war zuerst zu eng und ist angepasst. Die Durchsicht fand Zeichen, die Oberfläche und Hilfe als Text nutzen und die außerhalb der Teilmenge „latin“ lagen (← → neben ↑ ↓ aus Inter, ✓ ★ ▶ ⚠): soweit Inter sie enthält, sind sie in die Teilmenge aufgenommen. Der Fall „Kundenbereich lädt keine Schrift“ war vor der Umsetzung schon grün (Quelltext-Test und Dist-Prüfung bewachen nur); dass die Dist-Prüfung anschlägt, ist an einer Kopie von `dist` mit eingesetztem Preload nachgestellt. Der OFL-Text nennt den Schriftautor mit Namen; er bleibt unverändert, weil die Lizenz das verlangt. Unter Volllast anderer Sitzungen (CPU 100 %) waren Hilfe- und Begleiter-Tests auch auf `dev` ohne diese Slice rot (erstes Nachladen eines Themas über 1 s).
- Slice 22: Das Panel heißt nach dem gewählten Reiter; in der Behandlung trägt es dadurch denselben Namen „Material“ wie das Feld darin, und `getByLabelText('Material')` in `MasterDataPage.test.tsx` fand zwei Elemente. Die Abfrage prüft jetzt `getByRole('textbox', { name: 'Material' })` (gleiches Feld, zusätzlich mit Rolle). Die Durchsicht fand eine Lücke, die der Plan nicht vorsah: Der Link „Bestellung“ in den Lager-Bewegungen wählte auch ohne Recht `ManageLocation` den Reiter „Bestellungen“, den es dann nicht gibt; mit dem Roving-Tabindex hatte die Schiene so keinen Tab-Stopp mehr. Ohne Recht steht „Bestellung“ jetzt nur als Text (Test in `StockPage.test.tsx`). Dazu behoben: Alt, Strg und Meta mit Pfeil, Pos1 oder Ende bleiben beim Browser; der Quelltext-Scan findet auch `role = 'tab'`, `role: 'tab'`, bedingte Werte und `.ts`-Dateien. Die drei Hilfe-Testdateien waren nur unter Last rot: der erste Import aller Hilfetexte dauerte länger als die 1 s von `findBy…`; die Gegenprobe mit `help/content/seite-pos.md` im Stand vor Slice 12 war ein Zufallstreffer (ohne Last sind die Tests auch mit dem Hinweis aus Slice 12 grün). Nachgezogen auf `dev`: die drei Dateien laden die Themen in `beforeAll` vorab, danach ist die Gesamtsuite mit `--maxWorkers=3` grün (496 Tests in 62 Dateien).
- Slice 14: Das browsergetreue `showModal()` in `web/src/vitest.setup.ts` machte einen sechsten Test rot: `web/src/calendar/DepositSection.test.tsx` verlangt, dass „Abbrechen“ in der Rückfrage zur Anzahlung (`ReturnDepositDialog`) den Fokus hat. Der Dialog hatte denselben Fehler (B1) und war nur grün, weil jsdom kein `showModal()` kannte; aus demselben Grund verdeckten die zwei bestehenden Fokus-Prüfungen (Schließzeiten, Raumsperren) den Fehler. Der Test blieb unverändert, der Dialog bekam die Behebung in einem eigenen Commit (siehe Ruling). Der Plan hatte `ReturnDepositDialog` und `CancelAppointmentsDialog` als unverändert vorgesehen, weil dort angeblich kein Test den Fokus prüft; beide haben eine solche Prüfung (`DepositSection.test.tsx`, `ClosuresPage.test.tsx`).
- Slice 26: Der bestehende Test „wechselt die Abo-Stufe eines Spas“ prüfte genau das Sofort-Verhalten aus B58 (`change` → `PUT`). Er ist ersetzt: die Erfolgsprüfungen (Pfad, Schlüssel, Rumpf, Neuladen, axe) stehen im Abnahmetest, der Fehlerfall 400 „Unbekannte Abo-Stufe.“ in „zeigt den Fehler, wenn die Abo-Stufe nicht geändert werden kann“, jetzt über Knopf und Rückfrage; nichts ist abgeschwächt. Der erste Entwurf des Pfeiltasten-Tests scheiterte zuerst am fehlenden Knopf statt am Sofortaufruf; die Prüfung „Knopf gesperrt“ steht deshalb im Abbrechen-Test, damit das Rot den Befund zeigt. jsdom wechselt den Wert eines `<select>` bei Pfeiltasten nicht; der Test stellt den `change` nach, den Chrome und Edge danach feuern. Die Sichtprüfung im Browser (Vite mit gestubbter API) zeigte einen Fokusverlust, den die Slice selbst erzeugt hätte: nach Abbrechen setzt die Auswahl zurück, „Stufe ändern“ wird gesperrt, und der Fokus fiel auf `body`; behoben (siehe Ruling), der Abbrechen-Test prüft den Fokus.
- Slice 25: Der Abnahmetest der Dienstplan-Zelle nahm das Datum als „Mo. 1.3.“ an; `dayHead` liefert „Mo., 1.3.“, der Name lautet also „Lena Huber, Mo., 1.3.: 09:00–17:00 ⚠ …“ (Test berichtigt, nicht abgeschwächt). Für den Fall „übrige Warnung mit `role="status"`“ war eine Kasse außer Betrieb geplant; die Kasse wählt eine Kasse außer Betrieb aber nie vor, ihre Warnung erscheint also erst nach Auswahl. Der Test nimmt stattdessen den Hinweis der Test-Signatureinrichtung (`RksvFiscalization.Notice`). Der bestehende DATEV-Test erwartete den Hinweis zum fehlenden BU-Schlüssel als `status`; er erwartet jetzt `alert` im Abschnitt „Export“ (Annahme 7).
- Slice 23: jsdom 29 kennt kein Popover, und seine Standardvorlage blendet jedes `[popover]` dauerhaft aus (`:popover-open` trifft dort nie); ohne Näherung wäre der Inhalt des Kontomenüs in Tests unsichtbar. Die Näherung in `web/src/vitest.setup.ts` zeigt ein offenes Popover per Inline-`display`. Der erste Prüflauf im echten Chrome hing, weil das Route-Muster `**/api/**` auch das Modul `/src/api/client.ts` des Vite-Servers abfing; das Muster prüft jetzt den Pfadanfang. Im Gesamtlauf von Vitest war einmal `time/TimesheetPage.test.tsx` rot (1,6 s gegen 1 s von `findBy`, unter Last), einzeln grün.
- Slice 17: Avalonia gibt jedem `Border` einen `NoneAutomationPeer` (Rolle None, nicht in der Steuerelement-Sicht); das `AutomationProperties.Name` der Streifen kam deshalb nie beim Screenreader an, obwohl der Befund es als vorhanden nennt. Der Name der Streifen enthielt bei Raumpflege Zeilenumbrüche und die Anleitung „Klick: Raumpflege abhaken“; der Screenreader-Name nimmt die Zeilen ohne Anleitung, mit „, “ verbunden. Die Beispieldaten haben keinen gebuchten Termin um 09:30 (Abnahme); der Test nimmt den gebuchten Termin um 10:30. Über den Plan hinaus prüft der Auswahltest, dass der Peer den Wechsel als Eigenschaftsänderung meldet.
- Slice 05: Auch `HelpView.axaml` hatte einen `Border.alert` („Hilfe nicht erreichbar“), den die Slice-Datei nicht nennt; mitgezogen, weil der Abnahmefall „kein `Classes="alert"` unter src/AispaStudio.Desktop“ ihn erfasst. In drei Buchungsleisten des Kalenders (Termin, Gruppe, Eintritt) stand der Fehler unter dem Primärknopf; er steht jetzt darüber (meldungen.md, masken.md R8). Der Prüftest dafür zählte zuerst vier statt drei Leisten, weil auch die Zeilen der Warteliste „Buchen“ mit `BookCommand` haben; er sucht jetzt den breiten Primärknopf am Fuß der Leiste. `PaymentEntry.TerminalFailed` war nur von der alten Kartenzahlungs-Box genutzt und ist entfernt. Die Durchsicht fand, dass der Fehler der Eintritte beim Verschieben in das Formular (`ShowForm`) gerutscht war und Lade- und Stornofehler so unsichtbar geblieben wären; er steht jetzt vor dem Formular, ein Quelltext-Test sichert das, der Ansichtstest der genehmigten Kartenzahlung prüft zusätzlich Befehl und Parameter der Aktion.
- Slice 24: Ein `aria-label` an einem `div` ohne Rolle ist unzulässig (wird nicht vorgelesen); das Popover des Abgleichs heißt deshalb wie das Menü der Aktionen `role="group"` mit „Abgleich“ (Plan: nur ohne `role="dialog"`). Die per Code fokussierten Überschriften von Hilfe und „KI und Hilfe“ hätten im Browser nach Tastaturbedienung den Fokusrahmen gezeigt; sie bekommen wie `.app-main` (Slice 23) einen transparenten Umriss. Der Wrapper `span.sync-status` diente nur der absoluten Lage und entfällt mit dem Top-Layer.
- Slice 24: Die Durchsicht durch einen frischen Agenten fand, dass `role="status"` die ganze Antwort beim Wählen eines Vorschlags erneut vorlesen würde (`aria-atomic` ist dort voreingestellt wahr); die Region ist jetzt `aria-atomic="false"`.
- Slice 08: Die Hilfe „Termine am liebsten“ nannte noch die Felder „ab“ und „bis“, die schon „Frühester Beginn“ und „Spätestes Ende“ heißen (mit berichtigt, Annahme 6). Das Handbuchbild `kunden-vorlieben.png` (Bildunterschrift „Termine am liebsten bearbeiten“) zeigt nur den Kopf der Kundenkarte und die Kennzahlen, nicht die Karte im Bearbeiten-Zustand, weil der Bildtest nicht dorthin scrollt; es ist mit der Warnzeile neu erzeugt, der Ausschnitt blieb (siehe Zurückgestellte Kleinigkeiten). Die Zeiten „Frühester Beginn“ und „Spätestes Ende“ hatten schon `FormField`; der Scan zählt sie mit.
- Slice 06: Der Namens-Scan fand in jedem Zustand der Entwicklungs-Anmeldung zusätzlich die Testkonto-Auswahl ohne Namen (W2 nennt sie, die Slice-Datei nicht eigens); sie heißt jetzt „Testkonto“. `ShellViewModel.PinText` duzte ebenfalls („meldest du dich“), der Du-Test durchsucht deshalb auch `ShellViewModel.cs`. Die Web-Anmeldung und die Hilfe nannten dieselben Du-Texte wie die App; beide sind mitgezogen (Annahme 2). Die Vitest-Gesamtsuite war unter Last in drei unbeteiligten Dateien rot (Zeitüberschreitung), einzeln grün.
- Slice 10: Der Befund W29 verlangt `LiveSetting` an der Antwortkarte; Avalonia sagt eine Live-Region aber nur an, wenn sich der Name des Elements ändert (Windows-`AutomationNode`), und ein `Border` hat keinen Namen. Die Einstellung steht deshalb am Antworttext in der Karte (Annahme 1). Das Suchfeld der Hilfe (W-Muster 1, `HelpView.axaml:12`) gibt es seit T-010 nicht mehr; die App zeigt die Web-Hilfe. Zwei neue Tests waren zuerst aus dem falschen Grund rot (`CanRevise` ist im Beispielentwurf aus); die Testdaten setzen es jetzt, danach waren sie aus dem richtigen Grund rot. Die neuen Beschriftungen machen die Antwortkarte höher: der Bildtest `Posteingang_uebersetzt_mit_KI_Ueberarbeitung` scrollt jetzt weiter (560 statt 330), sonst zeigte `posteingang-uebersetzt.png` die Knöpfe nicht mehr. Die Durchsicht fand die alten Knopftexte noch in `docs/produkt/kalender-rezeption.md` und den Satz „den Kasten „Antwort an …“ lesen“ in der Hilfe; beides nachgezogen.
- Slice 07: Der Zähler „Personen“ (W55) steht nicht in der Gruppenbuchung, wie die Abnahme sagt, sondern im Panel „Sauna und Ruheraum“ (Annahme 1). Mit dem Zahlenfeld wurden `FewerCommand`/`MoreCommand` im `AreasViewModel` verwaist und sind entfernt; `GroupAndAreaTests` setzt `Persons` jetzt direkt. „Personen“ teilt sich die Zeile jetzt zu gleichen Teilen mit „Von“ und „Bis“ (vorher schmale Spalte mit zwei Knöpfen). Für Zustände, die ein Laufzeit-Scan schwer erreicht (Freigabe, Standort im Datums-Flyout, Serie bearbeiten, Warteliste „Grund“), prüft ein Quelltexttest `CalendarView.axaml`; dafür ist `MessageBarTests.FindRepoRoot` jetzt `internal`. Die Durchsicht fand, dass ein leeres oder ungültiges Feld „Personen“ still mit dem alten Wert buchte und das Zahlenfeld mit seiner festen Breite von 120 px über die Spalte ragte; seitdem ist „Personen“ Pflicht, „Eintritt buchen“ ist bei ungültigem Feld gesperrt (der Hinweis am Knopf nennt „1 bis 20 Personen“), das Feld füllt seine Spalte; beim Merge fehlte „Result: ☐“ in den Testlisten von Slice 09, 10 und 27 (09 und 27 schon auf `dev`), wieder ergänzt (Test `Personen_ausserhalb_1_bis_20_oder_leer_sperrt_Eintritt_buchen`). Die Hilfe „Sauna- und Ruheraum-Eintritt“ sagt beim Stornieren „Es gibt keine Rückfrage“, die App fragt aber („Eintritt stornieren?“); nicht Teil dieser Slice.
- Slice 15: Die Abnahme „genau ein Primärknopf“ war ohne den Kopfknopf „Neue Kundin / neuer Kunde“ nicht erfüllbar: er war immer `primary`, auch während „Neue Kundin“ mit „Anlegen“ offen war (zwei Primärknöpfe, nicht in W38 genannt). Er ist jetzt nur ohne offene Bearbeitung `primary` (`IsEditingAny`). Zwei Tests aus Slice 08 öffneten zwei Karten zugleich (Wünsche + Rechnungsempfänger, Termine + Kennzeichen); sie prüfen dieselben Felder jetzt nacheinander. Fünf Handbuchbilder zeigen den Kopfknopf im Bearbeiten-Zustand und sind neu erzeugt. Die frische Durchsicht fand zwei Lücken, die der Plan nicht vorsah: Öffner, die auf den Server warten (Rabatt, Termine, Vorschlag), öffneten nach der Antwort ihre Karte auch dann, wenn inzwischen eine andere geöffnet war (jetzt Zähler `_editorGeneration`, der spätere Klick gewinnt); und nach „Neue Kundin“ ließ die Wahl einer Kundin das Formular mit „Anlegen“ offen (jetzt geschlossen). Beides mit Test in `CustomerCardsEditTests.cs`. Plan-Test 3 ist ein Durchlauf aller Öffner (`[Fact]`) statt einer Theory.
- Slice 16: Der Startfokus scrollt die Kundenseite zur geöffneten Karte; dadurch zeigt das Handbuchbild `kunden-vorlieben.png` jetzt die Karte „Termine am liebsten“ im Bearbeiten-Zustand, wie es die Bildunterschrift sagt (in Slice 08 als zurückgestellt notiert); es ist neu erzeugt, die Bilder `kunden-kennzahlen.png` und `kunden-kennzeichen.png` unterscheiden sich nur durch die Farbreduktion und bleiben. Die frische Durchsicht fand, dass Annahme 2 (Enter auch aus einer geschlossenen Auswahl oder einem Kontrollkästchen) mit Avalonia nicht stimmt: `ComboBox` öffnet mit Enter die Liste, Knöpfe und Kontrollkästchen lösen sich selbst aus, beides mit `Handled`; Hilfe und Plan sind auf Eingabefelder eingeschränkt (siehe Ruling), zwei Tests prüfen Enter im Textfeld „Druck“ und Enter/Strg+Enter im mehrzeiligen Rezeptionsvermerk.

- Slice 09: Der Befund zählt 14 Felder; ohne Beschriftung waren 13, das 14. ist das Rabattfeld, dessen zwei Felder schon eigene Namen („Rabatt in Prozent“, „Rabatt als Betrag“) tragen, und das bleibt. Das erste Bild zeigte, dass die Knöpfe „Wertgutschein verkaufen“ und „Behandlung verkaufen“ neben dem Feld in der linken Spalte (330 px) das Feld auf rund 50 px zusammendrücken („Wert (EU“, „z. B. 5“); sie stehen jetzt darunter, der Knopftest prüft die Breite der beiden Felder (mindestens 240 px). Neun Handbuchbilder der Kassa zeigen die geänderten Felder oder Knöpfe und sind neu erzeugt.
- Slice 28: Die Durchsicht fand, dass Enter im Feld „Grund“ unbehandelt bis zum Fenster stieg und dort einen sichtbaren Standardknopf (`IsDefault`) hinter dem Overlay auslösen konnte, z. B. „Notiz speichern“ im offenen Terminpanel; mit dem neuen Anfangsfokus wäre das der Normalfall geworden. Die Rückfrage verbraucht Enter im Feld jetzt (Test prüft `Handled`). Die neuen KI-Knöpfe sprengten den Längentest der Knöpfe (26 Zeichen, Brief 5.1); Ruling oben. Der Quelltexttest für Richtungswörter fand den Hinweis im `InboxViewModel` zuerst nicht, weil der Text selbst ein `;` enthält. Die Zeilenangaben der Slice waren nach den Slices 06–10 verschoben.
- Slice 19: Die Slice-Datei zählt fünf Reiterschienen; die Seitenleiste zeigt dieselben Reiter ein zweites Mal als Einträge der Klappliste „⋯ n“ (`paneltab entry`), die ebenfalls nur die Klasse `on` trugen und mitgezogen sind. Der Abnahmetest für „Warum dieser Vorschlag?“ braucht die gestubbte Antwort des Assistenten (`POST /api/assistant`); ohne sie erscheint kein KI-Fuß. Zwei bestehende Fokusreihenfolge-Tests hingen an den alten Typen: einer erwartete „Termin (Button)“, der andere fand mit `OfType<AccordionHeader>().Single()` nach der Umstellung von „Mehr“ zwei Köpfe; beide nachgezogen (Rolle TabItem bzw. Suche per Name), nicht abgeschwächt.
- Slice 20: Die Abnahme nennt „Termine, 12“ und „Posteingang, 3 offen“; der Beispieltag hat 24 Termine, und der Posteingang heißt in der Navigation „Anfragen“. Die Tests prüfen deshalb „Termine, 24“ und „Anfragen, 3 offen“ (dasselbe Muster). Die zweite Stelle „Gast entfernen“ der Slice-Datei ist heute „Abschnitt entfernen“. Die Zeilenangaben der Slice-Datei waren durch frühere Slices verschoben. Im Tagesüberblick hießen auch „Noch nicht zugesagt“ und „Überfällig“ nur nach der Beschriftung, ohne Zahl; beide nennen jetzt den Wert mit.
- Slice 21: Die Durchsicht bestätigte, was Annahme 3 zurückstellt, und fand die Folge: Die vier Öffner (Zeiten, Notiz, Wiederholen, Verschieben) sperren sich nur paarweise (Zeiten ↔ Notiz); „Verschieben …“, „Wiederholen …“, „Zeiten dieses Termins ändern …“ und der Notiz-Stift bleiben bei offener Karte bedienbar. Zwei offene Karten zeigen dann zwei Primärknöpfe und zwei sichtbare Standardknöpfe (`IsDefault`), Enter löst beide aus. Bestand schon vor der Slice. Die Zeilenangaben der Slice waren nach Slice 07 verschoben; „Serie anlegen“ war im Befund nicht genannt, war aber ebenfalls primär und bleibt es als Bestätigen seiner Karte. Von den Handbuchbildern zeigt nur `kalender-panel-zeiten.png` eine offene Karte; es ist neu erzeugt, `kalender-gruppe`, `kalender-paar`, `kalender-treatwell-termin`, `meldungen-notizen` und `kalender-warteliste` zeigen keine geänderte Stelle.

- Slice 11: Es gibt 9 ListBoxen, nicht 10: die Hilfe hat seit T-010 keine Liste mehr (Web-Hilfe in der WebView). Ohne die Ableitung aus dem Platzhalter fand der Laufzeit-Scan auf den Seiten kein namenloses Feld, die Quelltextprüfung drei in der Kundenkarte (weitere E-Mail-Adresse, Freigabe und PIN beim Rabatt). Die Durchsicht fand zwei weitere, die keine Prüfung sah: das Textfeld in der Vorlage der beiden Titel-Auswahlen der Kundendaten (`AutoCompleteBox`, Beschriftung hängt an der Auswahl, den Fokus hat das Textfeld); `AccessibleNames` gibt ihm jetzt Name und Beschriftung der Auswahl, ein Test öffnet „Weitere Angaben“. Posteingang und Kundenliste waren im Seiten-Scan schon gefüllt; die Record-Erkennung meldete dort sofort 7 Zeilen.
- Slice 18: Der Fenster-Peer meldet den Fokuswechsel an UI Automation über `IRootProvider.FocusChanged`, in Avalonia 12 aber intern (`FocusChanged` und `GetFocus` nicht erreichbar); der Abnahmetest hört deshalb auf das öffentliche `GotFocus` und prüft am Peer der Karte `HasKeyboardFocus`, Name und Auswahl. Ist das Termin-Panel offen (Klick auf eine Karte öffnet es im Testaufbau), wechseln die Pfeiltasten nur die Markierung, hervorgehoben und „ausgewählt“ bleibt der Termin des Panels; der Test markiert Termin A deshalb ohne Panel.
## Zurückgestellte Kleinigkeiten
- Slice 01: `<caption>` fehlt weiter an 30 Tabellen (Namen kommen über `aria-label`, laut B4 in Ordnung).
- Slice 02: Hover-Dauer der Knöpfe bleibt 0,12 s statt 100 ms (Brief 4.6). `.results button { border: none }` (Suchtreffer) bleibt im Kontrastdesign ohne Rahmen. Lager-Reiter, -Zeilen, -Chips, -Schalter und -Links haben weiter keinen Druckzustand. Die eigenen Blöcke für reduzierte Bewegung in `stock.css` und `myday.css` sind jetzt doppelt (harmlos).
- Slice 03: Die Knöpfe „Erledigt“ unter dem Abgleich-Banner heißen weiter nur „Erledigt“ (Slice 20). Nur `kalender-offline.png` ist neu erzeugt; ob ein anderes Handbuchbild eines der alten Banner zeigt, ist nicht geprüft.
- Slice 12: Die Fehlertexte der Windows-App (`MoneyInputParser`, `NumberInputParser`, `DateInputParser`) enthalten „bitte“ (texte.md T8) und weichen damit vom Web ab. Beschriftungen der Betragsfelder ohne „(€)“ und Breite 98 px statt 140 (Brief 5.2); die Platzhalter „€“ und „Wert €“ sind keine Beispiele. „Rest Karte“ kann bei Überzahlung durch Überweisung oder Gutschein einen negativen Betrag ins Feld schreiben, der „Bezahlen“ sperrt und den Fehler erst nach dem Verlassen des Feldes zeigt (B42, offen). `admin/NumberInput.tsx` macht aus unlesbaren Zahlen weiter still 0, das Feld „Menge“ der Kassa (`Number(quantity) || 1`) still 1. Erscheint ein Fehlertext, springt das Layout (Nachbarfelder im `form-grid`, Knöpfe in Aktionszeilen; masken.md R8). 14 bis 15 ganze Ziffern überstehen das Verlassen nicht. Der rote Rahmen `input[aria-invalid='true']` gilt jetzt in der ganzen Verwaltung, `select` und `textarea` noch nicht. Der Web-Parser entfernt jedes Leerzeichen (`\s`), die App innen nur U+0020 und U+00A0.
- Slice 29: Im Assistenten steht die Beschriftung „Kunde“ des Suchfelds weiter neben dem Feld und ohne Beschriftungsstil (war vorher so, außerhalb B8). Neue Kunden lassen sich weiter ohne Geburtsdatum und Notiz anlegen (Nicht in dieser Slice: neue Felder).
- Slice 13: „Preis je Standort“ (`TreatmentsTab.tsx`, Bereich Preise) bleibt `type="number"` mit `Number(value.replace(',', '.'))` (Slice-Datei „Nicht in dieser Slice“); dieselbe Ursache wie B7, `MoneyField` passt dort ohne neue Logik. Steht ein ungültiger Preis in „Grunddaten“ und die Person wechselt den Bereich, ist „Speichern“ grau ohne sichtbaren Grund, und nach dem Zurückwechseln fehlt der Fehlertext bis zum nächsten Verlassen (wie B36). Das Hilfebild `masterdata-behandlung.png` zeigt den Preis noch als „79“ statt „79,00“. Kein Test für das Sprungziel `feld=treatment.price` (der Mechanismus `[data-field]` ist unverändert, das Attribut sitzt jetzt am Eingabefeld des `MoneyField`). Die Obergrenze 100.000 der Preisregeln prüft weiter nur der Server.
- Slice 04: Die Statuszeile der Sicherung neben „Jetzt sichern“ steht in einem waagrechten `StackPanel` und läuft bei langem Text über den Kartenrand hinaus (vorher schon so, im Bild `sicherung.png` sichtbar). Der Erklärtext über dem Feld „Schlüsselcode des Spas“ beginnt noch mit „Schlüsselcode des Spas:“ und wiederholt damit die neue Beschriftung. Die Statuszeilen bleiben Text mit Live-Region statt Toast (W-Muster 3 nennt Toast als Ziel). Der Stil `Border.alert` bleibt bis Slice 05.
- Slice 27: ✕ (Schließknöpfe), ▾, ⋯, ⧉, ☐, ♀ und ♂ fehlen in Inter und kommen weiter aus der Systemschrift. `<strong>` und `th` fordern 700 an; mit `font-weight: 400 600` zeichnet der Browser sie mit 600 (Brief erlaubt 700 ohnehin nicht), Sichtprüfung offen. Die Hilfe lädt Inter ohne Preload (kurz Ersatzschrift beim ersten Öffnen möglich). Die bestehenden Hilfebilder der Web-Oberfläche zeigen noch Segoe UI und werden erst beim nächsten Erzeugen mit Inter gerendert. Die Dist-Prüfung erkennt eine Schrift nur über HTML und CSS, nicht über JS oder Data-URL, und erwartet die Attribute des Preloads in fester Reihenfolge.
- Slice 22: Stammdaten und Berichte wählen den Reiter nur beim ersten Rendern; ist die Reiterliste leer (Rolle ohne jedes Recht der Seite) oder fällt der gewählte Reiter später weg, zeigt `aria-labelledby` des Panels ins Leere (vorher schon so, die App zeigt die Seiten ohne Rechteprüfung). `.segmented` bricht um, Brief 5.4 sagt „bricht nie um“ (Aussehen, nicht in dieser Slice). Das Panel „Vergleich mit anderen Spas“ hat kein Fokusziel und keinen eigenen Tab-Stopp (APG empfiehlt dann `tabindex="0"`). Manuelle Aktivierung (Pfeil bewegt nur den Fokus) für Berichte und Vergleich, die beim Durchpfeilen je Reiter neu laden, ist nicht gewählt. Keine Sichtprüfung im Browser.
- Slice 14: Der Kommentar in `ReturnDepositDialog.tsx` sagte „fokussiert den ersten Knopf“, dort ist es das Feld „Grund“; der Kopfkommentar von `web/src/vitest.setup.ts` nannte die Näherung nicht (kein inerter Hintergrund, kein Esc/`cancel`, kein `close()`, keine Sichtbarkeitsprüfung, kein `InvalidStateError`); beides in der Durchsicht berichtigt. Der Zweig `else dialog.setAttribute('open', '')` in den sieben bestehenden Dialogen (Altbestand) ist in Tests nicht mehr erreichbar und in Browsern toter Code (gemeldet, nicht gelöscht); im neuen Dialog des Dienstplans entfällt der Zweig. Nach der Bestätigung bleibt „Verwerfen“ im Dienstplan anklickbar, bis die Neuladung fertig ist (wie vor der Slice). Zum gemeinsamen Baustein (B2) gehören außerdem: die Rückkehr des Fokus zum Auslöser nach dem Schließen (fehlt in allen Rückfragen, Annahme 6), ein `aria-describedby` am Text (der neue Dialog im Dienstplan hat wie seine Geschwister keines, ein Screenreader liest die Zahl der Entwürfe womöglich nicht vor), die Alert-Rolle und der Innenabstand 24. Der Dialog im Dienstplan nutzt `promo-dialog` und `stock-*`-Klassen außerhalb von `.stock`, obwohl der Kopfkommentar von `stock.css` sagt, sie gälten nur unter `.stock`; Zeilenhöhe und Schriftgewicht kommen von der Seite (im manuellen Test prüfen, Schritt 10). `CancelAppointmentsDialog` ist nicht umgestellt (siehe Ruling): sein Fokus auf „Abbrechen“ (Test in `ClosuresPage.test.tsx`) stimmt nur, weil beim Öffnen allein „Abbrechen“ existiert und die Aktion samt Kontrollkästchen erst nach dem Laden der Vorschau erscheinen; das ist Zufall der Ladereihenfolge, das `autoFocus` dort wirkt nicht (B2). Der Prüfpunkt der Slice sagt „eine Schließzeit archivieren“, die Seite kennt nur „Löschen“ (Annahme 8, die Testliste schreibt „löschen“). `RosterPage.discard()` wertet die Antwort des `POST …/roster/discard` nicht aus (Altbestand, kein Fehlerpfad). Die Hilfe- und Companion-Tests (`helpPage.test.tsx`, `Companion.test.tsx`) laufen unter Last in die Grenze von 1 s bei `findBy` und waren auch in Slice 04 und 13 rot; ein Blick auf die Ladezeit des Hilfe-Index oder auf die Grenze lohnt sich in einer späteren Slice (nicht in dieser).
- Slice 26: Nach dem Bestätigen bleibt der Knopf neben der Auswahl bis zum Ende des Neuladens frei (die alte Stufe gilt dort noch); ein zweiter Druck öffnet nur die Rückfrage erneut. Ändert jemand anderes die Stufe, während hier eine Auswahl offen ist, bleibt die Auswahl stehen. Die Meldung „Stufe geändert.“ bleibt stehen, bis eine neue Stufe gewählt wird. Wie in Slice 14 fehlen der Rückfrage `aria-describedby` am Text und die ausdrückliche Rückkehr des Fokus zum Auslöser (B2); `README.md` nennt weiter nur „die Abo-Stufe ändern“ (stimmt weiter). Die Rückfrage bekommt ihre Werte aus den Tokens der Betreiber-Seite, nicht aus `index.css` (B69 offen).
- Slice 25: Die Zeiterfassung (`web/src/time/TimesheetPage.tsx`) hat dasselbe Muster wie der Dienstplan vorher: Zellen- und Zeilenwarnungen nur im `title`, nur die erste Zellenwarnung sichtbar (nicht in der Befundliste, nicht in dieser Slice). Die Kasse unterscheidet die Warnungen nicht einzeln: bei ausgefallenem Signaturgerät sind auch der Länderhinweis und „außer Betrieb“ `alert` (wie heute schon gelb); dafür bräuchte die API eine Art je Warnung. `.notice` hat 16 px Rand links und rechts und rückt in einer Karte unter der Aktionsleiste ein (wie die Hinweise der Lohn-Vorschau); keine Sichtprüfung im Browser. Erfolgsmeldungen bleiben stehen, bis die nächste Aktion sie ersetzt, und haben keinen Schließen-Knopf (meldungen.md M2, B-Muster, nicht in dieser Slice). Der Hinweis `datevHint` unter der Buchungstabelle wiederholt den Fehler zum BU-Schlüssel nach einem verweigerten DATEV-Export (vorher schon so).
- Slice 23: Einige Seiten haben zusätzlich ein unsichtbares `h2` mit demselben Namen wie das neue `h1` (z. B. „Lager“ in `StockPage`, „Betriebsinformationen“ in `BusinessInfoPage`). `placeAccountMenu` in `App.tsx` ist eine Kopie von `placeMenu` der Aktionen (zwei Stellen; ein gemeinsamer Baustein lohnt sich mit einem dritten Popover-Menü). Die Kopfzeile schneidet bei 1280 px den letzten Seitenreiter an („Zugän…“, die Schiene scrollt; vorher schon so). Die Popover-Näherung der Tests kennt kein Schließen per Klick daneben, keinen Top-Layer und keine verschachtelten Popover. B14 (Fokus nach dem Seitenwechsel) und B15 (`aria-current`) bleiben offen.
- Slice 17: „Auswählen“ per Screenreader-Befehl (`ISelectionItemProvider.Select`) tut nichts, und die Karten haben keinen Auswahlbehälter (Spalte als Liste); Terminkarten bleiben ohne Tab-Stopp, die Ansage beim Wechsel mit den Pfeiltasten folgt in Slice 18. Kennzeichen, Verspätung, Gruppe, Serie und Ende stehen nicht im Namen, nur in der Hover-Karte. Die Spaltenköpfe und die Schließ- und Sperrbänder sind nicht Teil dieser Slice und nicht geprüft.
- Slice 05: Die neuen InfoBars haben kein „Erneut versuchen“ und kein Schließen-Kreuz, weil es dafür keine Befehle gibt (meldungen.md nennt „Erneut versuchen“ als Ziel). Der Tooltip „Laufende Zahlung abbrechen; eine genehmigte wird am Terminal storniert“ am Knopf der Kartenzahlung entfällt, die Aktion der InfoBar hat keinen. Die Warnungen bei Gesundheitswörtern in den Kunden werden beim Erscheinen sofort angesagt, auch mitten im Tippen (Annahme 2, im manuellen Test prüfen). Die Konfliktanzeige der Buchungsleisten steht weiter unter dem Buchen-Knopf (W25, offene Entscheidung). Die Zusammenfassung „Vorschläge übernehmen“ im Kalender (`OkSoftBrush` ohne Symbol) ist keine `Border.alert` und bleibt. Texte unverändert, auch mit „bitte“: der Offline-Hinweis im Posteingang und „Bitte noch einmal versuchen.“ in `CalendarViewModel.Problems` (texte.md T8). In der schmalen Spalte „Bezahlen“ bricht die genehmigte Kartenzahlung neben „Stornieren“ in drei Zeilen um (Bild `kassa-terminal.png`). Die übrigen Handbuchbilder zeigen in der Seitenleiste noch „Wie Windows“ und im Assistenten einen älteren Platzhalter (nicht von dieser Slice, nicht erneuert).
- Slice 24: `place` in `SyncStatus.tsx` ist nach `placeMenu` der Aktionen und `placeAccountMenu` (Slice 23) die dritte Kopie derselben Platzierung; Slice 23 nannte den dritten Fall als Anlass für einen gemeinsamen Baustein (nicht hier, weil App.tsx und die Aktionen anderen Slices gehören). Esc im Begleiter prüft `defaultPrevented` nicht (ein Feld mit eigenem Esc in der Leiste schließt sie mit). Die Meldungen „Gebucht …“ und „Daten gespeichert …“ im Assistenten sind weiter bedingt gerenderte `role="status"` (Annahme 6). Die leere Live-Region steht als letztes Kind der Flex-Spalte und fügt unten 12 px Abstand an. Steht der Auslöser der Hilfe beim Schließen nicht mehr im Dokument (z. B. ein Knopf im inzwischen ausgeblendeten Begleiter), fällt der Fokus auf `body`; ein Schließen per Code holt den Fokus ebenfalls zum Auslöser zurück. Das Abgleich-Popover wird nur beim Öffnen platziert und wandert beim Scrollen nicht mit (wie das Menü der Aktionen). Die Live-Region enthält mit den Vorschlägen und der Kundensuche auch Bedienelemente; neu Hinzugekommenes wird angesagt.
- Slice 08: Freigabe-Auswahl („Freigabe durch …“) und PIN beim Rabatt der Kundenkarte haben weiter nur den Platzhalter als Namen (W-Muster 1, Annahme 3). Der Bildtest `Kundenkarte_Vorlieben_bearbeiten` sollte zur Karte „Termine am liebsten“ scrollen, damit `kunden-vorlieben.png` zeigt, was die Bildunterschrift sagt. Das Fragezeichen „therapeutin“ steht weiter neben dem Feld statt am Gruppentitel (Brief 5.11, gehört zu W40). Die Felder der Wünsche stehen mit 6 px Zeilenabstand unter dem vorherigen Feld, nur die Beschriftung trennt sie; die Kundendaten-Maske nutzt mehr Abstand (Sichtprüfung in der manuellen Testliste). Die Maske „Neue Kundin“ hat weiter unverbundene Beschriftungen ohne Pflichtkennzeichen (W36).
- Slice 06: Der Code-Hinweis der Verwaltung in `web/src/App.tsx` („… mit deinem Zugang angemeldet.“) duzt weiter; andere Oberflächentexte im Code außerhalb von Anmeldung und Konto-Flyout sind nicht auf Du-Form durchsucht. Die PIN-Hilfe am Empfangsgerät nennt immer „4 bis 8 Ziffern.“, auch wenn die gewählte Person zur Leitung gehört (dort 6 bis 8). Höhen und Abstände der Anmeldung (W3) und die Standardknöpfe (W4) bleiben offen; durch die Beschriftungen ist die Karte der Einrichtung höher geworden.
- Slice 10: Der gewählte Gast unter „Für wen?“ (Name und „Ändern“) ist nicht mit der Beschriftung verbunden; der Screenreader liest nur den Namen. Eine Antwort mit genau demselben Text wie die vorige wird nicht erneut angesagt (Name unverändert). Das README beschreibt die Überarbeitung weiter mit „freundlicher, kürzer, förmlicher, persönlicher“ (Fließtext, keine Knopfnamen). Der Abnahmetest zum Primärknopf zählt nur in `CompanionContent`, nicht in der ganzen Seitenleiste des Kalenders.
- Slice 07: Die Pflicht-Kennzeichen und der Primärknopf der Maske „Neue Kundin“ im Termin-Panel bleiben bei W54; die Beschriftung „Kundin oder Kunde“ über der Suche ist weiter ein unverbundener `TextBlock.label` (das Suchfeld hat einen eigenen Namen). Der Quelltexttest deckt nur `CalendarView.axaml` ab, die übrigen Ansichten folgen mit Slice 11.
- Slice 15: Keine Rückfrage, wenn die offene Karte ungespeicherte Eingaben hat (Annahme 1); ein Wechsel verwirft sie still. Esc, Enter und Startfokus der Karten folgen in Slice 16. Der Kundenwechsel in der Liste schließt die Karten wie bisher, „Neue Kundin“ ebenso über `Selected = null`. Eine Rolle ohne Recht auf Kennzeichen sieht „Kennzeichen setzen“ weiter; danach steht nur „Ihre Rolle darf hier keine Kennzeichen setzen.“ und kein Primärknopf auf der Seite (vorher schon so).
- Slice 16: Die Stammdaten im Kopf (`CustomerContactForm`) schließen auf der Kundenseite weiter nicht mit Esc (Annahme 3). Ist die KI-Leiste offen und der Fokus auf einer Auswahl oder einem Kontrollkästchen der Karte, schließt das erste Esc die Leiste, erst das zweite die Karte (`ShellView` hört im Tunnel zu; im Textfeld geht Esc gleich an die Karte). Nach „Übernehmen“ (Vorschlag für „Termine am liebsten“) geht der Fokus beim Schließen auf „Ändern“ der Karte, nicht auf „Übernehmen“. Die Rückkehr des Fokus nutzt `Focus()` ohne Tastatur-Navigation; ob der Fokusrahmen am Öffner nach Esc sichtbar ist, steht in der manuellen Testliste. Die sechs Kartenzustände stehen jetzt dreimal in `CustomersViewModel` (`IsEditingAny`, `CloseEditors`, `CancelCard`).

- Slice 09: Das Feld „Gutschein-Code“ trägt den Pflichtstern, sobald die Zahlungsart Gutschein angeboten wird, obwohl der Code erst bei einem Betrag über 0 nötig ist (das Feld war vorher schon immer sichtbar). Die Abstände in den Karten der Kassa (6, 8, 10 px) bleiben unter „Feld zu Feld 16“ aus masken.md. Der Rechnungsempfänger heißt in der Kundenkarte „Empfänger“ (Slice 08), in der Kassa „Name oder Firma“ (Annahme 1); beide Slices liefen parallel, eine gemeinsame Beschriftung ist offen. Die Felder der Rechnungsempfänger-Maske sind im Bild nur über den Namens-Scan geprüft, kein Handbuchbild zeigt die offene Maske.
- Slice 28: „Mit Entschuldigung (KI)“ hat weiter kein Verb (nicht im Befund). Dieselben Richtungswörter stehen im Web (`InboxPage.tsx` „Zuerst oben klären“, „Hinweis oben“; „Links eine …“ in Schließzeiten, Raumsperren, Aktionen, Betriebsinfo) und in der Hilfe (`anmeldung-anmelden.md` „Oben „Windows-App verbinden“ wählen.“, `aktionen.md`, `raumsperren.md`, `schliesszeiten.md`); die Web-Knöpfe heißen weiter „Freundlicher (KI)“ und „Förmlicher (KI)“. Die neutrale Rückfrage mit Feld („Falsch melden?“) bestätigt mit Enter nicht mehr, weil der Fokus im Feld steht. ADR 0073 nennt die alten Knopfnamen (Entscheidungstext, unverändert).
- Slice 19: Zwei weitere Aufklapper sind `ToggleButton.ghost` und melden weiter „aktiviert“: „Neue Kundin / neuer Kunde“ im Termin-Panel (`CalendarView.axaml`) und „Nachricht“ (Original einblenden, `InboxView.axaml`); die Durchsicht fand sie, die Slice-Datei nennt sie nicht, und der Quelltexttest sucht nur die Klasse `more`. Der Chip „Alle zeigen (n)“/„Nur mit Dienst“ (`HeadStaffFilter`) schaltet ein und aus, meldet als einfacher `Button` aber keinen Zustand (eher `ToggleButton`). Die Reiterschienen haben keinen Auswahlbehälter (Rolle Tab-Liste), Narrator nennt deshalb nicht „2 von 3“. Kein Test für „Auswählen“ eines gesperrten Reiters (UIA wirft dann `ElementNotEnabledException`) und kein eigener Peer-Test für „Einlage/Entnahme“ und „Team/Räume“ (nur der Quelltexttest).
- Slice 20: Die Tooltips „Baustein entfernen“, „Gast entfernen“, „Position entfernen“ und „Adresse entfernen“ bleiben allgemein (nur der Name hat den Bezug). Der Name des Konto-Knopfs folgt einem Wechsel des Zugangs nicht, solange die Shell lebt (`UserText` meldet keine Änderung; ein Wechsel baut die Shell heute neu auf). Weitere Knöpfe mit Zahl oder Uhrzeit als erstem Text außerhalb der genannten Stellen sind nicht gesucht (Namens-Scan W65, offene Entscheidung). Die Durchsicht fand drei weitere: geparkte Rechnungen der Kassa heißen nur nach dem Gast (mehrere „Laufkundschaft“ gleich, ohne Betrag), der Kopf-Filter im Kalender nur nach dem Filter ohne „aufheben“, Suchtreffer mit gleichem Gast gleich.
- Slice 21: Zwei Bearbeiten-Karten im Termin-Panel lassen sich zugleich öffnen (dann zwei Primär- und zwei Standardknöpfe, Enter löst beide aus); Vorschlag der Durchsicht: die vier Öffner kehren bei `IsEditingCard` früh zurück, die Öffner-Knöpfe und der Notiz-Stift sind dann gesperrt, mit Test „Notiz offen, Verschieben öffnet nicht“. W56 „Ganze Buchung stornieren“ als `danger` bleibt offen. Kein Bildtest prüft „Gast eintragen“ (die Testdaten haben keinen Termin mit `CanNameGuest`); die Klasse ist nur im Quelltext entfernt. Bei Termin- und Tagesnotiz steht „Abbrechen“ weiter links vom Bestätigen, sonst rechts. Übrige Teile von W-Muster 4 (Sicherung, Abgleich, Geräte, KI und Hilfe) liegen in anderen Slices.

- Slice 11: Die Prüfung der Listen vergleicht das `x:DataType` des Zeilenstils nicht mit dem der Vorlage (von Hand geprüft, alle 9 passen). Die Listen für Vorschläge und Gruppenzeiten laufen in den Bindungsfehler-Tests nur leer. Einträge im Aufklappmenü von Auswahllisten heißen weiter nach dem Record-Typ (gehört zu W65). Der Posteingang nennt die KI-Zusammenfassung nicht im Zeilennamen (lang, das Detail liest sie). Die Hilfe `kassa-rabatt-geben.md` schreibt noch „Freigabe durch …“, das Feld der Kassa heißt seit Slice 09 „Freigabe durch“.
- Slice 18: Mit offenem Termin-Panel sagt Narrator beim Pfeilwechsel den neuen Termin an, nennt ihn aber nicht „ausgewählt“, weil Auswahl und Ring am Termin des Panels bleiben (bestehendes Verhalten). Ein Mausklick gibt der Karte kurz den Fokus, `OnHostPressed` holt ihn gleich zur Kalenderfläche zurück (Narrator kann beides ansagen). Bild auf/ab und Ende scrollen bei fokussierter Karte den Tag. Wird der Tag neu aufgebaut (Live-Änderung, Alt+Pfeil), verliert die Karte den Fokus ohne Ansage; die nächste Taste holt ihn zurück.
## Offene Entscheidungen
Alle Punkte am 25.10.2026 entschieden (vom Auftraggeber an Claude übertragen); die frühere Frage samt Empfehlung steht im Git-Verlauf. Die nächste Runde schneidet sie als Slices.
Ruling: Maße außerhalb des Rasters (W-Muster 9, W3, W23, W41, W59) werden an die Werte des Briefs angeglichen; einzige Ausnahme ist die Höhe des Bezahlknopfs der Kassa, die Brief 4.4 als Ausnahme aufnimmt (Hauptaktion einer Kasse, große Trefferfläche gewollt). – Claude, vom Auftraggeber übertragen, 25.10.2026
Ruling: Information nur im Tooltip und Deaktivierungsgrund nur mit der Maus (W-Muster 12, W40) werden behoben: Grund zusätzlich als Hilfetext, gekürzte Texte per Tastatur erreichbar (WCAG 1.3.1, 2.1.1). – Claude, vom Auftraggeber übertragen, 25.10.2026
Ruling: W4, W8, W19, W25, W27, W56, W57 und W58 werden alle behoben; W27 als SearchField, W25 als InfoBar wie die übrigen Meldungen seit Slice 05. – Claude, vom Auftraggeber übertragen, 25.10.2026
Ruling: `StDone` wird in App und Web hell auf `#656c68` abgedunkelt (4,6:1 auf `#efede8`, gemessen; die Empfehlung `#6f7773` erreicht nur 3,93:1 und reicht nicht), dunkel bleibt `#97a6a2` (5,97:1); Brief 2.6 gilt nur für Felder, Kontrollkästchen und Optionsfelder, W66 und B29 werden damit aufgelöst. – Claude, vom Auftraggeber übertragen, 25.10.2026
Ruling: Namens-Scan je Zustand für Seitenleisten, Dialoge und Bearbeiten-Zustände (W65) kommt; Slice 11 hat die Grundlage gelegt. – Claude, vom Auftraggeber übertragen, 25.10.2026
Ruling: Rückfragen im Web werden ein gemeinsamer Baustein, zuerst B2 (Fokus zurück zum Auslöser, `aria-describedby`, Alert-Rolle, Innenabstand 24), danach B35, B40, B52 und B57 darauf. – Claude, vom Auftraggeber übertragen, 25.10.2026
Ruling: Doppelte Formularbausteine und Farbtoken werden zusammengeführt (B9, B28, B69). – Claude, vom Auftraggeber übertragen, 25.10.2026
Ruling: Fokus und Zustände im Web (B14, B15, B33, B39, B41, B48, B49, B51, B59, B61) werden alle behoben; B48 als gemeinsames SidePanel. – Claude, vom Auftraggeber übertragen, 25.10.2026
Ruling: Texte und Auswege im Web (B44, B50, B54, B55, B60, B66, B67) werden alle behoben. – Claude, vom Auftraggeber übertragen, 25.10.2026
Ruling: Kleinere Darstellungspunkte im Web (B11, B20, B22, B24, B32, B42, B43, B63, B65, B68) werden alle behoben; B68 (Tooltip-Baustein) als eigene Slice. – Claude, vom Auftraggeber übertragen, 25.10.2026
Ruling: Schriftgrößen bleiben in px (B30) und werden als bewusste Abweichung im Brief festgehalten, weil Brief W1 px verlangt und Browser-Zoom px mitskaliert. – Claude, vom Auftraggeber übertragen, 25.10.2026
Ruling: Der Kundenbereich `/buchen/` bekommt eine Content-Security-Policy (B31), zuerst als Report-Only; Grundsatzentscheidung, der ADR (nächste freie Nummer) entsteht mit der Slice, die sie umsetzt. – Claude, vom Auftraggeber übertragen, 25.10.2026
Ruling: Formulare der Zugänge, Buchhaltung und Lohn bekommen `<form>`, Enter zum Absenden und einen sichtbaren Grund am gesperrten Knopf (B36). – Claude, vom Auftraggeber übertragen, 25.10.2026
Ruling: Umgesetzte Befunde werden in der Befundliste docs/umsetzung/aenderungsliste-oberflaechen-2026-10-23.md gekennzeichnet („erledigt in Slice NN“), rückwirkend auch für die 29 Slices von R01, als erste Aufgabe der nächsten Runde. – Claude, vom Auftraggeber übertragen, 25.10.2026

Ruling: Für Runde R02 aufgenommen: Die Leiste „KI und Hilfe“ (der runde Knopf mit dem App-Symbol unten rechts, `CompanionFab`, und die Seitenleiste, die er öffnet) ist in allen Masken der Windows-App erreichbar und sichtbar, auch auf Seiten mit eingebettetem Browser (WebView2 in `EmbeddedBrowser`: Verwaltung, Hilfe); der Auftraggeber sieht den Knopf in der laufenden App derzeit nur im Kalender, nicht bei Kunden und Posteingang, es liegt also nicht nur am eingebetteten Browser. Ein Headless-Test auf `dev` (`c87abdb`) zeigt ihn auf allen Seiten sichtbar und oben, auch mit gewählter Kundin oder Mail; die Ursache muss deshalb zuerst in der laufenden App nachgestellt werden. Dabei gefunden: auf „Anfragen“ liegt der Knopf über dem Inhalt und verdeckt Text unten rechts; der Inhalt braucht Platz für ihn. Prüfung je Seite als Bild- und Headless-Test, Strg+Leertaste öffnet sie überall – Auftraggeber, 25.10.2026
## Manuelle Testliste
### Slice 01 – Tabellenköpfe mit scope und Zahlenspalten rechts
Start: Docker Desktop starten, `cd web && npm install && cd ..`, `dotnet run --project src/AispaStudio.AppHost`, Windows-App über die Ressource `windows-app`, als Inhaberin anmelden
1. Verwaltung › Buchhaltung öffnen, Zeitraum wählen, „Buchungen anzeigen“
Expected: „Betrag“ und „USt“ stehen samt Kopf rechts über den Zahlen, „Soll“ und „Haben“ links
2. Verwaltung › Lohnverrechnung › Monat wählen, „Monatswerte anzeigen“
Expected: „Menge“ steht samt Kopf rechts
3. Narrator an, in einer Tabelle mit Strg+Alt+Pfeil durch die Zellen gehen
Expected: Narrator nennt beim Spaltenwechsel den Spaltenkopf
Closes: B4, B56
Result: ☐

### Slice 02 – Bewegung, Hochkontrast und Druckzustand im Web
Start: Docker Desktop starten, `cd web && npm install && cd ..`, `dotnet run --project src/AispaStudio.AppHost`, Windows-App über die Ressource `windows-app`, als Inhaberin anmelden
1. Windows-Einstellungen › Barrierefreiheit › Visuelle Effekte › „Animationseffekte“ aus, dann Verwaltung › Lager öffnen und einen Artikel öffnen und speichern
Expected: Seitenleiste und Meldung erscheinen ohne Übergang
2. Windows-Kontrastdesign einschalten, Verwaltung › Lager öffnen und mit Tab durch Reiter und Knöpfe gehen
Expected: an jedem Knopf ist der Fokusrahmen sichtbar
3. Im Kontrastdesign den Kalender der Verwaltung öffnen
Expected: jede Terminkarte hat einen sichtbaren Rahmen
4. Normales Design, auf einen sekundären Knopf (z. B. „Abbrechen“) drücken und die Maustaste halten
Expected: der Knopf wird etwas kleiner und dunkler; „Speichern“ wird nur kleiner und bleibt farbig
Closes: B5, B6, B64
Result: ☐

### Slice 03 – Meldungen der Anmelde-, Start- und Verbindungsseiten als InfoBar
Start: Docker Desktop starten, `cd web && npm install && cd ..`, `dotnet run --project src/AispaStudio.AppHost`, Windows-App über die Ressource `windows-app`
1. Abmelden und mit einem falschen Code oder Passwort anmelden
Expected: über dem Formular erscheint eine Fehlermeldung mit rotem Symbol, nicht nur roter Text
2. Als Inhaberin angemeldet, im Aspire-Dashboard die Ressource `api` stoppen
Expected: oben erscheint die Warnung „Keine Verbindung zum Server …“ mit dem Knopf „Verbindung prüfen“
3. `api` wieder starten und „Verbindung prüfen“ drücken
Expected: die Warnung verschwindet, links steht wieder „Online“
4. Am Empfangsgerät angemeldet 4 Minuten 30 Sekunden nichts tun
Expected: oben mittig erscheint die Warnung „Ohne Eingabe wird in … Sekunden abgemeldet.“ mit „Weiter arbeiten“; Narrator sagt sie an
Closes: W5, W9, W18, W22
Result: ☐

### Slice 12 – Geldeingabe an der Kassa im Web mit Fehlertext statt stiller 0
Start: Docker Desktop starten, `cd web && npm install && cd ..`, `dotnet run --project src/AispaStudio.AppHost`, Windows-App über die Ressource `windows-app`, als Inhaberin anmelden, Verwaltung › Kassa
1. Bei „Heute abzurechnen“ **Abrechnen**, bei „Bar gegeben“ `12,5x` eingeben und das Feld mit Tab verlassen
Expected: roter Rahmen und „Der Betrag ist nicht lesbar. Beispiel: 12,50.“ unter dem Feld; „Bezahlen und Beleg erstellen“ ist grau
2. Die Eingabe zu `12,5` korrigieren und das Feld mit Tab verlassen
Expected: der Hinweis verschwindet schon beim Tippen; nach Tab steht `12,50` im Feld
3. `100` eingeben, Tab, **Bezahlen und Beleg erstellen**
Expected: Beleg mit Rückgeld wie bisher
4. Rechts bei „Gezählter Bestand“ mit Tab hinein und leer wieder hinaus
Expected: „Der Betrag fehlt. Beispiel: 12,50.“; „Tagesabschluss“ bleibt grau
5. `245,505` bei „Gezählter Bestand“ eingeben
Expected: „Höchstens zwei Nachkommastellen.“; „Tagesabschluss“ bleibt grau
6. Narrator an, bei „Bar gegeben“ `abc` eingeben und Tab
Expected: Narrator liest den Hinweis vor; beim Zurückspringen ins Feld nennt er Beschriftung und Hinweis
Closes: B37
Result: ☐

### Slice 29 – Kleinigkeiten Web: Beschriftungen im Buchungsfeld, Standorte als Gruppe
Start: Docker Desktop starten, `cd web && npm install && cd ..`, `dotnet run --project src/AispaStudio.AppHost`, Windows-App über die Ressource `windows-app`, als Inhaberin anmelden
1. Kalender der Verwaltung › **+ Termin**, bei „Kunde“ **+ Neuer Kunde**
Expected: über jedem der vier Felder steht eine Beschriftung (Vorname, Nachname, Telefon, E-Mail), in den Feldern kein grauer Platzhalter
2. In „Telefon“ klicken
Expected: der Browser schlägt keine gespeicherten Telefonnummern oder Adressen vor
3. Verwaltung › Zugänge › eine Person mit Rolle Rezeption öffnen
Expected: „Standorte“ mit Fragezeichen und die Kontrollkästchen sehen aus wie vorher
4. Narrator an, mit Tab auf das erste Standort-Kontrollkästchen
Expected: Narrator nennt die Gruppe „Standorte“ und den Namen des Kontrollkästchens
Closes: B8, B34
Result: ☐

### Slice 13 – Preise in den Stammdaten als Geldfeld
Start: Docker Desktop starten, `cd web && npm install && cd ..`, `dotnet run --project src/AispaStudio.AppHost`, Windows-App über die Ressource `windows-app`, als Inhaberin anmelden, Verwaltung › Stammdaten › Behandlungen
1. Eine Behandlung wählen, bei „Preis“ `89,9` eingeben und das Feld mit Tab verlassen
Expected: `89,90` steht im Feld
2. **Speichern**, eine andere Behandlung wählen und wieder zurück
Expected: „Gespeichert. Gilt für neue Buchungen.“; der Preis steht als `89,90` im Feld
3. Ins Preisfeld klicken, Pfeil nach oben drücken und das Mausrad drehen
Expected: der Betrag bleibt `89,90`
4. `89,9x` eingeben und das Feld mit Tab verlassen
Expected: roter Rahmen und „Der Betrag ist nicht lesbar. Beispiel: 12,50.“ unter dem Feld; **Speichern** ist grau
5. Bereich „Preise“, **+ Preisregel**, beim Preis der Regel `69,x` eingeben und Tab
Expected: Hinweis am Feld; **Preisregeln speichern** ist grau
6. `69,5` eingeben, Tab, **Preisregeln speichern**
Expected: `69,50` im Feld und „Preisregeln gespeichert. Gilt für neue Buchungen.“
Closes: B7
Result: ☐

### Slice 04 – Meldungen und Primärknöpfe in Sicherung, Abgleich und Geräten
Start: Docker Desktop starten, `cd web && npm install && cd ..`, `dotnet run --project src/AispaStudio.AppHost`, Windows-App über die Ressource `windows-app`, als Inhaberin anmelden; für Sicherung und Abgleich die Windows-App im Einzelplatz
1. Sicherung › **Neuen Wiederherstellungsschlüssel erzeugen**
Expected: über „Stand“ erscheint die Warnung „Wiederherstellungsschlüssel notieren“ in Warnfarbe mit Warnsymbol, schwarzer Schrift und dem Schlüssel; einziger hervorgehobener Knopf auf der Seite ist „Speichern und sichern“
2. In der Warnung **Verstanden**
Expected: die Warnung verschwindet, der Schlüssel ist nirgends mehr zu sehen
3. Abgleich › Verbindungsangabe eintragen, „Schlüsselcode des Spas“ leer lassen, **Eintragen**
Expected: die Warnung „Schlüsselcode notieren“ mit dem Code und **Verstanden** erscheint; „Eintragen“ ist der einzige hervorgehobene Knopf
4. Geräte öffnen
Expected: nur „Speichern“ ist hervorgehoben, „Diesen PC als Empfangsgerät anmelden“ ist ein normaler Knopf
5. Narrator an, Geräte › **Probedruck** ohne Drucker
Expected: Narrator liest die Statuszeile neben „Probedruck“ vor, ohne die laufende Ansage abzubrechen
Closes: W10, W11, W13, W16, W17
Result: ☐

### Slice 27 – Hausschrift Inter in der Verwaltung ausliefern
Start: Docker Desktop starten, `cd web && npm install && cd ..`, `dotnet run --project src/AispaStudio.AppHost`, Windows-App über die Ressource `windows-app`, als Inhaberin anmelden
1. In der Windows-App eine Seite der Verwaltung im WebView2 öffnen (z. B. Verwaltung › Kassa) und daneben eine native Seite der App (z. B. Kalender)
Expected: Texte in Verwaltung und App zeigen dieselbe Schrift (Inter: einstöckiges „a“ mit geradem Strich, „1“ mit Fähnchen, Ziffern gleich breit); Knöpfe in Gewicht 500 wirken gleich kräftig
2. In der Verwaltung eine Tabellenüberschrift und einen fett gesetzten Text ansehen (z. B. Kassa, Rechnung)
Expected: halbfett (600) und sauber, keine künstlich verdickten Buchstaben
3. Im Aspire-Dashboard die Web-Ressource im Browser öffnen, Entwicklertools › Netzwerk, Seite neu laden
Expected: genau eine Anfrage auf `inter-latin-var-….woff2`, keine Warnung „preloaded but not used“ in der Konsole
4. Den Kundenbereich `/buchen/` im Browser öffnen, Entwicklertools › Netzwerk, neu laden
Expected: keine Anfrage auf eine `.woff2`-Datei; die Seite zeigt die Systemschrift
Closes: B62
Result: ☐

### Slice 22 – Reiter im Web mit Pfeiltasten, Roving-Tabindex und Tabpanel
Start: Docker Desktop starten, `cd web && npm install && cd ..`, `dotnet run --project src/AispaStudio.AppHost`, Windows-App über die Ressource `windows-app`, als Inhaberin anmelden, Verwaltung › Stammdaten
1. Mit Tab bis zur Reiterschiene gehen
Expected: der Fokus liegt auf dem gewählten Reiter; ein weiteres Tab verlässt die Schiene in den Inhalt
2. Pfeil rechts drücken
Expected: Fokus und Auswahl liegen auf dem zweiten Reiter, der Inhalt wechselt
3. Ende, dann Pos1 drücken
Expected: Ende wählt den letzten Reiter („KI“), Pos1 den ersten
4. Narrator an, auf einen Reiter gehen
Expected: Narrator nennt Registerkarte, Name, ausgewählt und die Position (z. B. „2 von 15“)
5. Kalender › Schiene „Ansicht“, dann Berichte und Lager mit denselben Tasten
Expected: Team/Räume, Berichte und Lagerreiter wechseln; Aussehen und Abstände der Schienen und Inhalte wie vorher
Closes: B3
Result: ☐

### Slice 14 – Rückfragen im Web: Fokus auf Abbrechen, Dienstplan fragt vor dem Verwerfen
Start: Docker Desktop starten, `cd web && npm install && cd ..`, `dotnet run --project src/AispaStudio.AppHost`, Windows-App über die Ressource `windows-app`, als Inhaberin anmelden
1. Verwaltung › Stammdaten › Schließzeiten öffnen
Expected: die Liste der Schließzeiten erscheint
2. Eine Schließzeit ohne abgesagte Termine wählen
Expected: die Maske der Schließzeit zeigt den Knopf „Löschen“
3. „Löschen“ drücken
Expected: die Rückfrage „Schließzeit löschen?“ erscheint, der Fokus liegt auf „Abbrechen“
4. Enter drücken
Expected: die Rückfrage schließt, die Schließzeit steht noch in der Liste
5. „Löschen“ drücken
Expected: die Rückfrage „Schließzeit löschen?“ erscheint
6. Esc drücken
Expected: die Rückfrage schließt, die Schließzeit steht noch in der Liste
7. Verwaltung › Dienstplan öffnen
Expected: der Dienstplan der Woche erscheint
8. Die Zelle eines Tages wählen
Expected: die Seitenleiste „Tag planen“ mit Name und Tag öffnet sich (das Häkchen bei „Sofort gültig, ohne Veröffentlichen“ bleibt leer)
9. „Speichern“ drücken
Expected: die Meldung „Als Entwurf gespeichert …“ erscheint, die Zelle ist gestrichelt, die Knöpfe „1 Entwurf veröffentlichen“ und „Verwerfen“ erscheinen
10. „Verwerfen“ drücken
Expected: die Rückfrage „Entwürfe verwerfen?“ nennt „1 Entwurf“, der Fokus liegt auf „Abbrechen“, der Dialog sieht aus wie die anderen Rückfragen (Rahmen, Abstand, Knöpfe)
11. „Abbrechen“ drücken
Expected: die Rückfrage schließt, der Entwurf bleibt (Zelle gestrichelt, „1 Entwurf veröffentlichen“ steht noch da)
12. „Verwerfen“ drücken
Expected: die Rückfrage „Entwürfe verwerfen?“ erscheint
13. In der Rückfrage „Verwerfen“ drücken
Expected: die Rückfrage schließt, der Entwurf ist weg (Zelle nicht mehr gestrichelt, die Knöpfe „Verwerfen“ und „… veröffentlichen“ sind verschwunden)
14. Verwaltung › Kalender › einen Termin mit offener Anzahlung öffnen
Expected: im Termin steht der Abschnitt „Anzahlung“ (bei 100 % Anzahlung heißt der Abschnitt „Vorauszahlung“) mit dem Knopf „Anzahlung retournieren …“
15. „Anzahlung retournieren …“ drücken
Expected: die Rückfrage „Anzahlung retournieren?“ erscheint, der Fokus liegt auf „Abbrechen“, nicht im Feld „Grund“
Closes: B1, B47
Result: ☐

### Slice 26 – Abo-Stufe im Betreiberbereich nur mit Knopf und Rückfrage
Start: `dotnet user-secrets set "Operator:ApiKey" "<schlüssel>" --project src/AispaStudio.Api`, Docker Desktop starten, `dotnet run --project src/AispaStudio.AppHost`, http://localhost:5173/betreiber/ in Chrome oder Edge öffnen, mit dem Schlüssel anmelden
1. Bei einem Spa in das Feld „Abo-Stufe“ tabben (Auswahl bleibt zu)
Expected: das Feld hat den Fokus, der Knopf „Stufe ändern“ daneben ist gesperrt
2. Zweimal Pfeil runter drücken
Expected: das Feld zeigt eine andere Stufe, es erscheint keine Meldung und keine Rückfrage, die Stufe des Spas ist nach dem Neuladen der Seite (F5) unverändert
3. Eine andere Stufe wählen und „Stufe ändern“ drücken
Expected: die Rückfrage „Abo-Stufe ändern?“ nennt Spa, alte und neue Stufe und die Module, der Fokus liegt auf „Abbrechen“, der Hintergrund ist abgedunkelt
4. Esc drücken
Expected: die Rückfrage schließt, das Feld zeigt wieder die bisherige Stufe
5. Wieder eine andere Stufe wählen, „Stufe ändern“ drücken, in der Rückfrage „Stufe ändern“ drücken
Expected: die Rückfrage schließt, unter dem Feld steht „Stufe geändert.“, nach F5 zeigt das Feld die neue Stufe
6. Dasselbe im dunklen Design von Windows
Expected: Rückfrage, Knöpfe und Meldung sind gut lesbar
Closes: B58
Result: ☐

### Slice 25 – Dienstplan lesbar, Meldungen der Kasse und Buchhaltung mit Rolle
Start: Docker Desktop starten, `cd web && npm install && cd ..`, `dotnet run --project src/AispaStudio.AppHost`, Windows-App über die Ressource `windows-app`, als Inhaberin anmelden
1. Verwaltung › Dienstplan öffnen und eine Woche mit Warnungen wählen (z. B. eine Person mit mehr als 40 Stunden)
Expected: Zellenwarnungen stehen als „⚠ …“ vollständig in der Zelle, die Wochenwarnung steht unter der Stundenzahl; kein Tooltip nötig
2. Mit der Tabulatortaste auf eine Zelle mit Schicht gehen (Sprachausgabe von Windows an, Strg+Win+Eingabe)
Expected: die Sprachausgabe liest Person, Tag, Schichtzeit und Warnungen vor, z. B. „Lena Huber, Mo., 1.3.: 09:00–17:00 …“
3. Verwaltung › Buchhaltung › Kontenzuordnung: in „DATEV-BU-Schlüssel je Steuersatz“ „19“ eingeben
Expected: das Feld zeigt „19“
4. „Speichern“ drücken
Expected: direkt unter dem Knopf „Speichern“ erscheint eine rote Fehlermeldung zum Format der BU-Schlüssel; oben auf der Seite steht nichts
5. Das Feld auf „19=3;7=2“ setzen und „Speichern“ drücken
Expected: unter „Speichern“ steht „Einstellungen gespeichert.“, die Fehlermeldung ist weg
6. Oben „Buchungen anzeigen“ drücken
Expected: die Meldung unter „Speichern“ verschwindet; die Buchungen erscheinen
7. Verwaltung › Lohnverrechnung › Personalnummern: eine Nummer eintragen und das Feld verlassen
Expected: unter der Tabelle der Personalnummern steht „Personalnummer für … gespeichert.“
8. Kassa öffnen, wenn eine Kasse mit Test-Signatureinrichtung (Demo) gewählt ist
Expected: der Hinweis „Test-Signatureinrichtung …“ steht unter der Kassenauswahl wie vorher
Closes: B45, B46, B38, B53
Result: ☐

### Slice 23 – Shell der Verwaltung: main, h1, Skip-Link, Seitentitel, Kontomenü als Popover
Start: Docker Desktop starten, `cd web && npm install && cd ..`, `dotnet run --project src/AispaStudio.AppHost`, die Verwaltung im Browser über die Ressource `web` öffnen, als Inhaberin anmelden (`inhaberin@demo.aispa.test`)
1. Nach dem Laden einmal Tab drücken
Expected: oben links erscheint „Zum Inhalt springen“ mit Fokusrahmen
2. Enter drücken, dann noch einmal Tab
Expected: der Fokus springt in den Seiteninhalt (erstes Bedienelement unter der Kopfzeile), die Adresse bekommt kein `#inhalt`
3. Auf den Reiter des Browsers schauen
Expected: er heißt „Kalender · AISPA Studio“
4. Seitenreiter „Dienstplan“ drücken
Expected: der Reiter des Browsers heißt „Dienstplan · AISPA Studio“
5. Kontoknopf rechts oben (Name · Rolle) drücken
Expected: das Kontomenü öffnet rechtsbündig unter dem Knopf, Aussehen wie vorher (dunkle Fläche, Spa, Verbindung, Einträge)
6. Esc drücken
Expected: das Menü schließt, der Fokusrahmen liegt auf dem Kontoknopf
7. Kontomenü öffnen und neben das Menü in die Seite klicken
Expected: das Menü schließt
8. Kontomenü öffnen, „Hilfe“ drücken
Expected: das Menü schließt, die Hilfe öffnet
9. Mit einem Screenreader (Narrator) die Überschriften der Seite auflisten
Expected: genau eine Überschrift der Ebene 1 mit dem Namen der Seite, dazu die Landmarke „Hauptbereich“
10. In der Windows-App die Verwaltung öffnen und Tab drücken
Expected: „Zum Inhalt springen“ erscheint; die Seiten sind so breit und hoch wie vorher
Closes: B12, B13, B16
Result: ☐

### Slice 17 – Terminkarten mit Namen und Rolle für Screenreader
Start: Docker Desktop starten, `cd web && npm install && cd ..`, `dotnet run --project src/AispaStudio.AppHost`, Windows-App über die Ressource `windows-app`, als Inhaberin anmelden
1. Narrator einschalten (Strg+Windows+Eingabe) und den Kalender öffnen
Expected: der Kalender des Tages erscheint
2. Mit dem Narrator-Scan (Feststelltaste+Leertaste, dann Pfeil ab) durch eine Spalte mit Terminen gehen
Expected: jeder Termin wird mit Beginn, Gast, Behandlung und Status gelesen, z. B. „10:30 Brigitte Müller, Hot-Stone-Massage, Gebucht“, als Listeneintrag
3. Weiter durch eine Sperrzeit oder Pause gehen
Expected: Narrator liest Beginn und Grund, z. B. „12:30 Schulung Peeling, Gesperrt“
4. Einen Termin mit der Maus anklicken und im Scan auf ihn zurückgehen
Expected: Narrator nennt den Termin als ausgewählt
5. Bis zu einem Zettel und einem Vorbereitungs- oder Nachbereitungsstreifen gehen
Expected: der Zettel wird mit „Zettel“, Zeit und Text gelesen, der Streifen mit seinem Text (z. B. „Vorbereitung 15 Min.: …“), ohne „Klick:“
Closes: W50 (Teil 1)
Result: ☐

### Slice 05 – Meldungen in Kalender, Kunden, Kassa und Posteingang; Stil Border.alert entfällt
Start: Docker Desktop starten, `cd web && npm install && cd ..`, `dotnet run --project src/AispaStudio.AppHost`, Windows-App über die Ressource `windows-app`, als Inhaberin anmelden; unter Geräte ein Kartenterminal der Art „Test“ auswählen (anlegen, falls keines da ist)
1. Kassa › einen Termin kassieren, Zahlungsart Karte, Betrag auf 13 Cent (z. B. „10,13“), **Am Terminal zahlen**
Expected: unter dem Betrag erscheint „Nicht genehmigt: Abgelehnt (Testbetrag endet auf 13 Cent).“ als Fehler-Meldung mit rotem Fehlersymbol, ohne Knopf
2. Betrag ohne 13 Cent eintragen, **Am Terminal zahlen**
Expected: „Genehmigt · Testkarte …“ als Erfolgs-Meldung mit Häkchen-Symbol und dem Knopf **Stornieren** in der Meldung
3. Narrator an, Schritt 1 wiederholen
Expected: Narrator liest die Ablehnung sofort vor
4. Mit Narrator **Bezahlen** (Barzahlung mit Rückgeld)
Expected: Narrator liest das Wechselgeld vor, ohne die laufende Ansage abzubrechen
5. Kunden › Kundenkarte › Vorlieben bearbeiten, ins Feld „Sonstiges“ „Rückenschmerzen“ tippen
Expected: eine Warnung in Warnfarbe mit Warnsymbol und dunkler Schrift (nicht rot) erscheint über „Speichern“
6. Posteingang › eine Mail öffnen, Docker-Container der API anhalten, **Erledigt, ohne Termin und Antwort**
Expected: unten im Detail steht die Meldung als Fehler mit rotem Rahmen und Fehlersymbol
7. Kalender › Neuer Termin, Termin auf eine belegte Zeit legen lassen, sodass das Buchen scheitert (z. B. API anhalten), **Termin buchen**
Expected: die Fehler-Meldung steht über dem Knopf „Termin buchen“, nicht darunter
8. Einstellungen › Darstellung „Dunkel“, Schritte 1 und 5 wiederholen
Expected: Fehler und Warnung gut lesbar, Warnung ohne rote Schrift
Closes: W33, W46, W48, W63
Result: ☐

### Slice 24 – Hilfe, Begleiter, Assistent und Abgleich im Web: Fokus und Ansagen
Start: Docker Desktop starten, `cd web && npm install && cd ..`, `dotnet run --project src/AispaStudio.AppHost`, die Verwaltung im Browser über die Ressource `web` öffnen, als Inhaberin anmelden (`inhaberin@demo.aispa.test`)
1. Verwaltung › Aktionen › eine Aktion öffnen, mit Tab bis zum Fragezeichen neben „Aktion“ gehen und Enter drücken
Expected: die Hilfe öffnet rechts, der Screenreader bzw. die Bildschirmlupe steht auf der Überschrift der Hilfe (kein sichtbarer Fokusrahmen an der Überschrift)
2. Tab drücken
Expected: der Fokus liegt auf einem Knopf der Hilfe („Alle Themen“ oder „Schließen“)
3. Esc drücken
Expected: die Hilfe schließt, der Fokusrahmen liegt wieder auf dem Fragezeichen
4. Hilfe über das Fragezeichen öffnen, dann mit der Maus in die Seite klicken und Esc drücken
Expected: die Hilfe bleibt offen (Esc gehört der Seite)
5. Den runden Knopf unten rechts (KI und Hilfe) per Tab erreichen und Enter drücken
Expected: die Leiste „KI und Hilfe“ öffnet, Tab führt zu „Ausblenden“
6. Esc drücken
Expected: die Leiste schließt, der Fokusrahmen liegt auf dem runden Knopf
7. Narrator an, Leiste „KI und Hilfe“ öffnen und eine Beispielfrage drücken
Expected: Narrator sagt „Denkt nach …“ und danach die Antwort an, ohne dass der Fokus wandert
8. Im Einzelplatz (Windows-App, eingebettete Verwaltung) den Abgleich-Knopf in der Kopfzeile drücken
Expected: der Bericht öffnet rechtsbündig unter dem Knopf über dem Inhalt; Esc schließt ihn, Klick daneben ebenso
Closes: B18, B19, B21, B23
Result: ☐

### Slice 08 – Feldnamen, Warnfarbe und Speichern-Hinweis in den Kunden
Start: Docker Desktop starten, `cd web && npm install && cd ..`, `dotnet run --project src/AispaStudio.AppHost`, Windows-App über die Ressource `windows-app`, als Inhaberin anmelden
1. Kunden › Brigitte Müller wählen, zur Karte „Kennzahlen“ blättern
Expected: „Nicht erschienen“ und „Späte Absagen“ stehen in Warnfarbe (braun-orange, nicht rot) mit einem Warndreieck vor dem Datum
2. Unter „Wünsche des Gastes“ **Ändern**
Expected: jedes Feld hat eine Beschriftung darüber (Druck, Öl oder Duft, Raumtemperatur, Musik, Fokus, Nicht behandeln, Sonstiges), im leeren Feld steht nur das Beispiel „z. B. …“
3. Unter „Rechnungsempfänger“ **Ändern**
Expected: „Empfänger *“, „Anschrift *“ und „UID-Nummer“ stehen über den Feldern
4. Unter „Termine am liebsten“ **Ändern**
Expected: „Person“ steht über der ersten Auswahl, „Wünscht“ über der zweiten; das Fragezeichen steht auf Höhe der Auswahl
5. Narrator an, mit Tab durch die Felder aus Schritt 2 bis 4 gehen
Expected: Narrator nennt jedes Feld mit seiner Beschriftung, nicht mit dem Beispieltext
6. **Neue Kundin / neuer Kunde**, mit Narrator durch die Felder gehen
Expected: Narrator nennt Anrede, Vorname, Nachname, Titel vorangestellt, Rufname im Haus, Telefon, E-Mail, Notiz
7. Kundenkarte **Bearbeiten**, Vor- und Nachname leeren, mit der Maus auf **Speichern** zeigen
Expected: der Tooltip „Verfügbar, sobald ein Vor- oder Nachname eingetragen ist.“ erscheint am gesperrten Knopf
8. Einstellungen › Darstellung „Dunkel“, Schritt 1 und 2 wiederholen
Expected: Warnzeilen gut lesbar und nicht rot, Beschriftungen lesbar
Closes: W37, W39, W43, W-Muster 1 (Teil)
Result: ☐

### Slice 06 – Feldnamen und Anrede in Anmeldung, Einrichtung und Verbindung
Start: Docker Desktop starten, `cd web && npm install && cd ..`, `dotnet run --project src/AispaStudio.AppHost`, Windows-App über die Ressource `windows-app` starten
1. Auf der Anmeldeseite die Felder ansehen
Expected: über dem Codefeld steht „Code*“, über der Testkonto-Auswahl „Testkonto“
2. Narrator an, mit Tab ins Codefeld
Expected: Narrator sagt „Code“, Pflichtfeld, nicht „ABCD-EFGH“
3. Als Inhaberin anmelden, Profilbild unten links anklicken
Expected: über dem PIN-Feld steht „PIN“ (bzw. „Neue PIN“), darunter „6 bis 8 Ziffern.“, der Satz darunter siezt („wechseln Sie … Ihrem Zugang“)
4. Abmelden, auf einem Einzelplatz-PC ohne Spa die App starten (leere Datenbank)
Expected: „Spa einrichten“ zeigt sieben beschriftete Felder mit Stern, „Ihr Name“ statt „Dein Name“, der Hinweis beginnt mit „Dieser PC betreibt …“ und siezt
5. „Mit einem bestehenden Spa verbinden …“ und „Aus einer Sicherung wiederherstellen …“ öffnen
Expected: Felder „Verbindung zur zentralen Datenbank“, „Schlüsselcode“, „Sicherungsdatei“, „Wiederherstellungsschlüssel“ mit Stern und kurzem Hilfetext; „Datei wählen“ steht auf Höhe des Felds
6. Einzelplatz mit Spa: „Passwort vergessen?“
Expected: vier beschriftete Felder, unter „Neues Passwort“ steht „Mindestens 8 Zeichen.“
7. Weiterer PC ohne Server: Seite „Mit Haupt-PC im Netz verbinden“
Expected: Felder „Adresse“ und „Verbindungscode“ beschriftet, „Verbinden“ ist der gefüllte Hauptknopf
8. PC als Empfangsgerät anmelden (Geräte), abmelden
Expected: Überschrift „Wer meldet sich an?“; nach Wahl eines Namens steht „PIN für <Name>*“ über dem Feld und „4 bis 8 Ziffern.“ darunter
Closes: W1, W2, W6, W7, W21, W-Muster 11
Result: ☐

### Slice 09 – Feldnamen und Knopfverben in der Kassa
Start: Docker Desktop starten, `cd web && npm install && cd ..`, `dotnet run --project src/AispaStudio.AppHost`, Windows-App über die Ressource `windows-app`, als Inhaberin anmelden
1. Kassa › Kassieren, links bis „Gutschein verkaufen“ scrollen
Expected: die Knöpfe heißen „Wertgutschein verkaufen“, „Behandlung verkaufen“ und „Paket verkaufen“; „Wert (EUR)“ und „Behandlung“ stehen als Beschriftung über vollbreiten Feldern
2. Einen Termin kassieren, bezahlen, im Beleg **Empfänger erfassen**
Expected: über den Feldern stehen „Name oder Firma*“, „Anschrift*“ und „UID-Nummer“; „UID prüfen“ steht auf Höhe des Felds
3. Kassa › Belege, einen Beleg wählen
Expected: im Abschnitt „Beleg stornieren“ steht „Grund*“ über dem Feld, der Platzhalter beginnt mit „z. B.“
4. Kassa › Kassenlade & Abschluss
Expected: „Notiz“ über dem Notizfeld, „Grund*“ über dem Feld bei Einlage oder Entnahme
5. Narrator an, im Kassieren eine Position mit 25 % Rabatt versehen und mit Tab zu Freigabe und PIN gehen
Expected: Narrator liest „Freigabe durch“ und „PIN“ (nicht „Freigabe durch …“), beim Gutschein-Feld „Gutschein-Code“
Closes: W45, W49 (Verben), W-Muster 1 und 13 (Teil)
Result: ☐

### Slice 10 – Posteingang und Leiste „KI und Hilfe“: Namen, KI-Kennzeichen, ein Primärknopf
Start: Docker Desktop starten, `cd web && npm install && cd ..`, `dotnet run --project src/AispaStudio.AppHost`, Windows-App über die Ressource `windows-app`, als Inhaberin anmelden
1. Im Kalender Strg+Leertaste drücken, „Wann ist morgen Nachmittag eine Massage frei?“ eintippen, Enter
Expected: nur „Fragen“ ist farbig hervorgehoben; „Buchen …“ unter der Antwort ist ein normaler Knopf
2. Beim ersten Vorschlag **Buchen …** klicken
Expected: über dem Suchfeld steht „Für wen?“, im leeren Feld nur „z. B. Name, Telefon oder E-Mail“; „Termin buchen“ ist ein normaler Knopf, nicht farbig
3. Gast suchen, wählen, **Termin buchen**
Expected: „Gebucht: …“ steht in Grün mit einem Haken davor
4. Narrator an, noch eine Frage stellen
Expected: Narrator liest die Antwort vor, ohne dass der Fokus dorthin springt
5. Anfragen öffnen
Expected: jede Zeile mit Zusammenfassung zeigt die Pille „KI“ vor dem grauen Text
6. Eine Anfrage mit Antwortentwurf öffnen, zum Entwurf blättern
Expected: „Betreff“ steht über dem Betreff, „Antwort an …“ über dem Text; unter „Mit KI überarbeiten“ heißen die Knöpfe „Freundlicher formulieren“, „Kürzer formulieren“, „Siezen“, „Duzen“, „Entschuldigung ergänzen“; „Eigener Hinweis“ steht über dem Feld daneben „Überarbeiten“
7. Narrator an, mit Tab durch Betreff, Antworttext, „Wunsch ergänzen“ und „Eigener Hinweis“ gehen
Expected: Narrator nennt jedes Feld mit seiner Beschriftung, nicht mit dem Beispieltext
8. Einstellungen › Darstellung „Dunkel“, Schritt 5 und 6 wiederholen
Expected: Pille und Beschriftungen gut lesbar
Closes: W28, W29, W30, W32, W34, W35, W-Muster 1, 4, 13 (Teil)
Result: ☐

### Slice 07 – Feldnamen im Kalender und Zähler „Personen“ als Zahlenfeld
Start: Docker Desktop starten, `cd web && npm install && cd ..`, `dotnet run --project src/AispaStudio.AppHost`, Windows-App über die Ressource `windows-app`, als Inhaberin anmelden
1. Kalender › Sauna-Eintritt (Taste S), Gast wählen
Expected: neben „Von“ und „Bis“ steht „Personen“ als Zahlenfeld mit Pfeilspalte, Wert 1
2. Ins Feld „Personen“ klicken, Pfeil hoch, dann Pfeil runter drücken
Expected: der Wert steigt auf 2 und fällt wieder auf 1; unter 1 und über 20 geht es nicht
3. „25“ eintippen und das Feld mit Tab verlassen
Expected: das Feld wird rot mit dem Hinweis „Der Wert liegt zwischen 1 und 20.“, „Eintritt buchen“ ist gesperrt
4. Neuer Termin in einer Lücke, „Neue Kundin / neuer Kunde“ aufklappen
Expected: über den Feldern stehen „Vorname“, „Nachname“, „Telefon“, „E-Mail“, über der Auswahl „Behandlung“ und über der Notiz „Notiz“
5. Narrator an, mit Tab durch „Freie Zeit suchen“ (Strg+N) und „Paar oder Spa-Tag“ gehen
Expected: Narrator nennt jede Auswahl mit Namen („Behandlung“, „Bei“, „Raum“, „Tageszeit“, „Behandlung für <Gast>“, „Pause“), keine stumme Auswahl
6. Termin öffnen › Mehr › Wiederholen, „Termine prüfen“, mit Tab durch die Liste gehen
Expected: Narrator nennt an jedem Kontrollkästchen das Datum des Termins
7. Eine krank gemeldete Person: Ausfall-Assistent öffnen, mit Tab durch die Vorschläge gehen
Expected: Narrator nennt an jedem Kontrollkästchen Gast, Uhrzeit und Behandlung
Closes: W51, W52, W55, W-Muster 1 (Teil)
Result: ☐

### Slice 15 – Kundenkarten: nur eine Karte im Bearbeiten-Zustand
Start: Docker Desktop starten, `cd web && npm install && cd ..`, `dotnet run --project src/AispaStudio.AppHost`, Windows-App über die Ressource `windows-app`, als Inhaberin anmelden
1. Kunden › eine Kundin öffnen, bei „Kennzeichen“ auf „Kennzeichen setzen“ klicken
Expected: die Karte „Kennzeichen“ zeigt Auswahl und „Setzen“; „Neue Kundin / neuer Kunde“ oben rechts ist ein heller Standardknopf
2. Bei „Wünsche des Gastes“ auf „Ändern“ klicken
Expected: nur die Wünsche sind im Bearbeiten-Zustand, die Karte „Kennzeichen“ zeigt wieder nur ihre Liste
3. Neben dem Namen auf „Bearbeiten“ klicken
Expected: die Stammdaten sind offen, die Wünsche zeigen wieder ihren Text; auf der Seite ist nur „Speichern“ farbig
4. „Abbrechen“ klicken
Expected: nichts ist mehr offen, „Neue Kundin / neuer Kunde“ ist wieder farbig
Closes: W38 (Teil 1), W-Muster 4 (Teil)
Result: ☐

### Slice 16 – Kundenkarten: Esc, Enter und Startfokus
Start: Docker Desktop starten, `cd web && npm install && cd ..`, `dotnet run --project src/AispaStudio.AppHost`, Windows-App über die Ressource `windows-app`, als Inhaberin anmelden
1. Kunden › eine Kundin öffnen, bei „Rabatt“ auf „Ändern“ klicken
Expected: der Cursor steht im Feld „Rabatt (%)“, ohne Klick ins Feld
2. „25“ tippen, Tab drücken, dann Esc drücken
Expected: die Karte „Rabatt“ ist zu und zeigt den alten Rabatt; der Fokus liegt sichtbar auf „Ändern“ der Karte
3. Enter drücken (öffnet die Karte wieder), „15“ tippen, Enter drücken
Expected: die Karte ist zu und zeigt „15 %“
4. Bei „Rezeptionsvermerk“ auf „Ändern“ klicken, Enter drücken, ein Wort tippen, Strg+Enter drücken
Expected: Enter macht eine neue Zeile, Strg+Enter speichert, der Vermerk zeigt beide Zeilen
5. Bei „Termine am liebsten“ auf „Ändern“ klicken, Enter drücken
Expected: der Cursor stand in „Person“, Enter öffnet die Liste der Personen und speichert nicht
Closes: W38 (Teil 2)
Result: ☐

### Slice 28 – Kleinigkeiten Windows-App: Dialogfokus, Datumsauswahl, Richtungswörter, KI-Knöpfe
Start: Docker Desktop starten, `cd web && npm install && cd ..`, `dotnet run --project src/AispaStudio.AppHost`, Windows-App über die Ressource `windows-app`, als Inhaberin anmelden
1. Kalender › Termin öffnen › Stornieren
Expected: die Rückfrage „Termin stornieren?“ öffnet sich, der Cursor steht am Ende des Feldes „Grund“, das Feld trägt Ring und Unterstrich
2. Im Feld „Grund“ Enter drücken
Expected: nichts passiert, die Rückfrage bleibt offen; Tab führt zu den Knöpfen
3. Kassa › Beleg stornieren
Expected: der Cursor steht im Feld „Grund“
4. Kalender › Datum wählen (G), im hellen und im dunklen Design
Expected: die Tage des Vor- und Folgemonats sind gedämpft, aber gut lesbar
5. Kunden ohne Auswahl, Posteingang ohne Auswahl, Kassa ohne offene Rechnung ansehen
Expected: die Hinweise lauten „Eine Kundin oder einen Kunden aus der Liste wählen.“, „Eine Nachricht aus der Liste wählen.“ und „Einen Termin oder Artikel wählen, oder im Kalender beim Termin „Kassieren“.“
6. Mit KI-Schlüssel: Kalender › Schließzeit mit Terminen › Termine absagen
Expected: die Knöpfe heißen „Freundlicher formulieren (KI)“, „Mit Entschuldigung (KI)“, „Förmlicher formulieren (KI)“ und brechen bei Platzmangel in eine zweite Zeile um
Closes: W24, W60, W-Muster 10, W-Muster 13
Result: ☐

### Slice 11 – Namen nie aus Platzhaltern, Listenzeilen benannt, Namens-Scan streng
Start: Docker Desktop starten, `cd web && npm install && cd ..`, `dotnet run --project src/AispaStudio.AppHost`, Windows-App über die Ressource `windows-app`, als Inhaberin anmelden
1. Narrator einschalten (Strg+Win+Eingabe), Kunden öffnen, „mül“ ins Suchfeld tippen
Expected: Narrator liest das Suchfeld als „Kunden suchen“
2. Mit Tab in die Trefferliste wechseln und mit Pfeil nach unten durch die Treffer gehen
Expected: Narrator liest „Müller, Brigitte“ und danach die Telefonnummer, nicht „CustomerDto“
3. Anfragen öffnen und mit Pfeil durch die Liste gehen
Expected: jede Zeile wird mit Status, Absender, Uhrzeit und Betreff gelesen
4. Kalender › „Freie Zeit suchen“ mit einer Behandlung, dann mit Pfeil durch die freien Zeiten gehen
Expected: Narrator liest Uhrzeit, Person und Raum, bei passenden Zeiten „passt genau“
5. Kunden › Brigitte Müller › „Bearbeiten“, mit Tab bis „Titel vorangestellt“
Expected: Narrator liest „Titel vorangestellt“, nicht „z. B. Mag. Dr.“
Closes: W64, W-Muster 7
Result: ☐

### Slice 19 – Reiter und Aufklapp-Köpfe melden Auswahl und Aufklappen
Start: Docker Desktop starten, `cd web && npm install && cd ..`, `dotnet run --project src/AispaStudio.AppHost`, Windows-App über die Ressource `windows-app`, als Inhaberin anmelden
1. Narrator einschalten (Strg+Windows+Eingabe), einen Termin kassieren, sodass die Kassa offen ist
Expected: die Kassa zeigt den Reiter „Kassieren“
2. Mit Tab auf die Reiterschiene gehen, mit Pfeil rechts auf „Belege“ und Leertaste drücken
Expected: Narrator nennt „Belege“ als Registerkarte und „ausgewählt“; auf „Kassieren“ zurück sagt er „nicht ausgewählt“
3. Kalender öffnen, KI und Hilfe öffnen (Strg+Leertaste) und eine Beispielfrage stellen
Expected: unter der Antwort steht „Warum dieser Vorschlag?“
4. Mit Tab auf „Warum dieser Vorschlag?“ gehen und Leertaste drücken
Expected: Narrator sagt „erweitert“ (vorher „reduziert“), nicht „aktiviert“; die Begründung ist zu sehen
5. Einen Termin öffnen und die Warteliste öffnen, mit Tab auf die Reiter unten in der Seitenleiste gehen
Expected: Narrator nennt den gewählten Reiter „ausgewählt“
6. Kunden › eine Kundin öffnen, „Ändern“ der Stammdaten, mit Tab auf „Weitere Angaben“ und Leertaste
Expected: Narrator sagt „erweitert“
Closes: W26, W47 (Reiter), W-Muster 5
Result: ☐

### Slice 18 – Pfeiltasten-Auswahl im Kalender wird angesagt
Start: Docker Desktop starten, `cd web && npm install && cd ..`, `dotnet run --project src/AispaStudio.AppHost`, Windows-App über die Ressource `windows-app`, als Inhaberin anmelden
1. Narrator einschalten (Strg+Windows+Eingabe) und den Kalender öffnen
Expected: der Kalender des Tages erscheint
2. Pfeil ab drücken
Expected: der erste anstehende Termin ist markiert, Narrator sagt ihn an, z. B. „10:30 Brigitte Müller, Hot-Stone-Massage, Gebucht“, Listeneintrag, ausgewählt
3. Pfeil ab und Pfeil auf drücken
Expected: Narrator sagt jeweils den nächsten bzw. vorigen Termin der Spalte an
4. Pfeil rechts und Pfeil links drücken
Expected: Narrator sagt den Termin in der Nachbarspalte an; nur der Auswahlring markiert die Karte, kein zweiter Fokusrahmen
5. Tab drücken
Expected: der Fokus springt wie bisher aus der Kalenderfläche weiter, nicht von Karte zu Karte
Closes: W50 (Teil 2)
Result: ☐

### Slice 20 – Knopfnamen mit Bezug statt Zahl, Uhrzeit oder Initialen
Start: Docker Desktop starten, `cd web && npm install && cd ..`, `dotnet run --project src/AispaStudio.AppHost`, Windows-App über die Ressource `windows-app`, als Inhaberin anmelden
1. Narrator einschalten (Strg+Win+Eingabe), mit Tab durch die Navigation gehen
Expected: Narrator liest bei „Anfragen“ die Zahl mit, z. B. „Anfragen, 3 offen“; der Farbschema-Knopf heißt „Farbschema: Hell“, der Konto-Knopf „Konto“ mit Name und Rolle statt der Initialen
2. Kalender › Tagesüberblick öffnen, mit Tab durch die Kennzahlen gehen
Expected: Narrator liest „Termine“ mit der Zahl des Tages, z. B. „Termine, 24“, danach „Eingecheckt, …“ und „Auslastung Team, …“; unter „Als Nächstes“ Uhrzeit, Gast und Behandlung
3. Kalender › Paar oder Spa-Tag, einen weiteren Gast und einen Abschnitt hinzufügen, mit Tab zu den Schließen-Kreuzen gehen
Expected: Narrator liest „Gast 2 entfernen“ und „Abschnitt 2 entfernen“
4. Kassa › einen Termin kassieren, mit Tab zu den Knöpfen neben der Position gehen
Expected: Narrator liest „Rabatt auf …“ und „… entfernen“ mit dem Namen der Position; die offenen Termine unter „Heute abzurechnen“ nennen Uhrzeit, Gast, Behandlung und Preis
5. Einzelplatz mit abgelehnter Zeile im Abgleich: Abgleich öffnen, mit Tab zu „Erledigt“ gehen
Expected: Narrator liest „Erledigt: Termine“ mit Datum und Uhrzeit der Zeile
Closes: W14, W20, W61, W47 (Zeilen), W-Muster 6
Result: ☐

### Slice 21 – Ein Primärknopf je Fläche im Termin-Panel, Abbrechen als Ghost
Start: Docker Desktop starten, `cd web && npm install && cd ..`, `dotnet run --project src/AispaStudio.AppHost`, Windows-App über die Ressource `windows-app`, als Inhaberin anmelden
1. Kalender › einen eingecheckten Termin öffnen
Expected: nur „Kassieren“ ist hervorgehoben (gefüllt)
2. Auf den Stift an der Notiz klicken
Expected: nur „Notiz speichern“ ist hervorgehoben, „Kassieren“ ist ein normaler Knopf, „Abbrechen“ hat keine Fläche und keinen Rahmen
3. „Abbrechen“ klicken
Expected: die Notiz-Karte schließt, „Kassieren“ ist wieder hervorgehoben
4. Einen gebuchten Termin öffnen, Mehr › „Zeiten dieses Termins ändern …“
Expected: nur „Übernehmen“ ist hervorgehoben, „Einchecken“ nicht, „Abbrechen“ ohne Fläche
5. Einen gebuchten Paartermin öffnen
Expected: nur „Einchecken“ ist hervorgehoben, „Alle einchecken (2)“ ist ein normaler Knopf
6. Meldungen › „+ Notiz“, und Warteliste › „Erledigt …“
Expected: „Abbrechen“ hat jeweils keine Fläche und keinen Rahmen
Closes: W53, W-Muster 4 (Teil Termin-Panel)
Result: ☐
