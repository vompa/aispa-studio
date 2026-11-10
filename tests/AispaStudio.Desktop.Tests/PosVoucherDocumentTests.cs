using AispaStudio.Contracts;
using AispaStudio.Desktop.ViewModels;

namespace AispaStudio.Desktop.Tests;

/// <summary>
/// PDF-Ausgaben E (ADR 0092): Der Beleg in der Kassa trägt zu jedem mit ihm verkauften Gutschein einen Knopfblock „Drucken · PDF speichern · Per Mail senden“ für das
/// Geschenk-PDF. Den Inhalt des Gutscheins prüft die API (<c>VoucherPdfTests</c>).
/// </summary>
public sealed class PosVoucherDocumentTests
{
    private static readonly byte[] Pdf = [0x25, 0x50, 0x44, 0x46, 9];

    private static readonly VoucherDto Gift = new(Guid.NewGuid(), "K7QM-2XPA-9RDT", VoucherKind.MultiPurpose, 50m, 50m, null, VoucherStatus.Active, null);
    private static readonly VoucherDto Treatment = new(Guid.NewGuid(), "B3NV-8LTC-4WHE", VoucherKind.SinglePurpose, 79m, 79m, SpaSample.Treatments[0].Id, VoucherStatus.Active, null);

    private sealed class SampleTime : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => SpaSample.At(11, 40);

        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.FindSystemTimeZoneById("Europe/Vienna");
    }

    private sealed class RecordingFileSaver : IFileSaver
    {
        public string? SavedName { get; private set; }

        public Task<bool> SaveAsync(string suggestedName, string typeName, IReadOnlyList<string> patterns, byte[] content)
        {
            SavedName = suggestedName;
            return Task.FromResult(true);
        }
    }

    private static PosViewModel Pos() => new(new StubApi().OnFile($"GET /api/pos/vouchers/{Gift.Id}/pdf", Pdf, $"Gutschein-{Gift.Code}.pdf").Client(), new SampleTime());

    [Fact]
    public void Jeder_ausgestellte_Gutschein_bekommt_einen_Knopfblock_mit_PDF_und_Versand()
    {
        var receipt = SpaSample.Receipt() with { IssuedVouchers = [Gift, Treatment] };

        var documents = Pos().ReceiptFor(receipt, copy: false).VoucherDocuments;

        Assert.Equal([$"Gutschein {Gift.Code} als Geschenk", $"Gutschein {Treatment.Code} als Geschenk"], documents.Select(d => d.Label));
        var first = documents[0].Document.Source;
        Assert.Equal($"api/pos/vouchers/{Gift.Id}/pdf", first.PdfPath);
        Assert.Equal($"api/pos/vouchers/{Gift.Id}/send", first.SendPath);
        Assert.Equal($"Gutschein {Gift.Code}", first.Title);
        Assert.Equal($"api/pos/vouchers/{Treatment.Id}/pdf", documents[1].Document.Source.PdfPath);
    }

    [Fact]
    public void Ein_Beleg_ohne_Gutscheine_hat_keine_Knopfbloecke()
    {
        var model = Pos().ReceiptFor(SpaSample.Receipt(), copy: false);

        Assert.Empty(model.VoucherDocuments);
        Assert.False(model.HasVouchers);
    }

    [Fact]
    public async Task Speichern_holt_das_PDF_und_meldet_es_als_Toast_der_Kassa()
    {
        var pos = Pos();
        var documents = pos.ReceiptFor(SpaSample.Receipt() with { IssuedVouchers = [Gift] }, copy: false).VoucherDocuments;
        var files = new RecordingFileSaver();
        documents[0].Document.Files = files;

        await documents[0].Document.SaveCommand.ExecuteAsync(null);

        Assert.Equal($"Gutschein-{Gift.Code}.pdf", files.SavedName);
        Assert.Equal($"Gespeichert: Gutschein-{Gift.Code}.pdf", pos.Toast);
    }
}
