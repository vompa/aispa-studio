# Arbeitsanweisung: Wochenplan überarbeiten – Lesbarkeit, Besetzung über den Tag, Ausgaben, Drucken

**Für:** Claude Code im Repository `vompa/aispa-studio`
**Stand:** 26.11.2026, Auftrag des Auftraggebers nach Durchsicht des Wochenplans (Team → Wochenplan, Standort Graz Zentrum, Woche ab 23.11.).
**Ablauf:** Code Guardian nach [docs/arbeitsweise.md](../arbeitsweise.md) Abschnitt 9: **fünf** Aufgaben T-NNN (nächste freie Nummern prüfen; T-092 ff. sind für den Berichte-Ausbau vorgesehen): Aufgabe 1 „Lesbarkeit“ (Pakete A bis D, Modell sonnet), danach Aufgabe 2 „Besetzung über den Tag“ (Abschnitt 4, Modell sonnet); Aufgabe 3 „Export gedreht“ (Abschnitt 5) ist unabhängig; Aufgabe 4 „Kopfzeile: Menü Ausgabe oben rechts“ (Abschnitt 6) läuft nach Aufgabe 1; Aufgabe 5 „Druckdialog“ (Abschnitt 7) ist unabhängig und als Fehler im Betrieb zuerst dran. Empfohlene Reihenfolge: 5, 1, 4, 2, daneben 3. Je Aufgabe eine Sitzung im Worktree und ein Squash-Commit. Die Aufgaben 1, 2 und 4 ändern `src/AispaStudio.Desktop/Views/RosterPlanView.axaml`, deshalb diese drei nacheinander und nie parallel zu einer anderen Aufgabe an dieser Datei. Befundnummer: nächste freie Ä-Nummer (zuletzt gesehen Ä387, vorher prüfen).

---

## 1. Ziel

Wer den Wochenplan öffnet, soll in einem Blick sehen: wer ist wann **hier** im Dienst, wer fehlt, was ist heute. Heute konkurrieren dafür zu viele Farben, und die wichtigste Angabe ist die kleinste.

**Befunde am Bild vom 26.11.2026 (hell, Fensterbreite rund 2000 px):**

1. **Farbe ohne Aussage.** Jede Zeile hat eine eigene Pastellfläche (Personenfarbe zu 22 % in Weiß, `RosterChip`), dazu die türkise Spalte von heute, türkise Namen und der rote Balken „Krank“. Die Personenfarbe im Chip sagt nichts, was die Zeile nicht schon sagt. Der Chip einer Person mit brauner Farbe hebt sich kaum vom Fensterhintergrund `bg` ab.
2. **Das Wichtigste ist am kleinsten.** Dienstzeiten stehen in 12/400 (`Classes="num small"`), Namen und Besetzung in 14, Spaltenköpfe in 13/600.
3. **Dienst an einem anderen Standort sieht aus wie Dienst hier.** Eine Person mit „10:00–18:00 Salzburg Altstadt“ an allen Tagen trägt dieselbe gefüllte Fläche wie die Dienste am gewählten Standort; die Zeile „Besetzung“ zählt sie zu Recht nicht mit, das Bild widerspricht also der Zahl.
4. **Heute ist zerstückelt.** Die Spalte von heute ist je Zeile ein eigenes abgerundetes Kästchen (`AccentSoftBrush`, Radius 8, Rand 2) statt eines durchgehenden Streifens; der Kopf unterscheidet sich nur durch Farbe und Gewicht.
5. **Rollbalken, wo keiner sein darf (Auftraggeber, 26.11.2026: „Eigentlich sollte gar kein Scrollbalken angezeigt werden“).** In beiden Bildern ist die Tabelle breiter als die Planfläche: die Spalte „So.“ endet rund 65 bis 70 Bildpunkte hinter dem rechten Rand, obwohl 176 + 728 px Mindestbreite längst Platz hätten. Der waagrechte Rollbalken liegt dabei **über** der Zeile „Besetzung“ und verdeckt sie zur Hälfte (zweites Bild, Fenster 2000 × 1196); die Höhe der Tabelle rechnet den Balken nicht ein. **Vermutung zur Breite, nicht gemessen:** Der `ScrollViewer` misst die `UniformGrid` mit unendlicher Breite, alle sieben Spalten nehmen die Breite des längsten Chips an („10:00–18:00 Salzburg Altstadt“), und `TextWrapping="Wrap"` greift dabei nicht.
6. **Kleinigkeiten:** „Krank“ steht linksbündig zwischen zentrierten Zeiten; die Stunden „32 / 40 h“ stehen in `fg` statt als Untertitel in `fg-2`; „Besetzung“ ist `small muted`, obwohl es die Summenzeile ist; die Tabelle steht nach dem Bild direkt auf `bg` (prüfen: `PlanHost` in `Views/TeamView.axaml:38`).

**Erfolg ist erreicht, wenn:**

1. Befund 5 ist mit einem Test nachgestellt (erst rot) und behoben: Der Wochenplan zeigt **keinen** waagrechten Rollbalken, bei jeder Fensterbreite ab der Mindestbreite des Hauptfensters (`MainWindow.axaml`: `MinWidth="1100"`; nach Abzug von Navigation und Namensspalte bleiben überschlagen rund 110 px je Tag, nachmessen); die sieben Spalten teilen sich den vorhandenen Platz. Die Zeile „Besetzung“ ist immer ganz sichtbar, nichts liegt über ihr.
2. Im Plan kommen nur noch diese Farben vor: `accent`/`accent-soft` (heute, Namen als Link, Urlaub, Entwurfsrand), `danger` (Krank), `warn` (Schulung, Warnungen), der Punkt in der Personenfarbe vor dem Namen. Alles andere ist neutral.
3. Die Dienstzeit steht in 14 px mit Ziffern gleicher Breite; ein Dienst an einem anderen Standort ist ohne Lesen des Textes als „nicht hier“ erkennbar.
4. Heute ist ein durchgehender Streifen vom Kopf bis zur Besetzung, und der Kopf nennt „Heute“ als Wort.
5. Hell und dunkel, als Inhaberin und als Rezeptionistin ohne Planungsrecht je ein Bild im PR-Protokoll; Hilfebilder neu; volle Prüfkette am Rundenende grün.

**Nicht Teil von Aufgabe 1 (im PR-Protokoll als „bewusst offen“ nennen):** die Kopfzeile mit den Knöpfen über dem Plan (Aufgabe 4) und das Drucken (Aufgabe 5); Kennzeichnung von Unter- oder Überstunden („32 / 40 h“ in `warn`); Warnung bei Unterbesetzung (es gibt keine Mindestbesetzung); der Tages-Editor (`RosterDayEditorView`); der Aushang als PDF; das Web-Dashboard. Fällt dabei etwas auf, nur nennen.

## 2. Leitplanken

- `CLAUDE.md` gilt vollständig. Vor dem ersten Edit [docs/design.md](../design.md) Abschnitte 2, 3, 4.1, 4.7, 5.7, 5.8, 6 Punkt 8 (Dienstplan, um Zeile 553), 8, 9, 10 lesen, dazu [docs/gestaltung/masken.md](../gestaltung/masken.md) (Kurz-Checkliste abhaken) und den Skill `fluent2-avalonia`.
- **Erst der Brief, dann der Bau** (Brief Abschnitt 10, letzter Punkt): Paket B ändert zuerst `docs/design.md`, danach die Ansicht genau nach diesen Werten. Keine Farbe, Größe oder kein Gewicht außerhalb der Tabellen; keine festen Farben in AXAML.
- **Angekündigter Wegfall (vom Auftraggeber am 26.11.2026 freigegeben):** Dienst-Chips tragen nicht mehr die Farbe der Person. Die Personenfarbe bleibt als Punkt vor dem Namen. Sonst fällt nichts weg; fällt beim Bauen doch etwas weg, vorher fragen.
- **Die App zeichnet nur.** Keine neue Regel in der App; „anderer Standort“ ist dieselbe Unterscheidung `entry.LocationId == locationId`, die `EntryText` und `Coverage` in `RosterPlanViewModel` schon treffen. Aufgabe 1 ändert die API nicht (Aufgaben 2 und 3 ändern sie, siehe dort).
- **Dieselbe Ansicht dient der Personenkarte** (`IsCard`, Reiter „Arbeitszeit“, Überschrift „Dienstplan und Abwesenheit“): jede Änderung dort mitprüfen; die Karte bekommt keine zweite Kartenfläche.
- **Screenreader-Namen bleiben** (`AutomationName` der Zelle nennt Zeit und Standort wie heute); für heute kommt das Wort dazu.
- **Tests zuerst, mit Budget:** höchstens 5 neue Tests, höchstens 20 Minuten Testlauf je Paket, Läufe über 2 Minuten im Hintergrund mit Protokoll in `.test-logs/`. `date` am Anfang und vor jedem langen Lauf; nach 60 Minuten je Paket stoppen und berichten. Kein Test für eine gewählte Farbe oder Größe an sich (das wäre ein Änderungsmelder); geprüft werden Kontrast, Zustände und Breite.
- **Bildlauf und Prüfkette nie parallel.**
- **Fragen** sammeln und weiterarbeiten, wo es ohne Antwort geht.

## 3. Pakete

| Nr. | Paket | Dateien, die sich ändern dürfen |
|---|---|---|
| A | Befund: Breite und Rollbalken (30 Minuten, kein Umbau) | `tests/AispaStudio.Desktop.Tests/RosterWeekLayoutTests.cs` |
| B | Brief, Chips, Schrift | `docs/design.md`, `Views/RosterPlanView.axaml`, `ViewModels/RosterPlanViewModel.cs`, `Controls/RosterChip.cs` (entfällt), `Themes/Colors.axaml`, `RosterWeekLayoutTests.cs`, `DesignTokenTests.cs` |
| C | Heute, Karte, Kopf, Besetzung, Breite | `Views/RosterPlanView.axaml`, `Views/RosterPlanView.axaml.cs` (nur wenn die Lage des Tages-Popovers nachzieht), `ViewModels/RosterPlanViewModel.cs`, `Views/TeamView.axaml` (nur für die Kartenfläche), `RosterWeekLayoutTests.cs` |
| D | Hilfe, Bilder, Doku | `web/src/help/content/seite-roster.md`, `dienstplan-*.md`, Hilfebilder, `docs/umsetzung/pr-protokoll.md`, `docs/stand-und-offene-punkte.md`, `docs/umsetzung/handtests.md` |

Je Paket ein lokaler Commit; Paket D zuletzt.

## Paket A: Befund Breite

Ein Test in `RosterWeekLayoutTests`: Woche mit einer Person, die an jedem Tag an einem **anderen** Standort mit langem Namen Dienst hat, Ansicht in 1280 px Breite gemessen und angeordnet; erwartet: die Breite von `RosterTable` ist nicht größer als die sichtbare Breite des `ScrollViewer`, und die Unterkante von `RosterCoverage` liegt innerhalb der sichtbaren Höhe (kein Balken darüber). Dieselbe Messung einmal in der Mindestbreite des Hauptfensters. Ergebnis „bestätigt“ oder „widerlegt“ mit den gemessenen Zahlen ins PR-Protokoll. Ist die Vermutung widerlegt, die wahre Ursache des Rollbalkens suchen (Zeitrahmen bleibt 30 Minuten) und berichten; behoben wird in Paket C.

## Paket B: Brief, Chips, Schrift

**Brief zuerst (`docs/design.md`):**

- 5.8, Zeile „Dienst-Chip“ ersetzen: Höhe 28 · Radius 999 · Innenabstand 4/8 · Schrift Text 14/400 mit Ziffern gleicher Breite, für genau diesen Tag geplant 600 · Fläche `surface-3`, Text `fg`, Rahmen 1 px `line` (die Fläche allein hebt sich mit 1,2:1 kaum von `surface` ab); in der Spalte von heute Fläche `surface`, Rahmen `accent-line`. Füllt die Breite der Zelle, Text zentriert. „Festgelegt 26.11.2026, Auftraggeber: Farbe trägt im Dienstplan nur Bedeutung, die Personenfarbe steht als Punkt vor dem Namen.“
- 5.8, neue Zeile „Dienst an einem anderen Standort (Dienstplan)“: Radius 8 · Innenabstand 2/8 (Werte aus 4.1) · zwei Zeilen: Zeit 14/400, darunter Standort 12/400 mit Symbol `Location` 12 davor (Größe wie im Info-Chip in 5.8; 4.7 nennt sonst 16) · keine Fläche, Text und Symbol `fg-2`, Rahmen 1 px `line-strong` (in der Spalte von heute `accent-line`). Der Standort kürzt mit „…“, der volle Name steht im Tooltip.
- 5.8, Zeile „Abwesenheitsbalken“ ergänzen: über einen Tag zentriert, über mehrere Tage linksbündig. Werte bleiben, mit einer Ausnahme: „Frei“ (heute Balken in `surface-3`/`fg-2`, `Border.bar` aus `DayOff`) wird Text ohne Fläche in `fg-2`, Stufe Klein, zentriert – sonst sähe ein freier Tag aus wie der neue Dienst-Chip.
- Abschnitt 6 Punkt 8 (Dienstplan) im selben Zug berichtigen, sonst widerspricht der Brief dem Bau: „geplant / Soll h“ in `fg-2` 12; die Spalte von heute als durchgehender Streifen mit „Heute ·“ im Kopf; „Besetzung“ als Summenzeile (Zahl 14/600, die 0 in `fg-2`/400); der Verweis „Farben je Farbschema in 2.4, T-039“ entfällt mit dem gestrichenen Unterabschnitt.
- 2.4, Unterabschnitt „Dienst-Chip im Dienstplan (nur App, T-039, Ä372)“ durch einen Satz ersetzen, der auf 5.8 verweist und den Wegfall der Token `roster-chip-base`, `roster-chip-ink` und der Mischung mit Datum nennt.

**Bau:**

- `RosterChipViewModel` trägt statt der Personenfarbe: Zeit, Standort (leer, wenn hier), `IsPlanned`, `IsElsewhere`. `EntryText` bleibt für `Lines` und den Screenreader-Namen, wie es ist.
- Vorlage `RosterCellContent`: Chip als gewöhnlicher `Border` mit Stilklassen (`chip`, `chip.elsewhere`) in `UserControl.Styles`; Zeit `TextBlock` ohne `small`, mit `num`. Der Standort in der zweiten Zeile mit `TextTrimming="CharacterEllipsis"` und Tooltip (`ui:ToolTipBehavior.Reachable`).
- Abwesenheitsbalken: Text bei `Span == 1` zentriert.
- Stunden neben dem Namen (`RosterHours`): `num small secondary`.
- **Selbst verwaist, deshalb entfernen:** `Controls/RosterChip.cs`, die Klasse `RosterChipColors`, die Brushes `RosterChipBaseBrush` und `RosterChipInkBrush` in beiden Themes von `Themes/Colors.axaml`, die sechs Farb-Tests in `RosterWeekLayoutTests` (mit ihren Attributen Zeilen 195 bis 269) samt Hilfsfunktionen (271 bis 295; `FixedTime` ab 297 bleibt, der Karten-Test nutzt sie), die zwei Prüfungen in `DesignTokenTests` (Zeilen 63 bis 90, darunter die Zeilen, die `roster-chip-base` und `roster-chip-ink` im Brief verlangen) und den Vordergrund `RosterChipInkBrush` im Stil der Ansicht (`RosterPlanView.axaml` um Zeile 22). Vorher mit Grep sichern, dass nichts anderes sie nutzt (Stand 26.11.2026: nur diese Stellen).
- **Tests (höchstens 3):** Zelle mit Dienst hier und Zelle mit Dienst an anderem Standort unterscheiden sich in der Stilklasse und der zweiten Zeile, der Screenreader-Name bleibt gleich; Kontrast `fg` auf `surface-3` und `fg-2` auf `surface`, `surface-2` und `accent-soft` mindestens 4,5:1 in beiden Farbschemata (in `DesignTokenTests`, falls dort noch nicht gerechnet); die Zelle ohne Planungsrecht (`ContentControl.rcell`) zeigt dasselbe wie der Knopf.

## Paket C: Heute, Karte, Kopf, Besetzung, Breite

- **Breite und Rollbalken (Befund 5):** Die Spalten richten sich nach dem verfügbaren Platz, nie nach dem längsten Inhalt: sieben gleich breite Spalten über die ganze Breite der Planfläche. Der waagrechte `ScrollViewer` um `RosterTable` und die festen `MinWidth="728"` entfallen, wenn die Tabelle in der Mindestbreite des Hauptfensters lesbar bleibt: Zeit im Chip einzeilig, Standort gekürzt mit Tooltip, Balkentext gekürzt. Wird eine Spalte dafür zu schmal (unter rund 96 px für „11:00–19:00“ in 14 px plus Innenabstand), nicht stillschweigend einen Rollbalken behalten, sondern die Zahlen ins PR-Protokoll schreiben und als Frage an den Auftraggeber sammeln (Vorschlag dann: Zeit bricht hinter dem Strich in zwei Zeilen um). In keinem Fall liegt ein Balken über der Zeile „Besetzung“. Der Test aus Paket A wird grün. Das Tages-Popover (`DayHost`, Lage in `RosterPlanView.axaml.cs`) sitzt danach weiter unter seiner Zelle; einmal von Hand an erster und letzter Spalte prüfen.
- **Heute:** ein durchgehender Streifen `accent-soft` vom Spaltenkopf bis zur Zeile „Besetzung“, ohne Lücken zwischen den Zeilen und ohne Radius je Zelle (Radius 8 nur oben am Kopf und unten an der Besetzung). Kopftext „Heute · Do. 26.11.“ in `accent`, 13/600. Zellen und Besetzung von heute nennen „heute“ im Screenreader-Namen. In anderen Wochen bleibt alles neutral (Test dafür gibt es, anpassen statt neu schreiben).
- **Karte:** Im Wochenplan des Teams steht die Tabelle auf einer Karte nach Brief 5.5 (`surface`, Rahmen `line`), sofern `TeamView` sie nicht schon auf eine Fläche stellt (prüfen und im PR-Protokoll festhalten). Auf der Personenkarte keine zusätzliche Karte.
- **Kopfzeile:** Rahmen unten 1 px `line-strong` (5.7); Text bleibt Stufe Beschriftung.
- **Zeilen:** Mindesthöhe 44 (Chip 28 plus 8 oben und unten); die Trennlinien bleiben 1 px `line`.
- **Besetzung:** als Summenzeile nach 5.7: Rahmen oben 1 px `line-strong`, Bezeichnung „Besetzung“ in Stufe Beschriftung (`group`), Zahl 14/600 mit `num`; die Zahl 0 in `fg-2` und 400.
- **Tests (höchstens 2):** Kopf von heute trägt das Wort „Heute“ und der Name der Zelle nennt es; der Breiten-Test aus Paket A.
- **Prüfen am Bild:** kopflose Aufnahme des Wochenplans mit den Beispieldaten (Graz Zentrum, Woche mit Krankheitstag und Dienst an anderem Standort) in 1366 und 1920 px Breite, hell und dunkel, als Inhaberin; zusätzlich einmal als Rezeptionistin und einmal die Personenkarte. Jedes Bild ansehen: nichts abgeschnitten, kein Rollbalken, Chip in der Spalte von heute klar von der Fläche getrennt, Entwurfsrand und offener Tag (`rcell.open`) weiter erkennbar. Bilder und Befund ins PR-Protokoll.

## Paket D: Hilfe, Bilder, Doku

- Hilfetexte: `web/src/help/content/seite-roster.md` und `dienstplan-*.md` auf Sätze prüfen, die die Chip-Farbe der Person beschreiben, und anpassen; einen Satz ergänzen, woran man einen Dienst an einem anderen Standort erkennt. Texte nach [docs/gestaltung/texte.md](../gestaltung/texte.md), `textRules.test.ts` grün.
- Hilfebilder neu (`scripts/help-images.sh`): mindestens `images/app/verwaltung-team-wochenplan.png`, `verwaltung-team-wochenplan-antraege.png`, `verwaltung-team-karte-dienstplan.png`; alle Bilder suchen, die den Wochenplan zeigen.
- `docs/umsetzung/pr-protokoll.md`: Abschnitt mit Befund A, den Detailentscheidungen, den Vorher-Nachher-Bildern, dem Prüfsatz der Textregeln und „bewusst offen“; Handtestblock in `docs/umsetzung/handtests.md`; `docs/stand-und-offene-punkte.md` (Zeile zum Dienst-Chip, um Zeile 298) nachziehen; Kopfkommentar in `RosterPlanView.axaml` an den neuen Stand anpassen.
- Kein ADR: keine neue Technik und keine neue Regel, der Brief trägt die Änderung mit Datum.
- Fertigmeldung an den Auftraggeber: ein Bild vorher, eines nachher, die Liste „bewusst offen“, gesammelte Fragen.

## 4. Aufgabe 2: Besetzung über den Tag (Matrix unter dem Wochenplan)

**Anlass:** Unter dem Wochenplan bleibt bei einem kleinen Team der größte Teil der Fläche leer (Auftraggeber, 26.11.2026: „etwa eine Matrix-Ansicht“). Der Plan zeigt, **wer** an welchem Tag Dienst hat, aber nicht, **wann am Tag** das Haus dünn besetzt ist: ein Dienst 11:00–19:00 und einer 10:00–18:00 ergeben „2“ in der Besetzung, obwohl von 10 bis 11 und von 18 bis 19 Uhr nur eine Person da ist.

**Was entsteht:** unter der Tabelle, nur im Wochenplan des Teams (nicht auf der Personenkarte), ein Abschnitt „Besetzung über den Tag“ (Zwischentitel, Hilfe-Knopf): eine Matrix mit denselben sieben Tagesspalten, **bündig unter den Spalten des Plans**, und einer Zeile je volle Stunde von der frühesten Öffnung bis zum spätesten Schluss der Woche am gewählten Standort (früher oder später nur, wenn ein Dienst dort liegt). Die Zelle nennt die Zahl der Personen im Dienst **an diesem Standort** in dieser Stunde.

**Erfolg ist erreicht, wenn:**

1. Die Zahlen kommen vom Server, als zusätzliches Feld von `RosterWeek` (`src/AispaStudio.Contracts/RosterContracts.cs`) in der Antwort von `GET /locations/{locationId}/roster` (`src/AispaStudio.Api/Roster/RosterEndpoints.cs:24`; ADR 0085: nur additiv; danach `dotnet build`, `npm run gen:api`, beide Dateien committen). Basis ist derselbe Stand wie im Plan darüber (mit Entwürfen, wenn der Plan sie zeigt); Abwesenheiten und Dienste an anderen Standorten zählen nicht. Die Summe je Tag bleibt die bestehende Zeile „Besetzung“.
2. Eine geöffnete Stunde ohne Person im Dienst ist als Lücke gekennzeichnet, nie nur durch Farbe.
3. Die Matrix braucht keinen eigenen Rollbalken; ist die Seite höher als das Fenster, rollt die ganze Seite senkrecht.

**Festgelegt:**

- **Öffnungszeiten nicht neu rechnen:** `ResourcePlanner.OpeningWindows(location.OpeningHours, zone, date, closed)` mit `ClosureService.SpansAsync` verwenden (Vorbild `src/AispaStudio.Api/Scheduling/AreaVisits.cs` um Zeile 196 bis 204). Ein Tag mit Schließzeit (Betriebsurlaub, Feiertag) ist geschlossen (`off`) und nie eine Lücke.
- **Zählregel (ANNAHME, im PR-Protokoll nennen):** Eine Person zählt in einer Stunde, wenn sie darin mindestens 30 Minuten Dienst hat.
- **Brief zuerst:** neuer Eintrag in `docs/design.md` Abschnitt 5 „Besetzungsmatrix (Dienstplan)“, ausdrücklich als eigenes Bauteil und nicht als Tabelle nach 5.7 (dort Zeile mindestens 36, Zahlen rechtsbündig): Zeile 28 hoch, Stundenbeschriftung „10–11“ in Stufe Klein `fg-2` in der Namensspalte, Zahl 14/400 mit Ziffern gleicher Breite zentriert, `fg`. Flächen nur aus vorhandenen Token: außerhalb der Öffnungszeit `off` (`OffDutyBrush`) ohne Zahl; geöffnet und 0 Personen `warn-soft` (`WarnSoftBrush`) mit „0“ in `warn` 600 und Warndreieck 14 davor; sonst keine Fläche; die Spalte von heute führt den Streifen `accent-soft` aus dem Plan fort. Keine Farbskala, keine Heatmap: Farbe trägt nur die Lücke.
- **Barrierefreiheit:** jede Zelle hat einen Namen („Mi. 25.11., 18 bis 19 Uhr: 1 Person im Dienst“, bei 0 „niemand im Dienst“); die Matrix ist als Tabelle mit Zeilen- und Spaltenköpfen ausgezeichnet; nicht anklickbar, deshalb kein Hover.
- **Leerzustände:** ohne Öffnungszeiten am Standort ein Satz in `fg-2` mit dem Weg zur Einstellung statt einer leeren Matrix; ohne Personen entfällt der Abschnitt wie die Zeile „Besetzung“.
- **Hilfe und Doku (ADR 0055, 0056):** Absatz in `web/src/help/content/seite-roster.md` (was die Zahl zählt, was die Kennzeichnung heißt), Fragezeichen am Zwischentitel, Hilfebild des Wochenplans neu; PR-Protokoll, Handtestblock, `docs/stand-und-offene-punkte.md`, `docs/features.md`, Masken-Inventar.
- **Tests (höchstens 5, Logik im Server):** Dienst hier zählt, Dienst an anderem Standort und Abwesenheit zählen nicht; die 30-Minuten-Regel an einer angebrochenen Stunde; geschlossene Stunde trägt keine Zahl; geöffnete Stunde ohne Dienst ist als Lücke markiert; die App zeigt die Matrix nicht auf der Personenkarte. Endpunkt bleibt hinter dem bestehenden `RequirePermission`.

**Bewusst offen (nennen, nicht bauen):** gebuchte Termine oder Auslastung in derselben Matrix (wäre der nächste Schritt: Bedarf gegen Besetzung, Rechnung im `ResourcePlanner`); eine Mindestbesetzung je Stunde mit Warnung; Klick auf eine Lücke, der den Tag zum Planen öffnet; Räume statt Personen.

## 5. Aufgabe 3: Export des Dienstplans – Mitarbeitende oben, Tage links

**Anlass:** Auftraggeber, 26.11.2026: „bei Excel sollen die Mitarbeitenden oben stehen und die Tage links“.

**Stand heute (26.11.2026 nachgesehen):**

| Ausgabe | Wo | Aufbau heute |
|---|---|---|
| „Exportieren“ → „Dienstplan der Woche“ als Excel oder CSV | `GET /api/locations/{locationId}/staff/export?list=roster`, `StaffExports.RosterTableAsync` (`src/AispaStudio.Api/Exports/StaffExports.cs:157`), Knopf in `RosterPlanViewModel.Export.cs`; nur Inhaberin und Geschäftsführung, mit Entwürfen | Personen als Zeilen, Tage als Spalten |
| „Exportieren“ → „Abwesenheiten“ als Excel oder CSV | derselbe Endpunkt, `list=absences` | eine Zeile je Spanne |
| „Aushang als PDF“ | `GET /api/locations/{locationId}/roster/pdf`, `RosterDocument` (`src/AispaStudio.Api/Roster/RosterDocumentEndpoints.cs`); nur Standortleitung, nur veröffentlichter Stand | A4 quer, Personen als Zeilen, Tage als Spalten |

**Was sich ändert:**

- **Dienstplan der Woche (Excel und CSV) gedreht:** erste Spalte „Tag“ mit „Mo. 23.11.2026“ bis „So. 29.11.2026“ (sieben Zeilen), danach eine Spalte je Person (Name im Haus, Reihenfolge wie im Wochenplan). Zelleninhalt bleibt wie heute (Zeit, Standort bei anderem Standort, Abwesenheit, Notiz). Letzte Spalte „Besetzung“ mit der Zahl der Dienste an diesem Standort je Tag (dieselbe Zählung wie die Zeile im Plan); letzte Zeile „Stunden“ mit Ist und Soll je Person („32 / 40“), sofern `RosterTableAsync` sie heute schon ausgibt, sonst weglassen und im PR-Protokoll nennen.
- **Angekündigter Wegfall:** Der bisherige Aufbau (Personen als Zeilen) entfällt in Excel und CSV; es gibt keinen Schalter für beide Richtungen (ANNAHME; wer die alte Richtung braucht, dreht in Excel mit „Transponieren“). Die Abfrage `list=roster` und die Parameter bleiben, nur der Inhalt der Datei ändert sich; die Beschreibung in `WithSummary` und im Klassenkommentar nachziehen, danach `dotnet build` und `npm run gen:api`.
- **Excel lesbar machen**, soweit `TableExport` es ohne neue Abstraktion hergibt: Kopfzeile und erste Spalte fixiert, Spaltenbreite nach Inhalt. Braucht das einen Umbau des gemeinsamen Dienstes (ADR 0091), nur nennen.
- **Unverändert:** Abwesenheiten-Export, Rechte, Protokoll (`ExportLog`), Dateiname „Dienstplan_JJJJ-MM-TT“.
- **Aushang als PDF bleibt, wie er ist** (ANNAHME: der Wunsch nennt Excel; auf A4 quer passen sieben Tage als Spalten besser als viele Personen als Spalten). Als Frage an den Auftraggeber sammeln, ob der Aushang ebenfalls gedreht werden soll.
- **Hilfe:** `web/src/help/content/seite-roster.md` Zeile 36 („Personen als Zeilen, Tage als Spalten“) berichtigen; Stichwörter bleiben.
- **Tests (höchstens 3, in `tests/AispaStudio.Api.Tests/StaffExportTests.cs`, bestehende anpassen statt neue daneben):** erste Spalte trägt die sieben Tage, die Köpfe danach die Personen; ein Dienst an anderem Standort steht mit Standortnamen in der Zelle und zählt nicht in „Besetzung“; CSV hat denselben Aufbau. `tests/AispaStudio.Desktop.Tests/ListExportTests.cs:133` bleibt grün (Pfad unverändert).
- **Ablauf:** eigene kleine Aufgabe T-NNN, Modell sonnet; berührt `RosterPlanView.axaml` nicht und kann deshalb parallel zu Aufgabe 1 oder 2 laufen.

**Bewusst offen:** Export der Matrix „Besetzung über den Tag“ als zweites Blatt; Monatsexport des Dienstplans; Export für eine einzelne Person (iCal).

## 6. Aufgabe 4: Kopfzeile des Wochenplans – Menü „Ausgabe“ oben rechts, nichts springt

**Anlass:** Auftraggeber, 26.11.2026, mit Bild (Woche ab 12.10.): Die Exporte sollen woanders stehen; die Ansicht springt beim Blättern, und die Darstellung wirkt nicht professionell.

**Befunde am Bild und im Code (`RosterPlanView.axaml` Zeilen 102 bis 136):**

1. Die Kopfzeile ist ein `WrapPanel`, in dem alles hintereinander steht. Der Wochentitel („Woche ab Mo. 5.10.“ / „Woche ab Mo. 12.10.“) ist je Woche verschieden breit, also rutschen „Exportieren“, Hilfe, „Aushang als PDF“ und „Anträge“ beim Blättern nach links und rechts.
2. Die Bestätigung „Gespeichert: Dienstplan_2026-10-12.xlsx“ steht **in** der Zeile (`RosterExportSaved`) und schiebt die Knöpfe dahinter weiter; nach dem Blättern verschwindet sie, die Knöpfe springen zurück.
3. „Aushang als PDF“ klappt eine Karte **über** der Tabelle auf (`RosterDocumentCard`: Titel, Satz, zwei Knöpfe, noch eine Zeile „Gespeichert: …“) und schiebt den ganzen Plan um rund 170 Bildpunkte nach unten.
4. Zwei Ausgaben derselben Sache stehen als zwei verschiedene Bedienmuster nebeneinander (Knopf mit Auswahl, Knopf mit Klappkarte), dazwischen das Fragezeichen.

**Was entsteht:**

- **Kopfzeile als Raster mit zwei Seiten** statt `WrapPanel`. Links: Standort, „‹“, „Diese Woche“, „›“, Wochentitel mit fester Mindestbreite für den längsten Titel (Ziffern gleicher Breite). Rechts bündig am Rand der Planfläche, **in derselben Zeile** (Auftraggeber, 26.11.2026: „oben in einer Linie, aber rechtsbündig und geteiltes Panel, damit es nicht springt“): „Anträge“ (mit Zahl), **ein** Menüknopf „Ausgabe“ (Symbol Herunterladen, Pfeil nach unten), Fragezeichen ganz rechts. Der Menüknopf ersetzt die Knöpfe „Exportieren“ und „Aushang als PDF“ (Auftraggeber, 26.11.2026: „Sammelmenü, aber oben rechts, eigenes Panel, kein Sprung beim Blättern“). Die beiden Hälften sind getrennte Spalten (`Grid ColumnDefinitions="*,Auto"`), sodass die Breite des Wochentitels die rechte Seite nie verschiebt. Erst unter der Mindestbreite des Fensters darf die rechte Gruppe als Ganzes in eine zweite Zeile umbrechen; bei 1100 px muss alles in einer Zeile stehen (nachmessen, sonst Zahlen berichten).
- **Menü „Ausgabe“** (Flyout nach dem Skill `fluent2-avalonia`; Tastatur: Enter/Leertaste öffnet, Pfeile wandern, Esc schließt, Fokus kehrt zum Knopf zurück), rechtsbündig unter dem Knopf, mit zwei beschrifteten Gruppen:
  - „Aushang · veröffentlichter Stand“: „Drucken …“, „Als PDF speichern“ (Recht Standortleitung; ohne Recht ausgegraut mit dem bestehenden Hinweis; auf der Personenkarte kein Aushang).
  - „Liste · mit Entwürfen“: „Dienstplan der Woche als Excel“, „Dienstplan der Woche als CSV“, „Abwesenheiten als Excel“, „Abwesenheiten als CSV“ (nur Inhaberin und Geschäftsführung; für andere fehlt die Gruppe).
  - Hat jemand keines der Rechte, gibt es den Knopf nicht; „Anträge“ und das Fragezeichen bleiben rechtsbündig stehen, links ändert sich nichts.
- Nichts klappt mehr über der Tabelle auf: Das Menü liegt über dem Plan und verschiebt ihn nicht.
- **Bestätigung als Meldung, nicht in der Zeile:** nach dem Speichern eine kurze Meldung nach [docs/gestaltung/meldungen.md](../gestaltung/meldungen.md) („Gespeichert: Dienstplan_2026-10-12.xlsx“, für den Screenreader weiter höflich angesagt), die keinen Platz im Layout belegt. Welches Bauteil die App dafür schon hat (Toast oder `InfoBar`), zuerst nachsehen und wiederverwenden; kein neues erfinden.
- **Wiederverwenden statt neu bauen:** Die Befehle hinter den Menüpunkten bleiben die bestehenden (`ExportViewModel` mit `ExportOptions()`, `Document` mit Drucken und Speichern aus `RosterPlanViewModel.Document.cs`). `ui:ExportButton` und `ui:DocumentActions` sind gemeinsame Bauteile anderer Masken und bleiben dort unverändert; fällt auf, dass andere Masken dasselbe Springen haben, nur nennen.

**Angekündigter Wegfall (in der Fertigmeldung noch einmal nennen):** die Klappkarte „Dienstplan-Aushang“ über der Tabelle (ihre Aussage „veröffentlichter Stand, ohne Entwürfe“ steht künftig als Gruppentitel im Menü), die einzelnen Knöpfe „Exportieren“ und „Aushang als PDF“ (beide gehen im Menü „Ausgabe“ auf) und die Textzeile „Gespeichert: …“ in der Kopfzeile (wird zur Meldung). Keine Funktion entfällt: Drucken, PDF speichern, Excel und CSV bleiben erreichbar.

**Erfolg ist erreicht, wenn:**

1. Beim Blättern über mindestens vier Wochen (ein- und zweistellige Tage, mit und ohne vorher gespeicherte Datei) bleiben alle Knöpfe der Kopfzeile und die Oberkante der Tabelle an derselben Stelle. Als Test: Positionen von „Ausgabe“, „Anträge“ und `RosterTable` vor und nach `NextWeekCommand` gleich.
2. Keine Ausgabe verschiebt den Plan: weder das offene Menü noch die Bestätigung.
3. Jede Ausgabe ist mit Tastatur allein erreichbar; Rechte wie bisher (Test je Rolle: Inhaberin sieht beide Gruppen, Standortleitung nur den Aushang, Rezeptionistin keinen Knopf).

**Bewusst offen (nennen, als Frage sammeln):** Das Entwurfsband „Veröffentlichen · Verwerfen“ (`DraftBand`) steht ebenfalls über der Tabelle und erscheint nur in Wochen mit Entwürfen; beim Blättern zwischen Wochen mit und ohne Entwurf verschiebt es den Plan weiter. Vorschlag für später: fester Platz in der Kopfzeile.

**Hilfe und Doku:** `web/src/help/content/dienstplan-veroeffentlichen.md` Abschnitt „Aushang drucken“ (Schritte 2 und 3 nennen den Knopf und die Karte) und `seite-roster.md` Zeile 36 („Exportieren“) neu fassen; Hilfebilder des Wochenplans neu; PR-Protokoll, Handtestblock, `docs/stand-und-offene-punkte.md`; `docs/design.md` Abschnitt 6 Punkt 8 um die Kopfzeile ergänzen, **vor** dem Bau.

**Ablauf:** eigene Aufgabe T-NNN, Modell sonnet, ändert `RosterPlanView.axaml` und läuft deshalb **nach** Aufgabe 1 und nicht parallel zu Aufgabe 2. Tests höchstens 4 (die drei aus „Erfolg“ plus Tastatur des Menüs).

## 7. Aufgabe 5: „Drucken“ öffnet den Druckdialog von Windows

**Anlass (Fehlerbericht des Auftraggebers, 26.11.2026):** „Drucken“ beim Aushang fragt nichts, sondern schickt das PDF sofort an den Standarddrucker. Ist dort ein Dateidrucker eingestellt (PDF, OneNote), entsteht kein Ausdruck, sondern es öffnet sich OneNote bzw. eine Frage nach dem Speicherort. Verlangt: der übliche Druckdialog.

**Ursache (im Code nachgelesen, nicht nachgestellt):** `ShellDocumentPrinter.PrintAsync` in `src/AispaStudio.Desktop/Services/DocumentPrinter.cs:39` schreibt das PDF in eine Temp-Datei und ruft das Verb `print` der Windows-Shell auf. Das PDF-Programm des PCs druckt damit „auf dem Standarddrucker ohne eigenen Dialog“ (Klassenkommentar); so ist es in ADR 0092 festgelegt. Betroffen ist deshalb **jedes** Dokument mit dem Knopfblock `DocumentActions` (Rechnung, Beleg, Gutschein, Aushang …), nicht nur der Dienstplan.

**Was sich ändert:** „Drucken“ zeigt in allen Masken den Druckdialog von Windows mit Druckerwahl, Seiten, Kopien und Ausrichtung; gedruckt wird erst nach „Drucken“ dort, „Abbrechen“ druckt nichts und meldet nichts. Die Schnittstelle `IDocumentPrinter` bleibt (Tests antworten weiter selbst); die Meldungen „Kein Drucker eingerichtet“ bleiben, wo sie noch zutreffen. Der Bondrucker der Kassa (ESC/POS) ist nicht betroffen.

**Paket 5a – Machbarkeit (höchstens 45 Minuten, Ergebnis ist ein Befund, kein Umbau):** Zwei Wege an einem PDF aus der App ausprobieren und mit Zahlen berichten (Dialog erscheint ja/nein, Druck auf echtem und auf PDF-Drucker richtig, Schriften und Ränder stimmen, zusätzliche Abhängigkeiten und Größe):

1. **WebView2, das die App schon für die Hilfe mitbringt** (`Avalonia.Controls.WebView` in `Directory.Packages.props`): PDF laden und den System-Druckdialog des WebView2 aufrufen. Zu prüfen ist, ob das Avalonia-Paket den Aufruf erreichbar macht und ob das ohne sichtbares Fenster geht.
2. **Windows selbst:** Seiten mit `Windows.Data.Pdf` rendern und über `System.Drawing.Printing` mit dem Standard-Druckdialog ausgeben. Zu prüfen: Ziel-Framework und neue Abhängigkeit (Paketversion nur in `Directory.Packages.props`), Druckschärfe (Auflösung der gerenderten Seiten).

Kein Weg, der ein fremdes PDF-Programm voraussetzt oder nur dessen Fenster öffnet. Trägt keiner der beiden, stoppen und berichten.

**Paket 5b – Umbau nach dem Befund:**

- Neuer ADR (nächste freie Nummer 0101) als Nachtrag zu ADR 0092: „Drucken mit Druckdialog statt Verb `print`“, mit dem gewählten Weg und dem verworfenen; ADR 0092 bekommt den Verweis.
- **Fehler zuerst nachstellen:** ein Test, der zeigt, dass heute ohne Rückfrage gedruckt wird (roter Test gegen die neue Erwartung „erst Dialog, dann Druck; Abbrechen druckt nicht“), dann umbauen. Der echte Dialog lässt sich kopflos nicht bedienen: die Entscheidung des Dialogs hinter eine schmale Schnittstelle legen, die Tests beantworten.
- Temp-Dateien: wird keine mehr gebraucht, `CleanUp` und der Ordner „Druck“ entfallen (selbst verwaist); sonst bleibt das Aufräumen.
- Hilfe: `web/src/help/content/` – das Thema „Dokumente drucken, speichern und senden“ (`hilfe:dokumente-drucken-speichern-senden`) und `dienstplan-veroeffentlichen.md` Abschnitt „Aushang drucken“ nennen den Dialog; Texte nach `docs/gestaltung/texte.md`.
- **Handtest (Pflicht, im Handtestblock):** an einem PC mit „Microsoft Print to PDF“ oder OneNote als Standarddrucker „Drucken“ beim Aushang und bei einer Rechnung: Dialog erscheint, anderer Drucker wählbar, „Abbrechen“ druckt nichts.
- Tests höchstens 4: Dialog bestätigt → Druck mit gewähltem Drucker; Dialog abgebrochen → kein Druck, keine Fehlermeldung; kein Drucker eingerichtet → bestehende Meldung; `DocumentActionsViewModel` zeigt während des Dialogs keinen Fehler.

**Ablauf:** eigene Aufgabe T-NNN, unabhängig von den Aufgaben 1 bis 4 (andere Dateien), Modell sonnet; wegen ADR und Wirkung auf alle Dokumente nicht mit Aufgabe 4 zusammenlegen. Reihenfolge-Empfehlung an den Auftraggeber: zuerst, weil es ein Fehler im Betrieb ist.

**Bewusst offen:** Vorschau vor dem Druck; Merken des zuletzt gewählten Druckers je Dokumentart; Drucken aus der Weboberfläche (ADR 0092, Stufe 3).
