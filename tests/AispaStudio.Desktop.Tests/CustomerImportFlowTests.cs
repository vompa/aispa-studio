using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using AispaStudio.Contracts;
using AispaStudio.Desktop.ViewModels;

namespace AispaStudio.Desktop.Tests;

/// <summary>
/// Kunden-Import in der App (Export/Import, Aufgabe E): „…“ › „Kunden importieren …“ nur für die Leitung, vier Schritte (Datei, Zuordnung, Vorschau, Probelauf und Import) gegen die
/// Endpunkte unter <c>/api/customers/import</c>; Zuordnung, Dubletten und Zähler rechnet die API, ihre Tests prüfen das.
/// </summary>
public sealed class CustomerImportFlowTests
{
    private const string PreviewRoute = "POST /api/customers/import/preview";
    private const string ImportRoute = "POST /api/customers/import";
    private const string ErrorsRoute = "POST /api/customers/import/errors";
    private const string ListRoute = "GET /api/customers?search=";

    private static readonly string[] PickerPatterns = ["*.xlsx", "*.csv"];

    private static readonly byte[] Csv = "Nachname;Vorname;Geburtstag;Notiz\nHuber;Anna;03.05.1980;x\n"u8.ToArray();

    internal static CustomerImportPreview Preview() => new(
        "kunden.csv",
        4,
        [
            new ImportColumn(0, "Nachname", ["Huber", "Berger"], CustomerImportField.LastName),
            new ImportColumn(1, "Vorname", ["Anna", "Max"], CustomerImportField.FirstName),
            new ImportColumn(2, "Geburtstag", ["03.05.1980"], CustomerImportField.BirthDate),
            new ImportColumn(3, "Notiz", [], null),
        ],
        new CustomerImportMapping(
            [new ImportColumnMapping(0, CustomerImportField.LastName), new ImportColumnMapping(1, CustomerImportField.FirstName), new ImportColumnMapping(2, CustomerImportField.BirthDate)],
            ImportDateFormat.DayMonthYear),
        2,
        1,
        1,
        [
            new ImportRowPreview(2, ImportRowStatus.New, "Berta Muster", "berta@example.test", null, null, null),
            new ImportRowPreview(3, ImportRowStatus.Duplicate, "Anna Huber", "anna@example.test", null, "gleicher Nachname und gleiche E-Mail wie Anna Huber", Guid.NewGuid()),
        ],
        [new ImportRowPreview(3, ImportRowStatus.Duplicate, "Anna Huber", "anna@example.test", null, "gleicher Nachname und gleiche E-Mail wie Anna Huber", Guid.NewGuid())],
        [new ImportRowPreview(5, ImportRowStatus.Invalid, "Max Berger", null, "12x", "Geburtstag kein Datum", null)]);

    private static StubApi Api() => new StubApi()
        .On(ListRoute, new List<CustomerDto>())
        .On(PreviewRoute, Preview())
        .On(ImportRoute, new CustomerImportResult(false, 4, 2, 1, 0, 1));

    private static FakeFilePicker Picker(string name = "kunden.csv", byte[]? content = null) => new() { Picked = new PickedBytes(name, (content ?? Csv).Length, content ?? Csv) };

    private static async Task<CustomersViewModel> Customers(StubApi api, CurrentUser user, FakeDialogs dialogs, IFilePicker? picker = null, IFileSaver? saver = null)
    {
        var customers = new CustomersViewModel(api.Client(), new ManualTime(new DateTimeOffset(2026, 11, 9, 10, 0, 0, TimeSpan.FromHours(1))))
        {
            User = user,
            Dialogs = dialogs,
            FilePicker = picker ?? Picker(),
        };
        customers.Export.Files = saver;
        await customers.InitializeAsync();
        return customers;
    }

    /// <summary>Der Wert eines Formularfelds der gesendeten <c>multipart/form-data</c>-Anfrage, oder null, wenn es fehlt.</summary>
    private static string? Field(string? body, string name)
    {
        var match = Regex.Match(body ?? "", $"name=\"?{name}\"?(?:; filename=[^\\r\\n]*)?\\r?\\n\\r?\\n(.*?)\\r?\\n--", RegexOptions.Singleline);
        return match.Success ? match.Groups[1].Value : null;
    }

    private static string? BodyOf(StubApi api, string route, int index = 0) => api.Calls.Where(c => c.Route == route).Skip(index).Select(c => c.Body).FirstOrDefault();

    private static CustomerImportMapping MappingOf(string? body) => JsonSerializer.Deserialize<CustomerImportMapping>(Field(body, "mapping")!, ApiJson.Options)!;

    private static async Task<(CustomersViewModel Customers, CustomerImportViewModel Dialog, Task Running)> OpenAsync(StubApi api, FakeDialogs dialogs, IFileSaver? saver = null, IFilePicker? picker = null)
    {
        var customers = await Customers(api, TestData.Owner(), dialogs, picker, saver);
        var running = customers.ImportCustomersCommand.ExecuteAsync(null);
        var dialog = Assert.IsType<CustomerImportViewModel>(dialogs.Open);
        return (customers, dialog, running);
    }

    [Fact]
    public async Task Der_Eintrag_steht_nur_der_Leitung_zur_Verfuegung()
    {
        var owner = await Customers(Api(), TestData.Owner(), new FakeDialogs());
        var manager = await Customers(Api(), TestData.Owner(StaffRole.GeneralManager), new FakeDialogs());
        var reception = await Customers(Api(), TestData.Reception(), new FakeDialogs());

        Assert.True(owner.CanImport);
        Assert.True(owner.ImportCustomersCommand.CanExecute(null));
        Assert.True(manager.CanImport);
        Assert.False(reception.CanImport);
        Assert.False(reception.ImportCustomersCommand.CanExecute(null));
    }

    [Fact]
    public async Task Vier_Schritte_von_der_Datei_bis_zum_Import_melden_das_Ergebnis_und_laden_die_Liste_neu()
    {
        var api = Api();
        var dialogs = new FakeDialogs();
        var (customers, dialog, running) = await OpenAsync(api, dialogs);

        // 1. Datei wählen
        FakeDialogs.AssertRules(dialog.Request);
        Assert.False(dialog.Request.IsDestructive);
        Assert.Equal(CustomerImportStep.File, dialog.Step);
        Assert.Equal("Datei wählen …", dialog.ContinueText);
        await dialog.ContinueCommand.ExecuteAsync(null);
        Assert.Equal("kunden.csv", dialog.FileName);
        Assert.Null(Field(BodyOf(api, PreviewRoute), "mapping"));
        Assert.NotNull(Field(BodyOf(api, PreviewRoute), "file"));

        // 2. Spalten zuordnen: die Vorschläge der API sind übernommen
        Assert.Equal(CustomerImportStep.Mapping, dialog.Step);
        Assert.Equal(["Nachname", "Vorname", "Geburtstag", "Notiz"], dialog.Columns.Select(c => c.Header));
        Assert.Equal(["Nachname", "Vorname", "Geburtstag", "nicht importieren"], dialog.Columns.Select(c => c.Choice.Label));
        Assert.Equal("Huber · Berger", dialog.Columns[0].SamplesText);
        Assert.Equal("TT.MM.JJJJ", dialog.SelectedDateFormat.Label);
        Assert.Null(dialog.MappingError);
        await dialog.ContinueCommand.ExecuteAsync(null);

        // 3. Vorschau und Prüfung mit der gewählten Zuordnung
        Assert.Equal(CustomerImportStep.Review, dialog.Step);
        var mapping = MappingOf(BodyOf(api, PreviewRoute, 1));
        Assert.Equal([(0, CustomerImportField.LastName), (1, CustomerImportField.FirstName), (2, CustomerImportField.BirthDate)], mapping.Columns.Select(c => (c.Column, c.Field)));
        Assert.Equal(ImportDateFormat.DayMonthYear, mapping.DateFormat);
        Assert.Equal("2 neu, 1 Dublette, 1 fehlerhaft", dialog.Summary);
        Assert.Equal(2, dialog.SampleRows.Count);
        Assert.Equal("Zeile 3: Anna Huber", Assert.Single(dialog.DuplicateRows).Title);
        Assert.Equal("Geburtstag kein Datum", Assert.Single(dialog.InvalidRows).Reason);
        Assert.True(dialog.DuplicateRows[0].IsSkip);
        dialog.DuplicateRows[0].IsMerge = true;
        await dialog.ContinueCommand.ExecuteAsync(null);

        // 4. Probelauf ändert nichts, Import meldet das Ergebnis
        Assert.Equal(CustomerImportStep.Run, dialog.Step);
        Assert.Equal("Importieren", dialog.ContinueText);
        await dialog.CheckCommand.ExecuteAsync(null);
        Assert.Equal("true", Field(BodyOf(api, ImportRoute), "dryRun"));
        Assert.Contains("Probelauf: ", dialog.DryRunText, StringComparison.Ordinal);
        Assert.Null(customers.Toast);
        Assert.Same(dialog, dialogs.Open);

        await dialog.ContinueCommand.ExecuteAsync(null);
        await running;
        Assert.Equal("false", Field(BodyOf(api, ImportRoute, 1), "dryRun"));
        Assert.Null(dialogs.Open);
        Assert.Equal("Importiert: 2 neu, 1 ergänzt, 0 übersprungen, 1 fehlerhaft", customers.Toast);
        Assert.Equal(2, api.Calls.Count(c => c.Route == ListRoute));
    }

    [Fact]
    public async Task Die_Pflicht_Nachname_sperrt_Weiter_und_ein_Feld_gehoert_nur_einer_Spalte()
    {
        var api = Api();
        var (_, dialog, _) = await OpenAsync(api, new FakeDialogs());
        await dialog.ContinueCommand.ExecuteAsync(null);

        dialog.Columns[0].Choice = ImportFieldChoice.For(null);
        Assert.Equal(CustomerImportViewModel.LastNameMissing, dialog.MappingError);
        Assert.False(dialog.ContinueCommand.CanExecute(null));

        // Der Nachname geht an die Spalte „Vorname“; die Spalte, die ihn hatte, steht danach auf „nicht importieren“ (und umgekehrt der Vorname).
        dialog.Columns[1].Choice = ImportFieldChoice.For(CustomerImportField.LastName);
        Assert.Null(dialog.MappingError);
        Assert.True(dialog.ContinueCommand.CanExecute(null));
        Assert.Equal(["nicht importieren", "Nachname", "Geburtstag", "nicht importieren"], dialog.Columns.Select(c => c.Choice.Label));

        dialog.Columns[2].Choice = ImportFieldChoice.For(CustomerImportField.LastName);
        Assert.Equal(["nicht importieren", "nicht importieren", "Nachname", "nicht importieren"], dialog.Columns.Select(c => c.Choice.Label));
        Assert.Single(api.Calls, c => c.Route == PreviewRoute);
    }

    [Fact]
    public async Task Das_gewaehlte_Datumsformat_geht_mit_der_Zuordnung_an_die_API()
    {
        var api = Api();
        var (_, dialog, _) = await OpenAsync(api, new FakeDialogs());
        await dialog.ContinueCommand.ExecuteAsync(null);

        dialog.SelectedDateFormat = ImportDateFormatChoice.For(ImportDateFormat.MonthDayYear);
        await dialog.ContinueCommand.ExecuteAsync(null);

        Assert.Equal(ImportDateFormat.MonthDayYear, MappingOf(BodyOf(api, PreviewRoute, 1)).DateFormat);
        Assert.Equal(["Auto", "DayMonthYear", "YearMonthDay", "MonthDayYear"], dialog.DateFormats.Select(f => f.Format.ToString()));
    }

    [Fact]
    public async Task Die_Entscheidungen_zu_den_Dubletten_landen_im_Formular_und_der_Probelauf_sendet_dryRun_true()
    {
        var api = Api();
        var (_, dialog, _) = await OpenAsync(api, new FakeDialogs());
        await dialog.ContinueCommand.ExecuteAsync(null);
        await dialog.ContinueCommand.ExecuteAsync(null);

        // Vorgabe „überspringen“, dann „ergänzen“
        await dialog.ContinueCommand.ExecuteAsync(null);
        await dialog.CheckCommand.ExecuteAsync(null);
        dialog.BackCommand.Execute(null);
        Assert.Null(dialog.DryRunText);
        dialog.DuplicateRows[0].IsMerge = true;
        await dialog.ContinueCommand.ExecuteAsync(null);
        await dialog.CheckCommand.ExecuteAsync(null);

        Assert.Equal("true", Field(BodyOf(api, ImportRoute), "dryRun"));
        Assert.Equal("true", Field(BodyOf(api, ImportRoute, 1), "dryRun"));
        var first = JsonSerializer.Deserialize<List<ImportDuplicateDecision>>(Field(BodyOf(api, ImportRoute), "decisions")!, ApiJson.Options)!;
        var second = JsonSerializer.Deserialize<List<ImportDuplicateDecision>>(Field(BodyOf(api, ImportRoute, 1), "decisions")!, ApiJson.Options)!;
        Assert.Equal([new ImportDuplicateDecision(3, ImportDuplicateAction.Skip)], first);
        Assert.Equal([new ImportDuplicateDecision(3, ImportDuplicateAction.Merge)], second);
        Assert.NotNull(Field(BodyOf(api, ImportRoute), "mapping"));
    }

    [Fact]
    public async Task Abbrechen_ruft_nichts_auf_ausser_den_Vorschauen_und_importiert_nie()
    {
        var api = Api();
        var dialogs = new FakeDialogs();
        var (customers, dialog, running) = await OpenAsync(api, dialogs);

        // vor der Datei: gar kein Aufruf
        dialog.CancelCommand.Execute(null);
        await running;
        Assert.DoesNotContain(api.Calls, c => c.Route.StartsWith("POST", StringComparison.Ordinal));

        // nach der Zuordnung: nur die Vorschau war da, nie ein Import
        var again = customers.ImportCustomersCommand.ExecuteAsync(null);
        var second = Assert.IsType<CustomerImportViewModel>(dialogs.Open);
        await second.ContinueCommand.ExecuteAsync(null);
        await second.ContinueCommand.ExecuteAsync(null);
        Assert.Equal(CustomerImportStep.Review, second.Step);
        second.CancelCommand.Execute(null);
        await again;

        Assert.DoesNotContain(api.Calls, c => c.Route == ImportRoute);
        Assert.Null(customers.Toast);
        Assert.Equal(1, api.Calls.Count(c => c.Route == ListRoute));
    }

    [Fact]
    public async Task Fehlerzeilen_als_Excel_gehen_mit_Zuordnung_an_die_API_und_in_den_Speicherdialog()
    {
        var api = Api().OnFile(ErrorsRoute, [9, 9], "Importfehler_2026-11-09.xlsx");
        var files = new FakeFileSaver();
        var (_, dialog, _) = await OpenAsync(api, new FakeDialogs(), files);
        await dialog.ContinueCommand.ExecuteAsync(null);
        await dialog.ContinueCommand.ExecuteAsync(null);

        Assert.True(dialog.HasInvalid);
        await dialog.SaveErrorsCommand.ExecuteAsync(null);

        Assert.NotNull(MappingOf(BodyOf(api, ErrorsRoute)));
        var (name, content) = Assert.Single(files.Saved);
        Assert.Equal("Importfehler_2026-11-09.xlsx", name);
        Assert.Equal<byte>([9, 9], content);
        Assert.Equal("Gespeichert: Importfehler_2026-11-09.xlsx", dialog.SavedText);
        Assert.Null(dialog.Error);
    }

    [Fact]
    public async Task Ohne_Speicherdialog_steht_ein_Hinweis_im_Dialog()
    {
        var api = Api().OnFile(ErrorsRoute, [9], "Importfehler.xlsx");
        var (_, dialog, _) = await OpenAsync(api, new FakeDialogs(), saver: null);
        await dialog.ContinueCommand.ExecuteAsync(null);
        await dialog.ContinueCommand.ExecuteAsync(null);

        await dialog.SaveErrorsCommand.ExecuteAsync(null);

        Assert.Equal(CustomerImportViewModel.NotSaved, dialog.Error);
        Assert.Null(dialog.SavedText);
    }

    [Fact]
    public async Task Eine_falsche_oder_zu_grosse_Datei_steht_als_Fehler_am_Feld_und_ruft_nichts_auf()
    {
        var api = Api();
        var picker = Picker("kunden.pdf");
        var (_, dialog, _) = await OpenAsync(api, new FakeDialogs(), picker: picker);

        await dialog.ContinueCommand.ExecuteAsync(null);
        Assert.Equal(CustomerImportViewModel.UnsupportedFile, dialog.FileError);

        picker.Picked = new PickedBytes("gross.xlsx", CustomerImportViewModel.MaxBytes + 1, []);
        await dialog.ContinueCommand.ExecuteAsync(null);
        Assert.Equal(CustomerImportViewModel.TooBig, dialog.FileError);

        picker.Picked = new PickedBytes("leer.csv", 0, []);
        await dialog.ContinueCommand.ExecuteAsync(null);
        Assert.Equal(CustomerImportViewModel.EmptyFile, dialog.FileError);

        picker.Picked = null;
        await dialog.ContinueCommand.ExecuteAsync(null);
        Assert.Equal(CustomerImportStep.File, dialog.Step);
        Assert.DoesNotContain(api.Calls, c => c.Route == PreviewRoute);
        Assert.Equal(CustomerImportViewModel.MaxBytes, picker.Asked.Last().MaxBytes);
        Assert.Equal(PickerPatterns, picker.Asked.Last().Patterns);
    }

    [Fact]
    public async Task Ein_Fehler_der_API_bei_der_Vorschau_steht_am_Feld_Datei()
    {
        var api = Api().On(PreviewRoute, new { title = "Ungültig", errors = new { file = new List<string> { "Die Datei lässt sich nicht lesen. In der ersten Zeile müssen die Spaltenköpfe stehen." } } }, HttpStatusCode.BadRequest);
        var (_, dialog, _) = await OpenAsync(api, new FakeDialogs());

        await dialog.ContinueCommand.ExecuteAsync(null);

        Assert.Equal("Die Datei lässt sich nicht lesen. In der ersten Zeile müssen die Spaltenköpfe stehen.", dialog.FileError);
        Assert.Equal(CustomerImportStep.File, dialog.Step);
        Assert.False(dialog.IsBusy);
    }

    [Fact]
    public async Task Ein_Fehler_beim_Import_laesst_den_Dialog_offen_und_zeigt_den_Fehler()
    {
        var api = Api().On(ImportRoute, new { title = "Kunden importiert nur die Leitung." }, HttpStatusCode.Forbidden);
        var dialogs = new FakeDialogs();
        var (customers, dialog, _) = await OpenAsync(api, dialogs);
        await dialog.ContinueCommand.ExecuteAsync(null);
        await dialog.ContinueCommand.ExecuteAsync(null);
        await dialog.ContinueCommand.ExecuteAsync(null);

        await dialog.ContinueCommand.ExecuteAsync(null);

        Assert.Equal("Kunden importiert nur die Leitung.", dialog.Error);
        Assert.Same(dialog, dialogs.Open);
        Assert.Null(customers.Toast);
        Assert.Null(dialog.Outcome);
        Assert.False(dialog.IsBusy);
    }

    [Fact]
    public async Task Waehrend_der_Import_laeuft_ist_Abbrechen_gesperrt()
    {
        var gate = new TaskCompletionSource();
        var api = Api().Hold(ImportRoute, gate.Task);
        var dialogs = new FakeDialogs();
        var (_, dialog, running) = await OpenAsync(api, dialogs);
        await dialog.ContinueCommand.ExecuteAsync(null);
        await dialog.ContinueCommand.ExecuteAsync(null);
        await dialog.ContinueCommand.ExecuteAsync(null);

        var importing = dialog.ContinueCommand.ExecuteAsync(null);
        Assert.True(dialog.IsBusy);
        Assert.False(dialog.CancelCommand.CanExecute(null));
        Assert.False(dialog.ContinueCommand.CanExecute(null));
        Assert.False(dialog.CheckCommand.CanExecute(null));

        gate.SetResult();
        await importing;
        await running;
        Assert.False(dialog.IsBusy);
        Assert.NotNull(dialog.Outcome);
    }
}
