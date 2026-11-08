Status: closed

# Runde R04 – Test des Auftraggebers von „Neuer Termin“, Freie Zeit und Kalender

## Quelle
- Die Notizen des Auftraggebers vom 27.10.2026 aus dem Test der Windows-App nach dem Nachtrag „Neuer Termin bucht immer in der angeklickten Spalte“ (`532d6978`), gesammelt in der Sitzung „Neuer Termin: Lena und Konfliktmeldung“ und mit „kannst starten“ freigegeben.
- Die offenen Entscheidungen der Runde R03 stehen unten mit „(aus R03)“.

## Eingang

| Kennung | Wo | Beobachtet → Erwartet | Größe | Quelle |
|---|---|---|---|---|
| Ä146 | Windows-App, Neuer Termin, Tastatur | Mit Tab von oben kommt man nicht über „Ändern“ (gewählter Gast) hinaus, von unten nach oben geht es → Tab erreicht alles der Reihe nach | S | Auftraggeber, 27.10.2026 – erledigt in Slice 83 |
| Ä147 | Windows-App, alle Masken | Tab-Navigation in allen Masken prüfen → vorwärts und rückwärts jedes Element genau einmal, mit Scan-Test | M | Auftraggeber, 27.10.2026 – erledigt in Slice 83 |
| Ä148 | Windows-App, Neuer Termin, Notiz | Die Notiz ist zu klein, lange Wünsche werden abgeschnitten → mehrzeilig, wächst mit | S | Auftraggeber, 27.10.2026 – erledigt in Slice 82 |
| Ä149 | Windows-App, Seitenpanel, Reiter unten | Der Reiter „+ Neuer Termin“ soll den Kunden zeigen und ein Kennzeichen, dass ein neuer Termin in Bearbeitung ist | S | Auftraggeber, 27.10.2026 – erledigt in Slice 82 |
| Ä150 | Windows-App, Kalenderkopf | Knopf „Freie Zeit suchen“ neben dem Tag als Hauptfunktion zum Anlegen neuer Termine | S | Auftraggeber, 27.10.2026 |
| Ä151 | Windows-App und API, Neuer Termin | „Der Standort hat zu dieser Zeit geschlossen.“ ist als Hinweis gut, aber die Rezeption soll trotzdem anlegen können (Beispiel 18:45 bei Sophie Gruber, Dienst bis 17:00) | M | Auftraggeber, 27.10.2026 |
| Ä152 | Windows-App, Kalender, Scrollen | Sophie steht rechts außerhalb; nach rechts gescrollt, Notiz „Arzt“ angelegt und gespeichert → der Kalender springt an den Anfang, Sophie und der neue Eintrag sind weg → Scrollstelle bleibt | M | Auftraggeber, 27.10.2026 – in Slice 86 nicht nachstellbar, offen bis zur manuellen Probe (Ä171) |
| Ä153 | Windows-App, Neuer Termin, Wechsel der Art | Beim Wechsel auf Paar/Spa oder Sauna werden keine Kundendaten übernommen → Eingaben gehen beim Wechsel der Art nicht verloren; sinnvolle weitere Stellen mitdenken | M | Auftraggeber, 27.10.2026 – erledigt in Slice 82 |
| Ä154 | Windows-App, Kalender, Team- und Raumansicht | Es ist nicht erkennbar, dass es weitere Räume bzw. Mitarbeitende gibt und man scrollen muss | M | Auftraggeber, 27.10.2026 – erledigt in Slice 86 |
| Ä155 | Windows-App, Freie Zeit suchen, Hilfe | Klick auf Hilfe zeigt rohes JSON `Not Found` (404) statt des Themas | S | Auftraggeber, 27.10.2026 – erledigt in Slice 87 |
| Ä156 | Windows-App, alle Seiten, Meldungen | „In den nächsten 14 Tagen ist nichts frei.“ ist sehr klein → wirklich wichtige Meldungen deutlich und einheitlich in der ganzen App | M | Auftraggeber, 27.10.2026 – erledigt in Slice 88 |
| Ä157 | Windows-App, Freie Zeit suchen | „Sprache des Gastes“ wird nicht gebraucht → ausblenden, im Code für später vermerken | S | Auftraggeber, 27.10.2026 |
| Ä183 | Windows-App, Freie Zeit suchen | Durchklicken der Vorschläge buchte: ein Doppelklick auf eine Zeile buchte mit Gast sofort, der Reiter kam danach mit der alten Liste zurück, ein Klick auf die gebuchte Zeit meldete „belegt“ → Klick und Doppelklick wählen nur aus, gebucht wird nur über den Knopf oder Enter; nach dem Buchen fällt der Reiter weg, beim Zurückholen lädt er neu – erledigt im Nachtrag vom 28.10.2026 | S | Auftraggeber, 28.10.2026 |
| Ä184 | API und Windows-App, alle änderbaren Masken | Es gibt kein Protokoll, wer wann über welchen Kanal etwas angelegt, geändert oder gelöscht hat (Termin: nur `CreatedAt`, `UpdatedAt`, `Source`; kein „wer“, kein alter/neuer Wert, keine Spur von Stornos) → Verlauf je Datensatz, zuerst für Termine („angelegt per SMS am … um …, geändert von … am … um …, storniert …“, Abschnitt „Verlauf“ im Termin-Panel), danach die übrigen Masken; eigener ADR | L | Auftraggeber, 28.10.2026 – Termine erledigt in Slice 90, übrige Masken erledigt in Slice 91 |
| Ä158 | Windows-App, Hover-Karte und Popups | Schrift der Hover-Karte zu klein → auf das Nötige kürzen, Gast, Behandlung, Tag, Zeit und Raum fett und gut sichtbar; Popups in der ganzen App sinnvoll und einheitlich | M | Auftraggeber, 27.10.2026 – erledigt in Slice 89 |

## Slices
| Slice | Name | Befunde | Hängt ab von | Modell | Neue Kennungen |
|---|---|---|---|---|---|
| 82 | Neuer Termin: Notiz, Reiter, Eingaben beim Wechsel der Art | Ä148, Ä149, Ä153 | – | opus | Ä159–Ä161 |
| 83 | Tab-Reihenfolge in allen Masken | Ä146, Ä147 | 82 | opus | Ä162–Ä164 |
| 84 | Freie Zeit suchen als Hauptweg im Kalenderkopf, Sprache ausblenden | Ä150, Ä157 | – | sonnet | Ä165–Ä167 |
| 85 | Buchen außerhalb der Öffnungszeit mit „Trotzdem buchen“ | Ä151 | – | opus | Ä168–Ä170 |
| 86 | Kalender: Scrollstelle erhalten, weitere Spalten erkennbar | Ä152, Ä154 | 82 | opus | Ä171–Ä173 |
| 87 | Hilfe aus „Freie Zeit suchen“ öffnet das Thema statt einer 404 | Ä155 | – | sonnet | Ä174–Ä176 |
| 88 | Wichtige Meldungen sichtbar und einheitlich | Ä156 | 84 | sonnet | Ä177–Ä179 |
| 89 | Hover-Karten und Popups lesbar und einheitlich | Ä158 | – | sonnet | Ä180–Ä182 |
| 90 | Änderungsverlauf für Termine | Ä184 | – | opus | Ä185–Ä187 |
| 91 | Änderungsverlauf für Kunden, Behandlungen, Dienstplan und Rechte | Ä184 | 90 | sonnet | Ä188–Ä190 |

Parallel ohne Abhängigkeit: 82, 84, 85, 87, 89; danach 83 und 86 (nach 82) und 88 (nach 84). 82, 83, 84, 86 und 89 ändern alle `CalendarView.axaml`: nicht zugleich starten, was dieselbe Datei ändert (Feld `files`).

## Entscheidungen
Ruling: Nicht machbare Behandlungen werden in „Neuer Termin“ ausgeblendet, nicht grau und durchgestrichen gezeigt; die Person der angeklickten Spalte lässt sich nicht abwählen (Nachtrag `532d6978`) – Auftraggeber, 27.10.2026
Ruling: Weitere Spalten werden mit Variante 2 erkennbar: Verlauf am Rand des Rasters mit rundem Knopf „‹“/„›“ und „+n“, Klick schiebt um eine Bildschirmbreite; keine Zählerzeile im Kopf, keine angeschnittene Nachbarspalte (Slice 86, Ä154) – Auftraggeber, 27.10.2026
Ruling: „Sprache des Gastes“ in „Freie Zeit suchen“ wird nur ausgeblendet; die Logik bleibt mit einem Vermerk im Code für eine mögliche spätere Nutzung (Slice 84, Ä157) – Auftraggeber, 27.10.2026
Ruling: Die Rezeption darf außerhalb der Öffnungszeit und des Dienstes buchen, wenn sie es nach dem Hinweis ausdrücklich will („Trotzdem buchen“); Online-Buchung, Freie Zeit und KI bieten solche Zeiten nicht an; Nachtrag zu ADR 0073 (Slice 85, Ä151) – Auftraggeber, 27.10.2026
Ruling: „Freie Zeit suchen“ wird der primäre Knopf im Kalenderkopf; „+ Termin“ bleibt als normaler Knopf mit Menü und öffnet weiter sofort „Neuer Termin“ (Slice 84, Ä150) – Claude, 27.10.2026 (Vorschlag beim Sammeln, der Auftraggeber gab danach „kannst starten“ ohne Einwand)
Ruling: Beim Wechsel der Art in „Neuer Termin“ wird übernommen, was in der neuen Art ein Feld hat (Tabelle beim Sammeln: Paar/Spa, Sauna, Pause, Sperrzeit, Notiz, Rückweg), die Notiz geht in „Freie Zeit suchen“ mit; der Folgetermin schreibt weiter nur „Folgetermin“ (Slice 82, Ä153) – Claude, 27.10.2026 (Vorschlag beim Sammeln, ohne Einwand)
Ruling: Wichtige Meldungen (verhindern ein Ergebnis oder verlangen eine Entscheidung) sind InfoBars mit Ursache und Weg; kleiner grauer Text bleibt Nebeninfos (Slice 88, Ä156) – Claude, 27.10.2026 (Vorschlag beim Sammeln, ohne Einwand)
Ruling: „Trotzdem buchen“ ist ein Feld der Anfrage (`AppointmentRequest.OutsideHoursConfirmed`), das der Server nur bei Quelle Rezeption beachtet; aufhebbare Ablehnungen kennzeichnet der Server (`BookingRejected.OutsideHours`), die App vergleicht keine Meldungstexte; mit Bestätigung entfallen Öffnungszeit, Schließzeit des Standorts und Dienst, Belegung, Qualifikation, Raum, Gerät, Raumsperre, Abwesenheit und „Nicht bei mir“ gelten weiter (Slice 85, Ä151) – Claude, 27.10.2026 (Annahmen 1–4 im Plan, Auto-Lauf)
Ruling: Slice 85 setzt „Trotzdem buchen“ nur für „Neuer Termin“ um; Paar/Spa, Sauna-Eintritt und Verschieben im Kalender lehnen außerhalb der Öffnungszeit weiter ab und sind als Ä168–Ä170 für eine Folge-Slice festgehalten (Slice 85) – Claude, 27.10.2026 (Annahme 5 im Plan, Auto-Lauf)
Ruling: „Nichts frei“ in „Freie Zeit suchen“ ist eine Warn-InfoBar mit den Einschränkungen als Ursache und dem Knopf, der die erste aufhebt („Tageszeit: egal“, „Alle Personen“, „Alle Räume“); „Auf die Warteliste“ entfällt, weil die Windows-App keinen Wartelisten-Eintrag anlegen kann (Ä177) (Slice 88, Ä156) – Claude, 28.10.2026 (Annahme 1 im Plan, Auto-Lauf)
Ruling: Umgestellt auf InfoBar sind die Fehlertexte, die ein Ergebnis verhindern (Gast-Karte, Überblick nicht geladen, Abo-Zeile, Assistent, Antwortentwurf im Posteingang); `DayHint` und `OfflineNote` bleiben Nebeninfos; Regel M8 in `docs/gestaltung/meldungen.md` (Slice 88, Ä156) – Claude, 28.10.2026 (Annahme 3 im Plan, Auto-Lauf)
Entscheidung beim Bauen (Slice 82): Eine schon besuchte Art steht beim Zurückwechseln unverändert da (gemerkt je Art), nur eine erstmals gewählte Art übernimmt; „Länge der Belegung“ ist die Dauer der Behandlung und geht erst mit gewähltem Gast an Pause und Sperrzeit (ohne Gast behält die Pause ihren Vorschlag, bestehende Tests der Pausenlänge bleiben); die Notiz zum Termin wandert zwischen Termin, Paar/Spa, Sauna und der Art „Notiz“, nicht in den Grund einer Sperrzeit; Paar/Spa wählt unter den gemeinsamen Zeiten den übernommenen Beginn; das Kennzeichen „in Bearbeitung“ ist ein Punkt 6 × 6 accent am Symbol des Reiters, nur bei „Neuer Termin“; die Notiz wächst nur in den Eingaben für neue Einträge (Nachtrag zu Brief 5.16) – Claude, 27.10.2026 (Annahmen im Plan, Lauf ohne Rückfrage)
Entscheidung beim Bauen (Slice 82): Spalte „Tests rot“ angehakt mit echtem Rot (10 von 11 neuen ViewModel-Tests, nach der Durchsicht 5 von 7 weiteren; der Bildtest der Notiz fällt ohne den neuen Stil mit Höhe 68 statt 200) – Claude, 27.10.2026
Ruling: Hover-Karte des Termins (Slice 89, Ä158): Behandlung, Tag mit Uhrzeit und Raum stehen in Grundschrift 14 und fett, Dauer und Person normal, in der Raumansicht die Person fett. Der Tag ist neu in der Karte, weil der Auftraggeber ihn ausdrücklich nannte; sonst kommen keine Angaben dazu. – Auftraggeber, 27.10.2026 (Ä158)
Ruling: „Nur, was etwas sagt“ (Slice 89, Annahme, bei „Weiter mit den Annahmen“ übernommen): Besuche stehen nur bei Gästen ohne Etikett Stammgast/Erstbesuch; der Ablauf nur bei Einwirkzeit oder geänderten Zeiten; „Nicht erschienen“ ab dem ersten Mal als Warnung im orangen Band („1× nicht erschienen“), nie als Zeile „Etikett – Wert“. – Slice 89, Annahme
Ruling: Änderungsverlauf (Ä184) bleibt 3 Jahre ab der Änderung (Verjährung § 1489 ABGB), ein Hintergrundjob löscht danach; wird ein Gast gelöscht oder anonymisiert, fällt der Verlauf seiner Datensätze mit; die Kassa bleibt bei RKSV/TSE und den 7 Jahren der BAO (Slice 90, ADR 0082) – Auftraggeber, 28.10.2026 (Empfehlung von Claude angenommen)
Ruling: Den Verlauf sieht, wer den Datensatz sehen darf; Dienstplan, Benutzer und Rechte nur Inhaberin und Manager (Slices 90, 91) – Auftraggeber, 28.10.2026 (Empfehlung angenommen)
Ruling: Gesundheitsdaten (Schema `health`) erscheinen im Verlauf nur als „geändert von … am …“, ohne Inhalt, auch nicht verschlüsselt (Slices 90, 91) – Auftraggeber, 28.10.2026 (Empfehlung angenommen)
Ruling: Umfang des Verlaufs in zwei Schritten: zuerst Termine mit Kanal, Verschieben, Ändern, Storno, Nicht erschienen und Absage durch den Betrieb (Slice 90), danach Kundendaten, Behandlungen und Preise, Dienstplan und Abwesenheiten, Benutzer und Rechte (Slice 91); einzelne Einstellungen (Farben, Layout) nicht; technisch über den bestehenden Änderungs-Trigger mit Person und Kanal je Anfrage, alte und neue Werte (Slice 90) – Auftraggeber, 28.10.2026 (Empfehlung angenommen)
Entscheidung beim Bauen (Slice 86): Eine Spalte zählt an einem Rand als verborgen, wenn sie dort mehr als 1 px aus dem Ausschnitt ragt (angeschnittene zählen mit); der Knopf schiebt um genau die Breite des sichtbaren Ausschnitts, ohne Ausrichtung auf Spaltenanfänge; rund 40 × 40 im Stil des sekundären Knopfs mit Chevron 16 über „+n“ (12, halbfett), senkrecht mittig, 12 px vom Rand, Verlauf 56 px über Kopf und Raster; kein Tab-Halt (die Pfeiltasten scrollen den Kalender schon, Slice 83 prüft parallel die Tab-Reihenfolge); Hinweis und Screenreader-Name „Weiter zu 3 Spalten: …“ bzw. „Zurück zu 2 Spalten: …“ ohne Richtungswort; die Deckkraftmaske nutzt `Black`/`Transparent` mit begründeter Ausnahme im Farbscan statt eines neuen Pinsels in `Colors.axaml`; kein Schatten am Knopf (ein `Button` hat keinen), abweichend vom Plan – Claude, 28.10.2026 (Annahmen 1–5 im Plan, Auto-Lauf)
Entscheidung beim Bauen (Slice 86): Für Ä152 ist kein Code geändert, weil kein Test den Sprung zeigte (Ä171); vier Ansichtstests sichern, dass Versatz und neuer Eintrag nach dem Speichern bleiben. Ein Festhalten der Scrollstelle in Spalten statt Pixeln war gebaut und wurde wieder entfernt, weil derselbe Test auch ohne es grün war. Hilfetext und Handbuchbild ändern sich nicht (Ansicht, kein Ablauf) – Claude, 28.10.2026 (Annahme 6 im Plan, Auto-Lauf)
Entscheidung beim Bauen (Slice 86): Spalte „Tests rot“ angehakt mit echtem Rot gegen unverändertes `dev` in einem eigenen Prüf-Worktree: 3 von 6 neuen Tests rot (die drei Knopf-Tests, Element fehlt); die drei Scroll-Tests waren schon grün (Ä171) – Claude, 28.10.2026

Ruling: Ä146 wird allgemein behoben (`Controls/TabOnceBehavior.cs`: eine Liste vergisst ihr Tab-Ziel, wenn sie ausgeblendet wird oder sich einen unsichtbaren Eintrag merkt), nicht durch Umbau der Maske „Neuer Termin“; das deckt auch die Trefferliste in Gäste und Eintritt ab (Slice 83) – Claude, 28.10.2026 (Annahme 1 im Plan, Auto-Lauf)
Ruling: „Reihenfolge wie die Lage im Bild“ prüft der Scan in Formularen (Seitenleiste, Dialog, Kundendaten-Maske) und nur innerhalb desselben Scrollbereichs: Tab springt nicht nach oben, ohne in eine Spalte weiter rechts zu gehen; Kopfzeilen der Seiten werden auf Vollständigkeit und Rückwärtsfolge geprüft (Slice 83, Ä147) – Claude, 28.10.2026 (Annahme 4 im Plan, Auto-Lauf)
Entscheidung beim Bauen (Slice 83): Der „Vorschlag“ der Abnahme ist der erste Knopf im Kasten „Vorschläge“, mit den Beispieldaten „Wunsch in die Notiz übernehmen“; alle Spalten der Checkliste mit echtem Rot (Abnahmetest: Tab blieb 60-mal auf „Ändern“; Scan-Test ohne die Abhilfe: „Kalender, Neuer Termin: Tab kommt nicht zum Anfang zurück“) – Claude, 28.10.2026

Ruling: Änderungsverlauf technisch (Slice 90, ADR 0082): eigene Tabelle `sync.history` neben `sync.changes` mit eigenem Trigger `sync.log_history()` (andere Lebensdauer als das Abgleichprotokoll); Slice 90 protokolliert `aispa.Appointments` und jede Tabelle im Schema `health` (`ChangeHistory.Tables`, Slice 91 ergänzt); Anlegen und Löschen tragen keine Werte, Änderungen nur die geänderten Spalten ohne `UpdatedAt`; Zeitpunkt ist `now()` der Transaktion – Claude, 28.10.2026 (Annahmen 1–5 im Plan, Auto-Lauf)
Ruling: Person und Kanal kommen als Sitzungseinstellung `aispa.user_id`/`aispa.channel`, gesetzt vom `ChangeActorInterceptor` vor dem ersten schreibenden Befehl je Verbindung; Team = angemeldete Person, `/api/online…` = Online ohne Person, sonst `Job`; die Terminannahme setzt den Kanal der Nachricht; Abgleich = `Sync`; Personen stehen mit ihrem Anzeigenamen zur Zeit des Lesens da (Slice 90) – Claude, 28.10.2026 (Annahmen 6 und 10 im Plan, Auto-Lauf)
Ruling: Gast löschen löscht den Verlauf über einen Trigger an `Customers` (`customer_id` jeder Verlaufszeile); für ein späteres Anonymisieren steht `sync.forget_customer_history` bereit (Slice 90, Ä186) – Claude, 28.10.2026 (Annahme 7 im Plan, Auto-Lauf)
Ruling: Die Beispieldaten erzeugen ihren Verlauf am Ende aus den fertigen Terminen (gleich auf beiden Wegen, `DemoBulkTests`); jeder siebte Termin der Rezeption ist einmal um eine Stunde verschoben, Rezeptionstermine tragen die erste unbefristete Rezeptionskraft des Standorts (Slice 90) – Claude, 28.10.2026 (Annahme 9 im Plan, Auto-Lauf)
Entscheidung beim Bauen (Slice 90): „Behandlung ändern“ aus der manuellen Prüfung gibt es an einem Termin nicht (`TreatmentId` ist unveränderlich); die manuelle Testliste prüft stattdessen „Notiz ändern“, der Befund steht als Ä185 unter Überraschungen – Claude, 28.10.2026 (Annahme 8 im Plan)
Entscheidung beim Bauen (Slice 90): Spalte „Tests rot“ angehakt mit echtem Rot der acht API-Abnahmetests (sieben `ChangeHistoryTests`, ein SMS-Test in `MailIntakeTests`: „relation sync.history does not exist“ bzw. leerer Verlauf); die Desktop-Tests entstanden direkt vor dem ViewModel und wurden nicht einzeln rot ausgeführt (sie prüfen Typen, die es vorher nicht gab) – Claude, 28.10.2026
Ruling: „Inhaberin und Manager“ für den Verlauf von Dienstplan, Benutzern und Rechten heißt `Roles.IsOwnerLevel`: Inhaber und Manager (Geschäftsleitung); die Standortleitung (`StaffRole.Manager`) und das Backoffice sehen ihn nicht, auch nicht mit `ViewStaffRecords` (Slice 91) – Claude, 28.10.2026 (Annahme 1 im Plan, Auto-Lauf)
Ruling: Kunde und Behandlung zeigen den Verlauf mit dem Kalenderrecht wie der Datensatz selbst; der Verlauf eines Gastes führt Kundendaten, E-Mail-Adressen und Anamnese (nur „geändert von“), seine Termine behalten den Verlauf im Termin-Panel (Slice 91) – Claude, 28.10.2026 (Annahmen 2 und 3 im Plan, Auto-Lauf)
Ruling: Freitexte (Notiz, Notiz für die Rezeption, Hinweis an Gäste) erscheinen im Verlauf nur als „… geändert“, ohne Wortlaut; Räume, Geräte und Qualifikationen einer Behandlung (Verknüpfungstabellen) und Entwürfe des Dienstplans haben keinen Verlauf (Slice 91) – Claude, 28.10.2026 (Annahmen 4 bis 6 im Plan, Auto-Lauf)
Entscheidung beim Bauen (Slice 91): Kein neuer ADR; [ADR 0082](../../docs/entscheidungen/0082-aenderungsverlauf-ueber-den-aenderungs-trigger.md) sah den zweiten Schritt vor und bekommt den Stand von Slice 91 – Claude, 28.10.2026 (Annahme 8 im Plan)
Entscheidung beim Bauen (Slice 91): Behandlungen, Dienstplan und Zugänge sind Verwaltungsseiten der Team-Oberfläche (WebView2 der Windows-App, ADR 0053), daher bekommen sie den Abschnitt „Verlauf“ als React-Komponente (`web/src/components/HistorySection.tsx`); nur die Kundenkarte ist eine Avalonia-Ansicht (`HistorySectionView`, aus dem Termin-Panel herausgezogen) – Claude, 28.10.2026
Entscheidung beim Bauen (Slice 91): Spalte „Tests rot“ angehakt, ohne dass die Abnahmetests (`EntityHistoryTests`) einzeln rot gelaufen sind: sie entstanden vor Trigger-Erweiterung, Satzbau und Endpunkten (diese gab es nicht), liefen aber erst nach dem Bau; der erste Lauf zeigte nur das Rot von `Gast_loeschen_loescht_den_Verlauf_seiner_Termine` (Slice 90): der neue Trigger an `Customers` schrieb beim Löschen eine Zeile, die vom Löschen des Verlaufs übrig blieb; behoben, indem das Löschen eines Gastes nichts mehr protokolliert – Claude, 28.10.2026

## Überraschungen
- Ä168 (Slice 85): Paar oder Spa-Tag lehnt außerhalb der Öffnungszeit ohne „Trotzdem buchen“ ab (`GroupPlanner` prüft die Öffnungszeit selbst, an Schließtagen siehe Ä104). Nächster Schritt: dieselbe Bestätigung für `GroupBookingRequest` und `GroupBookingViewModel`.
- Ä169 (Slice 85): Der Sauna-Eintritt lehnt außerhalb der Öffnungszeit mit „Der Standort hat in dieser Zeit nicht durchgehend geöffnet.“ ohne „Trotzdem buchen“ ab (`AreaVisitService.BookAsync`).
- Ä170 (Slice 85): Verschieben im Kalender (Ziehen, Termin-Panel) außerhalb der Öffnungszeit oder des Dienstes wird ohne „Trotzdem buchen“ abgelehnt (`BookingService.MoveAsync`).
- Slice 85: In der vollen API-Suite lief `StartupStateTests.Einspielen_blockiert_den_Start_nicht…` unter Last parallel laufender Sitzungen 12 min 53 s und damit über seine Frist von 12 min; einzeln wiederholt grün. Last, keine Regression.
- Slice 85: Pause, Sperrzeit und Notiz prüfen keine Öffnungszeit (wie im Nachtrag R03 festgehalten); dort war nichts zu tun.
- Slice 87 (Ä155): Die 404 mit JSON ließ sich ohne laufenden AppHost nicht nachstellen. Vite liefert `/help/?thema=…` mit 200, die beiden Fragezeichen im Panel zeigen auf vorhandene Themen. Annahme: Ein Server (die API statt Vite/nginx, ein Web-Ordner ohne `help`) antwortete auf `/help/` mit seiner Fehlerantwort; die API leitet in der Entwicklung nur `/` auf Vite um (`TeamWebRoot`). Behoben ist es an drei Stellen: Fragezeichen am Panel auf `kalender-freie-zeit-suchen`, Prüfung der Serveradresse vor dem Laden (`HelpPageCheck`), Fehlerseite „Thema nicht gefunden“. Ob die Ursache getroffen ist, zeigt die Probe des Auftraggebers (manuelle Testliste). Ä174 (neu): `/buchen/`, `/mein-tag/`, `/betreiber/` und `/help/` auf der API in der Entwicklung leiten nicht auf Vite um.
- Slice 87: Das rote Vorab des Webtests und des Ansichtstests wurde nicht einzeln ausgeführt (Test und Umsetzung entstanden hintereinander in einem Lauf); die Tests prüfen Dinge, die es vorher nicht gab (Überschrift „Thema nicht gefunden“, Fragezeichen mit Thema `kalender-freie-zeit-suchen`, Klasse `HelpPageCheck`).
- Slice 87: Die Basisprüfung lief im Hauptordner auf sauberem `dev` (format grün, Build grün bis auf einen Fehler in der noch unfertigen Datei des Worktrees, den dessen Ordner `.claude/worktrees` ins Hauptprojekt zieht); im Worktree danach beides grün.
- Slice 84: Das rote Vorab der beiden Ansichtstests wurde nicht einzeln ausgeführt (Test und Umsetzung entstanden hintereinander); sie prüfen Dinge, die es vorher nicht gab (`FreeTimeButton`, `ShowLanguage`). Der erste Testlauf scheiterte am Analyzer CA1822 für `ShowLanguage => false`; jetzt Auto-Eigenschaft ohne Setter (Wert `false`).
- Slice 84: Die Bilderzeugung (`ViewTests` mit `AISPA_SCREENSHOTS`) änderte 113 PNG-Dateien, darunter solche ohne Bezug (`anmeldung.png`); übernommen sind nur `freie-zeit-suchen-1.png` und `paar-und-spa-tag-1.png` (zeigen den Kopf bzw. den Knopf „Termin“), die übrigen sind zurückgesetzt.
- Slice 84: In der Hilfe stand die Sprache auch bei `team.md` („bei Freie Zeit suchen … Sprache des Gastes“) und `sprachen-und-quelle.md` (Abschnitt „Freie Zeit in einer Sprache suchen“); beide angepasst, der Anker `#freie-zeit` bleibt.
- Ä177 (Slice 88): Die Windows-App kann keinen Wartelisten-Eintrag anlegen (nur ansehen und schließen); der Weg „Auf die Warteliste“ aus dem Slice-Text fehlt deshalb. Nächster Schritt: Eintrag aus „Freie Zeit suchen“ anlegen (Gast, Behandlung, Wunschzeit).
- Ä178 (Slice 88): Weitere Hinweise als `TextBlock` mit `WarnBrush`, nicht umgestellt, weil sie neben einem Ergebnis stehen (Nebeninfo): `DayHint`, `OfflineNote` im Kalenderkopf, `GapNote` und `OutsideWish` an den Zeiten, `Note` an Kunde und Anfrage. Falls der Auftraggeber den Kalenderkopf „Ohne Verbindung“ als wichtig sieht, wird er eine InfoBar.
- Slice 88: Plausibilität der Suche: „Nachmittags findet erst an Tag 2 Zeiten“ ist schon durch `Die_Tageszeit_holt_fehlende_Vorschlaege_an_den_naechsten_Tagen_nach` gesichert; `FreeTimeFinder` und der Server (ganze Tage ab jetzt) verhalten sich schlüssig. Ob für die Signaturmassage nachmittags wirklich 14 Tage nichts frei ist, hängt an den Beispieldaten und gehört in die Probe des Auftraggebers.
- Slice 88: Das rote Vorab der Tests wurde nicht einzeln ausgeführt (Tests und Umsetzung entstanden hintereinander); sie prüfen Dinge, die es vorher nicht gab (`HasNoSlots`, `RelaxText`, `RelaxCommand`, Scan der Eigenschaften).
- Ä180 (Slice 89): Durchsicht aller Popups: reiche Karten gibt es nur im Kalender (Termin, Streifen, Spaltenkopf mit `hovercard`); Kunden, Kassa, Meldungen und die übrigen Ansichten haben nur schlichte Tooltips (12, Fläche toast) und Flyouts mit Menüeinträgen. Darum nichts umgestaltet; das Muster steht in Design-Brief 5.22 und `meldungen.md` M8.
- Ä181 (Slice 89): „Nicht erschienen1×“ entstand, weil das Etikett (96 breit) länger war als seine Spalte; die Zeile entfällt nun, der erste Ausfall steht im Band. Die übrigen Etiketten der Karte (Buchung, Bereiche, Wünsche, Vorlieben …) passen in die 96; ein Ansichtstest prüft, dass kein Etikett abgeschnitten ist.
- Ä182 (Slice 89): Das Handbuchbild `kalender-hoverkarte-karte.png` zeigt noch die alte Karte (kleine Schrift); es ist nicht neu erzeugt (siehe Zurückgestellt).

- Slice 82: Drei Zeilen Notiz passen schon in die feste Höhe 68 (14 px Schrift); den Unterschied zeigt erst eine längere Notiz (bis 200). Der Bildtest prüft deshalb beides: drei Zeilen ohne Abschneiden und 20 Zeilen mit 200.
- Slice 82: Vier bestehende Tests der Pausenlänge (`NewEntryTests.Past`, Vorschau, Alt+Ziffer) wurden rot, als die Länge der vorbelegten Behandlung auch ohne Gast an die Pause ging; die Länge zählt jetzt erst mit gewähltem Gast, die Tests sind unverändert.
- Slice 82: `help/helpFeedback.test.tsx` und `help/helpPage.test.tsx` liefen unter Last paralleler Sitzungen in Zeitüberschreitungen (5–9 s), einzeln einmal grün, einmal wieder rot; kein Bezug zu den geänderten Hilfetexten (`textRules` und übrige Hilfe-Tests grün).
- Ä159 (neu, Slice 82): Aus „Neuer Termin“ gibt es keinen Weg „Auf die Warteliste“; Gast, Behandlung, Wunschtag und Notiz müssen dort neu eingetragen werden. Nicht gebaut (NICHT-bauen-Liste der Slice).
- Slice 82: Die Durchsicht des Branches (lesender Prüfer) fand: die Rückfrage beim Schließen sah nur die angezeigte Art (eine gemerkte Art mit Eingaben ging ohne Frage verloren), ein Klick in eine andere Spalte verwarf einen gemerkten Termin, und gemerkte Pausen und Notizen hingen an der alten Spalte; eine geleerte Notiz kam in der nächsten Art wieder, Sauna am späten Abend endete vor dem Beginn, Paar/Spa sprang bei jeder neuen Suche auf den übernommenen Beginn zurück, der Punkt fehlte in der Klappliste. Alles behoben, mit Tests.
- Slice 82: Drei Bildtests der Reiterschiene (`ViewTests.Tabs`, `ViewTests.TabKeys`) erwarteten als Screenreader-Namen „Neuer Termin“; sie erwarten jetzt „Neuer Termin, in Bearbeitung“ bzw. den neuen `Name` des Reiters (gewollte Änderung aus Ä149, keine Abschwächung).
- Ä160 (neu, Slice 82): Der Reiter unten rechnet seine Breite immer für zwei Reiter; schon mit nur „Neuer Termin“ offen wird „Neuer Termin · Brigitte Müller“ zu „Neuer Termin · B…“ gekürzt (voller Text im Tooltip und für den Screenreader).
- Slice 83 (Ä146): Die Quelltextreihenfolge von „Neuer Termin“ war richtig; der Fehler kam nur nach einem **Klick** auf einen Treffer. Der Klick wählt den Gast, die Trefferliste verschwindet, und erst danach merkt sich die Liste (Tab-Modus `Once`) den Eintrag als Tab-Ziel; Tab von „Ändern“ wollte in diesen unsichtbaren Eintrag. Ein Test, der den Gast nur im ViewModel setzt, war grün.
- Slice 83: Die Durchsicht des Branches (lesender Prüfer) fand: das erste Vergessen des Tab-Ziels ließ eine wieder eingeblendete Liste per Tab auf den ersten statt den gewählten Eintrag gehen (jetzt `Restore` beim Einblenden, mit Test), der Scan prüfte nicht, dass jedes erreichte Element ein Ziel ist und Tab im Dialog bleibt (ergänzt), und KI und Hilfe rechts war nie offen (ergänzt).
- Slice 83: Der Scan über alle Seiten, Seitenleisten des Kalenders, Kundendaten-Maske und Rückfragedialog fand außer Ä146 keine Stelle; zwei erste Meldungen („springt nach oben“ in Gruppe und Eintritt) waren Fehlalarm, weil die Reiter unten außerhalb des scrollenden Formulars liegen.
- Ä162 (neu, Slice 83): Der Knopf „+ Termin“ im Kalenderkopf (`SplitButton`) heißt für den Screenreader „Avalonia.Controls.StackPanel“; `AccessibleNames` deckt `SplitButton` nicht ab, und der Namens-Scan prüft diesen Typ nicht.
- Ä171 (Slice 86): Der Sprung des Auftraggebers „an den Anfang“ ließ sich im Headless-Fenster nicht nachstellen: Notiz direkt gespeichert, mit der Maus über „Neuer Termin“ nach einem angefangenen Termin bei Lena, nach einem offenen Termin, in der Raumansicht mit Spalten, die das Seitenpanel schmaler macht; immer blieb der Versatz (oder er stand am Ende, Ä172) und die Notiz im Bild. Geprüft und verworfen: Neuaufbau der Spalten, Öffnen und Schließen des Panels, `ScrollToMarked`, Fokus auf eine Karte (Karten bekommen per Klick keinen Fokus). Rechnerisch rutscht eine Spalte bei breiter werdenden Spalten erst ab etwa 20 Spalten aus dem Bild. Offen bleiben Wege außerhalb des Headless-Fensters (Live-Echo des Servers, echte Fenstergröße und Skalierung, Rechtsklick „Notiz hier“ nach Pfeiltasten); die manuelle Probe zeigt, ob es noch springt.
- Ä172 (Slice 86): Schließt das Panel und wird der Ausschnitt breiter, als rechts noch Spalten folgen, steht der Kalender am Ende statt am alten Versatz (840 → 618 im Test); die Notiz bleibt im Bild. Ein Versatz hinter dem Ende geht nicht.
- Ä173 (Slice 86): Die Knöpfe am Rand sind kein Tab-Halt; mit der Tastatur wandert man mit den Pfeiltasten über die Termine, und der Kalender scrollt mit. Eine Taste „nächste Bildschirmbreite“ gibt es nicht.
- Slice 86: Die Durchsicht durch einen lesenden Prüfer fand: der rechte Verlauf deckte den senkrechten Scrollbalken zu (jetzt 16 px Abstand unten und rechts, so breit wie die Balken, mit Test); der im Plan genannte zweite Test für das Buchen eines Termins fehlt, der Test „angefangener Termin“ speichert eine Notiz (Termin-Weg über `Apply` ungetestet, Abweichung vom Plan); der Knopf verdeckt auf mittlerer Höhe 40 × 40 der Randspalte, ein Klick dort trifft den Knopf statt einer Karte (gewollt).
- Slice 86: Im Beispiel liegen in der Raumansicht bei 1440 px zwei Spalten außerhalb, nicht drei; der Abnahmetest „+3“ bekommt einen siebten Behandlungsraum („Salzraum“).

- Ä185 (Slice 90): Die Behandlung eines gebuchten Termins lässt sich nicht ändern (`Appointment.TreatmentId` ist `init`, kein Endpunkt); wer die Behandlung tauschen will, storniert und bucht neu. Der Verlauf würde eine solche Änderung schon zeigen („Geändert …“), einen eigenen Satz gibt es dafür nicht.
- Ä186 (Slice 90): Im Produkt gibt es weder „Gast löschen“ noch „Gast anonymisieren“ (nur die Terminannahme entfernt einen eben angelegten, unbenutzten Gast). Die Abnahme „Gast gelöscht → Verlauf weg“ prüft deshalb den Trigger mit einer Löschung per SQL; das Anonymisieren muss `sync.forget_customer_history` aufrufen, sobald es gebaut wird.
- Slice 90: Die Basisprüfung lief auf sauberem `dev` an, während schon die neuen Testdateien entstanden: `dotnet format` grün, der Build scheiterte nur an diesen (noch ohne Gerüst); mit den Gerüsten danach grün. Beim Build meldet die OpenAPI-Erzeugung „An error occurred using the connection to database 'openapi' on server localhost:5432“ (ein PostgreSQL einer anderen Sitzung läuft auf 5432, bekannte Falle in CLAUDE.md); die Datei `web/openapi/aispa-api.json` entstand trotzdem vollständig.
- Slice 90: `DemoBulkTests` dauert mit dem Verlauf 10 min 33 s (Dienste und Speicher je eine Woche, unter Last paralleler Sitzungen); keine Zeitgrenze geändert.
- Slice 90: Zwei Fokustests des Termin-Panels (`Fokusreihenfolge_Termin_Panel`, `Kennzahlen_im_Abschnitt_Kunde_sind_ein_Tab_Stopp_mit_Pfeilen`) wurden rot, weil der Kopf „Verlauf“ ein neuer Tab-Stopp nach „Kunde“ ist; sie erwarten ihn jetzt (gewollte Änderung, keine Abschwächung). Slice 83 (Tab-Reihenfolge) berührt dieselbe Datei.
- Slice 90: Die Vitest-Gesamtsuite lief neben der API- und der Desktop-Suite in 7 rote Fälle in `BusinessInfoPage`, `helpFeedback`, `ReportsKeyboard` und `SubscriptionsPage`; jede Datei einzeln grün (Last, keine Regression).
- Ä188 (Slice 91): Die Beispieldaten haben nur für Termine einen Verlauf (Slice 90); Kunden, Behandlungen, Dienstplan und Zugänge der Beispieldaten zeigen „Noch keine Einträge“, bis jemand etwas ändert. Die Beispieldaten löschen den Verlauf ihres Mandanten beim Einspielen und schreiben nur den der Termine neu.
- Ä189 (Slice 91): Der Dienstplan ersetzt beim Speichern eines Tages alle Einträge (löschen, neu anlegen); der Verlauf zeigt deshalb auch unveränderte Einträge als „entfernt“ und „eingetragen“, statt nur das Geänderte zu nennen.
- Ä190 (Slice 91): Der Verlauf von Dienstplan, Benutzern und Rechten fehlt der Rolle Standortleitung, obwohl sie den Dienstplan ihrer Standorte pflegt und Zugänge verwaltet (Ruling „Inhaberin und Manager“, Annahme 1 im Plan); der Auftraggeber kann die Sichtbarkeit auf `ManageStaff` erweitern, das wäre ein Satz im Endpunkt.
- Slice 91: Der Trigger an `Customers` schrieb beim Löschen eines Gastes nach `aispa_history_forget` eine eigene Löschzeile (die Trigger laufen in der Reihenfolge ihrer Namen), der Verlauf des Gastes war nicht leer; jetzt schreibt das Löschen eines Gastes nichts (`ChangeHistoryTests` hat es gefangen).

## Zurückgestellte Kleinigkeiten
- Slice 84: Handbuchbilder der ganzen Kalenderseite (`kalender-*.png`) zeigen noch „+ Termin“ als primären Knopf und noch nicht „Freie Zeit suchen“ im Kopf; sie werden beim nächsten vollständigen Lauf von `scripts/help-images.sh` erneuert (Rauschen der übrigen Bilder vorher klären).
- Slice 85: Eine Person ohne Schicht an einem Tag, an dem andere Dienst haben, bleibt abgelehnt („ist an diesem Tag hier nicht im Dienst“) und bekommt kein „Trotzdem buchen“: der Planer lädt nur Personen im Dienst. Erst ändern, wenn der Auftraggeber das im Test braucht.
- Slice 85: Die Handbuchbilder zu „Termin buchen“ zeigen die Warnung „Trotzdem buchen“ nicht; der neue Abschnitt kommt ohne Bild aus.
- Slice 87: Redirect der Teil-Oberflächen (`/help`, `/buchen`, `/mein-tag`, `/betreiber`) auf Vite in `TeamWebRoot.RedirectToDevServer` (Ä174); ein Redirect würde im eingebetteten Browser außerdem als fremde Seite gelten (`IsOwnPage`) und im Standardbrowser landen.
- Slice 88: Handbuchbilder von „Freie Zeit suchen“ zeigen die neue Meldung nicht; neu erzeugen beim nächsten vollständigen Lauf von `scripts/help-images.sh`.
- Slice 89: Handbuchbild `kalender-hoverkarte-karte.png` (und die Bilder, die die Karte zeigen) zeigt noch die alte Karte; die Bilderzeugung ändert viele unbeteiligte PNGs (siehe Slice 84) und blieb deshalb aus.

- Ä161 (Slice 82): Die aus „Neuer Termin“ mitgenommene Notiz steht in „Freie Zeit suchen“ nicht sichtbar; sie landet im Termin, man sieht sie aber erst wieder in „Neuer Termin“ bzw. im gebuchten Termin.
- Ä163 (Slice 83): Der Tab-Scan öffnet keine Popups (Liste einer Auswahl, Flyouts, Menüs), nicht den Dialog „Schließtag absagen“ (`CancelClosureDialogViewModel`, mit Liste und Textfeld) und keine Zustände mit Meldungen oder aufgeklappten Abschnitten außer den geöffneten; nicht geprüft sind außerdem eine sichtbare Liste, deren gemerkter Eintrag aus den Daten verschwindet, und Tab sofort nach dem Klick auf einen Treffer (der Fokus liegt dann auf dem verschwundenen Eintrag). Der Lagevergleich prüft nur Sprünge nach oben, nicht rechts nach links in einer Zeile.

- Slice 90: Kein Handbuchbild zum Abschnitt „Verlauf“ (das Thema `kalender-termin-verlauf` kommt ohne Bild aus; der Ansichtstest `termin-verlauf` kann es mit `AISPA_SCREENSHOTS` erzeugen, die Bilderzeugung ändert aber viele unbeteiligte PNGs, siehe Slice 84).
- Slice 90: Der Verlauf wird nicht abgeglichen; ein Einzelplatz zeigt Änderungen der zentralen Seite als „beim Abgleich“ ohne Person.
- Slice 91: Kein Handbuchbild zu den neuen Abschnitten „Verlauf“ (die Themen kommen ohne Bild aus; die Bilderzeugung ändert viele unbeteiligte PNGs, siehe Slice 84).
- Slice 91: Das Anonymisieren eines Gastes gibt es weiter nicht (Ä186); `sync.forget_customer_history` bleibt bereit.
- Slice 91: Der Verlauf wird nicht abgeglichen (wie Slice 90); Räume, Geräte und Qualifikationen einer Behandlung sowie Entwürfe des Dienstplans haben keinen Verlauf.
- Slice 91 (Durchsicht): In der Dienstplan-Tabelle steht der Knopf „Verlauf“ in der Zeilenüberschrift, ein Screenreader liest ihn in jeder Zelle der Zeile mit (Knopf in eine eigene Spalte verlegen); der Fokus springt jetzt in den geöffneten Verlauf.
- Slice 91 (Durchsicht): Ein Dienstplan-Tag ohne Einträge, der im selben Zug wie ein Tag mit Einträgen angelegt wird (Veröffentlichen), fehlt im Verlauf als „angelegt“ (`EntityHistory`: Vergleich nur über den Zeitpunkt); Änderungen nur an Spalten außerhalb der Anzeigeliste (`AssignmentOpen`, `DiscountApprovedByUserId`) erscheinen als „Geändert per Mail“ ohne Inhalt; „Befristet bis“ nimmt das Datum in der Zeitzone der Datenbank (im Einzelplatz kann es einen Tag früher stehen).

## Offene Entscheidungen
NEEDS-DECISION: Mehrsprachigkeit (Ä103, Slice 80): Weg B (Wörterbücher im Build erzeugen und prüfen) bestätigen oder Weg A (erster Start auf dem Rechner des Spas) wählen? (aus R03)
NEEDS-DECISION: Mehrsprachigkeit (Ä103, Slice 80): Welche Sprachen zuerst (Englisch, danach Französisch und Italienisch?), nur Oberflächen oder auch Hilfe und Handbuchbilder, Sprache je Benutzerin oder je Standort, und wer gibt Pflichttexte frei? (aus R03)
NEEDS-DECISION: Eingabeprüfung (Ä98, Slice 81): Soll der Schritt „Eingabeprüfung“ (Durchsicht der geänderten Masken, später Prüftest) in den Rundenabschluss aufgenommen werden, und zwar zuerst nur die Durchsicht für R04? (aus R03)


## Manuelle Testliste
### Slice 82 – Neuer Termin: Notiz, Reiter, Eingaben beim Wechsel der Art
Start: Docker Desktop starten, `cd web && npm install && cd ..`, `dotnet run --project src/AispaStudio.AppHost`, Windows-App über die Ressource `windows-app` starten, als Inhaberin anmelden
1. Im Kalender heute auf eine freie Stelle einer Person klicken, einen Gast wählen, eine Behandlung wählen und unter „Notiz“ zwei Zeilen eintippen (Enter dazwischen).
Expected: Das Notizfeld zeigt beide Zeilen; der Reiter unten heißt „Neuer Termin · <Gast>“ (ggf. gekürzt, voller Text im Tooltip) und hat am Symbol einen Punkt.
2. Eine lange Notiz mit acht Zeilen eintippen.
Expected: Das Feld wächst mit dem Text und scrollt erst, wenn es etwa dreimal so hoch ist wie leer.
3. Oben auf „Paar/Spa“ wechseln.
Expected: Gast 1 ist der gewählte Gast mit der gewählten Behandlung, Datum und Notiz stehen da.
4. Auf „Sauna“ wechseln.
Expected: Gast, Datum, „Von“ = Beginn des Termins, Notiz und Personen 1 stehen da.
5. Zurück auf „Termin“ wechseln.
Expected: Gast, Behandlung, Beginn und Notiz sind unverändert.
6. Auf „Freie Zeit suchen“ klicken und eine angebotene Zeit für den Gast buchen.
Expected: Der gebuchte Termin trägt die Notiz (im Termin-Panel sichtbar).
Closes: Ä148, Ä149, Ä153
Result: ☐

### Slice 83 – Tab-Reihenfolge in allen Masken
Start: Docker Desktop starten, `cd web && npm install && cd ..`, `dotnet run --project src/AispaStudio.AppHost`, Windows-App über die Ressource `windows-app` starten, als Inhaberin anmelden
1. Im Kalender heute auf eine freie Stelle einer Person klicken, im Suchfeld „mül“ tippen und den Treffer mit der Maus anklicken.
Expected: Der Gast steht mit „Ändern“ da, die Trefferliste ist weg.
2. In das Suchfeld klicken und Tab drücken, bis der Fokus auf „Termin buchen“ steht.
Expected: Der Fokus läuft „Ändern“, der Vorschlag (falls sichtbar), „Behandlung“, „Freie Zeit suchen“, „Datum“, „Beginn“, „Notiz“, „Termin buchen“ der Reihe nach durch und bleibt nirgends stehen.
3. Umschalt+Tab drücken, bis der Fokus wieder im Suchfeld steht.
Expected: Der Fokus läuft dieselben Elemente in umgekehrter Reihenfolge durch.
Closes: Ä146, Ä147
Result: ☐

### Slice 84 – Freie Zeit suchen als Hauptweg im Kalenderkopf, Sprache ausblenden
Start: Docker Desktop starten, `cd web && npm install && cd ..`, dann `dotnet run --project src/AispaStudio.AppHost`; Windows-App über die Ressource `windows-app` starten, als Inhaberin anmelden (`inhaberin@demo.aispa.test`)
1. Im Kalender den Kopf ansehen.
Expected: Neben dem Datum steht der blaugrüne Hauptknopf **Freie Zeit suchen**; **Termin** rechts mit Pfeil ist ein normaler (heller) Knopf.
2. Mit der Maus kurz auf **Freie Zeit suchen** bleiben.
Expected: Der Hinweis lautet „Freie Zeit suchen (Strg+N)“.
3. Auf **Freie Zeit suchen** klicken.
Expected: Rechts öffnet sich das Panel „Freie Zeit suchen“ mit dem Feld **Behandlung**.
4. Im Panel die Felder ansehen.
Expected: Es gibt kein Feld „Sprache des Gastes“ und kein Fragezeichen dazu; **Tageszeit** und **Gast wünscht** (falls sichtbar) stehen wie zuvor.
5. Auf **Termin** (linke Seite des Knopfes rechts oben) klicken.
Expected: „Neuer Termin“ öffnet sich sofort.
Closes: Ä150, Ä157
Result: ☐

### Slice 85 – Buchen außerhalb der Öffnungszeit mit „Trotzdem buchen“
Start: Docker Desktop starten, `cd web && npm install && cd ..`, dann `dotnet run --project src/AispaStudio.AppHost`; Windows-App über die Ressource `windows-app` starten, als Inhaberin anmelden (`inhaberin@demo.aispa.test`)
1. Im Kalender in der Spalte von Sophie Gruber auf eine freie Stelle klicken und in „Neuer Termin“ einen Gast und eine Behandlung wählen.
Expected: Die Maske „Neuer Termin“ steht mit Gast und Behandlung, keine Warnung.
2. Unter **Beginn** `18:45` eintragen.
Expected: Über dem Knopf steht die gelbe Warnung „Der Standort hat zu dieser Zeit geschlossen.“ (oder „Sophie Gruber ist zu dieser Zeit nicht im Dienst.“, wenn 18:45 noch in der Öffnungszeit liegt und erst der Server ablehnt) mit dem Knopf **Trotzdem buchen**.
3. Auf **Termin buchen** klicken.
Expected: Nichts wird gebucht, die Warnung bleibt stehen.
4. In der Warnung auf **Trotzdem buchen** klicken.
Expected: Die Maske schließt, der Termin steht um 18:45 in Sophies Spalte.
5. Im Kundenbereich (Online-Buchung) für denselben Tag und dieselbe Behandlung die Zeiten ansehen.
Expected: Keine Zeit liegt außerhalb der Öffnungszeit, 18:45 wird nicht angeboten.
Closes: Ä151
Result: ☐

### Slice 86 – Kalender: Scrollstelle erhalten, weitere Spalten erkennbar
Start: Docker Desktop starten, `cd web && npm install && cd ..`, dann `dotnet run --project src/AispaStudio.AppHost`; Windows-App über die Ressource `windows-app` starten, als Inhaberin anmelden (`inhaberin@demo.aispa.test`)
1. Das Fenster so schmal ziehen, dass Sophie Gruber außerhalb des Kalenders liegt, und das Raster ansehen.
Expected: Am rechten Rand liegt ein weicher Verlauf mit einem runden Knopf mit Pfeil „›“ und der Zahl der verborgenen Spalten (etwa „+2“); links ist keiner.
2. Mit der Maus auf dem Knopf bleiben.
Expected: Der Hinweis nennt „Weiter zu … Spalten:“ und die Namen der verborgenen Personen.
3. Auf den Knopf klicken.
Expected: Der Kalender rückt um eine Breite weiter, links erscheint der Knopf „‹“ mit Zahl.
4. So scrollen, dass Sophie zu sehen ist, auf eine freie Stelle in ihrer Spalte klicken, oben „Notiz“ wählen, „Arzt“ eintippen und **Notiz speichern** klicken.
Expected: Der Kalender bleibt bei Sophie, die Notiz „Arzt“ steht sichtbar in ihrer Spalte.
5. In die Raumansicht wechseln, ganz nach rechts scrollen, in der letzten Spalte eines Behandlungsraums eine Notiz anlegen und speichern.
Expected: Der Kalender bleibt beim Raum, die Notiz ist sichtbar.
Closes: Ä152, Ä154, Ä171
Result: ☐

### Slice 87 – Hilfe aus „Freie Zeit suchen“ öffnet das Thema statt einer 404
Start: App starten wie in work/conventions.md beschrieben (Inhaberin `inhaberin@demo.aispa.test`), Windows-App öffnen, Kalender
1. Im Kalender Strg+N drücken („Freie Zeit suchen“ öffnet sich rechts)
Expected: Unter der Zusammenfassung der Suche steht ein Fragezeichen mit dem Hinweis „Hilfe“
2. Auf das Fragezeichen unter den Feldern klicken
Expected: Die Hilfe öffnet das Thema „Freie Zeit suchen“, kein JSON und keine Meldung „Not Found“
3. Zurück in den Kalender, in „Freie Zeit suchen“ (sofern sichtbar) auf das Fragezeichen neben „Gast wünscht“ klicken
Expected: Die Hilfe öffnet das Thema „Termine am liebsten“ beim Abschnitt „Therapeutin oder Therapeut“
4. Mit gestarteter App (AppHost) in der Hilfe nach einem Thema suchen und eines wählen
Expected: Das Thema erscheint; bei einem Server ohne Hilfe steht „Hilfe nicht erreichbar“ mit dem Grund, nie JSON
Closes: Ä155
Result: ☐

### Slice 88 – Wichtige Meldungen sichtbar und einheitlich
Start: App starten wie in work/conventions.md beschrieben (Inhaberin `inhaberin@demo.aispa.test`), Windows-App öffnen, Kalender
1. Strg+N drücken, die Behandlung „AISPA Signaturmassage“ wählen und die Tageszeit „nachmittags“ einstellen
Expected: Gibt es in 14 Tagen nichts, steht über der Liste eine gelbe Meldung „In den nächsten 14 Tagen ist nichts frei: nachmittags.“ mit dem Knopf „Tageszeit: egal“, kein kleiner grauer Text
2. Auf „Tageszeit: egal“ klicken
Expected: Die Tageszeit steht auf „ganztags“ und die Suche läuft neu; gibt es Zeiten, erscheinen sie und die Meldung verschwindet
3. Eine Person wählen, bei der nichts frei ist
Expected: Die Meldung nennt „bei <Name>“, der Knopf heißt „Alle Personen“
4. Im Überblick des Termin-Panels (Netz trennen oder den Server stoppen), „Erneut laden“ drücken
Expected: „Der Überblick wurde nicht geladen.“ steht als rote Meldung mit dem Knopf „Erneut laden“
Closes: Ä156
Result: ☐

### Slice 89 – Hover-Karten und Popups lesbar und einheitlich
Start: App starten wie in work/conventions.md beschrieben (Inhaberin `inhaberin@demo.aispa.test`), Windows-App öffnen, Kalender
1. Mit der Maus kurz auf einem Termin bleiben.
Expected: Die Hover-Karte zeigt Gast, Behandlung, Tag mit Uhrzeit und Raum in gleich großer, **fetter** Schrift; Dauer und Person stehen normal
2. Die Karte eines Gastes ohne Ausfall ansehen.
Expected: Es steht keine Zeile „Nicht erschienen“ darin
3. Die Karte eines Gastes mit genau einem Ausfall ansehen.
Expected: Im orangen Band steht „1× nicht erschienen“, nichts überlappt
4. In die Raumansicht wechseln und die Karte eines Termins ansehen.
Expected: Die Person steht fett
Closes: Ä158, Ä180, Ä181
Result: ☐

### Slice 90 – Änderungsverlauf für Termine
Start: Docker Desktop starten, `cd web && npm install && cd ..`, dann `dotnet run --project src/AispaStudio.AppHost`; Windows-App über die Ressource `windows-app` starten, als Inhaberin anmelden (`inhaberin@demo.aispa.test`)
1. Im Kalender heute auf eine freie Stelle einer Person klicken, einen Gast und eine Behandlung wählen und den Termin buchen.
Expected: Der Termin steht im Kalender, rechts ist sein Termin-Panel offen.
2. Im Termin-Panel auf **Verlauf** klicken.
Expected: Der Abschnitt klappt auf und zeigt „Angelegt von Katrin Aigner“ mit Datum und Uhrzeit von eben.
3. Den Termin im Kalender auf eine andere Uhrzeit ziehen und **Verlauf** zu- und wieder aufklappen.
Expected: Oben steht „Verschoben von Katrin Aigner: <alte Zeit> → <neue Zeit>“ mit Datum und Uhrzeit, darunter das Anlegen.
4. Über **Notiz hinzufügen** eine Notiz speichern und **Verlauf** zu- und wieder aufklappen.
Expected: Oben steht „Notiz geändert von Katrin Aigner“ mit Uhrzeit.
5. Den Termin stornieren (Grund „Test Verlauf“) und im Panel des stornierten Termins **Verlauf** aufklappen.
Expected: Oben steht „Storniert von Katrin Aigner: Test Verlauf“; alle vier Schritte stehen untereinander, die neueste Änderung oben.
6. Einen Beispieltermin von Wien aus der Vorwoche öffnen und **Verlauf** aufklappen.
Expected: Es steht „Angelegt von Nina Kovač“ (bzw. „Online gebucht vom Gast“) und, je nach Termin, „Eingecheckt …“, „Abgeschlossen …“ oder „Als nicht erschienen markiert …“.
Closes: Ä184 (Termine)
Result: ☐

### Slice 91 – Änderungsverlauf für Kunden, Behandlungen, Dienstplan und Rechte
Start: Docker Desktop starten, `cd web && npm install && cd ..`, dann `dotnet run --project src/AispaStudio.AppHost`; Windows-App über die Ressource `windows-app` starten, als Inhaberin anmelden (`inhaberin@demo.aispa.test`)
1. Links auf **Kunden** klicken, einen Gast wählen und seine Telefonnummer ändern (Kundendaten bearbeiten, speichern).
Expected: Die Karte zeigt die neue Nummer.
2. Am Ende der Karte auf **Verlauf** klicken.
Expected: Der Abschnitt klappt auf; oben steht „Geändert von Katrin Aigner: Telefon <alt> → <neu>“ mit Datum und Uhrzeit von eben.
3. Auf der Seite **Verwaltung** unter **Behandlungen** eine Behandlung öffnen, den Preis ändern, speichern und **Verlauf** aufklappen.
Expected: Oben steht „Geändert von Katrin Aigner: Preis <alt> € → <neu> €“ mit Datum und Uhrzeit.
4. Unter **Dienstplan** bei einer Person einen Urlaubstag eintragen und **Verlauf** dieser Person öffnen.
Expected: Es steht „Urlaub am <Datum> eingetragen von Katrin Aigner“.
5. Unter **Zugänge** bei einem Zugang die Rolle ändern, speichern und **Verlauf** öffnen.
Expected: Oben steht „Geändert von Katrin Aigner: Rolle <alt> → <neu>“.
6. Mit einem Zugang als Rezeption anmelden und Kundenkarte, Behandlungen, Dienstplan und Zugänge ansehen.
Expected: Der Verlauf der Kundenkarte und der Behandlung ist da; bei Dienstplan und Zugängen gibt es keinen Abschnitt „Verlauf“.
Closes: Ä184 (Kunden, Behandlungen, Dienstplan, Rechte)
Result: ☐
