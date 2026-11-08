using System.Net;
using AispaStudio.Contracts;
using AispaStudio.Desktop.ViewModels;

namespace AispaStudio.Desktop.Tests;

/// <summary>Ä184, Slice 90: Abschnitt „Verlauf“ im Termin-Panel; eingeklappt, lädt beim Aufklappen, neueste oben mit Datum und Uhrzeit in Ortszeit.</summary>
public sealed class HistorySectionTests
{
    private static readonly TimeZoneInfo Vienna = TimeZoneInfo.FindSystemTimeZoneById("Europe/Vienna");

    private static readonly Guid AppointmentId = Guid.NewGuid();

    private static string Route => $"GET /api/appointments/{AppointmentId}/history";

    // 3.11.2026 10:12 und 11:40 Wiener Zeit (Winterzeit, UTC+1).
    private static readonly List<HistoryEntry> Entries =
    [
        new(new DateTimeOffset(2026, 11, 3, 10, 40, 0, TimeSpan.Zero), "Verschoben von Lena: 14:00 → 15:30"),
        new(new DateTimeOffset(2026, 11, 3, 9, 12, 0, TimeSpan.Zero), "Angelegt per SMS"),
    ];

    private static HistorySectionViewModel Section(StubApi api) => new(api.Client(), $"api/appointments/{AppointmentId}/history", Vienna, "Tipp");

    private static async Task Until(Func<bool> condition)
    {
        for (var i = 0; i < 200 && !condition(); i++)
        {
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }

        Assert.True(condition());
    }

    [Fact]
    public async Task Eingeklappt_laedt_nichts_und_beim_Aufklappen_stehen_die_Eintraege_neueste_oben_mit_Datum_und_Uhrzeit()
    {
        var api = new StubApi().On(Route, Entries);
        var section = Section(api);
        Assert.False(section.IsOpen);
        Assert.Empty(api.Calls);

        section.IsOpen = true;
        await Until(() => section.HasEntries);

        Assert.Equal(["Verschoben von Lena: 14:00 → 15:30", "Angelegt per SMS"], section.Entries.Select(e => e.Text));
        Assert.Equal(["3.11.2026, 11:40", "3.11.2026, 10:12"], section.Entries.Select(e => e.WhenText));
        Assert.Equal("Angelegt per SMS, 3.11.2026, 10:12", section.Entries[1].Name);
        Assert.False(section.IsEmpty);
        Assert.False(section.IsLoading);
    }

    [Fact]
    public async Task Pfad_und_Tipp_kommen_vom_Aufrufer_zum_Beispiel_fuer_die_Kundenkarte()
    {
        var customerId = Guid.NewGuid();
        var api = new StubApi().On($"GET /api/customers/{customerId}/history", Entries);
        var section = new HistorySectionViewModel(api.Client(), $"api/customers/{customerId}/history", Vienna, "Wer die Kundendaten wann geändert hat");
        Assert.Equal("Wer die Kundendaten wann geändert hat", section.Tip);

        section.IsOpen = true;
        await Until(() => section.HasEntries);
        Assert.Equal(2, section.Entries.Count);
    }

    [Fact]
    public void Ueberschrift_ist_ohne_Angabe_Verlauf_und_auf_der_Kundenkarte_Aenderungen()
    {
        var standard = Section(new StubApi());
        Assert.Equal("Verlauf", standard.Title);
        Assert.Equal("Der Verlauf wurde nicht geladen.", standard.NotLoadedMessage);

        var customer = new HistorySectionViewModel(new StubApi().Client(), "api/customers/x/history", Vienna, "Tipp", "Änderungen", "Die Änderungen wurden nicht geladen.");
        Assert.Equal("Änderungen", customer.Title);
        Assert.Equal("Die Änderungen wurden nicht geladen.", customer.NotLoadedMessage);
    }

    [Fact]
    public async Task Erneutes_Aufklappen_laedt_den_aktuellen_Stand()
    {
        var api = new StubApi().On(Route, Entries);
        var section = Section(api);

        section.IsOpen = true;
        await Until(() => section.HasEntries);
        section.IsOpen = false;
        section.IsOpen = true;
        await Until(() => api.Calls.Count(c => c.Route == Route) == 2);
    }

    [Fact]
    public async Task Ohne_Eintraege_steht_ein_Hinweis_und_ein_Fehler_bietet_Erneut_laden()
    {
        var empty = Section(new StubApi().On(Route, new List<HistoryEntry>()));
        empty.IsOpen = true;
        await Until(() => empty.IsEmpty);

        var api = new StubApi().On(Route, null, HttpStatusCode.InternalServerError);
        var failing = Section(api);
        failing.IsOpen = true;
        await Until(() => failing.HasError);
        Assert.False(failing.IsEmpty);

        api.On(Route, Entries);
        await failing.LoadCommand.ExecuteAsync(null);
        Assert.False(failing.HasError);
        Assert.Equal(2, failing.Entries.Count);
    }
}
