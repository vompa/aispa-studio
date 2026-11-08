using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using AispaStudio.Contracts;

namespace AispaStudio.Desktop.Services;

/// <summary>
/// Druckbarer Anamnesebogen als HTML-Seite: Kästchen und Linien so, dass die KI ihn vom Foto gut lesen kann (eine Frage je Block). Fragen, Einwilligung und Link kommen aus der API;
/// nur die Beschriftungen des Bogens stehen hier, in den vier Sprachen des Kundenbereichs. Der QR-Code öffnet denselben Bogen im Kundenbereich.
/// </summary>
public static class PaperSheetDocument
{
    /// <summary>Maskiert nur, was HTML verlangt; Umlaute und Akzente bleiben lesbar in der Datei.</summary>
    private static readonly HtmlEncoder Encoder = HtmlEncoder.Create(UnicodeRanges.All);

    private sealed record SheetText(string Title, string For, string Appointment, string Yes, string No, string Qr, string Signature, string Form, string Culture);

    private static readonly Dictionary<string, SheetText> Texts = new()
    {
        ["de"] = new("Gesundheitsfragebogen", "Für", "Termin", "Ja", "Nein", "Lieber am Handy? QR-Code scannen und digital ausfüllen.", "Datum, Unterschrift (Vor- und Nachname)", "Formular", "de-AT"),
        ["en"] = new("Health questionnaire", "For", "Appointment", "Yes", "No", "Prefer your phone? Scan the QR code and fill it in online.", "Date, signature (first and last name)", "Form", "en-GB"),
        ["fr"] = new("Questionnaire de santé", "Pour", "Rendez-vous", "Oui", "Non", "Plutôt sur votre téléphone ? Scannez le code QR et remplissez le questionnaire en ligne.", "Date, signature (prénom et nom)", "Formulaire", "fr-CH"),
        ["it"] = new("Questionario sulla salute", "Per", "Appuntamento", "Sì", "No", "Preferisce il telefono? Scansioni il codice QR e compili il questionario online.", "Data, firma (nome e cognome)", "Modulo", "it-CH"),
    };

    private const string Style = """
        @page { margin: 15mm; }
        body { margin: 0; background: #fff; color: #000; font: 11pt/1.4 Arial, Helvetica, sans-serif; }
        header { display: flex; justify-content: space-between; gap: 16px; }
        h1 { margin: 0 0 8px; font-size: 16pt; }
        figure { margin: 0; max-width: 150px; text-align: center; font-size: 8pt; }
        figure img { width: 120px; height: 120px; }
        ol { padding-left: 20px; }
        li { break-inside: avoid; margin-bottom: 12px; }
        li p { margin: 4px 0; }
        .boxes { display: flex; gap: 32px; font-size: 13pt; }
        .line { border-bottom: 1px solid #000; min-height: 24px; }
        .signature { margin-top: 45px; border-top: 1px solid #000; padding-top: 4px; font-size: 9pt; }
        footer { margin-top: 16px; font-size: 8pt; color: #444; }
        """;

    public static string Html(AnamnesisPaperSheet sheet)
    {
        var text = Texts.GetValueOrDefault(sheet.Form.Language) ?? Texts["de"];
        var qr = Convert.ToBase64String(QrCodeImage.Png(sheet.Url));
        var page = new StringBuilder();
        page.Append(CultureInfo.InvariantCulture, $"""
            <!doctype html>
            <html lang="{Encode(sheet.Form.Language)}">
            <head>
            <meta charset="utf-8">
            <meta http-equiv="Content-Security-Policy" content="default-src 'none'; img-src data:; style-src 'unsafe-inline'; script-src 'unsafe-inline'">
            <title>{Encode(text.Title)}</title>
            <style>{Style}</style>
            </head>
            <body>
            <header>
            <div>
            <h1>{Encode(text.Title)}</h1>
            <p>{Encode(text.For)}: <strong>{Encode(sheet.CustomerName)}</strong></p>
            {AppointmentLine(sheet, text)}
            </div>
            <figure><img src="data:image/png;base64,{qr}" alt="{Encode(text.Qr)}"><figcaption>{Encode(text.Qr)}</figcaption></figure>
            </header>
            <p>{Encode(sheet.Form.Intro)}</p>
            <ol>
            """);
        foreach (var question in sheet.Form.Questions)
        {
            page.Append(CultureInfo.InvariantCulture, $"<li data-key=\"{Encode(question.Key)}\"><p>{Encode(question.Label)}</p>");
            page.Append(question.Kind == AnamnesisQuestionKind.YesNo
                ? $"<p class=\"boxes\"><span>☐ {Encode(text.Yes)}</span><span>☐ {Encode(text.No)}</span></p>{(question.DetailsLabel is { } details ? $"<p class=\"line\">{Encode(details)}:</p>" : "")}"
                : "<p class=\"line\"></p><p class=\"line\"></p>");
            page.Append("</li>");
        }

        page.Append(CultureInfo.InvariantCulture, $"""
            </ol>
            <p>☐ {Encode(sheet.Form.ConsentText)}</p>
            <p class="signature">{Encode(text.Signature)}</p>
            <footer>{Encode(text.Form)} {Encode(sheet.Form.Version)}</footer>
            <script>window.print()</script>
            </body>
            </html>
            """);
        return page.ToString();
    }

    private static string AppointmentLine(AnamnesisPaperSheet sheet, SheetText text)
    {
        if (sheet.Start is not { } start || sheet.TimeZone is not { } zone)
        {
            return "";
        }

        var local = TimeZoneInfo.ConvertTime(start, TimeZoneInfo.FindSystemTimeZoneById(zone));
        var when = local.ToString("f", CultureInfo.GetCultureInfo(text.Culture));
        var place = sheet.LocationName is { } location ? $", {location}" : "";
        return $"<p>{Encode(text.Appointment)}: {Encode($"{sheet.TreatmentName}, {when}{place}")}</p>";
    }

    private static string Encode(string value) => Encoder.Encode(value);
}
