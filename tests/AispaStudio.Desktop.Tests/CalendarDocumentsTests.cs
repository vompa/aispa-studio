using AispaStudio.Contracts;
using AispaStudio.Desktop.ViewModels;

namespace AispaStudio.Desktop.Tests;

/// <summary>
/// PDF-Ausgaben E (ADR 0092): Im Kalender-Menü „+ Termin“ steht „Tagesplan drucken …“ und öffnet den Knopfblock „Drucken · PDF speichern“ mit der Wahl Team oder Räume;
/// die Wartelisten-Leiste trägt denselben Knopfblock für den gezeigten Tag. Den Inhalt der PDFs prüft die API (<c>DayPlanPdfTests</c>, <c>WaitlistPdfTests</c>).
/// </summary>
public sealed class CalendarDocumentsTests
{
    private static readonly TimeZoneInfo Vienna = TimeZoneInfo.FindSystemTimeZoneById("Europe/Vienna");
    private static readonly byte[] Pdf = [0x25, 0x50, 0x44, 0x46, 9];
    private static readonly string PlanRoute = $"/api/locations/{TestData.LocationId}/day-plan/pdf?date=2027-03-01";

    private sealed class FixedTime : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => TestData.At(10);

        public override TimeZoneInfo LocalTimeZone => Vienna;
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

    private static StubApi Api() => new StubApi()
        .On("GET /api/locations", new List<LocationDto> { TestData.Location })
        .On($"GET /api/locations/{TestData.LocationId}/resources", TestData.Resources)
        .On($"GET /api/locations/{TestData.LocationId}/calendar?date=2027-03-01", TestData.Day())
        .On($"GET /api/locations/{TestData.LocationId}/calendar?date=2027-03-02", TestData.Day() with { Date = TestData.Monday.AddDays(1) })
        .On($"GET /api/locations/{TestData.LocationId}/waitlist", new List<WaitlistStaffEntry>())
        .OnFile($"GET {PlanRoute}&by=team", Pdf, "Tagesplan-Wien-2027-03-01.pdf")
        .OnFile($"GET {PlanRoute}&by=rooms", Pdf, "Tagesplan-Wien-2027-03-01.pdf")
        .OnFile($"GET /api/locations/{TestData.LocationId}/waitlist/pdf?date=2027-03-01", Pdf, "Warteliste-Wien-2027-03-01.pdf");

    private static async Task<CalendarViewModel> OpenAsync()
    {
        var vm = new CalendarViewModel(Api().Client(), TestData.Reception(), null, new FixedTime());
        await vm.InitializeAsync();
        await vm.LoadAsync();
        return vm;
    }

    [Fact]
    public async Task Tagesplan_drucken_oeffnet_den_Knopfblock_ohne_Senden_zuerst_fuer_das_Team()
    {
        var vm = await OpenAsync();
        Assert.Null(vm.DayPlanPrint);

        vm.OpenDayPlanPrintCommand.Execute(null);

        var print = Assert.IsType<DayPlanPrintViewModel>(vm.DayPlanPrint);
        Assert.False(print.ByRooms);
        Assert.Equal($"{PlanRoute[1..]}&by=team", print.Document.Source.PdfPath);
        Assert.False(print.Document.HasSend);
        Assert.Equal("Tagesplan Montag, 01.03.2027", print.Title);
    }

    [Fact]
    public async Task Zeigt_der_Kalender_die_Raeume_ist_die_Raumansicht_vorgewaehlt()
    {
        var vm = await OpenAsync();
        vm.ShowRoomsCommand.Execute(null);

        vm.OpenDayPlanPrintCommand.Execute(null);

        Assert.True(vm.DayPlanPrint!.ByRooms);
        Assert.EndsWith("&by=rooms", vm.DayPlanPrint.Document.Source.PdfPath, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Das_Umschalten_auf_Raeume_tauscht_den_Pfad_und_Speichern_meldet_den_Dateinamen_der_API_als_Toast()
    {
        var vm = await OpenAsync();
        vm.OpenDayPlanPrintCommand.Execute(null);
        var print = vm.DayPlanPrint!;

        print.ByRooms = true;
        var files = new RecordingFileSaver();
        print.Document.Files = files;
        await print.Document.SaveCommand.ExecuteAsync(null);

        Assert.False(print.IsTeam);
        Assert.EndsWith("&by=rooms", print.Document.Source.PdfPath, StringComparison.Ordinal);
        Assert.Equal("Tagesplan-Wien-2027-03-01.pdf", files.SavedName);
        Assert.Equal("Gespeichert: Tagesplan-Wien-2027-03-01.pdf", vm.Toast);
    }

    [Fact]
    public async Task Schliessen_und_Tageswechsel_beenden_den_Knopfblock_weil_der_Pfad_den_Tag_nennt()
    {
        var vm = await OpenAsync();
        vm.OpenDayPlanPrintCommand.Execute(null);
        vm.DayPlanPrint!.CloseCommand.Execute(null);
        Assert.Null(vm.DayPlanPrint);

        vm.OpenDayPlanPrintCommand.Execute(null);
        vm.Date = TestData.Monday.AddDays(1);

        Assert.Null(vm.DayPlanPrint);
    }

    [Fact]
    public async Task Ein_zweiter_Klick_auf_Tagesplan_drucken_schliesst_den_Knopfblock()
    {
        var vm = await OpenAsync();

        vm.OpenDayPlanPrintCommand.Execute(null);
        vm.OpenDayPlanPrintCommand.Execute(null);

        Assert.Null(vm.DayPlanPrint);
    }

    [Fact]
    public async Task Die_Wartelisten_Leiste_traegt_den_Knopfblock_fuer_den_gezeigten_Tag_ohne_Senden()
    {
        var vm = await OpenAsync();

        vm.OpenWaitlistCommand.Execute(null);

        var document = vm.WaitlistPanel!.Document!;
        Assert.Equal($"api/locations/{TestData.LocationId}/waitlist/pdf?date=2027-03-01", document.Source.PdfPath);
        Assert.False(document.HasSend);
        Assert.Contains("01.03.2027", vm.WaitlistPanel.PrintHint, StringComparison.Ordinal);
        var files = new RecordingFileSaver();
        document.Files = files;
        await document.SaveCommand.ExecuteAsync(null);
        Assert.Equal("Warteliste-Wien-2027-03-01.pdf", files.SavedName);
        Assert.Equal("Gespeichert: Warteliste-Wien-2027-03-01.pdf", vm.Toast);
    }
}
