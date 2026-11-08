using AispaStudio.Contracts;
using AispaStudio.Desktop.ViewModels;

namespace AispaStudio.Desktop.Tests;

/// <summary>Vor- und Nachbereitung als sichtbare Streifen am Termin: Mindesthöhe und Text nach verfügbarer Höhe.</summary>
public sealed class CalendarStripeTests
{
    private static readonly TimeZoneInfo Vienna = TimeZoneInfo.FindSystemTimeZoneById("Europe/Vienna");

    private static CalendarDay DayWithPreparation() => TestData.Day(TestData.Appointment(10, TestData.Anna)) with
    {
        Preparations = [new CalendarPreparation(TestData.MassageTreatment.Id, "Steine heizen")],
    };

    private static CalendarColumn Team(CalendarDay day, double pixelsPerMinute = DayLayout.PixelsPerMinute) =>
        DayLayout.Build(day, TestData.Resources, byRoom: false, Vienna, scale: new CalendarScale(pixelsPerMinute, DayLayout.ColumnWidth))
            .Columns.Single(c => c.Id == TestData.Anna.Id);

    [Fact]
    public void Streifen_ist_auch_bei_kleinem_Massstab_mindestens_6_Pixel_hoch()
    {
        var column = Team(DayWithPreparation(), pixelsPerMinute: 0.8);

        // 5 Minuten Vorbereitung ergeben bei 0,8 Pixel je Minute nur 4 Pixel.
        Assert.All(column.Stripes, s => Assert.True(s.Height >= 6, $"{s.Text}: {s.Height}"));
        var prep = column.Stripes.Single(s => s.Kind == StripeKind.Preparation);
        var item = column.Items.Single();
        // Das Band endet am Termin, es wächst nach oben und nicht unter den Termin.
        Assert.Equal(item.Top, prep.Top + prep.Height, 3);
    }

    [Fact]
    public void Vorbereitung_zeigt_ab_14_Pixel_die_Minuten_und_ab_20_Pixel_den_Schritt()
    {
        var day = DayWithPreparation();

        // 5 Minuten: 4 Pixel bei 0,8 (kein Text, Mindesthöhe 6), 8 bei 1,6, 15 bei 3,0.
        var small = Team(day, 0.8).Stripes.Single(s => s.Kind == StripeKind.Preparation);
        var medium = Team(day, 3.0).Stripes.Single(s => s.Kind == StripeKind.Preparation);
        var large = Team(day, 4.0).Stripes.Single(s => s.Kind == StripeKind.Preparation);

        Assert.False(small.ShowText);
        Assert.Equal(15, medium.Height, 3);
        Assert.True(medium.ShowText);
        Assert.Equal("Vorb. 5 Min.", medium.DisplayText);
        Assert.Equal(20, large.Height, 3);
        Assert.Equal("Vorb. 5 · Steine heizen", large.DisplayText);
        Assert.Equal("Vorbereitung 5 Min.: Steine heizen", large.Tip);
    }

    [Fact]
    public void Vorbereitung_ohne_Schritt_bleibt_auch_bei_grosser_Hoehe_bei_den_Minuten()
    {
        var day = TestData.Day(TestData.Appointment(10, TestData.Anna));

        var prep = Team(day, 4.0).Stripes.Single(s => s.Kind == StripeKind.Preparation);

        Assert.Equal("Vorb. 5 Min.", prep.DisplayText);
    }

    [Fact]
    public void Nachbereitung_in_der_Teamansicht_zeigt_die_Minuten()
    {
        var follow = Team(DayWithPreparation(), 3.0).Stripes.Single(s => s.Kind == StripeKind.FollowUp);

        Assert.Equal("Nachb. 10 Min.", follow.DisplayText);
        Assert.Equal("Nachbereitung 10 Min.", follow.Tip);
    }

    [Fact]
    public void Streifen_tragen_Klassen_fuer_Vorbereitung_und_Nachbereitung()
    {
        var stripes = Team(DayWithPreparation()).Stripes;

        Assert.True(stripes.Single(s => s.Kind == StripeKind.Preparation).IsPreparation);
        Assert.True(stripes.Single(s => s.Kind == StripeKind.FollowUp).IsAfter);
        Assert.False(stripes.Single(s => s.Kind == StripeKind.FollowUp).IsPreparation);
    }
}
