using System.Globalization;
using AispaStudio.Contracts;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>
/// Eine Zeile der Öffnungszeiten eines Standorts (Standorte &amp; Räume, R05 Slice 95): Wochentag mit Häkchen „geöffnet“ und Beginn und Ende als Uhrzeit.
/// Hat ein Tag mehrere Zeiträume (die API erlaubt es, das Web zeigt nur einen), bleiben die weiteren unverändert erhalten.
/// </summary>
public sealed partial class OpeningDayViewModel : ObservableObject
{
    private static readonly TimeOnly DefaultOpens = new(9, 0);
    private static readonly TimeOnly DefaultCloses = new(18, 0);

    private IReadOnlyList<OpeningHours> _rest = [];

    public OpeningDayViewModel(DayOfWeek day, string label)
    {
        Day = day;
        Label = label;
    }

    public DayOfWeek Day { get; }

    /// <summary>„Montag“ …; auch der Name des Häkchens für den Screenreader.</summary>
    public string Label { get; }

    public string OpensName => $"{Label} öffnet";

    public string ClosesName => $"{Label} schließt";

    [ObservableProperty]
    public partial bool IsOpen { get; set; }

    /// <summary>Beginn als <c>HH:mm</c>, wie ihn das Uhrzeitfeld bestätigt; leer, wenn die Eingabe gelöscht wurde.</summary>
    [ObservableProperty]
    public partial string OpensText { get; set; } = "";

    [ObservableProperty]
    public partial string ClosesText { get; set; } = "";

    /// <summary>Am Tag hat die Eingabe noch nichts Lesbares: Beginn oder Ende fehlt.</summary>
    public bool IsIncomplete => IsOpen && (!TryTime(OpensText, out _) || !TryTime(ClosesText, out _));

    /// <summary>Stellt die Zeile aus den Zeiträumen des Tages her; keiner heißt geschlossen.</summary>
    public void Apply(IEnumerable<OpeningHours> hours)
    {
        var list = hours.Where(h => h.Day == Day).ToList();
        IsOpen = list.Count > 0;
        OpensText = Format(list.Count > 0 ? list[0].Opens : DefaultOpens);
        ClosesText = Format(list.Count > 0 ? list[0].Closes : DefaultCloses);
        _rest = [.. list.Skip(1)];
    }

    /// <summary>Was an die API geht: der Zeitraum der Zeile und die unveränderten weiteren; geschlossen heißt kein Eintrag.</summary>
    public IEnumerable<OpeningHours> Hours()
    {
        if (!IsOpen)
        {
            yield break;
        }

        if (TryTime(OpensText, out var opens) && TryTime(ClosesText, out var closes))
        {
            yield return new OpeningHours(Day, opens, closes);
        }

        foreach (var more in _rest)
        {
            yield return more;
        }
    }

    /// <summary>Gleich, wenn Zustand und Zeiten gleich sind (für die Frage „geändert?“).</summary>
    public string Signature => IsOpen ? $"{Day}:{OpensText}-{ClosesText}" : $"{Day}:zu";

    private static string Format(TimeOnly time) => time.ToString("HH:mm", CultureInfo.InvariantCulture);

    private static bool TryTime(string text, out TimeOnly time) =>
        TimeOnly.TryParseExact(text, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out time);
}
