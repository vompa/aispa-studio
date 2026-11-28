using System.Net;
using System.Runtime.InteropServices;
using System.Text.Json;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Services;
using AispaStudio.Desktop.ViewModels;
using AispaStudio.Desktop.Views;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Tests;

public sealed partial class ViewTests
{
    [Fact]
    public Task Kassenverwaltung_zeigt_oeffentliche_FinanzOnline_Musterwerte_ohne_Zugangsfelder()
    {
        const string PublicSampleKey = "cWhay3H4asTvRzXzXGZQ3KyBEu9BZaIxl8J+L4Bhr5A=";
        const string PublicSampleSerial = "123456789ABCDEF0";
        var register = PracticeRegister;
        var path = $"/api/pos/registers/{register.Id}";
        var stub = Api().On("GET /api/pos/registers", new[] { register })
            .On("GET /api/pos/fiscal/providers?country=AT", new[] { HandbookProvider(FiscalCountry.AT) })
            .On($"GET {path}/fiscal-registration", new FiscalRegistrationDto(register.Code, PublicSampleKey, "qx6p",
                PublicSampleSerial, "test", true, "Übungskassa – bitte nicht bei FinanzOnline registrieren."));
        return Render("hell", "verwaltung-finanzonline-musterwerte", async (window, main) =>
        {
            var shell = await OpenAdminPage(window, main);
            shell.AdminPage.Show("masterData", "registers");
            await Pump(Settling);
            var fiscal = shell.AdminPage.Registers!.Fiscal!;
            Assert.Empty(fiscal.Fields);
            await ClickScrolled(window, Named<Button>(window, "LoadFiscalRegistrationButton"));
            await Pump(Settling);
            Assert.Contains(fiscal.Registration, row => row.Value == PublicSampleKey);
            var copyButtons = window.GetVisualDescendants().OfType<Button>()
                .Where(button => button.Name == "CopyFiscalValue").ToList();
            var checksum = Assert.Single(copyButtons, button => button.Tag is "qx6p");
            await ClickScrolled(window, checksum);
            Assert.Equal("qx6p", await window.Clipboard!.TryGetTextAsync());
            var serial = Assert.Single(copyButtons, button => button.Tag is PublicSampleSerial);
            await ClickScrolled(window, serial);
            Assert.Equal(PublicSampleSerial, await window.Clipboard!.TryGetTextAsync());
            serial.BringIntoView();
            await Pump(Settling);
            return fiscal;
        }, stub);
    }

    [Theory]
    [InlineData(1440)]
    [InlineData(1100)]
    public Task Fiscal_section_masks_credentials_and_uses_the_server_year_and_receipt(int width)
    {
        var historyId = Guid.NewGuid();
        var receipt = SpaSample.Receipt() with { Id = Guid.NewGuid(), Kind = ReceiptKind.Yearly, IsPractice = true };
        var register = SpaSample.Register with
        {
            FiscalProvider = "probe",
            IsPractice = true,
            YearlyReceipt = new(2026, null, false, true),
            PendingYearlyReceipts = [new(2025, historyId, false, false)],
        };
        var provider = new FiscalProviderDto("probe", "Signatureinrichtung im Test", FiscalProviderKind.OnlineService, false,
            [new("key", "Zugangsschlüssel", true)], "kassa-einrichten");
        var path = $"/api/pos/registers/{register.Id}";
        var activeRegister = register;
        var stub = Api().On("GET /api/pos/registers", new[] { register })
            .On("GET /api/pos/fiscal/providers?country=AT", new[] { provider })
            .On($"GET {path}/fiscal-credentials", new FiscalCredentialsDto([], null))
            .On($"PUT {path}/fiscal-credentials", new FiscalCredentialsDto(["key"], SpaSample.At(11, 40)))
            .On($"DELETE {path}/fiscal-credentials", null, HttpStatusCode.NoContent)
            .On($"POST {path}/fiscal-check", new FiscalCheckDto(true, "Die Einrichtung ist erreichbar."))
            .On($"GET {path}/fiscal-registration", new FiscalRegistrationDto(register.Code, "MUSTER-NUR-BILD", "MUSTER", "AT0-DEMO", "probe", true, null))
            .OnJson($"GET {path}", _ => activeRegister)
            .OnJson($"POST {path}/yearly-receipt", _ =>
            {
                activeRegister = activeRegister with { YearlyReceipt = new(2026, receipt.Id, false, false) };
                return receipt;
            });
        var pastConfirmation = $"{path}/confirmations/YearlyReceiptCheck?year=2025&receiptId={historyId:D}";
        stub.On($"DELETE {pastConfirmation}", null, HttpStatusCode.NoContent);
        stub.OnJson($"PUT {pastConfirmation}", _ =>
        {
            var confirmed = new RegisterConfirmationDto(RegisterConfirmationKind.YearlyReceiptCheck, 2025, historyId, SpaSample.At(11, 40), "u1", "Katrin Aigner");
            activeRegister = activeRegister with { PendingYearlyReceipts = [], Confirmations = [confirmed] };
            return confirmed;
        });

        return Render("hell", $"verwaltung-fiskal-{width}", async (window, main) =>
        {
            window.Width = width;
            var shell = await OpenAdminPage(window, main);
            var registers = shell.AdminPage.Registers!;
            shell.AdminPage.Show("masterData", "registers");
            await Pump(Settling);
            var fiscal = registers.Fiscal!;
            var secret = Assert.Single(window.GetVisualDescendants().OfType<TextBox>(), box => box.PasswordChar != '\0' && box.IsEffectivelyVisible);
            Assert.Equal("", secret.Text);
            secret.Text = "attrappe-nur-test";
            await ClickScrolled(window, Named<Button>(window, "SaveFiscalCredentialsButton"));
            await Pump(Settling);
            Assert.True(Assert.Single(fiscal.Fields).IsConfigured);
            Assert.Empty(secret.Text!);
            var saved = Assert.Single(stub.Calls, call => call.Route == $"PUT {path}/fiscal-credentials");
            Assert.Equal("attrappe-nur-test", JsonSerializer.Deserialize<FiscalCredentialsInput>(saved.Body!, ApiClient.Json)!.Values["key"]);
            await ClickScrolled(window, Named<Button>(window, "FiscalCheckButton"));
            await Pump(Settling);
            Assert.True(fiscal.Check!.Available);
            if (width == 1440)
            {
                secret.Text = "attrappe-erneuert";
                await ClickScrolled(window, Named<Button>(window, "SaveFiscalCredentialsButton"));
                await Pump(Settling);
                Assert.Null(fiscal.Check);
                await ClickScrolled(window, Named<Button>(window, "FiscalCheckButton"));
                await Pump(Settling);
                Assert.True(fiscal.Check!.Available);
                fiscal.Dialogs = new FakeDialogs().Answer(ConfirmChoice.Confirmed);
                var delete = window.GetVisualDescendants().OfType<Button>().Single(button => button.Command == fiscal.DeleteCredentialsCommand);
                await ClickScrolled(window, delete);
                await Pump(Settling);
                Assert.Null(fiscal.Check);
            }

            await ClickScrolled(window, Named<Button>(window, "LoadFiscalRegistrationButton"));
            await Pump(Settling);
            var copy = window.GetVisualDescendants().OfType<Button>().Single(button => button is { Name: "CopyFiscalValue", Tag: "MUSTER-NUR-BILD" });
            await ClickScrolled(window, copy);
            var assistantLeft = Named<Button>(window, "CompanionFab").TranslatePoint(new Point(), window)!.Value.X;
            Assert.True(RightOf(window, copy) <= assistantLeft);
            var clipboard = Assert.IsAssignableFrom<IClipboard>(window.Clipboard);
            Assert.Equal("MUSTER-NUR-BILD", await clipboard.TryGetTextAsync());
            var exclude = DataFormat.CreateBytesPlatformFormat("ExcludeClipboardContentFromMonitorProcessing");
            Assert.Contains(exclude, await clipboard.GetDataFormatsAsync());
            var selectable = window.GetVisualDescendants().OfType<SelectableTextBlock>().Single(text => text.Text == "MUSTER-NUR-BILD");
            selectable.SelectionStart = 0;
            selectable.SelectionEnd = "MUSTER".Length;
            selectable.Copy();
            await Pump();
            Assert.Equal("MUSTER", await clipboard.TryGetTextAsync());
            Assert.Contains(exclude, await clipboard.GetDataFormatsAsync());
            if (width == 1440)
            {
                SaveShot(window, "verwaltung-finanzonline", "hell");
                var view = window.GetVisualDescendants().OfType<RegisterFiscalView>().Single(item => item.DataContext == fiscal);
                var clipboardError = Assert.IsType<COMException>(Marshal.GetExceptionForHR(unchecked((int)0x800401D0), new IntPtr(-1)));
                await view.CopyRegistrationAsync("MUSTER-NUR-BILD", "MUSTER", _ =>
                    Task.FromException(clipboardError));
                Assert.Equal("Zwischenablage nicht verfügbar. Wert wurde nicht kopiert", shell.Confirmation.Text);
                Assert.Equal("MUSTER", await clipboard.TryGetTextAsync());
                shell.Confirmation.Close();
            }

            var before = Assert.Single(fiscal.Confirmations, item => item.Year == 2025);
            Assert.Equal(historyId, before.ReceiptId);
            var confirm = window.GetVisualDescendants().OfType<Button>().Single(button => Equals(button.CommandParameter, before) && button.Command == fiscal.ToggleConfirmationCommand);
            await ClickScrolled(window, confirm);
            await Pump(Settling);
            Assert.Single(stub.Calls, call => call.Route == $"PUT {pastConfirmation}");
            Assert.Equal("Katrin Aigner", fiscal.Confirmations.Single(item => item.Year == 2025).Confirmation?.ConfirmedByName);

            var yearly = Named<Button>(window, "CreateYearlyReceiptButton");
            Assert.True(yearly.IsEffectivelyEnabled);
            await ClickScrolled(window, yearly);
            await Pump(Settling);
            Assert.Single(stub.Calls, call => call.Route == $"POST {path}/yearly-receipt");
            Assert.Equal(receipt.Id, fiscal.YearlyReceipt!.Receipt.Id);
            Assert.True(fiscal.YearlyReceipt.IsPractice);
            Assert.NotNull(fiscal.YearlyReceipt.Document);
            Assert.False(yearly.IsEffectivelyEnabled);
            Assert.Equal(receipt.Id, fiscal.Confirmations.Single(item => item.Year == 2026).ReceiptId);
            Assert.True(yearly.Bounds.Height >= 24 && RightOf(window, yearly) <= window.ClientSize.Width);
            if (width == 1100)
            {
                var old = fiscal.Confirmations.Single(item => item.Year == 2025);
                stub.On($"GET {path}", null, HttpStatusCode.ServiceUnavailable);
                var undo = window.GetVisualDescendants().OfType<Button>().Single(button => Equals(button.CommandParameter, old) && button.Command == fiscal.ToggleConfirmationCommand);
                await ClickScrolled(window, undo);
                await Pump(Settling);
                Assert.Single(stub.Calls, call => call.Route == $"DELETE {pastConfirmation}");
                var after = Assert.Single(fiscal.Confirmations, item => item.Year == 2025);
                Assert.False(after.IsConfirmed);
                Assert.Equal(historyId, after.ReceiptId);
                Assert.True(after.CanToggle && after.HasReceipt);
            }

            return fiscal;
        }, stub);
    }
}
