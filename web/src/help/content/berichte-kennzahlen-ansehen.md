---
art: ablauf
bereich: Berichte
oberflaeche: verwaltung
rollen: leitung, inhaber, manager
seite: verwaltung
stichwoerter: Management, Cockpit, Kennzahlen, KPI, Umsatz, Auslastung, No-Show, Storno-Quote, Durchschnittsbon, Stammgäste, neue Gäste, Folgetermin, Marge, Vollkosten, Deckungsbeitrag, Umsatz je Dienststunde, Produktanteil, Umsatz je Gast, entgangener Umsatz, Verlauf, Diagramm, Wochentag, Tageszeit, schwache Zeiten, Räume, Kanal, Online-Buchung, beste Gäste, abwandernde Stammgäste, Vorschau, gebuchter Umsatz, Erlöskonto, Kostenstelle, Personal, Alarme, Vorperiode, Vormonat, Vorjahr, Vergleich, Schnellwahl, Eigener Zeitraum, Veränderung, Pfeil, Export, Ausgabe, Excel, Arbeitsmappe, Word, Bericht exportieren, Anmerkungen, Datenbalken, Pivot, CSV, PDF, Drucken, Bericht drucken, per Mail senden, Bericht senden, Kennzahlen exportieren, wie läuft das Geschäft, revenue, utilization, dashboard, figures
---
# Kennzahlen im Management ansehen

Der Abschnitt „Management“ im Reiter „Berichte“ zeigt die Kennzahlen eines Standorts für einen Zeitraum, jeweils neben einem Vergleichszeitraum.

## Zeitraum wählen {#zeitraum}

1. In der Windows-App unter „Verwaltung“ **Zahlen** öffnen und den Reiter **Berichte** wählen. Der Abschnitt **Management** steht oben.
2. **Standort** wählen.
3. **Von** und **Bis** setzen. Vorbelegt sind der Monatserste und heute. Der Zeitraum darf höchstens ein Jahr lang sein.
4. Schneller geht es mit **Zeitraum** links neben Von und Bis: **Dieser Monat**, **Letzter Monat**, **Dieses Quartal**, **Dieses Jahr** oder **Letzte 12 Monate**. Die Auswahl setzt Von und Bis. Dieser Monat, Dieses Quartal und Dieses Jahr reichen bis heute, Letzter Monat ist der ganze Vormonat. Passen Von und Bis zu keiner Auswahl, etwa nach einer Änderung von Hand, steht dort **Eigener Zeitraum**.
5. Reicht **Bis** bis heute oder darüber, steht „Heute läuft noch, die Zahlen können sich ändern.“: Umsatz und Termine von heute können noch wachsen.
6. **Vergleichen mit** wählt, wogegen die Kacheln rechnen, siehe [Vergleichen mit](#vergleich).
7. Wer die Zahlen weitergeben will, öffnet in der Kopfzeile das Menü **Ausgabe**: Drucken, PDF, Mail, Excel, Word und CSV. Mehr dazu unter [Bericht als Dokument ausgeben](hilfe:berichte-als-dokument-ausgeben).

![Management mit Kennzahlen](images/app/verwaltung-berichte-management.png)

## Vergleichen mit {#vergleich}

**Vergleichen mit** steht neben dem Zeitraum und hat ein Fragezeichen. Das Feld wählt, wogegen die Kacheln rechnen. Der kleine Text „Vergleich: …“ in derselben Zeile nennt den Zeitraum.

- **Zeitraum davor** (Standard): der gleich lange Zeitraum direkt davor.
- **Vormonat**: derselbe Abschnitt im Vormonat, also 1. bis 26. gegen 1. bis 26. des Vormonats. Endet Ihr Zeitraum am Monatsletzten, zählt der ganze Vormonat.
- **Vorjahr**: derselbe Zeitraum im Vorjahr.

Unter jedem Wert zeigt die Kachel, wie er sich gegenüber dem Vergleichszeitraum verändert hat: in Prozent, bei Quoten in Prozentpunkten. Prozentpunkte sind der Unterschied der beiden Prozentwerte: Von 40 % auf 45 % sind 5 Prozentpunkte, nicht 5 Prozent. Mehr dazu unter [Veränderung auf den Kacheln lesen](#veraenderung). Der Kommentar der KI und der Bericht per Mail vergleichen immer mit dem Zeitraum davor. Die Auswertung der KI per Knopf nimmt den gewählten Vergleich.

## Was angezeigt wird {#inhalt}

- **Alarme** oben, wenn etwas auffällt: hohe No-Show-Quote, Personal wenig ausgelastet, eine Behandlung mit Verlust je Termin, Kassendifferenzen von zusammen mindestens 5,00 € im Zeitraum (Überschuss und Fehlbetrag zählen beide), bereits vergangene Termine im Zeitraum, die noch nicht kassiert sind („3 Termine noch nicht kassiert“). Beginnt der Zeitraum davor vor dem ersten Termin des Standorts, steht „Vergleich reicht vor den Beginn der Aufzeichnung“: Die Vorperiode ist dann unvollständig, vor allem Neue Gäste und Stammgäste-Anteil sind nicht vergleichbar.
- **Kennzahlen** in vier Gruppen, jeweils mit dem Vergleichswert („Vorperiode“, „Vormonat“ oder „Vorjahr“):
  - **Umsatz und Ertrag:** Umsatz brutto, Umsatz netto, Durchschnittsbon netto, Umsatz ohne Gutscheinverkauf (netto), Durchschnittsbon ohne Gutscheinverkauf (netto), Deckungsbeitrag, Deckungsbeitrag in Prozent, Umsatz je Dienststunde, Umsatz je Behandlungsstunde, Produktanteil am Umsatz, Produktumsatz je Behandlung, Umsatz je Gast, Gutscheine verkauft, Gutscheine eingelöst und Offene Gutscheine (heute, ohne Vorperiode).
  - **Auslastung:** Behandlungen, Auslastung Personal, Auslastung Räume und Offene Anfragen (heute).
  - **Gäste:** Gäste, Neue Gäste, Stammgäste-Anteil, Folgetermin gebucht und Abwandernde Stammgäste (heute, ohne Vergleich).
  - **Ausfälle:** No-Show-Quote, Storno-Quote, Entgangen durch No-Show und Entgangen durch Storno.
- **Verlauf:** drei kleine Säulendiagramme für Umsatz ohne Gutscheinverkauf (netto), Behandlungen und Auslastung Personal. Bis 62 Tage steht jede Säule für einen Tag, bei längeren Zeiträumen für eine Woche von Montag bis Sonntag. Zeigen Sie mit der Maus auf eine Säule, um ihren Wert zu sehen. **Werte als Tabelle** unter jedem Diagramm klappt dieselben Werte als Liste auf.
- **Tabellen** nach dem Verlauf: Auslastung nach Wochentag und Tageszeit, Vorschau der nächsten vier Wochen, Räume, Umsatz nach Erlöskonto und Zahlungsart, Personal, Behandlungen, Aktionen, Buchungen nach Kanal und die Gäste. Mehr dazu unter [Auslastung, Räume, Kanäle und Vorschau](#muster) und [Gäste](#gaeste).

## Auslastung, Räume, Kanäle und Vorschau {#muster}

- **Auslastung nach Wochentag und Tageszeit:** je Wochentag mit Diensten die Auslastung Personal im Vormittag (bis 11 Uhr), Mittag (11 bis 14 Uhr), Nachmittag (14 bis 17 Uhr) und Abend (ab 17 Uhr). Gerechnet wird wie bei der Kachel: Behandlungsminuten geteilt durch Dienstminuten in diesem Abschnitt. Eine Behandlung über eine Grenze zählt in beiden Abschnitten anteilig. Ein Feld unter 50 % steht mit dem Wort „schwach“ und ist hervorgehoben: Das sind die Lücken, die sich für Aktionen eignen. Ein Strich heißt, in dieser Tageszeit hatte niemand Dienst.
- **Vorschau nächste vier Wochen:** je sieben Tage ab heute die gebuchten Termine, ihr Terminpreis brutto (**Gebuchter Umsatz**) und die **Gebuchte Auslastung** (Behandlungsminuten geteilt durch die geplanten Dienstminuten). Es zählt nur, was heute gebucht ist; stornierte Termine fehlen. Die Vorschau hängt nicht vom gewählten Zeitraum ab und kommt nicht von der KI.
- **Räume:** je Raum die Auslastung im Zeitraum (belegte Minuten mit Vor- und Nachbereitung geteilt durch die Öffnungsminuten) und die Termine. Bereiche wie Sauna oder Ruheraum fehlen.
- **Buchungen nach Kanal:** die stattfindenden Termine des Zeitraums nach ihrer Herkunft. **Rezeption** heißt, das Team hat den Termin eingetragen, auch nach einem Anruf. **Online-Buchung**, **Mail**, **SMS**, **WhatsApp**, **Telefon (Terminannahme)** und **Treatwell** kommen über die jeweilige Terminannahme.

## Gäste {#gaeste}

Die Gästelisten sieht nur, wer auch die Kundenliste sehen darf. Die Namen gehen nie an die KI und nicht in den Wochen- und Monatsbericht per Mail; im PDF und in der Arbeitsmappe stehen sie nur mit diesem Recht.

- **Beste Gäste:** die zehn Gäste mit den höchsten Ausgaben im Zeitraum, mit Besuchen (abgeschlossene Termine) und Ausgaben. Ohne das Recht „Berichte mit Beträgen“ sind es die zehn mit den meisten Besuchen, ohne Ausgaben.
- **Abwandernde Stammgäste:** Gäste mit mindestens drei Besuchen an diesem Standort, deren letzter Besuch über 90 Tage zurückliegt und die keinen Termin geplant haben. Die Liste zeigt Name, letzten Besuch und Besuche, die 50 mit den meisten Besuchen zuerst. Die Kachel **Abwandernde Stammgäste (heute)** zählt alle. Beide zeigen den Stand von heute, unabhängig vom gewählten Zeitraum.

## Veränderung auf den Kacheln lesen {#veraenderung}

Jede Kachel mit Vergleichswert zeigt unter dem Wert in einer Pille, wie sich die Zahl verändert hat:

- **Pfeil und Zahl:** „↓ 24 %“ heißt 24 Prozent weniger als im Vergleichszeitraum. Bei Quoten wie Auslastung oder No-Show-Quote steht die Veränderung in Prozentpunkten („↓ 3,0 Prozentpunkte“). War der Vergleichswert 0, steht „↑ von 0“. Bleibt der Wert gleich, steht „→ unverändert“.
- **Farbe der Pille:** Grün heißt, die Veränderung ist gut (mehr Umsatz, weniger No-Shows). Orange heißt, sie ist schlecht (weniger Umsatz, mehr No-Shows). Kennzahlen ohne klare Richtung, etwa „Offene Gutscheine“, zeigen eine graue Pille. Darunter steht klein der Wert des Vergleichszeitraums, etwa „Vorperiode 25.330,22 €“. Ein Screenreader liest den ganzen Satz, zum Beispiel „Umsatz brutto 19.250,97 €, 24 % unter der Vorperiode“.
- Kennzahlen, die nur den Stand von heute zeigen („Offene Gutscheine“, „Offene Anfragen“), haben keinen Vergleich.

## Gutscheine in den Kennzahlen {#gutscheine}

- **Umsatz brutto**, **Umsatz netto** und **Durchschnittsbon netto** enthalten den Verkauf von Wertgutscheinen, so wie er in der Kassa steht.
- **Umsatz ohne Gutscheinverkauf (netto)** und **Durchschnittsbon ohne Gutscheinverkauf (netto)** lassen verkaufte Wertgutscheine weg. Ein Wertgutschein ist beim Verkauf noch kein Umsatz; er wird es erst, wenn der Gast damit bezahlt. Ein Beleg, auf dem nur Wertgutscheine stehen, zählt nicht als Bon.
- Gutscheine für eine bestimmte Behandlung und Pakete sind schon beim Verkauf versteuerter Umsatz. Sie zählen deshalb auch im Umsatz ohne Gutscheinverkauf.
- **Gutscheine verkauft**: Wert aller Gutscheine und Pakete, die im Zeitraum verkauft wurden, brutto.
- **Gutscheine eingelöst**: Wert, der im Zeitraum mit Gutscheinen und Paketen bezahlt wurde, brutto. Ein Storno zieht den Betrag wieder ab.
- Der [Vergleich mit anderen Spas](hilfe:berichte-mit-anderen-spas-vergleichen) nimmt den Durchschnittsbon ohne Gutscheinverkauf.
- **Umsatz nach Erlöskonto**: je Erlöskonto mit Kostenstelle brutto und netto, daneben der Umsatz je Zahlungsart. In einem schmalen Fenster stehen beide Listen untereinander.
- **Personal**: je Person Dienststunden, Behandlungsstunden, Auslastung und Umsatz je Behandlungsstunde (netto).
- **Behandlungen**: je Behandlung Termine, Umsatz netto und Marge je Termin aus der Vollkostenrechnung. Eine negative Marge ist hervorgehoben; „–“ heißt, für die Behandlung sind keine Vollkosten gerechnet.
- Umsatz je Behandlung und je Person ist der Betrag, der für den Termin im Zeitraum kassiert wurde, netto und nach Rabatt. Ein Barzahlungsrabatt verteilt sich anteilig auf die Positionen des Belegs. Ein Storno zieht den Betrag wieder ab.
- Ein Termin, der noch nicht kassiert ist, zählt mit 0 €. Ein Termin, den der Gast mit einem Gutschein für die Behandlung oder einem Paket bezahlt hat, zählt ebenfalls mit 0 €: Der Umsatz entstand schon beim Verkauf des Gutscheins. Eine Anzahlung zählt beim Termin, wenn sie im selben Zeitraum kassiert wurde.
- Die Marge je Termin rechnet jeden Termin mit den Vollkosten seines Monats.

## Kommentar der KI {#ki-kommentar}

Bei einem Zeitraum ab sieben Tagen schreibt die KI über den Alarmen drei bis fünf Sätze: was gut lief, was schlechter als im Zeitraum davor, und höchstens drei Empfehlungen. Erst steht „Die KI kommentiert den Zeitraum …“, nach ein paar Sekunden der Text mit der Pille „KI“.

- Die KI rechnet nichts: Jede Zahl im Kommentar muss so in den Kennzahlen des Berichts stehen, sonst wird der Kommentar verworfen und es gibt keinen.
- Ohne das Recht „Berichte mit Beträgen“ bekommt auch die KI keine Beträge; der Kommentar nennt dann keine.
- Beim Personal bekommt die KI den Namen im Haus, ersatzweise den Namen der Person, mit den Zahlen je Person. Gästenamen und Gesundheitsdaten bekommt sie nie.
- Ändern sich die Zahlen des Zeitraums, schreibt die KI beim nächsten Öffnen neu. Ohne KI-Schlüssel gibt es keinen Kommentar.
- Eine ausführlichere Auswertung mit Auffälligkeiten und Maßnahmen gibt es auf Knopfdruck: [Bericht mit KI auswerten](hilfe:berichte-mit-ki-auswerten).

![Management mit dem Kommentar der KI](images/app/verwaltung-berichte-management-ki.png)

## Aktionen {#aktionen}

Wurden im Zeitraum Positionen mit einer [Aktion](hilfe:kassa-aktion-anwenden) kassiert, steht unten die Tabelle **Aktionen**, je Aktion:

- **Positionen:** wie oft die Aktion kassiert wurde; ein Storno hebt eine Position wieder auf.
- **Gäste** und **davon neu:** wie viele Gäste die Aktion bekommen haben, und wie viele davon zum ersten Mal im Spa waren.
- **Rabatt** und **Umsatz brutto** (nach Rabatt): nur mit dem Recht „Berichte mit Beträgen“.

Hat jemand den Rabatt der Position von Hand geändert, zählt sie nicht mehr zur Aktion. Was vor dem 19.11.2026 mit Aktion kassiert wurde, fehlt in der Tabelle.

![Management mit der Tabelle Aktionen](images/app/verwaltung-berichte-management-aktionen.png)

## Bericht per Mail {#bericht-per-mail}

Inhaber, Manager und Standortleitung bekommen den Bericht auch per Mail, je Standort, den sie sehen. Im Einzelplatz geht das nur mit eingerichtetem Mailversand (`Mail:Smtp:Host`); ohne ihn fehlt das Kästchen im Menü **Ausgabe**.

- **Wochenbericht** montags ab 7 Uhr, für Montag bis Sonntag der Vorwoche.
- **Monatsbericht** am Monatsersten ab 7 Uhr, für den Vormonat.

In der Mail stehen der Kommentar der KI, was zu beachten ist, die Kennzahlen mit dem Zeitraum davor und die Aktionen; Beträge nur mit dem Recht „Berichte mit Beträgen“. Dazu hängt der Bericht als PDF an, passend zu dem, was die Person sehen darf, mit Vergleich zum Zeitraum davor und ohne Gästelisten. Wäre das PDF größer als 5 MB, geht die Mail ohne Anhang hinaus. Abbestellen: beim Management im Menü **Ausgabe** das Kästchen **Wochen- und Monatsbericht per Mail** abwählen; wieder wählen bestellt ihn wieder. Das Kästchen gilt nur für Sie selbst. Ohne Mailadresse im Konto kommt keine Mail.

## Gut zu wissen {#hinweise}

- Inhaber, Manager, Standortleitung und Backoffice; die Standortleitung sieht nur ihre Standorte.
- Beträge (Umsatz, Durchschnittsbon, Umsatz je Behandlung und Person, Marge, Deckungsbeitrag, entgangener Umsatz, Umsatz im Verlauf, gebuchter Umsatz der Vorschau, Ausgaben der besten Gäste) und die Anteile daraus (Deckungsbeitrag in Prozent, Produktanteil) sieht nur, wer das Recht „Berichte mit Beträgen“ hat: Inhaber, Manager und Backoffice immer, Standortleitung und Rezeption nur, wenn es ihnen vergeben ist ([Berichte mit Beträgen](hilfe:zugaenge#berichte-mit-betraegen)). Ohne das Recht fehlen sie, und es steht „Beträge nur mit Berechtigung“; Auslastung, Anzahlen und Quoten bleiben.
- Die Umsätze kommen aus den Kassenbelegen, Auslastung und Stunden aus Kalender und Dienstplan. Die KI rechnet hier nichts; sie kommentiert nur (siehe oben).
- Die Marge braucht die Vollkosten unter „Kosten“, siehe [Kosten](hilfe:kosten).
- Eigene Erlöskonten (z. B. Shop) legt man unter „Buchhaltung“ an: [Erlöskonto anlegen](hilfe:buchhaltung-erloeskonto-anlegen).
- Vergleich mit anderen Day-Spas: [Mit anderen Spas vergleichen](hilfe:berichte-mit-anderen-spas-vergleichen).
- Kurze Zahlen auf Zuruf liefert auch der [KI-Assistent](hilfe:ki-assistent-fragen), z. B. „Wie war der Umsatz letzte Woche?“.
- Wie die Kennzahlen gerechnet werden: [Wie die Kennzahlen gerechnet werden](hilfe:berichte-kennzahlen-gerechnet).
