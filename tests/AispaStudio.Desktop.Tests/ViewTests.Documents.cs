using AispaStudio.Contracts;
using AispaStudio.Desktop.Controls;
using AispaStudio.Desktop.Services;
using AispaStudio.Desktop.ViewModels;
using AispaStudio.Desktop.Views;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Layout;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Tests;

/// <summary>Knopfblock der Dokumente und Dialog „Per Mail senden“ (ADR 0092) in Hell und Dunkel; das Bild dient dem Handbuch.</summary>
public sealed partial class ViewTests
{
    [Theory]
    [InlineData("hell")]
    [InlineData("dunkel")]
    public Task Knopfblock_Dokumente_und_Dialog_Per_Mail_senden(string theme) => Session.Dispatch(async () =>
    {
        Application.Current!.RequestedThemeVariant = theme == "dunkel" ? ThemeVariant.Dark : ThemeVariant.Light;
        var api = new StubApi()
            .OnFile("GET /api/pos/receipts/7/pdf", [0x25, 0x50, 0x44, 0x46], "Rechnung-K1-142.pdf")
            .On("GET /api/pos/receipts/7/send", new DocumentMailDraft("eva.muster@example.at", "Ihre Rechnung Nr. K1-142 von Day-Spa Wien",
                "Guten Tag,\n\nanbei Ihre Rechnung Nr. K1-142 als PDF.\n\nFreundliche Grüße\nDay-Spa Wien"));
        var host = new ConfirmDialogHost();
        var model = new DocumentActionsViewModel(new DocumentSource("Rechnung K1-142", "api/pos/receipts/7/pdf", "api/pos/receipts/7/send"), api.Client(), host, new SilentPrinter());
        var window = new Window
        {
            Width = 960,
            Height = 640,
            Content = new Panel
            {
                Children =
                {
                    new DocumentActions { DataContext = model, Margin = new Thickness(24), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top },
                    new ConfirmDialogView { DataContext = host },
                },
            },
        };
        window.Show();
        await Pump();

        var buttons = window.GetVisualDescendants().OfType<Button>().Where(b => b.Name is "PrintButton" or "SaveButton" or "SendButton").ToList();
        Assert.Equal(3, buttons.Count);
        Assert.All(buttons, b => Assert.True(b.IsEffectivelyVisible && b.Bounds.Height >= 28, $"{b.Name} ist sichtbar und groß genug"));

        var sending = model.SendCommand.ExecuteAsync(null);
        await Pump();
        var dialog = Assert.IsType<SendDocumentDialogViewModel>(host.Current);
        Assert.Equal("eva.muster@example.at", dialog.To);
        var field = window.GetVisualDescendants().OfType<TextBox>().First(t => t.Name == "SendTo");
        Assert.True(field.IsEffectivelyVisible);
        Assert.Equal("eva.muster@example.at", field.Text);
        Assert.NotNull(window.CaptureRenderedFrame());
        if (Environment.GetEnvironmentVariable("AISPA_SCREENSHOTS") is { Length: > 0 } folder)
        {
            await Pump(Settling);
            Directory.CreateDirectory(folder);
            window.CaptureRenderedFrame()!.Save(Path.Combine(folder, $"dokumente-per-mail-senden{(theme == "hell" ? "" : "-" + theme)}.png"), PngBitmapEncoderOptions.Default);
        }

        dialog.CancelCommand.Execute(null);
        await sending;
        Assert.Null(host.Current);
        window.Close();
        return true;
    }, CancellationToken.None);

    private sealed class SilentPrinter : IDocumentPrinter
    {
        public Task<string?> PrintAsync(string fileName, byte[] pdf, CancellationToken ct) => Task.FromResult<string?>(null);
    }
}
