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
    /// <summary>Wirkung und Grund für den Tooltip am Chip.</summary>
    public string Tip => string.IsNullOrWhiteSpace(Reason) ? Effect : $"{Effect}\nGrund: {Reason}";
}

// ---------- Kennzeichen mit Wirkung: mehr Zeit, Leitung informieren, Nicht bei mir, Nicht mehr buchen ----------
public partial class CustomersViewModel
{
    /// <summary>Angemeldete Person; bestimmt, welche Kennzeichen angeboten werden. Der Server prüft dasselbe.</summary>
    public CurrentUser? User { get; init; }

    public ObservableCollection<FlagRow> Flags { get; } = [];

    public ObservableCollection<FlagChoice> FlagChoices { get; } = [];

    public bool CanAddFlag => FlagChoices.Count > 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEditingAny))]
    public partial bool IsAddingFlag { get; private set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveFlagCommand))]
    [NotifyPropertyChangedFor(nameof(FlagHint))]
    public partial FlagChoice? NewFlag { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveFlagCommand))]
    [NotifyPropertyChangedFor(nameof(FlagReasonWarning))]
    public partial string NewFlagReason { get; set; } = "";

    public string? FlagReasonWarning => HealthWords.Warning(NewFlagReason);

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
        var flags = (await api.GetAsync<List<CustomerFlagDto>>($"api/customers/{customer.Id}/flags").ConfigureAwait(true)).Value;
        if (version == _detailVersion)
        {
            ShowFlags(flags ?? []);
        }
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
        NewFlag = FlagChoices.FirstOrDefault();
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
