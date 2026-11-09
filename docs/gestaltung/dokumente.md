# Dokumente: Gestaltung der PDF-Ausgaben

Verbindlich für alles, was die API als PDF erzeugt (ADR 0091): Rechnung und Beleg, Terminbestätigung, Datenschutz-Auskunft, Tagesabschluss, Tagesplan, Gutschein, Kundenkarte, Warteliste, Dienstplan-Aushang, Stundenliste, Inventurliste, Bestellung, Ablaufliste, Personalakte. Umgesetzt im `DocumentBuilder` (`src/AispaStudio.Api/Documents/`); wer ein Dokument baut, setzt es aus seinen Bausteinen zusammen und zeichnet nichts selbst. Texte nach [texte.md](texte.md).

## 1. Grundsätze

- **Druckt auf jedem Drucker:** nur Schwarz und ein Grau (Text 40 % Grau, Linien 70 % Grau, Flächen 94 % Grau), kein Akzent, keine Farben. Flächen nur für graue Zeilen (Pausen, Sperrzeiten).
- **Eine Schrift:** Liberation Sans (eingebettet, SIL OFL), Regular und Bold; keine Kursive, keine Versalien zur Betonung.
- **Linien nur waagrecht:** unter der Kopfzeile einer Tabelle (schwarz, 0,75 pt), unter jeder Zeile (grau, 0,25 pt), über der Summenzeile (schwarz). Keine senkrechten Linien, keine Rahmen um Tabellen oder Kästen.
- **Beträge rechtsbündig, Summen fett.** Zahlen und Daten im Format de-AT: `1.234,56`, `09.11.2026`, `09.11.2026 10:12`, Uhrzeit `10:12`; Währung als Wort in der Spaltenüberschrift oder der Zeile („Summe EUR“), nicht hinter jeder Zahl.
- **Ein Logo** kommt erst, wenn die Firmendaten eines haben (heute nicht).

## 2. Formate

| Format | Seite | Rand | Schrift (Titel / Überschrift / Text / Klein) | Wofür |
| --- | --- | --- | --- | --- |
| A4 hoch | 210 × 297 mm | 17 mm (48 pt) | 16 / 12 / 10 / 8 pt | Rechnung, Bestätigung, Auskunft, Tagesabschluss, Gutschein, Kundenkarte, Warteliste, Stundenliste, Inventur, Bestellung, Ablaufliste, Personalakte |
| A4 quer | 297 × 210 mm | 17 mm | wie A4 hoch | Tagesplan, Dienstplan-Aushang |
| Bon 80 mm | 80 mm breit, Höhe wächst mit dem Inhalt, eine Seite | 3,5 mm (10 pt) | 11 / 9 / 8 / 7 pt | Beleg im Bon-Format (Nachdruck, PDF) |

Zeilenhöhe ist das 1,3-Fache der Schriftgröße. Mehrseitige Dokumente gibt es nur in A4.

## 3. Kopf und Fuß

**Kopf (A4, auf jeder Seite):** links der Spa-Name (14 pt fett), darunter der Standort klein in Grau (Name, Straße, PLZ Ort, Telefon, Mail); rechts, rechtsbündig und klein in Grau, die Firmendaten (Firmenwortlaut, Anschrift des Unternehmens, UID, Firmenbuchnummer mit Gericht). Unter dem Kopf eine schwarze Linie über die ganze Breite. Firmendaten kommen vom Mandanten (ADR 0052) und werden je Standort überschrieben, wo der Standort eigene hat. Ohne Standort (Personalakte) fehlt die Standortspalte.

**Kopf (Bon):** alles zentriert: Spa-Name (11 pt fett), darunter Firmendaten und Standort klein; dann eine graue Linie.

**Fuß (A4):** graue Linie, darunter links „Erstellt am 09.11.2026 10:12 von Lena“ (Vorname des Rufnamens der Person, die das Dokument erzeugt; ohne Person nur das Datum), rechts „Seite 1 von 2“. **Fuß (Bon):** nur „Erstellt am …“, zentriert.

## 4. Bausteine

| Baustein | Aussehen | Regel |
| --- | --- | --- |
| Titel | 16 pt fett, links | Genau einer, erster Baustein nach dem Kopf: „Rechnung Nr. K1-142“, „Tagesplan Dienstag, 10.11.2026“. Er wird auch Titel der PDF-Datei. |
| Datum / Untertitel | Text in Grau | Direkt unter dem Titel: Datum, Zeitraum, Kasse; keine zweite Überschrift. |
| Überschrift | 12 pt fett, Abstand davor | Abschnitte: „Positionen“, „Zahlungen“, „Stammdaten“. Bleibt bei ihrem Inhalt (kein Umbruch dazwischen). |
| Absatz | 10 pt, Umbruch an der Breite | Fließtext; Hinweise in Grau; Wichtiges fett (z. B. „Kopie“, „Storno“). |
| Schlüssel-Wert | Beschriftung grau links (höchstens 45 % der Breite), Wert rechts | Stammdaten, Kontakt, Kassendaten; leere Werte als „–“. |
| Tabelle | Kopfzeile klein fett mit schwarzer Linie, Zeilen mit grauer Linie, Summe fett mit Linie darüber | Spalten mit Gewicht und Ausrichtung; Beträge rechts; die Kopfzeile wiederholt sich auf jeder Folgeseite; graue Zeilen für Pausen und Sperrzeiten. |
| QR-Code | schwarze Module, 30 mm, zentriert (A4) | Belegcode der RKSV (wie der Bondrucker), Gutscheincode. |
| Unterschriftenzeilen | Linie, darunter Beschriftung klein in Grau | Tagesabschluss („Gezählt von“, „Geprüft von“), Stundenliste („Mitarbeiter:in“, „Leitung“). |
| Linie, Abstand, Seitenumbruch | – | Zum Gliedern; sparsam. |

## 5. Dateinamen und Mail

- Dateiname aus Art und Nummer oder Name: `Rechnung-K1-142.pdf`, `Beleg-K1-143.pdf`, `Terminbestätigung-01-03-2027.pdf`, `Auskunft-Eva-Muster.pdf`, `Tagesabschluss-K1-2026-11-09.pdf`. Buchstaben und Ziffern bleiben (auch Umlaute), alles andere wird ein Bindestrich (`DocumentFiles.PdfName`).
- Mail: Betreff nennt das Dokument und das Spa („Ihre Rechnung Nr. K1-142 von Day-Spa Wien“); Text in zwei bis drei Sätzen, unpersönlich oder mit „Sie“ (texte.md T1), mit Gruß und Spa-Name; das PDF hängt an; Absender ist der Name des Spas, Antwortadresse das Postfach des Spas. Jede gesendete Mail steht danach als Vermerk am Beleg oder im Verlauf des Termins („Per Mail gesendet an eva@example.at am 09.11.2026 10:12 von Lena“).
- Knopfblock: „Drucken · PDF speichern · Per Mail senden“; ein zweites Format (Beleg im Bon-Format) steht im Menü an „PDF speichern“. Wo daneben ein anderer „Drucken“-Knopf steht (Kassa: Bondrucker), heißt der Druck des PDFs „A4 drucken“.

## 6. Datenschutz

Gesundheitsdaten stehen in keinem Dokument, außer in der Datenschutz-Auskunft für den Gast selbst, und dort nur, wenn die aufrufende Person die Anamnese in der App sehen darf; sonst steht der feste Satz „Gesundheitsangaben: vorhanden, Auskunft durch die Leitung“. Jedes Dokument folgt dem Recht der Daten, die es zeigt (Beleg `ViewReceipts`, Kundenverlauf wie die Kundenkarte, Stundenliste und Dienstplan nur Leitung, Lager wie die Lagerseite).

## 7. Prüfen

Jedes Dokument hat einen Test, der das PDF zurückliest (`PdfText` im API-Testprojekt: Seitenzahl, Text aus den Textoperatoren) und Titel, einen Namen und eine Summe findet. Zusätzlich wird ein Dokument je Art einmal als Datei gespeichert und mit `pymupdf` als Bild angesehen (Sichtprüfung, Ergebnis im PR-Protokoll).
