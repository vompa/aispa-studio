# 0096. Kassieren ohne Verbindung: Ersatzbeleg und Nacherfassung

- **Status:** angenommen, Steuerberatung offen
- **Datum:** 16.11.2026
- **Entschieden von:** Claude, autonom nach ADR 0019 (Auftrag des Auftraggebers vom 16.11.2026: „Mach weiter mit Netzausfall und Ersatzbelegen“; Reihenfolge vom 20.10.2026: „Startfähigkeit der Kassa“)

## Kontext

Die Windows-App arbeitet ohne Verbindung weiter ([ADR 0005](0005-offline-windows-app-am-empfang.md)), nur Kassieren ging nicht: Belegnummer, Signatur und Kassenjournal entstehen am Server. Fällt das Netz aus, steht der Gast an der Rezeption und kann nicht zahlen. ADR 0005 nennt das als offenen Punkt („lokale Belegnummern mit Nachsignatur bzw. Ersatzbelege nach RKSV“).

Rechtlicher Rahmen, Stand der Recherche vom 16.11.2026:

- Österreich, § 17 RKSV: Fällt die Registrierkasse aus, sind die Barumsätze auf einer anderen Registrierkasse zu erfassen. Geht das nicht, werden sie händisch erfasst und Zweitschriften der Belege aufbewahrt; nach dem Ausfall werden sie nacherfasst. Dauert der Ausfall länger als 48 Stunden, ist er über FinanzOnline zu melden.
- Deutschland, AEAO zu § 146a: Die Belegausgabepflicht entfällt nur bei vollständigem Ausfall des Aufzeichnungssystems; der Ausfall ist zu dokumentieren.
- [ADR 0010](0010-kassa-compliance-by-design.md) und [ADR 0041](0041-einzelplatz-standalone-und-zuschaltbare-webmodule.md): Es darf keinen Weg geben, Belege an Signatur oder Abgleich vorbeizuführen. Eine Kasse signiert nur an ihrer Stelle.

## Optionen

| Option | Bewertung |
| --- | --- |
| Lokal signieren (zweite Signaturkette in der App) | zwei Ketten je Kasse, Schlüssel auf jedem Empfangs-PC; widerspricht ADR 0041 |
| Lokale Belegnummern, später am Server nachsignieren | der Gast bekäme einen Beleg mit einer Nummer der Kasse, den es am Server noch nicht gibt; Lücken und Doppel bei zwei Geräten |
| Ersatzbeleg mit eigener Nummer, danach Nacherfassung als gewöhnlicher Beleg | gewählt: entspricht dem händischen Beleg der RKSV, die Kette der Kasse bleibt unberührt |
| Nichts bauen, Zettel und Stift | der Bezug zwischen Zettel und Beleg hinge an der Sorgfalt der Rezeption |

## Entscheidung

1. **Ersatzbeleg:** Ohne Verbindung gibt die App einen Ersatzbeleg aus. Er trägt eine eigene Nummer je Gerät („E-…“), Datum, Positionen, Summe, Zahlung und den Satz, dass der signierte Beleg nachgereicht wird. Er ist kein Beleg der Kasse: keine Belegnummer der Kasse, keine Signatur.
2. **Zweitschrift:** Der Ersatzbeleg bleibt verschlüsselt auf dem Gerät, bis er nacherfasst ist. Er lässt sich nicht verwerfen.
3. **Nacherfassung:** Sobald die Kasse erreichbar ist, sendet die App jeden Ersatzbeleg einzeln an `POST /api/pos/registers/{id}/substitute-receipts`. Daraus entsteht in einer Transaktion ein gewöhnlicher Beleg: Rechnung, Positionen, Zahlung, Nummer, Signatur. Kein Sammelbeleg.
4. **Bezug:** Der Beleg speichert Id, Nummer und Zeitpunkt des Ersatzbelegs (`Receipt.SubstituteId`, `SubstituteNumber`, `SubstituteIssuedAt`) und druckt „Nacherfassung zu Ersatzbeleg … vom …“. Belegdatum und Signatur sind die der Nacherfassung.
5. **Nur einmal:** Dieselbe Id liefert denselben Beleg (eindeutiger Index). Erneutes Senden nach einem Abbruch bucht nichts doppelt.
6. **Der Server entscheidet:** Preise, Steuer und Erlöskonten kommen vom Server. Stimmt die Summe nicht mit dem Ersatzbeleg überein, ist ein Termin schon abgerechnet oder bliebe Rückgeld, wird nichts gebucht (409). Den Fall klärt ein Mensch an der Kassa; die App zeigt ihn mit Grund, bis er erledigt ist.
7. **Umfang ohne Verbindung:** Termine und Artikel, bar und Karte am eigenständigen Gerät, Trinkgeld. Nicht ohne Verbindung: Gutscheine, Pakete, Rabatte, Barzahlungsrabatt, Anzahlungen, das angebundene Kartenterminal, Sofortüberweisung, Parken, Storno, Tagesabschluss.
8. **Einzelplatz:** Dort liegt die Kasse auf demselben PC; es gibt keinen Ersatzbeleg. Fällt nur die Signatureinrichtung aus, gilt weiter der Ablauf „Sicherheitseinrichtung ausgefallen“.

## Begründung

- Der Ersatzbeleg ist der händische Beleg der RKSV in lesbarer Form; die Nacherfassung stellt den Bezug her, den sonst die Rezeption von Hand eintragen müsste.
- Die Signaturkette der Kasse entsteht weiter nur am Server. Ein Empfangs-PC hält keinen Schlüssel.
- Geld, das der Gast schon bezahlt hat, wird nie stillschweigend anders gebucht als auf dem Ersatzbeleg steht.

## Folgen und Stand

- Gebaut (Server, Schritt 1): `PosService.ReenterSubstituteAsync`, Migration `ReceiptSubstitute`, `ReceiptDto.SubstituteNumber` und `SubstituteIssuedAt`, Hinweis auf PDF und Bon; Tests in `SubstituteReceiptTests`.
- Gebaut (Windows-App, Schritt 2): `PosViewModel.Substitute.cs`. Ohne Verbindung entsteht die Rechnung am Gerät (Termine und Artikel aus dem lokalen Stand), bezahlt wird bar oder mit Karte am eigenständigen Gerät, die Kassa zeigt und druckt den Ersatzbeleg. Nummer: `E-<Kasse>-<JJJJMMTT>-<HHMMSS>-<zwei Zeichen der Id>`. Die Zweitschrift liegt in der Warteschlange des lokalen Stands (`PendingChange.Substitute`, DPAPI) und geht beim Abgleich nach dem vorgemerkten Einchecken an die Kasse. Ein abgelehnter Ersatzbeleg lässt sich nicht abhaken, nur erneut senden. Hilfe `kassa-ersatzbeleg`.
- Der Barzahlungsrabatt gilt ohne Verbindung nicht; die Nacherfassung rechnet ihn nicht (`SubstituteLookups`).
- Offen, später: Hinweis in der App auf die Meldung über FinanzOnline nach 48 Stunden ohne Verbindung (bisher nur in der Hilfe); Liste der offenen Ersatzbelege in der Kassa mit Nachdruck; Eintritte ohne Verbindung; Auswertung der Ausfälle; Personenwechsel ohne Server (der Ersatzbeleg trägt bei der Nacherfassung die Person, die dann angemeldet ist).
- **Steuerberatung:** Genügt der Ersatzbeleg aus der App als händischer Beleg mit Zweitschrift nach § 17 RKSV? Pflichtangaben des Ersatzbelegs in Deutschland; Umgang mit der Schweiz.

## Quellen

- [§ 17 RKSV](https://gesetzefinden.at/bundesrecht/verordnungen/rksv/para-17)
- [BMF-Handbuch Registrierkassen in FinanzOnline](https://finanzonline.bmf.gv.at/eLearning/BMF_Handbuch_Registrierkassen.pdf)
- [ZDH-Praxishilfe Kassenführung 2026](https://www.hwk-reutlingen.de/wp-content/uploads/zdh_praxishilfe_kassenfuehrung_2026.pdf)
- [ADR 0005](0005-offline-windows-app-am-empfang.md), [ADR 0010](0010-kassa-compliance-by-design.md), [ADR 0041](0041-einzelplatz-standalone-und-zuschaltbare-webmodule.md)
