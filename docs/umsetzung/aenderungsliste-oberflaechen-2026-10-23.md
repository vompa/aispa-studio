# Änderungsliste Oberflächen (Durchsicht vom 23.10.2026)

Ergebnis der Durchsicht aller Masken der Windows-App und der Web-Oberflächen, wie vom Auftraggeber angekündigt („wir werden alle Masken durchgehen müssen“). Maßstab: der Design-Brief [design.md](../design.md) als Wertequelle, die Masken- und Meldungs-Checklisten ([gestaltung/masken.md](../gestaltung/masken.md), [gestaltung/meldungen.md](../gestaltung/meldungen.md)), für die Windows-App der Skill `fluent2-avalonia`, für das Web die Skills `fluent2-web`, `modern-web-guidance`, `impeccable` und `emil-design-eng` (`.claude/skills/`, [ADR 0069](../entscheidungen/0069-fluent-2-als-grundlage-der-windows-app.md)).

Stand: Branch `dev` auf `ee66289`. Beide Durchsichten haben nur Quelltext gelesen, nichts gebaut, nichts gestartet. Kontrastwerte sind aus `Colors.axaml` und `index.css` gerechnet, nicht am Bild gemessen. Was nicht geprüft werden konnte, steht am Ende jedes Teils mit dem Mittel, es nachzuholen.

**Umgesetzte Befunde tragen in der Spalte „Einstufung“ den Zusatz „erledigt in Slice NN“** (Slices der Rundenakten unter `work/rounds/`); alles ohne Zusatz ist offen. Einstufung je Befund: *verstößt* (gegen Brief oder Skill-Regel, beheben), *weicht ab, nicht dokumentiert* (der Auftraggeber entscheidet: beheben oder als bewusste Abweichung im Brief festhalten), *weicht ab, dokumentiert* (in Ordnung), *bekannt* (steht schon in [stand-und-offene-punkte.md](../stand-und-offene-punkte.md) oder im Brief 11.4). Aufwand: S unter einer Stunde, M ein halber Tag, L mehr. Nummern: W für die Windows-App, B für das Web; die Muster-Zeilen bündeln Einzelbefunde.

| Oberfläche | Befunde | verstößt | weicht ab, nicht dokumentiert | weicht ab, dokumentiert | bekannt |
| --- | --- | --- | --- | --- | --- |
| Windows-App (W1 bis W68 und 13 Muster) | 81 | 53 | 18 | 6 | 4 |
| Web: Verwaltung und Kundenbereich (B1 bis B70) | 68 | 25 | 39 | 0 | 4 |

Vorgeschlagene Reihenfolge der Umsetzung (Blöcke, je ein PR auf `dev`): zuerst die Muster mit vielen Stellen und kleinem Aufwand je Stelle (W-Muster 1 Feldnamen, W-Muster 2 und 3 Meldungen, B4 `th scope`, B5 und B6 Bewegung und Hochkontrast), dann die Verstöße mit Datenrisiko (B37 Geldeingabe, B1 Anfangsfokus in Rückfragen, B47 Verwerfen ohne Rückfrage), dann Screenreader-Zustände (W50 Terminkarten, W-Muster 5 bis 7, B3 Reiter, B12 bis B14 Shell), zuletzt die Abweichungen, über die der Auftraggeber entscheidet.

## Teil A: Windows-App

## Prüfung der Windows-App gegen Fluent 2 (Skill fluent2-avalonia) und den Design-Brief

Stand: Branch `dev`, 23.10.2026. Nur gelesen, nichts geändert, nichts gebaut. Geprüft: alle Dateien unter `src/AispaStudio.Desktop/Views/` (21 Seiten und Bausteine), `Themes/Colors.axaml`, `Themes/Styles.axaml` (Stichproben nach Befund), `Controls/` (AccessibleNames, FormField, InfoBar, DisabledHint, HelpButton, TabStripBehavior, FieldBehaviors, ToolTipBehavior), `Views/ConfirmDialogView.axaml.cs`, `Views/CustomersView.axaml.cs`, Tastaturlogik im Kalender (`CalendarViewModel.Keyboard.cs`) und der Namens-Scan-Test (`tests/AispaStudio.Desktop.Tests/ViewTests.FocusOrder.cs`).

Maßstab: Skill-Checklisten, `docs/design.md` (Brief; dort festgelegte Abweichungen von Fluent sind keine Befunde), `docs/gestaltung/masken.md`, `meldungen.md`, `fluent2-ableitung.md` und die offenen Punkte in `docs/stand-und-offene-punkte.md` und Brief 5.28/9.

Zahlen zu Feldern stammen aus einem Skript, das alle AXAML-Dateien parst (Felder = TextBox, ComboBox, AutoCompleteBox, DateField, TimeField, NumberField, MoneyField, SearchField). Kontrastzahlen aus `Themes/Colors.axaml` mit der WCAG-Formel (`scratchpad/contrast.py`).

### Zusammenfassung

**81 Befunde** (68 seitenbezogene W1 bis W68 und 13 W-Muster). Nach Einstufung: **verstößt 53**, **weicht ab, nicht dokumentiert 18**, **weicht ab, dokumentiert 6**, **bekannt 4**. Aufwand: S 56, M 20, L 5. Die Muster bündeln Befunde, die in mehreren Dateien vorkommen; die seitenbezogenen Zeilen verweisen darauf und zählen deshalb teilweise doppelt.

#### Die fünf wichtigsten Punkte

1. **Terminkarten sind für Screenreader nicht vorhanden (W50).** `Border.appt` hat weder Fokus noch Namen noch Rolle, und die Pfeiltasten-Auswahl im Kalender wird nirgends angesagt; die Kernfunktion der App ist so nicht ohne Maus nutzbar.
2. **94 von 111 Feldern ohne `FormField` haben keinen echten Namen, 26 gar keinen (Muster 1).** Beschriftung steht nur als Platzhalter, und `AccessibleNames` macht den Beispieltext zum Feldnamen. Betroffen sind Anmeldung, Einrichtung, Kundenwünsche, Kasse, Posteingang, Kalender-Buchung.
3. **Meldungen als `Border.alert` (35 Stellen) tragen kein Symbol, keine Aktion, keine Live-Region (Muster 2).** Folgen: die Warnung in BackupView steht in roter Schrift (W10), der Fehler im Posteingang erscheint ohne Rand und Symbol (W33), die Leerlauf-Abmeldung wird nicht angesagt (W18).
4. **Zustände fehlen für Screenreader (Muster 5 bis 7).** Fünf Reiterschienen ohne „gewählt“, vier Aufklapp-Köpfe ohne „aufgeklappt“, Knopfnamen wie „12“ oder „09:30“ statt der Bezeichnung, Listenzeilen mit Record-Text als Namen, den der Namens-Scan nicht erkennt.
5. **Masken im Altbestand bedienen sich nicht wie die neuen (Muster 8, 4, 11).** In den Kundenkarten, Geräten, Sicherung gibt es kein Esc, kein Enter-Speichern, keinen Startfokus; im Assistenten stehen bis zu drei Primärknöpfe gleichzeitig; die Anmeldung duzt („du“), obwohl der Brief „nie Du“ festlegt.

#### Seitenübergreifende Muster (W-Muster)

| Nr. | Muster | Umfang | Regel | Vorschlag | Einstufung | Aufwand |
| --- | --- | --- | --- | --- | --- | --- |
| W-Muster 1 | Feld ohne `FormField`: Beschriftung nur als Platzhalter oder als unverbundener `TextBlock.label`; `AccessibleNames.cs:26-27` setzt den Platzhalter als Namen. Von 111 Feldern ohne `FormField` haben 94 keinen eigenen Namen (Platzhalter 68, gar nichts 26). Verteilung ohne Namen: Calendar 21, Customers 25, Login 18, Pos 13, Inbox 4, Companion 2, Join 2, NetworkConnect 2, Restore 2, Sync 2, Backup 1, Help 1, Shell 1. Ganz ohne Namen: Calendar 13 ComboBoxen (:259, :584, :910, :920, :931, :938, :945, :1252, :1431, :1456, :1591, :1679, :1906), Customers 8 (:71, :75, :79, :101, :105, :345, :430, :435), Login 3 (:86, :151, :239), Inbox :235, Shell :91 | 14 Dateien | inputs.md §1 (Field anatomy), accessibility.md §7; Brief 5.2, 9, 10 („Platzhalter als Beschriftung“, „Felder ohne Beschriftung“); masken R6 | Jedes Feld in `ui:FormField Label=…` setzen (bindet Name, Hilfe, Pflicht, Fehler); Platzhalter nur als „z. B.“-Beispiel; `AccessibleNames.NameFromPlaceholder` danach entfernen, damit fehlende Beschriftungen im Scan auffallen | verstößt – erledigt in Slice 06, 07, 08, 09, 10 | L |
| W-Muster 2 | `Border.alert` (34 Zeilen in 14 Dateien) als Meldung: kein Symbol (Farbe als einziger Träger), keine Aktion, kein Schließen, keine Live-Region (nur CalendarView :1033 hat `LiveSetting`); Warnungen werden mit lokalem `Foreground`/`Background` umgefärbt, wodurch `Styles.axaml:106-109` rote Schrift in jede Warnung zieht, wo das lokale `Foreground` fehlt (W10) | Admin, Backup, Calendar (8), Customers (5), Devices, Inbox, Join, Login (4), MainWindow, NetworkConnect, Pos, Restore, Shell (3), Sync | dialogs-and-messages.md §5 (Message bar: Symbol, Aktion bei Warnung/Fehler, Live-Region); Brief 5.9, 5.15, 10 („Rote Warnungen“); meldungen.md M1 bis M3 | `Border.alert` durch `ui:InfoBar` ersetzen (Severity, Aktion „Erneut versuchen“, Schließen); danach Stil `Border.alert` entfernen | verstößt – erledigt in Slice 03, 04, 05 | M |
| W-Muster 3 | Statusmeldungen als einfacher Text ohne Live-Region: Backup :45 `Message`, Sync :94 `Message`, Devices :38 `PrinterText`, :92 `TerminalText`, :100 `SavedText`, Network :34 `Message`, Pos :248/:498 `PrintText`, :443 `ChangeText` | 6 Dateien | accessibility.md §6 (Messages: polite/assertive); dialogs-and-messages.md §6 (Toast statt Zeilentext) | Kurzbestätigungen als Toast (`CalendarViewModel.Toast` gibt es), Zustände als InfoBar; mindestens `AutomationProperties.LiveSetting="Polite"` am Text | verstößt – erledigt in Slice 03, 04, 05 | S |
| W-Muster 4 | Mehrere Primärknöpfe in einer Fläche: Backup :25 und :60; Sync :77 und :84; Devices :99 und :156; Companion :50, :186, :197 (bis zu drei in einer Seitenleiste); Termin-Panel Calendar :61, :125, :193, :228, :304/:311, :348 | 5 Dateien | buttons-and-selection.md (One primary per layout); Brief 5.1, 5.12 Punkt 6, 10 („zwei primäre Knöpfe“) | Je Karte/Seitenleiste genau einen Primärknopf; „Notiert“ und „Speichern und sichern“ nicht gleichzeitig zeigen; im Assistenten „Termin buchen“ und Hilfe-Aktion als Sekundär | verstößt – erledigt in Slice 04, 10, 15, 21 | S |
| W-Muster 5 | Auswahl und Aufklappen ohne Zustand für den Screenreader: fünf Reiterschienen als `Button` mit Klasse `on` (Pos :18-22 und :635-638, Calendar :1228 „Neuer Eintrag“, :2046/:2054 Team/Räume, Seitenleisten-Reiter :2872) melden nicht „gewählt“; vier `ToggleButton.more` melden nur „checked“, nicht „aufgeklappt“ (AiFooter :16, Calendar :354 und :1086, CustomerContactForm :101) | 5 Dateien | navigation.md §1 (aktiver Reiter als „current“), accessibility.md §6 (Expandable headers); Brief 5.28 („Zustand für den Screenreader“), 5.29 | Reiterschiene: `RadioButton`/`ListBoxItem` mit Selection-Peer oder `SelectionItem`-Peer für `Button.on`; Aufklapp-Köpfe auf `AccordionHeader` (hat den Expand-Peer) umstellen | verstößt – erledigt in Slice 19 | M |
| W-Muster 6 | Knopfname aus dem ersten Text im Inhalt (`AccessibleNames.cs:46-56`) liefert Zahl oder Uhrzeit statt Bezeichnung: Pos :58 (Uhrzeit, ohne Gast), Calendar :208 (Teil der Buchung, Uhrzeit), :2128-2155 (Kennzahlen „12“ statt „Termine, 12“), :2193 (Als Nächstes, Uhrzeit); dazu gleichlautende Zeilenknöpfe ohne Bezug: Sync :54 und Shell :134 „Erledigt“, Pos :286/:290, Calendar :1019 „Baustein entfernen“, :1395 „Gast entfernen“, :1421, :1608/:1612 „Weniger“/„Mehr“, Customers :227 | 4 Dateien | accessibility.md §7 (Buttons with non-text content, list rows) | Bei Knöpfen mit mehreren Texten `AutomationProperties.Name` binden („{Label}, {Wert}“; „Erledigt: {Zeile}“) | verstößt – erledigt in Slice 20 | M |
| W-Muster 7 | Listenzeilen ohne Name: ListBox-Zeilen sind Records ohne `ToString` (`CustomerDto`, `FreeSlotRow`, `ProposalRow`, `GroupSlotOption`, `InboxRow`, `HelpListEntry`, `ReceiptListItem`); nur die Navigation (ShellView :14-16) setzt den Namen per Stil. Der Namens-Scan (`ViewTests.FocusOrder.cs:213-214`) erkennt nur Namen, die mit „Avalonia.“ oder „AispaStudio.“ beginnen; ein Record-Text („CustomerDto { Id = … }“) rutscht durch | 10 ListBoxen (Customers :38, Calendar :956, :1119, :1280, :1365, :1477, Companion :170, Help :16, Inbox :39, Pos :460) | accessibility.md §7 (List rows, Scan) | Stil `ListBoxItem` mit `AutomationProperties.Name` an die Anzeige-Eigenschaft binden (wie ShellView); Scan um „enthält `{`“ und „endet mit `}`“ erweitern und mit gefüllten Listen laufen lassen | verstößt – erledigt in Slice 11 | M |
| W-Muster 8 | Masken im Altbestand ohne Tastaturweg: Customers (Kennzeichen :343, Wünsche :369, Rezeptionsvermerk :400, Termine am liebsten :428, Rechnungsempfänger :477, Rabatt :498, „Neue Kundin“ :59) ohne Esc zum Schließen, ohne Enter auf den Speichern-Knopf (kein `IsDefault`), ohne Startfokus (`CustomersView.axaml.cs:30-38` fokussiert nur die Kontaktmaske); ebenso Devices, Backup, Sync, Join, Restore, Pos (Rechnungsempfänger :219, Rabatt :328) | 8 Dateien | inputs.md §3 (Behavior matrix), §4; Brief 5.16 (Esc-Stufen, Enter), masken R13 | `IsDefault` am Speichern-Knopf, Esc-Behandlung und Startfokus in einem Verhalten (`MaskBehavior`) für alle Masken; sonst mit Umbau nach masken.md §8 | weicht ab, dokumentiert (masken.md §8: Altbestand „nur bei Änderung“) | L |
| W-Muster 9 | Maße außerhalb des Brief-Rasters: Kartenrand `Padding="18"` (Backup :29/:50, Devices :15/:43/:104, Network :15, Pos :378/:617/:632, Sync :16/:34/:64), `22` (Admin :11, Customers :151), `14` (Customers :248-281, Inbox :122/:227, Pos :101/:132, Login :219/:228, MainWindow :24), `28` (MainWindow :33); `Spacing` 14 (Calendar :1227, Companion :12, Inbox :74, MainWindow :35, Pos :193/:483), 18 (Login :46/:189), 28 (Calendar :2715); Höhen `MinHeight` 40/42/44/46/52 (Login 23 Stellen, Join :17-18, Restore :18/:21, Calendar :1344/:1510/:1620, Pos :445/:629); Radius 6 an Hinweisen (Calendar :1192/:1761/:1845, Customers :538) und InfoBar (`Styles.axaml:1360`) | 11 Dateien | foundations.md (4-px-Raster); Brief 4.1 („Kein anderer Wert; 28, 36, 40 und 48 sind nicht aufgenommen“), 4.2 (Hinweis 8), 4.4 | Werte auf 16/20 (Karte), 12, 16, 24 und Höhe 34 zurückführen oder die Ausnahme (Anmeldung) im Brief 4.4 eintragen; keine Prüfung dafür im Test (DesignTokenTests prüft nur Farben und Schriftgrößen) | weicht ab, nicht dokumentiert – erledigt in Slice 32 | M |
| W-Muster 10 | Richtungswörter im Text: Customers :140 „Links eine Kundin …“, Inbox :68 „Nachricht links auswählen.“, :212 „Hinweis oben“, `InboxViewModel.cs:587` „oben klären“, Login :64 „unten links“, :196 „Oben … wählen“, Pos :188 „Links einen Termin …“; `AiLabels.cs:83` „steht unten“ (vom Brief 5.28 vorgegeben) | 5 Dateien | content-design.md §3 (Words to avoid: Richtung), §5 | Ohne Richtung formulieren („Eine Kundin oder einen Kunden aus der Liste wählen.“) | verstößt (AiLabels: weicht ab, dokumentiert) – erledigt in Slice 28 | S |
| W-Muster 11 | Anrede „du“: Login :60 „Wer bist du?“, :153 „Dein Name“, :170 „Du hast …“, Shell :97 „wechselst du … deinem“ | 2 Dateien | Brief 1 („unpersönlich oder „Sie“, nie „Du““); content-design.md §1 | In „Sie“ oder unpersönlich umschreiben; ViewModel-Texte (`LoginViewModel`) gleich mitprüfen | verstößt – erledigt in Slice 06 | S |
| W-Muster 12 | Information nur im Tooltip auf nicht fokussierbarem Text: Customers :166 Sterne, :173 Adresszeile, :175 Anrede-Zeile; Calendar :682-697 Kennzahlen im Kunden-Abschnitt, :711 gekürzte Kundennotiz (volle Notiz nur im Tooltip), :2412-2415 Streifen; ein Deaktivierungsgrund (`DisabledHint`) wirkt nur mit der Maus, weil ein deaktivierter Knopf keinen Fokus nimmt | 3 Dateien | navigation.md §6 (Tooltip ergänzt, ersetzt nichts); Brief 5.22 | Gekürzte Texte per Hover-Karte oder Aufklappen auch mit Tastatur erreichbar machen; Deaktivierungsgrund zusätzlich als `AutomationProperties.HelpText` | weicht ab, nicht dokumentiert – erledigt in Slice 33 (Kundennotiz im Abschnitt „Kunde“ offen) | M |
| W-Muster 13 | Knopfbeschriftung ohne Verb: Pos :144 „Wertgutschein“, :157 „Behandlung“, :171 „Paket“; ConfirmDialog :92-94 „Freundlicher (KI)“, „Förmlicher (KI)“; Inbox :246-250 „freundlicher“, „kürzer“, „du“, „Entschuldigung ergänzen“ (klein geschrieben); Backup :25 und Sync :84 „Notiert“ | 4 Dateien | content-design.md §4 (Button labels); Brief 1, 3 („Knöpfe tragen ein Verb im Infinitiv“) | „Wertgutschein verkaufen“, „Paket verkaufen“, „Freundlicher formulieren (KI)“, „Duzen (KI)“, „Verstanden“ | verstößt – erledigt in Slice 04, 09, 10, 28 | S |

### Befunde je Seite

#### Views/LoginView.axaml

| Nr. | Befund | Regel | Vorschlag | Einstufung | Aufwand |
| --- | --- | --- | --- | --- | --- |
| W1 | 18 Felder ohne `FormField`, Beschriftung nur als Platzhalter (:104-105 Benutzername/Passwort, :119-122 Wiederherstellung, :150-156 Einrichtung mit 7 Feldern, :172-173); als Name liest der Screenreader z. B. „Wiederherstellungsschlüssel (24 Zeichen, z. B. K7QM-2XRT-…)“; keine Pflichtkennzeichnung (`IsRequiredForForm`) | inputs.md §1; Brief 5.2, 10 | `FormField` mit Label und Hilfetext („mindestens 8 Zeichen“ als Hilfe statt im Platzhalter) | verstößt – erledigt in Slice 06 | M |
| W2 | PIN-Feld :86, Land :151 und Testkonto :239 ohne jeden Namen (Titel :85 ist ein unverbundener `TextBlock`); der Namens-Scan läuft erst nach der Anmeldung (`ViewTests.FocusOrder.cs:217-241` durchsucht nur `shell.Pages`) und sieht die Anmeldeseite nicht | accessibility.md §7 | `AutomationProperties.Name` oder `FormField`; Scan um die Anmeldeseite erweitern | verstößt – erledigt in Slice 06 | S |
| W3 | Mischung aus Eingabehöhen 40, 42, 44, 52 und Radius/Abständen außerhalb des Rasters (:87-89, :104-107, :119-124, :150-158, :172-175, :203-205; `Padding="32"`, `Spacing="18"`); der Brief nimmt die Anmeldung nur für die Schrift (34/24) aus | Brief 4.1, 4.4 | Höhe 34 oder Ausnahme in Brief 4.4 eintragen → Muster 9 | weicht ab, nicht dokumentiert – erledigt in Slice 32 | S |
| W4 | `IsDefault="True"` fest an drei verdeckten Formularen (:123, :157, :174), dazu zwei bedingte (:88, :106, :204); der Brief kennt dazu den Fallstrick „mehrere sichtbare Standardknöpfe feuern alle“ (pitfalls.md) | Brief 5.16 (Enter) | Bedingung je Formular wie bei :88 oder Test „genau ein sichtbarer Standardknopf“ | weicht ab, nicht dokumentiert – erledigt in Slice 34 | S |
| W5 | Fehlertext der Anmeldung als `Border.alert` ohne Symbol und Live-Region (:91, :137, :178, :206) → Muster 2 | dialogs-and-messages.md §5 | InfoBar | verstößt – erledigt in Slice 03 | S |

#### Views/JoinView.axaml, RestoreView.axaml, NetworkConnectView.axaml, NetworkView.axaml

| Nr. | Befund | Regel | Vorschlag | Einstufung | Aufwand |
| --- | --- | --- | --- | --- | --- |
| W6 | Join :17-18, Restore :18/:21, NetworkConnect :14-15: Eingaben nur mit Beispiel-Platzhalter („Host=…;Database=…;Username=…;Password=…“), der zum Feldnamen wird; Verbindungszeichenfolge mit Passwort, Wiederherstellungsschlüssel und Code ohne Beschriftung → Muster 1 | inputs.md §1; Brief 5.2 | `FormField` mit Label „Verbindung zur zentralen Datenbank“, „Schlüsselcode“, „Adresse“, „Verbindungscode“ und kurzem Hilfetext | verstößt – erledigt in Slice 06 | S |
| W7 | NetworkConnectView :20 „Verbinden“ ist der einzige Knopf der Maske, aber sekundär (kein `primary`); Join :19 und Restore :22 sind primär | Brief 5.1 (eine primäre Aktion je Maske); masken R5 | `Classes="primary"` | verstößt – erledigt in Slice 06 | S |
| W8 | Technikdeutsch im Fließtext: NetworkView :28 „App ohne „--standalone“ starten“, :11 lange Absätze; Sync :70 „PostgreSQL … SSL Mode=Require“, Devices :22 „ESC/POS … Port 9100“, :50 „ZVT oder OPI“ | content-design.md §2/§5 (kein technischer Text im Hauptteil, Details aufklappbar) | Kurzsatz plus „Technische Angaben“ als Aufklapp-Abschnitt oder Hilfe-Link | weicht ab, nicht dokumentiert – erledigt in Slice 35 | S |
| W9 | NetworkView :34 und Restore :23 Rückmeldung als Zeilentext ohne Live-Region → Muster 3 | accessibility.md §6 | Toast oder `LiveSetting` | verstößt – erledigt in Slice 03 | S |

#### Views/BackupView.axaml

| Nr. | Befund | Regel | Vorschlag | Einstufung | Aufwand |
| --- | --- | --- | --- | --- | --- |
| W10 | Warnung „Wiederherstellungsschlüssel notieren“ (:16-27): `Border.alert` mit Warn-Hintergrund, aber `Styles.axaml:106-109` färbt jeden `TextBlock` darin in `CritBrush`; hier fehlt das lokale `Foreground` (SyncView :27 hat es) → Titel und Text erscheinen **rot** auf warn-soft (Kontrast 5,5:1 hell, also lesbar, aber „Rote Warnungen“ sind verboten) | Brief 5.9, 10 | Warnung als `InfoBar Severity="Warning"` mit Aktion „Notiert“ | verstößt – erledigt in Slice 04 | S |
| W11 | Primärknöpfe „Notiert“ (:25) und „Speichern und sichern“ (:60) gleichzeitig sichtbar → Muster 4 | Brief 5.1 | „Notiert“ als Sekundär | verstößt – erledigt in Slice 04 | S |
| W12 | :59 Ordner nur mit Platzhalter; kein „Ordner wählen“-Knopf (Restore hat `PickFile`), „Speichern und sichern“ koppelt zwei Aktionen | inputs.md §1; Brief 5.2 | `FormField` „Sicherungsordner“, Knopf „Ordner wählen“, Speichern und Sichern getrennt | weicht ab, dokumentiert (masken.md §8) | S |

#### Views/SyncView.axaml

| Nr. | Befund | Regel | Vorschlag | Einstufung | Aufwand |
| --- | --- | --- | --- | --- | --- |
| W13 | :26-28 und :79-86 Warnungen als `Border.alert`; :72 und :76 Felder nur mit Platzhalter; zwei Primärknöpfe :77 und :84 → Muster 1, 2, 4 | siehe Muster | InfoBar, `FormField`, „Notiert“ als Sekundär | verstößt – erledigt in Slice 04 | M |
| W14 | :54 „Erledigt“ je abgelehnter Zeile ohne Bezug zur Zeile → Muster 6 | accessibility.md §7; content-design.md §5 | `AutomationProperties.Name="Erledigt: {Area} {When}"` | verstößt – erledigt in Slice 20 | S |

#### Views/DevicesView.axaml

| Nr. | Befund | Regel | Vorschlag | Einstufung | Aufwand |
| --- | --- | --- | --- | --- | --- |
| W15 | Beschriftungen als `TextBlock.label` (:24, :26, :68, :135-137) über Feldern, die zusätzlich einen eigenen `AutomationProperties.Name` tragen: Name und Beschriftung werden doppelt gepflegt, Hilfe und Fehler hängen nicht am Feld | inputs.md §1; Brief 5.2, 5.16 | `FormField` | weicht ab, dokumentiert (masken.md §8) | M |
| W16 | Zwei Primärknöpfe auf der Seite: „Speichern“ (:99) und „Diesen PC als Empfangsgerät anmelden“ (:156) → Muster 4; „Speichern“ in einem eigenen Block außerhalb der Karten, ohne `IsDefault` | Brief 5.1 | zweiten Knopf sekundär | verstößt – erledigt in Slice 04 | S |
| W17 | :59-62 Warnung als `Border.alert`, :165-167 Fehler ebenso; Statuszeilen :38, :92, :100 ohne Live-Region → Muster 2, 3 | siehe Muster | InfoBar, Toast | verstößt – erledigt in Slice 04 | S |

#### Views/MainWindow.axaml

| Nr. | Befund | Regel | Vorschlag | Einstufung | Aufwand |
| --- | --- | --- | --- | --- | --- |
| W18 | :24-30 Leerlauf-Warnung („Sie werden gleich abgemeldet“) als `Border.alert`: kein Symbol, **keine Live-Region**; eine zeitkritische Ansage fehlt für Screenreader | accessibility.md §6 (assertive); Brief 5.15 | `InfoBar Severity="Warning"` mit „Weiter arbeiten“ als Aktion (assertive) | verstößt – erledigt in Slice 03 | S |
| W19 | :33-53 Verbindungsfehler: Aufbau mit Karte, Symbol 24 in Warnfarbe, Titel `h1`, aber der Zustand (`StatusText`) wird nicht angesagt (keine `LiveSetting` in der Datei) | dialogs-and-messages.md §5; accessibility.md §3 | `LiveSetting="Assertive"` am Statustext | weicht ab, nicht dokumentiert – erledigt in Slice 34 | S |

#### Views/ShellView.axaml (Navigation, Banner, Leiste „KI und Hilfe“)

| Nr. | Befund | Regel | Vorschlag | Einstufung | Aufwand |
| --- | --- | --- | --- | --- | --- |
| W20 | Konto-Knopf (:79-83) und Farbschema-Knopf (:58-68): Namen aus dem ersten Text, also „SJ“ (Initialen) bzw. „Hell“; Zähler-Badge der Navigation (:45-48) fehlt im Namen der Zeile (`ListBox.nav`-Stil setzt `Name = Title`, :14-16) | accessibility.md §7 | `AutomationProperties.Name="Konto {UserText}"`, „Farbschema: {ThemeText}“, Name der Zeile „{Title}, {Badge} offen“ | verstößt – erledigt in Slice 20 | S |
| W21 | PIN-Feld im Konto-Flyout (:91): kein Name, `TextBlock.label` :89 unverbunden; :97 duzt („wechselst du … deinem Zugang“) → Muster 1, 11 | inputs.md §1; Brief 1 | `FormField` „PIN“ mit Hilfetext, Satz in „Sie“ | verstößt – erledigt in Slice 06 | S |
| W22 | Drei Banner (Offline :117, Abgleich :122, Sicherung :143) als `Border.alert`: Offline- und Sicherungsbanner haben **keine Aktion** (Warnung braucht Knopf oder Link), kein Schließen, keine Live-Region; `Failed` listet je Zeile „Erledigt“ ohne Bezug; alle drei können gleichzeitig stehen (Brief: höchstens zwei, danach „n weitere“) | dialogs-and-messages.md §5; Brief 5.15 | `InfoBar`, Aktion („Sicherung öffnen“, „Verbindung prüfen“), Stapel auf zwei begrenzen | verstößt – erledigt in Slice 03 | M |
| W23 | Leiste „KI und Hilfe“ (:163) und im Kalender (CalendarView :2778) als schwebende Karte (Radius 12, Rahmen ringsum, Schatten `CardShadow` aus `Border.card`, Rand 16) statt „Rahmen links 1 px line, kein Schatten“; Kopfrand 18/16/10/12 und Inhalt 18/14/18/18 statt 16/20 | Brief 5.12 Punkt 1, 2 | Schatten und Rand im Stil `Border.sidepanel` zurücknehmen oder Abweichung im Brief 5.12 eintragen | weicht ab, nicht dokumentiert – erledigt in Slice 32 | S |

#### Views/ConfirmDialogView.axaml (und .axaml.cs)

| Nr. | Befund | Regel | Vorschlag | Einstufung | Aufwand |
| --- | --- | --- | --- | --- | --- |
| W24 | Anfangsfokus geht immer auf einen Knopf (`ConfirmDialogView.axaml.cs:76-79`), auch wenn der Dialog ein Eingabefeld hat (`HasInput`, axaml :122-124, z. B. Grund beim Storno) | dialogs-and-messages.md §2 (Focus: „A dialog with input fields focuses the first field“); inputs.md §3 | Bei `HasInput` zuerst `DialogInput` fokussieren; bei Zerstörung danach Abbrechen | verstößt – erledigt in Slice 28 | S |

#### Views/ConflictNoticeView.axaml

| Nr. | Befund | Regel | Vorschlag | Einstufung | Aufwand |
| --- | --- | --- | --- | --- | --- |
| W25 | Eigener Border `infobar crit` statt `ui:InfoBar`: die Handlung steht als `TextBlock` (:36), nicht als Knopf; kein Schließen-Kreuz; Fehler braucht Knopf oder Link | dialogs-and-messages.md §5; Brief 5.15 | `InfoBar` mit Aktion; Konfliktzeilen als Inhalt belassen | weicht ab, nicht dokumentiert – erledigt in Slice 34 | S |

#### Views/AiFooter.axaml, AiPill.axaml

| Nr. | Befund | Regel | Vorschlag | Einstufung | Aufwand |
| --- | --- | --- | --- | --- | --- |
| W26 | „Warum dieser Vorschlag?“ (AiFooter :16-29) ist ein `ToggleButton` ohne „aufgeklappt“ für den Screenreader; Brief 5.28 verlangt genau das → Muster 5 | Brief 5.28, 5.29; accessibility.md §6 | `AccordionHeader` oder Expand-Peer | verstößt – erledigt in Slice 19 | S |

#### Views/HelpView.axaml

| Nr. | Befund | Regel | Vorschlag | Einstufung | Aufwand |
| --- | --- | --- | --- | --- | --- |
| W27 | Suche (:12) als einfache `TextBox` mit Platzhalter statt `ui:SearchField` (Lupe, Löschen-Kreuz, Esc, Trefferliste); Zahl der Treffer und „Nichts gefunden“ (:14) ohne Live-Region; Zeilen (:16) sind `HelpListEntry`-Records → Muster 1, 7 | inputs.md §7 (Search field); Brief 5.16 Suchfeld | `SearchField` verwenden, Trefferzahl polite ansagen | weicht ab, nicht dokumentiert – erledigt in Slice 35 | S |

#### Views/CompanionContent.axaml (KI und Hilfe)

| Nr. | Befund | Regel | Vorschlag | Einstufung | Aufwand |
| --- | --- | --- | --- | --- | --- |
| W28 | Bis zu drei Primärknöpfe gleichzeitig: „Fragen“ (:50), „Termin buchen“ (:186), Hilfe-Aktion (:197) → Muster 4 | Brief 5.12 Punkt 6 | „Fragen“ primär, die beiden anderen sekundär | verstößt – erledigt in Slice 10 | S |
| W29 | Die Antwort des Assistenten (:77-211) erscheint ohne Live-Region; nur Fehler (:54) und „Kopiert“ (:130) sind angesagt; Screenreader-Nutzer erfahren nicht, dass eine Antwort da ist | accessibility.md §6; responsible-ai.md §3 | `AutomationProperties.LiveSetting="Polite"` an der Antwortkarte (nach dem Laden) | verstößt – erledigt in Slice 10 | S |
| W30 | „Für wen?“ (:162) als `TextBlock.label` ohne Verbindung, Gastsuche nur mit Platzhalter (:168); `BookedText` (:191) nur in `OkBrush` ohne Symbol → Muster 1 | Brief 9 (Farbe nie einziger Träger) | `FormField`, Haken-Symbol vor dem Text | verstößt – erledigt in Slice 10 | S |
| W31 | Ausschalter je KI-Funktion, Erstöffnungskarte „Was der Assistent kann“, Protokoll „KI-Aktivität“ und Kennzeichen für von der KI angenommene Termine fehlen | responsible-ai.md §1, §3; Brief 5.28 („Offen“) | wie im Brief geplant | bekannt | L |

#### Views/InboxView.axaml (Posteingang)

| Nr. | Befund | Regel | Vorschlag | Einstufung | Aufwand |
| --- | --- | --- | --- | --- | --- |
| W32 | Antworttext (:235) ohne jeden Namen (Beschriftung `ReplyLabel` :233 unverbunden); Betreff (:234), Wunsch (:219), Hinweis (:254) nur Platzhalter → Muster 1 | inputs.md §1; accessibility.md §7 | `FormField Label="{Binding ReplyLabel}"` | verstößt – erledigt in Slice 10 | S |
| W33 | Meldung `Notice` (:287-290): lokale Werte `Background=AccentSoft` und `BorderThickness=0` schlagen den Stil `Border.alert`; bei `NoticeIsError` bleibt nur rote Schrift auf Akzentfläche, ohne Rand und Symbol, ohne Live-Region (Kontrast 5,3:1 hell / 4,8:1 dunkel, also lesbar) | Brief 5.9, 5.15; Skill avalonia-mapping.md (Wertvorrang) | `InfoBar` mit Severity nach `NoticeIsError` | verstößt – erledigt in Slice 05 | S |
| W34 | KI-Zusammenfassung steht in der Listenzeile (:54, `InboxRow.Summary = Mail.Summary`) ohne KI-Kennzeichen; das Detail (:95-98) trägt Pille, Hinweis und Rückmeldung | responsible-ai.md §3 (jede KI-Ausgabe gekennzeichnet); Brief 5.28 | `AiPill` vor der Zusammenfassung in der Zeile oder Zusammenfassung dort weglassen | verstößt – erledigt in Slice 10 | S |
| W35 | Revise-Knöpfe (:246-250) und :249 „du“: klein geschrieben, keine Verben, „du“ unklar (Duzen des Gastes) → Muster 13 | content-design.md §4 | „Freundlicher formulieren“, „Kürzer formulieren“, „Duzen“ | verstößt – erledigt in Slice 10 | S |

#### Views/CustomersView.axaml (Kunden)

| Nr. | Befund | Regel | Vorschlag | Einstufung | Aufwand |
| --- | --- | --- | --- | --- | --- |
| W36 | Maske „Neue Kundin / neuer Kunde“ (:59-136): kein `FormField`, `TextBlock.label` unverbunden (:68-109), Pflicht nicht gekennzeichnet (Satz :63 „Nur ein Name ist Pflicht“ statt Stern), drei Fragezeichen an Einzelfeldern (:69, :86, :93), keine Gruppen, Titel als Freitext, dreimal anderes Raster (`130,*,*`, `UniformGrid 2`, voll), „Abbrechen“ sekundär statt Ghost (:134); dieselben Felder stehen zweimal (CustomerContactForm, Calendar :1312-1316) | masken R1, R3, R4, R5, R7, R9; Brief 5.14 | Anlage mit `CustomerContactForm` (Modus „neu“) statt einer eigenen Maske | bekannt (masken.md §8: „Neuer Eintrag“ steht als nächste Maske) | M |
| W37 | Kundenwünsche und Rechnungsempfänger: neun Felder nur mit Platzhalter als Beschriftung (:371-378, :478-480), ComboBoxen ohne Name (:345, :430, :435), „Wünscht“ links neben dem Feld (:434) | Brief 10 (Beschriftungen neben dem Feld, Platzhalter als Beschriftung); masken R6 | `FormField` mit Label | verstößt – erledigt in Slice 08 | M |
| W38 | Karten mit eigenem Bearbeiten-Zustand (6 Stück, :343-529): jede mit „Speichern“ als Primärknopf, mehrere können gleichzeitig offen sein; kein Esc, kein Enter, kein Startfokus → Muster 8, 4 | masken R5, R13 | nur eine Karte gleichzeitig bearbeitbar; Verhalten wie `CustomerContactForm` | verstößt – erledigt in Slice 15, 16 | L |
| W39 | Warnzeilen der Kennzahlen (`IsWarning`, :305-309) in `CritBrush`: „Warnungen sind nie rot“; die Farbe ist der einzige Unterschied zu normalen Zeilen | Brief 5.9, 9, 10 | `WarnBrush` plus Warnsymbol | verstößt – erledigt in Slice 08 | S |
| W40 | Tooltip-only-Hilfe auf Sternen, Adresszeile, Anrede-Zeile (:166, :173, :175) und `Button.icon` „Adresse entfernen“ je Zeile (:227) ohne Zeilenbezug → Muster 12, 6; Trefferliste :38 mit `CustomerDto` → Muster 7 | navigation.md §6; accessibility.md §7 | Hilfetext sichtbar oder Name mit Bezug | weicht ab, nicht dokumentiert – erledigt in Slice 20, 33 (Trefferliste über W-Muster 7) | S |

#### Views/CustomerContactForm.axaml (Kundendaten-Maske, das Vorbild)

| Nr. | Befund | Regel | Vorschlag | Einstufung | Aufwand |
| --- | --- | --- | --- | --- | --- |
| W41 | Abstände Feld zu Feld 16, Gruppe zu Gruppe 32, Überschrift zu Feld 12 (:11, :15, :18, :23) statt 12 / 20 / 8 (Brief 4.1, masken.md §4); der Kommentar :10-11 nennt die Werte absichtlich | Brief 4.1; masken §4 | Werte angleichen oder Brief 4.1 und masken §4 auf 16/32/12 ändern | weicht ab, nicht dokumentiert – erledigt in Slice 32 | S |
| W42 | Gruppenüberschriften als `h2` (18 px, :20, :49, :74, :105) statt `h3` (16 px) wie masken R1 und Brief 5.14; masken §7.2 schreibt selbst „h2“ | masken R1 gegen §7.2 | Widerspruch in masken.md auflösen (R1 oder §7.2) | weicht ab, dokumentiert | S |
| W43 | Speichern (:155) wird per `CanSave` deaktiviert, ohne `DisabledHint`; Brief 5 verlangt den Grund im Tooltip („Verfügbar, sobald ein Name eingetragen ist.“) | Brief 5 (deaktivierter Knopf mit Grund); buttons-and-selection.md | `ui:DisabledHint.Text="Verfügbar, sobald ein Vor- oder Nachname eingetragen ist."` | verstößt – erledigt in Slice 08 | S |
| W44 | Zeile Kontakt (:61): `240,160` neben der Zeile `*,*` darüber: Ränder fluchten nicht, zwei Raster in einer Gruppe; masken §7.4 Akzeptanz „eine Spaltenaufteilung je Gruppe; alle Ränder fluchten“ gilt auch für Anschrift (:80) | masken R4, §7.4 | Sprache und Geburtstag auf `*,*` | weicht ab, dokumentiert | S |

#### Views/PosView.axaml (Kassa)

| Nr. | Befund | Regel | Vorschlag | Einstufung | Aufwand |
| --- | --- | --- | --- | --- | --- |
| W45 | 14 Felder ohne `FormField`, 13 nur mit Platzhalter: Rechnungsempfänger (:220-223, :513-516), Gutschein-Code (:436), PIN (:359), Grund Storno (:538), Notiz Abschluss (:627), Grund Einlage (:644) → Muster 1 | inputs.md §1; Brief 5.2, 10 | `FormField` | verstößt – erledigt in Slice 09 | M |
| W46 | Kartenzahlung (:419-435): Ergebnis „genehmigt“/„fehlgeschlagen“ erscheint nur als Hintergrundfarbe plus Text, **ohne Live-Region**; ebenso Wechselgeld (:443) und Druckstatus (:248, :498) | accessibility.md §6; dialogs-and-messages.md §5 | `LiveSetting` assertive bei Fehlschlag, polite bei Erfolg; Symbol zur Farbe | verstößt – erledigt in Slice 05 | S |
| W47 | Reiterschiene „Kassieren/Belege/Kassenlade & Abschluss“ (:20-22) und „Einlage/Entnahme“ (:635-638) ohne Auswahlzustand → Muster 5; Unbilled-Zeilen (:58) heißen nach der Uhrzeit → Muster 6; Zeilenknöpfe (:286, :290) gleichlautend | navigation.md §1; accessibility.md §7 | siehe Muster | verstößt – erledigt in Slice 19, 20 | M |
| W48 | Fehlertext der Rechnungsvorbereitung `InvoiceHint` (:212, :505) als `crit small`-Text ohne Symbol; Alerts (:39-45) → Muster 2 | Brief 9 | `InfoBar` oder `FormField Error` | verstößt – erledigt in Slice 05 | S |
| W49 | Knöpfe „Wertgutschein“, „Behandlung“, „Paket“ (:144, :157, :171) ohne Verb → Muster 13; Bezahlen-Knopf `MinHeight=46` (:445), Abschluss 42 (:629) → Muster 9 | content-design.md §4; Brief 4.4 | „… verkaufen“; Höhe 34 oder Ausnahme für die Kassa im Brief 4.4 | verstößt – erledigt in Slice 09 | S |

#### Views/CalendarView.axaml (Kalender, Seitenleisten, Kopfzeile)

| Nr. | Befund | Regel | Vorschlag | Einstufung | Aufwand |
| --- | --- | --- | --- | --- | --- |
| W50 | Terminkarten (`Border.appt`, :2448-2455), Zettel (:2550) und Streifen (:2411) sind weder fokussierbar noch benannt (nur Streifen haben `AutomationProperties.Name`); die Pfeiltasten-Auswahl (`CalendarViewModel.Keyboard.cs`) setzt keinen UI-Automation-Fokus und sagt nichts an (im ganzen Projekt kein `RaiseNotification`/Live-Region-Aufruf dafür). Ein Screenreader sieht nur „Kalenderfläche“ | accessibility.md §6, §2 (Keyboard as primary), §3; Brief 9 | Termin-Peer mit Name „{Zeit} {Gast}, {Behandlung}, {Status}“ und Auswahlzustand, oder `RaiseNotificationEvent` bei Auswahlwechsel; Test mit Narrator | verstößt – erledigt in Slice 17, 18 | L |
| W51 | 26 Felder ohne `FormField`; 13 ComboBoxen ganz ohne Namen (Serienintervall :259, Freigabe :584, Freie Zeit :910-945, Behandlung :1252, Gruppenbuchung :1431/:1456, Bereich :1591, Grund :1679, Standort :1906); Beschriftung steht als `TextBlock.label` daneben (:889, :894, :909, :919, :930, :1251, :1678) → Muster 1 | inputs.md §12; accessibility.md §7 | `FormField` oder `LabeledBy` | verstößt – erledigt in Slice 07 | M |
| W52 | Kontrollkästchen ohne Namen: Serienvorschau (:279) und Ausfall-Assistent (:1174) haben keinen Inhalt, `AccessibleNames` überspringt sie | accessibility.md §7 | `AutomationProperties.Name="{Binding DateText}"` | verstößt – erledigt in Slice 07 | S |
| W53 | Mehrere Primärknöpfe im Termin-Panel (Einchecken/Kassieren :304/:311, Verschieben :348, Notiz speichern :125, Gast eintragen :193, Gruppe einchecken :228) → Muster 4; „Abbrechen“ meist sekundär statt Ghost (:70, :124, :297, :350, :418, :598, :1682) | Brief 5.1, 5.12 Punkt 6 | Primär nur in der Karte, die gerade bearbeitet wird; Abbrechen als Ghost | verstößt – erledigt in Slice 21 | M |
| W54 | Maske „Neuer Termin“ (BookingViewModel, :1247-1350): Vorname/Nachname/Telefon/E-Mail nur Platzhalter (:1312-1316), Notiz :1341, Labels `TextBlock.label` unverbunden, „Anlegen“ sekundär; wiederholt die Felder aus Customers :59-136 | masken R6, R9; Brief 5.14 | wie W36 gemeinsam umbauen | bekannt (masken.md §8) | M |
| W55 | Handgebauter Zähler „Personen“ aus zwei Symbolknöpfen und Text (:1605-1616), Knopfnamen „Weniger“/„Mehr“ ohne Bezug; der Wert ist kein Feld und wird nicht angesagt | Brief 5.19 (Zahlenfeld mit Pfeilspalte bis 20 Schritte); inputs.md §8 | `ui:NumberField ShowSpinner="True"` (wie :51) | verstößt – erledigt in Slice 07 | S |
| W56 | „Ganze Buchung stornieren“ (:238-239): Ghost mit lokalem `Foreground=CritBrush` statt Klasse `danger` (Rahmen, Hover, Fokus nicht definiert) | Brief 5.1 (Gefährlich) | `Classes="danger"` | weicht ab, nicht dokumentiert – erledigt in Slice 34 | S |
| W57 | Meldungskarte (:483-503) als `Border` mit `PointerPressed`/`KeyDown` statt Knopf: Rolle Gruppe, Name nur Titel, Hinweis nur im Tooltip; Fokusanzeige hängt am Stil `messagecard` (nicht geprüft) | accessibility.md §6 (Rollen); Brief 4.5 (Fokus) | `Button` mit Vorlage oder `ListBoxItem` verwenden | weicht ab, nicht dokumentiert – erledigt in Slice 35 | S |
| W58 | Menü „Neuer Termin“ (:2008): „Sonderfälle“ als deaktivierter `MenuItem` als Gruppentitel (Screenreader: „Sonderfälle, nicht verfügbar“); Primärknopf heißt „Termin“ (:2038, Nomen) | navigation.md §4 (Menü); content-design.md §4 (Create: „New“ + Objekt) | Gruppentitel über `Separator` plus `MenuItem.Header`-Gruppe oder weglassen; „Neuer Termin“ | weicht ab, nicht dokumentiert – erledigt in Slice 35 | S |
| W59 | Hinweise mit Radius 6 und Innenabstand 10/6 oder 8/6 (:1192, :1761, :1845), InfoBar mit Radius 6 (`Styles.axaml:1360`) statt 8 (Brief 4.2, Hinweis 10/12) → Muster 9; Toast :2729 `Padding="14,6,6,6"` statt 10/16 | Brief 4.2, 5.9, 5.10 | auf 8 und 10/12 | weicht ab, nicht dokumentiert – erledigt in Slice 32 | S |
| W60 | Datumsauswahl: Tage des Nachbarmonats in `Fg3Brush` (`Styles.axaml:1048-1050`) = 4,18:1 hell (unter 4,5:1), Brief 10 verbietet `fg-3` als Text unter 24 px; der Kommentar `Styles.axaml:993` nennt „nur blass“ als Absicht | Brief 2.6, 10; foundations.md (Kontrast) | Nachbartage in `Fg2Brush` zeigen oder ausblenden | verstößt – erledigt in Slice 28 | S |
| W61 | Kennzahlen und „Als Nächstes“ im Flyout (:2128-2155, :2193): Knopfname ist „12“ bzw. „09:30“ → Muster 6; Kopf eines Tages `Grid` mit `PointerPressed` (:2298) ohne Rolle (Tastaturweg „G“ vorhanden) | accessibility.md §7 | siehe Muster 6 | verstößt – erledigt in Slice 20 | S |
| W62 | Statusfarbe `StDone` (#8b918d) auf `StDoneBg` (#efede8) = 2,75:1 hell (Balken des erledigten Termins, `Styles.axaml:1137-1140`); Brief 2.6 fordert 3:1 für Elemente, die Zustand tragen; Status steht zusätzlich als Text im Termin (Brief 9), daher Dekoration, aber nicht belegt | Brief 2.6, 9 | `StDone` abdunkeln (z. B. #6f7773) | weicht ab, nicht dokumentiert – erledigt in Slice 36 | S |

#### Themes/Styles.axaml, Themes/Colors.axaml, Controls (seitenübergreifende Bausteine)

| Nr. | Befund | Regel | Vorschlag | Einstufung | Aufwand |
| --- | --- | --- | --- | --- | --- |
| W63 | `Border.alert` (`Styles.axaml:99-109`) ist Rot-fest (Hintergrund `CritSoft`, Rand `Crit`, Text `Crit` für alle Nachfahren-`TextBlock`); Warn- und Neutral-Varianten gibt es nur über lokale Werte in den Views → Muster 2 | Brief 5.9 | Stil entfernen, sobald alle Stellen `InfoBar` nutzen | verstößt – erledigt in Slice 05 | M |
| W64 | `AccessibleNames.cs:26-27` leitet den Namen von TextBox/ComboBox aus dem Platzhalter ab; das verdeckt fehlende Beschriftungen im Scan und liest Beispieltext als Namen vor | accessibility.md §7 | nach dem Umbau auf `FormField` entfernen | verstößt – erledigt in Slice 11 | S |
| W65 | Namens-Scan (`ViewTests.FocusOrder.cs:210-241`) prüft nur Seiten im Ausgangszustand (Panel „Termin“, leere Listen); Seitenleisten, Dialoge, Masken im Bearbeitungszustand und gefüllte Listen fehlen, Record-Namen werden nicht erkannt → Muster 7 | accessibility.md §7, §11 | Scan je Zustand (Panel, Dialog, Bearbeiten) laufen lassen | weicht ab, nicht dokumentiert – erledigt in Slice 37 | M |
| W66 | Sekundärknopf-Rahmen `LineStrong` = 1,82:1 hell, 2,09:1 dunkel auf Fläche (`Colors.axaml`, Brief 5.1 Tabelle); Brief 2.6 verlangt 3:1 für Rahmen von Bedienelementen und löst es nur für Felder und Kontrollkästchen; der Skill verlangt für Knöpfe mit Text keinen Rahmenkontrast (buttons-and-selection.md §Contrast) | Brief 2.6 gegen 5.1 | Satz 2.6 auf „Felder, Kontrollkästchen, Optionsfelder“ einschränken | weicht ab, nicht dokumentiert – erledigt in Slice 36 | S |
| W67 | `DisabledHint` zeigt den Grund nur als Tooltip am deaktivierten Knopf (`DisabledHint.cs`); Tab überspringt ihn, der Grund erreicht Tastatur- und Screenreader-Nutzer nie → Muster 12 | buttons-and-selection.md (Disabled buttons explain themselves) | zusätzlich `AutomationProperties.HelpText` setzen | weicht ab, dokumentiert (Brief 5) | S |
| W68 | Hochkontrast ist „vorbereitet, nicht geprüft“; Verläufe (`AccentGradientBrush`, `HeroBrush`) und das Schraffurmuster `ProcessingHatchBrush` (`Colors.axaml:48`, `:110`) haben keine Hochkontrast-Ersatzwerte | foundations.md (Hochkontrast); accessibility.md §9 | Hochkontrast-Lauf, Ersatzwerte für Verläufe | bekannt (Brief 9 „Hochkontrast … nicht geprüft“) | M |

### Prüfung der Masken-Checkliste (docs/gestaltung/masken.md §6)

Nur drei Masken erfüllen die Checkliste weitgehend: `CustomerContactForm` (Ausnahmen W41 bis W44), der Kalender-Notizblock (`AppointmentNoteBox`, `DayNoteBox`: `FormField`, Zähler, Esc, Strg+Enter) und die Zeiten-Maske (`PreparationBox`/`FollowUpBox`). Alle übrigen Masken sind Altbestand (masken.md §8).

| Punkt | CustomerContactForm | Neue Kundin (Customers) | Neuer Termin (Calendar) | Einstellungsseiten (Backup, Sync, Devices, Network) | Anmeldung/Einrichtung | Kasse (Rechnungsempfänger, Rabatt) |
| --- | --- | --- | --- | --- | --- | --- |
| 1 Gruppen mit Überschrift (R1) | ja | nein | nein | je Karte ja | nein | teilweise |
| 2 Seltenes eingeklappt (R2) | ja | nein | nein | – | – | – |
| 3 Eine Hauptspalte (R3) | ja | nein (3 Muster) | ja | ja | ja | ja |
| 4 Feldbreite und Ränder (R4) | zwei Abweichungen (W44) | nein | nein | Felder 100 % breit | `MinHeight` 40 bis 52 | – |
| 5 Eine Primäraktion, Speichern deaktiviert (R5) | ja (Hinweis fehlt W43) | ja, Abbrechen sekundär | ja, mehrere Primär (W53) | zwei Primär (W16, W11) | je Formular ja | ja |
| 6 Beschriftung über dem Feld, Substantiv (R6) | ja | `TextBlock.label` unverbunden | nein (Platzhalter) | `TextBlock.label` mit Name | nein (Platzhalter) | nein (Platzhalter) |
| 7 Hilfetext eine Zeile, Platzhalter nur Beispiel | ja | nein | nein | Hilfetexte als Absatz | nein | nein |
| 8 Pflicht mit Stern (R7) | ja | nein (Satz statt Stern) | teils (`IsRequired` bei Zeit) | nein | nein | teils |
| 9 Fehler am Feld, Fokus zum ersten Fehler (R8) | ja | nein (`Border.alert` unten) | nein | `Border.alert` | `Border.alert` | `Border.alert` |
| 10 Höchstens ein Fragezeichen je Gruppe (R9) | ja | nein (3 an Feldern) | ja | ja | ja | – |
| 11 Vorschlagsliste statt Chips (R10) | ja | Titel als Freitext | – | – | – | – |
| 12 Abgeleitete Werte als Textzeile (R11) | ja | – | – | – | – | – |
| 13 Passendes Steuerelement, Länge begrenzt (R12) | ja | `MaxLength` fehlt | `NumberField` fehlt bei Personen (W55) | `MaxLength` nur bei Gerätename | `MaxLength` nur PIN, Code | `MoneyField` ja |
| 14 Tab, Startfokus, Enter, Esc, Beschriftung am Feld (R13) | ja | nein | teilweise | nein | Enter ja, Rest nein | nein |
| 15 Abstände nur aus Brief 4.1 | W41 | `Spacing 12`, `Padding 24` | ja | `Padding 18` | `Padding 32`, `Spacing 18` | `Padding 14/18` |

### Kontrast (rechnerisch aus Themes/Colors.axaml)

Skript: `contrast.py` im Scratchpad der Sitzung, nicht im Repository (WCAG-Formel, nur Paare, die im Code vorkommen). Text 4,5:1, Symbole und Rahmen 3:1.

Bestanden (Auswahl, hell / dunkel): Fg auf Surface 16,08 / 14,13; Fg2 auf Surface 8,12 / 8,41, auf Surface3 6,64 / 6,81, auf AccentSoft 6,58 / 6,30, auf den Terminflächen 6,6 bis 7,5; Accent auf Surface 7,70 / 7,02, auf AccentSoft 6,24 / 5,26; AccentFg auf primärem Verlauf oben 5,73 / 7,70 und unten 8,89 / 5,30, auf Hover 9,96 / 8,61; Ok auf OkSoft 5,06 / 6,01; Warn auf WarnSoft 4,86 / 6,53, auf Surface 5,84 / 8,11; Crit auf CritSoft 5,34 / 5,69, auf Surface 6,57 / 6,37; Note auf NoteSoft 5,31 / 8,16; StBooked auf StBookedBg 4,76 / 5,89; Navigation RailFg auf Rail 11,01 / 14,57, RailFg2 auf Rail 6,42 / 6,31, RailFg auf RailSelected 6,63 / 9,73; Toast 14,13 / 15,45; Fg3 als Symbol und Feldunterkante auf Surface 4,18 / 5,09 (Brief 2.6: 3:1 ist erfüllt).

Unter der Schwelle:

| Paar | hell | dunkel | wo | Bewertung |
| --- | --- | --- | --- | --- |
| Fg3 als Text auf Surface | 4,18 | 5,09 | Datumswahl Nachbarmonat (`Styles.axaml:1048-1050`) | hell unter 4,5:1; Brief 10 verbietet es (W60) |
| StDone auf StDoneBg | 2,75 | 5,97 | Balken erledigter Termine | hell unter 3:1 (W62) |
| LineStrong auf Surface | 1,82 | 2,09 | Rahmen sekundärer Knöpfe, Chips | Widerspruch im Brief (W66) |
| StCheckedIn auf StCheckedInBg | 4,06 | 6,09 | Balken eingecheckt | über 3:1, kein Text, kein Befund |
| RailFg2 auf RailSelected | 3,87 | 4,21 | kommt im Code nicht vor (gewählte Zeile nutzt RailFg) | kein Befund |

Zustände: die Fehler-/Warn-Kombinationen aus W10 und W33 sind lesbar (Crit auf WarnSoft 5,47 / 5,13; Crit auf AccentSoft 5,33 / 4,77), verletzen aber Farbrolle und „Warnungen nie rot“.

### Was nicht geprüft werden konnte, und womit es nachzuholen wäre

- **Screenreader und Narrator:** Rollen, Namen, Reihenfolge der Ansage, Wirkung der Live-Regionen, ob ein modaler Rückfragedialog den Rest der Seite für UI Automation ausblendet. Bekannt: Brief F28 „Lauf mit Narrator steht aus“. Nachholen: Narrator-Durchgang je Seite mit der Fokusreihenfolge-Liste aus `ViewTests.FocusOrder.cs`, dazu UI-Automation-Inspektor (Accessibility Insights) für Namen, Rolle, `ExpandCollapse`, `SelectionItem`.
- **Laufzeitverhalten:** Esc-Stufen, Enter-Speichern, Fokus nach Schließen, `IsDefault` bei mehreren sichtbaren Formularen (LoginView W4), Wirkung der `Border.alert`-Nachfahren-Selektoren auf Knöpfe in Meldungen (z. B. Calendar :1037, Shell :134 — vermutlich rote Knopfschrift, nicht gesehen). Nachholen: Headless-Tests mit `KeyPress` (Muster in `avalonia-mapping.md` §9) und Screenshots je Zustand.
- **Aussehen:** Hover, gedrückt, Fokusring, deaktiviert je Zustand und Thema; Bewegung und Reduced Motion (Tests existieren, hier nicht gelaufen); Darstellung bei 125 % und 150 % Skalierung; Hochkontrast (bekannt, Brief 9). Nachholen: Bildtests als Inhaberin und Windows-Hochkontrast-Designs, `ReducedMotion`-Test.
- **Kontrast:** nur rechnerisch aus `Colors.axaml`; nicht gemessen sind Text auf Personenfarben (Daten), Text auf dem Verlauf `HeroBrush` mitten im Verlauf, Schrift auf dem schraffierten Einwirkzeit-Muster, Tooltips und Flyouts im echten Popup-Fenster. Nachholen: Bildpunkte aus Screenshots lesen.
- **Texte aus ViewModels:** Fehlermeldungen und Hinweise, die im Code gebaut werden (`*ViewModel.cs`), wurden nur stichprobenhaft auf Richtungswörter und „du“ geprüft; „Was, warum, zwei Wege“ je Fehlertext ist nicht durchgegangen. Nachholen: Grep über `Error =`/`…Text =` und Abgleich mit content-design.md §5.
- **Verwaltung im WebView2 (`AdminView`) und Beleg (`ReceiptPaper`):** bewusst nicht geprüft (Web bzw. Druckvorlage mit festen Farben nach Brief 10).
- **Keine Doppelmeldung:** bekannte Punkte sind als „bekannt“ geführt (W31, W36, W54, W68); die Brief-Abweichungen aus 4.2, 4.4, 4.7, 5.1 (Hover dunkler, Höhe 34, Radius 8/12, Gewicht 500, Skalierung 0,97, Inter, Titel 22) wurden nicht gemeldet.

## Teil B: Web-Oberflächen

## Prüfliste Web-Oberflächen (Stand dev, ee66289)

Geprüft wurden nur Quelltexte (`web/src`, `web/index.html`, `web/buchen/index.html`, `web/scripts/check-bundles.mjs`); nichts gebaut, nichts gestartet, nichts geändert. Rechnerischer Kontrast: Skripte `contrast.py` und `c2.py` im Scratchpad. Erster Durchgang: B1 bis B32 (fluent2-web, modern-web-guidance). Zweiter Durchgang ab „Zweiter Durchgang“ (Mitte der Datei): B33 bis B70 mit `impeccable` und `emil-design-eng` sowie den zuvor ausgelassenen Seiten.

Einstufung: **V** = verstößt, **ND** = weicht ab, nicht dokumentiert, **D** = weicht ab, dokumentiert, **B** = bekannt (mit Verweis). Aufwand: S unter 1 h, M halber Tag, L mehr.

### Zusammenfassung

| Einstufung | Zahl |
| --- | --- |
| V verstößt | 25 |
| ND weicht ab, nicht dokumentiert | 39 |
| D weicht ab, dokumentiert | 0 |
| B bekannt | 4 |
| **gesamt** | **68** (B1 bis B32 ohne B17, B26 und B27, dazu B33 bis B70; die Muster-Zeilen zählen nicht mit) |

Nach Skill (Hauptregel je Befund): fluent2-web 49, modern-web-guidance 8 (B1, B1a, B2, B16, B21, B22, B31, B55), impeccable 9, emil-design-eng 2.

#### Die fünf wichtigsten Punkte

1. **Bestätigen-Knopf bekommt den Anfangsfokus statt „Abbrechen“** in allen sieben `<dialog>`-Rückfragen: React setzt `autoFocus` per `focus()` im noch geschlossenen Dialog (wirkungslos), `showModal()` fokussiert danach den ersten Knopf, und das ist die zerstörende Aktion (B1).
2. **Angemeldete Shell hat kein `<main>`, kein `<h1>`, keinen Skip-Link und wechselt weder `document.title` noch Fokus beim Seitenwechsel** (B12 bis B14).
3. **Fünf Reiterschienen (`role="tablist"`) ohne Pfeiltasten, ohne Roving-tabindex und ohne `tabpanel`**, obwohl `stock/stockUi.tsx` es vormacht (B3).
4. **Kein `prefers-reduced-motion` in `index.css` und nirgends `forced-colors`**; mehrere Fokusdarstellungen bestehen nur aus `box-shadow` und verschwinden im Hochkontrastmodus (B5, B6).
5. **73 `<th>` in 11 Dateien ohne `scope`** und Preise als `type="number"` (Fließkomma, Tastaturpfeile ändern Beträge) (B4, B7).

#### Wichtigste neue Punkte aus dem zweiten Durchgang

1. **Geldeingabe an der Kasse (B37):** `parseMoney` macht aus ungültiger Eingabe stillschweigend 0 und rundet weg; kein Fehlertext in acht Betragsfeldern (Kasse, Tagesabschluss).
2. **Dienstplan nicht lesbar und ohne Rückfragen (B45 bis B47):** Zellenknöpfe verbergen Schichtzeiten hinter `aria-label`, Warnungen nur im `title`, „Verwerfen“ wirft alle Entwürfe ohne Rückfrage weg.
3. **Zustände und Schrift (B62, B64):** Inter wird im Web nie ausgeliefert (App und Web zeigen verschiedene Schriften), und Knöpfe haben keinen Druck-Zustand, den der Brief verlangt.

#### Seitenübergreifende Muster

| Nr. | Muster | Stellen |
| --- | --- | --- |
| B-Muster 1 | Rückfragedialog siebenmal einzeln gebaut (je eigener `showModal`-Effekt, Titel-ID, Kopfzeile) plus zwei Eigenbau-Modals und ein Eigenbau-Popover ohne `<dialog>`/Popover | siehe B1, B2, B21 – erledigt in Slice 38 (B2; B1, B21 offen) |
| B-Muster 2 | Formularbausteine `Row`, `Group` und `focusFirstInvalid` mehrfach kopiert (BusinessInfo, Promotions, Closures, RoomClosures, Subscriptions) | B9 |
| B-Muster 3 | Reiter ohne ARIA-Tabs-Verhalten | B3 |
| B-Muster 4 | Zustände nur über `box-shadow`/Farbe, ohne Hochkontrast-Rückfall | B5, B6 |

---

### Verwaltung (Team-Oberfläche, `web/src` ohne `booking`)

#### Gemeinsame Dialoge und Popover

Dateien: `closures/ClosuresPage.tsx`, `closures/RoomClosuresPage.tsx`, `promotions/PromotionsPage.tsx`, `subscriptions/SubscriptionsPage.tsx`, `businessinfo/BusinessInfoPage.tsx`, `stock/stockUi.tsx`, `anamnesis/Modal.tsx`, `admin/SyncStatus.tsx`

| Nr. | Befund | Regel | Vorschlag | Einstufung | Aufwand |
| --- | --- | --- | --- | --- | --- |
| B1 | `autoFocus` auf „Abbrechen“ in `<dialog>`: `ClosuresPage.tsx:544`, `RoomClosuresPage.tsx:432`, `PromotionsPage.tsx:618`, `SubscriptionsPage.tsx:618`, `BusinessInfoPage.tsx:685`. React ruft `focus()` beim Einhängen, der Dialog ist da noch zu; `showModal()` fokussiert dann den ersten Knopf, das ist „Archivieren/Löschen“ (steht im DOM vorn). Die Kommentare behaupten das Gegenteil. Jsdom-Tests sehen das nicht. Mit Playwright zu bestätigen. | fluent2-web `dialogs-and-messages.md` (Anfangsfokus nie auf zerstörender Aktion); modern-web-guidance Dialog (`autofocus`-Attribut, nicht React-`autoFocus`) | Gemeinsamen `ConfirmDialog` bauen, der nach `showModal()` den Abbrechen-Knopf per `ref.focus()` fokussiert (oder das HTML-Attribut `autofocus` setzt); Test mit Playwright | V – erledigt in Slice 14 | S |
| B2 | Fünf kopierte Rückfragedialoge (`ArchiveDialog` ×2, `DeleteDialog` ×3) mit identischem `useEffect` für `showModal`; dazu `ReturnDepositDialog.tsx:32` und `CancelAppointmentsDialog.tsx:73`. Der Dialog-Zweig `else dialog.setAttribute('open', '')` macht ihn nicht modal. | fluent2-web (ein Baustein statt Kopien); CLAUDE.md „native `<dialog>`“ | Eine Komponente `ConfirmDialog` (Titel, Text, Bestätigen, Abbrechen, optional Grund) in `components/`, die sieben Stellen darauf umstellen; Brief 5.5/5.27 (Reihenfolge, Innenabstand 24) dort einmal umsetzen | ND – erledigt in Slice 38 | M |
| B1a | `anamnesis/Modal.tsx:23` und `stock/stockUi.tsx:90` (`ConfirmModal`, benutzt in `StockCount.tsx`, `StoragePlacesAdmin.tsx`) sind Eigenbau mit `div role="dialog" aria-modal`: Hintergrund nicht inert (Tab läuft hinter den Dialog), Esc per `document`-Listener (schließt auch, wenn ein Feld/Menü Esc schon verbraucht hat), kein Fokusfang. `Modal.tsx` gibt den Fokus zurück, `ConfirmModal` nicht. | fluent2-web Dialoge (Hintergrund inert, Fokus zurück); modern-web-guidance `<dialog>`; Brief 11.4 F19 „Web-Modal offen“ | Beide auf den gemeinsamen `<dialog>`-Baustein umstellen (`Modal` mit `showModal()`, Druckansicht beachten: `.modal-backdrop` hat Print-Regel `index.css:1943`) | B (design.md 11.4 F19, „Web-Modal ist offen“) | M |
| B21 | `admin/SyncStatus.tsx:43`: Eigenbau-Popover (`div role="dialog" aria-label`) ohne Esc, ohne Klick-daneben-Schließen, ohne Fokusverwaltung; Knopf hat `aria-expanded` aber kein `aria-controls`. | modern-web-guidance Popover (`popover="auto"`, `popovertarget`); fluent2-web Popover | `popover="auto"` verwenden wie in `PromotionsPage.tsx:389`; `role="dialog"` entfällt (Inhalt ist ein Statusbericht) | V – erledigt in Slice 24 | S |

#### App-Shell (`web/src/App.tsx`, `index.html`)

| Nr. | Befund | Regel | Vorschlag | Einstufung | Aufwand |
| --- | --- | --- | --- | --- | --- |
| B12 | Angemeldete Ansicht ist ein `<div className="app">` (`App.tsx:310`, eingebettet `:271`): kein `<main>`, kein Skip-Link, kein `<h1>` (nur im Anmeldebildschirm `:407`/`:424`); Seitenüberschriften sind `h2` (z. B. `CalendarPage.tsx:253`). Landmarken fehlen, Hilfe/Companion (`aside`) stehen ohne Gegenstück `main`. | fluent2-web `accessibility.md` (Landmarken, Skip-Link, Seitenstruktur); WCAG 2.4.1 | `renderPage(...)` in `<main id="inhalt" tabIndex={-1}>` einhüllen, Skip-Link wie `booking/BookingApp.tsx:84` übernehmen, je Seite genau ein `h1` (im eingebetteten Betrieb Seitentitel) | V – erledigt in Slice 23 | M |
| B13 | `document.title` ist immer „AISPA Studio“ (`index.html`), kein Titelwechsel je Seite (nur `booking/BookingApp.tsx:78` setzt ihn). | fluent2-web `accessibility.md`; WCAG 2.4.2 | Beim Seitenwechsel `document.title = \`${Seite} · AISPA Studio\`` setzen | V – erledigt in Slice 23 | S |
| B14 | Seitenwechsel (`<Fragment key={pageVisit}>`, `App.tsx` ca. 396) setzt den Fokus nicht um; nach Klick auf den Seitenreiter liegt er auf dem Reiterknopf, ein Screenreader erfährt vom Wechsel nichts. | fluent2-web `accessibility.md` (Fokus nach Navigation) | Nach dem Wechsel den Fokus auf die Seitenüberschrift (`h1`/`main`) setzen, wie `booking/BookingApp.tsx:66-76` | ND – erledigt in Slice 43 | S |
| B15 | Seitenreiter der Kopfzeile sind `nav` mit `aria-pressed`-Knöpfen (`App.tsx:275`, `:317`), also Umschalter statt Navigation; `aria-current="page"` wird sonst im Projekt verwendet (12 Stellen). | fluent2-web `navigation.md` (Navigation: `aria-current`) | `aria-pressed` durch `aria-current={aktiv ? 'page' : undefined}` ersetzen | ND – erledigt in Slice 43 | S |
| B16 | Kontomenü ist `<details>` (`App.tsx:328`): schließt nicht mit Esc und nicht bei Klick außerhalb; das Schließen nach Eintrag hängt an einem `onClick` auf einem `div` (`:334`). | modern-web-guidance Popover; fluent2-web Menüs (Esc, Fokus zurück) | `popover="auto"` mit `popovertarget` (oder `<dialog>`-frei) und Esc/Fokus vom Browser; `onClick` auf `div` entfällt | V – erledigt in Slice 23 | S |

#### Hilfe und Begleiter (`help/HelpDrawer.tsx`, `companion/Companion.tsx`, `help/HelpButton.tsx`)

| Nr. | Befund | Regel | Vorschlag | Einstufung | Aufwand |
| --- | --- | --- | --- | --- | --- |
| B18 | `HelpDrawer.tsx:178`: Esc-Listener auf `window` schließt die Hilfe immer, auch wenn ein Dialog oder ein Feld Esc schon verbraucht hat; beim Öffnen wandert der Fokus nicht in die Leiste, beim Schließen nicht zurück zum Fragezeichen. | fluent2-web Esc-Leiter, Fokus zurück (`inputs.md`, `dialogs-and-messages.md`) | Esc nur behandeln, wenn `!e.defaultPrevented` und Fokus in der Leiste; Fokus beim Öffnen auf die Überschrift, beim Schließen auf den Auslöser | V – erledigt in Slice 24 | S |
| B19 | `Companion.tsx:93-96`: Der Knopf `companion-fab` wird beim Öffnen aus dem DOM genommen; der Fokus geht verloren. Beim Ausblenden (`:104`) kehrt der Fokus nicht zurück. Die Leiste öffnet ohne Fokus. | fluent2-web Fokus beim Öffnen/Schließen | Knopf im DOM lassen (`hidden`) oder Fokus per `ref` in die Leiste setzen und beim Schließen zum Knopf zurück | V – erledigt in Slice 24 | S |
| B20 | `HelpButton.tsx` / `index.css:2447`: Fragezeichen 21×21 px, unter dem Zielmaß von 24 px (WCAG 2.5.8); Rand `--border` hat 1,34:1 auf Surface (Zeichen selbst `--muted` ist ausreichend). Ausnahme „Abstand“ greift nur, wenn ringsum 24 px frei sind. | fluent2-web `accessibility.md` (Zielgröße 24) | Größe auf 24 px, oder Klickfläche per `::after` auf 24 px | ND – erledigt in Slice 46 | S |
| B22 | `help/HelpDrawer.tsx:78`: `<img>` der Handbuchbilder ohne `width`/`height` (nur `loading="lazy"`); Layout springt, wenn Bilder laden. | modern-web-guidance Bilder (Größe angeben, CLS) | Maße im Build in `help/topics.ts` mitliefern und als Attribute setzen | ND – erledigt in Slice 46 | S |

#### KI-Assistent und Posteingang (`assistant/*`, `inbox/InboxPage.tsx`)

| Nr. | Befund | Regel | Vorschlag | Einstufung | Aufwand |
| --- | --- | --- | --- | --- | --- |
| B23 | `AssistantPanel.tsx:181`: `aria-live="polite"` sitzt am Container, der erst mit der Antwort gerendert wird; Live-Regionen müssen vorher im DOM stehen, sonst wird der Inhalt oft nicht angesagt. „Denkt nach …“ (Knopftext, `:140`) wird nicht angekündigt. | fluent2-web `accessibility.md` (Live-Regionen) | Leere `<div role="status" aria-live="polite">` dauerhaft rendern und die Antwort hineinsetzen | V – erledigt in Slice 24 | S |
| B24 | `AnswerBlock.tsx:9`: `AiPill` ist `span` mit `aria-label` und `title`, aber ohne Rolle; `aria-label` an `span` wird nicht zuverlässig vorgelesen, und der sichtbare Text „KI“ allein sagt wenig. | fluent2-web `responsible-ai.md` (Kennzeichen); ARIA (Namen nur an Rollen) | Sichtbaren Text „KI“ belassen, `aria-label` entfernen und `<span class="visually-hidden"> erstellt von der KI</span>` anhängen | ND – erledigt in Slice 46 | S |
| B25 | Antwortentwurf im Posteingang (`InboxPage.tsx` ca. 417-436): Textfeld „Antworttext“ ohne KI-Kennzeichen am Entwurf selbst (nur der Hinweis „Wird beim Senden angehängt …“); `KI` steht nur als `dt` bei der Zusammenfassung (`:273`). Ausschalter je KI-Funktion und Weg ohne KI fehlen im Web. | fluent2-web `responsible-ai.md`; Brief 5.28 | `AiPill` und Hinweis „Entwurf der KI, bitte prüfen“ an Label und Knopf „Senden“ | B (design.md 11.4 F30: „`AiFooter` und `AiPill` fehlen in der Team-Oberfläche“) | M |

#### Kalender (`calendar/*`)

B26 (Tastaturweg zum Anlegen) ist im zweiten Durchgang zurückgezogen: der Knopf „+ Termin“ existiert (`CalendarPage.tsx:284`). Keine weiteren Befunde auf dieser Seite im ersten Durchgang.

#### Formulare und Einstellungen (`businessinfo`, `promotions`, `closures`, `subscriptions`, `admin`, `calendar/BookingPanel.tsx`)

| Nr. | Befund | Regel | Vorschlag | Einstufung | Aufwand |
| --- | --- | --- | --- | --- | --- |
| B7 | Preis als `<input type="number" step={0.5}>`: `admin/TreatmentsTab.tsx:258`, `admin/PriceRules.tsx:108`. Fließkomma, Mausrad/Pfeiltasten ändern Beträge, Dezimalkomma je Sprache uneinheitlich, kein Fehlertext bei Eingabe wie „12,5 €“. | fluent2-web `inputs.md` (Geld: `inputmode="decimal"`, Dezimalstring, `type="number"` ist falsch); Brief 5.20 | Textfeld mit `inputMode="decimal"` und Parser wie in der Windows-App (`MoneyInputParser`), ein gemeinsamer `MoneyField`; Dauer-Felder (`:254`, `:325`) dürfen `number` bleiben | V – erledigt in Slice 13 | M |
| B8 | Neukunde im Buchungsfeld: vier `<input>` nur mit `placeholder` (`calendar/BookingPanel.tsx:237-241`), keine `label`, kein `aria-label`; Platzhalter verschwindet beim Tippen. | fluent2-web `inputs.md` (Platzhalter nie als Beschriftung); masken.md R6, Checkliste 6/7 | `Field`-Baustein mit sichtbarer Beschriftung verwenden; `autoComplete="off"` hinzufügen, damit der Browser nicht Mitarbeiterdaten vorschlägt | V – erledigt in Slice 29 | S |
| B9 | Formularbausteine mehrfach kopiert: `Row`/`Group` in `BusinessInfoPage.tsx:320/362` und `PromotionsPage.tsx:211/253` (letztere exportiert und von Closures/Subscriptions genutzt), `focusFirstInvalid` fünfmal (`BusinessInfoPage.tsx:413`, `ClosuresPage.tsx:281`, `RoomClosuresPage.tsx:257`, `PromotionsPage.tsx:311`, `SubscriptionsPage.tsx:304`); `stock/stockUi.tsx:23` hat ein drittes `Field`. Fehlertext-Abstand und Symbol hängen an Einzelkopien; `Row` setzt kein `role` für Fehler, `focusFirstInvalid` nutzt `setTimeout(…, 0)`. | masken.md Kurz-Checkliste 3-9 (ein Raster); Clean Code (Duplikate) | `components/FormField.tsx` und Hook `useFocusFirstInvalid(ref)` zusammenführen; `stock-*`-Klassen schrittweise vereinheitlichen | ND – erledigt in Slice 40 | M |
| B10 | Feldrand `--border-strong` hat 1,82:1 (hell) bzw. 2,09:1 (dunkel) auf `--surface` (`index.css:337`); Mindestmaß 3:1 für Feldgrenzen. Der Brief sieht `--fg-3` (4,18:1 / 5,09:1) vor. | fluent2-web `foundations.md`/`accessibility.md` (3:1 für Feldgrenzen); Brief W10 | Feldunterkante auf `var(--fg-3)` setzen wie vorgesehen | B (design.md 11.4 „Web: Feldunterkante fg-3“ offen) | S |
| B11 | `TeamTab.tsx:116`: `<input type="color">` ohne Text-Alternative; die Farbe ist die einzige Information (Wert nicht sichtbar). | fluent2-web `accessibility.md` (nicht nur Farbe) | Hex-Wert daneben als Text anzeigen oder Auswahl aus benannten Farben | ND – erledigt in Slice 46 | S |

#### Tabellen (`costs/CostsPage.tsx`, `accounting/AccountingPage.tsx`, `reports/ManagementView.tsx`, `reports/ComplianceView.tsx`, `payroll/PayrollPage.tsx`, `pos/BillPanel.tsx`, `admin/*`, `roster/RosterPage.tsx`, `time/TimesheetPage.tsx`)

| Nr. | Befund | Regel | Vorschlag | Einstufung | Aufwand |
| --- | --- | --- | --- | --- | --- |
| B4 | 73 `<th>` ohne `scope` (Skript `scan.py`): `CostsPage` 15, `AccountingPage` 11, `ManagementView` 9, `LocationsTab` 6, `SenderRulesSection` 6, `ComplianceView` 6, `PayrollPage` 5, `BillPanel` 5, `TeamTab` 4, `RosterPage` 3, `TimesheetPage` 3. Daneben 31 `<table>`, aber nur 1 `<caption>`; Namen kommen über `aria-label` (in Ordnung). | fluent2-web `data-display.md` (Tabellen: `th scope`) | `scope="col"` bzw. `scope="row"` ergänzen (mechanisch, per Skript auffindbar) | V – erledigt in Slice 01 | S |

#### Stylesheets (`index.css`, `stock/stock.css`, `businessinfo/businessInfo.css`)

| Nr. | Befund | Regel | Vorschlag | Einstufung | Aufwand |
| --- | --- | --- | --- | --- | --- |
| B5 | Weder `prefers-reduced-motion` noch ein Schalter in `index.css` (8 `transition`-Regeln, z. B. `:178`); nur `stock.css:997`, `booking.css:50`, `myday.css:57` haben ihn. Brief 4.6 verlangt „Animationen aus“ ohne jeden Übergang. | fluent2-web `accessibility.md`/`foundations.md` (Bewegung); Brief 4.6, F22 | Globalen Block `@media (prefers-reduced-motion: reduce) { *,*::before,*::after { transition-duration: 0s !important; animation: none !important } }` wie in `booking.css` | V – erledigt in Slice 02 | S |
| B6 | `@media (forced-colors: active)` kommt nirgends vor. Fokus als `outline: none` + `box-shadow` (`stock.css:104`, `:160`, `:241`, `:337`, `:635`, `:703`, `:806`, `businessInfo.css:183`): Schatten werden im Hochkontrastmodus entfernt, der Fokus ist unsichtbar. Zusätzlich liefert `.appointment` (`index.css:850`) `border: none`, die Termine sind dort nicht abgegrenzt. | fluent2-web `accessibility.md` (forced colors: `outline` statt `box-shadow`, transparenter Rahmen); Brief 9, F26 | Bei `outline: none` durch `outline: 2px solid transparent` ersetzen (bleibt im Hochkontrast sichtbar); Termine/Segmente mit `border: 1px solid transparent` | V – erledigt in Slice 02 | S |
| B28 | `stock/stock.css:8-26` dupliziert Token aus `index.css` (`--stock-fg-2`, `--stock-fg-3`, `--stock-toast`, `--stock-toast-fg`) mit eigenen Dunkelwerten; Änderungen am Token-Satz wirken nicht automatisch. | Brief 12 (Tokens als einzige Quelle) | `--stock-*` durch `--muted`, `--fg-3`, `--toast`, `--toast-fg` ersetzen | ND – erledigt in Slice 41 | S |
| B29 | Token-Paare unter 4,5:1, falls als Text verwendet: `--st-checked-in` auf `--st-checked-in-bg` 4,06:1 (hell), `--st-done` auf `--st-done-bg` 2,75:1 (hell), `--fg-3` auf `--surface-3` 3,42:1 (hell) / 4,13:1 (dunkel). In den Web-Stylesheets ist `--st-*` nicht verwendet (nur Definition `index.css:32-35`, `:88-91`), also nur bei Übernahme in Text relevant. | fluent2-web `foundations.md` (4,5:1, je Zustand und Theme); Brief 2.1 bis 2.4 | Werte im Brief und in `Colors.axaml` zusammen anpassen (z. B. `--st-done` dunkler) oder nur für Flächen/Symbole zulassen und das im Brief vermerken | ND – erledigt in Slice 36 | S |
| B30 | `index.css` setzt `font-size: 14px` und Größen in `px` (Brief W1 verlangt das ausdrücklich); skaliert nicht mit der Browser-Schriftgröße (WCAG 1.4.4 Zoom bleibt möglich, Textgröße-Einstellung wird ignoriert). | fluent2-web `foundations.md` („`rem` verwenden“) | Als dokumentierte Abweichung im Brief eintragen oder auf `rem` umstellen | ND – erledigt in Slice 36 | M |

#### Reiter (mehrere Seiten)

| Nr. | Befund | Regel | Vorschlag | Einstufung | Aufwand |
| --- | --- | --- | --- | --- | --- |
| B3 | Fünf Reiterschienen mit `role="tablist"`/`role="tab"`, aber ohne Pfeiltasten, ohne `tabIndex={-1}` für nicht gewählte, ohne `aria-controls` und ohne `role="tabpanel"`: `admin/MasterDataPage.tsx:110`, `admin/TreatmentsTab.tsx:238`, `calendar/CalendarPage.tsx:255`, `reports/BenchmarkView.tsx:73`, `reports/ReportsPage.tsx:43`. `stock/stockUi.tsx:107` hat Pfeiltasten und Roving-tabindex, aber ebenfalls kein `tabpanel`, kein Pos1/Ende. `nav role="tablist"` (`MasterDataPage`, `TreatmentsTab`, `ReportsPage`) überschreibt zudem die Landmarke. | fluent2-web `navigation.md` (ARIA Tabs, ein Tab-Stopp je Schiene); Brief F15 | Eine Komponente `Tabs` (aus `stockUi.tsx` herausziehen: Pfeile, Pos1/Ende, `aria-controls`, `tabpanel`) und alle sechs Stellen darauf umstellen; `nav` durch `div` ersetzen | V – erledigt in Slice 22 | M |

---

### Kundenbereich (`web/src/booking`)

Der Kundenbereich ist deutlich sauberer: Skip-Link und Fokus auf die Überschrift nach Seitenwechsel (`BookingApp.tsx:66-92`), `document.title` je Seite, `lang` je Sprache, `Field` mit `label for`, `aria-describedby`, `aria-invalid` und `autoComplete` (`components.tsx:72`), `inputmode` für Betrag und Code, `prefers-reduced-motion` (`booking.css:50`), 3-px-Fokusring in eigener Farbe, Bedienflächen 44 px, kein `<dialog>`-Bedarf. Importe: nur `../api/client` und `api/schema` (Typen); keine Importe aus der Team-Oberfläche gefunden. Kontrast (hell und dunkel, alle Paare Text/Hintergrund, Rand/Surface, Fokus/Hintergrund) rechnerisch **alle bestanden**, kleinster Wert Rand/Surface 3,5:1.

| Nr. | Befund | Regel | Vorschlag | Einstufung | Aufwand |
| --- | --- | --- | --- | --- | --- |
| B31 | Keine Content-Security-Policy gefunden: weder `<meta http-equiv>` in `web/buchen/index.html` noch ein Header in `src/**/*.cs` (Suche nach „Content-Security-Policy“ ohne Treffer; Hosting/Proxy könnte sie setzen, nicht prüfbar). Die Seite nimmt Karten-Links und Zahlungsdaten (Kartengarantie) entgegen. | modern-web-guidance CSP; CLAUDE.md „CSP“ | CSP-Header für `/buchen/` ergänzen (`default-src 'self'`; Stripe-Quellen gezielt freigeben), zuerst als `Content-Security-Policy-Report-Only` | ND – erledigt in Slice 47 | M |
| B32 | `booking.css` hat keinen `forced-colors`-Abschnitt; der Fokusring ist `outline` (bleibt sichtbar), aber Karten/Reiter, die nur über Hintergrund unterschieden sind, verlieren im Hochkontrast ihre Abgrenzung. Nicht am Bild geprüft, nur am Code. | fluent2-web `accessibility.md` (forced colors) | Mit Edge-Hochkontrast (Windows) einmal alle Schritte ansehen; wo Rand fehlt `border: 1px solid transparent` | ND – erledigt in Slice 46 | S |

---

### Kontrast (rechnerisch, Kurzfassung)

Verwaltung hell/dunkel, nur zusammen vorkommende Paare aus `index.css`: Text/Hintergrund 14,1/15,5, Gedämpft/Hintergrund 7,1/9,2, Akzent/Surface 7,7/7,0, Weiß/Akzent 7,7/7,0, Danger/Surface 6,6/6,4, Warn/Warn-soft 4,86/6,53, Ok/Ok-soft 5,06/6,01 – bestanden. Unter Grenze: Feldrand (B10, bekannt), `--st-*`-Paare und `--fg-3` auf `--surface-3` (B29). Fokusfarbe (Akzent) auf Surface 7,7/7,0.

### Nicht geprüft und womit nachzuholen

| Was | Womit |
| --- | --- |
| Laufzeitverhalten (B1: tatsächlicher Anfangsfokus im `<dialog>`; B16/B21: Esc/Klick-daneben) | Playwright-Test je Dialog: nach `showModal()` `document.activeElement` prüfen |
| INP und LCP (Team: alle 30+ Seiten in einem Bundle, kein `lazy(`/`startTransition`; Polling 1 s in `InboxPage.tsx:211`, 2 s in `ComplianceView.tsx:109`) | Lighthouse und Performance-Trace (Chrome DevTools) mit angemeldetem Inhaber |
| Bundle-Größe Kundenbereich (unter 100 kB gzip) | `npm run build && npm run check:bundle` (Skript vorhanden, nicht gelaufen; keine Builds erlaubt) |
| Screenreader (Narrator/NVDA), Ansage von Live-Regionen (B23), Namen/Rollen | Narrator mit Edge/WebView2, `axe` über alle Routen als Inhaberin |
| Hochkontrast, Dunkelmodus am Bild, 200-%-Zoom, Touch-Zielgrößen im Kundenbereich | Edge-Kontrastdesigns, Playwright-Screenshots, axe `target-size` |
| Seiten, die auch im zweiten Durchgang nur punktuell gelesen wurden: Kalender-Panels, `TeamTab`, `LocationsTab`, `TreatmentsTab`, `PaymentsTab`, `IntakeTab`, `InboxPage`, Kundenbereich `Vouchers`, `Waitlist`, `MyAppointmentsPage`, `AnamnesisPage`, `SignInPage` | gezielte Durchsicht je Seite; `axe` findet viel automatisch |
| Wirkung von Abständen, Hierarchie und Bewegung am gerenderten Bild (Skills `impeccable`, `emil-design-eng` wurden nur am Quelltext angewandt) | Playwright-Screenshots hell und dunkel, bei 125 % und 150 % Skalierung, Aufnahme der Übergänge in Zeitlupe |

---

## Zweiter Durchgang

Umfang: `impeccable` und `emil-design-eng` geladen (Hierarchie, Zustände, UX-Texte, Übergänge, Mikrodetails; der `impeccable`-Kontextbefehl wurde nicht ausgeführt, weil er eine Datei herunterlädt) und die zuvor ausgelassenen Seiten im Quelltext gelesen: `StaffPage`, `PosPage` mit `BillPanel`, `DayPanel` und `ReceiptView`, `RosterPage`, `TimesheetPage`, `PayrollPage`, `AccountingPage`, `myday/*`, `operator/*`, `BookPage` (alle Schritte). Nummern ab B33. Weiterhin nichts geändert, nichts gebaut.

**Zu B26 (Tastaturweg zum Anlegen im Kalender): zurückgezogen.** `CalendarPage.tsx:284` hat den Knopf „+ Termin“ (primär), der das Buchungspanel öffnet. Das Panel „Lücken schließen“ (`GapPanel`, `CalendarPage.tsx:275`) hilft beim Füllen freier Zeiten und ist kein Ersatz, wird aber auch nicht gebraucht. Die erste Suche nach „Neue Buchung“ war zu eng. Offen bleibt nur die Beschriftung (B67).

**Kontrast, ergänzt:** Mein Tag und Betreiber verwenden dieselbe Palette wie der Kundenbereich (Rand `#8a948e` auf Weiß rund 3,2:1, dunkel `#7d8a83` auf `#1d2320` rund 3,8:1; `myday.css:1-45`, `operator.css:1-35`); keine Befunde. Hinweis: `contrast.py` und `c2.py` im Scratchpad wurden zwischenzeitlich von einer anderen Sitzung überschrieben; die Zahlen des ersten Durchgangs decken sich mit den Angaben im Brief (Feldrand 1,82:1, `--st-done` 2,75:1).

**Gesamteindruck der gelesenen Seiten:** „Mein Tag“, die Betreiber-Seite und der Buchungsablauf sind sauber gebaut (`aria-disabled` statt `disabled`, Fokus auf Überschrift, 44-px-Ziele, Fehler am Feld, Rückfragen vor Zerstörendem). Die Befunde sammeln sich in der Team-Oberfläche: Kasse (Geldeingabe), Dienstplan und Zeiten (Namen und Rückfragen), Zugänge (Gruppen und Zustände) sowie die Zustände der Knöpfe (Brief 5.1).

### Verwaltung, zweiter Durchgang

#### Zugänge (`admin/StaffPage.tsx`)

| Nr. | Befund | Regel | Vorschlag | Einstufung | Aufwand |
| --- | --- | --- | --- | --- | --- |
| B33 | Personenliste als `<button className="selected">` ohne `aria-current`/`aria-pressed` (`StaffPage.tsx:79-91`); der gewählte Eintrag ist nur an der Farbe erkennbar. Gleiches Muster in den `admin-list`-Listen, z. B. `TeamTab.tsx:17`. | fluent2-web `accessibility.md` (Zustand nicht nur über Farbe, ARIA) | `aria-current="true"` am gewählten Eintrag | ND – erledigt in Slice 43 | S |
| B34 | Standorte als Checkbox-Gruppe unter `<h3>` (`StaffPage.tsx:240`), ohne `<fieldset>`/`<legend>`: der Gruppenname hängt nicht an den Kontrollkästchen. | fluent2-web `buttons-and-selection.md` (Gruppen benennen); masken.md Abschnitt 5 (`fieldset` mit `legend`) | `<fieldset><legend>Standorte</legend>`, `HelpButton` in die Legende | V – erledigt in Slice 29 | S |
| B35 | „Zurückziehen“ (`StaffPage.tsx:608`, `ghost danger`) widerruft eine Einladung ohne Rückfrage und ohne Fehlerbehandlung: das Ergebnis von `api.DELETE` wird nicht geprüft, bei einem Fehler geschieht still nichts. | fluent2-web `dialogs-and-messages.md` (Rückfrage vor Datenverlust), `content-design.md`; Brief 5.27 | Rückfrage über den gemeinsamen Dialog (B2), Fehlertext am Knopf | ND – erledigt in Slice 39 | S |
| B36 | Anlegen, Einladen und Speichern sind lose `div` mit Knöpfen, kein `<form>` (`StaffPage.tsx` InviteForm, AccountForm, MemberForm): Enter in einem Feld löst nichts aus (Brief 5.16: Enter löst den Standardknopf aus). Die primären Knöpfe sind `disabled`, ohne den Grund zu nennen, obwohl der Brief (design.md Z. 202) einen Hinweis mit Grund verlangt. „Startpasswort (mindestens 8 Zeichen)“ hat einen Klammerzusatz statt Hilfetext (masken.md R6). Gleiches Muster in `AccountingPage.tsx:235` und `PayrollPage.tsx:309`. | fluent2-web `inputs.md`, `buttons-and-selection.md` (Enter, deaktiviert mit Grund); Brief 5.16, design.md Z. 202; masken.md R5, R6 | `<form onSubmit>` um die Gruppe, Knopf mit `aria-disabled` und `aria-describedby` auf einen Hilfetext („Name und E-Mail fehlen.“) | ND – erledigt in Slice 48 | M |

#### Kasse (`pos/*`)

| Nr. | Befund | Regel | Vorschlag | Einstufung | Aufwand |
| --- | --- | --- | --- | --- | --- |
| B37 | `parseMoney` (`pos/money.ts:8-11`) macht aus ungültiger Eingabe stillschweigend 0 (`"12,5x"` ergibt 0, `"12.345,50"` ergibt 0, `"1e3"` ergibt 1000) und rundet eine dritte Nachkommastelle weg (`round2`). Die Betragsfelder in `BillPanel.tsx:306-334`, `:375-409` und `DayPanel.tsx:78,98` zeigen keinen Fehler; ein Tippfehler wird zu einer Zahlung über 0 € bzw. zu 0 € gezähltem Bestand. | fluent2-web `inputs.md` (Geld: Fehlertext statt stummes Runden, leer ist nicht null); Brief 5.20 | Gemeinsames `MoneyField` mit Parser nach 5.20 (tolerant lesen, beim Verlassen normalisieren, Fehlertext bei ungültig oder zu vielen Stellen); löst auch B7 | V – erledigt in Slice 12 | M |
| B38 | Warnungen der Kasse (`PosPage.tsx:123-127`, u. a. Signaturgerät ausgefallen über `signatureDeviceFailedSince`) sind `<p>` ohne `role`; sie erscheinen beim Laden und werden nicht angesagt. | fluent2-web `dialogs-and-messages.md` (Meldungen mit `role="alert"` bzw. `status`) | `role="alert"` bei Ausfall des Signaturgeräts, sonst `role="status"` | V – erledigt in Slice 25 | S |
| B39 | Gleichlautende Knöpfe: je Zeile „Abrechnen“ (`PosPage.tsx:141`, `:156`) ohne Bezug zum Termin. Auswahl der offenen Rechnung und der Belege sind `chip`-Knöpfe, deren gewählter Zustand nur über die Klasse `selected` läuft (`PosPage.tsx:165`, `:242`). | fluent2-web `accessibility.md` (Namen eindeutig), `buttons-and-selection.md` | `aria-label` mit Behandlung und Gast; `aria-pressed` bzw. `aria-current` an den Chips | ND – erledigt in Slice 43 | S |
| B40 | „Tagesabschluss“ (`DayPanel.tsx:101`, Z-Bericht) löst mit einem Klick aus, ohne Rückfrage mit dem gezählten Bestand; ob der Abschluss umkehrbar ist, ist aus dem Code nicht erkennbar (zu klären). Das Feld „Gezählter Bestand“ hat dieselbe Stummheit wie B37. | fluent2-web `dialogs-and-messages.md` (Rückfrage vor Unumkehrbarem); Brief 5.27, ADR 0010 | Rückfrage „Tagesabschluss mit gezählt 245,50 €?“ über den gemeinsamen Dialog | ND – erledigt in Slice 39 | S |
| B41 | „Stornieren“ ist im gesperrten Fall `disabled`, der Grund steht nur im `title` (`ReceiptView.tsx:270-272`); ein deaktivierter Knopf nimmt weder Fokus noch Hinweis auf Tastatur oder Touch auf. | fluent2-web `buttons-and-selection.md` (deaktiviert mit Grund: `aria-disabled` und `aria-describedby`); Brief Z. 202 | `aria-disabled` und sichtbarer Hilfetext unter dem Knopf | ND – erledigt in Slice 43 | S |
| B42 | „Rest bar“ und „Rest Karte“ schreiben `String(cashDue)` ins Feld (`BillPanel.tsx:425`, `:428`): „12.5“ statt „12,50“; die Felder normalisieren beim Verlassen nicht (Brief 5.20). | fluent2-web `inputs.md` (Anzeigeformat zurückschreiben); Brief 5.20 | über `MoneyField` (B37) lösen | ND – erledigt in Slice 46 | S |
| B43 | „Bar zu zahlen“ und „Rückgeld“ (`BillPanel.tsx:459-463`) ändern sich beim Tippen, stehen aber in keiner Live-Region; an der Kasse mit Screenreader nicht wahrnehmbar. | fluent2-web `accessibility.md` (Live-Regionen) | `<p role="status">` um die beiden Werte | ND – erledigt in Slice 46 | S |
| B44 | Leerzustand ohne Weg: „Noch keine Kasse eingerichtet.“ (`PosPage.tsx:103`). | impeccable (Leerzustände sagen, was zu tun ist); fluent2-web `content-design.md` | Satz um den Ort ergänzen („Stammdaten, Zahlungen“) und Link dorthin | ND – erledigt in Slice 44 | S |

#### Dienstplan und Zeiten (`roster/RosterPage.tsx`, `time/TimesheetPage.tsx`)

| Nr. | Befund | Regel | Vorschlag | Einstufung | Aufwand |
| --- | --- | --- | --- | --- | --- |
| B45 | Zellenknopf mit `aria-label="Name Tag"` (`RosterPage.tsx` im `roster-cell`, ca. Z. 190): der Name ersetzt den Inhalt, die Schichtzeiten und Warnungen der Zelle fehlen dem Screenreader; das Raster ist damit nicht lesbar. | fluent2-web `accessibility.md` (Name enthält den sichtbaren Text, WCAG 2.5.3) | `aria-label` entfernen und `<span class="visually-hidden">Name, Tag:</span>` voranstellen | V – erledigt in Slice 25 | S |
| B46 | Warnungen nur im `title`: Zellenwarnung (`RosterPage.tsx` ca. Z. 196) und Zeilenwarnung „⚠ Stunden“ (`td title=`, ca. Z. 205); Tastatur, Touch und Screenreader erfahren den Text nicht. | fluent2-web `navigation.md` (`title` ist kein Tooltip) | Warntext sichtbar in der Zelle bzw. unter der Tabelle, `title` entfällt | V – erledigt in Slice 25 | S |
| B47 | „Verwerfen“ (`RosterPage.tsx:126`, `ghost danger`) wirft alle Entwürfe der Woche ohne Rückfrage weg; die Windows-App fragt hier (F20). | fluent2-web `dialogs-and-messages.md`; Brief 5.27, F20 | Rückfrage mit Zahl der Entwürfe über den gemeinsamen Dialog | V – erledigt in Slice 14 | S |
| B48 | Die Seitenpanels `aside.panel` (`RosterPage.tsx` DayEditor, `TimesheetPage.tsx` DayPanel) nehmen den Fokus beim Öffnen nicht auf, schließen nicht mit Esc und geben den Fokus nicht an die Zelle zurück; der Schließen-Knopf ist „×“. Die Panels im Kalender wurden dazu nicht geprüft. | fluent2-web `dialogs-and-messages.md`, `accessibility.md` (Fokus beim Öffnen und Schließen, Esc); Brief 5.12 | Gemeinsame Komponente `SidePanel` (Fokus auf Überschrift, Esc, Fokus zurück) | ND – erledigt in Slice 42 | M |
| B49 | Wiederholte Steuerelemente ohne Bezug: je Dienst „Entfernen“, „Standort“, „Beginn“, „Ende“ (`RosterPage.tsx` DayEditor), je Zeitabschnitt „Ändern“ und „Verlauf“ (`TimesheetPage.tsx` DayPanel). | fluent2-web `accessibility.md` (Namen eindeutig) | `aria-label` mit Dienstnummer bzw. Zeit („Dienst 2 entfernen“) | ND – erledigt in Slice 42 | S |
| B50 | Knopf „OK“ zum Schließen der Veröffentlichungsmeldung (`RosterPage.tsx:154`). | fluent2-web `content-design.md` (nie „OK“, ein Verb) | „Schließen“ | ND – erledigt in Slice 44 | S |
| B51 | `div.calendar-scroll` (`RosterPage.tsx:163`) scrollt waagerecht, ist aber nicht fokussierbar; die Tabelle ist per Tastatur nur über die Zellenknöpfe zu erreichen. | fluent2-web `accessibility.md` (scrollbare Bereiche fokussierbar, axe `scrollable-region-focusable`) | `tabIndex={0}`, `role="region"`, `aria-label="Dienstplan"` | ND – erledigt in Slice 42 | S |
| B52 | Zeit nachtragen (`TimesheetPage.tsx` RecordEditor): „Grund (wird protokolliert)“ ist für den Server Pflicht, aber nicht gekennzeichnet und nicht vorab geprüft; Ende vor Beginn wird still als „über Mitternacht“ gedeutet (`body()`); „Entfernen“ eines Abschnitts ohne Rückfrage. | fluent2-web `inputs.md` (Pflicht, abhängige Felder), `dialogs-and-messages.md`; masken.md R7 | Pflichtstern, Hinweis „Endet am Folgetag“, Rückfrage vor dem Entfernen | ND – erledigt in Slice 39 | S |

#### Buchhaltung und Lohn (`accounting/AccountingPage.tsx`, `payroll/PayrollPage.tsx`)

| Nr. | Befund | Regel | Vorschlag | Einstufung | Aufwand |
| --- | --- | --- | --- | --- | --- |
| B53 | Eine einzige Meldung ganz oben (`AccountingPage.tsx:113-117`, `PayrollPage.tsx:109-113`, beide `role="status"`) dient allen Aktionen der Seite, auch Fehlern wie „BU-Schlüssel fehlt“ (`AccountingPage.tsx:68`); wer unten „Speichern“ drückt, sieht nichts. Fehler gehören in `role="alert"`. | fluent2-web `dialogs-and-messages.md` (Meldung am Ort der Aktion, Fehler assertiv); meldungen.md | Meldung unter die jeweilige Aktionsleiste, Fehler mit `role="alert"` | V – erledigt in Slice 25 | S |
| B54 | Export-Links (`a.button download`, `AccountingPage.tsx:137-148`, `PayrollPage.tsx:136-143`) ohne Ladezustand und Fehlerweg. Die Lohn-Seite lässt den Export zu, obwohl die Vorschau Warnungen zeigt; die Buchhaltung prüft vor DATEV (`AccountingPage.tsx:67-77`). | fluent2-web `dialogs-and-messages.md` (Fortschritt, Fehler); impeccable (Fehlerzustände) | Vor dem Export dieselbe Prüfung wie bei DATEV; Zustand „Wird erstellt …“ | ND – erledigt in Slice 44 | S |
| B55 | `<input type="month">` (`PayrollPage.tsx:125`): in Firefox und Safari (Desktop) ohne Auswahl und ohne Formathinweis; die Verwaltung läuft auch im Browser. | modern-web-guidance Formulare (Eingabetypen mit Rückfall) | Jahr und Monat als zwei `select` oder Formathinweis „JJJJ-MM“ | ND – erledigt in Slice 44 | S |
| B56 | Zahlenspalten: die Köpfe stehen links, die Werte rechts (`td.amount`): `PayrollPage.tsx:162-166` („Menge“), `AccountingPage.tsx` Buchungstabelle („Soll“, „Haben“, „Betrag“, „USt“), `CostsPage.tsx:128-130`. Der Brief (W14) verlangt Zahlenspalten rechts samt Kopf. | Brief 11.2 W14; fluent2-web `data-display.md` | `th className="num"` an den Zahlenspalten (zusammen mit `scope`, B4) | V – erledigt in Slice 01 | S |
| B57 | „Auf Standard zurücksetzen“ (`AccountingPage.tsx:326`) entfernt die eigene Zuordnung des Landes ohne Rückfrage. | fluent2-web `dialogs-and-messages.md`; Brief 5.27 | Rückfrage mit Folge („Die eigene Zuordnung für AT geht verloren.“) | ND – erledigt in Slice 39 | S |

#### Betreiber (`operator/OperatorApp.tsx`)

| Nr. | Befund | Regel | Vorschlag | Einstufung | Aufwand |
| --- | --- | --- | --- | --- | --- |
| B58 | Die Abo-Stufe wechselt sofort im `onChange` des `<select>` (`OperatorApp.tsx:233`). Bei geschlossenem Auswahlfeld löst jede Pfeiltaste einen Wechsel aus (Chrome und Edge unter Windows), also mehrere Abo-Wechsel nacheinander; keine Rückfrage, kein Erfolgshinweis (das Abo bestimmt Kosten und Module des Kunden). | fluent2-web `inputs.md` (Auswahl mit Wirkung bestätigen), `dialogs-and-messages.md` | Auswahl plus Knopf „Stufe ändern“ mit Rückfrage und `role="status"` | V – erledigt in Slice 26 | S |
| B59 | Der Fokus geht verloren, wenn der Auslöser ersetzt wird: „Spa sperren“ zum Formular (`OperatorApp.tsx:312` zu `:291`, kein Fokus ins Feld „Grund“), „Lösen“ zu „Wirklich lösen“ (`:345` zu `:337`). | fluent2-web `accessibility.md` (Fokus nach Austausch) | `autoFocus` auf das Grundfeld; Fokus auf „Wirklich lösen“ bzw. zurück auf den Auslöser | ND – erledigt in Slice 43 | S |

#### Schrift, Zustände, Mikrodetails (`index.css`, Querschnitt)

| Nr. | Befund | Regel | Vorschlag | Einstufung | Aufwand |
| --- | --- | --- | --- | --- | --- |
| B62 | Die Hausschrift Inter wird im Web nicht ausgeliefert: `index.css:49` nennt `Inter` zuerst, es gibt aber kein `@font-face`, und `web/public` enthält nur `favicon.svg`. Die Verwaltung zeigt im WebView2 damit Segoe UI Variable, die Windows-App die eingebettete Inter (`.WithInterFont()`). Der Brief (Z. 109) nennt „damit App und Web dieselbe Schrift zeigen“ als Zweck; Gewicht 500 hat in Segoe UI keine echte Stufe. | Brief Abschnitt 3 (Schriftfamilie, Gewichte); impeccable (Typografie) | Inter als lokale Variable-WOFF2 (Subset latin) mit `@font-face`, `font-display: swap` und `<link rel="preload">`; im Kundenbereich bewusst die Systemschrift lassen und das notieren | V – erledigt in Slice 27 | S |
| B63 | `index.css:1538` setzt `font-weight: 700` (`.receipt-lines .total td`); der Brief erlaubt nur 400, 500 und 600 (Z. 111). | Brief Abschnitt 3 (Schriftgewichte) | `600` | ND – erledigt in Slice 46 | S |
| B64 | Der Brief verlangt für jede Komponente die Zustände normal, Hover, gedrückt, Fokus, deaktiviert (design.md Z. 202). In `index.css` gibt es keine Regel mit `:active`, in `stock.css` eine; Knöpfe haben nur Hover (`index.css:184`) und Fokus. Hover ist nicht auf Geräte mit Maus beschränkt (`@media (hover: hover)` kommt nicht vor): auf Touch bleibt Hover nach dem Tippen hängen. | Brief Z. 202; emil-design-eng (Knöpfe reagieren auf Druck, `scale(0.97)`; Hover nur mit `hover: hover`) | `button:active:not(:disabled) { background: var(--surface-3); transform: scale(0.98) }` mit 100 ms; Hover-Regeln in `@media (hover: hover)` | V – erledigt in Slice 02 | S |
| B65 | Toasts kommen mit Einblendung (`stock.css:949`, 0,16 s), verschwinden aber abrupt (`BusinessInfoPage.tsx:77` entfernt nach 5 s ohne Ausblenden). Hilfe- und Begleitleiste erscheinen ohne Übergang; das passt zu Strg+Leertaste (Tastaturaktion, keine Animation) und ist richtig. | emil-design-eng (Aus- und Einblenden, nichts verschwindet aus dem Nichts); fluent2-web `dialogs-and-messages.md` (Toast) | Ausblenden 120 ms per `transition`; zusammen mit B5 bei reduzierter Bewegung abschalten | ND – erledigt in Slice 46 | S |
| B66 | Allgemeine Fehlertexte ohne Ursache und Weg: „Das hat nicht geklappt.“ (`SenderRulesSection.tsx:72`, `AppointmentPanel.tsx:185`, `AreaPanel.tsx:61`); `CalendarPage.tsx:36` ergänzt immerhin „Bitte erneut versuchen.“ | fluent2-web `content-design.md` (was, warum, zwei Wege) | Problemtext des Servers (`problemText`) bevorzugen, Fallback mit Weg („Prüfen Sie die Verbindung oder versuchen Sie es erneut.“) | ND – erledigt in Slice 44 | S |
| B67 | Beschriftungen mit Pluszeichen statt Verb: „+ Termin“ (`CalendarPage.tsx:284`), „+ Gutschein“ (`BillPanel.tsx:423`), „+ Dienst“ (`RosterPage.tsx` DayEditor). | fluent2-web `content-design.md` (Knöpfe mit Verb) | „Termin anlegen“, „Gutschein hinzufügen“, „Dienst hinzufügen“ | ND – erledigt in Slice 44 | S |
| B68 | Erklärungen stehen in 67 Fällen nur im `title` (z. B. `CalendarPage.tsx:263` Live-Stand, `DayGrid.tsx` Terminkarten): native Tooltips, verzögert, ohne Tastatur- und Touchzugang, nicht im Stil von Brief 5.22. | fluent2-web `navigation.md` (Tooltip auch auf Fokus, `title` allein reicht nicht); emil-design-eng (Tooltip-Verzögerung) | Gemeinsame `Tooltip`-Komponente (Popover API, 250 ms, Fokus), wo der Text mehr als ein Name ist | ND – erledigt in Slice 45 | M |
| B69 | Vier Kopien derselben Palette: `booking.css:4-34`, `myday.css:5-45`, `operator.css:4-35` (Kommentar „Farben wie im Kundenbereich“) und `stock.css:8-26`; der Token-Test `designTokens.test.ts` liest nur `index.css`. Eine Markenänderung müsste fünfmal nachgezogen werden. | Brief Abschnitt 2 (Tokens als einzige Quelle) | Gemeinsame `tokens.css` für die schlanken Oberflächen (ohne Import aus der Team-Oberfläche), Test auf alle Dateien erweitern | ND – erledigt in Slice 41 | M |
| B70 | Ladezustände sind reiner Text („Lade Zugänge …“, `StaffPage.tsx` nach `useEffect`; „Lade Dienstplan …“, `RosterPage.tsx` vor der Tabelle), ohne Gerüst; das Layout springt, wenn Daten kommen. | Brief 5.25 (Skeleton); impeccable (Ladezustände) | Skeleton für Listen und Tabellen | B (design.md 11.4: Fortschrittsbalken und Skeleton offen) | M |

### Kundenbereich, zweiter Durchgang (`booking/BookPage.tsx`)

| Nr. | Befund | Regel | Vorschlag | Einstufung | Aufwand |
| --- | --- | --- | --- | --- | --- |
| B60 | Fehlerzustände ohne Weg heraus: `<Alert>{t.errorOffline}</Alert>` (`BookPage.tsx:131`) und `t.errorGeneric` bei der Verfügbarkeit (`:493`); `failed` wird nie zurückgesetzt, auch nach Wechsel der Behandlung nicht. Außer dem Neuladen der Seite gibt es keinen Knopf. Sackgasse auch bei „keine online buchbaren Behandlungen“. | impeccable (Fehlerzustände mit Weg); fluent2-web `content-design.md` | „Erneut versuchen“ (setzt `failed` zurück und lädt neu); bei fehlenden Behandlungen Telefon oder anderen Standort anbieten | ND – erledigt in Slice 44 | S |
| B61 | Die Fehlerübersicht nennt nur die Zahl (`t.formErrors(n)`, `BookPage.tsx:732`) und fokussiert den Container `summaryRef` (`:657`) statt des ersten ungültigen Feldes (masken.md R8). Der Absenden-Knopf trägt `disabled` und `aria-disabled` zugleich (`BookPage.tsx` am Ende von `DetailsStep`): beim Senden geht der Fokus verloren, anders als im Rest des Bereichs. Im `catch`-Zweig bekommt der Container keinen Fokus. | masken.md R8; fluent2-web `inputs.md` (Fehlertext mit Weg, Fokus); WCAG 3.3.1 | Fehlerübersicht als Liste mit Sprunglinks oder Fokus ins erste Feld; nur `aria-disabled` | ND – erledigt in Slice 43 | S |

### Zusammenfassung zum zweiten Durchgang

Neu sind 38 Befunde (B33 bis B70). Einstufung: 11 verstößt (B34, B37, B38, B45, B46, B47, B53, B56, B58, B62, B64), 26 weicht ab, nicht dokumentiert, 1 bekannt (B70). Nach Skill: fluent2-web 26, impeccable 9 (B44, B60, B61, B62, B63, B66, B67, B69, B70), emil-design-eng 2 (B64, B65), modern-web-guidance 1 (B55). B26 ist zurückgezogen. Der Kundenbereich bleibt bis auf B60 und B61 ohne Befund, `myday/*` ohne Befund.

Nicht geprüft im zweiten Durchgang: Laufzeit, Bundle, Screenreader (siehe Liste oben); Panels im Kalender (`AppointmentPanel`, `BookingPanel`, `GroupBookingPanel`, `WaitlistPanel`, `AreaPanel`, `GapPanel`), `TeamTab`, `LocationsTab`, `TreatmentsTab`, `PaymentsTab`, `IntakeTab`, `InboxPage` nur punktuell gelesen; `Vouchers`, `Waitlist`, `MyAppointmentsPage`, `AnamnesisPage`, `SignInPage` im Kundenbereich nur per Suche.
