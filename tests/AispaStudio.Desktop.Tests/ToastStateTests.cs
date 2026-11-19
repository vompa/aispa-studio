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
}
