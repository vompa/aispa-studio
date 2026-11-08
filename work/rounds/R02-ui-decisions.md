Status: closed

# Runde R02 – Entscheidungen aus R01, fluent2-web über alle Web-Masken, Hilfe professioneller

## Quelle
- Die 14 Entscheidungen zu den offenen Punkten der Runde R01 und dem Auftrag des Auftraggebers zur Leiste „KI und Hilfe“: `work/rounds/R01-ui-review.md` unter „Offene Entscheidungen“ (als Ruling). Die Befunde selbst stehen in der Befundliste `docs/umsetzung/aenderungsliste-oberflaechen-2026-10-23.md` (W…, B…, W-Muster …).
- Auftraggeber, 25.10.2026 (Chat): „In Runde 2 soll der Skill fluent2-web auf alle Web-Masken drüberlaufen“ und „die Hilfe soll professioneller ausschauen“, dazu der Artikel „Best Practices for Making User Assistance More Engaging“ (ClickHelp, clickhelp.com/clickhelp-technical-writing-blog/best-practices-for-making-user-assistance-more-engaging/).
- Die manuelle Testliste der Runde R01 ist noch nicht durchgegangen; was der Auftraggeber dort meldet, kommt in die nächste Runde.

## Eingang
Befunde aus der Befundliste nur mit Kennung: W-Muster 9, W3, W23, W41, W59, W-Muster 12, W40, W4, W8, W19, W25, W27, W56, W57, W58, W62, W66, B29, W65, B2, B35, B40, B52, B57, B9, B28, B69, B14, B15, B33, B39, B41, B48, B49, B51, B59, B61, B44, B50, B54, B55, B60, B66, B67, B11, B20, B22, B24, B32, B42, B43, B63, B65, B68, B30, B31, B36.

| Kennung | Wo | Beobachtet → Erwartet | Größe | Quelle |
|---|---|---|---|---|
| Ä74 | Windows-App, Leiste „KI und Hilfe“ (`ShellView.axaml`, `EmbeddedBrowser`) | Der runde Knopf mit dem App-Symbol ist laut Auftraggeber nur im Kalender zu sehen, nicht bei Kunden und Posteingang; neben dem eingebetteten Browser vermutlich verdeckt; auf „Anfragen“ verdeckt er Text → in allen Masken sichtbar und erreichbar, ohne Inhalt zu verdecken | M | Auftraggeber, 25.10.2026 (Chat) |
| Ä75 | Alle Web-Masken (`web/src/*/`, 26 Seiten) | Nur einzelne Masken wurden nach fluent2-web geprüft → jede Maske einmal nach den Prüflisten des Skills durchgesehen und behoben, Reste als Befunde | L → 4 Slices nach Bereichen | Auftraggeber, 25.10.2026 (Chat) |
| Befundliste | docs/umsetzung/aenderungsliste-oberflaechen-2026-10-23.md | Umgesetzte Befunde sind nicht gekennzeichnet → „erledigt in Slice NN“ | S | Ruling 14 aus R01 |

## Brief: Hilfe professioneller (Ä76)
Zweck: Die Hilfe (189 Themen unter `web/src/help/content/`, eine Darstellung für Windows-App und Web, ADR 0079) soll beim Lesen wie ein gepflegtes Handbuch wirken und zum Durcharbeiten einladen.
Nutzer: Rezeption, Therapeutinnen, Leitung, Inhaber; die KI-Seitenleiste nutzt dieselben Texte.
Aus dem ClickHelp-Artikel übernommen (passend für ein Handbuch ohne eigene Plattform): Aufgaben in kleine Schritte mit Hinweis je Schritt (AC-01); Bilder und Diagramme statt langer Absätze (AC-02); kontextbezogene Hilfe aus der Oberfläche (gibt es: Fragezeichen, Seitenleiste; bleibt); Inhalte nach Rolle (AC-03); Suche nach Absicht mit Trefferzahl (AC-04); Rückmeldung je Thema (offene Entscheidung); fortlaufende Pflege aus den Fragen an die KI (offene Entscheidung).
Nicht übernommen: Chatbot rund um die Uhr (der KI-Assistent deckt das), Abzeichen, Ranglisten und Quiz (passen nicht zu einer Fachanwendung am Empfang), Nutzerprofile und Lernpfade (Datenschutz, kein Nutzen bei fünf Rollen), Umfragen und Nutzungsstatistik (Datenschutz; nur als lokale Rückmeldung, siehe offene Entscheidung), Wahl einer Support-Plattform.
- AC-01 Given ein Ablauf-Thema When es angezeigt wird Then hat es Kurzsatz, Voraussetzungen, nummerierte Schritte mit je einem Bild, „Was danach“ und „Wenn etwas schiefgeht“
- AC-02 Given ein Bild eines Schritts When es gerendert wird Then ist das Zielelement markiert, das Bild hat Alternativtext und Bildunterschrift
- AC-03 Given die Startseite der Hilfe When sie angezeigt wird Then gibt es je Rolle einen Einstieg mit den ersten Schritten
- AC-04 Given eine Suche When Treffer da sind Then nennt eine Statuszeile die Zahl, und jeder Treffer zeigt Bereich und hervorgehobenen Begriff
- AC-05 Given ein Thema When es angezeigt wird Then stehen Brotkrumen „Bereich › Thema“, „Auf dieser Seite“ und „Verwandte Themen“
- AC-06 Given ein Hinweis (`> Hinweis:`, `> Tipp:`, `> Achtung:`) When gerendert Then ist er ein Kasten mit Symbol und Farbe nach Brief 5.15
Randfälle: Themen ohne `h2` (kein Inhaltsverzeichnis), Themen ohne Links (keine verwandten Themen), Bilder fehlen (Platzhalter mit Hinweis statt Lücke).
Fehler: Suche ohne Treffer nennt Alternativen (Bereiche, KI fragen).
Nicht-Ziele: neue Themen schreiben; die Hilfe aus dem Repository herauslösen.
NOT building: Videos, GIFs, Abzeichen, Profile, zentrale Statistik.
Assumptions: [Assumption] Die zehn meistgenutzten Abläufe sind die von Kalender und Kassa (Termin buchen, verschieben, stornieren, Status setzen, freie Zeit suchen, Paar und Spa-Tag, Kassieren, Beleg stornieren, Tagesabschluss, Gutschein verkaufen); der Rest folgt in der nächsten Runde. [Assumption] Die Markierung im Bild entsteht im Bildtest (Rahmen um das Zielelement), nicht nachträglich im Bildprogramm.

## Slices
| Slice | Name | Befunde | Hängt ab von |
|---|---|---|---|
| 30 | Befundliste: erledigte Befunde gekennzeichnet | Ruling 14 aus R01 | – |
| 31 | KI und Hilfe in allen Masken der Windows-App | Ä74 | – |
| 32 | Maße der Windows-App auf das Raster des Briefs | W-Muster 9, W3, W23, W41, W59 | – |
| 33 | Tooltip-Inhalte und Sperrgrund auch per Tastatur | W-Muster 12, W40 | – |
| 34 | Einzelpunkte Windows-App: Standardknöpfe, Ansage, Konflikt, Stornieren | W4, W19, W25, W56 | – |
| 35 | Einzelpunkte Windows-App: Technikdeutsch, Hilfesuche, Meldungskarte, Menü | W8, W27, W57, W58 | – |
| 36 | Farben, Kontrast und Schriftgrößen im Brief | W62, W66, B29, B30 | – |
| 37 | Namens-Scan je Zustand | W65 | 33, 34, 35 |
| 38 | Ein gemeinsamer Rückfragedialog im Web | B2 | – |
| 39 | Fehlende Rückfragen im Web | B35, B40, B52, B57 | 38 |
| 40 | Gemeinsamer Formularbaustein im Web | B9 | – |
| 41 | Farbtoken an einer Stelle im Web | B28, B69 | – |
| 42 | Seitenpanel und Dienstplan im Web | B48, B49, B51 | – |
| 43 | Fokus und Zustände im Web | B14, B15, B33, B39, B41, B59, B61 | – |
| 44 | Texte und Auswege im Web | B44, B50, B54, B55, B60, B66, B67 | – |
| 45 | Tooltip-Baustein im Web | B68 | – |
| 46 | Kleinigkeiten Web, zweiter Durchgang | B11, B20, B22, B24, B32, B42, B43, B63, B65 | – |
| 47 | Content-Security-Policy für den Kundenbereich | B31 | – |
| 48 | Formulare in Personal, Buchhaltung und Lohn | B36 | 40 |
| 49 | fluent2-web über Kalender, Posteingang, Kassa und Assistent | Auftraggeber, 25.10.2026 (fluent2-web über alle Web-Masken) | 38, 43, 44, 45, 46 |
| 50 | fluent2-web über Verwaltung, Personal und Stammdaten | Auftraggeber, 25.10.2026 (fluent2-web über alle Web-Masken) | 40, 48 |
| 51 | fluent2-web über Dienstplan, Zeit, Buchhaltung, Lohn und Berichte | Auftraggeber, 25.10.2026 (fluent2-web über alle Web-Masken) | 39, 42 |
| 52 | fluent2-web über Kundenbereich, Mein Tag, Betreiber und Lager | Auftraggeber, 25.10.2026 (fluent2-web über alle Web-Masken) | 41, 47 |
| 53 | Hilfe: Seitenaufbau, Navigation und Typografie | Auftraggeber, 25.10.2026 (Hilfe professioneller); Brief der Rundenakte | 32, 36 |
| 54 | Hilfe: Muster für Abläufe und Einstieg je Rolle | Auftraggeber, 25.10.2026 (Hilfe professioneller); Brief der Rundenakte | 53, 49, 50 |
| 55 | Hilfe: Rückmeldung je Thema | Ruling zur Rückmeldung (Brief Ä76) | 54 |
| 56 | Hilfe: unbeantwortete Fragen an die KI für die Leitung | Ruling zur Pflege der Hilfe (Brief Ä76) | – |

## Entscheidungen
Ruling: Slice 41 – Lager (`stock.css`) nutzt die Tokens aus `index.css` statt `tokens.css`, weil es in der Team-Oberfläche läuft; die Werte sind in beiden Modi gleich – Claude (Annahme der Slice-Sitzung, bei Freigabe des Auftraggebers zu bestätigen), 26.10.2026
Ruling: Slice 41 – `tokens.css` (in `web/src/`) trägt die Palette für Kundenbereich, Mein Tag und Betreiber; der Kundenbereich behält seinen helleren Rand als eigenes Token `--border-control` (`#808a84` hell statt `#8a948e`), damit kein Farbwert wechselt – Claude (Annahme der Slice-Sitzung, bei Freigabe des Auftraggebers zu bestätigen), 26.10.2026
Ruling: Slice 41 – Der nie greifende Fallback `var(--border, #dcd8d0)` in `booking.css` entfällt – Claude (Annahme der Slice-Sitzung, bei Freigabe des Auftraggebers zu bestätigen), 26.10.2026
Ruling: Die Runde setzt die 14 Entscheidungen aus R01 um (Rulings in `work/rounds/R01-ui-review.md`, vom Auftraggeber an Claude übertragen, 25.10.2026), Auftrag des Auftraggebers zur Leiste „KI und Hilfe“ (Ä74), die fluent2-web-Durchsicht aller Web-Masken (Ä75) und den Brief zur Hilfe (Ä76) – Auftraggeber, 25.10.2026
Ruling: `StDone` hell auf `#656c68` statt der Empfehlung `#6f7773` (nachgemessen 3,93:1, zu wenig) – Claude, vom Auftraggeber übertragen, 25.10.2026
Ruling: Die fluent2-web-Durchsicht läuft in vier Slices nach Bereichen und nach den Korrektur-Slices derselben Bereiche, damit nichts doppelt angefasst wird; was eine Sitzung sprengt, wird Befund ab B71 – Claude, 25.10.2026

Ruling: Keine Videos in der Hilfe; GIFs für Mikrointeraktionen erst, wenn Bildtests sie erzeugen können, sonst nicht (Pflege bei jeder Oberflächenänderung, Größe der Hilfe in der App) – Claude, vom Auftraggeber übertragen, 25.10.2026
Ruling: Rückmeldung je Hilfethema („War das hilfreich?“ Ja/Nein, optional Text), gespeichert wie die KI-Rückmeldung nur auf diesem PC, ohne zentrale Sammlung; eine zentrale Auswertung bräuchte später einen ADR → Slice 55 – Claude, vom Auftraggeber übertragen, 25.10.2026
Ruling: Unbeantwortete Fragen an die KI lokal sammeln und der Leitung als Liste zeigen, ohne Gästedaten; die Slice prüft zuerst den Datenschutz (was gespeichert wird, wie lange, wer es sieht) und hält das Ergebnis im Protokoll fest → Slice 56 – Claude, vom Auftraggeber übertragen, 25.10.2026
Ruling: Kein Rollenfilter in der Hilfe; der Einstieg je Rolle (Slice 54) reicht, ein Filter kommt erst bei Bedarf – Claude, vom Auftraggeber übertragen, 25.10.2026
Ruling: Checkliste „Erste Schritte“ mit Fortschritt für die Einrichtung (lokal) ja, als Slice der nächsten Runde nach der Startseite je Rolle (Slice 54) – Claude, vom Auftraggeber übertragen, 25.10.2026
Ruling: Slice 30 – Der Zusatz „erledigt in Slice NN“ steht in der Spalte „Einstufung“ der Befundliste (Gedankenstrich nach der Einstufung), keine neue Spalte; Nummern zweistellig, aufsteigend, alle beteiligten Slices auch bei Teilumsetzung (W50 „17, 18“); der Einleitungssatz „noch nicht umgesetzt“ erklärt jetzt den Zusatz – Claude, 25.10.2026
Ruling: Slice 30 – Der Prüftest `web/src/findingsMarked.test.ts` deckt nur die Slice-Tabelle der Runde R01 ab und prüft bei der Konvention nur, dass sie die Befundliste nennt; eine Fassung über alle geschlossenen Runden kommt mit dem Abschluss von R02 – Claude, 25.10.2026
Ruling: Slice 30 – Plan ohne Änderung freigegeben („build and close it“) – Auftraggeber, 25.10.2026
Ruling: Slice 33 – Tooltip-Inhalte werden per Tastatur erreichbar, nicht als sichtbarer Hilfetext: Sterne, Adresszeile und Anrede-Zeile der Kundenkarte sind Tab-Stopps (`ui:ToolTipBehavior.Reachable`), der Tooltip erscheint beim Fokus und ist der Hilfetext für den Screenreader; ein sichtbarer Satz hätte die Kundenkarte umgestaltet – Claude, 25.10.2026 (Plan-Annahme 1, Slice lief ohne Rückfrage)
Ruling: Slice 33 – Die vier Kennzahlen im Kopf des Abschnitts „Kunde“ sind ein Tab-Stopp mit Pfeiltasten ohne Umlauf (`ui:ToolTipBehavior.ReachableGroup`), wie eine Optionsgruppe; vier einzelne Stopps hätten jeden Weg durch das Termin-Panel verlängert; Name je Kennzahl „{Beschriftung} {Wert}“ – Claude, 25.10.2026 (Annahme 2)
Ruling: Slice 33 – Der Sperrgrund eines gesperrten Knopfes steht zusätzlich im Hilfetext (`AutomationProperties.HelpText`), den der Screenreader in der Elementliste und im Lesemodus liest; ein gesperrter Knopf nimmt in Avalonia keinen Fokus, ein sichtbarer Grund unter dem Knopf wäre neue Gestaltung – Claude, 25.10.2026 (Annahme 3)
Ruling: Slice 33 – Die neuen Tab-Stopps tragen denselben Fokusrahmen wie Knöpfe (Selektor um `:is(Control).tipstop` erweitert); Handbuch, ADR und Design-Brief bleiben unverändert (kein neuer Ablauf, keine Einstellung, keine Abweichung) – Claude, 25.10.2026 (Annahmen 6 und 7)
Ruling: Slice 31: Auf Verwaltung und Hilfe lässt der eingebettete Browser unten 68 px für den Knopf „KI und Hilfe“ frei (kein rechter Streifen, kein Umzug des Knopfs); scrollende Inhalte enden 72 px über dem Rand; Strg+Leertaste im Browser geht als Web-Nachricht `companion` an die App; Spalte „Tests red“: die Nachrichtentests kompilierten ohne die neuen Ereignisse nicht, rot beobachtet wurden Knopf und Text (5 Fehler) – Claude, 25.10.2026 (Annahmen des Plans, Auto-Lauf ohne Rückfrage)
Ruling: Slice 35 – W27 gilt für die Hilfe-Suche der Web-Seite im WebView2: die Avalonia-Hilfe mit Suchfeld gibt es seit ADR 0079 nicht mehr, ein `ui:SearchField` ist dort nicht möglich; das Feld ist schon ein Suchfeld (`type="search"`), die Trefferzahl steht jetzt in einer höflichen Statuszeile (`role="status"`), die Lupe im Web-Feld bleibt aus (neue Gestaltung der Web-Hilfe) – Claude, 25.10.2026 (Plan-Annahme 1, Slice lief ohne Rückfrage)
Ruling: Slice 35 – Technikdeutsch (Schalter, Port, Protokolle, Verbindungszeichenfolge, Zertifikat) steht in `ui:TechnicalDetails` („Technische Angaben“, zu beim Öffnen), im Fließtext bleibt ein Kurzsatz; Hilfetexte unverändert – Claude, 25.10.2026 (Annahme 3)
Ruling: Slice 35 – W58: Nur der Gruppentitel ändert sich; der deaktivierte Eintrag „Sonderfälle“ entfällt, der Trenner davor bleibt als Gruppengrenze, eine Überschrift gibt es nicht mehr („oder weglassen“ im Befund; ein Name am `Separator` erreicht den Screenreader nicht, Avalonia 12.1.4 hat dafür keinen eigenen Peer, nur gelesen, nicht gemessen); das Umbenennen des Hauptknopfs „Termin“ in „Neuer Termin“ bleibt zurückgestellt – Claude, 25.10.2026 (Annahme 4)
Ruling: Slice 35 – W57: Die Meldungskarte ist ein `Button` für den Kopfteil (Name „Titel. Text“, Hilfetext „Details und „Was tun?“ zeigen“), Aktion und „Wegklicken“ liegen daneben statt darin; die Hover-Fläche liegt deshalb nur auf dem Kopfteil – Claude, 25.10.2026 (Annahme 5)
Ruling: Slice 35 – Handbuch, ADR und `docs/design.md` bleiben unverändert (kein neuer Ablauf, keine Einstellung, keine bewusste Abweichung) – Claude, 25.10.2026 (Annahme 6)
Ruling: Slice 34 – Plan ohne Änderung freigegeben („build and close it“) – Auftraggeber, 25.10.2026
Ruling: Slice 34 – Die Konfliktmeldung ist eine `ui:InfoBar` (Fehler) mit Titel „Zeitraum ist belegt.“, der Handlungszeile als Text und den Konfliktzeilen als Inhalt (`Detail`); die Aktion „Termin anzeigen“ markiert den ersten Termin der Liste und fehlt, wenn nur Sperrzeiten, Räume oder Geräte im Weg stehen; das Schließen-Kreuz blendet die Meldung aus (`ConflictNotice.IsDismissed`), ein neuer Versuch zeigt sie wieder – Claude, 25.10.2026 (Plan-Annahmen 1 bis 3)
Ruling: Slice 34 – Die Aktion der Konfliktmeldung steht in der schmalen Seitenleiste unter dem Text (`InfoBar.Stacked`, Standard aus): neben dem Text blieb dem Titel eine Spalte von etwa 110 px; bestehende Meldungen bleiben unverändert – Claude, 25.10.2026 (im Bild beim Bauen gefunden, Abweichung vom Plan)
Ruling: Slice 34 – Der Schnellanmelde-Knopf am Empfangsgerät ist nur Standardknopf, solange „Anderes Konto“ nicht aufgeklappt ist; jeder andere Standardknopf bindet `IsDefault` an die Sichtbarkeit seines Formulars – Claude, 25.10.2026 (Annahme 4)
Ruling: Slice 34 – Das Handbuchbild `kalender-konflikt.png` und der Text in „Termin buchen“ (Abschnitt „Wenn die Zeit belegt ist“) sind neu, weil sich die Meldung sichtbar ändert (ADR 0056); kein ADR, kein Eintrag in `docs/design.md` – Claude, 25.10.2026 (Annahme 6, die Suche ergab das Bild)
Ruling: Slice 38 – `ConfirmDialog` ersetzt alle neun eigenen `showModal`-Rückfragen, nicht nur die sieben des Befunds: auch `DiscardDraftsDialog` (Dienstplan) und `ChangePlanDialog` (Betreiber) waren dieselbe Kopie, und `ReturnDepositDialog` (Feld „Grund“) und `CancelAppointmentsDialog` (Auswahl und Nachricht, `wide`) laufen über `children`, `error`, `confirmDisabled` und `confirmLabel={null}`; sonst bliebe `showModal` außerhalb des Bausteins – Claude, 25.10.2026 (Plan-Annahme 1, Slice lief ohne Rückfrage)
Ruling: Slice 38 – Die Rolle ist `alertdialog`; die Tests der Rückfragen suchen `alertdialog` statt `dialog` (Rolle absichtlich geändert, keine Prüfung gelockert). Der Innenabstand ist überall 24 (vorher 20 in drei der vier eigenen Stile, 24 im Betreiberstil) – Claude, 25.10.2026 (Plan-Annahme 3)
Ruling: Slice 38 – Handbuch, ADR und `docs/design.md` bleiben unverändert (kein neuer Ablauf, keine Einstellung; der Brief nennt Innenabstand 24 und Fokus schon in 5.27); Spalte „Tests red“: die neuen Tests scheiterten am fehlenden Baustein, die angepassten Rückfragetests an der Rolle (28 rot vor dem Baustein) – Claude, 25.10.2026 (Plan-Annahme 6)
Ruling: Die Hilfe-Umbau-Slices laufen am Rundenende und nacheinander: 53 nach 32 und 36 (Raster und Farben für den Abschnitt „Hilfe“ in `docs/design.md`), 54 nach 53, 49 und 50 (die Bilder der Abläufe zeigen den Endstand von Kalender, Kassa und Verwaltung), 55 nach 54 (alle drei ändern `web/src/help/`); Hilfetexte zu einer Änderung bleiben in deren Slice (ADR 0055, 0056) – Auftraggeber, 25.10.2026
Ruling (Slice 32): Der Bezahlknopf der Kassa bleibt 46 px hoch und steht in Brief 4.4 als einzige Ausnahme der Mindesthöhe; alle anderen `MinHeight` 40–52 (Anmeldung, Beitritt, Wiederherstellung, Kalender, Kassa) fallen weg, es gilt der Stil mit 34; PIN-Feld und Freigabecode wachsen mit ihrer Schrift über 34 hinaus (4.4 ist ein Mindestmaß) – Auftraggeber, 25.10.2026 (Freigabe des Plans)
Ruling (Slice 32): Anmelde- und Startkarte (zentriert, 460 bzw. 480 breit) bekommen Innenabstand 24 wie Modal und Rückfragedialog (Brief 5.5), nicht 32 oder 28 – Auftraggeber, 25.10.2026 (Freigabe des Plans)
Ruling (Slice 32): Kennzahl-Kacheln der Kundenkarte und die Karten in Posteingang und Kassa-Spalte bekommen 16/12 (Brief 5.5 verschachtelte Karte); die zwei Entwicklungs-Hinweise der Anmeldung bleiben Karten mit Innenabstand 12/10 (Hinweis nach 5.9), kein Umbau zu `ui:InfoBar` – Auftraggeber, 25.10.2026 (Freigabe des Plans)
Ruling (Slice 32): Hinweise im Kalender und InfoBar Radius 8 mit 10/12, Toast 10/16 (rechts 6 vor dem Kreuz); die Besuchszeile der Kundenkarte ist eine Listenzeile (Radius 6 bleibt, Innenabstand 12/10) – Auftraggeber, 25.10.2026 (Freigabe des Plans)
Ruling (Slice 32): Beide Seitenleisten („KI und Hilfe“, Kalender) liegen nach Brief 5.12 Punkt 1 am rechten Fensterrand an: Stil `Border.sidepanel` trägt Fläche surface, Rahmen nur links 1 px line, Radius 0, kein Schatten; Klasse `card` und `Margin="0,16,16,16"` entfallen; Kopf 20/16/12/12 und Inhalt 16 wie im Kalender – Auftraggeber, 25.10.2026 (Freigabe des Plans)
Ruling (Slice 32): Kundenformular 12 / 20 / 8 (Feld, Gruppe, Überschrift) statt 16 / 32 / 12; der Hilfetext bleibt 4 px unter dem Feld (`Margin="0,-8,0,0"`) – Auftraggeber, 25.10.2026 (Freigabe des Plans)
Ruling (Slice 32): Nicht angefasst: Initialenkreise 40/44, Markenzeichen 44, `c|SidePanelHeader MinHeight 40`, `InboxView` Innenabstand 10/6 am KI-Vorschlag, Radius 6 an Listenzeilen und Terminkarten (keine Bedienelemente bzw. vom Brief so vorgesehen) – Auftraggeber, 25.10.2026 (Freigabe des Plans)
Ruling (Slice 32): Keine Hilfetexte und keine neuen Handbuchbilder: Masken und Abläufe ändern sich nicht (ADR 0055, 0056); der Prüftest `GridValueTests` schützt die Werte, Ausnahmen stehen in seiner Liste und im Brief 4.4 – Claude, 25.10.2026

Ruling: Slice 37 – Der Scan der Zustände steht in eigenen Tests (`ViewTests.NameScanStates.cs`), der Scan über die Seiten im Ausgangszustand bleibt unverändert; geprüft werden Termin-Panel (zu und mit aufgeklapptem Kunden), jede der sechs Arten von „Neuer Eintrag“, der Rückfragedialog der Windows-App, die sieben Bearbeitungen der Kundenseite und die Kassa vor und nach dem Bezahlen mit der Maske „Rechnungsempfänger“; der Typensatz ist der des alten Scans plus `TabItem` – Claude, 25.10.2026 (Plan-Annahmen 1 bis 4, Slice lief ohne Rückfrage)
Ruling: Slice 37 – Spalte „Tests red“: der Scan fand in keinem dieser Zustände eine Namenslücke (alle Fälle sofort grün, weil frühere Slices (07 bis 11, 15, 19, 20, 33 bis 35) die Namen schon gesetzt haben); als Ersatz für das Rot prüft eine Gegenprobe, dass der Scan einen Knopf mit Layout statt Text und ohne Namen meldet und einen benannten nicht – Claude, 25.10.2026
Ruling: Slice 37 – Handbuch, ADR und `docs/design.md` bleiben unverändert (nur Tests; kein neuer Ablauf, keine Einstellung, keine Abweichung) – Claude, 25.10.2026 (Annahme 5)

Ruling: Slice 36 – Plan mit den Annahmen A1 bis A6 freigegeben („build and close it“) – Auftraggeber, 25.10.2026
Ruling: Slice 36 – Satz 2.6: 3:1 gilt für Rahmen von Feldern, Kontrollkästchen und Optionsfeldern; Symbole mit Zustand bleiben bei 3:1 (WCAG 1.4.11), Knopf- und Chip-Rahmen sind bewusst ausgenommen (Annahme A1, mit der Freigabe bestätigt) – Claude, 25.10.2026
Ruling: Slice 36 – px statt rem steht als „Gewollte Abweichung von fluent2-web“ im Brief (Abschnitt 0, Einheiten); keine Umstellung (Annahme A4) – Claude, 25.10.2026
Ruling: Slice 36 – `st-checked-in` hell (4,06:1) bleibt; die Statusfarben sind Flächen und Balken, Text darin ist `fg`, das steht im Brief 2.6 (Annahme A2) – Claude, 25.10.2026
Ruling: Slice 39 – Lauf ohne Planfreigabe (autonome Abarbeitung, Auftraggeber, 26.10.2026); die Annahmen des Plans `work/plans/2026-10-26-slice-39.md` gelten: Texte der vier Rückfragen wie dort, Fehler der API bleiben im Dialog, „Tagesabschluss“ ist primär (endgültig, aber kein Datenverlust), die drei übrigen sind danger – Claude, 26.10.2026
Ruling: Slice 39 – Der Betrag in der Rückfrage steht in der Hausschreibweise von `formatMoney` („Tagesabschluss mit gezählt € 245,50?“) wie jede Betragsanzeige der Web-Kassa, nicht „245,50 €“ wie im Abnahmetext; der Test prüft den Text als Literal – Claude, 26.10.2026
Ruling: Slice 39 – „Grund (wird protokolliert)“ ist beim Nachtragen, Ändern und Entfernen Pflicht (Stern, `required`); ein leerer Grund zeigt „Grund angeben.“ am Feld statt den Knopf zu sperren. Ende vor Beginn zeigt „Endet am Folgetag“ und speichert wie bisher über Mitternacht – Claude, 26.10.2026
Ruling: Slice 39 – Die Bestätigen-Knöpfe aller vier Rückfragen sind gesperrt, solange der Aufruf läuft (Befund der Durchsicht: ein Doppelklick hätte zwei Z-Berichte angelegt) – Claude, 26.10.2026
Ruling: Slice 40 – Plan mit den Annahmen A1 bis A5 freigegeben (autonome Abarbeitung): Bausteine `FormField` und `FormGroup` in `web/src/components/FormField.tsx`, Hook `useFocusFirstInvalid(ref)`, gemeinsame Klassen `form-field`/`form-message`/`form-group` in `components/formField.css`; `Field` der Lager-Seite (`stock/stockUi.tsx`) bleibt, weil Aufbau und Klassen (`w-*`, `role="alert"`) abweichen; keine neue Gestaltung – Claude, 26.10.2026
Ruling: Slice 47 – Plan mit den Annahmen 1 bis 7 autonom freigegeben (Freigabe durch den Auftraggeber in der Runde vom 26.10.2026) – Claude, 26.10.2026
Ruling: Slice 47 – Richtlinie `default-src 'self'; base-uri 'self'; object-src 'none'; form-action 'self'`; `img-src data:` entfällt, weil `buchen-*.css` keine `data:`-URL enthält (am Build gemessen) (Annahme 1) – Claude, 26.10.2026
Ruling: Slice 47 – „Stripe-Quellen gezielt“ heißt: keine freigeben, solange die Bezahlung eine Weiterleitung zu Stripe Checkout ist; ADR 0080 nennt die Quellen für Stripe.js/Elements (Annahme 2) – Claude, 26.10.2026
Ruling: Slice 47 – Richtlinie einmal als Konstante `BookingContentSecurityPolicy` in der API, wortgleich in `docker/nginx.conf`, ein Test hält beide gleich; kein Meldeendpunkt (`report-to`), keine CSP im Vite-Dev-Server, kein Konfigurationsschalter fürs Durchsetzen (Annahmen 3, 4, 5, 7) – Claude, 26.10.2026
Ruling: Slice 47 – `frame-ancestors` bleibt ungesetzt, bis der Auftraggeber entscheidet, ob Spas den Kundenbereich einbetten dürfen; steht in ADR 0080 und auf der Stand-Seite (Annahme 6) – Claude, 26.10.2026
Ruling: Slice 47 – Grundsatz als ADR 0080 (neue Sicherheitsregel für eine öffentliche Seite, mit Weg zum Durchsetzen); keine Hilfeänderung, weil sich weder Einstellung noch Ablauf ändert – Claude, 26.10.2026
Ruling: Slice 56 – Der Server bereinigt die Frage ohne Treffer und gibt sie als `AssistantAnswer.Unanswered` mit (nur bei „Dazu habe ich nichts gefunden“ und „im Handbuch nichts gefunden“), gespeichert wird nur in der App bzw. im Browser; kein Endpunkt, keine Tabelle, keine zentrale Sammlung. Der Server kennt alle Kundennamen des Spas, die Oberflächen nicht – Claude, 26.10.2026 (Plan-Annahme 1, autonome Freigabe)
Ruling: Slice 56 – Kundennamen (Vor-, Nach-, Rufname, wortweise ohne Groß und Klein), E-Mail-Adressen und Telefonnummern (ab sieben Ziffern) werden zu „[Name]“, „[E-Mail]“, „[Telefon]“ ersetzt, die Frage wird nicht verworfen – Claude, 26.10.2026 (Plan-Annahme 2)
Ruling: Slice 56 – Gespeichert werden Frage, Seite, Zeitpunkt und Anzahl gleicher Fragen; 90 Tage nach dem letzten Fragen fällt ein Eintrag weg, „Erledigt“ löscht ihn – Claude, 26.10.2026 (Plan-Annahmen 3 und 4)
Ruling: Slice 56 – Die Liste sehen die Rollen Inhaber, Manager und Standortleitung (`Owner`, `GeneralManager`, `Manager`), nicht Rezeption, Therapie und Backoffice; es entscheidet die Rolle, ein neues Recht gibt es nicht – Claude, 26.10.2026 (Plan-Annahme 5)
Ruling: Slice 56 – Windows-App: aufklappbare Leiste „Offene Fragen an die Hilfe (n)“ nativ über der Hilfe-Seite (Datei `unanswered-questions.jsonl` neben den KI-Rückmeldungen); Web: Abschnitt in der Übersicht der Hilfe-Leiste, Speicher des Browsers; nicht in der eigenständigen Hilfe-Seite `/help/` – Claude, 26.10.2026 (Plan-Annahme 6)
Ruling: Slice 56 – Hilfe-Abschnitt `ki-assistent-fragen#offene-fragen` mit Fragezeichen an beiden Listen; kein neues Handbuchbild – Claude, 26.10.2026 (Plan-Annahme 7)
Ruling: Slice 56 – Die manuelle Prüfung fragt „Wie färbe ich die Wolken lila?“ statt „Wie drucke ich den Dienstplan?“, weil die Dienstplan-Frage einen Handbuchtreffer hat (Plan-Annahme 8) – Claude, 26.10.2026
Ruling: Slice 42 – Plan mit den Annahmen A1 bis A4 freigegeben (autonome Abarbeitung): gemeinsame Komponente `SidePanel` (`web/src/components/SidePanel.tsx`) mit Fokus auf die Überschrift (`tabIndex=-1`), Esc und Rückgabe des Fokus an den Auslöser; Esc in einer Rückfrage (`<dialog>`) im Panel schließt das Panel nicht – Claude, 26.10.2026
Ruling: Slice 42 – Der Schließen-Knopf heißt „<Panelname> schließen“ (z. B. „Tag planen schließen“), sichtbar bleibt „×“; die Namen wiederholter Steuerelemente tragen den Bezug: „Dienst 2 entfernen“, „Dienst 2 Beginn“ (Dienstplan), „Abschnitt 09:00–13:00 ändern“ und „Verlauf von Abschnitt 09:00–13:00“ (Zeiterfassung); der sichtbare Text bleibt im Namen enthalten (WCAG 2.5.3) – Claude, 26.10.2026
Ruling: Slice 42 – Der Scrollbereich des Dienstplans ist `role="region"` mit Namen „Dienstplan“ und `tabIndex=0`; ein Hilfetext ändert sich nicht, die Abläufe bleiben gleich – Claude, 26.10.2026
Ruling: Slice 48 – Plan mit den Annahmen 1 bis 6 freigegeben (autonome Abarbeitung): Baustein `SubmitButton` (Knopf `type="submit"`, mit Grund `aria-disabled` und `aria-describedby` auf einen sichtbaren Hilfetext) und `guardedSubmit`/`missingText` in `web/src/components/` – Claude, 26.10.2026
Ruling: Slice 48 – Der gesperrte Knopf ist `aria-disabled` statt `disabled`: er behält den Fokus, der Hilfetext ist erreichbar, Klick und Enter senden nichts (`guardedSubmit`) – Claude, 26.10.2026
Ruling: Slice 48 – „Passwort zurücksetzen“ ist ein eigenes Formular neben dem Speichern-Formular der Person, weil sich Formulare nicht verschachteln lassen; Knöpfe im Formular, die nichts absenden („Um 30 Tage verlängern“, „Auf Standard zurücksetzen“), tragen `type="button"` – Claude, 26.10.2026
Ruling: Slice 48 – Keine Hilfeänderung und kein ADR: Ablauf und Aussehen bleiben, nur Enter und der Hinweis am Knopf kommen dazu – Claude, 26.10.2026
Ruling: Slice 50 – Plan mit den Annahmen 1 bis 5 freigegeben (autonome Abarbeitung): jede Maskengruppe bekommt eine axe-Prüfung, wo sie fehlte; kleine Funde werden behoben, Umbauten über mehrere Dateien werden Befund (B71 bis B76) – Claude, 26.10.2026
Ruling: Slice 50 – Behoben mit Test: leere Tabellenköpfe „Aktionen“ (Kosten, Standorte, Team, Absender-Regeln; `aria-label` an `<th>` ersetzt durch verborgenen Text) und die Fehlermeldung beim Annehmen einer Einladung (`role="alert"` statt `status`) – Claude, 26.10.2026
Ruling: Slice 50 – Keine Hilfeänderung und kein ADR: Ablauf und Aussehen bleiben – Claude, 26.10.2026
Ruling: Slice 51 – Plan mit den Annahmen 1 bis 5 freigegeben (autonome Abarbeitung): axe-Prüfung je Maske der Gruppe, kleine Funde behoben, Umbauten über mehrere Dateien werden Befund (B77 bis B79) – Claude, 26.10.2026
Ruling: Slice 51 – Behoben mit Test: Hinweise der Zeiterfassung standen nur als `title` und die Zelle hatte einen `aria-label` ohne Stunden und Hinweise (jetzt verborgener Vorspann wie im Dienstplan, alle Hinweise sichtbar, Wochenhinweise sichtbar statt „⚠“ mit `title`); 34 Knöpfe in den fünf Ordnern ohne `type` (Scan-Test `buttonTypes.test.ts`) – Claude, 26.10.2026
Ruling: Slice 51 – Keine Hilfeänderung und kein ADR: der Ablauf bleibt; in der Zeiterfassung stehen jetzt alle Hinweise eines Tages und der Wochenhinweis im Wortlaut statt nur des ersten bzw. „⚠“ (der Handbuchtext bleibt zutreffend, das ⚠ steht weiter vor jedem Hinweis; das Bild `time-woche.png` wurde nicht neu erzeugt) – Claude, 26.10.2026
Ruling: Slice 52 – Plan mit den Annahmen 1 bis 5 freigegeben (autonome Abarbeitung): Durchsicht der vier Gruppen per Skript und vorhandener axe-Läufe, Kleines beheben, Rest als Befund (B80 bis B83, weil Slice 51 B77 bis B79 nutzt) – Claude, 26.10.2026
Ruling: Slice 52 – Behoben mit Test: Der Fehler beim Sperren eines Spas hängt per `aria-invalid` und `aria-describedby` am Feld „Grund“ – Claude, 26.10.2026
Ruling: Slice 52 – Keine Hilfeänderung und kein ADR: Ablauf und Aussehen bleiben; der neue Test war vor der Änderung rot – Claude, 26.10.2026
Ruling: Slice 49 – Plan mit den Annahmen 1 bis 5 freigegeben (autonome Abarbeitung): Durchsicht von Kalender, Posteingang, Kassa, Live-Anzeige, Assistent und Begleiter per Skript und neuen axe-Läufen, Kleines beheben, Rest als Befund ab B84 (B71 bis B83 sind vergeben) – Claude, 26.10.2026
Ruling: Slice 49 – Behoben mit Test: „KI und Hilfe“ und „Ausblenden“ tragen Tooltip-Baustein und `aria-keyshortcuts` statt `title`; ein Termin einer gemeinsamen Buchung nennt „gemeinsam gebucht“ im Namen des Knopfs; die Live-Anzeige des Kalenders ist eine Statusregion – Claude, 26.10.2026
Ruling: Slice 49 – Keine Hilfeänderung und kein ADR: Abläufe und Aussehen bleiben; die neuen Tests waren vor der Änderung rot – Claude, 26.10.2026

Ruling: Slice 43 – Plan mit den Annahmen 1 bis 9 autonom freigegeben (Freigabe durch den Auftraggeber in der Runde vom 26.10.2026): Fokus auf die vorhandene `h1` (`PageHeading`), `aria-current` an allen fünf `admin-list`-Listen, „Abrechnen: Behandlung Gast“, gesperrtes „Stornieren“ als `aria-disabled` mit sichtbarem verbundenem Grund, Fokusführung im Betreiberbereich, Sprunglinks in der Fehlerübersicht der Buchung (Anamnese, Gutschein, Warteliste zurückgestellt) – Claude, 26.10.2026
Ruling: Slice 43 – Keine Hilfeänderung und kein ADR (Verhalten für Tastatur und Screenreader, kein neuer Ablauf, keine Einstellung, keine Abweichung vom Brief); Spalte „Tests red“: elf der neuen Tests waren vor der Umsetzung rot – Claude, 26.10.2026
Ruling: Slice 44 – Plan mit den Annahmen 1 bis 8 autonom freigegeben (`auto`): Leerzustand der Kassa mit Link-Knopf zu „Stammdaten, Zahlungen“ über `onOpenSettings` (App öffnet die Stammdaten mit Reiter `payments`), „OK“ → „Schließen“, alle Knöpfe mit „+ “ bekommen ein Verb („anlegen“ für Neues mit eigener Seite, „hinzufügen“ für Zeilen und Positionen), Exporte als Knöpfe mit „Wird erstellt …“ und Fehlertext statt Link, Lohn-Export prüft die Monatswerte vorher, Monat als zwei Auswahlfelder, „Erneut versuchen“ im Kundenbereich, ein gemeinsamer Fehlertext mit Ursache und Weg – Claude, 26.10.2026
Ruling: Slice 44 – Die Exporte der Buchhaltung und der Lohnverrechnung sind Knöpfe, keine Links mehr (`ExportButton`): Sie holen die Datei per `fetch` und legen sie über einen Blob-Link ab. Der Server bleibt unverändert; ein Fehlerstatus gibt den Servertext oder den allgemeinen Text mit Weg aus, es entsteht keine Datei – Claude, 26.10.2026
Ruling: Slice 44 – Der Lohn-Export wird nur gesperrt, wenn keine Zeile der Monatswerte Personalnummer und Lohnart hat oder der Monat keine Zeile hat (der Server würde eine leere Datei liefern); die Meldung nennt die Person und den Ort der Eingabe. Einzelne fehlende Zeilen und andere Hinweise sperren nicht, weil leere Lohnarten erlaubt sind (Durchsicht, Befund 1) – Claude, 26.10.2026
Ruling: Slice 44 – Hilfe angepasst (Lohn-Export, Buchhaltung-Export, Monatsauswahl, Kassa-Leerzustand, umbenannte Knöpfe in 30 Themen), kein ADR (Texte und Auswege, keine Grundsatzentscheidung); Spalte „Tests red“: der Prüftest für „+ “ und „OK“ und die Tests für Kassa, Lohn-Export, Fehlerexport und Wiederholen waren vor der Umsetzung rot – Claude, 26.10.2026
Ruling: Slice 45 – Plan mit den Annahmen 1 bis 5 autonom freigegeben (Freigabe durch den Auftraggeber in der Runde vom 26.10.2026): kein neues Paket, Popover API mit eigener Lagebestimmung; nicht fokussierbare Ziele bekommen mit `focusable` einen Tabstopp; der Prüftest erlaubt nur eine Liste reiner Namen als natives `title`; ein gesperrter Knopf (Lagerorte) trägt den Tooltip an einem umgebenden `span` – Claude, 26.10.2026
Ruling: Slice 45 – Der Text steht dauerhaft als `aria-description` am Ziel, das Popover wird nur im offenen Zustand ins DOM gesetzt (kein verstecktes Duplikat des Textes, `getByText` in Tests bleibt eindeutig); der Wechsel von einem offenen Tooltip zum nächsten geschieht ohne Verzögerung, ein verlassenes Ziel schließt nach 250 ms, Esc und Strg sofort – Claude, 26.10.2026
Ruling: Slice 45 – Keine Hilfeänderung und kein ADR (Verhalten von Hinweisen, kein neuer Ablauf, keine Einstellung); Spalte „Tests red“: nur der Prüftest der Quelltexte war vor der Umsetzung rot, die Komponente entstand vor ihren vier Tests (diese waren beim ersten Lauf nicht unabhängig rot) – Claude, 26.10.2026
Ruling: Slice 46 – B11: Der Hex-Wert der Kalenderfarbe steht als Text hinter dem Farbfeld im Label (`.color-value`), keine benannten Farben; der Name des Felds lautet dadurch „Farbe im Kalender #rrggbb“ – Claude (Annahme der Slice-Sitzung, bei Freigabe des Auftraggebers zu bestätigen), 26.10.2026
Ruling: Slice 46 – B20: `.help-button` ist 24 × 24 px (kein `::after`); der Prüftest liest die Maße aus dem CSS-Text, weil jsdom nichts misst – Claude (Annahme der Slice-Sitzung, bei Freigabe des Auftraggebers zu bestätigen), 26.10.2026
Ruling: Slice 46 – B22: Die Bildmaße der Hilfe kommen aus den PNG-Köpfen zur Bauzeit über ein Vite-Plugin (virtuelles Modul `virtual:help-image-sizes` in `vite.config.ts`), keine eingecheckte Maßdatei, die nach `help-images-shrink.py` veralten könnte; `max-width: 100%; height: auto` bleibt – Claude (Annahme der Slice-Sitzung, bei Freigabe des Auftraggebers zu bestätigen), 26.10.2026
Ruling: Slice 46 – B24: `AiPill` verliert `aria-label`, behält `title` und den Text „KI“ und trägt `<span class="visually-hidden"> erstellt von der KI</span>`; der Assistenten-Test prüft jetzt Text statt `aria-label` – Claude (Annahme der Slice-Sitzung, bei Freigabe des Auftraggebers zu bestätigen), 26.10.2026
Ruling: Slice 46 – B32: Im Kundenbereich unterscheidet nur `.summary` allein per Hintergrund; sie bekommt `border: 1px solid transparent` (Innenabstand um 1 px kleiner, gleiche Größe), alle anderen Karten, Reiter und Chips haben schon einen Rand – Claude (Annahme der Slice-Sitzung, bei Freigabe des Auftraggebers zu bestätigen), 26.10.2026
Ruling: Slice 46 – B42: „Rest bar“ und „Rest Karte“ schreiben `formatAmount(…)` ins Feld („12,50“, „49,00“); der bestehende Test erwartete „49“ im Feld und erwartet jetzt „49,00“ (Folge des Befunds, kein Abschwächen) – Claude (Annahme der Slice-Sitzung, bei Freigabe des Auftraggebers zu bestätigen), 26.10.2026
Ruling: Slice 46 – B43: Das vorhandene `<p>` mit „Bar zu zahlen“ und „Rückgeld“ bekommt `role="status"` und bleibt immer im DOM – Claude (Annahme der Slice-Sitzung, bei Freigabe des Auftraggebers zu bestätigen), 26.10.2026
Ruling: Slice 46 – B63: Nur `index.css` (`.receipt-lines .total td`) wechselt von 700 auf 600; die `700` in `booking.css` und `myday.css` stehen nicht im Befund und bleiben (siehe Überraschungen) – Claude (Annahme der Slice-Sitzung, bei Freigabe des Auftraggebers zu bestätigen), 26.10.2026
Ruling: Slice 46 – B65: Ein gemeinsamer Baustein `components/Toast.tsx` ersetzt die sechs gleichen Meldungsblöcke (Business-Info, Schließzeiten, Raumsperren, Aktionen, Lager, Abos), hält die Meldung nach dem Entfernen 120 ms mit Klasse `leaving` (`opacity 0`, `transition 0,12 s`) und entfernt sie dann; Schließen-Knopf wie bisher bei Fehlern, im Lager immer – Claude (Annahme der Slice-Sitzung, bei Freigabe des Auftraggebers zu bestätigen), 26.10.2026
Ruling: Slice 53 – Plan mit den Annahmen 1 bis 7 autonom freigegeben (Freigabe durch den Auftraggeber in der Runde vom 26.10.2026, Lauf „auto“): Brotkrumen „Hilfe › Bereich › Thema“, Rollenkennzeichen nur als Zeile („Für: …“, kein Filter), Inhaltsverzeichnis ab einer `h2`, Hinweis neutral, Tipp grün, Achtung gelb (Warnung, nie rot), Platzhalter „Bild folgt: …“, Treffer mit Bereich und `<mark>`, Satz der Statuszeile unverändert, Alternativen ohne Treffer als Zeile „Bereiche: …“ plus „Alle Themen anzeigen“; kein ADR, Abschnitt 5.30 in `docs/design.md` – Claude, 26.10.2026
Ruling: Slice 53 – Die Befundliste kennt Ä76 nicht (nur `W…`/`B…`); der Vermerk „erledigt in Slice 53“ entfällt, die Kennung steht in der Rundenakte und im PR-Protokoll. Alle Spalten außer „Commit“ treffen zu: „Tests red“ waren die zwölf neuen Tests (zwei Dateien) vor der Umsetzung, „Commit“ trägt die Warteschlange – Claude, 26.10.2026
Ruling: Slice 54 – Plan mit den Annahmen 1 bis 6 autonom freigegeben (Freigabe durch den Auftraggeber in der Runde vom 26.10.2026, Lauf „auto“): ein Schritt ist eine `###`-Überschrift „N. Titel“ mit genau einem Bild (keine nummerierte Liste, weil ein Bild sie beendet und die Zählung neu beginnt; beide Leser bleiben unverändert), Kopfangabe `aufbau: schritte`, feste Abschnitte „Voraussetzungen“, „Schritte“, „Was danach“, „Wenn etwas schiefgeht“, die zehn Abläufe der Rundenakte, Markierung als Rahmen mit Schrittnummer in der Überlagerung des Fensters (nur im Bildtest), Einstieg je Rolle als feste Zuordnung in `roleEntries.ts` (kein Filter), kein ADR – Claude, 26.10.2026
Ruling: Slice 54 – Der Einstieg „Erste Schritte“ nennt je Rolle vier bis fünf Abläufe: Rezeption (Termin buchen, Freie Zeit suchen, Status setzen, Termin kassieren, Tagesabschluss), Therapeut:in (Status setzen, Tagesplan ansehen, Termin finden, Tagesstart abhaken), Leitung (verschieben, stornieren, Beleg stornieren, Tagesabschluss), Inhaber:in (Tagesabschluss, Gutschein verkaufen, Beleg stornieren, Paar oder Spa-Tag); `manager` (Geschäftsführung) liest den Einstieg der Leitung – Claude (Annahme der Slice-Sitzung, bei Freigabe des Auftraggebers zu bestätigen), 26.10.2026
Ruling: Slice 54 – Die Befundliste kennt Ä76 nicht; der Vermerk „erledigt in Slice 54“ entfällt wie bei Slice 53. „Tests red“: der Prüftest „zehn umgestellte Abläufe“ und vier Tests der Startseite waren vor der Umsetzung rot; der Bildtest „markiert das Zielelement“ entstand zusammen mit dem Markierer und war nie rot (er prüft Rahmen, Lage im Fenster, Aufräumen und den Fehler bei fehlendem Ziel). „Commit“ trägt die Warteschlange – Claude, 26.10.2026
Ruling: Slice 55 – Plan mit den Annahmen 1 bis 4 autonom freigegeben (Lauf `auto`): Ja und Nein öffnen dasselbe freiwillige Textfeld (höchstens 500 Zeichen), gespeichert werden nur Themenkennung, Antwort, Text und Zeit in `localStorage` (`aispa.help.feedback`, ein Eintrag je Thema, die letzte gilt), nach dem Senden steht der Dank, bei vorhandener Rückmeldung fehlt die Frage; keine Auswertung, kein ADR – Claude, 26.10.2026
Ruling: Slice 55 – Die Befundliste kennt Ä76 nicht; der Vermerk „erledigt in Slice 55“ entfällt wie bei Slice 53 und 54. Alle Spalten außer „Commit“ treffen zu: „Tests red“ waren die fünf neuen Tests (`helpFeedback.test.tsx`), die vor der Umsetzung am fehlenden Modul scheiterten. „Commit“ trägt die Warteschlange – Claude, 26.10.2026

## Überraschungen
- Slice 30: Der Einleitungssatz der Befundliste stand in Zeile 7, nicht in Zeile 11 wie im Plan; ohne Folgen. Das Web-Projekt hat keinen Formatierer (nur oxlint über `npm run lint`), die Testdatei folgt dem Stil von `designTokens.test.ts` ohne Formatprüfung.
- Slice 33: W40 „Adresse entfernen“ war schon in Slice 20 gelöst („Adresse {Adresse} entfernen“); der Abnahmetest mit zwei Adressen war sofort grün. Ein `TextBlock` nennt dem Screenreader immer seinen Text als Namen und übergeht `AutomationProperties.Name` (die Sterne hießen „★★“); die Sterne stehen deshalb in einem Rahmen mit Namen „Sterne“, Rolle Text. Aus kompilierter XAML hängt ein Kind schon in der Gruppe, bevor es seine Eigenschaften bekommt; die Gruppe ordnet Tab-Stopps deshalb auch bei einer späteren `Reachable`-Änderung neu (sonst blieb die vierte Kennzahl ein eigener Stopp).
- Slice 31: Der Satz des Auftraggebers „Knopf nur im Kalender, nicht bei Kunden und Posteingang“ ließ sich mit dem Datenstand des Rechners (AppHost, 1440 × 900) nicht nachstellen: Kunden, Anfragen, Kassa und Geräte zeigen ihn; verdeckt war er nur auf Verwaltung und Hilfe (WebView2 ist ein eigenes Fenster über allem, was Avalonia zeichnet). Vermutlich meinte der Auftraggeber die Seiten mit Browser oder hatte die Leiste dort offen.
- Slice 31: Strg+Leertaste wirkte auf Verwaltung und Hilfe auch ohne Browser nicht: nach dem Seitenwechsel hatte nichts den Fokus, die Taste erreichte nur das Fenster, nie den Tunnel-Handler der Shell. Der Handler hängt jetzt am Fenster und lässt Tasten in Ruhe, wenn der Fokus außerhalb der Shell liegt (Rückfragedialog).
- Slice 31: Tasten ohne Scan-Code (`keybd_event` im Test, Bildschirmtastatur, Fernzugriff) kommen in Chromium mit leerem `e.code` an; `appShortcuts.ts` nimmt deshalb auch `e.key === ' '`.
- Slice 35: W27 zielte auf eine Avalonia-Ansicht (`HelpView.axaml` mit Suchfeld und Trefferliste), die seit ADR 0079 nur noch den eingebetteten Browser hält; der Befund stammt aus einem älteren Lesestand der Datei. Der Abnahmefall wurde auf die Hilfe-Seite (`HelpIndex.tsx`) übertragen. Ein `Button` mit nicht-String-Inhalt bekommt von Avalonia keinen Namen aus seinen Textkindern (`Announced` in den Tests fällt darauf zurück), die Karte braucht deshalb einen ausdrücklichen `AutomationProperties.Name`. Das Edit-Werkzeug entfernte in einem Testfile ein Leerzeichen hinter `=` (`dotnet format` fand es).
- Slice 34: Die Konfliktmeldung als InfoBar mit Aktion war in der Seitenleiste (rund 340 px) unlesbar, solange die Aktion rechts neben dem Text stand; das zeigte erst das Bild (`InfoBar.Stacked`). Der Name der InfoBar für den Screenreader ist jetzt Titel und Handlungszeile statt Titel und Konfliktzeilen (`InfoBar` setzt ihn selbst); die Zeilen bleiben als Inhalt mit eigenen Namen erreichbar. Der Test „Ohne Termin in der Liste gibt es keine Aktion“ war mit dem Platzhalter schon grün, weil er die Abwesenheit prüft.
- Slice 38: Der Befund zählt fünf kopierte Rückfragen plus Anzahlung und Absage; im Quelltext waren es neun (dazu Dienstplan „Entwürfe verwerfen?“ und Betreiber „Abo-Stufe ändern?“). Die Absage-Maske und die Anzahlung hatten kein eigenes Muster für den Fokus: Absage mit `autoFocus`, Anzahlung mit nachgeholtem `focus()`.
- Slice 38: `vitest.setup.ts` kannte `close()` und Esc nicht; der Ersatz für `showModal()` hat sie jetzt (Esc löst `cancel`, dann `close` aus). Unter Last (parallele Sitzungen) liefen im Gesamtlauf `MasterDataPage`, `booking`, `TimesheetPage` und vier Rückfrage-Dateien in 5-s-Zeitgrenzen; einzeln und mit 30 s grün, `TimesheetPage` auch einzeln auf `dev` grün.
- Slice 30: Der Einleitungssatz der Befundliste stand in Zeile 7, nicht in Zeile 11 wie im Plan; ohne Folgen. Das Web-Projekt hat keinen Formatierer (nur oxlint über `npm run lint`), die Testdatei folgt dem Stil von `designTokens.test.ts` ohne Formatprüfung.
- Slice 33: W40 „Adresse entfernen“ war schon in Slice 20 gelöst („Adresse {Adresse} entfernen“); der Abnahmetest mit zwei Adressen war sofort grün. Ein `TextBlock` nennt dem Screenreader immer seinen Text als Namen und übergeht `AutomationProperties.Name` (die Sterne hießen „★★“); die Sterne stehen deshalb in einem Rahmen mit Namen „Sterne“, Rolle Text. Aus kompilierter XAML hängt ein Kind schon in der Gruppe, bevor es seine Eigenschaften bekommt; die Gruppe ordnet Tab-Stopps deshalb auch bei einer späteren `Reachable`-Änderung neu (sonst blieb die vierte Kennzahl ein eigener Stopp).
- Slice 32: Zwei der `Padding="14"`-Stellen aus W-Muster 9 (Anmeldung) sind Hinweise, keine Karten; die Besuchszeile der Kundenkarte (im Befund „Radius 6 an Hinweisen“) ist eine Listenzeile, für die der Brief Radius 6 vorsieht. Der Kommentar im Kundenformular verwies für 16 / 32 / 12 auf masken.md Abschnitt 7, der keine Abstände nennt (§4 sagt 12 / 20 / 8).
- Slice 32: `Border.sidepanel` setzte bisher nur Deckkraft und Bewegung; Fläche, Rahmen und Schatten kamen allein aus der Klasse `card`, deshalb konnte kein Test die Leiste als Leiste prüfen. Mit dem Rahmen nur links rechnet die Reiterschiene mit 1 statt 2 px Rahmen (`RailWidth`, Brief 5.12 Punkt 7: 291 statt 290 px bei 360; Test `Reiter_unten_zeigen_beide_Seiten_und_der_Klick_wechselt` angepasst), ihre unteren Ecken sind nicht mehr gerundet, und die Regel „Karten in Karten liegen flach“ brauchte den Zusatz `Border.sidepanel Border.card`, sonst hätten die Karten in „KI und Hilfe“ Schatten bekommen (alles aus der Durchsicht des Prüfers).
- Slice 32: Der Prüfer fand weitere Kartenränder neben dem Raster, die kein Befund nannte (`Padding="20,14"` an acht Karten der Kundenkarte und der Belegvorschau, `Padding="8,14"` an drei Listenkarten); sie sind mit erledigt (Stil 20/16 bzw. 8/16), und der Prüftest findet jetzt auch mehrwertige Ränder mit 14/18/22/28.
- Slice 37: Der Scan fand in den fünf Zuständen keine Namenslücke; die Namen früherer Slices (07 bis 11, 15, 19, 20, 33 bis 35) reichen. Der Scan liest allein den Namen des Peers (nicht den Rückfall von `Announced` auf Kindtexte) und prüft sichtbare, fokussierbare Elemente, auch gesperrte. Der Typensatz kennt `AppointmentCard` und `AutoCompleteBox` als Ganzes nicht (nur ihre inneren Knöpfe und Felder); die Bearbeiten-Karten des Termin-Panels („Zeiten“, „Notiz“, „Verschieben“) sind nicht einzeln gescannt. Nicht gescannt: Zustände, die ein Gerät brauchen (Kartenterminal, Bondrucker), und die Web-Oberfläche.
- Slice 36: Die Brief-Lesart (Abschnitt 0) verlangt für Werteänderungen eine ADR-Änderung des Briefs; der neue Wert `StDone` hell folgt dem Ruling aus R02 (25.10.2026) und hat deshalb keinen ADR bekommen (Annahme A6). Ob ein Nachtrag nötig ist, entscheidet der Auftraggeber.
- Slice 36: Der Desktop-Test wurde nicht zuerst rot beobachtet, weil die Testdatei mit CA1861 (konstantes Array in `Luminance`) nicht baute; nach dem Umbau in ein statisches Feld lief er grün (96 Fälle in `DesignTokenTests`). Der Web-Test war vorher rot mit 2,75:1.
- Slice 39: Die Durchsicht fand den fehlenden Doppelklick-Schutz an allen vier neuen Rückfragen; der Baustein `ConfirmDialog` sperrt nicht selbst, jeder Aufrufer braucht `confirmDisabled={busy}`. `docs/stand-und-offene-punkte.md` hat nach den parallelen Merges vier Zeilen für die Runde R02, eine davon doppelt für Slice 30 (nicht bereinigt). Im Vitest-Gesamtlauf auf dem Endstand war `roster/RosterPage.test.tsx` („zeigt Wochenarbeitszeit …“, Knopf „Lena Huber, Mo“ nicht gefunden) unter Last rot; die Slice ändert dort nichts, einzeln zweimal unverändert 10 von 10 grün.
- Slice 40: Der Hook fokussiert nach dem Rendern über einen Effekt statt über `setTimeout(…, 0)`; der Fehlertext trägt wie bisher keine eigene Rolle (die Meldung über den Knöpfen ist die Live-Region). Die Tests der Seiten und ein neuer Test mit zwei ungültigen Feldern waren schon mit der Umsetzung grün; rot beobachtet wurde nur die Suche nach den Kopien am Stand von `dev` (sie fand BusinessInfoPage und PromotionsPage).
- Slice 40: Im Gesamtlauf von Vitest (`--maxWorkers=3`, parallel liefen andere Slice-Sitzungen) liefen vier Tests der Seiten (BusinessInfo, Closures, RoomClosures, Promotions) in das 5-s-Limit; dieselben Dateien einzeln wiederholt waren alle grün (43 Tests), also Last, keine Regression. Die Suche des Prüftests nach `Row`/`Group` darf nur die Formularseiten durchsuchen: `pos/DayPanel.tsx` hat eine eigene, nicht zum Formular gehörende `Row`.
- Slice 40: Die Durchsicht fand, dass `.form-group` (0,1,0) in der Verwaltung gegen `.admin fieldset` (Abstand 4 px statt 12 px) und `.admin h3` (Rand oben) verliert, was jsdom nicht zeigt. Die Regeln stehen jetzt als `.form-group.form-group` (höhere Spezifität, unabhängig von der Ladereihenfolge); veraltete CSS-Kommentare bereinigt, ein Test für erneutes Speichern ergänzt. Im Bild ist das nicht geprüft (manuelle Testliste: Schließzeiten, Raumsperren, Kalender-Abos auf gleichen Abstand der Gruppen achten).
- Slice 47: Die Bezahlung (Anzahlung, Kartengarantie, Gutschein) braucht keine Stripe-Quelle in der CSP: sie leitet zu Stripe Checkout weiter, der Kundenbereich lädt nichts von Stripe. Die Prüfung des gebauten Kundenbereichs über nginx (Startseite, alle Unterseiten, Buchung mit Anzahlung und Testzahlung, Gesundheitsfragebogen, Gutschein mit Testzahlung) zeigte keine CSP-Meldung; eine Gegenprobe mit einem fremden Bild wurde als Verstoß gemeldet.
- Slice 47: In späteren Läufen der Browserprüfung zeigte Graz für „Klassische Massage“ und „Sportmassage“ eine Woche lang „0 freie Zeiten“, im ersten Lauf noch freie Zeiten; nicht untersucht (kein Bezug zur CSP, Testdaten nach eigenen Buchungen).
- Slice 47: Beim Einmischen von `dev` fehlte in der manuellen Testliste das `Result: ☐` von Slice 37 (bei dessen Squash noch vorhanden, später auf `dev` verloren) und nach der Auflösung das von Slice 39; beide wieder eingesetzt (11 Blöcke, 11 `Result:`).
- Slice 56: „Wie drucke ich den Dienstplan?“ (Frage der Slice für die manuelle Prüfung) findet im Handbuch einen Treffer und ist damit beantwortet; die Testliste nennt eine Frage, die der API-Test als „ohne Treffer“ belegt.
- Slice 56: Der Phone-Filter hätte Ziffern eines Namens mit Ziffern (Testkunde „Muster<Guid>“) als Telefonnummer ersetzt; Namen werden deshalb vor Telefonnummern ersetzt, und Wörter umfassen auch Ziffern. EF/Npgsql übersetzt `ToLowerInvariant` nicht; der Abgleich läuft mit `EF.Functions.ILike` je Wort.
- Slice 56: Die Durchsicht (Opus) fand zwei Datenschutzlücken beim Bereinigen (Genitiv „Hubers“ und Teile von Doppelnamen blieben stehen; Nummern mit Punkt wie „0664.1234.567“ wurden nicht ersetzt, ISO-Datum und „2025-2026“ dagegen schon) und einen Datenverlust (eine kaputte Zeile leerte die ganze Datei); alle drei behoben und mit Tests belegt, dazu Neulesen beim Aufklappen und die Rollenregel im Web als Funktion.
- Slice 56: Abgelaufene Einträge verschwanden zuerst nur aus der Anzeige, nicht aus der Datei; jetzt schreibt das Lesen die Datei ohne sie neu (Dateitest).
- Slice 56: Im Block „Slice 37“ der manuellen Testliste fehlt die Zeile `Result: ☐` (beim Einmischen einer anderen Slice verloren); nicht in dieser Slice geändert.
- Slice 42: Vier bestehende Tests der Zeiterfassung griffen den Knopf über den Namen „Ändern“; mit dem neuen Namen „Abschnitt 09:00–13:00 ändern“ mussten sie den Namen anpassen (Aussage und Prüfung gleich, kein Test abgeschwächt).
- Slice 48: jsdom löst bei Enter im Feld keinen Formular-Absenden aus und `user-event` ist nicht installiert; die Tests prüfen deshalb, dass das Feld in einem Formular mit Standardknopf `type="submit"` steht, und senden mit `fireEvent.submit(feld.form)`.
- Slice 43: Der Fehlertext der Buchung steht jetzt zweimal im Dokument (Sprunglink in der Übersicht und am Feld); drei bestehende Tests (`findByText`) zählen ihn als zwei Treffer. Die Kassa-Tests, die „Abrechnen“ über den exakten Namen suchten, nutzen `/^Abrechnen/`, weil der Name jetzt Behandlung und Gast trägt (Aussage unverändert).
- Slice 43: Das Rot des Abnahmefalls „Skip-Link vor Überschrift“ gab es nicht: die Gegenprobe (Fokus bleibt nach der Anmeldung am Seitenanfang) war schon grün, sie schützt nur vor einem Fokusraub durch `PageHeading`.
- Slice 43: Die Durchsicht (Opus) fand, dass `PageHeading` im StrictMode (Dev-Server) gleich nach der Anmeldung den Fokus nahm (doppelter Effektlauf mit „erstes Mal“-Flag); jetzt vergleicht der Effekt die letzten Werte, die Gegenprobe läuft unter `StrictMode`. Außerdem: Sprunglinks nur für bekannte Felder (Serverfehler anderer Felder stehen als Text), keine sprachabhängige Regex für den Linktext.
- Slice 50: axe fand in den bisher ungeprüften Masken nur einen Verstoß (`empty-table-header` in der Kostentabelle, gleiche Bauart in drei weiteren Tabellen); Abos, alle 14 Reiter der Stammdaten, Zugänge, Anmeldung, Wiederherstellung, Passwortänderung, Registrierung und Einladung waren sonst sauber. Die übrigen Funde kennt axe nicht und stehen als Befunde:
  - B71: Die Reiter der Stammdaten (Standorte, Team, Behandlungen, Postfach, Firmendaten, Zahlungen, Kassa, KI u. a.) und das Personal bauen Felder als umhüllendes `<label>` ohne `FormField`/`FormGroup`: kein Hilfetext und kein Fehler per `aria-describedby` am Feld, Fehler als Sammelmeldung unter der Aktionsleiste (masken.md R1, R6 bis R8). Umbau von rund 15 Dateien, deshalb eigene Slice. – erledigt in Slice 60
  - B72: Anmeldung, Wiederherstellung, Einrichtung, Registrierung und Passwortänderung (`auth/StandaloneAuth.tsx`, `SignupForm.tsx`) prüfen Pflichtfelder nur über das HTML-Attribut `required` (Browser-Sprechblase) und melden „Passwörter stimmen nicht überein“ als Formularmeldung statt am Feld; kein `FormField`. – erledigt in Slice 61
  - B73: `anamnesis/Modal.tsx` ist ein `div` mit `role="dialog"` statt `<dialog>`: kein Fokusfang, Hintergrund nicht inert, Esc am `document`, feste ID `modal-title`. Der Umbau berührt die Druckansicht des Papierbogens (`.modal-backdrop` in `@media print`), die nur im Browser zu prüfen ist. – erledigt in Slice 64
  - B74: Hinweise nur als `title` an `span`, `td`, `li` und Spaltenköpfen (Sprache in `closures/CancelAppointmentsDialog.tsx` und `ClosuresPage.tsx`, Fehlergrund in der Mitteilungstabelle, Spaltenerklärungen in `admin/LocationsTab.tsx`, Ausführung in `admin/RebookSection.tsx`) sind per Tastatur und Touch nicht erreichbar; sie brauchen den Tooltip-Baustein (Slice 45) oder sichtbaren Text. – erledigt in Slice 65
  - B75: „Einladungslink kopieren“ (`admin/StaffPage.tsx`) meldet nicht, dass kopiert wurde, und tut ohne Zwischenablage nichts; „Diesen Browser als Empfangsgerät anmelden“ ist ohne Grund gesperrt (schon in den zurückgestellten Kleinigkeiten von Slice 48 genannt). – erledigt in Slice 65
  - B76: Rund 45 Knöpfe in `admin/*` tragen kein `type`; solange sie nicht in einem Formular stehen, ist das folgenlos, bei der Umstellung aus B71 werden sie zu Absendeknöpfen und müssen dann `type="button"` bekommen. – erledigt in Slice 60
- Slice 44: Der Prüftest für „+ “ am Anfang fand zwei Knöpfe, die der Befund B67 nicht nennt („+ Mindestmenge“ im Lager, „+ Zeit nachtragen“ in den Zeiten) und acht weitere in den Stammdaten, im Gruppenpanel und im Posteingang der Kunden; alle tragen jetzt ein Verb. „Neuer Kunde“ im Buchungsfeld heißt „Kunde anlegen“.
- Slice 44: Der Server liefert beim Lohn-Export eine leere Datei, wenn keine Zeile Personalnummer und Lohnart hat; die Prüfung vor dem Export liegt deshalb im Browser (`payroll/exportCheck.ts`), nicht am Server.
- Slice 44: `problemText` (Verwaltung) hatte den Fallback „Speichern hat nicht geklappt.“ auch für Ladefehler und Exporte; er heißt jetzt wie die übrigen Fälle (`GENERIC_PROBLEM`).
- Slice 45: Im Gesamtlauf nach dem Einmischen von `dev` waren `StandaloneAuth.test.tsx` und `RosterPage.test.tsx` je einmal rot (Last), einzeln und unverändert grün (16 Tests); beide Dateien sind nicht Teil der Slice.
- Slice 45: Das Popover immer ins DOM zu setzen (Beschreibung per `aria-describedby`) verdoppelte den sichtbaren Text in `getByText` (Kalender-Panel „Schließt bei Anna direkt an …“); deshalb `aria-description` am Ziel und Popover nur im offenen Zustand. Die Lint-Regeln für Refs und Abhängigkeiten ließen weder `useRef`-Zeitgeber noch `useCallback`-Ketten durch; ein kleiner `Controller` je Tooltip trägt Zeitgeber und Ziel. Der Warntext der Tageszellen in der Zeiterfassung stand im `title` eines inneren `div` im Knopf; der Tooltip hängt jetzt am Knopf. B74 aus Slice 50 ist damit für die Erklärungen miterledigt (Sprachnamen bleiben als Namens-`title`).
- Slice 46: Im Lager (`StockPage`) war die Meldung immer `role="status"`, auch bei Fehlern; mit dem gemeinsamen Baustein ist ein Fehler dort jetzt `role="alert"` wie auf den anderen fünf Seiten (kleine Verbesserung, die Lager-Tests blieben grün).
- Slice 46: Die Vitest-Gesamtsuite war einmal in `time/TimesheetPage.test.tsx` rot (1 Test, 1,25 s), einzeln und im zweiten Lauf grün: Last, keine Regression. `booking.css` (80, 104, 573) und `myday.css` (203, 350) setzen noch `font-weight: 700`, der Brief erlaubt nur 400, 500 und 600.
- Slice 51: axe fand in den Masken Dienstplan, Zeiterfassung, Buchhaltung, Lohn und Berichte (auch mit geöffnetem Tagespanel und den Berichtsansichten Tagesplan, Tagesstart, Management, Pflichtberichte) keinen Verstoß; die Funde stammen aus der Durchsicht des Quelltextes. Die Scan-Funktion des Tests für `type` ist ein kleiner Parser statt eines Regex, weil `=>` in Ausdrücken ein `>` enthält. Befunde dieser Slice (fortlaufend ab B77):
  - B77: Gesperrte Knöpfe und Felder ohne sichtbaren Grund: „Übernehmen (n)“ im Ausfall-Assistenten (`roster/AbsenceAssistant.tsx`, gesperrt ohne Auswahl), „Tagesstart abnehmen“ und „Raumpflege … abnehmen“ (`reports/HygieneView.tsx`, gesperrt ohne Prüferin und PIN), „Sofort gültig, ohne Veröffentlichen“ bei Krankenstand (`roster/RosterPage.tsx`, `disabled` ohne Hinweis), Stempeln ohne Standort (`time/TimeClock.tsx`). Sie brauchen `aria-disabled` mit Hilfetext wie `SubmitButton` (Slice 48). – erledigt in Slice 62
  - B78: Die Felder im Tagespanel des Dienstplans (`RosterPage.tsx`, `DayEditor`) und der Zeiterfassung (`TimesheetPage.tsx`, Nachtragsformular), im Ausfall-Assistenten und in den Berichtsansichten bauen Beschriftung und Eingabe als umhüllendes `<label>` ohne `FormField`: Hilfetext und Fehler hängen nicht per `aria-describedby` am Feld; der Dienstplan-Editor und der Nachtrag sind keine `<form>` (Enter sendet nicht, Pflichtfehler als Meldung unter der Leiste). – erledigt in Slice 62 (ohne `<form>`: B99)
  - B79: Die Berichtsansichten (`reports/*View.tsx`) haben keine Tastaturprüfung der Tabellen und Filter im Test (nur axe); Tabulatorreihenfolge, Fokusring und Ansage der `role="status"`-Meldungen sind nur manuell zu sehen (Testliste Slice 51). – erledigt in Slice 67
- Slice 51: Nach dem Einmischen von `dev` war `SubscriptionsPage.test.tsx` (axe-Fall der Abo-Seite) in der vollen Suite einmal rot („Unable to find role list“), einzeln und im zweiten Lauf grün: Last, keine Regression.
- Slice 52: Die Durchsicht fand den Kundenbereich, „Mein Tag“ und die Knöpfe überall in Ordnung (alle Knöpfe tragen `type`, Tabellenköpfe `scope`, Radio- und Häkchenfelder stehen in Beschriftungen oder `fieldset`, Fokusring, `prefers-reduced-motion` und Sprunglink sind da, keine Farben im Quelltext). Behoben ist nur der Betreiber-Fehler am Feld „Grund“. Die Symbolknöpfe im Lager mit `title` (Schließen, Nachfüllen, Bewegung buchen, Entfernen) tragen jetzt den Tooltip-Baustein aus Slice 45 (neuer Test); „Von Raum … übernehmen“ hat der Baustein schon in Slice 45 bekommen. Die übrigen Funde:
  - B80: Die Formulare des Betreibers (Anmeldung, Spa sperren, Nummer zuordnen, Tarif) sind umhüllende `<label>` mit HTML-Pflichtattribut (Browser-Sprechblase) und ohne `FormField`; nur Anmeldung und Sperren verbinden den Fehler mit dem Feld, „Nummer zuordnen“ meldet ihn als `alert` unter dem Formular. Gleiches Muster wie B72 in der Verwaltung. – erledigt in Slice 61
  - B81: `stock/stock.css` setzt alle Schriftgrößen in `px` (11,5 bis 22), die übrigen Stylesheets `rem`; der Skill verlangt `rem`, damit die Schrift mit der Browser-Einstellung wächst. Die Team-Oberfläche (`index.css`) arbeitet ebenfalls mit `px`, deshalb ist die Umstellung eine Entscheidung für das ganze Team-Layout.
  - B82: Keines der vier Stylesheets hat eine Regel für `forced-colors: active`. Im Kundenbereich zeigen die gewählte Zeit, der gewählte Tag und die gewählte Leistung (`.choice`, `.day`, `.time`) den Zustand über Randfarbe und `box-shadow`; im Kontrastdesign fällt beides weg, sichtbar bleibt nur, was das Alternativfeld des Browsers zeichnet. Nur im Browser mit Windows-Kontrastdesign prüfbar, deshalb nicht blind umgebaut. – erledigt in Slice 66
  - B83: Der Kontrast der vier Stylesheets ist nicht Teil der axe-Läufe (jsdom hat keine Farben); Sichtprüfung in hell und dunkel steht aus. – erledigt in Slice 66

- Slice 49: Neue axe-Läufe über das Tagesraster mit geöffnetem Termin, den Posteingang mit geöffneter Nachricht, den Begleiter mit Antwort und den Assistenten mit Tabellenantwort fanden keinen Verstoß (Kassa und die Terminpanels hatten sie schon). Der „Live-Stand“ ist keine eigene Maske, sondern `live/connection.ts` (SignalR) mit der Anzeige „Live/Offline“ im Kalender. Behoben sind drei Kleinigkeiten (siehe Rulings); die übrigen Funde:
  - B84: Felder in Kalender, Posteingang und Kassa (`calendar/AppointmentPanel.tsx`, `BookingPanel.tsx`, `AreaPanel.tsx`, `inbox/InboxPage.tsx`, `pos/BillPanel.tsx`, `ReceiptView.tsx`, `DayPanel.tsx`) sind umhüllende `<label>` ohne `FormField`; kein Hilfetext und kein Fehler per `aria-describedby` am Feld, 17 Fehlermeldungen als `role="alert"` unter dem Formular. Einzige Ausnahme ist `MoneyField`. Gleiches Muster wie B71, B72 und B80; Umbau von rund 12 Dateien. – erledigt in Slice 63
  - B85: `index.css` hat keine Regel für `forced-colors: active`; im Windows-Kontrastdesign hängen der Zustand der Knöpfe mit `aria-pressed` in der Kalenderleiste, die gewählte Nachricht (`.mail.selected`) und die Statusfarben der Termine an Farbe und `box-shadow`. Nur im Browser prüfbar (wie B82). – erledigt in Slice 66
  - B86: Die Rückmeldeknöpfe des Assistenten („Hilfreich“, „Nicht hilfreich“, `assistant/AnswerBlock.tsx`) und die KI-Pille tragen Zusatzinfo nur als `title` (Maus); der Name ist sichtbarer Text, deshalb geht keine Information verloren, der Tooltip-Baustein wäre die saubere Form (Rest von B74). – erledigt in Slice 65
  - B87: Rund 55 Knöpfe in `calendar/`, `inbox/` und `pos/` tragen kein `type`; solange keine Formulare darum stehen (nur „Neuer Kunde“ in `BookingPanel` und der Assistent, dort mit `type`), ist das folgenlos (wie B76). – erledigt in Slice 63
  - B88: `index.css` setzt 67 Schriftgrößen in `px` statt `rem` (zusammen mit B81 eine Entscheidung für das ganze Team-Layout).
  - Hinweis der Durchsicht: Der Tooltip am Begleiter-Knopf öffnet sich auch nach dem Schließen der Leiste (der Fokus kehrt auf den Knopf zurück), und Esc schließt bei offenem Tooltip erst diesen, dann die Leiste (Verhalten des Bausteins aus Slice 45).
- Slice 54: Eine nummerierte Liste mit Bildern dazwischen ergibt in beiden Lesern mehrere Listen, die bei 1 neu zählen; deshalb sind die Schritte `###`-Überschriften „N. Titel“ (erscheinen nicht unter „Auf dieser Seite“).
- Slice 54: `SpaSample.CalendarDay()` vergibt bei jedem Aufruf neue Termin-Nummern, nur `SpaSample.Selected` ist fest; `SpaSample.Paying` stimmt nicht mit der angezeigten Karte überein. Die Bildtests suchen den Termin deshalb über den Namen des Gastes.
- Slice 54: Der Vitest-Cache in `web/node_modules` (Junction auf den Hauptordner) kannte die neuen Bilder nicht; `help.test.tsx` meldete „Bild fehlt“, bis `.vite` und `.vitest-cache` beiseitegelegt waren.
- Slice 54: Zwei bestehende Tests hingen an Einzelheiten der umgestellten Themen und sind mitgezogen (kein Abschwächen): `HelpTests` liest den Kurzhinweis jetzt aus dem Abschnitt „schritte“ statt „ziehen“, `help.test.tsx` zählt die Übersicht ohne den Einstieg je Rolle, der Abläufe wiederholt.
- Slice 54: Die Markierung entsteht im Bildtest in der `OverlayLayer` des Fensters; ein Ziel, das fehlt oder außerhalb des Fensters liegt, bricht den Test ab. Die Bilder zeigen das ganze Fenster (1440 × 900, 256 Farben, je rund 70 kB).
- Rundenabschluss: Die volle Prüfkette war in Format, Build, `dotnet test --solution` (2 absichtlich übersprungen), `gen:api`, lint, typecheck, Web-Build und Bundle grün. `npm test` ohne Begrenzung startet so viele Worker wie Kerne (20) und lief zweimal in wechselnde 5-s-Timeouts (1 bzw. 8 Tests in anderen Dateien, einzeln grün); mit `--maxWorkers=3` wie in den Slice-Prüfungen lief die Suite grün (692 von 692). Vorschlag: die volle Prüfkette in work/conventions.md ebenfalls mit `--maxWorkers=3`.

## Zurückgestellte Kleinigkeiten
- Slice 30: Der Prüftest vergleicht den Zusatz als Teilstring; eine zusätzliche Nummer hinter der erwarteten Liste fällt nicht auf. Absichtlich nachsichtig, weil eine spätere Slice, die einen R01-Befund weiterführt, ihre Nummer anhängt.
- Slice 30: Prüftest über alle geschlossenen Rundenakten statt nur R01 (die offenen R02-Slices wären sonst rot); mit dem Abschluss von R02 nachziehen.
- Slice 33: Die gekürzte Kundennotiz im aufgeklappten Abschnitt „Kunde“ (`CalendarView.axaml`, `MaxLines="2"`, voller Text nur in `NoteTip`) steht in W-Muster 12, aber nicht im Umfang der Slice; sie bleibt nur per Maus vollständig lesbar. Ein sichtbarer Sperrgrund unter gesperrten Knöpfen (für sehende Tastaturnutzer) wäre neue Gestaltung und ist offen.
- Slice 31: `Companion.tsx` (Leiste der Team-Oberfläche im Browser) prüft nur `e.code === 'Space'`; Tasten ohne Scan-Code bleiben dort ohne Wirkung.
- Slice 31: Mitten im Scrollen läuft Text unter dem schwebenden Knopf durch (nur am Ende des Inhalts ist Platz); ein fester Streifen auf allen Seiten wäre eine Entscheidung für den Auftraggeber.
- Slice 35: Zwei „Technische Angaben“ stehen auf „Geräte“ (Bondrucker, Kartenterminal) mit gleichem Namen für den Screenreader; ein Bezug im Namen („… zum Bondrucker“) fehlt. Die Handbuchbilder `im-netz.png`, `geraete.png`, `abgleich.png` und `kalender-meldungen.png` zeigen noch die alten Texte (neu erzeugen mit `scripts/help-images.sh`).
- Slice 35: Der Hauptknopf „Termin“ im Kalenderkopf heißt nach W58 besser „Neuer Termin“ (Verb plus Objekt); das trifft Hilfetexte, Bilder und viele Tests und bleibt eine eigene Kleinigkeit.
- Slice 35: Das Suchfeld der Web-Hilfe hat keine eigene Lupe (nur Chromiums Löschen-Kreuz); eine Lupe wäre neue Gestaltung der Web-Hilfe und gehört zu einer Durchsicht nach `fluent2-web`.
- Slice 35: Die Hover-Fläche der Meldungskarte liegt nur auf dem Kopfteil, nicht auf der ganzen Karte (Aktionsknöpfe sind eigene Knöpfe daneben).
- Slice 34: Die Ansage der Konfliktmeldung nennt Titel und Handlungszeile, nicht mehr die Konfliktzeilen; ob der Screenreader sie vorlesen soll, entscheidet erst Narrator am echten Fenster (manuelle Testliste). Die Ansage `Assertive` am Statustext der Verbindungsseite gilt auch für die Fortschrittstexte beim Start (so schlägt es W19 vor); nach dem Schließen der Konfliktmeldung bleibt der Abstand des Panels (12 px) stehen. Die Aktion kennt nur „Termin anzeigen“; ein Weg für Sperrzeiten, Räume und Geräte (z. B. „Sperrzeit öffnen“) wäre neue Funktion.
- Slice 38: Die Absage-Maske scrollt als Ganzes; der Brief (5.27, „Rückfrage mit Auswahl und Text“) will Titel und Folge oben und die Knopfleiste fest unten, nur den Inhalt dazwischen scrollend. Das war schon vorher so und bleibt ein eigener Schritt.
- Slice 38: B-Muster 1 nennt außerdem zwei Eigenbau-Modals und ein Eigenbau-Popover (B1, B21); sie gehören nicht zu dieser Slice.
- Slice 38: Das Aussehen in Hell und Dunkel ist nur aus dem Quelltext gelesen, nicht am Bild geprüft; die Betreiber-Seite kennt nur einen Teil der Tokens, deshalb hat `confirmDialog.css` Rückfallwerte (`var(--x, var(--y))`).
- Slice 32: Die Handbuchbilder der Windows-App zeigen noch die alten Kartenränder, Höhen und die schwebenden Seitenleisten; neu erzeugen beim nächsten Bilderlauf (Slice 36 oder Rundenabschluss).
- Slice 32: `Padding="16,12"` und `Padding="12,10"` stehen als lokale Werte an den Karten; ein eigener Stil (`Border.card.compact`, `Border.hint`) käme erst mit einer dritten gleichartigen Stelle.
- Slice 32: Neun Hinweise sind noch Karten (`Classes="card"` auf einer Soft-Fläche, Radius 12 statt 8, Innenabstand 10/8 statt 10/12: `CustomersView` :114 :209, `InboxView` :108 :116 :119 :160, `LoginView` :246 :255, `PosView` :404), und die drei Kalender-Hinweise haben keinen Rahmen 1 px (Brief 5.9); nicht in den fünf Befunden, offen für eine spätere Slice oder einen Befund ab W67.
- Slice 32: Der Prüftest `GridValueTests` findet nur die Muster der Befunde (Kartenränder, Abstände, Höhen, Radius 6 auf Soft-Flächen in derselben Zeile); `Margin`, Setter in `Styles.axaml` und Hinweise als Karte prüft er nicht.
- Slice 32: Der runde Knopf „KI und Hilfe“ (`CompanionFab`, 20 px vom Rand, 48 breit) liegt im Kalender über der unteren Ecke der Seitenleiste, seit sie am Fensterrand anliegt: er ragt etwa 8 px weiter in die Reiterschiene (`RailWidth` reserviert 60 px rechts) und deckte den Kopieren-Knopf der untersten Kontaktzeile ab (Test `Kontaktzeilen_sind_Links_mit_Kopieren_Knoepfen_…` rot). Gemessen mit einer Headless-Sonde: der Knopfmittelpunkt rückte durch die 16 px nach rechts vom Kreisrand (Abstand 29,8 px) in den Kreis (23,2 px); ein größerer Innenabstand unten hilft nicht, weil `BringIntoView` am Rand des Sichtfensters ausrichtet. Übergang: der scrollende Inhalt der Kalender-Leiste (`ScrollViewer`) hat unten 32 px Rand, so viel wie der Knopf über die Reiterschiene in die Leiste ragt; der Inhalt endet darüber, das Kundenformular behält seine Aktionsleiste im Fuß (ein Rand am ganzen Inhaltsbereich hatte dessen Test rot gemacht). Bis Slice 31 (Ä74) den Knopf verlegt; dann den Rand entfernen und die 60 px in `RailWidth` prüfen. An Slice 31 gemeldet.
- Slice 32: Der Inhalt der Leiste „KI und Hilfe“ hat Rand 16 ringsum, der Kalender-Inhalt `16,12,16,32`; Brief 5.12 Punkt 5 sagt nur „Seitenrand 16“, masken.md §4 „16 oben/unten, 20 links/rechts“ für die Seitenleiste; beide Leisten sind seitlich gleich, oben/unten nicht wörtlich nach masken.md.
- Slice 37: Der Scan prüft das Vorhandensein eines lesbaren Namens, nicht seine Güte (Doppelnamen wie zwei „Technische Angaben“ auf „Geräte“ meldet er nicht, siehe Slice 35); die Leiste „KI und Hilfe“, der Abgleich-Bericht und Flyouts (Konto, Termin-Menü) sind nicht gescannt, weil sie eigene Zustände brauchen. Ein Scan je Zustand für die Web-Oberfläche (Testing Library mit axe) ist offen.
- Slice 39: Doppelklick-Schutz als Standard im Baustein `ConfirmDialog` (z. B. `onConfirm` als Promise, Baustein sperrt selbst) statt je Aufrufer; die Stand-Seite mit vier R02-Zeilen zu einer Zeile zusammenführen.
- Slice 40: `stock/stockUi.tsx` `Field` (Lager-Seite, B9 nennt es als drittes Feld) und die `stock-*`-Klassen sind nicht zusammengeführt; der Befund sagt „schrittweise“. Eine Folge-Slice kann `Field` auf `FormField` umstellen, wenn die Lager-Masken ohnehin angefasst werden.
- Slice 40: In `BusinessInfoPage` ist die Feldbreite `short` (vorher volle Breite der Spalte) zu `full` geworden, damit das Bild gleich bleibt; die gemeinsame Breite `short` hat 280 px wie bei den Aktionen.
- Slice 47: Die Browserprüfung lief nur in Chrome und nur als Skript im Scratchpad (`playwright-core`); Edge, Firefox und Safari und ein Browser-Test in der CI fehlen. Vor dem Umschalten auf durchsetzend nachholen (ADR 0080).
- Slice 47: Ein eigener Webserver vor der API beim Kunden (statt nginx aus `docker/`) setzt die Richtlinie nur, wenn er den Header der API durchreicht oder selbst setzt; im Server-Betrieb liefert nginx die Dateien, nicht die API.
- Slice 56: Kein Handbuchbild der Liste (Bildtests der Hilfe-Seite brauchen den Lauf mit `AISPA_SCREENSHOTS`); andere Beugungen als der Genitiv auf „s“ und Namen von Personen ohne Kundenkarte bleiben stehen; die offene Liste im Web liest erst beim nächsten Öffnen der Hilfe-Leiste neu; zwei gleichzeitig offene App-Fenster können sich beim Schreiben der Datei überschreiben.
- Slice 42: Die Panels im Kalender (Termin-Panel u. a.) nutzen `SidePanel` noch nicht; der Befund sagt, sie seien nicht geprüft, und der Auftrag nennt nur Dienstplan und Zeiterfassung.
- Slice 42: Das Panel der Kassa (`pos/DayPanel.tsx`, Tagesabschluss) ist ein anderer Aufbau (`aside` im Kassa-Raster) und bleibt unverändert.
- Slice 48: „Startpasswort (mindestens 8 Zeichen)“ behält den Klammerzusatz in der Beschriftung (masken.md R6 verlangt einen Hilfetext am Feld); der Grund steht am Knopf. „Empfangsgerät anmelden“ (StaffPage), „Passwort zurücksetzen“ ohne Wiederholung und andere Seiten mit losen Knöpfen sind nicht umgestellt; die Feldbeschriftungen der Formulare nutzen noch nicht `FormField` (B9 schrittweise).
- Slice 48: Durchsicht (Opus): ein zweites Enter während eines laufenden Aufrufs sendet nochmals (kein „Wird gesendet …“-Zustand; mit Klick schon vorher so), und Enter im gesperrten Formular nennt den Grund nur am Knopf (kein Fokussprung ins fehlende Feld, Verknüpfung mit `useFocusFirstInvalid` offen). Behoben aus der Durchsicht: `aria-disabled` sieht wie `:disabled` aus und hat kein Hover; `noValidate` an den Formularen, damit keine Browser-Sprechblase die Meldungen des Servers ersetzt.
- Slice 43: Anamnese, Gutschein und Warteliste im Kundenbereich haben dieselbe Fehlerübersicht nur mit Zahl (`AnamnesisPage`, `Vouchers`, `Waitlist`); die Slice nennt nur die Buchung (B61).
- Slice 43: Die Kalender-Seitenpanels und weitere Listen mit `selected`-Klasse außerhalb von `admin-list` (z. B. Chips in anderen Seiten) tragen noch kein `aria-current`; nicht im Befund.
- Slice 50: Der Farbkontrast ist nicht Teil der axe-Läufe (jsdom hat keine Farben); er ist Sache von `designTokens.test.ts`. Reihenfolge der Tabulatortaste im Browser, Screenreader-Ansage und Druckansicht sind nicht geprüft.
- Slice 44: Ladefehler in „Meine Termine“ und in der Anamnese des Kundenbereichs haben noch keinen Knopf „Erneut versuchen“ (B60 nennt nur die Buchung); „keine online buchbaren Behandlungen“ bietet bei einem Standort weder Telefon noch Mail an, weil der Online-Standort keine Telefonnummer mitliefert.
- Slice 44: Die Fehlertexte des Kundenbereichs für Terminverschiebung und Stornierung (`Move`, `MyAppointmentsPage`) bleiben bei `errorGeneric`/`errorOffline`; nur die Buchung bekam den Wiederholen-Knopf.
- Slice 45: Reine Namen bleiben als natives `title` (Symbolknöpfe mit `aria-label`, „Hilfe“, „Entfernen“, „Schließen“, „Weitere Aktionen“, Sprachname, Mandantenname, Knöpfe der KI-Antwort, „Gemeinsam gebucht“, „Nachfüllen“, „Bewegung buchen“); eine spätere Slice kann sie auf den Baustein heben, wenn Touch-Zugang nötig wird. Die Tooltips der Windows-App sind nicht Teil dieser Slice.
- Slice 45: Durchsicht (Opus), behoben: Ziehen im Tagesraster schließt den Tooltip (`onDragStart`) und der Hinweis fängt mit `pointer-events: none` keine Ablage ab; ein Wechsel zwischen leerem und gefülltem Text baut das Ziel nicht mehr neu auf (Fokus bleibt); Esc wird nur bei Fokus-Öffnung verbraucht; der Hinweis ist dadurch nicht mehr mit der Maus erreichbar. Zurückgestellt: Hover-Ende schließt einen per Fokus geöffneten Tooltip, und die Lage wird bei Scrollen oder Größenänderung nicht nachgeführt.
- Slice 45: Der Prüftest sieht nur wörtliche und einfache berechnete `title`-Werte an Elementen mit Kleinbuchstaben-Tag; ein `title`, das über gespreizte Props hineinkommt, fällt nicht auf.
- Slice 46: `font-weight: 700` in `booking.css` und `myday.css` auf 600 stellen (Brief Abschnitt 3); nicht in B63, der nur `index.css` nennt.
- Slice 46: Hilfetexte zu „Farbe im Kalender“ und „Rest bar“ bleiben unverändert; kein Ablauf hat sich geändert, deshalb kein Hilfetext und kein neues Bild.
- Slice 52: Es gibt keinen eigenen axe-Lauf je Eingabe-Kombination des Kundenbereichs; die vorhandenen Läufe in `booking.test.tsx` decken die Seiten ab, ob jede Kombination dabei ist, wurde nicht Zeile für Zeile geprüft.
- Slice 53: Noch kein Hilfetext nutzt `> Hinweis:`, `> Tipp:` oder `> Achtung:`; die Kästen sind Mittel für Slice 54. Der C#-Leser der Windows-App und des Assistenten (`src/AispaStudio.Help/HelpMarkdown.cs`) kennt die Zeilen mit `>` noch nicht und würde sie als Absatz mit „> “ zeigen; er muss sie vor dem ersten Text mit Kasten lesen (Slice 54 oder eigener Schritt).
- Slice 53: Die Schublade der Verwaltung zeigt Brotkrumen, „Auf dieser Seite“ und den Fuß mit denselben Bausteinen, aber ohne eigene Maße für die schmale Leiste; die Werte aus 5.30 sind für die Seite `/help/` gemessen.
- Slice 49: Die Tastenbedienung des Kalenders (Terminverschieben per Ziehen nur mit Maus, Alternative „Verschieben“ im Terminpanel) und die Ansage der Live-Anzeige sind nur im Test geprüft, nicht mit Screenreader.
- Slice 54: Die übrigen 179 Themen folgen dem Muster noch nicht (Rest in der nächsten Runde, Slice-Datei „NICHT bauen“); die Prüfregel gilt schon für jedes Thema mit `aufbau: schritte`.
- Slice 54: Im Einstieg der Therapeut:innen stehen Abläufe, die noch nicht im Muster stehen (Tagesplan, Termin finden, Tagesstart); die Abläufe der Team-Oberfläche (`images/web`) haben noch kein Bild mit Markierung.
- Slice 54: Die Bilder zeigen das ganze Fenster; ein Zuschnitt auf Ziel und Umgebung wäre kleiner und deutlicher, ändert aber die Bildtests und die Maße im Text.
- Slice 54: Rückmeldung je Thema bleibt Slice 55; die Hinweiskästen (`> Hinweis:` usw.) nutzt weiter noch kein Text, der C#-Leser kennt sie noch nicht (siehe Slice 53).
- Slice 55: Die Rückmeldung liegt im `localStorage` des Browsers bzw. des eingebetteten WebView2; ob die Windows-App den Datenordner des WebView2 zwischen Starts behält, ist nicht geprüft (dieselbe Frage wie bei den offenen Fragen, Slice 56). Es gibt kein Ändern und kein Löschen einer Rückmeldung und keine Ansicht dafür; eine Auswertung bräuchte einen ADR.

## Offene Entscheidungen
Alle Punkte am 26.10.2026 entschieden (Auftraggeber, nach Empfehlung von Claude); die frühere Frage steht im Git-Verlauf. Die nächste Runde schneidet sie als Slices.
Ruling: Slice 47 – Spas dürfen den Kundenbereich auf ihrer eigenen Website einbetten, aber nur von eingetragenen Adressen: je Mandant eine Liste erlaubter Websites, aus der `frame-ancestors` entsteht; ohne Eintrag bleibt es bei „nur eigene Seite“; Nachtrag zu ADR 0080 und Hilfetext – Auftraggeber, 26.10.2026
Ruling: Slice 36 – Kein eigener ADR für den neuen Wert der Statusfarbe „Erledigt“; ein Satz im Brief verweist auf das Ruling vom 25.10.2026, weil die Änderung einen Kontrastfehler behebt und keine neue Gestaltungsregel ist – Auftraggeber, 26.10.2026
Ruling: Slice 38 – Der gemeinsame Rückfragedialog folgt Brief 5.27: bei zerstörenden Aktionen Fokus auf „Abbrechen“, bei neutralen Rückfragen auf dem primären Knopf, mit Eingabefeld im Feld; der Baustein bekommt dafür eine Option, wie die Windows-App seit R01 Slice 28 – Auftraggeber, 26.10.2026

## Manuelle Testliste

### Slice 30 – Befundliste: erledigte Befunde gekennzeichnet
Start: `docs/umsetzung/aenderungsliste-oberflaechen-2026-10-23.md` im Editor öffnen (kein Programmstart nötig)
1. Nach „| W50 |“ suchen
Expected: In der Spalte „Einstufung“ steht „verstößt – erledigt in Slice 17, 18“
2. Nach „| W-Muster 9 |“ suchen
Expected: Kein Zusatz „erledigt“, der Befund gehört zur Runde R02
Closes: Ruling 14 aus R01
Result: ☐

### Slice 31 – KI und Hilfe in allen Masken der Windows-App
Start: Docker Desktop starten, `dotnet run --project src/AispaStudio.AppHost`, Windows-App im Aspire-Dashboard über `windows-app` starten, als Inhaberin anmelden
1. Nacheinander Kunden, Anfragen, Kassa, Geräte, Verwaltung und Hilfe öffnen
Expected: Unten rechts steht auf jeder Seite der runde Knopf mit dem AISPA-Zeichen; auf Verwaltung und Hilfe steht er unter dem Browser, nicht dahinter
2. Auf der Verwaltung in eine Tabelle klicken und Strg+Leertaste drücken
Expected: Rechts neben dem Browser erscheint „KI und Hilfe“ mit dem Fokus im Fragefeld; Strg+Leertaste noch einmal schließt die Leiste
3. Auf „Anfragen“ eine Mail öffnen und den Inhalt bis zum Ende scrollen
Expected: Die letzte Zeile steht über dem Knopf, kein Text liegt darunter
Closes: Ä74
Result: ☐

### Slice 32 – Maße der Windows-App auf das Raster des Briefs
Start: Docker Desktop, `cd web && npm install && cd ..`, `dotnet run --project src/AispaStudio.AppHost`, Windows-App über die Ressource `windows-app`, noch nicht anmelden
1. Anmeldeseite ansehen: Felder und Knöpfe im Vergleich zum Hilfe-Knopf; dann das PIN-Feld der Schnellanmeldung
Expected: Felder und Knöpfe sind gleich hoch (34 px) und bündig; das PIN-Feld ist höher, weil die Schrift 24 ist, und bleibt lesbar; die Karte hat ringsum denselben Innenabstand
2. Als Inhaberin anmelden, Verwaltung → Sicherung, Geräte, Netzwerk, Abgleich öffnen, jeweils im hellen und dunklen Design (Farbschema in der Leiste links unten)
Expected: Die Karten jeder Seite sind gleich weit eingerückt (Text beginnt in allen Karten auf derselben Linie), nichts springt beim Seitenwechsel
3. Kassa öffnen, einen Termin kassieren: Spalte „Zahlungen“ bis zum Knopf „Bezahlen und Beleg erstellen“ ansehen; dann „Tagesabschluss“ mit gezähltem Bestand
Expected: Der Bezahlknopf ist deutlich höher als die übrigen Knöpfe (46 px); „Tagesabschluss“ ist so hoch wie die anderen Knöpfe (34 px); Karten „Artikel“ und „Gutschein verkaufen“ wirken enger als die Belegkarte
4. Kalender öffnen, einen Termin anklicken (Seitenleiste rechts); dann auf „Kunden“ wechseln und den runden Knopf „KI und Hilfe“ drücken
Expected: Beide Seitenleisten liegen am rechten Fensterrand an, mit einer Linie links, ohne Schatten und ohne runde Ecken; Kopf und Inhalt haben in beiden Leisten denselben Abstand zum Rand
5. Im Kalender eine Warnung auslösen (z. B. Termin per Tastatur auf eine belegte Zeit verschieben) und die InfoBar oben ansehen; danach in der Kundenkarte „Bearbeiten“ öffnen
Expected: Die InfoBar hat weich gerundete Ecken (8) und Luft um den Text (10 oben/unten, 12 links); im Kundenformular stehen die Felder enger (12), die Gruppen „Name“, „Kontakt“, „Anschrift“ sind weiter auseinander (20), der Hilfesatz unter dem Namen klebt am Feld (4)
Closes: W-Muster 9, W3, W23, W41, W59
Result: ☐

### Slice 33 – Tooltip-Inhalte und Sperrgrund auch per Tastatur
Start: Docker Desktop starten, `cd web && npm install && cd ..`, `dotnet run --project src/AispaStudio.AppHost`, Windows-App über die Ressource `windows-app`, als Inhaberin anmelden
1. Kunden › eine Kundin mit Sternen öffnen (z. B. Brigitte Müller), mit Tab auf „Bearbeiten“ und einmal Umschalt+Tab zurück, dann zweimal Tab
Expected: Der Fokusrahmen liegt nacheinander auf den Sternen, der Adresszeile (nach „Bearbeiten“) und der Anrede-Zeile; nach einem Moment erscheint jeweils der Tooltip, Esc blendet ihn aus
2. Dasselbe mit Narrator
Expected: Narrator liest „2 Sterne, Text“ mit dem Satz des Tooltips (z. B. „7 Besuche in zwölf Monaten“), dann die Adresszeile mit „Adresszeile mit dem gültigen Namen …“, dann die Anrede mit „So beginnen Mails …“
3. Kalender › Termin öffnen, mit Tab bis „Kunde“ und einmal weiter, dann → und ←
Expected: Fokusrahmen auf „Besuche gesamt“ mit Tooltip „Erledigte Besuche insgesamt“; → geht zu „Letzter Besuch“, „Nicht erschienen“, „Nächster Termin“ (kein Tageswechsel); Tab verlässt die Gruppe zu „Einchecken“
4. Narrator an, Kalender › auf eine freie Stelle klicken (Buchen ohne Gast), Narrator+F7 (Elementliste) und „Termin buchen“ wählen
Expected: Narrator liest den Namen und „Verfügbar, sobald Gast, Behandlung und Beginn gewählt sind.“
Closes: W-Muster 12, W40
Result: ☐

### Slice 34 – Einzelpunkte Windows-App: Standardknöpfe, Ansage, Konflikt, Stornieren
Start: Docker Desktop starten, `cd web && npm install && cd ..`, `dotnet run --project src/AispaStudio.AppHost`, Windows-App über die Ressource `windows-app`, als Inhaberin anmelden
1. Kalender › einen Termin öffnen, „Verschieben“ wählen und eine belegte Zeit eintragen (oder eine Sperrzeit über einen Termin legen; beim Verschieben per Ziehen erscheint nur die Meldung unten)
Expected: Die Konfliktmeldung erscheint als rote Meldung mit Titel „Zeitraum ist belegt.“, der Handlungszeile, den Belegungen und dem Knopf „Termin anzeigen“ unter dem Text, dazu das Kreuz oben rechts
2. „Termin anzeigen“ klicken, dann beim nächsten Versuch das Kreuz
Expected: Der Termin ist im Kalender markiert; das Kreuz blendet die Meldung aus, ein neuer Versuch zeigt sie wieder
3. Kalender › einen Gruppentermin (z. B. Nina Weiss) öffnen
Expected: „Ganze Buchung stornieren“ steht mit rotem Rand und roter Schrift wie „Termin stornieren …“; Hover und Fokusrahmen sind sichtbar
4. Abmelden, auf der Anmeldeseite Enter drücken (nacheinander im Einzelplatz mit Passwort, bei „Passwort vergessen“ und am Empfangsgerät mit gewählter Person und PIN)
Expected: Enter löst genau die eine sichtbare Anmeldung aus, nie zwei
5. Server beenden, App starten, Narrator an
Expected: Narrator sagt den Verbindungsfehler („Server nicht erreichbar …“) sofort an
Closes: W4, W19, W25, W56
Result: ☐

### Slice 35 – Einzelpunkte Windows-App: Technikdeutsch, Hilfesuche, Meldungskarte, Menü
Start: Docker Desktop starten, `cd web && npm install && cd ..`, `dotnet run --project src/AispaStudio.AppHost`, Windows-App über die Ressource `windows-app`, als Inhaberin anmelden
1. Hilfe öffnen, in das Suchfeld „Kasse“ tippen
Expected: Das Löschen-Kreuz erscheint, unter dem Feld steht „… Treffer“ (bei vielen „Die 12 besten Treffer“); Esc leert das Feld und die Zeile; mit Narrator wird die Trefferzahl nach dem Tippen angesagt. Eine Lupe gibt es im Web-Feld nicht (zurückgestellt)
2. Die Seiten „Im Netz“, „Abgleich“ und „Geräte“ öffnen
Expected: Im Fließtext steht kein „--standalone“, „SSL Mode“, „Port 9100“, „ZVT“ oder „Zertifikat“; unter „Technische Angaben“ (zu) stehen sie nach Enter oder Klick, der Pfeil dreht sich, Narrator sagt „aufgeklappt“
3. Kalender › Meldungen öffnen, mit Tab auf eine Karte, Enter
Expected: Der Fokusrahmen liegt auf dem Kopfteil der Karte, Enter öffnet „Was tun?“; Narrator liest Titel und Text, „Schaltfläche“ und den Hinweis „Details und „Was tun?“ zeigen“; „Wegklicken“ und die Aktion sind eigene Knöpfe danach
4. Kalender › Pfeil am Knopf „Termin“ öffnen
Expected: Nach dem Trenner folgen „Paar oder Spa-Tag“, „Sauna-Eintritt“, „Warteliste ansehen“, „Tagesnotiz“; kein grauer Eintrag „Sonderfälle“
Closes: W8, W27, W57, W58
Result: ☐

### Slice 36 – Farben, Kontrast und Schriftgrößen im Brief
Start: Docker Desktop starten, `cd web && npm install && cd ..`, `dotnet run --project src/AispaStudio.AppHost`, Windows-App über die Ressource `windows-app`, als Inhaberin anmelden
1. Kalender öffnen und einen erledigten Termin suchen (Status „Erledigt“)
Expected: Der Balken links und der Text im Termin sind gut lesbar, die Farbe des Balkens ist deutlich dunkler als vorher (grau-grün statt hellgrau)
2. Windows auf das dunkle Design stellen, Windows-App neu öffnen, denselben Kalendertag ansehen
Expected: Balken und Text des erledigten Termins sind gut lesbar
Closes: W62, B29
Result: ☐

### Slice 37 – Namens-Scan je Zustand
Start: Docker Desktop starten, `cd web && npm install && cd ..`, `dotnet run --project src/AispaStudio.AppHost`, Windows-App über die Ressource `windows-app`, als Inhaberin anmelden
1. Kalender › einen Termin wählen, mit Narrator (Strg+Win+Enter) von Tab zu Tab durch das Termin-Panel gehen, danach „Kunde“ aufklappen und weitergehen
Expected: Jedes Element wird mit einem verständlichen Namen und seiner Rolle angesagt, nie mit „Schaltfläche“ allein, einem Typnamen oder einem Platzhalter
2. Im Termin „Termin stornieren …“ wählen, damit die Rückfrage mit dem Feld „Grund“ erscheint, und mit Tab durch sie gehen
Expected: Titel und Text der Rückfrage werden gelesen, Feld und Knöpfe haben je einen Namen
3. Auf eine freie Stelle im Kalender klicken und in „Neuer Eintrag“ nacheinander Termin, Gruppe, Pause, Sperre, Zettel und Bereich wählen; in jeder Art mit Tab durchgehen
Expected: Jedes Feld und jeder Knopf hat einen Namen
Closes: W65
Result: ☐

### Slice 38 – Ein gemeinsamer Rückfragedialog im Web
Start: Docker Desktop starten, `cd web && npm install && cd ..`, `dotnet run --project src/AispaStudio.AppHost`, Verwaltung im Browser als Inhaberin anmelden (`inhaberin@demo.aispa.test`)
1. Schließzeiten öffnen, eine Schließzeit ohne abgesagte Termine wählen, „Löschen“ klicken
Expected: Die Rückfrage „Schließzeit löschen?“ hat Innenabstand 24, die Aktion „Löschen“ steht links, „Abbrechen“ rechts und hat den Fokus; Enter bricht ab
2. Esc drücken
Expected: Die Rückfrage schließt, der Fokus steht wieder auf „Löschen“ in der Maske
3. Dieselbe Rückfrage öffnen und „Abbrechen“ klicken, danach noch einmal öffnen und „Löschen“ bestätigen
Expected: Nach „Abbrechen“ steht der Fokus auf „Löschen“; nach dem Bestätigen ist die Schließzeit weg (der Auslöser ist entfernt, der Fokus fällt auf die Seite); in Menüs von Aktionen, Abos und Betriebsinfo kehrt der Fokus nach „Abbrechen“ auf „Weitere Aktionen“ zurück
4. Mit Narrator oder dem Screenreader die Rückfrage öffnen
Expected: Gelesen werden die Rolle „Warnungsdialog“ (alertdialog), der Titel und der Text der Folge
5. In Aktionen eine Aktion archivieren, bei einer Anzahlung „Anzahlung retournieren“ öffnen, bei einer Schließzeit mit Terminen „Termine absagen“ öffnen
Expected: Alle drei Rückfragen sehen gleich aus (Breite 420, bei der Absage 640), „Abbrechen“ hat den Fokus, „Retournieren“ bleibt gesperrt, bis ein Grund steht, in hellem und dunklem Modus
Closes: B2
Result: ☐

### Slice 39 – Fehlende Rückfragen im Web
Start: App starten wie in work/conventions.md, als Inhaberin anmelden, Verwaltung öffnen
1. Kassa öffnen, bei „Gezählter Bestand“ 245,50 eintragen und „Tagesabschluss“ klicken
Expected: Rückfrage „Tagesabschluss mit gezählt € 245,50?“, der Fokus steht auf „Abbrechen“
2. „Abbrechen“ klicken
Expected: Kein Abschluss gespeichert, der Fokus steht wieder auf „Tagesabschluss“
3. Zugänge öffnen, bei einer offenen Einladung „Zurückziehen“ klicken
Expected: Rückfrage „Einladung zurückziehen?“ mit Name und E-Mail
4. Zeiten öffnen, „+ Zeit nachtragen“, Beginn 22:00, Ende 02:00, Grund leer lassen, „Speichern“ klicken
Expected: „Endet am Folgetag“ unter den Zeiten und „Grund angeben.“ am Feld, nichts gespeichert
5. Buchhaltung öffnen, Land AT mit eigener Zuordnung wählen, „Auf Standard zurücksetzen“ klicken
Expected: Rückfrage mit „Die eigene Zuordnung für AT geht verloren.“
Closes: B35, B40, B52, B57
Result: ☐

### Slice 40 – Gemeinsamer Formularbaustein im Web
Start: Docker Desktop starten, `cd web && npm install && cd ..`, `dotnet run --project src/AispaStudio.AppHost`, Verwaltung im Browser als Inhaberin
1. „Aktionen“ öffnen, „Neue Aktion“ wählen, ohne Namen „Speichern“ klicken
Expected: Der Fokus springt ins Feld „Name“, darunter steht der Fehlertext mit Symbol; oben steht die Meldung „Bitte die markierten Felder prüfen“
2. Dasselbe in „Betriebsinformationen“ (Neuer Eintrag, Pflichtfelder leer), in „Schließzeiten“ und in „Kalender-Abos“
Expected: Fokus im ersten ungültigen Feld, Aussehen der Felder und Gruppen wie vorher (Abstand zwischen Überschrift und erstem Feld sowie zwischen den Feldern einer Gruppe auch in Schließzeiten, Raumsperren und Kalender-Abos gleich)
Closes: B9
Result: ☐

### Slice 41 – Farbtoken an einer Stelle im Web
Start: `dotnet run --project src/AispaStudio.AppHost` (Docker Desktop läuft, vorher `cd web && npm install`)
1. Kundenbereich unter `/buchen/` mit hellem Systemmodus öffnen und Hintergrund, Karten und Ränder der Bedienelemente ansehen
Expected: Seitenhintergrund warmes Hellgrau, Karten weiß, Ränder grau-grün wie vor der Slice
Closes: B28, B69 (Farben wie vorher)
Result: ☐
2. Denselben Kundenbereich mit dunklem Systemmodus öffnen
Expected: Seitenhintergrund fast schwarz-grün, Karten dunkelgrau-grün, Text hell; keine helle Fläche
Closes: B69
Result: ☐
3. Lager (Verwaltung, Menüpunkt „Lager“) hell öffnen und Untertitel sowie Hinweise ansehen
Expected: Untertitel und Hinweistext in gedämpftem Grau, wie vor der Slice
Closes: B28
Result: ☐
4. Lager dunkel öffnen
Expected: Untertitel hell gedämpft, Meldung unten dunkle Fläche mit heller Schrift
Closes: B28
Result: ☐

### Slice 42 – Seitenpanel und Dienstplan im Web
Start: Docker Desktop starten, `cd web && npm install && cd ..`, `dotnet run --project src/AispaStudio.AppHost`, Verwaltung im Browser als Inhaberin
1. Dienstplan öffnen, mit Tab zur Tabelle gehen
Expected: Der Fokus erreicht den Rahmen „Dienstplan“ (sichtbarer Fokusring), danach die Zellen
2. Eine Zelle mit Enter öffnen, dann Esc drücken
Expected: Das Panel „Tag planen“ schließt, der Fokus liegt wieder in der Zelle
3. Zelle öffnen, „+ Dienst“ wählen und mit dem Screenreader die Knöpfe lesen
Expected: „Dienst 1 entfernen“, „Dienst 2 entfernen“, „Dienst 2 Beginn“; beim Öffnen liest der Screenreader die Überschrift „Name, Tag“
4. Zeiterfassung öffnen, einen Tag wählen, Esc drücken; dann einen Abschnitt „Entfernen“ (Rückfrage) und dort Esc
Expected: Esc im Panel schließt es und gibt den Fokus an die Zelle zurück; Esc in der Rückfrage schließt nur die Rückfrage
Closes: B48, B49, B51
Result: ☐

### Slice 43 – Fokus und Zustände im Web
Start: Docker Desktop starten, `cd web && npm install && cd ..`, `dotnet run --project src/AispaStudio.AppHost`, Verwaltung im Browser als Inhaberin
1. Nur mit der Tastatur: Tab bis zu den Seitenreitern, mit Pfeil oder Tab zu „Dienstplan“ gehen und Enter drücken, danach einmal Tab
Expected: Nach Enter liegt der Fokus auf der Überschrift „Dienstplan“ (der Screenreader nennt sie); der Reiter „Dienstplan“ gilt als „aktuelle Seite“
2. In „Kassa“ einen Termin in der Liste anwählen
Expected: Der Screenreader liest „Abrechnen: Behandlung Gast“; die gewählte Rechnung und der gewählte Beleg gelten als „aktuell“
3. Als Rezeption einen Anzahlungsbeleg öffnen und mit Tab zu „Stornieren“ gehen
Expected: Der Knopf nimmt den Fokus, der Grund „Anzahlungsbelege storniert nur die Leitung.“ steht sichtbar daneben und wird mitgelesen; Enter öffnet nichts
4. Im Betreiberbereich „Spa sperren“, dann „Abbrechen“; danach „Lösen“, dann „Abbrechen“
Expected: Der Fokus springt ins Feld „Grund“ bzw. auf „Wirklich lösen“ und kehrt beim Abbrechen auf den Auslöser zurück
5. Im Kundenbereich bei „Ihre Angaben“ ohne E-Mail und ohne Häkchen „Verbindlich buchen“ wählen und in der Übersicht einen Link wählen
Expected: Die Übersicht nennt beide Felder als Links; der Link setzt den Fokus ins Feld, die Adresse bleibt unverändert
Closes: B14, B15, B33, B39, B41, B59, B61
Result: ☐

### Slice 44 – Texte und Auswege im Web
Start: `dotnet run --project src/AispaStudio.AppHost` (Docker Desktop läuft, vorher `cd web && npm install`), als Inhaberin anmelden
1. Lohn öffnen, den Monat über **Monat** und **Jahr** wählen und **BMD Lohn** anklicken, solange eine Person ohne Personalnummer Dienste hat
Expected: Keine Datei; eine Meldung nennt die Person und sagt, dass die Nummer unter „Personalnummern“ einzutragen ist
Closes: B54, B55 (manuell: Lohn › Export ohne Personalnummern)
Result: ☐
2. Personalnummer eintragen und **BMD Lohn** noch einmal anklicken
Expected: Der Knopf zeigt kurz „Wird erstellt …“, danach liegt die Datei im Download-Ordner
Closes: B54
Result: ☐
3. Buchhaltung öffnen und **BMD-Import** anklicken, bei laufendem Server mit abgeschaltetem Netz (Entwicklerwerkzeuge, „Offline“)
Expected: Keine leere Datei; Meldung „Der Export wurde nicht erstellt. Prüfen Sie die Verbindung …“
Closes: B54
Result: ☐
4. Kassa öffnen, wenn keine Kasse eingerichtet ist, und **Stammdaten, Zahlungen** anklicken
Expected: Die Stammdaten öffnen beim Reiter „Zahlungen“
Closes: B44
Result: ☐
5. Kalender öffnen und die Kopfzeile ansehen
Expected: Die Knöpfe heißen „Termin anlegen“ und „Paar / Spa-Tag buchen“
Closes: B67
Result: ☐
6. Dienstplan öffnen und die Knöpfe einer Woche ansehen
Expected: Der Knopf heißt „Dienst hinzufügen“
Closes: B67
Result: ☐
7. Im Dienstplan „Veröffentlichen“ anklicken
Expected: Die Meldung danach hat den Knopf „Schließen“ statt „OK“
Closes: B50
Result: ☐
8. Kundenbereich `/buchen/` bei gestopptem Server öffnen, Server starten und **Erneut versuchen** anklicken
Expected: Die Standorte erscheinen ohne Neuladen der Seite
Closes: B60
Result: ☐

### Slice 45 – Tooltip-Baustein im Web
Start: Docker Desktop starten, `cd web && npm install && cd ..`, `dotnet run --project src/AispaStudio.AppHost`, Verwaltung im Browser als Inhaberin
1. Kalender öffnen und mit Tab zum Live-Stand („Live“ oder „Offline“) gehen
Expected: Nach etwa einer Viertelsekunde erscheint ein dunkler Hinweis „Änderungen anderer erscheinen sofort“ bzw. „Keine Live-Verbindung“ unter dem Live-Stand, mit Abstand 4 und ohne Pfeil
2. Esc drücken
Expected: Der Hinweis verschwindet sofort, der Fokus bleibt auf dem Live-Stand und der Kalender bleibt offen
3. Mit der Maus über eine Terminkarte fahren und kurz verweilen
Expected: Nach etwa einer Viertelsekunde erscheint der volle Text der Karte; beim Weiterfahren zur nächsten Karte erscheint deren Hinweis sofort
4. „Stammdaten“, Standorte, Räume: mit Tab zu den Spaltenköpfen „Plätze“, „Bereich“ und „Eintritt“ gehen
Expected: Jeder Kopf zeigt seine Erklärung bei Fokus; der Hinweis bleibt im Fenster und überdeckt den Kopf nicht
5. Lager, Lagerorte: bei ungespeicherten Änderungen „Von Raum … übernehmen“ (gesperrt) mit Tab erreichen
Expected: Der Hinweis „Erst speichern oder verwerfen“ erscheint, obwohl der Knopf gesperrt ist
Closes: B68, B74
Result: ☐

### Slice 46 – Kleinigkeiten Web, zweiter Durchgang
Start: `dotnet run --project src/AispaStudio.AppHost` (Docker Desktop läuft, vorher `cd web && npm install`), als Inhaberin anmelden
1. Kassa öffnen, einen Termin „Abrechnen“, bei „Karte“ 66,5 eintippen und „Rest bar“ drücken
Expected: Das Feld „Bar gegeben“ zeigt den Rest mit zwei Nachkommastellen und Komma (z. B. 12,50)
2. Mit einem Screenreader bei „Bar gegeben“ einen Betrag über dem Rechnungsbetrag eintippen
Expected: „Bar zu zahlen“ und „Rückgeld“ werden als Status angesagt
3. Stammdaten, Reiter „Team“, eine Person öffnen
Expected: Neben „Farbe im Kalender“ steht der Hex-Wert (z. B. #2a9d8f) als Text und folgt der Auswahl; das Fragezeichen neben den Feldern ist mit Maus und Touch leicht zu treffen (24 px)
4. Hilfe öffnen und zu einem Thema mit Bild blättern (z. B. „Rechnung mit Empfänger“)
Expected: Der Text unter dem Bild springt nicht, wenn das Bild nachlädt
5. Eine Seite mit Meldung öffnen (z. B. Schließzeiten, etwas speichern) und 5 Sekunden warten
Expected: Die Meldung blendet kurz aus (etwa eine Zehntelsekunde), statt abrupt zu verschwinden; bei „Animationen aus“ im System verschwindet sie ohne Überblendung
6. Kundenbereich `/buchen/` im Kontrastdesign von Windows (Einstellungen, Barrierefreiheit, Kontrastdesigns) bis zur Zusammenfassung durchklicken
Expected: Die Zusammenfassung des Termins hat eine sichtbare Umrandung
7. Assistenten öffnen, eine Frage stellen
Expected: Vor der Antwort steht „KI“; der Screenreader liest „KI erstellt von der KI“
Closes: B11, B20, B22, B24, B32, B42, B43, B63, B65
Result: ☐

### Slice 47 – Content-Security-Policy für den Kundenbereich
Start: Docker Desktop starten, `docker compose up -d --build`
1. Chrome öffnen, F12 (Entwicklertools), Reiter „Netzwerk“, dann `http://localhost:5173/buchen/` laden und die Anfrage `buchen/` wählen
Expected: Unter den Antwort-Headern steht `Content-Security-Policy-Report-Only` mit `default-src 'self'`
2. Reiter „Konsole“ öffnen, im Kundenbereich Graz Zentrum, eine Behandlung, einen Tag und eine Zeit wählen, die Angaben ausfüllen und verbindlich buchen
Expected: Die Konsole zeigt keine Meldung zu „Content Security Policy“
3. „Zahlung bestätigen (Test)“ wählen, danach „Gutschein schenken“ öffnen und einen Gutschein bis zur Zahlung kaufen
Expected: Die Konsole zeigt weiterhin keine Meldung zu „Content Security Policy“
4. `http://localhost:5173/` (Team-Oberfläche) laden und die Anfrage im Reiter „Netzwerk“ ansehen
Expected: Kein Header `Content-Security-Policy-Report-Only`
Closes: B31
Result: ☐

### Slice 48 – Formulare in Personal, Buchhaltung und Lohn
Start: Docker Desktop starten, `cd web && npm install && cd ..`, `dotnet run --project src/AispaStudio.AppHost`, Verwaltung im Browser als Inhaberin
1. „Personal“ öffnen, „+ Person einladen“ wählen, nur den Namen eintragen und mit Tab zu „Einladungslink erstellen“ gehen
Expected: Neben dem Knopf steht „E-Mail fehlt.“; Enter auf dem Knopf sendet nichts
2. Eine E-Mail-Adresse eintragen und im Feld E-Mail Enter drücken
Expected: Der Einladungslink erscheint, die Einladung steht in der Liste der offenen Einladungen
3. In „Buchhaltung“ unter „Erlöskonten“ Kontonummer und Bezeichnung eintragen und im Feld Enter drücken
Expected: „Erlöskonto … angelegt.“; mit leerer Bezeichnung steht neben dem Knopf „Bezeichnung fehlt.“
4. In „Buchhaltung › Kontenzuordnung“ und in „Lohn › Einstellungen“ in einem Feld Enter drücken
Expected: „Einstellungen gespeichert.“
Closes: B36
Result: ☐

### Slice 49 – fluent2-web über Kalender, Posteingang, Kassa und Assistent
Start: Docker Desktop starten, `cd web && npm install && cd ..`, `dotnet run --project src/AispaStudio.AppHost`, Verwaltung im Browser als Inhaberin
1. Den Kalender nur mit der Tastatur bedienen: Tab durch Leiste und Termine, Enter auf einen Termin, im Panel „Verschieben“ wählen, Zeit eintragen, Esc
Expected: Jedes Element ist erreichbar, der Fokusring ist überall sichtbar, „Live“ oder „Offline“ steht neben den Reitern
2. Im Posteingang eine Nachricht mit Vorschlägen öffnen und einen Vorschlag mit Enter wählen
Expected: Jedes Element ist erreichbar, Zustände (gewählt, gesendet) sind ohne Farbe erkennbar
3. In der Kassa eine Rechnung anlegen, eine Position entfernen und mit „Bar“ abschließen, alles nur mit der Tastatur
Expected: Jedes Element ist erreichbar, Fehler stehen beim Feld
4. Den Begleiter mit Strg+Leertaste öffnen, mit Tab auf „Ausblenden“ gehen; ausgeblendet mit Tab auf den Knopf unten rechts
Expected: Der Tooltip „KI und Hilfe (Strg+Leertaste)“ erscheint auch per Tastaturfokus
5. Einen Termin einer gemeinsamen Buchung mit Screenreader anhören
Expected: Der Knopf nennt „gemeinsam gebucht“
Closes: B84, B85, B86, B87, B88
Result: ☐

### Slice 50 – fluent2-web über Verwaltung, Personal und Stammdaten
Start: Docker Desktop starten, `cd web && npm install && cd ..`, `dotnet run --project src/AispaStudio.AppHost`, Verwaltung im Browser als Inhaberin
1. „Stammdaten“ öffnen und nur mit Tab, Pfeiltasten, Enter und Leertaste durch die Reiter gehen (Standorte, Team, Behandlungen, Firmendaten, Zahlungen); in Standorte und Team die Tabelle mit den Räumen bzw. Personen ansehen
Expected: Jeder Reiter ist erreichbar und jedes Feld, jeder Knopf und jede Liste per Tastatur bedienbar; die letzte Spalte der Tabellen hat einen (nicht sichtbaren) Kopf „Aktionen“, der Screenreader nennt ihn
2. „Personal“ öffnen, ein Mitglied wählen, Felder ändern, mit Tab zu „Speichern“ gehen und Enter drücken
Expected: Gespeichert; kein Feld ist nur mit der Maus erreichbar
3. Eine Einladung öffnen, deren Link abgelaufen ist (oder den Server anhalten) und „Einladung annehmen und Passkey anlegen“ wählen
Expected: Die Fehlermeldung wird vom Screenreader sofort angesagt und steht unter dem Knopf
Closes: B71, B72, B73, B74, B75, B76
Result: ☐

### Slice 51 – fluent2-web über Dienstplan, Zeit, Buchhaltung, Lohn und Berichte
Start: Docker Desktop starten, `cd web && npm install && cd ..`, `dotnet run --project src/AispaStudio.AppHost`, Verwaltung im Browser als Inhaberin
1. „Dienstplan“ öffnen und nur mit Tab, Pfeiltasten, Enter und Esc durch die Wochentabelle gehen; einen Tag öffnen, „Dienst“ ändern, mit Tab bis „Speichern“ und Enter
Expected: Jede Zelle ist erreichbar und wird mit Person, Tag, Zeiten und Warnung angesagt; das Panel öffnet mit Fokus auf der Überschrift und schließt mit Esc zurück auf die Zelle
2. „Zeiterfassung“ öffnen, eine Zelle mit Hinweis wählen und die Tabelle mit dem Screenreader oder per Tab lesen
Expected: Der Hinweis (z. B. „Pause zu kurz“) steht sichtbar in der Zelle und wird mit der Zelle angesagt; Wochenhinweise stehen sichtbar in der letzten Spalte
3. „Berichte“ öffnen und die Reiter, Filter und Tabellen nur mit der Tastatur bedienen
Expected: Reiter, Standortwahl, Zeitraum und Tabellen sind bedienbar, Meldungen werden angesagt
Closes: B77, B78, B79
Result: ☐

### Slice 52 – fluent2-web über Kundenbereich, Mein Tag, Betreiber und Lager
Start: Docker Desktop starten, `cd web && npm install && cd ..`, `dotnet run --project src/AispaStudio.AppHost`, Kundenbereich im Browser
1. Im Kundenbereich nur mit Tab, Pfeiltasten, Leertaste und Enter buchen: Leistung wählen, Tag und Zeit wählen, Angaben eintragen, „Verbindlich buchen“; einmal mit leerer E-Mail
Expected: Alle Schritte sind erreichbar, der Fokusring ist überall sichtbar, der Fehler nennt das Feld und der Link in der Übersicht springt dorthin
2. Dasselbe im Windows-Kontrastdesign (Einstellungen, Barrierefreiheit, Kontrastdesigns) mit Blick auf die gewählte Zeit
Expected: Die gewählte Zeit ist erkennbar (offen: B82)
3. Im Betreiberbereich „Spa sperren“ öffnen, einen Grund eintragen und einen Fehler auslösen (Server anhalten), „Wirklich sperren“ wählen
Expected: Die Meldung erscheint unter dem Formular, das Feld „Grund“ gilt als fehlerhaft und der Screenreader liest die Meldung beim Feld mit
Closes: B80, B81, B82, B83
Result: ☐

### Slice 53 – Hilfe: Seitenaufbau, Navigation und Typografie
Start: Docker Desktop starten, `cd web && npm install && cd ..`, `dotnet run --project src/AispaStudio.AppHost`; Windows-App über die Ressource `windows-app` als Inhaberin; Verwaltung im Browser als Inhaberin
1. In der Windows-App die Hilfe öffnen (Fragezeichen oder Strg+Leertaste) und „Termin verschieben“ wählen
Expected: Über dem Titel stehen die Brotkrumen „Hilfe › Kalender › Termin verschieben“, darunter „Für: …“ und der Kasten „Auf dieser Seite“ mit allen Zwischenüberschriften; am Ende „Verwandte Themen“ und „Zurück zur Übersicht“
2. Einen Eintrag unter „Auf dieser Seite“ anklicken
Expected: Die Seite rollt zum Abschnitt, die Überschrift ist eingefärbt
3. In der Suche „Kasse“ eingeben
Expected: Die Statuszeile nennt die Zahl; jeder Treffer zeigt oben seinen Bereich und das Wort „Kasse“ bzw. „Kassa“ ist gelb hinterlegt; bei „qzxwvu“ stehen die Bereiche und „Alle Themen anzeigen“ da
4. Dasselbe in der Verwaltung im Browser (`/help/?thema=kalender-termin-verschieben`)
Expected: Gleiche Seite, gleiche Bausteine; Bilder haben Rahmen und Unterschrift
Closes: Ä76 (AC-04, AC-05, AC-06)
Result: ☐

### Slice 54 – Hilfe: Muster für Abläufe und Einstieg je Rolle
Start: Docker Desktop starten, `cd web && npm install && cd ..`, `dotnet run --project src/AispaStudio.AppHost`; Windows-App über die Ressource `windows-app` als Rezeption (`empfang.wien@demo.aispa.test`)
1. In der Windows-App die Hilfe öffnen (Fragezeichen oder Strg+Leertaste) und die Übersicht ansehen
Expected: Über den Bereichen steht „Erste Schritte“ mit je einer Gruppe für Rezeption, Therapeut:in, Leitung und Inhaber:in, jede mit mindestens drei Abläufen; die Gruppe der angemeldeten Rolle steht zuerst
2. In der Gruppe „Rezeption“ „Termin buchen“ öffnen und den Schritten folgen
Expected: Die Seite hat „Voraussetzungen“, „Schritte“ (1 bis 4), „Was danach“ und „Wenn etwas schiefgeht“; jeder Schritt hat ein Bild mit orangefarbenem Rahmen um das Element, das der Schritt nennt, und der Nummer des Schritts
3. Dasselbe mit „Beleg stornieren“ und „Tagesabschluss“ in der Kassa
Expected: Die Markierung sitzt bei „Belege“, der Belegliste, dem Feld für den Grund und dem Knopf „Stornieren“ bzw. bei „Kassenlade & Abschluss“, dem Zwischenbericht, dem Feld „Gezählter Bestand“ und dem Knopf „Tagesabschluss durchführen“
4. Eine Frage an die KI, z. B. „Wie verschiebe ich einen Termin?“
Expected: Die Antwort verweist auf „Termin verschieben“; der Kurzhinweis nennt den ersten Schritt („Den Termin mit der Maus fassen …“)
Closes: Ä76 (AC-01, AC-02, AC-03)
Result: ☐

### Slice 55 – Hilfe: Rückmeldung je Thema
Start: Docker Desktop starten, `cd web && npm install && cd ..`, `dotnet run --project src/AispaStudio.AppHost`; Windows-App über die Ressource `windows-app` als Inhaberin (`inhaberin@demo.aispa.test`) anmelden
1. In der Windows-App die Hilfe öffnen, „Termin verschieben“ wählen und ans Ende des Themas rollen
Expected: Über „Zurück zur Übersicht“ steht „War das hilfreich?“ mit den Knöpfen „Ja“ und „Nein“
2. „Nein“ anklicken, ins Feld „Möchten Sie etwas ergänzen?“ einen Satz schreiben und „Senden“ anklicken
Expected: „Danke für die Rückmeldung.“ erscheint, die Frage ist weg
3. Zu „Alle Themen“ gehen und „Termin verschieben“ erneut öffnen; danach die App schließen, neu starten und das Thema noch einmal öffnen
Expected: Die Frage „War das hilfreich?“ steht nicht noch einmal da, auch nach dem Neustart; bei einem anderen Thema steht sie wieder
Closes: Ä76 (Ruling zur Rückmeldung)
Result: ☐

### Slice 56 – Hilfe: unbeantwortete Fragen an die KI für die Leitung
Start: Docker Desktop starten, `cd web && npm install && cd ..`, `dotnet run --project src/AispaStudio.AppHost`, Windows-App über die Ressource `windows-app`, als Inhaberin anmelden
1. Strg+Leertaste drücken und im Assistenten „Wie färbe ich die Wolken lila?“ fragen
Expected: Der Assistent antwortet „Dazu habe ich nichts gefunden …“
2. Im Assistenten „Wie färbe ich die Wolken für <Nachname eines Gastes aus dem Kalender> lila?“ fragen
Expected: Auch hier „Dazu habe ich nichts gefunden …“
3. Seite „Hilfe“ öffnen und auf „Offene Fragen an die Hilfe (2)“ klicken
Expected: Die Liste zeigt beide Fragen mit Seite „Kalender“ und dem heutigen Datum; in der zweiten steht „[Name]“ statt des Nachnamens
4. Bei der ersten Frage „Erledigt“ klicken
Expected: Die Frage verschwindet, die Leiste zeigt „Offene Fragen an die Hilfe (1)“
5. Abmelden und als Rezeption anmelden (`empfang.wien@demo.aispa.test`), Seite „Hilfe“ öffnen
Expected: Über der Hilfe steht keine Leiste „Offene Fragen an die Hilfe“
Closes: Slice 56 (Ruling zur Pflege der Hilfe, Brief Ä76)
Result: ☐
