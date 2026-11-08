using System.Collections.ObjectModel;
using System.Globalization;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Controls;
using AispaStudio.Desktop.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>Balken im Belegungsverlauf: Lage in Pixeln, Höhe nach belegten Plätzen.</summary>
public sealed record AreaLoadBar(double Left, double Width, double Top, double Height, bool IsFull, bool IsHigh, string Tip);

/// <summary>Bereich in der Seitenleiste des Kalenders: wie voll er gerade ist (heute) bzw. höchstens wird.</summary>
public sealed record AreaGlance(string Name, string Text, double Share, bool IsHigh, bool IsFull)
{
    /// <summary>Breite des Füllbalkens bei 220 Pixeln Gesamtbreite.</summary>
    public double BarWidth => Math.Max(Share > 0 ? 3 : 0, 220 * Share);

    public static AreaGlance Of(AreaDay day, DateTimeOffset now, bool isToday)
    {
        var occupied = isToday
            ? day.Load.LastOrDefault(l => l.Start <= now)?.Occupied ?? 0
            : day.Load.Select(l => l.Occupied).DefaultIfEmpty().Max();
        var share = Math.Min(1, occupied / (double)Math.Max(1, day.Places));
        var text = isToday ? $"{occupied} / {day.Places}" : $"bis {occupied} / {day.Places}";
        return new AreaGlance(day.Name, text, share, share >= 0.8, occupied >= day.Places);
    }
}

/// <summary>Ein Eintritt in der Liste eines Bereichs.</summary>
public sealed partial class AreaVisitRow(AreaVisitDto visit, TimeZoneInfo zone, bool canCancel, Func<AreaVisitDto, Task> cancel,
    Func<AreaVisitDto, Task>? checkout = null, bool isPending = false)
{
    /// <summary>Ohne Verbindung vorgemerkt: noch nicht beim Server, deshalb weder stornierbar noch an der Kassa.</summary>
    public bool IsPending { get; } = isPending;

    public AreaVisitDto Visit { get; } = visit;

    public string TimeText => DayLayout.TimeText(Visit.From, Visit.Until, zone);

    public string Name => Visit.CustomerName;

    public string Detail => string.Join(" · ", new[]
    {
        IsPending ? "vorgemerkt" : null,
        Visit.Persons > 1 ? $"{Visit.Persons} Personen" : null,
        Visit.IsCancelled ? "storniert" : null,
        Visit.Note,
    }.Where(s => !string.IsNullOrEmpty(s)));

    public bool HasDetail => Detail.Length > 0;

    public bool IsCancelled => Visit.IsCancelled;

    public double Opacity => Visit.IsCancelled ? 0.5 : 1;

    public bool CanCancel { get; } = canCancel && !visit.IsCancelled && !isPending;

    [RelayCommand]
    private Task CancelAsync() => cancel(Visit);

    /// <summary>„Kassieren“: nur mit Kassa-Zugang und nicht für stornierte Eintritte.</summary>
    public bool CanCheckout { get; } = checkout is not null && !visit.IsCancelled && !isPending;

    [RelayCommand]
    private Task CheckoutAsync() => checkout?.Invoke(Visit) ?? Task.CompletedTask;
}

/// <summary>Ein Bereich (Sauna, Ruheraum) an einem Tag.</summary>
public sealed class AreaCard(AreaDay day, string summary, IReadOnlyList<AreaLoadBar> bars, IReadOnlyList<AreaVisitRow> visits,
    string openFrom, string openUntil)
{
    /// <summary>Beschriftung unter dem Verlauf: Öffnungszeit des Tages.</summary>
    public string OpenFrom { get; } = openFrom;

    public string OpenUntil { get; } = openUntil;

    public Guid RoomId => day.RoomId;

    public string Name => day.Name;

    public int Places => day.Places;

    /// <summary>„jetzt 4 von 12 Plätzen belegt“ bzw. die höchste Belegung des Tages.</summary>
    public string Summary { get; } = summary;

    public IReadOnlyList<AreaLoadBar> Bars { get; } = bars;

    public IReadOnlyList<AreaVisitRow> Visits { get; } = visits;

    public bool HasVisits => Visits.Count > 0;

    public override string ToString() => Name;
}

/// <summary>
/// Sauna und Ruheraum an der Rezeption: Belegung des Tages, Eintritte und neuer Eintritt. Ob noch Platz ist, entscheidet der Server;
/// eine Absage kommt mit Begründung zurück. Bereiche sind keine Kalenderspalten.
/// </summary>
public partial class AreasViewModel : ViewModelBase
{
    /// <summary>Breite des Belegungsverlaufs in der Seitenleiste.</summary>
    public const double TimelineWidth = 320;

    public const double TimelineHeight = 30;

    private readonly IWorkstationApi _api;
    private readonly Guid _locationId;
    private readonly DateOnly _date;
    private readonly TimeZoneInfo _zone;
    private readonly IReadOnlyList<TimeRange> _opening;
    private readonly TimeProvider _time;
    private readonly Action _close;
    private int _loadVersion;
    private readonly Action<IReadOnlyList<AreaDay>>? _loaded;
    private readonly Func<AreaVisitDto, Task>? _checkout;

    public AreasViewModel(IWorkstationApi api, Guid locationId, DateOnly date, TimeZoneInfo zone, IReadOnlyList<TimeRange> opening, bool canBook,
        TimeProvider time, Action close, Action<IReadOnlyList<AreaDay>>? loaded = null, Func<AreaVisitDto, Task>? checkout = null)
    {
        _loaded = loaded;
        _checkout = checkout;
        _api = api;
        _locationId = locationId;
        _date = date;
        _zone = zone;
        _opening = opening;
        _time = time;
        _close = close;
        CanBook = canBook;
        Search = new CustomerSearchViewModel(api);
        Search.Changed += () => BookCommand.NotifyCanExecuteChanged();

        // Heute ab der nächsten Viertelstunde, sonst ab 10 Uhr; drei Stunden sind ein üblicher Saunabesuch.
        var now = DayLayout.Local(time.GetUtcNow(), zone);
        var from = new TimeOnly(10, 0);
        if (DateOnly.FromDateTime(now) == date)
        {
            var minutes = (int)Math.Ceiling(now.TimeOfDay.TotalMinutes / 15) * 15;
            from = TimeOnly.FromTimeSpan(TimeSpan.FromMinutes(Math.Min(minutes, 20 * 60)));
        }

        FromText = from.ToString("HH:mm", CultureInfo.InvariantCulture);
        UntilText = from.AddHours(3).ToString("HH:mm", CultureInfo.InvariantCulture);
    }

    public bool CanBook { get; }

    public string DateText => DayLayout.DateText(_date);

    public DateOnly Date => _date;

    /// <summary>
    /// Beim Wechsel der Art (Ä153): Gast, Beginn als „Von“ (der Besuch behält seine Dauer) und Notiz; Personen bleibt bei 1.
    /// </summary>
    internal void Adopt(string? startText, CustomerDto? customer, string? note)
    {
        if (TimeInputParser.TryParse(startText) is { } from && TimeInputParser.TryParse(FromText) is { } oldFrom
            && TimeInputParser.TryParse(UntilText) is { } oldUntil)
        {
            FromText = from.ToString("HH:mm", CultureInfo.InvariantCulture);
            // Über Mitternacht gibt es kein „Bis“: der Besuch endet dann am Ende des Tages.
            var until = from.Add(oldUntil - oldFrom, out var wrappedDays);
            UntilText = (wrappedDays > 0 || until <= from ? new TimeOnly(23, 59) : until).ToString("HH:mm", CultureInfo.InvariantCulture);
        }

        if (customer is not null)
        {
            Search.Customer = customer;
        }

        if (string.IsNullOrWhiteSpace(Note))
        {
            Note = note;
        }
    }

    public ObservableCollection<AreaCard> Areas { get; } = [];

    public CustomerSearchViewModel Search { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty), nameof(ShowForm))]
    public partial bool Loaded { get; private set; }

    public bool IsEmpty => Loaded && Areas.Count == 0;

    public bool ShowForm => CanBook && Loaded && Areas.Count > 0;

    public bool ShowAreaChoice => Areas.Count > 1;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(BookCommand))]
    public partial AreaCard? Area { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(BookCommand))]
    public partial string FromText { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(BookCommand))]
    public partial string UntilText { get; set; }

    [ObservableProperty]
    public partial int Persons { get; set; } = 1;

    [ObservableProperty]
    public partial string? Note { get; set; }

    [ObservableProperty]
    public partial string? Error { get; set; }

    /// <summary>Kurze Bestätigung nach Buchen oder Stornieren.</summary>
    [ObservableProperty]
    public partial string? Info { get; private set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(BookCommand))]
    public partial bool Busy { get; private set; }

    /// <summary>Lädt den Tag; kommt eine ältere Antwort nach einer neueren an, wird sie verworfen.</summary>
    public async Task LoadAsync()
    {
        var version = ++_loadVersion;
        var result = await _api.GetAsync<List<AreaDay>>($"api/locations/{_locationId}/areas?date={_date:yyyy-MM-dd}").ConfigureAwait(true);
        if (version != _loadVersion)
        {
            return;
        }

        if (result.Value is null)
        {
            Error = result.Error;
            Loaded = true;
            return;
        }

        // Ohne Verbindung vorgemerkte Eintritte stehen schon in der Liste; die Belegung rechnet erst der Server.
        var pending = (_api.Offline?.Pending ?? []).Select(p => p.Visit).OfType<AreaVisitDto>()
            .Where(v => v.LocationId == _locationId && DateOnly.FromDateTime(DayLayout.Local(v.From, _zone)) == _date)
            .ToList();
        var pendingIds = pending.Select(v => v.Id).ToHashSet();
        var (open, close) = Window();
        var selected = Area?.RoomId;
        Areas.Clear();
        foreach (var day in result.Value)
        {
            var visits = day.Visits.Where(v => !pendingIds.Contains(v.Id)).Concat(pending.Where(v => v.RoomId == day.RoomId)).ToList();
            Areas.Add(Card(day with { Visits = visits }, open, close, pendingIds));
        }

        Area = Areas.FirstOrDefault(a => a.RoomId == selected) ?? Areas.FirstOrDefault();
        _loaded?.Invoke(result.Value);
        OnPropertyChanged(nameof(ShowAreaChoice));
        Loaded = true;
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(ShowForm));
    }

    private AreaCard Card(AreaDay day, DateTimeOffset open, DateTimeOffset close, HashSet<Guid> pending)
    {
        var total = Math.Max(1, (close - open).TotalMinutes);
        var bars = new List<AreaLoadBar>();
        for (var i = 0; i < day.Load.Count; i++)
        {
            var load = day.Load[i];
            var from = load.Start < open ? open : load.Start;
            var until = i + 1 < day.Load.Count ? day.Load[i + 1].Start : close;
            if (until > close)
            {
                until = close;
            }

            if (load.Occupied <= 0 || until <= from)
            {
                continue;
            }

            var share = Math.Min(1, load.Occupied / (double)Math.Max(1, day.Places));
            var height = Math.Max(3, TimelineHeight * share);
            bars.Add(new AreaLoadBar(
                TimelineWidth * (from - open).TotalMinutes / total,
                Math.Max(1, TimelineWidth * (until - from).TotalMinutes / total),
                TimelineHeight - height,
                height,
                load.Occupied >= day.Places,
                share >= 0.8,
                $"{DayLayout.TimeText(from, until, _zone)}: {load.Occupied} von {day.Places} belegt"));
        }

        var now = _time.GetUtcNow();
        var isToday = DateOnly.FromDateTime(DayLayout.Local(now, _zone)) == _date;
        var peak = day.Load.Select(l => l.Occupied).DefaultIfEmpty().Max();
        string summary;
        if (peak == 0)
        {
            summary = $"{day.Places} Plätze, noch frei";
        }
        else if (isToday && now >= open && now < close)
        {
            var current = day.Load.LastOrDefault(l => l.Start <= now)?.Occupied ?? 0;
            summary = $"jetzt {current} von {day.Places} Plätzen belegt · höchstens {peak}";
        }
        else
        {
            summary = $"höchstens {peak} von {day.Places} Plätzen belegt";
        }

        var visits = day.Visits.OrderBy(v => v.IsCancelled).ThenBy(v => v.From)
            .Select(v => new AreaVisitRow(v, _zone, CanBook, AskCancelAsync, _checkout, pending.Contains(v.Id))).ToList();
        return new AreaCard(day, summary, bars, visits, $"{DayLayout.Local(open, _zone):HH:mm}", $"{DayLayout.Local(close, _zone):HH:mm}");
    }

    /// <summary>Öffnungszeit des Tages; ohne Angabe 9 bis 21 Uhr, damit der Verlauf trotzdem lesbar bleibt.</summary>
    private (DateTimeOffset Open, DateTimeOffset Close) Window() => _opening.Count > 0
        ? (_opening.Min(o => o.Start), _opening.Max(o => o.End))
        : (DayLayout.LocalToUtc(_date, new TimeOnly(9, 0), _zone), DayLayout.LocalToUtc(_date, new TimeOnly(21, 0), _zone));

    private bool CanBookVisit => CanBook && !Busy && Area is not null && Search.HasCustomer && TryTimes(out _, out _);

    [RelayCommand(CanExecute = nameof(CanBookVisit))]
    private async Task BookAsync()
    {
        if (!TryTimes(out var from, out var until))
        {
            return;
        }

        Error = null;
        Info = null;
        Busy = true;
        var name = Search.Name;
        var note = string.IsNullOrWhiteSpace(Note) ? null : Note.Trim();
        var when = $"{Area!.Name} {DayLayout.TimeText(from, until, _zone)}";
        var result = await _api.BookAreaVisitAsync(new AreaVisitRequest(_locationId, Area.RoomId, Search.Customer!.Id, from, until, Persons, note),
            name, $"{name}, {DayLayout.DateText(_date)}: Eintritt {when}").ConfigureAwait(true);
        Busy = false;
        if (result.Value is null)
        {
            Error = result.Error;
            return;
        }

        Info = result.Queued
            ? $"Ohne Verbindung vorgemerkt: {name}, {when}. Ob Platz ist, prüft der Server beim Abgleich."
            : $"{name}: {when} gebucht.{(result.Value.Warnings is { Count: > 0 } warnings ? " " + string.Join(" ", warnings) : "")}";
        Search.Customer = null;
        Search.Query = "";
        Persons = 1;
        Note = null;
        await LoadAsync().ConfigureAwait(true);
    }

    /// <summary>Eintritt stornieren fragt nach (Rückfragedialog, Design-Brief 5.27).</summary>
    private async Task AskCancelAsync(AreaVisitDto visit)
    {
        var answer = await AskAsync(new ConfirmRequest("Eintritt stornieren?",
            $"Der Eintritt von {visit.CustomerName} ({DayLayout.TimeText(visit.From, visit.Until, _zone)}) ist danach storniert.", "Stornieren")).ConfigureAwait(true);
        if (answer.IsConfirmed)
        {
            await CancelAsync(visit).ConfigureAwait(true);
        }
    }

    private async Task CancelAsync(AreaVisitDto visit)
    {
        Error = null;
        Info = null;
        var result = await _api.PostAsync<AreaVisitDto>($"api/locations/{_locationId}/area-visits/{visit.Id}/cancel", null).ConfigureAwait(true);
        if (result.Value is null)
        {
            Error = result.Error;
            return;
        }

        Info = $"Eintritt von {visit.CustomerName} storniert.";
        await LoadAsync().ConfigureAwait(true);
    }

    [RelayCommand]
    private void Close() => _close();

    private bool TryTimes(out DateTimeOffset from, out DateTimeOffset until)
    {
        from = until = default;
        if (!TryTime(FromText, out var f) || !TryTime(UntilText, out var u) || u <= f)
        {
            return false;
        }

        from = DayLayout.LocalToUtc(_date, f, _zone);
        until = DayLayout.LocalToUtc(_date, u, _zone);
        return true;
    }

    private static bool TryTime(string? text, out TimeOnly time)
    {
        var parsed = TimeInputParser.TryParse(text);
        time = parsed.GetValueOrDefault();
        return parsed is not null;
    }
}
