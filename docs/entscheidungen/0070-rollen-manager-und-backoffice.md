# 0070. Rollen Manager und Backoffice, Recht „Berichte mit Beträgen“

- **Status:** angenommen
- **Datum:** 22.10.2026
- **Entschieden von:** Auftraggeber (Vorgaben der 5. Runde, Ä37), Ausgestaltung Claude

## Kontext

Vorgaben des Auftraggebers (Runde 5, Block J, Ä37), wörtlich: „manager darf gleich viel wie inhaber“, „backoffice nur das was eine buchhaltung oder lohnverrechnung brauchen würde“ und „Kennzahlen wie Umsätze oder generell wenn Auswertungen mit Beträgen über den Betrieb gemacht werden sollte nur der Inhaber oder ein Rezeptionist mit Berechtigung machen dürfen.“

Bisher gibt es vier feste Rollen ([ADR 0003](0003-rollenbasierte-oberflaechen.md)): Inhaber, Standortleitung, Rezeption, Therapeut:in. Die Standortleitung heißt im Code `StaffRole.Manager`. Ihr gehört das Recht `ViewFinance`: Umsätze, Kosten, Buchhaltung und Exporte der eigenen Standorte. Eine Geschäftsleitung neben dem Inhaber und ein Zugang für Buchhaltung und Lohnverrechnung fehlen, und Umsätze sehen heute alle mit `ViewFinance`, die Rezeption nie.

## Optionen

| Option | Bewertung |
| --- | --- |
| Nur Einzelrechte je Zugang statt neuer Rollen | beliebig einstellbar, aber für Spas bis 50 Personen schwer zu überblicken; widerspricht der festen Rechtetabelle in `Roles.cs`; jeder Zugang müsste von Hand zusammengestellt werden |
| Eine Rolle „Backoffice“ für alles, was keine Kundendaten braucht (inklusive Einstellungen und Dienstplan schreiben) | zu mächtig; Buchhaltung und Lohn brauchen nur Lesen und Exportieren |
| Die Standortleitung in „Manager“ umbenennen und den Wert in der Datenbank ändern | vermischt zwei verschiedene Dinge (Leitung eines Standorts, Geschäftsleitung); Migration der Rollenspalte in Zugängen und Einladungen; Wort des Auftraggebers „Standortleitung“ steht in der Oberfläche und im Handbuch |
| Zwei neue Rollen `GeneralManager` („Manager“) und `Backoffice`, ein neues Recht `ViewAmountReports`, dazu zwei Lese-Rechte für das Backoffice | gewählt |

## Entscheidung

### Rollen und Namen

- **`StaffRole.GeneralManager`** (Oberfläche: „Manager“): dieselben Rechte wie der Inhaber (`Roles.RightsOf` liefert dieselbe Liste). Der Name `Manager` ist im Code an die Standortleitung vergeben (gespeicherter Wert, Hilfe-Kennung `leitung`); der Manager heißt deshalb im Code `GeneralManager`, in der Oberfläche „Manager“, als Hilfe-Kennung `manager`.
- **`StaffRole.Backoffice`** (Oberfläche: „Backoffice“): liest und exportiert, was Buchhaltung und Lohnverrechnung brauchen; schreibt nichts. Gilt immer für **alle Standorte**, weil Buchhaltung und Lohn fürs ganze Unternehmen gelten (der Server lehnt ein Backoffice für einzelne Standorte ab).
- Gespeichert wird die Rolle wie bisher als Text; neue Werte brauchen keine Migration.

### Was nur der Inhaber darf (Inhaber bleibt Eigentümer des Kontos)

- **Inhaber- und Manager-Zugänge** anlegen, ändern, sperren, entfernen und einladen: `UserAccess.CanManage` lässt für Ziele der Rollen `Owner` und `GeneralManager` nur den Inhaber zu. Ein Manager sieht diese Zugänge nicht in der Liste, bekommt beim Ändern 404 und beim Einladen eine Fehlermeldung. Manager und Standortleitung vergeben die übrigen Rollen, die Standortleitung nur an ihren Standorten (wie bisher).
- **Lizenz und Abrechnung kündigen:** gibt es heute noch nicht als Funktion. Wer sie baut, prüft `UserAccess.IsOwner`, nicht ein Recht; der Manager hat alle Rechte, soll das aber nicht dürfen.
- **Unbefristet:** Inhaber-Zugänge sind nie befristet. Das gilt nicht für Manager-Zugänge: sie dürfen befristet sein (z. B. interimistische Geschäftsführung). Die PIN für den Schnellwechsel hat für Inhaber **und** Manager mindestens sechs Ziffern ([ADR 0042](0042-schnellwechsel-pin-nur-am-empfangsgeraet.md)).
- Alles andere, was `ManageOrganization` verlangt (Standorte anlegen, Firmendaten, Zahlungskonto, Kassa-Einstellungen, Kontenzuordnung, Lohn-Einstellungen), darf der Manager.

### Neues Recht „Berichte mit Beträgen“ (`AccessRight.ViewAmountReports`)

Es trennt Auswertungen mit Geldbeträgen von Auswertungen ohne Beträge. Die Prüfung sitzt im Endpunkt (`RequirePermission`) bzw. im Berichtsdienst, nicht in der Oberfläche.

| Wer | Berichte mit Beträgen |
| --- | --- |
| Inhaber, Manager, Backoffice | immer |
| Standortleitung, Rezeption | nur, wenn dem Zugang das Recht einzeln vergeben ist (Häkchen „Berichte mit Beträgen“ in „Zugänge“; Spalte `AmountReports` an Zugang und Einladung) |
| Therapeut:in | nie |

Vergeben und entziehen dürfen es nur **Inhaber und Manager**; sonst könnte sich eine Standortleitung die Umsätze selbst freigeben. Es wirkt sofort. Wer es hat, hat auch `ViewFinance` (Berichte ohne Beträge).

`ViewFinance` bleibt als **Kennzahlen ohne Beträge** erhalten (Management-Cockpit, Pflichtberichte, Vergleich mit anderen Spas), das Recht der Standortleitung. Zuordnung der Endpunkte:

| Auswertung | Endpunkt | Recht |
| --- | --- | --- |
| Management-Cockpit (Auslastung, Behandlungen, Quoten, Gäste) | `GET …/reports/locations/{id}/management` | `ViewFinance`; **Beträge** (Umsatz-Kennzahlen, Umsatz nach Konto und Zahlart, Umsatz je Person und Behandlung, Margen, Warnungen mit €) nur mit `ViewAmountReports`, sonst fehlen sie in der Antwort (`AmountsHidden`); Behandlungen sind dann nach Anzahl geordnet |
| Pflichtberichte | `GET …/compliance` | `ViewFinance`; Summe der offenen Gutscheine nur mit `ViewAmountReports`; Anzahl offener Gutscheine, Pakete und Arbeitszeiten bleiben |
| Vergleich mit anderen Spas | `GET …/benchmark` | `ViewFinance`; Kennzahlen in Euro nur mit `ViewAmountReports` |
| Kosten (Vollkosten, Kostensätze, Material) | `/api/costs` | lesen `ViewAmountReports`; ändern zusätzlich `ManageCatalog` |
| Buchhaltung (Buchungen, Export, Einstellungen lesen) | `/api/accounting` | `ViewAmountReports`; Einstellungen und Erlöskonten ändern wie bisher `ManageOrganization` bzw. `ManageCatalog` |
| DEP, DSFinV-K, TSE-Export | `/api/pos/registers/{id}/dep` u. a. | `ViewAmountReports` (auch für das Backoffice, das nicht kassieren darf) |
| Letzte Tagesabschlüsse mit allen Zahlen | `GET …/registers/{id}/day-closings` | `ViewAmountReports`; den Tagesabschluss selbst macht weiter, wer kassieren darf |
| Umsatz und Ausgaben je Gast (Kundenkarte), Umsatz je Person | `GET /api/customers/{id}/insights` | Umsätze nur mit `ViewAmountReports` |
| KI-Assistent „Zahlen“ | Antwort auf Umsatz-Fragen | nur mit `ViewAmountReports` |

Die Windows-App zeigt „Umsatz laut Kalender“ nur mit diesem Recht; die Berichte der Team-Oberfläche (auch eingebettet in der Windows-App) blenden Beträge ohne Recht aus (Spalten weg) und zeigen „Beträge nur mit Berechtigung“.

**Folge für bestehende Standortleitungen:** Sie sehen Kosten, Buchhaltung, Umsätze im Cockpit und die Exporte nicht mehr, bis ihnen das Recht vergeben ist (Vorgabe des Auftraggebers). Auslastung, Anzahlen und Quoten bleiben.

### Rolle Backoffice im Einzelnen

Rechte: `ViewFinance`, `ViewAmountReports`, `ViewReceipts`, `ViewStaffRecords`. Die beiden letzten sind neu und nur lesend:

- **`ViewReceipts`:** Kassen, Belege (auch Belegdruck) und Tagesabschlüsse lesen. Nicht: kassieren, Rechnungen, Abschluss, Einlage und Entnahme, Zwischenbericht, Storno.
- **`ViewStaffRecords`:** Dienstplan, Zeiten (Stempelzeiten, Abwesenheiten, Urlaub), Stammdaten des Teams (Name, Qualifikationen, Arbeitszeiten; enthält weder Gesundheits- noch Zugangsdaten) und den Lohn (Einstellungen, Personalnummern, Monatswerte, Export) lesen. Die Standortliste liest das Backoffice mit `ViewFinance`. Nicht: Dienstplan planen, Zeiten nachtragen, Personalnummern oder Lohn-Einstellungen ändern.

Technik: `AlsoAllow(alternative, insteadOf)` am **einzelnen Endpunkt** lässt zusätzlich zum Gruppenrecht ein zweites Recht zu. Die Gruppen der Kassa (`Book`), des Dienstplans (`ViewCalendar`) und der Stammdaten behalten ihre Schreib-Endpunkte unberührt; was nicht ausdrücklich freigegeben ist, bleibt für das Backoffice 403.

**Kalender lesen: weggelassen.** `ViewCalendar` öffnet nicht nur den Kalender, sondern auch Kunden (Kontaktdaten, Kundenkarte, Notizen, Kennzeichen), Anamnese-Stand und Termine mit Namen. „Keine Kundendaten außer dem Belegbezug“ lässt sich damit nicht einhalten, und ein eigenes Lese-Recht nur für den Kalender ohne Namen gäbe es nur mit einem zweiten Kalender-Endpunkt. Das Backoffice braucht für Buchhaltung und Lohn keinen Kalender; die Belege nennen den Gast nur, wo er auf dem Beleg steht (Rechnungsempfänger).

### Wer darf was (Rolle × Recht)

| Recht | Inhaber | Manager | Standortleitung | Rezeption | Therapeut:in | Backoffice |
| --- | --- | --- | --- | --- | --- | --- |
| `ViewCalendar` Kalender, Team, Behandlungen, Kunden sehen | ja | ja | ja | ja | ja | nein |
| `Book` buchen, kassieren | ja | ja | ja | ja | nein | nein |
| `ChangeStatus` Status der Termine | ja | ja | ja | ja | ja (eigene) | nein |
| `ManageLocation` Öffnungszeiten, Räume, Dienstplan, Lager | ja | ja | ja | nein | nein | nein |
| `ManageCatalog` Team, Behandlungen, Kosten ändern | ja | ja | ja | nein | nein | nein |
| `ManageStaff` Zugänge (ohne Inhaber und Manager) | ja (alle) | ja | ja | nein | nein | nein |
| `ManageOrganization` Standorte, Firmendaten, Zahlungen, Lohn ändern | ja | ja | nein | nein | nein | nein |
| `ViewFinance` Berichte ohne Beträge | ja | ja | ja | mit Beträge-Recht | nein | ja |
| `SuperviseCash` Kassenaufsicht | ja | ja | ja | nein | nein | nein |
| `ViewAmountReports` Berichte mit Beträgen | ja | ja | nur vergeben | nur vergeben | nein | ja |
| `ViewReceipts` Belege und Tagesabschlüsse lesen | ja | ja | (über `Book`) | (über `Book`) | nein | ja |
| `ViewStaffRecords` Dienstplan, Zeiten, Team, Lohn lesen | ja | ja | (über `ViewCalendar`, `ManageLocation`) | (über `ViewCalendar`) | (über `ViewCalendar`) | ja |

„Inhaber und Manager anlegen, ändern, entfernen“ und „Lizenz kündigen“ stehen nicht in der Tabelle, weil sie keine Rechte sind, sondern Eigenschaft des Inhabers (`UserAccess.IsOwner`).

### Oberfläche

- „Zugänge“ (Web, auch eingebettet in der Windows-App): die Rollenauswahl bietet beide neuen Rollen, mit Kurzbeschreibung unter dem Feld und Fragezeichen zur Rollenhilfe ([ADR 0055](0055-hilfe-zu-jeder-einstellung-im-repository.md)). Der Inhaber sieht alle Rollen; Manager und Standortleitung nicht „Inhaber“ und „Manager“. Bei Standortleitung und Rezeption erscheint das Häkchen „Berichte mit Beträgen“ (für andere nur lesbar). Backoffice zeigt statt der Standorte den Hinweis „für alle Standorte“.
- Das Backoffice sieht in der Verwaltung Dienstplan, Zeiten (nur lesen), Berichte, Kosten und Buchhaltung (nur lesen, Export), Lohn (nur lesen, Export). Die Windows-App zeigt ihm keinen Kalender, keine Kunden und keine Kassa, nur Verwaltung und Hilfe.
- Windows-App: eigener Auswahl-Text in der Anmeldung; Testkonten für beide Rollen.

### Beispieldaten

Zwei fiktive Konten, beide für alle Standorte: Susanne Leeb (Manager, `manager@demo.aispa.test`, PIN 123456) und Gerhard Zehetner (Backoffice, `backoffice@demo.aispa.test`, PIN 1234). Keines der vorhandenen Konten bekommt das Recht „Berichte mit Beträgen“; der Inhaber vergibt es in der Demo unter „Zugänge“.

## Begründung

Eine Geschäftsleitung ohne die Eigentümerrechte und ein lesender Zugang für Buchhaltung und Lohn sind zwei übliche Rollen in kleinen Betrieben; beide lassen sich mit der festen Rechtetabelle ausdrücken, ohne frei einstellbare Rechte einzuführen. Die Trennung der Beträge ist die einzige Einzelvergabe, weil der Auftraggeber genau sie verlangt hat („Rezeptionist mit Berechtigung“). Das Recht am Endpunkt statt in der Oberfläche zu prüfen entspricht der Regel des Projekts, dass der Server bei jedem Aufruf prüft. Der neue Name `GeneralManager` vermeidet eine Umbenennung von gespeicherten Werten.

## Folgen und Stand

- Gebaut auf `dev`. Migration `RollenManagerBackoffice` fügt `AmountReports` (bool, Standard `false`) zu `StaffAccess` und `Invitations` hinzu.
- Tests: `RolesTests` (Standardrechte je Rolle als Tabelle, Inhaber/Manager-Regeln, Backoffice lesend und abgewiesen, Rezeption und Standortleitung mit und ohne Recht), Web (`StaffPage`, Berichte, Embedded-Navigation) und Windows-App (`RoleTests`).
- Hilfe: Thema „Zugänge“ (Rollen, „Berichte mit Beträgen“), Seiten und Abläufe zu Berichten, Kosten, Buchhaltung, Lohn, Zeiten und Dienstplan mit den neuen Rechten.
- **Offen (Fragen an den Auftraggeber):** Soll das Backoffice auch ein eigenes Konto-Kennzeichen für den Steuerberater (extern, befristet) bekommen? Der Zugang lässt sich schon befristen. Soll ein Backoffice einzelne Standorte bekommen können (heute nur alle)? Soll der Manager die Lizenz einsehen dürfen, sobald es die Funktion gibt?
- Die Funktion „Lizenz kündigen“ gibt es noch nicht; die Prüfung auf den Inhaber ist vorbereitet (`UserAccess.IsOwner`).

## Nachtrag Runde 5b, Block L3 (22.10.2026): Auswertungen ohne Beträge für Therapeut:innen, abschaltbar (Ä55)

Seit Block K3 beantwortet der Assistent die Auswertungen ohne Beträge (Top-Behandlungen, Auslastung, Termine je Person, Nicht-erschienen, Top-Gäste nach Besuchen) jedem Zugang mit dem Kalenderrecht, also auch der Rolle Therapeut:in. Auftraggeber: „default ja, aber abschaltbar“.

- **Schalter je Zugang:** `PlainReports` (Standard `true`) an `StaffAccess` und `Invitation`, im Vertrag an `StaffAccessInput`, `StaffAccountInput`, `InvitationInput`, `StaffMemberDto` und `InvitationDto`, im Zugang der Anfrage `UserAccess.PlainReports`. Er wirkt **nur bei der Rolle Therapeut:in**; bei den anderen Rollen ist er ohne Wirkung und wird immer als `true` gespeichert (`StaffEndpoints.PlainReportsOf`). Bestehende Zugänge bekommen per Migration `ZugangAuswertungenOhneBetraege` den Wert `true`, ihr Verhalten ändert sich nicht.
- **Prüfung am Server:** `UserAccess.CanSeePlainReports` (Berichtsrecht, oder Kalenderrecht, bei Therapeut:in nur mit `PlainReports`); der Antwortgeber für Auswertungen (`ReportAnswers`) antwortet sonst mit „Dafür fehlt die Berechtigung.“ wie bei fehlendem Recht auf Beträge. Rezeption, Standortleitung, Manager, Inhaber und Backoffice bleiben unberührt.
- **Wer ihn setzt:** wer den Zugang verwalten darf (`ManageStaff`, `CanManage`): Inhaber, Manager und die Standortleitung für Zugänge an ihren Standorten (einen Zugang für alle Standorte verwaltet sie nicht). Anders als „Berichte mit Beträgen“ ist keine Inhaber-Rolle nötig, weil dabei kein Recht auf Beträge freigegeben wird; es wird nur ein Recht auf Zahlen ohne Beträge entzogen.
- **Oberfläche:** in der Zugangsverwaltung (Web, in der Windows-App eingebettet) ein Häkchen „Auswertungen ohne Beträge im Assistenten“ mit Fragezeichen (Hilfe `zugaenge#auswertungen-ohne-betraege`), nur bei der Rolle Therapeut:in, neben dem Häkchen „Berichte mit Beträgen“, das es dort nicht gibt.
- Tests: `BusinessReportTests` (Standard an gibt Antwort, aus wird abgelehnt, andere Rollen unberührt, Standortleitung setzt nur für ihre Standorte und es wirkt sofort, andere Rollen speichern ihn nie als aus), Web `StaffPage.test.tsx`.

## Quellen

- [ADR 0003](0003-rollenbasierte-oberflaechen.md), [ADR 0042](0042-schnellwechsel-pin-nur-am-empfangsgeraet.md), [ADR 0055](0055-hilfe-zu-jeder-einstellung-im-repository.md)
- [Architektur: Rollen und Rechte](../architektur.md#rollen-und-rechte)
- [Hilfe: Zugänge](../../web/src/help/content/zugaenge.md)
- Code: `src/AispaStudio.Api/Access/Roles.cs`, `PermissionFilter.cs`, `StaffEndpoints.cs`
