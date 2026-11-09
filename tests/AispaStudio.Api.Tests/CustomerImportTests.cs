using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using AispaStudio.Contracts;
using ClosedXML.Excel;

namespace AispaStudio.Api.Tests;

/// <summary>
/// Kunden-Import aus Excel und CSV (Export/Import, Aufgabe E): Vorschläge der Zuordnung nach Spaltenköpfen (auch Shore), Datumsformate, fehlerhafte Zeilen mit Grund,
/// Dubletten im Bestand und in der Datei, „ergänzen“ füllt nur Leeres, Probelauf ändert nichts, Protokoll und Kanal „Import“ im Verlauf, Recht der Leitung und Grenzen.
/// </summary>
public sealed class CustomerImportTests(ApiFixture fixture)
{
    private const string Base = "/api/customers/import";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static string Unique() => Guid.NewGuid().ToString("N")[..8];

    private static byte[] Csv(params string[] lines) => [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(string.Join("\r\n", lines) + "\r\n")];

    private static byte[] Excel(string[] headers, params object?[][] rows)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Kunden");
        for (var c = 0; c < headers.Length; c++)
        {
            sheet.Cell(1, c + 1).Value = headers[c];
        }

        for (var r = 0; r < rows.Length; r++)
        {
            for (var c = 0; c < rows[r].Length; c++)
            {
                sheet.Cell(r + 2, c + 1).Value = rows[r][c] switch
                {
                    DateTime date => date,
                    null => Blank.Value,
                    var other => other.ToString(),
                };
            }
        }

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static MultipartFormDataContent Form(byte[] file, string fileName, CustomerImportMapping? mapping = null, List<ImportDuplicateDecision>? decisions = null,
        bool? dryRun = null)
    {
        var content = new ByteArrayContent(file);
        content.Headers.ContentType = new MediaTypeHeaderValue(fileName.EndsWith(".xlsx", StringComparison.Ordinal)
            ? "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
            : "text/csv");
        var form = new MultipartFormDataContent { { content, "file", fileName } };
        if (mapping is not null)
        {
            form.Add(new StringContent(JsonSerializer.Serialize(mapping, TestSpa.Json)), "mapping");
        }

        if (decisions is not null)
        {
            form.Add(new StringContent(JsonSerializer.Serialize(decisions, TestSpa.Json)), "decisions");
        }

        if (dryRun is { } dry)
        {
            form.Add(new StringContent(dry ? "true" : "false"), "dryRun");
        }

        return form;
    }

    private static async Task<T> PostAsync<T>(HttpClient client, string path, MultipartFormDataContent form)
    {
        using var response = await client.PostAsync(path, form, Ct);
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync(Ct)}");
        return (await response.Content.ReadFromJsonAsync<T>(TestSpa.Json, Ct))!;
    }

    private static Task<List<CustomerDto>> FindAsync(TestSpa spa, string lastName) => spa.GetAsync<List<CustomerDto>>($"/api/customers?search={lastName}", Ct);

    private static CustomerImportMapping Map(params CustomerImportField[] fields) =>
        new([.. fields.Select((field, column) => new ImportColumnMapping(column, field))]);

    [Fact]
    public async Task Vorschau_schlaegt_Felder_nach_Spaltenkoepfen_vor_auch_fuer_einen_Shore_Export()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var german = Csv("Vorname;Nachname;E-Mail-Adresse;Handy;Geburtstag;Notiz", "Anna;Huber;anna@example.at;0664 1234567;14.03.1985;Allergie");
        var shore = Csv("first_name,last_name,gender,email,mobile,birthday,adress_street,adress_postal_code,adress_city,tags",
            "Max,\"Maier, jun.\",male,max@example.at,+43 660 7654321,31.12.1988,Hauptstraße 1,1010,Wien,VIP");

        var first = await PostAsync<CustomerImportPreview>(spa.Client, $"{Base}/preview", Form(german, "kunden.csv"));
        var second = await PostAsync<CustomerImportPreview>(spa.Client, $"{Base}/preview", Form(shore, "shore.csv"));

        Assert.Equal([CustomerImportField.FirstName, CustomerImportField.LastName, CustomerImportField.Email, CustomerImportField.Phone, CustomerImportField.BirthDate, null],
            first.Columns.Select(c => c.Suggested));
        Assert.Equal(["Huber"], first.Columns[1].Samples);
        Assert.Equal(ImportDateFormat.DayMonthYear, first.Mapping.DateFormat);
        Assert.DoesNotContain(first.Mapping.Columns, c => c.Column == 5);
        Assert.Equal((1, 1, 0, 0), (first.RowCount, first.New, first.Duplicates, first.Invalid));

        Assert.Equal([CustomerImportField.FirstName, CustomerImportField.LastName, CustomerImportField.Salutation, CustomerImportField.Email, CustomerImportField.Phone,
                CustomerImportField.BirthDate, CustomerImportField.Street, CustomerImportField.PostalCode, CustomerImportField.City, null],
            second.Columns.Select(c => c.Suggested));
        var row = Assert.Single(second.Rows);
        Assert.Equal((2, ImportRowStatus.New, "Max Maier, jun.", "max@example.at"), (row.Row, row.Status, row.Name, row.Email));
    }

    [Fact]
    public async Task Datumsformate_und_fehlerhafte_Zeilen_mit_Grund_auch_als_Excel()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var name = $"Datum{Unique()}";
        var file = Excel(["Nachname", "Vorname", "Geburtstag", "Telefon"],
            [name, "Anna", "14.03.1985", ""],
            [name, "Berta", "1985-03-14", ""],
            [name, "Clara", new DateTime(1985, 3, 14), "0664 1234567"],
            [name, "Dora", "31.02.1990", ""],
            [name, "Erna", "", "abc"],
            ["", "Frieda", "", ""]);
        var mapping = Map(CustomerImportField.LastName, CustomerImportField.FirstName, CustomerImportField.BirthDate, CustomerImportField.Phone);

        var preview = await PostAsync<CustomerImportPreview>(spa.Client, $"{Base}/preview", Form(file, "kunden.xlsx", mapping));
        using var errors = await spa.Client.PostAsync($"{Base}/errors", Form(file, "kunden.xlsx", mapping), Ct);
        var sheet = await ExportResponses.SheetAsync(errors, Ct);
        var result = await PostAsync<CustomerImportResult>(spa.Client, Base, Form(file, "kunden.xlsx", mapping));
        var imported = await FindAsync(spa, name);

        Assert.Equal((6, 3, 0, 3), (preview.RowCount, preview.New, preview.Duplicates, preview.Invalid));
        Assert.Equal([(5, "Geburtstag kein Datum"), (6, "Telefon unleserlich"), (7, "Nachname fehlt")], preview.InvalidRows.Select(r => (r.Row, r.Reason)));
        Assert.Equal(["Zeile", "Grund", "Nachname", "Vorname", "Geburtstag", "Telefon"], ExportResponses.Headers(sheet));
        Assert.Equal(4, sheet.LastRowUsed()!.RowNumber());
        Assert.Equal((5d, "Geburtstag kein Datum", "Dora"), (sheet.Row(2).Cell(1).GetDouble(), sheet.Row(2).Cell(2).GetString(), sheet.Row(2).Cell(4).GetString()));
        Assert.Equal((false, 6, 3, 0, 0, 3), (result.DryRun, result.Rows, result.Created, result.Merged, result.Skipped, result.Failed));
        Assert.Equal(3, imported.Count);
        Assert.All(imported, c => Assert.Equal(new DateOnly(1985, 3, 14), c.BirthDate));
        Assert.Equal("+43 664 1234567", imported.Single(c => c.FirstName == "Clara").Phone);
    }

    [Fact]
    public async Task Csv_aus_Excel_ohne_UTF8_behaelt_die_Umlaute()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var ansi = Encoding.Latin1.GetBytes("Nachname;Vorname;Straße\r\nMüller;Jürgen;Hauptstraße 1\r\n");

        var preview = await PostAsync<CustomerImportPreview>(spa.Client, $"{Base}/preview", Form(ansi, "kunden.csv"));

        Assert.Equal(["Nachname", "Vorname", "Straße"], preview.Columns.Select(c => c.Header));
        Assert.Equal(CustomerImportField.Street, preview.Columns[2].Suggested);
        Assert.Equal("Jürgen Müller", Assert.Single(preview.Rows).Name);
    }

    [Fact]
    public async Task Datum_Jahr_zuerst_wird_erkannt_und_Monat_zuerst_ist_waehlbar()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var iso = Csv("Nachname;Geburtstag", "Huber;1990-07-04");
        var american = Csv("Nachname;Geburtstag", "Huber;07/04/1990");
        var mapping = Map(CustomerImportField.LastName, CustomerImportField.BirthDate);

        var detected = await PostAsync<CustomerImportPreview>(spa.Client, $"{Base}/preview", Form(iso, "iso.csv", mapping));
        var chosen = await PostAsync<CustomerImportPreview>(spa.Client, $"{Base}/preview", Form(american, "us.csv", mapping with { DateFormat = ImportDateFormat.MonthDayYear }));
        var european = await PostAsync<CustomerImportPreview>(spa.Client, $"{Base}/preview", Form(american, "us.csv", mapping));

        Assert.Equal((ImportDateFormat.YearMonthDay, 1), (detected.Mapping.DateFormat, detected.New));
        Assert.Equal((ImportDateFormat.MonthDayYear, 1), (chosen.Mapping.DateFormat, chosen.New));
        Assert.Equal(ImportDateFormat.DayMonthYear, european.Mapping.DateFormat);
    }

    [Fact]
    public async Task Dubletten_im_Bestand_und_in_der_Datei_ergaenzen_fuellt_nur_leere_Felder()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var s = Unique();
        var huber = await spa.PostAsync<CustomerDto>("/api/customers",
            new CustomerInput("Anna", $"Huber{s}", null, "0664 1234567", null, null, Address: new CustomerAddress(null, null, "Wien", null)), Ct);
        var file = Csv("Vorname;Nachname;E-Mail;Telefon;Geburtstag;Ort",
            $"Annemarie;Huber{s};anna{s}@example.at;+43 664 1234567;01.02.1980;Graz",
            $"Berta;Maier{s};berta{s}@example.at;;;",
            $"Berta;MAIER{s};BERTA{s}@example.at;;05.06.1975;",
            $"Clara;Berger{s};;0660 1111111;;",
            $"Clara;Berger{s};;+436601111111;;Linz");
        var mapping = Map(CustomerImportField.FirstName, CustomerImportField.LastName, CustomerImportField.Email, CustomerImportField.Phone, CustomerImportField.BirthDate,
            CustomerImportField.City);

        var preview = await PostAsync<CustomerImportPreview>(spa.Client, $"{Base}/preview", Form(file, "kunden.csv", mapping));
        var result = await PostAsync<CustomerImportResult>(spa.Client, Base, Form(file, "kunden.csv", mapping,
            [new ImportDuplicateDecision(2, ImportDuplicateAction.Merge), new ImportDuplicateDecision(4, ImportDuplicateAction.Merge)]));

        Assert.Equal((2, 3, 0), (preview.New, preview.Duplicates, preview.Invalid));
        Assert.Equal([
                (2, $"gleicher Nachname und gleiche Telefonnummer wie Anna Huber{s}", (Guid?)huber.Id),
                (4, "doppelt in der Datei (Zeile 3)", null),
                (6, "doppelt in der Datei (Zeile 5)", null),
            ],
            preview.DuplicateRows.Select(r => (r.Row, r.Reason, r.ExistingCustomerId)));
        Assert.Equal((2, 2, 1, 0), (result.Created, result.Merged, result.Skipped, result.Failed));

        var merged = Assert.Single(await FindAsync(spa, $"Huber{s}"));
        Assert.Equal(("Anna", $"anna{s}@example.at", "+43 664 1234567", new DateOnly(1980, 2, 1), "Wien"),
            (merged.FirstName, merged.Email, merged.Phone, merged.BirthDate, merged.Address?.City));
        var maier = Assert.Single(await FindAsync(spa, $"Maier{s}"));
        Assert.Equal(($"berta{s}@example.at", new DateOnly(1975, 6, 5)), (maier.Email, maier.BirthDate));
        var berger = Assert.Single(await FindAsync(spa, $"Berger{s}"));
        Assert.Null(berger.Address?.City);
    }

    [Fact]
    public async Task Probelauf_aendert_nichts_der_Import_steht_im_Verlauf_mit_Kanal_Import()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var s = Unique();
        var file = Csv("Nachname;Vorname;E-Mail;Newsletter", $"Gruber{s};Gerda;gerda{s}@example.at;ja", $"Gruber{s};Hans;hans{s}@example.at;nein");
        var mapping = Map(CustomerImportField.LastName, CustomerImportField.FirstName, CustomerImportField.Email, CustomerImportField.MarketingConsent);

        var dry = await PostAsync<CustomerImportResult>(spa.Client, Base, Form(file, "kunden.csv", mapping, dryRun: true));
        var afterDry = await FindAsync(spa, $"Gruber{s}");
        var historyAfterDry = await spa.GetAsync<List<HistoryEntry>>("/api/exports/history", Ct);
        var real = await PostAsync<CustomerImportResult>(spa.Client, Base, Form(file, "kunden.csv", mapping, dryRun: false));
        var guests = await FindAsync(spa, $"Gruber{s}");
        var gerda = guests.Single(c => c.FirstName == "Gerda");
        var hans = guests.Single(c => c.FirstName == "Hans");

        Assert.Equal((true, 2, 0), (dry.DryRun, dry.Created, afterDry.Count));
        Assert.Empty(historyAfterDry);
        Assert.Equal((false, 2), (real.DryRun, real.Created));
        Assert.Equal("Import Kunden: 2 neu, 0 ergänzt, 0 übersprungen (kunden.csv, 2 Zeilen) von Test Person",
            Assert.Single(await spa.GetAsync<List<HistoryEntry>>("/api/exports/history", Ct)).Text);
        Assert.Contains(await spa.GetAsync<List<HistoryEntry>>($"/api/customers/{gerda.Id}/history", Ct), h => h.Text == "Angelegt beim Import von Test Person");
        var yes = await spa.GetAsync<MarketingConsentDto>($"/api/customers/{gerda.Id}/marketing-consent", Ct);
        var no = await spa.GetAsync<MarketingConsentDto>($"/api/customers/{hans.Id}/marketing-consent", Ct);
        Assert.Equal((MarketingConsentState.Confirmed, "Import", $"gerda{s}@example.at"), (yes.State, yes.Source, yes.Email));
        Assert.NotEqual(MarketingConsentState.Confirmed, no.State);
    }

    [Fact]
    public async Task Nur_die_Leitung_mit_Nachname_in_der_Zuordnung_und_innerhalb_der_Grenzen()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        using var manager = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Manager, [spa.Location.Id]);
        using var reception = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Reception, [spa.Location.Id]);
        var file = Csv("Nachname;Vorname", "Huber;Anna");

        using var forManager = await manager.PostAsync($"{Base}/preview", Form(file, "kunden.csv"), Ct);
        using var forReception = await reception.PostAsync(Base, Form(file, "kunden.csv"), Ct);
        using var noLastName = await spa.Client.PostAsync($"{Base}/preview", Form(file, "kunden.csv", new CustomerImportMapping([new ImportColumnMapping(1, CustomerImportField.FirstName)])), Ct);
        using var unknownColumn = await spa.Client.PostAsync($"{Base}/preview", Form(file, "kunden.csv", Map(CustomerImportField.LastName, CustomerImportField.FirstName, CustomerImportField.Email)), Ct);
        using var pdf = await spa.Client.PostAsync($"{Base}/preview", Form(file, "kunden.pdf"), Ct);
        using var tooMany = await spa.Client.PostAsync($"{Base}/preview",
            Form(Csv([.. Enumerable.Range(0, 20_002).Select(i => i == 0 ? "Nachname" : $"Gast{i}")]), "gross.csv"), Ct);

        Assert.Equal((HttpStatusCode.Forbidden, HttpStatusCode.Forbidden), (forManager.StatusCode, forReception.StatusCode));
        Assert.Contains("nur die Leitung", await forManager.Content.ReadAsStringAsync(Ct), StringComparison.Ordinal);
        Assert.Equal([HttpStatusCode.BadRequest, HttpStatusCode.BadRequest, HttpStatusCode.BadRequest, HttpStatusCode.BadRequest],
            new[] { noLastName.StatusCode, unknownColumn.StatusCode, pdf.StatusCode, tooMany.StatusCode });
        Assert.Contains("Nachnamen", await noLastName.Content.ReadAsStringAsync(Ct), StringComparison.Ordinal);
        Assert.Contains("20 000", await tooMany.Content.ReadAsStringAsync(Ct), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("kunden-import-beispiel.xlsx")]
    [InlineData("kunden-import-beispiel.csv")]
    public async Task Beispieldateien_der_Hilfe_ergeben_vier_neue_eine_Dublette_und_zwei_Fehler(string file)
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AispaStudio.slnx")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        var bytes = await File.ReadAllBytesAsync(Path.Combine(directory.FullName, "docs", "beispiele", file), Ct);

        var preview = await PostAsync<CustomerImportPreview>(spa.Client, $"{Base}/preview", Form(bytes, file));

        Assert.Equal(9, preview.Mapping.Columns.Count);
        Assert.Equal((7, 4, 1, 2), (preview.RowCount, preview.New, preview.Duplicates, preview.Invalid));
        Assert.Equal("doppelt in der Datei (Zeile 2)", Assert.Single(preview.DuplicateRows).Reason);
        Assert.Equal(["Geburtstag kein Datum", "Nachname fehlt"], preview.InvalidRows.Select(r => r.Reason));
    }

    [Fact]
    public async Task Mehr_als_ein_Block_und_nur_der_eigene_Mandant_als_Dublette()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var other = await TestSpa.CreateAsync(fixture, Ct);
        var s = Unique();
        var many = Csv([.. Enumerable.Range(0, 451).Select(i => i == 0 ? "Nachname;Vorname" : $"Block{s};Gast {i}")]);
        var foreign = Csv("Nachname;E-Mail", $"{spa.Customer.LastName};{spa.Customer.Email}");

        var result = await PostAsync<CustomerImportResult>(spa.Client, Base, Form(many, "viele.csv"));
        var otherPreview = await PostAsync<CustomerImportPreview>(other.Client, $"{Base}/preview", Form(foreign, "kunden.csv"));
        var ownPreview = await PostAsync<CustomerImportPreview>(spa.Client, $"{Base}/preview", Form(foreign, "kunden.csv"));

        Assert.Equal((450, 450), (result.Rows, result.Created));
        Assert.Equal((1, 0), (otherPreview.New, otherPreview.Duplicates));
        Assert.Equal((0, 1, (Guid?)spa.Customer.Id), (ownPreview.New, ownPreview.Duplicates, ownPreview.DuplicateRows[0].ExistingCustomerId));
    }
}
