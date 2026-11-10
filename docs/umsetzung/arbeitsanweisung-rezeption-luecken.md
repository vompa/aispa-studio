# Arbeitsanweisung: Lücken aus dem Rezeptions-Onboarding schließen

**Für:** Claude Code im Repository `vompa/aispa-studio`
**Stand:** 08.11.2026, Analyse in [live-tests-rezeption-onboarding.md](live-tests-rezeption-onboarding.md), Abschnitt 3 (Lücken L1 bis L16)
**Ablauf:** Code Guardian nach [docs/arbeitsweise.md](../arbeitsweise.md) Abschnitt 9; eine Sitzung arbeitet die Aufgaben nacheinander in einem Worktree ab, ein Squash-Commit je Aufgabe, **kein Push, kein PR** (das entscheidet der Auftraggeber nach der Fertigmeldung).

---

## 1. Ziel

Die Rezeption soll an den Stellen, die im Onboarding-Bericht als Fehlerquellen im Alltag auffielen, von der App gebremst oder gewarnt werden, statt den Fehler erst an der Kassa oder beim Gast zu merken. Alles hier sind Regeln, Rückfragen, Hinweise und Texte an bestehenden Masken; **keine neue Maske, kein neues Modul.**

**Erfolg ist erreicht, wenn:**

1. Ein Termin lässt sich nur am Tag des Termins einchecken; ein Termin ohne Einchecken oder Erledigt kommt nicht auf eine Rechnung, auch nicht über die Kassa-Liste (L1).
2. Ein Termin lässt sich nicht auf einen vergangenen Tag verschieben (L2).
3. Der Storno-Dialog sagt, wenn die Absage innerhalb der Stornofrist liegt und welche Ausfallgebühr die Regel des Standorts vorsieht (L3).
4. Krank melden, Pause löschen und Sperrzeit löschen fragen nach (L10).
5. Beim Buchen für einen Gast ohne Telefon und E-Mail steht ein Hinweis im Buchungsbereich (L5).
6. Die Rezeption sieht den Tab „Entnahme“ nicht mehr (L6); Buchen aus dem Panel meldet „Gebucht: …“ (L11); V öffnet die Verschieben-Karte und steht in der Tastenhilfe (L16).
7. Das Handbuch stimmt an den sechs Stellen aus L15 mit der App überein.
8. Alle bestehenden Tests bleiben grün; jede neue Regel hat einen Test, der ohne die Regel rot ist.

**Nicht Teil dieser Anweisung** (Entscheidung des Auftraggebers steht aus): L4 Gutscheinsuche, L7 Teilretoure, L8 Abschlussliste ohne Beträge, L9 Anamnese-Link senden, L12 Terminbestätigung drucken oder senden, L13 Dubletten zusammenführen, L14 Pakete beim Buchen.

## 2. Leitplanken für alle Aufgaben

- `CLAUDE.md` gilt vollständig: englische Namen im Code, Deutsch in Oberfläche, Doku und Commits; Warnungen sind Fehler; Texte nach [docs/gestaltung/texte.md](../gestaltung/texte.md), Meldungen nach [docs/gestaltung/meldungen.md](../gestaltung/meldungen.md) (Toast ohne Schlusspunkt, höchstens 60 Zeichen; Warnung und Fehler nennen Problem und Weg).
- **Chirurgisch:** jede geänderte Zeile lässt sich auf eine Lücke zurückführen. Keine Umbauten nebenher. Toten Code melden, nicht löschen.
- **Server zuerst, Oberfläche folgt:** Eine Regel, die Daten schützt (L1, L2), sitzt am Endpunkt der API wie `PastBookings` (`src/AispaStudio.Api/Scheduling/PastBookings.cs`), nicht im `BookingService`, damit Beispieldaten und Planer weiter in der Vergangenheit rechnen dürfen. Die Windows-App spiegelt die Regel (Knopf fehlt oder ist gesperrt mit Grund im Tooltip), damit die Meldung des Servers der Ausnahmefall bleibt.
- **Nicht anfassen:** `tests/AispaStudio.LiveTests` und `tests/AispaStudio.LiveTests.Launcher` (parallel in Arbeit in einer anderen Sitzung). Nötige Folgen dort nur im PR-Protokoll vermerken.
- **Prüfen** nach Abschnitt 9 „Prüfen in der Aufgabe“: Basisprüfung (`dotnet format … --verify-no-changes`, `dotnet build … -c Release`), die zuständigen Testklassen der API (`tests/AispaStudio.Api.Tests`) und der Windows-App (`tests/AispaStudio.Desktop.Tests`), bei API-Änderungen `cd web && npm run gen:api`. Keine vollen Suiten.
- **Abschluss je Aufgabe** nach Abschnitt 9 „Abschluss einer Aufgabe“: PR-Protokoll-Abschnitt (`docs/umsetzung/pr-protokoll.md`) mit Auftrag, Was, Detailentscheidungen, Geprüft, Nicht geprüft; Handtestblock in `docs/umsetzung/handtests.md`; Handbuch im selben Commit, wo sich ein Ablauf ändert ([ADR 0056](../entscheidungen/0056-handbuch-mit-ablaeufen-und-bildern-als-grundlage-der-ki-hilfe.md); Bilder nur neu erzeugen, wo sich das Bild sichtbar ändert, sonst im PR-Protokoll vermerken). Im Bericht [live-tests-rezeption-onboarding.md](live-tests-rezeption-onboarding.md), Abschnitt 3, die Zeile der Lücke mit „erledigt in T-NNN“ ergänzen.
- **Fragen:** Bei einer offenen Entscheidung nicht raten, sondern die Frage mit den Optionen und einer Empfehlung an die beauftragende Sitzung stellen und weiterarbeiten, wo es ohne Antwort geht.

## 3. Aufgaben im Überblick

| Nr. | Aufgabe | Lücke | Modell | Hängt ab von |
|---|---|---|---|---|
| A | Verschieben nicht in die Vergangenheit | L2 | sonnet | – |
| B | Einchecken nur am Tag des Termins; Rechnung nur für eingecheckte oder erledigte Termine | L1 | opus | A |
| C | Storno-Dialog mit Frist und Ausfallgebühr | L3 | opus | – |
| D | Rückfragen für Krank melden, Pause löschen, Sperrzeit löschen | L10 | sonnet | – |
| E | Hinweis „Keine Kontaktmöglichkeit“ beim Buchen | L5 | sonnet | – |
| F | Kalender-Kleinigkeiten: Toast beim Buchen, Taste V, Automation-Namen | L11, L16 | sonnet | A, B (gemeinsame Dateien) |
| G | Kassa: Tab „Entnahme“ nur mit Kassenaufsicht | L6 | sonnet | B (gemeinsame Dateien) |
| H | Handbuch an die App angleichen | L15 | sonnet | A bis G (beschreibt den Endstand) |

Reihenfolge in **einer** Sitzung: A, B, C, D, E, F, G, H. „Modell“ ist das Modell für die Umsetzung der Aufgabe: Opus-Aufgaben macht die Sitzung selbst, Sonnet-Aufgaben kann sie an einen Subagenten mit Modell Sonnet geben und prüft das Ergebnis (Diff lesen, Tests selbst laufen lassen). Dateien, die mehrere Aufgaben berühren (`CalendarEndpoints.cs`, `AppointmentViewModel.cs`, `CalendarView.axaml`, `CalendarViewModel.Keyboard.cs`, `PosView.axaml`): nie zwei Aufgaben zugleich.

---

## Aufgabe A: Verschieben nicht in die Vergangenheit (L2)

**Befund:** Buchen auf einen vergangenen Tag lehnt die API mit 409 ab (`PastBookings`, `CalendarEndpoints.cs:188`, auch Gruppenbuchung `:370` und Sauna `:424`). Verschieben (`CalendarEndpoints.cs:210-217`) prüft das nicht; die Verschieben-Karte der App (`Views/CalendarView.axaml:185-215`) hat kein `Minimum` am Tag-Feld, anders als `DateField Minimum=Today` beim Buchen.

**Lösung:**

1. API: Am Verschieben-Endpunkt dieselbe Prüfung wie beim Buchen (`PastBookings`, Zieltag vor dem heutigen Kalendertag des Standorts → 409 mit `PastBookings.Message` oder einem eigenen Text „Termine lassen sich nicht in die Vergangenheit verschieben. Bitte heute oder einen späteren Tag wählen.“). Auch „Ganze Buchung verschieben“ (Gruppe), falls ein eigener Endpunkt. Die Uhrzeit am heutigen Tag bleibt frei (wie beim Buchen).
2. Windows-App: Tag-Feld der Verschieben-Karte mit `Minimum` = heute; „−1 Tag“ gesperrt, wenn das Ziel vor heute läge; Hauptknopf „Verschieben“ gesperrt mit Tooltip „Verfügbar für heute oder einen späteren Tag.“ Ziehen per Maus und Alt+Pfeile bleiben im Tag und sind nicht betroffen. Die Serverantwort 409 erscheint wie heute als InfoBar „Nicht verschoben“.

**Tests:** API: Verschieben auf gestern → 409, Verschieben auf heute und morgen → 200 (Klasse der Kalender-Endpunkte, Muster `PastBookings`-Tests). Desktop: `AppointmentViewModel` mit Zieltag gestern → `CanMove` false (ViewModel-Test, ohne Fenster).

---

## Aufgabe B: Einchecken nur am Tag des Termins; Rechnung nur für eingecheckte oder erledigte Termine (L1)

**Befund:**
- `POST /appointments/{id}/status` (`CalendarEndpoints.cs:289-306`) und `BookingService.ChangeStatusAsync` (`BookingService.cs:404-445`) prüfen kein Datum. Die App zeigt „Einchecken“ bei jedem gebuchten Termin (`AppointmentViewModel.cs:118`), auch an künftigen Tagen. Für „Behandlung begonnen“ gibt es die Regel schon („Begonnen lässt sich nur am Tag des Termins melden.“, `BookingService.cs:470`).
- Die Kassa nimmt jeden nicht stornierten, nicht abgerechneten Termin auf die Rechnung (`PosService.cs:401-414`). Die App erlaubt „Kassieren“ im Kalender nur bei Eingecheckt oder Erledigt (`AppointmentViewModel.cs:408`), die Kassa-Liste „Heute abzurechnen“ bietet unter „Später heute“ aber auch nicht eingecheckte Termine an (`PosDayList.cs:30`).

**Lösung:**

1. API, Status: Am Endpunkt `/status` (nicht im Service, siehe Leitplanken): `CheckedIn` nur, wenn der Termin am heutigen Kalendertag des Standorts liegt → sonst 400 „Einchecken geht nur am Tag des Termins.“ `Completed` und `NoShow` nur für heute oder vergangene Tage → sonst 400 „Erledigt und Nicht erschienen lassen sich erst am Tag des Termins setzen.“ „Alle einchecken“ der Gruppe folgt derselben Regel. Zeitzone und „heute“ wie in `PastBookings` und `BeginAsync`.
2. API, Kassa: Termin auf die Rechnung nur bei Status `CheckedIn` oder `Completed`, sonst 409 „Der Gast ist noch nicht eingecheckt. Zuerst im Kalender einchecken.“ (`PosService.cs`, bei der bestehenden Prüfung auf storniert und schon abgerechnet). Eintritte (Sauna) sind nicht betroffen.
3. Windows-App, Kalender: „Einchecken“ (Hauptknopf, Taste E, Rechtsklick, Unterleiste) nur am Tag des Termins; an anderen Tagen fehlt der Knopf und die Unterleiste nennt den Grund („Einchecken: nur am Tag des Termins“), wie heute gesperrte Einträge ihren Grund nennen (`CalendarViewModel.Keyboard.cs`, Unterleiste). „Erledigt“ und „Nicht erschienen“ hinter „Mehr“ an künftigen Tagen gesperrt mit Hinweis „Verfügbar ab dem Tag des Termins.“
4. Windows-App, Kassa-Liste: Zeilen nicht eingecheckter Termine unter „Später heute“ bleiben sichtbar (die Rezeption soll sehen, wer heute noch kommt), tragen aber den Zusatz „noch nicht eingecheckt“ im `AccessibleName` und im Text. Ein Klick darauf öffnet den bestehenden Bestätigungsdialog (`ConfirmDialogView`) „Gast ist noch nicht eingecheckt. Einchecken und kassieren?“ mit „Einchecken und kassieren“ und „Abbrechen“; Bestätigen setzt den Status über den vorhandenen Status-Aufruf und legt den Termin dann auf die Rechnung. Ohne Verbindung: kein Dialog, Hinweis wie heute, dass Kassieren Verbindung braucht.
5. Beispieldaten: Der Verlauf setzt Status über den Service bzw. im Speicher, nicht über den Endpunkt; prüfen, dass `DemoBulkTests` und `DemoSeederTests` grün bleiben. Der Weg `DemoData:Mode=Services` läuft nicht in der CI; im PR-Protokoll vermerken, ob er geprüft wurde.
6. ADR 0090: Nachtrag mit zwei Sätzen: Der Knopf heißt „Einchecken“; mit dieser Aufgabe gilt die Tagesregel wirklich, ein Live-Test des Kassierens braucht deshalb einen Termin am Tag des Laufs (Folge für die parallele Live-Test-Sitzung, nur vermerken, nicht umsetzen).

**Tests:** API: Einchecken morgen → 400, heute → 200; Erledigt morgen → 400; Termin „Gebucht“ auf Rechnung → 409, „Eingecheckt“ → 200 (bestehende Kassa-Tests, die gebuchte Termine kassieren, checken vorher ein; das ist die erwartete Anpassung, keine Abschwächung). Desktop: `AppointmentViewModel` an künftigem Tag → `ShowCheckIn` false; Kassa-Liste: Zeile eines gebuchten Termins trägt „noch nicht eingecheckt“, Klick öffnet den Dialog (ViewModel-Test).

---

## Aufgabe C: Storno-Dialog mit Frist und Ausfallgebühr (L3)

**Befund:** Der Storno-Dialog (`AppointmentViewModel.cs:218-224`, `ConfirmDialogView`) kennt nur „Grund“. Die Stornofrist (`CancellationDeadlineHours`) und die Ausfallgebühr (`NoShowFeePercent`, je Standort und Behandlung, `src/AispaStudio.Contracts/OnlineContracts.cs:23-36`, `src/AispaStudio.Api/Online/OnlineRules.cs`) gelten heute nur online (`OnlineBookingEndpoints.cs:135`). „Ausfallgebühr belasten …“ sieht nur, wer `SuperviseCash` hat (`AppointmentViewModel.CardGuarantee.cs:108`).

**Lösung (nur Hinweis, keine automatische Gebühr):**

1. API: Der Termin (`AppointmentDto` oder ein kleiner Zusatz am bestehenden Detail-Aufruf des Termins) liefert `CancellationDeadlineAt` (Beginn minus Frist des Standorts) und `LateCancelFeePercent` (die wirksame Gebühr nach denselben Regeln wie online: Behandlung vor Standort, „nur neue Gäste“ beachten; vorhandene Hilfsfunktion `CardGuarantees.EffectivePercent` nutzen, nichts doppelt rechnen). Ohne Regel beides leer. Zuerst suchen, ob der Termin diese Werte schon trägt (`CardGuarantee`-Felder); dann nur ergänzen, was fehlt.
2. Windows-App: Im Dialog „Termin stornieren?“ über dem Feld „Grund“ eine Hinweiszeile nach meldungen.md M3 (Hinweis am Objekt, Symbol und Text):
   - innerhalb der Frist, Gebühr > 0: „Absage innerhalb der Stornofrist (bis dd.MM. HH:mm). Laut Regel fallen 50 % Ausfallgebühr an; belasten kann die Standortleitung.“ Mit `SuperviseCash` statt des letzten Satzes: „Belasten unter **Ausfallgebühr belasten …** nach dem Stornieren.“
   - innerhalb der Frist, keine Gebühr: „Absage innerhalb der Stornofrist (bis dd.MM. HH:mm).“
   - außerhalb der Frist oder ohne Regel: keine Zeile.
   Der Dialog bleibt sonst gleich (Grund, Stornieren, Abbrechen). Der Grund bekommt bei Absage innerhalb der Frist die Vorbelegung „Kurzfristig an der Rezeption storniert“, damit der Verlauf es zeigt.
3. Handbuch `kalender-termin-stornieren.md`: Absatz zur Hinweiszeile.

**Tests:** API: Termin in 2 Stunden bei Frist 24 Stunden und Gebühr 50 % → Werte gesetzt; Standort ohne Regel → leer. Desktop: Dialogtext bei den drei Fällen (ViewModel-Test), Vorbelegung des Grunds.

---

## Aufgabe D: Rückfragen für Krank melden, Pause löschen, Sperrzeit löschen (L10)

**Befund:** „… krank melden“ speichert sofort (`CalendarViewModel.Absence.cs:91-112`), „Pause löschen“ und „Sperrzeit löschen“ im Rechtsklick ebenfalls; keines hat ein Rückgängig (`CalendarViewModel.cs:1406-1460` kennt nur Verschieben, Stornieren, Einchecken, Lücke). Die Rezeption kann einen Krankenstand nicht zurücknehmen (Dienstplan braucht `ViewStaffRecords`).

**Lösung:** Den bestehenden Bestätigungsdialog (`ConfirmDialogView`, wie beim Stornieren) vor die drei Aktionen setzen:
- „<Name> krank melden?“ – „Der Dienstplan bekommt für heute einen Krankenstand. Danach schlägt der Ausfall-Assistent für die offenen Termine Ersatz vor.“ Knopf „Krank melden“.
- „Pause löschen?“ – „Die Pause HH:mm–HH:mm von <Name> wird entfernt.“ Knopf „Löschen“.
- „Sperrzeit löschen?“ – „Die Sperrzeit „<Grund>“ HH:mm–HH:mm wird entfernt.“ Knopf „Löschen“.
Fokus startet auf „Abbrechen“ (wie im Storno-Dialog, Handbuch). Kein Rückgängig dazu bauen (bewusst klein; Krank melden zurücknehmen bleibt Sache der Leitung im Dienstplan, im Dialogtext steht das nicht, aber im Handbuch `kalender-krankmeldung.md`).

**Tests:** Desktop-ViewModel: Aktion öffnet den Dialog, Abbrechen ändert nichts, Bestätigen ruft den Aufruf einmal.

---

## Aufgabe E: Hinweis „Keine Kontaktmöglichkeit“ beim Buchen (L5)

**Befund:** Anlegen mit nur einem Namen ist gewollt (`CustomerEndpoints.cs:66-70`). Beim Buchen (`BookingViewModel.cs`) gibt es keinen Hinweis, wenn der Gast weder Telefon noch E-Mail hat; Erinnerungen und Zusagen laufen dann ins Leere.

**Lösung:** Im Buchungsbereich „Neuer Termin“ neben der gewählten Person eine Pille wie „Anamnese prüfen“ (`CalendarView.axaml:1265-1270`), Text „Keine Kontaktmöglichkeit“, Tooltip „Ohne Telefon oder E-Mail gehen keine Erinnerung und keine Zusage. Kontakt in der Kundenkarte ergänzen.“ Sie blockiert nichts. Datenquelle ist der schon geladene Kundentreffer (Telefon, E-Mail); kein neuer API-Aufruf. Dieselbe Pille in der Kundenkarte oben (M3) ist erlaubt, wenn sie mit einer Zeile Code geht, sonst weglassen.

**Tests:** `BookingViewModel`: Gast ohne beides → Pille sichtbar; mit Telefon → unsichtbar.

---

## Aufgabe F: Kalender-Kleinigkeiten (L11, L16, Tester)

1. **Toast beim Buchen aus dem Panel (L11):** Nach erfolgreichem Buchen (`CalendarViewModel.NewEntry.cs:256-263`) derselbe Toast wie bei der Freie-Zeit-Suche: „Gebucht: <Gast>, <dd.MM.> <HH:mm>“ (`CalendarViewModel.FreeTime.cs:178-182`, Text nicht doppelt bauen, gemeinsame Hilfsmethode). Toast nach meldungen.md M1 (eine Zeile, ohne Punkt, 5 s). Für die Gruppenbuchung bleibt der bestehende Toast.
2. **Taste V (L16):** V öffnet die Verschieben-Karte des markierten Termins (wie der Panel-Knopf „Verschieben …“) statt des Hinweis-Toasts (`CalendarViewModel.Keyboard.cs:555, 799-803`); bei eingechecktem Termin, wo die Karte nicht gilt, bleibt der heutige Hinweis-Toast. V in die Tastenhilfe (Gruppe „Markierter Termin“) und ins Handbuch `kalender-tastenkuerzel.md` und `kalender-termin-verschieben.md`.
3. **Automation-Namen für den Live-Tester:** `AutomationProperties.Name="Termin"` am `SplitButton` der Kopfzeile (`CalendarView.axaml:2099-2157`) und `AutomationProperties.Name="Meldung"` am Toast-Text in `ToastBar` (`CalendarView.axaml:2937`). Keine weiteren Namen.

**Tests:** ViewModel-Test für den Toast-Text nach dem Buchen; Keyboard-Test „V öffnet Verschieben“; Ansichtstest, dass die beiden Namen gesetzt sind (Muster: bestehende Namenstests in `ViewTests`).

---

## Aufgabe G: Kassa: Tab „Entnahme“ nur mit Kassenaufsicht (L6)

**Befund:** `PosView.axaml:884-885` zeigt den Tab „Entnahme“ für alle; der Server lehnt für die Rezeption ab („Bargeld entnehmen darf die Standortleitung; Einlagen darf auch die Rezeption buchen.“, `PosService.cs:1374`). Das Handbuch `seite-kassa.md` sagt, Entnahme sähen nur Standortleitung und Inhaber.

**Lösung:** Tab „Entnahme“ mit `IsVisible="{Binding MaySuperviseCash}"` (die Eigenschaft gibt es, `PosViewModel.OnlineInfo.cs:84`); ohne Recht ist „Einlage“ gewählt und die Überschrift lautet „Einlage“ statt „Einlage oder Entnahme“; die Fußzeile „Kein Umsatz. Entnehmen darf nur, wer die Kassenaufsicht hat.“ bleibt. Serverregel bleibt unverändert.

**Tests:** Ansichtstest: ohne `SuperviseCash` kein Tab „Entnahme“; mit Recht beide Tabs.

---

## Aufgabe H: Handbuch an die App angleichen (L15)

Nur Texte unter `web/src/help/content/`, nach `docs/gestaltung/texte.md` und `web/src/help/README.md`; `web/src/help/textRules.test.ts` muss grün bleiben.

1. `kalender-termin-buchen.md` (und weitere Seiten mit „Termin anlegen“ als Knopfname): der Knopf heißt **Termin** (`CalendarView.axaml:2105`); Suchen mit `grep -rn "Termin anlegen" web/src/help/content`.
2. `seite-kassa.md`: nach Aufgabe G stimmt „Entnahme sehen nur Standortleitung und Inhaber“ wieder; Satz prüfen und ggf. „und Manager“ ergänzen, je nachdem, wie `MaySuperviseCash` gesetzt wird.
3. `kalender-termin-anamnese.md` Zeilen 38 und 48: Die Rezeption hat `ViewHealthData` (`AccessContracts.cs:79`) und sieht **Antworten ansehen** und **Prüfen**; Text entsprechend.
4. `kalender-status-setzen.md`: Toast „… eingecheckt“ ohne Punkt; nach Aufgabe B den Satz „Einchecken geht nur am Tag des Termins“ ergänzen.
5. `kalender-paar-und-spa-tag.md`: „Ganze Buchung stornieren“ hat eine Rückfrage (`AppointmentViewModel.cs:586-592`); der Kasten heißt „Paar“, „Spa-Tag“ oder „Gruppe · n Gäste“, nicht „GEMEINSAM GEBUCHT“.
6. Gutschein-Beispiel: Platzhalter in `PosView.axaml:649` („z. B. GS-7K3M-Q9“) und Handbuchseiten `kassa-gutschein-*.md` auf das Serverformat `XXXX-XXXX-XXXX` (z. B. „K7M3-Q9XA-B2CD“, `PosService.cs:1898-1912`); Desktop-Tests, die „GS-…“ als Beispiel nutzen, nur anpassen, wenn sie am Format hängen.
7. `seite-kunden.md`: Es gibt keinen Knopf „Bearbeiten“ neben dem Kopf; der Weg ist **…** (Weitere Aktionen) → **Bearbeiten (Name, Anrede, Kontakt, Notiz)** oder der Stift „Notiz ändern“.

Bilder nur neu erzeugen, wo ein Bild einen falschen Knopf zeigt; sonst „Bilder unverändert“ im PR-Protokoll.

**Geprüft wenn:** `cd web && node node_modules/vitest/vitest.mjs run src/help` grün und `grep -rn "Termin anlegen\|GEMEINSAM GEBUCHT\|GS-7K3M" web/src/help/content` leer.

---

## 4. Fertigmeldung

Am Ende: Branch-Name, Liste der Squash-Commits je Aufgabe, was nicht geprüft wurde (z. B. `DemoData:Mode=Services`, Bilder), offene Fragen. Kein Push, kein PR.
