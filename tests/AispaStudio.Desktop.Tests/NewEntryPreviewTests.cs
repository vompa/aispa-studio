using AispaStudio.Contracts;
using AispaStudio.Desktop.ViewModels;
using Avalonia.Input;

namespace AispaStudio.Desktop.Tests;

/// <summary>
/// Ä67: Solange das Fenster „Neuer Termin“ offen ist, steht der geplante Eintrag als Vorschau (<see cref="CalendarViewModel.Ghost"/>) in der Spalte
/// und folgt den Eingaben. Testtag wie oben: Anna arbeitet 9 bis 17 Uhr, Termine um 10:00 und 12:00; eine Massage dauert 50 Minuten
/// mit 5 Minuten Vorbereitung und 10 Minuten Nachbereitung.
/// </summary>
public sealed partial class NewEntryTests
{
    private static double Ppm => DayLayout.PixelsPerMinute;

    [Fact]
    public async Task Vorschau_erscheint_beim_Oeffnen_in_der_Spalte_mit_Dauer_Vor_und_Nachbereitung()
    {
        var vm = await OpenAsync(Day());
        Assert.Null(vm.Ghost);

        vm.StartBookingAt(10, Y(vm, "13:00", 15));

        var ghost = vm.Ghost!;
        Assert.Equal(DayLayout.ColumnLeft(0, vm.Scale), ghost.Left);
        Assert.Equal(vm.Hours.Single(h => h.Label == "13:00").Top + (15 * Ppm), ghost.Top, 0.01);
        Assert.Equal(50 * Ppm, ghost.Height, 0.01);
        Assert.Equal(5 * Ppm, ghost.BeforeHeight, 0.01);
        Assert.Equal(10 * Ppm, ghost.AfterHeight, 0.01);
        Assert.Equal("Massage", ghost.Text);
        Assert.False(ghost.IsWarning);
    }

    [Fact]
    public async Task Vorschau_wandert_mit_dem_Beginn_und_waechst_mit_der_Behandlung()
    {
        var vm = await OpenAsync(Day());
        vm.StartBookingAt(10, Y(vm, "13:00"));
        var before = vm.Ghost!;

        vm.Booking!.StartText = "14:30";
        Assert.Equal(before.Top + (90 * Ppm), vm.Ghost!.Top, 0.01);

        vm.Booking.Treatment = TestData.Facial;
        Assert.Equal(60 * Ppm, vm.Ghost!.Height, 0.01);
        Assert.Equal(0, vm.Ghost.BeforeHeight);
        Assert.Equal("Gesicht", vm.Ghost.Text);

        // Eine Uhrzeit, die sich nicht lesen lässt, nimmt die Vorschau weg, bis sie wieder stimmt.
        vm.Booking.StartText = "14:";
        Assert.Null(vm.Ghost);
        vm.Booking.StartText = "14:00";
        Assert.NotNull(vm.Ghost);
    }

    [Fact]
    public async Task Vorschau_steht_in_Warnfarbe_wenn_die_Zeit_belegt_ist()
    {
        var vm = await OpenAsync(Day());
        vm.StartBookingAt(10, Y(vm, "13:00", 15));
        Assert.False(vm.Ghost!.IsWarning);

        // Mitten im Termin um 10 Uhr.
        vm.Booking!.StartText = "10:30";
        Assert.True(vm.Ghost!.IsWarning);

        // Die Vorbereitung ragt in den Termin davor (Ende 11:00, Nachbereitung bis 11:10).
        vm.Booking.StartText = "11:05";
        Assert.True(vm.Ghost!.IsWarning);

        // Direkt nach dem Termin um 12 Uhr (belegt bis 13:00) ragt die Vorbereitung hinein, fünf Minuten später nicht mehr.
        vm.Booking.StartText = "13:00";
        Assert.True(vm.Ghost!.IsWarning);
        vm.Booking.StartText = "13:05";
        Assert.False(vm.Ghost!.IsWarning);
    }

    [Fact]
    public async Task Vorschau_steht_nicht_an_einem_anderen_Tag_als_dem_des_Eintrags()
    {
        var vm = await OpenAsync(Day());
        vm.StartBookingAt(10, Y(vm, "13:00", 15));
        Assert.NotNull(vm.Ghost);

        // Der Kalender zeigt einen anderen Tag, das Fenster bleibt auf dem alten stehen.
        vm.Date = vm.Date.AddDays(1);
        for (var i = 0; i < 100 && vm.Ghost is not null; i++)
        {
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }

        Assert.Null(vm.Ghost);
    }

    [Fact]
    public async Task Vorschau_verschwindet_beim_Schliessen_des_Fensters()
    {
        var vm = await OpenAsync(Day());
        vm.StartBookingAt(10, Y(vm, "13:00"));
        Assert.NotNull(vm.Ghost);

        vm.Booking!.CloseCommand.Execute(null);

        Assert.Null(vm.NewEntry);
        Assert.Null(vm.Ghost);
    }

    [Fact]
    public async Task Vorschau_folgt_dem_Wechsel_der_Art_und_zeigt_Pause_Sperrzeit_und_Notiz()
    {
        var vm = await OpenAsync(Day());
        vm.StartBookingAt(10, Y(vm, "14:00"));
        var start = vm.Ghost!.Top;

        Assert.True(vm.SelectNewEntry(3));
        Assert.Equal("Pause", vm.Ghost!.Text);
        Assert.Equal(start, vm.Ghost.Top, 0.01);
        Assert.Equal(30 * Ppm, vm.Ghost.Height, 0.01);

        vm.Entry!.EndText = "15:00";
        Assert.Equal(60 * Ppm, vm.Ghost!.Height, 0.01);

        Assert.True(vm.SelectNewEntry(4));
        Assert.Equal("Sperrzeit", vm.Ghost!.Text);
        vm.Entry!.Text = "Arzt";
        Assert.Equal("Arzt", vm.Ghost!.Text);

        Assert.True(vm.SelectNewEntry(5));
        Assert.Equal("Arzt", vm.Ghost!.Text);
        Assert.Equal(15 * Ppm, vm.Ghost.Height, 0.01);
        vm.Entry!.Text = "";
        Assert.Equal("Notiz", vm.Ghost!.Text);
    }

    [Fact]
    public async Task Vorschau_einer_Sperrzeit_ueber_einem_Termin_ist_eine_Warnung_eine_Notiz_nicht()
    {
        var vm = await OpenAsync(Day());
        vm.OpenBlockAt(10, Y(vm, "13:00"));
        vm.Entry!.StartText = "11:30";
        vm.Entry.EndText = "12:30";
        Assert.True(vm.Ghost!.IsWarning);

        Assert.True(vm.SelectNewEntry(5));
        Assert.False(vm.Ghost!.IsWarning);
    }

    [Fact]
    public async Task Vorschau_von_Paar_und_Spa_steht_an_der_angeklickten_Stelle()
    {
        var vm = await OpenAsync(Day());
        vm.StartBookingAt(10, Y(vm, "14:00"));
        var top = vm.Ghost!.Top;

        Assert.True(vm.SelectNewEntry(2));

        Assert.Equal("Paar/Spa", vm.Ghost!.Text);
        Assert.Equal(top, vm.Ghost.Top, 0.01);
        Assert.Equal(DayLayout.ColumnLeft(0, vm.Scale), vm.Ghost.Left);
    }

    [Fact]
    public async Task Die_Buchungsvorschau_unter_der_Maus_ersetzt_die_Vorschau_des_Fensters_nicht()
    {
        var vm = await OpenAsync(Day());
        vm.StartBookingAt(10, Y(vm, "13:00"));
        var preview = vm.Ghost;

        vm.Hover(DayLayout.ColumnLeft(1, vm.Scale) + 10, Y(vm, "15:00"));
        Assert.Same(preview, vm.Ghost);
        vm.ClearHover();
        Assert.Same(preview, vm.Ghost);

        // Nach dem Schließen zeigt die Maus wieder ihre Vorschau.
        vm.Booking!.CloseCommand.Execute(null);
        vm.Hover(10, Y(vm, "15:00"));
        Assert.NotNull(vm.Ghost);
        Assert.Equal("15:00", vm.Ghost.Text);
    }

    [Fact]
    public async Task Vorschau_bleibt_beim_Ziehen_eines_Termins_der_Zieher_und_kehrt_danach_zurueck()
    {
        var vm = await OpenAsync(Day());
        var item = vm.Columns[0].Items.First(i => i.Appointment is not null);
        vm.StartBookingAt(10, Y(vm, "14:00"));
        var preview = vm.Ghost;

        vm.BeginDrag(item, 5);
        vm.DragTo(DayLayout.ColumnLeft(1, vm.Scale) + 10, Y(vm, "15:00"));
        Assert.True(vm.Ghost!.IsMove);

        vm.CancelDrag();
        Assert.Equal(preview, vm.Ghost);
    }

    // ---------- Vorschau ziehen (Slice 71, Ä83) ----------

    [Fact]
    public async Task Vorschau_ziehen_behaelt_Kunde_und_Behandlung_und_verlegt_Spalte_und_Beginn()
    {
        // Ben kann hier „Gesicht“, damit die Behandlung beim Ziehen bleibt; was die neue Spalte nicht kann, fällt weg (BookingColumnTests).
        var vm = await OpenAsync(Day(), TestData.Resources with { Employees = [TestData.Anna, TestData.Ben with { QualificationIds = TestData.Facial.RequiredQualificationIds }] });
        vm.StartBookingAt(10, Y(vm, "13:00", 15));
        var customer = new CustomerDto(Guid.NewGuid(), "Lena", "Gruber", null, null, null, null);
        vm.Booking!.Customers.Add(customer);
        vm.Booking.Customer = customer;
        vm.Booking.Treatment = TestData.Facial;
        var ghost = vm.Ghost!;
        Assert.True(vm.IsOnPreview(ghost.Left + 20, ghost.Top + 5));
        Assert.False(vm.IsOnPreview(ghost.Left + 20, ghost.Top + ghost.Height + 20));

        // Gegriffen 5 Pixel unter dem oberen Rand, abgelegt mit dem oberen Rand auf 15:00 bei Ben.
        vm.BeginPreviewDrag(grabOffset: 5);
        Assert.True(vm.IsDragging);
        vm.DragTo(DayLayout.ColumnLeft(1, vm.Scale) + 20, Y(vm, "15:00") - 1 + 5);
        await vm.DropAsync();

        Assert.False(vm.IsDragging);
        Assert.Equal("15:00", vm.Booking!.StartText);
        Assert.Equal("Ben", vm.Booking.ColumnTitle);
        Assert.Equal(customer, vm.Booking.Customer);
        Assert.Equal(TestData.Facial.Id, vm.Booking.Treatment?.Id);
        Assert.Equal(DayLayout.ColumnLeft(1, vm.Scale), vm.Ghost!.Left);
        Assert.Equal(Y(vm, "15:00") - 1, vm.Ghost.Top, 0.01);
    }

    [Fact]
    public async Task Esc_beim_Ziehen_der_Vorschau_laesst_die_Maske_unveraendert()
    {
        var vm = await OpenAsync(Day());
        vm.StartBookingAt(10, Y(vm, "13:00", 15));
        var preview = vm.Ghost;
        var start = vm.Booking!.StartText;

        vm.BeginPreviewDrag(grabOffset: 5);
        vm.DragTo(DayLayout.ColumnLeft(1, vm.Scale) + 20, Y(vm, "15:00"));
        Assert.True(vm.HandleKey(Key.Escape, KeyModifiers.None));

        Assert.False(vm.IsDragging);
        Assert.Equal(start, vm.Booking!.StartText);
        Assert.Equal("Anna", vm.Booking.ColumnTitle);
        Assert.Equal(preview, vm.Ghost);
    }

    // ---------- Keine liegen gebliebenen Platzhalter (Slice 71, Ä90) ----------

    [Fact]
    public async Task Ohne_offenen_Eintrag_raeumt_das_Neuzeichnen_die_gestrichelte_Vorschau()
    {
        var vm = await OpenAsync(Day());
        vm.Hover(10, Y(vm, "13:00"));
        Assert.Equal("13:00", vm.Ghost!.Text);

        // Neu gezeichnet (Laden nach dem Speichern, Live-Änderung, Minutentakt), ohne dass die Maus sich bewegt hat.
        await vm.LoadAsync();

        Assert.Null(vm.NewEntry);
        Assert.Null(vm.Ghost);
    }
}
