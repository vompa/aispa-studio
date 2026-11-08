# Design-Brief AISPA Studio (verbindlich)

Stand: 21.10.2026. Dokument 4 der sechs Dokumente, die vor dem Bauen stehen (Anforderungen, Technik, Ablauf, **Design**, Daten, Plan). Vom Auftraggeber am 21.10.2026 bestellt („ein einheitliches Design, genaue Designbeschreibung, die auch schwächere Modelle genau so umsetzen“).

## 0. Lesart

- Dieses Dokument legt **einen** Wert je Eigenschaft fest. Wo ein Wert steht, gibt es keine Alternative. „Etwa“, „ungefähr“, „ähnlich“ kommen nicht vor.
- **MUSS** = so umsetzen. **DARF NICHT** = nie, auch nicht „weil es hier besser aussieht“. Wer von einem Wert abweichen will, ändert zuerst dieses Dokument per ADR; bis dahin gilt der Wert.
- Es gilt für beide Oberflächen: Windows-App (Avalonia, `src/AispaStudio.Desktop/Themes/Colors.axaml` und `Styles.axaml`) und Web (`web/src/index.css`, Team-Oberfläche inklusive der in der App eingebetteten Verwaltung). Der Kundenbereich (`web/src/booking`) nutzt dieselben Token, aber nur die Komponenten aus Abschnitt 5, die er braucht.
- Einheiten: Web in `px` (die Grundschrift ist 14 px, `rem` wird nicht mehr für Größen benutzt), App in DIP. Gleiche Zahl = gleicher Wert. **Gewollte Abweichung von fluent2-web** („`rem` verwenden“): Brief W1 verlangt px ausdrücklich, und Browser-Zoom skaliert px mit; die Lesbarkeit bei Zoom sichert der Umbruch nach Abschnitt 9.
- Jede Farbe, jede Größe und jeder Radius kommt aus den Tabellen in Abschnitt 2 bis 4. Ein Wert, der dort nicht steht, **DARF NICHT** im Code vorkommen (Ausnahmen: Belegpapier und Druck, Abschnitt 10).
- **Eine Quelle für Oberflächenregeln:** Werte und Komponenten (Größen, Abstände, Farben, Radien, Dauer, Knopfarten) stehen nur hier. Aufbau und Verhalten von Masken und Meldungen (Gruppen, Reihenfolge, Texte, Meldungsarten) stehen in [gestaltung/masken.md](gestaltung/masken.md) und [gestaltung/meldungen.md](gestaltung/meldungen.md) ([ADR 0067](entscheidungen/0067-masken-und-meldungen-nach-material-3-und-fluent-2.md)) und verweisen für jeden Wert hierher; die Abschnitte 5.14 und 5.15 fassen sie zusammen. Bei einem Widerspruch zwischen Werten gilt dieses Dokument, bei einem Widerspruch im Aufbau ADR 0067 (Abgleich vom 21.10.2026 im [PR-Protokoll](umsetzung/pr-protokoll.md)).
- Reihenfolge bei der Umsetzung: erst Abschnitt 11 (Umstellungsliste), dann die Prüfung nach Abschnitt 12.

### 0.1 Fluent 2 als Grundlage der Windows-App

- Microsoft Fluent 2 (WinUI 3) ist die Grundlage für **Verhalten und Zustände** der Windows-App ([ADR 0069](entscheidungen/0069-fluent-2-als-grundlage-der-windows-app.md)). Gelesen und abgeleitet ist es in [gestaltung/fluent2-ableitung.md](gestaltung/fluent2-ableitung.md) (Zahlen mit Quelle, Umsetzungsliste F1 bis F30).
- **Regel:** Wo dieser Brief einen Wert oder ein Verhalten festlegt, gilt der Brief. Wo er schweigt, gelten die Fluent-Werte aus der Ableitung (Abschnitt 2 Grundlagen, Abschnitt 3 Komponenten); sie werden vor dem Bauen als eigener Abschnitt in 5 eingetragen (Abschnitt 10, letzter Punkt). Jede Abweichung von Fluent 2 steht im Brief an ihrer Stelle als **gewollte Abweichung von Fluent 2** mit einem Satz Grund; eine Abweichung ohne diesen Vermerk ist ein Fehler.
- Die Marke bleibt: Teal-Akzent, Inter, Lotus-Zeichen, Radien 8 und 12. Übernommen wird das Verhalten von Fluent 2, nicht das Markenbild von Microsoft.
- Die Umsetzung läuft auf einem eigenen Branch `fluent2`, damit sie sich zurücknehmen lässt (ADR 0069); der Stand je Punkt steht in 11.4.
- Der Brief gilt für beide Oberflächen. Das Verhalten der Eingabefelder (5.16 bis 5.20), der Tag-Picker (5.26) und der Schalter (5.23) ist für die Windows-App festgelegt; im Web gelten dieselben Anzeigeformate und Fehlertexte, die Bedienung der Web-Felder (native Elemente) ist darüber hinaus nicht festgelegt.

## 1. Marke

- Name im Produkt: **AISPA Studio**. Kurzform in der Navigation: **AISPA** (Großbuchstaben, Zeichenabstand 0,08 em, 12 px, SemiBold).
- Symbol: die stilisierte Lotusfigur aus `src/AispaStudio.Desktop/Controls/Icons.cs` in einem Kreis (Durchmesser 44 px, Fläche `rail-mark`, Figur `rail`). Es steht links oben in der Leiste der Windows-App und links in der Kopfzeile im Web. Nirgendwo sonst, nie als Wasserzeichen, nie gestreckt.
- Ton der Texte: Deutsch, österreichische Begriffe erlaubt („Kassa“). Knöpfe tragen ein Verb im Infinitiv („Speichern“, „Einchecken“), Titel Substantive („Team“, „Behandlungen“), Hinweise ganze Sätze mit Punkt, keine Ausrufezeichen, keine Großbuchstaben zur Betonung. Anrede der Nutzer: unpersönlich oder „Sie“ wie im Handbuch, nie „Du“.

## 2. Farben (Token)

Ein Token hat genau zwei Werte (hell, dunkel). Web: CSS-Variable in `:root` und im Block `@media (prefers-color-scheme: dark)`. App: `SolidColorBrush` mit `x:Key` in beiden Themes von `Colors.axaml`. Die Namen in der Tabelle sind die einzigen erlaubten Namen.

### 2.1 Flächen, Linien, Text

| Token | Web | App | Hell | Dunkel | Zweck |
|---|---|---|---|---|---|
| bg | `--bg` | `BgBrush` | `#f3f0ea` | `#0c1211` | Fensterhintergrund |
| surface | `--surface` | `SurfaceBrush` | `#ffffff` | `#151c1b` | Karten, Felder, Knöpfe |
| surface-2 | `--surface-2` | `Surface2Brush` | `#f9f7f3` | `#1a2321` | Hover von Listen und sekundären Knöpfen, verschachtelte Karten, nur lesbare Felder |
| surface-3 | `--surface-3` | `Surface3Brush` | `#ece8e0` | `#232e2c` | Reiterschiene, Streifen, Pillen, Hover von Ghost-Knöpfen |
| line | `--border` | `LineBrush` | `#e4ded3` | `#26322f` | Rahmen von Karten, Tabellenzeilen, Trenner |
| line-strong | `--border-strong` | `LineStrongBrush` | `#c7bfb1` | `#41524e` | Rahmen von Feldern (deren Unterkante trägt fg-3) und sekundären Knöpfen, Tabellenkopf |
| fg | `--fg` | `FgBrush` | `#1a2321` | `#e2eae7` | Text |
| fg-2 | `--muted` | `Fg2Brush` | `#47524f` | `#a9b8b4` | Sekundärtext, Beschriftungen, Tabellenkopf, Hilfetext |
| fg-3 | `--fg-3` | `Fg3Brush` | `#757e7a` | `#7e8f8a` | **nur** für großen Text (ab 24 px normal oder ab 18,5 px fett) und für Symbole; nie für Fließtext (Kontrast 4,18:1 hell, 5,09:1 dunkel); außerdem Unterkante der Felder und Rand von Kontrollkästchen und Optionsfeldern (5.2) |
| off | `--off` | `OffDutyBrush` | `rgba(26,35,33,0.05)` | `rgba(226,234,231,0.04)` | Fläche außerhalb der Dienst-/Öffnungszeit |
| hatch | `--hatch` | `HatchBrush` | `#e5e0d6` | `#25302e` | Schraffur Abwesenheit |
| toast | `--toast` | `ToastBrush` | `#1a2321` | `#e2eae7` | Fläche der Meldung unten |
| toast-fg | `--toast-fg` | `ToastFgBrush` | `#f3f0ea` | `#0c1211` | Text der Meldung |

### 2.2 Akzent und Bedeutungsfarben

| Token | Web | App | Hell | Dunkel | Zweck |
|---|---|---|---|---|---|
| accent | `--accent` | `AccentBrush` | `#0d5c63` | `#52b3ba` | Markierung, Links, gewählter Reiter, Fokus |
| accent-hover | `--accent-hover` | `AccentHoverBrush` | `#0a4a50` | `#71c5cb` | Hover des primären Knopfs |
| accent-soft | `--accent-soft` | `AccentSoftBrush` | `#d9ebea` | `#163638` | gewählte Listenzeile, Hinweis (neutral), Hover von Kacheln |
| accent-line | `--accent-line` | `AccentLineBrush` | `#86bcbf` | `#2f767b` | Rahmen von Wunsch-Chips |
| on-accent | `--on-accent` | `AccentFgBrush` | `#ffffff` | `#061f21` | Text auf accent |
| accent-gradient | `--accent-gradient` | `AccentGradientBrush` | `linear-gradient(180deg, #11717a, #0b5258)` | `linear-gradient(180deg, #5cbcc3, #3f9ca3)` | **nur** Fläche des primären Knopfs; Richtung senkrecht (App: StartPoint 0,0 EndPoint 0,1 relativ) |
| ok | `--ok` | `OkBrush` | `#2c6e46` | `#5ec28a` | Erfolg, erledigt, eingecheckt |
| ok-soft | `--ok-soft` | `OkSoftBrush` | `#dbeee2` | `#163626` | Fläche von Erfolgshinweisen |
| warn | `--warn` | `WarnBrush` | `#8f5900` | `#e2a744` | Warnung, überfällig, Stammgast-Kennzeichen |
| warn-soft | `--warn-soft` | `WarnSoftBrush` | `#f6e9cf` | `#382a10` | Fläche von Warnhinweisen |
| danger | `--danger` | `CritBrush` | `#b42318` | `#f2796c` | Fehler, gefährliche Knöpfe, negative Beträge |
| danger-soft | `--danger-soft` | `CritSoftBrush` | `#fbe2df` | `#3d1a16` | Fläche von Fehlerhinweisen |
| note | `--note` | `NoteBrush` | `#7a6000` | `#e8cf6a` | Text auf Notizflächen |
| note-soft | `--note-soft` | `NoteSoftBrush` | `#fff2b3` | `#3a3312` | Haftnotiz, „unsicher gelesen“, Notiz am Termin (Tagesnotizen stehen in der Seitenleiste „Meldungen“ auf surface, Gruppe „Notizen heute“, 5.12 Punkt 11) |

### 2.3 Navigation (dunkle Leiste)

| Token | Web | App | Hell | Dunkel |
|---|---|---|---|---|
| rail | `--rail` | `RailBrush` | `#0d3d42` | `#0a1f21` |
| rail-fg | `--rail-fg` | `RailFgBrush` | `#f2f7f6` | `#e6efed` |
| rail-fg-2 | `--rail-fg-2` | `RailFg2Brush` | `#a7c4c3` | `#86a3a1` |
| rail-hover | `--rail-hover` | `RailHoverBrush` | `#164c52` | `#12302f` |
| rail-bar-hover | `--rail-bar-hover` | `RailBarHoverBrush` | `#1a565c` | `#153837` |
| rail-selected | `--rail-selected` | `RailSelectedBrush` | `#1f6067` | `#18403f` |
| rail-mark | `--rail-mark` | `RailMarkBrush` | `#e3b77a` | `#e3b77a` |

### 2.4 Kalenderstatus (nur Kalender, beide Oberflächen)

| Status | Text/Rand (hell / dunkel) | Fläche (hell / dunkel) | Token-Namen |
|---|---|---|---|
| Gebucht | `#3a6a9c` / `#7fa8d4` | `#e3edf7` / `#1a2a3a` | `st-booked`, `st-booked-bg` (`StBookedBrush`, `StBookedBgBrush`) |
| Eingecheckt | `#2e8150` / `#5ec28a` | `#dcf1e4` / `#163525` | `st-checked-in`, `st-checked-in-bg` |
| Erledigt | `#656c68` / `#97a6a2` | `#efede8` / `#1f2826` | `st-done`, `st-done-bg` |
| Überfällig | warn | warn-soft | aus 2.2 |
| Nicht erschienen | danger, Rand gestrichelt 1 px | surface | aus 2.2 |

Der helle Wert `#656c68` für „Erledigt“ stammt aus dem Ruling vom 25.10.2026 (Rundenakte R02, `work/rounds/R02-ui-decisions.md`) und erreicht 4,6:1 auf `#efede8` (gemessen in `work/rounds/R01-ui-review.md`, Ruling dort vom selben Tag).

Farbe trägt im Kalender genau eine Bedeutung: den Status (Rezeptions-Konzept, Abschnitt „Symbole und Farben“). Die Personenfarbe erscheint nur als 4-px-Rand links und im Initialenkreis.

#### Dienst-Chip im Dienstplan (nur App, *T-039, Ä372*)

| Token | Hell | Dunkel | Token-Namen |
|---|---|---|---|
| Basis, in die die Personenfarbe gemischt wird | `#ffffff` | `#0c1211` (= `bg`) | `roster-chip-base` (`RosterChipBaseBrush`) |
| Tinte (Zeit im Chip) | `#1a2321` (= `fg`) | `#e2eae7` (= `fg`) | `roster-chip-ink` (`RosterChipInkBrush`) |
| Anteil der Personenfarbe an der Fläche | 22 % | 45 % | Konstanten `RosterChipColors.ShareLight` und `ShareDark` |

Die Fläche des Chips ist die Personenfarbe, zu diesem Anteil in die Basis gemischt (Kanalwert `Basis + (Person − Basis) × Anteil`, gerundet). Hell entspricht das genau der früheren Aufhellung um 78 % zu Weiß, die hellen Chips (und die Handbuchbilder) bleiben bit-gleich. Fällt der Kontrast der Tinte auf der Fläche unter 4,5:1 (Ausreißerfarben wie `#ffd400` oder `#ffffff`, die ein Nutzer wählen kann), sinkt der Anteil in 5-Punkt-Schritten. Die Mischung rechnet das Steuerelement `RosterChip` nach dem wirksamen Farbschema (auch „System“) und bei jedem Wechsel neu; `RosterWeekLayoutTests` rechnet den Kontrast für neun Farben nach, `DesignTokenTests` den der Tinte auf der Basis (hell 16,1:1, dunkel 15,5:1).

Gemessen für die elf Demo-Personenfarben (`DemoData.cs`): Tinte auf der Fläche hell 11,5 bis 12,9:1, dunkel 7,1 bis 10,4:1. Die dunkle Fläche liegt gegen `bg` bei 1,5 bis 2,2:1 und gegen `accent-soft` (Spalte von heute) bei 1,0 bis 1,5:1; der Chip trägt sich über Fläche und Text, die 3:1-Regel für Ränder gilt für ihn nicht (2.6). Sehr dunkle Personenfarben (`#000000`) bleiben bedienbar: die Zeit ist lesbar (16,2:1), die Chipform tritt kaum hervor (1,05:1 gegen `bg`).

### 2.5 Schatten und Fokus

| Token | Hell | Dunkel | Verwendung |
|---|---|---|---|
| shadow | `0 1px 2px rgba(26,35,33,0.05), 0 4px 14px -6px rgba(26,35,33,0.12)` | `0 1px 2px rgba(0,0,0,0.4), 0 6px 18px -8px rgba(0,0,0,0.6)` | Karten, Listenkarten, Popover. App: `BoxShadow` mit denselben Werten je Theme |
| shadow-raised | `0 2px 8px rgba(0,0,0,0.18)` | `0 2px 8px rgba(0,0,0,0.5)` | Termin beim Hover, Haftnotiz, gewählter Reiter (dort `0 1px 2px rgba(0,0,0,0.13)`) |
| focus | `0 0 0 3px rgba(13,92,99,0.22)` | `0 0 0 3px rgba(82,179,186,0.30)` | Fokusring, Abschnitt 4.5 |

Alle anderen Schattenwerte **DARF ES NICHT** geben. Der Hover eines Knopfs hat keinen Schatten.

### 2.6 Kontrastregel

Text auf seiner Fläche erreicht mindestens 4,5:1. 3:1 gilt für Rahmen von Feldern, Kontrollkästchen und Optionsfeldern sowie für Symbole, die einen Zustand tragen (WCAG 2.2 AA; **gewollte Abweichung von Fluent 2**, das 2.1 AA nennt: 2.2 ist strenger). Rahmen von Knöpfen und Chips sind davon ausgenommen (**bewusste Abweichung**: Fläche und Text tragen den Knopf; Fluent 2 verlangt für Knöpfe mit Text keinen Rahmenkontrast). Die Statusfarben `--st-*` sind Flächen und Balken von Terminen, Text darin ist `fg`; einzige Ausnahme ist die Pille „gebucht“ (`st-booked`, 4,76:1 hell). `st-checked-in` hell (4,06:1 auf seiner Fläche) trägt deshalb keinen Text in dieser Farbe. Gemessen (WCAG-Formel, Nachrechnung vom 21.10.2026): `fg-3` erreicht 4,18:1 hell und 5,09:1 dunkel auf surface, `line-strong` nur 1,82:1 hell und 2,09:1 dunkel. Deshalb: jedes Eingabe- und Auswahlfeld trägt seine Unterkante 1 px in `fg-3` (wie Fluent 2 mit dem dunkleren Unterrand), Kontrollkästchen und Optionsfelder den ganzen Rand; `fg-3` nur für Text ab 24 px normal oder ab 18,5 px fett (großer Text) und für Symbole; das Stammgast-Kennzeichen nutzt `warn`, nicht `#c08a00`; der Status „Prüfen“ im Posteingang nutzt `warn`, nicht `#b5524a`.

## 3. Schrift

- Familie, beide Oberflächen: `Inter, "Segoe UI Variable Text", "Segoe UI", system-ui, sans-serif`. App: `FontFamily` einmal am `Window` setzen. **Gewollte Abweichung von Fluent 2** (Hausschrift Segoe UI Variable): Inter, damit App und Web dieselbe Schrift zeigen; Segoe UI Variable bleibt der Rückfall. Das Web liefert Inter 3.019 (dieselbe Version wie die App) als lokale Variable-WOFF2 mit `font-display: swap` und Preload aus (`web/src/assets/fonts/`), nur für die Verwaltung (Team-Oberfläche und Hilfe). Der Kundenbereich bleibt bei der Systemschrift (`system-ui`), damit er unter dem Budget von 100 kB bleibt und keine Schrift nachlädt; ebenso „Mein Tag“ und die Betreiber-Seite. Monospace nur für Belege, Protokolle und Codes: `"Cascadia Mono", Consolas, ui-monospace, monospace`.
- Ziffern in Tabellen, Beträgen, Uhrzeiten und Kennzahlen immer mit gleicher Breite: Web `font-variant-numeric: tabular-nums`, App `FontFeatures="tnum"` (Klasse `num`).
- Schriftgewichte: nur 400, 500, 600. 550 und 700 **DARF ES NICHT** geben. Gewicht 500 ist eine **gewollte Abweichung von Fluent 2** (Windows kennt nur Regular und Semibold): Inter 500 trägt bei Knöpfen und Beschriftungen.
- Mindestgröße (Fluent 2, WinUI): 12 px Regular und 14 px Semibold, nichts kleiner. Satzschreibweise nach deutscher Rechtschreibung, keine Versalien zur Betonung, kein Kursiv; `TextBlock.overline` **DARF NICHT** vorkommen.

| Stufe | Größe | Gewicht | Zeilenhöhe | Verwendung | Web | App |
|---|---|---|---|---|---|---|
| Titel | 22 | 600 | 28 | Seitentitel, Name des Gastes im Panelkopf | `h1` | `TextBlock.h1` |
| Überschrift | 18 | 600 | 24 | Kartentitel, Uhrzeit im Termin-Panel | `h2` | `TextBlock.h2` |
| Zwischentitel | 16 | 600 | 22 | Abschnitt in einer Karte („Öffnungszeiten“) | `h3` | `TextBlock.h3` |
| Text | 14 | 400 | 20 | alles ohne andere Angabe | `body` | Standard |
| Klein | 12 | 400 | 16 | Hilfetext unter Feldern, Untertitel in Listen, Vorperiode in Kennzahlen | `.small` | `TextBlock.small` |
| Beschriftung | 12 | 500 | 16 | Feldbeschriftung, Tabellenkopf, Kennzahl-Beschriftung; als Gruppentitel über Listen und Abschnitten (5.6) ohne den Feldabstand | `label`, `th` | `TextBlock.label`, als Gruppentitel `TextBlock.group` |
| Mini | 12 | 500 | 14 | Pillen, Kennzeichen, Kürzel, Streifentext | `.status`, `.badge` | `Border.pill TextBlock`, `Border.stripe TextBlock` |
| Zahl groß | 22 | 600 | 28 | Kennzahl-Wert | `.kpi-value` | `TextBlock.kpi` |

**Gewollte Abweichung von Fluent 2:** Der Titel bleibt 22/28 (Fluent Windows: 20/28 oder 28/36; Entscheidung des Auftraggebers, ADR 0069). Alle anderen Stufen liegen auf der Fluent-Rampe (Caption 12, Body 14, Subtitle 2 16/22, Body large 18/24).

Die Zeilenhöhen stehen als `LineHeight` an den Stilen der App und in `index.css` am Web; die Stufen mit Größe, Zeilenhöhe und Gewicht führt `docs/design-tokens.json` (Test in Web und App, Fluent 2 F23). Außerhalb der Stufen kommt keine Schriftgröße vor (Ausnahme: die Anmeldemaske mit Markenname 34 und Eingabe 24, im Test benannt; dieselben Größen trägt der Seitentitel der Erklärseite `/ueber/`, 34 und unter 480 px Breite 24, in `web/src/about/about.css`) und kein Gewicht außer 400, 500 und 600. Texte, die vorher in Versalien standen, stehen jetzt in Satzschreibweise auf der Stufe Beschriftung (F24).

Zwischen Titel und erstem Inhalt stehen 12 px, zwischen Überschrift und Inhalt 12 px, zwischen Zwischentitel und Inhalt 8 px. Lange Texte brechen um; eine Zeile wird nur in Tabellenzellen und Terminkarten mit „…“ gekürzt, und dann steht der volle Text im Hinweis (Tooltip).

## 4. Raster, Formen, Bewegung

### 4.1 Abstände

Erlaubte Abstände: **4, 8, 12, 16, 20, 24, 32** px, dazu **2, 6, 10** nur als Innenabstand um Symbole und in Feldern, Hinweisen und Listenzeilen (Fluent 2: Ausgleich für Symbolränder; gemessen von der Begrenzungsbox). Kein anderer Wert; 28, 36, 40 und 48 sind nicht aufgenommen.

| Stelle | Wert |
|---|---|
| Seitenrand (Inhalt zum Fensterrand) | 24; in der App eingebettete Verwaltung 16 |
| Zwischen Karten | 16 |
| Innen in Karten | 16 oben/unten, 20 links/rechts |
| Zwischen Feldern in einer Zeile | 12 |
| Zwischen Feldzeilen | 12 |
| Zwischen Feldgruppen (Zwischentitel), ohne Trennlinie | 20 |
| Beschriftung zu Feld | 4 |
| Feld zu Hilfetext | 4 |
| Zwischen Knöpfen in einer Leiste | 8 |
| Innen in Hinweisen | 10 oben/unten, 12 links/rechts |
| Listenzeile | 10 oben/unten, 12 links/rechts |
| Tabellenzelle | 8 oben/unten, 12 links/rechts; erste Spalte links 0 |

### 4.2 Radien

| Element | Radius |
|---|---|
| Karte, Listenkarte, Modal und Rückfragedialog, Kennzahl-Kachel, Navigationszeile der App | 12 |
| Reiterschiene, Kachel zum Antippen | 10 |
| Knopf, Eingabefeld, Auswahlfeld, Reiter, Hinweis, verschachtelte Karte, Raumkürzel, Behandlungskarte, Menü, Auswahlliste, Kalender-Popover, Tooltip, Skeleton-Zeile | 8 |
| Terminkarte im Kalender, Listenzeile, Menüeintrag, Auswahlring | 6 |
| Streifen, Kästchen, Tastenkürzel, Fortschrittsbalken | 4 |
| Pille, Zähler-Chip, Auswahl-Chip, Tag, Schalter, Hilfe-Knopf, Initialenkreis, Kürzelkreis, Kennzeichen | 999 (Kreis bzw. Kapsel) |

**Gewollte Abweichung von Fluent 2:** Fluent rundet Steuerelemente mit 4 und Überlagerungen mit 8; der Brief nimmt 8 für Knopf, Feld und Hinweis und 12 für Karte und Dialog (weichere Marke; Fluent selbst kennt 2, 4, 6, 8 und 12 als Token). Menü, Auswahlliste und Tooltip folgen Fluent (Überlagerung 8).

### 4.3 Rahmen

Rahmen sind 1 px. Ausnahmen, genau diese: Personenfarbe am Termin links 4 px; gewählte Listenzeile links 3 px `accent`; überfällige Raumpflege links 2 px `warn`; Tastenkürzel unten 2 px; Fokus siehe 4.5.

### 4.4 Mindestmaße

Alle Bedienelemente sind mindestens **34 px** hoch (Knopf, Feld, Auswahl, Chip zum Auswählen, Reiter in der Schiene 28 px innerhalb der 34-px-Schiene). Symbol-Knöpfe sind 34 × 34. Der Hilfe-Knopf ist 22 × 22 (Ausnahme, weil er neben Beschriftungen steht). Kleine Knöpfe (in Tabellenzeilen und Werkzeugleisten) sind 28 px hoch. Nur im Web und nur bei Touch (`pointer: coarse`) sind Tippflächen 48 px hoch. **Gewollte Abweichung von Fluent 2:** Fluent nimmt 32 (mittel; klein 24, groß 40) und WinUI 32 als Mindesthöhe; der Brief nimmt 34 (klein 28) für ein größeres Ziel an Rezeption und Kassa und ein einziges Maß für alle Bedienelemente. 32 ist die untere Grenze, nie darunter. **Einzige Ausnahme nach oben für Knöpfe und Felder in voller Größe in der Windows-App** (*festgehalten 25.10.2026, Runde R02 Slice 32, W3*): der Bezahlknopf der Kassa („Bezahlen und Beleg erstellen“) ist **46 px** hoch, damit die eine Hauptaktion aus der Spalte der Zahlungen heraussticht. Alle anderen Knöpfe und Felder setzen keine eigene Höhe und erben 34 aus dem Stil (kleine Knöpfe 28 wie oben; der Akkordeon-Kopf 44 nach 5.29 und die Listenzeile 40 nach 5.6 sind keine Knöpfe in voller Größe). Felder mit größerer Schrift (PIN und Freigabecode der Anmeldung, 24) wachsen mit der Schrift über 34 hinaus.

### 4.5 Fokus

Tastaturfokus ist immer sichtbar: Ring `focus` (3 px, außen) **und** Rahmen `accent` 1 px. Web: `:focus-visible { outline: none; box-shadow: var(--focus); border-color: var(--accent); }` für `button, a, input, select, textarea, [role=tab], .chip`. Kontrollkästchen und Optionsfelder: `outline: 2px solid var(--accent); outline-offset: 2px`. App: `FocusAdorner` mit 2 px `AccentBrush` und 2 px Abstand für alle fokussierbaren Elemente; kein Element ohne sichtbaren Fokus.

**Eingabe- und Auswahlfelder** (Text, Zahl, Betrag, Uhrzeit, Datum, Auswahlliste, Tag-Picker; Windows-App) tragen zusätzlich (nicht statt Ring und Rahmen) einen **Unterstrich 2 px `accent`** an der Unterkante, **auch bei Mausfokus**; die Avalonia-Vorlage nähme sonst den Systemakzent. Er wächst beim Fokus in **200 ms** von der Mitte aus (Skalierung 0 auf 1, Kurve `(0,0,0,1)`) und verschwindet beim Verlassen in **50 ms** (Kurve `(1,0,1,1)`); bei „Animationen aus“ 0 ms. Knöpfe, Chips, Reiter und Listenzeilen behalten Ring und Rahmen. Der Unterstrich ersetzt die Unterkante `fg-3` (5.2) solange das Feld den Fokus hat.

**Gewollte Abweichung von Fluent 2:** Fluent zeichnet an Knöpfen einen Rand mit eingerücktem 1-px-Ring in Schwarz bzw. Weiß; der Brief nimmt überall den Akzent-Ring, damit der Fokus in beiden Oberflächen gleich aussieht.

### 4.6 Bewegung

| Was | Dauer | Kurve |
|---|---|---|
| Hover und Fokus (Fläche, Rahmen, Text) | 100 ms | `(0.33,0,0.67,1)` |
| Drücken eines Knopfs | 100 ms, Skalierung 0,97 | `(0.33,0,0.67,1)` |
| Seitenleiste ein / aus (ADR 0066) | 200 ms / 120 ms | ein `(0,0,0,1)`, aus `(1,0,1,1)` |
| Meldung unten ein / aus | 160 ms / 120 ms | ein `(0,0,0,1)`, aus `(1,0,1,1)` |
| Fokus-Unterstrich der Felder ein / aus (4.5) | 200 ms / 50 ms | ein `(0,0,0,1)`, aus `(1,0,1,1)` |
| Schalter (5.23) | 200 ms | `(0.33,0,0.67,1)` |
| Startbildschirm (5.31): Wellen des Logos zeichnen sich, danach der Tropfen | 600 ms (zweite Welle 120 ms später) / 200 ms | `(0,0,0,1)` |
| Startbildschirm: Name und Statuszeile erscheinen | 200 ms | `(0,0,0,1)` |
| Startbildschirm: unbestimmter Balken | 1400 ms je Durchlauf | `(0.33,0,0.67,1)` |
| Startbildschirm: Statuszeile aus / Fläche wird Leiste oder Markenspalte / Ansicht ein | 120 ms / 400 ms / 160 ms | aus `(1,0,1,1)`, sonst `(0,0,0,1)` |

Die Kurven sind die Fluent-2-Token „easy ease“ `(0.33,0,0.67,1)`, „decelerate mid“ `(0,0,0,1)` für Eintretendes und „accelerate mid“ `(1,0,1,1)` für Austretendes; die Dauern 100, 200 und 50 ms sind Fluent „schneller“, „normal“ und „ultraschnell“. Übrige Dauern (160 und 120 ms) bleiben.

Bei „Animationen aus“ (System) und bei Tastaturbefehlen keine Bewegung: **alle** Übergänge laufen mit 0 ms, auch Deckkraft-Übergänge. Nichts anderes bewegt sich: Menü, Auswahlliste, Kalender-Popover, Tooltip und Rückfragedialog erscheinen und verschwinden ohne Bewegung, es gibt keine Ladespinner außer dem Dreipunkt-Text „Wird geladen …“ (er erfüllt die Textregel von Fluent 2 für Spinner) und keine Einblend-Effekte für Inhalte. **Benannte Ausnahme** (Auftraggeber, 27.10.2026, nach der HTML-Vorschau): der Startbildschirm der Windows-App (5.31) mit gezeichnetem Logo, unbestimmtem Balken und dem Übergang in Leiste bzw. Markenspalte; bei „Animationen aus“ entfällt auch dort jede Bewegung. **Gewollte Abweichung von Fluent 2:** das Drücken skaliert 0,97 (taktile Rückmeldung; Fluent animiert dort nur Farbe, Rand und Text). *Umgesetzt (F22, 22.10.2026):* Fläche, Rahmen und Text der Knöpfe wechseln beim Hover in 100 ms mit `(0.33,0,0.67,1)`, Meldung und Seitenleiste ein mit `(0,0,0,1)` (160 bzw. 200 ms), aus mit `(1,0,1,1)` (120 ms). „Animationen aus“ gilt für **jedes** Steuerelement der App: `ReducedMotion` nimmt jedem Übergang die Dauer, auch dem Ein- und Ausblenden (kein 120-ms-Rest); ein Test öffnet alle Seiten und findet keinen Übergang mehr. Nur der Schalter von Avalonia setzt an einem unsichtbaren Teil im Code einen 200-ms-Übergang, den die Vorlage der App nicht zeigt.

### 4.7 Symbole

Ein Symbolsatz: die Pfade in `src/AispaStudio.Desktop/Controls/Icons.cs` (Strichstärke 1,5, Ansichtsfeld 24). Web nutzt dieselben Pfade als inline SVG (`<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5">`). Größen: 16 px im Text und in Knöpfen, 20 px in der Navigation, 24 px nur im Leerzustand. Farbe = Textfarbe des Elternelements. Keine Emoji als Symbole; Unicode-Zeichen nur für ♀/♂, ★, ↻, ✓ wie im Rezeptions-Konzept festgelegt. **Gewollte Abweichung von Fluent 2:** Symbole in Knopf und Feld sind 16 statt 20 (Dichte), und es gibt nur Strichsymbole: die gefüllte Variante für gewählte Zustände entfällt, der gewählte Zustand zeigt Fläche und Text.

## 5. Komponenten

Jede Komponente hat genau diese Zustände: normal, Hover, gedrückt, Fokus (4.5), deaktiviert. Deaktiviert: Deckkraft **0,5**, Cursor Standard, kein Hover. Ein deaktivierter Knopf, dessen Grund nicht selbstverständlich ist, trägt einen Tooltip (5.22) mit Grund und Weg („Verfügbar, sobald ein Name eingetragen ist.“); Beschriftung und Hilfetext eines deaktivierten Feldes bleiben unverändert, die Deckkraft 0,5 gilt nur am Eingabeelement. **Gewollte Abweichung von Fluent 2:** Fluent färbt deaktivierte Elemente mit eigenen Disabled-Farben und zeigt „nicht erlaubt“; der Brief nimmt Deckkraft 0,5, weil die Markenfarben erkennbar bleiben, und den Standard-Cursor.

### 5.1 Knöpfe

Rangfolge: in jeder Karte, jedem Dialog und jeder Seitenleiste gibt es **höchstens einen** primären Knopf. Alles andere ist sekundär, Ghost oder gefährlich. Reihenfolge in einer Leiste von links: primär, sekundär; gefährliche Knöpfe stehen rechts außen, durch `margin-left: auto` getrennt. Ausnahme Rückfragedialog (5.27): dort steht die Aktion links und „Abbrechen“ rechts (Reihenfolge der Windows-Dialoge).

| Art | Web-Klasse | App-Klasse | Fläche | Text | Rahmen | Hover | Verwendung |
|---|---|---|---|---|---|---|---|
| Primär | `button.primary` | `Button.primary` | accent-gradient | on-accent | 1 px accent-hover | Fläche accent-hover (einfarbig), Rahmen accent-hover | die eine Hauptaktion („Speichern“, „Einchecken“) |
| Sekundär (Standard) | `button` ohne Klasse | `Button` ohne Klasse | surface | fg | 1 px line-strong | Fläche surface-2, Rahmen accent | alles Übrige |
| Ghost | `button.ghost` | `Button.ghost` | transparent | fg-2 | transparent | Fläche surface-3, Text fg | Werkzeugleisten, „Mehr“, Schließen, Listenzeilen |
| Gefährlich | `button.danger` | `Button.danger` | transparent | danger | 1 px danger | Fläche danger-soft | Löschen, Stornieren, „Aus dem Team nehmen“ |
| Gefährlich, bestätigt | `button.danger.filled` | `Button.danger.filled` | danger | on-accent | 1 px danger | Fläche danger (Deckkraft 0,9) | **nur** der Bestätigen-Knopf in einem Rückfragedialog (5.27) |
| Erfolg | `button.ok` | `Button.ok` | ok-soft | ok | 1 px ok | Fläche ok-soft, Rahmen ok (Deckkraft 0,9) | „Erledigt“, „Abhaken“ |
| Klein | zusätzlich `.small` | zusätzlich `.small` | wie die Art | 12 px | wie die Art | wie die Art | Zeilen von Tabellen und Listen, Werkzeugleisten |
| Symbol | `button.icon` | `Button.icon` | wie Ghost | Symbol 16 | – | wie Ghost | nur mit `aria-label` bzw. `AutomationProperties.Name` und Hinweis |

Maße: Höhe 34 (klein 28), Innenabstand links/rechts 12 (klein 8), Radius 8, Schrift 14 (klein 12), Gewicht 500, Symbol links mit 6 px Abstand zum Text. Gedrückt: Skalierung 0,97. Geteilter Knopf (`SplitButton.primary`) sieht aus wie Primär mit 1 px Trennlinie `accent-hover` vor dem Pfeil. Mindestbreite eines Knopfs mit Text 96, Text höchstens 26 Zeichen (sonst umbrechen oder die Leiste stapeln). **Umschalt-Knopf** (Filter und Anzeigen, z. B. „Sauna“, „Nur mit Dienst“): gewählt = Fläche accent-soft, Text accent **und** ein Haken-Symbol 16 links (Farbe ist nicht der einzige Träger, 5.8); das Chip-Paar Team/Räume wechselt eine Ansicht und trägt keinen Haken.

**Gewollte Abweichungen von Fluent 2:** Höhe 34, Innenabstand 12, Gewicht 500, Radius 8 und Symbol 16 (siehe 3, 4.2, 4.4, 4.7). Der Hover macht die Fläche dunkler, nicht heller wie in WinUI, damit App und Web gleich wirken (Abschnitt 0).

Knopfleiste einer Maske: links „Speichern“ (primär; deaktiviert, solange Pflichtangaben fehlen), daneben bei einer Karte in der Verwaltung „Verwerfen“ (sekundär, nur mit ungespeicherten Änderungen), bei einer Seitenleiste oder einem Dialog „Abbrechen“ (Ghost; schließt die Maske). In der Seitenleiste steht die Leiste fest im Fuß (Trennlinie `line` oben, Innenabstand 12/16), nicht im scrollenden Inhalt.

**DARF NICHT:** zwei primäre Knöpfe nebeneinander; rot gefüllte Knöpfe außerhalb eines Rückfrage-Dialogs; Knöpfe ohne Text außer Symbol-Knöpfen; Schatten beim Hover; Großbuchstaben.

### 5.2 Eingabefelder

Gilt für Text, Zahl, Datum, Uhrzeit, mehrzeilig.

- Fläche surface, Text fg, Rahmen 1 px line-strong, **Unterkante 1 px fg-3** (Kontrast 3:1, 2.6), Radius 8, Höhe 34 (mehrzeilig mindestens 68), Innenabstand 6 oben/unten, 10 links/rechts, Schrift 14. Platzhaltertext fg-3 ist nicht erlaubt; Platzhalter in fg-2 und nur als Beispiel („z. B. 60“), nie als Beschriftung.
- Hover: Rahmen fg-2 (auch die Unterkante). Fokus: 4.5 (Ring, Rahmen accent und Unterstrich 2 px accent). Nur lesbar: Fläche surface-2, Rahmen line, Cursor Text, Fokus und Kopieren bleiben möglich. Fehler: Rahmen danger, darunter Text 12 px danger mit dem Fehlersatz des Servers. **Warnung am Feld** (z. B. „Gast existiert schon: Mia Muster“): Rahmen warn, Text 12 px warn. **Erfolg am Feld:** Text 12 px ok, Rahmen unverändert. Fehler, Warnung und Erfolg nennen Problem und Weg und ersetzen den Hilfetext. Der Rahmen von Fehler und Warnung behält seine Farbe auch beim Fokus (**gewollte Abweichung von Fluent 2**, wo der Rahmen beim Fokus wieder neutral wird: der Fehler soll beim Korrigieren sichtbar bleiben); Fokus-Unterstrich und Ring bleiben wie in 4.5.
- Beschriftung **immer über** dem Feld (Stufe „Beschriftung“, fg-2), Abstand 4; nie links daneben, nie als Platzhalter. Der Hilfe-Knopf (5.11) steht rechts neben dem Gruppentitel mit 6 px Abstand, nur ausnahmsweise (heikle Wirkung, im PR begründet) rechts neben einer Beschriftung. Hilfetext unter dem Feld in Stufe „Klein“, fg-2, höchstens eine Zeile; bei einem Fehler ersetzt der Fehlertext den Hilfetext. Der Hilfetext ist ein Satz mit Punkt (**gewollte Abweichung von Fluent 2**, dort ohne Punkt: ein Satz bekommt immer seinen Punkt, einheitlich mit Abschnitt 1).
- Web: die Beschriftung ist per `htmlFor`/`id` (`useId`) an das Feld gebunden; ein Hilfe-Knopf steht **außerhalb** des `<label>`, sonst bindet das Label an den Knopf.
- Breiten (Web `max-width`, App `Width`): Zahl 120, PLZ 96, Land 72, Uhrzeit 100, Datum 160, Anrede 160, kurzer Text (Name) 280, Auswahl 220, Betrag 140, E-Mail/Telefon 280, Adresse und mehrzeiliger Text 100 % der Karte. Ein Feld füllt nie „aus Versehen“ die ganze Breite.
- Einheiten stehen in der Beschriftung in Klammern („Dauer (min)“, „Preis (€)“), nicht im Feld. Klammern gibt es **nur** für Einheiten; Erklärungen („gültiger Name“) stehen im Hilfetext. Beschriftung ist ein Substantiv ohne Doppelpunkt. **Gewollte Abweichung von Fluent 2** (dort im Platzhalter): Der Platzhalter verschwindet beim Tippen, die Einheit soll stehen bleiben.
- Pflichtfelder tragen einen Stern `*` in Textfarbe fg hinter der Beschriftung (nie rot) und sind als Pflicht ausgezeichnet (App `AutomationProperties.IsRequiredForForm`, Web `required`); sind fast alle Felder Pflicht, tragen stattdessen die optionalen „optional“. **Gewollte Abweichung von Fluent 2** (dort rot): Rot bleibt die Fehlerfarbe und wird nicht doppelt belegt.
- Kontrollkästchen und Optionsfelder: 16 × 16, `accent-color: var(--accent)` (App: `CheckBox` mit `AccentBrush` als Füllung), Text 14 rechts daneben mit 8 px Abstand; eine Gruppe steht untereinander mit 8 px Abstand oder, bei kurzen Begriffen (Qualifikationen, Standorte), als Zeilenumbruch mit 16 px waagrecht und 8 px senkrecht. Rand 1 px fg-3 (3:1, siehe 2.6); die ganze Zeile samt Beschriftung ist klickbar (Höhe 34). Optionsfelder: eine Gruppe ist **ein** Tab-Stopp (Tab landet auf der gewählten Option, ohne Auswahl auf der ersten); Pfeile bewegen den Fokus **und wählen**, Strg+Pfeil bewegt nur den Fokus, die Leertaste wählt, kein Umlauf am Ende; die häufigste Option ist vorgewählt und steht an erster Stelle.
- Verhalten aller Felder (Betreten, Tippen, Esc, Enter, Einfügen): 5.16. Datum, Uhrzeit, Zahl und Betrag haben eigene Abschnitte (5.17 bis 5.20).

### 5.3 Auswahlfelder

- Aufklappliste (`select`, `ComboBox`): wie Eingabefeld, rechts ein Pfeil (Symbol 16, fg-2) mit 10 px Abstand. Die geöffnete Liste: Breite wie das Feld, Fläche surface, Rahmen 1 px line, Radius 8, Schatten `shadow`, Eintrag 32 hoch (Listenzeilen in 5.6 bleiben 40, WinUI-Listenzeile), höchstens 8 Einträge sichtbar, dann Scrollleiste. Tasten: Enter oder Alt+↓ öffnet, ↑/↓ markieren **ohne zu wählen**, Enter wählt und schließt, Esc schließt ohne Änderung, Buchstaben springen zum Eintrag; die Leertaste wählt nur, wenn nicht gefiltert wird. Freie Eingabe (z. B. Titel): gültig = übernehmen, ungültig = Fehler am Feld, der Text bleibt (kein stilles Zurücksetzen).
- Auswahl: 2 bis 4 Werte Optionsfelder, bis etwa 12 Werte Aufklappliste, darüber (oder frei erweiterbar) Eingabe mit Vorschlagsliste; Auswahl-Chips nur für höchstens sechs häufige Werte; Mehrfachauswahl über sechs Werte ist ein Tag-Picker (5.26). Die segmentierte Schiene (5.4) ist kein Ersatz für Optionsfelder, sie wechselt Ansichten. **Gewollte Abweichung von Fluent 2:** Fluent und WinUI widersprechen sich (Optionsfelder bis etwa 5 bzw. Aufklappliste ab 9); der Brief behält 4 und 12, das genügt im Alltag.
- Auswahl-Chip (mehrere wählbar, z. B. Wochentage): Höhe 32, Radius 999, Innenabstand 4/12, Rahmen 1 px line, Text fg-2; gewählt: Fläche accent, Text on-accent, Rahmen accent; Hover: Fläche surface-2.
- Auswahl-Karte mit Erklärung (z. B. Zahlungskonto): Rahmen 1 px line, Radius 8, Innenabstand 12/16, Titel 14/500, Erklärung 12 fg-2; gewählt: Rahmen accent, Fläche accent-soft.
- Listenauswahl (links in Stammdaten): siehe 5.6.

### 5.4 Reiter

- Schiene: Fläche surface-3, Radius 10, Innenabstand 3, Abstand zwischen Reitern 2.
- Reiter: Höhe 28, Innenabstand 0/12, Radius 8, Text 14/500 fg-2, Fläche transparent. Hover: Text fg. Gewählt: Fläche surface, Text accent, Schatten `0 1px 2px rgba(0,0,0,0.13)`. Fokus 4.5.
- Web: `role="tablist"`, `role="tab"`, `aria-selected`; Pfeiltasten wechseln den Reiter, Pos1 und Ende springen zum ersten und letzten; die Schiene ist ein Tab-Stopp, der Inhalt steht in einem `role="tabpanel"` (Komponente `Tabs`, Befund B3). App: dieselbe Schiene mit `Border.segmented` und `Button.on`.
- Reiter stehen in einer Zeile. Passen sie nicht, scrollt die Schiene waagrecht; sie bricht nie um.
- **Alltag vor Verwaltung** (*R07 Slice 143, Ä353*): Hat eine Maske Reiter für den Alltag und für die Verwaltung, stehen die Alltagsreiter links, danach ein Trenner (1 × 16 px, `line`, Abstand 6) und die Verwaltungsreiter; ohne Verwaltungsreiter fehlt der Trenner. Reiter, die ein Recht verlangen, erscheinen ohne das Recht nicht. Beispiel Lager: Übersicht · Bestand · Bestellen · Chargen · Bewegungen │ Inventur · Lagerorte · Lieferanten · Verbrauch.
- **Gewollte Abweichung von Fluent 2** (Unterstrich 2 px, Höhe 44): Die Schiene mit Fläche und gewähltem Reiter ist kompakter und trägt die Marke.

### 5.5 Karten

- Karte: Fläche surface, Rahmen 1 px line, Radius 12, Schatten `shadow`, Innenabstand 16/20. Titel Stufe „Überschrift“ mit Hilfe-Knopf; darunter 12 px Abstand. Zwischen Karten 16 px.
- Verschachtelte Karte (Gruppe in einer Karte, z. B. „Preise nach Wochentag“): Fläche surface-2, Rahmen 1 px line, Radius 8, kein Schatten, Innenabstand 12/16, Titel Stufe „Zwischentitel“.
- Kennzahl-Kachel: Fläche surface, Rahmen 1 px line, Radius 12, kein Schatten, Innenabstand 12/16; Beschriftung oben (Stufe „Beschriftung“, fg-2), Wert darunter (Stufe „Zahl groß“, tabular), Vergleich darunter (Stufe „Klein“, fg-2). Kacheln stehen in einem Raster mit Mindestbreite 160 und 12 px Abstand.
- Abschnitt auf einer Fläche (Kundenkarte, *festgelegt 04.11.2026, R07 Slice 141, Ä349*): statt vieler gerahmter Karten eine Karte (Innenabstand 20/24) mit Abschnitten, getrennt durch eine Linie oben 1 px line, Innenabstand 12/0. Kopf des Abschnitts: Bezeichnung Stufe „Beschriftung“ fg-2 links, rechts ein Symbol-Knopf (Stift bzw. Plus, Ghost 34 × 34), der erst bei Hover oder Fokus im Abschnitt sichtbar wird (Deckkraft 0 → 1) und in der Tab-Reihenfolge bleibt. Werte darunter in fg (`TextBlock.value`), keine Daten in fg-3. Ein Abschnitt erscheint nur, wenn etwas gesetzt ist oder gerade bearbeitet wird; Fehlendes steht in einer Zeile „Noch nicht erfasst:“ (fg-2) mit Links „+ …“ (5.24), deren Erklärung im Tooltip steht. Status je Termin im Verlauf als Status-Pille (5.8), nur bei Abweichung (nicht erschienen, storniert `crit`, eingecheckt). Kopierbare Werte (Telefon, E-Mail) sind schlichte Knöpfe ohne Fläche in fg mit Symbol 16 fg-2, unterstrichen bei Hover und Fokus; ein Klick kopiert.
- Modal und Rückfragedialog (Web **und** Windows-App, nur für Rückfragen und PIN): Breite 420, Radius 12, Innenabstand 24, Overlay `rgba(0,0,0,0.45)`. Titel Stufe „Überschrift“, darunter Text, unten Knopfleiste; Aufbau, Knopfreihenfolge und Fokus vollständig in 5.27. Esc schließt.
- **Gewollte Abweichung von Fluent 2** (Karte Radius 4 bis 6, Innenabstand 8, 12 oder 16, Schatten 4): Karte Radius 12 und Innenabstand 16/20 gehören zur Marke und zum Raster des Briefs.

### 5.6 Listen (linke Spalte der Stammdaten, Zugänge, Lager-Artikel)

- Listenkarte wie 5.5, Innenabstand 4.
- Zeile: Höhe mindestens 40, Innenabstand 10/12, Radius 8, Text 14 fg, Untertitel 12 fg-2 in zweiter Zeile. Hover: Fläche surface-2. Gewählt: Fläche accent-soft, Text fg, links 3 px accent. Fokus 4.5.
- Gruppentitel in der Liste (Kategorie): Stufe „Beschriftung“, fg-2, 12 px Abstand darüber, 4 darunter.
- „+ Neu …“ ist die letzte Zeile, Text accent, Gewicht 500, Symbol Plus 16.
- Breite der Liste: 240 px im Web, 260 in der App. Rechts daneben die Karte mit dem Formular, Abstand 16.

### 5.7 Tabellen

- Schrift 14, Ziffern tabular. Kopf: Stufe „Beschriftung“ fg-2, Rahmen unten 1 px line-strong, bleibt beim Scrollen oben (`position: sticky`).
- Zeile: Höhe mindestens 36, Rahmen unten 1 px line, Hover Fläche surface-2 (nur wenn die Zeile anklickbar ist). Kein Zebra.
- Zahlen, Beträge, Uhrzeiten rechtsbündig, **und die Kopfzelle derselben Spalte auch**. Text linksbündig. Einheit in der Kopfzelle, wenn alle Zeilen dieselbe haben („Bestand (ml)“); sonst in der Zelle.
- Negative Beträge in danger. Summenzeile: Gewicht 600, Rahmen oben 1 px line-strong.
- Aktionen in Zeilen: Symbol-Knöpfe klein (28) rechts außen, nie rote Textknöpfe je Zeile.
- Leere Tabelle: eine Zeile „Noch keine Einträge.“ in fg-2.
- **Gewollte Abweichung von Fluent 2** (Zeilenhöhe 34 oder 44, Zellabstand 0/8): Zeile mindestens 36 und Zelle 8/12 passen zum Raster des Briefs.

### 5.8 Chips und Pillen

| Art | Höhe | Radius | Innenabstand | Schrift | Fläche / Text / Rahmen |
|---|---|---|---|---|---|
| Status-Pille (gebucht, offen, bezahlt) | 20 | 999 | 2/8 | Mini | surface-3 / fg-2 / –; Varianten: `booked` st-booked-bg/st-booked, `ok` ok-soft/ok, `warn` warn-soft/warn, `crit` danger-soft/danger, `accent` accent-soft/accent |
| Kopfzeilen-Chip (jedes Element der Kopfzeile außer dem primären Knopf: Zähler „24 Termine · 3 da“, Sauna, Warteliste, **Schließzeit „Geschlossen · Betriebsurlaub“** (*Runde 5b, Block L, 22.10.2026*: Symbol Schloss, Betriebsurlaub `on`, Sonderschließung `warn`, Ausfall `crit`), Personen-/Raumfilter, „Meldungen“, „Kalender“ (Kalender-Abos; ein Punkt 7 px accent rechts im Text, wenn Abos für diesen Zugang ausgeblendet sind, *Runde 5b, Ä43*), Umschalter Team/Räume als Chip-Paar) | 34 | 999 | 0/12, Symbol 16 links mit 6 px Abstand | 14/400 (Umschalter gewählt 600) | surface / fg / line; Zustände nur über die weiche Fläche: `warn` warn-soft/warn/warn (Warteliste mit Einträgen, Meldungen mit Warnungen), `crit` danger-soft/danger/danger (Meldungen mit Fehlern), `on` accent-soft/accent/accent (aktiver Filter, gewählter Umschalter; Umschalt-Chips wie Sauna und Nur mit Dienst tragen im Zustand `on` zusätzlich ein Haken-Symbol 16 links, siehe 5.1). Keine anderen Rahmen, Höhen oder Schriftgewichte in einer Kopfzeile. *Festgelegt 21.10.2026 (Ä17).* |
| Auswahl-Chip | 32 | 999 | 4/12 | 14/400 | siehe 5.3 |
| Info-Chip (gesetzte Angabe, nicht anklickbar: Wunsch, Kennzeichen, „★ Stammgast“ auf der Kundenkarte) | mind. 26 | 999 | 3/10, 6 px Abstand rechts und unten | 14/400 | surface-2 / fg / line; `warn` warn-soft / fg / warn mit Warndreieck 14 warn links (Farbe nie allein, 5.9), etwa „Nicht behandeln: Füße“; ein Kreuz zum Aufheben (20 × 20, Symbol 12) nur mit Recht; Wirkung, Grund oder gekürzter Text im Tooltip. *Festgelegt 04.11.2026 (R07 Slice 141, Ä349).* |
| Kennzeichen am Termin (!, ★, N, +10) | 16 | 999 | 0 | Mini 600 | surface / fg / line-strong; `note` warn-soft/warn; `regular` warn |
| Initialenkreis (Person) | 30 | 999 | 0 | 12/600 | Personenfarbe / #ffffff |
| Dienst-Chip (Dienstplan, *R07 Slice 142, Ä350*) | Text + 4 | 999 | 2/6 | 12/400, geplanter Tag 600 | Personenfarbe zu 22 % (hell) bzw. 45 % (dunkel) in `roster-chip-base` gemischt, Tinte `roster-chip-ink` (2.4, *T-039, Ä372*; hell gleich der früheren Aufhellung um 78 % zu Weiß); Kontrast mindestens 4,5:1, sonst sinkt der Anteil in 5-Punkt-Schritten (`RosterWeekLayoutTests` rechnet es für neun Farben nach); füllt die Breite der Zelle |
| Abwesenheitsbalken (Dienstplan, *R07 Slice 142*) | 24 | 999 | 0/10 | 12/600 | Urlaub accent-soft/accent, Krank danger-soft/danger, Schulung warn-soft/warn, Frei surface-3/fg-2; liegt über den Tageszellen und nimmt keine Klicks an |
| Wertezeile (Zahlen, *R07 Slice 144, Ä352*) | – | – | 0/40 (rechts), 8 (unten) | Wert 22/28 600 tabellarisch, Bezeichnung 12/16 400 | Wert fg, Bezeichnung fg-2 (mindestens 4,5:1 auf der Fläche, `ViewTests.ReportsLayout` rechnet es in beiden Farbschemata nach); Wert über der Bezeichnung, die Werte umbrechen als Reihe |
| Zeitleiste des Personals (Tagesstart, *R07 Slice 144*) | 10 | 5 | 0/1 je Abschnitt | – | Leiste surface-3, Dienstabschnitt accent (mindestens 3:1 gegen die Leiste, Test rechnet es nach); reicht von Öffnung bis Schluss, die Uhrzeiten stehen als Text daneben und darüber, Abwesende zeigen statt der Leiste die Pille (Zustand, z. B. „Urlaub“) |
| Status-Chip im Lager (*R07 Slice 143, Ä351*) | 20 | 999 | 2/8 | 12/500 | Pille wie oben: `warn` für „nachbestellen“ (Bestand unter dem Mindestbestand oder nur noch für höchstens 5 Behandlungen), `crit` für „abgelaufen“; ohne Anlass steht kein Chip (kein „ok“-Chip) |
| Bestandsbalken (Lager, *R07 Slice 143*) | 4 | 2 | – | – | Spur surface-3, Füllung accent, unter dem Mindestbestand warn; voll beim Doppelten des Mindestbestands, ohne Mindestbestand kein Balken. Der Balken ist nur ein Bild: Zahlen und Zustand stehen im Namen der Zeile |
| Raumkürzel | 20 | 8 | 0/6 | Mini | surface-3 / fg-2; Paarraum accent-soft/accent |
| Tastenkürzel | 20 | 4 | 0/5 | Mini | surface-2 / fg-2 / line-strong, unten 2 px |

### 5.9 Hinweise

Ein Hinweis ist eine Fläche mit Text, Radius 8, Rahmen 1 px, Innenabstand 10/12, Symbol 16 links mit 8 px Abstand, Text 14.

| Art | Fläche | Rahmen | Text | Symbol |
|---|---|---|---|---|
| Neutral (Erklärung, „Leer lassen, wenn …“) | accent-soft | line | fg | Info |
| Erfolg („Gespeichert.“) | ok-soft | ok | ok | Haken |
| Warnung („Personal nur zu 38 % ausgelastet“) | warn-soft | warn | warn | Dreieck |
| Fehler (Validierung, Server) | danger-soft | danger | danger | Kreis mit X |

Eine InfoBar (5.15) ist ein Hinweis nach dieser Tabelle mit Schließen-Kreuz und höchstens einer Aktion. Warnungen sind **nie** rot. Ein Erklärtext ohne Fläche (z. B. „Leer lassen, wenn der Grundpreis gilt.“) steht als Stufe „Klein“ fg-2 direkt unter dem Titel, nicht als Hinweis.

### 5.10 Meldung unten (Toast)

Fläche toast, Text toast-fg, Radius 8, Innenabstand 10/16, unten mittig mit 16 px Abstand, Schrift 14; keine Varianten: Warnung und Fehler stehen nicht im Toast, sondern als InfoBar oben im Kalender (5.15). Enthält höchstens einen Knopf („Rückgängig“, Ghost mit Text toast-fg) und ein Kreuz. Ein Toast zugleich, ein neuer ersetzt den alten, eine Zeile mit höchstens **60 Zeichen** und **ohne Schlusspunkt** (Ergebnis in der Vergangenheit: „Termin verschoben auf Do 14:30“, „Gespeichert“; Fluent 2). Verschwindet nach 5 s, mit Knopf „Rückgängig“ nach 8 s. Der Mauszeiger darüber hält ihn an. Beide Oberflächen identisch; im Kalender mittig im Kalenderbereich. **Gewollte Abweichung von Fluent 2** (7 s, feste Ecke, bis zu vier Toasts): 5 s (8 s), mittig und ein Toast zugleich sind Projektfestlegungen (meldungen.md M1).

### 5.11 Hilfe-Knopf

22 × 22, Kreis, Fläche surface, Rahmen 1 px line-strong, Text „?“ 12/600 fg-2. Hover: Rahmen und Text accent. Steht rechts neben dem Titel einer Gruppe oder Karte mit 6 px Abstand und öffnet die Hilfe genau zum Abschnitt der Gruppe (ADR 0055, ADR 0067). Jede Gruppe und jede Karte mit Einstellungen hat einen, höchstens einen; jedes Feld steht trotzdem im Hilfethema. Ein zusätzlicher Knopf an einem einzelnen Feld ist die begründete Ausnahme.

### 5.12 Seitenleiste (verbindliches Layout, Stand 21.10.2026 abends; Vorbild: Warteliste)

Gilt für jede Seitenleiste der Windows-App und des Web (Termin, Neuer Termin, Paar/Spa-Tag, Freie Zeit, Wunsch, Warteliste, Meldungen, Raumpflege, Kundendaten, Raum-Infos, KI und Hilfe) und für jede, die neu dazukommt. Abweichungen zuerst hier eintragen.

1. **Maße:** Breite 360 px, von rechts, Fläche surface, Rahmen links 1 px line, kein Schatten; Bewegung nach 4.6 (200 ms ein, 120 ms aus). Nie zwei Seitenleisten übereinander; eine neue ersetzt die offene, die alte bleibt als Reiter erreichbar (Punkt 7).
2. **Kopf** (`SidePanelHeader`): Innenabstand 16/20; Titel Stufe „Überschrift“ (18/600), Untertitel Stufe „Klein“ fg-2 (Standort, Datum, Behandlung); rechts oben der Symbol-Knopf „Schließen“ 34 × 34 (Ghost, Kreuz 16). Links vom Titel darf ein Initialenkreis (30) oder ein Symbol (20) stehen; Stift-Symbol-Knöpfe zum Bearbeiten stehen rechts vom Titel mit 8 px Abstand. Unter dem Kopf ein Trenner 1 px line.
3. **Chip-Zeile** (optional, direkt unter dem Kopf, Innenabstand 8/20): Status-Pillen und Dauer nach 5.8, 8 px Abstand.
4. **Erklärsatz** (optional): ein Satz Stufe „Text“ fg-2 mit 12 px Abstand, nur wenn die Seite ohne ihn unklar wäre.
5. **Inhalt** als gestapelte Karten (5.5, verschachtelte Variante: Fläche surface, Rahmen 1 px line, Radius 12, Innenabstand 12/16), Abstand 12 px, Seitenrand 16 px. Reihenfolge fest: zuerst die Hauptkarte (das Objekt: Zeit und Ablauf, der Gast, die Meldung), dann Hinweise (5.9), dann Listen. In einer Karte: Titel Stufe „Text“ 500 in der ersten Zeile, rechts oben eine Status-Pille (5.8), darunter Zeilen Stufe „Text“ fg mit Symbol 16 fg-2 links (Uhr, Person, Tür, Telefon), Nebensächliches Stufe „Klein“ fg-2, Hervorzuhebendes („Frei: Mi. 21.10. 18:00 bei Marco Berger“) in accent 500. Felder einer Maske folgen 5.2 (Beschriftung über dem Feld, Breitenklassen); Feldgruppen haben keinen Trenner, nur 20 px Abstand und ihren Zwischentitel.
6. **Knöpfe:** in der Karte unten eine Zeile mit 8 px Abstand: links sekundär („Freie Zeiten zeigen“), rechts primär („Buchen“), darunter Ghost-Knöpfe („Erledigt …“, „Mehr“). Je Seitenleiste **genau ein** primärer Knopf: die eine Hauptaktion des Objekts (Einchecken, Kassieren, Buchen, Übernehmen, Raumpflege erledigt). Im Termin-Panel ist das Einchecken bzw. Kassieren; solange eine Karte zum Bearbeiten offen ist (Zeiten, Notiz, Wiederholen, Verschieben), ist deren Bestätigen der primäre Knopf und Einchecken/Kassieren sind sekundär. Karten, die von selbst offen stehen („Gast eintragen“, „Gruppe einchecken“), haben sekundäre Knöpfe; „Abbrechen“ ist Ghost (5.1) (*R01 Slice 21, W53, 25.10.2026*). Der Aktionsblock der Seite steht nach den Karten mit 16 px Abstand: primär in voller Breite, darunter sekundäre in voller Breite, „Mehr“ als Ghost mit Pfeil; gefährliche Aktionen nur unter „Mehr“ oder in einem Rückfrage-Dialog.
7. **Reiter unten** (Windows-App): am unteren Rand der Seitenleiste eine Reiterschiene (5.4) in voller Breite, Fläche surface-2, Rahmen oben 1 px line, Innenabstand 4/8; je offene Seite ein Reiter mit Symbol 16 und Text 12/500, der gewählte mit Fläche surface und Text accent. Ein Klick wechselt, das Kreuz schließt nur die gewählte. Die Reiter lassen sich nicht verschieben, herauslösen oder als Fenster öffnen. Höchstens sechs Reiter; der siebte ersetzt den ältesten nicht gewählten. Tastatur: Strg+Tab wechselt nach rechts, Strg+Umschalt+Tab nach links. *Ergänzt 21.10.2026 beim Bauen (Schritt 15):* je **Art** von Seite ein Reiter (Termin, Neuer Eintrag, Warteliste, Freie Zeit, Ausfall, Wunsch, Raumpflege, Meldungen, Meldung; *Schritt 16, 21.10.2026:* Tagesstart ist keine eigene Seite mehr, sondern eine Meldung mit Detail); öffnet man einen zweiten Termin, ersetzt er den Reiter des ersten, damit Klicken durch den Tag keine Reiter häuft. Die Schiene steht auch bei einer einzigen Seite da. Rechts bleiben 60 px frei (Innenabstand 4/8/60/4), weil der Knopf „KI und Hilfe“ über der Ecke der Leiste schwebt; passen die Reiter nicht, scrollt die Schiene waagrecht. Beim Wechsel des Standorts schließen alle Seiten samt Reitern. *Geändert (Runde 6, Block N3, Ä66, 23.10.2026, Auftraggeber):* „KI und Hilfe“ ist selbst ein Reiter dieser Schiene (Art Assistent, Symbol AISPA-Zeichen, Text „KI und Hilfe“) und macht den anderen **nicht** mehr Platz; Einzelheiten in 5.28. *Geändert 21.10.2026 (Ä15, Auftraggeber):* Der zuletzt geöffnete Reiter steht immer **ganz links**; sichtbar sind höchstens **zwei** Reiter (der gewählte und der davor benutzte); die übrigen offenen Seiten erreicht man über einen Chip rechts in der Schiene mit Zähler („⋯ 3“), der eine Klappliste mit allen offenen Seiten öffnet (Muster: Blattnavigation in Excel); daneben Pfeile links/rechts zum Blättern durch die Reihe, Strg+Tab bleibt. Die Schiene scrollt nicht mehr waagrecht; die 60 px rechts bleiben für „KI und Hilfe“ frei. *Beim Bauen festgelegt (Schritt 17, 21.10.2026):* Die Zahl im Chip nennt die Seiten, die **nicht** als Reiter sichtbar sind („⋯ 2“ bei vier offenen). Sichtbar sind der gewählte und der davor benutzte Reiter (fehlt der, der nächste in der Reihe), in der Reihenfolge der Reihe. Ein Klick auf einen Reiter, auf einen Eintrag der Klappliste oder auf einen Pfeil wählt nur und ändert die Reihenfolge nicht; nur eine neu geöffnete Seite rückt nach ganz links. Die Pfeile (28 × 28, Symbol 16) und Strg+Tab beginnen am Ende wieder vorn; Chip und Pfeile erscheinen erst, wenn mehr als zwei Seiten offen sind. Die Klappliste nennt alle offenen Seiten (Symbol 16, Text 12/500, die gewählte mit accent). *Beim Bauen festgelegt (Ä19, 22.10.2026, Blöcke B bis D):* Die Schiene ist so breit wie die Seitenleiste ohne den Rahmen links (1), Innenabstand links (8) und die freien 60 px rechts, also 291 px bei 360. Der Chip „⋯ n“ samt Pfeilen und Abständen braucht 112 px, sobald mehr als zwei Seiten offen sind; der Rest teilt sich auf die zwei sichtbaren Reiter. Ein Reiter ist mindestens **96 px** breit (Symbol 16, Innenabstand 8 + 8, Abstand 6 und etwa sechs Zeichen); bleibt weniger Platz je Reiter, zeigen beide **nur das Symbol** (Tooltip und Name nennen die Seite), das ist bei 360 px ab drei offenen Seiten der Fall. Darüber steht der Text mit „…“ gekürzt (Höchstbreite = Reiterbreite − 38, also Symbol, Innenabstand und Abstand abgezogen). Tastatur der Schiene (Fluent 2, F15): die Schiene ist **ein** Tab-Stopp (der gewählte Reiter), ← → wechseln zum Nachbarn (am Ende von vorn), Pos1 und Ende zum ersten und letzten, Enter und Leertaste wählen, **Strg+W** schließt die gewählte Seite (mit der Rückfrage aus 5.27, wenn sie Änderungen hat), **Strg+1** bis **Strg+8** wählen den n-ten Reiter der Reihe, **Strg+9** den letzten; im Wunsch-Feld mit Vorschlägen buchen Strg+1 bis Strg+3 weiter den Vorschlag. *Geändert (Runde 5, Ä29, 22.10.2026, Auftraggeber):* Die Regel „höchstens zwei sichtbar“ gilt nur für Reiter **mit Text**. Zeigen die Reiter nur das Symbol (ab 96 px zu wenig Platz für Text, bei 360 px ab drei offenen Seiten), sind bis zu **fünf** zugleich sichtbar (5 × 32 + 4 × 2 = 168 px; mit Chip und Pfeilen (112) passen sie in die 291 px der Schiene); Chip „⋯ n“ und Pfeile erscheinen erst, wenn mehr als fünf Seiten offen sind (bei sechs „⋯ 1“, die Zahl nennt die Seiten, die nicht in der Schiene stehen). *Beim Bauen festgelegt:* Sichtbar sind immer der gewählte und der davor benutzte Reiter; den Rest füllen die zuletzt geöffneten auf, in der Reihenfolge der Reihe. Mit Text bleibt es bei zwei, bei „⋯ n“ ab der dritten Seite wie bisher (die Breite je Reiter rechnet dabei mit Chip und Pfeilen). Strg+Tab und die Klappliste erreichen jede Seite weiter, auch wenn kein Chip steht.
8. **Zustände:** Laden „Wird geladen …“ Stufe „Text“ fg-2 anstelle des Inhalts; leer ein Satz Stufe „Text“ fg-2 mit Symbol 24 fg-3 darüber („Keine Meldungen.“); Fehler ein Hinweis „Fehler“ (5.9) mit dem Satz des Servers und einem sekundären Knopf „Erneut laden“; ohne Verbindung ein Hinweis „Warnung“ mit „Nur mit Verbindung“ an den betroffenen Knöpfen (deaktiviert, Deckkraft 0,5).
9. **Tastatur und Fokus:** Beim Öffnen bekommt das erste Bedienelement der Hauptkarte den Fokus, Esc schließt die gewählte Seite und gibt den Fokus an den Auslöser zurück (ADR 0066), Tab läuft Kopf, Chips, Karten, Aktionsblock, Reiter.
10. **Verboten:** farbiger Fließtext als Meldung, Text ohne Karte, zwei primäre Knöpfe, Inhalte über die Breite hinaus (Umbruch statt Kürzen), eigene Schriftgrößen.
11. **Notizen und Kunde (Runde 5, Block I, 22.10.2026, Auftraggeber; Ä31, Ä33, Ä34):**
    - *Meldungen, Gruppe „Notizen heute“* (ersetzt das Band „Heute“ über dem Kalender, Auftraggeber: „Meldungen sind oben, man kann alle MA einblenden“): ganz oben in der Seitenleiste, Zwischentitel 14/600 mit Zähler-Pille, rechts „+ Notiz“ (Ghost, klein). Die Tagesnotizen ohne Uhrzeit stehen als Zeilen: Kontrollkästchen, Text (abgehakt durchgestrichen, Deckkraft 0,6), Stift und Papierkorb (je 28 × 28, Symbol 16; Löschen fragt nach, 5.27). „+ Notiz“ und der Stift öffnen eine Karte (5.5, Innenabstand 12/16) mit mehrzeiligem Feld „Notiz“ (5.16: Höhe 68, 500 Zeichen, Zähler), Hilfetext „Für alle am Standort sichtbar.“ und zwei gleich breiten Knöpfen „Abbrechen“ (links) und „Notiz speichern“ bzw. „Änderung speichern“ (rechts, primär, Standardknopf, Strg+Enter). Während ein Feld offen ist, sind Ändern, Löschen und „+ Notiz“ gesperrt. Ohne Notizen zeigt die Gruppe nur „+ Notiz“; bei einem Filter auf eine Spalte und ohne Buchungsrecht ohne Notizen fehlt sie. Notizen sind keine Meldungen: sie zählen nicht am Chip „Meldungen“, der Chip trägt aber bei Notizen einen Punkt (Symbol Notiz 10, accent) rechts neben dem Zähler; Name für Screenreader „Meldungen 2, 1 Notiz heute“. Die Taste „.“ (und „Tagesnotiz“ im Menü) öffnet die Seitenleiste und setzt den Fokus ins Notizfeld; Esc stellt den Text zurück, das zweite Esc schließt das Feld, das dritte die Seitenleiste; danach steht der Fokus auf „+ Notiz“. Notizen mit Uhrzeit bleiben Zettel in der Spalte, Abwesenheiten zeigen die Spalten und, mit offenen Terminen, die Meldung „Abwesenheit“.
    - *Termin, Notiz zum Termin:* unter den Terminangaben (in der ersten Karte) ein `Button.link` „Notiz hinzufügen“ (5.24); mit Notiz die bekannte Hinweisfläche „warn“ (5.9) mit dem Stift-Knopf (28 × 28) rechts. Beim Schreiben steht statt der Hinweisfläche eine Karte mit dem Feld „Notiz zum Termin“ (mehrzeilig wie oben, Hilfetext „Nur für diesen Termin.“) und denselben zwei Knöpfen („Notiz speichern“ als Standardknopf). Ein leeres Feld entfernt die Notiz beim Speichern. Notiz und „Zeiten dieses Termins ändern“ sind nie zugleich offen, weil zwei Standardknöpfe Strg+Enter mehrdeutig machten. Beim Speichern und Abbrechen geht der Fokus auf den Stift bzw. den Link zurück.
    - *Termin, Abschnitt „Kunde“* (Akkordeon, 5.29) unter den Terminangaben und der Notiz: Karte (5.5, Innenabstand 0), Kopf mit Symbol Person, „Kunde“ 14/500 und Pfeil; darunter zwei Spalten mit vier Kennzahlen (Stufe „Zahl groß“ 22/600 tabular, Beschriftung Stufe „Klein“ fg-2, wie der Tagesüberblick, Abschnitt 7): „Besuche gesamt“, „Letzter Besuch“, „Nicht erschienen“ (warn, wenn nicht 0), „Nächster Termin“; Daten kurz („3.10.“, anderes Jahr „3.10.25“) mit dem vollen Datum im Hinweis. Mit dem Recht „Berichte mit Beträgen“ eine Zeile „Offen 45,00 € · Paket Massage: 7 von 10 offen“. Aufgeklappt: Kundennotiz als neutraler Hinweis (5.9) auf zwei Zeilen gekürzt (voller Text im Hinweis ab 80 Zeichen), Zeile „Anamnese vorhanden/fehlt“ (nur der Stand), Gruppe „Zuletzt“ (bis zu fünf Behandlungen; Datum, Behandlung, Person; die Zeile ist ein Knopf und öffnet den Termin), Gruppe „Kontakt“ mit `Button.link`: Telefonnummer (`tel:`), „Per WhatsApp schreiben“ (`https://wa.me/<Ziffern>`, nur mit Telefonnummer), E-Mail (`mailto:` mit dem Gast im Betreff); neben Telefon und E-Mail ein Kopieren-Knopf (28 × 28), Rückmeldung als Toast „Kopiert“. Zuletzt „Kundenkarte öffnen“ (Link, Strg+Enter im Abschnitt). Ein Gast ohne Namen hat den Abschnitt nicht.

### 5.13 Navigation

- App: linke Leiste 88 px, Fläche rail, Einträge 20-px-Symbol über Text 12/500, Zeile Radius 12, Innenabstand 10/2; Hover rail-hover; gewählt rail-selected mit 3-px-Strich rail-mark links. Oben das Markensymbol (Abschnitt 1), unten Verbindungsstand und Konto.
- Web im Browser: **eine** Kopfzeile, 48 px, Fläche rail, Markenname links (rail-fg, 600), Seiten als Textknöpfe (rail-fg-2, gewählt rail-selected mit Unterstrich 2 px rail-mark), rechts das Konto als Menü (Name · Rolle, darin Passkey, PIN, Windows-App verbinden, Hilfe, Abmelden). Die heutige zweite Zeile entfällt; „Verbunden · Version“ wandert ins Kontomenü.
- Web eingebettet in der App (`?eingebettet=1`): keine Kopfzeile. Oben nur die Reiterschiene der Seite, Seitenrand 16.

### 5.14 Masken (Aufbau)

Vollständig in [gestaltung/masken.md](gestaltung/masken.md) (R1 bis R13, Kurz-Checkliste in Abschnitt 6 dort); jede neue oder geänderte Maske hakt sie ab. Kurz: Gruppen mit Zwischentitel (16 px, `h3`) und einem Hilfe-Knopf, Häufiges oben; Seltenes in „Weitere Angaben“ (`details`), öffnet von selbst, wenn befüllt; eine Hauptspalte, Paare nur für Kurzfelder, höchstens drei Felder je Zeile; Feldbreite nach 5.2; Knopfleiste nach 5.1; Fehler am Feld nach 5.2, ein maskenweiter Fehler als InfoBar über der Knopfleiste. Beim Speichern mit Fehlern bekommt das **erste ungültige Feld** den Fokus, nicht die InfoBar; die InfoBar wird zusätzlich angezeigt (Entscheidung des Auftraggebers, ADR 0069; **gewollte Abweichung von Fluent 2**, das der Leiste den Fokus gibt: Der Fokus am Feld spart den Weg zum Fehler). Verhalten der Felder: 5.16 bis 5.20. Alle Werte aus den Abschnitten 3 bis 5, keine eigenen.

### 5.15 Meldungen (Arten)

Vollständig in [gestaltung/meldungen.md](gestaltung/meldungen.md) (M1 bis M6); Werte hier. Welche Art wofür: Toast (5.10) bestätigt eine eigene Aktion; **InfoBar** zeigt einen Zustand der Seite, der bleibt; Hinweis am Objekt gilt für genau einen Termin, Gast oder Raum und steht dort; Feldfehler am Feld; Dialog (5.5), wenn entschieden werden muss; Tagesnotiz ist keine Meldung (Gruppe „Notizen heute“ ganz oben in der Seitenleiste „Meldungen“, 5.12 Punkt 11; sie zählt nicht in der Zahl am Chip „Meldungen“).

- InfoBar: Hinweis nach 5.9 (Schweregrad Neutral, Erfolg, Warnung, Fehler; Symbol und Farbe zusammen), rechts höchstens eine Aktion (Knopf, klein) und ein Schließen-Kreuz (Symbol-Knopf klein, 28 × 28). Warnung und Fehler tragen immer eine Aktion oder einen Link. Sie steht oben im Inhalt der Seite, in einer Seitenleiste oben unter dem Kopf, in einer Maske über der Knopfleiste. Sie nimmt den Tastaturfokus nicht an sich (Fokus bei Formularfehlern: 5.14).
- Höchstens zwei sichtbar, sortiert nach Schweregrad (Fehler, Warnung, Erfolg, Neutral); weitere als Zeile „n weitere Meldungen“. Eine geschlossene Warnung kommt wieder, wenn der Zustand bleibt (Wechsel von Tag oder Standort, nächstes Laden).
- Text: Problem plus Weg, ohne Schuld, kein „erfolgreich“, keine Ausrufezeichen, Knopf als Verb. Fehler und Warnungen sind für Screenreader assertive, Info und Erfolg höflich.

### 5.16 Verhalten der Eingabefelder

Gilt für Textfeld, Zahlenfeld (5.19), Betragsfeld (5.20), Uhrzeitfeld (5.18), Datumsfeld (5.17) und Auswahlfeld (5.3) in der Windows-App. Jedes Feld steht in einem gemeinsamen Rahmen (Beschriftung über dem Feld, Hilfetext oder Fehlertext darunter, Hilfe-Knopf nur nach 5.11); Name, Hilfetext und Fehler sind an die Eingabe gebunden (`AutomationProperties.LabeledBy`, `HelpText`, `IsRequiredForForm`).

| Ereignis | Textfeld | Zahl / Betrag | Uhrzeit | Datum | Auswahlfeld |
|---|---|---|---|---|---|
| Fokus per Tab | Marke am Ende, nichts markiert (Bearbeiten) | alles markiert (Ersetzen) | alles markiert | alles markiert | Fokusrahmen |
| Fokus per Klick | Marke an der Klickstelle | Marke an der Klickstelle | Marke an der Klickstelle | Marke; Klick auf das Symbol öffnet den Kalender | öffnet die Liste |
| Tippen | alle Zeichen | nur `0-9 , . -` | nur `0-9 : . ,` | nur `0-9 . / -` | springt zum Eintrag |
| ↑ / ↓ | keine | Schritt | Minutenschritt | Tag | markiert Eintrag (offen) |
| Bild auf / ab | – | großer Schritt | Stunde | Monat | seitenweise |
| Pos1 / Ende | Zeilenanfang / -ende | Minimum / Maximum (falls gesetzt) | wie Zahl | – | erster / letzter Eintrag |
| Enter | löst den Standardknopf aus (einzeilig) | bestätigt den Wert, dann Standardknopf | wie Zahl | wie Zahl | wählt den Eintrag, schließt |
| Esc | stellt den letzten bestätigten Wert wieder her (nur wenn geändert), sonst schließt die Maske | wie Text | wie Text | wie Text | schließt die Liste ohne Änderung |
| Verlassen | prüft Länge und Pflicht | liest, schreibt das Anzeigeformat | liest, schreibt `HH:mm` | liest, schreibt `TT.MM.JJJJ` | übernimmt die Auswahl |
| Fehler | Rahmen danger, Fehlertext am Feld | wie Text | wie Text | wie Text | wie Text |
| Erster Fehler beim Speichern | Fokus auf das **erste ungültige Feld** (Entscheidung des Auftraggebers, ADR 0069); die InfoBar über der Knopfleiste wird zusätzlich angezeigt und nimmt den Fokus nicht | wie Text | wie Text | wie Text | wie Text |
| Neu prüfen nach einem Fehler | sofort beim Tippen; der Fehler verschwindet, sobald der Wert gültig ist | wie Text | wie Text | wie Text | – |

- **Klick** setzt immer nur die Marke und markiert nichts. Doppelklick markiert ein Wort bzw. ein Segment, Dreifachklick die Zeile, Strg+A alles.
- **Esc in Stufen:** erstens schließt Esc eine offene Auswahl oder ein Popup, zweitens stellt es ein **geändertes** Feld auf den letzten bestätigten Wert zurück und markiert ihn (das Esc ist damit verbraucht), drittens schließt es die Maske. **Enter** in einem einzeiligen Feld bestätigt zuerst das Feld und löst dann den Standardknopf (Speichern) aus; in mehrzeiligen Feldern nicht.
- **Wert beim Tippen (Zahl, Betrag):** Ergibt der getippte Text einen gültigen Wert, gilt er sofort für die Maske (Knöpfe werden frei, Summen rechnen mit), ohne dass man das Feld verlässt; der Text bleibt dabei so, wie getippt („5“, erst beim Verlassen „5,00“). Ein ungültiger oder leerer Zwischenstand gilt als „kein Wert“, die Fehlermeldung kommt erst beim Verlassen oder bei Enter. Esc stellt den letzten bestätigten Wert wieder her, auch für die Maske. Datum und Uhrzeit gelten erst nach dem Bestätigen. *Gewollte Abweichung von Fluent 2* (Wert erst bei Enter oder Verlassen; Auftraggeber 02.11.2026, Ä311; Grund in [gestaltung/fluent2-ableitung.md](gestaltung/fluent2-ableitung.md), Abschnitt 6, Nr. 27).
- **Einfügen** in ein einzeiliges Feld entfernt Zeilenumbrüche sowie führende und folgende Leerzeichen (auch geschützte). Ist der Text länger als das Feld erlaubt, wird er gekürzt und das Feld zeigt die Warnung „Auf 80 Zeichen gekürzt.“ (Warnung am Feld, 5.2).
- **Höchstlängen** (`MaxLength`), im ViewModel dokumentiert: Name 80, Telefon 30, E-Mail 254, PLZ 10, Notiz 500.
- **Rechtschreibprüfung** aus bei Namen, E-Mail, Telefon, PLZ und Codes; an bei Notizen und Freitext (soweit die Plattform sie bietet).
- **Nur lesbar** ist ein vorübergehender Zustand: Cursor Text, Fokus und Kopieren bleiben möglich. Ein dauerhaft unveränderlicher Wert ist Text, kein Feld.
- **Löschen-Kreuz** (16, rechts im Feld) gibt es nur im Suchfeld und nur, wenn es Text enthält; in normalen Feldern nicht (Pflichtfelder sollen nicht versehentlich geleert werden).
- **Mehrzeiliges Feld:** feste Höhe 68, `MaxHeight` 200, vertikale Scrollleiste bei Überlauf, es wächst nicht mit dem Text; Enter ist der Zeilenumbruch, Strg+Enter löst den Standardknopf aus, Tab verlässt das Feld. Hat die Notiz ein Limit, steht der Zähler („noch 120 Zeichen“) rechts in der Hilfezeile (Stufe „Klein“, fg-2; ab 90 % des Limits warn). Nur Klartext. *Ausnahme (R04 Slice 82, Ä148, Auftraggeber 27.10.2026: „die Notiz ist zu klein, lange Wünsche werden abgeschnitten“):* Die Notiz in den Eingaben für neue Einträge der Seitenleiste (Termin, Paar/Spa, Sauna, Art „Notiz“) **wächst** mit dem Text von 68 bis 200 und scrollt erst dann (Klasse `grow` im Stil von `CalendarView.axaml`); der Grund einer Sperrzeit bleibt einzeilig.
- **Suchfeld:** Lupe links (16), beim Fokus die zuletzt geöffneten Gäste (höchstens 5), beim Tippen Treffer nach Name, Telefon, E-Mail nach Relevanz; ohne Treffer „Keine Treffer.“ mit dem Knopf „Neuen Gast anlegen“. Tasten: ↓ geht in die Liste, ↑/↓ wählen, Enter übernimmt, die Leertaste wählt in der Liste nicht (Texteingabe hat Vorrang). Esc: zuerst schließt es die Liste, dann leert es das Feld, dann schließt es die Maske.
- **Fehlerregel für alle Zahlen- und Zeitfelder:** Eine ungültige Eingabe ergibt einen Fehlertext am Feld (Problem und Weg), der Text bleibt stehen; sie führt nie zu einem Absturz, nie zu stillem Überschreiben und nie zu stillem Runden oder Begrenzen. Ein leeres Feld heißt „keine Angabe“ (bei Pflicht: Fehler).

### 5.17 Datumsfeld

- Format `TT.MM.JJJJ` (de-AT), Breite 160, Ziffern tabular, Platzhalter „z. B. 21.10.2026“. Zwei Ausprägungen: **mit Kalenderknopf** (Termin, Serie bis, Zeitraum von/bis; Symbol Kalender 16 rechts im Feld) und **ohne** (Geburtstag, nur Tippen). Datum und Uhrzeit eines Termins sind **zwei Felder** nebeneinander (160 + 100, Abstand 12) mit einem gemeinsamen Wert; ändert man das Datum, bleibt die Uhrzeit.
- **Lesen beim Verlassen und bei Enter:** `21102026` ergibt 21.10.2026; `211026` ergibt 21.10.2026; `21.10.26` ergibt 21.10.2026; `21.10.` und `21.10` ergeben den 21.10. im aktuellen Jahr; `1.2.26` ergibt 01.02.2026. Ein zweistelliges Jahr wird das Jahr, das **heute** am nächsten liegt (Geburtstag: nie in der Zukunft). Ein Datum ohne Jahr beim Geburtstag nur, wenn das Feld es ausdrücklich erlaubt (masken.md R12). Danach schreibt das Feld `TT.MM.JJJJ`.
- **Fehler:** ein Datum, das es nicht gibt, ergibt „Den 30.02.2026 gibt es nicht.“; ein Datum außerhalb von Minimum und Maximum ergibt „Das Datum liegt zwischen 22.10.2026 und 21.10.2027.“ (mit den Grenzen des Feldes; nie stilles Begrenzen).
- **Pfeile im Feld:** ↑/↓ ± 1 Tag, Umschalt+↑/↓ ± 1 Woche, Bild auf/ab ± 1 Monat, Strg+Bild auf/ab ± 1 Jahr; das Mausrad wirkt nur bei Fokus; ohne Wert setzt ↑ den heutigen Tag.
- **Kalender (Popover):** öffnet per Klick auf den Kalenderknopf oder Alt+↓, überlagert die Maske und schiebt nichts weg; Fläche surface, Rahmen 1 px line, Radius 8, Schatten `shadow`; Wochenanfang Montag, heutiger Tag mit Rahmen 1 px accent, gewählter Tag Fläche accent und Text on-accent. Tasten: ←/→ ± 1 Tag, ↑/↓ ± 1 Woche, Bild auf/ab ± 1 Monat, Strg+Bild ± 1 Jahr, Pos1/Ende Wochenanfang / -ende, **Enter wählt und schließt**, **Esc schließt ohne Änderung**; danach kehrt der Fokus ins Feld zurück. Gesperrte Tage (kein Dienst, Ruhetag) sind mit Deckkraft 0,5 gezeichnet und tragen den Hinweis „Kein Dienst“.
- Name für Screenreader: Beschriftung plus „Datum“; Hilfetext „Format Tag.Monat.Jahr“.

### 5.18 Uhrzeitfeld

- 24-Stunden-Uhr, Format `HH:mm`, Breite 100, Ziffern tabular, Platzhalter „z. B. 09:30“. Ein einzeiliges Textfeld, **kein Popup-Rad**. **Entscheidung des Auftraggebers (ADR 0069): tolerant lesen und nach dem Verlassen normalisieren, keine feste Maske `__:__`.**
- **Tippen:** erlaubt sind `0-9 : . ,`, höchstens 5 Zeichen. **Lesen beim Verlassen und bei Enter:** `9` ergibt 09:00; `09` ergibt 09:00; `930` ergibt 09:30; `0930` ergibt 09:30; `9:30`, `9.30` und `9,30` ergeben 09:30; `9:5` ergibt 09:05. Danach schreibt das Feld `HH:mm`. `24`, `9:60` und jeder Wert über 23:59 sind ein Fehler: „Die Uhrzeit liegt zwischen 00:00 und 23:59.“ (24:00 nur in einem Feld, das ausdrücklich „bis Mitternacht“ erlaubt). Nicht lesbarer Text (`abc`) ergibt „Bitte die Uhrzeit im Format 09:30 eingeben.“ Pflicht und leer: „Die Uhrzeit fehlt. Bitte im Format 09:30 eingeben.“
- **Pfeile:** ↑/↓ ± Schrittweite des Kalenderrasters (5, 10 oder 15 Minuten; Vorgabe 5), Umschalt+↑/↓ ± 1 Minute, Bild auf/ab ± 1 Stunde, Pos1/Ende nur mit Grenzen (z. B. Öffnungszeit). Kein Umlauf über Mitternacht (der Wert wird begrenzt); das Mausrad wirkt nur bei Fokus.
- **Abhängige Felder** (Ende nach Beginn) rechnen erst nach der Bestätigung neu, nie beim Tippen. Liegt das Ende vor dem Beginn, steht am Feld „Ende liegt vor dem Beginn.“, ist es gleich dem Beginn, „Das Ende muss nach dem Beginn liegen.“
- Doppelklick markiert die Stunde bzw. die Minute. Name für Screenreader: Beschriftung plus „Uhrzeit“, Wert als Uhrzeit; Hilfetext „Format Stunde:Minute“.

### 5.19 Zahlenfeld

- Für Dauer, Anzahl, Bestand, Minuten. Breite 120; Zahlen stehen **linksbündig** im Feld (gleiche Kante wie die Beschriftung), in Tabellenzellen rechtsbündig mit dem Spaltenkopf (5.7). Mit Pfeilspalte (24 px breit, zwei übereinander liegende Knöpfe) **nur bei Bereichen bis etwa 20 Schritte** (Dauer, Anzahl), sonst ohne Pfeile. Die Pfeilknöpfe heißen „Erhöhen“ und „Verringern“ und sind nicht in der Tab-Reihenfolge. Die Einheit steht in der Beschriftung (5.2).
- **Tippen:** erlaubt sind Ziffern, `,` (Dezimalzeichen), `.` (Tausendertrenner) und bei einem Minimum unter 0 das Minuszeichen; alles andere wird nicht angenommen und beim Einfügen entfernt.
- **Tasten:** ↑/↓ = Schritt (Dauer 5 Minuten, Anzahl 1), Bild auf/ab = großer Schritt (Dauer 15, Anzahl 10), Umschalt+↑/↓ = Schritt 1, Pos1/Ende = Minimum / Maximum (nur wenn gesetzt), Mausrad nur bei Fokus. Gehaltene Taste: Wiederholung nach 300 ms, dann beschleunigend. Der Wert wird bei Pfeil, Bild und Rad begrenzt (kein Umlauf); am Rand ist der Pfeilknopf deaktiviert, das Feld bleibt bedienbar.
- **Beim Tippen** gilt ein gültiger Wert schon für die Maske, der Text bleibt wie getippt (5.16, „Wert beim Tippen“).
- **Bestätigen** bei Enter und Verlassen: Ein gültiger Wert wird im Anzeigeformat (Dezimalkomma, Tausenderpunkt) neu geschrieben. Ein ungültiger Wert wird **nicht überschrieben**: Rahmen danger, Text bleibt, Fehler „Bitte eine Zahl eingeben.“ bzw. „Der Wert liegt zwischen 0 und 120.“ (mit den Grenzen des Feldes). Leer heißt „keine Angabe“, bei Pflicht Fehler.
- **Formeln** (`+ - * /`) werden nicht gerechnet. Name für Screenreader: Beschriftung, Rolle „Zahlenfeld“, Bereich und Wert; Hilfetext z. B. „Minuten, 0 bis 120“.

### 5.20 Betragsfeld

- Für Preis, Rabatt, Trinkgeld, Kassenbetrag. Baut auf dem Zahlenfeld (5.19) auf: Breite 140, keine Pfeilknöpfe, `€` steht in der Beschriftung („Preis (€)“), immer **zwei Nachkommastellen**. Gerechnet wird mit `decimal`, nie mit `double`.
- **Tippen:** erlaubt sind `0-9 , .` und `-` (nur in Feldern für Storno und Gutschrift); Leerzeichen, `€` und geschützte Leerzeichen werden beim Einfügen entfernt.
- **Lesen (de-AT):** `,` ist das Dezimalzeichen, `.` der Tausendertrenner. **Ausnahme:** ein einzelner `.` mit genau ein oder zwei Ziffern danach und ohne `,` gilt als Dezimalzeichen (Zehnertastatur). `12` ergibt 12,00; `12,5` ergibt 12,50; `12.5` ergibt 12,50; `1.234,56` ergibt 1.234,56; `1234` ergibt 1.234,00 (keine automatische Kommaverschiebung); `12,505` ist ein **Fehler**: „Höchstens zwei Nachkommastellen.“ Beträge werden nie stumm gerundet (RKSV/TSE, ADR 0010). Ein Wert, der sich nicht lesen lässt, geht als Text ohne Wert an das ViewModel (nie ein alter Wert, der unbemerkt gilt); `Money.Parse` im ViewModel rundet ebenfalls nie.
- **Tasten:** ↑/↓ ± 1,00, Umschalt+↑/↓ ± 0,10, Bild auf/ab ± 10,00, Pos1/Ende nur mit Grenzen, Mausrad nur bei Fokus.
- **Anzeige:** beim Tippen gilt ein gültiger Betrag schon für die Maske (5.16, „Wert beim Tippen“; z. B. „Wertgutschein verkaufen“ ohne Feldwechsel), nach dem Verlassen steht `1.234,50`; negative Beträge in danger mit Minuszeichen (nicht in Klammern). Leer ist nicht 0,00, leer heißt „keine Angabe“. Name für Screenreader: Beschriftung plus „Euro“.

### 5.21 Menü und Kontextmenü

- Ersetzt das „Mehr“ als bloßen Ghost-Knopf mit Pfeil: der Knopf bleibt, öffnet aber dieses Menü. Das Menü listet ausgeblendete oder seltene Aktionen am Auslöser; **häufige Aktionen oben, riskante unten** (durch einen Trenner 1 px line abgesetzt, Text danger). Keine Untermenüs.
- **Symbole am Termin-Menü (Runde 5, Ä32, 22.10.2026, Auftraggeber):** Im Kontextmenü am Termin tragen nur die wichtigsten Einträge ein Symbol 16 links: Details öffnen (Info), Einchecken (Pfeil in die Tür), Kundenkarte (Person), Folgetermin (Kalender mit Plus), Verschieben (Vier-Pfeil), Erledigt (Haken), Nicht erschienen (Person mit Kreuz), Stornieren (Kreuz, in danger). Alle übrigen Einträge (Kassieren, Kommt später, Zeiten ändern, Wiederholen …) haben keines, halten die Stelle aber frei, damit alle Texte in einer Linie beginnen. „Kommt später“ hat drei Einträge: 5, 10 und 15 Minuten.
- Maße: Breite mindestens 138 und höchstens 300 (**kein Menütext darf breiter sein als seine Fläche**, ein Test prüft das am Kontextmenü des Termins und des Streifens; der längste Eintrag dort heißt „Behandlungszeiten bearbeiten …“, Runde 5, Ä32-Nachtrag), Fläche surface, Rahmen 1 px line, Radius 8, Schatten `shadow`, Innenabstand 4, Abstand zwischen Einträgen 2. Eintrag: Höhe 32, Innenabstand 6/10, Radius 6, Symbol 16 (nur bei bekannten Befehlen) mit 8 px Abstand zum Text, Text 14. Hover surface-2, gedrückt surface-3, Fokus 4.5. Gruppenüberschrift in der Liste: Stufe „Beschriftung“, fg-2. Ein Tastenkürzel steht rechts als Nebeninhalt (Stufe „Klein“, fg-2, `TextBlock.menugesture`, deutsch: „Strg+N“) und löst dasselbe aus wie der Eintrag; der Eintrag trägt es zusätzlich als `AutomationProperties.AcceleratorKey`. `InputGesture` gibt es nicht (es zeigt „Ctrl“ und „OemPeriod“, die Taste selbst gehört dem Kalender).
- Tasten: Enter, Leertaste oder Alt+↓ am Auslöser öffnen das Menü; ↓ geht zum ersten, ↑ zum letzten Eintrag, am Ende Umlauf (nur in Menüs); Enter oder Klick löst aus und schließt; Esc schließt, der Fokus kehrt zum Auslöser zurück. **Kontextmenü:** Rechtsklick auf einen Termin öffnet dasselbe Menü an der Mausposition.
- Das Menü eines geteilten Knopfs zeigt dessen Hauptaktion nicht noch einmal.
- Die Kopfzeile des Kalenders (7) nutzt dasselbe Menü als Überlauf „⋯“.

### 5.22 Tooltip

- Kurz, unformatiert und nie unverzichtbar. Erscheint bei Hover und Tastaturfokus nach **250 ms** (0 ms, wenn schon ein Tooltip offen ist) und verschwindet nach 250 ms; Esc oder Strg blendet ihn aus. Abstand zum Ziel 4, kein Pfeil.
- Maße: Fläche toast, Text toast-fg, Radius 8, Innenabstand 6/10, Höchstbreite 240, Stufe „Klein“ (12/16), Schatten `shadow`; keine Bedienelemente darin; keine Wiederholung eines sichtbaren Textes.
- **Lesbarkeit (Runde 5, Ä28):** Der Text im Tooltip hat immer Kontrast 4,5:1 zu seiner Fläche (toast), an jedem Ziel. Ein Tooltip hängt logisch unter seinem Ziel; Stilregeln mit Nachfahren-Selektor („Border.stripe TextBlock“) färben deshalb auch seinen Text, weil sie den Text des Ziels meinen. Regeln für den Inhalt eines Elements gelten nur für das direkte Kind („Border.stripe > TextBlock“); ein Test öffnet jeden Texthinweis des Kalenders und prüft den Kontrast.
- **Reiche Karte (Hover-Karte, Muster ab Slice 89, Ä158):** Karten mit mehreren Angaben (Termin, Spaltenkopf) tragen die Klasse `hovercard` (Fläche surface, Rahmen line, Höchstbreite 420, Grundschrift 14). Die **Hauptangaben** stehen in Grundschrift 14 und **SemiBold** (Termin: Gast, Behandlung, Tag mit Uhrzeit, Raum), Nebenangaben normal 14 (Dauer, Person; in der Raumansicht die Person fett), Zeilen „Bezeichnung – Wert“ in 12 mit Etikettenspalte 96 (das längste Etikett passt hinein; was länger wäre, gehört ins Band oder ins Panel). Ein oranges Band (warn-soft) trägt alles, was Handeln verlangt, auch den ersten Ausfall („1× nicht erschienen“). **Nur, was etwas sagt:** keine Zeile, die das Etikett oder der Kalender schon zeigt (Besuche bei Stammgast und Erstbesuch, der übliche Ablauf); der Rest steht im Termin-Panel. Schlichte Tooltips (Text) bleiben bei 12 und Fläche toast.
- Inhalt: Symbol-Knopf: Verb und Objekt („Termin verschieben“), bei Zuständen und Orten ein Substantiv („Meldungen“); **deaktivierter Knopf: wann er verfügbar wird** („Verfügbar, sobald ein Name eingetragen ist.“); Aktion mit Tastenkürzel nennt es („Rückgängig (Strg+Z)“); gekürzte Tabellenzellen und Terminkarten zeigen den vollen Text. Ein Satz endet mit Punkt, eine Wortgruppe nicht. Jeder Symbol-Knopf hat einen Tooltip und `AutomationProperties.Name`. Nennt ein Tooltip das Kürzel in Klammern, setzt die App es zugleich als `AutomationProperties.AcceleratorKey` (Fluent 2 F29, `ShortcutHints`); der Name eines Knopfs ohne eigenen Namen ist der erste Text seines Inhalts, sonst der Tooltip ohne Kürzel (`AccessibleNames`).

### 5.23 Schalter

- Nur für Einstellungen, die **sofort wirken** (z. B. „Online-Buchung aktiv“), und nur in Masken **ohne** Speichern-Knopf; in einer Maske mit Speichern steht stattdessen ein Kontrollkästchen. Ein Schalter ist eine Aktion, ein Kontrollkästchen ein Zustand.
- Maße: Spur 40 × 20, Radius 999, Rand 1 px, Daumen 18. Aus: Fläche surface, Rand fg-3, Daumen fg-3. An: Fläche accent, Rand accent, Daumen on-accent. Übergang 200 ms, Kurve `(0.33,0,0.67,1)` (bei „Animationen aus“ 0 ms). Hover: Rand fg-2 (aus) bzw. Fläche accent-hover (an). Fokus 4.5.
- Beschriftung: ein bis zwei Wörter, ein Substantiv („Online-Buchung“), rechts vom Schalter mit 8 px Abstand, nie als Frage und kein „Ein“/„Aus“ als Text. Die Leertaste schaltet; Rolle „Schalter“ mit Zustand für Screenreader.

### 5.24 Link

- `Button.link` (Web `a`): Text accent, Stufe „Text“ (14/400), keine Fläche, kein Rahmen. Hover: Text accent-hover, **unterstrichen**; gedrückt und Fokus ebenfalls unterstrichen, Fokus zusätzlich nach 4.5. Zwei Hinweise auf Bedienbarkeit: Farbe und Unterstreichung bei Interaktion.
- Für Navigation und Verweise (in Hinweisen, „3 Termine umbuchen“ im Spaltenkopf); eine Aktion mit Wirkung ist ein Knopf. Linktext beginnt mit einem Verb oder nennt das Ziel, ist ohne Umgebung verständlich, nie „hier klicken“, keine URL im Text. Öffnet ein Link ein neues Fenster, steht das im Text („Handbuch öffnen“).

### 5.25 Fortschrittsbalken und Skeleton

- **Fortschrittsbalken** nur für Vorgänge, die länger als 1 Sekunde dauern (Sicherung, Wiederherstellung, Einspielen). Bestimmt (mit Prozent), wenn die Dauer bekannt ist; unbestimmt nur kurz; nie Balken und Spinner zugleich. Spur 4 px hoch, Radius 4, Fläche surface-3, Füllung accent, Breite 100 % des Bereichs; die Füllung springt nie zurück und wird nicht animiert (4.6).
- Darüber die Beschriftung des Vorgangs (Stufe „Beschriftung“), darunter der Statustext (Stufe „Klein“, fg-2) in der Verlaufsform mit Auslassung und Leerzeichen davor („Gäste werden geprüft …“); keine Zeitangaben („noch 3 Minuten“). Bei Fehler ersetzt ein Satz mit Problem und Weg den Statustext, die Füllung wird danger. Ist der Vorgang abbrechbar, steht rechts ein kleiner sekundärer Knopf „Abbrechen“. Für blockierende Vorgänge (Anmeldung) gilt ein unbestimmter Balken, kein Spinner (4.6).
- **Skeleton** (Platzhalter für Listen): ab 1 Sekunde Ladezeit zeigen die Tagesliste und die Kundenliste Skeleton-Zeilen statt „Wird geladen …“; bei kürzerer Ladezeit bleibt es beim Text „Wird geladen …“. Zeile Höhe 40, Radius 8, Fläche surface-3, Abstand 4, so viele Zeilen wie die Liste Platz hat (höchstens 8), **statisch** (keine Welle, kein Puls, 4.6). Der Bereich ist als „beschäftigt“ gekennzeichnet (`AutomationProperties` bzw. `aria-busy`); der Fokus geht beim Laden nicht verloren.

### 5.26 Tag-Picker

- Mehrfachauswahl **über sechs Werte** (Qualifikationen, Standorte, Räume) ist ein Tag-Picker; bis sechs häufige Werte bleiben Chips (5.3), die Wolke aus Chips für lange Listen **DARF NICHT** bleiben (masken.md R10).
- Aufbau: ein Feld nach 5.2 (Breite 100 % der Karte, Höhe mindestens 34, wächst bei Umbruch), darin die gewählten **Tags** und das Tippfeld. Tag: Höhe 24, Radius 999, Innenabstand 0/8, Fläche surface-3, Text fg (Stufe „Klein“), rechts ein Entfernen-Kreuz 16 mit Name „Entfernen“; Abstand zwischen Tags 8 waagrecht und 8 senkrecht, sie brechen um.
- Liste: erscheint beim Tippen unter dem Feld mit den Maßen und Tasten der Aufklappliste (5.3: Eintrag 32 hoch, Radius 8, höchstens 8 Einträge); schon gewählte Werte fehlen darin. Tippen filtert; ↑/↓ markieren; **Enter wählt den markierten, ohne Markierung den ersten Eintrag**; die Liste bleibt offen, Esc schließt sie. **Rücktaste** im leeren Tippfeld entfernt das zuletzt gewählte Tag. Das Verhalten steht kurz im Hilfetext.

### 5.27 Rückfragedialog (Windows-App und Web)

- Fragt vor Löschen, Stornieren und vor dem Verwerfen von Änderungen; die einzige Art Dialog in der App. Er ist ein **Alert**: er schließt nur über seine Knöpfe oder Esc, nicht per Klick außerhalb, und hat kein Schließen-Kreuz. **Ein** Dialog je Fenster, nie auf einem Dialog, nie für Feldfehler (dafür Fehler am Feld).
- Maße: Breite 420, Fläche surface, Rahmen 1 px line, Radius 12, **Innenabstand 24**, kein Schatten; Overlay `rgba(0,0,0,0.45)` über dem Fenster. Aufbau von oben: Titel (Stufe „Überschrift“), 8 px, Text (Stufe „Text“), 24 px, Knopfleiste.
- Inhalt: Der Titel ist eine **Frage** („Termin endgültig löschen?“), der Text nennt zuerst die Folge („Der Termin von Mia Muster am Mi. 21.10. 10:00 ist danach nicht mehr im Kalender.“), die Knöpfe sind die konkrete Antwort in ein bis zwei Wörtern („Löschen“, „Abbrechen“), nie „Ja“, „Nein“ oder „OK“. Beispiel Änderungen: „Änderungen verwerfen?“ mit „Verwerfen“ und „Weiter bearbeiten“.
- **Knopfreihenfolge (Windows-Gewohnheit):** die Aktion **links**, „Abbrechen“ **rechts**, Abstand 8, rechtsbündig; höchstens drei Knöpfe. Zerstörende Aktion: `danger.filled` (5.1); neutrale Aktion: primär. „Abbrechen“ ist Ghost (5.1).
- **Fokus:** Beim Öffnen bekommt bei einer **zerstörenden** Aktion „Abbrechen“ den Fokus und Enter löst „Abbrechen“ aus (nie die Löschen-Aktion); bei einer neutralen Rückfrage der primäre Knopf. Hat die Rückfrage ein Eingabefeld (z. B. „Grund“ beim Storno), bekommt das Feld den Fokus, die Marke steht am Ende eines vorbelegten Textes; Enter im Feld löst nichts aus (*R01 Slice 28, W24*, nach Fluent 2). Esc ist immer „Abbrechen“. Tab bleibt im Dialog (Fokusfalle); beim Schließen kehrt der Fokus zum Auslöser zurück. Web: `<dialog>` als Modal (Fokus und Esc vom Browser), dieselbe Reihenfolge.
- **Gewollte Abweichung von Fluent 2:** Fluent setzt Radius 8, Höchstbreite 600 und Overlay 0,4; der Brief behält 12, 420 und 0,45 (Marke, kurze Rückfragen).
- **Rückfrage mit Auswahl und Text** (*Runde 5b, Block L, 22.10.2026*, Absage der Termine einer Schließzeit, ADR 0073): dieselbe Karte und dieselben Regeln, aber **600 breit** (Klasse `confirmdialog wide`) und höchstens so hoch wie das Fenster abzüglich 24 px Rand je Seite; Titel und Folge stehen oben, die Knopfleiste fest unten, der Inhalt dazwischen scrollt. Inhalt: Liste der Termine als Kontrollkästchen in einer Karte (Radius 8, Rahmen line, höchstens 190 hoch), Betreff, mehrzeiliges Feld „Nachricht“ (Höhe 170, 2000 Zeichen), die KI-Knöpfe (Standardknöpfe, nur mit KI), Kontrollkästchen „Umbuchung anbieten“ und die Zahl der Nachrichten (Stufe „Text“ 500, `Polite`). Zerstörende Aktion: roter Knopf mit der Zahl links; der Fokus liegt im ersten Feld (Betreff), bis die Liste geladen ist auf „Abbrechen“ (*R03 Slice 57*).

### 5.29 Akkordeon (Windows-App, Runde 5 Ä34)

- Ein Abschnitt, der zu ist, bis man ihn öffnet (Fluent 2 Accordion: alle zu, Kopf ist ein Knopf mit Zustand, Höhe mindestens 44, Pfeil am Ende). `AccordionHeader` (Knopf mit Zustand „gewählt“ = aufgeklappt, Stil `ToggleButton.accordion`): Fläche transparent, Radius 12, Innenabstand 16/10, Hover surface-2, gedrückt surface-3, Fokus 4.5; der Pfeil (Symbol 16, fg-2) dreht sich beim Aufklappen um 180°, ohne Übergang (4.6).
- Der Kopf darf Inhalt tragen (Kennzahlen); Knöpfe gehören nicht in den Kopf (ein Knopf im Knopf), sie stehen darunter. Der Inhalt unter dem Kopf erscheint erst beim Öffnen; öffnet ein Fehler oder Laden den Abschnitt nicht, steht die Meldung unter dem Kopf.
- Tastatur: Enter und Leertaste auf dem Kopf schalten; Tab geht danach in den Inhalt. Screenreader: Rolle Knopf, Name „Kunde“, Zustand aufgeklappt oder zugeklappt (`IExpandCollapseProvider`, nicht nur „gewählt“).
- Inhalt, der für die Aufgabe nötig ist, gehört nicht hinein; Abschnitte öffnen sich nicht von selbst (**Abweichung von Fluent 2, dort „bei gefüllten Werten automatisch“**: der Kopf zeigt die Kennzahlen schon, der Rest ist Nachschlagewerk).

### 5.28 KI-Kennzeichnung (Windows-App; Fluent 2 Responsible AI, F30)

Gilt für jede KI-Funktion der Windows-App (`AiFunctions`): „KI und Hilfe“ (Assistent), „Wunsch eintippen“, Posteingang (Erkanntes, „Wer ist das?“, freie Zeiten, Antwortentwurf) und „Was tun?“ bei einer Meldung. Was eine KI ausgibt, ist zu erkennen, zu prüfen, abzulehnen und zu bewerten. Pflichtzahlen (Kassa, Steuer, Berichte für Behörden) und freie Zeiten rechnet nie die KI (CLAUDE.md); keine Ausgabe der KI wird ohne Klick ausgeführt oder gesendet. Der Wortlaut steht zentral in `Services/AiLabels.cs`; er gilt, bis der Auftraggeber ihn freigibt oder ändert.

- **Pille „KI“** vor der Ausgabe: Stufe Mini (12/14, 500), Fläche accent-soft, Text accent, Kapsel (`Border.pill.accent`, Steuerelement `AiPill`); Name für den Screenreader „Von der KI erstellt“. Auf dem Knopf „KI und Hilfe“ steht „KI“ unter dem Zeichen im Knopf (nichts ragt über die Leiste). Der Assistent hat einen Namen ohne Menschennamen und ein Symbol, kein Gesicht.
- **Hinweiszeile** unter jeder Ausgabe (Stufe Klein, fg-2), **eine Zeile** mit „Warum?“ dahinter (Auftraggeber, 29.10.2026): „KI-Vorschlag. Bitte prüfen, bevor Sie ihn übernehmen.“ Bringt der Server einen eigenen Hinweis mit (Assistent, Wunsch eintippen, Was tun?; AI Act Art. 50), steht dieser statt des zentralen Wortlauts. **Gewollte Abweichung vom Auftragstext:** „Sie“ statt „du“ (Abschnitt 1).
- **Antwortentwurf:** über dem Entwurf Pille und „KI-Entwurf, noch nicht gesendet“ (Stufe Beschriftung), solange er nicht gesendet ist; danach nur die Pille und „Gesendet.“. Er wird nie von selbst gesendet: „Senden“ ist ein eigener Klick, ein geänderter Entwurf wird vorher gespeichert; den Hinweis auf die KI für den Gast hängt der Server immer an.
- **„Warum?“** (`AiFooter`, in der Hinweiszeile): aufklappbar wie „Mehr“ (Pfeil, Zustand für den Screenreader), zwei bis drei Sätze mit Quelle (was die KI gelesen hat, was Kalender und Kassa rechnen) und Regel (nichts ohne Klick). Es steht nur, was der Code einhält.
- **Keine Rückmeldung:** Die Knöpfe „Hilfreich“ und „Nicht hilfreich“ sind am 29.10.2026 auf Wunsch des Auftraggebers entfallen (Rezeption: so wenig wie möglich auf dem Bildschirm). **Gewollte Abweichung von Fluent 2** (Responsible AI, Grundsatz 5).
- **Fehlerzustand:** ein Satz, was nicht ging, dazu ein Weg ohne KI als Satz „Ohne KI weiter: …“ und, wo es passt, ein Knopf: Assistent „Handbuch öffnen“, Wunsch eintippen „Freie Zeit suchen“, Was tun? „das Handbuch zur Meldung steht unten“. Fehler sind für Screenreader assertive.
- **Reiter „KI und Hilfe“** (*Runde 6, Block N3, Ä66, 23.10.2026, Auftraggeber: „der ki knopf soll sich nicht als eigenständige seitenleiste öffnen sondern in den tabs eingliedern, sonst werden beim schließen alle anderen auch geschlossen“*): Im **Kalender** ist „KI und Hilfe“ eine Seite der Seitenleiste wie die Warteliste (5.12, Punkt 7): ein Reiter mit dem AISPA-Zeichen (16) und dem Text „KI und Hilfe“ (Name für den Screenreader), je Art ein Reiter, Inhalt derselbe Baustein wie sonst (`CompanionContent`). Der runde Knopf (48 × 48, schwebt weiter über der Ecke der Leiste, deshalb bleiben dort 60 px frei) und **Strg+Leertaste** *wählen* diesen Reiter und öffnen dafür die Seitenleiste, falls sie zu ist; ist er schon gewählt, schließen sie ihn. Das Kreuz (Hinweis „Schließen (Esc oder Strg+Leertaste)“) und Esc schließen **nur diesen Reiter**: die übrigen bleiben stehen, der nächste ist gewählt, ohne weitere Seite geht die Leiste zu. Wählt man einen anderen Reiter, bleibt der Assistent im Hintergrund offen, der Knopf ist wieder da und wählt ihn erneut. Fokus: beim Öffnen ins Fragefeld, beim Schließen zum Knopf zurück. Der Filter auf einen Gast (alle Seiten schließen) lässt diesen Reiter stehen, weil der Assistent ihn oft selbst auslöst. F6: im Kalender kein eigener Bereich mehr, die Seitenleiste trägt den Namen „KI und Hilfe“, solange der Reiter gewählt ist. *Entscheidung für die Seiten ohne Reiterschiene* (Kunden, Kassa, Verwaltung, Hilfe, Posteingang, Abgleich …): dort bleibt es bei der **Leiste der Shell** wie bisher (360 px, gleicher Kopf, gleiche Bewegung, Esc, Strg+Leertaste, Fokus, je Seite gemerkt, F6-Bereich „KI und Hilfe“) und es gibt **keine Reiterschiene mit einem einzigen Reiter**. Grund: Das Problem, dem Ä66 abhilft (das Schließen reißt die übrigen Reiter mit), gibt es dort nicht, denn auf diesen Seiten steht nichts neben dem Assistenten; eine Schiene mit einem Reiter wäre nur Rahmenwerk. Das Verhalten bleibt gleich (Knopf, Tasten, Inhalt); nur die Form folgt der Seite. Der Stand „offen“ gilt je Seite; wechselt man zwischen Kalender und anderer Seite, wechselt der Assistent zwischen Reiter und Leiste der Shell. *Beim Bauen festgelegt (R02 Slice 31, Ä74, 25.10.2026):* Auf **Verwaltung** und **Hilfe** liegt der eingebettete Browser als eigenes Fenster über allem, was die App zeichnet; er lässt deshalb unten **68 px** frei (Knopf 48 plus Rand 20, `EmbeddedBrowser.DockHeight`), der Knopf steht darunter, die Leiste der Shell wie überall daneben. Strg+Leertaste im Browser meldet die Seite als Nachricht an die App (`appShortcuts.ts`, nur eingebettet). Scrollende Inhalte, die bis in die rechte untere Ecke reichen (Posteingang-Detail, Kundenkarte), scrollen mit **72 px** Innenabstand unten (mit dem Seitenrand 16 endet die letzte Zeile über dem Knopf); mitten im Scrollen läuft Text wie bei jedem schwebenden Knopf darunter durch.
- **Offen (Entscheidung oder Web-Nachzug):** Ausschalter je Funktion in den Einstellungen (heute nur serverweit: ohne Schlüssel oder `Ai:Provider=RuleBased`, der Assistent je Recht), Erstöffnungskarte „Was der Assistent kann und nicht kann“, Protokoll „KI-Aktivität“, Kennzeichen am Termin für von der KI angenommene Termine mit „Bestätigen“ und „Ablehnen“ (der Server nennt nur den Kanal, nicht ob eine KI oder feste Regeln gelesen haben), Rückmeldung an den Server samt Auswertung, KI-Stellen im Web (Assistent und Anfragen der Team-Oberfläche im WebView2, Online-Assistent und Anamnese-Leser im Kundenbereich).

### 5.30 Hilfe (Aufbau eines Themas, Web und Windows-App)

Die Hilfe hat eine Darstellung (`web/src/help`, ADR 0079); die Werte gelten für die Seite `/help/` und, soweit sie passen, für die Schublade der Verwaltung. Farben nur aus den Token (Abschnitt 2).

- **Aufbau von oben nach unten:** Brotkrumen „Hilfe › Bereich › Thema“ (Stufe Klein 12/16, `muted`, „Hilfe“ führt zur Übersicht, das Thema ist `fg`), Titel `h1` 28/36 Gewicht 600, Rollenkennzeichen „Für: Rezeption, Leitung“ (12/16, `muted`; nur wenn die Kopfangabe `rollen` da ist; es filtert nichts), „Auf dieser Seite“ (Fläche `surface-2`, Rahmen `border`, Radius 8, Innenabstand 12/16, Titel 12/16 Gewicht 600 `muted`, Links 14; nur mit mindestens einer `h2`), der Text, dann der Fuß: Linie `border`, Abstand 32, „Verwandte Themen“ (14/20 Gewicht 600, nur wenn der Text mit `hilfe:` auf andere Themen verweist) und „Zurück zur Übersicht“.
- **Text:** Zwischenüberschrift `h2` 20/28 Gewicht 600 mit 32 px Abstand davor und 8 danach, `h3` 16 mit 16/4; Fließtext 16/24; Fließtext, Aufzählungen und Schritte höchstens 72 Zeichen breit, Bilder und Kästen in der vollen Spaltenbreite (höchstens 900). Listenpunkte und Schritte haben 8 px Abstand zueinander.
- **Hinweiskästen:** aus `> Hinweis:`, `> Tipp:` und `> Achtung:` (Fortsetzungszeilen mit `>`). Sie sind Hinweise nach 5.9 mit `role="note"`, 16 px Abstand oben und unten, das Stichwort fett vor dem Text: Hinweis neutral (accent-soft, Info-Symbol), Tipp Erfolg (ok-soft, Haken), Achtung Warnung (warn-soft, Dreieck; nie rot). Symbol und Farbe gehören zusammen (5.15).
- **Bilder:** Rahmen 1 px `border`, Radius 8, Bildunterschrift (= Alternativtext) 12/16 `muted` 4 px darunter. Fehlt ein Bild, steht ein Platzhalter „Bild folgt: <Beschreibung>“ mit gestricheltem Rahmen `border-strong` statt einer Lücke.
- **Suche:** Die Statuszeile (`role="status"`, höflich) nennt die Zahl der Treffer; jeder Treffer zeigt seinen Bereich (12/16 `muted`) über dem Titel und hebt die Suchwörter mit `<mark>` hervor (Fläche `note-soft`, Schriftfarbe unverändert). Ohne Treffer nennt die Zeile den Ausweg KI, darunter stehen die Bereiche und der Knopf „Alle Themen anzeigen“.

**Seite `/help/` (Slice 145, Ä354):** Zwei Spalten. Links die Navigation (280 px): Suche, darunter der Chip „Meine Rolle: …“ (Kapsel 999, `accent-soft` und Rahmen `accent` solange der Filter wirkt, danach „Alle“; nur wenn die Adresse eine Rolle nennt), darunter die Themen in einklappbaren Gruppen nach Bereich (Kalender, Kassa, Kunden zuerst, dann alphabetisch; jedes Thema einmal; Gruppentitel Klein 12/16 `muted`, Einträge um 28 px eingerückt, aktiver Eintrag als Pille `accent-soft` ohne Unterstreichung). Rechts der Inhalt: Text höchstens 720 px, 48 px Abstand oben, Brotkrumen und Rollen schlicht und klein, Schritte als nummerierte Blöcke (Kreis 24 px `accent-soft`, Rahmen 1 px `border`, Radius 8), Bilder Radius 12, Links in Absätzen und Listen unterstrichen, in Navigation und Verzeichnis nicht. „Auf dieser Seite“ ab 1180 px Fensterbreite als schmale mitlaufende Liste rechts (200 px, `position: sticky`, gelesener Abschnitt mit 2 px `accent`), darunter als zugeklapptes „Inhalt“ (`details`) über dem Text. Die Schublade der Verwaltung behält die alte Übersicht.

### 5.31 Startbildschirm (Windows-App)

*Festgehalten 27.10.2026 (Auftraggeber, nach der HTML-Vorschau); benannte Ausnahme von 4.6 und 5.25.* Ein Fenster, kein eigenes Splash-Fenster: die Fläche des Startbildschirms wird selbst zur Navigationsleiste oder zur Markenspalte der Anmeldung.

- **Fenster:** öffnet maximiert; das Fenster selbst wird nie animiert. Bis die API bereit ist, füllt eine Fläche rail das Fenster (kein heller Blitz). Antwortet die API in unter 300 ms, erscheint nichts weiter, und Kalender oder Anmeldung stehen sofort da. Erscheint der Startbildschirm, bleibt er, bis das Logo fertig gezeichnet ist.
- **Mitte:** Logo-Kachel 96 × 96, Radius 28, Fläche rail-mark, Schatten raised, Logo (`Icons.Logo`, Strich 2) in rail; darunter mit 24 px Abstand „AISPA Studio“ 34/600 rail-fg; mit 20 px Abstand die Statuszeile 14 rail-fg-2 und darunter mit 12 px der Balken 160 × 4, Radius 4, Spur rail-hover, Segment 56 px rail-mark (nicht accent wie in 5.25: accent ist auf Petrol nicht zu sehen).
- **Statuszeile** aus den Phasen des Servers: „Server wird gestartet …“, „Datenbank wird vorbereitet …“, „Beispiel-Spa wird eingerichtet (seit N s) …“ (Text vom Server), „Anmeldung wird geladen …“; im Einzelplatz die Schritte des lokalen Starts.
- **Nach 10 Sekunden** darunter „Das dauert länger als üblich.“ (12, rail-fg-2) und der Knopf „Technische Angaben“ (Fläche transparent, Hover und aufgeklappt rail-hover, Text rail-fg); aufgeklappt Adresse und Phase in einem Kasten rail-hover, Radius 8, Innenabstand 12/8.
- **Keine Verbindung** (nach dem bestehenden Zeitlimit): an Stelle von Statuszeile und Balken die Karte „Keine Verbindung zum Server“, Breite 480, Fläche rail-hover, Radius 12, Innenabstand 24/20; Titel `h1` rail-fg, Text rail-fg-2, Adresse 12 rail-fg-2, Knopf „Erneut versuchen“ Fläche rail-mark, Text rail, Hover rail-fg. Der Fokus steht auf „Erneut versuchen“. Die Karten „Mit Haupt-PC im Netz verbinden“ und „Wiederherstellen“ folgen darunter als normale Karten (5.5).
- **Übergang**, sobald die App bereit ist: die Statuszeile blendet in 120 ms `(1,0,1,1)` aus; in 400 ms `(0,0,0,1)` schrumpft die Fläche mit gemerkter Anmeldung auf die Leiste (88 px, 5.13) und die Kachel wandert an den Platz des Logos in der Leiste (44 px, Radius 14), der Name blendet aus; ohne Anmeldung schrumpft sie auf die Markenspalte der Anmeldung (5/12) und Kachel (60 px, Radius 18) und Name wandern an ihre Plätze. Danach blendet die Ansicht in 160 ms `(0,0,0,1)` über der gleich gefärbten Fläche ein.
- **„Animationen aus“** (4.6): Logo sofort fertig, kein Balken, alle Übergänge 0 ms.
- **Screenreader:** Bereich mit Namen „AISPA Studio startet“; die Statuszeile ist eine höfliche Live-Region am Text selbst, der Text der Karte eine bestimmte.

## 6. Seitenaufbau der Verwaltung

Gilt für Stammdaten, Zugänge, Dienstplan, Zeiten, Kassa-Einstellungen, Lager, Berichte, Kosten, Buchhaltung, Lohn, in Browser und App gleich.

1. **Kopf**: Reiterschiene (5.4) mit den Unterseiten. Darunter 16 px.
2. **Zweispaltig**, wenn es eine Auswahl gibt (Standorte, Team, Behandlungen, Zugänge, Artikel): links Liste (5.6), rechts eine Karte mit dem Formular. Einspaltig sonst (Firmendaten, Kassa, Berichte).
3. **Formular**: Felder in einem Raster `repeat(auto-fill, minmax(200px, 1fr))`, Abstand 12, höchstens 3 je Zeile (in einer Seitenleiste eine Spalte, Paare nur für zwei Kurzfelder); die Karte mit dem Formular ist höchstens 640 breit; jedes Feld mit Beschriftung oben und seiner Breite aus 5.2. Zusammengehörige Felder bilden eine Gruppe mit Zwischentitel und Hilfe-Knopf; vor der Gruppe 20 px.
4. **Zeilenlisten** (Arbeitszeiten, Preisregeln, Öffnungszeiten): eine Rasterzeile je Eintrag mit denselben Spalten, Spaltenköpfe einmal oben (Stufe „Beschriftung“), rechts außen ein Symbol-Knopf „Entfernen“ (Papierkorb, klein). Unter der Liste „Arbeitszeit hinzufügen“ als kleiner sekundärer Knopf. Kein roter Textknopf je Zeile.
5. **Knopfleiste** am Ende der Karte: 16 px Abstand zum letzten Feld, links „Speichern“ (primär, deaktiviert ohne Pflichtangaben), daneben „Verwerfen“ (sekundär, nur wenn es ungespeicherte Änderungen gibt; in Seitenleiste und Dialog stattdessen „Abbrechen“, Ghost, 5.1), rechts außen die gefährliche Aktion („Aus dem Team nehmen“). Nach dem Speichern erscheint die Meldung unten („Gespeichert“, ohne Punkt, 5.10), kein Text im Formular.
6. **Zustände**: Laden „Wird geladen …“ (fg-2, 14) an der Stelle des Inhalts; leer „Noch keine Standorte. Standort anlegen“ mit Symbol 24 fg-3, Text 14 fg-2, dem Knopf darunter; Fehler als Hinweis „Fehler“ (5.9) mit dem Satz des Servers und einem Knopf „Erneut laden“ (sekundär).
7. **Berichte**: Filterzeile (Standort, Von, Bis, Knopf) in einer Reihe mit 12 px Abstand, dann Kennzahl-Kacheln (5.5), dann Tabellen (5.7) mit Zwischentitel je Tabelle. Warnungen zu den Zahlen als Hinweis „Warnung“, nicht als roter Text. **Zahlen** (*geändert R07 Slice 144, Ä352*): genau eine Reiterebene (Tagesstart · Mein Tag · Hygiene · Berichte · Betriebsinfo, je nach Recht; Berichte nur mit dem Recht auf Berichte mit Zahlen, Betriebsinfo auch mit dem Recht zu buchen, T-025), Standort und Datum rechts im Kopf auf der Höhe der Reiter, Management und Pflichtberichte als Unterreiter rechts im Reiter „Berichte“. Tagesstart, Mein Tag und Hygiene beginnen mit dem Status (Hinweis „Erfolg“ „Nichts Dringendes offen“ bzw. Hinweis „Warnung“ mit den offenen Punkten darunter, 5.9), dann der Wertezeile (5.8), dann Abschnitten mit feinen Linien auf einer Fläche statt gerahmter Karten. Im Tagesstart steht das Personal als Zeitleiste von Öffnung bis Schluss (5.8) und die Kassa mit dem Anfangsbestand als Wert; Hinweise der Kassa stehen als Warnband.
8. **Dienstplan** (*geändert R07 Slice 142, Ä350, Auftraggeber 03.11.2026*): eine Fläche mit feinen Linien (`line`, 1 px) zwischen den Zeilen statt gerahmter Karten. Dienste sind Chips (5.8; Farben je Farbschema in 2.4, *T-039, Ä372*), Abwesenheiten ein Balken über die Tage (5.8), freie Tage ein schwaches „frei“ in fg-2 12/400 (nicht fg-3: Text unter 24 px). Beim Namen steht „geplant / Soll h“ in fg 12 (Werte dunkel, Bezeichnungen klein in fg-2). Die Spalte von heute trägt accent-soft hinter den Zellen und den Kopf in accent 600. Ein Entwurf trägt einen gestrichelten Rand (1 px accent, Strich 4/3, Radius 8), oben steht ein Band accent-soft mit 1 px accent-line, Radius 8, Innenabstand 8/12: „Entwurf: n Änderungen nicht veröffentlicht“ 600 mit **Veröffentlichen** (primär) und **Verwerfen** (ghost); eine Legende gibt es nicht. Unten zählt die Zeile „Besetzung“ (Personen im Dienst am Standort, 14 tabellarisch); eine Warnung dazu gibt es erst, wenn eine Mindestbesetzung hinterlegt werden kann. Zellen sind Knöpfe mit Innenabstand 4/6 und Rand 2; das Bearbeiten öffnet als Popover direkt unter der Zelle (Karte mit `RaisedShadow`, höchstens 560 breit, innerhalb der Ansicht; Ä370, T-017).

**Navigation der Verwaltung in der Windows-App (Ä293, Auftraggeber, 31.10.2026):** Statt einer Liste steht oben ein Balken im Stil der linken Leiste, etwas heller; die Masken der gewählten Gruppe stehen darunter als Reiter (5.4). Die Seite hat keine eigene Überschrift mehr, der Balken benennt sie.

- **Gruppen, feste Reihenfolge:** Betrieb (Firmendaten, Standorte & Räume, Schließzeiten, Raumsperren, Kalender-Abos, Handbuch), Angebot (Behandlungen, Aktionen), Team, Lager, Kassa & Geld (Kassa, Zahlungen, Kosten, Buchhaltung), Kontakt (Erinnerungen, Postfach & Nummern, KI), Zahlen (eine Maske mit einer Reiterebene: Tagesstart, Mein Tag, Hygiene, Berichte, Betriebsinfo; *geändert R07 Slice 144, Ä352*). Eine Gruppe ohne erlaubte Maske fehlt. Jede Gruppe merkt sich ihre zuletzt gewählte Maske.
- **Balken:** volle Breite der Seite, Fläche rail-hover, Innenabstand 8/6, Abstand zwischen Einträgen 4. **Eintrag:** Mindestbreite 72, Innenabstand 10/6, Radius 12, Symbol 22 über Text 12/500, dazwischen 4; darunter der Strich 20 × 3, Radius 2.
- **Zustände mit gemessenem Kontrast (WCAG-Formel, hell / dunkel):** Ruhe Text rail-fg-2 auf rail-hover 5,17:1 / 5,21:1. Überfahren Text rail-fg auf rail-bar-hover 7,67:1 / 10,85:1. Gewählt Text rail-fg 600 auf rail-selected 6,63:1 / 9,73:1, Strich rail-mark 3,87:1 / 6,14:1. rail-fg-2 reicht auf den helleren Flächen nicht (hell 4,48:1 auf rail-bar-hover, 3,87:1 auf rail-selected) und **DARF** dort **NICHT** stehen. rail-bar-hover ist die Mitte aus rail-hover und rail-selected.
- **Reiterzeile:** Schiene nach 5.4, Abstand 12 zum Balken und 16 zur Maske, nur bei mehr als einer Maske. Team und Lager haben keine Reiterzeile und behalten darunter ihre eigenen Reiter.
- **Tastatur:** Balken und Reiterzeile sind je ein Tab-Stopp (der gewählte Eintrag); ←/→, Pos1 und Ende bewegen den Fokus, Enter und Leertaste wählen (5.4). Erst wählen, dann laden: Pfeile allein öffnen keine Maske und lösen keine Rückfrage aus. F6 wechselt zwischen Leiste, Balken, Reitern und Maske.
- **Fokus im Balken:** Ring 2 px `rail-fg`, Radius 12: Der Akzent-Ring aus 4.5 erreicht auf `rail-hover` hell nur 1,25:1.
- **Zu schmal:** Einträge wandern von rechts in das Menü „Mehr“ (im Balken mit Symbol ChevronDown und Text wie ein Eintrag, in der Reiterzeile wie ein Reiter); nichts bricht um, nichts scrollt. Steht die gewählte Gruppe darin, zeigt „Mehr“ den Zustand „gewählt“, ist der Tab-Stopp heißt für den Screenreader und im Tooltip „Mehr, gewählt: …“, und das Menü markiert den Eintrag mit dem Punkt. **Gewollte Abweichung von 5.4** („scrollt waagrecht“) nur für die Reiterzeile dieser Seite: dieselbe Regel wie im Balken, verborgene Einträge bleiben per Tastatur erreichbar.
- **Mindestfenster und Kopf von „Zahlen“** (*T-038, Ä378, 06.11.2026*): Das Fenster der App ist mindestens **1100 × 680** DIP groß (`MinWidth`/`MinHeight` in `MainWindow.axaml`). Davon bleiben für den Kopf einer Maske 964 px (1100 − Leiste 88 − Rand 16 + 16 − Rand rechts 16), mit der Seitenleiste „KI und Hilfe“ (360) nur 604 px; bei 1024 px (Abnahme der Verwaltung) 888 bzw. 528 px. Der Kopf von „Zahlen“ hat links die Reiter (Schiene gemessen 497 px, nach 5.4 immer eine Zeile, nie umbrechen) und rechts Standort und Datum (gemessen 372 px): Passen beide nicht in eine Zeile (Bedarf 885 px), steht der Teil rechts rechtsbündig in Zeile 2 (`ReflowRow`). Der Wächter-Test `Die_fuenf_Reiter_passen_bei_Mindestbreite_mit_Seitenleiste_in_eine_Zeile` sorgt dafür, dass die Schiene bei 1024 px mit Seitenleiste in den Kopf passt; wird sie zu breit, scrollt sie nach 5.4 waagrecht, es gibt kein Überlaufmenü für Reiter.

## 7. Windows-App: Kalender und Kassa

Der Kalender folgt dem Rezeptions-Konzept (`docs/produkt/kalender-rezeption.md`); dieses Dokument ändert dort nur die Token (Abschnitt 2) und die Komponentenwerte (Abschnitt 5). Festgelegt für den Kalender zusätzlich:

- Terminkarte: Radius 6, Rand links 4 px Personenfarbe, Fläche st-*-bg, Text fg, Schatten `0 1px 2px rgba(0,0,0,0.06)`, Hover shadow-raised, gewählt Ring 3 px accent.
- Streifen (Vor-/Nachbereitung, Raumpflege): Fläche surface-3, Rahmen 1 px line-strong, Radius 4 (oben bzw. unten), Mindesthöhe 6, Text Mini fg; Raumpflege erledigt ok-soft, überfällig warn-soft mit 2 px warn links.
- Einwirkzeit-Band (Person frei, Raum belegt): im Termin der Person ab „Minuten am Gast“, Fläche surface-3 mit 55 % Deckkraft und diagonaler Schraffur in line-strong, 1 px line-strong oben und unten, Text Klein fg-2 am unteren Rand, nur ab 14 px Höhe und nur, wenn er die Textzeilen der Karte nicht deckt; nicht anklickbar; in der Raumansicht kein Band. Überlappen sich zwei Termine einer Person, teilen sie die Spalte in Bahnen zu gleicher Breite (Breite je Bahn minus 2 px Abstand), der später gebuchte rechts.
- Schließband (*Runde 5b, Block L, 22.10.2026*, ADR 0073): die Schließzeit des Standorts als Fläche über **allen** Spalten, Farbe nach Art aus vorhandenen Tokens: Betriebsurlaub accent-soft mit accent, Sonderschließung warn-soft mit warn, Ausfall danger-soft mit danger; Fläche mit 55 % Deckkraft, 1 px Rand oben und unten in der Farbe der Art, nicht anklickbar (die Termine darunter bleiben bedienbar). Der Text („Betriebsurlaub · Inventur“, 12/600) steht in einer Kapsel oben links (Fläche und Rand der Art, Radius 999, Innenabstand 2/8, Abstand 6/4, höchstens 360 breit), damit er auch über Terminen lesbar bleibt (Kontrast mindestens 4,5:1 gegen die Fläche). Ein halber Tag beginnt zur Uhrzeit und reicht bis zum Ende der gezeigten Stunden. Der Kopfzeilen-Chip steht in 5.8.
- Raumsperre (*Runde 6, Block N2, Ä65, 23.10.2026*, ADR 0076): in der Raumansicht ist die gesperrte Zeit in der Spalte des Raums **schraffiert wie eine Person ohne Dienst** (sie ist aus der verfügbaren Zeit herausgerechnet, das Raster zeichnet die Schraffur), darüber 1 px Rand oben und unten und oben links eine Kapsel mit Art und Grund („Wartung · Lüftung wird gewartet“) in den Werten des Schließbands (Radius 999, Innenabstand 2/8, Stufe Klein 12/600): Wartung und Sonstiges accent, Umbau warn, Schaden danger. Fläche und Kapsel liegen über den Terminen und sind nicht anklickbar. Kopf Zeile 2 in danger „gesperrt: Wartung bis 3.12.“ (halber Tag „ab 13:00“), mit offenen Terminen der Link „n Termine umbuchen“ (5.24) wie bei einer abwesenden Person; Hinweis am Kopf mit Schloss und Grund, höchstens fünf Zeilen (die Raumpflege-Zeile entfällt in der Zeit). Termine im gesperrten Raum tragen in Raum- und Teamansicht den Rand „umbuchen“ der Abwesenheit (4 px crit links) und in der Hover-Karte eine Zeile im orangen Band.
- Spaltenbreite (*Runde 6, Block N2, Ä65, 23.10.2026*): Team- und Raumansicht haben in Pixeln dieselbe Spaltenbreite: sie richtet sich nach der Zahl der **Personenspalten des Tages** (Fenster geteilt durch Personenspalten, mindestens 160), nicht nach der Zahl der Räume; mehr Spalten, als ins Fenster passen, scrollen waagrecht. Kopf (Höhe 38, zwei Zeilen) und Karten sind dieselben.
- Notizen im Kalender (*Runde 5, Ä31, 22.10.2026*): über dem Kalender steht kein Band „Heute“ mehr; die Tagesnotizen stehen als Gruppe „Notizen heute“ ganz oben in der Seitenleiste „Meldungen“ (5.12, Punkt 11), der Chip „Meldungen“ trägt bei Notizen einen Punkt neben dem Zähler.
- Kopfzeile des Kalenders (*festgelegt 21.10.2026, Ä16*): **höchstens zwei Zeilen**, nie eine dritte. Zeile 1: links Heute, Pfeile, Datum als Titel 22/600 mit Klappmenü; rechts Suche, Aktualisieren, Chip „Meldungen“, primärer Knopf „Termin anlegen“. Zeile 2: links der Umschalter Team/Räume, der Chip „Kalender“ (Kalender-Abos, nur mit Abos am Standort), Zähler, Sauna, Warteliste; rechts auf derselben Linie der Personenfilter („Nur mit Dienst“ / „Alle zeigen (n)“) bzw. in der Raumansicht der Raumfilter. Alle Elemente beider Zeilen sind Kopfzeilen-Chips (5.8), Abstand 8 px, Zeilenabstand 8 px. Wird es zu schmal, werden Chips kompakt (Symbol und Zahl, Text im Hinweis); reicht auch das nicht, wandern die Chips von Zeile 2 von rechts nach links in ein **Überlauf-Menü „⋯“** (5.21; Chip am rechten Ende der Zeile, Einträge mit Symbol und vollem Text in der Reihenfolge der Zeile), das auch die Tastatur erreicht. Es bricht nichts um, nichts scrollt waagrecht und nichts ist verborgen (*geändert 21.10.2026, ADR 0069*; Fluent 2 Toolbar). Dieselbe Regel (zwei Zeilen, Kopfzeilen-Chips) gilt für die Kopfzeilen von Kunden, Posteingang und Kassa. *Beim Bauen festgelegt (Schritt 17, 21.10.2026):* „Wird es zu schmal“ heißt: die sichtbare Breite der Spalten unter 1100 px (mit geöffneter Seitenleiste also früh); kompakt zeigen Zähler, Sauna, Warteliste, Personenfilter und Meldungen Symbol und Zahl, Team und Räume nur das Symbol; die Hinweise nennen dann den vollen Text. Der Zähler trägt das Symbol Kalender; Etikett „7 Tage“, Hervorhebung und „Tag einpassen“ sind Kopfzeilen-Chips in Zeile 2 (die ersten beiden `on`). Zeile 1 schrumpft nur über den Datumstitel (gekürzt mit „…“); einen Raumfilter gibt es nicht, in der Raumansicht bleibt rechts in Zeile 2 nichts.
- Spaltenköpfe (Personen und Räume gleich, *Ä12*): genau zwei Zeilen, Zeile 1 der Name 14/600 (gekürzt mit „…“, voller Name im Hinweis), Zeile 2 Stufe „Klein“ fg-2 (Person: Dienstzeit; mit Dienst an anderem Standort: Stecknadel und Standort, dann die Dienstzeit in derselben Zeile mit „·“; Raum: „Raumpflege 10 Min. · 13 Behandlungen“; *beim Bauen, Schritt 17:* die Zeile kürzt mit „…“ statt umzubrechen, und bei krank gemeldeten Personen mit offenen Terminen steht dort statt der Dienstzeit der Link „3 Termine umbuchen“, damit der Kopf zwei Zeilen behält; die Dienstzeit steht dann im Hinweis; Kopfhöhe 38 für alle Spalten). Links der Initialenkreis bzw. das Tür-Symbol (Paarraum: Symbol Personen-Paar statt Etikett), rechts höchstens ein Warn-Symbol 16. Keine Etiketten, keine dritte Zeile.
- Hinweis (Hover) am Spaltenkopf (*Ä14*): höchstens fünf Zeilen, je Zeile ein Symbol 16 und ein kurzer Text: Name und Art (Tür/Person: „Einzelraum“, „Paarraum · 2 Plätze“, bei Personen Dienstzeit und Standort), Raumpflege (Besen: „10 Min. nach jedem Termin“), Auslastung heute (Kalender: „13 Behandlungen“), Sprachen bei Personen (Sprechblase), Warnung (Dreieck, Kurzfassung); letzte Zeile der Klick-Hinweis. Keine Aufzählung der Behandlungen; die steht im Raum-Panel. *Beim Bauen festgelegt (Schritt 17, 21.10.2026):* „Auslastung heute“ heißt „13 Termine heute“ bzw. „Keine Termine heute“ (Zahl der Termine in der Spalte; die Zahl der möglichen Behandlungen des Raums steht schon in Zeile 2 des Kopfes); die Raumpflege-Zeile „Raumpflege 10 Min. nach jedem Termin“ bzw. „Keine Raumpflege nach dem Termin“ (Besen); Person: Dienstzeit (Uhr), „Dienst in Graz“ (Stecknadel), „Sprachen: Deutsch, Englisch“ (Sprechblase, nur mit eingetragenen Sprachen); die letzte Zeile trägt das Zeiger-Symbol.
- Streifen tragen kein Kontrollkästchen (*Ä13*): Zustand nur über Fläche und Text, ein Klick hakt die Raumpflege ab, der Hinweis sagt es.
- Nichts steht in der Spalte übereinander (*Ä22, festgelegt 21.10.2026*): was sich zeitlich überschneidet (Termine, Sperrzeiten, Pausen, Zettel), steht in Bahnen nebeneinander, Termine immer in der linken Bahn. Ohne Überschneidung volle Breite; zwei Termine je halbe Breite, der frühere links; Sperrzeit oder Zettel neben einem Termin in einer schmalen rechten Bahn (ein Drittel der Spaltenbreite, mindestens 72 px, Text mit „…“, voll im Hinweis), der Termin bekommt den Rest. Ein Zettel mit Ende (*Ä23*) reicht über die Dauer (mindestens 18 px, Text oben), ohne Ende ist er 22 px hoch.
- Konfliktmeldung (*Ä21, festgelegt 21.10.2026*): ein Hinweis nach 5.9, Art Fehler, mit dem Titel „Zeitraum ist belegt.“, je Belegung einer Zeile mit Symbol (Termin Kalender, Sperrzeit Schloss, Raum Tür, Gerät Werkzeug) und Text in **Ortszeit des Standorts** („Termin · 09:40–10:50 · Clara Fischer“; ohne Datum am Tag des Kalenders, sonst „Do. 22.10. 09:40–10:50“) und der Handlungszeile „Zuerst verschieben oder einen anderen Zeitraum wählen.“; eine Terminzeile ist anklickbar und markiert den Termin im Kalender. Der Server nennt Zeiträume nur als UTC-Zeitpunkte, nie in Textform.
- Tagesüberblick (*Runde 5, Ä30, 22.10.2026*): das Popover unter dem Chip „n Termine · n da · n offen“ ist 420 breit; „Als Nächstes“ zeigt je Termin zwei einzeilige Zeilen (Gast; Behandlung · Person), je höchstens 44 Zeichen, sonst „…“ und der volle Text im Tooltip (ohne Kürzung kein Tooltip). Ein Klick oder Enter auf einen Eintrag markiert den Termin im Kalender, öffnet sein Panel rechts (wie die Zeile der Konfliktmeldung, `OpenAppointment` markiert und öffnet) und schließt das Popover; ↑ ↓ Pos1 Ende bewegen den Fokus in der Liste, ohne Umlauf.
- Geburtstag des Gastes (*Runde 5b, Ä45, 22.10.2026*): am **Eintrag** nur ein kleines Geschenk-Symbol (`Icons.Gift`, 16 px, `accent`) rechts im Kopf hinter den Kennzeichen (Stern, Hinweis), ohne Text, nur am Tag des Termins und nur bei Gästen mit Geburtsdatum; Tooltip und Bildschirmleser „Geburtstag heute“ (der Tooltip hängt logisch unter dem Symbol, deshalb Kind-Selektoren, Test auf Kontrast 4,5:1). **Hover-Karte und Termin-Panel** haben eine Zeile mit demselben Symbol (14 bzw. 16 px): „Geburtstag heute · 42“ (das Alter nur dort und nur bei bekanntem Geburtsjahr), drei Tage davor und danach „Geburtstag am 25.10.“. Der **Tagesüberblick** hat oberhalb von „Als Nächstes“ die Zeile „Geburtstag heute: Brigitte Müller (10:30)“ (Symbol 14 px, 12/16, mehrere mit „ · “), nur wenn es einen gibt. Rechnen tut der Server (`GuestBirthday`), die Oberfläche setzt nur Worte (`BirthdayText`).
- Tagesfakten und Kalender-Abos (*Runde 5b, Ä43, 22.10.2026*; ADR 0075): ganztägige Feiertage stehen **im Datumstitel**, nicht als Band über den Spalten: Zeile 2 des Titels (Datum, Stufe „Klein“ fg-2) trägt hinter dem Datum „· Nationalfeiertag“ in 14/600 und fg (nur gesetzliche Feiertage und eine Schließzeit des Standorts als „Geschlossen: Betriebsurlaub“ (Block L), mehrere mit „ · “, höchstens 420 breit, sonst „…“); die Kopfzeile bleibt bei zwei Zeilen, Raumköpfe und Spalten ändern sich nicht (die Schließzeit zeigt sich außerdem als Band und als Chip, siehe 5.8 und 5.27). Der Tooltip am Datum nennt je Eintrag eine Zeile „Titel · Quelle, Stand“ (Ferien mit „bis 2.11.“) und zuletzt „Datum wählen (G)“; Schulferien, Hinweise und Sonstiges stehen nur dort und als erste Zeile des Tagesüberblicks („Heute: …“, 14/600, bricht um statt zu kürzen). Chip „Kalender“ (5.8): Klick öffnet ein Popover, 420 breit, mit Zwischentitel „Kalender-Abos“, Hilfezeile (Klein, fg-2) und je Abo einer Gruppe: Name 14/500, rechts der Schalter **Zeigen** (5.23, wirkt sofort), darunter Art und Stand (Klein, fg-2, bricht um), bei Fehler der Grund in warn, bei Feiertags-Abos der Hinweis zur Wirkung (Klein, fg-2), dann links der Schalter **Aktiv (für alle)** (nur mit dem Recht „Standorte verwalten“) und rechts der Knopf „Falsch melden“ (Ghost). Trenner `divider` zwischen den Abos; der Knopf „Falsch melden“ löst seinen Befehl aus und schließt das Popover erst danach (`Dispatcher.Post`), weil der Eintrag mit dem Schließen den DataContext verliert.
- Tastenübersicht „Tasten im Kalender“ (*Runde 5, Ä35, 22.10.2026*): Gruppentitel in der Stufe Zwischentitel (16/22, 600), Gruppen in der Reihenfolge Tag, Buchen, Markierter Termin, Seitenleiste und Reiter (Esc, Strg+Tab, Strg+W, Strg+1 … 9, F6), Allgemein; zwei gleich breite Spalten (links Tag und Buchen, rechts die übrigen drei), die Tastenspalte hat in beiden Spalten die Breite der längsten Taste („Rechtsklick Kopf“; `SharedSizeGroup`), die Beschreibungen beginnen bündig und brauchen höchstens zwei Zeilen (Texte entsprechend kurz), alle Tasten-Chips sind gleich hoch; unten der Link „Alle Tasten im Handbuch“ (öffnet das Hilfe-Thema `kalender-tastenkuerzel` und schließt die Übersicht). Die Karte ist höchstens 900 breit und scrollt senkrecht, wenn die Höhe nicht reicht.
- Kassa: Beträge tabular rechtsbündig, Belegvorschau in Monospace auf surface (Papier), Zahlungsknöpfe als Kacheln (Radius 10, surface-2, Rahmen line, Hover accent-soft/accent).
- Kassa, Spalten beim Kassieren (*Runde R06, Slice 129, Ä310, 03.11.2026*): vier Spalten mit 16 px Abstand. **Artikel und Gutscheine** (232 px, eingeklappt eine Leiste von 44 px; der Kopf ist ein Akkordeon-Kopf nach 5.29 mit Symbol, Titel 12/600 und Pfeil, der Pfeil zeigt zum Einklappen nach links), **Abrechnen** (192 px: Code scannen, Heute abzurechnen, Geparkte Rechnungen), **Rechnung** (der Rest) und **Bezahlen** (340 px, unverändert). Beide linken Spalten rollen jede für sich, nie waagrecht. Eingeklappt stehen auf der Leiste das Symbol, ein Pfeil nach rechts und der Name senkrecht; der Hinweis nennt „aufklappen“, der Name für den Screenreader bleibt in beiden Zuständen „Artikel und Gutscheine“. Der Zustand gilt je Arbeitsplatz und bleibt über den Neustart. Abweichung von 5.29: Der Kopf hat 10/10 Innenabstand und einen 14 px großen Pfeil in fg-3, damit der Titel in 232 px ungekürzt passt. *Beim Bauen festgelegt:* bei 1280 px Fensterbreite (mit der Navigation 1160 px Fläche) bleiben der Rechnung aufgeklappt 316 px, eingeklappt 504 px; wer mehr Platz für die Rechnung braucht, klappt die Spalte ein.

## 8. Dunkelmodus

- Beide Oberflächen folgen dem System; die App bietet zusätzlich den Schalter Hell/Dunkel (bestehend), das Web `data-theme="light|dark"` am `<html>` als Übersteuerung mit derselben Priorität wie in der App.
- Alle Token aus Abschnitt 2 haben beide Werte; ein Token ohne Dunkelwert **DARF ES NICHT** geben. Bilder im Handbuch bleiben hell (bekannt offen).
- Schatten im Dunkelmodus aus 2.5; keine aufgehellten Flächen zur Tiefenwirkung außer surface-2/surface-3.

## 9. Barrierefreiheit (WCAG 2.2 AA)

- Kontraste nach 2.6; Fokus nach 4.5; Mindestmaße nach 4.4 (Ziel 24 × 24 ist immer erfüllt). Der Brief folgt WCAG 2.2 AA, nicht 2.1 AA wie Fluent 2 (**gewollte Abweichung**, 2.2 ist strenger).
- Jede Beschriftung ist programmatisch an ihr Feld gebunden; jeder Symbol-Knopf hat einen Namen; jede Tabelle hat `th` mit `scope`; jede Reiterschiene ist eine `tablist`.
- Farbe ist nie der einzige Träger einer Information: Status hat Text oder Symbol (Kalender: Status steht in der Karte; Hinweise haben Symbol und Wortlaut).
- **Tastatur und Bereiche (Fluent 2 F27, F28, F29):** **F6** und **Umschalt+F6** wechseln zwischen den großen Bereichen: Navigation, Kopfzeile, Kalenderfläche, Seitenleiste und Reiter unten (nur vorhandene; „KI und Hilfe“ ist im Kalender ein Reiter der Seitenleiste, auf den Seiten ohne Reiterschiene ein eigener Bereich; eine Seite ohne eigene Bereiche zählt als einer); jeder Bereich trägt `AutomationProperties.Name` (`RegionNavigation`). Die Tab-Reihenfolge ist die Anzeigereihenfolge (Kopf, Chips, Karten, Aktionsblock, **zuletzt die Reiter unten**); ein Test gibt sie für Termin-Panel, Kundendaten-Maske und Rückfragedialog aus. Jedes bedienbare Element hat einen lesbaren Namen (ein Test prüft alle Seiten über die UI-Automation-Peers); Felder liefern Name, Rolle, Wert, Pflicht und Fehler. Tastenkürzel stehen im Tooltip bzw. im Menü und als `AcceleratorKey`.
- **Hochkontrast (F26, vorbereitet, nicht geprüft):** Ansichten und Stile enthalten keine festen Farben, alles läuft über `DynamicResource` auf `Colors.axaml` (Schatten als `BoxShadows`-Token); ein Test sucht Hex-Literale und Farbnamen in allen AXAML-Dateien (Ausnahmen mit Grund: die Palette der Systemvorlage und die weiße Schrift auf der Personenfarbe aus den Daten). Wie Avalonia die Kontrastdesigns von Windows abbildet, ist an einem echten Bildschirm noch zu prüfen.
- Bewegung respektiert „Animationen aus“ (4.6). Texte bleiben bei 200 % Zoom lesbar, nichts wird abgeschnitten (Umbruch statt Kürzen, außer 5.7 und Terminkarten mit Hinweis).

## 10. Verboten

- Farben, Größen, Radien, Schatten, Gewichte außerhalb der Tabellen. Ausnahme: Belegpapier (`.receipt`, `ReceiptPaper`) mit Schwarz auf Weiß für den Druck.
- `rem`, `em` für Größen im Web (nur `px`), Inline-Stile mit Farben (Ausnahme: Personenfarbe aus den Daten).
- Beschriftungen neben dem Feld, Platzhalter als Beschriftung, Felder ohne Beschriftung.
- Rote Warnungen, rote Textknöpfe je Zeile, zwei primäre Knöpfe, Knöpfe in Großbuchstaben, Knöpfe mit Schatten.
- Emoji, zweiter Symbolsatz, Schriftfamilien außerhalb Abschnitt 3.
- Text in fg-3 unter 24 px (normal) bzw. 18,5 px (fett); fg-3 gilt sonst nur für Symbole und die Feldunterkante.
- `TextBlock.overline`, Schrift in Versalien und Kursiv (Satzschreibweise, Mindestgröße 12).
- Zahl-, Betrags-, Uhrzeit- und Datumsfelder, die ungültige Eingaben stumm runden, begrenzen oder überschreiben, und Formeln in Zahlenfeldern (5.16 bis 5.20).
- Feste Farben in Ansichten und Stilen der Windows-App (`#…`, `White` in AXAML außerhalb von `Colors.axaml`) und Schatten mit eigenen Farbwerten (F26).
- KI-Ausgaben ohne Kennzeichnung nach 5.28 (Pille, Hinweiszeile mit „Warum?“, Weg ohne KI beim Fehler).
- Neue Komponenten ohne Eintrag in Abschnitt 5 (zuerst hier ergänzen, dann bauen).

## 11. Umstellungsliste (vom Stand 21.10.2026 zu diesem Brief)

Reihenfolge einhalten; jeder Punkt ist ein eigener, kleiner Commit mit Bild vorher/nachher aus den ViewTests bzw. aus Vitest-Schnappschüssen. Die Zielwerte der Schriftgrößen in W1, W11, W13, W14 und A1 und in der Abnahme sind mit ADR 0069 auf 12 und 16 gesetzt; Code, der nach den alten Werten (11,5 / 12,5 / 15) schon umgesetzt ist, zieht 11.4 (F23) nach. Der Umbau auf Fluent 2 steht in 11.4.

### 11.1 Web, `web/src/index.css`

| # | Stelle (Zeile am 21.10.) | Heute | Neu |
|---|---|---|---|
| W1 | `:root` Z. 30 | `font-size: 15px` | `font-size: 14px`; alle `rem`/`em`-Größen in `px` nach Abschnitt 3 und 4 umrechnen (`1.1rem` h2 → 18, `.98rem` h3 → 16, `.9rem` label → 12, `.85rem`/`.8rem` → 12, `.75rem` → 12) |
| W2 | `:root` Z. 5/41 | `--muted: #5d6764` / `#93a39f` | `--muted: #47524f` / `#a9b8b4`; neu `--fg-3: #757e7a` / `#7e8f8a` |
| W3 | `:root` | fehlt | neu `--ok`, `--ok-soft`, `--note`, `--note-soft`, `--accent-line`, `--hatch`, `--toast`, `--toast-fg`, `--st-booked`, `--st-booked-bg`, `--st-checked-in`, `--st-checked-in-bg`, `--st-done`, `--st-done-bg`, `--shadow-raised` mit den Werten aus Abschnitt 2; `--rail-mark` auch im Dunkelblock |
| W4 | `button` Z. 117–125 | Standard = Akzentverlauf, Gewicht 550 | Standard = Sekundär (surface, fg, line-strong, Radius 8, Höhe 34, Innenabstand 0/12, Gewicht 500); neu `button.primary` mit Verlauf; `button.danger` Umriss; `button.danger.filled` gefüllt; `button.ok`; `button.small`; `button.icon` |
| W5 | `button:hover` Z. 133–136 | Schatten | kein Schatten; Fläche/Rahmen nach 5.1 |
| W6 | `button:disabled` Z. 148 | Deckkraft 0,5 | bleibt 0,5 |
| W7 | alle Stellen mit primärer Bedeutung („Speichern“, „Einchecken“, „Buchen“, „Diesen Browser als Empfangsgerät anmelden“, „Preisregeln speichern“ …) | ohne Klasse | `className="primary"`, genau einer je Karte; alle übrigen bleiben ohne Klasse |
| W8 | `.segmented` Z. 357–386 | Radius 10/8, Text `--muted` | bleibt; gewählt Text `--accent`; Schatten `0 1px 2px rgba(0,0,0,0.13)`; `role=tablist/tab` ergänzen |
| W9 | `.notice` Z. 318–324, `.warning` Z. 1005, `.notice.warning` Z. 1010 | Radius 10, Warnung rot | Radius 8, Innenabstand 10/12; `.notice.warning` warn-soft/warn/warn; `.notice.error` danger; `.notice.ok` ok; `.warning` als Textklasse entfällt |
| W10 | `input, select, textarea` Z. 175–184 | Radius 8, Innenabstand .45/.6rem | Höhe 34, Innenabstand 6/10, Rahmen line-strong, Hover Rahmen `--muted`, `:read-only` surface-2; Breitenklassen `.w-num` 120, `.w-time` 100, `.w-date` 160, `.w-short` 280, `.w-select` 220, `.w-amount` 140 |
| W11 | `label` Z. 657, 855, 1751 | 13,5 px, teils neben dem Feld | 12/500 `--muted`, immer über dem Feld (`display:block; margin-bottom:4px`); alle `<label>` mit `HelpButton` darin: Knopf aus dem `<label>` herausziehen, `htmlFor` setzen; je Gruppe ein Knopf am Gruppentitel (5.11) |
| W12 | `section section` Z. 86–89 | surface-2, Radius 12 | Radius 8, Innenabstand 12/16 |
| W13 | `dl.kpis div` Z. 1499–1503, `dl.kpis dd` Z. 1511 | Radius .5rem, Wert 1.2rem | Radius 12, Innenabstand 12/16, Wert 22/600 tabular, Beschriftung 12/500 |
| W14 | `.edit-table`, `.roster-table` Z. 904–915, 1145–1170 | Kopf links, Zellen .2/.5rem | Kopf 12/500 `--muted`, Rahmen unten line-strong, Zahlenspalten rechts inkl. Kopf (`th.num, td.num { text-align:right; font-variant-numeric: tabular-nums }`), Zelle 8/12, Zeile min 36 |
| W15 | Team-Formular `TeamTab.tsx`, Standorte `LocationsTab.tsx`, Behandlungen `TreatmentsTab.tsx` | „Entfernen“ als `button.danger` je Zeile | `button.icon.small` mit Papierkorb und `aria-label="Entfernen"`; „Arbeitszeit hinzufügen“ als `button.small` |
| W16 | `.topbar` Z. 200–300 | zwei Zeilen | eine Zeile 48 px, zweite Zeile ins Kontomenü (5.13) |
| W17 | `.status-NeedsReview`, `.status-Failed` Z. 1101–1104 | `#b5524a` | `var(--warn)` |
| W18 | `.scan-answers fieldset.uncertain` Z. 1348–1365 | feste Gelbtöne | `--note-soft` / `--note` |
| W19 | Schatten Z. 1542, 1797, 1887, 1992 | feste rgb-Werte | `var(--shadow)` bzw. `var(--shadow-raised)`; die Seitenleisten im Web haben nach 5.12 keinen Schatten (umgesetzt am 21.10.2026) |
| W20 | `button:focus-visible` Z. 138–146 | Ring | bleibt; zusätzlich Kontrollkästchen `outline: 2px solid var(--accent); outline-offset: 2px` |

### 11.2 Windows-App, `Themes/Colors.axaml` und `Themes/Styles.axaml`

| # | Stelle (Zeile am 21.10.) | Heute | Neu |
|---|---|---|---|
| A1 | `Styles.axaml` Z. 9 | Grundschrift 13,5 | 14; `TextBlock.h2` 18, `TextBlock.h3` 16 (neu), `TextBlock.small` 12, `TextBlock.label` 12/500 Fg2 |
| A2 | `TextBlock.muted` Z. 26 | Fg3 | Fg2; Fg3 nur noch in Stilen für Größen ab 18 oder Symbole |
| A3 | `Button` Z. 206 | Radius 6 | 8; `TextBox` Z. 430 und `ComboBox` Z. 435 Radius 8; `Border.alert` Z. 80 Radius 8; `Border.segmented` Z. 408 Radius 10 |
| A4 | `Button.primary` Z. 240 | Rahmen 0 | Rahmen 1 `AccentHoverBrush`; `SplitButton.primary` Z. 348 Fläche `AccentGradientBrush` statt `AccentBrush` |
| A5 | `Colors.axaml` Z. 51–54 / 101–104 | Verlauf diagonal, dunkel `#62c2c8 → #45a3aa` | senkrecht (`StartPoint="0,0" EndPoint="0,1"` relativ), dunkel `#5cbcc3 → #3f9ca3` |
| A6 | `Border.card` Z. 69 | fester Schatten | je Theme die Werte aus 2.5 (als `BoxShadows`-Ressource `CardShadow` in beiden Themes); `Border.card` Padding 16,20 (neu, Z. 64–70; gemeint 16 oben/unten und 20 links/rechts, in Avalonia `20,16`; Karten, die ein eigenes Layout rahmen, setzen `Padding="0"`) |
| A7 | `Button.chip` Z. 363, `ListBox.chips` Z. 504, `ToggleButton.day` Z. 384 | Radius 16 / 14 / 16 | 999 (als `CornerRadius="999"`) |
| A8 | `Button:disabled` Z. 235 | Deckkraft 0,45 | 0,5 |
| A9 | `Border.flag.regular` Z. 634 | `#c08a00` | `WarnBrush` |
| A10 | `Colors.axaml` Z. 41/92 | `ScrimBrush` ungenutzt | entfernen, sobald `grep` keine Verwendung findet (StaticResource und Code prüfen) |
| A11 | fehlt | kein Fokusstil | `FocusAdorner` für `Button`, `TextBox`, `ComboBox`, `CheckBox`, `ListBoxItem`, `ToggleButton`: Rahmen 2 `AccentBrush`, Abstand 2 |
| A12 | `Border.stripe.care:pointerover` Z. 676 | AccentSoft (seit 21.10.) | bleibt |
| A13 | `Button.danger` Z. 274–283 | Umriss | bleibt; neu `Button.danger.filled` (Fläche Crit, Text AccentFg) nur für Rückfrage-Dialoge |
| A14 | `Window` | Schriftfamilie nicht gesetzt | `FontFamily="Inter, Segoe UI Variable Text, Segoe UI"` einmal am `MainWindow`; umgesetzt am 21.10.2026 als `fonts:Inter#Inter, Segoe UI Variable Text, Segoe UI`, weil „Inter“ allein nur eine auf dem PC installierte Schrift findet und die App sonst auf Segoe UI zurückfällt |
| A15 | `Border.infobar` (ADR 0067, `Styles.axaml`) | Radius 6, Innenabstand 12,8,8,8, Rahmen `AccentLineBrush` in Neutral | Radius 8, Innenabstand 10/12, Rahmen `LineBrush` in Neutral (5.9); Schließen-Knopf `Button.icon.small` 28 × 28 |
| A16 | `CustomerContactForm.axaml` (ADR 0067) | Gruppen 32, Felder 16, Spalten 16, Hilfetext 12 `Fg3`, Felder 32 hoch | Gruppen 20, Felder 12, Spalten 12 (4.1), Hilfetext Stufe „Klein“ `Fg2` (3), Felder 34 hoch (5.2) |
| A17 | `Border.toast` im Kalender | links unten, neutrale Fläche mit Streifen | mittig im Kalenderbereich, 16 px Abstand, Fläche je Variante `ToastBrush` / `WarnBrush` / `CritBrush` (5.10); `ToastDuration` 5 s, mit „Rückgängig“ 8 s |

### 11.3 Gemeinsam

| # | Was |
|---|---|
| G1 | `docs/README.md` und `CLAUDE.md` verweisen auf dieses Dokument („vor Änderungen an der Oberfläche lesen“). |
| G2 | Ein Vitest- und ein Desktop-Test lesen die Token aus beiden Dateien und vergleichen die Hex-Werte paarweise (Tabelle 2.1 bis 2.4); die CI wird rot, wenn Web und App auseinanderlaufen. Umgesetzt am 21.10.2026: die Namenspaare stehen in `docs/design-tokens.json`, ein neues Token kommt zuerst dorthin. |
| G3 | Handbuchbilder der Verwaltung (`web-masterdata-*.png`, `web-staff-*.png`, `web-reports-*.png`) nach W1 bis W16 neu erzeugen (`scripts/help-images.sh`). |

### 11.4 Umstellung auf Fluent 2 (ADR 0069, Stand offen)

Reihenfolge und Prüfung je Punkt stehen in der Ableitung ([gestaltung/fluent2-ableitung.md](gestaltung/fluent2-ableitung.md), Abschnitt 7; `D/` = `src/AispaStudio.Desktop/`). Umgesetzt wird auf dem Branch `fluent2`; jeder Punkt ist ein eigener, kleiner Commit mit Bild vorher/nachher (hell und dunkel). Der Token-Vergleichstest (G2) prüft nur die Farben aus 2.1 bis 2.4 und bleibt von diesen Punkten unberührt.

| # | Was | Brief | Stand |
|---|---|---|---|
| F1 | gemeinsamer Feldrahmen `FormField` (Beschriftung, Hilfetext oder Fehler, Pflichtstern, Automation) | 5.2, 5.16 | erledigt (Block A, 22.10.2026) |
| F2 | `TextBox`- und `ComboBox`-Stil: Unterkante fg-3, Hover fg-2, Fokus-Unterstrich 2 px accent (200 ms ein, 50 ms aus), Warnung und Fehler am Feld | 2.6, 4.5, 5.2 | erledigt (Block A, 22.10.2026; TextBox und ComboBox) |
| F3 | Markierung beim Betreten (Tab markiert bei Zeit, Datum, Zahl, Betrag, Suche; Textfelder Marke am Ende) | 5.16 | erledigt (Block A, 22.10.2026) |
| F4 | Esc und Enter in Feldern (Esc in Stufen, Enter löst den Standardknopf) | 5.16 | erledigt (Block A, 22.10.2026; Enter löst `Button.IsDefault` aus, gesetzt bei Eintrag, Verschieben, Zeiten ändern, Kundendaten) |
| F5 | `TimeField` mit `TimeInputParser` | 5.18 | erledigt (Block A, 22.10.2026) |
| F6 | `DateField` mit `DateInputParser` und Kalender-Popover | 5.17 | erledigt (Block A, 22.10.2026) ohne Kalender-Popover: die App hat keinen Datumswähler, das Feld ist ein Textfeld mit Leser |
| F7 | `NumberField` (ersetzt `NumericUpDown`) | 5.19 | erledigt (Block A, 22.10.2026) |
| F8 | `MoneyField` mit `MoneyInputParser` | 5.20 | erledigt (Block A, 22.10.2026; CHF-Kassen zeigen noch das österreichische Format) |
| F9 | `ComboBox` und Liste: Eintrag 32, Radius 8, Verhalten ohne Wahl beim Markieren | 5.3 | erledigt (Block A, 22.10.2026; die Tastatur der Avalonia-ComboBox entspricht schon dem Brief) |
| F10 | `TagPicker` (ersetzt Chip-Wolken über sechs Werte) | 5.26 | weggelassen (Block A, 22.10.2026): die Windows-App hat keine Mehrfachauswahl über sechs Werte; Qualifikationen, Standorte und Räume stehen in der Verwaltung im WebView2, die 7 Wochentage sind laut 5.3 Auswahl-Chips, die freien Zeiten eine Einzelauswahl |
| F11 | mehrzeiliges Feld: Höhe 68, `MaxHeight` 200, Zähler | 5.16 | erledigt (Block A, 22.10.2026) |
| F12 | Suchfeld: Löschen-Kreuz, zuletzt geöffnete Gäste, „Keine Treffer.“ | 5.16 | teilweise (Block A, 22.10.2026): Lupe, Kreuz nur mit Text und Esc in Stufen erledigt; die zuletzt geöffneten Gäste und „Keine Treffer.“ mit „Neuen Gast anlegen“ bleiben offen |
| F13 | Knopf-Ergänzungen: `Button.link`, Tooltip mit Grund an deaktivierten Knöpfen, Haken am gewählten Umschalt-Knopf, Mindestbreite 96 | 5, 5.1, 5.24 | erledigt (Blöcke B bis D, 22.10.2026; `Button.link`, `DisabledHint` („Verfügbar, sobald …“) an rund 30 deaktivierten Knöpfen, Mindestbreite 96 mit Ausnahmen für Symbol-, Chip-, Reiter-, Kachel- und Pfeilknöpfe (`.small` 64); Haken nur am Personenfilter, die Sauna ist kein Umschalt-Knopf; Knopftexte über 26 Zeichen bleiben dort, wo das Handbuch sie zitiert und sie allein stehen (Ausnahmeliste im Test)) |
| F14 | Kontrollkästchen und Optionsfelder (Rand fg-3, ein Tab-Stopp je Gruppe), `ToggleSwitch` | 5.2, 5.23 | erledigt (Blöcke B bis D, 22.10.2026; Optionsgruppe als ein Tab-Stopp, Schalter 40 × 20 mit eigener Vorlage, Filter „Nur Prüfen“ der Anfragen ist ein Schalter) |
| F15 | Reiter-Tastatur (← → Pos1 Ende, ein Tab-Stopp je Schiene), Strg+W, Strg+1 bis 9; Reiter unten kürzen bzw. nur Symbol (Ä19) | 5.4, 5.12 | erledigt (Blöcke B bis D, 22.10.2026; Werte in 5.12, Punkt 7) |
| F16 | Menü und Kontextmenü (`MenuFlyout`, `MenuItem`) | 5.21 | erledigt (Blöcke B bis D, 22.10.2026; Rechtsklick am Termin ist flach (bis 16 Einträge), weil 5.21 keine Untermenüs erlaubt; ein sehr langer Eintrag wird bei 300 px abgeschnitten, nicht mit „…“ gekürzt; der Eintrag „Behandlungszeiten bearbeiten …“ passt seit Runde 5, Ä32-Nachtrag, und ein Test prüft, dass kein Menütext abgeschnitten wird) |
| F17 | Tooltip: 250 ms, Höchstbreite 240, 12/16 | 5.22 | erledigt (Blöcke B bis D, 22.10.2026; die Ausblendverzögerung 250 ms und die Zeilenhöhe 16 sind **weggelassen** (Avalonia kennt keine Verzögerung zum Ausblenden); die Hover-Karten im Kalender behalten 300/400/500 ms und 420 Breite; der Schatten ist im echten Popup-Fenster nicht geprüft) |
| F18 | Überlauf-Menü „⋯“ in der Kalender-Kopfzeile statt waagrechtem Scrollen | 7 | erledigt (Blöcke B bis D, 22.10.2026; `OverflowStrip`, der angeheftete Personenfilter läuft zuerst über) |
| F19 | Rückfragedialog `ConfirmDialog` in der App; Web-Modal auf Reihenfolge und Innenabstand 24 | 5.5, 5.27 | erledigt (Blöcke B bis D, 22.10.2026, nur Windows-App; zwölf Rückfragen laufen über den Dialog; mit zweiter Aktion stehen die drei Knöpfe untereinander; ein Dialog hat zusätzlich ein optionales Grund-Feld; die Wartelisten-Auswahl „Entfernen“ bleibt eine Auswahl im Panel; das Web-Modal: erledigt in R02 Slice 38, `ConfirmDialog` in `web/src/components/`) |
| F20 | Rückfrage „Änderungen verwerfen?“ beim Schließen einer Seitenleiste mit Änderungen | 5.27 | erledigt (Blöcke B bis D, 22.10.2026; Kreuz, Esc, Strg+W und Reiter-Kreuz fragen, wenn Eintrag, Buchung, Gruppe, Bereiche, Anfrage oder Termin ungespeicherte Eingaben haben) |
| F21 | Toast: höchstens 60 Zeichen, ohne Schlusspunkt (Texte, `meldungen.md`); Fokus bei Formularfehler auf das erste ungültige Feld | 5.10, 5.14 | erledigt (Blöcke B bis D, 22.10.2026, nur Kalender; Fehler und Warnungen einer Aktion stehen als InfoBar oben statt als Toast; es steht höchstens **eine** InfoBar zugleich, nicht zwei (5.15); der Fokus auf das erste ungültige Feld beim Speichern gibt es nur in der Kundendaten-Maske (schon vorhanden, getestet); Verschieben, Eintrag und Zeiten ändern zeigen den Fehler am Feld bzw. sperren den Knopf, ein Fokussprung fehlt dort und bleibt **offen**) |
| F22 | Bewegungswerte: Hover und Fokus 100 ms, Kurven, `ReducedMotion` setzt alle Übergänge auf 0 | 4.6 | erledigt (Blöcke E bis G, 22.10.2026; Hover von Fläche, Rahmen und Text an Knöpfen neu, Meldung ein 160 / aus 120 ms, Seitenleiste ein 200 / aus 120 ms mit den Fluent-Kurven; „Animationen aus“ ohne jeden Übergang, Test über alle Seiten) |
| F23 | Schriftstufen 12 / 16 und Zwischentitel 16/22 in `D/Themes/Styles.axaml` und `web/src/index.css` (`docs/design-tokens.json` enthält nur Farben) | 3 | erledigt (Blöcke E bis G, 22.10.2026; Stufen mit Zeilenhöhen in App und Web, `docs/design-tokens.json` führt jetzt auch Schriftstufen, Schriftgrößen und Abstände, Tests in Web und App; das Gewicht 650 im Web und Bold in der App entfallen (700 bleibt nur im Belegpapier); Anmeldemaske 34 und 24 bleibt als benannte Ausnahme) |
| F24 | `TextBlock.overline` entfernen, Versalientexte in `CalendarView.axaml` auf Satzschreibweise | 3, 10 | erledigt (Blöcke E bis G, 22.10.2026; 44 Stellen in fünf Ansichten, jetzt `TextBlock.group`; „SONDERFÄLLE“, „PAAR“, „SPA-TAG“ ebenfalls; Handbuchtexte angeglichen, die Handbuchbilder zeigen noch die Versalien) |
| F25 | Abstände 2, 6, 10 erlaubt | 4.1, 12 | erledigt (Brief; `docs/design-tokens.json` führt die Abstände, Blöcke E bis G) |
| – | Fortschrittsbalken (Sicherung, Wiederherstellung, Einspielen) und Skeleton (Tages- und Kundenliste); ohne F-Nummer, Ableitung 3.6 | 5.25 | offen |
| – | Web: Feldunterkante fg-3 an `input`, `select`, `textarea` (Nachzug zu W10) | 5.2 | offen |

| F26 | Hochkontrast vorbereiten: keine festen Farben in AXAML, Schatten als Token | 9, 10 | erledigt, ohne Prüfung mit Windows-Kontrastdesigns (Blöcke E bis G, 22.10.2026; Scan-Test mit Ausnahmeliste) |
| F27 | F6-Bereichswechsel mit benannten Bereichen | 9 | erledigt (Blöcke E bis G, 22.10.2026; `RegionNavigation`; Namen der Bereiche stehen, ob Narrator sie beim Wechsel ansagt, ist nicht geprüft) |
| F28 | Narrator-Prüfung als Regel: Fokusreihenfolge, Namen, Rolle, Wert, Pflicht, Fehler | 9, 12 | erledigt als Test und Regel (Blöcke E bis G, 22.10.2026); der Lauf mit Narrator selbst steht aus |
| F29 | Tastenkürzel sichtbar (Tooltip, Menü, `AcceleratorKey`) | 5.21, 5.22 | erledigt (Blöcke E bis G, 22.10.2026; `ShortcutHints`, Tests lesen den Quelltext nach Kürzeln) |
| F30 | KI-Kennzeichnung nach 5.28 | 5.28 | erledigt für die Windows-App (Blöcke E bis G, 22.10.2026); offen: Ausschalter je Funktion, Erstöffnungskarte, Protokoll, KI-Zeichen am Termin, Rückmeldung am Server, Web-Nachzug (`AiFooter` und `AiPill` fehlen in der Team-Oberfläche) |
| Ä28 | Tooltip am Streifen „Vorb. 15 Min.“ unlesbar (Runde 5, Block H) | 5.22 | erledigt (Block H, 22.10.2026; die Regel `Border.stripe TextBlock` färbte den Text im Tooltip in fg auf die dunkle Fläche; jetzt `Border.stripe > TextBlock`; Test über alle Texthinweise des Kalenders in hell und dunkel) |
| Ä29 | Symbol-Reiter: bis zu fünf sichtbar, „⋯ n“ erst ab dem sechsten | 5.12 | erledigt (Block H, 22.10.2026; Punkt 7) |
| Ä30 | Tagesüberblick: 420 breit, „Als Nächstes“ einzeilig, Klick markiert und öffnet, Tastatur | 7 | erledigt (Block H, 22.10.2026) |
| Ä32 | Kontextmenü am Termin: Symbole an den wichtigsten Einträgen, „Kommt später“ nur 5, 10, 15 | 5.21 | erledigt (Block H, 22.10.2026) |
| Ä35 | Tastenübersicht: Zwischentitel, neue Gruppe Seitenleiste und Reiter, feste Tastenspalte, Link zum Handbuch | 7 | erledigt (Block H, 22.10.2026) |
| Ä31 | „Heute“-Band entfernt; Tagesnotizen als Gruppe „Notizen heute“ in der Seitenleiste „Meldungen“; Notiz-Punkt am Chip; „.“ öffnet das Notizfeld | 5.12 (11), 5.15, 7 | erledigt (Block I, 22.10.2026; die Tagesnotiz-Seite als eigener Reiter entfällt, Abwesende ohne offene Termine zeigen nur die Spalten) |
| Ä33 | Notiz zum Termin im Termin-Panel: Link „Notiz hinzufügen“, Hinweisfläche mit Stift, mehrzeiliges Feld mit Zähler | 5.12 (11), 5.16 | erledigt (Block I, 22.10.2026; neuer Endpunkt `PUT /api/appointments/{id}/note`) |
| Ä34 | Abschnitt „Kunde“ im Termin-Panel: Akkordeon mit vier Kennzahlen, Kundennotiz, Zuletzt, klickbarer Kontakt (Anruf, WhatsApp, E-Mail, Kopieren) | 5.12 (11), 5.29 | erledigt (Block I, 22.10.2026; neuer Endpunkt `GET /api/customers/{id}/summary`; die Kundenübersicht hinter dem Namen bleibt daneben bestehen) |
| Ä38 | Schließzeiten: Schließband über allen Spalten (Farbe nach Art), Kopfzeilen-Chip „Geschlossen · Betriebsurlaub“, Meldung „n Termine in der Schließzeit“, Rückfrage „Termine absagen?“ (600 breit, mit Liste und Text) | 5.8, 5.27, 7 | erledigt (Block L, 22.10.2026; neue Endpunkte unter `/api/locations/{id}/closures`) |
| Ä43 | Kalender-Abos: Feiertag im Datumstitel mit Hinweis zu Quelle und Stand, Schulferien nur im Hinweis und in der ersten Zeile des Tagesüberblicks, Chip „Kalender“ mit Liste, Schaltern „Zeigen“ und „Aktiv (für alle)“, „Falsch melden“ und Punkt bei ausgeblendeten Abos | 5.8, 5.23, 7 | erledigt (Runde 5b, Block K4, 22.10.2026; Chip, Liste, Titel, Tagesüberblick und Dialog in hell und dunkel durchgesehen, Kontrast des Hinweises und des Punkts geprüft; Narrator nicht) |
| Ä66 | KI-Knopf als Reiter der Seitenleiste: „KI und Hilfe“ ist im Kalender ein Reiter neben Warteliste, Meldungen …; Schließen lässt die übrigen stehen; Knopf und Strg+Leertaste wählen den Reiter; auf den Seiten ohne Reiterschiene bleibt die Leiste der Shell | 5.12 (7), 5.28 | erledigt (Runde 6, Block N3, 23.10.2026; Bildtests `ki-*` neu; Narrator nicht geprüft) |
| Ä68 | Ausgabefenster ohne Bindungsfehler und ohne Zeitüberschreitungen beim Start: `?.` und `TargetNullValue` statt Fehlermeldung bei absichtlich leeren Eigenschaften (Binding-Protokoll im Test), Start mit Zeitlimit 3 s, Wiederholung alle 2 s und Anzeige „Verbindung wird aufgebaut …“ | 5.28, 5.9 | erledigt (Runde 6, Block N3, 23.10.2026) |

Web-Nachzug zu E bis G bleibt offen: Handbuchbilder mit den alten Versalien, KI-Kennzeichnung in der Web-Oberfläche, F6 und Fokusreihenfolge in der Verwaltung im WebView2.

## 12. Abnahme-Checkliste

Vor dem Merge jeder Oberflächenänderung, Punkt für Punkt mit Ja beantworten:

1. Jede Farbe im Diff ist ein Token aus Abschnitt 2 (grep auf `#` und `rgb` im Diff findet nur Token-Definitionen).
2. Jede Größe ist ein Wert aus Abschnitt 3 oder 4 (Schrift 12 / 14 / 16 / 18 / 22; Abstände 4 / 8 / 12 / 16 / 20 / 24 / 32, dazu 2 / 6 / 10 nur als Innenabstand um Symbole und in Feldern; Radien 4 / 6 / 8 / 10 / 12 / 999).
3. Je Karte, Dialog, Seitenleiste höchstens ein primärer Knopf; gefährliche Knöpfe sind Umriss (außer Bestätigen im Dialog).
4. Jede Beschriftung steht über ihrem Feld und ist an es gebunden (`htmlFor` / `AutomationProperties.LabeledBy`); jeder Symbol-Knopf hat einen Namen.
5. Zahlenspalten sind rechtsbündig samt Kopf und tabular.
6. Warnungen sind warn, Fehler danger, Erfolg ok; nichts davon rot „weil auffällig“.
7. Fokus mit Tab ist auf jedem Element sichtbar (Ring plus Rahmen).
8. Kontrast gemessen (z. B. mit der Lighthouse-Prüfung oder dem Avalonia-DevTools-Farbwähler): Text ≥ 4,5:1, Symbole ≥ 3:1, Feldunterkante (fg-3) ≥ 3:1.
9. Hell und Dunkel beide angesehen (Bild je Modus im PR).
10. Hover hat keinen Schatten, Hover und Fokus bewegen sich 100 ms mit den Kurven aus 4.6, bei „Animationen aus“ gar nicht.
11. Der Token-Vergleichstest (G2) ist grün.
12. Das Handbuchbild der geänderten Seite ist neu erzeugt.
13. Neue und geänderte Eingabefelder folgen 5.16 bis 5.20 und haben Parser- und Tastaturtests (Headless); eine ungültige Eingabe ergibt einen Fehlertext, nie einen Absturz und nie stummes Runden.
14. Jede Abweichung von Fluent 2 trägt den Vermerk „gewollte Abweichung von Fluent 2“ mit Grund (Abschnitt 0.1).
15. **Fokusreihenfolge und Screenreader (F28):** für jede neue oder geänderte Maske steht die Fokusreihenfolge als nummerierte Liste im PR-Text (aus dem Headless-Test); Name, Rolle, Wert, Pflicht und Fehler der Felder liefert die UI-Automation (Stichprobe mit Testausgabe); der Narrator-Lauf ist abgehakt oder als „nicht geprüft“ vermerkt.
16. **KI-Ausgaben (F30):** jede neue KI-Stelle trägt Pille, Hinweiszeile mit „Warum?“ und einen Weg ohne KI beim Fehler (5.28), ist mit `[AiOutput]` ausgezeichnet, und die Bewertung nach der Rubrik (Ableitung 5.4) steht im PR-Protokoll.

## 13. Pflege

- Neue Werte, Komponenten oder Abweichungen zuerst hier eintragen (mit Datum und Grund), dann bauen. Grundsätzliche Änderungen (neue Schrift, neue Akzentfarbe, andere Knopf-Rangfolge) brauchen einen ADR.
- Dieses Dokument wird bei jeder Änderung an `index.css`, `Colors.axaml` oder `Styles.axaml` gegengelesen; der PR-Text nennt die Nummer aus Abschnitt 11, die er umsetzt.
- Quellen: Inventar vom 21.10.2026 (Sitzung Claude Code; Befunde: Knopf-Rangfolge invertiert, Radien 6/8/10 gemischt, Grundschrift 13,5/15, Warnungen rot, `Fg3` unter 4,5:1), ADR 0004 (Avalonia und React), ADR 0053 (Verwaltung eingebettet), ADR 0055 (Hilfe je Einstellung), ADR 0066 (Seitenleisten), Rezeptions-Konzept (Farben und Symbole im Kalender), Microsoft Fluent 2 (Verhalten und Zustände der Windows-App, vollständig gelesen in [gestaltung/fluent2-ableitung.md](gestaltung/fluent2-ableitung.md), ADR 0069) und Google Material 3 als Referenz für Mindestmaße, Fokus und Bewegung, WCAG 2.2 AA für Kontrast und Fokus.
