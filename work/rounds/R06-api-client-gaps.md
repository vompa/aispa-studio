Status: closed

# Runde R06 – Lücken der Arbeitsanweisung „Avalonia-Client gegen Azure-API“, Kassa, Eingabefelder und Assistent

Geöffnet am 02.11.2026 nach dem Abschluss von R05. Slice 121 ändert alle Routen; Slices, die neue Endpunkte anlegen (133 bis 135), ziehen ihre Pfade beim Einmischen von `dev` nach.

## Quelle
- Arbeitsanweisung „aispa – Avalonia-Client gegen Azure-API“ vom 30.10.2026, in der Sitzung eingefügt.
- Prüfung gegen den Code am selben Tag: Die Windows-App hat keinen Datenbankzugriff, `AispaStudio.Contracts` und SignalR-Live-Updates bestehen, der Kalender blockiert nicht.
- Die Wahl des Auftraggebers am 30.10.2026: „Nur echte Lücken“.
- Die Punkte des Auftraggebers vom 02.11.2026 in der Sitzung nach dem Abschluss von R05, gesammelt mit „ich sage wieder für runde 6 ein paar sachen , beende mit fertig“ und mit „fertig , mach bitte Runde06 ready“ freigegeben (Ä310–Ä319); dazu der Fragenkatalog der Rezeption in docs/konzepte/assistent-rezeption-fragen.md, erstellt auf Auftrag des Auftraggebers „versetze dich in die rolle einer rezeptionistin …“.

## Eingang
| Kennung | Bereich | Befund | Größe | Quelle |
|---|---|---|---|---|
| Ä281 | API, Windows-App, Termine | Der Termin hat eine xmin-Sperre, der Client schickt aber keine Version: Ein veralteter Stand überschreibt fremde Änderungen still → Version im Vertrag, Konfliktmeldung – erledigt in Slice 119 | M | Prüfung, 30.10.2026 |
| Ä282 | Windows-App, gemeinsame Bibliothek | `ApiClient` liegt in der Windows-App → in eine Bibliothek ohne Oberfläche, ViewModels an einer Schnittstelle – erledigt in Slice 120 | M | Arbeitsanweisung, Aufgabe 1 |
| Ä283 | API | Keine Versionierung der Pfade → `/api/v1`, alte Pfade bleiben, ADR 0085 – erledigt in Slice 121 | M | Arbeitsanweisung, Aufgabe 2 |
| Ä284 | Doku, Tests | Vorgehen je Maske fehlt, Regeln sind nicht abgesichert → Abschnitt in der Architektur, Architekturtests – erledigt in Slice 122 | S | Arbeitsanweisung, Leitplanken |
| Ä310 | Windows-App, Kassa | Artikel und Gutscheine stehen mit „Heute abzurechnen“ und geparkten Rechnungen in einer rollenden Spalte → eigene, einklappbare Spalte links, die bisherige wird schmäler – erledigt in Slice 129 | M | Auftraggeber, 02.11.2026 |
| Ä311 | Windows-App, alle Masken | Ein Betrag gilt erst nach dem Verlassen des Feldes (z. B. Wertgutschein) → gültige Werte gelten schon beim Tippen – erledigt in Slice 130 | M | Auftraggeber, 02.11.2026 |
| Ä312 | Windows-App, Kassa, Bezahlen | Das Betragsfeld einer Zahlart ist leer → beim Klick den offenen Betrag vorgeben, überschreibbar – erledigt in Slice 131 | S | Auftraggeber, 02.11.2026 |
| Ä313 | Beispieldaten, Belege | „Testdaten für Belege fehlen noch“; der Verlauf erzeugt rund 2.200 Kassabelege, welche Belege fehlen, ist offen (Offene Entscheidungen) | ? | Auftraggeber, 02.11.2026 |
| Ä314 | Windows-App, Kassa, Ansicht „Bezahlt“ | Ohne eingerichteten Bondrucker ist der Druckknopf unsichtbar → Drucken auch hier, ohne Drucker gesperrt mit Hinweis – erledigt in Slice 132 | S | Auftraggeber, 02.11.2026 |
| Ä315 | API, Assistent | „wann hat herr steiner seinen termin“ ergibt „Welcher Gast ist gemeint?“ → unscharfe Namen, Auswahl bei mehreren, Bezug auf den offenen Gast – erledigt in Slice 133 | M | Auftraggeber, 02.11.2026 |
| Ä316 | Windows-App, Assistent | Antworten sind Fließtext plus zweite Knopfliste, Wörter brechen mitten im Wort um; nach dem Fragen ist manchmal nichts zu sehen und die Frage weg → Kopfsatz und eine Liste, Frage bleibt, Ladezustand, Fehler mit „Erneut fragen“ – erledigt in Slice 134 | M | Auftraggeber, 02.11.2026 |
| Ä317 | API, Assistent | „Historie Christian Steiner“ zeigt nur ein Hilfethema → Verlauf des Gastes und weitere Fragen zu Gästen, Terminen und Absagen – erledigt in Slice 135 | L | Auftraggeber, 02.11.2026 |
| Ä318 | API, Assistent | Fragen zu Pausen, Ersthelfern, Räumen und Lager beantwortet der Assistent nicht → Antwortgeber dafür – erledigt in Slice 136 | M | Auftraggeber, 02.11.2026 |
| Ä319 | API, Windows-App, Assistent | Aufträge wie „buche“, „verschiebe“, „sag ab“ öffnen keine Maske → vorausgefüllte Maske, gespeichert erst per Knopf (zurückgestellt) | L | Claude, 02.11.2026 (Vorschlag im Fragenkatalog) |

## Slices
| Slice | Name | Befunde | Hängt ab von | Modell | Neue Kennungen |
|---|---|---|---|---|---|
| 119 | Termin-Konflikt: Version mitschicken statt still überschreiben | Ä281 | – | opus | – |
| 120 | API-Client in die gemeinsame Bibliothek | Ä282 | – | opus | – |
| 121 | API-Version in den Pfaden | Ä283 | 120 | opus | – |
| 122 | Muster für die Masken festhalten und absichern | Ä284 | 119, 120, 121 | sonnet | – |
| 129 | Kassa: eigene Spalte für Artikel und Gutscheine, einklappbar | Ä310 | – | sonnet | Ä320–Ä322 |
| 130 | Betrags- und Zahlenfelder übernehmen den Wert schon beim Tippen | Ä311 | – | opus | Ä323–Ä325 |
| 131 | Kassa: Betrag der Zahlart mit dem offenen Rest vorbelegen | Ä312 | 129, 130 | sonnet | Ä326–Ä328 |
| 132 | Kassa: Drucken direkt in der Ansicht „Bezahlt“ | Ä314 | 131 | sonnet | Ä329–Ä331 |
| 133 | Assistent: unscharfe Namen und Bezug auf den offenen Gast | Ä315 | – | opus | Ä332–Ä334 |
| 134 | Assistent: Antworten als kurze Liste, Frage bleibt stehen | Ä316 | 133 | opus | Ä335–Ä337 |
| 135 | Assistent: Verlauf des Gastes, Termine und Absagen | Ä317 | 134 | opus | Ä338–Ä340 |
| 136 | Assistent: Personal, Räume und Lager | Ä318 | 135 | sonnet | Ä341–Ä343 |

Reihenfolge: drei Ketten, je höchstens zwei Slices zugleich. Client: 119 und 120, dann 121, zuletzt 122. Kassa: 129 und 130, dann 131, dann 132. Assistent: 133, 134, 135, 136. Prüfstufe 3 (Standard); Meilensteine sind das Ende der Kassa-Kette (nach 132) und der Rundenabschluss.

## Entscheidungen
Ruling: Nur die echten Lücken der Arbeitsanweisung werden gebaut – User, 30.10.2026
Ruling: Anmeldung bleibt bei Identity und Passkeys nach ADR 0007, kein Entra ID – User, 30.10.2026
Ruling: Kein Web-Kalender und kein Blazor; der Kalender bleibt nativ nach ADR 0084 – User, 30.10.2026
Ruling: Der Offline-Betrieb nach ADR 0005 bleibt; die Datenbank bleibt PostgreSQL – User, 30.10.2026
Ruling: Der Azure-Rollout ist ein eigener Schritt und braucht eine eigene Freigabe, weil er kostet (ADR 0016) – User, 30.10.2026
Ruling: Artikel und Gutscheine bekommen an der Kassa eine eigene, einklappbare Spalte links; die bisherige linke Spalte wird schmäler (Ä310) – User, 02.11.2026
Ruling: Ein eingegebener Wert gilt für die Maske, ohne dass man das Feld verlässt (Ä311); das weicht von Fluent ab und wird in der Fluent-Ableitung festgehalten – User, 02.11.2026
Ruling: Ein Klick auf den Betrag einer Zahlart gibt die Rechnungssumme vor, überschreibbar (Ä312) – User, 02.11.2026
Ruling: Drucken geht auch aus der Ansicht „Bezahlt“ (Ä314) – User, 02.11.2026
Ruling: Der Assistent soll unscharfe Fragen verstehen, Antworten besser darstellen und die Frage nicht ohne Antwort verlieren (Ä315, Ä316, Ä317) – User, 02.11.2026
Build decision (slice 119): Auto-Lauf; der Plan work/plans/2026-11-02-slice-119.md gilt mit seinen Annahmen 1 bis 7, Spalte „Plan“ damit angehakt – Claude, 02.11.2026
Build decision (slice 119): Die 409 bei veraltetem Stand ist die bisherige Konfliktantwort `BookingRejected` mit neuem Feld `Current` (aktueller Termin), kein eigener Antworttyp; ohne `ExpectedVersion` ändert der Server wie bisher – Claude, 02.11.2026
Build decision (slice 119): Die Version schicken nur Verschieben, Zeiten, Notiz, Storno, Status (auch gemeinsames Einchecken je Teil) und Zurücknehmen aus Kalender und Terminfenster; „Kommt später“, Lücken-Vorschläge, Gruppen- und Serienwege und serverinterne Aufrufer bleiben ohne – Claude, 02.11.2026
Build decision (slice 119): „Rückgängig“ schickt die Version nach der eigenen Änderung; eigene Vormerkungen am selben Termin werden beim Abgleich verkettet, nur eine fremde Änderung ergibt „nicht übernommen“ – Claude, 02.11.2026
Build decision (slice 119): Ein Live-Update baut das Terminfenster nicht neu, solange dort eine Karte bearbeitet wird (Zeiten, Notiz, Serie, auf anderen Tag); das Speichern meldet dann den Konflikt oben im neu aufgebauten Terminfenster – Claude, 02.11.2026
Build decision (slice 119): Kein ADR; das Muster „Version im Vertrag statt ETag“ hält Slice 122 in docs/architektur.md fest – Claude, 02.11.2026
Build decision (slice 119): Spalte „Tests red“ angehakt mit dem Rot-Lauf auf 36f739bc (Prüfnachweis); die Schutztests „Verschieben ohne Version“ (API) und „Veraltete Vormerkung … nicht übernommen“ (Windows-App) waren schon vorher grün, weil sie bestehendes Verhalten absichern – Claude, 02.11.2026
Build decision (slice 119): Der Ziehtest läuft über `BeginDrag`/`DragTo`/`DropAsync` des Kalenders wie die übrigen Konflikttests, weil sich die Mausverdrahtung nicht ändert; der Notiztest im Terminfenster klickt echt – Claude, 02.11.2026
Build decision (slice 119): Die volle Desktop-Suite läuft als Desktop-Tests ohne `ViewTests` plus `scripts/view-tests.ps1` (Testgruppen in work/conventions.md) – Claude, 02.11.2026
Build decision (slice 119): Version 0 gilt als unbekannt und wird nicht geprüft; offline gebuchte Termine und alte Zwischenstände tragen 0 und bekämen sonst immer 409 (aus der Durchsicht) – Claude, 02.11.2026
Build decision (slice 119): Ein „Rückgängig“ von altem Stand meldet „Nicht zurückgenommen“ ohne „Erneut versuchen“, weil die Wiederholung mit derselben Version immer scheitert; der Kalender zeigt den aktuellen Termin (aus der Durchsicht) – Claude, 02.11.2026
Build decision (slice 119): Endet eine Bearbeitung im Terminfenster, ohne zu speichern, und hat sich der Termin inzwischen geändert, baut das Terminfenster mit dem neuen Stand neu auf (aus der Durchsicht) – Claude, 02.11.2026
Build decision (slice 119): `StaleOf` vergleicht die Version direkt nach dem Laden statt sie als Originalwert der Sperre zu setzen; zwischen Laden und Speichern greift weiter die xmin-Sperre, das Ergebnis ist gleich – Claude, 02.11.2026
Build decision (slice 120): Auto-Lauf; der Plan work/plans/2026-11-02-slice-120.md gilt mit seinen Annahmen 1 bis 7, Spalte „Plan“ damit angehakt – Claude, 02.11.2026
Build decision (slice 120): Die Bibliothek heißt `AispaStudio.Client` statt `AispaStudio.ApiClient`, weil ein Namespace dieses Namens die Klasse `ApiClient` in allen Dateien unter `AispaStudio.*` verdeckt hätte – Claude, 02.11.2026
Build decision (slice 120): `ApiClient` erbt von `HttpApiClient` und behält Name und Konstruktor; der Betrieb ohne Verbindung hängt an den Haken `OnJsonReceived`, `OnSessionEnded` und dem überschriebenen `GetAsync` – Claude, 02.11.2026
Build decision (slice 120): Alle ViewModels und die Dienste `LiveUpdates`, `ServerWeb`, `TerminalAgent` nehmen `IWorkstationApi`; die Statusabfragen von „Mit Server verbinden“ und „Mit Haupt-PC im Netz verbinden“ bauen einen eigenen `HttpApiClient`, der Designer-Konstruktor des `MainViewModel` weiter einen `ApiClient` – Claude, 02.11.2026
Build decision (slice 120): Spalte „Tests red“ angehakt mit dem Rot-Lauf auf ab810154 (Prüfnachweis): die zwei Abnahmetests rot, die drei Schutztests des HTTP-Teils grün, weil sie bestehendes Verhalten sichern – Claude, 02.11.2026
Build decision (slice 120): Der ViewModel-Test übergeht nur die Zustandsautomaten von async-Methoden; deren Felder sind lokale Variablen (der Handler der Statusabfrage), keine Abhängigkeit des ViewModels. Typargumente (`Func<ApiClient>`) prüft er mit (aus der Durchsicht) – Claude, 02.11.2026
Build decision (slice 120): Kein neues Testprojekt und kein ADR: es gab keine reinen HTTP-Tests zum Umziehen, und die Aufteilung lässt sich ohne Folgen zurücknehmen – Claude, 02.11.2026
Build decision (slice 121): Auto-Lauf; der Plan work/plans/2026-11-02-slice-121.md gilt mit seinen Annahmen 1 bis 7, Spalte „Plan“ damit angehakt – Claude, 02.11.2026
Build decision (slice 121): Das Umschreiben läuft als `IStartupFilter` vor der ganzen Pipeline statt als Middleware mit ausdrücklichem `UseRouting()` in `Program.cs`; die Reihenfolge hängt so an keiner Zeile in `Program.cs` – Claude, 02.11.2026
Build decision (slice 121): Spalte „Tests red“ angehakt mit den Rot-Läufen auf 8fb0f504 (Windows-App) und 32c68f19 (API) im Prüfnachweis; `Mandantensperre_gilt_unter_v1`, die Tests zu Kundenbereich, Webhooks und Betreiber sowie die Fälle ohne Version im Client waren vorher grün, weil sie bestehendes Verhalten sichern – Claude, 02.11.2026
Build decision (slice 121): Zwei Erwartungen des neuen API-Tests nach dem Rot-Lauf berichtigt (die traceId unterscheidet sich je Anfrage, Webhooks stehen nicht im OpenAPI-Dokument); geprüft werden jetzt Titel und Text der Ablehnung bzw. ein Betreiber-Pfad – Claude, 02.11.2026
Build decision (slice 121): Der Export-Pfad der Pflichtberichte heißt jetzt `/api/v1/…`; `ReportTests` erwartet ihn so – Claude, 02.11.2026
Build decision (slice 121): Die Hilfe `server` nennt die Statusabfrage mit v1; die Adressen für Webhooks und OAuth-Rückrufe bleiben ohne Version – Claude, 02.11.2026
Build decision (slice 122): Auto-Lauf; der Plan work/plans/2026-11-03-slice-122.md gilt mit seinen Annahmen 1 bis 8, Spalte „Plan“ damit angehakt – Claude, 03.11.2026
Build decision (slice 122): Die Regeln stehen als reine Funktionen über Text (`ArchitectureScan`), damit die Abnahmefälle („ViewModel mit `.Wait()`“, „Npgsql-Verweis“) an künstlichem Eingabetext rot werden, ohne den Quelltext zu verletzen – Claude, 03.11.2026
Build decision (slice 122): `.Result` ist in `ViewModels/` nur auf Tasks erlaubt, die `Task.WhenAll` abgewartet hat; Typnamen und `imported.Result` in `SubscriptionsViewModel` (Eigenschaft eines Ergebnisses, kein Task) sind die einzigen Ausnahmen. Die 18 `WhenAll`-Stellen bleiben unverändert, kein Produktivcode ändert sich – Claude, 03.11.2026
Build decision (slice 122): Spalte „Tests red“ angehakt mit dem Rot-Lauf auf 148f798f (Prüfnachweis): elf Verstoß-Fälle rot gegen das Gerüst ohne Funde, die Echtbaum-Tests und Gegenproben grün, weil sie bestehendes Verhalten sichern – Claude, 03.11.2026
Build decision (slice 122): Kein ADR; die Regeln folgen aus ADR 0084, 0085 und der Arbeitsanweisung – Claude, 03.11.2026
Build decision (slice 122): Aus der Durchsicht: `.Result` zählt nur nach einem früheren `await Task.WhenAll(…)` mit einfachen Namen, Typnamen nur vor einem Bezeichner, `?.`/`!.`/`[…]` werden mitgeprüft; fünf Negativfälle und der mehrzeilige Fall sind Tests; der Doku-Wortlaut zu `LoadError` (neue Masken) und zum Live-Update (`IsEditingCard`) ist angepasst – Claude, 03.11.2026
Build decision (slice 129): Auto-Lauf; der Plan work/plans/2026-11-03-slice-129.md gilt mit seinen Annahmen 1 bis 9, Spalte „Plan“ damit angehakt, Abweichung bei den Breiten siehe nächste Zeile – Claude, 03.11.2026
Build decision (slice 129): Spaltenbreiten 232 (eingeklappt 44) / 192 / Rest / 340 statt 280 / 260: die Fläche der Kassa ist bei 1280 px Fensterbreite wegen der Navigation nur 1160 px breit; mit den geplanten Werten wäre die Rechnung 232 px breit geblieben, jetzt 316 px (Annahme 1 des Plans rechnete mit der Fensterbreite) – Claude, 03.11.2026
Build decision (slice 129): Spalte „Tests red“ angehakt mit dem Rot-Lauf auf 46eecf28 (Prüfnachweis): zwei von drei ViewModel-Tests und alle sechs Bildtests rot gegen die Hüllen ohne Verhalten; der dritte Fall (aufgeklappt ohne gespeicherten Zustand) sichert das bisherige Verhalten und war von Anfang an grün – Claude, 03.11.2026
Build decision (slice 129): Der Test `kassa-verkaufsknoepfe` prüft die Feldbreite jetzt gegen die Breite der Spalte (Spaltenbreite minus 44 px) statt gegen 240 px; die Absicht bleibt, nur die Spalte ist schmäler geworden – Claude, 03.11.2026
Build decision (slice 129): Kein ADR; Maße und Verhalten stehen in docs/design.md Abschnitt 7, die Entscheidung für die Spalte ist das Ruling des Auftraggebers (Ä310) – Claude, 03.11.2026
Build decision (slice 130): Auto-Lauf; der Plan work/plans/2026-11-03-slice-130.md gilt mit seinen Annahmen 1 bis 9, Spalte „Plan“ damit angehakt – Claude, 03.11.2026
Build decision (slice 130): Spalte „Tests red“ angehakt mit dem Rot-Lauf auf 81374baa (Prüfnachweis): sieben neue Tests rot, die Schutztests für Uhrzeit- und Datumsfeld grün, weil sie bestehendes Verhalten sichern – Claude, 03.11.2026
Build decision (slice 130): Die Feldtests stehen in einer eigenen Datei `ViewTests.LiveFieldValues.cs` statt verteilt auf die Felddateien, und ihre Namen sind kürzer als im Plan (Regel „höchstens etwa 80 Zeichen“ aus .claude/slice/tests.md) – Claude, 03.11.2026
Build decision (slice 130): Beim Tippen meldet das Feld den Wert im Anzeigeformat („5,00“) nach außen, der Text bleibt „5“; ein ungültiger Zwischenstand geht als Text ohne Wert hinaus, wie beim Bestätigen mit Fehler – Claude, 03.11.2026
Build decision (slice 130): Die Durchsicht aller 18 Ansichten mit Betrags- und Zahlenfeldern fand nur einen teuren Rückruf (Laden der Freigebenden beim Rabatt); er läuft jetzt nur einmal gleichzeitig. Alle übrigen Rückrufe räumen Meldungen, rechnen Summen oder setzen „ungespeichert“; keiner speichert – Claude, 03.11.2026
Build decision (slice 130): Kein ADR; die Abweichung von Fluent ist das Ruling des Auftraggebers und steht in der Fluent-Ableitung (Abschnitt 6, Nr. 27) und im Brief 5.16, 5.19, 5.20; die Hilfe `seite-kassa` nennt sie in einem Satz – Claude, 03.11.2026
Build decision (slice 130): Esc stellt den letzten bestätigten Wert her (Annahme 4 des Plans, Brief 5.16), nicht wörtlich „den Wert vom Betreten“ der Slice-Datei; beim Tippen ist das derselbe Wert, nach einer Pfeiltaste der Wert der Pfeiltaste – Claude, 03.11.2026
Build decision (slice 130): Aus der Durchsicht: Das Tippen lädt die Freigebenden nur einmal, auch wenn die Antwort leer war; ein neuer Rabatt und „Übernehmen“ laden sie erneut. Der Test dazu war gegen die erste Fassung (nur „nicht gleichzeitig“) rot mit vier Aufrufen, auf ungespeichertem Stand, daher ohne Zeile im Prüfnachweis – Claude, 03.11.2026
Build decision (slice 130): Aus der Durchsicht: Der Kassatest prüft den Betrag genau (`"unitPrice":50` gefolgt von Komma oder Klammer), der Esc-Test zusätzlich die Lage der Marke beim Tippen; der Hilfesatz nennt den Knopf „Wertgutschein verkaufen“ – Claude, 03.11.2026
Build decision (slice 131): Auto-Lauf; der Plan work/plans/2026-11-03-slice-131.md gilt mit seinen Annahmen 1 bis 8, Spalte „Plan“ damit angehakt – Claude, 03.11.2026
Build decision (slice 131): Die Vorbelegung ruft dieselbe Rechnung wie der Knopf „Rest“ (`RestFor`, Barzahlungsrabatt und andere Zeilen eingerechnet); ausgelöst vom Fokus am `ItemsControl` der Zahlarten in `PosView`, nicht im gemeinsamen `MoneyField`; Kein ADR – Claude, 03.11.2026
Build decision (slice 131): Spalte „Tests red“ angehakt mit dem Rot-Lauf auf bbfc0813 (Prüfnachweis): fünf Tests rot (Rest, 88,80, Barzahlungsrabatt, Klick, Tab); die Schutztests „Karte bleibt 20,00“, „gedeckt bleibt leer“ und „Tippen ersetzt die Markierung, Rückgeld 11,00“ waren schon vorher grün, weil sie nur eine Regel absichern, die sich nicht verlieren darf – Claude, 03.11.2026
Build decision (slice 131): Die Ansichtstests laufen gegen die Rechnung der Ansichts-Stubs (89,00 €, Rückgeld 11,00 € bei 100 €); die 138,80 € des Plans gelten nur für die ViewModel-Tests – Claude, 03.11.2026
Build decision (slice 131): Der Test für eine gesperrte Zeile (Terminalzahlung läuft) entfällt: `PrefillRest` prüft `IsLocked` wie `FillRest`, dessen Sperre im Bestand ohne eigenen Test steht – Claude, 03.11.2026
Build decision (slice 132): Auto-Lauf; der Plan work/plans/2026-11-03-slice-132.md gilt mit seinen Annahmen 1 bis 8, Spalte „Plan“ damit angehakt – Claude, 03.11.2026
Build decision (slice 132): Der Knopf heißt in beiden Ansichten „Drucken“, ist ohne Bondrucker gesperrt und trägt den Hinweistext aus der Konstante `PosViewModel.NoPrinterHint`; die Belegansicht bindet dieselbe Konstante (Text und Sichtbarkeit unverändert) – Claude, 03.11.2026
Build decision (slice 132): Spalte „Tests red“ angehakt mit dem Rot-Lauf auf 5b292ce4 (Prüfnachweis): rot sind die beiden Ansichtstests (Knopf „Nochmals drucken“ nur mit Drucker, kein Hinweis); die drei `PosTests` (Druck an die Adresse, Reihenfolge Empfänger vor Druck, kein Drucker) sind Schutztests für Verhalten, das der Befehl schon hatte, und liefen grün – Claude, 03.11.2026
Build decision (slice 132): Die Tests schalten das automatische Drucken aus (`PrintAutomatically: false`), weil das Bezahlen sonst schon druckt und „Drucken“ doppelt zählt; der erste Entwurf fiel im Rot-Lauf damit auf („Sequence contains more than one element“) – Claude, 03.11.2026
Build decision (slice 132): Kein Klick-Test bis zum Drucker: die Shell setzt den echten Netzwerkdrucker ein; der Klickpfad ist durch `PrintReceiptCommand` in den `PosTests` und die manuelle Testliste gedeckt – Claude, 03.11.2026
Build decision (slice 132): Aus der Durchsicht: Am neuen Knopf steht der Grund als `ui:DisabledHint.Text` statt `ToolTip.Tip`; ein Tooltip erscheint an einem gesperrten Knopf nicht und hätte bei eingerichtetem Drucker zum Einrichten aufgefordert (Brief 5.22). Der gleiche Tooltip an der Belegansicht stammt aus `dev` und bleibt unverändert – Claude, 03.11.2026
Build decision (slice 133): Auto-Lauf; der Plan work/plans/2026-11-03-slice-133.md gilt mit seinen Annahmen, Spalte „Plan“ damit angehakt – Claude, 03.11.2026
Build decision (slice 133): Spalte „Tests red“ angehakt mit den Rot-Läufen auf 15c2700f (Prüfnachweis): API-Klassen `NameMatchTests`, `GuestFuzzyNameTests`, `BusinessIntentTests`, `BusinessAnswerTests`, `BusinessCatalogGuestTests` und Desktop `CompanionBlockTests` rot; das Gerüst baute, die Tests fielen an den Antworten – Claude, 03.11.2026
Build decision (slice 133): Die Gastsuche holt Kandidaten per ILIKE aus der Datenbank und behält davon nur ganze Namen (`NameMatch.Fit`, „stein“ trifft nicht „Steinhauser“); ohne Treffer folgt der Tippfehler-Vergleich im Speicher über Namen mit höchstens zwei Buchstaben Längenunterschied (±2 statt ±1 des Plans, damit „ue“ für „ü“ passt), erst danach der erste Name allein; Wörter unter zwei Buchstaben zählen nicht – Claude, 03.11.2026
Build decision (slice 133): Tippfehler bei Personen des Teams sind nur in `NameMatchTests` geprüft (gleiche Funktion `Fit`), nicht über eine eigene Teamfrage – Claude, 03.11.2026
Build decision (slice 133): Die Auswahl heißt „Der Name passt zu N Gästen. Bitte den Gast wählen.“; der Knopf trägt Name und Ort, damit gleichnamige Gäste unterscheidbar sind, und stellt dieselbe Frage mit `GuestId` – Claude, 03.11.2026
Build decision (slice 133): Der Shell-Test für den Bezug lädt den Kalender mit den Stubs der Dienstplan-Tests (`RosterTests.Setup`); ohne geladene Ressourcen ignoriert der Kalender `OpenAppointment` – Claude, 03.11.2026
Build decision (slice 133): Aus der Durchsicht: Zwei Wörter mit Tippfehler („christian steinr“) finden den Gast, bevor der erste Name allein gesucht wird; ein Wort ohne Gast („wann hat sie demnächst ihren termin“) verdrängt den Bezug nicht, wenn die Frage ein Pronomen trägt; „frei“ mit Pronomen ist keine Gastfrage; „ihr“ zählt als Bezug wie im Plan; die Auswahl nennt die Zahl aller Treffer, nicht nur der gezeigten fünf. Die vier neuen Tests kamen nach der Umsetzung und liefen ohne eigenen Rot-Lauf – Claude, 03.11.2026
Build decision (slice 133): Der Text der Auswahl weicht vom Plan („Für wen?“) ab: „Bitte den Gast wählen.“ sagt, was zu tun ist, und folgt der bestehenden Rückfrage „Bitte den Namen nennen“ – Claude, 03.11.2026
Build decision (slice 133): Der Klick-Test der Auswahl in `ViewTests` (Plan S5) entfällt: `CompanionBlockTests` prüfen, dass ein Folgeknopf die `GuestId` mitschickt; die Darstellung der Knöpfe ist die bestehende – Claude, 03.11.2026
Build decision (slice 133): Kein ADR; die Regeln folgen aus ADR 0072, ADR 0085 und dem Ruling vom 02.11.2026 – Claude, 03.11.2026
Build decision (slice 134): Auto-Lauf; der Plan work/plans/2026-11-03-slice-134.md gilt mit seinen Annahmen, Spalte „Plan“ damit angehakt – Claude, 03.11.2026
Build decision (slice 134): Spalte „Tests red“ angehakt mit dem Rot-Lauf auf 74f7b0a9 (Prüfnachweis): `AssistantListTests` und die umgestellte Erwartung in `BusinessTeamDayTests` rot gegen den alten Code, der Vertrag baute. Die Desktop-Tests (`CompanionListTests`, `ViewTests.CompanionList`, `RosterTests.Sprung_in_den_Wochenplan_zeigt_die_Woche_des_Tages`) entstanden nach dem ViewModel und liefen ohne eigenen Rot-Lauf – Claude, 03.11.2026
Build decision (slice 134): Eine nicht verstandene Frage ohne erkannten Tag und ohne Person behält den bisherigen Satz und die zwei Fragen der Rolle (Plan: neuer Satz, drei Fragen); es gibt je Rolle nur zwei feste Fragen, eine dritte wäre beliebig. Mit Erkanntem: „Dazu habe ich nichts gefunden. Erkannt: morgen, Anna. Meinen Sie vielleicht eine dieser Fragen?“ und bis zu drei Fragen; geprüft in `AssistantListTests`, ein eigener Test für den Fall ohne Erkanntes entfiel – Claude, 03.11.2026
Build decision (slice 134): „Wer arbeitet heute?“ ist über die umgestellte Erwartung in `BusinessTeamDayTests` geprüft (Kopfsatz, je Person eine Zeile, Sprung in den Wochenplan), nicht über einen eigenen neuen Test – Claude, 03.11.2026
Build decision (slice 134): Über 50 Zeilen endet die Liste ohne eigenen Knopf „Im Kalender zeigen“ für den Tag (Plan Annahme 4); der Kopfsatz nennt die volle Zahl, die API liefert höchstens 50 Zeilen (`Blocks.MaxItems`) – Claude, 03.11.2026
Build decision (slice 134): Titel und Detail einer Zeile tragen immer den vollen Text als Tooltip, nicht nur, wenn gekürzt; Avalonia meldet die Kürzung nicht als Eigenschaft. Zeilen ohne Ziel (freie Zeiten ohne Buchungsrecht) sind nicht anklickbar und nicht in der Tab-Reihenfolge und zeigen dadurch auch keinen Tooltip – Claude, 03.11.2026
Build decision (slice 134): Der Ladezustand sagt weiter „Der Assistent denkt nach …“ (bestehender Text) statt „Der Assistent sucht die Antwort …“; die Frage steht darüber als Kopf – Claude, 03.11.2026
Build decision (slice 134): Das Ins-Bild-Scrollen prüft `Nach_einem_Fehler_steht_die_Meldung_im_sichtbaren_Bereich` (Plan: `Nach_der_Antwort_steht_ihr_Kopf…`); die Antwort nutzt denselben Weg (`BringIntoView` nach jeder neuen Antwort oder Meldung), und bei einer neuen Antwort würde die kürzere Liste den Test auch ohne Scrollen grün machen – Claude, 03.11.2026
Build decision (slice 134): Der Sprung in den Wochenplan geht über `ITargetedArea` in `TeamViewModel`: Woche und Standort aus dem Ziel, auch wenn Team noch nie offen war (vorgemerkt bis zum Anlegen des Plans). Die Person wird nicht hervorgehoben (Plan Annahme 8) – Claude, 03.11.2026
Build decision (slice 134): Der Grund am gesperrten Knopf „Drucken“ der Kassa heißt jetzt „Verfügbar, sobald unter „Geräte“ ein Bondrucker eingerichtet ist.“: `ViewTests.Alle_Gruende_beginnen_mit_Verfuegbar_und_sind_kurz` war damit seit Slice 132 auf `dev` rot, und der Prüfnachweis dieser Slice braucht die Gruppe grün – Claude, 03.11.2026
Build decision (slice 134): `ki-und-hilfe-buchen.png` neu erzeugt: unverändert, weil die Bildvorlage eine Vorschlagsantwort aus der Ansichts-Attrappe zeigt und die Buchungskarte gleich bleibt – Claude, 03.11.2026
Build decision (slice 134): Aus der Durchsicht: Der Sprung in den Wochenplan fragt bei einem offenen Tag mit Änderungen „Änderungen speichern?“ (`Sprung_in_den_Wochenplan_fragt_bei_offenem_Tag_mit_Aenderung`); das Backoffice bekommt bei einer nicht verstandenen Frage weder erkannten Tag noch Personen des Teams (`Unverstaendliche_Frage_im_Backoffice…`); der Posteingang (`MessageDetailViewModel`) nimmt die Ziele der Zeilen zu seinen Vorschlägen dazu, damit „Was tun bei …“ keine Knöpfe verliert (`Listen_Antwort_bringt_die_Ziele…`); das Bild der Auswertung steht in der Hilfe wieder bei seinem Abschnitt. Diese Tests kamen nach der Korrektur und liefen ohne eigenen Rot-Lauf – Claude, 03.11.2026
Build decision (slice 134): Kein ADR; die Darstellung folgt aus ADR 0072, ADR 0085 und dem Ruling vom 02.11.2026 – Claude, 03.11.2026
Build decision (slice 135): Auto-Lauf; der Plan work/plans/2026-11-03-slice-135.md gilt mit seinen Annahmen 1 bis 12, Spalte „Plan“ damit angehakt – Claude, 03.11.2026
Build decision (slice 135): Spalte „Tests red“ angehakt mit den Rot-Läufen auf 35fcf4c6 (30 von 139 rot, Thema nicht erkannt, Gerüst ohne Zeilen), 422dceb5 und 40e85508 (Befunde der Durchsicht) und 5683e62b (Bedienfragen als Gast) im Prüfnachweis; grün als Schutztests: „Frage nach der Stornoregel bleibt bei der Regel“ und „Gutscheincode bleibt beim Gutscheinstand“ – Claude, 03.11.2026
Build decision (slice 135): Ohne Anrede und Bezug zählt ein Name bei Verlauf, Kassa-Stand und Fragebogen nur in kurzen Eingaben und nur, wenn es den Gast im Kundenstamm gibt (`GuestFinder.ExistsAsync`); sonst geht die Frage wie vor der Slice an Hilfe und Klassifizierer. Eine Wortliste ließ „rechnung splitten“, „gutschein verlängern“ und weitere Bedienfragen als Gastfragen durch (aus der Nachprüfung) – Claude, 03.11.2026
Build decision (slice 135): „Wer kommt als Nächstes“ greift nicht bei „nächster Termin“, damit „wann ist der nächste Termin von Steiner“ eine Gastfrage bleibt; allgemeine Fragen zur Ausfallgebühr ohne Termin oder Gast bleiben bei der Stornoregel – Claude, 03.11.2026
Build decision (slice 135): Die Gebühr rechnet nach Stornoregel, Behandlung und „nur neue Gäste“, unabhängig davon, ob ein Zahlungsanbieter eingerichtet ist; die Antwort sagt, ob eine Karte hinterlegt ist. Ohne Karte nennt sie also eine Gebühr, die das System nicht selbst einzieht – Claude, 03.11.2026
Build decision (slice 135): Der Fragebogen wird nur mit `ViewHealthData` entschlüsselt; ohne das Recht kommt der Stand aus den Metadaten (`AnamnesisStore.LatestSubmittedAtAsync`). Ein Test dafür fehlt, weil keine Rolle den Kalender ohne Gesundheitsrecht hat; Test 14 prüft deshalb das Backoffice (abgelehnt) – Claude, 03.11.2026
Build decision (slice 135): Therapie sieht in allen Gastantworten nur eigene Termine, auch in „zuletzt da“ und „nächster Termin“ (Zurückgestellt aus Slice 133); `BusinessCatalogGuestTests` fragt dafür als Anna (ihr Besuch) und zusätzlich als Ben (kein Besuch), keine Prüfung entfällt – Claude, 03.11.2026
Build decision (slice 135): Offene Anzahlungen stehen auch bei abgesagten Terminen, mit dem Hinweis auf die Absage (Geld noch nicht verrechnet); Tageszeiten begrenzen im Tagesüberblick auch die Liste, nicht nur die Zahl (aus der Durchsicht) – Claude, 03.11.2026
Build decision (slice 135): Die Windows-App zeigt Hilfethemen schon unter einem Block (`CompanionContent.axaml`); kein Vertrag und keine Ansicht geändert, deshalb keine Desktop-Prüfungen und kein `gen:api` – Claude, 03.11.2026
Build decision (slice 135): Kein ADR; die Themen folgen aus ADR 0072 und dem Ruling vom 02.11.2026 – Claude, 03.11.2026
Build decision (slice 136): Auto-Lauf; der Plan work/plans/2026-11-03-slice-136.md gilt mit seinen Annahmen 1 bis 14, Spalte „Plan“ damit angehakt – Claude, 03.11.2026
Build decision (slice 136): Spalte „Tests red“ angehakt mit dem Rot-Lauf auf 82e8067d im Prüfnachweis (41 von 49 rot: Thema nicht erkannt, Gerüst ohne Antwort; die 8 grünen sind Gegenproben bestehender Themen) – Claude, 03.11.2026
Build decision (slice 136): Kein eigenes Thema `ArticlePrice`; die Erkennung sieht einer Frage „Was kostet X?“ nicht an, ob X eine Behandlung oder ein Artikel ist. `CatalogAnswers` (Thema `TreatmentInfo`) sucht den Namen auch unter den Verkaufsartikeln; heißt er wie eine Behandlung, stehen beide in einer Tabelle (Test 21) – Claude, 03.11.2026
Build decision (slice 136): „Was muss bestellt werden?“ beginnt wie eine Bedienfrage („Was muss ich tun?“) und ging an das Handbuch; `BusinessIntents.Detect` nimmt Fragen, die `StockLowWords` treffen, von der Bedienregel `IsHowTo` aus. Andere Bedienfragen bleiben unverändert – Claude, 03.11.2026
Build decision (slice 136): Die Erkennungstests stehen in der neuen Klasse `StaffRoomStockIntentTests`, nicht in `BusinessIntentTests`. Die Gegenprobe „wie voll ist die sauna“ war falsch gewählt (sie war schon vor der Slice `DayLoad`); die Eingabe wurde zu „ist die sauna voll“ geändert, die Erwartung blieb – Claude, 03.11.2026
Build decision (slice 136): Annahme 10 geprüft: „haben wir am 8. dezember offen“ war ohne Änderung grün (Öffnung und Feiertag stehen schon in einer Antwort); nur die Doku-Zeile wurde ✅, kein Befund Ä342 – Claude, 03.11.2026
Build decision (slice 136): Der Liefertermin ist „voraussichtlich“: Bestelldatum plus Lieferzeit des Lieferanten (Lieferant über den Namen im Stamm), ohne neues Feld und ohne Migration; Bestellungen zeigt der Assistent nur mit `ManageLocation`, alle anderen neuen Themen mit `ViewCalendar` – Claude, 03.11.2026
Build decision (slice 136): Mindestbestand je Lagerort (`StoragePlaceMinimum`) nennt die Antwort nicht, nur den des Materials; die freie Zeit einer Person ist nicht gebaut (Annahme 4) – Claude, 03.11.2026
Build decision (slice 136): Nach der Durchsicht enger gefasst: „Was muss bestellt werden?“ und „Was sollten wir bestellen?“ nimmt allein `WhatToOrderWords` von der Bedienregel aus (nicht jeder Treffer von `StockLowWords`); „bis wann arbeitet“ statt „bis wann hat/ist“; Sperren und freie Zeiten brauchen ein Raumwort (nicht „Zeitraum“); Bestandsfragen schließen Warteliste, No-Shows, Geburtstage, Feiertage, Fragebogen, Absagen und Sperren aus; ein Artikel verdrängt eine genannte Behandlung nur, wenn sein ganzer Name in der Frage steht; Bestellungen und ihre Zeilen sind stabil sortiert – Claude, 03.11.2026
Build decision (slice 136): Bewusst nicht übernommen aus der Durchsicht: die Spalte „Bestellt“ zählt nur bestellte Positionen, keine Entwürfe (die Lager-Übersicht zählt Entwürfe mit; der Assistent sagt „bestellt“); die Bestellnummer steht nicht in der Tabelle; Zuständigkeiten außer Ersthelfer nennen kein „Zertifikat gültig“; freie Zeiten eines Raums kennen weder Feiertage noch Schließzeiten des Standorts (sie stehen bei den Öffnungszeiten) und zählen einen Mehrplatzraum bei einer Buchung als belegt wie „Welcher Raum ist frei?“ – Claude, 03.11.2026
Build decision (slice 136): Kein ADR, kein `gen:api`, keine Desktop-Prüfungen: Verträge und Ansichten bleiben unverändert (Blöcke, Tabellen und Hilfe zeigt die Leiste schon) – Claude, 03.11.2026

## Überraschungen
- Slice 119: Ein Live-Update baute das offene Terminfenster bisher auch während einer Bearbeitung neu auf; eine angefangene Notiz oder Zeitänderung ging dabei still verloren (jetzt behoben, siehe Build decision).
- Slice 119: Der Abgleich zeigte eine 409 schon vorher unter „Beim Abgleich nicht übernommen“; es fehlte nur die Version im Vertrag.
- Slice 120: Der Client wiederholt keine Aufrufe; „Wiederholung“ aus der Arbeitsanweisung gibt es nur als Statusabfrage beim Start (`MainViewModel`), sie bleibt dort.
- Slice 120: Drei ViewModels bauten selbst einen `ApiClient` (Designer-Konstruktor, zwei Statusabfragen); erst der Build nach der Umstellung zeigte sie.
- Slice 121: Ein blankes `null` neben einem `PathString` im Bedingungsausdruck wird zu einem leeren Pfad; die erste Fassung schrieb dadurch jede Anfrage auf „/“ um. Die bestehenden Tests zeigten es sofort (405).
- Slice 121: Die Mandantensperre prüft jeden Pfad unter `/api`, auch unbekannte; sie hätte `/api/v1/…` also auch ohne Umschreiben gesperrt.
- Slice 121: Drei Desktop-Testdateien prüfen die echte Adresse mit eigenem Handler statt über `StubApi` (`StartupWaitTests`, `StartupSignInTests`, `CustomerCardsEditTests`); die ersten beiden wurden in der vollen Suite rot, `CustomerCardsEditTests` blieb grün, hielt aber keine Antwort mehr zurück. Alle drei erwarten jetzt `/api/v1/…`.
- Slice 122: `.Result` kommt in `ViewModels/` an rund 20 Stellen vor und ist an keiner blockierend (fertige Tasks nach `Task.WhenAll`, DTO-Eigenschaft, Typname); ein pauschales Verbot wäre falsch rot gewesen. Außerhalb der ViewModels wartet eine dritte Stelle (`LocalServer`, `CountdownEvent` in `Task.Run`), die die Slice-Datei nicht nannte.
- Slice 129: Die Breitenrechnung des Plans (1248 px Fläche bei 1280 px Fenster) stimmte nicht: die Navigation nimmt Platz, gemessen wurden 1128 px Fläche der Seite; die Breiten wurden danach neu gewählt.
- Slice 129: Das Feld und die Auswahl der Gutscheinkarte passen auch in 232 px; nur der Knopf „Wertgutschein verkaufen“ war zu breit und kürzt jetzt mit „…“.
- Slice 130: Das Ereignis `ParsedField.Committed` hat keinen Abonnenten; die Masken hängen nur an den Bindungen `CommittedText` und `Value`.
- Slice 130: Das Rabattfeld der Kassa lud die Freigebenden bei jedem Zeichen über der Grenze neu, solange die erste Antwort fehlte (im Test vier Aufrufe für „25,00“); vorher fiel das nicht auf, weil der Wert erst beim Verlassen kam.
- Slice 131: Ein Gegenlauf ohne das zweite Markieren per `Dispatcher.Post` macht beide Klick-Tests rot (Text „89,00100“, Auswahl 5 statt 0): das Nach-Markieren ist nötig. Ein erster Entwurf markierte auch ohne Vorbelegung (Durchsicht): Klick in ein befülltes Feld hätte alles markiert; jetzt nur nach echter Vorbelegung (`PrefillRest` gibt `bool` zurück).
- Slice 131: Die Rechnung der Ansichtstests (89,00 €) weicht von der der ViewModel-Tests (138,80 €) ab; der erste Entwurf der Ansichtstests nahm die falsche Summe und fiel erst im Rot-Lauf auf.
- Slice 133: Ein Termin im Kalender lässt sich per `OpenAppointment` erst wählen, wenn die Ressourcen geladen sind; der erste Entwurf des Shell-Tests sah deshalb keinen Bezug.
- Slice 133: `BusinessIntents` hatte eine eigene Titel-Liste und eine eigene Namensregel neben `NameMatch`; beide sind jetzt eine Stelle (`NameMatch.ParseName`).
- Slice 134: `ViewTests.Alle_Gruende_beginnen_mit_Verfuegbar_und_sind_kurz` war auf `dev` seit Slice 132 rot (Grund „Bondrucker unter „Geräte“ einrichten“); hier mit behoben.
- Slice 134: `CustomerCardsEditTests.Termine_mit_langsamer_Antwort_dann_Stammdaten_bearbeiten_laesst_nur_Stammdaten_offen` war in einem gefilterten Lauf einmal rot (IndexOutOfRangeException in `ObservableCollection.Insert` aus `CustomersViewModel.LoadEmployeesAsync`), allein und in der Gruppe ohne `ViewTests` grün: Last, kein Fehler der Slice; der Stapel deutet auf zwei gleichzeitige Ladevorgänge derselben Liste.
- Slice 134: Neben der Leiste fragt auch `MessageDetailViewModel` (Posteingang) `api/assistant` und liest nur `Suggestions`; die Zeilen einer Listen-Antwort sieht es nicht.
- Slice 135: Wertgutscheine tragen keinen Gast; der Assistent findet sie nur über den Beleg, auf dem sie verkauft oder eingelöst wurden. Ein geschenkter, nie eingelöster Gutschein steht beim Beschenkten nicht.
- Slice 135: Einen Zeitpunkt der Absage speichert der Termin nicht; „heute abgesagt“ nimmt `UpdatedAt` wie `CardGuarantees`. EF übersetzt `OrderBy` auf einem projizierten positionellen Record nicht, und `Customer.InHouseName` ist nicht gemappt.
- Slice 136: „Was muss bestellt werden?“ galt als Bedienfrage (`IsHowTo` nimmt „was muss …“) und ging an das Handbuch; erst ein Rot-Lauf der Erkennungstests zeigte es. Alle 49 Tests der Slice waren danach auf Anhieb grün und trotzdem zu locker: die Durchsicht des Branches fand per Regex-Herleitung zwölf Fragen, die die ersten Regeln an sich zogen („bis wann ist die Sauna offen“, „haben wir noch jemanden auf der Warteliste“, „ist der Parkplatz gesperrt“, „Wie kann ich Arganöl nachbestellen?“), und einen Artikel „Massageöl“, der die Behandlung „Massage“ aus der Antwort verdrängte. Sie sind jetzt Gegenproben (`Andere_Fragen_gehen_nicht_an_die_neuen_Themen`, 13 Fälle), rot auf 80ecbf8b.
- Slice 135: Die erste Fassung leitete bestehende Fragen um („Wie hoch ist die Ausfallgebühr bei Nichterscheinen?“ → Gebühr, „Hat Frau Moser einen offenen Termin?“ → Kassa-Stand, „rechnung splitten“ → Gast); die Durchsicht fand sie per Reflection über rund 160 Rezeptionsfragen, die Tests der Slice waren grün.
- Rundenabschluss: `StartupWaitTests.Antwortet_der_Server_nie_zeigt_der_Start_nach_der_Wartezeit_keine_Verbindung_statt_zu_haengen` war in `dotnet test --solution` rot (Assert.True, 765 ms; 4874 von 4877 grün, 2 übersprungen), die Klasse danach zweimal einzeln grün (8 von 8): Last, kein Fehler.
- Rundenabschluss: `help.test.tsx` meldete das eingecheckte Bild `images/app/ki-antwort-liste.png` (Slice 134) als fehlend, bis neben `node_modules/.vite` auch `node_modules/.vitest-cache` beiseitegeschoben war; danach 359 von 359 grün. Ein beiseitegeschobener `.vite` allein reicht nicht.

## Zurückgestellte Kleinigkeiten
- Slice 122: Der ViewModel-Scan fängt kein Warten hinter einer Hilfsmethode und keine blockierenden Aufrufe in `Views/` und `Controls/` (heute ohne Fund); erweitern, wenn dort einer auftaucht.
- Versionsprüfung für andere Entitäten als Termine: nach demselben Muster, sobald dort ein Konflikt stört.
- Ä319: Aufträge an den Assistenten („buche“, „verschiebe“, „sag ab“) als vorausgefüllte Maske; braucht einen Nachtrag zu ADR 0072, Vorschlag für R07.
- Slice 119: Bricht ein Abgleich mittendrin ab, verlieren die übrigen Vormerkungen die Verkettung; eine zweite eigene Änderung am selben Termin steht beim nächsten Abgleich dann unter „nicht übernommen“.
- Slice 119: Zwei Änderungen fast gleichzeitig ergeben weiter „Diese Zeit wurde gerade anderweitig vergeben“ ohne aktuellen Termin.
- Slice 119: Eine offene Kundenbearbeitung im Terminfenster geht bei einem Live-Update des Termins weiter verloren (sie zählt nicht als Bearbeitungskarte).
- Slice 119: Wer einen Termin zieht, während im eigenen Terminfenster seine Notiz offen ist, bekommt beim Speichern der Notiz den Konflikt mit der eigenen Verschiebung.
- Druck auf A4 oder als PDF über den Windows-Drucker an der Kassa, falls der Auftraggeber das mit „drucken“ meinte (Slice 132 baut den Bondrucker-Weg).
- Slice 120: Ein eigenes Testprojekt für `AispaStudio.Client` (ohne Avalonia) erst, wenn ein zweiter Client die Bibliothek nutzt.
- Slice 121: `Location`-Adressen in `Created`-Antworten (rund 40) nennen noch `/api/…`; sie bleiben über den Alias gültig.
- Slice 130: Esc nach einer Pfeiltaste stellt den Wert der Pfeiltaste her, nicht den vom Betreten des Feldes (die Pfeiltaste bestätigt); kein Test dafür.
- Slice 130: Zahlungsdaten einer Überweisung (`Transfer`) räumt die Kassa jetzt schon beim ersten abweichenden Zwischenstand im Betragsfeld der Zahlart weg, auch wenn man denselben Betrag neu tippt; man fordert sie dann neu an.
- Slice 130: Regel für neue ViewModels: Setzt ein ViewModel während des Tippens den bisherigen Wert eines Betrags- oder Zahlenfelds erneut, überschreibt das jetzt den getippten Text (vorher ohne Wirkung). Heute tun das nur Befehle (neue Rechnung, „Rest“), nie ein Rückruf beim Tippen.
- Slice 121: Die Liste der Bereiche ohne Version steht in `ApiVersion` und `HttpApiClient` doppelt; eine gemeinsame Konstante in `AispaStudio.Contracts`, falls ein Bereich dazukommt.
- Slice 121: Ein Rückfall der Windows-App auf `/api/…`, wenn ein älterer Server v1 nicht kennt (ADR 0085: Server zuerst aktualisieren).
- Slice 129: Bei 1280 px bleiben der Rechnung aufgeklappt 316 px; eine zweite Spalte, die beim Einklappen der ersten breiter wird, wäre möglich und wurde nicht gebaut.
- Slice 131: Wer mit Tab durch die Zahlarten geht, füllt die erste leere Zeile mit dem Rest; die folgenden bleiben leer. Beobachten, ob das an der Kassa stört (Esc stellt den vorbelegten Wert her, leeren geht über Entf).
- Slice 132: Der Inhalt des Drucks mit Empfänger (ESC/POS) ist hier nicht geprüft; ihn rendert der Server (`ReceiptPrinter`), die Windows-App schickt nur die Beleg-Id. Ob dafür ein API-Test besteht, wurde nicht untersucht.
- Slice 132: Ein Klick-Test mit Drucker in der Kassa-Ansicht wäre machbar, ohne dass etwas ins Netz geht: der Ansichts-Stub kennt die Druck-Route nicht, `PrintText` begänne mit „Nicht gedruckt:“. Nicht gebaut; der Befehl ist in den `PosTests` gedeckt. Der Tooltip „Bondrucker unter „Geräte“ einrichten“ der Belegansicht gehört ebenfalls auf `ui:DisabledHint.Text` (steht auf `dev`).
- Slice 132: Das Bezahlen druckt bei der Standardeinstellung „Automatisch drucken“ schon; ein Test, der danach „Drucken“ wählt, sieht zwei Drucke, wenn er die Automatik nicht ausschaltet.
- Slice 133: „Wann kommt die Lieferung?“ gilt als Gastfrage und antwortet „Keinen Gast „Lieferung“ gefunden.“; mit Slice 136 (Lager) prüfen.
- Slice 133: Die Web-Leiste „KI und Hilfe“ schickt keinen Bezug (offener Termin, letzter Gast) mit; „sie“ fragt dort nach dem Namen.
- Slice 133: Eine Therapeutin sieht in Gastantworten Termine der Kolleg:innen am Standort (bestand schon vor der Slice); `DayAnswers` beschränkt dieselbe Rolle auf eigene Termine. Angleichen in Slice 135 (Verlauf des Gastes).
- Slice 133: „Wann hat Lena ihren nächsten Termin?“ mit Lena aus dem Team gilt als Gastfrage; Personen des Teams vor Gästen zu prüfen, braucht einen Abgleich mit dem Team in der Erkennung.
- Slice 133: Im Kalender ist meist ein Termin gewählt; „wann war er zuletzt da“ meint dann diesen Gast, nicht den Gast der letzten Antwort (Reihenfolge des Plans). Die Antwort nennt den Namen; der Auftraggeber prüft das in der manuellen Testliste.
- Slice 133: Doppelnamen mit Bindestrich und Namen aus drei Wörtern werden nur mit den ersten zwei Namenswörtern gesucht.
- Slice 134: Nach „Alle zeigen“ per Tastatur verschwindet der Knopf und der Fokus ist weg; den Fokus auf die elfte Zeile setzen.
- Slice 134: Über 50 Einträgen nennt der Kopfsatz die volle Zahl, der Knopf „Alle zeigen (50)“ nur die gelieferten; z. B. „50 von 60“.
- Slice 134: Die Tooltips von Titel und Detail decken die Zeile fast ganz; der Tooltip der Zeile mit der Aktion erscheint daher selten (der Bildschirmleser nennt die Aktion im Namen).
- Slice 134: „Erneut fragen“ nach einer Gastauswahl (Slice 133) stellt die Frage ohne die gewählte `GuestId`.
- Slice 134: Der Fall „nicht erreichbar“ (Status 0) ist in den Tests der Leiste nicht eigens gestellt, nur 5xx und leere Antwort.
- Slice 134: Mehrtägige Antworten („Wer arbeitet nächste Woche?“) und die übrigen Antwortgeber mit Text und Tabelle bleiben, wie sie sind; ob dort Einträge doppelt stehen, prüft kein Test.
- Slice 134: Die Ansage des Ladezustands für den Bildschirmleser ist nicht geprüft (Plan, Review-Fokus).
- Slice 135: „Fällt eine Gebühr an, wenn Steiner absagt?“ (Name ohne Anrede mitten im Satz) findet den Gast nicht; ohne offenen Termin fragt die Antwort nach dem Termin.
- Slice 135: „rechnung huber“ ohne Gast Huber geht an Hilfe und Klassifizierer und verliert „Keinen Gast … gefunden“ mit „Gast suchen“; mit Anrede bleibt es. Liefert ein Sprachmodell selbst ein Gastthema mit einem Bedienwort als Name, prüft niemand den Kundenstamm.
- Slice 135: Ohne Test: 23 Std. 59 Min. vor Beginn, „Karte hinterlegt“, „nur neue Gäste“, die Zahl der Nachmittagstermine im Tagesüberblick, der Fragebogen ohne Gesundheitsrecht (keine Rolle hat das).
- Slice 135: Begleitpersonen aus Gruppenbuchungen stehen unter dem Namen der buchenden Person, auch im Verlauf; die Zahl im Kopfsatz ist auf 50 begrenzt.
- Slice 135: Sprung in die Kassa aus „offene Rechnung“ und Aufträge „Bogen senden“, „Anbieten“, „Termin absagen“ (Ä319).
- Slice 136 (Ä341): Eine Bestellung speichert keinen zugesagten Liefertermin; der Assistent nennt den errechneten („voraussichtlich“). Ein Feld dafür wäre eine Schemaänderung.
- Slice 136 (Ä342): Der Mindestbestand je Lagerort (`StoragePlaceMinimum`) fehlt in der Antwort zum Bestand.
- Slice 136 (Ä343): „Hat Marco heute noch freie Zeit?“ (freie Zeiten einer Person) ist nicht gebaut; die Doku-Zeile bleibt ◐.
- Slice 136: Aus der Durchsicht zurückgestellt: Entwürfe in der Spalte „Bestellt“ (Übersicht und Assistent zählen verschieden), Bestellnummer in der Tabelle, „Zertifikat gültig“ bei Zuständigkeiten außer Ersthelfer, Feiertage und Schließzeiten in den freien Zeiten eines Raums, Mehrplatzräume als „frei, solange ein Platz frei ist“.
- Slice 136: Ohne Test: Raumsperren mit Uhrzeit (halbe Tage), Mehrplatzräume (gelten bei einer Buchung als belegt wie in „Welcher Raum ist frei?“), Feiertage und Schließzeiten in den freien Zeiten eines Raums (sie stehen nur bei den Öffnungszeiten), Zuständigkeiten außer Ersthelfer und Kassa, Teilzeit-Lieferungen (`PartiallyReceived`).

## Offene Entscheidungen
NEEDS-DECISION: Bestätigt die Datenschutz-Beratung, dass alle Rollen mit Kundenzugriff (auch die Rezeption) die Anamnese lesen und fotografierte Bögen prüfen dürfen (Ä309, Auftraggeber 02.11.2026)? (from R05)
NEEDS-DECISION: Passt es, dass Slice 93 nur drei Bausteine baut, „Liste mit Detail“ erst mit Slice 94 entsteht und „Feld mit Fragezeichen“ entfällt? (from R05)
NEEDS-DECISION: Bleibt die Stempeluhr in der Handy-App, obwohl E6 „keine Zeiterfassung per Handy“ nennt? Empfehlung: lassen, bis feststeht, ob E6 auch den Bestand meint. (from R05)
NEEDS-DECISION: Welche Belege fehlen in den Testdaten (Ä313)? Kassabelege erzeugt der Verlauf schon (rund 2.200); gemeint sein könnten Eingangsrechnungen, Lieferscheine oder Terminalbelege, oder eine Ansicht, die Kassabelege nicht zeigt. Danach eine eigene Slice.
NEEDS-DECISION: Kommen die Aufträge an den Assistenten (Ä319) in R07 statt in R06? Empfehlung: ja, erst wenn 133 bis 136 laufen.

## Manuelle Testliste
### Slice 119 – Termin-Konflikt: Version mitschicken statt still überschreiben
Start: `dotnet run --project src/AispaStudio.AppHost`, im Aspire-Dashboard die Ressource `windows-app` zweimal starten (Arbeitsplatz A und B), beide als Inhaberin anmelden und denselben Tag öffnen
1. Auf B einen Termin anklicken, **Notiz hinzufügen** wählen und einen Text eintippen, ohne zu speichern.
Expected: Das Notizfeld ist offen.
2. Auf A denselben Termin eine Stunde später ziehen.
Expected: A zeigt „Verschoben auf …“; auf B wandert die Karte, das Notizfeld im Terminfenster bleibt offen.
3. Auf B **Notiz speichern** klicken.
Expected: Oben im Terminfenster steht „Der Termin wurde inzwischen an einem anderen Arbeitsplatz geändert. Bitte den aktuellen Stand prüfen.“; das Terminfenster zeigt die Zeit von A, die Notiz ist nicht gespeichert.
4. Auf B im Terminfenster **Verschieben …** öffnen, dann auf A den Termin noch einmal ziehen und auf B eine andere Uhrzeit eingeben und **Verschieben** klicken.
Expected: Oben im Terminfenster steht dieselbe Meldung, der Termin bleibt bei der Zeit von A.
Closes: Ä281
Result: ☐
### Slice 120 – API-Client in die gemeinsame Bibliothek
Start: `dotnet run --project src/AispaStudio.AppHost`, im Aspire-Dashboard die Ressource `windows-app` starten und als Inhaberin anmelden
1. Den heutigen Tag im Kalender öffnen und einen Termin in eine belegte Zeit ziehen.
Expected: Der Kalender lädt wie bisher; die Meldungszeile nennt den Grund der Ablehnung wie vor der Slice.
2. Die App schließen und wieder starten.
Expected: Die App ist ohne neue Anmeldung angemeldet.
3. Im Aspire-Dashboard die Ressource `api` stoppen und in der App den Tag wechseln.
Expected: Oben steht „Keine Verbindung zum Server.“; der zuletzt geladene Tag ist weiter zu sehen.
Closes: Ä282
Result: ☐
### Slice 121 – API-Version in den Pfaden
Start: `dotnet run --project src/AispaStudio.AppHost`, im Aspire-Dashboard die Ressource `windows-app` starten und als Inhaberin anmelden
1. Den heutigen Tag im Kalender öffnen und einen Termin in eine freie Zeit ziehen.
Expected: Der Kalender lädt und verschiebt wie bisher; im Aspire-Dashboard (Ressource `api`, Traces) beginnt das Attribut `url.path` der Aufrufe mit `/api/v1/`.
2. Im Browser die Adresse der Ressource `web` plus `/mein-tag/` öffnen und als Mitarbeiterin anmelden.
Expected: Der Tagesplan lädt wie bisher.
3. Im Browser die Adresse der Ressource `web` plus `/buchen/` öffnen und einen freien Termin suchen.
Expected: Die Buchungsseite zeigt freie Zeiten wie bisher.
Closes: Ä283
Result: ☐
### Slice 122 – Muster für die Masken festhalten und absichern
Start: `docs/architektur.md` im Editor öffnen und im Worktree `dotnet test --project tests/AispaStudio.Desktop.Tests -c Release --filter-class "AispaStudio.Desktop.Tests.ClientArchitectureTests"` bereithalten
1. Den Abschnitt „Client gegen API“ in `docs/architektur.md` lesen.
Expected: Sechs Schritte (Endpunkt, Aufruf über den Client, ViewModel asynchron, Lade- und Fehlerzustand, Konflikt, Live-Update) und die begründeten Ausnahmen stehen dort; jede Angabe passt zum Code.
2. In einem ViewModel unter `src/AispaStudio.Desktop/ViewModels/` testweise `task.Wait();` einfügen und den Testlauf der Klasse starten.
Expected: `Kein_ViewModel_wartet_blockierend_auf_einen_Task` ist rot und nennt Datei und Zeile; nach dem Entfernen ist er grün.
Closes: Ä284
Result: ☐
### Slice 129 – Kassa: eigene Spalte für Artikel und Gutscheine, einklappbar
Start: `dotnet run --project src/AispaStudio.AppHost`, im Aspire-Dashboard die Ressource `windows-app` starten und als Inhaberin anmelden
1. Die Seite **Kassa** öffnen, **Kassieren** wählen und das Fenster auf 1280 px Breite ziehen; danach auch bei voller Breite ansehen.
Expected: Ganz links steht die Spalte „Artikel und Gutscheine“ mit Artikel-Kacheln und „Gutschein verkaufen“, daneben „Code scannen“, „Heute abzurechnen“ und „Geparkte Rechnungen“, dann die Rechnung und „Bezahlen“; Rechnung und Bezahlen sind voll bedienbar. (superseded by slice 137: links steht „Heute abzurechnen“ mit dem Suchfeld „Gast, Code oder Artikel“, daneben die Rechnung, das zu Beginn zugeklappte Panel „Artikel und Gutscheine“ und „Bezahlen“; unter 1350 px Seitenbreite steht „Bezahlen“ unter der Rechnung)
2. Im Kopf der Spalte auf den Pfeil klicken.
Expected: Es bleibt eine schmale Leiste mit Symbol und senkrechtem Namen, die Rechnung wird breiter. (superseded by slice 137: das Panel beginnt zugeklappt; ein Klick auf die Leiste klappt es auf, der Pfeil im Kopf wieder zu)
3. Die App schließen, neu starten und wieder die Kassa öffnen.
Expected: Die Spalte ist weiter eingeklappt.
4. Mit Tab auf die Leiste gehen und die Leertaste drücken.
Expected: Die Spalte klappt auf, der Fokus bleibt auf dem Kopf; der Screenreader meldet „Artikel und Gutscheine, aufgeklappt“.
5. Einen Artikel anklicken, dann unter „Gutschein verkaufen“ einen Wert eintragen und „Wertgutschein verkaufen“ klicken.
Expected: Artikel und Wertgutschein stehen auf der Rechnung.
Closes: Ä310
Result: ☐
### Slice 130 – Betrags- und Zahlenfelder übernehmen den Wert schon beim Tippen
Start: `dotnet run --project src/AispaStudio.AppHost`, im Aspire-Dashboard die Ressource `windows-app` starten, als Inhaberin anmelden und in der Kassa einen Termin abrechnen
1. In der Spalte „Artikel und Gutscheine“ bei **Wert (EUR)** „50“ tippen und ohne Tab gleich **Wertgutschein verkaufen** klicken.
Expected: Der Knopf ist schon beim Tippen bedienbar; ein Wertgutschein über 50,00 € steht auf der Rechnung.
2. An einer Position den Rabatt öffnen, auf Euro stellen, „5“ tippen und ohne Tab **Übernehmen** klicken.
Expected: Die Position hat 5,00 € Rabatt.
3. Bei einem Paket den Preis eintippen und ohne Tab **Paket verkaufen** klicken.
Expected: Das Paket steht mit dem getippten Preis auf der Rechnung.
4. Bei **Wert (EUR)** „12,345“ tippen und mit Tab weitergehen.
Expected: Während des Tippens ist **Wertgutschein verkaufen** gesperrt; nach dem Tab steht am Feld „Höchstens zwei Nachkommastellen.“
5. Bei **Wert (EUR)** „20“ tippen, Tab drücken, wieder ins Feld gehen, „35“ tippen und Esc drücken.
Expected: Im Feld steht wieder „20,00“, und ein Verkauf ergibt 20,00 €.
Closes: Ä311
Result: ☐
### Slice 131 – Kassa: Betrag der Zahlart mit dem offenen Rest vorbelegen
Start: `dotnet run --project src/AispaStudio.AppHost`, im Aspire-Dashboard die Ressource `windows-app` starten, als Inhaberin anmelden und in der Kassa einen Termin abrechnen
1. Bei **Bar gegeben** in das leere Feld klicken.
Expected: Dort steht der offene Betrag, ganz markiert.
2. „100“ tippen.
Expected: Das Feld zeigt 100; darunter steht „Rückgeld“ mit der Differenz zur Rechnung.
3. Das Feld leeren, bei **Karte** 50 eintragen, dann in das Feld **Bar gegeben** klicken.
Expected: Dort steht der offene Rest nach den 50,00 €.
4. In das Feld **Karte** mit der 50 erneut klicken.
Expected: Es bleibt bei 50,00.
5. Mit Tab in ein leeres Betragsfeld gehen, wenn alles schon gedeckt ist.
Expected: Das Feld bleibt leer.
6. Eine neue Rechnung nur in ein Feld klicken (bar), **Bezahlen und Beleg erstellen** drücken; danach mit Karte und geteilt (Gutschein plus Karte plus bar, jeweils nur klicken) wiederholen.
Expected: Jede Zahlung geht durch, der Beleg zeigt die Beträge, bar mit Rückgeld.
Closes: Ä312
Result: ☐
### Slice 132 – Kassa: Drucken direkt in der Ansicht „Bezahlt“
Start: `dotnet run --project src/AispaStudio.AppHost`, im Aspire-Dashboard die Ressource `windows-app` starten, als Inhaberin anmelden und in der Kassa einen Termin abrechnen
1. Ohne eingerichteten Bondrucker (unter „Geräte“ leer) bezahlen und die Ansicht „Bezahlt“ ansehen.
Expected: Der Knopf **Drucken** ist sichtbar und grau, daneben steht „Kein Bondrucker eingerichtet (unter „Geräte“).“
2. Unter „Geräte“ einen Bondrucker eintragen, speichern, zur Kassa zurückgehen (Ansicht „Bezahlt“ noch offen) und **Drucken** klicken.
Expected: Der Knopf ist bedienbar, der Hinweis ist weg; daneben steht „Beleg gedruckt.“ oder „Nicht gedruckt: …“ mit dem Grund, und der Beleg kommt aus dem Drucker.
3. Eine Rechnung über 400 € bezahlen, **Empfänger erfassen**, übernehmen und **Drucken** klicken.
Expected: Der Bon zeigt die Rechnung mit Empfänger.
Closes: Ä314
Result: ☐
### Slice 133 – Assistent: unscharfe Namen und Bezug auf den offenen Gast
Start: `dotnet run --project src/AispaStudio.AppHost`, im Aspire-Dashboard die Ressource `windows-app` starten, als Inhaberin anmelden; zwei Gäste „Christian Steiner“ (Anrede Herr, mit Termin nächste Woche) und „Eva Steiner“ (Anrede Frau) anlegen, „KI und Hilfe“ öffnen
1. „wann hat herr steiner seinen termin“ fragen.
Expected: Die Antwort nennt den nächsten Termin von Christian Steiner mit Datum, Uhrzeit, Behandlung und Person.
2. „wann kommt steiner“ fragen.
Expected: „Der Name passt zu 2 Gästen. Bitte den Gast wählen.“ mit einer Tabelle (Gast, Ort, Nächster Termin) und je Gast einem Knopf.
3. Den Knopf „Eva Steiner“ klicken.
Expected: Die Antwort gilt Eva Steiner („hat keinen Termin geplant …“).
4. „termin herr steinr“ fragen.
Expected: Trotz Tippfehler kommt der Termin von Christian Steiner.
5. Im Kalender einen Termin eines anderen Gastes anklicken und „wann war sie zuletzt da“ fragen.
Expected: Die Antwort nennt den letzten Besuch dieses Gastes.
6. Ohne gewählten Termin und ohne offene Kundenkarte direkt nach dem Start „wann war sie zuletzt da“ fragen.
Expected: „Welcher Gast ist gemeint? Bitte den Namen nennen, zum Beispiel „Frau Müller“.“
7. „wann kommt zzyzx“ fragen.
Expected: „Keinen Gast „zzyzx“ gefunden.“ mit dem Knopf „Gast suchen“.
Closes: Ä315
Result: ☐
### Slice 134 – Assistent: Antworten als kurze Liste, Frage bleibt stehen
Start: `dotnet run --project src/AispaStudio.AppHost`, im Aspire-Dashboard die Ressource `windows-app` starten, als Inhaberin anmelden, im Kalender den Standort Wien Innere Stadt wählen und „KI und Hilfe“ öffnen
1. „Was steht morgen an?“ fragen.
Expected: Ein Satz mit Tag, Standort und Zahl der Termine, darunter eine Liste: je Zeile Uhrzeit, Gast und darunter Behandlung und Person, jede Zeile einzeilig; bei mehr als zehn Terminen darunter „Alle zeigen (N)“, kein zweiter Knopf je Termin.
2. „Alle zeigen“ anklicken, dann eine Zeile anklicken.
Expected: Die Liste klappt an derselben Stelle auf; der Klick auf die Zeile öffnet den Kalender am Tag und markiert den Termin.
3. Mit der Maus über einen gekürzten Behandlungsnamen fahren.
Expected: Der Tooltip zeigt den ganzen Namen; in der Zeile endet er mit „…“.
4. „Wer arbeitet morgen?“ fragen und eine Person anklicken.
Expected: Satz mit der Zahl der Personen; ein Klick auf eine Zeile öffnet Verwaltung › Team › Wochenplan in der Woche von morgen.
5. Im Aspire-Dashboard die Ressource `api` anhalten und „Termine übermorgen?“ fragen.
Expected: Eine rote Meldung, die Frage steht weiter im Feld, die vorige Antwort bleibt, „Erneut fragen“ ist da; nach dem Starten der API beantwortet „Erneut fragen“ die Frage.
6. Fragefeld leeren und Pfeil nach oben drücken.
Expected: Die zuletzt gestellte Frage steht wieder im Feld, die Marke am Ende.
7. „Kaffee morgen mit Anna?“ fragen (Anna ist im Team).
Expected: „Dazu habe ich nichts gefunden. Erkannt: morgen, Anna. …“ und drei Fragen als Knöpfe, z. B. „Was steht morgen an?“.
Closes: Ä316
Result: ☐
### Slice 135 – Assistent: Verlauf des Gastes, Termine und Absagen
Start: `dotnet run --project src/AispaStudio.AppHost`, im Aspire-Dashboard die Ressource `windows-app` starten, als Rezeption anmelden, im Kalender den Standort Wien Innere Stadt und den heutigen Tag wählen und „KI und Hilfe“ öffnen
1. „historie“ und den Namen eines Gastes mit mehreren Besuchen fragen (aus den Beispieldaten, z. B. ein Gast aus dem heutigen Tag).
Expected: Kopfsatz mit Name, Zahl der Besuche und letztem Besuch; als erste Zeile „Kundenkarte öffnen“, dann der nächste Termin, die Besuche mit Behandlung und Person; darunter Hilfethemen. Ein Klick auf die erste Zeile öffnet die Kundenkarte.
2. Einen Termin dieses Gastes im Kalender öffnen und „hat sie noch einen gutschein“ fragen.
Expected: Die Antwort nennt den Gast und seine Wertgutscheine mit Restwert in Euro oder sagt, dass keiner offen ist.
3. „wer ist noch nicht da“ und danach „wer kommt als nächstes“ fragen.
Expected: Zuerst Termine, die schon begonnen haben und nicht eingecheckt sind, mit „seit N Min.“; dann die nächsten fünf Termine ab jetzt, je Zeile mit Sprung in den Kalender.
4. Einen Termin von heute öffnen und „fällt eine gebühr an“ fragen.
Expected: Die Antwort nennt Stunden bis Beginn, die Frist der Stornoregel und entweder „keine Gebühr“ oder den Prozentsatz mit Betrag in Euro.
5. „wer hat heute abgesagt“, „wer steht auf der warteliste“ und „termine heute nachmittag“ fragen.
Expected: Absagen von heute mit Grund; Einträge der Warteliste mit Behandlung und Wunschzeit; nur Termine zwischen 12 und 17 Uhr.
6. „rechnung splitten“ fragen.
Expected: Keine Gastantwort („Keinen Gast … gefunden“ erscheint nicht); es kommen Hilfethemen oder „Dazu habe ich nichts gefunden“.
7. Abmelden, als Therapeutin anmelden und „offene rechnung“ mit dem Namen des Gastes aus Schritt 1 fragen.
Expected: „Dafür fehlt die Berechtigung. Gutscheine, Rechnungen und Anzahlungen sieht die Rezeption an der Kassa.“
8. Die Fragen der Abschnitte Kunden, Termine und Absagen aus docs/konzepte/assistent-rezeption-fragen.md der Reihe nach stellen.
Expected: Jede Zeile mit ✅ oder ◐ antwortet aus den Daten wie in der Spalte „Antwort in der App“ beschrieben; keine fällt auf ein Hilfethema zurück.
Closes: Ä317
Result: ☐

### Slice 136 – Assistent: Personal, Räume und Lager
Start: `dotnet run --project src/AispaStudio.AppHost`, im Aspire-Dashboard die Ressource `windows-app` starten, als Rezeption anmelden, im Kalender den Standort Wien Innere Stadt und den heutigen Tag wählen und „KI und Hilfe“ öffnen
1. „wann hat“ und den Vornamen einer Person im Dienst heute und „pause“ fragen.
Expected: Die Antwort nennt Dienst, Dienstende und die eingetragene Pause (oder die Dauer, die das Arbeitszeitrecht verlangt, wenn keine eingetragen ist); für eine Person im Urlaub nur „nicht im Dienst: Urlaub“.
2. „wer ist heute ersthelfer“ und „wer ist für die kassa zuständig“ fragen.
Expected: Zuerst die Ersthelfer im Dienst mit Dienstende; ist niemand dabei, eine Tabelle mit den Zuständigen und dem Grund; dann Person und Vertretung der Kassa-Zuständigkeit.
3. „wann ist der paarraum morgen frei“ und „ist der hamam morgen gesperrt“ mit Namen von Räumen aus den Beispieldaten fragen.
Expected: Freie Zeiten innerhalb der Öffnungszeit, bei „heute“ ab jetzt; bei einer Raumsperre Art, Zeitraum und Grund statt freier Zeiten.
4. „haben wir das“ und den Namen eines Materials mit „noch“ fragen, dann „was ist bald aus“.
Expected: Bestand je Lagerort und gesamt, bei zu kleinem Bestand der Hinweis „Unter dem Mindestbestand“; danach eine Tabelle der Materialien unter dem Mindestbestand mit der bestellten Menge.
5. „was kostet die“ und den Namen eines Verkaufsartikels aus der Kassa fragen.
Expected: Der Verkaufspreis in Euro; heißt der Name auch eine Behandlung, stehen beide in einer Tabelle.
6. „wann kommt die lieferung“ fragen, erst als Inhaberin, dann als Rezeption.
Expected: Inhaberin: offene Bestellungen mit Lieferant, Bestelldatum und voraussichtlichem Termin; Rezeption: „Dafür fehlt die Berechtigung. Bestellungen sieht, wer das Lager des Standorts verwaltet.“
7. Die Fragen der Abschnitte Personal, Räume und Lager aus docs/konzepte/assistent-rezeption-fragen.md der Reihe nach stellen.
Expected: Jede Zeile mit ✅ antwortet aus den Daten wie in der Spalte „Antwort“ beschrieben; keine fällt auf ein Hilfethema oder „Keinen Gast … gefunden“ zurück.
Closes: Ä318
Result: ☐
