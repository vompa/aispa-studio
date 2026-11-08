# Masken: Vorgaben für Windows-App und Verwaltung

Verbindlich für **neue und geänderte Masken** (Formulare, Seitenleisten zum Eingeben, Dialoge) in der Windows-App und in der Verwaltung (Web). Grundlage: [ADR 0067](../entscheidungen/0067-masken-und-meldungen-nach-material-3-und-fluent-2.md). Meldungen stehen in [meldungen.md](meldungen.md).

**Werte stehen nur in [docs/design.md](../design.md)** (Größen, Abstände, Farben, Radien, Dauer, Knopfarten); dieses Dokument legt Aufbau und Verhalten fest. Bei einem Widerspruch zwischen Werten gilt design.md; der Abgleich vom 21.10.2026 steht im [PR-Protokoll](../umsetzung/pr-protokoll.md) ([ADR 0067](../entscheidungen/0067-masken-und-meldungen-nach-material-3-und-fluent-2.md), Nachtrag).

Anlass: Der Auftraggeber fand die Kundendaten-Maske „viel zu unruhig“ (Rückmeldung vom 21.10.2026). Sie hat 18 Titel-Chips, vier Hilfe-Fragezeichen, einen Dauer-Hinweistext, eine Vorschau mitten im Formular und zehn Zeilen mit je anderer Spaltenaufteilung.

## 1. Quellen und Grenzen

Was geprüft wurde und was nicht (Stand 21.10.2026):

| Quelle | Ergebnis |
| --- | --- |
| [Microsoft: Forms (Windows apps)](https://learn.microsoft.com/windows/apps/design/controls/forms) | Vollständig gelesen: Beschriftung links ausgerichtet **über** dem Feld; Pflicht mit Stern an der Beschriftung; Eingabehinweis (Platzhalter); passendes Steuerelement statt Freitext (z. B. Datumsauswahl); Speichern erst aktiv, wenn Pflichtangaben da sind; Fehler am Feld markieren; Abstände Feld zu Feld 24 px, Gruppe zu Gruppe 48 px (Beispielwerte bei 80 px hohen Feldern); Spalten nur nach Zahl der Felder und Fenstergröße; „bei fünf oder mehr Auswahlwerten eine Liste“; `AutoSuggestBox` für Vorschläge beim Tippen. |
| [Microsoft: Labels](https://learn.microsoft.com/windows/apps/design/controls/labels) | Nur über die Suchergebnisseite geprüft: Beschriftung als Substantiv oder kurze Wortgruppe, kein Satz, keine Anweisung, kein Doppelpunkt. |
| [Google web.dev: Form design basics](https://web.dev/learn/forms/design-basics) | Vollständig gelesen: sichtbares `<label>` zu jedem Feld, über dem Feld, kurz; Platzhalter nie als Beschriftung; Pflichtfelder offensichtlich kennzeichnen, bei überwiegend Pflicht die optionalen; Regeln vorab nennen, mit `aria-describedby` verknüpfen; Fehler neben dem Feld, nach fehlgeschlagenem Absenden zum ersten ungültigen Feld springen; **eine Spalte**; Tab-Reihenfolge wie Anzeige; richtige Eingabetypen; Tippflächen mindestens 48 px (Touch). |
| [Material Web: Text field](https://material-web.dev/components/text-field) und [Material 2: Text fields](https://m2.material.io/go/design-text-fields) | Über Suchergebnisse: Hilfetext (Supporting text) unter dem Feld, bei Fehler **ersetzt** der Fehlertext den Hilfetext; Hilfetext einsprachig kurz auf einer Zeile; Fehler beschreibt, wie man ihn vermeidet; Fehlertext nicht zusätzlich unter den Hilfetext stellen (Layoutsprung); Symbol zusätzlich zur Farbe für Farbfehlsichtige. |
| [Material Design 3: Text fields](https://m3.material.io/components/text-fields/guidelines) | **Nicht lesbar:** die Seite wird per JavaScript gerendert, der Abruf lieferte nur den Titel. M3-Aussagen oben stammen aus Material Web (setzt M3 um) und Material 2. |
| [Fluent 2](https://fluent2.microsoft.design) Field/Input | Seite nicht eigens abgerufen; Aufbau Beschriftung über Feld, Hilfetext darunter, Fehler am Feld folgt aus den Windows-Forms-Richtlinien und ist **Annahme** für die Windows-App. |
| Chrome „modern-web-guidance“ | Nicht neu abgerufen; Skill im Projekt-Setup (native Elemente `<fieldset>`, `<details>`, `<dialog>`, `<datalist>`, `popover`). Die Regel für das Web (Abschnitt 8) folgt ihm. |

Aussagen zu Progressive Disclosure, Gruppierung nach Häufigkeit und Hilfe-Fragezeichen je Gruppe sind **Gestaltungsentscheidungen dieses Projekts** auf Grundlage der obigen Quellen und der Rückmeldung des Auftraggebers, keine wörtlichen Zitate aus Material oder Fluent. Die Zahlen für Abstände sind aus `Themes/Styles.axaml` abgeleitet (Abschnitt 4), nicht aus Microsofts Beispielwerten (die rechnen mit größeren, touchtauglichen Feldern).

## 2. Regeln

Jede Regel: **Vorgabe**, Begründung, Quelle. „Prüfbar“ heißt: im Review per Blick oder Suche feststellbar.

### 2.1 Aufbau

**R1. Gruppen mit kurzer Überschrift, Reihenfolge nach Häufigkeit.** Eine Maske besteht aus 2 bis 5 Gruppen („Name“, „Kontakt“, „Anschrift“, „Weitere Angaben“). Überschrift `TextBlock.h3` (Zwischentitel, design.md Abschnitt 3), höchstens drei Wörter, kein Doppelpunkt. Was bei fast jeder Eingabe gebraucht wird, steht oben; Seltenes unten oder eingeklappt.
Begründung: Wer eine Maske überfliegt, liest Überschriften; ohne Gruppen wirkt jede Zeile gleich wichtig. Quelle: Microsoft Forms (Gruppenüberschrift `SubtitleTextBlockStyle`, Gruppen mit größerem Abstand) und Projekt-Entscheidung zur Reihenfolge.

**R2. Seltene Felder sind eingeklappt („Weitere Angaben“).** Felder, die in weniger als etwa einem Fünftel der Fälle ausgefüllt werden, stehen in einem eingeklappten Bereich am Ende. Ist ein Feld darin schon befüllt, klappt der Bereich beim Öffnen der Maske auf. Die Überschrift nennt, was darin ist („Weitere Angaben: Pronomen, Titel, Notiz“) und zeigt den Zustand.
Begründung: Weniger Felder gleichzeitig sichtbar ist das wirksamste Mittel gegen Unruhe; nichts geht verloren, weil befüllte Felder sichtbar bleiben. Quelle: Projekt-Entscheidung; Microsoft rät bei vielen Feldern zu mehreren Seiten statt „alles auf einmal“ (Forms, Abschnitt Columns).

**R3. Eine Spalte als Regelfall, zwei nur für zusammengehörige Kurzfelder.** Die Maske hat **eine** Hauptspalte. Zwei Felder stehen nebeneinander nur, wenn sie inhaltlich ein Paar sind (Vorname/Nachname, PLZ/Ort) und beide kurz sind. Nie mehr als drei Felder in einer Zeile.
Begründung: Einspaltige Formulare werden schneller und fehlerärmer ausgefüllt. Quelle: Google web.dev; Microsoft Forms (Spaltenzahl nach Zahl der Felder).

**R4. Festes Raster: Feldbreite folgt der erwarteten Eingabe.** Die Maske hat ein 12-teiliges Raster; Breiten sind Vielfache von 1/12 der Maskenbreite bzw. feste Werte: PLZ 96 px, Land 72 px, Datum 160 px, Anrede 160 px, Telefon und Ort halbe Breite, Name, Straße, E-Mail, Notiz volle Breite („volle Breite“ heißt: Breite des Inhaltsbereichs, siehe Abschnitt 4; in der 360-px-Seitenleiste stehen daher nur Paare mit zwei Kurzfeldern nebeneinander, sonst eine Spalte). Die Spaltenzahl ist je Gruppe gleich; Felder einer Gruppe beginnen am selben linken Rand. Kein `UniformGrid` mit leeren Zellen. Die Breiten stehen in design.md 5.2.
Begründung: Die Feldbreite verrät die erwartete Eingabe (PLZ schmal, Straße breit), und gleiche Ränder beruhigen. Quelle: Microsoft Forms (Beispielformular: Stadt 200, Bundesland 100, PLZ 100 px, Straße 400 px).

**R5. Eine primäre Aktion je Maske, unten links.** „Speichern“ (`Classes="primary"`) als einzige Hauptschaltfläche, daneben „Abbrechen“ (`ghost`); in einer Karte der Verwaltung stattdessen „Verwerfen“ (sekundär, nur mit ungespeicherten Änderungen), siehe design.md 5.1. In Seitenleisten und Dialogen steht die Leiste **fest im Fuß** (nicht im scrollenden Inhalt); in der Seite am Ende der Maske, linksbündig zur Feldkante. Zerstörende Aktionen („Löschen“) stehen getrennt, `danger`, nie neben „Speichern“. „Speichern“ ist deaktiviert, solange Pflichtangaben fehlen.
Begründung: Der Weg zur Hauptaktion ist immer gleich und immer sichtbar, auch bei langem Inhalt. Quelle: Microsoft Forms (Senden-Schaltfläche deaktivieren, bis Pflichtangaben da sind; Beispiel „Save/Cancel“ links unter dem Feld); Fuß der Seitenleiste nach [ADR 0066](../entscheidungen/0066-seitenleisten-ein-muster-und-ki-wirkt-auf-die-offene-seite.md).

**R5a. Eine Maske je Bereich, Tiefe nach Rolle.** Ein Bereich (Kassa, Kundenkarte, Dienstplan, Lager) hat eine Maske für alle Rollen. Oben und zuerst steht, was die Rezeption täglich braucht; was nur Inhaberin und Leitung verwalten, steht dahinter in derselben Maske (eingeklappt, in einem Unterreiter oder in einem Panel) und ist für andere Rollen ausgeblendet. Eine eigene Maske gibt es nur, wenn die Aufgabe eine andere ist (z. B. Einstellungen einer Kasse statt Kassieren). Die Rechte prüft immer die API; die Maske blendet nur aus.
Begründung: Wer verwaltet, arbeitet auch im Alltag mit derselben Maske und muss nicht zwischen zwei Fassungen suchen; die Rezeption sieht nur, was sie braucht. Quelle: Auftraggeber, 03.11.2026 (Ä353, Runde R07); Vorbild ist die Kassa mit „Heute abzurechnen“ vorn und dem zugeklappten Panel „Artikel und Gutscheine“ (Slice 137).

### 2.2 Felder

**R6. Beschriftung über dem Feld, Hilfetext darunter.** Beschriftung `TextBlock.label` (Stufe „Beschriftung“, design.md Abschnitt 3), linksbündig, **Substantiv** („Vorname“, nicht „Bitte Vornamen eingeben“), ohne Doppelpunkt, ohne Klammerzusatz außer für Einheiten („Vorname“ statt „Vorname (gültiger Name)“, aber „Dauer (min)“; die Erklärung steht im Hilfetext). Hilfetext: `TextBlock.small` (Stufe „Klein“, `Fg2`) **unter** dem Feld, höchstens eine Zeile; erscheint nur, wo er wirklich hilft. Kein Platzhalter als Beschriftung; der Platzhalter darf nur ein Beispiel zeigen („z. B. 1010“).
Begründung: Beschriftungen über dem Feld lesen sich am schnellsten; Platzhalter verschwinden beim Tippen. Quelle: Microsoft Forms und Labels; Google web.dev; Material Web (Supporting text unter dem Feld).

**R7. Pflichtfelder kennzeichnen statt erklären.** Pflicht: Stern `*` in Textfarbe `Fg` (nie rot) hinter der Beschriftung (mit `AutomationProperties.IsRequiredForForm`). Gibt es nur ein oder zwei Pflichtfelder, genügt das; sind fast alle Felder Pflicht, werden stattdessen die optionalen mit „optional“ gekennzeichnet. Der Satz „Leere Felder löschen den gespeicherten Wert. Nur ein Name ist Pflicht.“ entfällt; das Verhalten beim Leeren gehört in die Hilfe (R9), nicht in die Maske.
Begründung: Ein Stern kostet kein Auge, ein Satz wird nicht gelesen. Quelle: Microsoft Forms („Mark required input with an asterisk“); Google web.dev (Pflicht kennzeichnen, bei überwiegender Pflicht Optionale kennzeichnen).

**R8. Fehler am Feld, ein Fehlertext ersetzt den Hilfetext.** Ungültige Eingabe: roter Rand am Feld, Fehlertext (Stufe „Klein“, `danger`) **an der Stelle des Hilfetexts**, mit Warnsymbol (nicht nur Farbe), Text nennt Problem **und** Weg („Die PLZ hat vier Ziffern.“). Bei einem schmalen Feld in einer Zeile mit anderen Feldern (PLZ, Land) bricht der Text nicht in der Feldbreite um: `FormField.HideMessage` blendet ihn unter dem Feld aus, eine Zeile unter der Feldzeile zeigt ihn über die ganze Zeilenbreite (Feld bleibt rot). Geprüft wird beim Verlassen des Feldes und beim Speichern; nach fehlgeschlagenem Speichern springt der Fokus ins **erste ungültige Feld** (Entscheidung des Auftraggebers, [ADR 0069](../entscheidungen/0069-fluent-2-als-grundlage-der-windows-app.md); Fluent 2 würde der InfoBar den Fokus geben, die InfoBar wird hier zusätzlich angezeigt und nimmt den Fokus nicht). Ein maskenweiter Fehler (Server nicht erreichbar, Speichern abgelehnt) steht **über der Aktionsleiste** als InfoBar-Fehler ([meldungen.md](meldungen.md), M3), nicht als roter Block mitten im Formular.
Begründung: Der Fehler gehört dorthin, wo die Person hinsieht und korrigiert. Quelle: Material 2 / Material Web (Fehlertext ersetzt Hilfetext; Weg zur Lösung nennen; Symbol zusätzlich zur Farbe); Google web.dev (Fehler am Feld, Fokus zum ersten ungültigen Feld); Microsoft Forms (ungültige Felder hervorheben); Microsoft Message bar (bei Absenden mit Fehlern: Leiste oben im Formular und Fehler am Feld).

**R9. Hilfe: höchstens ein Fragezeichen je Gruppe, nicht je Feld.** Das `HelpButton` steht rechts in der Gruppenüberschrift und öffnet das Hilfethema der Gruppe (Abschnitt = Gruppe). Die Pflicht aus [ADR 0055](../entscheidungen/0055-hilfe-zu-jeder-einstellung-im-repository.md) bleibt erfüllt: Jedes Feld hat weiter seinen Absatz im Hilfethema `web/src/help/content/<thema>.md`; der Abschnittsanker der Gruppe führt dorthin, die Felder erscheinen als Liste. Einzelne Felder mit heikler Wirkung dürfen ausnahmsweise ein eigenes Fragezeichen behalten (höchstens eines je Gruppe zusätzlich), begründet im PR.
Begründung: Vier Fragezeichen in einer Maske sind vier optische Anker mehr; eine Gruppe ist die Einheit, nach der Menschen suchen. Quelle: Projekt-Entscheidung; Microsoft Forms (Hilfe nicht an jedem Feld, sondern Beschriftung + Hinweis).

**R10. Auswahl nach Zahl der Werte.** 2 bis 4 Werte: Optionsfelder (`RadioButton`; die segmentierte Schiene wechselt nur Ansichten); 5 bis etwa 12 feste Werte: Auswahlliste (`ComboBox`); mehr als etwa 12 oder frei erweiterbar: **Eingabe mit Vorschlagsliste** (`AutoCompleteBox` in Avalonia, `<input list>` + `<datalist>` im Web); nie eine Wolke aus Chips oder Schaltflächen als Ersatz für ein Eingabefeld. Chips sind nur für wenige (höchstens sechs) häufige Schnellwerte neben einem Eingabefeld erlaubt.
Begründung: Eine Vorschlagsliste hält die Maske ruhig, ist per Tastatur schneller und bleibt bei 50 oder 500 Werten gleich groß. Quelle: Microsoft Forms („bei fünf oder mehr Auswahlwerten eine Liste“, `AutoSuggestBox` für Vorschläge beim Tippen, `ComboBox` für lange Listen).

**R11. Abgeleitete Werte als ruhige Textzeile.** Vorschau und berechnete Werte (Anrede-Vorschau, Endpreis, Dauer) stehen als eine Zeile `TextBlock.secondary.small` mit kleiner Überschrift am Ende der betreffenden Gruppe, **ohne** Karte, Rand oder Hintergrund und ohne eigene Bedienelemente. Der Schalter, der die Vorschau verändert, steht in der Gruppe darüber, nicht in der Vorschau.
Begründung: Eine Karte in der Maske erzeugt den Eindruck eines zweiten Eingabebereichs. Quelle: Projekt-Entscheidung (Rückmeldung des Auftraggebers zur Unruhe).

**R12. Richtiges Steuerelement, richtiger Eingabetyp.** Datum, Uhrzeit, Zahl, Telefon, E-Mail bekommen das passende Steuerelement bzw. den passenden Typ (`type="tel"`, `type="email"`, `type="date"`, `inputmode`), Zeichenlängen begrenzen (`MaxLength`), Format als Beispiel-Platzhalter. Freitext für Datum ist nur erlaubt, wenn ein Datums-Steuerelement für den Fall nicht taugt (z. B. Geburtstag ohne Jahr), mit Format im Hilfetext. Datum, Uhrzeit, Zahl und Betrag in der Windows-App folgen [design.md 5.17 bis 5.20](../design.md): tolerant lesen, nach dem Verlassen normalisieren, ungültige Eingabe ergibt einen Fehlertext, nie stummes Runden.
Begründung: Ungültiges lässt sich nicht eingeben, Mobil- und Bildschirmtastatur passen. Quelle: Microsoft Forms (richtiges Steuerelement, `InputScope`); Google web.dev (Eingabetypen).

**R13. Tastatur und Fokus.** Tab-Reihenfolge entspricht der Anzeige, der Fokus beim Öffnen liegt im ersten Feld (bei Ansicht ohne Eingabe: nirgends, siehe ADR 0066), Enter in einem einzeiligen Feld bestätigt das Feld und löst den Standardknopf aus (im mehrzeiligen Feld ist Enter der Zeilenumbruch, Strg+Enter löst aus), Esc geht in Stufen (offene Auswahl zu, geändertes Feld auf den letzten bestätigten Wert, dann Maske schließen; [design.md 5.16](../design.md)), Beschriftungen sind mit dem Feld verbunden (`AutomationProperties.LabeledBy` bzw. `<label for>`).
Begründung: Verwaltungsarbeit wird mit der Tastatur erledigt. Quelle: Microsoft Forms (Tab stops); Google web.dev (Tab-Reihenfolge, Tastatur-Test); ADR 0066.

## 3. Windows-App und Verwaltung

Aufbau, Raster und Verhalten (Abschnitt 2) sind in beiden gleich. **Windows-App:** Farben und Steuerelemente im Fluent-Stil der App (`Themes/Styles.axaml`, DynamicResource-Pinsel; keine festen Farbwerte). **Verwaltung:** dieselben Regeln mit denselben Abständen als CSS-Variablen; Farben aus dem bestehenden Theme.

## 4. Abstände (Raster 4 und 8 px)

Die Werte kommen aus [design.md 4.1](../design.md) und gelten für Windows-App und Verwaltung gleich; sie werden hier nicht wiederholt, nur dem Aufbau zugeordnet:

| Stelle | Wert aus design.md |
| --- | --- |
| Beschriftung zu Feld, Feld zu Hilfetext | 4 px |
| Feld zu Feld (senkrecht) und Spalte zu Spalte | 12 px |
| Gruppe zu Gruppe, **ohne** Trennlinie | 20 px |
| Gruppenüberschrift zu erstem Feld | 8 px (Zwischentitel zu Inhalt) |
| Innenabstand der Seitenleiste | 16 oben/unten, 20 links/rechts (5.12) |
| Aktionsleiste im Fuß | Trennlinie `line` oben, 12 senkrecht, 16 seitlich; 8 px zwischen Knöpfen |
| Maskenbreite | 360 px Seitenleiste ([ADR 0066](../entscheidungen/0066-seitenleisten-ein-muster-und-ki-wirkt-auf-die-offene-seite.md)); 560 px breites Formular (Kundendaten); Karte der Verwaltung höchstens 640 px, linksbündig |

Feldhöhe ist 34 px (design.md 4.4). Die Gruppenabstände bleiben größer als die Feldabstände, damit Gruppen erkennbar sind. **Nicht geprüft** ist, ob diese Abstände bei 125 und 150 Prozent Windows-Skalierung ruhig wirken; das ist beim Umbau der ersten Maske im Fenster zu prüfen.

## 5. Verwaltung (Web): native Elemente

- Gruppen: `<fieldset>` mit `<legend>` (Überschrift der Gruppe); kein `div` mit Fettdruck.
- Eingeklappte „Weitere Angaben“: `<details><summary>`; `open` setzen, wenn ein Feld darin Inhalt hat.
- Vorschläge: `<input list="…">` mit `<datalist>`; Eingabetypen `tel`, `email`, `date`, `inputmode`, `autocomplete`.
- Dialoge und Seitenleisten zum Eingeben: `<dialog>` bzw. Popover-API, Fokus und Esc vom Browser, kein eigener Fokusfänger.
- Pflicht und Fehler: `required` plus `aria-required`, `aria-invalid`, Fehlertext mit `aria-describedby` am Feld; `aria-live` nur für die Meldung über der Aktionsleiste.
- Kein `placeholder` als Beschriftung, kein Doppelklick-Verhalten, Tippflächen mindestens 32 px (mit Maus), 48 px nur auf Touch (`pointer: coarse`).

## 6. Kurz-Checkliste bei jeder neuen oder geänderten Maske

1. Gruppen mit kurzer Überschrift, Häufiges oben (R1).
2. Seltene Felder in „Weitere Angaben“ eingeklappt, befüllt = offen (R2).
3. Eine Hauptspalte; Paare nur für Kurzfelder, höchstens drei in einer Zeile (R3).
4. Feldbreite passt zur Eingabe, Ränder gleich, kein leeres Raster (R4).
5. Eine primäre Aktion, unten links bzw. fest im Fuß; Speichern deaktiviert ohne Pflichtangaben (R5).
6. Beschriftung = Substantiv, über dem Feld, kein Doppelpunkt, kein Klammerzusatz (R6).
7. Hilfetext eine Zeile unter dem Feld; Platzhalter nur als Beispiel (R6).
8. Pflicht mit Stern (oder „optional“ bei fast nur Pflicht); kein Erklärsatz zum Leeren (R7).
9. Fehler am Feld mit Symbol, Text nennt Problem und Weg; Fokus zum ersten ungültigen Feld, InfoBar zusätzlich ohne Fokus (R8).
10. Höchstens ein Fragezeichen je Gruppe; jedes Feld steht im Hilfethema (R9, ADR 0055).
11. Mehr als etwa 12 Werte: Eingabe mit Vorschlagsliste, keine Chip-Wolke (R10).
12. Abgeleitete Werte als eine ruhige Textzeile ohne Karte (R11).
13. Passendes Steuerelement und Eingabetyp, Längen begrenzt (R12).
14. Tab-Reihenfolge, Startfokus, Enter, Esc, Beschriftung am Feld (R13).
15. Abstände und Maße nur aus design.md 4.1 und 4.4; Web mit `fieldset`, `details`, `dialog`, `datalist`.

## 7. Umbauvorschlag Kundendaten-Maske

Betrifft `Views/CustomerContactForm.axaml` (gilt in der Kundenkarte und im Panel des Kalenders) und `CustomerContactFormViewModel`. **Umgesetzt am 21.10.2026** (Abweichungen: „Weitere Angaben“ ist ein eigener Kopf mit Umschalter statt des Avalonia-`Expander`, dessen Vorlage Rand und Fläche mitbringt; Pflicht bleibt ein Name, Vor- **oder** Nachname, wie am Server, beide Felder tragen den Stern; drei Fragezeichen an Name, Kontakt und Weitere Angaben, die Anschrift hat keines, ihr Hilfetext steht unter Kontakt).

### 7.1 Heute

- Zehn Zeilen, sieben verschiedene Spaltenaufteilungen (`130,*,*`; 2 gleich; `2*,90,*,70`; …).
- 18 Chips unter den beiden Titelfeldern (`TitlesBeforeChoices`, `TitlesAfterChoices`), vier Hilfe-Fragezeichen, Vorschau als Karte mit Schalter („Titel in der Anrede“) mitten drin, Dauerhinweis „Leere Felder löschen …“, Fehler als roter Block unter dem letzten Feld, Beschriftungen mit Klammerzusatz („Vorname (gültiger Name)“).

### 7.2 Neu

Breite 560 px (wie bisher), Gruppenüberschrift `h2` mit Fragezeichen rechts, Abstände nach Abschnitt 4.

**Gruppe „Name“** (Hilfe: `kunden-name-und-anrede`, Abschnitt `name`)
1. Zeile: `Anrede` (160 px, ComboBox) | `Vorname *` (Rest) | `Nachname *` (Rest). Drei Felder, eine Zeile, drei Spalten (160 / 1* / 1*), Pflicht mit Stern.
2. Hilfetext unter der Zeile (eine Zeile): „Der gültige Name wie im Ausweis; Rufname und Titel unter Weitere Angaben.“
3. Ruhige Zeile (R11), nur wenn Name ausgefüllt: **„Anschrift: Frau Mag. Dr. Maria Muster“** und darunter „Anrede: Sehr geehrte Frau Dr. Muster“ (`secondary small`, ohne Karte).

**Gruppe „Kontakt“** (Hilfe: `kunden-name-und-anrede`, Abschnitt `kontakt`)
4. `Telefon` (1/2) | `E-Mail` (1/2).
5. `Sprache` (ComboBox, 240 px) | `Geburtstag` (160 px, Format als Beispiel „z. B. 14.03.1985“).

**Gruppe „Anschrift“** (Hilfe: Abschnitt `postanschrift`)
6. `Straße und Hausnummer` (volle Breite).
7. `PLZ` (96 px) | `Ort` (Rest) | `Land` (72 px, Vorgabe „AT“).

**Eingeklappt: „Weitere Angaben“** (`Expander`, zu; öffnet automatisch, wenn eines der Felder befüllt ist; Überschrift mit Zusammenfassung „Rufname, Pronomen, Titel, Notiz“)
8. `Rufname im Haus` (1/2, Beispiel „z. B. Steffi“) | `Pronomen` (1/2, Beispiel „z. B. sie/ihr“, `MaxLength` 40).
9. `Titel vorangestellt` (1/2, `AutoCompleteBox` mit Vorschlagsliste aus `TitlesBeforeChoices`; Mehrfachtitel durch Leerzeichen) | `Titel nachgestellt` (1/2, `AutoCompleteBox` aus `TitlesAfterChoices`). **Keine Chips mehr.** Gewählter Vorschlag wird an die vorhandenen Titel angehängt (`AppendTitle` bleibt, `AddTitleBeforeCommand`/`AddTitleAfterCommand` entfallen als Knopf-Befehle, bei Auswahl aus der Liste aufgerufen); Tippen filtert nach Anfang des Titels, ohne Vorschläge bleibt Freitext möglich.
10. `Titel in der Anrede` (ComboBox, 200 px) mit Hilfetext „Ob der Titel in der Anrede erscheint.“ (der Schalter steht **hier**, die Vorschau bleibt in der Gruppe Name; Änderungen wirken sofort in der Zeile „Anrede: …“).
11. `Notiz` (volle Breite, mehrzeilig 2 Zeilen), Hilfetext: „Vorlieben; keine Gesundheitsdaten (die gehören in die Anamnese).“

**Fuß** (fest, Trennlinie oben; in Kundenkarte am Ende der Maske):
- Links `Speichern` (primär, deaktiviert ohne Vor- und Nachname) und `Abbrechen` (ghost).
- Maskenweiter Fehler als InfoBar-Fehler **über** dem Fuß (nicht mehr unter dem letzten Feld); Feldfehler (z. B. PLZ, E-Mail, Geburtstag, Land) am Feld.

### 7.3 Was entfällt

- Der Satz „Leere Felder löschen den gespeicherten Wert. Nur ein Name ist Pflicht.“ (Pflicht ist mit Stern gekennzeichnet; das Leeren steht im Hilfethema, Abschnitt `kontakt`).
- Die Karte „VORSCHAU“ (wird Textzeile) und das Overline-Etikett.
- Die Klammerzusätze „(gültiger Name)“.
- Die Fragezeichen an Anrede, Rufname, Pronomen, Titel, Titel in der Anrede, Sprache: bleiben als **drei** Fragezeichen an den Gruppen Name, Kontakt/Anschrift und Weitere Angaben (ein Fragezeichen an „Weitere Angaben“ statt vier an Einzelfeldern). Die Hilfetexte zu den einzelnen Feldern bleiben im Thema `kunden-name-und-anrede` erhalten; die Abschnitte der Fragezeichen verschieben sich auf die Gruppe, die Anker `anrede`, `rufname`, `pronomen`, `titel`, `titel-in-der-anrede`, `sprache-des-gastes` werden als Unterpunkte im Gruppenabschnitt geführt (Anker nicht löschen, damit bestehende Verweise und KI-Antworten gültig bleiben).

### 7.4 Akzeptanz

- In der Grundansicht sind 11 Felder sichtbar (statt 17 Felder plus 18 Chips), kein Chip.
- Eine Spaltenaufteilung je Gruppe; alle Ränder fluchten.
- Hilfe-Fragezeichen: höchstens eines je Gruppe.
- Test (Avalonia Headless): „Weitere Angaben“ ist zu, wenn alle seltenen Felder leer sind, und offen, wenn eines befüllt ist; Auswahl eines Titels aus der Vorschlagsliste hängt ihn an; „Speichern“ ist ohne Namen deaktiviert; ungültige PLZ zeigt Fehler am Feld und setzt den Fokus dorthin.
- Hilfe-Test (`web/src/help`) bleibt grün (Anker vorhanden); Handbuchbilder neu erzeugen (`scripts/help-images.sh`, ADR 0056).

## 8. Übernahme in bestehende Masken

Altbestand wird **nicht** auf einmal umgebaut. Regel: Wer eine Maske ohnehin ändert, bringt sie in demselben Pull Request auf diese Vorgaben, mindestens Gruppen (R1), Abstände (Abschnitt 4), Fuß (R5) und Fehler am Feld (R8). Die Reihenfolge nach Wirkung: Kundendaten, Neuer Eintrag, Wunsch eintippen, dann die Einstellungsseiten.
