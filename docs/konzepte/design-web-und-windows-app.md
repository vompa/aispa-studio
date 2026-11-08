# Ein Design für Web und Windows-App

Stand: 03.11.2026. Anlass: Bei ersten Tests sahen Kunden-App, Mitarbeiter-App und Dashboard deutlich anders aus als die Windows-App. Das Dokument ist ein Konzept, keine Entscheidung. Die Entscheidungen gehören als Nachtrag zu ADR 0069 und als Änderung des Design-Briefs (`docs/design.md`) in einen eigenen ADR.

Wie belastbar ist das? Alle Werte sind aus den Dateien des Repositorys vom 03.11.2026 gelesen. Die Ursache ist an den Importen der Web-Apps nachgeprüft.

## Empfehlung in Kürze

- `docs/design-tokens.json` wird die einzige Quelle für alle Werte: Farben, Schrift, Abstände, Höhen, Radien, Schatten und Dauer.
- Das Web hat eine Token-Datei für alle Apps. Die zweite Palette `web/src/tokens.css` entfällt.
- Alle Web-Apps zeigen Inter, wie die Windows-App.
- Am Handy gilt dasselbe Bild. Ausnahmen gibt es nur bei Touch-Bedienung.
- Die Web-Apps nutzen gemeinsame Bausteine mit Aufbau und Zuständen wie in der Windows-App.
- Ein Test misst die gerenderten Werte in beiden Oberflächen. Eine Bildtafel zeigt Web und App nebeneinander.

## Ursache

Der Design-Brief gilt schon heute für beide Oberflächen. Die Web-Apps folgen ihm trotzdem nicht, aus fünf Gründen.

1. **Zweite Palette.** Kunden-App, Mitarbeiter-App und Dashboard laden `web/src/tokens.css`. Die brief-konforme `web/src/index.css` lädt seit Slice 103 nur noch die Hilfe. Brief, Token-Test und Umstellungsliste nennen nur `index.css`.
2. **Ungeprüft.** Kein Test vergleicht `tokens.css`, `booking.css`, `myday.css` oder `dashboard.css` mit der Windows-App.
3. **Der Brief selbst schreibt Unterschiede vor.** Kunden-App und „Mein Tag“ sollen bei der Systemschrift bleiben (Abschnitt 3); ein Test erzwingt das.
4. **Lücken in der Wertequelle.** `docs/design-tokens.json` führt nur Farben, Schriftstufen und Abstände. Höhen, Radien, Schatten und Dauer fehlen.
5. **Widersprüchliche Regeln.** Der Skill `fluent2-web` lässt `tokens.css` gelten und empfiehlt `rem`. Der Brief verlangt `px` und eine Wertequelle.

## Gemessene Unterschiede

| Eigenschaft | Windows-App | Kunden-App und Mitarbeiter-App |
|---|---|---|
| Akzent hell | `#0d5c63` | `#2a6354` |
| Hintergrund hell | `#f3f0ea` | `#f7f5f2` |
| Fokus | Akzent | Blau `#1b4fd1` |
| Schrift | Inter, 14 px | Systemschrift, 16 px |
| Knopf | 34 px, Rand 1 px, sekundär als Standard | 44–48 px, Rand 2 px, gefüllt als Standard |
| Feld | 34 px, Radius 8 | 44 px, Radius 6 |
| Karte | Radius 12, Schatten | Radius 12, ohne Schatten |
| Primärknopf | Verlauf | einfarbig |
| Einheit | px (DIP) | rem |

Das Dashboard nutzt dieselbe Palette wie die Apps, Kartenradius 8 und Schrift 28–44 px.

## Regeln

### R1 Eine Wertequelle

- `docs/design-tokens.json` enthält jede Wertart: Farben (hell, dunkel), Schriftfamilie, Schriftstufen, Abstände, Höhen von Knopf und Feld, Radien, Schatten, Dauer und Kurven.
- Das Web hat genau eine Token-Datei, die alle Apps laden. `tokens.css` entfällt; ihre eigenen Namen (`--soft`, `--success`, `--notice`) werden auf die Namen des Briefs umgestellt.
- Die Windows-App behält `Colors.axaml` und `Styles.axaml`.
- Tests auf beiden Seiten vergleichen jede Wertart mit der JSON. Eine Abweichung macht die CI rot.
- Werte stehen nur in der Token-Datei. Stylesheets der Apps verwenden nur Variablen; ein Test sucht nach Farb- und Größenwerten außerhalb der Token-Datei.

### R2 Eine Schrift

- Alle Web-Apps laden Inter aus `web/src/assets/fonts/` (39.784 Bytes, Latein, variabel), mit `font-display: swap`. Während des Ladens gilt die Systemschrift.
- Der Brief (Abschnitt 3) und der Test `interFont.test.ts` werden angepasst. Ob die Schrift in das Budget von 100 kB zählt, prüft `check-bundles.mjs`; die Prüfung wird bei der Umsetzung festgelegt.

### R3 Handy: gleiches Bild, benannte Ausnahmen

- Farben, Schrift, Radien, Ränder, Schatten und Knopfarten sind wie in der Windows-App.
- Bei Touch-Bedienung (`pointer: coarse`) werden Knöpfe und Felder 48 px hoch (Brief 4.4).
- Bei Touch-Bedienung haben Eingabefelder 16 px Schrift. Bei kleinerer Schrift vergrößert Safari am iPhone die Seite beim Tippen.
- Jede weitere Abweichung steht im Brief an ihrer Stelle, mit einem Satz Grund.

### R4 Tafel im Pausenraum

- Die Tafel nutzt dieselben Farben, Radien und Formen.
- Für 3 m Abstand bekommt sie eine eigene, benannte Schriftstufe im Brief.

### R5 Gemeinsame Bausteine

- `web/src/components` bekommt die Bausteine Knopf, Feld, Karte, Meldungsleiste, Dialog, Liste und Pille.
- Aufbau, Knopfarten und Zustände (Hover, Gedrückt, Fokus, Gesperrt, Fehler) sind wie in der Windows-App.
- Alle Web-Apps nutzen nur diese Bausteine. Ein Test findet eigene Knopf- und Feldstile in den App-Stylesheets.
- Fluent 2 gilt für das Verhalten auch im Web (Nachtrag zu ADR 0069). Fluent UI React bleibt ausgeschlossen.

### R6 Prüfung im Bild

- Eine Bausteinseite zeigt jeden Baustein in jedem Zustand, hell und dunkel. Sie wird im Web mit Chrome und in der Windows-App headless gerendert.
- Ein Test liest die gerenderten Werte aus, etwa Höhe, Radius, Farbe, Schrift und Rand, und vergleicht sie mit der JSON.
- Ein Pixelvergleich ist nicht vorgesehen. Chrome und Skia glätten Kanten und Schrift unterschiedlich, und der Vergleich würde oft falsch anschlagen.
- Eine Bildtafel legt Web und App nebeneinander. Sie wird bei jedem Rundenabschluss angesehen (Brief, Abschnitt 12).

### R7 Vorrang der Regeln

- Der Brief ist die Wertequelle. Wo der Skill `fluent2-web` etwas anderes sagt (`tokens.css` gilt, `rem`), gilt der Brief.
- `CLAUDE.md` nennt künftig die Token-Datei des Webs und die Bausteine, nicht nur `index.css`.

## Reihenfolge

| Nr. | Schritt | Aufwand |
|---|---|---|
| 1 | `design-tokens.json` um alle Wertarten erweitern, Tests auf beiden Seiten | M |
| 2 | Eine Token-Datei fürs Web, `tokens.css` entfernen, Namen umstellen | M |
| 3 | Inter in allen Web-Apps, Brief und Schrift-Test anpassen | S |
| 4 | Gemeinsame Bausteine in `web/src/components` | L |
| 5 | Kunden-App, Mitarbeiter-App und Dashboard auf die Bausteine umstellen | L |
| 6 | Bausteinseite, Messtest und Bildtafel | M |
| 7 | Brief, ADR 0069 (Nachtrag), `CLAUDE.md` und Hilfebilder nachziehen | S |

Die Web-Schritte des Konzepts „Informationen je Rolle“ (`docs/konzepte/informationen-je-rolle.md`) kommen nach diesen Schritten.

## Bewusst nicht

- Fluent UI React (ADR 0069).
- Pixelvergleich zwischen Web und App.
- Eine eigene Wertestufe fürs Handy.
- Werte aus der JSON in `Colors.axaml` erzeugen; die Tests halten beide Seiten gleich.

## Beim Schreiben festgestellt

- Abschnitt 0 des Briefs nennt `web/src/index.css` als Datei des Webs; die Web-Apps nutzen sie nicht.
- Die Umstellungsliste 11.1 (W1–W20) hat keine Statusspalte fürs Web.
- `docs/design.md` Abschnitt 0 spricht von der „in der App eingebetteten Verwaltung“, die es nach ADR 0084 nicht mehr gibt.
- Der Skill `fluent2-web` empfiehlt `rem`; der Brief verbietet es.

## Offene Fragen an den Auftraggeber

1. Soll der Verlauf am Primärknopf auch im Web gelten, oder wird er in beiden Oberflächen einfarbig?
2. Soll die Bildtafel bei jedem Rundenabschluss Pflicht sein oder nur bei Änderungen am Design?

## Quellen

- Design-Brief: `docs/design.md` (Abschnitte 0, 3, 4.4, 11, 12)
- Werte: `docs/design-tokens.json`, `web/src/tokens.css`, `web/src/index.css`, `web/src/booking/booking.css`, `web/src/myday/myday.css`, `web/src/dashboard/dashboard.css`, `src/AispaStudio.Desktop/Themes/Colors.axaml`, `src/AispaStudio.Desktop/Themes/Styles.axaml`
- Tests: `web/src/designTokens.test.ts`, `web/src/interFont.test.ts`, `tests/AispaStudio.Desktop.Tests/DesignTokenTests.cs`
- Entscheidungen: ADR 0004, ADR 0067, ADR 0069, ADR 0084
