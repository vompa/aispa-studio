using System.Text.Json;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Services;
using Avalonia.Controls;
using Avalonia.Input;

namespace AispaStudio.Desktop.Tests;

public sealed partial class ViewTests
{
    private static FiscalProviderDto HandbookProvider(FiscalCountry country) => country switch
    {
        FiscalCountry.AT => new("test", "AT0-Testeinrichtung", FiscalProviderKind.Test, true, [], "kassa"),
        FiscalCountry.DE => new("test", "Test-TSE", FiscalProviderKind.Test, true, [], "kassa"),
        FiscalCountry.CH => new("none", "Ohne Signatureinrichtung", FiscalProviderKind.NoSignature, false, [], "kassa"),
        _ => throw new ArgumentOutOfRangeException(nameof(country)),
    };

    [Theory]
    [InlineData(FiscalCountry.AT, true, 1440)]
    [InlineData(FiscalCountry.DE, true, 1100)]
    [InlineData(FiscalCountry.CH, false, 1440)]
    public Task Register_wizard_opens_from_create_button_and_creates_at_the_chosen_location(FiscalCountry country, bool practice, int width)
    {
        var provider = HandbookProvider(country);
        var (location, registerCode, header) = country switch
        {
            FiscalCountry.DE => (SpaSample.Salzburg with { Name = "München", Region = "DE-BY", TimeZone = "Europe/Berlin" },
                "MUENCHEN-2", "Day-Spa München\nRosenstraße 9\n80331 München"),
            FiscalCountry.CH => (SpaSample.Salzburg with { Name = "Basel", Region = "CH-BS", Currency = "CHF", TimeZone = "Europe/Zurich" },
                "BASEL-2", "Day-Spa Basel\nGerbergasse 8\n4001 Basel"),
            _ => (SpaSample.Salzburg, "SALZBURG-2", "Day-Spa Salzburg\nLinzer Gasse 9\n5020 Salzburg"),
        };
        var register = SpaSample.Register with
        {
            Id = Guid.NewGuid(),
            LocationId = location.Id,
            Code = registerCode,
            ReceiptHeader = header,
            Country = country,
            FiscalProvider = provider.Id,
            IsPractice = practice,
            Readiness = new(practice ? FiscalReadinessState.Practice : FiscalReadinessState.Ready, []),
        };
        var stub = Api()
            .On("GET /api/locations", new[] { SpaSample.Vienna, location })
            .On("GET /api/pos/fiscal/providers?country=AT", new[] { HandbookProvider(FiscalCountry.AT) })
            .On($"GET /api/pos/fiscal/providers?country={country}", new[] { provider })
            .On("POST /api/pos/registers", register);
        return Render("hell", $"kassa-einrichten-{country.ToString().ToLowerInvariant()}", async (window, main) =>
        {
            window.Width = width;
            var shell = await OpenAdminPage(window, main);
            await shell.Pos.InitializeAsync(SpaSample.LocationId);
            shell.AdminPage.Show("masterData", "registers");
            await Pump(Settling);
            ClickCenter(window, Named<Button>(window, "CreateRegisterButton"));
            await Pump(Settling);

            Assert.True(Named<ContentControl>(window, "RegisterWizardHost").IsEffectivelyVisible);
            var locationBox = Named<ComboBox>(window, "RegisterLocationBox");
            Assert.Same(locationBox, window.FocusManager!.GetFocusedElement());
            locationBox.SelectedItem = locationBox.Items.OfType<LocationDto>().Single(item => item.Id == register.LocationId);
            var countries = Named<ComboBox>(window, "RegisterCountryBox");
            countries.SelectedIndex = (int)country;
            var next = Named<Button>(window, "RegisterNextButton");
            Assert.True(next.Bounds.Height >= 24 && next.Bounds.Width >= 96);
            Assert.True(next.Focus());
            if (country == FiscalCountry.AT)
            {
                await Pump(Settling);
                SaveShot(window, "kassa-einrichten-standort", "hell");
            }

            Press(window, PhysicalKey.Enter);
            await Pump(Settling);
            if (practice)
            {
                Assert.False(next.IsEffectivelyEnabled);
                ClickCenter(window, Named<CheckBox>(window, "ConfirmPracticeBox"));
                await Pump();
            }

            if (country == FiscalCountry.AT)
            {
                Assert.True(Named<CheckBox>(window, "ConfirmPracticeBox").IsChecked);
                await Pump(Settling);
                SaveShot(window, "kassa-einrichten-uebungsbetrieb", "hell");
            }

            Assert.True(next.IsEffectivelyEnabled);
            ClickCenter(window, next);
            await Pump(Settling);
            var code = Named<TextBox>(window, "RegisterCodeBox");
            Assert.Same(code, window.FocusManager.GetFocusedElement());
            code.Text = registerCode;
            Named<TextBox>(window, "RegisterHeaderBox").Text = header;
            await Pump();
            if (country == FiscalCountry.AT)
            {
                SaveShot(window, "kassa-einrichten-belegkopf", "hell");
            }

            ClickCenter(window, next);
            await Pump(Settling);
            var submit = Named<Button>(window, "RegisterSubmitButton");
            Assert.True(submit.IsDefault && submit.IsEffectivelyVisible);
            ClickCenter(window, submit);
            await Pump(Settling);

            Assert.True(Named<Border>(window, "RegisterFinishCard").IsEffectivelyVisible);
            var sent = Assert.Single(stub.Calls, call => call.Route == "POST /api/pos/registers");
            var input = JsonSerializer.Deserialize<CashRegisterInput>(sent.Body!, ApiClient.Json)!;
            Assert.Equal((register.LocationId, country, provider.Id, (bool?)practice), (input.LocationId, input.Country, input.FiscalProvider, input.Practice));
            Assert.Equal((registerCode, header), (input.Code, input.ReceiptHeader));
            Assert.Contains(shell.Pos.Registers, item => item.Id == register.Id);
            Assert.Equal(register.Id, shell.AdminPage.Registers!.SelectedRow!.Register.Id);
            Assert.True(RightOf(window, Named<Button>(window, "RegisterFinishButton")) <= window.ClientSize.Width);
            return shell;
        }, stub);
    }

    [Theory]
    [InlineData(FiscalReadinessState.NotReady)]
    [InlineData(FiscalReadinessState.Practice)]
    [InlineData(FiscalReadinessState.Ready)]
    public Task Register_setup_card_shows_server_state_and_launches_when_not_ready(FiscalReadinessState state) =>
        Render("hell", $"firmendaten-kassa-{state.ToString().ToLowerInvariant()}", async (window, main) =>
        {
            var shell = await OpenAdminPage(window, main);
            var card = Named<Border>(window, "RegisterSetupCard");
            var button = Named<Button>(window, "RegisterSetupButton");
            Assert.True(card.IsEffectivelyVisible);
            Assert.Equal(state != FiscalReadinessState.Ready, button.IsEffectivelyVisible);
            Assert.True(RightOf(window, card) <= window.ClientSize.Width);
            if (state != FiscalReadinessState.Ready)
            {
                ClickCenter(window, button);
                await Pump(Settling);
                Assert.True(Named<ContentControl>(window, "RegisterWizardHost").IsEffectivelyVisible);
                ClickCenter(window, Named<Button>(window, "RegisterCloseButton"));
                await Pump(Settling);
                Assert.True(card.IsEffectivelyVisible);
            }

            return shell;
        }, Api().On("GET /api/pos/setup", new PosSetupDto(state, state switch
        {
            FiscalReadinessState.Ready => "Die Kassa ist für den Echtbetrieb bereit.",
            FiscalReadinessState.Practice => "Für den Echtbetrieb eine eigene Kassa einrichten.",
            _ => "Eine Kassa einrichten oder die Hinweise der vorhandenen Kassa prüfen.",
        })));
}
