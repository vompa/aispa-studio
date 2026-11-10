# 0092. Dokumente als PDF über PDFsharp mit eingebetteter Schrift

- **Status:** angenommen
- **Datum:** 09.11.2026
- **Entschieden von:** Auftraggeber (Paket und Aufbau in der Arbeitsanweisung vorgegeben), Umsetzung Claude nach ADR 0019

## Kontext

Rezeption und Betreiber sollen die wichtigen Dinge der App als Dokument in der Hand oder im Postfach haben: Rechnungen und Belege nachdrucken, als PDF speichern und dem Gast per Mail schicken; Terminbestätigung, Kundenverlauf als Datenschutz-Auskunft, Tagesabschluss; dazu Tagesplan, Gutschein, Kundenkarte, Warteliste, Dienstplan-Aushang, Stundenliste, Inventurliste, Bestellung und Ablaufliste ([Arbeitsanweisung PDF-Ausgaben](../umsetzung/arbeitsanweisung-pdf-ausgaben.md)). Bis dahin gab es nur `SimplePdf` (reiner Text als PDF ohne Paket, für die Personalakte), Belege als Text und ESC/POS für den Bondrucker und Mails ohne Anhang.

Die API läuft in Linux-Containern ohne Schriften; „ü“, „€“ und „–“ müssen trotzdem überall stimmen. Alle Dokumente sollen gleich aussehen (ein Kopf, eine Schrift, dieselben Tabellen) und in der App dieselben drei Knöpfe haben: **Drucken**, **PDF speichern**, **Per Mail senden**.

## Optionen

| Option | Bewertung |
| --- | --- |
| `SimplePdf` erweitern (eigener PDF-Schreiber) | Kein Paket, aber Tabellen, Seitenumbruch, Schrifteinbettung und QR-Codes wären Eigenbau mit eigenen Fehlern; die Standardschriften des PDF-Lesers haben keine verlässlichen Metriken für Umlaute außerhalb von WinAnsi. Verworfen. |
| **PDFsharp 6.2.4** (MIT, PDFsharp Team) | Zeichnet Text, Linien und Rechtecke mit eigenem Font-Resolver für eingebettete TrueType-Schriften; liest PDFs auch zurück (Seitenzahl, Inhaltsstrom), damit die Tests den Text finden. Layout (Umbruch, Tabellen, Seiten) bleibt eigener, kleiner Code (`DocumentBuilder`). **Gewählt.** 6.2.4 ist die letzte Fassung unter MIT; ab 6.5.0 (14.10.2026) steht PDFsharp unter AGPL-3.0, deshalb bleibt die Version gepinnt und Dependabot ignoriert höhere (`.github/dependabot.yml`). |
| QuestPDF | Bequemes Layout, aber Lizenz mit Umsatzgrenze (Community nur bis 1 Mio. USD Jahresumsatz, sonst kostenpflichtig); vom Auftraggeber ausgeschlossen. |
| Browser-Druck (HTML in der App rendern und drucken) | Kein PDF zum Speichern oder Versenden ohne weiteren Konverter; Darstellung hängt vom Browser ab; nicht aus der API heraus. Verworfen. |

Schrift: Inter (Hausschrift der App) liegt nur als Ressource in der Avalonia-DLL vor, nicht als Datei. Gewählt ist **Liberation Sans 2.1.5** (SIL Open Font License 1.1, Regular und Bold), metrisch kompatibel zu Arial, mit Umlauten, Euro und Gedankenstrich; die Dateien liegen mit Lizenztext unter `src/AispaStudio.Api/Documents/Fonts/` und werden als Ressource in die API eingebettet. Die Zeichen werden in WinAnsi kodiert: alles Deutsche passt hinein, und die Tests lesen den Text direkt aus den Textoperatoren der Seite zurück.

## Entscheidung

- Ein gemeinsamer Dokument-Dienst in `src/AispaStudio.Api/Documents/`: `DocumentBuilder` (Titel, Überschriften, Absätze, Schlüssel-Wert-Listen, Tabellen mit Ausrichtung je Spalte und wiederholter Kopfzeile nach dem Seitenumbruch, QR-Code aus `QRCoder` als Rechtecke, Unterschriftenzeilen, Seitenumbruch) in den Formaten A4 hoch, A4 quer und Bon 80 mm (Höhe wächst mit dem Inhalt); `DocumentHeader`/`DocumentHeaders` für Kopf (Spa-Name, Firmendaten, Standort) und Fuß („Seite n von m“, „Erstellt am … von …“); `DocumentFonts` als Font-Resolver; `DocumentFiles` für Dateinamen und die `application/pdf`-Antwort. Gestaltung: [dokumente.md](../gestaltung/dokumente.md).
- `SimplePdf` ist entfernt; die Personalakte baut über den Dienst, Inhalt unverändert.
- Mails können Anhänge tragen (`OutgoingMail.Attachments`, höchstens 5 MB je Mail); der Versand eines Dokuments geht als `SendSpaMail` in die Queue, mit dem Namen des Spas als Absender und dem Postfach des Spas als Antwortadresse.
- Server rechnet, App zeigt: jedes PDF entsteht in der API (`GET …/pdf` mit Dateiname im `Content-Disposition`), der Versand über `GET …/send` (Vorbelegung) und `POST …/send`. Die App hat einen Knopfblock `DocumentActions` (Drucken · PDF speichern · Per Mail senden), der die Bytes lädt, über den Dialog von Windows speichert, über das Verb `print` der Windows-Shell auf dem Standarddrucker druckt und vor dem Senden den Dialog `SendDocumentDialog` zeigt (Adresse, Betreff, Text vorbelegt).

## Begründung

PDFsharp liefert genau den Teil, der schwer selbst zu bauen ist (PDF-Objekte, Schrifteinbettung, Rücklesen) unter MIT, und lässt das Layout klein und prüfbar in eigener Hand. Eine eingebettete freie Schrift macht die Dokumente unabhängig vom Server-Betriebssystem. Der eine Dienst sorgt dafür, dass alle 17 Dokumente gleich aussehen und jede Maske nur den Knopfblock einbindet.

## Folgen und Stand

- Paket `PDFsharp` 6.2.4 in `Directory.Packages.props`; `QRCoder` auch in der API; Lizenzhinweise über `scripts/third-party-notices.py` (PDFsharp MIT, Liberation Sans OFL).
- Tests: `DocumentBuilderTests` (Seitenumbruch, Kopfzeile auf Seite 2, Umlaute und Euro, QR-Code, Bon wächst), `MailAttachmentTests`, `DocumentActionsTests` (App), Ansichtstest des Knopfblocks und des Dialogs; je Dokument ein Test, der das PDF zurückliest (Titel, ein Name, eine Summe).
- Ein Logo im Kopf kommt erst, wenn die Firmendaten eines haben (heute nicht).
- Bewusst offen (Stufe 3): Kennzahlen-Bericht, Kassenbuch des Monats, gefilterter Verlauf-Export, Umsatz je Behandlung und Person, Anamnesebogen leer und ausgefüllt, Drucken aus der Weboberfläche.

## Quellen

- [Arbeitsanweisung PDF-Ausgaben](../umsetzung/arbeitsanweisung-pdf-ausgaben.md)
- [Gestaltung der Dokumente](../gestaltung/dokumente.md)
- PDFsharp auf NuGet: `nuspec` 6.2.4 (`<license type="expression">MIT</license>`, veröffentlicht 06.01.2026) und 6.5.0 (`AGPL-3.0-only`, 14.10.2026), abgerufen 09.11.2026
- Liberation Fonts 2.1.5, GitHub-Release `liberationfonts/liberation-fonts` (SIL OFL 1.1), abgerufen 09.11.2026
