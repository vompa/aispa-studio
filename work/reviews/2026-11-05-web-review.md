# Durchsicht aller Web-Oberflächen mit `impeccable` und `emil-design-eng`

Stand: 05.11.2026. Plan: [work/plans/2026-11-05-web-review.md](../plans/2026-11-05-web-review.md). Die Durchsicht hat keinen Code geändert. Was davon umgesetzt wird, entscheidet der Auftraggeber.

## Umfang und Grundlage

- Sechs Oberflächen: W1 Kundenbereich, W2 Mein Tag, W3 Dashboard, W4 Hilfe, W5 Anmelden, W6 Betreiberseite.
- 222 Aufnahmen mit headless Chrome (hell und dunkel; 390 px für W1 und W2, 1920 × 1080, 1366 × 768 und Hochformat für W3, 1280 × 800 für W4 bis W6) gegen Demo-Daten. Die Aufnahmen liegen nicht im Repository.
- Je Oberfläche ein Agent (Sonnet), der beide Skills geladen, Brief und Hausregeln gelesen und Quelltext und Aufnahmen verglichen hat.
- **Geprüft im Hauptgespräch** (an Quelltext oder Aufnahme nachgesehen): W1-03, W1-06, W1-07, W1-12, W2-03, W2-05, W2-08, W2-11, W3-01, W4-02, W4-04, W5-02, W5-04, W6-06. Alle stimmen. Die übrigen Befunde stammen aus den Agenten und tragen ihre Fundstelle. Zeilennummern können um einige Zeilen abweichen.
- Nicht Teil der Durchsicht: Technik nach `modern-web-guidance` (CSP, INP, LCP), Verhalten nach `fluent2-web` (Tastatur, ARIA), die Windows-App.

## Zahlen

| Oberfläche | blockiert | stört | Feinschliff | gesamt |
| --- | --- | --- | --- | --- |
| W1 Kundenbereich | 0 | 10 | 14 | 24 |
| W2 Mein Tag | 0 | 9 | 9 | 18 |
| W3 Dashboard | 2 | 6 | 5 | 13 |
| W4 Hilfe | 0 | 5 | 8 | 13 |
| W5 Anmelden | 0 | 5 | 11 | 16 |
| W6 Betreiberseite | 0 | 6 | 7 | 13 |
| **Summe** | **2** | **41** | **54** | **97** |

## Die zehn wichtigsten Punkte

1. **Dashboard schneidet Inhalt unsichtbar ab (W3-01, W3-02, blockiert).** `body { overflow: hidden }` bei `.screen { min-height: 100vh }`: „Nächste Termine“ endet bei 1920 × 1080 nach 5 von 8 Einträgen mitten in der Zeile, bei 1366 × 768 fehlt das Feld ganz. Mehr als etwa 9 Personen im Dienst passen nicht, der Rest fehlt ohne Hinweis.
2. **„Offene Fragen an die Hilfe“ gibt es nur im Code (W4-02).** `HelpDrawer` und `OpenQuestions` werden nirgends angezeigt, `recordUnanswered` hat keinen Aufrufer. Das Hilfethema `ki-assistent-fragen` beschreibt die Funktion trotzdem.
3. **Mein Tag lässt Inhaberin, Leitung und Rezeption ohne Weg stehen (W2-08).** Ohne verknüpfte Person steht nur „…an die Standortleitung wenden“ (für die Inhaberin falsch), dazu ein wirkungsloses „Erneut versuchen“ und kein Abmelden.
4. **Fehlermeldungen führen in die Irre (W1-06, W1-07, W5-03, W6-02).** „Bitte rufen Sie uns an“ ohne Nummer; jeder Serverfehler beim Code-Anfordern meldet „gültige E-Mail-Adresse eingeben“; ein Serverausfall beim Anmelden wird dem Passkey angelastet; ein falscher Betreiber-Schlüssel leert das Feld.
5. **Auf dem Handy liegt die Hauptsache außer Sicht (W1-01, W1-02, W1-08, W2-01, W2-02).** Kopf des Kundenbereichs ca. 220 px; Uhrzeiten und „Weiter“ unter dem Tagesraster; 30 flache Behandlungskarten (3.700 px); in Mein Tag stehen ca. 600 px vor der ersten Karte, und der Raumcheck belegt ein Drittel der Seite.
6. **Zeitkritisches steht unten (W1-04, W1-05).** Die offene Anzahlung mit Frist („sonst geben wir den Termin frei“) steht in „Termin verwalten“ ca. 1.250 px tief; „Gesundheitsfragebogen fehlt noch“ ohne Link.
7. **Hinweise und Fehler folgen nicht Brief 5.9 (W2-03, W2-12, W5-01, W5-04, W6-01).** Warnungen rot, Erfolg in Warngelb, Fehler als ungestalteter Text, Rückmeldung weit weg vom Auslöser. Eine gemeinsame Hinweis-Komponente für die schlanken Oberflächen würde alle Stellen abdecken.
8. **Gefährliche Aktionen sehen aus wie harmlose (W6-04, W1-20).** „Spa sperren“ als Akzent-Rahmenknopf, Bestätigung inline rot gefüllt statt im Rückfragedialog; „Termin stornieren“ gleich wie „Zum Kalender hinzufügen“.
9. **Hilfe verletzt Brief 5.30 (W4-01, W4-04, W4-05).** Suchtreffer stehen nebeneinander in der schmalen Spalte (Auszug über 14 Zeilen), Fließtext mit ca. 100 statt höchstens 72 Zeichen je Zeile, unter 720 px steht die ganze Navigation vor dem Thema.
10. **Keine Rückmeldung bei Hover und Druck (W1-12, W2-11, W5-11, W6-11).** In den schlanken Oberflächen gibt es keine `:active`- oder Übergangsregeln; Brief 4.6 verlangt Hover in 100 ms und Drücken mit Skalierung 0,97.

## Entscheidungen für den Auftraggeber

1. **Brief oder eigene Werte für die schlanken Oberflächen (W1-11, W2-14, W3-13).** W1, W2, W3, W5 und W6 nutzen `web/src/tokens.css` (Akzent `#2a6354`, Gewicht 700, rem, eigene Größen). Der Brief sagt in Abschnitt 0, der Kundenbereich nutze dieselben Token. Entweder den Brief um eine Variante für Handy und Tafel ergänzen oder die Werte angleichen. Bis dahin sollten keine Einzelwerte angeglichen werden.
2. **Offene Fragen an die Hilfe (W4-02):** anbinden oder Handbuch und Verzeichnis anpassen.
3. **Rückmeldung zur Hilfe (W4-03):** zentral sammeln oder ehrlich sagen, dass sie nur auf diesem PC liegt.
4. **Dashboard (W3-02, W3-06, W3-07):** echte Bildschirmgröße und Ausrichtung, Verhalten bei vielen Personen (verdichten, rotieren oder nur Dienst jetzt), Feierabend-Zustand und Schutz gegen Einbrennen.
5. **Raumcheck in Mein Tag (W2-01):** Sammelaktion „Alle reichen noch“ ja oder nein.

## Nebenbefunde aus den Aufnahmen

- Die Testkonten Inhaberin, `leitung.wien`, `manager` und `empfang.wien` sind keiner Person im Team verknüpft (Folge: W2-08).
- An vielen Tagen der Demo-Daten gibt es im Kundenbereich keine freien Zeiten.
- `w5-signin-error-*` sind leer (Aufnahme fehlgeschlagen); der Zustand ist nicht beurteilt.

---

## W1 Kundenbereich (`/buchen/`, `web/src/booking/`)

| ID | Befund | Fundstelle | Schwere | Skill | Vorschlag | Aufwand | Brief |
| --- | --- | --- | --- | --- | --- | --- | --- |
| W1-01 | Kopfzeile 4 Zeilen, ca. 220 px, Inhalt ab y = 245 | BookingApp.tsx:94-148, booking.css:69-101; w1-book-location-hell | stört | imp | eine Zeile, Kopf höchstens 100 px | mittel | passt |
| W1-02 | Tagesraster füllt den ersten Bildschirm, Uhrzeiten und „Weiter“ darunter; Zeitzonenhinweis immer | BookPage.tsx:560-650, booking.css:245-298; w1-book-slots-times-hell | stört | imp, emil | Tage als wischbare Zeile, Zonenhinweis nur bei Abweichung, „Weiter“ fest unten | mittel | passt |
| W1-03 | „Ändern“ bricht im Wort um („Än/dern“) | BookPage.tsx:306-318, booking.css:161-168; w1-book-details-320-hell | stört | imp | `flex: none; white-space: nowrap`, 44 px | klein | passt |
| W1-04 | Offene Anzahlung mit Frist ca. 1.250 px unten, viele gleichrangige Knöpfe | ManagePage.tsx:35-72, Deposit.tsx; w1-move-dunkel | stört | imp | Hinweis mit Frist unter den Eckdaten, eine Hauptaktion | mittel | passt (5.1) |
| W1-05 | „Gesundheitsfragebogen fehlt noch“ ohne Link | MyAppointmentsPage.tsx:278-286, ManagePage.tsx:36-60; w1-manage-hell | stört | imp | Link zeigen oder auf die Mail verweisen | klein–mittel | passt |
| W1-06 | „Bitte rufen Sie uns an“ ohne Telefonnummer | i18n.tsx:41, 73; BookPage.tsx:783; w1-book-confirmed-dunkel | stört | imp | Nummer des Standorts als `tel:`-Link | klein | passt |
| W1-07 | Code anfordern: jeder Serverfehler meldet „gültige E-Mail-Adresse eingeben“ | SignInPage.tsx:50-52 (nur Quelltext) | stört | imp | Adressmeldung nur bei 400, sonst allgemein, 429 mit Wartezeit | klein | passt |
| W1-08 | 30 flache Behandlungskarten, Kategorie in jeder Zeile, Seite 3.711 px | BookPage.tsx:215-235, offer.ts:26; w1-book-treatment-full-hell | stört | imp | nach Kategorie gruppieren, Preis absetzen, „Weiter“ fest unten | mittel | passt (R1, R10) |
| W1-09 | Pflicht und freiwillig beide gekennzeichnet, „(Pflichtfeld)“ an allen 7 Anamnesefragen, Fehlertext ohne Weg, Fehlerzusammenfassung laut | components.tsx:98-129, BookPage.tsx:775-800, i18n.tsx:67-79; w1-book-details-errors-hell | stört | imp | eine Seite kennzeichnen, Fehler mit Weg, ruhigere Zusammenfassung | klein–mittel | passt (R6-R8, 5.2) |
| W1-10 | Frühere Termine als volle Karten mit Route und Anreise, Seite 6.540 px | MyAppointmentsPage.tsx:179-190, components.tsx:157-185; w1-mine-signedin-dunkel | stört | imp | frühere Termine als kompakte Zeilen | mittel | passt |
| W1-11 | Palette, Gewicht 700, rem, Titelgrößen, Feldradius weichen vom Brief ab | tokens.css:6-21, booking.css | Feinschliff | imp | Entscheidung des Auftraggebers (siehe oben) | mittel | weicht ab (0, 2, 3, 4.3, 5.1, 10) |
| W1-12 | Kein Hover, kein Drücken, kein Übergang | booking.css:192-243, 251-261, 383-426 | Feinschliff | emil | `:active` 0,97, 100 ms, Hover nur bei `hover: hover` | klein | passt (4.6) |
| W1-13 | Ladetext eingerückt, Fußzeile springt | components.tsx:50-64, booking.css:428-433; w1-book-loading-hell | Feinschliff | emil | Mindesthöhe, ruhiger Platzhalter | klein | passt (5.25) |
| W1-14 | „Frühere Tage“ blass deaktiviert (dunkel ca. 2:1), Tage ohne Zeiten wählbar | BookPage.tsx:605-640, booking.css:422-426 | Feinschliff | imp, emil | Knopf nur bei möglicher Richtung, leere Tage nicht wählbar | klein | passt |
| W1-15 | Schritt 4 zeigt die Auswahl doppelt, Formular erst ab y = 340 | BookPage.tsx:161-185, 862-875 | Feinschliff | imp | eine Zusammenfassung oben | mittel | passt |
| W1-16 | Gewählter Tag und Zeit nur durch Rand, dunkles Optionsfeld wirkt gewählt | booking.css:210-222, 263-273; w1-book-location-dunkel | Feinschliff | imp | Haken, getönte Fläche | klein–mittel | passt (5.1, 9) |
| W1-17 | Bestätigung: Mailadresse bricht im Wort, Datum fehlt in der Meldung | components.tsx:50-64, BookPage.tsx:903-913 | Feinschliff | imp | Datum in die Meldung, Umbruch an @ und Punkt | klein | passt |
| W1-18 | Anamnese: Linie läuft durch die Legende, kein Fortschritt | AnamnesisPage.tsx:158-165, booking.css:517-520 | Feinschliff | imp | Linie am Wrapper, „Frage 3 von 7“ | klein | passt |
| W1-19 | Marke „Online-Buchung“ statt Name des Spas | i18n.tsx:10, BookingApp.tsx:96-98 | Feinschliff | imp | Name und Logo aus den Standortdaten | mittel | passt |
| W1-20 | „Termin stornieren“ sieht aus wie „Zum Kalender hinzufügen“ | MyAppointmentsPage.tsx:322-336, booking.css:400-410 | Feinschliff | imp, emil | Danger-Outline, abgesetzt | klein | passt (5.1) |
| W1-21 | Wartezustände ohne Text, `disabled` verliert den Fokus | MyAppointmentsPage.tsx:231-246, CardGuarantee.tsx:84-105 | Feinschliff | emil | „Wird gespeichert …“, `aria-disabled` | klein | passt |
| W1-22 | Kopf zeigt beim Laden „Anmelden“ und springt dann (nur Quelltext) | BookingApp.tsx:116-134, session.tsx:15-47 | Feinschliff | emil | Platzhalter gleicher Breite | klein | passt |
| W1-23 | Gutschein: „Für (freiwillig)“ unklar, Testhinweis oben, Betragskarten 3 + 2 | i18n.tsx:135-137, Vouchers.tsx; w1-voucher-hell | Feinschliff | imp | klarere Beschriftung, Längenhinweis | klein | passt (R6) |
| W1-24 | „Meine Termine“ abgemeldet ohne Erklärsatz | MyAppointmentsPage.tsx:52-62; w1-mine-signedout-hell | Feinschliff | imp | ein Satz, „Anmelden“ primär | klein | passt |

Gut und zu erhalten: 44-px-Flächen, sichtbare Beschriftungen, 320 px ohne seitliches Scrollen; Fokus auf die Überschrift nach Schrittwechsel; Fehlerzusammenfassung mit Sprunglinks; vollständiger Dunkelmodus; Zeiten in der Zeitzone des Standorts; zweistufiges Stornieren mit Gebührenhinweis.

Nicht beurteilt: Gutschein-Bestellung, Anzahlung, Kartengarantie, Warteliste-Formular, eigene Gutscheine und eingebettete Darstellung nur aus dem Quelltext; Französisch und Italienisch; echte Geräte.

## W2 Mein Tag (`/mein-tag/`, `web/src/myday/`)

| ID | Befund | Fundstelle | Schwere | Skill | Vorschlag | Aufwand | Brief |
| --- | --- | --- | --- | --- | --- | --- | --- |
| W2-01 | Raumcheck mit 9 Zeilen belegt ca. 1.400 von 4.243 px, zwei 48-px-Knöpfe untereinander | PlanCard.tsx:211-252, myday.css:153 | stört | imp, emil | Knöpfe nebeneinander, einklappbar mit Zähler | mittel | passt |
| W2-02 | „Läuft gerade“/„Als Nächstes“ nicht im Blick, ca. 600 px Steuerung davor | MyDayApp.tsx:335-405; w2-day-therapeutin-hell | stört | imp, emil | zur markierten Karte scrollen, Abgeschlossenes einklappen | mittel | passt |
| W2-03 | Raumwechsel und Zertifikat-Erinnerung rot, ohne Symbol | myday.css:300, Reminders.tsx:30 | stört | imp | Hinweisfläche nach 5.9 | klein | weicht ab (5.9) |
| W2-04 | Sechs gleiche Umrissknöpfe als Ansichtsschalter, gewählter wie Primärknopf, keine `tablist` | MyDayApp.tsx:329-335, myday.css:421-460; w2-week-hell | stört | imp, emil | Schiene nach 5.4 | mittel | weicht ab (5.4) |
| W2-05 | Woche: „Heute“ nicht erkennbar | myday.css:101, 487-489 | stört | imp | Balken oder Pille „Heute“ | klein | passt |
| W2-06 | Inventur leer ohne Erklärung, „Abschicken“ ohne Grund deaktiviert | StockCountPanel.tsx:252-306; w2-count-running-hell | stört | imp | Leerzustand mit Weg, Grund am Knopf | klein | weicht ab (5) |
| W2-07 | Vorlieben und „Vom letzten Mal“ doppelt unter „Vorbereitung“ | PlanCard.tsx:242-301; w2-day-past-therapeutin-hell | stört | imp | Doppelung entfernen (Ursache evtl. API) | klein | passt |
| W2-08 | Konto ohne verknüpfte Person: roter Satz, wirkungsloses „Erneut versuchen“, kein Abmelden | MyDayApp.tsx:78, 239-251; w2-day-owner-unlinked-* | stört | imp | eigener Zustand mit Erklärung, Weg und Abmelden | klein | passt |
| W2-09 | Laden und Fehler: nur „Lade …“ bzw. roter Satz, 700 px leer | MyDayApp.tsx:239-271; w2-day-loading-dunkel, w2-day-error-hell | stört | emil, imp | Gerüst, Platzhalter ab 1 s, `aria-busy` | mittel | weicht ab (5.25) |
| W2-10 | Kopfleiste bricht in zwei Zeilen, ca. 80 px, fest oben | myday.css:41-60, MyDayApp.tsx:304-323 | Feinschliff | imp, emil | Konto-Menü | mittel | passt |
| W2-11 | Kein Hover, kein Drücken, kein Übergang | myday.css:101-130 | Feinschliff | emil | wie W1-12 | klein | weicht ab (4.6) |
| W2-12 | Rückmeldungen am Kartenende statt am Auslöser, Erfolg bleibt stehen | PlanCard.tsx, AbsencePanel.tsx:118, ReceivePanel.tsx:90, StockCountPanel.tsx:285 | Feinschliff | imp, emil | am Auslöser, Erfolg nach 5 s ausblenden | klein | weicht ab (5.9, 5.10) |
| W2-13 | Abwesenheit: toter Abstand über „Art“, „Meine Anträge“ klebt am Panel | AbsencePanel.tsx:80-120, myday.css:71, 223 | Feinschliff | imp | erstes Feld ohne Rand oben, 20 px vor h3 | klein | weicht ab (4.1) |
| W2-14 | Typografie und Palette (siehe Entscheidung 1) | myday.css:65-82, 177-186, tokens.css:8-37 | Feinschliff | imp | Entscheidung des Auftraggebers | mittel | weicht ab (3, 4.1, 10) |
| W2-15 | Blauer 3-px-Fokusring statt Brief-Ring | myday.css:36-39, tokens.css | Feinschliff | emil | mit Entscheidung 1 klären | klein | weicht ab (4.5) |
| W2-16 | „Aufgefüllt“ und „Reicht noch“ praktisch gleich | PlanCard.tsx:216-246, myday.css:123-129 | Feinschliff | imp | Aufgefüllt getönt, Reicht noch Ghost | klein | weicht ab (5.1) |
| W2-17 | „Heute im Spa“: sechs gleichrangige Zeilen, Umsatz klein | Today.tsx:68-86, myday.css:432-455 | Feinschliff | imp | Kachelraster, Umsatz zuerst | klein | weicht ab (5.5) |
| W2-18 | Anmelden: umständlicher Satz, Link klebt am Knopf | SignIn.tsx:25-39 | Feinschliff | imp | Ghost-Knopf, kürzerer Satz | klein | passt |

Gut und zu erhalten: Zielgrößen 48 px, Felder 44 px, 320 px ohne seitliches Scrollen; optimistisches Abhaken mit Rücknahme; Offline-Zustand im Quelltext durchdacht; Karteninhalt folgt dem Ablauf im Raum.

Nicht beurteilt: Barcode-Scan, Wareneingang nach Scan, Inventur-Bestätigung, Offline nur aus dem Quelltext.

## W3 Dashboard (`/dashboard/`, `web/src/dashboard/`)

Der Brief hat keinen Abschnitt zur Tafel. Maßstab ist der Kommentar in `dashboard.css:1` („lesbar aus 3 m, Schrift ab 28 px“).

| ID | Befund | Fundstelle | Schwere | Skill | Vorschlag | Aufwand | Brief |
| --- | --- | --- | --- | --- | --- | --- | --- |
| W3-01 | Seite wächst über 100vh, Überlauf abgeschnitten: „Nächste Termine“ nach 5 von 8, bei 1366 × 768 ganz weg | dashboard.css:19, 34-40, 106-114; w3-board-hell, w3-board-klein-* | blockiert | imp | `height: 100vh` mit Grid, nur ganze Einträge | mittel | nicht geregelt |
| W3-02 | Ab ca. 9 Personen fehlen die übrigen ohne Hinweis | dashboard.css:143-146, Board.tsx:68-86 (Maßrechnung) | blockiert | imp | verdichten, nur Dienst jetzt/bald oder Seiten mit „2 von 3“ | mittel–groß | nicht geregelt |
| W3-03 | Status, Stundenskala, Raum unter 28 px; Basis skaliert nur nach Breite | dashboard.css:7-9, 154-158, 177-184, 245-248 | stört | imp | Untergrenze 28 px, `min(vw, vh)` | klein | nicht geregelt |
| W3-04 | Zustand je Person am kleinsten und blassesten, „frei“ fünfmal | Board.tsx:71-74, dashboard.css:148-163 | stört | imp | „frei“ weglassen, Zustand als Chip | klein–mittel | nicht geregelt |
| W3-05 | Verbindung weg: „letzter Stand“ ohne Uhrzeit, Uhr läuft weiter, Streifen schiebt das Layout | DashboardApp.tsx:77-79, 117; w3-board-error-hell | stört | imp | „Stand 14:32“ im Kopf, alte Zustände dämpfen | klein–mittel | nicht geregelt |
| W3-06 | 1366 × 768 und Hochformat brechen Angaben um, Text ragt aus der Karte | dashboard.css:106-114, 212-248; w3-board-hochkant-dunkel | stört | imp | `nowrap`, Regel für Hochformat | mittel | nicht geregelt |
| W3-07 | Dauerbetrieb ohne Abblendung nach Dienstschluss, Einbrennen möglich (Annahme) | dashboard.css:11-20, live.ts:4 | stört | imp, emil | Feierabend-Zustand, dunkel als Standard | mittel | nicht geregelt |
| W3-08 | Dienstzeit kaum von Freizeit zu unterscheiden, vergangene Termine gleich stark | dashboard.css:165-201 | stört | imp | Vergangenes dämpfen, Dienst tönen | klein–mittel | nicht geregelt |
| W3-09 | Kopplungscode wird still ersetzt, keine Gültigkeit genannt | Pairing.tsx:36-49, 61 | Feinschliff | imp | Gültigkeit nennen, nächsten Schritt nennen | klein | nicht geregelt |
| W3-10 | Übergänge animieren `left`/`width` mit `ease`, Uhr hinkt bis 10 s | dashboard.css:267-275, DashboardApp.tsx:13, 96 | Feinschliff | emil | `transform`, `ease-out`, Takt auf die Minute | klein | nicht geregelt |
| W3-11 | Lange Namen ohne Umbruch, doppelte React-Keys, überlappende Termine verdecken sich (nur Quelltext) | dashboard.css:135-141, Board.tsx:70-81 | Feinschliff | imp | Umbruch, Keys aus Id, Halbspuren | klein–mittel | nicht geregelt |
| W3-12 | Räume ohne Leerzustand, „nicht im Dienst“ unter „Heute im Dienst“, kein Feierabend | Board.tsx:11-15, 55, 89-100 | Feinschliff | imp | Leerzustand, Überschrift anpassen | klein | nicht geregelt |
| W3-13 | Palette der schlanken Oberflächen (siehe Entscheidung 1) | tokens.css:1-19 | Feinschliff | imp | Entscheidung des Auftraggebers | klein | weicht ab (2) |

Gut und zu erhalten: Raumbelegung auf einen Blick; Kopplungsbildschirm passt für die Entfernung; keine Dauerbewegung, `prefers-reduced-motion` beachtet; alle Zustände vorhanden, Neuverbinden automatisch.

## W4 Hilfe (`/help/`, `web/src/help/`)

| ID | Befund | Fundstelle | Schwere | Skill | Vorschlag | Aufwand | Brief |
| --- | --- | --- | --- | --- | --- | --- | --- |
| W4-01 | Suchtreffer: Bereich, Titel, Auszug nebeneinander, Auszug über 14 Zeilen | HelpIndex.tsx:160-173, index.css:2907-2912; w4-search-hits-hell | stört | imp | Spalte, Auszug 2 Zeilen | klein | verletzt (5.30) |
| W4-02 | `HelpDrawer`/`OpenQuestions` nirgends angezeigt, `recordUnanswered` ohne Aufrufer; Handbuch beschreibt die Funktion | HelpDrawer.tsx:167-269, OpenQuestions.tsx, assistant/unanswered.ts:49 | stört | imp | Entscheidung 2; toten Code melden | klein–mittel | Entscheidung |
| W4-03 | Rückmeldung nur in `localStorage`, der Dank sagt das nicht | feedback.ts:2-4, 35-42, HelpFeedback.tsx:25 | stört | imp | Entscheidung 3 | klein | – |
| W4-04 | Fließtext ca. 100 Zeichen je Zeile | helpPage.css:164-167; w4-topic-long-dunkel | stört | imp | Text 72ch, Bilder bis 900 px | klein | verletzt (5.30) |
| W4-05 | Unter 720 px steht die Navigation (ca. 1.400 px) vor dem Thema, Klick scrollt nicht | HelpApp.tsx:107-114, helpPage.css:306-316; w4-topic-image-390-hell | stört | imp | Navigation einklappen, zum Thema scrollen | mittel | passt |
| W4-06 | Einstiege, Rollenraster, verwandte Themen als zentrierte Rahmenknöpfe statt Links | helpPage.css, index.css:189-198; w4-index-* | Feinschliff | imp | linksbündige Links | mittel | passt (5.6) |
| W4-07 | Brotkrumen „Hilfe“ als Chip mit Rahmen | index.css:2809-2814 | Feinschliff | imp | ohne Rahmen und Fläche | klein | passt |
| W4-08 | Gruppenkopf (12 px, blass) kleiner als die Einträge (14 px) | helpPage.css:55-64, 94-107 | Feinschliff | imp | Kopf 14/600 | klein | weicht ab (3) |
| W4-09 | Gewählte Antwort nur über Rahmenfarbe, Datenschutzhinweis als Feldname | HelpFeedback.tsx:32-37; w4-feedback-open-hell | Feinschliff | emil | Fläche und Haken, `aria-describedby` | klein | passt |
| W4-10 | „Wird geladen …“ doppelt, Layoutsprung | HelpApp.tsx:132-137; w4-loading-hell | Feinschliff | imp | ein Hinweis, Platzhalter | klein | passt |
| W4-11 | „Thema nicht gefunden“ eingerückt, nennt die Adresskennung | HelpApp.tsx:156-171; w4-topic-unknown-hell | Feinschliff | imp | Kennung weglassen, auf Suche verweisen | klein | passt |
| W4-12 | „Nichts gefunden“ listet 26 Bereiche als nicht klickbaren Text | HelpIndex.tsx:177-184; w4-search-empty-dunkel | Feinschliff | imp | Bereiche als Links | klein | weicht ab (5.30) |
| W4-13 | Suche scrollt weg, aktueller Eintrag nicht im Blick (nur Quelltext) | HelpApp.tsx:107-114, helpPage.css:11-16 | Feinschliff | emil | Suche fest oben, Eintrag in den Blick holen | klein | passt |

Gut und zu erhalten: `role="status"`, `aria-current`, Fokus auf den Dank; nummerierte Schritte, Bilder mit Unterschrift, mitlaufendes Seitenverzeichnis; der Stand steht in der Adresse.

Nicht beurteilt: Darstellung im WebView2 selbst, `<mark>` im Dunkelmodus, Inhalte nur in Stichproben.

## W5 Anmelden (`/anmelden/`, `web/src/signin/`)

| ID | Befund | Fundstelle | Schwere | Skill | Vorschlag | Aufwand | Brief |
| --- | --- | --- | --- | --- | --- | --- | --- |
| W5-01 | Fehler als ungestalteter Text (abgelaufene Einladung, Registrierung, falsches Passwort) | SignupForm.tsx:104, PasswordLogin.tsx:90, InvitationPage.tsx:46-52; w5-invitation-invalid-hell | stört | imp | gemeinsame Hinweis-Komponente nach 5.9 | klein | weicht ab (5.9) |
| W5-02 | Ungültige Einladung ist eine Sackgasse, kein Weg zur Anmeldung | InvitationPage.tsx:45-52, SignInApp.tsx:136-146 | stört | imp | „Zur Anmeldung“ | klein | passt |
| W5-03 | Server nicht erreichbar: normale Anmeldung, nach Klick wird der Passkey beschuldigt | SignInApp.tsx:58-67, 91-93; w5-signin-apistopped-hell | stört | imp | eigener Zustand, Netzfehler getrennt melden | mittel | passt (T19) |
| W5-04 | Erfolg und Info in Warngelb, Hinweise ohne Symbol | signIn.css:82-93, SignInApp.tsx:124, 172, 231 | stört | imp | Farben nach 5.9 mit Symbol | klein | weicht ab (5.9) |
| W5-05 | Gerätecode im Satz, verwechselbare Zeichen, kein Kopieren | signIn.css:95-99, SignInApp.tsx:171-175; w5-devicecode-dunkel | stört | imp | eigene Zeile, Monospace, Kopieren | klein | passt (3) |
| W5-06 | „Neues Spa registrieren“ klappt weit unten auf, Fokus bleibt am Knopf | SignInApp.tsx:197-219 | Feinschliff | emil | ins erste Feld fokussieren | klein | passt |
| W5-07 | Testkonten-Auswahl läuft bei 390 px aus der Karte, ohne sichtbare Beschriftung (nur Entwicklung) | DevAccounts.tsx:42-49 | Feinschliff | imp | `min-width: 0`, Beschriftung | klein | weicht ab (10) |
| W5-08 | Ghost-Knöpfe fluchten nicht, „Abmelden“ allein in eigener Zeile | signIn.css:168-171 | Feinschliff | imp | rechts außen | klein | passt (5.1) |
| W5-09 | Einladung ohne Karte, drei gleiche Absätze | InvitationPage.tsx:50-71 | Feinschliff | imp | Karte, Konto als eigene Zeile | klein | passt (5.5) |
| W5-10 | „für Alle Standorte“, uneinheitliche Rollenbegriffe | InvitationPage.tsx:58, roles.ts:4-11 | Feinschliff | imp | klein schreiben, vereinheitlichen | klein | weicht ab (T12) |
| W5-11 | Kein Hover, kein Drücken, Deaktiviert 0,6 | signIn.css:152-183 | Feinschliff | emil | wie W1-12, Deckkraft 0,5 | klein | passt (4.6) |
| W5-12 | Feldbreiten uneinheitlich, Fehlertext bricht im schmalen Feld | SignupForm.tsx:50-97, formField.css | Feinschliff | imp | Breiten nach 5.2 | klein | weicht ab (5.2) |
| W5-13 | „Land“ mit 3 Werten als Aufklappliste | SignupForm.tsx:62-72 | Feinschliff | imp | Optionsfelder | klein | passt (R10) |
| W5-14 | Registrierung ohne `autoComplete` | SignupForm.tsx:50-97 | Feinschliff | imp | Attribute ergänzen | klein | passt (R12) |
| W5-15 | Lotus-Symbol fehlt im Kopf (auch W6) | SignInApp.tsx:226-228 | Feinschliff | imp | Inline-SVG | klein | weicht ab (1) |
| W5-16 | Drei verschiedene Ladetexte (W5, W6) | SignInApp.tsx:130-135, InvitationPage.tsx:45, OperatorApp.tsx:131 | Feinschliff | imp | einheitlich „Wird geladen …“ | klein | passt |

Muster W5 und W6: Feldbeschriftung 12,5 px liegt nicht in der Rampe (signIn.css:117-122, operator.css:89-94).

## W6 Betreiberseite (`/betreiber/`, `web/src/operator/`)

| ID | Befund | Fundstelle | Schwere | Skill | Vorschlag | Aufwand | Brief |
| --- | --- | --- | --- | --- | --- | --- | --- |
| W6-01 | Ladefehler und Aktionsfehler ungestaltet | OperatorApp.tsx:132-139, 398, 483; w6-list-error-dunkel | stört | imp | Hinweis-Komponente nach 5.9 | klein | weicht ab (5.9) |
| W6-02 | Falscher Schlüssel: Formular wird ausgehängt, Feld leer, Fehler ohne Weg | OperatorApp.tsx:101-106, 130-131, 197-201; w6-signin-wrongkey-dunkel | stört | imp, emil | Formular stehen lassen, Fehler am Feld | mittel | passt (R8, T19) |
| W6-03 | Gesperrte Spas kaum erkennbar, volle Karten ohne Sortierung (nur Quelltext) | OperatorApp.tsx:146-153, 207-240, 388-401 | stört | imp | Status-Chips, kompakte Zeilen, Problemfälle zuerst | mittel–groß | passt (5.7, 5.8) |
| W6-04 | „Spa sperren“ wie normaler Knopf, Bestätigung inline rot, drei Bestätigungsmuster | OperatorApp.tsx:324-347, 404-439, 469-482; w6-list-hell | stört | imp | Danger-Outline, ConfirmDialog | mittel | weicht ab (5.1, 5.27) |
| W6-05 | Abo-Stufe läuft bei 390 px über den Kartenrand | operator.css:200-202; w6-list-390-hell | stört | imp | `min-width: 0` | klein | passt |
| W6-06 | Du-Form („Gib ihn ein.“, „Trage ihn ein“), Konfigurationsname in der Meldung | OperatorApp.tsx:63, 174, 369, 512 | stört | imp | unpersönlich, ohne `Operator:ApiKey` | klein | weicht ab (T1, T19) |
| W6-07 | Zwei Beschriftungsstile in einer Karte, Gruppen ohne Rhythmus | OperatorApp.tsx:216-226, 279; operator.css:80-94 | Feinschliff | imp | FormField, Gruppen mit h3 | klein–mittel | weicht ab (R6, 4.1) |
| W6-08 | Kopfzeile springt beim Anmelden von 47 auf 61 px | operator.css:26-33, 126-135 | Feinschliff | emil | kleiner Knopf, feste Höhe | klein | passt |
| W6-09 | Schlüsselfeld 700 px breit | OperatorApp.tsx:168-180, operator.css:40-44 | Feinschliff | imp | höchstens 280 px | klein | weicht ab (5.2) |
| W6-10 | `autoComplete="current-password"` ohne Benutzerfeld | OperatorApp.tsx:185 | Feinschliff | imp | entscheiden: Passwortmanager oder `off` | klein | passt (R12) |
| W6-11 | Deaktiviert ohne Grund, Erfolg bleibt stehen, kein Hover/Drücken | OperatorApp.tsx:296-304, 565-569; operator.css:126-151 | Feinschliff | emil | Grund nennen, 5.9, 4.6 | klein | passt |
| W6-12 | Zähler und Erklärung in einem blassen Absatz, Liste ohne Überschrift | OperatorApp.tsx:142-145 | Feinschliff | imp | „Spas (3)“ als h2 | klein | passt |
| W6-13 | Platzhalter in Browserfarbe, ca. 4,2:1 (gerechnet) | operator.css:76-84 | Feinschliff | imp | `::placeholder` in fg-2 | klein | passt (5.2) |

Gut und zu erhalten (W5, W6): Passkey zuerst; Fokusring in beiden Schemata klar, `forced-colors` berücksichtigt; Registrierung mit Fehlern am Feld und Fokus ins erste ungültige Feld; Rückfragedialog mit Fokus auf „Abbrechen“; Betreiber-Schlüssel nur in `sessionStorage`.

Nicht beurteilt: Anmeldung mit Passwort im Einzelplatz, Passkey-Systemdialog, `w5-signin-error-*` (leere Aufnahmen); auf der Betreiberseite gesperrter Zustand, Postfachfehler, Suche ab 6 Spas, offene Dialoge nur aus dem Quelltext.

## Vorschlag zur Umsetzung

Grob in dieser Reihenfolge, je als eigene Aufgabe:

1. W3-01/W3-02 (Dashboard abgeschnitten) – blockiert die Tafel.
2. Fehlermeldungen mit Weg: W1-06, W1-07, W5-02, W5-03, W6-02, W6-06, W2-08 – klein, hoher Nutzen.
3. Entscheidung 1 (Brief oder eigene Werte), danach eine gemeinsame Hinweis-Komponente und Hover/Drücken für alle schlanken Oberflächen (Punkte 7 und 10).
4. Handy-Layout Kundenbereich und Mein Tag (Punkte 5 und 6).
5. Hilfe nach Brief 5.30 und Entscheidung 2/3 (Punkte 2 und 9).
6. Feinschliff nach Oberfläche.
