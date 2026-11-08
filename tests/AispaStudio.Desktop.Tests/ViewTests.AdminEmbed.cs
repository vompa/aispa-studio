using AispaStudio.Contracts;
using Avalonia.Controls;
using Avalonia.Input.Platform;

namespace AispaStudio.Desktop.Tests;

/// <summary>R05 Slice 97 (Ä191): „Code kopieren“ im Abschnitt „Kundenbereich einbetten“ der Firmendaten.</summary>
public sealed partial class ViewTests
{
    private const string EmbedBookingUrl = "https://buchen.day-spa-wien.example/buchen/?spa=0190f5a3-0000-7000-8000-000000000093";

    [Fact]
    public Task Code_kopieren_legt_den_Einbettungscode_in_die_Zwischenablage() => Render("hell", "verwaltung-firmendaten-code-kopieren", async (window, main) =>
    {
        var shell = await OpenAdminPage(window, main);
        Assert.Same(shell.AdminPage.Company, shell.AdminPage.Content);
        Assert.True(shell.AdminPage.Company!.HasEmbedCode);
        await window.Clipboard!.ClearAsync();

        await ClickScrolled(window, "CopyEmbedButton");
        await Pump(Settling);

        Assert.Equal($"<iframe src=\"{EmbedBookingUrl}\" title=\"Online-Buchung\" width=\"100%\" height=\"900\" style=\"border:0\" loading=\"lazy\"></iframe>",
            await window.Clipboard.TryGetTextAsync());
        Assert.True(Shows(window, "Code kopiert."));
        return shell.AdminPage;
    }, Api().On("GET /api/tenant/embedding", new EmbeddingSettings([], EmbedBookingUrl)));

    [Fact]
    public Task Ohne_Adresse_der_API_fehlt_der_Knopf_Code_kopieren() => Render("hell", "verwaltung-firmendaten-ohne-code", async (window, main) =>
    {
        var shell = await OpenAdminPage(window, main);
        Assert.False(shell.AdminPage.Company!.HasEmbedCode);
        Assert.False(Named<Button>(window, "CopyEmbedButton").IsEffectivelyVisible);
        return shell.AdminPage;
    });
}
