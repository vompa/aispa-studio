using AispaStudio.Contracts;
using AispaStudio.Desktop.Controls;
using AispaStudio.Desktop.ViewModels;
using AispaStudio.Desktop.Views;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Tests;

/// <summary>Feldnamen und Knopfverben in der Kassa (Runde R01, Slice 09: W45, W49, W-Muster 1 und 13).</summary>
public sealed partial class ViewTests
{
    /// <summary>
    /// Alle sichtbaren, bedienbaren Felder der Kassa mit Namen; meldet jedes Feld, dessen Name leer, ein Typname oder sein Platzhalter ist.
    /// Gibt die Namen der geprüften Felder zurück, damit der Test sieht, dass der Scan die Felder des Zustands wirklich erfasst hat.
    /// </summary>
    private static List<string> ScanPosFields(Window window, string state, List<string> problems)
    {
        var page = window.GetVisualDescendants().OfType<PosView>().First();
        var names = new List<string>();
        foreach (var field in page.GetVisualDescendants().OfType<Control>().Where(c => c is TextBox or ComboBox))
        {
            if (!field.IsEffectivelyVisible || !field.IsEffectivelyEnabled || !field.Focusable)
            {
                continue;
            }

            var placeholder = field switch
            {
                TextBox box => box.PlaceholderText,
                ComboBox box => box.PlaceholderText,
                _ => null,
            };
            var unreadable = HasUnreadableName(field, out var name);
            if (unreadable || (!string.IsNullOrWhiteSpace(placeholder) && name == placeholder))
            {
                problems.Add($"{state}: {field.GetType().Name} \"{name}\" (Platzhalter \"{placeholder}\")");
            }

            names.Add(name);
        }

        return names;
    }

    [Fact]
    public Task Kassa_in_Kassieren_Rechnungsempfaenger_Storno_Abschluss_und_Einlage_jedes_Feld_hat_einen_Namen_der_nicht_aus_dem_Platzhalter_stammt()
    {
        var manager = new QuickSwitchPerson("u-leitung", "Maria Leitner", StaffRole.Manager);
        var stub = Api()
            .On("GET /api/pos/settings", new PosSettingsDto(20m, false))
            .On($"GET /api/pos/approvers?locationId={SpaSample.LocationId}", new List<QuickSwitchPerson> { manager });
        return Render("hell", "kassa-feldnamen", async (window, main) =>
        {
            var problems = new List<string>();
            await SignedInCalendar(main);
            var shell = (ShellViewModel)main.Content!;
            var pos = shell.Pos;
            pos.IsSalesColumnOpen = true;
            await shell.CheckoutAsync(SpaSample.Paying);
            await Pump();

            // Kassieren: Rabatt über der Grenze (Freigabe und PIN), Zahlung mit Gutschein (Code), Gutschein verkaufen (Behandlung).
            pos.EditDiscountCommand.Execute(pos.Lines[0]);
            pos.DiscountInput = "25";
            for (var i = 0; i < 50 && pos.Approvers.Count == 0; i++)
            {
                await Pump();
            }

            pos.Payments.First(p => p.IsVoucher).Amount = "10";
            await Pump();
            Assert.True(pos.NeedsApproval);
            var checkout = ScanPosFields(window, "Kassieren", problems);
            Assert.Contains("PIN", checkout);
            Assert.Contains("Gutschein-Code", checkout);
            Assert.Contains("Behandlung", checkout);
            Assert.Contains("Freigabe durch", checkout);
            pos.CancelDiscountCommand.Execute(null);
            pos.Payments.First(p => p.IsVoucher).Amount = "";

            // Beleg nach dem Bezahlen: Rechnungsempfänger erfassen.
            pos.Payments.First(p => p.Method == PaymentMethod.Card).Amount = "89";
            await pos.PayCommand.ExecuteAsync(null);
            await pos.StartInvoiceCommand.ExecuteAsync(pos.Receipt!);
            await Pump();
            var recipient = ScanPosFields(window, "Rechnungsempfänger nach dem Bezahlen", problems);
            Assert.Contains("Name oder Firma", recipient);
            Assert.Contains("Anschrift", recipient);
            Assert.Contains("UID-Nummer", recipient);
            pos.CancelInvoiceCommand.Execute(null);
            pos.CloseReceiptCommand.Execute(null);

            // Belege: Rechnungsempfänger und Storno am gewählten Beleg.
            pos.ShowReceiptsCommand.Execute(null);
            await pos.LoadReceiptsAsync();
            pos.SelectedReceiptItem = pos.Receipts.First();
            await Pump();
            await pos.StartInvoiceCommand.ExecuteAsync(pos.SelectedReceipt!);
            await Pump();
            var receipts = ScanPosFields(window, "Belege", problems);
            Assert.Contains("Name oder Firma", receipts);
            Assert.Contains("Grund", receipts);

            // Kassenlade: Tagesabschluss und Einlage.
            pos.ShowClosingCommand.Execute(null);
            await pos.LoadReportAsync();
            await Pump();
            var closing = ScanPosFields(window, "Abschluss und Einlage", problems);
            Assert.Contains("Notiz", closing);
            Assert.Contains("Grund", closing);

            Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
            return pos;
        }, stub);
    }

    [Fact]
    public Task Kassieren_zeigt_die_Verkaufsknoepfe_Wertgutschein_verkaufen_Behandlung_verkaufen_und_Paket_verkaufen() => Render("hell", "kassa-verkaufsknoepfe", async (window, main) =>
    {
        await SignedInCalendar(main);
        var shell = (ShellViewModel)main.Content!;
        shell.Pos.IsSalesColumnOpen = true;
        await shell.CheckoutAsync(SpaSample.Paying);
        await Pump();

        var page = window.GetVisualDescendants().OfType<PosView>().First();
        string Label(System.Windows.Input.ICommand command)
        {
            var button = page.GetVisualDescendants().OfType<Button>().Single(b => b.IsEffectivelyVisible && ReferenceEquals(b.Command, command));
            var visible = button.Content as string ?? button.GetVisualDescendants().OfType<TextBlock>().First(t => !string.IsNullOrWhiteSpace(t.Text)).Text!;
            Assert.Equal(visible, ControlAutomationPeer.CreatePeerForElement(button).GetName());
            return visible;
        }

        Assert.Equal("Wertgutschein verkaufen", Label(shell.Pos.SellVoucherCommand));
        Assert.Equal("Behandlung verkaufen", Label(shell.Pos.SellTreatmentVoucherCommand));
        Assert.Equal("Paket verkaufen", Label(shell.Pos.SellPackageCommand));

        // Die längeren Knöpfe drücken die Felder daneben nicht zusammen: Wert und Behandlung nutzen die Breite der Spalte.
        var value = page.GetVisualDescendants().OfType<FormField>().Single(f => f.Label == "Wert (EUR)");
        var treatment = page.GetVisualDescendants().OfType<FormField>().Single(f => f.Label == "Behandlung");
        // Die Felder füllen die Spalte „Artikel und Gutscheine“ bis auf Kartenrand und Rollbalken (Slice 129: 232 px statt der alten 330 px Spalte).
        var available = Named<Control>(page, "SalesColumn").Bounds.Width - 44;
        Assert.True(value.Bounds.Width >= available, $"Wert: {value.Bounds.Width} von {available}");
        Assert.True(treatment.Bounds.Width >= available, $"Behandlung: {treatment.Bounds.Width} von {available}");
        page.GetVisualDescendants().OfType<Button>().Single(b => ReferenceEquals(b.Command, shell.Pos.SellPackageCommand)).BringIntoView();
        await Pump();
        return shell.Pos;
    });
}
