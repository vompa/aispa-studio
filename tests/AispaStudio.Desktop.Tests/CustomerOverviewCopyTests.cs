using AispaStudio.Contracts;
using AispaStudio.Desktop.ViewModels;

namespace AispaStudio.Desktop.Tests;

/// <summary>
/// Kundenübersicht im Flyout (T-031): Telefon und E-Mail kopieren wie in der Kundenkarte (T-024) mit kurzer Bestätigung „Kopiert.“; klappt das Kopieren
/// nicht, steht „Nicht kopiert.“ da.
/// </summary>
public sealed class CustomerOverviewCopyTests
{
    private static readonly CustomerDto Anna = SpaSample.Customers[0] with
    {
        FirstName = "Anna",
        LastName = "Huber",
        Phone = "+43 664 1234567",
        Email = "anna@huber.at",
    };

    private static async Task<CustomerOverviewViewModel> Overview(CustomerCardCopyTests.Clipboard clipboard, CustomerDto customer)
    {
        var api = new StubApi().On($"GET /api/customers/{customer.Id}", customer);
        var overview = new CustomerOverviewViewModel(api.Client(), customer.Id, TimeProvider.System, null, null) { ContactActions = clipboard };
        await overview.LoadAsync();
        return overview;
    }

    [Fact]
    public async Task Telefon_kopieren_legt_die_Nummer_ab_und_bestaetigt_mit_Kopiert()
    {
        var clipboard = new CustomerCardCopyTests.Clipboard();
        var overview = await Overview(clipboard, Anna);

        await overview.CopyPhoneCommand.ExecuteAsync(null);

        Assert.Equal(["+43 664 1234567"], clipboard.Copied);
        Assert.Equal("Kopiert.", overview.Copy.Text);
    }

    [Fact]
    public async Task E_Mail_kopieren_legt_die_Adresse_ab_und_bestaetigt_mit_Kopiert()
    {
        var clipboard = new CustomerCardCopyTests.Clipboard();
        var overview = await Overview(clipboard, Anna);

        await overview.CopyEmailCommand.ExecuteAsync(null);

        Assert.Equal(["anna@huber.at"], clipboard.Copied);
        Assert.Equal("Kopiert.", overview.Copy.Text);
    }

    [Fact]
    public async Task Gesperrte_Zwischenablage_meldet_Nicht_kopiert()
    {
        var overview = await Overview(new CustomerCardCopyTests.Clipboard(locked: true), Anna);

        await overview.CopyPhoneCommand.ExecuteAsync(null);

        Assert.Equal("Nicht kopiert.", overview.Copy.Text);
    }

    [Fact]
    public async Task Ohne_Nummer_kopiert_die_Uebersicht_nichts_und_meldet_nichts()
    {
        var clipboard = new CustomerCardCopyTests.Clipboard();
        var overview = await Overview(clipboard, Anna with { Phone = null });

        await overview.CopyPhoneCommand.ExecuteAsync(null);

        Assert.Empty(clipboard.Copied);
        Assert.Null(overview.Copy.Text);
    }

    [Fact]
    public async Task Die_Bestaetigung_verschwindet_nach_der_Anzeigedauer()
    {
        var overview = await Overview(new CustomerCardCopyTests.Clipboard(), Anna);
        overview.Copy.Duration = TimeSpan.FromMilliseconds(20);

        await overview.CopyPhoneCommand.ExecuteAsync(null);

        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (overview.Copy.Text is not null && DateTime.UtcNow < deadline)
        {
            await Task.Delay(20, TestContext.Current.CancellationToken);
        }

        Assert.Null(overview.Copy.Text);
    }
}
