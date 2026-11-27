# Dokumente: Gestaltung der PDF-Ausgaben

Verbindlich für alles, was die API als PDF erzeugt (ADR 0092): Rechnung und Beleg, Terminbestätigung, Datenschutz-Auskunft, Tagesabschluss, Tagesplan, Gutschein, Kundenkarte, Warteliste, Dienstplan-Aushang, Stundenliste, Inventurliste, Bestellung, Ablaufliste, Gutscheinliste, Personalakte, Management-Bericht. Umgesetzt im `DocumentBuilder` (`src/AispaStudio.Api/Documents/`); wer ein Dokument baut, setzt es aus seinen Bausteinen zusammen und zeichnet nichts selbst. Texte nach [texte.md](texte.md).

## 1. Grundsätze

- **Druckt auf jedem Drucker:** nur Schwarz und ein Grau (Text 40 % Grau, Linien 70 % Grau, Flächen 94 % Grau), kein Akzent, keine Farben. Flächen nur für graue Zeilen (Pausen, Sperrzeiten).
- **Eine Schrift:** Liberation Sans (eingebettet, SIL OFL), Regular und Bold; keine Kursive, keine Versalien zur Betonung.
- **Linien nur waagrecht:** unter der Kopfzeile einer Tabelle (schwarz, 0,75 pt), unter jeder Zeile (grau, 0,25 pt), über der Summenzeile (schwarz). Keine senkrechten Linien, keine Rahmen um Tabellen oder Kästen.
- **Beträge rechtsbündig, Summen fett.** Zahlen und Daten im Format de-AT: `1.234,56`, `09.11.2026`, `09.11.2026 10:12`, Uhrzeit `10:12`; Währung als Wort in der Spaltenüberschrift oder der Zeile („Summe EUR“), nicht hinter jeder Zahl.
- **Ein Logo** kommt erst, wenn die Firmendaten eines haben (heute nicht).

## 2. Formate

| Format | Seite | Rand | Schrift (Titel / Überschrift / Text / Klein) | Wofür |
| --- | --- | --- | --- | --- |
| A4 hoch | 210 × 297 mm | 17 mm (48 pt) | 16 / 12 / 10 / 8 pt | Rechnung, Bestätigung, Auskunft, Tagesabschluss, Gutschein, Kundenkarte, Warteliste, Stundenliste, Inventur, Bestellung, Ablaufliste, Personalakte, Management-Bericht |
| A4 quer | 297 × 210 mm | 17 mm | wie A4 hoch | Tagesplan, Dienstplan-Aushang, Gutscheinliste |
| Bon 80 mm | 80 mm breit, Höhe wächst mit dem Inhalt, eine Seite | 3,5 mm (10 pt) | 11 / 9 / 8 / 7 pt | Beleg im Bon-Format (Nachdruck, PDF) |

Zeilenhöhe ist das 1,3-Fache der Schriftgröße. Mehrseitige Dokumente gibt es nur in A4.

## 3. Kopf und Fuß

**Kopf (A4, auf jeder Seite):** links der Spa-Name (14 pt fett), darunter der Standort klein in Grau (Name, Straße, PLZ Ort, Telefon, Mail); rechts, rechtsbündig und klein in Grau, die Firmendaten (Firmenwortlaut, Anschrift des Unternehmens, UID, Firmenbuchnummer mit Gericht). Unter dem Kopf eine schwarze Linie über die ganze Breite. Firmendaten kommen vom Mandanten (ADR 0052) und werden je Standort überschrieben, wo der Standort eigene hat. Ohne Standort (Personalakte) fehlt die Standortspalte.

**Kopf (Bon):** alles zentriert: Spa-Name (11 pt fett), darunter Firmendaten und Standort klein; dann eine graue Linie.

**Fuß (A4):** graue Linie, darunter links „Erstellt am 09.11.2026 10:12 von Lena“ (Vorname des Rufnamens der Person, die das Dokument erzeugt; ohne Person nur das Datum), rechts „Seite 1 von 2“. **Fuß (Bon):** nur „Erstellt am …“, zentriert.

## 4. Bausteine

| Baustein | Aussehen | Regel |
| --- | --- | --- |
| Anzeigezeile | 32 pt fett, zentriert | Nur auf dem Geschenkgutschein: der Spa-Name und der Wert oder die Behandlung (`Display`). Sonst nicht verwenden. |
| Titel | 16 pt fett, links | Genau einer, erster Baustein nach dem Kopf: „Rechnung Nr. K1-142“, „Tagesplan Dienstag, 10.11.2026“. Er wird auch Titel der PDF-Datei. |
| Datum / Untertitel | Text in Grau | Direkt unter dem Titel: Datum, Zeitraum, Kasse; keine zweite Überschrift. |
| Überschrift | 12 pt fett, Abstand davor | Abschnitte: „Positionen“, „Zahlungen“, „Stammdaten“. Bleibt bei ihrem Inhalt (kein Umbruch dazwischen). |
| Absatz | 10 pt, Umbruch an der Breite | Fließtext; Hinweise in Grau; Wichtiges fett (z. B. „Kopie“, „Storno“). |
| Schlüssel-Wert | Beschriftung grau links (höchstens 45 % der Breite), Wert rechts | Stammdaten, Kontakt, Kassendaten; leere Werte als „–“. |
| Tabelle | Kopfzeile klein fett mit schwarzer Linie, Zeilen mit grauer Linie, Summe fett mit Linie darüber | Spalten mit Gewicht und Ausrichtung; Beträge rechts; die Kopfzeile wiederholt sich auf jeder Folgeseite; graue Zeilen für Pausen und Sperrzeiten; im Tagesplan (eine Spalte je Person oder Raum) nur die grauen Zellen (`MutedCells`); Listen mit so vielen Spalten, dass 10 pt nicht passen (Gutscheinliste), setzen die Zeilen in 8 pt (`compact`). |
| Waagrechte Balken | Kopfzeile wie eine Tabelle; je Zeile Beschriftung links (ein Viertel der Breite), grauer Balken (Grau des Texts, 40 %) bis 55 % der Breite im Verhältnis zum größten Wert, Wert rechtsbündig | Verläufe und Rangfolgen, z. B. der Verlauf im Management-Bericht (`Bars`). Keine Achsen, keine Gitter, keine senkrechten Linien; der Wert steht immer als Zahl daneben, der Balken allein trägt keine Aussage. Werte unter null bekommen keinen Balken. Die Kopfzeile wiederholt sich auf Folgeseiten. |
| QR-Code | schwarze Module, 30 mm, zentriert (A4) | Belegcode der RKSV (wie der Bondrucker), Gutscheincode. |
| Unterschriftenzeilen | Linie, darunter Beschriftung klein in Grau | Tagesabschluss („Gezählt von“, „Geprüft von“), Stundenliste („Mitarbeiter:in“, „Leitung“). |
| Linie, Abstand, Seitenumbruch | – | Zum Gliedern; sparsam. |

## 5. Dateinamen und Mail

- Dateiname aus Art und Nummer oder Name: `Rechnung-K1-142.pdf`, `Beleg-K1-143.pdf`, `Terminbestätigung-01-03-2027.pdf`, `Auskunft-Eva-Muster.pdf`, `Tagesabschluss-K1-2026-11-09.pdf`, `Tagesplan-Wien-2027-03-01.pdf`, `Gutschein-K7QM-2XPA-9RDT.pdf`, `Stammblatt-Eva-Muster.pdf`, `Warteliste-Wien-2027-03-03.pdf`, `Dienstplan-Wien-2027-03-01.pdf` (Montag der Woche), `Stundenliste-Anna-2026-06.pdf`, `Inventurliste-Raumschrank-2-2027-03-01.pdf`, `Zählliste-Raumschrank-2-2027-03-01.pdf`, `Bestellung-7-Haslauer.pdf`, `Ablaufliste-Wien-2027-03-01.pdf`, `Gutscheinliste-Wien-2026-11-09.pdf`, `Management-Bericht-Wien-2026-11-01-bis-2026-11-26.pdf`. Buchstaben und Ziffern bleiben (auch Umlaute), alles andere wird ein Bindestrich (`DocumentFiles.PdfName`).
- Mail: Betreff nennt das Dokument und das Spa („Ihre Rechnung Nr. K1-142 von Day-Spa Wien“); Text in zwei bis drei Sätzen, unpersönlich oder mit „Sie“ (texte.md T1), mit Gruß und Spa-Name; das PDF hängt an; Absender ist der Name des Spas, Antwortadresse das Postfach des Spas. Jede gesendete Mail steht danach als Vermerk am Beleg oder im Verlauf des Termins („Per Mail gesendet an eva@example.at am 09.11.2026 10:12 von Lena“; an der Bestellung „Gesendet am 09.11.2026 10:12 an bestellung@haslauer.example.at von Lena“; das Senden ändert den Stand der Bestellung nicht).
- Versandweg: `SentDocument.Channel` unterscheidet Mail (`mail`, auch alle Vermerke vor dem Zusatz), Druck (`print`, die App meldet ihn, sobald der Druckdialog für den Gutschein aufgeht; der Dialog meldet keinen Ausdruck zurück, [ADR 0101](../entscheidungen/0101-drucken-mit-druckdialog-statt-verb-print.md)) und Übergabe (`handover`, Knopf „Übergeben“ am Gutschein). Unter „Per Mail gesendet an …“ stehen nur Mails; Druck und Übergabe haben keine Adresse und erscheinen nur als „Zum Drucken geöffnet am …“ und „Übergeben am …“ in der Gutscheinliste. Die Mail eines online gekauften Gutscheins vermerkt der Online-Shop selbst (ohne Person).
- Knopfblock: „Drucken · PDF speichern · Per Mail senden“; ein zweites Format (Beleg im Bon-Format) steht im Menü an „PDF speichern“. Wo daneben ein anderer „Drucken“-Knopf steht (Kassa: Bondrucker), heißt der Druck des PDFs „A4 drucken“.
- Der Management-Bericht geht nur an die Adresse, die im Dialog steht: vorbelegt ist die eigene Adresse der Person, die sendet, nie eine gespeicherte fremde. Jede Sendung steht im Export-Protokoll („Export Bericht per Mail (PDF, …, an …)“), jede gespeicherte oder gedruckte Datei als „Export Bericht (PDF, …)“.

## 6. Datenschutz

Gesundheitsdaten stehen in keinem Dokument, außer in der Datenschutz-Auskunft für den Gast selbst, und dort nur, wenn die aufrufende Person die Anamnese in der App sehen darf; sonst steht der feste Satz „Gesundheitsangaben: vorhanden, Auskunft durch die Leitung“. Jedes Dokument folgt dem Recht der Daten, die es zeigt (Beleg `ViewReceipts`, Kundenverlauf wie die Kundenkarte, Stundenliste und Dienstplan nur Leitung, Lager wie die Lagerseite, Management-Bericht wie die Seite „Berichte“: Beträge nur mit „Berichte mit Beträgen“, Gästenamen nur mit dem Recht der Kundenliste).

## 7. Prüfen

Jedes Dokument hat einen Test, der das PDF zurückliest (`PdfText` im API-Testprojekt: Seitenzahl, Text aus den Textoperatoren) und Titel, einen Namen und eine Summe findet. Zusätzlich wird ein Dokument je Art einmal als Datei gespeichert und mit `pymupdf` als Bild angesehen (Sichtprüfung, Ergebnis im PR-Protokoll).

## 8. Berichte als PDF, Excel und Word

Der Management-Bericht entsteht einmal als Inhalt (`ManagementReportDocument`); PDF, Excel und Word lesen nur diesen Inhalt und rechnen nichts.

- **PDF:** A4 hoch. Seite 1 Titel mit Standort, Zeitraum und Vergleich, KI-Kurzfassung mit Hinweis (nur wenn gewählt und vorhanden), Warnungen, Kennzahlen je Gruppe (Kennzahl, Einheit, Wert, Vergleich, Veränderung, Veränderung in %); danach Verlauf als waagrechte Balken und die übrigen Abschnitte als Tabellen. Ab fünf Spalten stehen Zeilen in 8 pt; Spalten sind so breit wie das längste Wort ihres Kopfs. Leere Abschnitte nennen „Keine Einträge im Zeitraum.“ A4 quer bräuchte erst eine Tabelle, die so nicht passt; heute hat keine mehr als sieben Spalten.
- **Excel (Arbeitsmappe):** ein Blatt je Abschnitt, Blattname wie die Überschrift; Kopfzeile fett und fixiert, Autofilter; Beträge `#,##0.00`, Anteile im Prozentformat, Daten als Datum; Datenbalken nur in Grau (`#A6A6A6`) in Spalten für Umsatz und Auslastung, Veränderung mit dem grauen Pfeil-Symbolsatz; sonst nur Schwarz, keine Füllfarben, keine Excel-Tabellenformate mit Streifen. Druckeinrichtung A4 quer, auf eine Seite Breite, Kopfzeile wiederholt, Fuß „Seite n von m“. Der Kopfblock (Betrieb, Standort, Zeitraum, Vergleich, Erstellt) steht auf dem ersten Blatt; Hinweise und KI-Text (mit Hinweis) auf dem letzten Blatt „Hinweise“.
- **Word (.docx):** Inhalt wie das PDF ohne Balken (der Verlauf als Tabelle), A4 hoch, Größen wie im PDF (Titel 16, Überschrift 12, Text 10, klein 8 pt, Rand 48 pt). Die Schrift heißt „Arial“, metrisch gleich der Liberation Sans der PDFs und auf jedem Rechner mit Word vorhanden; die API bettet keine Schrift ein. Kopf mit Betrieb, Standort und Firmenzeilen, Fuß mit „Erstellt am … von …“ und „Seite n von m“ (Felder). Nach jeder Kennzahlen-Gruppe „Anmerkungen:“ mit einem leeren Absatz zum Ergänzen. Nur Schwarz und Grau, keine Word-Diagramme, Tabellenköpfe fett und auf jeder Seite wiederholt. Gebaut mit `WordDocumentBuilder` (Open XML SDK), Sprache de-AT.
