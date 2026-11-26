---
art: ablauf
bereich: Berichte
oberflaeche: verwaltung
rollen: leitung, inhaber, manager
seite: verwaltung
stichwoerter: Management, Cockpit, Kennzahlen, KPI, Umsatz, Auslastung, No-Show, Storno-Quote, Durchschnittsbon, Stammgäste, neue Gäste, Folgetermin, Marge, Vollkosten, Erlöskonto, Kostenstelle, Personal, Alarme, Vorperiode, Export, Excel, CSV, Kennzahlen exportieren, wie läuft das Geschäft, revenue, utilization, dashboard, figures
---
# Kennzahlen im Management ansehen

Der Abschnitt „Management“ im Reiter „Berichte“ zeigt die Kennzahlen eines Standorts für einen Zeitraum, jeweils neben dem gleich langen Zeitraum davor.

## Zeitraum wählen {#zeitraum}

1. In der Windows-App unter „Verwaltung“ **Zahlen** öffnen und den Reiter **Berichte** wählen. Der Abschnitt **Management** steht oben.
2. **Standort** wählen.
3. **Von** und **Bis** setzen. Vorbelegt sind der Monatserste und heute. Der Zeitraum darf höchstens ein Jahr lang sein.
4. Wer die Zahlen weitergeben will: **Exportieren** daneben speichert die Kennzahlen des Zeitraums mit Wert, Vorperiode und Einheit als Excel oder CSV, z. B. als `Kennzahlen_2026-11-01_bis_2026-11-26.xlsx`. Beträge in Euro haben in Excel zwei Nachkommastellen. Beträge stehen nur darin, wenn man sie auf der Seite auch sieht.

![Management mit Kennzahlen](images/app/verwaltung-berichte-management.png)

## Was angezeigt wird {#inhalt}

- **Alarme** oben, wenn etwas auffällt: hohe No-Show-Quote, Personal wenig ausgelastet, eine Behandlung mit Verlust je Termin, Kassendifferenzen von zusammen mindestens 5,00 € im Zeitraum (Überschuss und Fehlbetrag zählen beide), bereits vergangene Termine im Zeitraum, die noch nicht kassiert sind („3 Termine noch nicht kassiert“). Beginnt der Zeitraum davor vor dem ersten Termin des Standorts, steht „Vergleich reicht vor den Beginn der Aufzeichnung“: Die Vorperiode ist dann unvollständig, vor allem Neue Gäste und Stammgäste-Anteil sind nicht vergleichbar.
- **Kennzahlen**, jeweils mit „Vorperiode“: Umsatz brutto, Umsatz netto, Durchschnittsbon netto, Umsatz ohne Gutscheinverkauf (netto), Durchschnittsbon ohne Gutscheinverkauf (netto), Gutscheine verkauft, Gutscheine eingelöst, Behandlungen, Auslastung Personal, Auslastung Räume, No-Show-Quote, Storno-Quote, Gäste, Neue Gäste, Stammgäste-Anteil, Folgetermin gebucht und Offene Gutscheine (heute, ohne Vorperiode).
- **Folgetermin gebucht**: Anteil der Gäste mit abgeschlossener Behandlung im Zeitraum, die einen späteren Termin haben. Es zählt jeder spätere Termin, der nicht storniert und kein No-Show ist, auch wenn er inzwischen stattgefunden hat.

## Gutscheine in den Kennzahlen {#gutscheine}

- **Umsatz brutto**, **Umsatz netto** und **Durchschnittsbon netto** enthalten den Verkauf von Wertgutscheinen, so wie er in der Kassa steht.
- **Umsatz ohne Gutscheinverkauf (netto)** und **Durchschnittsbon ohne Gutscheinverkauf (netto)** lassen verkaufte Wertgutscheine weg. Ein Wertgutschein ist beim Verkauf noch kein Umsatz; er wird es erst, wenn der Gast damit bezahlt. Ein Beleg, auf dem nur Wertgutscheine stehen, zählt nicht als Bon.
- Gutscheine für eine bestimmte Behandlung und Pakete sind schon beim Verkauf versteuerter Umsatz. Sie zählen deshalb auch im Umsatz ohne Gutscheinverkauf.
- **Gutscheine verkauft**: Wert aller Gutscheine und Pakete, die im Zeitraum verkauft wurden, brutto.
- **Gutscheine eingelöst**: Wert, der im Zeitraum mit Gutscheinen und Paketen bezahlt wurde, brutto. Ein Storno zieht den Betrag wieder ab.
- Der [Vergleich mit anderen Spas](hilfe:berichte-mit-anderen-spas-vergleichen) nimmt den Durchschnittsbon ohne Gutscheinverkauf.
- **Umsatz nach Erlöskonto**: je Erlöskonto mit Kostenstelle brutto und netto, darunter der Umsatz je Zahlungsart.
- **Personal**: je Person Dienststunden, Behandlungsstunden, Auslastung und Umsatz je Behandlungsstunde (netto).
- **Behandlungen**: je Behandlung Termine, Umsatz netto und Marge je Termin aus der Vollkostenrechnung. Eine negative Marge ist hervorgehoben; „–“ heißt, für die Behandlung sind keine Vollkosten gerechnet.
- Umsatz je Behandlung und je Person ist der Betrag, der für den Termin im Zeitraum kassiert wurde, netto und nach Rabatt. Ein Barzahlungsrabatt verteilt sich anteilig auf die Positionen des Belegs. Ein Storno zieht den Betrag wieder ab.
- Ein Termin, der noch nicht kassiert ist, zählt mit 0 €. Ein Termin, den der Gast mit einem Gutschein für die Behandlung oder einem Paket bezahlt hat, zählt ebenfalls mit 0 €: Der Umsatz entstand schon beim Verkauf des Gutscheins. Eine Anzahlung zählt beim Termin, wenn sie im selben Zeitraum kassiert wurde.
- Die Marge je Termin rechnet jeden Termin mit den Vollkosten seines Monats.

## Kommentar der KI {#ki-kommentar}

Bei einem Zeitraum ab sieben Tagen schreibt die KI über den Alarmen drei bis fünf Sätze: was gut lief, was schlechter als im Zeitraum davor, und höchstens drei Empfehlungen. Erst steht „Die KI kommentiert den Zeitraum …“, nach ein paar Sekunden der Text mit der Pille „KI“.

- Die KI rechnet nichts: Jede Zahl im Kommentar muss so in den Kennzahlen des Berichts stehen, sonst wird der Kommentar verworfen und es gibt keinen.
- Ohne das Recht „Berichte mit Beträgen“ bekommt auch die KI keine Beträge; der Kommentar nennt dann keine.
- Gästenamen und Gesundheitsdaten bekommt die KI nicht.
- Ändern sich die Zahlen des Zeitraums, schreibt die KI beim nächsten Öffnen neu. Ohne KI-Schlüssel gibt es keinen Kommentar.

![Management mit dem Kommentar der KI](images/app/verwaltung-berichte-management-ki.png)

## Aktionen {#aktionen}

Wurden im Zeitraum Positionen mit einer [Aktion](hilfe:kassa-aktion-anwenden) kassiert, steht unten die Tabelle **Aktionen**, je Aktion:

- **Positionen:** wie oft die Aktion kassiert wurde; ein Storno hebt eine Position wieder auf.
- **Gäste** und **davon neu:** wie viele Gäste die Aktion bekommen haben, und wie viele davon zum ersten Mal im Spa waren.
- **Rabatt** und **Umsatz brutto** (nach Rabatt): nur mit dem Recht „Berichte mit Beträgen“.

Hat jemand den Rabatt der Position von Hand geändert, zählt sie nicht mehr zur Aktion. Was vor dem 19.11.2026 mit Aktion kassiert wurde, fehlt in der Tabelle.

![Management mit der Tabelle Aktionen](images/app/verwaltung-berichte-management-aktionen.png)

## Bericht per Mail {#bericht-per-mail}

Inhaber, Manager und Standortleitung bekommen den Bericht auch per Mail, je Standort, den sie sehen:

- **Wochenbericht** montags ab 7 Uhr, für Montag bis Sonntag der Vorwoche.
- **Monatsbericht** am Monatsersten ab 7 Uhr, für den Vormonat.

In der Mail stehen der Kommentar der KI, was zu beachten ist, die Kennzahlen mit dem Zeitraum davor und die Aktionen; Beträge nur mit dem Recht „Berichte mit Beträgen“. Abbestellen: beim Management den Haken **Wochen- und Monatsbericht per Mail** entfernen; wieder setzen bestellt ihn wieder. Der Haken gilt nur für Sie selbst. Ohne Mailadresse im Konto kommt keine Mail.

## Hinweise {#hinweise}

- Inhaber, Manager, Standortleitung und Backoffice; die Standortleitung sieht nur ihre Standorte.
- Beträge (Umsatz, Durchschnittsbon, Umsatz je Behandlung und Person, Marge) sieht nur, wer das Recht „Berichte mit Beträgen“ hat: Inhaber, Manager und Backoffice immer, Standortleitung und Rezeption nur, wenn es ihnen vergeben ist ([Berichte mit Beträgen](hilfe:zugaenge#berichte-mit-betraegen)). Ohne das Recht fehlen sie, und es steht „Beträge nur mit Berechtigung“; Auslastung, Anzahlen und Quoten bleiben.
- Die Umsätze kommen aus den Kassenbelegen, Auslastung und Stunden aus Kalender und Dienstplan. Die KI rechnet hier nichts; sie kommentiert nur (siehe oben).
- Die Marge braucht die Vollkosten unter „Kosten“, siehe [Kosten](hilfe:kosten).
- Eigene Erlöskonten (z. B. Shop) legt man unter „Buchhaltung“ an: [Erlöskonto anlegen](hilfe:buchhaltung-erloeskonto-anlegen).
- Vergleich mit anderen Day-Spas: [Mit anderen Spas vergleichen](hilfe:berichte-mit-anderen-spas-vergleichen).
- Kurze Zahlen auf Zuruf liefert auch der [KI-Assistent](hilfe:ki-assistent-fragen), z. B. „Wie war der Umsatz letzte Woche?“.
