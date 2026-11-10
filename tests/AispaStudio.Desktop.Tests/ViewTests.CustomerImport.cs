using AispaStudio.Contracts;
using AispaStudio.Desktop.ViewModels;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Tests;

/// <summary>Kunden-Import (Export/Import, Aufgabe E): Eintrag „Kunden importieren …“ nur für die Leitung, jeder der vier Schritte in Hell und Dunkel, keine Bindungsfehler.</summary>
public sealed partial class ViewTests
{
    private static StubApi ImportApi() => Api()
        .On("POST /api/customers/import/preview", CustomerImportFlowTests.Preview())
        .On("POST /api/customers/import", new CustomerImportResult(true, 4, 2, 1, 0, 1))
        .OnFile("POST /api/customers/import/errors", [1, 2], "Importfehler_2026-11-09.xlsx");

    private static FakeFilePicker ImportFile() => new() { Picked = new PickedBytes("kunden.csv", 40, "Nachname;Vorname\nHuber;Anna\n"u8.ToArray()) };

    private static async Task<ShellViewModel> ShowCustomers(Window window, MainViewModel main)
    {
        await SignedInCalendar(main);
        var shell = (ShellViewModel)main.Content!;
        shell.SelectedPage = shell.Pages.First(p => p.Page == shell.Customers);
        await Pump();
        return shell;
    }

    private static bool ImportShown(Window window, string name) => window.GetVisualDescendants().OfType<Control>().Single(c => c.Name == name).IsEffectivelyVisible;

    private static List<string?> ImportTexts(Window window) => Dialog(window).GetVisualDescendants().OfType<TextBlock>().Where(t => t.IsEffectivelyVisible).Select(t => t.Text).ToList();

    [Theory]
    [InlineData("Rezeption", false)]
    [InlineData("Inhaberin", true)]
    public Task Kundenliste_zeigt_Kunden_importieren_nur_der_Leitung(string role, bool shown) => Render("hell", $"kunden-import-menue-{role}", async (window, main) =>
    {
        var shell = await ShowCustomers(window, main);

        var more = window.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "ImportMoreButton");
        Assert.Equal(shown, more.IsEffectivelyVisible);
        if (shown)
        {
            Assert.True(more.Bounds.Right <= window.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "NewButton").Bounds.Left + 1, "Das Menü steht vor dem Hauptknopf");
            more.Flyout!.ShowAt(more);
            await Pump();
            var items = window.GetVisualDescendants().OfType<MenuItem>().Where(i => i.IsEffectivelyVisible && i.Bounds.Height > 0).Select(i => i.Header as string).ToList();
            Assert.Equal(["Kunden importieren …"], items);
            more.Flyout.Hide();
        }

        Assert.Equal(shown, shell.Customers.ImportCustomersCommand.CanExecute(null));
        return shell;
    }, role == "Rezeption" ? ReceptionApi() : ImportApi());

    [Theory]
    [InlineData("hell")]
    [InlineData("dunkel")]
    public Task Kunden_importieren_zeigt_die_vier_Schritte(string theme) => Render(theme, "kunden-import", async (window, main) =>
    {
        var shell = await ShowCustomers(window, main);
        shell.Customers.FilePicker = ImportFile();

        var running = shell.Customers.ImportCustomersCommand.ExecuteAsync(null);
        await Pump(Settling);
        var dialog = (CustomerImportViewModel)main.DialogHost.Current!;
        var card = Dialog(window);
        Assert.Equal(600, card.Bounds.Width, 0.5);
        Assert.Equal("Kunden importieren?", card.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Name == "DialogTitle").Text);
        FakeDialogs.AssertRules(dialog.Request);

        // 1. Datei wählen
        Assert.Equal("Schritt 1 von 4 · Datei wählen", card.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Name == "ImportStepText").Text);
        Assert.True(ImportShown(window, "ImportFileStep"));
        Assert.False(ImportShown(window, "ImportMappingStep") || ImportShown(window, "ImportReviewStep") || ImportShown(window, "ImportRunStep"));
        var confirm = DialogButton(window, "ConfirmButton");
        Assert.Equal("Datei wählen …", confirm.Content);
        Assert.Contains("primary", confirm.Classes);
        Assert.Same(confirm, Focused(window));
        Assert.False(DialogButton(window, "BackButton").IsEffectivelyVisible);
        Assert.Contains("Erste Zeile: Spaltenköpfe.", ImportTexts(window).Single(t => t is not null && t.StartsWith("Erste Zeile", StringComparison.Ordinal))!, StringComparison.Ordinal);
        SaveShot(window, "kunden-import-1-datei", theme);

        // 2. Spalten zuordnen
        await dialog.ContinueCommand.ExecuteAsync(null);
        await Pump(Settling);
        Assert.True(ImportShown(window, "ImportMappingStep"));
        Assert.False(ImportShown(window, "ImportFileStep"));
        var rows = card.GetVisualDescendants().OfType<ItemsControl>().Single(c => c.Name == "ImportColumns").GetVisualDescendants().OfType<ComboBox>().ToList();
        Assert.Equal(4, rows.Count);
        Assert.Equal(["Nachname", "Vorname", "Geburtstag", "nicht importieren"], rows.Select(r => r.SelectedItem?.ToString()));
        Assert.Equal("TT.MM.JJJJ", card.GetVisualDescendants().OfType<ComboBox>().Single(c => c.Name == "ImportDateFormat").SelectedItem?.ToString());
        Assert.Contains("Huber · Berger", ImportTexts(window));
        Assert.True(DialogButton(window, "BackButton").IsEffectivelyVisible);
        SaveShot(window, "kunden-import-2-zuordnung", theme);

        // ohne Nachname steht der Fehler am Feld, und „Weiter“ ist gesperrt
        dialog.Columns[0].Choice = ImportFieldChoice.For(null);
        await Pump();
        Assert.Contains(CustomerImportViewModel.LastNameMissing, ImportTexts(window));
        Assert.False(DialogButton(window, "ConfirmButton").IsEffectivelyEnabled);
        dialog.Columns[0].Choice = ImportFieldChoice.For(CustomerImportField.LastName);
        await Pump();
        Assert.DoesNotContain(CustomerImportViewModel.LastNameMissing, ImportTexts(window));
        Assert.True(DialogButton(window, "ConfirmButton").IsEffectivelyEnabled);

        // 3. Vorschau und Prüfung
        await dialog.ContinueCommand.ExecuteAsync(null);
        await Pump(Settling);
        Assert.True(ImportShown(window, "ImportReviewStep"));
        Assert.Equal("2 neu, 1 Dublette, 1 fehlerhaft", card.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Name == "ImportSummary").Text);
        Assert.True(ImportShown(window, "ImportDuplicates"));
        Assert.True(ImportShown(window, "ImportInvalid"));
        Assert.Equal(2, card.GetVisualDescendants().OfType<RadioButton>().Count(r => r.IsEffectivelyVisible));
        Assert.True(card.GetVisualDescendants().OfType<RadioButton>().First(r => r.IsEffectivelyVisible).IsChecked);
        Assert.Contains("Geburtstag kein Datum", ImportTexts(window));
        Assert.True(DialogButton(window, "ImportErrorsButton").IsEffectivelyVisible);
        SaveShot(window, "kunden-import-3-vorschau", theme);

        // 4. Probelauf
        await dialog.ContinueCommand.ExecuteAsync(null);
        await Pump(Settling);
        Assert.True(ImportShown(window, "ImportRunStep"));
        Assert.Equal("Importieren", DialogButton(window, "ConfirmButton").Content);
        Assert.True(DialogButton(window, "CheckButton").IsEffectivelyVisible);
        Assert.False(ImportShown(window, "ImportProgress"));
        await dialog.CheckCommand.ExecuteAsync(null);
        await Pump();
        Assert.Contains(ImportTexts(window), t => t is not null && t.StartsWith("Probelauf: 2 neu, 1 ergänzt, 0 übersprungen, 1 fehlerhaft", StringComparison.Ordinal));
        SaveShot(window, "kunden-import-4-probelauf", theme);

        // Abbrechen schließt ohne Import
        DialogButton(window, "CancelButton").Command!.Execute(null);
        await running;
        await Pump();
        Assert.False(main.DialogHost.HasDialog);
        Assert.Null(shell.Customers.Toast);
        return shell;
    }, ImportApi());

    [Fact]
    public Task Kunden_importieren_meldet_keine_Bindungsfehler() => Render("hell", "bindungen-kunden-import", async (window, main) =>
    {
        var shell = await ShowCustomers(window, main);
        shell.Customers.FilePicker = ImportFile();
        shell.Customers.Export.Files = new FakeFileSaver();
        await WithBindingLog(async log =>
        {
            var running = default(Task);
            await AssertNoBindingErrors(log, "Dialog", () =>
            {
                running = shell.Customers.ImportCustomersCommand.ExecuteAsync(null);
                return Task.CompletedTask;
            });
            var dialog = (CustomerImportViewModel)main.DialogHost.Current!;
            await AssertNoBindingErrors(log, "Zuordnung", () => dialog.ContinueCommand.ExecuteAsync(null));
            await AssertNoBindingErrors(log, "Vorschau", () => dialog.ContinueCommand.ExecuteAsync(null));
            await AssertNoBindingErrors(log, "Dubletten ergänzen", () =>
            {
                dialog.DuplicateRows[0].IsMerge = true;
                return Task.CompletedTask;
            });
            await AssertNoBindingErrors(log, "Fehlerzeilen", () => dialog.SaveErrorsCommand.ExecuteAsync(null));
            await AssertNoBindingErrors(log, "Probelauf", async () =>
            {
                await dialog.ContinueCommand.ExecuteAsync(null);
                await dialog.CheckCommand.ExecuteAsync(null);
            });
            dialog.CancelCommand.Execute(null);
            await running!;
            log.AssertClean();
            return true;
        });
        return shell;
    }, ImportApi());
}
