using System.Text.Json;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Services;
using Avalonia.Controls;

namespace AispaStudio.Desktop.Tests;

/// <summary>R05 Slice 117 (Ä274, E14): Reiter „Zuständigkeiten“ und Ansicht „Zuständigkeiten“ mit echter Eingabe (Klick, Tippen, Strg+S) als Inhaberin; Bilder fürs Handbuch.</summary>
public sealed partial class ViewTests
{
    private static readonly DutyTypeDto DutyFirstAid = new(Guid.NewGuid(), DutyTypeKey.FirstAid, "Ersthelfer", "Erste-Hilfe-Kurs", 1, true);
    private static readonly DutyTypeDto DutyHygiene = new(Guid.NewGuid(), DutyTypeKey.Hygiene, "Hygiene", null, null, true);

    private static StubApi DutiesApi()
    {
        var (api, _) = RosterApi();
        var lena = RosterLena.Id;
        return api
            .On("GET /api/staff-duties/types", new List<DutyTypeDto> { DutyFirstAid, DutyHygiene })
            .On($"GET /api/employees/{lena}/duties", new List<StaffDutyDto>
            {
                new(Guid.NewGuid(), lena, DutyFirstAid.Id, "Ersthelfer", new DateOnly(2026, 1, 1), null, SpaSample.Marco.Id, "Marco Berger", false),
            })
            .On($"GET /api/employees/{lena}/certificates", new List<StaffCertificateDto>
            {
                new(Guid.NewGuid(), lena, "Erste-Hilfe-Kurs", new DateOnly(2024, 10, 3), new DateOnly(2026, 10, 3), "Rotes Kreuz", 60, true, true, true),
            })
            .On("GET /api/staff-regulations/flags", new List<StaffFlagsDto> { new(lena, [], [], [], [new("Ersthelfer", "Erste-Hilfe-Kurs abgelaufen am 3.10.")]) })
            .On("GET /api/staff-duties/overview", new List<DutyOverviewRowDto>
            {
                new(DutyFirstAid.Id, DutyTypeKey.FirstAid, "Ersthelfer", "Erste-Hilfe-Kurs",
                    [new(Guid.NewGuid(), lena, "Lena Huber", SpaSample.Marco.Id, "Marco Berger", new DateOnly(2026, 1, 1), null, false)]),
                new(DutyHygiene.Id, DutyTypeKey.Hygiene, "Hygiene", null, []),
            })
            .On("GET /api/staff-duties/expiring", new List<ExpiringCertificateDto>
            {
                new(Guid.NewGuid(), lena, "Lena Huber", "Erste-Hilfe-Kurs", new DateOnly(2026, 10, 3), true),
            });
    }

    [Theory]
    [InlineData("hell")]
    [InlineData("dunkel")]
    public Task Verwaltung_Team_Zustaendigkeiten_legt_per_Tippen_ein_Zertifikat_an_und_warnt_beim_abgelaufenen(string theme)
    {
        var api = DutiesApi();
        var route = $"POST /api/employees/{RosterLena.Id}/certificates";
        api.OnJson(route, body =>
        {
            var input = JsonSerializer.Deserialize<StaffCertificateInput>(body!, ApiClient.Json)!;
            return new StaffCertificateDto(Guid.NewGuid(), RosterLena.Id, input.Name, input.IssuedOn, input.ValidUntil, input.IssuedBy, input.RemindDays, false, false, false);
        });
        return Render(theme, "verwaltung-team-person-zustaendigkeiten", async (window, main) =>
        {
            var team = await OpenTeam(window, main);
            await ClickRow(window, "Lena Huber");
            Assert.True(Shows(window, "Warnung: Ersthelfer – Erste-Hilfe-Kurs abgelaufen am 3.10."));
            await ClickCardTab(window, "Zuständigkeiten");
            await Pump(Settling);
            Assert.True(Shows(window, "Vertretung: Marco Berger"));
            Assert.True(Shows(window, "Abgelaufen am 3.10.2026"));

            await ClickButton(window, VisibleNamed<Button>(window, "AddCertificateButton"));
            await Pump(Settling);
            await TypeIntoSuggest(window, "CertificateNameField", "Brandschutz");
            await TypeInto(window, "IssuedByField", "Feuerwehr");
            PressSave(window);
            await Pump(Settling);

            var sent = JsonSerializer.Deserialize<StaffCertificateInput>(Assert.Single(api.Calls, c => c.Route == route).Body!, ApiClient.Json)!;
            Assert.Equal("Brandschutz", sent.Name);
            Assert.Equal("Feuerwehr", sent.IssuedBy);
            Assert.Equal(60, sent.RemindDays);
            Assert.False(team.Card!.HasUnsavedChanges);
            return team;
        }, api);
    }

    [Theory]
    [InlineData("hell")]
    [InlineData("dunkel")]
    public Task Verwaltung_Team_Zustaendigkeiten_zeigt_wer_wofuer_zustaendig_ist_und_was_ablaeuft(string theme) =>
        Render(theme, "verwaltung-team-zustaendigkeiten", async (window, main) =>
        {
            var team = await OpenTeam(window, main);
            await ClickSection(window, "Zuständigkeiten");
            await Pump(Settling);

            Assert.True(Named<Control>(window, "DutyOverview").IsEffectivelyVisible);
            Assert.True(Shows(window, "Lena Huber, Vertretung Marco Berger, gültig ab 1.1.2026"));
            Assert.True(Shows(window, "Niemand eingetragen"));
            Assert.True(Shows(window, "Lena Huber: Erste-Hilfe-Kurs, abgelaufen am 3.10.2026"));
            Assert.True(VisibleNamed<Control>(window, "MinimumOnDutyField").IsEffectivelyVisible);
            Assert.False(Named<Control>(window, "TeamList").IsEffectivelyVisible);
            return team;
        }, DutiesApi());
}
