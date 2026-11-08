using System.Globalization;
using AispaStudio.Contracts;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>Texte zu Kalender-Abos (ADR 0075): Art, Stand des Abrufs, Zustand und die Ferien als „bis 2.11.“. Statisch, damit Titel, Hinweis, Tagesüberblick und Liste dieselben Worte nutzen.</summary>
public static class SubscriptionTexts
{
    private static readonly CultureInfo German = CultureInfo.GetCultureInfo("de-AT");

    public static string Kind(SubscriptionKind kind) => kind switch
    {
        SubscriptionKind.PublicHoliday => "Gesetzliche Feiertage",
        SubscriptionKind.SchoolHoliday => "Schulferien",
        SubscriptionKind.Notice => "Hinweise",
        _ => "Sonstiges",
    };

    /// <summary>„Stand heute“, „Stand gestern“, „Stand vor 3 Tagen“ oder „Stand 12.10.2026“ nach Ortszeit; ohne Abruf „noch nicht abgerufen“.</summary>
    public static string Stand(DateTimeOffset? asOf, DateTimeOffset now, TimeZoneInfo zone)
    {
        if (asOf is not { } at)
        {
            return "noch nicht abgerufen";
        }

        var days = DateOnly.FromDateTime(DayLayout.Local(now, zone)).DayNumber - DateOnly.FromDateTime(DayLayout.Local(at, zone)).DayNumber;
        return days switch
        {
            <= 0 => "Stand heute",
            1 => "Stand gestern",
            <= 14 => $"Stand vor {days} Tagen",
            _ => $"Stand {DayLayout.Local(at, zone).ToString("dd.MM.yyyy", German)}",
        };
    }

    public static string Days(int count) => count == 1 ? "1 Tag" : $"{count} Tage";

    /// <summary>Eine Zeile zum Zustand eines Abos: Stand und Umfang, bei einem Fehler, dass die Kopie gilt.</summary>
    public static string Status(CalendarSubscriptionDto sub, DateTimeOffset now, TimeZoneInfo zone) => sub switch
    {
        { Url: "" } => sub.DayCount > 0 ? $"Nur Datei · {Days(sub.DayCount)}" : "Nur Datei · noch keine Datei importiert",
        { IsActive: false } => sub.DayCount > 0 ? $"Ausgeschaltet · Kopie mit {Days(sub.DayCount)}" : "Ausgeschaltet",
        { LastResult: SubscriptionFetchResult.Never } => "Noch nicht abgerufen",
        { LastResult: SubscriptionFetchResult.Failed } => sub.DayCount > 0
            ? $"Abruf fehlgeschlagen · es gilt die Kopie ({Stand(sub.LastFetchAt, now, zone)})"
            : "Abruf fehlgeschlagen · noch keine Kopie",
        _ => $"{Stand(sub.LastFetchAt, now, zone)} · {Days(sub.DayCount)}",
    };

    /// <summary>„Herbstferien bis 2.11.“ für Ferien, die über den angezeigten Tag hinausgehen; Feiertage und eintägige Einträge nur mit dem Namen.</summary>
    public static string WithUntil(CalendarDayFact fact, DateOnly day) =>
        fact.Until > day && fact.Kind != SubscriptionKind.PublicHoliday
            ? $"{fact.Title} bis {(fact.Until.Year == day.Year ? fact.Until.ToString("d.M.", German) : fact.Until.ToString("d.M.yyyy", German))}"
            : fact.Title;
}

/// <summary>
/// Eine Zeile in der Liste unter dem Chip „Kalender“ (ADR 0075): ein Abo mit dem Schalter „Zeigen“ (für diesen Zugang, wirkt sofort), bei der Leitung zusätzlich
/// „Aktiv (für alle)“ und für jede Person „Falsch melden“. Die Schalter rufen den Server und stellen sich bei einem Fehler zurück.
/// </summary>
public sealed partial class SubscriptionRowViewModel : ObservableObject
{
    /// <summary>Hinweis bei Abos für gesetzliche Feiertage: Sie sind nur eine Information (Ä50: Feiertage ändern keine Buchung), ein- und ausschalten darf nur die Leitung.</summary>
    public const string HolidayNote = "Nur zur Information, ändert keine Buchung; aktiv/inaktiv nur Leitung";

    private readonly Func<SubscriptionRowViewModel, bool, Task<bool>> _setShow;
    private readonly Func<SubscriptionRowViewModel, bool, Task<bool>> _setActive;
    private bool _applying;

    public SubscriptionRowViewModel(CalendarSubscriptionDto dto, string status, bool canManage, Func<SubscriptionRowViewModel, bool, Task<bool>> setShow,
        Func<SubscriptionRowViewModel, bool, Task<bool>> setActive, Func<SubscriptionRowViewModel, Task> report)
    {
        Dto = dto;
        Status = status;
        CanManage = canManage;
        _setShow = setShow;
        _setActive = setActive;
        _applying = true;
        Show = dto.ShowInCalendar;
        IsActive = dto.IsActive;
        _applying = false;
        ReportCommand = new AsyncRelayCommand(() => report(this));
    }

    public CalendarSubscriptionDto Dto { get; private set; }

    public Guid Id => Dto.Id;

    public string Name => Dto.Name;

    /// <summary>„Gesetzliche Feiertage · Stand gestern · 136 Tage“.</summary>
    public string KindAndStatus => $"{SubscriptionTexts.Kind(Dto.Kind)} · {Status}";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(KindAndStatus), nameof(HasError))]
    public partial string Status { get; private set; }

    public bool IsHoliday => Dto.Kind == SubscriptionKind.PublicHoliday;

    public bool HasError => Dto is { IsActive: true, LastResult: SubscriptionFetchResult.Failed };

    public string? ErrorText => Dto.LastError;

    /// <summary>Nur die Leitung schaltet ein oder aus, und nur Abos ihrer Standorte (ein Abo für alle verlangt alle Standorte).</summary>
    public bool CanManage { get; }

    /// <summary>Der Zugang sieht das Abo im Kalender; wirkt nur auf die Anzeige, nie auf Buchung, Öffnungszeiten oder Assistent.</summary>
    [ObservableProperty]
    public partial bool Show { get; set; }

    /// <summary>Für alle aktiv; ausgeschaltet gilt das Abo nirgends. Nur die Leitung ändert es.</summary>
    [ObservableProperty]
    public partial bool IsActive { get; set; }

    public string ShowName => $"{Name} im Kalender zeigen";

    public string ActiveName => $"{Name} aktiv, für alle";

    public IAsyncRelayCommand ReportCommand { get; }

    partial void OnShowChanged(bool value)
    {
        if (!_applying)
        {
            _ = ChangeAsync(value, _setShow, v => Show = v);
        }
    }

    partial void OnIsActiveChanged(bool value)
    {
        if (!_applying)
        {
            _ = ChangeAsync(value, _setActive, v => IsActive = v);
        }
    }

    /// <summary>Schickt die Änderung an den Server; bei einem Fehler springt der Schalter zurück (ohne einen zweiten Aufruf).</summary>
    private async Task ChangeAsync(bool value, Func<SubscriptionRowViewModel, bool, Task<bool>> send, Action<bool> set)
    {
        if (await send(this, value).ConfigureAwait(true))
        {
            return;
        }

        _applying = true;
        set(!value);
        _applying = false;
    }

    /// <summary>Übernimmt die Antwort des Servers, ohne die Schalter erneut auszulösen.</summary>
    public void Apply(CalendarSubscriptionDto dto, string status)
    {
        _applying = true;
        Dto = dto;
        Show = dto.ShowInCalendar;
        IsActive = dto.IsActive;
        Status = status;
        _applying = false;
        OnPropertyChanged(nameof(HasError));
        OnPropertyChanged(nameof(ErrorText));
    }
}
