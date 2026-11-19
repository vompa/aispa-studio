using System.Collections.ObjectModel;
using System.Globalization;
using AispaStudio.Contracts;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>Kennzeichen, das die angemeldete Person setzen darf.</summary>
public sealed record FlagChoice(CustomerFlagKind Kind, string Label, bool NeedsReason)
{
    public override string ToString() => Label;
}

/// <summary>Gesetztes Kennzeichen in der Kundenkarte, z. B. „Nicht bei Lena Huber · seit 3.10.2026“.</summary>
public sealed record FlagRow(CustomerFlagDto Flag, string Title, string Effect, string? Reason, bool MayRemove)
{
    /// <summary>Wirkung und Grund für den Tooltip am Chip; ohne Kreuz steht dort, wer es aufheben darf.</summary>
    public string Tip => (string.IsNullOrWhiteSpace(Reason) ? Effect : $"{Effect}\nGrund: {Reason}") + (MayRemove ? "" : $"\n{RemoveHint}");

    /// <summary>Warum der Chip kein Kreuz hat: „Nicht bei mir“ hebt die Person selbst oder die Leitung auf, die anderen Kennzeichen das Recht, sie zu setzen.</summary>
    public string RemoveHint => Flag.Kind switch
    {
        CustomerFlagKind.NotWithMe => $"Aufheben kann nur {Flag.EmployeeName ?? "die Person"} selbst oder die Leitung.",
        CustomerFlagKind.MoreTime => "Aufheben darf, wer Termine buchen darf.",
        _ => "Aufheben darf nur die Leitung.",
    };
}

/// <summary>Vorschlag des Servers für ein Kennzeichen, z. B. „Vorschlag: Leitung informieren“ mit Begründung aus dem Verlauf.</summary>
public sealed record FlagSuggestionRow(CustomerFlagSuggestion Suggestion, string Title, string Evidence)
{
    public string Heading => $"Vorschlag: {Title}";
}

// ---------- Kennzeichen mit Wirkung: mehr Zeit, Leitung informieren, Nicht bei mir, Nicht mehr buchen ----------
public partial class CustomersViewModel
{
    /// <summary>Angemeldete Person; bestimmt, welche Kennzeichen angeboten werden. Der Server prüft dasselbe.</summary>
    public CurrentUser? User { get; init; }

    public ObservableCollection<FlagRow> Flags { get; } = [];

    public ObservableCollection<FlagChoice> FlagChoices { get; } = [];

    /// <summary>Vorschläge aus dem Verlauf (nur Kennzeichen, die die Person setzen darf); gesetzt wird erst mit „Übernehmen“.</summary>
    public ObservableCollection<FlagSuggestionRow> FlagSuggestions { get; } = [];

    public bool CanAddFlag => FlagChoices.Count > 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEditingAny))]
    public partial bool IsAddingFlag { get; private set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveFlagCommand))]
    [NotifyPropertyChangedFor(nameof(FlagHint), nameof(IsReasonRequired))]
    public partial FlagChoice? NewFlag { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveFlagCommand))]
    [NotifyPropertyChangedFor(nameof(FlagReasonWarning))]
    public partial string NewFlagReason { get; set; } = "";

    public string? FlagReasonWarning => HealthWords.Warning(NewFlagReason);

    /// <summary>„Nicht mehr buchen“ verlangt einen Grund; das Feld trägt dann den Pflichtstern (masken.md R8).</summary>
    public bool IsReasonRequired => NewFlag?.NeedsReason == true;

    public string FlagHint => NewFlag is { } choice ? Effect(choice.Kind, User?.DisplayName) : "";

    public static string Title(CustomerFlagDto f) => f.Kind switch
    {
        CustomerFlagKind.MoreTime => "Braucht mehr Zeit",
        CustomerFlagKind.InformManagement => "Leitung informieren",
        CustomerFlagKind.NotWithMe => $"Nicht bei {f.EmployeeName ?? "einer Person"}",
        _ => "Nicht mehr buchen",
    };

    public static string Effect(CustomerFlagKind kind, string? employee) => kind switch
    {
        CustomerFlagKind.MoreTime => "Jeder neue Termin bekommt 10 Minuten Puffer danach.",
        CustomerFlagKind.InformManagement => "Im Kalender steht „L“ am Termin; die Rezeption sagt der Leitung Bescheid, wenn der Gast kommt.",
        CustomerFlagKind.NotWithMe => $"Kalender, Terminannahme und Online-Buchung schlagen {employee ?? "diese Person"} für den Gast nicht mehr vor. Den Grund sehen nur die Person selbst und die Leitung.",
        _ => "Online-Buchung und KI-Terminannahme buchen nicht und geben an die Rezeption ab. Die Rezeption kann trotzdem buchen.",
    };

    private void FillFlagChoices()
    {
        FlagChoices.Clear();
        var rights = User?.Rights ?? [];
        if (rights.Contains(AccessRight.Book))
        {
            FlagChoices.Add(new FlagChoice(CustomerFlagKind.MoreTime, "Braucht mehr Zeit", false));
        }

        if (User?.EmployeeId is not null)
        {
            FlagChoices.Add(new FlagChoice(CustomerFlagKind.NotWithMe, "Nicht bei mir", false));
        }

        if (rights.Contains(AccessRight.ManageLocation))
        {
            FlagChoices.Add(new FlagChoice(CustomerFlagKind.InformManagement, "Leitung informieren", false));
            FlagChoices.Add(new FlagChoice(CustomerFlagKind.DoNotBook, "Nicht mehr buchen", true));
        }

        OnPropertyChanged(nameof(CanAddFlag));
    }

    private async Task LoadFlagsAsync(CustomerDto customer, int version)
    {
        var flagsTask = api.GetAsync<List<CustomerFlagDto>>($"api/customers/{customer.Id}/flags");
        var suggestionsTask = api.GetAsync<List<CustomerFlagSuggestion>>($"api/customers/{customer.Id}/flags/suggestions");
        var flags = (await flagsTask.ConfigureAwait(true)).Value;
        var suggestions = (await suggestionsTask.ConfigureAwait(true)).Value;
        if (version == _detailVersion)
        {
            ShowSuggestions(suggestions ?? []);
            ShowFlags(flags ?? []);
        }
    }

    private void ShowSuggestions(IEnumerable<CustomerFlagSuggestion> suggestions)
    {
        FlagSuggestions.Clear();
        foreach (var s in suggestions)
        {
            FlagSuggestions.Add(new FlagSuggestionRow(s, Title(new CustomerFlagDto(Guid.Empty, s.Kind, null, null, null, default, false)), s.Evidence));
        }
    }

    /// <summary>Übernimmt den Vorschlag: setzt das Kennzeichen mit der Begründung des Servers als Grund.</summary>
    [RelayCommand]
    private async Task AcceptFlagSuggestionAsync(FlagSuggestionRow? row)
    {
        if (Selected is not { } customer || row is null)
        {
            return;
        }

        var result = await api.PostAsync<List<CustomerFlagDto>>($"api/customers/{customer.Id}/flags",
            new CustomerFlagInput(row.Suggestion.Kind, null, row.Evidence)).ConfigureAwait(true);
        if (result.Value is not { } flags)
        {
            Error = result.Error;
            return;
        }

        Error = null;
        FlagSuggestions.Remove(row);
        ShowFlags(flags);
    }

    /// <summary>Verwirft den Vorschlag; der Server zeigt ihn für diesen Gast ein halbes Jahr nicht mehr.</summary>
    [RelayCommand]
    private async Task DismissFlagSuggestionAsync(FlagSuggestionRow? row)
    {
        if (Selected is not { } customer || row is null)
        {
            return;
        }

        var result = await api.PostAsync<object>($"api/customers/{customer.Id}/flags/suggestions/{row.Suggestion.Kind}/dismiss", null).ConfigureAwait(true);
        if (result.Error is { } error)
        {
            Error = error;
            return;
        }

        Error = null;
        FlagSuggestions.Remove(row);
        RaiseSections();
    }

    private void ShowFlags(IEnumerable<CustomerFlagDto> flags)
    {
        Flags.Clear();
        foreach (var f in flags)
        {
            Flags.Add(new FlagRow(f, Title(f), Effect(f.Kind, f.EmployeeName),
                f.Reason is { } reason ? $"Grund: {reason} · seit {f.Since.ToString("d.M.yyyy", CultureInfo.InvariantCulture)}"
                    : $"seit {f.Since.ToString("d.M.yyyy", CultureInfo.InvariantCulture)}",
                f.MayRemove));
        }

        RaiseSections();
    }

    [RelayCommand]
    private void AddFlag()
    {
        CloseEditors();
        FillFlagChoices();
        // Vorbelegt mit dem ersten Kennzeichen, das noch nicht gesetzt ist; sind alle gesetzt, mit dem ersten.
        NewFlag = FlagChoices.FirstOrDefault(c => Flags.All(f => f.Flag.Kind != c.Kind)) ?? FlagChoices.FirstOrDefault();
        NewFlagReason = "";
        IsAddingFlag = true;
    }

    [RelayCommand]
    private void CancelFlag() => IsAddingFlag = false;

    private bool CanSaveFlag => NewFlag is { } choice && (!choice.NeedsReason || NewFlagReason.Trim().Length > 0);

    [RelayCommand(CanExecute = nameof(CanSaveFlag))]
    private async Task SaveFlagAsync()
    {
        if (Selected is not { } customer || NewFlag is not { } choice)
        {
            return;
        }

        var reason = NewFlagReason.Trim();
        var result = await api.PostAsync<List<CustomerFlagDto>>($"api/customers/{customer.Id}/flags",
            new CustomerFlagInput(choice.Kind, null, reason.Length == 0 ? null : reason)).ConfigureAwait(true);
        if (result.Value is not { } flags)
        {
            Error = result.Error;
            return;
        }

        Error = null;
        IsAddingFlag = false;
        ShowFlags(flags);
    }

    [RelayCommand]
    private async Task RemoveFlagAsync(FlagRow? row)
    {
        if (Selected is not { } customer || row is null)
        {
            return;
        }

        var result = await api.DeleteAsync<List<CustomerFlagDto>>($"api/customers/{customer.Id}/flags/{row.Flag.Id}").ConfigureAwait(true);
        if (result.Value is not { } flags)
        {
            Error = result.Error;
            return;
        }

        Error = null;
        ShowFlags(flags);
    }
}
