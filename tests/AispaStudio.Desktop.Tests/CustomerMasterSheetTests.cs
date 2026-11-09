using AispaStudio.Contracts;
using AispaStudio.Desktop.ViewModels;

namespace AispaStudio.Desktop.Tests;

/// <summary>
/// PDF-Ausgaben E (ADR 0091): In der Kundenkarte steht im Menü „…“ der Eintrag „Stammblatt als PDF“; er öffnet den Knopfblock „Drucken · PDF speichern“ ohne Senden
/// und schließt beim Wechsel der Kundin. Den Inhalt des Stammblatts prüft die API (<c>CustomerCardPdfTests</c>).
/// </summary>
public sealed class CustomerMasterSheetTests
{
    private static readonly byte[] Pdf = [0x25, 0x50, 0x44, 0x46, 9];

    private sealed class RecordingFileSaver : IFileSaver
    {
        public string? SavedName { get; private set; }

        public Task<bool> SaveAsync(string suggestedName, string typeName, IReadOnlyList<string> patterns, byte[] content)
        {
            SavedName = suggestedName;
            return Task.FromResult(true);
        }
    }

    private static async Task<(CustomersViewModel Customers, CustomerDto Customer)> OpenAsync()
    {
        var customer = SpaSample.Customers[0];
        var stub = new StubApi()
            .On("GET /api/customers?search=", SpaSample.Customers.ToList())
            .On($"GET /api/customers/{customer.Id}/appointments", new List<AppointmentDto>())
            .OnFile($"GET /api/customers/{customer.Id}/card/pdf", Pdf, "Stammblatt-Eva-Muster.pdf");
        var customers = new CustomersViewModel(stub.Client(), TimeProvider.System);
        await customers.InitializeAsync();
        customers.Selected = customers.Results[0];
        return (customers, customer);
    }

    [Fact]
    public async Task Stammblatt_als_PDF_oeffnet_den_Knopfblock_ohne_Senden_speichert_mit_dem_Namen_der_API_und_schliesst_beim_Wechsel()
    {
        var (customers, customer) = await OpenAsync();
        Assert.False(customers.IsMasterSheetOpen);

        customers.ToggleMasterSheetCommand.Execute(null);
        var saver = new RecordingFileSaver();
        customers.MasterSheetDocument!.Files = saver;
        await customers.MasterSheetDocument.SaveCommand.ExecuteAsync(null);

        Assert.True(customers.IsMasterSheetOpen);
        Assert.False(customers.MasterSheetDocument.HasSend);
        Assert.Equal($"api/customers/{customer.Id}/card/pdf", customers.MasterSheetDocument.Source.PdfPath);
        Assert.Equal("Stammblatt-Eva-Muster.pdf", saver.SavedName);
        Assert.Equal("Gespeichert: Stammblatt-Eva-Muster.pdf", customers.MasterSheetMessage);

        customers.Selected = customers.Results[1];
        Assert.False(customers.IsMasterSheetOpen);
        Assert.Null(customers.MasterSheetMessage);
    }

    [Fact]
    public async Task Ein_zweiter_Klick_schliesst_das_Stammblatt_und_laesst_die_Auskunft_unberuehrt()
    {
        var (customers, _) = await OpenAsync();
        customers.ToggleDataAccessCommand.Execute(null);

        customers.ToggleMasterSheetCommand.Execute(null);
        customers.ToggleMasterSheetCommand.Execute(null);

        Assert.False(customers.IsMasterSheetOpen);
        Assert.True(customers.IsDataAccessOpen);
    }
}
