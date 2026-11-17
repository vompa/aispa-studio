using System.Collections.ObjectModel;
using System.Globalization;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>Filter des Versandprotokolls: alles, nur Erinnerungen, nur Wiederbuchungen oder nur, was nicht ankam.</summary>
public enum DispatchFilter
{
    All,
    Reminder,
    Rebook,
    Failed,
}

/// <summary>Ein Filter der Leiste als Auswahl-Chip.</summary>
public sealed record DispatchFilterOption(DispatchFilter Filter, string Label)
{
    public override string ToString() => Label;
}

/// <summary>Zeitraum der Liste in Tagen; die Zusammenfassung in der Maske zählt die letzten 30.</summary>
public sealed record DispatchPeriod(int Days, string Label)
{
    public override string ToString() => Label;
}

/// <summary>Eine Zeile des Versandprotokolls; ein Klick klappt den Text oder den Grund auf, wenn es einen gibt.</summary>
public sealed partial class DispatchLogRow(DispatchLogEntryDto entry, string when, string detail) : ObservableObject
{
    public string When { get; } = when;

    public string Recipient { get; } = entry.Recipient;

    /// <summary>„Wiederbuchung · Gesichtsbehandlung Klassik“ oder „Erinnerung · Massage · Termin 31.10., 09:00“.</summary>
    public string Detail { get; } = detail;

    public string StateText { get; } = StateLabel(entry.State);

    /// <summary>Stilklasse der Marke: ok, crit oder leer.</summary>
    public string StateClass { get; } = entry.State switch
    {
        "Sent" => "ok",
        "Failed" => "crit",
        _ => "",
    };

    public bool IsSent => entry.State == "Sent";

    public bool IsFailed => entry.State == "Failed";

    public string? Text { get; } = entry.Text is { Length: > 0 } text ? $"„{text}“" : null;

    public string? Reason { get; } = entry.Error is { Length: > 0 } error ? $"Grund: {error}" : null;

    public bool HasDetail => Text is not null || Reason is not null;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Chevron))]
    public partial bool IsExpanded { get; set; }

    /// <summary>Zeichen vor dem Namen: leer ohne Text, sonst ▸ zu und ▾ auf.</summary>
    public string Chevron => !HasDetail ? "" : IsExpanded ? "▾" : "▸";

    [RelayCommand]
    private void Toggle()
    {
        if (HasDetail)
        {
            IsExpanded = !IsExpanded;
        }
    }

    internal static string StateLabel(string state) => state switch
    {
        "Sent" => "Gesendet",
        "Failed" => "Fehlgeschlagen",
        "Skipped" => "Entfallen",
        "Queued" => "Wird geschrieben",
        _ => state,
    };
}

/// <summary>
/// Versandprotokoll der Erinnerungen (P8, Auftraggeber, 17.11.2026): Terminerinnerungen und Wiederbuchungs-Hinweise in einer Liste als Seitenleiste neben der Maske
/// „Erinnerungen“, mit Filter nach Art und Ergebnis, einem Zeitraum und Export. Die Liste kommt aus einem Aufruf der API (<c>api/dispatch-log</c>), die Filter rechnet der Server;
/// der Text eines Hinweises klappt je Zeile auf.
/// </summary>
public sealed partial class DispatchLogViewModel : ObservableObject
{
    private readonly IWorkstationApi _api;
    private readonly TimeZoneInfo _zone;
    private readonly TimeProvider _time;
    private int _loadId;
    private bool _opening;

    public DispatchLogViewModel(IWorkstationApi api, TimeZoneInfo zone, TimeProvider? time = null)
    {
        _api = api;
        _zone = zone;
        _time = time ?? TimeProvider.System;
        SelectedFilter = Filters[0];
        Period = Periods[0];
        Export = new ExportViewModel(api, ListExportOptions.Pair("Versandprotokoll", ExportPath).ToList())
        {
            Saved = text => ExportStatus = text,
            Failed = text =>
            {
                ExportStatus = null;
                Error = text;
            },
        };
    }

    public static IReadOnlyList<DispatchPeriod> Periods { get; } =
        [new(30, "Letzte 30 Tage"), new(90, "Letzte 90 Tage"), new(366, "Letztes Jahr")];

    public static IReadOnlyList<DispatchFilterOption> Filters { get; } =
    [
        new(DispatchFilter.All, "Alle"),
        new(DispatchFilter.Reminder, "Erinnerung"),
        new(DispatchFilter.Rebook, "Wiederbuchung"),
        new(DispatchFilter.Failed, "Fehlgeschlagen"),
    ];

    /// <summary>Der Knopf „Exportieren“ der Leiste: dieselben Filter und derselbe Zeitraum wie die Liste.</summary>
    public ExportViewModel Export { get; }

    public ObservableCollection<DispatchLogRow> Rows { get; } = [];

    [ObservableProperty]
    public partial bool IsOpen { get; set; }

    /// <summary>Die gewählte Art; ein Wechsel lädt die Liste neu.</summary>
    [ObservableProperty]
    public partial DispatchFilterOption SelectedFilter { get; set; }

    public DispatchFilter Current => SelectedFilter.Filter;

    [ObservableProperty]
    public partial DispatchPeriod Period { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowEmpty))]
    public partial bool IsLoaded { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    public partial string? Error { get; private set; }

    [ObservableProperty]
    public partial string? ExportStatus { get; private set; }

    public bool HasError => Error is not null;

    /// <summary>Nichts im Zeitraum mit diesem Filter: die Leiste sagt das in einem Satz statt einer leeren Tabelle.</summary>
    public bool ShowEmpty => IsLoaded && Error is null && Rows.Count == 0;

    public string EmptyText => Current switch
    {
        DispatchFilter.Failed => "Nichts ist fehlgeschlagen. Alles ist angekommen.",
        DispatchFilter.Reminder => "Im Zeitraum wurde keine Terminerinnerung verschickt.",
        DispatchFilter.Rebook => "Im Zeitraum wurde kein Wiederbuchungs-Hinweis verschickt.",
        _ => "Im Zeitraum wurde noch nichts verschickt.",
    };

    /// <summary>Öffnet die Leiste mit dem Filter, den der Abschnitt der Maske vorgibt, und lädt die Liste.</summary>
    public async Task OpenAsync(DispatchFilter filter)
    {
        _opening = true;
        SelectedFilter = Filters.Single(f => f.Filter == filter);
        _opening = false;
        IsOpen = true;
        await LoadAsync().ConfigureAwait(true);
    }

    [RelayCommand]
    public void Close() => IsOpen = false;

    [RelayCommand]
    public async Task LoadAsync()
    {
        var id = ++_loadId;
        Error = null;
        ExportStatus = null;
        var result = await _api.GetAsync<List<DispatchLogEntryDto>>(ListPath()).ConfigureAwait(true);
        if (id != _loadId)
        {
            return;
        }

        Rows.Clear();
        if (result.Value is not { } entries)
        {
            Error = result.Error ?? "Das Versandprotokoll wurde nicht geladen.";
            IsLoaded = true;
            OnPropertyChanged(nameof(ShowEmpty));
            return;
        }

        var thisYear = TimeZoneInfo.ConvertTime(_time.GetUtcNow(), _zone).Year;
        foreach (var entry in entries)
        {
            Rows.Add(new DispatchLogRow(entry, When(entry.At, thisYear), Detail(entry, thisYear)));
        }

        IsLoaded = true;
        OnPropertyChanged(nameof(ShowEmpty));
    }

    partial void OnPeriodChanged(DispatchPeriod value)
    {
        if (IsOpen)
        {
            _ = LoadAsync();
        }
    }

    partial void OnSelectedFilterChanged(DispatchFilterOption value)
    {
        OnPropertyChanged(nameof(Current));
        OnPropertyChanged(nameof(EmptyText));
        if (IsOpen && !_opening)
        {
            _ = LoadAsync();
        }
    }

    /// <summary>Die Abfrage der API mit Art, Ergebnis und Zeitraum des Filters.</summary>
    private string Query() => Current switch
    {
        DispatchFilter.Reminder => $"kind=reminder&days={Period.Days}",
        DispatchFilter.Rebook => $"kind=rebook&days={Period.Days}",
        DispatchFilter.Failed => $"result=failed&days={Period.Days}",
        _ => $"days={Period.Days}",
    };

    private string ListPath() => $"api/dispatch-log?{Query()}";

    private string ExportPath(string format) => $"api/dispatch-log/export?{Query()}&format={format}";

    /// <summary>„02.11. 19:09“, im Vorjahr „02.11.25 19:09“.</summary>
    private string When(DateTimeOffset at, int thisYear)
    {
        var local = TimeZoneInfo.ConvertTime(at, _zone);
        return local.ToString(local.Year == thisYear ? "dd.MM. HH:mm" : "dd.MM.yy HH:mm", CultureInfo.InvariantCulture);
    }

    private string Detail(DispatchLogEntryDto entry, int thisYear)
    {
        if (entry.Kind == DispatchKind.Rebook)
        {
            return $"Wiederbuchung · {entry.Subject}";
        }

        var channel = entry.Channel switch
        {
            ReminderChannel.Sms => " · SMS",
            ReminderChannel.WhatsApp => " · WhatsApp",
            _ => "",
        };
        var start = "";
        if (entry.AppointmentStart is { } at)
        {
            var local = TimeZoneInfo.ConvertTime(at, _zone);
            start = $" · Termin {local.ToString(local.Year == thisYear ? "dd.MM., HH:mm" : "dd.MM.yy, HH:mm", CultureInfo.InvariantCulture)}";
        }

        return $"Erinnerung · {entry.Subject}{start}{channel}";
    }
}
