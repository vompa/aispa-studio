# Meldungen: Vorgaben für Windows-App und Verwaltung

Verbindlich für alle Meldungen an Personen: Rückmeldungen, Zustände, Warnungen, Fehler, Hinweise zu einem Objekt. Grundlage: [ADR 0067](../entscheidungen/0067-masken-und-meldungen-nach-material-3-und-fluent-2.md). Masken stehen in [masken.md](masken.md).

**Werte stehen nur in [docs/design.md](../design.md)** (Größen, Abstände, Farben, Radien, Dauer, Knopfarten); dieses Dokument legt Aufbau und Verhalten fest. Bei einem Widerspruch zwischen Werten gilt design.md; der Abgleich vom 21.10.2026 steht im [PR-Protokoll](../umsetzung/pr-protokoll.md) ([ADR 0067](../entscheidungen/0067-masken-und-meldungen-nach-material-3-und-fluent-2.md), Nachtrag).

Anlass: Dem Auftraggeber gefällt der Meldungsbereich im Kalender nicht (Rückmeldung vom 21.10.2026). Heute stehen dort bis zu vier Dinge übereinander: roter Fehlerblock, gelbe Tagesleiste (Notizen, Abwesende, Warnungen gemischt), Betriebsleiste mit Pillen, Tagesstart und Hinweisen, dazu unten eine Rückmeldung mit bis zu vier Aktionen.

## 1. Quellen und Grenzen

| Quelle | Gelesen | Aussage |
| --- | --- | --- |
| [Fluent 2: Message bar](https://fluent2.microsoft.design/components/web/react/core/messagebar/usage) | vollständig | Für Zustand eines ganzen Produkts oder einer Fläche (Seite, Container, Karte, Formular, Drawer); nicht gegen zerstörende Aktionen (dafür Dialog); zeitkritisch zu etwas an anderer Stelle: Toast. Platz: Seite = unter der Befehlsleiste im Inhalt, Formular = oben im Formular; bei Absenden mit Fehlern geht der Fokus zur Leiste, Felder markieren den Fehler. Schweregrad Info, Erfolg, Warnung, Fehler; Warnung und Fehler brauchen Knopf oder Link; **jede Leiste hat einen Schließen-Knopf**; ungelöste Warnung/Fehler kommt in der nächsten Sitzung wieder. Text: Titel optional, Text ein bis zwei kurze Sätze (unter etwa zwei Zeilen), Erfolg ohne „erfolgreich“, Knopf ein Verb. Stapeln: von kritisch nach unkritisch; mehrere in einem Akkordeon mit Anzahl-Zeichen; Fehler/Warnung unterbrechen Screenreader, sparsam. |
| [Fluent 2: Toast](https://fluent2.microsoft.design/components/web/react/core/toast/usage) | vollständig | Kurz und nicht kritisch; für Kritisches Dialog, Feldfehler oder Message bar; nicht für Pflicht-Handlungen. Ohne Aktion nach 7 s weg, mit Mauszeiger pausiert; Fortschritt bleibt bis Ende. Position gleichbleibend unten oder oben rechts; höchstens vier gleichzeitig, 16 px Abstand. Titel passiv mit Verb in der Vergangenheit („Datei gespeichert“); Text unter etwa 60 Zeichen; Knopf ein bis zwei Wörter. |
| [Material: Snackbars](https://material.io/components/snackbars) (Suchergebnisse) | nur Zusammenfassung | Wenig störend, keine Handlung nötig, **eine Snackbar zugleich**. Dauer in Material 3 Compose: kurz etwa 4 s, lang etwa 10 s, unbegrenzt (aus Wissen zu `SnackbarDuration`, die Seite war nicht lesbar). |
| [Material: Banners](https://material.io/components/banners) (Suchergebnisse) | nur Zusammenfassung | Mittlere Priorität, oben unter der App-Leiste, bleibt, bis die Person es schließt oder der Zustand behoben ist. In Material 3 laut Suchergebnissen nicht mehr als Komponente für Android geführt. |
| m3.material.io Snackbar und Banner | **nicht lesbar** | Seiten werden per JavaScript gerendert; der Abruf lieferte nur Titel. Aussagen zu M3 stammen daher aus Material 1/2-Seiten und Zweitquellen. |
| Microsoft Windows App SDK `InfoBar` | nicht eigens abgerufen | Entspricht der Fluent-2-Message-bar (Schweregrade, Schließen, Aktion); Gleichsetzung ist **Annahme**. |

Zahlen für Dauer und Anzahl sind teils Projektfestlegungen auf Grundlage dieser Quellen (Abschnitt 2, markiert).

## 2. Welche Art wofür

| Art | Wofür | Beispiel | Ort |
| --- | --- | --- | --- |
| **Toast / Snackbar** | Bestätigung einer eigenen Aktion; nicht kritisch | „Termin verschoben auf Do 14:30“ | unten mittig, im Kalender mittig im Kalenderbereich |
| **InfoBar / Banner** | Zustand der Seite oder des Bereichs, der bleibt, bis er behoben oder geschlossen ist | „Ohne Verbindung. Änderungen werden vorgemerkt.“ | oben, über dem Inhalt der Seite |
| **Hinweis am Objekt** | Gilt für genau einen Termin, Gast, Raum, Artikel | „Gast ist Allergikerin: Anamnese fehlt.“ | am Objekt: im Termin-Panel, in der Kundenkarte, an der Zeile |
| **Feldfehler** | Eingabe falsch | „Die PLZ hat vier Ziffern.“ | am Feld ([masken.md](masken.md), R8) |
| **Dialog** | Person muss entscheiden, bevor es weitergeht; zerstörende Aktion | „Termin endgültig löschen?“ | in der Mitte, sparsam |
| **Tagesnotiz** | Eigene Notiz der Mitarbeitenden für den Tag, keine Systemmeldung | „Lieferung kommt um 10.“ | eigener Bereich, getrennt von Warnungen |

Entscheidungsfolge: Muss die Person entscheiden? Dialog. Betrifft es ein Feld? Feldfehler. Betrifft es einen einzelnen Termin oder Gast? Hinweis am Objekt. Dauert der Zustand an? InfoBar. Sonst: Toast.

## 3. Regeln

### M1. Toast: eine Zeile, höchstens eine Aktion

- Position unten mittig, im Kalender mittig im Kalenderbereich (design.md 5.10), **höchstens ein Toast zugleich**; ein neuer ersetzt den alten (Material: eine Snackbar).
- Text eine Zeile, Ergebnis in der Vergangenheit („Termin verschoben auf Do 14:30“, „Gespeichert“), höchstens **60 Zeichen** und **ohne Schlusspunkt** (Fluent 2; [ADR 0069](../entscheidungen/0069-fluent-2-als-grundlage-der-windows-app.md), vorher „unter etwa 80 Zeichen, Satz mit Punkt“). Ein Satz mit Punkt ist ein Hinweis (design.md 5.9), keine Rückmeldung. Keine Schuldzuweisung.
- **Höchstens eine Aktion**, ein bis zwei Wörter, meist „Rückgängig“. Weitere Entscheidungen gibt es im Kalender nicht mehr: Kürzen, Nach hinten schieben und Alle folgenden auch wurden am 23.10.2026 entfernt (Ä71), ein Termin bleibt stehen, auch wenn sein Beginn vorbei ist. Der Toast bleibt eine Bestätigung. 
- Dauer: **5 Sekunden** (Projektfestlegung, bleibt nach ADR 0069; liegt zwischen Material „kurz“ und Fluent 7 s, ist per Tastatur und Maus-Pause nicht hektisch); der Mauszeiger über dem Toast hält ihn an (wie heute `HoldToast`); Toasts mit Aktion „Rückgängig“ bleiben 8 s (heute `ToastDuration` 8 s).
- **Fehler und Warnungen bleiben stehen**, bis sie geschlossen werden, haben Schließen-Kreuz und nennen Problem **und** Weg; Hinweise, die auf einem Zustand beruhen (z. B. „Ohne Verbindung“), sind keine Toasts, sondern InfoBars (M2).
- Farben: Fläche je Variante nach design.md 5.10 (`ToastBrush` für Erfolg und Info, `WarnBrush`, `CritBrush`); bei Warnung und Fehler steht zusätzlich ein Symbol (Fluent: Symbol und Farbe zusammen).

### M2. InfoBar: oben über dem Inhalt, Schweregrad, schließbar, höchstens zwei

- Ort: **oben im Inhaltsbereich der Seite**, unter der Kopfzeile bzw. Befehlsleiste, nie über der Navigation; in einer Seitenleiste oben unter dem Kopf; in einer Maske über der Knopfleiste, nahe der Aktion (Fluent nennt „oben im Formular“; das Projekt folgt masken.md R8). Bei fehlgeschlagenem Speichern bekommt das **erste ungültige Feld** den Fokus, nicht die InfoBar; die InfoBar wird zusätzlich angezeigt (Entscheidung des Auftraggebers, ADR 0069; Fluent würde der Leiste den Fokus geben).
- Schweregrad **Info, Erfolg, Warnung, Fehler** je mit **Symbol und Farbe** (nie nur Farbe): Farben, Rahmen und Maße wie die Hinweise in design.md 5.9 (Neutral, Erfolg, Warnung, Fehler) und 5.15.
- Bauform: Symbol 16 px links, Titel (optional, SemiBold) und Text (eine bis zwei Zeilen), rechts **eine** Aktion (Knopf) und das Schließen-Kreuz. Warnung und Fehler tragen immer eine Aktion oder einen Link (Fluent).
- **Schließbar** (Fluent: jede Leiste hat Schließen); Warnung und Fehler kommen wieder, wenn der Zustand bleibt (nach einer Sitzung bzw. beim nächsten Laden der Seite).
- **Höchstens zwei sichtbar**, Reihenfolge nach Schweregrad (Fehler, Warnung, Erfolg, Info); weitere als Zeile „n weitere Meldungen“ (öffnet eine Liste) (Fluent: Akkordeon mit Anzahl; Zahl zwei ist Projektfestlegung).
- Erfolg ist **kein** InfoBar-Dauerzustand: Erfolg läuft als Toast; eine Erfolgs-InfoBar nur nach einem längeren Vorgang, der die Seite betrifft (z. B. „Import abgeschlossen“), und verschwindet nach dem Schließen.

### M3. Hinweise zu einem Objekt am Objekt

Was nur einen Termin, einen Gast oder einen Raum betrifft, erscheint dort: im Termin-Panel als Zeile mit Symbol oben im Panel (nicht als InfoBar der ganzen Seite), in der Kundenkarte oben, im Raum am Raumnamen. Der Kalender selbst zeigt am Termin nur ein kleines Symbol. Die Seiten-InfoBar bleibt Zuständen vorbehalten, die die ganze Seite betreffen.

### M4. Tagesnotizen getrennt von Warnungen

Tagesnotizen (Mitarbeitende schreiben, abhaken) und Abwesenheiten sind **Inhalt des Tages**, keine Systemmeldungen: *seit Runde 5, Ä31 (22.10.2026):* die Gruppe „Notizen heute“ ganz oben in der Seitenleiste „Meldungen“ (design.md 5.12, Punkt 11), mit „+ Notiz“; ein Band „Heute“ über dem Kalender gibt es nicht mehr, und die Notizen zählen nicht in der Zahl am Chip „Meldungen“ (der Chip trägt bei Notizen einen Punkt). *Vorher:* ein schmaler Bereich „Heute“ (siehe Abschnitt 4), neutral (surface-2 mit Rahmen line). Warnungen (Pause zu kurz, Lager unter Mindestmenge, Raum nicht bereit) sind **InfoBars** mit Schweregrad und werden nicht in die Notizzeile gemischt. Eine Warnung ist nie abhakbar; sie verschwindet, wenn der Zustand behoben ist.

### M5. Texte

- **Problem plus Lösung**, ohne Schuld: „Die Krankmeldung wurde nicht gespeichert. Bitte Verbindung prüfen und noch einmal versuchen.“ statt „Sie haben falsch eingegeben“ oder „Fehler 500“.
- Sachlich, Sie-frei, Imperativ nur in Aktionen; kein „erfolgreich“, kein Ausrufezeichen, keine Abkürzungen.
- Titel (optional) kurz, was ist passiert; Text, was die Person tun kann; Aktion ein Verb („Erneut versuchen“, „Rückgängig“, „Anzeigen“).
- Technische Ursache steht nur im Detail (aufklappbar) oder im Protokoll, nicht im Text.
- KI-Nachrichten tragen weiter den Hinweis auf die KI (CLAUDE.md).

### M6. Zugänglichkeit

Fehler und Warnungen: assertive Ansage; Info und Erfolg: höflich (`AutomationProperties.LiveSetting`, im Web `role="alert"` bzw. `role="status"`). Toast ohne Aktion erhält keinen Tastaturfokus; ein Toast mit Aktion lässt sich per Tastenkürzel erreichen (Strg+Z für Rückgängig bleibt). Bewegung nach ADR 0066 (`ReducedMotion`).

### M7. Ort der Meldung zu einer Aktion (Slice 72, Ä80)

Eine Meldung zu einer Aktion steht **direkt über dem Knopf dieser Aktion** im selben Panel, Fehlertext vor Konfliktliste: bei „Neuer Termin“, der gemeinsamen Buchung, dem Eintrag (Notiz, Sperrzeit, Pause) und bei „Verschieben“ im Termin-Panel (dort in der Verschieben-Karte; oben im Panel steht ein Fehler nur, solange die Karte zu ist). Eine Aktion ohne Panel, etwa das Ziehen im Raster, meldet in der Meldungszeile über dem Raster (`Problem`, M2). Die Person soll die Meldung dort sehen, wo sie gerade arbeitet, nicht an einem anderen Ende des Panels.

Meldungen über einen belegten Zeitraum („Zeitraum ist belegt.“, „Nicht verschoben“) nennen die belegende Person und den Raum des belegenden Termins und haben den Knopf **Zum Konflikt**: er zeigt die 7-Tage-Ansicht der Person (bei einem Raum im Weg: des Raums) ab dem Tag des Termins, der Termin ist markiert. „Kein geeigneter Raum ist zu dieser Zeit frei.“ nennt die belegten Räume mit ihrer Belegung und die nächste freie Zeit.

### M8. Wichtig oder Nebeninfo (Slice 88, Ä156)

Eine Meldung ist **wichtig**, wenn sie ein Ergebnis verhindert („In den nächsten 14 Tagen ist nichts frei“, „Der Überblick wurde nicht geladen“) oder eine Entscheidung verlangt. Wichtige Meldungen sind **InfoBars** (M2) mit **Ursache** (was ist los, mit den Einschränkungen, die dazu führen) und **Weg** (Knopf, der die Ursache aufhebt, z. B. „Tageszeit: egal“, „Erneut laden“; gibt es keinen Knopf, nennt der Text den Rat). Kleiner grauer Text (`muted small`) bleibt **Nebeninfos**: Erklärungen neben einem vorhandenen Ergebnis, Hilfen zum Feld, Zusatzangaben. Fehler dürfen nie nur als `TextBlock` mit `error` oder `WarnBrush` stehen; `MessageBarTests` prüft die bekannten Fehler-Eigenschaften (`Error`, `ErrorText`, `ReplyNotice`, `NoSlotsMessage`).

### M9. Hover-Karte und Popups (Slice 89, Ä158)

Eine reiche Karte am Termin oder am Spaltenkopf folgt dem Muster in [Design-Brief 5.22](../design.md): Hauptangaben in Grundschrift und fett, Nebenangaben normal, **wichtige Warnungen im orangen Band** statt als Zeile „Etikett – Wert“ (der erste Ausfall eines Gastes: „1× nicht erschienen“), und nur, was etwas sagt; alles andere steht im Termin-Panel. Schlichte Tooltips bleiben ein kurzer Satz. Kunden, Kassa und Meldungen haben nur schlichte Tooltips, keine reichen Karten.

## 4. So sieht der Kalender danach aus

Heute (von oben nach unten):

```text
+--------------------------------------------------------------+
| [ROT] Fehler als Textblock, Kalender                          |  Border.alert
+--------------------------------------------------------------+
| (gelb) Notiz abhaken | Abwesende | Warnung zur Pause    + Notiz einklappen |  Tagesleiste (alles gemischt)
+--------------------------------------------------------------+
| (grau) Pille Pille Pille ...                       einklappen |  Betriebsleiste
|   TAGESSTART je Raum | Bereitstellen | Hinweise | Lager       |  (aufgeklappt bis 220 px)
+--------------------------------------------------------------+
|                         Kalender                              |
|  [ Termin verschoben ... Rückgängig | x ]  |  Toast mit einer Aktion
```

Danach:

```text
+--------------------------------------------------------------+
| Kopfzeile (Tag, Ansicht, Filter)                              |
+--------------------------------------------------------------+
| (!) Warnung  Pause von Frau Berger ist kürzer als 30 Minuten.  [Anzeigen] [x] |  InfoBar, Warnung
| (x) Fehler   Krankmeldung nicht gespeichert. Verbindung prüfen. [Erneut]   [x] |  InfoBar, Fehler  (zuerst!)
|   + 3 weitere Meldungen                                                      |  höchstens zwei sichtbar
+--------------------------------------------------------------+
| Heute   Notiz: Lieferung 10 Uhr [ ]   Abwesend: Berger (krank)     + Notiz   |  Tagesnotizen, ein ruhiger Streifen
| Raumstart: 3 von 5 Räumen bereit                         [Tagesstart öffnen]  |  eine Zeile statt Pillenwolke
+--------------------------------------------------------------+
|                         Kalender                              |
|                                                               |
|  [ Termin verschoben auf Do 14:30             Rückgängig  x ]  |  Toast, eine Zeile, eine Aktion
+--------------------------------------------------------------+
```

Änderungen im Einzelnen (umgesetzt am 21.10.2026 im Kalender der Windows-App; die übrigen `Border.alert` der Windows-App am 25.10.2026 in Runde R01, Slices 03 bis 05, der Stil ist entfernt; im Web stehen sie noch aus):

1. **Roter Block** (`Border.alert` in `CalendarView.axaml`, Zeile `Error`) wird eine InfoBar „Fehler“ mit Symbol, Text und „Erneut versuchen“. Dasselbe gilt für die acht weiteren `Border.alert` in den Seitenleisten: sie wandern über die Aktionsleiste (siehe masken.md, R8).
2. **Tagesleiste** (`Border.daybar`) wird in zwei getrennte Teile geteilt: **Tagesnotizen und Abwesende** in einem neutralen Streifen „Heute“ (`Surface2Brush`, kein Gelb), **Warnungen** (`DayBarKind.Warning`) als InfoBar-Warnung. `DayBarLine` bleibt, bekommt die Aufteilung nach `Kind`.
3. **Betriebsleiste** (`Border.opsbar`) zeigt eingeklappt **eine Zeile** („Raumstart: 3 von 5 Räumen bereit“) statt einer Wolke von Pillen; der Inhalt (Tagesstart je Raum, Bereitstellen, Hinweise, Lager) öffnet sich als Seitenleiste „Tagesstart“ nach ADR 0066, nicht als aufgeklappter Bereich von 220 px über dem Kalender. Lagerwarnungen und wichtige Handbuchhinweise werden InfoBars (zählen zur Grenze „höchstens zwei“).
4. **Toast** (`Border.toast`) zeigt eine Zeile und höchstens **eine** Aktion. Im Toast bleibt „Rückgängig“; „Verkürzen“, „Nach hinten schieben“ und „Alle folgenden auch“ gibt es nicht mehr (Ä71, 23.10.2026).
5. **Hinweis zu einem Termin** (z. B. Anamnese fehlt, Allergie) steht im Termin-Panel oben, am Termin im Kalender nur das Symbol.
6. **Dauer:** Toast 5 s (`ToastDuration` von 8 auf 5 s, mit „Rückgängig“ 8 s); Warnung und Fehler bleiben.

Die Umsetzung braucht: ein Steuerelement `InfoBar` (Schweregrad, Titel, Text, Aktion, Schließen) in `Controls/`, ein ViewModel-Baustein für die Meldungsliste mit Sortierung und der Grenze „zwei sichtbar“, Anpassung von `ShowToast` (eine Aktion) und Tests für Sortierung, Grenze und „Warnung kommt wieder“. Gebaut sind `Controls/InfoBar.cs`, `CalendarViewModel.Notices.cs` (Meldungen, `CalendarNotice`), `CalendarViewModel.Offers.cs` (Vorschläge am Termin) und die Tests `CalendarNoticeTests` sowie `ViewTests.Masks`. „Warnung kommt wieder“ gilt hier beim Wechsel von Tag oder Standort (die Liste wird dann neu aufgebaut und geschlossene Meldungen sind wieder da), nicht erst nach einer Sitzung.

## 5. Kurz-Checkliste bei jeder neuen Meldung

1. Passt die Art (Toast, InfoBar, Objekthinweis, Feldfehler, Dialog) nach der Entscheidungsfolge (Abschnitt 2)?
2. Toast: eine Zeile bis 60 Zeichen ohne Schlusspunkt, höchstens eine Aktion, 5 s, ein Toast zugleich?
3. Fehler und Warnungen bleiben stehen und nennen Weg zur Lösung?
4. InfoBar oben im Inhalt, Schweregrad mit Symbol und Farbe, schließbar, höchstens zwei sichtbar?
5. Warnung ist nicht abhakbar und nicht mit Notizen vermischt?
6. Hinweis gilt für ein Objekt? Dann am Objekt, nicht als Seitenleiste oben.
7. Text: Problem plus Lösung, ohne Schuld, ohne „erfolgreich“, Aktion als Verb?
8. Fehler und Warnungen sind für den Screenreader assertive, Info und Erfolg höflich?
9. Verhindert die Meldung ein Ergebnis oder verlangt sie eine Entscheidung? Dann InfoBar mit Ursache und Weg, nie kleiner grauer Text (M8).
