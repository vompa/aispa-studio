# 0101. Drucken mit dem Druckdialog von Windows statt Verb `print`

- **Status:** angenommen
- **Datum:** 26.11.2026
- **Entschieden von:** Auftraggeber (Druckdialog von Windows, Vermerk beim Öffnen), Umsetzung Claude
- **Ergänzt:** [ADR 0092](0092-dokumente-als-pdf-ueber-pdfsharp.md)

## Kontext

Nach ADR 0092 druckte der Knopf **Drucken** jedes Dokument über das Verb `print` der Windows-Shell. Das PDF-Programm des PCs schickte es ohne Rückfrage an den Standarddrucker. Ist dort ein Dateidrucker eingestellt (Microsoft Print to PDF, OneNote), entstand kein Ausdruck. Stattdessen öffnete sich OneNote oder eine Frage nach dem Speicherort (Fehlerbericht des Auftraggebers vom 26.11.2026, Befund Ä388). Verlangt ist der übliche Druckdialog mit Druckerwahl, Seiten, Kopien und Ausrichtung. Betroffen sind alle Dokumente mit dem Knopfblock `DocumentActions`; der Bondrucker der Kassa (ESC/POS) nicht.

## Optionen

| Option | Bewertung |
| --- | --- |
| A: Druckdialog von Windows über das WebView2 der App (`ICoreWebView2_16::ShowPrintUI` mit der Art `System`) | gewählt: kein neues Paket, Vektordruck durch den PDF-Betrachter von WebView2, der Dialog von Windows 11 auch mit unsichtbarem Fenster; meldet kein Ergebnis |
| B: Seiten mit `Windows.Data.Pdf` rendern, über `System.Drawing.Printing` mit dem WinForms-Dialog drucken | verworfen: Ziel-Framework `net10.0-windows10.0.19041.0` und `UseWindowsForms`, rund 25 MB mehr (`Microsoft.Windows.SDK.NET.dll` 24.887.096 Byte), Rasterdruck statt Text (eine A4-Seite mit 300 dpi: 3508 × 2479 Bildpunkte, 482 ms), alter Dialog |
| C: Druckvorschau des Browsers (`NativeWebView.ShowPrintUI()`, öffentlich im Paket) | verworfen: braucht ein sichtbares Fenster mit dem PDF in der App |

Gemessen in der Machbarkeitsprobe 5a am 26.11.2026 (Arbeitsanweisung Wochenplan, Abschnitt 7) mit einem A4-quer-PDF.

## Entscheidung

- `IDocumentPrinter` bleibt die Schnittstelle des Knopfblocks. Tests beantworten sie selbst. Die Umsetzung `WindowsPrintDialogPrinter` prüft wie bisher den Standarddrucker, schreibt das PDF in den Ordner `%TEMP%\AispaStudio\Druck` und gibt es an `PrintDialogHost`.
- `PrintDialogHost` ist ein Fenster ohne Taskleisteneintrag außerhalb des Bildschirms mit einem `NativeWebView` (nur WebView2, eigener Datenordner `%LOCALAPPDATA%\AispaStudio\druck`, InPrivate). Es lädt das PDF, wartet 1,5 s, bis der Betrachter gezeichnet hat, und ruft `WebView2PrintDialog.TryShow` auf. Ein Fenster gilt für die ganze Laufzeit und schließt mit dem Hauptfenster.
- Das Paket Avalonia.Controls.WebView 12.1 macht den Dialog von Windows nicht öffentlich. Die App erreicht ihn über den öffentlichen Plattform-Handle (`IWindowsWebView2PlatformHandle.CoreWebView2`) und die COM-Tabelle von `ICoreWebView2_16`, Stelle 114.
- Der Dialog meldet weder „gedruckt“ noch „abgebrochen“ zurück. Die App sagt deshalb „Druckdialog geöffnet“ statt „Gedruckt“. Der Gutschein bekommt seinen Vermerk, sobald der Dialog aufgeht; die Gutscheinliste nennt ihn „Zum Drucken geöffnet am …“ (Auftraggeber, 26.11.2026).

## Begründung

Option A nutzt, was die App für die Hilfe schon mitbringt, und druckt das PDF als Vektor mit seinen Schriften. Option B hätte die App um ein Fünftel größer gemacht und unscharfe Ränder an Text gebracht. Der fehlende Rückgabewert ist der Preis von A. Ein Vermerk beim Öffnen, der ehrlich so heißt, ist besser als einer, der einen Ausdruck behauptet.

## Folgen und Stand

- Code: `Services/DocumentPrinter.cs`, `Views/PrintDialogHost.cs`, `Views/WebView2PrintDialog.cs`; Meldungen „Kein Drucker eingerichtet“ und „Der Druckdialog ließ sich nicht öffnen“. Die Meldung „Kein PDF-Programm zum Drucken eingerichtet“ entfällt, weil kein fremdes PDF-Programm mehr druckt.
- Die Stelle 114 hängt an der Reihenfolge der COM-Schnittstelle, die Microsoft nicht mehr ändert. Ein Wechsel des Pakets Avalonia.Controls.WebView prüft den Druck im Handtest.
- Der Dialog trägt den Titel „Microsoft Edge WebView2 – Drucken“, die App kann ihn nicht ändern. In der Probe stand er bei einem Querformat-PDF auf Hochformat; der Handtest prüft das am Aushang.
- Ohne WebView2-Laufzeit oder mit einer Laufzeit vor `ICoreWebView2_16` steht „Der Druckdialog ließ sich nicht öffnen“; **PDF speichern** bleibt.
- Bewusst offen: Vorschau vor dem Druck, Merken des zuletzt gewählten Druckers je Dokumentart, Drucken aus der Weboberfläche.

## Quellen

- [Arbeitsanweisung Wochenplan](../umsetzung/arbeitsanweisung-wochenplan-ausbau.md), Abschnitt 7
- Microsoft Learn, WebView2 Win32 API: `ICoreWebView2_16::ShowPrintUI`, `COREWEBVIEW2_PRINT_DIALOG_KIND`
- [ADR 0079](0079-eine-hilfe-fuer-windows-app-und-web.md), [ADR 0092](0092-dokumente-als-pdf-ueber-pdfsharp.md)
