# Live-Tests Rezeption: Onboarding-Katalog und Bewertung

Stand: 08.11.2026. Geschrieben aus Sicht einer Rezeptionistin, die seit Monaten mit der Windows-App arbeitet und eine neue Kollegin einschult. Grundlage: [ADR 0090](../entscheidungen/0090-live-tests-mit-flaui.md) (Live-Tests mit FlaUI), der Tester `tests/AispaStudio.LiveTests` (Muster `ReceptionJourney.cs`), das Handbuch (`web/src/help/content/`), die Demo-Daten ([testdaten.md](../testdaten.md), `src/AispaStudio.Api/Demo`) und die Masken in `src/AispaStudio.Desktop`. Jede Aussage über die App ist im Code nachgelesen; wo nur das Handbuch spricht, steht „(Handbuch)“.

Dieses Dokument plant und bewertet nur. Den Code der Live-Tests schreibt ein eigener Schritt nach dieser Spezifikation.

## Begriffe und Grenzen, die für alles gelten

- **„Ist da“ gibt es in der App nicht.** Der Knopf heißt **Einchecken** (Taste E), der Status „Eingecheckt“ (`Views/CalendarView.axaml:145-151`, `ViewModels/CalendarItems.cs:508-515`). Dieses Dokument sagt deshalb „Einchecken“.
- **Eine Tagesregel fürs Einchecken gibt es nicht.** `BookingService.ChangeStatusAsync` (`src/AispaStudio.Api/Scheduling/BookingService.cs:404-445`) prüft kein Datum; der Knopf steht bei jedem gebuchten Termin (`ViewModels/AppointmentViewModel.cs:118`). Nur „Behandlung begonnen“ aus der Handy-App ist auf den Tag beschränkt („Begonnen lässt sich nur am Tag des Termins melden.“, `BookingService.cs:470`). **Folge für die Live-Tests:** Ein für morgen gebuchter Termin lässt sich heute einchecken und kassieren; die Einschränkung aus ADR 0090 („Kassieren braucht ‚Ist da‘ am Tag des Termins“) gilt so nicht. Kassieren verlangt in der Oberfläche nur „Eingecheckt“ oder „Erledigt“ (`AppointmentViewModel.cs:408`); der Server prüft den Status gar nicht (`src/AispaStudio.Api/Pos/PosService.cs:401-414`). Fachlich ist das Einchecken eines Termins von morgen Unsinn, technisch geht es, und der Live-Test kann es nutzen (siehe Thema 8 und Lücke L1).
- **Rechte der Rezeption** (`src/AispaStudio.Api/Access/Roles.cs:25`): `ViewCalendar`, `Book`, `ChangeStatus`, `ViewHealthData`. Nicht dabei: `SuperviseCash` (Bargeld entnehmen, fremde oder ältere Belege stornieren, Ausfallgebühr belasten), `ViewFinance`/`ViewAmountReports` (Berichte mit Beträgen, frühere Tagesabschlüsse), `ManageLocation` (Kennzeichen „Nicht mehr buchen“, Raumsperren, Schließzeiten). Den Tagesabschluss darf die Rezeption auslösen (`PosEndpoints.cs:279-283`).
- **Navigation** für die Rezeption (`ViewModels/ShellViewModel.cs:79-138`): Kalender, Kunden, Anfragen (nur mit Modul Terminannahme), Kassa, Geräte, Netz (bei Serververbindung), Verwaltung (nur Team, Lager, Zahlen), Hilfe.
- **Demo-Daten:** Konto `empfang.wien@demo.aispa.test` (Nina Kovač, Wien), PIN `1234`. „Heute“ ist der Tag des ersten API-Starts; am Sonntag hat in Wien niemand Dienst, der Tester stellt deshalb auf den nächsten Tag. Namen außer den zwölf Stammgästen (Anna Bauer, Thomas Leitner, Katharina Moser, Florian Hofer, Eva Steiner, Lukas Pichler, Maria Wimmer, Stefan Egger, Julia Novak, Ayşe Kaya, Clara Fischer, David Horvath; `Demo/DemoCustomers.cs:22-27`) werden gewürfelt. **Ingrid Wallner ist nicht fest angelegt**; der bestehende Tester setzt sie voraus (`ReceptionJourney.cs:10`). Die Spezifikation nimmt deshalb Stammgäste oder die im Lauf angelegte Kundin.
- **Für den Tester:** Buchstaben-Kürzel greifen nur bei Fokus auf der Kalenderfläche (`CalendarView.axaml.cs:410-411`); Toasts stehen in `ToastBar` ohne Schlusspunkt, höchstens 60 Zeichen (`ViewModels/CalendarViewModel.Problems.cs:105-109`); die Kalenderkarte heißt „`HH:mm Gast, Behandlung, Status`“ (`ViewModels/CalendarItems.cs:496`); Rechtsklick-Einträge tragen ihre Beschriftung als `AutomationProperties.Name` (`CalendarView.axaml.cs:572`).

## 1. Onboarding-Plan

So würde ich eine neue Kollegin durch die App führen. Reihenfolge nach dem, was am ersten Tag wirklich vorkommt; die Nummern sind die Themen in Abschnitt 2.

### Erster Arbeitstag (Vormittag): sehen und verstehen

| Nr. | Thema | Ablauf in der App |
| --- | --- | --- |
| 1 | Anmelden und abmelden | Anmeldung → Testkonto wählen → **Als Testkonto anmelden**; Kontoknopf oben „Konto Nina Kovač, Rezeption · AISPA Day Spa GmbH“ → **Abmelden**. Im Betrieb: Schnellwechsel per PIN am Empfangsgerät. |
| 2 | Tagesüberblick und Kalender lesen | Kalender: Chip **Tagesüberblick** („17 Termine · 2 da · 1 offen“), Pfeile **Vortag**/**Nächster Tag**, **Heute**, Datum wählen (G), **Team**/**Räume**, Zoom, Chip **Meldungen**, Suche (Strg+F), Tastenhilfe (F1). |
| 3 | Kundin suchen und Kundenkarte lesen | Kunden: Feld **Kunden suchen** (Name, Telefon oder E-Mail), Karte mit Sternen, Kennzeichen, Verlauf, Wünschen, Rezeptionsvermerk. |

### Erster Arbeitstag (Nachmittag): die drei täglichen Handgriffe

| Nr. | Thema | Ablauf in der App |
| --- | --- | --- |
| 4 | Kundin anlegen | Kunden → **Neue Kundin / neuer Kunde** → Vorname, Nachname, Telefon → Block **Schon vorhanden?** beachten → **Anlegen** oder **Anlegen und Termin buchen**. |
| 5 | Termin buchen | Kalender → **Termin** (oder N, Klick auf freie Stelle) → Gast suchen → Behandlung → Datum/Beginn → **Termin buchen**; bei Unsicherheit **Freie Zeit suchen** (Strg+N) und Enter. |
| 6 | Einchecken und Status | Termin anklicken → **Einchecken** (E) → Toast „… eingecheckt“ mit **Rückgängig**; „Kommt 5/10/15 Min. später“ per Rechtsklick; hinter **Mehr**: **Erledigt**, **Nicht erschienen**. |
| 7 | Termin verschieben | Ziehen (rastet auf 5 Min.), Alt+Pfeile, oder Panel **Verschieben …** mit Tag, Beginn, +1 Tag/+1 Woche; Toast mit **Rückgängig**. |

### Zweiter und dritter Tag: Geld

| Nr. | Thema | Ablauf in der App |
| --- | --- | --- |
| 8 | Termin kassieren | Termin eingecheckt → **Kassieren** (K) → Kassa, Reiter **Kassieren**, Rechnung mit dem Termin → **Bar gegeben**/**Karte** → **Bezahlen und Beleg erstellen** → „Bezahlt“, „Beleg Nr. n“. |
| 9 | Artikel, Rabatt, Aktion | Spalte **Artikel und Gutscheine** aufklappen → Artikel „… auf die Rechnung“ → Prozentsymbol je Position → bis 20 % selbst, darüber **Freigabe durch** und PIN der Leitung → Banner **Aktion „Herbstzauber“** → **Anwenden**. |
| 10 | Gutschein verkaufen und einlösen | **Gutschein verkaufen** → **Wert** → **Wertgutschein verkaufen**; einlösen: Feld **Gutschein-Code** in der Bezahlen-Spalte, Hilfetext „Restwert … · gültig bis …“. |
| 11 | Beleg stornieren, Rechnung parken | Reiter **Belege** → Beleg wählen → **Grund** → **Stornieren** → Rückfrage „Beleg stornieren?“ (eigene Belege nur 15 Minuten lang); **Parken** und unter **Geparkte Rechnungen** weiterkassieren. |
| 12 | Tagesabschluss | Reiter **Kassenlade & Abschluss** → **Einlage** (Wechselgeld) → abends **Gezählter Bestand** → **Tagesabschluss durchführen** → „Tagesabschluss gespeichert“, „Keine Differenz“ oder „Differenz …“. |

### Erste Woche: alles, was seltener vorkommt

| Nr. | Thema | Ablauf in der App |
| --- | --- | --- |
| 13 | Termin stornieren und zurücknehmen | Entf oder **Termin stornieren …** → Dialog „Termin stornieren?“ mit **Grund** (vorbelegt „An der Rezeption storniert“) → **Stornieren**; Toast mit **Rückgängig**; später **Stornierung zurücknehmen**. |
| 14 | Anfragen bearbeiten | Anfragen (Seite „Posteingang“) → Nachricht wählen → **Wer ist das?** zuordnen → **Freie Zeit** → Zeit wählen → **Vormerken und vorschlagen** → Entwurf prüfen → **Entwurf speichern** → **Senden** → **Bestätigen** oder **Freigeben**. |
| 15 | Paar und Spa-Tag | **Termin ▾ → Paar oder Spa-Tag** (P) → Wann, Gast 1, Gast 2 oder **Name folgt**, je Behandlung → „… alles buchen“ → Karte „Paar mit …“; Panel-Kasten mit **Alle einchecken**, **Alle kassieren**, **Ganze Buchung verschieben/stornieren**. |
| 16 | Anamnese (Gesundheitsfragebogen) | Termin-Panel, Karte **Anamnese**: „Fehlt noch.“ → **Am Tablet ausfüllen** (QR-Code, 30 Minuten) oder **Papierbogen drucken** → **Foto hochladen** → **Prüfen**; Pille **Anamnese prüfen** beim Buchen. |
| 17 | Warteliste, Lücken, Pausen, Notizen | Chip **Warteliste n**, **Lücken schließen** („Auf HH:mm verschieben“), **Pause hier**, **Notiz hier**, **Tagesnotiz** (.). |
| 18 | Krankmeldung und Ersatz | Rechtsklick auf den Spaltenkopf → **… krank melden** (ohne Rückfrage!) → Ausfall-Assistent „Ersatz für …“ → **Gewählte übernehmen**. |
| 19 | Sauna-Eintritt | **Termin ▾ → Sauna-Eintritt** (S) → Panel „Sauna und Ruheraum“ → Eintritt → Kassa-Symbol „Eintritt an der Kassa abrechnen“. |
| 20 | Arbeiten ohne Verbindung | Banner „Keine Verbindung zum Server …“; Buchen, Verschieben, Status werden vorgemerkt („vorgemerkt“ an der Karte), Kassieren und Freie-Zeit-Suche gehen nicht. |

## 2. Live-Test-Spezifikation je Thema

Rolle = `ReceptionJourney.Journey` (`LiveJourney`, Vorlauf: abmelden, anmelden, nächster Tag). Jedes Thema ist ein `LiveTopic(Key, Name, Beschreibung, Schritte)`. **Priorität** für die Umsetzung: **A** = erster Satz (täglicher Kern, stabil prüfbar), **B** = zweiter Satz, **C** = später oder nur mit Vorarbeit. **Stabilität** sagt, ob ein Lauf heute ohne Eingriff in Daten oder Code zuverlässig durchläuft: ✅ stabil, ⚠️ mit Einschränkung, ❌ heute nicht sinnvoll.

Die Reihenfolge der Themen im Lauf ist fest (Liste in `LiveJourney.Topics`); Themen, die eine Kundin oder einen Termin des Laufs brauchen, stehen hinter `customer` und `appointment` und greifen über `LiveRunContext` darauf zu (heute: `Stamp`, `CustomerName`; neu nötig: der gebuchte Termin als Kartenname und Beginn).

### Thema 1 `signin` – Anmelden und abmelden (Prio A, ✅)

Heute ist die Anmeldung der Vorlauf, kein Thema. Als eigenes Thema nur die Abmeldung prüfen:

- Schritte: Kontoknopf (Name beginnt mit „Konto “) klicken → **Abmelden** → Prüfung: Knopf **Als Testkonto anmelden** sichtbar. Danach neu anmelden (wie Vorlauf).
- Demo-Daten: Konto Nina Kovač.
- Sonderfälle: **Falsches Konto** (Lisa Hofer, befristet, `Demo/DemoAccounts.cs:39`): Anmeldung geht; Prüfung „Konto Lisa Hofer, Rezeption …“. Mehr ist mit Testkonten nicht prüfbar (keine Passwörter). Schnellwechsel per PIN braucht ein gekoppeltes Empfangsgerät: ❌.

### Thema 2 `overview` – Tagesüberblick und Kalender lesen (Prio A, ✅)

- Schritte: Kalender öffnen → Chip `OverviewChip` klicken → Prüfung: Knöpfe „Termine, n“, „Eingecheckt, n“, „Noch erwartet, n“ (Name beginnt mit „Termine,“) → Esc → **Räume** (`HeadRooms`) → Prüfung: Spaltenköpfe mit Raumnamen („Raum Lavendel“) → **Team** (`HeadTeam`) → **Vortag**, **Nächster Tag**, **Heute** → Prüfung: Titel „Heute“ (`CalendarViewModel.cs:292-296`) → Strg+F, „Moser“ eintippen → Prüfung: Treffer-Popup (nur, wenn Katharina Moser einen künftigen Termin in Wien hat; sonst Text „Keine Treffer“, beides als Erfolg werten) → Esc → F1 → Prüfung: `KeyHelp` sichtbar → **Tastenhilfe schließen**.
- Demo-Daten: Termine des nächsten Tages in Wien (immer vorhanden, Verlauf reicht 4 Wochen voraus).
- Sonderfälle: **Sonntag** (Standort zu): Info „Der Standort hat an diesem Tag geschlossen.“ (`SchedulingContracts.cs:848`) auf dem Vortag des Montags prüfen – nur sinnvoll, wenn der Lauf an einem Montag läuft; sonst **Vortag** zweimal bis Sonntag. ⚠️ Zahl der Termine nicht fest, deshalb nur auf Vorhandensein prüfen, nie auf Werte.

### Thema 3 `customer-search` – Kundin suchen und Karte lesen (Prio A, ✅)

- Schritte: Kunden → Feld **Kunden suchen** „Katharina Moser“ → Trefferzeile klicken → Prüfung: Kartenkopf (h1) „Katharina Moser“, Rabattzeile mit „10“, Abschnitt **Verlauf** → Suche nach Telefon „+43 664 1234002“ (Stammgast Index 002, `DemoCustomers.cs:123`) → Prüfung: derselbe Treffer → Suche „Xyzzy“ → Prüfung: „Keine Kundin gefunden.“ und Knopf „Neue Kundin „Xyzzy“ anlegen“ (`ViewModels/CustomersViewModel.NewForm.cs:25-30`).
- Demo-Daten: Katharina Moser mit 10 % Rabatt (`DemoCustomers.cs:140`).
- Sonderfälle: Suche nach Rufname „Kathi“ (ist als Rufname angelegt) → Treffer Moser. Suche mit Titel „Mag. Leitner“: Titel werden ignoriert, Treffer trotzdem (`CustomerEndpoints.cs:21-42`).

### Thema 4 `customer` – Kundin anlegen (Prio A, ✅, besteht)

Wie heute (`Eva Live<Stamp>`), erweitert um Sonderfälle als eigene Themen, weil sie Daten hinterlassen:

- **`customer-duplicate` – Dublette** (Prio A, ✅): Kunden → **Neue Kundin / neuer Kunde** → Nachname „Moser“, Vorname „Katharina“ → Prüfung: Block **Schon vorhanden?** mit Zeile „Katharina Moser“ und Knopf **Diese Kundin nehmen** (`CustomersView.axaml:91-100`) → **Diese Kundin nehmen** → Prüfung: Karte „Katharina Moser“ offen, Maske zu. **Anlegen bleibt möglich**, die App blockiert nicht (`NewCustomerMatchesViewModel.cs:54`). Zweiter Lauf mit Telefon der Stammkundin (letzte 7 Ziffern) statt Name.
- **`customer-minimal` – nur Nachname, kein Telefon** (Prio A, ✅): nur „Nachname“ = „Live<Stamp>“ → Prüfung: **Anlegen** aktiv (Pflicht ist nur ein Name, `CustomerEndpoints.cs:66-70`) → **Anlegen** → Karte ohne Kopierknöpfe „Telefon … kopieren“ (erscheinen nur mit Telefon, `CustomersView.axaml:194-207`).
- **`customer-empty` – Pflichtfeld leer** (Prio A, ✅): Maske öffnen, nichts eintragen → Prüfung: **Anlegen** deaktiviert, Tooltip „Verfügbar, sobald ein Vor- oder Nachname eingetragen ist.“ (`CustomersViewModel.cs:581`) → **Abbrechen**.
- **`customer-badmail` – E-Mail ohne @** (Prio B, ✅): Nachname + E-Mail „keine-adresse“ → **Anlegen** → Prüfung: rote InfoBar „Keine gültige E-Mail-Adresse.“ (`CustomerEndpoints.cs:71`; Fehlertext-Anzeige in `CustomersView.axaml:30`).
- Telefonformat: „0664 1234567“ wird beim Verlassen des Feldes zu „+43 664 1234567“ (`CustomersViewModel.cs:127-141`) – Prüfung am Feldwert.

### Thema 5 `appointment` – Termin buchen (Prio A, ✅, besteht)

Wie heute, aber **Gast des Laufs oder ein Stammgast** statt Ingrid Wallner (siehe oben). Nach dem Buchen Kartenname und Beginn in den `LiveRunContext` legen, damit Thema 6 bis 8 und 13 ihn finden. Zusätzliche Prüfung: Panel geschlossen (kein Erfolgs-Toast beim Buchen aus dem Panel, `NewEntry.cs:256-263`; nur die Freie-Zeit-Suche meldet „Gebucht: …“).

Sonderfälle als eigene Themen:

- **`appointment-conflict` – Doppelbuchung** (Prio A, ✅): Termin des Laufs merken (Spalte, Beginn) → Rechtsklick auf dieselbe Stelle in derselben Spalte → **Termin hier (HH:mm, Person)** → anderen Gast (Stammgast) und dieselbe Behandlung wählen → **Termin buchen** → Prüfung: rote Meldung mit Titel „Zeitraum ist belegt.“ (`ViewModels/ConflictNotice.cs:47`) und Knopf **Freie Zeit suchen** → **Abbrechen**. Ein „Trotzdem buchen“ gibt es bei Belegung nicht (409 vom Server, `CalendarEndpoints.cs:195-196`). Stabil, weil die Vorbelegung aus dem Lauf stammt.
- **`appointment-past` – Termin in der Vergangenheit** (Prio A, ✅): **Vortag** bis auf gestern → Klick auf eine freie Stelle → Prüfung: Toast „Termine lassen sich nicht in der Vergangenheit eintragen“ (`NewEntry.cs:143-148`). Zusätzlich im Panel „Neuer Termin“ Datum auf gestern: **Termin buchen** deaktiviert (`BookingViewModel.cs:564`, `DateField Minimum=Today`).
- **`appointment-off-duty` – Person nicht im Dienst** (Prio B, ⚠️): Spalte einer Person ohne Dienst klicken (Montag: Marco Berger und Siriporn Kaewdee haben Di–Sa Dienst, `Scheduling/DemoData.cs:94-102`) → Prüfung: InfoBar „Kein Dienst“ mit „… hat an diesem Tag keinen Dienst hier …“ und Knopf **Trotzdem buchen** (`CalendarViewModel.Messages.cs:285-287`). ⚠️ Wochentagabhängig: Das Thema muss den Wochentag des Zieltags lesen und eine passende Person wählen oder sich selbst als „nicht anwendbar“ melden.
- **`appointment-closed` – außerhalb der Öffnungszeit** (Prio B, ✅): Panel „Neuer Termin“ mit Beginn 20:30 (Wien schließt 20 Uhr) → Prüfung: Warnung „Der Standort hat zu dieser Zeit geschlossen.“ und **Trotzdem buchen** (`BookingViewModel.cs:75-87`, `CalendarView.axaml:1372`) → **Abbrechen** (nicht buchen, sonst bleibt ein Termin außerhalb der Zeit).
- **`appointment-no-free-time` – keine freie Zeit** (Prio B, ⚠️): Strg+N → Behandlung „Massage für Schwangere“ (Räume nur Lotus und Alpenrose, `DemoData.cs`), Raum „Raum Lotus“ wählen, Zeitraum ab übermorgen (Raumsperre Lotus drei Tage, `Demo/DemoClosures.cs:42-54`) → Prüfung: Warnung „In den nächsten 14 Tagen ist nichts frei: … im Raum Lotus.“ mit Knopf **Alle Räume** (`FreeTimeViewModel.cs:182-216`). ⚠️ Die Suche schaut 14 Tage voraus; nach der Sperre ist der Raum wieder frei, deshalb kann sie Vorschläge nach der Sperre liefern („An diesem Tag ist nichts frei.“ als Kleintext). Dann ist dieser Kleintext die Prüfung. Sicherer wäre eine Behandlung, die eine Qualifikation braucht, die in Wien niemand hat („Zirben-Ritual“ nur Salzburg): Meldung „Team: Niemand mit der nötigen Qualifikation …“ (`ResourcePlanner.cs:370`) – zu prüfen, ob die Behandlung in Wien überhaupt wählbar ist.
- **`appointment-room-blocked` – Raum gesperrt** (Prio B, ⚠️): **Räume** → Spalte „Raum Lotus“ am Tag der Sperre (ab übermorgen) → Klick auf freie Stelle → Buchen → Prüfung: Konfliktzeile „Sperrzeit · … · Raum Lotus“ (`ResourcePlanner.cs:~395`). ⚠️ Nur gültig, solange die Demo-Daten frisch sind (Sperre ist relativ zum Einspieltag).
- **Pflichtfeld leer:** **Termin buchen** deaktiviert, Tooltip „Verfügbar, sobald Gast, Behandlung und Beginn gewählt sind.“ (`CalendarView.axaml:1376`) – als Prüfschritt in `appointment` aufnehmen, bevor der Gast gewählt wird.
- **Kundin ohne Telefon buchen:** geht ohne Warnung (kein Hinweis im Code). Nicht prüfbar als Sonderfall, siehe Lücke L5.
- **Qualifikation fehlt:** tritt beim normalen Buchen nicht auf, weil die Behandlungsliste der Spalte nur passende Behandlungen zeigt (`BookingViewModel.cs:118-128`). Prüfung: Spalte Sophie Gruber (Kosmetik) → Liste **Behandlung** enthält keine „Klassische Massage“ – als Negativprüfung („Treffer darf nicht existieren“) braucht der Tester eine `ExpectMissing`-Hilfe, die es heute nicht gibt.

### Thema 6 `checkin` – Einchecken, Verspätung, Status (Prio A, ✅)

- Vorbedingung: Termin des Laufs (Thema 5) am nächsten Tag; Einchecken geht ohne Tagesregel (siehe oben).
- Schritte: Karte des Laufs anklicken → Panel offen (Name „Termin“ oder Kundenname) → **Einchecken** → Prüfung: Toast „<Gast> eingecheckt“ mit **Rückgängig** (`CalendarViewModel.cs:1453`) und Karte endet auf „, Eingecheckt“ → **Rückgängig** (innerhalb 8 s) → Karte „, Gebucht“ → erneut **Einchecken** (für Thema 8).
- Sonderfälle:
  - **Zu spät zurücknehmen:** Panel-Knopf **Einchecken zurücknehmen** (unter dem Hauptknopf, `AppointmentViewModel.cs:248`) nach Ablauf des Toasts → Karte „, Gebucht“. ✅
  - **Kommt später:** Rechtsklick → **Kommt 10 Min. später** → Toast „<Gast> kommt 10 Min. später (HH:mm)“ (`CalendarViewModel.Late.cs:36-40`) → **Doch pünktlich**. Nur bei „Gebucht“, deshalb vor dem Einchecken. ✅
  - **Nicht erschienen nach Einchecken:** **Mehr** → **Nicht erschienen** deaktiviert, Hinweis „Verfügbar bei gebuchten Terminen.“ (`CalendarView.axaml:257-260`). ✅
  - **Erledigt:** nur in einem Lauf, der den Termin nicht mehr kassieren will (Erledigt verhindert Kassieren nicht, aber „Einchecken zurücknehmen“). Prüfung: Karte „, Erledigt“; kein Toast aus dem Panel (`AppointmentViewModel.cs`), per Rechtsklick Toast „<Gast>: erledigt“ (`Keyboard.cs:775-787`). ✅ als eigenes Thema `complete`, am Ende des Laufs.

### Thema 7 `reschedule` – Termin verschieben (Prio A, ✅)

- Schritte: Karte des Laufs (Status Gebucht; eingecheckte lassen sich nur ziehen, nicht per Panel, `AppointmentViewModel.cs:424`) → Panel **Verschieben …** → **+1 Tag** → **Verschieben** → Prüfung: Toast „Verschoben auf HH:mm–HH:mm (…)“ mit **Rückgängig** (`CalendarViewModel.cs:1319`), Kartenname am Folgetag (Navigation **Nächster Tag**) → **Rückgängig** oder zurück verschieben, damit Thema 8 den Termin am erwarteten Tag findet.
- Alternativ Alt+↓ (15 Min.) bei fokussierter Kalenderfläche; Ziehen per Maus ist mit FlaUI möglich (`Mouse.Drag`), aber koordinatenabhängig: Prio C.
- Sonderfälle:
  - **Ziel belegt:** Verschieben auf den Beginn eines anderen Termins derselben Spalte → InfoBar „Nicht verschoben“ mit Konfliktliste (`CalendarViewModel.cs:1300`). ⚠️ braucht einen zweiten Termin in der Spalte; den kann der Lauf vorher selbst buchen (zweiter Stammgast).
  - **In die Vergangenheit:** Panel **Verschieben …**, Tag auf gestern → **Verschieben** → es gibt **keine Sperre** (`CalendarEndpoints.cs:210-217`, kein `Minimum` am Feld, `CalendarView.axaml:195`). Der Live-Test kann das belegen, muss den Termin danach aber zurückholen. Siehe Lücke L2. ✅ (als Beleg einer Lücke)
  - **Eingecheckt verschieben:** Knopf **Verschieben …** fehlt → `ExpectMissing` nötig.

### Thema 8 `checkout` – Termin kassieren (Prio A, ⚠️)

- Vorbedingung: Termin des Laufs eingecheckt (Thema 6). Technisch geht das am Vortag (keine Tagesregel), deshalb ist Kassieren **heute prüfbar**, anders als ADR 0090 annimmt. Die Kassa-Liste „Heute abzurechnen“ zeigt den Termin von morgen aber nicht (`PosDayList.cs:30`, nur heute); der Weg führt über den Kalender-Knopf **Kassieren** (K), der den Termin direkt auf die Rechnung setzt (`ShellViewModel.cs:884-894`).
- Schritte: Karte des Laufs → **Kassieren** → Prüfung: Seite Kassa, Reiter **Kassieren**, Rechnungsposition mit Behandlungsname und Preis („Gesichtsbehandlung Klassik“, 95,00) → Bezahlen-Spalte `PayColumn`: Feld **Karte** → **Rest** → Prüfung: „Alles gedeckt“ (`PosViewModel.cs:1341-1358`) → **Bezahlen und Beleg erstellen** → Prüfung: Überschrift „Bezahlt“ und Text beginnt mit „Beleg Nr.“ (`PosView.axaml:245`).
- ⚠️ Kassa-Signatur: Die Demo-Kassa WIEN-1 läuft mit Test-Signatur; erste Belege eines Tages sind im Demo-Verlauf schon da, der Startbeleg existiert (`testdaten.md`). Wenn die Kasse „außer Betrieb“ meldet (`PosService.cs:1894`), ist das ein Serverzustand, kein App-Fehler.
- Sonderfälle:
  - **Bar mit Rückgeld** (Prio A, ✅): **Bar gegeben** 100,00 bei 95,00 → Prüfung: „Rückgeld: 5,00 €“ → Beleg.
  - **Zu wenig bar** (Prio A, ✅): 50,00 → „Noch offen: 45,00 €“, Knopf deaktiviert, Tooltip „Verfügbar, sobald die Zahlungen den Betrag decken …“.
  - **Rückgeld auf Karte** (Prio B, ✅): Karte 100,00 → Server: „Zu viel bezahlt: … Rückgeld gibt es nur bar.“ (`PosService.cs:1129`).
  - **Kassa ohne Einchecken** (Prio B, ✅): Karte eines gebuchten Termins → **Kassieren** fehlt (`CanCheckout`, `AppointmentViewModel.cs:408`); die K-Taste tut nichts. `ExpectMissing` nötig. Hinweis: In der Kassa-Liste „Später heute“ steht ein heutiger, nicht eingecheckter Termin trotzdem und lässt sich kassieren (`PosDayList.cs:30`, Server ohne Statusprüfung) – siehe Lücke L1.
  - **Zweimal kassieren** (Prio B, ✅): erneut **Kassieren** am abgerechneten Termin → Knopf fehlt bzw. Server „Dieser Termin steht schon auf einer Rechnung.“ (`PosService.cs:414`).
  - **Kundenrabatt automatisch:** Termin für Katharina Moser kassieren → Position „Kundenrabatt 10 %“ (Handbuch `kunden-rabatt.md`; Serverrechnung nicht gelesen). Prio B, ⚠️ Positionstext zu verifizieren.
  - **Anzahlung abgezogen:** nur in Graz vorbereitet (`DemoExtras.cs:195-223`), für die Wiener Rezeption ❌.
  - **Drucken:** ohne Bondrucker deaktiviert („Kein Bondrucker eingerichtet (unter „Geräte“).“, `PosViewModel.cs:220`) – als Prüfung des Zustands ✅, Drucken selbst ❌.

### Thema 9 `pos-sale` – Artikel, Rabatt, Aktion (Prio B, ✅)

- Schritte: Kassa → `SalesColumnHeader` **Artikel und Gutscheine** aufklappen → Artikel „Massageöl … auf die Rechnung“ (Name endet auf „auf die Rechnung“, `PosView.axaml:161`) → Prozentsymbol „Rabatt auf …“ → **in %** 10 → **Übernehmen** → Prüfung: Rabattzeile → **Bar gegeben** → **Rest** → Beleg.
- Sonderfälle:
  - **Rabatt über 20 %** (Prio A, ✅): 25 → Prüfung: Felder **Freigabe durch** und **PIN** erscheinen (`PosViewModel.cs:1065`); Freigabe durch „Markus Steiner“ mit PIN `1234` (`DemoAccounts.cs:22`) → Rabatt angenommen. Falsche PIN „0000“ → „Name oder PIN der Freigabe stimmt nicht.“ (`Access/QuickSwitch.cs:343`). Vorsicht: Nach mehreren Fehlversuchen sperrt die PIN („Zu viele Fehlversuche …“, `QuickSwitch.cs:353`), deshalb nur ein Fehlversuch je Lauf.
  - **Aktion anwenden** (Prio B, ⚠️): Termin mit „Klassische Massage“ auf der Rechnung → Banner „Aktion „Herbstzauber“ …“ → **Anwenden** (Name „Herbstzauber anwenden“, `PosViewModel.Promotions.cs:14,45`) → Zeile „Aktion Herbstzauber 10 %“. ⚠️ Herbstzauber läuft nur bis 20 Tage nach dem Einspielen.
  - **Unbekannter Barcode** (Prio B, ✅): im Feld **Gast, Code oder Artikel suchen** „0000000000000“ + Enter → „Der Code … gehört zu keinem Artikel der Kassa. …“ (`PosViewModel.cs:800`). Bekannter Barcode `9001234000001` → Artikel auf der Rechnung.
  - **Rechnung ohne Gast (Laufkundschaft)**: Artikel ohne Termin → Beleg geht (Handbuch `kassa-artikel-verkaufen.md`). ✅
  - **Rückgabe von Ware:** gibt es nicht, nur Storno des ganzen Belegs (Suche in `Pos/*.cs` ohne Treffer). ❌, Lücke L7.

### Thema 10 `voucher` – Gutschein verkaufen und einlösen (Prio A, ✅)

- Schritte: Kassa → **Gutschein verkaufen** → **Wert** 50,00 → **Wertgutschein verkaufen** → **Karte** → **Rest** → Beleg → Prüfung: Belegzeile „Gutschein XXXX-XXXX-XXXX“ (`PosViewModel.cs:1453`); **Code aus der Belegzeile lesen** (Format 4-4-4, Alphabet ohne 0/O/1/I/L, `PosService.cs:1898-1914`) und in den `LiveRunContext` legen → neue Rechnung: Artikel „Zirbenkissen“ → Feld **Gutschein-Code** = Code → Feld verlassen → Prüfung: Hilfetext beginnt mit „Restwert 50,00“ (`PosViewModel.OnlineInfo.cs:43-47`) → Betrag vorbelegt → **Bezahlen und Beleg erstellen** → „Bezahlt“.
- Sonderfälle:
  - **Ungültiger Code** (Prio A, ✅): „ABCD-EFGH-2345“ → „Diesen Gutschein-Code gibt es nicht.“
  - **Aufgebrauchter Gutschein** (Prio B, ✅): denselben Code nach vollem Einlösen → „… · nicht mehr gültig“ bzw. beim Bezahlen „Gutschein … ist nicht mehr gültig (Restwert 0,00)“ (`PosService.cs:995`).
  - **Teileinlösung mit Restwert:** Artikel 19,90 mit 50-Euro-Gutschein → Restwert 30,10 bei nächster Abfrage. ✅
  - **Behandlungsgutschein für andere Behandlung:** „… gilt nur für eine bestimmte Behandlung, die nicht auf diesem Beleg steht.“ – Prio C.
  - **Gutschein suchen ohne Kassa:** nicht möglich (nur `GET /api/pos/vouchers/{code}` beim Verlassen des Feldes; keine Liste). Lücke L4.
  - **Rabatt auf Gutschein:** „Gutscheine werden zum vollen Wert verkauft.“ (`PosService.cs:241-248`). Prio C.

### Thema 11 `receipt-void` – Beleg stornieren und Rechnung parken (Prio B, ✅)

- Schritte: direkt nach einem eigenen Beleg (Thema 9) → Reiter **Belege** → Zeile des Belegs (Name enthält „Nr. n“) → **Grund** „Testlauf“ → **Stornieren** → Dialog „Beleg stornieren?“ → **Stornieren** → Prüfung: Zeile mit Marker „storniert“ und neuer Stornobeleg (`PosViewModel.Day.cs`). Muss **innerhalb 15 Minuten** nach dem Kassieren laufen (`PosService.cs:1177`, `1230-1234`); im Lauf ist das gegeben.
- Parken: Rechnung mit Artikel → **Parken** → Prüfung: Eintrag unter **Geparkte Rechnungen** → Eintrag → weiterkassieren oder **Verwerfen** → Dialog „Rechnung verwerfen?“.
- Sonderfälle:
  - **Fremder Beleg** (Prio A, ✅): Beleg aus dem Demo-Verlauf (gestern, **Tag davor**) stornieren → „Stornieren darf die Standortleitung. Eigene Belege kann die Rezeption bis 15 Minuten nach dem Kassieren selbst stornieren.“ (403).
  - **Grund leer** (Prio A, ✅): **Stornieren** deaktiviert, mindestens 3 Zeichen (`PosViewModel.Day.cs:75,191`).
  - **Gutscheinbeleg nach Einlösung:** „Gutschein … wurde schon eingelöst; der Beleg kann nicht storniert werden.“ – mit dem Code aus Thema 10. Prio B, ✅.
  - **Entnahme als Rezeption** (Prio B, ✅): Reiter **Kassenlade & Abschluss** → Tab **Entnahme** (sichtbar!) → Betrag, Grund → **Buchen** → „Bargeld entnehmen darf die Standortleitung; Einlagen darf auch die Rezeption buchen.“ (`PosService.cs:1374`). Beleg für Lücke L6.

### Thema 12 `day-closing` – Einlage und Tagesabschluss (Prio B, ⚠️)

- Schritte: **Kassenlade & Abschluss** → **Einlage** 100,00, Grund „Wechselgeld“ → **Buchen** → Prüfung: Bewegung in der Liste → Block **Soll-Bestand** lesen → **Gezählter Bestand** = Soll → **Tagesabschluss durchführen** → Prüfung: „Tagesabschluss gespeichert“, „Keine Differenz“ (`PosViewModel.Day.cs:114,154`).
- ⚠️ Der Abschluss ist nicht rückgängig zu machen und verändert den Zustand der Demo-Kassa für alle weiteren Themen desselben Tages (neuer Zählstand ab null). **Immer als letztes Thema** laufen lassen, und der Bericht soll „Datenbank danach neu laden“ vermerken.
- Sonderfälle:
  - **Differenz** (Prio B, ✅): gezählt = Soll − 5 → „Differenz −5,00 €“, Abschluss trotzdem gespeichert (keine Sperre).
  - **Negativ:** „Gezählter Bestand kann nicht negativ sein.“ ✅
  - **Frühere Abschlüsse ansehen:** braucht `ViewAmountReports` → für die Rezeption nicht sichtbar; `ExpectMissing`. Lücke L8.

### Thema 13 `cancel` – Termin stornieren und zurücknehmen (Prio A, ✅)

- Schritte: Karte des Laufs (Gebucht oder Eingecheckt) → **Termin stornieren …** → Dialog `DialogCard` „Termin stornieren?“ mit Feld **Grund** „An der Rezeption storniert“ → `ConfirmButton` **Stornieren** → Prüfung: Toast „Termin storniert“ mit **Rückgängig** (`CalendarViewModel.cs:1449`), Karte verschwunden (`ExpectMissing`) → Panel **Stornierung zurücknehmen** → Dialog „Stornierung zurücknehmen?“ → **Zurücknehmen** → Karte wieder „, Gebucht“.
- Sonderfälle:
  - **Storno kurzfristig / Ausfallgebühr:** Der Dialog kennt keine Frist und keine Gebühr; **Ausfallgebühr belasten …** gibt es nur mit `SuperviseCash` (`AppointmentViewModel.CardGuarantee.cs:108`). Für die Rezeption: `ExpectMissing`. Lücke L3.
  - **Zurücknehmen, Zeit inzwischen belegt:** zwischen Storno und Zurücknahme einen Termin auf dieselbe Zeit buchen → Dialog „Zu anderer Zeit zurücknehmen?“ mit **Verschieben …** (`AppointmentViewModel.cs:290-296`). Prio B, ✅.
  - **Grund leer:** Feld geleert → **Stornieren** – ob ein leerer Grund angenommen wird, ist im Code nicht ausgeschlossen (nur der Vorbelegungstext); im Test prüfen. Prio C.
  - **Abbrechen mit Esc:** Dialog zu, Karte unverändert. ✅

### Thema 14 `inbox` – Anfragen bearbeiten (Prio B, ⚠️)

- Vorbedingung: Modul Terminannahme eingeschaltet; die Terminannahme verarbeitet die acht Demo-Anfragen beim Start, welche offen bleiben, hängt von KI-Schlüssel und Regelwerk ab (`testdaten.md`). Deshalb ⚠️: Das Thema wählt die **erste Nachricht mit Status „Prüfen“**, sonst meldet es „keine offene Anfrage“ als bestandenen Leerfall.
- Schritte: Anfragen → Schalter **Nur „Prüfen“** → erste Zeile → Prüfung: `MessageCard` sichtbar → Block **Wer ist das?**: Vorschlag **Zuordnen** → InfoBar „Zugeordnet: …“ → Spalte **Freie Zeit**: Standort Wien, Behandlung → erste Zeit → **Vormerken und vorschlagen** → Prüfung: „Vorgemerkt. Der Vorschlag steht im Entwurf: prüfen und senden.“ und `HoldCard` „Vorgemerkt: …“ → **Freigeben** → „Freigegeben. Die Zeit ist wieder frei.“ (damit der Lauf keine Zusage an Mailpit schickt, die später verwirrt; Senden ist Prio C).
- Sonderfälle:
  - **Buchen ohne Zuordnung:** vor **Zuordnen** vormerken → „Wer ist das? Bitte zuerst einen Vorschlag zuordnen oder den Gast bewusst neu anlegen; …“ (`MailIntakeEndpoints.cs:515`). ✅
  - **Englische Anfrage übersetzen:** Nachricht „Massage tomorrow?“ (James Miller) → `LanguageButton` **DE** → Text auf Deutsch; braucht KI-Schlüssel oder lokale KI, sonst ❌.
  - **Absage:** Mail „Absage für morgen“ → **Termin stornieren** → Dialog → „Termin storniert. Die Antwort wird neu entworfen.“ ⚠️ nur, wenn die Terminannahme den Termin erkannt hat.
  - **Treatwell:** Buchung TW-…-4711 → Abschnitt **In Treatwell noch annehmen** → **Erledigt** → „Als in Treatwell angenommen vermerkt.“ ⚠️ je nach Zustand nach dem Start.
  - **Senden ohne gespeicherten Entwurf:** **Senden** deaktiviert, Tooltip „Verfügbar, sobald der Entwurf gespeichert ist.“ ✅

### Thema 15 `group` – Paar und Spa-Tag (Prio B, ✅)

- Schritte: Kalender → Taste P oder **Termin ▾ → Paar oder Spa-Tag** → Panel „Paar oder Spa-Tag“ → **Wann** = nächster Tag 14:00 → **Gast 1** Stammgast A, Behandlung „Klassische Massage“ → **Gast 2** Schalter **Name folgt** (Name „Gast 2: Name folgt“, `CalendarView.axaml:1433`), Behandlung „Rückenmassage Express“ → Hauptknopf „… alles buchen“ → Prüfung: Toast „Gemeinsam gebucht: 2 Termine ab HH:mm“ (`NewEntry.cs:301`) und Karte „Gast 2 · Name folgt“ → Karte anklicken → Panel-Kasten „Paar“ mit **Alle einchecken (2)**, **Ganze Buchung stornieren** → **Ganze Buchung stornieren** → Dialog „Ganze Buchung stornieren?“ → **Stornieren** (aufräumen).
- Sonderfälle:
  - **Nur ein Teil:** Hinweis „Mindestens zwei Teile wählen, davon eine Behandlung.“ ✅
  - **Keine gemeinsame Zeit:** Wann = 19:45 → „An diesem Tag ist keine gemeinsame Zeit frei. Anderen Tag wählen.“ ✅ (Dauer überschreitet Schluss 20 Uhr).
  - **Namen nachtragen:** Rechtsklick **Namen nachtragen** → **Gast eintragen** → Karte mit Namen. ✅
  - **Ganze Buchung verschieben nach Einchecken:** Knopf fehlt (`AppointmentViewModel.cs:~541`) → `ExpectMissing`.

### Thema 16 `anamnesis` – Gesundheitsfragebogen (Prio B, ⚠️)

- Vorbedingung: Schlüssel für vertrauliche Daten eingerichtet, sonst gibt es keine Demo-Anamnesen (`DemoExtras.cs:293-296`); die Karte selbst gibt es immer.
- Schritte: Karte des Laufs → Panel-Karte `AnamnesisCard` → Prüfung: Text „Fehlt noch.“ (neue Kundin, `AppointmentViewModel.Anamnesis.cs:99-106`) → `TabletLinkButton` **Am Tablet ausfüllen** → Prüfung: Bild „QR-Code für das Tablet“ und Text beginnt mit „Gültig bis“ → `CloseTabletLinkButton` **Schließen** → `PaperSheetButton` **Papierbogen drucken** → Prüfung: kein Fehlertext in `AnamnesisMessageText` (der Bogen öffnet sich im Browser; Fenster danach schließen ist Sache des Testers, ⚠️).
- Sonderfälle:
  - **Stammgast mit Bogen:** Termin für Anna Bauer (einer der ersten 70 Gäste, `DemoExtras.cs:290-318`) → Text beginnt mit „Liegt vor:“ → `ShowRecordButton` **Antworten ansehen** → Kurztitel „Allergien“ sichtbar (die Rezeption hat `ViewHealthData`). ⚠️ Ob ein „Ja“ dabei ist, ist Zufall; nur Sichtbarkeit prüfen.
  - **Foto hochladen:** Dateidialog → ❌ für FlaUI (Windows-Dialog, Datei nötig); Prio C mit vorbereiteter Datei.
  - **Pille „Anamnese prüfen“:** beim Buchen nur bei Behandlung mit Merkmal und einem „Ja“ im Bogen → zufällig, ⚠️.
  - **Link senden** per Mail oder SMS gibt es nicht (Lücke L9).

### Thema 17 `calendar-extras` – Warteliste, Lücken, Pausen, Notizen (Prio B, ✅)

- Schritte: Rechtsklick auf freie Stelle einer Dienstspalte → **Notiz hier** → Panel „Notiz“ → Text „Testlauf“ → **Notiz speichern** → Toast „Notiz gespeichert“; Rechtsklick → **Pause hier (n Min.)** → **Pause eintragen** → Toast „Pause eingetragen“ → Karte „HH:mm Pause“ → Rechtsklick → **Pause löschen** (ohne Rückfrage!); Chip `HeadWaitlist` „Warteliste 3“ (drei Wiener Einträge, `DemoExtras.cs:86-94`) → Liste sichtbar → Esc; **Termin ▾ → Lücken schließen** → Panel „Lücken schließen“ mit Hinweis „Vorher mit dem Gast absprechen …“ → Esc (nicht verschieben).
- Sonderfälle: **Pause ohne Grund** – Sperrzeit verlangt Grund („Bitte einen Grund angeben.“, `CalendarEntryViewModel.cs:96-206`). **Pause löschen ohne Rückfrage** als Beleg für Lücke L10.

### Thema 18 `sick-leave` – Krankmeldung (Prio C, ❌ im Demo-Lauf)

- **… krank melden** speichert sofort ohne Rückfrage (`CalendarViewModel.Absence.cs:91-112`) und trägt einen Krankenstand in den Dienstplan ein; alle Termine der Person wandern in den Ausfall-Assistenten. Das verändert den Demo-Tag für alle folgenden Themen und lässt sich von der Rezeption nicht zurücknehmen (Dienstplan braucht `ViewStaffRecords`). Nur in einem eigenen Lauf mit anschließendem Neuladen der Datenbank. Prüfung dann: Toast „<Name> krank gemeldet“, Panel „Ersatz für …“.

### Thema 19 `sauna` – Sauna-Eintritt (Prio C, ⚠️)

- Taste S → Panel „Sauna und Ruheraum“ → Eintritt für Stammgast → Chip `HeadSauna` zählt hoch („Sauna 1 / 12“) → Kassa-Symbol „Eintritt an der Kassa abrechnen“ → Rechnung mit „Saunaeintritt“ (13 %). Eintritt in der Vergangenheit ist gesperrt (`CalendarEndpoints.cs:424`). ⚠️ Zahl im Chip ist von Demo-Eintritten abhängig.

### Thema 20 `offline` – ohne Verbindung (Prio C, ❌ ohne Testinfrastruktur)

- Braucht ein Trennen der API während des Laufs (Docker stoppen oder Firewall). Prüfung wäre: Banner „Keine Verbindung zum Server. …“ (`ShellViewModel.cs:407-413`), Buchen → Toast „Ohne Verbindung vorgemerkt …“, Karte „vorgemerkt“, **Kassieren** nicht möglich, nach Wiederverbinden „Gleicht ab“ → „Online“. Das Skript `live-tests.ps1` steuert Docker heute bewusst nicht (ADR 0090).

### Zusammenfassung Themen und Sonderfälle

| Prio | Themen (Keys) |
| --- | --- |
| A | signin, overview, customer-search, customer (+duplicate, minimal, empty), appointment (+conflict, past), checkin, reschedule, checkout, voucher, cancel |
| B | customer-badmail, appointment-off-duty/closed/no-free-time/room-blocked, pos-sale, receipt-void, day-closing, inbox, group, anamnesis, calendar-extras |
| C | sick-leave, sauna, offline, dazu Ziehen per Maus, Foto hochladen, Senden an Mailpit |

20 Themen; 72 Sonderfälle (die Aufzählungspunkte unter „Sonderfälle“ je Thema), davon 51 heute stabil prüfbar (✅), 12 mit Einschränkung (⚠️) und 9 heute nicht (❌).

**Was der Tester neu braucht** (für die zweite Sitzung): eine Prüfung „Element darf nicht da sein“ (`ExpectMissing`, mit kurzem Timeout); Rechtsklick (`Mouse.RightClick`) und Auswahl eines `ContextMenu`-Eintrags; Lesen eines Textwerts (Gutscheincode aus der Belegzeile, Soll-Bestand); Fokus auf die Kalenderfläche vor Tastenkürzeln; `LiveRunContext` um Kartenname/Beginn des gebuchten Termins und den Gutscheincode erweitern; eine Themen-Abhängigkeit („braucht `appointment`“) mit klarer Meldung, wenn sie fehlt; ein Vermerk „Datenbank danach neu laden“ bei `day-closing` und `sick-leave`. Zwei `AutomationProperties.Name` in der App wären hilfreich: am `SplitButton` **Termin** in der Kopfzeile (heute ohne Namen, `CalendarView.axaml:2099`) und am Toast-Text in `ToastBar` (`CalendarView.axaml:2937`).

## 3. Was ich vermisse

Aus Sicht der Rezeption, die den Tag damit verbringt. Nur, was die App heute nicht hat; alles mit Fundstelle. Wichtigkeit: hoch = kostet täglich Zeit oder Geld oder führt zu Fehlern am Gast; mittel = stört wöchentlich; niedrig = Komfort.

| Nr. | Was fehlt | Warum es im Alltag stört | Wichtig | Beleg |
| --- | --- | --- | --- | --- |
| L1 | **Kassieren prüft den Terminstatus nur in der Oberfläche.** Die Kassa-Liste „Heute abzurechnen“ bietet unter „Später heute“ auch nicht eingecheckte Termine an, und der Server nimmt jeden nicht stornierten Termin auf die Rechnung. Einchecken kennt keine Tagesregel. | Ein Gast, der gar nicht da ist, wird aus der Liste abgerechnet („der ist doch um 16 Uhr dran“), danach Storno mit Stornobeleg und Signatur. Das Einchecken von morgen passiert beim Blättern durch Versehen (E auf der falschen Karte am falschen Tag). | hoch | `PosDayList.cs:30`; `Pos/PosService.cs:401-414` (keine Statusprüfung); `Scheduling/BookingService.cs:404-445` (keine Datumsprüfung); Oberflächenregel `AppointmentViewModel.cs:408`. **Erledigt in Aufgabe B** der [Arbeitsanweisung](arbeitsanweisung-rezeption-luecken.md). |
| L2 | **Verschieben in die Vergangenheit ist nicht gesperrt.** Buchen ist es (409), Verschieben nicht; die Verschieben-Karte hat kein Mindestdatum. | Tippfehler beim Datum („+1 Tag“ gedrückt, dann Tag von Hand korrigiert) landet gestern; der Termin verschwindet aus dem heutigen Blick und niemand merkt es, bis der Gast dasteht. | hoch | `CalendarEndpoints.cs:210-217` gegenüber `:188`; `CalendarView.axaml:195` (kein `Minimum`); `PastBookings.cs` nur für Buchen. **Erledigt in Aufgabe A** der [Arbeitsanweisung](arbeitsanweisung-rezeption-luecken.md). |
| L3 | **Stornieren an der Rezeption kennt keine Frist und keine Ausfallgebühr.** Der Dialog hat nur „Grund“; **Ausfallgebühr belasten …** sieht nur, wer `SuperviseCash` hat; die Stornofrist gilt nur online. | Die Rezeption nimmt die Absage am Telefon entgegen und muss die Leitung holen, um die Gebühr zu belasten, oder vergisst es. Ein Hinweis „Absage unter 24 Stunden, Gebühr laut Regel: 50 %“ im Storno-Dialog würde reichen. | hoch | `AppointmentViewModel.cs:218-224` (Dialog), `AppointmentViewModel.CardGuarantee.cs:108` (Recht), `OnlineBookingEndpoints.cs:135` (Frist nur online). **Erledigt in Aufgabe C** der [Arbeitsanweisung](arbeitsanweisung-rezeption-luecken.md). |
| L4 | **Gutschein suchen geht nur an der Kassa über den vollständigen Code.** Keine Gutscheinliste, keine Suche nach Käuferin oder Restwert; die Kundenkarte zeigt nur die letzten Zeichen. | „Ich habe den Gutschein zu Hause vergessen, meine Mutter hat ihn gekauft“ – täglich. Heute: nichts tun oder Leitung fragen. | hoch | nur `GET /api/pos/vouchers/{code}` (`PosViewModel.OnlineInfo.cs:43-47`); `CustomersViewModel.Layout.cs:228` („…{letzte Zeichen}“); kein Listen-Endpunkt in `PosEndpoints.cs` |
| L5 | **Kein Hinweis beim Buchen für eine Kundin ohne Telefon und Mail.** Anlegen mit nur einem Namen ist richtig (Telefon-Anlage), aber beim Buchen fehlt ein Hinweis „Keine Kontaktmöglichkeit: Erinnerung und Zusage gehen nicht“. | Erinnerungen und Zusagen per Link laufen ins Leere; der Gast kommt nicht, wir wundern uns. | mittel | `CustomerEndpoints.cs:66-70`; `BookingViewModel.cs` ohne Prüfung; Erinnerungen nur an Gäste mit Mail (Handbuch `kalender-krankmeldung.md`). |
| L6 | **Tab „Entnahme“ ist für die Rezeption sichtbar und schlägt erst beim Buchen fehl.** | Jede neue Kollegin probiert es, bekommt eine rote Meldung und glaubt, sie habe etwas kaputt gemacht. Der Tab sollte ausgeblendet oder deaktiviert sein, wie es das Handbuch beschreibt. | niedrig | `PosView.axaml:884-885` (kein `IsVisible`-Binding), `PosService.cs:1374`; Handbuch `seite-kassa.md` sagt „sehen nur Standortleitung und Inhaber“ |
| L7 | **Keine Rückgabe einzelner Artikel.** Nur das Storno des ganzen Belegs; nach 15 Minuten nur die Leitung. | Gast bringt am nächsten Tag die Creme zurück: Leitung holen, ganzen Beleg stornieren (auch die Behandlung), alles neu kassieren. | mittel | kein Retouren-Endpunkt in `src/AispaStudio.Api/Pos`; `PosService.cs:1177`, `:1230-1234` |
| L8 | **Die Rezeption sieht ihre eigenen früheren Tagesabschlüsse nicht.** Abschluss darf sie, die Liste braucht `ViewAmountReports`. | Nach einer Differenz am Vortag kann sie nicht nachsehen, was sie gezählt hat. Eine Fassung „ohne Beträge, nur Datum und ob Differenz“ wäre genug. | mittel | `PosEndpoints.cs:279-283` gegenüber `:286-291` |
| L9 | **Anamnese-Link nur als QR-Code oder Papier, nicht senden.** Kein „Link per Mail/SMS an den Gast“ aus dem Termin. | Bei Telefonbuchung für einen Neukunden müsste der Bogen vorab kommen; heute füllt er ihn im Haus aus und der Termin beginnt später. | mittel | `CalendarView.axaml:354-402` (nur `TabletLinkButton`, `PaperSheetButton`, `UploadPhotoButton`); `Online/AnamnesisStaffEndpoints.cs` ohne Versand |
| L10 | **Krank melden, Pause löschen und Sperrzeit löschen haben keine Rückfrage, und Erledigt/Nicht erschienen/Krank melden kein Rückgängig.** | Ein Fehlklick im Rechtsklick-Menü am Spaltenkopf meldet eine Kollegin krank und schiebt alle Termine in den Assistenten; die Rezeption kann das nicht zurücknehmen (Dienstplan braucht andere Rechte). | hoch | `CalendarViewModel.Absence.cs:91-112`; Rückgängig nur für Verschieben, Stornieren, Einchecken, Lücke (`CalendarViewModel.cs:1406-1460`); Dienstplan-Rechte `Roles.cs:25`. **Erledigt in Aufgabe D** der [Arbeitsanweisung](arbeitsanweisung-rezeption-luecken.md) (Pause und Sperrzeit fragten schon nach, `CalendarViewModel.Notes.cs` `AskDeleteBlockAsync`). |
| L11 | **Kein Erfolgs-Toast beim Buchen aus dem Panel.** Das Panel schließt, die Karte erscheint irgendwo im Tag; bei vollem Tag ist sie nicht sofort zu sehen. | Die Kollegin bucht doppelt, weil sie nicht sicher ist, ob es geklappt hat. Die Freie-Zeit-Suche hat den Toast („Gebucht: …“), das Panel nicht. | mittel | `NewEntry.cs:256-263` gegenüber `CalendarViewModel.FreeTime.cs:178-182` |
| L12 | **Keine Terminbestätigung zum Drucken oder manuell senden.** Drucken gibt es nur für den Anamnese-Papierbogen; „Terminerinnerungen“ ist eine Verlaufsliste ohne Knopf. | Ältere Gäste wollen einen Zettel; Gäste ohne Mail wollen eine SMS „jetzt gleich“. | mittel | `AppointmentViewModel.Anamnesis.cs:213` (einziges Drucken); `CalendarView.axaml:319-330` (Erinnerungen nur Liste) |
| L13 | **Dublettenhinweis in der Kundenmaske ohne Grund und ohne Zusammenführen.** Die Kalender-Neuanlage nennt Gründe („gleiche Telefonnummer“), die Kundenmaske nur Namen. Zusammenführen oder Löschen einer Dublette gibt es für niemanden. | Nach einem Monat gibt es „Huber, Anna“ dreimal; Verlauf, Sterne und Rabatt verteilen sich. Die Rezeption kann nur im Rezeptionsvermerk „siehe andere Karte“ schreiben. | mittel | `NewCustomerMatchesViewModel.cs` gegenüber `MailIntake/CustomerMatcher.cs:69-169`; kein DELETE/Merge in `CustomerEndpoints.cs`; [uebergabe.md](../uebergabe.md) Abschnitt 5 („Dubletten zusammenführen“ geplant) |
| L14 | **Pakete lassen sich beim Buchen nicht wählen** und fehlen in den Demo-Daten. Nur die Kassa zieht ab; am Termin steht „P“ erst ab Restmenge 1 oder kurz vor Ablauf. | „Der Gast hat noch 3 Massagen im Paket, buch ihm die nächste“ – die Rezeption sieht das beim Buchen nicht und verspricht einen Preis, den die Kassa dann anders rechnet. | niedrig | `BookingViewModel.cs` ohne Paketbezug; `DayLayout.cs:622-640`; `Demo/Memory/MemoryDemoOps.Pos.cs:188` |
| L15 | **Handbuch und App weichen ab**: „Termin anlegen“ vs. Knopf **Termin**; Entnahme „sehen“; Anamnese-Rechte der Rezeption („Ansehen“/„Prüfen“ laut Handbuch nicht sichtbar, im Code sichtbar); Toast „… eingecheckt.“ mit Punkt; „Ganze Buchung stornieren ohne Rückfrage“; Gutschein-Beispiel „GS-7K3M-Q9“ gegen Serverformat 4-4-4. | Die neue Kollegin liest das Handbuch in der Leiste „KI und Hilfe“ und findet den Knopf nicht. | mittel | `kalender-termin-buchen.md` vs. `CalendarView.axaml:2105`; `seite-kassa.md` vs. `PosView.axaml:884`; `kalender-termin-anamnese.md:38,48` vs. `AccessContracts.cs:79`; `kalender-status-setzen.md` vs. `CalendarViewModel.Problems.cs:105-109`; `AppointmentViewModel.cs:586-592`; `PosView.axaml:649` vs. `PosService.cs:1898-1912` |
| L16 | **Tastatur:** Taste **V** zeigt nur einen Hinweis-Toast statt die Verschieben-Karte zu öffnen und steht weder in der Tastenhilfe noch im Handbuch; Buchstaben-Kürzel greifen nicht, sobald ein Feld im Seitenpanel den Fokus hat (erst Esc oder Klick in den Kalender). | Beim Telefonat tippt man im Panel, will dann E oder K drücken, und nichts passiert. | niedrig | `Keyboard.cs:555, 799-803` (V); `CalendarView.axaml.cs:410-411` (Fokus) |

**Die drei wichtigsten Lücken** für den Auftraggeber: L1 (Kassieren ohne Anwesenheit, Einchecken ohne Tagesregel), L3 (Storno ohne Frist und Gebühr an der Rezeption) und L10 (Krank melden ohne Rückfrage und ohne Rückgängig). L2 (Verschieben in die Vergangenheit) ist eine kleine Änderung mit großer Wirkung und sollte gleich mit.

## 4. Offene Punkte für die Umsetzungssitzung

- Ingrid Wallner durch einen Stammgast ersetzen (Thema 5) und in ADR 0090 sowie `docs/arbeitsweise.md` nachziehen.
- ADR 0090, Abschnitt „Kassieren ist noch nicht dabei: Es braucht „Ist da“, und das geht nur am Tag des Termins“: nach diesem Befund korrigieren (Einchecken heißt der Knopf, eine Tagesregel gibt es nicht; Kassieren ist über den Kalender-Knopf auch für morgen prüfbar).
- Reihenfolge im Lauf: `day-closing` immer zuletzt, `sick-leave` nur allein; beide mit Vermerk zum Neuladen im Bericht.
- Für Thema 14 und 16 vor dem Lauf prüfen, ob Terminannahme und Gesundheitsschlüssel im Testbetrieb eingerichtet sind; sonst melden die Themen „nicht anwendbar“ statt zu scheitern.
