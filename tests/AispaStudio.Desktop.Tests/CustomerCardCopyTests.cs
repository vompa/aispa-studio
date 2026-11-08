using AispaStudio.Contracts;
using AispaStudio.Desktop.Services;
using AispaStudio.Desktop.ViewModels;

namespace AispaStudio.Desktop.Tests;

/// <summary>
/// Kundenkarte (T-024, Ä368): Ein Klick auf Telefon oder E-Mail im Kopf kopiert den Wert und bestätigt kurz mit „Kopiert.“; klappt das Kopieren
/// nicht, steht „Nicht kopiert.“ da.
/// </summary>
public sealed class CustomerCardCopyTests
{
    private static readonly CustomerDto Anna = SpaSample.Customers[0] with
    {
        FirstName = "Anna",
        LastName = "Huber",
        Phone = "+43 664 1234567",
        Email = "anna@huber.at",
    };

    internal sealed class Clipboard(bool locked = false) : IContactActions
    {
        public List<string> Copied { get; } = [];

        public Task LaunchAsync(Uri uri) => Task.CompletedTask;

        public Task CopyAsync(string text)
        {
            if (locked)
            {
                throw new InvalidOperationException("Zwischenablage gesperrt");
            }

            Copied.Add(text);
            return Task.CompletedTask;
        }
    }

    private static CustomersViewModel Card(Clipboard clipboard, CustomerDto customer) =>
        new(new StubApi().Client(), TimeProvider.System) { ContactActions = clipboard, Selected = customer };

    [Fact]
    public async Task Klick_auf_Telefon_kopiert_die_Nummer_und_bestaetigt_mit_Kopiert()
    {
        var clipboard = new Clipboard();
        var card = Card(clipboard, Anna);

        await card.CopyPhoneCommand.ExecuteAsync(null);

        Assert.Equal(["+43 664 1234567"], clipboard.Copied);
        Assert.Equal("Kopiert.", card.Copy.Text);
    }

    [Fact]
    public async Task Klick_auf_E_Mail_kopiert_die_Adresse_und_bestaetigt_mit_Kopiert()
    {
        var clipboard = new Clipboard();
        var card = Card(clipboard, Anna);

        await card.CopyEmailCommand.ExecuteAsync(null);

        Assert.Equal(["anna@huber.at"], clipboard.Copied);
        Assert.Equal("Kopiert.", card.Copy.Text);
    }

    [Fact]
    public async Task Die_Bestaetigung_verschwindet_nach_der_Anzeigedauer()
    {
        var card = Card(new Clipboard(), Anna);
        card.Copy.Duration = TimeSpan.FromMilliseconds(20);

        await card.CopyPhoneCommand.ExecuteAsync(null);

        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (card.Copy.Text is not null && DateTime.UtcNow < deadline)
        {
            await Task.Delay(20, TestContext.Current.CancellationToken);
        }

        Assert.Null(card.Copy.Text);
    }

    [Fact]
    public async Task Gesperrte_Zwischenablage_meldet_Nicht_kopiert()
    {
        var card = Card(new Clipboard(locked: true), Anna);

        await card.CopyPhoneCommand.ExecuteAsync(null);

        Assert.Equal("Nicht kopiert.", card.Copy.Text);
    }

    [Fact]
    public async Task Ohne_Nummer_kopiert_die_Karte_nichts_und_meldet_nichts()
    {
        var clipboard = new Clipboard();
        var card = Card(clipboard, Anna with { Phone = null });

        await card.CopyPhoneCommand.ExecuteAsync(null);

        Assert.Empty(clipboard.Copied);
        Assert.Null(card.Copy.Text);
    }
}
