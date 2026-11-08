using System.Collections.ObjectModel;
using System.Globalization;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>Wochentag zum An- und Abwählen in den Buchungsvorlieben.</summary>
public sealed partial class WeekdayChoice(DayOfWeek day, string label) : ObservableObject
{
    public DayOfWeek Day { get; } = day;

    public string Label { get; } = label;

    [ObservableProperty]
    public partial bool IsChecked { get; set; }
}

/// <summary>Auswahl der Therapeut:in; <c>Id</c> null heißt „keine Vorliebe“.</summary>
public sealed record EmployeeChoice(Guid? Id, string Label)
{
    public override string ToString() => Label;
}

// ---------- Buchungsvorlieben: feste oder bevorzugte Therapeut:in, Wunschtage und -zeiten ----------
public partial class CustomersViewModel
{
    private Dictionary<Guid, string>? _employeeNames;

    public ObservableCollection<EmployeeChoice> EmployeeChoices { get; } = [];

    public ObservableCollection<WeekdayChoice> Weekdays { get; } =
    [
        .. new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday }
            .Select(d => new WeekdayChoice(d, CultureInfo.GetCultureInfo("de-AT").DateTimeFormat.GetAbbreviatedDayName(d).TrimEnd('.'))),
    ];

    public string BookingText =>
        DayLayout.BookingPreferenceText(Selected?.Booking, id => _employeeNames?.GetValueOrDefault(id))
        ?? "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEditingAny))]
    public partial bool IsEditingBooking { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSuggestion))]
    public partial CustomerPreferenceSuggestion? Suggestion { get; private set; }

    public bool HasSuggestion => Suggestion?.Text is not null;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveBookingCommand))]
    [NotifyPropertyChangedFor(nameof(CanBeFixed))]
    public partial EmployeeChoice? BookingEmployee { get; set; }

    public bool CanBeFixed => BookingEmployee?.Id is not null;

    [ObservableProperty]
    public partial bool BookingFixed { get; set; }

    /// <summary>Wunsch „Therapeutin“ oder „Therapeut“.</summary>
    [ObservableProperty]
    public partial GenderChoice BookingGender { get; set; } = GenderChoice.All[0];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveBookingCommand))]
    public partial string BookingFrom { get; set; } = "";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveBookingCommand))]
    public partial string BookingUntil { get; set; } = "";

    partial void OnBookingEmployeeChanged(EmployeeChoice? value)
    {
        if (value?.Id is null)
        {
            BookingFixed = false;
        }
    }

    private async Task LoadEmployeesAsync()
    {
        if (_employeeNames is not null)
        {
            return;
        }

        var employees = (await api.GetAsync<List<EmployeeDto>>("api/employees").ConfigureAwait(true)).Value;
        if (employees is null)
        {
            return;
        }

        _employeeNames = employees.ToDictionary(e => e.Id, e => e.InHouseName());
        EmployeeChoices.Clear();
        EmployeeChoices.Add(new EmployeeChoice(null, "Keine Vorliebe"));
        foreach (var e in employees.OrderBy(e => e.InHouseName(), StringComparer.CurrentCulture))
        {
            EmployeeChoices.Add(new EmployeeChoice(e.Id, e.InHouseName()));
        }

        OnPropertyChanged(nameof(BookingText));
    }

    /// <summary>Vorschlag aus den bisherigen Besuchen, z. B. „4 von 5 Besuchen bei Lena · meist Dienstag“.</summary>
    private async Task LoadSuggestionAsync(CustomerDto customer, int version)
    {
        await LoadEmployeesAsync().ConfigureAwait(true);
        var result = await api.GetAsync<CustomerPreferenceSuggestion>($"api/customers/{customer.Id}/booking-preferences/suggestion").ConfigureAwait(true);
        if (version == _detailVersion)
        {
            Suggestion = result.Value;
        }
    }

    [RelayCommand]
    private async Task EditBookingAsync()
    {
        CloseEditors();
        var generation = _editorGeneration;
        await LoadEmployeesAsync().ConfigureAwait(true);
        if (generation != _editorGeneration)
        {
            return;
        }

        Fill(Selected?.Booking);
        IsEditingBooking = true;
    }

    /// <summary>Übernimmt den Vorschlag ins Formular; gespeichert wird erst mit „Speichern“.</summary>
    [RelayCommand]
    private async Task TakeSuggestionAsync()
    {
        if (Suggestion is not { } s)
        {
            return;
        }

        CloseEditors();
        var generation = _editorGeneration;
        await LoadEmployeesAsync().ConfigureAwait(true);
        if (generation != _editorGeneration)
        {
            return;
        }

        Fill(new CustomerBookingPreferences(
            s.EmployeeId ?? Selected?.Booking?.EmployeeId,
            Selected?.Booking?.EmployeeFixed ?? false,
            s.Weekday is { } day ? [day] : Selected?.Booking?.Weekdays,
            s.From ?? Selected?.Booking?.From,
            s.Until ?? Selected?.Booking?.Until,
            Selected?.Booking?.Gender));
        IsEditingBooking = true;
    }

    private void Fill(CustomerBookingPreferences? p)
    {
        BookingEmployee = EmployeeChoices.FirstOrDefault(c => c.Id == p?.EmployeeId) ?? EmployeeChoices.FirstOrDefault();
        BookingFixed = p?.EmployeeFixed == true && p.EmployeeId is not null;
        BookingGender = GenderChoice.All.First(g => g.Value == p?.Gender);
        foreach (var day in Weekdays)
        {
            day.IsChecked = p?.Weekdays?.Contains(day.Day) == true;
        }

        BookingFrom = p?.From?.ToString("HH:mm", CultureInfo.InvariantCulture) ?? "";
        BookingUntil = p?.Until?.ToString("HH:mm", CultureInfo.InvariantCulture) ?? "";
    }

    [RelayCommand]
    private void CancelBooking() => IsEditingBooking = false;

    /// <summary>Leer ist erlaubt (keine Angabe), sonst muss die Uhrzeit lesbar sein.</summary>
    private static bool TryTime(string text, out TimeOnly? value)
    {
        var result = TimeInputParser.Parse(text);
        value = result.Value;
        return result.IsValid;
    }

    private bool CanSaveBooking => TryTime(BookingFrom, out var from) && TryTime(BookingUntil, out var until) && (from is null || until is null || from < until);

    [RelayCommand(CanExecute = nameof(CanSaveBooking))]
    private Task SaveBookingAsync()
    {
        TryTime(BookingFrom, out var from);
        TryTime(BookingUntil, out var until);
        var days = Weekdays.Where(d => d.IsChecked).Select(d => d.Day).ToList();
        return StoreBookingAsync(new CustomerBookingPreferences(BookingEmployee?.Id, BookingFixed && BookingEmployee?.Id is not null,
            days.Count == 0 ? null : days, from, until, BookingGender.Value));
    }

    /// <summary>„Entfernen“ fragt nach (Rückfragedialog, Design-Brief 5.27).</summary>
    [RelayCommand]
    private async Task AskRemoveBookingAsync()
    {
        var answer = await AskAsync(new ConfirmRequest("Buchungsvorlieben entfernen?",
            "Wunschperson, Tage, Zeiten und Geschlecht der Behandelnden gelten danach nicht mehr.", "Entfernen")).ConfigureAwait(true);
        if (answer.IsConfirmed)
        {
            await RemoveBookingAsync().ConfigureAwait(true);
        }
    }

    [RelayCommand]
    private Task RemoveBookingAsync() => StoreBookingAsync(new CustomerBookingPreferences());

    private async Task StoreBookingAsync(CustomerBookingPreferences preferences)
    {
        if (Selected is not { } customer)
        {
            return;
        }

        var result = await api.PutAsync<CustomerDto>($"api/customers/{customer.Id}/booking-preferences", preferences).ConfigureAwait(true);
        if (result.Value is not { } updated)
        {
            Error = result.Error;
            return;
        }

        Error = null;
        IsEditingBooking = false;
        Replace(customer, updated);
    }
}
