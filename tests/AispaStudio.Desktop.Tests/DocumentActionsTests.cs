using System.Net;
using System.Runtime.Versioning;
using System.Text.Json;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Controls;
using AispaStudio.Desktop.Services;
using AispaStudio.Desktop.ViewModels;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Dialogs;
using Avalonia.Headless;

namespace AispaStudio.Desktop.Tests;

/// <summary>
/// Knopfblock „Drucken · PDF speichern · Per Mail senden“ (ADR 0092): Speichern mit dem Dateinamen der API, Senden erst nach der Rückfrage mit vorbelegter Adresse,
/// gesperrte Knöpfe ohne Recht mit Hinweis, Meldungen als Toast ohne Punkt. Inhalt und Texte der Dokumente prüft die API (<c>DocumentBuilderTests</c> und je Dokument).
/// </summary>
public sealed class DocumentActionsTests
{
    private const string PdfRoute = "GET /api/pos/receipts/7/pdf";
    private const string SendPath = "/api/pos/receipts/7/send";
    private static readonly byte[] Pdf = [0x25, 0x50, 0x44, 0x46, 1, 2, 3];
    private static readonly DocumentMailDraft Draft = new("eva@example.at", "Ihre Rechnung Nr. K1-142 von Day-Spa Wien", "Guten Tag,\n\nanbei Ihre Rechnung als PDF.");

    private static StubApi Api() => new StubApi()
        .OnFile(PdfRoute, Pdf, "Rechnung-K1-142.pdf")
        .On($"GET {SendPath}", Draft)
        .On($"POST {SendPath}", null, HttpStatusCode.Accepted);

    private static DocumentSource Source(bool allowed = true, bool send = true) =>
        new("Rechnung K1-142", "api/pos/receipts/7/pdf", send ? "api/pos/receipts/7/send" : null, allowed, "Belege sieht nur, wer das Recht „Belege ansehen“ hat.");

    private static DocumentActionsViewModel Model(StubApi api, FakeDialogs? dialogs = null, FakePrinter? printer = null, List<string>? toasts = null, RecordingFileSaver? files = null,
        bool allowed = true, bool send = true) =>
        new(Source(allowed, send), api.Client(), dialogs, printer ?? new FakePrinter()) { Files = files ?? new RecordingFileSaver(), ShowToast = toasts is null ? null : toasts.Add };

    private static async Task<T> Until<T>(Func<T?> read)
        where T : class
    {
        for (var i = 0; i < 200; i++)
        {
            if (read() is { } value)
            {
                return value;
            }

            await Task.Delay(10, TestContext.Current.CancellationToken);
        }

        throw new TimeoutException("Der Dialog kam nicht.");
    }

    [Fact]
    public async Task PDF_Speichern_oeffnet_nach_erneuter_Navigation_den_Dialog_am_aktuellen_Fenster()
    {
        await FileDialogSession.Current.Dispatch(async () =>
        {
            var model = new DocumentActionsViewModel(Source(), Api().Client(), null);
            var first = new DocumentActions { DataContext = model };
            var previousWindow = new Window { Content = first };
            previousWindow.Show();
            previousWindow.Content = null;
            previousWindow.Close();
            Assert.Null(TopLevel.GetTopLevel(first));

            var current = new DocumentActions { DataContext = model };
            var window = new Window { Content = current };
            window.Show();
            try
            {
                Assert.Same(window, TopLevel.GetTopLevel(current));
                var saving = model.SaveCommand.ExecuteAsync(null);
                for (var attempt = 0; !saving.IsCompleted && window.OwnedWindows.Count == 0 && attempt < 200; attempt++)
                {
                    await Task.Delay(10, TestContext.Current.CancellationToken);
                }

                Assert.Single(window.OwnedWindows).Close();
                await saving;
                var files = new RecordingFileSaver();
                model.Files = files;
                window.Content = new DocumentActions { DataContext = model };
                await model.SaveCommand.ExecuteAsync(null);
                Assert.Equal(Pdf, files.Saved?.Content);
            }
            finally
            {
                window.Close();
            }
            return true;
        }, TestContext.Current.CancellationToken);
    }

    private static class FileDialogSession
    {
        internal static readonly HeadlessUnitTestSession Current = HeadlessUnitTestSession.StartNew(typeof(FileDialogApp));
    }

    public sealed class FileDialogApp
    {
        [SupportedOSPlatform("linux")]
        [SupportedOSPlatform("macos")]
        [SupportedOSPlatform("windows")]
        public static AppBuilder BuildAvaloniaApp() => HeadlessApp.BuildAvaloniaApp().UseManagedSystemDialogs();
    }

    [Fact]
    public async Task Speichern_fragt_mit_dem_Dateinamen_der_API_und_schreibt_die_Bytes()
    {
        var files = new RecordingFileSaver();
        var toasts = new List<string>();
        var model = Model(Api(), toasts: toasts, files: files);

        await model.SaveCommand.ExecuteAsync(null);

        Assert.Equal("Rechnung-K1-142.pdf", files.Saved?.Name);
        Assert.Equal("PDF-Datei", files.Saved?.Type);
        Assert.Equal(["*.pdf"], files.Saved?.Patterns);
        Assert.Equal(Pdf, files.Saved?.Content);
        Assert.Equal(["Gespeichert: Rechnung-K1-142.pdf"], toasts);
        Assert.Null(model.Error);
    }

    [Fact]
    public async Task Abgebrochener_Speicherdialog_meldet_nichts()
    {
        var toasts = new List<string>();
        var model = Model(Api(), toasts: toasts, files: new RecordingFileSaver { Answer = false });

        await model.SaveCommand.ExecuteAsync(null);

        Assert.Empty(toasts);
    }

    [Fact]
    public async Task Senden_fragt_mit_vorbelegter_Adresse_und_schickt_erst_nach_Senden()
    {
        var api = Api();
        var dialogs = new FakeDialogs();
        var toasts = new List<string>();
        var model = Model(api, dialogs, toasts: toasts);

        var sending = model.SendCommand.ExecuteAsync(null);
        var dialog = Assert.IsType<SendDocumentDialogViewModel>(await Until(() => dialogs.Open));

        Assert.Equal("Rechnung K1-142 per Mail senden?", dialog.Title);
        FakeDialogs.AssertRules(dialog.Request);
        Assert.Equal("eva@example.at", dialog.To);
        Assert.Equal(Draft.Subject, dialog.Subject);
        Assert.Equal(Draft.Text, dialog.Text);
        Assert.True(dialog.HasAddress);
        Assert.DoesNotContain(api.Calls, c => c.Route == $"POST {SendPath}");
        Assert.False(model.SaveCommand.CanExecute(null), "Solange die Rückfrage offen ist, bleiben die Knöpfe gesperrt.");

        dialog.To = "anna@example.at";
        dialog.SendCommand.Execute(null);
        await sending;

        var body = JsonSerializer.Deserialize<SendDocumentRequest>(Assert.Single(api.Calls, c => c.Route == $"POST {SendPath}").Body!, ApiClient.Json)!;
        Assert.Equal("anna@example.at", body.To);
        Assert.Equal(Draft.Subject, body.Subject);
        Assert.Equal(Draft.Text, body.Text);
        Assert.Equal(["Gesendet an anna@example.at"], toasts);
        Assert.True(model.SaveCommand.CanExecute(null));
    }

    [Fact]
    public async Task Abbrechen_der_Rueckfrage_sendet_nichts()
    {
        var api = Api();
        var dialogs = new FakeDialogs();
        var toasts = new List<string>();
        var model = Model(api, dialogs, toasts: toasts);

        var sending = model.SendCommand.ExecuteAsync(null);
        var dialog = await Until(() => dialogs.Open);
        dialog.CancelCommand.Execute(null);
        await sending;

        Assert.DoesNotContain(api.Calls, c => c.Route == $"POST {SendPath}");
        Assert.Empty(toasts);
    }

    [Fact]
    public async Task Ohne_gueltige_Adresse_meldet_Senden_den_Fehler_am_Feld_setzt_den_Fokus_und_sendet_nichts()
    {
        var dialogs = new FakeDialogs();
        var api = Api().On($"GET {SendPath}", new DocumentMailDraft(null, "Betreff", "Text"));
        var model = Model(api, dialogs);

        _ = model.SendCommand.ExecuteAsync(null);
        var dialog = Assert.IsType<SendDocumentDialogViewModel>(await Until(() => dialogs.Open));
        string? focus = null;
        dialog.FocusRequested += field => focus = field;

        Assert.False(dialog.HasAddress);
        Assert.True(dialog.SendCommand.CanExecute(null));
        Assert.Null(dialog.ToError);
        dialog.SendCommand.Execute(null);
        Assert.Equal(("Bitte eine Mailadresse eintragen.", SendDocumentDialogViewModel.ToField), (dialog.ToError, focus));
        dialog.To = "anna@";
        Assert.Equal("Das ist keine gültige Mailadresse.", dialog.ToError);
        dialog.SendCommand.Execute(null);
        Assert.Equal(("Das ist keine gültige Mailadresse.", SendDocumentDialogViewModel.ToField), (dialog.ToError, focus));
        dialog.To = " anna@example.at ";
        Assert.Null(dialog.ToError);
        dialog.Subject = " ";
        dialog.SendCommand.Execute(null);
        Assert.Equal(("Bitte einen Betreff eintragen.", SendDocumentDialogViewModel.SubjectField), (dialog.SubjectError, focus));
        dialog.Subject = "Betreff";
        Assert.Null(dialog.SubjectError);
        Assert.DoesNotContain(api.Calls, c => c.Route == $"POST {SendPath}");
        dialog.CancelCommand.Execute(null);
    }

    [Fact]
    public void Ohne_Recht_sind_die_Knoepfe_gesperrt_und_der_Hinweis_nennt_das_Recht()
    {
        var model = Model(Api(), allowed: false);

        Assert.False(model.PrintCommand.CanExecute(null));
        Assert.False(model.SaveCommand.CanExecute(null));
        Assert.False(model.SendCommand.CanExecute(null));
        Assert.Equal("Belege sieht nur, wer das Recht „Belege ansehen“ hat.", model.DisabledHint);
        Assert.True(model.HasSend);
    }

    [Fact]
    public void Ohne_Versandpfad_gibt_es_keinen_Knopf_Per_Mail_senden()
    {
        var model = Model(Api(), send: false);

        Assert.False(model.HasSend);
        Assert.False(model.SendCommand.CanExecute(null));
        Assert.True(model.SaveCommand.CanExecute(null));
        Assert.Null(model.DisabledHint);
    }

    [Fact]
    public async Task Drucken_oeffnet_den_Druckdialog_und_meldet_es()
    {
        var printer = new FakePrinter();
        var toasts = new List<string>();

        await Model(Api(), printer: printer, toasts: toasts).PrintCommand.ExecuteAsync(null);

        Assert.Equal("Rechnung-K1-142.pdf", printer.Printed?.FileName);
        Assert.Equal(Pdf, printer.Printed?.Content);
        Assert.Equal(["Druckdialog geöffnet"], toasts);
    }

    [Fact]
    public async Task Waehrend_der_Druckdialog_aufgeht_ist_der_Knopfblock_gesperrt_und_ohne_Fehler()
    {
        var opening = new TaskCompletionSource<string?>();
        var printer = new FakePrinter(answer: opening.Task);
        var model = Model(Api(), printer: printer);

        var print = model.PrintCommand.ExecuteAsync(null);
        await Until(() => printer.Printed is null ? null : "gerufen");

        Assert.True(model.IsBusy);
        Assert.False(model.PrintCommand.CanExecute(null));
        Assert.Null(model.Error);
        opening.SetResult(null);
        await print;
        Assert.False(model.IsBusy);
    }

    [Fact]
    public async Task Drucken_ohne_Drucker_meldet_Kein_Drucker_eingerichtet()
    {
        var toasts = new List<string>();

        await Model(Api(), printer: new FakePrinter(WindowsPrintDialogPrinter.NoPrinter), toasts: toasts).PrintCommand.ExecuteAsync(null);

        Assert.Equal(["Kein Drucker eingerichtet"], toasts);
    }

    [Fact]
    public async Task Ein_Fehler_der_API_steht_unter_den_Knoepfen_statt_als_Toast()
    {
        var files = new RecordingFileSaver();
        var toasts = new List<string>();
        var model = Model(new StubApi(), toasts: toasts, files: files);

        await model.SaveCommand.ExecuteAsync(null);

        Assert.NotNull(model.Error);
        Assert.True(model.HasError);
        Assert.Null(files.Saved);
        Assert.Empty(toasts);
        model.CloseErrorCommand.Execute(null);
        Assert.False(model.HasError);
    }

    private sealed class RecordingFileSaver : IFileSaver
    {
        public bool Answer { get; init; } = true;

        public (string Name, string Type, IReadOnlyList<string> Patterns, byte[] Content)? Saved { get; private set; }

        public Task<bool> SaveAsync(string suggestedName, string typeName, IReadOnlyList<string> patterns, byte[] content)
        {
            Saved = (suggestedName, typeName, patterns, content);
            return Task.FromResult(Answer);
        }
    }

    private sealed class FakePrinter(string? problem = null, Task<string?>? answer = null) : IDocumentPrinter
    {
        public (string FileName, byte[] Content)? Printed { get; private set; }

        public Task<string?> PrintAsync(string fileName, byte[] pdf, CancellationToken ct)
        {
            Printed = (fileName, pdf);
            return answer ?? Task.FromResult(problem);
        }
    }
}
