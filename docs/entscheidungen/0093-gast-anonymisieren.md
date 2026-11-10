# 0093. Gast anonymisieren und weich löschen

- **Status:** angenommen
- **Datum:** 10.11.2026
- **Entschieden von:** Auftraggeber (Vorgabe und Befragung vom 10.11.2026), Umsetzung Claude

## Kontext

Bisher ließ sich ein Gast weder löschen noch anonymisieren ([ADR 0082](0082-aenderungsverlauf-ueber-den-aenderungs-trigger.md) Punkt 7, `docs/stand-und-offene-punkte.md`). Vorgabe des Auftraggebers (10.11.2026): „Laut Datenschutz nur soweit Anonymisierung und softdelete“, „Man muss halt immer die aufbewahrungsfristen im Kopf haben“, „Dehne die DSG soweit wie möglich“. Gemeint ist: so viel aufbewahren, wie das Recht erlaubt, aber keine Klardaten behalten, die keine Frist deckt.

Die rechtlichen Grenzen kommen aus der Recherche vom 10.11.2026:
- Belege sind 7 Jahre aufzubewahren (§ 132 BAO, RKSV).
- Ansprüche verjähren nach 3 Jahren (§ 1489 ABGB).
- „Auf Vorrat behalten“ deckt Art. 17 Abs. 3 DSGVO nach der Linie der Datenschutzbehörde nicht.

## Optionen

| Option | Bewertung |
| --- | --- |
| Hart löschen (Zeile `Customers` entfernen) | verletzt die Aufbewahrung: Belege und Termine verlören ihren Bezug; vom Auftraggeber ausgeschlossen |
| Nur weich löschen (Marke, Daten bleiben) | Klardaten ohne Zweck bleiben gespeichert; keine Löschung im Sinne der DSGVO |
| Anonymisieren und weich löschen | gewählt: Zeile und Bezüge bleiben, Personenbezug fällt weg |

## Entscheidung

1. **Ablauf in einer Transaktion:** `POST /api/customers/{id}/anonymize` (`CustomerAnonymizer`).
   - Der Gast heißt danach „Gelöschter Gast“, `DeletedAt` ist gesetzt.
   - Kontakt, Adresse, Geburtstag, Notizen und Vorlieben sind geleert.
   - Gelöscht werden Zusatzadressen, Kennzeichen, Namensänderungen, Einwilligungen, Erinnerungen, Anamnese mit Scans, Online-Konto, Zugangslinks und offene Wartelisteneinträge.
   - Geleert wird Freitext an Terminen, Serien, Bereichsbesuchen, Nachfassern, Anfragen und Online-Bestellungen.
2. **Was bleibt (Aufbewahrung):** Termine, Rechnungen, Gutscheine und die Kassa-Belege mit Rechnungsempfänger (`ReceiptRecipients`) als Belegkopie nach BAO. Die Belege sind ohnehin nur zum Einfügen da.
3. **Verlauf:** Werte leeren, Zeilen behalten. Der verzögerte Constraint-Trigger `aispa_history_anonymize` ruft beim Commit `sync.scrub_customer_history`.
   - Bei Kunden und ihren Mailadressen fallen alle Werte weg.
   - Bei Terminen fallen nur Notiz und Stornogrund weg; „Storniert von Lena“ bleibt.
   - Der Trigger läuft auch auf der Gegenseite des Abgleichs, weil der Verlauf nicht abgeglichen wird.
4. **Rechte und Sperren:** nur Inhaber und Geschäftsführung (`IsOwnerLevel`).
   - Mit 409 und Hinweis sperren künftige Termine (auch Besuche in Bereichen), offene Rechnungen und laufende oder fehlgeschlagene Online-Erstattungen. Die Erstattung sperrt, weil Geld für den Gast offen ist und ihre Bestätigung an die gespeicherte Adresse geht.
   - Gutscheine sind Inhaberpapiere und sperren nicht.
   - Ein anonymisierter Gast lässt sich nicht mehr ändern (404).
5. **Kein globaler Abfragefilter:** Gezielt `DeletedAt == null` an Such- und Auswahlstellen (Kundensuche, Buchen, Zuordnung von Nachrichten, Assistent, Online, Marketing, Kassa). Lesestellen für alte Termine, Belege, Berichte und den Verlauf bleiben ungefiltert und zeigen „Gelöschter Gast“.
6. **Von Hand zuerst:** Die Automatik nach Inaktivität ist eine eigene Folgeaufgabe; sie nutzt denselben Baustein.

## Begründung

- Ein globaler Filter würde über die Pflicht-Navigation `Appointment.Customer` auch die alten Termine ausblenden und Berichte verfälschen.
- Das Leeren der Werte statt Löschen der Zeilen hält fest, wer wann was getan hat (Nachvollziehbarkeit für Kassa und Team), ohne Personenbezug.
- Anamnese ist Gesundheitsdatum (Art. 9 DSGVO) ohne Aufbewahrungsfrist im Spa und fällt deshalb ganz.

## Folgen und Stand

- **Vollständigkeitsprobe** (`CustomerAnonymizeTests`): Ein Gast mit eindeutigem Nachnamen und eindeutiger Nummer steht vorher in 23 Tabellen, danach nur noch in `aispa.ReceiptRecipients`. Beim Bau fand die Probe weitere Stellen, die jetzt mit geleert werden:
  - Anmeldecodes des Kundenbereichs (`online.SignInCodes`, über die Adresse)
  - die Kennung beim Anbieter im Änderungsprotokoll des Abgleichs (`sync.changes` zu `CustomerAccountLogins`)
  - Schließ-Hinweise an den Gast (`ClosureNotices`)
  - Erstattungen
  - der Gast als Kandidat in fremden Anfragen
- **Nach dem Merge mit `dev` (Exporte #18, PDF-Ausgaben #19):**
  - Die Kundenliste als Export enthält keine anonymisierten Gäste, und ihre vCard liefert 404.
  - Gesendete Terminbestätigungen und Belege (`SentDocuments`) verlieren Adresse und Betreff; der Verlauf sagt dann nur „Bestätigung per Mail gesendet“.
  - Der Verlauf der Werbe-Einwilligungen verliert seine Werte, darunter die Adresse.
  - Der Abgleich beim Kundenimport vergleicht nur Gäste mit Telefon oder E-Mail und trifft anonymisierte Gäste deshalb nie.
- Bewusst nicht angefasst: die Firmenprüfung des Beleg-Empfängers (`VatIdChecks`, gehört zur Belegkopie) sowie Freitext des Teams ohne Zuordnung zum Gast (Kassabewegungen, Kartenzahlungen, Erstattungsgrund, Kalendernotizen, Briefings).

- `sync.forget_customer_history` (Löschen mit dem Gast, ADR 0082 Punkt 7) bleibt für das harte Löschen bestehen; das Anonymisieren nutzt stattdessen das Bereinigen.
- ADR 0061 ist angepasst: Anonymisierte Gäste sind keine Kandidaten mehr.
- Offen:
  - die Automatik nach Inaktivität
  - Auskunft und Export für den Gast (Art. 15 und 20 DSGVO)
  - eine Frist für Anfragen ohne Gast
  - das Anonymisieren im Web
  - Freitext ohne Gastbezug, etwa Kalendernotizen
  - Die Endpunkte des Teams für Anamnese-Scans lassen für einen anonymisierten Gast noch neue Scans zu.
  - Die Offline-Kopie der Windows-App (`offline.bin`, `OfflineCache`) behält ältere Antworten mit dem Klarnamen. Betroffen sind Suchlisten und Kundenlisten, bis sie neu geladen werden. Spätestens nach 14 Tagen (`MaxAge`) oder beim Abmelden sind sie weg, und sie liegen verschlüsselt (DPAPI) auf dem Gerät. Die Offline-Suche findet den Gast bis dahin. Folgeaufgabe: Einträge mit der Kunden-Id nach dem Anonymisieren entfernen, auf anderen Geräten beim nächsten Abgleich.

## Quellen

- Recherche Normen und Fristen vom 10.11.2026 (Notizen der Sitzung)
- [ADR 0082](0082-aenderungsverlauf-ueber-den-aenderungs-trigger.md), [ADR 0061](0061-kundenzuordnung-eingehender-nachrichten.md)
- Hilfe `kunden-anonymisieren`
