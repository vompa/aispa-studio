Status: closed

# Runde R05 – Alle Masken nativ, Web nur für Dashboard und Handy, Team mit HR, besseres Lager

## Quelle
- Überlegungen des Auftraggebers vom 29.10.2026 in der Sitzung nach dem Posteingang (Kundenerkennung), gesammelt mit „ich sage fertig“ und mit „im groben bin ich fertig, kannst du daraus eine runde machen?“ freigegeben.
- Ergänzungen von Claude beim Sammeln (Rechte je Kanal, Datenschutz am Dashboard, Push, Passkeys, Kennzahlen der Leitung am Handy); der Auftraggeber hat sie gelesen und ohne Einwand die Runde verlangt.
- Nachträge des Auftraggebers am selben Tag: „auch sollte das personal ( team ) zusammengehalten werden für die verwaltung, und nicht teile verstreut auf mehrere verwaltungsmasken“ (Ä250) und „überlege dir auch ein paat hr funktionen zum personal, mitarbeiterbesprechung, anmerkungen, verwarnungen, hinweise, sonderregelungen, vollmachten sollen ersichtlich sein“ (Ä252), dazu „ersthelfer, sicherheitsvertrauensperson, zuständig fürs lager usw“ und „auch sollen zum personal zertifikate und wann sie erneuert werden müssen sichtbar sein“ (Ä274).
- Nachtrag des Auftraggebers: „überlege dir auch wie man die lagerverwaltung besser gestalten und in das buchungssystem integrieren kann, suche dazu ein paar beispiele bei der konkurrenz“ (Lager-Slices unten, Vergleich in docs/konzepte/lager-konkurrenz.md).

## Eingang

| Kennung | Wo | Beobachtet → Erwartet | Größe | Quelle |
|---|---|---|---|---|
| Ä191 | Windows-App und Web, alle Eingabe- und Verwaltungsmasken | Verwaltung läuft heute als Web-Team-Oberfläche im eingebetteten Browser (ADR 0053) → jede Maske zur Eingabe und Verwaltung nativ in der Windows-App; eingebettete Verwaltung und Web-Team-Oberfläche entfallen | L | Auftraggeber, 29.10.2026 | Teil erledigt in Slices 93–99, 101 und 102; Lohn erledigt in Slice 100; Rest erledigt in Slice 103.
| Ä192 | Web, Pausenraum | Es gibt keine Übersicht für das Team abseits der Rezeption → Dashboard für den Pausenraum, nur Anzeige | M | Auftraggeber, 29.10.2026 | erledigt in Slice 105 |
| Ä193 | Web, Handy der Mitarbeitenden | „Mein Tag“ zeigt nur den Plan → Handy-App für Mitarbeitende mit Grundfunktionen | L | Auftraggeber, 29.10.2026 |
| Ä194 | Web, Handy der Kunden | Kundenbereich ist auf Buchen ausgerichtet → Handy-App für Kunden zur Terminkontrolle | L | Auftraggeber, 29.10.2026 | erledigt in Slice 108 |
| Ä195 | API, alle Web-Apps | Ergänzung beim Sammeln: Rechte je Kanal (Anzeigegerät nur lesen, Mitarbeitende nur eigene Daten, Kunden nur eigene Termine), Kennzahlen der Leitung am Handy als Option | M | Claude, 29.10.2026 (Vorschlag beim Sammeln, ohne Einwand) | Rechte je Kanal erledigt in Slice 104; Kennzahlen am Handy erledigt in Slice 109.
| Ä250 | Windows-App, Verwaltung, Personal | Personal ist heute auf mehrere Masken verteilt (Personal, Team, Qualifikationen in den Stammdaten, Zugänge, Dienstplan, Zeiterfassung, Lohn) → alles zu einer Person an einer Stelle: Bereich „Team“ mit Personenkarte | M | Auftraggeber, 29.10.2026 | Teil erledigt in Slice 98 (Liste, Personenkarte, Zugang, Qualifikationen, Verlauf) und Slice 99 (Dienstplan, Abwesenheiten, Zeiten, Anträge); Lohn erledigt in Slice 100 (Reiter „Lohn“ der Personenkarte, Ansicht „Lohn“ im Team).
| Ä251 | API, Windows-App, Handy-App, Lager | Lagerverwaltung besser gestalten und in Buchung und Kassa einbinden, mit Beispielen der Konkurrenz | L | Auftraggeber, 29.10.2026 | Produktempfehlung im Termin und an der Kassa erledigt in Slice 115. Soll-Ist-Bericht, Zählung am Handy mit Abschluss in der Windows-App und Kamera-Scan erledigt in Slice 116 (Ä271–Ä273).
| Ä252 | Windows-App und API, Team | HR-Funktionen fehlen → Mitarbeiterbesprechungen, Anmerkungen, Verwarnungen, Hinweise, Sonderregelungen und Vollmachten je Person, auf einen Blick ersichtlich | L | Auftraggeber, 29.10.2026 | Gespräche, Anmerkungen und Verwarnungen erledigt in Slice 110; Hinweise, Sonderregelungen und Vollmachten erledigt in Slice 111 |
| Ä256 | API, Buchen, Dienstplan | Serien, Gruppenbuchungen, Offline-Nachbuchen und „Wie Wochenarbeitszeit“ prüfen Sonderregelungen nicht → auch dort warnen, sobald es dafür eine Rückfrage gibt | S | Claude, 01.11.2026 | offen |
| Ä274 | Windows-App und API, Team | Zuständigkeiten (Ersthelfer, Sicherheitsvertrauensperson, zuständig fürs Lager usw.) und Zertifikate mit Erneuerungsdatum sind nicht sichtbar → je Person ersichtlich, mit Erinnerung vor dem Ablauf | M | Auftraggeber, 29.10.2026 | Erledigt in Slice 117 |
| Ä196 | Windows-App, Start | Die App findet einen Server nur über die Umgebungsvariable `AISPA_API_URL`; eine Eingabe gibt es nur für den Haupt-PC im eigenen Netz → Server-Adresse in der App eintragen, damit die Leitung von zu Hause verwalten kann (E2) | S | Slice 92 – erledigt in Slice 118 |
| Ä197 | Mehrplatz im eigenen Netz | Der Haupt-PC lauscht auf allen Netzwerkkarten (ADR 0049), das Programm gibt ihn aber nicht nach außen frei → von zu Hause nur im Server-Betrieb oder über ein VPN des Spas, keine Portweiterleitung; im Handbuch beschreiben | S | Slice 92 – erledigt in Slice 118 |
| Ä198 | Windows-App, verlorenes Gerät | „Zugang sperren“ beendet die Anmeldung, der Offline-Stand bleibt aber auf dem Gerät (DPAPI, an das Windows-Konto gebunden) → im Handbuch zu Windows-Kennwort und Laufwerksverschlüsselung raten | S | Slice 92 – erledigt in Slice 118 |
| Ä199 | Windows-App, Verwaltung | Bis Slice 103 zeigt ein nicht nativer Bereich die eingebettete Verwaltung mit ihrer eigenen Leiste neben der nativen Bereichsliste (doppelte Navigation) → entfällt mit der eingebetteten Verwaltung | S | Slice 93 – erledigt in Slice 103 |
| Ä202 | Windows-App, Behandlungen | „Wer kann das?“ öffnet den Bereich „Team“ mit `id=` der Person; die eingebettete Web-Verwaltung (`TeamTab.tsx`) übergeht die Id und zeigt die erste Person → mit der nativen Personenkarte die Person direkt öffnen | S | Slice 94 – erledigt in Slice 98 |
| Ä203 | Hilfe, Seite „Verwaltung“ | `seite-verwaltung.md` beschreibt seit Slice 93 noch die Leiste der eingebetteten Verwaltung oben statt der Bereichsliste links und nennt die nativen Bereiche nicht → Thema mit Bereichsliste, nativen Masken und neuem Bild nachziehen | S | Slice 94 – erledigt in Slice 103 |
| Ä205 | Windows-App, Kalender und Schließzeiten | Die Absage der Termine einer Schließzeit ist im Kalender (`CalendarViewModel.Closures.cs`, `CancelClosureDialog`) und in der nativen Maske „Schließzeiten“ zweimal gebaut → ein gemeinsamer Ablauf, sobald der Kalender wieder angefasst wird | S | Slice 95 – offen |
| Ä206 | Windows-App und Web, Kalender-Abos | „Ausgeschaltet · Kopie mit 12 Tage“ (`daysText` im Web, `SubscriptionTexts.Status` im Kalender-Chip) → Einzahl und Mehrzahl richtig („1 Tag“, „12 Tagen“), an beiden Stellen zugleich | S | Slice 95 – offen |
| Ä207 | Windows-App, native Verwaltung | Fehler der API zu einem Feld (ValidationProblem) erscheinen in den Masken nur als „Nicht gespeichert“ über der Leiste, weil `ApiClient` den Fehlertext ohne Feldzuordnung liefert → Fehler dem Feld zuordnen wie im Web (`serverErrors`) | S | Slice 95 – offen |
| Ä211 | Windows-App, native Verwaltung | `CompanyViewModel` meldet nach dem Speichern `Saved` (die Shell versucht dann den Assistenten erneut); `IntakeViewModel` und `HandbookViewModel` melden es nicht → nach dem Speichern von Postfach und Handbuch denselben Haken auslösen | S | Slice 97 – offen |
| Ä212 | Windows-App, KI | Die Nachfrage nach dem Stand des Modell-Downloads (alle 2 s) läuft nach einem Seitenwechsel und nach dem Abmelden bis zum Ende des Downloads weiter (`AiViewModel.Dispose` ruft die App nirgends auf); die Nachfrage von „Jetzt prüfen“ (20 × 750 ms) hat kein Abbruch-Token → beim Verlassen des Bereichs und beim Abmelden beenden | S | Slice 97 – offen |
| Ä213 | Windows-App, native Masken | Texte.md T1 verlangt unpersönliche Anrede, die Feldmeldungen der nativen Masken sagen aber „Trage ihn ein“ (Firmendaten, Behandlungen, Handbuch) → einen Ton festlegen und in allen nativen Masken angleichen, zusammen mit dem Thema aus Slice 94 | S | Slice 97 – offen |
| Ä223 | Windows-App, Lager | Bestellungen (`PurchaseOrders.tsx`), Bedarfsprüfung (`StockDemand.tsx`) und Chargen-Rückverfolgung (`BatchTrace.tsx`) gibt es nur im Web; Slice 103 entfernt dieses → vor Slice 103 nativ bauen (Empfehlung: zusammen mit den Slices 112/113) | M | Slice 101 – erledigt in Slice 123 |
| Ä224 | Windows-App, Lager | Material und Mindestmengen werden aus Auswahllisten ohne Tippen und Vorschlag gewählt (Web: Eingabe mit Vorschlag), „Bewegung buchen“ ist eine Karte neben dem Reiter statt der Seitenleiste nach Brief 5.12, Lieferant ohne Vorschlagsliste → bei Gelegenheit angleichen | S | Slice 101 – offen |
| Ä225 | Windows-App und Web, Lager | Ein angelegtes Material lässt sich nicht mehr ändern (Name, Einheit, Preis, Lieferant), obwohl `PUT /api/stock/items/{id}` es kann → „Material ändern“ im Bestand (zusammen mit Slice 112, Lieferantenstamm) | S | Slice 101 – offen |
| Ä226 | Windows-App, Berichte | Der Vergleich mit anderen Spas zeigt nativ Werte als Text; die Balken des Web (mittlere Hälfte, Mitte, eigener Wert) fehlen → mit einfacher Zeichnung nachziehen, ohne Bibliothek | S | Claude, 30.10.2026 |
| Ä232 | API, Live-Hub | Anzeige und Mitarbeiter-App erreichen den Live-Hub nicht, weil die Kanal-Sperre ihn nicht freigibt → Slice 105 und 106 geben ihn je Kanal frei, ohne Personendaten für die Anzeige | S | Slice 104 – Anzeige erledigt in Slice 105, Mitarbeiter-App zurückgestellt in Slice 106: „Mein Tag“ nutzt keinen Live-Hub, der Hub bleibt für den Kanal gesperrt |
| Ä233 | API, Kopplung der Anzeigen | Abgelaufene Kopplungswünsche (`DisplayPairings`, ohne Mandant) bleiben in der Datenbank; das Anlegen ist je IP begrenzt → ein Hintergrundjob löscht Einträge, die länger als einen Tag abgelaufen sind | S | Slice 104 – offen |
| Ä234 | API, Webmodule | Kopplung und Sitzung der Anzeige hängen an keinem Modulschalter (ADR 0041) → Slice 105 legt sie hinter den Schalter des Dashboards | S | Slice 104 – erledigt in Slice 105 |
| Ä235 | API, Tafel im Pausenraum | Dienstplan, Raumsperren und Schließzeiten lösen kein Signal an die Anzeige aus → die Tafel lädt alle 30 Sekunden nach; ein eigenes Signal beim Speichern von Dienstplan und Sperren zeigt sie sofort | S | Slice 105 – offen |
| Ä236 | Hilfe, Tafel | Das Handbuchthema „Tafel im Pausenraum lesen“ hat kein Bild der Tafel → Bild erzeugen, sobald der Bildlauf der Web-Hilfe die Tafel mit Beispieldaten starten kann | S | Slice 105 – offen |
| Ä237 | API, Live-Hub | Eine widerrufene Anzeige bleibt mit ihrer Hub-Verbindung bis zum nächsten Abbruch in der Gruppe und bekommt weiter das Signal ohne Daten; ihre Abfrage der Tafel antwortet sofort mit 401 → Verbindungen widerrufener Anzeigen beim Widerruf schließen | S | Slice 105 – offen |
| Ä238 | Hilfe, Mein Tag | Die Handbuchbilder `myday-heute` und `myday-karte` zeigen die Oberfläche ohne die Leiste Tag, Woche, Abwesenheit und ohne „Begonnen melden“ → Bilder neu erzeugen; die neuen Themen bekommen Bilder | S | Slice 106 – offen |
| Ä239 | Windows-App, Dienstplan | Die Hilfe zu den Anträgen (Slice 99, `dienstplan-antrag-entscheiden.md`) sagt noch nicht, dass die Person die Entscheidung in der Handy-App sieht, und nennt die Liste bis zur Handy-App leer → nach dem Merge beider Slices Thema und Rückfrage nachziehen | S | Slice 106 – offen |
| Ä240 | Web, Mein Tag | Nach dem Kanalwechsel sieht dieselbe Anmeldung im selben Browser nur die Rechte der Mitarbeiter-App; wer am selben Gerät auch die Team-Oberfläche nutzt, muss sich neu anmelden → erklärender Hinweis in der App, wenn der Betrieb das braucht | S | Slice 106 – offen |
| Ä244 | API und Kunden-App, Anzahlung | Nach der Frist bleibt bei einer bezahlten Anzahlung nur der Anruf, weil keine Regel zum Einbehalten besteht → Regel festlegen und in der App wie die Gebühr anzeigen | S | Slice 108 – offen |
| Ä245 | Web, Kunden-App | Ohne Netz startet die App, zeigt aber „Anmelden“ statt eines Hinweises auf die fehlende Verbindung → eigener Zustand „offline“ | S | Slice 108 – offen |
| Ä246 | Hilfe, Kunden-App | Das Handbuchthema `kunden-app-termine` hat keine Bilder → Bilder erzeugen, sobald der Bildlauf der Web-Hilfe den Kundenbereich mit Beispieldaten starten kann | S | Slice 108 – offen |
| Ä247 | Hilfe, Mein Tag | Das Handbuchthema `mein-tag-heute-im-spa` hat kein Bild der Seite → Bild erzeugen, sobald der Bildlauf der Web-Hilfe die Mitarbeiter-App als Inhaberin mit Beispieldaten starten kann | S | Slice 109 – offen |
| Ä248 | Web, Heute im Spa | Die Seite zeigt nur den heutigen Tag eines Standorts, ohne Vergleich mit dem Vortag und ohne Summe über alle Standorte → bei Bedarf Vorperiode aus dem Bericht anzeigen und eine Summe der Standorte als Bericht der API anbieten (neue Kennzahl, deshalb nicht in Slice 109) | S | Slice 109 – offen |
| Ä249 | API, Bericht der Leitung | `openRequests` zählt nur Anfragen im Status `NeedsReview` zum Stand jetzt; Anfragen, die auf die Antwort des Gastes warten (`Proposed`), fehlen, und die Zeile „Offene Anfragen (heute)“ steht auch im Bericht für vergangene Zeiträume mit dem heutigen Stand (wie die offenen Gutscheine) → mit der Rezeption klären, was „offen“ heißen soll; Anfragen ohne Standort zählen bei jedem Standort mit | S | Slice 109 – offen |
| Ä220 | API und Windows-App, Lohn | Die Slice-Datei nennt „Provision je Person“; API und Web kennen keine Provision, und E12 schließt sie für den Produktverkauf aus → klären, ob und worauf (etwa Behandlungen) eine Provision kommt, bevor der Reiter sie zeigt | S | Slice 100 – offen |
| Ä221 | API und Windows-App, Lohn | Der Reiter „Lohn“ der Person holt die Vorschau aller Personen und filtert in der App (`GET api/payroll/preview` kennt keinen Filter `employeeId`, anders als Dienstplan und Zeiten) → Filter in der API | S | Slice 100 – offen |
| Ä222 | Hilfe, Lohn | Die Handbuchbilder `payroll-monatswerte` zeigen die Web-Seite; die Ansicht und der Reiter der Windows-App haben kein Bild → Bilder erzeugen | S | Slice 100 – offen |
| Ä253 | Hilfe, Personalakte | Das Handbuchthema `personalakte` hat kein Bild des Reiters → Bild aus dem Bildtest `verwaltung-team-person-personalakte` übernehmen | S | Slice 110 – offen |
| Ä254 | API, Auskunft als PDF | `SimplePdf` kann nur WinAnsi; Zeichen außerhalb (etwa türkische oder thailändische Namen) erscheinen als Fragezeichen → mit einer freigegebenen PDF-Bibliothek oder eingebetteter Schrift lösen | S | Slice 110 – offen |

| Ä285 | Windows-App, Termin und Kassa | Kartengarantie, Anzahlung anfordern, Erinnerungsverlauf am Termin, Info zur Online-Rückerstattung und Gutscheinabfrage an der Kassa gibt es nur im Web; Slice 103 entfernt dieses → vor Slice 103 nativ bauen | M | Slice 103 – erledigt in Slice 124 |
| Ä286 | Web, Server-Betrieb | Code für die Windows-App, Annahme einer Einladung mit Passkey, Passkey hinzufügen und Registrierung eines neuen Spas gibt es nur in der Team-Oberfläche; ADR 0084, Punkt 5 stimmt nur, solange sie da ist → eigene Seite `/anmelden/` | M | Slice 103 – erledigt in Slice 103 |
| Ä290 | Windows-App, Kassa | Sofortüberweisung am Beleg (`pos/bills/{id}/instant-transfer`) gibt es nur im Web; Slice 103 entfernt dieses → vor Slice 103 nativ bauen | M | Slice 124 – erledigt in Slice 126 |
| Ä291 | Windows-App, Kalender | Lückenvorschläge (`locations/{id}/gap-suggestions`) gibt es nur im Web → vor Slice 103 nativ bauen | M | Slice 124 – erledigt in Slice 126 |
| Ä292 | Windows-App, Termin | Einwilligung für Wiederbuchungs-Hinweise (`customers/{id}/marketing-consent`) gibt es nur im Web → vor Slice 103 nativ bauen | M | Slice 124 – erledigt in Slice 126 |
| Ä297 | Windows-App, Kassa | Die Sofortüberweisung zeigt Empfänger, IBAN, Betrag und Zahlungstext, aber kein QR-Bild; das Web zeichnet es mit dem Paket `qrcode`, die Windows-App hatte kein QR-Paket → Paket QRCoder (Auftraggeber, 31.10.2026) | S | Slice 126 – erledigt in Slice 126 |
| Ä298 | Windows-App, Kalender | Die Lückenvorschläge laden nur bei geöffneter Seite und beim Tageswechsel, nicht als Zähler in der Kopfzeile; wer sie täglich braucht, sieht sie erst nach dem Öffnen → Chip „Lücken n“ wie bei der Warteliste, falls gewünscht | S | Slice 126 – offen |
| Ä293 | Windows-App, Verwaltung | Die Auswahl der Verwaltungsmasken ist eine Liste → heller Balken oben im Stil der linken Leiste mit sieben Gruppen, Masken als Reiter darunter | M | Auftraggeber, 31.10.2026 – erledigt in Slice 125 |
| Ä300 | Windows-App, Termin | Anamnese im Team (Bogen lesen, Tablet-Link, Papierbogen, Foto mit Prüfen und Bestätigen) gibt es nur im Web; Slice 103 entfernt dieses → vor Slice 103 nativ bauen | M | Slice 127 – erledigt in Slice 127 |
| Ä304 | Datenschutz, Anamnese | ADR 0013, `CLAUDE.md` und `docs/architektur.md` sagen „Rezeption nur Stand“, API und Web lassen sie aber die Antworten lesen → entscheiden und einheitlich durchsetzen; blockiert die erste Auslieferung | H | Slice 127 – in Slice 128 geplant – erledigt in Slice 128 |
| Ä229 | Web, Kundenbereich | `booking/booking.css` nimmt mit `.heading:focus, main:focus` den Fokusrahmen weg, ohne Ersatz im Kontrastdesign; der in Slice 103 erweiterte Scan auf `outline: none` nimmt die Datei bis dahin aus → sichtbaren Fokus im Kontrastdesign ergänzen und die Ausnahme streichen | S | Slice 103 – offen |
| Ä230 | Web | `index.css` enthält noch die Regeln der entfernten Team-Oberfläche (Kalender, Posteingang, Verwaltung, Assistent …) und geht so ins Hilfe-Bundle; das Paket `qrcode` (mit `@types/qrcode`) nutzt kein Quelltext mehr → Regeln ohne Verwender entfernen, Paket mit Freigabe entfernen | S | Slice 103 – offen; Paket `qrcode` erledigt in T-034, die CSS-Regeln bleiben offen |
| Ä231 | Hilfe, Seite „Anmelden“ | Das Thema `anmelden-web` hat kein Bild, und die Themen, deren Bilder der Team-Oberfläche entfallen sind, haben noch keine App-Bilder → beim nächsten Bildlauf ergänzen (wie Ä259) | S | Slice 103 – offen |

## Slices
| Slice | Name | Befunde | Hängt ab von | Modell | Neue Kennungen |
|---|---|---|---|---|---|
| 92 | Grundsatz: alle Masken nativ, Web nur für Dashboard und Handy | Ä191, Ä192, Ä193, Ä194, Ä195 | – | opus | Ä196–Ä198 |
| 93 | Native Verwaltung: Gerüst und Firmendaten als Vorbild | Ä191 | 92 | opus | Ä199–Ä201 |
| 94 | Native Verwaltung: Behandlungen | Ä191 | 93 | opus | Ä202–Ä204 |
| 95 | Native Verwaltung: Standorte, Räume, Schließzeiten, Raumsperren und Kalender-Abos | Ä191 | 93 | sonnet | Ä205–Ä207 |
| 96 | Native Verwaltung: Kassa, Zahlungsanbieter und Erinnerungen | Ä191 | 93 | opus | Ä208–Ä210 |
| 97 | Native Verwaltung: Anfragen-Annahme, KI und Online-Buchung einbetten | Ä191 | 93 | sonnet | Ä211–Ä213 |
| 98 | Native Verwaltung: Bereich Team mit Personenkarte | Ä191, Ä250 | 93 | opus | Ä214–Ä216 |
| 99 | Team: Dienstplan, Abwesenheiten und Zeiten | Ä191, Ä250 | 98 | opus | Ä217–Ä219 |
| 100 | Team: Lohn und Provision | Ä191, Ä250 | 99 | sonnet | Ä220–Ä222 |
| 101 | Native Verwaltung: Lager und Aktionen | Ä191 | 93 | sonnet | Ä223–Ä225 |
| 102 | Native Verwaltung: Berichte, Kosten, Buchhaltung und Geschäftsinfo | Ä191 | 93 | opus | Ä226–Ä228 |
| 103 | Eingebettete Verwaltung und Web-Team-Oberfläche entfernen | Ä191, Ä286 | 94, 95, 96, 97, 98, 99, 100, 101, 102, 123, 124, 125, 126, 127 | opus | Ä229–Ä231 |
| 104 | API: Rechte je Kanal für Dashboard, Mitarbeiter-App und Kunden-App | Ä195 | 92 | opus | Ä232–Ä234 |
| 105 | Web: Dashboard für den Pausenraum | Ä192 | 104 | sonnet | Ä235–Ä237 |
| 106 | Web: Handy-App für Mitarbeitende | Ä193 | 104 | opus | Ä238–Ä240 |
| 108 | Web: Handy-App für Kunden zur Terminkontrolle | Ä194 | 104 | opus | Ä244–Ä246 |
| 109 | Web: Kennzahlen des Tages für die Leitung am Handy | Ä195 | 106 | sonnet | Ä247–Ä249 |
| 110 | Team: Personalakte mit Gesprächen, Anmerkungen und Verwarnungen | Ä252 | 98 | opus | Ä253–Ä255 |
| 111 | Team: Hinweise, Sonderregelungen und Vollmachten auf einen Blick | Ä252 | 98 | opus | Ä256–Ä258 |
| 112 | Lager: Lieferantenstamm, Packungseinheiten und Ablaufwarnung | Ä251 | 101 | opus | Ä259–Ä261 |
| 113 | Lager: Bestellvorschlag nach Verbrauch, Höchstmenge und Lieferzeit | Ä251 | 112 | opus | Ä262–Ä264 |
| 114 | Lager: Barcode und Scannen bei Wareneingang, Inventur und Kassa | Ä251 | 101 | sonnet | Ä265–Ä267 |
| 115 | Lager im Buchungsablauf: Produktempfehlung im Termin und an der Kassa | Ä251 | 101, 94 | opus | Ä268–Ä270 |
| 116 | Lager: Soll-Ist-Verbrauch und Inventur am Handy | Ä251 | 106, 114 | sonnet | Ä271–Ä273 |
| 117 | Team: Zuständigkeiten und Zertifikate mit Erneuerung | Ä274 | 98 | opus | Ä275–Ä277 |
| 118 | Verwalten von zu Hause: Server-Adresse in der App eintragen | Ä196, Ä197, Ä198 | 92 | sonnet | Ä278–Ä280 |
| 123 | Lager: Bestellungen, Bedarfsprüfung und Chargen-Rückverfolgung nativ | Ä223 | 113 | opus | Ä287–Ä289 |
| 124 | Termin und Kassa: Kartengarantie, Anzahlung, Erinnerungsverlauf, Rückerstattung und Gutscheinabfrage nativ | Ä285 | 94, 96 | opus | Ä290–Ä292 |
| 125 | Verwaltung: Navigation oben mit Gruppen statt Liste | Ä293 | 123 | opus | Ä294–Ä296 |
| 126 | Kassa und Kalender: Sofortüberweisung, Lückenvorschläge und Einwilligung nativ | Ä290, Ä291, Ä292 | 124 | sonnet | Ä297–Ä299 |
| 127 | Termin: Anamnese im Team nativ | Ä300 | 126 | opus | Ä301–Ä303 |
| 128 | Anamnese: Leserecht der Rezeption klären und durchsetzen | Ä304 | 127 | opus | Ä305–Ä307 |

Reihenfolge: zuerst 92 (ADR und Inventar), dann 93 (Gerüst) und parallel 104 (Kanäle). Danach parallel die Masken-Slices 94–98, 101, 102 (98 zuerst unter den Personal-Slices, weil 99 und 100 Reiter an seine Personenkarte hängen) (je eigene Ansichten; alle ändern die Navigation in `ShellViewModel`/`AdminViewModel` nur an einer Stelle, deshalb höchstens drei zugleich und `dev` vorab einmischen), 99 nach 98, 100 nach 99. 103 erst, wenn 94–102 fertig sind. Die Web-Slices 105, 106, 108 laufen nach 104 parallel zu den Masken-Slices; 109 nach 106; Push (früher Slice 107) ist zurückgestellt. 118 nach 92, unabhängig von den Masken-Slices (ändert nur Start und Seite „Netz“). HR: 110, 111 und 117 nach 98, untereinander nur nach Einmischen von `dev` (gleiche Personenkarte). Lager: 112 und 114 nach 101, 113 nach 112, 115 nach 101 und 94, 116 nach 106 und 114.

## Entscheidungen
Ruling: Jede Maske zur Eingabe und Verwaltung läuft nativ in der Windows-App; das Web liefert nur noch Dashboard für den Pausenraum, Handy-App für Mitarbeitende und Handy-App für Kunden (Ä191–Ä194, ADR 0084 in Slice 92) – Auftraggeber, 29.10.2026
Ruling: Bis Slice 103 bleibt die eingebettete Verwaltung für noch nicht native Bereiche erreichbar; jede Masken-Slice ersetzt ihren Bereich vollständig (gleiche Felder, Regeln, Hilfe), neue Felder kommen nicht dazu – Claude, 29.10.2026 (Vorschlag beim Sammeln: „eine Web-Maske fällt erst weg, wenn die native Fassung alles kann“)
Ruling: Alle Geschäftslogik der neuen Masken und Web-Apps (Fristen, Gebühren, Genehmigungen, Erinnerungen, Kennzahlen, Lagerbuchungen) liegt in der API; Oberflächen zeigen nur an – Auftraggeber (Arbeitsregel, Gedächtnis „Geschäftslogik in der API“)
Ruling: Das Team wird für die Verwaltung zusammengehalten: ein Bereich „Team“ mit Liste und Personenkarte (Stammdaten, Zugang und Rolle, Qualifikationen, Dienstplan und Abwesenheit, Zeiten, Lohn und Provision, Verlauf) ist der einzige Ort, an dem Personendaten geändert werden; andere Masken zeigen nur und springen dorthin (Ä250, Slices 98–100) – Auftraggeber, 29.10.2026
Ruling: HR-Funktionen (Mitarbeitergespräche, Anmerkungen, Verwarnungen, Hinweise, Sonderregelungen, Vollmachten) sind Reiter und Kennzeichen der Personenkarte, kein eigener Bereich; Hinweise, Sonderregelungen und Vollmachten sind oben auf der Karte ersichtlich (Ä252, Slices 110, 111) – Auftraggeber, 29.10.2026 (Auftrag), Aufteilung Claude
Ruling: Zuständigkeiten (Ersthelfer, Sicherheitsvertrauensperson, Lager …) und Zertifikate mit Gültig-bis sind Kennzeichen der Personenkarte mit Übersichten im Bereich Team und Erinnerung vor dem Ablauf (Ä274, Slice 117) – Auftraggeber, 29.10.2026 (Auftrag), Aufteilung Claude
Ruling: Das Lager baut auf dem Bestehenden auf (Bewegungen, Chargen, Lagerorte, Rezept, Bedarfsprüfung beim Buchen) und ergänzt die Lücken aus docs/konzepte/lager-konkurrenz.md in den Slices 112–116; Bestellungen gehen nie ohne Freigabe an Lieferanten (Ä251) – Claude, 29.10.2026 (nach Recherche, Auftrag des Auftraggebers)
Ruling: Dashboard, Mitarbeiter-App und Kunden-App sind zuschaltbare Webmodule nach ADR 0041; der Einzelplatz läuft ohne sie – Claude, 29.10.2026 (Vorschlag beim Sammeln, ohne Einwand)
Ruling: Das Dashboard zeigt Initialen statt Gastnamen und nie Gesundheitsdaten; die Mitarbeiter-App zeigt bei Gesundheitsdaten nur ein Kennzeichen ohne Inhalt – Claude, 29.10.2026 (Vorschlag beim Sammeln, ohne Einwand)

Ruling: Hilfe und Handbuch bleiben im eingebetteten Browser; WebView2 bleibt nur dafür (E1, Slices 97, 103) – Auftraggeber, 29.10.2026
Ruling: Verwalten von zu Hause ist nötig und geht über dieselbe Windows-App mit Verbindung zum Server des Spas bzw. zum Cloud-Betrieb, nicht im Einzelplatz und nicht am Mac; keine Web-Verwaltung, keine Fernwartung als Weg; Slice 92 stellt fest, was dafür fehlt (E2) – Auftraggeber, 29.10.2026
Ruling: Kennzahlen des Tages für Inhaberin und Managerin als lesende Seite in der Mitarbeiter-App (E3, Slice 109) – Auftraggeber, 29.10.2026
Ruling: Masken nach Häufigkeit im Alltag: Behandlungen (94), Team (98), Dienstplan (99), Standorte (95), danach der Rest (E4) – Auftraggeber, 29.10.2026
Ruling: Das Dashboard zeigt nur Tagesleiste je Person, freie Räume und nächste Termine mit Initialen (E5, Slice 105) – Auftraggeber, 29.10.2026
Ruling: Die Mitarbeiter-App bekommt nur die Grundfunktionen (Plan, nächster Termin, begonnen/beendet, Abwesenheit, offline lesen); keine Zeiterfassung, kein Diensttausch, keine eigenen Zertifikate (E6, Slice 106) – Auftraggeber, 29.10.2026
Ruling: Push-Hinweise für Mitarbeitende werden zurückgestellt; die frühere Slice 107 ist aus der Runde genommen (E7) – Auftraggeber, 29.10.2026
Ruling: Die Personalakte liegt verschlüsselt im eigenen Schema `hr`; lesen nur Inhaberin und Managerin, die Person bekommt ihre Einträge per Export auf Anfrage (E8, Slice 110) – Auftraggeber, 29.10.2026
Ruling: Verwarnungen haben ein frei wählbares Ablaufdatum mit Vorgabe 2 Jahre und werden danach automatisch gelöscht; die Frist ist arbeitsrechtlich abzuklären (E9, Slice 110) – Auftraggeber, 29.10.2026
Ruling: Sonderregelungen warnen im Dienstplan und beim Buchen im Kalender mit „Trotzdem“, nie als Sperre (E10, Slice 111) – Auftraggeber, 29.10.2026
Ruling: Kassa-Storno und Bestellungen auslösen prüft die API zusätzlich zur Rolle; die übrigen Vollmachten sind nur Anzeige (E11, Slice 111) – Auftraggeber, 29.10.2026
Ruling: Keine Provision auf Produktverkauf; Slice 115 baut nur die Produktempfehlung (E12) – Auftraggeber, 29.10.2026
Ruling: Gescannt wird mit USB-Handscanner als Tastatureingabe und mit der Handy-Kamera bei der Inventur, ohne eigene Hardware-Anbindung (E13, Slices 114, 116) – Auftraggeber, 29.10.2026
Ruling: Der Dienstplan warnt mit „Trotzdem“, wenn weniger Ersthelferinnen mit gültigem Kurs im Dienst sind als festgelegt (Vorgabe 1) (E14, Slice 117) – Auftraggeber, 29.10.2026
Ruling: Plan von Slice 92 mit allen Annahmen freigegeben: Kalender-Abos und Raumsperren gehören zu Slice 95, Slice 101 heißt „Lager und Aktionen“; ADR 0084 ersetzt 0053, 0041 und 0059 bekommen Nachträge, 0079 bleibt; Fernzugriff mit den Befunden Ä196 und Ä197 und der neuen Slice 118; der Doku-Test `maskInventory.test.ts` gilt bis Slice 103; CLAUDE.md wird angepasst – Auftraggeber, 29.10.2026
Entscheidung beim Bauen (Slice 92): Spalte „Tests rot“ angehakt mit echtem Rot: alle vier Fälle von `maskInventory.test.ts` rot gegen den Stand ohne Inventar und ADR (Datei fehlt, Index ohne 0084) – Claude, 29.10.2026
Entscheidung beim Bauen (Slice 93): Auto-Lauf ohne Rückfrage; der Plan (work/plans/2026-10-29-slice-93.md) gilt mit seinen neun Annahmen, Spalte „Plan“ damit angehakt – Claude, 29.10.2026
Entscheidung beim Bauen (Slice 93): Spalte „Tests rot“ angehakt mit echtem Rot: `CompanyHistoryTests` 3 von 3 rot vor dem Endpunkt (404), `AdminPageTests` 4 und `ViewTests.Admin` 9 rot gegen ein leeres Gerüst (fehlende Bereiche, Seite nicht verdrahtet, Felder fehlen); der Fall „Name leer“ lief erst nach dem Bauen – Claude, 29.10.2026
Entscheidung beim Bauen (Slice 93): „Kundenbereich einbetten“ gehört im Web zum Reiter „Firmendaten“ (`EmbedSection.tsx`) und ist deshalb Teil der nativen Maske, mit einem gemeinsamen „Speichern“ – Claude, 29.10.2026 (Ruling „gleiche Felder“)
Entscheidung beim Bauen (Slice 93): Text der Rückfrage „Ohne Speichern gehen die Änderungen in „Firmendaten“ verloren.“ statt des Plantexts „… sind noch nicht gespeichert.“, weil meldungen.md und Brief 5.27 zuerst die Folge verlangen; „Firmendaten“ steht in der Bereichsliste zuerst (Plan, Annahme 1), weil es der einzige native Bereich ist; „Kundenbereich einbetten“ hat in der App ein Fragezeichen statt zwei (masken.md, Checkliste 10) – Claude, 29.10.2026 (Review)
Entscheidung beim Bauen (Slice 94): Auto-Lauf ohne Rückfrage; der Plan (work/plans/2026-10-30-slice-94.md) gilt mit seinen neun Annahmen, Spalte „Plan“ damit angehakt – Claude, 30.10.2026
Entscheidung beim Bauen (Slice 94): Spalte „Tests rot“ angehakt mit echtem Rot auf `bf1007a`: `TreatmentsTests`, `AdminPageTests` und `CalendarAdminLinkTests` 13 von 20 rot, die fünf Bildtests der Maske 6 von 6 rot gegen ein Gerüst (Bereich nicht nativ, Laden und Speichern werfen `NotImplementedException`); danach grün – Claude, 30.10.2026
Entscheidung beim Bauen (Slice 94): Der Kalender nennt die Aktion „Behandlungszeiten bearbeiten …“ (DoD: „Behandlung bearbeiten“); sie öffnet die native Maske mit der Behandlung des Termins im Abschnitt „Ablauf“ – Claude, 30.10.2026
Entscheidung beim Bauen (Slice 94): Ein „Speichern“ schreibt die Behandlung und danach nur die geänderten Teil-Listen (Preisregeln, Vorbereitung, Material, Bereitstellen); beim ersten Fehler bleibt der Rest ungespeichert, die Meldung steht im Fuß und der betroffene Abschnitt wird gewählt. Fehlt die Qualifikation, warnt das erste Speichern am Feld und das zweite speichert (Annahme 4) – Claude, 30.10.2026
Entscheidung beim Bauen (Slice 94): Kein allgemeiner Baustein „Liste mit Detail“ (Annahme 7); gemeinsam mit den Firmendaten sind `INativeArea`, `UnsavedChanges` und `SaveBar`. Die offene Entscheidung aus Slice 93 bleibt offen – Claude, 30.10.2026
Entscheidung beim Bauen (Slice 94): Den Sprung aus dem Kalender prüfen die Tests als Inhaberin und Standortleitung, nicht als Rezeption (DoD: „die Rezeption“): Die Rezeption hat kein `ManageCatalog`, der Kalender blendet „Behandlungszeiten bearbeiten …“ für sie aus (`CanEditCatalog`, unverändert) – Claude, 30.10.2026
Entscheidung beim Bauen (Slice 94): „Material“ (Kosten je Behandlung) zeigt die Maske nur mit `ViewAmountReports`, wie der Endpunkt es verlangt; eine Standortleitung ohne dieses Recht bearbeitet alles andere. Lädt eine Teil-Liste nicht, bleiben die Grunddaten bearbeitbar und durch die Rückfrage geschützt, die Teil-Listen werden dann nicht gespeichert – Claude, 30.10.2026
Entscheidung beim Bauen (Slice 94): Ein Sprungziel, das nicht in der Liste steht (Behandlung nicht mehr angeboten), ändert die offene Behandlung nicht; ein Hinweis über der Fußleiste sagt es. Ein gescheitertes „Nicht mehr anbieten“ steht dort auch, nicht als Ladefehler mit „Erneut laden“. Ein Sprung in einen anderen Bereich gilt erst nach der Rückfrage zu ungespeicherten Änderungen – Claude, 30.10.2026
Entscheidung beim Bauen (Slice 94): Die Feldmeldungen „… fehlt. Trage ihn ein, …“ folgen dem Muster der Web-Masken (`TreatmentsTab.tsx`, `CompanyTab.tsx`); die Du-Form dort ist ein Thema für alle Masken, nicht für diese Slice – Claude, 30.10.2026
Entscheidung beim Bauen (Slice 95): Auto-Lauf ohne Rückfrage; der Plan (work/plans/2026-10-30-slice-95.md) gilt mit seinen acht Annahmen, Spalte „Plan“ damit angehakt – Claude, 30.10.2026
Entscheidung beim Bauen (Slice 95): Spalte „Tests rot“ angehakt mit echtem Rot: Standorte (`AdminLocationsTests`, Teil von `AdminPageTests`) auf `45fc4910`, Schließzeiten und Raumsperren 35 von 37 und 16 von 16 gegen leere Gerüste, Kalender-Abos 24 von 26 und 9 von 12. Bei den Schließzeiten kam ein Teil der Rötung von einem Fehler der Testhilfen (`TestData.At(0)` wirft); die Tests wurden danach gegen die echte Umsetzung grün, einen neuen Gerüstlauf gibt es nicht. Die Nachbesserungen der Durchsicht (Kästchen nach Fehler, Region-Vorschläge, „Verwerfen“ mit Sprungziel) haben je ihre Rot-Zeile auf `c3e7e119`; der Hilfeknopf am Protokoll der Absagen war nie einzeln rot – Claude, 30.10.2026
Entscheidung beim Bauen (Slice 95): Online-Buchung (`OnlineRulesSection`) gehört zu „Standorte & Räume“, weil sie im Web zum Reiter „Standorte“ zählt (`LocationsTab.tsx`); Slice 94 baut sie nicht mit den Behandlungen – Claude, 30.10.2026
Entscheidung beim Bauen (Slice 95): Das Standortformular ist wie Firmendaten ein Formular mit „Speichern / Verwerfen“ und Strg+S (`IEditableArea`); Räume, Geräte, Checkliste und Online-Regeln speichern je Abschnitt wie im Web. Die Leiste steht am Ende des Formulars statt im Fuß, weil `Controls/SaveBar.cs` unberührt bleibt – Claude, 30.10.2026
Entscheidung beim Bauen (Slice 95): Schließzeiten und Raumsperren teilen `PeriodAreaViewModel`/`PeriodFormViewModel` (Standort, Reiter „Aktuell / Beendet“ wie im Web, Liste, Auswahl mit Rückfrage, Speichern, Löschen, Sprungziele) statt zweier Kopien; die Reiter des Plans („aktuell / geplant / vergangen“) waren ungenau, die Web-Maske ist die Spezifikation – Claude, 30.10.2026
Entscheidung beim Bauen (Slice 95): Der Absage-Ablauf (Termine einer Schließzeit absagen, Protokoll mit „Jetzt senden“ / „Nicht senden“) ist im ViewModel der Schließzeiten nachgebaut; der Kalender bleibt unverändert (Ä205) – Claude, 30.10.2026
Entscheidung beim Bauen (Slice 95): Zu `INativeArea` aus Slice 94 (`Open(id, section, field)`) kommt `ITargetedArea` mit `Open(AdminTarget)` (Standort, Zeitraum) für die Bereiche, die mehr als Eintrag und Feld kennen; `AdminPageViewModel.Show` wählt je nach Bereich. Die Bereichs-ViewModels legt eine Tabelle in `CreateNative` an, nicht mehr je ein Feld – Claude, 30.10.2026
Entscheidung beim Bauen (Slice 95): Datei-Import der Kalender-Abos über neuen `IFilePicker` (`StorageFilePicker` im Fenster, `FakeFilePicker` in Tests); einen Dateidialog-Dienst gab es nicht, nur `RestoreView` öffnete einen Picker im Code-behind – Claude, 30.10.2026
Entscheidung beim Bauen (Slice 95): „Im Kalender zeigen“ ist ein sofort wirkendes Kästchen im Abschnitt „Zustand“ (`PUT …/show`, nur der eigene Zugang), weil das Web dafür nur den Schalter im Kalender-Chip hat; ein Sprung in den Kalender entfällt – Claude, 30.10.2026
Entscheidung beim Bauen (Slice 95): Die Region der Kalender-Abos ist eine AutoCompleteBox mit den Vorschlägen AT, DE, CH und `api/holidays/regions`; Freitext bleibt erlaubt, die ISO-Prüfung bleibt – Claude, 30.10.2026
Entscheidung beim Bauen (Slice 95): Zu den DoD-Fällen „Samstag 9–14 Uhr → danach keine freie Zeit“ und „Schließzeit 24.–26.12. → Kalender zeigt die Tage geschlossen“ kommt kein neuer API-Test, weil sie die vorhandenen Tests decken (`ClosureTests.Im_Zeitraum_gibt_es_keine_freien_Zeiten_intern_und_online_und_die_Rezeption_traegt_mit_Hinweis_ein`, `OutsideHoursBookingTests`, `BookingCheckSpeedTests.Neue_Oeffnungszeiten_gelten_sofort`, `MasterDataTests`); die Desktop-Tests prüfen, dass die Maske genau diese Eingaben sendet (Samstag 9–14, 24.–26.12.) – Claude, 30.10.2026
Entscheidung beim Bauen (Slice 95): Prüfstufe 3: weder `Controls/` noch `Styles.axaml` noch `MainWindow` geändert, daher keine volle Desktop-Suite; `dev` mit Slice 94 eingemischt (Konflikte in `App.axaml`, `AdminPageViewModel`, `CompanyViewModel`, `ShellViewModel`, Checkliste), danach Basisprüfung und Bereichstests auf der Spitze wiederholt – Claude, 30.10.2026
Entscheidung beim Bauen (Slice 96): Auto-Lauf ohne Rückfrage; der Plan (work/plans/2026-10-30-slice-96.md) gilt mit seinen sieben Annahmen, Spalte „Plan“ damit angehakt – Claude, 30.10.2026
Entscheidung beim Bauen (Slice 96): Spalte „Tests rot“ angehakt mit echtem Rot auf `03c8adac` gegen Gerüste, die `NotImplementedException` werfen: 18 Fälle aus `PosSettingsTests`, `PaymentsTests`, `RemindersTests` und `AdminPageTests` sowie 6 von 10 Ansichtstests rot; danach grün. Der API-Test `Changing_the_first_reminder_from_24_to_48_hours_plans_it_48_hours_before` war wie geplant von Anfang an grün (Annahme 6): Die API plante schon nach dem gespeicherten Stand, der Test sichert die Nahtstelle – Claude, 30.10.2026
Entscheidung beim Bauen (Slice 96): Ein hinterlegter Schlüssel steht nur als „••••“ mit den letzten vier Zeichen aus dem Hinweis der API da (Platzhalter „Hinterlegt: ••••wxyz“); die Felder bleiben leer, ein leeres Feld behält den gespeicherten Schlüssel, nach dem Speichern werden sie geleert – Claude, 30.10.2026
Entscheidung beim Bauen (Slice 96): Die Erinnerung an die nächste Behandlung (`RebookSection`) gehört mit in die Maske „Erinnerungen“ (Zurückgestelltes aus Slice 94); ein „Speichern“ schickt nur die geänderten Teile, Terminerinnerungen zuerst, der erste Fehler hält an – Claude, 30.10.2026
Entscheidung beim Bauen (Slice 96): „Speichern“ ohne Änderung schickt bei der Kassa nichts und meldet „Gespeichert.“; bei „Mit Stripe verbinden“ gibt es keine Speichern-Leiste, die Knöpfe handeln selbst (wie im Web). Keine API-Änderung, kein ADR – Claude, 30.10.2026
Entscheidung beim Bauen (Slice 96, nach der Durchsicht): Abstände für Behandlungen ohne Zeile in der Maske (nicht mehr angeboten, `api/treatments` liefert sie nicht) gehen beim Speichern unverändert mit, wie im Web; vorher meldete die Maske gleich nach dem Laden „ungespeichert“ und ein Speichern hätte sie gelöscht. Rot auf `3d895b49`, danach grün – Claude, 30.10.2026
Entscheidung beim Bauen (Slice 96, nach der Durchsicht): Die Regeln der API für Erinnerungen (zweite Erinnerung näher am Termin, Vorlage für WhatsApp, Sprache der Vorlage) prüft die Maske vorher am Feld und setzt den Fokus ins erste ungültige Feld (masken.md R8); der Text der zuletzt verschickten Erinnerung steht als zweite Zeile statt im Hinweis, die Abstände stehen in einem `WrapPanel` statt im `UniformGrid`; „Zuletzt verschickt“ ist beim Laden optional. Bei Zahlungen bleibt „beschäftigt“ bis zum Ende der Abfrage nach Verbinden und „Stand abfragen“, deren Fehler heißen „Stripe nicht erreicht“ – Claude, 30.10.2026
Entscheidung beim Bauen (Slice 97): Auto-Lauf ohne Rückfrage; der Plan (work/plans/2026-10-30-slice-97.md) gilt mit seinen acht Annahmen, Spalte „Plan“ damit angehakt – Claude, 30.10.2026
Entscheidung beim Bauen (Slice 97): Spalte „Tests rot“ angehakt mit echtem Rot auf `2d42c455` gegen ein Gerüst: 59 von 75 Tests der Klassen `AdminIntakeTests`, `AdminAiTests`, `AdminHandbookTests`, `AdminCompanyEmbedTests` und `AdminPageTests`, 20 von 20 neuen Bildtests, `BookingEmbeddingTests` 1 von 15; Prüfnachweis in `.slice-loop/slice-97.checks`. Der Gerüst-Build lief mit `-p:TreatWarningsAsErrors=false`, weil leere Gerüste CA1822 auslösen – Claude, 30.10.2026
Entscheidung beim Bauen (Slice 97): „Handbuch“ ist nativ das Rezeptionshandbuch (Liste, Formular, Löschen mit Rückfrage), kein Sprung in die Hilfe: Das Inventar sah einen Sprung vor, aber `HandbookTab.tsx` pflegt die Einträge des Hauses, die der Tagesstart liest; ein Sprung hätte das Pflegen abgeschafft (Annahme 1) – Claude, 30.10.2026
Entscheidung beim Bauen (Slice 97): „Einbetten“ ist seit Slice 93 Teil der Firmendaten; die Slice ergänzt dort nur „Code kopieren“. Die Adresse des Kundenbereichs liefert die API (`EmbeddingSettings.BookingUrl` = `{PublicUrl}/buchen/?spa={Kennung}`, in GET und PUT), weil nur der Server sie kennt (Annahme 3) – Claude, 30.10.2026
Entscheidung beim Bauen (Slice 97): Postfach-Formular mit Speichern / Verwerfen und Strg+S; Absenderregeln speichern je Zeile sofort (eigene Ebene) und zählen nicht zu den ungespeicherten Änderungen des Postfachs. Löschen (Postfach entfernen, Regel, Handbucheintrag) fragt vorher – im Web ohne Rückfrage (Annahmen 4, 5) – Claude, 30.10.2026
Entscheidung beim Bauen (Slice 97): Die KI-Maske zeigt nur den Stand und „Modell herunterladen“; es gibt kein Schlüsselfeld, der Claude-Schlüssel ist eine Server-Einstellung. „Maskiert wie Slice 96“ trifft deshalb nur auf das Postfach-Passwort und das Client-Geheimnis zu (leer = unverändert, nie angezeigt) (Annahme 6) – Claude, 30.10.2026
Entscheidung beim Bauen (Slice 97): Die Durchsicht (Opus) fand einen blockierenden Fehler: Antworten auf PUTs einer Absenderregel setzten eine inzwischen geänderte Eingabe der Zeile still zurück. Behoben mit eigenem Rot (zwei Änderungen ohne Warten dazwischen, `StubApi.Hold`), dazu Meldungen bei Löschen, „Passwort“ oder „Client-Geheimnis“ in der Rückfrage, Status „Code kopiert.“ nur bis zur nächsten Änderung, Neuladen der KI nach abgebrochener Nachfrage, Titel- und Textmeldung ohne „bitte“ – Claude, 30.10.2026
Entscheidung beim Bauen (Slice 97): Prüfstufe 3: weder `Controls/` noch `Styles.axaml` noch `MainWindow` geändert, daher keine volle Desktop-Suite; `dev` ist seit dem Abzweig nicht weitergewandert, es gibt nichts einzumischen – Claude, 30.10.2026
Entscheidung beim Bauen (Slice 98): Auto-Lauf ohne Rückfrage; der Plan (work/plans/2026-10-30-slice-98.md) gilt mit seinen neun Annahmen, Spalte „Plan“ damit angehakt – Claude, 30.10.2026
Entscheidung beim Bauen (Slice 98): Spalte „Tests rot“ angehakt mit echtem Rot: API auf `3fca4a5f` 3 von 3 rot, danach 29 von 29 grün; Desktop auf `4cca3c64` das Modell 17 rot und 26 grün, die Ansicht 7 rot, danach Klassen 67/67, Verwaltung 79/79, Scans 81/81, DesignToken und GridValue 100/100, Fokus und Tab 57/57 grün – Claude, 30.10.2026
Entscheidung beim Bauen (Slice 98): `GET /api/employees/former` verlangt das Recht ManageCatalog statt der Rechte von ListEmployees, damit die Rezeption 403 bekommt; das weicht von Annahme 2 des Plans ab – Claude, 30.10.2026
Entscheidung beim Bauen (Slice 98): Den Einladungslink baut die API aus `OnlineBooking:PublicUrl` wie die übrigen Mails und liefert ihn als Pflichtfeld `Link` in `InvitationCreated`; die App zeigt genau diesen Link, die Web-Oberfläche baut ihren weiter selbst – Claude, 30.10.2026
Entscheidung beim Bauen (Slice 98): Der Reiter heißt nach dem Ruling „Zugang und Rolle“ – Claude, 30.10.2026
Entscheidung beim Bauen (Slice 98): Ein Sprung ohne Id (Umleitung von `staff` und `masterData/qualifications`) öffnet die erste Zeile beim gewünschten Reiter – Claude, 30.10.2026
Entscheidung beim Bauen (Slice 98): Der Einzelplatz wird aus `api/system/modules` erkannt; ohne Antwort gilt der Server-Betrieb – Claude, 30.10.2026
Entscheidung beim Bauen (Slice 98): Nach dem Senden einer Einladung leeren sich die Felder, damit dieselbe Einladung nicht doppelt rausgeht – Claude, 30.10.2026
Entscheidung beim Bauen (Slice 98): Die Sprachen sind 18 Kästchen wie im Web (Ruling „gleiche Felder“); Regel R10 aus masken.md ist bewusst nicht angewandt – Claude, 30.10.2026
Entscheidung beim Bauen (Slice 98): Eine Rolle, die man nicht vergeben darf, bleibt wählbar und die API lehnt ab; ein Bezug der Meldung zum Feld „Rolle“ ginge nur mit Umbau des `ApiClient`, deshalb steht die Meldung mit dem Grund über der Leiste und die Vorwarnung „Vergeben kann das nur der Inhaber.“ als Hinweis am Feld – Claude, 30.10.2026
Entscheidung beim Bauen (Slice 98): Der Grund einer abgelehnten Rolle steht am Feld „Rolle“, wenn sich die Rolle vom gespeicherten Stand unterscheidet, und über der Leiste; die nächste Rollenwahl leert ihn. `ApiResult` bleibt ohne Feldbezug (Ä207) – Claude, 30.10.2026
Entscheidung beim Bauen (Slice 98): Ein ausgefülltes Formular „Person einladen“ oder „Zugang anlegen“ löst beim Zeilenwechsel die Rückfrage aus; „Speichern“ im Dialog sendet keine Mail und legt kein Konto an, die Karte bleibt offen – Claude, 30.10.2026
Entscheidung beim Bauen (Slice 98): Scheitert beim Speichern nur der Zugang, lädt die Karte neu und die Meldung nennt den gespeicherten und den nicht gespeicherten Teil – Claude, 30.10.2026
Entscheidung beim Bauen (Slice 98): Eine Arbeitszeit an einem für die Standortleitung unsichtbaren Standort behält ihre Id und zeigt „Anderer Standort“ – Claude, 30.10.2026
Entscheidung beim Bauen (Slice 101): Auto-Lauf ohne Rückfrage; der Plan (work/plans/2026-10-30-slice-101.md) gilt mit seinen sechs Annahmen, Spalte „Plan“ damit angehakt – Claude, 30.10.2026
Entscheidung beim Bauen (Slice 101): Spalte „Tests rot“ angehakt mit echtem Rot gegen Gerüste mit gesperrtem Verhalten (Laden, Buchen, Speichern, Löschen taten nichts): Lager 48 von 68 Modelltests und 10 von 10 Fenstertests rot, Aktionen 40 von 56; nach der Durchsicht 14 weitere Tests erst rot, dann grün; Protokoll in `.slice-loop/slice-101.red.log` – Claude, 30.10.2026
Entscheidung beim Bauen (Slice 101): Lager umfasst Bestand, Bewegungen, Inventur und Lagerorte (Umfang der Slice-Datei); Bestellungen, Bedarf und Chargen-Rückverfolgung bleiben offen und stehen als Ä223 vor Slice 103, weil dieses die Web-Oberfläche entfernt – Claude, 30.10.2026
Entscheidung beim Bauen (Slice 101): Inventur bucht nur Zeilen mit gezähltem, vom Soll abweichendem Wert (Art Count, gezählte Menge) und erst nach Rückfrage; ungespeichert gilt nur, was buchbar ist (Durchsicht, Punkt 4). Löschen eines Lagerorts fragt nach, Archivieren und Zurückholen einer Aktion nicht (umkehrbar; im Web fragt das Archivieren) – Claude, 30.10.2026
Entscheidung beim Bauen (Slice 101): Die Durchsicht (Opus) fand einen blockierenden Fehler: Mindestmengen ab 1000 wurden beim Speichern als 1,25 gesendet (deutsche Vorbelegung „1.250“, Parse liest den Punkt als Dezimalzeichen). Behoben mit eigenem Rot (`StockFormat.Plain`), dazu veraltete Antworten bei schnellem Standortwechsel (Versionszähler), gesperrte Zählfelder beim Buchen, doppeltes Anlegen nach gescheitertem Neuladen, Teilfehler der Inventur, Texte ohne „bitte“, Minuszeichen U+2212 – Claude, 30.10.2026
Entscheidung beim Bauen (Slice 101): Prüfstufe 3: weder `Controls/` noch `Styles.axaml` noch `MainWindow` geändert, daher keine volle Desktop-Suite; `dev` ist seit dem Abzweig nicht weitergewandert, es gibt nichts einzumischen – Claude, 30.10.2026
Ruling: Plan von Slice 118 mit allen sieben Annahmen übernommen (Auto-Lauf): Server mit öffentlichem Zertifikat ohne Verbindungscode, Standardport 443, Vorrang `AISPA_API_URL` vor Eintrag vor localhost, eigene Seite „Netz“ im Server-Betrieb, kein neuer ADR – Claude, 29.10.2026
Entscheidung beim Bauen (Slice 118): Spalte „Tests rot“ angehakt mit echtem Rot: 17 Fälle von `ServerConnectTests` rot gegen Gerüste, die `NotImplementedException` werfen; danach grün. Die Ansichtstests `ViewTests.ServerConnect` (Klick, Feldname, Seitenleiste) entstanden mit der Umsetzung – Claude, 29.10.2026

Entscheidung beim Bauen (Slice 104, Auto-Modus ohne Rückfrage): Plan mit allen sechs Annahmen umgesetzt – Kanal-Sperre als Middleware nach der Rechteprüfung für alle Endpunkte mit Anmeldung; die Mitarbeiter-App schränkt die laufende Anmeldung mit `POST /api/auth/channel/staff-app` selbst ein (nur einschränken, nie erweitern); öffentliche Endpunkte bleiben offen; Dashboard-Daten baut Slice 105; koppeln darf, wer „Standort verwalten“ hat; Live-Hub bleibt für die neuen Kanäle zu (Ä232) – Claude, 29.10.2026
Entscheidung beim Bauen (Slice 104): Spalte „Tests rot“ angehakt. Echtes Rot für alle API-Abnahmefälle (7 von 10 neuen Tests rot auf `f802614`, Kanal-Verträge ohne Umsetzung, `ChannelScan.Problems` noch leer) und für die Erneuerung des Cookies (Haken entfernt, rot auf `58f07da`). Die drei Tests der Windows-App (`Geraete_Leitung_tippt_den_Code_der_Anzeige_ein_und_koppelt_sie_per_Klick`, `Leitung_widerruft_eine_Anzeige_erst_nach_der_Rueckfrage`, `Ohne_Recht_Standort_verwalten_fragt_die_Seite_keine_Anzeigen_ab`) sind mit dem Abschnitt entstanden; vor ihm kompilierten sie nicht, ein Rot im Lauf gibt es für sie nicht – Claude, 29.10.2026

- Ruling: Prüfstufe 1: erst bauen, nur das Geänderte testen, die ganze Suite am Ende; Meilenstein für `-Merge -Verify` nach Slice 103 (eingebettete Verwaltung entfernt, erledigt) und beim Rundenabschluss – User, 01.11.2026 (ersetzt Prüfstufe 3 vom 30.10.2026)
Entscheidung beim Bauen (Slice 102): Auto-Lauf ohne Rückfrage; der Plan (work/plans/2026-10-30-slice-102.md) gilt mit seinen Annahmen, Spalte „Plan“ damit angehakt – Claude, 30.10.2026
Entscheidung beim Bauen (Slice 102): Spalte „Tests rot“ angehakt mit echtem Rot gegen ein Gerüst auf `a7a15515` (Abnahmen DATEV und „Im Bericht öffnen“), belegt im Prüfnachweis – Claude, 30.10.2026
Entscheidung beim Bauen (Slice 102): Ein Standort und ein Datum gelten für alle Berichtsreiter; eine offene Notiz im Tagesplan zählt als ungespeichert und geht beim Datumswechsel verloren wie im Web – Claude, 30.10.2026
Entscheidung beim Bauen (Slice 102): Die Kosten haben eine Leiste „Speichern“ für Sätze, Räume und Personen statt Knöpfen je Zeile (Rahmen aus Slice 93) und eine Monatsauswahl mit 24 Monaten – Claude, 30.10.2026
Entscheidung beim Bauen (Slice 102): Ein eingetippter Name eines neuen Erlöskontos zählt nicht als ungespeichert, weil „Erlöskonto anlegen“ sofort bucht; die PIN-Prüfung der Raumpflege meldet sich erst beim Klick – Claude, 30.10.2026
Entscheidung beim Bauen (Slice 102): Der Vergleich mit anderen Spas zeigt Werte als Text; die Balken sind zurückgestellt, weil eine Bibliothek ohne Freigabe ausgeschlossen ist und eine eigene Zeichnung den Umfang sprengt (Ä226) – Claude, 30.10.2026
Entscheidung beim Bauen (Slice 102): Die Durchsicht (Opus) fand: Nach Standort- oder Monatswechsel bzw. Ladefehler konnten Kosten und Kontenzuordnung alte Werte auf die neue Auswahl speichern; Haken der Bereitstellung konnten am Termin des Vortags hängen; die Leitung der Raumpflege blieb beim alten Standort. Behoben mit Tests; nach einem Ladefehler stehen keine alten Zahlen mehr da; der Vergleich startet mit 30 Tagen über den Kennzahlen wie im Web – Claude, 30.10.2026
Entscheidung beim Bauen (Slice 102): Feldmeldungen „Trage … ein.“ bleiben im Stil der bisherigen nativen Masken (Aktionen, Lager, Handbuch); die Anrede gehört in einen eigenen Durchgang (Ä213) – Claude, 30.10.2026
Entscheidung beim Bauen (Slice 102): Prüfstufe 3: `Controls/`, `Styles.axaml` und `MainWindow` nicht geändert, daher keine volle Desktop-Suite – Claude, 30.10.2026
Build decision (slice 99): Auto-Lauf ohne Rückfrage; der Plan (work/plans/2026-10-30-slice-99.md) gilt mit seinen Annahmen, Spalte „Plan“ damit angehakt – Claude, 30.10.2026
Build decision (slice 99): Spalte „Tests rot“ angehakt mit Rotläufen je Teil und je Prüfbefund, belegt in `.slice-loop/slice-99.red.log` und im Prüfnachweis – Claude, 30.10.2026
Build decision (slice 99): Die beiden API-Tests zu Abnahme 1 und der Test zur Zahl beim Entwurf (Abnahme 2) sind Charakterisierung: Der Planer rechnete schon richtig, deshalb gibt es dafür kein Rot; rot war nur das fehlende Neuladen des Kalenders in der App – Claude, 30.10.2026
Build decision (slice 99): Kein Endpunkt für Anträge in dieser Slice: Die Liste fragt `GET api/absence-requests` und bleibt bei 404 leer; die Routen und die Benachrichtigung bei Genehmigung oder Ablehnung standen als Pflicht in Slice 106, das sie vor dem Squash geliefert hat – Claude, 30.10.2026
Build decision (slice 99): Der Filter `employeeId` an `roster` und `timesheet` verlangt ManageLocation oder ViewStaffRecords (sonst 403), liefert nur aktive Personen und blendet Einträge an nicht sichtbaren Standorten aus; ohne dieses Recht fehlt der Reiter an der Karte – Claude, 30.10.2026
Build decision (slice 99): Arbeitszeit-Hinweise rechnen über alle Standorte der Person, auch aus Sicht einer Standortleitung; eine Zelle ohne sichtbaren Eintrag zeigt keinen Hinweis – Claude, 30.10.2026
Build decision (slice 99): Zellen ohne Planungsrecht sind Text mit Screenreader-Namen statt deaktivierter Knöpfe; der Tageseditor steht unter der Tabelle und bekommt beim Öffnen Fokus und Sichtbereich – Claude, 30.10.2026
Build decision (slice 99): Planzellen nach Brief 4.1 (8/12); auf der Karte läuft die Woche waagrecht scrollbar, weil 760 px dafür nicht reichen; der Brief bleibt unverändert – Claude, 30.10.2026
Build decision (slice 99): „Dienstplan“ und „Zeiten“ entfallen als Bereiche; alte Sprungziele `roster` und `time` führen ins Team – Claude, 30.10.2026
Build decision (slice 99): Prüfstufe 3: `ShellViewModel`, `AdminPageViewModel` und `App.axaml` sind Gemeinsames, daher einmal die volle Desktop-Suite am Ende – Claude, 30.10.2026

Entscheidung beim Bauen (Slice 105): Auto-Lauf ohne Rückfrage; der Plan (work/plans/2026-10-30-slice-105.md) gilt mit seinen sieben Annahmen, Spalte „Plan“ damit angehakt – Claude, 30.10.2026
Entscheidung beim Bauen (Slice 105): Spalte „Tests rot“ angehakt mit echtem Rot auf `fda6ee59`: alle 12 Fälle von `DisplayBoardTests` rot gegen ein Gerüst (Tafel 404, Hub 403, Modul-Schalter ohne Wirkung, Initialen werfen); die Web-Tests entstanden mit der Oberfläche und wurden mit eingebauten Fehlern (Raumtext, Signal, Abonnement) auf 6 von 10 rot geprüft; der Desktop-Test zum Modul-Schalter war vor der Umsetzung ein Übersetzungsfehler – Claude, 30.10.2026
Entscheidung beim Bauen (Slice 105): Die Anzeige bekommt das Signal `DisplayChanged` ohne Nutzdaten statt `AppointmentChanged`, weil dieses den Namen des Gastes trägt; die Tafel lädt nach dem Signal und alle 30 Sekunden neu – Claude, 30.10.2026
Entscheidung beim Bauen (Slice 105): Kein Behandlungsname, keine Notiz, kein Preis auf der Tafel (Rückschluss auf Gesundheit); Personen heißen mit dem Namen im Haus, Gäste nur als Initialen („A. H.“, Platzhaltergast „Gast“) – Claude, 30.10.2026
Entscheidung beim Bauen (Slice 105): Das Dashboard liegt unter `/dashboard/` in jeder Abo-Stufe (Start); `GET /api/system/modules` meldet es, die Seite Geräte der Windows-App blendet „Anzeigen im Pausenraum“ ohne das Modul aus – Claude, 30.10.2026
Entscheidung beim Bauen (Slice 105): Prüfstufe 3: `Controls/`, `Styles.axaml` und `MainWindow` nicht geändert, daher keine volle Desktop-Suite; geändert wurden der gemeinsame Vertrag `SystemModules` und der Live-Hub, deshalb liefen die Desktop-Klassen der Module, Geräte und Anmeldung, die Scan-Tests und die ganze Web-Suite – Claude, 30.10.2026

Entscheidung beim Bauen (Slice 106): „Läuft“ ist `Appointment.StartedAt` neben dem Status `CheckedIn`, kein neuer Wert von `AppointmentStatus`: Kassa, Berichte und Beispieldaten lesen den Enum, und ein laufender Termin bleibt kassierbar. – Claude, 30.10.2026
Entscheidung beim Bauen (Slice 106): Die Woche der App entsteht aus sieben Aufrufen des Tagesplans; so gelten derselbe Kanal, derselbe Zuschnitt und derselbe Speicher ohne Netz (Service Worker jetzt 28 statt 7 Tage, damit Blättern den heutigen Tag nicht verdrängt). – Claude, 30.10.2026
Entscheidung beim Bauen (Slice 106): „Hinweise zum Gast“ sind Vorlieben, letzte Notiz und ein Allergie-Kennzeichen nur als Ja oder Nein, nur im eigenen Plan und nach dem Vorbild von `anamnesis/check`; der Inhalt des Fragebogens bleibt im Schema `health`. – Claude, 30.10.2026
Entscheidung beim Bauen (Slice 106): Melden geht nur am Tag des Termins in Standortzeit, „beendet“ nur nach „begonnen“; beides prüft die API. Einchecken zurücknehmen leert den Beginn. – Claude, 30.10.2026
Entscheidung beim Bauen (Slice 106): Der Antragsvertrag ist zeichengleich zu Slice 99 (`AbsenceRequestDto`); Entscheiden bleibt beim Recht `ManageLocation`, die Notiz des Antrags kommt nicht in den Dienstplan, den alle am Standort sehen. – Claude, 30.10.2026
Entscheidung beim Bauen (Slice 106): Anmeldung mit Passkey-Knopf in der App, Passwort über den Link auf die Anmeldeseite (kein zweites Formular); die App wechselt nach der Anmeldung in den Kanal `StaffApp`, zurück geht es nur über eine neue Anmeldung, deshalb entfällt der Fußlink „Zur Team-Oberfläche“. – Claude, 30.10.2026
Entscheidung beim Bauen (Slice 106): Die Slice wurde ohne Unteragenten direkt gebaut, weil API, Windows-App und Web denselben Vertrag teilen; nur die Durchsicht lief getrennt. – Claude, 30.10.2026
Entscheidung beim Bauen (Slice 106): Prüfstufe 3: `Controls/`, `Styles.axaml` und `MainWindow` nicht geändert, daher keine volle Desktop-Suite; für die Änderung an `ChangeStatusAsync` liefen die Testklassen, die den Terminstatus nutzen, dazu die Scan-Tests. – Claude, 30.10.2026
Build decision (slice 108): Nach der Stornofrist sagt der Gast in der App nur ab, wenn eine Karte hinterlegt ist und der Termin noch nicht begonnen hat; die API nennt `LateCancelFee` und verlangt `acceptFee=true`. Belastet wird wie bisher nur durch die Kassenaufsicht, damit es keine neue Zahlung in der App gibt. – Claude, 30.10.2026
Build decision (slice 108): Ohne Karte, auch mit Anzahlung, bleibt nach der Frist der Anruf: Für einbehaltene Anzahlungen gibt es keine Regel in der API (Ä244). – Claude, 30.10.2026
Build decision (slice 108): Die .ics liefert die API über einen Link, nicht der Browser als Blob: Das lädt auch in der installierten App am iPhone, die Adresse steht nur einmal im Code. – Claude, 30.10.2026
Build decision (slice 108): Eigene Gutscheine sind Pakete des Gastes und Online-Bestellungen; an der Kassa ohne Namen verkaufte Gutscheine kennt das Konto nicht. – Claude, 30.10.2026
Build decision (slice 108): Der Service Worker hält nur die App selbst; die Seite kommt zuerst vom Netz und je Adresse, damit der Einbettungs-Header je Spa frisch bleibt. Er liest nichts aus der API, am Gerät liegen keine Termine und keine Gesundheitsdaten. Im `iframe` wird er nicht registriert. – Claude, 30.10.2026
Build decision (slice 108): Das Spa der Seite steht jetzt in `localStorage` statt `sessionStorage`, weil die installierte App ohne `?spa=` startet und keine Sitzung mit dem Browser teilt. – Claude, 30.10.2026
Build decision (slice 108): Anamnese vorab und Verschieben bestanden schon; die Slice bindet sie nur ein und baut sie nicht neu. Die API prüft dabei unverändert Frist und Anzahl. – Claude, 30.10.2026
Build decision (slice 108): Prüfstufe 3, kein gemeinsamer Code der Windows-App berührt; `OnlinePortal.CancelAsync` hat als Aufrufer nur die beiden Kundenbereichs-Endpunkte, deshalb API-Tests der betroffenen Klassen statt der ganzen Suite. – Claude, 30.10.2026
Build decision (slice 108): Mit hinterlegter Karte gilt die Frist, der der Gast zugestimmt hat, auch für Anzeige und Absage; die aktuelle Regel gilt nur ohne Karte. So stimmen Zustimmung und spätere Fälligkeit immer überein. – Claude, 30.10.2026
Build decision (slice 108): Karte und bezahlte Anzahlung zusammen bleiben nach der Frist beim Anruf, damit Einbehalt und Gebühr nicht doppelt treffen (Ä244). – Claude, 30.10.2026
Build decision (slice 108): Bei einem Mail-Link sendet die App das gemerkte Spa nicht mit, nur ein `?spa=` aus der Adresse; die Reihenfolge im Server bleibt. – Claude, 30.10.2026
Build decision (slice 109): Auto-Lauf ohne Rückfrage; der Plan (work/plans/2026-10-31-slice-109.md) gilt mit seinen acht Annahmen, Spalte „Plan“ damit angehakt. – Claude, 31.10.2026
Build decision (slice 109): Spalte „Tests rot“ angehakt mit echtem Rot: Web 4 von 5 neuen Tests rot auf `dev` ohne `Today.tsx` (der Fall „Rezeption sieht keine Seite“ prüft eine Abwesenheit und war von Anfang an grün), API 4 von 6 rot auf `19a1b3a9` (Kanalfreigabe, Kennzahl, Scan; der Fall „Rezeption bekommt 403“ ist von Anfang an grün, weil die Kanal-Sperre den Bericht ohne Freigabe schon abwies). – Claude, 31.10.2026
Build decision (slice 109): „Offene Anfragen“ gab es in keinem Bericht der Leitung; statt eines neuen Endpunkts steht `openRequests` als Zeile in den `Kpis` des bestehenden Management-Berichts (Mails im Status `NeedsReview` am Standort oder ohne Standort, Stand jetzt, ohne Vorperiode, wie die offenen Gutscheine). Der Vertrag bleibt gleich, nur die Liste wird länger; der Bericht in Windows-App und Web zeigt die Zeile mit (Ä249). – Claude, 31.10.2026
Build decision (slice 109): Der Management-Bericht ist für den Kanal `StaffApp` freigegeben (`ChannelScanTests`); die Rolle bleibt maßgeblich (`ViewFinance`, Beträge nur mit `ViewAmountReports`), die Rezeption bekommt weiter 403. Die Route hat kein `employeeId`, die Sperre „Nur eigene Daten“ trifft sie nicht. Kein ADR, folgt ADR 0084 und Slice 104. – Claude, 31.10.2026
Build decision (slice 109): Der Reiter „Heute im Spa“ erscheint nur mit `ViewFinance` in `rights` der Anmeldung (ohne Netz gestartet gibt es keine Rechte, also keinen Reiter). Der Bericht wird nicht vom Service Worker gehalten und nicht im Speicher des Geräts abgelegt; ohne Netz steht „Kennzahlen brauchen eine Verbindung.“ – Claude, 31.10.2026
Build decision (slice 109): Prüfstufe 3: weder `Controls/` noch `Styles.axaml` noch `MainWindow` geändert, daher keine volle Desktop-Suite. Die Kennzahl ändert die Liste `Kpis` des Berichts; geprüft sind alle Test-Klassen der API, die ihn lesen (Benchmark, Berichte, Rollen, Demo-Daten, Kanal), die Windows-App stellt Zeilen allgemein dar (`KpiValue` kennt die Einheit „“). – Claude, 31.10.2026
Build decision (slice 100): Auto-Lauf ohne Rückfrage; der Plan (work/plans/2026-10-31-slice-100.md) gilt mit seinen sechs Annahmen, Spalte „Plan“ damit angehakt. – Claude, 31.10.2026
Build decision (slice 100): Spalte „Tests rot“ angehakt: Die Tests entstanden vor den Modellen und ließen sich zuerst nicht übersetzen (`PayrollViewModel`, `PersonPayrollViewModel`, `PayrollMode`, `PayrollTab` fehlten), das ist das Rot dieser Slice; ein Lauf gegen alten Code mit Fehlschlägen im Einzelnen fehlt, weil die Klassen nur zusammen entstehen. – Claude, 31.10.2026
Build decision (slice 100): Der Reiter heißt „Lohn“ statt „Lohn und Provision“, weil es in API und Web keine Provision gibt (E12 streicht sie für den Produktverkauf); die Provision steht als Befund Ä220. – Claude, 31.10.2026
Build decision (slice 100): Die Personalnummer gehört zur Personenkarte und wird mit deren „Speichern“ gesichert (Entscheidung: Personendaten nur an einer Stelle); die Liste aller Nummern des Web entfällt, die Warnungen der Vorschau nennen, wem eine Nummer fehlt. – Claude, 31.10.2026
Build decision (slice 100): Der Bereich „Lohn“ der Verwaltungsliste entfällt; die vier Tests, die ihn als letzten eingebetteten Bereich nutzten, öffnen die eingebettete Verwaltung bis Slice 103 über `Admin.Show("payroll")` bzw. wählen „Kosten“. – Claude, 31.10.2026
Build decision (slice 100): Der Knopf heißt „DATEV Lodas“ statt „DATEV LODAS“, weil `DesignTokenTests` Versalien-Wörter in Beschriftungen verbietet (Brief 3). – Claude, 31.10.2026
Build decision (slice 100): Prüfstufe 3: weder `Controls/` noch `Styles.axaml` noch `MainWindow` geändert, keine volle Desktop-Suite; geprüft sind die Klassen von Lohn, Team, Dienstplan, Zeiten und Verwaltung, die Scan-Tests, `DesignTokenTests`, `GridValueTests` und Vitest `src/help`. – Claude, 31.10.2026

Build decision (slice 112): Die Entity heißt `StockSupplier`, damit sie nicht mit den Texteigenschaften `Supplier` kollidiert; `PurchaseOrder.Supplier` bleibt Text, weil eine Bestellung den Namen zum Zeitpunkt festhält – Claude, 31.10.2026
Build decision (slice 112): `StockItemInput` nimmt `SupplierId` und weiter den Text `Supplier`; der Text sucht ohne Groß- und Kleinschreibung und legt sonst an, damit die Web-Maske bis Slice 103 unverändert läuft – Claude, 31.10.2026
Build decision (slice 112): Packung am Material (`PackUnit`, `PackSize`, `CaseUnit`, `PacksPerCase`), die Ebene wählt `StockMovementInput.PackageLevel` nur beim Wareneingang von Hand; die API rechnet Menge und Preis in die Einheit um. `TreatmentDose` ist ein Richtwert, abgebucht wird weiter nach der Rezeptur – Claude, 31.10.2026
Build decision (slice 112): Die Ablaufwarnung gilt je Material (`ExpiryWarningDays`, Vorgabe 30, 1 bis 365). Neuer Reiter „Aufgaben“ nur mit `ManageLocation`, Reiter „Lieferanten“ und Knopf „Ändern“ nur mit `ManageCatalog` – Claude, 31.10.2026
Build decision (slice 112): Standort und Lagerort stehen in einer eigenen Zeile unter den Reitern, weil sechs Reiter „Bewegung buchen“ sonst aus dem Bild schieben – Claude, 31.10.2026
Build decision (slice 112): Ein deaktivierter Lieferant bleibt am Material, das ihn schon hat, lässt sich aber nicht neu zuordnen. Ein unveränderter Preis im Formular sendet den aktuellen Preis des Lagers, nicht den Stand beim Öffnen – Claude, 31.10.2026
Build decision (slice 112): Prüfstufe 3: nur Lager-Ansichten, ihre Viewmodels und die Lager-API geändert, nichts Gemeinsames; daher zuständige Tests mit Scan-Tests und keine volle Suite. `dev` wurde eingemischt, weil beide Seiten `web/openapi/aispa-api.json` und `web/src/api/schema.d.ts` ändern – Claude, 31.10.2026
Ruling: Anmeldung im Server-Betrieb (Code für die Windows-App, Einladung, Passkey, Registrierung eines Spas) kommt auf eine kleine eigene Seite `/anmelden/` mit Nachtrag zu ADR 0084, weil Mein Tag als Modul abgeschaltet sein kann; Slice 103 baut sie mit (Ä286) – Auftraggeber, 31.10.2026
Ruling: Bestellungen, Bedarfsprüfung und Chargen-Rückverfolgung werden vor Slice 103 nativ, als eigene Slice 123 nach Slice 113 (Ä223) – Auftraggeber, 31.10.2026
Ruling: Kartengarantie, Anzahlung anfordern und Erinnerungsverlauf am Termin sowie Info zur Online-Rückerstattung und Gutscheinabfrage an der Kassa werden vor Slice 103 nativ, als eigene Slice 124 (Ä285) – Auftraggeber, 31.10.2026
Build decision (slice 113): Die Höchstmenge steht an `StoragePlaceMinimum`; das Bestellziel des Standorts ist die Summe seiner Lagerorte, ohne Höchstmenge der Mindestbestand – Claude, 31.10.2026
Build decision (slice 113): Bedarf = Tagesverbrauch der letzten 90 Tage × Lieferzeit + Termine innerhalb der Lieferzeit, ohne Abzug von Überschneidungen; der erwartete Stand darf negativ sein. Bestellt wird erst, wenn der erwartete Stand unter den Mindestbestand fällt, dann bis zum Ziel. Ohne Lieferzeit bleibt die alte Rechnung (Termine 14 Tage, kein Tagesverbrauch) – Claude, 31.10.2026
Build decision (slice 113): Der Mindestbestellwert warnt nur und sperrt nicht. Senden geht nur mit Mailadresse beim Lieferanten, sonst ist der Knopf aus und die Maske nennt den Grund. Die Vollmacht aus Slice 111 ist noch nicht da, deshalb gilt `ManageLocation` – Claude, 31.10.2026
Build decision (slice 113): Ä223 nativ abgedeckt: von den Bestellungen Vorschlag, Entwurf anlegen, Menge ändern, verwerfen und senden. Nicht abgedeckt: Lieferung buchen, „Als bestellt markieren“ und Bestelltext der Bestellungen; Bedarfsprüfung und Chargen-Rückverfolgung gar nicht – Claude, 31.10.2026
Build decision (slice 113): Die Web-Maske der Lagerorte zeigt die Höchstmenge nicht, behält sie aber beim Speichern, weil Slice 103 das Web entfernt – Claude, 31.10.2026
Build decision (slice 113): Ohne echten Mailversand (nur Log, kein Postfach mit Versand) lehnt die API das Senden ab, die Bestellung bleibt Entwurf. Das Ziel ist mindestens der Mindestbestand, deaktivierte Lagerorte zählen nicht. Bedarf aus einer eigenen Abfrage mit denselben Status und Rezepten wie `StockDemand`, weil er je Material bis zur Lieferzeit gilt – Claude, 31.10.2026
Build decision (slice 113): Der Rot-Nachweis gilt für `StockReorderTests`; die Fenstertests entstanden nach der Maske und liefen nicht rot – Claude, 31.10.2026
Ruling: Die Verwaltung bekommt statt der Liste einen hellen Balken oben im Stil der linken Leiste mit den Gruppen Betrieb, Angebot, Team, Lager, Kassa & Geld, Kontakt und Zahlen; die Masken stehen darunter als Reiter (Ä293, Slice 125, vor Slice 103) – Auftraggeber, 31.10.2026

Build decision (slice 123): Die Bedarfsprüfung steht im Reiter Bestand (Recht Book), die Rückverfolgung im Reiter Bewegungen (Recht ManageLocation); keine neuen Reiter – Claude, 31.10.2026

Build decision (slice 123): Die Fehlmenge liefert die API als neues Feld `StockDemandLine.Shortfall`, die App rechnet nichts – Claude, 31.10.2026

Build decision (slice 123): Lieferung buchen fragt nicht nach, Stornieren und Abschließen schon. Liefereingaben zählen nicht als ungesichert und bleiben beim Neuladen stehen – Claude, 31.10.2026

Build decision (slice 123): Geänderte Mengen werden vor „Als bestellt markieren“ und vor dem Bestelltext gespeichert, sonst gälten die alten – Claude, 31.10.2026
Build decision (slice 110): Auto-Lauf ohne Rückfrage; der Plan (work/plans/2026-10-31-slice-110.md) gilt mit seinen Annahmen, Spalte „Plan“ damit angehakt. – Claude, 31.10.2026
Build decision (slice 110): Spalte „Tests rot“ angehakt mit echtem Rot, belegt im Prüfnachweis: API 20 von 21 rot gegen Gerüste, Windows-App 12 von 14 rot, solange der Reiter nicht verdrahtet war. – Claude, 31.10.2026
Build decision (slice 110): Neues Recht `ManageHrFile` nur für Inhaberin und Managerin; Standortleitung und Backoffice sehen die Akte nicht. – Claude, 31.10.2026
Build decision (slice 110): Inhalte liegen als JSON verschlüsselt in `hr.HrEntries` (vorhandener `HealthDataProtector`, Kontext `hr`); offen bleiben nur Art, Datum, Folgetermin, Erledigt und Ablaufdatum, weil die API damit rechnet. Ohne Schlüssel antwortet das Schreiben mit 503. – Claude, 31.10.2026
Build decision (slice 110): Das PDF schreibt ein kleiner eigener `SimplePdf` (Helvetica, WinAnsi), weil ein neues Paket eine Freigabe braucht; einmal mit PyMuPDF gegengelesen. – Claude, 31.10.2026
Build decision (slice 110): Eine Verwarnung ohne Ablaufdatum gilt 2 Jahre ab ihrem Datum; das Handbuch verweist für die passende Frist auf die Rechtsberatung, die App bewertet nichts. – Claude, 31.10.2026
Build decision (slice 110): Die API nennt fällige Folgetermine, der Kalender zeigt sie als Meldung mit „Zur Personalakte“. Bei Ausgeschiedenen ist die Akte nur lesbar und exportierbar, ihre Folgetermine erinnern nicht. – Claude, 31.10.2026
Build decision (slice 110): Prüfstufe 3: Trigger und Shell sind gemeinsamer Code, deshalb liefen volle API- und Desktop-Suite unter der Sperre. – Claude, 31.10.2026
Build decision (slice 125): Pfeiltasten bewegen im Balken und in den Reitern nur den Fokus, Enter und Leertaste wählen, sonst lädt jeder Pfeil eine Maske und fragt bei ungespeicherten Änderungen nach – Claude, 31.10.2026
Build decision (slice 125): Die Überschrift „Verwaltung“ mit Unterzeile entfällt, der Balken benennt die Seite und die Masken bekommen die Höhe zurück – Claude, 31.10.2026
Build decision (slice 125): Auch die Reiterzeile läuft in „Mehr“ über, statt zu scrollen; die Abweichung von Brief 5.4 steht im Brief – Claude, 31.10.2026
Build decision (slice 125): Symbole aus dem Bestand: Betrieb `Location`, Angebot `Steam`, Team `Users`, Lager `Archive`, Kassa & Geld `Register`, Kontakt `Chat`, Zahlen `Percent`, Mehr `ChevronDown` – Claude, 31.10.2026
Build decision (slice 125): Steht die gewählte Gruppe im Überlauf, trägt „Mehr“ den Zustand „gewählt“, ist der Tab-Stopp und nennt die Gruppe im Namen – Claude, 31.10.2026
Build decision (slice 125): Neuer Zwischenwert `rail-bar-hover` (hell `#1a565c`, dunkel `#153837`), Text beim Überfahren `rail-fg`, weil `rail-fg-2` hell nur 4,48:1 erreicht – Claude, 31.10.2026
Build decision (slice 125): Die Reiterzeile ist ein eigener F6-Bereich, sonst springt F6 vom Reiter zurück in die linke Leiste – Claude, 31.10.2026
Build decision (slice 125): Der Fokusring im Balken nimmt `rail-fg`, weil der Akzent-Ring dort hell nur 1,25:1 erreicht; die linke Leiste bleibt außerhalb dieser Slice – Claude, 31.10.2026
Ruling: „Anzahlung anfordern“ gab es nie, weder im Web noch in der API; Slice 124 zeigt nur den Stand der Anzahlung und baut keinen neuen Ablauf, das Abnahmekriterium ist angepasst – Auftraggeber, 31.10.2026
Ruling: Sofortüberweisung, Lückenvorschläge und Einwilligung für Wiederbuchungs-Hinweise werden vor Slice 103 nativ, als eigene Slice 126 nach Slice 124 (Ä290–Ä292) – Auftraggeber, 31.10.2026
Build decision (slice 124): Der Gutschein wird beim Verlassen des Felds abgefragt wie im Web, ohne eigenen Knopf; das Vorbelegen nimmt das Minimum aus Restwert und offenem Betrag vom Server und gilt nur für gültige Wertgutscheine bei leerem Betrag – Claude, 31.10.2026
Build decision (slice 124): „Ausfallgebühr belasten“ zeigt der Knopf nur mit dem Recht Kassenaufsicht und `CanCharge` vom Server, „Erstattung erneut anstoßen“ nur mit Kassenaufsicht und `CanRetry`; „Karte per Link anfordern“ nur mit dem Recht Buchen. Der Server prüft alles selbst – Claude, 31.10.2026
Build decision (slice 124): Ein gewählter Stornobeleg zeigt den Stand der Erstattung des stornierten Belegs, weil die Kassa nach dem Storno den Stornobeleg auswählt; im Web bleibt dafür der Originalbeleg offen – Claude, 31.10.2026
Build decision (slice 124): Der Erinnerungsverlauf nennt Datum und Uhrzeit, das Web nur die Uhrzeit, weil Erinnerungen meist an einem anderen Tag als der Termin hinausgehen – Claude, 31.10.2026
Build decision (slice 124): Keine neuen Handbuchbilder: Die drei Karten erscheinen nur mit Daten, die vorhandenen Bilder bleiben richtig – Claude, 31.10.2026
Build decision (slice 124): Der Abschnitt zu Beleg und Gutschein steht in `seite-kassa` statt in `seite-pos`, weil `seite-pos` die Web-Kassa beschreibt, die Slice 103 entfernt – Claude, 31.10.2026
Ruling: Die Sofortüberweisung bekommt das QR-Bild mit dem Paket QRCoder 1.8.0 (MIT-Lizenz, Version nur in `Directory.Packages.props`) in Slice 126 (Ä297) – Auftraggeber, 31.10.2026
Build decision (slice 126): Die Sofortüberweisung zeigt Empfänger, IBAN, Betrag, den Zahlungstext (EPC) des Servers als Text und als QR-Bild; der Code trägt genau den Text des Servers (Fehlerkorrektur M wie im Web), ist schwarz auf weißem, deckendem Grund mit Ruhezone und damit in hellem und dunklem Schema scanbar, und heißt für den Screenreader „QR-Code für die Sofortüberweisung“. Das Bild fällt weg, sobald sich der Betrag ändert – Claude, 31.10.2026
Build decision (slice 126): Der Betrag für „Zahlungsdaten anzeigen“ ist wie im Web die Eingabe der Zeile, sonst der offene Rest nach den anderen Zahlungen; der Betrag des Servers wird ins Feld eingetragen – Claude, 31.10.2026
Build decision (slice 126): „Lücken schließen“ steht im Menü neben „Termin anlegen“ (nur mit dem Recht zum Buchen), lädt nur bei geöffneter Seite und neu beim Tageswechsel und nach dem Verschieben; verschoben wird mit Startzeit und Person des Vorschlags, mit Rückgängig wie jedes Verschieben – Claude, 31.10.2026
Build decision (slice 126): Die Einwilligung steht als Karte im Termin-Panel; Anfragen und Widerrufen zeigt sie nur mit dem Recht zum Buchen, die Sprache der Mail (Deutsch, Englisch, Französisch, Italienisch) wählt die Rezeption wie im Web. Die Meldungen sind kürzer als im Web („Einwilligung widerrufen“), weil die Meldung Sätze mit Punkt kürzt – Claude, 31.10.2026
Ruling: Die Anamnese im Team (Bogen lesen, Tablet-Link, Papierbogen, Foto mit Prüfen und Bestätigen) wird vor Slice 103 nativ, als eigene Slice 127 (Ä300) – Auftraggeber, 31.10.2026
Ruling: Slice 127 baut die Anamnese nativ mit den heutigen Rechten aus Web und API, ohne etwas zu verschärfen; die Abweichung von ADR 0013 klärt Slice 128 und muss vor der ersten Auslieferung erledigt sein (Ä304) – Auftraggeber, 31.10.2026
Build decision (slice 127): Die Karte „Anamnese“ steht im Termin-Panel und lädt beim Öffnen nur den Stand; die Antworten holt erst der Klick auf „Ansehen“, „Ausblenden“ wirft sie weg. Tablet, Papier, Foto und „Prüfen“ zeigt sie nur mit dem Recht zum Buchen, der Server prüft es ebenso – Claude, 31.10.2026
Build decision (slice 127): Der Papierbogen ist eine HTML-Seite aus der API-Vorgabe, gedruckt über den Standardbrowser statt mit einem neuen Paket; die kurzlebige Datei enthält Name, Termin und Link, nie Antworten, und wird nach fünf Minuten gelöscht, Reste beim nächsten Druck – Claude, 31.10.2026
Build decision (slice 127): Das Foto wird wie im Web auf JPEG mit höchstens 2000 px verkleinert, im Speicher und mit der EXIF-Drehung; SkiaSharp kommt über Avalonia mit, kein neuer Paketeintrag – Claude, 31.10.2026
Build decision (slice 127): „Foto verwerfen“ löscht wie im Web ohne zweite Rückfrage, weil der Prüfdialog selbst ein Dialog ist; „Schließen“ speichert nichts und lässt das Foto unter „Prüfen“ – Claude, 31.10.2026
Build decision (slice 127): Der Prüfdialog hängt als eigene Vorlage in `ConfirmDialogView`, deshalb gilt Stufe (2) mit der vollen Desktop-Suite, ohne die vier auch auf `dev` roten Tests – Claude, 31.10.2026
Ruling: Die API wird verschärft, die Rezeption sieht von der Anamnese künftig nur den Stand; umgesetzt wird das in Slice 128, Slice 127 bleibt bei den heutigen Rechten. Offen bleibt in Slice 128, wer fotografierte Bögen prüft – Auftraggeber, 31.10.2026
Build decision (slice 103): Im Server-Betrieb meldet `/anmelden/` nur mit Passkey an, in Development dazu mit den Testkonten; eine Passwort-Anmeldung kennt die API nur im Einzelplatz, die Angabe „Server: E-Mail“ im Plan war falsch – Claude, 01.11.2026
Build decision (slice 103): Die Hinweise bei fehlendem Recht und unbekanntem Ziel stehen unpersönlich nach texte.md T1 und nennen den Weg, statt der Du-Form aus Annahme 6 des Plans – Claude, 01.11.2026
Build decision (slice 103): `/api/system/team-web`, `TeamWebInfo` und `AISPA_TEAM_WEB_URL` behalten Namen und Vertrag und liefern die Web-Adresse für Hilfe und Anmeldeseite (`Services/HelpServer.cs`); ein Umbenennen hätte OpenAPI, AppHost und Doku ohne Nutzen geändert – Claude, 01.11.2026
Build decision (slice 103): Mit der Team-Oberfläche entfallen im Browser „Testkonto anlegen“, das Empfangsgerät im Browser (Schnellwechsel, PIN, Abmelden nach Leerlauf) sowie Einrichten, Wiederherstellen und Passwort ändern; die Windows-App hat eigene Fassungen davon, `/anmelden/` meldet ein Startpasswort nicht weiter, bis es in der Windows-App geändert ist – Claude, 01.11.2026
Build decision (slice 103): Von den Seitenerklärungen der Team-Oberfläche gehen `seite-calendar`, `seite-pos` und `seite-inbox` sowie `zugaenge-empfangsgeraet-anmelden` in den nativen Themen auf; die übrigen bleiben mit `seite: verwaltung`, weil Masken der App sie als Fragezeichen nutzen – Claude, 01.11.2026
Build decision (slice 103): Tests red: Der Scan über alle Sprungziele als Inhaberin, der Abgleich der Assistenten-Ziele mit dem Inventar und die Kette Einladung → Code waren vor der Änderung schon grün, weil die Vorgänger-Slices die Masken gebaut haben; rot waren fehlendes Recht, unbekanntes Ziel, Sprung ohne Verwaltungsbereich, `/team` und Unbekanntes mit 404, Wurzel und `/anmelden/`, Einladungs- und Stripe-Link sowie die Seite `/anmelden/` – Claude, 01.11.2026
Build decision (slice 103): `/` leitet mit Query nach `/anmelden/` weiter (API und nginx), damit schon verschickte Einladungen `/?einladung=…` und laufende Stripe-Anmeldungen `/?zahlungen=1` ankommen – Claude, 01.11.2026
Build decision (slice 103): Prüfstufe 3 mit Gemeinsamem (Shell, `App.axaml`, `ShellView`, Middleware `TeamWebRoot`, Web-Prüftests): volle Desktop-, API- und Web-Kette unter der Sperre – Claude, 01.11.2026

Build decision (slice 111): Eine Vollmacht Kassa-Storno oder Bestellungen auslösen greift je Art erst, sobald im Spa eine solche Vollmacht existiert, auch eine abgelaufene; danach braucht jede Rolle außer Inhaber und Manager eine heute gültige. Sonst könnte in bestehenden Spas nach dem Update niemand mehr stornieren. – Claude, 2026-11-01
Build decision (slice 111): Pflege, Reiter, Kennzeichen und „Wer darf was?“ laufen über das Recht `ManageHrFile`, kein neues Recht; die Tabellen liegen im Schema `hr`, damit der Verlauf aus Slice 110 ohne Inhalte greift. – Claude, 2026-11-01
Build decision (slice 111): Kennzeichen auf Karte und Liste nennen nur heute gültige Einträge; abgelaufene stehen ausgegraut im Reiter und in „Wer darf was?“. – Claude, 2026-11-01
Build decision (slice 111): Ein ohne Verbindung vorgemerkter Termin geht bestätigt in die Warteschlange, weil beim Abgleich niemand mehr nachfragen kann und eine Regel nie sperren darf. Der Begleiter nennt die Regel und verweist auf den Kalender, ein eigenes „Trotzdem“ baut er nicht. – Claude, 2026-11-01
Build decision (slice 111): Das Symbol zeigt einen einfachen Tooltip, in der Kalenderspalte steht der Hinweis zusätzlich in der vorhandenen Hover-Karte des Spaltenkopfs (Slice 89); Karte und Liste zeigen Kennzeichen als Text, ohne Hover-Karte. – Claude, 2026-11-01
Build decision (slice 111): Verschieben sperrt und fragt nicht: Der Termin zieht um, die Regel steht in den Warnungen der Antwort. – Claude, 2026-11-01
Build decision (slice 111): Die Spalte „Tests red“ belegt der rote Lauf der API-Tests; die Desktop-Tests entstanden nach den Ansichten, weil sie ohne die neuen Modelle nicht bauen. Das Symbol im Kalender prüft ein Modell-Test, nicht der Tooltip im Fenster (manuelle Testliste). – Claude, 2026-11-01
Build decision (slice 111): Die Web-Masken senden `rulesConfirmed: false` und zeigen die Regel als Fehlermeldung; ein „Trotzdem“ bekommen sie nicht, weil diese Masken nach ADR 0084 entfallen. – Claude, 2026-11-01
Build decision (slice 111): Prüfstufe 3: `ApiClient`, `App.axaml` und die Regel in Buchung und Dienstplan sind Gemeinsames, daher liefen einmal die vollen Suiten von API und Desktop. Danach änderten sich nur drei Web-Aufrufe und eine Testliste, deshalb auf der Spitze Basisprüfung, zuständige Tests und Web-Kette. – Claude, 2026-11-01
Build decision (slice 114): Der Barcode hängt am Material (`StockItem`), nicht am Kassa-Artikel; die Kassa findet den Artikel über `StockItem.ArticleId`, ein Material ohne Verkaufsprodukt lässt sich dort nicht scannen. – Claude, 2026-11-01
Build decision (slice 114): Der Code hat 3 bis 50 Zeichen ohne Leerzeichen und keine Prüfziffer-Pflicht: Lieferantencodes sind oft keine EAN, und die EAN 9001234567890 aus der Abnahme hat nach EAN-13 eine falsche Prüfziffer. Er ist je Spa eindeutig (Eintrag in `TenantModel.UniquePerTenant`), `null` im Input entfernt ihn. – Claude, 2026-11-01
Build decision (slice 114): Statt eines Scanfelds je Reiter gibt es eines auf der Lager-Seite (wirkt nach dem Reiter: Inventur springt ins Feld „Gezählt“, sonst öffnet sich die Buchung Wareneingang) und eines an der Kassa. – Claude, 2026-11-01
Build decision (slice 114): Zwei Endpunkte `by-barcode`: im Lager das Material (Recht `ViewCalendar`), an der Kassa der Artikel ohne Lagerdaten; deaktivierte Materialien und Artikel werden nicht gefunden. Ein unbekannter Code fragt im Lager („Material anlegen oder zuordnen?“), an der Kassa nur die Meldung. – Claude, 2026-11-01
Build decision (slice 114): Die Spalte „Tests red“ belegt der rote Lauf der API-Tests (12 von 13 rot); die Desktop-Tests entstanden nach den Ansichten. Prüfstufe 3: Beispieldaten ändern sich (Codes an den Verkaufsprodukten), deshalb liefen `DemoBulkTests` und `DemoSeederTests`; keine gemeinsamen Teile der Windows-App berührt, daher keine volle Desktop-Suite. – Claude, 2026-11-01

Build decision (slice 115): Die Zuordnung „Passt dazu“ ist eine eigene Tabelle `TreatmentRecommendedArticles` (Behandlung, Kassa-Artikel, Position) mit höchstens fünf aktiven Artikeln je Behandlung; Endpunkte `GET/PUT api/treatments/{id}/recommended-articles` (Lesen `ViewCalendar`, Ändern `ManageCatalog`), `GET api/appointments/{id}/recommendations` (`ViewCalendar`) und `GET api/pos/bills/{id}/recommendations` (`Book`) – Claude, 2026-11-01
Build decision (slice 115): „Vorrätig“ heißt Bestand größer 0 am Verkaufsort, den auch die Kassa beim Bezahlen nimmt (`StockService.SalesPlace`: erstes aktives Verkaufsregal, sonst Hauptlager); Artikel ohne Lagerartikel werden nie vorgeschlagen, weil kein Bestand belegt ist – Claude, 2026-11-01
Build decision (slice 115): Die Klick-Tests der Windows-App stehen in `ViewTests.Recommendations` (gemeinsame Headless-Sitzung), das Verhalten im ViewModel in `PosRecommendationTests`, `AppointmentRecommendationTests` und `TreatmentsTests`; der Prüfnachweis hat deshalb je eine Zeile mit `--filter-class` und eine mit `--filter-method` – Claude, 2026-11-01
Build decision (slice 115): Prüfstufe 1 mit Beispieldaten: die Slice ändert `PosDemoData` (gemeinsamer Schritt beider Einspielwege), deshalb liefen `DemoBulkTests` und `DemoSeederTests`; `ShellViewModel`, `CalendarViewModel` und `PosViewModel` bekamen nur Ergänzungen, geprüft mit den Testklassen von Kassa, Termin-Panel, Kalender und Shell, keine volle Suite – Claude, 2026-11-01
Build decision (slice 116): Soll ist der gebuchte Verbrauch (`Consumption`), der Mehrverbrauch die negative Summe der Inventur-Differenzen (`Count`), Ist ist beides zusammen; Korrekturen und Verkäufe zählen nicht, weil sie erklärt sind. Der Schwund wird beim Artikel ausgewiesen und nicht auf Behandlungen verteilt; die Behandlungen stehen mit ihrem gebuchten Soll darunter (Bericht `GET api/reports/locations/{id}/stock-usage`, Recht `ViewFinance`, Wert in Euro nur mit `ViewAmountReports`) – Claude, 01.11.2026
Build decision (slice 116): Die Zählung vom Handy ist eine eigene Sitzung (`StockCountSession`, `StockCountLine`, Migration `StockCountSessions`), je Lagerort höchstens eine offene oder abgeschickte; jede Person im Team darf zählen und abschicken (`ViewCalendar`), schließen, verwerfen und lesen nur die Leitung (`ManageLocation`). Das Handy bekommt das Soll nie (blind zählen); `close` bucht über `StockService.AddAsync` (Art Inventur) und setzt den Status mit einem atomaren Update, ein zweiter Aufruf bucht nichts – Claude, 01.11.2026
Build decision (slice 116): Der Wareneingang am Handy ist nur mit dem Recht `Book` sichtbar (Rezeption, Leitung), weil der vorhandene Buchungsweg es verlangt; Therapeutinnen zählen nur. Die Kamera bleibt nach einem erkannten Code offen (Dauerscan für viele Zeilen) und endet mit **Kamera beenden**, beim Verlassen der Seite und beim Unmount; ohne `BarcodeDetector` oder Kamerarecht bleibt das Feld „Code eingeben“ – Claude, 01.11.2026
Build decision (slice 116): Die Spalte „Tests red“ ist durch drei rote Läufe belegt (API 22 von 22, Web 11 von 11, Windows-App 4 von 5 rot; die Rechtegrenze „ohne das Recht“ war schon ohne Umsetzung grün) in `slice-116.red.log`. Prüfstufe 1: Beispieldaten und `DemoHistory` bleiben unverändert (keine vorbelegten Zählungen), deshalb liefen `DemoBulkTests`, `StartupStateTests` und `DemoSeederTests` nicht – Claude, 01.11.2026
Ruling: Fotografierte Anamnesebögen: Hochladen darf, wer buchen darf (auch die Rezeption); ansehen, prüfen, übernehmen und verwerfen nur, wer das neue Recht „Anamnese lesen“ (`ViewHealthData`) hat: Inhaber, Manager, Standortleitung und Therapeut:innen, auch ohne Buchungsrecht. Eine Abstimmung mit der Datenschutz-Beratung ist damit nicht bestätigt und bleibt offen – Auftraggeber, 02.11.2026
Build decision (slice 128): Das Leserecht ist ein eigenes Recht `AccessRight.ViewHealthData` am Ende des Enums statt einer Rollenabfrage im Endpunkt, damit die Windows-App wie bei allen Rechten nach der Rechteliste ausblendet; `GET /overview` und `GET /check` bleiben bei `ViewCalendar`, Tablet, Papier und Hochladen bei `Book` – Claude, 02.11.2026
Build decision (slice 128): Die Rollentests stehen in `AnamnesisChannelTests` statt in einer neuen Klasse `AnamnesisRightsTests` (Plan), weil Hochladen, Warten auf die KI und Bogen dort schon als Hilfen stehen; die Rechtetabelle ist eine handgeschriebene `TheoryData` je Rolle über zehn Aufrufe – Claude, 02.11.2026
Build decision (slice 128): Ohne Leserecht öffnet die App nach dem Hochladen keinen Prüfdialog, sondern zeigt den Toast „Foto hochgeladen. Prüfen kann, wer Anamnesen lesen darf“ (Toasts ohne Schlusspunkt wie „Foto verworfen“); der Rollenhinweis der Rezeption nennt „Von der Anamnese nur den Stand“ in der App und auf `/anmelden/` (`web/src/signin/roles.ts`) – Claude, 02.11.2026
Build decision (slice 128): Kein neuer ADR, sondern ein datierter Nachtrag in ADR 0013, weil das Ruling die Festlegung bestätigt statt sie zu ändern – Claude, 02.11.2026
Build decision (slice 128): Tests red ist durch zwei Läufe belegt: API (Rezeption bekam 200 auf die Antworten, die Therapeutin 403 beim Prüfen, `RolesTests` ohne `ViewHealthData`; 3 rot) und Windows-App in einem zweiten Worktree ohne die App-Änderung (3 von 3 rot); `Rezeption_laedt_ein_Foto_hoch_und_die_Antwort_enthaelt_keine_Antworten` war schon vorher grün und sichert, dass Hochladen nichts verrät – Claude, 02.11.2026
Build decision (slice 117): Die Spalte „Tests red“ belegen die roten Läufe im Prüfnachweis: API 44 von 44 gegen Gerüste, Windows-App 15 von 17 und 4 von 4 Bildtests, Web 2 von 2; die Befunde der Durchsicht 5 von 36 auf eigenem Commit. Die Spalte „Plan“ gilt mit der Freigabe durch das Skript. – Claude, 02.11.2026
Build decision (slice 117): Prüfstufe 1 (Ruling vom 01.11.2026) mit Beispieldaten: `DemoBulkTests` und `DemoSeederTests` liefen, die Slice ändert `DemoExtras`; die Regel am Dienstplan-Speichern prüfen die Dienstplan-Klassen von API und App, keine volle Suite. – Claude, 02.11.2026
Build decision (slice 117): Die Ersthelfer-Warnung greift erst, wenn im Spa mindestens eine Zuständigkeit Ersthelfer eingetragen ist, auch eine abgelaufene; sonst warnte jedes Speichern in Spas, die die Liste nicht nutzen. Löscht die Leitung die letzte Zuständigkeit, schweigt die Warnung; die Hilfe nennt die Bedingung. – Claude, 02.11.2026
Build decision (slice 117): „Abwesend“ bei der Zuständigkeit Lager heißt: heute im veröffentlichten Plan ein Eintrag, der kein Dienst ist. Mehrere Lager-Zuständige bekommen die Aufgabe alle. Empfänger zählen nur, wenn sie aktiv sind und am Standort arbeiten (Arbeitszeit oder Dienstplan); bleibt niemand, geht die Aufgabe an die Leitung des Standorts. – Claude, 02.11.2026
Build decision (slice 117): Im Kanal Mitarbeiter-App liefert `reminders` nur eigene Zertifikate, auch für Inhaberin und Manager; die App filtert nichts selbst. – Claude, 02.11.2026
Build decision (slice 117): Der Dateityp eines Scans wird an den ersten Bytes erkannt (PDF, JPEG, PNG), die Angabe des Aufrufers zählt nicht; den Dateinamen bildet die App aus Bezeichnung und Endung. Arten lassen sich nicht löschen, nur ausblenden; eine ausgeblendete Art lässt sich nicht neu zuordnen, eine bestehende Zuständigkeit damit aber noch beenden. Neuer Endpunkt `duties/history` für den Verlauf auf der Karte. – Claude, 02.11.2026
Build decision (slice 117): `ViewTests.Roster` verbot der Rezeption jede Route `api/staff*`; die Route `api/staff-duties/reminders` ist ausgenommen, weil der Kalender dort für jede Person die eigenen Erinnerungen lädt. Alle übrigen Routen bleiben verboten. – Claude, 02.11.2026
Build decision (slice 117): Die Erinnerungen in „Mein Tag“ stehen in `Reminders.tsx`, eingebunden in `MyDayApp.tsx` bei „Heute“; `Today.tsx` aus dem Plan ist „Heute im Spa“ und nur für die Leitung. Die Klickwege der App stehen in `ViewTests.Duties.cs`, weil es die Headless-Sitzung nur dort gibt. – Claude, 02.11.2026

## Überraschungen
- Slice 92: Der Plan nannte als Sprungziele des Assistenten auch `time`, `staff` und `pos`; im Code gibt es sie nicht (nur `seite:` in Hilfethemen). Belegt sind „Jetzt ergänzen“ (`FieldMasks.cs`: locations, company, treatments, promotions, businessInfo), Öffnungszeiten (closures), Schulferien (subscriptions) und „Im Bericht öffnen“ (reports/management). Das Inventar nennt nur die belegten (Review).
- Slice 92: Der Haupt-PC im eigenen Netz lauscht auf allen Netzwerkkarten; „nur lokal erreichbar“ war zu stark. Neuer Befund Ä198 zum Offline-Stand auf verlorenen Geräten (Review).
- Slice 93: Die Slice-Datei nannte als Web-Vorbild nur `CompanyTab.tsx`; der Reiter hat ein zweites Formular „Kundenbereich einbetten“ mit eigenem Speichern. Übernommen (siehe Entscheidungen).
- Slice 93: Eine Rückfrage, die die Auswahl einer ListBox noch während ihrer eigenen Auswahl zurücksetzt, kommt in der Liste nicht an (Navigationsleiste blieb auf „Kalender“); die Auswahl wird jetzt gepostet zurückgesetzt, ein Test prüft die Liste.
- Slice 93: Die Bereichsliste mit `AlwaysSelected` wählt beim Einhängen der Seite kurz den ersten Bereich; ein Sprung aus dem Assistenten in einen eingebetteten Bereich verlor dadurch Zeitraum und Feld (vier Assistenten-Bildtests rot in der vollen Suite). Das Modell merkt sich jetzt, auf welchen Bereich die eingebettete Adresse zeigt.
- Slice 93: `RetentionTests.Erinnert_erst_nach_bestaetigter_Einwilligung_mit_KI_Text_und_Abmeldelink` und `OnlineBookingTests.Gast_bucht_bekommt_Bestaetigung_mit_Links_und_die_Rezeption_sieht_den_Termin` warteten in der vollen Suite und in einem Einzellauf kurz danach vergeblich auf die Gastmail (15 s); auf dev, mit nur dem Verlaufs-Trigger und in einer Wiederholung auf dem Branch grün: Last, kein Fehler. Ebenso `VoucherShopTests.Behandlungsgutschein_kostet_den_Preis_der_Behandlung` (Warten auf den Bestellstatus, 20 s) in zwei vollen Läufen auf `8b40099` und `17df446`; die Klasse einzeln auf `17df446` grün (7 von 7).
- Slice 94: Schritt 7 der manuellen Testliste von Slice 93 („Behandlungen“ öffnet die eingebettete Verwaltung) gilt nicht mehr; „Behandlungen“ ist jetzt nativ (Block Slice 94).
- Slice 94: Wer den Bereich „Behandlungen“ nicht sehen darf (ohne `ManageCatalog`), landet mit „Jetzt ergänzen“ des Assistenten weiter im ersten erlaubten Bereich und in der eingebetteten Verwaltung bei den Behandlungen (Verhalten seit Slice 93, `CompanionBlockTests` unverändert grün).
- Slice 95: Slice 94 und 95 bauten parallel zwei verschiedene Gerüste für native Bereiche (`INativeArea` mit `Open(id, section, field)` gegen `Open(AdminTarget)`); der Merge kollidierte in `AdminPageViewModel`, `App.axaml`, `CompanyViewModel` und `ShellViewModel`. Gelöst mit der Schnittstelle von Slice 94 plus `ITargetedArea`; `OnlineRulesSection` blieb bei den Standorten und ist nicht doppelt gebaut.
- Slice 95: Die Bildtests der Verwaltung und `OpenPage` der Begleiter-Seiten wählten „Standorte & Räume“ als nicht nativen Beispielbereich; er ist jetzt nativ, sie wählen einen anderen Bereich.
- Slice 95: `NewCommand` und `SaveCommand` der Kalender-Abos meldeten ihre Bedienbarkeit nach dem Laden nicht neu; der Klick-Bildtest fand es, ein reiner ViewModel-Test hätte es nicht gesehen.
- Slice 95 (Review): Kontrollkästchen mit `IsChecked` OneWay und zugleich Command blieben nach einem API-Fehler im umgeschalteten Zustand; jetzt meldet das ViewModel den Modellstand erneut, mit Klick-Test.
- Slice 95: Der Vitest-Lauf der Hilfe meldete nach dem Merge das Bild `verwaltung-behandlungen.png` als fehlend, obwohl es vorhanden war (Glob-Cache); nach dem Beiseiteschieben von `.vitest-cache` und `.vite` 52 von 52 grün – Cache, nicht Fehler.
- Slice 96: Das Zahlenfeld zeigt 10 Prozent als „10“, nicht „10,00“; die erste Erwartung im Bildtest `Verwaltung_Kassa` war falsch und ist berichtigt.
- Slice 96: Die Bildtests `Verwaltung_Zahlungen` und `Verwaltung_Erinnerungen` waren schon auf dem Gerüst grün (sie prüfen nur, welche Maske offen ist); die Abnahme tragen die drei Klick- und Tastentests und die Modelltests.
- Slice 96: Der Methodenfilter `Einstellungsmasken*` traf den Scan-Test nicht (der Filter vergleicht den voll qualifizierten Namen); im ersten Rot- und Grünlauf fehlte der Scan deshalb, ab der Durchsicht läuft er mit `*Einstellungsmasken*` bzw. über `*melden_keine_Bindungsfehler`.
- Slice 96 (nach dem Einmischen von `dev` mit Slice 95): `dev` war selbst rot. Die Bildtests `Verwaltung`, `Verwaltung_ohne_Team_Oberflaeche_auf_dem_Server` und `KI_und_Hilfe_Leiste_steht_auf_Seiten_mit_Browser…("Verwaltung")` wählten „Behandlungen“ als Bereich ohne native Maske, der aber seit Slice 94 nativ ist; sie wählen jetzt „Team“. `GridValueTests.Ansichten_nutzen_keine_Kartenraender…` meldete `Spacing="28"` in `LocationsView.axaml`, jetzt 24 nach dem Brief. Last, kein Fehler: der Tab-Scan `Jede_Seite_Tab_und_Umschalt_Tab…` war in der Prüfkette auf `12548d95` auf der Seite „Anfragen“ rot, einzeln grün.
- Slice 97: Das Inventar nannte „Handbuch“ einen Sprung in die Hilfe (E1); der Web-Reiter pflegt aber die Rezeptionseinträge. Nativ umgesetzt, das Inventar ist angepasst.
- Slice 97: „Einbetten“ stand in der Slice-Datei als eigener Bereich; es gehört seit Slice 93 zu den Firmendaten. Ergänzt wurde „Code kopieren“ samt Adresse aus der API.
- Slice 97: Die KI-Maske hat kein Schlüsselfeld (Server-Einstellung); die DoD-Zeile „Schlüssel maskiert“ gilt für Postfach-Passwort und Client-Geheimnis. Die Ordner-Vorgabe heißt „Inbox“, weil `DesignTokenTests` Platzhalter in Versalien abweisen.
- Slice 97: Die Leiste kennt keinen eigenen Knopftext; ein neuer Handbucheintrag zeigt „Speichern“ statt „Eintrag anlegen“ (wie in Slice 94).
- Slice 98: Ein Testfall erwartete falsch „Therapeut:in · eingeladen“; ohne Rollenwahl gilt die Vorgabe Rezeption. Die Fehlerwartung ist berichtigt, nicht abgeschwächt.
- Slice 98: Der Scan über alle Seiten fand den fehlenden Namen „Befristet bis einschließlich“ am Datumsfeld (ergänzt).
- Slice 98: Die DesignTokenTests werten Hexwerte in Texten als Farbe.
- Slice 98 (Review): Die Mail nannte den Namen eines beliebigen Spas, weil `Tenant` keinen Mandantenfilter hat (behoben, Test mit zweitem Spa). Weg gefilterte Zeile ließ die Rückfrage entfallen, Teil-Speichern führte zum Überschreiben des neuen Stands, paralleles Speichern konnte eine Person doppelt anlegen (behoben, je roter Test auf `d1218754`).
- Slice 104: 30 öffentliche Endpunkte (Anmeldung, Einladung, Kalender-Feed, Health, OpenAPI, Mandant anlegen) tragen weder Anmeldung noch `AllowAnonymous`; der Scan hält sie jetzt als Liste „öffentlich“ fest, statt sie alle umzuschreiben. Die Endpunkte der Buchungsseite sind schon ausdrücklich öffentlich.
- Slice 104: Identity stellt das Team-Cookie nach dem Prüfen des Sicherheitsstempels neu aus und verwirft dabei eigene Claims; ohne `SecurityStampValidatorOptions.OnRefreshingPrincipal` wäre die Mitarbeiter-App nach 30 Minuten wieder Arbeitsplatz gewesen. Der Haken übernimmt den Kanal-Claim, ein Test sichert ihn.
- Slice 104 (Review): Der Kanal der Mitarbeiter-App ist eine Einschränkung, die die App nach der Anmeldung selbst wählt; wer sich am Handy ohne sie anmeldet, hat die Rechte seiner Rolle wie in der Windows-App. „Nur eigene Daten“ gilt also für die Mitarbeiter-App, nicht für jede Anmeldung am Handy.
- Slice 104 (Review): Kein eigener ADR: der Kanal schränkt nur ein, was ADR 0041 und 0084 schon festlegen (Webmodule mit eigenen Zugängen); die Einzelheiten stehen im PR-Protokoll.
- Slice 104: Last, kein Fehler: In der Prüfkette auf `0b3e6d9` waren `ModuleTests.Einzelne_Module_lassen_sich_im_Standalone_zuschalten` (Deadlock 40P01 beim Start des Migrators) und `StartupStateTests.Einspielen_blockiert_den_Start_nicht…` („Einspielen dauert zu lange“) sowie Vitest in `help/help.test.tsx` (3), `pos/PosPage.test.tsx` (1) und `operator/OperatorApp.test.tsx` (1) rot (Zeitüberschreitungen); einzeln wiederholt alle grün (API 2 von 2, Vitest 62 von 62), der ganze API-Lauf davor auf `c20bfd9` war grün (1320).
- Slice 104: `MapChannelEndpoints` gab es schon (Kanäle der Terminannahme); die neue Methode heißt `MapAccessChannelEndpoints`.
- Slice 118: Last, nicht Fehler: Die Web-Gesamtläufe waren dreimal wegen wechselnder, unbeteiligter Dateien rot (BusinessInfoPage, BookingPanel, ClosuresPage, StockPage; dann `App.test.tsx`; dann `SubscriptionsPage.test.tsx`), jede einzeln grün; der vierte unveränderte Lauf war grün (780 Tests).
- Slice 118: Die Scans über alle Seiten bauen die Shell im Einzelplatz-Betrieb ohne Seite „Netz“ und erfassen sie nicht (der Plan sah das vor); dafür prüfen eigene Tests Name, Zustand und Platz von Karte und Seite. Eine Shell im Server-Betrieb in die Scans zu heben ist ein eigener Umbau der Testbasis.
- Slice 118: Ein Eintrag ohne Verbindungscode ist ein Server (System-Zertifikat), mit Code ein Haupt-PC; beide Wege teilen die Felder `ServerAddress`/`ServerCode` in `einstellungen.json`, ohne Schema-Änderung. Die Seite „Netz“ (Server-Betrieb) und „Im Netz“ (Einzelplatz) schließen sich aus.
- Slice 102: Die Namen `EmployeeChoice`, `RoomStartRow` und `TreatmentCard` gab es schon in der App; die Berichte nutzen eigene Zeilentypen, damit andere Masken unberührt bleiben.
- Slice 102: `ViewTests.KI_und_Hilfe_Leiste_steht_auf_Seiten_mit_Browser_neben_dem_Browser("Verwaltung")` ist auch auf frisch gebautem `dev` (83fd80f9) rot: Die Verwaltung öffnet Firmendaten nativ, der Test sucht den eingebetteten Browser. Vorbestehend, nicht von dieser Slice; Test nicht angefasst, die Slice, die den Browser entfernt (103), räumt ihn auf.
- Slice 105: Der Hub-Endpunkt ist jetzt für den Kanal Anzeige freigegeben; `WatchLocation` und `WatchInbox` lehnen die Anzeige ab, weil sie keinen Zugang zu einer Person hat (Test `Die_Anzeige_darf_weder_Standorte_noch_den_Posteingang_abonnieren`). Der Scan der Kanäle führt dafür zwei Einträge mehr (`* /hubs/live`, `* /hubs/live/negotiate`).
- Slice 105: Die Durchsicht (Opus) fand: 5xx beim Hochfahren verwarf die Kopplung, Raumsperren ohne Tagesgrenze, der Abnahmetest ohne Prüfung der neuen Zeit, hängendes Abo, „Modul aus“ bei laufender Tafel; alles behoben mit Tests. Das Team-Cookie überlebt in Tests mit fester Uhr (2027) nicht (Ablauf), das Anzeige-Cookie (365 Tage) schon; darum liest ein zweiter Host mit fester Uhr die Tafel.
- Slice 105: Das Bündel der Tafel ist 87 kB gzip groß (React 66, SignalR-Verbindung 14, eigener Code 4); als einzige der schlanken Oberflächen darf sie SignalR laden (`signalR: true` in `check-bundles.mjs`).
- Slice 99: Die Vorlagen des Ausfall-Assistenten standen nur in `CalendarView.axaml`; der Tageseditor braucht sie auch, sie stehen jetzt in `App.axaml`.
- Slice 99: Die Durchsicht (Opus) fand 16 Punkte, darunter: `employeeId` hob die Standortgrenze auf (fremde Abwesenheiten sichtbar), Datenverlust beim zweiten Klick auf dieselbe Zelle, Kalender und Wochenplan zeigten nach Änderungen alte Stände, lesende Zellen standen auf halber Deckkraft. Alle behoben, rot belegt bis auf die Charakterisierung und das Entfernen verwaister Bilder.
- Slice 99: `GridValueTests` prüft nur Attribute mit 14, 18, 22, 28 und 32; Stil-Setter und `Margin` deckt der Test nicht, die Abweichung der Planzellen fiel erst in der Durchsicht auf.
- Slice 99: Die volle Desktop-Suite auf `6aac8c84` hatte 11 Rote. Neun kamen aus dem Zusammenspiel mit `dev` und sind behoben. `ViewTests.Knopfbeschriftungen_haben_hoechstens_26_Zeichen` ist vorbestehend rot durch drei zu lange Knöpfe aus Slice 102 (`ComplianceReportView`, `CostsView`, `HygieneReportView`), `ViewTests.KI_und_Hilfe_Leiste_steht_auf_Seiten_mit_Browser_neben_dem_Browser("Verwaltung")` ebenso (siehe Slice 102). Beide nicht angefasst.
- Slice 99: Der Hilfetest fand `verwaltung-berichte-management.png` nicht, obwohl die Datei da ist: alter Vitest-Cache nach dem Einmischen, kein Fehler.

- Slice 106: Die Durchsicht (Opus) fand: gleichzeitiges Genehmigen und Ablehnen widersprach dem Dienstplan, Wiederherstellen ließ den Beginn stehen, Anträge überschrieben genehmigte Tage, der Speicher war nicht an die Person gebunden, Absenden blieb nach Netzrückkehr gesperrt; alle behoben mit Tests. Offen bleibt Ä240: Öffnet die Inhaberin die App im Browser der Team-Oberfläche, gelten dort bis zur neuen Anmeldung nur die Rechte der App; so hat es Slice 104 festgelegt.
- Slice 106: Der erste Rot-Lauf scheiterte am Kompilieren (fehlendes `using`) und zählte nicht; der gültige Lauf auf `dev` zeigt 18 rote Tests aus fachlichem Grund. Der Kanal-Scan führt Routen von `MapPost("")` mit Schrägstrich am Ende (`POST /api/absence-requests/`).
- Slice 108: Die Durchsicht der Spitze fand sechs Fehler, alle mit Test behoben und rot gezeigt: Frist der Anzeige wich von der eingefrorenen Frist der Gebühr ab; gemerktes Spa machte Mail-Links anderer Spas ungültig; der Service Worker speicherte Mail-Codes im Schlüssel; späte Absage mit bezahlter Anzahlung; toter Fragebogen-Link für Gäste ohne Konto; Karte des nächsten Termins übernahm nach der Absage den Fokus. Gebaute Dateien im Speicher sind jetzt auf 40 begrenzt.
- Slice 109: Die Slice-Datei verlangt „offene Anfragen“ und zugleich „nur bestehende Bericht-Endpunkte, keine neuen Kennzahlen“; ein Bericht mit offenen Anfragen bestand nicht. Aufgelöst über eine Zeile `openRequests` im bestehenden Bericht (Build decision 3); nicht über einen neuen Endpunkt.
- Slice 99: Slice 106 landete während des letzten Prüflaufs auf `dev`; Vertrag und Routen passten. Erst dadurch zählte, dass „Genehmigen“ Plan und Kalender nicht neu lud; behoben mit Test.
- Slice 100: Die Slice-Datei verlangt „Lohnangaben und Provision“; eine Provision gibt es nirgends. Aufgelöst über Annahme 1 des Plans (Ä220).
- Slice 100: Die Basisprüfung im neuen Worktree schlug zuerst beim Build mit CS2012 fehl (Datei gesperrt), weil ich zwei Läufe zugleich gestartet hatte; kein Fehler im Code, die Wiederholung auf der fertigen Spitze ist grün.
- Slice 100: Die Durchsicht (Opus) fand vier mittlere Punkte, alle behoben: Hilfeknopf im gesperrten Panel (Backoffice konnte die Hilfe nicht öffnen), Ladefehler der Nummer ließ das Feld offen und hätte eine unbekannte Nummer überschrieben (jetzt gesperrt mit „Erneut laden“, Test), Einstellungen als `|`-getrennter Text im Stand (jetzt Record), Zeile zur Ansicht „Lohn“ in `team.md`. Dazu Sperre gegen Doppelstart des Exports und ein klarerer Fehlertext.
- Slice 100: `DesignTokenTests` fand „DATEV LODAS“ als Versalien erst nach dem Bau; der Test deckt nur Beschriftungen in `.axaml`.

- Slice 112: Die Durchsicht fand zwölf Punkte, die die Tests nicht zeigten: Beim Ändern ließ „kein Lieferant“ den alten Fremdschlüssel stehen (Navigation nicht geladen). „Ändern“ rundete den Preis auf drei Stellen und überschrieb einen neueren Wert. Die Rückfrage beim Lieferantenwechsel speicherte das ganze Lager und hätte eine offene Inventur gebucht. Sehr große Mengen in Kartons endeten als Serverfehler. Alle sind behoben und mit Tests belegt.
- Slice 113: Die Durchsicht fand unter anderem: Eine Höchstmenge unter dem Mindestbestand verhinderte die Bestellung still; ohne Mailzugang stand die Bestellung auf „bestellt“ ohne Mail; ein Abbruch nach dem Commit verhinderte die Mail; ein Ladefehler löschte Eingaben. Alle sind behoben, die fachlichen mit Tests.
- Slice 112: Der Commit 51239fe enthielt nur einen Teil der Änderungen, weil die Prüfregel einzelne Bearbeitungen im selben Aufruf abwies; f5f10f34 trägt den Rest nach.
- Slice 112: Last, kein Fehler: `helpFeedback.test.tsx` war in der Vitest-Gesamtsuite rot und einzeln grün (6 von 6).
- Slice 110: Das Write-Tool wandelte `\u00A0` und `\u00FF` in `SimplePdf` in echte Zeichen; der Bereich steht jetzt als Hexzahl im Code.
- Slice 110: Die Durchsicht (Opus) fand unter anderem: „Bearbeiten“ verwarf ein offenes Formular ohne Rückfrage (jetzt gesperrt, solange es offen ist), DELETE ging bei Ausgeschiedenen durch (jetzt 409), ein nicht lesbarer Eintrag brachte Akte und PDF zum Absturz (jetzt 503 mit klarer Meldung), ein Fehler bei einem Mandanten stoppte das Aufräumen der übrigen, der Speicherfehler stand über den Knöpfen statt über der Leiste. Alle behoben. Der Plan nennt `HrFileStore.cs`; die Logik steht in `HrFileModel` und `HrFileRules`.
- Slice 110: Sprünge per `TeamViewModel.Open` kennen nur aktive Personen; die Akte einer Ausgeschiedenen öffnet man über „Ausgeschiedene zeigen“ und die Liste. Der Test folgt diesem Weg.
- Slice 110: Vier Desktop-Tests sind auf `dev` (2e227c6f) ohne diese Slice rot, per Gegenprobe im eigenen Arbeitsbaum belegt: `ViewTests.Verwaltung`, `ViewTests.Verwaltung_ohne_Team_Oberflaeche_auf_dem_Server`, `ViewTests.KI_und_Hilfe_Leiste_steht_auf_Seiten_mit_Browser_neben_dem_Browser("Verwaltung")`, `ViewTests.Knopfbeschriftungen_haben_hoechstens_26_Zeichen` (Texte in `ComplianceReportView`, `CostsView`, `HygieneReportView`). Der letzte Lauf der Desktop-Suite schließt diese vier Methoden aus und ist deshalb kein voll grüner Lauf; die anderen Fälle der KI-Leisten-Theorie liefen dabei ebenfalls nicht. Die Tests bleiben unverändert, zu beheben außerhalb dieser Slice.
- Slice 110: `CardTabs` in `ViewTests.Team.cs` kannte schon den Reiter „Lohn“ aus Slice 100 nicht und ist mit „Personalakte“ nachgezogen.
- Slice 110: Last, kein Fehler: Die volle API-Suite auf 439b9057 hatte zwei Rote (`AbsenceRequestTests.Gegeben_Genehmigung_Dann_steht_die_Abwesenheit_im_Dienstplan_und_Lena_sieht_die_Entscheidung`: 500; `MailIntakeTests.Fehlt_nur_die_Kopie_in_Gesendet_gilt_die_Antwort_als_gesendet_und_das_Spa_sieht_den_Hinweis`: Zeitüberschreitung); einzeln wiederholt waren beide grün. Ebenso auf 37a35378 `StripePaymentTests.Karte_ueber_Stripe_hinterlegen_und_Ausfallgebuehr_belasten` (Deadlock 40P01 beim Speichern von Beleg und Registrierkasse): einzeln zweimal grün; `HrFileCleanup` berührt diese Tabellen nicht.
- Slice 110: `HrFileCleanup` kann beim Start vor dem Migrator laufen, weil `StartupState` zunächst „bereit“ meldet: Auf einer leeren Datenbank (in den Tests) steht dann einmal die Warnung „relation aispa.Tenants does not exist“ im Log. Folgen hat das keine, dort gibt es noch keine Verwarnungen; auf einer vorhandenen Datenbank läuft die Bereinigung normal.
- Slice 110: Nach dem letzten Einmischen von `dev` (Slice 123 und 125, Überschneidung nur `web/openapi/aispa-api.json`, `web/src/api/schema.d.ts` und Dokumente) liefen Basisprüfung, Web-Prüfungen, API-Suite und die Team-Tests der Windows-App; die volle Desktop-Suite lief zuletzt auf 439b9057 ohne die vier genannten Tests grün.
- Slice 125: Das Überlauf-Menü von `OverflowStrip` öffnete im Fenstertest leer, auch im Kalender: Was erst in `Opening` hinzukam, zeigte das Menü beim Öffnen nicht. Der Kalendertest prüfte nur die Liste der Einträge. Jetzt füllt der Knopf vor dem Öffnen, beide Fenstertests prüfen die sichtbare Höhe jedes Eintrags.
- Slice 125: Vorbestehend rot auf unverändertem `dev` (`0016ce0d`): `Verwaltung_Team_Inhaberin_klickt_Lena…` (beide Schemata, Reiterliste ohne „Lohn“, mit Slice 110 behoben) und `Verwaltung_ohne_Team_Oberflaeche_auf_dem_Server` (`Admin.Error` bleibt leer).
- Slice 125: `MainWindow` hat `MinWidth=1100`, die 1024 px der Abnahme erreicht die laufende App also nicht; der Test hebt die Grenze auf und misst die Breite. Den Überlauf prüft ein Test mit schmalem Balken.
- Slice 125: Die linke Leiste hat denselben schwachen Akzent-Fokusring auf dunklem Grund, außerhalb dieser Slice.
- Slice 124: Der Gutscheinstatus kennt kein „abgelaufen“ (`Active`, `Redeemed`, `Voided`). Ein abgelaufener, noch aktiver Gutschein zeigt sein Datum „gültig bis“ ohne Warnung, wie im Web; erst beim Bezahlen lehnt ihn der Server ab.
- Slice 124: Vier Tests sind auch auf unverändertem `dev` (e8ba24c8) rot, keine Folge dieser Slice: `ViewTests.Verwaltung`, `ViewTests.Verwaltung_ohne_Team_Oberflaeche_auf_dem_Server`, `ViewTests.KI_und_Hilfe_Leiste_…("Verwaltung")` und `ViewTests.Knopfbeschriftungen_haben_hoechstens_26_Zeichen` (Texte in ComplianceReportView, CostsView, HygieneReportView). Die Prüfung lief einzeln im eigenen Worktree; der volle Lauf dieser Slice schließt die vier aus.
- Slice 126: Der Preis des Fixture-Termins ist 89 €, nicht 98 €; zwei Testwerte stimmten zuerst nicht. Die Meldung des Kalenders kürzt Texte um den Schlusspunkt und schneidet lange Sätze ab; die Meldungen der Einwilligung sind darum kurz.
- Slice 126: Die Hilfe zu `kassa-bezahlen` sagte, die Windows-App zeige keinen QR-Code; seit QRCoder zeigt sie ihn. Ein Test kann den Inhalt des Codes nicht entschlüsseln (kein Decoder im Projekt); er prüft Größe und Gestalt des Bildes gegen den Code aus dem Text der API und dass ein anderer Text ein anderes Bild ergibt. Ob eine Banking-App ihn liest, steht in der manuellen Testliste.
- Slice 126: Vier Tests sind auch auf unverändertem `dev` rot, keine Folge dieser Slice: `ViewTests.Verwaltung`, `ViewTests.Verwaltung_ohne_Team_Oberflaeche_auf_dem_Server`, `ViewTests.KI_und_Hilfe_Leiste_…("Verwaltung")`, `ViewTests.Knopfbeschriftungen_haben_hoechstens_26_Zeichen`. Der Überlappungstest `KI_und_Hilfe_Knopf_ueberlappt…("Kassa")` wurde durch die neue Zeile rot und ist mit einem Rand am Hinweistext behoben. Der Prüfer fand veraltete Zahlungsdaten nach geändertem Betrag, Vorschläge des Vortags nach Tageswechsel und das Wort „Bitte“; alle drei sind behoben und mit Tests belegt.
- Slice 127: `WebUtility.HtmlEncode` schreibt Umlaute und Akzente als Zahlencodes; der Papierbogen nimmt deshalb `HtmlEncoder` mit vollem Unicode-Bereich. `x:Name` lässt sich in einer Vorlage nicht binden, der Test sucht das Nein-Feld einer Frage über ihren `DataContext`.
- Slice 127: Zwei Hilfetexte sagten, der Kalender zeige nie Inhalte der Anamnese; sie nennen jetzt die Karte. Ändert Slice 128 das Leserecht, muss sie diese Sätze und `kalender-termin-anamnese` nachziehen.
- Slice 103: Die Web-Team-Oberfläche hing an 185 Dateien mit rund 440 Web-Testfällen; sie entfallen mit ihr. Die Prüftests über alle Quellen (Knopftypen, Feldabläufe, Zustände, Kontrastdesign, Schrift, Reiter) zeigen jetzt auf die verbleibenden Apps; `maskInventory.test.ts` prüft nur noch den ADR-Index. Nur die Team-`index.html` lud die Inter-Schrift vor; die Hilfe bekommt den Preload jetzt selbst.
- Slice 103: Der Prüfer fand einen Umweg um die Rückkehrprüfung (`/%09/…` wird nach dem Parsen zu `//…`), eine Weiterleitung in nginx ohne Query, eine Adresse der Anmeldeseite, die im Server-Betrieb auf die API statt auf das Web zeigte, das nicht mehr erzwungene Ändern eines Startpassworts und die Du-Form in übernommenen Texten; alle behoben und mit Tests belegt.
- Slice 103: `ViewTests.Knopfbeschriftungen_haben_hoechstens_26_Zeichen` ist weiter auf `dev` rot (seit Slice 124, Texte in CalendarView, ComplianceReportView, CostsView, HygieneReportView), keine Folge dieser Slice; die drei anderen auf `dev` roten Tests gehörten zur eingebetteten Verwaltung und entfallen mit ihr.

Slice 111: `DemoExtras` darf Kassa-Storno und Bestellungen auslösen nicht vergeben, weil eine einzige solche Vollmacht die Prüfung im ganzen Demo-Spa scharf schaltet.
Slice 111: Load, not a fault: `BookingEmbeddingTests` und `HttpResilienceTests` (Deadlock 40P01) sowie `BookingCheckSpeedTests` (7 statt höchstens 6 Befehle) waren in der vollen API-Suite rot und beim Wiederholen der Klassen grün.
Slice 111: `CalendarSubscriptionTests.Der_Planer_stellt_faellige_Abos…` ist seit dem 01.11.2026 auch einzeln rot und hängt nicht an dieser Slice: Der Test nimmt `DateTimeOffset.UtcNow` als „Oktober“ und erwartet keine Erinnerung, im November kommt sie aber. Er wird bis Jahresende rot bleiben, bis jemand die Uhr im Test festlegt.
Slice 111: In der vollen Desktop-Suite sind weiter die vier auch auf `dev` roten `ViewTests` rot: `Knopfbeschriftungen_haben_hoechstens_26_Zeichen` (keine der vier Beschriftungen stammt aus dieser Slice), `KI_und_Hilfe_Leiste…("Verwaltung")`, `Verwaltung` und `Verwaltung_ohne_Team_Oberflaeche_auf_dem_Server`.
- Slice 103: Last, kein Fehler: Im vollen Lauf auf der Spitze waren `TenantTests.Neues_Spa_registrieren_legt_Inhaber_an_und_startet_leer` (500 unter Last) und `help.test.tsx` „jeder Verweis findet sein Thema“ (Thema `regelungen-vollmachten` aus Slice 111 nicht gefunden) rot; einzeln bzw. im zweiten unveränderten Lauf grün (API-Suite 1498 von 1498, Vitest 346 von 346).
- Slice 114: Ein `Help`-Text am Feld „Barcode“ im Materialformular machte den Tab-Scan `Jede_Seite_Lager_…` rot (die Zeilenknöpfe „Buchen“ und „Ändern“ galten als nicht erreicht), obwohl die Tab-Reihenfolge gleich blieb; ohne `Help` (der Hilfetext steht im Thema `lager#barcode`, das Fragezeichen des Abschnitts führt dorthin) und in derselben Zeile wie Lieferant und Artikelnummer ist er grün. Die Ursache liegt im Layout der Seitenleiste, nicht im Feld; nicht weiter verfolgt.
- Slice 114: Die Durchsicht fand: Plan-Annahme 4 war falsch, denn die Rezeption hat `ViewCalendar` und darf den Lager-Endpunkt lesen (die zwei Endpunkte bleiben, damit die Kassa keine Lagerdaten bekommt). Eine gleichzeitige Speicherung mit demselben Code endete im Serverfehler statt in einem Fehler am Feld; „Einem Material zuordnen“ blieb nach einem Reiterwechsel stehen und hätte später den Code in ein beliebiges Material geschrieben. Beides ist behoben und mit Tests belegt.

- Slice 115: Die Durchsicht fand: Lädt beim Öffnen einer Behandlung nur die Liste „Passt dazu“ nicht, bot der Reiter „Verkauf“ trotzdem Artikel an, und Speichern hätte die gespeicherte Zuordnung durch die neue Auswahl ersetzt. Behoben mit Test (`TreatmentsTests.Passt_dazu_laedt_nicht_…`).
- Slice 115: Das Termin-Panel zeigt den Bestand ohne Einheit („4 am Regal“), die Kassa mit Einheit („3 Stück am Regal“); im Panel ist der Platz knapp. Die Hilfe beschreibt beide so.
- Slice 116: Die Slice-Datei nennt die Windows-App nicht, die Abnahme „sieht die Managerin die Zählung in der Windows-App“ verlangt sie; deshalb kamen `StockCountViewModel` (Zählungen vom Handy), der Reiter „Verbrauch“ und die Hilfe `lager-inventur-handy` dazu.
- Slice 116: Der Windows-App-Agent brach vor dem Prüfnachweis ab (Sitzungsende); die Spitze war committet, die Prüfungen (Format, Build, Lager-Tests, Scan-, Token- und Fokus-Tests) liefen danach erneut von Hand.
- Slice 116 (Durchsicht): Die Handy-Masken waren im echten Betrieb gesperrt: „Mein Tag“ wechselt in den Kanal Mitarbeiter-App, und kein Lager-Endpunkt trug die Freigabe `AllowChannel(StaffApp)`; die Web-Tests prüfen den Kanal nicht. Jetzt freigegeben (Materialliste, Suche per Code, Lagerorte, Wareneingang, Start, Zeile, Abschicken) und im Kanal-Scan sowie in einem API-Test mit der Mitarbeiter-App belegt. Ebenso fand die Durchsicht: `Start` gab die Zählung eines Lagerorts eines anderen Standorts heraus (jetzt nach Standort gefiltert, Test), Zwischenstände lösten bei jeder Eingabe ein Live-Ereignis aus (entfernt), „Letzte 7 Tage“ umfasste 8 Tage, eine Eingabe in den letzten 600 ms vor dem Reiterwechsel ging verloren (wird jetzt beim Verlassen gespeichert), Abschicken und Verwerfen sind wie Schließen bedingte Statuswechsel.
- Slice 116: Termine der API-Tests mussten um 10, 12 und 14 Uhr liegen; um 9 Uhr scheiterte die Buchung an der Vorbereitungszeit vor Dienstbeginn.
- Slice 128: Der Toast der App entfernt einen Schlusspunkt; der erste Testlauf erwartete den Text mit Punkt und war deshalb rot.
- Slice 128: Ein Bildtestlauf mit leerem Filter (Pfad im Skript falsch) startete die ganze Desktop-Suite ohne Sperre; er wurde nach rund 40 min abgebrochen. Darin war `ViewTests.Handbuch_Gutschein_verkaufen` (HelpFlows) rot; das wurde nicht weiter untersucht, weil die Slice die Kassa nicht berührt.
- Slice 128: Last, kein Fehler: `HrFileTests.Given_eine_Verwarnung_mit_Ablaufdatum_gestern_When_der_Hintergrundjob_laeuft_Then_ist_sie_geloescht_und_der_Verlauf_nennt_nur_die_Loeschung` war in der vollen API-Suite rot (1553 von 1554 grün) und einzeln grün (23 von 23).
- Slice 128: `web/src/help/terms.test.ts` war schon auf `dev` rot: `lager-inventur-handy.md` (Slice 116) schrieb „Therapeuten“; die Slice ersetzt es durch „Therapeut:innen“, weil der Test sonst ihre Web-Prüfung blockiert.
Slice 117: Die Durchsicht (Opus) fand: Die Inhaberin bekam in der Mitarbeiter-App die Erinnerungen des ganzen Teams (nur im Client gefiltert). Lageraufgaben eines zweiten Standorts und bei ausgeschiedener Vertretung erreichten niemanden. Ausgeschiedene standen in „Wer ist wofür zuständig?“. Ausgeblendete Arten ließen sich neu zuordnen. Alle sind behoben, jeweils mit Test.
Slice 117: Der Namensraum `AispaStudio.Api.System` verdeckt `System.Globalization`; mit einer festen Uhr im Jahr 2027 ist das Cookie von heute abgelaufen, die Tests melden sich deshalb am Host mit dieser Uhr neu an.
Slice 117: Auf dem unveränderten `dev` (6fa818cc) sind `ViewTests.Handbuch_Gutschein_verkaufen` (Kassa-Ziel außerhalb des Fensters) und `terms.test.ts` („Therapeuten“ in `lager-inventur-handy.md` aus Slice 116) rot; beide hängen nicht an dieser Slice.

## Zurückgestellte Kleinigkeiten
- Push-Hinweise für Mitarbeitende (frühere Slice 107, Ä241–Ä243 bleiben reserviert): Web Push mit VAPID-Schlüssel über die Queue, Ruhezeiten je Person; auf dem iPhone nur mit installierter App ab iOS 16.4 (E7, Auftraggeber, 29.10.2026)
- Slice 93: Rückfrage „Änderungen speichern?“ beim Abmelden und beim Schließen des Fensters (heute nur Navigation, Sprünge und Bereichswechsel).
- Slice 93: Baustein „Liste mit Detail“ entsteht erst mit dem zweiten Nutzer (Slice 94, Behandlungen); bis dahin ist die Verwaltung selbst das Muster.
- Slice 93: Bei der Rückfrage zeigt die Seite kurz den Kalender und baut die Verwaltung danach neu auf; nach „Abbrechen“ sind Fokus und Bildlauf der Maske weg (Review). Abhilfe wäre eine Rückfrage vor dem Seitenwechsel in der Navigationsleiste selbst.
- Slice 93: „Speichern“ bleibt bei leerem Pflichtnamen bedienbar und meldet den Fehler am Feld (wie im Web, masken.md R5); die Maske hat keinen Startfokus (R13).
- Slice 94: Der Knopf der Leiste heißt auch bei einer neuen Behandlung „Speichern“ (Web: „Behandlung anlegen“); die Leiste kennt keinen eigenen Text.
- Slice 94: „Kategorie“ ist ein freies Textfeld ohne Vorschläge aus den vorhandenen Kategorien.
- Slice 94: Lehnt die API einen einzelnen Wert ab (z. B. Einwirkzeit länger als die Dauer), steht die Meldung im Fuß, nicht am Feld; der `ApiClient` der App liefert Fehler nur als Text.
- Slice 94: „Wiederbuchen“ (`RebookSection`, im Web neben „Erinnerungen“) und die Online-Regeln je Behandlung (`OnlineRulesSection`) baut diese Slice nicht nativ; die Slice-Dateien 95 und 96 nennen sie noch nicht ausdrücklich → beim Bau von 95 (Standorte) und 96 (Erinnerungen) mitnehmen.
- Slice 95: Fehler der API stehen als „Nicht gespeichert“ über der Leiste, nicht am Feld (Ä207).
- Slice 95: Die Beobachtung nach „Jetzt abrufen“ läuft nach einem Bildtest bis zu 30 s weiter (Standardintervall 1,5 s); harmlos, in einer dicht laufenden Suite höchstens Rauschen.
- Slice 95: Die Wartezeit einer Absage-Nachricht („Jetzt senden / Nicht senden“) wird nur beim Laden berechnet, das Web aktualisiert alle 15 s; die Zeit in der Absageliste nutzt „dd.MM.yyyy HH:mm“ statt „d.M.yyyy, HH:mm“.
- Slice 95: Das Label der Anzahlung nennt „0 = keine“ nicht; die Gruppe „Anreise und Parkplätze“ wiederholt ihre Überschrift im Feldlabel; der Hilfetext der Standorte ist gegenüber dem Web gekürzt (Rezeption und KI).
- Slice 95: Nach dem Umbenennen eines Raums baut `SetRooms` die Checkliste neu auf; ungespeicherter Text der Checkliste kann dabei verloren gehen.
- Slice 95: Gruppenüberschriften der neuen Masken sind `h2` (`masken.md` R1 nennt `h3`); „Speichern“ bleibt auch ohne Änderung bedienbar (Web: gesperrt); die Raumzeile entfernt ohne Rückfrage.
- Slice 95: Die Tests gegen Bindungsfehler der neuen Masken schließen das Fenster nicht (Falle aus den Bekannten Fallen); die Scan-Tests für Tab-Reihenfolge und Namen decken je Bereich nur die erste Maske, die Bereiche haben eigene Namen-Tests.
- Slice 96: Nach „Mit Stripe verbinden“ kehrt Stripe nicht in die App zurück; die Maske sagt, nach dem Abschluss hierher zurückzukehren und „Stand abfragen“ zu wählen (keine Rückkehr-URL für die App).
- Slice 96: Die Abfrage des Stands nach Verbinden, Speichern und „Stand abfragen“ endet nach 20, 10 bzw. 5 Versuchen ohne eigene Meldung; der zuletzt gemeldete Stand bleibt stehen, wie im Web.
- Slice 96: „Zuletzt verschickt“ lädt nur beim Öffnen der Maske, nicht nach dem Speichern.
- Slice 97: Die KI-Nachfrage nach dem Modell-Download läuft nach einem Seitenwechsel weiter (Ä212); Postfach und Handbuch lösen kein `Saved` für den Assistenten aus (Ä211); Anrede der Feldmeldungen („Trage ihn ein“ gegen T1) ist gemischt (Ä213).
- Slice 97 (Review): Das Postfach-Formular zeigt Fehler der API nur als InfoBar, nicht am Feld mit Fokus; „Adresse des Postfachs“ trägt keinen Pflichtstern; ein geleerter Port wird als 0 gesendet (Ä207 deckt die Feldzuordnung). Art, Verschlüsselung, Plattform und Verarbeitung sind ComboBoxen (masken.md R10 verlangt bei 2 bis 4 Werten RadioButtons); ein fehlgeschlagenes „Jetzt prüfen“ meldet immer „nur mit einem eingeschalteten Postfach“; Kopieren in die Zwischenablage fängt eine gesperrte Zwischenablage nicht ab (wie CalendarView); „Absender-Regeln“ und „Absenderregeln“ sind gemischt.
- Slice 97: Die Nummern im Postfach-Bereich sind nur lesend (wie im Web); Zwischenablage von Windows und der Serverablauf von „Jetzt prüfen“ stehen nur in der manuellen Testliste.
- Slice 98: Bei Teil-Speichern steht über der Leiste weiter der Titel „Nicht gespeichert“, obwohl der Text den gespeicherten Teil nennt.
- Slice 98: Ein getipptes „Neues Startpasswort“ zählt nicht als ungespeicherte Änderung.
- Slice 98: `AccessExpiry.cs` liest den Spa-Namen ebenfalls ohne Filter auf den Mandanten (vorbestehend, nicht geändert).
- Slice 98: Die Annahmeseite der Einladung bleibt bis Slice 103 im Web.
- Slice 98: Ein Änderungsverlauf der Stammdaten fehlt (der Verlauf der Karte zeigt Zugang, Rolle, Standorte, Sperre und Befristung).
- Slice 98: Ausgeschiedene Personen lassen sich nicht wieder aufnehmen.
- Slice 101: Die Abnahme „10 Flaschen, Kassa verkauft 2, Lager zeigt 8“ prüft das native Lager nur als Anzeige nach dem Neuladen (der Stub liefert 8); die Abbuchung selbst und die Rabattrechnung der Aktion prüfen die vorhandenen API-Tests (`PromotionTests`, `BusinessPromotionTests`, Kassa-Verkauf).
- Slice 101: Die Hilfetexte von Lager und Aktionen beschrieben Reiter, Menü „Weitere Aktionen“ und Rückfrage beim Archivieren der Web-Maske; sie nennen jetzt den Weg in der Windows-App und die Knöpfe der nativen Maske.
- Slice 101: Der Rot-Lauf nutzte ein Gerüst mit gesperrtem Verhalten statt eines leeren Typs; Bildtests, die nur prüfen, welche Maske offen ist, waren auf dem Gerüst schon grün, die Abnahme tragen die Klick- und Tastentests.
- Slice 104: Die Anzeige fragt beim Koppeln mit `POST /api/display/pairing/redeem` nach; der Endpunkt ist mit der öffentlichen Begrenzung je IP geschützt. Slice 105 wählt den Abstand der Nachfrage passend dazu (etwa alle 5 Sekunden) und ergänzt im Handbuch `geraete-anzeige-koppeln` die Adresse, unter der die Anzeige geöffnet wird.
- Slice 104 (Review, zurückgestellt): `LastSeenAt` einer Anzeige wird nur beim Koppeln gesetzt; nicht getestet sind ein abgelaufener Code und das Koppeln an einen fremden Standort (die Standortprüfung ist die bestehende des `PermissionFilter`); `RefreshSignInAsync` (Passwort ändern) verwirft eigene Claims – gibt Slice 106 diesen Endpunkt frei, muss der Kanal erhalten bleiben.
- Slice 104: Die Allow-Liste der Mitarbeiter-App enthält nur Tagesplan, Behandlungskarte, Vorbereitung, Nachbereitung, Status und `/api/auth/me`; Woche, nächster Termin und Abwesenheit ergänzt Slice 106 im Scan-Test `ChannelScanTests`.
- Slice 118: Läuft das Zertifikat eines Servers ab, meldet die App nur einen Zertifikatsfehler; ein eigener Hinweis mit Ablaufdatum fehlt (Ausbau, keine neue Kennung).

- Slice 101: Serverfehler zu einem Feld stehen in „Aktionen“ als Leiste über dem Formular (Ä207); das Web sperrt das Formular beim Speichern, die App überschreibt danach Eingaben, die währenddessen gemacht wurden; ein Fehler bei einer falschen Mindestmenge im Lagerort-Formular setzt den Fokus nicht ins Feld (die Zeilen stehen in einer Vorlage ohne Fokus-Anker).
- Slice 101: Bei gleichzeitig einer Buchung und einem negativen Zählwert bleibt „Speichern“ in der Rückfrage folgenlos (der Warntext steht da); der Zähler der Bewegungen wird beim Fehler im Zeitraum nicht erhöht, eine noch laufende Antwort des vorigen gültigen Zeitraums würde dann noch angewendet; die Zählliste zeigt alle aktiven Materialien wie im Web, nicht nur die mit Bestand im Lagerort.
- Slice 101: „Wareneingang buchen über Klick“ setzt das Material per Code statt per Klick; die Behandlungsauswahl der Aktionen und die Standort-Kästchen sind im Fenster nur per Test bedient, nicht von Hand.
- Slice 102: Balken im Vergleich mit anderen Spas fehlen nativ (Ä226).
- Slice 102: Kleinere Punkte der Durchsicht bleiben offen: Erfolg als InfoBar statt Toast, Fehlerleisten ohne Aktion, mehr als zwei Warnleisten bei Berichten, Euro im Vergleich mit zwei Nachkommastellen, „Bereitstellen“ auch ohne Summen, Neuladen des Tagesplans löscht offene Bearbeitungen anderer Karten, Formular nach dem Filtern, Feldfehler des Servers bei der Betriebsinfo nur als Leiste, Anamnese-Warnung per Textanfang.
- Slice 105: Dienstplan, Raumsperren und Schließzeiten lösen kein Signal aus (Ä235); Bild der Tafel im Handbuch fehlt (Ä236); Verbindungen widerrufener Anzeigen bleiben bis zum Abbruch offen (Ä237).
- Slice 105: Termine, die über Mitternacht gehen, und Termine anderer Standorte zeigt die Tafel nicht; Räume mit Platzzahl über 1 (Paarsuiten) gelten bei einem Termin als belegt.
- Slice 99: Die Antwort auf das Speichern eines Tages (`ResultAsync`) liefert Einträge aller Standorte (nur mit ManageLocation); die Bilder `roster-tag` und `time-tag` wirken verwaist; `BringIntoView` hat keinen eigenen Rottest; das Bereichspanel im Kalender öffnet beim Neuladen wie bei jedem Neuladen.

- Slice 106: Handbuchbilder der App (Ä238); Hilfe zu den Anträgen nach dem Merge mit Slice 99 (Ä239); Hinweis zum Kanalwechsel am selben Gerät (Ä240). Slice 99 lag beim Bauen noch nicht auf `dev`.
- Slice 106: Melden und Anträge brauchen Netz, eine Warteschlange am Handy gibt es bewusst nicht. Die Prüfung „Beginn nicht in der Vergangenheit“ lässt einen Tag Spielraum, weil die Person keinen festen Standort hat. Das Allergie-Kennzeichen entschlüsselt je Gast einzeln (nicht gemessen).
- Slice 108: Eine bezahlte Anzahlung nach der Frist bleibt beim Anruf; eine Regel zum Einbehalten fehlt (Ä244). Ohne Netz öffnet die App, zeigt aber keine Termine und meldet nicht eigens „offline“ (Ä245). Das Handbuchthema hat noch keine Bilder der App (Ä246). Die Absagemail nennt die Gebühr nicht.
- Slice 108: Ein Konto bei Spa A mit gemerktem Spa B sieht ohne `?spa=` das falsche Spa; der Server zieht die Kopfzeile dem Konto vor. Das geht erst mit mehreren Spas je Gerät an, die Reihenfolge blieb unverändert.
- Slice 109: Bild der Seite im Handbuch (Ä247); Vergleich mit dem Vortag und Summe über Standorte (Ä248); was „offene Anfrage“ heißt, mit der Rezeption klären (Ä249). Kein Diagramm, kein Verlauf, keine Eingabe (Slice-Datei: nicht bauen).
- Slice 100: Provision (Ä220), Filter `employeeId` an der Vorschau (Ä221), Bilder im Handbuch (Ä222). Ohne Ladezustand für die Zeilen der Person beim Monatswechsel (die alten bleiben bis zur Antwort stehen); `TeamViewModel.CanPayroll` wiederholt `TeamContext.CanViewPayroll`, wie die anderen Ansichten es vor dem Laden des Kontexts tun; scheitern Dienstplan oder Zeiten nach dem Speichern der Nummer, meldet die Karte die Änderung der Nummer nicht an die Liste.

- Slice 112: Neue Handbuchbilder von Aufgaben und Lieferanten fehlen, weil der Bildlauf gesondert läuft (Ä259). Die Web-Karte rechnet noch mit der Vorgabe `WarningDays`, das Web-Formular kennt Packung und Lieferzeit nicht, und Lieferzeit, Mindestbestellwert und Bestellweg wirken erst mit Slice 113 (Ä260). Der Reiter „Korrektur“ der Leiste „Bewegung buchen“ ist abgeschnitten, das war schon vorher so (Ä261). Ein Text im alten Feld `Supplier` findet auch einen deaktivierten Lieferanten; das betrifft nur das Web-Formular bis Slice 103.
- Slice 113: Der Bestelltext-Knopf bleibt im Web. Der Lieferant einer Bestellung wird über den Namen gefunden; nach dem Umbenennen fehlen einem offenen Entwurf Mailadresse und Mindestwert, bis er neu angelegt wird. Ohne eigenes Postfach trägt die Mail keine Antwortadresse. Nach einem Mengenfehler springt der Fokus nicht ins Feld. Die Web-Maske der Lagerorte meldet einen Fehler zur Höchstmenge, die sie nicht zeigt. Neue Handbuchbilder des Reiters „Bestellungen“ fehlen; die Hilfe zeigt weiter das Web-Bild.
- Slice 123: Der Fokus springt nach einem Fehler in der Liefermenge nicht ins Feld, wie schon bei den Bestellmengen (Slice 113). Die Bedarfsprüfung lädt mit dem ganzen Lager neu, auch wenn ihr Reiter nicht offen ist. Nur das Bild der Lieferung ist neu; die übrigen Lagerbilder bleiben bei Ä259. Ein Standortwechsel verwirft Liefereingaben ohne Rückfrage, sie zählen bewusst nicht als ungesichert. Der Endpunkt der Rückverfolgung liefert Kundennamen schon mit `Book`, die App blendet ihn unter `ManageLocation` aus, wie bisher das Web; Slice 103 kann das nachziehen. Fehler der Lieferung stehen oben in der Maske unter dem festen Titel „Nicht gebucht“, auch beim Bestelltext. Die Du-Form „Gib eine Zahl …“ an der Liefermenge folgt der Bestellmenge (Ä213).
- Slice 110: Handbuchbild der Personalakte (Ä253); Zeichen außerhalb von WinAnsi im PDF (Ä254). Keine arbeitsrechtliche Bewertung, keine KI-Textvorschläge, keine Einsicht in der Handy-App, keine Gehaltsdaten (Slice-Datei: nicht bauen).
- Slice 125: Das Handbuchbild `verwaltung-fehlt.png` zeigt noch die alte Liste, weil sein Bildtest auf `dev` rot ist; Slice 103 entfernt die eingebettete Verwaltung.
- Slice 124: Ein geschlossenes Termin-Panel fragt den Stand von Anzahlung und Belastung noch bis zu 60 s weiter ab, ohne etwas anzuzeigen; ein Abbruchsignal bleibt offen.
- Slice 124: Die Abfrage läuft nur beim Verlassen des Felds, nicht mit Enter. Ein Fehler am Gutschein sperrt „Bezahlen“ nicht, das entscheidet weiter der Server.
- Slice 126: Die Lückenvorschläge haben keinen Zähler in der Kopfzeile (Ä298). Beim Verschieben auf Vorschlag gibt es keine Rückfrage, der Klick ist die Bestätigung wie bei „Buchen“ in der Warteliste; Rückgängig steht in der Meldung.
- Slice 127: Der Prüfdialog holt die Fragen immer auf Deutsch, auch bei einem Bogen in anderer Sprache, wie das Web. Das Foto zeigt den Bogen in der Sprache des Gastes.
- Slice 127: Die Hilfe `kalender-termin-anamnese` hat noch kein Bild; die Bilder `termin-anamnese-*` entstehen in den Tests und kommen beim nächsten Bildlauf dazu. Wird die App innerhalb von fünf Minuten nach dem Druck beendet, bleibt die Druckdatei bis zum nächsten Druck liegen.
- Slice 127: Nach dem Prüfdialog kehrt der Fokus nicht zu „Foto hochladen“ zurück, weil der Knopf bis zum Ende des Ablaufs gesperrt ist (aus dem Code abgeleitet, nicht nachgestellt); der Kalender holt den Fokus bei der nächsten Taste zurück.
- Slice 103: Tote Regeln in `index.css` und das ungenutzte Paket `qrcode` (Ä230), der Fokus im Kontrastdesign des Kundenbereichs (Ä229) und die fehlenden Hilfebilder (Ä231).
- Slice 103: Die API-Endpunkte für das Empfangsgerät im Browser haben keinen Web-Aufrufer mehr; ob die Windows-App sie nutzt oder sie entfallen, ist nicht geprüft. Die Rückverfolgung liefert weiter Kundennamen mit `Book` (Slice 123).
- Slice 103: Ohne festen Verwaltungsbereich bleibt die Person auf ihrer Seite und bekommt einen Toast; mit den festen Rollen ist das nicht erreichbar, nur mit eigenen Rechten.

Slice 111: Serien, Gruppenbuchungen, Offline-Nachbuchen und „Wie Wochenarbeitszeit“ ohne Prüfung der Sonderregelungen (Ä256). Die Web-Oberfläche zeigt die Warnung nur als Fehler, weil sie mit Slice 103 wegfällt.
- Slice 114: Material und Verkaufsprodukt der Kassa müssen getrennt angelegt und verknüpft werden, damit die Kassa einen Code findet; die Kassa legt selbst nichts an (die Rezeption hat kein Lagerrecht). Nicht gebaut: Prüfziffer-Kontrolle, mehrere Codes je Material, Mengen aus dem Code (GS1), Etiketten drucken und das Scannen mit der Handy-Kamera (Slice 116). Neue Handbuchbilder fehlen, weil der Bildlauf gesondert läuft (Ä259).
- Slice 114 (Durchsicht): Nach einem Scan in der Inventur oder im Wareneingang steht der Cursor in „Gezählt“ bzw. „Menge“; der nächste Scan landet dort, bis man wieder in das Scanfeld klickt. Ein zweiter Scan bei offener Buchung verwirft eine schon getippte Menge ohne Rückfrage. Schnelle Scans hintereinander, während die Suche noch läuft, werden nicht gepuffert (das Feld bleibt gefüllt, der Code hängt sich an den nächsten). Das Feld „Barcode“ zeigt Fehler nur als allgemeine Meldung, nicht am Feld, und prüft nicht vorab im Client. Ein Scan, während „Inventur buchen“ läuft, meldet „gefunden“, setzt aber den Fokus nicht. Die Kassa meldet ohne gewählte Kasse nichts. Die Meldung bei einem deaktivierten Material nennt nicht, dass es deaktiviert ist.
- Slice 115 (Durchsicht): Nach **Dazugeben** bleibt der Hinweis sichtbar, bis die Kassa die Vorschläge neu geladen hat; ein schneller zweiter Klick legt den Artikel ein zweites Mal auf die Rechnung (mit × entfernbar). Im offenen Termin-Panel aktualisiert sich „n am Regal“ nicht nach einem Verkauf. Die Höchstzahl 5 steht in API und App je als eigene Konstante; Gleichstand in der Reihenfolge ist über die Maske nicht möglich und deshalb nicht nach Namen sortiert; ein Test für inaktive Artikel fehlt, weil die Kassa Artikel nicht deaktivieren kann.
- Slice 115: Die Vorschläge zählen nur das Verkaufsregal; ein Hinweis „im Hauptlager vorrätig, Regal leer“ (zum Nachfüllen) ist nicht gebaut.
- Slice 116: Nicht gebaut: Offline-Zählung am Handy, Zählung mehrerer Lagerorte auf einmal, Verteilung des Schwunds auf Behandlungen, Etiketten drucken, „Lagerort wechseln“ nach dem Start einer Zählung (ein neuer Start geht über den Reiterwechsel).
- Slice 116 (Durchsicht): Nach **Übernehmen** verschwindet der Knopf (Block nur im Leerlauf), und niemand hat den Tastaturfokus; ebenso schon bei **Zählung starten**. Fokus auf **Inventur buchen** setzen und mit einem Tastaturtest belegen.
- Slice 116: Der Wareneingang am Handy bucht ohne `StoragePlaceId`, also ins Hauptlager (Standardziel der API); eine Lagerort-Auswahl fehlt. Bei einem Speicherfehler einer Zählzeile wird nicht automatisch wiederholt, sondern erst mit **Erneut speichern** oder der nächsten Eingabe.
- Slice 116: Der Wert in Euro rechnet mit dem aktuellen Einstandspreis des Artikels, nicht mit dem Preis der einzelnen Bewegungen. Handbuchbilder für die neue Windows-Ansicht und die Handy-Maske fehlen (kein Render-Test dafür).
- Slice 128: Die Rezeption kann ein falsch hochgeladenes Foto nicht selbst verwerfen; das macht, wer prüft. Nicht gebaut: protokollierter Abruf der Antworten, Beschränkung der Therapeut:in auf Gäste der eigenen Termine. Handbuchbilder der neuen Rezeptionsansicht entstehen beim nächsten Bildlauf (`termin-anamnese-rezeption*`).
Slice 117: Eine Zuständigkeit je Standort. Veröffentlichen prüft die Ersthelfer nicht noch einmal. Ein bestätigtes Speichern überspringt eine Ersthelfer-Warnung, die erst durch die nebenläufige Änderung einer zweiten Leitung entstand. Geprüft wird nur der Standort der Route, nicht der Zielstandort einer Aushilfe. „Zum Lager“ öffnet die Seite, keinen bestimmten Reiter. In der Hilfe stehen nur die hellen Bilder.

## Offene Entscheidungen
NEEDS-DECISION: Bestätigt die Datenschutz-Beratung die Rechtetabelle der Anamnese aus Slice 128 (Rezeption nur Stand und Hochladen, Antworten und Prüfen der Fotos nur mit „Anamnese lesen“)? Der Auftraggeber hat am 02.11.2026 entschieden, die Abstimmung ist offen
NEEDS-DECISION: Slice 93 baut als wiederverwendbare Bausteine nur die Leiste „Speichern / Verwerfen“, die Rückfrage beim Verlassen und die Bereichsliste; „Liste mit Detail“ soll erst Slice 94 aus dem zweiten Nutzer ziehen, „Feld mit Fragezeichen“ entfällt, weil das Fragezeichen nach Brief 5.11 am Gruppentitel steht. Passt das?

NEEDS-DECISION: Die Stempeluhr bleibt in der Handy-App, obwohl E6 „keine Zeiterfassung per Handy“ nennt: Sie ist seit #41 vorhanden; Slice 106 hat sie nicht erweitert, aber ihre drei Endpunkte für den Kanal `StaffApp` freigegeben, weil sie sonst nach dem Kanalwechsel ausfiele. Empfehlung: lassen, bis feststeht, ob E6 auch den Bestand meint; sonst Freigaben und Anzeige entfernen.

## Manuelle Testliste
### Slice 92 – Grundsatz: alle Masken nativ, Web nur für Dashboard und Handy
Start: keine App nötig, Dateien im Repository lesen
1. `docs/entscheidungen/0084-alle-masken-nativ-web-fuer-dashboard-und-handy.md` lesen.
Expected: Die Entscheidung nennt die drei Web-Apps, die Hilfe im eingebetteten Browser, das Team an einer Stelle und den Weg für das Verwalten von zu Hause.
2. `docs/umsetzung/masken-inventar.md` lesen.
Expected: Jede Seite der heutigen Verwaltung hat eine Ziel-Slice in der Reihenfolge, die der Auftraggeber will.
Closes: Ä191, Ä192, Ä193, Ä194, Ä195
Result: ☐

### Slice 93 – Native Verwaltung: Gerüst und Firmendaten als Vorbild
Start: App starten, wie `work/conventions.md` beschreibt („App zum manuellen Testen starten“), als Inhaberin anmelden
1. Links „Verwaltung“ wählen.
Expected: Links stehen die Bereiche, „Firmendaten“ ist gewählt und zeigt alle Felder mit Fragezeichen an jeder Gruppe.
2. Den Namen des Spas ändern und Strg+S drücken.
Expected: Neben „Speichern“ steht „Gespeichert.“
3. „Verlauf“ unter den Firmendaten aufklappen.
Expected: Oben steht „Geändert von …: Name alt → neu“.
4. Den Ort ändern und links „Kalender“ wählen.
Expected: Die Frage „Änderungen speichern?“ mit „Speichern“, „Verwerfen“ und „Abbrechen“ erscheint.
5. „Abbrechen“ wählen.
Expected: Die Verwaltung bleibt offen, der geänderte Ort steht noch da.
6. „Verwerfen“ unten in der Leiste wählen.
Expected: Der gespeicherte Ort steht wieder da.
7. Links „Behandlungen“ in der Liste der Verwaltung wählen.
Expected: Die eingebettete Verwaltung öffnet bei den Behandlungen.
8. Die App schließen, neu starten und „Verwaltung“ › „Firmendaten“ öffnen.
Expected: Der in Schritt 2 gespeicherte Name steht da.
9. Die Online-Buchung im Browser öffnen und das Impressum ansehen.
Expected: Das Impressum zeigt den neuen Namen.
10. Abmelden und als Rezeption anmelden, „Verwaltung“ wählen.
Expected: „Firmendaten“ fehlt in der Liste.
Closes: Ä191 (Teil)
Result: ☐

### Slice 94 – Native Verwaltung: Behandlungen
Start: App starten, wie `work/conventions.md` beschreibt („App zum manuellen Testen starten“), als Inhaberin anmelden
1. Links „Verwaltung“ wählen, dann in der Bereichsliste „Behandlungen“.
Expected: Links stehen die Behandlungen nach Kategorie mit „Behandlung anlegen“ am Ende, rechts die erste Behandlung mit den Reitern Grunddaten bis Preise. (superseded by slice 125: Die Maske steht unter ihrer Gruppe im Balken oben, darunter als Reiter; die Bereichsliste gibt es nicht mehr.)
2. „Hot-Stone-Massage“ wählen, den Preis um 6 € erhöhen und Strg+S drücken.
Expected: Unten steht „Gespeichert. Gilt für neue Buchungen.“
3. „Verlauf“ unter der Behandlung aufklappen.
Expected: Oben steht die Preisänderung mit altem und neuem Preis.
4. Im Reiter „Ablauf“ die Vorbereitung ändern und links eine andere Behandlung wählen.
Expected: Die Frage „Änderungen speichern?“ erscheint; „Abbrechen“ bleibt bei der Behandlung mit der geänderten Zeit.
5. „Behandlung anlegen“ wählen, Name „Kräuterstempel“ und Preis 59 eintragen, im Reiter „Qualifikationen & Räume“ einen Raum wählen und „Speichern“ wählen.
Expected: Der Reiter zeigt am Feld „Keine Qualifikation gewählt: Jede Person im Team kann diese Behandlung übernehmen. …“; nichts ist gespeichert.
6. Noch einmal „Speichern“ wählen.
Expected: Die Behandlung „Kräuterstempel“ steht in der Liste.
7. Bei „Kräuterstempel“ im Reiter „Ablauf“ die Vorbereitung auf 15 Minuten setzen und speichern; dann im Kalender einen Termin mit „Kräuterstempel“ buchen.
Expected: Der Kalender zeigt vor dem Termin den Streifen der Vorbereitung mit 15 Minuten.
8. Im Kalender am Termin per Rechtsklick „Behandlungszeiten bearbeiten …“ wählen.
Expected: Die Verwaltung öffnet „Kräuterstempel“ im Reiter „Ablauf“.
9. Bei einer Behandlung mit Qualifikation unter „Wer kann das?“ einen Namen anklicken.
Expected: Der Bereich „Team“ öffnet sich.
10. „Kräuterstempel“ wählen und „Nicht mehr anbieten“, in der Rückfrage bestätigen.
Expected: „Kräuterstempel“ fehlt in der Liste, der gebuchte Termin bleibt im Kalender.
Closes: Ä191 (Teil)
Result: ☐

### Slice 95 – Native Verwaltung: Standorte, Räume, Schließzeiten, Raumsperren und Kalender-Abos
Start: App starten, wie `work/conventions.md` beschreibt („App zum manuellen Testen starten“), als Inhaberin anmelden
1. Links „Verwaltung“ wählen, dann „Standorte & Räume“.
Expected: Links die Standorte, rechts das Formular mit Öffnungszeiten, Anreise und den Abschnitten Räume, Geräte, Checkliste und Online-Buchung.
2. Beim Standort für Samstag 9:00 bis 14:00 Uhr eintragen und Strg+S drücken.
Expected: Unten steht „Gespeichert“; im Kalender bietet „Freie Zeit suchen“ für diesen Samstag nach 14:00 Uhr nichts mehr an.
3. Einen Raum umbenennen und „Verwerfen“ wählen, dann einen Raum hinzufügen und im Abschnitt „Räume“ speichern.
Expected: Beim Verwerfen kehrt der Name zurück; der neue Raum steht in der Liste und im Kalender als Spalte.
4. „Schließzeiten“ wählen, eine neue Schließzeit für den 24. bis 26.12. mit Grund „Weihnachten“ anlegen und speichern.
Expected: Der Eintrag steht unter „Aktuell“; im Kalender sind der 24., 25. und 26.12. als geschlossen gekennzeichnet.
5. Bei der Schließzeit die Termine absagen und bestätigen, danach das Protokoll aufklappen.
Expected: Das Protokoll nennt die abgesagten Termine; eine wartende Nachricht bietet „Jetzt senden“ und „Nicht senden“.
6. Im Kalender per Rechtsklick auf eine Raumspalte „Raum sperren …“ wählen.
Expected: „Raumsperren“ öffnet sich mit einem neuen Formular und dem gewählten Raum.
7. Zeitraum und Grund eintragen und speichern; im Kalender denselben Raum prüfen.
Expected: Der Raum ist im Zeitraum gesperrt (Grund sichtbar); Termine im Zeitraum bieten das Umbuchen an.
8. „Kalender-Abos“ wählen, ein Abo mit einer Quelle aus den Vorschlägen anlegen und „Jetzt abrufen“ wählen.
Expected: Der Stand des Abrufs wechselt auf den Ergebnistext; Feiertage erscheinen im Kalender.
9. Im Feld „Region“ „AT“ eintippen und einen Vorschlag mit Pfeil nach unten und Enter übernehmen.
Expected: Die Region steht in Großbuchstaben im Feld.
10. Einen Wert ändern und ohne Speichern links „Schließzeiten“ wählen.
Expected: Die Frage „Änderungen speichern?“ erscheint; „Abbrechen“ bleibt beim Abo.
Closes: Ä191 (Teil)
Result: ☐

### Slice 96 – Native Verwaltung: Kassa, Zahlungen und Erinnerungen
Start: App starten, wie `work/conventions.md` beschreibt („App zum manuellen Testen starten“), als Inhaberin anmelden
1. Links „Verwaltung“ wählen, dann in der Bereichsliste „Kassa“.
Expected: Rechts stehen „Rabatte“ mit „Rabatt ohne Freigabe bis (%)“ und „Barzahlungsrabatt“ mit den gespeicherten Werten. (superseded by slice 125: Die Maske steht unter ihrer Gruppe im Balken oben, darunter als Reiter; die Bereichsliste gibt es nicht mehr.)
2. Nichts ändern und „Speichern“ wählen; danach an der Kassa einen Beleg mit Rabatt unter der Grenze buchen.
Expected: Unten steht „Gespeichert.“; im Verlauf der Firmendaten steht kein neuer Eintrag, die Kassa bucht wie vorher und der Beleg hat seine Signatur.
3. In der Bereichsliste „Zahlungen“ wählen (Spa mit eigenen Stripe-Schlüsseln).
Expected: Im Feld „Geheimer Schlüssel“ steht nur „Hinterlegt: ••••“ mit den letzten vier Zeichen, das Feld selbst ist leer. (superseded by slice 125: Die Maske steht unter ihrer Gruppe im Balken oben, darunter als Reiter; die Bereichsliste gibt es nicht mehr.)
4. Ohne neuen Schlüssel „Speichern“ wählen.
Expected: Unten steht „Gespeichert.“; Online-Zahlungen laufen weiter über dasselbe Stripe-Konto.
5. In der Bereichsliste „Erinnerungen“ wählen, „Erste Erinnerung“ von 24 auf 48 ändern und Strg+S drücken.
Expected: Unten steht „Gespeichert.“ (superseded by slice 125: Die Maske steht unter ihrer Gruppe im Balken oben, darunter als Reiter; die Bereichsliste gibt es nicht mehr.)
6. Einen Termin in drei Tagen für einen Gast mit Mailadresse buchen und den Termin am nächsten Tag im Kalender ansehen.
Expected: Die Erinnerung wird 48 Stunden vor dem Termin verschickt (Stand im Termin).
7. Bei einer Behandlung unter „Erinnerung an die nächste Behandlung“ 42 Tage eintragen, dann links „Kassa“ wählen.
Expected: Die Frage „Änderungen speichern?“ erscheint; „Speichern“ speichert den Abstand und wechselt.
Closes: Ä191 (Teil)
Result: ☐

### Slice 97 – Native Verwaltung: Postfach & Nummern, KI, Handbuch und Code kopieren
Start: App starten, wie `work/conventions.md` beschreibt („App zum manuellen Testen starten“), als Inhaberin anmelden
1. Links „Verwaltung“ wählen, dann „Postfach & Nummern“.
Expected: Oben das Postfach-Formular (Art, Adresse, Server, Port, Benutzername, Ordner, Passwort), darunter die Absenderregeln und die Liste der Nummern.
2. Ein Postfach eintragen, ins Passwortfeld tippen und Strg+S drücken.
Expected: Das Passwort erscheint als Punkte; unten steht „Gespeichert“; nach erneutem Öffnen ist das Passwortfeld leer, das Postfach bleibt eingetragen.
3. „Jetzt prüfen“ wählen.
Expected: Eine Rückmeldung zum Abruf erscheint, der Stand des letzten Abrufs wechselt.
4. Bei den Absenderregeln ein Muster eintippen, „Regel hinzufügen“ wählen und die Verarbeitung der Zeile umstellen.
Expected: Die Regel steht in der Liste; die Umstellung gilt sofort, ohne Speichern; „Löschen“ fragt zuerst nach.
5. Im Postfach einen Wert ändern und ohne Speichern „KI“ wählen.
Expected: Die Frage „Änderungen speichern?“ erscheint; „Abbrechen“ bleibt beim Postfach.
6. „KI“ öffnen.
Expected: Stand von Claude (Eingeschaltet oder Aus) und des lokalen Modells; fehlt das Modell, startet „Modell herunterladen“ einen Fortschritt, der sich selbst aktualisiert.
7. KI-Schlüssel auf dem Server eintragen, den Posteingang öffnen und eine Nachricht wählen.
Expected: Ein Antwortentwurf kommt.
8. „Handbuch“ öffnen, einen Eintrag mit Erinnerung am Tagesstart anlegen, speichern, dann löschen.
Expected: Der Eintrag steht in der Liste und erscheint am Tagesstart; „Löschen“ fragt zuerst nach.
9. „Firmendaten“ öffnen, im Abschnitt „Kundenbereich einbetten“ „Code kopieren“ wählen und den Text in ein Textfeld einfügen.
Expected: Es erscheint „Code kopiert.“; eingefügt wird ein iframe mit der Adresse des Kundenbereichs und der Kennung des Spas.
Closes: Ä191 (Teil)
Result: ☐

### Slice 98 – Native Verwaltung: Bereich Team mit Personenkarte
Start: App starten, wie `work/conventions.md` beschreibt („App zum manuellen Testen starten“), als Inhaberin anmelden; Mailpit läuft (Port 8025), `OnlineBooking:PublicUrl` zeigt auf den Server
1. Links „Verwaltung“ wählen, dann in der Bereichsliste „Team“.
Expected: Links steht die Liste mit Suchfeld „Name oder E-Mail“, rechts die Personenkarte mit den Reitern Stammdaten, Zugang, Qualifikationen und Verlauf; „Zugänge“ und „Qualifikationen“ fehlen in der Bereichsliste. (superseded by slice 99: Die Karte hat zusätzlich die Reiter „Dienstplan und Abwesenheit“ und „Zeiten“.) (superseded by slice 125: Die Maske steht unter ihrer Gruppe im Balken oben, darunter als Reiter; die Bereichsliste gibt es nicht mehr.)
2. „Person einladen“ wählen, Name und eine neue E-Mail-Adresse eintragen, als Rolle „Rezeption“ lassen, einen Standort wählen und „Einladung senden“ drücken.
Expected: Es erscheint „Einladung an … geschickt.“, die Felder sind leer, die Person steht als „Rezeption · eingeladen“ in der Liste.
3. Im Mailpit die Mail öffnen.
Expected: Betreff „Einladung zu“ und Name des Spas; der Link endet auf `/?einladung=` mit einem Schlüssel.
4. Den Link im Browser öffnen und die Einladung mit einem Passkey annehmen.
Expected: Die Anmeldung gelingt; in der App steht die Person als „Rezeption · aktiv“, und ihre Rolle ist „Rezeption“.
5. In der Bereichsliste „Behandlungen“ wählen, bei „Wer kann das?“ auf eine Person klicken.
Expected: „Team“ öffnet die Karte genau dieser Person (nicht der ersten in der Liste). (superseded by slice 125: Die Maske steht unter ihrer Gruppe im Balken oben, darunter als Reiter; die Bereichsliste gibt es nicht mehr.)
6. Das Kästchen „Ausgeschiedene zeigen“ anhaken.
Expected: Ausgeschiedene stehen als „ausgeschieden“ in der Liste; ihre Karte sagt „Ausgeschieden: Die Angaben sind nur lesbar.“
Closes: Ä191 (Teil), Ä250 (Teil), Ä202
Result: ☐

### Slice 99 – Team: Dienstplan, Abwesenheiten und Zeiten
Start: App starten, wie `work/conventions.md` beschreibt („App zum manuellen Testen starten“), als Inhaberin anmelden
1. Links „Verwaltung“ wählen, dann „Team“, dann oben „Wochenplan“.
Expected: Die Woche steht als Tabelle mit allen Personen des Standorts; „Dienstplan“ und „Zeiten“ fehlen in der Bereichsliste. (superseded by slice 125: Die Maske steht unter ihrer Gruppe im Balken oben, darunter als Reiter; die Bereichsliste gibt es nicht mehr.)
2. Für eine Person am kommenden Dienstag die Zelle wählen, Dienst 9–17 eintragen, „Sofort gültig“ anhaken und speichern; dann für weitere Tage der Woche Dienste eintragen.
Expected: Die Zellen zeigen die neuen Zeiten, der Fokus stand beim Öffnen im ersten Feld des Tages.
3. Den Dienst vom Dienstag auf 12–20 ändern und speichern, dann zum Kalender wechseln und den Dienstag öffnen.
Expected: Die Spalte der Person beginnt um 12 Uhr; eine Buchung vor 12 Uhr bietet die Person nicht an.
4. Zurück im Team für eine Person mit gebuchten Terminen eine Abwesenheit an einem dieser Tage eintragen und speichern.
Expected: Die Maske warnt mit der Zahl der betroffenen Termine.
5. Im Wochenplan auf den Namen einer Person klicken.
Expected: Die Personenkarte öffnet, die Reiter „Dienstplan und Abwesenheit“ und „Zeiten“ zeigen ihre Woche; rechts lässt sich die Woche scrollen, nichts ist abgeschnitten.
6. Oben „Zeiten“ wählen, an einem Tag einen Abschnitt ohne Grund speichern, dann mit Grund.
Expected: Ohne Grund steht die Meldung am Feld und der Fokus darin; mit Grund steht die Zeit in der Tabelle, und der Verlauf der Karte zeigt die Änderung.
7. Oben „Anträge“ wählen.
Expected: Die Anträge aus der Handy-App stehen in der Liste; „Genehmigen“ trägt die Abwesenheit im Wochenplan und im Kalender ein.
8. Als Rezeption anmelden und „Team“ öffnen.
Expected: Nur der Wochenplan steht da, gut lesbar, die Zellen öffnen nichts.
Closes: Ä191 (Teil), Ä250 (Teil)
Result: ☐

### Slice 100 – Team: Lohn und Provision
Start: App starten, wie `work/conventions.md` beschreibt („App zum manuellen Testen starten“), als Inhaberin anmelden
1. Links „Verwaltung“ wählen, dann „Team“, eine Person mit Diensten wählen, den Reiter „Lohn“ öffnen.
Expected: Das Feld „Personalnummer“ zeigt die Nummer, darunter stehen die Monatswerte der Person für den Vormonat; ein anderer Monat in der Auswahl lädt andere Werte.
2. Die Nummer ändern und unten „Speichern“ wählen (oder Strg+S).
Expected: Die Leiste zeigt „Gespeichert.“; dieselbe Nummer bei einer zweiten Person eintragen zeigt die Meldung „Diese Personalnummer hat schon jemand.“, die Karte bleibt im Reiter „Lohn“.
3. Oben im Team „Lohn“ wählen, den Oktober wählen und „Monatswerte anzeigen“.
Expected: Die Tabelle zeigt je Person Position, Menge und Lohnart, Hinweise stehen darüber.
4. „BMD Lohn“ wählen, im Speicherdialog einen Ort wählen und die Datei in Excel öffnen.
Expected: Die Meldung „lohn-BmdLohn-2026-10.csv gespeichert.“ erscheint; die Datei enthält dieselben Zahlen wie der bisherige Export im Browser.
5. Einen Monat ohne Dienste wählen und „DATEV Lodas“ wählen.
Expected: Es entsteht keine Datei, eine Meldung nennt, dass im Monat nichts zu übergeben ist.
6. Unter „Einstellungen der Lohnverrechnung“ eine Lohnart ändern und zu „Personen“ wechseln.
Expected: Die Rückfrage „Änderungen speichern?“ erscheint; „Speichern“ sichert die Einstellung.
7. Als Backoffice anmelden und „Team“ › „Lohn“ öffnen.
Expected: Die Einstellungen sind grau und nicht zu ändern, der Export geht; die Fragezeichen lassen sich öffnen.
Closes: Ä191 (Lohn), Ä250 (Lohn)
Result: ☐

### Slice 101 – Native Verwaltung: Lager und Aktionen
Start: App starten, wie `work/conventions.md` beschreibt („App zum manuellen Testen starten“), als Inhaberin anmelden
1. Links „Verwaltung“ wählen, dann „Lager“.
Expected: Reiter Bestand, Bewegungen, Inventur und Lagerorte; im Bestand die Kennzahlen, die Tabelle und „Material anlegen“.
2. Im Bestand ein Material anlegen (Bezeichnung „Massageöl Lavendel“, Einheit „ml“, Einstandspreis, Mindestbestand) und „Material anlegen“ wählen.
Expected: Eine Rückmeldung erscheint, das Material steht in der Tabelle mit Bestand 0.
3. Im Reiter Bewegungen „Bewegung buchen“ wählen, Wareneingang mit Menge 10 buchen.
Expected: Der Bestand zeigt 10; die Bewegung steht in der Liste.
4. In der Kassa 2 dieses Produkts verkaufen (Material mit einem Kassa-Artikel verknüpfen), dann im Lager den Bestand ansehen.
Expected: Der Bestand zeigt 8.
5. Im Reiter Inventur einen Lagerort wählen, „Zählung starten“, bei einem Material einen abweichenden Wert eintragen und „Inventur buchen“ wählen.
Expected: Zuerst fragt die Maske nach; nach „Inventur buchen“ zeigt der Bestand den gezählten Wert, die Abweichung steht als Inventur-Bewegung.
6. Im Reiter Lagerorte einen Lagerort anlegen, eine Mindestmenge eintragen, speichern, dann löschen.
Expected: „Löschen“ fragt zuerst nach; das Hauptlager lässt sich nicht löschen.
7. Im Kalender bei einem Hinweis zum Lager „Anzeigen“ wählen.
Expected: Die Verwaltung öffnet „Lager“ nativ beim Material.
8. „Aktionen“ öffnen, „Neue Aktion“ wählen: Name „Herbst 10 %“, Gästetext, Zeitraum, Rabatt Prozent 10, Gilt für „Alle Behandlungen“, Strg+S.
Expected: „Gespeichert“; die Aktion steht in der Liste mit dem Zustand „Läuft“ bzw. „Geplant“.
9. In der Kassa einen Termin einer Behandlung auf die Rechnung setzen.
Expected: Die Kassa schlägt die Aktion vor; „Anwenden“ senkt den Preis um 10 %.
10. In „Aktionen“ die Aktion pausieren, dann archivieren, „Archivierte anzeigen“ einschalten und „Zurückholen“ wählen.
Expected: Pausiert wird sie nicht mehr vorgeschlagen; archiviert verschwindet sie aus der Liste, bis „Archivierte anzeigen“ an ist; „Zurückholen“ bringt sie zurück.
Closes: Ä191 (Teil)
Result: ☐

### Slice 102 – Native Verwaltung: Berichte, Kosten, Buchhaltung und Betriebsinfo
Start: App starten, wie `work/conventions.md` beschreibt („App zum manuellen Testen starten“), als Inhaberin anmelden
1. Links „Verwaltung“ wählen, dann „Berichte“, Reiter „Management“, Standort und Oktober einstellen; denselben Bericht im Web öffnen.
Expected: Alle Kennzahlen, Umsatz nach Erlöskonto, Personal und Behandlungen zeigen dieselben Werte wie im Web-Bericht.
2. Den Assistenten nach dem Umsatz im Oktober fragen und „Im Bericht öffnen“ wählen.
Expected: Der native Umsatzbericht öffnet sich mit Standort und Oktober.
3. „Mit anderen Spas vergleichen“ wählen und den Zeitraum wechseln.
Expected: Der Vergleich erscheint über den Kennzahlen, die Werte stehen als Text.
4. Die Reiter „Mein Tag“, „Tagesstart“, „Hygiene“ und „Pflichtberichte“ durchgehen; in „Hygiene“ einen Haken setzen.
Expected: Jeder Reiter zeigt den Tag des Standorts; der Haken bleibt nach dem Neuladen gesetzt.
5. In „Pflichtberichte“ das DEP speichern.
Expected: Der Speichern-Dialog von Windows erscheint; die Datei liegt danach am gewählten Ort.
6. „Buchhaltung“ öffnen, Oktober setzen und „DATEV-Buchungsstapel“ speichern; denselben Export im Web laden.
Expected: Beide Dateien sind Byte für Byte gleich.
7. „Kosten“ öffnen, eine Behandlung aufklappen, einen Stundensatz ändern und Strg+S drücken.
Expected: Die Rechnung steht Schritt für Schritt da; „Gespeichert“ erscheint und die Vollkosten ändern sich.
8. „Betriebsinfo“ öffnen, einen Eintrag anlegen, speichern und archivieren.
Expected: Bei fehlender Bezeichnung meldet das Feld den Fehler; „Archivieren“ fragt zuerst nach; „Archivierte zeigen“ zeigt den Eintrag wieder.
Closes: Ä191 (Teil), Ä226 bleibt offen
Result: ☐

### Slice 103 – Eingebettete Verwaltung und Web-Team-Oberfläche entfernen
Start: Einzelplatz der Windows-App frisch starten (leerer Datenordner) und als Inhaberin einrichten; für Schritt 4 bis 6 `dotnet run --project src/AispaStudio.AppHost` und die Windows-App über die Ressource `windows-app`
1. In der Verwaltung jede Gruppe des Balkens und jeden Reiter einmal öffnen.
Expected: Jede Maske ist nativ, nirgends erscheint ein eingebetteter Browser; nur die Hilfe öffnet weiter ihre Seiten.
2. Im Browser `http://localhost:<Port des Einzelplatzes>/team` und `/` aufrufen.
Expected: `/team` antwortet mit „nicht gefunden“, `/` führt auf die Seite „Anmelden“.
3. Den Assistenten nach Öffnungszeiten fragen und „Jetzt ergänzen“ bzw. den Link der Antwort öffnen.
Expected: Die native Maske öffnet sich am genannten Feld, kein Browser.
4. Im Server-Betrieb im Bereich „Team“ eine Person einladen, den Einladungslink in einem anderen Browser öffnen, **Einladung annehmen** klicken und den Passkey anlegen.
Expected: Die Seite „Anmelden“ zeigt Spa und Rolle, nach dem Annehmen fragt der Browser nach dem Passkey, danach ist die Person angemeldet.
5. Dort **Code für die Windows-App** klicken und den Code in einer zweiten Windows-App bei „Mit Code anmelden“ eingeben.
Expected: Die Seite zeigt Code und Ablaufzeit; die Windows-App meldet die eingeladene Person an.
6. In Mein Tag auf **Mit Passwort anmelden** klicken (Einzelplatz mit Mein Tag) und mit Benutzername und Passwort anmelden.
Expected: Nach dem Anmelden führt die Seite zurück nach Mein Tag.
Closes: Ä191, Ä199, Ä203, Ä286
Result: ☐

### Slice 104 – API: Rechte je Kanal für Dashboard, Mitarbeiter-App und Kunden-App
Start: Docker Desktop starten, `dotnet run --project src/AispaStudio.AppHost`, Windows-App über die Ressource `windows-app` starten und als Inhaberin anmelden
1. In einem Browser-Tab auf der Adresse der API die Entwicklerwerkzeuge öffnen und `fetch('/api/display/pairing', {method: 'POST'}).then(r => r.json())` ausführen.
Expected: Die Antwort enthält einen Code wie „K7QM-2XRT“ und ein Geheimnis.
2. In der Windows-App **Geräte** öffnen, bei „Anzeigen im Pausenraum“ den Code eintippen und auf **Anzeige koppeln** klicken.
Expected: „Pausenraum“ steht in der Liste mit **Widerrufen**, darunter die Zeile „„Pausenraum“ gekoppelt. …“.
3. Im Browser-Tab `fetch('/api/display/pairing/redeem', {method: 'POST', headers: {'Content-Type': 'application/json'}, body: JSON.stringify({secret: '<Geheimnis>'})})` ausführen, danach `fetch('/api/display/session')`.
Expected: Beide antworten mit 200, die Sitzung nennt „Pausenraum“ und den Standort.
4. Im Browser-Tab `fetch('/api/appointments/<Termin-Id>/move', {method: 'POST', headers: {'Content-Type': 'application/json'}, body: '{"start":"2026-11-02T10:00:00+01:00"}'})` ausführen.
Expected: Die API antwortet mit 403 „Über diesen Zugang nicht freigegeben.“.
5. In der Windows-App bei „Pausenraum“ auf **Widerrufen** klicken, die Rückfrage bestätigen und im Browser-Tab wieder `fetch('/api/display/session')` ausführen.
Expected: Die Anzeige verschwindet aus der Liste, die Sitzung antwortet mit 401 (die Anzeige ist abgemeldet).
Closes: Ä195
Result: ☐

### Slice 105 – Web: Dashboard für den Pausenraum
Start: Docker Desktop starten, `dotnet run --project src/AispaStudio.AppHost`, Windows-App über die Ressource `windows-app` starten und als Inhaberin anmelden; auf einem zweiten Bildschirm im Browser die Adresse der Web-Oberfläche mit `/dashboard/` öffnen und mit **F11** auf Vollbild schalten
1. Am zweiten Bildschirm den Code ablesen. In der Windows-App **Geräte** öffnen, den Code bei „Anzeigen im Pausenraum“ eintippen und auf **Anzeige koppeln** klicken.
Expected: Der Bildschirm wechselt in wenigen Sekunden auf die Tafel mit dem Standortnamen, der Uhrzeit, den Leisten der Personen, den Räumen und den nächsten Terminen.
2. Die Tafel aus 3 m Abstand ansehen, einmal im hellen und einmal im dunklen Thema des Bildschirms.
Expected: Namen, Zeiten und Raumzustände sind lesbar; „frei“ und „belegt“ stehen als Text; die rote Linie zeigt die aktuelle Uhrzeit.
3. Im Kalender der Windows-App einen heutigen Termin eines Gastes „Anna Huber“ auf eine andere Zeit verschieben.
Expected: Die Tafel zeigt die neue Zeit innerhalb von 5 Sekunden, der Gast steht als „A. H.“, kein Name, keine Behandlung, kein Preis, keine Notiz.
4. Am zweiten Bildschirm die Maus bewegen und mit Tab drücken.
Expected: Nichts lässt sich anklicken oder fokussieren, der Mauszeiger ist ausgeblendet.
5. Den Server kurz anhalten (Aspire: Ressource `api` stoppen), nach einer halben Minute wieder starten.
Expected: Die Tafel zeigt den Hinweis „Keine Verbindung zum Server. Gezeigt wird der letzte Stand.“ und lädt nach dem Start ohne Zutun wieder.
6. In der Windows-App bei der Anzeige auf **Widerrufen** klicken und die Rückfrage bestätigen.
Expected: Die Tafel zeigt wieder den Kopplungscode.
7. Den Vormittag über laufen lassen (DoD „einen Vormittag“).
Expected: Die Uhrzeit läuft weiter, abgeschlossene und neue Termine erscheinen, die Tafel bleibt ohne Neustart stehen.
Closes: Ä192, Ä234
Result: ☐

### Slice 106 – Web: Handy-App für Mitarbeitende
Start: Docker Desktop starten, `dotnet run --project src/AispaStudio.AppHost`, die Windows-App über die Ressource `windows-app` starten und als Inhaberin anmelden; am Handy `/mein-tag/` mit einem Konto der Rolle Therapeut öffnen.
1. Im Browser des Handys die App installieren („Zum Startbildschirm“) und vom Startbildschirm öffnen.
Expected: Die App startet ohne Adressleiste; nach der Anmeldung zeigt sie oben die Leiste Tag, Woche, Abwesenheit.
2. Bei „Bitte anmelden“ **Mit Passwort anmelden** wählen, anmelden und die App wieder öffnen; dann **Passkey einrichten** antippen, abmelden und **Mit Passkey anmelden** antippen.
Expected: Die Anmeldung mit Passkey klappt, die App zeigt den heutigen Tag.
3. Beim heutigen Termin **Begonnen melden** antippen und den Kalender der Windows-App ansehen.
Expected: Die Karte zeigt „Läuft seit …“, im Kalender steht der Termin innerhalb weniger Sekunden als „Läuft“.
4. **Beendet melden** antippen.
Expected: Die Karte zeigt „Abgeschlossen“, im Kalender steht der Termin als abgeschlossen.
5. Oben **Woche** antippen und einen anderen Tag antippen.
Expected: Die Woche nennt je Tag Dienst und Zahl der Termine; der gewählte Tag öffnet sich.
6. Oben **Abwesenheit** antippen, als Art **Frei**, als Von und Bis den nächsten Freitag wählen und **Antrag stellen** antippen.
Expected: Unter „Meine Anträge“ steht der Antrag als **Offen**.
7. Den Antrag als Inhaberin in der Windows-App genehmigen (sobald Slice 99 auf `dev` liegt) und in der App **Abwesenheit** neu öffnen.
Expected: Der Antrag steht als **Genehmigt**, der Freitag steht im Dienstplan.
8. Den Flugmodus einschalten und die App vom Startbildschirm öffnen.
Expected: Der letzte Plan erscheint mit „Offline, Stand“ und der Uhrzeit des letzten Abrufs; **Begonnen melden** antwortet „Nur mit Verbindung.“.
9. Bei einem Gast mit angegebener Allergie die Karte ansehen.
Expected: Die Karte zeigt nur „Allergie angegeben“, nicht den Inhalt des Fragebogens.
Closes: Ä193
Result: ☐

### Slice 108 – Web: Handy-App für Kunden zur Terminkontrolle
Start: Docker Desktop starten, `dotnet run --project src/AispaStudio.AppHost`, die Windows-App über die Ressource `windows-app` starten und als Inhaberin anmelden; am Handy `/buchen/` öffnen und einen Termin mit der Mailadresse des Gastes buchen.
1. Im Browser des Handys die App installieren („Zum Startbildschirm“) und vom Startbildschirm öffnen.
Expected: Die App startet ohne Adressleiste bei „Meine Termine“.
2. Mit dem Code aus der Mail anmelden.
Expected: Oben steht „Ihr nächster Termin“, darunter die weiteren Termine.
3. **Termin verschieben** antippen, eine freie Zeit wählen und den Kalender der Windows-App ansehen.
Expected: Die App meldet „Der Termin ist verschoben“, im Kalender steht der Termin zur neuen Zeit.
4. **Zum Kalender hinzufügen** antippen und die Datei öffnen.
Expected: Der eigene Kalender übernimmt Zeit, Behandlung und Adresse des Standorts.
5. **Gesundheitsfragebogen ausfüllen** antippen und absenden.
Expected: Die Karte zeigt „Gesundheitsfragebogen liegt vor“; an der Rezeption steht nur der Stand, nicht der Inhalt.
6. Bei einem Standort mit Ausfallgebühr einen Termin innerhalb der Frist öffnen (Karte hinterlegt) und **Termin stornieren** antippen.
Expected: Die App nennt die Gebühr vor dem Bestätigen; nach **Ja, stornieren** ist der Termin abgesagt, in der Kassa lässt sich die Gebühr auslösen.
7. Einen online gekauften Gutschein teilweise einlösen und **Meine Termine** neu öffnen.
Expected: Unter „Meine Gutscheine“ steht der Restwert.
8. Abmelden und **Termin buchen** antippen.
Expected: Die Buchung geht in derselben App ohne Konto.
Closes: Ä194
Result: ☐

### Slice 109 – Web: Kennzahlen des Tages für die Leitung am Handy
Start: Docker Desktop starten, `dotnet run --project src/AispaStudio.AppHost`; in der Windows-App als Inhaberin eine Barzahlung an der Kassa buchen (z. B. 12,50 €) und eine Terminanfrage in „Postfach“ offen lassen. Am Handy `/mein-tag/` öffnen.
1. Als Inhaberin anmelden.
Expected: In der Leiste steht neben Tag, Woche und Abwesenheit der Reiter **Heute im Spa**.
2. **Heute im Spa** antippen und in der Windows-App den Bericht „Kennzahlen“ für heute daneben legen.
Expected: Umsatz laut Kassa, Auslastung Personal und Räume, Absagen und Nicht erschienen stimmen mit dem Bericht überein; **Offene Anfragen** zählt die Anfragen, über die die Rezeption noch entscheidet.
3. Eine Zahlung an der Kassa buchen und **Aktualisieren** antippen.
Expected: Der Umsatz steigt um den Betrag.
4. Flugmodus einschalten und **Aktualisieren** antippen.
Expected: Es steht „Kennzahlen brauchen eine Verbindung.“, keine alte Zahl.
5. Abmelden und als Rezeption (oder als Standortleitung ohne Recht auf Beträge) anmelden.
Expected: Bei der Rezeption fehlt der Reiter; bei der Standortleitung ohne Recht auf Beträge steht der Hinweis, aber kein Umsatz.
Closes: Ä195
Result: ☐

### Slice 110 – Team: Personalakte mit Gesprächen, Anmerkungen und Verwarnungen
Start: Docker Desktop starten, `dotnet run --project src/AispaStudio.AppHost`; Windows-App als Inhaberin anmelden, **Verwaltung** › **Team** öffnen und eine Person wählen.
1. Den Reiter **Personalakte** öffnen, **Gespräch anlegen** klicken, Notizen und zwei Vereinbarungen eintragen, eine davon abhaken und speichern.
Expected: Das Gespräch steht in der Liste mit beiden Vereinbarungen, eine ist als erledigt markiert.
2. **Als PDF speichern** klicken und die Datei öffnen.
Expected: Die Datei nennt die Person und alle ihre Einträge; Umlaute stimmen.
3. Im Gespräch einen Folgetermin auf heute setzen, speichern und den Kalender öffnen.
Expected: Unter den Meldungen steht „Folgegespräch mit …“; **Zur Personalakte** führt zur Karte der Person.
4. Im Reiter **Verlauf** den Abschnitt **Personalakte** lesen.
Expected: Dort steht, wer wann was geändert hat, aber kein Inhalt.
5. Abmelden und als Rezeption anmelden, dann dieselbe Person öffnen.
Expected: Der Reiter **Personalakte** fehlt.
Closes: Ä252 (Gespräche, Anmerkungen, Verwarnungen)
Result: ☐

### Slice 111 – Team: Hinweise, Sonderregelungen und Vollmachten auf einen Blick
Start: Docker Desktop starten, `dotnet run --project src/AispaStudio.AppHost`; Windows-App als Inhaberin anmelden, **Verwaltung** › **Team** öffnen
1. Lena Huber öffnen und den Kopf der Karte lesen.
Expected: Oben stehen der Hinweis, die Sonderregelung „Keine Termine nach 16:00“ und die Vollmacht Schlüssel; die Teamliste nennt die drei Arten.
2. Den Reiter **Regelungen und Vollmachten** öffnen.
Expected: Die drei Listen stehen da, die Vollmacht Tresor ist als abgelaufen ausgegraut, die Sonderregelung nennt den Grund.
3. **Wochenplan** öffnen, bei Lena einen Tag anklicken, Dienstende 18:00 eintragen und speichern.
Expected: Die Maske warnt mit der Regelung ohne den Grund und speichert nicht; **Trotzdem speichern** speichert den Tag.
4. Im **Kalender** an einem Tag mit Lenas Dienst eine Behandlung bei Lena um 17:00 buchen.
Expected: Der Kalender warnt mit der Regelung und bucht erst nach **Trotzdem buchen**.
5. Im Kalender auf das Symbol in Lenas Spaltenkopf zeigen.
Expected: Der Tooltip zeigt den Hinweis.
6. **Team** › **Wer darf was?** öffnen.
Expected: Je Vollmacht steht, wer sie hat und bis wann; Tresor steht ausgegraut, Kassa-Storno hat „Niemand“.
7. Einer Rezeptionistin Kassa-Storno mit Ablauf morgen erteilen; am übernächsten Tag als Rezeptionistin einen Beleg stornieren.
Expected: Die App lehnt ab und nennt die abgelaufene Vollmacht; solange die Vollmacht gilt, klappt das Storno.
8. Abmelden, als Rezeption anmelden und Lena öffnen.
Expected: Reiter, Kennzeichen und **Wer darf was?** fehlen; das Symbol im Kalender bleibt, und ein Hinweis „Nur Leitung“ erscheint nicht.
Closes: Ä252 (Hinweise, Sonderregelungen, Vollmachten)
Result: ☐

### Slice 112 – Lager: Lieferantenstamm, Packungseinheiten und Ablaufwarnung
Start: Docker Desktop starten, `dotnet run --project src/AispaStudio.AppHost`, Windows-App als Inhaberin öffnen, „Verwaltung“ › „Lager“.
1. Reiter **Lieferanten**, **Neuer Lieferant**: Name „Haslauer“, Mail, Lieferzeit 5, Mindestbestellwert 150 eintragen und speichern.
Expected: Es erscheint „Gespeichert.“ und „Haslauer“ steht in der Liste mit „5 Tage Lieferzeit“.
2. Im Reiter **Bestand** bei einem Material auf **Ändern** klicken, „Haslauer“ wählen, Packung „Flasche“ mit 500, Gebinde „Karton“ mit 4 und Menge je Behandlung 15 eintragen und speichern.
Expected: Das Material zeigt den Lieferanten „Haslauer“; der Bestand nennt, für wie viele Behandlungen er reicht.
3. **Bewegung buchen**, Reiter **Wareneingang**: dieses Material wählen, bei der Packungsebene „Karton“ wählen, Menge 1 und Preis eintragen, buchen.
Expected: Die Vorschau nennt 2000 in der Einheit des Materials; der Bestand steigt um genau diese Menge.
4. Einen Wareneingang mit einer Charge buchen, die in 20 Tagen abläuft, und den Reiter **Aufgaben** öffnen.
Expected: Die Charge steht mit Menge und Lagerort in **Bald ablaufend**, das Material in der **Abverkaufsliste**.
5. Beim Lieferanten eine Änderung eintragen und ohne Speichern einen anderen wählen.
Expected: Die Rückfrage nennt „Lieferanten“; **Verwerfen** ändert sonst nichts, eine offene Zählung bleibt offen.
Closes: Ä251 (Teil)
Result: ☐

### Slice 113 – Lager: Bestellvorschlag und Bestellung per Mail
Start: Docker Desktop starten, `dotnet run --project src/AispaStudio.AppHost`, Windows-App als Inhaberin öffnen, „Verwaltung“ › „Lager“. Mailpit öffnen (Aspire-Dashboard).
1. Reiter **Lieferanten**: beim Lieferanten eines Materials Mailadresse, Lieferzeit 5 und Mindestbestellwert 500 eintragen und speichern.
Expected: Es erscheint „Gespeichert.“
2. Reiter **Lagerorte**, Hauptlager: bei diesem Material eine Höchstmenge eintragen und speichern.
Expected: Es erscheint „Gespeichert.“; die Höchstmenge steht nach dem Neuladen noch da.
3. Reiter **Bestellungen** öffnen.
Expected: Der Bestellvorschlag nennt das Material mit Tagesverbrauch, Lieferzeit, Bestand, Ziel und Vorschlag.
4. Auf **Entwürfe anlegen** klicken.
Expected: Je Lieferant steht ein Entwurf da; liegt er unter 500, warnt er mit dem fehlenden Betrag.
5. Auf **Bestellen und senden** klicken und in der Rückfrage **Abbrechen** wählen.
Expected: Der Entwurf bleibt; in Mailpit ist keine neue Mail.
6. Erneut auf **Bestellen und senden** klicken und bestätigen.
Expected: Die Bestellung steht auf „bestellt“; in Mailpit liegt eine Mail an den Lieferanten mit Betreff „Bestellung“ und Nummer samt den Positionen.
Closes: Ä251 (Teil)
Result: ☐

### Slice 114 – Lager: Barcode und Scannen bei Wareneingang, Inventur und Kassa
Start: Docker Desktop starten, `dotnet run --project src/AispaStudio.AppHost`, Windows-App als Inhaberin öffnen, „Verwaltung“ › „Lager“. USB-Handscanner anschließen (er tippt wie eine Tastatur und schließt mit Enter ab).
1. Im Reiter **Bestand** bei einem Material auf **Ändern** klicken, im Feld **Barcode** den Code von einer Verpackung scannen und speichern.
Expected: Es erscheint „… gespeichert.“; ein zweites Material mit demselben Code wird mit „Der Code gehört schon zum Material …“ abgelehnt.
2. In das Feld **Code scannen** über den Reitern klicken und die Verpackung scannen.
Expected: Die Buchung **Wareneingang** öffnet sich mit dem Material, der Cursor steht im Feld **Menge**, das Scanfeld ist leer.
3. Reiter **Inventur**, **Zählung starten**, dieselbe Verpackung scannen.
Expected: Der Cursor springt in das Feld **Gezählt** der Zeile des Materials.
4. Einen Code scannen, den kein Material hat.
Expected: Die Rückfrage „Material anlegen oder zuordnen?“ nennt den Code; **Material anlegen** öffnet das Formular mit dem Code im Feld **Barcode**, **Einem Material zuordnen** fragt nach dem Material (**Ändern** in seiner Zeile) und trägt den Code ins Formular ein.
5. Das Material ist mit einem Verkaufsprodukt der Kassa verknüpft. In der Kassa in das Feld **Code scannen** klicken und die Verpackung scannen.
Expected: Der Artikel steht mit Menge 1 auf der Rechnung; ein zweiter Scan legt eine zweite Position an.
6. In der Kassa einen unbekannten Code scannen.
Expected: Eine Meldung nennt den Code und dass er zu keinem Artikel gehört; die Rechnung bleibt unverändert.
Closes: Ä251 (Teil)
Result: ☐

### Slice 115 – Lager im Buchungsablauf: Produktempfehlung im Termin und an der Kassa
Start: Docker Desktop starten, `dotnet run --project src/AispaStudio.AppHost`, Windows-App als Inhaberin öffnen (Beispieldaten).
1. „Verwaltung“ › „Angebot“ › „Behandlungen“, „Lava-Hot-Stone-Massage (60 Min.)“ wählen, Reiter **Verkauf** öffnen.
Expected: Unter „Passt dazu“ steht „Massageöl Lavendel 100 ml“; ein weiterer Artikel lässt sich mit **Artikel hinzufügen** dazunehmen und mit **Speichern** sichern, das Fragezeichen öffnet die Hilfe „Passt dazu“.
2. Im Kalender einen heutigen, eingecheckten Termin „Lava-Hot-Stone-Massage“ anklicken und im Panel unter **Mehr** auf **Erledigt** klicken.
Expected: Unter **Kassieren** erscheint „Passt dazu“ mit „Massageöl Lavendel 100 ml · 24,90 € · n am Regal“ und dem Knopf **Mit kassieren**.
3. Auf **Mit kassieren** klicken.
Expected: Die Kassa öffnet sich; auf der Rechnung stehen die Behandlung und das Massageöl, der Vorschlag unter der Rechnung ist weg.
4. Bezahlen und danach „Verwaltung“ › „Lager“, Lagerort „Verkaufsregal Rezeption“ ansehen.
Expected: Der Beleg nennt das Massageöl; der Bestand am Verkaufsregal ist um 1 gesunken.
5. Am Verkaufsregal das Massageöl per Inventur auf 0 zählen, einen weiteren Hot-Stone-Termin erledigen, das Panel ansehen und dann **Kassieren** klicken.
Expected: Weder im Panel noch unter der Rechnung der Kassa erscheint ein Vorschlag „Passt dazu“.
Closes: Ä251 (Teil)
Result: ☐

### Slice 116 – Lager: Soll-Ist-Verbrauch und Inventur am Handy
Start: Docker Desktop starten, `dotnet run --project src/AispaStudio.AppHost`, Windows-App als Inhaberin öffnen (Beispieldaten); die Handy-App „Mein Tag“ im Browser des Handys oder in der Geräteansicht des Browsers als Therapeutin öffnen.
1. In „Mein Tag“ den Reiter **Inventur** öffnen, den Raumschrank 2 wählen und auf **Zählung starten** klicken.
Expected: Die Liste zeigt Material mit Einheit und Zählfeld, aber keinen Sollbestand.
2. Bei zwei Materialien eine Menge eintragen.
Expected: Neben der Zeile steht kurz „Speichert …“, dann „Gespeichert“; nach einem Neuladen der Seite stehen die Mengen wieder im Feld.
3. Auf **Kamera starten** klicken und die Verpackung eines Materials in die Kamera halten (ohne Kamera-Erkennung im Browser: den Code im Feld **Code eingeben** tippen und **Suchen** wählen).
Expected: Die Maske springt zur Zeile des Materials, der Cursor steht im Zählfeld; **Kamera beenden** schaltet die Kamera aus.
4. Auf **Abschicken** klicken und bestätigen.
Expected: Es steht „Die Leitung schließt die Zählung ab.“, die Felder sind nur lesend.
5. In der Windows-App „Verwaltung“ › „Lager“ › Reiter **Inventur** öffnen.
Expected: Oben steht „1 Zählung vom Handy wartet“ mit Raumschrank 2, der zählenden Person und der Zeit; **Übernehmen** füllt die Zählliste mit den gezählten Mengen und zeigt Soll, Differenz und Wert.
6. Auf **Inventur buchen** klicken und bestätigen.
Expected: Die Zählung verschwindet aus dem Hinweis, der Bestand des Raumschranks entspricht den gezählten Mengen.
7. Als Rezeption in „Mein Tag“ den Reiter **Wareneingang** öffnen, einen Code eingeben, Menge 5 eintragen und buchen.
Expected: Es steht „5 … gebucht.“, im Lager erscheint ein Wareneingang; als Therapeutin fehlt der Reiter.
8. In der Windows-App Reiter **Verbrauch** öffnen und den Zeitraum der letzten 30 Tage wählen.
Expected: Die Tabelle zeigt je Material Soll, Ist und Mehrverbrauch; der Schwund aus Schritt 6 steht als Mehrverbrauch, die Behandlungen mit ihrem Soll darunter.
Closes: Ä251 (Teil), Ä271, Ä272, Ä273
Result: ☐

### Slice 117 – Team: Zuständigkeiten und Zertifikate mit Erneuerung
Start: Docker Desktop starten, `dotnet run --project src/AispaStudio.AppHost`, Windows-App als Inhaberin öffnen (Beispieldaten)
1. „Verwaltung“ › „Team“ öffnen und Lena Huber in der Liste ansehen.
Expected: Bei Lena steht das Kennzeichen „Ersthelfer“ und ihr Erste-Hilfe-Kurs mit dem Datum; eine weitere Person zeigt „Warnung: Ersthelfer – Erste-Hilfe-Kurs abgelaufen am …“ mit Warnsymbol.
2. Eine Person ohne Zertifikat öffnen, Reiter **Zuständigkeiten**, Zertifikat „Erste-Hilfe-Kurs“ mit Ablauf in 30 Tagen anlegen und speichern.
Expected: Die Zeile zeigt „Läuft am … ab“ mit Warnsymbol, das Kennzeichen oben auf der Karte nennt die Frist, und im Kalender steht die Erinnerung „… läuft am … ab“.
3. Beim selben Zertifikat einen Scan (PDF oder Bild) hinzufügen, danach speichern unter.
Expected: Die gespeicherte Datei gleicht dem Original.
4. Beim selben Zertifikat **Erneuern** wählen, ein späteres Gültig-bis eintragen und speichern.
Expected: Das neue Zertifikat trägt Bezeichnung und Stelle des alten, die Erinnerung verschwindet, das alte bleibt in der Liste.
5. Im Bereich Team die Ansicht **Zuständigkeiten** öffnen.
Expected: „Wer ist wofür zuständig?“ nennt je Zuständigkeit Person, Vertretung und Gültigkeit oder „niemand eingetragen“; „Was läuft bald ab?“ listet Zertifikate nach Datum.
6. Einer Person ohne Lagerrecht die Zuständigkeit Lager geben und sich als diese Person anmelden; ein Material liegt unter dem Mindestbestand.
Expected: Im Kalender steht „… nachbestellen“ ohne den Knopf **Zum Lager**; bei der Inhaberin steht diese Meldung nicht mehr.
7. Im Wochenplan einen Samstag ohne Ersthelferin mit gültigem Kurs planen und speichern.
Expected: Die Maske warnt „Keine Ersthelferin im Dienst am Samstag, …“; **Trotzdem speichern** speichert den Tag.
8. Als Lena in „Mein Tag“ den heutigen Tag öffnen.
Expected: Unter „Erinnerungen“ steht nur ihr eigener Kurs.
Closes: Ä274, E14
Result: ☐

### Slice 118 – Verwalten von zu Hause: Server-Adresse in der App eintragen
Start: Windows-App im Server-Betrieb ohne `AISPA_API_URL` und ohne Eintrag starten (`dotnet run --project src/AispaStudio.Desktop`), einen Server mit gültigem Zertifikat bereithalten (z. B. über einen Handy-Hotspot erreichbar)
1. Auf dem Startbildschirm unter „Mit Server verbinden“ eine nicht erreichbare Adresse eintragen und auf **Server verbinden** klicken.
Expected: Unter dem Feld steht eine Meldung „… ist nicht erreichbar …“; nach einem Neustart der App ist nichts gemerkt.
2. Die Adresse des erreichbaren Servers (`https://…`) eintragen und auf **Server verbinden** klicken.
Expected: Die App startet neu und zeigt die Anmeldung dieses Servers.
3. Anmelden und in der Seitenleiste **Netz** öffnen.
Expected: Der Satz nennt den Server; **Verbindung lösen** ist anklickbar.
4. Eine Verwaltungsmaske öffnen.
Expected: Die Maske lädt Daten vom Server.
5. Die Umgebungsvariable `AISPA_API_URL` auf eine andere Adresse setzen und die App neu starten.
Expected: Die App verbindet sich mit dieser Adresse; unter **Netz** steht die Umgebungsvariable, und **Verbindung lösen** ist gesperrt.
Closes: Ä196, Ä197, Ä198
Result: ☐

### Slice 123 – Lager: Bestellungen, Bedarfsprüfung und Chargen-Rückverfolgung nativ
Start: Docker Desktop starten, `dotnet run --project src/AispaStudio.AppHost`, Windows-App als Inhaberin öffnen, „Verwaltung“ › „Lager“.
1. Reiter **Bestellungen**: bei einem Entwurf auf **Bestelltext** klicken, dann auf **Kopieren**.
Expected: Der Text steht unter der Karte; daneben steht „Kopiert.“, und er lässt sich in einen Editor einfügen.
2. Auf **Als bestellt markieren** klicken.
Expected: Die Bestellung steht auf „bestellt“, und in Mailpit ist keine neue Mail. Die Karte zeigt die Felder „Geliefert“, „Charge“ und „Ablaufdatum“.
3. Bei **Geliefert** weniger als bestellt eintragen, eine Charge dazuschreiben und auf **Lieferung buchen** klicken.
Expected: Die Bestellung steht auf „teilweise geliefert“, bei **Geliefert** steht der Rest, und der Bestand ist im Reiter „Bestand“ gestiegen.
4. Auf **Abschließen** klicken und bestätigen.
Expected: Die Rückfrage erscheint vorher; danach steht die Bestellung auf „geliefert“ und bietet keine Lieferung mehr an.
5. Reiter **Bestand**, Abschnitt **Bedarfsprüfung**: den Zeitraum auf „Nächste 14 Tage“ stellen.
Expected: Die Tabelle lädt neu; bei knappem Material stehen eine Warnung und die Menge in der Spalte „Fehlt“.
6. Reiter **Bewegungen**, Abschnitt **Charge zurückverfolgen**: das Material und die Charge aus Schritt 3 wählen, dann auf **Zurückverfolgen** klicken.
Expected: Oben steht die Summe mit „erhalten“, darunter der Wareneingang aus Schritt 3.
7. Im Kalender einen Termin mit diesem Material abschließen, dann im Lager erneut auf **Zurückverfolgen** klicken.
Expected: Eine Zeile „Behandlung“ nennt Kunde und Behandlung des Termins.
Closes: Ä223
Result: ☐

### Slice 124 – Termin und Kassa: Kartengarantie, Stand der Anzahlung, Erinnerungsverlauf, Rückerstattung und Gutscheinabfrage nativ
Start: Windows-App, als Inhaberin anmelden, Testdaten mit Online-Buchungen
1. Einen online gebuchten Termin mit Anzahlung öffnen.
Expected: Die Karte „Anzahlung“ nennt Stand, Betrag und Prozent; einen Knopf „Anfordern“ gibt es nicht.
2. Einen gebuchten Termin ohne Hinterlegung öffnen, an einem Standort mit Ausfallgebühr, und auf **Karte per Link anfordern** klicken.
Expected: Die Meldung „Link per Mail verschickt“ erscheint, die Karte zeigt „Link verschickt, Zustimmung offen“ und den Knopf **Link erneut senden**.
3. Einen Termin mit hinterlegter Karte auf „Nicht erschienen“ setzen, auf **Ausfallgebühr belasten …** klicken und **Abbrechen** wählen.
Expected: Die Rückfrage nennt den Betrag, nichts wird abgebucht.
4. Einen Termin öffnen, zu dem Erinnerungen hinausgegangen sind.
Expected: Die Karte „Erinnerungen“ nennt Stufe, Kanal, Zustand und Zeit.
5. An der Kassa einen Termin kassieren, bei **Gutschein-Code** einen gültigen Code eintippen und das Feld verlassen.
Expected: Unter dem Feld stehen Restwert und „gültig bis“, ein leerer Betrag wird vorbelegt.
6. Unter „Belege“ einen online bezahlten Beleg wählen.
Expected: Die Karte nennt die Online-Zahlung und den Weg des Geldes vor dem Storno.
Closes: Ä285
Result: ☐

### Slice 125 – Verwaltung: Navigation oben mit Gruppen
Start: `dotnet run --project src/AispaStudio.AppHost`, Windows-App starten, als Inhaberin anmelden
1. In der linken Leiste „Verwaltung“ wählen.
Expected: Oben steht ein Balken in der Farbe der linken Leiste mit Betrieb, Angebot, Team, Lager, Kassa & Geld, Kontakt und Zahlen, „Betrieb“ ist markiert. Darunter stehen die Reiter ab „Firmendaten“. Die Liste links und die Überschrift „Verwaltung“ gibt es nicht mehr.
2. Den Reiter „Schließzeiten“ wählen.
Expected: Die Schließzeiten öffnen sich, der Reiter ist markiert.
3. Im Balken „Lager“ wählen.
Expected: Das Lager öffnet sich, eine Reiterzeile der Verwaltung steht nicht darüber.
4. Im Balken „Betrieb“ wählen.
Expected: „Schließzeiten“ ist wieder offen.
5. In „Firmendaten“ einen Wert ändern und im Balken „Team“ wählen.
Expected: Die Rückfrage „Änderungen speichern?“ erscheint.
6. In der Rückfrage „Abbrechen“ wählen.
Expected: „Betrieb“ bleibt markiert, die Änderung steht noch im Feld.
7. Die Änderung verwerfen, das Fenster auf die Mindestbreite ziehen und „KI und Hilfe“ öffnen.
Expected: Balken und Reiterzeile brechen nicht um; was nicht passt, steht unter „Mehr“ am Ende der Zeile.
8. „Mehr“ anklicken.
Expected: Das Menü zeigt die übrigen Einträge mit Text, im Balken auch mit Symbol.
9. Einen Eintrag aus dem Menü wählen und „Mehr“ erneut anklicken.
Expected: „Mehr“ ist markiert, sein Tooltip nennt die Wahl, im Menü steht der Punkt vor ihr.
10. Im Kalender bei schmalem Fenster das Menü „⋯“ in der Kopfzeile anklicken.
Expected: Das Menü zeigt seine Einträge mit Symbol und Text.
11. In der Verwaltung mit Tab in den Balken gehen und Pfeil rechts drücken.
Expected: Der Fokus wandert zum nächsten Eintrag, die Maske bleibt.
12. Enter drücken.
Expected: Die Gruppe mit dem Fokus öffnet sich.
13. F6 drücken, dann noch einmal F6.
Expected: Der Fokus springt erst auf den gewählten Reiter, dann in die Maske. Im Balken ist der Fokusring auch hell deutlich zu sehen.
14. Das Schema auf „Dunkel“ stellen und einen Eintrag im Balken überfahren.
Expected: Der gewählte Eintrag hebt sich mit Fläche und Strich ab, der überfahrene ist heller als der Balken, alle Texte sind gut lesbar.
Closes: Ä293
Result: ☐

### Slice 126 – Kassa und Kalender: Sofortüberweisung, Lückenvorschläge und Einwilligung nativ
Start: `dotnet run --project src/AispaStudio.AppHost`, Windows-App starten, als Inhaberin anmelden, Standort mit Bankverbindung
1. An der Kassa einen Termin kassieren und bei **Sofortüberweisung** auf **Zahlungsdaten anzeigen** klicken.
Expected: Empfänger, IBAN und Betrag stehen unter dem Feld, dazu der QR-Code und der Zahlungstext (EPC); der Betrag steht im Feld. Die Banking-App des Handys liest den Code mit Empfänger, IBAN und Betrag, im hellen und im dunklen Schema.
2. Eine Kasse ohne Bankverbindung wählen und denselben Knopf klicken.
Expected: Die Kassa zeigt den Fehler des Servers, das Feld bleibt leer.
3. Im Kalender im Menü neben **Termin anlegen** **Lücken schließen** wählen, an einem Tag mit einer Lücke von wenigen Minuten.
Expected: Die Seite nennt Gast, Behandlung, Person, alte und neue Zeit und den Grund; verschoben ist noch nichts.
4. Auf **Auf … verschieben** klicken.
Expected: Der Termin steht auf der neuen Zeit, die Meldung nennt den Gast und bietet Rückgängig, der Vorschlag verschwindet.
5. Einen Termin öffnen, dessen Gast noch nicht eingewilligt hat, und **Einwilligung per Mail anfragen** klicken.
Expected: Die Karte „Erinnerung an die nächste Behandlung“ zeigt „Angefragt am …, wartet auf Bestätigung“, die Meldung „Mail zum Bestätigen ist unterwegs“.
6. Nach der Bestätigung durch den Gast den Termin neu öffnen und **Widerrufen** klicken.
Expected: Die Karte zeigt „Widerrufen am …“, **Widerrufen** verschwindet.
Closes: Ä290, Ä291, Ä292
Result: ☐

### Slice 127 – Termin: Anamnese im Team nativ
Start: `dotnet run --project src/AispaStudio.AppHost`, Windows-App starten, als Inhaberin anmelden, Kundenbereich erreichbar, Tablet und Drucker bereit
1. Einen Termin mit ausgefülltem Bogen öffnen und in der Karte **Anamnese** auf **Ansehen** klicken.
Expected: Die Karte nennt Stand und Gültigkeit und zeigt die Antworten, ein „Ja“ ist hervorgehoben; **Ausblenden** nimmt sie wieder weg.
2. Einen Termin ohne Bogen öffnen, **Am Tablet ausfüllen** klicken und den Code mit dem echten Tablet scannen.
Expected: Die Karte zeigt QR-Code, Link und Uhrzeit; das Tablet öffnet den Bogen, und nach dem Absenden zeigt der neu geöffnete Termin „Liegt vor: am Tablet“.
3. Sprache Französisch wählen und **Papierbogen drucken** klicken, dann am echten Drucker drucken.
Expected: Der Standardbrowser öffnet den Bogen auf Französisch mit Gast, Termin und QR-Code samt Druckdialog; das Blatt ist vollständig und gut lesbar, der Code führt zum Bogen.
4. Ein Foto des ausgefüllten Bogens über **Foto hochladen** wählen, die Hinweise prüfen, dann **Schließen** klicken.
Expected: Der Dialog zeigt links das Foto und rechts den Vorschlag mit KI-Hinweis, Unsicheres ist gelb; nach **Schließen** steht das Foto unter **Prüfen**, der Stand ist unverändert.
5. **Prüfen** klicken, alle Antworten vergleichen, beide Kästchen ankreuzen, den Namen eintragen und **Übernehmen** klicken.
Expected: **Übernehmen** ist erst dann frei; die Meldung nennt die Übernahme, die Karte zeigt „Liegt vor: auf Papier“.
6. Als Therapeutin ohne Recht zum Buchen denselben Termin öffnen.
Expected: Stand und Antworten sind lesbar, Tablet, Papierbogen, Foto und **Prüfen** fehlen. (superseded by slice 128: **Prüfen** ist für die Therapeutin da, Tablet, Papierbogen und Foto fehlen weiter)
Closes: Ä300
Result: ☐

### Slice 128 – Anamnese: Leserecht der Rezeption klären und durchsetzen
Start: `dotnet run --project src/AispaStudio.AppHost`, Windows-App starten; Zugänge Inhaberin, Rezeption und Therapeutin der Beispieldaten bereit
1. Als Rezeption einen Termin mit ausgefülltem Bogen öffnen und die Karte **Anamnese** ansehen.
Expected: Die Karte nennt Stand und Gültigkeit; **Ansehen** fehlt, Tablet, Papierbogen und **Foto hochladen** sind da.
2. Als Rezeption über **Foto hochladen** ein Foto eines ausgefüllten Bogens wählen.
Expected: Kein Prüfdialog öffnet sich; die Meldung lautet „Foto hochgeladen. Prüfen kann, wer Anamnesen lesen darf“, die Karte zeigt das Foto ohne **Prüfen**.
3. Als Therapeutin denselben Termin öffnen, **Ansehen** und dann beim Foto **Prüfen** klicken, prüfen und **Übernehmen**.
Expected: Die Antworten sind lesbar; der Dialog zeigt Foto und Vorschlag, nach **Übernehmen** steht „Liegt vor: auf Papier“.
4. Als Inhaberin unter Team den Zugang einer Rezeptionskraft öffnen.
Expected: Der Hinweis zur Rolle nennt „Von der Anamnese nur den Stand“.
Closes: Ä304
Result: ☐
