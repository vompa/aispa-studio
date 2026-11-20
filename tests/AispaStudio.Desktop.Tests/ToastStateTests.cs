using AispaStudio.Desktop.ViewModels;

namespace AispaStudio.Desktop.Tests;

/// <summary>Der Toast der Personenkarte (meldungen.md M1): eine Zeile ohne Schlusspunkt, 5 Sekunden, die Maus darüber hält ihn an.</summary>
public sealed class ToastStateTests
{
    private static readonly DateTimeOffset Start = new(2027, 3, 1, 9, 0, 0, TimeSpan.Zero);

    /// <summary>Die Frist läuft über Task.Delay; nach dem Weiterstellen kurz warten, bis die Fortsetzung durch ist (oder bis klar ist, dass nichts passiert).</summary>
    private static async Task SettleAsync(ToastState toast)
    {
        for (var i = 0; i < 20 && toast.IsShown; i++)
        {
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public void Given_ein_Satz_mit_Punkt_When_er_gezeigt_wird_Then_steht_der_Toast_ohne_Schlusspunkt()
    {
        var toast = new ToastState(new ActivityTests.TimerTime(Start));

        toast.Show("Gespeichert.");

        Assert.Equal("Gespeichert", toast.Text);
        Assert.True(toast.IsShown);
    }

    [Fact]
    public async Task Given_ein_Toast_When_fuenf_Sekunden_vergehen_Then_verschwindet_er()
    {
        var time = new ActivityTests.TimerTime(Start);
        var toast = new ToastState(time);
        toast.Show("Gespeichert");

        time.Advance(TimeSpan.FromSeconds(4.9));
        await SettleAsync(toast);
        Assert.True(toast.IsShown);

        time.Advance(TimeSpan.FromSeconds(0.2));
        await SettleAsync(toast);
        Assert.False(toast.IsShown);
    }

    [Fact]
    public async Task Given_die_Maus_ueber_dem_Toast_When_die_Frist_ablaeuft_Then_bleibt_er_bis_zum_Weggehen()
    {
        var time = new ActivityTests.TimerTime(Start);
        var toast = new ToastState(time);
        toast.Show("Gelöscht");

        toast.Hold(true);
        time.Advance(TimeSpan.FromSeconds(30));
        await SettleAsync(toast);
        Assert.True(toast.IsShown);

        toast.Hold(false);
        time.Advance(TimeSpan.FromSeconds(5.1));
        await SettleAsync(toast);
        Assert.False(toast.IsShown);
    }

    [Fact]
    public void Given_ein_Toast_When_er_geschlossen_wird_Then_ist_er_weg_und_ein_neuer_ersetzt_den_alten()
    {
        var toast = new ToastState(new ActivityTests.TimerTime(Start));
        toast.Show("Gespeichert");
        toast.Show("Gelöscht");
        Assert.Equal("Gelöscht", toast.Text);

        toast.CloseCommand.Execute(null);

        Assert.Null(toast.Text);
    }

    [Fact]
    public async Task Given_ein_Toast_mit_Aktion_When_fuenf_Sekunden_vergehen_Then_bleibt_er_bis_acht_Sekunden_und_die_Aktion_geht_mit()
    {
        var time = new ActivityTests.TimerTime(Start);
        var toast = new ToastState(time);
        var undone = 0;
        toast.Show("Verschoben auf 14:30", "Rückgängig", new CommunityToolkit.Mvvm.Input.RelayCommand(() => undone++));
        Assert.True(toast.HasAction);
        Assert.Equal("Rückgängig", toast.ActionText);

        time.Advance(TimeSpan.FromSeconds(7.9));
        await SettleAsync(toast);
        Assert.True(toast.IsShown);
        toast.Action!.Execute(null);
        Assert.Equal(1, undone);

        time.Advance(TimeSpan.FromSeconds(0.2));
        await SettleAsync(toast);
        Assert.False(toast.IsShown);
        Assert.False(toast.HasAction);
        Assert.Null(toast.Action);
    }

    [Fact]
    public void Given_ein_Toast_mit_Aktion_When_ein_Toast_ohne_Aktion_folgt_Then_ist_die_Aktion_weg()
    {
        var toast = new ToastState(new ActivityTests.TimerTime(Start));
        toast.Show("Verschoben auf 14:30", "Rückgängig", new CommunityToolkit.Mvvm.Input.RelayCommand(() => { }));

        toast.Show("Gespeichert");

        Assert.False(toast.HasAction);
        Assert.Null(toast.Action);
    }

    [Fact]
    public void Given_ein_Satz_ueber_sechzig_Zeichen_When_er_gezeigt_wird_Then_ist_er_auf_sechzig_gekuerzt_und_endet_mit_Auslassung()
    {
        var toast = new ToastState(new ActivityTests.TimerTime(Start));

        toast.Show(new string('a', 30) + " " + new string('b', 40) + ".");

        Assert.Equal(ToastState.MaxLength, toast.Text!.Length);
        Assert.EndsWith("…", toast.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void Given_ein_gekuerzter_Text_When_er_gezeigt_wird_Then_traegt_der_Toast_den_ganzen_Text_fuer_den_Tooltip()
    {
        var toast = new ToastState(new ActivityTests.TimerTime(Start));
        var file = "Gespeichert: Personalakte_Maria_Musterfrau_Dienstvertrag_2027-03-01.pdf";

        toast.Show(file);
        Assert.Equal(file, toast.FullText);

        toast.Show("Gespeichert.");
        Assert.Null(toast.FullText);

        toast.Show(file);
        toast.CloseCommand.Execute(null);
        Assert.Null(toast.FullText);
    }

    [Fact]
    public async Task Given_die_Maus_ueber_dem_Toast_When_er_geschlossen_wird_Then_laeuft_der_naechste_wieder_ab()
    {
        var time = new ActivityTests.TimerTime(Start);
        var toast = new ToastState(time);
        toast.Show("Gespeichert");
        toast.Hold(true);
        toast.CloseCommand.Execute(null);

        toast.Show("Gelöscht");
        time.Advance(TimeSpan.FromSeconds(5.1));
        await SettleAsync(toast);

        Assert.False(toast.IsShown);
    }
}
