---
art: ablauf
bereich: Berichte
oberflaeche: verwaltung
rollen: leitung, inhaber, manager
seite: verwaltung
stichwoerter: Ausgabe, Menü Ausgabe, Bericht drucken, Bericht als PDF, PDF speichern, per Mail senden, Bericht senden, Excel, Arbeitsmappe, Word, Word-Datei, Anmerkungen, CSV, Kennzahlen exportieren, Bericht für die Bank, Bericht für die Mappe, Bericht weitergeben, Datenbalken, Pivot, Export-Protokoll, Wochenbericht, Monatsbericht, report, export, print, pdf, docx, xlsx
---
# Bericht als Dokument ausgeben

Den Management-Bericht gibt es als PDF, als Word-Datei und als Excel-Arbeitsmappe. Alle drei entstehen aus denselben Zahlen wie die Seite: Zeitraum, Vergleich und Standort sind die, die gerade gewählt sind.

## Das Menü Ausgabe {#menue}

1. In der Windows-App unter „Verwaltung“ **Zahlen** öffnen, den Reiter **Berichte** wählen und im Abschnitt **Management** Standort, Zeitraum und **Vergleichen mit** einstellen ([Kennzahlen im Management ansehen](hilfe:berichte-kennzahlen-ansehen)).
2. In der Kopfzeile auf **Ausgabe** klicken. Das Menü hat drei Gruppen:
   - **Drucken …**, **Als PDF speichern** und **Per Mail senden …**: der Bericht als PDF. Die Einträge stehen erst, wenn der Bericht geladen ist.
   - **Bericht (Excel)**, **Bericht (Word)** und **Kennzahlen (CSV)**.
   - Das Kästchen **Wochen- und Monatsbericht per Mail**: bestellt den Bericht ab oder wieder an, nur für die eigene Person. Es steht nur für Inhaber, Manager und Standortleitung, im Einzelplatz nur mit eingerichtetem Mailversand (`Mail:Smtp:Host`). Mehr dazu unter [Bericht per Mail](hilfe:berichte-kennzahlen-ansehen#bericht-per-mail).
3. Den Eintrag wählen. Nach dem Speichern einer Datei steht „Gespeichert: …“, und **Ordner öffnen** daneben öffnet den Ordner. Drucken, PDF und Mail melden sich mit einer kurzen Meldung.

Das Menü speichert, druckt oder sendet immer den Bericht, den die Seite gerade zeigt. Wer Standort, Zeitraum oder Vergleich ändert, wählt danach neu.

## Was in welcher Datei steht {#inhalt}

Alle Formate zeigen dasselbe in derselben Reihenfolge: Titel mit Betrieb und Standort, Zeitraum und Vergleichszeitraum, „Erstellt am … von …“, die Alarme, die Kennzahlen in ihren vier Gruppen (Kennzahl, Einheit, Wert, Vergleichswert, Veränderung, Veränderung in Prozent), den Verlauf und dann die Tabellen: Wochentag und Tageszeit, Vorschau, Räume, Erlöskonten, Zahlungsarten, Personal, Behandlungen, Aktionen, Kanäle und Gäste. Bei Quoten steht die Veränderung in Prozentpunkten.

- **PDF:** A4 hoch, z. B. `Management-Bericht-Wien-2026-11-01-bis-2026-11-26.pdf`. Der Verlauf steht als graue Balken mit dem Wert daneben, Umsatz und Auslastung mit Prozent von 0 bis 100 % Länge. Ein Abschnitt ohne Zeilen nennt „Keine Einträge im Zeitraum.“ Das PDF ist schwarz-weiß und druckt auf jedem Drucker sauber.
- **Excel:** eine Arbeitsmappe, z. B. `Bericht_2026-11-01_bis_2026-11-26.xlsx`, mit einem Blatt je Abschnitt: **Übersicht**, **Verlauf**, **Erlöskonten**, **Zahlungsarten**, **Personal**, **Behandlungen**, **Aktionen**, **Räume**, **Wochentage**, **Kanäle**, **Vorschau**, **Gäste**, **Vergleich mit Spas** und **Hinweise**. Betrieb, Standort, Zeitraum, Vergleich und wer die Datei wann erstellt hat stehen rechts neben der Übersicht. Jedes Blatt hat eine fixierte Kopfzeile mit Filter; Beträge sind echte Zahlen, Anteile stehen im Prozentformat. Auslastung und Umsatz haben graue Datenbalken, die Veränderung graue Pfeile. Der Verlauf eignet sich als Grundlage für eigene Pivot-Tabellen. Gedruckt wird auf A4 quer, mit der Kopfzeile auf jeder Seite.
- **Word:** ein Dokument auf A4 hoch zum Weiterarbeiten, z. B. `Management-Bericht-Wien-2026-11-01-bis-2026-11-26.docx`. Der Inhalt ist der des PDFs, aber der Verlauf steht als Tabelle und es gibt keine Diagramme. Nach jeder Kennzahlen-Gruppe steht „Anmerkungen:“ mit einem leeren Absatz für eigene Notizen. Kopf und Fuß tragen Betrieb, Standort und „Seite 1 von 3“. Die Schrift ist Arial; die Seitenzahl füllt Word beim Öffnen.
- **Kennzahlen (CSV):** nur die Kennzahlen mit Wert, Vergleichswert und Einheit, für andere Programme. Ohne Alarme, Tabellen und Verlauf.

Der **Vergleich mit anderen Spas** steht in Excel, PDF und Word nur, wenn er auf der Seite eingeschaltet ist ([Mit anderen Spas vergleichen](hilfe:berichte-mit-anderen-spas-vergleichen)).

## Drucken, speichern und senden {#pdf}

**Drucken …**, **Als PDF speichern** und **Per Mail senden …** arbeiten wie bei jedem anderen Dokument der App, mit Druckdialog, Speicherdialog und Rückfrage vor dem Senden. Die Schritte stehen unter [Dokumente drucken, als PDF speichern und per Mail senden](hilfe:dokumente-drucken-speichern-senden). Beim Bericht gilt zusätzlich:

- Die Mail an eine Person geht nur an die Adresse, die im Feld **An** steht. Vorbelegt ist die eigene Mailadresse, nie die einer anderen Person.
- Betreff mit Standort und Zeitraum und ein kurzer Text mit Gruß sind vorbelegt.
- Mit dem Anhang darf die Mail höchstens 5 MB groß sein.

## Wer was sieht {#rechte}

Jede Datei zeigt nur, was die Person auf der Seite auch sieht:

- **Beträge:** Ohne das Recht „Berichte mit Beträgen“ fehlen alle Beträge und die Anteile daraus, in Excel auch die Blätter **Erlöskonten** und **Zahlungsarten** ([Berichte mit Beträgen](hilfe:zugaenge#berichte-mit-betraegen)). Anzahlen, Auslastung und Quoten bleiben.
- **Gäste:** Die Listen **Beste Gäste** und **Abwandernde Stammgäste** mit Namen stehen nur, wenn die Person auch die Kundenliste sehen darf. Sonst fehlt in Excel das Blatt **Gäste**.
- **Gesundheitsdaten** stehen in keiner Datei.

## KI-Text in der Datei {#ki}

Zwei Texte der KI können im Bericht stehen, jeder mit seinem Hinweis, dass er von der KI stammt:

- **Kommentar der KI:** Er steht in PDF und Word, wenn die Seite ihn zeigt ([Kommentar der KI](hilfe:berichte-kennzahlen-ansehen#ki-kommentar)).
- **Übernommene Auswertung:** Wer in der Seitenleiste **In den Bericht übernehmen** gewählt hat, hat die Auswertung in **Drucken …**, **Als PDF speichern**, **Per Mail senden …**, **Bericht (Excel)** und **Bericht (Word)**. In Excel steht sie auf dem Blatt **Hinweise**. In **Kennzahlen (CSV)** steht sie nicht. Ändern sich Standort, Zeitraum oder Vergleich, gilt die Übernahme nicht mehr ([Bericht mit KI auswerten](hilfe:berichte-mit-ki-auswerten#uebernehmen)).

Der Bericht, der montags und am Monatsersten per Mail kommt, hat das PDF im Anhang, aber keine Auswertung der KI.

## Export-Protokoll {#protokoll}

Jede gespeicherte, gedruckte oder gesendete Datei steht im Export-Protokoll, mit Format, Zeitraum und Person, eine Sendung auch mit der Adresse. Das gilt für PDF, Word, Excel und CSV.

## Wenn etwas schiefgeht {#wenn-etwas-schiefgeht}

- **Drucken …**, **Als PDF speichern** und **Per Mail senden …** fehlen im Menü: Der Bericht ist noch nicht geladen. Kurz warten oder den Zeitraum prüfen.
- „Nicht gespeichert: …“ oder „Das Dokument kam nicht an.“ steht als rote Meldung in der Kopfzeile des Berichts. Das Kreuz schließt sie; danach noch einmal wählen.
- Die Word-Datei zeigt „Seite 1 von 1“ statt der echten Zahl: Word füllt die Seitenzahl beim Öffnen oder Drucken. Ein Betrachter ohne Word zeigt „Seite 1 von 1“.
