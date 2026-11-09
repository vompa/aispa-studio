using System.Globalization;
using AispaStudio.Contracts;

namespace AispaStudio.Desktop.ViewModels;

public sealed partial class ActivityViewModel
{
    /// <summary>Beim Öffnen der Seite: Personen des Standorts einmal laden, die Liste immer neu (es ist inzwischen etwas passiert).</summary>
    public async Task RefreshAsync()
    {
        _initialization ??= LoadPeopleAsync();
        await _initialization.ConfigureAwait(true);
        await ReloadAsync().ConfigureAwait(true);
    }

    /// <summary>Die Personen mit Zugang zum Standort für das Feld „Person“; ohne Recht auf Zugänge oder ohne Verbindung bleibt das Feld weg.</summary>
    private async Task LoadPeopleAsync()
    {
        if (!_user.Rights.Contains(AccessRight.ManageStaff) || LocationSource?.Invoke() is not { } location)
        {
            return;
        }

        var staff = await _api.GetAsync<List<StaffMemberDto>>("api/staff").ConfigureAwait(true);
        if (staff.Value is null)
        {
            return;
        }

        foreach (var member in staff.Value.Where(m => m.IsActive && (m.AllLocations || m.LocationIds.Contains(location.Id))).OrderBy(m => m.DisplayName, StringComparer.CurrentCultureIgnoreCase))
        {
            People.Add(new ActivityPersonOption(member.UserId, member.DisplayName));
        }

        OnPropertyChanged(nameof(HasPersonChoice));
    }

    /// <summary>
    /// „Storniert n“ im Kalender: der Verlauf zeigt genau diesen Tag und nur die Art <paramref name="kind"/>; alle anderen Filter sind aus.
    /// Die Filter stehen sofort, auch bevor die Antwort da ist.
    /// </summary>
    public async Task ShowDayAsync(DateOnly day, ActivityKind kind)
    {
        _searchVersion++;
        _suspended = true;
        Query = "";
        Person = People[0];
        Channel = Channels[0];
        foreach (var chip in KindChips)
        {
            chip.IsOn = chip.Kinds.Contains(kind);
        }

        CustomFrom = CustomUntil = day;
        Period = ActivityPeriod.Custom;
        _suspended = false;
        OnPropertyChanged(nameof(HasFilter));
        OnPropertyChanged(nameof(IsModified));
        OnPropertyChanged(nameof(EmptyText));
        _initialization ??= LoadPeopleAsync();
        await _initialization.ConfigureAwait(true);
        await ReloadAsync().ConfigureAwait(true);
    }

    /// <summary>Lädt die erste Seite mit den gesetzten Filtern; eine ältere, langsamere Antwort überschreibt keine neuere.</summary>
    public async Task ReloadAsync()
    {
        if (LocationSource?.Invoke() is not { } location)
        {
            return;
        }

        var (from, until) = Range();
        if (until < from)
        {
            Error = "Das Enddatum liegt vor dem Startdatum.";
            return;
        }

        var version = ++_version;
        _loaded = true;
        IsLoading = true;
        Error = null;
        var result = await _api.GetAsync<ActivityPage>(HistoryPath(location, from, until, before: null)).ConfigureAwait(true);
        if (version != _version)
        {
            return;
        }

        IsLoading = false;
        DropPanel();
        Rows.Clear();
        Selected = null;
        if (result.Value is not { } page)
        {
            Error = result.Error ?? "Der Verlauf konnte nicht geladen werden.";
            HasMore = false;
            Refreshed();
            return;
        }

        Append(page, location);
        Refreshed();
    }

    /// <summary>Hängt die nächste Seite an (beim Scrollen ans Ende und über „Weitere laden“); Einträge desselben Tages kommen in die bestehende Gruppe.</summary>
    [CommunityToolkit.Mvvm.Input.RelayCommand]
    public async Task LoadMoreAsync()
    {
        if (!HasMore || IsLoading || _nextBefore is not { } before || LocationSource?.Invoke() is not { } location)
        {
            return;
        }

        var (from, until) = Range();
        var version = _version;
        IsLoading = true;
        var result = await _api.GetAsync<ActivityPage>(HistoryPath(location, from, until, before)).ConfigureAwait(true);
        if (version != _version)
        {
            return;
        }

        IsLoading = false;
        if (result.Value is not { } page)
        {
            Error = result.Error ?? "Der Verlauf konnte nicht geladen werden.";
            Refreshed();
            return;
        }

        Append(page, location);
        Refreshed();
    }

    private void Refreshed()
    {
        OnPropertyChanged(nameof(ShowLoading));
        OnPropertyChanged(nameof(ShowEmpty));
        OnPropertyChanged(nameof(ShowMore));
        OnPropertyChanged(nameof(EmptyText));
    }

    private string HistoryPath(LocationDto location, DateOnly from, DateOnly until, long? before)
    {
        var query = new List<string>
        {
            $"from={from:yyyy-MM-dd}",
            $"until={until:yyyy-MM-dd}",
        };
        query.AddRange(KindChips.Where(c => c.IsOn).SelectMany(c => c.Kinds).Select(k => $"kinds={k}"));
        if (Person.UserId is { } actor)
        {
            query.Add($"actor={Uri.EscapeDataString(actor)}");
        }

        if (Channel.Channel is { } channel)
        {
            query.Add($"channel={channel}");
        }

        if (Query.Trim() is { Length: > 0 } search)
        {
            query.Add($"search={Uri.EscapeDataString(search)}");
        }

        if (before is { } cursor)
        {
            query.Add($"before={cursor.ToString(CultureInfo.InvariantCulture)}");
        }

        return $"api/locations/{location.Id}/history?{string.Join('&', query)}";
    }

    private void Append(ActivityPage page, LocationDto location)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById(location.TimeZone);
        var today = Today();
        var lastDay = Rows.OfType<ActivityDayHeader>().LastOrDefault()?.Day;
        foreach (var entry in page.Items)
        {
            var local = DayLayout.Local(entry.At, zone);
            var day = DateOnly.FromDateTime(local);
            if (lastDay != day)
            {
                Rows.Add(new ActivityDayHeader(day, DayTitle(day, today)));
                lastDay = day;
            }

            Rows.Add(new ActivityRow(entry, local.ToString("HH:mm", CultureInfo.InvariantCulture), DetailOf(entry)));
        }

        _nextBefore = page.NextBefore;
        HasMore = page.NextBefore is not null;
    }

    private static string? DetailOf(ActivityEntry entry)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(entry.CustomerName))
        {
            parts.Add(entry.CustomerName);
        }

        if (!string.IsNullOrWhiteSpace(entry.Actor))
        {
            parts.Add($"von {entry.Actor}");
        }

        return parts.Count == 0 ? null : string.Join(" · ", parts);
    }

    /// <summary>„Heute, Montag, 9. November“, „Gestern, Sonntag, 8. November“, sonst „Freitag, 6. November“; in einem anderen Jahr mit Jahreszahl.</summary>
    private static string DayTitle(DateOnly day, DateOnly today)
    {
        var text = day.ToString(day.Year == today.Year ? "dddd, d. MMMM" : "dddd, d. MMMM yyyy", German);
        return day == today ? $"Heute, {text}" : day == today.AddDays(-1) ? $"Gestern, {text}" : text;
    }
}
