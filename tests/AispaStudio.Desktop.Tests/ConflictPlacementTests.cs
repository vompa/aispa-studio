namespace AispaStudio.Desktop.Tests;

/// <summary>
/// Ortsregel für Meldungen im Kalender (meldungen.md, M7, Ä80): Fehler und Konfliktliste einer Aktion stehen im Panel direkt über dem Knopf der Aktion,
/// bei „Neuer Termin“, bei der gemeinsamen Buchung, beim Eintrag und beim Verschieben im Termin-Panel. Geprüft an der Reihenfolge im Quelltext der Ansicht.
/// </summary>
public sealed class ConflictPlacementTests
{
    private static readonly string CalendarAxaml = File.ReadAllText(Path.Combine(FindRepoRoot(), "src", "AispaStudio.Desktop", "Views", "CalendarView.axaml"));

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AispaStudio.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("AispaStudio.slnx nicht gefunden.");
    }

    /// <summary>Der Text einer Panel-Vorlage bis zur nächsten.</summary>
    private static string Template(string viewModel)
    {
        var start = CalendarAxaml.IndexOf($"<DataTemplate DataType=\"vm:{viewModel}\">", StringComparison.Ordinal);
        Assert.True(start >= 0, $"Vorlage {viewModel} fehlt.");
        var end = CalendarAxaml.IndexOf("<DataTemplate DataType=", start + 10, StringComparison.Ordinal);
        return CalendarAxaml[start..(end < 0 ? CalendarAxaml.Length : end)];
    }

    [Theory]
    [InlineData("BookingViewModel", "Command=\"{Binding BookCommand}\"")]
    [InlineData("GroupBookingViewModel", "Command=\"{Binding BookCommand}\"")]
    [InlineData("CalendarEntryViewModel", "Command=\"{Binding SaveCommand}\"")]
    public void Given_ein_Panel_mit_Aktion_When_eine_Meldung_erscheint_Then_steht_sie_ueber_dem_Knopf(string viewModel, string action)
    {
        var template = Template(viewModel);

        var button = template.IndexOf(action, StringComparison.Ordinal);
        Assert.True(button >= 0);
        Assert.InRange(template.IndexOf("Content=\"{Binding Conflict}\"", StringComparison.Ordinal), 0, button);
        Assert.InRange(template.IndexOf("Message=\"{Binding Error}\"", StringComparison.Ordinal), 0, button);
    }

    [Fact]
    public void Given_die_Verschieben_Karte_When_eine_Verschiebung_abgelehnt_wird_Then_steht_die_Meldung_in_der_Karte_ueber_Verschieben()
    {
        var template = Template("AppointmentViewModel");
        var card = template.IndexOf("Text=\"Verschieben\" FontWeight", StringComparison.Ordinal);
        var button = template.IndexOf("Command=\"{Binding MoveToDayCommand}\"", StringComparison.Ordinal);

        Assert.InRange(template.IndexOf("IsVisible=\"{Binding ErrorInMove}\"", StringComparison.Ordinal), card, button);
        Assert.InRange(template.IndexOf("IsVisible=\"{Binding ConflictInMove}\"", StringComparison.Ordinal), card, button);
        // Oben im Panel steht die Meldung nur, solange die Karte zu ist.
        Assert.Contains("IsVisible=\"{Binding ErrorOnTop}\"", template, StringComparison.Ordinal);
        Assert.Contains("IsVisible=\"{Binding ConflictOnTop}\"", template, StringComparison.Ordinal);
    }
}
