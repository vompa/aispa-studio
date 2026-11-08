using System.Globalization;
using AispaStudio.Contracts;
using AispaStudio.Desktop.ViewModels;
using Avalonia;
using Avalonia.Input;

namespace AispaStudio.Desktop.Tests;

/// <summary>Schritt 13: Einwirkzeit als Band im Termin der Person, Zeile im Ablauf und zwei Bahnen bei überlappenden Terminen.</summary>
public sealed class CalendarProcessingTests
{
    private static readonly TimeZoneInfo Vienna = TimeZoneInfo.FindSystemTimeZoneById("Europe/Vienna");

    private sealed class FixedTime : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => TestData.At(10);

        public override TimeZoneInfo LocalTimeZone => Vienna;
    }

    /// <summary>Packung 10:00–11:00 mit 5 Minuten Vorbereitung, 20 am Gast, 20 Einwirkzeit, 20 Ausarbeitung und 10 Nachbereitung.</summary>
    private static AppointmentDto Pack(EmployeeDto? who = null) => TestData.Appointment(10, who ?? TestData.Anna) with
    {
        End = TestData.At(11),
        BlockedFrom = TestData.At(9, 55),
        BlockedUntil = TestData.At(11, 10),
        PreparationMinutes = 5,
        FollowUpMinutes = 10,
        HandsOnMinutes = 20,
        ProcessingMinutes = 20,
    };

    /// <summary>Kurze Behandlung in der Einwirkzeit derselben Person: 10:20–10:40.</summary>
    private static AppointmentDto InsideProcessing() => TestData.Appointment(10, TestData.Anna) with
    {
        Start = TestData.At(10, 20),
        End = TestData.At(10, 40),
        BlockedFrom = TestData.At(10, 20),
        BlockedUntil = TestData.At(10, 40),
        PreparationMinutes = 0,
        FollowUpMinutes = 0,
    };

    private static CalendarColumn Team(params AppointmentDto[] appointments) =>
        DayLayout.Build(TestData.Day(appointments), TestData.Resources, byRoom: false, Vienna).Columns.Single(c => c.Id == TestData.Anna.Id);

    private static StubApi Api(params AppointmentDto[] appointments) => new StubApi()
        .On("GET /api/locations", new List<LocationDto> { TestData.Location })
        .On($"GET /api/locations/{TestData.LocationId}/resources", TestData.Resources)
        .On($"GET /api/locations/{TestData.LocationId}/calendar?date=2027-03-01", TestData.Day(appointments));

    [Fact]
    public void Das_Band_beginnt_nach_den_Minuten_am_Gast_und_nennt_die_Einwirkzeit()
    {
        var item = Team(Pack()).Items.Single();

        Assert.Equal(20 * DayLayout.PixelsPerMinute, item.ProcessingTop, 3);
        Assert.Equal(20 * DayLayout.PixelsPerMinute, item.ProcessingHeight, 3);
        Assert.Equal("Einwirkzeit 20 Min. · frei", item.ProcessingText);
        Assert.True(item.HasProcessing);
    }

    [Fact]
    public void Ohne_Einwirkzeit_und_in_alten_Staenden_gibt_es_kein_Band()
    {
        var plain = Team(TestData.Appointment(10, TestData.Anna)).Items.Single();
        var oldCache = Team(Pack() with { HandsOnMinutes = 0, ProcessingMinutes = 0 }).Items.Single();

        Assert.All(new[] { plain, oldCache }, i => Assert.Equal((false, 0.0, (string?)null), (i.HasProcessing, i.ProcessingHeight, i.ProcessingText)));
    }

    [Fact]
    public void Der_Text_im_Band_braucht_14_Pixel_Hoehe_und_darf_die_Zeilen_der_Karte_nicht_decken()
    {
        var small = new CalendarScale(0.5, DayLayout.ColumnWidth);
        var tiny = DayLayout.Build(TestData.Day(Pack()), TestData.Resources, byRoom: false, Vienna, scale: small).Columns.Single(c => c.Id == TestData.Anna.Id)
            .Items.Single();
        // 20 Minuten Einwirkzeit nach 20 Minuten am Gast: das Band beginnt in der dritten Zeile der Karte, der Text würde sie überdecken.
        var overlapping = Team(Pack()).Items.Single();
        // Mit 30 Minuten Einwirkzeit reicht das Band unter die Textzeilen.
        var clear = Team(Pack() with { ProcessingMinutes = 30 }).Items.Single();

        Assert.Equal((10, true, false), (tiny.ProcessingHeight, tiny.HasProcessing, tiny.ShowProcessingText));
        Assert.False(overlapping.ShowProcessingText);
        Assert.True(clear.ShowProcessingText);
    }

    [Fact]
    public void In_der_Raumansicht_gibt_es_kein_Band_und_gleichzeitige_Termine_stehen_in_Bahnen()
    {
        var column = DayLayout.Build(TestData.Day(Pack(), InsideProcessing()), TestData.Resources, byRoom: true, Vienna).Columns.Single();

        // Kein Band (der Raum ist belegt); was sich zeitlich überschneidet (z. B. im Paarraum), steht nebeneinander (Schritt 18, Ä22).
        Assert.All(column.Items, i => Assert.Equal(0.0, i.ProcessingHeight));
        Assert.Equal([(0, 2), (1, 2)], column.Items.OrderBy(i => i.Top).Select(i => (i.Lane, i.LaneCount)));
    }

    [Fact]
    public void Die_Hover_Karte_nennt_den_ganzen_Ablauf_und_laesst_leere_Teile_weg()
    {
        var plain = Team(Pack()).Items.Single().Hover!;
        // Keine Ausarbeitung danach: Dauer = Hände + Einwirken.
        var noFinishing = Team(Pack() with { HandsOnMinutes = 30, ProcessingMinutes = 30 }).Items.Single().Hover!;
        var without = Team(TestData.Appointment(10, TestData.Anna)).Items.Single().Hover!;

        // Die Raumpflege des Raums (10 Minuten) steht dahinter, wie bisher.
        Assert.Equal("Vorb. 5 · Hände 20 · Einwirken 20 · Ausarbeiten 20 · Nachb. 10 · Raumpflege 10 Min.", plain.Flow);
        Assert.Equal("Vorb. 5 · Hände 30 · Einwirken 30 · Nachb. 10 · Raumpflege 10 Min.", noFinishing.Flow);
        Assert.Equal("Vorb. 5 · Nachb. 10 · Raumpflege 10 Min.", without.Flow);
    }

    [Fact]
    public async Task Das_Panel_zeigt_den_Ablauf_in_einer_zweiten_Zeile()
    {
        var pack = Pack();
        var plain = TestData.Appointment(14, TestData.Anna);
        var vm = new CalendarViewModel(Api(pack, plain).Client(), TestData.Reception(), null, new FixedTime());
        await vm.InitializeAsync();
        await vm.LoadAsync();

        vm.OpenAppointment(pack);
        var withProcessing = vm.Selected!.BoundText;
        vm.OpenAppointment(plain);
        var without = vm.Selected!.BoundText;

        Assert.Equal("Gebunden 09:55–11:10 · Vorb. 5 · Nachb. 10 Min.\nVorb. 5 · Hände 20 · Einwirken 20 · Ausarbeiten 20 · Nachb. 10 Min.", withProcessing);
        Assert.Equal("Gebunden 13:55–15:00 · Vorb. 5 · Nachb. 10 Min.", without);
    }

    [Fact]
    public async Task Pfeil_runter_geht_auch_durch_Termine_in_schmalen_Bahnen()
    {
        var pack = Pack();
        var inside = InsideProcessing();
        var vm = new CalendarViewModel(Api(pack, inside).Client(), TestData.Reception(), null, new FixedTime());
        await vm.InitializeAsync();
        await vm.LoadAsync();

        vm.Mark(vm.Columns[0].Items.Single(i => i.Id == pack.Id));
        vm.HandleKey(Key.Down, KeyModifiers.None);

        Assert.Equal(inside.Id, vm.MarkedId);
        Assert.All(vm.Columns[0].Items, i => Assert.Equal(2, i.LaneCount));
    }

    [Fact]
    public void Zwei_ueberlappende_Termine_einer_Person_stehen_in_zwei_Bahnen_ein_einzelner_hat_die_volle_Breite()
    {
        var later = TestData.Appointment(14, TestData.Anna);
        var items = Team(Pack(), InsideProcessing(), later).Items.OrderBy(i => i.Top).ToList();

        Assert.Equal([(0, 2), (1, 2), (0, 1)], items.Select(i => (i.Lane, i.LaneCount)));
        var width = new CalendarScale(DayLayout.PixelsPerMinute, DayLayout.ColumnWidth).ItemWidth;
        Assert.Equal(width / 2 - 2, DayLayout.LaneWidth(width, new LanePlace(0, 2)), 3);
        Assert.Equal(width / 2, DayLayout.LaneLeft(width, new LanePlace(1, 2)), 3);
        Assert.Equal((width, 0.0), (DayLayout.LaneWidth(width, LanePlace.Alone), DayLayout.LaneLeft(width, LanePlace.Alone)));
    }

    [Fact]
    public void Hintereinander_liegende_Termine_teilen_sich_die_Spalte_nicht()
    {
        // 10:00–11:00 mit Nachbereitung bis 11:10, der nächste beginnt um 11:15: keine Überlappung der Karten.
        var next = TestData.Appointment(11, TestData.Anna) with { Start = TestData.At(11, 15), End = TestData.At(11, 45) };

        var items = Team(Pack(), next).Items;

        Assert.All(items, i => Assert.Equal(1, i.LaneCount));
    }

    [Fact]
    public void Die_Bindungen_rechnen_Breite_und_Versatz_aus_der_aktuellen_Spaltenbreite()
    {
        var culture = CultureInfo.InvariantCulture;

        var half = Converters.LaneWidth.Convert([202.0, new LanePlace(0, 2)], typeof(double), null, culture);
        var margin = Converters.LaneMargin.Convert([202.0, new LanePlace(1, 2)], typeof(Thickness), null, culture);
        var single = Converters.LaneMargin.Convert([202.0, LanePlace.Alone], typeof(Thickness), null, culture);

        Assert.Equal(99.0, half);
        Assert.Equal(new Thickness(104, 1, 3, 0), margin);
        Assert.Equal(new Thickness(3, 1, 3, 0), single);
    }
}
