using AispaStudio.Contracts;
using AispaStudio.Desktop.ViewModels;

namespace AispaStudio.Desktop.Tests;

/// <summary>Sprache des Gastes im Kalender, Quelle eines Termins und „Freie Zeit suchen“ nach Sprache.</summary>
public sealed class GuestLanguageTests
{
    private static readonly TimeZoneInfo Vienna = TimeZoneInfo.FindSystemTimeZoneById("Europe/Vienna");
    private static readonly EmployeeDto Ben = TestData.Ben with { Languages = ["en", "it"] };

    private static async Task Until(Func<bool> condition)
    {
        for (var i = 0; i < 200 && !condition(); i++)
        {
            await Task.Delay(10);
        }
    }

    [Fact]
    public void Sprachkuerzel_nach_FEST_und_die_Hover_Karte_sagt_ob_die_Person_sie_spricht()
    {
        var guest = new CalendarGuest(Guid.NewGuid(), 6, 6, 0, null, null, new CustomerBookingPreferences(Ben.Id, true), Language: "it");
        var employees = new Dictionary<Guid, EmployeeDto> { [TestData.Anna.Id] = TestData.Anna, [Ben.Id] = Ben };

        var atBen = DayLayout.Flags(guest, Ben.Id, employees);
        var atAnna = DayLayout.Flags(guest, TestData.Anna.Id, employees);

        Assert.Equal(["FEST", "IT", "★"], atBen.Select(f => f.Symbol));
        Assert.Equal("Spricht Italienisch, Ben auch", atBen.Single(f => f.Kind == GuestFlagKind.Language).Tip);
        Assert.Equal("Spricht Italienisch; Anna hat Italienisch nicht als Sprache eingetragen", atAnna.Single(f => f.Kind == GuestFlagKind.Language).Tip);
        Assert.False(atAnna.Single(f => f.Kind == GuestFlagKind.Language).IsNote);
        Assert.Empty(DayLayout.Flags(guest with { Language = null, Visits = 1, VisitsLastYear = 1, Booking = null }));
    }

    [Fact]
    public void Quelle_steht_nur_bei_Terminen_die_nicht_das_Team_gebucht_hat()
    {
        Assert.Null(DayLayout.SourceText(AppointmentSource.Reception));
        Assert.Equal("Online", DayLayout.SourceText(AppointmentSource.Online));
        Assert.Equal("per WhatsApp", DayLayout.SourceText(AppointmentSource.WhatsApp));

        var online = TestData.Appointment(10, TestData.Anna) with { Source = AppointmentSource.Online };
        var item = new CalendarItem(online.Id, "Eva Muster", "Massage", "10:00–10:50", 0, 120, "#2f6f5e", online.Status, online)
        {
            SourceLabel = DayLayout.SourceText(online.Source),
        };
        Assert.Equal("10:00–10:50 · Online", item.MetaText);
    }

    [Fact]
    public async Task Freie_Zeit_mit_Sprache_zeigt_zuerst_wer_sie_spricht()
    {
        var resources = TestData.Resources with { Employees = [TestData.Anna, Ben] };
        var api = new StubApi().On($"GET /api/locations/{TestData.LocationId}/availability?treatmentId={TestData.MassageTreatment.Id}&date=2027-03-01&minSlots=3&days=14",
            new List<AvailableSlot>
            {
                new(TestData.At(10), TestData.At(10, 50), TestData.Anna.Id, TestData.Room.Id, null),
                new(TestData.At(14), TestData.At(14, 50), Ben.Id, TestData.Room.Id, null),
            });
        var vm = new FreeTimeViewModel(api.Client(), resources, TestData.Monday, Vienna, TestData.At(8), _ => Task.CompletedTask, () => { },
            TestData.MassageTreatment);
        await Until(() => vm.HasSlots);

        Assert.True(vm.HasLanguages);
        Assert.Equal(["egal", "Englisch", "Italienisch"], vm.Languages.Select(l => l.Name));
        Assert.Equal(["Anna", "Ben"], vm.Slots.Select(s => s.Who));

        vm.Language = vm.Languages.Single(l => l.Code == "it");
        await Until(() => vm.Slots.Count > 0 && vm.Slots[0].Who == "Ben");

        Assert.Equal(["Ben", "Anna"], vm.Slots.Select(s => s.Who));
        Assert.Equal(["spricht Italienisch", null], vm.Slots.Select(s => s.SpeaksLanguage));
        Assert.False(new FreeTimeViewModel(api.Client(), TestData.Resources, TestData.Monday, Vienna, TestData.At(8), _ => Task.CompletedTask, () => { })
            .HasLanguages);
    }
}
